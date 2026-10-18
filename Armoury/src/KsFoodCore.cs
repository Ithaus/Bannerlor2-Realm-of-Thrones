using System;
using System.Collections.Generic;

namespace Armoury
{
    /// <summary>
    /// KS-R3 (recenzja rundy 3, wazne): RDZEN DECYZJI KUPCOW ZYWNOSCI KS-H - czysty C# (bez obiektow gry): ten sam kod liczy decyzje doby w grze (KsFoodCarts.Daily -
    /// tablice polek, cen, popytu i kas z gry, wykonanie lotu przez KsFoodCarts.Ship) i w tescie portu (kmoney/port4 - te same tablice z symulatora ksim/sim/r4port.py,
    /// porownanie lotow i sztuk doby). Reguly (opis w KsFoodCarts):
    ///  - zamowienie Dz = max(popyt zaplacony, odbior 7 dob) x kx; kx x1.10 (do 2.5) tylko przy prawdziwym braku (polka <= 6 szt. i polka + w drodze < (T* + doby
    ///    najblizszego zrodla) x popyt), stoi, gdy w drodze >= doby zrodla x popyt, inaczej x0.98 (od 1); zapas zrodla 1.25 x T* x popyt BEZ kx;
    ///  - zrodlo sprzedaje najwyzej polowe nadwyzki ponad zapas + 5 szt.; cel co 2. doba (parzystosc Fnv); 12 najblizszych, gdy nic - do 4 dalszych w zasiegu;
    ///  - koszt na miejscu c = indeks zrodla x (1 + tau x doby), indeks celu >= 1.03 x c; najwyzej 3 zrodla od najtanszego; ilosc (T* + doby + 2) x Dz - polka z droga;
    ///  - ZYSK KUPCA: koszt lotu + fracht <= 97% ceny polki celu x sztuki (do 3 polowien lotu - tansze sztuki z polki zrodla), inaczej bez lotu;
    ///  - KASA celu: 15% ponad zapas kupcow; przy pustej polce zboza i soli - do polowy zapasu i najwyzej 5% zapasu na dobe ponad zwykly budzet;
    ///  - kolejnosc miast celu: indeks malejaco, przy rownym - indeks miasta (stala kolejnosc w grze i w porcie).
    /// KS-R4 r4-kupcy (noc 17/18.10, PLAN-KS-RUNDA4 rozdz. 3): nowe flagi wejscia (kazda wylaczona = kod rundy 3 bit w bit, test portu kmoney/port4):
    ///  - FarAlways (H2): do 8 dalszych miast z oferta w zasiegu ZAWSZE (nie tylko, gdy 12 najblizszych nic nie da); najpierw tani test oferty, potem doby i wojna;
    ///  - RelKeep (H3): zapas zrodla wzgledny - max(0.5 T*, min(1.25 T*, 1.1 x mediana dni polki rynku)), sprzedaje cala nadwyzke zboza i soli, 2/3 reszty;
    ///  - DestValue (H4): wartosc n sztuk u celu (cena polki celu schodzi z kazda sztuka, najwyzej 3 x cena startu, x 0.97) - ta sama liczba w decyzji i w rachunku;
    ///    recenzja r4-kupcy: drugi i trzeci lot doby do tego samego (cel, towar) wyceniany ZA sztukami lotow wczesniejszych tej doby (calka od j0 = juz wyslane do j0 + n),
    ///    jak rachunek przy dostawie, gdy wczesniejszy woz lezy juz na polce celu;
    ///  - Consign (H5): kredyt kupcow - cel nie placi przy wyjezdzie (bez budzetu kasy celu), limit = miejsce kredytu zrodla SrcRoom[s] (towar + fracht), cel z rachunkiem
    ///    starszym niz zwloka (DstOverdue) - bez nowych dostaw (licznik "cel z zaleglym");
    ///  - ByNeed (diagnoza fin80 "ceny"): kolejka zamowien wszystkich towarow wedlug pokrycia H / (T* x Dz) rosnaco (najkrotsze pierwsze) - budzet kasy celu i miejsce
    ///    kredytu zrodla nie sa juz zjadane przez zboze i ryby w stalej kolejnosci towarow; licznik sztuk zablokowanych z braku pieniedzy wedlug towaru;
    ///  - Famine (decyzja Jeffa 17.10 "dowoz jak w ksiazkach"): cel z polka zboza / ryb / miesa ponizej 0.5 T* (FamineGood) - pierwszy w kolejce (przed reszta);
    ///  - IxAt (T9, bit w bit): indeks ceny liczony tylko tam, gdzie ktos go czyta (NaN w tablicy = jeszcze nie liczony).
    /// </summary>
    internal static class KsFoodCore
    {
        internal const double TauLand = KsCore.CorrTauLand, TauSea = KsCore.CorrTauSea, Margin = 0.03, SrcKeep = 1.25, OfferShare = 0.5, KxUp = 1.10, KxDown = 0.98, KxMax = 2.5, CofferShare = 0.15;
        internal const double ReserveFloorShort = 0.5, ReserveDayShort = 0.05, MaxDur = 30.0, MaxPer = 7.0;
        internal const int MaxSrc = 3, NCandFar = 4, Keep = 5;
        // KS-R4 r4-kupcy: H2 dalsze miasta zawsze (do 8), H3 zapas wzgledny, glod (pokrycie ponizej 0.5 T*)
        internal const int NCandFarAlways = 8;
        internal const double RelKeepMin = 0.5, RelKeepMed = 1.1, RelShareStaple = 1.0, RelShare = 0.67, FamineCover = 0.5;

