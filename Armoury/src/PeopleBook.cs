using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// KSIEGA LUDZI (projekt etapu 4, R0, U23) - jedyne miejsce stanu ksiegi (klucz zapisu "arm_people") i jedyna droga przesuniecia.
    /// Powstaje w paczce E4/2 (190) z jednym kontem: CIALA - pula Innych wedlug regionu (warownia regionu OutlawLaw, ta sama geografia co
    /// pula wyrzutkow). Konto nie-ludzi: zasilane tylko cialami poleglych w miejscach R13 (za Murem, przy Murze) - nadwyzka ponad miejsce
    /// w bandach z nekromancji, z odbitych jencow i z jencow, ktorych ROT kasuje - oraz bilansem otwarcia nowej kampanii (616 "dawnych umarlych",
    /// Jeff 07.10, STAN-PRAC pyt. 2; RK 4.2 B pkt 8). Wydawane tylko na narodziny bandy Innych (szablon 53 + wodz -> z puli; pusta pula = sam wodz).
    /// Nigdy nie wraca do ludzi (wight nie jest czlowiekiem z niczego).
    /// Do tego licznik POLEGLI (R3) z rak Innych wedlug miejsca - tylko log do E4/7 (smierc zolnierza zdejmuje z DOM dopiero razem z werbunkiem,
    /// ktory zdejmuje rekruta z DOM - R3/R4 w E4/7; trwaly licznik POLEGLI w zapisie - w E4/7).
    /// E4/3 (108n): konta DOM (wsie: hearth x k_v - stan w hearth gry; miasta: stan od E4/4), WYRZUTKI (pula OutlawLaw - stan w "arm_outlaws"),
    /// TABOR (tabory wsi i lodzie - zdjete z DOM przez gre) i SLUZBA (partie, zalogi, milicje, bandy - konto od E4/7) oraz jedyna droga zmiany
    /// hearth za ludzi: Move (PeopleUnit.Shift wola tylko Move). k_v na osade z dnia kalibracji ("v:" w "arm_people", U3) - zmiana kultury osady
    /// nie zmienia ani liczby ludzi, ani wagi czlowieka. Trzy punkty powrotu do DOM (U2): LosersFlee.SendHome, ClanBudget.ToVillage, OutlawLaw.ReturnHome.
    /// E4/4 (109n): DOM miast jako STAN ("t:" w "arm_people" - ludzie miasta zakladani raz: nowa kampania przy kalibracji, stary zapis pierwszej doby
    /// z dobrobytu x k_v tej chwili; zmienia go dopiero E4/8 - migracja) i podloga 10 hearth bez ludzi z niczego (R17): to, co podloga gry/BK dopisze
    /// wsi, schodzi z puli wyrzutkow regionu (od E4/5 najpierw z UCHODZCOW tej wsi); brak - licznik "podloga z niczego".
    /// E4/5 (113n): konto UCHODZCY ("u:" w "arm_people" - wies pochodzenia -> poza domem, zabici przy spustoszeniu, zmarli na koncie) i zegar regionu ("r:" - warownia
    /// regionu -> doba ostatniego rabunku albo zerowania z trafionymi > 0). Trafieni rabunkiem i zerowaniem armii schodza z DOM przez Move (MDevRaid, MDevForage),
    /// konta dopisuje Devastated; powrot (MRefugeeHome) i podloga 10 (MRefugeeFloor, najpierw z uchodzcow tej wsi) schodza z konta; uchodzcy umieraja 1.3%/rok (bez narodzin).
    /// E4/6 (191): smierc z glodu schodzi z DOM przez Move (konto ADead, MFamineDead - ubytek trwaly), uchodzcy z glodu - Move DOM -> UCHODZCY (MFamineFled) i FamineFled
    /// (konto wsi bez zegara rabunku); mieszczanie przy Food 0 - TownDie (stan miasta "t:", licznik MFamineTown). Stan spichlerzy (Famine.cs) w tej samej sekcji zapisu ("f:").
    /// E4/7 (164b-1): konta SLUZBA i JENCY z regionem pochodzenia ("sw:" "so:" "jo:" "wo:" "rg:"), pobor z DOM (MLevyVillage/MLevyA5/MLevyTown), powroty z pochodzeniem
    /// z hearth (MBack*), POLEGLI regionu pochodzenia, wolne rece (FreeHands) - PeopleBook.Origin.cs i PeopleBook.OriginPatches.cs (ta sama klasa, partial).
    /// E4/9 (192): konta NIEWOLNI ("ns:" "nw:") i MUR ("mw:"), liczniki Strazy (sluzba dozywotnia), pojmani z rabunku z konta UCHODZCY - PeopleBook.Canon.cs.
    /// Kolejne konta (NIEWOLNI, WETERANI...) - kazde w swojej paczce, kazde przez te klase.
    /// Zapis tylko przez SaveText.Sync (napis w kawalkach). Stary zapis bez klucza: pula od 0 (bez bilansu otwarcia - bandy w polu zostaja);
    /// zapis bez "v:" - k_v z kultury osady w dniu pierwszego wczytania tej wersji (bez skoku).
    /// </summary>
    internal static partial class PeopleBook
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // przyczyny ruchu na koncie CIALA (indeksy licznikow doby)
        internal const int COpening = 0, CNecro = 1, CRescued = 2, CMilitia = 3, CBirth = 4, CRaid = 5, CKinds = 6;
        private static readonly string[] CName = { "bilans otwarcia", "nadwyzka nekromancji (polegli i jency)", "odbici jency", "milicja przejetej osady", "narodziny band",
            "rajd Innych: zabici przy spustoszeniu (E4/5)" };

        // przyczyny smierci (R3) z rak Innych
        internal const int DCaptives = 0, DRescued = 1, DMilitia = 2, DKinds = 3;
        private static readonly string[] DName = { "jency Innych", "odbici przez Innych", "milicja osady przejetej przez Innych" };
        private static readonly string[] PlaceName = { "za Murem", "przy Murze", "Westeros", "Essos", "miejsce nieznane" };

        /// <summary>Bilans otwarcia puli cial nowej kampanii (Jeff 07.10: 616 dawnych umarlych na start - dzisiejsze 4 bandy x 154).</summary>
        internal const int OpeningBodies = 616;
        private const float OpeningDays = 2f;   // ten sam zegar i ta sama granica co bilans otwarcia CrashScribe (OthersStartDowryDays, RK 4.2 A pkt 1)

        // ---------------------------------------------------------------- stan w zapisie
        private static readonly Dictionary<string, long> _bodies = new Dictionary<string, long>();   // id warowni regionu -> ciala ("" = region nieznany)
        private static bool _seeded;          // bilans otwarcia zalozony (albo stary zapis: od 0)
        private static long _opening;         // ile dal bilans otwarcia (616 albo 0)

        // ---------------------------------------------------------------- liczniki doby (bez zapisu)
        private static readonly long[] _in = new long[CKinds], _out = new long[CKinds];
        private static readonly long[,] _dead = new long[DKinds, 5];
        private static long _deadSess;
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static string _openingNote;

        // ---------------------------------------------------------------- E4/3 (108n): konta ludzi i jedna droga ruchu hearth
        // konta: DOM (wsie: hearth x k_v), WYRZUTKI (pula OutlawLaw), TABOR (tabory wsi i lodzie), SLUZBA (wojsko - konto od E4/7), poza ksiega (zadania gracza)
        internal const int ADom = 0, AOutlaws = 1, ACarts = 2, AService = 3, AOutside = 4, ARefugees = 5;      // UCHODZCY (E4/5): konto wsi pochodzenia
        internal const int ADead = 6;     // E4/6: smierc (ubytek z DOM bez konta docelowego - zmarli z glodu, R3)
        // przyczyny ruchu ludzi (indeksy licznikow)
        internal const int MSeed = 0, MMisery = 1, MRaidFlee = 2, MPoolHome = 3, MCart = 4, MH3Cart = 5, MDisband = 6, MCommon = 7, MSoldier = 8,
                           M166 = 9, MW4c = 10, M179 = 11, MPress = 12, MQuest = 13, MIncident = 14, MFloor = 15,
                           MDevRaid = 16, MDevForage = 17, MRefugeeHome = 18, MRefugeeFloor = 19,
                           // E4/6 (191): glod - zmarli z glodu (DOM -> smierc), uciekli z glodu (DOM -> UCHODZCY), zmarli mieszczanie przy Food 0 (stan miasta)
                           MFamineDead = 20, MFamineFled = 21, MFamineTown = 22,
                           // E4/7 (164b-1): pobor z DOM (wies -1/k, A5) i powroty zolnierzy ze znanym pochodzeniem do DOM regionu pochodzenia (+1/k) - PeopleBook.Origin.cs;
                           // MLevyTown - pobor z miasta (stan miasta E4/4, bez hearth); MBackTown (recenzja E4/7 R1-1) - powrot z pochodzeniem do stanu miasta (bez hearth).
                           // Scalenie E4/6 + E4/7: E4/7 przesuniete o 3 (bylo 20..30, MKinds 31). Numery nie ida do zapisu (liczniki doby), wszystkie uzycia przez nazwy;
                           // MBackH3..MBackDisband musza zostac kolejno (petle w Measure40 i OriginSegment)
                           MLevyVillage = 23, MLevyA5 = 24, MBackH3 = 25, MBack166 = 26, MBackW4c = 27, MBack179 = 28, MBackPool = 29, MBackFreed = 30, MBackDisband = 31,
                           MLevyTown = 32, MBackTown = 33,
                           // E4/8 (164b-2): numery 34-45 (pierwsze wolne po scaleniu E4/6 + E4/7) - wojsko z ludzi
                           // (milicja, nowe druzyny, karawany, konwoje morskie), wedrowcy i konwoje BK (R19), miasta (R2) - PeopleBook.Arms.cs, ArmsFromPeople.cs, TownGrowth.cs
                           MLevyMilitia = 34, MBackMilitia = 35, MBackMilitiaNone = 36, MLevySpawn = 37, MCaravanOut = 38, MCaravanRefill = 39,
                           MRoadOut = 40, MRoadIn = 41, MRoadBack = 42, MRoadNone = 43, MTownMigrate = 44, MTownNatural = 45,
                           // E4/10 (167): weterani niewynajeci po 120 dobach do DOM regionu pochodzenia (z hearth, jak MBack*) i starzy zolnierze z karczm w dniu siewu
                           // (ze stanu ludzi miasta E4/4 - stan miasta, bez hearth). Wejscie SLUZBA -> WETERANI i najem WETERANI -> SLUZBA to nie ruchy DOM (liczniki E4/10).
                           // Scalenie calosci etapu 4: w paczce E4/10 bylo 44/45 (MKinds 46) - przeniesione za E4/8
                           MBackVeteran = 46, MVetStart = 47,
                           // MBackPress (E4/7b, decyzja Jeffa 11.10 12:40) - niewzieci z ekranu lupu poboru wymuszonego gracza wracaja do DOM wsi zdjecia (+1/k).
                           // Scalenie calosci etapu 4: w E4/7b bylo 31 (stara numeracja E4/7 sprzed scalenia z E4/6, MKinds 32) - pierwszy wolny numer za wszystkimi;
                           // poza ciagiem MBackH3..MBackDisband - dopisany osobno w Measure40 (o11), OriginSegment ("powroty z pochodzeniem") i backE47
                           MBackPress = 48,
                           // E4/9 (192): pobor do Strazy z ludnosci Westeros (WatchLaw.Levy - wies -1/k, MHearth); DLUG 4 - odchodzacy z rodu w zajeciu z braku pieniedzy
                           // do domu (ClanBudget.ToVillage: MDebt - czesc bez pochodzenia, tylko licznik; MBackDebt - z pochodzeniem, z hearth). Pierwsze wolne numery
                           MWatchLevy = 49, MDebt = 50, MBackDebt = 51, MKinds = 52;
        private static readonly string[] MName = { "siew puli wyrzutkow (bilans otwarcia)", "bieda i wojna do lasu", "spalona wies do lasu", "z puli do domu (OutlawLaw.ReturnHome)",
            "tabory wsi i lodzie (gra, okna E4/0)", "rozbite tabory do swojej wsi (LosersFlee.SendHome)", "rozwiazane tabory i lodzie do swojej wsi (R18)",
            "prosci i bandyci do wsi (LosersFlee.SendHome -> OutlawLaw.ReturnHome)", "zolnierze do ludnosci BK (LosersFlee.SendHome: rozbici, uwolnieni, I1, pula)",
            "zwolnieni 166 (ClanBudget.ToVillage)", "przepelnienie W4c (ClanBudget.ToVillage)", "rycerze 179 (ClanBudget.ToVillage)",
            "pobor wymuszony gracza", "zadania gracza (hearth gry = 2 ludzi)", "incydenty gracza (zostaja w hearth gry)",
            "podloga 10 hearth z puli wyrzutkow regionu (E4/4 R17; hearth dopisala gra/BK)",
            "spustoszenie: rabunek (trafieni = zabici + pojmani + las + uchodzcy; E4/5)", "spustoszenie: zerowanie armii (trafieni; E4/5)",
            "powrot uchodzcow do swojej wsi (E4/5)", "podloga 10 z uchodzcow tej wsi (E4/5; hearth dopisala gra/BK)",
            "glod: zmarli z glodu (E4/6, R10 - ubytek trwaly)", "glod: uciekli z glodu do regionu z zapasem (E4/6, konto UCHODZCY)",
            "glod: zmarli mieszczanie przy Food 0 (E4/6, R10 - stan miasta)",
            "pobor z wsi (E4/7, BK DeduceManpower)", "pobor A5 - autowerbunek zalogi (E4/7)", "powrot z pochodzeniem: rozbici H3 (E4/7)", "powrot z pochodzeniem: zwolnieni 166 (E4/7)",
            "powrot z pochodzeniem: przepelnienie W4c (E4/7)", "powrot z pochodzeniem: rycerze 179 (E4/7)", "powrot z pochodzeniem: z puli wyrzutkow (E4/7)",
            "powrot z pochodzeniem: jency uwolnieni i wypuszczeni (E4/7)", "powrot z pochodzeniem: rozwiazane partie (E4/7)", "pobor z miasta (E4/7, stan miasta E4/4)",
            "powrot z pochodzeniem do miasta (E4/7, stan miasta E4/4 - dlug poboru z miasta)",
            "milicja z wolnych rak (E4/8)", "milicja bez walki do domu (E4/8)", "milicja bez pochodzenia do domu (E4/8 - bez hearth)",
            "nowa druzyna z wolnych rak regionu siedziby (E4/8)", "karawana z ludzi osady startu (E4/8)", "konwoj morski uzupelniony z miasta (E4/8)",
            "wedrowcy i eskorta BK - wyjazd (R19)", "wedrowcy BK u celu (R19)", "wedrowcy i eskorta BK z powrotem w domu (R19)", "wedrowcy BK bez pochodzenia (R19 - bez hearth)",
            "migracja ze wsi do miasta (R2)", "narodziny i zgony miast (R2 - stan miasta)",
            "weterani niewynajeci po czasie czekania do domu (E4/10)", "starzy zolnierze z ludzi miast do karczm (siew E4/10, stan miasta E4/4)",
            "powrot z pochodzeniem: niewzieci z poboru wymuszonego gracza do swojej wsi (E4/7b)",
            "pobor do Strazy z ludnosci Westeros (E4/9)", "dluznicy: odeszli z braku pieniedzy do domu (E4/9 DLUG 4, ClanBudget.ToVillage)",
            "powrot z pochodzeniem: dluznicy do domu (E4/9 DLUG 4)" };
        // czy przyczyna rusza hearth wsi. SLUZBA -> DOM (zolnierze do BK, 166, W4c, 179) tylko liczona: werbunek nie zdejmuje jeszcze czlowieka z DOM
        // (R4 w E4/7), wiec powrot dopisany do hearth bylby ludzmi z niczego (i zaklocalby prog przyrostu E4/4); incydenty i podloga 10 - hearth zmienila gra (licznik).
        // E4/5: spustoszenie i powrot uchodzcow ruszaja hearth; podloga 10 z uchodzcow - hearth dopisala gra (okno W1 E4/0 ja mierzy), tylko licznik
        // E4/6: zmarli i uciekli z glodu ruszaja hearth (DOM -> smierc, DOM -> UCHODZCY); zmarli mieszczanie - stan miasta "t:" (TownDie), bez hearth wsi
        // E4/7: pobor z wsi i powroty z pochodzeniem ruszaja hearth (symetria - LevyLive, PeopleBook.Origin.cs); pobor z miasta - stan miasta, nie hearth
        private static readonly bool[] MHearth = { true, true, true, true, true, true, true, true, false, false, false, false, true, true, false, false, true, true, true, false,
                                                   true, true, false,
                                                   true, true, true, true, true, true, true, true, true, false, false,
                                                   // E4/8: wies ruszana (miasto - stan miasta przez TownShift, bez hearth); "bez pochodzenia" i narodziny miast - tylko licznik
                                                   true, true, false, true, true, true, true, true, true, false, true, false,
                                                   true, false,                                                              // E4/10: MBackVeteran (hearth), MVetStart (stan miasta)
                                                   true,                                                                     // E4/7b: MBackPress (hearth wsi zdjecia)
                                                   true, false, true };                                                      // E4/9: MWatchLevy (hearth), MDebt (licznik), MBackDebt (hearth)
        private static readonly object Gate = new object();
        private static readonly double[] _mMen = new double[MKinds], _mHearth = new double[MKinds];   // doba linii "Ludzie (ksiega)"
        private static readonly double[] _h40 = new double[MKinds];                                     // od ostatniej linii "Hearth (E4/0)" (Measure40 czyta i zeruje)
        private static long _disbandMen40;                                                              // R18 od ostatniej linii "Partie (E4/0)" (kontrola: wrocili = sklad)
        private static int _disbandNoVillage;
        // k na osade (U3): id osady -> ludzi na punkt hearth (wies) albo dobrobytu (miasto) z dnia kalibracji i kultura z tego dnia - stan w zapisie ("v:")
        private static readonly Dictionary<string, float> _kv = new Dictionary<string, float>();
        private static readonly Dictionary<string, string> _cv = new Dictionary<string, string>();
        private static bool _kvSet;
        private static string _kvNote;
        // E4/4 (9a): DOM miasta jako stan - id miasta -> ludzie (stan w zapisie, "t:"); zakladanie i porzucanie - PopulationLaw (TownPeopleFrozen)
        private static readonly Dictionary<string, float> _town = new Dictionary<string, float>();
        // E4/4 (R17): podloga 10 hearth - od ostatniego odczytu linii "Ludzie: przyrost naturalny" (PopulationLaw.GrowthDaily czyta i zeruje)
        private static double _floorHearth, _floorPoolMen, _floorNothingMen, _floorNothingHearth, _floorRefMen;
        private static int _floorN;
        // E4/5 (113n): konto UCHODZCY i zegar regionu - stan w zapisie ("u:", "r:"). Osobny zamek RefGate: latki modeli (GetHearthLevel, produkcja wsi) moga isc z watkow AI
        private sealed class Ref { public double Away, Dead, Died; public string Region; }      // poza domem; zabici przy spustoszeniu; zmarli na koncie (od poczatku kampanii); region (bez zapisu)
        private static readonly Dictionary<string, Ref> _ref = new Dictionary<string, Ref>();           // id osady wsi -> konto
        private static readonly Dictionary<string, int> _lastStrike = new Dictionary<string, int>();   // id warowni regionu (OutlawLaw.RegionFor) -> doba ostatniego rabunku/zerowania z trafionymi > 0
        private static volatile int _refCount;                                                          // wsie z Away > 0 - szybka sciezka latek modeli (bez zamka)
        private static readonly object RefGate = new object();
        private static double _dRefIn, _dRefHome, _dRefFloor, _dRefDied, _dKilled;                      // doba linii "Ludzie (ksiega)" (bez zapisu)
        private static double _dRefCapt;                                                                // E4/9: pojmani z konta do lochu najezdzcy (doba)
        private static double _refN0 = double.NaN;                                                      // stan konta z konca poprzedniego odcinka (NaN = pierwsza doba sesji)
        private static int _refStumbles;                                                                // potkniecia konta UCHODZCY (doba)

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_errSites.Add(where)) Log.Error("PeopleBook." + where, e); } catch { }
        }

        /// <summary>Konstruktor ArmouryBehavior: stan jednej kampanii nie przecieka do drugiej.</summary>
        internal static void Reset()
        {
            _bodies.Clear(); _seeded = false; _opening = 0;
            Array.Clear(_in, 0, CKinds); Array.Clear(_out, 0, CKinds); Array.Clear(_dead, 0, _dead.Length);
            _deadSess = 0; _stumbles = _stumblesDay = 0; _errSites.Clear(); _openingNote = null;
            lock (Gate) { Array.Clear(_mMen, 0, MKinds); Array.Clear(_mHearth, 0, MKinds); Array.Clear(_h40, 0, MKinds); _disbandMen40 = 0; _disbandNoVillage = 0; }
            _kv.Clear(); _cv.Clear(); _kvSet = false; _kvNote = null;
            lock (Gate) { _town.Clear(); _floorHearth = _floorPoolMen = _floorNothingMen = _floorNothingHearth = _floorRefMen = 0; _floorN = 0; }
            ClearRefugees();
            try { Famine.Reset(); } catch (Exception e) { Stumble("Reset(E4/6)", e); }      // E4/6: spichlerze - stan tej samej sekcji zapisu
            try { ResetOrigin(); } catch (Exception e) { Stumble("Reset(E4/7)", e); }     // E4/7: konta SLUZBA/JENCY z pochodzeniem, rg:, siew - nowa kampania od zera
            try { ResetArms(); } catch (Exception e) { Stumble("Reset(E4/8)", e); }       // E4/8: odniesienia miast tr:, liczniki ruchow stanu miast
            try { ResetVeterans(); } catch (Exception e) { Stumble("Reset(E4/10)", e); }  // E4/10: konto WETERANI (pula karczm), siew, akumulatory - nowa kampania od zera
            try { ResetCanon(); } catch (Exception e) { Stumble("Reset(E4/9)", e); }      // E4/9: konta NIEWOLNI i MUR, bilans otwarcia, liczniki Strazy - nowa kampania od zera
        }

        /// <summary>E4/5: konto UCHODZCY, zegary regionow i liczniki doby od zera (Reset, Import).</summary>
        private static void ClearRefugees()
        {
            lock (RefGate)
            {
                _ref.Clear(); _lastStrike.Clear(); _refCount = 0;
                _dRefIn = _dRefHome = _dRefFloor = _dRefDied = _dKilled = 0; _dRefCapt = 0; _refN0 = double.NaN; _refStumbles = 0;
                LastSeg = default(RefSeg);
            }
        }

        // ---------------------------------------------------------------- zapis ("v1|s:<zalozony 0/1>;<bilans otwarcia>|c:<id>=<ciala>,...|v:<id osady>=<k>;<kultura>,...|t:<id miasta>=<ludzie>,...")
        internal static string Export()
        {
            var sb = new StringBuilder(64 + _bodies.Count * 24 + (_kvSet ? _kv.Count * 40 : 0));
            sb.Append("v1|s:").Append(_seeded ? 1 : 0).Append(';').Append(_opening.ToString(Inv)).Append("|c:");
            bool first = true;
            foreach (var kv in _bodies)
            {
                if (kv.Value <= 0) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv));
            }
            // E4/3 (U3): k na osade z dnia kalibracji - tylko gdy zalozone (bez sekcji = stary zapis, k_v z pierwszego wczytania)
            if (_kvSet)
            {
                sb.Append("|v:");
                first = true;
                foreach (var kv in _kv)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    string c; _cv.TryGetValue(kv.Key, out c);
                    sb.Append(kv.Key).Append('=').Append(kv.Value.ToString("R", Inv)).Append(';').Append(c ?? "");
                }
            }
            // E4/4 (9a): ludzie miast jako stan - tylko gdy zalozony (bez sekcji = stary zapis albo stan porzucony - zalozy sie od nowa z dobrobytu)
            lock (Gate)
            {
                if (_town.Count > 0)
                {
                    sb.Append("|t:");
                    first = true;
                    foreach (var kv in _town)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append(kv.Key).Append('=').Append(kv.Value.ToString("R", Inv));
                    }
                }
            }
            // E4/5 (113n): konto UCHODZCY i zegary regionow - tylko gdy jest co zapisac (bez sekcji = brak uchodzcow, jak stary zapis)
            try { ExportRefugees(sb); } catch (Exception e) { Stumble("Export(E4/5)", e); }
            // E4/6 (191): spichlerze okregow i miejsca pobytu kont ("f:") - tylko gdy zalozone (bez sekcji = stary zapis, spichlerze zaloza sie przy pierwszej dobie)
            try { sb.Append(Famine.Export()); } catch (Exception e) { Stumble("Export(E4/6)", e); }
            // E4/7 (164b-1): pochodzenie (sw: so: jo: wo: rg:) - tylko po siewie (bez sekcji = siew przy pierwszej dobie)
            try { ExportOrigin(sb); } catch (Exception e) { Stumble("Export(E4/7)", e); }
            // E4/8 (R2): odniesienia miast "tr:" - tylko gdy zalozone (bez sekcji = stary zapis, odniesienie zalozy sie w pierwszej dobie)
            try { ExportArms(sb); } catch (Exception e) { Stumble("Export(E4/8)", e); }
            // E4/10 (167): konto WETERANI (vs: vt: vh:) - tylko po siewie (bez sekcji = siew przy pierwszej dobie)
            try { ExportVeterans(sb); } catch (Exception e) { Stumble("Export(E4/10)", e); }
            // E4/9 (192): konta NIEWOLNI i MUR, bilans otwarcia niewolnych (ns: nw: mw:) - tylko gdy jest co zapisac (bez sekcji = stary zapis: konta od 0, bez bilansu)
            try { ExportCanon(sb); } catch (Exception e) { Stumble("Export(E4/9)", e); }
            return sb.ToString();
        }

        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf('=') >= 0 || id.IndexOf(',') >= 0 || id.IndexOf(';') >= 0; }

        private static void ExportRefugees(StringBuilder sb)
        {
            List<KeyValuePair<string, Ref>> refs; List<KeyValuePair<string, int>> clocks;
            lock (RefGate) { refs = _ref.ToList(); clocks = _lastStrike.ToList(); }
            var withRef = new HashSet<string>();
            bool first = true;
            foreach (var kv in refs)
            {
                var r = kv.Value;
                if (BadId(kv.Key) || !(r.Away > 0.0 || r.Dead > 0.0 || r.Died > 0.0)) continue;
                if (r.Away > 0.0) { string reg = RegionIdOf(kv.Key, r); if (reg != null) withRef.Add(reg); }
                sb.Append(first ? "|u:" : ",").Append(kv.Key).Append('=').Append(r.Away.ToString("R", Inv)).Append(';').Append(r.Dead.ToString("R", Inv))
                  .Append(';').Append(r.Died.ToString("R", Inv));
                first = false;
            }
            // zegar zostaje, dopoki region ma uchodzcow albo jest mlodszy niz RefugeeReturnDays (porzadek w zapisie - starszy niczego juz nie wstrzymuje)
            int today = (int)CampaignTime.Now.ToDays;
            var s = Settings.Current;
            int keep = Math.Max(30, s != null ? s.RefugeeReturnDays : 364);
            first = true;
            foreach (var kv in clocks)
            {
                if (BadId(kv.Key) || (!withRef.Contains(kv.Key) && today - kv.Value >= keep)) continue;
                sb.Append(first ? "|r:" : ",").Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv));
                first = false;
            }
        }

        /// <summary>Region wsi konta (warownia OutlawLaw.RegionFor) - zapamietany przy Devastated, po wczytaniu ustalany raz z osady. null = brak.</summary>
        private static string RegionIdOf(string villageId, Ref r)
        {
            if (r.Region != null) return r.Region.Length > 0 ? r.Region : null;
            string reg = "";
            try
            {
                var st = Settlement.Find(villageId);
                var node = st != null ? OutlawLaw.RegionFor(st) : null;
                if (node != null && node.StringId != null) reg = node.StringId;
            }
            catch (Exception e) { Stumble("RegionIdOf", e); }
            r.Region = reg;
            return reg.Length > 0 ? reg : null;
        }

        internal static void Import(string data)
        {
            _bodies.Clear(); _seeded = false; _opening = 0;
            _kv.Clear(); _cv.Clear(); _kvSet = false; _kvNote = null;
            lock (Gate) _town.Clear();
            ClearRefugees();                          // E4/5: stary zapis (bez "u:" / "r:") - brak uchodzcow i zegarow
            try { Famine.Clear(); } catch (Exception e) { Stumble("Import(E4/6 czyszczenie)", e); }   // E4/6: zapis bez "f:" - spichlerze zaloza sie przy pierwszej dobie
            try { ResetOrigin(); } catch (Exception e) { Stumble("Import(E4/7 reset)", e); }   // E4/7: stary zapis bez sekcji - siew przy pierwszej dobie
            try { ResetArms(); } catch (Exception e) { Stumble("Import(E4/8 reset)", e); }     // E4/8: zapis bez "tr:" - odniesienia miast zaloza sie w pierwszej dobie
            try { ResetVeterans(); } catch (Exception e) { Stumble("Import(E4/10 reset)", e); }   // E4/10: stary zapis bez vs: - siew przy pierwszej dobie
            try { ResetCanon(); } catch (Exception e) { Stumble("Import(E4/9 reset)", e); }       // E4/9: stary zapis bez ns:/nw:/mw: - konta od 0, bez bilansu otwarcia
            if (string.IsNullOrEmpty(data)) return;   // stary zapis - bilans otwarcia przy pierwszym uzyciu (po dwoch dobach kampanii: od 0)
            try
            {
                foreach (var part in data.Split('|'))
                {
                    // E4/7: dwuliterowe przedrostki (sw: so: jo: wo: rg:) - nie koliduja z "s:" konta CIALA; czytane pierwsze
                    if (part.Length > 3 && part[2] == ':' && ImportOriginPart(part)) continue;
                    // E4/10: vs: vt: vh: (drugi znak inny niz ':' - "v:" k na osade ich nie lapie)
                    if (part.Length > 3 && part[2] == ':' && ImportVeteransPart(part)) continue;
                    // E4/9: ns: nw: mw: (dwuliterowe; "s:" konta CIALA ich nie lapie - drugi znak inny niz ':')
                    if (part.Length > 3 && part[2] == ':' && ImportCanonPart(part)) continue;
                    if (part.StartsWith("s:", StringComparison.Ordinal))
                    {
                        var a = part.Substring(2).Split(';');
                        _seeded = a.Length > 0 && a[0] == "1";
                        long o; if (a.Length > 1 && long.TryParse(a[1], NumberStyles.Integer, Inv, out o)) _opening = o;
                    }
                    else if (part.StartsWith("c:", StringComparison.Ordinal))
                    {
                        foreach (var item in part.Substring(2).Split(','))
                        {
                            int eq = item.LastIndexOf('=');
                            if (eq < 0) continue;
                            long n; if (!long.TryParse(item.Substring(eq + 1), NumberStyles.Integer, Inv, out n) || n <= 0) continue;
                            _bodies[item.Substring(0, eq)] = n;
                        }
                    }
                    else if (part.StartsWith("v:", StringComparison.Ordinal))
                    {
                        _kvSet = true;
                        foreach (var item in part.Substring(2).Split(','))
                        {
                            int eq = item.IndexOf('=');
                            if (eq <= 0) continue;
                            string rest = item.Substring(eq + 1);
                            int sc = rest.IndexOf(';');
                            float k;
                            if (!float.TryParse(sc >= 0 ? rest.Substring(0, sc) : rest, NumberStyles.Float, Inv, out k) || !(k > 0f) || float.IsInfinity(k)) continue;
                            string id = item.Substring(0, eq);
                            _kv[id] = k;
                            if (sc >= 0 && sc + 1 < rest.Length) _cv[id] = rest.Substring(sc + 1);
                        }
                        _kvNote = "z zapisu: " + _kv.Count + " osad";
                    }
                    else if (part.StartsWith("t:", StringComparison.Ordinal))
                    {
                        // E4/4 (9a): ludzie miast; zepsuty wpis pomijany (miasto zalozy stan od nowa z dobrobytu - bez skoku)
                        foreach (var item in part.Substring(2).Split(','))
                        {
                            int eq = item.LastIndexOf('=');
                            if (eq <= 0) continue;
                            float p;
                            if (!float.TryParse(item.Substring(eq + 1), NumberStyles.Float, Inv, out p) || float.IsNaN(p) || float.IsInfinity(p) || p < 0f) continue;
                            lock (Gate) _town[item.Substring(0, eq)] = p;
                        }
                    }
                    else if (part.StartsWith("u:", StringComparison.Ordinal))
                    {
                        // E4/5: konto UCHODZCY "id=poza domem;zabici;zmarli"; smieci (NaN, ujemne, nieskonczone) = 0; ta sama wies dwa razy - ostatni wpis
                        foreach (var item in part.Substring(2).Split(','))
                        {
                            int eq = item.LastIndexOf('=');
                            if (eq <= 0) continue;
                            var nums = item.Substring(eq + 1).Split(';');
                            double away = Num(nums, 0), dead = Num(nums, 1), died = Num(nums, 2);
                            if (!(away > 0.0 || dead > 0.0 || died > 0.0)) continue;
                            string id = item.Substring(0, eq);
                            lock (RefGate)
                            {
                                Ref old;
                                if (_ref.TryGetValue(id, out old) && old.Away > 0.0) _refCount--;
                                _ref[id] = new Ref { Away = away, Dead = dead, Died = died };
                                if (away > 0.0) _refCount++;
                            }
                        }
                    }
                    else if (part.StartsWith("tr:", StringComparison.Ordinal))
                    {
                        // E4/8 (R2): odniesienia miast; zepsuty wpis pomijany (zalozy sie od nowa w pierwszej dobie)
                        try { ImportArms(part.Substring(3)); } catch (Exception e) { Stumble("Import(E4/8)", e); }
                    }
                    else if (part.StartsWith("f:", StringComparison.Ordinal))
                    {
                        // E4/6: spichlerze okregow (Famine) - zepsuty wpis pomijany w Famine.Import
                        try { Famine.Import(part.Substring(2)); } catch (Exception e) { Stumble("Import(E4/6)", e); }
                    }
                    else if (part.StartsWith("r:", StringComparison.Ordinal))
                    {
                        // E4/5: zegary regionow "id warowni=doba"
                        foreach (var item in part.Substring(2).Split(','))
                        {
                            int eq = item.LastIndexOf('=');
                            if (eq <= 0) continue;
                            int d; if (!int.TryParse(item.Substring(eq + 1), NumberStyles.Integer, Inv, out d)) continue;
                            lock (RefGate) _lastStrike[item.Substring(0, eq)] = d;
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("Import", e); }
            try { VetAfterImport(); } catch (Exception e) { Stumble("Import(E4/10)", e); }      // E4/10: pula z zapisu - stan do kontroli pierwszej doby
            try { CanonAfterImport(); } catch (Exception e) { Stumble("Import(E4/9)", e); }     // E4/9: linia "z zapisu" (raz, w pierwszej dobie) i stan do kontroli
        }

        private static double Num(string[] a, int i)
        {
            double x;
            if (a == null || i >= a.Length || !double.TryParse(a[i], NumberStyles.Float, Inv, out x)) return 0.0;
            return double.IsNaN(x) || double.IsInfinity(x) || x < 0.0 ? 0.0 : x;
        }

        // ---------------------------------------------------------------- E4/4 (9a): DOM miast jako stan
        /// <summary>Ludzie miasta ze stanu ksiegi (false = brak stanu dla tego miasta).</summary>
        internal static bool TryTown(Settlement s, out float people)
        {
            people = 0f;
            if (s == null || s.StringId == null) return false;
            lock (Gate) return _town.TryGetValue(s.StringId, out people);
        }

        /// <summary>Zalozenie stanu miasta (PopulationLaw - raz, z dobrobytu x k_v tej chwili). Nie nadpisuje istniejacego. true = zalozono teraz.</summary>
        internal static bool SetTownOnce(Settlement s, float people)
        {
            if (s == null || s.StringId == null || float.IsNaN(people) || float.IsInfinity(people) || people < 0f) return false;
            lock (Gate)
            {
                if (_town.ContainsKey(s.StringId)) return false;
                _town[s.StringId] = people;
                _townSet += people;          // E4/8: kontrola linii "Miasta (E4/8 R2)" - zalozenie stanu to nie ruch ludzi
                return true;
            }
        }

        internal static int TownCount { get { lock (Gate) return _town.Count; } }
        internal static double TownSum { get { double t = 0; lock (Gate) foreach (var v in _town.Values) t += v; return t; } }

        /// <summary>Porzucenie stanu miast (TownPeopleFrozen wylaczone w MCM) - ludzie miast znow z dobrobytu x k; zwraca, ilu miast stan porzucono.</summary>
        internal static int DropTowns() { lock (Gate) { int n = _town.Count; foreach (var v in _town.Values) _townSet -= v; _town.Clear(); return n; } }

        // ---------------------------------------------------------------- E4/4 (R17): podloga 10 hearth bez ludzi z niczego
        /// <summary>
        /// Gra (Village.DailyTick) i BK (VillageDailyTicktPatch) stawiaja wsi ponizej 10 hearth z powrotem 10 - `hearth` punktow dopisanych z niczego.
        /// Tylu ludzi (hearth x k_v) schodzi najpierw z konta UCHODZCY tej wsi (E4/5: RefugeesHome MRefugeeFloor), potem z puli wyrzutkow regionu wsi
        /// (OutlawLaw.TakeForFloor - wrocili do domu);
        /// czego pula nie ma - licznik "podloga z niczego" (prog testu 0). Hearth zostaje taki, jak go postawila gra (podlogi nie cofamy - gra trzyma 10).
        /// Wola PopulationLaw.FloorFinalizer (finalizer ticku wsi, tylko przy czynnym przyroscie naturalnym). Nie rzuca (U24).
        /// </summary>
        internal static void FloorFromPool(Village v, float hearth)
        {
            if (v == null || !(hearth > 0f) || float.IsInfinity(hearth)) return;
            try
            {
                float k = v.Settlement != null ? PopulationLaw.KFor(v.Settlement) : 0f;
                double men = k > 0f ? (double)hearth * k : 0.0;
                double got = 0.0, fromRef = 0.0;
                if (men > 0.0)
                {
                    // E4/5: najpierw uchodzcy tej wsi (ruch UCHODZCY -> DOM bez hearth - dopisala go gra), potem wyrzutki regionu
                    fromRef = RefugeesHome(v, men, MRefugeeFloor, 1f / k);
                    double left = men - fromRef;
                    if (left > 1e-9)
                    {
                        var region = OutlawLaw.RegionFor(v.Settlement);
                        got = OutlawLaw.TakeForFloor(region, (float)left);
                    }
                }
                if (got > 0.0) Note(got, MFloor, k > 0f ? got / k : 0.0);       // hearth dopisala gra - tu tylko ruch ludzi WYRZUTKI -> DOM
                lock (Gate)
                {
                    _floorN++; _floorHearth += hearth; _floorPoolMen += got; _floorRefMen += fromRef;
                    double rest = men - fromRef - got;
                    if (rest > 0.0) { _floorNothingMen += rest; _floorNothingHearth += k > 0f ? rest / k : 0.0; }
                    else if (!(k > 0f)) _floorNothingHearth += hearth;     // wies bez przelicznika ludzi - hearth z niczego
                }
            }
            catch (Exception e) { Stumble("FloorFromPool(R17)", e); }
        }

        /// <summary>PopulationLaw.GrowthDaily: podloga 10 od ostatniego odczytu (kopiuje i zeruje).</summary>
        internal static void TakeFloor(out int villages, out double hearth, out double poolMen, out double nothingMen, out double nothingHearth, out double refMen)
        {
            lock (Gate)
            {
                villages = _floorN; hearth = _floorHearth; poolMen = _floorPoolMen; nothingMen = _floorNothingMen; nothingHearth = _floorNothingHearth; refMen = _floorRefMen;
                _floorN = 0; _floorHearth = _floorPoolMen = _floorNothingMen = _floorNothingHearth = _floorRefMen = 0;
            }
        }

        // ---------------------------------------------------------------- E4/5 (113n): konto UCHODZCY i zegar regionu
        private static string VillageId(Village v) { return v != null && v.Settlement != null ? v.Settlement.StringId : null; }

        private static void RefStumble(string where, Exception e) { lock (RefGate) _refStumbles++; Stumble(where, e); }

        /// <summary>Uchodzcy tej wsi poza domem (ludzie). 0 bez zamka, gdy nikt nie jest poza domem (szybka sciezka latek modeli). Nie rzuca.</summary>
        internal static double RefugeesOf(Village v)
        {
            if (_refCount == 0) return 0.0;
            try
            {
                string id = VillageId(v);
                if (id == null) return 0.0;
                lock (RefGate) { Ref r; return _ref.TryGetValue(id, out r) ? r.Away : 0.0; }
            }
            catch (Exception e) { RefStumble("RefugeesOf", e); return 0.0; }
        }

        /// <summary>Wsie z uchodzcami poza domem (0 = szybka sciezka).</summary>
        internal static int RefugeeVillages { get { return _refCount; } }

        /// <summary>Uchodzcy swiata poza domem (ludzie).</summary>
        internal static double RefugeesWorld()
        {
            if (_refCount == 0) return 0.0;
            try { lock (RefGate) { double s = 0.0; foreach (var r in _ref.Values) s += r.Away; return s; } }
            catch (Exception e) { RefStumble("RefugeesWorld", e); return 0.0; }
        }

        /// <summary>Region (warownia + jej wsie, BoundVillages): uchodzcy poza domem i zabici przy spustoszeniu od poczatku kampanii - CSV PeopleLedger.</summary>
        internal static void RegionRefugees(Settlement region, out double away, out double dead)
        {
            away = 0.0; dead = 0.0;
            try
            {
                if (region == null || region.BoundVillages == null) return;
                lock (RefGate)
                {
                    if (_ref.Count == 0) return;
                    foreach (var v in region.BoundVillages)
                    {
                        string id = VillageId(v); Ref r;
                        if (id != null && _ref.TryGetValue(id, out r)) { away += r.Away; dead += r.Dead; }
                    }
                }
            }
            catch (Exception e) { away = 0.0; dead = 0.0; RefStumble("RegionRefugees", e); }
        }

        /// <summary>
        /// TYLKO z Devastation.Strike, PO PeopleBook.Move trafionych z DOM (U23, U24): na konto wsi `refugees` uchodzcow i `dead` zabitych, zegar regionu = `day`
        /// (zegar rusza tylko przy trafionych > 0 - wolajacy wola tylko wtedy). Nie rzuca.
        /// </summary>
        internal static void Devastated(Village v, double refugees, double dead, Settlement region, int day)
        {
            try
            {
                string id = VillageId(v);
                if (id == null) return;
                if (!(refugees > 0.0) || double.IsInfinity(refugees)) refugees = 0.0;
                if (!(dead > 0.0) || double.IsInfinity(dead)) dead = 0.0;
                lock (RefGate)
                {
                    Ref r;
                    if (!_ref.TryGetValue(id, out r)) _ref[id] = r = new Ref();
                    bool had = r.Away > 0.0;
                    r.Away += refugees; r.Dead += dead;
                    if (!had && r.Away > 0.0) _refCount++;
                    _dRefIn += refugees; _dKilled += dead;
                    if (dead > 0.0) NoteCivilDead(region, dead);      // E4/7 (R5, D7): zabici cywile regionu - K_m wolnych rak (0.27 x cywile)
                    if (region != null && region.StringId != null)
                    {
                        r.Region = region.StringId;
                        int old;
                        if (!_lastStrike.TryGetValue(region.StringId, out old) || day > old) _lastStrike[region.StringId] = day;
                    }
                }
            }
            catch (Exception e) { RefStumble("Devastated", e); }
        }

        /// <summary>Doba ostatniego rabunku albo zerowania z trafionymi > 0 w regionie (warownia OutlawLaw.RegionFor). false = brak zegara.</summary>
        internal static bool LastStrike(Settlement region, out int day)
        {
            day = 0;
            try
            {
                if (region == null || region.StringId == null) return false;
                lock (RefGate) return _lastStrike.TryGetValue(region.StringId, out day);
            }
            catch (Exception e) { RefStumble("LastStrike", e); return false; }
        }

        /// <summary>Zegary regionow w pamieci (do linii).</summary>
        internal static int RegionClocks { get { lock (RefGate) return _lastStrike.Count; } }

        /// <summary>
        /// Uchodzcy wracaja do swojej wsi: zdejmuje min(men, poza domem) z konta. MRefugeeHome - Move UCHODZCY -> DOM (hearth +n/k); gdy Move nie przyjal
        /// (wyjatek w Shift - zwraca 0), ludzie wracaja na konto i liczy sie potkniecie (nikt nie znika). MRefugeeFloor - podloga 10: hearth dopisala juz gra/BK,
        /// tylko ruch ludzi w liczniku (Note). Zwraca, ilu zeszlo z konta. Nie rzuca (U24).
        /// </summary>
        internal static double RefugeesHome(Village v, double men, int cause, float legacy)
        {
            if (v == null || !(men > 0.0) || double.IsInfinity(men) || (cause != MRefugeeHome && cause != MRefugeeFloor)) return 0.0;
            string id = VillageId(v);
            if (id == null || _refCount == 0) return 0.0;
            double n = 0.0;
            Ref r = null;
            try
            {
                lock (RefGate)
                {
                    if (!_ref.TryGetValue(id, out r) || !(r.Away > 0.0)) return 0.0;
                    n = Math.Min(men, r.Away);
                    r.Away -= n;
                    if (!(r.Away > 1e-9)) { n += Math.Max(0.0, r.Away); r.Away = 0.0; _refCount--; }     // reszta ponizej 1e-9 idzie razem z nimi (kontrola konta)
                }
            }
            catch (Exception e) { RefStumble("RefugeesHome(konto)", e); return 0.0; }
            if (!(n > 0.0)) return 0.0;
            if (cause == MRefugeeHome)
            {
                double h = Move(v, ARefugees, ADom, n, cause, legacy);
                if (!(h > 0.0))
                {
                    // Move nie przyjal (wyjatek w Shift albo wies bez hearth) - ludzie zostaja na koncie, licznik ksiegi cofniety
                    try
                    {
                        lock (RefGate) { bool had = r.Away > 0.0; r.Away += n; if (!had) _refCount++; _refStumbles++; }
                        Count(-n, cause, 0.0);
                    }
                    catch (Exception e) { Stumble("RefugeesHome(zwrot)", e); }
                    return 0.0;
                }
                lock (RefGate) _dRefHome += n;
            }
            else
            {
                Note(n, cause, legacy > 0f ? n * legacy : 0.0);      // podloga: hearth dopisala gra (legacy = 1/k osady - tylko do linii)
                lock (RefGate) _dRefFloor += n;
            }
            return n;
        }

        /// <summary>
        /// E4/6 (R10, GL 2.6): TYLKO z Famine.TryFlee, PO PeopleBook.Move (DOM -> UCHODZCY, MFamineFled): n ludzi na konto wsi (nowi w odcinku doby) - BEZ zegara
        /// rabunku regionu (powrot uchodzcow z glodu liczy Famine od dnia odzyskania spichlerza wsi, ReturnGate). Nie rzuca (U24).
        /// </summary>
        internal static void FamineFled(Village v, double n)
        {
            try
            {
                string id = VillageId(v);
                if (id == null || !(n > 0.0) || double.IsInfinity(n)) return;
                lock (RefGate)
                {
                    Ref r;
                    if (!_ref.TryGetValue(id, out r)) _ref[id] = r = new Ref();
                    bool had = r.Away > 0.0;
                    r.Away += n;
                    if (!had && r.Away > 0.0) _refCount++;
                    _dRefIn += n;
                }
            }
            catch (Exception e) { RefStumble("FamineFled", e); }
        }

        /// <summary>
        /// E4/6 (R10): mieszczanie umieraja z glodu w miescie przy Food 0 - stan miasta (E4/4 "t:") w dol o min(n, stan). Zwraca, ilu faktycznie zeszlo (float stanu);
        /// 0 = miasto bez stanu (Town People Frozen wylaczone - ludzie miasta z dobrobytu, nie ma z czego zdjac). Poprawka po recenzji E4/6: zmarli w liczniku ksiegi
        /// (MFamineTown, bez hearth wsi) - slad R3 w "Ludzie (ksiega)". Nie rzuca.
        /// </summary>
        internal static double TownDie(Settlement s, double n)
        {
            if (s == null || s.StringId == null || !(n > 0.0) || double.IsInfinity(n)) return 0.0;
            double got = 0.0;
            try
            {
                lock (Gate)
                {
                    float p;
                    if (!_town.TryGetValue(s.StringId, out p) || !(p > 0f)) return 0.0;
                    float left = (float)Math.Max(0.0, (double)p - n);
                    _town[s.StringId] = left;
                    got = (double)p - left;
                    _townMv[MFamineTown] -= got;     // E4/8: ruch stanu miasta wedlug przyczyny (linia "Miasta (E4/8 R2)")
                }
            }
            catch (Exception e) { Stumble("TownDie", e); return 0.0; }
            if (got > 0.0) Count(got, MFamineTown, 0.0);
            if (got > 0.0) NoteCivilDead(s, got);     // scalenie E4/6 + E4/7: stan miasta liczy sie do DOM regionu (DomOf) - zmarli to cywile regionu (rownanie krain)
            return got;
        }

        /// <summary>Uchodzcy umieraja w drodze (1.3%/rok, bez narodzin): zdejmuje min(men, poza domem), Died += n; hearth bez zmian (sa poza wsia). Zwraca n.</summary>
        internal static double RefugeesDie(Village v, double men)
        {
            if (v == null || !(men > 0.0) || double.IsInfinity(men) || _refCount == 0) return 0.0;
            try
            {
                string id = VillageId(v);
                if (id == null) return 0.0;
                lock (RefGate)
                {
                    Ref r;
                    if (!_ref.TryGetValue(id, out r) || !(r.Away > 0.0)) return 0.0;
                    double n = Math.Min(men, r.Away);
                    r.Away -= n; r.Died += n; _dRefDied += n;
                    double rest = 0.0;
                    if (!(r.Away > 1e-9)) { rest = r.Away; r.Died += r.Away; _dRefDied += r.Away; r.Away = 0.0; _refCount--; }
                    // E4/7 (R5): zmarli uchodzcy - cywile regionu; scalenie z E4/6: region przez RegionIdOf (konto z glodu - FamineFled - i konto po wczytaniu nie maja Region)
                    string reg = RegionIdOf(id, r);
                    if (reg != null) NoteCivilDeadId(reg, n + rest);
                    return n;
                }
            }
            catch (Exception e) { RefStumble("RefugeesDie", e); return 0.0; }
        }

        /// <summary>Migawka konta: id wsi -> poza domem (tylko > 0) - Devastation.Daily i linia doby.</summary>
        internal static void RefugeesSnapshot(List<KeyValuePair<string, double>> dst)
        {
            if (dst == null) return;
            dst.Clear();
            if (_refCount == 0) return;
            try { lock (RefGate) foreach (var kv in _ref) if (kv.Value.Away > 0.0) dst.Add(new KeyValuePair<string, double>(kv.Key, kv.Value.Away)); }
            catch (Exception e) { RefStumble("RefugeesSnapshot", e); }
        }

        /// <summary>Migawka konta z zabitymi: id wsi -> (poza domem, zabici od poczatku) - linia "Ludzie (spustoszenie)".</summary>
        internal static void RefugeesFull(List<KeyValuePair<string, double[]>> dst)
        {
            if (dst == null) return;
            dst.Clear();
            try { lock (RefGate) foreach (var kv in _ref) dst.Add(new KeyValuePair<string, double[]>(kv.Key, new[] { kv.Value.Away, kv.Value.Dead })); }
            catch (Exception e) { RefStumble("RefugeesFull", e); }
        }

        /// <summary>Odcinek konta UCHODZCY jednej doby (zamkniety w Daily) - ta sama liczba w "Ludzie (ksiega)" i w "Ludzie (spustoszenie)" (S4).</summary>
        internal struct RefSeg
        {
            public bool Has, First;                 // zamkniety; pierwsza doba sesji (kontrola od jutra)
            public double N0, In, Home, Floor, Died, N, Killed, KilledAll;
            public double Capt;                     // E4/9: pojmani z konta do lochu najezdzcy (ThrallLaw.CapturePrefix)
            public int Villages, Regions, Clocks, Stumbles;
            public double Check { get { return First ? 0.0 : N - N0 - In + Home + Floor + Died + Capt; } }
        }
        internal static RefSeg LastSeg;

        /// <summary>Daily: zamyka dobe konta UCHODZCY (stan, ruch, kontrola) i zeruje liczniki doby.</summary>
        private static RefSeg CloseRefugeeDay()
        {
            var g = new RefSeg { Has = true };
            List<KeyValuePair<string, Ref>> refs;
            lock (RefGate)
            {
                g.In = _dRefIn; g.Home = _dRefHome; g.Floor = _dRefFloor; g.Died = _dRefDied; g.Killed = _dKilled; g.Stumbles = _refStumbles; g.Capt = _dRefCapt;
                g.N0 = _refN0; g.First = double.IsNaN(_refN0); g.Clocks = _lastStrike.Count;
                refs = _ref.ToList();
                _dRefIn = _dRefHome = _dRefFloor = _dRefDied = _dKilled = 0; _dRefCapt = 0; _refStumbles = 0;
            }
            var regions = new HashSet<string>();
            foreach (var kv in refs)
            {
                g.KilledAll += kv.Value.Dead;
                if (!(kv.Value.Away > 0.0)) continue;
                g.N += kv.Value.Away; g.Villages++;
                string reg = RegionIdOf(kv.Key, kv.Value);
                if (reg != null) regions.Add(reg);
            }
            g.Regions = regions.Count;
            lock (RefGate) _refN0 = g.N;
            LastSeg = g;
            return g;
        }

        private static string RefSegment(RefSeg g)
        {
            var sb = new StringBuilder(400);
            sb.Append(" | E4/5 konto UCHODZCY: ").Append(g.N.ToString("0.#", Inv)).Append(" ludzi w ").Append(g.Villages).Append(" wsiach (").Append(g.Regions).Append(" regionow) = ");
            if (g.First) sb.Append("pierwsza doba sesji - kontrola od jutra (nowi ").Append(M0(g.In)).Append(", powrot ").Append(M0(g.Home)).Append(", podloga 10 ").Append(M0(g.Floor))
                           .Append(", zmarli ").Append(g.Died.ToString("0.###", Inv)).Append(", pojmani ").Append(M0(g.Capt)).Append(')');
            else sb.Append("wczoraj ").Append(g.N0.ToString("0.#", Inv)).Append(" + nowi ").Append(M0(g.In)).Append(" - powrot ").Append(M0(g.Home)).Append(" - podloga 10 ").Append(M0(g.Floor))
                   .Append(" - zmarli ").Append(g.Died.ToString("0.###", Inv)).Append(" - pojmani (E4/9) ").Append(M0(g.Capt)).Append(" (kontrola ").Append(H3d(g.Check)).Append(')');
            sb.Append("; zabici przy spustoszeniu dzis ").Append(M0(g.Killed)).Append(", od poczatku ").Append(g.KilledAll.ToString("0", Inv)).Append("; zegary regionow ").Append(g.Clocks);
            if (g.Stumbles > 0) sb.Append("; potkniecia konta ").Append(g.Stumbles);
            return sb.ToString();
        }

        // ---------------------------------------------------------------- E4/3: k na osade (U3)
        /// <summary>PopulationLaw.Calibrate (nowa kampania): k_v kazdej wsi i miasta z tabeli = k jej kultury z dnia kalibracji. Zapis z "v:" - bez zmian.</summary>
        internal static void SetUnitsFromCalibration()
        {
            if (_kvSet) return;
            FillUnits("kalibracja nowej kampanii");
        }

        private static void FillUnits(string how)
        {
            _kv.Clear(); _cv.Clear();
            int villages = 0, towns = 0;
            foreach (var s in Settlement.All)
            {
                try
                {
                    if (s == null || s.Culture == null || !(s.IsVillage || s.IsTown)) continue;
                    float k = PopulationLaw.KOf(s.Culture.StringId, !s.IsVillage);
                    if (!(k > 0f) || float.IsInfinity(k)) continue;
                    _kv[s.StringId] = k; _cv[s.StringId] = s.Culture.StringId;
                    if (s.IsVillage) villages++; else towns++;
                }
                catch (Exception e) { Stumble("FillUnits(osada)", e); }
            }
            _kvSet = true;
            _kvNote = how + ": wsie " + villages + ", miasta " + towns;
            Log.Info("Ludzie (ksiega): k na osade zalozone (" + how + ") - wsie " + villages + " (ludzi na punkt hearth), miasta " + towns
                     + " (ludzi na punkt dobrobytu); odtad zmiana kultury osady nie zmienia ani liczby ludzi, ani wagi czlowieka w hearth (E4/3, U3).");
        }

        /// <summary>k_v osady (ludzi na punkt hearth wsi / dobrobytu miasta) z dnia kalibracji. Stary zapis bez "v:" - zakladane leniwie z kultury osady
        /// w dniu pierwszego wczytania tej wersji (tylko po kalibracji - sami jej nie wolamy). false = brak (PopulationLaw liczy wtedy po kulturze, jak dotad).</summary>
        internal static bool TryUnit(Settlement s, out float k)
        {
            k = 0f;
            if (s == null) return false;
            if (!_kvSet)
            {
                if (!PopulationLaw.Calibrated) return false;
                try { FillUnits("stary zapis bez k na osade - k kultury osady z dnia wczytania"); }
                catch (Exception e) { _kvSet = true; Stumble("TryUnit", e); }       // nieudane zalozenie - PopulationLaw liczy po kulturze (jak dotad), bez prob co wywolanie
            }
            return s.StringId != null && _kv.TryGetValue(s.StringId, out k) && k > 0f;
        }

        /// <summary>Kultura osady z dnia kalibracji (regula geografii i demografii - E4/1 zima, E4/7 prog wolnych rak); brak - biezaca.</summary>
        internal static string CultureAtCalibration(Settlement s)
        {
            if (s == null) return "?";
            string c;
            if (s.StringId != null && _cv.TryGetValue(s.StringId, out c) && !string.IsNullOrEmpty(c)) return c;
            return s.Culture != null ? s.Culture.StringId : "?";
        }

        // ---------------------------------------------------------------- E4/3: jedna droga ruchu hearth za ludzi
        /// <summary>
        /// JEDYNA droga zmiany hearth wsi za ludzi (R0, U23): men ludzi z konta `from` na konto `to` z przyczyny `cause`. Gdy przyczyna rusza hearth
        /// (MHearth), wies traci albo dostaje men x stawka: 1/k_v tej osady (PeopleUnit.Shift - reszta ponizej kroku floata czeka w pamieci wsi), a przy
        /// wylaczonej jednostce albo braku przelicznika - stawka dotychczasowa `legacy`, ta sama arytmetyka co stary kod. Wolajacy pilnuje swoich podlog
        /// (wies nie ponizej 50 hearth itd.); Move nie schodzi ponizej 0. Zwraca hearth faktycznie przesuniety (+ gdy DOM zyskuje). Nie rzuca (U24).
        /// </summary>
        internal static double Move(Village v, int from, int to, double men, int cause, float legacy)
        {
            if (v == null || !(men > 0.0) || double.IsInfinity(men) || cause < 0 || cause >= MKinds) return 0.0;
            double moved = 0.0;
            try
            {
                int sign = to == ADom ? 1 : (from == ADom ? -1 : 0);
                if (MHearth[cause] && sign != 0)
                {
                    bool unit;
                    float per = PeopleUnit.PerMan(v, legacy, out unit);
                    if (unit) moved = PeopleUnit.Shift(v, sign * men * per);
                    else
                    {
                        float before = v.Hearth;
                        if (sign < 0) v.Hearth -= (float)men * per; else v.Hearth += (float)men * per;   // dotychczasowa arytmetyka (float)
                        moved = (double)v.Hearth - before;
                    }
                }
            }
            catch (Exception e) { Stumble("Move", e); }
            Count(men, cause, moved);
            return moved;
        }

        /// <summary>Ruch ludzi bez zmiany hearth przez nas: SLUZBA -> DOM (zolnierze do ludnosci BK, 166, W4c, 179 - konto SLUZBA od E4/7) albo hearth
        /// zmieniony przez gre (tabory i pobor przy braku przelicznika, incydenty gracza). hearth - zmiana zrobiona przez gre (0 = zadna).</summary>
        internal static void Note(double men, int cause, double hearth)
        {
            if (cause < 0 || cause >= MKinds || double.IsNaN(men) || double.IsNaN(hearth)) return;
            Count(Math.Max(0.0, men), cause, hearth);
        }

        private static void Count(double men, int cause, double hearth)
        {
            try { lock (Gate) { _mMen[cause] += men; _mHearth[cause] += hearth; _h40[cause] += hearth; } }
            catch (Exception e) { Stumble("Count", e); }
        }

        /// <summary>Measure40 (linia "Hearth (E4/0)"): hearth przesuniety przez ksiege wedlug przyczyny od ostatniego odczytu (kopiuje i zeruje).</summary>
        internal static void TakeHearth40(double[] dst)
        {
            try { lock (Gate) { for (int i = 0; i < MKinds && i < dst.Length; i++) dst[i] = _h40[i]; Array.Clear(_h40, 0, MKinds); } }
            catch (Exception e) { Stumble("TakeHearth40", e); }
        }

        /// <summary>PeopleLedger (linia "Partie (E4/0)"): ludzie rozwiazanych taborow i lodzi, ktorzy wrocili do swojej wsi od ostatniej linii.</summary>
        internal static long Disband40 { get { lock (Gate) return _disbandMen40; } }
        internal static void ZeroDisband40() { lock (Gate) _disbandMen40 = 0; }

        /// <summary>
        /// R18 (U15e): tabor wsi albo lodz rybacka rozwiazana przez gre bez bitwy (MobilePartyDestroyed bez niszczyciela - VillagerCampaignBehavior: 0 zdrowych,
        /// sierota; NavalDLC: lodz ponad limit) - caly sklad (zdrowi i ranni) wraca do DOM swojej wsi (+n/k_v). Zdarzenie gry biegnie PRZED RemoveParty
        /// (DestroyPartyAction.ApplyInternal), wiec sklad jeszcze jest. Dotad ci ludzie znikali.
        /// </summary>
        internal static void OnPartyDestroyed(MobileParty mp, PartyBase destroyer)
        {
            // E4/8 (10.4, R19, R6): wedrowcy i konwoje BK u celu albo w domu, partia rozwiazana bez bitwy -> DOM pochodzenia; PRZED "resztka w nicosc" E4/7 (slownik jeszcze jest)
            try { ArmsPartyGone(mp, destroyer, false); } catch (Exception e) { Stumble("OnPartyDestroyed(E4/8)", e); }
            try { OriginPartyGone(mp != null ? mp.Party : null, destroyer != null); } catch (Exception e) { Stumble("OnPartyDestroyed(E4/7)", e); }   // E4/7 (4.12): slownik partii - resztka w nicosc (nazwana)
            try
            {
                if (mp == null || destroyer != null || !mp.IsVillager) return;
                var s = Settings.Current;
                if (s == null || !s.DisbandedVillagersGoHome) return;
                var r = mp.MemberRoster;
                int men = r != null ? r.TotalRegulars : 0;
                if (men <= 0) return;
                var home = mp.HomeSettlement;
                var v = home != null ? home.Village : null;
                if (v == null) { lock (Gate) _disbandNoVillage += men; return; }
                Move(v, ACarts, ADom, men, MDisband, PeopleUnit.GamePerMan);
                lock (Gate) _disbandMen40 += men;
            }
            catch (Exception e) { Stumble("OnPartyDestroyed(R18)", e); }
        }

        // ---------------------------------------------------------------- konto CIALA
        internal static long Bodies { get { long s = 0; foreach (var v in _bodies.Values) s += v; return s; } }

        private static string Key(Settlement region) { return region != null && region.StringId != null ? region.StringId : ""; }

        /// <summary>Bilans otwarcia - leniwie przy pierwszym uzyciu (pierwsze bandy rodza sie przed pierwszym tickiem dobowym): w nowej kampanii
        /// (mniej niz 2 doby od startu) 616 cial w regionie siedziby rodu Innych; w starszej kampanii (stary zapis bez klucza) - od 0.</summary>
        internal static void EnsureOpening()
        {
            if (_seeded) return;
            try
            {
                if (Campaign.Current == null) return;
                _seeded = true;
                float elapsed = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
                if (elapsed < OpeningDays)
                {
                    Settlement region = null;
                    try
                    {
                        var clan = Clan.FindFirst(c => c != null && c.StringId == "ROTclan_126");
                        var home = clan != null ? clan.HomeSettlement : null;
                        if (home != null) region = OutlawLaw.RegionFor(home);
                    }
                    catch (Exception e) { region = null; Stumble("EnsureOpening(region)", e); }   // bez regionu: pula w "regionie nieznanym" - bilans otwarcia i tak jest
                    _bodies[Key(region)] = (_bodies.TryGetValue(Key(region), out var v) ? v : 0) + OpeningBodies;
                    _opening = OpeningBodies; _in[COpening] += OpeningBodies;
                    _openingNote = "bilans otwarcia " + OpeningBodies + " cial w regionie " + (region != null ? region.Name.ToString() : "nieznanym") + " (nowa kampania, doba " + elapsed.ToString("0.0", Inv) + ")";
                }
                else
                {
                    _opening = 0;
                    _openingNote = "stary zapis bez ksiegi (doba kampanii " + elapsed.ToString("0", Inv) + ") - pula od 0, bandy w polu zostaja";
                }
                Log.Info("Ludzie (ksiega): konto CIALA zalozone - " + _openingNote + ".");
            }
            catch (Exception e) { Stumble("EnsureOpening", e); }
        }

        /// <summary>Doplyw do puli cial w regionie (tylko z miejsc R13 - wola OthersRise).</summary>
        internal static void ToBodies(Settlement region, long n, int cause)
        {
            if (n <= 0) return;
            try
            {
                EnsureOpening();
                string k = Key(region);
                long v; _bodies.TryGetValue(k, out v); _bodies[k] = v + n;
                if (cause >= 0 && cause < CKinds) _in[cause] += n;
            }
            catch (Exception e) { Stumble("ToBodies", e); }
        }

        /// <summary>Wydatek z puli cial: do want cial, od regionow najblizszych miejscu pos (region nieznany - na koncu). Zwraca, ile wzieto.</summary>
        internal static long FromBodies(Vec2 pos, long want, int cause)
        {
            if (want <= 0) return 0;
            long got = 0;
            try
            {
                EnsureOpening();
                if (_bodies.Count == 0) return 0;
                var keys = _bodies.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
                var dist = new Dictionary<string, float>(keys.Count);
                foreach (var k in keys)
                {
                    float d = float.MaxValue;
                    if (k.Length > 0) { try { var s = Settlement.Find(k); if (s != null) d = pos.DistanceSquared(s.GetPosition2D); } catch { } }
                    dist[k] = d;
                }
                keys.Sort((a, b) => dist[a].CompareTo(dist[b]));
                foreach (var k in keys)
                {
                    if (got >= want) break;
                    long have = _bodies[k];
                    long take = Math.Min(have, want - got);
                    if (take <= 0) continue;
                    if (have - take > 0) _bodies[k] = have - take; else _bodies.Remove(k);
                    got += take;
                }
                if (cause >= 0 && cause < CKinds) _out[cause] += got;
            }
            catch (Exception e) { Stumble("FromBodies", e); }
            return got;
        }

        /// <summary>R3: polegly z rak Innych (jeniec skasowany, odbity jeniec, milicja przejetej osady) - region miejsca do E4/7, potem pochodzenie.
        /// W E4/2 tylko licznik w logu (bez konta DOM nie ma z czego zdjac).</summary>
        internal static void Die(Settlement region, long n, int cause, int place)
        {
            if (n <= 0) return;
            try
            {
                int p = place >= 0 && place < 4 ? place : 4;
                if (cause >= 0 && cause < DKinds) _dead[cause, p] += n;
                _deadSess += n;
            }
            catch (Exception e) { Stumble("Die", e); }
        }

        // ---------------------------------------------------------------- linia doby
        /// <summary>Raz na dobe (po linii "Inni z poleglych (E4/2)"): stan konta CIALA z rownaniem i polegli z rak Innych.</summary>
        internal static void Daily(int day, bool log)
        {
            RefSeg rseg = default(RefSeg);
            try { rseg = CloseRefugeeDay(); } catch (Exception e) { Stumble("Daily(E4/5)", e); }
            // E4/7 (164b-1): siew, uzgodnienie doby, cache wolnych rak - PRZED linia "Ludzie (ksiega)" (jej odcinek E4/7 czyta wynik); linie E4/7 po niej
            long tOrigin = System.Diagnostics.Stopwatch.GetTimestamp();
            try { OriginDaily(day); } catch (Exception e) { Stumble("Daily(E4/7)", e); }
            // E4/10 (167): siew puli weteranow (po siewie E4/7), powroty po czasie czekania, synchronizacja karczm - PO uzgodnieniu E4/7, PRZED linia "Ludzie (ksiega)"
            try { VetDaily(day); } catch (Exception e) { Stumble("Daily(E4/10)", e); }
            try
            {
                EnsureOpening();
                if (log)
                {
                    long now = Bodies, inn = 0, outn = 0;
                    for (int i = 0; i < CKinds; i++) { inn += _in[i]; outn += _out[i]; }
                    var sb = new StringBuilder(600);
                    sb.Append("Ludzie (ksiega): dzien ").Append(day).Append(" | konto CIALA (pula Innych, nie-ludzie; E4/2): ").Append(now)
                      .Append(" w ").Append(_bodies.Count(kv => kv.Value > 0)).Append(" regionach = wczoraj ").Append(now - inn + outn)
                      .Append(" + doplyw ").Append(inn).Append(" (");
                    bool f = true;
                    for (int i = 0; i < CKinds; i++) { if (i == CBirth) continue; sb.Append(f ? "" : ", ").Append(CName[i]).Append(' ').Append(_in[i]); f = false; }
                    sb.Append(") - narodziny band ").Append(_out[CBirth]);
                    sb.Append(" | ").Append(_openingNote ?? ("bilans otwarcia w zapisie " + _opening));
                    long dd = 0; for (int c = 0; c < DKinds; c++) for (int p = 0; p < 5; p++) dd += _dead[c, p];
                    sb.Append(" | polegli z rak Innych dzis (R3, region miejsca do E4/7; tylko licznik do E4/7 - smierc zdejmuje z DOM razem z werbunkiem) ").Append(dd);
                    if (dd > 0)
                    {
                        sb.Append(" (");
                        bool g = true;
                        for (int c = 0; c < DKinds; c++)
                        {
                            long s = 0; for (int p = 0; p < 5; p++) s += _dead[c, p];
                            if (s <= 0) continue;
                            sb.Append(g ? "" : "; ").Append(DName[c]).Append(' ').Append(s).Append(':');
                            g = false;
                            for (int p = 0; p < 5; p++) if (_dead[c, p] > 0) sb.Append(' ').Append(PlaceName[p]).Append(' ').Append(_dead[c, p]);
                        }
                        sb.Append(')');
                    }
                    sb.Append(", od wczytania ").Append(_deadSess);
                    try { sb.Append(UnitSegment()); } catch (Exception e) { Stumble("Daily(E4/3)", e); }
                    try { if (rseg.Has) sb.Append(RefSegment(rseg)); } catch (Exception e) { Stumble("Daily(E4/5 odcinek)", e); }
                    try { sb.Append(PeopleUnit.DayNote()); } catch (Exception e) { Stumble("Daily(PeopleUnit)", e); }
                    try { sb.Append(OriginSegment()); } catch (Exception e) { Stumble("Daily(E4/7 odcinek)", e); }
                    try { sb.Append(VetSegment()); } catch (Exception e) { Stumble("Daily(E4/10 odcinek)", e); }
                    if (_stumblesDay > 0) sb.Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (razem ").Append(_stumbles).Append(", pierwszy blad kazdego miejsca w logu)");
                    sb.Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            try { if (log) OriginLines(day, tOrigin); } catch (Exception e) { Stumble("Daily(E4/7 linie)", e); }
            try { if (log) VetLine(day); } catch (Exception e) { Stumble("Daily(E4/10 linia)", e); }       // E4/10: "Weterani (E4/10)" zaraz po "Pochodzenie (E4/7)"
            try { CanonEndDay(); } catch (Exception e) { Stumble("Daily(E4/9 koniec doby)", e); }      // E4/9: stan slownikow Strazy i kontrola - PRZED OriginEndDay (rodzaj partii z pamieci tej doby)
            try { OriginEndDay(); } catch (Exception e) { Stumble("Daily(E4/7 koniec doby)", e); }
            try { ArmsCloseDay(); } catch (Exception e) { Stumble("Daily(E4/8 koniec doby)", e); }   // E4/8: ruch DOM przyczynami E4/8 tej doby - do kontroli linii "Wojsko z ludzi (E4/8)"
            try { VetEndDay(); } catch (Exception e) { Stumble("Daily(E4/10 koniec doby)", e); }
            Array.Clear(_in, 0, CKinds); Array.Clear(_out, 0, CKinds); Array.Clear(_dead, 0, _dead.Length);
            _stumblesDay = 0; _openingNote = null;
            lock (Gate) { Array.Clear(_mMen, 0, MKinds); Array.Clear(_mHearth, 0, MKinds); _disbandNoVillage = 0; }
            try { PeopleUnit.NewDay(); } catch { }
        }

        private static string M0(double v) { return v.ToString("0.#", Inv); }
        private static string H3d(double v) { return (v >= 0 ? "+" : "") + v.ToString("0.###", Inv); }

        /// <summary>E4/3: odcinek linii "Ludzie (ksiega)" - konta DOM i WYRZUTKI, ruch DOM wedlug przyczyny (ludzie / hearth), trzy punkty powrotu, k na osade.</summary>
        private static string UnitSegment()
        {
            var s = Settings.Current;
            double vil = 0, vilH = 0, town = 0; int noK = 0, cultMoved = 0, dup = 0;
            var seenV = new HashSet<Village>(); var seenS = new HashSet<Settlement>();
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !(st.IsVillage || st.IsTown)) continue;
                    // E4/4: kazda osada i kazda wies raz - Settlement.All podaje w ROT dwie wsie dwa razy (pomiar E4/0: hearth +705 ponad sume regionow)
                    if (!seenS.Add(st) || (st.IsVillage && st.Village != null && !seenV.Add(st.Village))) { dup++; continue; }
                    float k = PopulationLaw.KFor(st);
                    if (!(k > 0f)) { noK++; continue; }
                    if (st.IsVillage && st.Village != null) { float h = Math.Max(0f, st.Village.Hearth); vilH += h; vil += (double)h * k; }
                    else if (st.IsTown && st.Town != null) town += PopulationLaw.PeopleOf(st);     // E4/4: stan miasta (TownPeopleFrozen), inaczej dobrobyt x k osady
                    string c; if (st.StringId != null && _cv.TryGetValue(st.StringId, out c) && st.Culture != null && c != st.Culture.StringId) cultMoved++;
                }
                catch (Exception e) { Stumble("UnitSegment(osada)", e); }
            }
            double[] men = new double[MKinds], hh = new double[MKinds]; int noVil;
            lock (Gate) { Array.Copy(_mMen, men, MKinds); Array.Copy(_mHearth, hh, MKinds); noVil = _disbandNoVillage; }
            var sb = new StringBuilder(1600);
            sb.Append(" | E4/3 jednostka ludzi (1 czlowiek = 1/k osady): ").Append(PeopleUnit.On ? "CZYNNA" : "wylaczona - stawki dotychczasowe")
              .Append(", wyrzutki liczone od ludnosci: ").Append(PeopleUnit.ByPeople ? "TAK" : "NIE")
              .Append(", rozwiazane tabory do wsi (R18): ").Append(s != null && s.DisbandedVillagersGoHome ? "TAK" : "NIE")
              .Append(" | konto DOM: wsie ").Append(vil.ToString("0", Inv)).Append(" ludzi (hearth ").Append(vilH.ToString("0.0", Inv)).Append(" x k osady), miasta ")
              .Append(town.ToString("0", Inv)).Append(PopulationLaw.TownsFrozen ? " (stan miast E4/4, " + TownCount + " miast w zapisie)" : " (dobrobyt x k osady - stan miast wylaczony: Town People Frozen)")
              .Append(dup > 0 ? " [powtorzone w Settlement.All, liczone raz: " + dup + "]" : "")
              .Append(" | konto WYRZUTKI (pula OutlawLaw, linia \"Pula wyrzutkow (E4/3)\") ").Append(OutlawLaw.PoolTotal().ToString("0", Inv))
              .Append(" | ruch dzis (ludzie / hearth DOM):");
            bool any = false;
            for (int i = 0; i < MKinds; i++)
            {
                if (men[i] == 0 && hh[i] == 0) continue;
                sb.Append(any ? "; " : " ").Append(MName[i]).Append(' ').Append(M0(men[i])).Append(" / ").Append(MHearth[i] ? H3d(hh[i]) : (i == MIncident || i == MFloor || i == MRefugeeFloor ? "gra " + H3d(hh[i]) : (i == MFamineTown || i == MLevyTown || i == MBackTown || i == MTownNatural || i == MVetStart ? "stan miasta" : "bez hearth (bez pochodzenia)")));
                any = true;
            }
            if (!any) sb.Append(" brak");
            // recenzja E4/3: prosci i bandyci z LosersFlee (MCommon) przechodza przez OutlawLaw.ReturnHome, ale licza sie raz - pod punktem (1);
            // punkt (3) pokazuje tylko powrot z puli, a "razem" to suma bez podwojenia
            double back = men[MH3Cart] + men[MCommon] + men[MSoldier] + men[M166] + men[MW4c] + men[M179] + men[MPoolHome] + men[MDebt];     // E4/9: MDebt - ClanBudget.ToVillage (DLUG 4)
            // E4/7: powroty z pochodzeniem (z hearth) - czesc znana tych samych trzech punktow (bez podwojenia: czesc bez pochodzenia idzie dalej pod MSoldier/M166/MW4c/M179)
            // E4/7b: + niewzieci z poboru wymuszonego gracza wracajacy do swojej wsi (MBackPress - nie przechodza przez trzy punkty, wiec bez podwojenia)
            double backE47 = men[MBackH3] + men[MBack166] + men[MBackW4c] + men[MBack179] + men[MBackPool] + men[MBackFreed] + men[MBackDisband] + men[MBackTown] + men[MBackPress] + men[MBackDebt];
            back += backE47;
            sb.Append(" | trzy punkty powrotu do DOM, razem ").Append(M0(back)).Append(" (bez podwojenia): LosersFlee.SendHome ").Append(M0(men[MH3Cart] + men[MCommon] + men[MSoldier]))
              .Append(" (rozbite tabory ").Append(M0(men[MH3Cart])).Append(", prosci i bandyci ").Append(M0(men[MCommon])).Append(" - do wsi dalej przez OutlawLaw.ReturnHome, zolnierze do ludnosci BK ").Append(M0(men[MSoldier]))
              .Append("), ClanBudget.ToVillage ").Append(M0(men[M166] + men[MW4c] + men[M179] + men[MDebt])).Append(" (166 ").Append(M0(men[M166])).Append(", W4c ").Append(M0(men[MW4c]))
              .Append(", 179 ").Append(M0(men[M179])).Append(men[MDebt] > 0 ? ", dluznicy E4/9 " + M0(men[MDebt]) : "").Append("; SLUZBA -> DOM tylko liczona do E4/7 - werbunek jeszcze nie zdejmuje z DOM), OutlawLaw.ReturnHome ")
              .Append(M0(men[MPoolHome])).Append(" (tylko z puli; prosci i bandyci z LosersFlee policzeni w punkcie LosersFlee.SendHome)")
              .Append("; E4/7 z pochodzeniem (hearth regionu pochodzenia) ").Append(M0(backE47)).Append(men[MBackTown] > 0 ? " (w tym do stanu miasta " + M0(men[MBackTown]) + ")" : "")
              .Append(men[MBackVeteran] > 0 || men[MVetStart] > 0 ? "; E4/10 weterani: po czasie czekania do DOM regionu pochodzenia (hearth) " + M0(men[MBackVeteran]) + ", siew z ludzi miast do karczm " + M0(men[MVetStart]) : "");
            if (noVil > 0) sb.Append(" | rozwiazane tabory bez wsi domowej (znikneli) ").Append(noVil);
            sb.Append(" | k na osade: ").Append(_kvSet ? (_kvNote ?? (_kv.Count + " osad")) : "jeszcze nie zalozone (przed kalibracja)")
              .Append(", osad z kultura inna niz w dniu kalibracji ").Append(cultMoved).Append(" (ludzie bez zmian - U3), osad bez przelicznika ").Append(noK);
            return sb.ToString();
        }
    }
}
