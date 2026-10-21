using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Armoury.Villages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// KSIEGA WIOSEK POBOCZNYCH, PLON OKREGU Z PRACY WIOSEK, ZYWNOSC WAROWNI OD LUDZI (E4/11 = paczka 193, PLAN 4.5, 4.6; plan docs/etap4/PLAN-KODU-E411.md po krytyce;
    /// projekt etapu 4 E4/11; WM = docs/PROJEKT-WIOSKI-NA-MAPIE-2026-10-07.md rozdz. 3, 6; decyzje Jeffa SP:425-433, SP:760, SP:1137).
    /// Kazdy obrazek z arm_map_villages.tsv to WIOSKA Z NAZWA (gromada osad okregu wsi gry). Dotad byla tylko obrazkiem: dymek pokazywal "osady x 250" na sztywno,
    /// a plon i spalenie dotyczyly calej wsi gry. Odtad:
    ///  - okreg (wies gry + jej wioski z pliku) ma n = max(1, round(ludzie / DistrictSettlementPeople)) osad, zamrozone w zapisie przy pierwszym odczycie; wioska ma tyle
    ///    osad, ile w kolumnie settlements, wies gry reszte, ale nie mniej niz najwieksza wioska (okreg za maly - wszystkie pomniejszone w tej samej proporcji, WM 3);
    ///  - ksiega trzyma tylko PUSTE OSADY wiosek (dwie pule: rabunek i zerowanie b_r, glod b_f) - ludzi nikogo nie przesuwa: w domu wioski = DOM okregu x (osady - puste)
    ///    / (n - puste), poza domem = konto UCHODZCY rozdzielone wedlug pustych osad (suma po wioskach = ksiega ludzi z definicji, kontrola w linii);
    ///  - kazdy Devastation.Strike (rabunek, zerowanie armii) zamienia trafionych na spalone osady: trafieni x n / ludzie przed ruchem, lancuchem palenia - pierwsza wioska
    ///    rabunku od strony wroga (najblizsza warownia frakcji napastnika), dalej najblizsze, wies gry na koncu; zerowanie od obozu; kazda do pulapu DevastationMaxPercent;
    ///  - glod (Famine.TryFlee) oproznia najpierw ubocze, potem linie, potem wezly (od najdalszej), wies gry na koncu;
    ///  - puste osady maleja tylko w dobie, proporcjonalnie do ubytku swojej czesci konta (powrot, smierc, podloga 10), wagi odbudowy wezel 1.5 / linia 1.0 / ubocze 0.5 /
    ///    wies gry 1.5; konto 0 = wszystkie puste osady 0 - a E4/5 sprowadza wszystkich zywych najpozniej rok po ostatnim najezdzie w regionie, wiec spalona wioska
    ///    wstaje najpozniej w rok (decyzje Jeffa 09.10 i 11.10), bez osobnego zegara;
    ///  - stany wioski: stoi, czesciowo pusta, spalona (b_r >= 50% osad - decyzja Jeffa), opuszczona (glod), w odbudowie, odbudowana (zdarzenie), plonie (nakladka);
    ///  - plon okregu = suma pracy wiosek liczy juz czynnik E4/5 (x czynnik rak E4/7) - ksiega tylko przypisuje go wioskom (menu, dymek) - bez drugiego mnoznika;
    ///  - ZYWNOSC WAROWNI (jedyna nowa latka ekonomii): skladnik kazdej wsi w DefaultSettlementFoodModel = (poziom + 1) x 6 tylko w stanie Normal - odtad x czynnik rak
    ///    (E4/5 x E4/7), a wies spladrowana przy LootedProducesByPeople zywi warownie ludzmi, ktorzy zostali (zamiast 0); bez zimy (jedno zrodlo: WinterBite).
    /// Stan: klucz zapisu "arm_district" (SaveText.Sync). Widok (MapVillagesView) i menu ("People of this land") tylko czytaja ksiege. Wyjatki per okreg liczone
    /// (potkniecia, pierwszy blad miejsca do logu) - bez globalnych wylacznikow (CLAUDE.md 7). Wylaczniki: VillageBookEnabled, TownFoodByVillagePeople.
    /// Poza paczka (etap 7): wyglad spalonej wioski, komunikaty, napisy nad spalonymi, rabunek osada po osadzie (7.11 wola to samo Burn).
    /// </summary>
    internal static class VillageBook
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double BurnShare = 0.50;        // spalona / opuszczona: polowa osad wioski (decyzja Jeffa, WM 10)
        private const double KeepShare = 0.47;        // histereza 3%: stan spalona / opuszczona trzyma sie do 47%
        private const double StandShare = 0.01;       // ponizej 1% pustych osad wioska stoi (zdarzenie "odbudowana")
        private const double WHub = 1.5, WLine = 1.0, WField = 0.5, WMain = 1.5;   // wagi odbudowy (WM 6.4)
        private const double DirWeight = 0.3;         // waga kierunku wroga przy wyborze pierwszej wioski rabunku (WM 6.2)
        private const double Eps = 1e-9;
        private const int MaxLines = 12;              // menu: najwyzej tyle wierszy wiosek (stojace ponad limit - jedna linia)
        private const int TopEvents = 8;
        private const string CsvName = "wioski-okregi.csv";
        private const string CsvHeader = "dzien;okreg_id;okreg;region_id;region;gracz;n;n_wiosek;wiosek;dom;konto_r;konto_f;puste_r;puste_f;plonie;spalone;opuszczone;odbudowa;czesciowo;"
                                         + "plon_okregu;ostatni_najazd;dni_od_najazdu;oblezona;rok;wynik_rok";
        private const byte FBurned = 1, FAbandoned = 2;
        internal const int SStanding = 0, SDamaged = 1, SBurned = 2, SAbandoned = 3, SRebuilding = 4, SBurning = 5;
        private const int ZNone = 0, ZOld = 1, ZLayout = 2, ZEnable = 3;      // powod przydzialu zastepczego (3.7)
        private static readonly string[] ZName = { "-", "stary zapis", "inny uklad", "wlaczenie" };
        private static readonly TextObject _txtHands = new TextObject("{=!}Village fields: people away or back at work");

        // ------------------------------------------------------------ pamiec
        private sealed class Dist
        {
            public string Id, Name = "?"; public Village V; public Settlement St;
            public int W;                                   // wioski poboczne; indeks W = wies gry
            public double N; public bool NSet, Shrunk;
            public double[] F, Nw;                          // osady z pliku (wies gry 0) i po podziale n
            public string[] Uid, VName, Kind; public char[] Cls; public float[] X, Y, Rd; public int[] Order;
            public bool[] Fish;                             // E6/W2 (210): wioska rybacka (kolumna model = fishing; wies gry false)
            public int[] FamOrder;                          // kolejnosc opuszczania z glodu: ubocze, linia, wezel (od najdalszej), wies gry na koncu
            public double[] Br, Bf; public byte[] Flag; public int[] EvDay, RebuiltDay;
            public double ARexp, AFexp;                     // konto (czesc z rabunku, czesc z glodu) z ostatniego uzgodnienia + doplyw od niego
            public Sess S; public int SmokeIdx = -1;        // trwajacy rabunek i dym w czujnosci (bez zapisu)
            public int Z;                                   // przydzial zastepczy czeka (powod)
            public bool Event;                              // zdarzenie dzis (wiersz CSV)
            public Mem[] M;                                 // E7/W1 (232): pamiec wojny na wioske (indeks jak Br; W = wies gry) - sekcja m: zapisu
            public bool LookDirty;                          // E7/W4 (237): okreg czeka na odczyt wygladu przez widok mapy (bez zapisu)
            public Pend P;                                  // Jeff 16.10 (D5): puste osady z zapisu innego ukladu do rozlozenia na wioski tego ukladu (PlacePending)
        }

        /// <summary>Jeff 16.10 (D5, uklad v5 na zapisie v4): puste osady okregu z zapisu innego ukladu - sumy niezalezne od indeksow wiosek: rabunek i glod osobno
        /// w wioskach (S) i we wsi gry (M), doba ostatniego zdarzenia z flag zapisu (Ev; -1 brak). Rozkladane przy starcie sesji zaraz po McmSettings.Apply (PlacePendingAll;
        /// zapas: pierwsza doba albo zapis) z biezacym pulapem - wies gry swoja czesc, wioski wedlug kolejnosci (glod - FamOrder), reszta do wsi gry.
        /// Po recenzji D5: uklad zapisu znany (OldLayout) - Map: Kr/Kf = puste osady wiosek, ktore zostaly, i wsi gry na NOWYM indeksie (po uid), Pool = wioski usuniete
        /// (polozenie, puste osady, flaga, pamiec) - do najblizszych wiosek z miejscem.</summary>
        private sealed class Pend
        {
            public double Rs, Rm, Fs, Fm; public int Ev = -1; public bool NoAcc;   // NoAcc: zapis bez y: okregu - konto oczekiwane = biezace (bez drugiego przydzialu)
            public bool Map; public double[] Kr, Kf; public List<PoolItem> Pool;
        }

        /// <summary>D5 po recenzji: puste osady do rozlozenia od punktu (X, Y) - wioska usunieta z ukladu zapisu (z flaga, doba zdarzenia i pamiecia wojny) albo nadmiar
        /// wioski, ktora zostala, ponad biezacy pulap (From = jej indeks, bez flagi i pamieci).</summary>
        private sealed class PoolItem { public float X, Y; public double R, F; public byte Flag; public int Ev = -1, Rb = -1, From = -1; public Mem M; }

        // E7/W1: C - napastnik sesji (identyfikatory do pamieci i komunikatu), Burned - ta sesja przeszla jakas wioske na spalona (przy Looted bez "plundered")
        // m97-wioski: Fid - frakcja napastnika (StringId; wioski chronione okupem przed nia lancuch omija - SideVillageVisit.Spared)
        private sealed class Sess { public object Key; public int Cur = -1; public string Raider = "?"; public float Fx, Fy; public bool HasFrom; public int Day; public Cx C; public bool Burned; public string Fid; public int Men; public HashSet<string> DefHit; }

        /// <summary>
        /// E7/W1 (232, R14): pamiec wojny wioski - ostatnie zdarzenie (rabunek albo zerowanie armii): rodzaj ('B' - przeszla na spalona w ksiedze w tym zdarzeniu,
        /// 'P' - splondrowana bez przejscia), doba ostatniego uderzenia, StringId bohatera-wodza i rodu (nigdy wyswietlana nazwa - W1-zapis), ilu ucieklo i zginelo
        /// (z uderzen Devastation.Strike na osady tej wioski), licznik spalen (tylko przy przejsciu na spalona). Tag - zdarzenie w toku (bez zapisu).
        /// </summary>
        private sealed class Mem { public char Kind = 'P'; public int Day = -1, Burns; public string Hero = "", Clan = ""; public double Fled, Dead; public object Tag; }

        /// <summary>E7/W1: napastnik jednego uderzenia (sesja rabunku albo oboz armii) - identyfikatory, nazwa partii do komunikatu, ludzi na osade i udzial zabitych.</summary>
        private sealed class Cx { public string Hero = "", Clan = "", Who = "?"; public bool Player; public object Tag; public Sess S; public double Pps, DeadFrac; }

        /// <summary>E7/W1: przejscie wioski na spalona w ksiedze (CheckUp przy uderzeniu) - kolejka pod Gate, oproznia VillageNews w ticku godzinnym (nie z wnetrza Strike).</summary>
        internal sealed class BurnNews { public Village V; public string VName, DName, Hero, Who; public bool Player; public int Day; }

        /// <summary>E7/W1: pamiec wojny wioski dla dymka (W2) - same identyfikatory, nazwa na zywo u wolajacego.</summary>
        internal struct WarMemory { public bool Burned; public int Day, Burns; public string Hero, Clan; public double Fled, Dead; }

        // m97-wioski krok 2: Lk - brak rak wiosek tego okregu po bitwie z obroncami (SideVillageVisit.LackNow, null - brak), LkT - suma Lk[j] / (Dom - udzial j)
        private struct Snap { public double Dom, A, Ar, Af, B, Sbr, Sbf, Y; public double[] Lk; public double LkT; }

        /// <summary>Stan wioski dla widoku i etapu 7 (napisy nad spalonymi, komunikaty).</summary>
        internal struct VState
        {
            public int State; public double Home, Away, Settlements, Empty, Share; public int EvDay, RebuiltDay, Today; public string Raider;
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Dist> _d = new Dictionary<string, Dist>(StringComparer.Ordinal);
        private static readonly Dictionary<string, KeyValuePair<Dist, int>> _byUid = new Dictionary<string, KeyValuePair<Dist, int>>(StringComparer.Ordinal);
        // E4 fixB (G4): okreg po OBIEKCIE Village dla wpiec Strike / glod / pojmani (menu warowni - nie, recenzja fixB (3)). ROT-Map settlements.xml daje dwie osady na jednym
        // Village (Kurbok ROT_town40_village2 i Thupak ROT_town40_village3 maja komponent ROT_town39_village2/3_comp - ten sam co Guntak i Volug): v.Settlement to
        // ostatni wlasciciel z XML (town40), a okreg (Resolve, pierwszy w Settlement.All) ma klucz town39 - szukanie po v.Settlement.StringId chybialo (d120: d0 = 26).
        // Klucze zapisu (d.Id) i przypisanie wierszy pliku bez zmian. Porownanie po referencji (hash MBObjectBase idzie z Id - jak RefEq w GoodsLedger).
        private static readonly Dictionary<Village, Dist> _byV = new Dictionary<Village, Dist>(new VillageRefEq());
        private sealed class VillageRefEq : IEqualityComparer<Village>
        {
            public bool Equals(Village a, Village b) { return ReferenceEquals(a, b); }
            public int GetHashCode(Village o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }
        private static readonly List<string> _distNoVillageIds = new List<string>();     // E4 fixB (G2, tylko log): id okregow pliku bez wsi gry (linia pierwszej doby)
        private static string _pending;                     // napis z zapisu do Resolve
        private static bool _resolved, _resolveTried, _firstDay = true, _offSeen, _offLogged, _pSet;
        private static int _p = 250;                        // ludzi na osade przy n (z zapisu albo z ustawien przy pierwszym n)
        private static string _resolveNote = "";
        private static double _resolveMs;
        private static int _rowsNoVillage, _distNoVillage, _savedN, _savedB, _savedBad;
        private static bool _layoutSame;
        private static uint _saveCrc; private static bool _saveCrcSet;   // recenzja calosci scalenia6: "L:" zapisu (uklad wiosek przy zapisie) - VillageCrafts.LayoutCheck
        // Jeff 16.10 (D5): przeniesienie pustych osad z zapisu innego ukladu - liczniki do linii pierwszej doby (raz na sesje)
        private static int _pendDists, _pendBad, _pendMemSkipped;
        private static double _pendR0, _pendF0, _pendR, _pendF, _pendDrop;
        // D5 po recenzji: uklad zapisu rozpoznany (plik _pendMapFile) - przeniesienie po uid: wioski, ktore zostaly, z pustymi osadami (_mapKept, z tego stan spalonej /
        // opuszczonej zachowany udzialem _mapHeld - dolozone osady _pendAddR/_pendAddF, wliczone w _pendR0/_pendF0), usuniete z pustymi osadami (_mapGone), flagi
        // (_mapFlags; usunietych do najblizszej _mapFlagsMoved), pamiec wojny (_mapMem; usunietych do najblizszej _mapMemMoved, utracona przy zajetej _mapMemLost)
        private static string _pendMapFile, _pendWhere = "-";
        private static int _mapKept, _mapHeld, _mapGone, _mapFlags, _mapFlagsMoved, _mapMem, _mapMemMoved, _mapMemLost;
        private static double _pendAddR, _pendAddF;
        private static readonly List<double> _newN = new List<double>();
        private static readonly List<string> _newNames = new List<string>();
        private static string _csvPath;

        // uklad z pliku (raz na proces - plik w grze sie nie zmienia; Jeff 16.10: raz na plik - VillageLayoutV5 wybiera v5 albo v4 przy starcie sesji, MapVillageData.LayoutFileName)
        private static MapVillageFile _file;
        private static string _fileNote = "";
        private static uint _layoutCrc;
        private static double _loadMs;
        private static bool _fileTried;
        private static string _fileName;                    // nazwa wczytanego pliku ukladu (inny wybor w nowej sesji - wczytanie od nowa)

        // liczniki doby (bez zapisu; zeruje Daily)
        private static double _dRaidW, _dRaidMain, _dForW, _dForMain, _dFam, _dUp, _dZast, _dOver, _dOverPeople, _dSetS, _dHitSkip;
        private static int _dStrikes, _dD0, _dSess, _dFamSkip, _dLate;       // _dLate - konto znane umieszczone po zwolnieniu pulapu (R1-4)
        // drobne2 (G5): Devastation.HitAll przy ostatnim zerowaniu licznikow doby tej ksiegi - trafieni do r3 licza sie od tej chwili (to samo okno co _dSetS)
        private static double _hitMark;
        private static readonly int[] _dZastWhy = new int[4];
        private static readonly List<string> _dBurnedList = new List<string>(), _dRebuiltList = new List<string>();
        private static int _dBurnedN, _dRebuiltN, _dAbandonedN;
        private static int _stumbles, _stumblesAll;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static bool _townPatched;
        // E7/W1 (232): kolejka przejsc na spalona (pod Gate) i liczniki dla linii "Wioski - wiesci (E7/W1)" (zeruje TakeNewsCounts, nie NewDay - linia idzie po Daily ksiegi)
        private const int NewsMax = 64;
        private static readonly List<BurnNews> _news = new List<BurnNews>();
        private static int _nBurned, _nNewsLost, _nMemNew, _savedM;
        // E7/W4 (237, R10/R17): zmiany stanu ksiegi dla widoku mapy - okregi, w ktorych zmienily sie puste osady albo flagi (LookTouch w CheckUp = kazdy wzrost, Remove =
        // kazdy spadek, zdarzenie "odbudowana", pierwsze n okregu), albo wszystko (Reset, Resolve). Widok czyta numer zmiany bez blokady (4 razy na sekunde), liste zabiera
        // tylko przy nowym numerze. Wlasna blokada (kolejnosc Gate -> _lookLock, TakeLookDirty bierze sama _lookLock). Bez zapisu - widok po wczytaniu czyta wszystko od nowa.
        private static readonly object _lookLock = new object();
        private static readonly List<Dist> _lookDirty = new List<Dist>();
        private static bool _lookAll = true;
        private static int _lookVer;

        /// <summary>Ksiega czynna: klucz VillageBookEnabled i okregi rozwiazane w tej sesji (bez pliku ukladu - kazdy okreg to sama wies gry).</summary>
        internal static bool Active { get { var s = Settings.Current; return s != null && s.VillageBookEnabled && _resolved; } }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        private static void Stumble(string where, Exception e)
        {
            System.Threading.Interlocked.Increment(ref _stumbles);           // liczymy potkniecia, niczego nie gasimy (postfiks warowni moze isc z watkow AI)
            System.Threading.Interlocked.Increment(ref _stumblesAll);
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first) Log.Error(where, e);
        }

        private static bool BadId(string id)
        {
            return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf(';') >= 0 || id.IndexOf(',') >= 0 || id.IndexOf('=') >= 0 || id.IndexOf(':') >= 0 || id.IndexOf('/') >= 0;
        }

        private static string NameOf(Settlement st)
        {
            if (st == null) return "?";
            try { var n = st.Name; if (n != null) return n.ToString(); } catch { }
            return st.StringId ?? "?";
        }

        private static string F(double v, string fmt) { return v.ToString(fmt, Inv); }
        private static string F0(double v) { return Math.Round(v).ToString("0", Inv); }
        private static string N0(double v) { return Math.Max(0.0, Math.Round(v)).ToString("N0", Inv); }
        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' '); }
        private static double Sum(double[] a) { double s = 0.0; for (int i = 0; i < a.Length; i++) s += a[i]; return s; }

        // ------------------------------------------------------------ cykl zycia
        /// <summary>Konstruktor ArmouryBehavior: stan jednej kampanii nie przecieka do drugiej.</summary>
        internal static void Reset()
        {
            MapVillageData.ResetChoice();                   // Jeff 16.10: uklad v5 / v4 wybierany od nowa przy pierwszym odczycie tej kampanii (VillageLayoutV5; sw-przysiolki: v6 - VillageLayoutV6)
            VillageHamlets.Reset();                         // sw-przysiolki: stan sesji przysiolkow (linia startu, liczniki; nakladka ziemi KS zdejmowana przy nastepnym starcie KS)
            lock (Gate) { _d.Clear(); _byUid.Clear(); _byV.Clear(); _news.Clear(); _nBurned = _nNewsLost = _nMemNew = _savedM = 0; }
            LookTouchAll();                                 // E7/W4: stare okregi puszczone, widok czyta wszystko od nowa
            _pending = null; _resolved = _resolveTried = false; _firstDay = true; _offSeen = _offLogged = false; _pSet = false; _p = 250;
            _resolveNote = ""; _resolveMs = 0.0; _rowsNoVillage = _distNoVillage = _savedN = _savedB = _savedBad = 0; _layoutSame = false; _saveCrc = 0; _saveCrcSet = false; _distNoVillageIds.Clear();
            _pendDists = _pendBad = _pendMemSkipped = 0; _pendR0 = _pendF0 = _pendR = _pendF = _pendDrop = 0.0;
            _pendMapFile = null; _pendWhere = "-"; _mapKept = _mapHeld = _mapGone = _mapFlags = _mapFlagsMoved = _mapMem = _mapMemMoved = _mapMemLost = 0; _pendAddR = _pendAddF = 0.0;
            _newN.Clear(); _newNames.Clear(); _csvPath = null;
            _stumblesAll = 0;
            lock (_errSites) _errSites.Clear();
            NewDay();
        }

        private static void NewDay()
        {
            _dRaidW = _dRaidMain = _dForW = _dForMain = _dFam = _dUp = _dZast = _dOver = _dOverPeople = _dSetS = _dHitSkip = 0.0;
            _dStrikes = _dD0 = _dSess = _dFamSkip = _dLate = 0;
            _hitMark = Devastation.HitAll;
            Array.Clear(_dZastWhy, 0, _dZastWhy.Length);
            _dBurnedList.Clear(); _dRebuiltList.Clear();
            _dBurnedN = _dRebuiltN = _dAbandonedN = 0;
            _stumbles = 0;
        }

        /// <summary>SyncData (wczytanie): napis odlozony do Resolve (w SyncData osad jeszcze nie ma w pelni).</summary>
        internal static void Import(string data) { _pending = data; }

        // ------------------------------------------------------------ plik ukladu (ten sam parser co widok W2)
        private static void LoadFile()
        {
            string want = MapVillageData.LayoutFileName;
            if (_fileTried && string.Equals(_fileName, want, StringComparison.Ordinal)) return;
            _fileTried = true;
            _fileName = want;
            _file = null; _fileNote = ""; _layoutCrc = 0;     // inny plik niz w poprzedniej sesji procesu - nic ze starego ukladu nie zostaje
            var sw = Stopwatch.StartNew();
            try
            {
                string path = MapVillageData.DataPath();
                if (path == null || !System.IO.File.Exists(path)) { _fileNote = "brak pliku " + (path ?? MapVillageData.LayoutFileName); return; }
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                var f = MapVillageData.Parse(Encoding.UTF8.GetString(bytes));
                f.FileCrc32 = MapVillageData.Crc32(bytes);
                if (f.MissingColumns.Length > 0) { _fileNote = "BRAK kolumn w naglowku: " + f.MissingColumns; return; }
                // CRC sceny jak widok (Campaign laduje scene mapy przed OnSessionStart - LoadVisualsThirdState): plik z innej sceny - wioski pominiete, okregi same
                try
                {
                    var ms = Campaign.Current != null ? Campaign.Current.MapSceneWrapper : null;
                    if (ms != null)
                    {
                        uint scene = ms.GetSceneXmlCrc();
                        if (MapVillageData.CheckScene(f, scene) == MapVillageData.CheckMismatch) { _fileNote = "plik z innej sceny (crc pliku " + f.SceneCrc + ", scena " + scene + ")"; return; }
                    }
                }
                catch (Exception e) { Stumble("VillageBook.LoadFile(scena)", e); }
                _layoutCrc = LayoutCrcOf(f);
                _file = f;
            }
            catch (Exception e) { _file = null; _fileNote = "blad wczytania (" + e.GetType().Name + ")"; Stumble("VillageBook.LoadFile", e); }
            finally { _loadMs = sw.Elapsed.TotalMilliseconds; }
        }

        /// <summary>Crc ukladu z uid i osad wierszy w kolejnosci pliku ("uid:osady;" - ten sam napis co "L:" zapisu i naglowek layout_crc lore) - niezalezne od CRLF/LF,
        /// komentarzy i kolumn samego widoku.</summary>
        private static uint LayoutCrcOf(MapVillageFile f) { return LayoutCrcOf(f.Records); }

        /// <summary>sw-przysiolki (recenzja): to samo dla listy wierszy (uklad v5 z kolumn v5_* pliku v6 - MapVillageData.V5Of).</summary>
        private static uint LayoutCrcOf(List<MapVillageRecord> recs)
        {
            var sb = new StringBuilder(recs.Count * 40);
            foreach (var r in recs) sb.Append(r.Uid).Append(':').Append(r.Settlements.ToString(Inv)).Append(';');
            return MapVillageData.Crc32(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        /// <summary>Jeff 16.10 (D5, po recenzji): okreg ukladu zapisu - uid wiosek w kolejnosci indeksow zapisu (ByDistrict: order, potem id - jak Build przy zapisie),
        /// polozenie i osady z pliku (stary podzial n okregu).</summary>
        private sealed class OldDist { public string[] Uid; public float[] X, Y; public double[] F; }

        /// <summary>
        /// Jeff 16.10 (D5, po recenzji): uklad zapisu rozpoznany po crc - drugi plik ukladu modulu (arm_map_villages_v4.tsv przy v5, arm_map_villages.tsv przy v4) ma ten sam
        /// crc "uid:osady;" co "L:" zapisu. Wtedy stary indeks wioski -> uid jest znany dokladnie i stan wiosek przechodzi po uid (ApplyMapped). null - zaden plik nie pasuje
        /// (zostaje przeniesienie sum okregow, PendingFromSave). Pod Gate (ApplyPending), raz na wczytanie, nie rzuca.
        /// </summary>
        private static Dictionary<string, OldDist> OldLayout(uint crc, out string name)
        {
            name = null;
            // sw-przysiolki: zapis v5 na v6 (i odwrotnie) po uid; f10-uklad: zapis v6 (Jeff) / v5 / v4 na v7 po uid - plik _v6
            foreach (var cand in new[] { MapVillageData.FileNameV4, MapVillageData.FileNameV5, MapVillageData.FileNameV6, MapVillageData.FileName })
            {
                if (string.Equals(cand, _fileName, StringComparison.Ordinal)) continue;
                try
                {
                    string path = MapVillageData.DataPathFor(cand);
                    if (path == null || !System.IO.File.Exists(path)) continue;
                    var f = MapVillageData.Parse(Encoding.UTF8.GetString(System.IO.File.ReadAllBytes(path)));
                    if (f.MissingColumns.Length > 0 || f.Records.Count == 0 || LayoutCrcOf(f) != crc) continue;
                    name = cand;
                    return OldMap(f.Records);
                }
                catch (Exception e) { Stumble("VillageBook.OldLayout", e); }
            }
            // f10-uklad: plik ukladu tej sesji (v7) niesie wiersz v6 kazdej wioski (kolumny v6_*) - zapis v6 po uid takze bez arm_map_villages_v6.tsv w module
            try
            {
                var cur6 = _file;
                var v6 = cur6 != null ? MapVillageData.V6Of(cur6.Records) : null;
                if (v6 != null && LayoutCrcOf(v6) == crc)
                {
                    name = (_fileName ?? MapVillageData.FileName) + " (kolumny v6_*)";
                    return OldMap(v6);
                }
            }
            catch (Exception e) { Stumble("VillageBook.OldLayout(v6_*)", e); }
            // sw-przysiolki (recenzja): zaden plik modulu nie ma crc zapisu - plik ukladu tej sesji (v6; f10-uklad: takze v7) niesie wiersz v5 kazdej wioski (kolumny v5_*):
            // zapis v5 po uid takze wtedy, gdy wgranie nie przenioslo arm_map_villages_v5.tsv (dotad: sumy okregow, flagi spalonych i pamiec wojny tracone)
            try
            {
                var cur = _file;
                var v5 = cur != null ? MapVillageData.V5Of(cur.Records) : null;
                if (v5 != null && LayoutCrcOf(v5) == crc)
                {
                    name = (_fileName ?? MapVillageData.FileName) + " (kolumny v5_*)";
                    return OldMap(v5);
                }
            }
            catch (Exception e) { Stumble("VillageBook.OldLayout(v5_*)", e); }
            return null;
        }

        /// <summary>Okregi ukladu zapisu z wierszy (ByDistrict: order, potem id - jak Build przy zapisie). Wydzielone z OldLayout (sw-przysiolki, recenzja) bez zmian.</summary>
        private static Dictionary<string, OldDist> OldMap(List<MapVillageRecord> recs)
        {
            var map = new Dictionary<string, OldDist>(StringComparer.Ordinal);
            foreach (var kv in MapVillageData.ByDistrict(recs))
            {
                int n = kv.Value.Count;
                var od = new OldDist { Uid = new string[n], X = new float[n], Y = new float[n], F = new double[n + 1] };
                for (int i = 0; i < n; i++) { var r = kv.Value[i]; od.Uid[i] = r.Uid; od.X[i] = r.X; od.Y[i] = r.Y; od.F[i] = Math.Max(1, r.Settlements); }
                map[kv.Key] = od;
            }
            return map;
        }

        private static Dist Build(Settlement st, List<MapVillageRecord> recs)
        {
            int w0 = recs != null ? recs.Count : 0;
            var d = new Dist { Id = st.StringId, V = st.Village, St = st, Name = NameOf(st), W = w0 };
            int n = w0 + 1;
            d.F = new double[n]; d.Nw = new double[n]; d.Uid = new string[n]; d.VName = new string[n]; d.Kind = new string[n]; d.Cls = new char[n]; d.Fish = new bool[n];
            d.X = new float[n]; d.Y = new float[n]; d.Rd = new float[n]; d.Order = new int[n];
            d.Br = new double[n]; d.Bf = new double[n]; d.Flag = new byte[n]; d.EvDay = new int[n]; d.RebuiltDay = new int[n]; d.M = new Mem[n];
            var p = st.GetPosition2D;
            d.X[w0] = p.x; d.Y[w0] = p.y; d.Rd[w0] = 0f; d.Cls[w0] = 'M'; d.VName[w0] = d.Name; d.Kind[w0] = ""; d.Order[w0] = int.MaxValue;
            for (int w = 0; w < w0; w++)
            {
                var r = recs[w];
                d.F[w] = Math.Max(1, r.Settlements); d.Uid[w] = r.Uid; d.VName[w] = r.Name; d.Kind[w] = r.Kind ?? "";
                d.Fish[w] = r.Model == MapVillageData.ModelFishing;      // E6/W2 (210): rodzaj z pliku (widok zamienia obrazek bez wody - ksiega nie)
                d.Cls[w] = !string.IsNullOrEmpty(r.Class) ? r.Class[0] : 'R';
                d.X[w] = r.X; d.Y[w] = r.Y; d.Order[w] = r.Order;
                // road_dist z pliku (krytyka K1); brak kolumny - odleglosc w linii prostej od wsi gry
                d.Rd[w] = r.RoadDist >= 0f ? r.RoadDist : (float)Math.Sqrt((r.X - p.x) * (r.X - p.x) + (r.Y - p.y) * (r.Y - p.y));
            }
            for (int w = 0; w < n; w++) { d.EvDay[w] = -1; d.RebuiltDay[w] = -1; }
            var fam = new List<int>(w0);
            for (int w = 0; w < w0; w++) fam.Add(w);
            fam.Sort((a, b) =>
            {
                int c = ClsRank(d.Cls[a]).CompareTo(ClsRank(d.Cls[b]));
                if (c != 0) return c;
                c = d.Rd[b].CompareTo(d.Rd[a]);                    // od najdalszej
                return c != 0 ? c : a.CompareTo(b);
            });
            fam.Add(w0);
            d.FamOrder = fam.ToArray();
            return d;
        }

        private static int ClsRank(char c) { return c == 'F' ? 0 : (c == 'H' ? 2 : 1); }

        /// <summary>
        /// OnSessionLaunched (obok RecruitKit.ResolvePending): plik ukladu, okregi (wszystkie wsie gry - takze bez wiosek), stan z zapisu. Stary zapis bez klucza i nowa
        /// kampania - n przy pierwszej dobie, puste osady przydzialem zastepczym z konta uchodzcow (3.7).
        /// </summary>
        internal static void Resolve(string how)
        {
            if (_resolveTried) return;
            _resolveTried = true;
            var sw = Stopwatch.StartNew();
            try
            {
                LoadFile();
                var byDist = _file != null ? MapVillageData.ByDistrict(_file.Records) : new Dictionary<string, List<MapVillageRecord>>(StringComparer.Ordinal);
                lock (Gate)
                {
                    _d.Clear(); _byUid.Clear(); _byV.Clear();
                    var seen = new HashSet<Village>();
                    foreach (var st in Settlement.All)
                    {
                        try
                        {
                            if (st == null || !st.IsVillage || st.Village == null || st.StringId == null || !seen.Add(st.Village)) continue;   // ROT podaje dwie wsie dwa razy
                            List<MapVillageRecord> recs;
                            byDist.TryGetValue(st.StringId, out recs);
                            var d = Build(st, recs);
                            _d[st.StringId] = d;
                            _byV[st.Village] = d;                    // E4 fixB (G4): 1:1 z _d (seen.Add wyzej)
                            for (int w = 0; w < d.W; w++) if (!string.IsNullOrEmpty(d.Uid[w])) _byUid[d.Uid[w]] = new KeyValuePair<Dist, int>(d, w);
                        }
                        catch (Exception e) { Stumble("VillageBook.Resolve(wies)", e); }
                    }
                    foreach (var kv in byDist) if (!_d.ContainsKey(kv.Key)) { _distNoVillage++; _rowsNoVillage += kv.Value.Count; _distNoVillageIds.Add(kv.Key); }
                    _distNoVillageIds.Sort(StringComparer.Ordinal);
                    ApplyPending();
                }
                _resolved = true;
                LookTouchAll();                             // E7/W4: stan z zapisu (albo pusty) - widok czyta wszystko od nowa
            }
            catch (Exception e) { Stumble("VillageBook.Resolve", e); }
            _resolveMs = sw.Elapsed.TotalMilliseconds;
            try
            {
                int with = 0; lock (Gate) foreach (var d in _d.Values) if (d.W > 0) with++;
                Log.Info("Ksiega wiosek (E4/11): start sesji (" + how + ") - " + (_resolved ? "okregi " + _d.Count + " (z wioskami " + with + ")" : "NIEROZWIAZANA - ksiega nieczynna do nastepnego wczytania")
                         + "; plik ukladu: " + (_file != null ? "'" + _file.Title + "' (crc ukladu " + _layoutCrc.ToString("X8") + ", plik crc32 " + _file.FileCrc32.ToString("X8") + ", " + _fileName
                                                     + (MapVillageData.ChoiceNote.Length > 0 ? " - " + MapVillageData.ChoiceNote : "") + ")" : _fileNote)
                         + "; " + _resolveNote + "; czas: plik " + F(_loadMs, "0.0") + " ms, okregi " + F(_resolveMs, "0.0") + " ms.");
            }
            catch (Exception e) { Stumble("VillageBook.Resolve(linia)", e); }
        }

        // zapis: "v1|L:<crc ukladu hex>|P:<ludzi na osade>|n:<id>=<N>;...|w:<id>=<br>/<bf>,...;...|x:<id>=<i>:<flaga>:<doba zdarzenia>:<doba odbudowy>,...;...|y:<id>=<ARexp>/<AFexp>;..."
        // (zarezerwowane w E4/11: r: sesje rabunku, c: czujnosc - pisze je E7/R1 przez DistrictRaid.Export/Import; u: uprowadzeni - wolne)
        // poprawka po recenzji (R1-5): "|o:1" - ksiega byla wylaczona przy zapisie (OffDay wyzerowal puste osady i konto oczekiwane) - po wczytaniu przydzial zastepczy
        // z powodem "wlaczenie" (dotad liczony jako doplyw bez zrodla d0); starsze DLL te sekcje pomijaja
        private static void ApplyPending()
        {
            string data = _pending;
            if (string.IsNullOrEmpty(data))
            {
                foreach (var d in _d.Values) d.Z = ZOld;
                _resolveNote = "z zapisu: brak klucza arm_district (nowa kampania albo zapis sprzed E4/11) - n przy pierwszej dobie, puste osady z konta uchodzcow (przydzial zastepczy)";
                return;
            }
            string[] parts = data.Split('|');
            if (parts.Length == 0 || parts[0] != "v1")
            {
                foreach (var d in _d.Values) d.Z = ZOld;
                _resolveNote = "z zapisu: nieznana wersja klucza arm_district - jak stary zapis";
                return;
            }
            uint crc = 0; bool hasCrc = false, wasOff = false; string sn = null, sw = null, sx = null, sy = null, sm = null, sr = null, sc = null;
            foreach (var p in parts)
            {
                if (p.Length < 2 || p[1] != ':') continue;
                string body = p.Substring(2);
                switch (p[0])
                {
                    case 'L': hasCrc = uint.TryParse(body, NumberStyles.HexNumber, Inv, out crc); break;
                    case 'P': { int pp; if (int.TryParse(body, NumberStyles.Integer, Inv, out pp) && pp > 0) { _p = pp; _pSet = true; } break; }
                    case 'n': sn = body; break;
                    case 'w': sw = body; break;
                    case 'x': sx = body; break;
                    case 'y': sy = body; break;
                    case 'm': sm = body; break;              // E7/W1 (232): pamiec wojny (starsze DLL te sekcje pomijaja)
                    case 'o': wasOff = body == "1"; break;
                    case 'r': sr = body; break;              // E7/R1 (235): sesje rabunku osada po osadzie (po id wsi gry - bez indeksow wiosek, niezalezne od ukladu)
                    case 'c': sc = body; break;              // E7/R1 (235): czujnosc wsi (id wsi gry -> doba konca)
                }
            }
            try { DistrictRaid.Import(sr, sc); } catch (Exception e) { Stumble("VillageBook.Import(r/c)", e); }
            _layoutSame = hasCrc && _file != null && crc == _layoutCrc;
            _saveCrc = crc; _saveCrcSet = hasCrc;            // recenzja calosci scalenia6: uklad zapisu dla VillageCrafts.LayoutCheck (n zajec wiosek liczone na tym ukladzie)
            if (!string.IsNullOrEmpty(sn))
                foreach (var item in sn.Split(';'))
                {
                    try
                    {
                        int eq = item.LastIndexOf('=');
                        if (eq <= 0) continue;
                        Dist d; double n;
                        if (!_d.TryGetValue(item.Substring(0, eq), out d) || !double.TryParse(item.Substring(eq + 1), NumberStyles.Float, Inv, out n) || double.IsNaN(n) || double.IsInfinity(n) || n < 1.0) continue;
                        d.N = n; d.NSet = true; Split(d); _savedN++;
                    }
                    catch (Exception e) { Stumble("VillageBook.Import(n)", e); }
                }
            if (!_layoutSame)
            {
                // D5 po recenzji: uklad zapisu to drugi plik ukladu modulu (crc "L:" = crc pliku _v4 albo v5) - stan wiosek po uid (puste osady, flagi, pamiec wojny,
                // wies gry stary W -> W), wioski usuniete do najblizszych; jak przy zgodnym ukladzie konto (y:) i powody przydzialu
                Dictionary<string, OldDist> old = null;
                string oldName = null;
                if (hasCrc && _file != null)
                {
                    try { old = OldLayout(crc, out oldName); }
                    catch (Exception e) { old = null; Stumble("VillageBook.Import(uklad zapisu)", e); }
                }
                if (old != null)
                {
                    _pendMapFile = oldName;
                    ApplyMapped(old, sw, sx, sy, sm, wasOff);
                    _resolveNote = "z zapisu: n " + _savedN + " okregow, uklad inny (crc zapisu " + crc.ToString("X8") + " = " + oldName + ", pliku " + _layoutCrc.ToString("X8") + ") - stan wiosek po uid:"
                                   + " puste osady " + _savedB + " okregow (wiosek, ktore zostaly, " + _mapKept + ", usunietych " + _mapGone + " - do najblizszych przy starcie sesji; rabunek "
                                   + F(_pendR0, "0.##") + ", glod " + F(_pendF0, "0.##") + " osad), flagi " + _mapFlags + " wiosek, pamiec wojny (E7/W1) " + _mapMem + " wpisow"
                                   + (_savedBad > 0 ? ", zepsute wpisy " + _savedBad + " (przydzial zastepczy)" : "")
                                   + (wasOff ? ", ksiega wylaczona przy zapisie (przydzial zastepczy - wlaczenie)" : "");
                    return;
                }
                foreach (var d in _d.Values) d.Z = ZLayout;
                // Jeff 16.10 (D5): sumy pustych osad okregu (w:) i konto oczekiwane (y:) nie zaleza od indeksow wiosek - przeniesione na wioski tego ukladu (PlacePending
                // przy starcie sesji); okreg z zepsutym wpisem albo bez wpisu w: - jak dotad przydzial zastepczy z konta uchodzcow (Z = inny uklad). Zapis z ksiega
                // wylaczona (o:1) - stan i tak wyzerowany: jak dotad. Uklad zapisu nieznany (zaden plik modulu nie ma crc "L:") - stad ta droga.
                if (!wasOff) PendingFromSave(sw, sx, sy);
                if (!string.IsNullOrEmpty(sm)) _pendMemSkipped = CountMem(sm);
                _resolveNote = "z zapisu: n " + _savedN + " okregow, puste osady - uklad inny (crc zapisu " + (hasCrc ? crc.ToString("X8") : "?") + ", pliku " + _layoutCrc.ToString("X8") + ", uklad zapisu nieznany) - "
                               + (_pendDists > 0 || _pendBad > 0
                                  ? "sumy z zapisu przeniesione na wioski tego ukladu w " + _pendDists + " okregach (rabunek " + F(_pendR0, "0.##") + ", glod " + F(_pendF0, "0.##")
                                    + " osad; rozlozone przy starcie sesji), zepsute wpisy " + _pendBad + " (przydzial zastepczy), okregi bez pustych osad w zapisie - przydzial zastepczy tylko przy koncie uchodzcow"
                                  : "przydzial zastepczy" + (wasOff ? " (ksiega wylaczona przy zapisie)" : ""))
                               + (string.IsNullOrEmpty(sm) ? "" : "; pamiec wojny (E7/W1) pominieta - indeksy wiosek z innego ukladu (" + _pendMemSkipped + " wpisow)");
                return;
            }
            var bad = new HashSet<Dist>();
            if (!string.IsNullOrEmpty(sw))
                foreach (var item in sw.Split(';'))
                {
                    try
                    {
                        int eq = item.LastIndexOf('=');
                        if (eq <= 0) continue;
                        Dist d;
                        if (!_d.TryGetValue(item.Substring(0, eq), out d)) continue;
                        var a = item.Substring(eq + 1).Split(',');
                        bool ok = d.NSet && a.Length == d.W + 1;
                        const double cap = 0.95;          // najwyzszy mozliwy pulap (MCM wchodzi po Resolve - biezacy pulap moze byc jeszcze z pliku ustawien)
                        var br = new double[d.W + 1]; var bf = new double[d.W + 1];
                        for (int i = 0; ok && i < a.Length; i++)
                        {
                            var q = a[i].Split('/');
                            if (q.Length != 2 || !double.TryParse(q[0], NumberStyles.Float, Inv, out br[i]) || !double.TryParse(q[1], NumberStyles.Float, Inv, out bf[i])
                                || double.IsNaN(br[i]) || double.IsNaN(bf[i]) || br[i] < 0.0 || bf[i] < 0.0 || br[i] + bf[i] > cap * d.Nw[i] + 1e-4) ok = false;
                        }
                        if (!ok) { bad.Add(d); continue; }
                        Array.Copy(br, d.Br, br.Length); Array.Copy(bf, d.Bf, bf.Length);
                        _savedB++;
                    }
                    catch (Exception e) { Stumble("VillageBook.Import(w)", e); }
                }
            if (!string.IsNullOrEmpty(sx))
                foreach (var item in sx.Split(';'))
                {
                    try
                    {
                        int eq = item.LastIndexOf('=');
                        if (eq <= 0) continue;
                        Dist d;
                        if (!_d.TryGetValue(item.Substring(0, eq), out d) || bad.Contains(d)) continue;
                        foreach (var e1 in item.Substring(eq + 1).Split(','))
                        {
                            var q = e1.Split(':');
                            int i, fl, ev, rb;
                            if (q.Length != 4 || !int.TryParse(q[0], NumberStyles.Integer, Inv, out i) || i < 0 || i > d.W
                                || !int.TryParse(q[1], NumberStyles.Integer, Inv, out fl) || !int.TryParse(q[2], NumberStyles.Integer, Inv, out ev)
                                || !int.TryParse(q[3], NumberStyles.Integer, Inv, out rb)) continue;
                            d.Flag[i] = (byte)(fl & 3); d.EvDay[i] = ev; d.RebuiltDay[i] = rb;
                        }
                    }
                    catch (Exception e) { Stumble("VillageBook.Import(x)", e); }
                }
            if (!string.IsNullOrEmpty(sy))
                foreach (var item in sy.Split(';'))
                {
                    try
                    {
                        int eq = item.LastIndexOf('=');
                        if (eq <= 0) continue;
                        Dist d;
                        if (!_d.TryGetValue(item.Substring(0, eq), out d) || bad.Contains(d)) continue;
                        var q = item.Substring(eq + 1).Split('/');
                        double ar, af;
                        if (q.Length == 2 && double.TryParse(q[0], NumberStyles.Float, Inv, out ar) && double.TryParse(q[1], NumberStyles.Float, Inv, out af))
                        { d.ARexp = ar > 0.0 && !double.IsInfinity(ar) ? ar : 0.0; d.AFexp = af > 0.0 && !double.IsInfinity(af) ? af : 0.0; }
                    }
                    catch (Exception e) { Stumble("VillageBook.Import(y)", e); }
                }
            // E7/W1 (232): pamiec wojny - indeksy wiosek waza tylko przy zgodnym ukladzie (wyzej return przy innym); historia, nie stan - zepsute puste osady (bad) jej nie kasuja
            if (!string.IsNullOrEmpty(sm))
                foreach (var item in sm.Split(';'))
                {
                    try
                    {
                        int eq = item.LastIndexOf('=');
                        if (eq <= 0) continue;
                        Dist d;
                        if (!_d.TryGetValue(item.Substring(0, eq), out d)) continue;
                        foreach (var e1 in item.Substring(eq + 1).Split(','))
                        {
                            var q = e1.Split(':');
                            int i, day, burns; double fled, dead;
                            if (q.Length != 8 || !int.TryParse(q[0], NumberStyles.Integer, Inv, out i) || i < 0 || i > d.W || q[1].Length != 1 || (q[1][0] != 'B' && q[1][0] != 'P')
                                || !int.TryParse(q[2], NumberStyles.Integer, Inv, out day) || !double.TryParse(q[5], NumberStyles.Float, Inv, out fled) || !double.TryParse(q[6], NumberStyles.Float, Inv, out dead)
                                || !int.TryParse(q[7], NumberStyles.Integer, Inv, out burns) || double.IsNaN(fled) || double.IsNaN(dead) || double.IsInfinity(fled) || double.IsInfinity(dead)) continue;
                            d.M[i] = new Mem { Kind = q[1][0], Day = day, Hero = q[3], Clan = q[4], Fled = Math.Max(0.0, fled), Dead = Math.Max(0.0, dead), Burns = Math.Max(0, burns) };
                            _savedM++;
                        }
                    }
                    catch (Exception e) { Stumble("VillageBook.Import(m)", e); }
                }
            if (wasOff) foreach (var d in _d.Values) d.Z = ZEnable;          // R1-5 (zepsute wpisy nizej nadpisuja na "inny uklad")
            foreach (var d in bad)
            {
                Array.Clear(d.Br, 0, d.Br.Length); Array.Clear(d.Bf, 0, d.Bf.Length);
                for (int i = 0; i <= d.W; i++) { d.Flag[i] = 0; d.EvDay[i] = -1; d.RebuiltDay[i] = -1; }
                d.ARexp = d.AFexp = 0.0; d.Z = ZLayout; _savedBad++;
            }
            _resolveNote = "z zapisu: n " + _savedN + " okregow, puste osady " + _savedB + " okregow, uklad zgodny" + (_savedBad > 0 ? ", zepsute wpisy " + _savedBad + " (przydzial zastepczy)" : "")
                           + (wasOff ? ", ksiega wylaczona przy zapisie (przydzial zastepczy - wlaczenie)" : "")
                           + ", pamiec wojny (E7/W1) " + _savedM + " wpisow" + (sm == null ? " (zapis bez sekcji m:)" : "");
        }

        /// <summary>D5 po recenzji: stan okregu z zapisu na STARYM indeksie (0..oW-1 wioski ukladu zapisu, oW = wies gry).</summary>
        private sealed class OldState
        {
            public OldDist D; public int W; public byte[] Fl; public int[] Ev, Rb; public Mem[] M; public double[] Br, Bf, Nw; public bool HasW;
        }

        private static OldState OldStateOf(Dictionary<Dist, OldState> all, Dictionary<string, OldDist> old, Dist d)
        {
            OldState o;
            if (all.TryGetValue(d, out o)) return o;
            OldDist od;
            old.TryGetValue(d.Id, out od);
            int n = (od != null ? od.Uid.Length : 0) + 1;
            o = new OldState { D = od, W = n - 1, Fl = new byte[n], Ev = new int[n], Rb = new int[n], M = new Mem[n] };
            for (int i = 0; i < n; i++) { o.Ev[i] = -1; o.Rb[i] = -1; }
            all[d] = o;
            return o;
        }

        /// <summary>D5 po recenzji: stary indeks wioski -> indeks tego ukladu: wies gry (stary W) -> W, wioska, ktora zostala (ten sam uid w tym okregu) -> jej indeks,
        /// usunieta -> -1.</summary>
        private static int NewIndex(Dist d, OldState o, int i)
        {
            if (i == o.W) return d.W;
            if (o.D == null || i < 0 || i >= o.W) return -1;
            KeyValuePair<Dist, int> kv;
            return _byUid.TryGetValue(o.D.Uid[i] ?? "", out kv) && ReferenceEquals(kv.Key, d) && kv.Value >= 0 && kv.Value < d.W ? kv.Value : -1;
        }

        /// <summary>
        /// D5 po recenzji (zapis Jeffa: uklad v4 wczytany na v5): stan wiosek z zapisu innego, ale ZNANEGO ukladu (OldLayout) - stary indeks -> uid -> indeks tego ukladu:
        ///  - wioska, ktora zostala, i wies gry (stary W -> W): flaga (spalona / opuszczona / w odbudowie), doba zdarzenia i odbudowy oraz pamiec wojny wprost; puste osady
        ///    (Pend.Kr/Kf) rozkladane przy starcie sesji z biezacym pulapem - spalona albo opuszczona, ktora w nowym ukladzie urosla, dostaje tyle osad, zeby jej udzial
        ///    (a wiec stan) zostal (licznik "dolozone dla stanu"); nadmiar ponad pulap do najblizszych;
        ///  - wioska usunieta: puste osady, flaga i pamiec wojny do najblizszych wiosek okregu z miejscem (PoolItem), wies gry na koncu;
        ///  - konto oczekiwane (y:), zepsute wpisy (przydzial zastepczy "inny uklad") i zapis z ksiega wylaczona (o:1 - "wlaczenie") jak przy zgodnym ukladzie.
        /// Pod Gate (ApplyPending), kazdy wpis we wlasnym try, nie rzuca.
        /// </summary>
        private static void ApplyMapped(Dictionary<string, OldDist> old, string sw, string sx, string sy, string sm, bool wasOff)
        {
            var st = new Dictionary<Dist, OldState>();
            var bad = new HashSet<Dist>();
            if (!string.IsNullOrEmpty(sx))
                foreach (var item in sx.Split(';'))
                {
                    try
                    {
                        int eq = item.LastIndexOf('=');
                        if (eq <= 0) continue;
                        Dist d;
                        if (!_d.TryGetValue(item.Substring(0, eq), out d)) continue;
                        var o = OldStateOf(st, old, d);
                        foreach (var e1 in item.Substring(eq + 1).Split(','))
                        {
                            var q = e1.Split(':');
                            int i, fl, ev, rb;
                            if (q.Length != 4 || !int.TryParse(q[0], NumberStyles.Integer, Inv, out i) || i < 0 || i > o.W
                                || !int.TryParse(q[1], NumberStyles.Integer, Inv, out fl) || !int.TryParse(q[2], NumberStyles.Integer, Inv, out ev)
                                || !int.TryParse(q[3], NumberStyles.Integer, Inv, out rb)) continue;
                            o.Fl[i] = (byte)(fl & 3); o.Ev[i] = ev; o.Rb[i] = rb;
                        }
                    }
                    catch (Exception e) { Stumble("VillageBook.Import(x, inny uklad)", e); }
                }
            if (!string.IsNullOrEmpty(sm))
                foreach (var item in sm.Split(';'))
                {
                    try
                    {
                        int eq = item.LastIndexOf('=');
                        if (eq <= 0) continue;
                        Dist d;
                        if (!_d.TryGetValue(item.Substring(0, eq), out d)) continue;
                        var o = OldStateOf(st, old, d);
                        foreach (var e1 in item.Substring(eq + 1).Split(','))
                        {
                            var q = e1.Split(':');
                            int i, day, burns; double fled, dead;
                            if (q.Length != 8 || !int.TryParse(q[0], NumberStyles.Integer, Inv, out i) || i < 0 || i > o.W || q[1].Length != 1 || (q[1][0] != 'B' && q[1][0] != 'P')
                                || !int.TryParse(q[2], NumberStyles.Integer, Inv, out day) || !double.TryParse(q[5], NumberStyles.Float, Inv, out fled) || !double.TryParse(q[6], NumberStyles.Float, Inv, out dead)
                                || !int.TryParse(q[7], NumberStyles.Integer, Inv, out burns) || double.IsNaN(fled) || double.IsNaN(dead) || double.IsInfinity(fled) || double.IsInfinity(dead)) continue;
                            o.M[i] = new Mem { Kind = q[1][0], Day = day, Hero = q[3], Clan = q[4], Fled = Math.Max(0.0, fled), Dead = Math.Max(0.0, dead), Burns = Math.Max(0, burns) };
                        }
                    }
                    catch (Exception e) { Stumble("VillageBook.Import(m, inny uklad)", e); }
                }
            if (!string.IsNullOrEmpty(sw))
                foreach (var item in sw.Split(';'))
                {
                    Dist d = null;
                    try
                    {
                        int eq = item.LastIndexOf('=');
                        if (eq <= 0) continue;
                        if (!_d.TryGetValue(item.Substring(0, eq), out d)) continue;
                        var o = OldStateOf(st, old, d);
                        var a = item.Substring(eq + 1).Split(',');
                        bool ok = d.NSet && a.Length == o.W + 1;
                        const double cap = 0.95;          // najwyzszy mozliwy pulap (jak przy zgodnym ukladzie), wobec starego podzialu n okregu
                        var nw = new double[o.W + 1];
                        if (ok) SplitInto(d.N, o.D != null ? o.D.F : new double[1], o.W, nw);
                        var br = new double[o.W + 1]; var bf = new double[o.W + 1];
                        for (int i = 0; ok && i < a.Length; i++)
                        {
                            var q = a[i].Split('/');
                            if (q.Length != 2 || !double.TryParse(q[0], NumberStyles.Float, Inv, out br[i]) || !double.TryParse(q[1], NumberStyles.Float, Inv, out bf[i])
                                || double.IsNaN(br[i]) || double.IsNaN(bf[i]) || double.IsInfinity(br[i]) || double.IsInfinity(bf[i]) || br[i] < 0.0 || bf[i] < 0.0
                                || br[i] + bf[i] > cap * nw[i] + 1e-4) ok = false;
                        }
                        if (!ok) { bad.Add(d); continue; }
                        o.Br = br; o.Bf = bf; o.Nw = nw; o.HasW = true;
                    }
                    catch (Exception e) { if (d != null) bad.Add(d); Stumble("VillageBook.Import(w, inny uklad)", e); }
                }
            var acc = new Dictionary<Dist, KeyValuePair<double, double>>();
            if (!string.IsNullOrEmpty(sy))
                foreach (var item in sy.Split(';'))
                {
                    try
                    {
                        int eq = item.LastIndexOf('=');
                        if (eq <= 0) continue;
                        Dist d;
                        if (!_d.TryGetValue(item.Substring(0, eq), out d)) continue;
                        var q = item.Substring(eq + 1).Split('/');
                        double ar, af;
                        if (q.Length == 2 && double.TryParse(q[0], NumberStyles.Float, Inv, out ar) && double.TryParse(q[1], NumberStyles.Float, Inv, out af))
                            acc[d] = new KeyValuePair<double, double>(ar > 0.0 && !double.IsInfinity(ar) ? ar : 0.0, af > 0.0 && !double.IsInfinity(af) ? af : 0.0);
                    }
                    catch (Exception e) { Stumble("VillageBook.Import(y, inny uklad)", e); }
                }
            if (wasOff) foreach (var d in _d.Values) d.Z = ZEnable;          // R1-5 jak przy zgodnym ukladzie (zepsute wpisy nizej - "inny uklad")
            foreach (var kv in acc)
                if (!bad.Contains(kv.Key)) { kv.Key.ARexp = kv.Value.Key; kv.Key.AFexp = kv.Value.Value; }
            foreach (var kv in st)
            {
                var d = kv.Key; var o = kv.Value;
                try
                {
                    bool isBad = bad.Contains(d);
                    Pend p = null;
                    for (int i = 0; i <= o.W; i++)
                    {
                        int j = NewIndex(d, o, i);
                        double r = !isBad && o.HasW ? o.Br[i] : 0.0, f = !isBad && o.HasW ? o.Bf[i] : 0.0;
                        if (j >= 0)
                        {
                            if (o.M[i] != null) { d.M[j] = o.M[i]; _mapMem++; }               // historia, nie stan - zepsute puste osady jej nie kasuja (jak przy zgodnym ukladzie)
                            if (isBad) continue;
                            d.Flag[j] = o.Fl[i]; d.EvDay[j] = o.Ev[i]; d.RebuiltDay[j] = o.Rb[i];
                            if (o.Fl[i] != 0) _mapFlags++;
                            if (r + f <= 0.0) continue;
                            // stan spalonej / opuszczonej (BurnedAt, StateAt) zalezy od udzialu - wioska, ktora urosla, dostaje osady w tej samej proporcji
                            double onw = o.Nw[i];
                            bool held = onw > Eps && (((o.Fl[i] & FBurned) != 0 && r >= KeepShare * onw - 1e-12) || ((o.Fl[i] & FAbandoned) != 0 && r + f >= KeepShare * onw - 1e-12));
                            if (held && d.Nw[j] > onw + 1e-9)
                            {
                                double k = d.Nw[j] / onw;
                                _pendAddR += r * (k - 1.0); _pendAddF += f * (k - 1.0);
                                r *= k; f *= k; _mapHeld++;
                            }
                            if (p == null) p = NewMapPend(d);
                            p.Kr[j] += r; p.Kf[j] += f;
                            _pendR0 += r; _pendF0 += f;
                            if (j < d.W) _mapKept++;
                        }
                        else if (r + f > 0.0 || o.M[i] != null)
                        {
                            // wioska usunieta w tym ukladzie - do najblizszych przy starcie sesji (zepsuty wpis: tylko pamiec)
                            if (p == null) p = NewMapPend(d);
                            p.Pool.Add(new PoolItem { X = o.D != null ? o.D.X[i] : d.X[d.W], Y = o.D != null ? o.D.Y[i] : d.Y[d.W], R = r, F = f,
                                                      Flag = isBad ? (byte)0 : o.Fl[i], Ev = o.Ev[i], Rb = o.Rb[i], M = o.M[i] });
                            _pendR0 += r; _pendF0 += f;
                            if (r + f > 0.0) _mapGone++;
                        }
                    }
                    if (!isBad && o.HasW) _savedB++;
                    if (p != null)
                    {
                        for (int i = 0; i <= o.W; i++) if ((o.Fl[i] & 3) != 0 && o.Ev[i] > p.Ev) p.Ev = o.Ev[i];   // doba ostatniego zdarzenia okregu (CheckUp nowych flag)
                        p.NoAcc = !isBad && o.HasW && !acc.ContainsKey(d);
                        d.P = p;
                        if (o.HasW && !isBad) _pendDists++;
                    }
                }
                catch (Exception e) { Stumble("VillageBook.Import(po uid)", e); }
            }
            foreach (var d in bad)
            {
                Array.Clear(d.Br, 0, d.Br.Length); Array.Clear(d.Bf, 0, d.Bf.Length);
                for (int i = 0; i <= d.W; i++) { d.Flag[i] = 0; d.EvDay[i] = -1; d.RebuiltDay[i] = -1; }
                d.ARexp = d.AFexp = 0.0; d.Z = ZLayout; _savedBad++;
            }
        }

        private static Pend NewMapPend(Dist d)
        {
            return new Pend { Map = true, Kr = new double[d.W + 1], Kf = new double[d.W + 1], Pool = new List<PoolItem>() };
        }

        /// <summary>
        /// Jeff 16.10 (D5): zapis z innym ukladem wiosek - z sekcji w: (puste osady na indeks wioski starego ukladu, ostatni = wies gry) sumy na okreg: rabunek i glod
        /// osobno w wioskach i we wsi gry; z y: konto oczekiwane (ARexp / AFexp - na okreg, bez indeksow); z x: najpozniejsza doba zdarzenia flag okregu. Wpis zepsuty
        /// (liczba, ujemna, NaN, suma ponad 95% n okregu) - okreg zostaje przy przydziale zastepczym (Z = inny uklad). Pod Gate (ApplyPending), kazdy wpis we wlasnym try.
        /// </summary>
        private static void PendingFromSave(string sw, string sx, string sy)
        {
            var ev = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(sx))
                foreach (var item in sx.Split(';'))
                {
                    try
                    {
                        int eq = item.LastIndexOf('=');
                        if (eq <= 0) continue;
                        int best = -1;
                        foreach (var e1 in item.Substring(eq + 1).Split(','))
                        {
                            var q = e1.Split(':');
                            int fl, day;
                            if (q.Length != 4 || !int.TryParse(q[1], NumberStyles.Integer, Inv, out fl) || !int.TryParse(q[2], NumberStyles.Integer, Inv, out day)) continue;
                            if ((fl & 3) != 0 && day > best) best = day;
                        }
                        if (best >= 0) ev[item.Substring(0, eq)] = best;
                    }
                    catch (Exception e) { Stumble("VillageBook.PendingFromSave(x)", e); }
                }
            var acc = new Dictionary<string, KeyValuePair<double, double>>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(sy))
                foreach (var item in sy.Split(';'))
                {
                    try
                    {
                        int eq = item.LastIndexOf('=');
                        if (eq <= 0) continue;
                        var q = item.Substring(eq + 1).Split('/');
                        double ar, af;
                        if (q.Length == 2 && double.TryParse(q[0], NumberStyles.Float, Inv, out ar) && double.TryParse(q[1], NumberStyles.Float, Inv, out af))
                            acc[item.Substring(0, eq)] = new KeyValuePair<double, double>(ar > 0.0 && !double.IsInfinity(ar) ? ar : 0.0, af > 0.0 && !double.IsInfinity(af) ? af : 0.0);
                    }
                    catch (Exception e) { Stumble("VillageBook.PendingFromSave(y)", e); }
                }
            if (string.IsNullOrEmpty(sw)) return;
            foreach (var item in sw.Split(';'))
            {
                try
                {
                    int eq = item.LastIndexOf('=');
                    if (eq <= 0) continue;
                    Dist d;
                    string id = item.Substring(0, eq);
                    if (!_d.TryGetValue(id, out d)) continue;
                    var a = item.Substring(eq + 1).Split(',');
                    bool ok = d.NSet && a.Length >= 1;
                    double rs = 0.0, rm = 0.0, fs = 0.0, fm = 0.0;
                    for (int i = 0; ok && i < a.Length; i++)
                    {
                        var q = a[i].Split('/');
                        double br, bf;
                        if (q.Length != 2 || !double.TryParse(q[0], NumberStyles.Float, Inv, out br) || !double.TryParse(q[1], NumberStyles.Float, Inv, out bf)
                            || double.IsNaN(br) || double.IsNaN(bf) || double.IsInfinity(br) || double.IsInfinity(bf) || br < 0.0 || bf < 0.0) { ok = false; break; }
                        if (i == a.Length - 1) { rm = br; fm = bf; } else { rs += br; fs += bf; }   // ostatni indeks starego ukladu = wies gry (Build: indeks W)
                    }
                    if (ok && rs + rm + fs + fm > 0.95 * d.N + 1e-4) ok = false;   // najwyzszy pulap (jak przy zgodnym ukladzie) wobec n okregu
                    if (!ok) { _pendBad++; continue; }
                    if (rs + rm + fs + fm <= 0.0) continue;
                    int e0;
                    d.P = new Pend { Rs = rs, Rm = rm, Fs = fs, Fm = fm, Ev = ev.TryGetValue(id, out e0) ? e0 : -1 };
                    d.Z = ZNone;                             // stan z zapisu (rozlozony w pierwszej dobie), nie przydzial zastepczy
                    KeyValuePair<double, double> y;
                    if (acc.TryGetValue(id, out y)) { d.ARexp = y.Key; d.AFexp = y.Value; }
                    else d.P.NoAcc = true;
                    _pendDists++; _pendR0 += rs + rm; _pendF0 += fs + fm;
                }
                catch (Exception e) { _pendBad++; Stumble("VillageBook.PendingFromSave(w)", e); }
            }
        }

        private static int CountMem(string sm)
        {
            int n = 0;
            try { foreach (var item in sm.Split(';')) { int eq = item.LastIndexOf('='); if (eq > 0) n += item.Substring(eq + 1).Split(',').Length; } }
            catch { }
            return n;
        }

        /// <summary>Jeff 16.10 (D5): rozklada puste osady z zapisu innego ukladu (Pend) na wioski tego ukladu z biezacym pulapem - wies gry najpierw swoja czesc,
        /// wioski wedlug kolejnosci (rabunek: indeks = order - ByDistrict sortuje wiersze okregu wedlug order, potem id, a order to 0..W-1 w kazdym okregu v4 i v5, choc
        /// wiersze w pliku ida inna kolejnoscia; glod: FamOrder - ubocze, linia, wezel), nadmiar do wsi gry, reszta ponad pulap policzona. Flagi stanu (spalona /
        /// opuszczona) od nowa w CheckUp bez wiadomosci (list false) z doba ostatniego zdarzenia okregu z zapisu (brak - ostatni najazd regionu, inaczej dzis).
        /// D5 po recenzji: uklad zapisu znany (p.Map) - wioski, ktore zostaly, i wies gry swoje osady na swoim indeksie, wioski usuniete do najblizszych (Spread).
        /// Pod Gate (PlacePendingAll przy starcie sesji, zapas: Daily albo Export), nie rzuca.</summary>
        private static void PlacePending(Dist d, int today)
        {
            var p = d.P;
            d.P = null;
            if (p == null) return;
            try
            {
                int ev = p.Ev;
                if (ev < 0)
                {
                    ev = today;
                    try { int last; if (PeopleBook.LastStrike(OutlawLaw.RegionFor(d.St), out last)) ev = last; } catch (Exception e) { Stumble("VillageBook.PlacePending(zegar)", e); }
                }
                if (p.Map)
                {
                    PlaceMapped(d, p);
                    for (int w = 0; w <= d.W; w++) if (d.Br[w] + d.Bf[w] > 0.0) CheckUp(d, w, ev, "inny uklad", false);
                    if (p.NoAcc) { var s0 = SnapOf(d); d.ARexp = s0.Ar; d.AFexp = s0.Af; }
                    d.Event = true;
                    LookTouch(d);                           // E7/W4: flagi z zapisu przyszly przy Resolve, puste osady dopiero tu - widok czyta okreg od nowa
                    return;
                }
                // rabunek: wies gry swoja czesc, wioski wedlug kolejnosci, nadmiar do wsi gry
                double put = Math.Min(Math.Max(0.0, Room(d, d.W)), p.Rm);
                d.Br[d.W] += put; _pendR += put;
                double left = p.Rs + (p.Rm - put);
                for (int w = 0; w < d.W && left > Eps; w++) { double x = Math.Min(Math.Max(0.0, Room(d, w)), left); d.Br[w] += x; left -= x; _pendR += x; }
                if (left > Eps) { double x = Math.Min(Math.Max(0.0, Room(d, d.W)), left); d.Br[d.W] += x; left -= x; _pendR += x; }
                if (left > Eps) _pendDrop += left;
                // glod: wies gry swoja czesc, wioski kolejnoscia opuszczania (FamOrder konczy sie wsia gry)
                put = Math.Min(Math.Max(0.0, Room(d, d.W)), p.Fm);
                d.Bf[d.W] += put; _pendF += put;
                left = p.Fs + (p.Fm - put);
                foreach (int w in d.FamOrder)
                {
                    if (left <= Eps) break;
                    double x = Math.Min(Math.Max(0.0, Room(d, w)), left); d.Bf[w] += x; left -= x; _pendF += x;
                }
                if (left > Eps) _pendDrop += left;
                for (int w = 0; w <= d.W; w++) if (d.Br[w] + d.Bf[w] > 0.0) CheckUp(d, w, ev, "inny uklad", false);
                // zapis bez konta oczekiwanego okregu (y:) przy pustych osadach (rzadkie) - konto biezace, inaczej uzgodnienie dolozyloby osady drugi raz z konta
                if (p.NoAcc) { var sn = SnapOf(d); d.ARexp = sn.Ar; d.AFexp = sn.Af; }
                d.Event = true;
            }
            catch (Exception e) { Stumble("VillageBook.PlacePending", e); }
        }

        /// <summary>D5 po recenzji: wioski, ktore zostaly, i wies gry - swoje puste osady (Kr/Kf) do biezacego pulapu, nadmiar od nich do najblizszych; potem wioski
        /// usuniete (Pool) do najblizszych. Pod Gate (PlacePending).</summary>
        private static void PlaceMapped(Dist d, Pend p)
        {
            var pool = p.Pool ?? new List<PoolItem>();
            for (int j = 0; j <= d.W; j++)
            {
                double kr = p.Kr != null ? p.Kr[j] : 0.0, kf = p.Kf != null ? p.Kf[j] : 0.0;
                if (kr <= 0.0 && kf <= 0.0) continue;
                double x = Math.Min(Math.Max(0.0, Room(d, j)), kr); d.Br[j] += x; _pendR += x;
                double y = Math.Min(Math.Max(0.0, Room(d, j)), kf); d.Bf[j] += y; _pendF += y;
                if (kr - x > Eps || kf - y > Eps) pool.Add(new PoolItem { X = d.X[j], Y = d.Y[j], R = Math.Max(0.0, kr - x), F = Math.Max(0.0, kf - y), From = j });
            }
            for (int k = 0; k < pool.Count; k++) Spread(d, pool[k]);
        }

        /// <summary>D5 po recenzji: puste osady od punktu wioski usunietej (albo nadmiar wioski, ktora zostala) - najblizsze wioski okregu z miejscem, wies gry na koncu;
        /// reszta ponad pulap policzona (_pendDrop). Flaga wioski usunietej przechodzi na najblizsza, ktora wziela jej osady (gdy ta bez flagi); pamiec wojny - na nia
        /// albo na najblizsza wioske (przy zajetej zostaje nowsza). Pod Gate.</summary>
        private static void Spread(Dist d, PoolItem it)
        {
            var near = new List<int>(d.W + 1);
            for (int w = 0; w < d.W; w++) if (w != it.From) near.Add(w);
            near.Sort((a, b) =>
            {
                double da = (d.X[a] - it.X) * (double)(d.X[a] - it.X) + (d.Y[a] - it.Y) * (double)(d.Y[a] - it.Y);
                double db = (d.X[b] - it.X) * (double)(d.X[b] - it.X) + (d.Y[b] - it.Y) * (double)(d.Y[b] - it.Y);
                int c = da.CompareTo(db);
                return c != 0 ? c : a.CompareTo(b);
            });
            if (it.From != d.W) near.Add(d.W);             // wies gry na koncu (jak lancuch palenia)
            int first = -1;
            foreach (int w in near)
            {
                if (it.R <= Eps && it.F <= Eps) break;
                double x = Math.Min(Math.Max(0.0, Room(d, w)), it.R); d.Br[w] += x; it.R -= x; _pendR += x;
                double y = Math.Min(Math.Max(0.0, Room(d, w)), it.F); d.Bf[w] += y; it.F -= y; _pendF += y;
                if (first < 0 && x + y > 0.0) first = w;
            }
            if (it.R > Eps) _pendDrop += it.R;
            if (it.F > Eps) _pendDrop += it.F;
            it.R = it.F = 0.0;
            if (it.Flag != 0 && first >= 0 && d.Flag[first] == 0) { d.Flag[first] = it.Flag; d.EvDay[first] = it.Ev; d.RebuiltDay[first] = it.Rb; _mapFlagsMoved++; }
            if (it.M != null && d.M != null)
            {
                int t = first >= 0 ? first : (near.Count > 0 ? near[0] : d.W);
                var cur = d.M[t];
                if (cur == null) { d.M[t] = it.M; _mapMemMoved++; }
                else { if (it.M.Day > cur.Day) d.M[t] = it.M; _mapMemLost++; }     // jedna pamiec na wioske - zostaje nowsza
            }
        }

        /// <summary>
        /// Jeff 16.10 (D5, po recenzji): puste osady z zapisu innego ukladu na wioski tego ukladu ZARAZ PO McmSettings.Apply w OnSessionLaunched (biezacy pulap
        /// DevastationMaxPercent juz z MCM) - dotad lezaly w Pend do pierwszej doby (do 24 h gry): dymek, menu i wyglad pokazywaly spalone wioski z zapisu jako stojace,
        /// o polnocy robily sie ciemne bez komunikatu, a Devastation.Strike w tym oknie zajmowal ich miejsce (czesc Pend przepadala ponad pulapem). Wywolania w Daily
        /// i w Export zostaja jako zapas. Pod Gate, nie rzuca.
        /// </summary>
        internal static void PlacePendingAll()
        {
            if (!_resolved) return;
            try
            {
                int n = 0;
                lock (Gate)
                {
                    int t = Today();
                    foreach (var d in _d.Values) if (d.P != null) { PlacePending(d, t); n++; }
                }
                if (n > 0) _pendWhere = "przy starcie sesji (po MCM) w " + n + " okregach";
            }
            catch (Exception e) { Stumble("VillageBook.PlacePendingAll", e); }
        }

        /// <summary>SyncData (zapis). Ksiega nierozwiazana (zapis przed startem sesji) - napis z wczytania bez zmian.</summary>
        internal static string Export()
        {
            if (!_resolved) return _pending ?? "";
            // Jeff 16.10 (D5): zapis przed rozlozeniem pustych osad z zapisu z innym ukladem (zapas - zwykle PlacePendingAll juz przy starcie sesji) - najpierw na wioski
            try { lock (Gate) { int t = Today(), n = 0; foreach (var d in _d.Values) if (d.P != null) { PlacePending(d, t); n++; } if (n > 0 && _pendWhere == "-") _pendWhere = "przy zapisie w " + n + " okregach"; } }
            catch (Exception e) { Stumble("VillageBook.Export(przeniesienie)", e); }
            var sb = new StringBuilder(32 * 1024);
            sb.Append("v1|L:").Append(_layoutCrc.ToString("X8")).Append("|P:").Append(_p.ToString(Inv));
            if (_offSeen) sb.Append("|o:1");                                // R1-5: stan wyzerowany przez OffDay, wlaczenie jeszcze nie przeszlo doby
            int memN = 0, memBad = 0;
            lock (Gate)
            {
                sb.Append("|n:");
                bool first = true;
                foreach (var d in _d.Values)
                {
                    if (!d.NSet || BadId(d.Id)) continue;
                    if (!first) sb.Append(';');
                    sb.Append(d.Id).Append('=').Append(d.N.ToString("0.##", Inv));
                    first = false;
                }
                sb.Append("|w:"); first = true;
                foreach (var d in _d.Values)
                {
                    if (!d.NSet || BadId(d.Id) || (Sum(d.Br) <= 0.0 && Sum(d.Bf) <= 0.0)) continue;
                    if (!first) sb.Append(';');
                    sb.Append(d.Id).Append('=');
                    for (int i = 0; i <= d.W; i++) sb.Append(i > 0 ? "," : "").Append(d.Br[i].ToString("0.######", Inv)).Append('/').Append(d.Bf[i].ToString("0.######", Inv));
                    first = false;
                }
                sb.Append("|x:"); first = true;
                foreach (var d in _d.Values)
                {
                    if (BadId(d.Id)) continue;
                    bool any = false;
                    for (int i = 0; i <= d.W; i++)
                    {
                        if (d.Flag[i] == 0 && d.EvDay[i] < 0 && d.RebuiltDay[i] < 0) continue;
                        if (!any) { if (!first) sb.Append(';'); sb.Append(d.Id).Append('='); first = false; } else sb.Append(',');
                        sb.Append(i.ToString(Inv)).Append(':').Append(((int)d.Flag[i]).ToString(Inv)).Append(':').Append(d.EvDay[i].ToString(Inv)).Append(':').Append(d.RebuiltDay[i].ToString(Inv));
                        any = true;
                    }
                }
                sb.Append("|y:"); first = true;
                foreach (var d in _d.Values)
                {
                    if (BadId(d.Id) || (!(d.ARexp > 0.0) && !(d.AFexp > 0.0))) continue;
                    if (!first) sb.Append(';');
                    sb.Append(d.Id).Append('=').Append(d.ARexp.ToString("0.###", Inv)).Append('/').Append(d.AFexp.ToString("0.###", Inv));
                    first = false;
                }
                // E7/W1 (232, W1-zapis): "|m:<id>=<i>:<B|P>:<doba>:<bohater>:<rod>:<ucieklo>:<zginelo>:<spalen>,...;..." - same StringId po BadId (nigdy wyswietlana nazwa:
                // znak '|' w nazwie partii zepsulby Split calej ksiegi, a nazwa zostalaby zamrozona w jezyku); identyfikator odrzucony = pusty (dymek: tekst zastepczy)
                sb.Append("|m:"); first = true;
                foreach (var d in _d.Values)
                {
                    if (BadId(d.Id) || d.M == null) continue;
                    bool any = false;
                    for (int i = 0; i <= d.W && i < d.M.Length; i++)
                    {
                        var m = d.M[i];
                        if (m == null) continue;
                        string h = m.Hero ?? "", c = m.Clan ?? "";
                        if (h.Length > 0 && BadId(h)) { h = ""; memBad++; }
                        if (c.Length > 0 && BadId(c)) { c = ""; memBad++; }
                        if (!any) { if (!first) sb.Append(';'); sb.Append(d.Id).Append('='); first = false; } else sb.Append(',');
                        sb.Append(i.ToString(Inv)).Append(':').Append(m.Kind == 'B' ? 'B' : 'P').Append(':').Append(m.Day.ToString(Inv)).Append(':').Append(h).Append(':').Append(c)
                          .Append(':').Append(F0(m.Fled)).Append(':').Append(F0(m.Dead)).Append(':').Append(m.Burns.ToString(Inv));
                        any = true; memN++;
                    }
                }
            }
            try { var st = Settings.Current; if (st != null && st.LogEnabled) Log.Info("Wioski - wiesci (E7/W1): zapis - pamiec wojny " + memN + " wpisow (same identyfikatory; odrzucone przez BadId " + memBad + ")."); }
            catch { }
            // E7/R1 (235): sesje rabunku osada po osadzie (r:) i czujnosc wsi (c:) - jeden pisarz klucza arm_district (ta ksiega); starsze DLL te sekcje pomijaja
            try { DistrictRaid.Export(sb); } catch (Exception e) { Stumble("VillageBook.Export(r/c)", e); }
            return sb.ToString();
        }

        /// <summary>E7/R1 (235): liczba osad okregu n (zamrozona w ksiedze przy pierwszym odczycie - ta sama, co menu, dymek i lancuch palenia). false - ksiega
        /// nieczynna albo okreg nieznany (DistrictRaid liczy wtedy n z ludzi i DistrictSettlementPeople bez zamrazania). Nie rzuca.</summary>
        internal static bool DistrictN(Village v, out double n)
        {
            n = 0.0;
            if (v == null || !Active) return false;
            try
            {
                lock (Gate)
                {
                    Dist d;
                    if (!_byV.TryGetValue(v, out d) || !EnsureN(d)) return false;
                    n = d.N;
                    return n >= 1.0;
                }
            }
            catch (Exception e) { Stumble("VillageBook.DistrictN", e); return false; }
        }

        // ------------------------------------------------------------ n okregu i podzial na wioski (WM 3)
        private static bool EnsureN(Dist d)
        {
            if (d.NSet) return true;
            if (d.V == null || d.St == null) return false;
            PopulationLaw.EnsureCalibrated();
            float k = PopulationLaw.KFor(d.St);
            if (!(k > 0f) || float.IsInfinity(k)) return false;
            if (!_pSet)
            {
                var s = Settings.Current;
                _p = Math.Max(50, Math.Min(5000, s != null ? s.DistrictSettlementPeople : 250));
                _pSet = true;
            }
            double l0 = (double)Math.Max(0f, d.V.Hearth) * k + PeopleBook.RefugeesOf(d.V);
            if (double.IsNaN(l0) || double.IsInfinity(l0)) return false;
            d.N = Math.Max(1.0, Math.Round(l0 / _p));
            d.NSet = true;
            Split(d);
            LookTouch(d);                                   // E7/W4: okreg ma n - widok moze czytac wyglad jego wiosek
            _newN.Add(d.N); _newNames.Add(d.Name);
            return true;
        }

        private static void Split(Dist d)
        {
            d.Shrunk = SplitInto(d.N, d.F, d.W, d.Nw);
        }

        /// <summary>Podzial n okregu (WM 3): wioski po osadach z pliku f[0..W-1], wies gry reszta (nw[W]), nie mniej niz najwieksza wioska - inaczej wszystkie w tej samej
        /// proporcji (true = pomniejszone). Ten sam dla ukladu tej sesji (Split) i ukladu zapisu (ApplyMapped - stary udzial pustych osad).</summary>
        private static bool SplitInto(double n, double[] f, int W, double[] nw)
        {
            if (W == 0) { nw[0] = n; return false; }
            double sf = 0.0, mf = 0.0;
            for (int w = 0; w < W; w++) { sf += f[w]; if (f[w] > mf) mf = f[w]; }
            if (sf + mf <= n) { for (int w = 0; w < W; w++) nw[w] = f[w]; nw[W] = n - sf; return false; }
            double c = n / (sf + mf);
            for (int w = 0; w < W; w++) nw[w] = f[w] * c;
            nw[W] = mf * c;
            return true;
        }

        private static double Room(Dist d, int w)
        {
            double cap = Devastation.MaxShare(Settings.Current) * d.Nw[w];
            return cap - d.Br[w] - d.Bf[w];
        }

        private static double Wt(Dist d, int w)
        {
            if (w == d.W) return WMain;
            switch (d.Cls[w]) { case 'H': return WHub; case 'F': return WField; default: return WLine; }
        }

        // ------------------------------------------------------------ lancuch palenia (WM 6.2)
        /// <summary>Pierwsza wioska rabunku: min road_dist x (1 - 0.3 cos t) wsrod wiosek z miejscem (t - kat do strony wroga od wsi gry); wies gry na koncu.</summary>
        private static int PickFirst(Dist d, Sess s)
        {
            float mx = d.X[d.W], my = d.Y[d.W];
            int best = -1; double bs = double.MaxValue;
            for (int w = 0; w < d.W; w++)
            {
                if (Room(d, w) <= Eps || Shielded(d, w, s != null ? s.Fid : null) || Guarded(d, w)) continue;
                double cos = 0.0;
                if (s != null && s.HasFrom)
                {
                    double vx = d.X[w] - mx, vy = d.Y[w] - my, fx = s.Fx - mx, fy = s.Fy - my;
                    double nv = Math.Sqrt(vx * vx + vy * vy), nf = Math.Sqrt(fx * fx + fy * fy);
                    if (nv > 1e-6 && nf > 1e-6) cos = (vx * fx + vy * fy) / (nv * nf);
                }
                double sc = d.Rd[w] * (1.0 - DirWeight * cos);
                if (sc < bs) { bs = sc; best = w; }               // remis - pierwsza wedlug kolejnosci (indeksy wedlug order)
            }
            if (best >= 0) return best;
            return Room(d, d.W) > Eps ? d.W : -1;
        }

        /// <summary>Nastepna: najblizsza (x, y) wioska z miejscem do `from` (remis - kolejnosc); wies gry na koncu. m97-wioski: fid - bez wiosek chronionych okupem przed ta frakcja.</summary>
        private static int NextFrom(Dist d, int from, string fid = null)
        {
            int best = -1; double bd = double.MaxValue;
            for (int w = 0; w < d.W; w++)
            {
                if (w == from || Room(d, w) <= Eps || Shielded(d, w, fid) || Guarded(d, w)) continue;
                double dx = d.X[w] - d.X[from], dy = d.Y[w] - d.Y[from], dd = dx * dx + dy * dy;
                if (dd < bd) { bd = dd; best = w; }
            }
            if (best >= 0) return best;
            return from != d.W && Room(d, d.W) > Eps ? d.W : -1;
        }

        /// <summary>Zerowanie: najblizsza obozowi wioska z miejscem; wies gry na koncu.</summary>
        private static int NearestTo(Dist d, float x, float y)
        {
            int best = -1; double bd = double.MaxValue;
            for (int w = 0; w < d.W; w++)
            {
                if (Room(d, w) <= Eps) continue;
                double dx = d.X[w] - x, dy = d.Y[w] - y, dd = dx * dx + dy * dy;
                if (dd < bd) { bd = dd; best = w; }
            }
            if (best >= 0) return best;
            return Room(d, d.W) > Eps ? d.W : -1;
        }

        private static int FirstByOrder(Dist d)
        {
            for (int w = 0; w <= d.W; w++) if (Room(d, w) > Eps) return w;
            return -1;
        }

        private static Sess SessionFor(Dist d, object key, int today)
        {
            if (d.S != null && ReferenceEquals(d.S.Key, key)) return d.S;
            var s = new Sess { Key = key, Day = today };
            try
            {
                var me = d.St.Party != null ? d.St.Party.MapEvent : null;
                var lp = me != null && me.AttackerSide != null ? me.AttackerSide.LeaderParty : null;
                if (lp != null)
                {
                    if (lp.Name != null) s.Raider = lp.Name.ToString();
                    s.C = CxOf(lp.MobileParty, s.Raider, s);             // E7/W1 (232): identyfikatory napastnika raz na sesje
                    // strona wroga = najblizsza wsi warownia frakcji napastnika (raz na sesje); bandy i Inni bez twierdz - bez kierunku
                    var fac = lp.MapFaction;
                    if (fac != null) s.Fid = fac.StringId;   // m97-wioski: lancuch omija wioski, ktore zaplacily okup tej frakcji
                    try { s.Men = SideVillageVisit.PartyMen(lp.MobileParty); } catch { s.Men = 0; }   // m97-wioski krok 2: sila napastnika na starcie sesji (obroncy wiosek)
                    if (fac != null)
                    {
                        float mx = d.X[d.W], my = d.Y[d.W];
                        double bd = double.MaxValue;
                        foreach (var st in Settlement.All)
                        {
                            if (st == null || !st.IsFortification || st.MapFaction != fac) continue;
                            var p = st.GetPosition2D;
                            double dd = (p.x - mx) * (p.x - mx) + (p.y - my) * (p.y - my);
                            if (dd < bd) { bd = dd; s.Fx = p.x; s.Fy = p.y; s.HasFrom = true; }
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("VillageBook.SessionFor", e); }
            if (s.C == null) s.C = new Cx { Who = s.Raider, Tag = s, S = s };       // E7/W1: bez wodza po stronie napastnika - pamiec bez identyfikatorow
            // m97-wioski: rabunek wsi gry prowadzony przez gracza (DistrictRaid, klucz SideVillagesEnterable) pali tylko jej osady - ogien od razu na wsi gry, nie na wiosce
            s.Cur = s.C.Player && DistrictRaid.PlayerMainOnly ? (Room(d, d.W) > Eps ? d.W : -1) : PickFirst(d, s);
            d.S = s; d.SmokeIdx = -1;
            _dSess++;
            return s;
        }

        /// <summary>E7/W1 (232): napastnik uderzenia - identyfikatory bohatera-wodza i rodu (StringId), gracz, nazwa partii do komunikatu; Tag = zdarzenie pamieci.</summary>
        private static Cx CxOf(MobileParty mp, string who, Sess s)
        {
            var c = new Cx { Who = who ?? "?", S = s, Tag = s != null ? (object)s : mp };
            try
            {
                if (mp != null)
                {
                    var h = mp.LeaderHero; if (h != null && h.StringId != null) c.Hero = h.StringId;
                    var cl = mp.ActualClan; if (cl != null && cl.StringId != null) c.Clan = cl.StringId;
                    c.Player = mp.IsMainParty;
                }
            }
            catch (Exception e) { Stumble("VillageBook.CxOf", e); }
            return c;
        }

        /// <summary>
        /// E7/W1 (232, R14): pamiec wojny wioski (indeks w) przy uderzeniu - `put` spalonych osad x ludzi na osade (ten sam udzial co ksiega: trafieni x put / dB), zabici w proporcji
        /// uderzenia. Nowe zdarzenie: inny napastnik/sesja (Tag) albo przerwa > 1 doby (zerowanie tej samej armii po miesiacu); licznik spalen zostaje. Pod Gate, nie rzuca.
        /// </summary>
        private static void Remember(Dist d, int w, Cx cx, double put, int today)
        {
            var s = Settings.Current;
            if (cx == null || d.M == null || s == null || !s.VillageWarMemory) return;
            var m = d.M[w];
            // wpis z zapisu (Tag null) tego samego napastnika z tej albo poprzedniej doby - rabunek trwal przez zapis/wczytanie (sesje bez zapisu): to samo zdarzenie
            if (m != null && m.Tag == null && today - m.Day <= 1 && string.Equals(m.Hero, cx.Hero ?? "", StringComparison.Ordinal) && string.Equals(m.Clan, cx.Clan ?? "", StringComparison.Ordinal))
                m.Tag = cx.Tag;
            if (m == null || m.Tag == null || !ReferenceEquals(m.Tag, cx.Tag) || today - m.Day > 1)
            {
                if (m == null) { m = new Mem(); d.M[w] = m; }
                m.Tag = cx.Tag; m.Kind = 'P'; m.Fled = m.Dead = 0.0; m.Hero = cx.Hero ?? ""; m.Clan = cx.Clan ?? "";
                _nMemNew++;
            }
            double people = put * cx.Pps, dead = people * cx.DeadFrac;
            if (people > 0.0 && !double.IsInfinity(people)) { m.Fled += people - dead; m.Dead += dead; }
            m.Day = today;
        }

        private static void CheckUp(Dist d, int w, int today, string raider, bool list, Cx cx = null)
        {
            LookTouch(d);                                   // E7/W4 (237): kazdy wzrost pustych osad przechodzi tedy (Burn, FillFamine, Fallback) - okreg do odczytu przez widok
            double nw = d.Nw[w];
            if (!(nw > Eps)) return;
            if ((d.Flag[w] & FBurned) == 0 && d.Br[w] >= BurnShare * nw - 1e-12)
            {
                d.Flag[w] = FBurned; d.EvDay[w] = today; d.RebuiltDay[w] = -1; d.Event = true;
                if (list) { _dBurnedN++; if (_dBurnedList.Count < TopEvents) _dBurnedList.Add(d.VName[w] + " (" + d.Name + ") - " + (raider ?? "?")); }
                // E7/W1 (232, R10): JEDYNY punkt "burned" - przejscie na spalona w ksiedze przy uderzeniu (Burn z napastnikiem); pamiec 'B' + licznik spalen, wiadomosc do
                // kolejki (VillageNews w ticku - nie z wnetrza Strike). Przydzial zastepczy (stary zapis, inny uklad) i glod - bez wiadomosci (list false albo bez napastnika)
                if (list) _nBurned++;                                // = spalone dzis linii ksiegi (T5)
                if (list && cx != null)
                {
                    if (cx.S != null) cx.S.Burned = true;
                    var m = d.M != null ? d.M[w] : null;
                    if (m != null && ReferenceEquals(m.Tag, cx.Tag) && m.Day == today) { m.Kind = 'B'; m.Burns++; }
                    if (_news.Count < NewsMax) _news.Add(new BurnNews { V = d.V, VName = d.VName[w], DName = d.Name, Hero = cx.Hero, Who = cx.Who, Player = cx.Player, Day = today });
                    else _nNewsLost++;
                }
            }
            else if (d.Flag[w] == 0 && d.Br[w] < BurnShare * nw && d.Br[w] + d.Bf[w] >= BurnShare * nw - 1e-12)
            {
                d.Flag[w] = FAbandoned; d.EvDay[w] = today; d.RebuiltDay[w] = -1; d.Event = true;
                if (list) _dAbandonedN++;
            }
        }

        /// <summary>
        /// Spalenie osad lancuchem: rabunek (sesja - od biezacej wioski sesji), zerowanie (od obozu), bez zrodla (wedlug kolejnosci). Kazda wioska do pulapu; zwraca
        /// nadmiar (wszystko pelne). Punkt wpiecia etapu 7.11: rabunek osada po osadzie wola to samo z liczba osad kroku zamiast proporcji.
        /// </summary>
        private static double Burn(Dist d, double dB, bool raid, Sess s, bool hasPos, float px, float py, int today, string who, Cx cx = null)
        {
            // m97-wioski krok 2: lancuch rabunku (bez wymuszenia jednej wioski / wsi gry) zna sile napastnika - wioska z wiekszymi obroncami pominieta (Guarded)
            _chainMen = raid && s != null && ForceUid == null && !ForceMain ? s.Men : 0;
            try { return BurnCore(d, dB, raid, s, hasPos, px, py, today, who, cx); }
            finally { _chainMen = 0; }
        }

        /// <summary>m97-wioski krok 2: sila napastnika biezacego lancucha rabunku (0 - bez sprawdzania obroncow; ustawiane tylko na czas Burn, watek glowny pod Gate).</summary>
        private static int _chainMen;

        /// <summary>m97-wioski krok 2: obroncy wioski pobocznej w (wlasni ludzie w domu - SideVillageVisit.DefendersFor) wiecej niz napastnik biezacego lancucha - pominieta.</summary>
        private static bool Guarded(Dist d, int w)
        {
            if (_chainMen <= 0 || w < 0 || w >= d.W || d.Uid == null || d.Uid[w] == null || !SideVillageVisit.DefOn) return false;
            double home, away;
            People(d, SnapOf(d), w, out home, out away);
            if (SideVillageVisit.DefendersFor(d.Uid[w], home) <= _chainMen) return false;
            SideVillageVisit.NoteChainSkip(d.Uid[w]);
            return true;
        }

        private static double BurnCore(Dist d, double dB, bool raid, Sess s, bool hasPos, float px, float py, int today, string who, Cx cx)
        {
            int cur;
            int forced = Forced(d);   // m97-wioski: rabunek jednej wioski pobocznej (SideVillageVisit) albo wsi gry przez gracza (DistrictRaid) - tylko jej osady, bez lancucha (-2 = bez wymuszenia)
            string fid = raid && s != null ? s.Fid : null;   // m97-wioski: lancuch rabunku omija wioski chronione okupem przed frakcja napastnika
            if (forced != -2) cur = forced;
            else if (raid && s != null) cur = s.Cur >= 0 && Room(d, s.Cur) > Eps && !Shielded(d, s.Cur, fid) ? s.Cur : (s.Cur < 0 ? PickFirst(d, s) : NextFrom(d, s.Cur, fid));
            else cur = hasPos ? NearestTo(d, px, py) : FirstByOrder(d);
            double left = dB;
            int guard = 0;
            while (left > Eps && cur >= 0 && guard++ <= d.W + 1)
            {
                double put = Math.Min(Room(d, cur), left);
                if (put > 0.0)
                {
                    if (_chainMen > 0 && cur < d.W && d.Uid != null && d.Uid[cur] != null && s != null)
                    {
                        // m97-wioski krok 2: obroncy tej wioski pobici przez wiekszego napastnika - raz na sesje do pamieci poleglych (bez drugiego ubytku ludzi); ludzie w domu
                        // PRZED paleniem tej porcji (recenzja: blok stal miedzy if (raid) a jego else - else przypial sie do niego i rabunek liczyl sie tez jako zerowanie)
                        if (s.DefHit == null) s.DefHit = new HashSet<string>(StringComparer.Ordinal);
                        if (s.DefHit.Add(d.Uid[cur]))
                        {
                            double h0, a0;
                            People(d, SnapOf(d), cur, out h0, out a0);
                            SideVillageVisit.NoteChainBurn(d.Uid[cur], h0, _chainMen);
                        }
                    }
                    d.Br[cur] += put; left -= put;
                    if (raid) { if (s != null) s.Cur = cur; if (cur == d.W) _dRaidMain += put; else _dRaidW += put; }
                    else { if (cur == d.W) _dForMain += put; else _dForW += put; }
                    Remember(d, cur, cx, put, today);                   // E7/W1 (232): pamiec wojny PRZED CheckUp (przejscie na spalona oznacza to samo zdarzenie 'B')
                    CheckUp(d, cur, today, raid && s != null ? s.Raider : who, true, cx);
                }
                if (left <= Eps || forced != -2) break;
                cur = NextFrom(d, cur, fid);
            }
            return left > Eps ? left : 0.0;
        }

        /// <summary>m97-wioski: uid wioski pobocznej, ktorej osady pali biezace uderzenie (SideVillageVisit.StrikeChunk ustawia i zdejmuje w try/finally, watek glowny).</summary>
        internal static string ForceUid;

        /// <summary>m97-wioski: biezace uderzenie pali tylko osady wsi gry (DistrictRaid.StrikeChunk sesji gracza przy SideVillagesEnterable - try/finally, watek glowny).</summary>
        internal static bool ForceMain;

        /// <summary>m97-wioski: indeks wioski ForceUid w okregu albo wsi gry (ForceMain); -1 - brak albo pelna (nic nie spalone, nadmiar w linii), -2 - bez wymuszenia (lancuch jak dotad).</summary>
        private static int Forced(Dist d)
        {
            string u = ForceUid;
            if (u == null) return ForceMain ? (Room(d, d.W) > Eps ? d.W : -1) : -2;
            if (d.Uid != null) for (int w = 0; w < d.W; w++) if (string.Equals(d.Uid[w], u, StringComparison.Ordinal)) return Room(d, w) > Eps ? w : -1;
            return -1;
        }

        /// <summary>m97-wioski: wioska poboczna w chroniona okupem przed frakcja fid (SideVillageVisit - migawka, bez alokacji). Wies gry (w == W) - nigdy (jej ochrona to p6).</summary>
        private static bool Shielded(Dist d, int w, string fid)
        {
            return fid != null && w >= 0 && w < d.W && d.Uid != null && d.Uid[w] != null && SideVillageVisit.Spared(d.Uid[w], fid);
        }

        /// <summary>
        /// m97-wioski: miejsce (osady do pulapu, Room) i pulap (DevastationMaxPercent x osady) czesci okregu, ktore napastnik moze palic: mainOnly - sama wies gry (rabunek gracza
        /// przy SideVillagesEnterable), fid - bez wiosek chronionych okupem przed ta frakcja; n - osady okregu (ta sama miara co OnStrike: dB = trafieni x n / ludzie okregu).
        /// false - nic nie wykluczone (albo ksiega nieczynna, okreg nieznany) - wolajacy liczy jak dotad. Sam odczyt pod Gate, nie rzuca.
        /// </summary>
        internal static bool RoomFor(Village v, bool mainOnly, string fid, out double room, out double cap, out double n)
        {
            room = cap = n = 0.0;
            if (v == null || !Active || (!mainOnly && (fid == null || !SideVillageVisit.AnyProt))) return false;   // bez ochron - od razu (AI bez zmian, bez blokady)
            try
            {
                lock (Gate)
                {
                    Dist d;
                    if (!_byV.TryGetValue(v, out d) || d == null || !EnsureN(d) || d.Nw == null) return false;
                    double ms = Devastation.MaxShare(Settings.Current);
                    bool cut = mainOnly;
                    for (int w = 0; w <= d.W; w++)
                    {
                        if (w < d.W && (mainOnly || Shielded(d, w, fid))) { cut = true; continue; }
                        room += Math.Max(0.0, Room(d, w)); cap += ms * Math.Max(0.0, d.Nw[w]);
                    }
                    n = d.N;
                    return cut && n > 0.0;
                }
            }
            catch (Exception e) { Stumble("VillageBook.RoomFor", e); return false; }
        }

        /// <summary>m97-wioski: miejsce (osady do pulapu) jednej wioski pobocznej i osady okregu - rabunek tej wioski (SideVillageVisit) obcina uderzenie do jej miejsca.
        /// false - ksiega nieczynna albo wioska nieznana. Sam odczyt pod Gate, nie rzuca.</summary>
        internal static bool UidRoom(Village v, string uid, out double room, out double n)
        {
            room = n = 0.0;
            if (v == null || uid == null || !Active) return false;
            try
            {
                lock (Gate)
                {
                    Dist d;
                    if (!_byV.TryGetValue(v, out d) || d == null || !EnsureN(d) || d.Uid == null) return false;
                    for (int w = 0; w < d.W; w++)
                        if (string.Equals(d.Uid[w], uid, StringComparison.Ordinal)) { room = Math.Max(0.0, Room(d, w)); n = d.N; return n > 0.0; }
                    return false;
                }
            }
            catch (Exception e) { Stumble("VillageBook.UidRoom", e); return false; }
        }

        // ------------------------------------------------------------ wpiecia w nasz kod (bez Harmony)
        /// <summary>
        /// Devastation.Strike PO kontach (U24; Z1, Z2): `hit` trafionych (po f), `refugees` dopisanych na konto, `lBefore` - ludzie okregu przed ruchem. Rabunek - `comp` =
        /// RaidEventComponent (klucz sesji, ten sam co MapEvent.Component widoku - krytyka K2); zerowanie - `src` (oboz armii). Nie rzuca.
        /// E7/W1 (232): `dead` - zabici tego uderzenia (z tego samego Strike, ktory dopisuje RaidState.Dead) - tylko do pamieci wojny wioski.
        /// </summary>
        internal static void OnStrike(Village v, double hit, double refugees, double dead, bool raid, object comp, MobileParty src, double lBefore)
        {
            if (v == null || !(hit > 0.0) || double.IsInfinity(hit)) return;
            try
            {
                if (!Active) { _dHitSkip += hit; return; }
                lock (Gate)
                {
                    Dist d;
                    if (!_byV.TryGetValue(v, out d) || !EnsureN(d) || !(lBefore > 0.0)) { _dHitSkip += hit; return; }     // E4 fixB (G4): po obiekcie Village
                    int today = Today();
                    double dB = hit * d.N / lBefore;
                    if (refugees > 0.0 && !double.IsInfinity(refugees)) d.ARexp += refugees;
                    double perS = lBefore / d.N;                     // ludzi na osade przy tym uderzeniu
                    double deadFrac = dead > 0.0 && !double.IsInfinity(dead) ? Math.Min(1.0, dead / hit) : 0.0;
                    double over;
                    if (raid && comp != null)
                    {
                        var ss = SessionFor(d, comp, today);
                        if (ss.C != null) { ss.C.Pps = perS; ss.C.DeadFrac = deadFrac; }   // E7/W1: liczby tego uderzenia
                        over = Burn(d, dB, true, ss, false, 0f, 0f, today, null, ss.C);
                    }
                    else
                    {
                        bool hasPos = false; float x = 0f, y = 0f; string who = "zerowanie armii"; Cx cx = null;
                        if (src != null)
                        {
                            try { var p = src.GetPosition2D; x = p.x; y = p.y; hasPos = true; if (src.Name != null) who = "zerowanie armii (" + src.Name + ")"; }
                            catch (Exception e) { Stumble("VillageBook.OnStrike(oboz)", e); }
                            string nm = null;
                            try { nm = src.Name != null ? src.Name.ToString() : null; } catch { nm = null; }
                            cx = CxOf(src, nm, null);                   // E7/W1: zdarzenie pamieci = ta armia (Tag), wodz armii w komunikacie
                            cx.Pps = perS; cx.DeadFrac = deadFrac;
                        }
                        over = Burn(d, dB, false, null, hasPos, x, y, today, who, cx);
                    }
                    _dOver += over; _dOverPeople += over * perS; _dSetS += (dB - over) * perS;
                    _dStrikes++;
                }
            }
            catch (Exception e) { Stumble("VillageBook.OnStrike", e); }
        }

        /// <summary>Famine.TryFlee na koncu udanej sciezki (Z4, po kontach): n ludzi uciekla z glodu - opuszczone osady od ubocza. Nie rzuca.</summary>
        internal static void OnFamineFled(Village v, double n)
        {
            if (v == null || !(n > 0.0) || double.IsInfinity(n)) return;
            try
            {
                if (!Active) return;
                lock (Gate)
                {
                    Dist d;
                    if (!_byV.TryGetValue(v, out d) || !EnsureN(d)) { _dFamSkip++; return; }     // E4 fixB (G4): po obiekcie Village
                    float k = PopulationLaw.KFor(d.St);
                    double l = (k > 0f ? (double)Math.Max(0f, v.Hearth) * k : 0.0) + PeopleBook.RefugeesOf(v);     // po ruchu (Move + FamineFled)
                    if (!(l > 0.0)) { _dFamSkip++; return; }
                    double dB = n * d.N / l;
                    d.AFexp += n;
                    double left = FillFamine(d, dB, Today(), true);
                    _dFam += dB - left; _dOver += left;
                }
            }
            catch (Exception e) { Stumble("VillageBook.OnFamineFled", e); }
        }

        /// <summary>
        /// E4/9 Z11 (krytyka K7): punkt wpiecia dla ThrallLaw po PeopleBook.RefugeesToCaptives (wlasny try, po kontach) - pojmani schodza z konta UCHODZCY,
        /// ale nie wracaja do domu: tylko oczekiwane konto w dol, puste osady bez zmian (pojmani jak zabici - sa w osadach spalonych). Wola ThrallLaw.CapturePrefix (E4/9).
        /// </summary>
        internal static void OnRefugeesTaken(Village v, double taken)
        {
            if (v == null || !(taken > 0.0) || double.IsInfinity(taken)) return;
            try
            {
                if (!Active) return;
                lock (Gate)
                {
                    Dist d;
                    if (!_byV.TryGetValue(v, out d)) return;     // E4 fixB (G4): po obiekcie Village
                    d.ARexp = Math.Max(0.0, d.ARexp - taken);
                }
            }
            catch (Exception e) { Stumble("VillageBook.OnRefugeesTaken", e); }
        }

        private static double FillFamine(Dist d, double dB, int today, bool list)
        {
            double left = dB;
            foreach (int w in d.FamOrder)
            {
                if (left <= Eps) break;
                double put = Math.Min(Room(d, w), left);
                if (!(put > 0.0)) continue;
                d.Bf[w] += put; left -= put;
                CheckUp(d, w, today, null, list);
            }
            return left > Eps ? left : 0.0;
        }

        // ------------------------------------------------------------ uzgodnienie doby (3.5) - jedyny spadek pustych osad
        private static double Remove(Dist d, double[] b, double x)
        {
            if (x > 1e-12) LookTouch(d);                    // E7/W4 (237): jedyny spadek pustych osad - okreg do odczytu przez widok
            double left = x;
            for (int pass = 0; pass < 4 && left > 1e-12; pass++)
            {
                double ws = 0.0;
                for (int i = 0; i < b.Length; i++) if (b[i] > 0.0) ws += b[i] * Wt(d, i);
                if (!(ws > 0.0)) break;
                double take = left;
                for (int i = 0; i < b.Length; i++)
                {
                    if (!(b[i] > 0.0)) continue;
                    double t = take * b[i] * Wt(d, i) / ws;
                    if (t > b[i]) t = b[i];
                    b[i] -= t; left -= t;
                }
            }
            if (left > 1e-12)
            {
                double tot = Sum(b);
                if (tot > 0.0) { double f = Math.Min(1.0, left / tot); for (int i = 0; i < b.Length; i++) { double t = b[i] * f; b[i] -= t; left -= t; } }
            }
            for (int i = 0; i < b.Length; i++) if (b[i] < 1e-9) b[i] = 0.0;
            return x - Math.Max(0.0, left);
        }

        /// <summary>Wolne miejsce okregu do pulapu (suma dodatnich Room po wszystkich wioskach i wsi gry).</summary>
        private static double FreeRoom(Dist d)
        {
            double r = 0.0;
            for (int w = 0; w <= d.W; w++) { double x = Room(d, w); if (x > 0.0) r += x; }
            return r;
        }

        /// <summary>
        /// Przydzial zastepczy (3.7): nadwyzka konta bez zrodla w ksiedze (stary zapis, inny uklad, wlaczenie klucza, doplyw nieznany) albo konto znane umieszczone
        /// pozniej (doplyw przyszedl, gdy okreg byl na pulapie). countOver false - reszta ponad pulap byla juz policzona w nadmiarze w dobie doplywu.
        /// </summary>
        private static double Fallback(Dist d, double surplus, bool raidPart, Snap sn, Settings s, int today, bool countOver)
        {
            double l = sn.Dom + sn.A;
            if (!(surplus > 0.0) || !(l > 0.0)) return 0.0;
            double dB;
            if (raidPart)
            {
                double share = 1.0 - (Math.Max(0, s.DevastationKilledPercent) + Math.Max(0, s.DevastationOutlawBasePercent)) / 100.0;   // WM 7.2 "T0": zabici i las tez spalili osady
                dB = surplus * d.N / (l * Math.Max(0.5, share));
                int ev = today;
                try { int last; if (PeopleBook.LastStrike(OutlawLaw.RegionFor(d.St), out last)) ev = last; } catch (Exception e) { Stumble("VillageBook.Fallback(zegar)", e); }
                double left = dB;
                for (int w = 0; w <= d.W && left > Eps; w++)            // wzdluz kolejnosci od 0, wies gry na koncu (bez kierunku)
                {
                    double put = Math.Min(Room(d, w), left);
                    if (!(put > 0.0)) continue;
                    d.Br[w] += put; left -= put;
                    CheckUp(d, w, ev, "przydzial zastepczy", false);
                }
                if (countOver) _dOver += left;
                return dB - left;
            }
            dB = surplus * d.N / l;
            double rest = FillFamine(d, dB, today, false);
            if (countOver) _dOver += rest;
            return dB - rest;
        }

        private static Snap SnapOf(Dist d)
        {
            var sn = new Snap();
            float k = PopulationLaw.KFor(d.St);
            sn.Dom = k > 0f && !float.IsInfinity(k) ? (double)Math.Max(0f, d.V.Hearth) * k : 0.0;
            sn.A = PeopleBook.RefugeesOf(d.V);
            sn.Af = Math.Min(sn.A, Math.Max(0.0, Famine.FamPartOf(d.V, sn.A)));
            sn.Ar = Math.Max(0.0, sn.A - sn.Af);
            sn.Sbr = Sum(d.Br); sn.Sbf = Sum(d.Bf); sn.B = sn.Sbr + sn.Sbf;
            sn.Y = (double)Devastation.YieldFactor(d.V) * FreeHands.YieldFactor(d.V);
            if (double.IsNaN(sn.Y) || sn.Y < 0.0) sn.Y = 1.0;
            if (SideVillageVisit.LackAny) LackOf(d, ref sn);
            return sn;
        }

        /// <summary>m97-wioski krok 2 (recenzja: polegli obroncy maja zejsc z ludzi TEJ wioski): brak rak wiosek okregu po bitwie z obroncami. Ksiega nie trzyma ludzi
        /// wioski osobno (DOM okregu x stojace osady), wiec polegli zdjeci z DOM rozkladaliby sie na caly okreg; brak rak przesuwa ich udzial z powrotem na te wioske:
        /// w domu wioski w = udzial w - Lk[w] + udzial w x suma (j != w) Lk[j] / (Dom - udzial j) - suma po okregu = Dom (nikt z niczego). Lk[w] najwyzej udzial w.
        /// Zero kosztu bez bitew (LackAny). Sam odczyt, nie rzuca.</summary>
        private static void LackOf(Dist d, ref Snap sn)
        {
            try
            {
                if (d.Uid == null || d.W <= 0 || d.Nw == null) return;
                double denom = d.N - sn.B;
                if (!(denom > Eps) || !(sn.Dom > Eps)) return;
                for (int w = 0; w < d.W; w++)
                {
                    if (d.Uid[w] == null) continue;
                    double lk = SideVillageVisit.LackNow(d.Uid[w]);
                    if (!(lk > Eps)) continue;
                    double bw = Math.Max(0.0, sn.Dom * (d.Nw[w] - d.Br[w] - d.Bf[w]) / denom), rest = sn.Dom - bw;
                    lk = Math.Min(lk, bw);
                    if (!(lk > Eps) || !(rest > Eps)) continue;
                    if (sn.Lk == null) sn.Lk = new double[d.W + 1];
                    sn.Lk[w] = lk; sn.LkT += lk / rest;
                }
            }
            catch { sn.Lk = null; sn.LkT = 0.0; }
        }

        private static void Reconcile(Dist d, Settings s, int today)
        {
            var sn = SnapOf(d);
            bool zast = false, unk = false;
            // czesc z rabunku i zerowania
            double sbr = sn.Sbr;
            if (sn.Ar <= 1e-6)
            {
                // konto puste - powrot "ostatni czlowiek caly" i smierc zeruja konto co do 1e-9 (krytyka K6: bez progu 0.5)
                if (sbr > 0.0) { _dUp += sbr; Array.Clear(d.Br, 0, d.Br.Length); }
                d.ARexp = 0.0;
            }
            else
            {
                double tol = Math.Max(0.5, 1e-3 * d.ARexp);
                if (sbr <= Eps || sn.Ar > d.ARexp + tol)
                {
                    // poprawka po recenzji (R1-4): pula pusta przy koncie w granicach oczekiwanego (known) = doplyw z OnStrike, ktory nie znalazl miejsca (okreg na
                    // pulapie, nadmiar policzony w dobie doplywu) - to nazwany nadmiar (K9), nie doplyw bez zrodla: bez miejsca nic nie robimy, po zwolnieniu pulapu
                    // umieszczamy bez d0 i bez drugiego nadmiaru
                    bool known = d.Z == ZNone && d.ARexp > Eps && sn.Ar <= d.ARexp + tol;
                    if (!(known && FreeRoom(d) <= Eps))
                    {
                        double surplus = sbr <= Eps ? sn.Ar : sn.Ar - d.ARexp;
                        double got = Fallback(d, surplus, true, sn, s, today, !known);
                        _dZast += got; d.Event = true; zast = true;
                        if (!known) unk = true;
                    }
                }
                else if (sn.Ar < d.ARexp)
                {
                    double frac = 1.0 - sn.Ar / d.ARexp;
                    _dUp += Remove(d, d.Br, frac * sbr);
                }
                d.ARexp = sn.Ar;
            }
            // czesc z glodu (lustro FamN E4/6, recenzja R1-1)
            double sbf = Sum(d.Bf);
            if (sn.Af <= 1e-6)
            {
                if (sbf > 0.0) { _dUp += sbf; Array.Clear(d.Bf, 0, d.Bf.Length); }
                d.AFexp = 0.0;
            }
            else
            {
                double tol = Math.Max(0.5, 1e-3 * d.AFexp);
                if (sbf <= Eps || sn.Af > d.AFexp + tol)
                {
                    bool known = d.Z == ZNone && d.AFexp > Eps && sn.Af <= d.AFexp + tol;     // R1-4 jak wyzej (OnFamineFled bez miejsca)
                    if (!(known && FreeRoom(d) <= Eps))
                    {
                        double surplus = sbf <= Eps ? sn.Af : sn.Af - d.AFexp;
                        double got = Fallback(d, surplus, false, SnapOf(d), s, today, !known);
                        _dZast += got; d.Event = true; zast = true;
                        if (!known) unk = true;
                    }
                }
                else if (sn.Af < d.AFexp)
                {
                    double frac = 1.0 - sn.Af / d.AFexp;
                    _dUp += Remove(d, d.Bf, frac * sbf);
                }
                d.AFexp = sn.Af;
            }
            // przydzial zastepczy oczekiwany (stary zapis, inny uklad, wlaczenie) albo doplyw bez zrodla (d0 - oczekiwane 0) - liczone na okreg
            // R1-4: konto znane umieszczone dopiero po zwolnieniu pulapu - osobny licznik (nie d0)
            if (zast) { if (d.Z != ZNone) _dZastWhy[d.Z]++; else if (unk) _dD0++; else _dLate++; }
        }

        // ------------------------------------------------------------ odczyty (3.6) - pod Gate, nie rzucaja
        private static void People(Dist d, Snap sn, int w, out double home, out double away)
        {
            double denom = d.N - sn.B;
            home = denom > Eps ? sn.Dom * (d.Nw[w] - d.Br[w] - d.Bf[w]) / denom : (w == d.W ? sn.Dom : 0.0);
            if (sn.Lk != null && denom > Eps)
            {
                // m97-wioski krok 2: brak rak wiosek po bitwie z obroncami (LackOf) - ta wioska mniej, reszta okregu wiecej o tyle samo
                double own = sn.Lk[w], rest = sn.Dom - home;
                double t = sn.LkT - (own > 0.0 && rest > Eps ? own / rest : 0.0);
                home = Math.Max(0.0, home - own + home * t);
            }
            away = (sn.Sbr > Eps ? sn.Ar * d.Br[w] / sn.Sbr : (w == d.W ? sn.Ar : 0.0)) + (sn.Sbf > Eps ? sn.Af * d.Bf[w] / sn.Sbf : (w == d.W ? sn.Af : 0.0));
        }

        private static int StateAt(Dist d, int w)
        {
            double nw = d.Nw[w], b = d.Br[w] + d.Bf[w];
            bool raid = false;
            try { raid = d.S != null && d.S.Cur == w && d.St.IsUnderRaid; } catch { raid = false; }
            if (raid) return SBurning;
            if (BurnedAt(d, w)) return SBurned;
            if ((d.Flag[w] & FAbandoned) != 0 && b >= KeepShare * nw) return SAbandoned;
            if (d.Flag[w] != 0) return SRebuilding;
            if (b < StandShare * nw) return SStanding;
            return SDamaged;
        }

        /// <summary>Stan wioski po uid (dymek widoku, etap 7). false - ksiega nieczynna, wioska nieznana albo okreg bez n.</summary>
        internal static bool StateOf(string uid, out VState st)
        {
            st = default(VState);
            if (uid == null || !Active) return false;
            try
            {
                lock (Gate)
                {
                    KeyValuePair<Dist, int> kv;
                    if (!_byUid.TryGetValue(uid, out kv) || kv.Key == null || !kv.Key.NSet) return false;
                    var d = kv.Key; int w = kv.Value;
                    var sn = SnapOf(d);
                    double home, away;
                    People(d, sn, w, out home, out away);
                    st.State = StateAt(d, w); st.Home = home; st.Away = away; st.Settlements = d.Nw[w]; st.Empty = d.Br[w] + d.Bf[w];
                    st.Share = sn.Dom > 0.0 ? sn.Y * home / sn.Dom : 0.0;
                    st.EvDay = d.EvDay[w]; st.RebuiltDay = d.RebuiltDay[w]; st.Today = Today();
                    st.Raider = d.S != null ? d.S.Raider : null;
                    return true;
                }
            }
            catch (Exception e) { Stumble("VillageBook.StateOf", e); return false; }
        }

        // ------------------------------------------------------------ E7/W4 (237, R10/R17): wyglad wioski dla widoku mapy
        /// <summary>Spalona w ksiedze (R10 - JEDNA definicja, ta sama w StateAt): flaga spalenia (przejscie przy 50% osad w CheckUp) i spalone osady nie ponizej 47%
        /// (histereza). Rabunek w toku jej nie zdejmuje - wioska moze plonac i byc spalona naraz (WM 6.3).</summary>
        private static bool BurnedAt(Dist d, int w)
        {
            return (d.Flag[w] & FBurned) != 0 && d.Br[w] >= KeepShare * d.Nw[w];
        }

        /// <summary>
        /// Jeff 16.10 (D4 "czy dziala palenie wiosek"): spalone w CALEJ ksiedze (BurnedAt - ta sama definicja co wyglad) osobno wioski poboczne i wsie gry. Linia wygladu
        /// "Wioski - wyglad (E7/W4)" liczyla dotad tylko wioski, ktorych wyglad widok juz odczytal (obrazek postawiony od wczytania mapy - przy kamerze), a linia ksiegi
        /// liczy wszystkie wioski i wsie gry (wies gry spalona nie ma obrazka Armoury) - stad "ksiega 3 spalone, wyglad 0" w logu Jeffa 16.10 przy kamerze daleko od
        /// spalonych. Sam odczyt pod Gate (co 2 minuty), nie rzuca.
        /// </summary>
        internal static bool BurnedCounts(out int side, out int main)
        {
            side = main = 0;
            if (!Active) return false;
            try
            {
                lock (Gate)
                    foreach (var d in _d.Values)
                    {
                        if (!d.NSet) continue;
                        for (int w = 0; w <= d.W; w++) if (BurnedAt(d, w)) { if (w == d.W) main++; else side++; }
                    }
                return true;
            }
            catch (Exception e) { Stumble("VillageBook.BurnedCounts", e); return false; }
        }

        /// <summary>Okreg do odczytu wygladu przez widok mapy (raz do zabrania listy). Wolane pod Gate z miejsc, ktore zmieniaja puste osady albo flagi.</summary>
        private static void LookTouch(Dist d)
        {
            if (d == null) return;
            lock (_lookLock) { if (!d.LookDirty) { d.LookDirty = true; _lookDirty.Add(d); } }
            System.Threading.Interlocked.Increment(ref _lookVer);
        }

        private static void LookTouchAll()
        {
            lock (_lookLock) { foreach (var d in _lookDirty) d.LookDirty = false; _lookDirty.Clear(); _lookAll = true; }
            System.Threading.Interlocked.Increment(ref _lookVer);
        }

        /// <summary>Numer zmiany wygladu (rosnie przy kazdej zmianie pustych osad albo flag) - widok porownuje go bez blokady i dopiero przy nowym zabiera liste.</summary>
        internal static int LookVersion { get { return System.Threading.Volatile.Read(ref _lookVer); } }

        /// <summary>Widok mapy: id okregow ze zmiana od poprzedniego zabrania (all - wszystko od nowa: nowa kampania, wczytanie). Nie rzuca.</summary>
        internal static void TakeLookDirty(List<string> into, out bool all)
        {
            all = false;
            try
            {
                lock (_lookLock)
                {
                    all = _lookAll; _lookAll = false;
                    foreach (var d in _lookDirty) { d.LookDirty = false; if (!all && d.Id != null) into.Add(d.Id); }
                    _lookDirty.Clear();
                }
            }
            catch (Exception e) { all = true; Stumble("VillageBook.TakeLookDirty", e); }
        }

        /// <summary>Wyglad wioski po uid: osady wioski, puste osady (rabunek + glod) i czy spalona w ksiedze (BurnedAt). Sam odczyt tablic pod Gate - bez kont ludzi
        /// (tanszy niz StateOf). false - ksiega nieczynna, wioska nieznana albo okreg bez n (widok pokazuje wtedy obrazek z pliku).</summary>
        internal static bool LookOf(string uid, out double settlements, out double empty, out bool burned)
        {
            settlements = empty = 0.0; burned = false;
            if (uid == null || !Active) return false;
            try
            {
                lock (Gate)
                {
                    KeyValuePair<Dist, int> kv;
                    if (!_byUid.TryGetValue(uid, out kv) || kv.Key == null || !kv.Key.NSet) return false;
                    var d = kv.Key; int w = kv.Value;
                    settlements = d.Nw[w]; empty = d.Br[w] + d.Bf[w];
                    if (!(settlements > Eps)) return false;
                    burned = BurnedAt(d, w);
                    return true;
                }
            }
            catch (Exception e) { Stumble("VillageBook.LookOf", e); return false; }
        }

        /// <summary>KS (kalibracja startu): udzialy osad okregu - wioski poboczne (uid -> osady wioski / osady okregu) i wies gry (main). Udzial z osad (Nw), nie z ludzi
        /// w domu: ubytek rak (uchodzcy) robi czynnik E4/5 w modelu produkcji. false - ksiega nieczynna, wies nieznana albo okreg bez n. Sam odczyt pod Gate, nie rzuca.</summary>
        internal static bool KsShares(Village v, Dictionary<string, double> side, out double main)
        {
            main = 1.0;
            if (side != null) side.Clear();
            if (v == null || side == null || !Active) return false;
            try
            {
                lock (Gate)
                {
                    Dist d;
                    if (!_byV.TryGetValue(v, out d) || d == null || !EnsureN(d) || d.Nw == null || d.Uid == null) return false;
                    double n = 0.0;
                    for (int w = 0; w <= d.W; w++) n += Math.Max(0.0, d.Nw[w]);
                    if (!(n > Eps)) return false;
                    for (int w = 0; w < d.W; w++) if (d.Uid[w] != null) side[d.Uid[w]] = Math.Max(0.0, d.Nw[w]) / n;
                    main = Math.Max(0.0, d.Nw[d.W]) / n;
                    return true;
                }
            }
            catch (Exception e) { Stumble("VillageBook.KsShares", e); return false; }
        }

        /// <summary>E6/W2 (210): udzial ludzi W DOMU wiosek rybackich w ludziach w domu okregu tej wsi gry (People: dom wioski ~ osady - puste, ten sam mianownik
        /// dla calego okregu). 0 - ksiega nieczynna, wies nieznana, okreg bez wiosek albo bez n. Sam odczyt pod Gate, nie rzuca (lista produkcji BK).</summary>
        internal static double FishersShare(Village v)
        {
            if (v == null || !Active) return 0.0;
            try
            {
                lock (Gate)
                {
                    Dist d;
                    if (!_byV.TryGetValue(v, out d) || d == null || d.W == 0 || !d.NSet || d.Fish == null) return 0.0;
                    double all = 0.0, fish = 0.0;
                    for (int w = 0; w <= d.W; w++)
                    {
                        double h = Math.Max(0.0, d.Nw[w] - d.Br[w] - d.Bf[w]);
                        all += h;
                        if (d.Fish[w]) fish += h;
                    }
                    return all > Eps ? fish / all : 0.0;
                }
            }
            catch (Exception e) { Stumble("VillageBook.FishersShare", e); return 0.0; }
        }

        /// <summary>inwest (noc 17/18.10, prace okregu i zajecia wiosek): czesc okregu - wioska poboczna albo wies gry (Main, Uid null): nazwa, polozenie (Kind z pliku
        /// ukladu), model obrazka (Fish), osady (Nw), puste z rabunku (Br) i z glodu (Bf), ludzie w domu i poza domem (ten sam podzial co menu i dymek), stan StateAt.</summary>
        internal sealed class PartView { public string Uid, Name, Kind; public bool Main, Fish; public double Nw, Br, Bf, Home, Away; public int State; }

        /// <summary>inwest: czesci okregu tej wsi gry (wioski poboczne w kolejnosci ksiegi, wies gry na koncu). false - ksiega nieczynna, wies nieznana albo okreg bez n.
        /// Sam odczyt pod Gate (EnsureN jak menu), nie rzuca.</summary>
        internal static bool PartsOf(Village v, List<PartView> into)
        {
            if (into != null) into.Clear();
            if (v == null || into == null || !Active) return false;
            try
            {
                lock (Gate)
                {
                    Dist d;
                    if (!_byV.TryGetValue(v, out d) || d == null || !EnsureN(d) || d.Nw == null || d.Uid == null) return false;
                    var sn = SnapOf(d);
                    for (int w = 0; w <= d.W; w++)
                    {
                        double home, away;
                        People(d, sn, w, out home, out away);
                        into.Add(new PartView
                        {
                            Uid = w == d.W ? null : d.Uid[w], Name = d.VName[w], Kind = d.Kind[w] ?? "", Main = w == d.W, Fish = d.Fish != null && d.Fish[w],
                            Nw = Math.Max(0.0, d.Nw[w]), Br = Math.Max(0.0, d.Br[w]), Bf = Math.Max(0.0, d.Bf[w]), Home = Math.Max(0.0, home), Away = Math.Max(0.0, away), State = StateAt(d, w)
                        });
                    }
                    return true;
                }
            }
            catch (Exception e) { into.Clear(); Stumble("VillageBook.PartsOf", e); return false; }
        }

        /// <summary>inwest: miejsce wioski w zdaniu ("by the river") - ten sam tekst co wiersz okna okregu; "" - bez miejsca.</summary>
        internal static string PlaceOf(string kind) { try { return Place(kind ?? ""); } catch { return ""; } }

        /// <summary>
        /// Widok (PollRaids, 4 razy na sekunde - sam odczyt, gdy sesja otwarta): wioska z ogniem (fire, wies gry IsUnderRaid) albo dymem (czujnosc, IsRaided). Brak sesji
        /// przy rabunku - OTWIERA ja ta sama funkcja wyboru co Strike (krytyka K2: wybor ten sam przy widoku wlaczonym i wylaczonym, b sie nie zmienia do pierwszego Strike).
        /// true - ksiega prowadzi ogien tej wsi (uid null = plonie sama wies gry albo zadna wioska); false - widok po staremu (lancuch gry).
        /// </summary>
        internal static bool FxUid(string districtId, bool fire, out string uid)
        {
            uid = null;
            if (districtId == null || !Active) return false;
            try
            {
                lock (Gate)
                {
                    Dist d;
                    if (!_d.TryGetValue(districtId, out d) || d.W == 0 || !EnsureN(d)) return false;
                    int idx;
                    if (fire)
                    {
                        var me = d.St.Party != null ? d.St.Party.MapEvent : null;
                        object key = me != null ? me.Component : null;
                        if (key == null) return false;
                        idx = SessionFor(d, key, Today()).Cur;
                    }
                    else
                    {
                        idx = d.S != null ? d.S.Cur : d.SmokeIdx;
                        if (idx < 0)
                        {
                            // dym bez sesji (np. po wczytaniu - sesje bez zapisu): najbardziej spalona wioska
                            double best = 0.0;
                            for (int w = 0; w < d.W; w++) { double f = d.Nw[w] > Eps ? d.Br[w] / d.Nw[w] : 0.0; if (f > best) { best = f; idx = w; } }
                        }
                    }
                    if (idx >= 0 && idx < d.W) uid = d.Uid[idx];
                    return true;
                }
            }
            catch (Exception e) { Stumble("VillageBook.FxUid", e); return false; }
        }

        // ------------------------------------------------------------ E7/W1 (232): wiesci i pamiec wojny (odczyty pod Gate, nie rzucaja)
        /// <summary>VillageNews (tick godzinny): zabiera kolejke przejsc na spalona.</summary>
        internal static void TakeNews(List<BurnNews> into)
        {
            try { lock (Gate) { into.AddRange(_news); _news.Clear(); } }
            catch (Exception e) { Stumble("VillageBook.TakeNews", e); }
        }

        /// <summary>VillageNews.Daily: liczniki od poprzedniej linii (spalone w ksiedze przy uderzeniu, zgubione ponad kolejke, nowe zdarzenia pamieci) i stan pamieci; zeruje.</summary>
        internal static void TakeNewsCounts(out int burned, out int lost, out int memNew, out int memAll, out int burnsAll)
        {
            burned = lost = memNew = memAll = burnsAll = 0;
            try
            {
                lock (Gate)
                {
                    burned = _nBurned; lost = _nNewsLost; memNew = _nMemNew;
                    _nBurned = _nNewsLost = _nMemNew = 0;
                    foreach (var d in _d.Values)
                    {
                        if (d.M == null) continue;
                        for (int i = 0; i < d.M.Length; i++) { var m = d.M[i]; if (m != null) { memAll++; burnsAll += m.Burns; } }
                    }
                }
            }
            catch (Exception e) { Stumble("VillageBook.TakeNewsCounts", e); }
        }

        /// <summary>
        /// VillageStateChanged(Looted) - gra wola go w RaidEventComponent.OnBeforeFinalize PO Devastation.EndPrefix (ostatnie uderzenie rozliczone): wioska sesji z ogniem (ta sama,
        /// co FxUid; bez sesji - wies gry), osady spalone / osady tej wioski z ksiegi, czy ta sesja przeszla jakas wioske na spalona, czy ta wioska jest juz spalona w ksiedze
        /// (wczesniejsze zdarzenie - wioska spalona ma miejsce do pulapu DevastationMaxPercent, wiec kolejny rabunek zaczyna od niej). false - ksiega nieczynna albo wies nieznana.
        /// </summary>
        internal static bool LootNews(Village v, object comp, out string vname, out string dname, out int burned, out int n, out bool sessBurned, out bool wasBurned)
        {
            vname = dname = null; burned = n = 0; sessBurned = wasBurned = false;
            if (v == null || !Active) return false;
            try
            {
                lock (Gate)
                {
                    Dist d;
                    if (!_byV.TryGetValue(v, out d) || !d.NSet) return false;
                    var s = d.S != null && (comp == null || ReferenceEquals(d.S.Key, comp)) ? d.S : null;
                    int w = s != null && s.Cur >= 0 && s.Cur <= d.W ? s.Cur : d.W;
                    sessBurned = s != null && s.Burned;
                    wasBurned = (d.Flag[w] & FBurned) != 0;          // recenzja E7/W1: "plundered" tylko przy wiosce niespalonej w ksiedze (projekt pkt 2, R10, T5)
                    vname = d.VName[w]; dname = d.Name;
                    n = Math.Max(1, (int)Math.Round(d.Nw[w]));
                    burned = Math.Max(0, Math.Min(n, (int)Math.Round(d.Br[w])));
                    return true;
                }
            }
            catch (Exception e) { Stumble("VillageBook.LootNews", e); return false; }
        }

        /// <summary>"Plonie" (tick godzinny, rabunek w toku): nazwa wioski z ogniem - ta sama funkcja co widok (FxUid; otwiera sesje jak PollRaids) - i nazwa okregu.</summary>
        internal static bool FireNames(Village v, out string vname, out string dname)
        {
            vname = dname = null;
            if (v == null || !Active) return false;
            try
            {
                lock (Gate)
                {
                    Dist d;
                    if (!_byV.TryGetValue(v, out d)) return false;
                    dname = d.Name; vname = d.Name;
                    string uid;
                    KeyValuePair<Dist, int> kv;
                    // jak PollRaids: ogien tylko przy rabunku w toku (MapEvent wsi to rabunek - inaczej FxUid wzialby komponent innej bitwy za klucz sesji)
                    bool raid = false;
                    try { raid = d.St != null && d.St.IsUnderRaid; } catch { raid = false; }
                    if (raid && FxUid(d.Id, true, out uid) && uid != null && _byUid.TryGetValue(uid, out kv) && kv.Key != null) vname = kv.Key.VName[kv.Value];
                    return true;
                }
            }
            catch (Exception e) { Stumble("VillageBook.FireNames", e); return false; }
        }

        /// <summary>Jeff 16.10 (D1): uid wiosek pobocznych okregu (klucz = StringId wsi gry, jak _d) w stalej kolejnosci: order, potem uid - przydzial gospodarzy
        /// (VillageHolders). false - okreg nieznany (ksiega bez ukladu albo okreg bez wlasnego Village). Sam odczyt pod Gate, bez Active (herby nie zaleza od klucza ksiegi).</summary>
        internal static bool DistrictUids(string districtId, List<string> into)
        {
            if (districtId == null || into == null) return false;
            try
            {
                lock (Gate)
                {
                    Dist d;
                    if (!_d.TryGetValue(districtId, out d)) return false;
                    var idx = new List<int>(d.W);
                    for (int w = 0; w < d.W; w++) idx.Add(w);
                    idx.Sort((x, y) => { int c = d.Order[x].CompareTo(d.Order[y]); return c != 0 ? c : string.CompareOrdinal(d.Uid[x], d.Uid[y]); });
                    foreach (int w in idx) into.Add(d.Uid[w]);
                    return true;
                }
            }
            catch (Exception e) { Stumble("VillageBook.DistrictUids", e); return false; }
        }

        /// <summary>Jeff 16.10: sam crc ukladu (bez listy uid) - lore sprawdza nim przy kazdym dymku, czy uklad sie nie zmienil (VillageLayoutV5 w nowej sesji).</summary>
        internal static bool LayoutCrcNow(out uint crc)
        {
            crc = _layoutCrc;
            return _file != null;
        }

        /// <summary>Recenzja calosci (scalenie6): crc ukladu wiosek zapisanego w arm_district ("L:" - uklad przy zapisie); false - zapis bez klucza albo bez pola
        /// (nowa kampania, zapis sprzed E4/11, nieznana wersja). VillageCrafts.LayoutCheck - zapis arm_crafts bez wlasnego pola L:. Tylko odczyt (po Resolve).</summary>
        internal static bool SaveLayoutCrc(out uint crc)
        {
            crc = _saveCrc;
            return _saveCrcSet;
        }

        /// <summary>E7/W2 (233, R11): crc ukladu (ten sam co "L:" zapisu) i uid wierszy pliku ukladu - bramka danych lore (MapVillageLore). Plik ukladu wczytuje LoadFile
        /// raz na SESJE (Resolve w OnSessionLaunched; Jeff 16.10: klucz VillageLayoutV5 wybiera v5 albo v4, nowa sesja z innym wyborem czyta plik od nowa - lore
        /// sprawdza to przez LayoutCrcNow); false - pliku nie ma albo z innej sceny. Tylko odczyt, bez Gate (_file podmieniany tylko w Resolve, przed odczytami sesji).</summary>
        internal static bool Layout(out uint crc, out List<string> uids)
        {
            crc = _layoutCrc; uids = null;
            var f = _file;
            if (f == null) return false;
            uids = new List<string>(f.Records.Count);
            foreach (var r in f.Records) uids.Add(r.Uid);
            return true;
        }

        /// <summary>sw-przysiolki (v6): wiersze pliku ukladu tej sesji z kolumna hamlet (przysiolki na pustkowiach) - nakladka ziemi KS (VillageHamlets.OverlayKs) i linia
        /// sprawdzenia. Pusta lista - plik bez przysiolkow (v5, v4) albo nie wczytany. Tylko odczyt, bez Gate (_file podmieniany tylko w Resolve, jak Layout).</summary>
        internal static List<MapVillageRecord> HamletRecords()
        {
            var outl = new List<MapVillageRecord>();
            var f = _file;
            if (f == null) return outl;
            foreach (var r in f.Records) if (!string.IsNullOrEmpty(r.Hamlet)) outl.Add(r);
            return outl;
        }

        /// <summary>f10-uklad (v7): wiersze pliku ukladu tej sesji z ziemia KS w kolumnach ks_* (kazda wioska przeniesiona przez v7 i przysiolki v6) - nakladka ziemi KS
        /// (VillageHamlets.OverlayKs). W ukladzie v6 to te same wiersze co HamletRecords (ks_* mialy tylko przysiolki). Tylko odczyt, bez Gate (jak Layout).</summary>
        internal static List<MapVillageRecord> KsLandRecords()
        {
            var outl = new List<MapVillageRecord>();
            var f = _file;
            if (f == null) return outl;
            foreach (var r in f.Records) if (!string.IsNullOrEmpty(r.KsStart)) outl.Add(r);
            return outl;
        }

        /// <summary>sw-przysiolki: wszystkie wiersze pliku ukladu tej sesji (linia sprawdzenia - wioski okregow, najblizsza sasiadka); null - plik nie wczytany.</summary>
        internal static List<MapVillageRecord> LayoutRecords() { var f = _file; return f != null ? f.Records : null; }

        /// <summary>m100-kryjowki (recenzja 21.10): uklad wiosek pobocznych takze PRZED Resolve - nowa kampania wybiera startowe kryjowki w OnNewGameCreated
        /// (Campaign.DoLoadingForGameType, PostInitializeFourthState: scena mapy wczytana wczesniej w LoadVisualsThirdState), a Resolve idzie dopiero w OnSessionLaunched.
        /// LoadFile jest raz na plik (Resolve potem nie wczytuje drugi raz - ten sam _file), wybor pliku czyta MCM wprost (MapVillageData.EnsureChosen). Watek glowny.</summary>
        internal static List<MapVillageRecord> EnsureLayoutRecords()
        {
            if (_file == null) { try { LoadFile(); } catch (Exception e) { Stumble("VillageBook.EnsureLayoutRecords", e); } }
            var f = _file; return f != null ? f.Records : null;
        }

        /// <summary>sw-przysiolki: nazwa wczytanego pliku ukladu ("" - brak).</summary>
        internal static string LayoutFile { get { return _file != null ? (_fileName ?? "") : ""; } }

        /// <summary>f10-uklad: odleglosc minimalna wioska-wioska z naglowka pliku ukladu tej sesji ("# min_distance:"; -1 = brak naglowka albo plik nie wczytany).</summary>
        internal static float LayoutMinDist { get { var f = _file; return f != null ? f.MinDist : -1f; } }

        /// <summary>sw-przysiolki (tygodniowa linia przysiolkow): ludzie w domu przysiolkow (uid z listy) oraz wszystkich wiosek pobocznych i wsi gry - te same
        /// osady i ten sam podzial co dymek i menu (DOM okregu x (osady - puste) / (n - puste)). false - ksiega nieczynna. Pod Gate, nie rzuca.</summary>
        internal static bool HamletPeople(HashSet<string> hamlets, out double hHome, out double hSet, out double sideHome, out double mainHome, out int hBurned, out int dists)
        {
            hHome = hSet = sideHome = mainHome = 0.0; hBurned = 0; dists = 0;
            if (hamlets == null || !Active) return false;
            try
            {
                lock (Gate)
                {
                    foreach (var d in _d.Values)
                    {
                        try
                        {
                            if (d.W == 0 || !d.NSet || d.Nw == null) continue;
                            var sn = SnapOf(d);
                            bool any = false;
                            for (int w = 0; w <= d.W; w++)
                            {
                                double h, away;
                                People(d, sn, w, out h, out away);
                                if (w == d.W) { mainHome += h; continue; }
                                sideHome += h;
                                if (d.Uid[w] != null && hamlets.Contains(d.Uid[w]))
                                {
                                    any = true; hHome += h; hSet += d.Nw[w];
                                    if (BurnedAt(d, w)) hBurned++;
                                }
                            }
                            if (any) dists++;
                        }
                        catch (Exception e) { Stumble("VillageBook.HamletPeople(okreg)", e); }
                    }
                    return true;
                }
            }
            catch (Exception e) { Stumble("VillageBook.HamletPeople", e); return false; }
        }

        /// <summary>GL-5 (P5, dymek lore wsi lnianej): czy przysiolek ma obrazek rybacki (kolumna model = fishing pliku wiosek). false - ksiega bez ukladu, nieznany uid.</summary>
        internal static bool FishingHamlet(string uid)
        {
            if (uid == null) return false;
            try
            {
                lock (Gate)
                {
                    KeyValuePair<Dist, int> kv;
                    if (!_byUid.TryGetValue(uid, out kv) || kv.Key == null || kv.Key.Fish == null || kv.Value < 0 || kv.Value >= kv.Key.Fish.Length) return false;
                    return kv.Key.Fish[kv.Value];
                }
            }
            catch (Exception e) { Stumble("VillageBook.FishingHamlet", e); return false; }
        }

        /// <summary>Pamiec wojny wioski po uid (dymek W2). false - ksiega nieczynna, wioska nieznana albo bez pamieci.</summary>
        internal static bool MemoryOf(string uid, out WarMemory wm)
        {
            wm = default(WarMemory);
            if (uid == null || !Active) return false;
            try
            {
                lock (Gate)
                {
                    KeyValuePair<Dist, int> kv;
                    if (!_byUid.TryGetValue(uid, out kv) || kv.Key == null || kv.Key.M == null) return false;
                    var m = kv.Key.M[kv.Value];
                    if (m == null || m.Day < 0) return false;
                    wm.Burned = m.Kind == 'B'; wm.Day = m.Day; wm.Burns = m.Burns; wm.Hero = m.Hero; wm.Clan = m.Clan; wm.Fled = m.Fled; wm.Dead = m.Dead;
                    return true;
                }
            }
            catch (Exception e) { Stumble("VillageBook.MemoryOf", e); return false; }
        }

        // ------------------------------------------------------------ doba
        /// <summary>
        /// ArmouryBehavior.OnDailyTick PO Famine.Daily (Z6 - powroty, smierc i ucieczki z glodu tej doby juz na koncie): sesje rabunku, uzgodnienie pustych osad z kontem,
        /// zdarzenia odbudowy, rocznica najazdu (wszystkie okregi - krytyka K4), kontrola ksiegi, linia "Ksiega wiosek (E4/11)" i plik wioski-okregi.csv.
        /// </summary>
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = Today(), day = today - 1;
            try
            {
                if (!_resolveTried) Resolve("doba");
                if (!_resolved) return;
                if (!s.VillageBookEnabled) { OffDay(s, day); return; }
                bool enabling = _offSeen;
                _offSeen = false; _offLogged = false;
                int Q = Math.Max(0, Math.Min(360, s.RefugeeReturnQuietDays));
                int D = Math.Max(Q + 1, Math.Min(3640, s.RefugeeReturnDays));
                int distB = 0, wTouched = 0, nBurning = 0, nBurned = 0, nAband = 0, nRebuild = 0, nDamaged = 0;
                double sumBr = 0.0, sumBf = 0.0, maxR1 = 0.0, maxR2 = 0.0;
                int k1 = 0, k2 = 0, k3 = 0, kFull = 0, yearN = 0, yearYes = 0, ySiege = 0, yStay = 0, yGate = 0, yFam = 0, yNone = 0;
                bool any = false, first = _firstDay; int noK = 0;
                var csv = new StringBuilder(4096);
                lock (Gate)
                {
                    if (enabling) foreach (var d in _d.Values) d.Z = ZEnable;
                    foreach (var d in _d.Values)
                    {
                        try
                        {
                            // sesje rabunku: koniec rabunku - dym zostaje na ostatniej wiosce sesji do konca czujnosci (krytyka K3)
                            if (d.S != null && !d.St.IsUnderRaid) { d.SmokeIdx = d.S.Cur; d.S = null; }
                            if (d.S == null && d.SmokeIdx >= 0 && !d.St.IsRaided) d.SmokeIdx = -1;
                            if (!EnsureN(d)) { noK++; continue; }
                            if (d.P != null) { PlacePending(d, today); if (_pendWhere == "-") _pendWhere = "w pierwszej dobie"; }   // Jeff 16.10 (D5): zapas - zwykle juz PlacePendingAll przy starcie sesji
                            double a0 = PeopleBook.RefugeesOf(d.V);
                            if (a0 > 0.0 || d.ARexp > 0.0 || d.AFexp > 0.0 || Sum(d.Br) > 0.0 || Sum(d.Bf) > 0.0) Reconcile(d, s, today);
                            // zdarzenia "odbudowana" (b < 1% osad)
                            for (int w = 0; w <= d.W; w++)
                            {
                                if (d.Flag[w] == 0 || d.Br[w] + d.Bf[w] >= StandShare * d.Nw[w]) continue;
                                int ev = d.EvDay[w];
                                d.Flag[w] = 0; d.RebuiltDay[w] = today; d.Event = true; _dRebuiltN++;
                                LookTouch(d);               // E7/W4: flaga zdjeta (odbudowana)
                                if (_dRebuiltList.Count < TopEvents) _dRebuiltList.Add(d.VName[w] + " (" + d.Name + ") po " + (ev >= 0 ? (today - ev).ToString(Inv) : "?") + " dobach");
                            }
                            d.Z = ZNone;
                            // stan okregu, kontrola ksiegi (okreg nietkniety - bez pustych osad, konta, flag i sesji - zgodny z definicji: szybka sciezka)
                            bool touched = a0 > 0.0 || d.S != null || d.Event || Sum(d.Br) > 0.0 || Sum(d.Bf) > 0.0;
                            if (!touched) for (int w = 0; w <= d.W; w++) if (d.Flag[w] != 0) { touched = true; break; }
                            var region = OutlawLaw.RegionFor(d.St);
                            int last; bool clock = PeopleBook.LastStrike(region, out last);
                            bool year = clock && today - last == D;
                            if (!touched && !year) continue;
                            var sn = SnapOf(d);
                            int burning = 0, burned = 0, aband = 0, rebuild = 0, damaged = 0;
                            double homeSum = 0.0, awaySum = 0.0;
                            for (int w = 0; touched && w <= d.W; w++)
                            {
                                double h, a;
                                People(d, sn, w, out h, out a);
                                homeSum += h; awaySum += a;
                                double b = d.Br[w] + d.Bf[w];
                                if (b > 0.0 || d.Flag[w] != 0) wTouched++;
                                if (b > Devastation.MaxShare(s) * d.Nw[w] + 1e-6) k3++;
                                switch (StateAt(d, w))
                                {
                                    case SBurning: burning++; break;
                                    case SBurned: burned++; break;
                                    case SAbandoned: aband++; break;
                                    case SRebuilding: rebuild++; break;
                                    case SDamaged: damaged++; break;
                                }
                            }
                            nBurning += burning; nBurned += burned; nAband += aband; nRebuild += rebuild; nDamaged += damaged;
                            if (touched)
                            {
                                maxR1 = Math.Max(maxR1, Math.Abs(homeSum - sn.Dom));
                                maxR2 = Math.Max(maxR2, Math.Abs(awaySum - sn.A));
                            }
                            if ((sn.Ar <= 1e-6 && sn.Sbr > 0.0) || (sn.Af <= 1e-6 && sn.Sbf > 0.0)) k1++;
                            // R1-4: konto bez pustych osad w okregu na pulapie (brak miejsca) - nazwany nadmiar kp, nie k2
                            if ((sn.Ar > 1e-6 && sn.Sbr <= Eps) || (sn.Af > 1e-6 && sn.Sbf <= Eps)) { if (FreeRoom(d) <= Eps) kFull++; else k2++; }
                            if (sn.B > 0.0) { distB++; sumBr += sn.Sbr; sumBf += sn.Sbf; }
                            // rocznica: doba last + D w regionie (wszystkie okregi, takze bez pustych osad i konta - krytyka K4)
                            string yr = "";
                            if (year)
                            {
                                yearN++;
                                bool okB = true;
                                for (int w = 0; w <= d.W; w++) if (d.Br[w] >= StandShare * d.Nw[w]) { okB = false; break; }
                                if (sn.Ar < 1.0 && okB) { yr = "TAK"; yearYes++; }
                                else
                                {
                                    int stop = Devastation.StopOf(d.V);
                                    if (stop == Devastation.StopSiege) { yr = "NIE-oblezenie"; ySiege++; }
                                    else if (stop == Devastation.StopStay) { yr = "NIE-pobyt-oblezony"; yStay++; }
                                    else if (stop == Devastation.StopGate) { yr = "NIE-brama-E46"; yGate++; }
                                    else
                                    {
                                        // poprawka po recenzji (R1-2): czesc konta z glodu (StopFamWait, Af > 0) NIE wstrzymuje powrotu czesci z rabunku (Devastation.Daily:
                                        // backR liczy sie niezaleznie od famDay, w dobie last + D wraca cala) - takie NIE to porazka reguly, nie przyczyna nazwana
                                        yr = "NIE"; yNone++;
                                        if (stop == Devastation.StopFamWait || sn.Af > 0.0) yFam++;
                                    }
                                }
                            }
                            if (sn.B > 0.0 || sn.A > 0.0 || d.Event || year)
                            {
                                any = true;
                                if (s.LogEnabled) CsvRow(csv, d, sn, region, clock, last, today, day, year, yr, burning, burned, aband, rebuild, damaged);
                            }
                            d.Event = false;
                        }
                        catch (Exception e) { Stumble("VillageBook.Daily(okreg)", e); }
                    }
                }
                // zywnosc warowni od ludzi (ta sama funkcja co postfiks)
                int towns = 0, g1 = 0, g2 = 0, lootAll = 0, lootExp = 0; double fAll = 0.0, f1 = 0.0, f2 = 0.0;
                bool foodOn = s.TownFoodByVillagePeople && _townPatched;
                if (foodOn) TownFoodToday(out towns, out fAll, out f1, out g1, out f2, out g2, out lootAll, out lootExp);
                // drobne2 (G5, oczywisty blad pomiaru - bez klucza): trafieni od zerowania TEJ ksiegi (HitToday zeruje dopiero Devastation.DayLine pozniej w dobie, wiec
                // uderzenie miedzy VillageBook.Daily a DayLine bylo w HitToday poprzedniej doby, a w _dSetS tej - r3 = +5.0063 w d286 biegu dwuletniego)
                double hitDev = Devastation.HitAll - _hitMark;
                double hitLate = Math.Max(0.0, Devastation.HitAtDayLine - _hitMark);     // z tego po linii ksiegi poprzedniej doby, przed jej zamknieciem w Devastation
                double r3 = _dSetS - hitDev;
                bool firstLine = first;
                _firstDay = false;
                if (!s.LogEnabled) return;
                if (firstLine) FirstDayLine(t0);
                any |= _dStrikes > 0 || _dFam > 0.0 || _dUp > 0.0 || _dZast > 0.0 || _dRebuiltN > 0 || Math.Abs(fAll) > 0.001 || _stumbles > 0 || hitDev > 0.0;
                if (!any && !firstLine) return;
                var sb = new StringBuilder(2500);
                sb.Append("Ksiega wiosek (E4/11): dzien ").Append(day).Append(" | CZYNNA");
                sb.Append(" | okregi z pustymi osadami ").Append(distB).Append(" (pustych osad ").Append(F(sumBr + sumBf, "0.##")).Append(": rabunek i zerowanie ").Append(F(sumBr, "0.##"))
                  .Append(", glod ").Append(F(sumBf, "0.##")).Append("; wiosek dotknietych ").Append(wTouched).Append(": plonie ").Append(nBurning).Append(", spalone ").Append(nBurned)
                  .Append(", opuszczone ").Append(nAband).Append(", w odbudowie ").Append(nRebuild).Append(", czesciowo puste ").Append(nDamaged).Append(')');
                sb.Append(" | dzis: +").Append(F(_dRaidW + _dRaidMain + _dForW + _dForMain, "0.###")).Append(" osad (rabunek ").Append(F(_dRaidW + _dRaidMain, "0.###")).Append(" w wioskach ")
                  .Append(F(_dRaidW, "0.###")).Append(" / wsiach gry ").Append(F(_dRaidMain, "0.###")).Append(", zerowanie ").Append(F(_dForW + _dForMain, "0.###")).Append(" w wioskach ").Append(F(_dForW, "0.###"))
                  .Append(" / wsiach gry ").Append(F(_dForMain, "0.###")).Append("; uderzen ").Append(_dStrikes).Append(", sesji rabunku ").Append(_dSess).Append(')')
                  .Append(", opuszczone z glodu +").Append(F(_dFam, "0.###")).Append(", wstalo -").Append(F(_dUp, "0.###")).Append(", zastepczy +").Append(F(_dZast, "0.###"))
                  .Append(" (stary zapis ").Append(_dZastWhy[ZOld]).Append(" / inny uklad ").Append(_dZastWhy[ZLayout]).Append(" / wlaczenie ").Append(_dZastWhy[ZEnable]).Append(" okregow")
                  .Append("; konto znane po zwolnieniu pulapu ").Append(_dLate).Append(" okregow)")
                  .Append(", nadmiar ponad pulap ").Append(F(_dOver, "0.###"));
                sb.Append(" | spalone dzis ").Append(_dBurnedN).Append(_dBurnedList.Count > 0 ? ": " + string.Join("; ", _dBurnedList.ToArray()) : "")
                  .Append(" | opuszczone dzis ").Append(_dAbandonedN)
                  .Append(" | odbudowane dzis ").Append(_dRebuiltN).Append(_dRebuiltList.Count > 0 ? ": " + string.Join("; ", _dRebuiltList.ToArray()) : "");
                sb.Append(" | kontrola: w domu wiosek - DOM max |r1| ").Append(F(maxR1, "0.######")).Append(", poza domem wiosek - konto max |r2| ").Append(F(maxR2, "0.######"))
                  .Append(", puste osady przy pustym koncie k1 ").Append(k1).Append(", konto bez pustych osad k2 ").Append(k2).Append(", ponad pulap k3 ").Append(k3)
                  .Append(", konto bez miejsca (okreg na pulapie) kp ").Append(kFull)
                  .Append(", osady dzis x S - trafieni dzis r3 ").Append(F(r3, "0.####")).Append(" (trafieni ").Append(F(hitDev, "0.##")).Append(", nadmiar ludzi ").Append(F(_dOverPeople, "0.##"))
                  .Append(", bez ksiegi ").Append(F(_dHitSkip, "0.##")).Append(", w tym po linii ksiegi poprzedniej doby (drobne2) ").Append(F(hitLate, "0.##"))
                  .Append("), doplyw bez zrodla d0 ").Append(_dD0);
                if (_dFamSkip > 0) sb.Append(", ucieczki z glodu bez okregu ").Append(_dFamSkip);
                sb.Append(" | rok po najezdzie (").Append(D).Append(" dob): okregow ").Append(yearN).Append(" (TAK ").Append(yearYes).Append(", NIE ").Append(yearN - yearYes)
                  .Append(": oblezenie regionu ").Append(ySiege).Append(", pobyt oblezony ").Append(yStay).Append(", brama E4/6 ").Append(yGate)
                  .Append(", bez przyczyny ").Append(yNone).Append(" (w tym z czescia konta z glodu ").Append(yFam).Append("))");
                if (foodOn)
                {
                    // poprawka po recenzji (R1-1, G8): "z N pod korekta" = wsie Looted z LootedOn i przelicznikiem ludzi (kazda powinna dac > 0), "Looted razem" - wszystkie
                    // wsie Looted warowni nieoblezonych (bez Innych); przy LootedOn false nazwana przyczyna w nawiasie kwadratowym
                    sb.Append(" | zywnosc warowni od ludzi: warowni ").Append(towns).Append(", Food swiata ").Append(fAll >= 0 ? "+" : "").Append(F(fAll, "0.##")).Append("/d (spladrowane +")
                      .Append(F(f1, "0.##")).Append(" w ").Append(g1).Append(" wsiach z ").Append(lootExp).Append(" pod korekta, Looted razem ").Append(lootAll)
                      .Append(", uchodzcy i rece -").Append(F(f2, "0.##")).Append(" w ").Append(g2).Append(')');
                    if (!Devastation.LootedOn)
                        sb.Append(" [wies spladrowana bez korekty: ").Append(!s.LootedProducesByPeople ? "Looted Produces By People" : (!Devastation.On ? "spustoszenie nieczynne" : "latka produkcji Looted nie wpieta")).Append(']');
                }
                else sb.Append(" | zywnosc warowni od ludzi: WYLACZONA (").Append(!s.TownFoodByVillagePeople ? "Town Food By Village People" : "latka nie wpieta").Append(')');
                if (noK > 0) sb.Append(" | okregi bez przelicznika ludzi (n czeka) ").Append(noK);
                sb.Append(" | potkniecia ").Append(_stumbles).Append(" (razem ").Append(_stumblesAll).Append(')');
                sb.Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv)).Append(" ms.");
                Log.Info(sb.ToString());
                if (csv.Length > 0)
                {
                    string path = Log.Csv(CsvName, CsvHeader, csv.ToString());
                    if (path != null && _csvPath != path) { _csvPath = path; Log.Info("Ksiega wiosek (E4/11): plik okregow (wiersz na okreg z pustymi osadami, kontem, zdarzeniem albo rocznica): " + path); }
                }
            }
            catch (Exception e) { Stumble("VillageBook.Daily", e); }
            finally { NewDay(); Devastation.BookClosed(); }     // drobne2 (G5): od teraz do Devastation.DayLine - diagnoza spoznionych uderzen
        }

        private static void CsvRow(StringBuilder csv, Dist d, Snap sn, Settlement region, bool clock, int last, int today, int day, bool year, string yr,
                                   int burning, int burned, int aband, int rebuild, int damaged)
        {
            double nWio = 0.0; for (int w = 0; w < d.W; w++) nWio += d.Nw[w];
            bool player = false; try { player = d.St.OwnerClan == Clan.PlayerClan; } catch { }
            bool siege = region != null && region.IsUnderSiege;
            csv.Append(day).Append(';').Append(d.Id).Append(';').Append(Clean(d.Name)).Append(';').Append(region != null ? region.StringId : "").Append(';').Append(Clean(NameOf(region))).Append(';')
               .Append(player ? 1 : 0).Append(';').Append(F(d.N, "0.##")).Append(';').Append(F(nWio, "0.##")).Append(';').Append(d.W).Append(';')
               .Append(F0(sn.Dom)).Append(';').Append(F(sn.Ar, "0.##")).Append(';').Append(F(sn.Af, "0.##")).Append(';').Append(F(sn.Sbr, "0.####")).Append(';').Append(F(sn.Sbf, "0.####")).Append(';')
               .Append(burning).Append(';').Append(burned).Append(';').Append(aband).Append(';').Append(rebuild).Append(';').Append(damaged).Append(';')
               .Append(F(sn.Y, "0.####")).Append(';').Append(clock ? last.ToString(Inv) : "").Append(';').Append(clock ? (today - last).ToString(Inv) : "").Append(';')
               .Append(siege ? 1 : 0).Append(';').Append(year ? 1 : 0).Append(';').Append(yr).Append(Environment.NewLine);
        }

        private static void FirstDayLine(long t0)
        {
            int with = 0, without = 0, saved = 0, shrunk = 0, savedB = 0;
            int records = 0;
            lock (Gate)
                foreach (var d in _d.Values)
                {
                    if (d.W > 0) { with++; records += d.W; } else without++;
                    if (d.Shrunk) shrunk++;
                    if (Sum(d.Br) > 0.0 || Sum(d.Bf) > 0.0) savedB++;
                }
            saved = _savedN;
            var sb = new StringBuilder(800);
            sb.Append("Ksiega wiosek (E4/11): uklad ");
            if (_file != null)
                sb.Append('\'').Append(_file.Title).Append("' (crc ukladu ").Append(_layoutCrc.ToString("X8")).Append(", plik crc32 ").Append(_file.FileCrc32.ToString("X8")).Append("), wiosek ")
                  .Append(records).Append(" w ").Append(with).Append(" okregach (odrzucone ").Append(_file.BadRows).Append(", wsi gry brak ").Append(_rowsNoVillage).Append(" wiosek w ").Append(_distNoVillage).Append(" okregach")
                  .Append(_distNoVillageIds.Count > 0 ? ": " + string.Join(", ", _distNoVillageIds.Take(10)) + (_distNoVillageIds.Count > 10 ? " i " + (_distNoVillageIds.Count - 10) + " innych" : "") : "")   // E4 fixB (G2): id po nazwie
                  .Append(')');
            else sb.Append("BRAK (").Append(_fileNote).Append(") - kazdy okreg to sama wies gry");
            sb.Append(" | okregi ").Append(with + without).Append(": z wioskami ").Append(with).Append(", sama wies gry ").Append(without);
            sb.Append(" | osady (").Append(_p).Append(" ludzi na osade): z zapisu ").Append(saved).Append(" okregow, nowe ").Append(_newN.Count);
            if (_newN.Count > 0)
            {
                var sorted = _newN.Select((x, i) => new KeyValuePair<double, string>(x, _newNames[i])).OrderBy(kv => kv.Key).ToList();
                int m = sorted.Count;
                double med = m % 2 == 1 ? sorted[m / 2].Key : 0.5 * (sorted[m / 2 - 1].Key + sorted[m / 2].Key);
                sb.Append(" (mediana ").Append(F(med, "0.#")).Append(", min ").Append(F(sorted[0].Key, "0")).Append(" (").Append(sorted[0].Value).Append("), max ")
                  .Append(F(sorted[m - 1].Key, "0")).Append(" (").Append(sorted[m - 1].Value).Append("))");
            }
            sb.Append(", okregi z wioskami pomniejszonymi ").Append(shrunk);
            sb.Append(" | puste osady: z zapisu ").Append(_savedB).Append(" okregow, po pierwszej dobie ").Append(savedB).Append(" okregow, przydzial zastepczy ")
              .Append(_dZastWhy[ZOld] + _dZastWhy[ZLayout] + _dZastWhy[ZEnable]).Append(" (stary zapis ").Append(_dZastWhy[ZOld]).Append(" / inny uklad ").Append(_dZastWhy[ZLayout])
              .Append(" / wlaczenie ").Append(_dZastWhy[ZEnable]).Append(')');
            // D5 (po recenzji): "rabunek X z Y" / "glod X z Y" - rozlozone wobec oczekiwanych (z zapisu + dolozone dla stanu spalonych i opuszczonych, ktore urosly);
            // rowne przy "ponad pulap 0" (sprawdz_logi E4/11 G11 przy wczytaniu zapisu z innym ukladem)
            if (_pendMapFile != null)
                sb.Append(" | inny uklad (D5): stan wiosek po uid z ").Append(_pendMapFile).Append(" - puste osady w ").Append(_pendDists).Append(" okregach: wioski, ktore zostaly, ").Append(_mapKept)
                  .Append(" (spalone / opuszczone z zachowanym udzialem ").Append(_mapHeld).Append(", dolozone dla stanu ").Append(F(_pendAddR + _pendAddF, "0.##")).Append(" osad), usuniete ").Append(_mapGone)
                  .Append(" (do najblizszych) - rabunek ").Append(F(_pendR, "0.##")).Append(" z ").Append(F(_pendR0, "0.##")).Append(", glod ").Append(F(_pendF, "0.##")).Append(" z ").Append(F(_pendF0, "0.##"))
                  .Append(" osad, ponad pulap ").Append(F(_pendDrop, "0.##")).Append(", zepsute wpisy ").Append(_savedBad).Append(" (przydzial zastepczy); flagi ").Append(_mapFlags)
                  .Append(" wiosek (usunietych na najblizsza ").Append(_mapFlagsMoved).Append("), pamiec wojny ").Append(_mapMem).Append(" wpisow (usunietych na najblizsza ").Append(_mapMemMoved)
                  .Append(", przy zajetej zostaje nowsza ").Append(_mapMemLost).Append("); rozlozone ").Append(_pendWhere);
            else if (_pendDists > 0 || _pendBad > 0 || _pendMemSkipped > 0)
                sb.Append(" | inny uklad (D5): puste osady z zapisu przeniesione na wioski tego ukladu w ").Append(_pendDists).Append(" okregach - rabunek ").Append(F(_pendR, "0.##"))
                  .Append(" z ").Append(F(_pendR0, "0.##")).Append(", glod ").Append(F(_pendF, "0.##")).Append(" z ").Append(F(_pendF0, "0.##")).Append(" osad, ponad pulap ").Append(F(_pendDrop, "0.##"))
                  .Append(", zepsute wpisy ").Append(_pendBad).Append(" (przydzial zastepczy), pamiec wojny pominieta ").Append(_pendMemSkipped).Append(" wpisow (indeksy starego ukladu - uklad zapisu nieznany)")
                  .Append("; rozlozone ").Append(_pendWhere);
            sb.Append(" | czas: plik ").Append(F(_loadMs, "0.0")).Append(" ms, okregi ").Append(F(_resolveMs, "0.0")).Append(" ms, pierwsza doba ")
              .Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv)).Append(" ms.");
            Log.Info(sb.ToString());
            _newN.Clear(); _newNames.Clear();
        }

        /// <summary>Klucz wylaczony: wszystkie puste osady 0 jedna linia (odbudowane zbiorczo), sesje i dym od zera; po wlaczeniu - przydzial zastepczy z konta.</summary>
        private static void OffDay(Settings s, int day)
        {
            _offSeen = true;
            int dists = 0, villages = 0; double b = 0.0;
            lock (Gate)
                foreach (var d in _d.Values)
                {
                    double x = Sum(d.Br) + Sum(d.Bf);
                    bool flags = false;
                    for (int w = 0; w <= d.W; w++) { if (d.Flag[w] != 0) { villages++; flags = true; } d.Flag[w] = 0; }
                    if (x > 0.0 || flags) { dists++; b += x; }
                    Array.Clear(d.Br, 0, d.Br.Length); Array.Clear(d.Bf, 0, d.Bf.Length);
                    d.ARexp = d.AFexp = 0.0; d.S = null; d.SmokeIdx = -1; d.Event = false; d.P = null;   // D5: przeniesienie z zapisu przepada razem z reszta stanu
                }
            if (!s.LogEnabled || (_offLogged && dists == 0)) { NewDay(); return; }
            _offLogged = true;
            Log.Info("Ksiega wiosek (E4/11): dzien " + day + " | WYLACZONA (Village Book Enabled w MCM) - puste osady wyzerowane w " + dists + " okregach (" + F(b, "0.##")
                     + " osad), odbudowane zbiorczo " + villages + " wiosek; dymek 'settlements x 250', ogien '(lancuch gry)'; po wlaczeniu puste osady z konta uchodzcow (przydzial zastepczy).");
            NewDay();
        }

        // ------------------------------------------------------------ zywnosc warowni od ludzi (5.1)
        /// <summary>
        /// Postfiks na DefaultSettlementFoodModel.CalculateTownFoodStocksChange (addytywny, obok WinterBite.TownFoodPostfix i Famine.ShelterPostfix): skladnik kazdej wsi
        /// warowni (gra: (poziom + 1) x 6 tylko Normal) x czynnik rak (E4/5 x E4/7); wies Looted przy LootedProducesByPeople - skladnik Normal x czynnik rak zamiast 0.
        /// Oblezenie - jak w grze (skladnika wsi nie ma). Moze isc z watkow AI: tylko odczyty pod zamkami PeopleBook/FreeHands, bez stanu ksiegi wiosek. Nie rzuca.
        /// </summary>
        public static void TownFoodPostfix(Town town, ref ExplainedNumber __result)
        {
            if (!_townPatched || town == null) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.TownFoodByVillagePeople || town.IsUnderSiege) return;
                var st = town.Settlement;
                var bv = st != null ? st.BoundVillages : null;
                if (bv == null || bv.Count == 0) return;
                double sum = 0.0;
                for (int i = 0; i < bv.Count; i++) { double g, o; if (TownVillageTerm(bv[i], out g, out o)) sum += o - g; }
                if (Math.Abs(sum) > 0.001 && !double.IsNaN(sum)) __result.Add((float)sum, _txtHands);
            }
            catch (Exception e) { Stumble("VillageBook.TownFoodPostfix", e); }
        }

        /// <summary>Skladnik wsi warowni: gra (Normal - (poziom + 1) x 6, inaczej 0) i nasz (Normal albo Looted przy LootedOn - (poziom + 1) x 6 x czynnik rak). false = oba 0.</summary>
        private static bool TownVillageTerm(Village v, out double game, out double ours)
        {
            game = 0.0; ours = 0.0;
            if (v == null) return false;
            var state = v.VillageState;
            bool normal = state == Village.VillageStates.Normal;
            // poprawka po recenzji (R2-1): ta sama bramka co produkcja wsi Looted (Devastation.SwapLooted - CoversQuiet); wies bez przelicznika ludzi (KFor = 0) ma
            // YieldFactor 1 i dawalaby warowni pelne (poziom + 1) x 6 z wsi, ktora sama nic nie produkuje (zywnosc z niczego)
            bool looted = state == Village.VillageStates.Looted && Devastation.LootedOn && Devastation.CoversQuiet(v);
            if (!normal && !looted) return false;
            double unit = (v.GetHearthLevel() + 1) * 6.0;           // poziom z postfiksem progow E4/5 - ten sam, co w grze
            if (normal) game = unit;
            double y = (double)Devastation.YieldFactor(v) * FreeHands.YieldFactor(v);
            if (double.IsNaN(y) || double.IsInfinity(y) || y < 0.0) y = 1.0;
            ours = unit * Math.Min(1.0, y);
            return true;
        }

        /// <summary>
        /// Linia doby (ta sama funkcja co postfiks). Poprawka po recenzji (R1-1, G8): osobno wsie Looted warowni liczonych (lootAll) i te z nasza korekta
        /// (lootExp: LootedOn i przelicznik ludzi - TownVillageTerm true) - plusN (korekta > 0) porownany z lootExp w narzedziu (dotad G8 byl tautologia).
        /// </summary>
        private static void TownFoodToday(out int towns, out double all, out double plus, out int plusN, out double minus, out int minusN, out int lootAll, out int lootExp)
        {
            towns = 0; all = 0.0; plus = 0.0; plusN = 0; minus = 0.0; minusN = 0; lootAll = 0; lootExp = 0;
            var seen = new HashSet<Settlement>();
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !st.IsFortification || st.Town == null || st.IsUnderSiege || !seen.Add(st)) continue;
                    if (OthersRise.IsWhiteWalkerClan(st.MapFaction as Clan)) continue;     // ROT zwraca 0 przed baza - postfiks nie biegnie
                    var bv = st.BoundVillages;
                    if (bv == null || bv.Count == 0) continue;
                    double sum = 0.0;
                    for (int i = 0; i < bv.Count; i++)
                    {
                        double g, o;
                        bool isLooted = bv[i] != null && bv[i].VillageState == Village.VillageStates.Looted;
                        if (isLooted) lootAll++;
                        if (!TownVillageTerm(bv[i], out g, out o)) continue;
                        if (isLooted) lootExp++;
                        double x = o - g;
                        if (x > 0.001) { plus += x; plusN++; }
                        else if (x < -0.001) { minus -= x; minusN++; }
                        sum += x;
                    }
                    if (Math.Abs(sum) > 0.001) { towns++; all += sum; }
                }
                catch (Exception e) { Stumble("VillageBook.TownFoodToday", e); }
            }
        }

        /// <summary>
        /// E5/B4 (targ przy zamku, tylko odczyt - jedna liczba jedzenia okregu w linii "Targ przy zamku (5.11)"): skladnik tej wsi w zapasie jej warowni tak,
        /// jak liczy go dzis model - z korekta E4/11 (Town Food By Village People i latka wpieta), inaczej gry ((poziom + 1) x 6 tylko Normal). Warownia
        /// oblezona - 0 (postfiks i gra nie daja wtedy skladnika wsi). Wyjatek leci do wolajacego (on liczy potkniecia).
        /// </summary>
        internal static double FortVillageFood(Village v)
        {
            var b = v != null ? v.Bound : null;
            if (b == null || b.IsUnderSiege) return 0.0;
            double g, o;
            if (!TownVillageTerm(v, out g, out o)) return 0.0;
            var s = Settings.Current;
            return s != null && s.TownFoodByVillagePeople && _townPatched ? o : g;
        }

        // ------------------------------------------------------------ menu okregu ("People of this land", E4/7; napisy po angielsku)
        private static string Place(string kind)
        {
            switch (kind)
            {
                case "bridge": return VillageTexts.Make(VillageTexts.PlaceBridge).ToString();
                case "ford": return VillageTexts.Make(VillageTexts.PlaceFord).ToString();
                case "crossroad": return VillageTexts.Make(VillageTexts.PlaceCrossroad).ToString();
                case "road": return VillageTexts.Make(VillageTexts.PlaceRoad).ToString();
                case "river": return VillageTexts.Make(VillageTexts.PlaceRiver).ToString();
                case "lake": return VillageTexts.Make(VillageTexts.PlaceLake).ToString();
                case "coast": return VillageTexts.Make(VillageTexts.PlaceCoast).ToString();
                case "field": return VillageTexts.Make(VillageTexts.PlaceField).ToString();
                // sw-przysiolki (v6): miejsca przysiolkow na pustkowiach
                case "hills": return VillageTexts.Make(VillageTexts.PlaceHills).ToString();
                case "forest": return VillageTexts.Make(VillageTexts.PlaceForest).ToString();
                case "steppe": return VillageTexts.Make(VillageTexts.PlaceSteppe).ToString();
                case "moor": return VillageTexts.Make(VillageTexts.PlaceMoor).ToString();
                case "desert": return VillageTexts.Make(VillageTexts.PlaceDesert).ToString();
                case "wilds": return VillageTexts.Make(VillageTexts.PlaceWilds).ToString();
                default: return "";
            }
        }

        private static string Tidy(string t) { return t.Replace("  ", " ").Replace(" :", ":"); }

        /// <summary>PeopleMenu.Consequence (E4/7, okno ShowInquiry): sekcja okregu wsi albo lista okregow warowni. null - ksiega nieczynna.
        /// E7/W3 (234, R13): przy wpietym menu okregu ("Look over the district", DistrictMenu.On) wies dostaje sam naglowek i podsumowanie okregu - wiersze
        /// wiosek sa w oknie okregu (DistrictView, ta sama funkcja wiersza RowLine); klucz off - sekcja z wierszami jak dotad.</summary>
        internal static string MenuSection(Settlement st)
        {
            if (st == null || !Active) return null;
            try
            {
                lock (Gate)
                {
                    if (st.IsVillage && st.Village != null)
                    {
                        Dist d;
                        if (st.StringId == null || !_d.TryGetValue(st.StringId, out d)) return null;
                        if (!EnsureN(d)) return VillageTexts.Make(VillageTexts.DistNotYet).ToString();
                        return DistrictText(d, !DistrictMenu.On);
                    }
                    if (st.IsTown || st.IsCastle)
                    {
                        var bv = st.BoundVillages;
                        if (bv == null || bv.Count == 0) return null;
                        var sb = new StringBuilder(600);
                        sb.Append(VillageTexts.Make(VillageTexts.DistRegion, "REGION", NameOf(st)).ToString());
                        foreach (var v in bv)
                        {
                            Dist d;
                            // recenzja fixB (3): menu warowni zostaje przy kluczu v.Settlement.StringId (jak przed G4) - wspolne Village z ROT-Map (Kurbok/Thupak) dawalo
                            // w menu Omber okregi Guntak i Volug (d.Name z town39); zmiana menu - osobna decyzja (wariant sceptyka G4: okreg od wlasciciela Village)
                            if (v == null || v.Settlement == null || v.Settlement.StringId == null || !_d.TryGetValue(v.Settlement.StringId, out d) || !EnsureN(d)) continue;
                            var sn = SnapOf(d);
                            int standing = 0;
                            for (int w = 0; w <= d.W; w++) { int x = StateAt(d, w); if (x != SBurned && x != SAbandoned && x != SBurning) standing++; }
                            sb.Append('\n').Append(VillageTexts.Make(VillageTexts.DistRegionLine, "DISTRICT", d.Name, "STANDING", standing.ToString(Inv), "TOTAL", (d.W + 1).ToString(Inv),
                                "PEOPLE", N0(sn.Dom), "REFUGEES", N0(sn.A)).ToString());
                        }
                        return sb.ToString();
                    }
                }
            }
            catch (Exception e) { Stumble("VillageBook.MenuSection", e); }
            return null;
        }

        /// <summary>Tekst okregu wsi: naglowek, podsumowanie, plon; rows - takze wiersze wiosek (najwyzej MaxLines, stojace ponad limit - jedna linia).
        /// E7/W3: bez wierszy dla "People of this land" przy wpietym menu okregu i jako naglowek okna okregu (DistrictView).</summary>
        private static string DistrictText(Dist d, bool rows)
        {
            var sn = SnapOf(d);
            int n = d.W + 1;
            var states = new int[n];
            int burning = 0, burned = 0, aband = 0;
            for (int w = 0; w < n; w++)
            {
                states[w] = StateAt(d, w);
                if (states[w] == SBurning) burning++; else if (states[w] == SBurned) burned++; else if (states[w] == SAbandoned) aband++;
            }
            var sb = new StringBuilder(rows ? 1500 : 500);
            sb.Append(VillageTexts.Make(VillageTexts.DistHead, "DISTRICT", d.Name, "SETTLEMENTS", N0(d.N), "VILLAGES", n.ToString(Inv)).ToString()).Append('\n');
            double days;
            string standing = (n - burned - burning - aband).ToString(Inv);
            if (Famine.DaysOf(d.V, out days))
                sb.Append(VillageTexts.Make(VillageTexts.DistSummary, "STANDING", standing, "TOTAL", n.ToString(Inv), "BURNED", burned.ToString(Inv), "BURNING", burning.ToString(Inv),
                    "PEOPLE", N0(sn.Dom), "REFUGEES", N0(sn.A), "DAYS", N0(days)).ToString());
            else
                sb.Append(VillageTexts.Make(VillageTexts.DistSummaryNoGranary, "STANDING", standing, "TOTAL", n.ToString(Inv), "BURNED", burned.ToString(Inv), "BURNING", burning.ToString(Inv),
                    "PEOPLE", N0(sn.Dom), "REFUGEES", N0(sn.A)).ToString());
            if (aband > 0) sb.Append(' ').Append(VillageTexts.Make(VillageTexts.DistAbandonedNote, "ABANDONED", aband.ToString(Inv)).ToString());
            sb.Append('\n');
            if (sn.Y < 0.995) sb.Append(VillageTexts.Make(VillageTexts.DistHarvest, "PCT", (100.0 * sn.Y).ToString("0", Inv)).ToString()).Append('\n');
            if (!rows) return sb.ToString().TrimEnd('\n');
            int lines = 0, moreStanding = 0, today = Today();
            foreach (int w in RowOrder(d, states))
            {
                bool stand = states[w] == SStanding;
                if (stand && lines >= MaxLines) { moreStanding++; continue; }
                double home;
                string line = RowLine(d, sn, w, states[w], today, out home);
                sb.Append(line).Append(' ').Append(ShareText(sn, home)).Append('\n');
                lines++;
            }
            if (moreStanding > 0) sb.Append(VillageTexts.Make(VillageTexts.DistMore, "N", moreStanding.ToString(Inv)).ToString()).Append('\n');
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>Kolejnosc wierszy wiosek (menu i okno okregu E7/W3): najpierw plonace, spalone, opuszczone, w odbudowie, czesciowo puste, potem stojace;
        /// w grupie wies gry pierwsza, reszta wedlug indeksu.</summary>
        private static List<int> RowOrder(Dist d, int[] states)
        {
            int[] rank = { 0, 1, 4, 3, 2, 5 };     // indeks = stan (SStanding, SDamaged, SBurned, SAbandoned, SRebuilding, SBurning) -> waga kolejnosci (malejaco)
            return Enumerable.Range(0, states.Length).OrderByDescending(w => rank[states[w]]).ThenBy(w => w == d.W ? -1 : w).ToList();
        }

        /// <summary>Wiersz jednej wioski z ksiegi - JEDNA funkcja dla menu "People of this land" i okna okregu (E7/W3, R13); home - ludzie w domu (do udzialu w plonie).</summary>
        private static string RowLine(Dist d, Snap sn, int w, int state, int today, out double home)
        {
            double away;
            People(d, sn, w, out home, out away);
            string name = d.VName[w], place = w == d.W ? "" : Place(d.Kind[w]);
            string line;
            switch (state)
            {
                case SBurning:
                    line = VillageTexts.Make(VillageTexts.DistLineBurning, "VILLAGE", name, "RAIDER", d.S != null ? d.S.Raider : "?").ToString(); break;
                case SBurned:
                    line = VillageTexts.Make(VillageTexts.DistLineBurned, "VILLAGE", name, "DAYS", (d.EvDay[w] >= 0 ? today - d.EvDay[w] : 0).ToString(Inv), "RETURNED", N0(home), "PEOPLE", N0(home + away)).ToString(); break;
                case SAbandoned:
                    line = VillageTexts.Make(VillageTexts.DistLineAbandoned, "VILLAGE", name, "PLACE", place, "PEOPLE", N0(home)).ToString(); break;
                case SRebuilding:
                    line = VillageTexts.Make(VillageTexts.DistLineRebuilding, "VILLAGE", name, "PEOPLE", N0(home)).ToString(); break;
                case SDamaged:
                    line = VillageTexts.Make(d.Br[w] >= d.Bf[w] ? VillageTexts.DistLinePart : VillageTexts.DistLineEmptied, "VILLAGE", name, "PLACE", place,
                        "BURNED", Math.Max(1.0, Math.Round(d.Br[w] + d.Bf[w])).ToString("0", Inv), "SETTLEMENTS", Math.Max(1.0, Math.Round(d.Nw[w])).ToString("0", Inv),
                        "PEOPLE", N0(home), "FLED", N0(away)).ToString(); break;
                default:
                    line = w == d.W
                        ? VillageTexts.Make(VillageTexts.DistLineMain, "VILLAGE", name, "PEOPLE", N0(home), "SETTLEMENTS", Math.Max(1.0, Math.Round(d.Nw[w])).ToString("0", Inv)).ToString()
                        : VillageTexts.Make(VillageTexts.DistLineWhole, "VILLAGE", name, "PEOPLE", N0(home), "SETTLEMENTS", Math.Max(1.0, Math.Round(d.Nw[w])).ToString("0", Inv)).ToString();
                    break;
            }
            return Tidy(line);
        }

        /// <summary>"(12% of the harvest)" - udzial wioski w plonie okregu (ludzie w domu x plon / ludzie w domu okregu).</summary>
        private static string ShareText(Snap sn, double home)
        {
            double share = sn.Dom > 0.0 ? 100.0 * sn.Y * home / sn.Dom : 0.0;
            return VillageTexts.Make(VillageTexts.DistShare, "SHARE", share.ToString("0.#", Inv)).ToString();
        }

        /// <summary>E7/W3 (234, R13): jedna wioska okna okregu - krotki tytul wiersza (RowTitle: ramka wiersza okna ma 65 px, dwie linie czcionki 25),
        /// pelny wiersz z tej samej funkcji co menu (RowLine) i udzial w plonie (dymek wiersza, opis), uid wioski (lore, pamiec wojny; wies gry - null).</summary>
        internal sealed class DistRow { public string Uid, Name, Title, Line, Share; public int State; public bool Main; }

        /// <summary>E7/W3: krotki tytul wiersza okna okregu ze stanu ksiegi (ten sam StateAt co wiersz menu): stojaca - nazwa i polozenie ("Brackenford by the river"),
        /// inne stany - istniejace krotkie teksty stanu (VilBurning, VilBurned, VilAbandoned, VilRebuilding) i DistRowDamaged.</summary>
        private static string RowTitle(Dist d, int w, int state)
        {
            string name = d.VName[w];
            switch (state)
            {
                case SBurning: return VillageTexts.Make(VillageTexts.VilBurning, "VILLAGE", name).ToString();
                case SBurned: return VillageTexts.Make(VillageTexts.VilBurned, "VILLAGE", name).ToString();
                case SAbandoned: return VillageTexts.Make(VillageTexts.VilAbandoned, "VILLAGE", name).ToString();
                case SRebuilding: return VillageTexts.Make(VillageTexts.VilRebuilding, "VILLAGE", name).ToString();
                case SDamaged: return VillageTexts.Make(VillageTexts.DistRowDamaged, "VILLAGE", name).ToString();
                default:
                    string place = w == d.W ? "" : Place(d.Kind[w]);
                    return place.Length > 0 ? name + " " + place : name;
            }
        }

        /// <summary>E7/W3 (234, R13): okno okregu wsi gry ("Look over the district") - nazwa okregu, naglowek z podsumowaniem (DistrictText bez wierszy, jak
        /// "People of this land") i WSZYSTKIE wioski okregu w kolejnosci menu (lista przewijana - bez limitu MaxLines). Sam odczyt pod Gate (EnsureN jak w menu).
        /// false - ksiega nieczynna albo wies bez okregu; okreg bez n - naglowek DistNotYet, bez wierszy. Wyjatek liczony (Stumble), nic nie gasi.</summary>
        internal static bool DistrictView(Settlement st, out string name, out string head, List<DistRow> rows)
        {
            name = head = null;
            if (st == null || !st.IsVillage || st.Village == null || rows == null || !Active) return false;
            try
            {
                lock (Gate)
                {
                    Dist d;
                    if (st.StringId == null || !_d.TryGetValue(st.StringId, out d)) return false;
                    name = d.Name;
                    if (!EnsureN(d)) { head = VillageTexts.Make(VillageTexts.DistNotYet).ToString(); return true; }
                    head = DistrictText(d, false);
                    var sn = SnapOf(d);
                    int n = d.W + 1, today = Today();
                    var states = new int[n];
                    for (int w = 0; w < n; w++) states[w] = StateAt(d, w);
                    foreach (int w in RowOrder(d, states))
                    {
                        double home;
                        string line = RowLine(d, sn, w, states[w], today, out home);
                        rows.Add(new DistRow { Uid = w == d.W ? null : d.Uid[w], Name = d.VName[w], Title = RowTitle(d, w, states[w]), Line = line, Share = ShareText(sn, home),
                                               State = states[w], Main = w == d.W });
                    }
                    return true;
                }
            }
            catch (Exception e) { rows.Clear(); Stumble("VillageBook.DistrictView", e); return false; }
        }

        // ------------------------------------------------------------ latka i linia startowa
        internal static void ApplyAll(Harmony h)
        {
            string wired = "-", missing = "-";
            MethodInfo m = null;
            try
            {
                m = AccessTools.Method(typeof(DefaultSettlementFoodModel), "CalculateTownFoodStocksChange");
                if (m != null && m.ReturnType == typeof(ExplainedNumber))
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(VillageBook), nameof(TownFoodPostfix)));
                    _townPatched = true; wired = "zywnosc warowni od ludzi (DefaultSettlementFoodModel.CalculateTownFoodStocksChange)";
                }
                else missing = "DefaultSettlementFoodModel.CalculateTownFoodStocksChange";
            }
            catch (Exception e) { missing = "CalculateTownFoodStocksChange (" + e.GetType().Name + ")"; Log.Error("VillageBook.ApplyAll", e); }
            try
            {
                // stan z ustawien startowych; MCM wchodzi przy starcie kampanii - prawdziwy stan stoi co dobe w linii "Ksiega wiosek (E4/11)"
                var s = Settings.Current;
                var sb = new StringBuilder(900);
                sb.Append("VillageBook (E4/11): ksiega wiosek pobocznych - wpiete: ").Append(wired).Append("; BRAK: ").Append(missing)
                  .Append("; wlasciciele latek celu: ").Append(Owners(m)).Append(" (U25)")
                  .Append("; wpiecia w kod: Devastation.Strike, Famine.TryFlee, MapVillagesView (ogien, dymek), PeopleMenu (okno, village_looted)");
                if (s != null)
                    sb.Append("; ustawienia startowe przed MCM (stan - linia dzienna): ksiega ").Append(s.VillageBookEnabled ? "TAK" : "NIE").Append(", ludzi na osade ").Append(s.DistrictSettlementPeople)
                      .Append(", zywnosc warowni od ludzi ").Append(s.TownFoodByVillagePeople ? "TAK" : "NIE");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("VillageBook.ApplyAll(linia)", e); }
        }

        private static string Owners(MethodBase m)
        {
            try
            {
                if (m == null) return "brak metody";
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return "bez latek";
                var map = new SortedDictionary<string, int[]>(StringComparer.Ordinal);   // id -> prefiksy, postfiksy, transpilery, finalizery
                Action<IEnumerable<Patch>, int> add = (list, k) => { if (list == null) return; foreach (var p in list) { int[] c; if (!map.TryGetValue(p.owner ?? "?", out c)) map[p.owner ?? "?"] = c = new int[4]; c[k]++; } };
                add(info.Prefixes, 0); add(info.Postfixes, 1); add(info.Transpilers, 2); add(info.Finalizers, 3);
                if (map.Count == 0) return "bez latek";
                var parts = new List<string>();
                foreach (var kv in map)
                {
                    var c = kv.Value; var bits = new List<string>();
                    if (c[0] > 0) bits.Add("prefiksy " + c[0]);
                    if (c[1] > 0) bits.Add("postfiksy " + c[1]);
                    if (c[2] > 0) bits.Add("transpilery " + c[2]);
                    if (c[3] > 0) bits.Add("finalizery " + c[3]);
                    parts.Add(kv.Key + ": " + string.Join(", ", bits.ToArray()));
                }
                return string.Join("; ", parts.ToArray());
            }
            catch (Exception e) { return "blad (" + e.GetType().Name + ")"; }
        }
    }
}