        /// <summary>Wejscie doby: tablice [t * NK + k] (polka bez modyfikatora, w drodze do celu, indeks ceny kategorii, popyt zaplacony, odbior 7 dob), kx (zmieniane),
        /// T* [m * NK + k], towar psujacy sie i towar podstawowy (zboze, sol) [k], kasa i zapas kupcow [t], oblezenie i doba przegladu celu [t], kandydaci [t].</summary>
        internal sealed class In
        {
            public int NT, NK, Today;
            public int[] Mk; public bool[] Siege, Review;
            public double[] Sh, Inc, Ix, Dp, Off, Ts, Gold, Res;
            public float[] Kx;
            public bool[] Per, Staple;
            public int[][] Cand, CandFar;
            public Func<int, int, float> Days;                 // (zrodlo, cel) - doby; < 0 brak
            public Func<int, int, bool> Sea, War;
            public Func<int, int, int, long> LotPrice;         // (zrodlo, k, n) - zaplata za n sztuk z polki zrodla
            public Func<int, int, int> DestUnit;               // (cel, k) - cena sztuki na polce celu (0 - brak)
            public Func<int, int, int, int, long, long, bool, float, int> Ship;   // (zrodlo, cel, k, n, koszt, fracht, morze, doby) - wyslane sztuki
            public Func<int, int, int, bool> SampleAt;         // (zrodlo, cel, k) - probka wyceny tego lotu (gra: 1 na 64)
            public Action<int, int, int> Sample;               // (zrodlo, k, n)
            // KS-R4 r4-kupcy (kazda wylaczona / null = runda 3 bit w bit)
            public bool FarAlways, RelKeep, ByNeed, Famine, Consign;
            public double[] SrcRoom;                           // [nt] Consign: miejsce kredytu zrodla (zl: towar + fracht)
            public bool[] DstOverdue;                          // [nt] Consign: cel z rachunkiem starszym niz zwloka
            public bool[] FamineGood;                          // [NK] towar glodu (zboze, ryby, mieso)
            public Func<int, int, int, int, long> DestValue;   // (cel, k, j0, n) - wartosc n sztuk u celu po marzy za j0 sztukami juz wyslanymi dzis do (cel, k) (null - n x cena polki x 0.97)
            public Func<int, int, double> IxAt;                // (miasto, k) - indeks ceny na zadanie (T9; null - tablica Ix pelna)
        }

