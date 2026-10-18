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
    ///  - Gracz bierze cos z nadwyzek zbrojowni = KUPUJE od ludzi po cenie skupu (rozliczenie przy zamknieciu ekranu). Jeff 15.10 (a): przy
    ///    ArmouryShowsOnlyYours zapasu ludzi nie ma na liscie okna (PlayerArmoryView) - zakup nieczynny (NoteBuy/SettleBuys zostaja dla klucza wylaczonego).
    /// K1 (Jeff 09.10, docs/paczki/K1-dozbrajanie.md): przy wyjezdzie czesc odkladaja na lepszy sprzet (A3, limit dni zoldu); w miescie
    /// nadwyzki -> naprawy -> braki -> lepsze (MenUpgrade); nadwyzki i braki liczone po DOPASOWANIU (skill), nie po liczbie sztuk;
    /// zalogi maja wlasna sakiewke (czesc zoldu - SoldierPay) w tym samym slowniku i tym samym zapisie "arm_menpurse".
    /// E5/A3 (163, markietani): w POLU ludzie partii rodow wydaja co dobe czesc wolnej sakiewki (CampPlan/CampTake - te same rezerwy co OnLeft; odbiorcy w Sutlers).
    /// </summary>
    internal static class MenPurse
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.MenPurseEnabled; } }

        private static readonly Dictionary<string, int> _purse = new Dictionary<string, int>();
        // K1 (przeglad, A3): ile z sakiewki ludzie juz odlozyli na lepszy sprzet (czesc sakiewki, nie osobne pieniadze); zapis "arm_mensaved"
        private static readonly Dictionary<string, int> _saved = new Dictionary<string, int>();
        private static int _daySold, _dayGold, _dayLord, _dayLife, _dayGear, _dayStamp = -1;
        private static int _daySaved, _dayGarSold, _dayGarGold;   // K1: odlozone na lepszy sprzet przy wyjazdach (A3), nadwyzki zalog (A9)
        private static long _dayCloth;         // 150: odziez wojska kupiona z sakiewek przy wyjezdzie z miasta (ArmyClothing.BuyForParty) i u markietanow w polu (T3-K1, NoteCampCloth)
        private static long _dayWage;          // zold wplacony do sakiewek (SoldierPay)
        private static int _dayWageN;
        private static long _dayIn, _dayOut;   // ruch wszystkich sakiewek w dobie: kazda wplata i kazdy wydatek ida przez Add
        private static long _dayCamp;          // E5/A3 (163): markietani - wydane w polu (CampTake; odbiorcy w Sutlers)

        internal static void Reset() { _purse.Clear(); _saved.Clear(); _pending.Clear(); _sigStock = _sigArm = _sigRoster = int.MinValue; _daySold = _dayGold = _dayLord = _dayLife = _dayGear = 0; _daySaved = _dayGarSold = _dayGarGold = 0; _dayCloth = 0; _dayWage = 0; _dayWageN = 0; _dayIn = _dayOut = 0; _dayCamp = 0; _dayStamp = -1; }

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
        /// <summary>E5/A3 (163): oszczednosci K1, ktore markietani chronia dzis w sakiewce partii - ta sama regula co CampPlan (limit OnLeft: MenGearSaveDays
        /// x zold TERAZ minus zalegle naprawy). Tylko odczyt (linia markietanow: "w tym oszczednosci K1"). Bez wlasnego catch - wyjatek liczy Sutlers.</summary>
        internal static int KeptFor(MobileParty mp)
        {
            if (mp == null) return 0;
            int purse = Get(mp);
            if (purse <= 0) return 0;
            long res; int kept;
            CampReserve(mp, purse, out res, out kept);
            return kept;
        }
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
                if (BeatenPurse.LordPurseToTown(mp, destroyer, purse)) return;   // D11-6: druzyna lorda niepokonana w bitwie przez przywodce zwyciezcow - najblizsze miasto
                if (Seal31B.PurseOfBeaten(mp, destroyer, purse)) return;   // 3.1/3.3 (31b) E3/4 (W13): zwyciezca bez bohatera z kiesa (banda) albo osada niszczyciela
                var win = destroyer != null ? destroyer.MobileParty : null;
                if (win != null && win.LeaderHero != null && win.LeaderHero.IsAlive)
                {
                    if (win.IsMainParty) { Add(win, purse); Ransom178.NotePlayerPurse(purse); }   // ludzie gracza - do ich sakiewki; 178: korona 1/9 sakwy (jutro z sakiewki)
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
            if (_dayStamp >= 0 && (_daySold + _dayLife + _dayGear + _dayCloth + _dayWage + _dayIn + _dayOut + _daySaved + _dayCamp) > 0)
                Log.Info("Sakiewka ludzi: dzien " + _dayStamp + " - nadwyzki sprzedane " + _daySold + " szt. za " + _dayGold + " (w tym zalogi " + _dayGarSold + " za " + _dayGarGold + "; trzecia lordow AI i panow osad " + _dayLord + Drobne39.ThirdNote()
                         + "), ludzie wydali na sprzet " + _dayGear + ", na zycie w miastach " + _dayLife + ", u markietanow w polu (E5/A3) " + _dayCamp + ", na odziez wojska (150) " + _dayCloth
                         + "; zold wplacony do sakiewek " + _dayWage + " (" + _dayWageN + " wyplat); ruch sakiewek: wplynelo " + _dayIn + " (zold, lup, przejete sakiewki, zaplata za najem i udzial w puli weteranow - E5/B6, konie lordow ponad zapas - E6/K2), wyszlo " + _dayOut
                         + " (sprzet, naprawy, odziez, zaopatrzenie BK - D11-4, zycie w miastach, markietani, utracone sakiewki), w sakiewkach razem " + TotalNow() + "; odlozone na sprzet " + _daySaved + ".");
            _daySold = _dayGold = _dayLord = _dayLife = _dayGear = 0; _daySaved = _dayGarSold = _dayGarGold = 0; _dayCloth = 0; _dayWage = 0; _dayWageN = 0; _dayIn = _dayOut = 0; _dayCamp = 0; _dayStamp = d;
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
                    long p0 = DayProfile.On ? DayProfile.MpBegin() : 0L;   // E8/0 (242): rozbicie wejscia lorda, probka 1/16 (tylko pomiar)
                    int g0; SellArmorySurplus(mp, st, mp.LeaderHero, false, null, out g0);
                    long p1 = DayProfile.Now(p0);
                    AiWear.MendInTown(mp, st);
                    long p2 = DayProfile.Now(p0);
                    AiGear.TryBuy(mp, st);   // K1: braki i lepsze PO nadwyzkach i naprawach (AiGear.OnSettlementEntered oddaje miasta tutaj)
                    if (p0 != 0) DayProfile.MpEnd(p0, p1, p2);
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
                SettlePlayerBook();
                if (CitySack.NoSpendStay(st, mp)) return;   // lup miast L3 (poprawka po audycie): w oknie lupu ludzie gracza nie kupuja z polki lupionej osady (wlasny try)
                BuyPlayerGaps(st); MenUpgrade.ForPlayer(st);
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
                if (Seal31B.CaravanSpends(mp, st, purse)) return;   // 3.1/3.3 (31b) E3/4: karawana - cala sakiewka "na zycie", bez oszczednosci, odziezy i tarczy zoldu
                int reserve = mp.IsMainParty ? TroopSelfMend.OutstandingCost(st) : AiWear.OutstandingCost(mp, st);   // przy naprawach z materialem: z szacunkiem materialu z polki tego miasta
                // 150: odziez, buty i plotno wojska - z polki miasta po cenie targu, z tej samej sakiewki PRZED wydatkiem "na zycie"
                // (to samo zloto: mniej idzie "na zycie"); rezerwa na zalegle naprawy nietknieta, jak przy brakach w kompletach
                int cloth = CitySack.NoSpendCloth(st, mp) ? 0 : ArmyClothing.BuyForParty(mp, st, purse - reserve);   // lup miast L3: w oknie lupu bez zakupu z polki lupionej osady
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
                if (CitySack.NoSpend(st, mp, life)) return;   // lup miast L3: w lupionym miescie wojsko krolestwa zdobywcy nie placi - zold zostaje w sakiewce (wlasny try)
                Take(mp, life);
                int folk = TownFolk.EarnService(st, life, TownFolk.KLife);   // E5/B1 (R9, 75/25): uslugi do kiesy ludu (arytmetyka - nie rzuca); kupcy reszte
                st.Town.ChangeGold(life - folk);    // karczma, jedzenie, gra, kobiety - pieniadze zostaja w miescie
                ClanIncomeBook.NoteOwnPaid(st, mp.ActualClan, life);   // 169c: "wlasne" D stalego - wydatki ludzi rodu w jego miescie (tylko licznik)
                if ((mp.IsLordParty || mp.IsMainParty) && mp.ActualClan != null && ReferenceEquals(st.OwnerClan, mp.ActualClan))
                    CrownIncome.NoteOwnSpend(mp.ActualClan, life);   // 165 (K3): zold wydany we wlasnym miescie rodu wraca zaworem - bez zwrotu korony od niego (gracz tez - jedna regula)
                MoneyLedger.Note(MoneyLedger.NLife, st, life - folk);   // ksiega przeplywow osad (tylko licznik) - czesc w kasie (E5/B1: czesc ludu w kiesie, posiadacz H22/H23)
                try { TownFolk.Note(st, life, TownFolk.KLife); } catch { }   // E5/0 (195): zycie zolnierzy w miescie - okno kiesy ludu (calosc; E5/B1: podstawa robocizny 75%)
                SoldierPay.Hold(st, life);          // tarcza zoldu (gdy wlaczona): regulator kasy nie skasuje tych pieniedzy, zanim zawor renty odda je panu
                _dayLife += life;
                CitySack.NoteSpend(st, mp, life);   // lup miast (L0): zycie zostawione w osadzie po nastepstwach oblezenia (tylko licznik, wlasny try)
                if (mp.IsMainParty)
                    Log.Player("Your men spent " + life + " denars in " + st.Name + " - food, drink, dice and company." + (reserve > 0 ? " They kept " + Math.Min(purse, reserve) + " for mending their kit." : "")
                               + (saved > 0 ? " They put by " + saved + " for better kit (saved " + (kept + saved) + ", purse " + Get(mp) + ")." : ""));
            }
            catch (Exception e) { Log.Error("MenPurse.OnLeft", e); }
        }

        private static int Wage(MobileParty mp)
        {
            // perf-d2d3 (PerfSutlerWageOnce): w zakresie jednej partii markietanow (Sutlers - WageMemoBegin/End) zold liczony raz; miedzy odczytami nie dzieje
            // sie nic, co zmienia zold (rezerwy i sakiewka - same odczyty). Probka 1/64 liczy drugi raz i porownuje (PerfD2.WageHit)
            if (_wMemoOn && ReferenceEquals(mp, _wMemoP))
            {
                if (_wMemoHas) return PerfD2.WageHit(mp, _wMemoV);
                _wMemoV = WageRaw(mp); _wMemoHas = true;
                return _wMemoV;
            }
            return WageRaw(mp);
        }

        internal static int WageRaw(MobileParty mp) { try { return mp.TotalWage; } catch { return 0; } }

        [ThreadStatic] private static bool _wMemoOn, _wMemoHas;
        [ThreadStatic] private static MobileParty _wMemoP;
        [ThreadStatic] private static int _wMemoV;

        /// <summary>perf-d2d3: poczatek zakresu jednej partii markietanow (Sutlers.Daily wokol One) - przy PerfSutlerWageOnce zold tej partii liczony raz.</summary>
        internal static void WageMemoBegin(MobileParty mp) { _wMemoOn = PerfD2.SutlerWageOn; _wMemoP = mp; _wMemoHas = false; }

        /// <summary>perf-d2d3: koniec zakresu (zawsze w finally) - poza nim zold liczony jak dotad przy kazdym odczycie.</summary>
        internal static void WageMemoEnd() { _wMemoOn = false; _wMemoP = null; _wMemoHas = false; }

        /// <summary>
        /// KA (16.10, karczmy - TownInns.cs; Jeff "ludzie sami placa za siebie i za swoje konie, dostaja zold"): nocleg ludzi w karczmie z ich sakiewki.
        /// Ta sama droga co "zycie" przy wyjezdzie (OnLeft), ale cala kwota do karczmarzy - kiesy ludu osady (TownFolk.Earn, uslugi KLife); kiesa ludu nieczynna
        /// albo wyjatek - reszta do kasy miasta (jak dotad przy wylaczonej kiesie). Liczniki jak w OnLeft: "na zycie w miastach" linii "Sakiewka ludzi", ksiega
        /// przeplywow osad (czesc w kasie), 169c i 165 (wlasne miasto rodu), tarcza zoldu (czesc w kasie), lup miast L0. Okno kiesy ludu (TownFolk.Note) tylko
        /// dla czesci w kasie - podstawa robocizny kupcow (75% "zycia") nie liczy pieniedzy, ktore do kupcow nie trafily. Wolajacy sprawdza wolna sakiewke
        /// (CampFree) i okno lupu (CitySack.NoSpendStay). Zwraca zdjete z sakiewki; folk - ile przyjela kiesa ludu.
        /// </summary>
        internal static int InnPay(MobileParty mp, Settlement st, int amount, out int folk)
        {
            folk = 0;
            if (!On || mp == null || st == null || !st.IsTown || st.Town == null || amount <= 0) return 0;
            Day();
            int t = Take(mp, amount);
            if (t <= 0) return 0;
            folk = Math.Max(0, Math.Min(t, TownFolk.Earn(st, t, TownFolk.KLife)));   // arytmetyka - nie rzuca (0 przy kiesie nieczynnej)
            int coffer = t - folk;
            if (coffer > 0) st.Town.ChangeGold(coffer);
            ClanIncomeBook.NoteOwnPaid(st, mp.ActualClan, t);   // 169c: wydatki ludzi rodu w jego miescie (tylko licznik)
            // 165 (K3): zold wydany we wlasnym miescie rodu wraca rodowi zaworem kasy miasta - bez zwrotu korony od niego. Recenzja 16.10: tylko czesc w kasie (coffer),
            // jak MoneyLedger.Note / TownFolk.Note / SoldierPay.Hold nizej - przy czynnej kiesie ludu cala kwota idzie do karczmarzy i zawor kasy nic rodowi nie oddaje
            if (coffer > 0 && (mp.IsLordParty || mp.IsMainParty) && mp.ActualClan != null && ReferenceEquals(st.OwnerClan, mp.ActualClan))
                CrownIncome.NoteOwnSpend(mp.ActualClan, coffer);
            if (coffer > 0)
            {
                MoneyLedger.Note(MoneyLedger.NLife, st, coffer);   // ksiega przeplywow osad (tylko licznik) - czesc w kasie
                try { TownFolk.Note(st, coffer, TownFolk.KLife); } catch { }
                SoldierPay.Hold(st, coffer);
            }
            _dayLife += t;
            CitySack.NoteSpend(st, mp, t);   // lup miast (L0): zloto zostawione w osadzie (tylko licznik, wlasny try)
            return t;
        }

        /// <summary>sw-zajazdy (TownInns.RoadCommit): nocleg w zajezdzie przy szlaku - sakiewka ludzi -> wies okregu zajazdu. Ta sama regula co czesc wsi markietanow
        /// (Sutlers.One): podatek pana z czynnego modelu na licznik podatku wsi, reszta do kiesy (VillageTakings.Split - razem dokladnie zdjete); wies rodu partii -
        /// calosc do kiesy (K3); potem udzial korony DS od czesci w kiesie (DsVillage.WarShareSutlers - jak markietani; wies spoza DS - 0). Zwraca zdjete z sakiewki;
        /// `tax` - czesc na licznik podatku, `crown` - przelane z kiesy wsi do korony. Licznik "zycie w miastach" linii "Sakiewka ludzi" jak karczma miasta.
        /// Recenzja: podatek pana tylko przy kluczu VillageTributeFromSutlers (jak Sutlers.One - wylaczony = calosc do kiesy, udzial korony DS od calosci); zmiany
        /// kiesy wsi poza tickiem dobowym nazwane w ksiedze (MoneyLedger.Note169 "inne" - jak HideoutPurge), udzial korony na osobnym liczniku zajazdow linii DS.</summary>
        internal static int InnPayVillage(MobileParty mp, Settlement st, int amount, out int tax, out int crown)
        {
            tax = 0; crown = 0;
            if (!On || mp == null || st == null || st.Village == null || amount <= 0) return 0;
            Day();
            int t = Take(mp, amount);
            if (t <= 0) return 0;
            var v = st.Village;
            int purse = t;
            if (TownInns.RoadTributeOn && !ReferenceEquals(st.OwnerClan, mp.ActualClan)) VillageTakings.Split(v, t, t, out tax, out purse);
            else v.ChangeGold(t);
            if (purse > 0) MoneyLedger.Note169(MoneyLedger.N169Other, st, purse);   // ksiega przeplywow osad: sakiewka ludzi -> kiesa wsi (tylko licznik)
            if (purse > 0) crown = DsVillage.WarShareSutlers(v, purse, true);         // wlasny try - nie rzuca
            if (crown > 0) MoneyLedger.Note169(MoneyLedger.N169Other, st, -crown);    // kiesa wsi -> skarbiec korony (tylko licznik)
            _dayLife += t;
            return t;
        }

        /// <summary>
        /// E5/A3 (paczka 163, R9) - markietani: doba partii w POLU. Ta sama kolejnosc rezerw co OnLeft - zalegle naprawy (robocizna, bez szacunku materialu:
        /// poza miastem nie ma polki - jak zaopatrzenie BK D11-4), odziez czekajaca na zakup (150) i oszczednosci K1 juz odlozone zostaja w sakiewce;
        /// z reszty ludzie wydaja `rate` (nowe oszczednosci odkladaja dalej tylko przy wyjezdzie z miasta). Tylko liczy kwote - zdejmuje CampTake, odbiorcow
        /// (kasa miasta, kiesa wsi) i liczniki daje Sutlers (recenzja E5/A3 2-2: odbiorcy wyznaczeni PRZED zdjeciem, zeby wyjatek nie gubil zlota).
        /// Bez wlasnego catch - wyjatek liczy Sutlers (potkniecie na partie).
        /// </summary>
        internal static int CampPlan(MobileParty mp, float rate, out int reserve, out int kept) { return CampPlan(mp, rate, -1L, out reserve, out kept); }

        /// <summary>To samo z zaleglymi naprawami policzonymi juz przez wolajacego (`mend` >= 0 - T3-K1: Sutlers.One liczy je raz, do odziezy i do rezerwy;
        /// -1 - liczone tutaj, jak dotad).</summary>
        internal static int CampPlan(MobileParty mp, float rate, long mend, out int reserve, out int kept)
        {
            reserve = 0; kept = 0;
            if (!On || mp == null || rate <= 0f) return 0;
            int purse = Get(mp);
            if (purse <= 0) return 0;
            long res;
            CampReserve(mp, purse, mend, out res, out kept);
            reserve = (int)Math.Min(int.MaxValue, res);
            int avail = (int)Math.Max(0L, purse - res);
            return Math.Max(0, (int)((avail - kept) * (double)Math.Min(1f, rate)));
        }

        /// <summary>E5/A3 i T3-K1: zalegle naprawy partii w polu (robocizna, bez szacunku materialu - poza miastem nie ma polki) - rezerwa markietanow
        /// i budzet odziezy u markietanow liczone z tej samej liczby. Bez wlasnego catch (wyjatek liczy Sutlers).</summary>
        internal static long CampMend(MobileParty mp) { return Math.Max(0, mp.IsMainParty ? TroopSelfMend.OutstandingCost(null) : AiWear.OutstandingCost(mp, null)); }

        /// <summary>T3-K1 (SutlerClothing): odziez kupiona u markietanow w polu (ArmyClothing.FromSutlers - zloto juz zdjete z sakiewki) do pozycji
        /// "na odziez wojska (150)" linii "Sakiewka ludzi" (jak przy wyjezdzie z miasta).</summary>
        internal static void NoteCampCloth(int n) { if (n <= 0) return; Day(); _dayCloth += n; }

        /// <summary>E5/A3: zdjecie kwoty z CampPlan z sakiewki (licznik linii "Sakiewka ludzi"); zwraca zdjete.</summary>
        internal static int CampTake(MobileParty mp, int n)
        {
            if (!On || mp == null || n <= 0) return 0;
            Day();
            int t = Take(mp, n);
            _dayCamp += t;
            return t;
        }

        /// <summary>
        /// E5/A3: rezerwy markietanow. res = zalegle naprawy (robocizna) + odziez czekajaca na zakup; kept = oszczednosci K1 chronione TAK JAK w OnLeft
        /// (recenzja E5/A3 1-1): nie wiecej niz sakiewka po rezerwach i niz limit MenGearSaveDays x zold partii TERAZ minus zalegle naprawy. Etykieta _saved
        /// zostaje (przycina ja OnLeft przy wyjezdzie) - po stratach w polu (mniejszy zold) albo przy rosnacych naprawach markietani nie chronia nadwyzki
        /// ponad limit. Odziez nie zmniejsza limitu - w OnLeft kupowana jest przed odlozeniem, a limit liczy sie od samej rezerwy napraw.
        /// </summary>
        private static void CampReserve(MobileParty mp, int purse, out long res, out int kept) { CampReserve(mp, purse, -1L, out res, out kept); }

        private static void CampReserve(MobileParty mp, int purse, long mendIn, out long res, out int kept) { CampReserve(mp, purse, mendIn, 1f, out res, out kept); }

        /// <summary>N6 (noc 16.10, SutlersByPurse): `capShare` - czesc limitu MenGearSaveDays chroniona w polu (Sutlers: 0.5 dla partii bez zakupu sprzetu od 14 dob,
        /// inaczej 1 = jak dotad). Limit przy wyjezdzie z miasta (OnLeft) bez zmian.</summary>
        private static void CampReserve(MobileParty mp, int purse, long mendIn, float capShare, out long res, out int kept)
        {
            long mend = mendIn >= 0 ? mendIn : CampMend(mp);
            res = mend + Math.Max(0, ArmyClothing.PendingCost(mp));
            kept = 0;
            var s = Settings.Current;
            if (!MenUpgrade.On || s == null) return;
            int avail = (int)Math.Max(0L, purse - res);
            long cap = (long)Math.Max(0, s.MenGearSaveDays) * Math.Max(0, Wage(mp));
            if (capShare < 1f) cap = (long)(cap * (double)Math.Max(0f, capShare));
            int room = (int)Math.Max(0L, Math.Min(int.MaxValue, cap - mend));
            kept = Math.Min(Math.Min(SavedOf(mp), avail), room);
        }

        /// <summary>N6 (Sutlers.One): wolna sakiewka partii w polu (po rezerwach napraw i odziezy i po oszczednosciach K1 chronionych do `capShare` limitu) - ta sama
        /// regula co CampPlan (CampPlan = wolna x stawka). `mend` policzone przez wolajacego. Bez wlasnego catch - wyjatek liczy Sutlers.</summary>
        internal static int CampFree(MobileParty mp, long mend, float capShare, out int reserve, out int kept)
        {
            reserve = 0; kept = 0;
            if (!On || mp == null) return 0;
            int purse = Get(mp);
            if (purse <= 0) return 0;
            long res;
            CampReserve(mp, purse, mend, capShare, out res, out kept);
            reserve = (int)Math.Min(int.MaxValue, res);
            int avail = (int)Math.Max(0L, purse - res);
            return Math.Max(0, avail - kept);
        }

        /// <summary>N6: dzienny zold partii (dni wolnej sakiewki).</summary>
        internal static int WageOf(MobileParty mp) { return Wage(mp); }

        // ------------------------------------------------------------ gracz: nadwyzki na targ
        // K1 (A9): po DOPASOWANIU, nie po liczbie sztuk - najpierw sztuki LUDZI, ktorych nikt nie udzwignie, potem najgorsze uzyteczne
        // ponad komplet + SurplusKeepPercent; w zapasie zostaja najlepsze wolne uzyteczne. Dotad liczenie po typie trzymalo T6, ktorej
        // nikt nie naciagnie, a sprzedawalo uzyteczne T3. Czesc gracza (ksiega, NAJGORSZE egzemplarze id) nietknieta - dotad
        // pomijane bylo cale id, gdy gracz mial w nim choc jedna sztuke.
        /// <summary>K1 A9 - JEDNA definicja nadwyzki ludzi gracza w zbrojowni DTE: po dopasowaniu (przy wymianie 1:1 FitBest, inaczej Fit) Sell = co ponad
        /// komplet + SurplusKeepPercent (najpierw sztuki, ktorych nikt nie udzwignie, potem najgorsze uzyteczne); czesc gracza (Own) nietknieta. Kolejnosc
        /// WorseFirst; null - bez nadwyzki. Scalenie sklad8 (warunek scalenia K1 z naglowka Drill.cs): ta sama funkcja daje sprzedaz kupcom (SellPlayerSurplus)
        /// i zuzycie musztry (Drill.PlayerSurplus) - musztra nie zuzywa sztuk, ktore K1 uznaje za potrzebne (bez petli zuzycia i odkupu).</summary>
        internal static List<SwapMath.Piece> PlayerSurplusPlan(ItemRoster armory, ItemObject.ItemTypeEnum type)
        {
            var s = Settings.Current;
            var main = MobileParty.MainParty;
            if (armory == null || s == null || main == null || main.MemberRoster == null) return null;
            // Jeff 15.10 (b), PlayerArmoryView: zbedne konie ludzi (ponad 1 na jezdzca + SurplusKeepPercent i te, ktorych zaden ich jezdziec nie dosiadzie) do kupca
            // jak reszta lupu; klucz wylaczony - konie zostaja w zbrojowni, jak dotad (Stajnia). Musztra (Drill.PlayerSurplus) koni nie pyta (StockTypes bez Horse).
            if (type == ItemObject.ItemTypeEnum.Horse && !PlayerArmoryView.SellHorses) return null;
            var pieces = QuartermasterLaw.KitPieces(armory, type, true);
            if (pieces.Count == 0) return null;
            List<CharacterObject> troops; int[] men; SkillObject skill;
            QuartermasterLaw.MenOf(main.MemberRoster, type, pieces, out troops, out men, out skill);
            var meets = QuartermasterLaw.MeetsFitOf(troops);   // Jeff 15.10: ten sam predykat co wymiana (FitFor) - umiejetnosc, siodlo, kultura (GearFit)
            // K1 (Jeff 09.10 04:40): kto co nosi - ta sama regula co wymiana (najlepsze najpierw; sztuka gracza tylko w puste rece albo
            // w miejsce gorszej), inaczej ludzie sprzedawaliby lepsza sztuke, ktora nosza, bo "noszona" byla gorsza sztuka gracza
            if (s.QuartermasterSwapOneForOne) SwapMath.FitBest(men, troops.Count, meets, pieces);
            else SwapMath.Fit(men, troops.Count, meets, pieces);
            // ZB-3 (klucz SurplusUsableAnyGroup): "ktos udzwignie" - kazda grupa ludzi, nie tylko najzdolniejsza (wylaczony - SwapMath.Usable, jak dotad)
            if (SwapMath.SurplusPlan(pieces, men.Length, s.SurplusKeepPercent, ArmouryZb.SurplusUsable(men, meets, false)) <= 0) return null;
            pieces.Sort(SwapMath.WorseFirst);
            return pieces;
        }

        private static void SellPlayerSurplus(Settlement st)
        {
            var armory = QuartermasterLaw.DteArmory();
            if (armory == null || QuartermasterEscrow.Active) return;
            var main = MobileParty.MainParty;
            int sold = 0, gold = 0, horses = 0, horseGold = 0;
            foreach (var type in QuartermasterLaw.KitTypes)
            {
                var pieces = PlayerSurplusPlan(armory, type);   // K1 A9 - jedna definicja nadwyzki (konie - przy PlayerMenSellSpareHorses, Jeff 15.10 (b))
                if (pieces == null) continue;
                foreach (var p in pieces)
                {
                    if (p.Sell <= 0) continue;
                    var el = QuartermasterLaw.ElOf(p);
                    // sklad8-p (przeglad sklad8, uwagi 5 i 11): sztuka po sztuce, cena od nowa po kazdej sztuce dodanej na polke - jak nadwyzki lordow AI
                    // i zalog (SellArmorySurplus) i jak gracz na ekranie handlu; kasa miasta sprawdzana przed kazda sztuka. Dotad cena pierwszej x n
                    // (kupiec przeplacal ludziom gracza za partie). Kasa pusta na te sztuke - nastepny egzemplarz (tanszy moze sie zmiescic), jak dotad.
                    int n = 0, paidN = 0;
                    for (int k = 0; k < p.Sell; k++)
                    {
                        int unit = SellPrice(el, st, main);
                        if (unit <= 0 || st.Town.Gold - TownPurse.BuyFloor(st.Town, 0) < unit) break;   // E5/A1: miasto tylko z nadwyzki ponad zapas kupcow
                        armory.AddToCounts(el, -1);
                        st.ItemRoster.AddToCounts(el, 1);
                        st.Town.ChangeGold(-unit);
                        Measure174b.NoteSurplusSale(el.Item, 1, unit);                          // 174b.0 M3 (tylko licznik)
                        SellByCondition.NoteSale(SellByCondition.Men, el, 1, unit);             // ksiega skupu sprzetu (tylko log)
                        n++; paidN += unit;
                    }
                    if (n <= 0) continue;
                    Measure174b.NoteArrival(st, el.Item, n, Measure174b.ArrPurse);          // 174b.0 M1: nowa sztuka na polce
                    MoneyLedger.Note169(MoneyLedger.N169Surplus, st, -paidN);   // paczka 169: linia kas (tylko licznik)
                    Add(main, paidN);
                    sold += n; gold += paidN;
                    if (type == ItemObject.ItemTypeEnum.Horse) { horses += n; horseGold += paidN; }   // Jeff 15.10 (b): tylko do linii
                    MenUpgrade.NoteChurn(main, el.Item, false);                   // K1 (przeglad): kupione i sprzedane tej samej doby (autotest: 0)
                    GearFit.GapIdle(p.Key, n);                                    // GearFit (kontrola po recenzji): kupione na brak i sprzedane jako nadwyzka (tylko licznik)
                }
            }
            if (sold > 0)
            {
                _daySold += sold; _dayGold += gold;
                Log.Player("Your men sold " + sold + " spare pieces" + (horses > 0 ? " (" + horses + (horses == 1 ? " of them a horse)" : " of them horses)") : "") + " from the stores to the merchants of " + st.Name + " for " + gold
                           + " denars - their share of the spoils. Purse of the men: " + Get(main) + ".");
                Log.Info("Sakiewka ludzi: gracz w " + st.Name + " - nadwyzki " + sold + " szt. za " + gold + " (w tym konie " + horses + " za " + horseGold + "), sakiewka " + Get(main) + ".");
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
                // GearFit (kontrola po recenzji, tylko licznik): egzemplarz kupiony na brak w ostatnich dniach lezy wolny, a ludzie tego typu dalej bez sztuki
                if (gap > 0 && fit.Pieces != null)
                    foreach (var fp in fit.Pieces)
                        if (!fp.Barred && fp.MenFree > 0) GearFit.GapIdle(fp.Key, fp.MenFree);
                // K1c (przeglad K1b, Jeff 09.10 P1, "rekrut nie kupi plyty"): k-ty zakup typu najwyzej do k-tego sufitu ludzi bez sztuki
                // (SwapMath.CeilingTier, od najwyzszego); dotad braki nie mialy zadnego limitu tieru
                var ceils = MenUpgrade.CeilingsOf(fit.UnfitByTroop, Settings.Current.MenUpgradeOneTierUp);
                int gap0 = gap;
                while (gap > 0 && pieces < maxPieces && spent < budget)
                {
                    int best = -1, bestPrice = 0; float bestScore = 0f;
                    int maxTier = ceils.Count > 0 ? ceils[Math.Min(gap0 - gap, ceils.Count - 1)] : int.MaxValue;
                    bool resHit = false;
                    for (int i = 0; i < shelf.Count; i++)
                    {
                        var el = shelf.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || !QuartermasterLaw.CountsAsKit(it, type) || ArmsPricing.IsUnique(it)) continue;
                        if (it.Difficulty > 0 && ItemReq.SkillFor(it) != null && it.Difficulty > maxReq) continue;   // najslabszy bez sztuki ja udzwignie
                        // GearFit (poprawka po recenzji, uwaga krytyczna): ktos bez sztuki jej UZYJE wedlug tego samego predykatu co dopasowanie FitFor (MeetsFitOf:
                        // umiejetnosc, siodlo, kultura) - dotad tylko kultura: konnym kupowany byl dlugi luk / kusza / pika, dalej "bez sztuki", co dobe kolejne
                        if ((GearFit.On || GearFit.CultureOn) && !GearFit.AnyFitFor(fit.UnfitByTroop.Keys, it, GearFit.On)) continue;
                        if (AiGear.TierOf(it) > maxTier) continue;   // K1c (P1): nie ponad sufit czlowieka, dla ktorego ten zakup
                        if (ArmouryBehavior.StockOf(it.StringId) > 0) continue;   // K1 (przeglad): ksiega per id - zakup ludzi przesunalby Twoja czesc na gorszy egzemplarz
                        if (ShopReserve.Free(st, it) <= 0) { resHit = true; continue; }   // 174b.4: ludzie gracza kupuja hurtem jak ludzie lorda - ostatnia sztuka pasma zostaje
                        int price = st.Town.MarketData.GetPrice(el.EquipmentElement, main, false, st.Party);
                        if (price <= 0 || price > budget - spent) continue;
                        float score = XbowPrice.BuyScore(it, it.Effectiveness > 0f ? it.Effectiveness : 1f, price);   // kusze wedlug luku (XbowBuyByPower): kusza skutecznosc^2 / cena
                        if (score > bestScore) { bestScore = score; best = i; bestPrice = price; }
                    }
                    if (best < 0) { if (resHit) Measure174b.NoteHeld(Measure174b.BPlayerMen, 1); break; }
                    var pick = shelf.GetElementCopyAtIndex(best).EquipmentElement;
                    shelf.AddToCounts(pick, -1);
                    armory.AddToCounts(pick, 1);
                    Measure174b.NoteBuy(Measure174b.BPlayerMen, pick.Item, 1, bestPrice);   // 174b.0 M2 (tylko licznik)
                    Take(main, bestPrice);
                    st.Town.ChangeGold(bestPrice);
                    MoneyLedger.Note169(MoneyLedger.N169Kit, st, bestPrice);   // paczka 169: linia kas (tylko licznik)
                    MenUpgrade.NoteChurn(main, pick.Item, true);
                    XbowPrice.NoteBuy(pick.Item, 1, false);   // kusze wedlug luku: linia "Kusze wedlug luku (zakupy)" (tylko licznik)
                    GearFit.NoteGapBuy(QuartermasterLaw.KeyOf(pick));   // kontrola po recenzji: zakup braku bez skutku (tylko licznik)
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
        // sklad7 (scalenie 171 + K1 - JEDNA regula nadwyzek lordow i zalog): zaloga sprzedaje tu raz w tygodniu (kolejka 171 C10, GarrisonArmory.SellWeek)
        // na polke WLASNEJ osady (zamek - polka zamku, 171 Z6: kupcy wywoza ja do miast, bez teleportu do miasta); ludzie zalogi na patrolach BK licza sie
        // do potrzeby (roster - zaloga + patrole; ich sprzet lezy w twierdzy); bez sakiewki zalogi (GarrisonPurseEnabled off) cale zloto dostaje pan (171).
        // 174 pytanie 2a: przy AiAnyMeleeWhenShort bron biala liczona razem - sztuka zastepcza innego typu nie jest nadwyzka (limit grupy AiGear.MeleeGroupExtra).
        /// <summary>K1 A9 + 174 pytanie 2a - JEDNA definicja nadwyzki zbrojowni AI (lordowie i zalogi): ile wolno oddac z calej grupy broni bialej przy
        /// AiAnyMeleeWhenShort (int.MaxValue = bez grupy). Scalenie sklad8: sprzedaz (SellArmorySurplus) i zuzycie musztry AI (Drill.AiSurplus).</summary>
        internal static int AiMeleeLeft(Dictionary<ItemObject, int> arm, TroopRoster roster)
        {
            var s = Settings.Current;
            if (!AiGear.SubstituteMeleeOn || arm == null || roster == null || s == null) return int.MaxValue;
            var have = new Dictionary<int, int>();
            foreach (var kv in arm)
            {
                if (kv.Key == null || kv.Value <= 0 || !SupplyDemand.Equipmentish(kv.Key) || HorseKind(kv.Key) || ArmsPricing.IsUnique(kv.Key)) continue;
                int k = (int)kv.Key.ItemType; int n; have.TryGetValue(k, out n); have[k] = n + kv.Value;
            }
            return AiGear.MeleeGroupExtra(have, GarrisonArmory.NeedByType(roster), s.SurplusKeepPercent);
        }

        /// <summary>K1 A9 - JEDNA definicja nadwyzki zbrojowni AI typu: po dopasowaniu (bron po slotach wzorca, FitBest) Sell = co ponad komplet
        /// + SurplusKeepPercent (najpierw sztuki, ktorych nikt nie udzwignie, potem najgorsze uzyteczne). Kolejnosc WorseFirst; null - bez nadwyzki.
        /// Scalenie sklad8 (warunek scalenia K1 z naglowka Drill.cs): ta sama funkcja daje sprzedaz kupcom (SellArmorySurplus) i zuzycie musztry AI
        /// (Drill.AiSurplus) - musztra nie zuzywa sztuk, ktore K1 uznaje za potrzebne, a zakupy brakow ich nie odkupuja.</summary>
        internal static List<SwapMath.Piece> AiSurplusPlan(Dictionary<ItemObject, int> arm, TroopRoster roster, ItemObject.ItemTypeEnum type)
        {
            var s = Settings.Current;
            if (s == null) return null;
            int[] men; Func<int, SwapMath.Piece, bool> meets;
            var pieces = AiFitted(arm, roster, type, out men, out meets);   // E6/N1: dopasowanie wydzielone (to samo czyta AiWornByMen) - bez zmiany dzialania
            if (pieces == null) return null;
            // ZB-3 (klucz SurplusUsableAnyGroup): jak u gracza - kazda grupa (lordowie AI i zalogi; musztra AI czyta ten sam plan)
            if (SwapMath.SurplusPlan(pieces, men.Length, s.SurplusKeepPercent, ArmouryZb.SurplusUsable(men, meets, true)) <= 0) return null;
            pieces.Sort(SwapMath.WorseFirst);
            return pieces;
        }

        /// <summary>K1 A9 - dopasowanie zbrojowni AI typu (wynik w Used): ludzie nosza najlepsze, co udzwigna. null - konie i rzedy (Stajnia) albo brak sztuk.
        /// E6/N1 (217): wydzielone z AiSurplusPlan, zeby "co nosza ludzie" (naprawy AI - AiWornByMen) i "co jest nadwyzka" byly jednym dopasowaniem.</summary>
        private static List<SwapMath.Piece> AiFitted(Dictionary<ItemObject, int> arm, TroopRoster roster, ItemObject.ItemTypeEnum type, out int[] men, out Func<int, SwapMath.Piece, bool> meets)
        {
            men = null; meets = null;
            if (arm == null || roster == null || type == ItemObject.ItemTypeEnum.Horse || type == ItemObject.ItemTypeEnum.HorseHarness) return null;   // konie i rzedy - Stajnia
            long q0 = DayProfile.FitBegin();   // NW O6 (b): czesci dopasowania w probce 1/16 (tylko pomiar)
            var pieces = MenUpgrade.AiPieces(arm, type);
            if (pieces.Count == 0) { DayProfile.FitShort(q0); return null; }
            long q1 = DayProfile.Now(q0);
            List<CharacterObject> troops; SkillObject skill;
            // K1 (przeglad): bron po SLOTACH WZORCA (jak braki AiGear.Deficit i MenUpgrade.Buckets) - dotad 1 bron na czlowieka, wiec
            // druga bron oddzialu szla do kupca, a zakupy brakow (dawniej K1 BuyGaps) odkupowaly ja tego samego dnia
            QuartermasterLaw.MenOf(roster, type, pieces, out troops, out men, out skill, QuartermasterLaw.MenPerSlot, false);
            // GearFit (poprawka po recenzji, uwaga powazna): kto co nosi - ten sam predykat co u ludzi gracza i w bitwie (MeetsFitOf: umiejetnosc, siodlo, kultura);
            // dotad MeetsOf (sama umiejetnosc) - lepsza sztuka zza morza byla "noszona", gorsza wlasna szla do kupca, a w bitwie RealmWard zdejmowal obca (slot pusty)
            meets = QuartermasterLaw.MeetsFitOf(troops);
            long q2 = DayProfile.Now(q0);
            // K1 (Jeff 09.10 04:40, "to samo dla AI"): ludzie nosza najlepsze, co udzwigna (FitBest), a do kupca idzie najgorsze - dotad
            // ciezki gorszy grat byl "noszony", a lepsza lzejsza sztuka szla do kupca (czlowiek zostawal z gorsza w miejsce lepszej)
            SwapMath.FitBest(men, troops.Count, meets, pieces);
            DayProfile.FitEnd(q0, q1, q2);
            return pieces;
        }

        /// <summary>
        /// E6/N1 (217, R16, "AI naprawia tylko to, co nosi"): ile sztuk kazdego przedmiotu zbrojowni AI nosza ludzie - to samo dopasowanie co nadwyzki
        /// (AiFitted: Used). Tylko typy z types (null - wszystkie KitTypes; konie i rzedy pominiete). Przedmiot spoza wyniku - nikt go nie nosi.
        /// </summary>
        internal static Dictionary<ItemObject, int> AiWornByMen(Dictionary<ItemObject, int> arm, TroopRoster roster, ICollection<ItemObject.ItemTypeEnum> types)
        {
            var used = new Dictionary<ItemObject, int>();
            if (arm == null || roster == null) return used;
            foreach (var type in QuartermasterLaw.KitTypes)
            {
                if (types != null && !types.Contains(type)) continue;
                int[] men; Func<int, SwapMath.Piece, bool> meets;
                var pieces = AiFitted(arm, roster, type, out men, out meets);
                if (pieces == null) continue;
                foreach (var p in pieces)
                {
                    var it = QuartermasterLaw.ElOf(p).Item;
                    if (it == null || p.Used <= 0) continue;
                    int v; used.TryGetValue(it, out v); used[it] = v + p.Used;
                }
            }
            return used;
        }

        private static int SellArmorySurplus(MobileParty mp, Settlement st, Hero third, bool garrison, TroopRoster roster, out int gold)
        {
            gold = 0;
            var dict = AiGear.Armories();
            Dictionary<ItemObject, int> arm;
            if (dict == null || !dict.TryGetValue(mp.Id, out arm) || arm == null || arm.Count == 0) return 0;
            if (st == null || st.Town == null || st.ItemRoster == null) return 0;
            var s = Settings.Current;
            if (roster == null) roster = mp.MemberRoster;
            float pct = garrison && !MenUpgrade.GarrisonPurseOn ? 100f : MBMath.ClampFloat(s.LordLootThirdPercent, 0f, 100f);   // bez sakiewki zalogi - wszystko panu
            int sold = 0, cutSum = 0;   // 3.9: cutSum - trzecia oddana juz w tej wizycie (Drobne39.ThirdCut)
            bool stop = false;
            int meleeLeft = AiMeleeLeft(arm, roster);   // K1 A9 - jedna definicja nadwyzki (int.MaxValue = bez grupy broni bialej)
            foreach (var type in QuartermasterLaw.KitTypes)
            {
                if (stop) break;
                var pieces = AiSurplusPlan(arm, roster, type);   // konie i rzedy - Stajnia
                if (pieces == null) continue;
                bool grp = AiGear.Melee((int)type) && meleeLeft != int.MaxValue;
                int cap = grp ? meleeLeft : int.MaxValue, planned = 0;
                if (cap <= 0) continue;
                foreach (var p in pieces)
                {
                    var it = QuartermasterLaw.ElOf(p).Item;
                    int sold0 = sold;   // GearFit (po recenzji): sprzedane sztuki tego egzemplarza - licznik obcej kultury nizej
                    for (int k = 0; k < p.Sell && !stop && planned < cap; k++)
                    {
                        int cnt;
                        if (it == null || !arm.TryGetValue(it, out cnt) || cnt <= 0) break;
                        var el = new EquipmentElement(it, AiWear.TakeCondition(mp, it));
                        int unit = SellPriceHere(el, st, mp);
                        if (CastlePurse.Spendable(st.Town) < unit) { AiWear.PutBack(mp, it, el.ItemModifier); stop = true; break; }   // kasa pusta - koniec na dzis (stan wraca); B-2: zamek - tylko nadwyzka ponad zapas kupcow
                        if (cnt > 1) arm[it] = cnt - 1; else arm.Remove(it);
                        st.ItemRoster.AddToCounts(el, 1);
                        Measure174b.NoteSurplusSale(it, 1, unit);                          // 174b.0 M3 (tylko licznik)
                        Measure174b.NoteArrival(st, it, 1, garrison ? Measure174b.ArrGarrison : Measure174b.ArrPurse);   // 174b.0 M1: nowa sztuka na polce (scalenie sklad8: nadwyzki zalog ida tu - zrodlo "zalogi" jak w 174b)
                        st.Town.ChangeGold(-unit);
                        MoneyLedger.Note169(MoneyLedger.N169Surplus, st, -unit);   // paczka 169: linia kas (tylko licznik)
                        int cut = third != null && third.IsAlive ? Drobne39.ThirdCut(unit, gold, cutSum, pct) : 0;   // 3.9: od sumy wizyty (dotad od sztuki - przy cenie 1 zl dawalo 0)
                        if (cut > 0)
                        {
                            third.ChangeHeroGold(cut);
                            if (third != Hero.MainHero) ClanIncomeBook.NoteInflow(third, cut, ClanIncomeBook.KThird);   // paczka 169: D rodu (tylko licznik)
                        }
                        Add(mp, unit - cut);
                        sold++; gold += unit; _dayLord += cut; planned++; cutSum += cut;
                        SellByCondition.NoteSale(SellByCondition.Men, el, 1, unit);   // ksiega skupu sprzetu (tylko log)
                        MenUpgrade.NoteChurn(mp, it, false);                           // K1 (przeglad): kupione i sprzedane tej samej doby (autotest: 0)
                    }
                    if (sold > sold0 && GearFit.ForeignToAll(roster, it)) GearFit.NoteAiForeignSold(sold - sold0, garrison);   // tylko licznik linii "GearFit: dzien"
                }
                if (grp) meleeLeft -= planned;
            }
            // E8/Z (251, RC8 poz. 28): uprzaz ponad 1.5 x zapas (AiBuyRules.HarnessKeep) - na polke tej osady jak reszta nadwyzek sprzetu (lordowie i zalogi;
            // sztuki i zloto w licznikach nadwyzek tej wizyty, trzecia od sumy wizyty); kasa pusta przy sprzecie - uprzezy tez nie dzis. Przed galezia koni
            if (!stop && AiBuyRules.HarnessOn) SellSpareHarness(mp, st, third, pct, arm, roster, garrison, ref sold, ref gold, ref cutSum, ref stop);
            // E6/K2 (212): osobna galaz koni (AiSurplusPlan dalej bez koni) - tylko partie lordow; konie i ich zloto poza licznikami nadwyzek sprzetu tej linii
            // (segment E6/K2 linii "Konie AI (175)"); kasa pusta przy sprzecie - koni tez nie dzis
            if (!garrison && !stop && SpareHorses.SellOn) SellSpareHorses(mp, st, third, pct, arm, gold, ref cutSum);
            if (sold > 0) { _daySold += sold; _dayGold += gold; if (garrison) { _dayGarSold += sold; _dayGarGold += gold; } }
            return sold;
        }

        /// <summary>
        /// E8/Z (251, PLAN 8.8; RC8 poz. 28): uprzaz zbrojowni partii AI ponad 1.5 x zapas (AiBuyRules.HarnessKeep - konni szeregowi + gotowi do awansu na
        /// jezdnego + luz, histereza jak konie E6/K2) na polke tej osady. Dotad uprzaz po poleglych jezdzcach lezala w zbrojowni na zawsze (AiSurplusPlan
        /// pomija konie i rzedy, a Stajnia uprzezy nie sprzedaje). Od najnizszego tieru i najtanszej (zostaja sztuki, ktore pokrywaja koszyki zakupu uprzezy
        /// E6/R3 - bez petli kup-sprzedaj); bez unikatow; sztuka bez stanu (ksiega zuzycia AiWear nie prowadzi koni i rzedow). Zaplata jak za reszte nadwyzek:
        /// cena skupu tej polki liczona przy kazdej sztuce, kasa osady tylko z nadwyzki ponad zapas kupcow (CastlePurse.Spendable), trzecia panu od sumy
        /// wizyty (Drobne39.ThirdCut), reszta do sakiewki ludzi. Brak kupca konczy sprzedaz tej wizyty (stop - koni tez nie dzis).
        /// </summary>
        private static void SellSpareHarness(MobileParty mp, Settlement st, Hero third, float pct, Dictionary<ItemObject, int> arm, TroopRoster roster, bool garrison,
                                             ref int sold, ref int gold, ref int cutSum, ref bool stop)
        {
            int hSold = 0, hGold = 0, hThird = 0;
            bool noBuyer = false;
            try
            {
                List<ItemObject> items = null;
                int have = 0;
                foreach (var kv in arm)
                {
                    if (kv.Value <= 0 || !AiBuyRules.SpareHarness(kv.Key)) continue;
                    have += kv.Value;
                    if (items == null) items = new List<ItemObject>();
                    items.Add(kv.Key);
                }
                if (items == null) return;
                int riders;
                int over = have - AiBuyRules.HarnessKeep(mp, roster, garrison, out riders);
                if (over <= 0) return;
                // E8/H (271): jedna definicja potrzeby dla kupna i nadwyzki - zapas E8/Z liczy uprzaz bez rodzin wierzchowcow, a sprzedaje od najtanszej, czyli
                // najpierw siodla koni. Uprzaz rodziny konia nie schodzi ponizej zapasu siodel (SaddleLaw.Target - ten sam, do ktorego kupuje AiGear.BuySaddles),
                // inaczej partia z nadmiarem uprzezy innej rodziny sprzedawalaby siodla, ktore zaraz by odkupila (pompa)
                int fitKeep = 0, fitHave = 0, held = 0;
                if (SaddleLaw.On) { SaddleLaw.PairState ps; fitKeep = SaddleLaw.Target(mp, roster, garrison, out ps); fitHave = ps.Harness; }
                items.Sort((a, b) =>
                {
                    int d = AiGear.TierOf(a).CompareTo(AiGear.TierOf(b)); if (d != 0) return d;
                    d = a.Value.CompareTo(b.Value); if (d != 0) return d;
                    return string.CompareOrdinal(a.StringId, b.StringId);
                });
                foreach (var it in items)
                {
                    if (over <= 0 || noBuyer) break;
                    var el = new EquipmentElement(it);
                    bool fit = fitKeep > 0 && SaddleLaw.Fits(it);
                    while (over > 0)
                    {
                        int cnt;
                        if (!arm.TryGetValue(it, out cnt) || cnt <= 0) break;
                        if (fit && fitHave <= fitKeep) { held += Math.Min(over, cnt); break; }   // E8/H: zapas siodel zostaje
                        int unit = SellPriceHere(el, st, mp);
                        if (CastlePurse.Spendable(st.Town) < unit) { noBuyer = true; stop = true; break; }   // kasa pusta - koniec na dzis
                        if (cnt > 1) arm[it] = cnt - 1; else arm.Remove(it);
                        st.ItemRoster.AddToCounts(el, 1);
                        Measure174b.NoteSurplusSale(it, 1, unit);                          // 174b.0 M3 (tylko licznik)
                        Measure174b.NoteArrival(st, it, 1, garrison ? Measure174b.ArrGarrison : Measure174b.ArrPurse);   // ksiega uzbrojenia E6/0: przeniesienie (tylko licznik)
                        st.Town.ChangeGold(-unit);
                        MoneyLedger.Note169(MoneyLedger.N169Surplus, st, -unit);   // paczka 169: linia kas (tylko licznik)
                        int cut = third != null && third.IsAlive ? Drobne39.ThirdCut(unit, gold, cutSum, pct) : 0;
                        if (cut > 0)
                        {
                            third.ChangeHeroGold(cut);
                            if (third != Hero.MainHero) ClanIncomeBook.NoteInflow(third, cut, ClanIncomeBook.KThird);   // paczka 169: D rodu (tylko licznik)
                        }
                        Add(mp, unit - cut);
                        sold++; gold += unit; _dayLord += cut; cutSum += cut;
                        hSold++; hGold += unit; hThird += cut; over--;
                        if (fit) fitHave--;
                        SellByCondition.NoteSale(SellByCondition.Men, el, 1, unit);   // ksiega skupu sprzetu (tylko log)
                        MenUpgrade.NoteChurn(mp, it, false);                           // kupione i sprzedane tej samej doby (linia "Dozbrajanie"; autotest: 0)
                    }
                }
                SaddleLaw.NoteSaleHeld(held);
            }
            catch (Exception e) { AiBuyRules.Stumble("SellSpareHarness", e); }
            SaddleLaw.NoteSale(mp, hSold);   // E8/H (271): doba sprzedazy uprzezy tej partii - licznik "kupno po sprzedazy" (tylko log)
            AiBuyRules.NoteHarness(garrison, hSold, hGold, hThird, noBuyer);
        }

        /// <summary>
        /// E6/K2 (212, R11): konie partii lorda ponad 1.5 x zapas kategorii (Stables.Reserve - ta sama funkcja co kupno Stajni AI) na polke miasta. Najgorsze konie
        /// kategorii najpierw: z taboru (wartosc z modyfikatorem) albo wolne w zbrojowni DTE (najwyzej Free[k] z Balance - przydzial jezdzcom wedlug kategorii zostaje
        /// pokryty; stan konia jak przy sprzecie - AiWear.TakeCondition). Cena skupu tej polki liczona przy kazdej sztuce, kasa miasta tylko z nadwyzki ponad zapas
        /// kupcow (CastlePurse.Spendable); trzecia lordowi od sumy wizyty (Drobne39.ThirdCut - razem z nadwyzkami sprzetu tej wizyty), reszta do sakiewki ludzi.
        /// Kon przechodzi z partii na polke - przeniesienie w ksiedze "Konie (E6/0)".
        /// </summary>
        private static void SellSpareHorses(MobileParty mp, Settlement st, Hero third, float pct, Dictionary<ItemObject, int> arm, int kitGold, ref int cutSum)
        {
            Stables.HorseReserve r;
            try { r = Stables.Reserve(mp); }
            catch (Exception e) { SpareHorses.Stumble("Reserve", e); return; }
            var soldK = new int[3];
            int horseGold = 0, fromArm = 0;
            bool noBuyer = false;
            try
            {
                var bag = mp.ItemRoster;
                for (int k = 0; k < 3 && !noBuyer; k++)
                {
                    int over = r.Over(k), freeLeft = r.Free[k];
                    while (over > 0)
                    {
                        int bi = -1, bv = int.MaxValue, av = int.MaxValue;
                        ItemObject ai = null;
                        if (bag != null)
                            for (int i = 0; i < bag.Count; i++)
                            {
                                var e0 = bag[i];
                                var it0 = e0.EquipmentElement.Item;
                                if (it0 == null || e0.Amount <= 0 || !Stables.IsPlainMount(it0) || Stables.Level(it0.ItemCategory) != k) continue;
                                int v = e0.EquipmentElement.ItemValue;
                                if (v < bv) { bv = v; bi = i; }
                            }
                        if (freeLeft > 0 && arm != null)
                            foreach (var kv in arm)
                            {
                                if (kv.Key == null || kv.Value <= 0 || !Stables.IsPlainMount(kv.Key) || Stables.Level(kv.Key.ItemCategory) != k) continue;
                                if (kv.Key.Value < av) { av = kv.Key.Value; ai = kv.Key; }
                            }
                        if (bi < 0 && ai == null) break;
                        bool fromBag = bi >= 0 && (ai == null || bv <= av);
                        var el = fromBag ? bag[bi].EquipmentElement : new EquipmentElement(ai, AiWear.TakeCondition(mp, ai));
                        int unit = SellPriceHere(el, st, mp);
                        if (CastlePurse.Spendable(st.Town) < unit) { if (!fromBag) AiWear.PutBack(mp, ai, el.ItemModifier); noBuyer = true; break; }   // brak kupca - stan wraca
                        if (fromBag) bag.AddToCounts(el, -1);
                        else { int cnt = arm[ai]; if (cnt > 1) arm[ai] = cnt - 1; else arm.Remove(ai); freeLeft--; fromArm++; }
                        st.ItemRoster.AddToCounts(el, 1);
                        st.Town.ChangeGold(-unit);
                        MoneyLedger.Note169(MoneyLedger.N169Surplus, st, -unit);   // paczka 169: linia kas (tylko licznik)
                        int cut = third != null && third.IsAlive ? Drobne39.ThirdCut(unit, kitGold + horseGold, cutSum, pct) : 0;
                        if (cut > 0)
                        {
                            third.ChangeHeroGold(cut);
                            if (third != Hero.MainHero) ClanIncomeBook.NoteInflow(third, cut, ClanIncomeBook.KThird);   // paczka 169: D rodu (tylko licznik)
                        }
                        Add(mp, unit - cut);
                        horseGold += unit; cutSum += cut; _dayLord += cut;
                        soldK[k]++; over--;
                    }
                }
            }
            catch (Exception e) { SpareHorses.Stumble("SellSpareHorses", e); }
            if (fromArm > 0) HorseState.Refresh(mp);   // E6/K0: stan koni partii po zdjeciu wolnych koni zbrojowni
            SpareHorses.NoteVisit(mp, r, soldK, horseGold, fromArm, noBuyer);
        }

        /// <summary>sklad7: cena skupu na polce TEJ osady (miasto albo zamek - jego targ); bez targu - jak SellPrice (najblizsze miasto).
        /// W miescie to samo co SellPrice; zamek - nadwyzki zalogi i stara sztuka z dozbrajania ida na polke zamku po jego cenie (171 C10).</summary>
        internal static int SellPriceHere(EquipmentElement el, Settlement st, MobileParty seller)
        {
            try { if (st != null && st.Town != null && st.Town.MarketData != null) return Math.Max(1, st.Town.MarketData.GetPrice(el, seller, true, st.Party)); }
            catch { }
            return SellPrice(el, st, seller);
        }

        /// <summary>sklad7 (171 C10 + K1 A9): nadwyzki zalogi raz w tygodniu (kolejka GarrisonArmory.SellWeek) - na polke wlasnej osady; trzecia dla pana,
        /// reszta do sakiewki zalogi (bez sakiewki - wszystko panu). roster - zaloga razem z ludzmi na patrolach BK. Zwraca sprzedane sztuki.</summary>
        internal static int SellGarrisonSurplus(MobileParty g, Settlement st, Hero lord, TroopRoster roster, out int gold)
        {
            // poprawki sklad7: bez wlasnego catch - wyjatek idzie do GarrisonArmory.SellWeek (Stumble: jedna linia bledu na dobe, reszta w liczniku
            // potkniec); dotad Log.Error przy kazdej zalodze, do ok. 32 linii na dobe przy bledzie systematycznym
            gold = 0;
            Day();
            return SellArmorySurplus(g, st, lord, true, roster, out gold);
        }

        /// <summary>K1 (A11): doba zalogi - zaloga bez ludzi oddaje sakiewke do kasy osady. sklad7: nadwyzki zalog sprzedaje raz w tygodniu
        /// GarrisonArmory.SellWeek (ta sama funkcja SellArmorySurplus) - dotad K1 sprzedawal je tu codziennie w miescie handlowym zamku, a 171 raz w
        /// tygodniu na polke zamku: dwie sprzedaze tej samej zbrojowni.</summary>
        internal static void GarrisonDay(MobileParty mp, Settlement st)
        {
            try
            {
                if (!MenUpgrade.GarrisonPurseOn || mp == null || st == null || st.Town == null || !mp.IsGarrison) return;
                int men = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                if (men > 0) return;
                Day();
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
