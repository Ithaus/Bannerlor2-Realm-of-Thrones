using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// ZC0 (projekt docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md rozdz. 14 "ZC0 pomiar na sucho") - ZYCIE CODZIENNE: STANY, KOSZYK DNIA W KG, SPIZARNIA, DOSTATEK.
    /// TYLKO POMIAR: gra zachowuje sie dokladnie jak dzis - zadne zloto, towar, cena, dobrobyt, ludzie ani decyzja AI nie sa ruszane, zadna latka gry;
    /// liczby w pamieci sesji i w logu. Jedyny stan w zapisie: skala targu sigma (arm_dailylife, SaveText.Sync) - ustalona RAZ, przy pierwszym wczytaniu
    /// z kluczem DailyLifeMeasure (z DailyLifeMarketScale, domyslnie 37), potem nie ruszana w kampanii (zmiana w MCM - dopisek w linii startu sesji).
    ///  - stany kazdej osady z ksiegi ludzi (DailyLife.Estates.cs): pasma wedlug kultury i rodzaju osady, pochylenie klasami BK, konto NIEWOLNI ksiegi,
    ///    wielkie rody z bohaterow 1:1 (x3); suma stanow ksiegi = ksiega co do czlowieka (kontrola w linii);
    ///  - koszyk w kg na stan x kraine i koszyk przetrwania po cenach lokalnych (DailyLife.Basket.cs);
    ///  - Dostatek (Livelihood) i dochod na glowe na stan, osade, region, krolestwo i swiat (DailyLife.Livelihood.cs);
    ///  - Spizarnia: potrzeba jedzenia w kg wedlug stanu i krainy - z pola i domu (x czesc najedzona ze spichlerza) i z targu - wobec towaru, ktory lud
    ///    zjadl dzis z polek (ksiega towarow: mieszczanie i wsie x sigma, dwory 1:1); brak kg.
    /// Linie (raz na dobe przy pelnych pomiarach - ciche logi NW co N dob w grze, w autotescie codziennie): "Zycie (ZC0): dzien N", "Spizarnia (ZC): dzien N";
    /// co 7 dob linia "Zycie (ZC0) - krolestwa" i plik stany-dostatek.csv w katalogu sesji (wiersz na osade). Liczenie biegnie codziennie (srednia 28 dob).
    /// Koszt: ok. 800 osad x 7 stanow x 13 grup w tablicach, ceny miast raz na dobe; czas bloku w linii (cel projektu: mediana <= 5 ms na dobe).
    /// Wyjatki liczone per miejsce (pierwszy do pliku) - bez globalnych wylacznikow (CLAUDE.md 7). Klucz wylaczony - nic (linia raz).
    /// </summary>
    internal static partial class DailyLife
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string P = "Zycie (ZC0): ";
        private const string PS = "Spizarnia (ZC): ";
        private const string Version = "v1";
        private const int SigmaMin = 28, SigmaMax = 73;
        private const string CsvName = "stany-dostatek.csv";
        private const string CsvHeader = "dzien;osada_id;osada;rodzaj;krolestwo;kultura;profil;region_id;ludzie_ksiega;niewolni;chlopi;biedota;rzemieslnicy;kupcy;szlachta;wielkie_rody;"
                                         + "konto_niewolnych;D;zrodlo_D;D_niewolni;D_chlopi;D_biedota;D_rzemieslnicy;D_kupcy;D_szlachta;D_rody;dochod_na_glowe_zl_rok;koszyk_przetrwania_zl_d;"
                                         + "najedzeni;dni_sredniej;uwagi";

        // ------------------------------------------------------------ stan kampanii (zapis arm_dailylife)
        private static int _sigma = -1;            // -1 = jeszcze nie ustalona (klucz wylaczony od poczatku albo zapis sprzed paczki przed pierwsza doba)
        private static int _sigmaDay = -1;         // doba ustalenia
        private static bool _sigmaSaved;           // sigma przyszla z zapisu
        private static string _loadNote;           // dopisek wczytania (zly format)

        // ------------------------------------------------------------ pamiec sesji
        private static int _stumbles, _stumblesDay, _priceStumbles, _noteStumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _offLogged, _waitLogged;
        private static int _csvDay = int.MinValue, _days;
        private static readonly List<double> _ms = new List<double>();
        private static readonly object WinGate = new object();
        private static Dictionary<Settlement, double[]> _win = new Dictionary<Settlement, double[]>();   // wies -> [do kiesy wsi, podatek pana, majatki BK, powroty]

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.DailyLifeMeasure; } }

        /// <summary>Z konstruktora ArmouryBehavior: nowa sesja (zapis wczytuje Import po konstruktorze).</summary>
        internal static void Reset()
        {
            _sigma = -1; _sigmaDay = -1; _sigmaSaved = false; _loadNote = null;
            _stumbles = _stumblesDay = _priceStumbles = 0; _noteStumbles = 0; _err.Clear();
            _offLogged = _waitLogged = false; _csvDay = int.MinValue; _days = 0; _ms.Clear();
            lock (WinGate) { _win = new Dictionary<Settlement, double[]>(); }
            _winTaken = null;
            _recs = null; _byS.Clear(); _byHold.Clear(); _kName.Clear(); _cacheN = -1; _cacheDay = int.MinValue;
            _items = null; _pxBase = null; _usual = null; _pxWorld = null; _basket.Clear(); _kgOf.Clear(); _civil.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_err.Add(where)) { if (e != null) Log.Error("DailyLife." + where, e); else Log.Info(P + "potkniecie - " + where + "."); } } catch { }
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
        internal static string Export()
        {
            if (_sigma <= 0) return "";
            return Version + "|" + _sigma.ToString(Inv) + "|" + _sigmaDay.ToString(Inv);
        }

        internal static void Import(string data)
        {
            _sigma = -1; _sigmaDay = -1; _sigmaSaved = false; _loadNote = null;
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                var a = data.Split('|');
                int sg, d;
                if (a.Length < 3 || a[0] != Version || !int.TryParse(a[1], NumberStyles.Integer, Inv, out sg) || !int.TryParse(a[2], NumberStyles.Integer, Inv, out d) || sg < SigmaMin || sg > SigmaMax)
                { _loadNote = "zapis arm_dailylife w nieznanym formacie - pominiety (sigma od nowa z MCM)"; return; }
                _sigma = sg; _sigmaDay = d; _sigmaSaved = true;
            }
            catch (Exception e) { _sigma = -1; _loadNote = "blad odczytu arm_dailylife - sigma od nowa z MCM"; Stumble("Import", e); }
        }

        /// <summary>OnSessionLaunched: linia startu sesji - klucze i sigma.</summary>
        internal static void SessionStart()
        {
            try
            {
                if (KeyOn) EnsureSigma((int)CampaignTime.Now.ToDays);
                var s = Settings.Current;
                Log.Info(P + "start sesji | klucze: Daily Life Measure " + (KeyOn ? "tak" : "WYLACZONE") + ", Daily Life Market Scale " + (s != null ? s.DailyLifeMarketScale : 37)
                         + " | sigma " + SigmaText() + (_loadNote != null ? " | " + _loadNote : "")
                         + " | pomiar na sucho - gra bez zmian (zloto, towar, ceny, dobrobyt, AI) | linie: Zycie (ZC0), Spizarnia (ZC), co 7 dob krolestwa i " + CsvName
                         + " (doby pelnych pomiarow - ciche logi NW: w grze co " + LogGate.Every + " dob, w autotescie codziennie; liczenie codziennie)"
                         + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ------------------------------------------------------------ wejscie z MoneyLedger (powrot taboru wsi) - tylko licznik
        /// <summary>MoneyLedger.OnAfterEntered (tabor wraca do wsi z utargiem): ile weszlo do kiesy wsi, podatek pana, majatki BK. Tylko licznik - nie rzuca.</summary>
        internal static void NoteVillageReturn(Settlement home, int kept, int tax, int estates)
        {
            try
            {
                if (home == null || !home.IsVillage || !KeyOn) return;
                lock (WinGate)
                {
                    double[] w;
                    if (!_win.TryGetValue(home, out w)) { w = new double[4]; _win[home] = w; }
                    w[0] += kept; w[1] += tax; w[2] += estates; w[3] += 1;
                }
            }
            catch { System.Threading.Interlocked.Increment(ref _noteStumbles); }
        }

        private static Dictionary<Settlement, double[]> TakeWindows()
        {
            lock (WinGate) { var w = _win; _win = new Dictionary<Settlement, double[]>(); return w; }
        }

        // ------------------------------------------------------------ doba
        /// <summary>Z OnDailyTick (po TownFolk.Daily, GoodsLedger.Daily, Famine.Daily, PopulationLaw.Daily i ClanIncomeBook.Daily) - pomiar doby wczorajszej.</summary>
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays - 1;
            _stumblesDay = 0;
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

        private static void Compute(int day, long t0)
        {
            EnsureItems();
            EnsureRecs(day);
            _winTaken = TakeWindows();
            long t1 = Stopwatch.GetTimestamp();
            PeopleDay();
            long t2 = Stopwatch.GetTimestamp();
            PricesDay();                                   // po ludziach doby: koszyk zamku wazony ludzmi wsi regionu
            long t3 = Stopwatch.GetTimestamp();
            IncomeDay();
            LivelihoodDay();
            long t4 = Stopwatch.GetTimestamp();
            PantryDay();
            long t5 = Stopwatch.GetTimestamp();
            _days++;
            _winTaken = null;
            bool lines = LogGate.Lines;
            bool week = lines && (_csvDay == int.MinValue || day - _csvDay >= 7);
            double f = 1000.0 / Stopwatch.Frequency;
            double msCalc = (t5 - t0) * f;
            if (!lines) { _ms.Add(msCalc); return; }
            string csvNote = "";
            if (week)
            {
                _csvDay = day;
                try { csvNote = WriteCsv(day); } catch (Exception e) { Stumble("Csv", e); csvNote = CsvName + " NIE ZAPISANY (blad)"; }
                try { Log.Info(KingdomLine(day)); } catch (Exception e) { Stumble("KingdomLine", e); }
            }
            string pantry = null;
            try { pantry = PantryLine(day); } catch (Exception e) { Stumble("PantryLine", e); }
            long t6 = Stopwatch.GetTimestamp();
            double ms = (t6 - t0) * f;
            _ms.Add(ms);
            try { Log.Info(LifeLine(day, ms, (t1 - t0) * f, (t3 - t2) * f, (t2 - t1) * f, (t4 - t3) * f, (t5 - t4) * f, (t6 - t5) * f, week ? csvNote : null)); }
            catch (Exception e) { Stumble("LifeLine", e); }
            if (pantry != null) Log.Info(pantry);
        }

        // ------------------------------------------------------------ linie
        private static string N0(double x) { return double.IsNaN(x) || double.IsInfinity(x) ? "-" : Math.Round(x).ToString("0", Inv); }
        private static string F1(double x) { return double.IsNaN(x) || double.IsInfinity(x) ? "-" : x.ToString("0.0", Inv); }
        private static string F2(double x) { return double.IsNaN(x) || double.IsInfinity(x) ? "-" : x.ToString("0.00", Inv); }
        private static string F3(double x) { return double.IsNaN(x) || double.IsInfinity(x) ? "-" : x.ToString("0.000", Inv); }
        private static string Pc(double a, double b) { return b > 0 ? (100.0 * a / b).ToString("0.00", Inv) : "-"; }
        private static double Div(double a, double b) { return b > 0 ? a / b : double.NaN; }

        private static string LifeLine(int day, double ms, double tPrep, double tPrice, double tPeople, double tLive, double tPantry, double tLines, string csvNote)
        {
            var sb = new StringBuilder(2600);
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
            sb.Append(" | Dostatek swiat ").Append(F2(_dWorld)).Append(" (mediana chlopow wsi ").Append(F2(_medPeas)).Append(", Westeros ").Append(F2(Div(_yW[0], _nkW[0])))
              .Append(", Essos ").Append(F2(Div(_yW[1], _nkW[1]))).Append("), stany:");
            for (int e = 0; e < Estates; e++) sb.Append(e == 0 ? " " : ", ").Append(EstateName[e]).Append(' ').Append(F2(Div(_yE[e], _nkE[e])));
            sb.Append(" | dochod na glowe ").Append(N0(Div(_yWorld, _nWorld) * 364)).Append(" zl/rok (Westeros ").Append(N0(Div(_yW[0], _nW[0]) * 364)).Append(", Essos ")
              .Append(N0(Div(_yW[1], _nW[1]) * 364)).Append("), stany:");
            for (int e = 0; e < Estates; e++) sb.Append(e == 0 ? " " : ", ").Append(EstateName[e]).Append(' ').Append(N0(Div(_yE[e], _nE[e]) * 364));
            sb.Append(" | koszyk przetrwania zl/d: miasta ").Append(F3(_medTownK)).Append(" (mediana), wsie ").Append(F3(_medVilK)).Append(" (mediana, u bramy)");
            int nAll = _recs != null ? _recs.Count : 0;
            sb.Append(" | D policzone ").Append(_dOwn + _dReg + _dKing + _dWorldN).Append('/').Append(nAll).Append(" osad (wlasne ").Append(_dOwn).Append(", regionu ").Append(_dReg)
              .Append(", krolestwa ").Append(_dKing).Append(", swiata ").Append(_dWorldN).Append(", bez D ").Append(_dNone).Append(")")
              .Append("; regiony ").Append(_regions).Append(": D min ").Append(F2(_regMin)).Append(" / mediana ").Append(F2(_regMed)).Append(" / max ").Append(F2(_regMax));
            sb.Append(" | wejscia doby (zl gry): do kiesy wsi z utargu ").Append(N0(_inKept)).Append(" (").Append(N0(_inReturns)).Append(" powrotow taborow), majatki BK ").Append(N0(_inEst))
              .Append(", renta wsi ").Append(N0(_inRent)).Append(", lud miast netto W ").Append(N0(_inW)).Append(" / R ").Append(N0(_inR)).Append(" / P ").Append(N0(_inP))
              .Append(" (").Append(_inTowns).Append(" miast, bez liczb kiesy ").Append(_noEarn).Append("), podzamcza ").Append(N0(_inCastle)).Append(" (").Append(_inCastles).Append(" zamkow")
              .Append(_castleLost > 0 ? ", bez rzemieslnikow wsi " + N0(_castleLost / Math.Max(1, _sigma)) : "").Append("), rody ").Append(N0(_inHouse)).Append(" (1:1)");
            sb.Append(" | srednia ").Append(_recs != null && _recs.Count > 0 ? MaxDays() : 0).Append(" dob (najwyzej ").Append(AvgDays).Append(')')
              .Append(" | pochylenie BK R i P: ").Append(_tiltN).Append(" osad (klasy BK z kalibracji kiesy ludu: ").Append(_bkN).Append(')')
              .Append(" | New Ibbish: wsie poza BoundVillages swojej warowni ").Append(_outsideN).Append(" (").Append(N0(_outsideL)).Append(" ludzi: ").Append(_outsideNames)
              .Append(") - liczone do regionu przez Bound; wsie bez regionu ").Append(_noRegion).Append(", bez miasta targowego ").Append(_noMarket)
              .Append(" | kultury spoza tabeli ").Append(_unknownCul).Append(" osad (profil zachod)");
            if (_itemsMissing > 0) sb.Append(" | towarow cen brak w grze ").Append(_itemsMissing).Append(" (cena z tabeli)");
            if (csvNote != null) sb.Append(" | ").Append(csvNote);
            sb.Append(" | czas ").Append(F3(ms)).Append(" ms (mediana sesji ").Append(F3(Median(_ms))).Append(" ms z ").Append(_ms.Count).Append(" dob; spis ").Append(F3(tPrep))
              .Append(", ceny ").Append(F3(tPrice)).Append(", stany ").Append(F3(tPeople)).Append(", dostatek ").Append(F3(tLive)).Append(", spizarnia ").Append(F3(tPantry))
              .Append(", linie ").Append(F3(tLines)).Append(')');
            int ps = _priceStumbles; _priceStumbles = 0;
            int ns = System.Threading.Interlocked.Exchange(ref _noteStumbles, 0);
            sb.Append(" | potkniecia dzis ").Append(_stumblesDay + ps + ns).Append(" (od wczytania ").Append(_stumbles).Append(").");
            return sb.ToString();
        }

        private static int MaxDays() { int m = 0; foreach (var r in _recs) if (r.YDays > m) m = r.YDays; return m; }

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
            keys.Sort((a, b) => _king[b][2].CompareTo(_king[a][2]));
            for (int i = 0; i < keys.Count; i++)
            {
                var k = _king[keys[i]];
                string nm; if (!_kingName.TryGetValue(keys[i], out nm)) nm = keys[i];
                sb.Append(i == 0 ? " " : "; ").Append(nm).Append(' ').Append(F2(k[2] / 1e6)).Append(" mln, ").Append(Pc(k[3], k[2])).Append(" / ").Append(Pc(k[4], k[2])).Append(" / ")
                  .Append(Pc(k[5], k[2])).Append(" / ").Append(Pc(k[6], k[2])).Append(", rody ").Append(N0(k[7])).Append(", D ").Append(F2(Div(k[0], k[1]))).Append(", ").Append(N0(Div(k[0], k[2]) * 364)).Append(" zl");
            }
            sb.Append('.');
            return sb.ToString();
        }

        private static string WriteCsv(int day)
        {
            var sb = new StringBuilder(_recs.Count * 220);
            int rows = 0;
            foreach (var r in _recs)
            {
                double n = 0, y = 0;
                for (int e = 0; e < Estates; e++) { n += r.N[e]; y += r.YAvg[e]; }
                string note = r.OutsideBound ? "poza BoundVillages warowni (region przez Bound)" : r.Kind == KCastle ? "zamek - ludzie w jego wsiach, D regionu" : r.L <= 0 ? "bez ludzi ksiegi - D " + (r.DSrc == 1 ? "regionu" : r.DSrc == 2 ? "krolestwa" : "swiata") : "";
                sb.Append(day).Append(';').Append(r.Id).Append(';').Append(r.Name).Append(';').Append(r.Kind == KTown ? "miasto" : r.Kind == KCastle ? "zamek" : "wies").Append(';')
                  .Append(r.KingdomName).Append(';').Append(r.Cul).Append(';').Append(Profs[r.Prof].Id).Append(';').Append(r.Region != null ? r.Region.Id : "-").Append(';')
                  .Append(N0(r.L));
                for (int e = 0; e < Estates; e++) sb.Append(';').Append(N0(r.N[e]));
                sb.Append(';').Append(N0(r.Thralls)).Append(';').Append(F3(r.D)).Append(';').Append(r.DSrc == 0 ? "wlasne" : r.DSrc == 1 ? "region" : r.DSrc == 2 ? "krolestwo" : r.DSrc == 3 ? "swiat" : "-");
                for (int e = 0; e < Estates; e++) sb.Append(';').Append(F3(r.DE[e]));
                sb.Append(';').Append(N0(n > 0 ? y / n * 364 : double.NaN)).Append(';').Append(F3(r.K)).Append(';').Append(F3(r.Fed)).Append(';').Append(r.YDays).Append(';').Append(note)
                  .Append(Environment.NewLine);
                rows++;
            }
            string path = Log.Csv(CsvName, CsvHeader, sb.ToString());
            return CsvName + " " + (path != null ? rows + " wierszy" : "NIE ZAPISANY (log wylaczony albo brak katalogu sesji)");
        }
    }
}