        /// <summary>Liczniki doby: zamowione, kasa celu za mala, loty bez zysku, z dalszych miast, z zapasu kupcow (loty, zl), kasy miast z brakiem / zapas.
        /// KS-R4 r4-kupcy: zrodlo bez miejsca kredytu, cel z zaleglym (szt.), zablokowane z braku pieniedzy wedlug towaru, glod (celow - rozne miasta doby, zamowien
        /// glodu - pary (cel, towar), zamowione, wyslane).</summary>
        internal sealed class Stats
        {
            public long Ordered, Poor, NoProfit, ResGold; public int Far, ResLots;
            public readonly List<double> CofShort = new List<double>();
            public long SrcCap, Overdue, FamineOrd, FamineGot; public int FamineReq, FamineReqK;
            public long[] PoorByK;
        }

        // stan jednego wywolania Decide (wspolny dla obu kolejek)
        private sealed class Run
        {
            public In A; public Stats St;
            public double[] Dz, DzB, Sh, Offer, Budget, BudgetShort, Spent, SrcSpent;
            public bool[] ShortSeen, FarMark, FamSeen;
            public readonly List<KeyValuePair<double, int>> Cand = new List<KeyValuePair<double, int>>(24);
        }

        // zamowienie (cel, towar) w kolejce wedlug potrzeby
        private struct Req { public int T, K; public double Cov, Ix; public bool Fam; }

        /// <summary>T9: indeks ceny [o] - z tablicy albo na zadanie (NaN = jeszcze nie liczony), ten sam wynik co pelna tablica.</summary>
        private static double IX(In a, int o)
        {
            double v = a.Ix[o];
            if (a.IxAt != null && double.IsNaN(v)) { v = a.IxAt(o / a.NK, o % a.NK); a.Ix[o] = v; }
            return v;
        }

