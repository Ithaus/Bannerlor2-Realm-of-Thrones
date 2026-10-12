using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E5/B4 - TARG PRZY ZAMKU (paczka 199, PLAN 5.11, R17 projektu etapu 5 v2; decyzje Jeffa 09.10 F i Q2: "woz wsi zamkowej najpierw sprzedaje swojemu
    /// zamkowi jedzenie dla podzamcza na 10 dni, reszta do miasta").
    /// Dotad (wpis 100, poprawki 119 i 122) woz kazdej wsi zamkowej jedzie prosto na targ miasta; polka targu zamku dostaje jedzenie tylko od wozow bez miasta
    /// w zasiegu (latka BK "najpierw Bound": zamek kupuje caly ladunek z calej kasy). Dowod - linia "Jedzenie z targu (E5/0)" testu koncowego E4 (dzien 109241):
    /// na polkach 130 zamkow 4 454 szt. jedzenia, zuzycie podzamcza 285 szt./d, zamki bez zuzycia 92 z 130 (pusta polka - BK nie ma czego zjesc).
    /// Teraz (CastleMarketFirst):
    ///  1. Pierwszy przystanek (MarketRoad.RoutePrefix -> FirstStop): woz wsi zamkowej, ktory wyjezdza z domu (albo jest w drodze bez znacznika) na targ miasta
    ///     wybrany przez MarketRoad.PickTown i wiezie jedzenie, ktorego na polce jego zamku jest mniej niz CastleMarketFoodDays dni popytu podzamcza (kazda kategoria
    ///     jedzenia osobno, minus jedzenie, ktore inne wozy juz tam wioza), jedzie najpierw do zamku - gdy zamek ma ponad zapasem kupcow (CastlePurse.Spendable) na
    ///     choc jedna sztuke i lezy w zasiegu targow drogi ladowej. Wiesc z drogi o miescie (119) wygasa - drugi odcinek wybierze miasto od nowa.
    ///  2. Popyt podzamcza na dobe = budzet dnia BK kategorii (EconomyPatches.CalculateBudget z popytem gry GetDailyDemandForCategory - ta sama metoda, ktora BK
    ///     liczy zakupy ludnosci zamku w ItemConsumptionPatch; jak RawPrice.HouseBudget) / cena targu zamku (marketData.GetPrice - jak BK). ODSTEPSTWO OD PROJEKTU
    ///     (tam: SoldItems jedzenia, srednia 7 dob): przy pustej polce zjedzone = 0 (92 z 130 zamkow), wiec warunek bylby martwy tam, gdzie jedzenia brakuje
    ///     najbardziej. Spichlerz zalogi (FoodStocks) nie jest warunkiem (E5K1-07); jedzenie z polki do zapasu warowni liczy gra raz (SoldItems), B4 nic nie dopisuje.
    ///  3. Sprzedaz w zamku: prefiks High na SellGoodsForTradeAction.ApplyInternal (BK VillagerSettlementEnterPatch wola dla zamku vanilla ApplyByVillagerTrade,
    ///     ktora placi z calej kasy za caly ladunek; BK SellGoodsPatch tylko dla miast) - po ramce ksiegi towarow (GoodsLedger.WagonPre) i migawce ceny 119
    ///     (MarketCarts.SellPrefix), obie First. Tylko woz skierowany przez FirstStop do TEGO zamku; zamek kupuje tylko brakujace jedzenie (kategoria do dni popytu),
    ///     regula ceny gry (pozycje od ostatniej, cena pierwszej sztuki przy obecnej polce - potem MarketCarts.SellPostfix oddaje nadplate sztuka po sztuce, jak
    ///     w miescie), tylko ze Spendable. Zloto kasa zamku -> sakwa taboru, towar woz -> polka zamku, w oknie wjazdu taboru (ksiega pieniadza: utarg taborow
    ///     w zamkach - jak dotad; MoneyLedger bez nowych pozycji). Inny woz w zamku - jak dotad (gra).
    ///  4. Drugi odcinek: znacznik "byl w zamku" (do nastepnego wyjazdu z domu) i od razu rozkaz do miasta tym samym MarketRoad.PickTown (MarketCarts.Choose z zamku,
    ///     bez licznikow "Dowoz:"); zadne miasto - sprzedaz B4 cofnieta i zamek kupuje caly woz jak dotad (gra, return true - jedna sprzedaz na jedno
    ///     ApplyInternal, 119 odtwarza ja co do denara); nic do sprzedania - BK odsyla woz do domu.
    ///  5. Straz: BK BKPartyBehavior.VillagersCastleTick co godzine odsyla do domu KAZDY woz stojacy w zamku; woz z rozkazem do miasta wyjezdza z osady dopiero
    ///     w nastepnej klatce (Campaign.Tick: CampaignTickCacheDataStore.Tick po tickach godzinowych, przyjazd w EncounterManager.Tick), wiec prefiks pomija BK
    ///     tylko dla wozu, ktoremu wlasnie nadalismy miasto (najwyzej HoldHours godzin, cel = miasto). Wozy wrocone mimo to do domu z ladunkiem - licznik.
    ///  6. Bez petli zamek-zamek: woz ze znacznikiem nie dostaje drugiego przystanku (licznik "powtorne wizyty bez znacznika" = 0).
    /// Jedno zjawisko z E4/11 pkt 8: danina jedzenia wsi dla spichlerza zalogi zostaje w VillageBook.TownFoodPostfix; linia podaje obie drogi jedzenia okregu
    /// (VillageBook.FortVillageFood + woz). FeedFromDistrict przyjmuje Town - ta sama funkcja pozniej dla miast (GL 2.13 krok B), dzis tylko zamki (D3).
    /// Wylacznik CastleMarketFirst (wylaczony = woz prosto do miasta; woz juz w drodze do zamku nic tam nie sprzedaje i jedzie dalej). Stan wozow (w drodze
    /// do zamku z rezerwacja, znacznik "byl w zamku", straz) idzie do zapisu (arm_castlemarket, recenzja B4 uwaga 1) - po wczytaniu woz w drodze do zamku
    /// sprzedaje tam jak przed zapisem (tylko brakujace jedzenie, ze Spendable); popyt i polki liczone od nowa. Gracz = AI. Wyjatek przy jednym wozie -
    /// licznik potkniec, nic nie gasimy.
    /// </summary>
    internal static class CastleMarket
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double HoldHours = 6.0;            // straz przed BK VillagersCastleTick: najwyzej tyle godzin od rozkazu do miasta
        private const double TripMaxDays = 10.0;         // rezerwacja jedzenia wozu w drodze do zamku wygasa po tylu dobach

        // ------------------------------------------------------------ wpiecie (raz na proces)
        private static bool _wiredSell, _wiredGuard;
        private static string _missing = "-";
        private static MethodInfo _bkBudget;
        private static bool _bkBudgetLooked;

        // ------------------------------------------------------------ stan kampanii (Reset w konstruktorze ArmouryBehavior; _bound, _visited, _onward - zapis arm_castlemarket)
        private sealed class Trip { public Settlement Castle; public readonly Dictionary<ItemCategory, int> Units = new Dictionary<ItemCategory, int>(); public double Day; }
        private sealed class Onward { public Settlement Castle; public double Hour; }
        internal struct Sale { public EquipmentElement El; public int N; public int Pay; }   // jedna pozycja sprzedazy B4 (do cofniecia w tym samym ApplyInternal)
        private static readonly Dictionary<MobileParty, Trip> _bound = new Dictionary<MobileParty, Trip>();          // woz w drodze do zamku (pierwszy przystanek)
        private static readonly Dictionary<MobileParty, Settlement> _visited = new Dictionary<MobileParty, Settlement>(); // znacznik "byl w zamku" (do wyjazdu z domu)
        private static readonly Dictionary<MobileParty, Onward> _onward = new Dictionary<MobileParty, Onward>();      // woz ruszyl z zamku do miasta (straz przed BK)
        private static readonly Dictionary<Town, Dictionary<ItemCategory, float>> _budget = new Dictionary<Town, Dictionary<ItemCategory, float>>();   // budzet dnia BK
        private static List<ItemCategory> _foods;
        private static int _bkEnter = -1;                // -1 nie sprawdzone; 1 = BK sprzedaje w zamku (VillagerSettlementEnterPatch), 0 = nie
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static string _pending;                  // stan z zapisu (arm_castlemarket) do rozwiazania w OnSessionLaunched - w SyncData partii jeszcze nie ma

        // ------------------------------------------------------------ liczniki doby (linia "Targ przy zamku (5.11)")
        private static int _dAsk, _dRouted, _dFull, _dRouteNoCoin, _dVisits, _dNoNeed, _dNoCoin, _dOnward, _dFallback, _dEmpty, _dRepeat, _dWrong, _dOff;
        private static int _dBkHeld, _dHomeLoad, _dStale, _dBudgetBk, _dUndone, _stumbles;
        private static long _dRoutedUnits, _dSoldUnits, _dSoldGold, _dNoCoinUnits;

        internal static void Reset()
        {
            _bound.Clear(); _visited.Clear(); _onward.Clear(); _budget.Clear(); _errSites.Clear();
            _foods = null; _bkEnter = -1; _pending = null;
            ClearDay();
        }

        private static void ClearDay()
        {
            _dAsk = _dRouted = _dFull = _dRouteNoCoin = _dVisits = _dNoNeed = _dNoCoin = _dOnward = _dFallback = _dEmpty = _dRepeat = _dWrong = _dOff = 0;
            _dBkHeld = _dHomeLoad = _dStale = _dBudgetBk = _dUndone = _stumbles = 0;
            _dRoutedUnits = _dSoldUnits = _dSoldGold = _dNoCoinUnits = 0;
        }

        /// <summary>Wyjatek przy jednym wozie albo zamku: liczony zawsze, do pliku raz na miejsce (mechanizmu nie gasimy).</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error(where, e);
        }

        private static bool On(Settings s) { return s != null && s.CastleMarketFirst; }

        /// <summary>Dni popytu podzamcza na polce zamku (przyciete do 1..30).</summary>
        private static float Days(Settings s)
        {
            float d = s != null ? s.CastleMarketFoodDays : 10f;
            if (float.IsNaN(d) || d < 1f) return 1f;
            return d > 30f ? 30f : d;
        }

        /// <summary>Paczka czynna: sprzedaz w zamku i straz wpiete, a BK sprzedaje w zamku (bez BK woz w zamku nic by nie sprzedal i stal).</summary>
        private static bool Ready(out string why)
        {
            why = null;
            if (!_wiredSell) { why = "brak latki sprzedazy w zamku"; return false; }
            if (!_wiredGuard) { why = "brak BK BKPartyBehavior.VillagersCastleTick"; return false; }
            if (_bkEnter < 0) _bkEnter = BkSellsAtCastle() ? 1 : 0;
            if (_bkEnter != 1) { why = "BK nie sprzedaje w zamku (brak VillagerSettlementEnterPatch)"; return false; }
            return true;
        }

        private static bool BkSellsAtCastle()
        {
            try
            {
                var m = AccessTools.Method(typeof(VillagerCampaignBehavior), "OnSettlementEntered", new[] { typeof(MobileParty), typeof(Settlement), typeof(Hero) });
                var info = m != null ? Harmony.GetPatchInfo(m) : null;
                if (info != null)
                    foreach (var p in info.Prefixes)
                        if (p.PatchMethod != null && p.PatchMethod.DeclaringType != null && p.PatchMethod.DeclaringType.Name == "VillagerSettlementEnterPatch") return true;
            }
            catch (Exception e) { Stumble("CastleMarket.BkSellsAtCastle", e); }
            return false;
        }

        private static bool Hostile(MobileParty cart, Settlement home, Settlement to)
        {
            var a = cart.MapFaction ?? (home != null ? home.MapFaction : null);
            var b = to.MapFaction;
            return a != null && b != null && a != b && b.IsAtWarWith(a);
        }

        private static bool Others(Settlement st) { return OthersRise.IsWhiteWalkerClan(st.MapFaction as Clan); }   // ROT: warownie Innych bez zywnosci z okregu

        private static VillagerCampaignBehavior Villagers()
        {
            var c = Campaign.Current;
            return c != null ? c.GetCampaignBehavior<VillagerCampaignBehavior>() : null;
        }

        /// <summary>Kategorie jedzenia (jedna definicja z E5/0: TownFolk.IsFoodCategory) - raz na kampanie.</summary>
        private static List<ItemCategory> Foods()
        {
            if (_foods == null)
            {
                var l = new List<ItemCategory>();
                foreach (var c in ItemCategories.All) if (TownFolk.IsFoodCategory(c)) l.Add(c);
                _foods = l;
            }
            return _foods;
        }

        /// <summary>Czy na wozie jest jeszcze cos do sprzedania (poza zwierzetami jucznymi).</summary>
        private static bool Sellable(MobileParty cart)
        {
            var r = cart.ItemRoster;
            if (r == null) return false;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it != null && el.Amount > 0 && it.ItemCategory != DefaultItemCategories.PackAnimal) return true;
            }
            return false;
        }

        // ------------------------------------------------------------ popyt podzamcza
        /// <summary>Budzet dnia zakupow ludnosci osady w kategorii (zl): ta sama metoda, ktora liczy go BK przy konsumpcji (EconomyPatches.CalculateBudget - z polityka
        /// podatkowa osady i z BudgetPostfix Armoury), a bez BK - model gry; popyt dnia gry bez dodatku dobrobytu (jak UpdateDemandShift). Pamiec na dobe.</summary>
        private static float Budget(Town t, ItemCategory c)
        {
            Dictionary<ItemCategory, float> d;
            if (!_budget.TryGetValue(t, out d)) { d = new Dictionary<ItemCategory, float>(); _budget[t] = d; }
            float v;
            if (d.TryGetValue(c, out v)) return v;
            v = BudgetNow(t, c);
            if (float.IsNaN(v) || float.IsInfinity(v) || v < 0f) v = 0f;
            d[c] = v;
            return v;
        }

        private static float BudgetNow(Town t, ItemCategory c)
        {
            var econ = Campaign.Current.Models.SettlementEconomyModel;
            if (econ == null) return 0f;
            float dem = econ.GetDailyDemandForCategory(t, c, 0);
            if (!_bkBudgetLooked) { _bkBudgetLooked = true; _bkBudget = AccessTools.Method("BannerKings.Patches.EconomyPatches:CalculateBudget"); }
            if (_bkBudget != null)
            {
                // bez globalnego wylacznika: kazde wywolanie probuje BK znowu, blad liczony (pierwszy do logu), ta pozycja - model gry
                try { return (float)_bkBudget.Invoke(null, new object[] { t, dem, c }); }
                catch (Exception e) { _dBudgetBk++; Stumble("CastleMarket.Budget(BK)", e); }
            }
            return econ.CalculateDailySettlementBudgetForItemCategory(t, dem, c);
        }

        /// <summary>Popyt ludnosci osady na te sztuke w sztukach na dobe: budzet kategorii / cena zakupu na targu osady (BK ItemConsumptionPatch: marketData.GetPrice(item)).</summary>
        private static double PerDay(Town t, ItemObject it, ItemCategory c)
        {
            float b = Budget(t, c);
            if (!(b > 0f)) return 0.0;
            int p = t.MarketData.GetPrice(it);
            return p > 0 ? b / p : 0.0;
        }

        private static int ShelfUnits(Settlement st, ItemCategory c)
        {
            int n = 0;
            var r = st.ItemRoster;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it != null && el.Amount > 0 && it.GetItemCategory() == c) n += el.Amount;
            }
            return n;
        }

        /// <summary>Jedzenie, ktore inne wozy juz wioza do tego zamku (rezerwacje pierwszego przystanku) - w tej kategorii.</summary>
        private static int Reserved(Settlement castle, ItemCategory c)
        {
            int n = 0;
            foreach (var kv in _bound)
            {
                int u;
                if (kv.Value.Castle == castle && kv.Value.Units.TryGetValue(c, out u)) n += u;
            }
            return n;
        }

        /// <summary>Ile sztuk kategorii brakuje na polce osady do Days dni popytu ludnosci (0 = nic): popyt x dni - polka - to, co juz jedzie.</summary>
        private static int Want(Town t, ItemObject it, ItemCategory c, Settings s, int reserved)
        {
            double perDay = PerDay(t, it, c);
            if (!(perDay > 0.0)) return 0;
            double need = Days(s) * perDay - ShelfUnits(t.Settlement, c) - reserved;
            if (!(need > 0.0)) return 0;
            return need >= int.MaxValue ? int.MaxValue : (int)Math.Ceiling(need);
        }

        // ------------------------------------------------------------ 1. pierwszy przystanek (MarketRoad.RoutePrefix)
        /// <summary>
        /// Zamek wsi, do ktorego woz ma pojechac przed miastem `go` (null = prosto do miasta). Wolane tylko, gdy MarketRoad.PickTown wybral miasto. Przy wyborze
        /// zamku zapisuje rezerwacje jedzenia (inne wozy licza ja jak towar na polce) i skresla wiesc z drogi o miescie.
        /// </summary>
        internal static Settlement FirstStop(MobileParty cart, Settlement home, Village v, Settings s, Settlement go)
        {
            try
            {
                if (!On(s) || cart == null || home == null || v == null || go == null) return null;
                var castle = v.Bound;
                if (castle == null || !castle.IsCastle || castle.Town == null || castle == go) return null;
                var at = cart.CurrentSettlement;
                if (at == home) _visited.Remove(cart);                       // nowy kurs z domu - znacznik "byl w zamku" wygasa
                else if (at != null) return null;                             // woz w miescie albo w zamku - bez pierwszego przystanku
                if (_visited.ContainsKey(cart)) return null;                  // drugi odcinek po zamku - bez petli zamek-zamek
                string why;
                if (!Ready(out why)) return null;
                if (castle.IsUnderSiege || Hostile(cart, home, castle) || Others(castle)) return null;
                // droga ladowa do zamku w zasiegu targow (wpis 100: MarketMaxDistance; 0 = bez limitu)
                float d = Campaign.Current.Models.MapDistanceModel.GetDistance(home, castle, false, false, MobileParty.NavigationType.Default);
                if (d <= 0f || (s.MarketMaxDistance > 0f && d > s.MarketMaxDistance)) return null;
                var fort = castle.Town;
                var r = cart.ItemRoster;
                if (r == null) return null;
                _bound.Remove(cart);                                          // nowy wybor tego wozu (np. po ucieczce w drodze) - jego stara rezerwacja nie liczy sie przeciw niemu
                var left = new Dictionary<ItemCategory, int>();
                var trip = new Trip { Castle = castle, Day = CampaignTime.Now.ToDays };
                int total = 0, minPrice = int.MaxValue;
                bool food = false;
                for (int i = r.Count - 1; i >= 0; i--)                       // kolejnosc sprzedazy gry: od ostatniej pozycji
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    var c = it.GetItemCategory();
                    if (!TownFolk.IsFoodCategory(c)) continue;
                    food = true;
                    int w;
                    if (!left.TryGetValue(c, out w)) w = Want(fort, it, c, s, Reserved(castle, c));
                    int n = Math.Min(el.Amount, w);
                    if (n <= 0) { left[c] = 0; continue; }
                    left[c] = w - n;
                    int u; trip.Units.TryGetValue(c, out u); trip.Units[c] = u + n;
                    total += n;
                    int p = fort.GetItemPrice(el.EquipmentElement, cart, true);
                    if (p > 0 && p < minPrice) minPrice = p;
                }
                if (!food) return null;                                       // woz bez jedzenia - prosto do miasta (bez licznika)
                _dAsk++;
                if (total <= 0) { _dFull++; return null; }                    // zamek ma dosc tego, co woz wiezie
                if (minPrice == int.MaxValue || CastlePurse.Spendable(fort) < minPrice) { _dRouteNoCoin++; return null; }   // kasa przy zapasie kupcow - nie kupi ani sztuki
                _bound[cart] = trip;
                MarketCarts.Forget(cart);         // wiesc z drogi (119) o miescie wygasa: woz jedzie najpierw do zamku, miasto wybierze od nowa drugi odcinek
                _dRouted++; _dRoutedUnits += total;
                return castle;
            }
            catch (Exception e) { Stumble("CastleMarket.FirstStop", e); return null; }
        }

        // ------------------------------------------------------------ 3. sprzedaz w zamku i 4. drugi odcinek
        /// <summary>
        /// SellGoodsForTradeAction.ApplyInternal(Settlement, MobileParty, detail) - prefiks High (po GoodsLedger.WagonPre i MarketCarts.SellPrefix - First).
        /// Woz skierowany do tego zamku przez FirstStop: zamek kupuje brakujace jedzenie (FeedFromDistrict), woz dostaje znacznik i rozkaz do miasta; false = sprzedaz
        /// gry pominieta (postfiksy biegna: zwrot nadplaty 119, ramka ksiegi towarow). Kazdy inny woz - true (gra jak dotad); brak miasta dalej - sprzedaz B4
        /// cofnieta (Undo) i true: caly woz sprzedaje gra jak dotad, jedna sprzedaz na jedno ApplyInternal (recenzja B4 uwaga 2).
        /// </summary>
        public static bool SellPrefix(Settlement __0, MobileParty __1)
        {
            if (__0 == null || __1 == null || !__1.IsVillager || !__0.IsCastle) return true;
            Trip trip;
            if (_bound.Count == 0 || !_bound.TryGetValue(__1, out trip)) return true;   // woz nie skierowany przez targ przy zamku - jak dotad
            try
            {
                _bound.Remove(__1);
                if (trip.Castle != __0) { _dWrong++; return true; }        // woz wjechal do innego zamku - jak dotad
                var s = Settings.Current;
                var home = __1.HomeSettlement;
                var v = home != null ? home.Village : null;
                var fort = __0.Town;
                if (s == null || v == null || fort == null) return true;
                _dVisits++;
                if (_visited.ContainsKey(__1)) _dRepeat++;                   // drugi przystanek w jednym kursie - nie powinno sie zdarzyc (FirstStop pyta o znacznik)
                _visited[__1] = __0;                                         // znacznik "byl w zamku" - do nastepnego wyjazdu z domu
                long gold = 0; int sold = 0, shortN = 0; bool wanted = true;
                List<Sale> sales = null;
                if (On(s))
                {
                    sales = new List<Sale>();
                    sold = FeedFromDistrict(fort, __1, s, out gold, out shortN, out wanted, sales);
                }
                else _dOff++;                                                // wylaczone w drodze: nic nie sprzedaje, jedzie dalej (stan sprzed paczki)
                // drugi odcinek: reszta ladunku do miasta - ta sama regula co wyjazd z domu (MarketRoad.PickTown -> MarketCarts.Choose z zamku), bez licznikow "Dowoz:"
                if (!Sellable(__1)) { NoteSale(sold, gold, shortN, wanted); _dEmpty++; return false; }   // nic do sprzedania: BK odesle woz do domu (VillagersCastleTick)
                var go = MarketRoad.PickTown(__1, home, v, s, false);
                if (go == null || !go.IsTown)
                {
                    // zadne miasto: JEDNA sprzedaz na jedno ApplyInternal (recenzja B4 uwaga 2) - sprzedaz B4 cofnieta co do sztuki i denara, caly woz sprzedaje
                    // gra jak dotad (cala kasa, jak kazdy woz bez miasta); zwrot nadplaty 119 odtwarza te jedna sprzedaz z migawki (bez "niezgodne")
                    if (sales != null && sales.Count > 0) Undo(fort, __1, sales);
                    _dFallback++;
                    return true;
                }
                NoteSale(sold, gold, shortN, wanted);
                MarketRoad.Move(Villagers(), __1, go);
                _onward[__1] = new Onward { Castle = __0, Hour = CampaignTime.Now.ToHours };
                _dOnward++;
                return false;
            }
            catch (Exception e) { Stumble("CastleMarket.SellPrefix", e); return true; }   // blad: reszta jak w grze (to, co juz przeszlo, przeszlo parami zloto/towar)
        }

        /// <summary>Liczniki linii dla sprzedazy B4, ktora zostaje (woz pusty albo dalej do miasta); przy cofnieciu (bez miasta dalej) nic.</summary>
        private static void NoteSale(int sold, long gold, int shortN, bool wanted)
        {
            _dSoldUnits += sold; _dSoldGold += gold;
            if (!wanted) _dNoNeed++;
            if (shortN > 0) { _dNoCoin++; _dNoCoinUnits += shortN; }
        }

        /// <summary>
        /// Cofniecie sprzedazy B4 w tym samym ApplyInternal (zadne miasto dalej): odwrotnie do FeedFromDistrict - towar polka -> woz, zloto sakwa taboru ->
        /// kasa zamku, pozycje od ostatniej sprzedanej. Miedzy sprzedaza a cofnieciem nic nie rusza polki, wozu ani kas (MarketRoad.PickTown z zamku: bez
        /// doladunku - at != home; CastlePurse.CanPayCart tylko czyta), wiec polka, dane rynku zamku, kasa i sakwa wracaja do stanu sprzed sprzedazy; ramki
        /// ksiegi towarow i okno ksiegi pieniadza widza zero netto. Stos sprzedany do konca wraca na koniec wozu (kolejnosc pozycji gry inna niz w migawce 119 -
        /// wazne tylko przy dwoch roznych sztukach tej samej kategorii jedzenia; wtedy 119 liczy "niezgodne" i nie zwraca nadplaty, zloto dalej parami).
        /// </summary>
        private static void Undo(Town fort, MobileParty cart, List<Sale> sales)
        {
            var r = cart.ItemRoster;
            var shelf = fort.Settlement.ItemRoster;
            for (int k = sales.Count - 1; k >= 0; k--)
            {
                var x = sales[k];
                shelf.AddToCounts(x.El, -x.N);
                r.AddToCounts(x.El, x.N);
                fort.ChangeGold(x.Pay);
                cart.PartyTradeGold -= x.Pay;
            }
            _dUndone++;
        }

        /// <summary>
        /// Jedzenie okregu na polke warowni z wozu jej wsi: tylko brakujace do Days dni popytu ludnosci (kazda kategoria osobno), cena targu warowni wedlug reguly gry
        /// (pozycje od ostatniej, cena pierwszej sztuki przy obecnej polce - zwrot nadplaty sztuka po sztuce robi potem MarketCarts.SellPostfix), tylko z kasy ponad
        /// zapasem kupcow (CastlePurse.Spendable). Kazda pozycja parami w kolejnosci gry: zloto (kasa -> sakwa taboru), potem towar (woz -> polka). Dzis tylko zamki
        /// (FirstStop); miasta - pozniej (GL 2.13 krok B, D3). shortN = sztuki, ktorych warownia chciala, a nie miala czym zaplacic. sales (opcja) - pozycje
        /// sprzedazy do ewentualnego cofniecia (Undo).
        /// </summary>
        internal static int FeedFromDistrict(Town fort, MobileParty cart, Settings s, out long gold, out int shortN, out bool wanted, List<Sale> sales = null)
        {
            gold = 0; shortN = 0; wanted = false;
            var r = cart.ItemRoster;
            var shelf = fort.Settlement != null ? fort.Settlement.ItemRoster : null;
            if (r == null || shelf == null) return 0;
            long spend = CastlePurse.Spendable(fort);
            var left = new Dictionary<ItemCategory, int>();
            int sold = 0;
            for (int i = r.Count - 1; i >= 0; i--)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0) continue;
                var c = it.GetItemCategory();
                if (!TownFolk.IsFoodCategory(c)) continue;
                int w;
                if (!left.TryGetValue(c, out w)) w = Want(fort, it, c, s, Reserved(fort.Settlement, c));
                int n = Math.Min(el.Amount, w);
                if (n <= 0) { left[c] = 0; continue; }
                wanted = true;
                int price = fort.GetItemPrice(el.EquipmentElement, cart, true);
                if (price <= 0) continue;
                long can = spend / price;
                if (can < n)
                {
                    shortN += n - (int)Math.Max(0L, can);
                    n = (int)Math.Max(0L, can);
                    left[c] = 0;                                             // reszty tej kategorii warownia i tak nie oplaci (bez drugiego liczenia braku)
                }
                else left[c] = w - n;
                if (n <= 0) continue;
                int pay = n * price;
                cart.PartyTradeGold += pay;                                  // ta sama kolejnosc co sprzedaz gry (SellGoodsForTradeAction.ApplyInternal)
                fort.ChangeGold(-pay);
                shelf.AddToCounts(el.EquipmentElement, n);
                r.AddToCounts(el.EquipmentElement, -n);
                spend -= pay; gold += pay; sold += n;
                if (sales != null) sales.Add(new Sale { El = el.EquipmentElement, N = n, Pay = pay });   // do cofniecia (SellPrefix: zadne miasto dalej)
            }
            return sold;
        }

        // ------------------------------------------------------------ 5. straz przed BK VillagersCastleTick
        /// <summary>
        /// BK BKPartyBehavior.VillagersCastleTick(MobileParty) - prefiks First (BK woła go co godzine dla kazdej partii; woz w zamku -> SetMoveGoToSettlement(dom)).
        /// false tylko dla wozu, ktory wlasnie dostal z zamku rozkaz do miasta i jeszcze nie wyjechal (najwyzej HoldHours godzin); kazdy inny - BK jak dotad.
        /// </summary>
        public static bool CastleTickPrefix(MobileParty __0)
        {
            if (_onward.Count == 0 || __0 == null) return true;               // tanie wyjscie - BK wola to dla kazdej partii co godzine
            try
            {
                Onward o;
                if (!_onward.TryGetValue(__0, out o)) return true;
                if (__0.CurrentSettlement != o.Castle) { _onward.Remove(__0); return true; }    // woz juz wyjechal - straz wygasa
                var t = __0.TargetSettlement;
                if (CampaignTime.Now.ToHours - o.Hour > HoldHours || t == null || !t.IsTown || __0.DefaultBehavior != AiBehavior.GoToSettlement)
                {
                    _onward.Remove(__0);                                      // straz tylko dla swiezego rozkazu do miasta
                    return true;
                }
                _dBkHeld++;
                return false;
            }
            catch (Exception e) { Stumble("CastleMarket.CastleTickPrefix", e); return true; }
        }

        /// <summary>Tabor zniszczony (MarketRoad.OnPartyDestroyed): rezerwacja, znacznik i straz wygasaja.</summary>
        internal static void Forget(MobileParty p)
        {
            if (p == null) return;
            _bound.Remove(p); _visited.Remove(p); _onward.Remove(p);
        }

        // ------------------------------------------------------------ zapis (arm_castlemarket; recenzja B4 uwaga 1)
        // Bez zapisu woz skierowany do zamku przez FirstStop nie mial po wczytaniu wpisu w _bound: SellPrefix przepuszczal go do gry, a gra sprzedawala
        // zamkowi CALY ladunek (ruda, drewno, welna) z calej kasy, z pominieciem Spendable. Rekordy (oddzielone '~', pola '|'):
        //   b|woz|zamek|doba|kategoria:sztuk,...   - woz w drodze do zamku (rezerwacja jedzenia, liczona przez inne wozy jak towar na polce)
        //   v|woz|zamek                            - znacznik "byl w zamku" (do nastepnego wyjazdu z domu - bez petli zamek-zamek)
        //   o|woz|zamek|godzina                    - straz przed BK VillagersCastleTick (woz z rozkazem do miasta jeszcze w zamku)
        // Brak klucza (zapis sprzed paczki) = stan pusty jak dotad. Starszy DLL klucza nie czyta (zapis dalej sie wczytuje).

        /// <summary>Stan wozow do zapisu (napis; pusty = nic w drodze).</summary>
        internal static string Export()
        {
            if (_pending != null) ResolvePending("zapis przed startem sesji");
            var sb = new StringBuilder();
            foreach (var kv in _bound)
            {
                try   // wyjatek jednego rekordu nie gubi reszty
                {
                    var mp = kv.Key; var t = kv.Value;
                    if (mp == null || !mp.IsActive || string.IsNullOrEmpty(mp.StringId) || t == null || t.Castle == null) continue;
                    sb.Append("b|").Append(mp.StringId).Append('|').Append(t.Castle.StringId).Append('|').Append(t.Day.ToString("R", Inv)).Append('|');
                    foreach (var u in t.Units)
                        if (u.Key != null && u.Value > 0) sb.Append(u.Key.StringId).Append(':').Append(u.Value.ToString(Inv)).Append(',');
                    sb.Append('~');
                }
                catch (Exception e) { Stumble("CastleMarket.Export(b)", e); }
            }
            foreach (var kv in _visited)
            {
                var mp = kv.Key;
                if (mp == null || !mp.IsActive || string.IsNullOrEmpty(mp.StringId) || kv.Value == null) continue;
                sb.Append("v|").Append(mp.StringId).Append('|').Append(kv.Value.StringId).Append('~');
            }
            foreach (var kv in _onward)
            {
                var mp = kv.Key;
                if (mp == null || !mp.IsActive || string.IsNullOrEmpty(mp.StringId) || kv.Value == null || kv.Value.Castle == null) continue;
                sb.Append("o|").Append(mp.StringId).Append('|').Append(kv.Value.Castle.StringId).Append('|').Append(kv.Value.Hour.ToString("R", Inv)).Append('~');
            }
            return sb.ToString();
        }

        /// <summary>Z SyncData: tylko zapamietanie (partie i osady szukamy w OnSessionLaunched - ResolvePending).</summary>
        internal static void Import(string s)
        {
            _bound.Clear(); _visited.Clear(); _onward.Clear();
            _pending = string.IsNullOrEmpty(s) ? null : s;
        }

        /// <summary>OnSessionLaunched (i awaryjnie z Export): rekordy z zapisu na wozy i zamki (po StringId); woza albo zamku juz nie ma - rekord pominiety.</summary>
        internal static void ResolvePending(string why)
        {
            var s = _pending;
            _pending = null;
            if (string.IsNullOrEmpty(s)) return;
            int nb = 0, nv = 0, no = 0, lost = 0;
            try
            {
                var carts = new Dictionary<string, MobileParty>();
                foreach (var p in MobileParty.AllVillagerParties) if (p != null && !string.IsNullOrEmpty(p.StringId)) carts[p.StringId] = p;
                var cats = new Dictionary<string, ItemCategory>();
                foreach (var c in ItemCategories.All) if (c != null && !string.IsNullOrEmpty(c.StringId)) cats[c.StringId] = c;
                var om = MBObjectManager.Instance;
                foreach (var rec in s.Split('~'))
                {
                    if (rec.Length == 0) continue;
                    try
                    {
                        var a = rec.Split('|');
                        MobileParty mp = null; Settlement castle = null;
                        if (a.Length >= 3)
                        {
                            carts.TryGetValue(a[1], out mp);
                            try { castle = om.GetObject<Settlement>(a[2]); } catch { }
                        }
                        if (mp == null || !mp.IsActive || castle == null || !castle.IsCastle) { lost++; continue; }
                        if (a[0] == "b" && a.Length == 5)
                        {
                            double day;
                            if (!double.TryParse(a[3], NumberStyles.Float, Inv, out day)) { lost++; continue; }
                            var t = new Trip { Castle = castle, Day = day };
                            foreach (var tok in a[4].Split(','))
                            {
                                if (tok.Length == 0) continue;
                                var q = tok.Split(':');
                                ItemCategory c; int n;
                                if (q.Length != 2 || !cats.TryGetValue(q[0], out c) || !int.TryParse(q[1], NumberStyles.Integer, Inv, out n) || n <= 0) continue;
                                int u; t.Units.TryGetValue(c, out u); t.Units[c] = u + n;
                            }
                            _bound[mp] = t; nb++;
                        }
                        else if (a[0] == "v" && a.Length == 3) { _visited[mp] = castle; nv++; }
                        else if (a[0] == "o" && a.Length == 4)
                        {
                            double h;
                            if (!double.TryParse(a[3], NumberStyles.Float, Inv, out h)) { lost++; continue; }
                            _onward[mp] = new Onward { Castle = castle, Hour = h }; no++;
                        }
                        else lost++;
                    }
                    catch (Exception e) { Stumble("CastleMarket.ResolvePending(rekord)", e); lost++; }
                }
            }
            catch (Exception e) { Stumble("CastleMarket.ResolvePending", e); }
            Log.Info("CastleMarket (E5/B4): stan wozow z zapisu (" + why + ") - w drodze do zamku " + nb + ", znaczniki \"byl w zamku\" " + nv + ", straz BK " + no
                     + "; pominiete (woza albo zamku juz nie ma, rekord nieczytelny) " + lost + ".");
        }

        // ------------------------------------------------------------ doba
        /// <summary>Porzadki: rezerwacje wozow, ktore nie jada juz do zamku (nieczynne, zawrocone, ponad TripMaxDays), straz po HoldHours, znaczniki nieczynnych wozow.</summary>
        private static void Sweep()
        {
            double now = CampaignTime.Now.ToDays, nowH = CampaignTime.Now.ToHours;
            var drop = new List<MobileParty>();
            foreach (var kv in _bound)
            {
                var mp = kv.Key; var t = kv.Value;
                if (mp == null || !mp.IsActive || now - t.Day > TripMaxDays || (mp.TargetSettlement != t.Castle && mp.CurrentSettlement != t.Castle)) drop.Add(mp);
            }
            foreach (var mp in drop) _bound.Remove(mp);
            _dStale += drop.Count;
            drop.Clear();
            foreach (var kv in _onward)
            {
                var mp = kv.Key; var o = kv.Value;
                if (mp == null || !mp.IsActive) { drop.Add(mp); continue; }
                if (mp.CurrentSettlement == o.Castle && nowH - o.Hour <= HoldHours) continue;
                // woz zawrocony do domu z ladunkiem (straz nie zadzialala - np. JIT wkleil metode BK) zamiast do miasta
                if (mp.TargetSettlement != null && mp.TargetSettlement == mp.HomeSettlement && Sellable(mp)) _dHomeLoad++;
                drop.Add(mp);
            }
            foreach (var mp in drop) _onward.Remove(mp);
            drop.Clear();
            foreach (var kv in _visited) if (kv.Key == null || !kv.Key.IsActive) drop.Add(kv.Key);
            foreach (var mp in drop) _visited.Remove(mp);
        }

        /// <summary>Raz na dobe (po MarketCarts.Daily): porzadki i linia "Targ przy zamku (5.11)" - takze przy wylaczonym kluczu (stan polek zamkow do porownan).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays - 1;                     // numer doby jak w liniach "Dowoz:" (doba zakonczona)
            try
            {
                _budget.Clear();                                             // budzet dnia BK - od nowa (dobrobyt, ceny, polityka podatkowa)
                try { Sweep(); } catch (Exception e) { Stumble("CastleMarket.Sweep", e); }
                string why;
                bool ready = Ready(out why), on = On(s);
                float days = Days(s);
                int castles = 0, low = 0, noDemand = 0;
                long shelfUnits = 0;
                double budgetAll = 0.0, tribute = 0.0;
                var list = new List<double>();
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsCastle || st.Town == null) continue;
                    castles++;
                    try
                    {
                        if (Others(st)) continue;
                        var town = st.Town;
                        double val = 0.0;
                        var r = st.ItemRoster;
                        for (int i = 0; r != null && i < r.Count; i++)
                        {
                            var el = r.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (it == null || el.Amount <= 0 || !TownFolk.IsFoodCategory(it.GetItemCategory())) continue;
                            shelfUnits += el.Amount;
                            int p = town.MarketData.GetPrice(it);
                            if (p > 0) val += (double)el.Amount * p;
                        }
                        double b = 0.0;
                        foreach (var c in Foods()) b += Budget(town, c);
                        budgetAll += b;
                        if (!(b > 0.0)) noDemand++;
                        else { double dd = val / b; list.Add(dd); if (dd < days) low++; }
                        if (!st.IsUnderSiege)
                        {
                            var bv = st.BoundVillages;
                            for (int i = 0; bv != null && i < bv.Count; i++) tribute += VillageBook.FortVillageFood(bv[i]);
                        }
                    }
                    catch (Exception e) { Stumble("CastleMarket.Daily(zamek)", e); }
                }
                list.Sort();
                string med = list.Count > 0 ? list[list.Count / 2].ToString("0.0", Inv) : "-";
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                var sb = new StringBuilder(1400);
                sb.Append("Targ przy zamku (5.11): dzien ").Append(day).Append(" | ")
                  .Append(on ? (ready ? "CZYNNY" : "NIECZYNNY (" + why + ")") : "WYLACZONY (MCM Castle Market First)")
                  .Append(" | wizyt ").Append(_dVisits).Append(", jedzenie sprzedane zamkom ").Append(_dSoldUnits).Append(" szt. (").Append(_dSoldGold)
                  .Append(" zl wedlug ceny pierwszej sztuki - nadplate oddaje 119), dalej do miasta ").Append(_dOnward).Append(" wozow, odmowy (brak Spendable) ").Append(_dNoCoin)
                  .Append(" (niekupione ").Append(_dNoCoinUnits).Append(" szt.); nic nie brakowalo ").Append(_dNoNeed)
                  .Append(", bez miasta dalej (zamek kupil caly woz jak dotad) ").Append(_dFallback).Append(" (cofniete sprzedaze B4 ").Append(_dUndone).Append(")")
                  .Append(", pusty po zamku ").Append(_dEmpty)
                  .Append(", wylaczone w drodze ").Append(_dOff).Append(", inny zamek ").Append(_dWrong)
                  .Append(" | pierwszy przystanek: wozow z jedzeniem ").Append(_dAsk).Append(", skierowane do zamku ").Append(_dRouted).Append(" (").Append(_dRoutedUnits)
                  .Append(" szt.), zamek mial dosc ").Append(_dFull).Append(", odmowy przy wyborze (brak Spendable) ").Append(_dRouteNoCoin)
                  .Append(", w drodze do zamku teraz ").Append(_bound.Count).Append(", rezerwacje wygasle ").Append(_dStale)
                  .Append(" | zamki z polka < ").Append(days.ToString("0.#", Inv)).Append(" dni zuzycia podzamcza (popyt BK): ").Append(low).Append(" z ").Append(castles)
                  .Append(" (mediana ").Append(med).Append(" dni; bez popytu ").Append(noDemand).Append("), budzet jedzenia podzamczy ").Append(budgetAll.ToString("0", Inv))
                  .Append(" zl/d, jedzenie na polkach zamkow ").Append(shelfUnits).Append(" szt.")
                  .Append(" | jedzenie okregu dla zamkow: danina w naturze ").Append(tribute.ToString("0.#", Inv)).Append(" Food/d (E4/11, spichlerz zalogi) + woz ")
                  .Append(_dSoldUnits).Append(" szt. (polka podzamcza)")
                  .Append(" | BK odeslanie do domu wstrzymane ").Append(_dBkHeld).Append(", wrocily do domu z ladunkiem po zamku ").Append(_dHomeLoad)
                  .Append(" | powtorne wizyty bez znacznika: ").Append(_dRepeat);
                if (_dBudgetBk > 0) sb.Append(" | budzet BK z bledem (model gry) ").Append(_dBudgetBk);
                sb.Append(" | czas ").Append(ms.ToString("0.0", Inv)).Append(" ms");
                if (_stumbles > 0) sb.Append(" | POTKNIECIA ").Append(_stumbles);
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("CastleMarket.Daily", e); }
            finally { ClearDay(); }
        }

        // ------------------------------------------------------------ wpiecie i linia startowa
        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>(); var miss = new List<string>();
            MethodInfo sell = null, tick = null;
            try
            {
                sell = AccessTools.Method(typeof(SellGoodsForTradeAction), "ApplyInternal");
                var ps = sell != null ? sell.GetParameters() : null;
                if (ps == null || ps.Length < 2 || ps[0].ParameterType != typeof(Settlement) || ps[1].ParameterType != typeof(MobileParty))
                {
                    miss.Add("SellGoodsForTradeAction.ApplyInternal(Settlement, MobileParty, ..)");
                    sell = null;
                }
                else
                {
                    // High: po prefiksach First (ramka ksiegi towarow GoodsLedger.WagonPre, migawka ceny 119 MarketCarts.SellPrefix), przed BK SellGoodsPatch (Normal, tylko miasta)
                    h.Patch(sell, prefix: new HarmonyMethod(typeof(CastleMarket), nameof(SellPrefix)) { priority = Priority.High });
                    _wiredSell = true;
                    done.Add("sprzedaz w zamku (SellGoodsForTradeAction.ApplyInternal, prefiks High)");
                }
            }
            catch (Exception e) { miss.Add("sprzedaz w zamku (" + e.Message + ")"); Log.Error("CastleMarket.ApplyAll(sprzedaz)", e); }
            try
            {
                tick = AccessTools.Method("BannerKings.Behaviours.BKPartyBehavior:VillagersCastleTick");
                if (tick == null) miss.Add("BK BKPartyBehavior.VillagersCastleTick");
                else
                {
                    h.Patch(tick, prefix: new HarmonyMethod(typeof(CastleMarket), nameof(CastleTickPrefix)) { priority = Priority.First });
                    _wiredGuard = true;
                    done.Add("straz przed odeslaniem do domu (BK BKPartyBehavior.VillagersCastleTick, prefiks)");
                }
            }
            catch (Exception e) { miss.Add("straz BK (" + e.Message + ")"); Log.Error("CastleMarket.ApplyAll(straz)", e); }
            _missing = miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-";
            try
            {
                var s = Settings.Current;
                Log.Info("CastleMarket (E5/B4): targ przy zamku - wpiete: " + (done.Count > 0 ? string.Join(", ", done.ToArray()) : "nic") + "; BRAK: " + _missing
                         + "; wlasciciele latek celow: sprzedaz " + Owners(sell) + "; straz " + Owners(tick)
                         + "; pierwszy przystanek w MarketRoad.RoutePrefix (po wyborze miasta), drugi odcinek tym samym MarketRoad.PickTown"
                         + "; zuzycie podzamcza = popyt BK (budzet dnia kategorii jedzenia / cena targu zamku), kazda kategoria osobno; zakup tylko z CastlePurse.Spendable"
                         + "; MoneyLedger bez nowych pozycji (kasa zamku -> sakwa taboru w oknie wjazdu taboru)"
                         + (s != null ? "; ustawienia startowe przed MCM (stan - linia dzienna): Castle Market First " + (s.CastleMarketFirst ? "TAK" : "NIE")
                                        + ", dni " + Days(s).ToString("0.#", Inv) : "") + ".");
            }
            catch (Exception e) { Log.Error("CastleMarket.ApplyAll(linia)", e); }
        }

        private static string Owners(MethodBase m)
        {
            try
            {
                if (m == null) return "brak metody";
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return "bez latek";
                var ow = new List<string>();
                foreach (var o in info.Owners) if (!ow.Contains(o)) ow.Add(o);
                return ow.Count > 0 ? string.Join(" + ", ow.ToArray()) : "bez latek";
            }
            catch (Exception e) { return "blad (" + e.GetType().Name + ")"; }
        }
    }
}
