using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// CENA SUROWCOW OD NIEDOBORU (Jeff 06.10 po pierwszym tescie: "to znaczy, ze ruda jest zle wyceniona, bo gdyby w miastach
    /// nie bylo rudy, kosztowalaby krocie").
    /// Pomiar (log 06.10 14:08, 20 dob): 69 z 97 miast bez rudy, a miasta z brakiem placa za nia 10.4 d przy wartosci 8 d;
    /// drewno 1.3 d przy wartosci 4 d. Wzor gry (DefaultTradeItemPriceFactorModel.GetBasePriceFactor):
    ///   indeks = (popyt / (0.1 x podaz + 0.04 x wartosc polki + 2)) ^ 0.6, towary handlowe w granicach 0.1-10.
    /// Dwie przyczyny:
    ///  (A) STALA "+2" JEST W STAREJ MONECIE. Popyt i wartosc polki sa po przeliczeniu w pensach (HistoricalPrices), stala
    ///      zostala: wazy tyle, co cwierc ladunku rudy, pol ladunku drewna albo cala miara soli. Przy pustej polce indeks to
    ///      (popyt / 2) ^ 0.6: ruda 1.5, drewno 0.8, sol 2.6, piwo 3.8 - w grze bez przeliczenia kazdy z nich mial 10.
    ///      Naprawa: wzor dostaje swoje trzy wielkosci w monecie, w ktorej napisano jego stala (x przelicznik kategorii z
    ///      HistoricalPrices), czyli stala wazy 2 / przelicznik nowych denarow. Dla towaru o jednym przedmiocie to dokladnie
    ///      cena gry sprzed przeliczenia. Kazda kategoria towarow handlowych, ktora w nowej monecie POTANIALA (przelicznik
    ///      ponad 1) - bez listy. Kategorii, ktore podrozaly (futro 1 -> 200 d, miod pitny 1 -> 10, marmur 1 -> 2, atrament
    ///      2 -> 100, bawelna, aksamit, klejnoty, welna 22 -> 83, len 15 -> 20), nie ruszamy: tam 2 nowe denary waza mniej niz
    ///      w grze, a przeliczenie zrobiloby ze stalej 2 futra, 2 miary miodu albo cala sztuke atramentu - ten sam blad w druga
    ///      strone (proba recenzenta: pusta polka miodu, marmuru i atramentu 10 -> 7.5, welna w miescie bez welny ponizej
    ///      wartosci bazowej). Regula: stala nigdy nie wazy wiecej niz 2 nowe denary i nigdy wiecej niz w grze.
    ///  (B) POPYT TO SAMI MIESZCZANIE. Gra liczy popyt z dobrobytu (BaseDemand), a mieszczanie rudy nie kupuja wcale
    ///      (TownUseIron = 0), lnu, welny i skor surowych 2%. Zuzycie rzemieslnikow i warsztatow do popytu nie wchodzilo:
    ///      cena nie wiedziala, ze miastu z kuznia rudy brakuje, za to welna byla droga wszedzie, takze tam, gdzie nikt jej nie
    ///      przedzie. Naprawa: dla siedmiu surowcow masowych z tabeli karawan popyt miasta = jego PRAWDZIWE zuzycie dobowe:
    ///      mieszczanie (popyt gry x czesc, ktora naprawde kupuja - TownUse*) + rzemioslo (CaravanBulk.Usage - to samo zuzycie,
    ///      z ktorego karawany licza zapas docelowy: rece rzemieslnikow, garbowanie i tkanie, linie towarowe warsztatow - x wartosc
    ///      sztuki). Wpiete w szacunek popytu, z ktorego gra co dobe wygladza popyt rynku (15% dziennie) - budzetu zakupow
    ///      mieszczan to nie rusza (ten idzie z GetDailyDemandForCategory).
    /// Ksztalt (ten sam wzor gry, teraz w dobach zuzycia): polka pusta od dawna - sufit gry x10; za pierwszy dowieziony ladunek
    /// rudy miasto-mediana placi 37 d (x4.6), miasto z kuznia 74 d (x9); zapas na 7 dob = wartosc bazowa; zapas docelowy karawan
    /// (10 dob) x0.82; prog nadwyzki (20 dob) x0.54; 100 dob x0.2. Swiezo dowieziony towar zbija cene slabiej (0.04) niz zapas,
    /// ktory lezy (0.14) - dostawa calego zapasu docelowego idzie srednio po x2.5 wartosci.
    /// Sufit x10 zostaje z gry - jeden dla wszystkich towarow handlowych. Historycznie (woz ok. 1.5 d za tone na mile, Anglia
    /// XIV w. - szacunek): rudzie po 75 d za tone droga ladowa doklada jej wlasna wartosc co ok. 80 km, drewnu po 35 d co
    /// ok. 37 km - x10 to ruda wieziona ok. 700 km i drewno ok. 330 km; dalej nikt masowego towaru wozem nie wiozl.
    /// Warsztaty zbrojne placa za rude i drewno cene targu (WorkshopLaw.MatPrice), nie stala cene historyczna - inaczej drogi
    /// surowiec oplacalaby kasa miasta, a w jednym miescie byly dwie ceny rudy.
    /// Nowa kampania: popyt i podaz wygladzone przez ticki startowe sa jeszcze w starej monecie (przeliczenie wchodzi dopiero
    /// w OnSessionLaunched) - przeliczamy je raz, zaraz po StartStock; wczytany zapis dochodzi sam (15% dziennie).
    /// Sama cena niczego nie tworzy: zmienia sie tylko to, ile zlota przechodzi miedzy kasa miasta, kiesa karawany, taborem
    /// wsi i warsztatem.
    /// </summary>
    internal static class RawPrice
    {
        /// <summary>(A) stala wzoru ceny w nowej monecie. Wymaga popytu w nowej monecie (HistDemandScaling) - inaczej popyt bylby przeliczony dwa razy.</summary>
        internal static bool FormulaOn { get { var s = Settings.Current; return s != null && s.PriceFormulaInNewCoin && s.HistDemandScaling && HistoricalPrices.Applied; } }

        /// <summary>(B) popyt surowcow masowych z prawdziwego zuzycia miasta. Tabela zuzycia jest w ladunkach i pensach, a popyt
        /// mieszczan musi byc w tej samej monecie co zuzycie rzemiosla - tylko przy cenach historycznych i HistDemandScaling.</summary>
        internal static bool UseOn { get { var s = Settings.Current; return s != null && s.RawPriceByUse && s.HistDemandScaling && HistoricalPrices.Applied; } }

        // Stanu kampanii tu nie ma (popyt i podaz siedza w danych rynku gry i w sejwie). Zatrzaski bledow: wyjatek idzie do
        // logu raz na kampanie, a wycena wraca wtedy do wzoru gry; liczniki wpiecia sa na cale uruchomienie gry.
        private static bool _errFactor, _errUse, _errLog;
        private static int _factorModels, _demandModels;

        /// <summary>Nowa gra albo wczytanie (z HistoricalPrices.Reset, czyli z konstruktora ArmouryBehavior): bledy nowej kampanii znow ida do logu.</summary>
        internal static void Reset() { _errFactor = false; _errUse = false; _errLog = false; }

        // ------------------------------------------------------------ (A) stala wzoru w nowej monecie
        // BetterEconomy (BEE_ItemPriceFactorModel) wola w srodku model gry - przelicza tylko najbardziej zewnetrzne wywolanie.
        // Metoda jest wolana przy kazdej wycenie (karawany licza nia kazde miasto i kazdy towar): prefiks ma byc tani - zadnych
        // alokacji, jeden odczyt slownika. Pomiar poza gra na prawdziwym modelu: wycena 17 ns, z latka 34 ns (przez model w
        // modelu 43 ns), z wylaczonym wlacznikiem 25 ns - przy 2 mln wycen na dobe to ok. 50 ms.
        [ThreadStatic] private static int _depth;

        /// <summary>Prefiks GetBasePriceFactor(kategoria, wartosc polki, podaz, popyt, sprzedaz, wartosc sprzedawanej sztuki).</summary>
        public static void FactorPrefix(ItemCategory __0, ref float __1, ref float __2, ref float __3, bool __4, ref int __5)
        {
            _depth++;
            if (_depth > 1) return;
            try
            {
                if (__0 == null || !__0.IsTradeGood) return;      // uzbrojenie ma wlasne prawo podazy i popytu (SupplyDemand)
                var s = Settings.Current;
                if (s == null || !s.PriceFormulaInNewCoin || !s.HistDemandScaling) return;
                float r = HistoricalPrices.CoinRatio(__0);        // 0 = kategoria nieprzeliczona albo przeliczenie sesji jeszcze nie weszlo
                if (r <= 1f) return;                              // towar, ktory podrozal: 2 nowe denary waza przy nim mniej niz w grze - stala zostaje
                if (__4) { __1 += __5; __5 = 0; }                 // gra robi to samo: przy sprzedazy polka rosnie o sprzedawana sztuke
                __1 *= r; __2 *= r; __3 *= r;
            }
            catch (Exception e)
            {
                if (!_errFactor) { _errFactor = true; Log.Error("RawPrice.FactorPrefix", e); }   // raz na kampanie; cena zostaje wedle gry
            }
        }

        /// <summary>Finalizer biegnie zawsze (takze po wyjatku modelu) - licznik zagniezdzenia nie zostanie zawyzony.</summary>
        public static Exception FactorFinalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }

        // ------------------------------------------------------------ (B) popyt z prawdziwego zuzycia
        /// <summary>Popyt dobowy miasta na surowiec masowy w monecie = mieszczanie + rzemioslo.
        /// Mieszczanie: popyt gry (dobrobyt + 1000, jak w szacunku gry; juz w nowej monecie) x czesc, ktora naprawde kupuja
        /// (TownUse* - ten sam mnoznik, ktorym BudgetPostfix tnie ich budzet; ruda 0, len, welna i skory surowe 2%).
        /// Rzemioslo: zuzycie dobowe z tabeli karawan bez jej stalej czesci domowej (rece rzemieslnikow, garbowanie i tkanie,
        /// linie towarowe warsztatow) x wartosc sztuki. Ujemny = kategoria spoza tabeli albo zamek (zostaje popyt gry).</summary>
        internal static float UseDemand(Town town, ItemCategory cat, out float home, out float craft)
        {
            home = 0f; craft = 0f;
            ItemObject item; float use, fix; int keep;
            if (!CaravanBulk.Usage(town, cat, out item, out use, out fix, out keep)) return -1f;
            var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SettlementEconomyModel : null;
            float game = model != null ? model.GetDailyDemandForCategory(town, cat, 1000) : 0f;
            home = Math.Max(0f, game) * Math.Max(0f, HistoricalPrices.HouseShare(cat));
            craft = Math.Max(0f, use - fix) * Math.Max(1, item.Value);
            return home + craft;
        }

        /// <summary>Postfiks GetEstimatedDemandForCategory(miasto, dane rynku, kategoria): z tego szacunku gra co dobe wygladza popyt
        /// rynku (ItemConsumptionBehavior.UpdateSupplyAndDemand). Wynik podmieniamy liczba policzona od zera (nie z __result),
        /// wiec model w modelu (BetterEconomy wola model gry) nie liczy niczego dwa razy.</summary>
        public static void EstimatePostfix(Town __0, ItemCategory __2, ref float __result)
        {
            try
            {
                if (__0 == null || __2 == null || !UseOn) return;
                float home, craft;
                float d = UseDemand(__0, __2, out home, out craft);
                if (d >= 0f) __result = d;
            }
            catch (Exception e)
            {
                if (!_errUse) { _errUse = true; Log.Error("RawPrice.EstimatePostfix", e); }      // raz na kampanie; popyt zostaje wedle gry
            }
        }

        // ------------------------------------------------------------ nowa kampania: pamiec rynku w nowej monecie
        /// <summary>
        /// Wolane z OnSessionLaunched po HistoricalPrices.Apply i StartStock.Run. Tylko w sesji, ktora zalozyla kampanie: ticki
        /// startowe (ItemConsumptionBehavior.OnNewGameCreatedFollowUpEnd - 10 przebiegow wygladzania) zapisaly popyt i podaz w
        /// starej monecie i starych sztukach; bez przeliczenia pierwsze dwa-trzy tygodnie kampanii mialyby ceny z mieszanki monet
        /// (log 14:08: cena zbytu rudy 29.9 d w pierwszej dobie, 10.4 d w dwudziestej - bez zadnej zmiany na polkach).
        /// Podaz = wartosc polki w tej chwili (z rostera, w nowych cenach), popyt = szacunek czynnego modelu (juz w nowej monecie).
        /// Dotyczy kategorii, ktorych cene ta paczka liczy inaczej: przeliczonych towarow handlowych (A) i surowcow masowych (B).
        /// </summary>
        internal static void SeedNewCampaign()
        {
            try
            {
                if (Campaign.Current == null || !HistoricalPrices.Applied) return;
                bool fresh = false;
                try { fresh = Campaign.Current.CampaignGameLoadingType == Campaign.GameLoadingType.NewCampaign; } catch { }
                if (!fresh) return;
                bool formula = FormulaOn, use = UseOn;
                if (!formula && !use) return;
                var cats = new HashSet<ItemCategory>();
                if (formula) foreach (var c in HistoricalPrices.RepricedCategories()) if (c != null && c.IsTradeGood && HistoricalPrices.CoinRatio(c) > 1f) cats.Add(c);   // te same kategorie, co w FactorPrefix
                if (use) foreach (var it in CaravanBulk.Items()) if (it.ItemCategory != null) cats.Add(it.ItemCategory);
                var model = Campaign.Current.Models != null ? Campaign.Current.Models.SettlementEconomyModel : null;
                if (cats.Count == 0 || model == null) return;
                int places = 0, cells = 0, stumbles = 0;
                var worth = new Dictionary<ItemCategory, float>();
                foreach (var st in Settlement.All)
                {
                    try
                    {
                        var town = st != null ? st.Town : null;      // miasta i zamki - jedne i drugie maja dane rynku
                        if (town == null || town.MarketData == null || st.ItemRoster == null) continue;
                        worth.Clear();
                        var shelf = st.ItemRoster;
                        for (int i = 0; i < shelf.Count; i++)
                        {
                            var el = shelf.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            var cat = it != null ? it.ItemCategory : null;
                            if (cat == null || el.Amount <= 0 || !cats.Contains(cat)) continue;
                            float w; worth.TryGetValue(cat, out w); worth[cat] = w + (float)el.Amount * it.Value;   // jak TownMarketData: sztuki x Value
                        }
                        foreach (var cat in cats)
                        {
                            float supply; worth.TryGetValue(cat, out supply);
                            float demand = model.GetEstimatedDemandForCategory(town, town.MarketData.GetCategoryData(cat), cat);
                            town.MarketData.SetSupplyDemand(cat, Math.Max(0.1f, supply), Math.Max(0f, demand));     // 0.1 = podloga podazy gry
                            cells++;
                        }
                        places++;
                    }
                    catch (Exception e) { if (stumbles++ < 1) Log.Error("RawPrice.SeedNewCampaign (osada)", e); }
                }
                Log.Info("RawPrice: nowa kampania - popyt i podaz wygladzone przez ticki startowe przeliczone na nowa monete w " + places
                         + " miastach i zamkach, " + cats.Count + " kategorii (" + cells + " pozycji): podaz = wartosc polki teraz, popyt = szacunek modelu"
                         + (use ? ", dla surowcow masowych w miastach z prawdziwego zuzycia" : "") + "; potkniecia " + stumbles + ".");
            }
            catch (Exception e) { Log.Error("RawPrice.SeedNewCampaign", e); }
        }

        // ------------------------------------------------------------ log dnia
        private static string F(float v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }

        private static float Mid(List<float> l)
        {
            if (l.Count == 0) return 0f;
            l.Sort();
            return l.Count % 2 == 1 ? l[l.Count / 2] : (l[l.Count / 2 - 1] + l[l.Count / 2]) * 0.5f;
        }

        /// <summary>Raz na dobe, zaraz po liniach "Ruda:" i "Drewno:": tylko odczyt (97 miast x 7 surowcow). Biegnie takze przy
        /// wylaczonych wlacznikach - wtedy pokazuje ceny dotychczasowe i popyt, jaki dalaby zmiana.</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null || !HistoricalPrices.Applied) return;
                var items = CaravanBulk.Items();
                if (items.Count == 0) return;
                var econ = Campaign.Current.Models != null ? Campaign.Current.Models.SettlementEconomyModel : null;
                var price = Campaign.Current.Models != null ? Campaign.Current.Models.TradeItemPriceFactorModel : null;
                int day = (int)CampaignTime.Now.ToDays - 1;
                var sb = new StringBuilder();
                var idx = new List<float>(); var bare = new List<float>(); var glut = new List<float>();
                var home = new List<float>(); var craft = new List<float>(); var stored = new List<float>(); var game = new List<float>();
                foreach (var it in items)
                {
                    var cat = it.ItemCategory;
                    if (cat == null) continue;
                    idx.Clear(); bare.Clear(); glut.Clear(); home.Clear(); craft.Clear(); stored.Clear(); game.Clear();
                    var el = new EquipmentElement(it);
                    foreach (var t in Town.AllTowns)
                    {
                        if (t == null || t.Owner == null || t.Owner.ItemRoster == null || t.MarketData == null) continue;
                        ItemObject same; float use, fix, h, c; int keep;
                        if (!CaravanBulk.Usage(t, cat, out same, out use, out fix, out keep) || UseDemand(t, cat, out h, out c) < 0f) continue;
                        int n = t.Owner.ItemRoster.GetItemNumber(it);
                        idx.Add(t.GetItemCategoryPriceIndex(cat));
                        if (n <= 0) bare.Add(t.GetItemPrice(el, null, true));          // tyle miasto zaplaci za pierwsza dowieziona sztuke
                        else if (n > keep) glut.Add(t.GetItemPrice(el, null, false));  // tyle kosztuje sztuka tam, gdzie karawany kupuja
                        home.Add(h); craft.Add(c);
                        stored.Add(t.MarketData.GetDemand(cat));
                        if (econ != null) game.Add(econ.GetDailyDemandForCategory(t, cat, 1000));   // szacunek gry sprzed tej zmiany (caly popyt mieszczan, nowa moneta)
                    }
                    if (idx.Count == 0) continue;
                    idx.Sort();
                    int nb = bare.Count, ng = glut.Count;
                    sb.Append(sb.Length > 0 ? "; " : "").Append(CaravanBulk.NameOf(it)).Append(" (wartosc ").Append(it.Value).Append(" d): indeks min/mediana/max ")
                      .Append(F(idx[0], "0.00")).Append('/').Append(F(Mid(idx), "0.00")).Append('/').Append(F(idx[idx.Count - 1], "0.00"))
                      .Append(", bez towaru ").Append(nb).Append(" miast").Append(nb > 0 ? " - za pierwsza sztuke placa " + F(Mid(bare), "0.#") + " d" : "")
                      .Append(", z nadwyzka ").Append(ng).Append(" miast").Append(ng > 0 ? " - sprzedaja po " + F(Mid(glut), "0.#") + " d" : "")
                      .Append(", popyt dobowy miasta: mieszczan ").Append(F(Mid(home), "0.#")).Append(" d + rzemiosla ").Append(F(Mid(craft), "0.#"))
                      .Append(" d, w danych rynku ").Append(F(Mid(stored), "0.#")).Append(" d, sam szacunek gry ").Append(F(Mid(game), "0.#")).Append(" d");
                }
                // przyklady innych przeliczonych towarow handlowych: te sama stala w nowej monecie (A) widac po max indeksu
                var other = new StringBuilder();
                foreach (var id in new[] { "grain", "salt", "beer" })
                {
                    ItemCategory cat = null;
                    foreach (var c in HistoricalPrices.RepricedCategories()) if (c != null && c.StringId == id) { cat = c; break; }
                    if (cat == null) continue;
                    idx.Clear();
                    foreach (var t in Town.AllTowns) if (t != null && t.MarketData != null) idx.Add(t.GetItemCategoryPriceIndex(cat));
                    if (idx.Count == 0) continue;
                    idx.Sort();
                    other.Append(other.Length > 0 ? ", " : "").Append(id).Append(' ').Append(F(idx[0], "0.00")).Append('/').Append(F(Mid(idx), "0.00")).Append('/').Append(F(idx[idx.Count - 1], "0.00"));
                }
                Log.Info("Ceny surowcow: dzien " + day + " - popyt z prawdziwego zuzycia " + (UseOn ? "CZYNNY" : "wylaczony") + ", stala wzoru w nowej monecie "
                         + (FormulaOn ? "CZYNNA" : "wylaczona") + " (model cen " + (price != null ? price.GetType().Name : "?") + ", mediany po miastach; sztuka rudy i drewna = ladunek) - "
                         + sb + (other.Length > 0 ? ". Inne przeliczone towary, indeks min/mediana/max: " + other : "") + ".");
            }
            catch (Exception e)
            {
                if (!_errLog) { _errLog = true; Log.Error("RawPrice.Daily", e); }
            }
        }

        // ------------------------------------------------------------ wpiecie
        private static List<Type> ModelsDeclaring(Type baseType, string method)
        {
            var found = new List<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types; } catch { continue; }
                if (types == null) continue;
                foreach (var t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || !baseType.IsAssignableFrom(t)) continue;
                        var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (m != null && !m.IsAbstract) found.Add(t);
                    }
                    catch { }
                }
            }
            return found;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var names = new List<string>();
                foreach (var t in ModelsDeclaring(typeof(TradeItemPriceFactorModel), "GetBasePriceFactor"))
                {
                    try
                    {
                        var m = t.GetMethod("GetBasePriceFactor", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        var p = m.GetParameters();
                        // parametry bierzemy po pozycji - sprawdzamy typy, nie nazwy
                        if (m.ReturnType != typeof(float) || p.Length != 6 || p[0].ParameterType != typeof(ItemCategory) || p[1].ParameterType != typeof(float)
                            || p[2].ParameterType != typeof(float) || p[3].ParameterType != typeof(float) || p[4].ParameterType != typeof(bool) || p[5].ParameterType != typeof(int)) continue;
                        h.Patch(m, prefix: new HarmonyMethod(typeof(RawPrice), nameof(FactorPrefix)), finalizer: new HarmonyMethod(typeof(RawPrice), nameof(FactorFinalizer)));
                        _factorModels++; names.Add(t.Name);
                    }
                    catch (Exception e) { Log.Error("RawPrice.ApplyAll (" + t.FullName + ".GetBasePriceFactor)", e); }
                }
                foreach (var t in ModelsDeclaring(typeof(SettlementEconomyModel), "GetEstimatedDemandForCategory"))
                {
                    try
                    {
                        var m = t.GetMethod("GetEstimatedDemandForCategory", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        var p = m.GetParameters();
                        if (m.ReturnType != typeof(float) || p.Length != 3 || p[0].ParameterType != typeof(Town) || p[2].ParameterType != typeof(ItemCategory)) continue;
                        h.Patch(m, postfix: new HarmonyMethod(typeof(RawPrice), nameof(EstimatePostfix)) { priority = Priority.Last });
                        _demandModels++; names.Add(t.Name);
                    }
                    catch (Exception e) { Log.Error("RawPrice.ApplyAll (" + t.FullName + ".GetEstimatedDemandForCategory)", e); }
                }
                var s = Settings.Current;
                Log.Info("RawPrice: cena surowcow od niedoboru - stala wzoru ceny w nowej monecie wpieta w " + _factorModels + " modelach cen ("
                         + (s != null && s.PriceFormulaInNewCoin ? (s.HistDemandScaling ? "CZYNNA" : "NIECZYNNA - wymaga Hist Demand Scaling") : "wylaczona w ustawieniach")
                         + "), popyt surowcow masowych z prawdziwego zuzycia miasta wpiety w " + _demandModels + " modelach ekonomii osad ("
                         + (s != null && s.RawPriceByUse ? (s.HistDemandScaling ? "CZYNNY" : "NIECZYNNY - wymaga Hist Demand Scaling") : "wylaczony w ustawieniach") + ") [" + string.Join(", ", names.ToArray())
                         + "]; obie czesci dzialaja dopiero po przeliczeniu cen historycznych; stan co dobe w linii \"Ceny surowcow:\".");
            }
            catch (Exception e) { Log.Error("RawPrice.ApplyAll", e); }
        }
    }
}
