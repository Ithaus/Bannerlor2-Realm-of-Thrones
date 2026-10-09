// PROBA P2 (09.10, raport audytu 08 D-1): drogi nieutwardzone na mapie jako "stemple" sciezek - te same decale, ktorymi ROT
// rysuje uliczki w swoich osadach (decal_battania_path_a/b ...). TYLKO WIDOK: drogi nie zmieniaja predkosci ani tras partii.
// TYLKO dwa okna 50 x 50 jedn. (okolice Fairmarket i Winterfell) z pliku ModuleData\arm_map_roads_probe.tsv (generator
// s6_siec_gra.py; mod niczego nie liczy, czyta gotowe stemple: polozenie, kierunek, wielkosc pudla, material, kolor).
//
// Dwie drogi postawienia stempla (wybor w naglowku pliku "# method: auto|prefab|code", domyslnie auto):
//  - prefab: wlasny prefab z samym decal_component (Armoury\Prefabs\arm_map_roads.xml, wzor 1:1 map_track_arrow z Native
//    map_icons.xml) + GameEntity.Instantiate i ramka jak tropy partii (MapTracksVisualManager.GetGameEntity / CalculateTrackFrame);
//  - code: GameEntity.CreateEmpty + Decal.CreateDecal + SetMaterial + AddComponent + SetGlobalFrame + AddDecalInstance - kolejnosc
//    1:1 jak kola machin oblezniczych gry (SettlementVisualManager, tworzone i zdejmowane w czasie gry);
//  - auto: prefab, gdy gra zna prefab tego materialu (plik prefabu wgrany), inaczej code.
//
// Zasady (CLAUDE.md 7): co klatke tylko licznik czasu klatki; praca 4 razy na sekunde; stemple stawiane raz (budzet czasu na
// tick), widocznosc zmieniana TYLKO przy zmianie poziomu szczegolow (odleglosc kamery), nic co klatke na wizerunkach.
// Potkniecia liczone (zly stempel = pominiety), nigdy globalny wylacznik. Nic nie idzie do zapisu gry.
//
// Wylacznik: MCM "Map Roads Probe Enabled" (domyslnie WYLACZONY) albo stala ForceOnInProbeBuild (TYLKO w probnym buildzie
// galezi w-toku/p2-drogi-proba = true; przy przenoszeniu do glownej linii MUSI byc false).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SandBox.View;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>Dane proby drog (bez typow SandBox.View - bezpieczne do wolania co klatke z SubModuleMain).</summary>
    internal static class MapRoadsProbe
    {
        /// <summary>PROBA P2: true tylko w probnym buildzie (autotest ze zdjeciami). Poza proba - false (wtedy tylko MCM).</summary>
        internal const bool ForceOnInProbeBuild = true;

        internal const string FileName = "arm_map_roads_probe.tsv";
        internal const int MaxStamps = 4000;          // bezpiecznik: okno 50 x 50 ma do ok. 700 stempli (s6: max 707 na komorke)

        internal static bool On
        {
            get { return ForceOnInProbeBuild || (Settings.Current != null && Settings.Current.MapRoadsProbeEnabled); }
        }

        internal struct Stamp
        {
            public float X, Y, Yaw, Across, Along;
            public byte Class;        // 0 trakt, 1 wiejska, 2 polna, 3 sciezka (LOD: dalej tylko 0-1)
            public byte Look;         // 0 trakt, 1 wiejska, 2 polna, 3 sciezka, 4 lesna
            public string Material;
            public uint Factor;
            public int Window;
        }

        internal sealed class Window
        {
            public string Name = "";
            public float X0, Y0, X1, Y1;
            public bool Contains(float x, float y, float margin) { return x >= X0 - margin && x < X1 + margin && y >= Y0 - margin && y < Y1 + margin; }
        }

        internal sealed class ProbeFile
        {
            public readonly List<Stamp> Stamps = new List<Stamp>();
            public readonly List<Window> Windows = new List<Window>();
            public string Method = "auto";
            public float YawOffsetDeg;
            public string LayoutCrc = "", SceneCrc = "";
            public int BadRows, OutsideWindows, OverCap, DataRows;
        }

        // ---------- stan sesji (nic z kampanii; Reset w konstruktorze ArmouryBehavior - nowa kampania czyta plik od nowa) ----------
        private static ProbeFile _cached;
        private static string _cachedPath;

        internal static void Reset()
        {
            _cached = null;
            _cachedPath = null;
        }

        internal static ProbeFile Load(string path)
        {
            if (_cached != null && _cachedPath == path) return _cached;
            var f = Parse(System.IO.File.ReadAllText(path, Encoding.UTF8));
            _cached = f;
            _cachedPath = path;
            return f;
        }

        internal static byte ClassCode(string s)
        {
            switch (s) { case "trakt": return 0; case "wiejska": return 1; case "polna": return 2; case "sciezka": return 3; case "lesna": return 4; default: return 3; }
        }

        /// <summary>Plik proby: naglowek "# window: nazwa x0 y0 x1 y1", "# method: ...", "# yaw_offset_deg: ...", "# layout_crc32: ...",
        /// "# scene_xml_crc: ...", wiersz kolumn "#x y yaw_rad scale_across scale_along class look material factor window", potem dane.
        /// Zly wiersz = pominiety i policzony; stempel spoza okien = pominiety (proba tylko na 2 komorkach).</summary>
        internal static ProbeFile Parse(string text)
        {
            var f = new ProbeFile();
            var ci = CultureInfo.InvariantCulture;
            string[] lines = text.Replace("\r", "").Split('\n');
            var cols = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string line in lines)
            {
                if (line.Length == 0) continue;
                if (line[0] == '#')
                {
                    string h = line.TrimStart('#').Trim();
                    if (h.StartsWith("window:", StringComparison.Ordinal))
                    {
                        string[] p = h.Substring(7).Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        float a, b, c, d;
                        if (p.Length >= 5 && float.TryParse(p[1], NumberStyles.Float, ci, out a) && float.TryParse(p[2], NumberStyles.Float, ci, out b)
                            && float.TryParse(p[3], NumberStyles.Float, ci, out c) && float.TryParse(p[4], NumberStyles.Float, ci, out d) && c > a && d > b)
                            f.Windows.Add(new Window { Name = p[0], X0 = a, Y0 = b, X1 = c, Y1 = d });
                    }
                    else if (h.StartsWith("method:", StringComparison.Ordinal)) f.Method = FirstWord(h.Substring(7)).ToLowerInvariant();
                    else if (h.StartsWith("yaw_offset_deg:", StringComparison.Ordinal))
                    {
                        float y;
                        if (float.TryParse(FirstWord(h.Substring(15)), NumberStyles.Float, ci, out y) && Math.Abs(y) <= 360f) f.YawOffsetDeg = y;
                    }
                    else if (h.StartsWith("layout_crc32:", StringComparison.Ordinal)) f.LayoutCrc = h.Substring(13).Trim().Split(' ')[0];
                    else if (h.StartsWith("scene_xml_crc:", StringComparison.Ordinal)) f.SceneCrc = h.Substring(14).Trim();
                    else if (h.StartsWith("x\t", StringComparison.Ordinal))
                    {
                        string[] p = h.Split('\t');
                        for (int i = 0; i < p.Length; i++) cols[p[i].Trim()] = i;
                    }
                    continue;
                }
                f.DataRows++;
                try
                {
                    string[] c = line.Split('\t');
                    int ix = Col(cols, "x", 0), iy = Col(cols, "y", 1), iyaw = Col(cols, "yaw_rad", 2), ia = Col(cols, "scale_across", 3),
                        il = Col(cols, "scale_along", 4), icl = Col(cols, "class", 5), ilk = Col(cols, "look", 6), im = Col(cols, "material", 7),
                        ifa = Col(cols, "factor", 8);
                    var s = new Stamp
                    {
                        X = float.Parse(c[ix], ci), Y = float.Parse(c[iy], ci), Yaw = float.Parse(c[iyaw], ci),
                        Across = float.Parse(c[ia], ci), Along = float.Parse(c[il], ci),
                        Class = ClassCode(c[icl]), Look = ClassCode(c[ilk]), Material = c[im].Trim(),
                        Factor = uint.Parse(c[ifa].Trim(), NumberStyles.HexNumber, ci), Window = -1
                    };
                    if (s.Across <= 0.01f || s.Along <= 0.01f || s.Across > 3f || s.Along > 3f || s.Material.Length == 0) { f.BadRows++; continue; }
                    for (int w = 0; w < f.Windows.Count; w++) if (f.Windows[w].Contains(s.X, s.Y, 0f)) { s.Window = w; break; }
                    if (s.Window < 0) { f.OutsideWindows++; continue; }
                    if (f.Stamps.Count >= MaxStamps) { f.OverCap++; continue; }
                    f.Stamps.Add(s);
                }
                catch { f.BadRows++; }
            }
            return f;
        }

        private static string FirstWord(string s)
        {
            string[] p = s.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return p.Length > 0 ? p[0] : "";
        }

        private static int Col(Dictionary<string, int> cols, string name, int def)
        {
            int i;
            return cols.TryGetValue(name, out i) ? i : def;
        }

        /// <summary>Argument atlasu decala sciezki DOKLADNIE jak w scenie ROT (s5b: path_a -> 0.48 / 0.50, path_b -> -0.01 / 0.00).</summary>
        internal static void AtlasArgument(string material, out float a0, out float a1, out float a2, out float a3)
        {
            a0 = 0.53f; a1 = 0.53f; a3 = 0.01f;
            switch (material)
            {
                case "decal_battania_path_a": case "decal_empire_path_a": a2 = 0.48f; break;
                case "decal_khuzait_path_a": a2 = 0.50f; break;
                case "decal_battania_path_b": a2 = -0.01f; break;
                case "decal_empire_path_b": case "decal_khuzait_path_b": a2 = 0f; break;
                default: a0 = 1f; a1 = 1f; a2 = 0f; a3 = 0f; break;
            }
        }

        /// <summary>Nazwa prefabu z Armoury\Prefabs\arm_map_roads.xml dla materialu (jeden prefab na material, argument w XML).</summary>
        internal static string PrefabFor(string material) { return "arm_" + material; }
    }

    /// <summary>Widok proby drog (komponent mapy SandBox.View; zyje z MapScreen). Rejestracja: SubModuleMain.OnApplicationTick.</summary>
    public sealed class MapRoadsProbeView : EntityVisualManagerBase
    {
        public override int Priority => 105;   // po wioskach (100); nic nie trafia myszka

        private const float TickEvery = 0.25f;
        private static readonly long BuildBudgetTicks = System.Diagnostics.Stopwatch.Frequency * 8 / 1000;   // ok. 8 ms stawiania na tick
        private const int BuildPerTickCap = 400;
        private const float LodAllDist = 60f;      // odleglosc kamery: do 60 wszystkie klasy (DROGI.md rozdz. 4)
        private const float LodBigDist = 110f;     // 60-110 tylko trakt + wiejska, dalej nic (zostaje farba ROT)
        private const float SummaryEvery = 60f;    // linia pomiaru w logu najwyzej co minute (tylko gdy cos sie zmienilo)
        private const int Buckets = 256;           // histogram klatek po 1 ms

        private MapScreen _screen;
        private bool _active, _finalized, _built, _wasOff;
        private float _acc, _sinceSummary;
        private MapRoadsProbe.ProbeFile _file;
        private GameEntity[] _ents;
        private bool[] _viaPrefab;
        private float[] _z;
        private int _next;                         // nastepny stempel do postawienia
        private int _lod = -1;                     // 0 wszystkie, 1 trakt + wiejska, 2 nic
        private int _inWindow = -2;                // okno pod srodkiem kadru (-1 zadne)
        private float[] _winZ;
        // pomiar
        private long _buildTicks, _maxTickTicks;
        private int _buildTickCount, _made, _madePrefab, _madeCode, _skipped, _stCreate, _stTick, _stVis, _stRemove, _lodChanges, _visToggles,
                    _clearMem;
        private readonly int[][] _hist = { new int[Buckets], new int[Buckets] };   // [0] bez drog w kadrze, [1] z drogami w kadrze
        private readonly double[] _sumMs = new double[2];
        private bool _frameWithRoads;
        private string _lastSummary = "";
        private readonly Dictionary<string, bool> _prefabOk = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>(StringComparer.Ordinal);

        // ---------- rejestracja (tani test co klatke, jak MapVillagesView) ----------
        private static WeakReference _regVm, _regScreen;
        private static int _stRegister;

        internal static void EnsureRegistered()
        {
            try
            {
                if (!MapRoadsProbe.On) return;
                if (Campaign.Current == null) return;
                MapScreen screen = MapScreen.Instance;
                if (screen == null) return;
                SandBoxViewVisualManager vm = SandBoxViewSubModule.SandBoxViewVisualManager;
                if (vm == null) return;
                if (_regVm != null && ReferenceEquals(_regVm.Target, vm) && _regScreen != null && ReferenceEquals(_regScreen.Target, screen)) return;
                if (vm.GetEntityComponent<SettlementVisualManager>() == null) return;   // mapa jeszcze nie zbudowala swoich menedzerow
                var mine = vm.GetEntityComponent<MapRoadsProbeView>();
                if (mine != null && !ReferenceEquals(mine._screen, screen))
                {
                    vm.RemoveEntityComponent(mine);   // komponent po innej mapie: jego OnFinalize nie rusza cudzej sceny
                    mine = null;
                }
                if (mine == null) vm.AddEntityComponent<MapRoadsProbeView>();
                _regVm = new WeakReference(vm);
                _regScreen = new WeakReference(screen);
            }
            catch (Exception e)
            {
                _stRegister++;
                if (_stRegister <= 3 || _stRegister % 500 == 0) Log.Error("MapRoadsProbeView.EnsureRegistered (potkniecie " + _stRegister + ")", e);
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _screen = MapScreen.Instance;
            try { Load(); }
            catch (Exception e) { _active = false; Log.Error("MapRoadsProbeView.Load - proba drog wylaczona do nastepnego wczytania", e); }
        }

        private static string DataPath()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);   // Modules\Armoury\bin\Win64_Shipping_Client
                string p = System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "..", "..")), "ModuleData", MapRoadsProbe.FileName);
                if (System.IO.File.Exists(p)) return p;
            }
            catch { }
            try { return System.IO.Path.Combine(BasePath.Name, "Modules", "Armoury", "ModuleData", MapRoadsProbe.FileName); }
            catch { return null; }
        }

        private void Load()
        {
            string path = DataPath();
            if (path == null || !System.IO.File.Exists(path))
            {
                Log.Info("Drogi (proba P2): brak pliku " + (path ?? MapRoadsProbe.FileName) + " - nic nie stawiam (gra bez zmian).");
                return;
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _file = MapRoadsProbe.Load(path);
            uint sceneCrc = 0;
            try { sceneCrc = Campaign.Current.MapSceneWrapper.GetSceneXmlCrc(); } catch { }
            bool crcOk = _file.SceneCrc.Length == 0 || _file.SceneCrc == sceneCrc.ToString(CultureInfo.InvariantCulture);
            var sb = new StringBuilder("Drogi (proba P2): plik ").Append(MapRoadsProbe.FileName).Append(" - stempli ").Append(_file.Stamps.Count)
                .Append(" w oknach ").Append(_file.Windows.Count).Append(" (");
            int[] per = new int[_file.Windows.Count];
            foreach (var s in _file.Stamps) per[s.Window]++;
            for (int w = 0; w < _file.Windows.Count; w++)
            {
                var win = _file.Windows[w];
                if (w > 0) sb.Append("; ");
                sb.Append(win.Name).Append(' ').Append(per[w]).Append(" w ").Append(F1(win.X0)).Append('-').Append(F1(win.X1)).Append(" x ")
                  .Append(F1(win.Y0)).Append('-').Append(F1(win.Y1));
            }
            sb.Append("); zle wiersze ").Append(_file.BadRows).Append(", poza oknami ").Append(_file.OutsideWindows).Append(", ponad limit ").Append(_file.OverCap)
              .Append("; sposob '").Append(_file.Method).Append("', obrot pudla +").Append(F1(_file.YawOffsetDeg)).Append(" st.")
              .Append("; uklad wiosek ").Append(_file.LayoutCrc.Length > 0 ? _file.LayoutCrc : "-")
              .Append("; CRC sceny ").Append(_file.SceneCrc.Length > 0 ? (crcOk ? "zgodne (" + sceneCrc + ")" : "NIEZGODNE (plik " + _file.SceneCrc + ", gra " + sceneCrc + ")") : "brak w pliku")
              .Append("; LOD: do ").Append(F1(LodAllDist)).Append(" wszystkie, do ").Append(F1(LodBigDist)).Append(" trakt + wiejska")
              .Append("; czas ").Append(sw.ElapsedMilliseconds).Append(" ms. Tylko widok, nic do zapisu gry.");
            if (!crcOk)
            {
                Log.Info(sb.Append(" Plik z innej wersji mapy ROT - nic nie stawiam.").ToString());
                return;
            }
            Log.Info(sb.ToString());
            int n = _file.Stamps.Count;
            _ents = new GameEntity[n];
            _viaPrefab = new bool[n];
            _z = new float[n];
            _winZ = new float[_file.Windows.Count];
            _active = n > 0;
        }

        protected override void OnFinalize()
        {
            if (_finalized) { base.OnFinalize(); return; }
            _finalized = true;
            try
            {
                bool sceneAlive = _screen != null && ReferenceEquals(MapScreen.Instance, _screen);
                if (_active)
                {
                    string sum = SummaryText();
                    int removed = sceneAlive ? RemoveAll() : DropAll();
                    Log.Info("Drogi (proba P2): koniec mapy - zdjete stemple " + removed + (sceneAlive ? "" : " (scena juz nie ta - same odwolania)") + "; " + sum);
                }
            }
            catch (Exception e) { Log.Error("MapRoadsProbeView.OnFinalize", e); }
            _active = false;
            _screen = null;
            base.OnFinalize();
        }

        public override void ClearVisualMemory()
        {
            _clearMem++;   // materialy sciezek sa wspolne ze scena ROT (te same decale w osadach) - tylko liczymy
        }

        // ---------- co klatke: TYLKO licznik czasu klatki; praca 4 razy na sekunde ----------
        public override void OnVisualTick(MapScreen screen, float realDt, float dt)
        {
            if (!_active || _finalized) return;
            if (_built)
            {
                int b = (int)(realDt * 1000f);
                if (b < 0) b = 0; else if (b >= Buckets) b = Buckets - 1;
                int k = _frameWithRoads ? 1 : 0;
                _hist[k][b]++;
                _sumMs[k] += realDt * 1000.0;
            }
            _acc += realDt;
            _sinceSummary += realDt;
            if (_acc < TickEvery) return;
            _acc = 0f;
            try
            {
                if (!MapRoadsProbe.On)
                {
                    if (!_wasOff) { int n = RemoveAll(); _wasOff = true; Log.Info("Drogi (proba P2): wylaczone w MCM - zdjete stemple " + n + "."); }
                    return;
                }
                if (_wasOff)
                {
                    _wasOff = false; _built = false; _next = 0; _lod = -1; _inWindow = -2;
                    Array.Clear(_winZ, 0, _winZ.Length);
                    Log.Info("Drogi (proba P2): wlaczone w MCM - stemple wracaja.");
                }
                if (!_built) Build();
                UpdateLod(screen);
                if (_sinceSummary >= SummaryEvery)
                {
                    _sinceSummary = 0f;
                    string sum = SummaryText();
                    if (sum != _lastSummary) { _lastSummary = sum; Log.Info("Drogi (proba P2): " + sum); }
                }
            }
            catch (Exception e)
            {
                _stTick++;
                if (_stTick <= 3 || _stTick % 200 == 0) Log.Error("MapRoadsProbeView.OnVisualTick (potkniecie " + _stTick + ")", e);
            }
        }

        // ---------- stawianie: raz, z budzetem czasu na tick ----------
        private void Build()
        {
            Scene scene = MapScene;
            if (scene == null) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int made = 0;
            var ms = Campaign.Current.MapSceneWrapper;
            while (_next < _file.Stamps.Count && made < BuildPerTickCap)
            {
                if (made > 0 && System.Diagnostics.Stopwatch.GetTimestamp() - t0 > BuildBudgetTicks) break;
                int i = _next++;
                try
                {
                    var s = _file.Stamps[i];
                    float z; Vec3 n;
                    ms.GetTerrainHeightAndNormal(new Vec2(s.X, s.Y), out z, out n);
                    _z[i] = z;
                    MatrixFrame fr = StampFrame(s, z, n, _file.YawOffsetDeg);
                    bool vis = Visible(s, _lod < 0 ? 0 : _lod);
                    GameEntity e = null;
                    if (UsePrefab(s.Material))
                    {
                        // wzor: MapTracksVisualManager.GetGameEntity (Instantiate z ramka Identity) + UpdateTrackPoolPosition (SetFrame)
                        e = GameEntity.Instantiate(scene, MapRoadsProbe.PrefabFor(s.Material), MatrixFrame.Identity, true);
                        if (e != null)
                        {
                            _ents[i] = e;                     // od razu w tablicy: wyjatek ponizej nie zostawi encji bez zdjecia
                            _viaPrefab[i] = true;
                            e.SetFrame(ref fr, true);
                            if (s.Factor != 0xFFFFFFFFu)
                            {
                                Decal d = e.GetComponentAtIndex(0, GameEntity.ComponentType.Decal) as Decal;
                                if (d != null) d.SetFactor1(s.Factor);
                            }
                            e.SetVisibilityExcludeParents(vis);
                            _madePrefab++;
                        }
                    }
                    if (e == null)
                    {
                        Material m = MaterialFor(s.Material);
                        if (m == null) { _skipped++; continue; }
                        // wzor: SettlementVisualManager (kola machin oblezniczych) - ta sama kolejnosc wywolan
                        e = GameEntity.CreateEmpty(scene, false, false, false);
                        _ents[i] = e;
                        _viaPrefab[i] = false;
                        e.Name = "arm_road_probe";
                        Decal d = Decal.CreateDecal(null);
                        d.SetMaterial(m);
                        float a0, a1, a2, a3;
                        MapRoadsProbe.AtlasArgument(s.Material, out a0, out a1, out a2, out a3);
                        d.SetVectorArgument(a0, a1, a2, a3);
                        if (s.Factor != 0xFFFFFFFFu) d.SetFactor1(s.Factor);
                        e.AddComponent(d);
                        e.SetGlobalFrame(in fr, true);
                        e.SetVisibilityExcludeParents(vis);
                        scene.AddDecalInstance(d, "editor_set", true);
                        _madeCode++;
                    }
                    _made++;
                    made++;
                }
                catch (Exception ex)
                {
                    _stCreate++;
                    if (_stCreate <= 3 || _stCreate % 100 == 0) Log.Error("MapRoadsProbeView.Build stempel " + i + " (potkniecie " + _stCreate + ")", ex);
                }
            }
            long dt = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            _buildTicks += dt;
            _buildTickCount++;
            if (dt > _maxTickTicks) _maxTickTicks = dt;
            if (_next >= _file.Stamps.Count)
            {
                _built = true;
                if (_lod < 0) _lod = 0;
                // wysokosc gruntu okna (srodek kadru nad oknem)
                var cnt = new int[_file.Windows.Count];
                for (int i = 0; i < _file.Stamps.Count; i++) { int w = _file.Stamps[i].Window; _winZ[w] += _z[i]; cnt[w]++; }
                for (int w = 0; w < cnt.Length; w++) if (cnt[w] > 0) _winZ[w] /= cnt[w];
                Log.Info("Drogi (proba P2): postawione " + _made + " z " + _file.Stamps.Count + " (prefab " + _madePrefab + ", kod " + _madeCode
                         + ", pominiete bez materialu " + _skipped + ", potkniecia " + _stCreate + ") w " + Ms(_buildTicks) + " ms ("
                         + (_made > 0 ? (_buildTicks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency / _made).ToString("0.0", CultureInfo.InvariantCulture) : "-")
                         + " us na stempel), tickow " + _buildTickCount + ", najdluzszy tick " + Ms(_maxTickTicks) + " ms; prefaby: " + PrefabText() + ".");
            }
        }

        private bool UsePrefab(string material)
        {
            string m = _file.Method;
            if (m == "code") return false;
            bool ok;
            if (!_prefabOk.TryGetValue(material, out ok))
            {
                try { ok = GameEntity.PrefabExists(MapRoadsProbe.PrefabFor(material)); } catch { ok = false; }
                _prefabOk[material] = ok;
            }
            return ok;   // "prefab" bez prefabu w grze = zapas "code" (inaczej czerwony obiekt TEMP)
        }

        private Material MaterialFor(string name)
        {
            Material m;
            if (_mats.TryGetValue(name, out m)) return m;
            try { m = Material.GetFromResource(name); } catch { m = null; }
            _mats[name] = m;
            return m;
        }

        private string PrefabText()
        {
            if (_prefabOk.Count == 0) return "-";
            var sb = new StringBuilder();
            foreach (var kv in _prefabOk) { if (sb.Length > 0) sb.Append(", "); sb.Append(MapRoadsProbe.PrefabFor(kv.Key)).Append(kv.Value ? " jest" : " BRAK"); }
            return sb.ToString();
        }

        /// <summary>Ramka jak CalculateTrackFrame gry: os u = normalna terenu, f = kierunek drogi (+ obrot z pliku), s = f x u;
        /// skala: s = pol szerokosci pudla (w poprzek), f = pol dlugosci (wzdluz), u = pol wysokosci (na stoku pudlo musi objac teren).</summary>
        private static MatrixFrame StampFrame(MapRoadsProbe.Stamp st, float z, Vec3 n, float yawOffsetDeg)
        {
            Vec3 u = n;
            if (u.z < 0.2f) u = new Vec3(0f, 0f, 1f);
            u.Normalize();
            float yaw = st.Yaw + yawOffsetDeg * (float)(Math.PI / 180.0);
            Vec3 f = new Vec3((float)Math.Cos(yaw), (float)Math.Sin(yaw), 0f);
            Vec3 s = Vec3.CrossProduct(f, u);
            s.Normalize();
            f = Vec3.CrossProduct(u, s);
            f.Normalize();
            float h = Math.Max(st.Across, st.Along) + 0.1f;
            MatrixFrame fr = MatrixFrame.Identity;
            fr.rotation.s = s * st.Across;
            fr.rotation.f = f * st.Along;
            fr.rotation.u = u * h;
            fr.origin = new Vec3(st.X, st.Y, z);
            return fr;
        }

        private static bool Visible(MapRoadsProbe.Stamp s, int lod)
        {
            return lod == 0 || (lod == 1 && s.Class <= 1);
        }

        // ---------- poziom szczegolow: widocznosc zmieniana TYLKO przy zmianie poziomu ----------
        private void UpdateLod(MapScreen screen)
        {
            if (screen == null || screen.MapCameraView == null || screen.MapCameraView.Camera == null) return;
            float dist = screen.MapCameraView.CameraDistance;
            int lod = dist <= LodAllDist ? 0 : dist <= LodBigDist ? 1 : 2;
            if (lod != _lod)
            {
                int toggled = 0;
                for (int i = 0; i < _ents.Length; i++)
                {
                    var e = _ents[i];
                    if (e == null) continue;
                    var s = _file.Stamps[i];
                    bool was = _lod >= 0 && Visible(s, _lod), now = Visible(s, lod);
                    if (_lod >= 0 && was == now) continue;
                    try { e.SetVisibilityExcludeParents(now); toggled++; }
                    catch (Exception ex) { _stVis++; if (_stVis <= 3) Log.Error("MapRoadsProbeView.Lod " + i, ex); }
                }
                _visToggles += toggled;
                if (_lod >= 0) _lodChanges++;
                _lod = lod;
            }
            // okno pod srodkiem kadru (promien kamery do gruntu okna) - tylko do pomiaru klatki i jednej linii przy zmianie
            Vec3 p = screen.MapCameraView.Camera.Position;
            Vec3 d = screen.MapCameraView.Camera.Direction;
            int win = -1;
            for (int w = 0; w < _file.Windows.Count; w++)
            {
                if (d.z > -0.05f) break;
                float t = (p.z - _winZ[w]) / -d.z;
                float lx = p.x + d.x * t, ly = p.y + d.y * t;
                if (_file.Windows[w].Contains(lx, ly, 10f)) { win = w; break; }
            }
            _frameWithRoads = win >= 0 && lod < 2;
            if (win != _inWindow)
            {
                if (win >= 0)
                {
                    int vis = 0;
                    for (int i = 0; i < _ents.Length; i++) if (_ents[i] != null && _file.Stamps[i].Window == win && Visible(_file.Stamps[i], lod)) vis++;
                    Log.Info("Drogi (proba P2): kadr nad oknem " + _file.Windows[win].Name + " - odleglosc kamery " + F1(dist) + ", wysokosc " + F1(p.z - _winZ[win])
                             + ", poziom " + (lod == 0 ? "wszystkie" : lod == 1 ? "trakt + wiejska" : "nic") + ", widocznych stempli okna " + vis + ".");
                }
                _inWindow = win;
            }
        }

        // ---------- zdejmowanie ----------
        private int RemoveAll()
        {
            int n = 0;
            Scene scene = MapScene;
            if (_ents == null) return 0;
            for (int i = 0; i < _ents.Length; i++)
            {
                var e = _ents[i];
                if (e == null) continue;
                try
                {
                    e.SetVisibilityExcludeParents(false);
                    if (_viaPrefab[i] || scene == null) e.Remove(112);           // jak tropy (MapTracksVisualManager.OnFinalize: Remove)
                    else scene.RemoveEntity(e, 112);                              // jak kola machin (SettlementVisualManager.RemoveSiegeCircleVisuals)
                    n++;
                }
                catch (Exception ex) { _stRemove++; if (_stRemove <= 3) Log.Error("MapRoadsProbeView.Remove " + i, ex); }
                _ents[i] = null;
            }
            _made = 0; _madePrefab = 0; _madeCode = 0;
            return n;
        }

        private int DropAll()
        {
            int n = 0;
            if (_ents == null) return 0;
            for (int i = 0; i < _ents.Length; i++) { if (_ents[i] != null) n++; _ents[i] = null; }
            return n;
        }

        // ---------- pomiar ----------
        private string SummaryText()
        {
            var sb = new StringBuilder();
            sb.Append("stempli ").Append(_made).Append(" (prefab ").Append(_madePrefab).Append(", kod ").Append(_madeCode).Append(')')
              .Append("; stawianie ").Append(Ms(_buildTicks)).Append(" ms w ").Append(_buildTickCount).Append(" tickach (najdluzszy ").Append(Ms(_maxTickTicks)).Append(" ms)")
              .Append("; zmiany poziomu ").Append(_lodChanges).Append(" (przelaczen widocznosci ").Append(_visToggles).Append(")")
              .Append("; czyszczenie pamieci GPU ").Append(_clearMem)
              .Append("; klatki z drogami w kadrze: ").Append(HistText(1)).Append("; bez: ").Append(HistText(0))
              .Append("; potkniecia: stawianie ").Append(_stCreate).Append(", widocznosc ").Append(_stVis).Append(", zdejmowanie ").Append(_stRemove)
              .Append(", tick ").Append(_stTick).Append('.');
            return sb.ToString();
        }

        private string HistText(int k)
        {
            int[] h = _hist[k];
            long n = 0;
            for (int i = 0; i < Buckets; i++) n += h[i];
            if (n == 0) return "0";
            long acc = 0; int p50 = -1, p95 = -1; long over50 = 0;
            for (int i = 0; i < Buckets; i++)
            {
                acc += h[i];
                if (p50 < 0 && acc * 2 >= n) p50 = i;
                if (p95 < 0 && acc * 100 >= n * 95) p95 = i;
                if (i >= 50) over50 += h[i];
            }
            return n + " (srednio " + (_sumMs[k] / n).ToString("0.0", CultureInfo.InvariantCulture) + " ms, mediana " + p50 + ", p95 " + p95 + ", ponad 50 ms " + over50 + ")";
        }

        private static string Ms(long ticks) { return (ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.0", CultureInfo.InvariantCulture); }
        private static string F1(float v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
    }
}
