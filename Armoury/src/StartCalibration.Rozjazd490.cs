using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 490 (audyt 13.11, ekonomia pkt 1 - SAM POMIAR): rozjazd Plan - gra dla zboza, miesa (mieso + wieloryb) i zywca (cow, hog, sheep), wedlug rynku W/E.
    /// Ksiega d108865: mieso W 607 szt. wobec Planu 763 (0.80), E 0.85; zboze W 1.17, E 1.08 - Deliver.cs: Plan liczy produkcje "bez zimy i czynnikow rak", gra doklada
    /// ScaleRates i domieszke beta. Linia "KS: rozjazd Plan - gra (490)" co Plan (7 dob, przy odbiorze na watku glownym) rozbija droge towaru na kroki:
    ///  Plan (KsDeliver.Produce na rekach Planu, zjedzone w okregu, na targ) -> gra: Produce na rekach i zajeciach zywych (regula A7, ludzie doby) -> dodatki przed zima
    ///  (sprzet 435-L3, stado R3-06c, zamowienia 363, prace okregu m114) -> x zima (WinterBite) -> x rece (spustoszenie E4/5 i wojna E4/7) -> rok urodzaju, ziemia
    ///  zmeczona, towar zamowien -> minus wlasny stol -> na targ -> stawka po ScaleRates (drogi i budynki BK, 338) -> wsie Normal z miastem targowym (ProdStep: inne 0)
    ///  -> x beta rampy -> ksiega towarow: plon wsi (GVil) wedlug rynku, srednia z dob od poprzedniej linii. Pola mieszczan osobno (x beta, ksiega GKs).
    /// Kroki gry z ostatniego rachunku kazdego okregu (co 7 dob na zmiane - jak stawki), w sztukach gry na dobe (kg / (sigma x kg sztuki)).
    /// Plan zbierany na watku roboczym (PlanMarketSums, w KsResult), linia skladana na watku glownym. Nic nie zmienia w grze.
    /// </summary>
    internal static partial class StartCalibration
    {
        // grupy towarow: 0 zboze, 1 mieso (mieso + wieloryb - CtlGood miesa), 2 zywiec (cow, hog, sheep)
        private const int G490 = 3;
        private static readonly string[] G490Name = { "zboze", "mieso (mieso + wieloryb)", "zywiec (cow/hog/sheep)" };
        private static readonly int[][] G490Ix = { new[] { KsDeliver.IGrain }, new[] { KsDeliver.IMeat, KsDeliver.IWhaleMeat }, new[] { KsDeliver.ICow, KsDeliver.IHog, KsDeliver.ISheep } };
        // kroki gry w DistResult.S490 [k * G490 + g]: 0 Produce, 1 po dodatkach przed zima, 2 x sama zima, 3 x zima x rece, 4 po roku urodzaju / ziemi / zamowieniach,
        // 5 wlasny stol (Own), 6 na targ (Market)
        private const int K490 = 7;
        private static int _r490St;

        private static int G490Of(int i)
        {
            for (int g = 0; g < G490; g++) if (Array.IndexOf(G490Ix[g], i) >= 0) return g;
            return -1;
        }

        /// <summary>490: krok k drogi towaru okregu - kg/d z prod (x mul) w sztukach gry na dobe do s. Tylko odczyt prod. Nie rzuca.</summary>
        private static void S490Snap(double[] s, int k, double[] prod, double[] kgT, double mul)
        {
            if (s == null || prod == null || kgT == null) return;
            try
            {
                double sig = Math.Max(1, DailyLife.KsSigma);
                for (int g = 0; g < G490; g++)
                {
                    double v = 0;
                    foreach (int i in G490Ix[g]) if (i < prod.Length && i < kgT.Length && kgT[i] > 0) v += Math.Max(0.0, prod[i]) * mul / (sig * kgT[i]);
                    s[k * G490 + g] = v;
                }
            }
            catch { _r490St++; }
        }

        /// <summary>490: czynnik samej zimy okregu (to samo co w ComputeDistrict: 1 - VillageCut wsi albo 1 - TownCut pol mieszczan). Nie rzuca (blad - 1).</summary>
        private static double W490(LiveDist ld)
        {
            try
            {
                double f = ld.TF ? 1.0 - WinterBite.TownCut(ld.T) : (ld.V != null ? 1.0 - WinterBite.VillageCut(ld.V) : 1.0);
                return f >= 0 && !double.IsInfinity(f) ? f : 1.0;
            }
            catch { _r490St++; return 1.0; }
        }

        /// <summary>490: koniec rachunku okregu w grze - wlasny stol i na targ do krokow 5 i 6. Nie rzuca.</summary>
        private static void S490End(DistResult res, double[] kgT)
        {
            if (res == null || res.S490 == null) return;
            S490Snap(res.S490, 5, res.Own, kgT, 1.0);
            S490Snap(res.S490, 6, res.MarketKg, kgT, 1.0);
        }

        /// <summary>490 (watek roboczy albo glowny): Plan - okreg ld z rachunkiem bez czynnikow: [((m * 2 + tf) * G490 + g) * 3 + k], k 0 produkcja, 1 zjedzone, 2 na targ (szt./d).</summary>
        private static void P490Add(LiveDist ld, DistResult res, double[] a, double[] kgT)
        {
            if (ld == null || res == null || a == null || kgT == null) return;
            double sig = Math.Max(1, DailyLife.KsSigma);
            int o = ((ld.Market == 0 ? 0 : 1) * 2 + (ld.TF ? 1 : 0)) * G490 * 3;
            for (int g = 0; g < G490; g++)
                foreach (int i in G490Ix[g])
                {
                    if (i >= kgT.Length || !(kgT[i] > 0)) continue;
                    double d = sig * kgT[i];
                    a[o + g * 3] += Math.Max(0.0, res.Prod[i]) / d; a[o + g * 3 + 1] += Math.Max(0.0, res.Own[i]) / d; a[o + g * 3 + 2] += Math.Max(0.0, res.MarketKg[i]) / d;
                }
        }

        private static readonly Dictionary<string, long[]> _v490Tmp = new Dictionary<string, long[]>(StringComparer.Ordinal);

        /// <summary>490: linia "KS: rozjazd Plan - gra (490)" (watek glowny, odbior Planu). Tylko odczyt.</summary>
        private static string Rozjazd490Line(KsResult r)
        {
            var inv = Inv;
            var st = new double[2 * 2 * G490 * K490];    // [((m * 2 + tf) * K490 + k) * G490 + g]
            var nd = new int[4];
            var ra = new double[2 * G490 * 3];           // wsie: [(m * G490 + g) * 3 + k]: 0 stawka po ScaleRates, 1 tylko wsie Normal z miastem targowym, 2 to samo x beta
            var tfb = new double[2 * G490];              // pola mieszczan: na targ x beta
            double sig = Math.Max(1, DailyLife.KsSigma);
            var rates = _rates; int noRes = 0, notNormal = 0;
            if (_live != null)
                foreach (var ld in _live.Values)
                {
                    try
                    {
                        if (ld == null) continue;
                        var res = ld.Res;
                        if (res == null || res.S490 == null) { noRes++; continue; }
                        int m = ld.Market == 0 ? 0 : 1, tf = ld.TF ? 1 : 0;
                        nd[m * 2 + tf]++;
                        int o = (m * 2 + tf) * K490 * G490;
                        for (int x = 0; x < K490 * G490; x++) st[o + x] += res.S490[x];
                        if (ld.TF)
                        {
                            for (int g = 0; g < G490; g++) foreach (int i in G490Ix[g]) tfb[m * G490 + g] += Math.Max(0.0, res.Market[i]) * Beta(m, i);
                            continue;
                        }
                        VRate vr;
                        if (ld.V == null || rates == null || !rates.TryGetValue(ld.V, out vr) || vr == null || vr.A == null) continue;
                        int mr = vr.M == 0 ? 0 : 1;
                        bool ok = ld.V.VillageState == Village.VillageStates.Normal && ld.V.TradeBound != null;   // jak ProdStep: inna wies - 0
                        if (!ok) notNormal++;
                        for (int g = 0; g < G490; g++)
                            foreach (int i in G490Ix[g])
                            {
                                if (i >= vr.A.Length) continue;
                                double a = Math.Max(0.0, vr.A[i]);
                                int q = (mr * G490 + g) * 3;
                                ra[q] += a; if (ok) { ra[q + 1] += a; ra[q + 2] += a * Beta(mr, i); }
                            }
                    }
                    catch { _r490St++; }
                }
            // ksiega towarow: plon wsi (GVil) i pola mieszczan (GKs) wedlug rynku od poprzedniej linii
            var led = new double[2 * 2 * G490];          // [(m * 2 + tf) * G490 + g] szt./d
            int days = 0;
            try
            {
                days = GoodsLedger.V490Take(_v490Tmp);
                if (days > 0)
                    foreach (var kv in _v490Tmp)
                    {
                        int i = Array.IndexOf(ItemIds, kv.Key); int g = i >= 0 ? G490Of(i) : -1;
                        if (g < 0 || kv.Value == null || kv.Value.Length < 4) continue;
                        for (int m = 0; m < 2; m++) { led[(m * 2) * G490 + g] += kv.Value[m] / (double)days; led[(m * 2 + 1) * G490 + g] += kv.Value[2 + m] / (double)days; }
                    }
            }
            catch { days = 0; _r490St++; }
            var P = r != null ? r.P490 : null;
            var sb = new StringBuilder(5000);
            sb.Append(PK).Append("rozjazd Plan - gra (490) plan doba ").Append(_wDay)
              .Append(" (sam pomiar; szt. gry/d = kg / (sigma x kg sztuki), sigma ").Append(sig.ToString("0", inv))
              .Append("; Plan - KsDeliver.Produce na rekach Planu; gra - ostatni rachunek kazdego okregu (co 7 dob na zmiane); ksiega - plon wsi (wsie) i pola mieszczan (KS) wedlug rynku, srednia z ")
              .Append(days).Append(" dob od poprzedniej linii)");
            string[] mn = { "Westeros", "Essos" };
            for (int m = 0; m < 2; m++)
                for (int tf = 0; tf < 2; tf++)
                {
                    sb.Append(" | ").Append(mn[m]).Append(tf == 0 ? " wsie (" : " pola mieszczan (").Append(nd[m * 2 + tf]).Append(" okregow)");
                    for (int g = 0; g < G490; g++)
                    {
                        int o = (m * 2 + tf) * K490 * G490;
                        Func<int, double> Sx = k => st[o + k * G490 + g];
                        sb.Append(g == 0 ? ": " : "; ").Append(G490Name[g]).Append(':');
                        double pm = double.NaN;
                        if (P != null)
                        {
                            int p = ((m * 2 + tf) * G490 + g) * 3;
                            pm = P[p + 2];
                            sb.Append(" Plan produkcja ").Append(P[p].ToString("0.0", inv)).Append(", zjedzone ").Append(P[p + 1].ToString("0.0", inv)).Append(", na targ ").Append(pm.ToString("0.0", inv));
                        }
                        else sb.Append(" Plan brak");
                        double s1 = Sx(1), s3 = Sx(3);
                        sb.Append(" -> gra: Produce ").Append(Sx(0).ToString("0.0", inv))
                          .Append(", + dodatki przed zima (sprzet, stado, zamowienia, prace) ").Append(Sx(1).ToString("0.0", inv))
                          .Append(", x zima ").Append(Sx(2).ToString("0.0", inv)).Append(" (").Append(s1 > 1e-9 ? (Sx(2) / s1).ToString("0.000", inv) : "-").Append(')')
                          .Append(", x rece (spustoszenie, wojna) ").Append(s3.ToString("0.0", inv)).Append(" (").Append(Sx(2) > 1e-9 ? (s3 / Sx(2)).ToString("0.000", inv) : "-").Append(')')
                          .Append(", rok urodzaju / ziemia / zamowienia ").Append(Sx(4).ToString("0.0", inv))
                          .Append(", - wlasny stol ").Append(Sx(5).ToString("0.0", inv)).Append(" = na targ ").Append(Sx(6).ToString("0.0", inv));
                        double last;
                        if (tf == 0)
                        {
                            int q = (m * G490 + g) * 3;
                            sb.Append(" -> stawka po ScaleRates ").Append(ra[q].ToString("0.0", inv)).Append(", wsie Normal z targiem ").Append(ra[q + 1].ToString("0.0", inv))
                              .Append(", x beta ").Append(ra[q + 2].ToString("0.0", inv));
                            last = ra[q + 2];
                        }
                        else { sb.Append(" -> x beta ").Append(tfb[m * G490 + g].ToString("0.0", inv)); last = tfb[m * G490 + g]; }
                        double lg = led[(m * 2 + tf) * G490 + g];
                        sb.Append(" -> ksiega ").Append(days > 0 ? lg.ToString("0.0", inv) : "brak");
                        if (pm > 1e-9)
                            sb.Append(" (wobec Planu na targ: gra na targ ").Append((Sx(6) / pm).ToString("0.00", inv)).Append(", po stawce ").Append((last / pm).ToString("0.00", inv))
                              .Append(", ksiega ").Append(days > 0 ? (lg / pm).ToString("0.00", inv) : "-").Append(')');
                    }
                }
            sb.Append(" | okregow bez rachunku 490 (przed pierwszym przeliczeniem po wczytaniu) ").Append(noRes).Append(", wsi nie Normal albo bez targu ").Append(notNormal)
              .Append(" | Plan: ").Append(P != null ? "z watku roboczego albo odbioru" : "brak (potkniecie albo Plan bez sum)").Append(r != null ? ", potkniecia Planu " + r.P490St : "")
              .Append(" | potkniecia pomiaru ").Append(_r490St).Append(" | gra bez zmian.");
            return sb.ToString();
        }
    }

    internal sealed partial class KsResult
    {
        // 490 (sam pomiar): Plan wedlug rynku, wsi / pol mieszczan i grupy towaru (zboze, mieso, zywiec) - produkcja, zjedzone, na targ w szt. gry/d; null - brak
        public double[] P490; public int P490St;
    }
}
