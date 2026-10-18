using System;
using System.Collections.Generic;

namespace Armoury
{
    /// <summary>
    /// r4-koszyk (Jeff 17.10 "Tanszy koszyk", STAN-PRAC ### 72 i ### 82: "nie robmy sztucznego prawa - jak gloduja, to znaczy, ze musza jesc tansze jedzenie ... to powinna
    /// ustawiac ekonomia podazy i popytu"; PLAN-KS-RUNDA4 rozdz. 4 P9). DRABINA TANSZEGO KOSZYKA (klucz Ks Cheap Basket) - zwykle zachowanie KAZDEGO domu, ktorego
    /// nie stac na caly koszyk, w calym swiecie (jedna regula, bez krain; Essos to tylko miejsce, gdzie widac ja najbardziej):
    ///  (0) sol (mala i niezbedna);
    ///  (1) KALORIE: wszystkie kcal jedzenia koszyka (z napojami i z pozycjami, ktorych nikt nie dowiezie - regula zasiegu Planu) z NAJTANSZEGO za kcal na wlasnym targu:
    ///      ziarno (kasza), chleb, placek albo ryby (straczki sa domowe - nie ida przez targ); w grze najwyzej tyle, ile lezy na polce (najtanszy pierwszy, potem
    ///      nastepny; czego nie ma na polce - brak, ktory pokrywaja dalsze szczeble);
    ///  (1b) pozycje tansze za kcal niz podstawa, ktora zastepuja (zamiana sie oplaca) - od razu; (1c) zboze w formach koszyka (zwykly chleb i placek) do potrzeby;
    ///  (2) BIALKO (najtansze za kcal najpierw), (3) NAPOJ (najtanszy za litr najpierw), (4) RESZTA (owoce, oliwa, miod, przyprawy) po rowno -
    ///      kazda pozycja zastepuje kalorie podstawy: kupuje sie ja, a podstawy ubywa o jej kcal (najpierw najdrozszej); mieso, wino i ser - z tego, co zostaje;
    ///  (5) przedza domowa, (6) rzeczy - szczebel po szczeblu, szczebel po rowno (jak dotychczasowa drabina).
    /// Wynik: zloto na pozycje - podstawa moze dostac WIECEJ niz jej koszyk (dokupione kalorie), reszta najwyzej koszyk. Budzet stac na caly koszyk - caly koszyk, jak
    /// dotad. Nic nie powstaje: ta sama kiesa kupuje inny towar, a niewydane zostaje w kiesie.
    /// Wsie: brak pieniedzy na koszyk pokrywa juz praca domowa (krok 8 Planu) z towarem zastepczym 8b/8c - najwiecej kcal z reki na wlasnej ziemi (zboze, ryby,
    /// dziczyzna) - to jest ta sama zasada "tansze kalorie z wlasnych rak"; drugiej reguly dla wsi nie ma (jedna zasada na jedno zjawisko).
    /// </summary>
    internal static class KsCheap
    {
        internal const int RStaple = 0, RSalt = 1, RDrink = 2, RProtein = 3, RRest = 4, RYarn = 5, RThings = 6;
        internal static bool IsFood(int r) { return r == RStaple || r == RDrink || r == RProtein || r == RRest; }

