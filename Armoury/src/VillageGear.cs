using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 435-L3 - LANCUCH PODAZY (LP), krok 3: SPRZET PODSTAWOWY WSI. Projekt PROJEKT-LANCUCH-po-krytyce (11.11) rozdz. II.1-II.5, II.11, II.12, wiersz L3. Polecenie Jeffa:
    /// "inwestycja + ludzie daje 100% efektu, sami ludzie bez np. narzedzi pracuja mniej wydajnie, a niektore galezie wrecz wymagaja inwestycji, np. kopalnie".
    /// Klucz Village Gear (NIE = m = 1 wszedzie, stan w zapisie zamrozony, kod co do bitu jak przed 435-L3).
    ///
    /// REGULA: produkcja zajecia czesci okregu = dotychczasowa x m x (1 + b); b - dodatek prac okregu (DistrictWorks, bez zmian),
    ///   m = phi + (1 - phi) x min(1, K / H) - nigdy ponad 1 (krytyka B4: nadmiar sprzetu jest bezczynny, nie daje premii za odejscie ludzi);
    ///   H - ludzie czesci w domu (ksiega wiosek), srednia ruchoma 7 dob (tabory i pobor nie szarpia m);
    ///   K - sprzet czesci w jednostkach "wyposazony czlowiek w domu" (ta sama miara co H; rece zajec to staly ulamek ludzi);
    ///   phi - wydajnosc reki bez sprzetu wedlug rodzaju zajecia (tabela nizej; stado i stadniny poza LP: m = 1 - ich kapitalem jest stado).
    ///   Stosunek K / H jest jeden na czesc (kwantowany co 0.01; od 0.98 w gore liczony jak 1 - pas martwy na tabory, pobor i podzial ludzi miedzy czesci); m liczy sie na zajecie czesci (blok KS ma mieszanke zajec, wioska w przebudowie dwa).
    /// BILANS OTWARCIA (jedyny sprzet z niczego, jawny - linia i znacznik w zapisie): czesc bez wpisu dostaje K0 = (ludzie w domu + poza domem) x (1 - L x osady puste
    ///   z rabunku / osady); przez 7 dob od wpisu K0 jest domykane do tej samej liczby (wozy i pobor w drodze w dobie wpisu), o ile nie przybylo osad pustych z rabunku.
    ///   Skutek: m = 1 wszedzie tam, gdzie ludzie sa w domu - produkcja swiata w dobie wlaczenia jak dzien wczesniej.
    /// SKAD SPRZET: z PRAWDZIWEGO materialu robot gminy (TownFolk.DaRunOne - zlecenie z kroku 5 doby wsi, ta sama kolejka, te same pieniadze i te same sztuki z polki
    ///   miasta targowego co dotad): material zaplacony przez wies (polka targu + wytworcy 363 bez frachtu) jest najpierw sprzetem dla rak bez sprzetu (priorytet a),
    ///   dK = material / (KM x D x wartosc dnia czlowieka), reszta - roboty gminy bez skutku jak dotad. Zloto i sztuki plyna DOKLADNIE jak przed zmiana (ksiegi robot gminy
    ///   bez zmian); zmienia sie tylko kolejnosc zlecen (wsie z rekami bez sprzetu pierwsze do polki) i to, ze material ma skutek. Brak materialu = brak jednostek.
    /// OGIEN: ta sama regula co prace okregu (DistrictWorks.Diff) - osady oproznione rabunkiem zabieraja L x K x (nowe puste / stojace przed); reszta sprzetu zostaje
    ///   bezczynna i czeka na ludzi. Glod niczego nie niszczy.
    /// BEZ ZUZYCIA (L5), bez zmian w przebudowie zajec (L4: sprzet idzie za czescia, nie za zajeciem), bez fundatorow (L7). Pola zapisu "zaliczka", "stare zajecie",
    ///   "K stare" i doba zuzycia sa w formacie lp1 od razu (puste) - nastepne kroki nie zmieniaja formatu.
    /// DROGA PRODUKCJI: pod KS (okreg zywy z pelna stawka) - StartCalibration.GearKs w rachunku okregu; bez KS - plan zajec VillageCrafts (BuildPlan). Okreg bez drogi
    ///   (rampa KS, zajecia wiosek nieczynne) - m = 1 i bez potrzeby sprzetu (sprzet zamrozony, jak prace okregu bez drogi).
    /// Zapis arm_lp ("lp1|doba wlaczenia|doba zuzycia|czesci..."; zly wpis pominiety i policzony; brak klucza = bilans otwarcia od nowa; starsza DLL ignoruje klucz).
    /// Watek glowny (stan pod Gate); produkcja czyta jedna podmieniana referencje. Wyjatki per okreg liczone (potkniecia), bez globalnego wylacznika.
    ///
    /// 435-L4 - ZMIANA ZAJECIA PRZEZ SPRZET (projekt II.6, wiersz L4; klucz Village Gear On Switch, NIE = jak L3: sprzet idzie za czescia, przebudowa placona jak dotad).
    ///   Jedna regula dla obu drog (ZW bez KS - VillageCrafts, A7 pod KS - StartCalibration.Rule.cs); ta klasa trzyma sprzet, tamte - rece.
    ///   Czesc zmieniajaca zajecie ma TRZY stosy sprzetu: K - zajecia dotychczasowe (rece, ktore zostaja), NewK - nowe zajecie (kupowany od zera), OldK - jeden zapamietany
    ///   rodzaj bezczynnego sprzetu dawnego zajecia (powrot do niego - sprzet pracuje od razu; druga zmiana - starszy odpisany, licznik "odpisane").
    ///   Rece nowego zajecia pracuja z m z NewK / rece przy nowym zajeciu, rece dotychczasowe - z K / reszta rak (czesc okregu pod KS to mieszanka zajec: w = udzial rak
    ///   czesci w zmianie, q = czesc z nich, ktora juz przeszla).
    ///   - galezie OD RAZU (pola, lasy): rece przechodza wedlug okresu przejsciowego bez raty w zlocie (koszt przebudowy 0); dawny koszt przebudowy = sprzet nowego zajecia
    ///     z materialu robot gminy (potrzeba czesci, priorytet a). Miejsce "jedna czesc okregu naraz" trzyma do NewK >= 0.5 x rak w zmianie albo 182 dob od poczatku.
    ///   - galezie ZA SPRZETEM (szyby, sady, lodzie): rata przebudowy jak dotad (kiesa wsi -> PRAWDZIWY material z polki targu + rzemieslnicy), ale koszt = cena sprzetu rak
    ///     w zmianie, material raty jest sprzetem (NewK), a "zaplacone" przebudowy to lustro NewK / rak w zmianie - rece nie przejda ponad oplacony sprzet (ten sam wzor
    ///     postepu co dotad); sprzet z robot gminy liczy sie tak samo (zadnej podwojnej zaplaty). Postoj i porzucenie - regula L2 na tych samych polach.
    ///   - cel poza LP (stado), czesc bez wyceny, okreg bez drogi produkcji, droga prawdziwego materialu nieczynna - przebudowa jak dotad (tryb 0).
    ///   Koniec zmiany: nadwyzka K ponad rece, ktore zostaly, staje sie OldK dawnego zajecia; NewK wchodzi do K, gdy rece nowego zajecia sa wyposazone (>= 0.98) albo po
    ///   182 dobach. Zmiana porzucona przed przejsciem rak: kupiony NewK zostaje jako zapamietany bezczynny.
    ///   Zapis: 7 pol dopisanych po 12 polach lp1 (DLL L3 je pomija; zapis L3 - pola puste). Zloto: L4 nie dodaje ZADNEGO przelewu (rata i roboty gminy plyna dotychczasowa droga).
    ///
    /// 435-L5 - ZUZYCIE (projekt II.3, II.5 pkt 1, II.16, wiersz L5; klucz Village Gear Wear Scale, 0 = jak L4: sprzet sie nie zuzywa, kolejnosc zlecen jak L3).
    ///   Co dobe (BeforeProduction, przed stosunkiem K / H): K <- max(podloga, K x (1 - delta x skala x rampa)^(1/364)); delta - zuzycie roczne rodzaju (tabela GWear),
    ///   dla czesci z mieszanka zajec wazone cena sprzetu zajec (dni rodzaju x wartosc dnia - ta sama waga co cena jednostki); rampa 0 -> 1 przez 364 doby od doby
    ///   wlaczenia zuzycia (naglowek zapisu, pole "doba zuzycia" - bylo w formacie od L3); podloga = 0.5 x rece stosu (samo zuzycie nie zepchnie m ponizej
    ///   phi + (1 - phi) / 2 - hamulec spirali "brak narzedzi -> mniej rudy i drewna -> mniej narzedzi", krytyka B12).
    ///   - zboze w krainie z zima (WinterBite.WinterNow i czynnik krainy WinterRealm > 0 - ten sam warunek co bezpiecznik zboza L2): delta zboza = 0 (plan zimy bez marginesu);
    ///   - sprzet bezczynny zuzywa sie jak pracujacy: nadwyzka K ponad rece (podloga dalej 0.5 x rece) i zapamietany stary sprzet (OldK, bez rak - bez podlogi);
    ///   - sprzet NOWEGO zajecia w zmianie (NewK) nie zuzywa sie do wejscia do sprzetu czesci (najdluzej 182 doby po koncu zmiany): "zaplacone" przebudowy ZA SPRZETEM jest
    ///     lustrem NewK - zuzycie cofaloby postep rak i liczylo postoj L2 (odstepstwo od projektu, swiadome);
    ///   - okreg bez drogi produkcji i czesc w 7 dobach domykania bilansu otwarcia - bez zuzycia (sprzet zamrozony / domykany z niczego).
    ///   ODTWORZENIE (priorytet b): sprzet ubywajacy w pasie martwym (K / H od 0.98 do 1 - m dalej 1) jest potrzeba "odtworzenia": material robot gminy idzie najpierw
    ///   na rece bez sprzetu (a), potem na odtworzenie (b), reszta - roboty bez skutku. Zloto i sztuki bez zmian (ta sama zaplata co dotad).
    ///   KOLEJNOSC ZLECEN (TownFolk.DaWorksRunCore, tylko przy zuzyciu): wsie (a), potem (b), potem reszta; w (a) i (b) malejaco wedlug niedoboru N towaru zajecia
    ///   (SupplySignal.WorldN - pomiar L1 z poprzedniej doby; bez pomiaru N = 1 i kolejnosc dotychczasowa).
    ///   Zapis bez zmian formatu (doba zuzycia w naglowku). Zloto: L5 nie dodaje ZADNEGO przelewu.
    ///
    /// 435-L7 - FUNDATORZY I CZYNSZ OD SPRZETU: VillageGear.Fund.cs (pan wsi AI, kupcy miasta targowego, gracz; zapis arm_lpf). Sprzet od fundatora wchodzi do K czesci
    ///   jak sprzet z robot gminy (kontrola sprzetu linii L3: "+ od fundatorow").
    /// </summary>
    internal static partial class VillageGear
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const string P = "Sprzet wsi (LP, 435-L3): ";
        private const string SaveVer = "lp1";
        private const int OpenDays = 7;                   // domykanie bilansu otwarcia czesci (przyrost ludzi czesci w pierwszych dobach; 435-L3-p: poza domem = uchodzcy, nie wozy)
        private const double HsDays = 7.0;                // srednia ruchoma rak
        private const double Tolerance = 0.98;            // stosunek K / H od tej liczby w gore liczony jak 1 (pas martwy 2% rak)
        internal const string P4 = "Sprzet wsi (LP, 435-L4) zmiana zajecia: ";
        internal const int SlotDays = 182;                // 435-L4: zmiana "od razu" trzyma miejsce w okregu najdluzej tyle dob (projekt II.6)
        private const int SwitchMaxDays = 364 + 364 + 7;  // 435-L4-p: zmiana bez znaku konca z drogi produkcji konczona po tylu dobach
        internal const double SlotGear = 0.5;             // 435-L4: ... albo do sprzetu nowego zajecia dla polowy rak w zmianie
        private static readonly object Gate = new object();

        // ============================================================ tabela rodzajow (projekt II.2; liczby startowe do kalibracji - decyzja Jeffa 3)
        internal const int GPits = 0, GTrees = 1, GBoats = 2, GWoodD = 3, GWoodM = 4, GGrain = 5, GFields = 6, GOut = 7, GN = 8;
        internal static readonly string[] GName = { "SZYBY", "SADY", "LODZIE", "LASY-D", "LASY-M", "POLA-Z", "POLA", "poza LP (stado, stadniny)" };
        internal static readonly double[] GPhi = { 0.15, 0.20, 0.25, 0.50, 0.70, 0.70, 0.60, 1.0 };     // wydajnosc reki bez sprzetu
        internal static readonly double[] GDays = { 90, 60, 45, 14, 14, 14, 14, 0 };                   // cena jednostki w dniach wartosci produkcji czlowieka
        internal static readonly double[] GWear = { 0.12, 0.03, 0.10, 0.12, 0.08, 0.06, 0.06, 0 };     // zuzycie na rok (435-L5: x klucz Village Gear Wear Scale x rampa)
        internal const string P5 = "Sprzet wsi (LP, 435-L5) zuzycie: ";
        internal const double WearRampDays = 364.0;       // 435-L5: rampa zuzycia 0 -> 1 od doby wlaczenia
        internal const double WearFloor = 0.5;            // 435-L5: samo zuzycie nie schodzi ponizej tej czesci rak stosu
        private const double WearMaxYear = 0.95;          // 435-L5: zacisk rocznego ubytku (skala x tabela)

        internal static int GroupOf(string craft)
        {
            switch (craft)
            {
                case "iron_mine": case "silver_mine": case "gold_mine": case "salt_mine": case "clay_mine": case "quarry": case "lime_pit": case "sea_salt": return GPits;
                case "vineyard": case "olive_trees": case "date_farm": case "citrus": return GTrees;
                case "fisherman": case "whaler": case "walrus_hunter": return GBoats;
                case "lumberjack": return GWoodD;
                case "trapper": case "hunter": return GWoodM;
                case "wheat_farm": return GGrain;
                case "flax_plant": case "silk_plant": case "garden": case "spice_garden": return GFields;
                default: return GOut;
            }
        }

        private static int[] _gOf;
        /// <summary>Rodzaj LP zajecia (indeks KsTab); GOut - poza LP albo spoza tabeli.</summary>
        internal static int GroupOfCraft(int c)
        {
            var g = _gOf;
            if (g == null)
            {
                g = new int[KsTab.Crafts.Length];
                for (int i = 0; i < g.Length; i++) g[i] = DistrictWorks.KindOfCraft(KsTab.Crafts[i]) < 0 ? GOut : GroupOf(KsTab.Crafts[i]);
                _gOf = g;
            }
            return c >= 0 && c < g.Length ? g[c] : GOut;
        }

        internal static bool On { get { var s = Settings.Current; return s != null && s.VillageGear; } }
        /// <summary>435-L4: zmiana zajecia przez sprzet czynna (oba klucze). Wylaczona - sprzet idzie za czescia, przebudowa placona jak dotad.</summary>
        internal static bool SwitchOn { get { var s = Settings.Current; return s != null && s.VillageGear && s.VillageGearOnSwitch; } }
        /// <summary>435-L5: zuzycie sprzetu czynne (Village Gear i skala zuzycia ponad 0). Wylaczone - sprzet sie nie zuzywa, kolejnosc zlecen jak L3.</summary>
        internal static bool WearOn { get { var s = Settings.Current; return s != null && s.VillageGear && s.VillageGearWearScale >= WearMinScale; } }
        internal const float WearMinScale = 0.005f;       // 435-L5-p: suwak pokazuje 0.00 dla wartosci ponizej - licza sie jak 0 (jeden prog: WearOn, doba, linia)
        internal const double RepMinShare = 0.005;        // 435-L5-p: do grupy (b) kolejki zlecen liczy sie odtworzenie co najmniej 0.5% rak czesci

        private static ItemCategory[] _catOf; private static bool[] _catTried;
        /// <summary>435-L5: kategoria glownego towaru zajecia (pierwszy towar tabeli P0) - do odczytu niedoboru N; null - brak przedmiotu w grze.</summary>
        private static ItemCategory CatOfCraft(int c)
        {
            if (c < 0 || c >= KsTab.Crafts.Length || c >= KsTab.P0.Length) return null;
            if (_catOf == null) { _catOf = new ItemCategory[KsTab.Crafts.Length]; _catTried = new bool[KsTab.Crafts.Length]; }
            if (_catTried[c]) return _catOf[c];
            _catTried[c] = true;
            try
            {
                var row = KsTab.P0[c];
                if (row != null && row.Length > 0)
                {
                    var it = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemObject>(KsTab.Goods[row[0].Key]) : null;
                    _catOf[c] = it != null ? it.ItemCategory : null;
                }
            }
            catch { _catOf[c] = null; }
            return _catOf[c];
        }
        private static double NOfCraft(int c) { try { return SupplySignal.WorldN(CatOfCraft(c)); } catch { return 1.0; } }
        /// <summary>435-L4: tryb zmiany DO zajecia wedlug rodzaju: 0 - poza LP (przebudowa jak dotad), 1 - od razu (pola, lasy), 2 - za sprzetem (szyby, sady, lodzie).</summary>
        internal static int ModeOfGroup(int g) { return g == GOut ? 0 : (g == GPits || g == GTrees || g == GBoats ? 2 : 1); }

        // ============================================================ stan (zapis arm_lp)
        private sealed class Rec
        {
            public string Dist, Part; public int Craft = -1, OldCraft = -1, Day0 = -1; public double K = -1, Adv, Hs, OldK, SeenBr = -1, SeenBf = -1, Br0;
            // pamiec sesji (wycena co 7 dob, stan doby)
            public int ValDay = int.MinValue, SeenDay = int.MinValue; public double Unit, UnitMat, L, WLp, Ratio = 1, Need; public bool Path;
            // 435-L4 (zapis: 7 pol dopisanych po 12): sprzet NOWEGO zajecia w zmianie zajecia czesci - zajecie nowe i dawne, jednostki, udzial rak czesci w zmianie (w),
            // czesc z nich, ktora przeszla (q), doba poczatku, znak konca zmiany (rece doszly albo porzucona - NewK czeka na wejscie do K)
            public int NewCraft = -1, NewFrom = -1, SwDay = -1; public double NewK, NewW, NewQ; public bool NewEnd;
            // 435-L4 pamiec sesji: wartosc dnia czlowieka czesci (bez m i zimy), material jednostki nowego zajecia, potrzeba, stosunek nowego zajecia (zmieszany z rekami
            // tego zajecia, ktore byly w czesci wczesniej), udzial rak czesci przy nowym zajeciu, rece przy nowym zajeciu
            public double VPer, UnitMatNew, NeedNew, RatioNew = 1, NewTAll, HN;
            // 435-L5 pamiec sesji: zuzycie roczne jednostki czesci wedlug tabeli (wazone cena sprzetu zajec): zajecia poza zbozem i samo zboze (zboze w zimie 0);
            // potrzeba odtworzenia (ubytek w pasie martwym, priorytet b)
            public double WearNG, WearGr, NeedRep;
        }
        private struct NewR { public int Craft; public float Ratio; }
        private static readonly Dictionary<string, List<Rec>> _byDist = new Dictionary<string, List<Rec>>(StringComparer.Ordinal);
        private static int _onDay = -1, _wearDay = -1;
        private static string _loadNote = "nowa kampania albo zapis bez arm_lp (bilans otwarcia w pierwszej dobie klucza)";
        private static int _loadBad;

        // opublikowane dla produkcji: wies gry -> czesc -> stosunek K / H (tylko czesci ponizej 1). Jedna referencja podmieniana (odczyt bez zamka).
        private static volatile Dictionary<Village, Dictionary<string, float>> _pub = new Dictionary<Village, Dictionary<string, float>>();
        // 435-L4: wies gry -> czesc -> (nowe zajecie w zmianie, jego stosunek sprzetu) - wpis dla kazdej czesci ze sprzetem nowego zajecia (takze stosunek 1);
        // okregi, w ktorych skonczona zmiana "od razu" dalej trzyma miejsce (sprzet ponizej polowy rak, przed 182 doba). Jedna referencja podmieniana.
        private static volatile Dictionary<Village, Dictionary<string, NewR>> _pubNew = new Dictionary<Village, Dictionary<string, NewR>>();
        private static volatile HashSet<Village> _holdAfter = new HashSet<Village>();
        // 435-L3-p: stosunki ostatnio zgloszone do przeliczenia okregu (histereza 0.02; watek glowny), wpisy bez czesci w ksiedze dzis, czesci LP bez drogi materialu
        private static readonly Dictionary<Village, Dictionary<string, float>> _noted = new Dictionary<Village, Dictionary<string, float>>();
        private static int _sOrphan, _sNoRoad;

        // sesja
        private static int _main = -1, _preDay = int.MinValue, _st, _stAll;
        private static bool _valuedAll, _startLine;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static double _prevSum = double.NaN; private static int _prevSumDay = int.MinValue;
        // liczniki od ostatniej linii
        private static double _dBuyU, _dBuyGold, _dMatGold, _dOpenU, _dOpenVal, _dBurnU, _dKsKg;
        private static int _dBuyN, _dMatN, _dOpenN, _dBurnN, _dFirst, _dNoPrice, _dChanged, _dValued, _dKsN, _dOff;
        // 435-L4: liczniki od ostatniej linii (zdarzenia zmiany zajecia) i stan ostatniej doby
        private static int _dSwNoUnit, _dSwWait, _dEndTime;   // 435-L4-p: rata wstrzymana bez wyceny, blok czeka na koniec zmiany czesci, koniec po limicie czasu
        private static int _dSw1, _dSw2, _dSwJoin, _dSwNoRec, _dSwNoPrice, _dSwBusy, _dSwOut, _dSwRoad, _dEndDone, _dEndLeft, _dEndBack, _dEndLost, _dFold, _dFoldLate, _dFoldKey, _dBackN, _dWriteN, _dOutN, _dInstN;
        private static double _dBackU, _dWriteU, _dOutU, _dInstU, _dInstGold, _dNewBuyU;
        private static int _sRun1, _sRun2, _sAfter, _sHold, _sFree, _sOldN;
        // 435-L5: liczniki od ostatniej linii (zuzycie w BeforeProduction, odtworzenie i kolejnosc w DaWorksRun) i stan ostatniej doby
        private static double _dWearU, _dWearOldU, _dWearMat, _dRepU, _dRepGold, _sRepNeed, _sWearScale, _sWearRamp;
        private static int _dWearN, _dWearOldN, _dWearFloor, _dWearGrain, _dWearOpen, _dRepN, _dOrdRep, _dOrdHot, _sRepN;
        private static readonly double[] _dWearG = new double[GN];
        private static bool _startLine5;
        private static double _sH1, _sH2, _sHN, _sNewK, _sNeedNew, _sOldK, _sMw;
        private static readonly List<VillageBook.PartView> _book = new List<VillageBook.PartView>();
        private static readonly List<VillageCrafts.PartCraft> _parts = new List<VillageCrafts.PartCraft>();

        /// <summary>Nowa kampania / przed wczytaniem: stan i liczniki od zera (ArmouryBehavior - konstruktor).</summary>
        internal static void Reset()
        {
            lock (Gate)
            {
                _byDist.Clear(); _onDay = -1; _wearDay = -1; _loadBad = 0;
                _loadNote = "nowa kampania albo zapis bez arm_lp (bilans otwarcia w pierwszej dobie klucza)";
                _pub = new Dictionary<Village, Dictionary<string, float>>(); _noted.Clear(); _sOrphan = _sNoRoad = 0;
                _pubNew = new Dictionary<Village, Dictionary<string, NewR>>(); _holdAfter = new HashSet<Village>(); _startLine4 = false;
                _startLine5 = false; _catOf = null; _catTried = null; _sRepNeed = _sWearScale = _sWearRamp = 0; _sRepN = 0;
                _main = Environment.CurrentManagedThreadId; _preDay = int.MinValue; _st = _stAll = 0; _valuedAll = false; _startLine = false; _err.Clear();
                _prevSum = double.NaN; _prevSumDay = int.MinValue; _gOf = null;
                ZeroDay();
                FundReset();                                                       // 435-L7: wierzytelnosci fundatorow i okno tempa od zera
            }
        }

        private static void ZeroDay()
        {
            _dBuyU = _dBuyGold = _dMatGold = _dOpenU = _dOpenVal = _dBurnU = _dKsKg = 0;
            _dBuyN = _dMatN = _dOpenN = _dBurnN = _dFirst = _dNoPrice = _dChanged = _dValued = _dKsN = _dOff = 0; _st = 0;
            _dSwNoUnit = _dSwWait = _dEndTime = 0;
            _dSw1 = _dSw2 = _dSwJoin = _dSwNoRec = _dSwNoPrice = _dSwBusy = _dSwOut = _dSwRoad = _dEndDone = _dEndLeft = _dEndBack = _dEndLost = _dFold = _dFoldLate = _dFoldKey = _dBackN = _dWriteN = _dOutN = _dInstN = 0;
            _dBackU = _dWriteU = _dOutU = _dInstU = _dInstGold = _dNewBuyU = 0;
            _dWearU = _dWearOldU = _dWearMat = _dRepU = _dRepGold = 0; _dWearN = _dWearOldN = _dWearFloor = _dWearGrain = _dWearOpen = _dRepN = _dOrdRep = _dOrdHot = 0;
            Array.Clear(_dWearG, 0, GN);
            _dFundU = 0;                                                           // 435-L7: jednostki od fundatorow (kontrola sprzetu)
        }

        private static void Stumble(string where, Exception e)
        {
            bool first;
            lock (Gate) { _st++; _stAll++; first = _err.Add(where); }
            if (first) { try { Log.Error("VillageGear." + where, e); } catch { } }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static string F1(double v) { return v.ToString("0.0", Inv); }
        private static string F2(double v) { return v.ToString("0.00", Inv); }
        private static string N0(double v) { return Math.Round(v).ToString("0", Inv); }
        private static string D(double v) { return v.ToString("0.####", Inv); }
        private static bool Num(string s, out double v) { return double.TryParse(s, NumberStyles.Float, Inv, out v) && !double.IsNaN(v) && !double.IsInfinity(v); }

        // ============================================================ odczyty dla produkcji (dowolny watek - jedna referencja)
        /// <summary>Okreg ma czesc z niedoborem sprzetu (opublikowana). Sam odczyt.</summary>
        internal static bool Any(Village v)
        {
            if (v == null) return false;
            var m = _pub; var n = _pubNew;
            return ((m.Count > 0 && m.ContainsKey(v)) || (n.Count > 0 && n.ContainsKey(v))) && On;
        }

        /// <summary>435-L4: stosunek sprzetu ZAJECIA czesci - nowe zajecie w zmianie ma wlasny (sprzet nowego zajecia / rece, ktore do niego przeszly); kazde inne
        /// zajecie czesci - stosunek czesci (RatioOf). Bez zmiany zajecia przez sprzet - to samo co RatioOf. Sam odczyt.</summary>
        internal static double RatioOfCraft(Village v, string uid, int craft)
        {
            if (v == null) return 1.0;
            var n = _pubNew;
            if (n.Count > 0)
            {
                Dictionary<string, NewR> d; NewR x;
                if (n.TryGetValue(v, out d) && d != null && d.TryGetValue(DistrictWorks.PartKey(uid), out x) && x.Craft == craft && On) return x.Ratio >= 0f && x.Ratio < 1f ? x.Ratio : 1.0;
            }
            return RatioOf(v, uid);
        }

        /// <summary>Stosunek K / H czesci (wioska po uid, wies gry - null); 1 - bez niedoboru, klucz wylaczony albo okreg bez drogi produkcji. Sam odczyt.</summary>
        internal static double RatioOf(Village v, string uid)
        {
            if (v == null) return 1.0;
            var m = _pub; Dictionary<string, float> d; float k;
            if (m.Count == 0 || !m.TryGetValue(v, out d) || d == null || !d.TryGetValue(DistrictWorks.PartKey(uid), out k) || !On) return 1.0;
            return k >= 0f && k < 1f ? k : 1.0;
        }

        /// <summary>Czynnik sprzetu zajecia przy stosunku K / H czesci: phi + (1 - phi) x stosunek; zajecie poza LP - 1.</summary>
        internal static double MAt(int craft, double ratio)
        {
            if (!(ratio < 1.0)) return 1.0;
            int g = GroupOfCraft(craft);
            if (g == GOut) return 1.0;
            return GPhi[g] + (1.0 - GPhi[g]) * Math.Max(0.0, ratio);
        }

        /// <summary>Czynnik sprzetu m zajecia `craft` czesci okregu (1 - bez niedoboru). Sam odczyt, nie rzuca.</summary>
        internal static double MOf(Village v, string uid, int craft)
        {
            try { double r = RatioOfCraft(v, uid, craft); return r < 1.0 ? MAt(craft, r) : 1.0; }
            catch { return 1.0; }
        }

        /// <summary>StartCalibration.GearKs: kg/d zdjete z produkcji okregu pod KS czynnikiem sprzetu (sam licznik linii).</summary>
        internal static void NoteKs(double kg) { if (kg > 0) lock (Gate) { _dKsKg += kg; _dKsN++; } }

        /// <summary>Droga produkcji czynnika sprzetu: pod KS okreg zywy z pelna stawka (GearKs w rachunku okregu), bez KS plan zajec wiosek (VillageCrafts.BuildPlan).</summary>
        private static bool PathOf(Village v)
        {
            try
            {
                if (StartCalibration.KsDistrictLive(v)) return StartCalibration.GearPathKs(v);   // 435-L3-p (stabilnosc S1): rampa z mapy okregow (jest od wczytania), nie ze stawek doby
                return VillageCrafts.On && !StartCalibration.AppliedFor(v);
            }
            catch { return false; }
        }

        // ============================================================ wycena czesci (co 7 dob na zmiane; nowa czesc i pierwsza doba sesji - od razu)
        /// <summary>Cena jednostki sprzetu czesci: D x wartosc dnia czlowieka (zajecia LP czesci, cena miasta targowego, bez zimy i bez czynnika sprzetu), czesc materialowa
        /// KM (DistrictWorks.KM), strata ognia L (DistrictWorks.KL) - wagi wedlug wartosci dnia zajec. Czesc bez ludzi albo bez wartosci - poprzednia wycena zostaje.</summary>
        private static void Value(Village v, List<Rec> list, int today)
        {
            bool ks;
            if (!VillageCrafts.PartsOf(v, _parts, out ks)) return;
            double winter = 0;
            try { winter = WinterBite.VillageCut(v); } catch { winter = 0; }
            if (!(winter >= 0)) winter = 0; if (winter > 0.95) winter = 0.95;
            foreach (var pc in _parts)
            {
                if (pc == null) continue;
                Rec rec = Find(list, DistrictWorks.PartKey(pc.Uid));
                if (rec == null) continue;
                string uid = pc.Main ? null : pc.Uid;
                double all = 0, lp = 0, price = 0, mat = 0, lw = 0, wNg = 0, wGr = 0;
                for (int c = 0; c < pc.CraftVal.Length && c < KsTab.Crafts.Length; c++)
                {
                    double vc = pc.CraftVal[c];
                    if (!(vc > 0)) continue;
                    int rg = KsTab.RegGood(c);
                    double wq = rg >= 0 && WinterBite.SparesOn && WinterBite.SparedId(KsTab.Goods[rg]) ? 0.0 : winter;
                    double v0 = vc / Math.Max(0.05, MOf(v, uid, c)) / Math.Max(0.05, 1.0 - wq);
                    all += v0;
                    int g = GroupOfCraft(c), kind = DistrictWorks.KindOfCraft(KsTab.Crafts[c]);
                    if (g == GOut || kind < 0) continue;
                    lp += v0; price += GDays[g] * v0; mat += DistrictWorks.KM[kind] * GDays[g] * v0; lw += DistrictWorks.KL[kind] * v0;
                    if (g == GGrain) wGr += GWear[g] * GDays[g] * v0; else wNg += GWear[g] * GDays[g] * v0;   // 435-L5: zuzycie wazone cena sprzetu zajecia
                }
                rec.Craft = pc.Craft; rec.ValDay = today;
                if (all > 0 && !double.IsNaN(all) && !double.IsInfinity(all))
                {
                    rec.WLp = lp / all;
                    if (lp > 0) rec.L = lw / lp;
                    if (price > 0) { rec.WearNG = wNg / price; rec.WearGr = wGr / price; }
                    else { rec.WearNG = 0; rec.WearGr = 0; }                              // 435-L5-p: czesc bez zajec LP (stado) nie zuzywa sprzetu stara stopa - jedna zasada z czescia, ktora nigdy LP nie miala
                    double H = pc.Book != null ? pc.Book.Home : 0;
                    if (H >= 1 && lp > 0) { rec.Unit = price / H; rec.UnitMat = mat / H; }
                    if (H >= 1) rec.VPer = all / H;                                      // 435-L4: wartosc dnia czlowieka czesci - cena sprzetu nowego zajecia przy zmianie
                }
                else
                {
                    // bez wartosci dnia (czesc pusta, zima bez plonu): rodzaj z zajecia czesci, cena z poprzedniej wyceny
                    int g = GroupOfCraft(pc.Craft), kind = pc.Craft >= 0 && pc.Craft < KsTab.Crafts.Length ? DistrictWorks.KindOfCraft(KsTab.Crafts[pc.Craft]) : -1;
                    rec.WLp = g == GOut || kind < 0 ? 0.0 : 1.0;
                    if (kind >= 0) rec.L = DistrictWorks.KL[kind];
                    if (g != GOut && kind >= 0) { rec.WearNG = g == GGrain ? 0.0 : GWear[g]; rec.WearGr = g == GGrain ? GWear[g] : 0.0; }   // 435-L5-p: zawsze z rodzaju zajecia czesci
                    else { rec.WearNG = 0; rec.WearGr = 0; }
                }
            }
        }

        private static Rec Find(List<Rec> list, string part)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Part == part) return list[i];
            return null;
        }

        // ============================================================ doba (przed produkcja)
        /// <summary>ArmouryBehavior.OnDailyTick zaraz po DistrictWorks.BeforeProduction (po VillageBook.Daily, przed StartCalibration.Daily i VillageCrafts.Daily): bilans
        /// otwarcia nowych czesci, ogien, srednia rak, stosunek K / H i potrzeba sprzetu kazdej czesci; okregi ze zmienionym stosunkiem oznaczone do przeliczenia
        /// (KS, plan zajec, wycena prac - jak zmiana b). Linia doby. Nie rzuca.</summary>
        internal static void BeforeProduction()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = Today();
            if (_preDay == today) return;
            if (_main >= 0 && Environment.CurrentManagedThreadId != _main) { _dOff++; return; }   // 435-L3-p: doba poza watkiem glownym nie jest "zuzyta"
            _preDay = today;
            if (!s.VillageGear || !VillageBook.Active) { Unpublish(); return; }
            long t0 = Stopwatch.GetTimestamp();
            int parts = 0, low = 0, dist = 0, noPath = 0;
            double sumHs = 0, sumNeed = 0, idle = 0, value = 0, minR = 1, wM = 0, wH = 0;
            var gH = new double[GN]; var gNeed = new double[GN];
            var pub = new Dictionary<Village, Dictionary<string, float>>();
            var pubNew = new Dictionary<Village, Dictionary<string, NewR>>(); var holdAfter = new HashSet<Village>();   // 435-L4
            bool sw = s.VillageGearOnSwitch;
            // 435-L5: skala zuzycia (0 - nieczynne), rampa od doby wlaczenia, zima (zboze w krainie z zima bez zuzycia)
            double wearScale = Math.Max(0.0, Math.Min(3.0, (double)s.VillageGearWearScale)), wearRamp = 0;
            if (double.IsNaN(wearScale) || wearScale < WearMinScale) wearScale = 0;
            bool winterNow = false;
            if (wearScale > 0) { try { winterNow = WinterBite.WinterNow(); } catch { winterNow = false; } }
            _sRepNeed = 0; _sRepN = 0;
            _sRun1 = _sRun2 = _sAfter = _sHold = _sFree = _sOldN = 0; _sH1 = _sH2 = _sHN = _sNewK = _sNeedNew = _sOldK = _sMw = 0;
            try
            {
                lock (Gate)
                {
                    bool first = !_valuedAll; _valuedAll = true;
                    if (_onDay < 0) _onDay = today;
                    // 435-L3-p (stabilnosc N3): potrzeby wszystkich wpisow od zera - czesc, ktorej dzis nie ma w ksiedze wiosek (zmiana ukladu, wyjatek okregu),
                    // nie trzyma wczorajszej potrzeby (priorytet zlecen i zaliczanie materialu czytaja tylko potrzeby tej doby)
                    foreach (var l0 in _byDist.Values) foreach (var r0 in l0) { r0.Need = 0; r0.NeedNew = 0; r0.NeedRep = 0; }
                    _sOrphan = 0; _sNoRoad = 0;
                    if (wearScale > 0) { if (_wearDay < 0 || _wearDay > today) _wearDay = today; wearRamp = Math.Max(0.0, Math.Min(1.0, (today - _wearDay) / WearRampDays)); }
                    else
                    {
                        // klucz 0: naglowek zapisu jak L4, rampa po ponownym wlaczeniu od zera; 435-L5-p: slad w logu (kalibracja z linii L5 po takiej dobie nieporownywalna)
                        if (_wearDay >= 0 && s.LogEnabled) { try { Log.Info(P5 + "dzien " + today + " | klucz Village Gear Wear Scale 0 - doba wlaczenia zuzycia wyzerowana (byla " + _wearDay + "); po ponownym wlaczeniu rampa od zera."); } catch { } }
                        _wearDay = -1;
                    }
                    _sWearScale = wearScale; _sWearRamp = wearRamp;
                    double wearK = wearScale * wearRamp;
                    foreach (var v in Village.All)
                    {
                        try
                        {
                            if (v == null || v.Settlement == null || v.Settlement.StringId == null) continue;
                            if (!VillageBook.PartsOf(v, _book) || _book.Count < 1) continue;
                            string id = v.Settlement.StringId;
                            List<Rec> list;
                            if (!_byDist.TryGetValue(id, out list)) { list = new List<Rec>(); _byDist[id] = list; }
                            bool val = first || (today + (int)((uint)id.GetHashCode() % 7u)) % 7 == 0;
                            foreach (var p in _book)
                            {
                                string part = DistrictWorks.PartKey(p.Uid);
                                Rec r0 = Find(list, part);
                                if (r0 == null) { list.Add(new Rec { Dist = id, Part = part, Day0 = today }); val = true; }
                                else if (r0.ValDay == int.MinValue) val = true;
                            }
                            if (val) { Value(v, list, today); _dValued++; }
                            bool path = PathOf(v);
                            dist++; if (!path) noPath++;
                            // 435-L3-p (stabilnosc S2): bez drogi materialu (Ds Works Real Material wylaczone, targ bez zywej kiesy ludu, targ-zamek bez zaworu) sprzetu
                            // nie da sie kupic - czynnik nie tnie produkcji (m = 1), sprzet zamrozony, licznik w linii
                            bool road = path && TownFolk.DaMarketLive(v.TradeBound ?? v.Bound);   // ten sam targ co zlecenia robot gminy (TownFolk.DsSurplus)
                            bool grainFree = false;
                            if (wearK > 0 && path && winterNow) { try { grainFree = WinterRealm.OfVillage(v) > 0f; } catch { grainFree = false; } }
                            Dictionary<string, float> d = null;
                            Dictionary<string, NewR> dn = null;
                            foreach (var p in _book)
                            {
                                Rec rec = Find(list, DistrictWorks.PartKey(p.Uid));
                                if (rec == null) continue;
                                double brShare = p.Nw > 1e-9 ? Math.Min(1.0, p.Br / p.Nw) : 0.0;
                                double target = Math.Max(0.0, (p.Home + p.Away) * (1.0 - Math.Max(0.0, Math.Min(1.0, rec.L)) * brShare));
                                if (rec.K < 0)
                                {
                                    // bilans otwarcia czesci
                                    rec.K = target; rec.Hs = p.Home; rec.SeenBr = p.Br; rec.SeenBf = p.Bf; rec.Br0 = p.Br; rec.Day0 = today;
                                    _dOpenU += target; _dOpenN++; _dOpenVal += target * rec.Unit;
                                }
                                else
                                {
                                    if (rec.SeenBr >= 0)
                                    {
                                        // ogien: osady oproznione rabunkiem od ostatniego odczytu (ta sama regula co prace okregu)
                                        double sBefore = Math.Max(0.0, p.Nw - rec.SeenBr - Math.Max(0.0, rec.SeenBf)), dR = p.Br - rec.SeenBr;
                                        // 435-L3-p (recenzja 1): ludzie spalonych osad odeszli od razu - srednia rak H spada o te sama czesc co osady (nie nizej
                                        // niz ludzie w domu dzis); inaczej ocalale rece mialy przez ok. 3 tygodnie m < 1 i wies kupowala sprzet dla rak, ktorych nie ma
                                        if (dR > 1e-6 && sBefore > 1e-6 && rec.Hs > p.Home) rec.Hs = Math.Max(p.Home, rec.Hs * (1.0 - Math.Min(1.0, dR / sBefore)));
                                        if (dR > 1e-6 && sBefore > 1e-6 && rec.K > 0)
                                        {
                                            double lost = Math.Max(0.0, Math.Min(1.0, rec.L)) * rec.K * Math.Min(1.0, dR / sBefore);
                                            if (lost > 0) { rec.K -= lost; _dBurnU += lost; _dBurnN++; FundBurn(rec, lost / (rec.K + lost)); }   // 435-L7: wierzytelnosci fundatorow tej czesci maleja o te sama czesc
                                        }
                                        if (dR > 1e-6 && sBefore > 1e-6 && (rec.NewK > 0 || rec.OldK > 0))
                                        {
                                            // 435-L4: ten sam ogien dla sprzetu nowego zajecia i zapamietanego bezczynnego
                                            double fr = Math.Max(0.0, Math.Min(1.0, rec.L)) * Math.Min(1.0, dR / sBefore), l2 = fr * (Math.Max(0.0, rec.NewK) + Math.Max(0.0, rec.OldK));
                                            if (l2 > 0) { rec.NewK -= fr * Math.Max(0.0, rec.NewK); rec.OldK -= fr * Math.Max(0.0, rec.OldK); _dBurnU += l2; if (!(rec.K > 0)) _dBurnN++; }   // 435-L4-p: czesc liczona raz
                                        }
                                    }
                                    else rec.Br0 = p.Br;
                                    rec.SeenBr = p.Br; rec.SeenBf = p.Bf;
                                    if (today - rec.Day0 < OpenDays && Math.Abs(p.Br - rec.Br0) <= 1e-6 && target > rec.K + 1e-9 && rec.NewCraft < 0 && !(rec.OldK > 0))
                                    {
                                        // domkniecie bilansu otwarcia: przyrost ludzi czesci (w domu + uchodzcy) w pierwszych 7 dobach; 435-L3-p - bez zmiany pustych osad
                                        // w ktorakolwiek strone (powrot uchodzcow po rabunku nie daje sprzetu z niczego)
                                        _dOpenU += target - rec.K; _dOpenVal += (target - rec.K) * rec.Unit; rec.K = target;
                                    }
                                    rec.Hs += (p.Home - rec.Hs) / HsDays;
                                    if (!(rec.Hs >= 0)) rec.Hs = Math.Max(0.0, p.Home);
                                }
                                rec.Path = path; rec.SeenDay = today;
                                bool act = path && road && rec.WLp > 1e-9;
                                if (path && !road && rec.WLp > 1e-9) _sNoRoad++;
                                // 435-L4: rece przy nowym zajeciu w zmianie (0 - bez zmiany przez sprzet); reszta rak czesci pracuje sprzetem K
                                double hN = rec.NewCraft >= 0 ? SwitchDay(v, rec, today, sw) : 0.0;
                                double hE = hN > 0 ? Math.Max(0.0, rec.Hs - hN) : rec.Hs;
                                rec.HN = hN;
                                if (wearK > 0 && path) WearDay(rec, hE, wearK, grainFree, today);   // 435-L5
                                double ratio = hE >= 1.0 ? Math.Min(1.0, rec.K / hE) : 1.0;
                                // pas martwy: do 2% rak bez sprzetu nie liczy sie (tabory, pobor, podzial ludzi okregu miedzy czesci wedlug osad - bez migotania m i przeliczen
                                // okregu co dobe); ponizej - stosunek co 0.01, a zakup uzupelnia do pelna (histereza)
                                ratio = ratio >= Tolerance ? 1.0 : Math.Round(ratio * 100.0) / 100.0;
                                rec.Ratio = act ? ratio : 1.0;
                                rec.Need = act && ratio < 1.0 ? Math.Max(0.0, hE - rec.K) : 0.0;
                                // 435-L5: odtworzenie zuzytego (priorytet b) - ubytek w pasie martwym (m dalej 1); tylko przy czynnym zuzyciu
                                rec.NeedRep = wearScale > 0 && act && !(ratio < 1.0) && hE >= 1.0 && rec.K < hE - 1e-6 ? hE - rec.K : 0.0;
                                if (rec.NeedRep > 0) { _sRepNeed += rec.NeedRep; _sRepN++; }
                                if (rec.Ratio < 1.0) { if (d == null) d = new Dictionary<string, float>(StringComparer.Ordinal); d[rec.Part] = (float)rec.Ratio; low++; if (rec.Ratio < minR) minR = rec.Ratio; }
                                parts++; sumHs += rec.Hs; sumNeed += rec.Need; idle += Math.Max(0.0, rec.K - hE); value += rec.K * rec.Unit;
                                int g = GroupOfCraft(rec.Craft);
                                gH[g] += hE; gNeed[g] += rec.Need;
                                wH += hE; wM += hE * MAt(rec.Craft, rec.Ratio);
                                if (rec.OldK > 1e-9) { _sOldN++; _sOldK += rec.OldK; idle += rec.OldK; }
                                if (rec.NewCraft >= 0)
                                {
                                    // 435-L4: sprzet nowego zajecia - stosunek rak, ktore przeszly (zmieszany z rekami tego zajecia, ktore czesc miala wczesniej), potrzeba
                                    // (rece w zmianie; po koncu zmiany - rece, ktore przeszly), cena materialu jednostki (wartosc dnia czlowieka czesci x dni rodzaju x KM)
                                    int gn = GroupOfCraft(rec.NewCraft), kn = DistrictWorks.KindOfCraft(KsTab.Crafts[rec.NewCraft]);
                                    double hT = rec.NewEnd ? hN : rec.Hs * rec.NewW;
                                    double rn = hN >= 0.5 ? Math.Min(1.0, Math.Max(0.0, rec.NewK) / hN) : 1.0;
                                    rn = rn >= Tolerance ? 1.0 : rn;
                                    double xn = rec.NewTAll > 1e-9 ? Math.Min(1.0, rec.NewW * rec.NewQ / rec.NewTAll) : 1.0;
                                    double rt = xn * rn + (1.0 - xn) * rec.Ratio;
                                    rt = rt >= Tolerance ? 1.0 : Math.Round(rt * 100.0) / 100.0;
                                    rec.RatioNew = path ? rt : 1.0;
                                    // 435-L4-p: bez wartosci dnia (zima bez plonu, pierwsza doba po wczytaniu) zostaje cena z poprzedniej wyceny / z zapisu
                                    if (kn < 0 || gn == GOut) rec.UnitMatNew = 0.0;
                                    else if (rec.VPer > 0) rec.UnitMatNew = DistrictWorks.KM[kn] * GDays[gn] * rec.VPer;
                                    rec.NeedNew = path && hT >= 0.5 && rec.NewK < Tolerance * hT ? Math.Max(0.0, hT - rec.NewK) : 0.0;
                                    if (dn == null) dn = new Dictionary<string, NewR>(StringComparer.Ordinal);
                                    dn[rec.Part] = new NewR { Craft = rec.NewCraft, Ratio = (float)rec.RatioNew };
                                    if (rec.RatioNew < 1.0) { low++; if (rec.RatioNew < minR) minR = rec.RatioNew; }
                                    bool holds = today - rec.SwDay < SlotDays && rec.NewK < SlotGear * hT && hT >= 0.5;
                                    int mode = ModeOfGroup(gn);
                                    if (rec.NewEnd) { _sAfter++; if (holds) { _sHold++; holdAfter.Add(v); } }
                                    else if (mode == 2) { _sRun2++; _sH2 += rec.Hs * rec.NewW; }
                                    else { _sRun1++; _sH1 += rec.Hs * rec.NewW; if (holds) _sHold++; else _sFree++; }
                                    _sHN += hN; _sNewK += rec.NewK; _sNeedNew += rec.NeedNew; _sMw += hN * MAt(rec.NewCraft, rn);
                                    sumNeed += rec.NeedNew; idle += Math.Max(0.0, rec.NewK - hN); value += rec.NewK * (kn >= 0 && gn != GOut ? GDays[gn] * rec.VPer : 0.0);
                                    gH[gn] += hN; gNeed[gn] += rec.NeedNew; wH += hN; wM += hN * MAt(rec.NewCraft, rn);
                                }
                                else { rec.NeedNew = 0; rec.RatioNew = 1; }
                            }
                            if (d != null) pub[v] = d;
                            if (dn != null) pubNew[v] = dn;
                        }
                        catch (Exception e) { Stumble("BeforeProduction(okreg)", e); }
                    }
                }
                // publikacja: okregi ze zmienionym stosunkiem - rachunek KS, plan zajec i wycena prac tej doby od nowa (jak zmiana b prac okregu)
                var old = _pub; var oldNew = _pubNew;
                _pub = pub; _pubNew = pubNew; _holdAfter = holdAfter;
                HashSet<Village> ch = pubNew.Count > 0 || oldNew.Count > 0 ? new HashSet<Village>() : null;   // 435-L4: okreg zmieniony w obu mapach liczony raz
                // 435-L3-p (recenzje 7 / N4): przeliczenie okregu dopiero przy zmianie stosunku o co najmniej 0.02 wobec OSTATNIO ZGLOSZONEGO (nie wczorajszego) -
                // powolne zuzycie albo zakupy po 0.01 na dobe nie wymuszaja rachunku KS co dobe (okreg i tak liczy sie co 7 dob); nowy i znikajacy niedobor - od razu
                foreach (var kv in pub) if (Changed(_noted, kv.Key, kv.Value)) { _noted[kv.Key] = kv.Value; _dChanged++; DistrictWorks.NoteGearChanged(kv.Key); if (ch != null) ch.Add(kv.Key); }
                foreach (var kv in old) if (!pub.ContainsKey(kv.Key)) { _noted.Remove(kv.Key); _dChanged++; DistrictWorks.NoteGearChanged(kv.Key); if (ch != null) ch.Add(kv.Key); }
                if (ch != null)
                {
                    foreach (var kv in pubNew) if (!ch.Contains(kv.Key) && ChangedNew(oldNew, kv.Key, kv.Value)) { _dChanged++; DistrictWorks.NoteGearChanged(kv.Key); ch.Add(kv.Key); }
                    foreach (var kv in oldNew) if (!pubNew.ContainsKey(kv.Key) && ch.Add(kv.Key)) { _dChanged++; DistrictWorks.NoteGearChanged(kv.Key); }
                }
            }
            catch (Exception e) { Stumble("BeforeProduction", e); }
            try { Line(today, s, dist, noPath, parts, low, sumHs, sumNeed, idle, value, minR, wH > 0 ? wM / wH : 1.0, gH, gNeed, (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency); }
            catch (Exception e) { Stumble("Line", e); }
        }

        private static bool Changed(Dictionary<Village, Dictionary<string, float>> old, Village v, Dictionary<string, float> now)
        {
            Dictionary<string, float> was;
            if (!old.TryGetValue(v, out was) || was == null || was.Count != now.Count) return true;
            foreach (var kv in now) { float x; if (!was.TryGetValue(kv.Key, out x) || Math.Abs(x - kv.Value) > 0.015f) return true; }
            return false;
        }

        private static bool ChangedNew(Dictionary<Village, Dictionary<string, NewR>> old, Village v, Dictionary<string, NewR> now)
        {
            Dictionary<string, NewR> was;
            if (!old.TryGetValue(v, out was) || was == null || was.Count != now.Count) return true;
            foreach (var kv in now) { NewR x; if (!was.TryGetValue(kv.Key, out x) || x.Craft != kv.Value.Craft || Math.Abs(x.Ratio - kv.Value.Ratio) > 1e-4f) return true; }
            return false;
        }

        /// <summary>Klucz wylaczony (albo ksiega wiosek nieczynna): czynnik zdjety - okregi, ktore go mialy, do przeliczenia; stan zamrozony.</summary>
        private static void Unpublish()
        {
            try
            {
                var old = _pub; var oldNew = _pubNew;
                if (_holdAfter.Count > 0) _holdAfter = new HashSet<Village>();
                // 435-L3-p / L5-p: stan zamrozony nie trzyma potrzeb sprzed przerwy (po ponownym wlaczeniu roboty gminy biegna przed przeliczeniem)
                lock (Gate) { foreach (var l0 in _byDist.Values) foreach (var r0 in l0) { r0.Need = 0; r0.NeedNew = 0; r0.NeedRep = 0; } _noted.Clear(); }
                if (old.Count == 0 && oldNew.Count == 0) return;
                _pub = new Dictionary<Village, Dictionary<string, float>>();
                _pubNew = new Dictionary<Village, Dictionary<string, NewR>>();
                foreach (var v in oldNew.Keys) if (!old.ContainsKey(v)) DistrictWorks.NoteGearChanged(v);
                foreach (var v in old.Keys) DistrictWorks.NoteGearChanged(v);
                _prevSum = double.NaN; _prevSumDay = int.MinValue;
                Log.Info(P + "klucz Village Gear wylaczony (albo ksiega wiosek nieczynna) - czynnik sprzetu zdjety z " + old.Count + " okregow (m = 1; z nowym zajeciem w zmianie " + oldNew.Count + "), stan w zapisie zamrozony.");
            }
            catch (Exception e) { Stumble("Unpublish", e); }
        }

        // ============================================================ zakup: material robot gminy (TownFolk.DaRunOne)
        /// <summary>Potrzeba sprzetu okregu w zlocie materialu (rece bez sprzetu x material jednostki) z ostatniej doby; 0 - brak potrzeby albo klucz wylaczony.</summary>
        internal static double NeedGold(Village v)
        {
            if (v == null || v.Settlement == null || !On) return 0.0;
            try
            {
                lock (Gate)
                {
                    List<Rec> list;
                    if (!_byDist.TryGetValue(v.Settlement.StringId, out list)) return 0.0;
                    double g = 0;
                    foreach (var r in list)
                    {
                        if (r.Need > 0 && r.UnitMat > 1e-6) g += r.Need * r.UnitMat;
                        if (r.NeedNew > 0 && r.UnitMatNew > 1e-6) g += r.NeedNew * r.UnitMatNew;   // 435-L4: sprzet nowego zajecia w zmianie (priorytet a)
                    }
                    return g;
                }
            }
            catch { return 0.0; }
        }

        /// <summary>435-L5 (TownFolk.DaWorksRunCore przy czynnym zuzyciu): grupa zlecenia robot wsi - 0: rece bez sprzetu (a), 1: samo odtworzenie zuzytego (b),
        /// 2: bez potrzeby sprzetu; `n` - najwyzszy niedobor N swiata towaru zajecia czesci z potrzeba (1 - bez niedoboru albo bez pomiaru). Nie rzuca.</summary>
        internal static int OrderOf(Village v, out double n)
        {
            n = 1.0;
            if (v == null || v.Settlement == null || !On) return 2;
            try
            {
                lock (Gate)
                {
                    List<Rec> list;
                    if (!_byDist.TryGetValue(v.Settlement.StringId, out list)) return 2;
                    double a = 0, b = 0, na = 1.0, nb = 1.0;
                    foreach (var r in list)
                    {
                        if (r.Need > 0 && r.UnitMat > 1e-6) { a += r.Need * r.UnitMat; na = Math.Max(na, NOfCraft(r.Craft)); }
                        if (r.NeedNew > 0 && r.UnitMatNew > 1e-6) { a += r.NeedNew * r.UnitMatNew; na = Math.Max(na, NOfCraft(r.NewCraft)); }
                        // 435-L5-p (recenzje 1): grupa (b) dopiero od istotnego ubytku (0.5% rak czesci) - inaczej po pierwszej dobie zuzycia bylaby w niej kazda wies
                        if (r.NeedRep > 0 && r.UnitMat > 1e-6 && r.NeedRep >= RepMinShare * Math.Max(1.0, r.Hs)) { b += r.NeedRep * r.UnitMat; nb = Math.Max(nb, NOfCraft(r.Craft)); }
                    }
                    if (a >= 1.0) { n = na; return 0; }
                    if (b >= 1.0) { n = nb; return 1; }
                    return 2;
                }
            }
            catch { n = 1.0; return 2; }
        }

        /// <summary>435-L5: zlecenia z samym odtworzeniem zuzytego (grupa b) i zlecenia grup a / b z niedoborem N > 1 w tym przebiegu (same liczniki).</summary>
        internal static void NoteOrder(int rep, int hot) { if (rep > 0 || hot > 0) lock (Gate) { _dOrdRep += rep; _dOrdHot += hot; } }

        /// <summary>435-L5: zuzycie doby czesci (BeforeProduction, pod Gate): stos K (podloga 0.5 x rece stosu) i zapamietany stary sprzet (bez podlogi). Sprzet nowego
        /// zajecia w zmianie nie zuzywa sie do wejscia do K. `wearK` - skala klucza x rampa.</summary>
        private static void WearDay(Rec rec, double hE, double wearK, bool grainFree, int today)
        {
            if (rec.K < 0) return;
            if (today - rec.Day0 < OpenDays) { _dWearOpen++; return; }           // bilans otwarcia czesci jeszcze domykany
            if (grainFree && rec.WearGr > 0) _dWearGrain++;
            double year = (rec.WearNG + (grainFree ? 0.0 : rec.WearGr)) * wearK;
            if (year > 0 && rec.K > 1e-9)
            {
                double floor = WearFloor * Math.Max(0.0, hE);
                if (rec.K <= floor) _dWearFloor++;
                else
                {
                    double nk = rec.K * Math.Pow(1.0 - Math.Min(WearMaxYear, year), 1.0 / 364.0);
                    if (nk < floor) { nk = floor; _dWearFloor++; }
                    double lost = rec.K - nk;
                    if (lost > 0 && !double.IsNaN(lost))
                    {
                        rec.K = nk; _dWearU += lost; _dWearN++; _dWearMat += lost * rec.UnitMat;
                        _dWearG[GroupOfCraft(rec.Craft)] += lost;
                    }
                }
            }
            if (rec.OldK > 1e-9 && rec.OldCraft >= 0)
            {
                int g = GroupOfCraft(rec.OldCraft);
                double yo = g == GOut || (g == GGrain && grainFree) ? 0.0 : GWear[g] * wearK;
                if (yo > 0)
                {
                    double lost = rec.OldK - rec.OldK * Math.Pow(1.0 - Math.Min(WearMaxYear, yo), 1.0 / 364.0);
                    if (lost > 0 && !double.IsNaN(lost)) { rec.OldK -= lost; _dWearU += lost; _dWearOldU += lost; _dWearOldN++; _dWearG[g] += lost; }
                }
            }
        }

        /// <summary>TownFolk.DaWorksRunCore: zlecenia robot wsi z rekami bez sprzetu postawione przed pozostalymi (sam licznik).</summary>
        internal static void NoteFirst(int n) { if (n > 0) lock (Gate) _dFirst += n; }

        /// <summary>TownFolk.DaRunOne (roboty gminy wsi, nie septy), po zaplacie: `matGold` - PRAWDZIWY material zaplacony przez wies (polka targu + wytworcy 363 bez
        /// frachtu). Najpierw sprzet dla rak bez sprzetu czesci okregu (wedlug potrzeby w zlocie materialu), reszta - roboty gminy jak dotad (bez skutku). Zadnego zlota:
        /// zaplata juz przeszla dotychczasowa droga. Zwraca zloto materialu zaliczone na sprzet. Watek glowny, nie rzuca.</summary>
        internal static long OnWorksMaterial(Village v, long matGold)
        {
            if (matGold <= 0 || v == null || v.Settlement == null || !On) return 0;
            try
            {
                if (_main >= 0 && Environment.CurrentManagedThreadId != _main) { _dOff++; return 0; }
                lock (Gate)
                {
                    _dMatGold += matGold; _dMatN++;
                    List<Rec> list;
                    if (!_byDist.TryGetValue(v.Settlement.StringId, out list)) return 0;
                    double total = 0; bool noPrice = false;
                    foreach (var r in list)
                    {
                        if (r.NeedNew > 0) { if (r.UnitMatNew > 1e-6) total += r.NeedNew * r.UnitMatNew; else noPrice = true; }   // 435-L4
                        if (!(r.Need > 0)) continue;
                        if (r.UnitMat > 1e-6) total += r.Need * r.UnitMat; else noPrice = true;
                    }
                    // 435-L5: odtworzenie zuzytego (priorytet b) - z materialu, ktory zostal po rekach bez sprzetu; tylko przy czynnym zuzyciu (NeedRep inaczej 0)
                    double totalRep = 0;
                    if (WearOn) foreach (var r in list) if (r.NeedRep > 0 && r.UnitMat > 1e-6) totalRep += r.NeedRep * r.UnitMat;
                    if (!(total > 0) && totalRep > 0)
                    {
                        double useR0 = Math.Min(matGold, totalRep), unitsR0 = RepBuy(list, useR0, totalRep);
                        _dBuyU += unitsR0; _dBuyGold += useR0; _dBuyN++;
                        return (long)Math.Round(useR0);
                    }
                    if (!(total > 0)) { if (noPrice) _dNoPrice++; return 0; }
                    double use = Math.Min(matGold, total), units = 0;
                    foreach (var r in list)
                    {
                        if (r.NeedNew > 0 && r.UnitMatNew > 1e-6)
                        {
                            // 435-L4: sprzet nowego zajecia czesci w zmianie - ten sam podzial wedlug potrzeby w zlocie materialu
                            double dn = Math.Min(r.NeedNew, use * (r.NeedNew * r.UnitMatNew / total) / r.UnitMatNew);
                            if (dn > 0) { r.NewK += dn; r.NeedNew -= dn; units += dn; _dNewBuyU += dn; }
                        }
                        if (!(r.Need > 0) || !(r.UnitMat > 1e-6)) continue;
                        double dk = Math.Min(r.Need, use * (r.Need * r.UnitMat / total) / r.UnitMat);
                        if (!(dk > 0)) continue;
                        r.K += dk; r.Need -= dk; units += dk;
                    }
                    if (totalRep > 0 && matGold - use >= 1.0)
                    {
                        double useR = Math.Min(matGold - use, totalRep);
                        units += RepBuy(list, useR, totalRep); use += useR;
                    }
                    _dBuyU += units; _dBuyGold += use; _dBuyN++;
                    return (long)Math.Round(use);
                }
            }
            catch (Exception e) { Stumble("OnWorksMaterial", e); return 0; }
        }

        /// <summary>435-L5: material `use` zl na odtworzenie zuzytego sprzetu czesci okregu, wedlug potrzeby w zlocie materialu (`total`). Zwraca jednostki. Pod Gate.</summary>
        private static double RepBuy(List<Rec> list, double use, double total)
        {
            double units = 0;
            if (!(use > 0) || !(total > 0)) return 0;
            foreach (var r in list)
            {
                if (!(r.NeedRep > 0) || !(r.UnitMat > 1e-6)) continue;
                double dk = Math.Min(r.NeedRep, use * (r.NeedRep * r.UnitMat / total) / r.UnitMat);
                if (!(dk > 0)) continue;
                r.K += dk; r.NeedRep -= dk; units += dk;
            }
            if (units > 0) { _dRepU += units; _dRepGold += use; _dRepN++; }
            return units;
        }

        // ============================================================ 435-L4: zmiana zajecia przez sprzet
        private static string UidOf(string part) { return part == DistrictWorks.PartKey(null) ? null : part; }
        private static double C01(double x) { return x > 0 ? (x < 1 ? x : 1.0) : 0.0; }

        private static Rec RecOf(Village v, string uid)
        {
            List<Rec> list;
            if (v == null || v.Settlement == null || v.Settlement.StringId == null || !_byDist.TryGetValue(v.Settlement.StringId, out list)) return null;
            return Find(list, DistrictWorks.PartKey(uid));
        }

        /// <summary>Rece czesci w zmianie do zajecia `target` z drogi produkcji okregu (pod KS bloki, bez KS wioska poboczna). false - brak danych (stan zostaje).</summary>
        private static bool SwitchQuery(Village v, string part, int target, out bool trans, out double w, out double q, out double tAll)
        {
            trans = false; w = q = tAll = 0;
            try
            {
                string uid = UidOf(part);
                if (StartCalibration.KsDistrictLive(v)) return StartCalibration.GearSwitchOf(v, uid, target, out trans, out w, out q, out tAll);
                return VillageCrafts.GearSwitchOf(v, uid, target, out trans, out w, out q, out tAll);
            }
            catch (Exception e) { Stumble("SwitchQuery", e); return false; }
        }

        /// <summary>Sprzet dawnego zajecia zostaje bezczynny (jeden zapamietany rodzaj); inny zapamietany wczesniej - odpisany; zajecie poza LP (stado) - jednostki
        /// rachunkowe zdjete (to nie byl sprzet LP). Pod Gate.</summary>
        private static void Remember(Rec rec, int craft, double k)
        {
            if (!(k > 1e-9)) return;
            if (craft < 0 || craft >= KsTab.Crafts.Length || GroupOfCraft(craft) == GOut) { _dOutU += k; _dOutN++; return; }
            if (rec.OldCraft == craft) { rec.OldK += k; return; }
            if (rec.OldK > 1e-9) { _dWriteU += rec.OldK; _dWriteN++; }
            rec.OldCraft = craft; rec.OldK = k;
        }

        private static void ClearNew(Rec rec) { rec.NewCraft = -1; rec.NewFrom = -1; rec.SwDay = -1; rec.NewK = rec.NewW = rec.NewQ = rec.NewTAll = rec.NeedNew = rec.HN = 0; rec.NewEnd = false; rec.RatioNew = 1; }

        /// <summary>Sprzet nowego zajecia wchodzi do sprzetu czesci (rece wyposazone, 182 doby albo klucz wylaczony). Pod Gate.</summary>
        private static void Fold(Rec rec)
        {
            if (rec.NewCraft < 0) return;
            rec.K += Math.Max(0.0, rec.NewK);
            if (rec.NewW * rec.NewQ >= 0.5) rec.Craft = rec.NewCraft;
            ClearNew(rec);
        }

        /// <summary>Koniec zmiany zajecia czesci przy czesci rak `q`, ktora przeszla: nadwyzka sprzetu K ponad rece, ktore zostaly, zostaje bezczynna przy dawnym zajeciu;
        /// nic nie przeszlo - kupiony sprzet nowego zajecia zostaje jako zapamietany bezczynny. Pod Gate.</summary>
        private static void EndCore(Rec rec, double q)
        {
            q = C01(q); rec.NewQ = q;
            double hN = rec.Hs * C01(rec.NewW) * q;
            if (hN < 0.5)
            {
                Remember(rec, rec.NewCraft, Math.Max(0.0, rec.NewK));
                ClearNew(rec); _dEndBack++;
                return;
            }
            rec.NewEnd = true;
            double sur = Math.Max(0.0, rec.K - Math.Max(0.0, rec.Hs - hN));
            // 435-L4-p (stabilnosc S2): bezczynny zostaje tylko sprzet rak, ktore ODESZLY (i ich czesc nadwyzki ludzi poza domem) - sprzet ludzi, ktorzy nie zmienili
            // zajecia, tylko sa poza domem (uchodzcy), czeka na nich w K jak w L3
            sur = Math.Min(sur, hN + C01(rec.NewW) * q * Math.Max(0.0, rec.K - rec.Hs));
            if (sur > 1e-9) { double kAll = rec.K; rec.K -= sur; Remember(rec, rec.NewFrom, sur); FundShift(rec, sur / kAll); }
        }

        /// <summary>Doba czesci ze sprzetem nowego zajecia (BeforeProduction, pod Gate): udzial rak w zmianie i postep z drogi produkcji; zmiana zniknela bez znaku konca
        /// (klucz zajec przelaczony, wczytanie) - koniec przy ostatnim znanym postepie; po koncu - wejscie do K, gdy rece wyposazone albo po 182 dobach. Klucz zmiany
        /// wylaczony - sprzet idzie za czescia (jak L3). Zwraca rece przy nowym zajeciu (jednostki: ludzie w domu).</summary>
        private static double SwitchDay(Village v, Rec rec, int today, bool sw)
        {
            if (!sw) { Fold(rec); _dFoldKey++; return 0.0; }
            bool trans; double w, q, tAll;
            if (SwitchQuery(v, rec.Part, rec.NewCraft, out trans, out w, out q, out tAll))
            {
                rec.NewTAll = tAll;
                if (!rec.NewEnd)
                {
                    if (trans) { rec.NewW = C01(w); rec.NewQ = C01(q); }
                    else { EndCore(rec, rec.NewQ); _dEndLost++; }
                }
            }
            // 435-L4-p (stabilnosc N2): droga produkcji trwale nie zna tej zmiany (wioska zniknela, okreg wypadl z mapy) - koniec przy ostatnim znanym postepie po
            // najdluzszym okresie zmiany + roku (inaczej wpis blokowalby kazda inna zmiane tej czesci bez konca)
            if (rec.NewCraft >= 0 && !rec.NewEnd && rec.SwDay >= 0 && today - rec.SwDay > SwitchMaxDays) { EndCore(rec, rec.NewQ); _dEndTime++; }
            if (rec.NewCraft < 0) return 0.0;
            double hN = rec.Hs * C01(rec.NewW) * C01(rec.NewQ);
            if (rec.NewEnd)
            {
                if (hN < 0.5 || rec.NewK >= Tolerance * hN) { Fold(rec); _dFold++; return 0.0; }
                if (today - rec.SwDay >= SlotDays) { Fold(rec); _dFoldLate++; return 0.0; }
            }
            return Math.Min(rec.Hs, hN);
        }

        /// <summary>
        /// Poczatek zmiany zajecia czesci okregu (regula ZW / A7, doba kroku): `w` - udzial rak czesci, ktore zmieniaja zajecie z `cur` na `target`.
        /// Zwraca tryb: 0 - przebudowa jak dotad (klucz wylaczony, cel poza LP, czesc bez wyceny albo bez drogi produkcji, droga prawdziwego materialu nieczynna, czesc
        /// w innej zmianie przez sprzet); 1 - OD RAZU (koszt przebudowy 0, rece wedlug czasu); 2 - ZA SPRZETEM (`cost` - cena sprzetu rak w zmianie w zlocie raty:
        /// material + rzemieslnicy; "zaplacone" przebudowy = lustro sprzetu, SwitchPaid). Zapamietany bezczynny sprzet tego zajecia wraca do pracy od razu. Nie rzuca.
        /// </summary>
        internal static int SwitchBegin(Village v, string uid, int cur, int target, double w, out double cost)
        {
            cost = 0;
            try
            {
                if (!SwitchOn || v == null || target < 0 || target >= KsTab.Crafts.Length) return 0;
                if (_main >= 0 && Environment.CurrentManagedThreadId != _main) { _dOff++; return 0; }
                int g = GroupOfCraft(target), kind = DistrictWorks.KindOfCraft(KsTab.Crafts[target]);
                var s = Settings.Current;
                lock (Gate)
                {
                    if (g == GOut || kind < 0) { _dSwOut++; return 0; }
                    if (!TownFolk.DaWorksOn) { _dSwRoad++; return 0; }
                    Rec rec = RecOf(v, uid);
                    w = C01(w);
                    if (rec == null || rec.K < 0 || !rec.Path || !(rec.Hs * w >= 0.5)) { _dSwNoRec++; return 0; }
                    if (!(rec.VPer > 0)) { _dSwNoPrice++; return 0; }
                    bool join = false;
                    if (rec.NewCraft >= 0)
                    {
                        if (rec.NewEnd) { Fold(rec); _dFold++; }
                        else if (rec.NewCraft != target) { _dSwBusy++; return 0; }
                        else { _dSwBusy++; return 0; }   // 435-L4-p: zmiana do tego zajecia juz w toku (wolajacy sprawdza SwitchBusy przed poczatkiem) - bez dolaczania
                    }
                    if (!join)
                    {
                        rec.NewCraft = target; rec.NewFrom = cur; rec.NewK = 0; rec.NewW = w; rec.NewQ = 0; rec.NewEnd = false; rec.SwDay = Today(); rec.NewTAll = 0; rec.HN = 0;
                        if (rec.OldCraft == target && rec.OldK > 1e-9) { rec.NewK = rec.OldK; _dBackU += rec.OldK; _dBackN++; rec.OldK = 0; rec.OldCraft = -1; }
                    }
                    rec.UnitMatNew = DistrictWorks.KM[kind] * GDays[g] * rec.VPer;
                    rec.NeedNew = Math.Max(0.0, rec.Hs * rec.NewW - rec.NewK);
                    int mode = ModeOfGroup(g);
                    if (mode == 2)
                    {
                        double k = s != null ? Math.Max(0.0, Math.Min(3.0, (double)s.DsWorksWagePerMaterial)) : 0.5;
                        cost = rec.Hs * w * rec.UnitMatNew * (1.0 + k);
                        if (!(cost >= 2.0) || double.IsNaN(cost) || double.IsInfinity(cost)) { cost = 0; mode = 1; }   // ponizej najmniejszej raty (CraftsRebuildPay: 2 zl) - rece wedlug czasu
                    }
                    if (mode == 2) _dSw2++; else _dSw1++;
                    return mode;
                }
            }
            catch (Exception e) { Stumble("SwitchBegin", e); cost = 0; return 0; }
        }

        /// <summary>Tryb zmiany zajecia czesci w toku: 0 - nie przez sprzet (albo klucz wylaczony), 1 - od razu (koszt przebudowy 0), 2 - za sprzetem (koszt > 0). Nie rzuca.</summary>
        internal static int SwitchMode(Village v, string uid, int target, double cost)
        {
            try
            {
                // 435-L4-p (recenzje S1): droga prawdziwego materialu wylaczona w trakcie zmiany - rata idzie dawna droga (bez materialu = bez sprzetu), wiec rachunek
                // przebudowy tez dawny (Paid += zaplacone), nie lustro sprzetu
                if (!SwitchOn || v == null || !TownFolk.DaWorksOn) return 0;
                lock (Gate)
                {
                    Rec rec = RecOf(v, uid);
                    if (rec == null || rec.NewCraft != target || rec.NewEnd) return 0;
                    return cost > 0 ? 2 : 1;
                }
            }
            catch { return 0; }
        }

        /// <summary>435-L4-p (recenzje S1): zmiana ZA SPRZETEM moze dzis brac rate - material raty da sie zaliczyc na sprzet (jest cena materialu jednostki nowego
        /// zajecia). false - bez wyceny (wolajacy nie prosi o rate tej doby: zloto i sztuki nie schodza w proznie; licznik w linii L4). Nie rzuca.</summary>
        internal static bool SwitchCanPay(Village v, string uid, int target)
        {
            try
            {
                if (!SwitchOn || v == null) return true;
                lock (Gate)
                {
                    Rec rec = RecOf(v, uid);
                    if (rec == null || rec.NewCraft != target || rec.NewEnd) return true;
                    if (rec.UnitMatNew > 1e-6) return true;
                    _dSwNoUnit++;
                    return false;
                }
            }
            catch { return true; }
        }

        /// <summary>435-L4-p (recenzja ksiegi S2): czesc okregu ma juz zmiane przez sprzet do TEGO zajecia w toku - kolejny blok tej czesci (wies gry: 20 blokow) czeka
        /// do jej konca (regula "nowe zajecie = nowy sprzet" liczy rece i sprzet jednej zmiany naraz). Sam odczyt i licznik, nie rzuca.</summary>
        internal static bool SwitchBusy(Village v, string uid, int target)
        {
            try
            {
                if (!SwitchOn || v == null) return false;
                lock (Gate)
                {
                    Rec rec = RecOf(v, uid);
                    if (rec == null || rec.NewCraft != target || rec.NewEnd) return false;
                    _dSwWait++;
                    return true;
                }
            }
            catch { return false; }
        }

        /// <summary>Miejsce "jedna czesc okregu naraz" zmiany OD RAZU w toku: 0 - to nie zmiana przez sprzet, 1 - trzyma miejsce (sprzet nowego zajecia ponizej polowy rak
        /// w zmianie i przed 182 doba), 2 - miejsce wolne. Nie rzuca.</summary>
        internal static int SlotOf(Village v, string uid, int target)
        {
            try
            {
                if (!SwitchOn || v == null) return 0;
                lock (Gate)
                {
                    Rec rec = RecOf(v, uid);
                    if (rec == null || rec.NewCraft != target || rec.NewEnd) return 0;
                    double hT = rec.Hs * rec.NewW;
                    return Today() - rec.SwDay < SlotDays && hT >= 0.5 && rec.NewK < SlotGear * hT ? 1 : 2;
                }
            }
            catch { return 0; }
        }

        /// <summary>Okreg, w ktorym skonczona zmiana OD RAZU dalej trzyma miejsce (rece przeszly, sprzet nowego zajecia ponizej polowy, przed 182 doba) - stan z poczatku doby.</summary>
        internal static bool HoldsAfter(Village v)
        {
            if (v == null) return false;
            var h = _holdAfter; return h.Count > 0 && h.Contains(v) && SwitchOn;
        }

        /// <summary>Zmiana ZA SPRZETEM (Progress obu drog, co dobe): `matGold` - PRAWDZIWY material raty przebudowy tej doby (TownFolk.CraftsRebuildPay; 0 - sam odczyt)
        /// zaliczony na sprzet nowego zajecia. Zwraca czesc rak w zmianie z oplaconym sprzetem (lustro "zaplacone" przebudowy; sprzet z robot gminy liczy sie tak samo);
        /// -1 - to nie zmiana przez sprzet (wolajacy liczy zaplate jak dotad). Zadnego zlota. Nie rzuca.</summary>
        internal static double SwitchPaid(Village v, string uid, int target, long matGold)
        {
            try
            {
                if (!SwitchOn || v == null) return -1;
                if (_main >= 0 && Environment.CurrentManagedThreadId != _main) { _dOff++; return -1; }
                lock (Gate)
                {
                    Rec rec = RecOf(v, uid);
                    if (rec == null || rec.NewCraft != target || rec.NewEnd) return -1;
                    if (matGold > 0 && rec.UnitMatNew > 1e-6)
                    {
                        double dk = matGold / rec.UnitMatNew;
                        rec.NewK += dk; rec.NeedNew = Math.Max(0.0, rec.NeedNew - dk);
                        _dInstU += dk; _dInstGold += matGold; _dInstN++;
                    }
                    double hT = rec.Hs * rec.NewW;
                    if (!(hT >= 0.5)) return 1.0;
                    double sh = Math.Max(0.0, rec.NewK) / hT;
                    return sh >= Tolerance ? 1.0 : sh;
                }
            }
            catch (Exception e) { Stumble("SwitchPaid", e); return -1; }
        }

        /// <summary>Koniec zmiany zajecia czesci (Progress obu drog): `q` - czesc rak w zmianie, ktora doszla do nowego zajecia (1 - skonczona; porzucona - gdzie doszly).
        /// Wolane PO wyzerowaniu przebudowy u wolajacego. Nie rzuca.</summary>
        internal static void SwitchEnd(Village v, string uid, int target, double q, bool done)
        {
            try
            {
                if (v == null || !SwitchOn) return;   // 435-L4-p: przy wylaczonym kluczu stan zamrozony (Village Gear NIE) albo zwijany przez Fold (On Switch NIE) - bez przestawiania stosow
                if (_main >= 0 && Environment.CurrentManagedThreadId != _main) { _dOff++; return; }
                lock (Gate)
                {
                    Rec rec = RecOf(v, uid);
                    if (rec == null || rec.NewCraft != target || rec.NewEnd) return;
                    // inna czesc rak tej samej czesci okregu (drugi blok wsi gry) dalej w zmianie do tego zajecia - koniec dopiero z ostatnia (doba: SwitchDay)
                    bool trans; double w, q2, tAll;
                    if (SwitchQuery(v, rec.Part, target, out trans, out w, out q2, out tAll) && trans) return;
                    if (done) _dEndDone++; else _dEndLeft++;
                    EndCore(rec, q);
                    // 435-L4-p (stabilnosc N3): miejsce okregu trzymane od razu (migawka HoldsAfter jest z poczatku doby - w dobie kroku druga czesc zaczelaby zmiane)
                    if (rec.NewEnd && rec.NewCraft >= 0)
                    {
                        double hT = rec.Hs * C01(rec.NewW) * C01(rec.NewQ);
                        if (Today() - rec.SwDay < SlotDays && hT >= 0.5 && rec.NewK < SlotGear * hT && !_holdAfter.Contains(v)) { var h = new HashSet<Village>(_holdAfter); h.Add(v); _holdAfter = h; }
                    }
                }
            }
            catch (Exception e) { Stumble("SwitchEnd", e); }
        }

        private static bool _startLine4;
        /// <summary>Linia doby zmiany zajecia przez sprzet (przy kluczu albo gdy jest jeszcze sprzet nowego / dawnego zajecia albo zdarzenie).</summary>
        private static void Line4(int today, Settings s, bool log)
        {
            if (!log) return;
            bool sw = s.VillageGearOnSwitch;
            if (sw && !_startLine4)
            {
                _startLine4 = true;
                Log.Info(P4 + "start | klucz Village Gear On Switch TAK | OD RAZU (LASY-D, LASY-M, POLA-Z, POLA): rece przechodza wedlug okresu przejsciowego bez raty w zlocie, sprzet nowego zajecia "
                         + "z materialu robot gminy (potrzeba czesci), miejsce w okregu trzyma do sprzetu dla " + F2(SlotGear) + " rak w zmianie albo " + SlotDays + " dob | ZA SPRZETEM (SZYBY, SADY, LODZIE): rata przebudowy "
                         + "jak dotad, koszt = cena sprzetu rak w zmianie (material x (1 + rzemieslnicy)), material raty = sprzet, rece nie ponad oplacony sprzet | cel poza LP (stado) i czesc bez wyceny - "
                         + "przebudowa jak dotad | stary sprzet bezczynny przy dawnym zajeciu (jeden rodzaj; powrot - pracuje od razu; drugi - odpisany) | cena jednostki nowego zajecia: wartosc dnia "
                         + "czlowieka czesci x dni rodzaju x czesc materialowa | zloto: L4 nie dodaje zadnego przelewu.");
            }
            if (!LogGate.Lines) return;
            int run = _sRun1 + _sRun2;
            bool any = run + _sAfter + _sOldN > 0 || _dSwNoUnit + _dSwWait + _dEndTime + _dSw1 + _dSw2 + _dSwJoin + _dSwNoRec + _dSwNoPrice + _dSwBusy + _dSwRoad + _dEndDone + _dEndLeft + _dEndBack + _dEndLost + _dFold + _dFoldLate + _dFoldKey + _dWriteN + _dOutN + _dInstN > 0;
            if (!sw && !any) return;
            var sb = new StringBuilder(1100);
            sb.Append(P4).Append("dzien ").Append(today).Append(" | klucz ").Append(sw ? "TAK" : "NIE (sprzet nowych zajec wszedl do sprzetu czesci: " + _dFoldKey + ")")
              .Append(" | w zmianie: od razu ").Append(_sRun1).Append(" czesci (").Append(N0(_sH1)).Append(" rak), za sprzetem ").Append(_sRun2).Append(" (").Append(N0(_sH2)).Append(" rak)")
              .Append(", po koncu zmiany czeka na sprzet ").Append(_sAfter)
              .Append(" | rece przy nowych zajeciach ").Append(N0(_sHN)).Append(", ich sprzet ").Append(N0(_sNewK)).Append(" jedn., brakuje ").Append(N0(_sNeedNew))
              .Append(", m nowych zajec (wazone rekami) ").Append(_sHN > 0 ? (_sMw / _sHN).ToString("0.000", Inv) : "-")
              .Append(" | miejsce w okregu: trzyma ").Append(_sHold).Append(" (sprzet < ").Append(F2(SlotGear)).Append(" rak, przed ").Append(SlotDays).Append(" doba), zwolnione sprzetem albo czasem ").Append(_sFree)
              .Append(" | stary sprzet bezczynny: ").Append(_sOldN).Append(" czesci, ").Append(N0(_sOldK)).Append(" jedn.")
              .Append(" | od poprzedniej linii: zaczete od razu ").Append(_dSw1).Append(", za sprzetem ").Append(_dSw2).Append(", dolaczone do zmiany czesci ").Append(_dSwJoin)
              .Append("; jak dotad (tryb 0): cel poza LP ").Append(_dSwOut).Append(", czesc bez wpisu / drogi / rak ").Append(_dSwNoRec).Append(", bez wyceny ").Append(_dSwNoPrice)
              .Append(", czesc w innej zmianie ").Append(_dSwBusy).Append(", droga prawdziwego materialu nieczynna ").Append(_dSwRoad)
              .Append(" | skonczone ").Append(_dEndDone).Append(", porzucone z rekami przy nowym ").Append(_dEndLeft).Append(", bez rak przy nowym (sprzet zapamietany) ").Append(_dEndBack)
              .Append(", koniec bez znaku ").Append(_dEndLost).Append(", koniec po limicie ").Append(SwitchMaxDays).Append(" dob ").Append(_dEndTime)
              .Append(" | rata ZA SPRZETEM wstrzymana - brak wyceny materialu jednostki (zloto nie schodzi) ").Append(_dSwNoUnit).Append(", blok czeka na koniec zmiany swojej czesci do tego zajecia ").Append(_dSwWait)
              .Append(" | sprzet nowego zajecia do sprzetu czesci: wyposazone ").Append(_dFold).Append(", po ").Append(SlotDays).Append(" dobach ").Append(_dFoldLate)
              .Append(" | kupione: z rat przebudowy ").Append(F1(_dInstU)).Append(" jedn. za ").Append(N0(_dInstGold)).Append(" zl materialu w ").Append(_dInstN).Append(" ratach, z robot gminy ").Append(F1(_dNewBuyU)).Append(" jedn. (w liczbie 'na sprzet' linii L3)")
              .Append(" | wrocil stary sprzet ").Append(F1(_dBackU)).Append(" jedn. (").Append(_dBackN).Append("), odpisane ").Append(F1(_dWriteU)).Append(" jedn. (").Append(_dWriteN)
              .Append("), jednostki zajec poza LP zdjete ").Append(F1(_dOutU)).Append(" (").Append(_dOutN).Append(")")
              .Append(" | kontrola: sprzet w linii L3 (K + nowe + stare; z rat +, odpisane -); zloto - L4 nie rusza (raty w liniach ZW / KS, roboty w 'Akumulator kies').");
            Log.Info(sb.ToString());
        }

        /// <summary>435-L5: linia doby zuzycia sprzetu (przy czynnym zuzyciu albo gdy cos sie zuzylo / odtworzylo od poprzedniej linii).</summary>
        private static void Line5(int today, Settings s, bool log, double sum, double sumHs)
        {
            if (!log) return;
            bool on = s.VillageGearWearScale >= WearMinScale;
            if (on && !_startLine5)
            {
                _startLine5 = true;
                var t = new StringBuilder(700);
                t.Append(P5).Append("start | klucz Village Gear Wear Scale x").Append(F2(_sWearScale)).Append(" | doba wlaczenia zuzycia ").Append(_wearDay).Append(", rampa 0 -> 1 przez ").Append(N0(WearRampDays))
                 .Append(" dob | co dobe K x (1 - zuzycie roczne x skala x rampa)^(1/364), podloga ").Append(F2(WearFloor)).Append(" x rece stosu | zuzycie roczne wedlug rodzaju:");
                for (int g = 0; g < GOut; g++) t.Append(' ').Append(GName[g]).Append(' ').Append(F2(GWear[g])).Append(g < GOut - 1 ? "," : "");
                t.Append(" (czesc z mieszanka zajec: wazone cena sprzetu zajec) | zboze w krainie z zima bez zuzycia | sprzet bezczynny zuzywa sie jak pracujacy (stary zapamietany bez podlogi)")
                 .Append(" | sprzet nowego zajecia w zmianie nie zuzywa sie do wejscia do sprzetu czesci | okreg bez drogi produkcji i czesc w ").Append(OpenDays).Append(" dobach bilansu otwarcia - bez zuzycia")
                 .Append(" | odtworzenie (priorytet b): ubytek w pasie martwym (K / H od ").Append(F2(Tolerance)).Append(" do 1) z materialu robot gminy po rekach bez sprzetu")
                 .Append(" | kolejnosc zlecen robot WSI miedzy soba (septy i zlecenia ludu miast zostaja na swoich miejscach): (a) rece bez sprzetu, (b) odtworzenie od ").Append(F2(100.0 * RepMinShare)).Append("% rak czesci, reszta; w (a) i (b) malejaco wedlug niedoboru N towaru zajecia - zadnego stalego pierwszenstwa rudy ani drewna, tylko pomiar (Supply Chain Log: ")
                 .Append(SupplySignal.On ? "TAK" : "NIE - N = 1, kolejnosc dotychczasowa w grupach").Append(") | zloto: L5 nie dodaje zadnego przelewu.");
                Log.Info(t.ToString());
            }
            if (!LogGate.Lines) return;
            if (!on && !(_dWearU > 0) && !(_dRepU > 0)) return;
            double gs = 0; for (int g = 0; g < GN; g++) gs += _dWearG[g];
            var sb = new StringBuilder(1100);
            sb.Append(P5).Append("dzien ").Append(today).Append(" | klucz x").Append(_sWearScale.ToString("0.000", Inv)).Append(", doba wlaczenia zuzycia ").Append(_wearDay).Append(", rampa ").Append(_sWearRamp.ToString("0.000", Inv))
              .Append(" | zuzyte dzis ").Append(_dWearU.ToString("0.00", Inv)).Append(" jedn. (").Append(sum > 0 ? (100.0 * _dWearU / (sum + _dWearU)).ToString("0.0000", Inv) : "-").Append("% sprzetu; rocznie przy tym tempie ")
              .Append(sum > 0 ? F2(100.0 * 364.0 * _dWearU / (sum + _dWearU)) : "-").Append("%), material po cenie jednostki ").Append(N0(_dWearMat)).Append(" zl, czesci ").Append(_dWearN)
              .Append("; w tym stary bezczynny ").Append(_dWearOldU.ToString("0.00", Inv)).Append(" jedn. (").Append(_dWearOldN).Append(" czesci) | wedlug rodzaju [jedn.]:");
            for (int g = 0; g < GOut; g++) sb.Append(' ').Append(GName[g]).Append(' ').Append(_dWearG[g].ToString("0.00", Inv)).Append(g < GOut - 1 ? "," : "");
            sb.Append(" | podloga ").Append(F2(WearFloor)).Append(" x rece zatrzymala ").Append(_dWearFloor).Append(" czesci, zboze w krainie z zima bez zuzycia ").Append(_dWearGrain)
              .Append(" czesci, w bilansie otwarcia (bez zuzycia) ").Append(_dWearOpen)
              .Append(" | odtworzenie (priorytet b): potrzeba ").Append(F1(_sRepNeed)).Append(" jedn. w ").Append(_sRepN).Append(" czesciach (").Append(sumHs > 0 ? F2(100.0 * _sRepNeed / sumHs) : "-")
              .Append("% rak), kupione od poprzedniej linii ").Append(F1(_dRepU)).Append(" jedn. za ").Append(N0(_dRepGold)).Append(" zl materialu w ").Append(_dRepN).Append(" okregach (w liczbie 'na sprzet' linii L3)")
              .Append(" | kolejnosc zlecen robot: (a) rece bez sprzetu ").Append(_dFirst).Append(", (b) samo odtworzenie ").Append(_dOrdRep).Append(", z niedoborem N > 1 towaru zajecia ").Append(_dOrdHot)
              .Append(" | N swiata (wczoraj): tools ").Append(F2(NOfId("tools"))).Append(", iron ").Append(F2(NOfId("iron"))).Append(", hardwood ").Append(F2(NOfId("hardwood")))
              .Append(" | POKRYCIE zuzycia: zuzyte dzis ").Append(N0(_dWearMat)).Append(" zl materialu wobec kupionego na sprzet od poprzedniej linii ").Append(N0(_dBuyGold)).Append(" zl = ")
              .Append(_dBuyGold > 0 ? F2(_dWearMat / _dBuyGold) : (_dWearMat > 0 ? "bez zakupow" : "-")).Append(" (ponad 1 trwale = sprzet swiata schodzi do podlogi; skale podnosic tylko przy <= 1)")
              .Append(" | tozsamosc licznikow (nie kontrola): zuzyte wedlug rodzaju - zuzyte razem = ").Append(F2(gs - _dWearU)).Append("; prawdziwa kontrola sprzetu: linia L3 (dzis - wczoraj - kupione + zuzyte + spalone = 0); zloto - L5 nie rusza.");
            Log.Info(sb.ToString());
        }

        private static double NOfId(string id)
        {
            try { var it = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemObject>(id) : null; return it != null ? SupplySignal.WorldN(it.ItemCategory) : 1.0; }
            catch { return 1.0; }
        }

        // ============================================================ linia doby
        private static void Line(int today, Settings s, int dist, int noPath, int parts, int low, double sumHs, double sumNeed, double idle, double value,
                                 double minR, double mAvg, double[] gH, double[] gNeed, double ms)
        {
            double sum = 0; int recs = 0;
            lock (Gate) foreach (var l in _byDist.Values) foreach (var r in l) if (r.K >= 0) { sum += r.K + Math.Max(0.0, r.NewK) + Math.Max(0.0, r.OldK); recs++; if (r.SeenDay != today) _sOrphan++; }
            bool log = s.LogEnabled;
            if (log && !_startLine)
            {
                _startLine = true;
                var t = new StringBuilder(600);
                t.Append(P).Append("start | klucz Village Gear TAK | doba wlaczenia ").Append(_onDay).Append(", zuzycie (L5, klucz Village Gear Wear Scale): ").Append(s.VillageGearWearScale > 0f ? "x" + F2(s.VillageGearWearScale) : "0 - sprzet sie nie zuzywa").Append(" (doba wlaczenia zuzycia ").Append(_wearDay).Append(") | zapis: ").Append(_loadNote)
                 .Append(_loadBad > 0 ? ", zlych wpisow " + _loadBad : "").Append(" | regula: produkcja zajecia czesci x m, m = phi + (1 - phi) x min(1, K / H); H = ludzie czesci w domu (srednia 7 dob), ")
                 .Append("K = sprzet w jednostkach 'wyposazony czlowiek'; stosunek kwantowany co 0.01, od 0.98 w gore liczony jak 1 | tabela (phi; cena jednostki w dniach wartosci produkcji czlowieka):");
                for (int g = 0; g < GOut; g++) t.Append(' ').Append(GName[g]).Append(' ').Append(F2(GPhi[g])).Append("; ").Append(N0(GDays[g])).Append(g < GOut - 1 ? "," : "");
                t.Append(" | stado i stadniny poza LP (m = 1) | sprzet tylko z prawdziwego materialu robot gminy (te same zlecenia i przelewy co dotad; wsie z rekami bez sprzetu pierwsze w kolejce)")
                 .Append(" | bilans otwarcia: K0 = ludzie w domu + uchodzcy czesci, minus strata ognia osad pustych z rabunku; przyrost ludzi domykany ").Append(OpenDays).Append(" dob (bez zmiany pustych osad) | zmiana zajecia przez sprzet (L4, klucz Village Gear On Switch): ").Append(s.VillageGearOnSwitch ? "TAK" : "NIE - sprzet idzie za czescia, przebudowa placona jak dotad").Append('.');
                Log.Info(t.ToString());
            }
            if (log && _dOpenN > 0)
                Log.Info(P + "bilans otwarcia sprzetu: dzien " + today + " | nowych czesci " + _dOpenN + ", jednostek " + N0(_dOpenU) + " (z domknieciem wczesniejszych), wartosc " + N0(_dOpenVal)
                         + " zl (cena jednostki z wyceny czesci; czesc bez wyceny liczona 0) | jedyny sprzet z niczego - znacznik: wpis czesci w zapisie arm_lp.");
            if (log && LogGate.Lines)
            {
                var sb = new StringBuilder(1200);
                sb.Append(P).Append("dzien ").Append(today).Append(" | okregow ").Append(dist).Append(" (bez drogi produkcji - m = 1: ").Append(noPath).Append("), czesci ").Append(parts)
                  .Append(", sprzet K ").Append(N0(sum)).Append(" jedn. (wart. ").Append(N0(value)).Append(" zl), rece H ").Append(N0(sumHs)).Append(", bez sprzetu ").Append(N0(sumNeed))
                  .Append(" (").Append(sumHs > 0 ? F2(100.0 * sumNeed / sumHs) : "-").Append("%), bezczynny sprzet ").Append(N0(idle))
                  .Append(" | czesci z m < 1: ").Append(low).Append(", najnizszy stosunek K/H ").Append(F2(minR)).Append(", m srednie wazone rekami ").Append(mAvg.ToString("0.0000", Inv))
                  .Append(" | wedlug rodzaju (rece / bez sprzetu):");
                for (int g = 0; g < GN; g++) sb.Append(' ').Append(GName[g]).Append(' ').Append(N0(gH[g])).Append('/').Append(N0(gNeed[g])).Append(g < GN - 1 ? "," : "");
                sb.Append(" | od poprzedniej linii: material robot gminy ").Append(N0(_dMatGold)).Append(" zl w ").Append(_dMatN).Append(" zleceniach, z tego na sprzet ").Append(N0(_dBuyGold))
                  .Append(" zl = ").Append(F1(_dBuyU)).Append(" jedn. w ").Append(_dBuyN).Append(" okregach, reszta ").Append(N0(_dMatGold - _dBuyGold)).Append(" zl roboty bez skutku (jak dotad)")
                  .Append("; zlecen wsi z potrzeba sprzetu postawionych pierwszych ").Append(_dFirst).Append(", potrzeba bez wyceny ").Append(_dNoPrice)
                  .Append(" | otwarcie ").Append(F1(_dOpenU)).Append(" jedn. (").Append(_dOpenN).Append(" czesci), spalone ").Append(F1(_dBurnU)).Append(" jedn. (").Append(_dBurnN).Append(" czesci)")
                  .Append(" | produkcja: okregow ze zmienionym m (przeliczone: KS, plan zajec, wycena prac) ").Append(_dChanged).Append(", pod KS zdjete ").Append(F1(_dKsKg)).Append(" kg/d w ")
                  .Append(_dKsN).Append(" rachunkach okregow PRZELICZONYCH dzis (reszta okregow tnie dalej wynikiem sprzed dob - to nie suma swiata), wycenionych okregow ").Append(_dValued)
                  .Append(" | czesci LP bez drogi materialu (Ds Works Real Material wylaczone albo targ bez zywej kiesy ludu) - m = 1, sprzet zamrozony: ").Append(_sNoRoad)
                  .Append(", wpisow bez czesci w ksiedze wiosek dzis (bez potrzeby, sprzet stoi w sumie): ").Append(_sOrphan);
                if (_prevSumDay == today - 1 && !double.IsNaN(_prevSum))
                    sb.Append(" | kontrola sprzetu: dzis - (wczoraj + kupione + otwarcie - spalone").Append(_dInstU > 0 || _dWriteU > 0 || _dOutU > 0 ? " + z rat przebudowy - odpisane" : "")
                      .Append(_dWearU > 0 ? " - zuzyte " + F1(_dWearU) : "").Append(_dFundU > 0 ? " + od fundatorow " + F1(_dFundU) : "").Append(") = ")
                      .Append(F2(sum - (_prevSum + _dBuyU + _dOpenU - _dBurnU + _dInstU - _dWriteU - _dOutU - _dWearU + _dFundU)));
                else sb.Append(" | kontrola sprzetu: brak wczorajszej sumy (pierwsza doba sesji albo klucza)");
                sb.Append(" | zloto: sprzet nie rusza zlota (material zaplacony w linii 'Akumulator kies'; 'roboty bez skutku' to z definicji material - na sprzet, nie kontrola)")
                  .Append(" | wpisow w zapisie ").Append(recs).Append(" | koszt ").Append(F2(ms)).Append(" ms | poza watkiem ").Append(_dOff).Append(" | potkniecia ").Append(_st).Append(" (sesja ").Append(_stAll).Append(").");
                Log.Info(sb.ToString());
            }
            try { Line4(today, s, log); } catch (Exception e) { Stumble("Line4", e); }
            try { Line5(today, s, log, sum, sumHs); } catch (Exception e) { Stumble("Line5", e); }
            _prevSum = sum; _prevSumDay = today;
            lock (Gate) ZeroDay();
        }

        // ============================================================ menu okregu (linia informacyjna)
        /// <summary>DistrictMenu: "Gear: X of Y hands equipped (Z% yield). Missing: ..." - tylko przy kluczu i okregu z droga produkcji; null - nic. Nie rzuca.</summary>
        internal static string HeadText(Settlement st)
        {
            try
            {
                if (st == null || st.Village == null || st.StringId == null || !On) return null;
                double hs = 0, with = 0, wm = 0, worst = 0; int kind = -1;
                lock (Gate)
                {
                    List<Rec> list;
                    if (!_byDist.TryGetValue(st.StringId, out list)) return null;
                    foreach (var r in list)
                    {
                        if (!r.Path || !(r.WLp > 1e-9) || r.K < 0 || !(r.Hs >= 1.0)) continue;
                        double hn = r.NewCraft >= 0 ? Math.Max(0.0, Math.Min(r.Hs, r.HN)) : 0.0, he = r.Hs - hn;   // 435-L4: rece przy nowym zajeciu w zmianie licza wlasny sprzet
                        hs += r.Hs; with += Math.Min(r.K, he) + (hn > 0 ? Math.Min(Math.Max(0.0, r.NewK), hn) : 0.0); wm += he * MAt(r.Craft, r.Ratio) + (hn > 0 ? hn * MAt(r.NewCraft, r.RatioNew) : 0.0);
                        if (r.Need > worst && r.Craft >= 0 && r.Craft < KsTab.Crafts.Length) { int k = DistrictWorks.KindOfCraft(KsTab.Crafts[r.Craft]); if (k >= 0) { worst = r.Need; kind = k; } }
                    }
                }
                if (!(hs >= 1.0)) return null;
                string t = "Gear: " + Math.Round(with).ToString("N0", Inv) + " of " + Math.Round(hs).ToString("N0", Inv) + " hands equipped (" + Math.Round(100.0 * wm / hs).ToString("0", Inv) + "% yield).";
                if (kind >= 0 && worst >= 0.5) t += " Missing: " + DistrictWorks.KindName[kind] + ".";
                return t;
            }
            catch (Exception e) { Stumble("HeadText", e); return null; }
        }

        // ============================================================ zapis (arm_lp)
        private static bool IdOk(string x) { return !string.IsNullOrEmpty(x) && x.IndexOf('~') < 0 && x.IndexOf(';') < 0 && x.IndexOf('|') < 0; }
        private static string CraftId(int c) { return c >= 0 && c < KsTab.Crafts.Length ? KsTab.Crafts[c] : "-"; }
        private static int CraftIx(string id) { int c; return id != null && id != "-" && KsTab.CraftIndex.TryGetValue(id, out c) ? c : -1; }

        /// <summary>Zapis: "lp1|doba wlaczenia|doba zuzycia|okreg~czesc~zajecie~K~zaliczka~H~stare zajecie~K stare~doba wpisu~puste rabunek~puste glod~puste rabunek przy wpisie;...".
        /// Bez wpisow i bez doby wlaczenia - "" (nic do wczytania). 435-L4: po 12 polach wpisu 7 dopisanych "~nowe zajecie~K nowe~w~q~doba zmiany~dawne zajecie~koniec"
        /// (DLL L3 czyta pierwsze 12 i reszte pomija; wpis bez nich - bez zmiany zajecia w toku).</summary>
        internal static string Export()
        {
            try
            {
                lock (Gate)
                {
                    if (_onDay < 0 && _byDist.Count == 0) return "";
                    var sb = new StringBuilder(_byDist.Count * 320 + 32);
                    sb.Append(SaveVer).Append('|').Append(_onDay).Append('|').Append(_wearDay).Append('|');
                    bool f = true;
                    foreach (var l in _byDist.Values)
                        foreach (var r in l)
                        {
                            if (r.K < 0 || !IdOk(r.Dist) || !IdOk(r.Part)) continue;
                            if (!f) sb.Append(';'); f = false;
                            sb.Append(r.Dist).Append('~').Append(r.Part).Append('~').Append(CraftId(r.Craft)).Append('~').Append(D(r.K)).Append('~').Append(D(r.Adv)).Append('~').Append(D(r.Hs))
                              .Append('~').Append(CraftId(r.OldCraft)).Append('~').Append(D(r.OldK)).Append('~').Append(r.Day0).Append('~').Append(D(r.SeenBr)).Append('~').Append(D(r.SeenBf))
                              .Append('~').Append(D(r.Br0));
                            if (r.NewCraft < 0) continue;                                       // bez zmiany zajecia w toku - wpis jak L3 (12 pol)
                            sb.Append('~').Append(CraftId(r.NewCraft)).Append('~').Append(D(Math.Max(0.0, r.NewK))).Append('~').Append(D(r.NewW)).Append('~').Append(D(r.NewQ)).Append('~').Append(r.SwDay)
                              .Append('~').Append(CraftId(r.NewFrom)).Append('~').Append(r.NewEnd ? '1' : '0').Append('~').Append(D(Math.Max(0.0, r.UnitMatNew)));   // 435-L4-p: 20. pole - cena materialu jednostki nowego zajecia
                        }
                    return sb.ToString();
                }
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        /// <summary>Wczytanie (po Reset z konstruktora): brak klucza albo pusty - stan pusty (bilans otwarcia w pierwszej dobie klucza); zly wpis pominiety i policzony.</summary>
        internal static void Import(string data)
        {
            try
            {
                lock (Gate)
                {
                    _byDist.Clear(); _onDay = -1; _wearDay = -1; _loadBad = 0;
                    if (string.IsNullOrEmpty(data)) { _loadNote = "zapis bez arm_lp (bilans otwarcia w pierwszej dobie klucza)"; return; }
                    var p = data.Split('|');
                    if (p.Length < 4 || p[0] != SaveVer) { _loadNote = "arm_lp w nieznanej wersji '" + (p.Length > 0 ? p[0] : "") + "' - pominiety (bilans otwarcia od nowa)"; return; }
                    int d;
                    if (int.TryParse(p[1], NumberStyles.Integer, Inv, out d)) _onDay = d;
                    if (int.TryParse(p[2], NumberStyles.Integer, Inv, out d)) _wearDay = d;
                    int n = 0;
                    if (p[3].Length > 0)
                        foreach (var e in p[3].Split(';'))
                        {
                            var a = e.Split('~');
                            double k, adv, hs, ok, sbr, sbf, br0; int day0;
                            if (a.Length < 12 || !IdOk(a[0]) || !IdOk(a[1]) || !Num(a[3], out k) || !Num(a[4], out adv) || !Num(a[5], out hs) || !Num(a[7], out ok)
                                || !int.TryParse(a[8], NumberStyles.Integer, Inv, out day0) || !Num(a[9], out sbr) || !Num(a[10], out sbf) || !Num(a[11], out br0) || k < 0 || hs < 0)
                            { _loadBad++; continue; }
                            List<Rec> list;
                            if (!_byDist.TryGetValue(a[0], out list)) { list = new List<Rec>(); _byDist[a[0]] = list; }
                            if (Find(list, a[1]) != null) { _loadBad++; continue; }
                            var nr = new Rec { Dist = a[0], Part = a[1], Craft = CraftIx(a[2]), K = k, Adv = Math.Max(0.0, adv), Hs = hs, OldCraft = CraftIx(a[6]), OldK = Math.Max(0.0, ok), Day0 = day0,
                                               SeenBr = sbr, SeenBf = sbf, Br0 = Math.Max(0.0, br0) };
                            if (nr.OldCraft < 0) nr.OldK = 0;
                            if (a.Length >= 19 && CraftIx(a[12]) >= 0)
                            {
                                // 435-L4: sprzet nowego zajecia w zmianie; zle pola - wpis czesci zostaje, sama zmiana pominieta i policzona
                                double nk, nw, nq; int sd;
                                if (Num(a[13], out nk) && Num(a[14], out nw) && Num(a[15], out nq) && int.TryParse(a[16], NumberStyles.Integer, Inv, out sd) && nk >= 0)
                                { nr.NewCraft = CraftIx(a[12]); nr.NewK = nk; nr.NewW = Math.Max(0.0, Math.Min(1.0, nw)); nr.NewQ = Math.Max(0.0, Math.Min(1.0, nq)); nr.SwDay = sd; nr.NewFrom = CraftIx(a[17]); nr.NewEnd = a[18] == "1";
                                  double um; if (a.Length >= 20 && Num(a[19], out um) && um > 0) nr.UnitMatNew = um; }   // 435-L4-p: brak pola (zapis L4) - cena z pierwszej wyceny
                                else _loadBad++;
                            }
                            list.Add(nr);
                            n++;
                        }
                    _loadNote = "arm_lp wczytany: czesci " + n + ", doba wlaczenia " + _onDay;
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
