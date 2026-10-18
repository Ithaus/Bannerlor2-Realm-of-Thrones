using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// f5-koszt (noc 17/18.10, fala 5) - KOLCE DOBY KS z testu fin88 (Armoury-2026-10-18_03-13-38.log, pod-czasy T0 paczki ks-koszt): co 7 dob od D (doba kroku A7,
    /// u Jeffa ta sama doba co odbior Planu) regula_a7 brala ok. 16 ms, z tego ok. 12 ms diagnoza A7 paczki f4-pomiary (linia "KS: A7 diagnoza (f4-pomiary)": 15 tys.
    /// blokow x piec zestawow cen x kandydaci - kazdy dochod z osobnym wywolaniem IncomeV, czyli L zajecia z pamieci bloku i renta x ha od nowa w kazdym zestawie,
    /// do tego 4 sortowania list po 15 tys. liczb) i ok. 2 ms ten sam dochod przy cenach gry liczony drugi raz przez regule A7. NKS-9 q95 21.7 ms (prog 15; fin87 bez
    /// diagnozy 12.9 ms). Klucz Ks Cost Fast2 (domyslnie TAK; wylaczony = stary rachunek co do linii - F4A7Old):
    ///  - F1 diagnoza A7 na watku glownym tylko to, czego potrzebuje regula (F5Prep): przeglad blokow (te same filtry), L zajecia (b.L) i renta x ha (b.Rent x KsTab.Ha)
    ///    biezacego i kazdego kandydata RAZ na blok, zestaw G (ceny gry - ten sam, ktory liczy regula) z liczeniem i zapisem do pamieci bloku; reszta pomiaru
    ///    (zestawy B, T, S1, S2, kwantyle, napis) na watku z puli (F5Finish) z kopii liczb - zadnego obiektu gry poza watkiem glownym; linia pisana z watku (Log jest
    ///    pod blokada). Ta sama arytmetyka (L x v - K - renta x ha, ta sama kolejnosc dzialan, ten sam leniwy wiersz wartosci zajec na zestaw cen) - te same liczby co do
    ///    bitu; kwantyle q10/med/q90 przez wybor k-tego elementu (porzadek double.CompareTo - NaN najmniejsze, jak List.Sort); przejscia w tablicy z kolejnoscia
    ///    pierwszego wpisu (jak slownik Tr) - ten sam napis "top 10"; premia mapy S2 przez indeks zajecia startowego (CraftIndex - nazwy zajec sa rozne, porownanie
    ///    porzadkowe jak ==). Linia ta sama co do znaku poza liczba "czas" (czas = watek glowny + petla na watku, bez napisu - jak dotad petla bez napisu);
    ///    trafia do logu chwile pozniej (gdy watek skonczy; nowa sesja w trakcie - linia odrzucona). Poprzednia jeszcze liczona - ta liczona od razu na watku glownym;
    ///  - F2 regula A7 (RuleDaily w dobie kroku): dochod biezacy, najlepszy inny i jego zajecie przy cenach gry - z diagnozy tej doby (pamiec w bloku: doba, ten sam wiersz
    ///    cen miasta, to samo zajecie biezace; lista kandydatow ta sama i w tej samej kolejnosci), inaczej liczone jak dotad; probka 1/64 liczona starym rachunkiem i porownana;
    ///  - kontrola (raz na sesje, doba D+7k+3, poza czasem doby KS): stary rachunek diagnozy (F4A7Old) wobec szybkiego (F5Prep + F5Finish na miejscu, bez zapisu do bloku)
    ///    na tych samych danych - linia bez czasu ta sama.
    /// Linia "KS: koszt f5 dzien N" co 7 dob (D+7k+3, stan zastosowany, linie): czas ostatniej diagnozy na watku glownym i na watku, bloki reguly z dochodem z diagnozy,
    /// probka zgodne/rozne, bledy watku, kontrola. Zamknieta ekonomia: zadnej monety ani towaru - tylko mniej i gdzie indziej liczone te same liczby. Zapis: bez zmian.
    /// </summary>
    internal static partial class StartCalibration
    {
        /// <summary>f5-koszt: klucz Ks Cost Fast2 (domyslnie TAK) - szybka diagnoza A7 i dochody reguly A7 z niej (te same wyniki co do bitu); wylaczony = stary rachunek.</summary>
        internal static bool SwCostFast2 { get { var s = Settings.Current; return s == null || s.KsCostFast2; } }

        // liczniki do linii "KS: koszt f5" (od ostatniej linii) i kontrola raz na sesje
        private static int _f5RuleReuse, _f5RuleOk, _f5RuleBad, _f5RuleSample;
        private static double _f5DiagMs = -1, _f5WorkMs = -1; private static int _f5DiagDay = -1; private static bool _f5DiagFast;
        private static bool _f5Checked; private static string _f5Check = "jeszcze nie (pierwsza doba D+7k+3 po diagnozie)";
        // watek z puli: pokolenie sesji (linia z poprzedniej sesji odrzucona), praca w toku, bledy i linie zrobione na watku glownym (poprzednia jeszcze w toku)
        private static int _f5Gen, _f5Busy, _f5Err, _f5Inline; private static string _f5ErrMsg;

        /// <summary>f5-koszt: nowa kampania / wczytanie (z RuleReset).</summary>
        private static void F5Reset()
        {
            Interlocked.Increment(ref _f5Gen);
            _f5RuleReuse = _f5RuleOk = _f5RuleBad = _f5RuleSample = 0;
            _f5DiagMs = -1; _f5WorkMs = -1; _f5DiagDay = -1; _f5DiagFast = false; _f5Inline = 0;
            _f5Checked = false; _f5Check = "jeszcze nie (pierwsza doba D+7k+3 po diagnozie)";
        }

        /// <summary>Leniwy wiersz wartosci zajec przy zestawie cen px (NaN - jeszcze nie liczone) - ten sam, co rowOf w F4A7Old.</summary>
        private static double[] F5Row(Dictionary<double[], double[]> rows, double[] px)
        {
            double[] r;
            if (!rows.TryGetValue(px, out r)) { r = new double[KsTab.NC]; for (int c = 0; c < r.Length; c++) r[c] = double.NaN; rows[px] = r; }
            return r;
        }

        /// <summary>Wartosc towarow zajecia c przy indeksach px - liczona i zapisana do wiersza (wolane, gdy w wierszu NaN; ta sama petla co w IncomeV; mv - _mv z watku
        /// glownego, tablica nie zmienia sie po EnsureValues).</summary>
        private static double F5Fill(double[] row, double[] px, int c, double[] mv)
        {
            double v = 0; int o = c * KsTab.NG;
            for (int g = 0; g < KsTab.NG; g++) { double m = mv[o + g]; if (m != 0) v += m * Px(px, g); }
            row[c] = v;
            return v;
        }

        /// <summary>F1: liczniki jednego zestawu cen - to samo co F4Var.Note (ta sama arytmetyka i kolejnosc), przejscia w tablicy [zajecie x 64 + zajecie] z kolejnoscia pierwszego
        /// wpisu (jak kolejnosc slownika Tr bez usuwania); na koniec F4() - F4Var z tymi samymi liczbami i ta sama kolejnoscia Tr (F4Top, F4VarText bez zmian).</summary>
        private sealed class F5Var
        {
            public int N, Press, Floor, Neg, Agree, NoAlt; public double Hands, PressHands;
            private readonly double[] _cnt = new double[KsTab.NC * 64], _hnd = new double[KsTab.NC * 64];
            private readonly bool[] _seen = new bool[KsTab.NC * 64];
            private readonly List<int> _order = new List<int>(256);
            public void Note(int cur, int best, double ic, double ib, double hands, double thr)
            {
                N++; Hands += hands;
                if (ic <= 0) Neg++;
                if (ic <= 0.02) Floor++;
                if (best < 0) { NoAlt++; Agree++; return; }
                if (ib <= ic) Agree++;
                if (ib >= thr * Math.Max(0.02, ic))
                {
                    Press++; PressHands += hands;
                    int k = cur * 64 + best;
                    if (!_seen[k]) { _seen[k] = true; _order.Add(k); }
                    _cnt[k] += 1; _hnd[k] += hands;
                }
            }
            public F4Var F4()
            {
                var v = new F4Var { N = N, Press = Press, Floor = Floor, Neg = Neg, Agree = Agree, NoAlt = NoAlt, Hands = Hands, PressHands = PressHands };
                foreach (int k in _order) v.Tr[k] = new[] { _cnt[k], _hnd[k] };
                return v;
            }
        }

        /// <summary>F1: kopia liczb diagnozy jednej doby (watek glowny -> watek z puli). Bloki ocenione w kolejnosci przegladu: zajecie biezace, rynek, zajecie startowe (S2),
        /// rece, L i renta x ha biezacego, kandydaci (bez biezacego, w kolejnosci listy) w tablicach plaskich od Off[i] do Off[i + 1].</summary>
        private sealed class F5Job
        {
            public int Today, D0, PlanDay, St; public bool Rule, Step; public double Inert, MainMs;
            public double[] P, One, Mv; public double[][] PxT = new double[2][], PxS = new double[2][], VPlan;
            public F5Var VG = new F5Var();
            public List<double> CurG = new List<double>(16000), BestG = new List<double>(16000), RatioG = new List<double>(16000);
            public double LvC, KC, RC, LvB, KB, RB;
            public int InTrans, Locked, NoCand, Back, NoPx; public bool Record;
            public int N; public int[] Cur = new int[16384], Mk = new int[16384], Sc = new int[16384], Off = new int[16385];
            public double[] Hands = new double[16384], Lc = new double[16384], Rc = new double[16384];
            public int M; public int[] C = new int[131072]; public double[] L = new double[131072], R = new double[131072];

            public void AddBlock(int cur, int mk, int sc, double hands, double lc, double rc, int[] cs, double[] ls, double[] rs, int n)
            {
                if (N + 1 >= Cur.Length)
                {
                    int k = Cur.Length * 2;
                    Array.Resize(ref Cur, k); Array.Resize(ref Mk, k); Array.Resize(ref Sc, k); Array.Resize(ref Hands, k); Array.Resize(ref Lc, k); Array.Resize(ref Rc, k); Array.Resize(ref Off, k + 1);
                }
                if (M + n > C.Length) { int k = Math.Max(C.Length * 2, M + n); Array.Resize(ref C, k); Array.Resize(ref L, k); Array.Resize(ref R, k); }
                Cur[N] = cur; Mk[N] = mk; Sc[N] = sc; Hands[N] = hands; Lc[N] = lc; Rc[N] = rc; Off[N] = M;
                Array.Copy(cs, 0, C, M, n); Array.Copy(ls, 0, L, M, n); Array.Copy(rs, 0, R, M, n);
                M += n; N++; Off[N] = M;
            }
        }

        /// <summary>F1 (watek glowny): przeglad blokow jak F4A7Old, L i renta x ha kazdego kandydata raz, zestaw G z liczeniem (te same liczby co regula A7) i zapisem do
        /// pamieci bloku (record), kopia liczb dla zestawow B, T, S1, S2. Nie rzuca na zewnatrz z okregu (potkniecie jak dotad).</summary>
        private static F5Job F5Prep(int today, int d0, bool rule, bool step, bool record)
        {
            long t0 = Stopwatch.GetTimestamp();
            var job = new F5Job { Today = today, D0 = d0, Rule = rule, Step = step, St = _f4A7St, PlanDay = _planDay, Record = record };
            EnsureValues();
            job.Mv = _mv;
            var plan = _plan;
            double[] P = plan != null && plan.Error == null && plan.P != null && plan.P.Length == 2 * KsTab.NG ? plan.P : null;
            job.P = P;
            // zestawy cen na towary listy KS (ItemIds) wedlug rynku - jak F4A7Old (StartIndex / ShadowIndex na watku glownym)
            var one = new double[ItemCount]; for (int i = 0; i < ItemCount; i++) one[i] = 1.0;
            job.One = one;
            if (P != null)
            {
                for (int m = 0; m < 2; m++)
                {
                    job.PxT[m] = new double[ItemCount]; job.PxS[m] = new double[ItemCount];
                    for (int i = 0; i < ItemCount; i++) { job.PxT[m][i] = KsCore.StartIndex(ItemIds[i], P, m); job.PxS[m][i] = KsCore.ShadowIndex(ItemIds[i], P, m); }
                }
                if (plan.Val != null && plan.Val.Length == KsTab.NG)
                {
                    var mvp = KsCore.CraftValue(plan.Val);
                    job.VPlan = new double[2][];
                    for (int m = 0; m < 2; m++)
                    {
                        job.VPlan[m] = new double[KsTab.NC];
                        for (int c = 0; c < KsTab.NC; c++) { double s = 0; int o = c * KsTab.NG; for (int g = 0; g < KsTab.NG; g++) { double v = mvp[o + g]; if (v != 0) s += v * P[m * KsTab.NG + g]; } job.VPlan[m][c] = s; }
                    }
                }
            }
            job.Inert = _planIn != null ? Math.Max(1.0, _planIn.Inert) : 1.25;
            bool s2 = job.VPlan != null;
            var rows = new Dictionary<double[], double[]>();
            var vG = job.VG; var kcost = KsTab.KCost; var mv = job.Mv;
            int cap = 32; var cs = new int[cap]; var ls = new double[cap]; var rs = new double[cap];
            foreach (var ld in _live.Values)
            {
                if (ld == null || ld.St == null || ld.K == null || ld.B == null) continue;
                try
                {
                    if (LandBackOf(ld.St)) { job.Back++; continue; }
                    Town mt = ld.TF ? ld.T : (ld.V != null && ld.V.TradeBound != null ? ld.V.TradeBound.Town : (ld.V != null && ld.V.Bound != null ? ld.V.Bound.Town : null));
                    double[] px = null;
                    bool hasPx = mt != null && _px.TryGetValue(mt, out px) && px != null;
                    double[] rowG = hasPx ? F5Row(rows, px) : null;      // wiersz miasta raz na okreg (ten sam obiekt, co rowOf(px) dla kazdego bloku)
                    int m = ld.Market == 0 ? 0 : 1;
                    string type = ld.K.Type;
                    foreach (var b in ld.B)
                    {
                        if (b == null || b.K == null || b.C == null || b.X == null || b.X.Length == 0) continue;
                        if (b.Target >= 0) { job.InTrans++; continue; }
                        if (KsWorksLock(ld, b)) { job.Locked++; continue; }
                        var cand = b.Cand;
                        if (cand == null || cand.Count < 2) { job.NoCand++; continue; }
                        int ci = 0; for (int i = 1; i < b.X.Length; i++) if (b.X[i] > b.X[ci]) ci = i;
                        int cur = b.C[ci];
                        double hands = b.Hands * b.X[ci];
                        // L zajecia i renta x ha biezacego i kandydatow (bez biezacego, w kolejnosci listy) - raz na blok (dotad w kazdym z pieciu zestawow cen)
                        int nc = cand.Count;
                        if (nc > cap) { cap = 2 * nc; cs = new int[cap]; ls = new double[cap]; rs = new double[cap]; }
                        double lc = b.L(cur, type), rc = b.Rent * KsTab.Ha(cur, b.K);
                        int n = 0;
                        for (int q = 0; q < nc; q++) { int c = cand[q]; if (c == cur) continue; cs[n] = c; ls[n] = b.L(c, type); rs[n] = b.Rent * KsTab.Ha(c, b.K); n++; }
                        // S2: F4Start(ld, b, c) = (Crafts[c] == Start); nazwy zajec sa rozne, a CraftIndex to ich indeks (porownanie porzadkowe jak ==) - zajecie startowe raz
                        int sc = -1, t;
                        if (s2 && !ld.TF && b.K.Start != null && KsTab.CraftIndex.TryGetValue(b.K.Start, out t)) sc = t;
                        // G: ceny gry - dokladnie to, co liczy regula (ten sam wiersz, kandydaci, prog)
                        if (hasPx)
                        {
                            double w = rowG[cur]; if (double.IsNaN(w)) w = F5Fill(rowG, px, cur, mv);
                            double ic = lc * w - kcost[cur] - rc, ib = double.NegativeInfinity; int best = -1, bj = -1;
                            for (int j = 0; j < n; j++)
                            {
                                int c = cs[j];
                                double x = rowG[c]; if (double.IsNaN(x)) x = F5Fill(rowG, px, c, mv);
                                double v = ls[j] * x - kcost[c] - rs[j];
                                if (v > ib) { ib = v; best = c; bj = j; }
                            }
                            vG.Note(cur, best, ic, ib, hands, SwitchRatio);
                            job.CurG.Add(ic);
                            if (best >= 0)
                            {
                                job.BestG.Add(ib); job.RatioG.Add(ib / Math.Max(0.02, ic));
                                job.LvC += lc * rowG[cur]; job.KC += kcost[cur]; job.RC += rc;
                                job.LvB += ls[bj] * rowG[best]; job.KB += kcost[best]; job.RB += rs[bj];
                            }
                            if (record) { b.F5Day = today; b.F5Cur = cur; b.F5Best = best; b.F5Ic = ic; b.F5Ib = ib; b.F5Px = px; }   // F2: dla reguly A7 tej doby
                        }
                        else job.NoPx++;
                        job.AddBlock(cur, m, sc, hands, lc, rc, cs, ls, rs, n);
                    }
                }
                catch (Exception e) { _f4A7St++; Stumble("F4A7Diag(okreg)", e); }
            }
            job.St = _f4A7St - job.St;                                     // potkniecia tej diagnozy
            job.MainMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            return job;
        }

        /// <summary>F1 (watek z puli albo glowny): zestawy B, T, S1, S2 z kopii liczb i linia - co do znaku jak F4A7Old poza liczba czasu. Tylko liczby z F5Job, stale tablice
        /// (KsTab.KCost, GoodToItem przez Px) i czyste funkcje napisu - zadnego obiektu gry.</summary>
        private static string F5Finish(F5Job job)
        {
            long t0 = Stopwatch.GetTimestamp();
            var vB = new F5Var(); var vT = new F5Var(); var vS1 = new F5Var(); var vS2 = new F5Var();
            var kcost = KsTab.KCost; var mv = job.Mv;
            var row1 = new double[KsTab.NC]; var rowT = new double[2][]; var rowS = new double[2][];
            for (int c = 0; c < KsTab.NC; c++) row1[c] = double.NaN;
            bool hasP = job.P != null, s2 = job.VPlan != null;
            if (hasP) for (int m = 0; m < 2; m++) { rowT[m] = new double[KsTab.NC]; rowS[m] = new double[KsTab.NC]; for (int c = 0; c < KsTab.NC; c++) { rowT[m][c] = double.NaN; rowS[m][c] = double.NaN; } }
            int planAgree = 0, planN = 0;
            double inert = job.Inert;
            for (int i = 0; i < job.N; i++)
            {
                int cur = job.Cur[i], m = job.Mk[i], a = job.Off[i], e = job.Off[i + 1];
                double hands = job.Hands[i], lc = job.Lc[i], rc = job.Rc[i];
                F5Eval(vB, cur, hands, lc, rc, row1, job.One, job.C, job.L, job.R, a, e, mv);
                if (hasP)
                {
                    F5Eval(vT, cur, hands, lc, rc, rowT[m], job.PxT[m], job.C, job.L, job.R, a, e, mv);
                    F5Eval(vS1, cur, hands, lc, rc, rowS[m], job.PxS[m], job.C, job.L, job.R, a, e, mv);
                    if (s2)
                    {
                        // S2: wzor Planu przy cenach cienia (jak F4PlanInc: max(1e-6, L) x v - K - renta x ha), wybor Planu z premia mapy (zajecie startowe sc)
                        var vp = job.VPlan[m]; int sc = job.Sc[i];
                        double ic = Math.Max(1e-6, lc) * vp[cur] - kcost[cur] - rc, ib = double.NegativeInfinity; int best = -1, pick = cur;
                        double uMax = Math.Max(0.02, ic) * (cur == sc ? inert : 1.0);
                        for (int j = a; j < e; j++)
                        {
                            int c = job.C[j];
                            double v = Math.Max(1e-6, job.L[j]) * vp[c] - kcost[c] - job.R[j];
                            if (v > ib) { ib = v; best = c; }
                            double u = Math.Max(0.02, v) * (c == sc ? inert : 1.0);
                            if (u > uMax) { uMax = u; pick = c; }
                        }
                        vS2.Note(cur, best, ic, ib, hands, SwitchRatio);
                        planN++; if (pick == cur) planAgree++;
                    }
                }
            }
            double workMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            double ms = job.MainMs + workMs;
            if (job.Record) Volatile.Write(ref _f5WorkMs, workMs);              // czas watku do linii "KS: koszt f5" (kontrola na miejscu - bez)
            // linia - co do znaku jak w F4A7Old (kwantyle przez wybor: ten sam element co po sortowaniu; liczniki zestawow jako F4Var - te same liczby i kolejnosc przejsc)
            F4Var fG = job.VG.F4(), fB = vB.F4(), fT = vT.F4(), fS1 = vS1.F4(), fS2 = vS2.F4();
            var curG = job.CurG; var bestG = job.BestG; var ratioG = job.RatioG;
            int today = job.Today, d0 = job.D0;
            var sb = new StringBuilder(3000);
            sb.Append(PK).Append("A7 diagnoza (f4-pomiary) dzien ").Append(today).Append(" | D ").Append(d0).Append(" (D+").Append(today - d0).Append(") ")
              .Append(job.Step ? "krok reguly A7" : "doba D (srednie cen gry sprzed polek startu)")
              .Append(" | regula: ").Append(job.Rule ? "TAK (Start Calibration Switch Rule)" : "WYLACZONA - zajecia zamrozone na Planie wlaczenia (sam pomiar nacisku)")
              .Append(" | blokow ocenionych ").Append(fB.N).Append(" (pominiete: w przebudowie ").Append(job.InTrans).Append(", prace okregu ").Append(job.Locked).Append(", bez kandydatow ").Append(job.NoCand)
              .Append(", okregi krain wroconych ").Append(job.Back).Append(", bez cen miasta targowego ").Append(job.NoPx).Append(')');
            var buf = new double[Math.Max(1, Math.Max(curG.Count, Math.Max(bestG.Count, ratioG.Count)))];
            sb.Append(" | ceny gry (srednia 28 dob miasta targowego): ocenionych ").Append(fG.N).Append(", dochod biezacy q10/med/q90 ").Append(F5Q3(curG, buf))
              .Append(", <= 0 ").Append(fG.Neg).Append(", na podlodze 0.02 ").Append(fG.Floor).Append("; najlepsze inne q10/med/q90 ").Append(F5Q3(bestG, buf));
            string rMed = "-", r90 = "-";
            if (ratioG.Count > 0) { ratioG.CopyTo(buf); var rq = F5Quant(buf, ratioG.Count, 0.5, 0.9); rMed = rq[0]; r90 = rq[1]; }
            sb.Append("; iloraz najlepsze/max(0.02, biezace) med ").Append(rMed).Append(" q90 ").Append(r90);
            F4VarText(sb, "; ", fG); sb.Append(", biezace najlepsze ").Append(fG.Agree).Append(" (").Append(Pc(fG.Agree, fG.N)).Append("%)");
            sb.Append(" | przy indeksie 1 (wartosc bazowa)"); F4VarText(sb, ": ", fB); sb.Append(", na podlodze ").Append(fB.Floor).Append(", <= 0 ").Append(fB.Neg);
            if (hasP)
            {
                sb.Append(" | przy cenach startu (cien Planu doby ").Append(job.PlanDay).Append(" w 0.7-1.5)"); F4VarText(sb, ": ", fT); sb.Append(", na podlodze ").Append(fT.Floor);
                sb.Append(" | regula przy cenach cienia Planu (zywiec po cenie cienia zwierzecia, konie 0 - Plan liczy zywiec jako mieso i skory, konie z wartoscia)"); F4VarText(sb, ": ", fS1); sb.Append(", na podlodze ").Append(fS1.Floor);
                if (s2)
                {
                    sb.Append(" | wzor Planu przy cenach cienia (bez gradientu krain)"); F4VarText(sb, ": ", fS2);
                    sb.Append(", zajecie biezace = wybor Planu z premia mapy x").Append(inert.ToString("0.00", Inv)).Append(' ').Append(planAgree).Append(" z ").Append(planN).Append(" (").Append(Pc(planAgree, planN)).Append("%)");
                }
                else sb.Append(" | wzor Planu: brak wartosci kg Planu");
            }
            else sb.Append(" | ceny startu i cienia: brak Planu bez bledu");
            sb.Append(" | sklad dochodu (suma blokow, ceny gry): biezace L*v ").Append(job.LvC.ToString("0", Inv)).Append(", koszt K ").Append(job.KC.ToString("0", Inv)).Append(", renta ").Append(job.RC.ToString("0", Inv))
              .Append("; najlepsze inne L*v ").Append(job.LvB.ToString("0", Inv)).Append(", koszt K ").Append(job.KB.ToString("0", Inv)).Append(", renta ").Append(job.RB.ToString("0", Inv));
            sb.Append(" | przejscia (ceny gry, top 10, blokow/rak): ").Append(F4Top(fG, 10));
            sb.Append(" | przejscia przy indeksie 1 (top 5): ").Append(F4Top(fB, 5));
            if (s2) sb.Append(" | przejscia wzoru Planu (top 5): ").Append(F4Top(fS2, 5));
            sb.Append(" | czas ").Append(ms.ToString("0.0", Inv)).Append(" ms, potkniecia ").Append(job.St).Append('.');
            return sb.ToString();
        }

        /// <summary>F1: jeden zestaw cen (B, T, S1) z kopii liczb - to samo co F4Eval (IncomeV biezacego i kazdego kandydata, wybor pierwszego najwiekszego).</summary>
        private static void F5Eval(F5Var var, int cur, double hands, double lc, double rc, double[] row, double[] px, int[] cs, double[] ls, double[] rs, int a, int e, double[] mv)
        {
            var kc = KsTab.KCost;
            double w = row[cur]; if (double.IsNaN(w)) w = F5Fill(row, px, cur, mv);
            double ic = lc * w - kc[cur] - rc, ib = double.NegativeInfinity; int best = -1;
            for (int j = a; j < e; j++)
            {
                int c = cs[j];
                double x = row[c]; if (double.IsNaN(x)) x = F5Fill(row, px, c, mv);
                double v = ls[j] * x - kc[c] - rs[j];
                if (v > ib) { ib = v; best = c; }
            }
            var.Note(cur, best, ic, ib, hands, SwitchRatio);
        }

        /// <summary>F1: diagnoza A7 na miejscu (kontrola F5Check i uprzaz): F5Prep + F5Finish na watku glownym.</summary>
        private static string F4A7Fast(int today, int d0, bool rule, bool step, bool record)
        {
            return F5Finish(F5Prep(today, d0, rule, step, record));
        }

        /// <summary>F1 (z F4A7Diag, klucz Ks Cost Fast2): przeglad na watku glownym, reszta pomiaru i linia na watku z puli; poprzednia diagnoza jeszcze w toku albo blad
        /// kolejki - na miejscu (linia ta sama).</summary>
        private static void F4A7Async(int today, int d0, bool rule, bool step)
        {
            var job = F5Prep(today, d0, rule, step, true);
            _f5DiagMs = job.MainMs; _f5DiagDay = today; _f5DiagFast = true;
            if (Interlocked.CompareExchange(ref _f5Busy, 1, 0) != 0) { _f5Inline++; Log.Info(F5Finish(job)); return; }
            int gen = Volatile.Read(ref _f5Gen);
            try
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        string line = F5Finish(job);
                        if (gen == Volatile.Read(ref _f5Gen)) Log.Info(line);      // nowa sesja w trakcie - linia starej sesji odrzucona
                    }
                    catch (Exception e) { Interlocked.Increment(ref _f5Err); _f5ErrMsg = e.GetType().Name + ": " + e.Message; }
                    finally { Volatile.Write(ref _f5Busy, 0); }
                });
            }
            catch (Exception e)
            {
                Volatile.Write(ref _f5Busy, 0); Stumble("F4A7Async", e);
                _f5Inline++; Log.Info(F5Finish(job));
            }
        }

        // ============================================================ kwantyle przez wybor (ten sam element co Q3 / Qx po List.Sort)
        /// <summary>q10/med/q90 jak Q3 (pusta lista "-/-/-"), bez sortowania listy: wybor k-tego elementu na kopii w buf.</summary>
        private static string F5Q3(List<double> v, double[] buf)
        {
            int n = v.Count;
            if (n == 0) return "-/-/-";
            v.CopyTo(buf);
            var q = F5Quant(buf, n, 0.1, 0.5, 0.9);
            return q[0] + "/" + q[1] + "/" + q[2];
        }

        /// <summary>Kwantyle jak Qs (element o indeksie (int)(q x (n - 1)) po sortowaniu rosnacym, napis "0.00") dla q rosnacych - wybor w a[0..n) (przestawia a): NaN na poczatek
        /// (porzadek double.CompareTo, jak List&lt;double&gt;.Sort), potem kazdy nastepny kwantyl szukany tylko na prawo od poprzedniego.</summary>
        private static string[] F5Quant(double[] a, int n, params double[] qs)
        {
            var r = new string[qs.Length];
            int nn = 0;
            for (int i = 0; i < n; i++) if (double.IsNaN(a[i])) { double t = a[i]; a[i] = a[nn]; a[nn] = t; nn++; }
            int lo = nn;
            for (int qi = 0; qi < qs.Length; qi++)
            {
                int k = (int)(qs[qi] * (n - 1));
                double x;
                if (k < nn) x = a[k];
                else { if (k < lo) lo = nn; x = F5Sel(a, lo, n - 1, k); lo = k; }
                r[qi] = x.ToString("0.00", Inv);
            }
            return r;
        }

        /// <summary>k-ty element a[lo..hi] (bez NaN) w porzadku rosnacym - przestawia a tak, ze a[lo..k) &lt;= a[k] &lt;= a(k..hi]. Rowne w porzadku sa rowne liczbowo (poza -0 i +0,
        /// ktore .NET Framework drukuje tak samo). Podzial trojdrozny (powtorzenia), mediana z trzech; po 64 przejsciach - Array.Sort reszty (zabezpieczenie, ten sam wynik).</summary>
        private static double F5Sel(double[] a, int lo, int hi, int k)
        {
            int guard = 0;
            while (lo < hi)
            {
                if (++guard > 64) { Array.Sort(a, lo, hi - lo + 1); return a[k]; }
                int mid = lo + ((hi - lo) >> 1);
                double x0 = a[lo], x1 = a[mid], x2 = a[hi];
                double x = x0 < x1 ? (x1 < x2 ? x1 : (x0 < x2 ? x2 : x0)) : (x0 < x2 ? x0 : (x1 < x2 ? x2 : x1));
                int lt = lo, i = lo, gt = hi;
                while (i <= gt)
                {
                    double y = a[i];
                    if (y < x) { a[i] = a[lt]; a[lt] = y; lt++; i++; }
                    else if (y > x) { a[i] = a[gt]; a[gt] = y; gt--; }
                    else i++;
                }
                if (k < lt) hi = lt - 1;
                else if (k > gt) lo = gt + 1;
                else return a[k];
            }
            return a[k];
        }

        /// <summary>k-ty (od 0) element a[0..n) w porzadku double.CompareTo (NaN najmniejsze) - do uprzezy i kontroli (przestawia a).</summary>
        internal static double F5Kth(double[] a, int n, int k)
        {
            int nn = 0;
            for (int i = 0; i < n; i++) if (double.IsNaN(a[i])) { double t = a[i]; a[i] = a[nn]; a[nn] = t; nn++; }
            return k < nn ? a[k] : F5Sel(a, nn, n - 1, k);
        }

        // ============================================================ F2: regula A7 - dochody z diagnozy tej doby
        /// <summary>RuleDaily (doba kroku, dochody z diagnozy): licznik i probka 1/64 - stary rachunek (IncomeV / Income, kandydaci w kolejnosci listy) wobec pamieci bloku.</summary>
        private static void F5RuleNote(LiveBlock b, string type, int cur, double[] px, double[] vRow, double incCur, double incBest, int best)
        {
            _f5RuleReuse++;
            if ((++_f5RuleSample & 63) != 0) return;
            try
            {
                double ic = vRow != null ? IncomeV(b, type, cur, px, vRow) : Income(b, type, cur, px), ib = double.NegativeInfinity; int bs = -1;
                foreach (int c in b.Cand) { if (c == cur) continue; double v = vRow != null ? IncomeV(b, type, c, px, vRow) : Income(b, type, c, px); if (v > ib) { ib = v; bs = c; } }
                if (SameD(ic, incCur) && SameD(ib, incBest) && bs == best) _f5RuleOk++; else _f5RuleBad++;
            }
            catch (Exception e) { _f5RuleBad++; Stumble("F5RuleNote", e); }
        }

        // ============================================================ kontrola i linia "KS: koszt f5"
        /// <summary>Po linii doby KS (poza czasem doby KS), stan zastosowany, linie, klucz, doba D+7k+3: raz na sesje stary rachunek diagnozy A7 wobec szybkiego na tych
        /// samych danych (tylko odczyt; linia bez czasu ta sama) i linia "KS: koszt f5 dzien N" (liczniki od ostatniej linii).</summary>
        private static void F5Check(int today, int day)
        {
            if (!SwCostFast2 || _state != StateApplied || !LogGate.Lines) return;
            int d0 = DDay;
            if (d0 < 0 || today <= d0 || ((today - d0) % StepDays) != 3) return;
            try
            {
                double chkMs = -1;
                if (!_f5Checked && F4A7On && _f5DiagFast)
                {
                    _f5Checked = true;
                    long q0 = Stopwatch.GetTimestamp();
                    var s = Settings.Current; bool rule = s != null && s.StartCalibrationSwitchRule;
                    string a = F4A7Old(today, d0, rule, true), b = F4A7Fast(today, d0, rule, true, false);
                    chkMs = (Stopwatch.GetTimestamp() - q0) * 1000.0 / Stopwatch.Frequency;
                    string na = F5NoTime(a), nb = F5NoTime(b);
                    if (na == nb) _f5Check = "zgodna TAK (doba " + today + ", znakow " + na.Length + ")";
                    else
                    {
                        int i = 0; while (i < na.Length && i < nb.Length && na[i] == nb[i]) i++;
                        _f5Check = "ROZNA (doba " + today + ", pierwsza roznica na znaku " + i + ": stary '" + F5Cut(na, i) + "' / szybki '" + F5Cut(nb, i) + "')";
                    }
                }
                int err = Volatile.Read(ref _f5Err);
                if (err > 0) Stumble("F4A7Async(watek)", null);
                double wm = Volatile.Read(ref _f5WorkMs);
                Log.Info(PK + "koszt f5 dzien " + day + " | klucz Ks Cost Fast2 TAK | diagnoza A7 (f4-pomiary): ostatnia doba " + (_f5DiagDay >= 0 ? _f5DiagDay.ToString() : "-")
                         + ", watek glowny " + (_f5DiagMs >= 0 ? _f5DiagMs.ToString("0.00", Inv) : "-") + " ms, watek z puli " + (wm >= 0 ? wm.ToString("0.00", Inv) : "-") + " ms"
                         + " (" + (_f5DiagFast ? "szybka" : "stara") + ", na miejscu - poprzednia w toku " + _f5Inline + ", bledy watku " + err + (err > 0 && _f5ErrMsg != null ? " - " + _f5ErrMsg : "") + ")"
                         + " | regula A7: dochody z diagnozy tej doby " + _f5RuleReuse + " blokow, probka 1/64 starym rachunkiem: zgodne " + _f5RuleOk + ", rozne " + _f5RuleBad
                         + " | kontrola diagnozy (stary rachunek wobec szybkiego na tych samych danych, raz na sesje): " + _f5Check
                         + (chkMs >= 0 ? ", czas kontroli " + chkMs.ToString("0.0", Inv) + " ms poza czasem doby KS" : "")
                         + " | tylko szybsze liczenie tych samych liczb - zadnej zmiany gry.");
                _f5RuleReuse = _f5RuleOk = _f5RuleBad = 0; Interlocked.Exchange(ref _f5Err, 0); _f5Inline = 0;
            }
            catch (Exception e) { Stumble("F5Check", e); }
        }

        /// <summary>Linia diagnozy bez liczby czasu (" | czas X ms, potkniecia N." -> " | czas ms, potkniecia N.").</summary>
        private static string F5NoTime(string s)
        {
            if (s == null) return "";
            int i = s.LastIndexOf(" | czas ", StringComparison.Ordinal);
            if (i < 0) return s;
            int j = s.IndexOf(" ms, potkniecia ", i, StringComparison.Ordinal);
            return j < 0 ? s.Substring(0, i) : s.Substring(0, i) + " | czas" + s.Substring(j);
        }

        private static string F5Cut(string s, int i) { int a = Math.Max(0, i - 20); int len = Math.Min(s.Length - a, 60); return len > 0 ? s.Substring(a, len) : ""; }
    }
}
