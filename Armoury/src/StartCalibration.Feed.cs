using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Armoury
{
    // r4-polnoc (Jeff 17.10, STAN-PRAC ### 72: "Polnoc i za Murem - najpierw wyzywic siebie (hodowla, ryby, owies)"; PRZEKAZANIE-NOC 5A krok 3).
    // Czysty C# (bez typow gry) - ten sam plik kompiluje harness Planu (kmoney).

    internal sealed partial class KsInput
    {
        /// <summary>r4-polnoc "najpierw wyzywic siebie": 0 - wylaczone (Plan jak dotad, bit w bit), 1 - okregi wsi stref zimnych (1-arktyka, 2-wyspy, 2-zimno: Polnoc,
        /// za Murem, Nocna Straz, Skagos, Zelazne Wyspy, Ib), 2 - wszystkie okregi wsi swiata (klucz Ks Self Feed Everywhere).</summary>
        public int SelfFeed;
    }

    /// <summary>r4-polnoc: wyzywienie krainy w Planie (kcal surowca na dobe): potrzeba jedzenia wsi i miast, produkcja jedzenia okregow (zajecia z rak i ziemi), praca
    /// domowa (krok 8), prog "najpierw wyzywic siebie".</summary>
    internal sealed class KsFeedLand
    {
        public string Land; public int M;
        public double VilPeople, TownPeople, VilNeed, TownNeed, VilProd, TfProd, Home, Hands, FloorHands, Target, Cap, Got;
        public int Dist, DistFloor, DistShort;
        public double Vil { get { return VilNeed > 0 ? (VilProd + Home) / VilNeed : 0.0; } }
        public double All { get { double n = VilNeed + TownNeed; return n > 0 ? (VilProd + TfProd + Home) / n : 0.0; } }
    }

    internal sealed partial class KsResult
    {
        public int SelfFeed = -1; public bool ColdLand;
        public Dictionary<string, double> DistHomeKcalAbs = new Dictionary<string, double>(StringComparer.Ordinal);   // krok 8: kcal/d robione w domu (ostatnia runda)
        public List<KsFeedLand> Feed;                                     // wyzywienie krain (Finish)
        public int FeedBlocks, FeedBind; public double FeedSumDev, FeedHandsAll, FeedFloorHands;
    }

    internal static partial class KsCore
    {
        private sealed partial class Ctx
        {
            public bool FeedOn;                                        // r4-polnoc: prog "najpierw wyzywic siebie" czynny w tej rownowadze
            public double[] PairKcal;                                  // kcal jedzenia pary przy udziale 1 (OUTP z warstwa dom i zbozem wlasnym)
            public int[] FA, FB; public double[] FSa, FSb, FSum, FK;  // blok: zadanie liniowe bloku - pary jedzenia (najwyzej dwie), udzialy, suma udzialow, kcal (bez skali regale)
            public double[] Lam, FC, FG;                               // blok: czesc rak przesunieta w ostatniej rundzie, kcal bloku przy logicie, zysk kcal
            public int[] FdB0, FdB1; public KsDistrict[] FdD; public double[] FdNeed, FdDef, FdCap, FdGot;   // okregi w zakresie: bloki, potrzeba bez daktyli regale, brak, zdolnosc, z progu
            public int Fd;
            public int[] FOrd; public double[] FEff;                   // bufor kolejki blokow okregu
        }

        internal static HashSet<string> FeedOnlyLands;                    // harness: prog tylko w tych krainach (diagnoza); w grze null

        /// <summary>Okreg objety progiem: wies z ludzmi (pola mieszczan nie - miasto zyje z targu), zakres 1 - strefa zimna (strefa z kultury, ta sama dla calego okregu).</summary>
        private static bool InFeedScope(KsDistrict d, int mode, KsBlock b0)
        {
            if (d == null || d.TownFields || !(d.People > 0) || mode <= 0) return false;
            if (FeedOnlyLands != null && !FeedOnlyLands.Contains(d.Land ?? "?")) return false;
            if (mode >= 2) return true;
            return b0 != null && b0.O != null && KsTab.ColdZone(b0.O.Zone);
        }

        /// <summary>
        /// r4-polnoc - PROG "NAJPIERW WYZYWIC SIEBIE", przygotowanie (na poczatku kazdej rownowagi, po RegaleStep; koszt O(par)):
        ///  - potrzeba okregu = jedzenie jego ludzi w kcal surowca (KsTab.FoodNeedKcal z potrzeb startu; bez warstwy dom - H robi dom) minus daktyle regale okregu;
        ///  - blok: zadanie liniowe - najwiecej kcal jedzenia z par bloku (zajecia, ktore ziemia bloku daje: kandydaci Planu, land >= 0.3) przy udzialach &lt;= 1 i ziemi
        ///    &lt;= 0.98 x powierzchni bloku; najwyzej dwie pary (dwa ograniczenia: rece i ziemia - np. za Murem owce na ziemi i mysliwi na dzikiej ziemi, rybacy przy brzegu).
        /// Kazda runda (ApplyFloors): okreg liczy jedzenie przy udzialach logitu (rynek); gdy brakuje do potrzeby - przesuwa rece z mieszanki rynku do mieszanki jedzenia
        /// bloku, od bloku z najwiekszym zyskiem kcal na przesunieta reke, tylko tyle, ile brakuje ("na ile ziemia i rece pozwola"). Okreg, ktory rynek sam wyzywia, nie
        /// zmienia nic. Nic z niczego: prog przesuwa tylko rece bloku miedzy jego zajeciami (suma udzialow = 1), produkcja dalej rece x tabela swiata x ziemia.
        /// </summary>
        private static void FloorStep(Ctx x)
        {
            int mode = x.In.SelfFeed;
            x.FeedOn = false;
            if (mode <= 0) return;
            int NP = x.NP, NB = x.NB;
            var fk = KsTab.FoodKcal;
            if (x.PairKcal == null || x.PairKcal.Length != NP) x.PairKcal = new double[NP];
            for (int k = 0; k < NP; k++)
            {
                double s = 0;
                for (int j = x.OStart[k]; j < x.OStart[k + 1]; j++) s += x.OVal[j] * fk[x.OGood[j]];
                x.PairKcal[k] = s;
            }
            if (x.FA == null || x.FA.Length != NB)
            {
                x.FA = new int[NB]; x.FB = new int[NB]; x.FSa = new double[NB]; x.FSb = new double[NB]; x.FSum = new double[NB]; x.FK = new double[NB];
                x.Lam = new double[NB]; x.FC = new double[NB]; x.FG = new double[NB];
            }
            for (int b = 0; b < NB; b++) { x.FA[b] = x.FB[b] = -1; x.FSa[b] = x.FSb[b] = x.FSum[b] = x.FK[b] = x.Lam[b] = 0; }
            var b0l = new List<int>(); var b1l = new List<int>(); var dl = new List<KsDistrict>(); var nl = new List<double>();
            int maxB = 1, b0 = 0;
            while (b0 < NB)
            {
                var d = x.BD[b0];
                int b1 = b0 + 1;
                while (b1 < NB && ReferenceEquals(x.BD[b1], d)) b1++;       // bloki okregu sa kolejne (Prepare)
                if (InFeedScope(d, mode, x.B[b0]))
                {
                    double need = KsTab.FoodNeedKcal(d.Need0);
                    double[] rb;
                    if (x.RegByDist != null && x.RegByDist.TryGetValue(d.Id, out rb) && rb != null) for (int g = 0; g < KsTab.NG; g++) need -= rb[g] * fk[g];
                    for (int b = b0; b < b1; b++)
                    {
                        int a = x.BStart[b], e = x.BStart[b + 1];
                        double A = x.Area[b] * 0.98, best = 0, qi = 0, qj = 0;
                        int pi = -1, pj = -1;
                        for (int k = a; k < e; k++)
                        {
                            double F = x.PairKcal[k];
                            if (!(F > 1e-9)) continue;
                            double L = x.LandP[k], s = L > A ? A / L : 1.0;
                            if (F * s > best) { best = F * s; pi = k; qi = s; pj = -1; qj = 0; }
                            if (!(L > A)) continue;                         // wierzcholek obu ograniczen: k sam sie nie miesci, k2 tak
                            for (int k2 = a; k2 < e; k2++)
                            {
                                if (k2 == k) continue;
                                double F2 = x.PairKcal[k2], L2 = x.LandP[k2];
                                if (!(F2 > 1e-9) || !(L2 < A)) continue;
                                double s1 = (A - L2) / (L - L2), s2 = 1.0 - s1, v = F * s1 + F2 * s2;
                                if (s1 > 0 && s2 > 0 && v > best) { best = v; pi = k; qi = s1; pj = k2; qj = s2; }
                            }
                        }
                        if (pi < 0 || !(best > 0)) continue;
                        x.FA[b] = pi; x.FSa[b] = qi; x.FB[b] = pj; x.FSb[b] = qj; x.FSum[b] = qi + qj; x.FK[b] = best;
                    }
                    if (need > 0) { b0l.Add(b0); b1l.Add(b1); dl.Add(d); nl.Add(need); if (b1 - b0 > maxB) maxB = b1 - b0; }
                }
                b0 = b1;
            }
            x.Fd = dl.Count;
            x.FdB0 = b0l.ToArray(); x.FdB1 = b1l.ToArray(); x.FdD = dl.ToArray(); x.FdNeed = nl.ToArray();
            x.FdDef = new double[x.Fd]; x.FdCap = new double[x.Fd]; x.FdGot = new double[x.Fd];
            x.FOrd = new int[maxB]; x.FEff = new double[maxB];
            x.FeedOn = x.Fd > 0;
        }

        /// <summary>Prog w udzialach rownowagi (po logicie, kazda runda): okreg z brakiem jedzenia przesuwa czesc lam rak bloku z mieszanki rynku do mieszanki jedzenia
        /// bloku: x = x (1 - lam suma s*) + lam s* (suma udzialow = 1); bloki od najwiekszego zysku kcal na przesunieta reke, lam tylko na brak. Okreg wyzywiony
        /// przez rynek - udzialy bez zmian.</summary>
        private static void ApplyFloors(Ctx x, double[] xs)
        {
            if (!x.FeedOn) return;
            var pk = x.PairKcal; var sc = x.BlockScale; var C = x.FC; var G = x.FG; var ord = x.FOrd; var ev = x.FEff;
            for (int di = 0; di < x.Fd; di++)
            {
                int b0 = x.FdB0[di], b1 = x.FdB1[di];
                double tot = 0;
                for (int b = b0; b < b1; b++)
                {
                    double c = 0;
                    for (int k = x.BStart[b]; k < x.BStart[b + 1]; k++) c += xs[k] * pk[k];
                    c *= sc[b]; C[b] = c; tot += c; x.Lam[b] = 0;
                }
                double def = x.FdNeed[di] - tot;
                x.FdDef[di] = def > 0 ? def : 0; x.FdGot[di] = 0; x.FdCap[di] = 0;
                if (!(def > 1e-6)) continue;
                int n = 0; double cap = 0;
                for (int b = b0; b < b1; b++)
                {
                    if (x.FA[b] < 0) continue;
                    double g = sc[b] * x.FK[b] - x.FSum[b] * C[b];              // kcal bloku przy lam = 1 minus dzis: s* F + (1 - suma s*) C - C
                    if (!(g > 1e-9)) continue;
                    double hands = x.W[b] * sc[b] * x.FSum[b];
                    G[b] = g; cap += g;
                    double e = hands > 1e-9 ? g / hands : 0.0;
                    int t = n++;
                    while (t > 0 && ev[t - 1] < e) { ev[t] = ev[t - 1]; ord[t] = ord[t - 1]; t--; }   // malejaco wedlug zysku kcal na reke
                    ev[t] = e; ord[t] = b;
                }
                x.FdCap[di] = cap;
                double rem = def, got = 0;
                for (int t = 0; t < n && rem > 1e-6; t++)
                {
                    int b = ord[t];
                    double lam = Math.Min(1.0, rem / G[b]), f = 1.0 - lam * x.FSum[b];
                    for (int k = x.BStart[b]; k < x.BStart[b + 1]; k++) xs[k] *= f;
                    xs[x.FA[b]] += lam * x.FSa[b];
                    if (x.FB[b] >= 0) xs[x.FB[b]] += lam * x.FSb[b];
                    x.Lam[b] = lam; rem -= lam * G[b]; got += lam * G[b];
                }
                x.FdGot[di] = got;
            }
        }

        /// <summary>Wyzywienie krain z wyniku (Finish): produkcja jedzenia okregow (DistProd, kcal surowca), praca domowa (krok 8), potrzeba wsi i miast, prog (ostatnia
        /// runda rownowagi); kontrola rak (suma udzialow bloku okregu w zakresie = 1).</summary>
        private static void FeedStats(Ctx x, double[] xs)
        {
            var R = x.R;
            R.SelfFeed = x.In.SelfFeed; R.ColdLand = KsTab.ColdLand;
            var fk = KsTab.FoodKcal;
            var map = new Dictionary<string, KsFeedLand>(StringComparer.Ordinal);
            Func<string, int, KsFeedLand> get = (l, m) =>
            {
                KsFeedLand f; string key = l ?? "?";
                if (!map.TryGetValue(key, out f)) { f = new KsFeedLand { Land = key, M = m == 0 ? 0 : 1 }; map[key] = f; }
                return f;
            };
            foreach (var d in x.D)
            {
                var f = get(d.Land, d.Market);
                double kc = 0; double[] pr;
                if (R.DistProd != null && R.DistProd.TryGetValue(d.Id, out pr) && pr != null) for (int g = 0; g < KsTab.NG; g++) kc += pr[g] * fk[g];
                if (d.TownFields) { f.TfProd += kc; continue; }
                f.VilProd += kc; f.VilNeed += KsTab.FoodNeedKcal(d.Need0); f.VilPeople += d.People; f.Dist++;
                double h; if (R.DistHomeKcalAbs.TryGetValue(d.Id, out h)) f.Home += h;
            }
            foreach (var t in x.In.Towns) { var f = get(t.Land, t.Market); f.TownNeed += KsTab.FoodNeedKcal(t.Need0); f.TownPeople += t.People; }
            double dev = 0, hall = 0, hfl = 0; int nb = 0, nbind = 0;
            for (int b = 0; b < x.NB; b++)
            {
                var d = x.BD[b];
                if (!d.TownFields) { double w = x.W[b] * x.BlockScale[b]; get(d.Land, d.Market).Hands += w; hall += w; }
            }
            if (x.FeedOn)
            {
                for (int di = 0; di < x.Fd; di++)
                {
                    var d = x.FdD[di]; var f = get(d.Land, d.Market);
                    f.Target += x.FdDef[di]; f.Cap += x.FdCap[di]; f.Got += x.FdGot[di];
                    if (x.FdDef[di] > 1.0) f.DistFloor++;
                    if (x.FdDef[di] > x.FdCap[di] * 1.0001 + 1.0) f.DistShort++;
                    for (int b = x.FdB0[di]; b < x.FdB1[di]; b++)
                    {
                        nb++;
                        double s = 0;
                        for (int k = x.BStart[b]; k < x.BStart[b + 1]; k++) s += xs[k];
                        dev = Math.Max(dev, Math.Abs(s - 1.0));
                        if (x.Lam[b] > 0)
                        {
                            nbind++;
                            double hh = x.Lam[b] * x.FSum[b] * x.W[b] * x.BlockScale[b];
                            f.FloorHands += hh; hfl += hh;
                        }
                    }
                }
            }
            R.FeedBlocks = nb; R.FeedBind = nbind; R.FeedSumDev = dev; R.FeedHandsAll = hall; R.FeedFloorHands = hfl;
            var list = new List<KsFeedLand>(map.Values);
            list.Sort((p, q) => p.All.CompareTo(q.All));
            R.Feed = list;
        }

        // krainy zimne w linii zawsze pierwsze (Polnoc, Wolni Ludzie, Nocna Straz, Skagos, Ib, Zelazne Wyspy, Lorath/Nord)
        private static readonly string[] FeedFirst = { "battania", "freefolk", "nightswatch", "skagosi", "ibbenese", "sturgia", "nord" };

        /// <summary>Linia "KS: wyzywienie krain" (Plan; tekst z wyniku - gra i harness). Liczby do sprawdz_logi --grupa npolnoc (NKS-30): "&lt;W|E&gt; &lt;kraina&gt; wsie X kraina Y".</summary>
        internal static string FeedLine(KsResult r, int day)
        {
            if (r == null || r.Feed == null) return null;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(4200);
            sb.Append("KS: wyzywienie krain (plan doba ").Append(day).Append("; najpierw wyzywic siebie ")
              .Append(r.SelfFeed <= 0 ? "WYLACZONE" : (r.SelfFeed >= 2 ? "TAK (wszystkie okregi wsi)" : "TAK (okregi wsi stref zimnych)"))
              .Append(", zimna ziemia ").Append(r.ColdLand ? "TAK" : "WYLACZONA")
              .Append(") | kcal wlasne / potrzeba jedzenia: wsie = (produkcja okregow wsi + praca domowa) / potrzeba wsi, kraina = (wsie + pola mieszczan) / (potrzeba wsi + miast);")
              .Append(" reszta = dowoz kupcow, danina dla Muru, glod");
            var done = new HashSet<string>(StringComparer.Ordinal);
            foreach (var l in FeedFirst)
            {
                KsFeedLand f = null;
                foreach (var q in r.Feed) if (q.Land == l) { f = q; break; }
                if (f == null) continue;
                done.Add(l);
                sb.Append(" | ").Append(f.M == 0 ? "W " : "E ").Append(f.Land).Append(" wsie ").Append(f.Vil.ToString("0.00", inv)).Append(" kraina ").Append(f.All.ToString("0.00", inv))
                  .Append(" (ludzie wsi ").Append((f.VilPeople / 1000).ToString("0", inv)).Append(" tys., miast ").Append((f.TownPeople / 1000).ToString("0", inv))
                  .Append(" tys.; produkcja ").Append((f.VilProd / 1e9).ToString("0.000", inv)).Append(" + pola ").Append((f.TfProd / 1e9).ToString("0.000", inv))
                  .Append(" + dom ").Append((f.Home / 1e9).ToString("0.000", inv)).Append(" / potrzeba ").Append((f.VilNeed / 1e9).ToString("0.000", inv)).Append(" + ").Append((f.TownNeed / 1e9).ToString("0.000", inv))
                  .Append(" mld kcal/d; prog: okregi z brakiem przy rynku ").Append(f.DistFloor).Append('/').Append(f.Dist).Append(", ziemia i rece nie starcza ").Append(f.DistShort)
                  .Append(", brak przy rynku ").Append((f.Target / 1e9).ToString("0.000", inv)).Append(" z progu ").Append((f.Got / 1e9).ToString("0.000", inv))
                  .Append(", rece przesuniete do jedzenia ").Append(f.Hands > 0 ? (100 * f.FloorHands / f.Hands).ToString("0.0", inv) : "-").Append("%)");
            }
            sb.Append(" | reszta krain (wsie/kraina):");
            bool first = true;
            foreach (var f in r.Feed)
            {
                if (done.Contains(f.Land) || !(f.VilNeed + f.TownNeed > 0)) continue;
                sb.Append(first ? " " : ", ").Append(f.M == 0 ? "W " : "E ").Append(f.Land).Append(' ').Append(f.Vil.ToString("0.00", inv)).Append('/').Append(f.All.ToString("0.00", inv));
                first = false;
            }
            sb.Append(" | rece: bloki okregow w zakresie ").Append(r.FeedBlocks).Append(" (z przesunieciem ").Append(r.FeedBind).Append("), rece przesuniete do jedzenia ")
              .Append(r.FeedHandsAll > 0 ? (100 * r.FeedFloorHands / r.FeedHandsAll).ToString("0.00", inv) : "-").Append("% rak pola wsi swiata")
              .Append(", najwieksze odchylenie sumy udzialow bloku od 1 ").Append(r.FeedSumDev.ToString("0.0E+0", inv))
              .Append(" (tozsamosc rak: prog przesuwa tylko rece bloku miedzy jego zajeciami; jedzenie = rece x tabela swiata x ziemia - nic z niczego).");
            return sb.ToString();
        }
    }
}
