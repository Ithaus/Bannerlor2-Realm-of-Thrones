using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// ks-arm (noc 17/18.10, Jeff: "ma byc gra rano ... z poprawnie dzialajaca ekonomia") - KS WLACZA SIE SAM W KAMPANII JEFFA, BEZPIECZNIE:
    ///  - SAMO UZBROJENIE (klucz Start Calibration Auto Arm): w zwyklej grze (w autotescie tylko z plikiem armoury-ks-auto.flag) pierwsza doba z ta DLL uzbraja zapis
    ///    (stan "uzbrojony" w arm_ks, jak Arm This Save) - gra bez zmian. Pomiar: srednia ksiegi towarow 28 dob (w zapisie arm_ks_arm - przez sesje, pierwsza
    ///    niepelna doba sesji pominieta), potem 7 dob od pierwszego dojrzalego Planu, Plan z pomiarem pieniedzy, bramki gry (GameGates) i TWARDE BRAMKI projektu
    ///    rozdz. 10 (klucz Ks Hard Gates): (1) ZC1 BkRecipeYieldByWorkshop i WhaleMeatFromWhalersOnly, (2) jedzenie z niczego konwojow DTE i partii BK zamkniete
    ///    (DteConvoyNoFreeFood, BkCaravanNoFreeFood) i ujscie RemoveParty jedzenia <= 2 szt./d na towar, (3) zrodla "bez nazwy" (inne ticki dobowe ksiegi towarow)
    ///    <= 2 szt./d na towar - srednie 7 ostatnich pelnych dob ksiegi, (4) plik ziemi i uklad wiosek zgodny (PO etap 0-1 zastapiony w kodzie KS: zajecia wiosek
    ///    z arm_ks_land.tsv, nazwane zrodlo "KS: zajecia okregu"). Wszystko TAK - wlaczenie z ta sama rampa (podaz D-14..D, koszyk D..D+14). Bramka niespelniona -
    ///    zostaje Plan, linia "KS: pomiar przed wlaczeniem" mowi dlaczego.
    ///  - BRAMKA KRAINY (projekt rozdz. 8; klucz Ks Land Gate - zastepuje stala LandGateReady): po wlaczeniu kraina (kultura z kalibracji, jak kraina Planu) wraca do
    ///    dzisiejszych zasad (stawki wsi gry, zywnosc wsi gry, spichlerz ze wzoru gry, popyt i koszyk ZC4, przetwory ZC5, kupcy KS-H i B2 - bez niej; reszta swiata
    ///    dalej z KS), gdy (a) wies tej krainy zaczyna glodowac (brak u gospodarzy > polowy progu zgonow Famine, czyli racjonowanie przy spichlerzu ponizej
    ///    FamineRationDays - PRZED zgonami; recenzja ks-arm: dotad prog 10% = sam prog zgonow), jej glod nie jest z wojny TERAZ (Famine.HungerFromWar: wies nie Normal,
    ///    warownia albo region oblegany - bez "rabowany w ciagu roku" i bez uchodzcow, bo to wlasnie strefy wojny), a racje KS jej spichlerza sa ponizej wzoru gry tej
    ///    doby (powrot pomoze), albo (b) spichlerze krainy spadaja szybciej niz 1% potrzeby na dobe 7 dob z rzedu przy racjach KS krainy ponizej wzoru gry, a kraina
    ///    stracila juz pod KS Ks Land Gate Loss Days (91 - jedna pora roku; strata netto od wlaczenia, tylko doby z racjami KS ponizej wzoru, napelnianie oddaje,
    ///    rabunek sie nie liczy) albo ma zapas ponizej Ks Land Gate Reserve Days (364 - krainy oproznione wojna). 0 w ktoryms kluczu = doslownie projekt 16.10 (w fin80
    ///    przy KsGranaryNoFloor 29 z 30 krain w A+7). Kraina wrocona ciagnie za soba wsie innych krain, ktorych miasto targowe w niej lezy (miasto jest poza ZC4 i KS-H -
    ///    wies nie kupilaby koszyka, a zostalaby na stawkach KS bez jedzenia z niczego). Linia "KS: powrot krainy". Druga proba tylko recznie: Start Calibration off
    ///    i znow on (rampa calego swiata od nowa - LandRetry). W kampanii bramka dziala zawsze w stanie zastosowanym (projekt: "bez wylacznika poza calym KS");
    ///    klucz Ks Land Gate decyduje o wejsciu KS do kampanii, a w autotescie (stara droga) wylacza bramke jak dotad.
    ///  - ZRODLA Z NICZEGO ZNANE Z NAZWY (recenzja ks-arm: nic w grze nie zmienia sie przed koncem pomiaru): (i) NavalDLC NavalFishingCampaignBehaviour.OnDailyTickSettlement
    ///    (perk Night Raider gubernatora, klucz Ks No Fish From Perk, ok. 100-150 ryb/d w fin80) i (ii) zboze nowych druzyn patrolowych miast (PatrolPartyComponent.
    ///    CreatePatrolParty, 15-30 szt.; klucz Ks Patrol Grain From Town) - przed wlaczeniem dzialaja jak w grze, a ksiega towarow zna je z nazwy
    ///    (GoodsLedger.NoteKnownSource, grupa "zrodla znane z nazwy (KS)" - nie ida do "inne ticki dobowe", wiec bramka (3) liczy tylko zrodla naprawde nieznane); po
    ///    wlaczeniu (projekt rozdz. 10 pkt 2: zamkniete przy KS-A) perk nie robi ryb, a zboze patrolu idzie ze spichlerza miasta (Town.FoodStocks, jak ArmsFromPeople.
    ///    SpawnGrain); w krainie wroconej jak dzis. (iii) Ryby portow BK miast (BKBuildingsBehavior.RunPorts - praca ludzi miasta za zloto kasy; klucz Ks Bk Ports In
    ///    Ledger) - tylko nazwa w ksiedze, gra bez zmian (fin78: 21-22 szt./d w "inne ticki" od doby 109140 - bez nazwy bramka (3) nie przeszlaby nigdy).
    /// Zapis arm_ks_arm (nowy klucz, Z + base64 deflate, limit 64 KB - ponad: bez sredniej ksiegi): srednia ksiegi towarow, dojrzaly Plan od doby, bramki 7 dob,
    /// krainy wrocone, serie spadku spichlerzy, strata spichlerzy krain od wlaczenia (L=), id kampanii. Brak klucza (stary zapis) - pomiar od zera; starsza DLL go pomija.
    /// Klucze wylaczone - stara droga bez zmian.
    /// Wyjatki per wies liczone (potkniecia) - bez globalnych wylacznikow.
    /// </summary>
    internal static partial class StartCalibration
    {
        // ------------------------------------------------------------ klucze i stale
        private static bool AutoArmKey { get { var s = Settings.Current; return s != null && s.StartCalibrationAutoArm; } }
        private static bool LandGateKey { get { var s = Settings.Current; return s != null && s.KsLandGate; } }
        private static bool HardGatesKey { get { var s = Settings.Current; return s != null && s.KsHardGates; } }
        private static int ReserveDaysKey { get { var s = Settings.Current; int d = s != null ? s.KsLandGateReserveDays : 364; return d < 0 ? 0 : (d > 728 ? 728 : d); } }
        private static bool NoPerkFishKey { get { var s = Settings.Current; return s != null && s.KsNoFishFromPerk; } }
        private static bool AutoTestKey { get { var s = Settings.Current; return s != null && s.StartCalibrationAutoArmAutotest; } }
        // recenzja ks-arm (noc 17/18.10): budzet straty spichlerzy krainy pod KS, zboze patroli, bramka bez wylacznika w kampanii
        private static int LossDaysKey { get { var s = Settings.Current; int d = s != null ? s.KsLandGateLossDays : 91; return d < 0 ? 0 : (d > 364 ? 364 : d); } }
        private static bool PatrolKey { get { var s = Settings.Current; return s != null && s.KsPatrolGrainFromTown; } }
        private static bool BkPortsKey { get { var s = Settings.Current; return s != null && s.KsBkPortsInLedger; } }
        /// <summary>Bramka krainy czynna w stanie zastosowanym: w kampanii zawsze (projekt rozdz. 8 - "bez wylacznika poza calym KS"; KS wchodzi do kampanii tylko
        /// z kluczem Ks Land Gate), w autotescie wedlug klucza (stara droga z plikiem armoury-ks-wlacz.flag bez klucza - jak dotad).</summary>
        private static bool LandGateActive { get { return LandGateKey || !AutotestMode(); } }
        /// <summary>Glod wsi dla bramki krainy: brak u ponad polowe progu zgonow Famine (domyslnie 5%) - racjonowanie (u = 10% przy spichlerzu ponizej FamineRationDays)
        /// liczy sie, zgony (u > 10%) jeszcze nie musza nastapic. Recenzja ks-arm: dotad 10% = prog zgonow.</summary>
        private static float LandHungerU { get { return 0.5f * Famine.ThresholdNow; } }

        internal const string AutoFlagName = "armoury-ks-auto.flag";        // autotest: droga samego uzbrojenia (jak w grze Jeffa) zamiast wlaczenia przy pierwszym Planie
        internal const string GateTestFlagName = "armoury-ks-bramka.flag";  // autotest: bramka krainy bez progu zapasu (doslownie projekt 16.10) - proba mechanizmu powrotu
        private const string ArmVersion = "ka1";
        private const int GateDays = 7;                 // twarde bramki: 7 pelnych dob ksiegi; 7 dob dojrzalego Planu
        private const double GateMaxPerDay = 2.0;       // projekt rozdz. 10: najwyzej 2 szt./d na towar
        private const double LandDrawShare = 0.01;      // projekt rozdz. 8: spadek > 1% potrzeby na dobe
        private const int LandDrawDays = 7;             // ... przez 7 dob
        private const int ArmSaveMax = 64 * 1024;

        // ------------------------------------------------------------ stan (zapis arm_ks_arm)
        private static bool _autoArm;                   // zapis uzbrojony droga samego uzbrojenia (albo przejety przez nia)
        private static int _matureSince = -1;           // doba pierwszego dojrzalego Planu tego uzbrojenia (ksiega >= 28 dob)
        private sealed class LandBack { public int Day; public string Why; }
        private static readonly Dictionary<string, LandBack> _back = new Dictionary<string, LandBack>(StringComparer.Ordinal);
        private static volatile HashSet<Settlement> _backSet = new HashSet<Settlement>();   // osady krain wroconych (haki - tylko odczyt opublikowanego zbioru)
        private static readonly Dictionary<string, int> _drawStreak = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, double> _landLoss = new Dictionary<string, double>(StringComparer.Ordinal);   // recenzja ks-arm: strata spichlerzy krainy pod KS od wlaczenia (dni potrzeby, netto)
        private static readonly SortedDictionary<int, Dictionary<string, long[]>> _gateRing = new SortedDictionary<int, Dictionary<string, long[]>>();
        private static string _armPending, _armKeep;

        // ------------------------------------------------------------ pamiec sesji
        private static int _loadDay = int.MinValue, _gateLastDay = -1, _armExportLogged = -1, _armStum, _gateStumDay;
        private static bool _emaRestored;
        private static string _whyCodes;
        private static int _whyDay = int.MinValue;
        private static readonly Dictionary<string, long[]> _gateTmp = new Dictionary<string, long[]>(StringComparer.Ordinal);
        private static readonly Dictionary<Village, double> _gPrev = new Dictionary<Village, double>();       // spichlerz wsi wczoraj (bramka krainy)
        private static readonly Dictionary<Village, float[]> _gHarv = new Dictionary<Village, float[]>();     // [wzor gry, racje KS] z Famine.Daily tej doby
        private static int _perkFishSkip, _autoFlagDay = int.MinValue, _gPrevDay = int.MinValue; private static bool _autoFlagVal;
        private static bool _perkPatched, _patrolPatched, _portPatched;
        private static bool _autoApplying;
        // recenzja ks-arm: zrodla z niczego znane z nazwy (sesja) - ryby z perku nazwane w ksiedze przed wlaczeniem, zboze patroli (nazwane / ze spichlerza miasta / bez zapasu)
        private static long _perkFishNamed, _patrolNamed, _patrolPaid, _patrolNone, _portFishNamed;
        private static int _backDep;                    // wsie innych krain z miastem targowym w krainie wroconej (w _backSet)

        private static void ArmReset()
        {
            _autoArm = false; _matureSince = -1; _back.Clear(); _backSet = new HashSet<Settlement>(); _drawStreak.Clear(); _gateRing.Clear(); _landLoss.Clear();
            _armPending = null; _armKeep = null; _loadDay = int.MinValue; _gateLastDay = -1; _armExportLogged = -1; _armStum = 0; _gateStumDay = 0;
            _emaRestored = false; _whyCodes = null; _whyDay = int.MinValue; _gateTmp.Clear(); _gPrev.Clear(); _gHarv.Clear(); _perkFishSkip = 0;
            _autoFlagDay = int.MinValue; _autoFlagVal = false; _autoApplying = false; _gPrevDay = int.MinValue;
            _perkFishNamed = _patrolNamed = _patrolPaid = _patrolNone = _portFishNamed = 0; _backDep = 0;
        }

        private static string CampaignId()
        {
            try { return Campaign.Current != null ? Campaign.Current.UniqueGameId ?? "" : ""; } catch { return ""; }
        }

        private static string FlagPath(string name)
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), name);
        }

        /// <summary>Plik armoury-ks-auto.flag (raz na dobe - bez dysku przy kazdym pytaniu); tylko autotest go czyta.</summary>
        private static bool AutoFlag()
        {
            int t = Today();
            if (t == _autoFlagDay) return _autoFlagVal;
            _autoFlagDay = t;
            try { _autoFlagVal = System.IO.File.Exists(FlagPath(AutoFlagName)); } catch { _autoFlagVal = false; }
            return _autoFlagVal;
        }

        private static bool GateTestFlag()
        {
            try { return System.IO.File.Exists(FlagPath(GateTestFlagName)); } catch { return false; }
        }

        /// <summary>Droga samego uzbrojenia: klucz Start Calibration Auto Arm i zwykla gra; w autotescie tylko z plikiem armoury-ks-auto.flag albo kluczem
        /// Start Calibration Auto Arm Autotest (inne autotesty bez zmian).</summary>
        private static bool AutoPathOn()
        {
            if (!AutoArmKey) return false;
            return !AutotestMode() || AutoTestOn();
        }

        /// <summary>Autotest: droga samego uzbrojenia zamowiona plikiem armoury-ks-auto.flag albo kluczem Start Calibration Auto Arm Autotest.</summary>
        private static bool AutoTestOn() { return AutoTestKey || AutoFlag(); }

        private static void ConsumeAutoFlag()
        {
            try
            {
                string p = FlagPath(AutoFlagName);
                if (System.IO.File.Exists(p)) { System.IO.File.Delete(p); Log.Info(PK + "plik-przelacznik " + AutoFlagName + " zuzyty (usuniety) przy samym wlaczeniu w autotescie."); }
                _autoFlagDay = int.MinValue;
            }
            catch (Exception e) { Stumble("ConsumeAutoFlag", e); }
        }

        // ============================================================ latki: zrodla z niczego znane z nazwy (perk Night Raider NavalDLC, zboze patroli miast)
        internal static void ArmApplyAll(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("NavalDLC.CampaignBehaviors.NavalFishingCampaignBehaviour");
                var m = t != null ? AccessTools.DeclaredMethod(t, "OnDailyTickSettlement", new[] { typeof(Settlement) }) : null;
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(StartCalibration), nameof(PerkFishPrefix)), postfix: new HarmonyMethod(typeof(StartCalibration), nameof(PerkFishPostfix)));
                    _perkPatched = true;
                }
                Log.Info("StartCalibration (ks-arm): ryby z niczego (perk Night Raider gubernatora, NavalDLC) - " + (m != null ? "latka wpieta (NavalFishingCampaignBehaviour.OnDailyTickSettlement)" : "BRAK metody (NavalDLC nieobecny albo inna wersja)")
                         + "; klucz Ks No Fish From Perk: przed wlaczeniem KS ryby jak w grze, nazwane w ksiedze towarow; po wlaczeniu zablokowane (poza krainami wroconymi).");
            }
            catch (Exception e) { Log.Error("StartCalibration.ArmApplyAll", e); }
            try
            {
                // recenzja ks-arm: zboze nowej druzyny patrolowej miasta (G 1.4.8 PatrolPartyComponent.CreatePatrolParty: ItemRoster.Add(Grain, 15..30) - z niczego);
                // metoda statyczna zadeklarowana w PatrolPartyComponent, jedna przeciazona wersja (5 parametrow, homeSettlement = __3)
                var pt = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Party.PartyComponents.PatrolPartyComponent");
                var pm = pt != null ? AccessTools.DeclaredMethod(pt, "CreatePatrolParty") : null;
                if (pm != null && pm.IsStatic && pm.GetParameters().Length == 5 && pm.GetParameters()[3].ParameterType == typeof(Settlement))
                {
                    h.Patch(pm, postfix: new HarmonyMethod(typeof(StartCalibration), nameof(PatrolGrainPostfix)));
                    _patrolPatched = true;
                }
                Log.Info("StartCalibration (ks-arm): zboze nowych druzyn patrolowych miast (15-30 szt. z niczego) - " + (_patrolPatched ? "latka wpieta (PatrolPartyComponent.CreatePatrolParty)" : "BRAK metody albo inna sygnatura")
                         + "; klucz Ks Patrol Grain From Town: przed wlaczeniem KS jak w grze, nazwane w ksiedze towarow; po wlaczeniu ze spichlerza miasta (poza krainami wroconymi).");
            }
            catch (Exception e) { Log.Error("StartCalibration.ArmApplyAll", e); }
            try
            {
                // recenzja ks-arm: porty BK miast (BannerKings BKBuildingsBehavior.RunPorts, tick miasta: ryby = poziom portu x dzierzawcy / 300 albo rzemieslnicy / 200, za zloto
                // kasy miasta - dekompilacja BK) - w ksiedze "inne ticki dobowe" ok. 22 szt./d (fin78 od doby 109140), bramka (3) nie przeszlaby nigdy; tylko nazwa w ksiedze
                var bt = AccessTools.TypeByName("BannerKings.Behaviours.BKBuildingsBehavior");
                var bm = bt != null ? AccessTools.DeclaredMethod(bt, "RunPorts", new[] { typeof(Town) }) : null;
                if (bm != null)
                {
                    h.Patch(bm, prefix: new HarmonyMethod(typeof(StartCalibration), nameof(BkPortPrefix)), postfix: new HarmonyMethod(typeof(StartCalibration), nameof(BkPortPostfix)));
                    _portPatched = true;
                }
                Log.Info("StartCalibration (ks-arm): ryby portow BK miast (BKBuildingsBehavior.RunPorts, za zloto kasy miasta) - " + (_portPatched ? "latka wpieta" : "BRAK metody (BK nieobecny albo inna wersja)")
                         + "; klucz Ks Bk Ports In Ledger: tylko nazwa w ksiedze towarow (gra bez zmian).");
            }
            catch (Exception e) { Log.Error("StartCalibration.ArmApplyAll", e); }
        }

        /// <summary>Prefiks BKBuildingsBehavior.RunPorts: ryby w magazynie miasta przed polowem portu (do nazwy w ksiedze). Gra bez zmian.
        /// Scalenie ze stare-a17 (A17 PortsPre na tej samej metodzie, priorytet First, przy KS zastosowanym zwraca false): stan = ryby + 1, a 0 (domyslne __state,
        /// gdy ten prefiks nie biegl) znaczy "bez pomiaru" - postfiks nie moze wziac calego magazynu za przyrost.</summary>
        public static void BkPortPrefix(Town __0, out int __state)
        {
            __state = 0;
            try
            {
                if (__0 == null || !BkPortsKey || !KeyOn || !GoodsLedger.On) return;
                var ow = __0.Owner; var fish = ItemOf("fish");
                if (ow != null && ow.ItemRoster != null && fish != null) __state = ow.ItemRoster.GetItemNumber(fish) + 1;
            }
            catch { __state = 0; }
        }

        /// <summary>Postfiks BKBuildingsBehavior.RunPorts: przyrost ryb w magazynie miasta - nazwane zrodlo ksiegi towarow (z ramki ticku miasta).</summary>
        public static void BkPortPostfix(Town __0, int __state)
        {
            if (__state <= 0 || __0 == null) return;                    // scalenie: 0 = prefiks nie biegl / bez pomiaru
            int fish0 = __state - 1;
            try
            {
                var ow = __0.Owner; var fish = ItemOf("fish");
                if (ow == null || ow.ItemRoster == null || fish == null) return;
                int n = ow.ItemRoster.GetItemNumber(fish) - fish0;
                if (n <= 0) return;
                _portFishNamed += n;
                GoodsLedger.NoteKnownSource(fish, n, "porty BK miast - polow za zloto kasy miasta");
            }
            catch { }
        }

        /// <summary>Prefiks NavalFishingCampaignBehaviour.OnDailyTickSettlement (metoda robi tylko ryby z perku Night Raider dla wsi z miastem targowym - dekompilacja
        /// NavalDLC): KS zastosowany - pominieta (poza wsia krainy wroconej albo z miastem targowym w niej); przed wlaczeniem - dziala jak w grze, a prefiks zapamietuje
        /// ryby w magazynie wsi (__state), zeby postfiks nazwal przyrost w ksiedze towarow (recenzja ks-arm: nic sie nie zmienia przed koncem pomiaru).</summary>
        public static bool PerkFishPrefix(Settlement __0, out int __state)
        {
            __state = 0;                                        // scalenie ze stare-a17 (A17 NavalVillagePre na tej samej metodzie): stan = ryby + 1, 0 = bez pomiaru
            try
            {
                if (__0 == null || !__0.IsVillage || __0.Village == null || !NoPerkFishKey || !KeyOn) return true;
                if (_state == StateApplied)
                {
                    if (LandBackOf(__0)) return true;           // kraina wrocona (albo wies z miastem targowym w niej) - jak dzis
                    _perkFishSkip++;
                    return false;                               // KS zastosowany (projekt rozdz. 10 pkt 2): ryby tylko z rak rybakow
                }
                if (!GoodsLedger.On) return true;               // ksiega towarow wylaczona - nic do nazwania (bramka (3) i tak bez liczb)
                var ow = __0.Village.Owner; var fish = ItemOf("fish");
                if (ow != null && ow.ItemRoster != null && fish != null) __state = ow.ItemRoster.GetItemNumber(fish) + 1;
                return true;
            }
            catch { __state = 0; return true; }
        }

        /// <summary>Postfiks: przyrost ryb w magazynie wsi z perku (przed wlaczeniem KS) - nazwane zrodlo ksiegi towarow (z ramki ticku osady do pozycji nazwanej).</summary>
        public static void PerkFishPostfix(Settlement __0, int __state)
        {
            if (__state <= 0 || __0 == null) return;                    // scalenie: 0 = prefiks nie biegl / bez pomiaru
            int fish0 = __state - 1;
            try
            {
                var ow = __0.Village != null ? __0.Village.Owner : null; var fish = ItemOf("fish");
                if (ow == null || ow.ItemRoster == null || fish == null) return;
                int n = ow.ItemRoster.GetItemNumber(fish) - fish0;
                if (n <= 0) return;
                _perkFishNamed += n;
                GoodsLedger.NoteKnownSource(fish, n, "ryby z perku Night Raider (NavalDLC) - z niczego, przy KS zastosowanym zamkniete");
            }
            catch { }
        }

        /// <summary>Postfiks PatrolPartyComponent.CreatePatrolParty: zboze nowej druzyny patrolowej (gra 15-30 szt. z niczego). Przed wlaczeniem KS - nazwane zrodlo
        /// ksiegi (gra bez zmian); KS zastosowany - ze spichlerza miasta druzyny (Town.FoodStocks >= 2 x zboze, jak ArmsFromPeople.SpawnGrain), bez zapasu - zostaje,
        /// nazwane "bez zapasu"; kraina wrocona - jak dzis.</summary>
        public static void PatrolGrainPostfix(TaleWorlds.CampaignSystem.Party.MobileParty __result, Settlement __3)
        {
            try
            {
                if (__result == null || __3 == null || !PatrolKey || !KeyOn || Campaign.Current == null || !Campaign.Current.GameStarted) return;
                var ro = __result.ItemRoster; var grain = DefaultItems.Grain;
                if (ro == null || grain == null) return;
                int g = ro.GetItemNumber(grain);
                if (g <= 0) return;
                if (_state == StateApplied)
                {
                    if (LandBackOf(__3)) return;                // kraina wrocona - jak dzis
                    var t = __3.Town;
                    if (t != null && t.FoodStocks >= 2f * g)
                    {
                        t.FoodStocks -= g; _patrolPaid += g;    // zamknieta ekonomia: platnik - spichlerz miasta (FoodStocks), odbiorca - druzyna patrolowa
                        GoodsLedger.NoteKnownSource(grain, g, "druzyny patrolowe miast - ze spichlerza miasta (KS)");
                        return;
                    }
                    _patrolNone += g;
                    GoodsLedger.NoteKnownSource(grain, g, "druzyny patrolowe miast - z niczego (miasto bez zapasu)");
                    return;
                }
                _patrolNamed += g;
                GoodsLedger.NoteKnownSource(grain, g, "druzyny patrolowe miast - z niczego (gra; przy KS zastosowanym ze spichlerza miasta)");
            }
            catch (Exception e) { _armStum++; Stumble("PatrolGrainPostfix", e); }
        }

        // ============================================================ srednia ksiegi towarow i bramki - pomiar doby
        /// <summary>Daily (po LedgerEma): liczby bramek doby (zrodla bez nazwy, RemoveParty) do pierscienia 7 pelnych dob - pierwsza doba sesji (od wczytania) pominieta.</summary>
        private static void GateCollect()
        {
            if (_state == StateApplied) { if (_gateRing.Count > 0) _gateRing.Clear(); return; }   // po wlaczeniu bramki nie licza (zapis bez starych dob)
            if (!AutoArmKey && !HardGatesKey) return;
            int d = GoodsLedger.KsGateTake(_gateTmp);
            if (d < 0 || d == _gateLastDay) return;
            _gateLastDay = d;
            if (d == _loadDay) return;
            var copy = new Dictionary<string, long[]>(_gateTmp.Count, StringComparer.Ordinal);
            foreach (var kv in _gateTmp) if (kv.Value != null && kv.Value.Length >= 2) copy[kv.Key] = new[] { kv.Value[0], kv.Value[1] };
            _gateRing[d] = copy;
            var old = new List<int>();
            foreach (var k in _gateRing.Keys) if (k < d - 2 * GateDays) old.Add(k);
            foreach (var k in old) _gateRing.Remove(k);
            while (_gateRing.Count > GateDays) { int first = -1; foreach (var k in _gateRing.Keys) { first = k; break; } _gateRing.Remove(first); }
        }

        /// <summary>Twarde bramki rozdz. 10 (bez ZC1 i ukladu - te sprawdza GameGates). false - powod (log) i komunikat po angielsku; line - liczby do linii.</summary>
        private static bool HardGates(out string why, out string en, out string line)
        {
            var s = Settings.Current;
            int n = _gateRing.Count;
            double maxU = 0, maxR = 0; string itU = "-", itR = "-";
            var overU = new List<string>(); var overR = new List<string>();
            var sumU = new Dictionary<string, long>(StringComparer.Ordinal); var sumR = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var day in _gateRing.Values)
                foreach (var kv in day)
                {
                    long a; sumU.TryGetValue(kv.Key, out a); sumU[kv.Key] = a + kv.Value[0];
                    long b; sumR.TryGetValue(kv.Key, out b); sumR[kv.Key] = b + kv.Value[1];
                }
            foreach (var kv in sumU)
            {
                double v = n > 0 ? kv.Value / (double)n : 0;
                if (v > maxU) { maxU = v; itU = kv.Key; }
                if (v > GateMaxPerDay) overU.Add(kv.Key + " " + v.ToString("0.0", Inv));
            }
            foreach (var kv in sumR)
            {
                if (!IsFoodId(kv.Key)) continue;
                double v = n > 0 ? kv.Value / (double)n : 0;
                if (v > maxR) { maxR = v; itR = kv.Key; }
                if (v > GateMaxPerDay) overR.Add(kv.Key + " " + v.ToString("0.0", Inv));
            }
            bool leaks = s == null || (s.DteConvoyNoFreeFood && s.BkCaravanNoFreeFood);
            line = "twarde bramki (rozdz. 10): pelnych dob ksiegi " + n + "/" + GateDays + ", zrodla bez nazwy max " + maxU.ToString("0.0", Inv) + " szt./d (" + itU + ")"
                   + (overU.Count > 0 ? " [ponad " + GateMaxPerDay.ToString("0", Inv) + "/d: " + string.Join(", ", overU.ToArray()) + "]" : "")
                   + ", RemoveParty jedzenia max " + maxR.ToString("0.0", Inv) + " szt./d (" + itR + ")" + (overR.Count > 0 ? " [ponad: " + string.Join(", ", overR.ToArray()) + "]" : "")
                   + ", jedzenie z niczego konwojow DTE i partii BK zamkniete " + (leaks ? "TAK" : "NIE")
                   + " | zrodla z niczego znane z nazwy (przed wlaczeniem jak w grze, zamkniete przy wlaczeniu): ryby z perku Night Raider (Ks No Fish From Perk) "
                   + (NoPerkFishKey ? (_perkPatched ? "nazwane " + _perkFishNamed + " szt. od startu sesji" : "BRAK latki") : "NIE")
                   + ", zboze patroli miast (Ks Patrol Grain From Town) " + (PatrolKey ? (_patrolPatched ? "nazwane " + _patrolNamed + " szt. od startu sesji" : "BRAK latki") : "NIE")
                   + "; produkcja znana z nazwy (gra bez zmian): ryby portow BK miast (Ks Bk Ports In Ledger) " + (BkPortsKey ? (_portPatched ? "nazwane " + _portFishNamed + " szt. od startu sesji" : "BRAK latki") : "NIE");
            if (!HardGatesKey) { why = en = null; line += " | klucz Ks Hard Gates NIE - bramki tylko liczone"; return true; }
            if (n < GateDays) { why = "twarde bramki: za malo pelnych dob ksiegi towarow (" + n + "/" + GateDays + ")"; en = "the goods ledger needs " + GateDays + " full days first."; return false; }
            if (!leaks) { why = "twarde bramki: jedzenie z niczego konwojow DTE albo partii BK otwarte (DteConvoyNoFreeFood / BkCaravanNoFreeFood)"; en = "switch on Dte Convoy No Free Food and Bk Caravan No Free Food first."; return false; }
            if (overU.Count > 0) { why = "twarde bramki: zrodla bez nazwy ponad " + GateMaxPerDay.ToString("0", Inv) + " szt./d (" + string.Join(", ", overU.ToArray()) + ")"; en = "goods still appear from nowhere in the ledger - waiting."; return false; }
            if (overR.Count > 0) { why = "twarde bramki: jedzenie znika z partiami usunietymi (RemoveParty) ponad " + GateMaxPerDay.ToString("0", Inv) + " szt./d (" + string.Join(", ", overR.ToArray()) + ")"; en = "food still vanishes with removed parties - waiting."; return false; }
            why = en = null;
            return true;
        }

        /// <summary>Confirm (wlaczenie reczne w grze): twarde bramki rozdz. 10 jak przy samym wlaczeniu; false - linia i komunikat (klucz wraca na Off).</summary>
        private static bool HardGatesConfirm(int day)
        {
            string w, en, line;
            if (HardGates(out w, out en, out line)) return true;
            Log.Info(PK + "dzien " + day + " | Confirm odrzucony: " + w + " | " + line + " - nic, klucz wraca na Off.");
            Tell("Start calibration: " + en);
            return false;
        }

        private static readonly HashSet<string> FoodIds = new HashSet<string>(StringComparer.Ordinal)
        { "Egg", "WhaleMeat", "apple", "beer", "bread", "butter", "carrot", "cheese", "date_fruit", "fish", "grain", "grape", "honey", "meat", "olives", "orange", "pie", "wine", "mead", "oil" };
        private static bool IsFoodId(string id) { return id != null && FoodIds.Contains(id); }

        // ============================================================ samo uzbrojenie i samo wlaczenie (stan Plan / Uzbrojony)
        /// <summary>Daily w stanie Plan albo Uzbrojony (po KeysDaily): uzbrojenie zapisu i - po pomiarze i bramkach - wlaczenie z rampa. Klucz wylaczony - nic.</summary>
        private static void ArmDaily(int today, bool planOk)
        {
            if (!AutoPathOn() || _keepRaw != null || _state == StateApplied) return;
            int day = today - 1;
            string gid = CampaignId();
            if (_state == StatePlan)
            {
                uint lay0;
                if (!DailyLife.KeyOn || !LayoutMatches(out lay0))
                {
                    WhyLine(day, "samo uzbrojenie czeka", !DailyLife.KeyOn ? "Daily Life Measure wylaczony (bez stanow ZC nie ma koszyka ZC4)" : "uklad wiosek niezgodny z plikiem ziemi (albo ksiega wiosek nieczynna)");
                    return;
                }
                _state = StateArmed; _armDay = today; _armHash = PlanHash(_plan); _armLandCrc = _landCrc; _campaign = gid; _autoArm = true; _matureSince = -1;
                Log.Info(PK + "uzbrojony zapis doba " + today + " (samo - klucz Start Calibration Auto Arm" + (AutotestMode() ? "; autotest: " + (AutoTestKey ? "klucz Start Calibration Auto Arm Autotest" : "plik " + AutoFlagName) : "") + ") | kampania " + (gid.Length > 0 ? gid : "-")
                         + ", plik ziemi " + _landCrc.ToString("X8") + " | pomiar: srednia ksiegi towarow " + Math.Min(_emaDays, MinLedgerDays) + "/" + MinLedgerDays + " dob" + (_emaRestored ? " (z zapisu)" : "")
                         + ", potem " + GateDays + " dob dojrzalego Planu, Plan z pomiarem pieniedzy, bramki gry i twarde bramki rozdz. 10 - wlaczenie samo, najwczesniej doba " + (today + MinLedgerDays + GateDays)
                         + " | do wlaczenia nic w grze sie nie zmienia (ryby z perku Night Raider i zboze patroli miast tylko nazwane w ksiedze towarow - zamkniete przy wlaczeniu) | wylacznik: Start Calibration = off albo Start Calibration Auto Arm = off.");
                Tell("The markets of the realm are being surveyed: for about five weeks the stewards count what every district grows, makes and eats. Nothing changes yet.");
                return;
            }
            if (_state != StateArmed) return;
            if (!_autoArm)
            {
                _autoArm = true;
                Log.Info(PK + "dzien " + day + " | zapis uzbrojony recznie doba " + _armDay + " - przejety przez samo wlaczenie (klucz Start Calibration Auto Arm): wlaczy sie sam po pomiarze i bramkach (Confirm niepotrzebny).");
            }
            if (gid.Length > 0 && _campaign.Length > 0 && gid != _campaign) { WhyLine(day, "samo wlaczenie - nic", "zapis uzbrojony innej kampanii (id " + _campaign + ", gra " + gid + ")"); return; }
            if (_armLandCrc != _landCrc)
            {
                Log.Info(PK + "dzien " + day + " | samo wlaczenie: plik ziemi inny niz przy uzbrojeniu (" + _armLandCrc.ToString("X8") + " / teraz " + _landCrc.ToString("X8") + ") - dojrzaly Plan liczony od nowa.");
                _armLandCrc = _landCrc; _matureSince = -1;
            }
            bool mature = PlanMature();
            if (mature && _matureSince < 0) _matureSince = Math.Max(_planDay, _armDay);
            var why = new List<string>(); var codes = new StringBuilder();
            if (!mature) { why.Add("srednia ksiegi towarow " + Math.Min(_emaDays, MinLedgerDays) + "/" + MinLedgerDays + " dob (ostatni Plan z " + _planEma + ")"); codes.Append('E'); }
            else if (!_plan.MoneyMeasured) { why.Add("Plan doby " + _planDay + " bez pomiaru pieniedzy (7 dob tej sesji)"); codes.Append('M'); }
            int md = _matureSince >= 0 ? today - _matureSince : 0;
            if (_matureSince < 0 || md < GateDays) { why.Add("dojrzaly Plan " + (_matureSince >= 0 ? "od doby " + _matureSince + " (" + md + "/" + GateDays + " dob)" : "jeszcze nie") ); codes.Append('P'); }
            string gw = null, ge;
            if (mature && !GameGates(planOk, out gw, out ge)) { why.Add(gw); codes.Append('G').Append(gw.Length > 12 ? gw.Substring(0, 12) : gw); }
            if (!LandGateReady) { why.Add("bramka krainy wylaczona (klucz Ks Land Gate) - bez niej KS nie wchodzi do kampanii"); codes.Append('L'); }
            string hw, he, hline;
            if (!HardGates(out hw, out he, out hline)) { why.Add(hw); codes.Append('H').Append(hw.Length > 16 ? hw.Substring(0, 16) : hw); }
            if (why.Count == 0) { ApplyAuto(today, hline); return; }
            string cs = codes.ToString();
            if (LogGate.Lines || cs != _whyCodes)
            {
                _whyCodes = cs;
                Log.Info(PK + "pomiar przed wlaczeniem dzien " + day + " | stan UZBROJONY doba " + _armDay + " (samo) | srednia ksiegi towarow " + Math.Min(_emaDays, MinLedgerDays) + "/" + MinLedgerDays + " dob"
                         + " (Plan doby " + _planDay + " z " + _planEma + ", pieniadze z pomiaru " + (_plan != null && _plan.MoneyMeasured ? "TAK" : "NIE") + ")"
                         + " | dojrzaly Plan " + (_matureSince >= 0 ? "od doby " + _matureSince + " (" + md + "/" + GateDays + " dob)" : "-")
                         + " | bramki gry " + (mature ? (gw == null ? "TAK" : "NIE") : "-") + " | bramka krainy " + (LandGateReady ? "TAK" : "NIE") + " | " + hline
                         + " | WYNIK: czeka (" + why.Count + "): " + string.Join("; ", why.ToArray()) + ".");
            }
        }

        /// <summary>Linia "czeka" raz na zmiane powodu (albo w dobie linii).</summary>
        private static void WhyLine(int day, string head, string why)
        {
            if (!LogGate.Lines && why == _whyCodes) return;
            _whyCodes = why;
            Log.Info(PK + "pomiar przed wlaczeniem dzien " + day + " | " + head + ": " + why + ".");
        }

        private static void ApplyAuto(int today, string gatesLine)
        {
            Log.Info(PK + "samo wlaczenie doba " + today + " | zapis uzbrojony doba " + _armDay + " (samo), Plan doby " + _planDay + " (nr " + _plans + ") z ksiega towarow " + _planEma + " dob, pieniadze z pomiaru TAK"
                     + ", dojrzaly Plan od doby " + _matureSince + " (" + (today - _matureSince) + " dob) | " + gatesLine + " | bramka krainy TAK (" + LandGateRule(false) + ")"
                     + " | pieniadze: przelewow 0 zl, towar: 0 szt. (samo wlaczenie zmienia tylko zasady produkcji i popytu - rampa) | wszystkie bramki TAK - wlaczam.");
            _autoApplying = true;
            try { Apply(today, false); }
            finally { _autoApplying = false; }
            Tell("The survey of the realm is done. Over the next two weeks the villages turn to what their own hands and land yield; over the two weeks after that the people's daily basket comes to the markets.");
            if (AutotestMode()) ConsumeAutoFlag();
        }

        /// <summary>Progi bramki krainy do linii (jedno miejsce).</summary>
        private static string LandGateRule(bool test)
        {
            int reserve = ReserveDaysKey, loss = LossDaysKey;
            return "glod wsi z winy KS: brak u > " + (100 * LandHungerU).ToString("0.#", Inv) + "% (polowa progu zgonow Famine - racjonowanie przy spichlerzu ponizej FamineRationDays), poza wojna TERAZ, racje KS ponizej wzoru gry"
                   + "; albo spadek spichlerzy krainy > 1% potrzeby/d przez " + LandDrawDays + " dob przy racjach KS ponizej wzoru gry"
                   + (test ? " (autotest: plik " + GateTestFlagName + " - bez progow straty i zapasu)"
                           : (loss == 0 || reserve == 0 ? " (bez progu - doslownie projekt 16.10)" : " po stracie pod KS >= " + loss + " dni potrzeby albo przy zapasie ponizej " + reserve + " dni"));
        }

        // ============================================================ bramka krainy (stan zastosowany)
        /// <summary>Famine.Daily (po StartCalibration.GranaryFloor): plon wsi ze wzoru gry i racje, ktore dostala - bramka krainy tej doby (glod z winy KS, racje krainy).</summary>
        internal static void NoteGranary(Village v, double formula, double given)
        {
            if (v == null || _state != StateApplied || !LandGateActive) return;
            try { _gHarv[v] = new[] { (float)formula, (float)given }; } catch { }
        }

        /// <summary>Osada krainy wroconej przez bramke albo wies z miastem targowym w krainie wroconej (stawki, koszyk i popyt ZC4, przetwory, KS-H, B2 jak dzis).
        /// Bez krain wroconych - false. Haki - sam odczyt opublikowanego zbioru.</summary>
        internal static bool LandBackOf(Settlement st)
        {
            var b = _backSet;
            return st != null && b.Count > 0 && b.Contains(st);
        }

        private sealed class LandAgg { public int N, Skip, Hungry; public double F, K, Food, Need, Drop, NeedD; public string Example; }

        /// <summary>Daily w stanie zastosowanym, PRZED LiveDaily (stawki jutra juz bez krainy wroconej): glod wsi z winy KS i spadek spichlerzy krain.</summary>
        private static void LandGateDaily(int today)
        {
            if (!LandGateActive) { _gHarv.Clear(); return; }
            long t0 = Stopwatch.GetTimestamp();
            int day = today - 1;
            bool famine = Famine.On;
            if (_back.Count > 0) RebuildBackSet();               // recenzja ks-arm: wsie z miastem targowym w krainie wroconej (TradeBound zmienia sie z wlascicielem) - przed stawkami jutra
            if (_gPrevDay != today - 1) _gPrev.Clear();          // przerwa (Undo, wylaczony klucz) - spadek liczony tylko z doby na dobe
            _gPrevDay = today;
            float hungerU = LandHungerU;
            var agg = new Dictionary<string, LandAgg>(StringComparer.Ordinal);
            var seen = new HashSet<Village>();
            int stum = 0, depSkip = 0;
            foreach (var ld in _live.Values)
            {
                if (ld == null || ld.TF || ld.V == null || ld.St == null) continue;
                var v = ld.V;
                if (!seen.Add(v)) continue;
                try
                {
                    string land = TownLandOf(ld.St);
                    if (_back.ContainsKey(land)) continue;
                    if (LandBackOf(ld.St)) { _gPrev.Remove(v); depSkip++; continue; }   // wies z miastem targowym w krainie wroconej - jak dzis, poza miara swojej krainy
                    LandAgg a; if (!agg.TryGetValue(land, out a)) agg[land] = a = new LandAgg();
                    bool ok = v.VillageState == Village.VillageStates.Normal && !(v.Bound != null && v.Bound.IsUnderSiege);
                    if (!ok) { _gPrev.Remove(v); a.Skip++; continue; }
                    a.N++;
                    float[] hv; bool hasH = _gHarv.TryGetValue(v, out hv) && hv != null;
                    if (hasH) { a.F += hv[0]; a.K += hv[1]; }
                    double food = 0; bool hasF = famine && Famine.FoodOf(v, out food);
                    float k = PopulationLaw.KFor(ld.St);
                    double need = (k > 0f && !float.IsInfinity(k) ? Math.Max(0f, v.Hearth) * (double)k : 0.0) + Math.Max(0.0, PeopleBook.RefugeesOf(v));
                    if (hasF && need > 0)
                    {
                        a.Food += food; a.Need += need;
                        double prev;
                        if (_gPrev.TryGetValue(v, out prev)) { a.Drop += prev - food; a.NeedD += need; }
                        _gPrev[v] = food;
                    }
                    else _gPrev.Remove(v);
                    float u = famine ? Famine.HungerOf(v) : 0f;
                    if (u > hungerU && hasH && hv[1] < hv[0] - 0.5f && !Famine.HungerFromWar(v))
                    {
                        a.Hungry++;
                        if (a.Example == null) { try { a.Example = ld.St.Name != null ? ld.St.Name.ToString() : ld.St.StringId; } catch { a.Example = ld.St.StringId; } }
                    }
                }
                catch (Exception e) { stum++; _armStum++; Stumble("LandGateDaily(wies)", e); }
            }
            _gHarv.Clear();
            int reserve = ReserveDaysKey, lossKey = LossDaysKey;
            bool test = AutotestMode() && GateTestFlag();
            var ret = new List<KeyValuePair<string, string>>();
            int streakN = 0, drawN = 0, hungryV = 0, nv = 0, skipV = 0; int bestSt = 0; string bestLand = "-"; double bestDraw = 0, bestDays = 0;
            double sumF = 0, sumK = 0, bestLoss = 0; string bestLossLand = "-";
            var top = new List<KeyValuePair<double, string>>();
            foreach (var kv in agg)
            {
                var a = kv.Value;
                nv += a.N; skipV += a.Skip; hungryV += a.Hungry; sumF += a.F; sumK += a.K;
                double draw = a.NeedD > 0 ? a.Drop / a.NeedD : 0.0;     // dni potrzeby krainy stracone dzis (te same wsie wczoraj i dzis - rabunek pominiety)
                double days = a.Need > 0 ? a.Food / a.Need : 0.0;
                bool ksLess = a.K < a.F;
                int st; _drawStreak.TryGetValue(kv.Key, out st);
                double loss; _landLoss.TryGetValue(kv.Key, out loss);
                if (a.NeedD > 0)
                {
                    // recenzja ks-arm (wazne): strata netto od wlaczenia - spadek liczy sie tylko w doby, gdy KS karmi kraine gorzej niz wzor gry; napelnianie oddaje
                    if (draw < 0) loss = Math.Max(0.0, loss + draw);
                    else if (ksLess) loss += draw;
                    bool count = draw > LandDrawShare && ksLess && (test || reserve == 0 || lossKey == 0 || loss >= lossKey || days < reserve);
                    st = count ? st + 1 : 0;      // doba bez pomiaru (pierwsza po wczytaniu, spichlerze wylaczone) - seria i strata bez zmian
                }
                if (loss > 1e-6) _landLoss[kv.Key] = loss; else _landLoss.Remove(kv.Key);
                if (loss > bestLoss) { bestLoss = loss; bestLossLand = kv.Key; }
                if (st > 0) _drawStreak[kv.Key] = st; else _drawStreak.Remove(kv.Key);
                if (draw > LandDrawShare) drawN++;
                if (st > 0) streakN++;
                if (st > bestSt || (st == bestSt && draw > bestDraw)) { bestSt = st; bestLand = kv.Key; bestDraw = draw; bestDays = days; }
                if (a.NeedD > 0) top.Add(new KeyValuePair<double, string>(draw, kv.Key + " " + (100 * draw).ToString("0.0", Inv) + "%/d (zapas " + days.ToString("0", Inv) + " d, strata " + loss.ToString("0.0", Inv) + " d, seria " + st + ")"));
                if (a.Hungry > 0) ret.Add(new KeyValuePair<string, string>(kv.Key, "glod we wsiach " + a.Hungry + " (brak u > " + (100 * hungerU).ToString("0.#", Inv) + "% - racjonowanie albo brak, poza wojna teraz, racje KS ponizej wzoru gry tej doby; np. " + (a.Example ?? "-") + ")"));
                else if (st >= LandDrawDays)
                    ret.Add(new KeyValuePair<string, string>(kv.Key, "spichlerz krainy spada " + (100 * draw).ToString("0.0", Inv) + "% potrzeby na dobe " + st + " dob z rzedu (zapas " + days.ToString("0", Inv) + " dni, strata pod KS " + loss.ToString("0.0", Inv) + " dni"
                                                                    + (test ? ", proba bez progow - plik " + GateTestFlagName : (lossKey > 0 && reserve > 0 ? " - prog straty " + lossKey + " dni albo zapasu " + reserve + " dni" : "")) + "; racje KS krainy " + a.K.ToString("0", Inv) + " ponizej wzoru gry " + a.F.ToString("0", Inv) + ")"));
            }
            foreach (var r in ret) { try { LandReturn(r.Key, today, r.Value, r.Value.StartsWith("glod", StringComparison.Ordinal)); } catch (Exception e) { _armStum++; Stumble("LandReturn", e); } }
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            if (!LogGate.Lines && ret.Count == 0) return;
            top.Sort((x, y) => y.Key.CompareTo(x.Key));
            var tops = new List<string>(); for (int i = 0; i < top.Count && i < 5; i++) tops.Add(top[i].Value);
            var backs = new List<string>(); foreach (var kv in _back) backs.Add(kv.Key + " (doba " + kv.Value.Day + ")");
            Log.Info(PK + "bramka krainy dzien " + day + " | krain z KS " + agg.Count + ", wroconych " + _back.Count + (backs.Count > 0 ? " (" + string.Join(", ", backs.ToArray()) + ")" : "") + " | wsi " + nv + " (pominiete najazd/oblezenie " + skipV
                     + (depSkip > 0 ? ", z miastem targowym w krainie wroconej " + depSkip : "") + ")"
                     + " | glod z winy KS: wsi " + hungryV + " | spichlerze: krain ze spadkiem > 1% potrzeby/d dzis " + drawN + ", z seria " + streakN + " (najdluzsza " + bestSt + " dob - " + bestLand + ", " + (100 * bestDraw).ToString("0.0", Inv) + "%/d, zapas " + bestDays.ToString("0", Inv) + " d)"
                     + " | strata spichlerzy z winy KS od wlaczenia (netto): najwieksza " + bestLoss.ToString("0.0", Inv) + " d - " + bestLossLand + ", krain ze strata " + _landLoss.Count + ", prog straty " + lossKey + " d"
                     + " | prog: " + LandGateRule(test)
                     + " | racje KS / wzor gry (wsie z KS) " + (sumF > 0 ? (sumK / sumF).ToString("0.000", Inv) : "-") + " | najwiekszy spadek: " + (tops.Count > 0 ? string.Join(", ", tops.ToArray()) : "-")
                     + " | spichlerze " + (famine ? "czynne" : "WYLACZONE (Famine) - bez pomiaru") + " | powroty dzis " + ret.Count
                     + " | zrodla z niczego zamkniete (sesja): ryby z perku Night Raider - wywolan pominietych " + _perkFishSkip + ", zboze patroli miast ze spichlerza miasta " + _patrolPaid + " szt., bez zapasu (zostalo, nazwane) " + _patrolNone + " szt."
                     + " | czas " + ms.ToString("0.00", Inv) + " ms, potkniecia " + stum + " (sesja " + _armStum + ").");
        }

        /// <summary>Powrot krainy: zapisany, zbior osad przebudowany (z wsiami innych krain, ktorych miasto targowe tu lezy), stawki wsi, koszyk ZC4 wsi i miasta tej krainy
        /// zdjete od razu (kopie slownikow) - od jutra jak dzis.</summary>
        private static void LandReturn(string land, int today, string why, bool hunger)
        {
            if (land == null || _back.ContainsKey(land)) return;
            _back[land] = new LandBack { Day = today, Why = why };
            _drawStreak.Remove(land);
            _landLoss.Remove(land);
            int dep0 = _backDep;
            RebuildBackSet();
            var bs = _backSet;
            int nv = 0, nt = 0;
            var r = new Dictionary<Village, VRate>(_rates); var drop = new List<Village>();
            foreach (var kv in r) if (kv.Key != null && kv.Key.Settlement != null && bs.Contains(kv.Key.Settlement)) drop.Add(kv.Key);
            foreach (var v in drop) r.Remove(v);
            _rates = r;
            var rk = new Dictionary<Village, VRate>(_ratesKeep); foreach (var v in drop) rk.Remove(v); _ratesKeep = rk;
            var zt = new Dictionary<Settlement, ZTown>(_zt); var dropT = new List<Settlement>();
            foreach (var kv in zt) if (kv.Key != null && bs.Contains(kv.Key)) dropT.Add(kv.Key);
            foreach (var t in dropT) zt.Remove(t);
            _zt = zt; nt = dropT.Count;
            var zv = new Dictionary<Village, float[]>(_zv); var zvp = new Dictionary<Village, float[]>(_zvp);
            foreach (var v in new List<Village>(zv.Keys)) if (v != null && v.Settlement != null && bs.Contains(v.Settlement)) zv.Remove(v);
            foreach (var v in new List<Village>(zvp.Keys)) if (v != null && v.Settlement != null && bs.Contains(v.Settlement)) zvp.Remove(v);
            _zv = zv; _zvp = zvp;
            foreach (var st in bs) if (st != null && st.IsVillage && TownLandOf(st) == land) nv++;
            Log.Info(PK + "powrot krainy " + land + " doba " + today + " - " + why + " | od jutra ta kraina wraca do dzisiejszych zasad: stawki i zywnosc wsi gry, spichlerz ze wzoru gry, koszyk i popyt ZC4, przetwory ZC5, kupcy KS-H i B2 bez KS;"
                     + " reszta swiata dalej z KS (receptury warsztatow Ks Real Recipes - jedna tabela swiata - zostaja) | wsi " + nv + ", wsi innych krain z miastem targowym w krainie wroconej " + _backDep + " (bylo " + dep0 + ")"
                     + ", miast z mapy ZC4 zdjetych " + nt + ", stawek wsi zdjetych " + drop.Count
                     + " | pieniadze: przelewow 0 zl, towar: 0 szt. (wozy KS-H juz w drodze dojezdzaja i rozliczaja sie jak dotad) | druga proba tylko recznie: Start Calibration off i znow on (rampa od nowa).");
            Tell(LandName(land) + (hunger ? ": villages are running out of bread under the new reckoning" : ": the granaries are running low under the new reckoning") + ", so this land returns to the old ways from tomorrow; the rest of the realm keeps the new reckoning.");
        }

        /// <summary>Zbior osad "jak dzis": wszystkie osady krain wroconych i wsie innych krain z miastem targowym (TradeBound, inaczej Bound - tylko miasto) w tym zbiorze
        /// (recenzja ks-arm: miasto krainy wroconej jest poza ZC4 i KS-H, wiec wies nie kupilaby koszyka, a zostalaby na stawkach KS bez jedzenia z niczego).</summary>
        private static void RebuildBackSet()
        {
            var set = new HashSet<Settlement>();
            int dep = 0;
            if (_back.Count > 0)
            {
                foreach (var st in Settlement.All)
                {
                    if (st == null) continue;
                    try { if (_back.ContainsKey(TownLandOf(st))) set.Add(st); } catch { }
                }
                var add = new List<Settlement>();
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsVillage || st.Village == null || set.Contains(st)) continue;
                    try { var mk = st.Village.TradeBound ?? st.Village.Bound; if (mk != null && mk.IsTown && set.Contains(mk)) add.Add(st); } catch { }
                }
                foreach (var st in add) set.Add(st);
                dep = add.Count;
            }
            _backDep = dep;
            _backSet = set;
        }

        /// <summary>Druga proba krain wroconych (projekt rozdz. 8: "tylko recznie"): Start Calibration off i znow on na zapisie zastosowanym (RampRestart - rampa calego
        /// swiata od nowa) - krainy wrocone wracaja do KS razem z rampa, strata i serie od zera.</summary>
        private static void LandRetry(int today)
        {
            if (_back.Count == 0 && _landLoss.Count == 0 && _drawStreak.Count == 0) return;
            var names = new List<string>(); foreach (var kv in _back) names.Add(kv.Key + " (od doby " + kv.Value.Day + ")");
            int n = _back.Count;
            _back.Clear(); _drawStreak.Clear(); _landLoss.Clear(); _gPrev.Clear();
            RebuildBackSet();
            Log.Info(PK + "druga proba krain doba " + today + " | Start Calibration znow wlaczony (rampa od nowa): krainy wrocone wracaja do KS " + n + (names.Count > 0 ? " (" + string.Join(", ", names.ToArray()) + ")" : "")
                     + ", strata spichlerzy i serie spadku od zera | pieniadze: przelewow 0 zl, towar: 0 szt.");
            if (n > 0) Tell("The lands that went back to the old ways try the new reckoning again, together with the rest of the realm.");
        }

        private static string LandName(string land)
        {
            try
            {
                var c = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<CultureObject>(land) : null;
                if (c != null && c.Name != null) { string n = c.Name.ToString(); if (!string.IsNullOrEmpty(n)) return n; }
            }
            catch { }
            return land ?? "?";
        }

        // ============================================================ start sesji
        /// <summary>SessionStart (po ResolvePending i ResolveLive): stan arm_ks_arm z zapisu, zbior osad krain wroconych, linia startu.</summary>
        private static void ArmSessionStart()
        {
            _loadDay = Today();
            string data = _armPending; _armPending = null;
            string note = "brak (zapis bez arm_ks_arm)";
            if (_keepRaw != null) { _armKeep = data; note = "zachowany bez zmian (zapis zastosowany bez pliku ziemi)"; }
            else if (!string.IsNullOrEmpty(data))
            {
                try { note = ImportArmData(data); } catch (Exception e) { Stumble("ImportArm", e); note = "blad odczytu - pominiety"; ArmStateClear(); }
            }
            RebuildBackSet();
            var s = Settings.Current;
            Log.Info(PK + "samo uzbrojenie (ks-arm) start sesji | klucz Start Calibration Auto Arm " + (AutoArmKey ? "TAK" : "NIE") + ", droga " + (AutoPathOn() ? (AutotestMode() ? "autotest (" + (AutoTestKey ? "klucz Start Calibration Auto Arm Autotest" : "plik " + AutoFlagName) + ")" : "gra") : (AutoArmKey ? "nieczynna (autotest bez pliku " + AutoFlagName + " i bez klucza Start Calibration Auto Arm Autotest)" : "nieczynna"))
                     + " | stan " + StateText() + (_autoArm ? " (samo)" : "") + " | arm_ks_arm: " + note
                     + " | srednia ksiegi towarow " + _emaDays + " dob" + (_emaRestored ? " (z zapisu; pierwsza doba sesji pominieta)" : "") + ", dojrzaly Plan od doby " + _matureSince + ", pelnych dob bramek " + _gateRing.Count
                     + " | bramka krainy " + (LandGateKey ? "TAK" : (AutotestMode() ? "NIE (autotest)" : "NIE - w kampanii po wlaczeniu i tak czynna (bez wylacznika poza calym KS)"))
                     + " (prog straty " + LossDaysKey + " d, zapasu " + ReserveDaysKey + " d), wrocone krainy " + _back.Count + " (osad " + _backSet.Count + ", w tym wsi innych krain z miastem targowym w nich " + _backDep + ")"
                     + ", strata spichlerzy pod KS: krain " + _landLoss.Count
                     + " | twarde bramki " + (HardGatesKey ? "TAK" : "NIE") + " | ryby z perku Night Raider " + (NoPerkFishKey ? (_perkPatched ? "przed wlaczeniem nazwane w ksiedze, po wlaczeniu zablokowane" : "BRAK latki") : "jak dotad (klucz NIE)")
                     + " | zboze patroli miast " + (PatrolKey ? (_patrolPatched ? "przed wlaczeniem nazwane w ksiedze, po wlaczeniu ze spichlerza miasta" : "BRAK latki") : "jak dotad (klucz NIE)")
                     + " | ryby portow BK miast " + (BkPortsKey ? (_portPatched ? "nazwane w ksiedze" : "BRAK latki") : "jak dotad (klucz NIE)") + ".");
        }

        private static void ArmStateClear()
        {
            _autoArm = false; _matureSince = -1; _back.Clear(); _drawStreak.Clear(); _gateRing.Clear(); _landLoss.Clear();
        }

        // ============================================================ zapis arm_ks_arm
        internal static void ImportArm(string data) { _armPending = string.IsNullOrEmpty(data) ? null : data; }

        internal static string ExportArm()
        {
            try
            {
                if (_keepRaw != null) return _armKeep ?? "";
                bool ema = AutoArmKey && KeyOn && _emaDays > 0;
                if (!ema && !_autoArm && _matureSince < 0 && _back.Count == 0 && _drawStreak.Count == 0 && _gateRing.Count == 0 && _landLoss.Count == 0) return "";
                var sb = new StringBuilder(16 * 1024);
                sb.Append("V=").Append(ArmVersion).Append('\n');
                sb.Append("C=").Append(Clean(CampaignId())).Append('\n');
                sb.Append("A=").Append(_autoArm ? 1 : 0).Append('|').Append(_matureSince).Append('\n');
                if (_gateRing.Count > 0)
                {
                    sb.Append("G=");
                    bool fd = true;
                    foreach (var kv in _gateRing)
                    {
                        if (!fd) sb.Append(';'); fd = false;
                        sb.Append(kv.Key);
                        foreach (var it in kv.Value) sb.Append(',').Append(Clean(it.Key)).Append(':').Append(it.Value[0]).Append(':').Append(it.Value[1]);
                    }
                    sb.Append('\n');
                }
                if (_back.Count > 0)
                {
                    sb.Append("B=");
                    bool f = true;
                    foreach (var kv in _back) { if (!f) sb.Append(';'); f = false; sb.Append(Clean(kv.Key)).Append(':').Append(kv.Value.Day).Append(':').Append(Clean(kv.Value.Why ?? "-").Replace(':', ' ')); }
                    sb.Append('\n');
                }
                if (_drawStreak.Count > 0)
                {
                    sb.Append("S=");
                    bool f = true;
                    foreach (var kv in _drawStreak) { if (!f) sb.Append(';'); f = false; sb.Append(Clean(kv.Key)).Append(':').Append(kv.Value); }
                    sb.Append('\n');
                }
                if (_landLoss.Count > 0)
                {
                    // recenzja ks-arm: strata spichlerzy krain pod KS od wlaczenia (dni potrzeby, netto) - bez niej krotkie sesje Jeffa nie doszlyby do progu straty
                    sb.Append("L=");
                    bool f = true;
                    foreach (var kv in _landLoss) { if (!f) sb.Append(';'); f = false; sb.Append(Clean(kv.Key)).Append(':').Append(kv.Value.ToString("0.###", Inv)); }
                    sb.Append('\n');
                }
                int head = sb.Length;
                if (ema)
                {
                    sb.Append("E=").Append(_emaDays).Append('|').Append(_emaLastDay).Append('|');
                    bool f = true;
                    foreach (var kv in _ema)
                    {
                        var a = kv.Value; if (a == null) continue;
                        bool any = false; for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i]) > 0.0005) { any = true; break; }
                        if (!any) continue;
                        if (!f) sb.Append(';'); f = false;
                        sb.Append(Clean(kv.Key)).Append(':');
                        for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(','); sb.Append(a[i].ToString("0.###", Inv)); }
                    }
                    sb.Append('\n');
                }
                string res = Pack(sb.ToString());
                bool cut = false;
                if (res.Length > ArmSaveMax && ema) { sb.Length = head; res = Pack(sb.ToString()); cut = true; }
                if (res.Length > ArmSaveMax) { res = ""; cut = true; }
                int today = Today();
                if (today != _armExportLogged)
                {
                    _armExportLogged = today;
                    Log.Info(PK + "zapis arm_ks_arm doba " + today + " | " + (res.Length / 1024.0).ToString("0.0", Inv) + " KB (limit " + (ArmSaveMax / 1024) + " KB " + (cut ? "PRZEKROCZONY - " + (res.Length > 0 ? "bez sredniej ksiegi" : "zapis pominiety") : "TAK") + ")"
                             + " | srednia ksiegi towarow " + (ema && !cut ? _emaDays + " dob, towarow " + _ema.Count : "nie zapisana") + ", pelnych dob bramek " + _gateRing.Count + ", krainy wrocone " + _back.Count + ", serie spadku " + _drawStreak.Count
                             + ", strata spichlerzy krain " + _landLoss.Count + ".");
                }
                return res;
            }
            catch (Exception e) { Stumble("ExportArm", e); return ""; }
        }

        /// <summary>Odczyt arm_ks_arm (po ResolvePending): id kampanii, samo uzbrojenie, bramki, krainy wrocone (tylko stan zastosowany), srednia ksiegi (tylko przy kluczach).</summary>
        private static string ImportArmData(string packed)
        {
            string s;
            try { s = Unpack(packed); } catch (Exception e) { Stumble("ImportArm(rozpakowanie)", e); return "blad rozpakowania - pominiety"; }
            if (string.IsNullOrEmpty(s)) return "pusty";
            var map = new Dictionary<char, string>();
            foreach (var line in s.Split('\n')) if (line.Length >= 2 && line[1] == '=') map[line[0]] = line.Substring(2);
            string v;
            if (!map.TryGetValue('V', out v) || v != ArmVersion) return "nieznana wersja - pominiety";
            string gid = CampaignId(), cid;
            if (map.TryGetValue('C', out cid) && cid != "-" && cid.Length > 0 && gid.Length > 0 && cid != Clean(gid)) return "inna kampania (id " + cid + ") - pominiety";
            if (_campaign.Length > 0 && gid.Length > 0 && _campaign != gid) return "stan KS innej kampanii - pominiety";
            var notes = new List<string>();
            if (map.TryGetValue('A', out v))
            {
                var p = v.Split('|'); int a0, a1;
                if (p.Length >= 2 && int.TryParse(p[0], out a0) && int.TryParse(p[1], out a1))
                {
                    _autoArm = a0 == 1 && _state == StateArmed;
                    _matureSince = _state == StateArmed ? a1 : -1;
                }
            }
            if (map.TryGetValue('G', out v) && _state != StateApplied)
            {
                foreach (var dayPart in v.Split(';'))
                {
                    var q = dayPart.Split(','); int d;
                    if (q.Length == 0 || !int.TryParse(q[0], out d)) continue;
                    var dd = new Dictionary<string, long[]>(StringComparer.Ordinal);
                    for (int i = 1; i < q.Length; i++)
                    {
                        var e = q[i].Split(':'); long x, y;
                        if (e.Length == 3 && long.TryParse(e[1], out x) && long.TryParse(e[2], out y)) dd[e[0]] = new[] { Math.Max(0, x), Math.Max(0, y) };
                    }
                    _gateRing[d] = dd;
                }
                while (_gateRing.Count > GateDays) { int first = -1; foreach (var k in _gateRing.Keys) { first = k; break; } _gateRing.Remove(first); }
                notes.Add("bramki " + _gateRing.Count + " dob");
            }
            if (map.TryGetValue('B', out v) && _state == StateApplied)
            {
                foreach (var e in v.Split(';'))
                {
                    var q = e.Split(':'); int d;
                    if (q.Length >= 2 && q[0].Length > 0 && int.TryParse(q[1], out d)) _back[q[0]] = new LandBack { Day = d, Why = q.Length >= 3 ? q[2] : "-" };
                }
                notes.Add("krainy wrocone " + _back.Count);
            }
            if (map.TryGetValue('S', out v) && _state == StateApplied)
            {
                foreach (var e in v.Split(';'))
                {
                    var q = e.Split(':'); int n;
                    if (q.Length == 2 && int.TryParse(q[1], out n) && n > 0) _drawStreak[q[0]] = Math.Min(n, LandDrawDays);
                }
            }
            if (map.TryGetValue('L', out v) && _state == StateApplied)
            {
                foreach (var e in v.Split(';'))
                {
                    var q = e.Split(':'); double x;
                    if (q.Length == 2 && q[0].Length > 0 && double.TryParse(q[1], NumberStyles.Float, Inv, out x) && x > 0 && !double.IsInfinity(x)) _landLoss[q[0]] = Math.Min(x, 1000.0);
                }
                notes.Add("strata spichlerzy krain " + _landLoss.Count);
            }
            if (map.TryGetValue('E', out v))
            {
                if (!AutoArmKey || !KeyOn) notes.Add("srednia ksiegi pominieta (klucze)");
                else if (_emaDays > 0) notes.Add("srednia ksiegi pominieta (juz liczona w sesji)");
                else
                {
                    var p = v.Split(new[] { '|' }, 3); int nd, last;
                    if (p.Length == 3 && int.TryParse(p[0], out nd) && int.TryParse(p[1], out last) && nd > 0 && last <= Today())
                    {
                        int kn = GoodsLedger.KsN;
                        foreach (var e in p[2].Split(';'))
                        {
                            int c = e.IndexOf(':'); if (c <= 0) continue;
                            var xs = e.Substring(c + 1).Split(',');
                            var a = new double[kn];
                            for (int i = 0; i < kn && i < xs.Length; i++) { double x; if (double.TryParse(xs[i], NumberStyles.Float, Inv, out x) && !double.IsNaN(x) && !double.IsInfinity(x)) a[i] = x; }
                            _ema[e.Substring(0, c)] = a;
                        }
                        _emaDays = nd;
                        _emaLastDay = Math.Max(last, Today());      // pierwsza doba sesji (od wczytania) - niepelna, pominieta
                        _emaRestored = true;
                        notes.Add("srednia ksiegi " + nd + " dob, towarow " + _ema.Count);
                    }
                    else notes.Add("srednia ksiegi - zly format, pominieta");
                }
            }
            return "wczytany (" + (notes.Count > 0 ? string.Join(", ", notes.ToArray()) : "naglowek") + ")";
        }
    }
}
