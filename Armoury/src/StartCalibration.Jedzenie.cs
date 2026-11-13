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
    ///     zaplacony miast (ZTstar x (Q + QV + Der + One)); decyzja z DZISIEJSZEGO zapasu (jak zelazo f5-ruda i slowa Jeffa "ktorego brakuje na rynku"):
    ///       BRAK     - S + R &lt; 0.5 T (nawet z towarem w drodze rynek jest w braku);
    ///       NADMIAR  - S &gt; 2 T i S + R &gt; 2 T (towar lezy na polkach);
    ///       pasmo 0.5..2 T albo bez celu - bez zmian (histereza 0.5..2 T* - zadnego skakania w te i z powrotem).
    ///     Poprawka po recenzji (18.10): dotad decyzja z zapasu RZUCONEGO 8 tygodni naprzod przy tempie 4 ostatnich krokow - to nie bylo "brakuje na rynku", a 91-dniowa
    ///     zima (WinterBite: plon wsi -50% x gradient polnocy x kraina) w rzucie x8 dawala hustawke rak co roku. ZIMA: rece bez zmian (decyzje w linii tylko do wgladu) -
    ///     zimowy brak to pora roku, nie brak rynku; model offline (fin93, zima -25/-50%, szum 8% T*/tydz.): bez pauzy kazda wersja (rzut 8 tyg., sam poziom, krotki rzut)
    ///     hustala rece zajecia o 50-200% w roku, z pauza zimowa sam poziom 6-15% i 0-1.6 zmian kierunku w 3 lata (CHANGELOG f20-d4 - odstepstwo od "co 7 dni" do decyzji Jeffa).
    ///  2. Wynik zajecia na rynku (jedna tabela swiata P0): sum(wartosc kg x sygnal towaru) / sum(wartosc kg) po towarach jedzenia zajecia, ktore maja cel na rynku
    ///     (zywiec - czesc miesa z uboju, mieso wieloryba - mieso; welna, len, jedwab, futra, skory - bez sygnalu i bez glosu). -1..1. Stadniny (konie - lancuch gry) poza regula.
    ///  3. Blok (czesc okregu - jedna ziemia): rece przechodza TYLKO miedzy zajeciami bloku z udzialem >= 0.2% i ziemia, ktora na nie pozwala (L &gt; 0 - zajecia z par Planu
    ///     wlaczenia, tabela swiata): udzial zajecia x (1 + 0.05 x wynik), potem do tej samej sumy - zajecie z brakiem bierze do 5% rak wiecej, z nadmiarem oddaje do 5%,
    ///     reszta zajec bloku oddaje / bierze roznice. Zadne zajecie nie zmienia sie o wiecej niz 5% swoich rak na krok (najwyzej ok. 2.6% rak bloku - zawsze ponizej
    ///     5% rak okregu). Ziemia: zajeta ziemia bloku po zmianie nie ponad powierzchnie bloku x 1.02 (jak A7) - ruch skracany. Blok w przebudowie A7 i czesc z pracami
    ///     okregu - bez zmian. Okreg w progu "najpierw wyzywic siebie" (Plan, KsResult.FeedShort, ten sam warunek co A7) - rece tylko miedzy zajeciami przy jedzeniu
    ///     (takze regale: sol w takim okregu tylko w dol). Drzewa (winnica, gaj oliwny, sad cytrusowy, gaj daktylowy - te same co Tree() reguly A7): wiecej rak = wiecej ziemi
    ///     pod drzewami OD RAZU (A7 sadzi drzewa 364 doby z kosztem) - klucz Ks Food Hands Grow Groves (TAK - rosna od razu jak inne zajecia; NIE - tylko oddaja rece).
    ///  4. Regale jedzenia (zloza soli, sol morska, gaje daktylowe - udzial rak bloku przy zlozu): BRAK x1.05, NADMIAR x0.95 (sufit jak RegaleCycle: suma rak przy zlozach
    ///     &lt;= polowa rak bloku poza domem, wzrost przycinany najwyzej do sumy sprzed kroku). Przy kluczu RegaleCycle (co 28 dob) nie rusza juz soli i daktyli po D+49
    ///     (jedna regula na jedno zjawisko - jak zelazo przy f5-ruda; przed D+49 regale jak dotad).
    /// Pieniadze i towar: bez przelewow - zmiana tylko podzialu rak bloku (produkcja z prawdziwych rak i ziemi). Bez regul krain (tylko caly rynek). A7 bez zmian.
    /// Udzialy zajec i rece przy zlozach sa w zapisie KS (arm_ks) - bez nowego klucza zapisu; regula nie ma pamieci sesji (decyzja z dzisiejszych polek).
    /// Linia "Jedzenie KS (f20-d4): dzien N" co 7 dob (tools/sprawdz_logi.py --grupa njedz).
    /// Klucz NIE albo regula A7 czynna (A7RuleOn) - nic z tego (RegaleCycle jak dotad co do bitu; recenzja calosci noc-ks4: jedna regula zajec naraz).
    /// </summary>
    internal static partial class StartCalibration
    {
        private const int F20Every = 7;
        private const double F20Lo = 0.5, F20Hi = 2.0, F20Rate = 0.05, F20MaxRel = 0.05, F20MinX = 0.002, F20Group = 0.5;
        private const int F20DUp = 1, F20DDown = 2, F20DBand = 3, F20DNoT = 6;

        private static int[][] _f20Wg; private static double[][] _f20Ww;           // wagi wartosci towarow jedzenia zajecia (tabela swiata P0 x wartosc kg)
        private static int _f20Stum;
        private static bool _f20RegSkip;                                           // doba RuleDaily: RegaleCycle bez soli i daktyli (regula f20-d4 czynna)

        internal static bool F20On { get { var s = Settings.Current; return s != null && s.KsFoodHandsFollowMarket; } }

        /// <summary>Recenzja f20-d4: drzewa (Tree() - winnica, gaj oliwny, sad cytrusowy, gaj daktylowy) biora rece od razu (TAK) albo tylko je oddaja (NIE - jak A7: drzewa 364 doby).</summary>
        private static bool F20Groves { get { var s = Settings.Current; return s == null || s.KsFoodHandsGrowGroves; } }

        private static void F20Reset() { _f20Wg = null; _f20Ww = null; _f20Stum = 0; _f20RegSkip = false; _f20Snap = null; _f20Gen = 0; _f20MeasC = null; _f20PrevAfter = null; _f20Cov = null; _f20Hc = null; _f20CovDay = int.MinValue; }

        // 479-e (noc 13.11): pokrycie polek rynku jedzenia z kroku f20 - T* / (polki + w drodze), [m * NG + g]; NaN - bez celu (T* < 1), +inf - pusto.
        // Czyta sygnal A7 (A7sInst) przy kluczu Ks Switch Food Shelves; _f20Hc - rece pola zajec rynku przed krokiem f20 ([m * NC + c], hamulec wzrostu 479-e).
        // Pamiec sesji (bez zapisu): po wczytaniu do pierwszego kroku f20 sygnal A7 jak dotad.
        private static double[] _f20Cov, _f20Hc;
        // 479-f (recenzja 479-e, noc 13.11): dzien tablicy; tablica zywa tylko przy czynnym f20 (F20Active) i nie starsza niz 14 dob - inaczej sygnal A7 jak przed 479-e,
        // a hamulec 479-e bez rak zajec (cap 0 - przejscia dzieki 479-e wstrzymane), zeby wylaczenie klucza f20 w MCM w polowie sesji nie zamrazalo sygnalu
        private static int _f20CovDay = int.MinValue;
        private const int F20CovMaxAge = 14;
        private static bool F20TabLive(int today) { return _f20Cov != null && _f20CovDay != int.MinValue && today >= _f20CovDay && today - _f20CovDay <= F20CovMaxAge && F20Active(today, DDay); }

        // 479-d (noc 13.11, decyzja domyslna - analiza at-220b, workflow mieso/drzewa): SAM POMIAR, bez zmiany ekonomii. Rece pola zajec stada i drzew miedzy
        // krokami f20 rozbite na przyczyny: A7 (blok w przebudowie teraz albo przy poprzednim kroku), inna zmiana udzialu X (poza A7), ludzie bloku (Hands),
        // praca domowa (HomeFrac), rece przy zlozach (RF), bloki nowe/znikniete; obok ruch samego f20 w tym kroku. Rozbicie dokladne (teleskop):
        // x*Fh(H,hf,rs) - x1*Fh(H1,hf1,rs1) = dX*Fh(H1,hf1,rs1) + x*[Fh(H,hf1,rs1)-Fh(H1,hf1,rs1)] + x*[Fh(H,hf,rs1)-Fh(H,hf1,rs1)] + x*[Fh(H,hf,rs)-Fh(H,hf,rs1)].
        // Pamiec sesji (migawka bloku po kroku f20), czyszczona w F20Reset (nowa kampania / wczytanie) - bez zapisu.
        private static readonly string[] F20MeasNames = { "sheep_farm", "swine_farm", "cattle_farm", "olive_trees", "vineyard" };
        private sealed class F20Snap { public double H, Hf, Rs; public double[] X; public bool Busy; public int Gen, M; }
        private static Dictionary<LiveBlock, F20Snap> _f20Snap;
        private static int _f20Gen;
        private static int[] _f20MeasC;
        private static double[] _f20PrevAfter;

        private static double F20Fh(double h, double hf, double rs) { return Math.Max(0.0, h) * Math.Max(0.0, 1.0 - hf) * Math.Max(0.0, 1.0 - rs); }

        private static double F20Rs(LiveBlock b) { double r = 0; if (b.RF != null) for (int j = 0; j < b.RF.Length; j++) r += b.RF[j]; return r; }

        private static double[] F20MeasX(LiveBlock b)
        {
            int K = _f20MeasC.Length; var x = new double[K];
            for (int j = 0; j < b.C.Length && j < b.X.Length; j++) { int c = b.C[j]; for (int k = 0; k < K; k++) if (_f20MeasC[k] == c) x[k] += b.X[j]; }
            return x;
        }

        /// <summary>Recenzja f20-d4: dopisek linii "KS: produkcja" (F4FrozenText) po D+49 z kluczem - zajecia nie sa zamrozone (sprawdz_logi NF4-4 przyjmuje oba napisy).</summary>
        private const string F20FrozenOff = "zajecia: regula A7 wylaczona (Start Calibration Switch Rule), rece jedzenia i soli co 7 dob za polkami rynku (f20-d4, Ks Food Hands Follow Market)";

        /// <summary>Recenzja f20-d4: stan reguly w linii "A7 diagnoza" przy A7 wylaczonej (F4A7Old i F5Finish - ten sam napis; "regula: WYLACZONA" na poczatku jak dotad).</summary>
        private static string F20A7OffText(bool f20)
        {
            return f20 ? "WYLACZONA - zajecia: rece jedzenia i soli co 7 dob za polkami rynku (f20-d4), reszta na Planie wlaczenia (sam pomiar nacisku)"
                       : "WYLACZONA - zajecia zamrozone na Planie wlaczenia (sam pomiar nacisku)";
        }

        /// <summary>Regula czynna (klucz, po oknie sterowania rynku D+49, regula A7 wylaczona). Recenzja calosci noc-ks4: przy czynnej A7 (A7RuleOn - Start Calibration
        /// Switch Rule, Ks Switch Signal + Ks Switch Signal Rule albo plik autotestu) zajecia przesuwa TYLKO A7 (z kosztem i 60/364 dobami) - f20-d4 bez kosztu i od razu
        /// w tych samych blokach bylaby druga regula na to samo zjawisko. Ten sam warunek: krok F20Week, RegaleCycle bez soli i daktyli (_f20RegSkip - przy A7 regale
        /// wracaja do soli i daktyli jak przed paczka), napisy linii A7 / KS: produkcja / ZW (te i tak pisza f20 tylko przy A7 wylaczonej).</summary>
        /// r4-jedzenie (noc 13.11, decyzja domyslna): przy czynnej A7 (klucz Ks Food Hands With A7, TAK) f20-d4 tez dziala - tylko w blokach bez przebudowy A7 (b.Target < 0,
        /// jak dotad pomijane w F20Week). Powod (at-220a): A7 liczy sygnal cen cienia Planu (Plan narzuca zbozu x3) i od D zaczela 217 zmian, a polki oliwek/winogron/sera/miesa
        /// stoja ponizej 0.3 T* przez 160 dob (ceny gry x1.4-2.7); f20-d4 patrzy na prawdziwe polki rynku.
        private static bool F20WithA7 { get { var s = Settings.Current; return s != null && s.KsFoodHandsWithA7; } }
        private static bool F20Active(int today, int d0) { return d0 >= 0 && today > d0 + CtlEnd && F20On && (!A7RuleOn || F20WithA7); }

        /// <summary>Scalenie f20-d4 z f19-e4: regula czynna dzis - linia ZW (VillageCrafts.KsWorksText) mowi "zamrozone" tylko wtedy, gdy rece jedzenia i soli
        /// nie ida za polkami rynku (ten sam warunek co dopisek linii "KS: produkcja" F4FrozenText). Tylko odczyt.</summary>
        internal static bool F20ActiveToday { get { return F20Active(Today(), DDay); } }

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
            // recenzja: decyzja z DZISIEJSZEGO zapasu (jak zelazo f5-ruda: "ktorego brakuje na rynku"), bez rzutu naprzod; zima - rece bez zmian (decyzje tylko do wgladu)
            bool winter = false; try { winter = WinterBite.WinterNow(); } catch { winter = false; }
            // 479-b (noc 13.11, decyzja domyslna - recenzja 479-a): przy czynnej A7 nowe gaje (winnica, oliwki, cytrusy, daktyle) sadzi tylko platna A7 (koszt, 364 doby);
            // f20-d4 przesuwa wtedy rece tylko miedzy zajeciami bez wzrostu drzew (zamknieta ekonomia - bez darmowych drzew). A7 wylaczona - bez zmian (klucz Grow Groves)
            bool grovesKey = F20Groves;
            bool grovesA7 = grovesKey && A7RuleOn;
            bool groves = grovesKey && !A7RuleOn;
            var dec = new int[2 * NG]; var sig = new int[2 * NG];
            for (int m = 0; m < 2; m++)
                foreach (int g in KsCore.Food)
                {
                    int i = m * NG + g; double t = T[i];
                    if (!(t >= 1.0)) { dec[i] = F20DNoT; continue; }
                    double sum = S[i] + R[i];
                    if (sum < F20Lo * t) dec[i] = F20DUp;
                    else if (S[i] > F20Hi * t && sum > F20Hi * t) dec[i] = F20DDown;
                    else dec[i] = F20DBand;
                    sig[i] = winter ? 0 : dec[i] == F20DUp ? 1 : dec[i] == F20DDown ? -1 : 0;
                }
            // 479-e: pokrycie polek do sygnalu A7 (ta sama miara co decyzja f20: polki + w drodze wobec T*)
            {
                var cov = new double[2 * NG];
                for (int i = 0; i < cov.Length; i++) cov[i] = double.NaN;
                for (int m = 0; m < 2; m++)
                    foreach (int g in KsCore.Food)
                    {
                        int i = m * NG + g; double t = T[i];
                        if (!(t >= 1.0)) continue;
                        double sum = S[i] + R[i];
                        cov[i] = sum > 0 ? t / sum : double.PositiveInfinity;
                    }
                _f20Cov = cov; _f20CovDay = today;                                         // 479-f: dzien tablicy
            }
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
            int nWinter = 0, nTree = 0, nRegGuard = 0, nRegTree = 0, nHeldReset = 0;               // recenzja: bloki zima bez ruchu, drzewa bez wzrostu (klucz Grow Groves NIE), regale bez wzrostu
            double maxPct = 0; string maxId = "-";
            var cnt = new F20Count();
            // 479-d: pomiar rak pola (sam pomiar)
            if (_f20MeasC == null) { _f20MeasC = new int[F20MeasNames.Length]; for (int k = 0; k < _f20MeasC.Length; k++) _f20MeasC[k] = Array.IndexOf(KsTab.Crafts, F20MeasNames[k]); }
            bool meas = Array.IndexOf(_f20MeasC, -1) < 0;
            int MK = F20MeasNames.Length;
            if (_f20Snap == null) _f20Snap = new Dictionary<LiveBlock, F20Snap>();
            bool measPrev = _f20Gen > 0 && _f20PrevAfter != null;
            _f20Gen++;
            var dA7 = new double[2 * MK]; var dXo = new double[2 * MK]; var dH = new double[2 * MK]; var dHome = new double[2 * MK]; var dRf = new double[2 * MK];
            var dNew = new double[2 * MK]; var dGone = new double[2 * MK]; var dF20 = new double[2 * MK]; var mNow = new double[2 * MK]; var mAfter = new double[2 * MK];
            int measSt = 0;
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
                        double[] xb = null; F20Snap sp = null;
                        if (meas)
                        {
                            try
                            {
                                xb = F20MeasX(b); _f20Snap.TryGetValue(b, out sp);
                                double fNow = F20Fh(b.Hands, b.HomeFrac, rsum);
                                for (int k = 0; k < MK; k++) mNow[m * MK + k] += xb[k] * fNow;
                                if (sp != null && sp.X != null && sp.X.Length == MK)
                                {
                                    double f1 = F20Fh(sp.H, sp.Hf, sp.Rs), fa = F20Fh(b.Hands, sp.Hf, sp.Rs), fb = F20Fh(b.Hands, b.HomeFrac, sp.Rs);
                                    bool a7 = b.Target >= 0 || sp.Busy;
                                    int mp = sp.M;                                                          // migawka liczona na rynku z poprzedniego kroku (kontrola)
                                    for (int k = 0; k < MK; k++)
                                    {
                                        double dx = (xb[k] - sp.X[k]) * f1;
                                        if (a7) dA7[m * MK + k] += dx; else dXo[m * MK + k] += dx;
                                        dH[m * MK + k] += xb[k] * (fa - f1); dHome[m * MK + k] += xb[k] * (fb - fa); dRf[m * MK + k] += xb[k] * (fNow - fb);
                                        if (mp != m) { dNew[m * MK + k] += sp.X[k] * f1; dGone[mp * MK + k] += sp.X[k] * f1; }   // blok zmienil rynek - przenosiny miedzy rynkami
                                    }
                                }
                                else for (int k = 0; k < MK; k++) dNew[m * MK + k] += xb[k] * fNow;
                            }
                            catch { measSt++; xb = null; }
                        }
                        double moved = 0;
                        if (b.Target >= 0) nBusy++;                                               // przebudowa A7 - RegaleCycle i krok zelaza tez ja pomijaja
                        else if (!(b.Hands > 0) || b.K == null) nNoHands++;
                        else if (winter) { nWinter++; F20RegHands(b, m, rg0, rg1); }             // recenzja: zima - rece bez zmian (zimowy brak to pora roku, nie brak rynku)
                        else
                        {
                            cnt.Clear();
                            moved += F20Block(ld, b, m, type, guard, groves, fh, sc, hc1, mvG, cnt);
                            nChg += cnt.Chg; nFlat += cnt.Flat; nSingle += cnt.Single; nLock += cnt.Lock; nLandCut += cnt.LandCut; nLandStop += cnt.LandStop; nTree += cnt.Tree; nHeldReset += cnt.HeldReset;
                            moved += F20Regale(b, m, sig, guard, groves, rg0, rg1, rUp, rDn, mvG, ref nRegCap, ref nRegGuard, ref nRegTree);
                        }
                        if (cnt.Applied == 0) for (int j = 0; j < b.C.Length && j < b.X.Length; j++) hc1[m * NC + b.C[j]] += b.X[j] * fh;
                        cnt.Applied = 0;
                        if (meas && xb != null)
                        {
                            try
                            {
                                var xa = F20MeasX(b); double rsA = F20Rs(b), fA = F20Fh(b.Hands, b.HomeFrac, rsA), fNow = F20Fh(b.Hands, b.HomeFrac, rsum);
                                for (int k = 0; k < MK; k++) { dF20[m * MK + k] += xa[k] * fA - xb[k] * fNow; mAfter[m * MK + k] += xa[k] * fA; }
                                if (sp == null) { sp = new F20Snap(); _f20Snap[b] = sp; }
                                sp.H = b.Hands; sp.Hf = b.HomeFrac; sp.Rs = rsA; sp.X = xa; sp.Busy = b.Target >= 0; sp.Gen = _f20Gen; sp.M = m;
                            }
                            catch { measSt++; }
                        }
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
            _f20Hc = hc0;                                                                  // 479-e: rece pola zajec przed krokiem (hamulec wzrostu A7 z polek)
            // 479-d: bloki z migawka, ktorych ten krok nie widzial (kraina wrocona, okreg z potknieciem, blok zniknal) - poza rachunkiem od tego kroku
            if (meas)
            {
                List<LiveBlock> gone = null;
                foreach (var kv in _f20Snap)
                {
                    var g0 = kv.Value; if (g0 == null || g0.Gen == _f20Gen) continue;
                    if (g0.X != null && g0.X.Length == MK) { double f1 = F20Fh(g0.H, g0.Hf, g0.Rs); for (int k = 0; k < MK; k++) dGone[g0.M * MK + k] += g0.X[k] * f1; }
                    (gone ?? (gone = new List<LiveBlock>())).Add(kv.Key);
                }
                if (gone != null) foreach (var gb in gone) _f20Snap.Remove(gb);
            }
            // 4. linia
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            string[] mn = { "Westeros", "Essos" };
            var sb = new StringBuilder(6000);
            sb.Append("Jedzenie KS (f20-d4): dzien ").Append(today).Append(" | klucz Ks Food Hands Follow Market TAK, co ").Append(F20Every).Append(" dob od D ").Append(d0)
              .Append(" po D+").Append(CtlEnd).Append(" (rece zajec jedzenia i soli za dzisiejszymi polkami calego rynku: BRAK - polki + w drodze < ")
              .Append(F20Lo.ToString("0.0", inv)).Append(" T*, NADMIAR - polki i polki + w drodze > ").Append(F20Hi.ToString("0.0", inv))
              .Append(" T*; zajecie x(1 + ").Append(F20Rate.ToString("0.00", inv)).Append(" x wynik), najwyzej ").Append((100 * F20MaxRel).ToString("0", inv))
              .Append("% rak zajecia na krok, tylko miedzy zajeciami ziemi bloku; drzewa biora rece ").Append(groves ? "TAK" : grovesA7 ? "NIE - A7 czynna, nowe gaje tylko platna A7 (479-b)" : "NIE").Append(" (Ks Food Hands Grow Groves))")
              .Append(" | zima: ").Append(winter ? "TAK - rece bez zmian (decyzje tylko do wgladu)" : "nie")
              .Append(" | regula A7 ").Append(A7RuleOn ? "TAK - razem z nia (r4-jedzenie, Ks Food Hands With A7), bloki w przebudowie A7 pominiete" : "NIE");
            for (int m = 0; m < 2; m++)
            {
                sb.Append(" | ").Append(mn[m]).Append(" (miast ").Append(nTown[m]).Append("):");
                bool f = true;
                foreach (int g in KsCore.Food)
                {
                    int i = m * NG + g; if (T[i] < 1.0 && S[i] + R[i] < 1.0) continue;
                    sb.Append(f ? " " : "; ").Append(KsTab.Goods[g]).Append(' ').Append(S[i].ToString("0", inv)).Append('+').Append(R[i].ToString("0", inv)).Append('/').Append(T[i].ToString("0", inv))
                      .Append(" (").Append(T[i] >= 1 ? (S[i] / T[i]).ToString("0.00", inv) : "-").Append(' ').Append(T[i] >= 1 ? ((S[i] + R[i]) / T[i]).ToString("0.00", inv) : "-")
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
              .Append(", bez rachunku rak ").Append(nNoHands).Append(", zima bez ruchu ").Append(nWinter).Append("; okregi w progu najpierw wyzywic siebie ").Append(nGuard).Append(", krainy wrocone ").Append(nBack)
              .Append("; drzewa bez wzrostu (Grow Groves NIE albo A7 czynna) ").Append(nTree)
              .Append("; wzrost gajow pominiety przez A7 (479-b): bloki ").Append(grovesA7 ? nTree : 0).Append(", regale daktyli ").Append(grovesA7 ? nRegTree : 0)
              .Append("; nacisk A7 wyzerowany - f20 zmienil glowne zajecie bloku (479-c) ").Append(nHeldReset);
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
              .Append("; sufit bloku przycial wzrost ").Append(nRegCap).Append("; wzrost wstrzymany: sol w progu wyzywienia ").Append(nRegGuard).Append(", gaje daktylowe (Grow Groves NIE) ").Append(nRegTree)
              .Append("; rece przy soli Westeros ").Append(rg0[KsTab.GSalt].ToString("0", inv)).Append(" -> ").Append(rg1[KsTab.GSalt].ToString("0", inv))
              .Append(", Essos ").Append(rg0[NG + KsTab.GSalt].ToString("0", inv)).Append(" -> ").Append(rg1[NG + KsTab.GSalt].ToString("0", inv))
              .Append("; przy daktylach Westeros ").Append(rg0[KsTab.GDate].ToString("0", inv)).Append(" -> ").Append(rg1[KsTab.GDate].ToString("0", inv))
              .Append(", Essos ").Append(rg0[NG + KsTab.GDate].ToString("0", inv)).Append(" -> ").Append(rg1[NG + KsTab.GDate].ToString("0", inv))
              .Append(" (RegaleCycle co 28 dob nie rusza soli i daktyli po D+").Append(CtlEnd).Append(")");
            sb.Append(" | przesunieto rak: Westeros ").Append(mvM[0].ToString("0", inv)).Append(", Essos ").Append(mvM[1].ToString("0", inv)).Append("; najwiecej w okregu ")
              .Append(maxPct.ToString("0.00", inv)).Append("% rak poza domem (").Append(maxId).Append("; limit ").Append((100 * F20MaxRel).ToString("0", inv)).Append("%), okregow ponad limit ").Append(nOver)
              .Append(" z ").Append(nDist);
            // 479-d: rozbicie zmiany rak pola miedzy krokami
            if (!meas) sb.Append(" | rece pola miedzy krokami (479-d): brak zajecia w tabeli - bez pomiaru");
            else if (!measPrev) sb.Append(" | rece pola miedzy krokami (479-d): pierwszy krok sesji - migawka zapisana, rozbicie od nastepnego kroku");
            else
            {
                sb.Append(" | rece pola miedzy krokami (479-d, sam pomiar; po poprzednim kroku f20 -> przed tym krokiem = A7 + udzial poza A7 + ludzie bloku + praca domowa + zloza + nowe - znikniete; obok ruch f20 w tym kroku):");
                for (int m = 0; m < 2; m++)
                {
                    sb.Append(m == 0 ? " Westeros" : "; Essos");
                    for (int k = 0; k < MK; k++)
                    {
                        int i = m * MK + k; double prev = _f20PrevAfter[i];
                        double ctl = mNow[i] - (prev + dA7[i] + dXo[i] + dH[i] + dHome[i] + dRf[i] + dNew[i] - dGone[i]);
                        sb.Append(k == 0 ? " " : ", ").Append(F20MeasNames[k]).Append(' ').Append(prev.ToString("0", inv)).Append(" -> ").Append(mNow[i].ToString("0", inv))
                          .Append(" (A7 ").Append(F20Sg(dA7[i], inv)).Append(", udzial poza A7 ").Append(F20Sg(dXo[i], inv)).Append(", ludzie ").Append(F20Sg(dH[i], inv))
                          .Append(", dom ").Append(F20Sg(dHome[i], inv)).Append(", zloza ").Append(F20Sg(dRf[i], inv)).Append(", nowe/znikniete ").Append(F20Sg(dNew[i], inv)).Append('/').Append(F20Sg(-dGone[i], inv))
                          .Append(", kontrola ").Append(ctl.ToString("0", inv)).Append("; f20 ").Append(F20Sg(dF20[i], inv)).Append(')');
                    }
                }
                sb.Append("; potkniecia pomiaru ").Append(measSt);
            }
            if (meas) _f20PrevAfter = mAfter;
            sb.Append(" | pieniadze i towar: bez przelewow - zmiana tylko podzialu rak bloku (rece zajec + rece przy zlozach = rece bloku)")
              .Append(" | czas ").Append(ms.ToString("0.0", inv)).Append(" ms | potkniecia ").Append(_f20Stum).Append('.');
            Log.Info(sb.ToString());
            try { Log.Info(MeatOwnLine(today)); } catch { _meatOwnSt++; }   // 479-h: sam pomiar
            try { Log.Info(DemandShelfLine(today)); } catch { _demShelfSt++; }   // 479-i: sam pomiar
            _f20Stum = 0;
        }

        // 479-h (noc 13.11, sam pomiar): co krok f20 (7 dob) - zywiec (cow/hog/sheep) i mieso (mieso + wieloryb) wsi wedlug rynku z ostatniego rachunku okregu
        // (ld.Res - KsDeliver.Own po zimie i czynnikach rak): produkcja, zjedzone na wlasny stol (Own), na targ; do porownania z linia "KS: mieso Planu (479-h)". Tylko odczyt
        private static int _meatOwnSt;
        private static string MeatOwnLine(int today)
        {
            var inv = CultureInfo.InvariantCulture;
            var a = new double[2 * 9]; var nv = new int[2];
            var tf = new double[2 * MeatTfN]; var ra = new double[2 * MeatRaN]; var nr = new int[2];   // 479-i
            // [m * 9 + k]: 0 zywiec produkcja, 1 zywiec zjedzony, 2 zywiec na targ, 3 mieso produkcja, 4 mieso zjedzone, 5 mieso na targ, 6 potrzeba miesa z wlasnego pola, 7 hog na targ, 8 hog zjedzony
            int[] live = { KsDeliver.ICow, KsDeliver.IHog, KsDeliver.ISheep }, meat = { KsDeliver.IMeat, KsDeliver.IWhaleMeat };
            if (_live != null)
                foreach (var ld in _live.Values)
                {
                    try
                    {
                        if (ld == null || ld.Res == null) continue;
                        if (ld.TF) { MeatTfAdd(ld, tf); continue; }                              // 479-i: pola mieszczan osobno (dawne kolumny - tylko wsie jak w 479-h)
                        MeatRateAdd(ld, ra, nr);                                              // 479-i: stawka wsi po Buildings338Village.ScaleRates (to, co gra dostaje)
                        var r = ld.Res; int o = (ld.Market == 0 ? 0 : 1) * 9;
                        nv[o / 9]++;
                        foreach (int i in live) { a[o + 0] += r.Prod[i]; a[o + 1] += r.Own[i]; a[o + 2] += r.MarketKg[i]; }
                        foreach (int i in meat) { a[o + 3] += r.Prod[i]; a[o + 4] += r.Own[i]; a[o + 5] += r.MarketKg[i]; }
                        if (ld.OwnNeed != null && ld.OwnNeed.Length > KsTab.NMeat) a[o + 6] += ld.OwnNeed[KsTab.NMeat];
                        a[o + 7] += r.MarketKg[KsDeliver.IHog]; a[o + 8] += r.Own[KsDeliver.IHog];
                    }
                    catch { _meatOwnSt++; }
                }
            var sb = new StringBuilder(700);
            sb.Append("Jedzenie KS (479-h): mieso wsi dzien ").Append(today).Append(" (sam pomiar; kg/d z ostatniego rachunku okregu - po zimie i czynnikach rak; zywiec w kg zywca, mieso = zywiec x ")
              .Append(KsTab.MeatOfLive.ToString("0.00", inv)).Append(")");
            for (int m = 0; m < 2; m++)
            {
                int o = m * 9;
                sb.Append(m == 0 ? " | Westeros" : " | Essos").Append(" (wsi ").Append(nv[m]).Append("): zywiec cow/hog/sheep produkcja ").Append(a[o].ToString("0", inv))
                  .Append(", na wlasny stol (KsDeliver.Own) ").Append(a[o + 1].ToString("0", inv)).Append(" (= mieso ").Append((a[o + 1] * KsTab.MeatOfLive).ToString("0", inv)).Append(")")
                  .Append(", na targ ").Append(a[o + 2].ToString("0", inv)).Append(" (= mieso ").Append((a[o + 2] * KsTab.MeatOfLive).ToString("0", inv)).Append(")")
                  .Append(", z tego hog: stol ").Append(a[o + 8].ToString("0", inv)).Append(", targ ").Append(a[o + 7].ToString("0", inv))
                  .Append("; mieso i wieloryb produkcja ").Append(a[o + 3].ToString("0", inv)).Append(", na wlasny stol ").Append(a[o + 4].ToString("0", inv)).Append(", na targ ").Append(a[o + 5].ToString("0", inv))
                  .Append("; potrzeba miesa z wlasnego pola ").Append(a[o + 6].ToString("0", inv));
            }
            // 479-i: pola mieszczan (ld.TF) i stawki wsi po ScaleRates (drogi, budynki BK, 338c) - w sztukach gry na dobe i w kg (kg na sztuke z rachunku okregu)
            for (int m = 0; m < 2; m++)
            {
                int o = m * MeatTfN, q = m * MeatRaN;
                sb.Append(m == 0 ? " | 479-i Westeros" : " | 479-i Essos")
                  .Append(": pola mieszczan (okregow ").Append(tf[o + 8].ToString("0", inv)).Append(") zywiec produkcja ").Append(tf[o].ToString("0", inv)).Append(" kg, na targ ").Append(tf[o + 1].ToString("0", inv))
                  .Append(" kg (").Append(tf[o + 2].ToString("0.0", inv)).Append(" szt., x beta rampy ").Append(tf[o + 3].ToString("0.0", inv)).Append(" szt.)")
                  .Append(", mieso i wieloryb produkcja ").Append(tf[o + 4].ToString("0", inv)).Append(" kg, na targ ").Append(tf[o + 5].ToString("0", inv))
                  .Append(" kg (").Append(tf[o + 6].ToString("0.0", inv)).Append(" szt., x beta rampy ").Append(tf[o + 7].ToString("0.0", inv)).Append(" szt.)")
                  .Append("; stawki wsi po ScaleRates (wsi ze stawka ").Append(nr[m]).Append(") zywiec ").Append(ra[q].ToString("0.0", inv)).Append(" szt. = ").Append(ra[q + 1].ToString("0", inv))
                  .Append(" kg (= mieso ").Append((ra[q + 1] * KsTab.MeatOfLive).ToString("0", inv)).Append("), x beta rampy ").Append(ra[q + 2].ToString("0.0", inv)).Append(" szt.")
                  .Append(", mieso i wieloryb ").Append(ra[q + 3].ToString("0.0", inv)).Append(" szt. = ").Append(ra[q + 4].ToString("0", inv)).Append(" kg, x beta rampy ").Append(ra[q + 5].ToString("0.0", inv)).Append(" szt.")
                  .Append("; dodatek ScaleRates wobec rachunku okregu: zywiec ").Append(F20Sg(ra[q + 1] - ra[q + 6], inv)).Append(" kg, mieso ").Append(F20Sg(ra[q + 4] - ra[q + 7], inv)).Append(" kg");
            }
            sb.Append(" (beta < 1 - rampa: gra miesza (1 - beta) x lancuch gry + beta x stawka; czesc lancucha gry tu niewidoczna)");
            sb.Append(" | potkniecia pomiaru ").Append(_meatOwnSt).Append(" | gra bez zmian.");
            return sb.ToString();
        }

        // 479-i (noc 13.11, sam pomiar): pola mieszczan - [m * MeatTfN + k]: 0 zywiec produkcja kg, 1 zywiec na targ kg, 2 zywiec na targ szt., 3 to samo x beta (DeliverTownFields),
        // 4 mieso produkcja kg, 5 mieso na targ kg, 6 mieso na targ szt., 7 to samo x beta, 8 okregow. Tylko odczyt ld.Res
        private const int MeatTfN = 9, MeatRaN = 8;
        private static readonly int[] MeatLiveIx = { KsDeliver.ICow, KsDeliver.IHog, KsDeliver.ISheep }, MeatMeatIx = { KsDeliver.IMeat, KsDeliver.IWhaleMeat };
        private static void MeatTfAdd(LiveDist ld, double[] tf)
        {
            try
            {
                var r = ld.Res; int m = ld.Market == 0 ? 0 : 1; int o = m * MeatTfN;
                tf[o + 8] += 1;
                foreach (int i in MeatLiveIx) { tf[o] += r.Prod[i]; tf[o + 1] += r.MarketKg[i]; tf[o + 2] += r.Market[i]; tf[o + 3] += r.Market[i] * Beta(m, i); }
                foreach (int i in MeatMeatIx) { tf[o + 4] += r.Prod[i]; tf[o + 5] += r.MarketKg[i]; tf[o + 6] += r.Market[i]; tf[o + 7] += r.Market[i] * Beta(m, i); }
            }
            catch { _meatOwnSt++; }
        }

        // 479-i (noc 13.11, sam pomiar): stawka wsi _rates[V].A (sztuki gry na dobe po Buildings338Village.ScaleRates - to bierze ProdStep) - [m * MeatRaN + k]:
        // 0 zywiec szt., 1 zywiec kg, 2 zywiec szt. x beta, 3 mieso szt., 4 mieso kg, 5 mieso szt. x beta, 6 zywiec kg z rachunku okregu (MarketKg), 7 mieso kg z rachunku.
        // kg na sztuke = MarketKg / Market rachunku okregu (sigma x kg sztuki, ten sam dzielnik co stawka); bez towaru na targ - sigma x waga sztuki gry. Tylko odczyt
        private static void MeatRateAdd(LiveDist ld, double[] ra, int[] nr)
        {
            try
            {
                var rates = _rates; VRate vr;
                if (ld.V == null || rates == null || !rates.TryGetValue(ld.V, out vr) || vr == null || vr.A == null) return;
                var r = ld.Res; int m = vr.M == 0 ? 0 : 1; int o = m * MeatRaN;
                nr[m]++;
                double sig = Math.Max(1, DailyLife.KsSigma);
                for (int pass = 0; pass < 2; pass++)
                    foreach (int i in pass == 0 ? MeatLiveIx : MeatMeatIx)
                    {
                        if (i >= vr.A.Length) continue;
                        double a = Math.Max(0.0, vr.A[i]);
                        double kpp = r.Market[i] > 1e-12 ? r.MarketKg[i] / r.Market[i] : sig * (_itemKg != null && i < _itemKg.Length ? _itemKg[i] : 10.0);
                        int b = pass == 0 ? 0 : 3;
                        ra[o + b] += a; ra[o + b + 1] += a * kpp; ra[o + b + 2] += a * Beta(m, i);
                        ra[o + 6 + pass] += r.MarketKg[i];
                    }
            }
            catch { _meatOwnSt++; }
        }

        // 479-i (noc 13.11, sam pomiar): co krok f20 (7 dob) - popyt zaplacony gry, z ktorego idzie cel polki T* (ZTstar x (Q + QV + Der + One), jak w kroku f20),
        // jego skladniki na dobe, polki, w drodze, zuzycie w ksiedze towarow (ostatnia doba ksiegi i srednia 28 dob sterowania) i popyt ostatniego Planu w tych samych
        // sztukach (kg / (sigma x kg sztuki ZC4) - ten sam dzielnik co TownItems). Mieso = pozycje meat + WhaleMeat (CtlGood); zboze = tylko pozycja grain (chleb, placek,
        // piwo poza T* ziarna, a w popycie Planu ziarna sa). Tylko odczyt - nic nie zmienia
        private static int _demShelfSt;
        private static readonly int[] DemGoods = { KsTab.GMeat, KsTab.GGrain, KsTab.GCheese, KsTab.GButter, KsTab.GFish };
        private static readonly Dictionary<string, long[]> _demLedger = new Dictionary<string, long[]>(StringComparer.Ordinal);
        private static int DemZ(int g)
        {
            return g == KsTab.GMeat ? ZMeat : g == KsTab.GGrain ? ZGrain : g == KsTab.GCheese ? ZCheese : g == KsTab.GButter ? ZButter : g == KsTab.GFish ? ZFish : -1;
        }

        private static string DemandShelfLine(int today)
        {
            var inv = CultureInfo.InvariantCulture; int NG = KsTab.NG;
            var sb = new StringBuilder(2500);
            sb.Append("Jedzenie KS (479-i): popyt a polki dzien ").Append(today)
              .Append(" (sam pomiar; szt./d: popyt zaplacony gry = Q lud miasta + QV wsie kupujace + Der wsad przetworow + One 1:1, T* = suma dni polki x popyt; polki, w drodze KS-H w szt.;")
              .Append(" ksiega - zuzycie z targu rynku i produkcja na targ ostatniej doby ksiegi; srednia 28 dob - zuzycie i brak sterowania rynku; Plan - popyt ostatniego Planu kg/d i szt./d)");
            var map = _zt;
            if (map == null || map.Count == 0) { sb.Append(" | brak mapy koszyka ZC4 | potkniecia pomiaru ").Append(_demShelfSt).Append(" | gra bez zmian."); return sb.ToString(); }
            var q = new double[2 * NG]; var qv = new double[2 * NG]; var der = new double[2 * NG]; var one = new double[2 * NG];
            var tt = new double[2 * NG]; var sh = new double[2 * NG]; var rd = new double[2 * NG]; var nTown = new int[2];
            foreach (var zt in map.Values)
            {
                try
                {
                    if (zt == null || zt.St == null) continue;
                    int m = zt.Market == 0 ? 0 : 1; nTown[m]++;
                    for (int z = 0; z < ZN; z++)
                    {
                        int g = CtlGood(z); if (g < 0) continue;
                        int i = m * NG + g;
                        q[i] += zt.Q[z]; qv[i] += zt.QV[z]; der[i] += zt.Der[z]; one[i] += zt.One[z];
                        double dp = zt.Q[z] + zt.QV[z] + zt.Der[z] + zt.One[z];
                        if (dp > 0 && !double.IsNaN(dp) && !double.IsInfinity(dp)) tt[i] += ZTstar(m, z) * dp;
                        sh[i] += Math.Max(0, zt.Cnt[z]);
                        int inc = 0; try { inc = KsFoodCarts.Incoming(zt.St, z); } catch { inc = 0; _demShelfSt++; }
                        rd[i] += Math.Max(0, inc);
                    }
                }
                catch { _demShelfSt++; }
            }
            // ksiega towarow (GoodsLedger.KsMarketTake: [0 + m] produkcja na targ m, [3 + m] zuzycie z targu m) - kopia liczb ostatniej doby ksiegi
            var lp = new double[2 * NG]; var lc = new double[2 * NG]; int lday = -1;
            try
            {
                lday = GoodsLedger.KsMarketTake(_demLedger);
                if (lday >= 0)
                    foreach (var kv in _demLedger)
                    {
                        int z = Array.IndexOf(ZIds, kv.Key); if (z < 0) continue;
                        int g = CtlGood(z); if (g < 0 || kv.Value == null || kv.Value.Length < 5) continue;
                        for (int m = 0; m < 2; m++) { lp[m * NG + g] += Math.Max(0, kv.Value[m]); lc[m * NG + g] += Math.Max(0, kv.Value[3 + m]); }
                    }
            }
            catch { lday = -1; _demShelfSt++; }
            var pl = _plan; double sig = Math.Max(1, DailyLife.KsSigma); var zkg = _zKg;
            string[] mn = { "Westeros", "Essos" };
            for (int m = 0; m < 2; m++)
            {
                sb.Append(" | ").Append(mn[m]).Append(" (miast ").Append(nTown[m]).Append("):");
                bool f = true;
                foreach (int g in DemGoods)
                {
                    try
                    {
                        int i = m * NG + g; double dp = q[i] + qv[i] + der[i] + one[i];
                        int z = DemZ(g);
                        bool kz = zkg != null && z >= 0 && z < zkg.Length && zkg[z] > 0;
                        sb.Append(f ? " " : "; ").Append(KsTab.Goods[g]).Append(" popyt zaplacony ").Append(dp.ToString("0", inv))
                          .Append(" (Q ").Append(q[i].ToString("0", inv)).Append(", QV ").Append(qv[i].ToString("0", inv)).Append(", Der ").Append(der[i].ToString("0", inv)).Append(", One ").Append(one[i].ToString("0", inv))
                          .Append("), T*/7 ").Append((tt[i] / 7.0).ToString("0", inv)).Append(" (T* ").Append(tt[i].ToString("0", inv)).Append(", dni polki ").Append(dp > 0 ? (tt[i] / dp).ToString("0.0", inv) : "-")
                          .Append("), polki ").Append(sh[i].ToString("0", inv)).Append(", w drodze ").Append(rd[i].ToString("0", inv));
                        if (lday >= 0) sb.Append(", ksiega doba ").Append(lday).Append(": zuzycie ").Append(lc[i].ToString("0", inv)).Append(", produkcja ").Append(lp[i].ToString("0", inv));
                        else sb.Append(", ksiega brak");
                        sb.Append(", srednia 28 dob: zuzycie ").Append(_c28[i].ToString("0", inv)).Append(", brak ").Append(_u28[i].ToString("0", inv));
                        if (pl != null && pl.D != null && i < pl.D.Length)
                        {
                            double kg = pl.D[i];
                            sb.Append(", Plan popyt ").Append(kg.ToString("0", inv)).Append(" kg");
                            if (kz) sb.Append(" (").Append((kg / (sig * zkg[z])).ToString("0", inv)).Append(" szt.)");
                            if (pl.Eaten != null && i < pl.Eaten.Length)
                            {
                                double mk = Math.Max(0.0, kg - pl.Eaten[i]);
                                sb.Append(", Plan popyt na targ ").Append(mk.ToString("0", inv)).Append(" kg");
                                if (kz) sb.Append(" (").Append((mk / (sig * zkg[z])).ToString("0", inv)).Append(" szt.)");
                            }
                        }
                        else sb.Append(", Plan brak");
                    }
                    catch { _demShelfSt++; }
                    f = false;
                }
            }
            sb.Append(" | sigma ").Append(sig.ToString("0", inv)).Append(", kg na sztuke ZC4:");
            for (int k = 0; k < DemGoods.Length; k++)
            {
                int z = DemZ(DemGoods[k]);
                sb.Append(k == 0 ? " " : ", ").Append(ZIds[z]).Append(' ').Append(zkg != null && z >= 0 && z < zkg.Length ? zkg[z].ToString("0.00", inv) : "-");
            }
            sb.Append(" | potkniecia pomiaru ").Append(_demShelfSt).Append(" | gra bez zmian.");
            return sb.ToString();
        }

        private static string F20Sg(double v, CultureInfo inv) { return (v >= 0 ? "+" : "") + v.ToString("0", inv); }

        private sealed class F20Count
        {
            public int Chg, Flat, Single, Lock, LandCut, LandStop, Applied, Tree, HeldReset;
            public void Clear() { Chg = Flat = Single = Lock = LandCut = LandStop = Applied = Tree = HeldReset = 0; }
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

        /// <summary>Rece pola bloku miedzy jego zajeciami (replikator z krokiem 0.05, najwyzej 5% rak zajecia, ziemia bloku). Zwraca rece przesuniete (dodatnia czesc);
        /// hc1 - rece pola wedlug zajecia po kroku (tylko gdy krok zmienil blok - cnt.Applied). groves false (klucz Ks Food Hands Grow Groves NIE): drzewo (Tree()), ktore by
        /// zyskalo rece, wychodzi z kroku (udzial bez zmian) i replikator liczy sie od nowa bez niego - drzewa tylko oddaja rece (sadzenie drzew to A7, 364 doby).</summary>
        private static double F20Block(LiveDist ld, LiveBlock b, int m, string type, bool guard, bool groves, double fh, double[] sc, double[] hc1, double[] mvG, F20Count cnt)
        {
            int n = Math.Min(b.C.Length, b.X.Length), NC = KsTab.NC;
            if (n < 2) { cnt.Single++; return 0; }
            if (KsWorksLock(ld, b)) { cnt.Lock++; return 0; }                       // inwest: czesc z pracami okregu nie zmienia zajecia (jak A7)
            var part = new bool[n];
            for (int j = 0; j < n; j++)
            {
                int c = b.C[j];
                if (!(b.X[j] >= F20MinX) || F20Horse(c) || KsTab.IsRegale[c]) continue;
                if (guard && !FoodCraft(c)) continue;                                // najpierw wyzywic siebie: rece tylko miedzy zajeciami przy jedzeniu
                if (!(b.L(c, type) > 0)) continue;                                    // tylko zajecia, na ktore ziemia bloku pozwala (tabela swiata)
                part[j] = true;
            }
            var d = new double[n]; double t, big;
            for (int pass = 0; ; pass++)
            {
                int np = 0; double xp = 0, ap = 0;
                for (int j = 0; j < n; j++) { if (!part[j]) continue; np++; xp += b.X[j]; ap += b.X[j] * (1.0 + F20Rate * sc[m * NC + b.C[j]]); }
                if (np < 2 || !(xp > 0) || !(ap > 0)) { cnt.Single++; return 0; }
                t = 1.0; big = 0;
                for (int j = 0; j < n; j++)
                {
                    d[j] = 0;
                    if (!part[j]) continue;
                    d[j] = xp * b.X[j] * (1.0 + F20Rate * sc[m * NC + b.C[j]]) / ap - b.X[j];
                    double ad = Math.Abs(d[j]); if (ad > big) big = ad;
                    if (ad > 1e-12) t = Math.Min(t, F20MaxRel * b.X[j] / ad);
                }
                // recenzja (klucz Ks Food Hands Grow Groves NIE): drzewo, ktore by zyskalo, zostaje przy swoim udziale - reszta bloku od nowa (najwyzej n przejsc)
                bool drop = false;
                if (!groves && pass < n)
                    for (int j = 0; j < n; j++) if (part[j] && Tree(b.C[j]) && d[j] > 1e-12) { part[j] = false; drop = true; cnt.Tree++; }
                if (!drop) break;
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
            // 479-c (noc 13.11, decyzja domyslna - recenzja 479-a): nacisk A7 (b.Held) liczony jest wobec glownego zajecia bloku (argmax X, ta sama zasada co petla A7 -
            // pierwsze najwieksze); gdy krok f20 zmieni glowne zajecie, nacisk dotyczyl innego zajecia - zeruje sie (A7 liczy 60 dob od nowa)
            int ciA = 0; for (int j = 1; j < b.X.Length; j++) if (b.X[j] > b.X[ciA]) ciA = j;
            double moved = 0, s = 0;
            for (int j = 0; j < n; j++)
            {
                if (part[j]) { double nx = Math.Max(0.0, b.X[j] + t * d[j]); b.X[j] = (float)nx; }
                s += b.X[j];
            }
            if (s > 0) for (int j = 0; j < n; j++) b.X[j] = (float)(b.X[j] / s);
            int ciB = 0; for (int j = 1; j < b.X.Length; j++) if (b.X[j] > b.X[ciB]) ciB = j;
            if (ciB != ciA && b.Held > 0 && A7RuleOn) { b.Held = 0; cnt.HeldReset++; }
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

        /// <summary>Zima (recenzja): regale bez zmian - tylko rece przy soli i daktylach do linii (przed = po).</summary>
        private static void F20RegHands(LiveBlock b, int m, double[] rg0, double[] rg1)
        {
            if (b.RC == null || b.RF == null) return;
            int n = Math.Min(b.RC.Length, b.RF.Length), NG = KsTab.NG;
            for (int j = 0; j < n; j++)
            {
                if (!F20RegCraft(b.RC[j])) continue;
                int g = KsTab.RegGood(b.RC[j]); if (g < 0) continue;
                double h = b.RF[j] * b.Hands; rg0[m * NG + g] += h; rg1[m * NG + g] += h;
            }
        }

        /// <summary>Regale jedzenia (sol, sol morska, daktyle): BRAK x1.05, NADMIAR x0.95; wzrost przycinany do sufitu bloku najwyzej do sumy sprzed kroku (jak f5-ruda).
        /// Zwraca rece przesuniete (|zmiana| x rece bloku, dodatnia czesc - liczona raz). Recenzja: okreg w progu "najpierw wyzywic siebie" (guard) - zloze bez jedzenia
        /// (sol, sol morska - FoodCraft false) bez wzrostu (wzrost bralby rece z pol, takze z pol jedzenia); klucz Ks Food Hands Grow Groves NIE - gaj daktylowy (Tree()) bez wzrostu.</summary>
        private static double F20Regale(LiveBlock b, int m, int[] sig, bool guard, bool groves, double[] rg0, double[] rg1, int[] rUp, int[] rDn, double[] mvG, ref int nCap,
                                        ref int nGuardUp, ref int nTreeUp)
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
                if (s > 0 && guard && !FoodCraft(b.RC[j])) { nGuardUp++; continue; }       // prog wyzywienia: sol nie bierze rak z pol
                if (s > 0 && !groves && Tree(b.RC[j])) { nTreeUp++; continue; }           // drzewa bez wzrostu (Grow Groves NIE)
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
