using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// WOZY WSI DO NAJLEPIEJ PLACACEGO MIASTA I UCZCIWA CENA LADUNKU (poprawka 119; test 06.10 14:08: 69 z 97 miast bez rudy,
    /// choc w 28 miastach przy kopalniach lezy 1740 ladunkow, a wsie gornicze dopisuja ok. 185 ladunkow na dobe).
    /// Tabor wsi to prawdziwa partia na mapie (VillagerCampaignBehavior); do tej pory jechal zawsze w to samo miejsce: wies miejska
    /// - do swojego miasta (latka BK "najpierw Bound"), wies zamkowa - do TradeBound (wpis 100, MarketRoad) albo do zamku.
    /// Ruda z 26 kopaln trafiala wiec do ok. 24 miast, a reszta swiata czekala na karawany.
    ///
    /// CZESC 1 (VillageCartsBestMarket) - wybor miasta z biezacego zysku wsi. Gdy wies wysyla tabor (i gdy gra go przekierowuje -
    /// oblezenie, wojna, wies obca), Armoury wycenia ladunek w kazdym miescie w zasiegu MarketMaxDistance drogi ladowej (wlasne
    /// miasto wsi miejskiej zawsze), pomijajac miasta oblezone i wrogie (te same warunki co gra i MarketRoad). Wycena = ta sama
    /// regula co zaplata przy wjezdzie (Value: kolejnosc od ostatniej pozycji, zwierzeta juczne zatrzymane jak w BK albo w grze,
    /// kasa miasta jako limit, cena z modelu cen gry przy polce powiekszonej o sztuki sprzedane wczesniej). Miernik: utarg na dobe
    /// kursu = wartosc / max(kurs tam i z powrotem w dobach, doby, ktore wytrzyma magazyn wsi). Magazyn wsi miesci 5 dob produkcji
    /// (Village.GetWarehouseCapacity), a przy 1.5 pojemnosci gra wstrzymuje cala produkcje wsi (TickProductions) - kurs krotszy niz
    /// zapas miejsca w magazynie nic wsi nie kosztuje, dluzszy oznacza dni bez produkcji, wiec dzielimy przez dluzszy z tych czasow
    /// (to jest dlugookresowy utarg wsi na dobe). Remis = blizsze miasto. Bez sztucznych list i wag: tylko ceny i czas.
    ///  - pelny woz na daleka droge (VillageCartFullLoadFar): gdy kurs jest dluzszy niz wytrzyma magazyn, woz bierze reszte
    ///    magazynu do udzwigu (wyceniany jest wtedy pelny ladunek) - wies nie staje, a towar nie lezy;
    ///  - wiesc z drogi (VillageCartRoadNews): do polki miasta doliczamy towar, ktory inne wozy juz tam wioza (zapisany przy
    ///    wyborze, skreslany przy wjezdzie do miasta albo zamku, przy zniszczeniu taboru i po 30 dobach) - wozy nie jada calym
    ///    stadem do jednego pustego miasta.
    /// Gdy zadne miasto nie wchodzi w gre, dalej jak dotad (wies zamkowa: MarketRoad / BK - zamek). Tabor juz stojacy w miescie
    /// nie jest przekierowywany (jak dotad). Woz jedzie trasa gry (MoveVillagersToSettlementWithBestNavigationType).
    /// CZESC 3 (VillageCartFairPrice) - uczciwa cena ladunku. Gra (SellGoodsForTradeAction.ApplyInternal) i BK (SellGoodsPatch,
    /// prefiks zwracajacy false) placa za CALY stos cene PIERWSZEJ sztuki. Zostawiamy im decyzje, CO i ILE sprzedac; prefiks
    /// (Priority.First, przed BK) zapisuje juki, kiese taboru i dane rynku miasta, postfiks odtwarza sprzedaz w tej samej kolejnosci
    /// i liczy cene sztuka po sztuce przy rosnacej polce. Gdy cena pierwszej sztuki x ilosc zgadza sie co do denara z tym, co
    /// tabor dostal (dowod, ze odtworzenie jest wierne), nadplata wraca z sakwy taboru do kasy osady. Zloto nie powstaje i nie
    /// znika - przechodzi z powrotem miedzy tymi samymi dwiema kasami. Niezgodnosc albo blad = sprzedaz zostaje jak w grze (licznik).
    /// CZESC 2 (MarketCartAllVillages) siedzi w MarketRoad.CartPostfix (woz x MarketCartFactor dla wszystkich wsi).
    /// POPRAWKA 122 (VillageCartWholeStore; test 07.10: 89 z 571 wsi z zatkanym magazynem, 8 z 26 kopaln stoi) - WOZ ZABIERA CALY
    /// MAGAZYN: przy kazdym wyjezdzie z domu na targ miasta woz bierze wszystko, co lezy w magazynie wsi (bez limitu udzwigu - udzwig
    /// taboru rosnie do wagi ladunku, CapFloor), a miernik liczy kurs z czekaniem (CycleWait) wobec dob, po ktorych magazyn zbierze
    /// nastepny ladunek (W = 5 dob produkcji), zamiast dob do przestoju (1.5 W). Wylaczone = zachowanie 121.
    /// </summary>
    internal static class MarketCarts
    {
        /// <summary>Jedna pozycja ladunku (kolejnosc jak w jukach).</summary>
        internal struct Line { public EquipmentElement El; public int N; public Line(EquipmentElement el, int n) { El = el; N = n; } }

        /// <summary>Migawka przed sprzedaza: kiesa taboru, juki i dane rynku osady.</summary>
        internal sealed class Snap { public Town Town; public int Gold; public EquipmentElement[] El; public int[] N; public Dictionary<ItemCategory, ItemData> Market; }

        /// <summary>Wiesc z drogi: dokad jedzie woz i ile wartosci towaru (Item.Value x sztuki - jednostka polki; paczka 121: waga sztuki) wiezie w kazdej kategorii.</summary>
        private sealed class Haul { public Town To; public Dictionary<ItemCategory, int> Value; public double Day; }

        private sealed class Cand { public Town T; public float D; public double Rate; public double Ub; public List<Line> Load; public bool Full; public Dictionary<ItemCategory, int> News; }

        private static MethodInfo _sell;
        private static bool _wired;                          // latka sprzedazy wpieta (czesc 3 i skreslanie wiesci przy wjezdzie)
        private static int _bkRule = -1;                     // -1 nie sprawdzone; 1 = sprzedaz w miescie robi BK SellGoodsPatch (zwierzeta: 0.5 x ludzie + 2), 0 = gra
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        // ------------------------------------------------------------ stan kampanii
        private static readonly Dictionary<MobileParty, Haul> _hauls = new Dictionary<MobileParty, Haul>();
        private static readonly Dictionary<Town, Dictionary<ItemCategory, int>> _news = new Dictionary<Town, Dictionary<ItemCategory, int>>();

        // ------------------------------------------------------------ liczniki doby (linia "Dowoz (wozy):")
        private static int _dec, _decCastle, _toOwn, _toOther, _noTown, _inTown, _full, _fullUnits, _ownN, _oreCarts, _oreToEmpty, _evals, _stumbles;
        private static long _calls, _ticks;
        private static double _sumD, _sumVal, _sumOwn, _sumOwnChosen;
        private static readonly HashSet<Town> _oreTowns = new HashSet<Town>();
        private static int _fairN, _mismatch, _rising, _fairMax;
        private static long _fairFirst, _fairPaid;
        private static double _wholeKg, _wholeMaxKg;                // poprawka 122: waga ladunku wozow z calym magazynem (linia dobowa)

        /// <summary>Poprawka 122: czekanie w kursie, ktorego nie ma w drodze - stale gry: woz wraca z miasta z szansa 20% na godzine
        /// (srednio 5 h, ThinkAboutSendingInsideVillagersToTheirHomeVillage), a z domu rusza z szansa 15% na godzine (srednio 6.7 h,
        /// ThinkAboutSendingItemToTown); razem ok. 0.49 doby.</summary>
        internal const double CycleWait = (1.0 / 0.20 + 1.0 / 0.15) / 24.0;

        internal static void Reset()
        {
            _hauls.Clear(); _news.Clear(); _errSites.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _dec = _decCastle = _toOwn = _toOther = _noTown = _inTown = _full = _fullUnits = _ownN = _oreCarts = _oreToEmpty = _evals = _stumbles = 0;
            _calls = _ticks = 0; _sumD = _sumVal = _sumOwn = _sumOwnChosen = 0.0;
            _oreTowns.Clear();
            _fairN = _mismatch = _rising = _fairMax = 0; _fairFirst = _fairPaid = 0;
            _wholeKg = _wholeMaxKg = 0.0;
        }

        /// <summary>Wyjatek przy jednym taborze: liczony zawsze, do pliku raz na miejsce (mechanizmu nie gasimy).</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error(where, e);
        }

        // ------------------------------------------------------------ regula sprzedazy (ta sama w wycenie i w odtworzeniu)
        /// <summary>Czy sprzedaz w miescie robi latka BK (SellGoodsPatch) - jej regula zwierzat jucznych jest inna niz gry.</summary>
        private static bool BkSells()
        {
            if (_bkRule < 0)
            {
                _bkRule = 0;
                try
                {
                    var info = _sell != null ? Harmony.GetPatchInfo(_sell) : null;
                    if (info != null)
                        foreach (var p in info.Prefixes)
                            if (p.PatchMethod != null && p.PatchMethod.DeclaringType != null && p.PatchMethod.DeclaringType.Name == "SellGoodsPatch") { _bkRule = 1; break; }
                }
                catch (Exception e) { Stumble("MarketCarts.BkSells", e); }
            }
            return _bkRule == 1;
        }

        /// <summary>Najtanszy rodzaj zwierzecia jucznego w jukach (tak szukaja gra i BK: wartosc ponizej 10 000).</summary>
        private static ItemObject CheapestPack(List<Line> load)
        {
            ItemObject pack = null; int v = 10000;
            var cat = DefaultItemCategories.PackAnimal;
            for (int i = 0; i < load.Count; i++)
            {
                var it = load[i].El.Item;
                if (it != null && it.ItemCategory == cat && it.Value < v) { v = it.Value; pack = it; }
            }
            return pack;
        }

        /// <summary>Ile zwierzat jucznych tabor zatrzymuje: gra 0.5 x ludzie; BK 0.5 x ludzie + 2, przy mulach jeszcze 0.1 x ludzie.</summary>
        private static int Kept(MobileParty cart, ItemObject pack, bool bkRule)
        {
            int men = cart.MemberRoster != null ? cart.MemberRoster.TotalManCount : 0;
            int k = (int)(0.5f * men);
            if (bkRule) { k += 2; if (pack.StringId == "mule") k += (int)(men * 0.1f); }
            return k;
        }

        private static int Price(TradeItemPriceFactorModel model, EquipmentElement el, MobileParty cart, int inStore, float supply, float demand)
        {
            _calls++;
            return model.GetPrice(el, cart, null, true, inStore, supply, demand);
        }

        /// <summary>Suma cen sztuk 0..n-1 stosu (sztuka k przy polce powiekszonej o k sztuk). Krotki stos sztuka po sztuce, dlugi -
        /// odcinkami rownej ceny przez polowienie (cena gry nie rosnie z polka: wzor GetBasePriceFactor maleje z wartoscia polki);
        /// wynik ten sam co sztuka po sztuce, wyliczen kilka razy mniej.</summary>
        private static long SumRun(TradeItemPriceFactorModel model, EquipmentElement el, MobileParty cart, int inStore, float s, float d, int value, int n, int p0)
        {
            if (n <= 0) return 0;
            if (n <= 24)
            {
                long t = p0;
                for (int k = 1; k < n; k++) t += Price(model, el, cart, inStore + k * value, s, d);
                return t;
            }
            int pl = Price(model, el, cart, inStore + (n - 1) * value, s, d);
            return Seg(model, el, cart, inStore, s, d, value, 0, n - 1, p0, pl);
        }

        private static long Seg(TradeItemPriceFactorModel model, EquipmentElement el, MobileParty cart, int inStore, float s, float d, int value, int a, int b, int pa, int pb)
        {
            if (pa == pb) return (long)(b - a + 1) * pa;
            if (b - a <= 1) return (long)pa + (b > a ? pb : 0);
            int m = (a + b) / 2;
            int pm = Price(model, el, cart, inStore + m * value, s, d);
            return Seg(model, el, cart, inStore, s, d, value, a, m, pa, pm) + Seg(model, el, cart, inStore, s, d, value, m, b, pm, pb) - pm;
        }

        /// <summary>
        /// Ile osada zaplaci za ladunek - te same reguly co sprzedaz gry / BK: pozycje od ostatniej, zwierzeta juczne zatrzymane,
        /// liczba sztuk z kasy osady po cenie pierwszej sztuki stosu (tak licza gra i BK), cena z czynnego modelu cen przy polce
        /// powiekszonej o sztuki sprzedane wczesniej w tej samej kategorii. fair = sztuka po sztuce (czesc 3), inaczej cena
        /// pierwszej sztuki za caly stos (gra). news = wartosc towaru, ktory inne wozy juz wioza do tej osady (doliczona do polki).
        /// </summary>
        internal static long Value(Town town, MobileParty cart, List<Line> load, bool fair, Dictionary<ItemCategory, int> news, bool bkRule)
        {
            var model = Campaign.Current.Models.TradeItemPriceFactorModel;
            var market = town.MarketData;
            var pack = CheapestPack(load);
            Dictionary<ItemCategory, int> added = null;
            long gold = town.Gold, total = 0;
            for (int i = load.Count - 1; i >= 0; i--)
            {
                var it = load[i].El.Item;
                if (it == null) continue;
                int n = load[i].N;
                if (pack != null && ReferenceEquals(it, pack)) n -= Kept(cart, it, bkRule);
                if (n <= 0) continue;
                var cat = it.GetItemCategory();
                var d = market.GetCategoryData(cat);
                int off = 0, nw = 0;
                if (added != null) added.TryGetValue(cat, out off);
                if (news != null) news.TryGetValue(cat, out nw);
                int inStore = d.InStoreValue + off + nw;
                int p0 = Price(model, load[i].El, cart, inStore, d.Supply, d.Demand);
                if (p0 <= 0) continue;
                int sold = (int)Math.Min((long)n, gold / p0);
                if (sold <= 0) continue;
                gold -= (long)sold * p0;
                total += fair ? SumRun(model, load[i].El, cart, inStore, d.Supply, d.Demand, HistoricalPrices.ShelfWorth(it), sold, p0) : (long)sold * p0;   // paczka 121: jednostka polki = waga sztuki (kategorie mieszane)
                if (added == null) added = new Dictionary<ItemCategory, int>();
                added[cat] = off + sold * HistoricalPrices.ShelfWorth(it);
            }
            return total;
        }

        /// <summary>Gorna granica wyceny - do odciecia miast, ktore i tak nie wygraja (bez limitu kasy, kazda pozycja od obecnej polki).
        /// Cena pierwszej sztuki za stos: n x p(0). Sztuka po sztuce: cena nie rosnie z polka, wiec stos dzielimy w 1/8 i 1/2 -
        /// m1 x p(0) + (m2 - m1) x p(m1) + (n - m2) x p(m2) (trzy ceny zamiast n, a granica ciasna przy pustej polce).</summary>
        private static long Upper(Town town, MobileParty cart, List<Line> load, Dictionary<ItemCategory, int> news, bool bkRule, bool fair)
        {
            var model = Campaign.Current.Models.TradeItemPriceFactorModel;
            var market = town.MarketData;
            var pack = CheapestPack(load);
            long total = 0;
            for (int i = 0; i < load.Count; i++)
            {
                var it = load[i].El.Item;
                if (it == null) continue;
                int n = load[i].N;
                if (pack != null && ReferenceEquals(it, pack)) n -= Kept(cart, it, bkRule);
                if (n <= 0) continue;
                var cat = it.GetItemCategory();
                var d = market.GetCategoryData(cat);
                int nw = 0;
                if (news != null) news.TryGetValue(cat, out nw);
                int inStore = d.InStoreValue + nw;
                long p0 = Math.Max(0, Price(model, load[i].El, cart, inStore, d.Supply, d.Demand));
                if (!fair || n < 8) { total += n * p0; continue; }
                int m1 = n / 8, m2 = n / 2;
                int wv = HistoricalPrices.ShelfWorth(it);   // paczka 121: jednostka polki = waga sztuki
                long p1 = Math.Max(0, Price(model, load[i].El, cart, inStore + m1 * wv, d.Supply, d.Demand));
                long p2 = Math.Max(0, Price(model, load[i].El, cart, inStore + m2 * wv, d.Supply, d.Demand));
                total += m1 * p0 + (m2 - m1) * p1 + (n - m2) * p2;
            }
            return total;
        }

        private static List<Line> LoadOf(ItemRoster r)
        {
            var l = new List<Line>(r.Count);
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.EquipmentElement.Item != null && el.Amount > 0) l.Add(new Line(el.EquipmentElement, el.Amount));
            }
            return l;
        }

        /// <summary>Plan doladunku "pelny woz": reszta magazynu wsi do udzwigu taboru (zwierzeta ida bez wagi - jak w grze,
        /// MoveItemsToVillagerParty), w kolejnosci magazynu. Nic nie przenosi; left = ile sztuk zostanie w magazynie.
        /// Poprawka 122 (capped = false): caly magazyn, bez limitu udzwigu - udzwig taboru rosnie do wagi ladunku (CapFloor).</summary>
        private static List<Line> TopUpPlan(MobileParty cart, ItemRoster store, bool capped, out int units, out int left)
        {
            float room = capped ? cart.InventoryCapacity - cart.TotalWeightCarried : 0f;
            var plan = new List<Line>();
            units = 0; left = 0;
            for (int i = 0; i < store.Count; i++)
            {
                var el = store.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0) continue;
                int n = el.Amount;
                if (capped && !it.HasHorseComponent && it.Weight > 0f) n = Math.Min(n, (int)Math.Floor(room / it.Weight));
                if (n < 0) n = 0;
                left += el.Amount - n;
                if (n == 0) continue;
                if (!it.HasHorseComponent) room -= n * it.Weight;
                plan.Add(new Line(el.EquipmentElement, n));
                units += n;
            }
            return plan;
        }

        /// <summary>Juki po doladunku - tak, jak ulozy je ItemRoster.AddToCounts (istniejaca pozycja rosnie, nowa idzie na koniec).</summary>
        private static List<Line> Merged(List<Line> load, List<Line> plan)
        {
            var l = new List<Line>(load);
            foreach (var p in plan)
            {
                int at = -1;
                for (int i = 0; i < l.Count; i++) if (l[i].El.IsEqualTo(p.El)) { at = i; break; }
                if (at >= 0) l[at] = new Line(l[at].El, l[at].N + p.N);
                else l.Add(p);
            }
            return l;
        }

        /// <summary>Doladunek naprawde: magazyn wsi -> juki taboru, pozycja po pozycji (najpierw zdjecie z magazynu, potem do jukow;
        /// gdy dolozenie sie nie uda - towar wraca do magazynu). Zwraca liczbe przeniesionych sztuk.</summary>
        private static int TopUp(MobileParty cart, ItemRoster store, List<Line> plan)
        {
            int moved = 0;
            foreach (var p in plan)
            {
                int at = store.FindIndexOfElement(p.El);
                int n = at >= 0 ? Math.Min(p.N, store.GetElementNumber(at)) : 0;
                if (n <= 0) continue;
                store.AddToCounts(p.El, -n);
                try { cart.ItemRoster.AddToCounts(p.El, n); moved += n; }
                catch (Exception e) { store.AddToCounts(p.El, n); Stumble("MarketCarts.TopUp", e); break; }
            }
            return moved;
        }

        /// <summary>Sztuki, ktore tabor sprzeda (bez zatrzymanych zwierzat jucznych).</summary>
        private static int SellUnits(MobileParty cart, List<Line> load, bool bkRule)
        {
            var pack = CheapestPack(load);
            int n = 0;
            foreach (var l in load) n += (pack != null && ReferenceEquals(l.El.Item, pack)) ? Math.Max(0, l.N - Kept(cart, pack, bkRule)) : l.N;
            return n;
        }

        private static int Count(ItemRoster r)
        {
            int n = 0;
            if (r != null) for (int i = 0; i < r.Count; i++) n += r.GetElementNumber(i);
            return n;
        }

        /// <summary>Jednostek drogi na dobe taboru wsi: oszacowanie gry (Campaign.EstimatedAverageVillagerPartySpeed, 3.43 na godzine)
        /// x tempo swiata (WorldPace) x godzin doby (24). Pomiar z logu 06.10 (tempo 75%): ok. 61 jednostek na dobe.</summary>
        private static double PerDay(Settings s)
        {
            var c = Campaign.Current;
            float v = c != null ? c.EstimatedAverageVillagerPartySpeed : 0f;
            if (v <= 0.1f) v = 3.43f;
            int p = s.WorldPacePercent;
            double pace = (p >= 100 || p < 5) ? 1.0 : p / 100.0;
            return v * Hours() * pace;
        }

        /// <summary>Zasieg gry dla targu wsi: DefaultVillageTradeModel - 3 srednie odleglosci miedzy sasiednimi miastami (droga ladowa).
        /// 0 = nie da sie odczytac (wtedy bez limitu, a dlugi kurs i tak tnie utarg na dobe).</summary>
        private static float GameRange()
        {
            try
            {
                var c = Campaign.Current;
                return c.Models.VillageTradeModel.TradeBoundDistanceLimitAsDays(MobileParty.NavigationType.Default) * c.EstimatedAverageVillagerPartySpeed * Hours();
            }
            catch (Exception e) { Stumble("MarketCarts.GameRange", e); return 0f; }
        }

        private static int Hours() { int h = CampaignTime.HoursInDay; return h > 0 ? h : 24; }

        private static float Dist(MobileParty cart, Settlement at, Settlement to)
        {
            var m = Campaign.Current.Models.MapDistanceModel;
            if (at != null) return m.GetDistance(at, to, false, false, MobileParty.NavigationType.Default);
            float lr;
            return m.GetDistance(cart, to, false, MobileParty.NavigationType.Default, out lr);
        }

        private static bool Hostile(MobileParty cart, Settlement home, Settlement to)
        {
            var a = cart.MapFaction ?? home.MapFaction;
            var b = to.MapFaction;
            return a != null && b != null && a != b && b.IsAtWarWith(a);
        }

        // ------------------------------------------------------------ czesc 1: wybor miasta
        /// <summary>
        /// Miasto, do ktorego tabor pojedzie (null = jak dotad: MarketRoad / BK / gra). Wolane z MarketRoad.RoutePrefix tylko przy
        /// wlaczonym VillageCartsBestMarket. Przy wyborze "pelnego wozu" doladowuje tabor z magazynu wsi.
        /// </summary>
        internal static Settlement Choose(MobileParty cart, Settlement home, Village v, Settings s)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var at = cart.CurrentSettlement;
                if (at != null && at.IsTown) { _inTown++; return null; }              // tabor w miescie: jak dotad (gra / BK)
                bool castle = v.Bound.IsCastle;
                if (castle && !s.CastleVillagesSellInTown) return null;              // wsie zamkowe wylaczone z targow miast (wpis 100)
                Forget(cart);                                                          // poprzednia wiesc tego wozu wygasa
                var load = LoadOf(cart.ItemRoster);
                // recenzja: wycena = to, co tabor NAPRAWDE dostanie - sztuka po sztuce tylko wtedy, gdy latka sprzedazy jest wpieta
                // (bez niej osada placi cene pierwszej sztuki za stos, a woz liczylby na mniej niz dostanie)
                bool bk = BkSells(), fair = s.VillageCartFairPrice && _wired;
                int uStd = SellUnits(cart, load, bk);
                if (uStd <= 0) return null;                                            // nic do sprzedania (same zwierzeta juczne) - jak dotad
                var own = castle ? v.TradeBound : v.Bound;                            // "wlasne" miasto do porownania w logu
                if (own != null && !own.IsTown) own = null;
                float max = s.MarketMaxDistance > 0f ? s.MarketMaxDistance : GameRange();          // 0 = zasieg gry dla targu wsi (TradeBound)
                double perDay = PerDay(s);
                int W = Math.Max(1, v.GetWarehouseCapacity());
                int stock = Count(home.ItemRoster);
                // poprawka 122 (caly magazyn): woz zabiera wszystko przy kazdym wyjezdzie z domu, a kurs liczy sie bez straty, gdy woz wraca,
                // zanim magazyn zbierze nastepny ladunek (W = 5 dob produkcji: magazyn pelny = wyjazd); kazda doba ponad to to doba, w ktorej
                // towar wsi lezy, a od 1.5 W wies nie produkuje niczego. Kurs liczony z czekaniem w miescie i w domu (CycleWait).
                // Zapas 2.5 doby miedzy W a bramka 1.5 W pokrywa to, czego miernik nie widzi: dosypke RealisticBannerlord po wyjezdzie
                // (10 drewna + 4 narzedzia, przy W ok. 65 ok. 1.1 doby), losowe czekanie i jazde wolniejsza od szacunku gry.
                // Wylaczone: jak 121 - doby do przestoju (1.5 W), kurs bez czekania, pelny woz tylko na daleka droge, do udzwigu.
                bool whole = s.VillageCartWholeStore;
                double fill = whole ? 1.0 : 1.5, wait = whole ? CycleWait : 0.0;
                double tfree = Math.Max(0.0, 5.0 * (fill - (double)stock / W));
                List<Line> full = null, plan = null; double tfreeFull = tfree; int extra = 0, uFull = 0;
                if (at == home && (s.VillageCartFullLoadFar || whole))
                {
                    int left;
                    plan = TopUpPlan(cart, home.ItemRoster, !whole, out extra, out left);
                    if (extra > 0) { full = Merged(load, plan); uFull = SellUnits(cart, full, bk); tfreeFull = Math.Max(0.0, 5.0 * (fill - (double)left / W)); }
                    if (uFull <= 0) full = null;
                }
                var cands = new List<Cand>();
                Cand ownC = null;
                foreach (var t in Town.AllTowns)
                {
                    var st = t != null ? t.Settlement : null;
                    if (st == null || st.IsUnderSiege || Hostile(cart, home, st)) continue;
                    bool isOwn = !castle && st == v.Bound;                             // wies miejska: swoje miasto zawsze kandydatem
                    float d = Dist(cart, at, st);
                    if (!isOwn && (d <= 0f || (max > 0f && d > max))) continue;
                    if (d < 0f) d = 0f;
                    double T = 2.0 * d / perDay + wait;
                    bool useFull = full != null && (whole || T > tfree);
                    var news = s.VillageCartRoadNews ? NewsFor(t) : null;
                    var ld = useFull ? full : load;
                    // miernik: utarg na sztuke x czesc kursu, w ktorej wies dalej produkuje (min(1, doby magazynu / doby kursu)) -
                    // dlugookresowy utarg wsi na dobe; dla zwyklego ladunku ta sama kolejnosc co wartosc / max(kurs, doby magazynu)
                    double tf = Math.Max(0.25, useFull ? tfreeFull : tfree);              // co najmniej 6 godzin: zatkany magazyn - wtedy liczy sie utarg na dobe drogi
                    var c = new Cand { T = t, D = d, Load = ld, Full = useFull, News = news };
                    c.Rate = (T > tf ? tf / T : 1.0) / (useFull ? uFull : uStd);
                    c.Ub = Upper(t, cart, ld, news, bk, fair) * c.Rate;
                    cands.Add(c);
                    if (own != null && st == own) ownC = c;
                }
                if (cands.Count == 0) { _noTown++; return null; }
                cands.Sort((a, b) => a.Ub != b.Ub ? b.Ub.CompareTo(a.Ub) : a.D.CompareTo(b.D));
                Cand best = null; double bestSc = 0.0; long bestVal = 0;
                foreach (var c in cands)
                {
                    if (best != null && c.Ub < bestSc) break;                          // gorna granica ponizej najlepszego - dalej juz nic
                    long val = Value(c.T, cart, c.Load, fair, c.News, bk); _evals++;
                    double sc = val * c.Rate;
                    if (best == null || sc > bestSc || (sc == bestSc && c.D < best.D)) { best = c; bestSc = sc; bestVal = val; }
                }
                if (best.Full && plan != null)
                {
                    int moved = TopUp(cart, home.ItemRoster, plan);
                    if (moved > 0)
                    {
                        _full++; _fullUnits += moved;
                        if (whole) { double kg = cart.TotalWeightCarried; _wholeKg += kg; if (kg > _wholeMaxKg) _wholeMaxKg = kg; }
                    }
                }
                Note(cart, best.T, best.Load, bk);
                // liczniki linii dobowej
                _dec++; if (castle) _decCastle++;
                if (own != null && best.T.Settlement == own) _toOwn++; else _toOther++;
                _sumD += best.D; _sumVal += bestVal;
                if (ownC != null)
                {
                    _ownN++; _sumOwnChosen += bestVal;
                    _sumOwn += ReferenceEquals(ownC, best) ? bestVal : Value(ownC.T, cart, ownC.Load, fair, ownC.News, bk);
                }
                OreNote(best.T, best.Load);
                return best.T.Settlement;
            }
            catch (Exception e) { Stumble("MarketCarts.Choose", e); return null; }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>
        /// Poprawka 122: udzwig taboru wsi = co najmniej waga tego, co wiezie (wolane z MarketRoad.CartPostfix, po wozie x MarketCartFactor).
        /// Woz zabiera caly magazyn bez limitu udzwigu - okreg najmuje tylu wozakow, ilu trzeba; bez tego ciezki ladunek (ruda i drewno
        /// po 100 kg) przeciazylby tabor i model predkosci gry (kara przeciazenia -0.4 x nadwyzka / udzwig) wydluzylby kurs. Z tym tabor
        /// jedzie jak pelny woz dzis (ladunek w udzwigu: -2% predkosci). Tylko w gore, tylko gdy waga przekracza dotychczasowy udzwig.
        /// </summary>
        internal static void CapFloor(MobileParty mp, Settings s, ref ExplainedNumber cap)
        {
            try
            {
                if (!s.VillageCartWholeStore || !s.VillageCartsBestMarket) return;
                // recenzja: w gore do pelnego kilograma - gra czyta udzwig jako liczbe calkowita (MobileParty.InventoryCapacity, (int) w
                // CalculateLandBaseSpeed), wiec ulamek kg ponad udzwig (np. 3933.1 kg przy udzwigu 3933) liczylby sie jako przeciazenie
                float need = (float)Math.Ceiling(mp.TotalWeightCarried);
                if (need > cap.ResultNumber) cap.LimitMin(need);
            }
            catch (Exception e) { Stumble("MarketCarts.CapFloor", e); }
        }

        private static void OreNote(Town t, List<Line> load)
        {
            var iron = DefaultItemCategories.Iron;
            if (iron == null) return;
            foreach (var l in load)
                if (l.El.Item != null && l.El.Item.ItemCategory == iron && l.N > 0)
                {
                    _oreCarts++; _oreTowns.Add(t);
                    if (t.MarketData.GetItemCountOfCategory(iron) <= 0) _oreToEmpty++;
                    return;
                }
        }

        // ------------------------------------------------------------ wiesc z drogi
        private static Dictionary<ItemCategory, int> NewsFor(Town t)
        {
            Dictionary<ItemCategory, int> d;
            return _news.TryGetValue(t, out d) && d.Count > 0 ? d : null;
        }

        private static void Note(MobileParty cart, Town to, List<Line> load, bool bk)
        {
            var s = Settings.Current;
            if (s == null || !s.VillageCartRoadNews) return;
            var pack = CheapestPack(load);
            var h = new Haul { To = to, Value = new Dictionary<ItemCategory, int>(), Day = CampaignTime.Now.ToDays };
            foreach (var l in load)
            {
                var it = l.El.Item;
                int n = l.N;
                if (pack != null && ReferenceEquals(it, pack)) n -= Kept(cart, it, bk);
                if (n <= 0) continue;
                var cat = it.GetItemCategory();
                int v;
                h.Value.TryGetValue(cat, out v);
                h.Value[cat] = v + n * HistoricalPrices.ShelfWorth(it);   // jednostka polki (paczka 121: waga sztuki w kategorii mieszanej)
            }
            if (h.Value.Count == 0) return;
            _hauls[cart] = h;
            Dictionary<ItemCategory, int> d;
            if (!_news.TryGetValue(to, out d)) { d = new Dictionary<ItemCategory, int>(); _news[to] = d; }
            foreach (var kv in h.Value) { int v; d.TryGetValue(kv.Key, out v); d[kv.Key] = v + kv.Value; }
        }

        /// <summary>Woz nie jedzie juz do miasta z wiesci (cel zmienila gra albo BK, bez nowego wyboru) - porzadki dobowe.</summary>
        private static bool Turned(MobileParty mp, Haul h)
        {
            try
            {
                var to = h.To != null ? h.To.Settlement : null;
                if (to == null) return true;
                return mp.TargetSettlement != to && mp.CurrentSettlement != to;
            }
            catch (Exception e) { Stumble("MarketCarts.Turned", e); return false; }
        }

        /// <summary>Woz dojechal (sprzedaz w miescie albo zamku), zostal zniszczony albo wybiera cel od nowa - jego wiesc wygasa.</summary>
        internal static void Forget(MobileParty cart)
        {
            Haul h;
            if (cart == null || !_hauls.TryGetValue(cart, out h)) return;
            _hauls.Remove(cart);
            Dictionary<ItemCategory, int> d;
            if (h.To == null || !_news.TryGetValue(h.To, out d)) return;
            foreach (var kv in h.Value)
            {
                int v;
                if (!d.TryGetValue(kv.Key, out v)) continue;
                v -= kv.Value;
                if (v > 0) d[kv.Key] = v; else d.Remove(kv.Key);
            }
            if (d.Count == 0) _news.Remove(h.To);
        }

        // ------------------------------------------------------------ czesc 3: uczciwa cena ladunku
        /// <summary>SellGoodsForTradeAction.ApplyInternal(Settlement, MobileParty, detail) - Priority.First, przed prefiksem BK.
        /// Skresla wiesc taboru (dojechal) i przy wlaczonej czesci 3 zapisuje stan przed sprzedaza. Niczego nie zmienia.</summary>
        public static void SellPrefix(Settlement __0, MobileParty __1, out Snap __state)
        {
            __state = null;
            if (__1 == null || !__1.IsVillager) return;
            try
            {
                Forget(__1);
                var s = Settings.Current;
                var town = __0 != null ? __0.Town : null;
                if (s == null || !s.VillageCartFairPrice || town == null) return;
                var r = __1.ItemRoster;
                var snap = new Snap { Town = town, Gold = __1.PartyTradeGold, El = new EquipmentElement[r.Count], N = new int[r.Count], Market = new Dictionary<ItemCategory, ItemData>() };
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    snap.El[i] = el.EquipmentElement; snap.N[i] = el.Amount;
                    var it = el.EquipmentElement.Item;
                    if (it == null) continue;
                    var cat = it.GetItemCategory();
                    if (cat != null && !snap.Market.ContainsKey(cat)) snap.Market[cat] = town.MarketData.GetCategoryData(cat);
                }
                __state = snap;
            }
            catch (Exception e) { __state = null; Stumble("MarketCarts.SellPrefix", e); }
        }

        /// <summary>Po sprzedazy (gry albo BK): odtworzenie tej samej sprzedazy z migawki, cena sztuka po sztuce, zwrot nadplaty
        /// z sakwy taboru do kasy osady. Tylko gdy odtworzenie zgadza sie co do denara z tym, co tabor dostal.</summary>
        public static void SellPostfix(Settlement __0, MobileParty __1, Snap __state)
        {
            if (__state == null || __1 == null) return;
            try
            {
                int paid = __1.PartyTradeGold - __state.Gold;
                if (paid <= 0) return;
                var model = Campaign.Current.Models.TradeItemPriceFactorModel;
                var r = __1.ItemRoster;
                var added = new Dictionary<ItemCategory, int>();
                long first = 0, fair = 0;
                for (int i = __state.El.Length - 1; i >= 0; i--)
                {
                    var el = __state.El[i];
                    var it = el.Item;
                    if (it == null) continue;
                    int at = r.FindIndexOfElement(el);
                    int sold = __state.N[i] - (at >= 0 ? r.GetElementNumber(at) : 0);
                    if (sold <= 0) continue;
                    var cat = it.GetItemCategory();
                    ItemData d;
                    if (cat == null || !__state.Market.TryGetValue(cat, out d)) { _mismatch++; return; }
                    int off;
                    added.TryGetValue(cat, out off);
                    int inStore = d.InStoreValue + off;
                    int p0 = model.GetPrice(el, __1, null, true, inStore, d.Supply, d.Demand);
                    first += (long)sold * p0;
                    fair += p0;
                    int wv = HistoricalPrices.ShelfWorth(it);   // paczka 121: jednostka polki = waga sztuki (tak samo dopisuje ja gra w danych rynku)
                    for (int k = 1; k < sold; k++) fair += model.GetPrice(el, __1, null, true, inStore + k * wv, d.Supply, d.Demand);
                    added[cat] = off + sold * wv;
                }
                if (first != paid) { _mismatch++; return; }                            // odtworzenie niewierne (inna sprzedaz niz gry / BK) - nie ruszamy
                long refund = paid - fair;
                if (refund < 0) { _rising++; return; }                                  // cena rosla z polka (inny model cen) - nie dokladamy z kasy osady
                _fairN++; _fairFirst += paid; _fairPaid += fair;
                if (refund == 0) return;
                __1.PartyTradeGold -= (int)refund;                                      // sakwa taboru -> kasa osady: zloto nie powstaje i nie znika
                __state.Town.ChangeGold((int)refund);
                if (refund > _fairMax) _fairMax = (int)refund;
            }
            catch (Exception e) { Stumble("MarketCarts.SellPostfix", e); }            // blad = sprzedaz zostaje jak w grze
        }

        // ------------------------------------------------------------ doba
        /// <summary>Raz na dobe: linia "Dowoz (wozy):" i porzadki we wiesci z drogi (wozy nieczynne albo w drodze ponad 30 dob).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            double now = CampaignTime.Now.ToDays;
            List<MobileParty> stale = null;
            // recenzja: wiesc wygasa takze wtedy, gdy woz skrecil gdzie indziej bez nowego wyboru (gra odeslala go do domu - HourlyTickParty /
            // TrySendingVillagerPartyToVillage, BK - z zamku do domu) - inaczej jego ladunek "lezalby" na polce miasta do 30 dob
            foreach (var kv in _hauls)
                if (kv.Key == null || !kv.Key.IsActive || now - kv.Value.Day > 30.0 || Turned(kv.Key, kv.Value)) { if (stale == null) stale = new List<MobileParty>(); stale.Add(kv.Key); }
            if (stale != null) foreach (var mp in stale) Forget(mp);
            int dropped = stale != null ? stale.Count : 0;                         // wiesci skreslone przy porzadkach (woz zniknal, skrecil, 30 dob)
            int day = (int)now - 1;
            var inv = CultureInfo.InvariantCulture;
            float lim = GameRange();
            string game = lim > 0f ? lim.ToString("0", inv) : "?";
            double ms = _ticks * 1000.0 / Stopwatch.Frequency;
            long overpay = _fairFirst - _fairPaid;
            Log.Info("Dowoz (wozy): dzien " + day + " - wybor miasta " + (s.VillageCartsBestMarket ? "CZYNNY" : "WYLACZONY") + ": " + _dec + " wozow (wsi zamkowych " + _decCastle + ")"
                     + ", do wlasnego miasta " + _toOwn + ", do innego " + _toOther + ", bez miasta w zasiegu " + _noTown + ", w miescie (jak dotad) " + _inTown
                     + "; srednio " + (_dec > 0 ? (_sumD / _dec).ToString("0", inv) : "-") + " jedn. drogi (zasieg " + s.MarketMaxDistance.ToString("0", inv) + ", gry dla targu wsi " + game + ")"
                     + "; oczekiwany utarg " + (long)_sumVal + " d" + (_ownN > 0 ? " (tam, gdzie wlasne miasto bylo w grze: " + (long)_sumOwnChosen + " d wobec " + (long)_sumOwn + " d we wlasnym)" : "")
                     + (s.VillageCartWholeStore
                        ? "; caly magazyn przy wyjezdzie (122): wozow " + _full + " (doladowane " + _fullUnits + " szt.; srednio " + (_full > 0 ? (_wholeKg / _full).ToString("0", inv) : "-") + " kg na woz, najciezszy " + _wholeMaxKg.ToString("0", inv) + " kg)"
                        : "; pelny woz na daleka droge " + _full + " (doladowane " + _fullUnits + " szt.)" + (s.VillageCartFullLoadFar ? "" : " - WYLACZONY"))
                     + "; wiesc z drogi: wozow w drodze " + _hauls.Count + ", miast " + _news.Count + ", wygasle przy porzadkach " + dropped + (s.VillageCartRoadNews ? "" : " - WYLACZONA")
                     + "; z ruda " + _oreCarts + ", w tym do miasta bez rudy " + _oreToEmpty + " (roznych miast " + _oreTowns.Count + ")"
                     + "; wycen " + _evals + " (cen " + _calls + ", " + ms.ToString("0.0", inv) + " ms)"
                     + " | cena ladunku sztuka po sztuce " + (!_wired ? "BRAK LATKI" : (s.VillageCartFairPrice ? "CZYNNA" : "WYLACZONA")) + ": sprzedazy " + _fairN + ", po cenie pierwszej sztuki " + _fairFirst
                     + " d, sztuka po sztuce " + _fairPaid + " d, tabory oddaly osadom nadplate " + overpay + " d (" + (_fairFirst > 0 ? (100.0 * overpay / _fairFirst).ToString("0.0", inv) : "0") + "%, najwieksza " + _fairMax + ")"
                     + ", niezgodne " + _mismatch + ", cena rosla " + _rising
                     + " | woz x" + s.MarketCartFactor.ToString("0.0", inv) + " dla wsi " + (s.MarketCartAllVillages ? "wszystkich" : "zamkowych z targiem w miescie")
                     + (_stumbles > 0 ? " | POTKNIECIA " + _stumbles : "") + ".");
            ClearDay();
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                _sell = AccessTools.Method(typeof(SellGoodsForTradeAction), "ApplyInternal");
                var ps = _sell != null ? _sell.GetParameters() : null;
                if (ps == null || ps.Length < 2 || ps[0].ParameterType != typeof(Settlement) || ps[1].ParameterType != typeof(MobileParty))
                {
                    Log.Info("MarketCarts: BRAK SellGoodsForTradeAction.ApplyInternal(Settlement, MobileParty, ..) - tabory dalej po cenie pierwszej sztuki; wybor miasta dziala.");
                    return;
                }
                h.Patch(_sell, prefix: new HarmonyMethod(typeof(MarketCarts), nameof(SellPrefix)) { priority = Priority.First },
                               postfix: new HarmonyMethod(typeof(MarketCarts), nameof(SellPostfix)));
                _wired = true;
                var s = Settings.Current;
                Log.Info("MarketCarts: poprawka 119 - wozy wsi do najlepiej placacego miasta w zasiegu " + (s != null ? s.MarketMaxDistance.ToString("0", CultureInfo.InvariantCulture) : "?")
                         + " (" + (s != null && s.VillageCartsBestMarket ? "CZYNNE" : "wylaczone") + "; pelny woz na daleka droge " + (s != null && s.VillageCartFullLoadFar ? "tak" : "nie")
                         + ", caly magazyn przy wyjezdzie (122) " + (s != null && s.VillageCartWholeStore ? "tak - udzwig na miare ladunku, kurs z czekaniem " + CycleWait.ToString("0.00", CultureInfo.InvariantCulture) + " doby wobec dob do nastepnego ladunku" : "nie")
                         + ", wiesc z drogi " + (s != null && s.VillageCartRoadNews ? "tak" : "nie") + "), cena ladunku sztuka po sztuce - latka wpieta ("
                         + (s != null && s.VillageCartFairPrice ? "CZYNNA" : "wylaczona") + "), woz x" + (s != null ? s.MarketCartFactor.ToString("0.0", CultureInfo.InvariantCulture) : "?")
                         + " dla wsi " + (s != null && s.MarketCartAllVillages ? "wszystkich" : "zamkowych") + ".");
            }
            catch (Exception e) { Log.Error("MarketCarts.ApplyAll", e); }
        }
    }
}
