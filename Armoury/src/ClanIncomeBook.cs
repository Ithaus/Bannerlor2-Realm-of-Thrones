using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// PACZKA 169 - D NA ROD (dochod staly) + BUDZET RODOW I DLUGI NA SUCHO (sam log, docs/paczki/169-ksiega-obiegu.md rozdz. 7-8).
    /// D = srednia 28 ostatnich dob wplywow do kies rodu z zewnatrz rodu (bez pozyczek i wyprzedazy): (a) dochod BRUTTO czynnego modelu
    /// finansow (ta sama definicja co KingdomTreasury, BuildFunding, IronBank - D19), (b) renty PopulationLaw, (c) zwrot zoldu korony,
    /// (d) mennica i monopole krola, (e) zdarzenia gry spoza rodu poza rozliczeniem rodu i poza naszym tickiem, (f) trzecia lorda.
    /// Brakujace doby pierscienia licza sie jako G/60 z pierwszego zobaczenia rodu (Seed). Pierscien zapisywany w grze (SaveText,
    /// klucz Clan.StringId). Budzet (pulap zoldu, zwolnienia) i dlugi (szczeble, zajecie, pozyczki wg 8.2) TYLKO liczone i logowane -
    /// niczego nie zmienia w grze; paczki 166/168 zaczna te liczby stosowac. IronBank.Limit NIE jest wolany (D21).
    /// </summary>
    internal static partial class ClanIncomeBook
    {
        internal const int KRefund = 0, KCrownLevies = 1, KThird = 2;
        private const int Days = 28;

        // 6.4 projektu, przebieg na sucho - 166: udzialy pulapu przeszly do Settings (ClanBudget.Ceiling - jedna formula dla logu i gry); tu zostaje reszta przebiegu na sucho
        private const double BudgetHysteresis = 1.10, ReleasePerDay = 0.15, CeilingFill = 0.9,
                             IronBankIncomeDays82 = 15, InstalmentShare = 0.10, SeizeFloor = 38000, SeizeWageDays = 10;
        private const int BudgetHysteresisDays = 3;

        internal static bool On { get { var s = Settings.Current; return s != null && s.ClanIncomeBookEnabled; } }

        internal sealed class Rec
        {
            public readonly int[] Ring = new int[Days];    // wplyw kolejnych dob (int, przyciety do int.MaxValue)
            public int Head, Filled, Seed = -1, Streak;     // Seed = G/60 przy pierwszym zobaczeniu rodu; Streak = doby z zoldem > 1.10 x pulap
            public long Today, TodayRefund, TodayCrown, TodayEvtNone, TodayEvtSettl, TodayEvtOther, TodayThird;   // od ostatniego Daily
            public long TodayEstates;                                  // 169c: w tym wyplaty majatkow BK widziane jako zdarzenie (podwojne) - tylko pamiec
            public long WageAccLord, WageAccGar, WageAccCar;          // w biezacym rozliczeniu rodu
            public int WageLastLord, WageLastGar, WageLastCar;        // z ostatniego pelnego rozliczenia rodu
            public bool HadTick;                                       // bylo choc jedno zmierzone rozliczenie
            public int SaldoLast; public bool SaldoSeen;               // D19: saldo naliczone przy ostatnim rozliczeniu (O40) - tylko CSV i linia, nie zapisywane
            // ostatni przebieg Daily (do linii i CSV, nie zapisywane)
            public double D, G; public long Inflow, A, B, Refund, Crown, Evt, Third;
        }

        private static readonly Dictionary<string, Rec> _book = new Dictionary<string, Rec>();   // klucz Clan.StringId (zapis)
        private static readonly Dictionary<Clan, Rec> _byClan = new Dictionary<Clan, Rec>();     // pamiec podreczna sesji
        private static Rec _open;
        private static Clan _openClan;

        // sumy dnia (linia "Obieg") - Day* rosna razem z Today*, na koncu Daily kopiowane do Last* i zerowane
        internal static long DayRefund, DayCrown, DayEvtNone, DayEvtSettl, DayEvtOther, DayThird;
        internal static long LastRefund, LastCrown, LastEvtNone, LastEvtSettl, LastEvtOther, LastThird, LastModelIncomeSum, LastRentSum;
        internal static long LastTicks;                     // koszt ostatniego Daily (kontrolka 6.8)
        // 169b: rozbicie kosztu; dochod modelu liczony TU jak w 169 (po recenzji: liczba z KingdomTreasury jest z innej chwili - zmienialaby D),
        // obok porownanie z liczba KingdomTreasury tej doby (za darmo, bez dodatkowych wyliczen) - dane do decyzji o przyspieszeniu
        internal static long LastTicksModel, LastTicksReport, LastTicksCsv, LastTicksCmp, LastCmpDiff, LastCmpAbs;
        internal static int LastOwnCalls, LastCmpN, LastCmpNe;
        private static long _ticksCsv;
        private static readonly Dictionary<Clan, float> _modelSeen = new Dictionary<Clan, float>();   // dochod modelu policzony dzis przez KingdomTreasury.Daily
        private static int _modelSeenDay = -1;

        private static readonly Dictionary<Clan, float> IncomeToday = new Dictionary<Clan, float>();   // (a) raz na rod na dobe (D21)
        private static int _importN = -1, _importBad;
        private static string _csvPath;
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        // bufory wielokrotnego uzytku (raz na dobe)
        private static readonly HashSet<string> _seen = new HashSet<string>();
        private static readonly List<string> _drop = new List<string>();
        private static readonly List<Clan> _today = new List<Clan>();
        private static readonly Dictionary<IFaction, bool> _war = new Dictionary<IFaction, bool>();
        private static readonly HashSet<Kingdom> _defaulted = new HashSet<Kingdom>();
        private static readonly Dictionary<string, Clan> _byId = new Dictionary<string, Clan>();

        internal static void Reset()
        {
            _book.Clear(); _byClan.Clear(); _open = null; _openClan = null;
            DayRefund = DayCrown = DayEvtNone = DayEvtSettl = DayEvtOther = DayThird = 0;
            LastRefund = LastCrown = LastEvtNone = LastEvtSettl = LastEvtOther = LastThird = LastModelIncomeSum = LastRentSum = 0;
            LastTicks = 0; IncomeToday.Clear(); _importN = -1; _importBad = 0; _csvPath = null; _stumbles = 0; _errSites.Clear();
            LastTicksModel = LastTicksReport = LastTicksCsv = LastTicksCmp = LastCmpDiff = LastCmpAbs = 0; LastOwnCalls = LastCmpN = LastCmpNe = 0;   // 169b
            _modelSeen.Clear(); _modelSeenDay = -1; _ticksCsv = 0;
            ResetStable();   // 169c
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("ClanIncomeBook." + where, e); } catch { }
        }

        private static Rec Of(Clan c)
        {
            Rec r;
            if (_byClan.TryGetValue(c, out r)) return r;
            string id = c.StringId;
            if (id == null) return null;
            if (!_book.TryGetValue(id, out r)) { r = new Rec(); _book[id] = r; }
            _byClan[c] = r;
            return r;
        }

        private static bool OutOfWorld(Hero h) { return h.HeroState == Hero.CharacterStates.Dead || h.HeroState == Hero.CharacterStates.Disabled; }

        // ------------------------------------------------------------ wplywy (liczniki; kazda metoda we wlasnym try - D17)
        /// <summary>Wplyw do kiesy bohatera z naszych modulow bez zdarzenia gry: zwrot zoldu korony, mennica i monopole, trzecia lorda.</summary>
        internal static void NoteInflow(Hero h, int amount, int kind)
        {
            try
            {
                if (!On || h == null || amount <= 0) return;
                var c = h.Clan;
                if (c == null || c.IsBanditFaction) return;
                var r = Of(c);
                if (r == null) return;
                r.Today += amount;
                if (kind == KRefund) { r.TodayRefund += amount; DayRefund += amount; }
                else if (kind == KCrownLevies) { r.TodayCrown += amount; DayCrown += amount; }
                else if (kind == KThird) { r.TodayThird += amount; DayThird += amount; }
            }
            catch (Exception e) { Stumble("NoteInflow", e); }
        }

        /// <summary>MoneyLedger.OnGoldTraded: zdarzenie gry do zywego czlonka rodu od platnika spoza rodu, poza rozliczeniem rodu i naszym tickiem.</summary>
        internal static void OnEvent(Hero gh, PartyBase gp, Hero rh, int a, bool gNone, bool inClan, bool inBlock, bool estate)
        {
            try
            {
                if (inClan || inBlock || rh == null || a <= 0 || !On) return;
                var c = rh.Clan;
                if (c == null || c.IsBanditFaction || OutOfWorld(rh)) return;
                if (gh != null && gh.Clan == c) return;             // przelew wewnatrz rodu
                var r = Of(c);
                if (r == null) return;
                r.Today += a;
                if (gNone) { r.TodayEvtNone += a; DayEvtNone += a; if (estate) { r.TodayEstates += a; DayEstates += a; } }   // 169c: majatek BK (podwojne)
                else if (gh == null && gp != null && gp.IsSettlement) { r.TodayEvtSettl += a; DayEvtSettl += a; }
                else { r.TodayEvtOther += a; DayEvtOther += a; }
            }
            catch (Exception e) { Stumble("OnEvent", e); }
        }

        /// <summary>
        /// 169b: KingdomTreasury.Daily policzyl dochod brutto czynnego modelu (CalculateClanIncome(rod, false, false, false)) - te same
        /// argumenty co (a) w D, ale w INNEJ chwili: przed powinnosciami tego i wczesniejszych rodow, clem (licznik cel miast), zwrotem zoldu
        /// i Bankiem, ktore zmieniaja to, co model czyta (kiesa glowy, skarbiec, TradeTaxAccumulated). Daily NIE bierze jej do D (zmienialaby
        /// wynik - recenzja 169b), tylko porownuje z wlasnym wyliczeniem (dane do decyzji, czy przyjac "D(a) z chwili powinnosci"). Tylko zapis liczby.
        /// </summary>
        internal static void NoteModelIncome(Clan c, float income)
        {
            try
            {
                if (!On || c == null) return;
                int d = (int)CampaignTime.Now.ToDays;
                if (d != _modelSeenDay) { _modelSeen.Clear(); _modelSeenDay = d; }
                _modelSeen[c] = income;
            }
            catch (Exception e) { Stumble("NoteModelIncome", e); }
        }

        // ------------------------------------------------------------ zold rodu z rozliczenia (okno rodu MoneyLedger)
        internal static void ClanTickOpen(Clan c)
        {
            try
            {
                _open = null; _openClan = null;
                if (!On || c == null) return;
                var r = Of(c);
                if (r == null) return;
                r.WageAccLord = r.WageAccGar = r.WageAccCar = 0;
                _open = r; _openClan = c;
            }
            catch (Exception e) { _open = null; _openClan = null; Stumble("ClanTickOpen", e); }
        }

        /// <summary>MoneyLedger.WagePostfix: zold naliczony partii w trakcie rozliczenia rodu (k: 0 partie rodow, 1 garnizony, 2 karawany, 3 inne).</summary>
        internal static void NoteWage(Clan c, int k, int wage)
        {
            try
            {
                if (_open == null || c == null || !ReferenceEquals(c, _openClan)) return;
                if (k == 0) _open.WageAccLord += wage;
                else if (k == 1) _open.WageAccGar += wage;
                else if (k == 2) _open.WageAccCar += wage;
            }
            catch (Exception e) { Stumble("NoteWage", e); }
        }

        private static int Clamp(long v) { return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, v)); }

        internal static void ClanTickClose(Clan c)
        {
            try
            {
                if (_open != null && ReferenceEquals(c, _openClan))
                {
                    _open.WageLastLord = Clamp(_open.WageAccLord); _open.WageLastGar = Clamp(_open.WageAccGar); _open.WageLastCar = Clamp(_open.WageAccCar);
                    _open.HadTick = true;
                }
            }
            catch (Exception e) { Stumble("ClanTickClose", e); }
            _open = null; _openClan = null;
        }

        /// <summary>Finalizer okna rodu (D16) zamknal przerwane rozliczenie - nie nadpisuje ostatniego pelnego.</summary>
        internal static void ClanTickAbort() { _open = null; _openClan = null; }

        /// <summary>O40 (D19): saldo naliczone tego rozliczenia - tylko do CSV i linii budzetu.</summary>
        internal static void NoteModelSaldo(Clan c, int saldo)
        {
            try
            {
                if (!On || c == null) return;
                var r = Of(c);
                if (r == null) return;
                r.SaldoLast = saldo; r.SaldoSeen = true;
            }
            catch (Exception e) { Stumble("NoteModelSaldo", e); }
        }

        /// <summary>D rodu albo -1 (dla przyszlych paczek; w 169 nikt go nie wola w logice gry).</summary>
        internal static double StableIncome(Clan c)
        {
            try
            {
                if (c == null || c.StringId == null) return -1;
                Rec r;
                if (!_book.TryGetValue(c.StringId, out r) || r.Seed < 0) return -1;
                return DOf(r);
            }
            catch { return -1; }
        }

        private static double DOf(Rec r)
        {
            long s = 0;
            for (int i = 0; i < Days; i++) s += r.Ring[i];
            return (s + (double)(Days - Math.Min(Days, r.Filled)) * Math.Max(0, r.Seed)) / Days;
        }

        private static long FamilyGold(Clan c)
        {
            long g = 0; bool leader = false;
            var lords = c.AliveLords;
            if (lords != null)
                for (int i = 0; i < lords.Count; i++)
                {
                    var h = lords[i];
                    if (h == null || h.IsChild) continue;
                    g += h.Gold;
                    if (h == c.Leader) leader = true;
                }
            if (!leader && c.Leader != null) g += c.Leader.Gold;
            return g;
        }

        // ------------------------------------------------------------ raz na dobe (w bloku, po Banku, przed MoneyLedger.Daily)
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            bool ran = false;
            try
            {
                if (Campaign.Current == null) return;
                if (!On) return;
                ran = true;
                var model = Campaign.Current.Models.ClanFinanceModel;
                IncomeToday.Clear(); _seen.Clear(); _byId.Clear(); _today.Clear();
                // 169c: D staly - wsie, renty i "wlasne" tej doby przed petla rodow (wlasny try; blad wylacza tylko rozbicie na dzis)
                bool sd = StableDOn;
                if (sd) { try { StableBegin(); } catch (Exception e) { sd = false; Stumble("StableBegin", e); } }
                long modelSum = 0, rentSum = 0;
                // 169b (po recenzji): dochod modelu (a) liczony TU dla kazdego rodu - ta sama chwila co w 169 (po powinnosciach, clach, zwrocie
                // zoldu i Banku tej doby), wiec ten sam wynik; liczba z KingdomTreasury.Daily tylko do porownania (koszt i roznica - do decyzji)
                int gameDay = (int)CampaignTime.Now.ToDays;
                bool seenToday = gameDay == _modelSeenDay && _modelSeen.Count > 0;
                long tModel = 0, tCmp = 0, cDiff = 0, cAbs = 0; int own = 0, cmpN = 0, cmpNe = 0;
                foreach (var c in Clan.All)
                {
                    try
                    {
                        if (c == null || c.IsEliminated || c.StringId == null) continue;
                        _seen.Add(c.StringId); _byId[c.StringId] = c;
                        if (c.IsBanditFaction || c.Leader == null) continue;
                        var r = Of(c);
                        if (r == null) continue;
                        long g = FamilyGold(c);
                        if (r.Seed < 0) r.Seed = (int)Math.Min(int.MaxValue, g / 60);
                        float a = 0f, seen;
                        long ts = Stopwatch.GetTimestamp();
                        // 169c: z opisami linii (wynik ten sam - opisy nie zmieniaja rachunku modelu), zeby rozbic (a) na czesci D stalego
                        ExplainedNumber en = default(ExplainedNumber); bool haveEn = false;
                        try
                        {
                            if (sd) { en = model.CalculateClanIncome(c, true, false, false); haveEn = true; a = Math.Max(0f, en.ResultNumber); }
                            else a = Math.Max(0f, model.CalculateClanIncome(c, false, false, false).ResultNumber);
                        }
                        catch { haveEn = false; }
                        long dt = Stopwatch.GetTimestamp() - ts;
                        tModel += dt; own++;
                        if (seenToday && _modelSeen.TryGetValue(c, out seen))
                        {
                            // porownanie (tylko log): liczba z chwili powinnosci minus liczba D; dt - koszt, ktorego by nie bylo przy jej wzieciu
                            long dd = (long)Math.Max(0f, seen) - (long)a;
                            cDiff += dd; cAbs += Math.Abs(dd); cmpN++; if (dd != 0) cmpNe++; tCmp += dt;
                        }
                        IncomeToday[c] = a;
                        int b; PopulationLaw.RentToday.TryGetValue(c, out b);
                        long inflow = (long)a + Math.Max(0, b) + r.Today;
                        r.Ring[r.Head] = Clamp(inflow);
                        r.Head = (r.Head + 1) % Days;
                        r.Filled = Math.Min(Days, r.Filled + 1);
                        r.Inflow = inflow; r.A = (long)a; r.B = b; r.Refund = r.TodayRefund; r.Crown = r.TodayCrown; r.Third = r.TodayThird;
                        r.Evt = r.TodayEvtNone + r.TodayEvtSettl + r.TodayEvtOther;
                        if (sd) { try { StableClan(c, r, en, haveEn, (long)a, b); } catch (Exception e) { Stumble("StableClan", e); } }   // 169c: przed zerowaniem Today*
                        r.Today = r.TodayRefund = r.TodayCrown = r.TodayEvtNone = r.TodayEvtSettl = r.TodayEvtOther = r.TodayThird = r.TodayEstates = 0;
                        r.D = DOf(r); r.G = g;
                        modelSum += (long)a; rentSum += b;
                        _today.Add(c);
                    }
                    catch (Exception e) { Stumble("Daily(rod)", e); }
                }
                // rody, ktorych nie ma (albo wyeliminowane) - z ksiegi
                _drop.Clear();
                foreach (var key in _book.Keys) if (!_seen.Contains(key)) _drop.Add(key);
                for (int i = 0; i < _drop.Count; i++) _book.Remove(_drop[i]);
                if (_drop.Count > 0)
                {
                    var stale = new List<Clan>();
                    foreach (var kv in _byClan) if (kv.Key == null || kv.Key.StringId == null || !_book.ContainsKey(kv.Key.StringId)) stale.Add(kv.Key);
                    for (int i = 0; i < stale.Count; i++) _byClan.Remove(stale[i]);
                }
                LastModelIncomeSum = modelSum; LastRentSum = rentSum;
                LastTicksModel = tModel; LastOwnCalls = own; LastTicksCmp = tCmp; LastCmpN = cmpN; LastCmpNe = cmpNe; LastCmpDiff = cDiff; LastCmpAbs = cAbs;   // 169b
                _modelSeen.Clear(); _modelSeenDay = -1;     // liczby tej doby zuzyte
                LastTicksReport = 0; LastTicksCsv = 0;
                var s = Settings.Current;
                if (s == null || !s.LogEnabled) return;     // D11: przy wylaczonym logu tylko pierscien D
                if (_importN >= 0) { Log.Info("Budzet rodow: wczytano " + _importN + " rodow (bledne " + _importBad + ")."); _importN = -1; }
                long tr = Stopwatch.GetTimestamp(); _ticksCsv = 0;
                Report(s);
                LastTicksCsv = _ticksCsv; LastTicksReport = Stopwatch.GetTimestamp() - tr - _ticksCsv;
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                if (ran)
                {
                    LastRefund = DayRefund; LastCrown = DayCrown; LastEvtNone = DayEvtNone; LastEvtSettl = DayEvtSettl; LastEvtOther = DayEvtOther; LastThird = DayThird;
                }
                else
                {
                    LastRefund = LastCrown = LastEvtNone = LastEvtSettl = LastEvtOther = LastThird = LastModelIncomeSum = LastRentSum = -1;
                }
                DayRefund = DayCrown = DayEvtNone = DayEvtSettl = DayEvtOther = DayThird = 0;
                DayEstates = 0; _cutToday.Clear(); _cutBlind.Clear();   // 169c
                LastTicks = Stopwatch.GetTimestamp() - t0;
            }
        }

        private static bool AtWar(Clan c)
        {
            var f = c.MapFaction;
            if (f == null) return false;
            bool w;
            if (_war.TryGetValue(f, out w)) return w;
            w = false;
            var list = f.FactionsAtWarWith;
            if (list != null) for (int i = 0; i < list.Count; i++) { var o = list[i]; if (o != null && o.IsKingdomFaction) { w = true; break; } }
            _war[f] = w;
            return w;
        }

        private static double Pctl(List<double> sorted, double q)
        {
            if (sorted.Count == 0) return 0;
            int i = (int)Math.Floor(q * (sorted.Count - 1));
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, i))];
        }

        private static string N0(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "-" : Math.Round(v).ToString("0", CultureInfo.InvariantCulture); }
        private static readonly char[] CsvBad = { ';', '\n', '\r' };
        /// <summary>Pole CSV bez separatora i konca linii (169b: bez kopiowania, gdy nie ma czego zamieniac - ten sam wynik).</summary>
        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : (s.IndexOfAny(CsvBad) < 0 ? s : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' ')); }

        // 169b: nazwa krolestwa raz na raport (ten sam obiekt i ta sama chwila - ten sam napis); rody - nazwa raz na rod
        private static readonly Dictionary<Kingdom, string> _kName = new Dictionary<Kingdom, string>();
        private static string KingdomName(Kingdom k)
        {
            if (k == null || k.Name == null) return "";
            string n;
            if (_kName.TryGetValue(k, out n)) return n;
            n = Clean(k.Name.ToString());
            _kName[k] = n;
            return n;
        }

        private struct Top { public string Name; public double V; }

        /// <summary>Budzet (rozdz. 8 i 6.1 projektu) i dlugi (8.2-8.3) na sucho, linie 6.9-6.10 i CSV. Tylko odczyt.</summary>
        private static void Report(Settings s)
        {
            int day = (int)CampaignTime.Now.ToDays - 1, gameDay = (int)CampaignTime.Now.ToDays;
            _war.Clear(); _kName.Clear();
            try { IronBank.DefaultedKingdoms(_defaulted, _byId); } catch (Exception e) { _defaulted.Clear(); Stumble("Report(bankruci)", e); }
            // linia 6.9
            int ai = 0, peace = 0, war = 0, mercMinor = 0, noTick = 0, ringFull = 0; long ringDays = 0;
            var dList = new List<double>(); double dSum = 0;
            int saldoPlusN = 0, saldoMinusN = 0; long saldoPlus = 0, saldoMinus = 0;
            double ceilSum = 0; long wageSum = 0;
            int overN = 0, over110 = 0, over3 = 0, overOnCeil = 0, onCeilN = 0, belowR = 0, releasedClans = 0;
            double overSum = 0; long releasedMen = 0;
            int poorHeads = 0, poorTown = 0, poorCastle = 0, poorNone = 0, poorFam = 0;
            var tops = new List<Top>();
            // linia 6.10
            int tCredit = 0, tArrear = 0, tSeize = 0, tSale = 0, seizeVillages = 0;
            long seizeRent = 0, seizePurse = 0;
            var payDays = new List<double>(); double payMax = -1; string payMaxName = "-";
            long bankAll = 0; int bankN = 0; long crownDebt = 0; int crownN = 0;
            int candWar = 0, candRansom = 0, blkArrear = 0, blkCrown = 0, blkNoSurplus = 0; double candWarLimit = 0;
            int over15N = 0; double over15Sum = 0, limitBank = 0, limit82 = 0;
            int r1a = 0, r1b = 0, r1c = 0, r1d = 0, r1e = 0;
            var inv = CultureInfo.InvariantCulture;
            var csv = new StringBuilder(_today.Count * 300);
            bool sdOn = StableDOn;
            var aiList = new List<Clan>();   // 169c: rody AI do linii "D staly"
            for (int idx = 0; idx < _today.Count; idx++)
            {
                var c = _today[idx];
                if (IsUndeadClan(c)) continue;   // 169c (K40): Inni poza linia i CSV - kiesa zawsze 0
                int rowStart = csv.Length;
                try
                {
                    var r = Of(c);
                    if (r == null) continue;
                    string cname = c.Name != null ? c.Name.ToString() : null;   // 169b: raz na rod (CSV, najwiekszy nadmiar, prognoza splaty)
                    bool player = c == Clan.PlayerClan;
                    bool courtier = c.StringId.StartsWith("bk_courtiers_", StringComparison.Ordinal);
                    bool isAi = !player && !courtier;
                    bool atWar = AtWar(c);
                    int towns = 0, castles = 0, garMen = 0;
                    var fiefs = c.Fiefs;
                    if (fiefs != null)
                        for (int i = 0; i < fiefs.Count; i++)
                        {
                            var f = fiefs[i];
                            if (f == null) continue;
                            if (f.IsCastle) castles++; else if (f.IsTown) towns++;
                            var gp = f.GarrisonParty;
                            if (gp != null && gp.MemberRoster != null) garMen += gp.MemberRoster.TotalRegulars;
                        }
                    int partyMen = 0, partyLimit = 0;
                    var wps = c.WarPartyComponents;
                    if (wps != null)
                        for (int i = 0; i < wps.Count; i++)
                        {
                            var mp = wps[i] != null ? wps[i].MobileParty : null;
                            if (mp == null || !mp.IsActive) continue;
                            if (mp.MemberRoster != null) partyMen += mp.MemberRoster.TotalRegulars;
                            if (mp.Party != null) partyLimit += mp.Party.PartySizeLimit;
                        }
                    string kind = player ? "gracz" : c.IsUnderMercenaryService ? "najemnik" : towns > 0 ? "miasto" : castles > 0 ? "zamek" : "bez lenna";
                    double D = r.D, G = r.G;
                    int leaderGold = c.Leader != null ? c.Leader.Gold : 0;
                    long wage = r.HadTick ? (long)r.WageLastLord + r.WageLastGar : -1;
                    // 166: jedna formula pulapu dla logu i gry (ClanBudget.Ceiling - udzialy z Settings, R z podlogami rodziny); rod z budzetem w grze - ta sama
                    // liczba, ktora dzis stosuje gra (na D stalym); bez budzetu - formula na D169 (przebieg na sucho jak dotad)
                    double R, fq, chestQ;
                    double ceiling = ClanBudget.Ceiling(D, G, atWar, ClanBudget.AdultsOf(c), out R, out fq, out chestQ);
                    double capGame; if (ClanBudget.TryCap(c, out capGame)) ceiling = capGame;
                    bool onCeil = partyLimit > 0 && partyMen >= CeilingFill * partyLimit;
                    long released = 0;
                    bool budget = isAi && r.HadTick;
                    if (budget)
                    {
                        bool o110 = wage > BudgetHysteresis * ceiling;
                        r.Streak = o110 ? r.Streak + 1 : 0;
                        if (r.Streak >= BudgetHysteresisDays && wage > ceiling)
                        {
                            double perMan = (double)wage / Math.Max(1, partyMen + garMen);
                            released = Math.Max(1, (long)Math.Ceiling(ReleasePerDay * (wage - ceiling) / Math.Max(1e-6, perMan)));
                        }
                    }
                    // dlugi (tylko odczyt Banku i gry)
                    double principal; int missed; bool defaulted; int dueDay;
                    bool hasDebt = false;
                    try { hasDebt = IronBank.TryGetDebt(c, out principal, out missed, out defaulted, out dueDay); }
                    catch { principal = 0; missed = 0; defaulted = false; dueDay = 0; }
                    int rentV; PopulationLaw.RentVillageToday.TryGetValue(c, out rentV);
                    string tier = "-";
                    if (hasDebt)
                    {
                        bankAll += (long)principal; bankN++;
                        if (missed >= 3 || defaulted) tier = principal / Math.Max(1, rentV) > 364 ? "wyprzedaz" : "zajecie";
                        else if (missed >= 1) tier = "zaleglosc";
                        else tier = "kredyt";
                    }
                    int debtCrown = c.DebtToKingdom;
                    if (debtCrown > 0) { crownDebt += debtCrown; crownN++; }
                    string cand = "";
                    double lim82 = Math.Max(0, IronBankIncomeDays82 * D - principal);
                    if (isAi)
                    {
                        // statystyki linii 6.9
                        ai++;
                        aiList.Add(c);
                        if (atWar) war++; else peace++;
                        if (c.IsUnderMercenaryService || c.IsMinorFaction) mercMinor++;
                        if (!r.HadTick) noTick++;
                        if (r.Filled >= Days) ringFull++;
                        ringDays += r.Filled;
                        dList.Add(D); dSum += D;
                        if (r.SaldoSeen) { if (r.SaldoLast > 0) { saldoPlusN++; saldoPlus += r.SaldoLast; } else if (r.SaldoLast < 0) { saldoMinusN++; saldoMinus += r.SaldoLast; } }
                        if (budget)
                        {
                            ceilSum += ceiling; wageSum += wage;
                            if (wage > ceiling)
                            {
                                overN++; overSum += wage - ceiling;
                                if (onCeil) overOnCeil++;
                                tops.Add(new Top { Name = cname ?? c.StringId, V = wage - ceiling });
                            }
                            if (wage > BudgetHysteresis * ceiling) over110++;
                            if (r.Streak >= BudgetHysteresisDays) over3++;
                            if (released > 0) { releasedMen += released; releasedClans++; }
                            double days = G / Math.Max(1, wage);
                            if (days < 10) r1a++; else if (days < 20) r1b++; else if (days < 45) r1c++; else if (days < 90) r1d++; else r1e++;
                        }
                        if (onCeil) onCeilN++;
                        if (atWar && G < R) belowR++;
                        if (leaderGold < 5000) { poorHeads++; if (towns > 0) poorTown++; else if (castles > 0) poorCastle++; else poorNone++; }
                        if (G < 5000) poorFam++;
                        // dlugi (8.2-8.3) - tylko rody AI
                        if (hasDebt)
                        {
                            if (tier == "kredyt") tCredit++;
                            else if (tier == "zaleglosc") tArrear++;
                            else
                            {
                                if (tier == "wyprzedaz") tSale++; else tSeize++;
                                long seizeP = Math.Max(0, leaderGold - (long)Math.Max(SeizeFloor, SeizeWageDays * Math.Max(0, wage)));
                                seizeRent += rentV; seizePurse += seizeP;
                                var ss = c.Settlements;
                                if (ss != null) for (int i = 0; i < ss.Count; i++) if (ss[i] != null && ss[i].IsVillage) seizeVillages++;
                                double pd = principal / Math.Max(1, rentV);
                                payDays.Add(pd);
                                if (pd > payMax) { payMax = pd; payMaxName = cname ?? c.StringId; }
                            }
                            if (principal > IronBankIncomeDays82 * D) { over15N++; over15Sum += principal - IronBankIncomeDays82 * D; }
                        }
                        bool blocked = false;
                        if (hasDebt && (missed > 0 || defaulted)) { blkArrear++; blocked = true; }
                        if (debtCrown > 0) { blkCrown++; blocked = true; }
                        if (r.HadTick && D <= wage) { blkNoSurplus++; blocked = true; }
                        if (!blocked)
                        {
                            if (atWar && G < R && r.HadTick && wage > ceiling) { candWar++; candWarLimit += lim82; cand = "wojna"; }
                            if (c.Leader != null && c.Leader.IsPrisoner) { candRansom++; cand = cand.Length > 0 ? cand + "+okup" : "okup"; }
                        }
                        if (c.Kingdom != null)
                        {
                            // limit Banku na sucho - wzor jak IronBank.Limit / ClanIncomeBook (D21: bez wolania Limit)
                            float trust; IronBank.TryGetTrust(c, out trust);
                            float inc; IncomeToday.TryGetValue(c, out inc);
                            double lim = inc * Math.Max(0f, s.IronBankIncomeDays) + towns * s.IronBankPerTown + castles * s.IronBankPerCastle;
                            bool enemy = false;
                            foreach (var k in _defaulted) if (k != null && k != c.Kingdom && c.Kingdom.IsAtWarWith(k)) { enemy = true; break; }
                            if (enemy) lim *= 1.5;
                            limitBank += lim * trust;
                            limit82 += IronBankIncomeDays82 * Math.Max(0, D);
                        }
                    }
                    // wiersz CSV (wzor PeopleLedger: wiersz z bledem nie trafia do pliku)
                    bool bud = budget;
                    csv.Append(day).Append(';').Append(gameDay).Append(';').Append(Clean(c.StringId)).Append(';').Append(Clean(cname ?? "")).Append(';')
                       .Append(KingdomName(c.Kingdom)).Append(';').Append(kind).Append(';').Append(atWar ? 1 : 0).Append(';')
                       .Append(leaderGold).Append(';').Append(N0(G)).Append(';').Append(N0(D)).Append(';').Append(r.Filled).Append(';').Append(r.Inflow).Append(';').Append(r.A).Append(';')
                       .Append(r.SaldoSeen ? r.SaldoLast.ToString(inv) : "").Append(';').Append(r.B).Append(';').Append(r.Refund).Append(';').Append(r.Crown).Append(';')
                       .Append(r.Evt).Append(';').Append(r.Third).Append(';')
                       .Append(bud ? N0(ceiling) : "").Append(';')
                       .Append(r.HadTick && !player ? r.WageLastLord.ToString(inv) : "").Append(';').Append(r.HadTick && !player ? r.WageLastGar.ToString(inv) : "").Append(';')
                       .Append(r.HadTick && !player ? r.WageLastCar.ToString(inv) : "").Append(';')
                       .Append(player ? "" : partyMen.ToString(inv)).Append(';').Append(player ? "" : garMen.ToString(inv)).Append(';').Append(player ? "" : partyLimit.ToString(inv)).Append(';')
                       .Append(player ? "" : (onCeil ? "1" : "0")).Append(';').Append(bud ? r.Streak.ToString(inv) : "").Append(';').Append(bud ? released.ToString(inv) : "").Append(';')
                       .Append(player ? "" : N0(R)).Append(';')
                       .Append(hasDebt ? N0(principal) : "").Append(';').Append(hasDebt ? missed.ToString(inv) : "").Append(';').Append(hasDebt ? (defaulted ? "1" : "0") : "").Append(';')
                       .Append(debtCrown).Append(';').Append(tier).Append(';').Append(cand).Append(';').Append(isAi ? N0(lim82) : "");
                    StableCsv(csv, c, leaderGold, sdOn);   // 169c: kolumny D stalego, zold przyciety, doby bankruta
                    csv.Append(Environment.NewLine);
                }
                catch (Exception e) { csv.Length = rowStart; Stumble("Report(rod)", e); }
            }
            dList.Sort(); payDays.Sort();
            tops.Sort((a, b) => b.V.CompareTo(a.V));
            var topTxt = new List<string>();
            for (int i = 0; i < tops.Count && i < 3; i++) topTxt.Add(tops[i].Name + " " + N0(tops[i].V));
            string path = null;
            long tcsv = Stopwatch.GetTimestamp();
            try { path = Log.Csv("budzet-rodow.csv", CsvHeader + CsvHeaderStable, csv.ToString()); } catch (Exception e) { Stumble("Report(csv)", e); }
            _ticksCsv = Stopwatch.GetTimestamp() - tcsv;
            if (path != null && path != _csvPath) { _csvPath = path; Log.Info("Budzet rodow: plik CSV " + path + "."); }
            var sb = new StringBuilder(2048);
            sb.Append("Budzet rodow (na sucho): dzien ").Append(day)
              .Append(" | rody AI ").Append(ai).Append(" (pokoj ").Append(peace).Append(", wojna ").Append(war).Append("; najemnicy i pomniejsze ").Append(mercMinor)
              .Append("; bez zmierzonego zoldu ").Append(noTick).Append(')')
              .Append(" | D - dochod staly 28 dob [P] (dochod brutto modelu przed zoldem + renty + wplywy spoza rodu): mediana ").Append(N0(Pctl(dList, 0.5)))
              .Append(", 10% ").Append(N0(Pctl(dList, 0.1))).Append(", 90% ").Append(N0(Pctl(dList, 0.9))).Append(", razem ").Append(N0(dSum))
              .Append("; dla porownania saldo modelu (po zoldzie, z rozliczen) dodatnie u ").Append(saldoPlusN).Append(" rodow, razem +").Append(saldoPlus)
              .Append(", ujemne u ").Append(saldoMinusN).Append(", razem ").Append(saldoMinus)
              .Append("; pierscien: pelny u ").Append(ringFull).Append(" rodow, srednio ").Append(ai > 0 ? Math.Round((double)ringDays / ai).ToString("0", inv) : "0")
              .Append(" zmierzonych dob z 28 (brakujace doby = G/60)")
              .Append("; dochod modelu liczony tu u ").Append(LastOwnCalls).Append(" rodow (po powinnosciach, clach, zwrocie zoldu i Banku tej doby - jak w 169)")
              .Append(", porownanie z liczba z chwili powinnosci (KingdomTreasury) u ").Append(LastCmpN).Append(" rodow: rozna u ").Append(LastCmpNe)
              .Append(", roznica ").Append(LastCmpDiff >= 0 ? "+" : "").Append(LastCmpDiff).Append(" (bezwzgl. ").Append(LastCmpAbs).Append(')')
              .Append(" | pulap zoldu wg planu budzetu razem ").Append(N0(ceilSum)).Append(" a zold naliczony (partie + zalogi) ").Append(wageSum)
              .Append(" (").Append(ceilSum > 0 ? (100.0 * wageSum / ceilSum).ToString("0", inv) + "%" : "-").Append(')')
              .Append(" | ponad pulapem ").Append(overN).Append(" rodow (ponad 1.10 x: ").Append(over110).Append(", od 3 dob: ").Append(over3).Append("), nadwyzka zoldu ")
              .Append(N0(overSum)).Append(", w tym na suficie partii ").Append(overOnCeil)
              .Append(" | zwolnionych by dzis (15% nadwyzki od 3. doby): ").Append(releasedMen).Append(" ludzi w ").Append(releasedClans).Append(" rodach")
              .Append(" | na suficie partii (90% limitu wielkosci): ").Append(onCeilN).Append(" rodow")
              .Append(" | ponizej rezerwy wojny R: ").Append(belowR).Append(" rodow")
              .Append(" | glowy < 5000: ").Append(poorHeads).Append(" (z miastem ").Append(poorTown).Append(", z zamkiem ").Append(poorCastle).Append(", bez lenna ").Append(poorNone)
              .Append("), rodziny < 5000: ").Append(poorFam).Append(" (bez dworzan BK i Innych - 169c)")
              .Append(" | najwiekszy nadmiar: ").Append(topTxt.Count > 0 ? string.Join(", ", topTxt.ToArray()) : "-")
              .Append(" | zalegly zold: ").Append(MoneyLedger.WageShortParties).Append(" partii i zalog z ").Append(MoneyLedger.WagePaidParties)
              .Append(" rozliczonych dzis (").Append(MoneyLedger.WagePaidParties > 0 ? (100.0 * MoneyLedger.WageShortParties / MoneyLedger.WagePaidParties).ToString("0.0", inv) : "0.0")
              .Append("%; cel planu: najwyzej 2%)")
              .Append(" | jeszcze nie liczone (przyszle paczki): dwor -, sprzet -, werbunek -, przelewy czlonek -> glowa -, zwolnieni do karczmy -, zwolnieni do wsi -")
              .Append("; majatki BK - (kod BK nieczynny bez Economy Overhaul)")
              .Append(" | plik: budzet-rodow.csv")
              .Append(" | kontrola: rody=").Append(ai).Append(" pokoj+wojna=").Append(peace + war).Append(" ponad<=rody ").Append(overN <= ai ? "TAK" : "NIE")
              .Append(_stumbles > 0 ? " (potkniecia " + _stumbles + ")" : "").Append('.');
            Log.Info(sb.ToString());
            sb.Length = 0;
            sb.Append("Dlugi (na sucho): dzien ").Append(day)
              .Append(" | szczeble dlugu wg planu (dzisiejszy stan Banku): kredyt ").Append(tCredit).Append(", zaleglosc ").Append(tArrear).Append(", zajecie ").Append(tSeize)
              .Append(", wyprzedaz ").Append(tSale)
              .Append(" | zajeloby dzis ").Append(seizeRent + seizePurse).Append(" zl (renty wsi ").Append(seizeRent).Append(" zl z ").Append(seizeVillages).Append(" wsi, kiesy ponad podloge ")
              .Append(seizePurse).Append(" zl; utarg wsi -)")
              .Append(" | prognoza splaty w zajeciu: mediana ").Append(payDays.Count > 0 ? N0(Pctl(payDays, 0.5)) : "-").Append(" dni, najdluzej ")
              .Append(payMax >= 0 ? N0(payMax) + " (" + payMaxName + ")" : "-")
              .Append(" | wierzyciele: Bank ").Append(bankAll).Append(", korona (dlug wobec korony z gry) ").Append(crownDebt).Append(" u ").Append(crownN).Append(" rodow, skarbce -, rody -")
              .Append(" | pozyczyliby wg planu: na wojne ").Append(candWar).Append(" (limit 15 x D razem ").Append(N0(candWarLimit)).Append("), na okup glowy ").Append(candRansom)
              .Append(", na trybut -; zablokowani: zaleglosc ").Append(blkArrear).Append(", dlug wobec korony ").Append(blkCrown).Append(", bez nadwyzki (D <= zold) ").Append(blkNoSurplus)
              .Append(" | Bank dzis a plan: dluznikow ").Append(bankN).Append(", dlug ").Append(bankAll).Append("; z dlugiem ponad 15 x D: ").Append(over15N)
              .Append(" (nadwyzka ").Append(N0(over15Sum)).Append("); limit Banku wg jego wzoru (dochod x dni z ustawien Banku + lenna, x zaufanie, x1.5 dla wrogow bankrutow) razem ")
              .Append(N0(limitBank)).Append(", wg planu (15 x D) razem ").Append(N0(limit82))
              .Append(" | zapas kiesy w dniach zoldu: ponizej 10: ").Append(r1a).Append(", 10-20: ").Append(r1b).Append(", 20-45: ").Append(r1c).Append(", 45-90: ").Append(r1d)
              .Append(", ponad 90: ").Append(r1e).Append('.');
            Log.Info(sb.ToString());
            if (sdOn) { try { Log.Info(StableLine(day, aiList)); } catch (Exception e) { Stumble("StableLine", e); } }   // 169c
        }

        private const string CsvHeader = "dzien;dzien_gry;rod_id;rod;krolestwo;rodzaj;wojna;kiesa_glowy;G;D;dni_pomiaru;wplyw_doby;dochod_modelu;saldo_modelu;renty;zwrot_korony;"
                                         + "mennica_monopole;zdarzenia;trzecia;pulap;zold_partii;zold_zalog;zold_karawan;ludzi_partie;ludzi_zalogi;limit_partii;na_suficie;dni_ponad;"
                                         + "zwolnieni_na_sucho;R;dlug_bank;spoznienia;bankrut;dlug_korona;szczebel;kandydat_pozyczki;limit_8_2";

        // ------------------------------------------------------------ zapis (SaveText - kawalki po 8000 znakow)
        /// <summary>"v1;" + rekordy rozdzielone ';', pola '|': StringId|Seed|Head|Filled|Streak|Today|WageLastLord|WageLastGar|WageLastCar|HadTick|r0,...,r27.</summary>
        internal static string Export()
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(_book.Count * 200 + 8);
                sb.Append("v1");
                foreach (var kv in _book)
                {
                    var r = kv.Value;
                    if (kv.Key == null || kv.Key.IndexOf('|') >= 0 || kv.Key.IndexOf(';') >= 0) continue;
                    sb.Append(';').Append(kv.Key).Append('|').Append(r.Seed.ToString(inv)).Append('|').Append(r.Head.ToString(inv)).Append('|').Append(r.Filled.ToString(inv))
                      .Append('|').Append(r.Streak.ToString(inv)).Append('|').Append(r.Today.ToString(inv)).Append('|').Append(r.WageLastLord.ToString(inv))
                      .Append('|').Append(r.WageLastGar.ToString(inv)).Append('|').Append(r.WageLastCar.ToString(inv)).Append('|').Append(r.HadTick ? '1' : '0').Append('|');
                    for (int i = 0; i < Days; i++) { if (i > 0) sb.Append(','); sb.Append(r.Ring[i].ToString(inv)); }
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        /// <summary>Pusty albo null = pusta ksiega (stary zapis). Zly rekord pominiety i policzony. Obiektow Clan tu NIE szukamy (lekcja 161).</summary>
        internal static void Import(string data)
        {
            _book.Clear(); _byClan.Clear(); _open = null; _openClan = null;
            _importN = 0; _importBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var inv = CultureInfo.InvariantCulture;
                var recs = data.Split(';');
                for (int n = 1; n < recs.Length; n++)
                {
                    try
                    {
                        var f = recs[n].Split('|');
                        if (f.Length < 11 || f[0].Length == 0) { _importBad++; continue; }
                        var r = new Rec();
                        r.Seed = int.Parse(f[1], NumberStyles.Integer, inv);
                        r.Head = int.Parse(f[2], NumberStyles.Integer, inv);
                        r.Filled = int.Parse(f[3], NumberStyles.Integer, inv);
                        r.Streak = int.Parse(f[4], NumberStyles.Integer, inv);
                        r.Today = long.Parse(f[5], NumberStyles.Integer, inv);
                        r.WageLastLord = int.Parse(f[6], NumberStyles.Integer, inv);
                        r.WageLastGar = int.Parse(f[7], NumberStyles.Integer, inv);
                        r.WageLastCar = int.Parse(f[8], NumberStyles.Integer, inv);
                        r.HadTick = f[9] == "1";
                        var ring = f[10].Split(',');
                        if (ring.Length != Days || r.Head < 0 || r.Head >= Days || r.Filled < 0 || r.Filled > Days) { _importBad++; continue; }
                        for (int i = 0; i < Days; i++) r.Ring[i] = int.Parse(ring[i], NumberStyles.Integer, inv);
                        _book[f[0]] = r;
                        _importN++;
                    }
                    catch { _importBad++; }
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