        internal static void Decide(In a, Stats st)
        {
            int nt = a.NT, NK = a.NK;
            if (st.PoorByK == null || st.PoorByK.Length != NK) st.PoorByK = new long[NK];
            var dz = new double[nt * NK]; var dzB = new double[nt * NK];
            var budget = new double[nt]; var budgetShort = new double[nt]; var spent = new double[nt]; var shortSeen = new bool[nt];
            var sh = (double[])a.Sh.Clone();
            for (int i = 0; i < nt; i++)
            {
                double lmin = a.Cand != null && a.Cand[i] != null && a.Cand[i].Length > 0 ? Math.Max(1f, a.Days(a.Cand[i][0], i)) : 1.0;
                for (int k = 0; k < NK; k++)
                {
                    int o = i * NK + k;
                    double p = a.Dp[o], b = Math.Max(p, a.Off[o]), T = a.Ts[a.Mk[i] * NK + k];
                    double kxv = a.Kx[o], H = sh[o] + a.Inc[o];
                    if (p > 0 && sh[o] <= Keep + 1 && H < (T + lmin) * b) kxv = Math.Min(KxMax, kxv * KxUp);
                    else if (!(b > 0 && a.Inc[o] >= lmin * b)) kxv = Math.Max(1.0, kxv * KxDown);
                    a.Kx[o] = (float)kxv;
                    dzB[o] = b; dz[o] = b * kxv;
                }
                double res = Math.Max(0.0, a.Res[i]);
                budget[i] = CofferShare * Math.Max(0.0, a.Gold[i] - res);
                budgetShort[i] = Math.Max(budget[i], Math.Min(CofferShare * Math.Max(0.0, a.Gold[i] - ReserveFloorShort * res), budget[i] + ReserveDayShort * res));
            }
            var offer = new double[nt * NK];
            if (!a.RelKeep)
            {
                for (int i = 0; i < nt; i++)
                    for (int k = 0; k < NK; k++)
                    {
                        if (a.Siege[i]) continue;
                        int o = i * NK + k;
                        double sur = sh[o] - SrcKeep * a.Ts[a.Mk[i] * NK + k] * dzB[o] - Keep;
                        if (sur > 1) offer[o] = Math.Floor(OfferShare * sur);
                    }
            }
            else
            {
                // H3: mediana dni polki rynku (miasta z popytem) - gdy caly rynek ma malo, eksporter schodzi ku medianie (nigdy ponizej 0.5 T*)
                var med = new double[2 * NK]; var tmp = new List<double>(nt);
                for (int m = 0; m < 2; m++)
                    for (int k = 0; k < NK; k++)
                    {
                        tmp.Clear();
                        for (int i = 0; i < nt; i++) { if ((a.Mk[i] == 1 ? 1 : 0) != m) continue; int o = i * NK + k; if (dzB[o] > 0) tmp.Add(sh[o] / dzB[o]); }
                        if (tmp.Count == 0) { med[m * NK + k] = double.PositiveInfinity; continue; }
                        tmp.Sort();
                        med[m * NK + k] = tmp[(tmp.Count - 1) / 2];
                    }
                for (int i = 0; i < nt; i++)
                    for (int k = 0; k < NK; k++)
                    {
                        if (a.Siege[i]) continue;
                        int o = i * NK + k;
                        double T = a.Ts[a.Mk[i] * NK + k];
                        double keepT = Math.Max(RelKeepMin * T, Math.Min(SrcKeep * T, RelKeepMed * med[(a.Mk[i] == 1 ? 1 : 0) * NK + k]));
                        double sur = sh[o] - keepT * dzB[o] - Keep;
                        if (sur > 1) offer[o] = Math.Floor((a.Staple[k] ? RelShareStaple : RelShare) * sur);
                    }
            }
            if (a.IxAt != null)
            {
                // T9 (bit w bit): indeks ceny PRZED pierwszym lotem (polka zrodla zmienia sie z kazdym lotem, a runda 3 czytala indeks z rana) - tylko tam, gdzie ktos
                // go przeczyta: zrodlo z oferta (AddCand) albo cel przegladu doby z brakiem (kolejka i AddCand)
                for (int i = 0; i < nt; i++)
                    for (int k = 0; k < NK; k++)
                    {
                        int o = i * NK + k;
                        if (!double.IsNaN(a.Ix[o])) continue;
                        bool need = offer[o] >= 1;
                        if (!need && !a.Siege[i] && a.Review[i] && dz[o] > 0)
                        {
                            double lim = a.Per[k] ? MaxPer : MaxDur;
                            need = sh[o] + a.Inc[o] < (a.Ts[a.Mk[i] * NK + k] + lim + 2) * dz[o];
                        }
                        if (need) a.Ix[o] = a.IxAt(i, k);
                    }
            }
            var r = new Run { A = a, St = st, Dz = dz, DzB = dzB, Sh = sh, Offer = offer, Budget = budget, BudgetShort = budgetShort, Spent = spent, ShortSeen = shortSeen, SrcSpent = new double[nt], FarMark = new bool[nt], FamSeen = new bool[nt] };
            if (!a.ByNeed)
            {
                var dst = new List<int>(nt);
                var fam = a.Famine ? new bool[nt] : null;
                for (int k = 0; k < NK; k++)
                {
                    double lim = a.Per[k] ? MaxPer : MaxDur;
                    dst.Clear();
                    for (int i = 0; i < nt; i++)
                    {
                        if (a.Siege[i] || !a.Review[i]) continue;
                        double d = dz[i * NK + k]; if (!(d > 0)) continue;
                        double H = sh[i * NK + k] + a.Inc[i * NK + k];
                        if (H >= (a.Ts[a.Mk[i] * NK + k] + lim + 2) * d) continue;
                        dst.Add(i);
                        if (fam != null) fam[i] = IsFamine(a, i, k, H, d);
                    }
                    int kk = k;
                    if (fam == null) dst.Sort((x, y) => { int c = IX(a, y * NK + kk).CompareTo(IX(a, x * NK + kk)); return c != 0 ? c : x.CompareTo(y); });
                    else dst.Sort((x, y) => { int c = fam[y].CompareTo(fam[x]); if (c != 0) return c; c = IX(a, y * NK + kk).CompareTo(IX(a, x * NK + kk)); return c != 0 ? c : x.CompareTo(y); });
                    foreach (int t in dst) Serve(r, t, k, fam != null && fam[t]);
                }
            }
            else
            {
                // kolejka wedlug potrzeby: wszystkie (cel, towar) wedlug pokrycia H / (T* x Dz) rosnaco; glod pierwszy; przy rownym - indeks malejaco, cel, towar
                var req = new List<Req>(nt * 4);
                for (int k = 0; k < NK; k++)
                {
                    double lim = a.Per[k] ? MaxPer : MaxDur;
                    for (int i = 0; i < nt; i++)
                    {
                        if (a.Siege[i] || !a.Review[i]) continue;
                        double d = dz[i * NK + k]; if (!(d > 0)) continue;
                        double T = a.Ts[a.Mk[i] * NK + k];
                        double H = sh[i * NK + k] + a.Inc[i * NK + k];
                        if (H >= (T + lim + 2) * d) continue;
                        req.Add(new Req { T = i, K = k, Cov = H / Math.Max(1e-9, T * d), Ix = IX(a, i * NK + k), Fam = a.Famine && IsFamine(a, i, k, H, d) });
                    }
                }
                req.Sort((x, y) =>
                {
                    int c = y.Fam.CompareTo(x.Fam); if (c != 0) return c;
                    c = x.Cov.CompareTo(y.Cov); if (c != 0) return c;
                    c = y.Ix.CompareTo(x.Ix); if (c != 0) return c;
                    c = x.T.CompareTo(y.T); return c != 0 ? c : x.K.CompareTo(y.K);
                });
                foreach (var q in req) Serve(r, q.T, q.K, q.Fam);
            }
        }

