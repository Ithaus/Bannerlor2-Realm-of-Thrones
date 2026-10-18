using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Armoury
{
    // feed-swiat (noc 17/18.10; Jeff: "zadnej reguly dla jednego regionu" - STAN-PRAC ### 82). Czysty C# (bez typow gry) - ten sam plik kompiluje harness Planu (kmoney).
    //
    // 1. RYNEK MUSI SIE OCZYSCIC (klucz Ks Plan Markets Clear). Test fin83 (NKS-30e: Plany niezbiezne, mediana 18 towarow): w Essos punkt staly kroku 8 konczy sie
    //    z popytem zaplaconym wiekszym niz podaz rak o 4-9% ("brak rak do oddania": wsie trzymaja rece pracy domowej na wlasny koszyk), a rownowaga trzyma srednia
    //    cen rynku = 1 - przy niedoborze calego rynku zaden uklad cen nie wyczysci towarow, wiec zostaja poza 2% wszystkie naraz (harness, przebieg rownowagi: kazdy
    //    towar Essos S/D = 0.866 i ceny stoja). Harness: bez tanszego koszyka -2.7% popytu targu i 8 towarow niezbieznych, z tanszym koszykiem -7% (zboze -5.3%)
    //    i 21 towarow, z korytarzami na kredyt -4.2% i 12-20. Przyczyna: drabina koszyka miast liczy cene startu ucieta do 0.7-1.5 (StartIndex) - mieszczanie widza
    //    zboze po 1.5, choc rece Essos go nie urodza (cena cienia ok. 3), a tanszy koszyk dokupuje wlasnie zboze i ryby.
    //    a) CENA NIEDOBORU DLA MIESZCZAN: gdy rynek ma niedobor, a wsie nie maja pracy domowej ponad wlasny koszyk do oddania (ten sam warunek co "brak rak do oddania"),
    //       towar, ktorego brakuje o ponad 2% popytu targu, drozeje w drabinie koszyka miast (narzut na cene startu towaru i jego wyrobow - chleb, placek, piwo
    //       z ziarna; wino z winogron...) o niedobor, najwyzej 7% na runde kroku 8 (krok sterownika rynku), najwyzej x3 (drozyzna sredniowieczna 2-4 x); towar
    //       w nadmiarze albo rynek, ktory juz nie utknal - narzut schodzi do 1. Mieszczanie za ten sam budzet kupuja mniej tego towaru albo tanszy (najpierw
    //       biedni) - popyt zaplacony = to, co rece robia. Budzety w zl bez zmian - wydane <= budzet, zadna moneta nie powstaje ani nie znika. Luka zapasu P4
    //       (popyt na zapas polek) narzutu nie podnosi - mieszczanie nie gloduja, zeby napelnic polki.
    //    b) KROK CENY TOWARU w rownowadze: maleje (x0.6, najmniej 0.002), gdy niedobor towaru zmienia sie w nadmiar i odwrotnie (cena skacze w kolko - pomarancze,
    //       oliwki, ryby, welna: kilka duzych blokow na granicy dwoch zajec), i wraca do 0.05 (x1.02 na runde), gdy idzie w jedna strone.
    //    Proby odrzucone w harnessie (swiat + klucze scalenia; mediana towarow niezbieznych Planow po pierwszym): jeden poziom cen miast dla wszystkich towarow -
    //    Essos do x2.5, R/P kupuja ok. 26% koszyka, owoce Essos 4 x popytu; koniec rownowagi osobno dla kazdego rynku - mediana 5 zamiast 3 i +40% rund; wiekszy
    //    krok ceny (0.10-0.25) - 6-12; sam krok ceny bez narzutu - 14-19 (niedobor Essos zostaje).
    // 2. NAJPIERW WYZYWIC SIEBIE DLA CALEGO SWIATA (Ks Self Feed Everywhere TAK + Ks Self Feed Staples): prog liczy tylko jedzenie, ktorym rodzina sie zywi -
    //    zboze (chleb, kasza, piwo), ryby, mieso, ser, maslo. Oliwki, winogrona, pomarancze, jablka, marchew, jaja, miod i daktyle rodzina sprzedaje (wartosc na targ,
    //    jak futra), cena zboza normalna (cena startu x gradient krainy - "jedzenie, ktore wies kupuje po zwyklych cenach, jest pokryte"; prog nie drga z cena
    //    rownowagi). Harness bez tej reguly: prog swiata stawial rece Dorne i Essos przy oliwkach i winogronach (najwiecej kcal na reke na suchej ziemi - 8 kg
    //    oliwek = 9200 kcal, tyle co zboze), oliwki Westeros na targ 3.3 x popytu po cenie podlogi, winogrona Essos 1.16, Essos niezbiezny.
    // 3. KROK PROGU (Ks Self Feed Slow): udzial bloku przesuniety progiem zmienia sie najwyzej o 0.07 na Plan wobec poprzedniego Planu (pierwszy Plan sesji - bez
    //    tlumienia), w obie strony - okreg nie skacze z rak przy jedzeniu na targ i z powrotem co 7 dob.

    internal sealed partial class KsInput
    {
        /// <summary>feed-swiat (klucz Ks Plan Markets Clear): cena niedoboru dla mieszczan i krok ceny towaru w rownowadze. false - jak dotad (bit w bit).</summary>
        public bool MarketsClear;
        /// <summary>[NM * NG] narzut niedoboru towaru z poprzedniego Planu (cieply start kroku 8) albo null.</summary>
        public double[] WarmMu;
        /// <summary>feed-swiat (klucz Ks Self Feed Staples): prog na jedzeniu podstawowym (zboze, ryby, mieso, ser, maslo), reszta jedzenia na targ, cena zboza normalna.</summary>
        public bool SelfFeedStaple;
        /// <summary>feed-swiat (klucz Ks Self Feed Slow): najwiekszy krok udzialu bloku przesunietego progiem na Plan (0 - bez tlumienia).</summary>
        public double FeedStep;
        /// <summary>uid bloku rachunku -&gt; udzial przesuniety progiem w poprzednim Planie (null - pierwszy Plan sesji: bez tlumienia).</summary>
        public Dictionary<string, double> WarmFloor;
    }

    internal sealed partial class KsResult
    {
        public bool MarketsClear;
        public double[] Mu = Ones2(), Mu0 = Ones2();                   // narzut niedoboru towaru dla mieszczan [m * NG + g] (koniec kroku 8 / start - cieply)
        public int[] MuUp = new int[2], MuDown = new int[2];            // rundy kroku 8 x towary z narzutem w gore / w dol
        public double[] ExRel0 = { double.NaN, double.NaN };           // nadwyzka targu / popyt targu w pierwszej rundzie kroku 8 z nadwyzka (przed narzutem)
        public int EqN, EqConv;                                         // rownowagi Planu: razem, zbiezne
        public int[] EtaCut = new int[2];                               // krok ceny towaru: towary z krokiem ponizej 0.05 na koncu ostatniej rownowagi
        public Dictionary<string, double> FloorLam;                     // uid bloku -> udzial przesuniety progiem (cieply start kroku progu nastepnego Planu)
        public int FeedStepHi, FeedStepLo; public double FeedStepGap, FeedStepDown;   // krok progu: bloki uciete w gore, trzymane w dol; najwiekszy wzrost Lam wobec poprzedniego
                                                                        // Planu (kontrola <= krok) i najwiekszy spadek (rynek sam stawia rece przy jedzeniu albo brak rak do trzymania)
        public bool FeedStaple; public double FeedStep;
        private static double[] Ones2() { var a = new double[2 * KsTab.NG]; for (int i = 0; i < a.Length; i++) a[i] = 1.0; return a; }
    }

    internal static partial class KsCore
    {
        private sealed partial class Ctx
        {
            public double[] Mu;                                        // feed-swiat: narzut niedoboru [m * NG + g] (krok 8; zostaje miedzy krokami 8 tego Planu)
            public double[] MuP;                                       // bufor cen koszyka miasta z narzutem [NZ]
            public double[] FWarm, FHi, FLo;                           // feed-swiat: blok - udzial progu z poprzedniego Planu (-1 brak), granica gorna i dolna tego Planu
        }

        internal const double ClearStep = 0.07;                        // krok najwyzej 7% na runde kroku 8 (krok sterownika rynku); ten sam krok progu na Plan
        private const double EtaMin = 0.002;                           // krok ceny towaru: najmniejszy
        internal static double MuMax = 3.0;                            // narzut niedoboru towaru najwyzej x3 (drozyzna sredniowieczna 2-4 x)
        internal static double EtaMax = 0.05, EtaGrow = 1.02;          // krok ceny towaru: najwiekszy (Eta jak dotad) i wzrost na runde w jedna strone
        internal static bool ClearMu = true, ClearEta = true;          // harness: proby (narzut niedoboru / krok ceny towaru)
        internal static bool FeedNormalPrice = true;                   // harness: prog przy Ks Self Feed Staples - zboze po cenie startu (false - cena rownowagi w 0.7-1.5 jak dotad)
        internal static Action<int, double[], double[], double[], double[]> EqDebug = null;   // harness: przebieg rownowagi (runda, ceny, podaz, popyt, udzialy par); w grze null

        // ------------------------------------------------------------ 1a. cena niedoboru dla mieszczan (krok 8)

        /// <summary>Poczatek kroku 8: narzut z poprzedniego kroku 8 tego Planu albo cieply z poprzedniego Planu (KsInput.WarmMu), w [1, MuMax].</summary>
        private static void MuInit(Ctx x)
        {
            if (!x.In.MarketsClear) return;
            int n = NM * KsTab.NG;
            if (x.Mu == null)
            {
                x.Mu = new double[n];
                var w = x.In.WarmMu;
                for (int i = 0; i < n; i++)
                {
                    double v = w != null && w.Length == n ? w[i] : 1.0;
                    x.Mu[i] = v > 1.0 && !double.IsNaN(v) && !double.IsInfinity(v) ? Math.Min(MuMax, v) : 1.0;
                }
            }
            x.R.MarketsClear = true;
            Array.Copy(x.Mu, x.R.Mu0, n);
            for (int m = 0; m < NM; m++) x.R.ExRel0[m] = double.NaN;
        }

        /// <summary>Ceny koszyka miasta do drabiny klas: cena startu pozycji x narzut niedoboru jej surowca (KsBasket.RawGood - chleb, placek, piwo z ziarna; wino
        /// z winogron...). Bez klucza - te same ceny (tablica i przesuniecie bez zmian).</summary>
        private static double[] TownLadderPrices(Ctx x, int m, double[] tpz, int tpo, out int lpo)
        {
            lpo = tpo;
            if (!x.In.MarketsClear || x.Mu == null) return tpz;
            int nz = KsBasket.N;
            if (x.MuP == null || x.MuP.Length != nz) x.MuP = new double[nz];
            for (int z = 0; z < nz; z++)
            {
                int g = KsBasket.RawGood[z];
                x.MuP[z] = tpz[tpo + z] * (g >= 0 ? x.Mu[m * KsTab.NG + g] : 1.0);
            }
            lpo = 0;
            return x.MuP;
        }

        /// <summary>Runda kroku 8 (po nadwyzce targu i poprawce G). Na rynku z niedoborem, ktory nie ma juz pracy domowej ponad wlasny koszyk wsi do oddania (warunek
        /// "brak rak do oddania" punktu stalego), towar z niedoborem ponad 2% ((podaz - popyt) / popyt targu bez zjedzonego w okregach; bez luki zapasu P4) drozeje dla
        /// mieszczan o niedobor, najwyzej 7%, najwyzej MuMax. Towar w nadmiarze ponad 2% albo rynek, ktory juz nie utknal - narzut w dol (nie ponizej 1, bez histerezy).
        /// Towary jak nadwyzka targu (bez regale, koni i wyjatkow projektu).</summary>
        private static void MuRound(Ctx x, KsMoney M, double[] S, double[] eatPrev)
        {
            if (!x.In.MarketsClear || x.Mu == null || !ClearMu) return;
            for (int m = 0; m < NM; m++)
            {
                double rel = M.ExcessRel[m];
                if (double.IsNaN(rel) || double.IsInfinity(rel)) continue;
                if (double.IsNaN(x.R.ExRel0[m])) x.R.ExRel0[m] = rel;
                bool stuck = rel < 0 && M.HwLeft[m] < 0.05 * Math.Abs(M.Excess[m]);
                for (int g = 0; g < KsTab.NG; g++)
                {
                    int i = m * KsTab.NG + g;
                    if (KsTab.RegGoodMask[g] || g == KsTab.GHorse || ExcessSkip(g) || !(x.Dm[i] > 1.0)) continue;
                    double dmk = Math.Max(0.0, x.Dm[i] - eatPrev[i]);
                    if (!(dmk > 1.0)) continue;
                    double rg = (S[i] - x.Dm[i]) / dmk;
                    if (stuck && rg < -Tol && x.Mu[i] < MuMax)
                    {
                        x.Mu[i] = Math.Min(MuMax, x.Mu[i] * Math.Min(1.0 + ClearStep, 1.0 - rg));
                        x.R.MuUp[m]++;
                    }
                    else if (x.Mu[i] > 1.0 && (rg > Tol || !stuck))
                    {
                        x.Mu[i] = Math.Max(1.0, x.Mu[i] / Math.Min(1.0 + ClearStep, 1.0 + Math.Max(rg, Tol)));
                        x.R.MuDown[m]++;
                    }
                }
            }
        }

        /// <summary>Koniec kroku 8: narzut do wyniku.</summary>
        private static void MuFinish(Ctx x)
        {
            if (!x.In.MarketsClear || x.Mu == null) return;
            Array.Copy(x.Mu, x.R.Mu, x.Mu.Length);
        }

        // ------------------------------------------------------------ 1b. krok ceny towaru (rownowaga)

        /// <summary>Krok ceny towaru i w tej rundzie rownowagi: maleje x0.6 (najmniej EtaMin), gdy znak niedoboru odwrocil sie wobec poprzedniej rundy (cena skacze
        /// w kolko), rosnie x EtaGrow do EtaMax, gdy idzie w jedna strone. sg - znak poprzedniej rundy (+1 niedobor, -1 nadmiar, 0 - brak).</summary>
        private static double EtaOf(double[] eta, sbyte[] sg, int i, double r)
        {
            sbyte s = r > 1.0 ? (sbyte)1 : (r < 1.0 ? (sbyte)-1 : (sbyte)0);
            if (s != 0 && sg[i] != 0 && s != sg[i]) eta[i] = Math.Max(EtaMin, eta[i] * 0.6);
            else eta[i] = Math.Min(EtaMax, eta[i] * EtaGrow);
            if (s != 0) sg[i] = s;
            return eta[i];
        }

        /// <summary>Koniec rownowagi: towary z krokiem ceny ponizej 0.05 (wynik - linia rynkow Planu).</summary>
        private static void EtaStats(Ctx x, double[] eta, bool[] act)
        {
            if (eta == null) return;
            for (int m = 0; m < NM; m++) x.R.EtaCut[m] = 0;
            for (int i = 0; i < eta.Length; i++) if (act[i] && eta[i] < Eta * 0.999) x.R.EtaCut[i / KsTab.NG]++;
        }

        // ------------------------------------------------------------ 2. prog na jedzeniu podstawowym

        /// <summary>Jedzenie podstawowe (rodzina sie nim zywi): zboze, ryby, mieso (z zywcem i dziczyzna - BlockOutput), ser, maslo.</summary>
        internal static bool Staple(int g) { return g == KsTab.GGrain || g == KsTab.GFish || g == KsTab.GMeat || g == KsTab.GCheese || g == KsTab.GButter; }

        /// <summary>kcal na kg towaru g liczone przez prog: przy kluczu Ks Self Feed Staples tylko jedzenie podstawowe, bez klucza - kazde jedzenie (jak dotad).</summary>
        private static double FeedKcal(Ctx x, int g)
        {
            double k = KsTab.FoodKcal[g];
            return x.In.SelfFeedStaple && !Staple(g) ? 0.0 : k;
        }

        /// <summary>Towar, ktory prog liczy jako sprzedany (kcal dostepu przez alfa x wartosc): bez jedzenia (futra, welna, len...), przy kluczu Ks Self Feed Staples takze
        /// jedzenie spoza podstawowego (oliwki, winogrona, owoce, miod, jaja, daktyle).</summary>
        private static bool FeedSold(Ctx x, int g)
        {
            if (NonFoodSold(g)) return true;
            return x.In.SelfFeedStaple && KsTab.FoodKcal[g] > 0 && !Staple(g);
        }

        /// <summary>Potrzeba jedzenia podstawowego osady w kcal surowca (zboze z ziarnem na piwo, mieso, ryby, ser, maslo) - miara FeedKcal.</summary>
        internal static double StapleNeedKcal(double[] n)
        {
            if (n == null) return 0.0;
            return (n[KsTab.NG_] + n[KsTab.NBeer] * KsTab.BeerGrain) * 3300 + n[KsTab.NMeat] * 2200 + n[KsTab.NFish] * 1100 + n[KsTab.NCheese] * 3600 + n[KsTab.NButter] * 7200;
        }

        // ------------------------------------------------------------ 3. krok progu na Plan

        /// <summary>FloorStep: granice udzialu progu bloku z poprzedniego Planu (KsInput.WarmFloor; brak - bez granic).</summary>
        private static void FeedStepPrepare(Ctx x)
        {
            int nb = x.NB;
            if (x.FWarm == null || x.FWarm.Length != nb) { x.FWarm = new double[nb]; x.FHi = new double[nb]; x.FLo = new double[nb]; }
            double step = x.In.FeedStep;
            var w = x.In.WarmFloor;
            for (int b = 0; b < nb; b++)
            {
                double v;
                if (step > 0 && w != null && x.B[b].Uid != null && w.TryGetValue(x.B[b].Uid, out v) && v >= 0 && !double.IsNaN(v))
                {
                    x.FWarm[b] = v; x.FHi[b] = v + step; x.FLo[b] = Math.Max(0.0, v - step);
                }
                else
                {
                    // blok bez udzialu w poprzednim Planie: przy kroku i cieplym starcie (Plan po Planie) - jak blok z udzialem 0 (prog najwyzej krok), bez cieplego startu - bez granic
                    bool warm = step > 0 && w != null;
                    x.FWarm[b] = warm ? 0.0 : -1.0; x.FHi[b] = warm ? step : double.MaxValue; x.FLo[b] = 0.0;
                }
            }
        }

        /// <summary>Wynik: udzial progu blokow w zakresie (uid -&gt; Lam) i kontrola kroku: najwiekszy wzrost Lam wobec poprzedniego Planu (twarda granica - krok) i najwiekszy
        /// spadek (dol trzymany tylko rekami, ktore jeszcze nie sa przy jedzeniu: gdy rynek sam stawia rece przy jedzeniu, prog nie ma czego trzymac - Lam spada, rece zostaja).</summary>
        private static void FeedStepStats(Ctx x)
        {
            var R = x.R;
            R.FeedStaple = x.In.SelfFeedStaple; R.FeedStep = x.In.FeedStep;
            if (!x.FeedOn) return;
            var fl = new Dictionary<string, double>(StringComparer.Ordinal);
            double gap = 0, down = 0; int hi = 0, lo = 0;
            for (int di = 0; di < x.Fd; di++)
                for (int b = x.FdB0[di]; b < x.FdB1[di]; b++)
                {
                    if (x.B[b].Uid != null && x.Lam[b] > 0) fl[x.B[b].Uid] = x.Lam[b];
                    if (x.In.FeedStep > 0 && x.FWarm != null && x.FWarm[b] >= 0)
                    {
                        gap = Math.Max(gap, x.Lam[b] - x.FWarm[b]); down = Math.Max(down, x.FWarm[b] - x.Lam[b]);
                        if (x.Lam[b] >= x.FHi[b] - 1e-9) hi++;
                        if (x.FLo[b] > 1e-12 && x.Lam[b] <= x.FLo[b] + 1e-9) lo++;
                    }
                }
            R.FloorLam = fl; R.FeedStepGap = gap; R.FeedStepDown = down; R.FeedStepHi = hi; R.FeedStepLo = lo;
        }

        // ------------------------------------------------------------ linia

        /// <summary>Linia "KS: rynki Planu (feed-swiat)" (gra i harness): nadwyzka targu przed / po narzucie, towary z narzutem niedoboru, wydane miast wobec budzetu,
        /// rownowagi, towary niezbiezne. Liczby do sprawdz_logi --grupa npolnoc (NKS-30k..n).</summary>
        internal static string ClearLine(KsResult r, int day)
        {
            if (r == null || r.Error != null) return null;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(1600);
            var M = r.Money;
            sb.Append("KS: rynki Planu (feed-swiat) plan doba ").Append(day).Append(" | rynek musi sie oczyscic ").Append(r.MarketsClear ? "TAK" : "WYLACZONE")
              .Append(" (gdy rynkowi brakuje rak, a wsie nie maja pracy domowej ponad wlasny koszyk do oddania, towar, ktorego brakuje, drozeje dla mieszczan najwyzej 7% na runde kroku 8, najwyzej x")
              .Append(MuMax.ToString("0.0", inv)).Append("; budzety klas miast w zl bez zmian - wydane <= budzet, zadna moneta nie powstaje ani nie znika; krok ceny towaru maleje, gdy cena skacze w kolko)")
              .Append(" | rownowagi ").Append(r.EqN).Append(", zbiezne ").Append(r.EqConv);
            string[] mk = { "Westeros", "Essos" };
            for (int m = 0; m < NM; m++)
            {
                sb.Append(" | ").Append(mk[m]).Append(": nadwyzka targu / popyt targu ").Append(double.IsNaN(r.ExRel0[m]) ? "-" : (100 * r.ExRel0[m]).ToString("0.0", inv)).Append("% -> ")
                  .Append(M != null ? (100 * M.ExcessRel[m]).ToString("0.0", inv) : "-").Append("%");
                var mu = new List<string>();
                for (int g = 0; g < KsTab.NG; g++) { double v = r.Mu[m * KsTab.NG + g]; if (v > 1.0005) mu.Add(KsTab.Goods[g] + " x" + v.ToString("0.000", inv)); }
                sb.Append(", towary z narzutem ").Append(mu.Count).Append(mu.Count > 0 ? " (" + string.Join(" ", mu) + ")" : "")
                  .Append(", rund kroku 8 x towary w gore ").Append(r.MuUp[m]).Append(" w dol ").Append(r.MuDown[m]);
                if (M != null)
                {
                    double bud = M.TBudget[m * 3] + M.TBudget[m * 3 + 1] + M.TBudget[m * 3 + 2];
                    double paid = M.TPaid[m * 3] + M.TPaid[m * 3 + 1] + M.TPaid[m * 3 + 2];
                    sb.Append(", miasta: wydane ").Append(paid.ToString("0", inv)).Append(" zl/d <= budzet ").Append(bud.ToString("0", inv)).Append(" zl/d, W/R/P zaplacone ");
                    for (int c = 0; c < 3; c++)
                    {
                        double b = M.TBasket[m * 3 + c];
                        sb.Append(c > 0 ? "/" : "").Append(b > 0 ? (100 * M.TPaid[m * 3 + c] / b).ToString("0.0", inv) : "-");
                    }
                    sb.Append("% wartosci koszyka przy cenach startu");
                }
                sb.Append(", krok ceny zmniejszony ").Append(r.EtaCut[m]);
                int open = 0; string pre = mk[m] + " ";
                foreach (var o in r.Open) if (o.StartsWith(pre, StringComparison.Ordinal)) open++;
                sb.Append(", towary niezbiezne ").Append(open);
            }
            sb.Append(" | towar: popyt zaplacony miast i wsi = podaz rak - zjedzone (nadwyzka targu ~0); narzut tylko w Planie (rece i czesc zaplacona koszyka miast), ceny gry robi rynek gry.");
            return sb.ToString();
        }
    }
}
