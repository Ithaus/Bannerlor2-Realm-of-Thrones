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
using TaleWorlds.CampaignSystem.Settlements.Workshops;
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
    ///
    /// 554 (15.11, Jeff "warsztaty to napraw, czemu nie jezdza karawany") - DOPLATA ZLOTNIKA. Dowod (log 2026-11-15_15-14-12, doby 109108-117): karawan
    /// skierowanych 0 przez 10 dob, sprzedanych 16 szt. (Gulltown 12, King's Landing 2, Frostfang's Camp 2), odrzucone 'bez zysku po koszcie' 9 przy wyborze
    /// i 11 przy wjezdzie; 15 zlotnikow swiata 0 cykli (311c), brak 14 miast 608 szt., juki karawan 500 szt. Przyczyna: cena srebra jest wyrownana w calym
    /// rynku (360d rozklada zapas poza polkami na wszystkie miasta wedlug popytu; indeks cwiartek miast 1.71-2.07, 287), a popyt gry to tylko mieszczanie -
    /// brak zlotnika nie podnosi ceny. Pierwsze sztuki ida po mycie powyzej kosztu, kolejne ponizej (cena spada z kazda sztuka), MaxAtCost dawal < MinLot.
    /// Zmiana (klucz CaravanSilverSmithPays): sztuki, ktore po cenie rynku i mycie nie daja karawanie kosztu x (1 + EarnK), dostaja doplate od zlotnikow tego
    /// miasta (kapital warsztatow ze srebrem na wsadzie, ponad polowe kapitalu poczatkowego) - najwyzej PremMaxK x wartosci sztuki. Kasa miasta placi cene
    /// rynku jak dotad (i tylko ponad zapas kupcow), zlotnik pozniej kupuje srebro z polki po cenie rynku (BK ConsumeInputFromTownMarket) - doplata to jego
    /// zamowienie dowozu. Zloto: kapital warsztatu -> kiesa karawany, nic z niczego. Klucz NIE - jak 492 (sprzedaz tylko po koszcie).
    /// </summary>
    internal static class CaravanSilver492
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int MinLot = 10;              // ponizej tego nie warto zmieniac trasy
        private const int TownDayCap = 2;           // skierowan na miasto na dobe
        private const int RouteTowns = 10;          // kandydaci (najblizsze miasta ze zlotnikiem i brakiem)
        private const double MaxDaysK = 2.0;        // zasieg = max(dni do wyboru BK, 2 x odcinek)
        private const double NoLegDays = 6.0;       // zasieg bez pomiaru odcinka
        private const double EarnK = 0.10;          // 554: z doplata karawana dostaje za sztuke co najmniej koszt x 1.10 (po mycie)
        private const double PremMaxK = 1.0;        // 554: doplata zlotnika najwyzej 1.0 x wartosci sztuki srebra
        private const int PlanMax = 200;            // 554: najwiecej sztuk w jednej ocenie

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
        private static int _pSales, _pPcs, _pShort, _rPurse; private static long _pGold;   // 554: doplata zlotnikow
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
            _sGot = _sToll = 0; _pSales = _pPcs = _pShort = _rPurse = 0; _pGold = 0; _soldTowns.Clear(); _ticks = 0; _stumbles = 0;
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

        // ------------------------------------------------------------ 554: doplata zlotnika
        private static bool Pays(Settings s) { return s != null && s.CaravanSilverSmithPays; }

        /// <summary>Warsztat ze srebrem na wsadzie (ta sama miara co GoodsPaths.SilverBatchOf), z wlascicielem.</summary>
        private static bool UsesSilver(Workshop w)
        {
            var wt = w != null ? w.WorkshopType : null;
            if (wt == null || wt.Productions == null || w.Owner == null) return false;
            foreach (var p in wt.Productions)
            {
                if (p.Inputs == null) continue;   // Production to struct
                foreach (var i in p.Inputs) if (i.Item1 == _cat && i.Item2 > 0) return true;
            }
            return false;
        }

        /// <summary>Kapital warsztatu, ktory zlotnik moze dac na doplate: ponad polowe kapitalu poczatkowego (reszta zostaje na wsad i place).</summary>
        private static int SpareOf(Workshop w) { return Math.Max(0, w.Capital - Math.Max(0, w.InitialCapital) / 2); }

        /// <summary>Razem kapital zlotnikow miasta na doplate.</summary>
        private static long SmithPurse(Town t)
        {
            long n = 0;
            var ws = t != null ? t.Workshops : null;
            if (ws == null) return 0;
            foreach (var w in ws)
            {
                try { if (UsesSilver(w)) n += SpareOf(w); }
                catch (Exception e) { Stumble("SmithPurse", e); }
            }
            return n;
        }

        /// <summary>Zdejmuje do `due` zlota z kapitalu zlotnikow miasta (po kolei, kazdy do swojej nadwyzki); zwraca zdjete.</summary>
        private static int TakeFromSmiths(Town t, int due)
        {
            int got = 0;
            var ws = t != null ? t.Workshops : null;
            if (ws == null || due <= 0) return 0;
            foreach (var w in ws)
            {
                if (got >= due) break;
                try
                {
                    if (!UsesSilver(w)) continue;
                    int x = Math.Min(due - got, SpareOf(w));
                    if (x <= 0) continue;
                    w.ChangeGold(-x);
                    WorkshopTrade.NoteOutsideIncome(w, -x);   // 554 (recenzja 2): koszt zlotnika poza jego tickiem - do wyniku doby Op, nie "wyplata zysku wlascicielowi"
                    got += x;
                }
                catch (Exception e) { Stumble("TakeFromSmiths", e); }
            }
            return got;
        }

        /// <summary>554: karawana nie przyjela doplaty (wyjatek) - zloto wraca do pierwszego zlotnika miasta.</summary>
        private static void TakeBack(Town t, int x)
        {
            try
            {
                var ws = t != null ? t.Workshops : null;
                if (ws == null) return;
                foreach (var w in ws) if (UsesSilver(w)) { w.ChangeGold(x); WorkshopTrade.NoteOutsideIncome(w, x); return; }   // 554 (recenzja 2): zwrot tym samym torem
            }
            catch (Exception e) { Stumble("TakeBack", e); }
        }

        /// <summary>
        /// Ile z n sztuk karawana sprzeda w town: kazda sztuka po mycie co najmniej `floor` (prog sprzedazy), a gdy zlotnik doplaca (pay) - sztuka ponizej
        /// floor x (1 + EarnK) dostaje doplate do tej kwoty, najwyzej PremMaxK x wartosci, razem najwyzej `purse`. Cena k-tej sztuki maleje z k (CaravanGoods.NetAt) -
        /// pierwsza sztuka, ktorej nie da sie sprzedac, konczy liczenie. Bez doplaty - MaxAtCost jak w 492. prem - doplata razem (zl).
        /// </summary>
        private static int Plan(Town t, Settlement st, MobileParty mp, EquipmentElement el, int n, double floor, long purse, bool pay, out long prem)
        {
            prem = 0;
            if (n <= 0 || !(floor > 0) || el.Item == null) return 0;
            if (!pay) return CaravanGoods.MaxAtCost(t, st, mp, el, n, floor);
            double target = floor * (1.0 + EarnK);
            double cap = PremMaxK * Math.Max(1, el.Item.Value);
            int k = 0;
            n = Math.Min(n, PlanMax);
            for (int j = 1; j <= n; j++)
            {
                double net = CaravanGoods.NetAt(t, st, mp, el, j);
                if (double.IsNaN(net)) { prem = 0; return CaravanGoods.MaxAtCost(t, st, mp, el, n, floor); }
                double p = net >= target ? 0.0 : target - net;
                long pc = (long)Math.Ceiling(p);
                if (pc > 0 && (p > cap || prem + pc > purse))
                {
                    if (net >= floor) pc = 0;                               // bez doplaty sztuka i tak nie idzie ponizej progu
                    else break;
                }
                prem += pc; k = j;
            }
            return k;
        }

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
                    bool pay = Pays(s);
                    long purse = pay ? SmithPurse(t) : 0;
                    long prem;
                    int k = Plan(t, t.Settlement, mp, el, want, floor, purse, pay, out prem);   // 554: z doplata zlotnika
                    if (k < MinLot) { if (pay && purse <= 0) _rPurse++; else _rCost++; continue; }
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
                bool pay = Pays(s);
                long prem;
                // nigdy ponizej progu sprzedazy (koszt z ksiegi / istniejace progi strat); 554: sztuki ponizej kosztu x (1 + EarnK) z doplata zlotnika
                int k = Plan(t, st, mp, el.EquipmentElement, n, floor, pay ? SmithPurse(t) : 0, pay, out prem);
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
                if (moved > 0 && pay && prem > 0)
                {
                    // 554: doplata z rzeczywistego utargu po mycie (got - toll) do floor x (1 + EarnK) za sprzedane sztuki, najwyzej plan
                    double due = Math.Ceiling(floor * (1.0 + EarnK) * moved - (got - toll));
                    int dueI = (int)Math.Max(0.0, Math.Min((double)prem, due));
                    if (dueI > 0)
                    {
                        int paid = TakeFromSmiths(t, dueI);
                        if (paid > 0)
                        {
                            try { mp.PartyTradeGold += paid; }
                            catch (Exception e) { Stumble("Sell(doplata)", e); TakeBack(t, paid); paid = 0; }
                            if (paid > 0) CaravanSettle.Flow586(mp, CaravanSettle.LedBonus, paid);   // 586: dziennik kiesy karawany gracza
                        }
                        if (paid > 0) { _pSales++; _pPcs += moved; _pGold += paid; CaravanTrade284.NoteBonus(mp, paid); }   // 554 (recenzja 1): doplata do ksiegi zysku karawany
                        if (paid < dueI) _pShort++;
                    }
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
                sb.Append(" | doplata zlotnikow (554) ").Append(s != null && s.CaravanSilverSmithPays ? "TAK" : "NIE").Append(": ").Append(_pGold).Append(" zl w ").Append(_pSales)
                  .Append(" sprzedazach (").Append(_pPcs).Append(" szt.), za malo kapitalu ").Append(_pShort).Append(", kandydat bez kapitalu zlotnika ").Append(_rPurse);
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