        /// <summary>Szczebel pozycji koszyka Planu (KsBasket.Ids).</summary>
        internal static readonly int[] PlanRung = BuildPlanRung();
        /// <summary>Podstawa kalorii (ziarno, chleb, placek, ryby).</summary>
        internal static readonly bool[] PlanStaple = BuildPlanStaple();
        private static int[] BuildPlanRung()
        {
            var r = new int[KsBasket.N];
            for (int z = 0; z < r.Length; z++) r[z] = RThings;
            r[KsBasket.Grain] = r[KsBasket.Bread] = r[KsBasket.Pie] = RStaple;
            r[KsBasket.Salt] = RSalt;
            r[KsBasket.Beer] = r[KsBasket.Wine] = r[KsBasket.Mead] = RDrink;
            foreach (int z in new[] { KsBasket.Meat, KsBasket.Whale, KsBasket.Fish, KsBasket.Cheese, KsBasket.Butter, KsBasket.Egg }) r[z] = RProtein;
            foreach (int z in new[] { KsBasket.Apple, KsBasket.Carrot, KsBasket.Orange, KsBasket.Grape, KsBasket.Olives, KsBasket.Date, KsBasket.Oil, KsBasket.Honey, KsBasket.Spice }) r[z] = RRest;
            r[KsBasket.Flax] = r[KsBasket.Wool] = RYarn;
            return r;
        }
        private static bool[] BuildPlanStaple()
        {
            var s = new bool[KsBasket.N];
            s[KsBasket.Grain] = s[KsBasket.Bread] = s[KsBasket.Pie] = s[KsBasket.Fish] = true;
            return s;
        }

        /// <summary>Grupa skladu kcal (linia P9): 0 zboze (ziarno, chleb, placek), 1 bialko (z rybami), 2 napoje, 3 reszta jedzenia; -1 - poza jedzeniem.</summary>
        internal static int Group(int rung) { return rung == RStaple ? 0 : rung == RProtein ? 1 : rung == RDrink ? 2 : rung == RRest ? 3 : -1; }

