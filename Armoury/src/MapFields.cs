// p6-pola (R3-07b, POMYSLY-35 pkt 18; Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak", potem "Rob od razu fale 2") - POLA WOKOL WIOSEK NA MAPIE WEDLUG PORY ROKU.
// SAM WIDOK: nie zmienia ekonomii, tras, walki ani zapisu gry. Stan zyje w widoku mapy i znika razem z nim (zapis gry bez nowych danych, bez Reset kampanii -
// nowa kampania / wczytanie = nowa mapa = nowy widok).
//
// Co i dlaczego tak:
//  - naklejka: material pola pszenicy gry decal_production_farm_wheat_a/b/c/d (Native map_icon_parts.xml - prefab map_icons_production_farm_wheat_*, ROT-Map
//    stawia go 28 razy przy wsiach zbozowych); w materials.tpac: shader "modulate", use_albedo, render_on_terrain (maluje tylko teren - nie domy ani drzewa),
//    ten sam rodzaj co mainmap_decal_undervillage, ktora drogi boczne barwia kolorem (SetFactor1) od 18.10. Cztery materialy = cztery cwiartki atlasu (argument
//    jak w prefabie gry), wybor z hasha pola - pola nie sa jednakowe;
//  - decale BEZ encji (ten sam wzor co drogi boczne / piana statkow NavalDLC: Decal.CreateDecal -> SetMaterial -> Scene.AddDecalInstance(decal, "editor_set")
//    -> Frame; zdjecie Scene.RemoveDecalInstance). Kolor zmienia SetFactor1 na zywym decalu (tak gra barwi tropy partii: MapTracksVisualManager
//    .ParallelUpdateTrackColors) - TYLKO przy zmianie wygladu pola, nie co klatke;
//  - KTO MA POLA (100% wsi rolniczych): kazda wies gry (ok. 570; rybacka i gornicza mniej pol) i kazda wioska ukladu Armoury (ok. 1 950) poza przysiolkami na
//    pustkowiach (pasterze, drwale, weglarze, mysliwi, rybacy - kolumna hamlet: oni nie oraja); liczba pol z wielkosci wioski (poziom 1/2/3 = 2/3/4 pola,
//    mlyn / wiatrak / zagroda / spichlerz +1, rybacka -2, najmniej 1);
//  - GDZIE: pierscien wokol obrazka wioski (pol obrysu + pol pola + 0.2 jedn.), katy rowno co 360/K st. od frontu wioski (+ hash), pole wzdluz pierscienia.
//    Pole odrzucone (probuje 6 miejsc, potem bez tego pola): za blisko miasta / zamku, nachodzi na inne pole wioski, nie po swojej stronie granicy z sasiednia
//    wioska (polplaszczyzna Voronoi z zapasem pol pola - pola dwoch wiosek nigdy na siebie nie wchodza) albo na jej obrazek, nie na ladzie uprawnym (sciana
//    siatki: Plain / Desert / Forest / Steppe / RuralArea - bez sniegu wiecznego, bagna, wydm i plazy), na drodze (MapRoads.KindAt - trakt albo droga boczna
//    w siatce premii), na stoku (rozrzut wysokosci rogow > 0.7 jedn. albo normalna < 0.85);
//  - PORA POL (raz na dobe gry, ta sama dla calego swiata; dni w roku 364-dniowym, inny rok - proporcjonalnie): dni 0-69 zaorane (siew), 70-195 zielone,
//    okno zniw FreeHands (dni 196-252, ta sama regula co rece do zniw i plon - lato albo jesien Westeros): 196-223 dojrzale zolte, 224-252 sciernisko;
//    253-299 sciernisko (w roku wiosny Westeros, bez zniw - zielone), od 300 zaorane. ZIMA Westeros (pora nieba - ta sama, ktora pokrywa mape sniegiem):
//    kraina zimna (WinterRealm 1.0) - pola znikaja pod sniegiem (decal zdjety ze sceny), kraina ciepla (Dorne i Essos, czynnik < 1) - zaorany ugor,
//    Wyspy Letnie (0) - zielone;
//  - STAN WSI (co godzine gry, tylko wsie w scenie): wies gry rabowana albo zlupiona (VillageState BeingRaided / Looted) - wszystkie pola spalone (ciemne);
//    wioska ukladu - ksiega wiosek (VillageBook.LookOf): spalona - pola spalone, puste osady (rabunek + glod) - tyle pol odlogiem (szarobure), ile osad pustych;
//    bez ksiegi - stan wsi gry okregu. Odczyt ksiegi tylko przy zmianie jej numeru wygladu (LookVersion) albo przy wejsciu wioski do sceny;
//  - WYDAJNOSC: plan pol wioski dopiero, gdy jej komorka (32 x 32 jedn.) pierwszy raz wchodzi w kolo widoku kamery (do 6 prob miejsca x 16 sond terenu i drogi na pole), potem w
//    pamieci; w scenie tylko pola komorek w kole widoku (to samo kolo co drogi boczne - MapSideRoads.ViewCircle, histereza 16 jedn.), kamera wyzej niz
//    min(MapVillagesHideAboveCameraHeight, 110) - zadnego pola (jak wioski i drogi); praca 4 razy na sekunde, budzet 4 ms na cwierc sekundy; co klatke nic
//    (CLAUDE.md 7). Zmiana siatki drog (start premii, drogi boczne opublikowane / zdjete) - pola od nowa (raz na zmiane);
//  - potkniecia liczone (pole albo wioska pominieta), nigdy globalny wylacznik.
// Wylacznik: klucz MCM "Map Village Fields" (MapVillageFields, NOWY, DOMYSLNIE TAK). NIE = typ widoku sie nie laduje (nowa sesja), na mapie - pola zdjete.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using SandBox.View;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>Pola wokol wiosek - czesc bez typow SandBox.View (bezpieczna do wolania co klatke z SubModuleMain): wylacznik, pora pol, rejestracja widoku.</summary>
    internal static class MapFields
    {
        internal const string P = "Pola (p6): ";
        // wyglad pola
        internal const byte LGreen = 0, LGold = 1, LStubble = 2, LPlough = 3, LFallow = 4, LBurned = 5, LSnow = 6, LCount = 7, LNone = 255;
        internal static readonly string[] LookName = { "zielone", "dojrzale", "sciernisko", "zaorane", "odlogiem", "spalone", "pod sniegiem" };
        // kolor (mnozy barwe tekstury pola - shader modulate); pod sniegiem - bez decala
        internal static readonly uint[] LookColor = { 0xFF8FBF5Au, 0xFFFFE6A0u, 0xFFC9B48Au, 0xFF8A6E52u, 0xFF9A9478u, 0xFF3A322Cu, 0xFFFFFFFFu };
        // dni roku 364-dniowego (inny rok - proporcjonalnie); okno zniw - FreeHands.HarvestFrom / HarvestTo
        internal const int SowEnd = 70, HarvestMid = 224, PloughFrom = 300;

        private static int _loadFails;

        internal static bool On { get { var s = Settings.Current; return s != null && s.MapVillageFields; } }

        /// <summary>SubModuleMain.OnApplicationTick: rejestracja widoku (tani test co klatke). Blad ladowania typu (inna wersja SandBox.View) wylatuje tu, nie do petli
        /// modulow gry; po 3 probach pola nie startuja do konca sesji (gra bez zmian).</summary>
        internal static void AppTick()
        {
            if (_loadFails >= 3 || !On) return;
            try { Reg(); }
            catch (Exception e)
            {
                _loadFails++;
                Log.Error("MapFields: komponent widoku nie laduje sie (proba " + _loadFails + "/3" + (_loadFails >= 3 ? " - pola wylaczone do konca sesji, gra bez zmian" : "") + ")", e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Reg() { MapFieldsView.EnsureRegistered(); }

        /// <summary>Pora pol swiata (raz na dobe gry): wyglad z dnia roku i pory Westeros; LSnow = zima nieba (wies wybiera wedlug krainy - LookFor).</summary>
        internal static byte Phase(out int doy, out int season, out bool harvest)
        {
            doy = 0; season = -1; harvest = false;
            int y = Math.Max(28, CampaignTime.DaysInYear);
            int d = (int)CampaignTime.Now.ToDays % y; if (d < 0) d += y;
            doy = d;
            int d364 = (int)Math.Round(d * 364.0 / y);
            var t = CampaignTime.Now;
            CampaignTime.Seasons s = WesterosClimate.SkySeason(ref t);
            season = (int)s;
            harvest = FreeHands.HarvestWindow();
            if (s == CampaignTime.Seasons.Winter) return LSnow;
            bool crops = s == CampaignTime.Seasons.Summer || s == CampaignTime.Seasons.Autumn;
            if (d364 < SowEnd) return LPlough;
            if (d364 < FreeHands.HarvestFrom) return LGreen;
            if (d364 <= FreeHands.HarvestTo) return harvest ? (d364 < HarvestMid ? LGold : LStubble) : LGreen;
            if (d364 < PloughFrom) return crops ? LStubble : LGreen;
            return LPlough;
        }

        internal static string SeasonName(int s)
        {
            switch (s) { case 0: return "Spring"; case 1: return "Summer"; case 2: return "Autumn"; case 3: return "Winter"; default: return "?"; }
        }
    }

    /// <summary>Wioska z polami (wies gry albo wioska ukladu).</summary>
    internal sealed class FieldVillage
    {
        public float X, Y, Hmax, Rf, D, A0;
        public int K, Cell;
        public bool Game, Planned;
        public string Key = "", Uid = "";      // Key - hash miejsc pol (StringId wsi gry / uid wioski ukladu); Uid - ksiega wiosek (tylko uklad)
        public Village V;                      // wies gry (wies gry albo okreg wioski ukladu) - stan, kraina
        public FieldPatch[] Fields;            // null - jeszcze bez planu
        // stan (godzina gry)
        public bool Burned;
        public int Fallow;
        public float Realm = 1f;
        public int BookVer = int.MinValue;
        public bool BookBurned, BookKnown;
        public int BookFallow;
    }

    internal sealed class FieldPatch
    {
        public MatrixFrame Fr;
        public int Mat;
        public byte Look = MapFields.LNone;
        public Decal D;
    }

    /// <summary>Widok pol (komponent mapy SandBox.View; zyje z MapScreen). Rejestracja: MapFields.AppTick (tylko przy kluczu MapVillageFields).</summary>
    public sealed class MapFieldsView : EntityVisualManagerBase
    {
        public override int Priority => 107;   // po drogach bocznych (106); nic nie trafia myszka

        private const float TickEvery = 0.25f;
        private const float SummaryEvery = 120f;
        private const float LayoutWaitS = 20f;
        private const float CellSize = 32f;
        private const float Hyst = 16f;
        private const float BucketSize = 8f;
        private const float GameVillageR = 2.4f;     // obrys ikony wsi gry (ROT) - promien
        private const float TownR = 6.0f, CastleR = 4.0f;
        private const float MaxSpread = 0.7f;        // rozrzut wysokosci rogow pola (stok)
        private const float MinNormalZ = 0.85f;
        private const float Alpha = 0.92f;
        private const string DecalSet = "editor_set";
        private static readonly string[] MatNames = { "decal_production_farm_wheat_a", "decal_production_farm_wheat_b", "decal_production_farm_wheat_c", "decal_production_farm_wheat_d" };
        private static readonly float[,] MatArg = { { 0.490f, 0.490f, -0.010f, -0.040f }, { 0.490f, 0.490f, 0.500f, 0.040f }, { 0.490f, 0.510f, -0.020f, 0.450f }, { 0.520f, 0.510f, 0.470f, 0.560f } };
        private static readonly float[] TryDa = { 0f, 0.45f, -0.45f, 0f, 0.45f, -0.45f };
        private static readonly float[] TryDd = { 0f, 0f, 0f, 0.7f, 0.7f, 0.7f };
        private static readonly long BudgetTicks = System.Diagnostics.Stopwatch.Frequency * 4 / 1000;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class FCell
        {
            public int Cx, Cy;
            public readonly List<FieldVillage> V = new List<FieldVillage>(8);
            public bool In;           // w scenie (albo w trakcie stawiania)
            public int Next;          // nastepna wioska do postawienia (budzet)
            public int Gen;
        }

        private MapScreen _screen;
        private bool _finalized, _wasOff, _started, _visible = true;
        private float _acc, _sinceSummary, _waited;
        // wioski i komorki
        private List<FieldVillage> _vill;
        private FCell[] _cells;
        private int _cx0, _cy0, _cw, _ch;
        private readonly List<FCell> _active = new List<FCell>(64);
        private readonly List<FCell> _fill = new List<FCell>(64);
        private int _gen;
        // sasiedzi (wszystkie obrazki wiosek i wsie gry) i warownie
        private readonly Dictionary<int, List<int>> _bucket = new Dictionary<int, List<int>>();
        private readonly List<float> _nx = new List<float>(3000), _ny = new List<float>(3000), _nr = new List<float>(3000);
        private readonly List<FieldVillage> _nOwner = new List<FieldVillage>(3000);
        private readonly List<float> _tx = new List<float>(256), _ty = new List<float>(256), _tr = new List<float>(256);
        private readonly List<int> _near = new List<int>(64);
        // materialy
        private readonly Material[] _mats = new Material[4];
        private int _matCount;
        private bool _matTried, _matBad, _matChecked;
        private string _matNote = "";
        // pora i stan
        private byte _phase = MapFields.LNone;
        private int _lastDay = int.MinValue, _lastHour = int.MinValue, _doy, _season = -1;
        private bool _harvest;
        private object _gridRef;
        private bool _gridKnown;
        // pomiar
        private long _buildTicks, _maxTickTicks, _planTicks;
        private int _buildTickCount, _live, _made, _removed, _plannedV, _plannedF, _noField, _replans, _phaseChanges, _stateRefresh, _lookChanges, _clearMem;
        private int _rTown, _rOwn, _rNeighbor, _rLand, _rRoad, _rSlope;
        private int _stPlan, _stCreate, _stLook, _stRemove, _stTick, _stState;
        private double _startMs;
        private string _startText = "", _lastSummary = "";

        // ---------- rejestracja (tani test co klatke, jak MapSideRoadsView) ----------
        private static WeakReference _regVm, _regScreen;
        private static int _stRegister;

        internal static void EnsureRegistered()
        {
            try
            {
                if (Campaign.Current == null) return;
                MapScreen screen = MapScreen.Instance;
                if (screen == null) return;
                SandBoxViewVisualManager vm = SandBoxViewSubModule.SandBoxViewVisualManager;
                if (vm == null) return;
                if (_regVm != null && ReferenceEquals(_regVm.Target, vm) && _regScreen != null && ReferenceEquals(_regScreen.Target, screen)) return;
                if (vm.GetEntityComponent<SettlementVisualManager>() == null) return;   // mapa jeszcze nie zbudowala swoich menedzerow
                var mine = vm.GetEntityComponent<MapFieldsView>();
                if (mine != null && !ReferenceEquals(mine._screen, screen))
                {
                    vm.RemoveEntityComponent(mine);   // komponent po innej mapie: jego OnFinalize nie rusza cudzej sceny
                    mine = null;
                }
                if (mine == null) vm.AddEntityComponent<MapFieldsView>();
                _regVm = new WeakReference(vm);
                _regScreen = new WeakReference(screen);
            }
            catch (Exception e)
            {
                _stRegister++;
                if (_stRegister <= 3 || _stRegister % 500 == 0) Log.Error("MapFieldsView.EnsureRegistered (potkniecie " + _stRegister + ")", e);
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _screen = MapScreen.Instance;
        }

        protected override void OnFinalize()
        {
            if (_finalized) { base.OnFinalize(); return; }
            _finalized = true;
            try
            {
                bool sceneAlive = _screen != null && ReferenceEquals(MapScreen.Instance, _screen);
                if (_started)
                {
                    string sum = SummaryText();
                    int n = sceneAlive ? RemoveAll() : DropAll();
                    Log.Info(MapFields.P + "koniec mapy - zdjete pola " + n + (sceneAlive ? "" : " (scena juz nie ta - same odwolania)") + "; " + sum);
                }
            }
            catch (Exception e) { Log.Error("MapFieldsView.OnFinalize", e); }
            _screen = null;
            base.OnFinalize();
        }

        public override void ClearVisualMemory() { _clearMem++; }   // material wspolny z polami wsi gry - tylko liczymy

        // ---------- co klatke: nic poza licznikiem; praca 4 razy na sekunde ----------
        public override void OnVisualTick(MapScreen screen, float realDt, float dt)
        {
            if (_finalized) return;
            _acc += realDt;
            _sinceSummary += realDt;
            if (_acc < TickEvery) return;
            float step = _acc;
            _acc = 0f;
            try
            {
                if (!MapFields.On)
                {
                    if (!_wasOff) { _wasOff = true; int n = RemoveAll(); if (_started) Log.Info(MapFields.P + "wylaczone w MCM - zdjete pola " + n + "."); }
                    return;
                }
                if (_wasOff) { _wasOff = false; if (_started) Log.Info(MapFields.P + "wlaczone w MCM - pola wracaja."); }
                if (!_started)
                {
                    // uklad wiosek tej sesji: czekamy na ksiege (VillageBook.Resolve w OnSessionLaunched) - najwyzej LayoutWaitS, potem same wsie gry
                    uint crc;
                    var s = Settings.Current;
                    if (s != null && s.MapVillagesEnabled && !VillageBook.LayoutCrcNow(out crc) && _waited < LayoutWaitS) { _waited += step; return; }
                    Start();
                    return;
                }
                if (_vill == null || _cells == null) return;
                Scene scene = MapScene;
                var ms = Campaign.Current != null ? Campaign.Current.MapSceneWrapper : null;
                if (scene == null || ms == null) return;
                bool rend = true;
                try { rend = scene.HasDecalRenderer(); } catch { }
                if (!rend) return;   // renderer decali jeszcze nie gotowy - nastepna cwierc sekundy
                if (!PickMaterials()) return;
                CheckGrid();
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                Daily();
                Hourly();
                SelectCells(screen, ms);
                Process(scene, ms, t0);
                long tt = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                if (_fill.Count > 0 || tt > BudgetTicks / 8) { _buildTicks += tt; _buildTickCount++; if (tt > _maxTickTicks) _maxTickTicks = tt; }
                if (_sinceSummary >= SummaryEvery)
                {
                    _sinceSummary = 0f;
                    string sum = SummaryText();
                    if (sum != _lastSummary) { _lastSummary = sum; Log.Info(MapFields.P + "stan - " + sum); }
                }
            }
            catch (Exception e)
            {
                _stTick++;
                if (_stTick <= 3 || _stTick % 200 == 0) Log.Error("MapFieldsView.OnVisualTick (potkniecie " + _stTick + ")", e);
            }
        }

        // ---------- start: lista wiosek z polami, sasiedzi, komorki (raz na mape) ----------
        private void Start()
        {
            _started = true;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _vill = new List<FieldVillage>(2600);
            int game = 0, layout = 0, hamlets = 0, bad = 0, gameFew = 0, layoutFew = 0, fieldsPlan = 0;
            var byId = new Dictionary<string, Village>(StringComparer.Ordinal);
            var seen = new HashSet<Village>();
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null) continue;
                    if (st.IsVillage && st.Village != null)
                    {
                        var v = st.Village;
                        if (!seen.Add(v)) continue;                      // ROT podaje dwie wsie dwa razy (jak VillageBook.Resolve)
                        if (st.StringId != null) byId[st.StringId] = v;
                        Vec2 p = st.GetPosition2D;
                        if (float.IsNaN(p.x) || float.IsNaN(p.y)) { bad++; continue; }
                        string type = v.VillageType != null && v.VillageType.StringId != null ? v.VillageType.StringId : "";
                        int k = 4;
                        if (type.IndexOf("fish", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("mine", StringComparison.OrdinalIgnoreCase) >= 0
                            || type.IndexOf("quarry", StringComparison.OrdinalIgnoreCase) >= 0) { k = 2; gameFew++; }
                        var fv = new FieldVillage { X = p.x, Y = p.y, Hmax = GameVillageR, Rf = 1.05f, K = k, Game = true, Key = st.StringId ?? "", V = v };
                        fv.D = fv.Hmax + fv.Rf + 0.2f;
                        fv.A0 = (float)(Hash01(fv.Key, 7) * Math.PI * 2.0);
                        _vill.Add(fv); AddNeighbor(fv.X, fv.Y, fv.Hmax, fv);
                        game++; fieldsPlan += k;
                    }
                    else if (st.IsTown || st.IsCastle)
                    {
                        Vec2 p = st.GetPosition2D;
                        _tx.Add(p.x); _ty.Add(p.y); _tr.Add(st.IsTown ? TownR : CastleR);
                    }
                }
                catch { bad++; }
            }
            var s = Settings.Current;
            var recs = s != null && s.MapVillagesEnabled ? VillageBook.LayoutRecords() : null;
            if (recs != null)
            {
                foreach (var r in recs)
                {
                    try
                    {
                        if (r == null || float.IsNaN(r.X) || float.IsNaN(r.Y)) { bad++; continue; }
                        float hm = Math.Max(0.5f, Math.Max(r.HalfLen, r.HalfWid));
                        if (!string.IsNullOrEmpty(r.Hamlet)) { AddNeighbor(r.X, r.Y, hm, null); hamlets++; continue; }   // przysiolek pasterzy / drwali - bez pol
                        int k = Math.Max(1, Math.Min(3, r.Level)) + 1;
                        if (r.Model == MapVillageData.ModelMill || r.Model == MapVillageData.ModelWindmill || r.Model == MapVillageData.ModelFarm || r.Model == MapVillageData.ModelGranary) k++;
                        else if (r.Model == MapVillageData.ModelFishing) { k = Math.Max(1, k - 2); layoutFew++; }
                        Village dv = null;
                        if (!string.IsNullOrEmpty(r.VillageId)) byId.TryGetValue(r.VillageId, out dv);
                        var fv = new FieldVillage { X = r.X, Y = r.Y, Hmax = hm, Rf = 0.75f, K = k, Game = false, Key = r.Uid ?? "", Uid = r.Uid ?? "", V = dv };
                        fv.D = fv.Hmax + fv.Rf + 0.2f;
                        fv.A0 = (float)(r.FrontDeg * Math.PI / 180.0 + (Hash01(fv.Key, 7) - 0.5) * 0.6);
                        _vill.Add(fv); AddNeighbor(fv.X, fv.Y, fv.Hmax, fv);
                        layout++; fieldsPlan += k;
                    }
                    catch { bad++; }
                }
            }
            // komorki 32 x 32 jedn. (srodek wioski)
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var v in _vill) { if (v.X < minX) minX = v.X; if (v.Y < minY) minY = v.Y; if (v.X > maxX) maxX = v.X; if (v.Y > maxY) maxY = v.Y; }
            int cellsUsed = 0;
            if (_vill.Count > 0)
            {
                _cx0 = (int)Math.Floor(minX / CellSize); _cy0 = (int)Math.Floor(minY / CellSize);
                _cw = (int)Math.Floor(maxX / CellSize) - _cx0 + 1; _ch = (int)Math.Floor(maxY / CellSize) - _cy0 + 1;
                _cells = new FCell[_cw * _ch];
                foreach (var v in _vill)
                {
                    int cx = (int)Math.Floor(v.X / CellSize) - _cx0, cy = (int)Math.Floor(v.Y / CellSize) - _cy0;
                    int idx = cx + cy * _cw;
                    v.Cell = idx;
                    var c = _cells[idx];
                    if (c == null) { c = new FCell { Cx = cx + _cx0, Cy = cy + _cy0 }; _cells[idx] = c; cellsUsed++; }
                    c.V.Add(v);
                }
            }
            _startMs = sw.Elapsed.TotalMilliseconds;
            int doy, season; bool harvest;
            byte ph = MapFields.Phase(out doy, out season, out harvest);
            _startText = "wsi z polami " + _vill.Count + " (wsie gry " + game + ", w tym rybackich / gorniczych z 2 polami " + gameFew + "; wioski ukladu " + layout
                         + (recs == null ? " - uklad wiosek " + (s != null && s.MapVillagesEnabled ? "NIEWCZYTANY" : "wylaczony (MapVillagesEnabled)") : "") + ", w tym rybackich " + layoutFew
                         + "; bez pol: przysiolki na pustkowiach " + hamlets + (bad > 0 ? ", zle wiersze " + bad : "") + "), pol w planie najwyzej " + fieldsPlan
                         + ", komorek " + cellsUsed + " po " + CellSize.ToString("0", Inv) + " jedn., miasta i zamki " + _tx.Count + "; czas " + _startMs.ToString("0.0", Inv) + " ms";
            Log.Info(MapFields.P + "start - " + _startText + "; pora pol teraz: " + MapFields.LookName[ph] + " (dzien roku " + doy + ", pora Westeros " + MapFields.SeasonName(season)
                     + ", okno zniw " + (harvest ? "TAK" : "NIE") + "). Pola stawia widok mapy wokol kamery - bez zmian ekonomii, tras i zapisu gry.");
        }

        private void AddNeighbor(float x, float y, float r, FieldVillage owner)
        {
            int i = _nx.Count;
            _nx.Add(x); _ny.Add(y); _nr.Add(r); _nOwner.Add(owner);
            int key = BucketKey((int)Math.Floor(x / BucketSize), (int)Math.Floor(y / BucketSize));
            List<int> l;
            if (!_bucket.TryGetValue(key, out l)) { l = new List<int>(4); _bucket[key] = l; }
            l.Add(i);
        }

        private static int BucketKey(int bx, int by) { return (bx & 0xFFFF) | (by << 16); }

        /// <summary>Indeksy obrazkow wiosek w kwadracie +-r wokol punktu (lista wspolna _near).</summary>
        private void Near(float x, float y, float r)
        {
            _near.Clear();
            int bx0 = (int)Math.Floor((x - r) / BucketSize), bx1 = (int)Math.Floor((x + r) / BucketSize);
            int by0 = (int)Math.Floor((y - r) / BucketSize), by1 = (int)Math.Floor((y + r) / BucketSize);
            for (int by = by0; by <= by1; by++)
                for (int bx = bx0; bx <= bx1; bx++)
                {
                    List<int> l;
                    if (_bucket.TryGetValue(BucketKey(bx, by), out l)) _near.AddRange(l);
                }
        }

        private static double Hash01(string s, int salt)
        {
            unchecked
            {
                uint h = 2166136261u ^ (uint)(salt * 16777619);
                if (s != null) foreach (char c in s) { h ^= c; h *= 16777619u; }
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }

        // ---------- materialy ----------
        private bool PickMaterials()
        {
            if (_matBad) return false;
            if (_matTried) return _matCount > 0;
            _matTried = true;
            var miss = new StringBuilder();
            for (int i = 0; i < MatNames.Length; i++)
            {
                try
                {
                    Material m = Material.GetFromResource(MatNames[i]);
                    if ((object)m != null && m.IsValid && m.Name != null && string.Equals(m.Name, MatNames[i], StringComparison.OrdinalIgnoreCase)) { _mats[i] = m; _matCount++; continue; }
                }
                catch { }
                if (miss.Length > 0) miss.Append(", ");
                miss.Append(MatNames[i]);
            }
            if (_matCount == 0)
            {
                _matBad = true;
                Log.Info(MapFields.P + "BRAK materialow pola (" + string.Join(", ", MatNames) + ") - pol nie bedzie (gra bez zmian).");
                return false;
            }
            // brakujace cwiartki - pierwszy dostepny material (z jego atlasem)
            for (int i = 0; i < _mats.Length; i++) if ((object)_mats[i] == null) { for (int j = 0; j < _mats.Length; j++) if ((object)_mats[j] != null) { _mats[i] = _mats[j]; break; } }
            _matNote = miss.Length > 0 ? " (brak: " + miss + " - zastapione)" : "";
            return true;
        }

        private int MatIndex(int i)
        {
            // material zastepczy ma atlas swojego zrodla
            for (int j = 0; j < MatNames.Length; j++) if ((object)_mats[i] != null && string.Equals(_mats[i].Name, MatNames[j], StringComparison.OrdinalIgnoreCase)) return j;
            return i;
        }

        // ---------- siatka drog: zmiana = pola od nowa (raz na zmiane) ----------
        private void CheckGrid()
        {
            object g = MapRoads.GridNow;
            if (_gridKnown && ReferenceEquals(g, _gridRef)) return;
            bool first = !_gridKnown;
            _gridRef = g; _gridKnown = true;
            if (first) return;
            int n = RemoveAll();
            int vp = 0;
            foreach (var v in _vill) { if (v.Planned) vp++; v.Planned = false; v.Fields = null; }
            _replans++;
            if (_replans <= 5 || _replans % 50 == 0)
                Log.Info(MapFields.P + "siatka drog zmieniona (premia drog / drogi boczne) - zdjete pola " + n + ", plan " + vp + " wsi od nowa przy kamerze (zmiana " + _replans + ").");
        }

        // ---------- pora (doba) i stan wsi (godzina) ----------
        private void Daily()
        {
            int day = (int)Math.Floor(CampaignTime.Now.ToDays);
            if (day == _lastDay) return;
            _lastDay = day;
            int doy, season; bool harvest;
            byte ph = MapFields.Phase(out doy, out season, out harvest);
            _doy = doy; _season = season; _harvest = harvest;
            // kraina (zima) - czynnik krainy wsi w scenie raz na dobe
            foreach (var c in _active) foreach (var v in c.V) if (v.Fields != null) RefreshRealm(v);
            if (ph == _phase) return;
            byte old = _phase;
            _phase = ph;
            if (old != MapFields.LNone)
            {
                _phaseChanges++;
                Log.Info(MapFields.P + "pora pol " + MapFields.LookName[old] + " -> " + MapFields.LookName[ph] + " (dzien roku " + doy + ", pora Westeros " + MapFields.SeasonName(season)
                         + ", okno zniw " + (harvest ? "TAK" : "NIE") + "); zmiany pory od wczytania " + _phaseChanges + ", pol w scenie " + _live + ".");
            }
            ApplyActive();
        }

        private void Hourly()
        {
            int hour = (int)Math.Floor(CampaignTime.Now.ToHours);
            if (hour == _lastHour) return;
            _lastHour = hour;
            _stateRefresh++;
            int ver = VillageBook.LookVersion;
            foreach (var c in _active)
                foreach (var v in c.V)
                {
                    if (v.Fields == null) continue;
                    RefreshState(v, ver);
                    ApplyLooks(v);
                }
        }

        private void RefreshRealm(FieldVillage v)
        {
            try { v.Realm = v.V != null ? WinterRealm.OfVillage(v.V) : 1f; }
            catch (Exception e) { _stState++; if (_stState <= 3) Log.Error("MapFieldsView.RefreshRealm", e); v.Realm = 1f; }
        }

        private void RefreshState(FieldVillage v, int ver)
        {
            try
            {
                bool vanilla = false;
                if (v.V != null)
                {
                    var st = v.V.VillageState;
                    vanilla = st == Village.VillageStates.BeingRaided || st == Village.VillageStates.Looted;
                }
                if (v.Game) { v.Burned = vanilla; v.Fallow = 0; return; }
                if (v.BookVer != ver)
                {
                    v.BookVer = ver;
                    double n, e; bool b;
                    v.BookKnown = VillageBook.LookOf(v.Uid, out n, out e, out b);
                    v.BookBurned = v.BookKnown && b;
                    v.BookFallow = 0;
                    if (v.BookKnown && !b && n > 1e-6)
                    {
                        double q = e / n; if (q < 0) q = 0; else if (q > 1) q = 1;
                        v.BookFallow = (int)Math.Round(v.K * q);
                    }
                }
                if (v.BookKnown) { v.Burned = v.BookBurned; v.Fallow = v.BookFallow; }
                else { v.Burned = vanilla; v.Fallow = 0; }   // bez ksiegi - stan wsi gry okregu
            }
            catch (Exception e) { _stState++; if (_stState <= 3 || _stState % 500 == 0) Log.Error("MapFieldsView.RefreshState (potkniecie " + _stState + ")", e); }
        }

        private byte LookFor(FieldVillage v, int i)
        {
            byte ph = _phase;
            if (ph == MapFields.LSnow)
            {
                if (v.Realm >= 0.99f) return MapFields.LSnow;               // kraina zimna - pod sniegiem
                if (v.Realm > 0.001f) return v.Burned ? MapFields.LBurned : MapFields.LPlough;   // ciepla - zaorany ugor
                ph = MapFields.LGreen;                                       // Wyspy Letnie - bez zimy
            }
            if (v.Burned) return MapFields.LBurned;
            if (i < v.Fallow) return MapFields.LFallow;
            return ph;
        }

        private void ApplyActive()
        {
            foreach (var c in _active) foreach (var v in c.V) if (v.Fields != null) ApplyLooks(v);
        }

        /// <summary>Wyglad pol wioski w scenie: kolor tylko przy zmianie; pod sniegiem - decal zdjety, spod sniegu - postawiony.</summary>
        private void ApplyLooks(FieldVillage v)
        {
            var fs = v.Fields;
            if (fs == null) return;
            Scene scene = MapScene;
            for (int i = 0; i < fs.Length; i++)
            {
                var f = fs[i];
                byte want = LookFor(v, i);
                if (want == f.Look) continue;
                try
                {
                    _lookChanges++;
                    if (want == MapFields.LSnow) { RemoveField(f, scene); f.Look = want; continue; }
                    if ((object)f.D == null) { f.Look = want; CreateField(f, scene); continue; }
                    f.D.SetFactor1(MapFields.LookColor[want]);
                    f.Look = want;
                }
                catch (Exception e) { _stLook++; if (_stLook <= 3 || _stLook % 500 == 0) Log.Error("MapFieldsView.ApplyLooks (potkniecie " + _stLook + ")", e); f.Look = want; }
            }
        }

        // ---------- kolo widoku i komorki ----------
        private void SelectCells(MapScreen screen, TaleWorlds.CampaignSystem.Map.IMapScene ms)
        {
            int gen = ++_gen;
            float cx = 0f, cy = 0f, r = 0f;
            bool have = Circle(screen, ms, out cx, out cy, out r);
            if (!have)
            {
                foreach (var c in _active) c.Gen = gen;   // brak kamery - zbior bez zmian
                return;
            }
            if (!_visible) return;   // kamera wysoko - zadna komorka (Process zdejmie)
            float rk = r + Hyst, r2 = r * r, rk2 = rk * rk;
            int x0 = (int)Math.Floor((cx - rk) / CellSize) - _cx0, x1 = (int)Math.Floor((cx + rk) / CellSize) - _cx0;
            int y0 = (int)Math.Floor((cy - rk) / CellSize) - _cy0, y1 = (int)Math.Floor((cy + rk) / CellSize) - _cy0;
            if (x0 < 0) x0 = 0; if (y0 < 0) y0 = 0; if (x1 >= _cw) x1 = _cw - 1; if (y1 >= _ch) y1 = _ch - 1;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var c = _cells[x + y * _cw];
                    if (c == null) continue;
                    float lx = c.Cx * CellSize, ly = c.Cy * CellSize;
                    float dx = cx < lx ? lx - cx : (cx > lx + CellSize ? cx - lx - CellSize : 0f);
                    float dy = cy < ly ? ly - cy : (cy > ly + CellSize ? cy - ly - CellSize : 0f);
                    float d2 = dx * dx + dy * dy;
                    if (d2 <= r2 || (c.In && d2 <= rk2))
                    {
                        c.Gen = gen;
                        if (!c.In) { c.In = true; c.Next = 0; _active.Add(c); _fill.Add(c); }
                    }
                }
        }

        private bool Circle(MapScreen screen, TaleWorlds.CampaignSystem.Map.IMapScene ms, out float cx, out float cy, out float r)
        {
            cx = 0f; cy = 0f; r = 0f;
            if (screen == null || screen.MapCameraView == null) return false;
            Camera cam = screen.MapCameraView.Camera;
            if (cam == null) return false;
            MatrixFrame fr = cam.Frame;
            Vec3 p = fr.origin;
            float z0 = Ground(ms, p.x, p.y, p.z - 10f) - 1f;
            if (z0 > p.z - 1f) z0 = p.z - 1f;
            // chowanie razem z wioskami i drogami: min(MapVillagesHideAboveCameraHeight, 110), histereza 10 jedn.
            var s = Settings.Current;
            float hideZ = s != null ? s.MapVillagesHideAboveCameraHeight : 160f;
            if (hideZ <= 0f) hideZ = 160f;
            hideZ = Math.Min(hideZ, MapSideRoads.DefaultHideZ);
            _visible = _visible ? p.z <= hideZ : p.z <= hideZ - 10f;
            if (!_visible) return true;
            float fov = MapSideRoads.MapFovV, asp = 16f / 9f;
            try { float v = cam.GetFovVertical(); if (v > fov && v < 2.5f) fov = v; } catch { }
            try { float a = cam.GetAspectRatio(); if (a > asp && a < 5f) asp = a; } catch { }
            float tanV = (float)Math.Tan(fov * 0.5f), tanH = tanV * asp;
            float h = p.z - z0;
            float cap = Math.Max(MapSideRoads.ChunkCapDefault, MapSideRoads.DefaultFar(h, tanV, tanH));
            Vec3 a1 = fr.rotation.s, f1 = fr.rotation.f, u1 = fr.rotation.u;
            return MapSideRoads.ViewCircle(p.x, p.y, p.z, a1.x, a1.y, a1.z, f1.x, f1.y, f1.z, u1.x, u1.y, u1.z, tanV, tanH, z0, cap, MapSideRoads.MarginFor(h), out cx, out cy, out r);
        }

        private static float Ground(TaleWorlds.CampaignSystem.Map.IMapScene ms, float x, float y, float fallback)
        {
            try
            {
                Vec2 lo = Campaign.MapMinimumPosition, hi = Campaign.MapMaximumPosition;
                if (hi.x > lo.x + 2f && hi.y > lo.y + 2f)
                {
                    x = x < lo.x + 1f ? lo.x + 1f : (x > hi.x - 1f ? hi.x - 1f : x);
                    y = y < lo.y + 1f ? lo.y + 1f : (y > hi.y - 1f ? hi.y - 1f : y);
                }
                if (float.IsNaN(x) || float.IsNaN(y)) return fallback;
                float z; Vec3 n;
                ms.GetTerrainHeightAndNormal(new Vec2(x, y), out z, out n);
                if (float.IsNaN(z) || float.IsInfinity(z)) return fallback;
                return z;
            }
            catch { return fallback; }
        }

        /// <summary>Najpierw zdejmowanie komorek spoza kola (calosc - tanie), potem stawianie z budzetem (plan wioski przy pierwszym wejsciu).</summary>
        private void Process(Scene scene, TaleWorlds.CampaignSystem.Map.IMapScene ms, long t0)
        {
            int gen = _gen;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var c = _active[i];
                if (c.Gen == gen) continue;
                RemoveCell(c, scene);
                _active.RemoveAt(i);
                _fill.Remove(c);
            }
            while (_fill.Count > 0)
            {
                var c = _fill[0];
                while (c.Next < c.V.Count)
                {
                    if (System.Diagnostics.Stopwatch.GetTimestamp() - t0 > BudgetTicks) return;
                    var v = c.V[c.Next++];
                    try
                    {
                        if (!v.Planned) PlanVillage(v, ms);
                        if (v.Fields == null || v.Fields.Length == 0) continue;
                        RefreshRealm(v);
                        RefreshState(v, VillageBook.LookVersion);
                        for (int k = 0; k < v.Fields.Length; k++)
                        {
                            var f = v.Fields[k];
                            f.Look = LookFor(v, k);
                            if (f.Look != MapFields.LSnow) CreateField(f, scene);
                        }
                    }
                    catch (Exception e) { _stCreate++; if (_stCreate <= 3 || _stCreate % 200 == 0) Log.Error("MapFieldsView.Process wioska (potkniecie " + _stCreate + ")", e); }
                }
                _fill.RemoveAt(0);
            }
        }

        private void RemoveCell(FCell c, Scene scene)
        {
            c.In = false; c.Next = 0;
            foreach (var v in c.V)
            {
                if (v.Fields == null) continue;
                foreach (var f in v.Fields) RemoveField(f, scene);
            }
        }

        // ---------- plan pol wioski (watek glowny, raz - przy pierwszym wejsciu komorki) ----------
        private void PlanVillage(FieldVillage v, TaleWorlds.CampaignSystem.Map.IMapScene ms)
        {
            v.Planned = true;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var list = new List<FieldPatch>(v.K);
                var px = new float[v.K]; var py = new float[v.K]; var pr = new float[v.K];
                int n = 0;
                Near(v.X, v.Y, v.D + v.Rf * 3f + 12f);
                for (int i = 0; i < v.K; i++)
                {
                    double baseA = v.A0 + i * Math.PI * 2.0 / v.K;
                    float ha = v.Rf * (float)(0.85 + 0.15 * Hash01(v.Key, 31 + i * 7));
                    float hb = v.Rf * (float)(1.0 + 0.3 * Hash01(v.Key, 32 + i * 7));
                    float rad = (float)Math.Sqrt(ha * ha + hb * hb);
                    int mat = (int)(Hash01(v.Key, 33 + i * 7) * 4.0) & 3;
                    bool done = false;
                    int why = 0;
                    for (int t = 0; t < TryDa.Length && !done; t++)
                    {
                        double a = baseA + TryDa[t];
                        float dist = v.D + TryDd[t] * v.Rf;
                        float x = v.X + (float)Math.Cos(a) * dist, y = v.Y + (float)Math.Sin(a) * dist;
                        float yaw = (float)(a + Math.PI * 0.5 + (Hash01(v.Key, 34 + i * 7) - 0.5) * 0.5);
                        MatrixFrame fr;
                        why = Check(v, x, y, ha, hb, rad, yaw, px, py, pr, n, ms, out fr);
                        if (why != 0) continue;
                        list.Add(new FieldPatch { Fr = fr, Mat = mat });
                        px[n] = x; py[n] = y; pr[n] = Math.Max(ha, hb); n++;
                        done = true;
                    }
                    if (!done)
                    {
                        _noField++;
                        switch (why) { case 1: _rTown++; break; case 2: _rOwn++; break; case 3: _rNeighbor++; break; case 4: _rLand++; break; case 5: _rRoad++; break; default: _rSlope++; break; }
                    }
                }
                v.Fields = list.ToArray();
                _plannedV++;
                _plannedF += v.Fields.Length;
            }
            catch (Exception e)
            {
                v.Fields = new FieldPatch[0];
                _stPlan++;
                if (_stPlan <= 3 || _stPlan % 200 == 0) Log.Error("MapFieldsView.PlanVillage (potkniecie " + _stPlan + ")", e);
            }
            finally { _planTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>0 - pole dobre (fr - ramka decala); 1 miasto / zamek, 2 inne pole wioski, 3 sasiednia wioska, 4 nie lad uprawny, 5 droga, 6 stok / zly teren.</summary>
        private int Check(FieldVillage v, float x, float y, float ha, float hb, float rad, float yaw, float[] px, float[] py, float[] pr, int n,
                          TaleWorlds.CampaignSystem.Map.IMapScene ms, out MatrixFrame fr)
        {
            fr = MatrixFrame.Identity;
            for (int i = 0; i < _tx.Count; i++)
            {
                float dx = x - _tx[i], dy = y - _ty[i], lim = _tr[i] + rad;
                if (dx * dx + dy * dy < lim * lim) return 1;
            }
            float own = Math.Max(ha, hb);
            for (int i = 0; i < n; i++)
            {
                float dx = x - px[i], dy = y - py[i], lim = (own + pr[i]) * 0.95f;
                if (dx * dx + dy * dy < lim * lim) return 2;
            }
            float ax = x - v.X, ay = y - v.Y, da2 = ax * ax + ay * ay;
            foreach (int j in _near)
            {
                var o = _nOwner[j];
                if (ReferenceEquals(o, v)) continue;
                float bx = _nx[j], by = _ny[j];
                float ex = x - bx, ey = y - by, db2 = ex * ex + ey * ey;
                float lim = _nr[j] + rad * 0.8f;
                if (db2 < lim * lim) return 3;                       // na obrazek sasiada
                float abx = bx - v.X, aby = by - v.Y, ab = (float)Math.Sqrt(abx * abx + aby * aby);
                if (ab < 1e-3f) return 3;
                if ((db2 - da2) / (2f * ab) < rad * 0.9f) return 3;   // nie po swojej stronie granicy z sasiadem (z zapasem)
            }
            // teren: srodek i 4 rogi - lad uprawny, bez drogi, bez stoku
            float c = (float)Math.Cos(yaw), s = (float)Math.Sin(yaw);
            float zmin = float.MaxValue, zmax = float.MinValue;
            for (int k = 0; k < 5; k++)
            {
                float qx = x, qy = y;
                if (k > 0)
                {
                    float sa = (k == 1 || k == 2) ? ha : -ha, sb = (k == 1 || k == 3) ? hb : -hb;
                    qx = x + c * sb - s * sa; qy = y + s * sb + c * sa;
                }
                var cv = new CampaignVec2(new Vec2(qx, qy), true);
                PathFaceRecord f = ms.GetFaceIndex(in cv);
                if (!f.IsValid() || !Arable(f.FaceGroupIndex)) return 4;
                int kind = MapRoads.KindAt(qx, qy);
                if (kind > 0) return 5;
                float z; Vec3 nn;
                ms.GetTerrainHeightAndNormal(new Vec2(qx, qy), out z, out nn);
                if (float.IsNaN(z) || float.IsInfinity(z)) return 6;
                if (z < zmin) zmin = z; if (z > zmax) zmax = z;
            }
            if (zmax - zmin > MaxSpread) return 6;
            float zc; Vec3 nc;
            ms.GetTerrainHeightAndNormal(new Vec2(x, y), out zc, out nc);
            if (float.IsNaN(zc)) return 6;
            Vec3 u = nc;
            float ul = u.Length;
            if (!(ul > 1e-4f)) return 6;
            u = u * (1f / ul);
            if (u.z < MinNormalZ) return 6;
            // ramka jak drogi boczne (CalculateTrackFrame gry): u = normalna terenu, f = wzdluz pola, s = f x u; skala = pol wymiaru pudla
            Vec3 ff = new Vec3(c, s, 0f);
            Vec3 ss = Vec3.CrossProduct(ff, u); ss.Normalize();
            ff = Vec3.CrossProduct(u, ss); ff.Normalize();
            float h = 0.6f + (zmax - zmin);
            fr.rotation.s = ss * ha;
            fr.rotation.f = ff * hb;
            fr.rotation.u = u * h;
            fr.origin = new Vec3(x, y, zc);
            return 0;
        }

        /// <summary>Lad pod pole: Plain 1, Desert 2, Forest 4, Steppe 5, RuralArea 14 (bez Snow 3 - snieg wieczny, Swamp 15, Dune 16, Beach 20 i wszystkiego, co nie ladem).</summary>
        private static bool Arable(int g) { return g == 1 || g == 2 || g == 4 || g == 5 || g == 14; }

        // ---------- decale ----------
        private void CreateField(FieldPatch f, Scene scene)
        {
            if ((object)f.D != null || scene == null || _matBad) return;
            try
            {
                Decal d = Decal.CreateDecal(null);
                if ((object)d == null || !d.IsValid) { _stCreate++; return; }
                int mi = f.Mat & 3;
                d.SetMaterial(_mats[mi]);                                // material PRZED dodaniem do sceny
                if (!_matChecked)
                {
                    _matChecked = true;
                    Material got = d.GetMaterial();                      // raz: decal ma NASZ material (nie zastepczy)
                    if ((object)got == null || !got.IsValid || (got.Pointer != _mats[mi].Pointer && !string.Equals(got.Name, _mats[mi].Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        _matBad = true;
                        Log.Info(MapFields.P + "decal nie przyjal materialu '" + _mats[mi].Name + "' - pol nie bedzie (gra bez zmian).");
                        return;
                    }
                }
                int ai = MatIndex(mi);
                d.SetVectorArgument(MatArg[ai, 0], MatArg[ai, 1], MatArg[ai, 2], MatArg[ai, 3]);   // cwiartka atlasu jak w prefabie gry
                byte look = f.Look < MapFields.LCount ? f.Look : MapFields.LGreen;
                d.SetFactor1(MapFields.LookColor[look]);
                scene.AddDecalInstance(d, DecalSet, true);
                f.D = d;                                                 // od razu: wyjatek ponizej nie zostawi decala bez zdjecia
                _live++; _made++;
                d.Frame = f.Fr;
                d.SetAlpha(Alpha);
            }
            catch (Exception e) { _stCreate++; if (_stCreate <= 3 || _stCreate % 200 == 0) Log.Error("MapFieldsView.CreateField (potkniecie " + _stCreate + ")", e); }
        }

        private void RemoveField(FieldPatch f, Scene scene)
        {
            var d = f.D;
            if ((object)d == null) return;
            f.D = null;
            _live--; _removed++;
            try
            {
                d.SetIsVisible(false);
                bool rend = false;
                try { rend = scene != null && scene.HasDecalRenderer(); } catch { }
                if (rend) scene.RemoveDecalInstance(d, DecalSet);
            }
            catch (Exception e) { _stRemove++; if (_stRemove <= 3) Log.Error("MapFieldsView.RemoveField", e); }
        }

        private int RemoveAll()
        {
            int n0 = _live;
            Scene scene = MapScene;
            foreach (var c in _active) RemoveCell(c, scene);
            _active.Clear(); _fill.Clear();
            return n0 - _live;
        }

        private int DropAll()
        {
            int n = 0;
            foreach (var c in _active)
            {
                c.In = false;
                foreach (var v in c.V) if (v.Fields != null) foreach (var f in v.Fields) if ((object)f.D != null) { f.D = null; n++; }
            }
            _live -= n;
            _active.Clear(); _fill.Clear();
            return n;
        }

        // ---------- pomiar ----------
        private string SummaryText()
        {
            var cnt = new int[MapFields.LCount];
            int inCells = 0;
            foreach (var c in _active)
            {
                inCells++;
                foreach (var v in c.V) if (v.Fields != null) foreach (var f in v.Fields) if (f.Look < MapFields.LCount) cnt[f.Look]++;
            }
            var sb = new StringBuilder();
            sb.Append("w scenie pol ").Append(_live).Append(" (komorek ").Append(inCells).Append(", widoczne ").Append(_visible ? "tak" : "nie - kamera wysoko").Append(")");
            sb.Append("; wsi z planem ").Append(_plannedV).Append(" z ").Append(_vill != null ? _vill.Count : 0).Append(", pol w planie ").Append(_plannedF)
              .Append(" (bez miejsca: ").Append(_noField).Append(" - miasto/zamek ").Append(_rTown).Append(", inne pole ").Append(_rOwn).Append(", sasiad ").Append(_rNeighbor)
              .Append(", nie lad uprawny ").Append(_rLand).Append(", droga ").Append(_rRoad).Append(", stok ").Append(_rSlope).Append(")");
            sb.Append("; wyglad pol komorek w scenie:");
            for (int i = 0; i < MapFields.LCount; i++) sb.Append(i == 0 ? " " : ", ").Append(MapFields.LookName[i]).Append(' ').Append(cnt[i]);
            sb.Append("; pora pol ").Append(_phase < MapFields.LCount ? MapFields.LookName[_phase] : "-").Append(" (dzien roku ").Append(_doy).Append(", pora Westeros ")
              .Append(MapFields.SeasonName(_season)).Append(", okno zniw ").Append(_harvest ? "TAK" : "NIE").Append("), zmiany pory ").Append(_phaseChanges)
              .Append(", zmiany wygladu pol ").Append(_lookChanges).Append(", odswiezenia stanu ").Append(_stateRefresh);
            sb.Append("; stawianie ").Append(Ms(_buildTicks)).Append(" ms w ").Append(_buildTickCount).Append(" tickach (najdluzszy ").Append(Ms(_maxTickTicks)).Append(" ms), plan wsi ")
              .Append(Ms(_planTicks)).Append(" ms").Append(_plannedV > 0 ? " (" + (_planTicks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency / _plannedV).ToString("0", Inv) + " us na wies)" : "")
              .Append(", postawione ").Append(_made).Append(", zdjete ").Append(_removed).Append(", plan od nowa (siatka drog) ").Append(_replans)
              .Append("; ").Append(_matCount).Append(" materialy pola").Append(_matNote).Append("; czyszczenie pamieci GPU ").Append(_clearMem);
            sb.Append("; potkniecia: plan ").Append(_stPlan).Append(", stawianie ").Append(_stCreate).Append(", wyglad ").Append(_stLook).Append(", stan ").Append(_stState)
              .Append(", zdejmowanie ").Append(_stRemove).Append(", tick ").Append(_stTick).Append('.');
            return sb.ToString();
        }

        private static string Ms(long ticks) { return (ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.0", Inv); }
    }
}
