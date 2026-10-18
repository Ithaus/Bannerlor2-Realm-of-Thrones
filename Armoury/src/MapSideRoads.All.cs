// f19-drogi (Jeff 18.10 wieczorem: "drogi sa, ale wiele wiosek jest bez zadnej drogi, sa w glebi ladu, lasu, ale nie ma tam zadnej drogi - mowie o wioskach
// pobocznych, kazda wioska powinna miec droge do siebie") - REGULA "KAZDA WIOSKA UKLADU" (Params.AllVillages = 1, klucz MapSideRoadsAllVillages, domyslnie TAK;
// NIE = plan f11 bez zmian - MapSideRoads.Plan.cs Build).
//
// Dlaczego plan f11 zostawial wioski bez drogi (log Jeffa 18.10 13:50): siec rosla TYLKO od traktow ROT, najwyzej 14 jedn. prosto i 40 po sieci - kraina
// bez traktu w poblizu (las Polnocy, srodek Dorzecza, Reach, za Murem, stepy Essos) nie dostawala nic (796 + 328 + 66 wiosek), a wioska blisko traktu
// z zasady nie miala drogi (290).
//
// Zasady f19 (czysta geometria, te same pliki i ten sam ksztalt drogi co f11 - Shape, stemple):
//  - WEZLY SIECI od poczatku: farba traktow ROT, miasta i zamki (pierscien tuz za ObstR - "pod brama"), drogi juz polozone, a dla wiosek ukladu takze wsie gry
//    (droga konczy sie w srodku wsi gry - plac pod wsia gry ma ziemie ROT);
//  - KRATA CALEJ MAPY co RouteStep (1 jedn.) z kosztem komorki jak f11 (rownina 1 ... las 1.4, stok x1..4, woda / gory / miasta nieprzejezdne, srodek wioski
//    x2.5) i JEDNO pole odleglosci od sieci (Dijkstra z wielu zrodel); kazda nowa droga staje sie zrodlem i pole poprawia sie tylko tam, gdzie siec podeszla
//    blizej (bez liczenia od nowa);
//  - KOLEJNOSC jak u Prima: zawsze wioska NAJTANSZA do obecnej sieci - wioska za wioska dochodzi wiec do jej drogi (jeden korytarz, rozgalezienia), a nie
//    rownolegla kreska do traktu; zadnych limitow skoku ani dlugosci - wioska bez drogi to tylko wioska bez trasy po ladzie do zadnego wezla (wyspa bez osady);
//  - KOLEJNOSC FAZ: (0) wioski ukladu blisko traktu - krotka laczka pod katem (ponizej), (A) wsie gry dalej od traktu - wlasna droga do sieci, gdy koszt trasy
//    <= GameLinkMax (pien dla wiosek okregu; dalsza wies gry zostaje wezlem dla swoich wiosek), (B) wszystkie pozostale wioski ukladu - bez limitu;
//  - BLISKO TRAKTU (krawedz farby <= 1.5 x pol-szer. + 3.5, jak f11): obrazek wioski NA farbie (krawedz blizej niz obrys obrazka w strone traktu + 0.3) - bez drogi;
//    pozostale - laczka, ktora odchodzi od wioski ku traktowi i skreca w strone najblizszego miasta / zamku, wpada w farbe pod katem ok. 25 st. (wlot przesuniety
//    wzdluz traktu o MergeK x odleglosc w bok; krzywa Beziera, nie prostopadla kreska); laczka po wodzie / przez miasto / przecinajaca farbe przed wlotem -
//    druga strona, krocej, a na koniec zwykla trasa fazy B;
//  - droga krotsza niz MinLenAll (1.2) - wioska styka sie z siecia (obrazek ja przykrywa), bez rysowania; wies gry jak f11 (krotsza niz MinLen - bez rysowania).
// Stemple jak f11 (wyglad Jeffa 18.10, wariant A); koniec przy wsi gry / bramie wezszy i bledszy (jak wyjscie spod domow). Ponad MaxStamps najpierw rzadziej
// (sam odstep do SpacingOnlyMax - zakladka >= 5 stempli, krzywe bez zmian), dopiero potem dluzsze stemple; nigdy urwana droga.
// Pamiec: krata 1601 x 1403 (2.25 mln wezlow) - ok. 25 MB na czas planu (watek z puli, raz na mape), potem do zebrania przez GC.
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Armoury
{
    internal static partial class SideRoadPlan
    {
        private static readonly int[] LDX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] LDY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        private static readonly int[] LOPP = { 1, 0, 3, 2, 7, 6, 5, 4 };       // kierunek przeciwny (z v do u)
        private const byte FPaint = 1, FHub = 2, FFort = 4, FCand = 8;          // flagi wezla kraty
        private const byte DirSrc = 8, DirNone = 255;                            // Dir: 0..7 kierunek do poprzednika, 8 zrodlo (siec), 255 nieosiagniety

        // stan wezlow wioski po planie f19 (podglad poza gra - obrazki i liczby)
        internal const byte StNone = 0, StLane = 1, StOnPaint = 2, StAtNet = 3, StNoRoute = 4, StGameNear = 5, StGameLinked = 6, StGameStub = 7, StGameHub = 8,
                            StGameNoRoute = 9, StCap = 10, StMerged = 11, StGameJoined = 12;

        // ------------------------------------------------------------------ krata calej mapy
        private sealed class Lattice
        {
            public int Nx, Ny;
            public float X0, Y0, Step;
            public byte[] Code;          // 0 nieprzejezdne; 1..20 kod siatki ladu, 21 farba traktu nad '.', +32 srodek wioski (x2.5)
            public byte[] Flag;
            public byte[] Dir;
            public float[] Dist;         // koszt do najblizszego wezla sieci
            public int[] HeapPos;        // -1 poza kopcem
            public int[] Heap = new int[1 << 16];
            public int HeapN;
            public readonly float[] Tab = new float[64];
            public long Relax;
            public readonly List<int> Touched = new List<int>(1024);   // wezly-kandydaci wiosek, ktorym spadl koszt (klucze kolejki wiosek do poprawy)

            internal float X(int v) { return X0 + (v % Nx) * Step; }
            internal float Y(int v) { return Y0 + (v / Nx) * Step; }

            internal int Index(float x, float y)
            {
                int i = (int)Math.Round((x - X0) / Step), j = (int)Math.Round((y - Y0) / Step);
                if (i < 0 || j < 0 || i >= Nx || j >= Ny) return -1;
                return j * Nx + i;
            }

            internal int I0(float x) { int i = (int)Math.Floor((x - X0) / Step); return i < 0 ? 0 : (i >= Nx ? Nx - 1 : i); }
            internal int I1(float x) { int i = (int)Math.Ceiling((x - X0) / Step); return i < 0 ? 0 : (i >= Nx ? Nx - 1 : i); }
            internal int J0(float y) { int j = (int)Math.Floor((y - Y0) / Step); return j < 0 ? 0 : (j >= Ny ? Ny - 1 : j); }
            internal int J1(float y) { int j = (int)Math.Ceiling((y - Y0) / Step); return j < 0 ? 0 : (j >= Ny ? Ny - 1 : j); }
        }

        private static Lattice MakeLattice(Ctx cx)
        {
            var g = cx.G; var m = cx.M; var p = cx.P;
            var L = new Lattice { Step = p.RouteStep };
            L.X0 = (float)Math.Floor(g.X0 - g.Cell * 0.5f);
            L.Y0 = (float)Math.Floor(g.Y0 - g.Cell * 0.5f);
            L.Nx = (int)Math.Ceiling(g.Nx * g.Cell / L.Step) + 1;
            L.Ny = (int)Math.Ceiling(g.Ny * g.Cell / L.Step) + 1;
            int n = L.Nx * L.Ny;
            L.Code = new byte[n]; L.Flag = new byte[n]; L.Dir = new byte[n]; L.Dist = new float[n]; L.HeapPos = new int[n];
            for (int c = 1; c < 64; c++)
            {
                int b = c & 31;
                float v = b >= 1 && b <= 20 ? CodeCost((byte)b) : (b == 21 ? 1.5f : 0f);
                if ((c & 32) != 0) v *= 2.5f;
                L.Tab[c] = v;
            }
            for (int j = 0; j < L.Ny; j++)
            {
                float y = L.Y0 + j * L.Step;
                int row = j * L.Nx;
                for (int i = 0; i < L.Nx; i++) L.Code[row + i] = g.At(L.X0 + i * L.Step, y);
            }
            for (int v = 0; v < n; v++) { L.Dist[v] = float.PositiveInfinity; L.Dir[v] = DirNone; L.HeapPos[v] = -1; }
            // farba traktow: przejezdna nad '.' (jak CostAt: os <= pol-szer. + 0.5) i cel (os <= pol-szer. + 0.75 - jak cel trasy f11)
            for (int k = 0; k < m.Segments; k++)
            {
                float h = m.Half[k], rg = h + 0.75f;
                float ax = m.Ax[k], ay = m.Ay[k], dx = m.Dx[k], dy = m.Dy[k], il = m.InvL2[k];
                int i0 = L.I0(Math.Min(ax, ax + dx) - rg), i1 = L.I1(Math.Max(ax, ax + dx) + rg);
                int j0 = L.J0(Math.Min(ay, ay + dy) - rg), j1 = L.J1(Math.Max(ay, ay + dy) + rg);
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        float qx = L.X0 + i * L.Step - ax, qy = L.Y0 + j * L.Step - ay;
                        float t = (qx * dx + qy * dy) * il;
                        if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                        float ex = qx - t * dx, ey = qy - t * dy;
                        float d = (float)Math.Sqrt(ex * ex + ey * ey);
                        if (d > rg) continue;
                        int v = j * L.Nx + i;
                        if (d <= h + 0.5f && L.Code[v] == 0) L.Code[v] = 21;
                        L.Flag[v] |= FPaint;
                    }
            }
            // miasta i zamki: kolo ObstR nieprzejezdne; pierscien tuz za nim (do ObstR + 1) - wezel sieci "pod brama"
            float r0 = p.ObstR, r1 = p.ObstR + 1f;
            foreach (var o in cx.Obst) Disc(L, o.X, o.Y, r0, (v, d) => L.Code[v] = 0);
            foreach (var o in cx.Obst) Disc(L, o.X, o.Y, r1, (v, d) => { if (d > r0) L.Flag[v] |= FFort; });
            // srodki wiosek (wszystkich, takze tej, ktora wlasnie prowadzi droge - stala reszta przy starcie) x2.5
            if (p.VillageAvoidR > 0f)
                foreach (var nd in cx.Nodes) Disc(L, nd.X, nd.Y, p.VillageAvoidR, (v, d) => { if (L.Code[v] != 0) L.Code[v] |= 32; });
            return L;
        }

        private static void Disc(Lattice L, float x, float y, float r, Action<int, float> f)
        {
            int i0 = L.I0(x - r), i1 = L.I1(x + r), j0 = L.J0(y - r), j1 = L.J1(y + r);
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    float dx = L.X0 + i * L.Step - x, dy = L.Y0 + j * L.Step - y;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d <= r) f(j * L.Nx + i, d);
                }
        }

        private static void LSeed(Lattice L, int v)
        {
            if (v < 0 || L.Code[v] == 0 || L.Dist[v] <= 0f) return;
            L.Dist[v] = 0f; L.Dir[v] = DirSrc;
            if ((L.Flag[v] & FCand) != 0) L.Touched.Add(v);
            if (L.HeapPos[v] >= 0) LUp(L, L.HeapPos[v]); else LPush(L, v);
        }

        /// <summary>Dijkstra od zrodel w kopcu; poprawia tylko wezly, ktorym spadl koszt (nowa droga = nowe zrodla, reszta pola bez zmian).</summary>
        private static void LFlood(Lattice L)
        {
            int nx = L.Nx, ny = L.Ny;
            float st = L.Step * 0.5f, dg = L.Step * 1.41421356f * 0.5f;
            while (L.HeapN > 0)
            {
                int u = LPop(L);
                float du = L.Dist[u], cu = L.Tab[L.Code[u]];
                int ui = u % nx, uj = u / nx;
                for (int d = 0; d < 8; d++)
                {
                    int vi = ui + LDX[d], vj = uj + LDY[d];
                    if ((uint)vi >= (uint)nx || (uint)vj >= (uint)ny) continue;
                    int v = vj * nx + vi;
                    byte cc = L.Code[v];
                    if (cc == 0) continue;
                    float nd = du + (d < 4 ? st : dg) * (cu + L.Tab[cc]);
                    if (nd < L.Dist[v])
                    {
                        L.Dist[v] = nd; L.Dir[v] = (byte)LOPP[d];
                        L.Relax++;
                        if ((L.Flag[v] & FCand) != 0) L.Touched.Add(v);
                        if (L.HeapPos[v] >= 0) LUp(L, L.HeapPos[v]); else LPush(L, v);
                    }
                }
            }
        }

        private static void LPush(Lattice L, int v)
        {
            if (L.HeapN == L.Heap.Length) Array.Resize(ref L.Heap, L.Heap.Length * 2);
            int i = L.HeapN++;
            L.Heap[i] = v; L.HeapPos[v] = i;
            LUp(L, i);
        }

        private static void LUp(Lattice L, int i)
        {
            int v = L.Heap[i];
            float d = L.Dist[v];
            while (i > 0)
            {
                int pa = (i - 1) >> 1;
                int u = L.Heap[pa];
                if (L.Dist[u] <= d) break;
                L.Heap[i] = u; L.HeapPos[u] = i; i = pa;
            }
            L.Heap[i] = v; L.HeapPos[v] = i;
        }

        private static int LPop(Lattice L)
        {
            int top = L.Heap[0];
            L.HeapPos[top] = -1;
            int n = --L.HeapN;
            if (n > 0)
            {
                int v = L.Heap[n];
                float d = L.Dist[v];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1;
                    if (l >= n) break;
                    int r = l + 1;
                    int c = (r < n && L.Dist[L.Heap[r]] < L.Dist[L.Heap[l]]) ? r : l;
                    if (L.Dist[L.Heap[c]] >= d) break;
                    L.Heap[i] = L.Heap[c]; L.HeapPos[L.Heap[i]] = i; i = c;
                }
                L.Heap[i] = v; L.HeapPos[v] = i;
            }
            return top;
        }

        // ------------------------------------------------------------------ wioski do polaczenia (kolejka jak u Prima)
        private sealed class Term
        {
            public int Ni;
            public int[] C;              // wezly kraty przy wiosce (do 1.5 jedn.; wioska na probce '.' przy brzegu - do 3 jedn.)
            public float[] Pen;          // koszt dojscia od wioski do wezla
            public bool Active, Done;
        }

        private static Term MakeTerm(Lattice L, Ctx cx, int ni)
        {
            var nd = cx.Nodes[ni];
            int vn = L.Index(nd.X, nd.Y);
            float cv = vn >= 0 && L.Code[vn] != 0 ? L.Tab[L.Code[vn]] : 2f;
            var cs = new List<int>(12); var ps = new List<float>(12);
            for (float r = 1.5f; r <= 3.01f && cs.Count == 0; r += 1.5f)
            {
                float rr = r;
                Disc(L, nd.X, nd.Y, rr, (v, d) => { if (L.Code[v] != 0) { cs.Add(v); ps.Add(d * 0.5f * (cv + L.Tab[L.Code[v]])); } });
            }
            return new Term { Ni = ni, C = cs.ToArray(), Pen = ps.ToArray() };
        }

        private static float TermValue(Lattice L, Term t, out int bk)
        {
            bk = -1;
            float bv = float.PositiveInfinity;
            for (int k = 0; k < t.C.Length; k++)
            {
                float v = L.Dist[t.C[k]] + t.Pen[k];
                if (v < bv) { bv = v; bk = k; }
            }
            return bv;
        }

        /// <summary>Kopiec min (klucz, wioska); przy rownym kluczu mniejszy numer - kolejnosc niezalezna od historii wstawiania.</summary>
        private sealed class KeyHeap
        {
            private float[] _k = new float[1024];
            private int[] _v = new int[1024];
            public int Count;

            private bool Less(int a, int b) { return _k[a] < _k[b] || (_k[a] == _k[b] && _v[a] < _v[b]); }

            private void Swap(int a, int b) { float k = _k[a]; _k[a] = _k[b]; _k[b] = k; int v = _v[a]; _v[a] = _v[b]; _v[b] = v; }

            internal void Push(float k, int v)
            {
                if (Count == _k.Length) { Array.Resize(ref _k, Count * 2); Array.Resize(ref _v, Count * 2); }
                int i = Count++;
                _k[i] = k; _v[i] = v;
                while (i > 0) { int pa = (i - 1) >> 1; if (!Less(i, pa)) break; Swap(i, pa); i = pa; }
            }

            internal void Pop(out float k, out int v)
            {
                k = _k[0]; v = _v[0];
                int n = --Count;
                if (n <= 0) return;
                _k[0] = _k[n]; _v[0] = _v[n];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1;
                    if (l >= n) break;
                    int r = l + 1;
                    int c = (r < n && Less(r, l)) ? r : l;
                    if (!Less(c, i)) break;
                    Swap(c, i); i = c;
                }
            }
        }

        private sealed class AllState
        {
            public Lattice L;
            public Term[] Terms;                                    // indeks jak wioska (null - nie czeka na droge)
            public Dictionary<int, List<int>> CandOf = new Dictionary<int, List<int>>();
            public KeyHeap Q = new KeyHeap();
            public byte[] St;
            public double ShapeMs;
        }

        /// <summary>Wezly, ktorym spadl koszt: wioski (czynne, bez drogi) z nowym kluczem w kolejce.</summary>
        private static void Requeue(AllState a)
        {
            var L = a.L;
            foreach (int v in L.Touched)
            {
                List<int> ts;
                if (!a.CandOf.TryGetValue(v, out ts)) continue;
                foreach (int ti in ts)
                {
                    var t = a.Terms[ti];
                    if (t == null || !t.Active || t.Done) continue;
                    int bk;
                    float val = TermValue(L, t, out bk);
                    if (!float.IsInfinity(val)) a.Q.Push(val, ti);
                }
            }
            L.Touched.Clear();
        }

        // ------------------------------------------------------------------ plan f19
        internal static Result BuildAll(LandGrid g, MainRoads m, List<Node> nodes, List<Obstacle> obst, Params p)
        {
            var sw = Stopwatch.StartNew();
            var res = new Result { All = true };
            var cx = new Ctx { G = g, M = m, P = p, Nodes = nodes, Obst = obst };
            for (int i = 0; i < obst.Count; i++) BAdd(cx.ObstB, obst[i].X, obst[i].Y, i);
            for (int i = 0; i < nodes.Count; i++) BAdd(cx.NodeB, nodes[i].X, nodes[i].Y, i);
            res.Nodes = nodes.Count;
            var a = new AllState { St = new byte[nodes.Count], Terms = new Term[nodes.Count] };
            var L = MakeLattice(cx);
            a.L = L;
            res.LatNx = L.Nx; res.LatNy = L.Ny;

            // faza 0: blisko traktu - obrazek na farbie bez drogi, reszta wiosek ukladu laczka pod katem; dalsze - do kolejki (wsie gry faza A, wioski ukladu faza B)
            var gameT = new List<int>(); var layT = new List<int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var nd = nodes[i];
                if (nd.Game) res.GameNodes++; else res.LayoutNodes++;
                float centr, half, px, py; int seg;
                bool any = m.NearestSeg(nd.X, nd.Y, 12f, out centr, out half, out px, out py, out seg);
                float edge = any ? centr - half : float.MaxValue;
                bool near = any && edge <= p.NearHalfFactor * half + p.NearExtra;
                if (nd.Game)
                {
                    if (near) { res.GameNear++; a.St[i] = StGameNear; }
                    else gameT.Add(i);
                    continue;
                }
                // laczka pod katem: blisko traktu zawsze; dalej (do MergeEdge od farby) - gdy wies gry ani brama nie sa blizej niz trakt (zwykla trasa fazy B
                // do traktu z 4-8 jedn. bylaby krotka prostopadla kreska)
                if (near || (any && edge <= p.MergeEdge && !HubCloser(cx, nd, edge)))
                {
                    float ux = centr > 1e-3f ? (px - nd.X) / centr : 1f, uy = centr > 1e-3f ? (py - nd.Y) / centr : 0f;
                    if (near && edge <= Extent(nd, ux, uy) + p.OnPaintPad) { res.OnPaint++; a.St[i] = StOnPaint; continue; }
                    if (res.Roads.Count < p.MaxRoads)
                    {
                        float[] rx, ry; byte how;
                        long t0 = Stopwatch.GetTimestamp();
                        bool ok = MergeLane(cx, i, seg, px, py, centr, half, out rx, out ry, out how);
                        a.ShapeMs += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                        if (ok)
                        {
                            AddRoad(a, cx, res, i, rx, ry, 0, -1, 0, how);
                            res.Merged++; a.St[i] = StMerged;
                            continue;
                        }
                    }
                    res.MergeFail++;
                }
                layT.Add(i);
            }
            // zrodla: farba traktow, pierscienie miast i zamkow (laczki fazy 0 juz zasiane w AddRoad)
            for (int v = 0; v < L.Code.Length; v++) if ((L.Flag[v] & (FPaint | FFort)) != 0) LSeed(L, v);
            foreach (int i in gameT) MakeTermAt(a, cx, i);
            foreach (int i in layT) MakeTermAt(a, cx, i);
            LFlood(L);
            L.Touched.Clear();
            res.LatMs = sw.Elapsed.TotalMilliseconds;

            // faza A: wsie gry - wlasna droga do sieci przy koszcie <= GameLinkMax (pien dla wiosek okregu)
            Activate(a, gameT);
            Prim(a, cx, res, p.GameLinkMax, 1);
            foreach (int i in gameT)
            {
                var t = a.Terms[i];
                if (t.Done) continue;
                int bk;
                bool inf = float.IsInfinity(TermValue(L, t, out bk));
                if (inf) { res.GameNoRoute++; a.St[i] = StGameNoRoute; } else { res.GameOverCap++; a.St[i] = StGameHub; }
                t.Active = false; t.Done = true;
            }

            // faza B: wsie gry jako wezly sieci, potem kazda pozostala wioska ukladu (bez limitu - najtansza do obecnej sieci najpierw)
            foreach (var nd in nodes)
                if (nd.Game) Disc(L, nd.X, nd.Y, p.HubR, (v, d) => { if (L.Code[v] != 0) { L.Flag[v] |= FHub; LSeed(L, v); } });
            LFlood(L);
            L.Touched.Clear();
            Activate(a, layT);
            Prim(a, cx, res, float.PositiveInfinity, 2);
            foreach (int i in layT)
            {
                var t = a.Terms[i];
                if (t.Done) continue;
                int bk;
                if (float.IsInfinity(TermValue(L, t, out bk)))
                {
                    res.NoRouteLayout++; a.St[i] = StNoRoute;
                    var nd = nodes[i];
                    res.NoRouteNames.Add((string.IsNullOrEmpty(nd.Name) ? nd.Id : nd.Name) + " (" + nd.X.ToString("0", Inv) + ", " + nd.Y.ToString("0", Inv) + ")");
                }
                else { res.CapLayout++; a.St[i] = StCap; }
                t.Done = true;
            }

            // faza C: wezly wsi gry bez polaczenia z traktem / brama (z drogami wiosek, ktore do nich dochodza) - dolaczone do sieci od najtanszego punktu
            PhaseC(a, cx, res);
            res.Relax = L.Relax;
            res.ShapeMs = a.ShapeMs;

            // stemple: ponad MaxStamps odstep x f (tyle stempli mniej - kazda droga cala); dlugosc stempla bez zmian do f = SpacingOnlyMax (zakladka >= 5 stempli,
            // krzywe jak dotad), dalej rosnie razem z odstepem (zakladka jak przy SpacingOnlyMax)
            foreach (var rd in res.Roads) { res.Length += rd.Length; if (rd.Length > res.MaxRoadLen) res.MaxRoadLen = rd.Length; }
            double need = res.Length / p.Spacing, room = p.MaxStamps - 1.2 * res.Roads.Count;
            if (p.MaxStamps > 0 && need > room)
            {
                float f = room > 0.25 * p.MaxStamps ? (float)(need / room * 1.03) : (float)(need / (0.25 * p.MaxStamps));
                res.SpaceScale = f;
                res.StampScale = Math.Max(1f, f / p.SpacingOnlyMax);
            }
            for (int r = 0; r < res.Roads.Count; r++) Stamps(res.Roads[r], r, p, res);
            res.States = a.St;
            res.Ms = sw.Elapsed.TotalMilliseconds;
            return res;
        }

        /// <summary>Faza C. Czesc sieci bez korzenia = wies gry bez wlasnej drogi do traktu / bramy (stan "wezel" albo "bez trasy" po fazie A) razem z drogami
        /// wiosek, ktore do niej dochodza (wprost albo przez inne drogi). Pole odleglosci od nowa - tylko od korzenia (farba, bramy, drogi z korzeniem, wsie gry
        /// przy trakcie / z wlasna droga); kazda czesc to jedna "wioska" kolejki z wezlami wszystkich swoich drog i placu wsi gry, wiec laczy sie od punktu
        /// najblizszego sieci (koniec drogi wioski od strony traktu), a nie rownolegla kreska od srodka wsi gry. Koszt <= HubLinkMax; dalej - siec lokalna.</summary>
        private static void PhaseC(AllState a, Ctx cx, Result res)
        {
            var L = a.L; var p = cx.P; var nodes = cx.Nodes;
            int nr = res.Roads.Count;
            var rooted = new bool[nr];
            var comp = new int[nr];
            for (int r = 0; r < nr; r++)
            {
                var rd = res.Roads[r];
                comp[r] = -1;
                if (rd.Join == 1 && rd.Parent >= 0 && rd.Parent < r) { rooted[r] = rooted[rd.Parent]; comp[r] = comp[rd.Parent]; }
                else if (rd.Join == 2 && rd.Hub >= 0 && !HubRooted(a.St[rd.Hub])) { rooted[r] = false; comp[r] = rd.Hub; }
                else rooted[r] = true;
            }
            var lanesOf = new Dictionary<int, List<int>>();
            for (int r = 0; r < nr; r++)
                if (!rooted[r])
                {
                    List<int> l;
                    if (!lanesOf.TryGetValue(comp[r], out l)) { l = new List<int>(); lanesOf[comp[r]] = l; }
                    l.Add(r);
                }
            // pole od nowa: tylko korzen
            for (int v = 0; v < L.Dist.Length; v++) { L.Dist[v] = float.PositiveInfinity; L.Dir[v] = DirNone; L.HeapPos[v] = -1; L.Flag[v] = (byte)(L.Flag[v] & ~FCand); }
            L.HeapN = 0;
            L.Touched.Clear();
            a.CandOf.Clear();
            Action<int, float> seed = (v, d) => LSeed(L, v);
            for (int v = 0; v < L.Code.Length; v++) if ((L.Flag[v] & (FPaint | FFort)) != 0) LSeed(L, v);
            for (int r = 0; r < nr; r++)
                if (rooted[r]) { var rd = res.Roads[r]; for (int k = 0; k < rd.X.Length; k++) Disc(L, rd.X[k], rd.Y[k], 0.75f, seed); }
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].Game && HubRooted(a.St[i])) Disc(L, nodes[i].X, nodes[i].Y, p.HubR, seed);
            // czesci bez korzenia: wezly placu wsi gry i wszystkich drog czesci
            var list = new List<int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!nodes[i].Game || HubRooted(a.St[i])) continue;
                var set = new HashSet<int>();
                Disc(L, nodes[i].X, nodes[i].Y, p.HubR, (v, d) => { if (L.Code[v] != 0) set.Add(v); });
                List<int> lanes;
                if (lanesOf.TryGetValue(i, out lanes))
                    foreach (int r in lanes) { var rd = res.Roads[r]; for (int k = 0; k < rd.X.Length; k++) Disc(L, rd.X[k], rd.Y[k], 0.75f, (v, d) => { if (L.Code[v] != 0) set.Add(v); }); }
                var cs = new int[set.Count];
                set.CopyTo(cs);
                Array.Sort(cs);
                a.Terms[i] = new Term { Ni = i, C = cs, Pen = new float[cs.Length] };
                foreach (int v in cs)
                {
                    List<int> l;
                    if (!a.CandOf.TryGetValue(v, out l)) { l = new List<int>(2); a.CandOf[v] = l; }
                    l.Add(i);
                    L.Flag[v] |= FCand;
                }
                list.Add(i);
            }
            LFlood(L);
            L.Touched.Clear();
            Activate(a, list);
            Prim(a, cx, res, p.HubLinkMax, 3);
            foreach (int i in list)
            {
                var t = a.Terms[i];
                if (t.Done) continue;
                t.Done = true;
                if (lanesOf.ContainsKey(i)) { res.HubLocal++; continue; }
                res.HubLonely++;
                int bk;
                var nd = nodes[i];
                res.LonelyNames.Add((string.IsNullOrEmpty(nd.Name) ? nd.Id : nd.Name) + " (" + nd.X.ToString("0", Inv) + ", " + nd.Y.ToString("0", Inv) + ", "
                                    + (float.IsInfinity(TermValue(L, t, out bk)) ? "bez trasy po ladzie" : "dalej niz prog") + ")");
            }
        }

        /// <summary>Wies gry polaczona z korzeniem sieci: przy trakcie, z wlasna droga, ostroga (tuz przy sieci) albo dolaczona w fazie C.</summary>
        private static bool HubRooted(byte st) { return st == StGameNear || st == StGameLinked || st == StGameStub || st == StGameJoined; }

        private static void MakeTermAt(AllState a, Ctx cx, int i)
        {
            var t = MakeTerm(a.L, cx, i);
            a.Terms[i] = t;
            foreach (int v in t.C)
            {
                List<int> l;
                if (!a.CandOf.TryGetValue(v, out l)) { l = new List<int>(2); a.CandOf[v] = l; }
                l.Add(i);
                a.L.Flag[v] |= FCand;
            }
        }

        private static void Activate(AllState a, List<int> list)
        {
            foreach (int i in list)
            {
                var t = a.Terms[i];
                t.Active = true;
                int bk;
                float val = TermValue(a.L, t, out bk);
                if (!float.IsInfinity(val)) a.Q.Push(val, i);
            }
        }

        /// <summary>Kolejka wiosek: najtansza do obecnej sieci; droga = trasa po polu odleglosci do wezla sieci, ksztalt jak f11; po kazdej drodze pole poprawione.
        /// cap - koszt, powyzej ktorego faza sie konczy (faza A); faza 1 = wsie gry, 2 = wioski ukladu.</summary>
        private static void Prim(AllState a, Ctx cx, Result res, float cap, int phase)
        {
            var L = a.L; var p = cx.P;
            while (a.Q.Count > 0)
            {
                float key; int ti;
                a.Q.Pop(out key, out ti);
                var t = a.Terms[ti];
                if (t == null || t.Done || !t.Active) continue;
                int bk;
                float val = TermValue(L, t, out bk);
                if (float.IsInfinity(val)) continue;
                if (val < key - 1e-4f) { a.Q.Push(val, ti); continue; }   // klucz nieaktualny - siec podeszla blizej
                if (val > cap) { a.Q.Push(val, ti); break; }              // faza A: reszta wsi gry dalej niz prog
                if (res.Roads.Count >= p.MaxRoads) { res.CapRoads++; continue; }
                t.Done = true;
                int why = RouteAll(a, cx, res, t, bk, phase);
                if (phase == 1)
                {
                    if (why == 0) { res.GameLinked++; a.St[ti] = StGameLinked; }
                    else if (why == 5) { res.GameStub++; a.St[ti] = StGameStub; }
                    else { res.GameNoRoute++; a.St[ti] = StGameNoRoute; }
                }
                else if (phase == 3)
                {
                    // czesc dolaczona (droga albo tuz przy sieci): jej wezly staja sie korzeniem - inne czesci moga dojsc do niej
                    if (why == 0) { res.HubJoined++; a.St[ti] = StGameJoined; }
                    else if (why == 5) { res.HubTouch++; a.St[ti] = StGameJoined; }
                    else { res.HubLocal++; continue; }
                    foreach (int v in t.C) LSeed(L, v);
                    LFlood(L);
                    Requeue(a);
                }
                else
                {
                    if (why == 0) { res.Routed++; a.St[ti] = StLane; }
                    else if (why == 4) { res.AtNet++; a.St[ti] = StAtNet; }
                    else
                    {
                        res.NoRouteLayout++; a.St[ti] = StNoRoute;
                        var nd = cx.Nodes[ti];
                        res.NoRouteNames.Add((string.IsNullOrEmpty(nd.Name) ? nd.Id : nd.Name) + " (zerwana trasa)");
                    }
                }
            }
            a.Q = new KeyHeap();
        }

        /// <summary>Trasa wioski po polu odleglosci do wezla sieci. 0 = droga dodana; 2 = trasa zerwana (potkniecie, bez drogi); 4 = styk z siecia (krotsza niz
        /// MinLenAll, wioska ukladu); 5 = ostroga wsi gry (krotsza niz MinLen).</summary>
        private static int RouteAll(AllState a, Ctx cx, Result res, Term t, int bk, int phase)
        {
            var L = a.L; var p = cx.P;
            var nd = cx.Nodes[t.Ni];
            int c = t.C[bk];
            float pen = t.Pen[bk];
            var path = new List<int>(64);
            int u = c, guard = 0;
            while (true)
            {
                path.Add(u);
                byte d = L.Dir[u];
                if (d == DirSrc) break;
                if (d >= 8 || ++guard > 400000) { res.PathBroken++; return 2; }
                u = (u / L.Nx + LDY[d]) * L.Nx + (u % L.Nx + LDX[d]);
            }
            int s = path[path.Count - 1];
            float sx = L.X(s), sy = L.Y(s);
            // co jest na koncu: trakt (wlot w farbe jak f11), boczna droga (punkt jej osi), wies gry (srodek), brama (wezel pierscienia)
            int kind, goalNet = -1, hub = -1;
            float ex = sx, ey = sy;
            float centr, half, qx, qy;
            float dn;
            int q;
            if ((L.Flag[s] & FPaint) != 0 && cx.M.Nearest(sx, sy, 3f, out centr, out half, out qx, out qy))
            {
                kind = 0;
                float lx = sx - qx, ly = sy - qy, ll = (float)Math.Sqrt(lx * lx + ly * ly);
                if (ll > 1e-3f) { ex = qx + lx / ll * half * 0.5f; ey = qy + ly / ll * half * 0.5f; } else { ex = qx; ey = qy; }
            }
            else if ((q = cx.N.Nearest(sx, sy, 0.8f, out dn)) >= 0 && cx.N.R[q] >= 0)
            {
                kind = 1; goalNet = q; ex = cx.N.X[q]; ey = cx.N.Y[q];
            }
            else if ((L.Flag[s] & FHub) != 0 && (hub = NearestGame(cx, sx, sy, p.HubR + 0.75f, out qx, out qy)) >= 0)
            {
                kind = 2; ex = qx; ey = qy;
            }
            else if ((L.Flag[s] & FFort) != 0) kind = 3;
            else { kind = 3; res.EndOdd++; }   // zrodlo bez rozpoznanego celu (nie powinno sie zdarzyc) - koniec w wezle, licznik w linii planu
            var px0 = new List<float>(path.Count + 1); var py0 = new List<float>(path.Count + 1); var pc = new List<float>(path.Count + 1);
            float bx = nd.X, by = nd.Y;
            if (phase == 3)
            {
                // faza C: start w punkcie czesci - plac wsi gry (srodek) albo najblizszy punkt osi jej drogi (rozwidlenie, nie rownolegla kreska)
                float cxp = L.X(c), cyp = L.Y(c);
                int qs;
                if (Dist(cxp, cyp, nd.X, nd.Y) > p.HubR + 0.01f && (qs = cx.N.Nearest(cxp, cyp, 0.8f, out dn)) >= 0) { bx = cx.N.X[qs]; by = cx.N.Y[qs]; }
            }
            px0.Add(bx); py0.Add(by); pc.Add(0f);
            float dc = L.Dist[c];
            foreach (int v in path) { px0.Add(L.X(v)); py0.Add(L.Y(v)); pc.Add(pen + dc - L.Dist[v]); }
            px0[px0.Count - 1] = ex; py0[py0.Count - 1] = ey;

            uint seed = Hash((uint)p.Seed, (uint)t.Ni);
            float[] rx, ry;
            long t0 = Stopwatch.GetTimestamp();
            if (kind >= 2) { cx.EndX = ex; cx.EndY = ey; cx.EndR2 = 2.25f; }
            byte how = Shape(cx, px0, py0, pc, t.Ni, seed, out rx, out ry);
            cx.EndR2 = 0f;
            a.ShapeMs += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            float len = 0f;
            for (int k = 1; k < rx.Length; k++) len += Dist(rx[k - 1], ry[k - 1], rx[k], ry[k]);
            if ((phase == 1 || phase == 3) && len < p.MinLen) return 5;
            if (phase == 2 && len < p.MinLenAll) return 4;
            int parent = -1;
            if (kind == 1) { int pr = cx.N.R[goalNet]; parent = pr >= cx.Nodes.Count ? pr - cx.Nodes.Count : -1; }
            AddRoad(a, cx, res, t.Ni, rx, ry, kind, parent, phase, how, hub);
            LFlood(L);
            Requeue(a);
            return 0;
        }

        /// <summary>Droga do wyniku i do sieci: punkty osi do kubelkow (cel "inna boczna"), wezly kraty do 0.75 jedn. od osi - zrodla pola (zalew wola wolajacy).</summary>
        private static void AddRoad(AllState a, Ctx cx, Result res, int ni, float[] rx, float[] ry, int kind, int parent, int phase, byte how, int hub = -1)
        {
            float len = 0f;
            for (int k = 1; k < rx.Length; k++) len += Dist(rx[k - 1], ry[k - 1], rx[k], ry[k]);
            int ri = res.Roads.Count;
            var rd = new Road { Node = ni, X = rx, Y = ry, Length = len, Join = kind, Parent = parent, Pass = phase, Smooth = how, Hub = kind == 2 ? hub : -1 };
            switch (kind) { case 0: res.JoinMain++; break; case 1: res.JoinSide++; break; case 2: res.JoinHub++; break; default: res.JoinFort++; break; }
            res.SmoothKinds[how]++;
            res.Roads.Add(rd);
            int rid = cx.Nodes.Count + ri;
            var L = a.L;
            Action<int, float> seed = (v, d) => LSeed(L, v);
            for (int k = 0; k < rx.Length; k++)
            {
                cx.N.Add(rx[k], ry[k], 0f, rid);
                Disc(L, rx[k], ry[k], 0.75f, seed);
            }
        }

        /// <summary>Wies gry albo brama miasta / zamku blizej niz krawedz farby traktu (edge) - wioska idzie wtedy zwykla trasa (do wezla), nie laczka do traktu.</summary>
        private static bool HubCloser(Ctx cx, Node nd, float edge)
        {
            float gx, gy;
            if (NearestGame(cx, nd.X, nd.Y, edge, out gx, out gy) >= 0) return true;
            float r = cx.P.ObstR + edge;
            foreach (var o in cx.Obst)
            {
                float dx = o.X - nd.X, dy = o.Y - nd.Y;
                if (dx * dx + dy * dy <= r * r) return true;
            }
            return false;
        }

        /// <summary>Najblizsza wies gry w promieniu r: indeks wezla (-1 brak) i polozenie.</summary>
        private static int NearestGame(Ctx cx, float x, float y, float r, out float gx, out float gy)
        {
            gx = gy = 0f;
            float best = r * r;
            int any = -1;
            int ci = (int)Math.Floor(x / 4f), cj = (int)Math.Floor(y / 4f), rr = (int)Math.Ceiling(r / 4f);
            for (int j = cj - rr; j <= cj + rr; j++)
                for (int i = ci - rr; i <= ci + rr; i++)
                {
                    List<int> l;
                    if (!cx.NodeB.TryGetValue(((long)i << 32) ^ (uint)j, out l)) continue;
                    foreach (int id in l)
                    {
                        var nd = cx.Nodes[id];
                        if (!nd.Game) continue;
                        float dx = nd.X - x, dy = nd.Y - y, d2 = dx * dx + dy * dy;
                        if (d2 <= best) { best = d2; gx = nd.X; gy = nd.Y; any = id; }
                    }
                }
            return any;
        }

        /// <summary>Obrys obrazka wioski w kierunku (ux, uy) - pol-dlugosc wzdluz ulicy (lokalna X) i pol-szerokosc (lokalna Y) po obrocie FrontDeg; nieznany - 1 jedn.</summary>
        private static float Extent(Node nd, float ux, float uy)
        {
            if (!(nd.Hl > 0f) || !(nd.Hw > 0f)) return 1f;
            double th = nd.FrontDeg * Math.PI / 180.0;
            float c = (float)Math.Cos(th), s = (float)Math.Sin(th);
            return nd.Hl * Math.Abs(ux * c + uy * s) + nd.Hw * Math.Abs(-ux * s + uy * c);
        }

        // ------------------------------------------------------------------ laczka przy trakcie (faza 0)
        private static float SegLen(MainRoads m, int k) { return (float)Math.Sqrt(m.Dx[k] * m.Dx[k] + m.Dy[k] * m.Dy[k]); }

        /// <summary>Wzdluz lamanej traktu od (k, t) o dist w strone dir (+1 = rosnace odcinki); konczy sie na koncu lamanej. Zwraca przebyta droge.</summary>
        private static float Walk(MainRoads m, ref int k, ref float t, int dir, float dist)
        {
            float got = 0f;
            for (int guard = 0; guard <= m.Segments; guard++)
            {
                float len = SegLen(m, k);
                float room = dir > 0 ? (1f - t) * len : t * len;
                if (got + room >= dist)
                {
                    if (len > 1e-6f) t += dir * (dist - got) / len;
                    if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                    return dist;
                }
                got += room;
                int nk = k + dir;
                if (nk < 0 || nk >= m.Segments || m.LineOf[nk] != m.LineOf[k]) { t = dir > 0 ? 1f : 0f; return got; }
                k = nk; t = dir > 0 ? 0f : 1f;
            }
            return got;
        }

        /// <summary>Kierunek traktu wygladzony na +-2 jedn. lamanej (odcinki z rastra ida schodkami 0 / 90 st.).</summary>
        private static void Tangent(MainRoads m, int k, float t, out float tx, out float ty)
        {
            int k1 = k, k2 = k;
            float t1 = t, t2 = t;
            Walk(m, ref k1, ref t1, -1, 2f);
            Walk(m, ref k2, ref t2, 1, 2f);
            tx = (m.Ax[k2] + m.Dx[k2] * t2) - (m.Ax[k1] + m.Dx[k1] * t1);
            ty = (m.Ay[k2] + m.Dy[k2] * t2) - (m.Ay[k1] + m.Dy[k1] * t1);
            float l = (float)Math.Sqrt(tx * tx + ty * ty);
            if (l < 1e-4f) { tx = m.Dx[k]; ty = m.Dy[k]; l = (float)Math.Sqrt(tx * tx + ty * ty); }
            if (l < 1e-6f) { tx = 1f; ty = 0f; return; }
            tx /= l; ty /= l;
        }

        /// <summary>f19 (c) - laczka wioski blisko traktu: odchodzi od wioski ku traktowi, skreca w strone najblizszego miasta / zamku i wpada w farbe pod katem
        /// ok. 25 st. (wlot przesuniety wzdluz traktu o MergeK x odleglosc w bok, krzywa Beziera). Proby: ta strona, druga strona, to samo krocej. False - zadna
        /// nie przeszla sprawdzianu (lad, miasta, farba przecieta przed wlotem).</summary>
        private static bool MergeLane(Ctx cx, int ni, int seg, float px, float py, float centr, float half, out float[] rx, out float[] ry, out byte how)
        {
            rx = ry = null; how = 1;
            var p = cx.P; var m = cx.M; var nd = cx.Nodes[ni];
            float vx = nd.X, vy = nd.Y;
            if (seg < 0 || !(centr > 0.3f)) return false;
            float nx0 = (px - vx) / centr, ny0 = (py - vy) / centr;
            float t0 = (px - m.Ax[seg]) * m.Dx[seg] * m.InvL2[seg] + (py - m.Ay[seg]) * m.Dy[seg] * m.InvL2[seg];
            if (t0 < 0f) t0 = 0f; else if (t0 > 1f) t0 = 1f;
            float tx0, ty0;
            Tangent(m, seg, t0, out tx0, out ty0);
            uint seed = Hash((uint)p.Seed ^ 0x51ED270Bu, (uint)ni);
            int sg0 = Rnd(ref seed) < 0.5f ? 1 : -1;
            float bestF = 120f * 120f;
            foreach (var o in cx.Obst)
            {
                float dx = o.X - px, dy = o.Y - py, d2 = dx * dx + dy * dy;
                if (d2 >= bestF) continue;
                bestF = d2;
                float dot = dx * tx0 + dy * ty0;
                if (Math.Abs(dot) > 1f) sg0 = dot > 0f ? 1 : -1;
            }
            float lat = centr - half * 0.5f;
            float kr = 0.9f + 0.2f * Rnd(ref seed);
            for (int at = 0; at < 4; at++)
            {
                int sg = (at & 1) == 0 ? sg0 : -sg0;
                float a = lat * p.MergeK * kr * (at < 2 ? 1f : 0.7f);
                int kj = seg;
                float tj = t0;
                if (Walk(m, ref kj, ref tj, sg, a) < 0.6f * a) continue;
                float jx = m.Ax[kj] + m.Dx[kj] * tj, jy = m.Ay[kj] + m.Dy[kj] * tj;
                float tjx, tjy;
                Tangent(m, kj, tj, out tjx, out tjy);
                tjx *= sg; tjy *= sg;
                float hj = m.Half[kj];
                float njx = -tjy, njy = tjx;
                if (njx * (vx - jx) + njy * (vy - jy) < 0f) { njx = -njx; njy = -njy; }
                if (njx * (vx - jx) + njy * (vy - jy) < hj + 0.3f) continue;     // zakret traktu - wioska nie stoi obok farby przy wlocie
                float ex = jx + njx * hj * 0.5f, ey = jy + njy * hj * 0.5f;
                float D = Dist(vx, vy, ex, ey);
                if (D < 0.5f) continue;
                float s0x = nx0 + 0.35f * sg * tx0, s0y = ny0 + 0.35f * sg * ty0, s0 = (float)Math.Sqrt(s0x * s0x + s0y * s0y);
                float s1x = tjx - 0.45f * njx, s1y = tjy - 0.45f * njy, s1 = (float)Math.Sqrt(s1x * s1x + s1y * s1y);
                if (s0 < 1e-4f || s1 < 1e-4f) continue;
                s0x /= s0; s0y /= s0; s1x /= s1; s1y /= s1;
                float b1x = vx + s0x * D * 0.38f, b1y = vy + s0y * D * 0.38f, b2x = ex - s1x * D * 0.42f, b2y = ey - s1y * D * 0.42f;
                var bx = new List<float>(49); var by = new List<float>(49);
                for (int i = 0; i <= 48; i++)
                {
                    float u = i / 48f, w = 1f - u;
                    bx.Add(w * w * w * vx + 3f * w * w * u * b1x + 3f * w * u * u * b2x + u * u * u * ex);
                    by.Add(w * w * w * vy + 3f * w * w * u * b1y + 3f * w * u * u * b2y + u * u * u * ey);
                }
                float[] sx, sy;
                Resample(bx, by, 0.25f, out sx, out sy);
                if (!Clear(cx, sx, sy, vx, vy, ni) || EarlyPaint(cx, sx, sy)) continue;
                float[] mx, my;
                if (p.Meander > 0f && Meander(sx, sy, p.Meander, seed, out mx, out my) && Clear(cx, mx, my, vx, vy, ni) && !EarlyPaint(cx, mx, my)) { rx = mx; ry = my; how = 0; }
                else { rx = sx; ry = sy; how = 1; }
                return true;
            }
            return false;
        }

        /// <summary>Punkt laczki w farbie traktu przed ostatnimi 30% jej dlugosci - krzywa przecina trakt zamiast w niego wpasc.</summary>
        private static bool EarlyPaint(Ctx cx, float[] xs, float[] ys)
        {
            int n = xs.Length;
            float total = 0f;
            for (int i = 1; i < n; i++) total += Dist(xs[i - 1], ys[i - 1], xs[i], ys[i]);
            float s = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) s += Dist(xs[i - 1], ys[i - 1], xs[i], ys[i]);
                if (s >= 0.7f * total) break;
                float centr, half, qx, qy;
                if (cx.M.Nearest(xs[i], ys[i], 3f, out centr, out half, out qx, out qy) && centr < half - 0.05f) return true;
            }
            return false;
        }
    }
}
