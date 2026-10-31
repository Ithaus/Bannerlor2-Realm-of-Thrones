using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// WARSZTATY TOWAROWE W NOWEJ MONECIE (Jeff 06.10 po tescie: "chcialem kupic bakery w Lannisporcie, ale cena to 31 000 -
    /// czy tak ma byc, czy to tyle jest warte, ile to zarabia?").
    ///
    /// Stan przed paczka (kod gry 1.4.8, BK i log testu 06.10 14:08):
    ///  - czynny model warsztatow to NavalDLCWorkshopModel -> DefaultWorkshopModel; BK NIE rejestruje swojego BKWorkshopModel
    ///    (Main.OnGameStart nie ma AddModel WorkshopModel) - jego wzor z "placami x 15 x dni roku" dziala tylko u lordow AI;
    ///  - cena dla gracza = EquipmentCost + 4 x dobrobyt + InitialCapital / 5 (piekarnia w Lannisporcie: 6000 + 4 x 5700 + 2000
    ///    = 30 800), wszystko w STAREJ monecie i bez zwiazku z zarobkiem;
    ///  - wydatek dzienny 100 (stara moneta) schodzi z kapitalu i ZNIKA (Workshop.ChangeGold(-100) bez odbiorcy);
    ///  - warsztat rusza cykl tylko, gdy utarg &gt; wsad + 200 / szybkosc linii (stara moneta): przy cenach HistoricalPrices
    ///    (chleb 6, piwo 3, wino 14, skora 40) piekarnie, browary, winiarnie, garbarnie, garncarnie, olejarnie stoja zawsze;
    ///  - bankructwo: kapital wraca do 10 000 z niczego (ChangeOwnerOfWorkshop(nowy, typ, InitialCapital)); to samo przy
    ///    kupnie, sprzedazy i przejeciu w wojnie; sprzedaz gracza notablowi wyplaca gra z niczego.
    ///
    /// Co robi paczka (wlacznik WorkshopTradeEnabled; dziala tylko przy cenach historycznych):
    ///  1. UTRZYMANIE: kazdy warsztat (poza ukrytymi rzemieslnikami) placi co dobe WorkshopTradeUpkeepPerDay do KASY MIASTA
    ///     (utrzymanie mistrza i czynsz) - z kapitalu, a u gracza ponizej progu z jego kiesy;
    ///  2. PLACE ZA ROBOTE (WorkshopTradeBatchWages): linia towarowa rusza cykl tylko, gdy utarg &gt; wsad + place cyklu, a po
    ///     cyklu warsztat wyplaca te place do kasy miasta. Place cyklu = WorkshopWorkers x WorkshopWagePerDay / suma szybkosci
    ///     linii towarowych typu (zaloga zajeta w calosci, gdy ida wszystkie linie) - ta sama stawka co w warsztatach
    ///     zbrojnych (WorkshopLaw), jedna zasada na place;
    ///  3. KAPITAL: warsztat czysto towarowy startuje z WorkshopTradeStartCapital (nowa moneta); typ z linia uzbrojenia i ukryci
    ///     rzemieslnicy zostaja przy 10 000 gry - tyle kosztuje wykucie najdrozszych sztuk (kropierz kolczy wart 10-11 tys. d), mniej
    ///     zmieniloby po cichu produkcje uzbrojenia. Prog "z kiesy gracza" w nowej monecie. Przy kazdej zmianie wlasciciela kapital
    ///     ZOSTAJE (nic z niczego i nic w nicosc), a po bankructwie nowy wlasciciel wklada kapital z WLASNEJ kiesy;
    ///  4. CENA: wartosc warsztatu = WorkshopTradePriceYears lat jego zysku po podatku, ktory KUPUJACY naprawde zaplaci (notabl:
    ///     2 x stawka BK; gracz i lordowie: 0, dopoki czynnym modelem warsztatow nie jest model BK), ze sredniej kroczacej
    ///     prawdziwego wyniku dnia (pierwszej doby - oczekiwany wynik z cen targu), nie mniej niz wartosc sprzetu; do tego kapital
    ///     w kasie, ktory zostaje w warsztacie. Gracz kupuje wedle lepszej z dwoch pamieci (ok. 30 dob, rok), notabl odkupuje
    ///     od gracza czesc wartosci wedle gorszej - z WLASNEJ kiesy (recenzja: bez okazji na warsztacie, ktory chwilowo stoi);
    ///  5. log dzienny "Warsztaty towarowe:" i linia wyjasnienia ceny w rozmowie kupna (po angielsku);
    ///  6. CENA SPRAWIEDLIWA (paczka 123, wlacznik WorkshopTradeFairPrice): za wyrob cyklu linii towarowej miasto placi warsztatowi
    ///     najwyzej koszt cyklu (wsad wedle ceny miasta + place cyklu) x (1 + SmithProfitPercent) - ta sama marza mistrza co w cenie
    ///     broni (ArmsPricing); nadwyzke ceny (renta z pustej polki, BK placi bez limitu gry 1000) zatrzymuje kasa miasta.
    ///     PACZKA 241 (Jeff 27.10 "czemu moj warsztat prawie nic nie zarabia"; audyt: 567 z 591 cykli przycietych, ok. 90 tys./dobe zostawalo w kasach miast, srebrnicy
    ///     koszt 28.8 tys. a utarg 538 tys.): zamiast kosztu x marza - CENA KUPCA. Kupiec miasta placi za kazda sztuke najwyzej tyle, za ile ja odsprzedaje: srednia
    ///     prawdziwej sprzedazy tego towaru w tym miescie z 28 dob (MarketSales), a bez sprzedazy - cena graniczna ludu (TownFolk.PeopleCeilingIndex). Wytworca dostaje
    ///     cene rynku, wiec brak towaru przyciaga warsztaty; prog cyklu gry i warunek kasy miasta licza utarg ta sama cena (ItemsGatePost, Gate200). Recenzja 241: BK
    ///     placi mimo to pelna cene polki (kasa obcieta do 0 - SettlementComponent.ChangeGold), wiec zloto ponad to, co kasa naprawde stracila, OutPostfix cofa
    ///     z kapitalu warsztatu w nicosc (ClampBack - to zloto powstalo z niczego). Z zaplaty mistrz placi czeladnikom ich udzial (TownFolkCraftWageShare) - doplata ludu N4 z kasy miasta spada sama. SmithProfitPercent juz nie dotyczy
    ///     warsztatow (zostaje w ArmsPricing i TownCrafts).
    /// Linii uzbrojenia (WorkshopLaw), ukrytych rzemieslnikow, receptur i szybkosci NIE rusza. Przy wylaczonym - gra jak dotad.
    /// </summary>
    internal static class WorkshopTrade
    {
        // ------------------------------------------------------------ stan
        private sealed class Rec
        {
            public string Type;                 // typ warsztatu, dla ktorego liczona jest srednia (zmiana typu = od nowa)
            public float Avg;                   // srednia kroczaca wyniku dnia (po placach i utrzymaniu, przed wyplata wlascicielowi i podatkiem)
            public float Slow;                  // to samo z pamiecia roku (recenzja: sprzedajacy pamieta dobre lata, kupujacy zle - miesiac przestoju nie robi z warsztatu okazji)
            public int Days;                    // ile dob weszlo do sredniej
            public int Mem;                     // R3-11b: ile dob w pamieciach od ich punktu wyjscia (dzielnik srednich, okno ceny); = Days, dopoki pamieci nie zaczely od nowa po rozruchu rynku
            public bool Warm;                   // R3-11b: pamieci maja doby rozruchu rynku (WorkshopStartup) - pierwsza doba po rozruchu zostawia w nich 7 ostatnich dob
            public int[] Week;                  // R3-11b: wyniki 7 ostatnich dob rozruchu (WorkshopStartup; [0] = liczba dob, [1..7] = pierscien) - cena w rozruchu; poza nim null
            public int Op;                      // dzis: zmiana kapitalu w RunTownWorkshop (utarg - wsad - place z kapitalu)
            public int Purse;                   // dzis: place cykli zaplacone z kiesy gracza
            public int Wages, Cycles;           // dzis: place za cykle (razem) i liczba cykli towarowych
            public float WageCarry, KeepCarry;  // ulamki pensa do nastepnej wyplaty (gra zna tylko pensy calkowite)
            public int CapEnd; public bool HasEnd;   // kapital po rozliczeniu doby - roznica do nastepnego ticku to wyplata wlascicielowi
        }

        private sealed class TypeStat { public int Shops, Worked, Cycles; public long Plus, Minus, Wages, Keep; }

        private static readonly Dictionary<Workshop, Rec> _rec = new Dictionary<Workshop, Rec>();
        private static readonly Dictionary<string, string[]> _saved = new Dictionary<string, string[]>();   // z zapisu: klucz -> typ, srednia, doby
        private static readonly Dictionary<WorkshopType, float> _tradeSpeed = new Dictionary<WorkshopType, float>();
        private static readonly Dictionary<WorkshopType, float> _tradeSpeedOpen = new Dictionary<WorkshopType, float>();   // 172: bez zamknietej linii strzal
        private static readonly Dictionary<ItemCategory, ItemObject> _sample = new Dictionary<ItemCategory, ItemObject>();
        private static readonly Dictionary<string, TypeStat> _dType = new Dictionary<string, TypeStat>();
        private static readonly HashSet<string> _errOnce = new HashSet<string>();
        private static long _dWageCap, _dWagePurse, _dKeepCap, _dKeepPurse, _dUnpaid, _dPayNotable, _dPayLord, _dPayPlayer, _dFunded, _dSalePaid, _dSaleShort, _dGear;
        private static int _dFail, _dNoTaker, _dStumbles;
        // 305b: zmiana produkcji gracza z wyrownaniem kapitalu (WorkshopRetoolSettlesCapital) - zwrot do kiesy, doplata z kiesy, brak (gracza nie stac), zmian;
        // wyceny dla gracza (WorkshopTradeBuyerMemory) - z pamieci pol na pol, z cen targu w calosci (krotka historia), z polowa cen targu, z korekta tempa, bez linii broni
        private static long _dRetBack, _dRetAdd, _dRetShort; private static int _dRetN, _dQBlend, _dQExpFull, _dQExpHalf, _dQPace, _dQArms, _dQResaleCap;
        private static int _gatePatched;                    // ile razy transpiler podmienil stala 200 (Harmony przelicza go przy kazdej nowej latce na metodzie - liczy sie przyrost w ApplyAll)
        private static Harmony _harmony;                    // do latki zakladanej w kampanii (raz na proces - Reset jej nie rusza)
        private static bool _finTried, _finPatched;
        private static bool _bkTried; private static object _bkModel; private static MethodInfo _bkTax;
        private static MethodInfo _decide; private static FieldInfo _lastSelected;
        private static MethodInfo _setCapital, _setInitial; private static bool _settersTried;

        // cena sprawiedliwa (123) -> cena kupca (241): biezacy cykl linii towarowej - gra wola cykle po kolei w jednym watku, wiec wystarczy jeden zestaw pol
        private static Workshop _cyc;                       // warsztat, ktorego cykl wlasnie trwa i podlega regule (null = cykl poza regula)
        private static long _cycIn, _cycWare, _cycCredit;   // wsad zaplacony miastu, wsad z magazynu gracza (wartosc wedle ceny miasta), utarg za wyroby oddane miastu
        private static int _cycOut;                         // ile wywolan wyrobu do miasta w cyklu (warsztat gracza czesc odklada do magazynu)
        // 241: cena kupca - ile kupiec miasta placi za sztuki tego cyklu (suma min(dopisane przez BK, sztuki x cena kupca)), sztuki do miasta (z historii / z ceny granicznej)
        private static long _cycAllowed; private static int _cycUnits, _cycHistU, _cycCeilU, _cycShelf0;
        private sealed class FairStat { public int Cycles, Capped, Units, HistU, CeilU; public long Credit, Cost, Kept, Allowed, Craft; }
        private static readonly Dictionary<string, FairStat> _dFair = new Dictionary<string, FairStat>();
        private static int _dGateCut; private static long _dGateCutGold;   // 241: cykle, ktorym prog gry policzono cena kupca (i o ile mniej)
        // recenzja 241: zaplata BK ponad kase miasta (kasa obcieta do 0) - wyrobow, zloto z niczego cofniete z kapitalu warsztatu, niecofniete (kapital za maly)
        private static int _dClampN; private static long _dClampBack, _dClampLeft;
        private static int _cycGold0 = int.MinValue;          // recenzja 241: kasa miasta przed prefiksem BK biezacego wyrobu
        private static long _dNotFrom, _dNotTo;             // kiesy notabli: zdarzenia gry "z niczego" i "w nicosc" (pomiar do ryzyka 123)
        // recenzja 123: regula tylko przy wpietym CALYM pomiarze (wsad z targu, wsad z magazynu, wyrob do miasta, poczatek obu cykli) -
        // bez latki wsadu cykl liczylby koszt 0 i warsztat oddawalby miastu cene wsadu w kazdym cyklu. Raz na proces (Reset nie rusza)
        private static bool _fairWired;
        private static bool _gateItems;                     // 241: postfiks GetItemsToProduce (utarg progu wedlug ceny kupca) wpiety
        internal static bool ConvertWired;                  // E3-D3: latka ChangeProductionTypeOfWorkshopAction.Apply wpieta (koszt sprzetu -> kasa miasta)

        /// <summary>Nowa gra / wczytanie: wolane z WorkshopLaw.Reset(), czyli z konstruktora ArmouryBehavior.</summary>
        internal static void Reset()
        {
            _rec.Clear(); _saved.Clear(); _tradeSpeed.Clear(); _tradeSpeedOpen.Clear(); _sample.Clear(); _errOnce.Clear(); _oreItem = null;
            _bkTried = false; _bkModel = null; _bkTax = null;
            _cyc = null; _cycIn = _cycWare = _cycCredit = 0; _cycOut = 0;
            _cycAllowed = 0; _cycUnits = _cycHistU = _cycCeilU = _cycShelf0 = 0;
            ClearDay();
            MarketSales.Reset();     // 241: sprzedaz miast z 28 dob (zapis arm_mktsales wczytuje Import po konstruktorze)
            ArtisanInputs.Reset();   // 147: rzemieslnicy BK - te same latki cyklu i wyrobu, stan czyszczony razem
            WorkshopPace.Reset();    // E3-D2 (noc-ekonomia3 czesc 2): tempo linii - stan sesji
            WorkshopRetool.Reset();  // E3-D3: zmiany typu za cena - pamiec sesji (limit miasta 30 dob)
            WorkshopFairScore.Reset();   // F4 (noc 17/18.10): pomiar wsadu regionu i pamiec ocen - stan sesji (pomiar wraca z zapisu arm_wsinput)
            WorkshopStartup.Reset();     // R3-11b: rozruch rynku - liczniki i porownanie w logu (stan sesji)
        }

        private static void ClearDay()
        {
            _dType.Clear(); _dFair.Clear();
            _dWageCap = _dWagePurse = _dKeepCap = _dKeepPurse = _dUnpaid = _dPayNotable = _dPayLord = _dPayPlayer = _dFunded = _dSalePaid = _dSaleShort = _dGear = 0;
            _dNotFrom = _dNotTo = 0;
            _dFail = _dNoTaker = _dStumbles = 0;
            _dRetBack = _dRetAdd = _dRetShort = 0; _dRetN = _dQBlend = _dQExpFull = _dQExpHalf = _dQPace = _dQArms = _dQResaleCap = 0;   // 305b
            _dGateCut = 0; _dGateCutGold = 0;
            _dClampN = 0; _dClampBack = _dClampLeft = 0;
        }

        /// <summary>Nowa moneta warsztatow ma sens tylko przy cenach historycznych (HistoricalPrices) - bez nich zostaje gra.
        /// Audyt 120: po starcie gry liczy sie, czy przeliczenie NAPRAWDE weszlo (HistoricalPrices.Applied, jak RawPrice), nie samo
        /// ustawienie - wyjatek przed HistoricalPrices.Apply w OnSessionLaunched zostawia ceny starej monety i wtedy zostaje gra.
        /// Przed startem gry (zakladanie swiata, przed OnSessionLaunched) przeliczenia jeszcze nie ma z natury rzeczy - tam jak dotad
        /// (InitPostfix, poprawke robi SeedNewCampaign po Apply).</summary>
        internal static bool On { get { var s = Settings.Current; return s != null && s.WorkshopTradeEnabled && HistoricalPrices.On && (HistoricalPrices.Applied || !Live); } }
        private static bool Batch { get { return On && Settings.Current.WorkshopTradeBatchWages; } }
        private static float Upkeep { get { return Math.Max(0f, Settings.Current.WorkshopTradeUpkeepPerDay); } }
        /// <summary>Utrzymanie warsztatu na dobe w JEGO miescie: utrzymanie mistrza i czynsz x poziom plac miasta (TownWage; Jeff 07.10 -
        /// koszty w miescie z dobrobytu i stawek historycznych). Przy wylaczonym WorkshopWageByTier - wszedzie tyle samo.</summary>
        private static float UpkeepAt(Workshop w) { return Upkeep * WageIndex(w); }
        private static float WageIndex(Workshop w)
        {
            var s = Settings.Current;
            return s != null && s.WorkshopWageByTier && w != null ? TownWage.Index(w.Settlement) : 1f;
        }
        private static int LowCapital { get { return Math.Max(0, Settings.Current.WorkshopTradeLowCapital); } }
        private static int Span { get { return Math.Max(2, Settings.Current.WorkshopTradeProfitDays); } }
        /// <summary>Pamiec roku (recenzja): rok kalendarza Armoury, nie krocej niz pamiec biezaca.</summary>
        private static int YearSpan { get { return Math.Max(Span, Math.Max(2, CampaignTime.DaysInYear)); } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }
        /// <summary>Cena sprawiedliwa (123): czynna razem z paczka warsztatow (nowa moneta) i wlasnym wlacznikiem - i tylko, gdy wpiete
        /// sa wszystkie jej latki pomiaru (recenzja).</summary>
        private static bool Fair { get { return On && _fairWired && Settings.Current.WorkshopTradeFairPrice; } }
        // ------------------------------------------------------------ 241: cena kupca (zamiast kosztu x marza mistrza z paczki 123)
        /// <summary>241: czy regula ceny kupca jest czynna (ten sam wlacznik WorkshopTradeFairPrice, wpiete latki pomiaru) - dla WorkshopPace i TownFolk (sam odczyt).</summary>
        internal static bool MerchantRule { get { return Fair; } }

        /// <summary>241: udzial rzemieslnikow w zaplacie kupca (TownFolkCraftWageShare - ten sam, co doplata ludu do 35% wartosci wyrobow, N4): warsztat placi go
        /// czeladnikom z tego, co dostal od kupca (gdy wiecej niz place cyklu). Tylko przy regule i placach cyklu (WorkshopTradeBatchWages); inaczej 0.</summary>
        private static float CraftShare
        {
            get
            {
                if (!Fair || !Batch) return 0f;
                float v = Settings.Current.TownFolkCraftWageShare;
                return float.IsNaN(v) ? 0f : Math.Max(0f, Math.Min(1f, v));
            }
        }

        /// <summary>
        /// 241: indeks ceny, za jaki kupiec miasta odsprzedaje towar kategorii `cat` (wzgledem wartosci sztuki): srednia prawdziwej sprzedazy tego miasta z 28 dob
        /// (MarketSales), a bez sprzedazy - cena graniczna ludu (FolkLadderPriceCeiling, wedlug zasobnosci ludu dla listy dostatku - TownFolk.PeopleCeilingIndex).
        /// Oba z istniejacych modeli (audyt 27.10, punkt zatwierdzony przez Jeffa). Sam odczyt.
        /// </summary>
        internal static double MerchantIndex(Town town, ItemCategory cat, out bool fromHist)
        {
            fromHist = false;
            double idx; int units;
            if (town != null && MarketSales.Index(town.Settlement, cat, out idx, out units)) { fromHist = true; return Math.Max(0.0, idx); }
            return TownFolk.PeopleCeilingIndex(town != null ? town.Settlement : null, cat);
        }

        /// <summary>241: cena kupca za sztuke `it` (bez modyfikatora - BK kladzie na polke sztuke bez modyfikatora): najwyzej indeks kupca x wartosc sztuki, co najmniej 1.</summary>
        internal static int RefUnit(Town town, ItemObject it, out bool fromHist)
        {
            fromHist = false;
            if (it == null) return 1;
            double idx = MerchantIndex(town, it.ItemCategory, out fromHist);
            double v = Math.Floor(idx * Math.Max(0, new EquipmentElement(it).ItemValue));
            return v >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)v);
        }

        internal static int RefUnit(Town town, ItemObject it) { bool h; return RefUnit(town, it, out h); }

        /// <summary>241: wartosc wyrobu, ktory warsztat pod regula wlasnie oddaje miastu (TownFolk.OnItemProduced - podstawa doplaty N4): najwyzej cena kupca.
        /// 0 - poza cyklem pod regula albo kategoria spoza linii towarowych (wolajacy bierze cene targu jak dotad). Sam odczyt.</summary>
        internal static int MerchantValueInCycle(Settlement st, ItemObject it)
        {
            try
            {
                var w = _cyc;
                if (w == null || it == null || st == null || !ReferenceEquals(w.Settlement, st) || st.Town == null || !MarketSales.Tracked(it.ItemCategory)) return 0;
                return RefUnit(st.Town, it);
            }
            catch { return 0; }
        }

        /// <summary>241: utarg linii wedlug cen sprzedazy gry (sell) i cen kupna miasta (credit), kazdy najwyzej cena kupca (przy regule) - sztuki probne kategorii (Sample).
        /// false - brak sztuki probnej.</summary>
        private static bool LinePay(Town town, WorkshopType.Production p, out float sell, out float credit)
        {
            sell = 0f; credit = 0f;
            bool rule = Fair;
            foreach (var o in p.Outputs)
            {
                var it = Sample(o.Item1);
                if (it == null) return false;
                int s = town.GetItemPrice(it, null, true), c = town.GetItemPrice(it, null, false);
                if (rule) { int ru = RefUnit(town, it); if (s > ru) s = ru; if (c > ru) c = ru; }
                sell += (float)s * o.Item2;
                credit += (float)c * o.Item2;
            }
            return true;
        }

        /// <summary>Wyjatek przy jednym warsztacie: pierwszy z danego miejsca do pliku, kazdy liczony w linii dnia (funkcji nie gasimy).</summary>
        private static void Stumble(string where, Exception e)
        {
            _dStumbles++;
            if (_errOnce.Add(where)) Log.Error(where, e);
        }

        private static string Key(Workshop w)
        {
            var st = w.Settlement;
            return (st != null ? st.StringId : "?") + "|" + (w.Tag ?? "?");
        }

        private static string TypeId(Workshop w) { return w.WorkshopType != null ? (w.WorkshopType.StringId ?? "?") : "?"; }

        private static Rec Get(Workshop w)
        {
            Rec r;
            string type = TypeId(w);
            if (_rec.TryGetValue(w, out r))
            {
                if (r.Type != type)
                {
                    // zmiana produkcji albo bankructwo: srednia od nowa. Typ potrafi tez zmienic cudzy kod wprost
                    // (Workshop.ChangeWorkshopProduction - np. CrashScribe "strawa Polnocy") - wtedy znacznik kapitalu poprawiamy tutaj
                    r.Type = type; r.Avg = 0f; r.Slow = 0f; r.Days = 0; r.WageCarry = 0f; r.Mem = 0; r.Warm = false; r.Week = null;
                    try { if (On) SyncInitialCapital(w); } catch (Exception e) { Stumble("WorkshopTrade.Get(kapital)", e); }
                }
                return r;
            }
            r = new Rec { Type = type };
            string[] sv;
            if (_saved.TryGetValue(Key(w), out sv) && sv.Length >= 3 && sv[0] == type)
            {
                float a, sl; int d;
                if (float.TryParse(sv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out a) && int.TryParse(sv[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out d) && d > 0)
                {
                    r.Avg = a; r.Days = d;
                    // pamiec roku (pole dopisane w recenzji); brak pola = srednia miesieczna
                    r.Slow = sv.Length >= 4 && float.TryParse(sv[3], NumberStyles.Float, CultureInfo.InvariantCulture, out sl) ? sl : a;
                    // R3-11b: doby pamieci i znacznik rozruchu (pola dopisane 20.10); brak pola = pamiec od pierwszej doby, bez rozruchu (stary zapis bez zmian)
                    int m;
                    r.Mem = sv.Length >= 5 && int.TryParse(sv[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out m) ? Math.Max(1, Math.Min(d, m)) : d;
                    r.Warm = sv.Length >= 6 && sv[5] == "1";
                    if (r.Warm && sv.Length >= 7) r.Week = WorkshopStartup.ParseWeek(sv[6]);   // tydzien rozruchu (tylko w rozruchu)
                }
            }
            _rec[w] = r;
            return r;
        }

        // ------------------------------------------------------------ linie towarowe i place cyklu
        /// <summary>Linia towarowa: sa wyjscia, a wszystkie wejscia i wyjscia to towary handlowe (ta sama flaga co effectCapital w grze).</summary>
        internal static bool IsTradeLine(WorkshopType.Production p)
        {
            if (p.Outputs == null || p.Outputs.Count == 0) return false;   // Production to struct
            foreach (var o in p.Outputs) if (o.Item1 == null || !o.Item1.IsTradeGood) return false;
            if (p.Inputs != null) foreach (var i in p.Inputs) if (i.Item1 == null || !i.Item1.IsTradeGood) return false;
            return true;
        }

        /// <summary>Suma szybkosci linii towarowych typu: tyle cykli na dobe robi zaloga, gdy ida wszystkie. Paczka 172: skipArrows - bez linii
        /// "arrows" (BK ma ja za towar), zamknietej w warsztatach notabli przy czynnych strzelarzach miasta - place pozostalych linii warsztatu
        /// "fletcher" nie dziela sie z linia, ktora nie dziala.</summary>
        private static float TradeSpeed(WorkshopType t, bool skipArrows = false)
        {
            float v;
            if (t == null) return 0f;
            var cache = skipArrows ? _tradeSpeedOpen : _tradeSpeed;
            if (cache.TryGetValue(t, out v)) return v;
            v = 0f;
            foreach (var p in t.Productions) if (IsTradeLine(p) && !(skipArrows && TownFletchers.IsArrowsLine(p))) v += Math.Max(0f, p.ConversionSpeed);
            cache[t] = v;
            return v;
        }

        /// <summary>
        /// Place jednego cyklu linii towarowej (pensy): zaloga warsztatu (WorkshopWorkers roboczodni na dobe po WorkshopWagePerDay,
        /// jak w warsztatach zbrojnych) dzielona rowno na wszystkie cykle towarowe doby. Linia uzbrojenia i ukryci rzemieslnicy: 0.
        /// </summary>
        internal static float CycleLabour(Workshop w, WorkshopType.Production p)
        {
            if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden || !IsTradeLine(p)) return 0f;
            // 172: warsztat notabla bez zamknietej linii strzal; poprawka 174b: warsztat gracza tez, gdy jego linia strzal zamknieta (PlayerWorkshopsSameRule)
            float sum = TradeSpeed(w.WorkshopType, TownFletchers.Active && (w.Owner != Hero.MainHero || (Settings.Current != null && Settings.Current.PlayerWorkshopsSameRule)));
            if (sum <= 0f) return 0f;
            var s = Settings.Current;
            return Math.Max(0f, s.WorkshopWorkers) * Math.Max(0f, s.WorkshopWagePerDay) * WageIndex(w) / sum * BulkFactor(p);   // place czeladnikow x poziom plac miasta
        }

        /// <summary>
        /// E3-D3 (noc-ekonomia3 czesc 2): place jednego cyklu linii towarowej typu `t` w osadzie `st` dla warsztatu NOTABLA - ten sam wzor co CycleLabour
        /// (zaloga x stawka x poziom plac miasta / suma szybkosci linii towarowych typu, bez zamknietej linii strzal przy czynnych strzelarzach), ale dla typu,
        /// ktorego warsztat jeszcze nie ma (wycena przezbrojenia). Bez plac cyklu (WorkshopTradeBatchWages wylaczone) - 0, jak w Expected.
        /// </summary>
        internal static float CycleLabourAt(Settlement st, WorkshopType t, WorkshopType.Production p)
        {
            if (!Batch || t == null || t.IsHidden || !IsTradeLine(p)) return 0f;
            float sum = TradeSpeed(t, TownFletchers.Active);
            if (sum <= 0f) return 0f;
            var s = Settings.Current;
            float idx = s.WorkshopWageByTier && st != null ? TownWage.Index(st) : 1f;
            return Math.Max(0f, s.WorkshopWorkers) * Math.Max(0f, s.WorkshopWagePerDay) * idx / sum * BulkFactor(p);
        }

        /// <summary>
        /// F4 (noc 17/18.10, paczka f4-warsztaty): wynik JEDNEGO cyklu linii towarowej z wsadem typu `t` w miescie `town` wedlug dzisiejszych cen - ta sama regula co
        /// prawdziwy cykl: prog gry (utarg wedle ceny sprzedazy miastu > wsad + place cyklu; bez plac cyklu - prog gry 200 / szybkosc), utarg wedle ceny, ktora placi BK
        /// (cena miasta), przy cenie sprawiedliwej (Fair) najwyzej (wsad + place) x marza mistrza - jak FairCycle. Wsad i wyrob wedlug sztuki probnej kategorii (Sample,
        /// jak Expected). false - linia nie rusza (prog gry, brak sztuki probnej, linia bez wsadu albo nie towarowa). Sam odczyt.
        /// </summary>
        internal static bool LineCycleAt(Town town, WorkshopType t, WorkshopType.Production p, out float net, out float inCost)
        {
            net = 0f; inCost = 0f;
            if (town == null || t == null || p.Inputs == null || p.Inputs.Count == 0 || p.ConversionSpeed <= 0f || !IsTradeLine(p)) return false;
            float inC = 0f;
            foreach (var i in p.Inputs)
            {
                var it = Sample(i.Item1);
                if (it == null) return false;
                inC += (float)town.GetItemPrice(it, null, false) * Math.Max(0, i.Item2);
            }
            // 241: utarg wedlug ceny kupca (sztuka najwyzej tyle, za ile kupiec ja odsprzedaje) - ta sama regula co OutPostfix / FairCycle i prog cyklu (ItemsGatePost)
            float sell, pay;
            if (!LinePay(town, p, out sell, out pay)) return false;
            inCost = inC;
            float share = CraftShare;
            // place cyklu: zaloga warsztatu albo udzial rzemieslnikow w zaplacie kupca - wieksze (CyclePostfix); prog gry z tym samym udzialem od utargu (Gate200)
            float labour = Math.Max(CycleLabourAt(town.Settlement, t, p), share * pay);
            float gate = Batch ? Math.Max(CycleLabourAt(town.Settlement, t, p), share * sell) : 200f / p.ConversionSpeed;
            if (sell <= inC + gate) return false;
            net = pay - inC - labour;
            return true;
        }

        /// <summary>F4: utrzymanie warsztatu na dobe (to, co CloseDay odejmuje od wyniku doby) - zmierzony wynik + to = wynik przed utrzymaniem, jak ocena z cen.</summary>
        internal static float UpkeepOf(Workshop w) { return w == null || Settings.Current == null ? 0f : UpkeepAt(w); }

        /// <summary>E3-D3: srednie wyniku warsztatu (miesiac, rok) i liczba dob w sredniej dla OBECNEGO typu; false - brak rachunku albo typ juz inny (sam odczyt).</summary>
        internal static bool RecOf(Workshop w, out float avg, out float slow, out int days)
        {
            avg = 0f; slow = 0f; days = 0;
            Rec r;
            if (w == null || !_rec.TryGetValue(w, out r) || r == null || r.Type != TypeId(w)) return false;
            avg = r.Avg; slow = r.Slow; days = r.Mem;   // R3-11b: doby W PAMIECI (po rozruchu rynku licza sie od 7 ostatnich dob rozruchu)
            return true;
        }

        private static ItemObject _oreItem;

        /// <summary>
        /// Linia bez wsadu dajaca rude albo drewno (kopalnia BK): WorkshopLaw.BulkLineSkip puszcza co N-ty cykl, bo wyrob to ladunek
        /// 100 kg zamiast dawnych 10 kg (N = HistoricalPrices.BulkScale). Cykl, ktory przeszedl, to robota N cykli - tyle plac.
        /// </summary>
        private static float BulkFactor(WorkshopType.Production p)
        {
            try
            {
                if (p.Inputs != null && p.Inputs.Count > 0) return 1f;
                foreach (var o in p.Outputs)
                {
                    string id = o.Item1 != null ? o.Item1.StringId : null;
                    if (id != "iron" && id != "hardwood") return 1f;
                }
                // bez pamietania wyniku: przelicznik ladunku obowiazuje dopiero po HistoricalPrices.Apply (wczesniej 1)
                if (_oreItem == null)
                {
                    var om = TaleWorlds.ObjectSystem.MBObjectManager.Instance;
                    if (om == null) return 1f;
                    _oreItem = om.GetObject<ItemObject>("iron");
                }
                return Math.Max(1f, HistoricalPrices.CountScale(_oreItem));   // E6/R1 (214): lupa liczy sie jak ladunek rudy (ten sam przelicznik co BulkLineSkip)
            }
            catch (Exception e) { Stumble("WorkshopTrade.BulkFactor", e); return 1f; }
        }

        /// <summary>
        /// Wstawiane przez transpiler w miejsce stalej 200 w CanNotableWorkshopProduceThisCycle / CanPlayerWorkshopProduceThisCycle
        /// (gra liczy: wsad + 200 / szybkosc linii). Zwraca X takie, ze X / szybkosc = place cyklu; przy wylaczonym - 200 jak w grze.
        /// </summary>
        public static float Gate200(WorkshopType.Production production, Workshop workshop)
        {
            try
            {
                if (!Batch || workshop == null || workshop.WorkshopType == null || !IsTradeLine(production)) return 200f;
                float lab = CycleLabour(workshop, production);
                // 241: place cyklu = wieksze z placy zalogi i udzialu rzemieslnikow w utargu (CyclePostfix) - utarg wedlug ceny sprzedazy najwyzej cena kupca, jak ItemsGatePost
                float share = CraftShare;
                var town = workshop.Settlement != null ? workshop.Settlement.Town : null;
                if (share > 0f && town != null && !workshop.WorkshopType.IsHidden)
                {
                    float sell, pay;
                    if (LinePay(town, production, out sell, out pay)) lab = Math.Max(lab, share * sell);
                }
                return lab * production.ConversionSpeed;
            }
            catch (Exception e) { Stumble("WorkshopTrade.Gate200", e); return 200f; }
        }

        public static IEnumerable<CodeInstruction> GateTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            bool done = false;
            var gate = AccessTools.Method(typeof(WorkshopTrade), nameof(Gate200));
            foreach (var c in instructions)
            {
                if (!done && c.opcode == OpCodes.Ldc_R4 && c.operand is float && (float)c.operand == 200f)
                {
                    done = true; _gatePatched++;
                    // production = arg 1, workshop = arg 2 w obu metodach (metody instancji); etykiety zostaja na pierwszej instrukcji
                    var first = new CodeInstruction(OpCodes.Ldarg_1) { labels = c.labels, blocks = c.blocks };
                    yield return first;
                    yield return new CodeInstruction(OpCodes.Ldarg_2);
                    yield return new CodeInstruction(OpCodes.Call, gate);
                    continue;
                }
                yield return c;
            }
        }

        // ------------------------------------------------------------ przelewy
        /// <summary>
        /// Warsztat placi miastu (place albo utrzymanie). Gracz z kapitalem przy progu placi z wlasnej kiesy (jak w grze), reszta z
        /// kapitalu - najwyzej tyle, ile w nim jest. Zwraca kwote faktycznie przekazana; to, co zeszlo, w calosci trafia do kasy miasta.
        /// Audyt 120: z kiesy liczy sie tylko to, co naprawde z niej zeszlo (pomiar przed / po, jak ConvertPostfix); brak - z kapitalu,
        /// reszta niezaplacona. Zapora kuzni (TrueArmourCost.SwallowForgeFee) polyka KAZDY przelew gracza do osady, gdy gracz czeka
        /// w menu kuzni (i przy wygaslej dobie kupuje za niego dobe kuzni) - wyplata warsztatu to nie oplata za kuznie, wiec idzie z
        /// flaga, ktora zapora przepuszcza (DayPass.Charging; samej zapory nie zmieniamy). Bez tego warsztat gracza z pustym
        /// kapitalem bankrutowalby w trakcie kucia, choc w kiesie bylo zloto.
        /// </summary>
        private static int PayToTown(Workshop shop, Town town, int amount, bool wages, Rec rec)
        {
            if (amount <= 0 || shop == null || town == null) return 0;
            var owner = shop.Owner;
            int fromPurse = 0;
            if (owner != null && owner == Hero.MainHero && shop.Capital <= LowCapital && owner.Gold >= amount)
            {
                int before = owner.Gold;
                bool was = DayPass.Charging;
                try
                {
                    DayPass.Charging = true;
                    GiveGoldAction.ApplyForCharacterToSettlement(owner, shop.Settlement, amount, true);   // zdarzenie gry: ksiega widzi przelew gracz -> miasto
                }
                finally { DayPass.Charging = was; }
                fromPurse = Math.Max(0, Math.Min(amount, before - owner.Gold));
                if (fromPurse > 0) { if (wages) { _dWagePurse += fromPurse; if (rec != null) rec.Purse += fromPurse; } else _dKeepPurse += fromPurse; }
                if (wages && fromPurse > 0)
                {
                    TownFolk.EarnFromTown(shop.Settlement, fromPurse, TownFolk.KCraftPaid);   // E5/B1: place z kiesy gracza (przelew gry do kasy) - z kasy do kiesy ludu (ksiega N9)
                    try { TownFolk.Note(shop.Settlement, fromPurse, TownFolk.KCraftPaid); } catch { }   // E5/B1: place juz wyplacone (doplata do 35% nie placi ich drugi raz)
                }
                if (fromPurse >= amount) return amount;
            }
            int rest = amount - fromPurse;
            int paid = Math.Min(rest, Math.Max(0, shop.Capital));
            if (paid > 0)
            {
                shop.ChangeGold(-paid);
                int folk = wages ? TownFolk.Earn(shop.Settlement, paid, TownFolk.KCraftPaid) : 0;   // E5/B1 (R11): place z kapitalu do kiesy ludu (arytmetyka - nie rzuca); utrzymanie - kasa jak dotad
                town.ChangeGold(paid - folk);
                MoneyLedger.NoteWorkshopPay(shop.Settlement, wages ? paid - folk : 0, wages ? 0 : paid);
                try { TownFolk.Note(shop.Settlement, paid, wages ? TownFolk.KCraftPaid : TownFolk.KShopKeep); } catch { }   // E5/0 (195): place i utrzymanie z kapitalu (tylko licznik)
                if (wages) _dWageCap += paid; else _dKeepCap += paid;
            }
            if (paid < rest) _dUnpaid += rest - paid;
            return fromPurse + paid;
        }

        // ------------------------------------------------------------ latki: cykl produkcji
        /// <summary>
        /// WorkshopsCampaignBehavior.RunTownWorkshop - kapital przed (sam odczyt). Przy okazji pomiar do linii dnia: o ile kapital
        /// zmalal od wczorajszego rozliczenia - to wyplata zysku wlascicielowi (i monopol korony), zdjeta miedzy tickami miasta.
        /// </summary>
        public static void RunPrefix(Workshop __1, out int __state)
        {
            __state = __1 != null ? __1.Capital : 0;
            WorkshopPace.Begin(__1);   // E3-D2 (noc-ekonomia3 czesc 2): mnozniki linii tego warsztatu na ten tick (wlasny try; sam klucz WorkshopPaceByMarket)
            try
            {
                if (__1 == null || !On || !Live || __1.WorkshopType == null || __1.WorkshopType.IsHidden) return;
                var rec = Get(__1);
                if (!rec.HasEnd) return;
                rec.HasEnd = false;
                long d = (long)rec.CapEnd - __1.Capital;
                if (d == 0) return;
                var o = __1.Owner;
                if (o != null && o == Hero.MainHero) _dPayPlayer += d; else if (o != null && o.IsNotable) _dPayNotable += d; else _dPayLord += d;
            }
            catch (Exception e) { Stumble("WorkshopTrade.RunPrefix", e); }
        }

        /// <summary>... i po: wynik doby z produkcji (utarg - wsad - place zaplacone z kapitalu), takze warsztatow zbrojnych.</summary>
        public static void RunPostfix(Workshop __1, int __state)
        {
            WorkshopPace.End(__1);     // E3-D2: kontrola licznika linii (wlasny try)
            try
            {
                if (__1 == null || !On || !Live || __1.WorkshopType == null || __1.WorkshopType.IsHidden) return;
                Get(__1).Op += __1.Capital - __state;
            }
            catch (Exception e) { Stumble("WorkshopTrade.RunPostfix", e); }
        }

        /// <summary>
        /// TickOneProductionCycleForNotableWorkshop / ...ForPlayerWorkshop - po udanym cyklu linii towarowej warsztat wyplaca place
        /// cyklu do kasy miasta. Linie uzbrojenia (effectCapital = false; place placi WorkshopLaw od sztuki) i ukryci rzemieslnicy - nic.
        /// </summary>
        public static void CyclePostfix(WorkshopType.Production __0, Workshop __1, bool __2, bool __result)
        {
            ArtisanInputs.CycleEnd();   // 147: koniec cyklu rzemieslnikow BK (zamyka ich rachunek takze po pominietym cyklu)
            // f4-ksiega-i-skory: zapis ulamka skory osady tylko po udanym cyklu (KsHides - wlasne try; poza regula nic). 241: pozycje skory zdjete z listy wyrobow
            // nie ida juz do udzialu sztuk (cena kupca liczy sie od sztuk, ktore naprawde trafily na polke - OutPostfix)
            KsHides.CycleEnd(__1, __result);
            // cena kupca (241) przed placami: utarg ponad cene kupca wraca do kasy miasta, z reszty warsztat placi place cyklu (i udzial rzemieslnikow)
            long payTown = -1; FairStat fst = null;
            if (_cyc != null)
            {
                var shop = _cyc; _cyc = null;
                try { if (__result && __2 && shop == __1) payTown = FairCycle(__0, __1, out fst); }
                catch (Exception e) { Stumble("WorkshopTrade.FairCycle", e); }
            }
            try
            {
                if (!__result || !__2 || !Batch || !Live || __1 == null || __1.WorkshopType == null || __1.WorkshopType.IsHidden) return;
                var town = __1.Settlement != null ? __1.Settlement.Town : null;
                if (town == null) return;
                float labour = CycleLabour(__1, __0);
                // 241: mistrz placi czeladnikom ich udzial (TownFolkCraftWageShare) z tego, co dal kupiec - doplata ludu N4 (kasa miasta do 35% wartosci wyrobow)
                // spada sama, bo place ida tym samym licznikiem KCraftPaid (PayToTown)
                if (payTown > 0) labour = Math.Max(labour, CraftShare * payTown);
                var rec = Get(__1);
                rec.Cycles++;
                if (labour <= 0f) return;
                rec.WageCarry += labour;
                int due = (int)Math.Floor(rec.WageCarry);
                if (due <= 0) return;
                rec.WageCarry -= due;
                int paid = PayToTown(__1, town, due, true, rec);
                rec.Wages += paid;
                if (fst != null) fst.Craft += paid;
            }
            catch (Exception e) { Stumble("WorkshopTrade.CyclePostfix", e); }
        }

        // ------------------------------------------------------------ latki: cena sprawiedliwa (123)
        /// <summary>
        /// TickOneProductionCycleFor{Notable,Player}Workshop - poczatek cyklu: cykl linii towarowej (effectCapital) warsztatu, ktory
        /// nie jest ukrytym rzemieslnikiem, otwiera rachunek cyklu. Prefiks bez wyniku (void) - Harmony wola go takze wtedy, gdy prefiks
        /// WorkshopLaw pomija cykl (linia uzbrojenia, kopalnia co N-ty cykl); wtedy rachunek zamyka CyclePostfix bez przelewu.
        /// Paczka 147: ten sam prefiks otwiera rachunek cyklu ukrytych rzemieslnikow BK (ArtisanInputs - linia z wsadem). Recenzja 147
        /// (dekompilacja Harmony 2.4.2, MethodCreatorTools.AffectsOriginal): prefiks z parametrem typu referencyjnego (Workshop) Harmony
        /// POMIJA, gdy wczesniejszy prefiks (WorkshopLaw, wpiety przed nami) zwrocil false - oba rachunki zamyka wtedy CyclePostfix
        /// (postfiksy ida zawsze), a pominiety cykl i tak nie woli ProduceAnOutputToTown.
        /// </summary>
        public static void CycleStartPrefix(WorkshopType.Production __0, Workshop __1, bool __2)
        {
            ArtisanInputs.CycleStart(__0, __1, __2);
            _cyc = null; _cycIn = _cycWare = _cycCredit = 0; _cycOut = 0;
            _cycAllowed = 0; _cycUnits = _cycHistU = _cycCeilU = _cycShelf0 = 0;
            try
            {
                if (!__2 || __1 == null || __1.WorkshopType == null || __1.WorkshopType.IsHidden || !Fair || !Live) return;
                if (__1.Settlement == null || __1.Settlement.Town == null) return;
                _cyc = __1;
            }
            catch (Exception e) { Stumble("WorkshopTrade.CycleStartPrefix", e); }
        }

        /// <summary>ConsumeInputFromTownMarket (prefiks BK placi miastu za wsad z kapitalu) - ile naprawde zeszlo z kapitalu za wsad cyklu.
        /// Pierwszenstwo First: prefiks BK pomija oryginal (false), a Harmony wola dalsze prefiksy bez wyniku PO nim - odczyt kapitalu
        /// musi isc przed BK, inaczej widzialby kapital juz po zaplacie (sprawdzone na Harmony 2.4.2).</summary>
        [HarmonyPriority(Priority.First)]
        public static void InPrefix(Town __2, Workshop __3, out int __state)
        {
            CirculationWindows.WorkshopInPre(__2, __3);    // paczka 169: okno O36 (tylko log, wlasny try)
            __state = _cyc != null && __3 == _cyc ? __3.Capital : int.MinValue;
        }

        public static void InPostfix(Town __2, Workshop __3, int __state)
        {
            CirculationWindows.WorkshopInPost(__2, __3);   // paczka 169: okno O36 (tylko log, wlasny try) - pierwsza linia
            try { if (__state != int.MinValue && _cyc != null && __3 == _cyc) _cycIn += (long)__state - __3.Capital; }
            catch (Exception e) { Stumble("WorkshopTrade.InPostfix", e); }
        }

        /// <summary>ProduceAnOutputToTown (prefiks BK: cena kupna miasta bez limitu gry 1000) - ile kasa miasta zaplacila za wyrob cyklu.
        /// Pierwszenstwo First - jak InPrefix. Paczka 147: ten sam prefiks zapamietuje polke wyrobu i kapital rzemieslnikow BK przed
        /// prefiksem BK (ArtisanInputs.OutPre), postfiks liczy, ile BK dopisal, i bierze wsad dodatkowych cykli albo zdejmuje sztuki bez wsadu.</summary>
        [HarmonyPriority(Priority.First)]
        public static void OutPrefix(EquipmentElement __0, Workshop __1, out int __state)
        {
            CirculationWindows.WorkshopOutPre(__1);        // paczka 169: okno O35 (tylko log, wlasny try) - przed ArtisanInputs.OutPre
            ArtisanInputs.OutPre(__0, __1);
            __state = _cyc != null && __1 == _cyc ? __1.Capital : int.MinValue;
            // 241: polka wyrobu przed wywolaniem - BK kladzie na polke `count` sztuk bez modyfikatora (rzemieslnicy i kopalnie wiecej niz 1)
            try { _cycShelf0 = __state != int.MinValue && __0.Item != null ? __1.Settlement.Town.Owner.ItemRoster.GetItemNumber(__0.Item) : 0; }
            catch { _cycShelf0 = 0; }
            // recenzja 241: kasa miasta przed zaplata BK - OutPostfix porownuje, ile kasa naprawde stracila, z tym, co BK dopisal warsztatowi
            try { _cycGold0 = __state != int.MinValue ? __1.Settlement.Town.Gold : int.MinValue; }
            catch { _cycGold0 = int.MinValue; }
        }

        /// <summary>
        /// Recenzja 241: prefiks BK ProduceOutputPrefix placi warsztatowi pelna cene polki (Town.GetItemPrice, kupno) i robi town.ChangeGold(-cena) bez limitu;
        /// SettlementComponent.ChangeGold obcina kase do 0, wiec gdy kasa ma mniej, roznica powstaje z niczego. Prog cyklu liczony cena kupca (ItemsGatePost)
        /// wpuszcza takie cykle w biednych miastach (klejnoty, aksamit przy pustej polce). Tu: zloto dopisane warsztatowi ponad to, co kasa naprawde stracila
        /// (tylko gdy kasa skonczyla na 0 - obciecie), wraca z kapitalu warsztatu w nicosc - znosi zloto z niczego. Wolane w OutPostfix PRZED zamknieciem okna O35
        /// (CirculationWindows.WorkshopOutPost), wiec okno i pomiar N3 widza wyrob netto. Reszte (kasa zaplacila - cena kupca) oddaje jak dotad FairCycle.
        /// </summary>
        private static void ClampBack(Workshop w, int state)
        {
            int g0 = _cycGold0; _cycGold0 = int.MinValue;
            if (state == int.MinValue || g0 == int.MinValue || _cyc == null || w != _cyc) return;
            var town = w.Settlement != null ? w.Settlement.Town : null;
            if (town == null || town.Gold > 0) return;
            long cred = (long)w.Capital - state;
            long paid = Math.Max(0L, (long)g0 - town.Gold);
            long minted = cred - paid;
            if (minted <= 0) return;
            int n = (int)Math.Min(minted, (long)Math.Max(0, w.Capital));
            if (n > 0) w.ChangeGold(-n);
            _dClampN++; _dClampBack += Math.Max(0, n); _dClampLeft += minted - Math.Max(0, n);
        }

        public static void OutPostfix(WorkshopsCampaignBehavior __instance, EquipmentElement __0, Workshop __1, int __state)
        {
            // recenzja 241: zloto z obciecia kasy miasta do 0 cofniete PRZED oknem O35 - utarg ponizej to juz tylko to, co kasa naprawde zaplacila
            try { ClampBack(__1, __state); }
            catch (Exception e) { _cycGold0 = int.MinValue; Stumble("WorkshopTrade.ClampBack", e); }
            CirculationWindows.WorkshopOutPost(__1);       // paczka 169: okno O35 - PRZED wplatami rzemieslnikow 147 (ArtisanInputs.OutPost), wlasny try
            try
            {
                if (__state != int.MinValue && _cyc != null && __1 == _cyc)
                {
                    long cred = (long)__1.Capital - __state;   // po ClampBack: tyle, ile kasa miasta naprawde zaplacila (+ niecofniete przy za malym kapitale)
                    _cycCredit += cred; _cycOut++;
                    // 241: cena kupca - za sztuki, ktore naprawde przybyly na polke, kupiec placi najwyzej tyle, za ile je odsprzedaje (RefUnit); reszte FairCycle oddaje kasie
                    var town = __1.Settlement.Town;
                    int units = __0.Item != null ? town.Owner.ItemRoster.GetItemNumber(__0.Item) - _cycShelf0 : 0;
                    if (units <= 0 && cred > 0) units = 1;   // BK dopisal zloto, a polka nie urosla (cudzy podsluch polki) - liczymy jedna sztuke
                    if (units > 0 && cred > 0)
                    {
                        bool hist;
                        int ru = RefUnit(town, __0.Item, out hist);
                        _cycAllowed += Math.Min(cred, (long)units * ru);
                        _cycUnits += units;
                        if (hist) _cycHistU += units; else _cycCeilU += units;
                    }
                }
            }
            catch (Exception e) { Stumble("WorkshopTrade.OutPostfix", e); }
            ArtisanInputs.OutPost(__instance, __1);
        }

        /// <summary>
        /// 241: postfiks WorkshopsCampaignBehavior.GetItemsToProduce (Priority.Last - po GoodsPaths (wieloryb) i KsHides (skory), ktore poprawiaja liste i utarg): utarg
        /// cyklu, z ktorego gra liczy prog zysku (utarg &gt; wsad + place) i warunek kasy miasta (Town.Gold &gt;= utarg), liczy kazda sztuke najwyzej po cenie kupca -
        /// cykl nie rusza dla ceny pustej polki, ktorej nikt nie placi, a kasa miasta musi miec to, co kupiec naprawde zaplaci (nic z niczego). Tylko linie towarowe
        /// warsztatow pod regula (bez ukrytych rzemieslnikow). Sam utarg - lista bez zmian. Nie rzuca.
        /// </summary>
        public static void ItemsGatePost(WorkshopType.Production production, Workshop workshop, List<EquipmentElement> __result, ref int income)
        {
            try
            {
                if (!Fair || !Live || __result == null || __result.Count == 0 || workshop == null || workshop.WorkshopType == null || workshop.WorkshopType.IsHidden) return;
                if (!IsTradeLine(production)) return;
                var town = workshop.Settlement != null ? workshop.Settlement.Town : null;
                if (town == null) return;
                long cut = 0;
                for (int i = 0; i < __result.Count; i++)
                {
                    var el = __result[i];
                    if (el.Item == null) continue;
                    int sell = town.GetItemPrice(el, null, true);
                    int ru = RefUnit(town, el.Item);
                    if (sell > ru) cut += sell - ru;
                }
                if (cut <= 0) return;
                income = (int)Math.Max(int.MinValue, (long)income - cut);
                _dGateCut++; _dGateCutGold += cut;
            }
            catch (Exception e) { Stumble("WorkshopTrade.ItemsGatePost", e); }
        }

        /// <summary>ConsumeInputFromWarehouse (warsztat gracza bierze wsad z jego magazynu - za darmo dla kapitalu): wsad i tak jest kosztem
        /// cyklu - liczony wedle ceny, za jaka miasto sprzedaje ten towar (inaczej miasto zatrzymaloby wartosc srebra gracza).</summary>
        public static void WarePostfix(ItemCategory __0, int __1, Workshop __2)
        {
            try
            {
                if (_cyc == null || __2 != _cyc || __0 == null || __1 <= 0) return;
                var town = __2.Settlement != null ? __2.Settlement.Town : null;
                var it = Sample(__0);
                if (town != null && it != null) _cycWare += (long)town.GetItemPrice(it, null, false) * __1;
            }
            catch (Exception e) { Stumble("WorkshopTrade.WarePostfix", e); }
        }

        /// <summary>
        /// 241 (zamiast ceny sprawiedliwej 123 - koszt x marza): udany cykl linii towarowej - kupiec miasta placi za kazda sztuke oddana na polke najwyzej tyle, za ile
        /// ja odsprzedaje (RefUnit: srednia prawdziwej sprzedazy miasta z 28 dob albo cena graniczna ludu; suma w _cycAllowed z OutPostfix). Utarg ponad to warsztat
        /// oddaje do kasy miasta, ktora mu go przed chwila zaplacila (te same dwie kasy co latka BK - zloto nie powstaje i nie znika). Rynek placacy mniej - bez zmian.
        /// Warsztat gracza, ktory czesc wyrobu odklada do magazynu: sztuki magazynu nie ida przez ProduceAnOutputToTown - kupiec za nie nie placi i ich nie liczy.
        /// Zwraca, ile kupiec ostatecznie zaplacil warsztatowi (utarg - zwrot; podstawa udzialu rzemieslnikow w CyclePostfix); `st` - licznik typu.
        /// </summary>
        private static long FairCycle(WorkshopType.Production p, Workshop w, out FairStat st)
        {
            st = null;
            var town = w.Settlement != null ? w.Settlement.Town : null;
            if (town == null) return 0;
            string type = TypeId(w);
            if (!_dFair.TryGetValue(type, out st)) { st = new FairStat(); _dFair[type] = st; }
            st.Cycles++;
            long credit = _cycCredit, cost = Math.Max(0L, _cycIn) + _cycWare;
            float labour = CycleLabour(w, p);
            st.Credit += credit; st.Cost += cost + (long)Math.Round(labour);
            st.Units += _cycUnits; st.HistU += _cycHistU; st.CeilU += _cycCeilU;
            if (credit <= 0) return 0;
            long allowed = Math.Max(0L, Math.Min(credit, _cycAllowed));
            st.Allowed += allowed;
            long over = credit - allowed;
            if (over <= 0) return credit;
            int n = (int)Math.Min(over, (long)Math.Max(0, w.Capital));
            if (n > 0)
            {
                w.ChangeGold(-n);
                town.ChangeGold(n);
                MoneyLedger.NoteWorkshopFair(w.Settlement, n);
                try { TownFolk.Note(w.Settlement, n, TownFolk.KCraftFair); } catch { }   // E5/0 (195): nadwyzka ceny wyrobu (odczyt - jest w wartosci wyrobow)
                st.Capped++; st.Kept += n;
            }
            return credit - Math.Max(0, n);
        }

        /// <summary>Pomiar do ryzyka 123 (tylko licznik): zdarzenia gry "z niczego" do notabli i "w nicosc" z ich kies. Gra trzyma kiese
        /// notabla w pasie 4 500 - 10 500 (NotablePowerManagementBehavior.BalanceGoldAndPowerOfNotable: nadwyzka -> wplyw, brak <- wplyw).</summary>
        internal static void OnGold((Hero, PartyBase) giver, (Hero, PartyBase) recipient, (int, string) amount, bool showNotification)
        {
            try
            {
                int a = amount.Item1;
                if (a == 0 || !On) return;
                Hero gh = giver.Item1, rh = recipient.Item1;
                PartyBase gp = giver.Item2, rp = recipient.Item2;
                if (a < 0) { var th = gh; gh = rh; rh = th; var tp = gp; gp = rp; rp = tp; a = -a; }   // jak MoneyLedger: ujemna kwota = w druga strone
                if (gh == null && gp == null && rh != null && rh.IsNotable) _dNotFrom += a;
                else if (rh == null && rp == null && gh != null && gh.IsNotable) _dNotTo += a;
            }
            catch (Exception e) { Stumble("WorkshopTrade.OnGold", e); }
        }

        // ------------------------------------------------------------ latka: wydatek dzienny i bankructwo
        /// <summary>
        /// WorkshopsCampaignBehavior.HandleDailyExpense - w miejsce 100 d znikajacych z kapitalu: utrzymanie w nowej monecie do kasy
        /// miasta. Kto nie ma z czego zaplacic, traci warsztat na rzecz innego notabla - ten zmienia produkcje i wklada kapital z
        /// wlasnej kiesy (gra dawala 10 000 z niczego). Przy wylaczonym - oryginal.
        /// </summary>
        public static bool ExpensePrefix(WorkshopsCampaignBehavior __instance, Workshop __0)
        {
            if (!On) return true;
            try
            {
                if (__0 == null || __0.WorkshopType == null || __0.WorkshopType.IsHidden) return false;   // gra tez nie bierze nic od ukrytych
                var town = __0.Settlement != null ? __0.Settlement.Town : null;
                if (town == null) return false;
                var rec = Get(__0);
                rec.KeepCarry += UpkeepAt(__0);
                int due = (int)Math.Floor(rec.KeepCarry);
                rec.KeepCarry -= due;
                int paid = PayToTown(__0, town, due, false, rec);
                CloseDay(__0, rec, paid);
                if (paid < due) Fail(__instance, __0);
                rec.CapEnd = __0.Capital; rec.HasEnd = true;
            }
            catch (Exception e) { Stumble("WorkshopTrade.ExpensePrefix", e); }
            return false;
        }

        /// <summary>Koniec doby warsztatu: wynik do sredniej kroczacej i do licznikow linii dnia.</summary>
        private static void CloseDay(Workshop shop, Rec rec, int keepPaid)
        {
            int x = rec.Op - rec.Purse - keepPaid;
            int span = Span;
            // R3-11b: rozruch rynku (pierwsze 30 dob kampanii) - wynik doby do tygodnia; pierwsza doba po nim zostawia w pamieciach 7 ostatnich dob rozruchu
            WorkshopStartup.BeforeDay(shop, rec.Type, ref rec.Warm, ref rec.Week, ref rec.Mem, ref rec.Avg, ref rec.Slow, x, span, YearSpan);
            if (rec.Mem <= 0) rec.Avg = Expected(shop);   // pierwsza doba pamieci: punkt wyjscia sredniej biezacej = oczekiwany wynik z cen targu
            rec.Avg += (x - rec.Avg) / Math.Min(rec.Mem + 2, span);
            // audyt 120: pamiec roku to srednia samych PRAWDZIWYCH wynikow doby (pierwsza doba: Slow = x) - oczekiwany wynik z cen
            // pustej polki wyrobu (garbarnia +1456 d przy 86 d prawdziwych) nie trzyma ceny kupna przez caly pierwszy rok
            rec.Slow += (x - rec.Slow) / Math.Min(rec.Mem + 1, YearSpan);
            rec.Days++; rec.Mem++;
            TypeStat t;
            if (!_dType.TryGetValue(rec.Type, out t)) { t = new TypeStat(); _dType[rec.Type] = t; }
            t.Shops++; t.Cycles += rec.Cycles; t.Wages += rec.Wages; t.Keep += keepPaid;
            if (rec.Cycles > 0 || rec.Op != 0) t.Worked++;
            if (x >= 0) t.Plus += x; else t.Minus -= x;
            rec.Op = 0; rec.Purse = 0; rec.Wages = 0; rec.Cycles = 0;
        }

        /// <summary>Bankructwo bez zlota z niczego: chetny notabl musi miec z czego wlozyc kapital; inaczej warsztat stoi zamkniety do jutra.</summary>
        private static void Fail(WorkshopsCampaignBehavior beh, Workshop shop)
        {
            var model = Campaign.Current.Models.WorkshopModel;
            Hero buyer = model.GetNotableOwnerForWorkshop(shop);
            int need = Math.Max(0, shop.InitialCapital - Math.Max(0, shop.Capital));
            int least = Math.Min(need, (int)Math.Ceiling(UpkeepAt(shop) * 30f));      // co najmniej miesiac utrzymania
            if (buyer == null || Math.Max(0, buyer.Gold) / 2 < least) { _dNoTaker++; return; }
            WorkshopType type = null;
            try
            {
                if (_decide == null) _decide = AccessTools.Method(typeof(WorkshopsCampaignBehavior), "DecideBestWorkshopType");
                if (_decide != null) type = _decide.Invoke(beh, new object[] { shop.Settlement, false, shop.WorkshopType }) as WorkshopType;
            }
            catch (Exception e) { Stumble("WorkshopTrade.Fail(DecideBestWorkshopType)", e); }
            if (type == null) type = shop.WorkshopType;
            int cost = model.GetCostForNotable(shop);
            ChangeOwnerOfWorkshopAction.ApplyByBankruptcy(shop, buyer, type, cost);   // kapital zostaje (OwnerPrefix), graczowi placi kupiec (SalePrefix / SalePostfix)
            _dFail++;
            int fund = Math.Min(Math.Max(0, shop.InitialCapital - Math.Max(0, shop.Capital)), Math.Max(0, buyer.Gold) / 2);
            if (fund > 0) { buyer.ChangeHeroGold(-fund); shop.ChangeGold(fund); _dFunded += fund; }
        }

        // ------------------------------------------------------------ kapital startowy wedle typu
        /// <summary>
        /// Kapital, z jakim warsztat danego typu zaczyna i ponad ktory zysk idzie do wlasciciela (Workshop.InitialCapital).
        /// Typ czysto towarowy: WorkshopTradeStartCapital. Typ z choc jedna linia uzbrojenia i ukryci rzemieslnicy: wartosc gry
        /// (10 000) - material na najdrozsze sztuki w robocie (WorkshopLaw kupuje go z kapitalu) kosztuje tysiace d, wiec mniejszy
        /// kapital zmienilby produkcje uzbrojenia.
        /// Recenzja: ta sama zasada dla wsadu towarowego - gra puszcza cykl tylko, gdy kapital >= cena wsadu partii; tkalnia
        /// aksamitu kupuje bele jedwabiu za 1000 d (x wspolczynnik ceny), wiec z 2000 w kasie stalaby, gdy jedwab jest ponad 2 x
        /// drozszy niz zwykle (w grze z 10 000 - dopiero ponad 10 x). Kapital czysto towarowy = co najmniej BatchesInTill partii
        /// najdrozszego wsadu w cenach bazowych, najwyzej tyle co gra: aksamit 10 000 jak dotad, piekarnia, garbarnia, winiarnia 2000.
        /// </summary>
        internal static int StartCapitalFor(WorkshopType t)
        {
            int game = Campaign.Current.Models.WorkshopModel.InitialCapital;
            if (t == null || t.IsHidden) return game;
            int batch = 0;
            foreach (var p in t.Productions)
            {
                if (!IsTradeLine(p)) return game;
                int c = 0;
                if (p.Inputs != null)
                    foreach (var i in p.Inputs)
                    {
                        var it = Sample(i.Item1);
                        if (it != null) c += Math.Max(0, it.Value) * Math.Max(1, i.Item2);
                    }
                batch = Math.Max(batch, c);
            }
            return Math.Min(Math.Max(game, 100), Math.Max(Math.Max(100, Settings.Current.WorkshopTradeStartCapital), batch * BatchesInTill));
        }

        private const int BatchesInTill = 10;   // ile partii najdrozszego wsadu warsztat ma w kasie na start (jak 10 000 gry dla aksamitu)

        /// <summary>
        /// Nowa kampania (raz, w sesji, ktora ja zalozyla - po HistoricalPrices.Apply w OnSessionLaunched ArmouryBehavior): warsztaty
        /// zaklada gra PRZED przeliczeniem cen (OnNewGameCreatedPartialFollowUp), wiec InitPostfix wycenil wsad w starej monecie
        /// (jedwab 80 zamiast 1000). Tu ta sama zasada w nowej monecie - tylko warsztaty nietkniete od zalozenia swiata (kapital =
        /// znacznik); to dar startowy swiata, jak 10 000 gry, nie zloto w trakcie gry. Zwraca opis do logu.
        /// </summary>
        internal static string SeedNewCampaign()
        {
            if (!On || Campaign.Current == null || !HistoricalPrices.Applied) return null;   // audyt 120: kapital w nowej monecie tylko po przeliczeniu
            bool fresh = false;
            try { fresh = Campaign.Current.CampaignGameLoadingType == Campaign.GameLoadingType.NewCampaign; }
            catch (Exception e) { Stumble("WorkshopTrade.SeedNewCampaign(rodzaj sesji)", e); return null; }
            if (!fresh) return null;
            int changed = 0, seen = 0; long delta = 0;
            var byType = new Dictionary<string, string>();
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t.Workshops == null) continue;
                foreach (var w in t.Workshops)
                {
                    try
                    {
                        if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden || w.Owner == null) continue;
                        seen++;
                        int want = StartCapitalFor(w.WorkshopType);
                        if (want == w.InitialCapital || w.Capital != w.InitialCapital) continue;
                        delta += want - w.Capital;
                        byType[TypeId(w)] = w.InitialCapital + " -> " + want;
                        SetCapitalMark(w, want, true);
                        changed++;
                    }
                    catch (Exception e) { Stumble("WorkshopTrade.SeedNewCampaign", e); }
                }
            }
            var parts = new List<string>();
            foreach (var kv in byType) parts.Add(kv.Key + " " + kv.Value);
            return "nowa kampania - kapital startowy wedle cen nowej monety: sprawdzono " + seen + " warsztatow, poprawiono " + changed
                   + (parts.Count > 0 ? " (" + string.Join(", ", parts.ToArray()) + ")" : "") + ", kapital warsztatow " + (delta >= 0 ? "+" : "") + delta
                   + " (dar startowy swiata - gra dawala 10000 kazdemu).";
        }

        private static void SetCapitalMark(Workshop w, int initial, bool alsoCapital)
        {
            if (!_settersTried)
            {
                _settersTried = true;
                _setCapital = AccessTools.PropertySetter(typeof(Workshop), "Capital");
                _setInitial = AccessTools.PropertySetter(typeof(Workshop), "InitialCapital");
                if (_setCapital == null || _setInitial == null) Log.Info("WorkshopTrade: BRAK setterow Workshop.Capital / InitialCapital - kapital startowy zostaje jak w grze.");
            }
            if (_setCapital == null || _setInitial == null) return;
            _setInitial.Invoke(w, new object[] { initial });
            if (alsoCapital) _setCapital.Invoke(w, new object[] { initial });
        }

        /// <summary>Workshop.InitializeWorkshop(wlasciciel, typ) - start kampanii: warsztat czysto towarowy dostaje kapital w nowej monecie (gra: 10 000 kazdemu).</summary>
        public static void InitPostfix(Workshop __instance, WorkshopType __1)
        {
            try
            {
                if (!On || __instance == null || __1 == null || Campaign.Current == null) return;
                int c = StartCapitalFor(__1);
                if (c != __instance.InitialCapital) SetCapitalMark(__instance, c, true);
            }
            catch (Exception e) { Stumble("WorkshopTrade.InitPostfix", e); }
        }

        /// <summary>Po zmianie typu (bankructwo, zmiana produkcji przez gracza): znacznik kapitalu poczatkowego wedle NOWEGO typu. Samego kapitalu nie rusza.</summary>
        private static void SyncInitialCapital(Workshop w)
        {
            if (w == null || w.WorkshopType == null || Campaign.Current == null) return;
            int c = StartCapitalFor(w.WorkshopType);
            if (c != w.InitialCapital) SetCapitalMark(w, c, false);
        }

        // ------------------------------------------------------------ latki: zmiana wlasciciela bez zlota z niczego
        /// <summary>Workshop.ChangeOwnerOfWorkshop(nowy, typ, kapital) - gra wpisuje tu InitialCapital (10 000 z niczego albo w nicosc); kapital zostaje.</summary>
        public static void OwnerPrefix(Workshop __instance, ref int __2, out WorkshopType __state)
        {
            __state = null;
            try { if (On && __instance != null) { __2 = __instance.Capital; __state = __instance.WorkshopType; } }
            catch (Exception e) { Stumble("WorkshopTrade.OwnerPrefix", e); }
        }

        /// <summary>... a gdy przy okazji zmienil sie typ (bankructwo), znacznik kapitalu poczatkowego idzie za nowym typem.</summary>
        public static void OwnerPostfix(Workshop __instance, WorkshopType __state)
        {
            try { if (__state != null && On && __instance != null && __instance.WorkshopType != __state) SyncInitialCapital(__instance); }
            catch (Exception e) { Stumble("WorkshopTrade.OwnerPostfix", e); }
        }

        /// <summary>
        /// ChangeOwnerOfWorkshopAction.ApplyInternal - gdy sprzedaje gracz (albo jego warsztat bankrutuje), gra wyplaca mu cene z niczego.
        /// Zerujemy te wyplate, a po zmianie wlasciciela placi kupiec z wlasnej kiesy (GiveGoldAction: najwyzej tyle, ile ma).
        /// </summary>
        public static void SalePrefix(Workshop __0, Hero __1, ref int __4, out int __state)
        {
            __state = 0;
            try
            {
                if (!On || __0 == null || __1 == null || __4 <= 0) return;
                if (__0.Owner != Hero.MainHero || __1 == Hero.MainHero) return;
                __state = __4; __4 = 0;
            }
            catch (Exception e) { Stumble("WorkshopTrade.SalePrefix", e); }
        }

        public static void SalePostfix(Hero __1, int __state)
        {
            try
            {
                if (__state <= 0 || __1 == null) return;
                int pay = Math.Min(__state, Math.Max(0, __1.Gold));
                bool pn = PayNotes.On;   // m96-wies: jeden komunikat z powodem zamiast "You received N" gry
                if (pay > 0) GiveGoldAction.ApplyBetweenCharacters(__1, Hero.MainHero, pay, pn);
                if (pn) PayNotes.Got("workshop", pay, " from " + __1.Name + " for your workshop.");
                _dSalePaid += pay;
                if (pay < __state)
                {
                    _dSaleShort += __state - pay;
                    Log.Player("The buyer could raise only " + pay + " of the " + __state + " denars agreed for your workshop.", true);
                }
            }
            catch (Exception e) { Stumble("WorkshopTrade.SalePostfix", e); }
        }

        /// <summary>
        /// ChangeProductionTypeOfWorkshopAction.Apply - gra kasuje koszt nowego sprzetu (wlasciciel -> nic). Wylaczamy jej pobor
        /// (ignoreCost) i po zmianie produkcji wlasciciel placi te sama kwote kasie miasta (rzemieslnicy, ktorzy sprzet robia).
        /// </summary>
        public static void ConvertPrefix(Workshop __0, WorkshopType __1, ref bool __2, out int __state)
        {
            __state = 0;
            try
            {
                if (!On || __2 || __0 == null || __1 == null || __0.Owner == null || __0.Settlement == null) return;
                __state = Math.Max(0, Campaign.Current.Models.WorkshopModel.GetConvertProductionCost(__1));
                __2 = true;
            }
            catch (Exception e) { __state = 0; Stumble("WorkshopTrade.ConvertPrefix", e); }
        }

        public static void ConvertPostfix(Workshop __0, int __state)
        {
            try { if (On && __0 != null) SyncInitialCapital(__0); }
            catch (Exception e) { Stumble("WorkshopTrade.ConvertPostfix(kapital)", e); }
            try
            {
                if (__state > 0 && __0 != null && __0.Owner != null && __0.Settlement != null)   // 305b: bez return - wyrownanie kapitalu idzie po tym bloku
                {
                    int before = __0.Owner.Gold;
                    bool pn = __0.Owner == Hero.MainHero && PayNotes.On;   // m96-wies (recenzja): warsztat gracza - jeden komunikat z powodem zamiast golego "You paid N"
                    GiveGoldAction.ApplyForCharacterToSettlement(__0.Owner, __0.Settlement, __state, pn);
                    int took = before - __0.Owner.Gold;
                    _dGear += took;
                    if (pn) PayNotes.Paid("workshopgear", took, " to the craftsmen of " + __0.Settlement.Name + " for new tools to change your workshop's trade.");
                }
            }
            catch (Exception e) { Stumble("WorkshopTrade.ConvertPostfix", e); }
            // 305b (WorkshopRetoolSettlesCapital): po sprzecie - kapital do znacznika nowego typu od razu (wlasny try; tylko warsztat gracza)
            try { if (On && __0 != null && __0.Owner == Hero.MainHero && RetoolSettleOn) RetoolSettle(__0); }
            catch (Exception e) { Stumble("WorkshopTrade.RetoolSettle", e); }
        }

        private static bool RetoolSettleOn { get { var s = Settings.Current; return s != null && s.WorkshopRetoolSettlesCapital; } }

        /// <summary>
        /// 305b (WorkshopRetoolSettlesCapital, diagnoza 31.10 ustalenia 2-3): SyncInitialCapital przesuwa sam znacznik kapitalu poczatkowego, zloto zostaje w kasie.
        /// Gra liczy zysk jako Capital - InitialCapital, wiec znacznik w dol oddawal wlasny kapital gracza jako "zysk" (po polowie na dobe, z podatkiem D11-3 -
        /// White Harbor: 7968 + 4994 zwrotu, ok. 3700 podatku), a znacznik w gore zamrazal wyplaty. Teraz wyrownanie od razu i tylko miedzy kasa warsztatu a kiesa
        /// wlasciciela: nadwyzka -> kiesa (poza CalculateOwnerIncomeFromWorkshop, wiec bez D11-3), brak <- kiesa (ile gracz ma; reszte warsztat odbuduje z utargu,
        /// zanim znow zaplaci). Znacznik konca doby (CapEnd) przesuniety o ten sam przelew - RunPrefix nie liczy go jako wyplaty zysku. Ksiega 259 dostaje zwrot
        /// i doplate osobno (CaravanSettle.NoteWsCapital). Notable bez zmian (WorkshopRetool i bankructwa - pas kies notabli).
        /// </summary>
        private static void RetoolSettle(Workshop w)
        {
            var owner = w.Owner;
            if (owner == null || w.WorkshopType == null || w.WorkshopType.IsHidden) return;
            int mark = Math.Max(0, w.InitialCapital), cap = w.Capital;
            var rec = Get(w);   // typ juz nowy - Get zaczyna pamiec od nowa (jak dotad przy pierwszym odczycie po zmianie)
            bool pn = PayNotes.On;
            _dRetN++;
            if (cap > mark)
            {
                int back = cap - mark;
                w.ChangeGold(-back);
                owner.ChangeHeroGold(back);
                if (rec.HasEnd) rec.CapEnd -= back;
                _dRetBack += back;
                CaravanSettle.NoteWsCapital(w, back, 0, cap);
                string why = " from your workshop's till in " + w.Settlement.Name + ": working capital returned - its new trade needs only " + mark + " to work with.";
                if (pn) PayNotes.Got("workshopcapital", back, why); else Log.Player("You received " + back + " denars" + why);
                Log.Info("Warsztaty gracza - kapital i cena (305b): zmiana produkcji (" + w.Settlement.Name + ", " + TypeId(w) + ") - kapital " + cap + " nad znacznikiem " + mark + ", zwrot do kiesy " + back + ".");
            }
            else if (cap < mark)
            {
                int need = mark - cap;
                int add = Math.Min(need, Math.Max(0, owner.Gold));
                if (add > 0)
                {
                    owner.ChangeHeroGold(-add);
                    w.ChangeGold(add);
                    if (rec.HasEnd) rec.CapEnd += add;
                    _dRetAdd += add;
                    CaravanSettle.NoteWsCapital(w, 0, add, cap);
                    string why = " into your workshop's till in " + w.Settlement.Name + ": working capital for its new trade, which needs " + mark + " to work with.";
                    if (pn) PayNotes.Paid("workshopcapital", add, why); else Log.Player("You paid " + add + " denars" + why);
                }
                if (add < need)
                {
                    _dRetShort += need - add;
                    Log.Player("Your workshop in " + w.Settlement.Name + " holds " + (cap + add) + " of the " + mark + " its new trade needs to work with. It pays you no profit until its own takings make up the rest.", true);
                }
                Log.Info("Warsztaty gracza - kapital i cena (305b): zmiana produkcji (" + w.Settlement.Name + ", " + TypeId(w) + ") - kapital " + cap + " pod znacznikiem " + mark + ", doplata z kiesy " + add
                         + (add < need ? ", brak " + (need - add) + " (wyplaty po odbudowie z utargu)" : "") + ".");
            }
        }

        /// <summary>305b: linia rozmowy potwierdzenia zmiany produkcji (stan player_change_production_3, przed linia gry workshop_46): sprzet i kapital nowego typu.</summary>
        private static FieldInfo _lastType; private static MethodInfo _findShop; private static bool _retoolTalkTried;
        internal static bool RetoolCondition()
        {
            try
            {
                if (!On || !RetoolSettleOn || Campaign.Current == null) return false;
                var beh = Campaign.Current.GetCampaignBehavior<WorkshopsCharactersCampaignBehavior>();
                if (beh == null) return false;
                if (!_retoolTalkTried)
                {
                    _retoolTalkTried = true;
                    _lastType = AccessTools.DeclaredField(typeof(WorkshopsCharactersCampaignBehavior), "_lastSelectedWorkshopType");
                    _findShop = AccessTools.DeclaredMethod(typeof(WorkshopsCharactersCampaignBehavior), "FindCurrentWorkshop");
                    if (_lastType == null || _findShop == null) Log.Info("Warsztaty gracza - kapital i cena (305b): rozmowa zmiany produkcji - BRAK pola _lastSelectedWorkshopType albo FindCurrentWorkshop, linia gry jak dotad.");
                }
                if (_lastType == null || _findShop == null) return false;
                var t = _lastType.GetValue(beh) as WorkshopType;
                var w = _findShop.Invoke(beh, null) as Workshop;
                if (t == null || w == null || w.Owner != Hero.MainHero || t.IsHidden) return false;
                int tools = Math.Max(0, Campaign.Current.Models.WorkshopModel.GetConvertProductionCost(t));
                int mark = StartCapitalFor(t), cap = Math.Max(0, w.Capital);
                int purse = Math.Max(0, Hero.MainHero.Gold - tools);
                var sb = new StringBuilder();
                sb.Append("All right. Just to confirm - you want us to switch to ").Append(t.Name).Append("? New tools cost ").Append(tools).Append(" denars. ");
                sb.Append("The new trade needs ").Append(mark).Append(" in its till and the shop holds ").Append(cap);
                if (cap > mark) sb.Append(", so ").Append(cap - mark).Append(" of your working capital comes back to your purse.");
                else if (cap == mark) sb.Append(" - just what it needs.");
                else if (purse >= mark - cap) sb.Append(", so you add ").Append(mark - cap).Append(" from your purse.");
                else sb.Append(". You can add only ").Append(purse).Append(" after the tools; until its own takings make up the rest, the shop pays you no profit.");
                MBTextManager.SetTextVariable("ARM_WS_RETOOL", sb.ToString(), false);
                return true;
            }
            catch (Exception e) { Stumble("WorkshopTrade.RetoolCondition", e); return false; }
        }

        // ------------------------------------------------------------ latki: stale modelu w nowej monecie

        public static void LowLimitPostfix(ref int __result) { if (On) __result = LowCapital; }
        /// <summary>
        /// DailyExpense czytaja juz tylko ekran rodu ("Daily Wage") i drugi pobor z kiesy gracza w modelu finansow. Dopoki ten drugi
        /// pobor nie jest wylaczony latka zakladana w kampanii (ApplyInCampaign), zwracamy 0 - gracz nie placi dwa razy.
        /// </summary>
        public static void DailyExpensePostfix(ref int __result) { if (On) __result = _finPatched ? (int)Math.Round(Upkeep) : 0; }
        public static void ConvertCostPostfix(ref int __result) { if (On) __result = Math.Max(1, (int)Math.Round(__result * EquipScale)); }

        /// <summary>DefaultClanFinanceModel.AddPlayerExpenseForWorkshops - drugi pobor wydatku z kiesy gracza (w nicosc); utrzymanie placi juz ExpensePrefix.</summary>
        public static bool PlayerExpensePrefix() { return !On; }

        /// <summary>E3-D2: latka tempa linii na metode szybkosci czynnego modelu warsztatow - w kampanii (model znany dopiero tu), tym samym Harmony.</summary>
        internal static void ApplyPaceInCampaign() { WorkshopPace.ApplyInCampaign(_harmony); }

        /// <summary>
        /// Jedyna latka paczki na DefaultClanFinanceModel - zakladana DOPIERO w kampanii (docs/ERRORS.md, pulapka z 14.09): konstruktor
        /// statyczny tej klasy czyta Game.Current, a Harmony kompiluje zalatana metode przy zakladaniu latki - przy starcie gry
        /// zabiloby to klase na caly proces. W OnSessionLaunched Game.Current istnieje i klasa jest juz zainicjowana (wymuszamy to).
        /// Raz na proces; gdy sie nie uda, DailyExpense zostaje 0 (gracz nie placi dwa razy, ekran rodu pokaze 0).
        /// </summary>
        internal static void ApplyInCampaign()
        {
            if (_finTried || _harmony == null) return;
            _finTried = true;
            try
            {
                if (Game.Current == null || Campaign.Current == null) { _finTried = false; return; }
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(DefaultClanFinanceModel).TypeHandle);
                var m = AccessTools.Method(typeof(DefaultClanFinanceModel), "AddPlayerExpenseForWorkshops");
                if (m == null) { Log.Info("WorkshopTrade: BRAK DefaultClanFinanceModel.AddPlayerExpenseForWorkshops - wydatek w ekranie rodu zostaje 0."); return; }
                _harmony.Patch(m, prefix: new HarmonyMethod(typeof(WorkshopTrade), nameof(PlayerExpensePrefix)));
                _finPatched = true;
                Log.Info("WorkshopTrade: latka modelu finansow rodu (zakladana w kampanii) - drugi pobor wydatku z kiesy gracza wylaczony, ekran rodu pokazuje utrzymanie dzienne.");
            }
            catch (Exception e) { Log.Error("WorkshopTrade.ApplyInCampaign", e); }
        }

        // ------------------------------------------------------------ E8/B (258, LAWA 6): ekran rodu - utrzymanie warsztatu z poziomem plac miasta
        private static bool _clanTried;
        private static readonly TextObject _txtDailyWage = new TextObject("{=CaRbMaZY}Daily Wage");

        /// <summary>
        /// Ekran rodu pokazuje przy warsztacie "Daily Wage" = Workshop.Expense, czyli stala modelu (DailyExpensePostfix: gole utrzymanie), a warsztat
        /// placi utrzymanie x poziom plac SWOJEGO miasta (UpkeepAt, ExpensePrefix). Postfiks na ClanFinanceWorkshopItemVM.PopulateStatsList podmienia
        /// sam napis tej pozycji na kwote, ktora warsztat naprawde placi - zadnej zmiany w modelu ani w pieniadzach. Zakladany w kampanii, raz na proces
        /// (jak ApplyInCampaign); gdy nie znajdzie celu - linia "nie znaleziono" i ekran jak dotad (R11). Wlacznik SmallFixes8 czytany w postfiksie.
        /// </summary>
        internal static void ApplyClanScreen()
        {
            if (_clanTried || _harmony == null) return;
            _clanTried = true;
            try
            {
                var vm = AccessTools.TypeByName("TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinance.ClanFinanceWorkshopItemVM");
                var m = vm != null ? AccessTools.Method(vm, "PopulateStatsList") : null;
                if (m == null) { Log.Info("Kowadlo i drobne (E8/B): ekran rodu - nie znaleziono ClanFinanceWorkshopItemVM.PopulateStatsList, 'Daily Wage' jak dotad (wpiete 0/1)."); return; }
                _harmony.Patch(m, postfix: new HarmonyMethod(typeof(WorkshopTrade), nameof(ClanWagePostfix)));
                Log.Info("Kowadlo i drobne (E8/B): ekran rodu - 'Daily Wage' warsztatu pokazuje utrzymanie x poziom plac jego miasta (wpiete 1/1).");
            }
            catch (Exception e) { Log.Error("WorkshopTrade.ApplyClanScreen", e); }
        }

        public static void ClanWagePostfix(object __instance)
        {
            try
            {
                if (!On || !_finPatched || !SmallFixes8.On || __instance == null) return;
                var tr = Traverse.Create(__instance);
                var w = tr.Property("Workshop").GetValue<Workshop>();
                var list = tr.Property("ItemProperties").GetValue() as System.Collections.IEnumerable;
                if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden || list == null) return;
                string name = _txtDailyWage.ToString();
                string was = w.Expense.ToString();
                string now = ((int)Math.Round(UpkeepAt(w))).ToString();
                if (was == now) return;
                foreach (var p in list)
                {
                    if (p == null) continue;
                    var tp = Traverse.Create(p);
                    if (tp.Property("Name").GetValue<string>() != name || tp.Property("Value").GetValue<string>() != was) continue;
                    tp.Property("Value").SetValue(now);
                    SmallFixes8.NoteClanWage();
                    break;
                }
            }
            catch (Exception e) { Stumble("WorkshopTrade.ClanWagePostfix", e); }
        }

        // ------------------------------------------------------------ wycena
        private static float EquipScale { get { return MBMath.ClampFloat(Settings.Current.WorkshopTradeEquipmentScale, 0.01f, 10f); } }

        internal sealed class Quote
        {
            public int Price, Going, Equip, Capital, Days, Window;
            public float Profit, Tax, Years;
            public bool Floor;                  // zysk nie dzwiga wartosci sprzetu - cena to sam sprzet
            public bool Exp;                    // 241: zysk z cen targu (Expected) wyzszy niz pamiec - sprzedajacy liczy wedlug dzisiejszej ceny kupca
            // 305b (WorkshopTradeBuyerMemory): pamieci do rozmowy, pol na pol, cena targu w calosci (krotka historia) albo polowa, korekta tempa, bez linii broni
            public float Avg, Slow, Pace = 1f; public int Mem; public bool Blend, ExpHalf, ArmsIdle;
        }

        /// <summary>
        /// Cena warsztatu, ktory kupuje `buyer` od obecnego wlasciciela: lata zysku po podatku, ktory ten KUPUJACY naprawde zaplaci
        /// (srednia kroczaca prawdziwego wyniku; bez historii - oczekiwany wynik z dzisiejszych cen targu), nie mniej niz wartosc
        /// sprzetu, plus kapital w kasie (zostaje w warsztacie). Sprzedajacy liczy zysk z lepszej z dwoch pamieci (miesiac, rok).
        /// </summary>
        internal static Quote QuoteFor(Workshop w, Hero buyer) { return Build(w, TaxShare(w.Settlement, buyer), true); }

        /// <summary>Cena, ktora daje NOTABL kupujacy warsztat od gracza (przed udzialem WorkshopTradeResaleShare): jego podatek i gorsza z dwoch pamieci.</summary>
        internal static Quote QuoteForNotable(Workshop w) { return Build(w, NotableTax(w.Settlement, w.Owner), false); }

        /// <summary>
        /// Recenzja: sprzedajacy pamieta dobre lata, kupujacy zle. Sama srednia z ok. 30 dob robila z warsztatu, ktory miesiac stal
        /// bez wsadu (oblezenie, pusta polka), okazje za cene sprzetu - gracz kupowal go za ulamek, a po powrocie wsadu odsprzedawal
        /// notablowi za wielokrotnosc. Teraz gracz placi wedle lepszej z pamieci (miesiac, rok), notabl daje wedle gorszej - kupno i
        /// odsprzedaz w tej samej chwili nigdy nie daja zysku; tanio idzie tylko warsztat, ktory nie zarabia od roku.
        /// </summary>
        private static Quote Build(Workshop w, float tax, bool sellerAsks)
        {
            var s = Settings.Current;
            var q = new Quote();
            var rec = Get(w);
            q.Days = rec.Days;
            float wp; int wn;
            if (rec.Days > 0 && WorkshopStartup.WeekProfit(rec.Warm, rec.Week, sellerAsks, rec.Avg, rec.Slow, out wp, out wn))
            {
                q.Profit = wp; q.Window = wn;   // R3-11b: w rozruchu rynku zysk z 7 ostatnich dob (notabl - najgorsza z tygodnia, miesiaca i roku)
            }
            else if (rec.Days > 0 && BuyerBlend && (sellerAsks || w.Owner == Hero.MainHero))   // tylko kupno przez gracza i odkup od gracza (sprzedaz lorda z dlugu - jak dotad)
            {
                // 305b (WorkshopTradeBuyerMemory): pol na pol pamiec miesiaca i roku - ta sama miara dla kupujacego gracza i odkupujacego notabla (CostNotablePostfix);
                // przy kupnie od cudzego wlasciciela pamiec przeliczona na tempo gracza (warsztat gracza pracuje w tempie gry - WorkshopPace pomija gracza)
                q.Profit = 0.5f * (rec.Avg + rec.Slow);
                q.Window = Math.Min(rec.Mem, YearSpan);
                q.Blend = true; if (sellerAsks) _dQBlend++;
                if (sellerAsks && Settings.Current.WorkshopBuyerPaceCorrection && w.Owner != Hero.MainHero && !HasArmsLine(w.WorkshopType))
                {
                    float f = WorkshopPace.ShopFactor(w);
                    if (f > 0.01f && Math.Abs(f - 1f) > 0.02f)
                    {
                        float up = UpkeepAt(w);
                        q.Profit = (q.Profit + up) / f - up;   // wynik = (linie x F) - utrzymanie -> (linie x 1) - utrzymanie
                        q.Pace = f; _dQPace++;
                    }
                }
            }
            else if (rec.Days > 0)
            {
                bool year = sellerAsks ? rec.Slow > rec.Avg : rec.Slow < rec.Avg;
                q.Profit = year ? rec.Slow : rec.Avg;
                q.Window = Math.Min(rec.Mem, year ? YearSpan : Span);   // R3-11b: okno = doby w pamieci (po rozruchu rynku od 7 ostatnich dob rozruchu)
            }
            else q.Profit = Expected(w);
            q.Avg = rec.Avg; q.Slow = rec.Slow; q.Mem = rec.Mem;
            // 241: pamieci (ok. 30 dob, rok) trzymaja jeszcze wyniki sprzed ceny kupca (przyciete do kosztu x marza) - sprzedajacy nie odda warsztatu za ulamek
            // tego, co da dzis kupiec: bierze lepsze z pamieci i oczekiwanego wyniku z cen (Expected - istniejacy model, ten sam co bez historii). Notabl (gorsza pamiec) bez zmian
            // 305b: przy WorkshopTradeBuyerMemory cena targu w calosci tylko przy krotkiej historii (< WorkshopBuyerShortHistoryDays), potem najwyzej polowa
            if (sellerAsks && rec.Days > 0 && Fair)
            {
                float exp = Expected(w);
                bool half = q.Blend && rec.Mem >= Math.Max(1, Settings.Current.WorkshopBuyerShortHistoryDays);
                if (half) exp *= 0.5f;
                if (exp > q.Profit) { q.Profit = exp; q.Exp = true; q.ExpHalf = half; if (half) _dQExpHalf++; else if (q.Blend) _dQExpFull++; }
            }
            // 305b: warsztat z linia broni/zbroi z wsadem, gdy te linie nic graczowi nie dadza (PlayerArmsAsFirm wylaczone) - pamiec notabla zawiera zarobek broni
            // (WorkshopLaw.CyclePrefix), ktorego gracz nie dostanie: cena z linii towarowych (Expected - tylko IsTradeLine)
            if (sellerAsks && BuyerBlend && w.Owner != Hero.MainHero && HasArmsInputLine(w.WorkshopType) && !PlayerArmsLive)
            {
                q.Profit = Expected(w); q.Exp = true; q.ExpHalf = false; q.ArmsIdle = true; _dQArms++;
            }
            q.Tax = tax;
            q.Years = Math.Max(0f, s.WorkshopTradePriceYears);
            float net = Math.Max(0f, q.Profit) * (1f - q.Tax);
            q.Going = (int)Math.Round(net * Math.Max(1, CampaignTime.DaysInYear) * q.Years);
            q.Equip = Math.Max(1, (int)Math.Round(w.WorkshopType.EquipmentCost * EquipScale));
            q.Capital = Math.Max(0, w.Capital);
            q.Floor = q.Going <= q.Equip;
            q.Price = Math.Max(q.Going, q.Equip) + q.Capital;
            return q;
        }

        private static bool BuyerBlend { get { var s = Settings.Current; return s != null && s.WorkshopTradeBuyerMemory; } }
        /// <summary>305b: linie broni gracza pracuja jako firma (PlayerArmsAsFirm i WorkshopLaw czynne).</summary>
        private static bool PlayerArmsLive { get { var s = Settings.Current; return s != null && s.PlayerArmsAsFirm && WorkshopLaw.On; } }

        /// <summary>305b: typ ma linie, ktorej wszystkie wyjscia to nie-towar (bron, zbroja, kon) - z wsadem albo bez.</summary>
        private static bool HasArmsLine(WorkshopType t)
        {
            if (t == null) return false;
            foreach (var p in t.Productions)
            {
                if (p.Outputs == null || p.Outputs.Count == 0) continue;
                bool arms = true;
                foreach (var o in p.Outputs) if (o.Item1 == null || o.Item1.IsTradeGood) { arms = false; break; }
                if (arms) return true;
            }
            return false;
        }

        /// <summary>305b: typ ma linie broni z wsadem (ta, ktora PlayerArmsAsFirm zamienia w firme).</summary>
        private static bool HasArmsInputLine(WorkshopType t)
        {
            if (t == null) return false;
            foreach (var p in t.Productions)
            {
                if (p.Inputs == null || p.Inputs.Count == 0 || p.Outputs == null || p.Outputs.Count == 0) continue;
                bool arms = true;
                foreach (var o in p.Outputs) if (o.Item1 == null || o.Item1.IsTradeGood) { arms = false; break; }
                if (arms) return true;
            }
            return false;
        }

        /// <summary>DefaultWorkshopModel.GetCostForPlayer - cena z zarobku zamiast sprzet + 4 x dobrobyt + 2000. Ukryci rzemieslnicy: bez zmian (1 000 000 - nie na sprzedaz).</summary>
        public static void CostPlayerPostfix(Workshop __0, ref int __result)
        {
            try
            {
                if (!On || !Live || __0 == null || __0.WorkshopType == null || __0.WorkshopType.IsHidden) return;
                __result = QuoteFor(__0, Hero.MainHero).Price;
            }
            catch (Exception e) { Stumble("WorkshopTrade.CostPlayerPostfix", e); }
        }

        /// <summary>DefaultWorkshopModel.GetCostForNotable - tyle notabl daje graczowi: czesc wartosci DLA NIEGO (jego podatek, gorsza pamiec) plus kapital w kasie.
        /// Audyt 120: nie wiecej niz ma w sakiewce najbogatszy notabl miasta, ktory moze kupic - te liczbe gracz widzi w rozmowie
        /// ("you can get {PRICE}") PRZED sprzedaza; kupca losuje gra dopiero w konsekwencji, wiec biedniejszy kupiec dalej placi tyle, ile ma.</summary>
        public static void CostNotablePostfix(Workshop __0, ref int __result)
        {
            try
            {
                if (!On || !Live || __0 == null || __0.WorkshopType == null || __0.WorkshopType.IsHidden) return;
                var q = QuoteForNotable(__0);
                float share = MBMath.ClampFloat(Settings.Current.WorkshopTradeResaleShare, 0f, 1f);
                int worth = Math.Max(q.Going, q.Equip);
                // 305b (WorkshopTradeBuyerMemory): ta sama miara co przy kupnie (pol na pol pamieci) - i nigdy wiecej niz udzial tego, co za ten warsztat zaplacilby
                // dzis gracz (inny podatek notabla nie robi z kupna i odsprzedazy zysku)
                if (q.Blend)
                {
                    var qp = QuoteFor(__0, Hero.MainHero);
                    int wp = Math.Max(qp.Going, qp.Equip);
                    if (wp < worth) { worth = wp; _dQResaleCap++; }
                }
                __result = (int)Math.Round(worth * share) + q.Capital;
                int purse = RichestBuyerPurse(__0);
                if (purse >= 0 && __result > purse) __result = purse;
            }
            catch (Exception e) { Stumble("WorkshopTrade.CostNotablePostfix", e); }
        }

        /// <summary>Sakiewka najbogatszego notabla, ktory moze odkupic warsztat: zywy notabl osady poza obecnym wlascicielem (z tych
        /// samych gra losuje kupca w GetNotableOwnerForWorkshop). -1 = nikogo takiego (gra i tak nie pozwoli sprzedac).</summary>
        private static int RichestBuyerPurse(Workshop w)
        {
            var list = w.Settlement != null ? w.Settlement.Notables : null;
            if (list == null) return -1;
            int best = -1;
            foreach (var h in list)
                if (h != null && h.IsAlive && h != w.Owner) best = Math.Max(best, Math.Max(0, h.Gold));
            return best;
        }

        private static Type _bkWsType; private static bool _bkWsTried;

        /// <summary>
        /// Jaka czesc zysku warsztatu naprawde zabiera podatek BK temu, kto bedzie wlascicielem (recenzja - sprawdzone w dekompilacji):
        ///  - NOTABL: BKClanFinanceModel.CalculateNotableDailyGoldChange zawsze odejmuje TaxExpenses (stawka x zysk NAGROMADZONY
        ///    w kapitale), a wyplata to polowa tego zysku (CalculateOwnerIncomeFromWorkshop) - w stanie ustalonym 2 x stawka zysku dnia;
        ///  - ROD (gracz, lord): BKClanFinanceModel.AddExpenses pobiera podatek warsztatow tylko glowie rodu, tylko poza wlasnym
        ///    miastem i tylko gdy CZYNNYM modelem warsztatow gry jest BKWorkshopModel. U Jeffa czynny jest NavalDLCWorkshopModel
        ///    (BK swojego nie rejestruje), wiec gracz i lordowie podatku od warsztatow nie placa wcale - autor liczyl graczowi 40%
        ///    (cena o 40% za niska, a rozmowa obiecywala podatek, ktorego nie bedzie).
        ///  - D11-3 (WorkshopOwnerPaysTownTax): lord albo gracz z warsztatem w miescie INNEGO rodu placi ten podatek panu miasta (Seal31B.OwnerTaxDaily -
        ///    GetWorkshopTaxes = stawka x zysk, ta sama co u glowy rodu przy BKWorkshopModel) - wtedy ta sama czesc co wyzej (Seal31B.OwnerPaysTax).
        ///  - 241 (WorkshopTaxOnPayout): gdy podatek pobiera nasz kod (E3/8a u notabli, D11-3 u wlascicieli w cudzych miastach), placi sie raz od wyplaty - 1 x stawka.
        /// Bez BK: 0.
        /// </summary>
        internal static float TaxShare(Settlement st, Hero payer)
        {
            try
            {
                if (st == null || payer == null) return 0f;
                bool ownerPays = !payer.IsNotable && Seal31B.OwnerPaysTax(st, payer);
                // 241 (WorkshopTaxOnPayout): gdy podatek pobiera nasz kod (notabl - E3/8a, wlasciciel w cudzym miescie - D11-3), kazda zarobiona moneta raz:
                // stawka BK od wyplaty, nie od zysku nagromadzonego (2 x stawka) - cena warsztatu zaklada 1 x stawka; BK pobierajacy sam (BKWorkshopModel) - 2 x jak dotad
                bool once = Seal31B.PayoutRule && (payer.IsNotable ? Seal31B.NotableTaxLive : ownerPays);
                if (!payer.IsNotable && !ownerPays)   // D11-3: wlasciciel w cudzym miescie placi panu miasta - podatek jak u glowy rodu przy BKWorkshopModel
                {
                    var clan = payer.Clan;
                    if (clan == null || clan.Leader != payer || st.OwnerClan == clan) return 0f;
                    if (!_bkWsTried) { _bkWsTried = true; _bkWsType = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKWorkshopModel"); }
                    var active = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.WorkshopModel : null;
                    if (_bkWsType == null || active == null || !_bkWsType.IsInstanceOfType(active)) return 0f;
                }
                if (!_bkTried)
                {
                    _bkTried = true;
                    var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                    var inst = cfgT != null ? AccessTools.Property(cfgT, "Instance").GetValue(null, null) : null;
                    _bkModel = inst != null ? AccessTools.Property(cfgT, "WorkshopModel").GetValue(inst, null) : null;
                    _bkTax = _bkModel != null ? AccessTools.Method(_bkModel.GetType(), "CalculateWorkshopTax", new[] { typeof(Settlement), typeof(Hero) }) : null;
                }
                if (_bkTax == null) return 0f;
                var en = (ExplainedNumber)_bkTax.Invoke(_bkModel, new object[] { st, payer });
                return MBMath.ClampFloat((once ? 1f : 2f) * en.ResultNumber, 0f, 0.9f);
            }
            catch (Exception e) { Stumble("WorkshopTrade.TaxShare", e); return 0f; }
        }

        /// <summary>Podatek notabla, ktory odkupi warsztat od gracza (kupca losuje gra dopiero przy transakcji - bierzemy pierwszego
        /// zywego notabla miasta poza obecnym wlascicielem; stawka BK rozni sie miedzy nimi tylko jednym atutem).</summary>
        private static float NotableTax(Settlement st, Hero owner)
        {
            if (st == null) return 0f;
            Hero who = null;
            var list = st.Notables;
            if (list != null)
                foreach (var h in list)
                    if (h != null && h.IsAlive && h != owner) { who = h; break; }
            if (who == null) return 0f;
            return TaxShare(st, who);
        }

        internal static ItemObject Sample(ItemCategory cat)
        {
            ItemObject it;
            if (cat == null) return null;
            if (_sample.TryGetValue(cat, out it)) return it;
            it = null;
            var om = Game.Current != null ? Game.Current.ObjectManager : null;
            if (om == null) return null;                    // przedmioty jeszcze nie wczytane - bez zapamietywania, nastepne wywolanie sprobuje znowu
            foreach (var o in om.GetObjectTypeList<ItemObject>())
                if (o != null && o.ItemCategory == cat && !o.NotMerchandise && !o.MultiplayerItem && (it == null || o.Value < it.Value)) it = o;
            _sample[cat] = it;
            return it;
        }

        /// <summary>
        /// Oczekiwany wynik doby z dzisiejszych cen targu: linie towarowe, na ktore jest wsad i ktore przechodza prog gry
        /// (utarg wedle ceny sprzedazy &gt; wsad + place cyklu), licza utarg wedle ceny kupna targu (tyle warsztatowi dopisuje BK),
        /// minus wsad i place, razy szybkosc linii; minus utrzymanie. Linie uzbrojenia nie sa wyceniane (wynik da dopiero srednia).
        /// </summary>
        internal static float Expected(Workshop w)
        {
            try
            {
                var town = w != null && w.Settlement != null ? w.Settlement.Town : null;
                var type = w != null ? w.WorkshopType : null;
                if (town == null || type == null) return 0f;
                var roster = town.Owner != null ? town.Owner.ItemRoster : null;
                float sum = 0f;
                foreach (var p in type.Productions)
                {
                    if (!IsTradeLine(p) || p.ConversionSpeed <= 0f) continue;
                    if (TownFletchers.ClosesLine(p, w)) continue;   // 172: linia strzal warsztatu notabla zamknieta - nic nie zarobi
                    int inCost = 0; bool ok = true;
                    if (p.Inputs != null)
                        foreach (var inp in p.Inputs)
                        {
                            int need = inp.Item2;
                            if (roster != null)
                                for (int i = 0; i < roster.Count && need > 0; i++)
                                {
                                    var it = roster.GetItemAtIndex(i);
                                    if (it == null || it.ItemCategory != inp.Item1) continue;
                                    int take = Math.Min(need, roster.GetElementNumber(i));
                                    if (take <= 0) continue;
                                    need -= take; inCost += town.GetItemPrice(it, null, false) * take;
                                }
                            if (need > 0) { ok = false; break; }
                        }
                    if (!ok) continue;
                    // 241: utarg wedlug ceny kupca (sztuka najwyzej tyle, za ile kupiec ja odsprzedaje) - ta sama regula co FairCycle i prog cyklu (ItemsGatePost, Gate200)
                    float sell, pay;
                    if (!LinePay(town, p, out sell, out pay)) continue;
                    float share = CraftShare;
                    float crew = Batch ? CycleLabour(w, p) : 0f;
                    float labour = Math.Max(crew, share * pay);
                    float gate = Batch ? Math.Max(crew, share * sell) : 200f / p.ConversionSpeed;
                    if (sell <= inCost + gate) continue;
                    sum += (pay - inCost - labour) * p.ConversionSpeed / BulkFactor(p);   // kopalnia: przechodzi co N-ty cykl
                }
                return sum - UpkeepAt(w);
            }
            catch (Exception e) { Stumble("WorkshopTrade.Expected", e); return 0f; }
        }

        // ------------------------------------------------------------ rozmowa kupna (teksty dla gracza - po angielsku)
        private static string N1(float v) { return v.ToString("0.#", CultureInfo.InvariantCulture); }

        /// <summary>Warunek naszej linii rozmowy (pierwszenstwo przed linia gry): cena i z czego wynika.</summary>
        internal static bool OfferCondition()
        {
            try
            {
                if (!On || Campaign.Current == null) return false;
                var beh = Campaign.Current.GetCampaignBehavior<WorkshopsCharactersCampaignBehavior>();
                if (beh == null) return false;
                if (_lastSelected == null) _lastSelected = AccessTools.Field(typeof(WorkshopsCharactersCampaignBehavior), "_lastSelectedWorkshop");
                var w = _lastSelected != null ? _lastSelected.GetValue(beh) as Workshop : null;
                if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden) return false;
                var q = QuoteFor(w, Hero.MainHero);
                int price = Campaign.Current.Models.WorkshopModel.GetCostForPlayer(w);   // ta sama liczba, ktora sprawdza przycisk "Yes"
                MBTextManager.SetTextVariable("ARM_WS_OFFER", OfferText(q, price), false);
                return true;
            }
            catch (Exception e) { Stumble("WorkshopTrade.OfferCondition", e); return false; }
        }

        internal static string OfferText(Quote q, int price)
        {
            var sb = new StringBuilder();
            sb.Append("I'm willing to sell. It will cost you ").Append(price).Append(" denars. ");
            if (q.Blend) return OfferBlend(sb, q);   // 305b
            string earned = q.Days > 0 && !q.Exp
                ? "The shop has cleared about " + N1(q.Profit) + " a day " + (q.Window >= YearSpan ? "over the past year" : q.Window <= 1 ? "over the last day" : "over the last " + q.Window + " days")
                : "At today's market prices the shop should clear about " + N1(q.Profit) + " a day";
            if (q.Floor)
                sb.Append(earned).Append(" - too little to price it by, so I ask only what its tools and fittings are worth: ").Append(q.Equip).Append(". ");
            else
            {
                sb.Append(earned);
                bool taxed = q.Tax > 0.001f;
                if (taxed) sb.Append("; the town takes about ").Append(N1(q.Tax * 100f)).Append("% of that in tax");
                sb.Append(". I ask ").Append(N1(q.Years)).Append(taxed ? " years of what is left: " : " years of that: ").Append(q.Going).Append(". ");
            }
            sb.Append("The ").Append(q.Capital).Append(" in its till stays with the shop and becomes yours. Are you willing to pay?");
            return sb.ToString();
        }

        /// <summary>305b (WorkshopTradeBuyerMemory): rozmowa kupna - oba wyniki z pamieci i miara ceny (po angielsku).</summary>
        private static string OfferBlend(StringBuilder sb, Quote q)
        {
            int mon = Math.Min(Math.Max(1, q.Mem), Span);
            sb.Append("Last ").Append(mon).Append(mon == 1 ? " day: " : " days: ").Append(N1(q.Avg)).Append(" a day, ")
              .Append(q.Mem >= YearSpan ? "last year: " : "since my records begin (" + Math.Max(1, q.Mem) + " days): ").Append(N1(q.Slow)).Append(" a day. ");
            if (q.ArmsIdle) sb.Append("Its weapon and armour lines would earn you nothing, so I price it by its trade lines alone: about ").Append(N1(q.Profit)).Append(" a day. ");
            else if (q.Exp) sb.Append(q.ExpHalf ? "Half of what today's market prices promise is more than that: " : "My records are short, and today's market prices promise more: ").Append(N1(q.Profit)).Append(" a day. ");
            else if (q.Pace != 1f) sb.Append("My hands ran it ").Append(q.Pace > 1f ? "faster" : "slower").Append(" with this town's market than yours will; at your pace that is about ").Append(N1(q.Profit)).Append(" a day. ");
            else sb.Append("I price it by half of each: ").Append(N1(q.Profit)).Append(" a day. ");
            if (q.Floor) sb.Append("That is too little to price it by, so I ask only what its tools and fittings are worth: ").Append(q.Equip).Append(". ");
            else
            {
                bool taxed = q.Tax > 0.001f;
                if (taxed) sb.Append("The town takes about ").Append(N1(q.Tax * 100f)).Append("% of that in tax. ");
                sb.Append("I ask ").Append(N1(q.Years)).Append(taxed ? " years of what is left: " : " years of that: ").Append(q.Going).Append(". ");
            }
            sb.Append("The ").Append(q.Capital).Append(" in its till stays with the shop and becomes yours. Are you willing to pay?");
            return sb.ToString();
        }

        /// <summary>305b: linia doby "Warsztaty gracza - kapital i cena (305b)" (z Daily, po linii "Warsztaty towarowe").</summary>
        private static void Daily305b(int day)
        {
            var s = Settings.Current;
            Log.Info("Warsztaty gracza - kapital i cena (305b): dzien " + day + " | klucze: Workshop Retool Settles Capital " + (s.WorkshopRetoolSettlesCapital ? "TAK" : "NIE")
                     + ", Workshop Trade Buyer Memory " + (s.WorkshopTradeBuyerMemory ? "TAK (pol na pol)" : "NIE (lepsza pamiec)") + ", Pace Correction " + (s.WorkshopBuyerPaceCorrection ? "TAK" : "NIE")
                     + ", krotka historia " + s.WorkshopBuyerShortHistoryDays + " dob, Player Arms As Firm " + (PlayerArmsLive ? "TAK" : "NIE")
                     + " | zmiany produkcji gracza " + _dRetN + ": zwrot kapitalu do kiesy " + _dRetBack + ", doplata z kiesy " + _dRetAdd + ", brak (odbudowa z utargu) " + _dRetShort
                     + " | wyceny dla gracza: pol na pol " + _dQBlend + ", cena targu w calosci (krotka historia) " + _dQExpFull + ", polowa ceny targu " + _dQExpHalf
                     + ", korekta tempa " + _dQPace + ", bez linii broni " + _dQArms + ", odkup notabla przyciety do ceny gracza " + _dQResaleCap + " | potkniecia " + _dStumbles + ".");
        }

        // ------------------------------------------------------------ linia dnia
        private static int Median(List<int> l) { if (l.Count == 0) return 0; l.Sort(); return l[l.Count / 2]; }

        /// <summary>Raz na dobe (DailyTickEvent): "Warsztaty towarowe:" - stan, wynik wedle typu, place, wyplaty, przyklad ceny.</summary>
        internal static void Daily()
        {
            try
            {
                if (!On || Campaign.Current == null) { ClearDay(); return; }
                int day = (int)CampaignTime.Now.ToDays - 1;
                int shops = 0, broke = 0, worked = 0, cycles = 0;
                long plus = 0, minus = 0;
                var prices = new List<int>();
                Town sampleTown = null; float bestProsperity = -1f;
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || t.Workshops == null) continue;
                    bool any = false;
                    foreach (var w in t.Workshops)
                    {
                        if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden || w.Owner == null) continue;
                        shops++; any = true;
                        if (w.Capital < Math.Max(1, (int)Math.Ceiling(UpkeepAt(w)))) broke++;
                        try { prices.Add(QuoteFor(w, Hero.MainHero).Price); } catch (Exception e) { Stumble("WorkshopTrade.Daily(cena)", e); }
                    }
                    if (!any) continue;
                    if (t.Settlement != null && t.Settlement.StringId == "town_V9") { sampleTown = t; bestProsperity = float.MaxValue; }   // Lannisport (zgloszenie Jeffa)
                    else if (t.Prosperity > bestProsperity) { sampleTown = t; bestProsperity = t.Prosperity; }
                }
                var keys = new List<string>(_dType.Keys);
                keys.Sort((a, b) => _dType[b].Shops.CompareTo(_dType[a].Shops));
                var parts = new List<string>();
                foreach (var k in keys)
                {
                    var t = _dType[k];
                    worked += t.Worked; cycles += t.Cycles; plus += t.Plus; minus += t.Minus;
                    long net = t.Plus - t.Minus;
                    parts.Add(k + " " + t.Shops + " (pracowalo " + t.Worked + ", cykli " + t.Cycles + ", wynik " + (net >= 0 ? "+" : "") + net + " = "
                              + (t.Shops > 0 ? ((float)net / t.Shops).ToString("0.#", CultureInfo.InvariantCulture) : "0") + " d na warsztat, place " + t.Wages + ", utrzymanie " + t.Keep + ")");
                }
                int min = 0, max = 0, med = Median(prices);
                if (prices.Count > 0) { min = prices[0]; max = prices[prices.Count - 1]; }
                var s = Settings.Current;
                var sb = new StringBuilder();
                sb.Append("Warsztaty towarowe: dzien ").Append(day).Append(" | warsztatow ").Append(shops).Append(" (bez ukrytych rzemieslnikow), rozliczonych dzis ")
                  .Append(SumShops()).Append(", pracowalo ").Append(worked).Append(" (cykli towarowych ").Append(cycles).Append("), bez kapitalu na utrzymanie ").Append(broke)
                  .Append(" | wynik dnia po placach i utrzymaniu: zysk +").Append(plus).Append(", strata -").Append(minus).Append(", razem ").Append(plus - minus >= 0 ? "+" : "").Append(plus - minus)
                  .Append(" | wedle typu: ").Append(parts.Count > 0 ? string.Join("; ", parts.ToArray()) : "brak rozliczen")
                  .Append(" | do kas miast: place za cykle ").Append(_dWageCap + _dWagePurse).Append(" (z kapitalu ").Append(_dWageCap).Append(", z kiesy gracza ").Append(_dWagePurse)
                  .Append("), utrzymanie ").Append(_dKeepCap + _dKeepPurse).Append(" (z kapitalu ").Append(_dKeepCap).Append(", z kiesy gracza ").Append(_dKeepPurse)
                  .Append("), sprzet przy zmianie produkcji ").Append(_dGear).Append("; niezaplacone (pusty kapital) ").Append(_dUnpaid)
                  .Append(" | zeszlo z kapitalu miedzy tickami miast (wyplata zysku wlascicielom, monopol korony): notable ").Append(_dPayNotable).Append(", lordowie ").Append(_dPayLord).Append(", gracz ").Append(_dPayPlayer)
                  .Append(" | bankructwa ").Append(_dFail).Append(" (nowi wlasciciele wlozyli z wlasnych kies ").Append(_dFunded).Append("), bez chetnego z pieniedzmi ").Append(_dNoTaker)
                  .Append("; sprzedaz gracza notablom: zaplacone ").Append(_dSalePaid).Append(", zabraklo kupcowi ").Append(_dSaleShort)
                  .Append(" | ").Append(FairText()).Append(" | ").Append(NotablesText())
                  .Append(" | cena kupna dla gracza: mediana ").Append(med).Append(", od ").Append(min).Append(" do ").Append(max).Append(" (").Append(prices.Count).Append(" warsztatow)");
                if (sampleTown != null) sb.Append("; ").Append(SampleLine(sampleTown));
                sb.Append(" | zasady: utrzymanie ").Append(N1(Upkeep)).Append(" d na dobe").Append(s.WorkshopWageByTier ? " x poziom plac miasta (dobrobyt / " + N1(s.TownWageRefProsperity) + ", 0.5-1.5)" : "").Append(", place cyklu ").Append(s.WorkshopTradeBatchWages ? N1(Math.Max(0f, s.WorkshopWorkers) * Math.Max(0f, s.WorkshopWagePerDay)) + " d na dobe pelnej pracy" : "WYLACZONE (prog gry 200 / szybkosc)")
                  .Append(", cena = ").Append(N1(Math.Max(0f, s.WorkshopTradePriceYears))).Append(" x roczny zysk po podatku kupujacego (nie mniej niz sprzet) + kapital; zysk ze sredniej ok. ").Append(Span)
                  .Append(" dob albo roku - gracz kupuje wedle lepszej, notabl odkupuje wedle gorszej; potkniecia ").Append(_dStumbles).Append('.');
                Log.Info(sb.ToString());
                try { Daily305b(day); } catch (Exception e) { Stumble("WorkshopTrade.Daily305b", e); }
            }
            catch (Exception e) { Log.Error("WorkshopTrade.Daily", e); }
            finally { ClearDay(); }
        }

        private static int SumShops() { int n = 0; foreach (var t in _dType.Values) n += t.Shops; return n; }

        /// <summary>241: czesc linii dnia o cenie kupca: cykle, sztuki do miast (cena z historii sprzedazy / z ceny granicznej ludu), utarg wedle polki, zaplacono
        /// warsztatom, oddane kasom miast, rzemieslnikom (udzial w zaplacie), wedlug typu; indeksy cen wybranych towarow i rzemieslnikow BK (poza regula).</summary>
        private static string FairText()
        {
            if (!Settings.Current.WorkshopTradeFairPrice) return "cena kupca (241) WYLACZONA (MCM) - warsztat bierze pelna cene miasta za kazda sztuke";
            if (!_fairWired) return "cena kupca (241) NIECZYNNA - brak latki pomiaru (BRAK w linii startowej WorkshopTrade), warsztat bierze pelna cene miasta za kazda sztuke";
            int cyc = 0, capped = 0, units = 0, hist = 0, ceil = 0; long credit = 0, kept = 0, craft = 0;
            foreach (var f in _dFair.Values) { cyc += f.Cycles; capped += f.Capped; units += f.Units; hist += f.HistU; ceil += f.CeilU; credit += f.Credit; kept += f.Kept; craft += f.Craft; }
            var keys = new List<string>(_dFair.Keys);
            keys.Sort((a, b) => _dFair[b].Credit != _dFair[a].Credit ? _dFair[b].Credit.CompareTo(_dFair[a].Credit) : string.CompareOrdinal(a, b));
            var parts = new List<string>();
            foreach (var k in keys)
            {
                var f = _dFair[k];
                if (f.Credit <= 0 || parts.Count >= 10) continue;
                parts.Add(k + " " + f.Capped + "/" + f.Cycles + " cykli: sztuk " + f.Units + ", utarg " + f.Credit + ", zaplacono " + (f.Credit - f.Kept) + ", oddane " + f.Kept + ", rzemieslnikom " + f.Craft);
            }
            return "cena kupca (241) CZYNNA: cykli " + cyc + ", przycietych " + capped + "; sztuk do miast " + units + " (z historii " + hist + ", z ceny granicznej " + ceil + ")"
                   + "; utarg wedle polki " + credit + ", zaplacono warsztatom " + (credit - kept) + ", oddane kasom miast " + kept + ", rzemieslnikom " + craft
                   + "; zaplata BK ponad kase miasta: wyrobow " + _dClampN + ", z niczego cofniete " + _dClampBack + ", niecofniete " + _dClampLeft + "; prog cyklu wedlug ceny kupca: cykli " + _dGateCut + " (utarg progu mniejszy o " + _dGateCutGold + ")"
                   + " [wedlug typu: " + (parts.Count > 0 ? string.Join("; ", parts.ToArray()) : "-") + "]"
                   + " [indeksy polka/kupiec/28 dob: " + IndexText() + "]";
        }

        private static readonly string[] IdxCats = { "jewelry", "velvet", "tools" };

        /// <summary>241 (tylko log): srednie indeksy swiata (wszystkie miasta) - polka (GetItemCategoryPriceIndex), kupiec (MerchantIndex), 28 dob (miasta z historia);
        /// plus rzemieslnicy BK (ukryte warsztaty - poza regula): ich wyroby, polka wobec kupca, w miastach z rzemieslnikami.</summary>
        private static string IndexText()
        {
            var parts = new List<string>();
            var inv = CultureInfo.InvariantCulture;
            try
            {
                foreach (var id in IdxCats)
                {
                    ItemCategory cat = null;
                    foreach (var c in ItemCategories.All) if (c != null && c.StringId == id) { cat = c; break; }
                    if (cat == null) { parts.Add(id + " brak"); continue; }
                    double sa = 0, ka = 0, ha = 0; int n = 0, h = 0;
                    foreach (var t in Town.AllTowns)
                    {
                        if (t == null || t.Settlement == null) continue;
                        n++;
                        sa += t.GetItemCategoryPriceIndex(cat);
                        bool fh; ka += MerchantIndex(t, cat, out fh);
                        double hi; int hu;
                        if (MarketSales.Index(t.Settlement, cat, out hi, out hu)) { ha += hi; h++; }
                    }
                    if (n == 0) continue;
                    parts.Add(id + " " + (sa / n).ToString("0.00", inv) + "/" + (ka / n).ToString("0.00", inv) + "/" + (h > 0 ? (ha / h).ToString("0.00", inv) : "-") + " (miast z historia " + h + ")");
                }
                double asa = 0, aka = 0; int an = 0;
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || t.Workshops == null) continue;
                    foreach (var w in t.Workshops)
                    {
                        if (w == null || w.WorkshopType == null || !w.WorkshopType.IsHidden) continue;
                        foreach (var p in w.WorkshopType.Productions)
                        {
                            if (!IsTradeLine(p)) continue;
                            foreach (var o in p.Outputs)
                            {
                                if (o.Item1 == null) continue;
                                bool fh;
                                asa += t.GetItemCategoryPriceIndex(o.Item1); aka += MerchantIndex(t, o.Item1, out fh); an++;
                            }
                        }
                    }
                }
                parts.Add("rzemieslnicy BK (poza regula 241, pelna cena polki) " + (an > 0 ? "polka " + (asa / an).ToString("0.00", inv) + " / kupiec " + (aka / an).ToString("0.00", inv) + " (" + an + " wyrobow-miast)" : "brak"));
            }
            catch (Exception e) { Stumble("WorkshopTrade.IndexText", e); }
            return string.Join(", ", parts.ToArray());
        }

        /// <summary>Pomiar do ryzyka 123 (tylko log): kiesy notabli i zdarzenia gry z niczego / w nicosc z ich udzialem.</summary>
        private static string NotablesText()
        {
            int n = 0, low = 0, high = 0; long sum = 0;
            try
            {
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.Notables == null) continue;
                    foreach (var h in st.Notables)
                    {
                        if (h == null || !h.IsAlive) continue;
                        n++; sum += h.Gold;
                        if (h.Gold < 4500) low++; else if (h.Gold > 10500) high++;
                    }
                }
            }
            catch (Exception e) { Stumble("WorkshopTrade.NotablesText", e); }
            return "kiesy notabli (gra trzyma je w pasie 4500-10500 - nadwyzke zamienia na wplyw, brak dosypuje za wplyw): notabli " + n + ", w kiesach " + sum
                   + ", ponizej 4500 " + low + ", ponad 10500 " + high + "; zdarzenia gry dzis: do notabli z niczego +" + _dNotFrom
                   + " (wyplaty zysku warsztatow i karawan, dosypka, nowi notable), z kies notabli w nicosc -" + _dNotTo + " (nadwyzka na wplyw)";
        }

        private static string SampleLine(Town t)
        {
            var parts = new List<string>();
            foreach (var w in t.Workshops)
            {
                if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden || w.Owner == null) continue;
                try
                {
                    var q = QuoteFor(w, Hero.MainHero);
                    parts.Add(TypeId(w) + " " + q.Price + " (zysk " + (q.Exp ? "oczekiwany z ceny kupca (241, wyzszy niz pamiec) " : q.Days > 0 ? "sredni z " + q.Window + " dob" + (q.Window < q.Days ? " (rozliczonych od startu " + q.Days + ") " : " ") : "oczekiwany z cen ") + N1(q.Profit) + " d na dobe, podatek gracza " + N1(q.Tax * 100f) + "%, "
                              + (q.Floor ? "sam sprzet " + q.Equip : N1(q.Years) + " x roczny zysk = " + q.Going) + ", kapital " + q.Capital + ProductIndex(t, w) + ")");
                }
                catch (Exception e) { Stumble("WorkshopTrade.SampleLine", e); }
            }
            return "przyklad " + (t.Name != null ? t.Name.ToString() : "?") + " (dobrobyt " + (int)t.Prosperity + "): " + (parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "brak warsztatow");
        }

        /// <summary>Recenzja (tylko log): wspolczynnik ceny wyrobu i wsadu pierwszej linii towarowej w tym miescie - rachunek autora
        /// zakladal 1.0 wszedzie, a zysk (i cena) piekarni zalezy glownie od tego, ile miasto placi za chleb.</summary>
        private static string ProductIndex(Town t, Workshop w)
        {
            try
            {
                foreach (var p in w.WorkshopType.Productions)
                {
                    if (!IsTradeLine(p)) continue;
                    var sb = new StringBuilder("; wyrob ");
                    sb.Append(p.Outputs[0].Item1.StringId).Append(" x").Append(t.GetItemCategoryPriceIndex(p.Outputs[0].Item1).ToString("0.00", CultureInfo.InvariantCulture));
                    if (p.Inputs != null && p.Inputs.Count > 0)
                        sb.Append(", wsad ").Append(p.Inputs[0].Item1.StringId).Append(" x").Append(t.GetItemCategoryPriceIndex(p.Inputs[0].Item1).ToString("0.00", CultureInfo.InvariantCulture));
                    return sb.ToString();
                }
            }
            catch (Exception e) { Stumble("WorkshopTrade.ProductIndex", e); }
            return "";
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            var sb = new StringBuilder();
            foreach (var kv in _rec)
            {
                var w = kv.Key; var r = kv.Value;
                if (w == null || r == null || r.Days <= 0 || w.Settlement == null) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(Key(w)).Append('|').Append(r.Type).Append('|').Append(r.Avg.ToString("0.###", CultureInfo.InvariantCulture)).Append('|').Append(r.Days)
                  .Append('|').Append(r.Slow.ToString("0.###", CultureInfo.InvariantCulture))
                  .Append('|').Append(r.Mem.ToString(CultureInfo.InvariantCulture)).Append('|').Append(r.Warm ? '1' : '0');   // R3-11b (starsza DLL czyta 4 pierwsze pola)
                string wk = WorkshopStartup.WeekField(r.Warm, r.Week);   // R3-11b: tydzien rozruchu - tylko w rozruchu
                if (wk != null) sb.Append('|').Append(wk);
            }
            // warsztaty, ktorych w tej sesji jeszcze nikt nie rozliczyl, zachowuja zapis z poprzedniej
            var live = new HashSet<string>();
            foreach (var w in _rec.Keys) if (w != null && w.Settlement != null) live.Add(Key(w));
            foreach (var kv in _saved)
            {
                if (live.Contains(kv.Key) || kv.Value == null || kv.Value.Length < 3) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append('|').Append(kv.Value[0]).Append('|').Append(kv.Value[1]).Append('|').Append(kv.Value[2]);
                for (int i = 3; i < kv.Value.Length; i++) sb.Append('|').Append(kv.Value[i]);   // R3-11b: wszystkie pola z zapisu (rok, doby pamieci, rozruch)
            }
            return sb.ToString();
        }

        internal static void Import(string data)
        {
            _saved.Clear(); _rec.Clear();
            if (string.IsNullOrEmpty(data)) return;
            foreach (var row in data.Split(';'))
            {
                var f = row.Split('|');                     // osada | znacznik warsztatu | typ | srednia | doby [| srednia roku [| doby pamieci | rozruch [| tydzien] (R3-11b)]]
                if (f.Length < 5) continue;
                var v = new string[Math.Min(f.Length, 9) - 2];
                Array.Copy(f, 2, v, 0, v.Length);
                _saved[f[0] + "|" + f[1]] = v;
            }
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>(); var miss = new List<string>();
            Action<string, MethodBase, string, string, string> patch = (name, target, prefix, postfix, transpiler) =>
            {
                try
                {
                    if (target == null) { miss.Add(name); return; }
                    h.Patch(target,
                            prefix: prefix != null ? new HarmonyMethod(typeof(WorkshopTrade), prefix) : null,
                            postfix: postfix != null ? new HarmonyMethod(typeof(WorkshopTrade), postfix) : null,
                            transpiler: transpiler != null ? new HarmonyMethod(typeof(WorkshopTrade), transpiler) : null);
                    done.Add(name);
                }
                catch (Exception e) { miss.Add(name + " (" + e.Message + ")"); Log.Error("WorkshopTrade.ApplyAll(" + name + ")", e); }
            };
            // KOLEJNOSC: najpierw metody wolane, potem te, ktore je wolaja - Harmony kompiluje opakowanie zalatanej metody od razu,
            // a kompilator moze wkleic (inline) w nie mala metode, ktora latke dostaje dopiero pozniej (taka latka bylaby omijana)
            var beh = typeof(WorkshopsCampaignBehavior);
            int before = _gatePatched;
            patch("prog cyklu notabla", AccessTools.Method(beh, "CanNotableWorkshopProduceThisCycle"), null, null, nameof(GateTranspiler));
            patch("prog cyklu gracza", AccessTools.Method(beh, "CanPlayerWorkshopProduceThisCycle"), null, null, nameof(GateTranspiler));
            int gates = _gatePatched - before;
            // cena sprawiedliwa (123): pomiar wsadu i utargu cyklu - metody wolane przez cykl, wiec PRZED latkami cyklu
            patch("cena sprawiedliwa: wsad z targu", AccessTools.Method(beh, "ConsumeInputFromTownMarket"), nameof(InPrefix), nameof(InPostfix), null);
            patch("cena sprawiedliwa: wsad z magazynu", AccessTools.Method(beh, "ConsumeInputFromWarehouse"), null, nameof(WarePostfix), null);
            patch("cena sprawiedliwa: wyrob do miasta", AccessTools.Method(beh, "ProduceAnOutputToTown"), nameof(OutPrefix), nameof(OutPostfix), null);
            // 241: utarg progu cyklu wedlug ceny kupca - GetItemsToProduce jest wolana przez cykl, wiec PRZED latkami cyklu; postfiks Last (po wielorybie i skorach)
            try
            {
                var gi = AccessTools.DeclaredMethod(beh, "GetItemsToProduce");
                if (gi == null) miss.Add("cena kupca: prog cyklu (GetItemsToProduce)");
                else { h.Patch(gi, postfix: new HarmonyMethod(typeof(WorkshopTrade), nameof(ItemsGatePost)) { priority = Priority.Last }); done.Add("cena kupca: prog cyklu"); _gateItems = true; }
            }
            catch (Exception e) { miss.Add("cena kupca: prog cyklu (" + e.Message + ")"); Log.Error("WorkshopTrade.ApplyAll(GetItemsToProduce)", e); }
            try { MarketSales.ApplyAll(h); } catch (Exception e) { Log.Error("MarketSales.ApplyAll", e); }   // 241: pomiar sprzedazy miast (konsumpcja, partie)
            patch("place cyklu notabla", AccessTools.Method(beh, "TickOneProductionCycleForNotableWorkshop"), nameof(CycleStartPrefix), nameof(CyclePostfix), null);
            patch("place cyklu gracza", AccessTools.Method(beh, "TickOneProductionCycleForPlayerWorkshop"), nameof(CycleStartPrefix), nameof(CyclePostfix), null);
            _fairWired = done.Contains("cena sprawiedliwa: wsad z targu") && done.Contains("cena sprawiedliwa: wsad z magazynu") && done.Contains("cena sprawiedliwa: wyrob do miasta")
                         && done.Contains("place cyklu notabla") && done.Contains("place cyklu gracza");
            // 147: rzemieslnicy BK ida przez te same latki cyklu notabla i wyrobu do miasta
            string artisans = ArtisanInputs.Wire(done.Contains("place cyklu notabla"), done.Contains("cena sprawiedliwa: wyrob do miasta"));
            patch("wynik doby", AccessTools.Method(beh, "RunTownWorkshop"), nameof(RunPrefix), nameof(RunPostfix), null);
            patch("wydatek dzienny", AccessTools.Method(beh, "HandleDailyExpense"), nameof(ExpensePrefix), null, null);
            patch("kapital startowy", AccessTools.Method(typeof(Workshop), "InitializeWorkshop"), null, nameof(InitPostfix), null);
            patch("kapital przy zmianie wlasciciela", AccessTools.Method(typeof(Workshop), "ChangeOwnerOfWorkshop"), nameof(OwnerPrefix), nameof(OwnerPostfix), null);
            patch("sprzedaz gracza", AccessTools.Method(typeof(ChangeOwnerOfWorkshopAction), "ApplyInternal"), nameof(SalePrefix), nameof(SalePostfix), null);
            patch("zmiana produkcji", AccessTools.Method(typeof(ChangeProductionTypeOfWorkshopAction), "Apply"), nameof(ConvertPrefix), nameof(ConvertPostfix), null);
            ConvertWired = done.Contains("zmiana produkcji");   // E3-D3: przezbrojenie za cena tylko, gdy koszt sprzetu idzie do kasy miasta (bez latki - gra kasuje go w nicosc)
            var model = typeof(DefaultWorkshopModel);

            patch("prog kapitalu", AccessTools.PropertyGetter(model, "CapitalLowLimit"), null, nameof(LowLimitPostfix), null);
            patch("wydatek w ekranie rodu", AccessTools.PropertyGetter(model, "DailyExpense"), null, nameof(DailyExpensePostfix), null);
            patch("cena dla gracza", AccessTools.Method(model, "GetCostForPlayer"), null, nameof(CostPlayerPostfix), null);
            patch("cena dla notabla", AccessTools.Method(model, "GetCostForNotable"), null, nameof(CostNotablePostfix), null);
            patch("koszt sprzetu", AccessTools.Method(model, "GetConvertProductionCost"), null, nameof(ConvertCostPostfix), null);
            // UWAGA: zadnej latki na DefaultClanFinanceModel przy starcie gry (konstruktor statyczny czyta Game.Current) - patrz ApplyInCampaign
            _harmony = h;
            var s = Settings.Current;
            Log.Info("WorkshopTrade: warsztaty towarowe w nowej monecie " + (On ? "CZYNNE" : (s != null && s.WorkshopTradeEnabled ? "wylaczone (ceny historyczne wylaczone)" : "wylaczone w MCM"))
                     + " - wpiete: " + (done.Count > 0 ? string.Join(", ", done.ToArray()) : "nic") + "; stala 200 podmieniona w " + gates + " z 2 metod"
                     + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "") + "; latka modelu finansow rodu dojdzie w kampanii"
                     + (s != null ? ". Utrzymanie " + N1(Upkeep) + " d na dobe do kasy miasta (gra: 100 w nicosc), place cyklu " + (s.WorkshopTradeBatchWages ? "wlaczone" : "wylaczone")
                                    + ", kapital startowy warsztatu czysto towarowego " + s.WorkshopTradeStartCapital + " (gra: 10000; typy z uzbrojeniem zostaja przy 10000), prog kiesy gracza " + s.WorkshopTradeLowCapital + " (gra: 5000), sprzet x"
                                    + N1(EquipScale * 100f) + "%, cena = " + N1(s.WorkshopTradePriceYears) + " x roczny zysk po podatku + kapital"
                                    + "; " + (!_fairWired ? "cena kupca (241) NIECZYNNA - brak latki pomiaru (BRAK wyzej), warsztat bierze pelna cene miasta"
                                              : s.WorkshopTradeFairPrice ? "cena kupca (241) WLACZONA - za sztuke najwyzej srednia sprzedaz miasta z " + MarketSales.Window + " dob albo cena graniczna ludu; rzemieslnikom udzial "
                                                                           + Math.Max(0f, Math.Min(1f, s.TownFolkCraftWageShare)).ToString("0.##", CultureInfo.InvariantCulture) + " zaplaty"
                                                                           + (s.WorkshopTradeBatchWages ? "" : " (NIECZYNNY - place cyklu wylaczone)") + "; prog cyklu gry wedlug ceny kupca: " + (_gateItems ? "wpiety" : "BRAK")
                                              : "cena kupca (241) wylaczona (pelna cena miasta)") + "." : ""));
            Log.Info(artisans);
        }
    }

    /// <summary>Zdarzenia i zapis paczki "warsztaty towarowe": linia dnia, rozmowa kupna, srednie zysku w sejwie (klucz arm_wstrade).</summary>
    internal sealed class WorkshopTradeBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DayProfile.Dx("MarketSales.Daily", MarketSales.Daily));     // 241: linia "Sprzedaz miast (241)" (sam log - okno 28 dob przesuwa sie samo z doba)
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DayProfile.Dx("WorkshopTrade.Daily", WorkshopTrade.Daily));   // E8/0: opakowanie pomiaru (tylko log)
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HeroOrPartyTradedGold.AddNonSerializedListener(this, WorkshopTrade.OnGold);   // 123: pomiar kies notabli (tylko licznik)
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DayProfile.Dx("ArtisanInputs.Daily", ArtisanInputs.Daily));           // 147: linia dnia rzemieslnikow BK
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DayProfile.Dx("SmallFixes8.Daily", SmallFixes8.Daily));               // E8/B (258): linia dnia "Kowadlo i drobne (E8/B)" (sam log)
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DayProfile.Dx("WorkshopPace.Daily", WorkshopPace.Daily));             // E3-D2 (noc-ekonomia3 czesc 2): linia "Tempo warsztatow (E3-D2)"
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DayProfile.Dx("WorkshopRetool.Daily", WorkshopRetool.Daily));         // E3-D3: krok "warsztat zmienia branze za cena" i jego linia
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DayProfile.Dx("WorkshopStartup.Daily", WorkshopStartup.Daily));       // R3-11b: linia "Warsztaty - rozruch rynku (R3-11b)"
            CampaignEvents.OnItemProducedEvent.AddNonSerializedListener(this, WorkshopFairScore.OnItemProduced);   // F4 (noc 17/18.10): wsad regionu - produkcja wsi i miast na dobe (tylko licznik, przy kluczu Workshop Retool Fair Score)
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            WorkshopTrade.ApplyInCampaign();
            WorkshopTrade.ApplyClanScreen();   // E8/B (258, LAWA 6): "Daily Wage" warsztatu w ekranie rodu z poziomem plac miasta (sam napis)
            try { WorkshopTrade.ApplyPaceInCampaign(); WorkshopPace.SessionStart(); } catch (Exception e) { Log.Error("WorkshopPace.SessionStart", e); }   // E3-D2: latka szybkosci czynnego modelu (raz na proces) i linia startu
            try { WorkshopRetool.SessionStart(); } catch (Exception e) { Log.Error("WorkshopRetool.SessionStart", e); }   // E3-D3: linia startu
            try { MarketSales.SessionStart(); } catch (Exception e) { Log.Error("MarketSales.SessionStart", e); }       // 241: wiersze z zapisu do przegrodek dob i linia startu
            // 124: kapitalu startowego NIE tu - ten sluchacz idzie przed ArmouryBehavior (gra wola od ostatnio dopisanego), czyli przed
            // HistoricalPrices.Apply; SeedNewCampaign wola ArmouryBehavior.OnSessionLaunched zaraz po RawPrice.SeedNewCampaign
            try
            {
                // ta sama para stanow co linia gry "workshop_notable_owner_answer_1_single" (priorytet 100) - nasza idzie pierwsza, gdy paczka jest czynna
                starter.AddDialogLine("arm_ws_owner_answer_single", "workshop_owner_notable_single_response", "workshop_notable_player_buy_options",
                                      "{=!}{ARM_WS_OFFER}", WorkshopTrade.OfferCondition, null, 200);
                // 305b (WorkshopRetoolSettlesCapital): potwierdzenie zmiany produkcji ze sprzetem i kapitalem - ta sama para stanow co linia gry "workshop_46" (priorytet 100)
                starter.AddDialogLine("arm_ws_retool_confirm", "player_change_production_3", "player_change_production_5",
                                      "{=!}{ARM_WS_RETOOL}", WorkshopTrade.RetoolCondition, null, 200);
            }
            catch (Exception e) { Log.Error("WorkshopTrade.OnSessionLaunched", e); }
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                string data = dataStore.IsSaving ? WorkshopTrade.Export() : null;
                SaveText.Sync(dataStore, "arm_wstrade", ref data);   // 161: dlugi napis w kawalkach
                if (dataStore.IsLoading) WorkshopTrade.Import(data);
            }
            catch (Exception e) { Log.Error("WorkshopTrade.SyncData", e); }
            // E8/B (258): liczniki welny z uboju rzemieslnikow BK (ulamek sztuki na osade, ArtisanInputs) - wlasny try; stary zapis bez klucza = puste liczniki
            try
            {
                string wool = dataStore.IsSaving ? ArtisanInputs.ExportWool() : null;
                SaveText.Sync(dataStore, "arm_wool_frac", ref wool);
                if (dataStore.IsLoading) ArtisanInputs.ImportWool(wool);
            }
            catch (Exception e) { Log.Error("WorkshopTrade.SyncData(arm_wool_frac)", e); }
            // f4-ksiega-i-skory (noc 17/18.10): ulamki skory z uboju w miescie wedlug tabeli swiata (KsHides; osada=ulamek, najwyzej 4096 wpisow) - wlasny try;
            // stary zapis bez klucza = puste liczniki, starsza DLL klucza nie czyta
            try
            {
                string hide = dataStore.IsSaving ? KsHides.Export() : null;
                SaveText.Sync(dataStore, "arm_hide_frac", ref hide);
                if (dataStore.IsLoading) KsHides.Import(hide);
            }
            catch (Exception e) { Log.Error("WorkshopTrade.SyncData(arm_hide_frac)", e); }
            // F4 (noc 17/18.10, paczka f4-warsztaty): pomiar wsadu regionu (srednia produkcji wsi i miast na dobe wedlug miasta targowego) - wlasny try; przy wylaczonym kluczu
            // nic nie zapisujemy (zapis jak dotad); stary zapis bez klucza = pomiar od zera (pierwszy tydzien bez zmian typu); starsza DLL klucza nie czyta
            try
            {
                if (dataStore.IsSaving)
                {
                    string meas = WorkshopFairScore.Export();
                    if (meas != null) SaveText.Sync(dataStore, "arm_wsinput", ref meas);
                }
                else if (dataStore.IsLoading)
                {
                    string meas = null;
                    SaveText.Sync(dataStore, "arm_wsinput", ref meas);
                    WorkshopFairScore.Import(meas);
                }
            }
            catch (Exception e) { Log.Error("WorkshopTrade.SyncData(arm_wsinput)", e); }
            // 241: sprzedaz miast z 28 dob (sumy okna na komorke miasto x kategoria) - wlasny try; stary zapis bez klucza = okno puste (cena graniczna ludu, az przyjdzie
            // sprzedaz); starsza DLL klucza nie czyta
            try
            {
                string ms = dataStore.IsSaving ? MarketSales.Export() : null;
                SaveText.Sync(dataStore, "arm_mktsales", ref ms);
                if (dataStore.IsLoading) MarketSales.Import(ms);
            }
            catch (Exception e) { Log.Error("WorkshopTrade.SyncData(arm_mktsales)", e); }
        }
    }
}
