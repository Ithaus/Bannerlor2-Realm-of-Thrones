using System;
using System.Collections.Generic;

namespace Armoury
{
    /// <summary>
    /// K1 - DOZBRAJANIE (Jeff 09.10: "chce oba mechanizmy: ze za swoje sami sie zbroja z lupow i zoldu, i ja rowniez moge ich
    /// dozbroic na zasadzie wrzuc im lepsza zbroje, a oni wydaja mi swoja gorsza jako wymiane"). Czyste funkcje BEZ typow gry -
    /// jedna implementacja dla druzyny gracza, zalog i progu K1-C; da sie ja sprawdzic poza gra (docs/paczki/K1-dozbrajanie.md C5).
    ///  - Fit: dopasowanie ludzi (od najwyzszego skilla) do sztuk (wymog malejaco, sila, jakosc, id, mod) - ten sam algorytm co
    ///    dotad QuartermasterLaw.FitFor (porzadek calkowity z 19.09, wynik nie zalezy od kolejnosci rostera).
    ///  - AllocateOwn: ksiega wkladow gracza jest per id; przy jednym id obejmuje NAJGORSZE egzemplarze (watpliwosc na korzysc ludzi).
    ///  - Swap (B2): noszony wklad gracza przechodzi na ludzi; za kazdy, ktory WYPARL sztuke ludzi, gracz dostaje jedna najgorsza
    ///    wolna UZYTECZNA sztuke ludzi tego typu (Jeff 30.08); wklad, ktory wypelnil puste rece, przechodzi bez zwrotu (Jeff 14.09);
    ///    nienoszony wklad zostaje gracza; reszta wolnych sztuk ludzi nalezy do LUDZI.
    ///  - Assign (A7): przydzial sztuk ludzi do koszykow typ x tier wzorca - kandydat do wymiany na lepsza.
    ///  - SurplusPlan (A9): co ludzie oddaja kupcowi - najpierw sztuki, ktorych nikt nie udzwignie, potem najgorsze uzyteczne ponad
    ///    komplet + zapas.
    /// </summary>
    internal static class SwapMath
    {
        internal sealed class Piece
        {
            public string Id = "", Mod = "";
            public int Tier = 1;          // tier wyswietlany 1..6
            public long Power;            // sila: RangedRank.Key (naciag RBM dla lukow i kusz, Effectiveness dla reszty)
            public int Req;               // wymog (Difficulty) - porzadek dopasowania
            public float Quality = 1f;    // PriceMultiplier modyfikatora (brak = 1)
            public bool Wreck;            // wrak - zawsze najgorszy w swoim id
            public bool Barred;           // straz bitewna tego nie wyda (unikaty, klingi lore, sprzet umarlych) - poza dopasowaniem
            public bool NoSell;           // unikat - kupiec nie bierze hurtem
            public int Total, Own;        // ile sztuk tego egzemplarza, ile z tego gracza
            public object Tag;            // adapter: EquipmentElement / ItemObject
            // wyniki
            public int UsedBefore, Used, OwnWorn, Back, Target, Sell, Index;
            public int MenTotal { get { return Math.Max(0, Total - Own); } }
            public int MenWorn { get { return Math.Min(Used, MenTotal); } }   // przy identycznym egzemplarzu ludzie nosza najpierw swoje
            public int MenFree { get { return MenTotal - MenWorn; } }
            public string Key { get { return Id + "|" + Mod; } }
        }

        internal sealed class SwapResult
        {
            public int Need, Unfit, Worn, Displaced, X, Filled, KeptOwn, MenSpare;
            public bool[] UnfitMan;       // kto (indeks czlowieka) zostal bez uzytecznej sztuki w dopasowaniu PO
        }

        // ------------------------------------------------------------ porzadki
        /// <summary>Porzadek dopasowania (jak FitFor): wymog malejaco, sila malejaco, jakosc malejaco, id, mod.</summary>
        internal static int FitOrder(Piece a, Piece b)
        {
            int d = b.Req.CompareTo(a.Req); if (d != 0) return d;
            d = b.Power.CompareTo(a.Power); if (d != 0) return d;
            d = b.Quality.CompareTo(a.Quality); if (d != 0) return d;
            d = string.CompareOrdinal(a.Id, b.Id); if (d != 0) return d;
            return string.CompareOrdinal(a.Mod, b.Mod);
        }

