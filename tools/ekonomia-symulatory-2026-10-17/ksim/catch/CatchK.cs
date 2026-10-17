using System;
using System.Collections.Generic;

namespace Armoury
{
    /// <summary>PROTOTYP (offline): wejscie planu targow - K targow (miasto + jego wsie wedlug TradeBound + pola mieszczan), korytarze (wozy wsi do 250, kupcy dalej).</summary>
    internal sealed class KInp
    {
        public int K; public int[] Mk; public string[] Id, Name; public bool[] Garum;
        public bool Mode1 = true;                       // true - rownowaga targow; false - rownowaga 2 rynkow (ocena rozkladu po fakcie)
        public bool[] Loc;                              // towary targu (cena na targ); reszta - cena rynku (Westeros / Essos)
        public int NE; public int[] CI, CJ, CT; public double[] CDays, CDist, CR, CA;   // korytarze: zrodlo, cel, typ (0 woz wsi, 1 kupiec lad, 2 kupiec morze), doby, dl., wsp. wozu, udzial rak wsi w zasiegu
        public double CapLong = -1;                     // kg/d na korytarz kupca i towar (<0 - bez sufitu poza popytem celu i podaza zrodla; 0 - bez kupcow)
        public double CapShare = -1;                    // > 0: sufit kupca = udzial popytu celu (np. 0.25) - przewoz "zmierzony" w ulamku
        public double Theta = 1.0, Margin0 = 0.05, CostPer100 = 0.05, PerishDays = 7, DurDays = 30, FDamp = 0.02;
        public double[] WarmP2;                          // ceny 2 rynkow do cieplego startu
        public bool Money;                               // krok 8 na targ
        public bool Subst;                               // krok 8: kcal niezaplacone spoza HomeMask robione z wlasnego zboza / jedzenia (zamiennik kcal)
        public bool Retain;                              // krok 8: kcal bez pokrycia z wlasnego ziarna zatrzymanego (nie sprzedanego)
        public bool ExcStart;                            // nadwyzka targow w cenach startu (0.7-1.5) zamiast cen cienia
        public bool GMarket = true;                      // poprawka G na rynek (suma nadwyzek targow + towarow rynku), jak MoneyStep; false - G na targ
    }

    internal sealed class KOut
    {
        public double[] P, S, Snet, D, Svil, Dw, Sw; public double[] F;   // F[e * NG + g]
        public int Rounds; public bool Conv; public List<string> Open = new List<string>(), Bounds = new List<string>();
        public double[] HandsCraft;                     // [k * NC + c]
        public double[] P2;                              // ceny 2 rynkow
        public double[] Eaten;                           // [k * NG + g]
        public double[] VSubKcal = new double[2];        // kcal zamiennika (krok 8, Subst)
        public double[] VRetKcal = new double[2];        // kcal wlasnego zboza zatrzymanego zamiast sprzedazy (krok 8, Retain)
    }

    internal static partial class KsCore
    {
        private sealed class KSt
        {
            public double[] P, Pw, Xs, S, Svil, Snet, Sw, F, Dk; public int Rounds; public bool Conv;
            public Dictionary<string, int> Dck; public List<int> LocG; public bool[] Per;
        }

        private static double[] DemandK(Ctx x, KInp KI)
        {
            int K = KI.K, NG = KsTab.NG;
            var civ = new double[K * KsTab.NN];
            var pop = new double[K];
            Action<KsSettlement> add = s =>
            {
                if (s.Ck < 0) return;
                for (int k = 0; k < KsTab.NN; k++) civ[s.Ck * KsTab.NN + k] += s.Need[k];
                pop[s.Ck] += s.People;
            };
            foreach (var d in x.D) add(d);
            foreach (var t in x.In.Towns) add(t);
            double wpop = 0; foreach (var v in pop) wpop += v; if (!(wpop > 0)) wpop = 1;
            int ng = 0; for (int k = 0; k < K; k++) if (KI.Garum != null && KI.Garum[k]) ng++;
            var Dk = new double[K * NG];
            var C = new double[KsTab.NN];
            for (int kk = 0; kk < K; kk++)
            {
                Array.Copy(civ, kk * KsTab.NN, C, 0, KsTab.NN);
                double sh = pop[kk] / wpop;
                var D = new double[NG];
                Func<string, double> o = id => OneToOne(x, id) * sh;
                D[KsTab.GGrain] += C[KsTab.NG_] + o("grain") + o("bread") + o("pie") + (C[KsTab.NBeer] + o("beer")) * KsTab.BeerGrain;
                D[KsTab.GFish] += C[KsTab.NFish] + o("fish");
                D[KsTab.GCheese] += C[KsTab.NCheese] + o("cheese");
                D[KsTab.GButter] += C[KsTab.NButter] + o("butter");
                D[KsTab.GEgg] += C[KsTab.NEgg] + o("Egg");
                D[KsTab.GSalt] += C[KsTab.NSalt] + o("salt");
                D[KsTab.GHoney] += C[KsTab.NHoney] + o("honey");
                D[KsTab.GMeat] += C[KsTab.NMeat] + o("meat") + o("WhaleMeat");
                for (int i = 0; i < 6; i++) D[KsTab.FruitGood[i]] += C[KsTab.FruitNeed[i]] + o(KsTab.Goods[KsTab.FruitGood[i]]);
                D[KsTab.GGrape] += (C[KsTab.NWine] + o("wine")) * KsTab.WineGrape;
                D[KsTab.GOlives] += (C[KsTab.NOil] + o("oil") + C[KsTab.NSwOlej]) * KsTab.OilOlive;
                D[KsTab.GHoney] += (C[KsTab.NMead] + o("mead")) * KsTab.MeadHoney;
                if (KI.Garum != null && KI.Garum[kk] && ng > 0) { D[KsTab.GFish] += KsTab.Garum * 2.0 / ng; D[KsTab.GSalt] += KsTab.Garum * 0.4 / ng; }
                for (int g = 0; g < NG; g++) Dk[kk * NG + g] = D[g];
            }
            return Dk;
        }

