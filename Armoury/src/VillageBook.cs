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
            public int[] FamOrder;                          // kolejnosc opuszczania z glodu: ubocze, linia, wezel (od najdalszej), wies gry na koncu
            public double[] Br, Bf; public byte[] Flag; public int[] EvDay, RebuiltDay;
            public double ARexp, AFexp;                     // konto (czesc z rabunku, czesc z glodu) z ostatniego uzgodnienia + doplyw od niego
            public Sess S; public int SmokeIdx = -1;        // trwajacy rabunek i dym w czujnosci (bez zapisu)
            public int Z;                                   // przydzial zastepczy czeka (powod)
            public bool Event;                              // zdarzenie dzis (wiersz CSV)
        }

        private sealed class Sess { public object Key; public int Cur = -1; public string Raider = "?"; public float Fx, Fy; public bool HasFrom; public int Day; }

        private struct Snap { public double Dom, A, Ar, Af, B, Sbr, Sbf, Y; }

        /// <summary>Stan wioski dla widoku i etapu 7 (napisy nad spalonymi, komunikaty).</summary>
        internal struct VState
        {
            public int State; public double Home, Away, Settlements, Empty, Share; public int EvDay, RebuiltDay, Today; public string Raider;
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Dist> _d = new Dictionary<string, Dist>(StringComparer.Ordinal);
        private static readonly Dictionary<string, KeyValuePair<Dist, int>> _byUid = new Dictionary<string, KeyValuePair<Dist, int>>(StringComparer.Ordinal);
        private static string _pending;                     // napis z zapisu do Resolve
        private static bool _resolved, _resolveTried, _firstDay = true, _offSeen, _offLogged, _pSet;
        private static int _p = 250;                        // ludzi na osade przy n (z zapisu albo z ustawien przy pierwszym n)
        private static string _resolveNote = "";
        private static double _resolveMs;
        private static int _rowsNoVillage, _distNoVillage, _savedN, _savedB, _savedBad;
        private static bool _layoutSame;
        private static readonly List<double> _newN = new List<double>();
        private static readonly List<string> _newNames = new List<string>();
        private static string _csvPath;

        // uklad z pliku (raz na proces - plik w grze sie nie zmienia)
        private static MapVillageFile _file;
        private static string _fileNote = "";
        private static uint _layoutCrc;
        private static double _loadMs;
        private static bool _fileTried;

        // liczniki doby (bez zapisu; zeruje Daily)
        private static double _dRaidW, _dRaidMain, _dForW, _dForMain, _dFam, _dUp, _dZast, _dOver, _dOverPeople, _dSetS, _dHitSkip;
        private static int _dStrikes, _dD0, _dSess, _dFamSkip, _dLate;       // _dLate - konto znane umieszczone po zwolnieniu pulapu (R1-4)
        private static readonly int[] _dZastWhy = new int[4];
        private static readonly List<string> _dBurnedList = new List<string>(), _dRebuiltList = new List<string>();
        private static int _dBurnedN, _dRebuiltN, _dAbandonedN;
        private static int _stumbles, _stumblesAll;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static bool _townPatched;

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
            lock (Gate) { _d.Clear(); _byUid.Clear(); }
            _pending = null; _resolved = _resolveTried = false; _firstDay = true; _offSeen = _offLogged = false; _pSet = false; _p = 250;
            _resolveNote = ""; _resolveMs = 0.0; _rowsNoVillage = _distNoVillage = _savedN = _savedB = _savedBad = 0; _layoutSame = false;
            _newN.Clear(); _newNames.Clear(); _csvPath = null;
            _stumblesAll = 0;
            lock (_errSites) _errSites.Clear();
            NewDay();
        }

        private static void NewDay()
        {
            _dRaidW = _dRaidMain = _dForW = _dForMain = _dFam = _dUp = _dZast = _dOver = _dOverPeople = _dSetS = _dHitSkip = 0.0;
            _dStrikes = _dD0 = _dSess = _dFamSkip = _dLate = 0;
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
            if (_fileTried) return;
            _fileTried = true;
            var sw = Stopwatch.StartNew();
            try
            {
                string path = MapVillageData.DataPath();
                if (path == null || !System.IO.File.Exists(path)) { _fileNote = "brak pliku " + (path ?? MapVillageData.FileName); return; }
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
                // crc ukladu z uid i osad wierszy w kolejnosci pliku - niezalezne od CRLF/LF, komentarzy i kolumn samego widoku
                var sb = new StringBuilder(f.Records.Count * 40);
                foreach (var r in f.Records) sb.Append(r.Uid).Append(':').Append(r.Settlements.ToString(Inv)).Append(';');
                _layoutCrc = MapVillageData.Crc32(Encoding.UTF8.GetBytes(sb.ToString()));
                _file = f;
            }
            catch (Exception e) { _file = null; _fileNote = "blad wczytania (" + e.GetType().Name + ")"; Stumble("VillageBook.LoadFile", e); }
            finally { _loadMs = sw.Elapsed.TotalMilliseconds; }
        }

        private static Dist Build(Settlement st, List<MapVillageRecord> recs)
        {
            int w0 = recs != null ? recs.Count : 0;
            var d = new Dist { Id = st.StringId, V = st.Village, St = st, Name = NameOf(st), W = w0 };
            int n = w0 + 1;
            d.F = new double[n]; d.Nw = new double[n]; d.Uid = new string[n]; d.VName = new string[n]; d.Kind = new string[n]; d.Cls = new char[n];
            d.X = new float[n]; d.Y = new float[n]; d.Rd = new float[n]; d.Order = new int[n];
            d.Br = new double[n]; d.Bf = new double[n]; d.Flag = new byte[n]; d.EvDay = new int[n]; d.RebuiltDay = new int[n];
            var p = st.GetPosition2D;
            d.X[w0] = p.x; d.Y[w0] = p.y; d.Rd[w0] = 0f; d.Cls[w0] = 'M'; d.VName[w0] = d.Name; d.Kind[w0] = ""; d.Order[w0] = int.MaxValue;
            for (int w = 0; w < w0; w++)
            {
                var r = recs[w];
                d.F[w] = Math.Max(1, r.Settlements); d.Uid[w] = r.Uid; d.VName[w] = r.Name; d.Kind[w] = r.Kind ?? "";
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
                    _d.Clear(); _byUid.Clear();
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
                            for (int w = 0; w < d.W; w++) if (!string.IsNullOrEmpty(d.Uid[w])) _byUid[d.Uid[w]] = new KeyValuePair<Dist, int>(d, w);
                        }
                        catch (Exception e) { Stumble("VillageBook.Resolve(wies)", e); }
                    }
                    foreach (var kv in byDist) if (!_d.ContainsKey(kv.Key)) { _distNoVillage++; _rowsNoVillage += kv.Value.Count; }
                    ApplyPending();
                }
                _resolved = true;
            }
            catch (Exception e) { Stumble("VillageBook.Resolve", e); }
            _resolveMs = sw.Elapsed.TotalMilliseconds;
            try
            {
                int with = 0; lock (Gate) foreach (var d in _d.Values) if (d.W > 0) with++;
                Log.Info("Ksiega wiosek (E4/11): start sesji (" + how + ") - " + (_resolved ? "okregi " + _d.Count + " (z wioskami " + with + ")" : "NIEROZWIAZANA - ksiega nieczynna do nastepnego wczytania")
                         + "; plik ukladu: " + (_file != null ? "'" + _file.Title + "' (crc ukladu " + _layoutCrc.ToString("X8") + ", plik crc32 " + _file.FileCrc32.ToString("X8") + ")" : _fileNote)
                         + "; " + _resolveNote + "; czas: plik " + F(_loadMs, "0.0") + " ms, okregi " + F(_resolveMs, "0.0") + " ms.");
            }
            catch (Exception e) { Stumble("VillageBook.Resolve(linia)", e); }
        }

        // zapis: "v1|L:<crc ukladu hex>|P:<ludzi na osade>|n:<id>=<N>;...|w:<id>=<br>/<bf>,...;...|x:<id>=<i>:<flaga>:<doba zdarzenia>:<doba odbudowy>,...;...|y:<id>=<ARexp>/<AFexp>;..."
        // (zarezerwowane, nie pisane w E4/11: r: sesje rabunku, c: czujnosc, u: uprowadzeni - etap 7.11)
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
            uint crc = 0; bool hasCrc = false, wasOff = false; string sn = null, sw = null, sx = null, sy = null;
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
                    case 'o': wasOff = body == "1"; break;
                }
            }
            _layoutSame = hasCrc && _file != null && crc == _layoutCrc;
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
                foreach (var d in _d.Values) d.Z = ZLayout;
                _resolveNote = "z zapisu: n " + _savedN + " okregow, puste osady - uklad inny (crc zapisu " + (hasCrc ? crc.ToString("X8") : "?") + ", pliku " + _layoutCrc.ToString("X8") + ") - przydzial zastepczy";
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
            if (wasOff) foreach (var d in _d.Values) d.Z = ZEnable;          // R1-5 (zepsute wpisy nizej nadpisuja na "inny uklad")
            foreach (var d in bad)
            {
                Array.Clear(d.Br, 0, d.Br.Length); Array.Clear(d.Bf, 0, d.Bf.Length);
                for (int i = 0; i <= d.W; i++) { d.Flag[i] = 0; d.EvDay[i] = -1; d.RebuiltDay[i] = -1; }
                d.ARexp = d.AFexp = 0.0; d.Z = ZLayout; _savedBad++;
            }
            _resolveNote = "z zapisu: n " + _savedN + " okregow, puste osady " + _savedB + " okregow, uklad zgodny" + (_savedBad > 0 ? ", zepsute wpisy " + _savedBad + " (przydzial zastepczy)" : "")
                           + (wasOff ? ", ksiega wylaczona przy zapisie (przydzial zastepczy - wlaczenie)" : "");
        }

        /// <summary>SyncData (zapis). Ksiega nierozwiazana (zapis przed startem sesji) - napis z wczytania bez zmian.</summary>
        internal static string Export()
        {
            if (!_resolved) return _pending ?? "";
            var sb = new StringBuilder(32 * 1024);
            sb.Append("v1|L:").Append(_layoutCrc.ToString("X8")).Append("|P:").Append(_p.ToString(Inv));
            if (_offSeen) sb.Append("|o:1");                                // R1-5: stan wyzerowany przez OffDay, wlaczenie jeszcze nie przeszlo doby
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
            }
            return sb.ToString();
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
            _newN.Add(d.N); _newNames.Add(d.Name);
            return true;
        }

        private static void Split(Dist d)
        {
            int W = d.W; double n = d.N;
            if (W == 0) { d.Nw[0] = n; d.Shrunk = false; return; }
            double sf = 0.0, mf = 0.0;
            for (int w = 0; w < W; w++) { sf += d.F[w]; if (d.F[w] > mf) mf = d.F[w]; }
            if (sf + mf <= n) { for (int w = 0; w < W; w++) d.Nw[w] = d.F[w]; d.Nw[W] = n - sf; d.Shrunk = false; }
            else { double c = n / (sf + mf); for (int w = 0; w < W; w++) d.Nw[w] = d.F[w] * c; d.Nw[W] = mf * c; d.Shrunk = true; }
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
                if (Room(d, w) <= Eps) continue;
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

        /// <summary>Nastepna: najblizsza (x, y) wioska z miejscem do `from` (remis - kolejnosc); wies gry na koncu.</summary>
        private static int NextFrom(Dist d, int from)
        {
            int best = -1; double bd = double.MaxValue;
            for (int w = 0; w < d.W; w++)
            {
                if (w == from || Room(d, w) <= Eps) continue;
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
                    // strona wroga = najblizsza wsi warownia frakcji napastnika (raz na sesje); bandy i Inni bez twierdz - bez kierunku
                    var fac = lp.MapFaction;
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
            s.Cur = PickFirst(d, s);
            d.S = s; d.SmokeIdx = -1;
            _dSess++;
            return s;
        }

        private static void CheckUp(Dist d, int w, int today, string raider, bool list)
        {
            double nw = d.Nw[w];
            if (!(nw > Eps)) return;
            if ((d.Flag[w] & FBurned) == 0 && d.Br[w] >= BurnShare * nw - 1e-12)
            {
                d.Flag[w] = FBurned; d.EvDay[w] = today; d.RebuiltDay[w] = -1; d.Event = true;
                if (list) { _dBurnedN++; if (_dBurnedList.Count < TopEvents) _dBurnedList.Add(d.VName[w] + " (" + d.Name + ") - " + (raider ?? "?")); }
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
        private static double Burn(Dist d, double dB, bool raid, Sess s, bool hasPos, float px, float py, int today, string who)
        {
            int cur;
            if (raid && s != null) cur = s.Cur >= 0 && Room(d, s.Cur) > Eps ? s.Cur : (s.Cur < 0 ? PickFirst(d, s) : NextFrom(d, s.Cur));
            else cur = hasPos ? NearestTo(d, px, py) : FirstByOrder(d);
            double left = dB;
            int guard = 0;
            while (left > Eps && cur >= 0 && guard++ <= d.W + 1)
            {
                double put = Math.Min(Room(d, cur), left);
                if (put > 0.0)
                {
                    d.Br[cur] += put; left -= put;
                    if (raid) { if (s != null) s.Cur = cur; if (cur == d.W) _dRaidMain += put; else _dRaidW += put; }
                    else { if (cur == d.W) _dForMain += put; else _dForW += put; }
                    CheckUp(d, cur, today, raid && s != null ? s.Raider : who, true);
                }
                if (left <= Eps) break;
                cur = NextFrom(d, cur);
            }
            return left > Eps ? left : 0.0;
        }

        // ------------------------------------------------------------ wpiecia w nasz kod (bez Harmony)
        /// <summary>
        /// Devastation.Strike PO kontach (U24; Z1, Z2): `hit` trafionych (po f), `refugees` dopisanych na konto, `lBefore` - ludzie okregu przed ruchem. Rabunek - `comp` =
        /// RaidEventComponent (klucz sesji, ten sam co MapEvent.Component widoku - krytyka K2); zerowanie - `src` (oboz armii). Nie rzuca.
        /// </summary>
        internal static void OnStrike(Village v, double hit, double refugees, bool raid, object comp, MobileParty src, double lBefore)
        {
            if (v == null || !(hit > 0.0) || double.IsInfinity(hit)) return;
            try
            {
                if (!Active) { _dHitSkip += hit; return; }
                lock (Gate)
                {
                    Dist d;
                    if (v.Settlement == null || v.Settlement.StringId == null || !_d.TryGetValue(v.Settlement.StringId, out d) || !EnsureN(d) || !(lBefore > 0.0)) { _dHitSkip += hit; return; }
                    int today = Today();
                    double dB = hit * d.N / lBefore;
                    if (refugees > 0.0 && !double.IsInfinity(refugees)) d.ARexp += refugees;
                    double over;
                    if (raid && comp != null) over = Burn(d, dB, true, SessionFor(d, comp, today), false, 0f, 0f, today, null);
                    else
                    {
                        bool hasPos = false; float x = 0f, y = 0f; string who = "zerowanie armii";
                        if (src != null)
                        {
                            try { var p = src.GetPosition2D; x = p.x; y = p.y; hasPos = true; if (src.Name != null) who = "zerowanie armii (" + src.Name + ")"; }
                            catch (Exception e) { Stumble("VillageBook.OnStrike(oboz)", e); }
                        }
                        over = Burn(d, dB, false, null, hasPos, x, y, today, who);
                    }
                    double perS = lBefore / d.N;                     // ludzi na osade przy tym uderzeniu
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
                    if (v.Settlement == null || v.Settlement.StringId == null || !_d.TryGetValue(v.Settlement.StringId, out d) || !EnsureN(d)) { _dFamSkip++; return; }
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
        /// ale nie wracaja do domu: tylko oczekiwane konto w dol, puste osady bez zmian (pojmani jak zabici - sa w osadach spalonych). Do E4/9 nikt tego nie wola.
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
                    if (v.Settlement == null || v.Settlement.StringId == null || !_d.TryGetValue(v.Settlement.StringId, out d)) return;
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
            return sn;
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
            away = (sn.Sbr > Eps ? sn.Ar * d.Br[w] / sn.Sbr : (w == d.W ? sn.Ar : 0.0)) + (sn.Sbf > Eps ? sn.Af * d.Bf[w] / sn.Sbf : (w == d.W ? sn.Af : 0.0));
        }

        private static int StateAt(Dist d, int w)
        {
            double nw = d.Nw[w], b = d.Br[w] + d.Bf[w];
            bool raid = false;
            try { raid = d.S != null && d.S.Cur == w && d.St.IsUnderRaid; } catch { raid = false; }
            if (raid) return SBurning;
            if ((d.Flag[w] & FBurned) != 0 && d.Br[w] >= KeepShare * nw) return SBurned;
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
                            double a0 = PeopleBook.RefugeesOf(d.V);
                            if (a0 > 0.0 || d.ARexp > 0.0 || d.AFexp > 0.0 || Sum(d.Br) > 0.0 || Sum(d.Bf) > 0.0) Reconcile(d, s, today);
                            // zdarzenia "odbudowana" (b < 1% osad)
                            for (int w = 0; w <= d.W; w++)
                            {
                                if (d.Flag[w] == 0 || d.Br[w] + d.Bf[w] >= StandShare * d.Nw[w]) continue;
                                int ev = d.EvDay[w];
                                d.Flag[w] = 0; d.RebuiltDay[w] = today; d.Event = true; _dRebuiltN++;
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
                double hitDev = Devastation.HitToday;
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
                  .Append(", bez ksiegi ").Append(F(_dHitSkip, "0.##")).Append("), doplyw bez zrodla d0 ").Append(_dD0);
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
            finally { NewDay(); }
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
                  .Append(records).Append(" w ").Append(with).Append(" okregach (odrzucone ").Append(_file.BadRows).Append(", wsi gry brak ").Append(_rowsNoVillage).Append(" wiosek w ").Append(_distNoVillage).Append(" okregach)");
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
                    d.ARexp = d.AFexp = 0.0; d.S = null; d.SmokeIdx = -1; d.Event = false;
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
                default: return "";
            }
        }

        private static string Tidy(string t) { return t.Replace("  ", " ").Replace(" :", ":"); }

        /// <summary>PeopleMenu.Consequence (E4/7, okno ShowInquiry): sekcja okregu wsi albo lista okregow warowni. null - ksiega nieczynna.</summary>
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
                        return DistrictText(d);
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

        private static string DistrictText(Dist d)
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
            var sb = new StringBuilder(1500);
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
            // wiersze: najpierw plonace, spalone, opuszczone, w odbudowie, czesciowo puste (wszystkie), potem stojace do limitu
            int[] rank = { 0, 1, 4, 3, 2, 5 };     // indeks = stan (SStanding, SDamaged, SBurned, SAbandoned, SRebuilding, SBurning) -> waga kolejnosci (malejaco)
            var order = Enumerable.Range(0, n).OrderByDescending(w => rank[states[w]]).ThenBy(w => w == d.W ? -1 : w).ToList();
            int lines = 0, moreStanding = 0;
            foreach (int w in order)
            {
                bool stand = states[w] == SStanding;
                if (stand && lines >= MaxLines) { moreStanding++; continue; }
                double home, away;
                People(d, sn, w, out home, out away);
                string name = d.VName[w], place = w == d.W ? "" : Place(d.Kind[w]);
                string line;
                int today = Today();
                switch (states[w])
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
                double share = sn.Dom > 0.0 ? 100.0 * sn.Y * home / sn.Dom : 0.0;
                sb.Append(Tidy(line)).Append(' ').Append(VillageTexts.Make(VillageTexts.DistShare, "SHARE", share.ToString("0.#", Inv)).ToString()).Append('\n');
                lines++;
            }
            if (moreStanding > 0) sb.Append(VillageTexts.Make(VillageTexts.DistMore, "N", moreStanding.ToString(Inv)).ToString()).Append('\n');
            return sb.ToString().TrimEnd('\n');
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
