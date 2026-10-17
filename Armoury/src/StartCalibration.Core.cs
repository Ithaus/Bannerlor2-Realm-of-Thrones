using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Armoury
{
    /// <summary>KS: wejscie Planu (zrzut na watku glownym - same liczby, bez obiektow gry).</summary>
    internal sealed class KsInput
    {
        public double Sigma = 37.0, Inert = 1.25, Beta = 10.0;
        public bool Ksk = true;                                       // KS-K (koszyk: kozuchy Polnocy w popycie futer)
        public readonly List<KsDistrict> Districts = new List<KsDistrict>();   // okregi wsi gry i pola mieszczan
        public readonly List<KsSettlement> Towns = new List<KsSettlement>();    // miasta (potrzeby do popytu krainy)
        public readonly Dictionary<string, KsItem> Items = new Dictionary<string, KsItem>(StringComparer.Ordinal);   // towary gry: waga, wartosc, zuzycie 1:1
        public int RoundsFirst = 6000, RoundsR8 = 4000, RoundsR8b = 3000;
        public double[] WarmPrices;                                   // [rynek * NG + towar] z poprzedniego Planu (cieply start) albo null
        public Dictionary<string, double> WarmRent;                   // uid bloku -> renta ziemi z poprzedniego Planu
        public bool StopAfterFirst = false;                           // test portu: tylko pierwsza rownowaga (bez lepszego stolu)
        public bool StallExit = true;                                 // test: false - rownowaga bez konca "stoi" (do limitu rund albo zbieznosci)
        public readonly Dictionary<string, double[]> ExoLand = new Dictionary<string, double[]>(StringComparer.Ordinal);   // kraina -> podaz spoza tabeli [NG] kg/d (las wsi, lodzie NavalDLC)
        // recenzja 16.10 (3): mieszczanie jedza wprost surowy jedwab / rude srebra, dopoki poprawki ZC1 tych drog sa wylaczone (SilkToWeavers; srebro - bez klucza) -
        // popyt Planu z prawdziwego zuzycia mieszczan (inaczej Plan zaklada drogi, ktorych w grze nie ma, i start nie jest zbilansowany)
        public bool SilkTownDirect, SilverTownDirect;
        // POPRAWKA PO AUTOTESCIE 16.10 (projekt rozdz. 9 i 11.1 krok 8 "pieniadze" - dotad nie bylo go w kodzie): Plan liczy tylko popyt, za ktory ktos placi.
        //  - wies: budzet koszyka = alfa okregu (KsDistrict.Alpha z pomiaru; bez pomiaru VilAlpha rynku) x wartosc BAZOWA tego, co okreg sprzedaje na targ (z konmi stadnin)
        //    - alfa = wplyw do kiesy wsi / wartosc bazowa produkcji wsi (recenzja 17.10: ta sama podstawa w pomiarze i w Planie);
        //  - klasy miast W/R/P: KsSettlement.Budget (zarobek netto 28 dob x czesc na koszyk);
        //  - drabina koszyka (KsBasket.Ladder) przy cenach startu; to, czego wies nie oplaci, robi w domu (praca domowa - te same rece i ziemia, nigdy na targ),
        //    reszta niezaplacona miast odpada z popytu; punkt staly z tlumieniem 0.5 (MoneyIter rund rownowagi).
        public bool Money = true;
        public double[] VilAlpha = { 0.47, 0.47 };
        public double[][] Keep;                                       // [stan 6][grupa koszyka 13] udzial z wlasnego pola (DailyLife.KsKeepVillage) - koszyk wsi kroku 8
        public int MoneyIter = 10;
        public double HomeMax = 0.85;                                 // najwyzej taka czesc rak zajec okregu w pracy domowej
        public Dictionary<string, double> WarmHome;                   // okreg -> praca domowa z poprzedniego Planu (cieply start kroku 8) albo null
        // recenzja 17.10: alfa okregu (KsDistrict.Alpha) i budzety miast z pomiaru w grze (false - domyslne: wynik kroku 8 nie zastepuje zapisanego w stanie zastosowanym);
        // margines popytu targu (zapas na polki w oknie D..D+28, 1.0 - bez) - poprawka rynku pracy domowej liczy brak wobec popytu targu x margines
        public bool MoneyMeasured;
        public double MarketMargin = 1.0;
        // KS-R3 (17.10, "kupcy i krainy"; projekt rozdz. 5): KROK 5 "korytarze i krainy" (StartCalibration.Corridors.cs) - rynek dalej czyszczony na dwoch rynkach,
        // miejsce wchodzi jednym czynnikiem ceny krainy w dochodzie bloku (Grad), liczonym z problemu przewozu krain (doby wozem, wojna, przepustowosc);
        // regula zasiegu towarow psujacych sie (Reach). Same liczby z watku glownego (bez obiektow gry).
        public bool Corridors;                                        // klucz KsLandGradient: gradient krainy w dochodzie bloku
        public bool Reach;                                            // klucz KsPerishReach: psujace sie bez zrodla w 7 dobach - czesc zaplacona 0
        public bool HomeSub;                                          // klucz KsAlphaNet: krok 8b - zastepczy wlasny towar pracy domowej (kcal za kcal)
        public bool ApplyPlan;                                        // Plan, ktory moze byc wlaczony (stan Plan): druga ciepla runda, gdy gradient zmienia sie > 5% i czas < 15 s
        public bool AfterD;                                           // po D: przepustowosc korytarza z dowozu KS-H (inaczej z kas miast importera)
        public Dictionary<string, int> DistTown;                      // okreg -> indeks miasta targowego w Towns
        public float[] TownDays;                                      // [nt * nt] doby wozem (-1: brak drogi albo inny rynek)
        public bool[] TownSea, TownWar;                               // [nt * nt] para morzem, wlasciciele w wojnie
        public double[] TownGold28;                                   // [nt] srednia 28 dob kasy miasta (zl)
        public Dictionary<string, double> CorrDelivered;              // "kraina zrodla|kraina celu|towar KsTab" -> szt./d dowiezione wozami KS-H (srednia 28 dob)
        public Dictionary<string, double[]> GradPrev;                 // kraina -> [NG] gradient poprzedniego Planu (null - 1)
        public Dictionary<string, double[]> LandCut;                  // kraina -> [NG] ciecie nadmiaru krainy (regula nadmiaru P4; null - 1)
        public Dictionary<string, bool[]> LandGlut;                   // kraina -> [NG] nadmiar krainy rosnacy (potrojna waga w poprawce G '+')
        public double[] MarketFactor;                                 // [NM * NG] czynnik popytu targu (P4; null - MarketMargin dla wszystkich)
        public Dictionary<string, double[]> TownOne;                  // miasto -> [NG] kg/d prawdziwego odbioru panow i dworow w miescie (P3 - popyt miasta reguly zasiegu)
    }

    /// <summary>Towar gry: kg sztuki, wartosc, zuzycie dnia w sztukach (wojsko, zamki, dwory, budowy, warsztaty, razem, mieszczanie).</summary>
    internal sealed class KsItem { public double Kg, Val, Army, Cast, Court, Bud, Ws, Cons, Town; }

    /// <summary>Osada do popytu: kraina (kultura z kalibracji), rynek 0 Westeros / 1 Essos, ludzie, potrzeby [NN] w kg (napoje l) na dobe.</summary>
    internal class KsSettlement
    {
        public string Id, Land; public int Market; public double People;
        public double[] Need0 = new double[KsTab.NN];
        public double[] Need;                                         // biezace (po lepszym stole) - Solve zaklada kopie Need0
        // krok 8 (pieniadze), tylko miasta: potrzeby klas W/R/P [3][NN] (suma = Need0 bez opalu) i budzet koszyka klas w zl gry na dobe; null - placi cale
        public double[][] ClassNeed; public double[] Budget;
        internal double[] NeedR8;                                     // potrzeby po lepszym stole, przed krokiem 8 (Solve)
    }

    /// <summary>Okreg pracy: wies gry z wioskami pobocznymi albo pola mieszczan miasta.</summary>
    internal sealed class KsDistrict : KsSettlement
    {
        public string Type;                                           // typ wsi gry ("town_fields" - pola mieszczan)
        public double Wf;                                             // rece w polu (ludzie x udzial pracy stanu)
        public double Area;                                           // ziemia uzytkowa okregu, ha
        public bool TownFields;
        public long NoReg;                                            // recenzja 16.10 (drobne): zajecia regale zablokowane w tym okregu (bit zajecia; RockGold - zloza Skaly i zloto Zachodu)
        public readonly List<KsBlock> Blocks = new List<KsBlock>();
        public double[] PplHome; public KsCore.Basket[] Bask; public bool Oil;   // gra: ludzie w domu wedlug stanu i koszyki (zjedzone w okregu) - krok 8 (koszyk wsi)
        public double HomeWork;                                       // krok 8: czesc rak zajec w pracy domowej (koszyk, na ktory wsi nie stac - nigdy na targ); 0 - jak dotad
        public double Alpha = -1;                                     // recenzja 17.10: alfa okregu z pomiaru (wplyw do kiesy / wartosc bazowa sprzedazy); < 0 - VilAlpha rynku
        internal int CorrLand = -1;                                   // KS-R3 krok 5: kraina miasta targowego okregu (indeks krain Planu; -1 - wlasna)
    }

    internal sealed class KsBlock
    {
        public string Uid, Start; public double Share, Area; public bool Main, Block0;
        public KsTab.Land O;
        public int Index;                                             // indeks w Solve
    }

    /// <summary>KS: wynik Planu (wszystko w prawdziwych kg na dobe, ceny cienia wzgledem wartosci bazowej).</summary>
    internal sealed class KsResult
    {
        public string Error;
        public int Rounds, RoundsFirst, R8Rounds, R8bRounds; public double Ms;
        public string[] Markets = { "Westeros", "Essos" };
        public double[] P, S, D, SA, D0;                              // [m * NG + g]: ceny cienia, podaz, popyt (po lepszym stole), podaz dzis (zajecia z mapy), popyt przed
        public double[] Table = { 1.0, 1.0 };                         // R8b: lepszy stol wsi rynku
        public double[] Absorbed;                                     // R8: [m * NG + g] kg/d zjedzone w domu zamiast ziarna
        public List<string> Open = new List<string>(), Bounds = new List<string>();
        public double LandBind, RentMean, RentMax;
        public int NB, NP, NDist;
        public KsBlock[] Blocks;
        public int[] PairBlock, PairCraft; public double[] X;         // pary blok-zajecie i udzial rak (logit)
        public int[] BlockStart;                                       // pierwsza para bloku
        public double[] Rent;                                          // renta ziemi bloku
        public double[] RegHands;                                      // [blok * NC + zajecie] rece przy zlozu (regale)
        public double[] BlockW, BlockHome, BlockSub;                   // rece bloku, rece domu, rece zboza wlasnego
        public Dictionary<string, double[]> DistProd = new Dictionary<string, double[]>(StringComparer.Ordinal);   // okreg -> kg/d wedlug towaru
        public Dictionary<string, double[]> DistNeed = new Dictionary<string, double[]>(StringComparer.Ordinal);   // okreg -> potrzeby po lepszym stole
        public double[] RegLabour = new double[KsTab.NC];              // rece regale wedlug zajecia
        public double[] CraftLabour = new double[KsTab.NC];            // rece pola wedlug zajecia (po logicie)
        public double HomeLabour, OwnGrainLabour, TownFieldHands;
        public List<string> RegInfo = new List<string>();
        public double[] Val;                                           // wartosc kg towaru (cena bazowa)
        // krok 8 (pieniadze): czesc rak zajec w pracy domowej (okreg), czesc koszyka targu zaplacona (okreg wsi i miasto; KsBasket.Ids), liczby do linii
        public Dictionary<string, double> DistHome = new Dictionary<string, double>(StringComparer.Ordinal);
        public Dictionary<string, float[]> DistPaid = new Dictionary<string, float[]>(StringComparer.Ordinal);
        public Dictionary<string, float[]> TownPaid = new Dictionary<string, float[]>(StringComparer.Ordinal);
        public KsMoney Money;
        public double HomeWorkLabour;
        // recenzja 17.10: czesc kcal pelnego koszyka wsi, ktora praca domowa okregu naprawde robi (pozycje HomeMask x czesc pokryta) - gra: racje spichlerza najwyzej tyle;
        // zjedzone w okregach [m * NG + g] (bilans NA TARG: (podaz - zjedzone) / (popyt - zjedzone)); krok 8 z pomiaru w grze
        public Dictionary<string, float> DistHomeKcal = new Dictionary<string, float>(StringComparer.Ordinal);
        public double[] Eaten;
        public bool MoneyMeasured;
        // KS-R3 krok 5: gradient krainy (kraina -> [NG], tylko jedzenie; reszta 1), przewoz krain, kraina-tryb, psujace sie bez zrodla, linie i pliki z watku roboczego
        public Dictionary<string, double[]> Grad;
        public List<KsFlow> LandFlow = new List<KsFlow>();
        public List<string> Kraina = new List<string>(), Unreached = new List<string>();
        public double GradMax = 1.0, GradDMax; public int GradDBig;
        public int CorrRounds;
        public double CorrMs;
        public string CorrLine, CsvPlan, CsvBudgets;                   // C2: tekst linii korytarzy i plikow ks-plan.csv / ks-budzety.csv z watku roboczego
        public int CsvPlanRows, CsvBudgetRows;
        public double[] MarketFactor;                                  // czynnik popytu targu uzyty w kroku 8
    }

    /// <summary>KS-R3 krok 5: przewoz Planu na korytarzu krain (szt. gry na dobe).</summary>
    internal sealed class KsFlow { public string From, To; public int G, M; public double Pcs, Days; public bool Sea; }

    /// <summary>KS krok 8: liczby pieniedzy Planu na rynek (zl gry na dobe przy cenach startu) - linie "KS: pieniadze planu" i "KS: koszyk niezaplacony".</summary>
    internal sealed class KsMoney
    {
        public int Iter; public bool On;
        public double[] VSales = new double[2], VBudget = new double[2], VBasket = new double[2], VPaid = new double[2], VHome = new double[2], VHands = new double[2], VHomeHands = new double[2];
        public int[] VShort = new int[2], VN = new int[2];
        public double[] TBudget = new double[6], TBasket = new double[6], TPaid = new double[6];   // [m * 3 + klasa]
        public double[] Excess = new double[2], Global = new double[2], TUnRaw = new double[2];
        public double[] UnpaidV = new double[2 * KsBasket.N], UnpaidT = new double[2 * KsBasket.N];   // kg/d niezaplacone (wsie: robione w domu; miasta: odpada)
        public List<string> Trace = new List<string>();
        // recenzja 17.10: sprzedaz w wartosci bazowej (podstawa budzetu), z tego konie stadnin, sprzedaz bez pracy domowej (te same zajecia), alfa okregow (najmniejsza,
        // mediana, najwieksza; z pomiaru), kcal koszyka wsi (pelny, niezaplacony, robiony w domu), surowiec niezaplacony bez pokrycia (poza HomeMask), zbieznosc punktu stalego
        public double[] VSalesB = new double[2], VHorse = new double[2], VSalesNoHw = new double[2], ExcessRel = new double[2], HwLeft = new double[2];
        public double[] AlphaMin = new double[2], AlphaMed = new double[2], AlphaMax = new double[2];
        public int[] AlphaN = new int[2];
        public double[] VFullKcal = new double[2], VUnpaidKcal = new double[2], VHomeKcal = new double[2], VUnmetRaw = new double[2];
        public bool[] Conv = new bool[2], NoHands = new bool[2];
        public double DHw = -1, DG, Margin = 1.0;
        // KS-R3: alfa netto (z odjetymi wydatkami spoza koszyka), kcal zastepcze kroku 8b, czynnik popytu targu (najmniejszy/najwiekszy), ciecie zasiegu psujacych sie
        public double[] VSubKcal = new double[2], MfMin = { 1, 1 }, MfMax = { 1, 1 };
        public int[] ReachCut = new int[2];
        public double[] DHwM = new double[2]; public int[] DHwN = new int[2];   // ostatnia runda: najwieksza zmiana pracy domowej okregu i okregi ze zmiana >= 0.05
        public double[] DTot = { -1, -1 };                              // ostatnia runda: najwieksza wzgledna zmiana sum rynku (sprzedaz, zaplacone, rece w pracy domowej)
    }

    /// <summary>
    /// KS - RDZEN PLANU (port ks_model2.py, rozdz. 3-6 i 11.1 projektu). Czysty C#: liczby z wejscia, wynik zawsze ten sam dla tego samego wejscia
    /// (watek roboczy, bez obiektow gry). Kroki: produkcja par blok-zajecie (rece x P0 x L, dom, zboze wlasne), popyt krain (cywile + 1:1 x sigma + wsady),
    /// regale (rece przy zlozu ida za popytem przy cenie 1), rownowaga reguly dochodu (logit beta 10 z premia mapy, ceny cienia, renta ziemi, test 2%),
    /// lepszy stol R8 (nadwyzki uboczne zostaja w domu) i R8b (wspolna nadwyzka rak - koszyk wsi do +10%).
    /// </summary>
    internal static partial class KsCore
    {
        private const double PLo = 0.05, PHi = 5.0, Eta = 0.05, Tol = 0.02;
        private const int StallN = 10;                                 // rownowaga stoi: porownanie sumy odchylen co 50 rund z wartoscia sprzed 10 x 50 rund
        private const int NM = 2;

        private sealed partial class Ctx
        {
            public KsInput In; public KsResult R;
            public List<KsDistrict> D; public KsBlock[] B; public KsDistrict[] BD; public int NB, NP;
            public int[] Pb, Pc, Pm, BStart; public double[] Lp, St, LandP, Area;
            public int[] OStart, OGood; public double[] OVal;          // OUTP0 rzadko: para -> (towar, kg/d)
            public double[] BlockScale;                                // OUTP = OUTP0 x skala bloku (regale zdejmuje rece)
            public double[] Rent;
            public double[] MV;                                        // [c * NG + g]
            public double[] Val;
            public double[] Sreg;                                      // [m * NG + g]
            public double[] RegDed;                                    // rece regale bloku
            public double[] RegHands;                                  // [b * NC + c]
            public Dictionary<string, double[]> RegByDist;
            public List<string> Lands; public Dictionary<string, int> LandMk; public Dictionary<string, double> LPop; public double WPop;
            public List<RegRow>[] RegRows;                             // [regale good] lista wierszy
            public double[] W, WH, Sub;                                // rece bloku, dom, zboze wlasne
            public double[] Dm;                                        // popyt rynku
            public Dictionary<string, double[]> DL;                    // popyt krainy
        }
        private sealed class RegRow { public int B, C; public double L, Cap; public string Land; }

        internal static KsResult Solve(KsInput inp, Func<bool> cancel)
        {
            var sw = Stopwatch.StartNew();
            var R = new KsResult();
            try
            {
                var x = new Ctx { In = inp, R = R, D = inp.Districts };
                R.MoneyMeasured = inp.MoneyMeasured;
                Prepare(x);
                R.Val = x.Val;
                Demand(x);
                var dm0 = (double[])x.Dm.Clone();
                R.D0 = dm0;
                // A: dzisiejsze zajecia z mapy (start) x tabela - do porownania
                var sa = new double[NM * KsTab.NG];
                for (int k = 0; k < x.NP; k++)
                    if (x.St[k] > 0) for (int j = x.OStart[k]; j < x.OStart[k + 1]; j++) sa[x.Pm[k] * KsTab.NG + x.OGood[j]] += x.OVal[j];
                R.SA = sa;
                RegaleStep(x);
                double[] p = null;
                if (inp.WarmPrices != null && inp.WarmPrices.Length == NM * KsTab.NG) p = (double[])inp.WarmPrices.Clone();
                double[] xs, S;
                int rounds;
                p = Equilibrium(x, p, inp.RoundsFirst, out xs, out S, out rounds, cancel);
                R.RoundsFirst = rounds; R.Rounds = rounds;
                if (cancel != null && cancel()) { R.Error = "przerwane"; return R; }
                if (inp.StopAfterFirst) { R.Absorbed = new double[NM * KsTab.NG]; Finish(x, p, xs, S); R.Ms = sw.Elapsed.TotalMilliseconds; return R; }
                // R8: lepszy stol - nadwyzki uboczne gospodarstw zostaja na stole okregu (zamiast ziarna)
                var absorbed = new double[NM * KsTab.NG];
                for (int rnd = 0; rnd < 4; rnd++)
                {
                    var prod = DistrictProd(x, xs);
                    bool changed = false;
                    for (int m = 0; m < NM; m++)
                        for (int a = 0; a < Absorb.Length; a++)
                        {
                            int g = Absorb[a].G; double alpha = Absorb[a].Alpha, kcal = Absorb[a].Kcal;
                            double dmg = x.Dm[m * KsTab.NG + g], surplus = S[m * KsTab.NG + g] - dmg;
                            if (surplus <= 0.02 * dmg) continue;
                            int key = g == KsTab.GHides ? KsTab.NSkora : NeedOfGood(g);
                            double f = g == KsTab.GHides ? KsTab.LeatherHide : 1.0;
                            var own = new List<KeyValuePair<KsDistrict, double>>();
                            double tot = 0;
                            foreach (var d in x.D)
                            {
                                if (d.Market != m) continue;
                                double[] pr; if (!prod.TryGetValue(d.Id, out pr)) continue;
                                double ex = pr[g] - d.Need[key] * f;
                                double room = alpha * d.Need0[key] * f - (d.Need[key] - d.Need0[key]) * f;
                                if ((g == KsTab.GDate || g == KsTab.GOlives || g == KsTab.GOrange) && d.Need0[key] <= 0) room = 0.0;
                                if (ex > 0 && room > 0) { double v = Math.Min(ex, room); own.Add(new KeyValuePair<KsDistrict, double>(d, v)); tot += v; }
                            }
                            if (tot <= 0) continue;
                            double take = Math.Min(1.0, (surplus - 0.02 * dmg) / tot);
                            foreach (var kv in own)
                            {
                                var d = kv.Key; double av = kv.Value * take;
                                if (g == KsTab.GHides) d.Need[KsTab.NSkora] += av / KsTab.LeatherHide;
                                else
                                {
                                    d.Need[key] += av;
                                    d.Need[KsTab.NG_] = Math.Max(0.25 * d.Need0[KsTab.NG_], d.Need[KsTab.NG_] - av * kcal / 3300.0);
                                }
                                absorbed[m * KsTab.NG + g] += av;
                            }
                            changed = true;
                        }
                    if (!changed) break;
                    Demand(x);
                    RegaleStep(x);
                    p = Equilibrium(x, p, inp.RoundsR8, out xs, out S, out rounds, cancel);
                    R.R8Rounds += rounds; R.Rounds += rounds;
                    if (cancel != null && cancel()) { R.Error = "przerwane"; return R; }
                }
                R.Absorbed = absorbed;
                // R8b: wspolna nadwyzka rak rynku - koszyk wsi w gore o mediane nadwyzki towarow rdzenia, najwyzej +10% (brak nie jest zmniejszany)
                for (int rnd = 0; rnd < 3; rnd++)
                {
                    bool changed = false;
                    for (int m = 0; m < NM; m++)
                    {
                        var rs = new List<double>();
                        foreach (int g in Core)
                        {
                            double dm = x.Dm[m * KsTab.NG + g];
                            if (dm > 0) rs.Add(S[m * KsTab.NG + g] / dm);
                        }
                        rs.Sort();
                        double c = rs.Count > 0 ? rs[rs.Count / 2] : 1.0;
                        double f = Math.Min(1.10 / R.Table[m], c);
                        if (f <= 1.01) continue;
                        R.Table[m] *= f;
                        foreach (var d in x.D)
                        {
                            if (d.Market != m || d.TownFields) continue;
                            foreach (int k in FoodKeys) d.Need[k] *= f;
                        }
                        changed = true;
                    }
                    if (!changed) break;
                    Demand(x);
                    RegaleStep(x);
                    p = Equilibrium(x, p, inp.RoundsR8b, out xs, out S, out rounds, cancel);
                    R.R8bRounds += rounds; R.Rounds += rounds;
                    if (cancel != null && cancel()) { R.Error = "przerwane"; return R; }
                }
                // KS-R3 krok 5: korytarze i krainy - problem przewozu krain na rownowadze, gradient krainy (cieply z poprzedniego Planu, tlumienie 0.5) do rownowag kroku 8;
                // Plan wlaczenia: druga ciepla runda, gdy gradient zmienil sie o ponad 5% i od startu minelo mniej niz 15 s; regula zasiegu psujacych sie do kroku 8
                if (inp.Corridors || inp.Reach)
                {
                    CorridorStep(x, p, xs, S);
                    if (inp.Corridors && inp.ApplyPlan && R.GradDMax > 0.05 && sw.Elapsed.TotalMilliseconds < 15000)
                    {
                        Demand(x);
                        RegaleStep(x);
                        p = Equilibrium(x, p, inp.RoundsR8b, out xs, out S, out rounds, cancel);
                        R.Rounds += rounds;
                        if (cancel != null && cancel()) { R.Error = "przerwane"; return R; }
                        CorridorStep(x, p, xs, S);
                    }
                }
                // krok 8: pieniadze - popyt tylko zaplacony, praca domowa wsi, ktorych nie stac na koszyk (projekt rozdz. 9, 11.1)
                if (inp.Money) { MoneyStep(x, ref p, ref xs, ref S, cancel); if (cancel != null && cancel()) { R.Error = "przerwane"; return R; } }
                if (inp.Corridors || inp.Reach) { try { CorridorLine(x); } catch (Exception ce) { R.CorrLine = "KS: korytarze planu - blad linii (" + ce.GetType().Name + ")"; } }
                Finish(x, p, xs, S);
            }
            catch (Exception e) { R.Error = e.GetType().Name + ": " + e.Message; }
            R.Ms = sw.Elapsed.TotalMilliseconds;
            return R;
        }

        private struct Abs { public int G; public double Alpha, Kcal; public Abs(int g, double a, double k) { G = g; Alpha = a; Kcal = k; } }
        private static readonly Abs[] Absorb =
        {
            new Abs(KsTab.GMeat, 1.0, 2200), new Abs(KsTab.GEgg, 1.0, 1450), new Abs(KsTab.GHoney, 0.3, 3000), new Abs(KsTab.GCarrot, 0.6, 400),
            new Abs(KsTab.GApple, 0.6, 500), new Abs(KsTab.GCheese, 0.5, 3500), new Abs(KsTab.GFish, 0.5, 1100), new Abs(KsTab.GHides, 2.0, 0),
            new Abs(KsTab.GDate, 1.0, 2800), new Abs(KsTab.GOlives, 0.5, 1150), new Abs(KsTab.GOrange, 0.6, 450)
        };
        private static readonly int[] Core = { KsTab.GGrain, KsTab.GFish, KsTab.GCheese, KsTab.GButter, KsTab.GGrape, KsTab.GOlives, KsTab.GOrange, KsTab.GCarrot, KsTab.GApple, KsTab.GMeat };
        private static readonly int[] FoodKeys = { KsTab.NG_, KsTab.NH, KsTab.NMeat, KsTab.NFish, KsTab.NCheese, KsTab.NButter, KsTab.NEgg, KsTab.NApple, KsTab.NCarrot, KsTab.NOrange,
                                                   KsTab.NGrape, KsTab.NOlives, KsTab.NDate, KsTab.NBeer, KsTab.NWine, KsTab.NOil, KsTab.NMead, KsTab.NHoney };

        /// <summary>Klucz potrzeby, ktory jest towarem (R8: mieso, jaja, owoce, ser, ryby, miod).</summary>
        private static int NeedOfGood(int g)
        {
            switch (g)
            {
                case KsTab.GMeat: return KsTab.NMeat;
                case KsTab.GEgg: return KsTab.NEgg;
                case KsTab.GHoney: return KsTab.NHoney;
                case KsTab.GCarrot: return KsTab.NCarrot;
                case KsTab.GApple: return KsTab.NApple;
                case KsTab.GCheese: return KsTab.NCheese;
                case KsTab.GFish: return KsTab.NFish;
                case KsTab.GDate: return KsTab.NDate;
                case KsTab.GOlives: return KsTab.NOlives;
                case KsTab.GOrange: return KsTab.NOrange;
                default: return -1;
            }
        }

        // ============================================================ 1. bloki, pary, produkcja par (block_output)
        /// <summary>Przydatnosc zajecia c w bloku: land2; punkt wsi gry (blok 0) ma typ wsi co najmniej 0.3.</summary>
        internal static double LandEff(KsBlock b, int c, string dtype)
        {
            double L = KsTab.Land2(c, b.O, dtype);
            if (b.Main && b.Block0 && KsTab.Crafts[c] == dtype && L < 0.3) L = 0.3;
            return L;
        }

        /// <summary>Kandydaci bloku (zajecia pola z L >= 0.3, zajecie startowe; bez regale) - jak PAIRS modelu.</summary>
        internal static List<int> Candidates(KsBlock b, string dtype)
        {
            var c = new List<int>(12);
            for (int i = 0; i < KsTab.NC; i++) if (!KsTab.IsRegale[i] && KsTab.Land2(i, b.O, dtype) >= 0.3) c.Add(i);
            int st = -1;
            if (b.Start == null || !KsTab.CraftIndex.TryGetValue(b.Start, out st)) st = -1;
            if (st >= 0 && !c.Contains(st) && !KsTab.IsRegale[st]) c.Add(st);
            if (st >= 0 && KsTab.IsRegale[st] && c.Count == 0) c.Add(KsTab.CSheep);
            if (c.Count == 0) c.Add(KsTab.CSheep);
            return c;
        }

        /// <summary>Rece bloku: wszystkie (W), dom (wH), L zboza i L domu. need0H i grainNeed - potrzeby okregu (kg/d).</summary>
        internal static void HomeLabour(double wf, double share, double needH, KsTab.Land o, string dtype, out double W, out double wH, out double lg, out double lh)
        {
            W = wf * share;
            lg = KsTab.Land2(KsTab.CWheat, o, dtype);
            lh = 0.5 + 0.5 * Math.Min(1.0, lg);
            wH = Math.Min(W, needH * share / (KsTab.PH * lh));
            if (!(wH >= 0)) wH = 0;
        }

        /// <summary>Czesc rak zajec okregu w pracy domowej (krok 8) w 0..0.95; pola mieszczan - 0.</summary>
        internal static double HomeOf(KsDistrict d)
        {
            if (d == null || d.TownFields) return 0.0;
            double h = d.HomeWork;
            return h > 0 && !double.IsNaN(h) ? Math.Min(0.95, h) : 0.0;
        }

        /// <summary>Potrzeba ziarna rodziny (z piwem domowym 90%).</summary>
        internal static double GrainOwnNeed(double[] need0) { return need0[KsTab.NG_] + 0.9 * need0[KsTab.NBeer] * KsTab.BeerGrain; }

        /// <summary>Produkcja pary (blok, zajecie) w kg/d: dom (jaja, miod), zboze wlasne, zajecie (P0 x L), zywiec na mieso i skory. out lab - rece domu, zboza, zajecia.</summary>
        internal static void BlockOutput(KsDistrict d, KsBlock b, int c, double[] outv, out double wH, out double sub, out double cl, out double W)
        {
            Array.Clear(outv, 0, outv.Length);
            double lg, lh;
            HomeLabour(d.Wf, b.Share, d.Need0[KsTab.NH], b.O, d.Type, out W, out wH, out lg, out lh);
            double rem = W - wH;
            outv[KsTab.GEgg] += wH * KsTab.PHEgg * lh; outv[KsTab.GHoney] += wH * KsTab.PHHoney * lh;
            sub = 0.0;
            if (c != KsTab.CWheat && lg > 0)
            {
                double ng = GrainOwnNeed(d.Need0) * b.Share;
                sub = Math.Min(rem, KsTab.OwnGrainShare(lg) * ng / (KsTab.PGrainSub * lg));
                outv[KsTab.GGrain] += sub * KsTab.PGrainSub * lg;
            }
            cl = (rem - sub) * (1.0 - HomeOf(d));                 // krok 8: praca domowa okregu zdejmuje rece zajec (nie dom i nie zboze wlasne)
            double L = LandEff(b, c, d.Type);
            // recenzja 17.10 (krytyczne): konie robi lancuch gry (poza KS) - praca domowa ich nie zdejmuje; rece zajec bez (1 - hw) jak w grze
            foreach (var kv in KsTab.P0[c]) outv[kv.Key] += (kv.Key == KsTab.GHorse ? rem - sub : cl) * kv.Value * L;
            foreach (int a in new[] { KsTab.GCow, KsTab.GHog, KsTab.GSheep })
            {
                outv[KsTab.GMeat] += outv[a] * KsTab.MeatOfLive;
                outv[KsTab.GHides] += outv[a] * KsTab.HideOfLive(a);
            }
            outv[KsTab.GMeat] += outv[KsTab.GWhaleMeat];
        }

        private static void Prepare(Ctx x)
        {
            var inp = x.In;
            // wartosc kg (VAL modelu): wartosc / max(1, kg sztuki); kon 120 d
            x.Val = new double[KsTab.NG];
            for (int g = 0; g < KsTab.NG; g++)
            {
                KsItem it;
                x.Val[g] = inp.Items.TryGetValue(KsTab.Goods[g], out it) && it != null ? it.Val / Math.Max(1.0, it.Kg) : 1.0;
            }
            x.Val[KsTab.GHorse] = KsTab.HorseVal;
            // MV: wartosc towarow zajecia na pracujacego (zywiec liczony jako mieso i skory, mieso bez uboju jako mieso)
            x.MV = CraftValue(x.Val);
            // potrzeby biezace = kopia potrzeb startu
            foreach (var d in x.D) d.Need = (double[])d.Need0.Clone();
            // krok 8: praca domowa z poprzedniego Planu (cieply start punktu stalego) - bez kroku 8 zawsze 0
            foreach (var d in x.D)
            {
                double hw = 0;
                if (inp.Money && inp.WarmHome != null && !d.TownFields) inp.WarmHome.TryGetValue(d.Id, out hw);
                d.HomeWork = hw > 0 && hw <= inp.HomeMax ? hw : 0.0;
            }
            foreach (var t in inp.Towns) t.Need = (double[])t.Need0.Clone();
            // bloki: bloki okregu o tej samej ziemi i tym samym zajeciu startowym rachunek laczy w jeden (udzial i ziemia sie sumuja - wszystko w bloku jest liniowe
            // w udziale: rece, dom, zboze wlasne, ziemia, sufit regale; renta i logit te same) - wynik dokladnie ten sam, 2-3 razy mniej par. Blok wejscia dostaje
            // Index bloku rachunku (wynik czyta sie przez niego)
            var blocks = new List<KsBlock>(16000); var bd = new List<KsDistrict>(16000);
            var keys = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var d in x.D)
            {
                keys.Clear();
                foreach (var b in d.Blocks)
                {
                    var o = b.O;
                    string k = o.Zone + "|" + o.Terr + "|" + o.Kind + "|" + o.Model + "|" + o.DSea.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|"
                               + o.DRiver.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" + o.DMount.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                               + "|" + (o.Main ? 1 : 0) + "|" + (b.Main ? 1 : 0) + "|" + (b.Block0 ? 1 : 0) + "|" + b.Start;
                    int j;
                    if (keys.TryGetValue(k, out j)) { blocks[j].Share += b.Share; blocks[j].Area += b.Area; b.Index = j; continue; }
                    var m = new KsBlock { Uid = b.Uid, Start = b.Start, Share = b.Share, Area = b.Area, Main = b.Main, Block0 = b.Block0, O = b.O, Index = blocks.Count };
                    keys[k] = m.Index; b.Index = m.Index;
                    blocks.Add(m); bd.Add(d);
                }
            }
            x.B = blocks.ToArray(); x.BD = bd.ToArray(); x.NB = x.B.Length;
            x.Area = new double[x.NB];
            x.W = new double[x.NB]; x.WH = new double[x.NB]; x.Sub = new double[x.NB];
            for (int i = 0; i < x.NB; i++) x.Area[i] = x.B[i].Area > 0 ? x.B[i].Area : 1e12;
            Pairs(x);
            x.BlockScale = new double[x.NB]; for (int i = 0; i < x.NB; i++) x.BlockScale[i] = 1.0;
            x.Rent = new double[x.NB];
            if (inp.WarmRent != null)
                for (int i = 0; i < x.NB; i++) { double r; if (x.B[i].Uid != null && inp.WarmRent.TryGetValue(x.B[i].Uid, out r) && r >= 0 && r <= 0.5) x.Rent[i] = r; }
            // krainy: ludzie, rynek wiekszosci ludzi
            x.LPop = new Dictionary<string, double>(StringComparer.Ordinal);
            var lm = new Dictionary<string, double[]>(StringComparer.Ordinal);
            var lands = new SortedSet<string>(StringComparer.Ordinal);
            Action<KsSettlement> addLand = s =>
            {
                string l = s.Land ?? "?";
                lands.Add(l);
                double v; x.LPop.TryGetValue(l, out v); x.LPop[l] = v + s.People;
                double[] a; if (!lm.TryGetValue(l, out a)) { a = new double[NM + 1]; a[NM] = -1; lm[l] = a; }
                a[s.Market] += s.People;
                if (a[NM] < 0) a[NM] = s.Market;                  // pierwszy rynek krainy - remis jak Counter.most_common (pierwszy wstawiony)
            };
            foreach (var d in x.D) addLand(d);
            foreach (var t in inp.Towns) addLand(t);
            x.Lands = new List<string>(lands);
            x.WPop = 0; foreach (var v in x.LPop.Values) x.WPop += v;
            if (!(x.WPop > 0)) x.WPop = 1;
            x.LandMk = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in lm)
            {
                int first = (int)kv.Value[NM], best = first;
                for (int m = 0; m < NM; m++) if (kv.Value[m] > kv.Value[best]) best = m;
                x.LandMk[kv.Key] = best;
            }
            CorridorPrepare(x);                                        // KS-R3 krok 5: indeksy krain, para -> kraina, gradient z poprzedniego Planu
            // regale: wiersze (blok, zajecie, L, sufit = polowa rak bloku poza domem x P0 x L)
            x.RegRows = new List<RegRow>[KsTab.NG];
            for (int i = 0; i < x.NB; i++)
            {
                var b = x.B[i]; var d = x.BD[i];
                double craftLab = Math.Max(0.0, x.W[i] - x.WH[i]);
                foreach (int c in KsTab.Regale)
                {
                    if ((d.NoReg & (1L << c)) != 0) continue;              // zloze zablokowane w grze (RockGold) - L = 0, jak wies bez zloza
                    double L = (b.Main && KsTab.Crafts[c] == d.Type) ? LandEff(b, c, d.Type) : KsTab.Land2(c, b.O, d.Type);
                    if (c == KsTab.CLumber && L < 0.8) continue;
                    if (L < 0.5) continue;
                    int g = KsTab.RegGood(c);
                    double p0 = 0; foreach (var kv in KsTab.P0[c]) if (kv.Key == g) p0 = kv.Value;
                    if (x.RegRows[g] == null) x.RegRows[g] = new List<RegRow>();
                    x.RegRows[g].Add(new RegRow { B = i, C = c, L = L, Cap = craftLab * 0.5 * p0 * L, Land = d.Land ?? "?" });
                }
            }
        }

        /// <summary>Pary blok-zajecie i ich produkcja (OUTP0 rzadko, ziemia pary) - przy starcie i po zmianie pracy domowej okregow (krok 8). Kolejnosc par stala.</summary>
        private static void Pairs(Ctx x)
        {
            var pb = new List<int>(120000); var pc = new List<int>(120000); var pm = new List<int>(120000);
            var lp = new List<double>(120000); var st = new List<double>(120000); var landp = new List<double>(120000);
            var os = new List<int>(120000); var og = new List<int>(600000); var ov = new List<double>(600000);
            var bstart = new int[x.NB + 1];
            var outv = new double[KsTab.NG];
            for (int i = 0; i < x.NB; i++)
            {
                var b = x.B[i]; var d = x.BD[i];
                bstart[i] = pb.Count;
                foreach (int c in Candidates(b, d.Type))
                {
                    double wH, sub, cl, W;
                    BlockOutput(d, b, c, outv, out wH, out sub, out cl, out W);
                    x.W[i] = W; x.WH[i] = wH;
                    pb.Add(i); pc.Add(c); pm.Add(d.Market);
                    lp.Add(Math.Max(1e-6, LandEff(b, c, d.Type)));
                    st.Add(!d.TownFields && b.Start == KsTab.Crafts[c] ? 1.0 : 0.0);
                    // praca domowa (krok 8) na ziemi domu
                    double hwk = Math.Max(0.0, W - wH - sub - cl);
                    landp.Add(wH * KsTab.HaHome + sub * KsTab.HaOwnGrain + cl * KsTab.HaPerWorker[c] + hwk * KsTab.HaHome);
                    os.Add(og.Count);
                    for (int g = 0; g < KsTab.NG; g++) if (outv[g] != 0.0) { og.Add(g); ov.Add(outv[g]); }
                }
            }
            bstart[x.NB] = pb.Count;
            os.Add(og.Count);
            x.NP = pb.Count;
            x.Pb = pb.ToArray(); x.Pc = pc.ToArray(); x.Pm = pm.ToArray(); x.Lp = lp.ToArray(); x.St = st.ToArray(); x.LandP = landp.ToArray();
            x.OStart = os.ToArray(); x.OGood = og.ToArray(); x.OVal = ov.ToArray(); x.BStart = bstart;
            if (x.LandIx != null) BuildPl(x);                          // KS-R3 krok 5: para -> kraina x rynek (pary od nowa w kroku 8)
        }

        /// <summary>Wartosc towarow zajecia na pracujacego przy ziemi 1 i cenie bazowej [c * NG + g] (MV modelu): zywiec jako mieso (polowa) i skory, mieso bez uboju jako mieso.</summary>
        internal static double[] CraftValue(double[] val)
        {
            var mv = new double[KsTab.NC * KsTab.NG];
            for (int c = 0; c < KsTab.NC; c++)
                foreach (var kv in KsTab.P0[c])
                {
                    int g = kv.Key; double r = kv.Value;
                    if (g == KsTab.GCow || g == KsTab.GHog || g == KsTab.GSheep)
                    {
                        mv[c * KsTab.NG + KsTab.GMeat] += r * KsTab.MeatOfLive * val[KsTab.GMeat];
                        mv[c * KsTab.NG + KsTab.GHides] += r * KsTab.HideOfLive(g) * val[KsTab.GHides];
                    }
                    else if (g == KsTab.GWhaleMeat) mv[c * KsTab.NG + KsTab.GMeat] += r * val[KsTab.GMeat];
                    else mv[c * KsTab.NG + g] += r * val[g];
                }
            return mv;
        }

        // ============================================================ 2. popyt krain (demand_land)
        private static double OneToOne(Ctx x, string id)
        {
            KsItem it;
            if (!x.In.Items.TryGetValue(id, out it) || it == null) return 0.0;
            double pcs = it.Army + it.Cast + it.Court;
            if (id == "hardwood" || id == "clay" || id == "limestone" || id == "marble" || id == "tools" || id == "planks") pcs += it.Bud;
            if (id == "iron" || id == "hardwood" || id == "silver") pcs += it.Ws;
            return pcs * x.In.Sigma * it.Kg;
        }

        private static void Demand(Ctx x)
        {
            x.DL = new Dictionary<string, double[]>(StringComparer.Ordinal);
            var civ = new Dictionary<string, double[]>(StringComparer.Ordinal);
            Action<KsSettlement> add = s =>
            {
                string l = s.Land ?? "?";
                double[] a; if (!civ.TryGetValue(l, out a)) { a = new double[KsTab.NN]; civ[l] = a; }
                for (int k = 0; k < KsTab.NN; k++) a[k] += s.Need[k];
            };
            foreach (var d in x.D) add(d);
            foreach (var t in x.In.Towns) add(t);
            double sigma = x.In.Sigma;
            x.Dm = new double[NM * KsTab.NG];
            foreach (var l in x.Lands)
            {
                double sh = x.LPop[l] / x.WPop;
                double[] C; if (!civ.TryGetValue(l, out C)) C = new double[KsTab.NN];
                var D = new double[KsTab.NG];
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
                D[KsTab.GSpice] += C[KsTab.NSpice];
                D[KsTab.GFlax] += (C[KsTab.NPlotno] + o("linen")) * KsTab.LinenFlax;
                D[KsTab.GWool] += (C[KsTab.NSukno] + o("felt")) * KsTab.FeltWool;
                D[KsTab.GHides] += (C[KsTab.NSkora] + o("leather")) * KsTab.LeatherHide;
                D[KsTab.GWood] += C[KsTab.NDrewno] + o("hardwood");
                D[KsTab.GClay] += (C[KsTab.NGarnki] + o("pottery")) * KsTab.PotClay + o("clay");
                D[KsTab.GLime] += o("limestone"); D[KsTab.GMarble] += o("marble");
                D[KsTab.GIron] += (C[KsTab.NNarz] + o("tools")) * KsTab.ToolsIron + o("iron");
                D[KsTab.GWhaleOil] += C[KsTab.NSwTran] + o("whale_oil");
                if (x.In.Ksk) D[KsTab.GFur] += C[KsTab.NFutro];
                foreach (var id in new[] { "fur", "cotton", "spice", "walrus_tusk", "gold_ore" })
                {
                    KsItem it; if (!x.In.Items.TryGetValue(id, out it) || it == null) continue;
                    double cons = it.Cons - (id == "cotton" && !x.In.SilkTownDirect ? it.Town : 0.0);   // jedwab mieszczan odjety tylko przy czynnym SilkToWeavers
                    D[KsTab.GoodIndex[id]] += Math.Max(0.0, cons) * sigma * it.Kg * sh;
                }
                KsItem vel, jew;
                if (x.In.Items.TryGetValue("velvet", out vel) && vel != null) D[KsTab.GCotton] += vel.Cons * sigma * vel.Kg * KsTab.VelvetSilk * sh;
                if (x.In.Items.TryGetValue("jewelry", out jew) && jew != null) D[KsTab.GSilver] += jew.Cons * sigma * jew.Kg * KsTab.JewelSilver / 100.0 * sh;
                D[KsTab.GSilver] += o("silver");
                // recenzja 16.10 (3): ruda srebra jedzona wprost przez mieszczan (116 z 165 szt./d) - prawdziwe zuzycie, dopoki brak poprawki ZC1 (srebro do zlotnikow)
                KsItem sil; if (x.In.SilverTownDirect && x.In.Items.TryGetValue("silver", out sil) && sil != null) D[KsTab.GSilver] += Math.Max(0.0, sil.Town) * sigma * sil.Kg * sh;
                D[KsTab.GHorse] += KsTab.HorseGameDay * sigma * sh;
                if (KsTab.GarumLands.Contains(l)) { D[KsTab.GFish] += KsTab.Garum * 2.0 / KsTab.GarumLands.Count; D[KsTab.GSalt] += KsTab.Garum * 0.4 / KsTab.GarumLands.Count; }
                x.DL[l] = D;
                int mk; if (!x.LandMk.TryGetValue(l, out mk)) mk = 0;
                for (int g = 0; g < KsTab.NG; g++) x.Dm[mk * KsTab.NG + g] += D[g];
            }
        }

        // ============================================================ 3. regale (rece przy zlozu ida za popytem przy cenie 1; pula = strefa krainy)
        private static void RegaleStep(Ctx x)
        {
            x.Sreg = new double[NM * KsTab.NG];
            x.RegDed = new double[x.NB];
            x.RegHands = new double[x.NB * KsTab.NC];
            x.RegByDist = new Dictionary<string, double[]>(StringComparer.Ordinal);
            x.R.RegInfo.Clear();
            Array.Clear(x.R.RegLabour, 0, KsTab.NC);
            foreach (int g in KsTab.RegGoodsSorted)
            {
                var rows = x.RegRows[g] ?? new List<RegRow>();
                for (int z = 0; z < NM; z++)
                {
                    var lands = new List<string>();
                    foreach (var l in x.Lands) { int mk; if (x.LandMk.TryGetValue(l, out mk) && mk == z) lands.Add(l); }
                    var cap = new Dictionary<string, double>(StringComparer.Ordinal); var need = new Dictionary<string, double>(StringComparer.Ordinal);
                    var use = new Dictionary<string, double>(StringComparer.Ordinal);
                    foreach (var l in lands) { double[] ex; cap[l] = 0; need[l] = Math.Max(0.0, x.DL[l][g] - (x.In.ExoLand.TryGetValue(l, out ex) && ex != null ? ex[g] : 0.0)); }
                    foreach (var r in rows) { double v; if (cap.TryGetValue(r.Land, out v)) cap[r.Land] = v + r.Cap; }
                    double short_ = 0, ts = 0;
                    foreach (var l in lands) { use[l] = Math.Min(cap[l], need[l]); short_ += need[l] - use[l]; ts += cap[l] - use[l]; }
                    if (short_ > 0 && ts > 0)
                    {
                        double f = Math.Min(1.0, short_ / ts);
                        foreach (var l in lands) use[l] += (cap[l] - use[l]) * f;
                    }
                    double tn = 0, tc = 0; foreach (var l in lands) { tn += need[l]; tc += cap[l]; }
                    x.R.RegInfo.Add(KsTab.Goods[g] + "/" + x.R.Markets[z] + " popyt " + (tn / 1000).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                                    + " t/d, sufit x" + (tc / Math.Max(1.0, tn)).ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                                    + ", bez zrodla " + (Math.Max(0.0, short_ - ts) / 1000).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " t/d");
                    foreach (var r in rows)
                    {
                        double cl; if (!cap.TryGetValue(r.Land, out cl) || cl <= 0) continue;
                        double ul = use[r.Land]; if (ul <= 0) continue;
                        double f = ul / cl;
                        double p0 = 0; foreach (var kv in KsTab.P0[r.C]) if (kv.Key == g) p0 = kv.Value;
                        double lab = f * r.Cap / (p0 * r.L);
                        x.RegDed[r.B] += lab; x.RegHands[r.B * KsTab.NC + r.C] += lab; x.R.RegLabour[r.C] += lab;
                        var d = x.BD[r.B];
                        double[] bd; if (!x.RegByDist.TryGetValue(d.Id, out bd)) { bd = new double[KsTab.NG]; x.RegByDist[d.Id] = bd; }
                        foreach (var kv in KsTab.P0[r.C])
                        {
                            double v = lab * kv.Value * r.L;
                            x.Sreg[d.Market * KsTab.NG + kv.Key] += v; bd[kv.Key] += v;
                        }
                    }
                }
            }
            for (int i = 0; i < x.NB; i++)
            {
                double W = x.W[i];
                x.BlockScale[i] = x.RegDed[i] > 0 && W > 0 ? Math.Max(0.0, 1.0 - x.RegDed[i] / W) : 1.0;
            }
            // podaz spoza tabeli (las wsi, lodzie rybackie) - do podazy rynku krainy (regale: odjeta juz od potrzeby wyzej)
            foreach (var kv in x.In.ExoLand)
            {
                int mk; if (kv.Value == null || !x.LandMk.TryGetValue(kv.Key, out mk)) continue;
                for (int g = 0; g < KsTab.NG; g++) x.Sreg[mk * KsTab.NG + g] += kv.Value[g];
            }
        }

        // ============================================================ 4. rownowaga reguly dochodu (logit na dochodzie na pracujacego przy cenie rynku bloku)
        private static void Shares(Ctx x, double[] p, double[] rent, double[] xs)
        {
            var inp = x.In;
            var Rm = new double[NM * KsTab.NC];
            for (int m = 0; m < NM; m++)
                for (int c = 0; c < KsTab.NC; c++)
                {
                    double s = 0; int o = c * KsTab.NG, pm = m * KsTab.NG;
                    for (int g = 0; g < KsTab.NG; g++) { double v = x.MV[o + g]; if (v != 0) s += p[pm + g] * v; }
                    Rm[m * KsTab.NC + c] = s;
                }
            double lnI = Math.Log(inp.Inert), beta = inp.Beta;
            // KS-R3 krok 5: cena krainy (gradient Grad x ciecie nadmiaru) - dodatek do dochodu zajecia na pracujacego w krainie i rynku bloku; bez gradientu (null) -
            // dochod jak dotad, bit w bit (Rm bez dodatku)
            double[] dl = x.GradOn ? LandDelta(x, p) : null;
            if (beta == 10.0)
            {
                // koszt Planu (poprawka po autotescie 16.10 - krok 8 doklada rund): exp(beta (ln u - ln umax)) = (u / umax)^10 mnozeniem, u = dochod x premia mapy^start -
                // ten sam logit bez Log/Exp na parze (wynik rowny z dokladnoscia zaokraglen)
                double inert = inp.Inert;
                for (int b = 0; b < x.NB; b++)
                {
                    int a = x.BStart[b], e = x.BStart[b + 1];
                    if (e <= a) continue;
                    double umax = 0;
                    for (int k = a; k < e; k++)
                    {
                        int c = x.Pc[k];
                        double rmc = Rm[x.Pm[k] * KsTab.NC + c];
                        if (dl != null) rmc += dl[x.Pl[k] * KsTab.NC + c];
                        double inc = x.Lp[k] * rmc - KsTab.KCost[c] - rent[b] * KsTab.HaPerWorker[c];
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
                return;
            }
            for (int b = 0; b < x.NB; b++)
            {
                int a = x.BStart[b], e = x.BStart[b + 1];
                if (e <= a) continue;
                double zmax = double.NegativeInfinity;
                for (int k = a; k < e; k++)
                {
                    int c = x.Pc[k];
                    double rmc = Rm[x.Pm[k] * KsTab.NC + c];
                    if (dl != null) rmc += dl[x.Pl[k] * KsTab.NC + c];
                    double inc = x.Lp[k] * rmc - KsTab.KCost[c] - rent[b] * KsTab.HaPerWorker[c];
                    if (inc < 0.02) inc = 0.02;
                    double z = beta * (Math.Log(inc) + x.St[k] * lnI);
                    xs[k] = z;
                    if (z > zmax) zmax = z;
                }
                double sum = 0;
                for (int k = a; k < e; k++) { double v = Math.Exp(xs[k] - zmax); xs[k] = v; sum += v; }
                for (int k = a; k < e; k++) xs[k] /= sum;
            }
        }

        private static void Supply(Ctx x, double[] xs, double[] S)
        {
            Array.Copy(x.Sreg, S, S.Length);
            for (int k = 0; k < x.NP; k++)
            {
                double w = xs[k] * x.BlockScale[x.Pb[k]];
                if (w == 0) continue;
                int mo = x.Pm[k] * KsTab.NG;
                for (int j = x.OStart[k]; j < x.OStart[k + 1]; j++) S[mo + x.OGood[j]] += w * x.OVal[j];
            }
        }

        private static double[] Equilibrium(Ctx x, double[] p0, int rounds, out double[] xs, out double[] S, out int done, Func<bool> cancel)
        {
            int n = NM * KsTab.NG;
            var p = p0 != null ? (double[])p0.Clone() : Ones(n);
            var act = new bool[n];
            for (int m = 0; m < NM; m++) for (int g = 0; g < KsTab.NG; g++) act[m * KsTab.NG + g] = !KsTab.RegGoodMask[g] && x.Dm[m * KsTab.NG + g] > 1.0;
            var rr = (double[])x.Rent.Clone();
            xs = new double[x.NP]; S = new double[n];
            var dem = new double[x.NB];
            var stall = new double[StallN];
            // recenzja 17.10 (drobne): najlepszy stan (najmniejsza suma odchylen towarow poza granica ceny) - rownowaga, ktora konczy sie na limicie rund albo stoi,
            // oddaje najlepszy stan, nie ostatni (rownowaga drgajaca zatrzymana w gorszej fazie dawala wiecej towarow niezbieznych)
            double bestMt = double.MaxValue, lastMt = double.MaxValue; double[] bp = null, bxs = null, bS = null, brr = null; int bestImp = 0;
            bool conv = false;
            int it = 0;
            for (it = 0; it < rounds; it++)
            {
                Shares(x, p, rr, xs);
                Supply(x, xs, S);
                Array.Clear(dem, 0, dem.Length);
                for (int k = 0; k < x.NP; k++) dem[x.Pb[k]] += xs[k] * x.LandP[k];
                for (int b = 0; b < x.NB; b++)
                {
                    double v = rr[b] + 0.002 * (dem[b] / x.Area[b] - 1.0);
                    rr[b] = v < 0 ? 0 : (v > 0.5 ? 0.5 : v);
                }
                for (int i = 0; i < n; i++)
                {
                    int g = i % KsTab.NG;
                    double r = KsTab.RegGoodMask[g] ? 1.0 : (x.Dm[i] + 1.0) / (S[i] + 1.0);
                    double v = p[i] * Math.Pow(r, Eta);
                    p[i] = v < PLo ? PLo : (v > PHi ? PHi : v);
                }
                for (int m = 0; m < NM; m++)
                {
                    double ws = 0, wl = 0;
                    for (int g = 0; g < KsTab.NG; g++)
                    {
                        int i = m * KsTab.NG + g;
                        if (!act[i]) continue;
                        double w = x.Dm[i] * x.Val[g];
                        ws += w; wl += w * Math.Log(p[i]);
                    }
                    if (ws > 0) { double f = Math.Exp(wl / ws); for (int g = 0; g < KsTab.NG; g++) p[m * KsTab.NG + g] /= f; }
                }
                for (int m = 0; m < NM; m++) for (int g = 0; g < KsTab.NG; g++) if (KsTab.RegGoodMask[g]) p[m * KsTab.NG + g] = 1.0;
                if (it % 50 == 49)
                {
                    bool bad = false; double mt = 0;
                    for (int i = 0; i < n; i++)
                    {
                        if (!act[i]) continue;
                        double dev = Math.Abs(S[i] / Math.Max(1.0, x.Dm[i]) - 1.0);
                        bool atb = p[i] <= PLo * 1.001 || p[i] >= PHi * 0.999;
                        if (dev > Tol && !atb) bad = true;
                        if (!atb) mt += dev;
                    }
                    if (!bad) { conv = true; break; }
                    lastMt = mt;
                    if (mt < bestMt * 0.995) bestImp = it;
                    if (mt < bestMt)
                    {
                        bestMt = mt;
                        if (bp == null) { bp = new double[n]; bxs = new double[x.NP]; bS = new double[n]; brr = new double[x.NB]; }
                        Array.Copy(p, bp, n); Array.Copy(xs, bxs, x.NP); Array.Copy(S, bS, n); Array.Copy(rr, brr, x.NB);
                    }
                    if (cancel != null && cancel()) break;
                    // koszt Planu (poprawka po autotescie 16.10): rownowaga, ktora stoi (suma odchylen towarow poza granica ceny zmienila sie przez 500 rund o mniej niz 0.5%) -
                    // koniec; dotad kazda rownowaga szla do limitu rund (towary uboczne nigdy w 2%), 25 tys. rund na Plan. Recenzja 17.10: |zmiana| - suma, ktora przez
                    // 500 rund WZROSLA, nie konczy rownowagi (dotad warunek byl prawdziwy takze przy wzroscie odchylen)
                    // Koszt (recenzja 17.10): rownowaga drgajaca albo rosnaca bez poprawy najlepszego stanu o 0.5% przez 1000 rund - koniec z najlepszym stanem
                    int slot = (it / 50) % StallN;
                    if (x.In.StallExit && it >= 50 * StallN && stall[slot] > 0 && Math.Abs(stall[slot] - mt) < 0.005 * stall[slot]) break;
                    if (x.In.StallExit && it - bestImp >= 100 * StallN) break;
                    stall[slot] = mt;
                }
            }
            done = Math.Min(rounds, it + 1);
            // koniec bez zbieznosci (stoi, limit rund albo przerwane): najlepszy stan z punktow kontrolnych, gdy ostatni jest gorszy
            if (!conv && bp != null && bestMt < lastMt)
            {
                Array.Copy(bp, p, n); Array.Copy(bxs, xs, x.NP); Array.Copy(bS, S, n); Array.Copy(brr, rr, x.NB);
            }
            x.Rent = rr;
            // stan zbieznosci do wyniku
            x.R.Open.Clear(); x.R.Bounds.Clear();
            for (int i = 0; i < n; i++)
            {
                if (!act[i]) continue;
                double dev = Math.Abs(S[i] / Math.Max(1.0, x.Dm[i]) - 1.0);
                bool atb = p[i] <= PLo * 1.001 || p[i] >= PHi * 0.999;
                string nm = x.R.Markets[i / KsTab.NG] + " " + KsTab.Goods[i % KsTab.NG];
                if (atb) x.R.Bounds.Add(nm + " " + p[i].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                else if (dev > Tol) x.R.Open.Add(nm);
            }
            Array.Clear(dem, 0, dem.Length);
            for (int k = 0; k < x.NP; k++) dem[x.Pb[k]] += xs[k] * x.LandP[k];
            int bind = 0; double rs = 0, rmax = 0;
            for (int b = 0; b < x.NB; b++) { if (dem[b] / x.Area[b] > 0.98) bind++; rs += rr[b]; if (rr[b] > rmax) rmax = rr[b]; }
            x.R.LandBind = x.NB > 0 ? (double)bind / x.NB : 0; x.R.RentMean = x.NB > 0 ? rs / x.NB : 0; x.R.RentMax = rmax;
            return p;
        }

        private static double[] Ones(int n) { var a = new double[n]; for (int i = 0; i < n; i++) a[i] = 1.0; return a; }

        private static Dictionary<string, double[]> DistrictProd(Ctx x, double[] xs)
        {
            var prod = new Dictionary<string, double[]>(StringComparer.Ordinal);
            for (int k = 0; k < x.NP; k++)
            {
                var d = x.BD[x.Pb[k]];
                double[] a; if (!prod.TryGetValue(d.Id, out a)) { a = new double[KsTab.NG]; prod[d.Id] = a; }
                double w = xs[k] * x.BlockScale[x.Pb[k]];
                for (int j = x.OStart[k]; j < x.OStart[k + 1]; j++) a[x.OGood[j]] += w * x.OVal[j];
            }
            foreach (var kv in x.RegByDist)
            {
                double[] a; if (!prod.TryGetValue(kv.Key, out a)) { a = new double[KsTab.NG]; prod[kv.Key] = a; }
                for (int g = 0; g < KsTab.NG; g++) a[g] += kv.Value[g];
            }
            return prod;
        }

        private static void Finish(Ctx x, double[] p, double[] xs, double[] S)
        {
            var R = x.R;
            R.P = p; R.S = S; R.D = (double[])x.Dm.Clone();
            R.NB = x.NB; R.NP = x.NP; R.NDist = x.D.Count;
            R.Blocks = x.B; R.PairBlock = x.Pb; R.PairCraft = x.Pc; R.X = xs; R.BlockStart = x.BStart; R.Rent = x.Rent; R.RegHands = x.RegHands;
            R.BlockW = x.W; R.BlockHome = x.WH;
            R.DistProd = DistrictProd(x, xs);
            // potrzeby po lepszym stole (R8, R8b) - przed krokiem 8 (gra liczy z nich NeedMult; to, czego nie stac, idzie osobno: DistPaid / DistHome)
            foreach (var d in x.D) R.DistNeed[d.Id] = (double[])(d.NeedR8 ?? d.Need).Clone();
            // rece wedlug zajecia (po logicie): dom, zboze wlasne, zajecia, praca domowa (krok 8)
            var sub = new double[x.NB];
            for (int k = 0; k < x.NP; k++)
            {
                int b = x.Pb[k], c = x.Pc[k];
                var d = x.BD[b]; var blk = x.B[b];
                double scale = x.BlockScale[b];
                double W = x.W[b], wH = x.WH[b], lg = KsTab.Land2(KsTab.CWheat, blk.O, d.Type);
                double rem = W - wH, sb = 0;
                if (c != KsTab.CWheat && lg > 0) sb = Math.Min(rem, KsTab.OwnGrainShare(lg) * GrainOwnNeed(d.Need0) * blk.Share / (KsTab.PGrainSub * lg));
                double hw = HomeOf(d);
                R.CraftLabour[c] += xs[k] * (rem - sb) * (1.0 - hw) * scale;
                R.HomeWorkLabour += xs[k] * (rem - sb) * hw * scale;
                R.OwnGrainLabour += xs[k] * sb * scale;
                sub[b] += xs[k] * sb;
                R.HomeLabour += xs[k] * wH * scale;
            }
            R.BlockSub = sub;
            foreach (var d in x.D) if (d.TownFields) R.TownFieldHands += d.Wf;
        }

        // ============================================================ 8. pieniadze (projekt rozdz. 9 i 11.1 krok 8; poprawka po autotescie 16.10)
        // towary psujace sie (najwyzej 7 dni polki - cena startu >= 1) i towary z cena startu 1 (domowe, uboczne, zwierzeta) - ta sama regula co polki startu w grze
        internal static readonly HashSet<string> Perish = new HashSet<string>(StringComparer.Ordinal) { "fish", "meat", "WhaleMeat", "Egg", "butter", "apple", "carrot", "orange", "grape", "date_fruit" };
        internal static readonly HashSet<string> PriceOne = new HashSet<string>(StringComparer.Ordinal) { "Egg", "honey", "carrot", "apple", "hides", "cow", "hog", "sheep", "spice", "whale_oil", "walrus_tusk" };

        /// <summary>Indeks ceny startu towaru id na rynku m (projekt rozdz. 6): cena cienia Planu w 0.7-1.5; psujace sie >= 1.0; domowe, uboczne, zwierzeta, regale
        /// i wyroby spoza towarow KS (chleb, piwo, wino ...) - 1.0.</summary>
        internal static double StartIndex(string id, double[] P, int m)
        {
            if (id == null || PriceOne.Contains(id)) return 1.0;
            int g;
            if (!KsTab.GoodIndex.TryGetValue(id == "WhaleMeat" ? "meat" : id, out g) || KsTab.RegGoodMask[g]) return 1.0;
            double p = P != null && P.Length == NM * KsTab.NG ? P[(m == 0 ? 0 : 1) * KsTab.NG + g] : 1.0;
            if (!(p > 0) || double.IsNaN(p) || double.IsInfinity(p)) p = 1.0;
            p = Math.Max(0.7, Math.Min(1.5, p));
            if (Perish.Contains(id)) p = Math.Max(1.0, p);
            return p;
        }

        /// <summary>Indeks ceny cienia towaru id na rynku m (praca - rachunek rak pracy domowej): towary KS - cena cienia Planu; regale, wyroby - 1.0.
        /// Recenzja 17.10 (drobne): towar na podlodze 0.05 (nadmiar: jaja, miod, marchew, skory, mieso z nadmiaru zywca) - jego cena cienia (praca nic prawie nie kosztuje;
        /// dotad wartosc bazowa - 20 razy wiecej, i niezaplacone jaja i miod "potrzebowaly" rak); na suficie 5 - wartosc bazowa (cena nie mowi tam o pracy).</summary>
        internal static double ShadowIndex(string id, double[] P, int m)
        {
            int g;
            if (id == null || !KsTab.GoodIndex.TryGetValue(id == "WhaleMeat" ? "meat" : id, out g) || KsTab.RegGoodMask[g]) return 1.0;
            double p = P != null && P.Length == NM * KsTab.NG ? P[(m == 0 ? 0 : 1) * KsTab.NG + g] : 1.0;
            if (!(p > 0) || double.IsNaN(p) || double.IsInfinity(p)) return 1.0;
            return p >= PHi * 0.99 ? 1.0 : p;
        }

        /// <summary>Cena kilograma towaru id w zl gry przy indeksie idx (wartosc sztuki x indeks / (sigma x kg sztuki)); brak towaru - 0.</summary>
        private static double ZlPerKg(KsInput inp, string id, double idx)
        {
            KsItem it;
            if (id == null || !inp.Items.TryGetValue(id, out it) || it == null || !(it.Kg > 0) || !(inp.Sigma > 0)) return 0.0;
            return Math.Max(0.0, it.Val) * idx / (inp.Sigma * it.Kg);
        }

        /// <summary>Test portu: slad okregu w kazdej rundzie kroku 8 (runda, okreg, typ, rynek, hw, cel, HwV, G, surowiec, V0, budzet, sprzedaz, koszyk, zaplacone); w grze null.</summary>
        internal static Action<int, string, string, int, double, double, double, double, double, double, double, double, double, double> MoneyDebug = null;

        private sealed class MDist
        {
            public KsDistrict D; public KsPart[] Parts; public double HwV, V0, G, Sur, Alpha; public bool[] Ok;
            public bool[] Cut;                                         // KS-R3: pozycje koszyka wyciete regula zasiegu (psujace sie bez zrodla w 7 dobach)
            public int Sub = -1;                                       // KS-R3 krok 8b: najtanszy wlasny towar zastepczy okregu (pozycja koszyka) albo -1
            public bool[] Glut;                                        // KS-R3 P4: towary w nadmiarze krainy okregu (potrojna waga w poprawce G '+')
        }

        /// <summary>KS-R3 krok 8b: wlasny towar zastepczy okregu za kcal pozycji spoza HomeMask - zboze (gdy okreg sieje), ryby (wies rybacka), dziczyzna, daktyle.</summary>
        private static int SubstituteOf(bool[] ok)
        {
            if (ok == null) return -1;
            if (ok[KsBasket.Grain]) return KsBasket.Grain;
            if (ok[KsBasket.Fish]) return KsBasket.Fish;
            if (ok[KsBasket.Meat]) return KsBasket.Meat;
            if (ok[KsBasket.Date]) return KsBasket.Date;
            return -1;
        }

        /// <summary>Krok 8 (recenzja 17.10, wazne): pozycje koszyka wsi, ktore okreg moze zrobic W DOMU - z towarow wlasnych zajec (blok ma zajecie: land2 > 0 albo zajecie
        /// startowe bloku; regale - zloze >= 0.5 jak w Planie i bez blokady RockGold), zboza wlasnego (land2 zboza > 0) i domu (jaja, miod); przetwor domowy z wlasnego
        /// surowca (chleb, placek i piwo z ziarna, wino, oliwa, miod pitny, plotno i sukno z przedzy). Skora, narzedzia i garnki - nigdy (rzemieslnik miasta). Ryby w glebi
        /// ladu, oliwki na Polnocy, sol bez zloza - nie: ta czesc niezaplaconego koszyka zostaje bez pokrycia (wies sie obywa - widac to w Dostatku, nic nie jest zaliczone).</summary>
        internal static bool[] HomeMask(KsDistrict d)
        {
            var z = new bool[KsBasket.N];
            if (d == null || d.Blocks == null) return z;
            var g = new bool[KsTab.NG];
            g[KsTab.GEgg] = g[KsTab.GHoney] = true;
            foreach (var b in d.Blocks)
            {
                if (b == null || b.O == null) continue;
                if (KsTab.Land2(KsTab.CWheat, b.O, d.Type) > 0) g[KsTab.GGrain] = true;
                int st; if (b.Start == null || !KsTab.CraftIndex.TryGetValue(b.Start, out st)) st = -1;
                for (int c = 0; c < KsTab.NC; c++)
                {
                    bool ok;
                    if (KsTab.IsRegale[c]) ok = (d.NoReg & (1L << c)) == 0 && ((b.Main && KsTab.Crafts[c] == d.Type) ? LandEff(b, c, d.Type) : KsTab.Land2(c, b.O, d.Type)) >= 0.5;
                    else ok = c == st || KsTab.Land2(c, b.O, d.Type) > 0;
                    if (!ok) continue;
                    foreach (var kv in KsTab.P0[c]) g[kv.Key] = true;
                }
            }
            if (g[KsTab.GCow] || g[KsTab.GHog] || g[KsTab.GSheep] || g[KsTab.GWhaleMeat]) { g[KsTab.GMeat] = true; g[KsTab.GHides] = true; }
            for (int i = 0; i < KsBasket.N; i++) { int k = KsBasket.RawGood[i]; z[i] = k >= 0 && g[k]; }
            return z;
        }

        /// <summary>
        /// KROK 8 - PIENIADZE (punkt staly, MoneyIter rund rownowagi z tlumieniem 0.5; zloto nie powstaje - liczy tylko, kto za co placi):
        ///  - WIES: produkcja okregu (KsDeliver.Produce z udzialow rak rownowagi; konie stadnin - KsDeliver.HorseOut, lancuch gry, bez pracy domowej), to, co zje sama
        ///    (KsDeliver.Own), koszyk targu (KsBasket.VillageKg); SPRZEDAZ = to, co idzie na targ (z konmi), w wartosci bazowej (indeks 1); BUDZET = alfa okregu x sprzedaz
        ///    (alfa z pomiaru w grze na tej samej podstawie: wplyw do kiesy tej wsi / wartosc bazowa produkcji wsi swiata x utarg swiata - StartCalibration.Money.cs; bez
        ///    pomiaru - VilAlpha rynku); drabina koszyka (KsBasket.Ladder) przy cenach startu; czego nie stac - PRACA DOMOWA: czesc rak zajec hw robi w domu SUROWIEC
        ///    tej czesci koszyka, ktora okreg moze zrobic sam (HomeMask - wlasne zajecia, zboze, dom); punkt staly hw = surowiec niezaplaconego koszyka (z HomeMask)
        ///    w cenach cienia / V0 (wartosc w cenach cienia rak zajec okregu przy hw = 0 - nachylenie produkcji zajec w (1 - hw), druga produkcja przy hw + 0.5), najwyzej
        ///    HomeMax; popyt Planu bez czesci niezaplaconej (cala - takze bez pokrycia), podaz bez rak w domu; czesc pokryta (hw x V0 / surowiec, najwyzej 1) x kcal
        ///    pozycji HomeMask - DistHomeKcal (gra: jedzenie robione w domu do racji spichlerza najwyzej tyle);
        ///  - MIASTO: klasy W/R/P z budzetem (zarobek netto) - drabina; czesc niezaplacona odpada z popytu (miasto nie ma pola);
        ///  - RYNEK (recenzja 17.10, wazne - bez nawijania): nadwyzka wartosci podazy nad popytem towarow rak po rownowadze (x MarketMargin na popycie targu - zapas na polki
        ///    w oknie D..D+28) - poprawka pracy domowej okregu G: nadwyzka (+) wedlug udzialu okregu w wartosci towarow w nadmiarze (rece zdejmuje ten, kto robi nadmiar),
        ///    brak (-) wedlug wartosci pracy domowej okregu (oddaje ten, kto ma prace domowa); G w [-HwV, HomeMax - HwV] - nie pcha w okregi bez pracy domowej i nie zjada
        ///    wlasnej pracy domowej ponizej 0. Zbieznosc w linii: najwieksza zmiana pracy domowej w ostatniej rundzie i nadwyzka rynku.
        /// Wynik: DistHome, DistPaid, TownPaid, DistHomeKcal, Eaten (zjedzone w okregach - bilans na targ), liczby do linii (KsMoney).
        /// </summary>
        private static void MoneyStep(Ctx x, ref double[] p, ref double[] xs, ref double[] S, Func<bool> cancel)
        {
            var inp = x.In; var R = x.R;
            var M = new KsMoney { On = true };
            R.Money = M;
            foreach (var d in x.D) d.NeedR8 = (double[])d.Need.Clone();
            foreach (var t in inp.Towns) t.NeedR8 = (double[])t.Need.Clone();
            var vs = new List<MDist>();
            var al = new List<double>[NM]; for (int m = 0; m < NM; m++) al[m] = new List<double>();
            if (inp.Keep != null)
                foreach (var d in x.D)
                {
                    if (d.TownFields || d.PplHome == null || d.Bask == null || d.Blocks.Count == 0) continue;
                    var parts = new KsPart[d.Blocks.Count];
                    for (int i = 0; i < parts.Length; i++) parts[i] = new KsPart { K = d.Blocks[i] };
                    int m0 = d.Market == 0 ? 0 : 1;
                    bool meas = d.Alpha >= 0 && !double.IsNaN(d.Alpha) && !double.IsInfinity(d.Alpha);
                    double a0 = Math.Max(0.0, Math.Min(0.95, meas ? d.Alpha : inp.VilAlpha[m0]));
                    if (meas) M.AlphaN[m0]++;
                    al[m0].Add(a0);
                    var md = new MDist { D = d, Parts = parts, HwV = HomeOf(d), Ok = HomeMask(d), Alpha = a0 };
                    if (inp.HomeSub) md.Sub = SubstituteOf(md.Ok);
                    if (x.ReachD != null) x.ReachD.TryGetValue(d.Id, out md.Cut);
                    if (inp.LandGlut != null && d.Land != null) inp.LandGlut.TryGetValue(d.Land, out md.Glut);
                    vs.Add(md);
                }
            for (int m = 0; m < NM; m++)
            {
                var l = al[m]; if (l.Count == 0) continue;
                l.Sort(); M.AlphaMin[m] = l[0]; M.AlphaMed[m] = l[l.Count / 2]; M.AlphaMax[m] = l[l.Count - 1];
            }
            bool towns = false; foreach (var t in inp.Towns) if (t.Budget != null && t.ClassNeed != null) { towns = true; break; }
            if (vs.Count == 0 && !towns) { M.On = false; return; }
            int NZ = KsBasket.N, NI = KsDeliver.ItemCount, NGm = NM * KsTab.NG;
            var view = new KsResult();
            var prod = new double[NI]; var prod2 = new double[NI]; var own = new double[NI]; var own2 = new double[NI];
            var q = new double[NZ]; var paid = new double[NZ]; var u = new double[NZ];
            var keep = new double[KsTab.NN]; var full = new double[KsTab.NN];
            var pz = new double[NM * NZ]; var pi = new double[NM * NI]; var bi = new double[NM * NI];
            var sz = new double[NM * NZ]; var si = new double[NM * NI];   // ceny cienia (praca) - rachunek pracy domowej
            var pH = new double[NM]; var sH = new double[NM];
            double bH = KsTab.HorseVal / Math.Max(1.0, inp.Sigma);         // kon (jednostka Planu = sztuka gry / sigma) w zl gry przy wartosci bazowej
            var sur = new bool[NGm];                                       // towar rynku w nadmiarze po ostatniej rownowadze (poprawka G w gore)
            var eat = new double[NGm]; var eatPrev = new double[NGm];      // zjedzone w okregach [m * NG + towar] (bilans na targ; popyt targu marginesu)
            double margin = inp.MarketMargin > 1.0 && inp.MarketMargin <= 1.10 ? inp.MarketMargin : 1.0;
            M.Margin = margin;
            // KS-R3 P4: czynnik popytu targu na towar (sterowanie rynku po D+14, 0.85-1.10); bez niego - margines dla wszystkich (jak dotad)
            var mfac = new double[NGm];
            for (int i = 0; i < NGm; i++)
            {
                double f = inp.MarketFactor != null && i < inp.MarketFactor.Length && inp.MarketFactor[i] > 0 && !double.IsNaN(inp.MarketFactor[i]) ? Math.Max(0.85, Math.Min(1.10, inp.MarketFactor[i])) : margin;
                mfac[i] = f;
                int m = i / KsTab.NG;
                if (x.Dm[i] > 1.0 && !KsTab.RegGoodMask[i % KsTab.NG]) { if (f < M.MfMin[m]) M.MfMin[m] = f; if (f > M.MfMax[m]) M.MfMax[m] = f; }
            }
            R.MarketFactor = mfac;
            var qq = new double[NZ];                                       // KS-R3: koszyk bez pozycji wycietych regula zasiegu (drabina zastepuje)
            if (x.ReachD != null) foreach (var v in vs) if (v.Cut != null) M.ReachCut[v.D.Market == 0 ? 0 : 1]++;
            var sumSur = new double[NM]; var sumHv = new double[NM]; var tot0 = new double[NM * 3];
            var tq = new double[3][]; for (int c = 0; c < 3; c++) tq[c] = new double[NZ];
            var tqs = new double[NZ]; var tus = new double[NZ];
            int[] live = { KsDeliver.ICow, KsDeliver.IHog, KsDeliver.ISheep }; int[] liveG = { KsTab.GCow, KsTab.GHog, KsTab.GSheep };
            for (int it = 0; it <= inp.MoneyIter; it++)
            {
                bool last = it == inp.MoneyIter;
                for (int m = 0; m < NM; m++)
                {
                    for (int z = 0; z < NZ; z++) pz[m * NZ + z] = ZlPerKg(inp, KsBasket.Ids[z], StartIndex(KsBasket.Ids[z], p, m));
                    for (int i = 0; i < NI; i++) { pi[m * NI + i] = ZlPerKg(inp, KsDeliver.ItemIds[i], StartIndex(KsDeliver.ItemIds[i], p, m)); bi[m * NI + i] = ZlPerKg(inp, KsDeliver.ItemIds[i], 1.0); }
                    for (int z = 0; z < NZ; z++) sz[m * NZ + z] = ZlPerKg(inp, KsBasket.Ids[z], ShadowIndex(KsBasket.Ids[z], p, m));
                    for (int i = 0; i < NI; i++) si[m * NI + i] = ZlPerKg(inp, KsDeliver.ItemIds[i], ShadowIndex(KsDeliver.ItemIds[i], p, m));
                    // zywiec w cenach cienia: mieso i skory (jak MV Planu) - zwierze samo nie ma popytu (cena cienia na podlodze nic nie mowi)
                    for (int k = 0; k < 3; k++) si[m * NI + live[k]] = KsTab.MeatOfLive * si[m * NI + KsDeliver.IMeat] + KsTab.HideOfLive(liveG[k]) * si[m * NI + KsDeliver.IHides];
                    pH[m] = bH * StartIndex("horse", p, m); sH[m] = bH * ShadowIndex("horse", p, m);
                }
                double maxDG = 0;
                if (it > 0)
                {
                    // RYNEK: nadwyzka podazy nad popytem towarow rak (bez regale, ubocznych i koni) z ostatniej rownowagi w cenach cienia; popyt targu (popyt - zjedzone
                    // w okregach) x margines; towary w nadmiarze - do udzialu okregu w poprawce w gore
                    for (int m = 0; m < NM; m++)
                    {
                        double e = 0, dm = 0;
                        for (int k = 0; k < KsTab.NG; k++)
                        {
                            int i = m * KsTab.NG + k;
                            sur[i] = false;
                            if (KsTab.RegGoodMask[k] || k == KsTab.GHorse || ExcessSkip(k)) continue;
                            if (!(x.Dm[i] > 1.0)) continue;
                            double pk = ZlPerKg(inp, KsTab.Goods[k], ShadowIndex(KsTab.Goods[k], p, m));
                            double dmk = Math.Max(0.0, x.Dm[i] - eatPrev[i]), dk = x.Dm[i] + (mfac[i] - 1.0) * dmk;
                            e += (S[i] - dk) * pk; dm += dmk * pk;
                            sur[i] = S[i] > dk;
                        }
                        M.Excess[m] = e; M.ExcessRel[m] = dm > 0 ? e / dm : 0.0;
                    }
                    // poprawka okregu G (bez nawijania): + wedlug udzialu w towarach w nadmiarze, - wedlug wartosci pracy domowej; G w [-HwV, HomeMax - HwV]
                    Array.Clear(sumSur, 0, NM); Array.Clear(sumHv, 0, NM);
                    foreach (var v in vs) { int m = v.D.Market == 0 ? 0 : 1; sumSur[m] += v.Sur; sumHv[m] += HomeOf(v.D) * v.V0; }
                    for (int m = 0; m < NM; m++) M.HwLeft[m] = sumHv[m];
                    var gw = new double[NM]; var ww = new double[NM];
                    foreach (var v in vs)
                    {
                        int m = v.D.Market == 0 ? 0 : 1;
                        double e = M.Excess[m], dG = 0;
                        if (e > 0 && sumSur[m] > 0 && v.V0 > 0) dG = 0.5 * e * (v.Sur / sumSur[m]) / v.V0;
                        else if (e < 0 && sumHv[m] > 0) dG = 0.5 * e * HomeOf(v.D) / sumHv[m];
                        double g0 = v.G;
                        v.G = Math.Max(-v.HwV, Math.Min(inp.HomeMax - v.HwV, v.G + dG));
                        if (Math.Abs(v.G - g0) > maxDG) maxDG = Math.Abs(v.G - g0);
                        gw[m] += v.G * v.D.Wf; ww[m] += v.D.Wf;
                    }
                    for (int m = 0; m < NM; m++) M.Global[m] = ww[m] > 0 ? gw[m] / ww[m] : 0.0;
                    M.DG = maxDG;
                }
                Array.Clear(M.VSales, 0, NM); Array.Clear(M.VSalesB, 0, NM); Array.Clear(M.VBudget, 0, NM); Array.Clear(M.VBasket, 0, NM); Array.Clear(M.VPaid, 0, NM); Array.Clear(M.VHome, 0, NM);
                Array.Clear(M.VHands, 0, NM); Array.Clear(M.VHomeHands, 0, NM); Array.Clear(M.VShort, 0, NM); Array.Clear(M.VN, 0, NM); Array.Clear(M.VHorse, 0, NM);
                Array.Clear(M.TBudget, 0, M.TBudget.Length); Array.Clear(M.TBasket, 0, M.TBasket.Length); Array.Clear(M.TPaid, 0, M.TPaid.Length); Array.Clear(M.TUnRaw, 0, NM);
                Array.Clear(M.UnpaidV, 0, M.UnpaidV.Length); Array.Clear(M.UnpaidT, 0, M.UnpaidT.Length); Array.Clear(M.VUnmetRaw, 0, NM);
                Array.Clear(eat, 0, NGm);
                view.BlockStart = x.BStart; view.X = xs; view.PairCraft = x.Pc; view.BlockW = x.W; view.RegHands = x.RegHands; view.Rent = x.Rent;
                // ---- wsie
                foreach (var v in vs)
                {
                    var d = v.D; int m = d.Market == 0 ? 0 : 1;
                    for (int j = 0; j < v.Parts.Length; j++) KsDeliver.FromPlan(view, d.Blocks[j].Index, v.Parts[j]);
                    KsDeliver.Produce(d, v.Parts, prod);
                    double hk = KsDeliver.HorseOut(d, v.Parts);
                    double made = hk * sH[m];                              // przed Own (Own dopisuje skory z uboju na wlasny stol)
                    for (int i = 0; i < NI; i++) made += prod[i] * si[m * NI + i];
                    double home = 0; foreach (var e in d.PplHome) home += Math.Max(0.0, e);
                    double hr = d.People > 0 ? Math.Min(1.0, home / d.People) : 0.0;
                    NeedsOfKeep(d.PplHome, d.Bask, inp.Keep, d.Oil, home, keep);
                    for (int k = 0; k < KsTab.NN; k++)
                    {
                        double mult = d.Need0[k] > 1e-9 ? Math.Max(0.25, Math.Min(4.0, d.NeedR8[k] / d.Need0[k])) : 1.0;
                        keep[k] *= mult; full[k] = d.NeedR8[k] * hr;
                    }
                    KsDeliver.Own(prod, keep, own);
                    for (int i = 0; i < NI; i++)
                    {
                        int gk = KsDeliver.ItemGood[i];
                        if (gk < 0 || !(own[i] > 0)) continue;
                        eat[m * KsTab.NG + gk] += own[i] * (i == KsDeliver.ICow || i == KsDeliver.IHog || i == KsDeliver.ISheep ? KsTab.MeatOfLive : 1.0);
                    }
                    Array.Clear(q, 0, NZ);
                    KsBasket.VillageKg(full, keep, own, q);
                    // sprzedaz: na targ (z konmi stadnin - recenzja 17.10, krytyczne) przy cenach startu (linia) i w wartosci bazowej (budzet - ta sama podstawa co pomiar alfy)
                    double sales = hk * pH[m], salesB = hk * bH;
                    for (int i = 0; i < NI; i++) { double mk = prod[i] - own[i]; if (mk > 0) { sales += mk * pi[m * NI + i]; salesB += mk * bi[m * NI + i]; } }
                    double bask = 0; for (int z = 0; z < NZ; z++) bask += q[z] * pz[m * NZ + z];
                    double budget = v.Alpha * salesB;
                    double pv;
                    if (v.Cut != null)
                    {
                        // KS-R3 regula zasiegu: psujacego sie towaru nikt nie dowiezie - pozycja poza drabina (budzet idzie na reszte), czesc zaplacona 0
                        Array.Copy(q, qq, NZ);
                        for (int z = 0; z < NZ && z < v.Cut.Length; z++) if (v.Cut[z]) qq[z] = 0.0;
                        pv = KsBasket.Ladder(qq, pz, m * NZ, budget, paid);
                        for (int z = 0; z < NZ && z < v.Cut.Length; z++) if (v.Cut[z]) paid[z] = 0.0;
                    }
                    else pv = KsBasket.Ladder(q, pz, m * NZ, budget, paid);
                    double hwc = HomeOf(d);
                    // punkt staly pracy domowej: reka w domu robi tyle surowca, ile robilaby w zajeciu - V0 (wartosc rak zajec przy hw = 0, w cenach cienia; konie bez zmian -
                    // lancuch gry); hw x V0 = surowiec niezaplaconego koszyka z HomeMask (wyroby - ich wsad) przy biezacym hw; nachylenie < 1 - zbiega
                    double hw2 = Math.Min(0.95, hwc + 0.5);
                    d.HomeWork = hw2;
                    KsDeliver.Produce(d, v.Parts, prod2);
                    d.HomeWork = hwc;
                    double made2 = hk * sH[m], svs = 0;
                    for (int i = 0; i < NI; i++)
                    {
                        double vi = prod2[i] * si[m * NI + i];
                        made2 += vi;
                        int gk = KsDeliver.ItemGood[i];
                        // KS-R3 P4 (regula nadmiaru krainy): towar, ktorego polki krainy okregu maja ponad 2 x cel i rosna - potrojna waga w poprawce G '+'
                        if (gk >= 0 && sur[m * KsTab.NG + gk]) svs += (prod[i] * si[m * NI + i] - vi) * (v.Glut != null && gk < v.Glut.Length && v.Glut[gk] ? 3.0 : 1.0);
                    }
                    double v0 = hw2 > hwc ? Math.Max(0.0, made - made2) / (hw2 - hwc) : made, raw = 0, rawNo = 0, subK = 0;
                    // udzial w nadmiarze (poprawka G w gore): wartosc towarow w nadmiarze w czesci rak zajec, ktora praca domowa zdejmuje (nie dom, nie zboze wlasne)
                    v.Sur = hw2 > hwc ? Math.Max(0.0, svs) / (hw2 - hwc) : 0.0;
                    for (int z = 0; z < NZ; z++)
                    {
                        u[z] = (1.0 - paid[z]) * q[z];
                        if (!(u[z] > 0)) continue;
                        double r = u[z] * RawZ(sz, m * NZ, z);
                        if (v.Ok[z]) raw += r;
                        else if (v.Sub >= 0 && KsBasket.Kcal[z] > 0 && KsBasket.Kcal[v.Sub] > 0)
                        {
                            // KS-R3 krok 8b: kcal pozycji, ktorej okreg sam nie zrobi, robi praca domowa jako najtanszy wlasny towar okregu (kcal za kcal)
                            double kz = u[z] * KsBasket.Kcal[z];
                            raw += kz / KsBasket.Kcal[v.Sub] * RawZ(sz, m * NZ, v.Sub);
                            subK += kz;
                        }
                        else rawNo += r;
                    }
                    v.V0 = v0;
                    double target = v0 > 0 ? Math.Max(0.0, Math.Min(inp.HomeMax, raw / v0)) : 0.0;
                    if (MoneyDebug != null) MoneyDebug(it, d.Id, d.Type, m, hwc, target, v.HwV, v.G, raw, v0, budget, salesB, bask, pv);
                    if (!last) v.HwV += 0.5 * (target - v.HwV);
                    M.VSales[m] += sales; M.VSalesB[m] += salesB; M.VHorse[m] += hk * bH; M.VBudget[m] += budget; M.VBasket[m] += bask; M.VPaid[m] += pv; M.VHome[m] += Math.Max(0.0, bask - pv); M.VN[m]++;
                    if (bask > budget * 1.001 + 1e-6) M.VShort[m]++;
                    M.VHands[m] += d.Wf; M.VHomeHands[m] += d.Wf * hwc; M.VUnmetRaw[m] += rawNo;
                    for (int z = 0; z < NZ; z++) M.UnpaidV[m * NZ + z] += u[z];
                    if (last)
                    {
                        var fp = new float[NZ]; for (int z = 0; z < NZ; z++) fp[z] = (float)paid[z];
                        R.DistPaid[d.Id] = fp; R.DistHome[d.Id] = hwc;
                        // czesc pokryta praca domowa (recenzja 17.10, wazne): hw x V0 / surowiec pozycji HomeMask, najwyzej 1 - kcal robione w domu najwyzej tyle (gra: racje)
                        double cov = raw > 0 ? Math.Min(1.0, hwc * v0 / raw) : 0.0, hkc = 0, fk = 0, uk = 0;
                        for (int z = 0; z < NZ; z++)
                        {
                            double kc = KsBasket.Kcal[z]; if (!(kc > 0)) continue;
                            fk += q[z] * kc; uk += u[z] * kc;
                            if (v.Ok[z]) hkc += u[z] * kc;
                        }
                        hkc = cov * (hkc + subK);                                 // KS-R3 krok 8b: kcal zastepcze tez robione w domu (ta sama czesc pokryta)
                        R.DistHomeKcal[d.Id] = fk > 0 ? (float)Math.Max(0.0, Math.Min(1.0, hkc / fk)) : 0f;
                        M.VFullKcal[m] += fk; M.VUnpaidKcal[m] += uk; M.VHomeKcal[m] += hkc; M.VSubKcal[m] += cov * subK;
                        // sprzedaz bez pracy domowej (te same zajecia) - ile krok 8 zdejmuje z targu (linia; podatek panow od utargu wozow idzie za sprzedaza)
                        d.HomeWork = 0.0;
                        KsDeliver.Produce(d, v.Parts, prod2);
                        d.HomeWork = hwc;
                        KsDeliver.Own(prod2, keep, own2);
                        double s0 = hk * bH; for (int i = 0; i < NI; i++) { double mk = prod2[i] - own2[i]; if (mk > 0) s0 += mk * bi[m * NI + i]; }
                        M.VSalesNoHw[m] += s0;
                    }
                    else
                    {
                        Array.Copy(d.NeedR8, d.Need, KsTab.NN);
                        UnpaidToNeed(d.Need, u, false);
                    }
                }
                Array.Copy(eat, eatPrev, NGm);
                // zbieznosc w sumach rynku (recenzja 17.10): sprzedaz, zaplacone i rece w pracy domowej wobec poprzedniej rundy (pojedyncze okregi przechodza miedzy zajeciami
                // rownowagi - ich praca domowa idzie za rynkiem; liczy sie suma)
                for (int m = 0; m < NM; m++)
                {
                    double[] cur = { M.VSalesB[m], M.VPaid[m], M.VHomeHands[m] };
                    double dmax = 0;
                    for (int k = 0; k < 3; k++)
                    {
                        // rece w domu - zmiana jako czesc wszystkich rak zajec x 2 (prog 0.5% rak = 1%); sprzedaz i zaplacone - wzgledem siebie
                        if (it > 0) { double den = k == 2 ? 0.5 * Math.Max(1.0, M.VHands[m]) : Math.Max(1.0, Math.Abs(tot0[m * 3 + k])); dmax = Math.Max(dmax, Math.Abs(cur[k] - tot0[m * 3 + k]) / den); }
                        tot0[m * 3 + k] = cur[k];
                    }
                    M.DTot[m] = it > 0 ? dmax : -1;
                }
                // ---- miasta (klasy W/R/P)
                foreach (var t in inp.Towns)
                {
                    if (t.Budget == null || t.ClassNeed == null || t.Budget.Length < 3 || t.ClassNeed.Length < 3)
                    {
                        // KS-R3 regula zasiegu w miescie bez budzetu klas (placi cale): wyciete psujace sie i tak odpadaja z popytu
                        bool[] c0 = null;
                        if (x.ReachT == null || !x.ReachT.TryGetValue(t.Id, out c0) || c0 == null || t.NeedR8 == null) continue;
                        Array.Clear(tqs, 0, NZ); Array.Clear(tus, 0, NZ);
                        double fk0; KsBasket.TownKg(t.NeedR8, 0.0, tqs, out fk0);
                        for (int z = 0; z < NZ && z < c0.Length; z++) if (c0[z]) tus[z] = tqs[z];
                        if (last) { var fp0 = new float[NZ]; for (int z = 0; z < NZ; z++) fp0[z] = z < c0.Length && c0[z] ? 0f : 1f; R.TownPaid[t.Id] = fp0; }
                        else { Array.Copy(t.NeedR8, t.Need, KsTab.NN); UnpaidToNeed(t.Need, tus, true); }
                        continue;
                    }
                    int m = t.Market == 0 ? 0 : 1;
                    Array.Clear(tqs, 0, NZ); Array.Clear(tus, 0, NZ);
                    bool[] tcut = null; if (x.ReachT != null) x.ReachT.TryGetValue(t.Id, out tcut);
                    for (int c = 0; c < 3; c++)
                    {
                        var qc = tq[c]; Array.Clear(qc, 0, NZ);
                        if (t.ClassNeed[c] == null) continue;
                        double fk; KsBasket.TownKg(t.ClassNeed[c], 0.0, qc, out fk);
                        double cost = 0; for (int z = 0; z < NZ; z++) cost += qc[z] * pz[m * NZ + z];
                        double pv;
                        if (tcut != null)
                        {
                            // KS-R3 regula zasiegu: psujace sie bez zrodla w 7 dobach - poza drabina miasta (budzet na reszte), czesc zaplacona 0 (odpada z popytu)
                            Array.Copy(qc, qq, NZ);
                            for (int z = 0; z < NZ && z < tcut.Length; z++) if (tcut[z]) qq[z] = 0.0;
                            pv = KsBasket.Ladder(qq, pz, m * NZ, Math.Max(0.0, t.Budget[c]), paid);
                            for (int z = 0; z < NZ && z < tcut.Length; z++) if (tcut[z]) paid[z] = 0.0;
                        }
                        else pv = KsBasket.Ladder(qc, pz, m * NZ, Math.Max(0.0, t.Budget[c]), paid);
                        M.TBudget[m * 3 + c] += Math.Max(0.0, t.Budget[c]); M.TBasket[m * 3 + c] += cost; M.TPaid[m * 3 + c] += pv;
                        for (int z = 0; z < NZ; z++) { tqs[z] += qc[z]; tus[z] += (1.0 - paid[z]) * qc[z]; }
                    }
                    for (int z = 0; z < NZ; z++) { M.UnpaidT[m * NZ + z] += tus[z]; M.TUnRaw[m] += tus[z] * RawZ(sz, m * NZ, z); }
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
                // praca domowa na nastepna rownowage: wlasna (koszyk ponad budzet, HomeMask) + poprawka rynku okregu
                double dh = 0;
                Array.Clear(M.DHwM, 0, NM); Array.Clear(M.DHwN, 0, NM);
                foreach (var v in vs)
                {
                    int m = v.D.Market == 0 ? 0 : 1;
                    double hn = Math.Max(0.0, Math.Min(inp.HomeMax, v.HwV + v.G));
                    double dd = Math.Abs(hn - HomeOf(v.D)); if (dd > dh) dh = dd;
                    if (dd > M.DHwM[m]) M.DHwM[m] = dd;
                    if (dd >= 0.05) M.DHwN[m]++;
                    v.D.HomeWork = hn;
                }
                M.DHw = dh;
                M.Trace.Add("runda " + (it + 1) + ": wsie W/E sprzedaz " + Math.Round(M.VSalesB[0]) + "/" + Math.Round(M.VSalesB[1]) + ", koszyk " + Math.Round(M.VBasket[0]) + "/" + Math.Round(M.VBasket[1])
                            + ", zaplacone " + Math.Round(M.VPaid[0]) + "/" + Math.Round(M.VPaid[1]) + ", nadwyzka rynku " + Math.Round(M.Excess[0]) + "/" + Math.Round(M.Excess[1])
                            + ", poprawka rynku G (srednio) " + M.Global[0].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "/" + M.Global[1].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                            + ", najwieksza zmiana pracy domowej " + dh.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) + " (G " + maxDG.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) + ")");
                Pairs(x);
                Demand(x);
                RegaleStep(x);
                int rounds;
                p = Equilibrium(x, p, inp.RoundsR8b, out xs, out S, out rounds, cancel);
                R.Rounds += rounds; M.Iter++;
                if (cancel != null && cancel()) return;
            }
            R.Eaten = (double[])eat.Clone();
            // zbieznosc punktu stalego (recenzja 17.10): sumy rynku stoja (sprzedaz, zaplacone, rece w domu - zmiana < 1% w ostatniej rundzie) i rynek bez nadwyzki
            // (< 1% popytu targu) albo brak rak do oddania (praca domowa rynku nie pokryje braku)
            for (int m = 0; m < NM; m++)
            {
                double rel = M.ExcessRel[m];
                bool stuck = rel < 0 && M.HwLeft[m] < 0.05 * Math.Abs(M.Excess[m]);      // praca domowa rynku (wartosc) mniejsza niz 5% braku - nie ma czego oddac
                M.Conv[m] = M.DTot[m] >= 0 && M.DTot[m] < 0.01 && (Math.Abs(rel) < 0.01 || stuck);
                M.NoHands[m] = stuck;
            }
        }

        /// <summary>Cena surowca w kg (l) pozycji koszyka z przy cenach startu (zl gry): wyrob - jego wsad (chleb i placek 1 kg ziarna, piwo 0.4 kg ziarna, wino 1.3 kg winogron,
        /// oliwa 5 kg oliwek, miod pitny 0.35 kg miodu, plotno 1.25 kg lnu, sukno 1.45 kg welny, skora 2.4 kg skor, narzedzia 1.2 kg zelaza, garnki 1.2 kg gliny); surowiec - on sam.</summary>
        private static double RawZ(double[] pz, int o, int z)
        {
            switch (z)
            {
                case KsBasket.Bread: case KsBasket.Pie: return pz[o + KsBasket.Grain];
                case KsBasket.Beer: return KsTab.BeerGrain * pz[o + KsBasket.Grain];
                case KsBasket.Wine: return KsTab.WineGrape * pz[o + KsBasket.Grape];
                case KsBasket.Oil: return KsTab.OilOlive * pz[o + KsBasket.Olives];
                case KsBasket.Mead: return KsTab.MeadHoney * pz[o + KsBasket.Honey];
                case KsBasket.Linen: return KsTab.LinenFlax * pz[o + KsBasket.Flax];
                case KsBasket.Felt: return KsTab.FeltWool * pz[o + KsBasket.Wool];
                case KsBasket.Leather: return KsTab.LeatherHide * pz[o + KsBasket.Hides];
                case KsBasket.Tools: return KsTab.ToolsIron * pz[o + KsBasket.Iron];
                case KsBasket.Pottery: return KsTab.PotClay * pz[o + KsBasket.Clay];
                default: return pz[o + z];
            }
        }

        /// <summary>Towary poza nadwyzka wartosci rynku (krok 8): uboczne i domowe (wyjatki pasma projektu) - ich nadmiar nie jest praca do zdjecia.</summary>
        private static bool ExcessSkip(int g)
        {
            return g == KsTab.GCarrot || g == KsTab.GApple || g == KsTab.GFur || g == KsTab.GHoney || g == KsTab.GSpice || g == KsTab.GWhaleOil || g == KsTab.GTusk || g == KsTab.GEgg || g == KsTab.GHides;
        }

        private static void Cut(double[] n, int k, double v) { if (v > 0) n[k] = Math.Max(0.0, n[k] - v); }
        private static double CutR(double[] n, int k, double v) { if (!(v > 0)) return 0.0; double t = Math.Min(n[k], v); n[k] -= t; return v - t; }

        /// <summary>Niezaplacony koszyk targu (kg/d wedlug KsBasket.Ids) -> potrzeby osady bez tej czesci (wies: towar robiony w domu; miasto: odpada). Wino, oliwa, miod pitny,
        /// piwo, przedza domowa - reszta ponad swiezy towar z przetworu (te same proporcje co popyt Planu).</summary>
        internal static void UnpaidToNeed(double[] n, double[] u, bool town)
        {
            double r = CutR(n, NG_X, u[KsBasket.Grain] + (town ? u[KsBasket.Bread] + u[KsBasket.Pie] : 0.0));
            if (r > 0) Cut(n, KsTab.NBeer, r / KsTab.BeerGrain);
            Cut(n, KsTab.NMeat, u[KsBasket.Meat] + u[KsBasket.Whale]);
            Cut(n, KsTab.NFish, u[KsBasket.Fish]); Cut(n, KsTab.NCheese, u[KsBasket.Cheese]); Cut(n, KsTab.NButter, u[KsBasket.Butter]); Cut(n, KsTab.NEgg, u[KsBasket.Egg]);
            Cut(n, KsTab.NApple, u[KsBasket.Apple]); Cut(n, KsTab.NCarrot, u[KsBasket.Carrot]); Cut(n, KsTab.NOrange, u[KsBasket.Orange]); Cut(n, KsTab.NDate, u[KsBasket.Date]);
            Cut(n, KsTab.NBeer, u[KsBasket.Beer]); Cut(n, KsTab.NWine, u[KsBasket.Wine]); Cut(n, KsTab.NMead, u[KsBasket.Mead]);
            r = CutR(n, KsTab.NGrape, u[KsBasket.Grape]); if (r > 0) Cut(n, KsTab.NWine, r / KsTab.WineGrape);
            r = CutR(n, KsTab.NOlives, u[KsBasket.Olives]); if (r > 0) Cut(n, KsTab.NOil, r / KsTab.OilOlive);
            r = CutR(n, KsTab.NHoney, u[KsBasket.Honey]); if (r > 0) Cut(n, KsTab.NMead, r / KsTab.MeadHoney);
            r = CutR(n, KsTab.NOil, u[KsBasket.Oil]); if (r > 0) Cut(n, KsTab.NSwOlej, r);
            Cut(n, KsTab.NSalt, u[KsBasket.Salt]); Cut(n, KsTab.NSpice, u[KsBasket.Spice]);
            Cut(n, KsTab.NPlotno, u[KsBasket.Linen] + u[KsBasket.Flax] / KsTab.LinenFlax); Cut(n, KsTab.NSukno, u[KsBasket.Felt] + u[KsBasket.Wool] / KsTab.FeltWool);
            Cut(n, KsTab.NSkora, u[KsBasket.Leather]); Cut(n, KsTab.NNarz, u[KsBasket.Tools]); Cut(n, KsTab.NGarnki, u[KsBasket.Pottery]);
            Cut(n, KsTab.NSwTran, u[KsBasket.WhaleOil]); Cut(n, KsTab.NFutro, u[KsBasket.Fur]);
        }
        private const int NG_X = KsTab.NG_;

        // ============================================================ potrzeby osady (needs_of modelu) i rece (workers_of)
        /// <summary>Koszyk stanu: 13 grup (G, H, M, F, D, V, O, S, Hn, Sp, beer, wine, mead) + podzial owocow (6) + daktyle ponad koszyk (KS-K).</summary>
        internal sealed class Basket { public double[] G = new double[13]; public double[] Vs = new double[6]; public double DatesExtra; }

        /// <summary>Potrzeby osady (kg/d) z ludzi stanow (6) i koszykow stanow kultury: needs_of modelu. oilProf - kraina pije oliwe (OILPROF), northFur - KS-K kozuchy
        /// (polnoc, wyspy), town - miasto (opal 1.5 kg na glowe).</summary>
        internal static void NeedsOf(double[] ppl, Basket[] bask, bool oilProf, bool northFur, bool ksk, bool town, double people, double[] n)
        {
            Array.Clear(n, 0, n.Length);
            if (!(people > 0)) return;
            for (int e = 0; e < KsTab.Est; e++)
            {
                double p = ppl[e];
                if (!(p > 0)) continue;
                var b = bask[e]; if (b == null) continue;
                n[KsTab.NG_] += p * b.G[0]; n[KsTab.NH] += p * b.G[1]; n[KsTab.NMeat] += p * b.G[2]; n[KsTab.NFish] += p * b.G[3];
                n[KsTab.NCheese] += p * b.G[4] * 0.5; n[KsTab.NButter] += p * b.G[4] * 0.25; n[KsTab.NEgg] += p * b.G[4] * 0.25;
                double ve = b.DatesExtra;
                for (int i = 0; i < 6; i++) n[KsTab.FruitNeed[i]] += p * (b.G[5] - ve) * b.Vs[i];
                n[KsTab.NDate] += p * ve;
                if (oilProf) n[KsTab.NOil] += p * b.G[6]; else n[KsTab.NMeat] += p * b.G[6];
                n[KsTab.NSalt] += p * b.G[7]; n[KsTab.NHoney] += p * b.G[8]; n[KsTab.NSpice] += p * b.G[9];
                n[KsTab.NBeer] += p * b.G[10]; n[KsTab.NWine] += p * b.G[11]; n[KsTab.NMead] += p * b.G[12];
                var nf = KsTab.NonFood[e];
                for (int j = 0; j < 9; j++) n[KsTab.NfNeed[j]] += p * nf[j] / 364.0;
                if (ksk && northFur) n[KsTab.NFutro] += p * Math.Max(0.3, nf[6] * 3) / 364.0;
                if (oilProf) n[KsTab.NSwOlej] += p * nf[5] / 364.0 * 0.6; else n[KsTab.NSwTran] += p * nf[5] / 364.0 * 0.3;
            }
            if (town) n[KsTab.NDrewno] += people * KsTab.FirewoodTown;
        }

        /// <summary>To, co okreg je z wlasnego pola i domu (kg/d): potrzeby stanow x keep[stan][grupa koszyka] (1 - udzial z targu ZC); dom (H) zawsze z domu;
        /// plotno i sukno - polowa przedziona w domu (len, welna). Bez opalu, swiatla i reszty rzeczy (z targu).</summary>
        internal static void NeedsOfKeep(double[] ppl, Basket[] bask, double[][] keep, bool oilProf, double people, double[] n)
        {
            Array.Clear(n, 0, n.Length);
            if (!(people > 0)) return;
            for (int e = 0; e < KsTab.Est; e++)
            {
                double p = ppl[e];
                if (!(p > 0)) continue;
                var b = bask[e]; var k = keep[e]; if (b == null || k == null) continue;
                n[KsTab.NG_] += p * b.G[0] * k[0]; n[KsTab.NH] += p * b.G[1]; n[KsTab.NMeat] += p * b.G[2] * k[2]; n[KsTab.NFish] += p * b.G[3] * k[3];
                n[KsTab.NCheese] += p * b.G[4] * 0.5 * k[4]; n[KsTab.NButter] += p * b.G[4] * 0.25 * k[4]; n[KsTab.NEgg] += p * b.G[4] * 0.25 * k[4];
                double ve = b.DatesExtra;
                for (int i = 0; i < 6; i++) n[KsTab.FruitNeed[i]] += p * (b.G[5] - ve) * b.Vs[i] * k[5];
                n[KsTab.NDate] += p * ve * k[5];
                if (oilProf) n[KsTab.NOil] += p * b.G[6] * k[6]; else n[KsTab.NMeat] += p * b.G[6] * k[6];
                n[KsTab.NSalt] += p * b.G[7] * k[7]; n[KsTab.NHoney] += p * b.G[8] * k[8]; n[KsTab.NSpice] += p * b.G[9] * k[9];
                n[KsTab.NBeer] += p * b.G[10] * k[10]; n[KsTab.NWine] += p * b.G[11] * k[11]; n[KsTab.NMead] += p * b.G[12] * k[12];
                n[KsTab.NPlotno] += p * KsTab.NonFood[e][0] / 364.0 * 0.5; n[KsTab.NSukno] += p * KsTab.NonFood[e][1] / 364.0 * 0.5;
            }
        }

        /// <summary>Rece w polu wsi (workers_of): chlopi x 0.5 + niewolni x 0.65.</summary>
        internal static double VillageHands(double[] ppl) { return ppl[KsTab.EPeasant] * KsTab.Lambda[KsTab.EPeasant] + ppl[KsTab.ESlave] * KsTab.Lambda[KsTab.ESlave]; }

        /// <summary>Rece pol mieszczan: chlopi miasta x 1.0, niewolni x 0.5, wyrobnicy x 0.2 (razy LAMBDA stanu).</summary>
        internal static double TownFieldHands(double[] ppl)
        {
            double w = 0;
            for (int e = 0; e < KsTab.Est; e++) w += KsTab.TownFieldShare[e] * ppl[e] * KsTab.Lambda[e];
            return w;
        }
    }
}
