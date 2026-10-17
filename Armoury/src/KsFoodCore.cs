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
    /// </summary>
    internal static class KsFoodCore
    {
        internal const double TauLand = KsCore.CorrTauLand, TauSea = KsCore.CorrTauSea, Margin = 0.03, SrcKeep = 1.25, OfferShare = 0.5, KxUp = 1.10, KxDown = 0.98, KxMax = 2.5, CofferShare = 0.15;
        internal const double ReserveFloorShort = 0.5, ReserveDayShort = 0.05, MaxDur = 30.0, MaxPer = 7.0;
        internal const int MaxSrc = 3, NCandFar = 4, Keep = 5;

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
        }

        /// <summary>Liczniki doby: zamowione, kasa celu za mala, loty bez zysku, z dalszych miast, z zapasu kupcow (loty, zl), kasy miast z brakiem / zapas.</summary>
        internal sealed class Stats
        {
            public long Ordered, Poor, NoProfit, ResGold; public int Far, ResLots;
            public readonly List<double> CofShort = new List<double>();
        }

        internal static void Decide(In a, Stats st)
        {
            int nt = a.NT, NK = a.NK;
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
            for (int i = 0; i < nt; i++)
                for (int k = 0; k < NK; k++)
                {
                    if (a.Siege[i]) continue;
                    int o = i * NK + k;
                    double sur = sh[o] - SrcKeep * a.Ts[a.Mk[i] * NK + k] * dzB[o] - Keep;
                    if (sur > 1) offer[o] = Math.Floor(OfferShare * sur);
                }
            var dst = new List<int>(nt);
            var cand = new List<KeyValuePair<double, int>>(16);
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
                }
                int kk = k;
                dst.Sort((x, y) => { int c = a.Ix[y * NK + kk].CompareTo(a.Ix[x * NK + kk]); return c != 0 ? c : x.CompareTo(y); });
                foreach (int t in dst)
                {
                    double d = dz[t * NK + k], T = a.Ts[a.Mk[t] * NK + k];
                    double Hc = sh[t * NK + k] + a.Inc[t * NK + k];
                    cand.Clear();
                    var ct = a.Cand[t];
                    for (int ci = 0; ci < ct.Length; ci++) AddCand(a, offer, cand, ct[ci], t, k, lim, false);
                    bool far = false;
                    if (cand.Count == 0 && a.CandFar != null && a.CandFar[t] != null)
                    {
                        var cf = a.CandFar[t];
                        for (int ci = 0; ci < cf.Length && cand.Count < NCandFar; ci++)
                        {
                            float L0 = a.Days(cf[ci], t); if (!(L0 >= 0f)) continue;
                            if (L0 > lim) break;
                            AddCand(a, offer, cand, cf[ci], t, k, lim, true);
                        }
                        far = cand.Count > 0;
                    }
                    if (cand.Count == 0) continue;
                    cand.Sort((x, y) => x.Key != y.Key ? x.Key.CompareTo(y.Key) : x.Value.CompareTo(y.Value));
                    bool shortStall = Hc < T * d;
                    if (shortStall && !shortSeen[t]) { shortSeen[t] = true; if (a.Res[t] > 0) st.CofShort.Add(a.Gold[t] / a.Res[t]); }
                    int destUnit = a.DestUnit != null ? a.DestUnit(t, k) : 0;
                    int used = 0;
                    long ord0 = -1, got0 = 0; bool poorHit = false;
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
                        if (destUnit > 0)
                        {
                            int tries = 0;
                            while (n > 0 && cost + fr > (long)Math.Floor(n * (double)destUnit * (1.0 - Margin)) && tries < 3)
                            {
                                n /= 2; tries++;
                                if (n < Math.Max(5.0, 0.5 * d)) { n = 0; break; }
                                cost = a.LotPrice(s, k, n); fr = (long)Math.Round(cost * tau * L);
                            }
                            if (n <= 0 || cost + fr > (long)Math.Floor(n * (double)destUnit * (1.0 - Margin))) { st.NoProfit++; continue; }
                        }
                        double avail = (shortStall && a.Staple[k] ? budgetShort[t] : budget[t]) - spent[t];
                        if (cost + fr > avail)
                        {
                            int nf = cost + fr > 0 && avail > 0 ? (int)Math.Floor(n * avail / (double)(cost + fr)) : 0;
                            poorHit = true;
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
                        if (far) st.Far++;
                        double over = spent[t] + cost + fr - Math.Max(budget[t], spent[t]);
                        if (over > 0) { st.ResLots++; st.ResGold += (long)Math.Round(over); }
                        offer[s * NK + k] -= got; sh[s * NK + k] -= got; Hc += got; spent[t] += cost + fr;
                    }
                    if (ord0 > 0) { st.Ordered += ord0; if (poorHit) st.Poor += Math.Max(0, ord0 - got0); }
                }
            }
        }

        private static void AddCand(In a, double[] offer, List<KeyValuePair<double, int>> cand, int s, int t, int k, double lim, bool far)
        {
            int NK = a.NK;
            if (offer[s * NK + k] < 1 || a.Siege[s]) return;
            float L = a.Days(s, t); if (!(L >= 0f) || L > lim) return;
            if (a.War != null && a.War(s, t)) return;
            double tau = a.Sea(s, t) ? TauSea : TauLand;
            double c = a.Ix[s * NK + k] * (1.0 + tau * L);
            if (a.Ix[t * NK + k] < c * (1.0 + Margin)) return;
            cand.Add(new KeyValuePair<double, int>(c, s));
        }
    }
}
