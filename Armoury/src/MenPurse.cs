using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// SAKIEWKA LUDZI (wpis 84; Jeff 05.10: "70% do wojska, to wojsko potem sklada lepszy sprzet, a stary powinno sprzedac przy
    /// pierwszej wizycie w miescie i potem wydaje na kurwy, alkohol, zabawe, naprawe sprzetu, jedzenie i polepszenie sprzetu";
    /// "kupiec da najwyzej polowe, to z trupa zdjete" -> cena skupu TEJ sztuki W TYM STANIE; "naprawy: maja kase, niech
    /// naprawiaja, na ile ich stac"; "tak robimy, dzialaj").
    ///
    /// Historycznie ("trzecie" z umow Edwarda III): lup nalezal do zdobywcy, kapitan bral trzecia z lupu swoich ludzi.
    /// Gracz swoja trzecia bierze na ekranie lupow (wpis 82), reszta jest LUDZI.
    ///  - Wejscie do MIASTA: zbrojownia oddaje kupcowi to, co ponad komplet ludzi + SurplusKeepPercent zapasu (najgorsze sztuki;
    ///    wklady gracza nietkniete). Kupiec placi cene skupu tej sztuki (stan, polka) - nie wiecej niz ma w kasie.
    ///    Gracz: wszystko do sakiewki ludzi. Lord AI: trzecia (LordLootThirdPercent) do kiesy, reszta do sakiewki.
    ///  - Ludzie wydaja: naprawy (godzinowo u kowali miasta, TroopSelfMend) -> braki w kompletach -> reszta na zycie w miescie
    ///    przy wyjezdzie (kasa miasta). Zostaje tylko tyle, ile potrzeba na zalegle naprawy.
    ///  - Gracz bierze cos z nadwyzek zbrojowni = KUPUJE od ludzi po cenie skupu (rozliczenie przy zamknieciu ekranu).
    /// K1 (Jeff 09.10, docs/paczki/K1-dozbrajanie.md): przy wyjezdzie czesc odkladaja na lepszy sprzet (A3, limit dni zoldu); w miescie
    /// nadwyzki -> naprawy -> braki -> lepsze (MenUpgrade); nadwyzki i braki liczone po DOPASOWANIU (skill), nie po liczbie sztuk;
    /// zalogi maja wlasna sakiewke (czesc zoldu - SoldierPay) w tym samym slowniku i tym samym zapisie "arm_menpurse".
    /// </summary>
    internal static class MenPurse
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.MenPurseEnabled; } }

        private static readonly Dictionary<string, int> _purse = new Dictionary<string, int>();
        // K1 (przeglad, A3): ile z sakiewki ludzie juz odlozyli na lepszy sprzet (czesc sakiewki, nie osobne pieniadze); zapis "arm_mensaved"
        private static readonly Dictionary<string, int> _saved = new Dictionary<string, int>();
        private static int _daySold, _dayGold, _dayLord, _dayLife, _dayGear, _dayStamp = -1;
        private static int _daySaved, _dayGarSold, _dayGarGold;   // K1: odlozone na lepszy sprzet przy wyjazdach (A3), nadwyzki zalog (A9)
        private static long _dayCloth;         // 150: odziez wojska kupiona z sakiewek przy wyjezdzie z miasta (ArmyClothing.BuyForParty)
        private static long _dayWage;          // zold wplacony do sakiewek (SoldierPay)
        private static int _dayWageN;
        private static long _dayIn, _dayOut;   // ruch wszystkich sakiewek w dobie: kazda wplata i kazdy wydatek ida przez Add

        internal static void Reset() { _purse.Clear(); _saved.Clear(); _pending.Clear(); _sigStock = _sigArm = _sigRoster = int.MinValue; _daySold = _dayGold = _dayLord = _dayLife = _dayGear = 0; _daySaved = _dayGarSold = _dayGarGold = 0; _dayCloth = 0; _dayWage = 0; _dayWageN = 0; _dayIn = _dayOut = 0; _dayStamp = -1; }

        internal static string Export()
        {
            var parts = new List<string>();
            foreach (var kv in _purse) if (kv.Value > 0) parts.Add(kv.Key + "=" + kv.Value);
            return string.Join(";", parts.ToArray());
        }

        internal static void Import(string s)
        {
            _purse.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (var p in s.Split(';'))
            {
                var a = p.Split('='); int v;
                if (a.Length == 2 && int.TryParse(a[1], out v) && v > 0) _purse[a[0]] = v;
            }
        }

        /// <summary>K1 (przeglad, A3): oszczednosci na sprzet - zapis i odczyt (ten sam format co sakiewki).</summary>
        internal static string ExportSaved()
        {
            var parts = new List<string>();
            foreach (var kv in _saved) if (kv.Value > 0) parts.Add(kv.Key + "=" + kv.Value);
            return string.Join(";", parts.ToArray());
        }

        internal static void ImportSaved(string s)
        {
            _saved.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (var p in s.Split(';'))
            {
                var a = p.Split('='); int v;
                if (a.Length == 2 && int.TryParse(a[1], out v) && v > 0) _saved[a[0]] = v;
            }
        }

        private static int SavedOf(MobileParty mp) { int v; var k = Key(mp); return k != null && _saved.TryGetValue(k, out v) ? v : 0; }
        private static void SetSaved(MobileParty mp, int v) { var k = Key(mp); if (k == null) return; if (v > 0) _saved[k] = v; else _saved.Remove(k); }

        private static string Key(MobileParty mp) { return mp != null ? mp.StringId : null; }
        internal static bool HorseKind(ItemObject it) { return it != null && (it.ItemType == ItemObject.ItemTypeEnum.Horse || it.ItemType == ItemObject.ItemTypeEnum.HorseHarness); }

        /// <summary>wpis 89 (audyt): rozbita partia - sakiewka ludzi idzie do zwyciezcy (lup), inaczej do najblizszego miasta; nic nie znika.</summary>
        internal static void OnPartyDestroyed(MobileParty mp, TaleWorlds.CampaignSystem.Party.PartyBase destroyer)
        {
            try
            {
                if (mp == null) return;
                AiWear.Forget(mp); AiGear.Forget(mp); MenUpgrade.Forget(mp); SetSaved(mp, 0);
                int purse = Get(mp);
                if (purse <= 0) return;
                Take(mp, purse);
                var win = destroyer != null ? destroyer.MobileParty : null;
                if (win != null && win.LeaderHero != null && win.LeaderHero.IsAlive)
                {
                    if (win.IsMainParty) Add(win, purse);   // ludzie gracza - do ich sakiewki
                    else { int third = purse / 3; win.LeaderHero.ChangeHeroGold(third); Add(win, purse - third); }
                    if (!win.IsMainParty) ClanIncomeBook.NoteInflow(win.LeaderHero, purse / 3, ClanIncomeBook.KThird);   // paczka 169: D rodu (tylko licznik)
                    MoneyLedger.NotePurseGone(purse, false);                                                             // paczka 169: linia "Obieg" (tylko licznik)
                }
                else
                {
                    var t = NearestTown(mp);
                    if (t != null && t.Town != null)
                    {
                        t.Town.ChangeGold(purse);
                        // paczka 169 (tylko liczniki, obie metody z wlasnym try i bramka CirculationWindows.On) - ta sama osada, bez drugiego przegladu
                        MoneyLedger.NotePurseGone(purse, true);
                        MoneyLedger.Note169(MoneyLedger.N169PurseGone, t, purse);
                    }
                }
            }
            catch (Exception e) { Log.Error("MenPurse.OnPartyDestroyed", e); }
        }
        /// <summary>Suma wszystkich sakiewek ludzi - odczyt dla ksiegi "Pieniadz swiata" (MoneyLedger).</summary>
        internal static long TotalNow() { long t = 0; foreach (var v in _purse.Values) t += v; return t; }

        /// <summary>Zold partii wplacony do jej sakiewki (SoldierPay) - tylko licznik dziennej linii "Sakiewka ludzi:".</summary>
        internal static void NoteWage(int amount)
        {
            if (amount <= 0) return;
            try { Day(); } catch { }
            _dayWage += amount; _dayWageN++;
        }

        /// <summary>Stan sakiewek do linii "Zold:": suma, liczba, najwieksza oraz sakiewki partii, ktorych juz nie ma na mapie (tylko odczyt).</summary>
        internal static void Stats(out long total, out int count, out int max, out string maxKey, out int orphans, out long orphanGold)
        {
            total = 0; count = 0; max = 0; maxKey = null; orphans = 0; orphanGold = 0;
            var alive = new HashSet<string>();
            // kazda partia, ktora gra jeszcze zna - takze chwilowo nieczynna (np. partia gracza w niewoli)
            try { foreach (var mp in MobileParty.All) if (mp != null && mp.StringId != null) alive.Add(mp.StringId); } catch { }
            foreach (var kv in _purse)
            {
                if (kv.Value <= 0) continue;
                total += kv.Value; count++;
                if (kv.Value > max) { max = kv.Value; maxKey = kv.Key; }
                if (alive.Count > 0 && !alive.Contains(kv.Key)) { orphans++; orphanGold += kv.Value; }
            }
        }
        internal static int Get(MobileParty mp) { int v; var k = Key(mp); return k != null && _purse.TryGetValue(k, out v) ? v : 0; }
        internal static void Add(MobileParty mp, int n)
        {
            var k = Key(mp); if (k == null || n == 0) return;
            int v; _purse.TryGetValue(k, out v);
            int nv = Math.Max(0, v + n);
            if (nv > v) _dayIn += nv - v; else _dayOut += v - nv;   // tylko licznik linii "Sakiewka ludzi:" (stan dzis - stan wczoraj = wplynelo - wyszlo)
            if (nv > 0) _purse[k] = nv; else _purse.Remove(k);
        }
        internal static int Take(MobileParty mp, int n) { int have = Get(mp); int t = Math.Min(have, Math.Max(0, n)); Add(mp, -t); return t; }

        private static void Day()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (_dayStamp == d) return;
            if (_dayStamp >= 0 && (_daySold + _dayLife + _dayGear + _dayCloth + _dayWage + _dayIn + _dayOut + _daySaved) > 0)
                Log.Info("Sakiewka ludzi: dzien " + _dayStamp + " - nadwyzki sprzedane " + _daySold + " szt. za " + _dayGold + " (w tym zalogi " + _dayGarSold + " za " + _dayGarGold + "; trzecia lordow AI i panow osad " + _dayLord
                         + "), ludzie wydali na sprzet " + _dayGear + ", na zycie w miastach " + _dayLife + ", na odziez wojska (150) " + _dayCloth
                         + "; zold wplacony do sakiewek " + _dayWage + " (" + _dayWageN + " wyplat); ruch sakiewek: wplynelo " + _dayIn + " (zold, lup, przejete sakiewki), wyszlo " + _dayOut
                         + " (sprzet, naprawy, odziez, zycie w miastach, utracone sakiewki), w sakiewkach razem " + TotalNow() + "; odlozone na sprzet " + _daySaved + ".");
            _daySold = _dayGold = _dayLord = _dayLife = _dayGear = 0; _daySaved = _dayGarSold = _dayGarGold = 0; _dayCloth = 0; _dayWage = 0; _dayWageN = 0; _dayIn = _dayOut = 0; _dayStamp = d;
        }

        /// <summary>Cena skupu sztuki (w tym stanie) w miescie; poza miastem - najblizsze miasto.</summary>
        internal static int SellPrice(EquipmentElement el, Settlement st, MobileParty seller)
        {
            try
            {
                var town = st != null && st.IsTown ? st : NearestTown(seller);
                if (town != null && town.Town != null) return Math.Max(1, town.Town.MarketData.GetPrice(el, seller, true, town.Party));
            }
            catch { }
            return Math.Max(1, el.ItemValue / 2);
        }

        private static Settlement NearestTown(MobileParty mp)
        {
            Settlement best = null; float bd = float.MaxValue;
            try
            {
                var p = mp != null ? mp.GetPosition2D : Vec2.Zero;
                foreach (var t in Settlement.All)
                {
                    if (t == null || !t.IsTown) continue;
                    float d = p.DistanceSquared(t.GetPosition2D);
                    if (d < bd) { bd = d; best = t; }
                }
            }
            catch { }
            return best;
        }

        // ------------------------------------------------------------ wejscie / wyjscie z miasta
        // K1 (A5): w jednym zdarzeniu - sprzedaz nadwyzek -> naprawy -> braki -> lepsze (Jeff 14.09: "najpierw braki, potem wymiana";
        // naprawy maja pierwszenstwo od wpisu 84)
        internal static void OnEntered(MobileParty mp, Settlement st, Hero h)
        {
            try
            {
                if (!On || mp == null || st == null || !st.IsTown || st.Town == null) return;
                Day();
                if (mp.IsMainParty) { SettlePlayerBook(); SellPlayerSurplus(st); BuyPlayerGaps(st); MenUpgrade.ForPlayer(st); }
                else if (mp.IsLordParty && mp.LeaderHero != null && mp.LeaderHero.IsAlive && mp.MapEvent == null)
                {
                    SellArmorySurplus(mp, st, mp.LeaderHero, false);
                    AiWear.MendInTown(mp, st);
                    AiGear.TryBuy(mp, st);   // K1: braki i lepsze PO nadwyzkach i naprawach (AiGear.OnSettlementEntered oddaje miasta tutaj)
                }
            }
            catch (Exception e) { Log.Error("MenPurse.OnEntered", e); }
        }

        /// <summary>K1 (A4): doba postoju druzyny gracza w miescie - braki, potem lepsze (nadwyzki tylko przy wjezdzie).</summary>
        internal static void OnDailyTickParty(MobileParty mp)
        {
            try
            {
                if (!On || mp == null || !mp.IsMainParty) return;
                var st = mp.CurrentSettlement;
                if (st == null || !st.IsTown || st.Town == null || QuartermasterEscrow.Active) return;
                Day();
                SettlePlayerBook(); BuyPlayerGaps(st); MenUpgrade.ForPlayer(st);
            }
            catch (Exception e) { Log.Error("MenPurse.OnDailyTickParty", e); }
        }

        /// <summary>K1 (B4): zanim ludzie gracza pojda na targ, kwatermistrz rozlicza wklady gracza (wymiana 1:1, jak przy ekranie
        /// zbrojowni) - zamowienie od kowala czy wklad sprzed wjazdu nie jest wtedy ani "brakiem", ani "zapasem ludzi".</summary>
        private static void SettlePlayerBook()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.QuartermasterSwapOneForOne || !s.ArmouryProtectUsed || QuartermasterEscrow.Active) return;
                var armory = QuartermasterLaw.DteArmory();
                var main = MobileParty.MainParty;
                if (armory == null || main == null || main.MemberRoster == null) return;
                // K1 (przeglad, koszt i szum): porzadek tylko, gdy od ostatniego zmienila sie ksiega gracza, zbrojownia albo druzyna (licznik
                // wersji ksiegi i VersionNo rosterow gry) - dotad pelne dopasowanie 15 typow przy kazdym wjezdzie i co dobe postoju
                if (ArmouryBehavior.StockVersion == _sigStock && armory.VersionNo == _sigArm && main.MemberRoster.VersionNo == _sigRoster) return;
                ArmouryBehavior.ReconcileStock("miasto");
                QuartermasterLaw.PurgeUnusable(armory);
                _sigStock = ArmouryBehavior.StockVersion; _sigArm = armory.VersionNo; _sigRoster = main.MemberRoster.VersionNo;
            }
            catch (Exception e) { Log.Error("MenPurse.SettlePlayerBook", e); }
        }
        private static int _sigStock = int.MinValue, _sigArm = int.MinValue, _sigRoster = int.MinValue;

        internal static void OnLeft(MobileParty mp, Settlement st)
        {
            try
            {
                if (!On || mp == null || st == null || !st.IsTown || st.Town == null) return;
                Day();
                if (mp.IsMainParty) TroopSelfMend.LeftTown();
                int purse = Get(mp);
                if (purse <= 0) return;
                int reserve = mp.IsMainParty ? TroopSelfMend.OutstandingCost(st) : AiWear.OutstandingCost(mp, st);   // przy naprawach z materialem: z szacunkiem materialu z polki tego miasta
                // 150: odziez, buty i plotno wojska - z polki miasta po cenie targu, z tej samej sakiewki PRZED wydatkiem "na zycie"
                // (to samo zloto: mniej idzie "na zycie"); rezerwa na zalegle naprawy nietknieta, jak przy brakach w kompletach
                int cloth = ArmyClothing.BuyForParty(mp, st, purse - reserve);
                if (cloth > 0) { _dayCloth += cloth; purse = Get(mp); }
                int avail = Math.Max(0, purse - reserve);
                // K1 (A3): MenGearSavePercent ludzie odkladaja na lepszy sprzet - nie wiecej niz MenGearSaveDays dni zoldu partii (z rezerwa
                // na naprawy); reszta i nadwyzka ponad limit - "na zycie" w miescie, jak dotad.
                // K1 (przeglad): odkladaja czesc PRZYROSTU od ostatniego wyjazdu, a dawne oszczednosci zostaja (do limitu). Dotad szlo 50%
                // CALEJ sakiewki razem z oszczednosciami - kazda wizyta zjadala polowe odlozonego, wiec uzbierac dalo sie okolo jednego
                // przychodu miedzy wizytami, a limit 30 dni zoldu byl nieosiagalny (droga zbroja t4-t5 zawsze poza zasiegiem)
                int saved = 0, over = 0, kept = 0;
                if (MenUpgrade.On && avail > 0)
                {
                    var s = Settings.Current;
                    long cap = (long)Math.Max(0, s.MenGearSaveDays) * Math.Max(0, Wage(mp));
                    int room = (int)Math.Max(0L, Math.Min(int.MaxValue, cap - reserve));
                    kept = Math.Min(Math.Min(SavedOf(mp), avail), room);      // dawne oszczednosci: nie wiecej, niz jest w sakiewce i niz limit
                    int want = (int)((long)(avail - kept) * Math.Max(0, Math.Min(100, s.MenGearSavePercent)) / 100);
                    saved = Math.Min(want, room - kept); over = want - saved;
                    MenUpgrade.NoteSaved(saved, over);
                    _daySaved += saved;
                }
                SetSaved(mp, kept + saved);
                int life = avail - kept - saved;
                if (life <= 0)
                {
                    if (mp.IsMainParty && saved > 0) Log.Player("Your men put by " + saved + " for better kit (saved " + (kept + saved) + ", purse " + Get(mp) + ").");
                    return;
                }
                Take(mp, life);
                st.Town.ChangeGold(life);           // karczma, jedzenie, gra, kobiety - pieniadze zostaja w miescie
                MoneyLedger.Note(MoneyLedger.NLife, st, life);   // ksiega przeplywow osad (tylko licznik)
                SoldierPay.Hold(st, life);          // tarcza zoldu (gdy wlaczona): regulator kasy nie skasuje tych pieniedzy, zanim zawor renty odda je panu
                _dayLife += life;
                if (mp.IsMainParty)
                    Log.Player("Your men spent " + life + " denars in " + st.Name + " - food, drink, dice and company." + (reserve > 0 ? " They kept " + Math.Min(purse, reserve) + " for mending their kit." : "")
                               + (saved > 0 ? " They put by " + saved + " for better kit (saved " + (kept + saved) + ", purse " + Get(mp) + ")." : ""));
            }
            catch (Exception e) { Log.Error("MenPurse.OnLeft", e); }
        }

        private static int Wage(MobileParty mp) { try { return mp.TotalWage; } catch { return 0; } }

        // ------------------------------------------------------------ gracz: nadwyzki na targ
        // K1 (A9): po DOPASOWANIU, nie po liczbie sztuk - najpierw sztuki LUDZI, ktorych nikt nie udzwignie, potem najgorsze uzyteczne
        // ponad komplet + SurplusKeepPercent; w zapasie zostaja najlepsze wolne uzyteczne. Dotad liczenie po typie trzymalo T6, ktorej
        // nikt nie naciagnie, a sprzedawalo uzyteczne T3. Czesc gracza (ksiega, NAJGORSZE egzemplarze id) nietknieta - dotad
        // pomijane bylo cale id, gdy gracz mial w nim choc jedna sztuke.
        private static void SellPlayerSurplus(Settlement st)
        {
            var armory = QuartermasterLaw.DteArmory();
            if (armory == null || QuartermasterEscrow.Active) return;
            var s = Settings.Current;
            var main = MobileParty.MainParty;
            int sold = 0, gold = 0;
            foreach (var type in QuartermasterLaw.KitTypes)
            {
                if (type == ItemObject.ItemTypeEnum.Horse) continue;   // konie - Stajnia
                var pieces = QuartermasterLaw.KitPieces(armory, type, true);
                if (pieces.Count == 0) continue;
                List<CharacterObject> troops; int[] men; SkillObject skill;
                QuartermasterLaw.MenOf(main.MemberRoster, type, pieces, out troops, out men, out skill);
                var meets = QuartermasterLaw.MeetsOf(troops);
                // K1 (Jeff 09.10 04:40): kto co nosi - ta sama regula co wymiana (najlepsze najpierw; sztuka gracza tylko w puste rece albo
                // w miejsce gorszej), inaczej ludzie sprzedawaliby lepsza sztuke, ktora nosza, bo "noszona" byla gorsza sztuka gracza
                if (s.QuartermasterSwapOneForOne) SwapMath.FitBest(men, troops.Count, meets, pieces);
                else SwapMath.Fit(men, troops.Count, meets, pieces);
                if (SwapMath.SurplusPlan(pieces, men.Length, s.SurplusKeepPercent, p => SwapMath.Usable(men, meets, p)) <= 0) continue;
                pieces.Sort(SwapMath.WorseFirst);
                foreach (var p in pieces)
                {
                    if (p.Sell <= 0) continue;
                    var el = QuartermasterLaw.ElOf(p);
                    int unit = SellPrice(el, st, main);
                    int n = Math.Min(p.Sell, Math.Max(0, st.Town.Gold) / Math.Max(1, unit));
                    if (n <= 0) continue;
                    armory.AddToCounts(el, -n);
                    st.ItemRoster.AddToCounts(el, n);
                    st.Town.ChangeGold(-unit * n);
                    MoneyLedger.Note169(MoneyLedger.N169Surplus, st, -unit * n);   // paczka 169: linia kas (tylko licznik)
                    Add(main, unit * n);
                    sold += n; gold += unit * n;
                    SellByCondition.NoteSale(SellByCondition.Men, el, n, unit);   // ksiega skupu sprzetu (tylko log)
                    MenUpgrade.NoteChurn(main, el.Item, false);                   // K1 (przeglad): kupione i sprzedane tej samej doby (autotest: 0)
                }
            }
            if (sold > 0)
            {
                _daySold += sold; _dayGold += gold;
                Log.Player("Your men sold " + sold + " spare pieces from the stores to the merchants of " + st.Name + " for " + gold
                           + " denars - their share of the spoils. Purse of the men: " + Get(main) + ".");
                Log.Info("Sakiewka ludzi: gracz w " + st.Name + " - nadwyzki " + sold + " szt. za " + gold + ", sakiewka " + Get(main) + ".");
            }
        }

        // ------------------------------------------------------------ gracz: braki w kompletach za pieniadze ludzi
        // K1 (A6): brak = ludzie bez UZYTECZNEJ sztuki (FitFor.UnfitMen); kupiona sztuka ma wymog <= UnfitMinSkill typu (to samo
        // "bring <= N", co pokazuje kwatermistrz). Dotad NeedForType - HaveFor liczylo jako "ma" takze sztuki ponad umiejetnosc.
        private static void BuyPlayerGaps(Settlement st)
        {
            var armory = QuartermasterLaw.DteArmory();
            var main = MobileParty.MainParty;
            if (armory == null || QuartermasterEscrow.Active) return;
            int budget = Get(main) - TroopSelfMend.OutstandingCost(st);   // naprawy maja pierwszenstwo (z materialem - szacunek z tej polki)
            if (budget <= 0) return;
            int spent = 0, pieces = 0, maxPieces = Math.Max(1, Settings.Current.AiGearMaxPiecesPerVisit);
            var shelf = st.ItemRoster;
            foreach (var type in QuartermasterLaw.KitTypes)
            {
                if (type == ItemObject.ItemTypeEnum.Horse) continue;
                var fit = QuartermasterLaw.FitFor(armory, type);
                int gap = fit.UnfitMen;
                int maxReq = fit.UnfitMinSkill;
                // K1c (przeglad K1b, Jeff 09.10 P1, "rekrut nie kupi plyty"): k-ty zakup typu najwyzej do k-tego sufitu ludzi bez sztuki
                // (SwapMath.CeilingTier, od najwyzszego); dotad braki nie mialy zadnego limitu tieru
                var ceils = MenUpgrade.CeilingsOf(fit.UnfitByTroop, Settings.Current.MenUpgradeOneTierUp);
                int gap0 = gap;
                while (gap > 0 && pieces < maxPieces && spent < budget)
                {
                    int best = -1, bestPrice = 0; float bestScore = 0f;
                    int maxTier = ceils.Count > 0 ? ceils[Math.Min(gap0 - gap, ceils.Count - 1)] : int.MaxValue;
                    for (int i = 0; i < shelf.Count; i++)
                    {
                        var el = shelf.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || !QuartermasterLaw.CountsAsKit(it, type) || ArmsPricing.IsUnique(it)) continue;
                        if (it.Difficulty > 0 && ItemReq.SkillFor(it) != null && it.Difficulty > maxReq) continue;   // najslabszy bez sztuki ja udzwignie
                        if (AiGear.TierOf(it) > maxTier) continue;   // K1c (P1): nie ponad sufit czlowieka, dla ktorego ten zakup
                        if (ArmouryBehavior.StockOf(it.StringId) > 0) continue;   // K1 (przeglad): ksiega per id - zakup ludzi przesunalby Twoja czesc na gorszy egzemplarz
                        int price = st.Town.MarketData.GetPrice(el.EquipmentElement, main, false, st.Party);
                        if (price <= 0 || price > budget - spent) continue;
                        float score = (it.Effectiveness > 0f ? it.Effectiveness : 1f) / price;
                        if (score > bestScore) { bestScore = score; best = i; bestPrice = price; }
                    }
                    if (best < 0) break;
                    var pick = shelf.GetElementCopyAtIndex(best).EquipmentElement;
                    shelf.AddToCounts(pick, -1);
                    armory.AddToCounts(pick, 1);
                    Take(main, bestPrice);
                    st.Town.ChangeGold(bestPrice);
                    MoneyLedger.Note169(MoneyLedger.N169Kit, st, bestPrice);   // paczka 169: linia kas (tylko licznik)
                    MenUpgrade.NoteChurn(main, pick.Item, true);
                    spent += bestPrice; pieces++; gap--;
                }
            }
            if (pieces > 0)
            {
                _dayGear += spent;
                Log.Player("Your men bought " + pieces + " missing pieces of kit for " + spent + " denars from their own purse.");
                Log.Info("Sakiewka ludzi: gracz w " + st.Name + " - braki " + pieces + " szt. za " + spent + ".");
            }
        }

        // ------------------------------------------------------------ AI i zalogi: nadwyzki na targ, trzecia dla pana
        // K1 (A9): ta sama regula co u gracza (po dopasowaniu); zbrojownia AI bez stanu - do kupca idzie najgorsza obita (AiWear).
        // Lord AI: trzecia (LordLootThirdPercent) do kiesy lorda; zaloga: trzecia do pana osady; reszta do sakiewki ludzi.
        private static void SellArmorySurplus(MobileParty mp, Settlement st, Hero third, bool garrison)
        {
            var dict = AiGear.Armories();
            Dictionary<ItemObject, int> arm;
            if (dict == null || !dict.TryGetValue(mp.Id, out arm) || arm == null || arm.Count == 0) return;
            var s = Settings.Current;
            int sold = 0, gold = 0;
            bool stop = false;
            foreach (var type in QuartermasterLaw.KitTypes)
            {
                if (stop) break;
                if (type == ItemObject.ItemTypeEnum.Horse || type == ItemObject.ItemTypeEnum.HorseHarness) continue;   // konie i rzedy - Stajnia
                var pieces = MenUpgrade.AiPieces(arm, type);
                if (pieces.Count == 0) continue;
                List<CharacterObject> troops; int[] men; SkillObject skill;
                // K1 (przeglad): bron po SLOTACH WZORCA (jak AiGear.BuyGaps i MenUpgrade.Buckets) - dotad 1 bron na czlowieka, wiec
                // druga bron oddzialu szla do kupca, a BuyGaps odkupowal ja tego samego dnia
                QuartermasterLaw.MenOf(mp.MemberRoster, type, pieces, out troops, out men, out skill, QuartermasterLaw.MenPerSlot, false);
                var meets = QuartermasterLaw.MeetsOf(troops);
                // K1 (Jeff 09.10 04:40, "to samo dla AI"): ludzie nosza najlepsze, co udzwigna (FitBest), a do kupca idzie najgorsze - dotad
                // ciezki gorszy grat byl "noszony", a lepsza lzejsza sztuka szla do kupca (czlowiek zostawal z gorsza w miejsce lepszej)
                SwapMath.FitBest(men, troops.Count, meets, pieces);
                if (SwapMath.SurplusPlan(pieces, men.Length, s.SurplusKeepPercent, p => SwapMath.Usable(men, meets, p)) <= 0) continue;
                pieces.Sort(SwapMath.WorseFirst);
                foreach (var p in pieces)
                {
                    var it = QuartermasterLaw.ElOf(p).Item;
                    for (int k = 0; k < p.Sell && !stop; k++)
                    {
                        int cnt;
                        if (it == null || !arm.TryGetValue(it, out cnt) || cnt <= 0) break;
                        var el = new EquipmentElement(it, AiWear.TakeCondition(mp, it));
                        int unit = SellPrice(el, st, mp);
                        if (st.Town.Gold < unit) { AiWear.PutBack(mp, it, el.ItemModifier); stop = true; break; }   // kasa pusta - koniec na dzis (stan wraca)
                        if (cnt > 1) arm[it] = cnt - 1; else arm.Remove(it);
                        st.ItemRoster.AddToCounts(el, 1);
                        st.Town.ChangeGold(-unit);
                        MoneyLedger.Note169(MoneyLedger.N169Surplus, st, -unit);   // paczka 169: linia kas (tylko licznik)
                        int cut = third != null && third.IsAlive ? (int)Math.Round(unit * MBMath.ClampFloat(s.LordLootThirdPercent, 0f, 100f) / 100f) : 0;
                        if (cut > 0)
                        {
                            third.ChangeHeroGold(cut);
                            if (third != Hero.MainHero) ClanIncomeBook.NoteInflow(third, cut, ClanIncomeBook.KThird);   // paczka 169: D rodu (tylko licznik)
                        }
                        Add(mp, unit - cut);
                        sold++; gold += unit; _dayLord += cut;
                        SellByCondition.NoteSale(SellByCondition.Men, el, 1, unit);   // ksiega skupu sprzetu (tylko log)
                        MenUpgrade.NoteChurn(mp, it, false);                           // K1 (przeglad): kupione i sprzedane tej samej doby (autotest: 0)
                    }
                }
            }
            if (sold > 0) { _daySold += sold; _dayGold += gold; if (garrison) { _dayGarSold += sold; _dayGarGold += gold; } }
        }

        /// <summary>K1 (A9, A11): doba zalogi - zaloga bez ludzi oddaje sakiewke do kasy osady; inaczej nadwyzki zbrojowni do kupca
        /// (miasto - jego targ, zamek - najblizsze miasto handlowe); trzecia dla pana osady, reszta do sakiewki zalogi.</summary>
        internal static void GarrisonDay(MobileParty mp, Settlement st)
        {
            try
            {
                if (!MenUpgrade.GarrisonPurseOn || mp == null || st == null || st.Town == null || !mp.IsGarrison) return;
                Day();
                int men = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                if (men <= 0)
                {
                    int purse = Get(mp);
                    if (purse > 0)
                    {
                        Take(mp, purse);
                        st.Town.ChangeGold(purse);
                        MoneyLedger.NotePurseGone(purse, true);                         // paczka 169: linia "Obieg" (tylko licznik)
                        MoneyLedger.Note169(MoneyLedger.N169PurseGone, st, purse);
                        MenUpgrade.NoteGarrisonEmpty(purse);
                    }
                    SetSaved(mp, 0);
                    return;
                }
                if (st.IsUnderSiege) return;
                var market = st.IsTown ? st : ArmyClothing.MarketTown(st);
                if (market == null || market.Town == null || market.ItemRoster == null || market.IsUnderSiege) return;
                if (FactionManager.IsAtWarAgainstFaction(mp.MapFaction, market.MapFaction)) return;
                SellArmorySurplus(mp, market, st.OwnerClan != null ? st.OwnerClan.Leader : null, true);
            }
            catch (Exception e) { Log.Error("MenPurse.GarrisonDay", e); }
        }

        // ------------------------------------------------------------ gracz kupuje od ludzi (ekran zbrojowni)
        private static readonly List<KeyValuePair<EquipmentElement, int>> _pending = new List<KeyValuePair<EquipmentElement, int>>();

        internal static void NoteBuy(EquipmentElement el, int n)
        {
            if (!On || el.Item == null || n <= 0) return;
            _pending.Add(new KeyValuePair<EquipmentElement, int>(el, n));
        }

        /// <summary>Gracz odklada z powrotem to, co w tej sesji wzial od ludzi: kasujemy zakup (zwraca ile).</summary>
        internal static int CancelBuy(EquipmentElement el, int n)
        {
            int cut = 0;
            for (int i = _pending.Count - 1; i >= 0 && n > 0; i--)
            {
                var kv = _pending[i];
                // K1: ten sam egzemplarz (przedmiot i stan) - dotad sam przedmiot, wiec odlozenie WLASNEJ sprawnej sztuki kasowalo zakup obitej od ludzi
                if (kv.Key.Item != el.Item || kv.Key.ItemModifier != el.ItemModifier) continue;
                int c = Math.Min(kv.Value, n); n -= c; cut += c;
                if (kv.Value - c <= 0) _pending.RemoveAt(i); else _pending[i] = new KeyValuePair<EquipmentElement, int>(kv.Key, kv.Value - c);
            }
            return cut;
        }

        internal static void ClearBuys() { _pending.Clear(); }

        /// <summary>K1 (przeglad): ile zaplacisz ludziom przy zamknieciu za to, co juz wziales w tej sesji (cena kupca w tym stanie).</summary>
        internal static long PendingCost(Settlement st)
        {
            long t = 0;
            try { var main = MobileParty.MainParty; foreach (var kv in _pending) t += (long)SellPrice(kv.Key, st, main) * kv.Value; } catch { }
            return t;
        }

        /// <summary>K1: gracz wzial w tej sesji ekranu cos z zapasu ludzi - zamkniecie musi to rozliczyc.</summary>
        internal static bool PendingBuys { get { return _pending.Count > 0; } }

        /// <summary>Zamkniecie ekranu zbrojowni: placisz ludziom za wziete nadwyzki; czego nie stac - wraca do zbrojowni.</summary>
        internal static void SettleBuys()
        {
            if (_pending.Count == 0) return;
            try
            {
                var main = MobileParty.MainParty;
                var armory = QuartermasterLaw.DteArmory();
                var st = Settlement.CurrentSettlement;
                int paid = 0, pieces = 0, back = 0, lost = 0;
                foreach (var kv in _pending)
                {
                    int unit = SellPrice(kv.Key, st, main);
                    for (int k = 0; k < kv.Value; k++)
                    {
                        if (Hero.MainHero.Gold >= unit)
                        {
                            Hero.MainHero.ChangeHeroGold(-unit); Add(main, unit); paid += unit; pieces++;
                        }
                        else if (armory != null && main.ItemRoster.GetElementNumber(main.ItemRoster.FindIndexOfElement(kv.Key)) > 0)
                        {
                            main.ItemRoster.AddToCounts(kv.Key, -1); armory.AddToCounts(kv.Key, 1); back++;
                        }
                        else lost++;   // K1 (przeglad): ani zaplaty, ani sztuki w sakwach - prog w QuartermasterLaw.CanPayMen powinien to wykluczyc
                    }
                }
                _pending.Clear();
                if (pieces > 0) Log.Player("You paid your men " + paid + " denars for " + pieces + " pieces from their share of the spoils.");
                if (back > 0) Log.Player(back + " pieces went back to the stores - your purse would not cover them.", true);
                Log.Info("Sakiewka ludzi: gracz kupil od ludzi " + pieces + " szt. za " + paid + ", zwrocono " + back + (lost > 0 ? ", NIEZAPLACONE i poza sakwami " + lost : "") + ".");
            }
            catch (Exception e) { Log.Error("MenPurse.SettleBuys", e); _pending.Clear(); }
        }
    }
}
