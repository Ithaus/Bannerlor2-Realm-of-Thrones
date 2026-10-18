using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Armoury
{
    /// <summary>
    /// f20-d4 (noc 18.10, decyzja Jeffa "4. b": "mala, bezpieczna regula jak dla zelaza: co 7 dni najwyzej 5% rak w okregu przesuwa sie do zajecia, ktorego brakuje na
    /// rynku, i z tego, ktorego jest za duzo. Bez kosztow i bez zlota z niczego, tylko ludzie zmieniaja prace") - RECE ZAJEC JEDZENIA I SOLI ZA POLKAMI CALEGO RYNKU.
    ///
    /// Przyczyna (audyt 18.10, soczewka 'towary'; fin93 Armoury-2026-10-18_12-18-40.log, ks-miasta-towary.csv): po D+49 sterowanie rynku (R3 MarketControl) konczy sie,
    /// Plan zmienia juz tylko prace domowa, a rece zajec przesuwa wylacznie A7 - wylaczona (Start Calibration Switch Rule, Ks Switch Signal Rule). Podaz nie idzie za
    /// cena: Essos oliwki 0.34 -> 0.14 T* (indeks 1.87 -> 3.41), mieso 0.44 -> 0.20, winogrona 0.81 -> 0.33, a sol Essos 0.10 -> 3.62 T* (indeks 0.62) i ryby Westeros
    /// 0.92 -> 3.28 T* rosna bez reakcji. Dla zelaza ta sama luka zamknieta regula f5-ruda (Ks Iron Hands Follow Market) - tu ta sama regula dla jedzenia i soli.
    ///
    /// REGULA (klucz Ks Food Hands Follow Market, tylko KS zastosowany - RuleDaily; co 7 dob od D, pierwszy krok D+56 - po oknie sterowania rynku D+14..D+49):
    ///  1. Rynek (Westeros / Essos) i towar jedzenia KS (KsCore.Food: zboze, ryby, mieso z miesem wieloryba, ser, maslo, jaja, jablka, marchew, pomarancze, winogrona,
    ///     oliwki, daktyle, miod, sol) - ta sama miara co sterowanie rynku i ks-miasta-towary.csv: polki S (mapa ZC4, zt.Cnt), w drodze KS-H R, cel T = suma T* x popyt
    ///     zaplacony miast (ZTstar x (Q + QV + Der + One)); zapas ZA 8 TYGODNI przy tempie ostatnich (do 4) krokow: S8 = S + 8 x tempo S, Z8 = S + R + 8 x tempo (S + R):
    ///       BRAK     - Z8 &lt; 0.5 T (nawet z towarem w drodze rynek bedzie w braku);
    ///       NADMIAR  - S8 &gt; 2 T i Z8 &gt; 2 T (towar bedzie lezal na polkach);
    ///       pasmo 0.5..2 T albo bez celu - bez zmian (histereza 0.5..2 T* - zadnego skakania w te i z powrotem).
    ///     Rzut 8 tygodni (a nie sam dzisiejszy poziom) - pomiar offline na liczbach fin93: przy samym poziomie (z wstrzymaniem, gdy rynek "wychodzi sam") sol Essos i ryby
    ///     Westeros przelatuja przez cale pasmo do zera w ok. 24 tygodnie i wracaja w gore (rece zostaja na poziomie ciecia, a zapas spada dalej - hustawka co pol roku);
    ///     z rzutem zatrzymuja sie w pasmie (1-2 T*), bez przeskoku w gore / w dol w kolejnych krokach przy szumie polek 8% T* na tydzien. Ostroznie w obie strony: brak
    ///     liczy towar w drodze, nadmiar wymaga polek i polek z droga. Tempo z pamieci sesji (4 ostatnie kroki); po wczytaniu - od zera (pierwszy krok: sam poziom).
    ///  2. Wynik zajecia na rynku (jedna tabela swiata P0): sum(wartosc kg x sygnal towaru) / sum(wartosc kg) po towarach jedzenia zajecia, ktore maja cel na rynku
    ///     (zywiec - czesc miesa z uboju, mieso wieloryba - mieso; welna, len, jedwab, futra, skory - bez sygnalu i bez glosu). -1..1. Stadniny (konie - lancuch gry) poza regula.
    ///  3. Blok (czesc okregu - jedna ziemia): rece przechodza TYLKO miedzy zajeciami bloku z udzialem >= 0.2% i ziemia, ktora na nie pozwala (L &gt; 0 - zajecia z par Planu
    ///     wlaczenia, tabela swiata): udzial zajecia x (1 + 0.05 x wynik), potem do tej samej sumy - zajecie z brakiem bierze do 5% rak wiecej, z nadmiarem oddaje do 5%,
    ///     reszta zajec bloku oddaje / bierze roznice. Zadne zajecie nie zmienia sie o wiecej niz 5% swoich rak na krok (najwyzej ok. 2.6% rak bloku - zawsze ponizej
    ///     5% rak okregu). Ziemia: zajeta ziemia bloku po zmianie nie ponad powierzchnie bloku x 1.02 (jak A7) - ruch skracany. Blok w przebudowie A7 i czesc z pracami
    ///     okregu - bez zmian. Okreg w progu "najpierw wyzywic siebie" (Plan, KsResult.FeedShort, ten sam warunek co A7) - rece tylko miedzy zajeciami przy jedzeniu.
    ///  4. Regale jedzenia (zloza soli, sol morska, gaje daktylowe - udzial rak bloku przy zlozu): BRAK x1.05, NADMIAR x0.95 (sufit jak RegaleCycle: suma rak przy zlozach
    ///     &lt;= polowa rak bloku poza domem, wzrost przycinany najwyzej do sumy sprzed kroku). Przy kluczu RegaleCycle (co 28 dob) nie rusza juz soli i daktyli po D+49
    ///     (jedna regula na jedno zjawisko - jak zelazo przy f5-ruda; przed D+49 regale jak dotad).
    /// Pieniadze i towar: bez przelewow - zmiana tylko podzialu rak bloku (produkcja z prawdziwych rak i ziemi). Bez regul krain (tylko caly rynek). A7 bez zmian.
    /// Udzialy zajec i rece przy zlozach sa w zapisie KS (arm_ks) - bez nowego klucza zapisu. Linia "Jedzenie KS (f20-d4): dzien N" co 7 dob (tools/sprawdz_logi.py --grupa njedz).
    /// Klucz NIE - nic z tego (RegaleCycle jak dotad co do bitu).
    /// </summary>
    internal static partial class StartCalibration
    {
        private const int F20Every = 7, F20Look = 8, F20Ring = 4;
        private const double F20Lo = 0.5, F20Hi = 2.0, F20Rate = 0.05, F20MaxRel = 0.05, F20MinX = 0.002, F20Group = 0.5;
        private const int F20DUp = 1, F20DDown = 2, F20DBand = 3, F20DNoT = 6;

        private sealed class F20Snap { public int Day; public double[] Sum, Shelf; }
        private static readonly List<F20Snap> _f20Snaps = new List<F20Snap>();     // pamiec sesji: polki rynku z ostatnich krokow (tempo do rzutu 8 tygodni)
        private static int[][] _f20Wg; private static double[][] _f20Ww;           // wagi wartosci towarow jedzenia zajecia (tabela swiata P0 x wartosc kg)
        private static int _f20Stum;
        private static bool _f20RegSkip;                                           // doba RuleDaily: RegaleCycle bez soli i daktyli (regula f20-d4 czynna)

        internal static bool F20On { get { var s = Settings.Current; return s != null && s.KsFoodHandsFollowMarket; } }

        private static void F20Reset() { _f20Snaps.Clear(); _f20Wg = null; _f20Ww = null; _f20Stum = 0; _f20RegSkip = false; }

        /// <summary>Regula czynna (klucz, po oknie sterowania rynku D+49).</summary>
        private static bool F20Active(int today, int d0) { return d0 >= 0 && today > d0 + CtlEnd && F20On; }

        /// <summary>Doba kroku: co 7 dob od D, po D+49 (pierwszy D+56).</summary>
        private static bool F20Day(int today, int d0) { return F20Active(today, d0) && (today - d0) % F20Every == 0; }

        /// <summary>Regale jedzenia: zloze soli, sol morska, gaj daktylowy.</summary>
        private static bool F20RegCraft(int c) { return c == KsTab.CSalt || c == KsTab.CSeaSalt || c == KsTab.CDate; }

        /// <summary>RegaleCycle: wpis soli albo daktyli w dobie z regula f20-d4 czynna - bez progu indeksu (krok tygodniowy F20Week).</summary>
        private static bool F20RegSkipOf(int craft) { return _f20RegSkip && F20RegCraft(craft); }

        private static bool F20Horse(int c) { return c >= KsTab.CHorse0 && c <= KsTab.CHorse5; }

        /// <summary>Wagi wartosci towarow jedzenia zajecia c (P0 x wartosc kg z EnsureValues; zywiec - czesc miesa z uboju, mieso wieloryba - mieso). Wartosc 0
        /// wszystkich towarow zajecia (ceny BK bez cen historycznych) - wagi kg.</summary>
        private static void F20Weights()
        {
            if (_f20Wg != null) return;
            EnsureValues();
            var val = _valKg;
            var food = new bool[KsTab.NG]; foreach (int g in KsCore.Food) food[g] = true;
            var wg = new int[KsTab.NC][]; var ww = new double[KsTab.NC][];
            for (int c = 0; c < KsTab.NC; c++)
            {
                var lg = new List<int>(4); var lv = new List<double>(4); var lk = new List<double>(4);
                foreach (var kv in KsTab.P0[c])
                {
                    int g = kv.Key, gs; double r = kv.Value, v;
                    if (g == KsTab.GCow || g == KsTab.GHog || g == KsTab.GSheep) { gs = KsTab.GMeat; r *= KsTab.MeatOfLive; }   // skora z uboju - bez sygnalu (nie jedzenie)
                    else if (g == KsTab.GWhaleMeat) gs = KsTab.GMeat;
                    else if (food[g]) gs = g;
                    else continue;
                    v = val != null && gs < val.Length ? val[gs] : 0.0;
                    lg.Add(gs); lv.Add(r * Math.Max(0.0, v)); lk.Add(r);
                }
                double sv = 0; foreach (var x in lv) sv += x;
                wg[c] = lg.ToArray(); ww[c] = sv > 0 ? lv.ToArray() : lk.ToArray();
            }
            _f20Wg = wg; _f20Ww = ww;
        }

        /// <summary>Tempo zapasu (szt. na tydzien) z najstarszego kroku pamieci (do 4 krokow, najwyzej 35 dob wstecz); brak pamieci - 0.</summary>
        private static void F20Slope(int i, int today, double sum, double shelf, out double slSum, out double slShelf)
        {
            slSum = 0; slShelf = 0;
            F20Snap old = null;
            foreach (var s in _f20Snaps) if (s != null && s.Day < today && today - s.Day <= 35 && (old == null || s.Day < old.Day)) old = s;
            if (old == null || old.Sum == null || i >= old.Sum.Length) return;
            double w = (today - old.Day) / 7.0; if (!(w > 0)) return;
            slSum = (sum - old.Sum[i]) / w; slShelf = (shelf - old.Shelf[i]) / w;
        }

        /// <summary>Okreg w progu "najpierw wyzywic siebie" (ten sam warunek co A7 FeedKeeps): rece tylko miedzy zajeciami przy jedzeniu.</summary>
        private static bool F20FeedGuard(LiveDist ld)
        {
            int mode = SelfFeedMode();
            if (mode <= 0 || ld == null || ld.TF || ld.K == null || !(ld.K.People > 0)) return false;
            var pr = _plan; var fs = pr != null ? pr.FeedShort : null;
            if (fs == null || ld.Id == null || !fs.Contains(ld.Id)) return false;
            if (mode == 1)
            {
                var b0 = ld.K.Blocks != null && ld.K.Blocks.Count > 0 ? ld.K.Blocks[0] : null;
                if (b0 == null || b0.O == null || !KsTab.ColdZone(b0.O.Zone)) return false;
            }
            return true;
        }

        private static void F20Week(int today, int d0)
        {
            long t0 = Stopwatch.GetTimestamp();
            var inv = CultureInfo.InvariantCulture;
            int NG = KsTab.NG, NC = KsTab.NC;
            var map = _zt;
            if (map == null || map.Count == 0)
            {
                Log.Info("Jedzenie KS (f20-d4): dzien " + today + " | klucz Ks Food Hands Follow Market TAK | krok pominiety - brak mapy koszyka ZC4 (pierwsza doba po wczytaniu), rece bez zmian"
                         + " | pieniadze i towar: bez przelewow | potkniecia " + _f20Stum + ".");
                _f20Stum = 0;
                return;
            }
            F20Weights();
            // 1. rynek: polki, w drodze KS-H, cel T* x popyt zaplacony (ta sama miara co sterowanie rynku i ks-miasta-towary.csv)
            var S = new double[2 * NG]; var R = new double[2 * NG]; var T = new double[2 * NG]; var nTown = new int[2];
            foreach (var zt in map.Values)
            {
                try
                {
                    if (zt == null || zt.St == null) continue;
                    int m = zt.Market == 0 ? 0 : 1;
                    nTown[m]++;
                    for (int z = 0; z < ZN; z++)
                    {
                        int g = CtlGood(z); if (g < 0) continue;
                        double dp = zt.Q[z] + zt.QV[z] + zt.Der[z] + zt.One[z];
                        if (dp > 0 && !double.IsNaN(dp) && !double.IsInfinity(dp)) T[m * NG + g] += ZTstar(m, z) * dp;
                        S[m * NG + g] += Math.Max(0, zt.Cnt[z]);
                        int inc = 0; try { inc = KsFoodCarts.Incoming(zt.St, z); } catch { inc = 0; }
                        R[m * NG + g] += Math.Max(0, inc);
                    }
                }
                catch (Exception e) { _f20Stum++; Stumble("F20Week(miasto)", e); }
            }
            var dec = new int[2 * NG]; var sig = new int[2 * NG]; var pS = new double[2 * NG]; var pZ = new double[2 * NG];
            bool slope = false;
            for (int m = 0; m < 2; m++)
                foreach (int g in KsCore.Food)
                {
                    int i = m * NG + g; double t = T[i];
                    if (!(t >= 1.0)) { dec[i] = F20DNoT; continue; }
                    double sum = S[i] + R[i], a, b;
                    F20Slope(i, today, sum, S[i], out a, out b);
                    if (a != 0 || b != 0) slope = true;
                    double z8 = sum + F20Look * a, s8 = S[i] + F20Look * b;           // zapas za 8 tygodni przy tempie ostatnich krokow (polki + droga, same polki)
                    pZ[i] = z8; pS[i] = s8;
                    if (z8 < F20Lo * t) dec[i] = F20DUp;
                    else if (s8 > F20Hi * t && z8 > F20Hi * t) dec[i] = F20DDown;
                    else dec[i] = F20DBand;
                    sig[i] = dec[i] == F20DUp ? 1 : dec[i] == F20DDown ? -1 : 0;
                }
            _f20Snaps.Add(new F20Snap { Day = today, Sum = AddArr(S, R), Shelf = (double[])S.Clone() });
            while (_f20Snaps.Count > F20Ring) _f20Snaps.RemoveAt(0);
            // 2. wynik zajecia na rynku (-1..1): wartosc towarow jedzenia z celem x sygnal / wartosc towarow jedzenia z celem
            var sc = new double[2 * NC];
            for (int m = 0; m < 2; m++)
                for (int c = 0; c < NC; c++)
                {
                    if (F20Horse(c) || KsTab.IsRegale[c] || _f20Wg == null) continue;
                    double num = 0, den = 0; var wg = _f20Wg[c]; var ww = _f20Ww[c];
                    for (int k = 0; k < wg.Length; k++) { int i = m * NG + wg[k]; if (dec[i] == F20DNoT) continue; den += ww[k]; num += ww[k] * sig[i]; }
                    sc[m * NC + c] = den > 0 ? Math.Max(-1.0, Math.Min(1.0, num / den)) : 0.0;
                }
            // 3. bloki: rece pola miedzy zajeciami bloku, regale soli i daktyli
            var hc0 = new double[2 * NC]; var hc1 = new double[2 * NC]; var mvG = new double[2 * NG];
            var rg0 = new double[2 * NG]; var rg1 = new double[2 * NG]; var rUp = new int[2 * NG]; var rDn = new int[2 * NG];
            var mvM = new double[2];
            int nBlk = 0, nChg = 0, nFlat = 0, nSingle = 0, nBusy = 0, nLock = 0, nNoHands = 0, nLandCut = 0, nLandStop = 0, nGuard = 0, nRegCap = 0, nBack = 0, nDist = 0, nOver = 0;
            double maxPct = 0; string maxId = "-";
            var cnt = new F20Count();
            foreach (var ld in _live.Values)
            {
                if (ld == null || ld.St == null || ld.K == null || ld.B == null) continue;
                if (LandBackOf(ld.St)) { nBack++; continue; }                         // ks-arm: kraina wrocona - bez reguly (jak A7 i regale)
                try
                {
                    int m = ld.Market == 0 ? 0 : 1;
                    string type = ld.K.Type;
                    bool guard = F20FeedGuard(ld);
                    if (guard) nGuard++;
                    double distAll = 0, distMoved = 0;
                    foreach (var b in ld.B)
                    {
                        if (b == null || b.C == null || b.X == null) continue;
                        nBlk++;
                        double rsum = 0; if (b.RF != null) for (int j = 0; j < b.RF.Length; j++) rsum += b.RF[j];
                        double out0 = Math.Max(0.0, b.Hands) * Math.Max(0.0, 1.0 - b.HomeFrac);     // rece bloku poza domem (ostatni rachunek)
                        double fh = out0 * Math.Max(0.0, 1.0 - rsum);                               // rece pola (zajecia bloku)
                        distAll += out0;
                        for (int j = 0; j < b.C.Length && j < b.X.Length; j++) hc0[m * NC + b.C[j]] += b.X[j] * fh;
                        double moved = 0;
                        if (b.Target >= 0) nBusy++;                                               // przebudowa A7 - RegaleCycle i krok zelaza tez ja pomijaja
                        else if (!(b.Hands > 0) || b.K == null) nNoHands++;
                        else
                        {
                            cnt.Clear();
                            moved += F20Block(ld, b, m, type, guard, fh, sc, hc1, mvG, cnt);
                            nChg += cnt.Chg; nFlat += cnt.Flat; nSingle += cnt.Single; nLock += cnt.Lock; nLandCut += cnt.LandCut; nLandStop += cnt.LandStop;
                            moved += F20Regale(b, m, sig, rg0, rg1, rUp, rDn, mvG, ref nRegCap);
                        }
                        if (cnt.Applied == 0) for (int j = 0; j < b.C.Length && j < b.X.Length; j++) hc1[m * NC + b.C[j]] += b.X[j] * fh;
                        cnt.Applied = 0;
                        distMoved += moved;
                    }
                    if (distAll > 0)
                    {
                        nDist++;
                        double pct = 100.0 * distMoved / distAll;
                        if (pct > maxPct) { maxPct = pct; maxId = ld.Id ?? "-"; }
                        if (pct > 100.0 * F20MaxRel + 1e-6) nOver++;
                        mvM[m] += distMoved;
                    }
                }
                catch (Exception e) { _f20Stum++; Stumble("F20Week(okreg)", e); }
            }
            // 4. linia
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            string[] mn = { "Westeros", "Essos" };
            var sb = new StringBuilder(6000);
            sb.Append("Jedzenie KS (f20-d4): dzien ").Append(today).Append(" | klucz Ks Food Hands Follow Market TAK, co ").Append(F20Every).Append(" dob od D ").Append(d0)
              .Append(" po D+").Append(CtlEnd).Append(" (rece zajec jedzenia i soli za polkami calego rynku za ").Append(F20Look).Append(" tyg. przy tempie ostatnich krokow: BRAK - polki + w drodze < ")
              .Append(F20Lo.ToString("0.0", inv)).Append(" T*, NADMIAR - polki i polki + w drodze > ").Append(F20Hi.ToString("0.0", inv))
              .Append(" T*; zajecie x(1 + ").Append(F20Rate.ToString("0.00", inv)).Append(" x wynik), najwyzej ").Append((100 * F20MaxRel).ToString("0", inv))
              .Append("% rak zajecia na krok, tylko miedzy zajeciami ziemi bloku; tempo z ").Append(slope ? "pamieci krokow" : "braku pamieci (pierwszy krok sesji - sam poziom)").Append(")");
            for (int m = 0; m < 2; m++)
            {
                sb.Append(" | ").Append(mn[m]).Append(" (miast ").Append(nTown[m]).Append("):");
                bool f = true;
                foreach (int g in KsCore.Food)
                {
                    int i = m * NG + g; if (T[i] < 1.0 && S[i] + R[i] < 1.0) continue;
                    sb.Append(f ? " " : "; ").Append(KsTab.Goods[g]).Append(' ').Append(S[i].ToString("0", inv)).Append('+').Append(R[i].ToString("0", inv)).Append('/').Append(T[i].ToString("0", inv))
                      .Append(" (").Append(T[i] >= 1 ? (S[i] / T[i]).ToString("0.00", inv) : "-").Append(' ').Append(T[i] >= 1 ? ((S[i] + R[i]) / T[i]).ToString("0.00", inv) : "-")
                      .Append(", za ").Append(F20Look).Append(" tyg. ").Append(T[i] >= 1 ? (pS[i] / T[i]).ToString("0.00", inv) : "-").Append(' ').Append(T[i] >= 1 ? (pZ[i] / T[i]).ToString("0.00", inv) : "-")
                      .Append(") ").Append(F20DecText(dec[i]));
                    f = false;
                }
            }
            sb.Append(" | wynik zajec (-1..1, tylko != 0):");
            for (int m = 0; m < 2; m++)
            {
                sb.Append(m == 0 ? " Westeros" : "; Essos"); bool f = true;
                for (int c = 0; c < NC; c++) { double v = sc[m * NC + c]; if (Math.Abs(v) < 0.005 || !(hc0[m * NC + c] > 0)) continue; sb.Append(f ? " " : ", ").Append(KsTab.Crafts[c]).Append(' ').Append(v >= 0 ? "+" : "").Append(v.ToString("0.00", inv)); f = false; }
                if (f) sb.Append(" -");
            }
            sb.Append(" | bloki ").Append(nBlk).Append(": zmienione ").Append(nChg).Append(", rowny wynik ").Append(nFlat).Append(", jedno zajecie albo bez rak do przesuniecia ").Append(nSingle)
              .Append(", ziemia skrocila ").Append(nLandCut).Append(", ziemia zatrzymala ").Append(nLandStop).Append(", przebudowa A7 ").Append(nBusy).Append(", prace okregu ").Append(nLock)
              .Append(", bez rachunku rak ").Append(nNoHands).Append("; okregi w progu najpierw wyzywic siebie ").Append(nGuard).Append(", krainy wrocone ").Append(nBack);
            // grupy zajec: w brak (wynik >= 0.5) i w nadmiarze (<= -0.5) - rece pola przed -> po (bez zmiany regale)
            for (int m = 0; m < 2; m++)
            {
                double p0 = 0, p1 = 0, n0 = 0, n1 = 0;
                for (int c = 0; c < NC; c++) { double v = sc[m * NC + c]; if (v >= F20Group) { p0 += hc0[m * NC + c]; p1 += hc1[m * NC + c]; } else if (v <= -F20Group) { n0 += hc0[m * NC + c]; n1 += hc1[m * NC + c]; } }
                sb.Append(m == 0 ? " | grupy zajec (rece pola przed -> po): Westeros" : "; Essos").Append(" w brak (wynik >= ").Append(F20Group.ToString("0.0", inv)).Append(") ")
                  .Append(p0.ToString("0", inv)).Append(" -> ").Append(p1.ToString("0", inv)).Append(", w nadmiarze (wynik <= -").Append(F20Group.ToString("0.0", inv)).Append(") ")
                  .Append(n0.ToString("0", inv)).Append(" -> ").Append(n1.ToString("0", inv));
            }
            for (int m = 0; m < 2; m++)
            {
                sb.Append(m == 0 ? " | rece pola wedlug zajecia (zmienione, najwieksze): Westeros" : "; Essos");
                var lst = new List<int>();
                for (int c = 0; c < NC; c++) if (Math.Abs(hc1[m * NC + c] - hc0[m * NC + c]) >= 0.5) lst.Add(c);
                lst.Sort((x, y) => Math.Abs(hc1[m * NC + y] - hc0[m * NC + y]).CompareTo(Math.Abs(hc1[m * NC + x] - hc0[m * NC + x])));
                if (lst.Count == 0) sb.Append(" -");
                for (int k = 0; k < lst.Count && k < 8; k++)
                {
                    int c = lst[k]; double a = hc0[m * NC + c], b = hc1[m * NC + c];
                    sb.Append(k == 0 ? " " : ", ").Append(KsTab.Crafts[c]).Append(' ').Append(a.ToString("0", inv)).Append(" -> ").Append(b.ToString("0", inv)).Append(" (").Append(Pct5(a, b)).Append(')');
                }
            }
            for (int m = 0; m < 2; m++)
            {
                sb.Append(m == 0 ? " | ruchy rak wedlug towaru (rece x udzial wartosci jedzenia zajecia, regale wprost): Westeros" : "; Essos");
                bool f = true;
                foreach (int g in KsCore.Food) { double v = mvG[m * NG + g]; if (Math.Abs(v) < 0.5) continue; sb.Append(f ? " " : ", ").Append(KsTab.Goods[g]).Append(' ').Append(v >= 0 ? "+" : "").Append(v.ToString("0", inv)); f = false; }
                if (f) sb.Append(" -");
            }
            sb.Append(" | regale jedzenia (wpisy w gore/w dol): sol Westeros ").Append(rUp[KsTab.GSalt]).Append('/').Append(rDn[KsTab.GSalt]).Append(", Essos ").Append(rUp[NG + KsTab.GSalt]).Append('/').Append(rDn[NG + KsTab.GSalt])
              .Append("; daktyle Westeros ").Append(rUp[KsTab.GDate]).Append('/').Append(rDn[KsTab.GDate]).Append(", Essos ").Append(rUp[NG + KsTab.GDate]).Append('/').Append(rDn[NG + KsTab.GDate])
              .Append("; sufit bloku przycial wzrost ").Append(nRegCap)
              .Append("; rece przy soli Westeros ").Append(rg0[KsTab.GSalt].ToString("0", inv)).Append(" -> ").Append(rg1[KsTab.GSalt].ToString("0", inv))
              .Append(", Essos ").Append(rg0[NG + KsTab.GSalt].ToString("0", inv)).Append(" -> ").Append(rg1[NG + KsTab.GSalt].ToString("0", inv))
              .Append("; przy daktylach Westeros ").Append(rg0[KsTab.GDate].ToString("0", inv)).Append(" -> ").Append(rg1[KsTab.GDate].ToString("0", inv))
              .Append(", Essos ").Append(rg0[NG + KsTab.GDate].ToString("0", inv)).Append(" -> ").Append(rg1[NG + KsTab.GDate].ToString("0", inv))
              .Append(" (RegaleCycle co 28 dob nie rusza soli i daktyli po D+").Append(CtlEnd).Append(")");
            sb.Append(" | przesunieto rak: Westeros ").Append(mvM[0].ToString("0", inv)).Append(", Essos ").Append(mvM[1].ToString("0", inv)).Append("; najwiecej w okregu ")
              .Append(maxPct.ToString("0.00", inv)).Append("% rak poza domem (").Append(maxId).Append("; limit ").Append((100 * F20MaxRel).ToString("0", inv)).Append("%), okregow ponad limit ").Append(nOver)
              .Append(" z ").Append(nDist);
            sb.Append(" | pieniadze i towar: bez przelewow - zmiana tylko podzialu rak bloku (rece zajec + rece przy zlozach = rece bloku)")
              .Append(" | czas ").Append(ms.ToString("0.0", inv)).Append(" ms | potkniecia ").Append(_f20Stum).Append('.');
            Log.Info(sb.ToString());
            _f20Stum = 0;
        }

        private sealed class F20Count
        {
            public int Chg, Flat, Single, Lock, LandCut, LandStop, Applied;
            public void Clear() { Chg = Flat = Single = Lock = LandCut = LandStop = Applied = 0; }
        }

        private static string F20DecText(int d)
        {
            switch (d)
            {
                case F20DUp: return "BRAK";
                case F20DDown: return "NADMIAR";
                case F20DNoT: return "BEZ-CELU";
                default: return "PASMO";
            }
        }

        private static double[] AddArr(double[] a, double[] b) { var r = new double[a.Length]; for (int i = 0; i < r.Length; i++) r[i] = a[i] + b[i]; return r; }

        /// <summary>Rece pola bloku miedzy jego zajeciami (replikator z krokiem 0.05, najwyzej 5% rak zajecia, ziemia bloku). Zwraca rece przesuniete (dodatnia czesc);
        /// hc1 - rece pola wedlug zajecia po kroku (tylko gdy krok zmienil blok - cnt.Applied).</summary>
        private static double F20Block(LiveDist ld, LiveBlock b, int m, string type, bool guard, double fh, double[] sc, double[] hc1, double[] mvG, F20Count cnt)
        {
            int n = Math.Min(b.C.Length, b.X.Length), NC = KsTab.NC;
            if (n < 2) { cnt.Single++; return 0; }
            if (KsWorksLock(ld, b)) { cnt.Lock++; return 0; }                       // inwest: czesc z pracami okregu nie zmienia zajecia (jak A7)
            var part = new bool[n]; int np = 0; double xp = 0, ap = 0;
            for (int j = 0; j < n; j++)
            {
                int c = b.C[j];
                if (!(b.X[j] >= F20MinX) || F20Horse(c) || KsTab.IsRegale[c]) continue;
                if (guard && !FoodCraft(c)) continue;                                // najpierw wyzywic siebie: rece tylko miedzy zajeciami przy jedzeniu
                if (!(b.L(c, type) > 0)) continue;                                    // tylko zajecia, na ktore ziemia bloku pozwala (tabela swiata)
                part[j] = true; np++; xp += b.X[j]; ap += b.X[j] * (1.0 + F20Rate * sc[m * NC + c]);
            }
            if (np < 2 || !(xp > 0) || !(ap > 0)) { cnt.Single++; return 0; }
            var d = new double[n]; double t = 1.0, big = 0;
            for (int j = 0; j < n; j++)
            {
                if (!part[j]) continue;
                d[j] = xp * b.X[j] * (1.0 + F20Rate * sc[m * NC + b.C[j]]) / ap - b.X[j];
                double ad = Math.Abs(d[j]); if (ad > big) big = ad;
                if (ad > 1e-12) t = Math.Min(t, F20MaxRel * b.X[j] / ad);
            }
            if (big < 1e-7) { cnt.Flat++; return 0; }
            // ziemia: zajeta ziemia bloku po zmianie nie ponad powierzchnie x 1.02 (jak A7), chyba ze juz byla ponad - wtedy nie rosnie
            double area = BlockArea(ld, b);
            if (area > 0 && type != null)
            {
                double land0 = 0, dl = 0;
                for (int j = 0; j < n; j++) { double lo = b.LandOf(b.C[j], type); land0 += b.X[j] * lo; if (part[j]) dl += d[j] * lo; }
                if (dl > 1e-12)
                {
                    double lim = Math.Max(land0, area * 1.02);
                    double tl = (lim - land0) / dl;
                    if (tl <= 1e-6) { cnt.LandStop++; return 0; }
                    if (tl < t) { t = tl; cnt.LandCut++; }
                }
            }
            double moved = 0, s = 0;
            for (int j = 0; j < n; j++)
            {
                if (part[j]) { double nx = Math.Max(0.0, b.X[j] + t * d[j]); b.X[j] = (float)nx; }
                s += b.X[j];
            }
            if (s > 0) for (int j = 0; j < n; j++) b.X[j] = (float)(b.X[j] / s);
            for (int j = 0; j < n; j++)
            {
                int c = b.C[j];
                hc1[m * NC + c] += b.X[j] * fh;
                if (!part[j]) continue;
                double dh = t * d[j] * fh;
                if (dh > 0) moved += dh;
                // ruch wedlug towaru: rece zajecia x udzial wartosci jego towarow jedzenia
                if (_f20Wg != null && Math.Abs(dh) > 0)
                {
                    var wg = _f20Wg[c]; var ww = _f20Ww[c]; double den = 0; foreach (var w in ww) den += w;
                    if (den > 0) for (int k = 0; k < wg.Length; k++) mvG[m * KsTab.NG + wg[k]] += dh * ww[k] / den;
                }
            }
            cnt.Chg++; cnt.Applied = 1;
            return moved;
        }

        /// <summary>Regale jedzenia (sol, sol morska, daktyle): BRAK x1.05, NADMIAR x0.95; wzrost przycinany do sufitu bloku najwyzej do sumy sprzed kroku (jak f5-ruda).
        /// Zwraca rece przesuniete (|zmiana| x rece bloku, dodatnia czesc - liczona raz).</summary>
        private static double F20Regale(LiveBlock b, int m, int[] sig, double[] rg0, double[] rg1, int[] rUp, int[] rDn, double[] mvG, ref int nCap)
        {
            if (b.RC == null || b.RF == null) return 0;
            int n = Math.Min(b.RC.Length, b.RF.Length), NG = KsTab.NG;
            double sum0 = 0; for (int j = 0; j < n; j++) sum0 += b.RF[j];
            var old = new float[n]; var inc = new double[n]; double sumInc = 0; bool any = false;
            for (int j = 0; j < n; j++)
            {
                old[j] = b.RF[j];
                if (!F20RegCraft(b.RC[j])) continue;
                int g = KsTab.RegGood(b.RC[j]); if (g < 0) continue;
                rg0[m * NG + g] += b.RF[j] * b.Hands;
                int s = sig[m * NG + g];
                if (s > 0) { b.RF[j] = (float)(b.RF[j] * (1.0 + F20Rate)); inc[j] = b.RF[j] - old[j]; sumInc += inc[j]; rUp[m * NG + g]++; any = true; }
                else if (s < 0) { b.RF[j] = (float)(b.RF[j] * (1.0 - F20Rate)); rDn[m * NG + g]++; any = true; }
            }
            if (sumInc > 0)
            {
                double sum = 0; for (int j = 0; j < n; j++) sum += b.RF[j];
                double cap = 0.5 * Math.Max(0.0, 1.0 - b.HomeFrac);                 // ten sam sufit co RegaleCycle i f5-ruda
                double lim = Math.Max(cap, sum0);
                if (sum > lim)
                {
                    double ex = sum - lim;
                    for (int j = 0; j < n; j++) if (inc[j] > 0) b.RF[j] = (float)Math.Max(old[j], b.RF[j] - Math.Min(inc[j], ex * inc[j] / sumInc));
                    nCap++;
                }
            }
            double up = 0, dn = 0;
            for (int j = 0; j < n; j++)
            {
                if (!F20RegCraft(b.RC[j])) continue;
                int g = KsTab.RegGood(b.RC[j]); if (g < 0) continue;
                rg1[m * NG + g] += b.RF[j] * b.Hands;
                if (!any) continue;
                double dh = (b.RF[j] - old[j]) * b.Hands;
                if (dh > 0) up += dh; else dn -= dh;
                mvG[m * NG + g] += dh;
            }
            return Math.Max(up, dn);
        }
    }
}
