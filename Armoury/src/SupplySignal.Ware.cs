using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 435-N-C (12.11, poprawka sygnalu niedoboru krok C; plan przedstawiony Jeffowi: "Dodac sygnal braku wyrobow, zeby warsztaty mogly przechodzic na to, czego brakuje
    /// na targu"): N WYROBU - osobna tablica (NTable.Ware) czytana TYLKO przez stanowiska L8 (WorkshopBenches) i przezbrojenie L9 (WorkshopIdleRetool). TownN / WorldN
    /// (L5, L6, L7, VillageCrafts, 444 regaly) jej nie widza - surowce bez zmian. Klucz Workshops Follow Goods Shortage (NIE = L8 / L9 co do bitu jak przed; pomiar
    /// i linia licza "bylby").
    ///
    /// Miara (po krytyce projektu 12.11):
    ///  - U (niezaspokojony popyt, zl/d): reszta budzetu kategorii po OSTATNIM PRZETWORZONYM wierszu polki w konsumpcji mieszczan (BK ItemConsumptionPatch:
    ///    categoryDemand[c] = budzet - kupione x cena), zbierana w zamknieciu nawiasu kiesy ludu (TownFolk.ConsumePostfix - zadnego wywolania CalculateBudget z naszej
    ///    strony, krytyka 3). Obcieta do [0, budzet pierwszego wiersza] (krytyka 4: kazdy wiersz liczy budzet od nowa). Ostatni wiersz z budzetem &lt;= 0.01 (bron,
    ///    konie, strzaly przy strzelarzach, deski przy ZC1 - HistoricalPrices.BudgetPostfix) = U 0: slownik trzyma tam surowy popyt dnia, nie pieniadz (krytyka 1).
    ///    Kategoria BEZ wiersza na polce pominieta (krytyka 2: slownik to wtedy popyt przed przeliczeniem na budzet - inne jednostki); liczona osobno w linii.
    ///  - Oferta nadwyzki ludu (P3, AddOffer) to POZIOM kiesy, nie nowy popyt (krytyka 5): czesc U do wysokosci oferty tej kategorii (oferta wydawana ostatnia - ta sama
    ///    regula co OfferClose) idzie do osobnego licznika i nie wchodzi do N.
    ///  - S (sprzedaz, zl/d) = zloto prawdziwej sprzedazy miasta (MarketSales 241) z TYCH SAMYCH dob co U (krytyka 7): tylko doby zamkniete (dzisiejsza przegrodka
    ///    dzieli miejsce z doba sprzed 28 dob), najwyzej 27, od pierwszej doby pomiaru.
    ///  - F (podloga) = najnizsza cena bazowa przedmiotu kategorii (ItemObject.Value, bez czynnika ceny targu - krytyka 6).
    ///  - N_w = 1 + min(1, U / max(S, F)); okno 7 dob osobno (N_w7). BRAMKA (nie ranga): N_w &gt;= 1.5 i U &gt;= wartosc jednego cyklu linii (sztuki wyrobu cyklu x cena
    ///    przedmiotu kategorii na targu dzis); dla L8 dodatkowo N_w7 &gt;= 1.5 (krytyka 8). RANGA w L9: (U - S) / wartosc cyklu.
    ///  - Zero nowego zlota i zero nowych sztuk - sam odczyt. Pomiar dopiero po MinSpan (7) dobach zamknietych.
    /// Zapis arm_lpu ("u1|doba|dob|miasto~kategoria~U~oferta;..." - srednie z dob zamknietych; przy wczytaniu rowno w te doby). Starsza DLL ignoruje klucz.
    /// Watek glowny; wyjatek = potkniecie liczone w linii, bez globalnego wylacznika (CLAUDE.md 7).
    /// </summary>
    internal static partial class SupplySignal
    {
        internal const double WareNeed = 1.5;               // bramka N_w (popyt bez towaru co najmniej polowa sprzedazy albo podlogi)
        private const int WareSpanMax = Window - 1;          // doby zamkniete w pierscieniu 28 (dzis dzieli przegrodke z doba sprzed 28 dob)
        private const int WareShort = 7;                     // krotkie okno dla L8 (krytyka 8)
        private const string WareVer = "u1";

        /// <summary>435-N-C: klucz Workshops Follow Goods Shortage - L8 / L9 czytaja N wyrobu. NIE = jak przed (pomiar liczy "bylby").</summary>
        internal static bool WareKey { get { var s = Settings.Current; return s != null && s.WorkshopFollowGoodsShortage; } }

        private sealed class WCell
        {
            public readonly int[] Day = new int[Window];
            public readonly float[] U = new float[Window], O = new float[Window];   // U - podstawa (bez oferty P3); O - czesc U do wysokosci oferty P3
            public WCell() { for (int i = 0; i < Window; i++) Day[i] = int.MinValue; }
        }
        private static readonly Dictionary<Settlement, Dictionary<ItemCategory, WCell>> _ware = new Dictionary<Settlement, Dictionary<ItemCategory, WCell>>();
        private static int _wFirst = int.MinValue;
        private static string _wPend, _wLoadNote = "nowa kampania albo zapis bez arm_lpu (pomiar od pierwszej doby)", _wareLine;
        private static int _wLoadBad;
        private static Dictionary<ItemCategory, ItemObject> _wRep;    // najtanszy przedmiot kategorii (podloga F i cena cyklu na targu) - sesja
        private static Dictionary<ItemCategory, int> _wCycle;        // najmniejsza liczba sztuk wyrobu cyklu linii z wsadem (statystyka "z warunkiem cyklu") - sesja

        // liczniki doby (od poprzedniej linii)
        private static int _dwBr, _dwNotes, _dwGhost, _dwNoRow, _dwNeg, _dwCap, _dwCtl, _dwMixed;
        private static double _dwU, _dwO, _dwGhostDict, _dwNoRowDict;

        /// <summary>Publiczna komorka tablicy N wyrobu (tylko N_w &gt; 1). N, N7 - N_w z okna dob zamknietych i z 7 dob; U - podstawa zl/d; S - sprzedaz zl/d; P - cena przedmiotu
        /// kategorii na targu dzis; O - czesc oferty P3 zl/d (poza N).</summary>
        internal sealed class WareCell { public float N, N7, U, S, P, O; }

        private static void WareReset()
        {
            _ware.Clear(); _wFirst = int.MinValue; _wPend = null; _wLoadNote = "nowa kampania albo zapis bez arm_lpu (pomiar od pierwszej doby)"; _wLoadBad = 0;
            _wRep = null; _wCycle = null; _wareLine = null;
            WareZeroDay();
        }

        private static void WareZeroDay()
        {
            _dwBr = _dwNotes = _dwGhost = _dwNoRow = _dwNeg = _dwCap = _dwCtl = _dwMixed = 0;
            _dwU = _dwO = _dwGhostDict = _dwNoRowDict = 0;
        }

        /// <summary>TownFolk.ConsumePostfix: czy zbierac N wyrobu z tego nawiasu (miasto, pomiar LP czynny). Sam odczyt.</summary>
        internal static bool WareWanted(Town town) { return town != null && town.IsTown && On; }

        /// <summary>
        /// TownFolk.ConsumePostfix (zamkniecie nawiasu konsumpcji miasta, po petli wierszy BK): kategoria `c` z wierszem na polce. `lastOk` - ostatni wiersz przetworzony
        /// (budzet &gt; 0.01); `rest` - wartosc slownika budzetu po konsumpcji; `first` - budzet pierwszego wiersza; `spent` - wydatek wszystkich wierszy (po zamknieciu
        /// ostatniego); `lastB` - budzet ostatniego przetworzonego wiersza; `offer` - oferta P3 tej kategorii w zlocie budzetu; `zeroRows` - wiersze z budzetem &lt;= 0.01.
        /// Sam licznik, nie rzuca.
        /// </summary>
        internal static void NoteWare(Town town, ItemCategory c, bool lastOk, double rest, double first, double spent, double lastB, double offer, int zeroRows)
        {
            if (!On || town == null || !town.IsTown || c == null) return;
            try
            {
                if (OffMain() || !MarketSales.Tracked(c)) return;
                if (_wPend != null) ResolveWare(Today);                      // 435-N-C-p: wpisy z zapisu PRZED pierwsza komorka sesji (dzienne ticki miast sa rozlozone na dobe)
                _dwNotes++;
                double u = 0;
                if (lastOk && !double.IsNaN(rest))
                {
                    u = rest;
                    if (u < 0) { _dwNeg++; u = 0; }                          // dokup jedzenia BK (zapas <= 10%) zawyza kupione - reszta ujemna
                    // 435-N-C-p (recenzja 2): BK liczy budzet kazdego wiersza od nowa (x idx^0.15 + dobrobyt) - przyrost wierszy 2.. to nie pieniadz ludu;
                    // pieniadz ludu po polce = budzet 1. wiersza - wydatek wszystkich wierszy (przy 1 wierszu rowne reszcie slownika)
                    double cap = double.IsNaN(first) ? u : Math.Max(0, double.IsNaN(spent) ? first : first - spent);
                    if (u > cap + 1e-6) { _dwCap++; u = cap; }
                    if (zeroRows > 0 && u > 0) _dwMixed++;
                    if (!double.IsNaN(lastB) && rest > lastB + 0.5) _dwCtl++;   // kontrola (recenzja 3): reszta slownika ponad budzet ostatniego wiersza - BK kupuje >= 0 sztuk, ma byc 0
                }
                else if (!double.IsNaN(rest) && rest > 0) { _dwGhost++; _dwGhostDict += rest; }   // krytyka 1: wiersz bez budzetu - surowy popyt, nie pieniadz
                double o = Math.Min(u, Math.Max(0, double.IsNaN(offer) ? 0 : offer)), ub = u - o;
                var st = town.Settlement;
                Dictionary<ItemCategory, WCell> m;
                if (!_ware.TryGetValue(st, out m)) { m = new Dictionary<ItemCategory, WCell>(); _ware[st] = m; }
                WCell cell;
                if (!m.TryGetValue(c, out cell)) { cell = new WCell(); m[c] = cell; }
                int d = Today, k = ((d % Window) + Window) % Window;
                if (cell.Day[k] != d) { cell.Day[k] = d; cell.U[k] = 0f; cell.O[k] = 0f; }
                cell.U[k] += (float)ub; cell.O[k] += (float)o;
                if (_wFirst == int.MinValue) _wFirst = d;
                _dwU += ub; _dwO += o;
            }
            catch (Exception e) { Stumble("NoteWare", e); }
        }

        /// <summary>TownFolk.ConsumePostfix: nawias konsumpcji miasta zamkniety; `noRow` kategorii sledzonych (MarketSales) w slowniku budzetu bez wiersza na polce
        /// (pominiete - krytyka 2), `noRowDict` - ich suma w jednostkach slownika (nie zloto). Sam licznik, nie rzuca.</summary>
        internal static void NoteWareBracket(Town town, int noRow, double noRowDict)
        {
            if (!On || town == null || !town.IsTown) return;
            try
            {
                if (OffMain()) return;
                _dwBr++; _dwNoRow += noRow; if (noRowDict > 0) _dwNoRowDict += noRowDict;
            }
            catch (Exception e) { Stumble("NoteWareBracket", e); }
        }

        private static int WareSpan(int today) { return _wFirst == int.MinValue ? 0 : Math.Max(0, Math.Min(WareSpanMax, today - _wFirst)); }

        private static void WareSum(WCell c, int from, int to, out double u, out double o)
        {
            u = o = 0;
            for (int k = 0; k < Window; k++)
            {
                int dd = c.Day[k];
                if (dd < from || dd > to) continue;
                u += c.U[k]; o += c.O[k];
            }
        }

        /// <summary>Przedmiot wzorcowy kategorii i liczba sztuk cyklu - raz na sesje; false - kategorie wyrobow jeszcze niegotowe (bez pamieci, proba nastepnej doby).</summary>
        private static bool WareCats()
        {
            if (_wRep != null) return true;
            var rep = new Dictionary<ItemCategory, ItemObject>();
            foreach (var it in TaleWorlds.CampaignSystem.Extensions.Items.All)
            {
                if (it == null || it.ItemCategory == null || it.Value <= 0 || !MarketSales.Tracked(it.ItemCategory)) continue;
                ItemObject cur;
                if (!rep.TryGetValue(it.ItemCategory, out cur) || it.Value < cur.Value) rep[it.ItemCategory] = it;
            }
            var cyc = new Dictionary<ItemCategory, int>();
            foreach (var wt in WorkshopType.All)
            {
                if (wt == null || wt.Productions == null) continue;
                foreach (var p in wt.Productions)
                {
                    if (p.Inputs == null || p.Inputs.Count == 0 || p.Outputs == null || p.Outputs.Count == 0 || !WorkshopTrade.IsTradeLine(p)) continue;
                    var c = p.Outputs[0].Item1;
                    if (c == null) continue;
                    int n = Math.Max(1, p.Outputs[0].Item2), x;
                    if (!cyc.TryGetValue(c, out x) || n < x) cyc[c] = n;
                }
            }
            if (rep.Count == 0 || cyc.Count == 0) return false;
            _wCycle = cyc;
            _wRep = rep;
            return true;
        }

        /// <summary>Podloga F kategorii: cena bazowa najtanszego przedmiotu (co najmniej 1).</summary>
        private static double WareFloor(ItemCategory c)
        {
            ItemObject it;
            return _wRep != null && _wRep.TryGetValue(c, out it) && it != null ? Math.Max(1, it.Value) : 1.0;
        }

        /// <summary>
        /// 435-N-C: bramka N wyrobu dla L8 / L9 - kategoria `c` (glowny wyrob linii, `count` sztuk na cykl) w miescie targowym `mk` wedlug tablicy `t`: N_w &gt;= 1.5
        /// i U &gt;= wartosc jednego cyklu (count x cena na targu); `need7` - takze N_w z 7 dob (L8). `score` = (U - S) / wartosc cyklu (ranga L9). Sam odczyt, nie rzuca.
        /// </summary>
        internal static bool WareGate(NTable t, Settlement mk, ItemCategory c, int count, bool need7, out double score)
        {
            score = 0;
            try
            {
                if (t == null || t.Ware == null || mk == null || c == null) return false;
                Dictionary<ItemCategory, WareCell> m; WareCell w;
                if (!t.Ware.TryGetValue(mk, out m) || !m.TryGetValue(c, out w) || w == null) return false;
                double cyc = Math.Max(1, count) * (double)Math.Max(1f, w.P);
                if (w.N < WareNeed - 1e-6 || w.U < cyc) return false;
                if (need7 && w.N7 < WareNeed - 1e-6) return false;
                score = (w.U - w.S) / cyc;
                return true;
            }
            catch { return false; }
        }

        /// <summary>ShortageText: tablica N wyrobu do `nt` i tekst linii "LP N wyrobu (435-N-C)" (zapamietany do Daily). Watek glowny.</summary>
        private static void WareBuild(int today, NTable nt)
        {
            int span = WareSpan(today), span7 = Math.Min(WareShort, span);
            int from = today - span, to = today - 1, from7 = today - span7;
            bool cats = WareCats();
            int noRep = 0, cells = 0, c13 = 0, c15 = 0, c15cyc = 0, c2 = 0, c2s0 = 0, towns15 = 0, n7ok = 0;
            double sumU = 0, sumS = 0, sumO = 0;
            var agg = new Dictionary<ItemCategory, double[]>();   // [z pomiarem, >= 1.5, >= 1.5 z cyklem, suma U, suma S, = 2 przy S = 0]
            var s0cats = new SortedSet<string>(StringComparer.Ordinal);
            if (span >= MinSpan && cats)
                foreach (var tk in _ware)
                {
                    var st = tk.Key;
                    if (st == null || st.Town == null) continue;
                    bool hot = false;
                    foreach (var ck in tk.Value)
                    {
                        try
                        {
                            var c = ck.Key;
                            ItemObject rep;
                            if (c == null || !_wRep.TryGetValue(c, out rep) || rep == null) { noRep++; continue; }   // bez przedmiotu wzorcowego - bez podlogi i ceny cyklu: poza N
                            double u, o, u7, o7;
                            WareSum(ck.Value, from, to, out u, out o); u /= span; o /= span;
                            WareSum(ck.Value, from7, to, out u7, out o7); u7 /= Math.Max(1, span7);
                            double s = MarketSales.GoldIn(st, c, from, to) / (double)span;
                            double s7 = MarketSales.GoldIn(st, c, from7, to) / (double)Math.Max(1, span7);
                            double f = WareFloor(c);
                            double n = 1.0 + Math.Min(1.0, u / Math.Max(s, f));
                            double n7 = 1.0 + Math.Min(1.0, u7 / Math.Max(s7, f));
                            double p = PriceOf(st.Town, rep);
                            int cn; if (!_wCycle.TryGetValue(c, out cn)) cn = 1;
                            bool cyc = u >= cn * Math.Max(1.0, p);
                            cells++; sumU += u; sumS += s; sumO += o;
                            double[] a;
                            if (!agg.TryGetValue(c, out a)) { a = new double[6]; agg[c] = a; }
                            a[0]++; a[3] += u; a[4] += s;
                            if (n >= 1.3 - 1e-9) c13++;
                            if (n >= WareNeed - 1e-9)
                            {
                                c15++; a[1]++;
                                if (cyc) { c15cyc++; a[2]++; hot = true; if (n7 >= WareNeed - 1e-9) n7ok++; }
                            }
                            if (n >= 2.0 - 1e-9) { c2++; if (!(s > 0)) { c2s0++; a[5]++; s0cats.Add(c.StringId); } }
                            if (n > 1.0 + 1e-9)
                            {
                                if (nt.Ware == null) nt.Ware = new Dictionary<Settlement, Dictionary<ItemCategory, WareCell>>();
                                Dictionary<ItemCategory, WareCell> wm;
                                if (!nt.Ware.TryGetValue(st, out wm)) { wm = new Dictionary<ItemCategory, WareCell>(); nt.Ware[st] = wm; }
                                wm[c] = new WareCell { N = (float)n, N7 = (float)n7, U = (float)u, S = (float)s, P = (float)p, O = (float)o };
                            }
                        }
                        catch (Exception e) { Stumble("WareBuild(komorka)", e); }
                    }
                    if (hot) towns15++;
                }
            int towns = 0;
            foreach (var t in Town.AllTowns) if (t != null && t.IsTown) towns++;
            var rows = new List<KeyValuePair<ItemCategory, double[]>>(agg);
            rows.Sort((x, y) => { int c = y.Value[2].CompareTo(x.Value[2]); if (c != 0) return c; c = y.Value[1].CompareTo(x.Value[1]); return c != 0 ? c : y.Value[3].CompareTo(x.Value[3]); });
            var sb = new StringBuilder(1400);
            sb.Append("klucz Workshops Follow Goods Shortage ").Append(WareKey ? "TAK (L8 / L9 czytaja N wyrobu)" : "NIE (L8 / L9 jak przed, liczone 'bylby')")
              .Append(" | N_w = 1 + min(1, U / max(S, F)) <= 2; U = reszta budzetu mieszczan po ostatnim przetworzonym wierszu polki (bez oferty P3), S = zloto prawdziwej sprzedazy (241) z tych samych dob, F = cena bazowa najtanszego przedmiotu; bramka L8 / L9: N_w >= ")
              .Append(F1(WareNeed)).Append(" i U >= wartosc cyklu linii (L8 takze N_w z ").Append(WareShort).Append(" dob)")
              .Append(" | nawias kiesy ludu (zrodlo U): ").Append(TownFolk.WareBracketOn ? "czynny" : "NIECZYNNY - bez pomiaru (klucze Town Folk)")
              .Append(", kotwica kiesy ").Append(TownFolk.LiveKey ? "czynna (U = pieniadz ludu)" : "NIECZYNNA (U = popyt gry, nie kiesa ludu)")
              .Append(" | dob zamknietych w oknie ").Append(span).Append(span < MinSpan ? " - N WYROBU WYCISZONE (wymagane " + MinSpan + ")" : "").Append(" | ").Append(_wLoadNote)
              .Append(" | od poprzedniej linii: nawiasow konsumpcji miast ").Append(_dwBr).Append(" (miast ").Append(towns).Append("), kategorii z wierszem ").Append(_dwNotes)
              .Append(", U podstawa ").Append(N0(_dwU)).Append(" zl, czesc oferty P3 (poza N) ").Append(N0(_dwO)).Append(" zl")
              .Append(" | komorek (miasto x wyrob) z pomiarem ").Append(cells).Append(": N_w >= 1.3 ").Append(c13).Append(", >= 1.5 ").Append(c15).Append(" (z warunkiem cyklu ").Append(c15cyc)
              .Append(", w tym takze N_w 7 dob >= 1.5: ").Append(n7ok).Append("), = 2 ").Append(c2).Append(" (przy S = 0: ").Append(c2s0)
              .Append(s0cats.Count > 0 ? " [" + string.Join(", ", new List<string>(s0cats).ToArray()) + "]" : "").Append(")")
              .Append(" | miast z wyrobem ponad bramka (>= 1.5 i cykl) ").Append(towns15).Append(" | suma U ").Append(N0(sumU)).Append(" zl/d wobec suma S ").Append(N0(sumS)).Append(" zl/d (oferta P3 ").Append(N0(sumO)).Append(" zl/d poza N)")
              .Append(" | top 10 (wyrob: miast ponad bramka / >= 1.5 / z pomiarem; U zl/d; S zl/d): ");
            int shown = 0;
            foreach (var r in rows)
            {
                if (shown >= 10) break;
                if (!(r.Value[1] > 0) && !(r.Value[3] > 0)) continue;
                sb.Append(shown > 0 ? "; " : "").Append(r.Key.StringId).Append(": ").Append((int)r.Value[2]).Append(" / ").Append((int)r.Value[1]).Append(" / ").Append((int)r.Value[0])
                  .Append("; ").Append(N0(r.Value[3])).Append("; ").Append(N0(r.Value[4]));
                shown++;
            }
            if (shown == 0) sb.Append('-');
            sb.Append(" | pominiete (poza U): kategorie bez wiersza polki ").Append(_dwNoRow).Append(" (slownik ").Append(N0(_dwNoRowDict)).Append(" jedn. - popyt przed przeliczeniem na budzet)")
              .Append(", ostatni wiersz bez budzetu (bron, konie, strzaly, deski ZC1) ").Append(_dwGhost).Append(" (slownik ").Append(N0(_dwGhostDict)).Append(" jedn.)")
              .Append(", reszta ujemna obcieta (dokup jedzenia BK) ").Append(_dwNeg).Append(", reszta ponad (budzet 1. wiersza - wydatek wierszy) obcieta ").Append(_dwCap)
              .Append(", U z kategorii z czescia wierszy bez budzetu ").Append(_dwMixed)
              .Append(", komorek bez przedmiotu wzorcowego (poza N) ").Append(noRep).Append(cats ? "" : " | KATEGORIE WYROBOW NIEGOTOWE - N wyrobu pominiete tej doby")
              .Append(" | kontrola: reszta slownika ponad budzet ostatniego wiersza = ").Append(_dwCtl).Append(" (ma byc 0)");
            _wareLine = sb.ToString();
        }

        /// <summary>Daily: tekst linii N wyrobu policzony w ShortageText tej doby (null - brak) i liczniki doby od zera.</summary>
        private static string WareTake() { var l = _wareLine; _wareLine = null; return l; }

        // ------------------------------------------------------------ zapis arm_lpu
        /// <summary>Zapis: "u1|doba|dob|miasto~kategoria~U~oferta;..." (srednie z dob zamknietych). "" - nic do zapisania. Wczytany, nierozwiniety napis wraca bez zmian. Nie rzuca.</summary>
        internal static string ExportWare()
        {
            try
            {
                if (_wPend != null) return _wPend;
                int today = Today, span = WareSpan(today);
                if (_ware.Count == 0 || span <= 0) return "";
                var sb = new StringBuilder(_ware.Count * 200 + 32);
                sb.Append(WareVer).Append('|').Append(today).Append('|').Append(span).Append('|');
                bool f = true;
                foreach (var tk in _ware)
                {
                    if (tk.Key == null || !IdOk(tk.Key.StringId)) continue;
                    foreach (var ck in tk.Value)
                    {
                        if (ck.Key == null || !IdOk(ck.Key.StringId)) continue;
                        double u, o;
                        WareSum(ck.Value, today - span, today - 1, out u, out o);
                        u /= span; o /= span;
                        if (!f) sb.Append(';'); f = false;
                        sb.Append(tk.Key.StringId).Append('~').Append(ck.Key.StringId).Append('~').Append(D(u)).Append('~').Append(D(o));
                    }
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("ExportWare", e); return ""; }
        }

        /// <summary>Wczytanie (po Reset): napis czeka do pierwszej doby sesji. Brak klucza - pomiar od zera.</summary>
        internal static void ImportWare(string data)
        {
            _wPend = string.IsNullOrEmpty(data) ? null : data;
            _wLoadNote = _wPend == null ? "zapis bez arm_lpu (pomiar od pierwszej doby)" : "arm_lpu czeka na pierwsza dobe sesji";
        }

        /// <summary>Rozwiniecie arm_lpu: srednie wpisane rowno w doby zamkniete sprzed zapisu (N wyrobu po wczytaniu jak przed zapisem). Watek glowny, nie rzuca.</summary>
        private static void ResolveWare(int today)
        {
            string data = _wPend;
            if (data == null) return;
            _wPend = null;
            try
            {
                var p = data.Split('|');
                int day, span;
                if (p.Length < 4 || p[0] != WareVer || !int.TryParse(p[1], NumberStyles.Integer, Inv, out day) || !int.TryParse(p[2], NumberStyles.Integer, Inv, out span))
                { _wLoadNote = "arm_lpu w nieznanej wersji - pominiety (pomiar od pierwszej doby)"; return; }
                span = Math.Max(1, Math.Min(WareSpanMax, span));
                if (day > today) day = today;
                int cells = 0, merged = 0;
                var seen = new HashSet<KeyValuePair<Settlement, ItemCategory>>();   // 435-N-C-p: powtorzony wpis w samym zapisie - zly wpis
                var om = MBObjectManager.Instance;
                if (p[3].Length > 0 && om != null && today - day < WareSpanMax)
                    foreach (var e in p[3].Split(';'))
                    {
                        var a = e.Split('~');
                        double u, o;
                        if (a.Length < 4 || !Num(a[2], out u) || !Num(a[3], out o)) { _wLoadBad++; continue; }
                        var st = Settlement.Find(a[0]);
                        var cat = om.GetObject<ItemCategory>(a[1]);
                        if (st == null || st.Town == null || cat == null) { _wLoadBad++; continue; }
                        Dictionary<ItemCategory, WCell> m;
                        if (!_ware.TryGetValue(st, out m)) { m = new Dictionary<ItemCategory, WCell>(); _ware[st] = m; }
                        if (!seen.Add(new KeyValuePair<Settlement, ItemCategory>(st, cat))) { _wLoadBad++; continue; }
                        // 435-N-C-p (recenzja 1): komorka mogla juz powstac w tej sesji (NoteWare przed rozwinieciem) - scalenie: doby z zapisu tylko w przegrodki
                        // starsze od nich (doby sesji >= doby zapisu zostaja)
                        WCell cell;
                        if (m.TryGetValue(cat, out cell)) merged++; else { cell = new WCell(); m[cat] = cell; }
                        for (int i = 1; i <= span; i++)
                        {
                            int d = day - i, k = ((d % Window) + Window) % Window;
                            if (cell.Day[k] >= d) continue;
                            cell.Day[k] = d; cell.U[k] = (float)u; cell.O[k] = (float)o;
                        }
                        cells++;
                    }
                if (cells > 0) _wFirst = _wFirst == int.MinValue ? day - span : Math.Min(_wFirst, day - span);
                _wLoadNote = "arm_lpu wczytany: zapis z doby " + day + " (dob " + span + "), komorek " + cells + (merged > 0 ? " (scalonych z pomiarem sesji " + merged + ")" : "") + (_wLoadBad > 0 ? ", zlych wpisow " + _wLoadBad : "");
            }
            catch (Exception e) { Stumble("ResolveWare", e); _wLoadNote = "arm_lpu - blad wczytania (pomiar od pierwszej doby)"; }
        }
    }
}