        /// <summary>
        /// Rdzen drabiny (czysta arytmetyka, bez obiektow gry): need - zloto pelnego koszyka pozycji, kcal - kcal pelnego koszyka pozycji, rung - szczebel, staple - podstawa
        /// kalorii, key - klucz kolejnosci (bialko: zl za kcal, napoj: zl za litr; null - zl za kcal), cap - zloto najwyzej na pozycje (polka w grze; null - bez sufitu - Plan),
        /// extraK - kcal pozycji poza drabina (regula zasiegu - pokrywa je tylko podstawa). give - zloto na pozycje. Zwraca wydane zloto.
        /// Poprawka recenzji (noc 17/18.10): sufit polki obejmuje KAZDY szczebel, nie tylko pule kalorii - podniesienie pozycji (1b, 1c, 2, 3, 4) najwyzej do tego, co lezy
        /// (podstawa: razem z jej pula), wiec podstawa traci tylko te kcal, ktore naprawde zastapil towar z polki, a kcal kupione licza sie tylko z polki (BK kupuje
        /// min(budzet / cena, polka) - pusta kategoria nie zabiera kalorii zbozu). Plan (cap null) - co do liczby jak dotad.
        /// </summary>
        internal static double Core(int n, double[] need, double[] kcal, int[] rung, bool[] staple, double[] key, double[] cap, double extraK, double budget, double[] give,
                                    out double kcalNeed, out double kcalGot)
        {
            double b0 = budget > 0 && !double.IsInfinity(budget) ? budget : 0.0, left = b0;
            var nd = new double[n]; var kc = new double[n];
            double total = 0, kfood = 0;
            for (int i = 0; i < n; i++)
            {
                give[i] = 0;
                nd[i] = need[i] > 0 && !double.IsInfinity(need[i]) ? need[i] : 0.0;
                kc[i] = IsFood(rung[i]) && nd[i] > 0 && kcal[i] > 0 && !double.IsInfinity(kcal[i]) ? kcal[i] : 0.0;
                total += nd[i]; kfood += kc[i];
            }
            double ex = extraK > 0 && !double.IsInfinity(extraK) ? extraK : 0.0;
            kcalNeed = kfood + ex;
            if (total <= left && ex <= 0) { for (int i = 0; i < n; i++) give[i] = nd[i]; kcalGot = kfood; return total; }
            // (0) sol
            for (int i = 0; i < n; i++) if (rung[i] == RSalt && nd[i] > 0) { double g = Math.Min(left, nd[i]); give[i] = g; left -= g; }
            // (1) kalorie z najtanszej podstawy (kcal na zl malejaco), w grze najwyzej polka
            var S = new List<int>();
            for (int i = 0; i < n; i++) if (staple[i] && kc[i] > 0 && (cap == null || cap[i] > 0)) S.Add(i);
            S.Sort((a, b) => (kc[b] / nd[b]).CompareTo(kc[a] / nd[a]));
            var pool = new double[n];
            double rem = kfood + ex;
            foreach (int s in S)
            {
                double kpg = kc[s] / nd[s], ck = cap == null ? double.PositiveInfinity : cap[s] * kpg, k = Math.Min(rem, ck);
                if (!(k > 0)) continue;
                pool[s] = k; rem -= k;
                if (rem <= 0) break;
            }
            double D = Math.Max(0.0, rem);                 // kcal bez podstawy na polce (tylko w grze) - pokrywaja je dalsze szczeble
            double pc = 0; foreach (int s in S) pc += pool[s] * nd[s] / kc[s];
            if (pc > left)
            {
                double f = pc > 0 ? left / pc : 0.0, kg0 = 0;
                foreach (int s in S) { pool[s] *= f; give[s] += pool[s] * nd[s] / kc[s]; kg0 += pool[s]; }
                kcalGot = kg0;
                return b0;
            }
            left -= pc;
            var u = new double[n];
            var lmv = new double[n];
            // sufit podniesienia pozycji (czesc koszyka pozycji 0..1): Plan - 1; gra - polka (cap) minus to, co juz bierze pula kalorii podstawy; nigdy ponizej juz kupionego
            double Lim(int i)
            {
                if (cap == null) return 1.0;
                double c = cap[i];
                if (double.IsPositiveInfinity(c)) return 1.0;
                if (!(c > 0) || !(nd[i] > 0)) return u[i];
                double room = c - (staple[i] && pool[i] > 0 && kc[i] > 0 ? pool[i] * nd[i] / kc[i] : 0.0), l = room / nd[i];
                return l >= 1.0 ? 1.0 : (l > u[i] ? l : u[i]);
            }
            // odliczenie podstawy za kcal pozycji: kcal pozycji spoza podstawy najpierw zapelniaja brak (D), reszta zdejmuje podstawe od najdrozszej za kcal
            double Refund(double dkN, double dkS, bool apply)
            {
                double fd = Math.Min(dkN, D), take = dkN - fd + dkS, r = 0;
                if (apply) D -= fd;
                for (int j = S.Count - 1; j >= 0 && take > 1e-12; j--)
                {
                    int s = S[j]; double t = Math.Min(take, pool[s]);
                    if (!(t > 0)) continue;
                    r += t * nd[s] / kc[s]; take -= t;
                    if (apply) pool[s] -= t;
                }
                return r;
            }
            double GroupCost(List<int> items, double f, bool apply)
            {
                double dg = 0, dkN = 0, dkS = 0;
                foreach (int i in items) lmv[i] = Lim(i);                                   // sufit przed odliczeniem (pula podstawy tylko maleje - sufit zostaje dotrzymany)
                foreach (int i in items) { double x = f * (lmv[i] - u[i]); dg += x * nd[i]; if (staple[i]) dkS += x * kc[i]; else dkN += x * kc[i]; }
                double r = Refund(dkN, dkS, apply);
                if (apply) foreach (int i in items) u[i] += f * (lmv[i] - u[i]);
                return dg - r;
            }
            void Upgrade(List<int> items)
            {
                if (items.Count == 0) return;
                double c1 = GroupCost(items, 1.0, false);
                if (c1 <= left) { GroupCost(items, 1.0, true); left -= c1; if (left < 0) left = 0; return; }
                if (!(left > 0)) return;
                double lo = 0, hi = 1;
                for (int it = 0; it < 30; it++) { double mid = 0.5 * (lo + hi); if (GroupCost(items, mid, false) <= left) lo = mid; else hi = mid; }
                if (lo > 0) { left -= GroupCost(items, lo, true); if (left < 0) left = 0; }
            }
            bool Open(int i) { return nd[i] > 0 && u[i] < 1.0 && !(staple[i] && D > 1e-9) && (cap == null || Lim(i) > u[i] + 1e-12); }   // podstawa bez polki (brak D) i pozycja z pusta polka (gra) - nie podnoszona
            var one = new List<int>(1);
            // (1b) zamiany oplacalne: pozycja jedzenia tansza za kcal od podstawy, ktora zastepuje
            for (int i = 0; i < n; i++)
            {
                if (!IsFood(rung[i]) || !Open(i)) continue;
                one.Clear(); one.Add(i);
                double c = GroupCost(one, 1.0, false);
                if (c <= 1e-9) { GroupCost(one, 1.0, true); left -= c; }
            }
            // (1c) zboze w formach koszyka do potrzeby (zwykly chleb i placek domu, reszta jako ziarno - kasza) - przed bialkiem: chleb i kasza sa podstawa biedy miejskiej
            if (left > 0)
            {
                var l0 = new List<int>();
                for (int i = 0; i < n; i++) if (rung[i] == RStaple && Open(i)) l0.Add(i);
                Upgrade(l0);
            }
            // (2) bialko - najtansze za kcal najpierw, (3) napoj - najtanszy za litr najpierw
            foreach (int rg in new[] { RProtein, RDrink })
            {
                var l = new List<int>();
                for (int i = 0; i < n; i++) if (rung[i] == rg && Open(i)) l.Add(i);
                l.Sort((a, b) =>
                {
                    double ka = key != null ? key[a] : (kc[a] > 0 ? nd[a] / kc[a] : double.MaxValue), kb = key != null ? key[b] : (kc[b] > 0 ? nd[b] / kc[b] : double.MaxValue);
                    return ka.CompareTo(kb);
                });
                foreach (int i in l) { if (!(left > 0)) break; one.Clear(); one.Add(i); Upgrade(one); }
            }
            // (4) reszta jedzenia po rowno (owoce, oliwa, miod, przyprawy; zboze w formach koszyka, gdy podstawy nie bylo na polce, a brak juz pokryty)
            if (left > 0)
            {
                var l = new List<int>();
                for (int i = 0; i < n; i++) if ((rung[i] == RRest || rung[i] == RStaple) && Open(i)) l.Add(i);
                Upgrade(l);
            }
            // (5) przedza domowa, (6) rzeczy - szczebel po rowno, bez zamiany kcal
            foreach (int rg in new[] { RYarn, RThings })
            {
                double cost = 0;
                for (int i = 0; i < n; i++) if (rung[i] == rg && nd[i] > 0) cost += nd[i];
                if (!(cost > 0)) continue;
                double f = cost <= left ? 1.0 : Math.Max(0.0, left / cost);
                for (int i = 0; i < n; i++) if (rung[i] == rg && nd[i] > 0) u[i] = f;
                left = Math.Max(0.0, left - f * cost);
            }
            double kg = 0;
            for (int i = 0; i < n; i++)
            {
                if (IsFood(rung[i])) { give[i] = u[i] * nd[i] + (pool[i] > 0 && kc[i] > 0 ? pool[i] * nd[i] / kc[i] : 0.0); kg += u[i] * kc[i] + pool[i]; }
                else if (rung[i] == RYarn || rung[i] == RThings) give[i] = u[i] * nd[i];
            }
            kcalGot = kg;
            return Math.Max(0.0, b0 - left);
        }

