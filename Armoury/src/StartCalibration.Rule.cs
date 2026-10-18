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
    /// KS - REGULA ZMIANY ZAJEC PO WLACZENIU (A7, projekt KS rozdz. 3.1 i 11.2, dopisek Jeffa do Prac okregu) i REGALE (rozdz. 3.2):
    ///  - co 7 dob OD DOBY D (koniec rampy podazy; recenzja 16.10 - nie od wlaczenia: ceny rampy to jeszcze dzisiejsze braki) kazdy blok liczy dochod rodziny na pracujacego
    ///    kazdego swojego zajecia przy 28-dobowym indeksie cen miasta targowego wsi (srednia zerowana w dobie D, po polkach startu):
    ///    L x suma(P0 x wartosc kg x indeks) - K - renta ziemi z Planu x ha (ta sama funkcja co Plan, ceny gry zamiast cen cienia); zywiec po indeksie zywca (cow, hog,
    ///    sheep - to sprzedaje wies; wartosc kg jak w Planie: mieso i skory z uboju), konie poza KS (lancuch gry) - bez wartosci;
    ///  - HAMULEC JEFFA: zmiana, gdy inne zajecie daje >= 1.25 x dochodu biezacego (najwiekszy udzial rak bloku) bez przerwy przez 60 dob; najwyzej JEDNA czesc okregu
    ///    (blok wsi gry albo wioska poboczna) w przebudowie naraz; najwyzej 5% rak pola regionu (warownia i jej wsie) na sezon (91 dob); licznik regionu i srednie cen
    ///    w zapisie (arm_ks) - wczytanie nie daje nowego budzetu ani sredniej z jednej doby;
    ///  - ZIEMIA (recenzja 16.10): zmiana nie zaczyna sie i rece nie przechodza, gdy ziemia zajeta bloku (dom, zboze wlasne, rece x ha na pracujacego) przekroczylaby jego
    ///    powierzchnie (owce 120 ha, bydlo 18 ha na pracujacego) - produkcja tylko z rak NA ziemi;
    ///  - KOSZT PRZEBUDOWY: 28 dob kosztu K nowego zajecia za przenoszone rece (w zl gry: / sigma), placony co dobe z kiesy wsi do kasy miasta targowego (rzemieslnicy
    ///    i kupcy miasta robia sprzet) - najwyzej czwarta czesc kiesy na dobe; doba bez zaplaty = przebudowa stoi (postep proporcjonalny do zaplaty);
    ///  - OKRES PRZEJSCIOWY: rece przechodza liniowo przez 60 dob (winnice, gaje, daktyle, sady - 364 doby): stary towar gasnie, nowy rosnie;
    ///  - REGALE: co 28 dob od D rece przy zlozu +10% przy indeksie towaru w miescie > 1.2, -10% przy < 0.8 (sufit - polowa rak bloku poza domem, dno 0).
    ///  - Klucz Start Calibration Switch Rule wylaczony (domyslnie od recenzji 16.10 - ceny startu nie sa cenami cienia): sam POMIAR nacisku (licznik 60 dob, linia),
    ///    bez zmian zajec; regale dzialaja.
    ///  - f5-a7 (noc 17/18.10, StartCalibration.A7s.cs): klucz Ks Switch Signal - dochod liczony przy cenach KS (cena cienia Planu x gradient krainy x ograniczone odchylenie
    ///    rynku miasta, srednia 4 krokow) zamiast indeksu gry; klucz Ks Switch Signal Rule (domyslnie WYL. do pomiaru; autotest: plik armoury-a7-wlacz.flag) - zmiany
    ///    naprawde (ten sam hamulec), rata przebudowy co 7 dob rozlozona na doby tygodnia (hasz bloku), granica kiesy jak przy racie dziennej.
    /// Pola mieszczan - ta sama regula bez kosztu w zlocie (zloza i sprzet miasta). Pieniadz: tylko przelew kiesa wsi -> kasa miasta (MoneyLedger MKs).
    /// </summary>
    internal static partial class StartCalibration
    {
        private const double SwitchRatio = 1.25;
        private const int HoldDays = 60, StepDays = 7, CostDays = 28, Season = 91, RegCycle = 28;
        private const double RegionShare = 0.05;
        private const double CeilIx = 9.5, FloorIx = 0.11;                 // indeks na suficie / podlodze wzoru ceny gry (0.1 - 10)
        private static readonly Dictionary<Town, double[]> _px = new Dictionary<Town, double[]>();
        private static readonly Dictionary<Town, double[]> _pxNow = new Dictionary<Town, double[]>();   // ostatni odczyt (sufit / podloga w linii cen)
        private static readonly Dictionary<Town, int> _pxN = new Dictionary<Town, int>();
        private static Dictionary<string, KeyValuePair<int, double[]>> _pxLoad;   // srednie z zapisu (id miasta) - do pierwszego odczytu sesji
        private static int _pxDays, _pxDay = -1;
        private static ItemObject _hides;
        private static double[] _mv, _valKg;
        private static readonly Dictionary<string, double> _regionMoved = new Dictionary<string, double>(StringComparer.Ordinal);
        private static int _regionSeason = -1;
        // liczniki do linii (od ostatniej linii)
        private static int _rPress, _rMaxHeld, _rStarted, _rDone, _rStall, _rBudget, _rBusy, _rUp, _rDown, _rActive, _rCancel, _rWould, _rLand, _rLandStall; private static long _rPaid;
        private static int _rWinStarted;                                   // zmiany zaczete od doby D (nks: prawdziwe zmiany A7 w oknie <= 1% wiosek)

        private static void RuleReset()
        {
            _k4Clean = false; _k4Live = null;                     // ks-koszt T10 (K4a)
            _px.Clear(); _pxNow.Clear(); _pxN.Clear(); _pxLoad = null; _pxDays = 0; _pxDay = -1; _hides = null; _mv = null; _valKg = null;
            _regionMoved.Clear(); _regionSeason = -1; _rFeed = 0;
            _rPress = _rMaxHeld = _rStarted = _rDone = _rStall = _rBudget = _rBusy = _rUp = _rDown = _rActive = _rCancel = _rWould = _rLand = _rLandStall = 0; _rPaid = 0; _rWinStarted = 0;
            _pxRestartDay = -1; _pxRestartEnd = -1;               // KS r4 P7 (r4-ceny): restart srednich cen od nowa przy wlaczeniu
            _f4A7Day = -1;                                        // f4-pomiary: doba ostatniej diagnozy A7 (nowa kampania / wczytanie)
            F5Reset();                                            // f5-koszt: liczniki linii "KS: koszt f5" i kontrola raz na sesje
            F20Reset();                                           // f20-d4: pamiec polek rynku (tempo) i wagi zajec od nowa
            A7sReset();                                           // f5-a7: sygnal KS reguly (srednie cen miast, rodzaj Held, sezon) od nowa
        }

        /// <summary>Doba D (po polkach startu): srednie cen miast od zera - A7 i regale licza od D na cenach po rampie (recenzja 16.10).</summary>
        private static void RulePxReset() { _px.Clear(); _pxNow.Clear(); _pxN.Clear(); _pxLoad = null; _pxDays = 0; _pxDay = -1; }

        /// <summary>Indeksy cen miast dla towarow KS (zwykla srednia przez 4 odczyty, potem wykladnicza 1/4). Stan zastosowany: co dobe, miasto co 7 dob na zmiane
        /// (ok. 28 dob; poprawka po autotescie 16.10 - koszt doby KS, dotad co 2 doby i 14 odczytow). Plan i Uzbrojony (recenzja 16.10 - koszt): tylko w doby linii cen
        /// (co 7 dob), srednia z 4 odczytow - sama linia "KS: ceny miast".</summary>
        private static void PricesDaily(int today)
        {
            if (_pxDay == today) return;
            bool applied = _state == StateApplied;
            if (!applied && !(LogGate.Lines && (today - 1) % 7 == 0)) return;
            _pxDay = today;
            EnsureItems();
            if (_hides == null) _hides = ItemOf("hides");
            _pxDays++;
            try
            {
                PxResolveLoad();
                int k = 0;
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || t.Settlement == null) continue;
                    double[] a;
                    bool fresh = !_px.TryGetValue(t, out a);
                    // koszt doby KS (poprawka po autotescie 16.10: co 2 -> co 7 dob): miasto co 7 dob na zmiane (srednia z 4 odczytow = ok. 28 dob), nowe miasto od razu
                    if (applied && !fresh && ((k++ + today) % 7) != 0) continue;
                    if (fresh) { a = new double[ItemCount]; _px[t] = a; }
                    double[] now;
                    if (!_pxNow.TryGetValue(t, out now)) { now = new double[ItemCount]; _pxNow[t] = now; }
                    int nu; _pxN.TryGetValue(t, out nu); nu++; _pxN[t] = nu;
                    double w = 1.0 / Math.Min(4, nu);
                    for (int i = 0; i < ItemCount; i++)
                    {
                        var it = _items[i];
                        double ix = 1.0;
                        if (it != null && it.ItemCategory != null) { try { ix = t.GetItemCategoryPriceIndex(it.ItemCategory); } catch { ix = 1.0; } }
                        if (!(ix > 0) || double.IsInfinity(ix) || double.IsNaN(ix)) ix = 1.0;
                        now[i] = ix;
                        a[i] = fresh ? ix : a[i] + (ix - a[i]) * w;
                    }
                }
            }
            catch (Exception e) { Stumble("PricesDaily", e); }
        }

        /// <summary>Srednie cen z zapisu -> miasta tej sesji (raz, przy pierwszym odczycie po wczytaniu).</summary>
        private static void PxResolveLoad()
        {
            var ld = _pxLoad; if (ld == null) return;
            _pxLoad = null;
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t.Settlement == null || t.Settlement.StringId == null) continue;
                KeyValuePair<int, double[]> kv;
                if (!ld.TryGetValue(t.Settlement.StringId, out kv) || kv.Value == null || kv.Value.Length != ItemCount) continue;
                _px[t] = (double[])kv.Value.Clone(); _pxN[t] = kv.Key;
            }
        }

        /// <summary>Indeks ceny towaru KS g w miescie (srednia): mieso z uboju - indeks miesa, skory - skor, zywiec - zywca, kon - 1.</summary>
        private static double Px(double[] px, int g)
        {
            if (g == KsTab.GMeat) return px[IMeat];
            if (g == KsTab.GHides) return px[KsDeliver.IHides];
            int it = GoodToItem[g];
            return it >= 0 ? px[it] : 1.0;
        }

        private static void EnsureValues()
        {
            if (_mv != null) return;
            EnsureItems();
            var val = new double[KsTab.NG];
            for (int g = 0; g < KsTab.NG; g++)
            {
                ItemObject it = null;
                string id = KsTab.Goods[g];
                if (g == KsTab.GHides) it = _hides ?? ItemOf("hides");
                else if (g == KsTab.GMeat) it = _items[IMeat];
                else { int i = GoodToItem[g]; it = i >= 0 ? _items[i] : ItemOf(id); }
                val[g] = it != null ? Math.Max(0, it.Value) / Math.Max(1.0, it.Weight) : 1.0;
            }
            val[KsTab.GHorse] = 0.0;              // recenzja 16.10: konie robi lancuch gry (poza KS) - rece stadnin nie zarabiaja na koniach w regule
            _valKg = val;
            _mv = RuleCraftValue(val);
        }

        /// <summary>Wartosc towarow zajecia na pracujacego przy ziemi 1 [c * NG + g] dla reguly w grze: jak KsCore.CraftValue, ale zywiec zostaje zywcem (indeks cow / hog / sheep
        /// - to wies sprzedaje na targu), z wartoscia kg Planu (polowa miesa i skory z uboju).</summary>
        private static double[] RuleCraftValue(double[] val)
        {
            var mv = new double[KsTab.NC * KsTab.NG];
            for (int c = 0; c < KsTab.NC; c++)
                foreach (var kv in KsTab.P0[c])
                {
                    int g = kv.Key; double r = kv.Value;
                    if (g == KsTab.GCow || g == KsTab.GHog || g == KsTab.GSheep) mv[c * KsTab.NG + g] += r * (KsTab.MeatOfLive * val[KsTab.GMeat] + KsTab.HideOfLive(g) * val[KsTab.GHides]);
                    else if (g == KsTab.GWhaleMeat) mv[c * KsTab.NG + KsTab.GMeat] += r * val[KsTab.GMeat];
                    else mv[c * KsTab.NG + g] += r * val[g];
                }
            return mv;
        }

        /// <summary>Dochod rodziny na pracujacego zajecia c w bloku przy indeksach cen miasta (regula 3.1 przy cenach gry).</summary>
        private static double Income(LiveBlock b, string type, int c, double[] px)
        {
            double L = b.L(c, type);
            double v = 0; int o = c * KsTab.NG;
            for (int g = 0; g < KsTab.NG; g++) { double m = _mv[o + g]; if (m != 0) v += m * Px(px, g); }
            return L * v - KsTab.KCost[c] - b.Rent * KsTab.Ha(c, b.K);      // r4-polnoc: ha wedlug strefy bloku (bez klucza Ks Cold Land - HaPerWorker[c])
        }

        /// <summary>ks-koszt T2: Income z wartoscia towarow zajecia c z wiersza miasta (liczona raz ta sama petla co w Income) - ten sam wynik co do bitu.</summary>
        private static double IncomeV(LiveBlock b, string type, int c, double[] px, double[] vRow)
        {
            double L = b.L(c, type);
            double v = vRow[c];
            if (double.IsNaN(v))
            {
                v = 0; int o = c * KsTab.NG;
                for (int g = 0; g < KsTab.NG; g++) { double m = _mv[o + g]; if (m != 0) v += m * Px(px, g); }
                vRow[c] = v;
            }
            return L * v - KsTab.KCost[c] - b.Rent * KsTab.Ha(c, b.K);
        }

        private static bool Tree(int c) { return c == KsTab.CVine || c == KsTab.COlive || c == KsTab.CDate || c == KsTab.CCitrus; }

        private static int IndexOfCraft(LiveBlock b, int c) { if (b.C != null) for (int i = 0; i < b.C.Length; i++) if (b.C[i] == c) return i; return -1; }

        private static void RuleDaily(int today)
        {
            var s = Settings.Current;
            bool rule = A7RuleOn;                                             // f5-a7: dawny klucz albo sygnal KS z kluczem Ks Switch Signal Rule (klucze f5 wylaczone = Start Calibration Switch Rule)
            bool sig = A7sOn;                                                 // f5-a7 (klucz Ks Switch Signal): dochod przy cenach KS, nie przy indeksie gry
            long a7t0 = sig ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            EnsureValues();
            int season = today / Season;
            if (season != _regionSeason) { if (sig) A7sSeasonClose(_regionSeason); _regionSeason = season; _regionMoved.Clear(); }   // f5-a7: linia sezonu zamknietego przed zerowaniem rak regionu
            int d0 = DDay;                                                    // recenzja 16.10: A7 i regale od D (nie od wlaczenia)
            bool step = d0 >= 0 && today > d0 && (today - d0) % StepDays == 0;
            bool reg = d0 >= 0 && today > d0 && (today - d0) % RegCycle == 0;
            if (step) { if (sig) { try { A7sStepBegin(today); } catch (Exception e) { Stumble("A7sStepBegin", e); } } else A7sOff(); }   // f5-a7: srednia cen sygnalu, Held od zera przy pierwszym kroku sygnalu
            // f4-pomiary (klucz Measure Craft Rule): diagnoza A7 w dobie kroku i w dobie D - sam odczyt, przed petla reguly (niczego nie zmienia)
            if (d0 >= 0 && (step || today == d0) && F4A7On)
            {
                long f4t = sig ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                try { F4A7Diag(today, d0, rule, step); } catch (Exception e) { Stumble("F4A7Diag", e); }
                if (sig) a7t0 += System.Diagnostics.Stopwatch.GetTimestamp() - f4t;   // f5-a7: koszt reguly bez diagnozy f4 (ta ma wlasny czas w swojej linii)
            }
            bool f4reg = reg && F4RegOn;                                     // f4-pomiary (klucz Measure Regale Shelf): regale wobec polki - tylko liczniki
            if (f4reg) F4RegBegin();
            // f5-ruda (klucz Ks Iron Hands Follow Market): rece przy zlozach zelaza co 7 dob od D za ruda rynku - przed wczesnym wyjsciem K4a (wlasny try)
            if (F5IronDay(today, d0))
            {
                long irt = sig ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                try { F5IronWeek(today, d0); } catch (Exception e) { Stumble("F5IronWeek", e); }
                if (sig) a7t0 += System.Diagnostics.Stopwatch.GetTimestamp() - irt;   // recenzja calosci fali 5: czas reguly A7 (f5-a7) bez kroku zelaza f5-ruda (jak bez diagnozy f4)
            }
            // f20-d4 (klucz Ks Food Hands Follow Market): po D+49 RegaleCycle nie rusza soli i daktyli (jedna regula) i co 7 dob rece zajec jedzenia i soli za polkami rynku -
            // przed wczesnym wyjsciem K4a (wlasny try; doba kroku jest doba kroku A7, wiec K4a i tak nie wychodzi)
            _f20RegSkip = F20Active(today, d0);
            if (F20Day(today, d0))
            {
                long frt = sig ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                try { F20Week(today, d0); } catch (Exception e) { Stumble("F20Week", e); }
                if (sig) a7t0 += System.Diagnostics.Stopwatch.GetTimestamp() - frt;   // czas reguly A7 (f5-a7) bez kroku jedzenia f20-d4 (ma wlasny czas w swojej linii)
            }
            double sigma = Math.Max(1, DailyLife.KsSigma);
            int active = 0;
            // ks-koszt T2 (klucz Ks Cost Fast): wartosc towarow zajecia przy indeksach miasta (suma w Income) raz na miasto i zajecie w dobie kroku - ta sama petla, ta sama kolejnosc
            var vTab = step && SwCostFast ? new Dictionary<double[], double[]>() : null;
            bool f5g = step && SwCostFast2;                                  // f5-koszt: dochody G z diagnozy A7 tej doby (pamiec w bloku)
            // ks-koszt T10 (K4a, klucz Ks Cost Fast): doba bez kroku A7 i bez cyklu regali, a ostatni pelny przeglad tego stanu blokow nie mial zadnej przebudowy w toku -
            // petla nic by nie zrobila (przebudowy zaczynaja sie tylko w dobie kroku; Target zmienia tylko ta funkcja, a odczyt zapisu daje nowa mape okregow) - bez petli
            if (!step && !reg && SwCostFast && _k4Clean && ReferenceEquals(_k4Live, _live)) { _rActive = 0; return; }
            foreach (var ld in _live.Values)
            {
                if (ld.St == null || ld.K == null || ld.B == null) continue;
                if (LandBackOf(ld.St)) continue;                         // ks-arm: kraina wrocona przez bramke krainy - bez reguly zmiany zajec i regali
                try
                {
                    Town mt = ld.TF ? ld.T : (ld.V.TradeBound != null ? ld.V.TradeBound.Town : (ld.V.Bound != null ? ld.V.Bound.Town : null));
                    double[] px;
                    if (mt == null || !_px.TryGetValue(mt, out px)) continue;
                    double[] vRow = null;
                    if (vTab != null && !vTab.TryGetValue(px, out vRow)) { vRow = new double[KsTab.NC]; for (int c0 = 0; c0 < vRow.Length; c0++) vRow[c0] = double.NaN; vTab[px] = vRow; }
                    double[] sRow = sig && step ? A7sRow(mt) : null, sV = sRow != null ? A7sVRow(sRow) : null;   // f5-a7: cena sygnalu miasta targowego (srednia 4 krokow)
                    string type = ld.K.Type;
                    bool busy = false;
                    foreach (var b in ld.B) if (b != null && b.Target >= 0) { busy = true; break; }
                    string region = ld.TF ? ld.St.StringId : (ld.V.Bound != null ? ld.V.Bound.StringId : ld.St.StringId);
                    foreach (var b in ld.B)
                    {
                        if (b == null || b.K == null || b.C == null) continue;
                        if (b.Target >= 0) { Progress(ld, b, today, mt); active++; continue; }
                        // f5-a7 (klucz Ks Switch Signal): doba bez kroku i bez regali przy przebudowach w toku - blok bez Held nie ma tu nic do zrobienia (blokada prac zeruje
                        // tylko Held > 0); wynik ten sam, bez sprawdzania blokady prac dla ~15 tys. blokow co dobe
                        if (sig && !step && !reg && b.Held <= 0) continue;
                        if (reg && b.RC != null) RegaleCycle(b, px, f4reg ? mt : null, f4reg);
                        if (KsWorksLock(ld, b)) { b.Held = 0; continue; }   // inwest: czesc z pracami okregu nie zmienia zajecia (projekt A7)
                        if (!step || b.Cand == null || b.Cand.Count < 2) continue;
                        int ci = 0; for (int i = 1; i < b.X.Length; i++) if (b.X[i] > b.X[ci]) ci = i;
                        int cur = b.C[ci];
                        double incCur, incBest = double.NegativeInfinity; int best = -1;
                        if (sig)
                        {
                            // f5-a7 (klucz Ks Switch Signal): dochod rodziny przy cenie sygnalu KS (wzor Planu); miasto bez sygnalu - blok bez oceny (Held bez zmian)
                            if (sRow == null) { _a7sNoSig++; continue; }
                            double rent = A7sRent(b);
                            incCur = A7sIncome(b, type, cur, sRow, sV, rent);
                            foreach (int c in b.Cand) { if (c == cur) continue; double v = A7sIncome(b, type, c, sRow, sV, rent); if (v > incBest) { incBest = v; best = c; } }
                            A7sNoteEval(b, ci);
                        }
                        // f5-koszt (klucz Ks Cost Fast2; scalenie fali 5: tylko bez sygnalu KS f5-a7 - sygnal liczony na watku glownym w regule): te same dochody przy cenach gry policzyla dzis diagnoza A7 (ten sam wiersz cen miasta, to samo zajecie biezace, ta sama
                        // lista kandydatow i kolejnosc) - bez liczenia drugi raz; probka 1/64 sprawdza je starym rachunkiem (linia "KS: koszt f5")
                        // (recenzja: po roznicy w probce F5Fallback - od nastepnego bloku do konca sesji starym rachunkiem; blok z roznica bierze liczby starego rachunku)
                        else if (f5g && !F5Off && b.F5Day == today && b.F5Cur == cur && ReferenceEquals(b.F5Px, px)) { incCur = b.F5Ic; incBest = b.F5Ib; best = b.F5Best; F5RuleNote(b, type, cur, px, vRow, today, ref incCur, ref incBest, ref best); }
                        else
                        {
                            incCur = vRow != null ? IncomeV(b, type, cur, px, vRow) : Income(b, type, cur, px);
                            foreach (int c in b.Cand) { if (c == cur) continue; double v = vRow != null ? IncomeV(b, type, c, px, vRow) : Income(b, type, c, px); if (v > incBest) { incBest = v; best = c; } }
                        }
                        if (best < 0 || incBest < SwitchRatio * Math.Max(0.02, incCur)) { b.Held = 0; continue; }
                        b.Held += StepDays; _rPress++; if (b.Held > _rMaxHeld) _rMaxHeld = b.Held;
                        if (sig) A7sNotePress(b, ci);                    // f5-a7: rece pod naciskiem i trzymane >= 60 dob
                        if (b.Held < HoldDays) continue;
                        if (!rule) { _rWould++; continue; }                  // klucz wylaczony - sam pomiar nacisku (zmiana bylaby tu)
                        if (busy) { _rBusy++; continue; }
                        if (FeedKeeps(ld, cur, best)) { _rFeed++; continue; }   // r4-polnoc: najpierw wyzywic siebie - czesc przy jedzeniu nie idzie na zajecie tylko na targ
                        // ziemia bloku: po zmianie rak X0 z cur na best ziemia zajeta nie moze przekroczyc powierzchni bloku
                        double area = BlockArea(ld, b);
                        double after = b.LandUse + b.X[ci] * (b.LandOf(best, type) - b.LandOf(cur, type));
                        if (area > 0 && after > area * 1.02 && after > b.LandUse) { _rLand++; continue; }
                        double hands = b.Hands * b.X[ci];
                        double rh, moved;
                        _regionHands.TryGetValue(region ?? "-", out rh); _regionMoved.TryGetValue(region ?? "-", out moved);
                        if (moved + hands > RegionShare * rh) { _rBudget++; continue; }
                        int T = Tree(best) ? 364 : 60;
                        b.Cur = (sbyte)cur; b.Target = (sbyte)best; b.TStart = today; b.TEnd = today + T; b.X0 = b.X[ci]; b.H0 = (float)hands;
                        b.Cost = ld.TF ? 0f : (float)(CostDays * KsTab.KCost[best] * hands / sigma); b.Paid = 0f;
                        _regionMoved[region ?? "-"] = moved + hands;
                        if (IndexOfCraft(b, best) < 0)
                        {
                            var nc = new byte[b.C.Length + 1]; var nx = new float[b.X.Length + 1];
                            Array.Copy(b.C, nc, b.C.Length); Array.Copy(b.X, nx, b.X.Length); nc[b.C.Length] = (byte)best; nx[b.X.Length] = 0f;
                            b.C = nc; b.X = nx;
                        }
                        busy = true; _rStarted++; _rWinStarted++; active++;
                        if (sig) A7sNoteStart(ld, b, cur, best, hands, mt, T, today);   // f5-a7: sezon wedlug rynku i przejscia, powroty, komunikat gracza (jego wies)
                        if (_rStarted <= 12) _startNotes.Add((ld.St.Name != null ? ld.St.Name.ToString() : ld.Id) + " " + KsTab.Crafts[cur] + "->" + KsTab.Crafts[best] + " (" + Math.Round(hands) + " rak, " + T + " dob, " + Math.Round(b.Cost) + " zl)");
                    }
                }
                catch (Exception e) { Stumble("RuleDaily(okreg)", e); }
            }
            if (f4reg) { try { F4RegLine(today); } catch (Exception e) { Stumble("F4RegLine", e); } }
            _rActive = active;
            _k4Live = _live; _k4Clean = active == 0 && K4NoTransition();   // ks-koszt T10 (K4a): pelny przeglad bez przebudowy w toku (w zadnym bloku - takze okregow pominietych)
            if (sig)
            {
                A7sTime(a7t0);                                                // f5-a7: koszt reguly na dobe (doby z przebiegiem petli)
                if (step) { try { A7sStepLine(today, d0, rule); } catch (Exception e) { Stumble("A7sStepLine", e); } }
            }
        }
        private static bool _k4Clean; private static object _k4Live;

        /// <summary>ks-koszt T10 (K4a): zaden blok zadnego okregu nie jest w przebudowie (Target &lt; 0 wszedzie).</summary>
        private static bool K4NoTransition()
        {
            if (!SwCostFast) return false;
            foreach (var ld in _live.Values)
            {
                if (ld == null || ld.B == null) continue;
                foreach (var b in ld.B) if (b != null && b.Target >= 0) return false;
            }
            return true;
        }
        private static readonly List<string> _startNotes = new List<string>();

        /// <summary>Powierzchnia bloku (ha): ziemia okregu x udzial bloku (ostatni rachunek doby).</summary>
        private static double BlockArea(LiveDist ld, LiveBlock b) { return b.K != null ? b.K.Area : 0.0; }

        /// <summary>Doba przebudowy: rata (koszt do dzis wedlug czasu minus zaplacone, najwyzej 1/4 kiesy) - material z polki miasta targowego i rzemieslnicy
        /// (VillageCrafts.PayRebuild, recenzja calosci 17/18.10), rece z zajecia biezacego do nowego najwyzej X0/T na dobe i nie ponad czesc zaplacona; koniec - gdy stare
        /// zajecie nie ma juz rak. Ziemia bloku pelna (ludzi przybylo) - rece nie przechodza (recenzja 16.10).</summary>
        private static void Progress(LiveDist ld, LiveBlock b, int today, Town mt)
        {
            int ci = IndexOfCraft(b, b.Cur), ti = IndexOfCraft(b, b.Target);
            if (ci < 0 || ti < 0 || today > b.TEnd + 364) { b.Target = -1; b.Cur = -1; b.Held = 0; b.TStart = b.TEnd = -1; b.Cost = b.Paid = b.X0 = b.H0 = 0f; _rCancel++; _a7sCancel++; return; }   // rok po terminie bez zaplaty - przebudowa porzucona (rece zostaja tam, gdzie doszly); f5-a7: licznik linii
            string type = ld.K != null ? ld.K.Type : null;
            double area = BlockArea(ld, b);
            if (type != null && area > 0 && b.LandUse > area * 1.02 && b.LandOf(b.Target, type) > b.LandOf(b.Cur, type)) { _rLandStall++; return; }
            int T = Math.Max(1, b.TEnd - b.TStart);
            double progress = 1.0;
            if (b.Cost > 0 && !ld.TF && ld.V != null)
            {
                // recenzja calosci (noc 17/18.10): ta sama regula co zajecia wiosek bez KS (VillageCrafts.PayRebuild) - rata wedlug czasu (zaleglosci czekaja, material kupuje
                // sie na sztuki), najwyzej 1/4 kiesy, placona prawdziwym materialem z polki miasta targowego i rzemieslnikami miasta (Ds Works Real Material wylaczone - jak
                // dotad kiesa wsi -> kasa miasta); rece przechodza najwyzej X0/T na dobe i nigdy ponad czesc zaplacona (pierwsza doba z pelna zaplata - jak dotad)
                double fTime = Math.Min(1.0, Math.Max(0, today - b.TStart) / (double)T);
                double owe = Math.Min(b.Cost - b.Paid, fTime * b.Cost - b.Paid);
                // f5-a7 (klucz Ks Switch Signal): rata co 7 dob (material kupowany wiekszym zamowieniem, zakup z polki raz na tydzien na przebudowe - ok. 1/7 wywolan
                // PayRebuild na dobe wobec raty dziennej); rece ida dalej codziennie do czesci zaplaconej. Recenzja f5-a7: dzien raty przesuniety haszem bloku (A7sPaySpan) -
                // przebudowy zaczynaja sie tylko w dobie kroku A7, wiec bez przesuniecia wszystkie placilyby w dobie kroku; granica kiesy raty = tyle, ile rata dzienna
                // (1/4 kiesy na dobe) przez doby od poprzedniej raty (A7sPayCap) - tempo zaplaty jak przy racie dziennej. Klucz wylaczony - rata dzienna jak dotad.
                int ask = 0;
                if (owe >= 0.5)
                {
                    if (!A7sOn) ask = (int)Math.Min(Math.Ceiling(owe), Math.Max(0, ld.V.Gold) / 4);
                    else { int span = A7sPaySpan(ld, b, today); if (span > 0) ask = (int)Math.Min(Math.Ceiling(owe), A7sPayCap(ld.V.Gold, span)); }
                }
                long got = ask > 0 && mt != null ? (A7sOn ? A7sPay(ld.V, mt, ask) : VillageCrafts.PayRebuild(ld.V, mt, ask, _tally)) : 0;   // f5-a7: ta sama rata + pomiar (zloto, material, polka)
                b.Paid += got; _rPaid += got;
                double fPay = b.Paid >= b.Cost - 0.5 ? 1.0 : Math.Max(0.0, b.Paid / (double)b.Cost);
                double moved = b.X0 > 1e-6f ? Math.Max(0.0, Math.Min(1.0, 1.0 - b.X[ci] / (double)b.X0)) : 1.0;
                progress = Math.Max(0.0, Math.Min(1.0, (fPay - moved) * T));
                if (progress < 1.0 - 1e-6) _rStall++;
            }
            double dx = Math.Min(b.X[ci], b.X0 / T * progress);
            b.X[ci] -= (float)dx; b.X[ti] += (float)dx;
            if (b.X[ci] <= 1e-4f)
            {
                // koniec przebudowy: stare zajecie bez rak znika z bloku
                var nc = new List<byte>(); var nx = new List<float>();
                for (int i = 0; i < b.C.Length; i++) if (i != ci || b.X[i] > 1e-4f) { nc.Add(b.C[i]); nx.Add(b.X[i]); }
                double s = 0; foreach (var x in nx) s += x;
                for (int i = 0; i < nx.Count; i++) nx[i] = (float)(nx[i] / (s > 0 ? s : 1));
                b.C = nc.ToArray(); b.X = nx.ToArray();
                b.Target = -1; b.Cur = -1; b.Held = 0; b.TStart = b.TEnd = -1; b.Cost = b.Paid = b.X0 = b.H0 = 0f;
                _rDone++; _a7sDone++;                                     // f5-a7: licznik linii
            }
        }

        private static void RegaleCycle(LiveBlock b, double[] px, Town mt, bool f4)
        {
            double sum = 0;
            for (int j = 0; j < b.RC.Length; j++)
            {
                int g = KsTab.RegGood(b.RC[j]);
                double ix = Px(px, g);
                // f5-ruda (klucz Ks Iron Hands Follow Market): zelazo - krok co 7 dob za ruda rynku (StartCalibration.Ruda.cs); przed pomiarem f4 - linia "regale wobec polki"
                // i licznik regali linii produkcji licza te same wpisy (NF4-5b)
                if (F5IronSkip(b.RC[j])) { sum += b.RF[j]; continue; }
                // f20-d4 (klucz Ks Food Hands Follow Market): sol i daktyle po D+49 - krok co 7 dob za polkami rynku (StartCalibration.Jedzenie.cs); przed pomiarem f4 jak zelazo
                if (F20RegSkipOf(b.RC[j])) { sum += b.RF[j]; continue; }
                if (f4) { try { F4RegNote(b, j, g, ix, mt); } catch (Exception e) { Stumble("F4RegNote", e); } }   // f4-pomiary: polka wobec celu obok progu (tylko liczniki)
                if (ix > 1.2) { b.RF[j] *= 1.1f; _rUp++; }
                else if (ix < 0.8) { b.RF[j] *= 0.9f; _rDown++; }
                sum += b.RF[j];
            }
            double cap = 0.5 * Math.Max(0.0, 1.0 - b.HomeFrac);
            if (sum > cap && sum > 0) { float k = (float)(cap / sum); for (int j = 0; j < b.RF.Length; j++) b.RF[j] *= k; }
        }

        // kontrola pieniedzy KS od ostatniej linii: przebudowa (kiesy wsi -> kasy miast), pola mieszczan (kasy miast -> kiesy ludu)
        private static long _mTfTown, _mTfFolk;
        private static readonly VillageCrafts.RebuildTally _tally = new VillageCrafts.RebuildTally();   // recenzja calosci: rata przebudowy A7 (kiesy wsi -> kasa targu za material + lud)

        /// <summary>Linia co 7 dob: wedlug rynku (Westeros, Essos) mediana 28-dobowego indeksu cen miast dla towarow KS i odsetek miast na suficie / podlodze wzoru ceny
        /// w ostatnim odczycie (pomiar "ceny blisko startu" - progi nks NKS-7).</summary>
        private static string PriceLine(int day)
        {
            var sb = new StringBuilder(2600);
            sb.Append(PK).Append("ceny miast dzien ").Append(day).Append(" (mediana indeksu 28 dob, miast ").Append(_px.Count).Append(", dob sredniej ").Append(Math.Min(28, _pxDays)).Append("):");
            var vals = new List<double>(_px.Count);
            bool first = true;
            for (int i = 0; i < ItemCount; i++)
            {
                vals.Clear();
                foreach (var a in _px.Values) if (a != null && i < a.Length) vals.Add(a[i]);
                if (vals.Count == 0) continue;
                sb.Append(first ? " " : ", ").Append(ItemIds[i]).Append(' ').Append(Med(vals).ToString("0.00", Inv));
                first = false;
            }
            for (int m = 0; m < 2; m++)
            {
                sb.Append(m == 0 ? " | Westeros:" : " | Essos:");
                first = true;
                for (int i = 0; i < ItemCount; i++)
                {
                    vals.Clear(); int ceil = 0, floor = 0, n = 0;
                    foreach (var kv in _px)
                    {
                        if (kv.Value == null || TownMarket(kv.Key) != m) continue;
                        vals.Add(kv.Value[i]);
                        double[] now; if (_pxNow.TryGetValue(kv.Key, out now) && now != null) { n++; if (now[i] >= CeilIx) ceil++; else if (now[i] <= FloorIx) floor++; }
                    }
                    if (vals.Count == 0) continue;
                    sb.Append(first ? " " : ", ").Append(ItemIds[i]).Append(' ').Append(Med(vals).ToString("0.00", Inv)).Append(" (s").Append(n > 0 ? (100 * ceil / n).ToString(Inv) : "-")
                      .Append(" p").Append(n > 0 ? (100 * floor / n).ToString(Inv) : "-").Append(')');
                    first = false;
                }
            }
            sb.Append(" | (s/p - % miast na suficie / podlodze wzoru ceny w ostatnim odczycie).");
            return sb.ToString();
        }

        private static double Med(List<double> v)
        {
            v.Sort();
            return v.Count % 2 == 1 ? v[v.Count / 2] : 0.5 * (v[v.Count / 2 - 1] + v[v.Count / 2]);
        }

        /// <summary>Kontrola pieniedzy KS (od ostatniej linii): ile zeszlo z kies wsi i weszlo do kas miast przy przebudowie, ile zeszlo z kas miast i weszlo do kies ludu
        /// za plon pol mieszczan - roznice musza byc 0 (nic z niczego).</summary>
        private static string MoneyText()
        {
            // recenzja calosci (noc 17/18.10): "kasy miast" przebudowy = kasy targow (material) + lud miast (rzemieslnicy) - te same liczby co dotad przy Ds Works Real Material wylaczonym;
            // recenzja calosci fali 5: + kasy targow z placy rzemieslnikow (czesc nieprzyjeta przez lud wedlug reguly f5 - TownFolk.PayVillageWage)
            string t = "kontrola pieniedzy KS: przebudowa kiesy wsi -" + _tally.Vil + " / kasy miast +" + (_tally.Town + _tally.Folk + _tally.Cof) + " (roznica " + (_tally.Vil - _tally.Town - _tally.Folk - _tally.Cof) + "), pola mieszczan kasy miast -" + _mTfTown
                       + " / kiesy ludu +" + _mTfFolk + " (roznica " + (_mTfTown - _mTfFolk) + ")"
                       + (_tally.Vil > 0 || _tally.NoMat > 0 ? ", przebudowa: material z polki +" + _tally.Mat + " (szt. " + _tally.Pcs + "), rzemieslnicy +" + _tally.Wage + (_tally.Cof > 0 ? " (z tego do kas miast - lud nie przyjal, regula f5 " + _tally.Cof + ")" : "") + ", bez materialu " + _tally.NoMat + " razy, droga dawna bez towaru +" + _tally.Old : "");
            _tally.Clear(); _mTfTown = _mTfFolk = 0;
            return t;
        }

        /// <summary>Dopisek linii produkcji: nacisk na zmiane, przebudowy, regale (liczniki od ostatniej linii).</summary>
        private static string RuleText()
        {
            var s = Settings.Current;
            bool rule = A7RuleOn;                                       // f5-a7: dawny klucz albo sygnal KS z kluczem Ks Switch Signal Rule
            var sb = new StringBuilder(800);
            sb.Append(F4FrozenText(rule));                              // f4-pomiary (klucz Measure Craft Rule): jawny stan "zajecia zamrozone" (pusty napis przy kluczu wylaczonym)
            sb.Append("nacisk A7 na zmiane (co ").Append(StepDays).Append(" dob od D ").Append(DDay).Append(", inne zajecie >= x").Append(SwitchRatio.ToString("0.00", Inv)).Append("): przegladow ponad progiem ").Append(_rPress)
              .Append(", najdluzej ").Append(_rMaxHeld).Append(" dob, po ").Append(HoldDays).Append(" dobach (zmiana bylaby, klucz ").Append(rule ? "TAK" : "WYLACZONY - sam pomiar").Append(") ").Append(_rWould)
              .Append(" | zmiany zajec: zaczete ").Append(_rStarted).Append(" (od D razem ").Append(_rWinStarted).Append("), w toku ").Append(_rActive).Append(", skonczone ").Append(_rDone)
              .Append(", wstrzymane (inna czesc okregu w przebudowie) ").Append(_rBusy).Append(", (5% rak regionu w sezonie) ").Append(_rBudget).Append(", (ziemia bloku) ").Append(_rLand)
              .Append(", przejscia stojace na ziemi ").Append(_rLandStall)
              .Append(", dob bez pelnej zaplaty ").Append(_rStall).Append(", porzucone (rok po terminie) ").Append(_rCancel).Append(", zaplacone z kies wsi do kas miast ").Append(_rPaid).Append(" zl")
              .Append(_startNotes.Count > 0 ? " (" + string.Join("; ", _startNotes.ToArray()) + ")" : "")
              .Append(" | regale (co ").Append(RegCycle).Append(" dob od D): rece przy zlozu w gore ").Append(_rUp).Append(", w dol ").Append(_rDown)
              .Append(FeedRuleText());                                  // r4-polnoc
            _rPress = _rMaxHeld = _rStarted = _rDone = _rStall = _rBudget = _rBusy = _rUp = _rDown = _rCancel = _rWould = _rLand = _rLandStall = 0; _rPaid = 0; _startNotes.Clear();
            return sb.ToString();
        }

        // ============================================================ pamiec reguly w zapisie (recenzja 16.10): rece regionu w sezonie i srednie cen miast
        // sezon~region:rece,...~zmiany od D~miasto:n:indeksy (2 znaki base36 na towar, indeks x 100, 0..12.95)!miasto...
        private static void RuleExport(StringBuilder sb)
        {
            sb.Append(_regionSeason).Append('~');
            bool f = true;
            foreach (var kv in _regionMoved) { if (!f) sb.Append(','); f = false; sb.Append(Clean(kv.Key)).Append(':').Append(Math.Round(kv.Value).ToString("0", Inv)); }
            sb.Append('~').Append(_rWinStarted).Append('~');
            f = true;
            foreach (var kv in _px)
            {
                if (kv.Key == null || kv.Key.Settlement == null || kv.Value == null) continue;
                int n; _pxN.TryGetValue(kv.Key, out n);
                if (!f) sb.Append('!'); f = false;
                sb.Append(Clean(kv.Key.Settlement.StringId)).Append(':').Append(n).Append(':');
                for (int i = 0; i < kv.Value.Length; i++) sb.Append(E36(Math.Max(0, Math.Min(1295, (int)Math.Round(kv.Value[i] * 100))), 2));
            }
        }

        /// <summary>Odczyt pamieci reguly z zapisu ks3; zwraca opis do linii startu. Srednie cen trafiaja do miast przy pierwszym odczycie cen sesji.</summary>
        private static string RuleImport(string t)
        {
            try
            {
                if (string.IsNullOrEmpty(t)) return "pusta";
                var p = t.Split('~');
                if (p.Length < 4) return "nieznany format - od zera";
                int season; if (int.TryParse(p[0], NumberStyles.Integer, Inv, out season)) _regionSeason = season;
                _regionMoved.Clear();
                if (p[1].Length > 0)
                    foreach (var e in p[1].Split(','))
                    {
                        int c = e.LastIndexOf(':'); double v;
                        if (c > 0 && double.TryParse(e.Substring(c + 1), NumberStyles.Float, Inv, out v)) _regionMoved[e.Substring(0, c)] = v;
                    }
                int.TryParse(p[2], NumberStyles.Integer, Inv, out _rWinStarted);
                var load = new Dictionary<string, KeyValuePair<int, double[]>>(StringComparer.Ordinal);
                if (p[3].Length > 0)
                    foreach (var e in p[3].Split('!'))
                    {
                        var q = e.Split(':');
                        int n;
                        if (q.Length != 3 || !int.TryParse(q[1], NumberStyles.Integer, Inv, out n) || q[2].Length != 2 * ItemCount) continue;
                        var a = new double[ItemCount]; bool ok = true;
                        for (int i = 0; i < ItemCount && ok; i++) { int v = D36(q[2], 2 * i, 2); if (v < 0) ok = false; else a[i] = Math.Max(0.01, v / 100.0); }
                        if (ok) load[q[0]] = new KeyValuePair<int, double[]>(n, a);
                    }
                _pxLoad = load.Count > 0 ? load : null;
                return "sezon " + _regionSeason + ", regionow z przeniesionymi rekami " + _regionMoved.Count + ", srednie cen " + load.Count + " miast, zmian od D " + _rWinStarted;
            }
            catch (Exception e) { Stumble("RuleImport", e); return "blad odczytu - od zera"; }
        }
    }
}
