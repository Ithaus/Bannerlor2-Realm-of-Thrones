using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// p6-szubienice (R3-07a, POMYSLY-35 pkt 17; Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak", "Rob od razu fale 2") - SZUBIENICE PRZY ROZSTAJACH. Czysty wyglad:
    /// nie zmienia ekonomii, walki, jencow ani relacji - zadne zloto, sztuka ani czlowiek nie przechodzi z rak do rak (nic do ksiegi Obieg).
    ///
    /// WYZWALACZ (jedna regula dla gracza i AI, cala mapa): koniec bitwy (MapEventEnded), w ktorej strona przegrana miala bande (MapFaction.IsBanditFaction, bez
    /// Nieumarlych - Undead.Party), a strona zwycieska ma "prawo": druzyne gracza, lorda (takze towarzysza z wlasna druzyna), patrol, zaloge albo milicje osady
    /// (karawana i wozy wsi same - nie wieszaja, licznik "bez pana"). Bitwa w kryjowce (IsHideoutBattle) = oczyszczenie kryjowki - miejscem jest kryjowka.
    ///
    /// MIEJSCE: najblizsze rozstaje od miejsca bitwy - wezly traktow ROT (koniec lamanej, w ktorym schodza sie co najmniej 3 odnogi; plik arm_map_roads_geo.tsv, ten sam
    /// co premia drog MapRoads) i wloty drog bocznych do wiosek (plan MapSideRoads widoczny na mapie, MapRoads.SidePlan: droga do traktu = rozstaje traktu, do innej
    /// bocznej = rozwidlenie, waga odleglosci x1.4) do 10 jedn. (kryjowka 15). Brak rozstajow - przy drodze najblizej bitwy (do 8 jedn., kryjowka 12). Brak drogi -
    /// nic (licznik "bez drogi"). Szubienica stoi obok drogi, nie na farbie: 0.6 (albo 0.9) jedn. od srodka rozstajow / osi drogi, w kierunku najdalszym od drog,
    /// na ladzie, z dala od osad gry (miasto 3, zamek 2, wies 1.5, kryjowka 1 jedn.), wiosek na mapie (1.3) i zajazdow (1.2); belka bokiem do rozstajow.
    ///
    /// OKREG = wies gry najblizsza szubienicy. Jedna szubienica na okreg: nowe rozbicie bandy w okregu z szubienica przedluza jej czas (i doklada wisielca), nie
    /// stawia drugiej. Czas: bitwa w polu 7 + ludzie bandy / 6 dob (najwyzej 14), kryjowka 14 dob. Wisielcy: do 9 ludzi bandy 1, do 24 - 2, wiecej i kryjowka - 3.
    /// Najwyzej 40 naraz w swiecie: przy pelnej liscie nowa zastepuje te, ktorej czas konczy sie najwczesniej (licznik "zastapione"). Koniec czasu = zdjeta (Daily).
    ///
    /// ZAPIS: SaveText.Sync "arm_gallows" ("g1|x;y;yaw;koniec;okreg;wisielcy;rodzaj;kryjowka~..."), pisany tylko z danymi; brak klucza (stary zapis, nowa kampania
    /// save040 Jeffa na WGRANIU 92) = brak szubienic. Reset (nowa kampania / przed wczytaniem) - osobna linia _p6SzubieniceReset w ArmouryBehavior.
    /// WIDOK: GallowsView (kopie siatek jak zajazdy - bez fizyki, skryptow, czastek; obrazek raz przy kamerze, potem tylko pokaz / schowaj, zdjecie przy zmianie
    /// listy - nic co klatke). Wszystko na watku glownym (zdarzenie bitwy, doba, widok) - bez blokad, poza goraca sciezka.
    /// Linie logu: "Szubienice (p6): start sesji", "Szubienice (p6): postawiona ..." (kazda przy gracza bitwie, z AI pierwsze 5 na sesje),
    /// "Szubienice (p6): dzien N | aktywne ..." (doba linii LogGate.Lines; liczniki od ostatniej linii). Klucz MCM MapGallows (TAK).
    /// </summary>
    internal static class Gallows
    {
        private const string P = "Szubienice (p6): ";
        internal const int MaxActive = 40;
        private const float CrossR = 10f, CrossRHideout = 15f, RoadR = 8f, RoadRHideout = 12f;
        private const float LaneForkWeight = 1.4f;     // rozwidlenie drog bocznych - dalej w kolejce niz rozstaje traktu
        private const float NodeJoin = 0.6f;           // konce lamanych traktu blizej niz tyle = ten sam wezel (szkielet farby)
        private const float Off1 = 0.6f, Off2 = 0.9f;  // odsuniecie szubienicy od srodka rozstajow / osi drogi
        private const float OffRoadMin = 0.42f;        // najmniej tyle od osi najblizszej drogi (pol-szerokosc farby ~0.35)
        private const float Cell = 8f;
        private const int CandMax = 8;
        private const int AiSampleLines = 5;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal sealed class Entry
        {
            public float X, Y, Yaw;        // polozenie (jedn. mapy) i obrot (rad; lokalna +Y ku rozstajom, belka wzdluz lokalnej X)
            public double End;             // doba konca (CampaignTime.ToDays)
            public string District = "";   // StringId wsi gry okregu
            public int Bodies = 1;         // 1..3
            public int Kind;               // 0 rozstaje traktu, 1 droga boczna do traktu, 2 rozwidlenie drog bocznych, 3 przy drodze
            public bool Hideout;
            public Settlement Village;     // wies okregu (widok: mgla zwiadu); po wczytaniu rozwiazuje widok (Settlement.Find)
        }

        private struct Cross { public float X, Y; public int Kind; }

        private static readonly List<Entry> _active = new List<Entry>();
        /// <summary>Zmiana listy (postawiona, zdjeta, zastapiona, wiecej wisielcow) - widok uzgadnia obrazki tylko wtedy.</summary>
        internal static int Version;
        internal static List<Entry> Active { get { return _active; } }

        // siec drog: odcinki (trakty [0, _segMain), potem drogi boczne co ok. 1 jedn.) i rozstaje, w komorkach Cell x Cell
        private static readonly List<float> _mainSeg = new List<float>(4 * 11000);     // trakty z pliku: ax, ay, bx, by
        private static readonly List<Cross> _mainCross = new List<Cross>(160);
        private static float[] _seg = new float[0];
        private static int _segN, _segMain;
        private static Cross[] _cross = new Cross[0];
        private static readonly Dictionary<long, List<int>> _segCells = new Dictionary<long, List<int>>();
        private static readonly Dictionary<long, List<int>> _crossCells = new Dictionary<long, List<int>>();
        private static object _planRef;
        private static bool _netBuilt, _mainLoaded;
        private static string _fileNote = "nie wczytany";
        private static int _lanes, _laneCross, _laneFork;

        // przeszkody: osady gry, wioski na mapie, zajazdy (x, y, promien) i wsie gry (okregi)
        private static readonly List<float> _blk = new List<float>(3 * 3000);
        private static readonly Dictionary<long, List<int>> _blkCells = new Dictionary<long, List<int>>();
        private static readonly List<Settlement> _villages = new List<Settlement>(600);
        private static bool _blkBuilt;
        private static int _blkMv;                      // wioski na mapie w przeszkodach (0 - uklad nie byl jeszcze wczytany: nastepne uzycie buduje od nowa)
        private static string _blkNote = "-";

        // liczniki od ostatniej linii doby
        private static int _dPlaced, _dCross, _dLane, _dFork, _dRoad, _dRefresh, _dReplaced, _dRemoved, _dNoRoad, _dNoLaw, _dUndead, _dPlayer, _dHideout, _dEvents;
        private static int _aPlaced, _aRemoved, _aRefresh, _aReplaced, _aiLines;
        private static int _stumbles, _stumblesAll;
        private static double _pickMs;
        private static int _pickN;
        private static double _netMs, _loadMs;         // ostatnia przebudowa sieci (drogi boczne) i wczytanie traktow - poza srednia wyboru miejsca
        private static int _netBuilds, _blkBuilds;
        private static int _loadedFromSave, _expiredOnLoad;
        // widok (bez typow SandBox.View - linia doby czyta je, gdy typ widoku sie nie zaladowal)
        internal static int VCreated, VShowDay, VHideDay, VRemoved, VShownMax, VMeshMiss, VFailed, VStCreate, VStVis, VPiecesDay;

        internal static bool On { get { var s = Settings.Current; return s != null && s.MapGallows; } }

        // ------------------------------------------------------------------ stan kampanii
        internal static void Reset()
        {
            _active.Clear();
            Version++;
            _planRef = null; _netBuilt = false; _mainLoaded = false; _fileNote = "nie wczytany";
            _mainSeg.Clear(); _mainCross.Clear(); _segCells.Clear(); _crossCells.Clear();
            _seg = new float[0]; _segN = 0; _segMain = 0; _cross = new Cross[0];
            _lanes = _laneCross = _laneFork = 0;
            _blk.Clear(); _blkCells.Clear(); _villages.Clear(); _blkBuilt = false; _blkNote = "-"; _blkMv = 0;
            _netMs = _loadMs = 0; _netBuilds = _blkBuilds = 0;
            ZeroDay();
            _aPlaced = _aRemoved = _aRefresh = _aReplaced = _aiLines = 0;
            _stumbles = _stumblesAll = 0;
            _loadedFromSave = _expiredOnLoad = 0;
            VCreated = VRemoved = VShownMax = VMeshMiss = VFailed = VStCreate = VStVis = 0;
        }

        private static void ZeroDay()
        {
            _dPlaced = _dCross = _dLane = _dFork = _dRoad = _dRefresh = _dReplaced = _dRemoved = _dNoRoad = _dNoLaw = _dUndead = _dPlayer = _dHideout = _dEvents = 0;
            _pickMs = 0; _pickN = 0;
            VShowDay = VHideDay = VPiecesDay = 0;
            _stumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;
            if (_stumblesAll <= 3 || _stumblesAll % 200 == 0) Log.Error("Gallows." + where + " (potkniecie " + _stumblesAll + ")", e);
        }

        private static double Now() { try { return CampaignTime.Now.ToDays; } catch { return 0; } }

        internal static string Export()
        {
            if (_active.Count == 0) return null;
            var sb = new StringBuilder("g1|");
            for (int i = 0; i < _active.Count; i++)
            {
                var e = _active[i];
                if (i > 0) sb.Append('~');
                sb.Append(e.X.ToString("0.###", Inv)).Append(';').Append(e.Y.ToString("0.###", Inv)).Append(';').Append(e.Yaw.ToString("0.###", Inv)).Append(';')
                  .Append(e.End.ToString("0.###", Inv)).Append(';').Append(e.District).Append(';').Append(e.Bodies).Append(';').Append(e.Kind).Append(';')
                  .Append(e.Hideout ? '1' : '0');
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _active.Clear();
            Version++;
            _loadedFromSave = 0;
            if (string.IsNullOrEmpty(s) || !s.StartsWith("g1|", StringComparison.Ordinal)) return;
            foreach (var part in s.Substring(3).Split('~'))
            {
                try
                {
                    var c = part.Split(';');
                    if (c.Length < 8) continue;
                    var e = new Entry
                    {
                        X = float.Parse(c[0], Inv), Y = float.Parse(c[1], Inv), Yaw = float.Parse(c[2], Inv), End = double.Parse(c[3], Inv), District = c[4],
                        Bodies = Math.Max(1, Math.Min(3, int.Parse(c[5], Inv))), Kind = Math.Max(0, Math.Min(3, int.Parse(c[6], Inv))), Hideout = c[7] == "1"
                    };
                    if (float.IsNaN(e.X) || float.IsNaN(e.Y) || _active.Count >= MaxActive) continue;
                    _active.Add(e);
                    _loadedFromSave++;
                }
                catch { }
            }
        }

        // ------------------------------------------------------------------ start sesji (OnSessionLaunched, po McmSettings.Apply)
        internal static void SessionStart()
        {
            var s = Settings.Current;
            try
            {
                // przeterminowane w zapisie (gra stala dluzej niz czas szubienicy - np. zapis z innej doby) - zdjete od razu
                double now = Now();
                _expiredOnLoad = 0;
                for (int i = _active.Count - 1; i >= 0; i--) if (_active[i].End <= now) { _active.RemoveAt(i); _expiredOnLoad++; }
                if (_expiredOnLoad > 0) Version++;
                LoadMain();
                Log.Info(P + "start sesji - klucz MapGallows " + (s != null && s.MapGallows ? "tak" : "NIE (szubienic nie przybywa, stojace schowane)") + " | " + _fileNote
                         + " | z zapisu aktywnych " + _active.Count + " (wczytanych " + _loadedFromSave + ", po czasie zdjetych " + _expiredOnLoad + ")"
                         + " | regula: rozbita banda (gracz, lord, patrol, zaloga, milicja) albo oczyszczona kryjowka -> rozstaje do " + CrossR.ToString("0", Inv) + " jedn. (kryjowka "
                         + CrossRHideout.ToString("0", Inv) + "), inaczej droga do " + RoadR.ToString("0", Inv) + " (" + RoadRHideout.ToString("0", Inv) + "); 7-14 dob, jedna na okreg, najwyzej "
                         + MaxActive + " naraz; czysty wyglad (nic do ksiegi Obieg).");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        /// <summary>Trakty z pliku drog ROT: odcinki i wezly (konce lamanych, w ktorych schodza sie >= 3 odnogi, takze koniec na srodku innej lamanej = T).</summary>
        private static void LoadMain()
        {
            if (_mainLoaded) return;
            _mainLoaded = true;
            _mainSeg.Clear(); _mainCross.Clear();
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                string path = MapVillageData.DataPathFor(MapRoads.FileName);
                if (path == null || !File.Exists(path)) { _fileNote = "BRAK pliku " + MapRoads.FileName + " - rozstaje tylko z drog bocznych"; return; }
                var lines = new List<List<Vec2>>(320);
                int last = int.MinValue, bad = 0;
                foreach (string raw in File.ReadAllText(path, Encoding.UTF8).Replace("\r", "").Split('\n'))
                {
                    if (raw.Length == 0 || raw[0] == '#') continue;
                    var c = raw.Split('\t');
                    if (c.Length < 4) { bad++; continue; }
                    int id; float x, y;
                    if (!int.TryParse(c[0], NumberStyles.Integer, Inv, out id) || !float.TryParse(c[2], NumberStyles.Float, Inv, out x) || !float.TryParse(c[3], NumberStyles.Float, Inv, out y)
                        || float.IsNaN(x) || float.IsNaN(y)) { bad++; continue; }
                    if (id != last || lines.Count == 0) { lines.Add(new List<Vec2>()); last = id; }
                    lines[lines.Count - 1].Add(new Vec2(x, y));
                }
                foreach (var l in lines)
                    for (int i = 1; i < l.Count; i++) { _mainSeg.Add(l[i - 1].x); _mainSeg.Add(l[i - 1].y); _mainSeg.Add(l[i].x); _mainSeg.Add(l[i].y); }
                // wezly: konce lamanych sklejone do NodeJoin; stopien = liczba koncow + 2 za kazda inna lamana przechodzaca przez wezel srodkiem
                var nx = new List<float>(); var ny = new List<float>(); var deg = new List<int>(); var own = new List<HashSet<int>>();
                for (int li = 0; li < lines.Count; li++)
                {
                    var l = lines[li];
                    if (l.Count < 2) continue;
                    for (int k = 0; k < 2; k++)
                    {
                        Vec2 p = k == 0 ? l[0] : l[l.Count - 1];
                        int hit = -1;
                        for (int n = 0; n < nx.Count; n++) { float dx = nx[n] - p.x, dy = ny[n] - p.y; if (dx * dx + dy * dy <= NodeJoin * NodeJoin) { hit = n; break; } }
                        if (hit < 0) { nx.Add(p.x); ny.Add(p.y); deg.Add(1); own.Add(new HashSet<int> { li }); }
                        else { deg[hit]++; own[hit].Add(li); }
                    }
                }
                for (int n = 0; n < nx.Count; n++)
                {
                    for (int li = 0; li < lines.Count; li++)
                    {
                        if (own[n].Contains(li)) continue;
                        var l = lines[li];
                        if (l.Count < 2) continue;
                        float d0x = l[0].x - nx[n], d0y = l[0].y - ny[n], d1x = l[l.Count - 1].x - nx[n], d1y = l[l.Count - 1].y - ny[n];
                        if (d0x * d0x + d0y * d0y <= NodeJoin * NodeJoin || d1x * d1x + d1y * d1y <= NodeJoin * NodeJoin) continue;
                        bool near = false;
                        for (int i = 1; i < l.Count && !near; i++)
                            near = SegDist2(nx[n], ny[n], l[i - 1].x, l[i - 1].y, l[i].x, l[i].y) <= NodeJoin * NodeJoin;
                        if (near) { deg[n] += 2; own[n].Add(li); }
                    }
                    if (deg[n] >= 3) _mainCross.Add(new Cross { X = nx[n], Y = ny[n], Kind = 0 });
                }
                _fileNote = "plik " + MapRoads.FileName + ": lamanych " + lines.Count + ", odcinkow " + (_mainSeg.Count / 4) + ", wezlow " + nx.Count + ", rozstajow traktu "
                            + _mainCross.Count + " (>= 3 odnogi), zle wiersze " + bad;
            }
            catch (Exception e) { _mainSeg.Clear(); _mainCross.Clear(); _fileNote = "blad wczytania " + MapRoads.FileName + " (" + e.GetType().Name + ")"; Stumble("LoadMain", e); }
            _loadMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            _fileNote += ", wczytanie " + _loadMs.ToString("0.0", Inv) + " ms";
            _netBuilt = false;
        }

        private static float SegDist2(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
            float t = l2 > 1e-8f ? ((px - ax) * dx + (py - ay) * dy) / l2 : 0f;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            float ex = px - ax - t * dx, ey = py - ay - t * dy;
            return ex * ex + ey * ey;
        }

        private static long Key(int cx, int cy) { return ((long)cx << 32) ^ (uint)cy; }
        private static int CellOf(float v) { return (int)Math.Floor(v / Cell); }

        private static void AddCell(Dictionary<long, List<int>> map, long k, int i)
        {
            List<int> l;
            if (!map.TryGetValue(k, out l)) map[k] = l = new List<int>(8);
            l.Add(i);
        }

        /// <summary>Siec drog dla wyboru miejsca: trakty z pliku + drogi boczne z planu widoku (przebudowa tylko, gdy plan sie zmienil).</summary>
        private static void EnsureNet()
        {
            if (!_mainLoaded) LoadMain();
            object plan = null;
            SideRoadPlan.Result res = null;
            try { res = MapRoads.SidePlan(); plan = res; } catch { res = null; plan = null; }
            if (_netBuilt && ReferenceEquals(plan, _planRef)) return;
            _planRef = plan;
            _netBuilt = true;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var seg = new List<float>(_mainSeg.Count + 4 * 32000);
            seg.AddRange(_mainSeg);
            var cross = new List<Cross>(_mainCross.Count + 2200);
            cross.AddRange(_mainCross);
            _lanes = _laneCross = _laneFork = 0;
            if (res != null)
            {
                foreach (var r in res.Roads)
                {
                    if (r == null || r.X == null || r.Y == null) continue;
                    int n = Math.Min(r.X.Length, r.Y.Length);
                    if (n < 2) continue;
                    _lanes++;
                    for (int i = 0; i < n - 1; i += 4)
                    {
                        int j = Math.Min(i + 4, n - 1);
                        seg.Add(r.X[i]); seg.Add(r.Y[i]); seg.Add(r.X[j]); seg.Add(r.Y[j]);
                    }
                    if (r.Join == 0) { cross.Add(new Cross { X = r.X[n - 1], Y = r.Y[n - 1], Kind = 1 }); _laneCross++; }
                    else if (r.Join == 1) { cross.Add(new Cross { X = r.X[n - 1], Y = r.Y[n - 1], Kind = 2 }); _laneFork++; }
                }
            }
            _seg = seg.ToArray(); _segN = _seg.Length / 4; _segMain = _mainSeg.Count / 4;
            _cross = cross.ToArray();
            _segCells.Clear(); _crossCells.Clear();
            for (int k = 0; k < _segN; k++)
            {
                float ax = _seg[4 * k], ay = _seg[4 * k + 1], bx = _seg[4 * k + 2], by = _seg[4 * k + 3];
                int x0 = CellOf(Math.Min(ax, bx) - 1f), x1 = CellOf(Math.Max(ax, bx) + 1f), y0 = CellOf(Math.Min(ay, by) - 1f), y1 = CellOf(Math.Max(ay, by) + 1f);
                if (x1 - x0 > 64 || y1 - y0 > 64) continue;   // odcinek-olbrzym (blad danych) - pominiety
                for (int cx = x0; cx <= x1; cx++) for (int cy = y0; cy <= y1; cy++) AddCell(_segCells, Key(cx, cy), k);
            }
            for (int k = 0; k < _cross.Length; k++) AddCell(_crossCells, Key(CellOf(_cross[k].X), CellOf(_cross[k].Y)), k);
            _netMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            _netBuilds++;
        }

        /// <summary>Przeszkody (osady gry, wioski na mapie, zajazdy) i wsie gry - raz na sesje, przy pierwszym uzyciu.</summary>
        private static void EnsureBlockers()
        {
            if (_blkBuilt)
            {
                // uklad wiosek doszedl po pierwszym uzyciu (wczesniej pusty) - przeszkody jeszcze raz, tylko wtedy
                var lr = _blkMv > 0 ? null : VillageBook.LayoutRecords();
                if (lr == null || lr.Count == 0) return;
            }
            _blkBuilt = true;
            _blkBuilds++;
            _blk.Clear(); _blkCells.Clear(); _villages.Clear();
            int st = 0, mv = 0, inns = 0;
            try
            {
                foreach (var se in Settlement.All)
                {
                    if (se == null) continue;
                    float r = se.IsTown ? 3f : se.IsCastle ? 2f : se.IsVillage ? 1.5f : se.IsHideout ? 1f : 1.5f;
                    var p = se.GetPosition2D;
                    AddBlk(p.x, p.y, r); st++;
                    if (se.IsVillage && se.Village != null) _villages.Add(se);
                }
            }
            catch (Exception e) { Stumble("Blockers.Settlements", e); }
            try
            {
                var recs = VillageBook.LayoutRecords();
                if (recs != null) foreach (var r in recs) { if (r == null) continue; AddBlk(r.X, r.Y, 1.3f); mv++; }
            }
            catch (Exception e) { Stumble("Blockers.MapVillages", e); }
            try
            {
                var all = Settings.Current != null && Settings.Current.RoadsideInns ? RoadsideInns.All : null;   // zajazdy widoczne tylko przy kluczu (wczytuje je ich start sesji)
                if (all != null) foreach (var inn in all) { if (inn == null) continue; AddBlk(inn.Bx, inn.By, 1.2f); inns++; }
            }
            catch (Exception e) { Stumble("Blockers.Inns", e); }
            _blkMv = mv;
            _blkNote = "osady " + st + ", wioski na mapie " + mv + ", zajazdy " + inns + ", wsie gry (okregi) " + _villages.Count + (_blkBuilds > 1 ? " (budowane " + _blkBuilds + " razy)" : "");
        }

        private static void AddBlk(float x, float y, float r)
        {
            int i = _blk.Count / 3;
            _blk.Add(x); _blk.Add(y); _blk.Add(r);
            int x0 = CellOf(x - r), x1 = CellOf(x + r), y0 = CellOf(y - r), y1 = CellOf(y + r);
            for (int cx = x0; cx <= x1; cx++) for (int cy = y0; cy <= y1; cy++) AddCell(_blkCells, Key(cx, cy), i);
        }

        private static bool Blocked(float x, float y)
        {
            List<int> l;
            if (!_blkCells.TryGetValue(Key(CellOf(x), CellOf(y)), out l)) return false;
            foreach (int i in l)
            {
                float dx = _blk[3 * i] - x, dy = _blk[3 * i + 1] - y, r = _blk[3 * i + 2];
                if (dx * dx + dy * dy < r * r) return true;
            }
            return false;
        }

        /// <summary>Najblizszy punkt drogi w promieniu r (komorki sieci); false - brak drogi.</summary>
        private static bool NearestRoad(float x, float y, float r, out float qx, out float qy, out float d)
        {
            qx = qy = 0f; d = float.MaxValue;
            int x0 = CellOf(x - r), x1 = CellOf(x + r), y0 = CellOf(y - r), y1 = CellOf(y + r);
            for (int cx = x0; cx <= x1; cx++)
                for (int cy = y0; cy <= y1; cy++)
                {
                    List<int> l;
                    if (!_segCells.TryGetValue(Key(cx, cy), out l)) continue;
                    foreach (int k in l)
                    {
                        float ax = _seg[4 * k], ay = _seg[4 * k + 1], dx = _seg[4 * k + 2] - ax, dy = _seg[4 * k + 3] - ay;
                        float l2 = dx * dx + dy * dy;
                        float t = l2 > 1e-8f ? ((x - ax) * dx + (y - ay) * dy) / l2 : 0f;
                        if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                        float px = ax + t * dx, py = ay + t * dy, ex = x - px, ey = y - py;
                        float dd = ex * ex + ey * ey;
                        if (dd < d) { d = dd; qx = px; qy = py; }
                    }
                }
            if (d == float.MaxValue) return false;
            d = (float)Math.Sqrt(d);
            return d <= r;
        }

        /// <summary>Odleglosc punktu od osi najblizszej drogi (do 2 jedn.; dalej = 2).</summary>
        private static float RoadClearance(float x, float y)
        {
            float qx, qy, d;
            return NearestRoad(x, y, 2f, out qx, out qy, out d) ? d : 2f;
        }

        private static bool Land(float x, float y)
        {
            try
            {
                var ms = Campaign.Current != null ? Campaign.Current.MapSceneWrapper : null;
                if (ms == null) return true;
                var cv = new CampaignVec2(new Vec2(x, y), true);
                PathFaceRecord f = ms.GetFaceIndex(in cv);
                return f.IsValid() && MapVillageData.IsLandGroup(f.FaceGroupIndex);
            }
            catch { return true; }
        }

        /// <summary>Miejsce szubienicy obok punktu (cx, cy): 8 kierunkow na Off1, potem Off2 - najdalej od drog (co najmniej OffRoadMin), na ladzie, poza przeszkodami.
        /// Pierwszy kierunek - ku miejscu bitwy (bx, by). yaw: lokalna +Y ku (cx, cy).</summary>
        private static bool Place(float cx, float cy, float bx, float by, out float px, out float py, out float yaw)
        {
            px = py = yaw = 0f;
            float a0 = (float)Math.Atan2(by - cy, bx - cx);
            if (float.IsNaN(a0)) a0 = 0f;
            foreach (float off in new[] { Off1, Off2 })
            {
                float best = -1f, bxp = 0f, byp = 0f;
                for (int k = 0; k < 8; k++)
                {
                    float a = a0 + k * (float)Math.PI / 4f;
                    float x = cx + off * (float)Math.Cos(a), y = cy + off * (float)Math.Sin(a);
                    if (Blocked(x, y)) continue;
                    float clr = RoadClearance(x, y);
                    if (clr < OffRoadMin || clr <= best) continue;
                    if (!Land(x, y)) continue;
                    best = clr; bxp = x; byp = y;
                }
                if (best > 0f)
                {
                    px = bxp; py = byp;
                    float vx = cx - px, vy = cy - py;
                    yaw = (float)Math.Atan2(-vx, vy);
                    return true;
                }
            }
            return false;
        }

        private static Settlement DistrictOf(float x, float y)
        {
            Settlement best = null;
            float bd = float.MaxValue;
            foreach (var v in _villages)
            {
                var p = v.GetPosition2D;
                float dx = p.x - x, dy = p.y - y, d = dx * dx + dy * dy;
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }

        private static readonly List<KeyValuePair<float, int>> _cand = new List<KeyValuePair<float, int>>(64);

        /// <summary>Miejsce dla bitwy w (bx, by): rozstaje (najblizsze wazone, do CandMax prob), potem droga; false - brak.</summary>
        private static bool Pick(float bx, float by, bool hideout, out float px, out float py, out float yaw, out int kind)
        {
            px = py = yaw = 0f; kind = -1;
            float R = hideout ? CrossRHideout : CrossR;
            _cand.Clear();
            int x0 = CellOf(bx - R), x1 = CellOf(bx + R), y0 = CellOf(by - R), y1 = CellOf(by + R);
            for (int cx = x0; cx <= x1; cx++)
                for (int cy = y0; cy <= y1; cy++)
                {
                    List<int> l;
                    if (!_crossCells.TryGetValue(Key(cx, cy), out l)) continue;
                    foreach (int k in l)
                    {
                        float dx = _cross[k].X - bx, dy = _cross[k].Y - by, d = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (d > R) continue;
                        if (_cross[k].Kind == 2) d *= LaneForkWeight;
                        _cand.Add(new KeyValuePair<float, int>(d, k));
                    }
                }
            _cand.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < _cand.Count && i < CandMax; i++)
            {
                var c = _cross[_cand[i].Value];
                if (Place(c.X, c.Y, bx, by, out px, out py, out yaw)) { kind = c.Kind; return true; }
            }
            float qx, qy, d0;
            if (NearestRoad(bx, by, hideout ? RoadRHideout : RoadR, out qx, out qy, out d0) && Place(qx, qy, bx, by, out px, out py, out yaw)) { kind = 3; return true; }
            return false;
        }

        // ------------------------------------------------------------------ zdarzenie bitwy (watek glowny)
        internal static void OnMapEventEnded(MapEvent me)
        {
            if (me == null || !On) return;
            try
            {
                var win = me.WinningSide;
                if (win == BattleSideEnum.None) return;
                var winSide = win == BattleSideEnum.Attacker ? me.AttackerSide : me.DefenderSide;
                var lose = win == BattleSideEnum.Attacker ? me.DefenderSide : me.AttackerSide;
                if (lose == null || lose.Parties == null || winSide == null || winSide.Parties == null) return;
                int men = 0;
                bool band = false, undead = false;
                foreach (var mep in lose.Parties)
                {
                    var pb = mep != null ? mep.Party : null;
                    var f = pb != null ? pb.MapFaction : null;
                    if (f == null || !f.IsBanditFaction) continue;
                    if (pb.MobileParty != null && Undead.Party(pb.MobileParty)) { undead = true; continue; }
                    band = true;
                    men += Math.Max(0, mep.HealthyManCountAtStart);
                }
                if (!band) { if (undead) _dUndead++; return; }
                _dEvents++;
                bool law = false;
                foreach (var mep in winSide.Parties)
                {
                    var pb = mep != null ? mep.Party : null;
                    if (pb == null) continue;
                    var f = pb.MapFaction;
                    if (f != null && f.IsBanditFaction) continue;
                    if (pb.IsSettlement) { law = true; break; }
                    var mp = pb.MobileParty;
                    if (mp != null && (mp.IsMainParty || mp.IsLordParty || mp.IsPatrolParty || mp.IsGarrison || mp.IsMilitia)) { law = true; break; }
                }
                if (!law) { _dNoLaw++; return; }
                bool hideout = me.IsHideoutBattle;
                Vec2 at = hideout && me.MapEventSettlement != null ? me.MapEventSettlement.GetPosition2D : me.Position.ToVec2();
                double days = hideout ? 14.0 : Math.Min(14.0, 7.0 + men / 6.0);
                int bodies = hideout ? 3 : men < 10 ? 1 : men < 25 ? 2 : 3;
                bool player = me.IsPlayerMapEvent;
                if (player) _dPlayer++;
                if (hideout) _dHideout++;
                Raise(at.x, at.y, hideout, days, bodies, men, player, me);
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        private static void Raise(float bx, float by, bool hideout, double days, int bodies, int men, bool player, MapEvent me)
        {
            EnsureNet();
            EnsureBlockers();
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            float px, py, yaw;
            int kind;
            bool ok = Pick(bx, by, hideout, out px, out py, out yaw, out kind);
            _pickMs += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            _pickN++;
            if (!ok) { _dNoRoad++; return; }
            var dist = DistrictOf(px, py);
            string did = dist != null ? dist.StringId : "";
            double now = Now(), end = now + days;
            string what;
            Entry e = null;
            if (did.Length > 0) foreach (var a in _active) if (a.District == did) { e = a; break; }
            if (e != null)
            {
                // jedna na okreg: przedluzenie czasu i wisielcy dokladani (najwyzej 3) - bez drugiej szubienicy
                if (end > e.End) e.End = end;
                int nb = Math.Min(3, Math.Max(e.Bodies + 1, bodies));   // kazda nastepna rozbita banda okregu - jeden wisielec wiecej
                if (nb != e.Bodies) { e.Bodies = nb; Version++; }
                if (hideout) e.Hideout = true;
                if (e.Village == null) e.Village = dist;
                _dRefresh++; _aRefresh++;
                what = "odnowiona";
            }
            else
            {
                if (_active.Count >= MaxActive)
                {
                    int oi = 0;
                    for (int i = 1; i < _active.Count; i++) if (_active[i].End < _active[oi].End) oi = i;
                    _active.RemoveAt(oi);
                    _dReplaced++; _aReplaced++;
                }
                e = new Entry { X = px, Y = py, Yaw = yaw, End = end, District = did, Bodies = bodies, Kind = kind, Hideout = hideout, Village = dist };
                _active.Add(e);
                Version++;
                _dPlaced++; _aPlaced++;
                if (kind == 0) _dCross++; else if (kind == 1) _dLane++; else if (kind == 2) _dFork++; else _dRoad++;
                what = "postawiona";
            }
            if (player || _aiLines < AiSampleLines)
            {
                if (!player) _aiLines++;
                string who = "-";
                try { var lp = me.GetLeaderParty(me.WinningSide); who = lp != null && lp.Name != null ? lp.Name.ToString() : "-"; } catch { }
                Log.Info(P + what + " " + KindText(e.Kind) + " w okregu " + (dist != null ? dist.Name.ToString() : "-") + " (" + e.X.ToString("0.0", Inv) + ", " + e.Y.ToString("0.0", Inv)
                         + "; bitwa " + bx.ToString("0.0", Inv) + ", " + by.ToString("0.0", Inv) + ", odleglosc " + new Vec2(bx, by).Distance(new Vec2(e.X, e.Y)).ToString("0.0", Inv)
                         + ") | " + (hideout ? "kryjowka oczyszczona" : "banda rozbita") + ", ludzi bandy " + men + ", zwyciezca " + who + (player ? " (gracz)" : " (AI)")
                         + " | do doby " + e.End.ToString("0.0", Inv) + ", wisielcow " + e.Bodies + " | aktywne " + _active.Count + "/" + MaxActive + ".");
            }
        }

        private static string KindText(int k)
        {
            return k == 0 ? "przy rozstajach traktu" : k == 1 ? "przy rozstajach (droga do wsi)" : k == 2 ? "przy rozwidleniu drog wiejskich" : "przy drodze";
        }

        // ------------------------------------------------------------------ doba (watek glowny)
        internal static void Daily()
        {
            try
            {
                double now = Now();
                for (int i = _active.Count - 1; i >= 0; i--)
                    if (_active[i].End <= now) { _active.RemoveAt(i); _dRemoved++; _aRemoved++; Version++; }
                if (!LogGate.Lines) return;
                int day = (int)now - 1;   // doba zakonczona (jak linie drog i blota)
                int k0 = 0, k1 = 0, k2 = 0, k3 = 0, hid = 0, b = 0;
                var districts = new HashSet<string>(StringComparer.Ordinal);
                foreach (var e in _active)
                {
                    if (e.Kind == 0) k0++; else if (e.Kind == 1) k1++; else if (e.Kind == 2) k2++; else k3++;
                    if (e.Hideout) hid++;
                    b += e.Bodies;
                    districts.Add(e.District);
                }
                Log.Info(P + "dzien " + day + " | klucz MapGallows " + (On ? "tak" : "NIE") + " | aktywne " + _active.Count + " (maks. " + MaxActive + "), okregow " + districts.Count
                         + ", przy rozstajach traktu " + k0 + ", przy rozstajach drogi do wsi " + k1 + ", przy rozwidleniu " + k2 + ", przy drodze " + k3 + ", po kryjowkach " + hid + ", wisielcow " + b
                         + " | od ostatniej linii: bitwy z bandami " + _dEvents + " (gracza " + _dPlayer + ", kryjowki " + _dHideout + "), postawione " + _dPlaced + " (rozstaje traktu " + _dCross
                         + ", droga do wsi " + _dLane + ", rozwidlenie " + _dFork + ", przy drodze " + _dRoad + "), odnowione " + _dRefresh + ", zastapione " + _dReplaced + ", zdjete " + _dRemoved
                         + " (koniec czasu), pominiete: bez drogi " + _dNoRoad + ", bez pana (karawana / wozy) " + _dNoLaw + ", Nieumarli " + _dUndead
                         + " | wybor miejsca srednio " + (_pickN > 0 ? (_pickMs / _pickN).ToString("0.00", Inv) : "-") + " ms (" + _pickN + ")"
                         + " | siec: rozstaje traktu " + _mainCross.Count + ", drogi boczne " + _lanes + " (wloty do traktu " + _laneCross + ", rozwidlenia " + _laneFork + ")"
                         + ", przebudowy " + _netBuilds + (_netBuilds > 0 ? " (ostatnia " + _netMs.ToString("0.0", Inv) + " ms)" : "")
                         + (_planRef == null ? " - plan drog bocznych niedostepny" : "") + "; przeszkody: " + _blkNote
                         + " | od wczytania: postawione " + _aPlaced + ", odnowione " + _aRefresh + ", zastapione " + _aReplaced + ", zdjete " + _aRemoved
                         + " | widok: obrazki postawione " + VCreated + ", zdjete " + VRemoved + ", pokazane najwiecej naraz " + VShownMax + ", pokaz/schowaj " + VShowDay + "/" + VHideDay
                         + ", elementow " + VPiecesDay + ", brak siatek " + VMeshMiss + ", pominiete " + VFailed + ", potkniecia widoku: tworzenie " + VStCreate + ", widocznosc " + VStVis
                         + " | potkniecia " + _stumbles + " (od wczytania " + _stumblesAll + ").");
                ZeroDay();
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
