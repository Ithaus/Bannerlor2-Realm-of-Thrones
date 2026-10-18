using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

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
        /// kalorii, key - klucz kolejnosci (bialko: zl za kcal, napoj: zl za litr; null - zl za kcal), cap - zloto najwyzej na podstawe (polka w grze; null - bez sufitu),
        /// extraK - kcal pozycji poza drabina (regula zasiegu - pokrywa je tylko podstawa). give - zloto na pozycje. Zwraca wydane zloto.
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
                foreach (int i in items) { double x = f * (1.0 - u[i]); dg += x * nd[i]; if (staple[i]) dkS += x * kc[i]; else dkN += x * kc[i]; }
                double r = Refund(dkN, dkS, apply);
                if (apply) foreach (int i in items) u[i] += f * (1.0 - u[i]);
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
            bool Open(int i) { return nd[i] > 0 && u[i] < 1.0 && !(staple[i] && D > 1e-9); }   // podstawa bez polki (brak D) - nie podnoszona
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

    internal static partial class StartCalibration
    {
        internal static bool SwCheapBasket { get { var s = Settings.Current; return s == null || s.KsCheapBasket; } }

        // r4-koszyk: dokupiona podstawa koszyka miasta z Planu (czesc koszyka ponad 1 wedlug pozycji KsBasket.Ids; czesc zaplacona _townPaid zostaje 0..1 - zapis "@" bez zmian,
        // starsza DLL jej nie widzi) - klucz zapisu arm_ks_koszyk
        private static Dictionary<string, float[]> _townExtra = new Dictionary<string, float[]>(StringComparer.Ordinal);

        /// <summary>Dokupiona podstawa koszyka miasta (czesc ponad koszyk wedlug pozycji) albo null (brak albo klucz Ks Cheap Basket wylaczony - jak dotad).</summary>
        private static float[] TownExtraOf(Settlement st)
        {
            if (!SwCheapBasket || st == null || st.StringId == null) return null;
            float[] x;
            return _townExtra.TryGetValue(st.StringId, out x) ? x : null;
        }

        /// <summary>r4-koszyk: miasto w mapie koszyka ZC4 (KS zastosowane, lambda > 0) - place za brak rak (KsWageHands) dzialaja tylko tam.</summary>
        internal static bool KsTownApplied(Town t) { ZTown zt; return ZTownOf(t, out zt); }

        private static void CheapReset()
        {
            _townExtra = new Dictionary<string, float[]>(StringComparer.Ordinal);
            CheapZero();
        }

        // ------------------------------------------------------------ gra: drabina tanszego koszyka (TownFolk.AnchorDict -> Zc4Ladder)
        // liczniki od ostatniej linii wedlug rynku: miasto-doby drabiny, bez podstawy na polce (stara drabina), kcal potrzebne / kupione, budzet / wydane, zloto wedlug
        // grup (zboze i ryby podstawy, bialko, napoje, reszta) i dokupiona podstawa (zl ponad koszyk), niekupione wedlug grup
        private static readonly int[] _chN = new int[2], _chOld = new int[2];
        private static readonly double[] _chKN = new double[2], _chKG = new double[2], _chB = new double[2], _chSp = new double[2], _chX = new double[2];
        private static readonly double[] _chG = new double[8], _chCut = new double[8];
        private static int _chStum;
        private static void CheapZero()
        {
            for (int m = 0; m < 2; m++) { _chN[m] = _chOld[m] = 0; _chKN[m] = _chKG[m] = _chB[m] = _chSp[m] = _chX[m] = 0; }
            Array.Clear(_chG, 0, _chG.Length); Array.Clear(_chCut, 0, _chCut.Length);
        }

        /// <summary>
        /// Drabina tanszego koszyka w grze (klucz Ks Cheap Basket; TownFolk.AnchorDict, kiesy nie stac na caly koszyk jedzenia): budzet B = ff x suma kategorii jedzenia
        /// koszyka - KsCheap.Core na kategoriach (pozycja = najtanszy za kg towar kategorii z polka; kcal = zloto kategorii x kcal na zl; podstawa najwyzej to, co lezy na polce).
        /// Zwraca sume slownika albo -1 (zadna podstawa nie lezy na polce - dotychczasowa drabina).
        /// </summary>
        private static double Zc4LadderCheap(Town town, ZTown zt, Dictionary<ItemCategory, float> dict, List<ItemCategory> keys, double ff, double tot)
        {
            int n = keys.Count;
            if (n == 0) return -1;
            var ro = town.Settlement != null ? town.Settlement.ItemRoster : null;
            var need = new double[n]; var kcal = new double[n]; var rung = new int[n]; var staple = new bool[n]; var key = new double[n]; var cap = new double[n]; var give = new double[n];
            bool anyStaple = false;
            for (int i = 0; i < n; i++)
            {
                var c = keys[i];
                float v; dict.TryGetValue(c, out v);
                need[i] = v > 0 ? v : 0.0;
                int[] zs; _zCats.TryGetValue(c, out zs);
                int best = -1; double bestPk = double.MaxValue; bool bestStock = false; long cnt = 0;
                if (zs != null)
                    foreach (int z in zs)
                    {
                        var it = _zItems[z]; if (it == null) continue;
                        int have = ro != null ? ro.GetItemNumber(it) : 0;
                        cnt += Math.Max(0, have);
                        double pk = Math.Max(1, zt.Px[z]) / _zKg[z];
                        bool st = have > 0;
                        if (best < 0 || (st && !bestStock) || (st == bestStock && pk < bestPk)) { best = z; bestPk = pk; bestStock = st; }
                    }
                int rg = best >= 0 ? Rung(best) : 4;
                rung[i] = rg == 0 ? KsCheap.RStaple : rg == 1 ? KsCheap.RSalt : rg == 2 ? KsCheap.RDrink : rg == 3 ? KsCheap.RProtein : KsCheap.RRest;
                double kk = best >= 0 ? KsBasket.Kcal[best] : 0.0;
                kcal[i] = best >= 0 && bestPk > 0 ? need[i] / bestPk * kk : 0.0;   // zl -> kg -> kcal
                staple[i] = best >= 0 && (rg == 0 || best == ZFish);
                key[i] = rung[i] == KsCheap.RProtein ? (kk > 0 ? bestPk / kk : double.MaxValue) : bestPk;
                cap[i] = best >= 0 ? cnt * Math.Max(1, zt.Px[best]) : 0.0;
                if (staple[i] && need[i] > 0 && kcal[i] > 0 && cap[i] > 0) anyStaple = true;
            }
            int m = zt.Market == 0 ? 0 : 1;
            if (!anyStaple) { _chOld[m]++; return -1; }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            double B = Math.Max(0.0, ff) * tot, kN, kG;
            KsCheap.Core(n, need, kcal, rung, staple, key, cap, 0.0, B, give, out kN, out kG);
            double after = 0;
            for (int i = 0; i < n; i++)
            {
                double g = give[i] > 0 && !double.IsNaN(give[i]) && !double.IsInfinity(give[i]) ? give[i] : 0.0;
                dict[keys[i]] = (float)g; after += g;
                int gr = KsCheap.Group(rung[i]);
                if (gr < 0) continue;
                int gi = staple[i] ? 0 : gr;
                _chG[m * 4 + gi] += g;
                if (g > need[i]) _chX[m] += g - need[i];
                else _chCut[m * 4 + gr] += need[i] - g;
            }
            _chN[m]++; _chKN[m] += kN; _chKG[m] += kG; _chB[m] += B; _chSp[m] += after;
            _zLadderN++;
            _zLadCut += Math.Max(0.0, tot - after);                                                     // jedzenie koszyka niekupione z braku pieniedzy (zl) - jak dotad
            _zTkLadder += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            return after;
        }

        /// <summary>Linia "Tanszy koszyk (r4-koszyk): dzien N" (liczniki od ostatniej linii; po linii ZC4) albo null (klucz wylaczony i nic nie liczylo).</summary>
        private static string CheapLine(int day)
        {
            bool on = SwCheapBasket;
            if (!on && _chN[0] + _chN[1] + _chOld[0] + _chOld[1] == 0 && _townExtra.Count == 0) { CheapZero(); return null; }
            var sb = new StringBuilder(1400);
            sb.Append("Tanszy koszyk (r4-koszyk): dzien ").Append(day).Append(" | klucz Ks Cheap Basket ").Append(on ? "TAK" : "NIE");
            string[] mk = { "Westeros", "Essos" };
            string[] gn = { "zboze i ryby (podstawa)", "bialko", "napoje", "reszta" };
            for (int m = 0; m < 2; m++)
            {
                sb.Append(m == 0 ? " | " : "; ").Append(mk[m]).Append(": drabina w ").Append(_chN[m]).Append(" miasto-dobach (bez podstawy na polce - stara drabina ").Append(_chOld[m])
                  .Append("), kcal jedzenia kupione ").Append(_chKN[m] > 0 ? (100.0 * _chKG[m] / _chKN[m]).ToString("0.0", Inv) : "-").Append("% potrzebnych")
                  .Append(", budzet ").Append(F0(_chB[m])).Append(" zl = wydane ").Append(F0(_chSp[m])).Append(" + niewydane ").Append(F0(_chB[m] - _chSp[m]))
                  .Append(" zl (wydane:");
                for (int g = 0; g < 4; g++) sb.Append(g == 0 ? " " : ", ").Append(gn[g]).Append(' ').Append(F0(_chG[m * 4 + g]));
                sb.Append("; podstawa ponad koszyk ").Append(F0(_chX[m])).Append(" zl; niekupione:");
                for (int g = 1; g < 4; g++) sb.Append(g == 1 ? " " : ", ").Append(gn[g]).Append(' ').Append(F0(_chCut[m * 4 + g]));
                sb.Append(')');
            }
            // dokupiona podstawa z Planu w popycie miast (szt./d przy lambdzie)
            var map = _zt; var xp = new double[ZN]; int xn = 0;
            foreach (var zt in map.Values)
            {
                var x = zt.Extra; if (x == null) continue;
                bool any = false;
                for (int z = 0; z < ZN && z < x.Length; z++)
                {
                    if (!(x[z] > 0)) continue;
                    double tp = zt.Paid != null && z < zt.Paid.Length ? Math.Max(0f, Math.Min(1f, zt.Paid[z])) : 1.0, f = tp + x[z];
                    if (f > 0) { xp[z] += zt.Q[z] * x[z] / f * zt.Lam; any = true; }
                }
                if (any) xn++;
            }
            sb.Append(" | Plan: dokupiona podstawa w popycie miast (szt./d przy lambdzie) w ").Append(xn).Append(" miastach:");
            bool fx = true;
            foreach (int z in new[] { ZGrain, ZBread, ZPie, ZFish }) { if (xp[z] < 0.5) continue; sb.Append(fx ? " " : ", ").Append(ZIds[z]).Append(' ').Append(F0(xp[z])); fx = false; }
            if (fx) sb.Append(" -");
            sb.Append(" | potkniecia ").Append(_chStum).Append('.');
            CheapZero(); _chStum = 0;
            return sb.ToString();
        }

        // ------------------------------------------------------------ Plan: linia P9 (podzial niezaplaconego koszyka klas miast na kcal jedzenia i rzeczy)
        /// <summary>Linia "KS: tanszy koszyk miast (P9, r4-koszyk) plan doba N" - takze przy kluczu wylaczonym (podzial P9 stara drabina).</summary>
        private static void P9Line(KsResult r)
        {
            var M = r.Money;
            if (M == null || !M.On) return;
            var sb = new StringBuilder(1600);
            sb.Append(PK).Append("tanszy koszyk miast (P9, r4-koszyk) plan doba ").Append(_wDay).Append(" | klucz Ks Cheap Basket ").Append(M.Cheap ? "TAK" : "NIE")
              .Append(" (klasy miast przy cenach startu; kcal kupione = zaplacone + dokupiona podstawa)");
            string[] cls = { "W", "R", "P" };
            double sigma = Math.Max(1, DailyLife.KsSigma);
            for (int m = 0; m < 2; m++)
            {
                sb.Append(" | ").Append(r.Markets[m]).Append(':');
                for (int c = 0; c < 3; c++)
                {
                    int o = (m * 3 + c) * KsCheap.StatN;
                    double kn = M.P9[o], kg = M.P9[o + 1], gc = M.P9[o + 2], gp = M.P9[o + 3];
                    sb.Append(c == 0 ? " " : "; ").Append(cls[c]).Append(" kcal jedzenia ").Append(kn > 0 ? (100.0 * kg / kn).ToString("0.0", Inv) : "-").Append("%, rzeczy ")
                      .Append(gc > 0 ? (100.0 * gp / gc).ToString("0.0", Inv) : "-").Append("%, sklad kcal kupione/koszyk (zboze/bialko/napoje/reszta) ");
                    for (int g = 0; g < 4; g++) sb.Append(g == 0 ? "" : "/").Append(kg > 0 ? (100.0 * M.P9[o + 4 + g] / kg).ToString("0", Inv) : "-");
                    sb.Append(" vs ");
                    for (int g = 0; g < 4; g++) sb.Append(g == 0 ? "" : "/").Append(kn > 0 ? (100.0 * M.P9[o + 8 + g] / kn).ToString("0", Inv) : "-");
                    sb.Append("%, stara drabina lepsza ").Append(F0(M.P9[o + 12]));
                }
                sb.Append("; dokupiona podstawa (szt. gry/d):");
                bool f = true;
                foreach (int z in new[] { KsBasket.Grain, KsBasket.Bread, KsBasket.Pie, KsBasket.Fish })
                {
                    double pcs = M.ExtraT[m * KsBasket.N + z] / (sigma * ZKg(z));
                    if (pcs < 0.5) continue;
                    sb.Append(f ? " " : ", ").Append(KsBasket.Ids[z]).Append(' ').Append(F0(pcs)); f = false;
                }
                if (f) sb.Append(" -");
            }
            sb.Append(" | P9: klasy R i P - kcal jedzenia >= 95% (prog), rzeczy - INFO.");
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ zapis (klucz arm_ks_koszyk: dokupiona podstawa z Planu)
        /// <summary>Dokupiona podstawa miast: "miasto:z=v/z=v,..." (tylko v > 0.0005).</summary>
        internal static string ExportCheap()
        {
            var sb = new StringBuilder();
            foreach (var kv in _townExtra)
            {
                if (kv.Value == null || string.IsNullOrEmpty(kv.Key) || kv.Key.IndexOfAny(new[] { ':', ',', '/', '=', '|' }) >= 0) continue;
                var p = new StringBuilder();
                for (int z = 0; z < kv.Value.Length; z++)
                {
                    float v = kv.Value[z];
                    if (!(v > 0.0005f) || float.IsInfinity(v)) continue;
                    if (p.Length > 0) p.Append('/');
                    p.Append(z.ToString(Inv)).Append('=').Append(Math.Min(v, 50f).ToString("0.####", Inv));
                }
                if (p.Length == 0) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(kv.Key).Append(':').Append(p);
            }
            return sb.ToString();
        }

        /// <summary>Odczyt (klucz arm_ks_koszyk). Zly wpis - pomijany; straznik rozmiaru (najwyzej 4000 miast, v w 0..50).</summary>
        internal static void ImportCheap(string s)
        {
            var map = new Dictionary<string, float[]>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(s) && s.Length <= 400000)
            {
                foreach (var ent in s.Split(','))
                {
                    if (map.Count >= 4000) break;
                    int c = ent.IndexOf(':'); if (c <= 0) continue;
                    var a = new float[KsBasket.N]; bool any = false;
                    foreach (var kv in ent.Substring(c + 1).Split('/'))
                    {
                        var q = kv.Split('='); int z; float v;
                        if (q.Length == 2 && int.TryParse(q[0], NumberStyles.Integer, Inv, out z) && z >= 0 && z < a.Length && float.TryParse(q[1], NumberStyles.Float, Inv, out v) && v > 0 && v <= 50f) { a[z] = v; any = true; }
                    }
                    if (any) map[ent.Substring(0, c)] = a;
                }
            }
            _townExtra = map;
        }
    }
}
