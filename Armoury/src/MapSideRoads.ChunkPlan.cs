// f22-drogi-perf: CZYSTA GEOMETRIA KOMOREK BOCZNYCH DROG (bez typow gry - ten sam plik kompiluje podglad poza gra tools/drogi/boczne-podglad):
// podzial stempli planu na komorki 32 x 32 jedn. (watek z puli razem z planem), kolo widoku kamery z 4 rogow ekranu, promien przy zwyklym pochyleniu
// kamery gry i oczekiwane decale w scenie przy typowych wysokosciach kamery. Opis zasad - MapSideRoads.Chunks.cs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Armoury
{
    internal static partial class MapSideRoads
    {
        internal const float ChunkCell = 32f;      // bok komorki (jedn. mapy)
        internal const float ChunkMargin = 8f;     // promien kola ponad najdalszy rog ekranu (stempel do ok. 1.2 jedn. + ruch kamery w cwierc sekundy) ...
        internal const float ChunkMarginPerH = 0.1f;   // ... + tyle na jednostke wysokosci kamery (wyzej = szybsze przesuwanie mapy: 9 przy 10, 12 przy 40, 16 przy 80)
        internal const float ChunkHyst = 16f;      // komorka w scenie schodzi dopiero dalej niz promien + tyle (pol komorki)
        internal const float MapFovV = 0.6981317f; // pion kamery mapy (MapCameraView: Camera.SetFovVertical(0.6981317f, Screen.AspectRatio, ...))
        internal const int ChunkCapDefault = 160;
        internal const int ChunkCapMin = 40, ChunkCapMax = 400;   // przyciecie klucza MapSideRoadsChunkRadiusCap (suwak MCM 40..400)
        internal const float MapCamMinH = 1.62f;   // kamera najblizej (MapCameraView: TargetCameraDistance >= 2.5) - (2.5 + 2) sin(0.0075 * 2.5 + 0.35)

        /// <summary>Podzial stempli planu na komorki (czysta geometria - watek z puli, razem z planem).</summary>
        internal sealed class ChunkGrid
        {
            public float Cell = ChunkCell;
            public int X0, Y0, W, H;           // komorka (cx, cy) ma indeks (cx - X0) + (cy - Y0) * W
            public int[][] Idx;                // stemple komorki (indeksy w Result.Stamps); null - komorka bez drog
            public int Cells, PerMed, PerP90, PerMax, Lost;
            public double PerAvg;
            public string Expect = "";         // oczekiwane decale w scenie przy typowych wysokosciach kamery (linia "komorki")
        }

        internal static ChunkGrid GroupChunks(SideRoadPlan.Result r, List<SideRoadPlan.Node> nodes, float cap)
        {
            if (r == null || r.Stamps.Count == 0) return null;
            const float cs = ChunkCell;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            var st = r.Stamps;
            for (int i = 0; i < st.Count; i++)
            {
                float x = st[i].X, y = st[i].Y;
                if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y) || Math.Abs(x) > 1e6f || Math.Abs(y) > 1e6f) continue;
                int cx = (int)Math.Floor(x / cs), cy = (int)Math.Floor(y / cs);
                if (cx < minX) minX = cx; if (cx > maxX) maxX = cx;
                if (cy < minY) minY = cy; if (cy > maxY) maxY = cy;
            }
            if (minX > maxX || minY > maxY) return null;
            long wl = (long)maxX - minX + 1, hl = (long)maxY - minY + 1;
            if (wl * hl > 1000000L) return null;   // straznik (mapa ROT ok. 1600 x 1400 jedn. = 50 x 44 komorek)
            var g = new ChunkGrid { X0 = minX, Y0 = minY, W = (int)wl, H = (int)hl };
            var cnt = new int[g.W * g.H];
            var at = new int[st.Count];
            for (int i = 0; i < st.Count; i++)
            {
                float x = st[i].X, y = st[i].Y;
                if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y) || Math.Abs(x) > 1e6f || Math.Abs(y) > 1e6f) { at[i] = -1; g.Lost++; continue; }
                int k = ((int)Math.Floor(x / cs) - g.X0) + ((int)Math.Floor(y / cs) - g.Y0) * g.W;
                at[i] = k;
                cnt[k]++;
            }
            g.Idx = new int[cnt.Length][];
            var per = new List<int>(1024);
            for (int k = 0; k < cnt.Length; k++)
                if (cnt[k] > 0) { g.Idx[k] = new int[cnt[k]]; per.Add(cnt[k]); cnt[k] = 0; }
            for (int i = 0; i < st.Count; i++)
            {
                int k = at[i];
                if (k < 0) continue;
                g.Idx[k][cnt[k]++] = i;
            }
            per.Sort();
            g.Cells = per.Count;
            if (per.Count > 0)
            {
                g.PerMed = per[per.Count / 2];
                g.PerP90 = per[Math.Min(per.Count - 1, (int)(per.Count * 0.9))];
                g.PerMax = per[per.Count - 1];
                g.PerAvg = (double)(st.Count - g.Lost) / per.Count;
            }
            g.Expect = Expect(g, nodes, cap);
            return g;
        }

        /// <summary>Oczekiwane decale (stemple) w scenie przy kamerze z 10 / 20 / 40 / 80 (zwykle pochylenie, ekran 16:9): kolo o promieniu z ViewCircle wokol
        /// kazdej wioski (ukladu i gry) - mediana, p90 i najwiecej stempli w komorkach dotykajacych kola, mediana komorek.</summary>
        private static string Expect(ChunkGrid g, List<SideRoadPlan.Node> nodes, float cap)
        {
            if (g == null || nodes == null || nodes.Count == 0) return "";
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("oczekiwane decale w scenie (kolo wokol kazdej z ").Append(nodes.Count).Append(" wiosek, zwykle pochylenie kamery, ekran 16:9): ");
            float[] hs = { 10f, 20f, 40f, 80f };
            var dec = new List<int>(nodes.Count);
            var cel = new List<int>(nodes.Count);
            for (int h = 0; h < hs.Length; h++)
            {
                float r = DefaultRadius(hs[h], cap);
                dec.Clear(); cel.Clear();
                foreach (var n in nodes)
                {
                    int d, c;
                    CountIn(g, n.X, n.Y, r, out d, out c);
                    dec.Add(d); cel.Add(c);
                }
                dec.Sort(); cel.Sort();
                if (h > 0) sb.Append("; ");
                sb.Append("z ").Append(hs[h].ToString("0", inv)).Append(" (promien ").Append(r.ToString("0", inv)).Append(") mediana ").Append(dec[dec.Count / 2])
                  .Append(", p90 ").Append(dec[Math.Min(dec.Count - 1, (int)(dec.Count * 0.9))]).Append(", najwiecej ").Append(dec[dec.Count - 1])
                  .Append(" (komorek ").Append(cel[cel.Count / 2]).Append(')');
            }
            return sb.ToString();
        }

        private static void CountIn(ChunkGrid g, float cx, float cy, float r, out int decals, out int cells)
        {
            decals = 0; cells = 0;
            float cs = g.Cell, r2 = r * r;
            int i0 = Math.Max(0, (int)Math.Floor((cx - r) / cs) - g.X0), i1 = Math.Min(g.W - 1, (int)Math.Floor((cx + r) / cs) - g.X0);
            int j0 = Math.Max(0, (int)Math.Floor((cy - r) / cs) - g.Y0), j1 = Math.Min(g.H - 1, (int)Math.Floor((cy + r) / cs) - g.Y0);
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    var a = g.Idx[i + j * g.W];
                    if (a == null) continue;
                    if (RectDist2(i + g.X0, j + g.Y0, cs, cx, cy) > r2) continue;
                    decals += a.Length; cells++;
                }
        }

        /// <summary>Kwadrat odleglosci punktu od prostokata komorki (0 - w srodku).</summary>
        internal static float RectDist2(int cx, int cy, float cs, float x, float y)
        {
            float x0 = cx * cs, y0 = cy * cs;
            float dx = x < x0 ? x0 - x : (x > x0 + cs ? x - x0 - cs : 0f);
            float dy = y < y0 ? y0 - y : (y > y0 + cs ? y - y0 - cs : 0f);
            return dx * dx + dy * dy;
        }

        /// <summary>Kolo komorek wokol tego, co widzi kamera (czysta geometria). Kamera w (px, py, pz), osie ramki: s (w bok ekranu), f (w gore ekranu), u (do tylu -
        /// kamera patrzy w -u, Camera.Direction gry). 4 promienie rogow ekranu (-u +- s tgH +- f tgV - znaki osi bez znaczenia, rogi sa symetryczne) przeciete
        /// z ziemia z = zg; rog dalej niz cap (poziomo od kamery) albo nad horyzontem - punkt na cap w kierunku promienia. Srodek = srednia rogow, promien =
        /// najdalszy rog od srodka + margin. false - zle dane (NaN, zerowe osie).</summary>
        internal static bool ViewCircle(float px, float py, float pz, float sx, float sy, float sz, float fx, float fy, float fz, float ux, float uy, float uz,
                                        float tanV, float tanH, float zg, float cap, float margin, out float cx, out float cy, out float r)
        {
            cx = px; cy = py; r = 0f;
            if (!Norm(ref sx, ref sy, ref sz) || !Norm(ref fx, ref fy, ref fz) || !Norm(ref ux, ref uy, ref uz)) return false;
            if (float.IsNaN(px) || float.IsNaN(py) || float.IsNaN(pz) || float.IsNaN(zg) || float.IsNaN(tanV) || float.IsNaN(tanH)) return false;
            float h = pz - zg;
            if (h < 0.5f) h = 0.5f;
            var hx = new float[4];
            var hy = new float[4];
            int k = 0;
            for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2)
                {
                    float dx = -ux + sx * a * tanH + fx * b * tanV;
                    float dy = -uy + sy * a * tanH + fy * b * tanV;
                    float dz = -uz + sz * a * tanH + fz * b * tanV;
                    float lh = (float)Math.Sqrt(dx * dx + dy * dy);
                    float ox, oy;
                    if (dz < -1e-4f)
                    {
                        float t = h / -dz;
                        ox = dx * t; oy = dy * t;
                        float dist = (float)Math.Sqrt(ox * ox + oy * oy);
                        if (dist > cap) { ox = ox / dist * cap; oy = oy / dist * cap; }
                    }
                    else if (lh > 1e-5f) { ox = dx / lh * cap; oy = dy / lh * cap; }
                    else { ox = 0f; oy = 0f; }
                    hx[k] = px + ox; hy[k] = py + oy; k++;
                }
            float mx = (hx[0] + hx[1] + hx[2] + hx[3]) * 0.25f, my = (hy[0] + hy[1] + hy[2] + hy[3]) * 0.25f;
            float best = 0f;
            for (int i = 0; i < 4; i++)
            {
                float ddx = hx[i] - mx, ddy = hy[i] - my;
                float d2 = ddx * ddx + ddy * ddy;
                if (d2 > best) best = d2;
            }
            cx = mx; cy = my; r = (float)Math.Sqrt(best) + margin;
            return !(float.IsNaN(cx) || float.IsNaN(cy) || float.IsNaN(r) || float.IsInfinity(r));
        }

        private static bool Norm(ref float x, ref float y, ref float z)
        {
            float l = (float)Math.Sqrt(x * x + y * y + z * z);
            if (!(l > 1e-6f) || float.IsInfinity(l)) return false;
            x /= l; y /= l; z /= l;
            return true;
        }

        /// <summary>Zapas promienia kola ponad najdalszy rog ekranu przy kamerze h nad ziemia.</summary>
        internal static float MarginFor(float h) { return ChunkMargin + ChunkMarginPerH * (h > 0f ? (h < 400f ? h : 400f) : 0f); }

        /// <summary>Wzniesienie kamery w zwyklym pochyleniu gry przy kamerze z h nad celem (MapCameraView: e = 0.0075 d + 0.35, kamera (d + 2) od celu;
        /// d z h bisekcja).</summary>
        internal static float DefaultElevation(float h)
        {
            float lo = 0f, hi = 160f, d = 0f;
            for (int i = 0; i < 60; i++)
            {
                d = (lo + hi) * 0.5f;
                double e = Math.Min(0.0075 * d + 0.35, Math.PI * 99.0 / 200.0);
                if ((d + 2f) * Math.Sin(e) < h) lo = d; else hi = d;
            }
            return (float)Math.Min(0.0075 * d + 0.35, Math.PI * 99.0 / 200.0);
        }

        /// <summary>Poprawka po recenzji f22: odleglosc (poziomo od kamery) najdalszego rogu ekranu w zwyklym pochyleniu gry przy kamerze z h nad ziemia -
        /// granica kola nigdy nie jest blizej (klucz MapSideRoadsChunkRadiusCap tnie tylko przy kamerze pochylonej nizej niz zwykle). Przy 40 st. i 16:9:
        /// 81 jedn. z 10, 92 z 20, 108 z 40, 125 z 80, 128 z 110 (z 1.6 - kamera najblizej - 98); ekran szerszy - dalej (21:9 z 110: 148). h ponizej kamery
        /// najblizej gry liczone jak kamera najblizej (nizej gorna krawedz ekranu idzie w horyzont); wynik najwyzej ChunkCapMax.</summary>
        internal static float DefaultFar(float h, float tanV, float tanH)
        {
            if (float.IsNaN(h) || float.IsNaN(tanV) || float.IsNaN(tanH)) return 0f;
            if (h < MapCamMinH) h = MapCamMinH;
            float el = DefaultElevation(h);
            float c = (float)Math.Cos(el), s = (float)Math.Sin(el);
            // gorne rogi ekranu (jak w ViewCircle: -u + f tgV +- s tgH; u = (-c, 0, s), f = (s, 0, c), s = (0, 1, 0))
            float dz = -s + c * tanV;
            if (!(dz < -1e-4f)) return ChunkCapMax;   // gorna krawedz ekranu w horyzoncie
            float dx = c + s * tanV;
            float far = h / -dz * (float)Math.Sqrt(dx * dx + tanH * tanH);
            if (float.IsNaN(far) || far > ChunkCapMax) return ChunkCapMax;
            return far;
        }

        /// <summary>Promien kola przy kamerze z h nad ziemia w zwyklym pochyleniu gry (MapCameraView: wzniesienie e = 0.0075 d + 0.35, kamera (d + 2) od celu),
        /// pion 40 st., ekran 16:9 - do linii oczekiwanych decali. Granica jak w grze: klucz, ale nie blizej niz najdalszy rog ekranu (DefaultFar).</summary>
        internal static float DefaultRadius(float h, float cap)
        {
            float el = DefaultElevation(h);
            float c = (float)Math.Cos(el), s = (float)Math.Sin(el);
            // kamera patrzy w +x w dol pod katem el: u (do tylu) = (-c, 0, s), f (gora ekranu) = (s, 0, c), s (bok) = (0, 1, 0)
            float tanV = (float)Math.Tan(MapFovV * 0.5f);
            float tanH = tanV * 16f / 9f;
            cap = Math.Max(cap, DefaultFar(h, tanV, tanH));
            float cx, cy, r;
            if (!ViewCircle(0f, 0f, h, 0f, 1f, 0f, s, 0f, c, -c, 0f, s, tanV, tanH, 0f, cap, MarginFor(h), out cx, out cy, out r)) return cap;
            return r;
        }
    }
}