        private static void SharesK(Ctx x, double[] p, double[] rent, double[] xs, int K)
        {
            var inp = x.In; int NC = KsTab.NC, NG = KsTab.NG;
            if (_mvG == null || _mvRef != x.MV)
            {
                _mvRef = x.MV; _mvG = new int[NC][]; _mvV = new double[NC][];
                for (int c = 0; c < NC; c++) { var lg = new List<int>(); var lv = new List<double>(); for (int g = 0; g < NG; g++) { double v = x.MV[c * NG + g]; if (v != 0) { lg.Add(g); lv.Add(v); } } _mvG[c] = lg.ToArray(); _mvV[c] = lv.ToArray(); }
            }
            if (_rm == null || _rm.Length != K * NC) _rm = new double[K * NC];
            var Rm = _rm;
            for (int k = 0; k < K; k++)
            {
                int pm = k * NG;
                for (int c = 0; c < NC; c++)
                {
                    double s = 0; var gg = _mvG[c]; var vv = _mvV[c];
                    for (int q = 0; q < gg.Length; q++) s += p[pm + gg[q]] * vv[q];
                    Rm[k * NC + c] = s;
                }
            }
            double inert = inp.Inert;
            for (int b = 0; b < x.NB; b++)
            {
                int a = x.BStart[b], e = x.BStart[b + 1];
                if (e <= a) continue;
                double umax = 0;
                for (int k = a; k < e; k++)
                {
                    int c = x.Pc[k];
                    double inc = x.Lp[k] * Rm[x.Pk[k] * NC + c] - KsTab.KCost[c] - rent[b] * KsTab.HaPerWorker[c];
                    if (inc < 0.02) inc = 0.02;
                    double u = x.St[k] > 0 ? inc * inert : inc;
                    xs[k] = u;
                    if (u > umax) umax = u;
                }
                double sum = 0, inv = 1.0 / umax;
                for (int k = a; k < e; k++) { double r = xs[k] * inv, r2 = r * r, r4 = r2 * r2, v = r4 * r4 * r2; xs[k] = v; sum += v; }
                double isum = 1.0 / sum;
                for (int k = a; k < e; k++) xs[k] *= isum;
            }
        }

        [ThreadStatic] private static int[][] _mvG; [ThreadStatic] private static double[][] _mvV; [ThreadStatic] private static double[] _mvRef, _rm;

        private static void SupplyK(Ctx x, double[] xs, double[] S, double[] Svil, Dictionary<string, int> dck)
        {
            var loc = x.In.KIn.Loc;
            Array.Clear(S, 0, S.Length); Array.Clear(Svil, 0, Svil.Length);
            int NG = KsTab.NG;
            for (int k = 0; k < x.NP; k++)
            {
                double w = xs[k] * x.BlockScale[x.Pb[k]];
                if (w == 0) continue;
                int mo = x.Pk[k] * NG; bool vil = !x.BD[x.Pb[k]].TownFields;
                for (int j = x.OStart[k]; j < x.OStart[k + 1]; j++) { double v = w * x.OVal[j]; S[mo + x.OGood[j]] += v; if (vil) Svil[mo + x.OGood[j]] += v; }
            }
            foreach (var kv in x.RegByDist)
            {
                int ck; if (!dck.TryGetValue(kv.Key, out ck) || ck < 0) continue;
                for (int g = 0; g < NG; g++) { if (!loc[g] || KsTab.RegGoodMask[g]) continue; S[ck * NG + g] += kv.Value[g]; Svil[ck * NG + g] += kv.Value[g]; }
            }
        }

        internal static readonly long[] Prof = new long[6];

        private static void PairsK(Ctx x)
        {
            if (x.Pk == null || x.Pk.Length != x.NP) x.Pk = new int[x.NP];
            for (int k = 0; k < x.NP; k++) x.Pk[k] = Math.Max(0, x.BD[x.Pb[k]].Ck);
        }

        private static void SolveK(Ctx x, Func<bool> cancel)
        {
            var KI = x.In.KIn; var R = x.R; int K = KI.K, NG = KsTab.NG;
            var O = new KOut(); R.KOut = O;
            PairsK(x);
            var st = new KSt { Dck = new Dictionary<string, int>(StringComparer.Ordinal), LocG = new List<int>(), Per = new bool[NG] };
            foreach (var d in x.D) st.Dck[d.Id] = d.Ck;
            for (int g = 0; g < NG; g++) if (KI.Loc[g] && !KsTab.RegGoodMask[g]) st.LocG.Add(g);
            foreach (var id in Perish) { int g; if (KsTab.GoodIndex.TryGetValue(id, out g)) st.Per[g] = true; }
            st.Dk = DemandK(x, KI);
            int n = K * NG;
            st.S = new double[n]; st.Svil = new double[n]; st.Snet = new double[n]; st.Sw = new double[2 * NG]; st.F = new double[KI.NE * NG];
            if (!KI.Mode1)
            {
                double[] xs2, S2; int r2;
                var p2 = Equilibrium(x, null, x.In.RoundsFirst, out xs2, out S2, out r2, cancel);
                O.P2 = p2; O.Rounds = r2; O.Conv = x.R.Open.Count == 0;
                O.Open.AddRange(x.R.Open); O.Bounds.AddRange(x.R.Bounds);
                SupplyK(x, xs2, st.S, st.Svil, st.Dck);
                var p = new double[n];
                for (int k = 0; k < K; k++) for (int g = 0; g < NG; g++) p[k * NG + g] = p2[KI.Mk[k] * NG + g];
                O.P = p; O.S = st.S; O.Snet = (double[])st.S.Clone(); O.D = st.Dk; O.Svil = st.Svil; O.F = st.F;
                O.Dw = (double[])x.Dm.Clone(); O.Sw = S2;
                O.HandsCraft = Hands(x, xs2, K);
                return;
            }
            st.P = new double[n]; st.Pw = new double[2 * NG]; st.Xs = new double[x.NP];
            for (int m = 0; m < 2; m++) for (int g = 0; g < NG; g++) st.Pw[m * NG + g] = KI.WarmP2 != null ? KI.WarmP2[m * NG + g] : 1.0;
            for (int k = 0; k < K; k++) for (int g = 0; g < NG; g++) st.P[k * NG + g] = st.Pw[KI.Mk[k] * NG + g];
            EquilibriumK(x, st, x.In.RoundsFirst, cancel, O);
            if (KI.Money) MoneyStepK(x, st, cancel, O);
            O.P = st.P; O.S = st.S; O.Snet = st.Snet; O.D = st.Dk; O.Svil = st.Svil; O.F = st.F; O.Dw = (double[])x.Dm.Clone();
            O.Sw = (double[])st.Sw.Clone(); for (int i = 0; i < 2 * NG; i++) O.Sw[i] += x.Sreg[i];
            O.HandsCraft = Hands(x, st.Xs, K);
            O.P2 = st.Pw;
        }

