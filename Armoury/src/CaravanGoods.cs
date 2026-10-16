using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
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
    /// E3-T1 (noc-ekonomia3 16.10, czesc 1; klucz CaravanSellWhereDear) - KARAWANA SPRZEDAJE TAM, GDZIE MA ZYSK.
    /// POPRAWKA JEFFA 16.10 (wiazaca, zastepuje regule planu "sprzedaz tam, gdzie indeks >= srednia indeksow"): "Karawany sprzedaja tam, gdzie maja zysk, a nie
    /// powyzej sredniej, bo nie sprzedaja na stracie, chyba ze juz nie maja wyjscia".
    /// Dowod [L] (test noc-ekonomia2 21-25-46 d109195, linia "Karawany - zapas wedlug towaru (nekon)"): narzedzia - karawany 2 371 z 2 671 sztuk swiata (88.8%),
    /// miasta 194, karawany kupily 18 i sprzedaly 19 na dobe; srebro w jukach 4 736 (kupily 303, sprzedaly 32 - stos rosnie), wino 819 (81.6% swiata).
    /// Przyczyna [K]: BK SellGoods (BKCaravansBehavior.cs:1592-1611) sprzedaje na wizyte tyle, ile wynosi wynik kategorii / cene (1-2 sztuki), i tylko przy
    /// indeksie >= (1.1 - do 0.4) x srednia; reszta jedzie dalej.
    /// Regula (przy wjezdzie do MIASTA, po surowcach masowych CaravanBulk, przed amunicja CaravanAmmo i przed BK - ArmouryBehavior):
    ///  - towar handlowy (IsTradeGood) spoza tabeli CaravanBulk, bez zwierzat i koni; karawana z kontraktem 174 albo z rozkazem gracza (BK) - pominieta;
    ///  - sztuka schodzi, gdy cena ostatniej sztuki kroku w tym miescie > koszt zakupu sztuki + jej czesc zoldu karawany za dni w drodze (zold dnia x dni od
    ///    zakupu (najwyzej 30) x koszt sztuki / koszt calego ladunku; najwyzej 100% kosztu sztuki). Koszt zakupu = srednia wazona partii w jukach - ksiega
    ///    sesji z latki SellItemsAction.ApplyInternal (kazdy zakup karawany od osady: BK, CaravanBulk, kontrakty, gabka); bez klucza zapisu: po wczytaniu
    ///    sztuki bez wpisu dostaja koszt = cena bazowa przedmiotu (Value) i doba wczytania;
    ///  - NA STRACIE tylko bez wyjscia: towar lezy w jukach > 30 dob, jedzenie > 7 dob (psuje sie), kiesa < 3 dni zoldu, juki >= 95% udzwigu, karawana
    ///    rozwiazywana (IsDisbanding) - powod w linii doby;
    ///  - BEZ progu "powyzej sredniej" (poprawka Jeffa). Zeby nie bylo petli sprzedaz-odkupienie: w tej wizycie BK nie wycenia zakupu kategorii, ktora T1 tu
    ///    sprzedal (postfiks BKCaravansBehavior.CalculateBuyValue przy karawanie z BuyGoods - jak CaravanBulk dla surowcow; uwagi recenzji 1 i 2), a cena ostatniej
    ///    sztuki kroku >= 0.4 x wartosci (gabka BK przy wyjezdzie kupuje ponizej 0.33 - BKPartyBehavior :329); licznik "odkupione w tej samej wizycie";
    ///  - kasa miasta tylko ponad TownPurse.BuyFloor i najwyzej 1/20 tej nadwyzki na dobe (uwaga recenzji: zawor 7%/d nadwyzki do panow i koron nie traci
    ///    wiecej niz ok. 5%);
    ///  - krokami po 5 sztuk (sztuka > 200 d - po 1), cena liczona od nowa po kazdym kroku, najwyzej polowa stosu na wizyte, jedzenie zostaje na 10 dob druzyny.
    /// Kto placi, kto dostaje: kasa miasta -> kiesa karawany (SellItemsAction - zwykly handel gry), od utargu myto E3-G3 (TownToll). Potem lud i wsie kupuja
    /// z polki: kiesy -> kasa -> zawor.
    /// Linia "Karawany - towar tam, gdzie maja zysk (E3-T1): dzien N" (z zyskiem / na stracie z powodem, odrzucone, wedlug towaru, ksiega kosztu, juki
    /// i mediana indeksu wybranych towarow). Nic w zapisie. Wyjatek per wizyta - licznik potkniec (sprzedaz zostaje jak w BK).
    /// </summary>
    internal static class CaravanGoods
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int MaxAgeDays = 30, FoodAgeDays = 7, FoodKeepDays = 10, CashDays = 3, StepN = 5, DearValue = 200, TownDayShare = 20;
        private const float FullShare = 0.95f, GabkaFloor = 0.40f;
        private static readonly string[] WatchIds = { "tools", "fur", "silver", "wine", "Papyrus", "velvet", "jewelry", "Eggs", "honey", "salt" };

        // ------------------------------------------------------------ ksiega kosztu (sesja)
        private sealed class Lot { public int N; public double Cost, Day; }
        private static readonly Dictionary<MobileParty, Dictionary<ItemObject, Lot>> _book = new Dictionary<MobileParty, Dictionary<ItemObject, Lot>>();
        private sealed class Visit { public Settlement St; public HashSet<ItemCategory> Cats = new HashSet<ItemCategory>(); }
        private static readonly Dictionary<MobileParty, Visit> _visit = new Dictionary<MobileParty, Visit>();
        private static readonly Dictionary<Town, int> _townLeft = new Dictionary<Town, int>(); private static int _townDay = -1;
        private static MobileParty _bkBuyer;                         // karawana w BK BuyGoods (prefiks/postfiks) - blokada odkupienia w tej wizycie

        public struct TState { public bool On; public int Kind; public MobileParty Mp; public Settlement St; public ItemObject It; public int Gold0, N0; }

        private static bool _hooked, _startDone, _bkHooked;
        private static readonly HashSet<string> _err = new HashSet<string>();

        // ------------------------------------------------------------ liczniki doby
        private sealed class Row { public int ProfitN, LossN; public long ProfitG, LossG, ProfitCost, LossCost; }
        private static readonly Dictionary<ItemObject, Row> _rows = new Dictionary<ItemObject, Row>();
        private static int _entries, _sellers, _rNoProfit, _bkBlocked, _rGabka, _rCash, _rLimit, _rFood, _rHalf, _skipOrders, _skipContract, _stumbles, _stumbleSess;
        private static int _lAge, _lFood, _lCash, _lFull, _lDisband;
        private static long _lAgeG, _lFoodG, _lCashG, _lFullG, _lDisbandG, _gross, _toll, _rebought, _rebuilt, _bookBuys, _bookBuyUnits;
        private static long _townSpare0;

        internal static void Reset()
        {
            _book.Clear(); _visit.Clear(); _townLeft.Clear(); _townDay = -1; _bkBuyer = null;
            _startDone = false; _stumbleSess = 0; _err.Clear();
            ZeroDay();
        }

        private static void ZeroDay()
        {
            _rows.Clear();
            _entries = _sellers = _rNoProfit = _bkBlocked = _rGabka = _rCash = _rLimit = _rFood = _rHalf = _skipOrders = _skipContract = _stumbles = 0;
            _lAge = _lFood = _lCash = _lFull = _lDisband = 0;
            _lAgeG = _lFoodG = _lCashG = _lFullG = _lDisbandG = _gross = _toll = _rebought = _rebuilt = _bookBuys = _bookBuyUnits = 0;
            _townSpare0 = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("CaravanGoods." + where, e); } catch { }
        }

        private static bool On { get { var s = Settings.Current; return s != null && s.CaravanSellWhereDear; } }
        private static double Now { get { return CampaignTime.Now.ToDays; } }

        // ------------------------------------------------------------ ksiega kosztu: zgodnosc z jukami
        /// <summary>Wpis ksiegi zgodny z jukami (`have` sztuk): nadmiar wpisu (towar zszedl bez SellItemsAction - jedzenie, cofniecie gabki) zdejmowany proporcjonalnie,
        /// brak wpisu (stary zapis, towar sprzed sesji) odtwarzany po cenie bazowej i dobie teraz. Zwraca wpis albo null przy have <= 0.</summary>
        private static Lot Sync(MobileParty mp, ItemObject it, int have, bool create)
        {
            Dictionary<ItemObject, Lot> d;
            if (!_book.TryGetValue(mp, out d)) { if (!create && have <= 0) return null; d = new Dictionary<ItemObject, Lot>(); _book[mp] = d; }
            Lot l;
            if (!d.TryGetValue(it, out l)) { if (have <= 0 && !create) return null; l = new Lot { N = 0, Cost = 0, Day = Now }; d[it] = l; }
            if (l.N > have)
            {
                if (have <= 0) { l.N = 0; l.Cost = 0; }
                else { l.Cost = l.Cost * have / l.N; l.N = have; }
            }
            else if (l.N < have)
            {
                int miss = have - l.N;
                double now = Now;
                l.Day = l.N > 0 ? (l.Day * l.N + now * miss) / have : now;
                l.Cost += (double)miss * Math.Max(1, it.Value);
                l.N = have;
                _rebuilt += miss;
            }
            return l;
        }

        // ------------------------------------------------------------ latka SellItemsAction.ApplyInternal (zakup karawany - ksiega; sprzedaz karawany - ksiega i myto)
        public static void TradePrefix(PartyBase __0, PartyBase __1, ItemRosterElement __2, int __3, out TState __state)
        {
            __state = default(TState);
            try
            {
                var c = Campaign.Current;
                if (c == null || !c.GameStarted || __0 == null || __1 == null) return;
                var it = __2.EquipmentElement.Item;
                if (it == null) return;
                MobileParty buyer = __1.MobileParty, seller = __0.MobileParty;
                if (buyer != null && buyer.IsCaravan && __0.IsSettlement && __0.Settlement != null && __0.Settlement.Town != null)
                {
                    __state.On = true; __state.Kind = 1; __state.Mp = buyer; __state.St = __0.Settlement;
                }
                else if (seller != null && seller.IsCaravan && __1.IsSettlement && __1.Settlement != null && __1.Settlement.Town != null)
                {
                    __state.On = true; __state.Kind = 2; __state.Mp = seller; __state.St = __1.Settlement;
                    TownToll.LastTaken = 0;
                }
                else return;
                __state.It = it; __state.Gold0 = __state.Mp.PartyTradeGold; __state.N0 = __state.Mp.ItemRoster.GetItemNumber(it);
            }
            catch (Exception e) { __state.On = false; Stumble("TradePrefix", e); }
        }

        public static void TradePostfix(TState __state)
        {
            if (!__state.On) return;
            try
            {
                var mp = __state.Mp; var it = __state.It;
                int n1 = mp.ItemRoster.GetItemNumber(it);
                if (__state.Kind == 1)
                {
                    int got = n1 - __state.N0;
                    long paid = (long)__state.Gold0 - mp.PartyTradeGold;
                    if (got <= 0 || paid < 0) return;
                    var l = Sync(mp, it, __state.N0, true);
                    double now = Now;
                    l.Day = l.N > 0 ? (l.Day * l.N + now * got) / (l.N + got) : now;
                    l.Cost += paid; l.N += got;
                    _bookBuys++; _bookBuyUnits += got;
                    Visit v;
                    if (_visit.TryGetValue(mp, out v) && ReferenceEquals(v.St, __state.St) && v.Cats.Contains(it.ItemCategory)) _rebought += got;   // odkupione to, co tu sprzedalismy (gabka, CaravanBulk)
                }
                else
                {
                    int moved = __state.N0 - n1;
                    int got = mp.PartyTradeGold - __state.Gold0;
                    if (moved > 0)
                    {
                        var l = Sync(mp, it, __state.N0, false);
                        if (l != null && l.N > 0) { int m = Math.Min(moved, l.N); l.Cost -= l.Cost * m / l.N; l.N -= m; }
                    }
                    if (got > 0) TownToll.LastTaken = TownToll.TakeCaravan(__state.St, mp, got);
                }
            }
            catch (Exception e) { Stumble("TradePostfix", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(SellItemsAction), "ApplyInternal");
                var ps = m != null ? m.GetParameters() : null;
                if (m != null && m.IsStatic && ps.Length >= 4 && ps[0].ParameterType == typeof(PartyBase) && ps[1].ParameterType == typeof(PartyBase)
                    && ps[2].ParameterType == typeof(ItemRosterElement) && ps[3].ParameterType == typeof(int))
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(CaravanGoods), nameof(TradePrefix)), postfix: new HarmonyMethod(typeof(CaravanGoods), nameof(TradePostfix)));
                    _hooked = true;
                }
                TownToll.NoteHooks(false, _hooked);
                // blokada odkupienia: BK BuyGoods(MobileParty, Town) ustawia karawane, CalculateBuyValue(ItemCategory, Town, float, float) -> 0 dla kategorii sprzedanej tu przez T1
                var bkc = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                var goods = bkc != null ? AccessTools.DeclaredMethod(bkc, "BuyGoods", new[] { typeof(MobileParty), typeof(Town) }) : null;
                var value = bkc != null ? AccessTools.DeclaredMethod(bkc, "CalculateBuyValue", new[] { typeof(ItemCategory), typeof(Town), typeof(float), typeof(float) }) : null;
                if (goods != null && value != null && value.ReturnType == typeof(float))
                {
                    h.Patch(goods, prefix: new HarmonyMethod(typeof(CaravanGoods), nameof(BkBuyPrefix)), finalizer: new HarmonyMethod(typeof(CaravanGoods), nameof(BkBuyFinalizer)));
                    h.Patch(value, postfix: new HarmonyMethod(typeof(CaravanGoods), nameof(BkValuePostfix)));
                    _bkHooked = true;
                }
                Log.Info("CaravanGoods (E3-T1, E3-G3): SellItemsAction.ApplyInternal - latka " + (_hooked ? "wpieta" : "NIE wpieta (inna sygnatura)") + " (ksiega kosztu karawan, myto karawan)"
                         + "; blokada odkupienia przez BK (BuyGoods + CalculateBuyValue) - " + (_bkHooked ? "wpieta" : "NIE (brak metod BK)") + ".");
            }
            catch (Exception e) { Log.Error("CaravanGoods.ApplyAll", e); }
        }

        // ------------------------------------------------------------ blokada odkupienia przez BK w tej samej wizycie
        public static void BkBuyPrefix(MobileParty __0) { _bkBuyer = __0; }
        public static Exception BkBuyFinalizer(Exception __exception) { _bkBuyer = null; return __exception; }

        /// <summary>BK CalculateBuyValue w BuyGoods karawany, ktora w tej wizycie sprzedala te kategorie przez T1: wycena 0 (BK jej tu nie odkupi). Nie rzuca.</summary>
        public static void BkValuePostfix(ItemCategory __0, Town __1, ref float __result)
        {
            if (__result <= 0f || _bkBuyer == null || __0 == null || __1 == null || _visit.Count == 0) return;
            try
            {
                Visit v;
                if (!_visit.TryGetValue(_bkBuyer, out v) || !ReferenceEquals(v.St, __1.Settlement) || !v.Cats.Contains(__0)) return;
                __result = 0f;
                _bkBlocked++;
            }
            catch (Exception e) { Stumble("BkValuePostfix", e); }
        }

        // ------------------------------------------------------------ wjazd do miasta

        private sealed class Cand { public ItemObject It; public EquipmentElement El; public int Have; public double Unit, Min; public bool Food; public float Gain; public int Price; }

        /// <summary>Powod sprzedazy na stracie (0 - brak wyjscia nie zachodzi): 1 rozwiazanie, 2 brak na zold, 3 pelne juki, 4 jedzenie psuje sie, 5 dlugo w jukach.</summary>
        private static int LossWhy(bool disband, bool cash, bool full, bool food, double age)
        {
            if (disband) return 1;
            if (cash) return 2;
            if (full) return 3;
            if (food && age > FoodAgeDays) return 4;
            if (age > MaxAgeDays) return 5;
            return 0;
        }

        internal static void OnEntered(MobileParty mp, Settlement st, Hero hero)
        {
            if (mp == null || st == null || !mp.IsCaravan || !st.IsTown) return;   // tanie wyjscie: zdarzenie pada dla kazdej partii i bohatera
            try
            {
                if (_visit.Count > 0) _visit.Remove(mp);
                var s = Settings.Current;
                if (s == null || !s.CaravanSellWhereDear || st.Town == null || !CaravanBulk.TradesForT1(mp)) return;
                if (MaterialOrders.HasContract(mp)) { _skipContract++; return; }
                if (CaravanBulk.HasOrders(mp)) { _skipOrders++; return; }
                Sell(mp, st, st.Town, s);
            }
            catch (Exception e) { Stumble("OnEntered", e); }
        }

        private static void Sell(MobileParty mp, Settlement st, Town town, Settings s)
        {
            _entries++;
            var pack = mp.ItemRoster;
            var model = Campaign.Current.Models.TradeItemPriceFactorModel;
            if (pack == null || model == null) return;
            double now = Now;
            int wage = Math.Max(0, mp.TotalWage), cap = Math.Max(1, mp.InventoryCapacity);
            bool disband = mp.IsDisbanding;
            double foodUse = Math.Max(0.0, -mp.FoodChange);
            int foodKeep = (int)Math.Ceiling(foodUse * FoodKeepDays), foodHave = 0;
            // ksiega: koszt calego ladunku towarow handlowych (podzial zoldu) i kandydaci
            double cargo = 0;
            var cands = new List<Cand>();
            for (int i = 0; i < pack.Count; i++)
            {
                var el = pack.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0) continue;
                if (it.IsFood) foodHave += el.Amount;
                if (!it.IsTradeGood) continue;
                var l = Sync(mp, it, pack.GetItemNumber(it), true);
                if (l == null || l.N <= 0) continue;
                cargo += l.Cost * el.Amount / l.N;
                if (it.HasHorseComponent || it.ItemCategory == null || it.ItemCategory == DefaultItemCategories.PackAnimal || it.NotMerchandise || CaravanBulk.Owns(it)) continue;
                bool dup = false;
                foreach (var c in cands) if (ReferenceEquals(c.It, it)) { dup = true; break; }
                if (dup) continue;
                cands.Add(new Cand { It = it, El = el.EquipmentElement, Have = pack.GetItemNumber(it), Unit = l.Cost / l.N, Food = it.IsFood });
            }
            if (cands.Count == 0) return;
            foreach (var c in cands)
            {
                var l = Sync(mp, c.It, c.Have, false);
                double age = l != null ? Math.Min(MaxAgeDays, Math.Max(0, now - l.Day)) : 0;
                double travel = cargo > 0 ? c.Unit * Math.Min(1.0, wage * age / cargo) : 0;
                c.Min = c.Unit + travel;
                c.Price = Math.Max(1, town.GetItemPrice(c.El, mp, true));
                c.Gain = (float)((c.Price - c.Min) / c.Price);
            }
            cands.Sort((a, b) => b.Gain.CompareTo(a.Gain));             // najpierw najwiekszy zysk na denara kasy miasta
            int reserve = (int)Math.Max(0f, s.TownRentFloorGold);
            int today = (int)now;
            if (today != _townDay) { _townLeft.Clear(); _townDay = today; }
            int left;
            if (!_townLeft.TryGetValue(town, out left))
            {
                int spare0 = town.Gold - TownPurse.BuyFloor(town, reserve);
                left = Math.Max(0, spare0) / TownDayShare;
                _townLeft[town] = left; _townSpare0 += Math.Max(0, spare0);
            }
            bool any = false;
            List<(EquipmentElement, int)> done = null;
            foreach (var c in cands)
            {
                var it = c.It; var cat = it.ItemCategory;
                int maxSell = (c.Have + 1) / 2, sold = 0;
                int wv = Math.Max(1, HistoricalPrices.ShelfWorth(it));
                for (int guard = 0; guard < 100 && sold < maxSell; guard++)
                {
                    int at = pack.FindIndexOfItem(it);
                    if (at < 0) break;
                    var el = pack.GetElementCopyAtIndex(at);
                    if (el.Amount <= 0) break;
                    int step = Math.Min(it.Value > DearValue ? 1 : StepN, Math.Min(maxSell - sold, el.Amount));
                    if (c.Food)
                    {
                        int room = foodHave - foodKeep;
                        if (room <= 0) { _rFood++; break; }
                        step = Math.Min(step, room);
                    }
                    if (step <= 0) break;
                    var d = town.MarketData.GetCategoryData(cat);
                    int pLast = model.GetPrice(el.EquipmentElement, mp, null, true, d.InStoreValue + (step - 1) * wv, d.Supply, d.Demand);   // cena ostatniej sztuki kroku
                    var lot = Sync(mp, it, pack.GetItemNumber(it), false);
                    double age = lot != null ? Math.Max(0, now - lot.Day) : 0;
                    bool cash = mp.PartyTradeGold < CashDays * wage, full = mp.TotalWeightCarried >= FullShare * cap;
                    bool profit = pLast > c.Min;
                    int why = profit ? 0 : LossWhy(disband, cash, full, c.Food, age);
                    if (!profit && why == 0) { _rNoProfit++; break; }
                    if (pLast < GabkaFloor * it.Value) { _rGabka++; break; }
                    int price = Math.Max(1, town.GetItemPrice(el.EquipmentElement, mp, true));
                    int spare = town.Gold - TownPurse.BuyFloor(town, reserve);
                    if (spare < price) { _rCash++; break; }
                    step = Math.Min(step, spare / price);
                    if (left < price) { _rLimit++; break; }
                    step = Math.Min(step, left / price);
                    if (step <= 0) { _rLimit++; break; }
                    int purse = mp.PartyTradeGold, had = pack.GetItemNumber(it);
                    bool broke = false;
                    TownToll.LastTaken = 0;
                    try { SellItemsAction.Apply(mp.Party, town.Owner, el, step, st); }
                    catch (Exception e) { broke = true; Stumble("Sell(SellItemsAction)", e); }
                    int moved = had - pack.GetItemNumber(it);
                    int toll = TownToll.LastTaken; TownToll.LastTaken = 0;
                    int gross = Math.Max(0, mp.PartyTradeGold - purse) + toll;   // utarg przed mytem E3-G3
                    if (broke && moved > 0 && gross == 0)
                    {
                        try { town.Owner.ItemRoster.AddToCounts(el.EquipmentElement, -moved); pack.AddToCounts(el.EquipmentElement, moved); moved = 0; }
                        catch (Exception e) { Stumble("Sell(cofniecie)", e); }
                    }
                    if (moved <= 0) break;
                    sold += moved; any = true;
                    left = Math.Max(0, left - gross);
                    if (c.Food) foodHave -= moved;
                    _gross += gross; _toll += toll;
                    double cost = c.Unit * moved;
                    Row r; if (!_rows.TryGetValue(it, out r)) { r = new Row(); _rows[it] = r; }
                    if (why == 0) { r.ProfitN += moved; r.ProfitG += gross; r.ProfitCost += (long)cost; }
                    else
                    {
                        r.LossN += moved; r.LossG += gross; r.LossCost += (long)cost;
                        switch (why) { case 1: _lDisband += moved; _lDisbandG += gross; break; case 2: _lCash += moved; _lCashG += gross; break; case 3: _lFull += moved; _lFullG += gross; break; case 4: _lFood += moved; _lFoodG += gross; break; default: _lAge += moved; _lAgeG += gross; break; }
                    }
                    Visit v; if (!_visit.TryGetValue(mp, out v)) { v = new Visit { St = st }; _visit[mp] = v; }
                    v.Cats.Add(cat);
                    if (done == null) done = new List<(EquipmentElement, int)>();
                    done.Add((el.EquipmentElement, moved));
                    if (broke) break;
                }
                if (sold >= maxSell && sold > 0) _rHalf++;
            }
            _townLeft[town] = left;
            if (any) _sellers++;
            if (done != null)
            {
                try { CampaignEventDispatcher.Instance.OnCaravanTransactionCompleted(mp, town, done); }   // jak CaravanBulk: sluchacze handlu karawan (marza P1, ksiegi)
                catch (Exception e) { Stumble("Sell(zdarzenie)", e); }
            }
        }

        // ------------------------------------------------------------ doba
        private static string Median(ItemCategory c)
        {
            var l = new List<float>();
            foreach (var t in Town.AllTowns) if (t != null) l.Add(t.GetItemCategoryPriceIndex(c));
            if (l.Count == 0) return "-";
            l.Sort();
            return l[l.Count / 2].ToString("0.00", Inv);
        }

        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                int day = (int)Now - 1;
                if (!_startDone)
                {
                    _startDone = true;
                    Log.Info("Karawany - towar tam, gdzie maja zysk (E3-T1): start sesji | klucz Caravan Sell Where Dear " + (s.CaravanSellWhereDear ? "tak" : "nie")
                             + " | latka SellItemsAction.ApplyInternal " + (_hooked ? "wpieta" : "NIE") + " (ksiega kosztu partii w jukach - sesja; po wczytaniu koszt = cena bazowa)"
                             + " | regula: sprzedaz, gdy cena > koszt zakupu + zold za dni w drodze (najwyzej " + MaxAgeDays + " dob, najwyzej 100% kosztu); na stracie tylko: rozwiazanie, kiesa < "
                             + CashDays + " dni zoldu, juki >= " + (100 * FullShare).ToString("0", Inv) + "% udzwigu, jedzenie > " + FoodAgeDays + " dob, towar > " + MaxAgeDays + " dob"
                             + " | bez progu sredniej (Jeff 16.10); zawsze: BK nie odkupuje w tej wizycie kategorii sprzedanej przez T1 (blokada " + (_bkHooked ? "wpieta" : "NIE") + "), cena >= " + GabkaFloor.ToString("0.00", Inv)
                             + " x wartosci (gabka), kasa miasta ponad BuyFloor i najwyzej 1/" + TownDayShare + " nadwyzki na dobe, krok " + StepN + " szt. (ponad " + DearValue + " d - 1), polowa stosu,"
                             + " jedzenie na " + FoodKeepDays + " dob.");
                }
                // juki wybranych towarow (raz na dobe) i porzadki ksiegi
                var cats = new Dictionary<ItemCategory, string>();
                var om = MBObjectManager.Instance;
                if (om != null) foreach (var id in WatchIds) { var c = om.GetObject<ItemCategory>(id); if (c != null && !cats.ContainsKey(c)) cats[c] = id; }
                var held = new Dictionary<ItemCategory, long>();
                int caravans = 0, books = 0; long bookUnits = 0;
                var gone = new List<MobileParty>();
                foreach (var kv in _book)
                {
                    if (kv.Key == null || !kv.Key.IsActive || !kv.Key.IsCaravan) { gone.Add(kv.Key); continue; }
                    books++;
                    foreach (var l in kv.Value.Values) bookUnits += l.N;
                }
                foreach (var p in gone) _book.Remove(p);
                var vgone = new List<MobileParty>();
                foreach (var kv in _visit) if (kv.Key == null || !kv.Key.IsActive || !ReferenceEquals(kv.Key.CurrentSettlement, kv.Value.St)) vgone.Add(kv.Key);
                foreach (var p in vgone) _visit.Remove(p);
                foreach (var c in MobileParty.AllCaravanParties)
                {
                    if (c == null || !c.IsActive || c.ItemRoster == null) continue;
                    caravans++;
                    var r = c.ItemRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || it.ItemCategory == null || el.Amount <= 0 || !cats.ContainsKey(it.ItemCategory)) continue;
                        long h0; held.TryGetValue(it.ItemCategory, out h0); held[it.ItemCategory] = h0 + el.Amount;
                    }
                }
                var soldBy = new Dictionary<ItemCategory, long>();
                foreach (var kv in _rows) { var c = kv.Key.ItemCategory; if (c == null) continue; long x; soldBy.TryGetValue(c, out x); soldBy[c] = x + kv.Value.ProfitN + kv.Value.LossN; }
                long pN = 0, pG = 0, pC = 0, lN = 0, lG = 0, lC = 0;
                var rows = new List<KeyValuePair<ItemObject, Row>>(_rows);
                foreach (var kv in rows) { pN += kv.Value.ProfitN; pG += kv.Value.ProfitG; pC += kv.Value.ProfitCost; lN += kv.Value.LossN; lG += kv.Value.LossG; lC += kv.Value.LossCost; }
                rows.Sort((a, b) => (b.Value.ProfitG + b.Value.LossG).CompareTo(a.Value.ProfitG + a.Value.LossG));
                var sb = new StringBuilder(2400);
                sb.Append("Karawany - towar tam, gdzie maja zysk (E3-T1): dzien ").Append(day).Append(" | ").Append(s.CaravanSellWhereDear ? "CZYNNE" : "WYLACZONE (MCM)")
                  .Append(" | wjazdow z towarem ").Append(_entries).Append(", sprzedaly ").Append(_sellers)
                  .Append(" | z zyskiem ").Append(pN).Append(" szt. za ").Append(pG).Append(" (koszt zakupu ").Append(pC).Append(", marza ").Append(pC > 0 ? ((pG / (double)pC - 1) * 100).ToString("+0.0;-0.0", Inv) + "%" : "-").Append(')')
                  .Append(" | na stracie ").Append(lN).Append(" szt. za ").Append(lG).Append(" (koszt ").Append(lC).Append(", strata ").Append(Math.Max(0, lC - lG)).Append("): rozwiazanie ").Append(_lDisband).Append('/').Append(_lDisbandG)
                  .Append(", brak na zold ").Append(_lCash).Append('/').Append(_lCashG).Append(", pelne juki ").Append(_lFull).Append('/').Append(_lFullG)
                  .Append(", jedzenie psuje sie ").Append(_lFood).Append('/').Append(_lFoodG).Append(", dlugo w jukach ").Append(_lAge).Append('/').Append(_lAgeG).Append(" (szt./zl)")
                  .Append(" | odrzucone kroki: bez zysku ").Append(_rNoProfit).Append(", gabka (cena < ").Append(GabkaFloor.ToString("0.00", Inv)).Append(" x wartosci) ").Append(_rGabka)
                  .Append(", kasa miasta (BuyFloor) ").Append(_rCash).Append(", limit 1/").Append(TownDayShare).Append(" nadwyzki ").Append(_rLimit).Append(", rezerwa jedzenia ").Append(_rFood)
                  .Append(", polowa stosu ").Append(_rHalf).Append(" | pominiete: kontrakt 174 ").Append(_skipContract).Append(", rozkaz gracza (BK) ").Append(_skipOrders)
                  .Append(" | wedlug towaru (z zyskiem / na stracie, szt./zl): ");
                int k = 0;
                foreach (var kv in rows)
                {
                    if (k++ >= 10) break;
                    if (k > 1) sb.Append(", ");
                    sb.Append(kv.Key.StringId).Append(' ').Append(kv.Value.ProfitN).Append('/').Append(kv.Value.ProfitG).Append(" ; ").Append(kv.Value.LossN).Append('/').Append(kv.Value.LossG);
                }
                if (k == 0) sb.Append('-');
                sb.Append(" | zloto: z kas miast ").Append(_gross).Append(" (nadwyzka kas ponad BuyFloor przy pierwszej wizycie ").Append(_townSpare0).Append("), myto E3-G3 ").Append(_toll)
                  .Append(", przyrost kies karawan ").Append(_gross - _toll)
                  .Append(" | zakup BK zablokowany (kategoria sprzedana tu w tej wizycie) ").Append(_bkBlocked).Append(" wycen, odkupione mimo to w tej samej wizycie (gabka, inne) ").Append(_rebought).Append(" szt.")
                  .Append(" | ksiega kosztu: karawan ").Append(books).Append(", sztuk ").Append(bookUnits).Append(", zakupow dzis ").Append(_bookBuys).Append(" (").Append(_bookBuyUnits).Append(" szt.)")
                  .Append(", odtworzone po cenie bazowej ").Append(_rebuilt).Append(" szt.")
                  .Append(" | w jukach ").Append(caravans).Append(" karawan (szt., dni do wyczerpania przy dzisiejszej sprzedazy T1): ");
                k = 0;
                foreach (var kv in cats)
                {
                    long h0; held.TryGetValue(kv.Key, out h0); long sd; soldBy.TryGetValue(kv.Key, out sd);
                    if (k++ > 0) sb.Append(", ");
                    sb.Append(kv.Value).Append(' ').Append(h0).Append(" (").Append(sd > 0 ? (h0 / (double)sd).ToString("0.0", Inv) : "-").Append(')');
                }
                sb.Append(" | indeks mediana: ");
                k = 0;
                foreach (var kv in cats) { if (k++ > 0) sb.Append(", "); sb.Append(kv.Value).Append(' ').Append(Median(kv.Key)); }
                sb.Append(" | wplyw na skrzynie: kasy miast -").Append(_gross).Append(", karawany +").Append(_gross - _toll).Append(", myto (glowy rodow, skarbce, kasy - linia E3-G3) +").Append(_toll)
                  .Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }
    }
}
