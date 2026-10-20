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
    //    c) RECENZJA (noc 17/18.10, wazne): NARZUT TYLKO W PLANIE - rece i popyt Planu (przy kluczu Ks Plan Markets Clear Villages takze koszyk i praca domowa wsi -
    //       DistPaid/DistHome, wies naprawde robi to sama); do gry idzie czesc zaplacona koszyka miast przy cenach startu (TownPaid/TownExtra - druga drabina
    //       w ostatniej rundzie). Dotad gra dostawala czesc
    //       zaplacona z narzutem: popyt miast ZC4 (Zc4Build) i mianownik glodu (KsTownMeal - koszyk zaplacony) szly w dol, wiec brak rak, ktorego gra nie zmieni,
    //       wygladal w grze jak "nie stac" (KsHunger: to nie glod) - glod biednych Essos byl schowany. Teraz brak rak pokazuja ceny gry, puste polki i glod
    //       (KsHungerUnrest) - Jeff: "jak nie ma jedzenia to bedzie bunt". Linia "KS: tanszy koszyk miast (P9)" i NKS-3c licza drabine przy cenach startu (jak dotad);
    //       kcal przy narzucie (tyle rece rynku urodza) - w linii rynkow Planu.
    //    d) RECENZJA - JEDNA CENA RYNKU W PLANIE (klucz Ks Plan Markets Clear Villages, domyslnie WYLACZONY - decyzja Jeffa): narzut placi tez koszyk wsi tego rynku (wies,
    //       ktorej nie stac na drogie zboze, robi wiecej w domu; czego nie zrobi - kcal bez pokrycia). Harness (8 Planow, swiat + klucze scalenia): niezbiezne mediana
    //       7 zamiast 3 (NKS-30n <= 6 - NIE), kcal wsi Essos bez pokrycia 5.0% zamiast 3.3%, R/P Essos przy narzucie 77/78% zamiast 72/73%, pomarancze Essos 3.2
    //       zamiast 4.2 x popytu; wies z narzutem z poprzedniego Planu (proba) - mediana 5, pomarancze 4.5. W grze jedna cena jest zawsze (cena targu gry dla mieszczan
    //       i wsi - KS-H, kiesa wsi, drabina ZC4); bez klucza dwie ceny sa tylko w rachunku Planu.
    // 2. NAJPIERW WYZYWIC SIEBIE DLA CALEGO SWIATA (Ks Self Feed Everywhere TAK + Ks Self Feed Staples): prog liczy tylko jedzenie, ktorym rodzina sie zywi -
    //    zboze (chleb, kasza, piwo), ryby, mieso, ser, maslo. Oliwki, winogrona, pomarancze, jablka, marchew, jaja, miod i daktyle rodzina sprzedaje (wartosc na targ,
    //    jak futra), cena zboza normalna (cena startu x gradient krainy - "jedzenie, ktore wies kupuje po zwyklych cenach, jest pokryte"; prog nie drga z cena
    //    rownowagi). Harness bez tej reguly: prog swiata stawial rece Dorne i Essos przy oliwkach i winogronach (najwiecej kcal na reke na suchej ziemi - 8 kg
    //    oliwek = 9200 kcal, tyle co zboze), oliwki Westeros na targ 3.3 x popytu po cenie podlogi, winogrona Essos 1.16, Essos niezbiezny.
    // 3. KROK PROGU (Ks Self Feed Slow): udzial bloku przesuniety progiem zmienia sie najwyzej o 0.07 na Plan wobec poprzedniego Planu (pierwszy Plan sesji - bez
    //    tlumienia), w obie strony - okreg nie skacze z rak przy jedzeniu na targ i z powrotem co 7 dob. W dol (recenzja): trzymany tylko rekami par o mniejszych
    //    kcal dostepu niz mieszanka jedzenia bloku (futra, len, ubozsze jedzenie) - nigdy z par, ktore karmia lepiej (prog nie robi nikogo glodniejszym); brak takich
    //    par - udzial spada szybciej (linia: najwiekszy spadek).

    internal sealed partial class KsInput
    {
        /// <summary>feed-swiat (klucz Ks Plan Markets Clear): cena niedoboru dla mieszczan i krok ceny towaru w rownowadze. false - jak dotad (bit w bit).</summary>
        public bool MarketsClear;
        /// <summary>Recenzja feed-swiat (klucz Ks Plan Markets Clear Villages): narzut niedoboru takze w koszyku wsi (jedna cena rynku w Planie). false - narzut tylko w drabinie
        /// klas miast (Plan); gra i tak ma jedna cene - rynku gry.</summary>
        public bool MarketsClearVil;
        /// <summary>[NM * NG] narzut niedoboru towaru z poprzedniego Planu (cieply start kroku 8) albo null.</summary>
        public double[] WarmMu;
        /// <summary>feed-swiat (klucz Ks Self Feed Staples): prog na jedzeniu podstawowym (zboze, ryby, mieso, ser, maslo), reszta jedzenia na targ, cena zboza normalna.</summary>
        public bool SelfFeedStaple;
        /// <summary>feed-swiat (klucz Ks Self Feed Slow): najwiekszy krok udzialu bloku przesunietego progiem na Plan (0 - bez tlumienia).</summary>
        public double FeedStep;
        /// <summary>uid bloku rachunku -&gt; udzial przesuniety progiem w poprzednim Planie (null - pierwszy Plan sesji: bez tlumienia).</summary>
        public Dictionary<string, double> WarmFloor;
        /// <summary>n22f-plany (klucz Ks Plan Converge Shelf Level, po recenzji): ocena zbieznosci stanu oddanego rownowagi wobec pasma [1, wspolny poziom podazy / popytu rynku],
        /// poziom uciety do celu kroku 8 tego rynku (czynnik towaru mfac: luka zapasu P4 1.00-1.25 w stanie Plan, sterowanie 0.85-1.25 od D+14, margines 1.03) - zapas polek,
        /// ktory krok 8 zamawia rekami z pracy domowej, to nadwyzka wspolna wszystkim towarom, ktorej ceny wzgledne nie zbijaja (normalizacja cen na rynek). Tylko miara
        /// (lista towarow niezbieznych, linia rynkow Planu) - przebieg rownowagi bez zmian. false - jak dotad (bit w bit).</summary>
        public bool ConvShelfLevel;
    }

    internal sealed partial class KsResult
    {
        public bool MarketsClear;
        public double[] Mu = Ones2(), Mu0 = Ones2();                   // narzut niedoboru towaru dla mieszczan [m * NG + g] (koniec kroku 8 / start - cieply)
        public int[] MuUp = new int[2], MuDown = new int[2];            // rundy kroku 8 x towary z narzutem w gore / w dol
        public double[] ExRel0 = { double.NaN, double.NaN };           // nadwyzka targu / popyt targu w pierwszej rundzie kroku 8 z nadwyzka (przed narzutem)
        public int EqN, EqConv;                                         // rownowagi Planu: razem, zbiezne
        public double[] ConvLev = { 1.0, 1.0 };                         // n22f-plany: wspolny poziom podazy / popytu rynku ostatniej rownowagi (1 - bez; Ks Plan Converge Shelf Level)
        public double[] ConvLevT = { 1.0, 1.0 };                        // n22f-plany (recenzja): cel kroku 8 rynku, do ktorego uciety jest poziom (1 - bez kroku 8 albo klucz wylaczony)
        public int[] OpenAbsM = new int[2];                             // n22f-plany (recenzja): towary niezbiezne rynku stara miara |S/D - 1| > 2% (rundy pierwszej rownowagi nastepnego Planu)
        public int[] EtaCut = new int[2];                               // krok ceny towaru: towary z krokiem ponizej 0.05 na koncu ostatniej rownowagi
        public Dictionary<string, double> FloorLam;                     // uid bloku -> udzial przesuniety progiem (cieply start kroku progu nastepnego Planu)
        public int FeedStepHi, FeedStepLo; public double FeedStepGap, FeedStepDown;   // krok progu: bloki uciete w gore, trzymane w dol; najwiekszy wzrost Lam wobec poprzedniego
                                                                        // Planu (kontrola <= krok) i najwiekszy spadek (rynek sam stawia rece przy jedzeniu albo brak rak do trzymania)
        public bool FeedStaple; public double FeedStep;
        // recenzja: ostatnia runda kroku 8 przy narzucie (Plan) - miasta [m * 3 + klasa]: wydane zl/d, kcal jedzenia pelnego koszyka i kupione (do gry idzie drabina
        // przy cenach startu - KsMoney.TPaid / P9); wsie: drabiny koszyka z narzutem (ostatnia runda); cieply start narzutu i kroku progu (pierwszy Plan po wczytaniu - NIE)
        public double[] ClrTPaid = new double[6], ClrK0 = new double[6], ClrK1 = new double[6];
        public int[] ClrTown = new int[2], ClrVil = new int[2];
        public bool MuWarm, FeedWarm, MarketsClearVil;
        private static double[] Ones2() { var a = new double[2 * KsTab.NG]; for (int i = 0; i < a.Length; i++) a[i] = 1.0; return a; }
    }

    internal static partial class KsCore
    {
        private sealed partial class Ctx
        {
            public double[] Mu;                                        // feed-swiat: narzut niedoboru [m * NG + g] (krok 8; zostaje miedzy krokami 8 tego Planu)
            public double[] MuP;                                       // bufor cen koszyka miasta z narzutem [NZ]
            public double[] MuPV;                                      // recenzja: bufor cen koszyka wsi z narzutem [NZ] (jedna cena rynku)
            public bool[] MuOn;                                        // recenzja: rynek z narzutem ponad 1 na jakims towarze (bez - ceny bez kopii, co do bitu)
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
                x.R.MuWarm = w != null && w.Length == n;
                for (int i = 0; i < n; i++)
                {
                    double v = w != null && w.Length == n ? w[i] : 1.0;
                    x.Mu[i] = v > 1.0 && !double.IsNaN(v) && !double.IsInfinity(v) ? Math.Min(MuMax, v) : 1.0;
                }
            }
            x.R.MarketsClear = true; x.R.MarketsClearVil = x.In.MarketsClearVil;
            Array.Copy(x.Mu, x.R.Mu0, n);
            for (int m = 0; m < NM; m++) x.R.ExRel0[m] = double.NaN;
            // recenzja: liczby ostatniej rundy - od nowa w kazdym kroku 8 (ponowny krok 8 Planu nie dodaje drugi raz)
            Array.Clear(x.R.ClrTPaid, 0, 6); Array.Clear(x.R.ClrK0, 0, 6); Array.Clear(x.R.ClrK1, 0, 6); Array.Clear(x.R.ClrTown, 0, NM); Array.Clear(x.R.ClrVil, 0, NM);
            MuFlags(x);
        }

        /// <summary>Recenzja: rynek z narzutem ponad 1 na jakims towarze (po MuInit i kazdym MuRound). Rynek bez narzutu - drabiny na tych samych tablicach cen co dotad.</summary>
        private static void MuFlags(Ctx x)
        {
            if (x.MuOn == null) x.MuOn = new bool[NM];
            for (int m = 0; m < NM; m++)
            {
                bool on = false;
                for (int g = 0; g < KsTab.NG && !on; g++) on = x.Mu[m * KsTab.NG + g] > 1.0;
                x.MuOn[m] = on;
            }
        }

        /// <summary>Ceny koszyka z narzutem: cena pozycji (src[so + z]) x narzut niedoboru jej surowca (KsBasket.RawGood - chleb, placek, piwo z ziarna; wino
        /// z winogron...) do bufora buf (przesuniecie 0). Bez klucza albo rynek bez narzutu - te same ceny (tablica i przesuniecie bez zmian).</summary>
        private static double[] MarkedPrices(Ctx x, int m, double[] src, int so, ref double[] buf, out int lpo)
        {
            lpo = so;
            if (!x.In.MarketsClear || x.Mu == null || x.MuOn == null || !x.MuOn[m]) return src;
            int nz = KsBasket.N;
            if (buf == null || buf.Length != nz) buf = new double[nz];
            for (int z = 0; z < nz; z++)
            {
                int g = KsBasket.RawGood[z];
                buf[z] = src[so + z] * (g >= 0 ? x.Mu[m * KsTab.NG + g] : 1.0);
            }
            lpo = 0;
            return buf;
        }

        /// <summary>Ceny koszyka miasta do drabiny klas (z narzutem - MarkedPrices).</summary>
        private static double[] TownLadderPrices(Ctx x, int m, double[] tpz, int tpo, out int lpo) { return MarkedPrices(x, m, tpz, tpo, ref x.MuP, out lpo); }

        /// <summary>Recenzja (jedna cena rynku, klucz Ks Plan Markets Clear Villages - domyslnie WYLACZONY, decyzja Jeffa): ceny koszyka wsi do drabiny wsi - ten sam narzut
        /// co mieszczanie tego rynku (wies, ktorej nie stac, robi wiecej w domu - praca domowa kroku 8). Bez klucza / rynek bez narzutu - pzu, po bez zmian (co do bitu).</summary>
        private static double[] VillageLadderPrices(Ctx x, int m, double[] pzu, ref int po, bool last)
        {
            if (!x.In.MarketsClearVil) return pzu;
            int lpo; var r = MarkedPrices(x, m, pzu, po, ref x.MuPV, out lpo);
            if (!ReferenceEquals(r, pzu)) { po = lpo; if (last) x.R.ClrVil[m]++; }
            return r;
        }

        /// <summary>Recenzja (wazne): ostatnia runda kroku 8, klasa miasta - narzut tylko w Planie. Drabina z narzutem (lpz) do liczb Planu (ClrTPaid, ClrK0/1 - linia
        /// rynkow), potem ta sama drabina przy cenach startu (tpz) - do gry (TownPaid, TownExtra), KsMoney (TPaid, P9, ExtraT, UnpaidT) i NKS-3c. Brak rak rynku
        /// pokazuje gra (ceny, polki, glod KsHunger), nie mniejszy popyt miast z Planu. Bez narzutu (lpz == tpz) - nic (jedna drabina jak dotad).</summary>
        private static void ClearTownExport(Ctx x, bool cheap, int m, int c, bool[] tcut, double[] qc, double[] qq, double[] tpz, int tpo, double[] lpz, double budget,
                                            ref double pv, double[] paid, double[] tex1, double[] p9)
        {
            if (ReferenceEquals(lpz, tpz)) return;
            var R = x.R; int nz = KsBasket.N;
            R.ClrTPaid[m * 3 + c] += pv; R.ClrK0[m * 3 + c] += p9[0]; R.ClrK1[m * 3 + c] += p9[1];
            if (c == 0) R.ClrTown[m]++;
            Array.Clear(p9, 0, p9.Length);
            if (tcut != null)
            {
                Array.Copy(qc, qq, nz);
                for (int z = 0; z < nz && z < tcut.Length; z++) if (tcut[z]) qq[z] = 0.0;
                pv = KsCheap.PlanLadder(cheap, qq, qc, tpz, tpo, budget, paid, tex1, p9);
                for (int z = 0; z < nz && z < tcut.Length; z++) if (tcut[z]) paid[z] = 0.0;
            }
            else pv = KsCheap.PlanLadder(cheap, qc, qc, tpz, tpo, budget, paid, tex1, p9);
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
            MuFlags(x);
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
            x.R.FeedWarm = step > 0 && w != null;                       // recenzja: pierwszy Plan po wczytaniu (bez poprzedniego Planu w pamieci) - bez granic kroku (linia)
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
              .Append(" (gdy rynkowi brakuje rak, a wsie nie maja pracy domowej ponad wlasny koszyk do oddania, towar, ktorego brakuje, drozeje w Planie dla mieszczan")
              .Append(r.MarketsClearVil ? " i wsi tego rynku (jedna cena - Ks Plan Markets Clear Villages TAK)" : " (wsie po cenie startu z praca domowa - Ks Plan Markets Clear Villages WYLACZONY)")
              .Append(" najwyzej 7% na runde kroku 8, najwyzej x")
              .Append(MuMax.ToString("0.0", inv)).Append("; budzety klas miast w zl bez zmian - wydane <= budzet, zadna moneta nie powstaje ani nie znika; narzut tylko w Planie - do gry idzie czesc zaplacona koszyka miast przy cenach startu; krok ceny towaru maleje, gdy cena skacze w kolko)")
              .Append(" | rownowagi ").Append(r.EqN).Append(", zbiezne ").Append(r.EqConv)
              .Append(", narzut z poprzedniego Planu ").Append(r.MuWarm ? "TAK" : "NIE (pierwszy Plan po wczytaniu - od 1)");
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
                    // recenzja: KsMoney (TPaid, P9) - drabina przy cenach startu (to idzie do gry: TownPaid); przy narzucie (Plan) - ClrTPaid / ClrK (rynek bez narzutu - te same liczby)
                    bool mark = r.ClrTown[m] > 0;
                    double bud = M.TBudget[m * 3] + M.TBudget[m * 3 + 1] + M.TBudget[m * 3 + 2];
                    double paid = M.TPaid[m * 3] + M.TPaid[m * 3 + 1] + M.TPaid[m * 3 + 2];
                    double paidM = mark ? r.ClrTPaid[m * 3] + r.ClrTPaid[m * 3 + 1] + r.ClrTPaid[m * 3 + 2] : paid;
                    sb.Append(", miasta: wydane ").Append(paid.ToString("0", inv)).Append(" zl/d <= budzet ").Append(bud.ToString("0", inv)).Append(" zl/d (przy narzucie Planu wydane ")
                      .Append(paidM.ToString("0", inv)).Append(" zl/d), W/R/P zaplacone ");
                    for (int c = 0; c < 3; c++)
                    {
                        double b = M.TBasket[m * 3 + c];
                        sb.Append(c > 0 ? "/" : "").Append(b > 0 ? (100 * M.TPaid[m * 3 + c] / b).ToString("0.0", inv) : "-");
                    }
                    sb.Append("% wartosci koszyka przy cenach startu, kcal jedzenia W/R/P przy cenach startu ");
                    int sn = KsCheap.StatN;
                    for (int c = 0; c < 3; c++)
                    {
                        double k0 = M.P9[(m * 3 + c) * sn], k1 = M.P9[(m * 3 + c) * sn + 1];
                        sb.Append(c > 0 ? "/" : "").Append(k0 > 0 ? (100 * k1 / k0).ToString("0.0", inv) : "-");
                    }
                    sb.Append("% (popyt miast do gry), przy narzucie Planu ");
                    for (int c = 0; c < 3; c++)
                    {
                        double k0 = mark ? r.ClrK0[m * 3 + c] : M.P9[(m * 3 + c) * sn], k1 = mark ? r.ClrK1[m * 3 + c] : M.P9[(m * 3 + c) * sn + 1];
                        sb.Append(c > 0 ? "/" : "").Append(k0 > 0 ? (100 * k1 / k0).ToString("0.0", inv) : "-");
                    }
                    sb.Append("% (tyle rece rynku urodza - reszta to brak, ktory pokaza ceny gry, puste polki i glod)");
                    double fk = M.VFullKcal[m];
                    sb.Append(", wsie: drabiny koszyka z narzutem ").Append(r.ClrVil[m]).Append(", kcal koszyka bez pokrycia ")
                      .Append(fk > 0 ? (100 * Math.Max(0.0, M.VUnpaidKcal[m] - M.VHomeKcal[m]) / fk).ToString("0.0", inv) : "-").Append("% robione w domu ")
                      .Append(fk > 0 ? (100 * M.VHomeKcal[m] / fk).ToString("0.0", inv) : "-").Append("%");
                }
                sb.Append(", krok ceny zmniejszony ").Append(r.EtaCut[m]);
                int open = 0; string pre = mk[m] + " ";
                foreach (var o in r.Open) if (o.StartsWith(pre, StringComparison.Ordinal)) open++;
                sb.Append(", towary niezbiezne ").Append(open);
                // n22f-plany (recenzja): niezbiezne wobec pasma [1, wspolny poziom podazy / popytu rynku], poziom uciety do celu kroku 8 (Ks Plan Converge Shelf Level);
                // x1.000 - test jak dotad
                double lv = r.ConvLev != null && m < r.ConvLev.Length ? r.ConvLev[m] : 1.0, lt = r.ConvLevT != null && m < r.ConvLevT.Length ? r.ConvLevT[m] : 1.0;
                sb.Append(" (wobec wspolnego poziomu podazy/popytu x").Append(lv.ToString("0.000", inv)).Append(", cel kroku 8 x").Append(lt.ToString("0.000", inv))
                  .Append(" - zapas polek, ktorego ceny wzgledne nie zbijaja; towar w pasmie od 1 do poziomu zbiezny; stara miara |S/D - 1| > 2%: ");
                int oa = r.OpenAbsM != null && m < r.OpenAbsM.Length ? r.OpenAbsM[m] : open;
                sb.Append(oa).Append(")");
                // recenzja: towary na granicy ceny (podloga 0.05 / sufit) rownowaga liczy jako zbiezne - te z odchyleniem podazy od popytu ponad 2% osobno (nadmiar przy
                // cenie zero to tez rynek bez rownowagi: gaj rodzi, nikt nie kupuje)
                var bnd = new List<string>();
                if (r.S != null && r.D != null)
                    foreach (var o in r.Bounds)
                    {
                        if (!o.StartsWith(pre, StringComparison.Ordinal)) continue;
                        var sp = o.Split(' '); if (sp.Length < 2) continue;
                        int g = Array.IndexOf(KsTab.Goods, sp[1]); if (g < 0) continue;
                        int i = m * KsTab.NG + g; if (i >= r.S.Length || i >= r.D.Length) continue;
                        double sd = r.S[i] / Math.Max(1.0, r.D[i]);
                        if (Math.Abs(sd - 1.0) > Tol) bnd.Add(sp[1] + " S/D " + sd.ToString("0.00", inv));
                    }
                sb.Append(", na granicy ceny z odchyleniem ponad 2% ").Append(bnd.Count).Append(bnd.Count > 0 ? " (" + string.Join(" ", bnd) + ")" : "");
            }
            sb.Append(" | towar: popyt zaplacony miast i wsi = podaz rak - zjedzone (nadwyzka targu ~0); narzut tylko w Planie (rece i popyt Planu; przy narzucie wsi - koszyk i praca domowa wsi); czesc zaplacona koszyka miast idzie do gry przy cenach startu - ceny gry robi rynek gry.");
            return sb.ToString();
        }
    }
}