        private static void EquilibriumK(Ctx x, KSt st, int rounds, Func<bool> cancel, KOut O)
        {
            var KI = x.In.KIn; int K = KI.K, NG = KsTab.NG, n = K * NG;
            var p = st.P; var pw = st.Pw; var xs = st.Xs; var S = st.S; var Svil = st.Svil; var Snet = st.Snet; var Sw = st.Sw; var F = st.F; var Dk = st.Dk; var locG = st.LocG; var perish = st.Per;
            var rr = (double[])x.Rent.Clone();
            var dem = new double[x.NB];
            var outS = new double[n];
            var actL = new bool[n]; var actW = new bool[2 * NG];
            for (int k = 0; k < K; k++) for (int g = 0; g < NG; g++) actL[k * NG + g] = KI.Loc[g] && !KsTab.RegGoodMask[g] && Dk[k * NG + g] > 1.0;
            for (int m = 0; m < 2; m++) for (int g = 0; g < NG; g++) actW[m * NG + g] = !KI.Loc[g] && !KsTab.RegGoodMask[g] && x.Dm[m * NG + g] > 1.0;
            double bestMt = double.MaxValue; int bestImp = 0; double[] bp = null, bpw = null, bxs = null, bF = null, brr = null; bool conv = false; int it;
            for (it = 0; it < rounds; it++)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                SharesK(x, p, rr, xs, K);
                long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                SupplyK(x, xs, S, Svil, st.Dck);
                long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
                Prof[0] += t1 - t0; Prof[1] += t2 - t1;
                Array.Clear(dem, 0, dem.Length);
                for (int k = 0; k < x.NP; k++) dem[x.Pb[k]] += xs[k] * x.LandP[k];
                for (int b = 0; b < x.NB; b++) { double v = rr[b] + 0.002 * (dem[b] / x.Area[b] - 1.0); rr[b] = v < 0 ? 0 : (v > 0.5 ? 0.5 : v); }
                long t3 = System.Diagnostics.Stopwatch.GetTimestamp();
                Flows(KI, st, outS, p);
                long t4 = System.Diagnostics.Stopwatch.GetTimestamp(); Prof[2] += t4 - t3; Prof[4] += t3 - t2;
                Array.Clear(Sw, 0, Sw.Length);
                for (int k = 0; k < K; k++) { int m = KI.Mk[k]; for (int g = 0; g < NG; g++) Sw[m * NG + g] += S[k * NG + g]; }
                for (int k = 0; k < K; k++)
                    foreach (int g in locG)
                    {
                        int i = k * NG + g;
                        double r = (Dk[i] + 1.0) / (Math.Max(0.0, Snet[i]) + 1.0);
                        double v = p[i] * Math.Pow(r, Eta);
                        p[i] = v < PLo ? PLo : (v > PHi ? PHi : v);
                    }
                for (int m = 0; m < 2; m++)
                    for (int g = 0; g < NG; g++)
                    {
                        if (KI.Loc[g] && !KsTab.RegGoodMask[g]) continue;
                        int i = m * NG + g;
                        if (KsTab.RegGoodMask[g]) { pw[i] = 1.0; continue; }
                        double r = (x.Dm[i] + 1.0) / (Sw[i] + x.Sreg[i] + 1.0);
                        double v = pw[i] * Math.Pow(r, Eta);
                        pw[i] = v < PLo ? PLo : (v > PHi ? PHi : v);
                    }
                for (int m = 0; m < 2; m++)
                {
                    double ws = 0, wl = 0;
                    for (int g = 0; g < NG; g++) { int i = m * NG + g; if (!actW[i]) continue; double w = x.Dm[i] * x.Val[g]; ws += w; wl += w * Math.Log(pw[i]); }
                    for (int k = 0; k < K; k++) { if (KI.Mk[k] != m) continue; foreach (int g in locG) { int i = k * NG + g; if (!actL[i]) continue; double w = Dk[i] * x.Val[g]; ws += w; wl += w * Math.Log(p[i]); } }
                    if (ws > 0)
                    {
                        double f = Math.Exp(wl / ws);
                        for (int g = 0; g < NG; g++) if (!KsTab.RegGoodMask[g]) pw[m * NG + g] /= f;
                        for (int k = 0; k < K; k++) if (KI.Mk[k] == m) foreach (int g in locG) p[k * NG + g] /= f;
                    }
                }
                for (int k = 0; k < K; k++) { int m = KI.Mk[k]; for (int g = 0; g < NG; g++) if (!(KI.Loc[g] && !KsTab.RegGoodMask[g])) p[k * NG + g] = KsTab.RegGoodMask[g] ? 1.0 : pw[m * NG + g]; }
                Prof[3] += System.Diagnostics.Stopwatch.GetTimestamp() - t4;
                if (it % 50 == 49)
                {
                    bool bad = false; double mt = 0;
                    for (int i = 0; i < n; i++)
                    {
                        if (!actL[i]) continue;
                        double dev = Math.Abs(Snet[i] / Math.Max(1.0, Dk[i]) - 1.0);
                        bool atb = p[i] <= PLo * 1.001 || p[i] >= PHi * 0.999;
                        if (dev > Tol && !atb && Dk[i] > 1000) bad = true;
                        if (!atb) mt += dev * Math.Min(1.0, Dk[i] / 1000.0);
                    }
                    for (int i = 0; i < 2 * NG; i++)
                    {
                        if (!actW[i]) continue;
                        double dev = Math.Abs((Sw[i] + x.Sreg[i]) / Math.Max(1.0, x.Dm[i]) - 1.0);
                        bool atb = pw[i] <= PLo * 1.001 || pw[i] >= PHi * 0.999;
                        if (dev > Tol && !atb) bad = true;
                        if (!atb) mt += dev * 10;
                    }
                    if (!bad) { conv = true; break; }
                    if (mt < bestMt * 0.995) bestImp = it;
                    if (mt < bestMt)
                    {
                        bestMt = mt;
                        if (bp == null) { bp = new double[n]; bpw = new double[2 * NG]; bxs = new double[x.NP]; bF = new double[F.Length]; brr = new double[x.NB]; }
                        Array.Copy(p, bp, n); Array.Copy(pw, bpw, pw.Length); Array.Copy(xs, bxs, x.NP); Array.Copy(F, bF, F.Length); Array.Copy(rr, brr, x.NB);
                    }
                    if (x.In.StallExit && it - bestImp >= 2000) break;
                    if (cancel != null && cancel()) break;
                }
            }
            if (!conv && bp != null)
            {
                Array.Copy(bp, p, n); Array.Copy(bpw, pw, pw.Length); Array.Copy(bxs, xs, x.NP); Array.Copy(bF, F, F.Length); Array.Copy(brr, rr, x.NB);
                SupplyK(x, xs, S, Svil, st.Dck);
                Array.Copy(S, Snet, n);
                for (int e = 0; e < KI.NE; e++) { int i = KI.CI[e], j = KI.CJ[e]; foreach (int g in locG) { double f = F[e * NG + g]; Snet[i * NG + g] -= f; Snet[j * NG + g] += f; } }
                Array.Clear(Sw, 0, Sw.Length);
                for (int k = 0; k < K; k++) { int m = KI.Mk[k]; for (int g = 0; g < NG; g++) Sw[m * NG + g] += S[k * NG + g]; }
            }
            x.Rent = rr;
            st.Rounds += Math.Min(rounds, it + 1); st.Conv = conv;
            O.Rounds = st.Rounds; O.Conv = conv; O.Open.Clear(); O.Bounds.Clear();
            for (int i = 0; i < n; i++)
            {
                if (!actL[i]) continue;
                double dev = Math.Abs(Snet[i] / Math.Max(1.0, Dk[i]) - 1.0);
                bool atb = p[i] <= PLo * 1.001 || p[i] >= PHi * 0.999;
                string nm = KI.Name[i / NG] + " " + KsTab.Goods[i % NG];
                if (atb) O.Bounds.Add(nm + " " + p[i].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                else if (dev > Tol && Dk[i] > 1000) O.Open.Add(nm + " " + (Snet[i] / Math.Max(1.0, Dk[i])).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        private static void Flows(KInp KI, KSt st, double[] outS, double[] p)
        {
            int NG = KsTab.NG; var S = st.S; var Svil = st.Svil; var Snet = st.Snet; var F = st.F; var Dk = st.Dk;
            Array.Clear(outS, 0, outS.Length);
            for (int e = 0; e < KI.NE; e++)
            {
                int i = KI.CI[e], j = KI.CJ[e], t = KI.CT[e];
                foreach (int g in st.LocG)
                {
                    int fi = e * NG + g;
                    double pi = p[i * NG + g], pj = p[j * NG + g], thr, cap;
                    if (t == 0) { thr = KI.CR[e] * pi; cap = KI.CA[e] * Svil[i * NG + g]; }
                    else
                    {
                        if (KI.CapLong == 0 || KI.CDays[e] > (st.Per[g] ? KI.PerishDays : KI.DurDays)) { F[fi] = 0; continue; }
                        thr = pi * (1 + KI.Margin0) + KI.CostPer100 * KI.CDist[e] / 100.0;
                        cap = Math.Min(Dk[j * NG + g], S[i * NG + g]);
                        if (KI.CapLong > 0) cap = Math.Min(cap, KI.CapLong);
                        if (KI.CapShare > 0) cap = Math.Min(cap, KI.CapShare * Dk[j * NG + g]);
                    }
                    double z = (pj - thr) / (KI.Theta * pi);
                    double tg = cap * (z <= 0 ? 0 : (z >= 1 ? 1 : z));
                    F[fi] += KI.FDamp * (tg - F[fi]);
                    outS[i * NG + g] += F[fi];
                }
            }
            Array.Copy(S, Snet, S.Length);
            for (int e = 0; e < KI.NE; e++)
            {
                int i = KI.CI[e], j = KI.CJ[e];
                foreach (int g in st.LocG)
                {
                    int fi = e * NG + g; double f = F[fi]; if (f == 0) continue;
                    double o = outS[i * NG + g], s = S[i * NG + g];
                    if (o > s && o > 0) { f *= s / o; F[fi] = f; }
                    Snet[i * NG + g] -= f; Snet[j * NG + g] += f;
                }
            }
        }

        private static double[] Hands(Ctx x, double[] xs, int K)
        {
            var h = new double[K * KsTab.NC];
            for (int k = 0; k < x.NP; k++)
            {
                int b = x.Pb[k]; double W = x.W[b] - x.WH[b];
                h[x.Pk[k] * KsTab.NC + x.Pc[k]] += xs[k] * W * x.BlockScale[b];
            }
            return h;
        }

        /// <summary>Indeks ceny startu na targ k (ta sama regula co StartIndex, cena cienia targu).</summary>
        internal static double StartIndexK(string id, double[] P, int k)
        {
            if (id == null || PriceOne.Contains(id)) return 1.0;
            int g;
            if (!KsTab.GoodIndex.TryGetValue(id == "WhaleMeat" ? "meat" : id, out g) || KsTab.RegGoodMask[g]) return 1.0;
            double p = P[k * KsTab.NG + g];
            if (!(p > 0) || double.IsNaN(p) || double.IsInfinity(p)) p = 1.0;
            p = Math.Max(0.7, Math.Min(1.5, p));
            if (Perish.Contains(id)) p = Math.Max(1.0, p);
            return p;
        }

        internal static double ShadowIndexK(string id, double[] P, int k)
        {
            int g;
            if (id == null || !KsTab.GoodIndex.TryGetValue(id == "WhaleMeat" ? "meat" : id, out g) || KsTab.RegGoodMask[g]) return 1.0;
            double p = P[k * KsTab.NG + g];
            if (!(p > 0) || double.IsNaN(p) || double.IsInfinity(p)) return 1.0;
            return p >= PHi * 0.99 ? 1.0 : p;
        }

        private sealed class MDistK { public KsDistrict D; public KsPart[] Parts; public double HwV, V0, G, SurL, SurW, Alpha, Ret; public double[] RetI = new double[KsDeliver.ItemCount]; public bool[] Ok; public int Sub = -1; }

        /// <summary>Krok 8 na targ (port MoneyStep): ceny drabiny i rachunek rak pracy domowej w cenach targu wsi; nadwyzka towarow targu - poprawka G w targu,
        /// towarow rynku - w rynku. Opcja Subst: kcal niezaplacone spoza HomeMask robione z wlasnego jedzenia (najtanszy wlasny towar z kcal).</summary>
        private static void MoneyStepK(Ctx x, KSt st, Func<bool> cancel, KOut O)
        {
            var inp = x.In; var R = x.R; var KI = inp.KIn; int K = KI.K, NG = KsTab.NG;
            var M = new KsMoney { On = true };
            R.Money = M;
            foreach (var d in x.D) d.NeedR8 = (double[])d.Need.Clone();
            foreach (var t in inp.Towns) t.NeedR8 = (double[])t.Need.Clone();
            var vs = new List<MDistK>();
            foreach (var d in x.D)
            {
                if (d.TownFields || d.PplHome == null || d.Bask == null || d.Blocks.Count == 0 || d.Ck < 0) continue;
                var parts = new KsPart[d.Blocks.Count];
                for (int i = 0; i < parts.Length; i++) parts[i] = new KsPart { K = d.Blocks[i] };
                int m0 = KI.Mk[d.Ck];
                bool meas = d.Alpha >= 0 && !double.IsNaN(d.Alpha) && !double.IsInfinity(d.Alpha);
                double a0 = Math.Max(0.0, Math.Min(0.95, meas ? d.Alpha : inp.VilAlpha[m0]));
                vs.Add(new MDistK { D = d, Parts = parts, HwV = HomeOf(d), Ok = HomeMask(d), Alpha = a0 });
            }
            int NZ = KsBasket.N, NI = KsDeliver.ItemCount;
            var view = new KsResult();
            var prod = new double[NI]; var prod2 = new double[NI]; var own = new double[NI];
            var q = new double[NZ]; var paid = new double[NZ]; var u = new double[NZ];
            var keep = new double[KsTab.NN]; var full = new double[KsTab.NN];
            var pz = new double[K * NZ]; var pi = new double[K * NI]; var bi = new double[K * NI]; var sz = new double[K * NZ]; var si = new double[K * NI];
            var pH = new double[K]; var sH = new double[K];
            double bH = KsTab.HorseVal / Math.Max(1.0, inp.Sigma);
            var surL = new bool[K * NG]; var surW = new bool[2 * NG];
            var eat = new double[K * NG]; var eatPrev = new double[K * NG];
            double margin = inp.MarketMargin > 1.0 && inp.MarketMargin <= 1.10 ? inp.MarketMargin : 1.0;
            var excK = new double[K]; var excW = new double[2];
            var tq = new double[3][]; for (int c = 0; c < 3; c++) tq[c] = new double[NZ];
            var tqs = new double[NZ]; var tus = new double[NZ];
            int[] live = { KsDeliver.ICow, KsDeliver.IHog, KsDeliver.ISheep }; int[] liveG = { KsTab.GCow, KsTab.GHog, KsTab.GSheep };
            for (int it = 0; it <= inp.MoneyIter; it++)
            {
                bool last = it == inp.MoneyIter;
                for (int k = 0; k < K; k++)
                {
                    for (int z = 0; z < NZ; z++) pz[k * NZ + z] = ZlPerKg(inp, KsBasket.Ids[z], StartIndexK(KsBasket.Ids[z], st.P, k));
                    for (int i = 0; i < NI; i++) { pi[k * NI + i] = ZlPerKg(inp, KsDeliver.ItemIds[i], StartIndexK(KsDeliver.ItemIds[i], st.P, k)); bi[k * NI + i] = ZlPerKg(inp, KsDeliver.ItemIds[i], 1.0); }
                    for (int z = 0; z < NZ; z++) sz[k * NZ + z] = ZlPerKg(inp, KsBasket.Ids[z], ShadowIndexK(KsBasket.Ids[z], st.P, k));
                    for (int i = 0; i < NI; i++) si[k * NI + i] = ZlPerKg(inp, KsDeliver.ItemIds[i], ShadowIndexK(KsDeliver.ItemIds[i], st.P, k));
                    for (int j = 0; j < 3; j++) si[k * NI + live[j]] = KsTab.MeatOfLive * si[k * NI + KsDeliver.IMeat] + KsTab.HideOfLive(liveG[j]) * si[k * NI + KsDeliver.IHides];
                    pH[k] = bH * StartIndexK("horse", st.P, k); sH[k] = bH * ShadowIndexK("horse", st.P, k);
                }
                double maxDG = 0;
                if (it > 0)
                {
                    // nadwyzka targu (towary targu, po korytarzach) i rynku (towary rynku) w cenach cienia
                    for (int k = 0; k < K; k++)
                    {
                        double e = 0;
                        foreach (int g in st.LocG)
                        {
                            int i = k * NG + g; surL[i] = false;
                            if (ExcessSkip(g) || !(st.Dk[i] > 1.0)) continue;
                            if (st.P[i] >= PHi * 0.99 || st.P[i] <= PLo * 1.01) continue;   // brak / nadmiar strukturalny (cena na granicy) - nie jest praca do zdjecia ani oddania
                            double pk = ZlPerKg(inp, KsTab.Goods[g], KI.ExcStart ? StartIndexK(KsTab.Goods[g], st.P, k) : ShadowIndexK(KsTab.Goods[g], st.P, k));
                            double dmk = Math.Max(0.0, st.Dk[i] - eatPrev[i]), dk = st.Dk[i] + (margin - 1.0) * dmk;
                            e += (st.Snet[i] - dk) * pk; surL[i] = st.Snet[i] > dk;
                        }
                        excK[k] = e;
                    }
                    var eatW = new double[2 * NG]; for (int k = 0; k < K; k++) for (int g = 0; g < NG; g++) eatW[KI.Mk[k] * NG + g] += eatPrev[k * NG + g];
                    for (int m = 0; m < 2; m++)
                    {
                        double e = 0;
                        for (int g = 0; g < NG; g++)
                        {
                            int i = m * NG + g; surW[i] = false;
                            if (KI.Loc[g] || KsTab.RegGoodMask[g] || g == KsTab.GHorse || ExcessSkip(g) || !(x.Dm[i] > 1.0)) continue;
                            double pk = ZlPerKg(inp, KsTab.Goods[g], ShadowIndexK(KsTab.Goods[g], st.P, FirstOf(KI, m)));
                            double dmk = Math.Max(0.0, x.Dm[i] - eatW[i]), dk = x.Dm[i] + (margin - 1.0) * dmk;
                            double sw = st.Sw[i] + x.Sreg[i];
                            e += (sw - dk) * pk; surW[i] = sw > dk;
                        }
                        excW[m] = e; M.Excess[m] = e;
                    }
                    var sumSL = new double[K]; var sumHvK = new double[K]; var sumSW = new double[2]; var sumHvM = new double[2];
                    foreach (var v in vs) { int k = v.D.Ck, m = KI.Mk[k]; sumSL[k] += v.SurL; sumSW[m] += v.SurW; double h = HomeOf(v.D) * v.V0; sumHvK[k] += h; sumHvM[m] += h; }
                    var excKm = new double[2]; var sumSLm = new double[2];
                    for (int k = 0; k < K; k++) { excKm[KI.Mk[k]] += excK[k]; sumSLm[KI.Mk[k]] += sumSL[k]; }
                    M.ExcessRel[0] = excKm[0]; M.ExcessRel[1] = excKm[1];
                    foreach (var v in vs)
                    {
                        int k = v.D.Ck, m = KI.Mk[k];
                        double dG = 0;
                        if (v.V0 > 0 && KI.GMarket)
                        {
                            double eT = excW[m] + excKm[m], sS = sumSW[m] + sumSLm[m];
                            if (eT > 0 && sS > 0) dG += 0.5 * eT * ((v.SurW + v.SurL) / sS) / v.V0;
                            else if (eT < 0 && sumHvM[m] > 0) dG += 0.5 * eT * HomeOf(v.D) / sumHvM[m];
                        }
                        else if (v.V0 > 0)
                        {
                            double eL = excK[k];
                            if (eL > 0 && sumSL[k] > 0) dG += 0.5 * eL * (v.SurL / sumSL[k]) / v.V0;
                            else if (eL < 0 && sumHvK[k] > 0) dG += 0.5 * eL * HomeOf(v.D) / sumHvK[k];
                            double eW = excW[m];
                            if (eW > 0 && sumSW[m] > 0) dG += 0.5 * eW * (v.SurW / sumSW[m]) / v.V0;
                            else if (eW < 0 && sumHvM[m] > 0) dG += 0.5 * eW * HomeOf(v.D) / sumHvM[m];
                        }
                        double g0 = v.G;
                        v.G = Math.Max(-v.HwV, Math.Min(inp.HomeMax - v.HwV, v.G + dG));
                        if (Math.Abs(v.G - g0) > maxDG) maxDG = Math.Abs(v.G - g0);
                    }
                    M.DG = maxDG;
                }
                Array.Clear(M.VSales, 0, 2); Array.Clear(M.VSalesB, 0, 2); Array.Clear(M.VBudget, 0, 2); Array.Clear(M.VBasket, 0, 2); Array.Clear(M.VPaid, 0, 2); Array.Clear(M.VHome, 0, 2);
                Array.Clear(M.VHands, 0, 2); Array.Clear(M.VHomeHands, 0, 2); Array.Clear(M.VShort, 0, 2); Array.Clear(M.VN, 0, 2); Array.Clear(M.VHorse, 0, 2);
                Array.Clear(M.TBudget, 0, M.TBudget.Length); Array.Clear(M.TBasket, 0, M.TBasket.Length); Array.Clear(M.TPaid, 0, M.TPaid.Length);
                Array.Clear(M.VFullKcal, 0, 2); Array.Clear(M.VUnpaidKcal, 0, 2); Array.Clear(M.VHomeKcal, 0, 2); Array.Clear(M.VUnmetRaw, 0, 2); Array.Clear(O.VSubKcal, 0, 2); Array.Clear(O.VRetKcal, 0, 2);
                Array.Clear(eat, 0, eat.Length);
                view.BlockStart = x.BStart; view.X = st.Xs; view.PairCraft = x.Pc; view.BlockW = x.W; view.RegHands = x.RegHands; view.Rent = x.Rent;
                foreach (var v in vs)
                {
                    var d = v.D; int k = d.Ck, m = KI.Mk[k];
                    for (int j = 0; j < v.Parts.Length; j++) KsDeliver.FromPlan(view, d.Blocks[j].Index, v.Parts[j]);
                    KsDeliver.Produce(d, v.Parts, prod);
                    double hk = KsDeliver.HorseOut(d, v.Parts);
                    double made = hk * sH[k];
                    for (int i = 0; i < NI; i++) made += prod[i] * si[k * NI + i];
                    double home = 0; foreach (var e in d.PplHome) home += Math.Max(0.0, e);
                    double hr = d.People > 0 ? Math.Min(1.0, home / d.People) : 0.0;
                    NeedsOfKeep(d.PplHome, d.Bask, inp.Keep, d.Oil, home, keep);
                    for (int kk = 0; kk < KsTab.NN; kk++)
                    {
                        double mult = d.Need0[kk] > 1e-9 ? Math.Max(0.25, Math.Min(4.0, d.NeedR8[kk] / d.Need0[kk])) : 1.0;
                        keep[kk] *= mult; full[kk] = d.NeedR8[kk] * hr;
                    }
                    KsDeliver.Own(prod, keep, own);
                    for (int i = 0; i < NI; i++)
                    {
                        int gk = KsDeliver.ItemGood[i];
                        if (gk < 0 || !(own[i] > 0)) continue;
                        eat[k * NG + gk] += own[i] * (i == KsDeliver.ICow || i == KsDeliver.IHog || i == KsDeliver.ISheep ? KsTab.MeatOfLive : 1.0);
                    }
                    Array.Clear(q, 0, NZ);
                    KsBasket.VillageKg(full, keep, own, q);
                    double sales = hk * pH[k], salesB = hk * bH;
                    for (int i = 0; i < NI; i++) { double mk = prod[i] - own[i]; if (mk > 0) { sales += mk * pi[k * NI + i]; salesB += mk * bi[k * NI + i]; } }
                    double bask = 0; for (int z = 0; z < NZ; z++) bask += q[z] * pz[k * NZ + z];
                    for (int i = 0; i < NI; i++) if (v.RetI[i] > 0) { salesB = Math.Max(0.0, salesB - v.RetI[i] * bi[k * NI + i]); sales = Math.Max(0.0, sales - v.RetI[i] * pi[k * NI + i]); }
                    double budget = v.Alpha * salesB;
                    double pv = KsBasket.Ladder(q, pz, k * NZ, budget, paid);
                    double hwc = HomeOf(d);
                    double hw2 = Math.Min(0.95, hwc + 0.5);
                    d.HomeWork = hw2;
                    KsDeliver.Produce(d, v.Parts, prod2);
                    d.HomeWork = hwc;
                    double made2 = hk * sH[k], svL = 0, svW = 0;
                    for (int i = 0; i < NI; i++)
                    {
                        double vi = prod2[i] * si[k * NI + i];
                        made2 += vi;
                        int gk = KsDeliver.ItemGood[i];
                        if (gk < 0) continue;
                        double dv = prod[i] * si[k * NI + i] - vi;
                        if (KI.Loc[gk] && !KsTab.RegGoodMask[gk]) { if (surL[k * NG + gk]) svL += dv; }
                        else if (surW[m * NG + gk]) svW += dv;
                    }
                    double v0 = hw2 > hwc ? Math.Max(0.0, made - made2) / (hw2 - hwc) : made, raw = 0, rawNo = 0;
                    v.SurL = hw2 > hwc ? Math.Max(0.0, svL) / (hw2 - hwc) : 0.0;
                    v.SurW = hw2 > hwc ? Math.Max(0.0, svW) / (hw2 - hwc) : 0.0;
                    // zamiennik kcal (Subst): najtanszy wlasny towar z kcal (HomeMask) w cenach cienia za 1000 kcal
                    int subZ = -1; double subCost = double.MaxValue;
                    if (KI.Subst)
                        for (int z = 0; z < NZ; z++)
                        {
                            if (!v.Ok[z] || !(KsBasket.Kcal[z] > 0)) continue;
                            if (z == KsBasket.Beer || z == KsBasket.Wine || z == KsBasket.Mead || z == KsBasket.Oil) continue;
                            double c = RawZ(sz, k * NZ, z) / KsBasket.Kcal[z];
                            if (c > 0 && c < subCost) { subCost = c; subZ = z; }
                        }
                    double subK = 0;
                    for (int z = 0; z < NZ; z++)
                    {
                        u[z] = (1.0 - paid[z]) * q[z];
                        if (!(u[z] > 0)) continue;
                        double r = u[z] * RawZ(sz, k * NZ, z);
                        if (v.Ok[z]) raw += r;
                        else if (subZ >= 0 && KsBasket.Kcal[z] > 0) { double kc = u[z] * KsBasket.Kcal[z]; raw += kc * subCost; subK += kc; }
                        else rawNo += r;
                    }
                    v.V0 = v0;
                    // Retain: kcal bez pokrycia (po zakupach z budzetu i pracy domowej) - z wlasnego ziarna, ktore szloby na targ (nie sprzedaje, je); najwyzej cale ziarno na targ
                    if (KI.Retain && !last)
                    {
                        double covNow = raw > 0 ? Math.Min(1.0, hwc * v0 / raw) : 0.0, ukc = 0, okc = 0;
                        for (int z = 0; z < NZ; z++) { double kc = KsBasket.Kcal[z]; if (!(kc > 0)) continue; ukc += u[z] * kc; if (v.Ok[z]) okc += u[z] * kc; }
                        double unc = Math.Max(0.0, ukc - covNow * (okc + subK));
                        // najtansze za kcal (ceny cienia targu) wlasne jedzenie, ktore idzie na targ
                        var cand = new List<KeyValuePair<double, int>>();
                        for (int i = 0; i < NI; i++) { double kc = KsDeliver.ItemKcal[i]; if (!(kc > 0)) continue; double av = prod[i] - own[i]; if (av <= 1e-9) continue; cand.Add(new KeyValuePair<double, int>(si[k * NI + i] / kc, i)); }
                        cand.Sort((a1, b1) => a1.Key.CompareTo(b1.Key));
                        var nr = new double[NI];
                        foreach (var c1 in cand)
                        {
                            if (unc <= 0) break;
                            int i = c1.Value; double av = Math.Max(0.0, prod[i] - own[i]), kc = KsDeliver.ItemKcal[i];
                            double tk = Math.Min(av, unc / kc); nr[i] = tk; unc -= tk * kc;
                        }
                        for (int i = 0; i < NI; i++) v.RetI[i] = 0.5 * (v.RetI[i] + nr[i]);
                    }
                    v.Ret = 0;
                    for (int i = 0; i < NI; i++)
                    {
                        if (!(v.RetI[i] > 0)) continue;
                        v.Ret += v.RetI[i] * KsDeliver.ItemKcal[i] / 3300.0;
                        int gk = KsDeliver.ItemGood[i]; if (gk >= 0) eat[k * NG + gk] += v.RetI[i] * (i == KsDeliver.ICow || i == KsDeliver.IHog || i == KsDeliver.ISheep ? KsTab.MeatOfLive : 1.0);
                    }
                    double target = v0 > 0 ? Math.Max(0.0, Math.Min(inp.HomeMax, raw / v0)) : 0.0;
                    if (!last) v.HwV += 0.5 * (target - v.HwV);
                    M.VSales[m] += sales; M.VSalesB[m] += salesB; M.VHorse[m] += hk * bH; M.VBudget[m] += budget; M.VBasket[m] += bask; M.VPaid[m] += pv; M.VHome[m] += Math.Max(0.0, bask - pv); M.VN[m]++;
                    if (bask > budget * 1.001 + 1e-6) M.VShort[m]++;
                    M.VHands[m] += d.Wf; M.VHomeHands[m] += d.Wf * hwc; M.VUnmetRaw[m] += rawNo;
                    if (last)
                    {
                        var fp = new float[NZ]; for (int z = 0; z < NZ; z++) fp[z] = (float)paid[z];
                        R.DistPaid[d.Id] = fp; R.DistHome[d.Id] = hwc;
                        double cov = raw > 0 ? Math.Min(1.0, hwc * v0 / raw) : 0.0, hkc = 0, fk = 0, uk = 0;
                        for (int z = 0; z < NZ; z++)
                        {
                            double kc = KsBasket.Kcal[z]; if (!(kc > 0)) continue;
                            fk += q[z] * kc; uk += u[z] * kc;
                            if (v.Ok[z]) hkc += u[z] * kc;
                        }
                        hkc = (hkc + subK) * cov + v.Ret * 3300.0;
                        O.VRetKcal[m] += v.Ret * 3300.0;
                        R.DistHomeKcal[d.Id] = fk > 0 ? (float)Math.Max(0.0, Math.Min(1.0, hkc / fk)) : 0f;
                        M.VFullKcal[m] += fk; M.VUnpaidKcal[m] += uk; M.VHomeKcal[m] += hkc; O.VSubKcal[m] += subK * cov;
                    }
                    else
                    {
                        Array.Copy(d.NeedR8, d.Need, KsTab.NN);
                        UnpaidToNeed(d.Need, u, false);
                        for (int i = 0; i < NI; i++)
                        {
                            double r1 = v.RetI[i]; if (!(r1 > 0)) continue;
                            int key = NeedKeyOfItem(i); if (key < 0) continue;
                            d.Need[key] += r1 * (i == KsDeliver.ICow || i == KsDeliver.IHog || i == KsDeliver.ISheep ? KsTab.MeatOfLive : 1.0);
                        }
                    }
                }
                Array.Copy(eat, eatPrev, eat.Length);
                foreach (var t in inp.Towns)
                {
                    if (t.Ck < 0 || t.Budget == null || t.ClassNeed == null || t.Budget.Length < 3 || t.ClassNeed.Length < 3) continue;
                    int k = t.Ck, m = KI.Mk[k];
                    Array.Clear(tqs, 0, NZ); Array.Clear(tus, 0, NZ);
                    for (int c = 0; c < 3; c++)
                    {
                        var qc = tq[c]; Array.Clear(qc, 0, NZ);
                        if (t.ClassNeed[c] == null) continue;
                        double fk; KsBasket.TownKg(t.ClassNeed[c], 0.0, qc, out fk);
                        double cost = 0; for (int z = 0; z < NZ; z++) cost += qc[z] * pz[k * NZ + z];
                        double pv = KsBasket.Ladder(qc, pz, k * NZ, Math.Max(0.0, t.Budget[c]), paid);
                        M.TBudget[m * 3 + c] += Math.Max(0.0, t.Budget[c]); M.TBasket[m * 3 + c] += cost; M.TPaid[m * 3 + c] += pv;
                        for (int z = 0; z < NZ; z++) { tqs[z] += qc[z]; tus[z] += (1.0 - paid[z]) * qc[z]; }
                    }
                    if (last)
                    {
                        var fp = new float[NZ]; for (int z = 0; z < NZ; z++) fp[z] = tqs[z] > 1e-12 ? (float)Math.Max(0.0, Math.Min(1.0, 1.0 - tus[z] / tqs[z])) : 1f;
                        R.TownPaid[t.Id] = fp;
                    }
                    else
                    {
                        Array.Copy(t.NeedR8, t.Need, KsTab.NN);
                        UnpaidToNeed(t.Need, tus, true);
                    }
                }
                if (last) break;
                double dh = 0;
                foreach (var v in vs)
                {
                    double hn = Math.Max(0.0, Math.Min(inp.HomeMax, v.HwV + v.G));
                    double dd = Math.Abs(hn - HomeOf(v.D)); if (dd > dh) dh = dd;
                    v.D.HomeWork = hn;
                }
                M.DHw = dh;
                M.Trace.Add("runda " + (it + 1) + ": wsie W/E sprzedaz " + Math.Round(M.VSalesB[0]) + "/" + Math.Round(M.VSalesB[1]) + ", koszyk " + Math.Round(M.VBasket[0]) + "/" + Math.Round(M.VBasket[1])
                            + ", zaplacone " + Math.Round(M.VPaid[0]) + "/" + Math.Round(M.VPaid[1]) + ", nadwyzka rynku (towary rynku) " + Math.Round(excW[0]) + "/" + Math.Round(excW[1]) + ", targow " + Math.Round(M.ExcessRel[0]) + "/" + Math.Round(M.ExcessRel[1])
                            + ", najwieksza zmiana pracy domowej " + dh.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) + " (G " + maxDG.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) + ")");
                Pairs(x);
                PairsK(x);
                Demand(x);
                st.Dk = DemandK(x, KI);
                RegaleStep(x);
                if (st.Xs.Length != x.NP) st.Xs = new double[x.NP];
                EquilibriumK(x, st, inp.RoundsR8b, cancel, O);
                M.Iter++;
                if (cancel != null && cancel()) return;
            }
            O.Eaten = (double[])eat.Clone();
        }

        private static int NeedKeyOfItem(int i)
        {
            switch (i)
            {
                case KsDeliver.IGrain: return KsTab.NG_; case KsDeliver.IFish: return KsTab.NFish; case KsDeliver.ICheese: return KsTab.NCheese; case KsDeliver.IButter: return KsTab.NButter;
                case KsDeliver.IEgg: return KsTab.NEgg; case KsDeliver.IApple: return KsTab.NApple; case KsDeliver.ICarrot: return KsTab.NCarrot; case KsDeliver.IOrange: return KsTab.NOrange;
                case KsDeliver.IGrape: return KsTab.NGrape; case KsDeliver.IOlives: return KsTab.NOlives; case KsDeliver.IDate: return KsTab.NDate; case KsDeliver.IHoney: return KsTab.NHoney;
                case KsDeliver.IMeat: case KsDeliver.IWhaleMeat: case KsDeliver.ICow: case KsDeliver.IHog: case KsDeliver.ISheep: return KsTab.NMeat;
                default: return -1;
            }
        }

        private static int FirstOf(KInp KI, int m) { for (int k = 0; k < KI.K; k++) if (KI.Mk[k] == m) return k; return 0; }
    }
}