        /// <summary>Glod: towar glodu (zboze, ryby, mieso) i polka z droga ponizej 0.5 T* x Dz.</summary>
        private static bool IsFamine(In a, int t, int k, double H, double d)
        {
            if (a.FamineGood == null || k >= a.FamineGood.Length || !a.FamineGood[k]) return false;
            double T = a.Ts[a.Mk[t] * a.NK + k];
            return H < FamineCover * T * d;
        }

        /// <summary>Zakupy jednego celu t i towaru k (kandydaci, zysk kupca, kasa celu albo miejsce kredytu zrodla, wykonanie lotu).</summary>
        private static void Serve(Run r, int t, int k, bool fam)
        {
            var a = r.A; var st = r.St; int NK = a.NK;
            var offer = r.Offer; var sh = r.Sh; var cand = r.Cand;
            double lim = a.Per[k] ? MaxPer : MaxDur;
            double d = r.Dz[t * NK + k], T = a.Ts[a.Mk[t] * NK + k];
            double Hc = sh[t * NK + k] + a.Inc[t * NK + k];
            cand.Clear();
            var ct = a.Cand[t];
            for (int ci = 0; ci < ct.Length; ci++) AddCand(a, offer, cand, ct[ci], t, k, lim, false);
            bool far = false;
            if ((a.FarAlways || cand.Count == 0) && a.CandFar != null && a.CandFar[t] != null)
            {
                var cf = a.CandFar[t];
                if (!a.FarAlways)
                {
                    for (int ci = 0; ci < cf.Length && cand.Count < NCandFar; ci++)
                    {
                        float L0 = a.Days(cf[ci], t); if (!(L0 >= 0f)) continue;
                        if (L0 > lim) break;
                        AddCand(a, offer, cand, cf[ci], t, k, lim, true);
                    }
                    far = cand.Count > 0;
                }
                else
                {
                    // H2: dalsze miasta zawsze - najpierw tani test oferty, potem doby (lista wedlug dob: dalej niz zasieg - koniec) i wojna
                    int farN = 0;
                    for (int ci = 0; ci < cf.Length && farN < NCandFarAlways; ci++)
                    {
                        int s = cf[ci];
                        if (offer[s * NK + k] < 1) continue;
                        float L0 = a.Days(s, t); if (!(L0 >= 0f)) continue;
                        if (L0 > lim) break;
                        int before = cand.Count;
                        AddCand(a, offer, cand, s, t, k, lim, true);
                        if (cand.Count > before) { farN++; r.FarMark[s] = true; }
                    }
                }
            }
            // recenzja r4-kupcy: "celow" = rozne miasta glodu doby (miasto bez zboza i ryb liczone raz), zamowienia glodu (cel, towar) osobno
            if (fam) { st.FamineReqK++; if (!r.FamSeen[t]) { r.FamSeen[t] = true; st.FamineReq++; } }
            if (cand.Count == 0) { ClearFar(r); return; }
            cand.Sort((x, y) => x.Key != y.Key ? x.Key.CompareTo(y.Key) : x.Value.CompareTo(y.Value));
            if (a.Consign && a.DstOverdue != null && a.DstOverdue[t])
            {
                // H5: cel z rachunkiem starszym niz zwloka - bez nowych dostaw; zamowienie (pierwszy lot, jaki by poszedl) liczone jako "cel z zaleglym"
                foreach (var kv in cand)
                {
                    int s = kv.Value; float L = a.Days(s, t);
                    double q = Math.Min(offer[s * NK + k], (T + L + 2) * d - Hc);
                    if (q < Math.Max(5.0, 0.5 * d)) continue;
                    long n0 = (long)Math.Floor(q);
                    st.Ordered += n0; st.Overdue += n0;
                    if (fam) st.FamineOrd += n0;
                    break;
                }
                ClearFar(r); return;
            }
            bool shortStall = Hc < T * d;
            if (shortStall && !r.ShortSeen[t]) { r.ShortSeen[t] = true; if (a.Res[t] > 0) st.CofShort.Add(a.Gold[t] / a.Res[t]); }
            int destUnit = a.DestUnit != null ? a.DestUnit(t, k) : 0;
            int used = 0;
            long ord0 = -1, got0 = 0; bool poorHit = false, srcHit = false;
            foreach (var kv in cand)
            {
                if (used >= MaxSrc) break;
                int s = kv.Value; float L = a.Days(s, t);
                double want = (T + L + 2) * d - Hc;
                double q = Math.Min(offer[s * NK + k], want);
                if (q < Math.Max(5.0, 0.5 * d)) continue;
                int n = (int)Math.Floor(q);
                if (ord0 < 0) ord0 = n;
                bool sea = a.Sea(s, t);
                double tau = sea ? TauSea : TauLand;
                long cost = a.LotPrice(s, k, n);
                long fr = (long)Math.Round(cost * tau * L);
                int j0 = (int)Math.Min(int.MaxValue, got0);         // H4: sztuki wczesniejszych lotow tej doby do (t, k) - na polce celu przed tym lotem
                if (destUnit > 0)
                {
                    int tries = 0;
                    while (n > 0 && cost + fr > Val(a, t, k, j0, n, destUnit) && tries < 3)
                    {
                        n /= 2; tries++;
                        if (n < Math.Max(5.0, 0.5 * d)) { n = 0; break; }
                        cost = a.LotPrice(s, k, n); fr = (long)Math.Round(cost * tau * L);
                    }
                    if (n <= 0 || cost + fr > Val(a, t, k, j0, n, destUnit)) { st.NoProfit++; continue; }
                }
                // kasa celu (runda 3) albo miejsce kredytu zrodla (H5: towar + fracht, cel placi dopiero przy dostawie)
                double avail = a.Consign ? (a.SrcRoom != null && s < a.SrcRoom.Length ? a.SrcRoom[s] : 0.0) - r.SrcSpent[s]
                                         : (shortStall && a.Staple[k] ? r.BudgetShort[t] : r.Budget[t]) - r.Spent[t];
                if (cost + fr > avail)
                {
                    int nf = cost + fr > 0 && avail > 0 ? (int)Math.Floor(n * avail / (double)(cost + fr)) : 0;
                    if (a.Consign) srcHit = true; else poorHit = true;
                    if (nf < Math.Max(5.0, 0.5 * d)) continue;
                    n = nf;
                    cost = a.LotPrice(s, k, n); fr = (long)Math.Round(cost * tau * L);
                    if (cost + fr > avail + 1) continue;
                }
                if (n <= 0 || cost <= 0) continue;
                if (a.SampleAt != null && a.Sample != null && a.SampleAt(s, t, k)) a.Sample(s, k, n);
                int got = a.Ship(s, t, k, n, cost, fr, sea, L);
                if (got <= 0) continue;
                got0 += got;
                used++;
                if (a.FarAlways ? r.FarMark[s] : far) st.Far++;
                if (!a.Consign)
                {
                    double over = r.Spent[t] + cost + fr - Math.Max(r.Budget[t], r.Spent[t]);
                    if (over > 0) { st.ResLots++; st.ResGold += (long)Math.Round(over); }
                }
                offer[s * NK + k] -= got; sh[s * NK + k] -= got; Hc += got;
                if (a.Consign) r.SrcSpent[s] += cost + fr; else r.Spent[t] += cost + fr;
            }
            if (ord0 > 0)
            {
                st.Ordered += ord0;
                long miss = Math.Max(0, ord0 - got0);
                if (poorHit) st.Poor += miss;
                if (srcHit) st.SrcCap += miss;
                if (poorHit || srcHit) st.PoorByK[k] += miss;
                if (fam) { st.FamineOrd += ord0; st.FamineGot += got0; }
            }
            ClearFar(r);
        }

