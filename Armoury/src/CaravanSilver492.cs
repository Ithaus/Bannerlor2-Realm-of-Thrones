using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 492 (14.11, Jeff "wybieram A") - KARAWANY WIOZA SREBRO DO ZLOTNIKOW. Dowod (log 2026-11-14_00-24-57, doba 108711-12): srebro w jukach karawan 864-940 szt.
    /// (54-62% swiata), 77 z 97 miast bez srebra na polce, warsztat 'Silversmith' Jeffa w Lannisport 21 dob bez cyklu (cykl = 25 szt. naraz). Przyczyna: wybor celu
    /// BK i 139 C nie zna zapasu warsztatow (GoodsPaths.CraftReserve), a wyjatek 285 (CaravanGlutToShortTown) liczy brak ze ZMIERZONEGO zuzycia - zlotnik, ktory
    /// nie zrobil cyklu, ma zuzycie 0 i brak 0.
    ///  (1) CEL (Redirect, z CaravanRoute294c.ThinkPostfix - ostatnie slowo po 139 C i 308, przed wpisem Inbound): karawana (nie wyjeta, bez statkow) z co najmniej
    ///      MinLot szt. srebra w jukach bierze sposrod najblizszych miast (te same wykluczenia co BK - CaravanLeg140.Towns) w zasiegu max(dni do wyboru BK,
    ///      2 x zmierzony odcinek) miasto ze zlotnikiem, ktoremu brakuje srebra (brak = max(CraftReserve, wsad jednego cyklu) - polka - w drodze 174/wozy
    ///      - skierowane juz karawany) - gdy sprzeda tam co najmniej MinLot szt. po cenie po mycie nie nizszej niz prog sprzedazy (CaravanGoods.SaleFloor: koszt
    ///      z ksiegi, przy wyjsciach na stracie istniejace progi), kasa miasta ponad zapas kupcow to oplaci, trasa ma zysk na dobe > 0 i nie mniej niz polowa
    ///      zysku na dobe wyboru BK. Najwyzej TownDayCap skierowan na miasto na dobe.
    ///  (2) SPRZEDAZ (OnEntered, przy wjezdzie - po dostawie 174, przed CaravanBulk, T1 i BK): kazda karawana ze srebrem w miescie z brakiem sprzedaje do braku,
    ///      po cenie rynku miasta (SellItemsAction gry - kasa miasta ponad zapas kupcow TownPurse.BuyFloor, myto TownToll, ksiega kosztu E3-T1), nigdy ponizej progu
    ///      SaleFloor. Zloto za towar, nic z niczego. TA karawana nie odkupi tego srebra w tej wizycie (CaravanGoods.BkValuePostfix - SoldHere).
    /// Klucz CaravanSilverToSmiths; czynne tylko przy SilverOreToSmiths (GoodsPaths.CraftFirst - mieszczanie nie biora zapasu warsztatow) i przy czynnym wyborze
    /// celu 294c (RouteOn). Stan tylko w sesji. Linia doby 'Srebro do zlotnikow (492)'. Wyjatek - licznik potkniec, bez wylacznika.
    /// </summary>
    internal static class CaravanSilver492
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int MinLot = 10;              // ponizej tego nie warto zmieniac trasy
        private const int TownDayCap = 2;           // skierowan na miasto na dobe
        private const int RouteTowns = 10;          // kandydaci (najblizsze miasta ze zlotnikiem i brakiem)
        private const double MaxDaysK = 2.0;        // zasieg = max(dni do wyboru BK, 2 x odcinek)
        private const double NoLegDays = 6.0;       // zasieg bez pomiaru odcinka

        private static ItemCategory _cat;
        private static ItemObject _item;
        private static bool _resolved;

        // plan: karawana -> (miasto, sztuki) - zdejmowany przy nowej decyzji, wjezdzie do tego miasta i w Daily (zmiana celu, karawana zniknela)
        private static readonly Dictionary<MobileParty, KeyValuePair<Town, int>> _plan = new Dictionary<MobileParty, KeyValuePair<Town, int>>();
        private static readonly Dictionary<Town, int> _dirToday = new Dictionary<Town, int>();
        private static readonly Dictionary<MobileParty, Settlement> _soldHere = new Dictionary<MobileParty, Settlement>();
        private static readonly HashSet<Town> _smiths = new HashSet<Town>();
        private static int _smithDay = int.MinValue;

        // liczniki doby
        private static int _dec, _dir, _already, _noCand, _rCost, _rGold, _rCap, _rRoute, _sCaravans, _sPcs, _sCost, _sGold, _sExempt;
        private static long _sGot, _sToll;
        private static readonly Dictionary<string, int> _soldTowns = new Dictionary<string, int>();
        private static long _ticks;
        private static int _stumbles, _stumbleSess;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Reset()
        {
            _plan.Clear(); _dirToday.Clear(); _soldHere.Clear(); _smiths.Clear(); _smithDay = int.MinValue;
            _cat = null; _item = null; _resolved = false;
            _stumbleSess = 0; _err.Clear();
            ZeroDay();
        }

        private static void ZeroDay()
        {
            _dec = _dir = _already = _noCand = _rCost = _rGold = _rCap = _rRoute = _sCaravans = _sPcs = _sCost = _sGold = _sExempt = 0;
            _sGot = _sToll = 0; _soldTowns.Clear(); _ticks = 0; _stumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("CaravanSilver492." + where, e); } catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        private static bool Resolve()
        {
            if (_resolved) return _cat != null && _item != null;
            var om = MBObjectManager.Instance;
            if (om == null) return false;
            _cat = om.GetObject<ItemCategory>("silver");
            _item = om.GetObject<ItemObject>("silver");
            _resolved = true;
            if (_cat == null || _item == null) Log.Info("CaravanSilver492 (492): BRAK kategorii albo przedmiotu 'silver' - regula nieczynna.");
            return _cat != null && _item != null;
        }

        private static bool On(Settings s) { return s != null && s.CaravanSilverToSmiths && Resolve() && GoodsPaths.CraftFirst(_cat); }

        // ------------------------------------------------------------ brak miasta
        /// <summary>Miasta z zapasem warsztatow srebra (CraftReserve > 0) - pamiec doby.</summary>
        private static void EnsureSmiths()
        {
            int d = Today();
            if (d == _smithDay) return;
            _smithDay = d; _smiths.Clear();
            foreach (var t in Town.AllTowns)
            {
                try { if (t != null && t.IsTown && t.Settlement != null && GoodsPaths.CraftReserve(t, _cat) > 0f) _smiths.Add(t); }
                catch (Exception e) { Stumble("EnsureSmiths", e); }
            }
        }

        private static int ShelfOf(Town t)
        {
            var r = t.Owner != null ? t.Owner.ItemRoster : null;
            if (r == null) return 0;
            int n = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount > 0 && el.EquipmentElement.Item != null && el.EquipmentElement.Item.ItemCategory == _cat) n += el.Amount;
            }
            return n;
        }

        /// <summary>Zapas, ktory miasto chce miec: max(CraftReserve, wsad jednego cyklu) - cykl rusza tylko z pelnym wsadem. 0 - poza regula.</summary>
        private static double TargetOf(Town t)
        {
            float res = GoodsPaths.CraftReserve(t, _cat);
            if (!(res > 0f)) return 0.0;
            return Math.Ceiling(Math.Max(res, GoodsPaths.SilverBatchOf(t)));
        }

        /// <summary>Brak miasta: zapas - polka - w drodze (kontrakty 174, wozy kupcow).</summary>
        private static int LackOf(Town t)
        {
            double target = TargetOf(t);
            if (!(target > 0)) return 0;
            return (int)target - ShelfOf(t) - MaterialOrders.InTransitTo(t.Settlement, _item);
        }

        /// <summary>Sztuki zaplanowane przez inne karawany, ktore nadal jada do tego miasta.</summary>
        private static int PlanTo(Town t)
        {
            int n = 0;
            foreach (var kv in _plan)
            {
                var mp = kv.Key;
                if (kv.Value.Key != t || mp == null || !mp.IsActive || mp.TargetSettlement != t.Settlement) continue;
                n += kv.Value.Value;
            }
            return n;
        }

        private static bool ShortForRoute(Town t)
        {
            try { return _smiths.Contains(t) && LackOf(t) - PlanTo(t) >= MinLot; }
            catch (Exception e) { Stumble("ShortForRoute", e); return false; }
        }

        private static int SilverIn(ItemRoster r, out ItemObject top)
        {
            top = null;
            if (r == null || _cat == null) return 0;
            int n = 0, big = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || it == null || it.ItemCategory != _cat || !it.IsTradeGood) continue;
                n += el.Amount;
                if (el.Amount > big) { big = el.Amount; top = it; }
            }
            return n;
        }

        // ------------------------------------------------------------ (1) wybor celu
        /// <summary>Z CaravanRoute294c.ThinkPostfix (karawana nie wyjeta, decyzja BK z wynikiem): miasto ze zlotnikiem i brakiem srebra zamiast wyboru BK / 139 C / 308.</summary>
        internal static void Redirect(object bk, MobileParty mp, ref Town result)
        {
            if (mp == null) return;
            if (_plan.Count > 0) _plan.Remove(mp);                            // nowa decyzja kasuje stary plan
            long t0 = Stopwatch.GetTimestamp();
            bool eval = false;
            try
            {
                var s = Settings.Current;
                if (!On(s) || result == null || result.Settlement == null || mp.HasNavalNavigationCapability) return;
                ItemObject it;
                int have = SilverIn(mp.ItemRoster, out it);
                if (have < MinLot || it == null) return;
                EnsureSmiths();
                if (_smiths.Count == 0) return;
                _dec++;
                double db = CaravanWait139.Days(mp, result.Settlement);
                double leg = CaravanWait139.Leg;
                double dmax = Math.Max(db, leg > 0 ? MaxDaysK * leg : NoLegDays);
                var cand = CaravanLeg140.Towns(mp, bk, null, dmax, RouteTowns, ShortForRoute);
                if (cand.Count == 0) { _noCand++; return; }
                eval = true;
                var pantry = CaravanTrade284.PantryCopy(mp);
                int reserve = (int)Math.Max(0f, s.TownRentFloorGold);
                var el = new EquipmentElement(it);
                int itHave = mp.ItemRoster.GetItemNumber(it);
                double pdB = double.NaN;
                Town best = null; int bestK = 0; double bestScore = 0;
                foreach (var kv in cand)
                {
                    var t = kv.Key; double d = kv.Value;
                    int dir; _dirToday.TryGetValue(t, out dir);
                    if (dir >= TownDayCap && t != result) { _rCap++; continue; }
                    int want = Math.Min(itHave, LackOf(t) - PlanTo(t));
                    if (want < MinLot) { _rCost++; continue; }
                    double floor = CaravanGoods.SaleFloor(mp, it, t.Settlement);
                    if (!(floor > 0)) { _rCost++; continue; }
                    int k = CaravanGoods.MaxAtCost(t, t.Settlement, mp, el, want, floor);
                    if (k < MinLot) { _rCost++; continue; }
                    int price = Math.Max(1, t.GetItemPrice(el, mp, true));
                    int kg = (t.Gold - TownPurse.BuyFloorPeek(t, reserve)) / price;
                    if (kg < MinLot) { _rGold++; continue; }
                    k = Math.Min(k, kg);
                    double pd = CaravanWait139.PerDayNet(mp, t, d, pantry);
                    if (!(pd > 0)) { _rRoute++; continue; }
                    if (t != result)
                    {
                        if (double.IsNaN(pdB)) pdB = CaravanWait139.PerDayNet(mp, result, db, pantry);
                        if (pdB > 0 && pd < 0.5 * pdB) { _rRoute++; continue; }
                    }
                    double score = k / Math.Max(1.0, d);
                    if (score > bestScore) { bestScore = score; best = t; bestK = k; }
                }
                if (best == null) return;
                if (best == result) _already++;
                else
                {
                    int dir; _dirToday.TryGetValue(best, out dir); _dirToday[best] = dir + 1;
                    _dir++;
                    result = best;
                }
                _plan[mp] = new KeyValuePair<Town, int>(best, bestK);
            }
            catch (Exception e) { Stumble("Redirect", e); }
            finally
            {
                if (eval) CaravanWait139.ClearDecision();
                _ticks += Stopwatch.GetTimestamp() - t0;
            }
        }

        // ------------------------------------------------------------ (2) sprzedaz przy wjezdzie
        internal static void OnEntered(MobileParty mp, Settlement st, Hero hero)
        {
            if (mp == null || st == null || !mp.IsCaravan || !st.IsTown) return;   // tanie wyjscie: zdarzenie pada dla kazdej partii i bohatera
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (_soldHere.Count > 0) _soldHere.Remove(mp);              // nowa wizyta
                KeyValuePair<Town, int> p;
                if (_plan.Count > 0 && _plan.TryGetValue(mp, out p) && p.Key == st.Town) _plan.Remove(mp);
                var s = Settings.Current;
                var t = st.Town;
                if (t == null || !On(s)) return;
                ItemObject top;
                if (SilverIn(mp.ItemRoster, out top) <= 0) return;
                EnsureSmiths();
                if (!_smiths.Contains(t)) return;
                if (CaravanRoute294c.ExemptOf(mp)) { _sExempt++; return; }
                Sell(mp, st, t, s);
            }
            catch (Exception e) { Stumble("OnEntered", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        private static void Sell(MobileParty mp, Settlement st, Town t, Settings s)
        {
            int lack = LackOf(t);
            if (lack <= 0) return;
            var pack = mp.ItemRoster;
            var shelf = t.Owner.ItemRoster;
            int reserve = (int)Math.Max(0f, s.TownRentFloorGold);
            List<(EquipmentElement, int)> done = null;
            // stosy srebra w jukach (zwykle jeden przedmiot) - kopia listy, bo sprzedaz zmienia juki
            var els = new List<ItemRosterElement>();
            for (int i = 0; i < pack.Count; i++)
            {
                var e0 = pack.GetElementCopyAtIndex(i);
                var it0 = e0.EquipmentElement.Item;
                if (e0.Amount > 0 && it0 != null && it0.ItemCategory == _cat && it0.IsTradeGood) els.Add(e0);
            }
            foreach (var el in els)
            {
                if (lack <= 0) break;
                var it = el.EquipmentElement.Item;
                int had = pack.GetItemNumber(it);
                if (had <= 0) continue;
                int spare = t.Gold - TownPurse.BuyFloor(t, reserve);    // E5/A1: miasto tylko z nadwyzki ponad zapas kupcow
                int price = Math.Max(1, t.GetItemPrice(el.EquipmentElement, mp, true));
                int n = spare > 0 ? Math.Min(Math.Min(lack, Math.Min(el.Amount, had)), spare / price) : 0;   // cena spada z kazda sztuka - pierwsza jest gorna granica
                if (n <= 0) { _sGold++; break; }
                double floor = CaravanGoods.SaleFloor(mp, it, st);
                if (!(floor > 0)) { _sCost++; continue; }
                int k = CaravanGoods.MaxAtCost(t, st, mp, el.EquipmentElement, n, floor);   // nigdy ponizej progu sprzedazy (koszt z ksiegi / istniejace progi strat)
                if (k <= 0) { _sCost++; continue; }
                int purse = mp.PartyTradeGold;
                bool broke = false;
                TownToll.LastTaken = 0;
                CaravanTrade284.Path = CaravanTrade284.PathBulkShort;     // 284: droga 'surowce masowe - brak miasta' w ksiedze zysku (sprzedaz do braku miasta)
                try { SellItemsAction.Apply(mp.Party, t.Owner, el, k, st); }
                catch (Exception e) { broke = true; Stumble("Sell(akcja gry)", e); }
                finally { CaravanTrade284.Path = 0; }
                int moved = had - pack.GetItemNumber(it);
                int toll = TownToll.LastTaken;
                int got = Math.Max(0, mp.PartyTradeGold - purse) + toll;  // utarg przed mytem (kasa miasta zaplacila)
                TownToll.LastTaken = 0;
                if (broke && moved > 0 && got == 0)
                {
                    try { shelf.AddToCounts(el.EquipmentElement, -moved); pack.AddToCounts(el.EquipmentElement, moved); }   // towar poszedl, zloto nie - cofamy
                    catch (Exception e) { Stumble("Sell(cofniecie)", e); }
                    moved = 0;
                }
                if (moved > 0)
                {
                    lack -= moved;
                    _sPcs += moved; _sGot += got; _sToll += toll;
                    string nm = st.Name != null ? st.Name.ToString() : st.StringId;
                    int was; _soldTowns.TryGetValue(nm, out was); _soldTowns[nm] = was + moved;
                    _soldHere[mp] = st;
                    if (done == null) done = new List<(EquipmentElement, int)>();
                    done.Add((el.EquipmentElement, moved));
                }
                if (broke) break;
            }
            if (done == null) return;
            _sCaravans++;
            try { CampaignEventDispatcher.Instance.OnCaravanTransactionCompleted(mp, t, done); } catch { }   // jak CaravanBulk: dymek nad miastem
        }

        /// <summary>MaterialOrders.SourceKeep: zapas zlotnikow miasta (max(CraftReserve, wsad cyklu)) - ponizej niego miasto nie oddaje srebra na kontrakty i wozy kupcow
        /// (zmierzone zuzycie zlotnika bez cyklu = 0, wiec dowiezione srebro wyjechaloby, zanim warsztat ruszy). Poza regula, inny towar, blad - 0.</summary>
        internal static int SmithKeep(Town t, ItemObject it)
        {
            try
            {
                if (t == null || it == null || !On(Settings.Current) || it.ItemCategory != _cat) return 0;
                return (int)TargetOf(t);
            }
            catch (Exception e) { Stumble("SmithKeep", e); return 0; }
        }

        /// <summary>CaravanGoods.BkValuePostfix: karawana sprzedala srebro zlotnikom w tym miescie w tej wizycie - nie odkupuje kategorii srebra. Nie rzuca.</summary>
        internal static bool SoldHere(MobileParty mp, Town town, ItemCategory cat)
        {
            try
            {
                Settlement st;
                return mp != null && town != null && cat != null && _soldHere.Count > 0 && cat == _cat && _soldHere.TryGetValue(mp, out st) && st == town.Settlement;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------ linia doby
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            var s = Settings.Current;
            int smiths = 0, shortT = 0; long lackSum = 0;
            bool on = false;
            try
            {
                on = On(s);
                // plany: karawana zniknela albo jedzie gdzie indziej (wojna, oblezenie, rozbita, nowy cel BK)
                if (_plan.Count > 0)
                {
                    List<MobileParty> gone = null;
                    foreach (var kv in _plan)
                    {
                        var mp = kv.Key;
                        if (mp == null || !mp.IsActive || kv.Value.Key == null || mp.TargetSettlement != kv.Value.Key.Settlement)
                        { if (gone == null) gone = new List<MobileParty>(); gone.Add(mp); }
                    }
                    if (gone != null) foreach (var mp in gone) _plan.Remove(mp);
                }
                if (_soldHere.Count > 0)
                {
                    var gone2 = _soldHere.Where(kv => kv.Key == null || !kv.Key.IsActive || kv.Key.CurrentSettlement != kv.Value).Select(kv => kv.Key).ToList();
                    foreach (var mp in gone2) _soldHere.Remove(mp);
                }
                if (on)
                {
                    _smithDay = int.MinValue;
                    EnsureSmiths();
                    foreach (var t in _smiths)
                    {
                        smiths++;
                        int l = LackOf(t);
                        if (l > 0) { shortT++; lackSum += l; }
                    }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            try
            {
                var sb = new StringBuilder();
                sb.Append("Srebro do zlotnikow (492): dzien ").Append(Today()).Append(" | klucz ").Append(s != null && s.CaravanSilverToSmiths ? "TAK" : "NIE");
                if (s != null && s.CaravanSilverToSmiths && !on) sb.Append(" (nieczynne: SilverOreToSmiths NIE albo brak 'silver')");
                sb.Append(" | miast ze zlotnikiem ").Append(smiths).Append(", z brakiem ").Append(shortT).Append(" (brak razem ").Append(lackSum).Append(" szt.)");
                sb.Append(" | decyzji karawan ze srebrem ").Append(_dec).Append(", karawan skierowanych ").Append(_dir).Append(" (juz jechalo ").Append(_already).Append(")");
                sb.Append(", w planie ").Append(_plan.Count);
                sb.Append(" | sprzedanych szt. ").Append(_sPcs).Append(" za ").Append(_sGot).Append(" zl (kasy miast; myto ").Append(_sToll).Append("), karawan ").Append(_sCaravans).Append(" w miastach [");
                bool first = true;
                foreach (var kv in _soldTowns.OrderByDescending(x => x.Value))
                {
                    if (!first) sb.Append(", ");
                    sb.Append(kv.Key).Append(' ').Append(kv.Value); first = false;
                }
                sb.Append("]");
                sb.Append(" | odrzucone kandydaty: bez zysku po koszcie ").Append(_rCost).Append(", kasa miasta ").Append(_rGold).Append(", limit miasta ").Append(_rCap)
                  .Append(", trasa bez zysku ").Append(_rRoute).Append(" | brak kandydata (za daleko/wojna) ").Append(_noCand);
                sb.Append(" | przy wjezdzie: ponizej progu kosztu ").Append(_sCost).Append(", kasa miasta ").Append(_sGold).Append(", wyjete ").Append(_sExempt);
                sb.Append(" | czas ").Append((_ticks * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv)).Append(" ms | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(")");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily(log)", e); }
            finally
            {
                ZeroDay();
                _dirToday.Clear();
                _ticks += Stopwatch.GetTimestamp() - t0;
            }
        }
    }
}
