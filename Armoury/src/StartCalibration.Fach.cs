using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Armoury
{
    // m106-wozy P1 "FACH WSI" (22.10, klucz Ks Village Trade Keep; diagnoza scratchpad wozy/diagnoza-wozow.md P1: po wlaczeniu KS w 185 z 568 wsi glowny towar spadl do zera -
    // Tarth wies rybacka bez ryb, Red Watch wies drwali bez drewna, 44 wsie rybackie; ryby nie zniknely ze swiata, lowia je bloki przy brzegu wsi innych typow).
    // Czysty C# (bez typow gry) - ten sam plik kompiluje harness Planu (tools/ekonomia-symulatory-2026-10-17/kmoney).
    //
    // ZASADA (Jeff: z podazy i popytu, start neutralny, bez sztywnych mnoznikow, zmiany produkcji powoli, z biezacego zysku, zamknieta ekonomia): fach wchodzi do rownowagi
    // Planu jako DOLNA GRANICA rak przy zajeciu typu wsi - jak prog "najpierw wyzywic siebie" (ApplyFloors), ktory ma pierwszenstwo (biegnie po fachu):
    //  - cel okregu T = k x baza gry na targ (FachKg: stawka typu x u x g albo (poziom + 1) / 2 - "baza_reguly" pliku plon-wsi.csv - x sigma x kg sztuki) + to, co okreg zje
    //    sam z tego towaru (NeedsOfKeep jak KsDeliver.Own, x lepszy stol);
    //  - k rynku i towaru = min(1, (popyt rynku - podaz regale i spoza tabeli - zjedzone w okregach fachu) / suma baz fachu) - wprost z podazy i popytu Planu, jedyna skala;
    //    suma celow nigdy nie przekracza popytu, wiec fach nie robi nadwyzki (zywiec - wobec rynku miesa: zywiec daje mieso x 0.5);
    //  - w kazdej rundzie rownowagi (po logicie, przed progiem wyzywienia): okreg z produkcja towaru ponizej celu przesuwa udzial rak bloku na pare zajecia typu - najpierw
    //    bloki, w ktorych typ daje najwiecej towaru na udzial (brzeg dla rybakow), tylko w blokach, w ktorych praca typu daje wiecej, niz kosztuje (dochod pary przed
    //    podloga > 0); w bloku dawcy od pary z najnizszym dochodem (recenzja R2 - najpierw praca najgorzej platna, nie zboze i ser po rowno z reszta), bez pary mieszanki
    //    jedzenia progu i bez pary z wiecej kcal jedzenia niz para typu (recenzja R1 - fach nie robi okregu glodniejszym); tylko tyle, ile brakuje, i najwyzej do ziemi
    //    bloku (ha zajete <= 0.98 x powierzchni - jak prog wyzywienia). Suma udzialow bloku = 1:
    //    rak tyle samo, zmienia sie tylko ich zajecie; produkcja dalej rece x tabela swiata x ziemia - nic z niczego. Rownowaga przestawia reszte swiata (ceny), wiec bilans
    //    Planu (NKS-3) trzyma sie z konstrukcji;
    //  - regale (drwale, kopalnie, daktyle): w RegaleStep wiersze okregow typu najpierw - min(sufit wiersza, cel), razem najwyzej potrzeba krainy (jedna skala na kraine), reszta
    //    potrzeby po staremu proporcjonalnie do wolnego sufitu. Suma podazy bez zmian.
    // Klucz wylaczony (KsInput.FachOn false) - nic z tego (Plan bit w bit jak w a7a767e8).

    internal sealed partial class KsDistrict
    {
        public int FachC = -1, FachG = -1;   // m106: zajecie typu wsi (KsTab.Crafts) i towar glownej produkcji typu (KsTab.Goods); -1 - bez fachu
        public double FachKg;                // m106: baza gry na targ w kg/d (przed k)
    }

    internal sealed partial class KsInput
    {
        public bool FachOn;                  // m106 (klucz Ks Village Trade Keep): fach wsi w rownowadze Planu i w RegaleStep
    }

    /// <summary>m106: fach okregu w wyniku Planu. T - cel na targ kg/d po k (regale - po skali krainy), P - produkcja towaru w Planie kg/d, Own - chec zjedzenia w okregu kg/d,
    /// K - k rynku i towaru, Flag: 0 w pelni, 1 rece/ziemia (albo sufit regale), 2 praca typu bez zysku, 3 prog wyzywienia cofnal fach.</summary>
    internal sealed class KsFach { public int G; public double T, P, Own, K; public int Flag; public bool Reg; }

    internal sealed partial class KsResult
    {
        public Dictionary<string, KsFach> DistFach;   // m106: okreg -> fach (null - klucz wylaczony)
        public string FachLine;                       // m106: linia "KS: fach wsi (m106) plan doba {DOBA}" z watku roboczego
        public double FachMs;
    }

    internal static partial class KsCore
    {
        private sealed partial class Ctx
        {
            public bool FachEq;                                        // m106: fach czynny w tej rownowadze (FachStep)
            public double[] IncRaw;                                    // [NP] dochod pary przed podloga 0.02 (Shares, przy FachEq)
            public double[] FhOg;                                       // [NP] kg/d towaru fachu okregu pary przy udziale 1 (OVal; 0 poza okregami fachu i bez towaru)
            public int[] FhKt;                                          // [NB] para zajecia typu bloku okregu fachu (-1 - brak)
            public bool[] FhFeed;                                       // okreg fachu objety progiem wyzywienia w tej rownowadze (PairKcal i mieszanka bloku policzone)
            public int[] FhIdx;                                         // bufor dawcow bloku (ApplyFach - od najnizszego dochodu pary)
            public int FhN;                                             // okregi fachu (bez regale)
            public KsDistrict[] FhD; public int[] FhG, FhPs, FhPk, FhOs, FhOrd; public double[] FhT, FhOwn, FhK, FhAfter; public byte[] FhFlag;
            public double[] FhHandsBy = new double[KsTab.NC];           // rece przesuniete do fachu wedlug zajecia dawcy (ostatnia runda)
            public double FhHands;
            public long FhTicks;
            public List<string> FhKList;                                // k wedlug rynku i towaru (do linii)
            public Dictionary<KsDistrict, double> FhRegT;               // regale: cel okregu po skali krainy (kg/d) - ostatni RegaleStep
            public double FhRegFirst, FhRegTarget;                       // regale: dane wsiom typu najpierw / ich cel (kg/d) - ostatni RegaleStep
        }

        /// <summary>m106: kg/d towaru g pary k przy udziale 1 (OVal, z praca domowa kroku 8).</summary>
        private static double OValOf(Ctx x, int k, int g)
        {
            for (int j = x.OStart[k]; j < x.OStart[k + 1]; j++) if (x.OGood[j] == g) return x.OVal[j];
            return 0.0;
        }

        private static bool Livestock(int g) { return g == KsTab.GCow || g == KsTab.GHog || g == KsTab.GSheep; }

        /// <summary>m106: chec zjedzenia towaru g w okregu (kg/d) - te same potrzeby co KsDeliver.Own (NeedsOfKeep ludzi w domu x lepszy stol): zboze z piwem domowym, ryby,
        /// winogrona z winem, oliwki z oliwa, daktyle, len (plotno x 1.25), welna (sukno x 1.45), miod; reszta (drewno, zywiec, futra...) - 0.</summary>
        private static double FachOwn(Ctx x, KsDistrict d, int g, double[] n)
        {
            if (d.PplHome == null || d.Bask == null || x.In.Keep == null) return 0.0;
            double ppl = 0; foreach (var v in d.PplHome) ppl += v;
            NeedsOfKeep(d.PplHome, d.Bask, x.In.Keep, d.Oil, ppl, n);
            var nr = d.NeedR8 ?? d.Need;
            Func<int, double> M = k => (nr != null && d.Need0[k] > 1e-9) ? Math.Max(0.25, Math.Min(4.0, nr[k] / d.Need0[k])) : 1.0;
            switch (g)
            {
                case KsTab.GGrain: return n[KsTab.NG_] * M(KsTab.NG_) + n[KsTab.NBeer] * M(KsTab.NBeer) * KsTab.BeerGrain;
                case KsTab.GFish: return n[KsTab.NFish] * M(KsTab.NFish);
                case KsTab.GGrape: return n[KsTab.NGrape] * M(KsTab.NGrape) + n[KsTab.NWine] * M(KsTab.NWine) * KsTab.WineGrape;
                case KsTab.GOlives: return n[KsTab.NOlives] * M(KsTab.NOlives) + n[KsTab.NOil] * M(KsTab.NOil) * KsTab.OilOlive;
                case KsTab.GDate: return n[KsTab.NDate] * M(KsTab.NDate);
                case KsTab.GFlax: return n[KsTab.NPlotno] * M(KsTab.NPlotno) * KsTab.LinenFlax;
                case KsTab.GWool: return n[KsTab.NSukno] * M(KsTab.NSukno) * KsTab.FeltWool;
                case KsTab.GHoney: return n[KsTab.NHoney] * M(KsTab.NHoney) + n[KsTab.NMead] * M(KsTab.NMead) * KsTab.MeadHoney;
                default: return 0.0;
            }
        }

        /// <summary>m106 - raz na rownowage (po FloorStep): okregi fachu, k rynku i towaru, cele, pary z towarem fachu i kolejnosc blokow (typ daje najwiecej towaru na udzial).</summary>
        private static void FachStep(Ctx x)
        {
            x.FachEq = false;
            if (!x.In.FachOn) return;
            long t0 = Stopwatch.GetTimestamp();
            int NP = x.NP, NB = x.NB;
            if (x.FhOg == null || x.FhOg.Length != NP) x.FhOg = new double[NP]; else Array.Clear(x.FhOg, 0, NP);
            if (x.IncRaw == null || x.IncRaw.Length != NP) x.IncRaw = new double[NP];
            if (x.FhKt == null || x.FhKt.Length != NB) x.FhKt = new int[NB];
            for (int b = 0; b < NB; b++) x.FhKt[b] = -1;
            var dl = new List<KsDistrict>(); var b0l = new List<int>(); var b1l = new List<int>();
            int b0 = 0;
            while (b0 < NB)
            {
                var d = x.BD[b0];
                int b1 = b0 + 1;
                while (b1 < NB && ReferenceEquals(x.BD[b1], d)) b1++;       // bloki okregu sa kolejne (Prepare)
                if (!d.TownFields && d.FachC >= 0 && d.FachC < KsTab.NC && d.FachG >= 0 && d.FachG < KsTab.NG && d.FachKg > 0 && !KsTab.IsRegale[d.FachC]) { dl.Add(d); b0l.Add(b0); b1l.Add(b1); }
                b0 = b1;
            }
            int n = dl.Count;
            x.FhN = n; x.FhD = dl.ToArray(); x.FhG = new int[n]; x.FhT = new double[n]; x.FhOwn = new double[n]; x.FhK = new double[n];
            x.FhAfter = new double[n]; x.FhFlag = new byte[n]; x.FhFeed = new bool[n];
            var ps = new List<int>(n + 1); var pk = new List<int>(n * 24); var os = new List<int>(n + 1); var ord = new List<int>(n * 20);
            var sumKg = new Dictionary<int, double>(); var sumOwn = new Dictionary<int, double>();
            var tmp = new double[KsTab.NN];
            for (int i = 0; i < n; i++)
            {
                var d = dl[i]; int g = d.FachG;
                x.FhG[i] = g;
                x.FhFeed[i] = x.FeedOn && x.FA != null && x.PairKcal != null && InFeedScope(d, x.In.SelfFeed, x.B[b0l[i]]);   // recenzja R1: prog wyzywienia liczyl ten okreg
                double own = Livestock(g) ? 0.0 : FachOwn(x, d, g, tmp);
                x.FhOwn[i] = own;
                int key = d.Market * KsTab.NG + g;
                double a; sumKg.TryGetValue(key, out a); sumKg[key] = a + d.FachKg * (Livestock(g) ? KsTab.MeatOfLive : 1.0);
                sumOwn.TryGetValue(key, out a); sumOwn[key] = a + own;
            }
            // k rynku i towaru (jedyna skala - z podazy i popytu Planu); zywiec wobec rynku miesa (popyt Planu nie ma zywca osobno: mieso z uboju = zywiec x 0.5)
            var kOf = new Dictionary<int, double>();
            var keys = new List<int>(sumKg.Keys); keys.Sort();
            x.FhKList = new List<string>(keys.Count);
            foreach (int key in keys)
            {
                int m = key / KsTab.NG, g = key % KsTab.NG;
                int gi = m * KsTab.NG + (Livestock(g) ? KsTab.GMeat : g);
                double dm = x.Dm != null && gi < x.Dm.Length ? x.Dm[gi] : 0.0, sr = x.Sreg != null && gi < x.Sreg.Length ? x.Sreg[gi] : 0.0;
                double avail = Math.Max(0.0, dm - sr - sumOwn[key]), tot = sumKg[key];
                double k = tot > 0 ? Math.Min(1.0, avail / tot) : 0.0;
                if (double.IsNaN(k) || double.IsInfinity(k)) k = 0.0;
                kOf[key] = k;
                x.FhKList.Add((m == 0 ? "W " : "E ") + KsTab.Goods[g] + " " + k.ToString("0.00", CultureInfo.InvariantCulture));
            }
            for (int i = 0; i < n; i++)
            {
                var d = dl[i]; int g = d.FachG;
                double k = kOf[d.Market * KsTab.NG + g];
                x.FhK[i] = k;
                x.FhT[i] = k * d.FachKg + x.FhOwn[i];
                ps.Add(pk.Count); os.Add(ord.Count);
                var blk = new List<int>();
                for (int b = b0l[i]; b < b1l[i]; b++)
                {
                    for (int q = x.BStart[b]; q < x.BStart[b + 1]; q++)
                    {
                        double og = OValOf(x, q, g);
                        if (og != 0.0) { x.FhOg[q] = og; pk.Add(q); }
                        if (x.Pc[q] == d.FachC) x.FhKt[b] = q;
                    }
                    if (x.FhKt[b] >= 0 && x.FhOg[x.FhKt[b]] > 0) blk.Add(b);
                }
                blk.Sort((p1, p2) => { int c = x.FhOg[x.FhKt[p2]].CompareTo(x.FhOg[x.FhKt[p1]]); return c != 0 ? c : p1.CompareTo(p2); });
                ord.AddRange(blk);
            }
            ps.Add(pk.Count); os.Add(ord.Count);
            x.FhPs = ps.ToArray(); x.FhPk = pk.ToArray(); x.FhOs = os.ToArray(); x.FhOrd = ord.ToArray();
            x.FachEq = n > 0;
            x.FhTicks += Stopwatch.GetTimestamp() - t0;
        }

        /// <summary>m106 - w kazdej rundzie (Shares, po logicie, PRZED progiem wyzywienia): okreg z towarem fachu ponizej celu przesuwa rece bloku na pare zajecia typu
        /// (opis na gorze pliku; blok - FachBlock). Liczby ostatniej rundy: produkcja po fachu, flagi (1 ziemia, 2 typ bez zysku, 4 rece - wszystko juz w typie albo tylko pary
        /// chronione progiem), rece wedlug zajecia dawcy.</summary>
        private static void ApplyFach(Ctx x, double[] p, double[] xs)
        {
            if (!x.FachEq) return;
            long t0 = Stopwatch.GetTimestamp();
            var sc = x.BlockScale; var og = x.FhOg; var inc = x.IncRaw; var hb = x.FhHandsBy;
            Array.Clear(hb, 0, hb.Length); x.FhHands = 0;
            for (int i = 0; i < x.FhN; i++)
            {
                double P = 0;
                for (int j = x.FhPs[i]; j < x.FhPs[i + 1]; j++) { int k = x.FhPk[j]; P += xs[k] * sc[x.Pb[k]] * og[k]; }
                double T = x.FhT[i], tol = 1e-9 * Math.Max(1.0, T);
                byte fl = 0;
                double gap = T - P;
                if (gap > tol)
                {
                    for (int q = x.FhOs[i]; q < x.FhOs[i + 1] && gap > tol; q++)
                    {
                        int b = x.FhOrd[q], kt = x.FhKt[b];
                        if (!(sc[b] > 0)) continue;
                        if (!(inc[kt] > 0)) { fl |= 2; continue; }                // praca typu nie daje wiecej, niz kosztuje (dochod przed podloga)
                        FachBlock(x, x.FhFeed[i], b, kt, xs, tol, ref gap, ref P, ref fl);
                    }
                    if (gap > tol && fl == 0) fl = 4;                              // bez bloku z praca typu do przesuniecia
                }
                x.FhAfter[i] = P; x.FhFlag[i] = fl;
            }
            x.FhTicks += Stopwatch.GetTimestamp() - t0;
        }

        /// <summary>m106 recenzja R1: para k bloku b moze oddac rece parze zajecia typu kt. Okreg objety progiem wyzywienia (feed): nie oddaje pary mieszanki jedzenia bloku
        /// (FA, FB - te, ktore prog i tak przywroci) ani pary dajacej wiecej kcal jedzenia niz para typu (PairKcal przy pelnej pracy - ta sama miara co prog) - fach nie robi
        /// okregu glodniejszym, a prog nie musi cofac fachu (dotad zabieralo zboze: wheat_farm 0.69 mln rak, cattle_farm:maslo 0.27 mln - Plan bez pokrycia W 8.0 -> 10.0%).</summary>
        private static bool FachDonor(Ctx x, bool feed, int b, int k, int kt)
        {
            if (!feed) return true;
            if (k == x.FA[b] || (x.FB[b] >= 0 && k == x.FB[b])) return false;
            return !(x.PairKcal[k] > x.PairKcal[kt]);
        }

        /// <summary>m106 recenzja R2: przesuniecie rak bloku b na pare typu kt - dawcy od pary z NAJNIZSZYM dochodem (IncRaw - dochod przed podloga; remis - kolejnosc par),
        /// kazdy tylko tyle, ile brakuje do celu, i tylko gdy typ daje z udzialu wiecej towaru niz dawca; ziemia bloku (ha zajete) najwyzej 0.98 x powierzchni - ruch, ktory bierze
        /// wiecej ziemi niz dawca, uciety do wolnej ziemi. Dotad wszyscy dawcy oddawali po rowno - najlepiej platna praca (zboze, ser) szla na tani towar typu (ryby W 0.24) i wies
        /// biedniala w Planie (sprzedaz wsi W -2%). Suma udzialow bloku bez zmian (nic z niczego). Flagi: 1 ziemia, 4 brak dawcy (wszystko juz w typie albo tylko pary chronione).</summary>
        private static void FachBlock(Ctx x, bool feed, int b, int kt, double[] xs, double tol, ref double gap, ref double P, ref byte fl)
        {
            var sc = x.BlockScale; var og = x.FhOg; var lp = x.LandP; var inc = x.IncRaw; var hb = x.FhHandsBy;
            int a = x.BStart[b], e = x.BStart[b + 1];
            double lu = 0; for (int k = a; k < e; k++) lu += xs[k] * lp[k];
            double room = 0.98 * x.Area[b] - lu;
            if (x.FhIdx == null || x.FhIdx.Length < e - a) x.FhIdx = new int[Math.Max(64, e - a)];
            var idx = x.FhIdx;
            int n = 0;
            for (int k = a; k < e; k++)
            {
                if (k == kt || !(xs[k] > 1e-12) || !FachDonor(x, feed, b, k, kt)) continue;
                int t = n++;                                                   // wstawianie rosnaco wedlug dochodu (k rosnie - remis zostaje w kolejnosci par)
                while (t > 0 && inc[idx[t - 1]].CompareTo(inc[k]) > 0) { idx[t] = idx[t - 1]; t--; }
                idx[t] = k;
            }
            if (n == 0) { fl |= 4; return; }
            double hands = x.W[b] * sc[b];
            for (int i = 0; i < n && gap > tol; i++)
            {
                int k = idx[i];
                double g = sc[b] * (og[kt] - og[k]);                           // towar fachu na przesuniety udzial
                if (!(g > 1e-12)) continue;
                double t = Math.Min(xs[k], gap / g);
                double dl = lp[kt] - lp[k];                                    // ha na przesuniety udzial (> 0 - typ bierze wiecej ziemi niz dawca)
                if (dl > 1e-12) { double lim = room > 0 ? room / dl : 0.0; if (lim < t) { t = lim; fl |= 1; } }
                if (!(t > 1e-15)) continue;
                xs[k] -= t; xs[kt] += t; room -= t * dl;
                P += t * g; gap -= t * g;
                hb[x.Pc[k]] += t * hands; x.FhHands += t * hands;
            }
        }

        /// <summary>m106 - RegaleStep, rynek z i towar regale g: kwota (kg/d) kazdego wiersza. Wiersze okregow, ktorych typ jest tym zajeciem regale, najpierw: okreg dostaje
        /// min(sufit swoich wierszy, baza na targ), razem najwyzej uzycie krainy (jedna skala na kraine - ten sam udzial dla kazdej wsi typu), rozlozone na jego wiersze wedlug sufitu;
        /// reszta uzycia krainy po staremu - proporcjonalnie do wolnego sufitu wierszy. Suma kwot krainy = uzycie krainy (podaz bez zmian). null - w tym rynku brak wierszy fachu
        /// (RegaleStep liczy po staremu).</summary>
        private static double[] FachRegale(Ctx x, int g, List<RegRow> rows, Dictionary<string, double> cap, Dictionary<string, double> use)
        {
            if (!x.In.FachOn || rows == null || rows.Count == 0) return null;
            long t0 = Stopwatch.GetTimestamp();
            Dictionary<string, Dictionary<KsDistrict, double>> byLand = null;
            for (int ri = 0; ri < rows.Count; ri++)
            {
                var r = rows[ri];
                if (!cap.ContainsKey(r.Land)) continue;
                var d = x.BD[r.B];
                if (d.TownFields || d.FachC != r.C || !(d.FachKg > 0)) continue;
                if (byLand == null) byLand = new Dictionary<string, Dictionary<KsDistrict, double>>(StringComparer.Ordinal);
                Dictionary<KsDistrict, double> m;
                if (!byLand.TryGetValue(r.Land, out m)) { m = new Dictionary<KsDistrict, double>(); byLand[r.Land] = m; }
                double c0; m.TryGetValue(d, out c0); m[d] = c0 + r.Cap;
            }
            if (byLand == null) { x.FhTicks += Stopwatch.GetTimestamp() - t0; return null; }
            var amt = new double[rows.Count];
            var first = new Dictionary<KsDistrict, double>();
            foreach (var kv in byLand)
            {
                double ul; use.TryGetValue(kv.Key, out ul);
                double want = 0;
                foreach (var dc in kv.Value) want += Math.Min(dc.Value, dc.Key.FachKg);
                double s = want > ul && want > 0 ? Math.Max(0.0, ul) / want : 1.0;
                foreach (var dc in kv.Value)
                {
                    double fd = Math.Min(dc.Value, dc.Key.FachKg) * s;
                    first[dc.Key] = fd;
                    x.FhRegT[dc.Key] = dc.Key.FachKg * s;
                    x.FhRegFirst += fd; x.FhRegTarget += dc.Key.FachKg;
                }
            }
            // wiersze fachu: czesc okregu wedlug sufitu wiersza
            for (int ri = 0; ri < rows.Count; ri++)
            {
                var r = rows[ri];
                if (!cap.ContainsKey(r.Land)) continue;
                var d = x.BD[r.B];
                double fd; Dictionary<KsDistrict, double> m;
                if (!first.TryGetValue(d, out fd) || d.FachC != r.C || !byLand.TryGetValue(r.Land, out m)) continue;
                double cd; m.TryGetValue(d, out cd);
                amt[ri] = cd > 0 ? fd * r.Cap / cd : 0.0;
            }
            // reszta uzycia krainy po staremu - proporcjonalnie do wolnego sufitu (kraina bez wierszy fachu: jak dotad, uzycie / sufit)
            var rest = new Dictionary<string, double>(StringComparer.Ordinal); var free = new Dictionary<string, double>(StringComparer.Ordinal);
            for (int ri = 0; ri < rows.Count; ri++)
            {
                var r = rows[ri];
                if (!cap.ContainsKey(r.Land)) continue;
                double a; free.TryGetValue(r.Land, out a); free[r.Land] = a + Math.Max(0.0, r.Cap - amt[ri]);
                rest.TryGetValue(r.Land, out a); rest[r.Land] = a + amt[ri];
            }
            for (int ri = 0; ri < rows.Count; ri++)
            {
                var r = rows[ri];
                double cl, ul;
                if (!cap.TryGetValue(r.Land, out cl) || !(cl > 0) || !use.TryGetValue(r.Land, out ul) || !(ul > 0)) { amt[ri] = 0; continue; }
                double fr, given; free.TryGetValue(r.Land, out fr); rest.TryGetValue(r.Land, out given);
                double left = Math.Max(0.0, ul - given);
                double f2 = fr > 0 ? Math.Min(1.0, left / fr) : 0.0;
                amt[ri] += Math.Max(0.0, r.Cap - amt[ri]) * f2;
            }
            x.FhTicks += Stopwatch.GetTimestamp() - t0;
            return amt;
        }

        /// <summary>m106 - Finish: fach okregow w wyniku (DistFach - gra: cel na targ dla reguly A7) i linia "KS: fach wsi (m106)". Produkcja z udzialow wyniku (FOg ostatniej
        /// rownowagi - te same pary; regale - podaz okregu z RegaleStep). Liczby przyczyn z ostatniej rundy rownowagi.</summary>
        private static void FachFinish(Ctx x, double[] xs)
        {
            var R = x.R;
            if (!x.In.FachOn) { R.DistFach = null; R.FachLine = null; return; }
            long t0 = Stopwatch.GetTimestamp();
            var inv = CultureInfo.InvariantCulture;
            var map = new Dictionary<string, KsFach>(StringComparer.Ordinal);
            double sig = Math.Max(1.0, x.In.Sigma);
            Func<int, double> pcs = g => { KsItem it; return sig * (x.In.Items.TryGetValue(KsTab.Goods[g], out it) && it != null && it.Kg > 0 ? it.Kg : 10.0); };
            double A = 0, B = 0; int full = 0, part = 0, cLand = 0, cProfit = 0, cFloor = 0, nReg = 0;
            var worst = new List<KeyValuePair<double, string>>();
            var sc = x.BlockScale;
            for (int i = 0; i < x.FhN; i++)
            {
                var d = x.FhD[i]; int g = x.FhG[i];
                double P = 0;
                for (int j = x.FhPs[i]; j < x.FhPs[i + 1]; j++) { int k = x.FhPk[j]; P += xs[k] * sc[x.Pb[k]] * x.FhOg[k]; }
                double[] rb; if (x.RegByDist != null && x.RegByDist.TryGetValue(d.Id, out rb) && rb != null) P += rb[g];
                double T = x.FhT[i], own = x.FhOwn[i], Tm = Math.Max(0.0, T - own);
                int flag = 0;
                if (P < 0.99 * T - 1e-6)
                {
                    byte fl = x.FhFlag[i];
                    if (P < x.FhAfter[i] - 1e-6 * Math.Max(1.0, T)) flag = 3;
                    else if ((fl & 5) != 0) flag = 1;
                    else if ((fl & 2) != 0) flag = 2;
                    else flag = 1;
                }
                map[d.Id] = new KsFach { G = g, T = Tm, P = P, Own = own, K = x.FhK[i], Flag = flag };
                double u = pcs(g);
                A += Tm / u; double got = Math.Min(Tm, Math.Max(0.0, P - own)); B += got / u;
                if (flag == 0) full++; else { part++; if (flag == 1) cLand++; else if (flag == 2) cProfit++; else cFloor++; }
                if (Tm > 0 && got < 0.95 * Tm) worst.Add(new KeyValuePair<double, string>((Tm - got) / u, d.Id + " " + KsTab.Goods[g] + " " + (Tm / u).ToString("0.0", inv) + "/" + (got / u).ToString("0.0", inv)));
            }
            if (x.FhRegT != null)
                foreach (var kv in x.FhRegT)
                {
                    var d = kv.Key; int g = d.FachC >= 0 && d.FachC < KsTab.NC ? KsTab.RegGood(d.FachC) : -1;
                    if (g < 0) continue;
                    double P = 0; double[] rb;
                    if (x.RegByDist != null && x.RegByDist.TryGetValue(d.Id, out rb) && rb != null) P = rb[g];
                    double T = kv.Value;
                    int flag = P < 0.99 * T - 1e-6 ? 1 : 0;
                    map[d.Id] = new KsFach { G = g, T = T, P = P, Own = 0, K = d.FachKg > 0 ? T / d.FachKg : 0, Flag = flag, Reg = true };
                    nReg++;
                    double u = pcs(g);
                    A += T / u; double got = Math.Min(T, P); B += got / u;
                    if (flag == 0) full++; else { part++; cLand++; }
                    if (T > 0 && got < 0.95 * T) worst.Add(new KeyValuePair<double, string>((T - got) / u, d.Id + " " + KsTab.Goods[g] + " " + (T / u).ToString("0.0", inv) + "/" + (got / u).ToString("0.0", inv)));
                }
            R.DistFach = map;
            worst.Sort((p1, p2) => p2.Key.CompareTo(p1.Key));
            var donors = new List<int>(); for (int c = 0; c < KsTab.NC; c++) if (x.FhHandsBy[c] > 0) donors.Add(c);
            donors.Sort((c1, c2) => x.FhHandsBy[c2].CompareTo(x.FhHandsBy[c1]));
            x.FhTicks += Stopwatch.GetTimestamp() - t0;
            R.FachMs = x.FhTicks * 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder(1600);
            sb.Append("KS: fach wsi (m106) plan doba {DOBA} | klucz TAK | wsi z fachem ").Append(x.FhN + nReg).Append(" (regale ").Append(nReg).Append(")")
              .Append(" | k: ").Append(x.FhKList != null && x.FhKList.Count > 0 ? string.Join(", ", x.FhKList.ToArray()) : "-")
              .Append(" | cel na targ ").Append(A.ToString("0", inv)).Append(" szt./d, w Planie ").Append(B.ToString("0", inv)).Append(" (").Append(A > 0 ? (100.0 * B / A).ToString("0", inv) : "-").Append("%)")
              .Append(", w pelni ").Append(full).Append(", czesciowo ").Append(part).Append(" (rece/ziemia ").Append(cLand).Append(", bez zysku ").Append(cProfit).Append(", prog wyzywienia ").Append(cFloor).Append(")")
              .Append(" | rece do fachu ").Append((x.FhHands / 1000.0).ToString("0.0", inv)).Append(" tys. (z: ");
            if (donors.Count == 0) sb.Append('-');
            for (int i = 0; i < donors.Count && i < 6; i++) sb.Append(i > 0 ? ", " : "").Append(KsTab.Crafts[donors[i]]).Append(' ').Append((x.FhHandsBy[donors[i]] / 1000.0).ToString("0.0", inv));
            sb.Append(") | regale: wsie typu najpierw ").Append((x.FhRegFirst / 1000.0).ToString("0.0", inv)).Append(" t/d z celu ").Append((x.FhRegTarget / 1000.0).ToString("0.0", inv)).Append(" t/d")
              .Append(" | najwiekszy niedobor (cel/w Planie szt./d): ");
            if (worst.Count == 0) sb.Append('-');
            for (int i = 0; i < worst.Count && i < 6; i++) sb.Append(i > 0 ? ", " : "").Append(worst[i].Value);
            sb.Append(" | czas fach ").Append(R.FachMs.ToString("0", inv)).Append(" ms.");
            R.FachLine = sb.ToString();
        }
    }
}