        /// <summary>Wartosc n sztuk u celu po marzy: H4 (DestValue, za j0 sztukami wczesniejszych lotow doby) albo runda 3 - n x cena polki celu x 0.97 (bez j0, bit w bit).</summary>
        private static long Val(In a, int t, int k, int j0, int n, int destUnit)
        {
            if (a.DestValue != null) return a.DestValue(t, k, j0, n);
            return (long)Math.Floor(n * (double)destUnit * (1.0 - Margin));
        }

        /// <summary>H4: suma cen n sztuk dowiezionych na polke celu - wzor ceny gry przy polce rosnacej o kazda sztuke: p(j) = p0 x (A0 / (A0 + c j))^0.6
        /// (A0 = 0.1 x podaz + 0.04 x wartosc polki + 2, c = 0.04 x wartosc sztuki na polce), kazda sztuka najwyzej cap (3 x cena startu); calka zamknieta (bez marzy).</summary>
        internal static double DestIntegral(double p0, double A0, double c, double cap, int n)
        {
            if (n <= 0 || !(p0 > 0)) return 0.0;
            if (!(A0 > 0) || !(c > 0)) return n * (cap > 0 ? Math.Min(p0, cap) : p0);
            double sum = 0.0, j0 = 0.0;
            if (cap > 0 && p0 > cap)
            {
                // sztuki az do j*, gdzie cena wzoru schodzi do sufitu: p(j*) = cap
                double js = A0 * (Math.Pow(p0 / cap, 1.0 / 0.6) - 1.0) / c;
                if (js >= n) return n * cap;
                sum += cap * js; j0 = js;
            }
            sum += p0 * Math.Pow(A0, 0.6) / (0.4 * c) * (Math.Pow(A0 + c * n, 0.4) - Math.Pow(A0 + c * j0, 0.4));
            return Math.Max(sum, n);                       // najmniej 1 zl za sztuke (cena gry >= 1)
        }

