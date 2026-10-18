using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KS RUNDA 4 - CENY I STEROWANIE RYNKU (paczka r4-ceny, noc 17/18.10; docs/PLAN-KS-RUNDA4-2026-10-17.md rozdz. 4 P3-P10, rozdz. 7 C1-C3; diagnoza testow fin78/fin80).
    /// Kazda czesc za swoim kluczem MCM (domyslnie TAK), dziala tylko przy zastosowanej kalibracji startu; klucz wylaczony = zachowanie sprzed paczki.
    ///  - P7 (Ks Price Restart): srednie cen miast od nowa w dobie konca rampy popytu (D+14) - okno odbioru D+22..D+49 mierzy ceny KS; co 7 dob linia cen chwilowych
    ///    (mediana ostatniego odczytu miast - NKS-7d).
    ///  - P4 (Ks Gap Margin, wymaga Ks Market Control): od Planu wlaczenia do D+28 czynnik popytu targu jedzenia z LUKI ZAPASU polek miast:
    ///    f = 1 + (T* x popyt zaplacony - polki - w drodze) / (28 x popyt zaplacony), 1.00-1.25 (Plan wlaczenia od razu, potem najwyzej 7% na Plan) - zamiast marginesu
    ///    1.03 (ShelfMargin) dla jedzenia; czynnik dziala w KROKU 8 (popyt targu x f w nadwyzce rynku - rece z pracy domowej, poprawka G; sufit kroku 8 1.25 przy kluczu),
    ///    nie w popycie rownowagi (recenzja: popyt na zapas w rownowadze Planu wlaczenia przechylal zajecia na stale - bloki gry buduje tylko Plan wlaczenia, a regula
    ///    zmiany zajec jest wylaczona); gasnie, gdy polki dojda do celu. Bez towaru z niczego: produkcja z rak i ziemi, zakup za monete miast (wozy wsi, KS-H).
    ///  - C1-C3 (Ks Ctl Per Town; granice C3 przy Ks Gap Margin): brak zaplaconego popytu w sterowaniu liczony miasto po miescie (miasta z polka < 0.5 T*: popyt zaplacony -
    ///    odbior 7 dob, srednia 28 dob), nie przez polki calego rynku; bez ciecia, gdy ponad 25% popytu zaplaconego towaru stoi przy polkach < 0.5 T*; granice
    ///    0.85-1.25 (luka +-25%) do D+28, potem sufit schodzi do 1.10 najwyzej 7% na Plan (luka +-5%).
    ///  - P6 (Ks Land Glut Guard): ciecie nadmiaru krainy tylko, gdy mediana miast rynku ma polki >= T* (inaczej "nadmiar bez ujscia (rozwoz)" w linii, bez ciecia).
    ///  - P3 (Ks Made7 Derived, wymaga Ks Real Offtake): wsad przetworow ZC5 w popycie ceny miasta = min(wyrob x f, srednia 7 dob wyrobu pary + brak do 4 dni zbytu / 7).
    ///  - P8 (Ks Cloth Demand): kolumna ksiegi KsCloth (odziez wojska, warsztaty zbrojne, naprawy kowali) w popycie 1:1 plotna, sukna i skory Planu.
    ///  - P10 (pomiar): "bez rynku" w linii "KS: rynki" rozdzielone na "wies je swoje" (wedlug rynku wsi) i stracone w drodze KS-H.
    ///  - SOL (Ks Surplus Skips Basket): krok 5 wsi DS przy koszyku ZC4 nie kupuje listy wsi (sol, narzedzia, plotno, garnki, piwo - wszystkie piec sa w koszyku ZC4
    ///    z limitem potrzeby i cena; TownFolk.DsSurplus), zakupy wsi bez koszyka ZC4 w ramce ksiegi rynku miasta.
    /// Zapis: osobny klucz arm_ks_r4c (doba restartu cen i koniec rampy, dla ktorego byl, czynnik popytu targu bez przyciecia 1.10, srednia braku miast) - starsza DLL
    /// go pomija, stary zapis bez klucza dziala.
    /// </summary>
    internal static partial class StartCalibration
    {
        // ------------------------------------------------------------ klucze
        internal static bool SwPriceRestart { get { var s = Settings.Current; return s == null || s.KsPriceRestart; } }
        internal static bool SwGapMargin { get { var s = Settings.Current; return s == null || s.KsGapMargin; } }
        internal static bool SwCtlPerTown { get { var s = Settings.Current; return s == null || s.KsCtlPerTown; } }
        internal static bool SwLandGlutGuard { get { var s = Settings.Current; return s == null || s.KsLandGlutGuard; } }
        internal static bool SwMade7 { get { var s = Settings.Current; return s == null || s.KsMade7Derived; } }
        internal static bool SwClothDemand { get { var s = Settings.Current; return s == null || s.KsClothDemand; } }
        internal static bool SwSurplusSkip { get { var s = Settings.Current; return s == null || s.KsSurplusSkipsBasket; } }

        private const double GapMaxF = 1.25, GapDays = 28.0, GapStep = 0.07, NoCutShare = 0.25, GapCtl = 0.25;
        private const int GapEnd = 28;                                  // P4 i granice C3: do D+28
        private const int R4cSaveMax = 16 * 1024;                       // straznik rozmiaru arm_ks_r4c

        private static int _pxRestartDay = -1;                          // P7: doba restartu srednich cen (-1 - jeszcze nie; zerowane w RuleReset)
        private static int _pxRestartEnd = -1;                          // P7: koniec rampy popytu (D+14), dla ktorego byl restart - Redo przesuwa D, restart od nowa
        private static readonly double[] _ut28 = new double[2 * KsTab.NG]; // C1: srednia 28 dob braku miast z polka < 0.5 T* (szt./d)
        private static readonly double[] _lowSh = new double[2 * KsTab.NG];// C2: czesc popytu zaplaconego przy polkach < 0.5 T* (ostatnia doba)
        private static string _gapLine;                                  // P4: linia "KS: luka zapasu" Planu w toku
        private static readonly List<string> _glutHeld = new List<string>();
        private static readonly List<string> _c2Held = new List<string>();
        private static long _skipZl; private static int _skipN;          // SOL: krok 5 DS - pominiete zakupy soli i narzedzi (zl, wsi) od ostatniej linii
        private static long _skipBk; private static int _skipBkN;        // SOL: krok 5 DS - zakupy z ramka ksiegi rynku (bez koszyka ZC4) od ostatniej linii
        private static long _r4cTicks; private static int _r4cDays;      // koszt doby (C1) od ostatniej linii
        private static string _r4cPending, _r4cKeep;
        // P3: srednia wyrobu par ZC5 (miasto -> [para] szt./d) i liczba przerobek; suma wsadu w popycie ceny ostatniej mapy [m * ZN + z] z Made7 i bez
        private static readonly Dictionary<Town, double[]> _kMade7 = new Dictionary<Town, double[]>();
        private static readonly Dictionary<Town, int> _kMade7N = new Dictionary<Town, int>();
        private static readonly double[] _m7Der = new double[2 * KsBasket.N], _m7Der0 = new double[2 * KsBasket.N];
        private static int _m7Day = -1;

        private static void R4cReset()
        {
            _pxRestartDay = -1; _pxRestartEnd = -1; Array.Clear(_ut28, 0, _ut28.Length); Array.Clear(_lowSh, 0, _lowSh.Length); _gapLine = null; _glutHeld.Clear(); _c2Held.Clear();
            _skipZl = 0; _skipN = 0; _skipBk = 0; _skipBkN = 0; _r4cTicks = 0; _r4cDays = 0; _r4cPending = null; _r4cKeep = null;
            _kMade7.Clear(); _kMade7N.Clear(); Array.Clear(_m7Der, 0, _m7Der.Length); Array.Clear(_m7Der0, 0, _m7Der0.Length); _m7Day = -1;
        }

        // ============================================================ P7: srednie cen od D+14 i ceny chwilowe
        /// <summary>LiveDaily (po polkach startu): restart srednich cen miast w dobie konca rampy popytu (D+14), raz na koniec rampy - po Undo -> Redo (RampRestart przesuwa D)
        /// od nowa w nowym D+14, jak srednie zuzycia sterowania (ControlDaily, _cRestart).</summary>
        private static void R4cPriceRestart(int today)
        {
            if (!SwPriceRestart || !_shelvesDone || DDay < 0) return;
            int end = DDay + (RampDemand - RampSupply);
            if (_pxRestartEnd == end || today < end) return;
            RulePxReset();
            _pxRestartDay = today; _pxRestartEnd = end;
            Log.Info(PK + "srednie cen miast od nowa doba " + today + " (koniec rampy popytu D+" + (RampDemand - RampSupply) + ", D " + DDay + "; klucz Ks Price Restart) - linia 'ceny miast'"
                     + " w oknie odbioru D+22..D+49 mierzy ceny po rampie, nie ceny sprzed KS i z rampy; A7 i regale licza od tej doby na nowej sredniej.");
        }

        /// <summary>Linia co 7 dob (stan zastosowany): mediana OSTATNIEGO odczytu indeksu cen miast (miasto co 7 dob na zmiane - ok. srednia tygodnia, bez pamieci 28 dob)
        /// wedlug rynku - NKS-7d (INFO).</summary>
        private static string PriceNowLine(int day)
        {
            if (!SwPriceRestart || _state != StateApplied || _pxNow.Count == 0 || _items == null) return null;
            var sb = new StringBuilder(1600);
            sb.Append(PK).Append("ceny chwilowe dzien ").Append(day).Append(" (mediana ostatniego odczytu indeksu miast, miasto co 7 dob na zmiane; srednie od nowa doba ")
              .Append(_pxRestartDay >= 0 ? _pxRestartDay.ToString(Inv) : "-").Append(", D ").Append(DDay).Append("):");
            var vals = new List<double>(_pxNow.Count);
            for (int m = 0; m < 2; m++)
            {
                sb.Append(m == 0 ? " | Westeros:" : " | Essos:");
                bool first = true;
                for (int i = 0; i < ItemCount; i++)
                {
                    vals.Clear();
                    foreach (var kv in _pxNow) { if (kv.Value == null || i >= kv.Value.Length || TownMarket(kv.Key) != m) continue; vals.Add(kv.Value[i]); }
                    if (vals.Count == 0) continue;
                    sb.Append(first ? " " : ", ").Append(ItemIds[i]).Append(' ').Append(Med(vals).ToString("0.00", Inv));
                    first = false;
                }
            }
            sb.Append('.');
            return sb.ToString();
        }

        // ============================================================ C1/C2: brak miasto po miescie
        /// <summary>ControlDaily (po srednich zuzycia): brak zaplaconego popytu w miastach z polka &lt; 0.5 T* (popyt zaplacony - odbior 7 dob) - srednia 28 dob z tym samym
        /// restartem co zuzycie (koniec rampy); czesc popytu przy pustych polkach (C2). Mapa ZC4 z wczoraj.</summary>
        private static void R4cUnmetDaily(double w, bool first)
        {
            if (!SwCtlPerTown) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var u = new double[2 * KsTab.NG]; var lo = new double[2 * KsTab.NG]; var all = new double[2 * KsTab.NG];
            if (_state == StateApplied && _zt.Count > 0)
            {
                foreach (var zt in _zt.Values)
                {
                    if (zt == null || zt.St == null) continue;
                    int m = zt.Market == 0 ? 0 : 1;
                    for (int z = 0; z < ZN; z++)
                    {
                        int g = CtlGood(z); if (g < 0) continue;
                        double dp = zt.Q[z] + zt.QV[z] + zt.Der[z] + zt.One[z];
                        if (!(dp > 0.5) || double.IsNaN(dp)) continue;
                        int i = m * KsTab.NG + g;
                        all[i] += dp;
                        if (zt.Cnt[z] < 0.5 * ZTstar(m, z) * dp) { lo[i] += dp; u[i] += Math.Max(0.0, dp - ZOff7(zt.St, z)); }
                    }
                }
            }
            for (int i = 0; i < _ut28.Length; i++) { _ut28[i] = first ? u[i] : _ut28[i] + (u[i] - _ut28[i]) * w; _lowSh[i] = all[i] > 0 ? lo[i] / all[i] : 0.0; }
            _r4cTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; _r4cDays++;
        }

        /// <summary>C2: towaru rynku nie wolno ciac, gdy ponad 25% jego popytu zaplaconego stoi przy polkach &lt; 0.5 T* (rozwoz, nie rece).</summary>
        private static bool R4cNoCut(int i) { return SwCtlPerTown && i >= 0 && i < _lowSh.Length && _lowSh[i] > NoCutShare; }

        /// <summary>C3 (klucz Ks Gap Margin): do D+28 luka +-25% i sufit 1.25 (luka zapasu P4 dziala od Planu wlaczenia), potem sufit schodzi do 1.10 najwyzej o krok na Plan.</summary>
        private static void R4cBounds(int day, double basis, ref double gMax, ref double hi)
        {
            if (!SwGapMargin || DDay < 0) return;
            if (day < DDay + GapEnd) { gMax = GapCtl; hi = GapMaxF; }
            else hi = Math.Max(hi, Math.Min(GapMaxF, basis * (1.0 - CtlStep)));
        }

        /// <summary>Dopisek linii sterowania (C1-C3): towary bez ciecia (C2) i granice.</summary>
        private static string R4cCtlNote(int day)
        {
            if (!SwCtlPerTown && !SwGapMargin) return "";
            var sb = new StringBuilder(300);
            if (SwCtlPerTown) sb.Append(" | C1: brak = miasta z polka < 0.5 T* (popyt zaplacony - odbior 7 dob, srednia 28 dob) | C2 bez ciecia (> 25% popytu zaplaconego przy polkach < 0.5 T*): ")
                                .Append(_c2Held.Count > 0 ? string.Join(", ", _c2Held.ToArray()) : "brak");
            if (SwGapMargin && DDay >= 0) sb.Append(" | C3: granice 0.85-").Append(day < DDay + GapEnd ? "1.25, luka +-25% (do D+28)" : "1.10 (sufit schodzi najwyzej 7% na Plan), luka +-5%");
            _c2Held.Clear();
            return sb.ToString();
        }

        // ============================================================ P4: luka zapasu polek
        private static bool R4cGapPhase(int day) { return SwGapMargin && (_state != StateApplied || DDay < 0 || day < DDay + GapEnd); }

        /// <summary>MarketControl przed sterowaniem (do D+14 + 5 dob; stan Plan - kazdy Plan moze byc Planem wlaczenia): czynnik jedzenia z luki zapasu polek miast rynku.
        /// Stan zastosowany - mapa ZC4 (polki, w drodze KS-H, T* x popyt zaplacony), krok najwyzej 7% od poprzedniego czynnika; stan Plan - zapas miast z ksiegi towarow
        /// i popyt targu poprzedniego Planu (polki starej gospodarki), czynnik od razu.</summary>
        private static void R4cGapFactors(int day, KsInput inp, double[] mf)
        {
            int NG = KsTab.NG;
            var hs = new double[2 * NG]; var h = new double[2 * NG]; var tr = new double[2 * NG]; var dp = new double[2 * NG];
            bool fromMap = _state == StateApplied && _zt.Count > 0;
            double sig = Math.Max(1, DailyLife.KsSigma);
            if (fromMap)
            {
                foreach (var zt in _zt.Values)
                {
                    if (zt == null || zt.St == null) continue;
                    int m = zt.Market == 0 ? 0 : 1;
                    for (int z = 0; z < ZN; z++)
                    {
                        int g = CtlGood(z); if (g < 0) continue;
                        double d = zt.Q[z] + zt.QV[z] + zt.Der[z] + zt.One[z];
                        if (!(d > 0) || double.IsNaN(d)) d = 0.0;
                        int i = m * NG + g;
                        hs[i] += ZTstar(m, z) * d; h[i] += zt.Cnt[z]; dp[i] += d;
                        int inc = 0; try { inc = KsFoodCarts.Incoming(zt.St, z); } catch { inc = 0; }
                        tr[i] += Math.Max(0, inc);
                    }
                }
            }
            else
            {
                if (_plan == null || _plan.Error != null || _plan.D == null) { _gapLine = PK + "luka zapasu (P4) doba " + day + " | brak Planu bez bledu - czynnik 1.03 (polki startu)."; return; }
                if (_ctlTmp.Count == 0) { _gapLine = PK + "luka zapasu (P4) doba " + day + " | brak liczb ksiegi towarow wedlug rynku (zapas miast) - czynnik 1.03 (polki startu)."; return; }
                for (int z = 0; z < ZN; z++)
                {
                    int g = CtlGood(z); if (g < 0) continue;
                    long[] a;
                    if (_ctlTmp.TryGetValue(ZIds[z], out a) && a != null && a.Length >= 8) for (int m = 0; m < 2; m++) h[m * NG + g] += Math.Max(0, a[6 + m]);
                }
                for (int m = 0; m < 2; m++)
                    foreach (int g in KsCore.Food)
                    {
                        int i = m * NG + g;
                        KsItem it; double kg = inp.Items.TryGetValue(KsTab.Goods[g], out it) && it != null && it.Kg > 0 ? it.Kg : 10.0;
                        double e = _plan.Eaten != null && i < _plan.Eaten.Length ? _plan.Eaten[i] : 0.0;
                        dp[i] = Math.Max(0.0, _plan.D[i] - e) / (sig * kg);
                        int zp = GoodZ(g);
                        hs[i] = (zp >= 0 ? ZTstar(m, zp) : ShelfDays(1.0)) * dp[i];
                    }
            }
            var basis = _state == StateApplied ? _mf : null;
            var sb = new StringBuilder(1800);
            sb.Append(PK).Append("luka zapasu (P4) doba ").Append(day).Append(" | ").Append(fromMap ? "z mapy miast (polki + w drodze KS-H wobec T* x popyt zaplacony)" : "Plan przed wlaczeniem (zapas miast z ksiegi wobec T* x popyt targu Planu)")
              .Append("; f = 1 + (cel - polki - w drodze) / (28 x popyt), 1.00-1.25").Append(basis != null ? ", krok najwyzej 7% na Plan" : ", od razu");
            var stage = (double[])mf.Clone();
            for (int m = 0; m < 2; m++)
            {
                sb.Append(m == 0 ? " | Westeros:" : " | Essos:");
                foreach (int g in KsCore.Food)
                {
                    int i = m * NG + g;
                    if (!(dp[i] >= 1.0)) { mf[i] = 1.0; stage[i] = 1.0; continue; }   // bez popytu - bez luki i bez marginesu
                    double gap = hs[i] - h[i] - tr[i];
                    double f = Math.Max(1.0, Math.Min(GapMaxF, 1.0 + gap / (GapDays * dp[i])));
                    double k = f;
                    if (basis != null && i < basis.Length && basis[i] > 0) k = Math.Max(0.85, Math.Min(GapMaxF, Math.Max(basis[i] * (1.0 - GapStep), Math.Min(basis[i] * (1.0 + GapStep), f))));
                    mf[i] = k; stage[i] = k;
                    sb.Append(' ').Append(KsTab.Goods[g]).Append(" f").Append(k.ToString("0.000", Inv)).Append(" (polki ").Append(F0(h[i])).Append(", w drodze ").Append(F0(tr[i]))
                      .Append(", cel ").Append(F0(hs[i])).Append(", popyt ").Append(F0(dp[i])).Append("/d)");
                }
            }
            _mfStage = stage;                                            // do _mf przy Planie bez bledu (podstawa kroku 7% i sterowania od D+14)
            _gapLine = sb.ToString();
        }

        /// <summary>Pozycja koszyka glowna towaru Planu g (T* z ceny startu) albo -1.</summary>
        private static int GoodZ(int g)
        {
            for (int z = 0; z < ZN; z++) if (CtlGood(z) == g) return z;
            return -1;
        }

        /// <summary>R3PlanInputs (po MarketControl): sufit czynnika kroku 8 1.25 (klucz Ks Gap Margin; bez klucza 1.10 - jak dotad). Luka zapasu P4 i granice C3 dzialaja
        /// TYLKO w kroku 8 (popyt targu x f w nadwyzce rynku - rece z pracy domowej, poprawka G okregow), nie w popycie rownowagi: zajecia Planu wlaczenia (jedyny Plan,
        /// z ktorego powstaja bloki gry) nie przechylaja sie na stale na luke polek, a korytarze i gradient krain licza ten sam popyt co dotad. Linia: popyt na zapas polek
        /// w kroku 8 (t/d = (f - 1) x (popyt - zjedzone w okregach) poprzedniego Planu).</summary>
        private static void R4cGapInputs(KsInput inp, int day)
        {
            inp.MarketFactorMax = 1.10;
            if (inp.MarketFactor == null) _gapLine = null;               // bez klucza Ks Market Control - bez linii luki (MarketControl wraca bez czyszczenia)
            if (!SwGapMargin || inp.MarketFactor == null) return;
            inp.MarketFactorMax = GapMaxF;
            if (_gapLine == null || _plan == null || _plan.Error != null || _plan.D == null) return;
            int NG = KsTab.NG;
            var mf = inp.MarketFactor;
            double sig = Math.Max(1, DailyLife.KsSigma);
            var sb = new StringBuilder(_gapLine.TrimEnd('.'), 2400);
            sb.Append(" | popyt na zapas polek w kroku 8 (t/d = (f - 1) x (popyt - zjedzone w okregach) poprzedniego Planu; rece z pracy domowej, zaplata miast; nie w rownowadze):");
            int n = 0;
            for (int m = 0; m < 2; m++)
            {
                sb.Append(m == 0 ? " Westeros" : " | Essos");
                bool first = true;
                foreach (int g in KsCore.Food)
                {
                    int i = m * NG + g;
                    if (i >= mf.Length || !(mf[i] > 1.0005) || i >= _plan.D.Length) continue;
                    double e = _plan.Eaten != null && i < _plan.Eaten.Length ? _plan.Eaten[i] : 0.0;
                    double dkg = (Math.Min(GapMaxF, mf[i]) - 1.0) * Math.Max(0.0, _plan.D[i] - e);
                    if (!(dkg > 0) || double.IsNaN(dkg)) continue;
                    KsItem it; double kg = inp.Items.TryGetValue(KsTab.Goods[g], out it) && it != null && it.Kg > 0 ? it.Kg : 10.0;
                    sb.Append(first ? " " : ", ").Append(KsTab.Goods[g]).Append(' ').Append((dkg / 1000.0).ToString("0.0", Inv)).Append(" (").Append(F0(dkg / (sig * kg))).Append(" szt./d)");
                    first = false; n++;
                }
                if (first) sb.Append(" -");
            }
            sb.Append(" | towarow z f > 1: ").Append(n).Append(" (sufit kroku 8 ").Append(GapMaxF.ToString("0.00", Inv)).Append(").");
            _gapLine = sb.ToString();
        }

        // ============================================================ P6: nadmiar krainy tylko przy pelnych polkach rynku
        /// <summary>Mediana miast rynku (polka / (T* x popyt zaplacony)) jedzenia [m * NG + g]; -1 - brak miast.</summary>
        private static double[] R4cMedRatio()
        {
            int NG = KsTab.NG;
            var lists = new List<double>[2 * NG];
            var cnt = new double[NG]; var tg = new double[NG];
            foreach (var zt in _zt.Values)
            {
                if (zt == null || zt.St == null) continue;
                int m = zt.Market == 0 ? 0 : 1;
                Array.Clear(cnt, 0, NG); Array.Clear(tg, 0, NG);
                for (int z = 0; z < ZN; z++)
                {
                    int g = CtlGood(z); if (g < 0) continue;
                    double d = zt.Q[z] + zt.QV[z] + zt.Der[z] + zt.One[z];
                    if (!(d > 0) || double.IsNaN(d)) continue;
                    cnt[g] += zt.Cnt[z]; tg[g] += ZTstar(m, z) * d;
                }
                for (int g = 0; g < NG; g++)
                {
                    if (!(tg[g] > 0.5)) continue;
                    int i = m * NG + g;
                    if (lists[i] == null) lists[i] = new List<double>(64);
                    lists[i].Add(cnt[g] / tg[g]);
                }
            }
            var r = new double[2 * NG];
            for (int i = 0; i < r.Length; i++) r[i] = lists[i] != null && lists[i].Count > 0 ? Med(lists[i]) : -1.0;
            return r;
        }

        /// <summary>LandGlutWeekly: nadmiar krainy przy rynku, ktorego mediana miast nie ma polki celu - bez ciecia (rozwoz), do linii.</summary>
        private static bool R4cGlutHeld(string land, int m, int g, double[] med)
        {
            if (!SwLandGlutGuard || med == null) return false;
            int i = (m == 0 ? 0 : 1) * KsTab.NG + g;
            if (i < 0 || i >= med.Length || med[i] < 0 || med[i] >= 1.0) return false;
            if (_glutHeld.Count < 40) _glutHeld.Add(land + " " + KsTab.Goods[g] + " (mediana " + med[i].ToString("0.00", Inv) + " x T*)");
            return true;
        }

        /// <summary>Dopisek linii sterowania (P6): nadmiar bez ujscia (rozwoz) - bez ciecia.</summary>
        private static string R4cGlutNote()
        {
            if (!SwLandGlutGuard) return "";
            string t = " | nadmiar bez ujscia (rozwoz, bez ciecia - mediana miast rynku ponizej T*): " + (_glutHeld.Count > 0 ? string.Join(", ", _glutHeld.ToArray()) : "brak");
            _glutHeld.Clear();
            return t;
        }

        // ============================================================ P3: wsad ZC5 z prawdziwego wyrobu
        /// <summary>TownCraftsDay (przerobka za 2 doby): srednia ok. 7 dob wyrobu kazdej pary w miescie (szt./d).</summary>
        internal static void R4cMade7Note(Town t, int[] made)
        {
            if (!SwMade7 || t == null || made == null) return;
            double[] a; int n;
            if (!_kMade7.TryGetValue(t, out a) || a == null || a.Length != made.Length) { a = new double[made.Length]; _kMade7[t] = a; _kMade7N[t] = 0; }
            _kMade7N.TryGetValue(t, out n); n++; _kMade7N[t] = n;
            double w = 1.0 / Math.Min(4, n);
            for (int i = 0; i < a.Length; i++) a[i] = n <= 1 ? made[i] / 2.0 : a[i] + (made[i] / 2.0 - a[i]) * w;
        }

        /// <summary>Srednia wyrobu pary pi w miescie (szt./d) - po 4 przerobkach (ok. 7-8 dob); false - za wczesnie albo klucz wylaczony.</summary>
        private static bool R4cMade7(Town t, int pi, out double v)
        {
            v = 0;
            if (!SwMade7 || t == null) return false;
            int n; double[] a;
            if (!_kMade7N.TryGetValue(t, out n) || n < 4 || !_kMade7.TryGetValue(t, out a) || a == null || pi < 0 || pi >= a.Length) return false;
            v = a[pi];
            return true;
        }

        /// <summary>Zc4Derived: wsad w popycie ceny (z Made7 i bez) do linii - suma mapy doby.</summary>
        private static void R4cDerNote(int m, int z, double x, double x0)
        {
            int today = Today();
            if (_m7Day != today) { _m7Day = today; Array.Clear(_m7Der, 0, _m7Der.Length); Array.Clear(_m7Der0, 0, _m7Der0.Length); }
            if (z < 0 || z >= ZN) return;
            int i = (m == 0 ? 0 : 1) * ZN + z;
            _m7Der[i] += x; _m7Der0[i] += x0;
        }

        // ============================================================ SOL: krok 5 DS
        /// <summary>TownFolk.DsSurplus: wies z koszykiem ZC4 nie kupila listy wsi (sol, narzedzia, plotno, garnki, piwo - wszystkie w koszyku ZC4; zl budzetu kroku 5 dalej -
        /// roboty gminy / kiesa).</summary>
        internal static void R4cNoteSurplusSkip(long zl) { if (zl <= 0) return; _skipZl += zl; _skipN++; }
        /// <summary>TownFolk.DsSurplus: zakup listy wsi w kroku 5 z ramka ksiegi rynku miasta targowego (wies bez koszyka ZC4).</summary>
        internal static void R4cNoteSurplusFramed(long zl) { if (zl <= 0) return; _skipBk += zl; _skipBkN++; }
        /// <summary>Wies na koszyku ZC4 z drabina "najpierw zaplacone" (ten sam warunek co G4 leakCut w TownFolk.Villages).</summary>
        internal static bool R4cSkipBasket(bool zc4) { return zc4 && SwSurplusSkip && SwPaidFirst && Applied; }

        // ============================================================ linia co 7 dob
        /// <summary>Linia "KS: ceny r4 dzien N" (co 7 dob, stan zastosowany): restart cen, brak miast (C1), polki puste (C2), wsad ZC5 (P3), krok 5 wsi (lista wsi), koszt.</summary>
        private static string R4cLine(int day)
        {
            if (_state != StateApplied || !(SwPriceRestart || SwGapMargin || SwCtlPerTown || SwLandGlutGuard || SwMade7 || SwClothDemand || SwSurplusSkip)) return null;
            int NG = KsTab.NG;
            var sb = new StringBuilder(1600);
            sb.Append(PK).Append("ceny r4 dzien ").Append(day).Append(" | klucze: Ks Price Restart ").Append(SwPriceRestart ? "TAK" : "NIE").Append(", Ks Gap Margin ").Append(SwGapMargin ? "TAK" : "NIE")
              .Append(", Ks Ctl Per Town ").Append(SwCtlPerTown ? "TAK" : "NIE").Append(", Ks Land Glut Guard ").Append(SwLandGlutGuard ? "TAK" : "NIE").Append(", Ks Made7 Derived ").Append(SwMade7 ? "TAK" : "NIE")
              .Append(", Ks Cloth Demand ").Append(SwClothDemand ? "TAK" : "NIE").Append(", Ks Surplus Skips Basket ").Append(SwSurplusSkip ? "TAK" : "NIE")
              .Append(" | srednie cen od nowa doba ").Append(_pxRestartDay >= 0 ? _pxRestartDay.ToString(Inv) : "-");
            if (SwCtlPerTown)
            {
                sb.Append(" | C1 brak miast z polka < 0.5 T* (szt./d, srednia 28 dob) / czesc popytu przy pustych polkach:");
                int[] show = { KsTab.GGrain, KsTab.GFish, KsTab.GMeat, KsTab.GCheese, KsTab.GButter, KsTab.GGrape, KsTab.GOlives, KsTab.GSalt };
                for (int m = 0; m < 2; m++)
                {
                    sb.Append(m == 0 ? " Westeros" : " | Essos");
                    bool first = true;
                    foreach (int g in show) { int i = m * NG + g; sb.Append(first ? " " : ", ").Append(KsTab.Goods[g]).Append(' ').Append(F0(_ut28[i])).Append('/').Append((100 * _lowSh[i]).ToString("0", Inv)).Append('%'); first = false; }
                }
            }
            if (SwMade7 && SwRealOfftake)
            {
                sb.Append(" | P3 wsad ZC5 w popycie ceny (szt./d, z Made7 / bez):");
                int[] zs = { ZGrain, ZGrape, ZOlives, ZHoney, ZClay };
                for (int m = 0; m < 2; m++)
                {
                    sb.Append(m == 0 ? " Westeros" : " | Essos");
                    bool first = true;
                    foreach (int z in zs) { int i = m * ZN + z; sb.Append(first ? " " : ", ").Append(ZIds[z]).Append(' ').Append(F0(_m7Der[i])).Append('/').Append(F0(_m7Der0[i])); first = false; }
                }
                sb.Append(" (miast ze srednia 7 dob ").Append(CountMade7()).Append(")");
            }
            sb.Append(" | krok 5 wsi DS: pominieta lista wsi (sol, narzedzia, plotno, garnki, piwo - w koszyku ZC4) ").Append(_skipZl).Append(" zl w ").Append(_skipN).Append(" zakupach (zostaje w budzecie kroku 5 - roboty gminy albo kiesa wsi; nic nie znika)")
              .Append(", zakupy listy z ramka rynku ").Append(_skipBk).Append(" zl (").Append(_skipBkN).Append(")");
            sb.Append(" | koszt C1 ").Append(_r4cDays > 0 ? (_r4cTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / _r4cDays).ToString("0.000", Inv) : "-").Append(" ms/d.");
            _skipZl = 0; _skipN = 0; _skipBk = 0; _skipBkN = 0; _r4cTicks = 0; _r4cDays = 0;
            return sb.ToString();
        }

        private static int CountMade7() { int n = 0; foreach (var kv in _kMade7N) if (kv.Value >= 4) n++; return n; }

        // ============================================================ zapis arm_ks_r4c
        /// <summary>Stan paczki r4-ceny: V=1, P=doba restartu cen, E=koniec rampy popytu tego restartu, F=czynnik popytu targu (bez przyciecia 1.10 pola M w arm_ks_r3),
        /// U=srednia braku miast (C1).</summary>
        internal static string ExportR4c()
        {
            try
            {
                if (_keepRaw != null) return _r4cKeep ?? "";
                if (_state == StatePlan && _plans == 0) return "";
                var sb = new StringBuilder(2048);
                sb.Append("V=1\n");
                sb.Append("P=").Append(_pxRestartDay).Append('\n');
                sb.Append("E=").Append(_pxRestartEnd).Append('\n');
                if (_mf != null) { sb.Append("F="); for (int i = 0; i < _mf.Length; i++) { if (i > 0) sb.Append(','); sb.Append(_mf[i].ToString("0.####", Inv)); } sb.Append('\n'); }
                sb.Append("U=").Append(_cN);
                for (int i = 0; i < _ut28.Length; i++) if (_ut28[i] > 0.05) sb.Append(',').Append(i).Append(':').Append(_ut28[i].ToString("0.#", Inv));
                sb.Append('\n');
                string s = sb.ToString();
                if (s.Length > R4cSaveMax) { Stumble("ExportR4c(rozmiar)", null); return ""; }
                return s;
            }
            catch (Exception e) { Stumble("ExportR4c", e); return ""; }
        }

        internal static void ImportR4c(string data) { _r4cPending = string.IsNullOrEmpty(data) ? null : data; }

        /// <summary>SessionStart (po ResolvePending): stan r4-ceny z zapisu. Stary zapis bez klucza po D+14 - srednie cen uznane za juz zerowane (bez zerowania w srodku gry).</summary>
        private static void R4cResolve()
        {
            string x = _r4cPending; _r4cPending = null;
            if (_keepRaw != null) { _r4cKeep = x; return; }
            if (_state != StateApplied) return;
            if (x == null || x.Length > R4cSaveMax)
            {
                if (DDay >= 0 && Today() > DDay + (RampDemand - RampSupply)) _pxRestartDay = _pxRestartEnd = DDay + (RampDemand - RampSupply);
                return;
            }
            bool hasE = false;
            foreach (var line in x.Split('\n'))
            {
                if (line.Length < 2 || line[1] != '=') continue;
                string v = line.Substring(2);
                try
                {
                    switch (line[0])
                    {
                        case 'P': { int d; if (int.TryParse(v, NumberStyles.Integer, Inv, out d)) _pxRestartDay = d; break; }
                        case 'E': { int d; if (int.TryParse(v, NumberStyles.Integer, Inv, out d)) { _pxRestartEnd = d; hasE = true; } break; }
                        case 'F':
                            {
                                var p = v.Split(','); if (p.Length != 2 * KsTab.NG) break;
                                var mf = new double[p.Length]; bool ok = true;
                                for (int i = 0; i < p.Length && ok; i++) { double d; if (double.TryParse(p[i], NumberStyles.Float, Inv, out d) && d >= 0.85 && d <= GapMaxF) mf[i] = d; else ok = false; }
                                if (ok) _mf = mf;
                                break;
                            }
                        case 'U':
                            {
                                var p = v.Split(',');
                                for (int i = 1; i < p.Length; i++)
                                {
                                    var q = p[i].Split(':'); int k; double u;
                                    if (q.Length == 2 && int.TryParse(q[0], out k) && k >= 0 && k < _ut28.Length && double.TryParse(q[1], NumberStyles.Float, Inv, out u)) _ut28[k] = Math.Max(0.0, u);
                                }
                                break;
                            }
                    }
                }
                catch (Exception e) { Stumble("R4cResolve(" + line[0] + ")", e); }
            }
            // zapis bez pola E (wersja paczki przed recenzja): restart juz byl - dla dzisiejszego konca rampy (bez drugiego zerowania srednich po wczytaniu)
            if (!hasE && _pxRestartDay >= 0 && DDay >= 0) _pxRestartEnd = DDay + (RampDemand - RampSupply);
        }
    }
}
