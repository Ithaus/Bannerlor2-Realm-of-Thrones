// f11-drogi (Jeff 18.10: "drogi do wiosek odchodzace od glownego traktu; jesli wioska jest blisko traktu - bez drogi, bo to bedzie wygladalo jak krotka kreska,
// tylko dalsze wioski") - PLAN BOCZNYCH DROG (czysta geometria, bez typow gry: da sie sprawdzic poza gra na tych samych plikach).
//
// Wejscie: siatka ladu przejezdnego 2 x 2 jedn. (ModuleData\arm_map_side_roads_grid.tsv, tools/drogi/drogi_boczne_siatka.py z geo-siatka.tsv sondy),
// drogi malowane ROT (ModuleData\arm_map_roads_geo.tsv - ten sam plik co premia drog MapRoads), wioski ukladu tej sesji (VillageBook) i wsie gry,
// miasta i zamki (przeszkody). Wyjscie: lamane bocznych drog (od wioski do traktu albo do innej bocznej drogi) i stemple decali wzdluz nich.
//
// Zasady (dlaczego tak - proba 14.10 "drogi wygladaja fatalnie": cienkie jasne kreski z decali sciezek osad, lamane zygzaki z rastra, droga do kazdej
// wioski - takze krotkie ostrogi przy trakcie):
//  - BLISKO TRAKTU = BEZ DROGI: wioska, ktorej odleglosc od krawedzi farby traktu <= 1.5 x pol-szerokosc traktu + NearExtra (3.5 jedn.), nie dostaje drogi;
//    droga krotsza niz MinLen (4 jedn.) tez nie (ostroga);
//  - SIEC ROSNIE OD TRAKTOW: dalsze wioski po kolei od najblizszej traktowi; kazda prowadzi droge do NAJBLIZSZEGO (koszt trasy) punktu sieci - trakt albo
//    juz polozona boczna droga - wiec wioski za wioska dochodza do jej drogi (jeden korytarz zamiast rownoleglych kresek); najwyzej MaxHop (14 jedn.) prosto
//    do sieci i najwyzej MaxNet (40 jedn.) droga od traktu - kraina bez traktu w poblizu nie dostaje sieci (to nie sa "boczne drogi"); drugi przebieg dla
//    wiosek pominietych w pierwszym (droga dalszej wioski mogla przejsc obok);
//  - TRASA po ladzie: Dijkstra na kracie co RouteStep (1 jedn.) w oknie wokol wioski; koszt: rownina / step 1, pustynia 1.15, las 1.4, brod 2, stok x1 / 1.35 /
//    2.2 / 4, morze / gory ROT / woda - nieprzejezdne, miasto i zamek w promieniu ObstR - nieprzejezdne, srodek innej wioski x2.5 (droga nie idzie przez domy);
//  - KRZYWE, NIE ZYGZAKI: wezly trasy -> skroty prosto tam, gdzie prosta nie jest drozsza (bez wody i przeszkod) -> splajn Catmull-Rom (dosrodkowy) co 0.25 jedn.
//    -> lagodne wicie (amplituda do Meander, fala 5-9 jedn., zero na koncach); punkt na nieprzejezdnym - wicie zdjete, dalej wygladzenie samych wezlow
//    (Chaikin) - nigdy schodki kraty;
//  - STEMPLE: co Spacing (0.5 jedn.) wzdluz drogi, pol-szerokosc Width / 2, pol-dlugosc StampHalfLen (zachodza na siebie - pas bez przerw, miekkie brzegi
//    materialu), drobny rozrzut (obrot, szerokosc, dlugosc, przesuniecie w bok) z ziarna drogi - bez powtarzalnego wzoru; przy wiosce wezej i bladziej
//    (wyjscie spod domow), przy trakcie rozszerzenie (wlot w farbe traktu).
// Wszystko z ziarna (Seed + numer wioski) - ten sam uklad daje te same drogi przy kazdym wczytaniu. Nic nie idzie do zapisu gry.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Armoury
{
    internal static class SideRoadPlan
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ parametry (plik-przelacznik autotestu moze je zmienic - MapSideRoads.FlagParams)
        internal sealed class Params
        {
            public float NearHalfFactor = 1.5f;   // blisko traktu: odleglosc od krawedzi farby <= NearHalfFactor x pol-szerokosc + NearExtra
            public float NearExtra = 3.5f;
            public float MaxHop = 14f;            // najwyzej tyle prosto od wioski do sieci
            public float MaxNet = 40f;            // najwyzej tyle droga od traktu (boczna przez boczna)
            public float MinLen = 4f;             // krotsza droga = ostroga, bez rysowania (wioska i tak liczy sie jako przy sieci)
            public float RouteStep = 1f;          // krata trasy (jedn.)
            public float ObstR = 2.2f;            // promien miasta / zamku nieprzejezdny dla bocznej drogi
            public float VillageAvoidR = 0.8f;    // srodek innej wioski - drozej (droga nie przez domy)
            public float Width = 0.5f;            // szerokosc pasa drogi (wygladzone 18.10, decyzja Jeffa "tak wygladz" - wariant A ze zdjec)
            public float StampHalfLen = 0.9f;     // pol-dlugosc stempla wzdluz drogi (bylo 0.6 - z bliska lancuch prostokatow)
            public float Spacing = 0.15f;         // odstep stempli (gesto - kazdy punkt drogi pod ok. 12 stemplami, ciagly pas; bylo 0.5)
            public float Meander = 0.3f;          // amplituda wicia (jedn.)
            public int MaxRoads = 1500;           // bezpieczniki
            public int MaxStamps = 80000;         // (v7: ok. 69.5k stempli, stawianie 75 ms raz na mape) wiecej stempli niz tyle - rzadziej i dluzsze (ta sama zakladka), nigdy urwana droga
            public int Seed = 1811;

            internal void Clamp()
            {
                NearHalfFactor = Cl(NearHalfFactor, 0f, 5f); NearExtra = Cl(NearExtra, 0f, 20f);
                MaxHop = Cl(MaxHop, 2f, 40f); MaxNet = Cl(MaxNet, MaxHop, 120f); MinLen = Cl(MinLen, 0f, 10f);
                RouteStep = Cl(RouteStep, 0.5f, 2f); ObstR = Cl(ObstR, 0f, 6f); VillageAvoidR = Cl(VillageAvoidR, 0f, 3f);
                Width = Cl(Width, 0.15f, 2f); StampHalfLen = Cl(StampHalfLen, 0.15f, 2f); Spacing = Cl(Spacing, 0.1f, 2f); Meander = Cl(Meander, 0f, 1f);
                if (MaxRoads < 0) MaxRoads = 0; if (MaxRoads > 5000) MaxRoads = 5000;
                if (MaxStamps < 0) MaxStamps = 0; if (MaxStamps > 100000) MaxStamps = 100000;
            }

            internal string Text()
            {
                return "blisko traktu <= " + F2(NearHalfFactor) + " x pol-szer. + " + F2(NearExtra) + ", skok <= " + F2(MaxHop) + ", od traktu <= " + F2(MaxNet)
                       + ", krotsza niz " + F2(MinLen) + " bez rysowania, krata " + F2(RouteStep) + ", miasta/zamki " + F2(ObstR) + ", szerokosc " + F2(Width)
                       + ", stempel " + F2(StampHalfLen) + " co " + F2(Spacing) + ", wicie " + F2(Meander);
            }
        }

        internal static float Cl(float v, float lo, float hi) { return float.IsNaN(v) ? lo : (v < lo ? lo : (v > hi ? hi : v)); }
        internal static string F2(float v) { return v.ToString("0.00", Inv); }

        // ------------------------------------------------------------------ siatka ladu (plik arm_map_side_roads_grid.tsv)
        internal sealed class LandGrid
        {
            public int Version, Nx, Ny, Land, BadRows;
            public float Cell = 2f, X0, Y0;
            public long TerrainSize;
            public string Md5 = "-";
            public byte[] Code;            // 0 = bez drogi ladem; 1 + klasa * 4 + stok

            /// <summary>Kod najblizszej probki (probki w punktach X0 + Cell x i, Y0 + Cell x j); poza siatka 0.</summary>
            internal byte At(float x, float y)
            {
                float fx = (x - X0) / Cell + 0.5f, fy = (y - Y0) / Cell + 0.5f;
                if (!(fx >= 0f) || !(fy >= 0f)) return 0;
                int i = (int)fx, j = (int)fy;
                if (i >= Nx || j >= Ny) return 0;
                return Code[j * Nx + i];
            }
        }

        internal static LandGrid ParseGrid(string text)
        {
            var g = new LandGrid();
            var rows = new List<string>(800);
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                if (raw.Length == 0) continue;
                if (raw[0] == '#')
                {
                    string h = raw.TrimStart('#').Trim();
                    string[] p = h.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 2) continue;
                    if (p[0] == "version:") int.TryParse(p[1], NumberStyles.Integer, Inv, out g.Version);
                    else if (p[0] == "terrain_md5:") g.Md5 = p[1];
                    else if (p[0] == "terrain_size:") long.TryParse(p[1], NumberStyles.Integer, Inv, out g.TerrainSize);
                    else if (p[0] == "cell:") float.TryParse(p[1], NumberStyles.Float, Inv, out g.Cell);
                    else if (p[0] == "origin:" && p.Length >= 3) { float.TryParse(p[1], NumberStyles.Float, Inv, out g.X0); float.TryParse(p[2], NumberStyles.Float, Inv, out g.Y0); }
                    else if (p[0] == "size:" && p.Length >= 3) { int.TryParse(p[1], NumberStyles.Integer, Inv, out g.Nx); int.TryParse(p[2], NumberStyles.Integer, Inv, out g.Ny); }
                    continue;
                }
                rows.Add(raw);
            }
            if (g.Version < 1 || g.Nx <= 0 || g.Ny <= 0 || !(g.Cell > 0.1f) || g.Nx * (long)g.Ny > 20000000L) throw new FormatException("naglowek siatki bez wersji / wymiarow");
            g.Code = new byte[g.Nx * g.Ny];
            for (int j = 0; j < g.Ny; j++)
            {
                if (j >= rows.Count) { g.BadRows++; continue; }
                string r = rows[j];
                if (r.Length != g.Nx) g.BadRows++;
                int n = Math.Min(r.Length, g.Nx);
                for (int i = 0; i < n; i++)
                {
                    int c = r[i] - 'A';
                    if (c >= 0 && c < 20) { g.Code[j * g.Nx + i] = (byte)(c + 1); g.Land++; }
                }
            }
            if (rows.Count != g.Ny) g.BadRows += Math.Abs(rows.Count - g.Ny);
            return g;
        }

        // koszt komorki: klasa (rownina, step, pustynia, las, brod) x stok (0..3); 0 = nieprzejezdne
        private static readonly float[] ClassCost = { 1f, 1f, 1.15f, 1.4f, 2f };
        private static readonly float[] SlopeCost = { 1f, 1.35f, 2.2f, 4f };

        internal static float CodeCost(byte code)
        {
            if (code == 0) return 0f;
            int c = code - 1;
            return ClassCost[Math.Min(4, c >> 2)] * SlopeCost[c & 3];
        }

        // ------------------------------------------------------------------ trakty (drogi malowane ROT, arm_map_roads_geo.tsv)
        internal sealed class MainRoads
        {
            public float[] Ax, Ay, Dx, Dy, InvL2, Half;
            public int Segments, Lines, Points, BadRows, Version;
            public long TerrainSize;
            public string Md5 = "-";
            public double Length;
            // kubelki 4 x 4 jedn. (CSR)
            public float X0, Y0;
            public int Nx, Ny;
            public int[] Start, Seg;
            public const float Bucket = 4f;

            /// <summary>Najblizszy odcinek w promieniu maxR: odleglosc od osi (centr), pol-szerokosc i punkt na osi; false - brak w zasiegu.</summary>
            internal bool Nearest(float x, float y, float maxR, out float centr, out float half, out float px, out float py)
            {
                centr = float.MaxValue; half = 0f; px = py = 0f;
                if (Segments == 0) return false;
                int r = (int)Math.Ceiling(maxR / Bucket);
                int cx = (int)Math.Floor((x - X0) / Bucket), cy = (int)Math.Floor((y - Y0) / Bucket);
                float best = maxR * maxR;
                bool any = false;
                for (int j = Math.Max(0, cy - r); j <= Math.Min(Ny - 1, cy + r); j++)
                    for (int i = Math.Max(0, cx - r); i <= Math.Min(Nx - 1, cx + r); i++)
                    {
                        int b = j * Nx + i;
                        for (int k = Start[b]; k < Start[b + 1]; k++)
                        {
                            int s = Seg[k];
                            float qx = x - Ax[s], qy = y - Ay[s];
                            float t = (qx * Dx[s] + qy * Dy[s]) * InvL2[s];
                            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                            float ex = qx - t * Dx[s], ey = qy - t * Dy[s];
                            float d2 = ex * ex + ey * ey;
                            if (d2 < best) { best = d2; any = true; half = Half[s]; px = Ax[s] + t * Dx[s]; py = Ay[s] + t * Dy[s]; }
                        }
                    }
                if (any) centr = (float)Math.Sqrt(best);
                return any;
            }
        }

        internal static MainRoads ParseMainRoads(string text)
        {
            var m = new MainRoads();
            var ids = new List<int>(11000); var xs = new List<float>(11000); var ys = new List<float>(11000); var ws = new List<float>(11000);
            var cols = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                if (raw.Length == 0) continue;
                if (raw[0] == '#')
                {
                    string h = raw.TrimStart('#').Trim();
                    if (h.StartsWith("version:", StringComparison.Ordinal)) int.TryParse(h.Substring(8).Trim(), NumberStyles.Integer, Inv, out m.Version);
                    else if (h.StartsWith("terrain_size:", StringComparison.Ordinal)) long.TryParse(h.Substring(13).Trim(), NumberStyles.Integer, Inv, out m.TerrainSize);
                    else if (h.StartsWith("terrain_md5:", StringComparison.Ordinal)) m.Md5 = h.Substring(12).Trim();
                    else if (h.StartsWith("road\t", StringComparison.Ordinal)) { string[] hc = h.Split('\t'); for (int i = 0; i < hc.Length; i++) cols[hc[i].Trim()] = i; }
                    continue;
                }
                try
                {
                    string[] c = raw.Split('\t');
                    int id = int.Parse(c[Col(cols, "road", 0)], Inv);
                    float x = float.Parse(c[Col(cols, "x", 2)], Inv), y = float.Parse(c[Col(cols, "y", 3)], Inv), w = float.Parse(c[Col(cols, "width", 4)], Inv);
                    if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) { m.BadRows++; continue; }
                    ids.Add(id); xs.Add(x); ys.Add(y); ws.Add(float.IsNaN(w) ? 0f : w);
                }
                catch { m.BadRows++; }
            }
            var sa = new List<int>(xs.Count);
            for (int i = 0; i < xs.Count; i++) { if (i == 0 || ids[i] != ids[i - 1]) { m.Lines++; continue; } sa.Add(i); }
            int n = sa.Count;
            m.Points = xs.Count; m.Segments = n;
            m.Ax = new float[n]; m.Ay = new float[n]; m.Dx = new float[n]; m.Dy = new float[n]; m.InvL2 = new float[n]; m.Half = new float[n];
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int k = 0; k < n; k++)
            {
                int i = sa[k];
                float ax = xs[i - 1], ay = ys[i - 1], dx = xs[i] - ax, dy = ys[i] - ay, l2 = dx * dx + dy * dy;
                float half = Cl(ws[i] * 0.5f, 0.15f, 1.5f);   // jak MapRoads (pol-szerokosc farby 0.15..1.5)
                m.Ax[k] = ax; m.Ay[k] = ay; m.Dx[k] = dx; m.Dy[k] = dy; m.InvL2[k] = l2 > 1e-8f ? 1f / l2 : 0f; m.Half[k] = half;
                m.Length += Math.Sqrt(l2);
                minX = Math.Min(minX, Math.Min(ax, ax + dx)); maxX = Math.Max(maxX, Math.Max(ax, ax + dx));
                minY = Math.Min(minY, Math.Min(ay, ay + dy)); maxY = Math.Max(maxY, Math.Max(ay, ay + dy));
            }
            if (n == 0) { minX = minY = 0f; maxX = maxY = 1f; }
            m.X0 = minX - MainRoads.Bucket; m.Y0 = minY - MainRoads.Bucket;
            m.Nx = Math.Max(1, (int)Math.Ceiling((maxX + MainRoads.Bucket - m.X0) / MainRoads.Bucket) + 1);
            m.Ny = Math.Max(1, (int)Math.Ceiling((maxY + MainRoads.Bucket - m.Y0) / MainRoads.Bucket) + 1);
            int cells = m.Nx * m.Ny;
            var cnt = new int[cells];
            m.Start = new int[cells + 1];
            for (int pass = 0; pass < 2; pass++)
            {
                int[] fill = pass == 1 ? new int[cells] : null;
                if (pass == 1)
                {
                    for (int c = 0; c < cells; c++) m.Start[c + 1] = m.Start[c] + cnt[c];
                    m.Seg = new int[m.Start[cells]];
                }
                for (int k = 0; k < n; k++)
                {
                    int i0 = (int)Math.Floor((Math.Min(m.Ax[k], m.Ax[k] + m.Dx[k]) - m.X0) / MainRoads.Bucket), i1 = (int)Math.Floor((Math.Max(m.Ax[k], m.Ax[k] + m.Dx[k]) - m.X0) / MainRoads.Bucket);
                    int j0 = (int)Math.Floor((Math.Min(m.Ay[k], m.Ay[k] + m.Dy[k]) - m.Y0) / MainRoads.Bucket), j1 = (int)Math.Floor((Math.Max(m.Ay[k], m.Ay[k] + m.Dy[k]) - m.Y0) / MainRoads.Bucket);
                    for (int j = Math.Max(0, j0); j <= Math.Min(m.Ny - 1, j1); j++)
                        for (int i = Math.Max(0, i0); i <= Math.Min(m.Nx - 1, i1); i++)
                        {
                            int c = j * m.Nx + i;
                            if (pass == 0) cnt[c]++; else m.Seg[m.Start[c] + fill[c]++] = k;
                        }
                }
            }
            return m;
        }

        private static int Col(Dictionary<string, int> cols, string name, int def) { int i; return cols.TryGetValue(name, out i) ? i : def; }

        // ------------------------------------------------------------------ wejscie: wioski i przeszkody (z gry - MapSideRoads.Snapshot)
        internal struct Node
        {
            public float X, Y;
            public string Id;        // uid wioski albo StringId wsi gry (log)
            public bool Game;        // wies gry (nie wioska z ukladu)
        }

        internal struct Obstacle { public float X, Y; }

        // ------------------------------------------------------------------ wynik
        internal sealed class Road
        {
            public int Node;                 // indeks wioski
            public float[] X, Y;             // os drogi co ok. 0.25 jedn. od wioski (0) do wlotu (ostatni)
            public float Length;
            public int Join;                 // 0 trakt, 1 inna boczna droga
            public int Parent = -1;          // droga, do ktorej dochodzi (Join 1)
            public float NetAtJoin;          // odleglosc wlotu od traktu po sieci
            public int Pass;                 // 1 albo 2 (drugi przebieg)
            public byte Smooth;              // 0 splajn z wiciem, 1 splajn, 2 Chaikin wezlow, 3 Chaikin bez sprawdzianu
        }

        internal struct Stamp
        {
            public float X, Y, Yaw, Across, Along, Alpha;
            public int Road;
        }

        internal sealed class Result
        {
            public readonly List<Road> Roads = new List<Road>();
            public readonly List<Stamp> Stamps = new List<Stamp>();
            public int Nodes, GameNodes, Near, Far, NoMainInReach, TooFar, NoPath, OverNet, Stubs, CapRoads, CapStamps, Pass2, JoinMain, JoinSide;
            public int[] SmoothKinds = new int[4];
            public long Expanded;
            public double Length, Ms, MaxRoadMs;
            public float StampScale = 1f;        // odstep i pol-dlugosc stempli x tyle, gdy siec przekracza MaxStamps
            public float MaxRoadLen;

            internal string Text()
            {
                var sb = new StringBuilder();
                sb.Append("wiosek ").Append(Nodes).Append(" (w tym wsi gry ").Append(GameNodes).Append("): blisko traktu bez drogi ").Append(Near)
                  .Append(", dalszych ").Append(Far).Append(" -> drog ").Append(Roads.Count).Append(" (do traktu ").Append(JoinMain).Append(", do innej bocznej ").Append(JoinSide)
                  .Append(", z drugiego przebiegu ").Append(Pass2).Append("), bez drogi: ostroga krotsza niz min. ").Append(Stubs).Append(", trakt dalej niz skok ").Append(TooFar)
                  .Append(", bez traktu w zasiegu sieci ").Append(NoMainInReach).Append(", bez trasy po ladzie ").Append(NoPath).Append(", za daleko po sieci ").Append(OverNet)
                  .Append(", ponad limit drog ").Append(CapRoads)
                  .Append(" | dlugosc ").Append(Length.ToString("0.0", Inv)).Append(" jedn. (najdluzsza ").Append(MaxRoadLen.ToString("0.0", Inv)).Append(")")
                  .Append(", wygladzenie: splajn z wiciem ").Append(SmoothKinds[0]).Append(", splajn ").Append(SmoothKinds[1]).Append(", Chaikin ").Append(SmoothKinds[2]).Append(", Chaikin bez sprawdzianu ").Append(SmoothKinds[3])
                  .Append(" | stempli ").Append(Stamps.Count).Append(" (odstep x").Append(StampScale.ToString("0.00", Inv)).Append(", ponad limit ").Append(CapStamps).Append(")")
                  .Append(" | wezlow trasy ").Append(Expanded).Append(", czas ").Append(Ms.ToString("0", Inv)).Append(" ms (najdluzsza droga ").Append(MaxRoadMs.ToString("0.0", Inv)).Append(" ms)");
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------------ siec bocznych drog (punkty osi w kubelkach 1 x 1 jedn.)
        private sealed class Net
        {
            public readonly List<float> X = new List<float>(), Y = new List<float>(), D = new List<float>();   // punkt, odleglosc od traktu po sieci
            public readonly List<int> R = new List<int>();                                                      // droga punktu
            private readonly Dictionary<long, List<int>> _b = new Dictionary<long, List<int>>();

            private static long Key(int i, int j) { return ((long)i << 32) ^ (uint)j; }

            internal void Add(float x, float y, float d, int road)
            {
                int id = X.Count;
                X.Add(x); Y.Add(y); D.Add(d); R.Add(road);
                long k = Key((int)Math.Floor(x), (int)Math.Floor(y));
                List<int> l;
                if (!_b.TryGetValue(k, out l)) { l = new List<int>(4); _b[k] = l; }
                l.Add(id);
            }

            /// <summary>Najblizszy punkt sieci w promieniu r (r &lt;= 2); -1 brak.</summary>
            internal int Nearest(float x, float y, float r, out float dist)
            {
                dist = float.MaxValue;
                int best = -1;
                float r2 = r * r;
                int ci = (int)Math.Floor(x), cj = (int)Math.Floor(y), rr = (int)Math.Ceiling(r);
                for (int j = cj - rr; j <= cj + rr; j++)
                    for (int i = ci - rr; i <= ci + rr; i++)
                    {
                        List<int> l;
                        if (!_b.TryGetValue(Key(i, j), out l)) continue;
                        foreach (int id in l)
                        {
                            float dx = X[id] - x, dy = Y[id] - y, d2 = dx * dx + dy * dy;
                            if (d2 <= r2) { r2 = d2; best = id; }
                        }
                    }
                if (best >= 0) dist = (float)Math.Sqrt(r2);
                return best;
            }

            /// <summary>Odleglosc prosto do najblizszego punktu sieci w promieniu maxR (szukanie po kubelkach rosnacym kwadratem); float.MaxValue brak.</summary>
            internal float NearestFar(float x, float y, float maxR, out int id)
            {
                id = -1;
                float best = maxR * maxR;
                int ci = (int)Math.Floor(x), cj = (int)Math.Floor(y), rr = (int)Math.Ceiling(maxR);
                for (int ring = 0; ring <= rr; ring++)
                {
                    for (int j = cj - ring; j <= cj + ring; j++)
                        for (int i = ci - ring; i <= ci + ring; i++)
                        {
                            if (Math.Max(Math.Abs(i - ci), Math.Abs(j - cj)) != ring) continue;
                            List<int> l;
                            if (!_b.TryGetValue(Key(i, j), out l)) continue;
                            foreach (int p in l)
                            {
                                float dx = X[p] - x, dy = Y[p] - y, d2 = dx * dx + dy * dy;
                                if (d2 < best) { best = d2; id = p; }
                            }
                        }
                    if (id >= 0 && (ring - 1) * (ring - 1) > best) break;
                }
                return id >= 0 ? (float)Math.Sqrt(best) : float.MaxValue;
            }
        }

        // ------------------------------------------------------------------ plan
        private sealed class Ctx
        {
            public LandGrid G; public MainRoads M; public Params P; public Net N = new Net();
            public List<Node> Nodes; public List<Obstacle> Obst;
            public Dictionary<long, List<int>> ObstB = new Dictionary<long, List<int>>();   // przeszkody i wioski w kubelkach 4 x 4
            public Dictionary<long, List<int>> NodeB = new Dictionary<long, List<int>>();
            // okno Dijkstry (wspolne bufory, numer przebiegu zamiast czyszczenia)
            public int W, Side;
            public float[] Dist, Len, CostC;
            public int[] Prev, Seen, HeapPos;
            public int Gen;
            public int[] Heap; public int HeapN;
            public long Expanded;
        }

        private static long BKey(float x, float y) { return ((long)(int)Math.Floor(x / 4f) << 32) ^ (uint)(int)Math.Floor(y / 4f); }

        private static void BAdd(Dictionary<long, List<int>> b, float x, float y, int id)
        {
            long k = BKey(x, y);
            List<int> l;
            if (!b.TryGetValue(k, out l)) { l = new List<int>(4); b[k] = l; }
            l.Add(id);
        }

        /// <summary>Plan calej sieci (watek roboczy albo poza gra): czysta funkcja wejsc i parametrow.</summary>
        internal static Result Build(LandGrid g, MainRoads m, List<Node> nodes, List<Obstacle> obst, Params p)
        {
            var sw = Stopwatch.StartNew();
            var res = new Result();
            p.Clamp();
            var cx = new Ctx { G = g, M = m, P = p, Nodes = nodes, Obst = obst };
            for (int i = 0; i < obst.Count; i++) BAdd(cx.ObstB, obst[i].X, obst[i].Y, i);
            for (int i = 0; i < nodes.Count; i++) BAdd(cx.NodeB, nodes[i].X, nodes[i].Y, i);
            cx.W = (int)Math.Ceiling((p.MaxHop * 1.35f + 2f) / p.RouteStep);
            cx.Side = 2 * cx.W + 1;
            int nn = cx.Side * cx.Side;
            cx.Dist = new float[nn]; cx.Len = new float[nn]; cx.CostC = new float[nn]; cx.Prev = new int[nn]; cx.Seen = new int[nn]; cx.HeapPos = new int[nn]; cx.Heap = new int[nn];

            res.Nodes = nodes.Count;
            // odleglosc kazdej wioski od krawedzi farby traktu; blisko = bez drogi
            var far = new List<KeyValuePair<float, int>>();
            float reach = p.MaxNet + 4f;
            for (int i = 0; i < nodes.Count; i++)
            {
                var nd = nodes[i];
                if (nd.Game) res.GameNodes++;
                float centr, half, px, py;
                bool any = m.Nearest(nd.X, nd.Y, reach, out centr, out half, out px, out py);
                float edge = any ? centr - half : float.MaxValue;
                if (any && edge <= p.NearHalfFactor * half + p.NearExtra) { res.Near++; continue; }
                res.Far++;
                if (!any) { res.NoMainInReach++; continue; }
                far.Add(new KeyValuePair<float, int>(edge, i));
            }
            far.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
            var skipped = new List<KeyValuePair<float, int>>();
            for (int pass = 1; pass <= 2; pass++)
            {
                var list = pass == 1 ? far : skipped;
                var again = new List<KeyValuePair<float, int>>();
                foreach (var kv in list)
                {
                    if (res.Roads.Count >= p.MaxRoads) { res.CapRoads++; continue; }
                    int why = Route(cx, kv.Value, kv.Key, pass, res);
                    if (why == 0) { if (pass == 2) res.Pass2++; continue; }
                    if (pass == 1 && why != 5) { again.Add(kv); continue; }   // ostroga (5) - ostateczne
                    switch (why)
                    {
                        case 1: res.TooFar++; break;
                        case 2: res.NoPath++; break;
                        case 3: res.OverNet++; break;
                        case 5: res.Stubs++; break;
                    }
                }
                if (pass == 1) skipped = again;
            }
            // stemple: siec dluzsza niz MaxStamps x odstep - odstep i pol-dlugosc razem w gore (ta sama zakladka, kazda droga cala)
            foreach (var rd in res.Roads) { res.Length += rd.Length; if (rd.Length > res.MaxRoadLen) res.MaxRoadLen = rd.Length; }
            double need = res.Length / p.Spacing, room = p.MaxStamps - 1.2 * res.Roads.Count;   // + ok. 1 stempel na droge (poczatek przy wiosce)
            if (p.MaxStamps > 0 && need > room) res.StampScale = room > 0.25 * p.MaxStamps ? (float)(need / room * 1.03) : (float)(need / (0.25 * p.MaxStamps));
            for (int r = 0; r < res.Roads.Count; r++) Stamps(res.Roads[r], r, p, res);
            res.Expanded = cx.Expanded;
            res.Ms = sw.Elapsed.TotalMilliseconds;
            return res;
        }

        /// <summary>Jedna wioska: trasa do najblizszego punktu sieci. 0 = droga dodana; 1 = siec dalej niz MaxHop prosto; 2 = brak trasy po ladzie w oknie;
        /// 3 = kazdy wlot za daleko po sieci (MaxNet); 5 = ostroga (krotsza niz MinLen - bez rysowania, wioska liczy sie jako przy sieci).</summary>
        private static int Route(Ctx cx, int ni, float edgeMain, int pass, Result res)
        {
            var p = cx.P;
            var nd = cx.Nodes[ni];
            long t0 = Stopwatch.GetTimestamp();
            // szybki test: trakt albo boczna siec w zasiegu skoku (prosto)
            int sid;
            float dSide = cx.N.NearestFar(nd.X, nd.Y, p.MaxHop, out sid);
            if (edgeMain > p.MaxHop && dSide > p.MaxHop) return 1;

            float step = p.RouteStep;
            int W = cx.W, S = cx.Side;
            float ox = nd.X - W * step, oy = nd.Y - W * step;
            float rad2 = (p.MaxHop * 1.35f) * (p.MaxHop * 1.35f);
            int gen = ++cx.Gen;
            cx.HeapN = 0;
            int start = W * S + W;
            Touch(cx, start, gen, ox, oy, step, nd.X, nd.Y, ni);
            cx.Dist[start] = 0f; cx.Len[start] = 0f; cx.Prev[start] = -1;
            HeapPush(cx, start);
            int goal = -1, goalKind = -1, goalNet = -1;
            float goalX = 0f, goalY = 0f, goalNetD = 0f;
            float joinSide = Math.Max(0.75f, step * 0.8f);
            bool sawOver = false;
            while (cx.HeapN > 0)
            {
                int u = HeapPop(cx);
                cx.Expanded++;
                int ui = u % S, uj = u / S;
                float ux = ox + ui * step, uy = oy + uj * step;
                // cel: trakt (pas farby + pol kraty) albo punkt bocznej sieci (z limitem MaxNet po sieci; punkty ostrog nie sa celem - droga dalszej
                // wioski idzie wtedy dalej, do prawdziwej sieci, a nie urywa sie przy wiosce stojacej tuz przy trakcie)
                {
                    float centr, half, px, py;
                    if (cx.M.Nearest(ux, uy, 2.5f, out centr, out half, out px, out py) && centr <= half + step * 0.75f)
                    {
                        if (cx.Len[u] <= p.MaxNet) { goal = u; goalKind = 0; goalX = px; goalY = py; goalNetD = 0f; break; }
                        sawOver = true;
                    }
                    float dn;
                    int q = cx.N.Nearest(ux, uy, joinSide, out dn);
                    if (q >= 0 && cx.N.R[q] >= 0)
                    {
                        float tot = cx.N.D[q] + cx.Len[u];
                        if (tot <= p.MaxNet) { goal = u; goalKind = 1; goalNet = q; goalX = cx.N.X[q]; goalY = cx.N.Y[q]; goalNetD = cx.N.D[q]; break; }
                        sawOver = true;
                    }
                }
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int vi = ui + di, vj = uj + dj;
                        if (vi < 0 || vj < 0 || vi >= S || vj >= S) continue;
                        float vx = ox + vi * step, vy = oy + vj * step;
                        float ddx = vx - nd.X, ddy = vy - nd.Y;
                        if (ddx * ddx + ddy * ddy > rad2) continue;
                        int v = vj * S + vi;
                        if (cx.Seen[v] != gen) Touch(cx, v, gen, ox, oy, step, nd.X, nd.Y, ni);
                        float cv = cx.CostC[v];
                        if (cv <= 0f) continue;
                        float ln = (di != 0 && dj != 0) ? step * 1.41421356f : step;
                        float nd2 = cx.Dist[u] + ln * 0.5f * (cx.CostC[u] + cv);
                        if (nd2 < cx.Dist[v])
                        {
                            cx.Dist[v] = nd2; cx.Len[v] = cx.Len[u] + ln; cx.Prev[v] = u;
                            if (cx.HeapPos[v] >= 0) HeapUp(cx, cx.HeapPos[v]); else HeapPush(cx, v);
                        }
                    }
            }
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            if (ms > res.MaxRoadMs) res.MaxRoadMs = ms;
            if (goal < 0) return sawOver ? 3 : 2;

            // wezly trasy od wioski do celu
            var path = new List<int>();
            for (int u = goal; u >= 0; u = cx.Prev[u]) path.Add(u);
            path.Reverse();
            var px0 = new List<float>(path.Count + 2); var py0 = new List<float>(path.Count + 2); var pc = new List<float>(path.Count + 2);
            for (int k = 0; k < path.Count; k++)
            {
                int u = path[k];
                px0.Add(ox + (u % S) * step); py0.Add(oy + (u / S) * step); pc.Add(cx.Dist[u]);
            }
            px0[0] = nd.X; py0[0] = nd.Y;
            // wlot: trakt - punkt na osi przesuniety ku wiosce o pol pol-szerokosci (droga wchodzi w farbe); boczna - punkt osi tamtej drogi
            float ex = goalX, ey = goalY;
            if (goalKind == 0)
            {
                float centr, half, qx, qy;
                cx.M.Nearest(px0[px0.Count - 1], py0[py0.Count - 1], 3f, out centr, out half, out qx, out qy);
                float lx = px0[px0.Count - 1] - goalX, ly = py0[py0.Count - 1] - goalY, ll = (float)Math.Sqrt(lx * lx + ly * ly);
                if (ll > 1e-3f) { ex = goalX + lx / ll * half * 0.5f; ey = goalY + ly / ll * half * 0.5f; }
            }
            if (path.Count >= 2)
            {
                // ostatni wezel kraty zastapiony punktem wlotu (bez haczyka na koncu)
                px0[px0.Count - 1] = ex; py0[py0.Count - 1] = ey;
            }
            else { px0.Add(ex); py0.Add(ey); pc.Add(pc[pc.Count - 1]); }

            uint seed = Hash((uint)p.Seed, (uint)ni);
            float[] rx, ry;
            byte how = Shape(cx, px0, py0, pc, ni, seed, out rx, out ry);
            float L = 0f;
            for (int k = 1; k < rx.Length; k++) L += Dist(rx[k - 1], ry[k - 1], rx[k], ry[k]);
            int roadId = cx.Nodes.Count + res.Roads.Count;   // tylko do odroznienia w siec
            if (L < p.MinLen)
            {
                // ostroga: bez rysowania; wioska liczy sie jako przy sieci (punkt w sieci tylko do skrotu odleglosci, nie jako wlot - R < 0)
                cx.N.Add(nd.X, nd.Y, goalNetD + L, -1 - ni);
                return 5;
            }
            var rd = new Road { Node = ni, X = rx, Y = ry, Length = L, Join = goalKind, NetAtJoin = goalNetD, Pass = pass, Smooth = how };
            if (goalKind == 1)
            {
                int pr = cx.N.R[goalNet];
                rd.Parent = pr >= cx.Nodes.Count ? pr - cx.Nodes.Count : -1;
                res.JoinSide++;
            }
            else res.JoinMain++;
            res.SmoothKinds[how]++;
            res.Roads.Add(rd);
            // os drogi do sieci: odleglosc od traktu = wlot + reszta drogi
            float s = 0f;
            float[] cum = new float[rx.Length];
            for (int k = 1; k < rx.Length; k++) { s += Dist(rx[k - 1], ry[k - 1], rx[k], ry[k]); cum[k] = s; }
            for (int k = 0; k < rx.Length; k++) cx.N.Add(rx[k], ry[k], goalNetD + (L - cum[k]), roadId);
            return 0;
        }

        /// <summary>Koszt wezla kraty (raz na wezel w przebiegu): siatka ladu, miasta / zamki (nieprzejezdne), srodki innych wiosek (drozej);
        /// wezly przy wiosce startu (1.5 jedn.) przejezdne zawsze i bez przeszkod - wioska stoi na ladzie, a probka siatki 2 jedn. bywa morzem przy brzegu.</summary>
        private static void Touch(Ctx cx, int v, int gen, float ox, float oy, float step, float sx, float sy, int self)
        {
            cx.Seen[v] = gen;
            cx.Dist[v] = float.MaxValue;
            cx.Len[v] = 0f;
            cx.Prev[v] = -1;
            cx.HeapPos[v] = -1;
            int S = cx.Side;
            float x = ox + (v % S) * step, y = oy + (v / S) * step;
            cx.CostC[v] = CostAt(cx, x, y, sx, sy, self);
        }

        private static float CostAt(Ctx cx, float x, float y, float sx, float sy, int self)
        {
            var p = cx.P;
            float c = CodeCost(cx.G.At(x, y));
            float dsx = x - sx, dsy = y - sy;
            bool nearStart = dsx * dsx + dsy * dsy <= 2.25f;
            if (c <= 0f)
            {
                if (nearStart) c = 2f;
                else
                {
                    // pas farby traktu jest ladem (probka 2 jedn. przy brzegu / rzece bywa '.') - wezel na trakcie przejezdny
                    float centr, half, qx, qy;
                    if (cx.M.Nearest(x, y, 2f, out centr, out half, out qx, out qy) && centr <= half + 0.5f) c = 1.5f;
                    else return 0f;
                }
            }
            if (nearStart) return c;   // wioska przy zamku / miescie tez wyjdzie na droge
            if (p.ObstR > 0f && Near(cx.ObstB, cx.Obst, null, x, y, p.ObstR, -1)) return 0f;
            if (p.VillageAvoidR > 0f && Near(cx.NodeB, null, cx.Nodes, x, y, p.VillageAvoidR, self)) c *= 2.5f;
            return c;
        }

        /// <summary>Punkt (przeszkoda albo wioska - jedna z list) w promieniu r od (x, y), z pominieciem indeksu skip.</summary>
        private static bool Near(Dictionary<long, List<int>> b, List<Obstacle> obst, List<Node> nodes, float x, float y, float r, int skip)
        {
            float r2 = r * r;
            int ci = (int)Math.Floor(x / 4f), cj = (int)Math.Floor(y / 4f), rr = (int)Math.Ceiling(r / 4f);
            for (int j = cj - rr; j <= cj + rr; j++)
                for (int i = ci - rr; i <= ci + rr; i++)
                {
                    List<int> l;
                    if (!b.TryGetValue(((long)i << 32) ^ (uint)j, out l)) continue;
                    foreach (int id in l)
                    {
                        if (id == skip) continue;
                        float px = obst != null ? obst[id].X : nodes[id].X, py = obst != null ? obst[id].Y : nodes[id].Y;
                        float dx = px - x, dy = py - y;
                        if (dx * dx + dy * dy <= r2) return true;
                    }
                }
            return false;
        }

        // kopiec (min po Dist)
        private static void HeapPush(Ctx cx, int v) { int i = cx.HeapN++; cx.Heap[i] = v; cx.HeapPos[v] = i; HeapUp(cx, i); }

        private static void HeapUp(Ctx cx, int i)
        {
            int v = cx.Heap[i];
            float d = cx.Dist[v];
            while (i > 0)
            {
                int pa = (i - 1) >> 1;
                int u = cx.Heap[pa];
                if (cx.Dist[u] <= d) break;
                cx.Heap[i] = u; cx.HeapPos[u] = i; i = pa;
            }
            cx.Heap[i] = v; cx.HeapPos[v] = i;
        }

        private static int HeapPop(Ctx cx)
        {
            int top = cx.Heap[0];
            cx.HeapPos[top] = -2;
            int n = --cx.HeapN;
            if (n > 0)
            {
                int v = cx.Heap[n];
                float d = cx.Dist[v];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1;
                    if (l >= n) break;
                    int r = l + 1;
                    int c = (r < n && cx.Dist[cx.Heap[r]] < cx.Dist[cx.Heap[l]]) ? r : l;
                    if (cx.Dist[cx.Heap[c]] >= d) break;
                    cx.Heap[i] = cx.Heap[c]; cx.HeapPos[cx.Heap[i]] = i; i = c;
                }
                cx.Heap[i] = v; cx.HeapPos[v] = i;
            }
            return top;
        }

        // ------------------------------------------------------------------ ksztalt drogi
        /// <summary>Wezly kraty -> skroty -> splajn -> wicie; sprawdzian przejezdnosci po kazdym kroku (punkt co ok. 0.25 jedn. na ladzie wedlug siatki,
        /// z wyjatkiem 1.5 jedn. przy wiosce i pasa traktu). Zwraca rodzaj wygladzenia (0 splajn z wiciem, 1 splajn, 2 Chaikin wezlow, 3 Chaikin bez sprawdzianu).</summary>
        private static byte Shape(Ctx cx, List<float> xs, List<float> ys, List<float> cost, int ni, uint seed, out float[] rx, out float[] ry)
        {
            var nd = cx.Nodes[ni];
            // skroty: z kotwicy i najdalszy wezel j, do ktorego prosta nie jest drozsza od trasy (koszt probek co 0.4) i nie trafia w nieprzejezdne
            var wx = new List<float> { xs[0] }; var wy = new List<float> { ys[0] };
            int a = 0, n = xs.Count;
            while (a < n - 1)
            {
                int best = a + 1;
                for (int j = n - 1; j > a + 1; j--)
                {
                    if (Straight(cx, xs[a], ys[a], xs[j], ys[j], nd.X, nd.Y, ni, (cost[Math.Min(j, cost.Count - 1)] - cost[a]) * 1.05f + 0.05f)) { best = j; break; }
                }
                wx.Add(xs[best]); wy.Add(ys[best]);
                a = best;
            }
            float[] sx, sy;
            CatmullRom(wx, wy, 0.25f, out sx, out sy);
            if (Clear(cx, sx, sy, nd.X, nd.Y, ni))
            {
                float[] mx, my;
                if (cx.P.Meander > 0f && Meander(sx, sy, cx.P.Meander, seed, out mx, out my) && Clear(cx, mx, my, nd.X, nd.Y, ni))
                {
                    rx = mx; ry = my; return 0;
                }
                rx = sx; ry = sy; return 1;
            }
            // Chaikin samych wezlow kraty (zostaje blisko przejezdnych wezlow, scina rogi najwyzej o cwierc kraty); nie przechodzi sprawdzianu - i tak
            // Chaikin (3), nigdy schodki kraty: punkt nad woda nie dostanie stempla (sprawdzian ladu przy stawianiu), rog gory scina sie o ulamek jednostki
            var cxs = new List<float>(xs); var cys = new List<float>(ys);
            for (int it = 0; it < 3; it++) Chaikin(cxs, cys);
            Resample(cxs, cys, 0.25f, out rx, out ry);
            return Clear(cx, rx, ry, nd.X, nd.Y, ni) ? (byte)2 : (byte)3;
        }

        private static bool Passable(Ctx cx, float x, float y, float sx, float sy, int self)
        {
            return CostAt(cx, x, y, sx, sy, self) > 0f;
        }

        private static bool Straight(Ctx cx, float ax, float ay, float bx, float by, float sx, float sy, int self, float budget)
        {
            float L = Dist(ax, ay, bx, by);
            int k = Math.Max(1, (int)Math.Ceiling(L / 0.4f));
            float sum = 0f, prev = CostAt(cx, ax, ay, sx, sy, self);
            if (prev <= 0f) return false;
            for (int i = 1; i <= k; i++)
            {
                float t = i / (float)k;
                float c = CostAt(cx, ax + (bx - ax) * t, ay + (by - ay) * t, sx, sy, self);
                if (c <= 0f) return false;
                sum += 0.5f * (prev + c) * (L / k);
                prev = c;
                if (sum > budget) return false;
            }
            return true;
        }

        private static bool Clear(Ctx cx, float[] xs, float[] ys, float sx, float sy, int self)
        {
            for (int i = 0; i < xs.Length; i++) if (!Passable(cx, xs[i], ys[i], sx, sy, self)) return false;
            return true;
        }

        /// <summary>Splajn Catmull-Rom dosrodkowy (alfa 0.5) przez punkty, probki co ok. step (koncowe punkty powielone).</summary>
        internal static void CatmullRom(List<float> xs, List<float> ys, float step, out float[] ox, out float[] oy)
        {
            int n = xs.Count;
            var rx = new List<float>(); var ry = new List<float>();
            if (n < 3) { Resample(xs, ys, step, out ox, out oy); return; }
            rx.Add(xs[0]); ry.Add(ys[0]);
            for (int i = 0; i < n - 1; i++)
            {
                float x0 = i > 0 ? xs[i - 1] : 2 * xs[0] - xs[1], y0 = i > 0 ? ys[i - 1] : 2 * ys[0] - ys[1];
                float x1 = xs[i], y1 = ys[i], x2 = xs[i + 1], y2 = ys[i + 1];
                float x3 = i + 2 < n ? xs[i + 2] : 2 * xs[n - 1] - xs[n - 2], y3 = i + 2 < n ? ys[i + 2] : 2 * ys[n - 1] - ys[n - 2];
                float t0 = 0f;
                float t1 = t0 + Knot(x0, y0, x1, y1), t2 = t1 + Knot(x1, y1, x2, y2), t3 = t2 + Knot(x2, y2, x3, y3);
                float seg = Dist(x1, y1, x2, y2);
                int k = Math.Max(1, (int)Math.Ceiling(seg / step));
                for (int s = 1; s <= k; s++)
                {
                    float t = t1 + (t2 - t1) * s / k;
                    float a1x = Lerp(x0, x1, t0, t1, t), a1y = Lerp(y0, y1, t0, t1, t);
                    float a2x = Lerp(x1, x2, t1, t2, t), a2y = Lerp(y1, y2, t1, t2, t);
                    float a3x = Lerp(x2, x3, t2, t3, t), a3y = Lerp(y2, y3, t2, t3, t);
                    float b1x = Lerp(a1x, a2x, t0, t2, t), b1y = Lerp(a1y, a2y, t0, t2, t);
                    float b2x = Lerp(a2x, a3x, t1, t3, t), b2y = Lerp(a2y, a3y, t1, t3, t);
                    rx.Add(Lerp(b1x, b2x, t1, t2, t)); ry.Add(Lerp(b1y, b2y, t1, t2, t));
                }
            }
            Resample(rx, ry, step, out ox, out oy);
        }

        private static float Knot(float ax, float ay, float bx, float by) { return Math.Max(1e-3f, (float)Math.Sqrt(Dist(ax, ay, bx, by))); }

        private static float Lerp(float a, float b, float ta, float tb, float t) { float d = tb - ta; return d < 1e-6f ? a : a + (b - a) * (t - ta) / d; }

        private static void Chaikin(List<float> xs, List<float> ys)
        {
            int n = xs.Count;
            if (n < 3) return;
            var nx = new List<float>(2 * n); var ny = new List<float>(2 * n);
            nx.Add(xs[0]); ny.Add(ys[0]);
            for (int i = 0; i < n - 1; i++)
            {
                nx.Add(0.75f * xs[i] + 0.25f * xs[i + 1]); ny.Add(0.75f * ys[i] + 0.25f * ys[i + 1]);
                nx.Add(0.25f * xs[i] + 0.75f * xs[i + 1]); ny.Add(0.25f * ys[i] + 0.75f * ys[i + 1]);
            }
            nx.Add(xs[n - 1]); ny.Add(ys[n - 1]);
            xs.Clear(); xs.AddRange(nx); ys.Clear(); ys.AddRange(ny);
        }

        /// <summary>Probki rowno co step wzdluz lamanej (pierwszy i ostatni punkt zostaja).</summary>
        internal static void Resample(List<float> xs, List<float> ys, float step, out float[] ox, out float[] oy)
        {
            var rx = new List<float>(); var ry = new List<float>();
            int n = xs.Count;
            if (n == 0) { ox = new float[0]; oy = new float[0]; return; }
            float total = 0f;
            for (int i = 1; i < n; i++) total += Dist(xs[i - 1], ys[i - 1], xs[i], ys[i]);
            int k = Math.Max(1, (int)Math.Round(total / step));
            float want = total / k, acc = 0f;
            rx.Add(xs[0]); ry.Add(ys[0]);
            int seg = 1; float segPos = 0f;
            for (int s = 1; s < k; s++)
            {
                float target = s * want;
                while (seg < n)
                {
                    float l = Dist(xs[seg - 1], ys[seg - 1], xs[seg], ys[seg]);
                    if (acc + l >= target) { segPos = l > 1e-6f ? (target - acc) / l : 0f; break; }
                    acc += l; seg++;
                }
                if (seg >= n) break;
                rx.Add(xs[seg - 1] + (xs[seg] - xs[seg - 1]) * segPos); ry.Add(ys[seg - 1] + (ys[seg] - ys[seg - 1]) * segPos);
            }
            if (n > 1) { rx.Add(xs[n - 1]); ry.Add(ys[n - 1]); }
            ox = rx.ToArray(); oy = ry.ToArray();
        }

        /// <summary>Lagodne wicie: przesuniecie w bok A x sin(2 pi s / fala + faza) x sin(pi s / L) (zero na koncach), fala 5-9 jedn. z ziarna;
        /// amplituda rosnie z dlugoscia do pelnej przy 6 jedn. False - droga za krotka na wicie.</summary>
        private static bool Meander(float[] xs, float[] ys, float amp, uint seed, out float[] ox, out float[] oy)
        {
            ox = oy = null;
            int n = xs.Length;
            if (n < 6) return false;
            var cum = new float[n];
            for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + Dist(xs[i - 1], ys[i - 1], xs[i], ys[i]);
            float L = cum[n - 1];
            if (L < 3f) return false;
            float A = amp * Math.Min(1f, L / 6f);
            float wave = 5f + 4f * Rnd(ref seed);
            float phase = 6.2831853f * Rnd(ref seed);
            float wave2 = 2.3f + 1.4f * Rnd(ref seed), phase2 = 6.2831853f * Rnd(ref seed);
            ox = new float[n]; oy = new float[n];
            for (int i = 0; i < n; i++)
            {
                int a = Math.Max(0, i - 1), b = Math.Min(n - 1, i + 1);
                float tx = xs[b] - xs[a], ty = ys[b] - ys[a], tl = (float)Math.Sqrt(tx * tx + ty * ty);
                if (tl < 1e-5f) { ox[i] = xs[i]; oy[i] = ys[i]; continue; }
                float s = cum[i];
                float off = A * ((float)Math.Sin(6.2831853f * s / wave + phase) * 0.8f + (float)Math.Sin(6.2831853f * s / wave2 + phase2) * 0.2f) * (float)Math.Sin(Math.PI * s / L);
                ox[i] = xs[i] - ty / tl * off; oy[i] = ys[i] + tx / tl * off;
            }
            return true;
        }

        // ------------------------------------------------------------------ stemple
        private static void Stamps(Road rd, int ri, Params p, Result res)
        {
            int n = rd.X.Length;
            if (n < 2) return;
            var cum = new float[n];
            for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + Dist(rd.X[i - 1], rd.Y[i - 1], rd.X[i], rd.Y[i]);
            float L = cum[n - 1];
            uint seed = Hash((uint)p.Seed ^ 0x9E3779B9u, (uint)rd.Node);
            float s = 0.3f + 0.1f * Rnd(ref seed);
            int k = 0;
            while (s <= L)
            {
                if (res.Stamps.Count >= p.MaxStamps) { res.CapStamps++; break; }
                while (k < n - 2 && cum[k + 1] < s) k++;
                float seg = cum[k + 1] - cum[k];
                float t = seg > 1e-6f ? (s - cum[k]) / seg : 0f;
                float x = rd.X[k] + (rd.X[k + 1] - rd.X[k]) * t, y = rd.Y[k] + (rd.Y[k + 1] - rd.Y[k]) * t;
                float dx = rd.X[k + 1] - rd.X[k], dy = rd.Y[k + 1] - rd.Y[k];
                float yaw = (float)Math.Atan2(dy, dx);
                float half = p.Width * 0.5f;
                float tv = s < 1.2f ? 0.7f + 0.3f * (s / 1.2f) : 1f;                         // przy wiosce wezej
                float rest = L - s;
                float fl = rd.Join == 0 && rest < 0.9f ? 1f + 0.3f * (1f - rest / 0.9f) : 1f; // wlot w farbe traktu szerzej
                float across = half * tv * fl * (0.92f + 0.16f * Rnd(ref seed));
                float along = p.StampHalfLen * res.StampScale * (0.9f + 0.2f * Rnd(ref seed));
                float alpha = s < 1.0f ? 0.55f + 0.45f * s : 1f;
                float side = (Rnd(ref seed) - 0.5f) * 0.06f;
                float l = (float)Math.Sqrt(dx * dx + dy * dy);
                if (l > 1e-5f) { x += -dy / l * side; y += dx / l * side; }
                yaw += (Rnd(ref seed) - 0.5f) * 0.16f;
                res.Stamps.Add(new Stamp { X = x, Y = y, Yaw = yaw, Across = across, Along = along, Alpha = alpha, Road = ri });
                s += p.Spacing * res.StampScale * (0.9f + 0.2f * Rnd(ref seed));
            }
        }

        /// <summary>Miejsca z najgestsza siecia (do celow zdjec autotestu - plik JSON dla -PhotosFile): kwadraty 24 x 24 jedn. z najwieksza dlugoscia drog,
        /// najwyzej k, odstep >= 60 jedn.</summary>
        internal static string DenseSpots(Result r, int k)
        {
            if (r == null || r.Roads.Count == 0) return "-";
            const float bin = 24f;
            var sum = new Dictionary<long, float>();
            foreach (var rd in r.Roads)
                for (int i = 1; i < rd.X.Length; i++)
                {
                    long key = ((long)(int)Math.Floor(rd.X[i] / bin) << 32) ^ (uint)(int)Math.Floor(rd.Y[i] / bin);
                    float v;
                    sum.TryGetValue(key, out v);
                    sum[key] = v + Dist(rd.X[i - 1], rd.Y[i - 1], rd.X[i], rd.Y[i]);
                }
            var list = new List<KeyValuePair<long, float>>(sum);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            var picked = new List<float[]>();
            var sb = new StringBuilder();
            foreach (var kv in list)
            {
                if (picked.Count >= k) break;
                float cx = ((int)(kv.Key >> 32) + 0.5f) * bin, cy = ((int)(uint)(kv.Key & 0xFFFFFFFF) + 0.5f) * bin;
                bool near = false;
                foreach (var q in picked) if (Dist(q[0], q[1], cx, cy) < 60f) { near = true; break; }
                if (near) continue;
                picked.Add(new[] { cx, cy });
                if (sb.Length > 0) sb.Append("; ");
                sb.Append('(').Append(cx.ToString("0", Inv)).Append(", ").Append(cy.ToString("0", Inv)).Append(") ").Append(kv.Value.ToString("0", Inv)).Append(" jedn.");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ drobne
        internal static float Dist(float ax, float ay, float bx, float by) { float dx = bx - ax, dy = by - ay; return (float)Math.Sqrt(dx * dx + dy * dy); }

        internal static uint Hash(uint a, uint b)
        {
            uint h = 2166136261u;
            h = (h ^ a) * 16777619u; h = (h ^ b) * 16777619u;
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return h == 0 ? 1u : h;
        }

        /// <summary>xorshift32 -> 0..1.</summary>
        internal static float Rnd(ref uint s)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (s & 0xFFFFFF) / 16777216f;
        }
    }
}
