using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// 435-L1 - LANCUCH PODAZY (LP), krok 1: SAM POMIAR. Zadna liczba gry sie nie zmienia - same liczniki i linie logu. Klucz Supply Chain Log (NIE = zadnej linii, liczniki
    /// nie licza - kod co do znaku jak przed 435-L1). Projekt: PROJEKT-LANCUCH-po-krytyce (11.11), rozdz. II.7, II.8, II.4, II.19 wiersz L1. Polecenie Jeffa: warsztaty
    /// i produkcja maja isc za zapotrzebowaniem swiata, ludzie za praca, a inwestycja + ludzie = 100% efektu - ten krok tylko mierzy, zanim cokolwiek zacznie sterowac.
    ///
    /// Co mierzy:
    ///  1. BRAK MATERIALU W SZTUKACH wedlug towaru i miasta targowego (dotad tylko zloto): budzet materialu zlecenia robot gminy / septu (TownFolk.DaRunOne) i raty
    ///     przebudowy zajecia wioski (TownFolk.CraftsRebuildPay), ktorego nie pokryla polka targu ani wytworcy 363, przeliczony na sztuki po cenie targu - rowno miedzy
    ///     materialy budow (reszta budzetu oznacza, ze wszystkie sa wyczerpane do zapasu albo ponad sufitem ceny; reszta mniejsza od najtanszej sztuki - pominieta);
    ///     wsad warsztatow towarowych, ktorego zabraklo do cyklu (CommodityInputs284.SufPost: potrzeba - stan polki). Obok sztuki KUPIONE tymi samymi drogami.
    ///     Zlecenie robot ponawia sie co dobe (ta sama potrzeba), dlatego pamiec to POZIOM dobowy (srednia 28 dob), nie suma (krytyka B8).
    ///  2. TABLICA N (towar x miasto targowe): N = max(1, S_stos, S_kolejka), najwyzej 2. Cena NIE wchodzi do N. Od 435-L5 czyta ja kolejnosc zlecen sprzetu wsi,
    ///     od 435-L6 (klucze domyslnie wylaczone) regula zmiany zajec i pulap zamowien 363 - przez opublikowana tablice (Table / TownN), CraftShortage.cs.
    ///     S_stos = 1.25 ^ (liczba kolejnych decyzji regulatora wsi 221 z mnoznikiem na suficie kroku i stosem ponizej dolnego progu), najwyzej 2;
    ///     S_kolejka = 1 + min(1, brak / sprzedaz), srednie dobowe 28 dob w sztukach; zboze: N = 2 w miescie, ktorego wsie gloduja (Famine.HungerOf &gt; 0).
    ///     ODSTEPSTWO od projektu (do decyzji przed L6): "sprzedaz" = wieksza z dwoch liczb - prawdziwa sprzedaz miasta z 28 dob (MarketSales 241; tylko kategorie bedace
    ///     wyrobem warsztatu) i sztuki kupione przez te same trzy strumienie popytu. Licznika calej sprzedazy miasta dla surowcow wsi (drewno, glina, ruda) nie ma.
    ///  3. WYKORZYSTANIE WARSZTATOW TOWAROWYCH miast (linie towarowe, bez ukrytych rzemieslnikow BK i linii uzbrojenia): cykle udane / (udane + nieudane), z powodem
    ///     cyklu bez wyrobu - wstrzymany regula przed sprawdzeniem wsadu (bramka narzedzi 215, kopalnia co N-ty cykl, strzelarze), brak wsadu na polce, prog ceny
    ///     (WorkshopTrade.LineCycleAt), kasa miasta ponizej utargu, kapital warsztatu ponizej wsadu. Gra po pierwszym nieudanym cyklu linii nie probuje nastepnych tej
    ///     doby, wiec u jest GORNA granica. Pamiec 28 dob na warsztat (sesja).
    ///  4. WYCENA NA SUCHO bilansu otwarcia sprzetu (projekt II.4) i tabeli II.2 - co 28 dob i w pierwszej dobie sesji: K0, wartosc, material, ile materialu na 1% rak,
    ///     ile produkcji dnia stoi za sprzetem (1 - phi). Liczby tabeli (phi, dni, zuzycie) sa tu TYLKO do wyceny - nic ich nie czyta.
    ///  5. ILE OKREGOW I CZESCI ZMIENIA RECE (ludzi w domu) o ponad 1% na dobe - koszt i szarpanie przyszlego czynnika sprzetu m (II.18).
    ///  6. DLACZEGO PRACE GRACZA NIE PLACA - co 7 dob (DistrictWorks.Lp1Text, powody zbierane przy wozach).
    /// Od 435-L8 wynik cyklu (pkt 3) czyta tez pamiec stanowisk warsztatow (WorkshopBenches.Observe; klucz Workshop Benches domyslnie wylaczony).
    /// Powody postoju przebudow zajec sa juz w linii ZW / A7 (427, EcoAudit427.RbText) - tu nie powtarzane.
    /// Stan tylko sesyjny (bez zapisu): po wczytaniu srednie licza sie od pierwszej doby (N = 1 dopoki nie ma danych); pamiec N w zapisie przyjdzie z arm_lp (L3).
    /// Watek glowny; wyjatek = potkniecie liczone w linii, bez globalnego wylacznika (CLAUDE.md 7).
    /// </summary>
    internal static class SupplySignal
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int Window = 28;
        internal const int SWorks = 0, SSept = 1, SRebuild = 2, SShop = 3, Streams = 4;
        private static readonly string[] SName = { "roboty gminy", "septy", "przebudowy zajec", "wsad warsztatow" };

        internal static bool On { get { var s = Settings.Current; return s != null && s.SupplyChainLog; } }

        private static int _main = -1, _dOff;
        private static int _st, _stSess;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _startLine;
        private static int _first = int.MinValue;         // pierwsza doba pomiaru w sesji (dzielnik srednich)

        private static void Stumble(string where, Exception e)
        {
            _st++; _stSess++;
            try { if (_err.Add(where)) Log.Error("SupplySignal." + where, e); } catch { }
        }

        private static bool OffMain()
        {
            if (_main < 0 || Environment.CurrentManagedThreadId == _main) return false;
            _dOff++;
            return true;
        }

        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static string F1(double v) { return v.ToString("0.0", Inv); }
        private static string F2(double v) { return v.ToString("0.00", Inv); }
        private static string N0(double v) { return Math.Round(v).ToString("0", Inv); }

        // ============================================================ 1. brak i kupione w sztukach (towar x miasto targowe)
        private sealed class Cell
        {
            public readonly int[] Day = new int[Window];
            public readonly float[] Miss = new float[Window], Got = new float[Window];
            public Cell() { for (int i = 0; i < Window; i++) Day[i] = int.MinValue; }
        }
        private static readonly Dictionary<Settlement, Dictionary<ItemCategory, Cell>> _cells = new Dictionary<Settlement, Dictionary<ItemCategory, Cell>>();
        private static readonly double[] _dMiss = new double[Streams], _dGot = new double[Streams], _dGapGold = new double[Streams];
        private static readonly Dictionary<ItemCategory, double[]> _dCat = new Dictionary<ItemCategory, double[]>();   // doba: [brak, kupione]
        private static double _dRound;                    // reszty budzetu ponizej ceny najtanszej sztuki (nie sa brakiem)
        private static readonly Dictionary<Town, Dictionary<ItemObject, int>> _price = new Dictionary<Town, Dictionary<ItemObject, int>>();   // cena targu, pamiec doby

        private static void Add(Settlement st, ItemCategory c, double miss, double got, int stream)
        {
            if (st == null || c == null || (!(miss > 0) && !(got > 0))) return;
            Dictionary<ItemCategory, Cell> m;
            if (!_cells.TryGetValue(st, out m)) { m = new Dictionary<ItemCategory, Cell>(); _cells[st] = m; }
            Cell cell;
            if (!m.TryGetValue(c, out cell)) { cell = new Cell(); m[c] = cell; }
            int d = Today, k = ((d % Window) + Window) % Window;
            if (cell.Day[k] != d) { cell.Day[k] = d; cell.Miss[k] = 0f; cell.Got[k] = 0f; }
            if (_first == int.MinValue) _first = d;
            double[] dc;
            if (!_dCat.TryGetValue(c, out dc)) { dc = new double[2]; _dCat[c] = dc; }
            if (miss > 0) { cell.Miss[k] += (float)miss; _dMiss[stream] += miss; dc[0] += miss; }
            if (got > 0) { cell.Got[k] += (float)got; _dGot[stream] += got; dc[1] += got; }
        }

        private static int PriceOf(Town town, ItemObject it)
        {
            Dictionary<ItemObject, int> m;
            if (!_price.TryGetValue(town, out m)) { m = new Dictionary<ItemObject, int>(); _price[town] = m; }
            int p;
            if (m.TryGetValue(it, out p)) return p;
            p = Math.Max(1, town.GetItemPrice(it, null, false));
            m[it] = p;
            return p;
        }

        /// <summary>TownFolk (roboty gminy, septy, przebudowa zajec): sztuki materialu zdjete z polki targu `mk` wedlug materialow budow (`pcs` w kolejnosci `items`). Sam licznik, nie rzuca.</summary>
        internal static void NoteWorksGot(Settlement mk, ItemObject[] items, int[] pcs, int stream)
        {
            if (!On || mk == null || items == null || pcs == null || stream < 0 || stream >= Streams) return;
            try
            {
                if (OffMain()) return;
                for (int i = 0; i < pcs.Length && i < items.Length; i++)
                    if (pcs[i] > 0 && items[i] != null) Add(mk, items[i].ItemCategory, 0, pcs[i], stream);
            }
            catch (Exception e) { Stumble("NoteWorksGot", e); }
        }

        /// <summary>
        /// TownFolk: `gapGold` - budzet materialu zlecenia, ktorego nie pokryla polka targu `mk` (ani wytworcy 363). Przeliczony na sztuki po cenie targu, rowno miedzy
        /// materialy budow (reszta oznacza, ze kazdy jest wyczerpany do zapasu albo ponad sufitem ceny). Reszta mniejsza od najtanszej sztuki to zaokraglenie, nie brak.
        /// Sam licznik (odczyt cen targu z pamiecia doby), nie rzuca.
        /// </summary>
        internal static void NoteWorksGap(Settlement mk, double gapGold, ItemObject[] items, int stream)
        {
            if (!On || mk == null || mk.Town == null || items == null || !(gapGold >= 1) || stream < 0 || stream >= Streams) return;
            try
            {
                if (OffMain()) return;
                int n = 0, minP = int.MaxValue;
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] == null) continue;
                    int p = PriceOf(mk.Town, items[i]);
                    n++; if (p < minP) minP = p;
                }
                if (n == 0) return;
                if (gapGold < minP) { _dRound += gapGold; return; }
                double each = gapGold / n;
                for (int i = 0; i < items.Length; i++)
                    if (items[i] != null) Add(mk, items[i].ItemCategory, each / PriceOf(mk.Town, items[i]), 0, stream);
                _dGapGold[stream] += gapGold;
            }
            catch (Exception e) { Stumble("NoteWorksGap", e); }
        }

        /// <summary>CommodityInputs284: wsad linii towarowej warsztatu w miescie - `miss` sztuk zabraklo do cyklu, `got` sztuk zdjetych z polki w udanym cyklu. Sam licznik, nie rzuca.</summary>
        internal static void NoteInput(Town town, ItemCategory c, int miss, int got)
        {
            if (!On || town == null || c == null) return;
            try
            {
                if (OffMain()) return;
                Add(town.Settlement, c, Math.Max(0, miss), Math.Max(0, got), SShop);
            }
            catch (Exception e) { Stumble("NoteInput", e); }
        }

        // ============================================================ 3. wykorzystanie warsztatow towarowych
        private const int ROk = 0, RHeld = 1, RInput = 2, RPrice = 3, RCoffer = 4, RCapital = 5, ROther = 6, RKinds = 7;
        private static readonly string[] RName =
        {
            "udane", "wstrzymane regula przed sprawdzeniem wsadu (bramka narzedzi 215, kopalnia co N-ty cykl, strzelarze, inne prefiksy)", "brak wsadu na polce",
            "prog ceny (utarg wedlug ceny kupca nie pokrywa wsadu i plac)", "kasa miasta ponizej utargu", "kapital warsztatu ponizej wsadu", "inne (po wsadzie, bez wyceny)"
        };
        private sealed class WRec
        {
            public readonly int[] Day = new int[Window];
            public readonly ushort[] Done = new ushort[Window], Fail = new ushort[Window];
            public WRec() { for (int i = 0; i < Window; i++) Day[i] = int.MinValue; }
        }
        private static readonly Dictionary<Workshop, WRec> _ws = new Dictionary<Workshop, WRec>();
        private static readonly int[] _wDay = new int[RKinds];
        private static readonly Dictionary<Workshop, int[]> _wLine = new Dictionary<Workshop, int[]>();   // od poprzedniej linii: [udane, nieudane, ostatni powod]
        private static int _wArms, _wCastle, _wPlayer;
        private static bool _cySuf, _cySufOk, _cyInc; private static int _cyIncome; private static Workshop _cyIncShop;

        /// <summary>CommodityInputs284.CyclePre (Priority.First - przed kazdym cyklem warsztatu): znaczniki cyklu od zera.</summary>
        internal static void CycleBegin() { _cySuf = false; _cySufOk = false; _cyInc = false; _cyIncShop = null; _cyIncome = 0; }

        /// <summary>CommodityInputs284.SufPost: gra sprawdzila wsad cyklu (`ok` - jest; false - brak na polce miasta).</summary>
        internal static void SufSeen(bool ok) { _cySuf = true; _cySufOk = ok; }

        /// <summary>WorkshopTrade.ItemsGatePost: utarg progu cyklu (gra liczy go dopiero po udanym sprawdzeniu wsadu). Sam odczyt.</summary>
        internal static void IncomeSeen(Workshop w, int income) { _cyInc = true; _cyIncShop = w; _cyIncome = income; }

        /// <summary>WorkshopTrade.CyclePostfix (cykl warsztatu notabla i gracza, takze pominiety przez prefiks): wynik cyklu linii towarowej i powod braku wyrobu. Sam licznik, nie rzuca.</summary>
        internal static void CycleEnd(WorkshopType.Production p, Workshop w, bool effectCapital, bool result)
        {
            if (!On) return;
            try
            {
                if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden || w.Settlement == null) return;
                var town = w.Settlement.Town;
                if (town == null || OffMain()) return;
                if (!effectCapital) { _wArms++; return; }                 // linia uzbrojenia - WorkshopLaw (linia "Warsztaty: dzien")
                if (!w.Settlement.IsTown) { _wCastle++; return; }
                int reason;
                if (result) reason = ROk;
                else if (!_cySuf) reason = RHeld;
                else if (!_cySufOk) reason = RInput;
                else if (p.Inputs == null || p.Inputs.Count == 0) reason = ROther;   // linia bez wsadu: LineCycleAt jej nie wycenia
                else
                {
                    float net, inC;
                    bool line = WorkshopTrade.LineCycleAt(town, w.WorkshopType, p, out net, out inC);
                    if (!line) reason = RPrice;
                    else if (_cyInc && ReferenceEquals(_cyIncShop, w) && town.Gold < _cyIncome) reason = RCoffer;
                    else if (w.Capital < inC) reason = RCapital;
                    else reason = ROther;
                }
                WRec rec;
                if (!_ws.TryGetValue(w, out rec)) { rec = new WRec(); _ws[w] = rec; }
                int d = Today, k = ((d % Window) + Window) % Window;
                if (rec.Day[k] != d) { rec.Day[k] = d; rec.Done[k] = 0; rec.Fail[k] = 0; }
                if (_first == int.MinValue) _first = d;
                if (reason == ROk) { if (rec.Done[k] < ushort.MaxValue) rec.Done[k]++; }
                else { if (rec.Fail[k] < ushort.MaxValue) rec.Fail[k]++; }
                _wDay[reason]++;
                int[] ln;
                if (!_wLine.TryGetValue(w, out ln)) { ln = new int[3]; _wLine[w] = ln; }
                if (reason == ROk) ln[0]++; else { ln[1]++; ln[2] = reason; }
                if (w.Owner == Hero.MainHero) _wPlayer++;
                WorkshopBenches.Observe(w, reason == ROk);   // 435-L8: wykorzystanie warsztatu do pamieci stanowisk (wlasny try; klucz wylaczony - nic)
            }
            catch (Exception e) { Stumble("CycleEnd", e); }
        }

        // ============================================================ 2. S_stos (regulator wsi 221)
        private static readonly Dictionary<ItemObject, int> _runs = new Dictionary<ItemObject, int>();   // kolejne decyzje na suficie przy stosie ponizej progu (sesja)
        private static int _seenDec = -1;
        // 435-L5: N swiata z ostatniej linii niedoboru (tylko towary z N > 1); jedna podmieniana referencja - odczyt kolejnosci zlecen sprzetu wsi (VillageGear.OrderOf)
        private static volatile Dictionary<ItemCategory, double> _pubN = new Dictionary<ItemCategory, double>();
        /// <summary>435-L5: niedobor N towaru w swiecie z ostatniej doby pomiaru (1 - bez niedoboru, bez pomiaru albo klucz Supply Chain Log wylaczony). Sam odczyt.</summary>
        internal static double WorldN(ItemCategory c)
        {
            if (c == null) return 1.0;
            var m = _pubN; double n;
            return m.Count > 0 && m.TryGetValue(c, out n) && n > 1.0 && On ? Math.Min(2.0, n) : 1.0;
        }

        // 435-L6: tablica N (towar x miasto targowe) z ostatniej linii niedoboru - jedna podmieniana, nigdy nie zmieniana po publikacji referencja (odczyt z reguly
        // zmiany zajec obu drog i z zamowien 363; bezpieczna dla kazdego watku). Cell - tylko komorki z N > 1; Stock - S_stos swiata (towar na suficie regulatora);
        // Hungry - miasta targowe, ktorych wsie gloduja (zboze N = 2).
        internal sealed class NTable
        {
            public readonly Dictionary<Settlement, Dictionary<ItemCategory, double>> Cell = new Dictionary<Settlement, Dictionary<ItemCategory, double>>();
            public readonly Dictionary<ItemCategory, double> Stock = new Dictionary<ItemCategory, double>();
            public readonly HashSet<Settlement> Hungry = new HashSet<Settlement>();
            public ItemCategory Grain; public int Day;
        }
        private static volatile NTable _pubT;
        /// <summary>435-L6: tablica N z ostatniej doby pomiaru; null - bez pomiaru w tej sesji albo klucz Supply Chain Log wylaczony (wtedy N = 1 wszedzie).</summary>
        internal static NTable Table { get { return On ? _pubT : null; } }
        /// <summary>435-L6: niedobor N towaru `c` w miescie targowym `mk` wedlug tablicy `t` (1 - bez niedoboru; najwyzej 2). Sam odczyt, nie rzuca.</summary>
        internal static double TownN(NTable t, Settlement mk, ItemCategory c)
        {
            if (t == null || c == null) return 1.0;
            double n = 1.0, x;
            if (t.Stock.TryGetValue(c, out x) && x > n) n = x;
            if (mk != null)
            {
                Dictionary<ItemCategory, double> m;
                if (t.Cell.TryGetValue(mk, out m) && m.TryGetValue(c, out x) && x > n) n = x;
                if (t.Grain != null && c == t.Grain && t.Hungry.Contains(mk)) n = 2.0;
            }
            return n > 2.0 ? 2.0 : n;
        }

        // ============================================================ 5. rece okregow (zmiana doby)
        private static readonly Dictionary<string, double> _prevHome = new Dictionary<string, double>(StringComparer.Ordinal);
        private static int _prevHomeDay = int.MinValue;

        // ============================================================ 4. tabela II.2 projektu (TYLKO do wyceny na sucho)
        private const int GPits = 0, GTrees = 1, GBoats = 2, GWoodD = 3, GWoodM = 4, GGrain = 5, GFields = 6, GOut = 7, GN = 8;
        private static readonly string[] GName = { "SZYBY", "SADY", "LODZIE", "LASY-D", "LASY-M", "POLA-Z", "POLA", "poza LP (stado, stadniny)" };
        // 435-L3: jedna tabela - liczby z VillageGear (od L3 czyta je czynnik sprzetu; wycena na sucho liczy z tych samych)
        private static readonly double[] GPhi = VillageGear.GPhi;
        private static readonly double[] GDays = VillageGear.GDays;
        private static readonly double[] GWear = VillageGear.GWear;
        private static int _dryDay = int.MinValue, _whyDay = int.MinValue;

        private static int GroupOf(string craft)
        {
            switch (craft)
            {
                case "iron_mine": case "silver_mine": case "gold_mine": case "salt_mine": case "clay_mine": case "quarry": case "lime_pit": case "sea_salt": return GPits;
                case "vineyard": case "olive_trees": case "date_farm": case "citrus": return GTrees;
                case "fisherman": case "whaler": case "walrus_hunter": return GBoats;
                case "lumberjack": return GWoodD;
                case "trapper": case "hunter": return GWoodM;
                case "wheat_farm": return GGrain;
                case "flax_plant": case "silk_plant": case "garden": case "spice_garden": return GFields;
                default: return GOut;
            }
        }

        /// <summary>Nowa kampania / przed wczytaniem: liczniki i pamiec sesji od zera.</summary>
        internal static void Reset()
        {
            _main = Environment.CurrentManagedThreadId; _dOff = 0;
            _st = _stSess = 0; _err.Clear(); _startLine = false; _first = int.MinValue;
            _cells.Clear(); _price.Clear(); ZeroDay();
            _ws.Clear(); CycleBegin();
            _runs.Clear(); _seenDec = -1; _pubN = new Dictionary<ItemCategory, double>(); _pubT = null;
            _prevHome.Clear(); _prevHomeDay = int.MinValue;
            _dryDay = int.MinValue; _whyDay = int.MinValue;
        }

        private static void ZeroDay()
        {
            Array.Clear(_dMiss, 0, Streams); Array.Clear(_dGot, 0, Streams); Array.Clear(_dGapGold, 0, Streams);
            _dCat.Clear(); _dRound = 0;
            Array.Clear(_wDay, 0, RKinds); _wArms = _wCastle = _wPlayer = 0; _wLine.Clear();
            _dOff = 0; _st = 0;
        }

        private static int Span(int today) { return _first == int.MinValue ? 1 : Math.Max(1, Math.Min(Window, today - _first + 1)); }

        private static void Means(Cell c, int today, out double miss, out double got)
        {
            miss = got = 0;
            for (int k = 0; k < Window; k++)
            {
                int dd = c.Day[k];
                if (dd > today || dd <= today - Window) continue;
                miss += c.Miss[k]; got += c.Got[k];
            }
            double span = Span(today);
            miss /= span; got /= span;
        }

        // ============================================================ doba
        /// <summary>ArmouryBehavior.OnDailyTick (po pracach okregu tej doby): linie LP i zerowanie licznikow doby. Sam odczyt stanu gry. Nie rzuca.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            if (!s.SupplyChainLog) { ZeroDay(); _price.Clear(); if (_pubN.Count > 0) _pubN = new Dictionary<ItemCategory, double>(); _pubT = null; CraftShortage.ZeroLine(); return; }
            long t0 = Stopwatch.GetTimestamp();
            int today = Today;
            bool log = s.LogEnabled && LogGate.Lines;
            try
            {
                if (_first == int.MinValue) _first = today;
                if (!_startLine)
                {
                    _startLine = true;
                    Log.Info("LP start (435-L1): klucz Supply Chain Log TAK - SAM POMIAR, zadna liczba gry sie nie zmienia; pamiec sesji (srednie 28 dob licza sie od tej doby) | "
                             + "kolejnosc doby (ArmouryBehavior.OnDailyTick), wazna dla nastepnych krokow: TownPurse.BeforeRents -> DsTowns.DayStart -> PopulationLaw.Daily "
                             + "(tu zawor kasy miasta TownPurse.Valve: nadwyzka kasy do pana i korony) -> CastlePurse.Daily -> TownFolk.Daily -> TownGranary.Daily -> TownDebt.Daily -> "
                             + "BuildFunding.Daily -> TownFolk.DaWorksRun (zlecenia robot gminy i septow) -> GoodsLedger.Daily -> HarvestValve.Daily (regulator wsi) -> ... -> "
                             + "DistrictWorks.BeforeProduction -> StartCalibration.Daily -> VillageCrafts.Daily (przebudowy zajec) -> DistrictWorks.Daily -> SupplySignal.Daily (te linie). "
                             + "Fundator-miasto 'przed zaworem' (projekt L7) musi isc przed PopulationLaw.Daily, po DsTowns.DayStart.");
                }
                double msHands = 0;
                string hands = null;
                try { long th = Stopwatch.GetTimestamp(); hands = HandsText(today); msHands = (Stopwatch.GetTimestamp() - th) * 1000.0 / Stopwatch.Frequency; }
                catch (Exception e) { Stumble("Hands", e); }
                string nLine = null, wLine = null;
                try { nLine = ShortageText(today); } catch (Exception e) { Stumble("Shortage", e); }
                try { wLine = WorkshopText(today); } catch (Exception e) { Stumble("Workshops", e); }
                if (log)
                {
                    var sb = new StringBuilder(1400);
                    sb.Append("Lancuch podazy (LP): dzien ").Append(today).Append(" | L1 SAM POMIAR (zadna liczba gry sie nie zmienia) | material chciany i nieznaleziony dzis [szt.]:");
                    double mAll = 0, gAll = 0;
                    for (int i = 0; i < Streams; i++)
                    {
                        mAll += _dMiss[i]; gAll += _dGot[i];
                        sb.Append(i == 0 ? " " : ", ").Append(SName[i]).Append(' ').Append(F1(_dMiss[i]));
                        if (i != SShop) sb.Append(" (budzet bez pokrycia ").Append(N0(_dGapGold[i])).Append(" zl)");
                    }
                    sb.Append("; razem ").Append(F1(mAll)).Append(" | kupione tymi drogami dzis [szt.]:");
                    for (int i = 0; i < Streams; i++) sb.Append(i == 0 ? " " : ", ").Append(SName[i]).Append(' ').Append(F1(_dGot[i]));
                    sb.Append("; razem ").Append(F1(gAll)).Append(" | reszty budzetu ponizej ceny najtanszej sztuki (nie brak) ").Append(N0(_dRound)).Append(" zl")
                      .Append(" | wedlug towaru dzis (brak / kupione): ");
                    var l = new List<KeyValuePair<ItemCategory, double[]>>(_dCat);
                    l.Sort((a, b) => b.Value[0].CompareTo(a.Value[0]));
                    double cm = 0;
                    for (int i = 0; i < l.Count; i++)
                    {
                        cm += l[i].Value[0];
                        if (i < 14) sb.Append(i > 0 ? ", " : "").Append(l[i].Key.StringId).Append(' ').Append(F1(l[i].Value[0])).Append('/').Append(F1(l[i].Value[1]));
                    }
                    if (l.Count == 0) sb.Append('-'); else if (l.Count > 14) sb.Append(", ... (").Append(l.Count - 14).Append(" dalszych)");
                    sb.Append(" | przelicznik: budzet bez pokrycia rowno miedzy materialy budow po cenie targu (szacunek); wsad: potrzeba cyklu - stan polki; zlecenie robot ponawia sie co dobe - to poziom, nie suma")
                      .Append(" | ").Append(hands ?? "rece: brak pomiaru")
                      .Append(" | kontrola: brak wedlug drog - brak wedlug towarow = ").Append(F2(mAll - cm))
                      .Append(" | koszt ").Append(F2((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency)).Append(" ms (z tego rece ").Append(F2(msHands)).Append(" ms)")
                      .Append(" | poza watkiem ").Append(_dOff).Append(" | potkniecia ").Append(_st).Append(" (sesja ").Append(_stSess).Append(").");
                    Log.Info(sb.ToString());
                    if (nLine != null) Log.Info("LP niedobor: dzien " + today + " | " + nLine);
                    if (wLine != null) Log.Info("LP warsztaty: dzien " + today + " | " + wLine);
                }
                if (_dryDay == int.MinValue || today - _dryDay >= 28)
                {
                    _dryDay = today;
                    try { string d = DryText(); if (d != null && s.LogEnabled) Log.Info("LP wycena na sucho: dzien " + today + " | " + d); }
                    catch (Exception e) { Stumble("Dry", e); }
                }
                if (_whyDay == int.MinValue || today - _whyDay >= 7)
                {
                    _whyDay = today;
                    try { string w = DistrictWorks.Lp1Text(today); if (w != null && s.LogEnabled) Log.Info("LP prace gracza: dzien " + today + " | sam pomiar, ostatni powod kazdego wiersza przy powrocie wozu okregu | " + w); }
                    catch (Exception e) { Stumble("Why", e); }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); _price.Clear(); }
        }

        // ------------------------------------------------------------ rece okregow
        private static string HandsText(int today)
        {
            var book = new List<VillageBook.PartView>();
            int dist = 0, parts = 0, cmpParts = 0, movedParts = 0, movedDist = 0, cmpDist = 0;
            double home = 0, away = 0, maxRel = 0; string maxName = null;
            bool cmp = _prevHomeDay == today - 1;
            var now = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var v in Village.All)
            {
                try
                {
                    if (v == null || v.Settlement == null || !VillageBook.PartsOf(v, book)) continue;
                    dist++;
                    string id = v.Settlement.StringId;
                    double dNow = 0, dPrev = 0; bool dAll = cmp, any = false;
                    foreach (var p in book)
                    {
                        parts++; home += p.Home; away += p.Away;
                        string key = id + "|" + (p.Uid ?? "#main");
                        now[key] = p.Home;
                        dNow += p.Home;
                        double prev;
                        if (!cmp || !_prevHome.TryGetValue(key, out prev)) { dAll = false; continue; }
                        dPrev += prev;
                        if (prev < 1) continue;
                        cmpParts++;
                        double rel = (p.Home - prev) / prev;
                        if (Math.Abs(rel) > 0.01) { movedParts++; any = true; }
                        if (Math.Abs(rel) > Math.Abs(maxRel)) { maxRel = rel; maxName = (p.Name ?? "?") + " (" + (v.Name != null ? v.Name.ToString() : id) + ")"; }
                    }
                    if (dAll && dPrev >= 1)
                    {
                        cmpDist++;
                        if (Math.Abs(dNow - dPrev) / dPrev > 0.01 || any) movedDist++;
                    }
                }
                catch (Exception e) { Stumble("Hands(wies)", e); }
            }
            _prevHome.Clear();
            foreach (var kv in now) _prevHome[kv.Key] = kv.Value;
            _prevHomeDay = today;
            var sb = new StringBuilder(300);
            sb.Append("rece (ksiega wiosek): okregow ").Append(dist).Append(", czesci ").Append(parts).Append(", ludzi w domu ").Append(N0(home)).Append(", poza domem ").Append(N0(away));
            if (!cmp || cmpParts == 0) sb.Append("; zmiana doby: brak wczorajszej migawki (pierwsza doba sesji)");
            else sb.Append("; zmiana ludzi w domu od wczoraj ponad 1%: czesci ").Append(movedParts).Append(" z ").Append(cmpParts).Append(" (").Append(F1(100.0 * movedParts / cmpParts))
                   .Append("%), okregow z taka czescia albo zmiana sumy ").Append(movedDist).Append(" z ").Append(cmpDist).Append(" (").Append(cmpDist > 0 ? F1(100.0 * movedDist / cmpDist) : "-")
                   .Append("%), najwieksza ").Append((100 * maxRel).ToString("+0.0;-0.0", Inv)).Append("% ").Append(maxName ?? "-");
            return sb.ToString();
        }

        // ------------------------------------------------------------ tablica N
        private static Dictionary<ItemCategory, double[]> StockSignal(out int known, out int unknown)
        {
            known = unknown = 0;
            var res = new Dictionary<ItemCategory, double[]>();   // kategoria -> [S_stos, kolejne decyzje, stos dni (-1 nieznany), mnoznik]
            int dec;
            var peek = HarvestValve.LpPeek(out dec);
            if (peek == null || dec < _seenDec) { if (dec < _seenDec) _runs.Clear(); _seenDec = dec; }
            if (peek != null && dec != _seenDec)
            {
                _seenDec = dec;
                foreach (var g in peek)
                {
                    if (g == null || g.Item == null) continue;
                    int r; _runs.TryGetValue(g.Item, out r);
                    _runs[g.Item] = g.AtCeil && g.Short ? r + 1 : 0;
                }
            }
            var seen = new HashSet<ItemObject>();
            if (peek != null)
                foreach (var g in peek)
                {
                    if (g == null || g.Item == null || g.Item.ItemCategory == null) continue;
                    seen.Add(g.Item);
                    int r; _runs.TryGetValue(g.Item, out r);
                    if (r <= 0) continue;
                    known++;
                    double sv = Math.Min(2.0, Math.Pow(1.25, r));
                    double[] cur;
                    if (!res.TryGetValue(g.Item.ItemCategory, out cur) || cur[0] < sv) res[g.Item.ItemCategory] = new[] { sv, r, g.StockDays, g.M };
                }
            // po wczytaniu (brak migawki decyzji w tej sesji): towar z mnoznikiem na suficie kroku liczony jak jedna decyzja, stos nieznany
            var f = HarvestValve.LpFactors();
            if (f != null)
            {
                float ceil = HarvestValve.LpCeil();
                foreach (var kv in f)
                {
                    if (kv.Key == null || kv.Key.ItemCategory == null || seen.Contains(kv.Key) || kv.Value < ceil - 1e-3f) continue;
                    unknown++;
                    if (!res.ContainsKey(kv.Key.ItemCategory)) res[kv.Key.ItemCategory] = new[] { 1.25, 1, -1.0, kv.Value };
                }
            }
            return res;
        }

        private static string ShortageText(int today)
        {
            int known, unknown;
            var stock = StockSignal(out known, out unknown);
            // zboze: miasta, ktorych wsie gloduja
            var hungry = new HashSet<Settlement>();
            ItemCategory grain = null;
            try { var gi = MBObjectManager.Instance.GetObject<ItemObject>("grain"); grain = gi != null ? gi.ItemCategory : null; } catch { grain = null; }
            foreach (var v in Village.All)
            {
                try
                {
                    if (v == null || !(Famine.HungerOf(v) > 0f)) continue;
                    var mk = v.TradeBound != null ? v.TradeBound : v.Bound;
                    if (mk != null && mk.Town != null) hungry.Add(mk);
                }
                catch (Exception e) { Stumble("Shortage(glod)", e); }
            }
            // kategoria -> [brak swiata, sprzedaz swiata, miast z N >= 1.5, najwieksze N miasta, miast z komorka]
            var agg = new Dictionary<ItemCategory, double[]>();
            var hot = new HashSet<Settlement>();
            int cells = 0, real = 0;
            var nt = new NTable { Grain = grain, Day = today };   // 435-L6: tablica do odczytu (publikowana na koncu, po zbudowaniu)
            foreach (var tk in _cells)
            {
                foreach (var ck in tk.Value)
                {
                    double miss, got;
                    Means(ck.Value, today, out miss, out got);
                    if (!(miss > 0) && !(got > 0)) continue;
                    cells++;
                    double sales = got;
                    double idx; int units;
                    if (MarketSales.Index(tk.Key, ck.Key, out idx, out units) && units / (double)Window > sales) { sales = units / (double)Window; real++; }
                    double sq = miss > 1e-6 ? 1.0 + Math.Min(1.0, sales > 1e-9 ? miss / sales : 1.0) : 1.0;
                    double[] st; double ss = stock.TryGetValue(ck.Key, out st) ? st[0] : 1.0;
                    double n = Math.Min(2.0, Math.Max(1.0, Math.Max(ss, sq)));
                    if (grain != null && ck.Key == grain && hungry.Contains(tk.Key)) n = 2.0;
                    if (n > 1.0 + 1e-9)
                    {
                        Dictionary<ItemCategory, double> tm;
                        if (!nt.Cell.TryGetValue(tk.Key, out tm)) { tm = new Dictionary<ItemCategory, double>(); nt.Cell[tk.Key] = tm; }
                        tm[ck.Key] = n;
                    }
                    double[] a;
                    if (!agg.TryGetValue(ck.Key, out a)) { a = new double[5]; agg[ck.Key] = a; }
                    a[0] += miss; a[1] += sales; a[4]++;
                    if (n >= 1.5) { a[2]++; hot.Add(tk.Key); }
                    if (n > a[3]) a[3] = n;
                }
            }
            foreach (var kv in stock) if (!agg.ContainsKey(kv.Key)) agg[kv.Key] = new double[5];
            foreach (var h in hungry) { hot.Add(h); nt.Hungry.Add(h); }
            foreach (var kv in stock) if (kv.Value[0] > 1.0 + 1e-9) nt.Stock[kv.Key] = Math.Min(2.0, kv.Value[0]);
            _pubT = nt;
            var rows = new List<KeyValuePair<ItemCategory, double[]>>();
            int over = 0;
            foreach (var kv in agg)
            {
                double[] st; double ss = stock.TryGetValue(kv.Key, out st) ? st[0] : 1.0;
                double sq = kv.Value[0] > 1e-6 ? 1.0 + Math.Min(1.0, kv.Value[1] > 1e-9 ? kv.Value[0] / kv.Value[1] : 1.0) : 1.0;
                double nw = Math.Min(2.0, Math.Max(1.0, Math.Max(ss, sq)));
                if (nw > 1.0 + 1e-9) over++;
                rows.Add(new KeyValuePair<ItemCategory, double[]>(kv.Key, new[] { nw, ss, sq, kv.Value[0], kv.Value[1], kv.Value[2], kv.Value[3], kv.Value[4] }));
            }
            rows.Sort((a, b) => { int c = b.Value[0].CompareTo(a.Value[0]); return c != 0 ? c : b.Value[3].CompareTo(a.Value[3]); });
            // 435-L5: N swiata do odczytu (kolejnosc zlecen sprzetu wsi nastepnej doby)
            var pn = new Dictionary<ItemCategory, double>();
            foreach (var r in rows) if (r.Value[0] > 1.0 + 1e-9) pn[r.Key] = r.Value[0];
            _pubN = pn;
            var sb = new StringBuilder(1200);
            sb.Append(CraftShortage.UsedText()).Append(" | N = max(1, S_stos, S_kolejka) <= 2, cena poza N; srednie z ").Append(Span(today)).Append(" dob sesji (okno 28)")
              .Append(" | towarow z N swiata > 1: ").Append(over).Append(" z ").Append(rows.Count).Append(" | lista (towar N swiata = stos / kolejka; brak szt./d; sprzedaz szt./d; miast z N >= 1.5 / z pomiarem; najwyzsze N miasta): ");
            int shown = 0;
            foreach (var r in rows)
            {
                if (shown >= 12) break;
                var x = r.Value;
                if (!(x[0] > 1.0 + 1e-9) && !(x[3] > 0)) continue;
                sb.Append(shown > 0 ? "; " : "").Append(r.Key.StringId).Append(' ').Append(F2(x[0])).Append(" = ").Append(F2(x[1])).Append(" / ").Append(F2(x[2]))
                  .Append("; ").Append(F1(x[3])).Append("; ").Append(F1(x[4])).Append("; ").Append((int)x[5]).Append(" / ").Append((int)x[7]).Append("; ").Append(F2(x[6]));
                shown++;
            }
            if (shown == 0) sb.Append('-');
            sb.Append(" | S_stos (regulator wsi 221): towarow na suficie przy stosie ponizej progu ").Append(known).Append(", na suficie bez migawki decyzji w tej sesji (stos nieznany, liczone 1.25) ").Append(unknown);
            if (stock.Count > 0)
            {
                sb.Append(" [");
                bool f = true;
                foreach (var kv in stock)
                {
                    sb.Append(f ? "" : ", ").Append(kv.Key.StringId).Append(" x").Append(F2(kv.Value[3])).Append(" decyzji ").Append((int)kv.Value[1])
                      .Append(kv.Value[2] >= 0 ? ", stos " + N0(kv.Value[2]) + " dni" : ", stos ?");
                    f = false;
                }
                sb.Append(']');
            }
            sb.Append(" | zboze: miast z glodujacymi wsiami (N = 2) ").Append(hungry.Count)
              .Append(" | miast z N >= 1.5 dla jakiegos towaru: ").Append(hot.Count).Append(" | komorek (miasto x towar) z pomiarem ").Append(cells)
              .Append(", w tym sprzedaz z prawdziwej sprzedazy miasta (241) ").Append(real).Append(", reszta: sztuki kupione tymi samymi drogami (odstepstwo od projektu - brak licznika calej sprzedazy surowcow)")
              .Append(" | poza pomiarem: surowiec warsztatow zbrojnych (linia 'Warsztaty: dzien' - brak surowca), kontrakty 174")
              .Append(" | ").Append(CraftShortage.LineText(nt))
              .Append(" | kolejnosc zlecen sprzetu wsi (435-L5, tylko przy zuzyciu sprzetu): ").Append(VillageGear.WearOn ? "czyta N swiata" : "nieczynna");
            return sb.ToString();
        }

        // ------------------------------------------------------------ warsztaty
        private static string WorkshopText(int today)
        {
            int total = 0, tried = 0, worked = 0, seen28 = 0, hi = 0, mid = 0, low = 0, dead = 0, noData = 0, player = 0;
            var stand = new int[RKinds];
            long done = 0, fail = 0, done28 = 0, fail28 = 0;
            var live = new HashSet<Workshop>();
            foreach (var t in Town.AllTowns)
            {
                if (t == null || !t.IsTown || t.Workshops == null) continue;
                foreach (var w in t.Workshops)
                {
                    try
                    {
                        if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden) continue;
                        total++; live.Add(w);
                        if (w.Owner == Hero.MainHero) player++;
                        int[] ln;
                        if (_wLine.TryGetValue(w, out ln) && ln[0] + ln[1] > 0)
                        {
                            tried++; done += ln[0]; fail += ln[1];
                            if (ln[0] > 0) worked++; else stand[ln[2] > 0 && ln[2] < RKinds ? ln[2] : ROther]++;
                        }
                        WRec rec;
                        if (!_ws.TryGetValue(w, out rec)) { noData++; continue; }
                        long d28 = 0, f28 = 0;
                        for (int k = 0; k < Window; k++)
                        {
                            int dd = rec.Day[k];
                            if (dd > today || dd <= today - Window) continue;
                            d28 += rec.Done[k]; f28 += rec.Fail[k];
                        }
                        if (d28 + f28 == 0) { noData++; continue; }
                        seen28++; done28 += d28; fail28 += f28;
                        double u = d28 / (double)(d28 + f28);
                        if (u >= 0.90) hi++; else if (u >= 0.30) mid++; else { low++; if (u < 0.10) dead++; }
                    }
                    catch (Exception e) { Stumble("Workshops(warsztat)", e); }
                }
            }
            if (_ws.Count > live.Count + 64)                                  // warsztaty, ktorych juz nie ma (zmiana typu trzyma ten sam obiekt) - pamiec nie rosnie
            {
                var old = new List<Workshop>();
                foreach (var k in _ws.Keys) if (!live.Contains(k)) old.Add(k);
                foreach (var k in old) _ws.Remove(k);
            }
            int cyc = 0; for (int r = 0; r < RKinds; r++) cyc += _wDay[r];
            var sb = new StringBuilder(900);
            sb.Append("L1 sam pomiar | warsztaty towarowe miast (bez ukrytych rzemieslnikow) ").Append(total).Append(", w tym gracza ").Append(player)
              .Append(" | od poprzedniej linii: z proba cyklu linii towarowej ").Append(tried).Append(", pracuje (>= 1 udany cykl) ").Append(worked).Append(", stoi ").Append(tried - worked)
              .Append(" wedlug ostatniego powodu:");
            bool f = true;
            for (int r = 1; r < RKinds; r++) if (stand[r] > 0) { sb.Append(f ? " " : ", ").Append(RName[r]).Append(' ').Append(stand[r]); f = false; }
            if (f) sb.Append(" -");
            sb.Append(" | cykle od poprzedniej linii: ").Append(cyc).Append(" = udane ").Append(_wDay[ROk]);
            for (int r = 1; r < RKinds; r++) sb.Append(", ").Append(RName[r]).Append(' ').Append(_wDay[r]);
            sb.Append("; u tej doby ").Append(done + fail > 0 ? F2(done / (double)(done + fail)) : "-").Append(" (cykle udane / (udane + nieudane); GORNA granica - po nieudanym cyklu gra nie probuje nastepnych tej doby)")
              .Append(" | u z ").Append(Span(today)).Append(" dob sesji (okno 28): srednie ").Append(done28 + fail28 > 0 ? F2(done28 / (double)(done28 + fail28)) : "-")
              .Append(", warsztatow z pomiarem ").Append(seen28).Append(": u >= 0.90 ").Append(hi).Append(", 0.30-0.90 ").Append(mid).Append(", ponizej 0.30 ").Append(low)
              .Append(" (w tym ponizej 0.10 ").Append(dead).Append("), bez proby w oknie ").Append(noData)
              .Append(" | poza: cykle linii uzbrojenia ").Append(_wArms).Append(" (WorkshopLaw), warsztaty zamkow ").Append(_wCastle).Append(", cykle warsztatow gracza w liczbach wyzej ").Append(_wPlayer)
              .Append(" | kontrola: cykle warsztatow z listy - cykle z licznika powodow = ").Append(done + fail - cyc).Append(" (rozne od 0: warsztat zmienil wlasciciela albo miasto w tej dobie)")
              .Append(", warsztaty z proba - (pracuje + stoi wedlug powodu) = ").Append(tried - worked - SumA(stand));
            string bl = WorkshopBenches.TakeLine();   // 435-L8: stanowiska warsztatow (policzone tej doby w WorkshopBenches.Daily, przed ta linia)
            if (bl != null) sb.Append(" | ").Append(bl);
            return sb.ToString();
        }

        private static int SumA(int[] a) { int x = 0; for (int i = 0; i < a.Length; i++) x += a[i]; return x; }

        // ------------------------------------------------------------ wycena na sucho (bilans otwarcia sprzetu, tabela II.2)
        private static string DryText()
        {
            long t0 = Stopwatch.GetTimestamp();
            var parts = new List<VillageCrafts.PartCraft>();
            var n = new int[GN];
            double[] hands = new double[GN], awayG = new double[GN], val = new double[GN], k0 = new double[GN], value = new double[GN], mat = new double[GN],
                     idle = new double[GN], onePct = new double[GN], burnt = new double[GN], wearMat = new double[GN];
            int dist = 0, ksN = 0, noCraft = 0, empty = 0;
            foreach (var v in Village.All)
            {
                try
                {
                    bool ks;
                    if (v == null || !VillageCrafts.PartsOf(v, parts, out ks)) continue;
                    dist++; if (ks) ksN++;
                    foreach (var pc in parts)
                    {
                        if (pc == null || pc.Book == null) continue;
                        double H = pc.Book.Home, A = pc.Book.Away;
                        double sum = 0; for (int c = 0; c < pc.CraftVal.Length; c++) sum += Math.Max(0, pc.CraftVal[c]);
                        if (!(sum > 0)) { if (pc.Craft < 0) noCraft++; else if (H < 1) empty++; if (!(pc.ValDay > 0) || pc.Craft < 0) continue; }
                        double br = pc.Book.Nw > 0 ? Math.Min(1.0, Math.Max(0.0, pc.Book.Br / pc.Book.Nw)) : 0.0;
                        for (int c = 0; c < pc.CraftVal.Length && c < KsTab.Crafts.Length; c++)
                        {
                            double vc = sum > 0 ? Math.Max(0, pc.CraftVal[c]) : (c == pc.Craft ? pc.ValDay : 0);
                            if (!(vc > 0)) continue;
                            double share = sum > 0 ? vc / sum : 1.0;
                            int g = GroupOf(KsTab.Crafts[c]);
                            int kind = DistrictWorks.KindOfCraft(KsTab.Crafts[c]);
                            double km = kind >= 0 && g != GOut ? DistrictWorks.KM[kind] : 0, kl = kind >= 0 && g != GOut ? DistrictWorks.KL[kind] : 0;
                            n[g]++; hands[g] += H * share; awayG[g] += A * share; val[g] += vc;
                            if (g == GOut) continue;
                            idle[g] += vc * (1 - GPhi[g]);
                            onePct[g] += 0.01 * km * GDays[g] * vc;
                            wearMat[g] += GWear[g] * km * GDays[g] * vc / 364.0;
                            if (H < 1) continue;                                // bez ludzi w domu wartosc dnia reki nieokreslona
                            double keep = 1 - kl * br;
                            double units = (H + A) * share * keep;
                            double pUnit = GDays[g] * vc / (H * share);         // cena jednostki = D x wartosc dnia reki
                            k0[g] += units; burnt[g] += (H + A) * share * kl * br;
                            value[g] += units * pUnit; mat[g] += km * units * pUnit;
                        }
                    }
                }
                catch (Exception e) { Stumble("Dry(wies)", e); }
            }
            var sb = new StringBuilder(1500);
            sb.Append("L1 sam pomiar, co 28 dob - wycena bilansu otwarcia sprzetu (projekt II.4) i tabeli II.2; nic tych liczb nie czyta | okregow ").Append(dist).Append(" (pod KS ").Append(ksN)
              .Append("), czesci bez zajecia ").Append(noCraft).Append(", bez wartosci dnia ").Append(empty)
              .Append(" | rodzaj: czesci-zajec; rece w domu / poza domem; wartosc dnia zl; phi; cena jednostki dni; K0 jednostek (stracone ogniem); wartosc K0 zl; z tego material zl; "
                      + "produkcja dnia stojaca za sprzetem (1 - phi) zl; material na 1% rak zl; material zuzycia na dobe przy pelnej tabeli zl");
            double tv = 0, tk = 0, tval = 0, tm = 0, ti = 0, t1 = 0, tw = 0, th = 0, ta = 0;
            for (int g = 0; g < GN; g++)
            {
                sb.Append(" | ").Append(GName[g]).Append(": ").Append(n[g]).Append("; ").Append(N0(hands[g])).Append(" / ").Append(N0(awayG[g])).Append("; ").Append(N0(val[g]));
                tv += val[g]; th += hands[g]; ta += awayG[g];
                if (g == GOut) continue;
                sb.Append("; ").Append(F2(GPhi[g])).Append("; ").Append(N0(GDays[g])).Append("; ").Append(N0(k0[g])).Append(" (").Append(N0(burnt[g])).Append("); ").Append(N0(value[g]))
                  .Append("; ").Append(N0(mat[g])).Append("; ").Append(N0(idle[g])).Append("; ").Append(N0(onePct[g])).Append("; ").Append(N0(wearMat[g]));
                tk += k0[g]; tval += value[g]; tm += mat[g]; ti += idle[g]; t1 += onePct[g]; tw += wearMat[g];
            }
            sb.Append(" | RAZEM: rece ").Append(N0(th)).Append(" / ").Append(N0(ta)).Append(", wartosc dnia wsi ").Append(N0(tv)).Append(" zl, K0 ").Append(N0(tk)).Append(" jednostek o wartosci ")
              .Append(N0(tval)).Append(" zl (material ").Append(N0(tm)).Append(" zl), produkcja dnia zalezna od sprzetu ").Append(N0(ti)).Append(" zl (").Append(tv > 0 ? F1(100 * ti / tv) : "-")
              .Append("% wartosci dnia), material na 1% rak ").Append(N0(t1)).Append(" zl, material zuzycia na dobe przy pelnej tabeli ").Append(N0(tw))
              .Append(" zl (porownaj z 'budzet bez pokrycia' robot gminy w linii LP)")
              .Append(" | zastrzezenia: rece = ludzie w domu z ksiegi wiosek dzieleni miedzy zajecia czesci wedlug wartosci dnia; wartosc dnia przy cenach miasta targowego dzis (zima tnie plon - wycena w zimie zanizona)")
              .Append(" | koszt ").Append(F2((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency)).Append(" ms");
            return sb.ToString();
        }
    }
}
