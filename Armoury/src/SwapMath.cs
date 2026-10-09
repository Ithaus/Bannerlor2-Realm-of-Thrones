using System;
using System.Collections.Generic;

namespace Armoury
{
    /// <summary>
    /// K1 - DOZBRAJANIE (Jeff 09.10: "chce oba mechanizmy: ze za swoje sami sie zbroja z lupow i zoldu, i ja rowniez moge ich
    /// dozbroic na zasadzie wrzuc im lepsza zbroje, a oni wydaja mi swoja gorsza jako wymiane"). Czyste funkcje BEZ typow gry -
    /// jedna implementacja dla druzyny gracza, zalog i progu K1-C; da sie ja sprawdzic poza gra (docs/paczki/K1-dozbrajanie.md C5).
    ///  - Fit: dopasowanie ludzi (od najwyzszego skilla) do sztuk (wymog malejaco, sila, jakosc, id, mod) - ten sam algorytm co
    ///    dotad QuartermasterLaw.FitFor (porzadek calkowity z 19.09, wynik nie zalezy od kolejnosci rostera). Liczy LICZBE ludzi
    ///    ze sztuka (nadwyzki, braki, prog K1-C); wymiana z graczem (Swap) liczy tyle samo ludzi, ale "najlepsze najpierw" (Greedy).
    ///  - AllocateOwn: ksiega wkladow gracza jest per id; przy jednym id obejmuje NAJGORSZE egzemplarze (watpliwosc na korzysc ludzi).
    ///  - Swap (B2): noszony wklad gracza przechodzi na ludzi; za kazdy, ktory WYPARL sztuke ludzi, gracz dostaje jedna najgorsza
    ///    wolna UZYTECZNA sztuke ludzi tego typu (Jeff 30.08); wklad, ktory wypelnil puste rece, przechodzi bez zwrotu (Jeff 14.09);
    ///    nienoszony wklad zostaje gracza; reszta wolnych sztuk ludzi nalezy do LUDZI.
    ///    K1 (Jeff 09.10 04:40, WYMIANA): "jesli dasz cos taniego, ale gorszego od tego, co maja - oni tego tez nie biora, zostaje po
    ///    prostu w okienku DTE". Wklad wypiera sztuke ludzi TYLKO, gdy jest od niej LEPSZY (Better: tier, sila, wrak, jakosc) i ktos
    ///    go udzwignie; gorszy, rowny albo za trudny zostaje gracza; puste rece wypelnia kazda uzyteczna sztuka. Liczy to dopasowanie
    ///    "najlepsze najpierw" (Greedy) - nigdy nie daje czlowiekowi gorszej sztuki w miejsce lepszej, ktora mial.
    ///  - Assign (A7): przydzial sztuk ludzi do koszykow typ x tier wzorca - kandydat do wymiany na lepsza; sufit zakupu CeilingTier.
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
            // K1c (przeglad K1b): nienoszona czesc gracza TEGO egzemplarza i powod (jak SwapResult.KeptWorse / KeptHard) - komunikat
            // po zamknieciu mowi o wkladach z tej sesji, nie o calym schowku
            public int KeptWorse, KeptHard;
            public int MenTotal { get { return Math.Max(0, Total - Own); } }
            public int MenWorn { get { return Math.Min(Used, MenTotal); } }   // przy identycznym egzemplarzu ludzie nosza najpierw swoje
            public int MenFree { get { return MenTotal - MenWorn; } }
            public string Key { get { return Id + "|" + Mod; } }
        }

        internal sealed class SwapResult
        {
            public int Need, Unfit, Worn, Displaced, X, Filled, KeptOwn, MenSpare;
            // K1 (Jeff 09.10 04:40): nienoszone sztuki gracza zostaja w oknie - KeptWorse: nikt ich nie chce (kazdy, kto je udzwignie,
            // ma rowna albo lepsza, albo nikt ich nie potrzebuje), KeptHard: ten, komu bylyby lepsze, ich nie udzwignie (wymog)
            public int KeptWorse, KeptHard;
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

        /// <summary>K1 (Jeff 09.10 04:40): czy a jest LEPSZA od b - ten sam porzadek co WorseFirst (tier, sila, wrak, jakosc), ale BEZ
        /// rozstrzygania po id: dwie sztuki rowne w tych czterech sa rowne (rowna sztuka gracza nie wypiera sztuki ludzi).
        /// &gt;0 - a lepsza, 0 - rowne, &lt;0 - a gorsza.</summary>
        internal static int Better(Piece a, Piece b)
        {
            int d = a.Tier.CompareTo(b.Tier); if (d != 0) return d;
            d = a.Power.CompareTo(b.Power); if (d != 0) return d;
            d = (b.Wreck ? 1 : 0).CompareTo(a.Wreck ? 1 : 0); if (d != 0) return d;
            return a.Quality.CompareTo(b.Quality);
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
            // K1 (przeglad, koszt): ludzie jednej grupy stoja w group[] obok siebie (MenOf) - nastepny z tej samej grupy zaczyna od sztuki,
            // ktora wzial poprzedni: wczesniejsze byly dla tej grupy wyczerpane albo nie do udzwigniecia i takie zostaja (wynik ten sam,
            // koszt O(grupy x sztuki + ludzie) zamiast O(ludzie x sztuki))
            int lastG = -1, from = 0;
            for (int m = 0; m < group.Length; m++)
            {
                if (group[m] != lastG) { lastG = group[m]; from = 0; }
                Piece pick = null;
                int i = from;
                for (; i < sorted.Count; i++)
                {
                    var p = sorted[i];
                    if (p.Barred) continue;
                    if ((before ? p.UsedBefore : p.Used) >= (before ? p.MenTotal : p.Total)) continue;
                    if (!ok.Ok(group[m], p)) continue;
                    pick = p; break;
                }
                from = i;
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

        /// <summary>K1 (Jeff 09.10 04:40): dopasowanie calej polki "najlepsze najpierw" (Greedy - ten sam algorytm co wymiana), wynik w Used.
        /// Nadwyzki ludzi gracza (przy wymianie 1:1), lordow AI i zalog licza wolne sztuki tak jak wymiana: ludzie nosza najlepsze, co
        /// udzwigna, a do kupca idzie najgorsze. Dotad FitCore stawial wymog przed sila: ciezki gorszy grat (albo gorsza sztuka gracza) byl
        /// "noszony", a lepsza lzejsza sztuka ludzi szla do kupca. Zwraca liczbe ludzi bez sztuki (ta sama co Fit - najwiecej mozliwych).</summary>
        internal static int FitBest(int[] group, int groups, Func<int, Piece, bool> meets, List<Piece> pieces)
        {
            pieces.Sort(FitOrder);
            var gr = new Greedy(group, groups, new Meets(groups, pieces, meets), pieces);
            gr.Run(true);
            return gr.MarkUnfit(null);
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
        /// Wymiana (B2). Dwa dopasowania "najlepsze najpierw" (Greedy): PRZED (tylko sztuki ludzi) i PO (cala polka). Na egzemplarz:
        /// OwnWorn = max(0, Used - (Total - Own)); wyparte = suma max(0, UsedBefore - MenWorn); X = min(suma OwnWorn, wyparte).
        /// Graczowi: (Own - OwnWorn) oraz X najgorszych wolnych uzytecznych sztuk ludzi (Back). Wykluczone (Barred) - graczowi w calosci.
        /// K1 (Jeff 09.10 04:40, WYMIANA): kazda wyparta sztuka ludzi ma swoj noszony wklad od niej LEPSZY (Better &gt; 0), a wklad gorszy
        /// albo rowny wchodzi tylko w puste rece - wynika to z kolejnosci Greedy (lepsze najpierw, przy rownych sztuki ludzi przed sztukami
        /// gracza; w PO sztuki ludzi sa podzbiorem tych z PRZED). Dotychczasowy przypadek "wklad wyparl lepsza sztuke ludzi" (NotBetter)
        /// nie moze sie zdarzyc. Zwrot (Back) to X najgorszych wolnych - kazdy nie lepszy od swojej wypartej.
        /// oneForOne=false: regula sprzed K1 - dopasowanie FitCore i wszystko nienoszone dla gracza (Target = Total - Used).
        /// </summary>
        internal static SwapResult Swap(int[] group, int groups, Func<int, Piece, bool> meets, List<Piece> pieces, bool oneForOne)
        {
            var r = new SwapResult { Need = group.Length, UnfitMan = new bool[group.Length] };
            pieces.Sort(FitOrder);
            var ok = new Meets(groups, pieces, meets);
            foreach (var p in pieces) { p.KeptWorse = 0; p.KeptHard = 0; }
            if (!oneForOne)
            {
                r.Unfit = FitCore(group, ok, pieces, false, r.UnfitMan);
                foreach (var p in pieces)
                {
                    p.OwnWorn = Math.Max(0, p.Used - p.MenTotal); p.Back = 0; r.Worn += p.OwnWorn;
                    p.Target = p.Barred ? p.Total : p.Total - p.Used;
                }
                r.Filled = r.Worn;
                return r;
            }
            var gr = new Greedy(group, groups, ok, pieces);
            gr.Run(false);                       // PRZED: tylko sztuki ludzi -> UsedBefore
            gr.Run(true);                        // PO: cala polka -> Used
            r.Unfit = gr.MarkUnfit(r.UnfitMan);
            foreach (var p in pieces) { p.OwnWorn = Math.Max(0, p.Used - p.MenTotal); p.Back = 0; r.Worn += p.OwnWorn; }
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
            r.Filled = r.Worn - Math.Min(r.Worn, r.Displaced);   // puste rece (bez zwrotu)
            foreach (var p in pieces)
            {
                p.Target = p.Barred ? p.Total : (p.Own - p.OwnWorn) + p.Back;
                if (p.Barred) continue;
                int kept = p.Own - p.OwnWorn;
                r.KeptOwn += kept; r.MenSpare += p.MenFree - p.Back;
                if (kept > 0)
                {
                    if (gr.WantedBySomeone(p)) { r.KeptHard += kept; p.KeptHard = kept; }
                    else { r.KeptWorse += kept; p.KeptWorse = kept; }
                }
            }
            return r;
        }

        // ------------------------------------------------------------ K1c: wklady z tej sesji ekranu
        /// <summary>K1c (przeglad K1b): co stalo sie z wkladami tej sesji (klucz id|mod -&gt; ile wlozono), po Swap z oneForOne.</summary>
        internal sealed class DepositTally
        {
            public int Worn, KeptWorse, KeptHard, KeptBarred, Moved;
            public readonly List<KeyValuePair<Piece, int>> Kept = new List<KeyValuePair<Piece, int>>();
            public readonly List<KeyValuePair<Piece, int>> MovedPieces = new List<KeyValuePair<Piece, int>>();
            public int Kept0 { get { return KeptWorse + KeptHard + KeptBarred; } }
        }

        /// <summary>K1c (przeglad K1b, Jeff 09.10 04:40 "zostaje po prostu w okienku DTE"): rozliczenie TYLKO wkladow z tej sesji - dotad
        /// komunikat liczyl KeptOwn calej ksiegi (stary schowek, dawne zwroty X, konie). Na egzemplarz wlozony (klucz z deps):
        ///  - na = min(wlozone, Own) - tyle wkladow zostalo czescia gracza tego egzemplarza; noszone najpierw wklady (min(na, OwnWorn)),
        ///    reszta zostaje w oknie z powodem egzemplarza (KeptHard - wymog, KeptWorse - nikt nie chcial, Barred - nie wydawane);
        ///  - Moved = wlozone - na: ksiega gracza jest per id i obejmuje NAJGORSZE egzemplarze (AllocateOwn), wiec lepszy egzemplarz
        ///    (np. "Fine X") wlozony obok gorszego egzemplarza ludzi tego samego id przechodzi na ludzi, a Twoja czescia staje sie ich
        ///    gorszy egzemplarz - to wymiana, nie "nikt nie chcial".</summary>
        internal static void TallyDeposits(List<Piece> pieces, IDictionary<string, int> deps, DepositTally t)
        {
            if (pieces == null || deps == null || deps.Count == 0 || t == null) return;
            foreach (var p in pieces)
            {
                int d;
                if (!deps.TryGetValue(p.Key, out d) || d <= 0) continue;
                int on = Math.Min(d, Math.Max(0, p.Own));
                int moved = d - on;
                int worn = Math.Min(on, Math.Max(0, p.OwnWorn));
                int kept = on - worn;
                t.Worn += worn;
                if (moved > 0) { t.Moved += moved; t.MovedPieces.Add(new KeyValuePair<Piece, int>(p, moved)); }
                if (kept <= 0) continue;
                if (p.Barred) t.KeptBarred += kept;
                else if (p.KeptHard > 0) t.KeptHard += kept;
                else t.KeptWorse += kept;
                t.Kept.Add(new KeyValuePair<Piece, int>(p, kept));
            }
        }

        /// <summary>
        /// K1 (Jeff 09.10 04:40): dopasowanie "najlepsze najpierw". Wpisy - osobno czesc ludzi i czesc gracza kazdego egzemplarza - ida od
        /// najlepszego (Better), przy rownych najpierw czesc ludzi, potem id i mod (porzadek staly). Sztuka wchodzi, jesli da sie ja dolozyc
        /// do przydzialu bez zdejmowania wczesniejszych: wprost do grupy z wolnym czlowiekiem albo sciezka powiekszajaca (ktos ja bierze,
        /// a jego dotychczasowa przechodzi na innego, ktory ja udzwignie, az do kogos z pustymi rekami). To zachlanny wybor w matroidzie
        /// przydzialow: sztuke ma tylu ludzi, ilu sie da (jak w FitCore), a do tego nosza najlepsze sztuki, jakie udzwigna.
        /// Grupy, z ktorych sciezka nie wyszla (pelne i zamkniete), sa martwe do konca przebiegu - nic juz do nich nie wejdzie.
        /// </summary>
        private sealed class Greedy
        {
            internal sealed class Entry { public Piece P; public bool Own; public int Cap; }

            private readonly Meets _ok;
            private readonly int[] _group;
            private readonly int _groups;
            private readonly int[] _size;                        // ilu ludzi w grupie
            private readonly List<int> _order = new List<int>(); // grupy od najwyzszego skilla (kolejnosc w group[])
            private readonly List<Entry> _all = new List<Entry>();
            private readonly List<Piece> _pieces;
            private List<Entry> _es;
            private int[] _free;
            private int[,] _assign;

            internal Greedy(int[] group, int groups, Meets ok, List<Piece> pieces)
            {
                _ok = ok; _group = group; _groups = Math.Max(1, groups); _pieces = pieces;
                _size = new int[_groups];
                foreach (int g in group)
                {
                    if (g < 0 || g >= _groups) continue;
                    if (_size[g]++ == 0) _order.Add(g);
                }
                foreach (var p in pieces)
                {
                    if (p.Barred) continue;
                    if (p.MenTotal > 0) _all.Add(new Entry { P = p, Own = false, Cap = p.MenTotal });
                    int own = Math.Min(Math.Max(0, p.Own), p.Total);
                    if (own > 0) _all.Add(new Entry { P = p, Own = true, Cap = own });
                }
                _all.Sort((a, b) =>
                {
                    int d = Better(b.P, a.P); if (d != 0) return d;
                    d = (a.Own ? 1 : 0).CompareTo(b.Own ? 1 : 0); if (d != 0) return d;
                    d = string.CompareOrdinal(a.P.Id, b.P.Id); if (d != 0) return d;
                    return string.CompareOrdinal(a.P.Mod, b.P.Mod);
                });
            }

            /// <summary>withOwn=false - tylko czesc ludzi (wynik w UsedBefore), true - cala polka (wynik w Used).</summary>
            internal void Run(bool withOwn)
            {
                _es = new List<Entry>();
                foreach (var e in _all) if (withOwn || !e.Own) _es.Add(e);
                _free = (int[])_size.Clone();
                _assign = new int[_groups, Math.Max(1, _es.Count)];
                var dead = new bool[_groups];
                for (int ei = 0; ei < _es.Count; ei++)
                {
                    int want = _es[ei].Cap;
                    while (want > 0)
                    {
                        int got = Place(ei, want, dead);
                        if (got <= 0) break;   // reszta tego wpisu tez nie wejdzie (te same sztuki, a przydzial tylko rosnie)
                        want -= got;
                    }
                }
                foreach (var p in _pieces) { if (withOwn) p.Used = 0; else p.UsedBefore = 0; }
                for (int ei = 0; ei < _es.Count; ei++)
                {
                    int n = 0;
                    for (int g = 0; g < _groups; g++) n += _assign[g, ei];
                    if (withOwn) _es[ei].P.Used += n; else _es[ei].P.UsedBefore += n;
                }
            }

            /// <summary>Dolozenie do want sztuk wpisu ei. Zwraca ile weszlo (0 - wpis jest juz rozpiety przez wczesniejsze).</summary>
            private int Place(int ei, int want, bool[] dead)
            {
                var p = _es[ei].P;
                // 1) wprost: grupa z wolnym czlowiekiem, ktora ja udzwignie - od najslabszej (silniejsi zostaja dla ciezszych sztuk)
                for (int oi = _order.Count - 1; oi >= 0; oi--)
                {
                    int g = _order[oi];
                    if (_free[g] <= 0 || !_ok.Ok(g, p)) continue;
                    int k = Math.Min(want, _free[g]);
                    _assign[g, ei] += k; _free[g] -= k;
                    return k;
                }
                // 2) sciezka powiekszajaca (BFS po grupach): grupa g bierze sztuke, a jedna ze sztuk g (e2) przechodzi do g2, ...
                var prevG = new int[_groups]; var prevE = new int[_groups]; var seen = new bool[_groups];
                var q = new Queue<int>(); var visited = new List<int>();
                foreach (int g in _order)
                {
                    if (dead[g] || !_ok.Ok(g, p)) continue;
                    seen[g] = true; prevG[g] = -1; q.Enqueue(g);
                }
                int end = -1;
                while (q.Count > 0 && end < 0)
                {
                    int g = q.Dequeue(); visited.Add(g);
                    for (int e2 = 0; e2 < _es.Count && end < 0; e2++)
                    {
                        if (_assign[g, e2] <= 0) continue;
                        var p2 = _es[e2].P;
                        foreach (int g2 in _order)
                        {
                            if (seen[g2] || dead[g2] || !_ok.Ok(g2, p2)) continue;
                            seen[g2] = true; prevG[g2] = g; prevE[g2] = e2;
                            if (_free[g2] > 0) { end = g2; break; }
                            q.Enqueue(g2);
                        }
                    }
                }
                if (end < 0)
                {
                    // odwiedzone grupy sa pelne, a zadna ich sztuka nie przejdzie do grupy spoza nich - martwe do konca przebiegu
                    foreach (int g in visited) dead[g] = true;
                    return 0;
                }
                int amt = Math.Min(want, _free[end]);
                for (int x = end; prevG[x] >= 0; x = prevG[x]) amt = Math.Min(amt, _assign[prevG[x], prevE[x]]);
                int y = end;
                while (prevG[y] >= 0)
                {
                    int pg = prevG[y], pe = prevE[y];
                    _assign[y, pe] += amt; _assign[pg, pe] -= amt;
                    y = pg;
                }
                _assign[y, ei] += amt; _free[end] -= amt;
                return amt;
            }

            /// <summary>Po Run(true): ludzie bez sztuki (ostatni w swojej grupie). Zwraca ich liczbe.</summary>
            internal int MarkUnfit(bool[] unfitOut)
            {
                var left = (int[])_free.Clone(); int n = 0;
                for (int m = _group.Length - 1; m >= 0; m--)
                {
                    int g = _group[m];
                    if (g < 0 || g >= _groups || left[g] <= 0) continue;
                    left[g]--; n++;
                    if (unfitOut != null) unfitOut[m] = true;
                }
                return n;
            }

            /// <summary>Po Run(true): czy ktos zostal bez sztuki albo nosi sztuke gorsza od p. Wtedy p mu sie nie dostala, bo jej nie
            /// udzwignie (inaczej Greedy dolozylby p przed ta gorsza) - nienoszona sztuka gracza "za trudna", a nie "nikt jej nie chce".</summary>
            internal bool WantedBySomeone(Piece p)
            {
                foreach (int g in _order)
                {
                    if (_free[g] > 0) return true;
                    for (int e = 0; e < _es.Count; e++)
                        if (_assign[g, e] > 0 && Better(p, _es[e].P) > 0) return true;
                }
                return false;
            }
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

        /// <summary>K1-A (Jeff 09.10, P1: "tak, jesli go na to stac i jest dostepna"): najwyzszy tier sztuki, jaka zolnierz kupi za swoje -
        /// tier JEDNOSTKI (paczka 175 daje wzorcom sprzet ich tieru; najmniej 1, bo tier sztuk zaczyna sie od 1), a z oneUp
        /// (MenUpgradeOneTierUp) o jeden stopien wyzej. Ten sam sufit dla ludzi gracza, lordow AI i zalog; wymog sztuki (ItemReq) osobno.</summary>
        internal static int CeilingTier(int troopTier, bool oneUp) { return Math.Max(1, troopTier) + (oneUp ? 1 : 0); }

        /// <summary>A7: czy nowa sztuka moze zastapic stara. Tier nie wyzszy niz sufit (CeilingTier: tier jednostki, z P1 o jeden wyzej);
        /// zawsze silniejsza, a do tego o prog (gain, np. 0.10) albo wyzszego tieru; nosiciel ja udzwignie (lift); cena w budzecie.</summary>
        internal static int UpgradeVerdict(int ceilingTier, int oldTier, long oldPower, int newTier, long newPower, double gain, bool lift, int price, int budget)
        {
            if (newTier > ceilingTier) return UpAboveGrade;
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