        /// <summary>Statystyka klasy (linia P9): [0] kcal pelnego koszyka, [1] kcal kupione, [2] koszt rzeczy, [3] rzeczy zaplacone, [4..7] kcal kupione wedlug grup,
        /// [8..11] kcal pelnego koszyka wedlug grup, [12] stara drabina lepsza w kcal (1/0), [13] dokupiona podstawa (kg Planu).</summary>
        internal const int StatN = 14;

        /// <summary>
        /// KS krok 8 (Plan), klasa miasta: drabina koszyka q (pozycje wyciete regula zasiegu = 0; qFull - pelny koszyk klasy) przy cenach price[po + z] (zl za kg) na budzet.
        /// cheap - drabina tanszego koszyka, inaczej dotychczasowa (KsBasket.Ladder - co do liczby jak dotad). paid - czesc zaplacona 0..1, extraKg - podstawa dokupiona
        /// ponad koszyk (kg/d). Straznik: stara drabina daje wiecej kcal - zostaje stara (licznik w st[12]). Zwraca wydane zl.
        /// </summary>
        internal static double PlanLadder(bool cheap, double[] q, double[] qFull, double[] price, int po, double budget, double[] paid, double[] extraKg, double[] st)
        {
            int N = KsBasket.N;
            Array.Clear(extraKg, 0, N);
            double spent = KsBasket.Ladder(q, price, po, budget, paid);
            if (cheap)
            {
                var need = new double[N]; var kcal = new double[N]; var key = new double[N]; var give = new double[N];
                double cutK = 0, oldK = 0;
                for (int z = 0; z < N; z++)
                {
                    double p = Math.Max(1e-9, price[po + z]), qz = q[z] > 0 ? q[z] : 0.0;
                    need[z] = qz * p; kcal[z] = qz * KsBasket.Kcal[z];
                    key[z] = PlanRung[z] == RProtein ? (KsBasket.Kcal[z] > 0 ? p / KsBasket.Kcal[z] : double.MaxValue) : p;
                    if (IsFood(PlanRung[z]))
                    {
                        if (qFull != null && qFull[z] > qz) cutK += (qFull[z] - qz) * KsBasket.Kcal[z];
                        oldK += Math.Max(0.0, Math.Min(1.0, paid[z])) * qz * KsBasket.Kcal[z];
                    }
                }
                double kN, kG;
                double sp = Core(N, need, kcal, PlanRung, PlanStaple, key, null, cutK, budget, give, out kN, out kG);
                if (oldK > kG + 1e-6 * Math.Max(1.0, kN)) { if (st != null) st[12] += 1; }
                else
                {
                    spent = sp;
                    for (int z = 0; z < N; z++)
                    {
                        if (!(need[z] > 0)) { paid[z] = 1.0; continue; }
                        paid[z] = Math.Min(1.0, give[z] / need[z]);
                        if (give[z] > need[z]) extraKg[z] = (give[z] - need[z]) / Math.Max(1e-9, price[po + z]);
                    }
                }
            }
            if (st != null)
                for (int z = 0; z < N; z++)
                {
                    double qz = q[z] > 0 ? q[z] : 0.0, qf = qFull != null ? Math.Max(qz, qFull[z]) : qz, p = price[po + z];
                    int g = Group(PlanRung[z]);
                    double pd = Math.Max(0.0, Math.Min(1.0, paid[z])) * qz;
                    if (g >= 0)
                    {
                        double k = KsBasket.Kcal[z], got = (pd + extraKg[z]) * k;
                        st[0] += qf * k; st[1] += got; st[4 + g] += got; st[8 + g] += qf * k;
                        st[13] += extraKg[z];
                    }
                    else if (PlanRung[z] != RSalt) { st[2] += qf * p; st[3] += pd * p; }
                }
            return spent;
        }

        /// <summary>Podstawa dokupiona ponad koszyk (kg/d wedlug KsBasket.Ids) -> potrzeby miasta w gore (popyt Planu: zboze, ryby). Pozycje spoza podstawy - 0.</summary>
        internal static void ExtraToNeed(double[] n, double[] ex)
        {
            double g = ex[KsBasket.Grain] + ex[KsBasket.Bread] + ex[KsBasket.Pie];
            if (g > 0) n[KsTab.NG_] += g;
            if (ex[KsBasket.Fish] > 0) n[KsTab.NFish] += ex[KsBasket.Fish];
        }
    }
}