        /// <summary>"Gorsza najpierw" (Jeff 30.08: "nizszy tier, przy rownym nizsza wartosc"): tier, sila, wrak, jakosc, id, mod - porzadek calkowity.</summary>
        internal static int WorseFirst(Piece a, Piece b)
        {
            int d = a.Tier.CompareTo(b.Tier); if (d != 0) return d;
            d = a.Power.CompareTo(b.Power); if (d != 0) return d;
            d = (b.Wreck ? 1 : 0).CompareTo(a.Wreck ? 1 : 0); if (d != 0) return d;
            d = a.Quality.CompareTo(b.Quality); if (d != 0) return d;
            d = string.CompareOrdinal(a.Id, b.Id); if (d != 0) return d;
            return string.CompareOrdinal(a.Mod, b.Mod);
        }

        // ------------------------------------------------------------ udzwig (z pamiecia: ta sama grupa ludzi i ta sama sztuka)
        private sealed class Meets
        {
            private readonly Func<int, Piece, bool> _f; private readonly sbyte[] _c; private readonly int _n;
            internal Meets(int groups, List<Piece> sorted, Func<int, Piece, bool> f)
            {
                _f = f; _n = sorted.Count; _c = new sbyte[Math.Max(1, groups) * Math.Max(1, _n)];
                for (int i = 0; i < sorted.Count; i++) sorted[i].Index = i;
            }
            internal bool Ok(int g, Piece p)
            {
                int k = g * _n + p.Index;
                if (k < 0 || k >= _c.Length) return _f(g, p);
                if (_c[k] == 0) _c[k] = (sbyte)(_f(g, p) ? 1 : 2);
                return _c[k] == 1;
            }
        }

        /// <summary>Dopasowanie: kazdy czlowiek (group[m] - jego grupa, ludzie posortowani od najwyzszego skilla) bierze pierwsza
        /// w porzadku FitOrder sztuke, ktora udzwignie i ktorej jeszcze starcza. before=true - tylko sztuki ludzi (UsedBefore),
        /// false - cala polka (Used). Zwraca liczbe ludzi bez uzytecznej sztuki.</summary>
        private static int FitCore(int[] group, Meets ok, List<Piece> sorted, bool before, bool[] unfitOut)
        {
            int unfit = 0;
            foreach (var p in sorted) { if (before) p.UsedBefore = 0; else p.Used = 0; }
            for (int m = 0; m < group.Length; m++)
            {
                Piece pick = null;
                for (int i = 0; i < sorted.Count; i++)
                {
                    var p = sorted[i];
                    if (p.Barred) continue;
                    if ((before ? p.UsedBefore : p.Used) >= (before ? p.MenTotal : p.Total)) continue;
                    if (!ok.Ok(group[m], p)) continue;
                    pick = p; break;
                }
                if (pick == null) { unfit++; if (unfitOut != null) unfitOut[m] = true; }
                else if (before) pick.UsedBefore++; else pick.Used++;
            }
            return unfit;
        }

        /// <summary>Dopasowanie calej polki (Used). Sortuje liste w porzadku FitOrder. Zwraca liczbe ludzi bez uzytecznej sztuki.</summary>
        internal static int Fit(int[] group, int groups, Func<int, Piece, bool> meets, List<Piece> pieces)
        {
            pieces.Sort(FitOrder);
            return FitCore(group, new Meets(groups, pieces, meets), pieces, false, null);
        }

        /// <summary>Czy ktos z ludzi tego typu udzwignie sztuke - wystarczy najsilniejszy (ludzie posortowani od najwyzszego skilla).</summary>
        internal static bool Usable(int[] group, Func<int, Piece, bool> meets, Piece p)
        {
            return group != null && group.Length > 0 && !p.Barred && meets(group[0], p);
        }

        // ------------------------------------------------------------ ksiega gracza na egzemplarze
        /// <summary>Ksiega wkladow gracza (per id) rozdzielona na egzemplarze tego id: NAJGORSZE najpierw.</summary>
        internal static void AllocateOwn(List<Piece> pieces, Func<string, int> bookOf)
        {
            var byId = new Dictionary<string, List<Piece>>();
            foreach (var p in pieces)
            {
                List<Piece> l;
                if (!byId.TryGetValue(p.Id, out l)) byId[p.Id] = l = new List<Piece>();
                l.Add(p);
            }
            foreach (var kv in byId)
            {
                kv.Value.Sort(WorseFirst);
                int left = Math.Max(0, bookOf(kv.Key));
                foreach (var p in kv.Value) { p.Own = Math.Min(p.Total, left); left -= p.Own; }
            }
        }

