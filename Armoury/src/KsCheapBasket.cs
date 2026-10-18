using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>r4-koszyk: tanszy koszyk w grze (Zc4Ladder -> Zc4LadderCheap), linia P9 Planu, dokupiona podstawa w zapisie. Rdzen drabiny - KsCheap.cs (czysty C#).</summary>
    internal static partial class StartCalibration
    {
        internal static bool SwCheapBasket { get { var s = Settings.Current; return s == null || s.KsCheapBasket; } }

        // r4-koszyk: dokupiona podstawa koszyka miasta z Planu (czesc koszyka ponad 1 wedlug pozycji KsBasket.Ids; czesc zaplacona _townPaid zostaje 0..1 - zapis "@" bez zmian,
        // starsza DLL jej nie widzi) - klucz zapisu arm_ks_koszyk
        private static Dictionary<string, float[]> _townExtra = new Dictionary<string, float[]>(StringComparer.Ordinal);

        /// <summary>Dokupiona podstawa koszyka miasta (czesc ponad koszyk wedlug pozycji) albo null (brak albo klucz Ks Cheap Basket wylaczony - jak dotad).</summary>
        private static float[] TownExtraOf(Settlement st)
        {
            if (!SwCheapBasket || st == null || st.StringId == null) return null;
            float[] x;
            return _townExtra.TryGetValue(st.StringId, out x) ? x : null;
        }

        /// <summary>r4-koszyk: miasto w mapie koszyka ZC4 (KS zastosowane, lambda > 0) - place za brak rak (KsWageHands) dzialaja tylko tam.</summary>
        internal static bool KsTownApplied(Town t) { ZTown zt; return ZTownOf(t, out zt); }

        private static void CheapReset()
        {
            _townExtra = new Dictionary<string, float[]>(StringComparer.Ordinal);
            CheapZero();
        }

        // ------------------------------------------------------------ gra: drabina tanszego koszyka (TownFolk.AnchorDict -> Zc4Ladder)
        // liczniki od ostatniej linii wedlug rynku: miasto-doby drabiny, bez podstawy na polce (stara drabina), kcal potrzebne / kupione, budzet / wydane, zloto wedlug
        // grup (zboze i ryby podstawy, bialko, napoje, reszta) i dokupiona podstawa (zl ponad koszyk), niekupione wedlug grup
        private static readonly int[] _chN = new int[2], _chOld = new int[2];
        private static readonly double[] _chKN = new double[2], _chKG = new double[2], _chB = new double[2], _chSp = new double[2], _chX = new double[2];
        private static readonly double[] _chG = new double[8], _chCut = new double[8];
        private static int _chStum;
        private static void CheapZero()
        {
            for (int m = 0; m < 2; m++) { _chN[m] = _chOld[m] = 0; _chKN[m] = _chKG[m] = _chB[m] = _chSp[m] = _chX[m] = 0; }
            Array.Clear(_chG, 0, _chG.Length); Array.Clear(_chCut, 0, _chCut.Length);
        }

        /// <summary>
        /// Drabina tanszego koszyka w grze (klucz Ks Cheap Basket; TownFolk.AnchorDict, kiesy nie stac na caly koszyk jedzenia): budzet B = ff x suma kategorii jedzenia
        /// koszyka - KsCheap.Core na kategoriach (pozycja = najtanszy za kg towar kategorii z polka; kcal = zloto kategorii x kcal na zl; KAZDA pozycja - pula podstawy i kazde podniesienie - najwyzej to, co lezy na polce: cap, poprawka recenzji).
        /// Zwraca sume slownika albo -1 (zadna podstawa nie lezy na polce - dotychczasowa drabina).
        /// </summary>
        private static double Zc4LadderCheap(Town town, ZTown zt, Dictionary<ItemCategory, float> dict, List<ItemCategory> keys, double ff, double tot)
        {
            int n = keys.Count;
            if (n == 0) return -1;
            var ro = town.Settlement != null ? town.Settlement.ItemRoster : null;
            int[] sc = ro != null && SwCostFast ? ShelfFirst(ro) : null;     // ks-koszt T10: ilosci pozycji na polce jednym przegladem (te same liczby co GetItemNumber)
            var need = new double[n]; var kcal = new double[n]; var rung = new int[n]; var staple = new bool[n]; var key = new double[n]; var cap = new double[n]; var give = new double[n];
            bool anyStaple = false;
            for (int i = 0; i < n; i++)
            {
                var c = keys[i];
                float v; dict.TryGetValue(c, out v);
                need[i] = v > 0 ? v : 0.0;
                int[] zs; _zCats.TryGetValue(c, out zs);
                int best = -1; double bestPk = double.MaxValue; bool bestStock = false; long cnt = 0;
                if (zs != null)
                    foreach (int z in zs)
                    {
                        var it = _zItems[z]; if (it == null) continue;
                        int have = ro != null ? (sc != null ? CountZ(ro, z, sc) : ro.GetItemNumber(it)) : 0;
                        cnt += Math.Max(0, have);
                        double pk = Math.Max(1, zt.Px[z]) / _zKg[z];
                        bool st = have > 0;
                        if (best < 0 || (st && !bestStock) || (st == bestStock && pk < bestPk)) { best = z; bestPk = pk; bestStock = st; }
                    }
                int rg = best >= 0 ? Rung(best) : 4;
                rung[i] = rg == 0 ? KsCheap.RStaple : rg == 1 ? KsCheap.RSalt : rg == 2 ? KsCheap.RDrink : rg == 3 ? KsCheap.RProtein : KsCheap.RRest;
                double kk = best >= 0 ? KsBasket.Kcal[best] : 0.0;
                kcal[i] = best >= 0 && bestPk > 0 ? need[i] / bestPk * kk : 0.0;   // zl -> kg -> kcal
                staple[i] = best >= 0 && (rg == 0 || best == ZFish);
                key[i] = rung[i] == KsCheap.RProtein ? (kk > 0 ? bestPk / kk : double.MaxValue) : bestPk;
                cap[i] = best >= 0 ? cnt * Math.Max(1, zt.Px[best]) : 0.0;
                if (staple[i] && need[i] > 0 && kcal[i] > 0 && cap[i] > 0) anyStaple = true;
            }
            int m = zt.Market == 0 ? 0 : 1;
            if (!anyStaple) { _chOld[m]++; return -1; }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            double B = Math.Max(0.0, ff) * tot, kN, kG;
            KsCheap.Core(n, need, kcal, rung, staple, key, cap, 0.0, B, give, out kN, out kG);
            double after = 0;
            for (int i = 0; i < n; i++)
            {
                double g = give[i] > 0 && !double.IsNaN(give[i]) && !double.IsInfinity(give[i]) ? give[i] : 0.0;
                dict[keys[i]] = (float)g; after += g;
                int gr = KsCheap.Group(rung[i]);
                if (gr < 0) continue;
                int gi = staple[i] ? 0 : gr;
                _chG[m * 4 + gi] += g;
                if (g > need[i]) _chX[m] += g - need[i];
                else _chCut[m * 4 + gr] += need[i] - g;
            }
            _chN[m]++; _chKN[m] += kN; _chKG[m] += kG; _chB[m] += B; _chSp[m] += after;
            _zLadderN++;
            _zLadCut += Math.Max(0.0, tot - after);                                                     // jedzenie koszyka niekupione z braku pieniedzy (zl) - jak dotad
            _zTkLadder += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            return after;
        }

        /// <summary>Linia "Tanszy koszyk (r4-koszyk): dzien N" (liczniki od ostatniej linii; po linii ZC4) albo null (klucz wylaczony i nic nie liczylo).</summary>
        private static string CheapLine(int day)
        {
            bool on = SwCheapBasket;
            if (!on && _chN[0] + _chN[1] + _chOld[0] + _chOld[1] == 0 && _townExtra.Count == 0) { CheapZero(); return null; }
            var sb = new StringBuilder(1400);
            sb.Append("Tanszy koszyk (r4-koszyk): dzien ").Append(day).Append(" | klucz Ks Cheap Basket ").Append(on ? "TAK" : "NIE");
            string[] mk = { "Westeros", "Essos" };
            string[] gn = { "zboze i ryby (podstawa)", "bialko", "napoje", "reszta" };
            for (int m = 0; m < 2; m++)
            {
                sb.Append(m == 0 ? " | " : "; ").Append(mk[m]).Append(": drabina w ").Append(_chN[m]).Append(" miasto-dobach (bez podstawy na polce - stara drabina ").Append(_chOld[m])
                  .Append("), kcal jedzenia kupione ").Append(_chKN[m] > 0 ? (100.0 * _chKG[m] / _chKN[m]).ToString("0.0", Inv) : "-").Append("% potrzebnych")
                  .Append(", budzet ").Append(F0(_chB[m])).Append(" zl = wydane ").Append(F0(_chSp[m])).Append(" + niewydane ").Append(F0(_chB[m] - _chSp[m]))
                  .Append(" zl (wydane:");
                for (int g = 0; g < 4; g++) sb.Append(g == 0 ? " " : ", ").Append(gn[g]).Append(' ').Append(F0(_chG[m * 4 + g]));
                sb.Append("; podstawa ponad koszyk ").Append(F0(_chX[m])).Append(" zl; niekupione:");
                for (int g = 1; g < 4; g++) sb.Append(g == 1 ? " " : ", ").Append(gn[g]).Append(' ').Append(F0(_chCut[m * 4 + g]));
                sb.Append(')');
            }
            // dokupiona podstawa z Planu w popycie miast (szt./d przy lambdzie)
            var map = _zt; var xp = new double[ZN]; int xn = 0;
            foreach (var zt in map.Values)
            {
                var x = zt.Extra; if (x == null) continue;
                bool any = false;
                for (int z = 0; z < ZN && z < x.Length; z++)
                {
                    if (!(x[z] > 0)) continue;
                    double tp = zt.Paid != null && z < zt.Paid.Length ? Math.Max(0f, Math.Min(1f, zt.Paid[z])) : 1.0, f = tp + x[z];
                    if (f > 0) { xp[z] += zt.Q[z] * x[z] / f * zt.Lam; any = true; }
                }
                if (any) xn++;
            }
            sb.Append(" | Plan: dokupiona podstawa w popycie miast (szt./d przy lambdzie) w ").Append(xn).Append(" miastach:");
            bool fx = true;
            foreach (int z in new[] { ZGrain, ZBread, ZPie, ZFish }) { if (xp[z] < 0.5) continue; sb.Append(fx ? " " : ", ").Append(ZIds[z]).Append(' ').Append(F0(xp[z])); fx = false; }
            if (fx) sb.Append(" -");
            sb.Append(" | potkniecia ").Append(_chStum).Append('.');
            CheapZero(); _chStum = 0;
            return sb.ToString();
        }

        // ------------------------------------------------------------ Plan: linia P9 (podzial niezaplaconego koszyka klas miast na kcal jedzenia i rzeczy)
        /// <summary>Linia "KS: tanszy koszyk miast (P9, r4-koszyk) plan doba N" - takze przy kluczu wylaczonym (podzial P9 stara drabina).</summary>
        private static void P9Line(KsResult r)
        {
            var M = r.Money;
            if (M == null || !M.On) return;
            var sb = new StringBuilder(1600);
            sb.Append(PK).Append("tanszy koszyk miast (P9, r4-koszyk) plan doba ").Append(_wDay).Append(" | klucz Ks Cheap Basket ").Append(M.Cheap ? "TAK" : "NIE")
              .Append(" (klasy miast przy cenach startu; kcal kupione = zaplacone + dokupiona podstawa)");
            string[] cls = { "W", "R", "P" };
            double sigma = Math.Max(1, DailyLife.KsSigma);
            for (int m = 0; m < 2; m++)
            {
                sb.Append(" | ").Append(r.Markets[m]).Append(':');
                for (int c = 0; c < 3; c++)
                {
                    int o = (m * 3 + c) * KsCheap.StatN;
                    double kn = M.P9[o], kg = M.P9[o + 1], gc = M.P9[o + 2], gp = M.P9[o + 3];
                    sb.Append(c == 0 ? " " : "; ").Append(cls[c]).Append(" kcal jedzenia ").Append(kn > 0 ? (100.0 * kg / kn).ToString("0.0", Inv) : "-").Append("%, rzeczy ")
                      .Append(gc > 0 ? (100.0 * gp / gc).ToString("0.0", Inv) : "-").Append("%, sklad kcal kupione/koszyk (zboze/bialko/napoje/reszta) ");
                    for (int g = 0; g < 4; g++) sb.Append(g == 0 ? "" : "/").Append(kg > 0 ? (100.0 * M.P9[o + 4 + g] / kg).ToString("0", Inv) : "-");
                    sb.Append(" vs ");
                    for (int g = 0; g < 4; g++) sb.Append(g == 0 ? "" : "/").Append(kn > 0 ? (100.0 * M.P9[o + 8 + g] / kn).ToString("0", Inv) : "-");
                    sb.Append("%, stara drabina lepsza ").Append(F0(M.P9[o + 12]));
                }
                sb.Append("; dokupiona podstawa (szt. gry/d):");
                bool f = true;
                foreach (int z in new[] { KsBasket.Grain, KsBasket.Bread, KsBasket.Pie, KsBasket.Fish })
                {
                    double pcs = M.ExtraT[m * KsBasket.N + z] / (sigma * ZKg(z));
                    if (pcs < 0.5) continue;
                    sb.Append(f ? " " : ", ").Append(KsBasket.Ids[z]).Append(' ').Append(F0(pcs)); f = false;
                }
                if (f) sb.Append(" -");
            }
            sb.Append(" | P9: klasy R i P - kcal jedzenia >= 95% (prog), rzeczy - INFO.");
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ zapis (klucz arm_ks_koszyk: dokupiona podstawa z Planu)
        /// <summary>Dokupiona podstawa miast: "miasto:z=v/z=v,..." (tylko v > 0.0005).</summary>
        internal static string ExportCheap()
        {
            var sb = new StringBuilder();
            foreach (var kv in _townExtra)
            {
                if (kv.Value == null || string.IsNullOrEmpty(kv.Key) || kv.Key.IndexOfAny(new[] { ':', ',', '/', '=', '|' }) >= 0) continue;
                var p = new StringBuilder();
                for (int z = 0; z < kv.Value.Length; z++)
                {
                    float v = kv.Value[z];
                    if (!(v > 0.0005f) || float.IsInfinity(v)) continue;
                    if (p.Length > 0) p.Append('/');
                    p.Append(z.ToString(Inv)).Append('=').Append(Math.Min(v, 50f).ToString("0.####", Inv));
                }
                if (p.Length == 0) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(kv.Key).Append(':').Append(p);
            }
            return sb.ToString();
        }

        /// <summary>Odczyt (klucz arm_ks_koszyk). Zly wpis - pomijany; straznik rozmiaru (najwyzej 4000 miast, v w 0..50).</summary>
        internal static void ImportCheap(string s)
        {
            var map = new Dictionary<string, float[]>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(s) && s.Length <= 400000)
            {
                foreach (var ent in s.Split(','))
                {
                    if (map.Count >= 4000) break;
                    int c = ent.IndexOf(':'); if (c <= 0) continue;
                    var a = new float[KsBasket.N]; bool any = false;
                    foreach (var kv in ent.Substring(c + 1).Split('/'))
                    {
                        var q = kv.Split('='); int z; float v;
                        if (q.Length == 2 && int.TryParse(q[0], NumberStyles.Integer, Inv, out z) && z >= 0 && z < a.Length && float.TryParse(q[1], NumberStyles.Float, Inv, out v) && v > 0 && v <= 50f) { a[z] = v; any = true; }
                    }
                    if (any) map[ent.Substring(0, c)] = a;
                }
            }
            _townExtra = map;
        }
    }
}
