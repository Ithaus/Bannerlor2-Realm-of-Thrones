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
        public readonly Dictionary<string, double[]> ExoLand = new Dictionary<string, double[]>(StringComparer.Ordinal);   // kraina -> podaz spoza tabeli [NG] kg/d (las wsi, lodzie NavalDLC)
        // recenzja 16.10 (3): mieszczanie jedza wprost surowy jedwab / rude srebra, dopoki poprawki ZC1 tych drog sa wylaczone (SilkToWeavers; srebro - bez klucza) -
        // popyt Planu z prawdziwego zuzycia mieszczan (inaczej Plan zaklada drogi, ktorych w grze nie ma, i start nie jest zbilansowany)
        public bool SilkTownDirect, SilverTownDirect;
        // POPRAWKA PO AUTOTESCIE 16.10 (projekt rozdz. 9 i 11.1 krok 8 "pieniadze" - dotad nie bylo go w kodzie): Plan liczy tylko popyt, za ktory ktos placi.
        //  - wies: budzet koszyka = VilAlpha x wartosc sprzedazy wozow przy cenie polki startu (udzial kiesy wsi w utargu x cena wozu / cena polki x (1 - zapas na oplaty));
        //  - klasy miast W/R/P: KsSettlement.Budget (zarobek netto 28 dob x czesc na koszyk);
        //  - drabina koszyka (KsBasket.Ladder) przy cenach startu; to, czego wies nie oplaci, robi w domu (praca domowa - te same rece i ziemia, nigdy na targ),
        //    reszta niezaplacona miast odpada z popytu; punkt staly z tlumieniem 0.5 (MoneyIter rund rownowagi).
        public bool Money = true;
        public double[] VilAlpha = { 0.47, 0.47 };
        public double[][] Keep;                                       // [stan 6][grupa koszyka 13] udzial z wlasnego pola (DailyLife.KsKeepVillage) - koszyk wsi kroku 8
        public int MoneyIter = 10;
        public double HomeMax = 0.85;                                 // najwyzej taka czesc rak zajec okregu w pracy domowej
        public Dictionary<string, double> WarmHome;                   // okreg -> praca domowa z poprzedniego Planu (cieply start kroku 8) albo null
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
    }

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
    }

    /// <summary>
    /// KS - RDZEN PLANU (port ks_model2.py, rozdz. 3-6 i 11.1 projektu). Czysty C#: liczby z wejscia, wynik zawsze ten sam dla tego samego wejscia
    /// (watek roboczy, bez obiektow gry). Kroki: produkcja par blok-zajecie (rece x P0 x L, dom, zboze wlasne), popyt krain (cywile + 1:1 x sigma + wsady),
    /// regale (rece przy zlozu ida za popytem przy cenie 1), rownowaga reguly dochodu (logit beta 10 z premia mapy, ceny cienia, renta ziemi, test 2%),
    /// lepszy stol R8 (nadwyzki uboczne zostaja w domu) i R8b (wspolna nadwyzka rak - koszyk wsi do +10%).
    /// </summary>
    internal static class KsCore
    {
        private const double PLo = 0.05, PHi = 5.0, Eta = 0.05, Tol = 0.02;
        private const int StallN = 10;                                 // rownowaga stoi: porownanie sumy odchylen co 50 rund z wartoscia sprzed 10 x 50 rund
        private const int NM = 2;

        private sealed class Ctx
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
                // krok 8: pieniadze - popyt tylko zaplacony, praca domowa wsi, ktorych nie stac na koszyk (projekt rozdz. 9, 11.1)
                if (inp.Money) { MoneyStep(x, ref p, ref xs, ref S, cancel); if (cancel != null && cancel()) { R.Error = "przerwane"; return R; } }
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
            foreach (var kv in KsTab.P0[c]) outv[kv.Key] += cl * kv.Value * L;
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
                        double inc = x.Lp[k] * Rm[x.Pm[k] * KsTab.NC + c] - KsTab.KCost[c] - rent[b] * KsTab.HaPerWorker[c];
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
                    double inc = x.Lp[k] * Rm[x.Pm[k] * KsTab.NC + c] - KsTab.KCost[c] - rent[b] * KsTab.HaPerWorker[c];
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
                    if (!bad) break;
                    if (cancel != null && cancel()) break;
                    // koszt Planu (poprawka po autotescie 16.10): rownowaga, ktora stoi (suma odchylen towarow poza granica ceny spadla przez 500 rund o mniej niz 0.5%) -
                    // koniec; dotad kazda rownowaga szla do limitu rund (towary uboczne nigdy w 2%), 25 tys. rund na Plan
                    int slot = (it / 50) % StallN;
                    if (it >= 50 * StallN && stall[slot] > 0 && (stall[slot] - mt) < 0.005 * stall[slot]) break;
                    stall[slot] = mt;
                }
            }
            done = Math.Min(rounds, it + 1);
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

        /// <summary>Indeks ceny cienia towaru id na rynku m (praca - rachunek rak pracy domowej): towary KS - cena cienia Planu; regale, wyroby - 1.0.</summary>
        internal static double ShadowIndex(string id, double[] P, int m)
        {
            int g;
            if (id == null || !KsTab.GoodIndex.TryGetValue(id == "WhaleMeat" ? "meat" : id, out g) || KsTab.RegGoodMask[g]) return 1.0;
            double p = P != null && P.Length == NM * KsTab.NG ? P[(m == 0 ? 0 : 1) * KsTab.NG + g] : 1.0;
            if (!(p > 0) || double.IsNaN(p) || double.IsInfinity(p)) return 1.0;
            // cena na granicy 0.05 / 5 (towary uboczne: jaja, miod, skory, mieso z nadmiaru zywca) nie mowi o pracy - wartosc bazowa
            return p <= PLo * 1.01 || p >= PHi * 0.99 ? 1.0 : p;
        }

        /// <summary>Cena kilograma towaru id w zl gry przy indeksie idx (wartosc sztuki x indeks / (sigma x kg sztuki)); brak towaru - 0.</summary>
        private static double ZlPerKg(KsInput inp, string id, double idx)
        {
            KsItem it;
            if (id == null || !inp.Items.TryGetValue(id, out it) || it == null || !(it.Kg > 0) || !(inp.Sigma > 0)) return 0.0;
            return Math.Max(0.0, it.Val) * idx / (inp.Sigma * it.Kg);
        }

        private sealed class MDist { public KsDistrict D; public KsPart[] Parts; public double HwV, Sg, V0; }

        /// <summary>
        /// KROK 8 - PIENIADZE (punkt staly, MoneyIter rund rownowagi z tlumieniem 0.5; zloto nie powstaje - liczy tylko, kto za co placi):
        ///  - WIES: produkcja okregu (KsDeliver.Produce z udzialow rak rownowagi), to, co zje sama (KsDeliver.Own), koszyk targu (KsBasket.VillageKg) i sprzedaz wozow przy
        ///    cenach startu; budzet = VilAlpha x sprzedaz; drabina koszyka (KsBasket.Ladder); czego nie stac - PRACA DOMOWA: czesc rak zajec okregu hw robi to w domu
        ///    (reka w domu robi tyle, ile jej towar na targu - te same rece i ziemia, nigdy na targ): hw = (koszyk - alfa x S) / ((1 - alfa) x S), S - sprzedaz bez pracy domowej;
        ///    popyt Planu bez czesci niezaplaconej, podaz bez rak w domu;
        ///  - MIASTO: klasy W/R/P z budzetem (zarobek netto) - drabina; czesc niezaplacona odpada z popytu (miasto nie ma pola);
        ///  - RYNEK: nadwyzka wartosci podazy nad popytem towarow rak po rownowadze (np. niezaplacony koszyk miast) - wspolna praca domowa wsi rynku (rodziny robia dla siebie,
        ///    gdy targ nie kupi); niedobor - mniej pracy domowej.
        /// Wynik: DistHome, DistPaid, TownPaid (gra: stawki KS i koszyk ZC4 tylko zaplacony), liczby do linii (KsMoney).
        /// </summary>
        private static void MoneyStep(Ctx x, ref double[] p, ref double[] xs, ref double[] S, Func<bool> cancel)
        {
            var inp = x.In; var R = x.R;
            var M = new KsMoney { On = true };
            R.Money = M;
            foreach (var d in x.D) d.NeedR8 = (double[])d.Need.Clone();
            foreach (var t in inp.Towns) t.NeedR8 = (double[])t.Need.Clone();
            var vs = new List<MDist>();
            if (inp.Keep != null)
                foreach (var d in x.D)
                {
                    if (d.TownFields || d.PplHome == null || d.Bask == null || d.Blocks.Count == 0) continue;
                    var parts = new KsPart[d.Blocks.Count];
                    for (int i = 0; i < parts.Length; i++) parts[i] = new KsPart { K = d.Blocks[i] };
                    vs.Add(new MDist { D = d, Parts = parts, HwV = HomeOf(d) });
                }
            bool towns = false; foreach (var t in inp.Towns) if (t.Budget != null && t.ClassNeed != null) { towns = true; break; }
            if (vs.Count == 0 && !towns) { M.On = false; return; }
            int NZ = KsBasket.N, NI = KsDeliver.ItemCount;
            var view = new KsResult();
            var prod = new double[NI]; var prod2 = new double[NI]; var own = new double[NI]; var q = new double[NZ]; var paid = new double[NZ]; var u = new double[NZ];
            var keep = new double[KsTab.NN]; var full = new double[KsTab.NN];
            var pz = new double[NM * NZ]; var pi = new double[NM * NI];
            var sz = new double[NM * NZ]; var si = new double[NM * NI];   // ceny cienia (praca) - rachunek pracy domowej
            var g = new double[NM];
            var tq = new double[3][]; for (int c = 0; c < 3; c++) tq[c] = new double[NZ];
            var tqs = new double[NZ]; var tus = new double[NZ];
            for (int it = 0; it <= inp.MoneyIter; it++)
            {
                bool last = it == inp.MoneyIter;
                for (int m = 0; m < NM; m++)
                {
                    for (int z = 0; z < NZ; z++) pz[m * NZ + z] = ZlPerKg(inp, KsBasket.Ids[z], StartIndex(KsBasket.Ids[z], p, m));
                    for (int i = 0; i < NI; i++) pi[m * NI + i] = ZlPerKg(inp, KsDeliver.ItemIds[i], StartIndex(KsDeliver.ItemIds[i], p, m));
                    for (int z = 0; z < NZ; z++) sz[m * NZ + z] = ZlPerKg(inp, KsBasket.Ids[z], ShadowIndex(KsBasket.Ids[z], p, m));
                    for (int i = 0; i < NI; i++) si[m * NI + i] = ZlPerKg(inp, KsDeliver.ItemIds[i], ShadowIndex(KsDeliver.ItemIds[i], p, m));
                }
                // RYNEK: nadwyzka podazy nad popytem towarow rak (bez regale, ubocznych i koni) z ostatniej rownowagi w cenach cienia (praca) - wspolna praca domowa wsi rynku
                // (+: targ nie kupi, np. niezaplacony koszyk miast - rodziny robia dla siebie; -: brak - mniej pracy domowej, czesc niezaplaconego koszyka wsi zostaje
                // niezaspokojonym lepszym stolem); poziom rownowagi, ktorego ceny wzgledne nie poprawia
                if (it > 0)
                {
                    var v0m = new double[NM];
                    foreach (var v in vs) v0m[v.D.Market == 0 ? 0 : 1] += v.V0;
                    for (int m = 0; m < NM; m++)
                    {
                        double e = 0;
                        for (int k = 0; k < KsTab.NG; k++)
                        {
                            if (KsTab.RegGoodMask[k] || k == KsTab.GHorse || ExcessSkip(k)) continue;
                            int i = m * KsTab.NG + k;
                            if (!(x.Dm[i] > 1.0)) continue;
                            e += (S[i] - x.Dm[i]) * ZlPerKg(inp, KsTab.Goods[k], ShadowIndex(KsTab.Goods[k], p, m));
                        }
                        M.Excess[m] = e;
                        if (v0m[m] > 0) g[m] = Math.Max(-inp.HomeMax, Math.Min(inp.HomeMax, g[m] + 0.5 * e / v0m[m]));
                        M.Global[m] = g[m];
                    }
                }
                Array.Clear(M.VSales, 0, NM); Array.Clear(M.VBudget, 0, NM); Array.Clear(M.VBasket, 0, NM); Array.Clear(M.VPaid, 0, NM); Array.Clear(M.VHome, 0, NM);
                Array.Clear(M.VHands, 0, NM); Array.Clear(M.VHomeHands, 0, NM); Array.Clear(M.VShort, 0, NM); Array.Clear(M.VN, 0, NM);
                Array.Clear(M.TBudget, 0, M.TBudget.Length); Array.Clear(M.TBasket, 0, M.TBasket.Length); Array.Clear(M.TPaid, 0, M.TPaid.Length); Array.Clear(M.TUnRaw, 0, NM);
                Array.Clear(M.UnpaidV, 0, M.UnpaidV.Length); Array.Clear(M.UnpaidT, 0, M.UnpaidT.Length);
                view.BlockStart = x.BStart; view.X = xs; view.PairCraft = x.Pc; view.BlockW = x.W; view.RegHands = x.RegHands; view.Rent = x.Rent;
                // ---- wsie
                foreach (var v in vs)
                {
                    var d = v.D; int m = d.Market == 0 ? 0 : 1;
                    for (int j = 0; j < v.Parts.Length; j++) KsDeliver.FromPlan(view, d.Blocks[j].Index, v.Parts[j]);
                    KsDeliver.Produce(d, v.Parts, prod);
                    double made = 0; for (int i = 0; i < NI; i++) made += prod[i] * si[m * NI + i];   // przed Own (Own dopisuje skory z uboju na wlasny stol)
                    double home = 0; foreach (var e in d.PplHome) home += Math.Max(0.0, e);
                    double hr = d.People > 0 ? Math.Min(1.0, home / d.People) : 0.0;
                    NeedsOfKeep(d.PplHome, d.Bask, inp.Keep, d.Oil, home, keep);
                    for (int k = 0; k < KsTab.NN; k++)
                    {
                        double mult = d.Need0[k] > 1e-9 ? Math.Max(0.25, Math.Min(4.0, d.NeedR8[k] / d.Need0[k])) : 1.0;
                        keep[k] *= mult; full[k] = d.NeedR8[k] * hr;
                    }
                    KsDeliver.Own(prod, keep, own);
                    Array.Clear(q, 0, NZ);
                    KsBasket.VillageKg(full, keep, own, q);
                    double sales = 0;
                    for (int i = 0; i < NI; i++) { double mk = prod[i] - own[i]; if (mk > 0) sales += mk * pi[m * NI + i]; }
                    double bask = 0; for (int z = 0; z < NZ; z++) bask += q[z] * pz[m * NZ + z];
                    double a = Math.Max(0.0, Math.Min(0.95, inp.VilAlpha[m]));
                    double budget = a * sales;
                    double pv = KsBasket.Ladder(q, pz, m * NZ, budget, paid);
                    double hwc = HomeOf(d);
                    double sgross = sales / Math.Max(0.05, 1.0 - hwc);
                    v.Sg = sgross;
                    // punkt staly pracy domowej: reka w domu robi tyle surowca, ile robilaby w zajeciu (wartosc calej produkcji okregu przy hw = 0, takze zjadanej) -
                    // hw x V0 = surowiec koszyka niezaplaconego (wyroby - ich wsad: piwo -> ziarno, wino -> winogrona ...; przerob w domu bez dodatkowych rak) przy biezacym hw;
                    // nachylenie (zjedzone + alfa x sprzedaz) / V0 < 1 - zbiega. Rachunek rak w CENACH CIENIA (praca): rece zdjete z zajec = praca zawarta w koszyku, ktory
                    // odpada z popytu - poziom rownowagi Planu sie zgadza (w cenach startu zboze Essos 1.5 zamiast 3.1 zdejmowalo 2-4 razy za duzo rak); budzet i drabina - ceny startu
                    // V0 - wartosc samych zajec przy hw = 0: produkcja zajec jest liniowa w (1 - hw) - druga produkcja przy hw + 0.5 (dom, zboze wlasne, regale bez zmian)
                    double hw2 = Math.Min(0.95, hwc + 0.5);
                    d.HomeWork = hw2;
                    KsDeliver.Produce(d, v.Parts, prod2);
                    d.HomeWork = hwc;
                    double made2 = 0; for (int i = 0; i < NI; i++) made2 += prod2[i] * si[m * NI + i];
                    double v0 = hw2 > hwc ? Math.Max(0.0, made - made2) / (hw2 - hwc) : made, raw = 0;
                    for (int z = 0; z < NZ; z++) if (paid[z] < 1.0) raw += (1.0 - paid[z]) * q[z] * RawZ(sz, m * NZ, z);
                    v.V0 = v0;
                    double target = v0 > 0 ? Math.Max(0.0, Math.Min(inp.HomeMax, raw / v0)) : 0.0;
                    if (!last) v.HwV += 0.5 * (target - v.HwV);
                    M.VSales[m] += sales; M.VBudget[m] += budget; M.VBasket[m] += bask; M.VPaid[m] += pv; M.VHome[m] += Math.Max(0.0, bask - pv); M.VN[m]++;
                    if (bask > budget * 1.001 + 1e-6) M.VShort[m]++;
                    M.VHands[m] += d.Wf; M.VHomeHands[m] += d.Wf * hwc;
                    for (int z = 0; z < NZ; z++) { u[z] = (1.0 - paid[z]) * q[z]; M.UnpaidV[m * NZ + z] += u[z]; }
                    if (last)
                    {
                        var fp = new float[NZ]; for (int z = 0; z < NZ; z++) fp[z] = (float)paid[z];
                        R.DistPaid[d.Id] = fp; R.DistHome[d.Id] = hwc;
                    }
                    else
                    {
                        Array.Copy(d.NeedR8, d.Need, KsTab.NN);
                        UnpaidToNeed(d.Need, u, false);
                    }
                }
                // ---- miasta (klasy W/R/P)
                foreach (var t in inp.Towns)
                {
                    if (t.Budget == null || t.ClassNeed == null || t.Budget.Length < 3 || t.ClassNeed.Length < 3) continue;
                    int m = t.Market == 0 ? 0 : 1;
                    Array.Clear(tqs, 0, NZ); Array.Clear(tus, 0, NZ);
                    for (int c = 0; c < 3; c++)
                    {
                        var qc = tq[c]; Array.Clear(qc, 0, NZ);
                        if (t.ClassNeed[c] == null) continue;
                        double fk; KsBasket.TownKg(t.ClassNeed[c], 0.0, qc, out fk);
                        double cost = 0; for (int z = 0; z < NZ; z++) cost += qc[z] * pz[m * NZ + z];
                        double pv = KsBasket.Ladder(qc, pz, m * NZ, Math.Max(0.0, t.Budget[c]), paid);
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
                // praca domowa na nastepna rownowage: wlasna (koszyk ponad budzet) + wspolna rynku
                foreach (var v in vs) v.D.HomeWork = Math.Max(0.0, Math.Min(inp.HomeMax, v.HwV + g[v.D.Market == 0 ? 0 : 1]));
                M.Trace.Add("runda " + (it + 1) + ": wsie W/E sprzedaz " + Math.Round(M.VSales[0]) + "/" + Math.Round(M.VSales[1]) + ", koszyk " + Math.Round(M.VBasket[0]) + "/" + Math.Round(M.VBasket[1])
                            + ", zaplacone " + Math.Round(M.VPaid[0]) + "/" + Math.Round(M.VPaid[1]) + ", nadwyzka rynku " + Math.Round(M.Excess[0]) + "/" + Math.Round(M.Excess[1])
                            + ", wspolna praca domowa " + M.Global[0].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "/" + M.Global[1].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                Pairs(x);
                Demand(x);
                RegaleStep(x);
                int rounds;
                p = Equilibrium(x, p, inp.RoundsR8b, out xs, out S, out rounds, cancel);
                R.Rounds += rounds; M.Iter++;
                if (cancel != null && cancel()) return;
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
