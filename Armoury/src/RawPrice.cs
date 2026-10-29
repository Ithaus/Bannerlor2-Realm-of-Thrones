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
    ///      ponad 1) - bez listy. Kategorii, ktore podrozaly (welna 22 -> 83, len 15 -> 20, bawelna, aksamit, bizuteria; od
    ///      paczki "towary w nowej monecie" przelicznik liczy sie od wartosci z definicji przedmiotu, wiec takze futro 125 -> 200,
    ///      jajka 5 -> 8, papirus 60 -> 100 - a miod pitny, marmur, atrament, zloto i klejnoty, ktore BKROTPatch zbil do 1-10 d,
    ///      sa wsrod tanszych), nie ruszamy: tam 2 nowe denary waza mniej niz w grze, a przeliczenie zrobiloby ze stalej 2 futra
    ///      albo pol beli aksamitu - ten sam blad w druga strone (proba recenzenta: pusta polka miodu pitnego, marmuru i atramentu
    ///      10 -> 7.5 przy przelicznikach odwrotnych sprzed tamtej paczki, welna w miescie bez welny ponizej wartosci bazowej).
    ///      Regula: stala nigdy nie wazy wiecej niz 2 nowe denary i nigdy wiecej niz w grze.
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
    /// (10 dob) x0.82; prog nadwyzki (20 dob) x0.54; 100 dob x0.2. Swiezo dowieziony towar zbija cene slabiej niz zapas, ktory lezy
    /// (0.14): w grze 0.04 od razu, od paczki 299 (klucz PriceFlatterLotWalk) 0.021 od razu i reszta przez pamiec podazy (15% dziennie), od paczki 299b
    /// (klucz PriceSlowSupplyMemory) pamiec podazy idzie za polka 6% dziennie, a swiezy towar wazy od razu 0.14 x 0.06 = 0.0084 -
    /// dostawa calego zapasu docelowego idzie srednio po x2.5 wartosci (liczone wagami gry).
    /// Sufit x10 zostaje z gry - jeden dla wszystkich towarow handlowych. Historycznie (woz ok. 1.5 d za tone na mile, Anglia
    /// XIV w. - szacunek): rudzie po 75 d za tone droga ladowa doklada jej wlasna wartosc co ok. 80 km, drewnu po 35 d co
    /// ok. 37 km - x10 to ruda wieziona ok. 700 km i drewno ok. 330 km; dalej nikt masowego towaru wozem nie wiozl.
    /// Warsztaty zbrojne placa za rude i drewno cene targu (WorkshopLaw.MatPrice), nie stala cene historyczna - inaczej drogi
    /// surowiec oplacalaby kasa miasta, a w jednym miescie byly dwie ceny rudy.
    /// Nowa kampania: popyt i podaz wygladzone przez ticki startowe sa jeszcze w starej monecie (przeliczenie wchodzi dopiero
    /// w OnSessionLaunched) - przeliczamy je raz, zaraz po StartStock, w miastach (zamkow gra nie wygladza - ich popyt jest 0
    /// i tak zostaje); wczytany zapis dochodzi sam (15% dziennie).
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
        // Audyt 120: kazde potkniecie liczone (_st*, linia dnia "Ceny surowcow: ... Potkniecia dzis") - po pierwszym wpisie w logu
        // widac, czy to jeden przypadek, czy kazda wycena. Prefiks ceny moze biec poza watkiem glownym - licznik przez Interlocked.
        private static bool _errFactor, _errUse, _errLog;
        private static int _stFactor, _stUse, _stBudget;
        private static int _factorModels, _demandModels;

        /// <summary>Nowa gra albo wczytanie (z HistoricalPrices.Reset, czyli z konstruktora ArmouryBehavior): bledy nowej kampanii znow ida do logu.</summary>
        internal static void Reset()
        {
            _errFactor = false; _errUse = false; _errLog = false; _errBudget = false; _stFactor = 0; _stUse = 0; _stBudget = 0;
            MemoryLive = false; _errPace = false;   // 299b: ticki startowe nowej kampanii (przed OnSessionLaunched) wygladzaja podaz tempem gry
            lock (_paceLock) { _paceDay = -1; _paceNow = 0; _paceLast = 0; _paceNowGame = 0; _paceLastGame = 0; }
            System.Threading.Interlocked.Exchange(ref _paceBefore, 0); System.Threading.Interlocked.Exchange(ref _stPace, 0);
        }

        /// <summary>NW O4 (ciche logi): doba bez linii "Ceny surowcow" - potkniecia doby odczytane i zerowane jak w Daily (linia "potkniecia dzis" mowi
        /// o jednej dobie); niezerowe - krotka linia z innym poczatkiem (bledy zawsze w logu, narzedzie czyta "Ceny surowcow: dzien" tylko z pelnych dob).</summary>
        internal static void QuietDay()
        {
            int stF = System.Threading.Interlocked.Exchange(ref _stFactor, 0), stU = System.Threading.Interlocked.Exchange(ref _stUse, 0);
            int stB = _stBudget; _stBudget = 0;
            int stS = HistoricalPrices.TakeShelfStumbles();
            if (stF + stU + stB + stS > 0)
                Log.Info("Ceny surowcow (ciche logi): dzien " + ((int)CampaignTime.Now.ToDays - 1) + " - potkniecia dzis (wyjatki, pierwszy w logu): wycena " + stF
                         + ", popyt " + stU + ", budzet BK " + stB + ", wagi polki " + stS + ".");
        }

        // ------------------------------------------------------------ (A) stala wzoru w nowej monecie
        // BetterEconomy (BEE_ItemPriceFactorModel) wola w srodku model gry - przelicza tylko najbardziej zewnetrzne wywolanie.
        // Metoda jest wolana przy kazdej wycenie (karawany licza nia kazde miasto i kazdy towar): prefiks ma byc tani - zadnych
        // alokacji, jeden odczyt slownika. Pomiar poza gra na prawdziwym modelu: wycena 17 ns, z latka 34 ns (przez model w
        // modelu 43 ns), z wylaczonym wlacznikiem 25 ns - przy 2 mln wycen na dobe to ok. 50 ms.
        [ThreadStatic] private static int _depth;

        // ------------------------------------------------------------ paczka 299: plaski krok ceny na sztuke (Jeff 29.10: "za bardzo spada cena jak sprzedaje towar,
        // trzeba bardziej wyplaszczyc zmiane kursu towaru gdy jest sprzedaz i zakup"). Wzor gry: (D / (0.1 S + 0.04 I + 2))^0.6 - polka, ktora lezy (S = I), wazy
        // razem 0.14, z czego swiezo polozona sztuka od razu 0.04 (DefaultTradeItemPriceFactorModel.GetBasePriceFactor 1.4.8), a reszta dochodzi przez pamiec podazy
        // 15% dziennie (DefaultSettlementEconomyModel.GetSupplyDemandForCategory: 0.85 stara + 0.15 polka; BEE deleguje do gry, BK tylko tnie podaz do popytu x rok).
        // Regula 299: swiezy towar wazy od razu tyle, ile przepuszcza jedna doba tego wygladzania gry: 0.14 x 0.15 = 0.021, podaz 0.14 - 0.021 = 0.119. Przy S = I
        // mianownik ten sam co w grze (0.14 I + 2) - ceny ustalone, "7 dob popytu na polce = indeks 1" i kalibracja startu bez zmian; krok na sztuke ok. 1.9 x
        // mniejszy; cena zalezy tylko od stanu polki, podazy i popytu - kupno i odsprzedaz w jednej wizycie nie daja zysku. Zmienia sie tylko cena (nic z niczego).
        internal const double GameShelfW = 0.04, GameSupplyW = 0.10, GameDayW = 0.15;   // GetBasePriceFactor; dzienne wygladzanie podazy gry
        internal const double LotShelfW = (GameShelfW + GameSupplyW) * GameDayW;     // 0.021
        internal const double LotSupplyW = GameShelfW + GameSupplyW - LotShelfW;      // 0.119
        private const float KShelf = (float)(LotShelfW / GameShelfW), KSupply = (float)(LotSupplyW / GameSupplyW);  // 0.525 / 1.19
        /// <summary>Krzywa gry przy tym samym stanie rynku (wagi 0.04 / 0.1) - ustawiana tylko w try/finally na watku wolajacego: sonda 299 (PriceWalk299) i
        /// recenzja 299 - CaravanTrade284.DepthNet (kolejka towaru, ktory laduje na polce przez odcinek drogi: po jednej dobie pamieci podazy 299 waga swiezego towaru
        /// to 0.021 + 0.119 x 0.15 = 0.039 - krzywa gry przy migawce jest krzywa 299 dobe po dostawie; brama 294b zostaje tak, jak ja skalibrowano na 296e).</summary>
        [ThreadStatic] internal static bool GameCurve;
        internal static bool LotWalkOn { get { var s = Settings.Current; return s != null && s.PriceFlatterLotWalk; } }

        // ------------------------------------------------------------ paczka 299b (Jeff 29.10 ok. 20:20, o lupie i sprzecie: "tak dla wszysatkich - 15% dziennie za duzo, dajmy 5-8%
        // dziennie. Czemu 15%?"). 15% to stala gry, nie nasza: DefaultSettlementEconomyModel.GetSupplyDemandForCategory 1.4.8 - podaz = max(0.1, 0.85 stara + 0.15 polka),
        // popyt = 0.85 stary + 0.15 szacunek; paczka 299 tylko z niej wziela wage swiezego towaru (0.14 x 0.15). Klucz PriceSlowSupplyMemory: PODAZ idzie za polka w tempie Pace
        // (PriceSupplyMemoryDaily, domyslnie 0.06 - decyzja sesji glownej w pasmie Jeffa 5-8%), POPYT bez zmian 0.15 (popyt nie bierze sie z handlu, tylko z dobrobytu i zuzycia;
        // FoodSubstitute.Alpha, HarvestFairs.Alpha, NoMakerBudget.Alpha czytaja Item2 jako wage dzisiejszego popytu 0.15). Waga swiezego towaru ta sama regula co 299: 0.14 x tempo
        // (0.0084), podaz 0.14 - 0.0084 = 0.1316; przy S = I mianownik dalej 0.14 I + 2 - ceny ustalone bez zmian. Klucz PriceFlatterAllItems: to samo prawo dla sprzetu i lupu
        // (ShelfMemory - pamiec polki koszyka typ x tier w SupplyDemand.Factor) i te same wagi w czynniku gry kategorii spoza towarow handlowych (0.8-1.3: podstawa sprzedazy broni
        // i zbroi, SellByCondition.Fraction). Oba klucze NIE - dokladnie jak w paczce 299.
        internal static bool SlowOn { get { var s = Settings.Current; return s != null && s.PriceFlatterLotWalk && s.PriceSlowSupplyMemory; } }
        internal static bool AllItemsOn { get { var s = Settings.Current; return s != null && s.PriceFlatterLotWalk && s.PriceFlatterAllItems; } }
        /// <summary>Dzienne tempo pamieci podazy: klucz PriceSlowSupplyMemory - PriceSupplyMemoryDaily w granicach 0.02-0.15; inaczej 0.15 gry.</summary>
        internal static double Pace { get { return PaceOf(Settings.Current); } }
        internal static double PaceOf(Settings s)
        {
            if (s == null || !s.PriceFlatterLotWalk || !s.PriceSlowSupplyMemory) return GameDayW;
            float v = s.PriceSupplyMemoryDaily;
            if (!(v >= 0.02f)) return 0.02;          // takze NaN
            return v >= 0.15f ? GameDayW : v;
        }
        /// <summary>299b: true od OnSessionLaunched (ArmouryBehavior) do Reset - 10 przebiegow wygladzania nowej kampanii (ItemConsumptionBehavior.OnNewGameCreatedFollowUpEnd,
        /// Campaign: OnNewGameCreated przed OnSessionStart) zostaje w tempie gry.</summary>
        internal static bool MemoryLive;
        /// <summary>Mnozniki wag polki i podazy w FactorPrefix: 299 - 0.525 / 1.19; 299b - (0.14 x tempo) / 0.04 i (0.14 - 0.14 x tempo) / 0.1.</summary>
        private static void Weights(Settings s, out float ks, out float kp)
        {
            if (!s.PriceSlowSupplyMemory) { ks = KShelf; kp = KSupply; return; }
            double sh = (GameShelfW + GameSupplyW) * PaceOf(s);
            ks = (float)(sh / GameShelfW); kp = (float)((GameShelfW + GameSupplyW - sh) / GameSupplyW);
        }
        /// <summary>Wagi wzoru dla wlasnych kopii wzoru (KsFoodCarts.DestParams, KsFoodCore.LotSteps, StartCalibration.Ramp); klucz wylaczony - dokladnie literaly gry;
        /// 299b (PriceSlowSupplyMemory) - 0.14 x tempo i reszta do 0.14.</summary>
        internal static double ShelfW { get { return LotWalkOn ? (SlowOn ? (GameShelfW + GameSupplyW) * Pace : LotShelfW) : 0.04; } }
        internal static double SupplyW { get { return LotWalkOn ? (SlowOn ? GameShelfW + GameSupplyW - (GameShelfW + GameSupplyW) * Pace : LotSupplyW) : 0.1; } }
        internal static int FactorModels { get { return _factorModels; } }

        /// <summary>Recenzja 299: stala wzoru "+2" po przeliczniku monety, jak w FactorPrefix - (r D / (r X + 2))^e = (D / (X + 2 / r))^e, wiec przy r > 1
        /// (PriceFormulaInNewCoin, HistDemandScaling) stala to 2 / r; inaczej 2. Dla wlasnych kopii wzoru (KsFoodCarts.DestParams, KsFoodCore.LotSteps): wagi 299
        /// polowia wage polki, wiec blad stalej 2 zamiast 2 / r w sztukach rosl 1.9 x. Klucz PriceFlatterLotWalk wylaczony - 2 (jak przed 299, bit w bit).</summary>
        internal static double FormulaConst(ItemCategory cat)
        {
            var s = Settings.Current;
            if (s == null || !s.PriceFlatterLotWalk || !s.PriceFormulaInNewCoin || !s.HistDemandScaling || cat == null) return 2.0;
            float r = HistoricalPrices.CoinRatio(cat);
            return r > 1f ? 2.0 / r : 2.0;
        }

        /// <summary>Prefiks GetBasePriceFactor(kategoria, wartosc polki, podaz, popyt, sprzedaz, wartosc sprzedawanej sztuki).</summary>
        public static void FactorPrefix(ItemCategory __0, ref float __1, ref float __2, ref float __3, bool __4, ref int __5)
        {
            _depth++;
            if (_depth > 1) return;
            try
            {
                if (__0 == null) return;
                if (!__0.IsTradeGood)
                {
                    // uzbrojenie ma wlasne prawo podazy i popytu (SupplyDemand); 299b (klucz PriceFlatterAllItems): czynnik gry kategorii spoza towarow (0.8-1.3 - podstawa
                    // sprzedazy broni i zbroi w lancuchu gry, SellByCondition.Fraction) z tymi samymi wagami co towary - bez przelicznika monety i bez wag kategorii mieszanych
                    var sa = Settings.Current;
                    if (sa != null && sa.PriceFlatterLotWalk && sa.PriceFlatterAllItems && !GameCurve)
                    {
                        float ka, kb; Weights(sa, out ka, out kb);
                        if (__4 && __5 != 0) { __1 += __5; __5 = 0; }
                        __1 *= ka; __2 *= kb;
                    }
                    return;
                }
                // paczka 121: sprzedawana sztuka kategorii mieszanej wazy tyle, ile na polce w danych rynku (HistoricalPrices.ShelfWorth;
                // model zna tylko kategorie i wartosc sztuki) - takze przy wylaczonej stalej wzoru; bez wag = wartosc, jak dotad
                if (__4)
                {
                    int w = HistoricalPrices.ShelfWorthOf(__0, __5);
                    if (w != __5) { __1 += w; __5 = 0; }
                }
                var s = Settings.Current;
                // paczka 299: sprzedawana sztuka dolacza do polki z waga polki, polka 0.525 x, podaz 1.19 x (0.04 -> 0.021, 0.1 -> 0.119; razem 0.14 jak w grze);
                // przed przelicznikiem monety (mnozenie przez r dalej jest przemienne); GetTradePenalty dostaje wartosci oryginalne (ref zmienia tylko to wywolanie)
                // 299b (klucz PriceSlowSupplyMemory): wagi z tempa pamieci podazy (0.0084 / 0.1316 przy 6%); klucz NIE - stale 299 (bit w bit)
                if (s != null && s.PriceFlatterLotWalk && !GameCurve)
                {
                    if (__4 && __5 != 0) { __1 += __5; __5 = 0; }
                    float ks, kp; Weights(s, out ks, out kp);
                    __1 *= ks; __2 *= kp;
                }
                if (s == null || !s.PriceFormulaInNewCoin || !s.HistDemandScaling) return;
                float r = HistoricalPrices.CoinRatio(__0);        // 0 = kategoria nieprzeliczona albo przeliczenie sesji jeszcze nie weszlo
                if (r <= 1f) return;                              // towar, ktory podrozal: 2 nowe denary waza przy nim mniej niz w grze - stala zostaje
                if (__4) { __1 += __5; __5 = 0; }                 // gra robi to samo: przy sprzedazy polka rosnie o sprzedawana sztuke
                __1 *= r; __2 *= r; __3 *= r;
            }
            catch (Exception e)
            {
                System.Threading.Interlocked.Increment(ref _stFactor);
                if (!_errFactor) { _errFactor = true; Log.Error("RawPrice.FactorPrefix", e); }   // raz na kampanie; cena zostaje wedle gry
            }
        }

        /// <summary>Finalizer biegnie zawsze (takze po wyjatku modelu) - licznik zagniezdzenia nie zostanie zawyzony.</summary>
        public static Exception FactorFinalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }

        // ------------------------------------------------------------ 299b: tempo pamieci podazy (postfiks DefaultSettlementEconomyModel.GetSupplyDemandForCategory)
        // BEE_SettlementEconomyModel wola _vanilla (DefaultSettlementEconomyModel), BKEconomyModel wola baze i tnie podaz do popytu x DaysInYear (to ciecie biegnie PO nas) -
        // jeden postfiks na metodzie gry obejmuje kazda droge. Stale 0.85 / 0.15 sa literalami IL - postfiks liczy podaz od nowa z argumentow (nie z __result), popyt zostaje.
        private static readonly object _paceLock = new object();
        private static int _paceDay = -1, _paceNow, _paceLast, _paceNowGame, _paceLastGame, _paceBefore, _stPace, _paceModels;
        private static bool _errPace;
        internal static int PaceModels { get { return _paceModels; } }

        public static void SupplyPacePostfix(Town __0, ItemCategory __1, float __2, float __3, float __4, float __5, ref ValueTuple<float, float> __result)
        {
            try
            {
                double p = PaceOf(Settings.Current);
                bool apply = MemoryLive && p < GameDayW - 1e-6;
                if (!MemoryLive) System.Threading.Interlocked.Increment(ref _paceBefore);
                else NotePace(apply);
                if (!apply) return;
                float pf = (float)p;
                __result = new ValueTuple<float, float>(Math.Max(0.1f, __4 * (1f - pf) + __2 * pf), __result.Item2);
            }
            catch (Exception e)
            {
                System.Threading.Interlocked.Increment(ref _stPace);
                if (!_errPace) { _errPace = true; Log.Error("RawPrice.SupplyPacePostfix", e); }   // raz na kampanie; podaz zostaje wedle gry
            }
        }

        private static void NotePace(bool applied)
        {
            int d = (int)CampaignTime.Now.ToDays;
            lock (_paceLock)
            {
                if (d != _paceDay)
                {
                    bool next = d == _paceDay + 1;
                    _paceLast = next ? _paceNow : 0; _paceLastGame = next ? _paceNowGame : 0;
                    _paceNow = 0; _paceNowGame = 0; _paceDay = d;
                }
                if (applied) _paceNow++; else _paceNowGame++;
            }
        }

        /// <summary>299b (linia doby PriceWalk299b): wygladzenia podazy w ostatniej pelnej dobie - tempem 299b i tempem gry; przed startem sesji (ticki startowe) od Reset;
        /// potkniecia postfiksu od poprzedniego odczytu.</summary>
        internal static void PaceCounts(out int applied, out int game, out int before, out int stumbles)
        {
            int d = (int)CampaignTime.Now.ToDays;
            lock (_paceLock)
            {
                if (_paceDay == d) { applied = _paceLast; game = _paceLastGame; }
                else if (_paceDay == d - 1) { applied = _paceNow; game = _paceNowGame; }
                else { applied = 0; game = 0; }
            }
            before = System.Threading.Interlocked.CompareExchange(ref _paceBefore, 0, 0);
            stumbles = System.Threading.Interlocked.Exchange(ref _stPace, 0);
        }

        // ------------------------------------------------------------ (B) popyt z prawdziwego zuzycia
        /// <summary>Popyt dobowy miasta na surowiec masowy w monecie = mieszczanie + rzemioslo.
        /// Mieszczanie: popyt gry (dobrobyt + 1000, jak w szacunku gry; juz w nowej monecie) x czesc, ktora naprawde kupuja
        /// (TownUse* - ten sam mnoznik, ktorym BudgetPostfix tnie ich budzet; ruda 0, len, welna i skory surowe 2%).
        /// Rzemioslo: zuzycie dobowe z tabeli karawan bez jej stalej czesci domowej (rece rzemieslnikow, garbowanie i tkanie,
        /// linie towarowe warsztatow) x wartosc sztuki. Ujemny = kategoria spoza tabeli albo zamek (zostaje popyt gry).
        /// f7-browary K2 (klucz ClothDemandFromOfftake): skora i plotno - do tego odbior z polki na odziez wojska, naprawy kowali, zakupy wsi i dwory (F7Workshops.UseOf;
        /// recenzja: wsie i dwory x (1 - lambda) w miastach z koszykiem ZC4 - tam "home" ma juz koszyk wsi QV i 1:1 dworow ze Zc4Demand,
        /// srednia ok. 14 dob) x wartosc sztuki ("off"); sama cena - zapas karawan, progi polki i zamowienia (CaravanBulk.Use) bez zmian. Klucz wylaczony - 0 (jak dotad).
        /// f14-rynek K4 (klucz ClothExportInPrice): do "off" takze wywoz skory i plotna z polki miasta przez kupcow (karawany kupujace w miescie, wozy kupcow innych miast;
        /// F7Workshops.ExportOf, srednia ok. 14 dob) - kupiec placi zrodlu cene jego targu, wiec jego zakup jest popytem tego targu. Klucz wylaczony - 0 (jak dotad).</summary>
        internal static float UseDemand(Town town, ItemCategory cat, out float home, out float craft)
        {
            float off;
            return UseDemand(town, cat, out home, out craft, out off);
        }

        internal static float UseDemand(Town town, ItemCategory cat, out float home, out float craft, out float off)
        {
            home = 0f; craft = 0f; off = 0f;
            ItemObject item; float use, fix; int keep;
            if (!CaravanBulk.Usage(town, cat, out item, out use, out fix, out keep)) return -1f;
            var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SettlementEconomyModel : null;
            float game = model != null ? model.GetDailyDemandForCategory(town, cat, 1000) : 0f;
            home = Math.Max(0f, game) * Math.Max(0f, HistoricalPrices.HouseShare(cat));
            craft = Math.Max(0f, use - fix) * Math.Max(1, item.Value);
            off = (F7Workshops.UseOf(town, item) + F7Workshops.ExportOf(town, item)) * Math.Max(1, item.Value);   // f7-browary K2 + f14-rynek K4 (wywoz kupcow): 0 przy wylaczonych kluczach i dla innych towarow
            return off > 0f ? home + craft + off : home + craft;
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
                System.Threading.Interlocked.Increment(ref _stUse);
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
        /// Kategorie: przy (A) WSZYSTKIE przeliczone towary handlowe - takze te, ktore w nowej monecie podrozaly (futro, welna,
        /// aksamit, jajka), i te, ktore przelicznik maja z wartosci z definicji (chleb, ciasta, owoce, miod - paczka "towary w
        /// nowej monecie"): stala wzoru tych pierwszych nie dotyczy, ale pamiec kazdej z tickow startowych jest w starej monecie
        /// (proba recenzenta 2: aksamit przy 5 sztukach na polce 0.38 zamiast 0.54 w 1. dobie, dochodzi przez 3 tygodnie;
        /// sol 2.25 zamiast 1.83); przy (B) siedem surowcow masowych (welna przy 20 sztukach 0.63 zamiast 0.10).
        /// TYLKO MIASTA (recenzja 2): gra prowadzi dane rynku samych miast (TradeCampaignBehavior.InitializeMarkets, ticki startowe
        /// i dobowe ItemConsumptionBehavior ida po Town.AllTowns). Zamek ma popyt 0 przez cala kampanie (indeks towaru = podloga
        /// 0.1) - nie ma tam czego przeliczac, a wpisany raz popyt zostalby na zawsze (nikt go potem nie wygladza): ceny w zamku
        /// skoczylyby z 0.1 do 10, a targowiska wsi BK (BKBuildingsBehavior: kupuje z polki Bound, gdy podaz > popyt) zaczelyby
        /// wykupywac polki zamkow.
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
                if (formula) foreach (var c in HistoricalPrices.RepricedCategories()) if (c != null && c.IsTradeGood) cats.Add(c);   // kazdy przeliczony towar handlowy (uzbrojenie ma wlasne prawo podazy i popytu)
                if (use) foreach (var it in CaravanBulk.Items()) if (it.ItemCategory != null) cats.Add(it.ItemCategory);
                var model = Campaign.Current.Models != null ? Campaign.Current.Models.SettlementEconomyModel : null;
                if (cats.Count == 0 || model == null) return;
                int places = 0, cells = 0, stumbles = 0;
                var worth = new Dictionary<ItemCategory, float>();
                foreach (var town in Town.AllTowns)      // same miasta - zamkow gra nie wygladza (opis wyzej)
                {
                    try
                    {
                        var shelf = town != null && town.Owner != null ? town.Owner.ItemRoster : null;
                        if (shelf == null || town.MarketData == null) continue;
                        worth.Clear();
                        for (int i = 0; i < shelf.Count; i++)
                        {
                            var el = shelf.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            var cat = it != null ? it.ItemCategory : null;
                            if (cat == null || el.Amount <= 0 || !cats.Contains(cat)) continue;
                            float w; worth.TryGetValue(cat, out w); worth[cat] = w + (float)el.Amount * HistoricalPrices.ShelfWorth(it);   // jak TownMarketData: sztuki x Value (paczka 121: x waga w kategorii mieszanej)
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
                    catch (Exception e) { if (stumbles++ < 1) Log.Error("RawPrice.SeedNewCampaign (miasto)", e); }
                }
                Log.Info("RawPrice: nowa kampania - popyt i podaz wygladzone przez ticki startowe przeliczone na nowa monete w " + places
                         + " miastach (zamki bez zmian - gra nie prowadzi ich danych rynku), " + cats.Count + " kategorii (" + cells + " pozycji): podaz = wartosc polki teraz, popyt = szacunek modelu"
                         + (use ? ", dla surowcow masowych z prawdziwego zuzycia" : "") + "; potkniecia " + stumbles + ".");
            }
            catch (Exception e) { Log.Error("RawPrice.SeedNewCampaign", e); }
        }

        // ------------------------------------------------------------ log dnia
        private static string F(float v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }

        // budzet dnia zakupow mieszczan w kategorii: ta sama metoda, ktora liczy go BK (EconomyPatches.CalculateBudget - z polityka
        // podatkowa osady i z BudgetPostfix Armoury), a bez BK - model gry; popyt dnia bez dodatku dobrobytu (jak UpdateDemandShift)
        private static MethodInfo _bkBudget; private static bool _bkBudgetLooked, _errBudget;
        private static float HouseBudget(Town t, ItemCategory c, SettlementEconomyModel econ)
        {
            if (econ == null) return 0f;
            float dem = econ.GetDailyDemandForCategory(t, c, 0);
            if (!_bkBudgetLooked) { _bkBudgetLooked = true; _bkBudget = AccessTools.Method("BannerKings.Patches.EconomyPatches:CalculateBudget"); }
            if (_bkBudget != null)
            {
                try { return (float)_bkBudget.Invoke(null, new object[] { t, dem, c }); }
                catch (Exception e)
                {
                    // audyt 120: bez globalnego wylacznika - kazde wywolanie probuje BK znowu (blad jednego miasta nie gasi budzetu BK
                    // wszystkim), potkniecia liczone w linii dnia, w logu pierwszy raz na kampanie; ta pozycja - model gry
                    _stBudget++;
                    if (!_errBudget) { _errBudget = true; Log.Error("RawPrice.Daily (budzet BK)", e); }
                }
            }
            return econ.CalculateDailySettlementBudgetForItemCategory(t, dem, c);
        }

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
                var offs = new List<float>();   // f7-browary K2: odbior skory i plotna (wojsko, naprawy, wsie, dwory) w popycie ceny
                foreach (var it in items)
                {
                    var cat = it.ItemCategory;
                    if (cat == null) continue;
                    idx.Clear(); bare.Clear(); glut.Clear(); home.Clear(); craft.Clear(); stored.Clear(); game.Clear(); offs.Clear();
                    var el = new EquipmentElement(it);
                    foreach (var t in Town.AllTowns)
                    {
                        if (t == null || t.Owner == null || t.Owner.ItemRoster == null || t.MarketData == null) continue;
                        ItemObject same; float use, fix, h, c, o; int keep;
                        if (!CaravanBulk.Usage(t, cat, out same, out use, out fix, out keep) || UseDemand(t, cat, out h, out c, out o) < 0f) continue;
                        if (o > 0f) offs.Add(o);
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
                    if (offs.Count > 0) sb.Append(" (f7: odbior wojska i napraw, wsi i dworow poza koszykiem ZC4").Append(F7Workshops.ExpOn ? " i wywoz kupcow (f14 K4)" : "").Append(" w ").Append(offs.Count).Append(" miastach, mediana ").Append(F(Mid(offs), "0.#")).Append(" d - w danych rynku, poza mieszczanami i rzemioslem)");
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
                // paczka "towary w nowej monecie": kategorie z przedmiotem, ktorego wartosc z definicji rozni sie od wartosci w grze
                // (chleb, ciasta, owoce, miod, miod pitny, futro...) - bez listy, wedle HistoricalPrices; przy wylaczonym
                // HistDemandFromDefinition te same kategorie "bez przelicznika" albo z dawnym (porownanie w jednej grze); indeks, sztuki
                // na polkach miast, puste polki i budzet "zakupow" mieszczan (zloto z niczego do kas miast): caly i ile z niego dalo sie
                // dzis wydac przy tej polce
                var defined = new StringBuilder();
                var dcats = HistoricalPrices.DefinedCategories();
                if (dcats.Count > 0)
                {
                    var units = new Dictionary<ItemCategory, int>(); var seen = new Dictionary<ItemCategory, List<string>>();
                    var bareTowns = new Dictionary<ItemCategory, int>(); var worth = new Dictionary<ItemCategory, float>();
                    var budget = new Dictionary<ItemCategory, double>(); var spend = new Dictionary<ItemCategory, double>();
                    foreach (var c in dcats) { units[c] = 0; seen[c] = new List<string>(); bareTowns[c] = 0; budget[c] = 0; spend[c] = 0; }
                    foreach (var t in Town.AllTowns)
                    {
                        if (t == null || t.Owner == null || t.Owner.ItemRoster == null) continue;
                        worth.Clear();
                        var shelf = t.Owner.ItemRoster;
                        for (int i = 0; i < shelf.Count; i++)
                        {
                            var e = shelf.GetElementCopyAtIndex(i);
                            var it = e.EquipmentElement.Item;
                            var c = it != null ? it.ItemCategory : null;
                            if (c == null || e.Amount <= 0 || !units.ContainsKey(c)) continue;
                            units[c] += e.Amount;
                            float w; worth.TryGetValue(c, out w); worth[c] = w + (float)e.Amount * t.GetItemPrice(e.EquipmentElement, null, false);   // polka po cenie, jaka placa mieszczanie
                            var names = seen[c]; string tag = it.StringId + " " + it.Value + " d";
                            if (names.Count < 3 && !names.Contains(tag)) names.Add(tag);
                        }
                        foreach (var c in dcats)
                        {
                            float w; if (!worth.TryGetValue(c, out w)) { w = 0f; bareTowns[c]++; }
                            float b = HouseBudget(t, c, econ);
                            budget[c] += b; spend[c] += Math.Min(b, w);
                        }
                    }
                    foreach (var c in dcats)
                    {
                        idx.Clear();
                        foreach (var t in Town.AllTowns) if (t != null && t.MarketData != null) idx.Add(t.GetItemCategoryPriceIndex(c));
                        if (idx.Count == 0) continue;
                        idx.Sort();
                        defined.Append(defined.Length > 0 ? "; " : "").Append(c.StringId).Append(seen[c].Count > 0 ? " (" + string.Join(", ", seen[c].ToArray()) + ")" : "")
                               .Append(HistoricalPrices.CoinRatio(c) > 0f ? " /" + F(HistoricalPrices.CoinRatio(c), "0.##") : " bez przelicznika").Append(' ')
                               .Append(F(idx[0], "0.00")).Append('/').Append(F(Mid(idx), "0.00")).Append('/').Append(F(idx[idx.Count - 1], "0.00"))
                               .Append(", na polkach ").Append(units[c]).Append(" szt., pusto w ").Append(bareTowns[c]).Append(" miastach, budzet mieszczan ")
                               .Append(F((float)budget[c], "0")).Append(" d (przy dzisiejszych polkach do ").Append(F((float)spend[c], "0")).Append(" d)");
                    }
                }
                // paczka 121: kategorie mieszane (bez listy - wedle HistoricalPrices) - ile miasto placi za sprzedana sztuke przy dzisiejszej
                // polce (mediana miast, x wartosci) i ile sztuk lezy na polkach; przy wylaczonym wlaczniku te same przedmioty bez wag
                var mixed = new StringBuilder();
                foreach (var m in HistoricalPrices.MixedCategories())
                {
                    var p = new List<string>();
                    for (int i = 0; i < m.Items.Count; i++)
                    {
                        var it = m.Items[i]; var el = new EquipmentElement(it);
                        idx.Clear(); int units = 0;
                        foreach (var t in Town.AllTowns)
                        {
                            if (t == null || t.Owner == null || t.Owner.ItemRoster == null || t.MarketData == null) continue;
                            idx.Add(t.GetItemPrice(el, null, true));
                            units += t.Owner.ItemRoster.GetItemNumber(it);
                        }
                        if (idx.Count == 0) continue;
                        float mid = Mid(idx);
                        p.Add(it.StringId + " " + it.Value + " d (waga " + m.Worth[i] + ") - za sztuke " + F(mid, "0") + " d (x" + F(mid / Math.Max(1, it.Value), "0.00") + "), na polkach " + units + " szt.");
                    }
                    if (p.Count > 0) mixed.Append(mixed.Length > 0 ? "; " : "").Append(m.Cat.StringId).Append(" /").Append(F(m.Coin, "0.##")).Append(": ").Append(string.Join(", ", p.ToArray()));
                }
                // audyt 120: potkniecia od poprzedniej linii (zdanie konczy kropka - narzedzie logow czyta liczby potkniec do kropki)
                int stF = System.Threading.Interlocked.Exchange(ref _stFactor, 0), stU = System.Threading.Interlocked.Exchange(ref _stUse, 0);
                int stB = _stBudget; _stBudget = 0;
                int stS = HistoricalPrices.TakeShelfStumbles();
                Log.Info("Ceny surowcow: dzien " + day + " - popyt z prawdziwego zuzycia " + (UseOn ? "CZYNNY" : "wylaczony") + ", stala wzoru w nowej monecie "
                         + (FormulaOn ? "CZYNNA" : "wylaczona") + " (model cen " + (price != null ? price.GetType().Name : "?") + ", mediany po miastach; sztuka rudy i drewna = ladunek) - "
                         + sb + ". Potkniecia dzis (wyjatki, pierwszy w logu): wycena " + stF + ", popyt " + stU + ", budzet BK " + stB + ", wagi polki " + stS
                         // paczka 121: czesc o kategoriach mieszanych PRZED "Inne" i "Towary z wartoscia z definicji" - narzedzie logow czyta te
                         // dwie czesci do konca linii, a odcinek surowcow (do "Inne") przeszukuje wzorcem, ktorego ta czesc nie ma
                         + (mixed.Length > 0 ? ". Kategorie mieszane (waga sztuki na polce " + (HistoricalPrices.MixedActive ? "CZYNNA" : "NIEUZYTA") + "; moneta popytu, miasto placi za sprzedana sztuke przy dzisiejszej polce - mediana miast): " + mixed : "")
                         + (other.Length > 0 ? ". Inne przeliczone towary, indeks min/mediana/max: " + other : "")
                         + (defined.Length > 0 ? ". Towary z wartoscia z definicji przedmiotu (przelicznik popytu, indeks min/mediana/max, sztuki na polkach miast): " + defined : "") + ".");
            }
            catch (Exception e)
            {
                if (!_errLog) { _errLog = true; Log.Error("RawPrice.Daily", e); }
            }
        }

        // ------------------------------------------------------------ wpiecie
        internal static List<Type> ModelsDeclaring(Type baseType, string method)   // P4 (TownFolk.LadderApplyAll) - te same modele szacunku popytu
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
                // 299b: tempo pamieci podazy - jedna metoda gry (DeclaredOnly), przez ktora ida BEE (_vanilla) i BK (wywolanie bazy); typy po pozycji
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementEconomyModel), "GetSupplyDemandForCategory");
                    var p = m != null ? m.GetParameters() : null;
                    if (m != null && m.ReturnType == typeof(ValueTuple<float, float>) && p.Length == 6 && p[0].ParameterType == typeof(Town) && p[1].ParameterType == typeof(ItemCategory)
                        && p[2].ParameterType == typeof(float) && p[3].ParameterType == typeof(float) && p[4].ParameterType == typeof(float) && p[5].ParameterType == typeof(float))
                    {
                        h.Patch(m, postfix: new HarmonyMethod(typeof(RawPrice), nameof(SupplyPacePostfix)) { priority = Priority.First });
                        _paceModels++;
                    }
                    else Log.Info("RawPrice: 299b - DefaultSettlementEconomyModel.GetSupplyDemandForCategory nie znaleziona albo inne typy - podaz zostaje w tempie gry.");
                }
                catch (Exception e) { Log.Error("RawPrice.ApplyAll (GetSupplyDemandForCategory)", e); }
                var s = Settings.Current;
                Log.Info("RawPrice: cena surowcow od niedoboru - stala wzoru ceny w nowej monecie wpieta w " + _factorModels + " modelach cen ("
                         + (s != null && s.PriceFormulaInNewCoin ? (s.HistDemandScaling ? "CZYNNA" : "NIECZYNNA - wymaga Hist Demand Scaling") : "wylaczona w ustawieniach")
                         + "), popyt surowcow masowych z prawdziwego zuzycia miasta wpiety w " + _demandModels + " modelach ekonomii osad ("
                         + (s != null && s.RawPriceByUse ? (s.HistDemandScaling ? "CZYNNY" : "NIECZYNNY - wymaga Hist Demand Scaling") : "wylaczony w ustawieniach") + ") [" + string.Join(", ", names.ToArray())
                         + "]; obie czesci dzialaja dopiero po przeliczeniu cen historycznych; stan co dobe w linii \"Ceny surowcow:\".");
                bool lot = s != null && s.PriceFlatterLotWalk;
                var inv = CultureInfo.InvariantCulture;
                // 299b: wagi z tempa (klucz PriceSlowSupplyMemory); przy kluczu PriceFlatterLotWalk NIE - wagi, ktore wzialby klucz (napis jak w 299)
                double shw = lot ? ShelfW : LotShelfW, spw = lot ? SupplyW : LotSupplyW;
                Log.Info("Ceny na sztuke (299): klucz Price Flatter Lot Walk " + (lot ? "TAK" : "NIE")
                         + " - swiezy towar wazy od razu " + shw.ToString("0.0000", inv) + " (gra 0.040), podaz " + spw.ToString("0.0000", inv)
                         + " (gra 0.100), razem 0.14 jak w grze; przez RawPrice.FactorPrefix w " + _factorModels + " modelach cen; wlasne kopie wzoru: KsFoodCarts.DestParams, KsFoodCore.LotSteps, StartCalibration.Ramp.");
                bool slow = s != null && s.PriceSlowSupplyMemory, all = s != null && s.PriceFlatterAllItems;
                double pace = PaceOf(s);
                Log.Info("Ceny na sztuke (299b): klucze Price Slow Supply Memory " + (slow ? "TAK" : "NIE") + " (tempo " + pace.ToString("0.000", inv) + "/d, gra 0.150), Price Flatter All Items "
                         + (all ? "TAK" : "NIE") + (lot ? "" : " (oba wymagaja Price Flatter Lot Walk - NIECZYNNE)")
                         + " - podaz rynku idzie za polka " + (100.0 * pace).ToString("0.0", inv) + "% dziennie (popyt bez zmian 15%); swiezy towar wazy od razu " + ShelfW.ToString("0.0000", inv)
                         + ", podaz " + SupplyW.ToString("0.0000", inv) + " (razem 0.14); sprzet (bron, zbroje, konie, lup) - pamiec polki koszyka typ x tier, sztuka wazy od razu "
                         + (lot && all ? pace : 1.0).ToString("0.000", inv) + "; wpiete: GetSupplyDemandForCategory w " + _paceModels + " modelu (DefaultSettlementEconomyModel), GetBasePriceFactor wszystkich kategorii w "
                         + _factorModels + " modelach; wlasne kopie wzoru: KsFoodCarts.DestParams, KsFoodCore.LotSteps, StartCalibration.Ramp, SupplyDemand.DailyTradeCore, TradeExportMemo.PickCore.");
            }
            catch (Exception e) { Log.Error("RawPrice.ApplyAll", e); }
        }
    }
}
