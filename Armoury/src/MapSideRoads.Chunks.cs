// f22-drogi-perf (Jeff 18.10: "przy max x8 predkosci strasznie gra zwalnia i sie przycina tam gdzie jest duzo jednostek"; f19-drogi: "kazda wioska poboczna
// powinna miec droge do siebie" - 2446 drog, 73 535 decali w scenie mapy CALY CZAS) - BOCZNE DROGI W KOMORKACH: w scenie tylko decale wokol tego, co widzi kamera.
//
// Co kosztuje (sprawdzone w dekompilacji, TaleWorlds.Engine 1.4.8): Decal to cienka nakladka na obiekt silnika (rglDecal) - CreateDecal, SetMaterial, SetFrame,
// SetAlpha, SetIsVisible (IDecal "set_is_visible") i Scene.AddDecalInstance / RemoveDecalInstance (IScene) ida prosto do silnika C++; po stronie .NET nie ma
// zadnego odrzucania poza kadrem ani pytania o widocznosc, a kod silnika nie jest do odczytania. Nie da sie wiec z kodu udowodnic, ze decal schowany przez
// SetIsVisible(false) albo lezacy poza kadrem nic nie kosztuje (zbior "editor_set" moze byc przegladany co klatke w calosci). Pewne jest jedno: decal zdjety
// ze sceny (RemoveDecalInstance) nie jest juz w zbiorze sceny - dlatego komorki STAWIAJA i ZDEJMUJA decale (jak NavalDLC przy zdjeciu wizerunku statku),
// a nie tylko je chowaja. Pomiar (linia "decali ..." co minute): klatki wedlug liczby komorek w scenie - jesli koszt klatki rosnie z liczba decali, bedzie widac.
//
// Zasady:
//  - komorki 32 x 32 jedn. (plan: 870 komorek z drogami, srednio 85 stempli, najwiecej 231); podzial liczy watek z puli razem z planem (czysta geometria);
//  - KOLO WIDOKU: 4 promienie rogow ekranu (kamera mapy: pion 40 st., proporcje ekranu) przeciete z ziemia pod srodkiem ekranu; rog dalej niz klucz
//    MapSideRoadsChunkRadiusCap (160 jedn., poziomo od kamery) albo nad horyzontem - na tej odleglosci. Srodek = srednia rogow, promien = najdalszy rog + 8 jedn.
//    + 0.1 x wysokosc kamery (stempel i ruch kamery przez cwierc sekundy); ziemia = najnizsza z 4 probek terenu (pod kamera, pod srodkiem ekranu, pod
//    srodkiem gornej i dolnej krawedzi) minus 1 jedn. Przy zwyklym pochyleniu kamery (bez przechylu) kolo obejmuje caly ekran: promien ok. 61 jedn. przy
//    kamerze z 10, 68 przy 20, 81 przy 40, 103 przy 80, 118 przy 110 (plan v7: w scenie mediana 1.6 / 1.8 / 2.3 / 3.3 tys. decali zamiast 73.5 tys.);
//  - komorka wchodzi, gdy jej prostokat dotyka kola; schodzi dopiero, gdy jest dalej niz promien + 16 jedn. (histereza pol komorki - zbior zmienia sie
//    tylko przy przejsciu granic komorek, bez migania przy drganiu kamery);
//  - kamera wyzej niz prog chowania (ten sam co dotad: min(MapVillagesHideAboveCameraHeight, 110), histereza 10) - zadna komorka w scenie;
//  - praca 4 razy na sekunde (jak dotad), budzet 8 ms na cwierc sekundy wspolny dla zdejmowania i stawiania, najpierw zdejmowanie, potem stawianie od
//    komorek najblizszych srodka kola; bieg zdjec autotestu (plik-przelacznik) - bez budzetu: kamera ustawiona na cel zdjecia ma drogi w nastepnej cwierci
//    sekundy (autotest czeka 6 s i 30 klatek), wiec drogi sa na zdjeciach bez osobnej listy celow;
//  - ramki stempli (lad / woda, wysokosc i normalna terenu) liczone raz na komorke przy pierwszym wejsciu i trzymane (nastepne wejscia - samo stawianie);
//  - nic co klatke poza licznikiem czasu klatki (CLAUDE.md 7); potkniecia liczone (stempel pominiety, komorka po 20 potknieciach wylaczona do konca mapy),
//    nigdy globalny wylacznik.
// Klucz MapSideRoadsChunks (NOWY, domyslnie TAK; NIE = stara droga co do znaku: wszystkie decale w scenie, chowane z wioskami). Przelaczenie na mapie - decale
// zdjete i postawione od nowa w nowym trybie (ten sam plan, bez liczenia od nowa).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Armoury
{
    internal static partial class MapSideRoads
    {
        /// <summary>f22: klucz MapSideRoadsChunks (bez ustawien - TAK, domyslna klucza).</summary>
        internal static bool KeyChunks { get { var s = Settings.Current; return s == null || s.MapSideRoadsChunks; } }

        /// <summary>f22: najdalsza odleglosc komorek od kamery (poziomo), klucz MapSideRoadsChunkRadiusCap przyciety do 40..400.</summary>
        internal static float ChunkCap
        {
            get
            {
                var s = Settings.Current;
                int v = s != null ? s.MapSideRoadsChunkRadiusCap : ChunkCapDefault;
                if (v < 40) v = 40; else if (v > 400) v = 400;
                return v;
            }
        }
    }

    public sealed partial class MapSideRoadsView
    {
        private const int CellStrikes = 20;                      // komorka po tylu potknieciach wylaczona do konca mapy (reszta drog dziala)
        private static readonly string[] CellBinNames = { "0", "1-4", "5-9", "10-19", "20-39", "40+" };

        /// <summary>Komorka bocznych drog (f22): stemple planu, ramki liczone przy pierwszym wejsciu, decale w scenie.</summary>
        private sealed class Cell
        {
            public int Cx, Cy;
            public int[] Idx;
            public MatrixFrame[] Fr;
            public float[] Al;
            public int Valid;                  // ramek (stempli na ladzie) po przygotowaniu
            public bool Prepared, Failed;
            public int Next;                   // nastepna ramka do postawienia; > 0 = komorka w scenie (takze czesciowo)
            public int Strikes;
            public int Gen;                    // numer ticku, w ktorym komorka byla w kole (albo w histerezie)
            public float D2;                   // kwadrat odleglosci od srodka kola (kolejnosc stawiania)
            public readonly List<Decal> Live = new List<Decal>();
        }

        private int _mode = -1;                                  // co stoi w scenie: -1 nic, 0 stara droga (wszystkie decale), 1 komorki
        private Cell[] _cells;
        private MapSideRoads.ChunkGrid _grid;
        private readonly List<Cell> _active = new List<Cell>();  // komorki z Next > 0
        private readonly List<Cell> _want = new List<Cell>();
        private int _gen, _liveDecals, _maxLiveDecals, _maxLiveCells;
        private bool _matChecked, _matBad, _firstFillLogged, _lastHave, _gridLogged;
        private float _lastCx, _lastCy, _lastR, _sumR;
        private long _nR;
        private int _cellLoads, _cellUnloads, _prepCells, _prepStamps, _cellFails, _jumps;
        private long _addDecals, _remDecals;
        private readonly int[][] _cHist = { new int[Buckets], new int[Buckets], new int[Buckets], new int[Buckets], new int[Buckets], new int[Buckets] };
        private readonly double[] _cSum = new double[6];
        private readonly double[] _cDec = new double[6];
        private static readonly Comparison<Cell> ByDist = (a, b) => a.D2.CompareTo(b.D2);

        private static int CellBin(int n) { return n <= 0 ? 0 : n < 5 ? 1 : n < 10 ? 2 : n < 20 ? 3 : n < 40 ? 4 : 5; }

        /// <summary>Licznik klatki w trybie komorek (wolany z OnVisualTick przy _built) - same tablice, zadnych wywolan silnika.</summary>
        private void CountChunkFrame(int bucket, float realDt)
        {
            int cb = CellBin(_active.Count);
            _cHist[cb][bucket]++;
            _cSum[cb] += realDt * 1000.0;
            _cDec[cb] += _liveDecals;
        }

        /// <summary>Tryb przelaczony kluczem MapSideRoadsChunks: wszystko ze sceny, stawianie od nowa w nowym trybie (plan ten sam). Raz na zmiane.</summary>
        private void ModeChanged(bool chunks)
        {
            int n = RemoveAll();
            _visKnown = false;
            Log.Info(MapSideRoads.P + "tryb komorek (klucz MapSideRoadsChunks) zmieniony na " + (chunks ? "TAK" : "NIE") + " - zdjete decale " + n + ", stawianie od nowa ("
                     + (chunks ? "tylko komorki wokol widoku kamery" : "wszystkie decale, chowane z wioskami") + "; plan ten sam).");
        }

        /// <summary>Nowy plan (zmiana ukladu wiosek albo reguly): komorki starego planu zapomniane (decale juz zdjete przez RemoveAll).</summary>
        private void ResetChunks()
        {
            _cells = null; _grid = null; _gridLogged = false;
            _matChecked = false; _matBad = false; _firstFillLogged = false; _lastHave = false;
        }

        /// <summary>Praca trybu komorek (4 razy na sekunde): kolo widoku, wybor komorek, zdejmowanie i stawianie z budzetem.</summary>
        private void ChunkTick(MapScreen screen)
        {
            _mode = 1;
            if (_matBad) return;
            Scene scene = MapScene;
            var ms = Campaign.Current != null ? Campaign.Current.MapSceneWrapper : null;
            if (scene == null || ms == null) return;
            bool rend = true;
            try { rend = scene.HasDecalRenderer(); } catch { }
            if (!rend) return;   // renderer decali jeszcze nie gotowy - nastepna cwierc sekundy (jak Build)
            if (!PickMaterial())
            {
                if (!_built) { _built = true; _noMat++; _matBad = true; Log.Info(MapSideRoads.P + "BRAK materialu (" + _job.Look.Material + " ani zapasowych) - nic nie stawiam."); }
                return;
            }
            if (_cells == null) MakeCells();
            if (_cells == null) return;
            _built = true;
            ChunkVisibility(screen);
            int gen = ++_gen;
            _want.Clear();
            if (_visible)
            {
                float cx, cy, r;
                if (CameraCircle(screen, ms, out cx, out cy, out r))
                {
                    SelectCells(cx, cy, r, gen);
                    _sumR += r; _nR++;
                    if (_lastHave)
                    {
                        float jx = cx - _lastCx, jy = cy - _lastCy;
                        if (jx * jx + jy * jy > 100f * 100f) _jumps++;
                    }
                    _lastHave = true; _lastCx = cx; _lastCy = cy; _lastR = r;
                }
                else
                {
                    foreach (var c in _active) c.Gen = gen;   // brak kamery - zbior bez zmian (nic nie zdejmujemy na slepo)
                }
            }
            else _lastHave = false;
            ProcessCells(scene, gen);
        }

        /// <summary>Komorki z podzialu planu (watek z puli): obiekty raz na plan i linia "komorki".</summary>
        private void MakeCells()
        {
            var g = _job.Grid;
            if (g == null || g.Idx == null) return;
            var cells = new Cell[g.Idx.Length];
            for (int k = 0; k < cells.Length; k++)
            {
                var a = g.Idx[k];
                if (a == null) continue;
                cells[k] = new Cell { Cx = k % g.W + g.X0, Cy = k / g.W + g.Y0, Idx = a };
            }
            _cells = cells; _grid = g;
            if (_gridLogged) return;
            _gridLogged = true;
            var r = _job.Res;
            Log.Info(MapSideRoads.P + "komorki (f22) - stempli " + r.Stamps.Count + " w " + g.Cells + " komorkach po " + g.Cell.ToString("0", Inv) + " jedn. (na komorke srednio "
                     + g.PerAvg.ToString("0", Inv) + ", mediana " + g.PerMed + ", p90 " + g.PerP90 + ", najwiecej " + g.PerMax + (g.Lost > 0 ? "; zle stemple " + g.Lost : "")
                     + "); w scenie tylko komorki w kole widoku kamery (najdalej " + MapSideRoads.ChunkCap.ToString("0", Inv) + " jedn. od kamery - klucz MapSideRoadsChunkRadiusCap; histereza "
                     + MapSideRoads.ChunkHyst.ToString("0", Inv) + " jedn.), stawiane i zdejmowane przy przejsciu granic komorek, budzet 8 ms na cwierc sekundy"
                     + (MapSideRoads.AutotestFlag ? " (bieg zdjec - bez budzetu)" : "") + "; " + g.Expect + "; " + _matName + _matNote + ", " + _job.Look.Text() + ".");
        }

        /// <summary>Chowanie z wioskami w trybie komorek: ten sam prog i histereza co UpdateVisibility, ale bez przelaczania decali (powyzej progu kolo jest puste -
        /// komorki schodza ze sceny w ProcessCells).</summary>
        private void ChunkVisibility(MapScreen screen)
        {
            if (screen == null || screen.MapCameraView == null || screen.MapCameraView.Camera == null) return;
            float camZ = screen.MapCameraView.Camera.Position.z;
            float hideZ = Settings.Current.MapVillagesHideAboveCameraHeight;
            if (hideZ <= 0f) hideZ = 160f;
            hideZ = Math.Min(hideZ, _job.Look.HideZ);
            bool want = _visible ? camZ <= hideZ : camZ <= hideZ - 10f;
            if (_visKnown && want == _visible) return;
            if (_visKnown) _visChanges++;
            _visKnown = true;
            _visible = want;
        }

        /// <summary>Kolo widoku z kamery mapy: ramka kamery, pion (co najmniej 40 st. gry) i proporcje (co najmniej 16:9), ziemia pod srodkiem ekranu.</summary>
        private bool CameraCircle(MapScreen screen, TaleWorlds.CampaignSystem.Map.IMapScene ms, out float cx, out float cy, out float r)
        {
            cx = 0f; cy = 0f; r = 0f;
            if (screen == null || screen.MapCameraView == null) return false;
            Camera cam = screen.MapCameraView.Camera;
            if (cam == null) return false;
            MatrixFrame fr = cam.Frame;
            float fov = MapSideRoads.MapFovV, asp = 16f / 9f;
            try { float v = cam.GetFovVertical(); if (v > fov && v < 2.5f) fov = v; } catch { }   // mniejszy / dziwny odczyt - pion gry (wiecej komorek, nie mniej)
            try { float a = cam.GetAspectRatio(); if (a > asp && a < 5f) asp = a; } catch { }
            float tanV = (float)Math.Tan(fov * 0.5f);
            Vec3 p = fr.origin;
            float cap = MapSideRoads.ChunkCap;
            Vec3 s = fr.rotation.s, f = fr.rotation.f, u = fr.rotation.u;
            // ziemia: NAJNIZSZA z probek pod kamera, pod srodkiem ekranu i pod srodkiem gornej / dolnej krawedzi (znak osi f bez znaczenia - obie) minus 1 jedn. -
            // nizsza ziemia = dalsze rogi = wieksze kolo (dolina przed kamera nie zostaje bez drog na gorze ekranu)
            float z0 = Ground(ms, p.x, p.y, p.z - 10f);
            float zg = z0;
            zg = Math.Min(zg, GroundAlong(ms, p, -u.x, -u.y, -u.z, z0, cap));
            zg = Math.Min(zg, GroundAlong(ms, p, -u.x + f.x * tanV, -u.y + f.y * tanV, -u.z + f.z * tanV, z0, cap));
            zg = Math.Min(zg, GroundAlong(ms, p, -u.x - f.x * tanV, -u.y - f.y * tanV, -u.z - f.z * tanV, z0, cap));
            zg -= 1f;
            if (zg > p.z - 1f) zg = p.z - 1f;
            return MapSideRoads.ViewCircle(p.x, p.y, p.z, s.x, s.y, s.z, f.x, f.y, f.z, u.x, u.y, u.z, tanV, tanV * asp, zg, cap, MapSideRoads.MarginFor(p.z - zg), out cx, out cy, out r);
        }

        /// <summary>Wysokosc terenu tam, gdzie promien z kamery (dx, dy, dz) przecina wysokosc z0 (najdalej cap poziomo; promien nad horyzontem - na cap).</summary>
        private static float GroundAlong(TaleWorlds.CampaignSystem.Map.IMapScene ms, Vec3 p, float dx, float dy, float dz, float z0, float cap)
        {
            float lh = (float)Math.Sqrt(dx * dx + dy * dy);
            if (!(lh > 1e-5f)) return z0;   // pionowo w dol - punkt pod kamera (juz w probkach)
            float dist = dz < -1e-4f ? (p.z - z0) / -dz * lh : cap;
            if (!(dist >= 0f) || dist > cap) dist = cap;
            return Ground(ms, p.x + dx / lh * dist, p.y + dy / lh * dist, z0);
        }

        private static float Ground(TaleWorlds.CampaignSystem.Map.IMapScene ms, float x, float y, float fallback)
        {
            try
            {
                // probka zawsze w granicach mapy (kamera i rogi ekranu moga wyjsc poza mape - jak ComputeMapCamera gry, ktora przycina cel do tych granic)
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

        /// <summary>Komorki w kole (wejscie) albo juz w scenie i w kole + histereza (zostaja): Gen = gen; niepelne - do listy stawiania z odlegloscia.</summary>
        private void SelectCells(float cx, float cy, float r, int gen)
        {
            var g = _grid;
            float cs = g.Cell, rk = r + MapSideRoads.ChunkHyst;
            float r2 = r * r, rk2 = rk * rk;
            int i0 = Math.Max(0, (int)Math.Floor((cx - rk) / cs) - g.X0), i1 = Math.Min(g.W - 1, (int)Math.Floor((cx + rk) / cs) - g.X0);
            int j0 = Math.Max(0, (int)Math.Floor((cy - rk) / cs) - g.Y0), j1 = Math.Min(g.H - 1, (int)Math.Floor((cy + rk) / cs) - g.Y0);
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    var c = _cells[i + j * g.W];
                    if (c == null || c.Failed) continue;
                    float d2 = MapSideRoads.RectDist2(c.Cx, c.Cy, cs, cx, cy);
                    if (d2 <= r2 || (c.Next > 0 && d2 <= rk2))
                    {
                        c.Gen = gen;
                        c.D2 = d2;
                        if (!c.Prepared || c.Next < c.Valid) _want.Add(c);
                    }
                }
        }

        /// <summary>Zdejmowanie komorek spoza kola, potem stawianie od najblizszych - wspolny budzet czasu (bieg zdjec bez budzetu).</summary>
        private void ProcessCells(Scene scene, int gen)
        {
            bool now = MapSideRoads.AutotestFlag;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int removed = 0, made = 0, unloaded = 0;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var c = _active[i];
                if (c.Gen == gen) continue;
                if (!now && (removed > 0 || unloaded > 0) && System.Diagnostics.Stopwatch.GetTimestamp() - t0 > BuildBudgetTicks) break;
                removed += UnloadCell(scene, c);
                _active.RemoveAt(i);
                unloaded++;
            }
            if (_want.Count > 1) _want.Sort(ByDist);
            int loaded = 0;
            for (int w = 0; w < _want.Count; w++)
            {
                var c = _want[w];
                if (!now && (removed > 0 || unloaded > 0 || made > 0) && System.Diagnostics.Stopwatch.GetTimestamp() - t0 > BuildBudgetTicks) break;
                if (!c.Prepared) PrepareCell(c);
                bool wasEmpty = c.Next == 0;
                LoadCell(scene, c, now, t0, ref made);
                if (wasEmpty && c.Next > 0) { _active.Add(c); loaded++; }
                if (_matBad) break;
                if (c.Next < c.Valid && !c.Failed) break;   // budzet skonczyl sie w tej komorce - reszta w nastepnej cwierci sekundy
            }
            _cellLoads += loaded; _cellUnloads += unloaded;
            _addDecals += made; _remDecals += removed;
            if (removed > 0 || made > 0 || unloaded > 0)
            {
                long dt = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                _buildTicks += dt;
                _buildTickCount++;
                if (dt > _maxTickTicks) _maxTickTicks = dt;
            }
            if (_liveDecals > _maxLiveDecals) _maxLiveDecals = _liveDecals;
            if (_active.Count > _maxLiveCells) _maxLiveCells = _active.Count;
            if (!_firstFillLogged && _liveDecals > 0 && Pending() == 0)
            {
                _firstFillLogged = true;
                Log.Info(MapSideRoads.P + "pierwsze wypelnienie komorek - w scenie komorek " + _active.Count + ", decali " + _liveDecals + " (stawianie " + Ms(_buildTicks) + " ms w "
                         + _buildTickCount + " tickach, najdluzszy " + Ms(_maxTickTicks) + " ms" + (now ? ", bieg zdjec - bez budzetu" : "") + "); kolo widoku: srodek ("
                         + _lastCx.ToString("0", Inv) + ", " + _lastCy.ToString("0", Inv) + "), promien " + _lastR.ToString("0", Inv) + " jedn.; komorek przygotowanych " + _prepCells
                         + " (stempli " + _prepStamps + ", pominiete nad woda / brodem / mostem " + _skipWater + ", zle " + _skipBad + ").");
            }
        }

        /// <summary>Ile komorek z biezacego wyboru czeka jeszcze na stawianie (po ProcessCells) - do linii pierwszego wypelnienia.</summary>
        private int Pending()
        {
            int n = 0;
            foreach (var c in _want) if (!c.Failed && (!c.Prepared || c.Next < c.Valid)) n++;
            return n;
        }

        /// <summary>Ramki stempli komorki (raz na komorke): sciana siatki nawigacyjnej ladowa, wysokosc i normalna terenu - jak Build starej drogi.</summary>
        private void PrepareCell(Cell c)
        {
            var ms = Campaign.Current != null ? Campaign.Current.MapSceneWrapper : null;
            var res = _job.Res;
            var look = _job.Look;
            int len = c.Idx.Length;
            c.Fr = new MatrixFrame[len];
            c.Al = new float[len];
            int n = 0;
            if (ms != null)
                for (int i = 0; i < len; i++)
                {
                    try
                    {
                        var st = res.Stamps[c.Idx[i]];
                        var cv = new CampaignVec2(new Vec2(st.X, st.Y), true);
                        PathFaceRecord f = ms.GetFaceIndex(in cv);
                        if (!f.IsValid() || !MapVillageData.IsLandGroup(f.FaceGroupIndex)) { _skipWater++; continue; }
                        float z; Vec3 nn;
                        ms.GetTerrainHeightAndNormal(new Vec2(st.X, st.Y), out z, out nn);
                        if (float.IsNaN(z) || float.IsInfinity(z)) { _skipBad++; continue; }
                        c.Fr[n] = Frame(st, z, nn);
                        c.Al[n] = st.Alpha * look.Alpha;
                        n++;
                    }
                    catch (Exception ex)
                    {
                        _stCreate++;
                        c.Strikes++;
                        if (_stCreate <= 3 || _stCreate % 200 == 0) Log.Error("MapSideRoadsView.PrepareCell (" + c.Cx + ", " + c.Cy + ") stempel " + i + " (potkniecie " + _stCreate + ")", ex);
                    }
                }
            c.Valid = n;
            c.Prepared = ms != null;
            _prepCells++;
            _prepStamps += len;
            if (c.Strikes >= CellStrikes) FailCell(c);
        }

        /// <summary>Stawianie decali komorki od c.Next (ta sama kolejnosc wywolan co Build starej drogi: material przed dodaniem do sceny, ramka i alfa po).</summary>
        private void LoadCell(Scene scene, Cell c, bool now, long t0, ref int made)
        {
            var look = _job.Look;
            while (c.Next < c.Valid && !c.Failed)
            {
                if (!now && made > 0 && System.Diagnostics.Stopwatch.GetTimestamp() - t0 > BuildBudgetTicks) return;
                int k = c.Next++;
                try
                {
                    Decal d = Decal.CreateDecal(null);                       // wzor: NavalMobilePartyVisual (piana na mapie)
                    if ((object)d == null || !d.IsValid) { _skipBad++; continue; }
                    d.SetMaterial(_mat);
                    if (!_matChecked)
                    {
                        Material got = d.GetMaterial();                      // raz: decal ma NASZ material (nie zastepczy)
                        if ((object)got == null || !got.IsValid || (got.Pointer != _mat.Pointer && !string.Equals(got.Name, _mat.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            _noMat++; _matBad = true;
                            Log.Info(MapSideRoads.P + "decal nie przyjal materialu '" + _matName + "' - nic nie stawiam.");
                            return;
                        }
                        _matChecked = true;
                    }
                    if (look.HasArg) d.SetVectorArgument(look.A0, look.A1, look.A2, look.A3);
                    else if (_matName.StartsWith("decal_", StringComparison.Ordinal) && _matName.Contains("_path_"))
                    {
                        float a0, a1, a2, a3;
                        MapRoadsProbe.AtlasArgument(_matName, out a0, out a1, out a2, out a3);
                        d.SetVectorArgument(a0, a1, a2, a3);
                    }
                    d.SetFactor1(look.Factor);
                    scene.AddDecalInstance(d, DecalSet, true);
                    c.Live.Add(d);                                           // od razu w liscie: wyjatek ponizej nie zostawi decala bez zdjecia
                    _liveDecals++;
                    d.Frame = c.Fr[k];
                    float a = c.Al[k];
                    if (a < 0.999f) d.SetAlpha(a);
                    made++;
                }
                catch (Exception ex)
                {
                    _stCreate++;
                    c.Strikes++;
                    if (_stCreate <= 3 || _stCreate % 200 == 0) Log.Error("MapSideRoadsView.LoadCell (" + c.Cx + ", " + c.Cy + ") stempel " + k + " (potkniecie " + _stCreate + ")", ex);
                    if (c.Strikes >= CellStrikes) { FailCell(c); return; }
                }
            }
        }

        /// <summary>Komorka z CellStrikes potknieciami - wylaczona do konca mapy (nie wchodzi do kola; jej decale schodza w nastepnym ProcessCells). Reszta drog dziala.</summary>
        private void FailCell(Cell c)
        {
            if (c.Failed) return;
            c.Failed = true;
            _cellFails++;
            if (_cellFails <= 5) Log.Info(MapSideRoads.P + "komorka (" + c.Cx + ", " + c.Cy + ") wylaczona do konca mapy po " + c.Strikes + " potknieciach (reszta drog bez zmian).");
        }

        /// <summary>Decale komorki ze sceny (jak RemoveAll: schowany, potem zdjety ze zbioru sceny). Zwraca liczbe zdjetych.</summary>
        private int UnloadCell(Scene scene, Cell c)
        {
            int n = 0;
            foreach (var d in c.Live)
            {
                try
                {
                    d.SetIsVisible(false);
                    scene.RemoveDecalInstance(d, DecalSet);
                    n++;
                }
                catch (Exception ex) { _stRemove++; if (_stRemove <= 3 || _stRemove % 200 == 0) Log.Error("MapSideRoadsView.UnloadCell (" + c.Cx + ", " + c.Cy + ")", ex); }
            }
            _liveDecals -= c.Live.Count;
            if (_liveDecals < 0) _liveDecals = 0;
            c.Live.Clear();
            c.Next = 0;
            return n;
        }

        /// <summary>Wszystkie komorki ze sceny (RemoveAll: wylaczenie, koniec mapy, nowy plan, zmiana trybu). Zwraca liczbe zdjetych decali.</summary>
        private int RemoveCells(Scene scene, bool rend)
        {
            int n = 0;
            foreach (var c in _active)
            {
                foreach (var d in c.Live)
                {
                    try
                    {
                        d.SetIsVisible(false);
                        if (rend) scene.RemoveDecalInstance(d, DecalSet);
                        n++;
                    }
                    catch (Exception ex) { _stRemove++; if (_stRemove <= 3) Log.Error("MapSideRoadsView.Remove", ex); }
                }
                c.Live.Clear();
                c.Next = 0;
            }
            _active.Clear();
            _liveDecals = 0;
            _firstFillLogged = false;
            _lastHave = false;
            return n;
        }

        /// <summary>Scena juz nie ta (koniec mapy po zmianie ekranu): same odwolania.</summary>
        private int DropCells()
        {
            int n = _liveDecals;
            foreach (var c in _active) { c.Live.Clear(); c.Next = 0; }
            _active.Clear();
            _liveDecals = 0;
            return n;
        }

        /// <summary>Kawalek linii pomiaru trybu komorek (po "schowanymi: ...").</summary>
        private void ChunkSummary(StringBuilder sb)
        {
            if (_grid == null) return;
            sb.Append("; tryb komorek f22: w scenie komorek ").Append(_active.Count).Append(" z ").Append(_grid.Cells).Append(" (najwiecej ").Append(_maxLiveCells)
              .Append(", decali najwiecej ").Append(_maxLiveDecals).Append("), kolo widoku sr. promien ").Append(_nR > 0 ? (_sumR / _nR).ToString("0", Inv) : "-")
              .Append(" jedn. (teraz ").Append(_lastHave ? _lastR.ToString("0", Inv) : "-").Append("), komorki postawione ").Append(_cellLoads).Append(" razy (decali ").Append(_addDecals)
              .Append("), zdjete ").Append(_cellUnloads).Append(" razy (decali ").Append(_remDecals).Append("), przygotowane ").Append(_prepCells).Append(" (stempli ").Append(_prepStamps)
              .Append(", nad woda / brodem / mostem ").Append(_skipWater).Append(", zle ").Append(_skipBad).Append("), wylaczone ").Append(_cellFails).Append(", skoki kamery ").Append(_jumps)
              .Append("; klatki wedlug komorek w scenie (komorki: klatek / sr. ms / p95 / sr. decali): ");
            for (int b = 0; b < CellBinNames.Length; b++)
            {
                if (b > 0) sb.Append(", ");
                int[] h = _cHist[b];
                long n = 0;
                for (int i = 0; i < Buckets; i++) n += h[i];
                sb.Append(CellBinNames[b]).Append(": ").Append(n);
                if (n == 0) continue;
                long acc = 0; int p95 = -1;
                for (int i = 0; i < Buckets; i++) { acc += h[i]; if (acc * 100 >= n * 95) { p95 = i; break; } }
                sb.Append('/').Append((_cSum[b] / n).ToString("0.0", Inv)).Append('/').Append(p95).Append('/').Append((_cDec[b] / n).ToString("0", Inv));
            }
        }
    }
}
