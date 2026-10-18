using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Armoury
{
    /// <summary>
    /// KS-R3 (17.10, "kupcy i krainy") - PLAN, KROK 5 "KORYTARZE I KRAINY" (projekt KS rozdz. 5; czysty C#, watek roboczy). Runda 3 pokazala, ze Plan bilansuje
    /// SWIAT (Westeros i Essos jako jeden targ kazdy), ale towar nie dochodzi do miast, ktore go potrzebuja: zboze lezy tam, gdzie roslo, a miasta daleko od pol stoja
    /// puste i drogie. Krok 5 wprowadza miejsce jednym czynnikiem:
    ///  - KRAINA osady = kultura miasta targowego (miasto - jego kultura; okreg wsi - kraina jego miasta targowego; pola mieszczan - ich miasto);
    ///  - podaz krainy S_L = produkcja okregow krainy (z polami mieszczan i podaza spoza tabeli), popyt krainy D_L = popyt ZAPLACONY osad krainy (potrzeby po kroku 8 -
    ///    krok 5 liczy sie po kroku 8; recenzja runda 3) + odbior panow i dworow w miastach krainy + reszta 1:1 wedlug ludzi - zjedzone w domu jest po obu stronach;
    ///  - PROBLEM PRZEWOZU na rynek i towar jedzenia: krainy z nadwyzka -> krainy z brakiem, koszt luku = doby wozem bramy (najblizsza para miast obu krain,
    ///    nie w wojnie; morzem - statkiem), luk tylko do 30 dob (trwale) albo 7 dob (psujace sie, KsCore.Perish), przepustowosc przed D z JEDNEGO budzetu kasy krainy
    ///    importera (15% kasy 28 dob na dobe, polowa kasy na towar w drodze; dzielony na towary wedlug wartosci braku), po D - 1.2 x dowiezione wozami KS-H (najmniej
    ///    20 szt./d; do NKS-24 takze przewoz bez tego sufitu); najtanszy przeplyw (kolejne najkrotsze sciezki);
    ///  - GRADIENT krainy (cena krainy wobec ceny rynku w dochodzie bloku): importer 1.03 + tau x srednie doby dowozu (tau 0.025/d ladem, 0.0025/d morzem), najwyzej 1.5;
    ///    eksporter z nadwyzka, ktorej nie da sie wywiezc (> 5% podazy) - najwyzej 1 - nadwyzka/podaz, najmniej 0.6; brak bez zrodla w zasiegu (> 5% popytu) - 1.5
    ///    (tryb "kraina": kraina liczy sie sama); reszta 1; tlumienie: Grad = 0.75 x poprzedni + 0.25 x nowy (recenzja rundy 3; cieply z Planu do Planu, w zapisie arm_ks_r3);
    ///  - rownowaga: dochod bloku przy cenie rynku x gradient krainy (Rl), czyszczenie rynku, numerator, renta, najlepszy stan i koniec "stoi" - bez zmian; gradient 1 -
    ///    wynik bit w bit jak dotad;
    ///  - REGULA ZASIEGU psujacych sie: miasto, ktorego okregi daja mniej niz 5% jego popytu na towar psujacy sie i bez miasta z nadwyzka tego rynku w 7 dobach wozem
    ///    (nie w wojnie) - czesc zaplacona tego towaru w miescie i jego wsiach 0 (krok 8: drabina zastepuje; popyt Planu spada o tyle samo).
    /// Wydajnosc rak bez zmian - zmienia sie tylko liczba rak przy towarze (rece ida za cena krainy).
    /// </summary>
    internal static partial class KsCore
    {
        // jedzenie przewozone (towary KsTab): zboze, ryby, mieso (z miesem wieloryba), ser, maslo, jaja, jablka, marchew, pomarancze, winogrona, oliwki, daktyle, miod, sol
        internal static readonly int[] Food = { KsTab.GGrain, KsTab.GFish, KsTab.GMeat, KsTab.GCheese, KsTab.GButter, KsTab.GEgg, KsTab.GApple, KsTab.GCarrot, KsTab.GOrange,
                                                KsTab.GGrape, KsTab.GOlives, KsTab.GDate, KsTab.GHoney, KsTab.GSalt };
        private static readonly bool[] FoodMask = BuildFoodMask();
        private static bool[] BuildFoodMask() { var a = new bool[KsTab.NG]; foreach (int g in Food) a[g] = true; return a; }
        private static readonly bool[] PerishG = BuildPerishG();
        private static bool[] BuildPerishG()
        {
            // te same towary co KsCore.Perish (inicjalizacja pol statycznych w plikach czesciowych bez kolejnosci - bez odwolania do tamtego pola)
            var a = new bool[KsTab.NG];
            foreach (int g in new[] { KsTab.GFish, KsTab.GMeat, KsTab.GEgg, KsTab.GButter, KsTab.GApple, KsTab.GCarrot, KsTab.GOrange, KsTab.GGrape, KsTab.GDate }) a[g] = true;
            return a;
        }
        // KS-R3 runda 3 symulatora: psujace sie na poziomie miast. W GRZE tylko pomiar (linia korytarzy: stojace i bez dowozu wedlug miast); gradient miast (TownGrad:
        // eksporter TownExport z najnizszym TownExportMin, premia braku z wlasnej podazy TownImport) i regula zasiegu po przewozie miast (TownReach) - WYLACZONE, wlacza
        // je tylko harness do prob (port symulatora 17.10, runda 3: eksporter zabieral Arbor utarg ryb - Plan bez pokrycia Westeros 3.8% -> 6.0%; premia i regula
        // zasiegu po przewozie - mediany cen bez zmiany ponad szum 3 ziaren, bez pokrycia Westeros +0.3..+0.9 pkt). Wylaczone - Plan bit w bit jak bez kroku miast.
        internal static bool TownGrad = false, TownExport = true, TownImport = true, TownReach = false; internal static double TownExportMin = 0.6;
        // recenzja runda 3: tlumienie gradientu 0.75 poprzedni + 0.25 nowy (0.5/0.5 drgal: Essos winogrona i pomarancze 0.6-1.7 miedzy Planami w harnessie; 0.75 - zmiana < 5%)
        internal static double GradDamp = 0.75; internal static bool GradSecondRound = true;   // harness (recenzja runda 3): tlumienie gradientu z Planu do Planu, druga runda Planu wlaczenia
        // recenzja runda 3 (wazne): przewoz ladem drogi, morzem tani - woz 2.5% wartosci na dobe, statek 0.25% na dobe (dotad 1.0% / 0.5%: morze tylko 2 x taniej niz woz;
        // w sredniowieczu woda 10-20 x taniej). Te same stale co KS-H (KsFoodCarts). Kasa importera: 15% kasy 28 dob na dobe (jak KS-H)
        // recenzja runda 3 (wazne): statek szybszy niz woz - doby morzem = droga ladem i morzem / (4 x tempo wozu) + 1 doba portu (zaladunek i wyladunek); KS-H i krok 5
        internal const double ShipSpeedK = 4.0; internal const int PortDays = 1;
        internal const double CorrTauLand = 0.025, CorrTauSea = 0.0025, CorrMargin = 0.03, CorrMaxG = 1.5, CorrMinG = 0.6, CorrDurableDays = 30.0, CorrPerishDays = 7.0, CorrCofferDay = 0.15;

        private sealed partial class Ctx
        {
            public Dictionary<string, int> LandIx; public int NL;
            public int[] Pl;                                           // para -> kraina x NM + rynek
            public double[] GradBase, GradEff;                         // [NL * NG]: gradient (tlumiony) i skuteczny (x ciecie nadmiaru)
            public bool GradOn;
            public double[] GradDelta; public readonly List<int> GradActive = new List<int>();
            public List<KeyValuePair<int, double>>[] MVFood;           // [c]: (towar jedzenia, MV)
            public float[] GwDays; public bool[] GwSea;                // [NL * NL] brama krain (doby, morze); -1 - brak
            public int[] GwFrom, GwTo;                                 // [NL * NL] para miast bramy (indeksy Towns)
            public Dictionary<string, bool[]> ReachT, ReachD;           // regula zasiegu: miasto / okreg -> pozycje koszyka wyciete
            public int[] TownLand;                                     // [nt] kraina miasta
            public double[] FlowPcs = new double[NM * KsTab.NG], FlowDays = new double[NM * KsTab.NG], FlowSea = new double[NM * KsTab.NG];
            // KS-R3 runda 3 symulatora: PSUJACE SIE NA POZIOMIE MIAST (okregi miasta targowego) - wezly dochodu: krainy x rynek [0, NodeTown), miasta x rynek
            // [NodeTown, NodeTown + NT x NM), wiersz zerowy na koncu; gradient miasta towaru psujacego sie (tlumiony, cieply z Planu do Planu), stojace i niedowiezione
            public int NT, NodeTown;
            public double[] TGradBase;                                 // [NT * NG] gradient miasta (tylko psujace sie; reszta 1)
            public double[] TUnmet;                                    // [NT * NG] czesc popytu miasta psujacego sie bez dowozu w 7 dobach po przewozie miast (regula zasiegu)
            public double[] TStuck = new double[NM * KsTab.NG], TSup = new double[NM * KsTab.NG], TMoved = new double[NM * KsTab.NG], TShort = new double[NM * KsTab.NG], TDem = new double[NM * KsTab.NG];
            public double TGMin = 1.0, TGMax = 1.0; public int TGN; public bool TownPerishDone;
        }

        /// <summary>Kraina okregu: kraina miasta targowego (CorrLand) albo wlasna; -1 - nieznana.</summary>
        private static int LandOfD(Ctx x, KsDistrict d)
        {
            if (d.CorrLand >= 0) return d.CorrLand;
            int l; return d.Land != null && x.LandIx.TryGetValue(d.Land, out l) ? l : -1;
        }

        /// <summary>Przygotowanie kroku 5 (Prepare, po krainach): indeksy krain, kraina okregu z miasta targowego, para -> kraina, MV jedzenia zajec, brama krain
        /// z tabeli dob miast, gradient z poprzedniego Planu i ciecie nadmiaru krainy. Bez kroku 5 (klucze wylaczone) - nic (dochod bloku jak dotad).</summary>
        private static void CorridorPrepare(Ctx x)
        {
            var inp = x.In;
            if (!inp.Corridors && !inp.Reach) return;
            x.LandIx = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < x.Lands.Count; i++) x.LandIx[x.Lands[i]] = i;
            x.NL = x.Lands.Count;
            int nt = inp.Towns.Count;
            x.TownLand = new int[nt];
            for (int t = 0; t < nt; t++) { int l; x.TownLand[t] = x.LandIx.TryGetValue(inp.Towns[t].Land ?? "?", out l) ? l : -1; }
            foreach (var d in x.D)
            {
                d.CorrLand = -1; d.CorrTown = -1;
                int t;
                if (inp.DistTown != null && inp.DistTown.TryGetValue(d.Id, out t) && t >= 0 && t < nt) { d.CorrLand = x.TownLand[t]; d.CorrTown = t; }
            }
            x.NT = nt; x.NodeTown = x.Lands.Count * NM;
            // MV jedzenia zajec (rzadko)
            x.MVFood = new List<KeyValuePair<int, double>>[KsTab.NC];
            for (int c = 0; c < KsTab.NC; c++)
            {
                var l = new List<KeyValuePair<int, double>>();
                foreach (int g in Food) { double v = x.MV[c * KsTab.NG + g]; if (v != 0.0) l.Add(new KeyValuePair<int, double>(g, v)); }
                x.MVFood[c] = l;
            }
            // brama krain: najblizsza para miast obu krain tego samego rynku, nie w wojnie
            int nl = x.NL;
            x.GwDays = new float[nl * nl]; x.GwSea = new bool[nl * nl]; x.GwFrom = new int[nl * nl]; x.GwTo = new int[nl * nl];
            for (int i = 0; i < x.GwDays.Length; i++) { x.GwDays[i] = -1f; x.GwFrom[i] = x.GwTo[i] = -1; }
            if (inp.TownDays != null && inp.TownDays.Length == nt * nt)
                for (int a = 0; a < nt; a++)
                {
                    int la = x.TownLand[a]; if (la < 0) continue;
                    for (int b = 0; b < nt; b++)
                    {
                        if (a == b) continue;
                        int lb = x.TownLand[b]; if (lb < 0 || lb == la) continue;
                        float dd = inp.TownDays[a * nt + b];
                        if (!(dd >= 0f)) continue;
                        if (inp.TownWar != null && inp.TownWar[a * nt + b]) continue;
                        int k = la * nl + lb;
                        if (x.GwDays[k] < 0f || dd < x.GwDays[k]) { x.GwDays[k] = dd; x.GwSea[k] = inp.TownSea != null && inp.TownSea[a * nt + b]; x.GwFrom[k] = a; x.GwTo[k] = b; }
                    }
                }
            // gradient z poprzedniego Planu (tlumiony) i ciecie nadmiaru krainy (regula P4)
            x.GradBase = new double[nl * KsTab.NG]; x.GradEff = new double[nl * KsTab.NG];
            for (int i = 0; i < x.GradBase.Length; i++) x.GradBase[i] = 1.0;
            if (inp.Corridors && inp.GradPrev != null)
                for (int l = 0; l < nl; l++)
                {
                    double[] gp; if (!inp.GradPrev.TryGetValue(x.Lands[l], out gp) || gp == null) continue;
                    foreach (int g in Food) if (g < gp.Length && gp[g] > 0 && !double.IsNaN(gp[g])) x.GradBase[l * KsTab.NG + g] = Math.Max(CorrMinG, Math.Min(CorrMaxG, gp[g]));
                }
            // KS-R3 runda 3 symulatora: gradient miast psujacych sie z poprzedniego Planu (wpisy "@" + miasto w tym samym slowniku)
            x.TGradBase = new double[nt * KsTab.NG]; x.TUnmet = new double[nt * KsTab.NG];
            for (int i = 0; i < x.TGradBase.Length; i++) x.TGradBase[i] = 1.0;
            if (inp.Corridors && inp.GradPrev != null)
                for (int t = 0; t < nt; t++)
                {
                    double[] gp; if (!inp.GradPrev.TryGetValue("@" + inp.Towns[t].Id, out gp) || gp == null) continue;
                    foreach (int g in Food) if (PerishG[g] && g < gp.Length && gp[g] > 0 && !double.IsNaN(gp[g])) x.TGradBase[t * KsTab.NG + g] = Math.Max(CorrMinG, Math.Min(CorrMaxG, gp[g]));
                }
            x.GradDelta = new double[(x.NodeTown + nt * NM + 1) * KsTab.NC];
            RefreshGradEff(x);
            BuildPl(x);
        }

        /// <summary>Gradient skuteczny = gradient x ciecie nadmiaru krainy; lista par kraina x rynek z gradientem rozny od 1 (dochod - LandDelta).</summary>
        private static void RefreshGradEff(Ctx x)
        {
            var inp = x.In;
            int nl = x.NL;
            x.GradActive.Clear();
            bool any = false;
            for (int l = 0; l < nl; l++)
            {
                double[] cut = null;
                if (inp.LandCut != null) inp.LandCut.TryGetValue(x.Lands[l], out cut);
                bool act = false;
                for (int g = 0; g < KsTab.NG; g++)
                {
                    int i = l * KsTab.NG + g;
                    double v = inp.Corridors ? x.GradBase[i] : 1.0;
                    if (cut != null && g < cut.Length && cut[g] > 0 && cut[g] < 1.0) v *= cut[g];
                    x.GradEff[i] = v;
                    if (v != 1.0) act = true;
                }
                if (act) { any = true; for (int m = 0; m < NM; m++) x.GradActive.Add(l * NM + m); }
            }
            // KS-R3 runda 3 symulatora: wezly miast (okregi z miastem targowym) - czynne, gdy czynna kraina miasta albo gradient miasta psujacego sie rozny od 1
            if (x.TGradBase != null)
                for (int t = 0; t < x.NT; t++)
                {
                    int l = x.TownLand[t];
                    if (l < 0) continue;                                // miasto bez krainy - jego okregi zostaja w wezle wlasnej krainy (BuildPl)
                    bool act = false;
                    if (l >= 0) for (int g = 0; g < KsTab.NG && !act; g++) if (x.GradEff[l * KsTab.NG + g] != 1.0) act = true;
                    if (!act && inp.Corridors) foreach (int g in Food) if (PerishG[g] && x.TGradBase[t * KsTab.NG + g] != 1.0) { act = true; break; }
                    if (act) { any = true; for (int m = 0; m < NM; m++) x.GradActive.Add(x.NodeTown + t * NM + m); }
                }
            x.GradOn = any;
        }

        /// <summary>Para -> kraina x NM + rynek (kraina miasta targowego okregu bloku pary).</summary>
        private static void BuildPl(Ctx x)
        {
            if (x.LandIx == null || x.Pb == null) return;
            var pl = new int[x.NP];
            for (int k = 0; k < x.NP; k++)
            {
                var d = x.BD[x.Pb[k]];
                // KS-R3 runda 3 symulatora: okreg z miastem targowym - wezel miasta (kraina miasta + gradient miasta psujacych sie); bez miasta - wezel krainy
                if (d.CorrTown >= 0 && d.CorrTown < x.NT && x.TGradBase != null && x.TownLand[d.CorrTown] >= 0) { pl[k] = x.NodeTown + d.CorrTown * NM + x.Pm[k]; continue; }
                int l = LandOfD(x, d);
                pl[k] = l >= 0 ? l * NM + x.Pm[k] : 0;
                if (l < 0) pl[k] = -1;
            }
            // okreg bez krainy - wiersz zerowy (dodatek 0): osobny indeks za ostatnim miastem
            int zero = x.NodeTown + x.NT * NM;
            for (int k = 0; k < x.NP; k++) if (pl[k] < 0) pl[k] = zero;
            if (x.GradDelta == null || x.GradDelta.Length < (zero + 1) * KsTab.NC) x.GradDelta = new double[(zero + 1) * KsTab.NC];
            x.Pl = pl;
        }

        /// <summary>Dodatek do dochodu zajecia c na pracujacego w krainie x rynku (cena rynku x (gradient - 1) x MV jedzenia) - tylko pary z gradientem rozny od 1.</summary>
        private static double[] LandDelta(Ctx x, double[] p)
        {
            var d = x.GradDelta;
            Array.Clear(d, 0, d.Length);
            foreach (int lm in x.GradActive)
            {
                int o = lm * KsTab.NC;
                if (lm >= x.NodeTown)
                {
                    // wezel miasta: gradient krainy miasta x gradient miasta (towary psujace sie)
                    int t = (lm - x.NodeTown) / NM, mt = (lm - x.NodeTown) % NM, lt = x.TownLand[t], pmt = mt * KsTab.NG, to = t * KsTab.NG;
                    for (int c = 0; c < KsTab.NC; c++)
                    {
                        double s = 0;
                        foreach (var kv in x.MVFood[c])
                        {
                            double gr = lt >= 0 ? x.GradEff[lt * KsTab.NG + kv.Key] : 1.0;
                            if (PerishG[kv.Key] && x.In.Corridors) gr *= x.TGradBase[to + kv.Key];
                            if (gr != 1.0) s += p[pmt + kv.Key] * (gr - 1.0) * kv.Value;
                        }
                        d[o + c] = s;
                    }
                    continue;
                }
                int l = lm / NM, m = lm % NM, pm = m * KsTab.NG, lo = l * KsTab.NG;
                for (int c = 0; c < KsTab.NC; c++)
                {
                    double s = 0;
                    foreach (var kv in x.MVFood[c])
                    {
                        double gr = x.GradEff[lo + kv.Key];
                        if (gr != 1.0) s += p[pm + kv.Key] * (gr - 1.0) * kv.Value;
                    }
                    d[o + c] = s;
                }
            }
            return d;
        }

        /// <summary>Potrzeby osady (kg/d, NN) -> popyt towarow KsTab bez 1:1 (te same proporcje co Demand: zboze z chlebem, plackiem i piwem, wino, oliwa, miod pitny).</summary>
        private static void CivilGoods(double[] C, double[] D)
        {
            D[KsTab.GGrain] += C[KsTab.NG_] + C[KsTab.NBeer] * KsTab.BeerGrain;
            D[KsTab.GFish] += C[KsTab.NFish]; D[KsTab.GCheese] += C[KsTab.NCheese]; D[KsTab.GButter] += C[KsTab.NButter]; D[KsTab.GEgg] += C[KsTab.NEgg];
            D[KsTab.GSalt] += C[KsTab.NSalt]; D[KsTab.GHoney] += C[KsTab.NHoney] + C[KsTab.NMead] * KsTab.MeadHoney; D[KsTab.GMeat] += C[KsTab.NMeat];
            for (int i = 0; i < 6; i++) D[KsTab.FruitGood[i]] += C[KsTab.FruitNeed[i]];
            D[KsTab.GGrape] += C[KsTab.NWine] * KsTab.WineGrape;
            D[KsTab.GOlives] += (C[KsTab.NOil] + C[KsTab.NSwOlej]) * KsTab.OilOlive;
        }

        /// <summary>1:1 jedzenia (wojsko, zamki, dwory - kg/d swiata) x udzial ludzi.</summary>
        private static void OneGoods(Ctx x, double sh, double[] D)
        {
            Func<string, double> o = id => OneToOne(x, id) * sh;
            D[KsTab.GGrain] += o("grain") + o("bread") + o("pie") + o("beer") * KsTab.BeerGrain;
            D[KsTab.GFish] += o("fish"); D[KsTab.GCheese] += o("cheese"); D[KsTab.GButter] += o("butter"); D[KsTab.GEgg] += o("Egg");
            D[KsTab.GSalt] += o("salt"); D[KsTab.GHoney] += o("honey") + o("mead") * KsTab.MeadHoney; D[KsTab.GMeat] += o("meat") + o("WhaleMeat");
            for (int i = 0; i < 6; i++) D[KsTab.FruitGood[i]] += o(KsTab.Goods[KsTab.FruitGood[i]]);
            D[KsTab.GGrape] += o("wine") * KsTab.WineGrape; D[KsTab.GOlives] += o("oil") * KsTab.OilOlive;
        }

        /// <summary>Kg sztuki gry towaru KsTab g (mieso - mieso) x sigma: kg Planu na sztuke gry.</summary>
        private static double KgPerPiece(Ctx x, int g)
        {
            KsItem it;
            string id = KsTab.Goods[g];
            if (!x.In.Items.TryGetValue(id, out it) || it == null || !(it.Kg > 0)) return Math.Max(1.0, x.In.Sigma) * 10.0;
            return Math.Max(1.0, x.In.Sigma) * it.Kg;
        }

        /// <summary>Recenzja runda 3 (wazne): 1:1 jedzenia (kg/d Planu) poza odbiorem panow i dworow w miastach (TownOne) - zamki, wojsko w polu, dwory poza miastami;
        /// rozkladane wedlug ludzi (gdzie nie wiadomo, kto je je).</summary>
        private static double[] OneRest(Ctx x)
        {
            var w = new double[KsTab.NG];
            OneGoods(x, 1.0, w);
            if (x.In.TownOne != null)
                foreach (var kv in x.In.TownOne) if (kv.Value != null) foreach (int g in Food) if (g < kv.Value.Length) w[g] -= Math.Max(0.0, kv.Value[g]);
            for (int g = 0; g < w.Length; g++) if (w[g] < 0) w[g] = 0;
            return w;
        }

        /// <summary>Odbior panow i dworow w miescie (TownOne, kg/d Planu) do popytu jedzenia D.</summary>
        private static void TownOneAdd(Ctx x, string id, double[] D)
        {
            double[] one; if (x.In.TownOne == null || id == null || !x.In.TownOne.TryGetValue(id, out one) || one == null) return;
            foreach (int g in Food) if (g < one.Length && one[g] > 0) D[g] += one[g];
        }

        /// <summary>KROK 5 na rownowadze PO KROKU 8 (xs, ceny p): podaz krain i popyt ZAPLACONY krain, problem przewozu na rynek i towar jedzenia, gradient (tlumiony).
        /// Recenzja runda 3 (wazne): (1) popyt = potrzeby osad po kroku 8 (czesc niezaplacona zdjeta - dotad pelna potrzeba, blad rundy 1 na poziomie krainy) + odbior panow
        /// i dworow w miastach (TownOne), reszta 1:1 swiata wedlug ludzi; zjedzone w okregu po obu stronach; (2) przed D jeden budzet kasy krainy importera na wszystkie towary
        /// i luki (najwyzej 15% kasy 28 dob na dobe - jak KS-H - i polowa kasy na towar w drodze), dzielony wedlug wartosci braku towaru (dotad cala kasa dla kazdego towaru
        /// i luku); (3) po D przepustowosc z dowozu KS-H (gradient), a do NKS-24 przewoz BEZ sufitu dowozu (z budzetem kasy jak przed D) - R.LandFlowFree.</summary>
        private static void CorridorStep(Ctx x, double[] p, double[] xs, double[] S)
        {
            var sw = Stopwatch.StartNew();
            var inp = x.In; var R = x.R;
            if (x.LandIx == null) return;
            int nl = x.NL, NG = KsTab.NG, nt = inp.Towns.Count;
            R.CorrRounds++;
            x.WarCutN = 0; x.WarCutL.Clear();                          // KS-R5 ceny-r5: luki odciete wojna w tej rundzie kroku 5
            var prod = DistrictProd(x, xs);
            var SL = new double[nl * NG]; var DL = new double[nl * NG];
            var tmp = new double[NG];
            double wpop = Math.Max(1.0, x.WPop);
            var rest = OneRest(x);
            foreach (var d in x.D)
            {
                int l = LandOfD(x, d); if (l < 0) continue;
                double[] a;
                if (prod.TryGetValue(d.Id, out a)) foreach (int g in Food) SL[l * NG + g] += a[g];
                Array.Clear(tmp, 0, NG);
                CivilGoods(d.Need, tmp);
                foreach (int g in Food) DL[l * NG + g] += tmp[g] + rest[g] * d.People / wpop;
            }
            for (int t = 0; t < nt; t++)
            {
                int l = x.TownLand[t]; if (l < 0) continue;
                var ts = inp.Towns[t];
                Array.Clear(tmp, 0, NG);
                CivilGoods(ts.Need, tmp);
                TownOneAdd(x, ts.Id, tmp);
                foreach (int g in Food) DL[l * NG + g] += tmp[g] + rest[g] * ts.People / wpop;
            }
            foreach (var kv in inp.ExoLand)
            {
                int l; if (kv.Value == null || !x.LandIx.TryGetValue(kv.Key, out l)) continue;
                foreach (int g in Food) SL[l * NG + g] += kv.Value[g];
            }
            foreach (var gl in KsTab.GarumLands)
            {
                int l; if (!x.LandIx.TryGetValue(gl, out l)) continue;
                DL[l * NG + KsTab.GFish] += KsTab.Garum * 2.0 / KsTab.GarumLands.Count; DL[l * NG + KsTab.GSalt] += KsTab.Garum * 0.4 / KsTab.GarumLands.Count;
            }
            // kasa 28 dob miast krainy i budzet krainy importera: udzial towaru = wartosc braku towaru / wartosc braku wszystkich towarow jedzenia krainy
            var gold = new double[nl];
            for (int t = 0; t < nt; t++) { int l = x.TownLand[t]; if (l >= 0 && inp.TownGold28 != null && t < inp.TownGold28.Length) gold[l] += Math.Max(0.0, inp.TownGold28[t]); }
            var zlG = new double[NM * NG];
            foreach (int g in Food) for (int m = 0; m < NM; m++) zlG[m * NG + g] = ZlPerKg(inp, KsTab.Goods[g], StartIndex(KsTab.Goods[g], p, m));
            var defSh = new double[nl * NG];
            for (int l = 0; l < nl; l++)
            {
                int mk; if (!x.LandMk.TryGetValue(x.Lands[l], out mk)) mk = 0;
                double tot = 0;
                foreach (int g in Food) { double e = DL[l * NG + g] - SL[l * NG + g]; if (e > 0) { defSh[l * NG + g] = e * zlG[mk * NG + g]; tot += defSh[l * NG + g]; } }
                foreach (int g in Food) defSh[l * NG + g] = tot > 0 ? defSh[l * NG + g] / tot : 0.0;
            }
            // KS-R4 r4-kupcy H7 (= K1): korytarze z modelem kredytu kupcow (klucze Ks Corridors Credit + Ks Food Carts Consign) - importer placi przy dostawie z kasy ponad
            // polowe zapasu (15% x max(0, kasa 28 dob - 0.5 x zapas) na dobe, miasta krainy), eksporter niesie towar na kredyt (0.5 x kasa 28 dob 3 miast z nadwyzka / (doby + 3),
            // dzielone na towary wedlug wartosci nadwyzki), doby luku = srednia wedlug braku miast importera najlepszych dob do 3 miast eksportera z nadwyzka (tabela KS-H)
            var cr = CreditPrep(x, prod, rest, wpop, SL, DL, zlG);
            // problem przewozu
            R.LandFlow.Clear(); R.LandFlowFree.Clear(); R.Kraina.Clear(); R.NoOutlet.Clear();
            Array.Clear(x.FlowPcs, 0, x.FlowPcs.Length); Array.Clear(x.FlowDays, 0, x.FlowDays.Length); Array.Clear(x.FlowSea, 0, x.FlowSea.Length);
            var graw = new double[nl * NG]; for (int i = 0; i < graw.Length; i++) graw[i] = 1.0;
            var nodes = new List<int>(nl);
            var kraina = new List<string>();
            foreach (int g in Food)
            {
                double kgp = KgPerPiece(x, g);
                bool per = PerishG[g];
                // bez gradientu (przewoz tylko do linii): towary uboczne gospodarstw (jaja, miod, marchew, jablka - wyjatki pasma) i towar, ktorego cena rynku lezy na
                // podlodze (nadmiar z uboczu stad - mieso: wyzsza cena krainy dalaby wiecej stad i jeszcze wiekszy nadmiar)
                bool side = ExcessSkip(g);
                double lim = per ? CorrPerishDays : CorrDurableDays;
                for (int m = 0; m < NM; m++)
                {
                    nodes.Clear();
                    for (int l = 0; l < nl; l++) { int mk; if (x.LandMk.TryGetValue(x.Lands[l], out mk) && mk == m && (SL[l * NG + g] > 1.0 || DL[l * NG + g] > 1.0)) nodes.Add(l); }
                    int n = nodes.Count;
                    if (n < 1) continue;
                    var ex = new double[n]; var exM = new double[n];
                    for (int i = 0; i < n; i++) { int l = nodes[i]; ex[i] = SL[l * NG + g] - DL[l * NG + g]; }
                    double zl = zlG[m * NG + g];
                    // budzet kasy importera (przed D i przewoz bez sufitu po D): najwyzej udzial towaru x 15% kasy krainy na dobe (towar wezla celu)
                    for (int i = 0; i < n; i++)
                    {
                        int l = nodes[i];
                        double capN = zl > 0 ? defSh[l * NG + g] * (cr != null ? cr.ImpCap[l] : CorrCofferDay * gold[l]) / zl : 0.0;
                        exM[i] = ex[i] < 0 ? -Math.Min(-ex[i], Math.Max(0.0, capN)) : ex[i];
                    }
                    if (cr != null) CreditNodes(x, cr, nodes, g);
                    // luki (zrodlo -> cel): doby bramy, przepustowosc (przed D - polowa udzialu kasy importera na towar w drodze; po D - dowoz KS-H)
                    var cost = new double[n * n]; var cap = new double[n * n]; var capM = new double[n * n];
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            cost[i * n + j] = -1; cap[i * n + j] = 0; capM[i * n + j] = 0;
                            if (i == j || ex[i] <= 0 || ex[j] >= 0) continue;
                            int li = nodes[i], lj = nodes[j], k = li * nl + lj;
                            float dd = x.GwDays[k];
                            double arcPeace = 1.0;                             // KS-R5 (Ks Corridors War Cut): udzial kasy eksportera z miast poza wojna z celami luku
                            if (cr != null)
                            {
                                float ld; bool ls, lw; double lpg;
                                if (CreditLink(x, cr, i, j, out ld, out ls, out lw, out lpg)) { dd = ld; cr.ArcSea[i * n + j] = ls; cr.ArcSet[i * n + j] = true; if (inp.CorrWarCut) arcPeace = lpg; }
                                else if (lw && inp.CorrWarCut)
                                {
                                    // KS-R5 ceny-r5 (klucz Ks Corridors War Cut): kazde miasto eksportera z nadwyzka jest w wojnie z kazdym miastem importera z brakiem - kupcy KS-H
                                    // tej drogi nie pojada; dotad Plan bral brame innej pary miast (spoza nadwyzki) z kasa eksportera w wojnie (fin83: reach>aserai 967 szt./d przy wojnie
                                    // The Reach - Dorne, dowiezione 0)
                                    x.WarCutN++;
                                    if (x.WarCutL.Count < 24) x.WarCutL.Add(x.Lands[li] + ">" + x.Lands[lj] + " " + KsTab.Goods[g]);
                                    continue;
                                }
                            }
                            if (!(dd >= 0f) || dd > lim) continue;
                            double cm = zl > 0 ? defSh[lj * NG + g] * 0.5 * gold[lj] / (zl * Math.Max(1.0, dd)) : 0.0, c = cm;
                            if (cr != null && cr.Src[i].Length > 0) { cm = zl > 0 ? arcPeace * DepK(inp) * cr.SupSh[li * NG + g] * cr.ExpGold[i] / (zl * (Math.Max(1.0, dd) + CreditPayDays)) : 0.0; c = cm; }   // KS-R5: x zadatek importera (DepK), x kasa miast poza wojna (arcPeace)
                            if (inp.AfterD)
                            {
                                double del = 0;
                                if (inp.CorrDelivered != null) inp.CorrDelivered.TryGetValue(x.Lands[li] + "|" + x.Lands[lj] + "|" + g.ToString(CultureInfo.InvariantCulture), out del);
                                c = Math.Max(1.2 * del, 20.0) * kgp;
                            }
                            cost[i * n + j] = Math.Max(1.0, dd);
                            if (c > 0) cap[i * n + j] = c;
                            if (cm > 0) capM[i * n + j] = cm;
                        }
                    if (cr != null)
                        for (int i = 0; i < n; i++)
                        {
                            // H7: eksporter niesie na kredyt najwyzej udzial towaru x 0.5 x kasa 3 miast z nadwyzka / (najkrotsze doby luku + 3) na dobe (wezel, nie luk)
                            if (!(ex[i] > 0) || cr.Src[i].Length == 0) continue;
                            double dmin = double.MaxValue;
                            for (int j = 0; j < n; j++) { double cc = cost[i * n + j]; if (cc > 0 && cc < dmin) dmin = cc; }
                            if (dmin == double.MaxValue) continue;
                            double capE = zl > 0 ? DepK(inp) * cr.SupSh[nodes[i] * NG + g] * cr.ExpGold[i] / (zl * (dmin + CreditPayDays)) : 0.0;   // KS-R5: x zadatek importera (DepK)
                            exM[i] = Math.Min(ex[i], Math.Max(0.0, capE));
                        }
                    var flow = KsCorridors.Transport(n, inp.AfterD ? ex : exM, cost, cap);
                    if (inp.AfterD)
                    {
                        // NKS-24 (recenzja runda 3): przewoz bez sufitu dowozu KS-H (budzet kasy jak przed D) - wzorzec dowozu do porownania z tym, co KS-H naprawde dowiozl
                        var free = KsCorridors.Transport(n, exM, cost, capM);
                        for (int i = 0; i < n; i++)
                            for (int j = 0; j < n; j++)
                            {
                                double f = free[i * n + j]; if (!(f > 1e-9)) continue;
                                int li = nodes[i], lj = nodes[j];
                                R.LandFlowFree.Add(new KsFlow { From = x.Lands[li], To = x.Lands[lj], G = g, M = m, Pcs = f / kgp, Days = cost[i * n + j], Sea = cr != null && cr.ArcSet[i * n + j] ? cr.ArcSea[i * n + j] : x.GwSea[li * nl + lj] });
                            }
                    }
                    bool noGrad = side || p[m * NG + g] <= 2.0 * PLo;
                    // gradient surowy
                    var inF = new double[n]; var inT = new double[n]; var outF = new double[n];
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            double f = flow[i * n + j];
                            if (!(f > 1e-9)) continue;
                            int li = nodes[i], lj = nodes[j], k = li * nl + lj;
                            double dd = cost[i * n + j];
                            bool sea = cr != null && cr.ArcSet[i * n + j] ? cr.ArcSea[i * n + j] : x.GwSea[k];
                            inF[j] += f; inT[j] += f * (sea ? CorrTauSea : CorrTauLand) * dd; outF[i] += f;
                            int o = m * NG + g;
                            x.FlowPcs[o] += f / kgp; x.FlowDays[o] += f / kgp * dd; if (sea) x.FlowSea[o] += f / kgp;
                            R.LandFlow.Add(new KsFlow { From = x.Lands[li], To = x.Lands[lj], G = g, M = m, Pcs = f / kgp, Days = dd, Sea = sea });
                        }
                    for (int i = 0; i < n; i++)
                    {
                        int l = nodes[i]; double sl = SL[l * NG + g], dl = DL[l * NG + g];
                        double gr = 1.0;
                        if (ex[i] < 0)
                        {
                            double r = -ex[i] - inF[i];
                            // tryb "kraina" (brak bez zrodla w zasiegu albo bez kasy na dowoz > 5% popytu - kraina liczy sie sama, cena 1.5) tylko dla zboza (rozdz. 5 projektu);
                            // psujacego sie bez zrodla w 7 dobach nikt nie dowiezie - regula zasiegu zdejmuje jego popyt zaplacony (drabina zastepuje)
                            if (!per && g == KsTab.GGrain && dl > 50.0 && r > 0.05 * dl) { gr = CorrMaxG; kraina.Add((m == 0 ? "W " : "E ") + x.Lands[l] + " " + KsTab.Goods[g] + " " + (100.0 * r / dl).ToString("0", CultureInfo.InvariantCulture) + "%"); }
                            else if (inF[i] > 1e-9) gr = Math.Min(CorrMaxG, 1.0 + CorrMargin + inT[i] / inF[i]);
                        }
                        else if (ex[i] > 0 && sl > 50.0 && !per)
                        {
                            double u = ex[i] - outF[i];
                            if (u > 0.05 * sl)
                            {
                                gr = Math.Max(CorrMinG, 1.0 - u / sl);
                                // recenzja runda 3 (NKS-23): kraina z nadwyzka bez odbiorcy (wojna albo poza zasiegiem) - lista w linii korytarzy
                                R.NoOutlet.Add((m == 0 ? "W " : "E ") + x.Lands[l] + " " + KsTab.Goods[g] + " " + (100.0 * u / sl).ToString("0", CultureInfo.InvariantCulture) + "%");
                            }
                        }
                        graw[l * NG + g] = noGrad ? 1.0 : gr;
                    }
                }
            }
            R.Kraina.AddRange(kraina);
            // KS-R3 runda 3 symulatora: psujace sie na poziomie miast (przewoz miast w 7 dobach, gradient miasta, niedowiezione do reguly zasiegu)
            double[] tgraw = null;
            try { tgraw = TownPerish(x, p, prod); } catch (Exception te) { tgraw = null; x.TownPerishDone = false; R.Kraina.Add("blad przewozu miast " + te.GetType().Name); }
            // tlumienie i gradient skuteczny
            double dmax = 0, gmax = 1.0; int dbig = 0;
            if (inp.Corridors)
            {
                for (int l = 0; l < nl; l++)
                    foreach (int g in Food)
                    {
                        int i = l * NG + g;
                        double nv = GradDamp * x.GradBase[i] + (1.0 - GradDamp) * graw[i];
                        nv = Math.Max(CorrMinG, Math.Min(CorrMaxG, nv));
                        double dd = Math.Abs(nv - x.GradBase[i]) / Math.Max(1e-9, x.GradBase[i]);
                        if (dd > dmax) dmax = dd;
                        if (dd > 0.05) dbig++;
                        x.GradBase[i] = nv;
                        if (Math.Abs(nv - 1.0) > Math.Abs(gmax - 1.0)) gmax = nv;
                    }
                // KS-R3 runda 3 symulatora: gradient miast psujacych sie - to samo tlumienie (0.5 poprzedni + 0.5 nowy); blizej 1 niz 0.5% - 1 (miasto bez dodatku)
                x.TGMin = x.TGMax = 1.0; x.TGN = 0;
                if (tgraw != null)
                    for (int t = 0; t < x.NT; t++)
                        foreach (int g in Food)
                        {
                            if (!PerishG[g]) continue;
                            int i = t * NG + g;
                            double nv = Math.Max(CorrMinG, Math.Min(CorrMaxG, 0.5 * x.TGradBase[i] + 0.5 * tgraw[i]));
                            if (Math.Abs(nv - 1.0) < 0.005) nv = 1.0;
                            double dd = Math.Abs(nv - x.TGradBase[i]) / Math.Max(1e-9, x.TGradBase[i]);
                            if (dd > dmax) dmax = dd;
                            if (dd > 0.05) dbig++;
                            x.TGradBase[i] = nv;
                            if (nv != 1.0) { x.TGN++; if (nv < x.TGMin) x.TGMin = nv; if (nv > x.TGMax) x.TGMax = nv; }
                            if (Math.Abs(nv - 1.0) > Math.Abs(gmax - 1.0)) gmax = nv;
                        }
                RefreshGradEff(x);
                var gd = new Dictionary<string, double[]>(StringComparer.Ordinal);
                for (int l = 0; l < nl; l++)
                {
                    bool any = false; foreach (int g in Food) if (x.GradBase[l * NG + g] != 1.0) { any = true; break; }
                    if (!any) continue;
                    var a = new double[NG]; for (int g = 0; g < NG; g++) a[g] = FoodMask[g] ? x.GradBase[l * NG + g] : 1.0;
                    gd[x.Lands[l]] = a;
                }
                for (int t = 0; t < x.NT; t++)
                {
                    bool any = false; foreach (int g in Food) if (PerishG[g] && x.TGradBase[t * NG + g] != 1.0) { any = true; break; }
                    if (!any) continue;
                    var a = new double[NG]; for (int g = 0; g < NG; g++) a[g] = PerishG[g] && FoodMask[g] ? x.TGradBase[t * NG + g] : 1.0;
                    gd["@" + inp.Towns[t].Id] = a;
                }
                R.Grad = gd;
            }
            R.GradDMax = dmax; R.GradMax = gmax; R.GradDBig = dbig;
            // regula zasiegu psujacych sie - przed krokiem 8 (Solve, recenzja runda 3)
            R.CorrMs += sw.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// KS-R3 (port symulatora 17.10, runda 3): PSUJACE SIE NA POZIOMIE MIAST. Krainy zbieraja podaz i popyt calej kultury, a towar psujacy sie jedzie najwyzej 7 dob:
        /// ryby Westeros lezaly na Arbor (okreg winnicy Ramsport z 300 tys. ludzi - polowa ryb Westeros), Essos - na Nowym Ghis, a Plan widzial "nadwyzke Reach"
        /// wywozona przez najlepsza brame krainy (Highgarden - Dorzecze 4 doby), ktorej z Arbor nikt nie dowiezie (symulator: 61% ryb, 55-64% owocow, 34% winogron,
        /// 32-51% masla rynku bez odbiorcy w 7 dobach). Dla kazdego towaru psujacego sie i rynku: podaz okregow miasta targowego (z polami mieszczan) i popyt miasta
        /// z jego okregami (potrzeby + 1:1 wedlug ludzi - to, co okreg zjada sam, jest po obu stronach); problem przewozu miast (kolejne najkrotsze sciezki): luk
        /// najwyzej 7 dob wozem (tabela dob KS-H), nie w wojnie, przepustowosc z kasy miasta celu (polowa kasy 28 dob na towar w drodze - jak przed D krok 5).
        /// GRADIENT MIASTA (cena towaru w dochodzie bloku okregow miasta, razem z gradientem krainy): nadwyzka bez odbiorcy w 7 dobach (> 5% podazy miasta) -
        /// 1 - stojace / podaz, najmniej 0.6 (rece odchodza od towaru, ktorego nikt nie kupi); dowoz - 1.03 + tau x srednie doby; brak bez dowozu (> 5% popytu)
        /// przy wlasnej podazy >= 5% popytu - 1 + brak / popyt, najwyzej 1.5 (rece miasta, ktore umie to robic, ida do towaru). Bez gradientu: towary uboczne
        /// (ExcessSkip) i cena rynku na podlodze (jak krok 5 krain). NIEDOWIEZIONE (czesc popytu bez dowozu) - do reguly zasiegu (TUnmet).
        /// Wydajnosc bez zmian - zmienia sie tylko liczba rak przy towarze.
        /// </summary>
        private static double[] TownPerish(Ctx x, double[] p, Dictionary<string, double[]> prod)
        {
            var inp = x.In;
            int nt = x.NT, NG = KsTab.NG;
            x.TownPerishDone = false;
            if (nt <= 0 || x.TGradBase == null || inp.TownDays == null || inp.TownDays.Length != nt * nt) return null;
            var sup = new double[nt * NG]; var dem = new double[nt * NG]; var tmp = new double[NG];
            double wpop = Math.Max(1.0, x.WPop);
            // recenzja runda 3: popyt zaplacony (potrzeby po kroku 8) + odbior panow i dworow w miescie + reszta 1:1 wedlug ludzi (jak krainy)
            var rest = OneRest(x);
            foreach (var d in x.D)
            {
                int t = d.CorrTown; if (t < 0 || t >= nt) continue;
                double[] a;
                if (prod.TryGetValue(d.Id, out a)) foreach (int g in Food) if (PerishG[g]) sup[t * NG + g] += a[g];
                Array.Clear(tmp, 0, NG);
                CivilGoods(d.Need, tmp);
                foreach (int g in Food) if (PerishG[g]) dem[t * NG + g] += tmp[g] + rest[g] * d.People / wpop;
            }
            for (int t = 0; t < nt; t++)
            {
                var ts = inp.Towns[t];
                Array.Clear(tmp, 0, NG);
                CivilGoods(ts.Need, tmp);
                TownOneAdd(x, ts.Id, tmp);
                foreach (int g in Food) if (PerishG[g]) dem[t * NG + g] += tmp[g] + rest[g] * ts.People / wpop;
            }
            // recenzja runda 3 (wazne): jeden budzet kasy miasta celu na wszystkie psujace sie (udzial wedlug wartosci braku towaru), jak krainy
            var tsh = new double[nt * NG];
            for (int t = 0; t < nt; t++)
            {
                int mt = inp.Towns[t].Market == 0 ? 0 : 1; double tot = 0;
                foreach (int g in Food) { if (!PerishG[g]) continue; double e = dem[t * NG + g] - sup[t * NG + g]; if (e > 0) { tsh[t * NG + g] = e * ZlPerKg(inp, KsTab.Goods[g], StartIndex(KsTab.Goods[g], p, mt)); tot += tsh[t * NG + g]; } }
                foreach (int g in Food) if (PerishG[g]) tsh[t * NG + g] = tot > 0 ? tsh[t * NG + g] / tot : 0.0;
            }
            var graw = new double[nt * NG]; for (int i = 0; i < graw.Length; i++) graw[i] = 1.0;
            Array.Clear(x.TUnmet, 0, x.TUnmet.Length);
            Array.Clear(x.TStuck, 0, x.TStuck.Length); Array.Clear(x.TSup, 0, x.TSup.Length); Array.Clear(x.TMoved, 0, x.TMoved.Length); Array.Clear(x.TShort, 0, x.TShort.Length); Array.Clear(x.TDem, 0, x.TDem.Length);
            var nodes = new List<int>(nt);
            foreach (int g in Food)
            {
                if (!PerishG[g]) continue;
                for (int m = 0; m < NM; m++)
                {
                    nodes.Clear();
                    for (int t = 0; t < nt; t++) if (inp.Towns[t].Market == m && (sup[t * NG + g] > 1.0 || dem[t * NG + g] > 1.0)) nodes.Add(t);
                    int n = nodes.Count;
                    if (n < 1) continue;
                    var ex = new double[n]; var exM = new double[n];
                    for (int i = 0; i < n; i++) ex[i] = sup[nodes[i] * NG + g] - dem[nodes[i] * NG + g];
                    double idx = StartIndex(KsTab.Goods[g], p, m), zl = ZlPerKg(inp, KsTab.Goods[g], idx);
                    for (int i = 0; i < n; i++)
                    {
                        int tj = nodes[i];
                        double capN = inp.TownGold28 != null && tj < inp.TownGold28.Length && zl > 0 ? tsh[tj * NG + g] * CorrCofferDay * CreditGold(inp, tj) / zl : double.PositiveInfinity;
                        exM[i] = ex[i] < 0 ? -Math.Min(-ex[i], capN) : ex[i];
                    }
                    var cost = new double[n * n]; var cap = new double[n * n];
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            int k = i * n + j;
                            cost[k] = -1; cap[k] = 0;
                            if (i == j || ex[i] <= 0 || ex[j] >= 0) continue;
                            int ti = nodes[i], tj = nodes[j];
                            float dd = inp.TownDays[ti * nt + tj];
                            if (!(dd >= 0f) || dd > CorrPerishDays) continue;
                            if (inp.TownWar != null && inp.TownWar[ti * nt + tj]) continue;
                            double c = inp.TownGold28 != null && tj < inp.TownGold28.Length && zl > 0 ? tsh[tj * NG + g] * 0.5 * Math.Max(0.0, inp.TownGold28[tj]) / (zl * Math.Max(1.0, dd)) : 1e12;
                            // KS-R4 r4-kupcy H7: eksporter niesie towar na kredyt - 0.5 x kasa 28 dob miasta z nadwyzka / (doby + 3), dzielone na psujace sie wedlug wartosci nadwyzki
                            if (CreditOn(inp) && inp.TownGold28 != null && ti < inp.TownGold28.Length && zl > 0)
                                c = DepK(inp) * TownSupShare(sup, dem, ti, g, NG) * 0.5 * Math.Max(0.0, inp.TownGold28[ti]) / (zl * (Math.Max(1.0, dd) + 3.0));   // KS-R5: x zadatek importera (DepK)
                            if (!(c > 0)) continue;
                            cost[k] = Math.Max(1.0, dd); cap[k] = c;
                        }
                    var flow = KsCorridors.Transport(n, exM, cost, cap);
                    var inF = new double[n]; var inT = new double[n]; var outF = new double[n];
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            double f = flow[i * n + j];
                            if (!(f > 1e-9)) continue;
                            int ti = nodes[i], tj = nodes[j];
                            bool sea = inp.TownSea != null && inp.TownSea[ti * nt + tj];
                            inF[j] += f; inT[j] += f * (sea ? CorrTauSea : CorrTauLand) * cost[i * n + j]; outF[i] += f;
                        }
                    bool ng = !TownGrad || ExcessSkip(g) || p[m * NG + g] <= 2.0 * PLo;
                    int o = m * NG + g;
                    for (int i = 0; i < n; i++)
                    {
                        int t = nodes[i]; double s = sup[t * NG + g], dm = dem[t * NG + g], gr = 1.0;
                        x.TSup[o] += s; x.TDem[o] += dm;
                        if (ex[i] > 0)
                        {
                            double u = Math.Max(0.0, ex[i] - outF[i]);
                            x.TStuck[o] += u; x.TMoved[o] += outF[i];
                            if (!ng && TownExport && s > 20.0 && u > 0.05 * s) gr = Math.Max(Math.Max(CorrMinG, TownExportMin), 1.0 - u / s);
                        }
                        else if (ex[i] < 0)
                        {
                            double r = Math.Max(0.0, -ex[i] - inF[i]);
                            x.TUnmet[t * NG + g] = dm > 0 ? r / dm : 0.0;
                            x.TShort[o] += r;
                            if (!ng)
                            {
                                if (inF[i] > 1e-9) gr = Math.Min(CorrMaxG, 1.0 + CorrMargin + inT[i] / inF[i]);
                                if (TownImport && dm > 20.0 && r > 0.05 * dm && s >= 0.05 * dm) gr = Math.Min(CorrMaxG, Math.Max(gr, 1.0 + r / dm));
                            }
                        }
                        graw[t * NG + g] = gr;
                    }
                }
            }
            x.TownPerishDone = true;
            return graw;
        }

        /// <summary>Pozycje koszyka ZC4 towaru psujacego sie KsTab g (mieso - mieso i mieso wieloryba).</summary>
        private static int[] BasketOfPerish(int g)
        {
            switch (g)
            {
                case KsTab.GFish: return new[] { KsBasket.Fish };
                case KsTab.GMeat: return new[] { KsBasket.Meat, KsBasket.Whale };
                case KsTab.GButter: return new[] { KsBasket.Butter };
                case KsTab.GEgg: return new[] { KsBasket.Egg };
                case KsTab.GApple: return new[] { KsBasket.Apple };
                case KsTab.GCarrot: return new[] { KsBasket.Carrot };
                case KsTab.GOrange: return new[] { KsBasket.Orange };
                case KsTab.GGrape: return new[] { KsBasket.Grape };
                case KsTab.GDate: return new[] { KsBasket.Date };
                default: return null;
            }
        }

        /// <summary>Potrzeba towaru psujacego sie g w potrzebach osady (kg/d; swiezy towar - bez wina).</summary>
        private static double PerishNeed(double[] n, int g)
        {
            switch (g)
            {
                case KsTab.GFish: return n[KsTab.NFish];
                case KsTab.GMeat: return n[KsTab.NMeat];
                case KsTab.GButter: return n[KsTab.NButter];
                case KsTab.GEgg: return n[KsTab.NEgg];
                case KsTab.GApple: return n[KsTab.NApple];
                case KsTab.GCarrot: return n[KsTab.NCarrot];
                case KsTab.GOrange: return n[KsTab.NOrange];
                case KsTab.GGrape: return n[KsTab.NGrape];
                case KsTab.GDate: return n[KsTab.NDate];
                default: return 0.0;
            }
        }

        /// <summary>REGULA ZASIEGU (krok 5): miasto t i towar psujacy sie - okregi miasta (wsie z tym miastem targowym i pola mieszczan) daja mniej niz 5% popytu
        /// miasta i jego wsi (z prawdziwym odbiorem panow i dworow), a zadne miasto tego rynku z nadwyzka (okregi wiecej niz popyt) nie jest w 7 dobach wozem
        /// (nie w wojnie) - pozycje koszyka tego towaru wyciete w miescie i jego wsiach (krok 8: czesc zaplacona 0, drabina zastepuje).</summary>
        private static void ReachRule(Ctx x, Dictionary<string, double[]> prod)
        {
            var inp = x.In; var R = x.R;
            int nt = inp.Towns.Count, NG = KsTab.NG;
            var sup = new double[nt * NG]; var dem = new double[nt * NG];
            var byTown = new List<KsDistrict>[nt];
            foreach (var d in x.D)
            {
                int t; if (inp.DistTown == null || !inp.DistTown.TryGetValue(d.Id, out t) || t < 0 || t >= nt) continue;
                (byTown[t] ?? (byTown[t] = new List<KsDistrict>())).Add(d);
                double[] a; prod.TryGetValue(d.Id, out a);
                for (int g = 0; g < NG; g++)
                {
                    if (!PerishG[g]) continue;
                    if (a != null) sup[t * NG + g] += a[g];
                    dem[t * NG + g] += PerishNeed(d.Need, g);
                }
            }
            for (int t = 0; t < nt; t++)
            {
                var ts = inp.Towns[t];
                double[] one = null; if (inp.TownOne != null) inp.TownOne.TryGetValue(ts.Id, out one);
                for (int g = 0; g < NG; g++)
                {
                    if (!PerishG[g]) continue;
                    dem[t * NG + g] += PerishNeed(ts.Need, g) + (one != null && g < one.Length ? Math.Max(0.0, one[g]) : 0.0);
                }
            }
            // recenzja runda 3 (drobne): podaz spoza tabeli (lodzie rybackie NavalDLC - ExoLand krainy) w podazy miasta wedlug ludzi okregow miasta (jak Plan rozklada lodzie);
            // pola mieszczan sa okregami miasta (byTown) - juz w podazy
            if (x.TownLand != null && inp.ExoLand.Count > 0)
                for (int t = 0; t < nt; t++)
                {
                    int l = x.TownLand[t]; if (l < 0) continue;
                    double[] exo; double lp;
                    if (!inp.ExoLand.TryGetValue(x.Lands[l], out exo) || exo == null || !x.LPop.TryGetValue(x.Lands[l], out lp) || !(lp > 0)) continue;
                    double pp = inp.Towns[t].People; if (byTown[t] != null) foreach (var d in byTown[t]) pp += d.People;
                    double sh = Math.Min(1.0, pp / lp);
                    for (int g = 0; g < NG; g++) if (PerishG[g] && exo[g] > 0) sup[t * NG + g] += exo[g] * sh;
                }
            x.ReachT = new Dictionary<string, bool[]>(StringComparer.Ordinal); x.ReachD = new Dictionary<string, bool[]>(StringComparer.Ordinal);
            R.Unreached.Clear();
            for (int t = 0; t < nt; t++)
            {
                var ts = inp.Towns[t];
                for (int g = 0; g < NG; g++)
                {
                    if (!PerishG[g]) continue;
                    double dm = dem[t * NG + g];
                    if (!(dm > 1.0) || sup[t * NG + g] >= 0.05 * dm) continue;
                    bool ok = false;
                    // KS-R3 runda 3 symulatora: po przewozie miast (TownPerish) - wyciete, gdy ponad polowa popytu miasta zostaje bez dowozu w 7 dobach (dotad: jakiekolwiek
                    // miasto z nadwyzka w 7 dobach - jedno male miasto rybackie "zasilalo" kilkanascie miast w glebi ladu); bez przewozu miast - jak dotad
                    if (TownReach && x.TUnmet != null && x.TownPerishDone) ok = x.TUnmet[t * NG + g] <= 0.5;
                    else
                    for (int u = 0; u < nt && !ok; u++)
                    {
                        if (u == t || inp.Towns[u].Market != ts.Market || !(sup[u * NG + g] > dem[u * NG + g])) continue;
                        float dd = inp.TownDays != null && inp.TownDays.Length == nt * nt ? inp.TownDays[u * nt + t] : -1f;
                        if (dd >= 0f && dd <= CorrPerishDays && !(inp.TownWar != null && inp.TownWar[u * nt + t])) ok = true;
                    }
                    if (ok) continue;
                    var zs = BasketOfPerish(g); if (zs == null) continue;
                    bool[] c; if (!x.ReachT.TryGetValue(ts.Id, out c)) { c = new bool[KsBasket.N]; x.ReachT[ts.Id] = c; }
                    foreach (int z in zs) c[z] = true;
                    if (byTown[t] != null)
                        foreach (var d in byTown[t])
                        {
                            if (d.TownFields) continue;
                            bool[] cd; if (!x.ReachD.TryGetValue(d.Id, out cd)) { cd = new bool[KsBasket.N]; x.ReachD[d.Id] = cd; }
                            foreach (int z in zs) cd[z] = true;
                        }
                    R.Unreached.Add(ts.Id + " " + KsTab.Goods[g]);
                }
            }
        }

        // ------------------------------------------------------------ KS-R4 r4-kupcy H7 (= K1): korytarze z modelem kredytu kupcow
        internal const double CreditFloorShare = 0.5, CreditExpShare = 0.5, CreditPayDays = 3.0; internal const int CreditTopSrc = 3;

        // KS-R5 ceny-r5: luki odciete wojna w ostatniej rundzie kroku 5 (linia korytarzy)
        private sealed partial class Ctx { public int WarCutN; public readonly List<string> WarCutL = new List<string>(); }

        /// <summary>KS-R5 ceny-r5 (klucz Ks Consign Deposit): zadatek kupcow importera przy wysylce (udzial wartosci lotu) - kredyt eksportera niesie tylko reszte, wiec
        /// ta sama kasa eksportera unosi 1 / (1 - udzial) wiecej towaru w drodze (udzial najwyzej 0.9); bez zadatku (CorrDepShare 0) - 1 (H7 bit w bit).</summary>
        private static double DepK(KsInput inp)
        {
            double s = inp != null ? inp.CorrDepShare : 0.0;
            if (!(s > 0)) return 1.0;
            return 1.0 / (1.0 - Math.Min(0.9, s));
        }

        private sealed class CreditCtx
        {
            public double[] ImpCap;                     // [nl] zl/d: 15% x max(0, kasa 28 dob - 0.5 x zapas) miast krainy
            public double[] TSup, TDem;                 // [nt * NG] podaz okregow miasta i popyt miasta z okregami (jedzenie)
            public double[] SupSh;                      // [nl * NG] udzial towaru w wartosci nadwyzki krainy
            public List<int>[] TownsOf;                 // [nl] miasta krainy
            public int[][] Src; public int[][] Dst; public double[][] DstW; public double[] ExpGold;   // [wezel] miasta z nadwyzka (top 3), z brakiem (waga), 0.5 x kasa top 3
            public bool[] ArcSea, ArcSet;               // [n * n] luk: morzem, ustawiony z kredytu
        }

        private static bool CreditOn(KsInput inp)
        {
            int nt = inp.Towns != null ? inp.Towns.Count : 0;
            return inp.CorrCredit && nt > 0 && inp.TownRes != null && inp.TownRes.Length == nt && inp.TownGold28 != null && inp.TownGold28.Length >= nt && inp.TownDays != null && inp.TownDays.Length == nt * nt;
        }

        /// <summary>H7: kasa miasta, z ktorej placi przy dostawie (bez H7 - kasa 28 dob, jak dotad): max(0, kasa 28 dob - 0.5 x zapas kupcow).</summary>
        private static double CreditGold(KsInput inp, int t)
        {
            double g28 = inp.TownGold28 != null && t < inp.TownGold28.Length ? Math.Max(0.0, inp.TownGold28[t]) : 0.0;
            if (!CreditOn(inp)) return g28;
            return Math.Max(0.0, g28 - CreditFloorShare * Math.Max(0.0, inp.TownRes[t]));
        }

        /// <summary>H7: udzial towaru psujacego sie g w wartosci nadwyzki miasta (bez cen - wedlug ilosci kg; tylko psujace sie z nadwyzka).</summary>
        private static double TownSupShare(double[] sup, double[] dem, int t, int g, int NG)
        {
            double e = sup[t * NG + g] - dem[t * NG + g]; if (!(e > 0)) return 0.0;
            double tot = 0; foreach (int h in Food) { if (!PerishG[h]) continue; double x = sup[t * NG + h] - dem[t * NG + h]; if (x > 0) tot += x; }
            return tot > 0 ? e / tot : 0.0;
        }

        /// <summary>H7: przygotowanie kroku 5 z kredytem - przepustowosc importerow, podaz i popyt jedzenia miast, udzialy nadwyzki krain. Bez H7 - null (jak dotad).</summary>
        private static CreditCtx CreditPrep(Ctx x, Dictionary<string, double[]> prod, double[] rest, double wpop, double[] SL, double[] DL, double[] zlG)
        {
            var inp = x.In;
            if (!CreditOn(inp)) return null;
            int nl = x.NL, nt = inp.Towns.Count, NG = KsTab.NG;
            var c = new CreditCtx { ImpCap = new double[nl], TSup = new double[nt * NG], TDem = new double[nt * NG], SupSh = new double[nl * NG], TownsOf = new List<int>[nl] };
            for (int l = 0; l < nl; l++) c.TownsOf[l] = new List<int>();
            for (int t = 0; t < nt; t++)
            {
                int l = x.TownLand[t]; if (l < 0) continue;
                c.TownsOf[l].Add(t);
                c.ImpCap[l] += CorrCofferDay * CreditGold(inp, t);
            }
            var tmp = new double[NG];
            foreach (var d in x.D)
            {
                int t = d.CorrTown; if (t < 0 || t >= nt) continue;
                double[] a;
                if (prod.TryGetValue(d.Id, out a)) foreach (int g in Food) c.TSup[t * NG + g] += a[g];
                Array.Clear(tmp, 0, NG);
                CivilGoods(d.Need, tmp);
                foreach (int g in Food) c.TDem[t * NG + g] += tmp[g] + rest[g] * d.People / wpop;
            }
            for (int t = 0; t < nt; t++)
            {
                var ts = inp.Towns[t];
                Array.Clear(tmp, 0, NG);
                CivilGoods(ts.Need, tmp);
                TownOneAdd(x, ts.Id, tmp);
                foreach (int g in Food) c.TDem[t * NG + g] += tmp[g] + rest[g] * ts.People / wpop;
            }
            for (int l = 0; l < nl; l++)
            {
                int mk; if (!x.LandMk.TryGetValue(x.Lands[l], out mk)) mk = 0;
                double tot = 0;
                foreach (int g in Food) { double e = SL[l * NG + g] - DL[l * NG + g]; if (e > 0) { c.SupSh[l * NG + g] = e * zlG[mk * NG + g]; tot += c.SupSh[l * NG + g]; } }
                foreach (int g in Food) c.SupSh[l * NG + g] = tot > 0 ? c.SupSh[l * NG + g] / tot : 0.0;
            }
            return c;
        }

        /// <summary>H7: wezly problemu przewozu towaru g - miasta krainy z nadwyzka (3 najwieksze) i z brakiem (waga = brak; bez braku w zadnym miescie - popyt), kasa eksportera.</summary>
        private static void CreditNodes(Ctx x, CreditCtx c, List<int> nodes, int g)
        {
            var inp = x.In; int n = nodes.Count, NG = KsTab.NG;
            c.Src = new int[n][]; c.Dst = new int[n][]; c.DstW = new double[n][]; c.ExpGold = new double[n];
            c.ArcSea = new bool[n * n]; c.ArcSet = new bool[n * n];
            var sl = new List<KeyValuePair<double, int>>(); var dl = new List<KeyValuePair<double, int>>();
            for (int i = 0; i < n; i++)
            {
                sl.Clear(); dl.Clear();
                foreach (int t in c.TownsOf[nodes[i]])
                {
                    double e = c.TSup[t * NG + g] - c.TDem[t * NG + g];
                    if (e > 1e-9) sl.Add(new KeyValuePair<double, int>(e, t)); else if (e < -1e-9) dl.Add(new KeyValuePair<double, int>(-e, t));
                }
                sl.Sort((a, b) => a.Key != b.Key ? b.Key.CompareTo(a.Key) : a.Value.CompareTo(b.Value));
                var src = new int[Math.Min(CreditTopSrc, sl.Count)];
                double eg = 0;
                for (int k = 0; k < src.Length; k++) { src[k] = sl[k].Value; eg += CreditExpShare * Math.Max(0.0, inp.TownGold28[src[k]]); }
                c.Src[i] = src; c.ExpGold[i] = eg;
                if (dl.Count == 0) foreach (int t in c.TownsOf[nodes[i]]) { double dm = c.TDem[t * NG + g]; if (dm > 1e-9) dl.Add(new KeyValuePair<double, int>(dm, t)); }
                c.Dst[i] = new int[dl.Count]; c.DstW[i] = new double[dl.Count];
                for (int k = 0; k < dl.Count; k++) { c.Dst[i][k] = dl[k].Value; c.DstW[i][k] = dl[k].Key; }
            }
        }

        /// <summary>H7: doby luku i -> j = srednia (waga - brak miasta importera) najlepszych dob z 3 miast eksportera z nadwyzka (tabela KS-H, bez wojny); morzem, gdy wiekszosc wagi
        /// jedzie statkiem. Brak miast albo drogi - false (brama krain, jak dotad).
        /// KS-R5 ceny-r5 (klucz Ks Corridors War Cut): warOnly - drogi sa, ale kazda para miasto zrodla z nadwyzka - miasto celu z brakiem w wojnie; udzial kasy eksportera (ExpGold) z miast z nadwyzka, ktore maja choc jedna pare z miastem importera z brakiem poza wojna
        /// (peaceGold 0..1; 1 - zadne miasto zrodla nie jest w wojnie ze wszystkimi celami).</summary>
        private static bool CreditLink(Ctx x, CreditCtx c, int i, int j, out float days, out bool sea, out bool warOnly, out double peaceGold)
        {
            days = -1f; sea = false; warOnly = false; peaceGold = 1.0;
            var inp = x.In; int nt = inp.Towns.Count;
            var src = c.Src != null ? c.Src[i] : null; var dst = c.Dst != null ? c.Dst[j] : null;
            if (src == null || src.Length == 0 || dst == null || dst.Length == 0) return false;
            double sw = 0, sd = 0, ss = 0;
            bool road = false, peace = false;                         // KS-R5 ceny-r5: czy byla para z droga i czy jakakolwiek poza wojna
            for (int q = 0; q < dst.Length; q++)
            {
                int tj = dst[q]; float best = -1f; bool bs = false;
                foreach (int ti in src)
                {
                    float d = inp.TownDays[ti * nt + tj];
                    if (!(d >= 0f)) continue;
                    road = true;
                    if (inp.TownWar != null && inp.TownWar[ti * nt + tj]) continue;
                    peace = true;
                    if (best < 0f || d < best) { best = d; bs = inp.TownSea != null && inp.TownSea[ti * nt + tj]; }
                }
                if (best < 0f) continue;
                double w = c.DstW[j][q];
                sw += w; sd += w * best; if (bs) ss += w;
            }
            if (!(sw > 0)) { warOnly = road && !peace; return false; }
            days = (float)(sd / sw); sea = ss >= 0.5 * sw;
            if (inp.TownWar != null && c.ExpGold != null && i < c.ExpGold.Length && c.ExpGold[i] > 0 && inp.TownGold28 != null)
            {
                // kasa miast zrodla, ktore moga handlowac z ktorymkolwiek miastem celu (choc jedna para z droga poza wojna)
                double g = 0;
                foreach (int ti in src)
                {
                    bool ok = false;
                    for (int q = 0; q < dst.Length && !ok; q++) { int tj = dst[q]; float d = inp.TownDays[ti * nt + tj]; if (d >= 0f && !inp.TownWar[ti * nt + tj]) ok = true; }
                    if (ok && ti < inp.TownGold28.Length) g += CreditExpShare * Math.Max(0.0, inp.TownGold28[ti]);
                }
                peaceGold = Math.Max(0.0, Math.Min(1.0, g / c.ExpGold[i]));
            }
            return true;
        }

        /// <summary>Linia "KS: korytarze planu" (tekst z watku roboczego; dobe Planu wstawia watek glowny w miejsce {DOBA}): przewoz na rynek i towar (szt./d,
        /// srednio dob, morzem %), korytarze >= 20 szt./d (kraina zrodla > kraina celu), kraina-tryb, psujace sie bez zrodla, gradient najwiekszy, czas.</summary>
        private static void CorridorLine(Ctx x)
        {
            var R = x.R; var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(6000);
            sb.Append("KS: korytarze planu doba {DOBA} | gradient krainy ").Append(x.In.Corridors ? "TAK" : "NIE (klucz Ks Land Gradient)").Append(", regula zasiegu ").Append(x.In.Reach ? "TAK" : "NIE")
              .Append(", przepustowosc ").Append(x.In.AfterD ? "po D (1.2 x dowoz KS-H, najmniej 20 szt./d)" : "przed D (budzet kasy importera: 15% kasy 28 dob na dobe, polowa kasy na towar w drodze, dzielony wedlug wartosci braku)")
              .Append(CreditOn(x.In) ? ", kredyt kupcow (H7): importer 15% kasy 28 dob ponad 0.5 x zapas na dobe, eksporter 0.5 x kasa 28 dob 3 miast z nadwyzka / (doby + 3), doby od miast z brakiem do 3 miast z nadwyzka" : "")
              .Append(CreditOn(x.In) && x.In.CorrDepShare > 0 ? ", zadatek importera (KS-R5, Ks Consign Deposit) " + x.In.CorrDepShare.ToString("0.00", inv) + " wartosci lotu - eksporter niesie x" + DepK(x.In).ToString("0.00", inv) : "")
              .Append(", popyt krain zaplacony (po kroku 8), tau lad ").Append(CorrTauLand.ToString("0.000", inv)).Append(" morze ").Append(CorrTauSea.ToString("0.0000", inv)).Append("/d").Append(", rund kroku 5 ").Append(R.CorrRounds)
              .Append(", czas ").Append(R.CorrMs.ToString("0", inv)).Append(" ms");
            for (int m = 0; m < NM; m++)
            {
                sb.Append(m == 0 ? " | Westeros:" : " | Essos:");
                bool f = true;
                foreach (int g in Food)
                {
                    int o = m * KsTab.NG + g;
                    double pc = x.FlowPcs[o];
                    if (pc < 0.5) continue;
                    sb.Append(f ? " " : ", ").Append(KsTab.Goods[g]).Append(' ').Append(pc.ToString("0", inv)).Append(" szt./d ").Append((x.FlowDays[o] / pc).ToString("0.0", inv)).Append(" dob ")
                      .Append((100.0 * x.FlowSea[o] / pc).ToString("0", inv)).Append("% morzem");
                    f = false;
                }
                if (f) sb.Append(" -");
            }
            sb.Append(" | korytarze >= 20 szt./d:");
            var big = R.LandFlow.FindAll(k => k.Pcs >= 20.0);
            big.Sort((a, b) => b.Pcs.CompareTo(a.Pcs));
            if (big.Count == 0) sb.Append(" -");
            for (int i = 0; i < big.Count && i < 80; i++)
            {
                var k = big[i];
                sb.Append(i == 0 ? " " : ", ").Append(k.From).Append('>').Append(k.To).Append(' ').Append(KsTab.Goods[k.G]).Append(' ').Append(k.Pcs.ToString("0", inv)).Append(" (").Append(k.Days.ToString("0", inv)).Append(k.Sea ? " d morzem)" : " d)");
            }
            if (big.Count > 80) sb.Append(", inne ").Append(big.Count - 80);
            // recenzja runda 3 (NKS-24): po D przewoz bez sufitu dowozu KS-H (budzet kasy importera) - wzorzec do porownania z dowozem
            if (x.In.AfterD)
            {
                sb.Append(" | korytarze bez sufitu dowozu >= 20 szt./d:");
                var fb = R.LandFlowFree.FindAll(k => k.Pcs >= 20.0);
                fb.Sort((a, b) => b.Pcs.CompareTo(a.Pcs));
                if (fb.Count == 0) sb.Append(" -");
                for (int i = 0; i < fb.Count && i < 80; i++)
                {
                    var k = fb[i];
                    sb.Append(i == 0 ? " " : ", ").Append(k.From).Append('>').Append(k.To).Append(' ').Append(KsTab.Goods[k.G]).Append(' ').Append(k.Pcs.ToString("0", inv)).Append(" (").Append(k.Days.ToString("0", inv)).Append(k.Sea ? " d morzem)" : " d)");
                }
            }
            // KS-R3 runda 3 symulatora: psujace sie na poziomie miast - stojace (nadwyzka miasta bez odbiorcy w 7 dobach) i bez dowozu (% podazy / popytu rynku), gradient miast
            if (x.TownPerishDone)
            {
                sb.Append(" | miasta (psujace sie, 7 dob):");
                for (int m = 0; m < NM; m++)
                {
                    sb.Append(m == 0 ? " Westeros" : "; Essos");
                    bool f3 = true;
                    foreach (int g in Food)
                    {
                        if (!PerishG[g]) continue;
                        int o = m * KsTab.NG + g;
                        if (!(x.TSup[o] > 1.0)) continue;
                        sb.Append(f3 ? " " : ", ").Append(KsTab.Goods[g]).Append(" stoi ").Append((100.0 * x.TStuck[o] / x.TSup[o]).ToString("0", inv)).Append("% bez dowozu ")
                          .Append((100.0 * x.TShort[o] / Math.Max(1.0, x.TDem[o])).ToString("0", inv)).Append("% popytu");
                        f3 = false;
                    }
                }
                sb.Append("; gradient miast ").Append(x.TGN).Append(" komorek, ").Append(x.TGMin.ToString("0.00", inv)).Append("-").Append(x.TGMax.ToString("0.00", inv));
            }
            // KS-R5 ceny-r5: luki krain odciete wojna (H7 - eksporter z nadwyzka w wojnie z importerem z brakiem; klucz Ks Corridors War Cut)
            if (x.In.CorrWarCut) sb.Append(" | luki odciete wojna (KS-R5): ").Append(x.WarCutN).Append(x.WarCutL.Count > 0 ? " (" + string.Join(", ", x.WarCutL.ToArray()) + (x.WarCutN > x.WarCutL.Count ? ", ..." : "") + ")" : "");
            sb.Append(" | kraina-tryb: ").Append(R.Kraina.Count > 0 ? string.Join(", ", R.Kraina.ToArray()) : "brak");
            sb.Append(" | bez zrodla: ").Append(R.Unreached.Count > 0 ? string.Join(", ", R.Unreached.ToArray()) : "brak");
            sb.Append(" | bez ujscia (nadwyzka bez odbiorcy - wojna albo poza zasiegiem): ").Append(R.NoOutlet.Count > 0 ? string.Join(", ", R.NoOutlet.ToArray()) : "brak");
            sb.Append(" | gradient max ").Append(R.GradMax.ToString("0.000", inv)).Append(" (zmiana ostatniej rundy ").Append((100 * R.GradDMax).ToString("0.0", inv)).Append("%, komorek kraina x towar ze zmiana > 5%: ").Append(R.GradDBig).Append(')');
            if (x.In.LandCut != null && x.In.LandCut.Count > 0)
            {
                sb.Append(" | ciecie nadmiaru krain:");
                bool f2 = true;
                foreach (var kv in x.In.LandCut)
                {
                    if (kv.Value == null) continue;
                    foreach (int g in Food) if (g < kv.Value.Length && kv.Value[g] < 0.999) { sb.Append(f2 ? " " : ", ").Append(kv.Key).Append(' ').Append(KsTab.Goods[g]).Append(" x").Append(kv.Value[g].ToString("0.00", inv)); f2 = false; }
                }
                if (f2) sb.Append(" -");
            }
            sb.Append('.');
            R.CorrLine = sb.ToString();
        }
    }

    /// <summary>
    /// KS-R3 krok 5: PROBLEM PRZEWOZU KRAIN (czysty C#) - najtanszy przeplyw z krain z nadwyzka do krain z brakiem po lukach dozwolonych (koszt - doby bramy,
    /// przepustowosc) metoda kolejnych najkrotszych sciezek (Bellman-Ford na sieci resztowej: zrodlo -> nadwyzki -> braki -> ujscie). Wynik: przeplyw [n * n] (kg/d).
    /// </summary>
    internal static class KsCorridors
    {
        private sealed class Arc { public int To, Rev; public double Cap, Cost; }

        /// <summary>n krain; ex[i] = podaz - popyt (kg/d); cost[i * n + j] - doby luku i -> j (-1 brak), cap - przepustowosc (kg/d). Zwraca przeplyw [i * n + j].</summary>
        internal static double[] Transport(int n, double[] ex, double[] cost, double[] cap)
        {
            var flow = new double[n * n];
            int s = n, t = n + 1, nn = n + 2;
            var g = new List<Arc>[nn];
            for (int i = 0; i < nn; i++) g[i] = new List<Arc>();
            var arcIx = new int[n * n]; for (int i = 0; i < arcIx.Length; i++) arcIx[i] = -1;
            Action<int, int, double, double> add = (a, b, c, w) =>
            {
                g[a].Add(new Arc { To = b, Rev = g[b].Count, Cap = c, Cost = w });
                g[b].Add(new Arc { To = a, Rev = g[a].Count - 1, Cap = 0, Cost = -w });
            };
            double need = 0, have = 0;
            for (int i = 0; i < n; i++)
            {
                if (ex[i] > 1e-9) { add(s, i, ex[i], 0); have += ex[i]; }
                else if (ex[i] < -1e-9) { add(i, t, -ex[i], 0); need -= ex[i]; }
            }
            if (!(have > 0) || !(need > 0)) return flow;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    double c = cost[i * n + j];
                    if (c < 0 || !(cap[i * n + j] > 0)) continue;
                    arcIx[i * n + j] = g[i].Count;
                    add(i, j, cap[i * n + j], c);
                }
            var dist = new double[nn]; var pv = new int[nn]; var pe = new int[nn]; var inq = new bool[nn];
            var q = new Queue<int>();
            for (int it = 0; it < 4 * (n * n + n + 4); it++)
            {
                for (int i = 0; i < nn; i++) { dist[i] = double.PositiveInfinity; pv[i] = -1; pe[i] = -1; inq[i] = false; }
                dist[s] = 0; q.Clear(); q.Enqueue(s); inq[s] = true;
                int guard = 0;
                while (q.Count > 0 && guard++ < 200000)
                {
                    int a = q.Dequeue(); inq[a] = false;
                    var la = g[a];
                    for (int e = 0; e < la.Count; e++)
                    {
                        var arc = la[e];
                        if (arc.Cap <= 1e-9) continue;
                        double nd = dist[a] + arc.Cost;
                        if (nd < dist[arc.To] - 1e-12)
                        {
                            dist[arc.To] = nd; pv[arc.To] = a; pe[arc.To] = e;
                            if (!inq[arc.To]) { inq[arc.To] = true; q.Enqueue(arc.To); }
                        }
                    }
                }
                if (double.IsPositiveInfinity(dist[t])) break;
                double push = double.PositiveInfinity;
                for (int v = t; v != s; v = pv[v]) { var arc = g[pv[v]][pe[v]]; if (arc.Cap < push) push = arc.Cap; }
                if (!(push > 1e-9)) break;
                for (int v = t; v != s; v = pv[v])
                {
                    var arc = g[pv[v]][pe[v]];
                    arc.Cap -= push;
                    g[arc.To][arc.Rev].Cap += push;
                }
            }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    int e = arcIx[i * n + j];
                    if (e < 0) continue;
                    var arc = g[i][e];
                    double f = cap[i * n + j] - arc.Cap;
                    if (f > 1e-9) flow[i * n + j] = f;
                }
            return flow;
        }
    }

    // KS-R5 ceny-r5: wejscie Planu - udzial zadatku kupcow importera (klucz Ks Consign Deposit z kredytem kupcow; 0 - bez zadatku, H7 jak w r4-kupcy)
    internal sealed partial class KsInput
    {
        public double CorrDepShare;
        public bool CorrWarCut;                                       // klucz Ks Corridors War Cut: luk H7 "tylko wojna" odciety (bez - brama innej pary miast, jak dotad)
    }
}
