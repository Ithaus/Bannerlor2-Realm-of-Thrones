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
        /// <summary>r4-polnoc (recenzja, decyzja Jeffa): true - prog na samym wlasnym jedzeniu (regula doslownie: okreg najpierw rece przy jedzeniu, nawet gdy futra kupia
        /// wiecej zboza, niz da pole; klucz Ks Self Feed Own Food Only); false - na kcal dostepu (jedzenie + alfa x utarg bez jedzenia / cena kcal zboza).</summary>
        public bool SelfFeedOwn;
    }

    /// <summary>r4-polnoc: wyzywienie krainy w Planie (kcal surowca na dobe): potrzeba jedzenia wsi i miast, produkcja jedzenia okregow (zajecia z rak i ziemi), praca
    /// domowa (krok 8), prog "najpierw wyzywic siebie".</summary>
    internal sealed class KsFeedLand
    {
        public string Land; public int M;
        public double VilPeople, TownPeople, VilNeed, TownNeed, VilProd, TfProd, Home, Hands, FloorHands, Target, Cap, Got;
        public double Unc, DietNeed;                                  // krok 8: kcal koszyka wsi bez pokrycia i pelna potrzeba kcal ludzi w domu (ta sama miara co koszyk - KsBasket.Kcal, potrzeby R8)
        public double Diet { get { return DietNeed > 0 ? Math.Max(0.0, Math.Min(1.0, 1.0 - Unc / DietNeed)) : 0.0; } }   // jedzenie wsi: wlasne + kupione + dom (1 - bez pokrycia)
        public int Dist, DistFloor, DistShort, LandCut;               // LandCut: okregi, w ktorych ziemia bloku ucina ruch progu (recenzja)
        public double Vil { get { return VilNeed > 0 ? (VilProd + Home) / VilNeed : 0.0; } }
        public double All { get { double n = VilNeed + TownNeed; return n > 0 ? (VilProd + TfProd + Home) / n : 0.0; } }
    }

    internal sealed partial class KsResult
    {
        public int SelfFeed = -1; public bool ColdLand, SelfFeedOwn;
        public Dictionary<string, double> DistHomeKcalAbs = new Dictionary<string, double>(StringComparer.Ordinal);   // krok 8: kcal/d robione w domu (ostatnia runda)
        public Dictionary<string, double> DistUncKcal = new Dictionary<string, double>(StringComparer.Ordinal);       // krok 8: kcal/d koszyka bez pokrycia (ostatnia runda)
        public Dictionary<string, double> DistDietKcal = new Dictionary<string, double>(StringComparer.Ordinal);      // krok 8: pelna potrzeba kcal ludzi w domu w mierze koszyka (recenzja)
        public List<KsFeedLand> Feed;                                     // wyzywienie krain (Finish)
        public int FeedBlocks, FeedBind; public double FeedSumDev, FeedHandsAll, FeedFloorHands;
        public int FeedOver0, FeedOver1, FeedLandCut; public double FeedLandInc;   // ziemia blokow w zakresie: ponad 0.98 powierzchni przed / po progu, okregi z ruchem ucietym ziemia, przyrost ziemi z progu ponad granice
        public HashSet<string> FeedShort;                                 // okregi z brakiem kcal dostepu przy udzialach rynku (prog czynny) - A7 trzyma tam rece przy jedzeniu (gra)
    }

    internal static partial class KsCore
    {
        private sealed partial class Ctx
        {
            public bool FeedOn;                                        // r4-polnoc: prog "najpierw wyzywic siebie" czynny w tej rownowadze
            public double[] PairKcal, PairLand;                        // kcal jedzenia i ziemia (ha) pary przy udziale 1 i pelnej pracy (bez pracy domowej kroku 8)
            public double[] PairNf;                                    // wartosc bazowa towarow bez jedzenia pary (futra, welna, len, skory, konie) przy udziale 1 i pelnej pracy
            public int[] FdM, FdL; public double[] FdAlpha, FdRegV;   // okreg w zakresie: rynek, kraina (krok 5), alfa (czesc utargu na koszyk), wartosc bazowa regale bez jedzenia
            public double[] FOut;                                      // bufor produkcji pary
            public int[] FA, FB; public double[] FSa, FSb, FSum, FK;  // blok: zadanie liniowe bloku - pary jedzenia (najwyzej dwie), udzialy, suma udzialow, kcal (bez skali regale)
            public double[] Lam, FC, FG;                               // blok: udzial przesuniety w ostatniej rundzie, kcal bloku przy logicie, wolne miejsce mieszanki
            public int[] FdB0, FdB1; public KsDistrict[] FdD; public double[] FdNeed, FdDef, FdCap, FdGot;   // okregi w zakresie: bloki, potrzeba bez daktyli regale, brak, zdolnosc, z progu
            public int Fd;
            public int[] IB, IK; public double[] IG, IE;               // bufor kolejki przesuniec okregu (blok, para dawcy, zysk kcal na udzial, zysk na reke)
            public double[] PHa;                                       // ha na pracujacego pary (Pairs: KsTab.Ha zajecia na strefie bloku - mysliwy na zimnej ziemi)
            public double[] FM, FH, FL0;                               // blok (recenzja): ha mieszanki na udzial (LandP), wolna ziemia do 0.98 x powierzchni, ha zajete przed progiem (ostatnia runda)
            public int[] FdCut;                                        // okreg: ruch progu uciety ziemia bloku (ostatnia runda)
            public double[] BkLam, BkL0, BkDef, BkCap, BkGot; public int[] BkCut; public bool BkOn;   // liczby progu z punktu kontrolnego rownowagi (najlepszy stan)
            public double[] FZq, FZo;                                  // bufory pelnej potrzeby kcal (krok 8): koszyk przy zerze z wlasnego
        }

        internal static HashSet<string> FeedOnlyLands;                    // harness: prog tylko w tych krainach (diagnoza); w grze null
        internal static bool FeedFullWork = true;                         // harness: false - zdolnosc pary z praca domowa kroku 8 (OUTP), true - przy pelnej pracy (domyslnie)
        internal static bool FeedAccess = true;                           // harness: false - prog na samych kcal jedzenia; true - na kcal dostepu (jedzenie + alfa x utarg bez jedzenia / cena kcal zboza)

        /// <summary>Towar Planu bez jedzenia, ktory wies sprzedaje (futra, welna, len, skory, konie, drewno...): bez towarow z kcal, bez zywca i miesa wieloryba (ich mieso
        /// jest w towarze meat, skory w hides).</summary>
        private static bool NonFoodSold(int g)
        {
            return KsTab.FoodKcal[g] <= 0 && g != KsTab.GCow && g != KsTab.GHog && g != KsTab.GSheep && g != KsTab.GWhaleMeat;
        }

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
        ///  - zdolnosc pary (PairFood): kcal jedzenia i ziemia przy udziale 1 i pelnej pracy rak zajec (bez pracy domowej kroku 8);
        ///  - blok: zadanie liniowe - najwiecej kcal jedzenia z par bloku (zajecia, ktore ziemia bloku daje: kandydaci Planu, land >= 0.3) przy udzialach &lt;= 1 i ziemi
        ///    &lt;= 0.98 x powierzchni bloku; najwyzej dwie pary (dwa ograniczenia: rece i ziemia - np. za Murem owce na ziemi i mysliwi na dzikiej ziemi, rybacy przy brzegu).
        /// Kazda runda (ApplyFloors): okreg liczy jedzenie przy udzialach logitu (rynek); gdy brakuje do potrzeby - przesuwa rece do mieszanki jedzenia bloku, najpierw
        /// z zajec bez jedzenia, od najwiekszego zysku kcal na przesunieta reke, tylko tyle, ile brakuje ("na ile ziemia i rece pozwola"). Okreg, ktory rynek sam wyzywia,
        /// nie zmienia nic. Nic z niczego: prog przesuwa tylko rece bloku miedzy jego zajeciami (suma udzialow = 1), produkcja dalej rece x tabela swiata x ziemia.
        /// </summary>
        private static void FloorStep(Ctx x)
        {
            int mode = x.In.SelfFeed;
            x.FeedOn = false;
            if (mode <= 0) return;
            int NP = x.NP, NB = x.NB;
            var fk = KsTab.FoodKcal;
            if (x.PairKcal == null || x.PairKcal.Length != NP) { x.PairKcal = new double[NP]; x.PairLand = new double[NP]; x.PairNf = new double[NP]; }
            if (x.FOut == null) x.FOut = new double[KsTab.NG];
            if (x.FA == null || x.FA.Length != NB)
            {
                x.FA = new int[NB]; x.FB = new int[NB]; x.FSa = new double[NB]; x.FSb = new double[NB]; x.FSum = new double[NB]; x.FK = new double[NB];
                x.Lam = new double[NB]; x.FC = new double[NB]; x.FG = new double[NB];
                x.FM = new double[NB]; x.FH = new double[NB]; x.FL0 = new double[NB];
            }
            x.BkOn = false;
            for (int b = 0; b < NB; b++) { x.FA[b] = x.FB[b] = -1; x.FSa[b] = x.FSb[b] = x.FSum[b] = x.FK[b] = x.Lam[b] = 0; }
            var b0l = new List<int>(); var b1l = new List<int>(); var dl = new List<KsDistrict>(); var nl = new List<double>();
            var ml = new List<int>(); var ll = new List<int>(); var al = new List<double>(); var rl = new List<double>();
            int maxP = 1, b0 = 0;
            while (b0 < NB)
            {
                var d = x.BD[b0];
                int b1 = b0 + 1;
                while (b1 < NB && ReferenceEquals(x.BD[b1], d)) b1++;       // bloki okregu sa kolejne (Prepare)
                if (InFeedScope(d, mode, x.B[b0]))
                {
                    PairFood(x, d, b0, b1);
                    double need = KsTab.FoodNeedKcal(d.Need0), regV = 0;
                    double[] rb;
                    if (x.RegByDist != null && x.RegByDist.TryGetValue(d.Id, out rb) && rb != null)
                        for (int g = 0; g < KsTab.NG; g++) { need -= rb[g] * fk[g]; if (NonFoodSold(g)) regV += rb[g] * x.Val[g]; }
                    for (int b = b0; b < b1; b++)
                    {
                        int a = x.BStart[b], e = x.BStart[b + 1];
                        double A = x.Area[b] * 0.98, best = 0, qi = 0, qj = 0;
                        int pi = -1, pj = -1;
                        for (int k = a; k < e; k++)
                        {
                            double F = x.PairKcal[k];
                            if (!(F > 1e-9)) continue;
                            double L = x.PairLand[k], s = L > A ? A / L : 1.0;
                            if (F * s > best) { best = F * s; pi = k; qi = s; pj = -1; qj = 0; }
                            if (!(L > A)) continue;                         // wierzcholek obu ograniczen: k sam sie nie miesci, k2 tak
                            for (int k2 = a; k2 < e; k2++)
                            {
                                if (k2 == k) continue;
                                double F2 = x.PairKcal[k2], L2 = x.PairLand[k2];
                                if (!(F2 > 1e-9) || !(L2 < A)) continue;
                                double s1 = (A - L2) / (L - L2), s2 = 1.0 - s1, v = F * s1 + F2 * s2;
                                if (s1 > 0 && s2 > 0 && v > best) { best = v; pi = k; qi = s1; pj = k2; qj = s2; }
                            }
                        }
                        if (pi < 0 || !(best > 0)) continue;
                        x.FA[b] = pi; x.FSa[b] = qi; x.FB[b] = pj; x.FSb[b] = qj; x.FSum[b] = qi + qj; x.FK[b] = best;
                    }
                    if (need > 0)
                    {
                        b0l.Add(b0); b1l.Add(b1); dl.Add(d); nl.Add(need); int np = x.BStart[b1] - x.BStart[b0]; if (np > maxP) maxP = np;
                        int m0 = d.Market == 0 ? 0 : 1;
                        bool meas = d.Alpha >= 0 && !double.IsNaN(d.Alpha) && !double.IsInfinity(d.Alpha);   // alfa jak krok 8 (pomiar wsi albo alfa rynku)
                        ml.Add(m0); al.Add(Math.Max(0.0, Math.Min(0.95, meas ? d.Alpha : x.In.VilAlpha[m0]))); rl.Add(regV);
                        ll.Add(x.LandIx != null ? LandOfD(x, d) : -1);
                    }
                }
                b0 = b1;
            }
            x.Fd = dl.Count;
            x.FdB0 = b0l.ToArray(); x.FdB1 = b1l.ToArray(); x.FdD = dl.ToArray(); x.FdNeed = nl.ToArray();
            x.FdM = ml.ToArray(); x.FdL = ll.ToArray(); x.FdAlpha = al.ToArray(); x.FdRegV = rl.ToArray();
            x.FdDef = new double[x.Fd]; x.FdCap = new double[x.Fd]; x.FdGot = new double[x.Fd]; x.FdCut = new int[x.Fd];
            if (x.IB == null || x.IB.Length < maxP) { x.IB = new int[maxP]; x.IK = new int[maxP]; x.IG = new double[maxP]; x.IE = new double[maxP]; }
            x.FeedOn = x.Fd > 0;
        }

        /// <summary>Zdolnosc par blokow okregu do progu: kcal jedzenia i ziemia przy udziale 1 i PELNEJ pracy rak zajec (bez pracy domowej kroku 8 - praca domowa
        /// robi w domu koszyk, na ktory wsi nie stac, z tych samych rak; prog mowi, ile rak trzeba przy jedzeniu, zeby okreg wyzywil swoich ludzi). Ten sam wzor co
        /// BlockOutput (praca domowa okregu na chwile 0, jak w kroku 8). Harness FeedFullWork = false - z OUTP (z praca domowa).</summary>
        private static void PairFood(Ctx x, KsDistrict d, int b0, int b1)
        {
            var fk = KsTab.FoodKcal; var ha = x.PHa; var o = x.FOut;
            double hw0 = d.HomeWork;
            if (FeedFullWork) d.HomeWork = 0.0;
            try
            {
                for (int b = b0; b < b1; b++)
                    for (int k = x.BStart[b]; k < x.BStart[b + 1]; k++)
                    {
                        if (!FeedFullWork)
                        {
                            double s0 = 0, v0 = 0;
                            for (int j = x.OStart[k]; j < x.OStart[k + 1]; j++) { int g = x.OGood[j]; s0 += x.OVal[j] * fk[g]; if (NonFoodSold(g)) v0 += x.OVal[j] * x.Val[g]; }
                            x.PairKcal[k] = s0; x.PairLand[k] = x.LandP[k]; x.PairNf[k] = v0;
                            continue;
                        }
                        double wH, sub, cl, W;
                        BlockOutput(d, x.B[b], x.Pc[k], o, out wH, out sub, out cl, out W);
                        double s = 0, v = 0;
                        for (int g = 0; g < KsTab.NG; g++) if (o[g] != 0.0) { s += o[g] * fk[g]; if (NonFoodSold(g)) v += o[g] * x.Val[g]; }
                        x.PairKcal[k] = s; x.PairNf[k] = v;
                        x.PairLand[k] = wH * KsTab.HaHome + sub * KsTab.HaOwnGrain + cl * ha[k];
                    }
            }
            finally { d.HomeWork = hw0; }
        }

        /// <summary>Prog w udzialach rownowagi (po logicie, kazda runda). KCAL DOSTEPU pary = kcal jedzenia, ktore rodzina zje sama, + to, co kupi za towar bez jedzenia
        /// (futra, welna, len, skory, konie): alfa okregu (czesc utargu, ktora zostaje wsi na koszyk - reszta to pan, dziesiecina, korona, kupiec, jak w kroku 8)
        /// x wartosc bazowa / cena kcal zboza (cena startu: cena cienia w 0.7-1.5, x gradient krainy kroku 5 jak koszyk wsi). Okreg, ktoremu przy udzialach rynku brakuje kcal dostepu do potrzeby, przesuwa
        /// rece do mieszanki jedzenia bloku (zadanie liniowe bloku) - tylko z par, ktore daja mniej kcal dostepu niz mieszanka (najpierw futra i len biednej wsi,
        /// potem jedzenie o mniejszej liczbie kcal na reke), od najwiekszego zysku na reke, tylko tyle, ile brakuje, i najwyzej do udzialow mieszanki (ziemia bloku).
        /// Wies, ktora za futra kupi wiecej jedzenia, niz urodzi jej ziemia, zostaje przy futrach (prog nie robi nikogo glodniejszym); wies wyzywiona - bez zmian.
        /// ZIEMIA (recenzja): ha zajete bloku (suma udzial x LandP - ta sama miara co renta i LandBind Planu) po progu najwyzej 0.98 x powierzchni bloku; blok, ktory
        /// logit juz wypchnal ponad granice (renta do 0.5), prog nie powieksza - ruch z dawcy na mieszanke, ktora bierze wiecej ziemi niz dawca, jest uciety do wolnej ziemi
        /// (mieszanka zadania liniowego liczy tylko siebie; pary, ktore zostaja - obora, owce, konie, mysliwy na 1500 ha - tez zajmuja ziemie). Prog nie daje jedzenia
        /// z ziemi, ktorej blok nie ma. Suma udzialow bloku = 1. Harness FeedAccess = false - same kcal jedzenia (alfa 0).</summary>
        private static void ApplyFloors(Ctx x, double[] p, double[] xs)
        {
            if (!x.FeedOn) return;
            var pk = x.PairKcal; var pv = x.PairNf; var sc = x.BlockScale; var C = x.FC; var cap = x.FG;
            var lp = x.LandP; var fm = x.FM; var fh = x.FH; var l0 = x.FL0;
            var ib = x.IB; var ik = x.IK; var ig = x.IG; var ie = x.IE;
            double vg = x.Val[KsTab.GGrain];
            for (int di = 0; di < x.Fd; di++)
            {
                int b0 = x.FdB0[di], b1 = x.FdB1[di];
                double conv = 0;
                if (FeedAccess && !x.In.SelfFeedOwn && vg > 0)                 // klucz Ks Self Feed Own Food Only - same kcal jedzenia (alfa 0), jak harness FeedAccess = false
                {
                    int m = x.FdM[di];
                    double pg = p != null && p.Length == NM * KsTab.NG ? p[m * KsTab.NG + KsTab.GGrain] : 1.0;
                    if (!(pg > 0) || double.IsNaN(pg)) pg = 1.0;
                    pg = Math.Max(0.7, Math.Min(1.5, pg));
                    // cena zboza w krainie okregu (krok 5: gradient krainy - jak koszyk wsi w kroku 8; wojna i daleka droga = drogie zboze, np. Wolni Ludzie)
                    int l = x.FdL[di];
                    if (x.GradOn && R4LandPrice && l >= 0 && x.GradEff != null && l * KsTab.NG + KsTab.GGrain < x.GradEff.Length)
                    {
                        double gr = x.GradEff[l * KsTab.NG + KsTab.GGrain];
                        if (gr > 0 && !double.IsNaN(gr)) pg *= gr;
                    }
                    conv = x.FdAlpha[di] / (vg * pg / 3300.0);            // kcal zboza za 1 d wartosci bazowej utargu (alfa wsi)
                }
                double tot = 0;
                for (int b = b0; b < b1; b++)
                {
                    double c = 0, lu = 0;
                    for (int k = x.BStart[b]; k < x.BStart[b + 1]; k++) { c += xs[k] * (pk[k] + conv * pv[k]); lu += xs[k] * lp[k]; }
                    c *= sc[b]; C[b] = c; tot += c; x.Lam[b] = 0; l0[b] = lu;
                }
                double def = x.FdNeed[di] - conv * x.FdRegV[di] - tot;
                x.FdDef[di] = def > 0 ? def : 0; x.FdGot[di] = 0; x.FdCap[di] = 0; x.FdCut[di] = 0;
                if (!(def > 1e-6)) continue;
                int n = 0; double capK = 0;
                for (int b = b0; b < b1; b++)
                {
                    int fa = x.FA[b], fb = x.FB[b];
                    if (fa < 0 || !(x.FSum[b] > 0) || !(sc[b] > 0)) continue;
                    double room = x.FSum[b] - xs[fa] - (fb >= 0 ? xs[fb] : 0.0);
                    cap[b] = room;
                    if (!(room > 1e-12)) continue;
                    fh[b] = 0.98 * x.Area[b] - l0[b];                                                   // wolna ziemia bloku (ha) do granicy renty
                    fm[b] = (x.FSa[b] * lp[fa] + (fb >= 0 ? x.FSb[b] * lp[fb] : 0.0)) / x.FSum[b];      // ha mieszanki na przesuniety udzial
                    double vm = x.FSa[b] * pv[fa] + (fb >= 0 ? x.FSb[b] * pv[fb] : 0.0);
                    double M = (x.FK[b] + conv * vm) / x.FSum[b], hands = x.W[b] * sc[b];   // kcal dostepu mieszanki na udzial
                    double rb = 0;
                    for (int k = x.BStart[b]; k < x.BStart[b + 1]; k++)
                    {
                        if (k == fa || k == fb || !(xs[k] > 1e-12)) continue;
                        double g = (M - pk[k] - conv * pv[k]) * sc[b];
                        if (!(g > 1e-9)) continue;
                        double e = hands > 1e-9 ? g / hands : 0.0;
                        int t = n++;
                        while (t > 0 && ie[t - 1] < e) { ie[t] = ie[t - 1]; ib[t] = ib[t - 1]; ik[t] = ik[t - 1]; ig[t] = ig[t - 1]; t--; }   // malejaco wedlug zysku kcal na reke
                        ie[t] = e; ib[t] = b; ik[t] = k; ig[t] = g;
                        rb += Math.Min(xs[k], room) * g;
                    }
                    capK += rb;
                }
                x.FdCap[di] = capK;
                double rem = def, got = 0; int cut = 0;
                for (int t = 0; t < n && rem > 1e-6; t++)
                {
                    int b = ib[t], k = ik[t];
                    double avail = Math.Min(xs[k], cap[b]);
                    if (!(avail > 1e-15)) continue;
                    double g = ig[t], dlt = Math.Min(avail, rem / g);
                    double dl = fm[b] - lp[k];                                 // ha na przesuniety udzial: mieszanka - dawca (> 0 - ruch bierze ziemie)
                    if (dl > 1e-12)
                    {
                        double lim = fh[b] > 0 ? fh[b] / dl : 0.0;
                        if (lim < dlt) { dlt = lim; cut = 1; }
                        if (!(dlt > 1e-15)) continue;
                    }
                    xs[k] -= dlt;
                    xs[x.FA[b]] += dlt * x.FSa[b] / x.FSum[b];
                    if (x.FB[b] >= 0) xs[x.FB[b]] += dlt * x.FSb[b] / x.FSum[b];
                    fh[b] -= dlt * dl;
                    cap[b] -= dlt; x.Lam[b] += dlt; rem -= dlt * g; got += dlt * g;
                }
                x.FdGot[di] = got; x.FdCut[di] = cut;
            }
        }

        /// <summary>Recenzja: liczby progu (udzial przesuniety bloku, ziemia przed progiem, brak, zdolnosc, z progu, ruchy uciete ziemia) zapisane razem z punktem kontrolnym
        /// rownowagi - rownowaga bez zbieznosci oddaje najlepszy stan, a linia wyzywienia krain i A7 maja liczby tego samego stanu co udzialy (dotad - ostatniej rundy).</summary>
        private static void FeedSave(Ctx x)
        {
            if (!x.FeedOn) return;
            int nb = x.NB, nd = x.Fd;
            if (x.BkLam == null || x.BkLam.Length != nb) { x.BkLam = new double[nb]; x.BkL0 = new double[nb]; }
            if (x.BkDef == null || x.BkDef.Length != nd) { x.BkDef = new double[nd]; x.BkCap = new double[nd]; x.BkGot = new double[nd]; x.BkCut = new int[nd]; }
            Array.Copy(x.Lam, x.BkLam, nb); Array.Copy(x.FL0, x.BkL0, nb);
            Array.Copy(x.FdDef, x.BkDef, nd); Array.Copy(x.FdCap, x.BkCap, nd); Array.Copy(x.FdGot, x.BkGot, nd); Array.Copy(x.FdCut, x.BkCut, nd);
            x.BkOn = true;
        }

        private static void FeedRestore(Ctx x)
        {
            if (!x.FeedOn || !x.BkOn || x.BkLam == null || x.BkLam.Length != x.NB || x.BkDef == null || x.BkDef.Length != x.Fd) return;
            int nb = x.NB, nd = x.Fd;
            Array.Copy(x.BkLam, x.Lam, nb); Array.Copy(x.BkL0, x.FL0, nb);
            Array.Copy(x.BkDef, x.FdDef, nd); Array.Copy(x.BkCap, x.FdCap, nd); Array.Copy(x.BkGot, x.FdGot, nd); Array.Copy(x.BkCut, x.FdCut, nd);
        }

        /// <summary>Recenzja: pelna potrzeba kcal ludzi wsi w domu w TEJ SAMEJ mierze co koszyk kroku 8 (KsBasket.Kcal, potrzeby R8 x ludzie w domu, piwo z wlasnego ziarna) -
        /// koszyk przy zerze z wlasnego pola. Mianownik "jedzenie" linii wyzywienia krain: jedzenie = 1 - kcal bez pokrycia / ta potrzeba (licznik i mianownik tej samej
        /// miary; "Plan bez pokrycia wedlug krainy" dzieli te same kcal bez pokrycia przez koszyk ponad wlasne - kcal / koszyk kupowany).</summary>
        private static double DietKcal(Ctx x, double[] full, double[] keep)
        {
            int nz = KsBasket.N, ni = KsDeliver.ItemCount;
            if (x.FZq == null || x.FZq.Length != nz) x.FZq = new double[nz];
            if (x.FZo == null || x.FZo.Length != ni) x.FZo = new double[ni];           // zero: nic z wlasnego pola - pelna potrzeba
            Array.Clear(x.FZq, 0, nz);
            KsBasket.VillageKg(full, keep, x.FZo, x.FZq);
            double s = 0;
            for (int z = 0; z < nz; z++) { double kc = KsBasket.Kcal[z]; if (kc > 0) s += x.FZq[z] * kc; }
            return s;
        }

        /// <summary>Wyzywienie krain z wyniku (Finish): produkcja jedzenia okregow (DistProd, kcal surowca), praca domowa (krok 8), potrzeba wsi i miast, prog (ostatnia
        /// runda rownowagi); kontrola rak (suma udzialow bloku okregu w zakresie = 1).</summary>
        private static void FeedStats(Ctx x, double[] xs)
        {
            var R = x.R;
            R.SelfFeed = x.In.SelfFeed; R.ColdLand = KsTab.ColdLand; R.SelfFeedOwn = x.In.SelfFeedOwn || !FeedAccess;
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
                double u, fa;
                if (R.DistUncKcal.TryGetValue(d.Id, out u) && R.DistDietKcal.TryGetValue(d.Id, out fa) && fa > 0) { f.Unc += u; f.DietNeed += fa; }   // recenzja: ta sama miara kcal
            }
            foreach (var t in x.In.Towns) { var f = get(t.Land, t.Market); f.TownNeed += KsTab.FoodNeedKcal(t.Need0); f.TownPeople += t.People; }
            double dev = 0, hall = 0, hfl = 0, linc = 0; int nb = 0, nbind = 0, ov0 = 0, ov1 = 0, ncut = 0;
            var shortSet = new HashSet<string>(StringComparer.Ordinal);
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
                    if (x.FdDef[di] > 1.0) { f.DistFloor++; shortSet.Add(d.Id); }
                    if (x.FdDef[di] - x.FdGot[di] > x.FdDef[di] * 1e-4 + 1.0) f.DistShort++;   // recenzja: brak po progu (rece albo ziemia nie starcza)
                    if (x.FdCut[di] > 0) { f.LandCut++; ncut++; }
                    for (int b = x.FdB0[di]; b < x.FdB1[di]; b++)
                    {
                        nb++;
                        double s = 0, lu = 0;
                        for (int k = x.BStart[b]; k < x.BStart[b + 1]; k++) { s += xs[k]; lu += xs[k] * x.LandP[k]; }
                        dev = Math.Max(dev, Math.Abs(s - 1.0));
                        // ziemia (recenzja): ha zajete bloku przed progiem (logit) i po progu wobec 0.98 x powierzchni; prog nie bierze ziemi ponad granice
                        double A = 0.98 * x.Area[b];
                        if (x.FL0[b] > A * (1.0 + 1e-9)) ov0++;
                        if (lu > A * (1.0 + 1e-9)) ov1++;
                        if (x.Lam[b] > 0 && x.Area[b] > 0) { double e = (lu - Math.Max(x.FL0[b], A)) / x.Area[b]; if (e > linc) linc = e; }
                        if (x.Lam[b] > 0)
                        {
                            nbind++;
                            double hh = x.Lam[b] * x.W[b] * x.BlockScale[b];      // Lam = udzial bloku przesuniety do mieszanki jedzenia
                            f.FloorHands += hh; hfl += hh;
                        }
                    }
                }
            }
            R.FeedBlocks = nb; R.FeedBind = nbind; R.FeedSumDev = dev; R.FeedHandsAll = hall; R.FeedFloorHands = hfl;
            R.FeedOver0 = ov0; R.FeedOver1 = ov1; R.FeedLandCut = ncut; R.FeedLandInc = linc; R.FeedShort = shortSet;
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
              .Append(r.SelfFeed <= 0 ? "WYLACZONE" : (r.SelfFeed >= 2 ? "TAK (wszystkie okregi wsi" : "TAK (okregi wsi stref zimnych")
                      + (r.SelfFeedOwn ? "; prog na samym wlasnym jedzeniu)" : "; prog na kcal dostepu)"))
              .Append(", zimna ziemia ").Append(r.ColdLand ? "TAK" : "WYLACZONA")
              .Append(") | jedzenie = 1 - kcal koszyka wsi bez pokrycia (krok 8) / pelna potrzeba kcal ludzi wsi w domu w tej samej mierze (kcal koszyka, potrzeby R8; wlasne + kupione + dom); kcal wlasne / potrzeba jedzenia:")
              .Append(" wsie = (produkcja okregow wsi + praca domowa) / potrzeba wsi, kraina = (wsie + pola mieszczan) / (potrzeba wsi + miast);")
              .Append(" reszta = dowoz kupcow, danina dla Muru, glod");
            var done = new HashSet<string>(StringComparer.Ordinal);
            foreach (var l in FeedFirst)
            {
                KsFeedLand f = null;
                foreach (var q in r.Feed) if (q.Land == l) { f = q; break; }
                if (f == null) continue;
                done.Add(l);
                sb.Append(" | ").Append(f.M == 0 ? "W " : "E ").Append(f.Land).Append(" jedzenie ").Append(f.DietNeed > 0 ? f.Diet.ToString("0.00", inv) : "-")
                  .Append(" wsie ").Append(f.Vil.ToString("0.00", inv)).Append(" kraina ").Append(f.All.ToString("0.00", inv))
                  .Append(" (ludzie wsi ").Append((f.VilPeople / 1000).ToString("0", inv)).Append(" tys., miast ").Append((f.TownPeople / 1000).ToString("0", inv))
                  .Append(" tys.; produkcja ").Append((f.VilProd / 1e9).ToString("0.000", inv)).Append(" + pola ").Append((f.TfProd / 1e9).ToString("0.000", inv))
                  .Append(" + dom ").Append((f.Home / 1e9).ToString("0.000", inv)).Append(" / potrzeba ").Append((f.VilNeed / 1e9).ToString("0.000", inv)).Append(" + ").Append((f.TownNeed / 1e9).ToString("0.000", inv))
                  .Append(" mld kcal/d; prog: okregi z brakiem przy rynku ").Append(f.DistFloor).Append('/').Append(f.Dist).Append(", ziemia i rece nie starcza ").Append(f.DistShort)
                  .Append(", brak przy rynku ").Append((f.Target / 1e9).ToString("0.000", inv)).Append(" z progu ").Append((f.Got / 1e9).ToString("0.000", inv))
                  .Append(", rece przesuniete do jedzenia ").Append(f.Hands > 0 ? (100 * f.FloorHands / f.Hands).ToString("0.0", inv) : "-").Append("%")
                  .Append(", okregi z ruchem ucietym ziemia ").Append(f.LandCut).Append(")");
            }
            sb.Append(" | reszta krain (jedzenie/wsie/kraina):");
            bool first = true;
            foreach (var f in r.Feed)
            {
                if (done.Contains(f.Land) || !(f.VilNeed + f.TownNeed > 0)) continue;
                sb.Append(first ? " " : ", ").Append(f.M == 0 ? "W " : "E ").Append(f.Land).Append(' ').Append(f.DietNeed > 0 ? f.Diet.ToString("0.00", inv) : "-").Append('/')
                  .Append(f.Vil.ToString("0.00", inv)).Append('/').Append(f.All.ToString("0.00", inv));
                first = false;
            }
            sb.Append(" | ziemia blokow w zakresie (ha zajete / powierzchnia - miara renty Planu): ponad 0.98 przed progiem ").Append(r.FeedOver0).Append(", po progu ").Append(r.FeedOver1)
              .Append(", okregi z ruchem progu ucietym ziemia ").Append(r.FeedLandCut).Append(", najwiekszy przyrost ziemi z progu ponad granice ").Append(r.FeedLandInc.ToString("0.0E+0", inv))
              .Append(" powierzchni bloku (prog nie bierze ziemi, ktorej blok nie ma)");
            sb.Append(" | rece: bloki okregow w zakresie ").Append(r.FeedBlocks).Append(" (z przesunieciem ").Append(r.FeedBind).Append("), rece przesuniete do jedzenia ")
              .Append(r.FeedHandsAll > 0 ? (100 * r.FeedFloorHands / r.FeedHandsAll).ToString("0.00", inv) : "-").Append("% rak pola wsi swiata")
              .Append(", najwieksze odchylenie sumy udzialow bloku od 1 ").Append(r.FeedSumDev.ToString("0.0E+0", inv))
              .Append(" (tozsamosc rak: prog przesuwa tylko rece bloku miedzy jego zajeciami; jedzenie = rece x tabela swiata x ziemia - nic z niczego).");
            return sb.ToString();
        }
    }
}
