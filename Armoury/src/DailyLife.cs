using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// ZC0 (projekt docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md rozdz. 14 "ZC0 pomiar na sucho") - ZYCIE CODZIENNE: STANY, KOSZYK DNIA W KG, SPIZARNIA, DOSTATEK.
    /// TYLKO POMIAR: gra zachowuje sie dokladnie jak dzis - zadne zloto, towar, cena, dobrobyt, ludzie ani decyzja AI nie sa ruszane, zadna latka gry;
    /// liczby w pamieci sesji i w logu. Stan w zapisie (arm_dailylife, SaveText.Sync - kawalki ponizej limitu napisu): skala targu sigma - ustalona RAZ, przy
    /// pierwszym wczytaniu z kluczem DailyLifeMeasure (z DailyLifeMarketScale, domyslnie 37), potem nie ruszana w kampanii (zmiana w MCM - dopisek w linii startu
    /// sesji) - oraz srednia biezaca dochodu stanow kazdej osady (7 liczb i dni sredniej, ok. 60 KB), zeby wczytany zapis dawal te same Dostatki (prog wspolny
    /// testow rozdz. 14). Zapis bez sredniej (v1) albo bez klucza - srednia od nowa, linie pisza ROZGRZEWKA, dopoki polowa osad nie ma 7 dob.
    ///  - stany kazdej osady z ksiegi ludzi (DailyLife.Estates.cs): pasma wedlug kultury i rodzaju osady, pochylenie klasami BK, konto NIEWOLNI ksiegi,
    ///    wielkie rody z bohaterow 1:1 (x3); suma stanow ksiegi = ksiega (z budowy); ksiega bez konta niewolnych wobec linii "Ludnosc" (sprawdz_logi NZC0-2);
    ///  - koszyk w kg na stan x kraine i koszyk przetrwania po cenach lokalnych (DailyLife.Basket.cs);
    ///  - Dostatek (Livelihood) i dochod na glowe na stan, osade, region, krolestwo i swiat (DailyLife.Livelihood.cs);
    ///  - Spizarnia: potrzeba jedzenia w kg wedlug stanu i krainy - z pola i domu (x czesc najedzona ze spichlerza) i z targu - wobec towaru, ktory lud
    ///    zjadl dzis z polek (ksiega towarow: mieszczanie i wsie x sigma, dwory 1:1); brak kg.
    /// Linie (doby pelnych pomiarow - ciche logi NW co N dob w grze, w autotescie codziennie): "Zycie (ZC0): dzien N", "Spizarnia (ZC): dzien N"; raz na
    /// 7 dob (autotest: doba podzielna przez 7; ciche logi: pierwsza doba linii w nowym tygodniu; nie w rozgrzewce) linia "Zycie (ZC0) - krolestwa" i plik
    /// stany-dostatek.csv w katalogu sesji (wiersz na osade). Srednia dochodu (spis, stany, ceny, dochod) biegnie co dobe; Dostatek i Spizarnia - tylko w dobie
    /// linii; log wylaczony (LogEnabled) - nic (srednia stoi). Srednia: zwykla przez pierwsze 28 dob osady, potem wykladnicza z waga 1/28.
    /// Koszt: ok. 800 osad x 7 stanow x 13 grup w tablicach, ceny miast raz na dobe; czas bloku w linii bez zapisu linii (cel projektu: mediana <= 5 ms na dobe).
    /// Wyjatki liczone per miejsce (pierwszy do pliku) - bez globalnych wylacznikow (CLAUDE.md 7). Klucz wylaczony - nic (linia raz).
    /// ZC2 (klucz DailyLifeEstates, DailyLife.Estates.cs i DailyLife.View.cs): udzialy stanow z ksiegi stanow w zapisie (arm_estates; zasiew z pasm x BK),
    /// Dostatek liczony KAZDEJ doby (takze bez linii i przy wylaczonym logu - czyta go widok w grze: dymki osad, People of this land, Look over the district,
    /// Realm ledger), pamiec Dostatku co 7 dob, linia "Stany (ZC2): dzien N" w dobie linii. Bez klucza - jak ZC0.
    /// </summary>
    internal static partial class DailyLife
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string P = "Zycie (ZC0): ";
        private const string PS = "Spizarnia (ZC): ";
        private const string Version = "v2";       // v2|sigma|dzien|id:dni:y0,...,y6;... (v1 = sama sigma - czytany)
        private const int SigmaMin = 28, SigmaMax = 73;
        private const int WarmDays = 7;            // rozgrzewka sredniej: mniej niz 7 dob - D wstepne, bez pliku i linii krolestw
        private const string CsvName = "stany-dostatek.csv";
        private const string CsvHeader = "dzien;osada_id;osada;rodzaj;krolestwo;kultura;profil;region_id;ludzie_ksiega;niewolni;chlopi;biedota;rzemieslnicy;kupcy;szlachta;wielkie_rody;"
                                         + "konto_niewolnych;D;zrodlo_D;D_bez_rodow;D_niewolni;D_chlopi;D_biedota;D_rzemieslnicy;D_kupcy;D_szlachta;D_rody;dochod_na_glowe_zl_rok;koszyk_przetrwania_zl_d;"
                                         + "najedzeni;dni_sredniej;uwagi";

        // ------------------------------------------------------------ stan kampanii (zapis arm_dailylife)
        private static int _sigma = -1;            // -1 = jeszcze nie ustalona (klucz wylaczony od poczatku albo zapis sprzed paczki przed pierwsza doba)
        private static int _sigmaDay = -1;         // doba ustalenia
        private static bool _sigmaSaved;           // sigma przyszla z zapisu
        private static string _loadNote;           // dopisek wczytania (zly format)
        private static Dictionary<string, double[]> _saved;   // srednia z zapisu: id osady -> [YAvg 0..6, dni] - do pierwszego spisu osad (potem null)
        private static int _savedN, _savedBad, _restoredN;

        // ------------------------------------------------------------ pamiec sesji
        private static int _stumbles, _stumblesDay;
        private static int _noteStumbles;          // NoteVillageReturn / NoteVillageLevy (sluchacze poza doba) - do potkniec na poczatku doby
        private static Exception _noteErr;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _offLogged, _waitLogged;
        private static int _csvDay = int.MinValue, _days;
        private const int MsKeep = 364;            // czas bloku: ostatnie 364 doby (pierscien)
        private static readonly double[] _ms = new double[MsKeep];
        private static readonly double[] _msTmp = new double[MsKeep];
        private static int _msN, _msPos;
        private static readonly object WinGate = new object();
        private static Dictionary<Settlement, double[]> _win = new Dictionary<Settlement, double[]>();   // wies -> [do kiesy wsi, podatek pana, majatki BK, powroty, danina wojenna]

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.DailyLifeMeasure; } }

        /// <summary>Z konstruktora ArmouryBehavior: nowa sesja (zapis wczytuje Import po konstruktorze).</summary>
        internal static void Reset()
        {
            _sigma = -1; _sigmaDay = -1; _sigmaSaved = false; _loadNote = null;
            _saved = null; _savedN = _savedBad = _restoredN = 0;
            _stumbles = _stumblesDay = 0; _err.Clear();
            Interlocked.Exchange(ref _noteStumbles, 0); Interlocked.Exchange(ref _noteErr, null);
            _offLogged = _waitLogged = false; _csvDay = int.MinValue; _days = 0; _msN = _msPos = 0;
            lock (WinGate) { _win = new Dictionary<Settlement, double[]>(); }
            _winTaken = null;
            _recs = null; _byS.Clear(); _byHold.Clear(); _kName.Clear(); _cacheSig = -1; _cacheDay = int.MinValue;
            _items = null; _pxBase = null; _usual = null; _pxWorld = null; _basket.Clear(); _kgOf.Clear(); _civil.Clear();
            _reg.Clear(); _idxMemo.Clear(); _king.Clear(); _kingName.Clear(); _thrHold.Clear(); _wv.Clear();
            ResetEstates();                                // ZC2: ksiega stanow, pamiec Dostatku, liczniki widoku (zapis wczytuje ImportEstates po konstruktorze)
        }

        /// <summary>Potkniecie w miejscu where: liczone zawsze, pierwsze w danym miejscu do pliku (wyjatek albo opis detail). Bez wylacznika.</summary>
        private static void Stumble(string where, Exception e, string detail = null)
        {
            _stumbles++; _stumblesDay++;
            try
            {
                if (_err.Add(where))
                {
                    if (e != null) Log.Error("DailyLife." + where, e);
                    else Log.Info(P + "potkniecie - " + where + (detail != null ? " (" + detail + ")" : "") + ".");
                }
            }
            catch { }
        }

        /// <summary>Sluchacze poza doba (NoteVillageReturn, NoteVillageLevy) licza potkniecia atomowo - na poczatku doby do licznikow doby i sesji.</summary>
        private static void FoldNoteStumbles()
        {
            int ns = Interlocked.Exchange(ref _noteStumbles, 0);
            if (ns <= 0) return;
            var ne = Interlocked.Exchange(ref _noteErr, null);
            _stumbles += ns - 1; _stumblesDay += ns - 1;
            Stumble("NoteVillage", ne, ne == null ? ns + " w sluchaczu" : null);
        }

        private static void NoteFail(Exception e)
        {
            Interlocked.Increment(ref _noteStumbles);
            Interlocked.CompareExchange(ref _noteErr, e, null);
        }

        private static int SigmaFromMcm()
        {
            var s = Settings.Current;
            int v = s != null ? s.DailyLifeMarketScale : 37;
            return Math.Max(SigmaMin, Math.Min(SigmaMax, v));
        }

        /// <summary>Sigma ustalona raz na kampanie (z zapisu albo z MCM przy pierwszym wczytaniu z kluczem).</summary>
        private static void EnsureSigma(int day)
        {
            if (_sigma > 0) return;
            _sigma = SigmaFromMcm(); _sigmaDay = day; _sigmaSaved = false;
        }

        private static string SigmaText()
        {
            if (_sigma <= 0) return "nie ustalona";
            int mcm = SigmaFromMcm();
            return _sigma + (_sigmaSaved ? " (z zapisu, ustalona dnia " + _sigmaDay + ")" : " (ustalona dnia " + _sigmaDay + " z MCM - do zapisu)")
                   + (mcm != _sigma ? " - MCM ma " + mcm + ", zostaje " + _sigma + " (skala nie zmienia sie w kampanii)" : "");
        }

        // ------------------------------------------------------------ zapis
        private static bool SafeId(string id) { return !string.IsNullOrEmpty(id) && id.IndexOfAny(IdBad) < 0; }
        private static readonly char[] IdBad = { ':', ';', ',', '|' };

        private static void AppendAvg(StringBuilder sb, string id, double[] y, int days, ref int n)
        {
            if (n > 0) sb.Append(';');
            sb.Append(id).Append(':').Append(days.ToString(Inv)).Append(':');
            for (int e = 0; e < Estates; e++)
            {
                if (e > 0) sb.Append(',');
                double v = y[e];
                sb.Append(v != 0 && !double.IsNaN(v) && !double.IsInfinity(v) ? v.ToString("G9", Inv) : "0");
            }
            n++;
        }

        internal static string Export()
        {
            if (_sigma <= 0) return "";
            var sb = new StringBuilder(64 * 1024);
            sb.Append(Version).Append('|').Append(_sigma.ToString(Inv)).Append('|').Append(_sigmaDay.ToString(Inv)).Append('|');
            int n = 0;
            try
            {
                if (_recs != null)
                {
                    foreach (var r in _recs) if (r.YDays > 0 && SafeId(r.Id)) AppendAvg(sb, r.Id, r.YAvg, r.YDays, ref n);
                }
                else if (_saved != null)
                {
                    foreach (var kv in _saved) if (SafeId(kv.Key)) AppendAvg(sb, kv.Key, kv.Value, (int)kv.Value[Estates], ref n);   // wczytane, jeszcze bez spisu (klucz wylaczony, przed kalibracja)
                }
            }
            catch (Exception e) { Stumble("Export", e); }
            return sb.ToString();
        }

        internal static void Import(string data)
        {
            _sigma = -1; _sigmaDay = -1; _sigmaSaved = false; _loadNote = null; _saved = null; _savedN = _savedBad = 0;
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                var a = data.Split(new[] { '|' }, 4);
                int sg, d;
                bool v1 = a.Length == 3 && a[0] == "v1", v2 = a.Length == 4 && a[0] == Version;
                if (!(v1 || v2) || !int.TryParse(a[1], NumberStyles.Integer, Inv, out sg) || !int.TryParse(a[2], NumberStyles.Integer, Inv, out d) || sg < SigmaMin || sg > SigmaMax)
                { _loadNote = "zapis arm_dailylife w nieznanym formacie - pominiety (sigma od nowa z MCM)"; return; }
                _sigma = sg; _sigmaDay = d; _sigmaSaved = true;
                if (v2) ImportAvg(a[3]);
                else _loadNote = "zapis arm_dailylife v1 (bez sredniej dochodu) - srednia od nowa";
            }
            catch (Exception e) { _sigma = -1; _saved = null; _loadNote = "blad odczytu arm_dailylife - sigma od nowa z MCM"; Stumble("Import", e); }
        }

        private static void ImportAvg(string s)
        {
            var map = new Dictionary<string, double[]>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(s))
                foreach (var ent in s.Split(';'))
                {
                    if (ent.Length == 0) continue;
                    var p = ent.Split(':');
                    double[] v = null;
                    int days;
                    if (p.Length == 3 && p[0].Length > 0 && int.TryParse(p[1], NumberStyles.Integer, Inv, out days) && days > 0 && days <= AvgDays)
                    {
                        var ys = p[2].Split(',');
                        if (ys.Length == Estates)
                        {
                            v = new double[Estates + 1];
                            for (int e = 0; e < Estates; e++)
                            {
                                double x;
                                if (!double.TryParse(ys[e], NumberStyles.Float, Inv, out x) || double.IsNaN(x) || double.IsInfinity(x)) { v = null; break; }
                                v[e] = x;
                            }
                            if (v != null) v[Estates] = days;
                        }
                    }
                    if (v == null) { _savedBad++; continue; }
                    map[p[0]] = v;
                }
            _saved = map; _savedN = map.Count;
        }

        /// <summary>OnSessionLaunched: linia startu sesji - klucze, sigma, srednia z zapisu.</summary>
        internal static void SessionStart()
        {
            try
            {
                if (KeyOn) EnsureSigma((int)CampaignTime.Now.ToDays);
                var s = Settings.Current;
                Log.Info(P + "start sesji | klucze: Daily Life Measure " + (KeyOn ? "tak" : "WYLACZONE") + ", Daily Life Market Scale " + (s != null ? s.DailyLifeMarketScale : 37)
                         + " | sigma " + SigmaText() + (_loadNote != null ? " | " + _loadNote : "")
                         + " | srednia dochodu z zapisu: " + _savedN + " osad" + (_savedBad > 0 ? " (pominiete wpisy " + _savedBad + ")" : "")
                         + " | pomiar na sucho - gra bez zmian (zloto, towar, ceny, dobrobyt, AI) | linie: Zycie (ZC0), Spizarnia (ZC), co 7 dob krolestwa i " + CsvName
                         + " (doby pelnych pomiarow - ciche logi NW: w grze co " + LogGate.Every + " dob, w autotescie codziennie; srednia dochodu codziennie)"
                         + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ------------------------------------------------------------ wejscia z MoneyLedger (powrot taboru wsi) i KingdomTreasury (danina wojenna wsi) - tylko liczniki
        /// <summary>MoneyLedger.OnAfterEntered (tabor wraca do wsi z utargiem): ile weszlo do kiesy wsi, podatek pana, majatki BK. Tylko licznik - nie rzuca.</summary>
        internal static void NoteVillageReturn(Settlement home, int kept, int tax, int estates)
        {
            try
            {
                if (home == null || !home.IsVillage || !KeyOn) return;
                lock (WinGate)
                {
                    double[] w;
                    if (!_win.TryGetValue(home, out w)) { w = new double[5]; _win[home] = w; }
                    w[0] += kept; w[1] += tax; w[2] += estates; w[3] += 1;
                }
            }
            catch (Exception e) { NoteFail(e); }
        }

        /// <summary>KingdomTreasury.Levies (danina wojenna z oszczednosci wsi ponad 15 dni zakupow, R15a): ile zeszlo z kiesy wsi. Tylko licznik - nie rzuca.</summary>
        internal static void NoteVillageLevy(Settlement st, int x)
        {
            try
            {
                if (st == null || !st.IsVillage || x <= 0 || !KeyOn) return;
                lock (WinGate)
                {
                    double[] w;
                    if (!_win.TryGetValue(st, out w)) { w = new double[5]; _win[st] = w; }
                    w[4] += x;
                }
            }
            catch (Exception e) { NoteFail(e); }
        }

        private static Dictionary<Settlement, double[]> TakeWindows()
        {
            lock (WinGate) { var w = _win; _win = new Dictionary<Settlement, double[]>(); return w; }
        }

        // ------------------------------------------------------------ doba
        /// <summary>Z OnDailyTick (po TownFolk.Daily, KingdomTreasury.Levies, GoodsLedger.Daily, Famine.Daily, PopulationLaw.Daily i ClanIncomeBook.Daily) -
        /// pomiar doby wczorajszej.</summary>
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays - 1;
            _stumblesDay = 0;
            FoldNoteStumbles();
            try
            {
                if (!KeyOn)
                {
                    TakeWindows();
                    if (!_offLogged) { _offLogged = true; Log.Info(P + "dzien " + day + " | WYLACZONY (Daily Life Measure = off) - bez pomiaru, gra jak dotad."); }
                    return;
                }
                _offLogged = false;
                if (Campaign.Current == null) return;
                var s = Settings.Current;
                // log wylaczony: nikt nie przeczyta - bez liczenia (srednia stoi); ZC2 (DailyLifeEstates): widok w grze czyta liczby - liczenie bez linii
                if (s == null || (!s.LogEnabled && !EstatesOn)) { TakeWindows(); return; }
                if (!PopulationLaw.Calibrated)
                {
                    TakeWindows();
                    if (!_waitLogged) { _waitLogged = true; Log.Info(P + "dzien " + day + " | czeka na kalibracje ksiegi ludzi (PopulationLaw) - pomiar od pierwszej doby po kalibracji."); }
                    return;
                }
                EnsureSigma(day);
                Compute(day, t0);
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static void AddMs(double ms)
        {
            _ms[_msPos] = ms; _msPos = (_msPos + 1) % MsKeep;
            if (_msN < MsKeep) _msN++;
        }

        private static double MedianMs()
        {
            if (_msN <= 0) return double.NaN;
            Array.Copy(_ms, _msTmp, _msN);
            Array.Sort(_msTmp, 0, _msN);
            return _msN % 2 == 1 ? _msTmp[_msN / 2] : 0.5 * (_msTmp[_msN / 2 - 1] + _msTmp[_msN / 2]);
        }

        private static void Compute(int day, long t0)
        {
            EnsureItems();
            EnsureRecs(day);
            _winTaken = TakeWindows();
            _eDay = day; _eSeedDay = 0;                    // ZC2: doba zasiewu ksiegi stanow
            long t1 = Stopwatch.GetTimestamp();
            PeopleDay();
            long t2 = Stopwatch.GetTimestamp();
            PricesDay();                                   // po ludziach doby: koszyk zamku wazony ludzmi wsi regionu
            long t3 = Stopwatch.GetTimestamp();
            IncomeDay();                                   // srednia biezaca dochodu - kazdej doby
            _winTaken = null;
            _days++;
            double f = 1000.0 / Stopwatch.Frequency;
            var st = Settings.Current;
            bool estates = EstatesOn;
            if (!LogGate.Lines || st == null || !st.LogEnabled)
            {
                // doba bez linii (ciche logi, log wylaczony): bez Spizarni i linii; ZC2 - Dostatek do widoku co dobe (i pamiec Dostatku co 7 dob)
                if (estates) { try { LivelihoodDay(); ViewDay(day); } catch (Exception e) { Stumble("LivelihoodDay(widok)", e); } }
                AddMs((Stopwatch.GetTimestamp() - t0) * f);
                return;
            }
            LivelihoodDay();
            if (estates) { try { ViewDay(day); } catch (Exception e) { Stumble("ViewDay", e); } }
            long t4 = Stopwatch.GetTimestamp();
            PantryDay();
            long t5 = Stopwatch.GetTimestamp();
            // plik i linia krolestw raz na 7 dob: przy liniach co dobe - doba podzielna przez 7 (ten sam rytm po wczytaniu), przy cichych logach - pierwsza doba linii
            // w nowym tygodniu; nie w rozgrzewce sredniej
            bool daily = !LogGate.On || LogGate.Every <= 1;
            bool week = !_warm && (daily ? day % 7 == 0 : (_csvDay == int.MinValue || day / 7 != _csvDay / 7));
            string csvNote = "";
            if (week)
            {
                _csvDay = day;
                try { csvNote = WriteCsv(day); } catch (Exception e) { Stumble("Csv", e); csvNote = CsvName + " NIE ZAPISANY (blad)"; }
            }
            string king = null, pantry = null;
            if (week) { try { king = KingdomLine(day); } catch (Exception e) { Stumble("KingdomLine", e); } }
            try { pantry = PantryLine(day); } catch (Exception e) { Stumble("PantryLine", e); }
            long t6 = Stopwatch.GetTimestamp();
            double ms = (t6 - t0) * f;                     // bez zapisu linii do pliku (Log.Info nizej)
            AddMs(ms);
            if (king != null) Log.Info(king);
            try { Log.Info(LifeLine(day, ms, (t1 - t0) * f, (t3 - t2) * f, (t2 - t1) * f, (t4 - t3) * f, (t5 - t4) * f, (t6 - t5) * f, week ? csvNote : null)); }
            catch (Exception e) { Stumble("LifeLine", e); }
            if (pantry != null) Log.Info(pantry);
            if (estates) { try { Log.Info(EstatesLine(day)); } catch (Exception e) { Stumble("EstatesLine", e); } }   // ZC2: linia "Stany (ZC2): dzien N"
        }

        // ------------------------------------------------------------ linie
        private static bool Fin(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        private static string N0(double x) { return !Fin(x) ? "-" : Math.Round(x).ToString("0", Inv); }
        private static string F1(double x) { return !Fin(x) ? "-" : x.ToString("0.0", Inv); }
        private static string F2(double x) { return !Fin(x) ? "-" : x.ToString("0.00", Inv); }
        private static string F3(double x) { return !Fin(x) ? "-" : x.ToString("0.000", Inv); }
        private static string Pc(double a, double b) { return b > 0 ? (100.0 * a / b).ToString("0.00", Inv) : "-"; }
        private static double Div(double a, double b) { return b > 0 ? a / b : double.NaN; }

        private static string LifeLine(int day, double ms, double tPrep, double tPrice, double tPeople, double tLive, double tPantry, double tLines, string csvNote)
        {
            var sb = new StringBuilder(3000);
            sb.Append(P).Append("dzien ").Append(day).Append(" | sigma ").Append(_sigma).Append(_sigmaSaved ? " (zapis)" : " (MCM, do zapisu)")
              .Append(" | ksiega ").Append(N0(_book)).Append(" (wsie ").Append(N0(_bookVil)).Append(", miasta ").Append(N0(_bookTown)).Append(", konto niewolnych ").Append(N0(_thrBook))
              .Append(") | stany ksiegi ").Append(N0(_estSum)).Append(" (roznica ").Append(F2(_estSum - _book)).Append(')')
              .Append(" | wielkie rody ").Append(N0(_houses)).Append(" (").Append(_houseHeroes).Append(" bohaterow x 3 w ").Append(_houseClans).Append(" rodach z lennem, ponad ksiege)");
            double all = _nWorld;
            sb.Append(" | swiat %:");
            for (int e = 0; e < Estates; e++) sb.Append(e == 0 ? " " : ", ").Append(EstateName[e]).Append(' ').Append(Pc(_nE[e], all));
            for (int w = 0; w < 2; w++)
            {
                double t = 0; for (int e = 0; e < Estates; e++) t += _nWE[w, e];
                sb.Append(w == 0 ? " | Westeros %:" : " | Essos %:");
                for (int e = 0; e < 6; e++) sb.Append(e == 0 ? " " : ", ").Append(EstateName[e]).Append(' ').Append(Pc(_nWE[w, e], t));
            }
            // Dostatek: srednia D osad wazona ludzmi (suma Y/K przez sume ludzi); dochod na glowe: suma Y przez sume ludzi (ludzie z liczbami dochodu)
            sb.Append(" | Dostatek swiat ").Append(F2(_dWorld)).Append(" (mediana chlopow wsi ").Append(F2(_medPeas)).Append(", Westeros ").Append(F2(Div(_ykW[0], _dnW[0])))
              .Append(", Essos ").Append(F2(Div(_ykW[1], _dnW[1]))).Append("), stany:");
            for (int e = 0; e < Estates; e++) sb.Append(e == 0 ? " " : ", ").Append(EstateName[e]).Append(' ').Append(F2(Div(_ykE[e], _dnE[e])));
            sb.Append(" | dochod na glowe ").Append(N0(Div(_yWorld, _dnWorld) * 364)).Append(" zl/rok (Westeros ").Append(N0(Div(_yW[0], _dnW[0]) * 364)).Append(", Essos ")
              .Append(N0(Div(_yW[1], _dnW[1]) * 364)).Append("), stany:");
            for (int e = 0; e < Estates; e++) sb.Append(e == 0 ? " " : ", ").Append(EstateName[e]).Append(' ').Append(N0(Div(_yE[e], _dnE[e]) * 364));
            sb.Append(" | koszyk przetrwania zl/d: miasta ").Append(F3(_medTownK)).Append(" (mediana), wsie ").Append(F3(_medVilK)).Append(" (mediana, u bramy)");
            int nAll = _recs != null ? _recs.Count : 0;
            sb.Append(" | D policzone ").Append(_dOwn + _dReg + _dKing + _dWorldN).Append('/').Append(nAll).Append(" osad (wlasne ").Append(_dOwn).Append(", regionu ").Append(_dReg)
              .Append(", krolestwa ").Append(_dKing).Append(", swiata ").Append(_dWorldN).Append(", bez D ").Append(_dNone).Append("; miasta bez liczb kiesy - bez wlasnego D ").Append(_dNoEarn).Append(")")
              .Append("; regiony ").Append(_regions).Append(": D min ").Append(F2(_regMin)).Append(" / mediana ").Append(F2(_regMed)).Append(" / max ").Append(F2(_regMax))
              .Append(" (bez wielkich rodow: mediana ").Append(F2(_regMed0)).Append(')');
            sb.Append(" | wejscia doby (zl gry): do kiesy wsi z utargu ").Append(N0(_inKept)).Append(" (").Append(N0(_inReturns)).Append(" powrotow taborow), majatki BK ").Append(N0(_inEst))
              .Append(", renta wsi ").Append(N0(_inRent)).Append(", dziesiecina wsi ").Append(N0(_inTithe)).Append(", danina wojenna wsi ").Append(N0(_inLevy))
              .Append(", lud miast netto W ").Append(N0(_inW)).Append(" / R ").Append(N0(_inR)).Append(" / P ").Append(N0(_inP))
              .Append(" (").Append(_inTowns).Append(" miast, bez liczb kiesy ").Append(_noEarn).Append("), podzamcza ").Append(N0(_inCastle)).Append(" (").Append(_inCastles).Append(" zamkow")
              .Append(_castleLost > 0 ? ", bez rzemieslnikow wsi " + N0(_castleLost / Math.Max(1, _sigma)) : "").Append("), rody ").Append(N0(_inHouse)).Append(" (1:1 brutto)")
              .Append(", racja niewolnych miast z kies R 40% / P 60% ").Append(N0(_slavePaid / Math.Max(1, _sigma))).Append(" (niepokryte ").Append(N0(_slaveUnpaid / Math.Max(1, _sigma)))
              .Append("), ujemny dochod stanow wsi przyciety do 0 ").Append(N0(_vilNeg / Math.Max(1, _sigma)));
            sb.Append(" | srednia biezaca dochodu: wykladnicza 1/28 (pierwsze 28 dob - zwykla), mediana osad ").Append(_medDays).Append(" dob (najwyzej ").Append(AvgDays)
              .Append("), z zapisu ").Append(_restoredN).Append(" osad");
            if (_warm) sb.Append(" - ROZGRZEWKA (polowa osad ma mniej niz ").Append(WarmDays).Append(" dob): D wstepne, bez ").Append(CsvName).Append(" i linii krolestw");
            sb.Append(" | pochylenie BK R i P: ").Append(_tiltN).Append(" osad (klasy BK z kalibracji kiesy ludu: ").Append(_bkN).Append(')')
              .Append(" | New Ibbish: wsie poza BoundVillages swojej warowni ").Append(_outsideN).Append(" (").Append(N0(_outsideL)).Append(" ludzi: ").Append(_outsideNames)
              .Append(") - liczone do regionu przez Bound; wsie bez regionu ").Append(_noRegion).Append(", bez miasta targowego ").Append(_noMarket)
              .Append(" | kultury spoza tabeli ").Append(_unknownCul).Append(" osad (profil zachod)");
            if (_itemsMissing > 0) sb.Append(" | towarow cen brak w grze ").Append(_itemsMissing).Append(" (cena z tabeli)");
            if (csvNote != null) sb.Append(" | ").Append(csvNote);
            sb.Append(" | czas ").Append(F3(ms)).Append(" ms (bez zapisu linii; mediana ").Append(F3(MedianMs())).Append(" ms z ").Append(_msN).Append(" ostatnich dob; spis ").Append(F3(tPrep))
              .Append(", ceny ").Append(F3(tPrice)).Append(", stany ").Append(F3(tPeople)).Append(", dostatek ").Append(F3(tLive)).Append(", spizarnia ").Append(F3(tPantry))
              .Append(", linie ").Append(F3(tLines)).Append(')');
            sb.Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(").");
            return sb.ToString();
        }

        private static string PantryLine(int day)
        {
            double need = 0, field = 0, mkt = 0, act = 0, brak = 0, dn = 0, df = 0, dm = 0, da = 0, db = 0;
            for (int g = 0; g < Groups; g++)
            {
                double b = Math.Max(0, _mkt[g] - _act[g]);
                if (IsDrink(g)) { dn += _need[g]; df += _field[g]; dm += _mkt[g]; da += _act[g]; db += b; }
                else { need += _need[g]; field += _field[g]; mkt += _mkt[g]; act += _act[g]; brak += b; }
            }
            double ppl = _nWorld;
            var sb = new StringBuilder(1800);
            sb.Append(PS).Append("dzien ").Append(day).Append(" | sigma ").Append(_sigma)
              .Append(" | potrzeba stale ").Append(N0(need / 1000)).Append(" t/d (").Append(F3(Div(need, ppl))).Append(" kg/os), napoje ").Append(N0(dn / 1000)).Append(" tys. l (")
              .Append(F3(Div(dn, ppl))).Append(" l/os), kcal ").Append(N0(Div(_kcal, ppl)))
              .Append(" | z pola i domu ").Append(N0(field / 1000)).Append(" t (").Append(F1(100 * Div(field, need))).Append("%), z targu ").Append(N0(mkt / 1000)).Append(" t (")
              .Append(F1(100 * Div(mkt, need))).Append("%) - razem ").Append(N0((field + mkt) / 1000)).Append(" t = potrzeba ").Append(F2(100 * (Div(field + mkt, need) - 1))).Append("% (glod ze spichlerzy ")
              .Append(N0((need - field - mkt) / 1000)).Append(" t)")
              .Append(" | napoje: z domu ").Append(N0(df / 1000)).Append(" tys. l, z targu ").Append(N0(dm / 1000)).Append(" tys. l")
              .Append(" | targ dzis (ksiega towarow, doba ").Append(_actDay).Append("): mieszczanie ").Append(N0(_actTownU)).Append(" szt., wsie ").Append(N0(_actVilU)).Append(" szt., dwory ")
              .Append(N0(_actCourtU)).Append(" szt. -> stale ").Append(N0(act / 1000)).Append(" t, napoje ").Append(N0(da / 1000)).Append(" tys. l (x sigma; dwory x1)")
              .Append(" | brak z targu stale ").Append(N0(brak / 1000)).Append(" t (").Append(F1(100 * Div(brak, mkt))).Append("% potrzeby z targu), napoje ").Append(N0(db / 1000)).Append(" tys. l")
              .Append(" | grupy t/d (potrzeba / z pola / z targu / targ dzis / brak):");
            for (int g = 0; g < Groups; g++)
                sb.Append(g == 0 ? " " : "; ").Append(GroupName[g]).Append(' ').Append(N0(_need[g] / 1000)).Append('/').Append(N0(_field[g] / 1000)).Append('/').Append(N0(_mkt[g] / 1000))
                  .Append('/').Append(N0(_act[g] / 1000)).Append('/').Append(N0(Math.Max(0, _mkt[g] - _act[g]) / 1000));
            sb.Append('.');
            return sb.ToString();
        }

        private static string KingdomLine(int day)
        {
            var sb = new StringBuilder(3000);
            sb.Append(P.TrimEnd(' ', ':')).Append(" - krolestwa: dzien ").Append(day).Append(" | ludzie mln, % niewolni / chlopi / mieszczanie / szlachta, wielkie rody, Dostatek, dochod na glowe zl/rok |");
            var keys = new List<string>(_king.Keys);
            keys.Sort((a, b) => _king[b][KN].CompareTo(_king[a][KN]));
            for (int i = 0; i < keys.Count; i++)
            {
                var k = _king[keys[i]];
                string nm; if (!_kingName.TryGetValue(keys[i], out nm)) nm = keys[i];
                sb.Append(i == 0 ? " " : "; ").Append(nm).Append(' ').Append(F2(k[KN] / 1e6)).Append(" mln, ").Append(Pc(k[KSlave], k[KN])).Append(" / ").Append(Pc(k[KPeas], k[KN])).Append(" / ")
                  .Append(Pc(k[KBurgh], k[KN])).Append(" / ").Append(Pc(k[KGent], k[KN])).Append(", rody ").Append(N0(k[KHouse])).Append(", D ").Append(F2(Div(k[KYK], k[KNv])))
                  .Append(", ").Append(N0(Div(k[KY], k[KNv]) * 364)).Append(" zl");
            }
            sb.Append('.');
            return sb.ToString();
        }

        private static string WriteCsv(int day)
        {
            var sb = new StringBuilder(_recs.Count * 240);
            int rows = 0;
            var notes = new List<string>(4);
            foreach (var r in _recs)
            {
                notes.Clear();
                if (r.OutsideBound) notes.Add("poza BoundVillages warowni (region przez Bound)");
                if (r.Kind == KCastle) notes.Add("zamek - ludzie w jego wsiach, D regionu");
                else if (r.L <= 0) notes.Add("bez ludzi ksiegi - D " + (r.DSrc == 1 ? "regionu" : r.DSrc == 2 ? "krolestwa" : "swiata"));
                if (r.NoEarn) notes.Add("miasto bez liczb kiesy ludu dzis - D regionu, W/R/P bez sredniej");
                if (r.YDays < WarmDays) notes.Add("srednia z " + r.YDays + " dob (rozgrzewka)");
                sb.Append(day).Append(';').Append(r.Id).Append(';').Append(r.Name).Append(';').Append(r.Kind == KTown ? "miasto" : r.Kind == KCastle ? "zamek" : "wies").Append(';')
                  .Append(r.KingdomName).Append(';').Append(r.Cul).Append(';').Append(Profs[r.Prof].Id).Append(';').Append(r.Region != null ? r.Region.Id : "-").Append(';')
                  .Append(N0(r.L));
                for (int e = 0; e < Estates; e++) sb.Append(';').Append(N0(r.N[e]));
                sb.Append(';').Append(N0(r.Thralls)).Append(';').Append(F3(r.D)).Append(';').Append(r.DSrc == 0 ? "wlasne" : r.DSrc == 1 ? "region" : r.DSrc == 2 ? "krolestwo" : r.DSrc == 3 ? "swiat" : "-")
                  .Append(';').Append(F3(r.D0));
                for (int e = 0; e < Estates; e++) sb.Append(';').Append(F3(r.DE[e]));
                sb.Append(';').Append(N0(r.Nv > 0 ? r.YSum / r.Nv * 364 : double.NaN)).Append(';').Append(F3(r.K)).Append(';').Append(F3(r.Fed)).Append(';').Append(r.YDays).Append(';')
                  .Append(string.Join(", ", notes.ToArray())).Append(Environment.NewLine);
                rows++;
            }
            string path = Log.Csv(CsvName, CsvHeader, sb.ToString());
            if (path == null) Stumble("CsvNull", null, "Log.Csv bez pliku - log wylaczony albo brak katalogu sesji");
            return CsvName + " " + (path != null ? rows + " wierszy" : "NIE ZAPISANY (log wylaczony albo brak katalogu sesji)");
        }
    }
}