        // ------------------------------------------------------------ B2: wymiana 1:1
        /// <summary>
        /// Wymiana (B2). Dwa dopasowania tym samym algorytmem: PRZED (tylko sztuki ludzi) i PO (cala polka). Na egzemplarz:
        /// OwnWorn = max(0, Used - (Total - Own)); wyparte = suma max(0, UsedBefore - MenWorn); X = min(suma OwnWorn, wyparte).
        /// Graczowi: (Own - OwnWorn) oraz X najgorszych wolnych uzytecznych sztuk ludzi (Back). Wykluczone (Barred) - graczowi w calosci.
        /// oneForOne=false: regula sprzed K1 - wszystko nienoszone dla gracza (Target = Total - Used).
        /// </summary>
        internal static SwapResult Swap(int[] group, int groups, Func<int, Piece, bool> meets, List<Piece> pieces, bool oneForOne)
        {
            var r = new SwapResult { Need = group.Length, UnfitMan = new bool[group.Length] };
            pieces.Sort(FitOrder);
            var ok = new Meets(groups, pieces, meets);
            if (oneForOne) FitCore(group, ok, pieces, true, null);
            r.Unfit = FitCore(group, ok, pieces, false, r.UnfitMan);
            foreach (var p in pieces) { p.OwnWorn = Math.Max(0, p.Used - p.MenTotal); p.Back = 0; r.Worn += p.OwnWorn; }
            if (!oneForOne)
            {
                foreach (var p in pieces) p.Target = p.Barred ? p.Total : p.Total - p.Used;
                r.Filled = r.Worn;
                return r;
            }
            foreach (var p in pieces) r.Displaced += Math.Max(0, p.UsedBefore - p.MenWorn);
            int x = Math.Min(r.Worn, r.Displaced);
            if (x > 0 && group.Length > 0)
            {
                var cand = new List<Piece>();
                foreach (var p in pieces) if (!p.Barred && p.MenFree > 0 && ok.Ok(group[0], p)) cand.Add(p);
                cand.Sort(WorseFirst);
                foreach (var p in cand)
                {
                    if (x <= 0) break;
                    int k = Math.Min(x, p.MenFree);
                    p.Back = k; x -= k; r.X += k;
                }
            }
            r.Filled = r.Worn - r.X;
            foreach (var p in pieces)
            {
                p.Target = p.Barred ? p.Total : (p.Own - p.OwnWorn) + p.Back;
                if (!p.Barred) { r.KeptOwn += p.Own - p.OwnWorn; r.MenSpare += p.MenFree - p.Back; }
            }
            return r;
        }

        // ------------------------------------------------------------ A7: koszyki typ x tier
        internal sealed class Bucket
        {
            public int Tier;                                                // tier wzorca
            public bool Mounted;                                            // ktos z koszyka jezdzi - bron z siodla
            public List<KeyValuePair<int, int>> Men = new List<KeyValuePair<int, int>>();   // (grupa, ilu) od najwyzszego skilla
            public int Size, Filled;
            public bool Full { get { return Filled >= Size; } }
        }

        internal sealed class Slot
        {
            public Bucket Bucket; public int Group; public Piece Piece; public int N;
        }

        /// <summary>Przydzial (A7): koszyki od najwyzszego tieru, w koszyku ludzie od najwyzszego skilla, kazdy bierze NAJLEPSZA
        /// pozostala sztuke ludzi, ktora udzwignie. Czego zaden koszyk nie przyjmie - zapas. Zwraca obsadzone sloty (grupy).</summary>
        internal static List<Slot> Assign(List<Bucket> buckets, List<Piece> pieces, Func<int, Piece, bool> meets)
        {
            var slots = new List<Slot>();
            var best = new List<Piece>(pieces);
            best.Sort((a, b) => WorseFirst(b, a));
            var left = new int[best.Count];
            for (int i = 0; i < best.Count; i++) { left[i] = best[i].Barred ? 0 : best[i].MenTotal; best[i].Index = i; }
            buckets.Sort((a, b) => b.Tier.CompareTo(a.Tier));
            foreach (var b in buckets)
            {
                b.Filled = 0;
                foreach (var g in b.Men)
                {
                    int need = g.Value;
                    for (int i = 0; i < best.Count && need > 0; i++)
                    {
                        if (left[i] <= 0 || !meets(g.Key, best[i])) continue;
                        int k = Math.Min(need, left[i]);
                        left[i] -= k; need -= k; b.Filled += k;
                        slots.Add(new Slot { Bucket = b, Group = g.Key, Piece = best[i], N = k });
                    }
                }
            }
            return slots;
        }