        /// <summary>Recenzja r4-kupcy (H4): suma cen sztuk j0 .. j0 + n - 1 dowiezionych na polke celu za j0 sztukami wczesniejszych lotow doby - ta sama calka zamknieta
        /// co DestIntegral od j0 (sufit cap do j*, potem wzor), najmniej 1 zl za sztuke; j0 <= 0 - DestIntegral bit w bit.</summary>
        internal static double DestIntegralFrom(double p0, double A0, double c, double cap, int j0, int n)
        {
            if (j0 <= 0) return DestIntegral(p0, A0, c, cap, n);
            if (n <= 0 || !(p0 > 0)) return 0.0;
            if (!(A0 > 0) || !(c > 0)) return n * (cap > 0 ? Math.Min(p0, cap) : p0);
            double ja = j0, jb = (double)j0 + n, lo = ja, sum = 0.0;
            if (cap > 0 && p0 > cap)
            {
                double js = A0 * (Math.Pow(p0 / cap, 1.0 / 0.6) - 1.0) / c;
                if (js >= jb) return n * cap;
                if (js > ja) { sum += cap * (js - ja); lo = js; }
            }
            sum += p0 * Math.Pow(A0, 0.6) / (0.4 * c) * (Math.Pow(A0 + c * jb, 0.4) - Math.Pow(A0 + c * lo, 0.4));
            return Math.Max(sum, n);
        }

