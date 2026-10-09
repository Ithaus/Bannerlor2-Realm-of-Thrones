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
    /// </summary>
    internal static class MenPurse
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.MenPurseEnabled; } }

        private static readonly Dictionary<string, int> _purse = new Dictionary<string, int>();
        private static int _daySold, _dayGold, _dayLord, _dayLife, _dayGear, _dayStamp = -1;
        private static long _dayCloth;         // 150: odziez wojska kupiona z sakiewek przy wyjezdzie z miasta (ArmyClothing.BuyForParty)
        private static long _dayWage;          // zold wplacony do sakiewek (SoldierPay)
        private static int _dayWageN;
        private static long _dayIn, _dayOut;   // ruch wszystkich sakiewek w dobie: kazda wplata i kazdy wydatek ida przez Add

        internal static void Reset() { _purse.Clear(); _pending.Clear(); _daySold = _dayGold = _dayLord = _dayLife = _dayGear = 0; _dayCloth = 0; _dayWage = 0; _dayWageN = 0; _dayIn = _dayOut = 0; _dayStamp = -1; }

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

        private static string Key(MobileParty mp) { return mp != null ? mp.StringId : null; }
        internal static bool HorseKind(ItemObject it) { return it != null && (it.ItemType == ItemObject.ItemTypeEnum.Horse || it.ItemType == ItemObject.ItemTypeEnum.HorseHarness); }

        /// <summary>wpis 89 (audyt): rozbita partia - sakiewka ludzi idzie do zwyciezcy (lup), inaczej do najblizszego miasta; nic nie znika.</summary>
        internal static void OnPartyDestroyed(MobileParty mp, TaleWorlds.CampaignSystem.Party.PartyBase destroyer)
        {
            try
            {
                if (mp == null) return;
                AiWear.Forget(mp); AiGear.Forget(mp);
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
            if (_dayStamp >= 0 && (_daySold + _dayLife + _dayGear + _dayCloth + _dayWage + _dayIn + _dayOut) > 0)
                Log.Info("Sakiewka ludzi: dzien " + _dayStamp + " - nadwyzki sprzedane " + _daySold + " szt. za " + _dayGold + " (trzecia lordow AI " + _dayLord
                         + "), ludzie wydali na sprzet " + _dayGear + ", na zycie w miastach " + _dayLife + ", na odziez wojska (150) " + _dayCloth
                         + "; zold wplacony do sakiewek " + _dayWage + " (" + _dayWageN + " wyplat); ruch sakiewek: wplynelo " + _dayIn + " (zold, lup, przejete sakiewki), wyszlo " + _dayOut
                         + " (sprzet, naprawy, odziez, zycie w miastach, utracone sakiewki), w sakiewkach razem " + TotalNow() + ".");
            _daySold = _dayGold = _dayLord = _dayLife = _dayGear = 0; _dayCloth = 0; _dayWage = 0; _dayWageN = 0; _dayIn = _dayOut = 0; _dayStamp = d;
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
        internal static void OnEntered(MobileParty mp, Settlement st, Hero h)
        {
            try
            {
                if (!On || mp == null || st == null || !st.IsTown || st.Town == null) return;
                Day();
                if (mp.IsMainParty) { SellPlayerSurplus(st); BuyPlayerGaps(st); }
                else if (mp.IsLordParty && mp.LeaderHero != null && mp.LeaderHero.IsAlive && mp.MapEvent == null) { SellAiSurplus(mp, st); AiWear.MendInTown(mp, st); }
            }
            catch (Exception e) { Log.Error("MenPurse.OnEntered", e); }
        }

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
                int life = Math.Max(0, purse - reserve);
                if (life <= 0) return;
                Take(mp, life);
                st.Town.ChangeGold(life);           // karczma, jedzenie, gra, kobiety - pieniadze zostaja w miescie
                MoneyLedger.Note(MoneyLedger.NLife, st, life);   // ksiega przeplywow osad (tylko licznik)
                SoldierPay.Hold(st, life);          // tarcza zoldu (gdy wlaczona): regulator kasy nie skasuje tych pieniedzy, zanim zawor renty odda je panu
                _dayLife += life;
                if (mp.IsMainParty)
                    Log.Player("Your men spent " + life + " denars in " + st.Name + " - food, drink, dice and company." + (reserve > 0 ? " They kept " + Math.Min(purse, reserve) + " for mending their kit." : ""));
            }
            catch (Exception e) { Log.Error("MenPurse.OnLeft", e); }
        }

        // ------------------------------------------------------------ gracz: nadwyzki na targ
        private static void SellPlayerSurplus(Settlement st)
        {
            var armory = QuartermasterLaw.DteArmory();
            if (armory == null) return;
            var s = Settings.Current;
            var main = MobileParty.MainParty;
            int sold = 0, gold = 0;
            foreach (var type in QuartermasterLaw.KitTypes)
            {
                if (type == ItemObject.ItemTypeEnum.Horse) continue;   // konie - Stajnia
                int need = QuartermasterLaw.NeedForType(type);
                int have = QuartermasterLaw.HaveFor(armory, type);
                // Twoje wklady to nie zapas ludzi - odliczamy je od stanu
                var ownIds = new Dictionary<string, int>();
                for (int i = 0; i < armory.Count; i++)
                {
                    var el0 = armory.GetElementCopyAtIndex(i);
                    var it0 = el0.EquipmentElement.Item;
                    if (el0.Amount <= 0 || !QuartermasterLaw.CountsAsKit(it0, type)) continue;
                    int c0; ownIds.TryGetValue(it0.StringId, out c0); ownIds[it0.StringId] = c0 + el0.Amount;
                }
                foreach (var kv in ownIds) have -= Math.Min(kv.Value, Math.Max(0, ArmouryBehavior.StockOf(kv.Key)));
                int keep = (int)Math.Ceiling(need * (1f + Math.Max(0f, s.SurplusKeepPercent) / 100f));
                int extra = have - keep;
                if (extra <= 0) continue;
                // najgorsze najpierw: tier, potem stan; wklady gracza nietkniete
                var cand = new List<ItemRosterElement>();
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (el.Amount <= 0 || !QuartermasterLaw.CountsAsKit(it, type)) continue;
                    if (ArmouryBehavior.StockOf(it.StringId) > 0) continue;
                    if (ArmsPricing.IsUnique(it)) continue;   // unikat nie idzie do kupca hurtem
                    cand.Add(el);
                }
                cand.Sort((a, b) =>
                {
                    int c = a.EquipmentElement.Item.Tier.CompareTo(b.EquipmentElement.Item.Tier);
                    if (c != 0) return c;
                    float ma = a.EquipmentElement.ItemModifier != null ? a.EquipmentElement.ItemModifier.PriceMultiplier : 1f;
                    float mb = b.EquipmentElement.ItemModifier != null ? b.EquipmentElement.ItemModifier.PriceMultiplier : 1f;
                    return ma.CompareTo(mb);
                });
                foreach (var el in cand)
                {
                    if (extra <= 0) break;
                    int unit = SellPrice(el.EquipmentElement, st, main);
                    int n = Math.Min(extra, el.Amount);
                    n = Math.Min(n, Math.Max(0, st.Town.Gold) / Math.Max(1, unit));
                    if (n <= 0) continue;
                    armory.AddToCounts(el.EquipmentElement, -n);
                    st.ItemRoster.AddToCounts(el.EquipmentElement, n);
                    st.Town.ChangeGold(-unit * n);
                    MoneyLedger.Note169(MoneyLedger.N169Surplus, st, -unit * n);   // paczka 169: linia kas (tylko licznik)
                    Add(main, unit * n);
                    sold += n; gold += unit * n; extra -= n;
                    SellByCondition.NoteSale(SellByCondition.Men, el.EquipmentElement, n, unit);   // ksiega skupu sprzetu (tylko log)
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
        private static void BuyPlayerGaps(Settlement st)
        {
            var armory = QuartermasterLaw.DteArmory();
            var main = MobileParty.MainParty;
            if (armory == null) return;
            int budget = Get(main) - TroopSelfMend.OutstandingCost(st);   // naprawy maja pierwszenstwo (z materialem - szacunek z tej polki)
            if (budget <= 0) return;
            int spent = 0, pieces = 0, maxPieces = Math.Max(1, Settings.Current.AiGearMaxPiecesPerVisit);
            var shelf = st.ItemRoster;
            foreach (var type in QuartermasterLaw.KitTypes)
            {
                if (type == ItemObject.ItemTypeEnum.Horse) continue;
                int gap = QuartermasterLaw.NeedForType(type) - QuartermasterLaw.HaveFor(armory, type);
                while (gap > 0 && pieces < maxPieces && spent < budget)
                {
                    int best = -1, bestPrice = 0; float bestScore = 0f;
                    for (int i = 0; i < shelf.Count; i++)
                    {
                        var el = shelf.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || !QuartermasterLaw.CountsAsKit(it, type) || ArmsPricing.IsUnique(it)) continue;
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

        // ------------------------------------------------------------ AI: nadwyzki na targ, trzecia dla lorda
        private static void SellAiSurplus(MobileParty mp, Settlement st)
        {
            var dict = AiGear.Armories();
            Dictionary<ItemObject, int> arm;
            if (dict == null || !dict.TryGetValue(mp.Id, out arm) || arm == null || arm.Count == 0) return;
            var s = Settings.Current;
            var need = AiGear.NeedBuckets(mp);
            var have = new Dictionary<int, int>();
            // wpis 89 (audyt): po TYPIE - sztuka innego tieru pokrywa potrzebe (AiGear tez tak liczy); konie i rzedy - Stajnia,
            // NeedBuckets ich nie widzi, wiec dotad kazdy kon szedl do kupca jako "nadwyzka"
            foreach (var kv in arm) { if (kv.Key == null || kv.Value <= 0 || !SupplyDemand.Equipmentish(kv.Key) || HorseKind(kv.Key)) continue; int k = (int)kv.Key.ItemType; int n; have.TryGetValue(k, out n); have[k] = n + kv.Value; }
            var needT = new Dictionary<int, int>();
            foreach (var nk in need) { int ty = nk.Key / 10; int v; needT.TryGetValue(ty, out v); needT[ty] = v + nk.Value; }
            int sold = 0, gold = 0;
            foreach (var hk in have.ToList())
            {
                int nd; needT.TryGetValue(hk.Key, out nd);
                int keep = (int)Math.Ceiling(nd * (1f + Math.Max(0f, s.SurplusKeepPercent) / 100f));
                int extra = hk.Value - keep;
                if (extra <= 0) continue;
                var items = arm.Where(kv => kv.Key != null && kv.Value > 0 && SupplyDemand.Equipmentish(kv.Key) && !HorseKind(kv.Key) && (int)kv.Key.ItemType == hk.Key && !ArmsPricing.IsUnique(kv.Key))
                               .OrderBy(kv => kv.Key.Tier).ThenBy(kv => kv.Key.Value).Select(kv => kv.Key).ToList();
                foreach (var it in items)
                {
                    int cnt; if (!arm.TryGetValue(it, out cnt)) continue;
                    while (cnt > 0 && extra > 0)
                    {
                        var el = new EquipmentElement(it, AiWear.TakeCondition(mp, it));
                        int unit = SellPrice(el, st, mp);
                        if (st.Town.Gold < unit) { extra = 0; break; }
                        cnt--; extra--;
                        st.ItemRoster.AddToCounts(el, 1);
                        st.Town.ChangeGold(-unit);
                        MoneyLedger.Note169(MoneyLedger.N169Surplus, st, -unit);   // paczka 169: linia kas (tylko licznik)
                        int third = (int)Math.Round(unit * MBMath.ClampFloat(s.LordLootThirdPercent, 0f, 100f) / 100f);
                        mp.LeaderHero.ChangeHeroGold(third);
                        ClanIncomeBook.NoteInflow(mp.LeaderHero, third, ClanIncomeBook.KThird);   // paczka 169: D rodu (tylko licznik)
                        Add(mp, unit - third);
                        sold++; gold += unit; _dayLord += third;
                        SellByCondition.NoteSale(SellByCondition.Men, el, 1, unit);   // ksiega skupu sprzetu (tylko log)
                    }
                    if (cnt > 0) arm[it] = cnt; else arm.Remove(it);
                    if (extra <= 0) break;
                }
            }
            if (sold > 0) { _daySold += sold; _dayGold += gold; }
        }

        // ------------------------------------------------------------ gracz kupuje od ludzi (ekran zbrojowni)
        private static readonly List<KeyValuePair<EquipmentElement, int>> _pending = new List<KeyValuePair<EquipmentElement, int>>();

        internal static void NoteBuy(EquipmentElement el, int n)
        {
            if (!On || el.Item == null || n <= 0) return;
            _pending.Add(new KeyValuePair<EquipmentElement, int>(el, n));
        }

        /// <summary>Gracz odklada z powrotem to, co w tej sesji wzial od ludzi: kasujemy zakup (zwraca ile).</summary>
        internal static int CancelBuy(ItemObject item, int n)
        {
            int cut = 0;
            for (int i = _pending.Count - 1; i >= 0 && n > 0; i--)
            {
                var kv = _pending[i];
                if (kv.Key.Item != item) continue;
                int c = Math.Min(kv.Value, n); n -= c; cut += c;
                if (kv.Value - c <= 0) _pending.RemoveAt(i); else _pending[i] = new KeyValuePair<EquipmentElement, int>(kv.Key, kv.Value - c);
            }
            return cut;
        }

        internal static void ClearBuys() { _pending.Clear(); }

        /// <summary>Zamkniecie ekranu zbrojowni: placisz ludziom za wziete nadwyzki; czego nie stac - wraca do zbrojowni.</summary>
        internal static void SettleBuys()
        {
            if (_pending.Count == 0) return;
            try
            {
                var main = MobileParty.MainParty;
                var armory = QuartermasterLaw.DteArmory();
                var st = Settlement.CurrentSettlement;
                int paid = 0, pieces = 0, back = 0;
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
                    }
                }
                _pending.Clear();
                if (pieces > 0) Log.Player("You paid your men " + paid + " denars for " + pieces + " pieces from their share of the spoils.");
                if (back > 0) Log.Player(back + " pieces went back to the stores - your purse would not cover them.", true);
                Log.Info("Sakiewka ludzi: gracz kupil od ludzi " + pieces + " szt. za " + paid + ", zwrocono " + back + ".");
            }
            catch (Exception e) { Log.Error("MenPurse.SettleBuys", e); _pending.Clear(); }
        }
    }
}