        // ------------------------------------------------------------ A9: nadwyzki po dopasowaniu
        /// <summary>Po Fit (Used na calej polce): Sell = co ludzie oddaja kupcowi. Najpierw ich wolne sztuki, ktorych nikt nie
        /// udzwignie (i wykluczone z bitwy), potem najgorsze wolne uzyteczne ponad komplet + keepPercent. Zostaja najlepsze wolne
        /// uzyteczne. Unikaty (NoSell) nigdy. Zwraca liczbe sztuk do sprzedazy.</summary>
        internal static int SurplusPlan(List<Piece> pieces, int need, float keepPercent, Func<Piece, bool> usable)
        {
            int used = 0, sell = 0;
            foreach (var p in pieces) { p.Sell = 0; if (!p.Barred) used += p.Used; }
            int keep = (int)Math.Ceiling(need * (1f + Math.Max(0f, keepPercent) / 100f));
            int spare = Math.Max(0, keep - used);
            var free = new List<Piece>();
            foreach (var p in pieces)
            {
                if (p.NoSell || p.MenFree <= 0) continue;
                if (p.Barred || !usable(p)) { p.Sell = p.MenFree; sell += p.Sell; continue; }
                free.Add(p);
            }
            free.Sort((a, b) => WorseFirst(b, a));   // najlepsze najpierw - zostaja
            foreach (var p in free)
            {
                int k = Math.Min(spare, p.MenFree);
                spare -= k;
                p.Sell = p.MenFree - k; sell += p.Sell;
            }
            return sell;
        }

        // ------------------------------------------------------------ A7: czy kupic lepsza
        internal const int UpOk = 0, UpNotBetter = 1, UpAboveGrade = 2, UpNoLift = 3, UpNoMoney = 4;

        /// <summary>A7: czy nowa sztuka moze zastapic stara w koszyku. Tier nie wyzszy niz tier koszyka ("w swoim stopniu"); zawsze
        /// silniejsza, a do tego o prog (gain, np. 0.10) albo wyzszego tieru; ktos z koszyka ja udzwignie (lift); cena w budzecie.</summary>
        internal static int UpgradeVerdict(int bucketTier, int oldTier, long oldPower, int newTier, long newPower, double gain, bool lift, int price, int budget)
        {
            if (newTier > bucketTier) return UpAboveGrade;
            if (newPower <= oldPower) return UpNotBetter;
            if ((double)newPower < oldPower + Math.Abs((double)oldPower) * gain && newTier <= oldTier) return UpNotBetter;
            if (!lift) return UpNoLift;
            if (price <= 0 || price > budget) return UpNoMoney;
            return UpOk;
        }

        /// <summary>A7: najwiecej sily za denara netto - (sila nowej - sila starej) / (cena nowej - skup starej); netto <= 0 - darmowa poprawa.</summary>
        internal static double UpgradeScore(long oldPower, long newPower, int price, int oldSell)
        {
            double net = (double)price - oldSell;
            return net >= 1 ? (newPower - oldPower) / net : (newPower - oldPower) * 1e6;
        }

        /// <summary>A8: stara sztuka idzie do kupca tylko, gdy kasa miasta ja oplaci; inaczej zostaje w zbrojowni jako zapas.</summary>
        internal static bool MerchantPays(int townGold, int unit) { return unit > 0 && townGold >= unit; }

        /// <summary>Niezmiennik B7.3: czesc gracza nigdy ujemna i nigdy ponad liczbe sztuk egzemplarza.</summary>
        internal static bool TargetsSane(List<Piece> pieces)
        {
            foreach (var p in pieces) if (p.Target < 0 || p.Target > p.Total) return false;
            return true;
        }
    }
}