        /// <summary>T9b (czysty C#, test portu): suma cen n sztuk z polki zrodla przy cenie sztuki Q(k) = ceil(q x (D / (0.1 S + 0.04 max(0, V - k x wv) + 2))^0.6)
        /// niemalejacej od p0 = Q(0) do pl = Q(n-1) (wolajacy sprawdza obie ceny gra). Poziomy cen L = p0 .. pl-1: ostatnia sztuka z cena <= L z odwrotnosci wzoru
        /// (jeden Pow), granica poprawiana Q obok (najwyzej 6 krokow - inaczej false i wolajacy liczy trzy ceny).</summary>
        internal static bool LotSteps(long p0, long pl, int n, double q, double ins0, double sup, double dem, int wv, Func<int, long> Q, out long sum)
        {
            sum = 0;
            if (n <= 0 || wv <= 0 || !(q > 0)) return false;
            if (pl == p0) { sum = p0 * (long)n; return true; }
            if (pl < p0 || pl - p0 > 128) return false;
            long tot = 0; int k0 = 0;
            for (long L = p0; L < pl; L++)
            {
                // ostatnia sztuka z cena <= L: czynnik <= L / q  <=>  0.1 S + 0.04 V + 2 >= D / (L / q)^(1/0.6)  <=>  V >= (D / y^(1/0.6) - 0.1 S - 2) / 0.04
                double y = L / q; int kL;
                if (y >= 10.0) kL = n - 1;
                else
                {
                    double vNeed = (dem / Math.Pow(y, 1.0 / 0.6) - 0.1 * sup - 2.0) / 0.04;
                    double kk = (ins0 - vNeed) / wv;
                    kL = double.IsNaN(kk) ? k0 - 1 : (kk >= n ? n - 1 : (kk < -1 ? -1 : (int)Math.Floor(kk)));
                }
                if (kL < k0 - 1) kL = k0 - 1;
                if (kL > n - 2) kL = n - 2;
                int g = 0;
                while (kL >= k0 && Q(kL) > L) { kL--; if (++g > 6) return false; }
                while (kL + 1 <= n - 1 && Q(kL + 1) <= L) { kL++; if (++g > 6) return false; }
                if (kL >= k0) { tot += L * (long)(kL - k0 + 1); k0 = kL + 1; }
            }
            tot += pl * (long)(n - k0);
            sum = tot;
            return true;
        }

        private static void ClearFar(Run r)
        {
            if (!r.A.FarAlways) return;
            foreach (var kv in r.Cand) r.FarMark[kv.Value] = false;
        }

        private static void AddCand(In a, double[] offer, List<KeyValuePair<double, int>> cand, int s, int t, int k, double lim, bool far)
        {
            int NK = a.NK;
            if (offer[s * NK + k] < 1 || a.Siege[s]) return;
            float L = a.Days(s, t); if (!(L >= 0f) || L > lim) return;
            if (a.War != null && a.War(s, t)) return;
            double tau = a.Sea(s, t) ? TauSea : TauLand;
            double c = IX(a, s * NK + k) * (1.0 + tau * L);
            if (IX(a, t * NK + k) < c * (1.0 + Margin)) return;
            cand.Add(new KeyValuePair<double, int>(c, s));
        }
    }
}
