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
    /// KALIBRACJA STARTU (KS, projekt docs/PROJEKT-KALIBRACJA-STARTU-2026-10-16.md; etap 1 - rdzen): produkcja okregu = rece x wydajnosc swiata x ziemia z mapy.
    ///  - BLOKI: wies gry w 20 blokach (punkt wsi + mieszanka ziemi okregu z rastra, arm_ks_land.tsv), kazda wioska poboczna osobno (udzial z osad ksiegi wiosek),
    ///    pola mieszczan (20 blokow rdzenia twierdzy; chlopi miasta, polowa pracy niewolnych, piata czesc wyrobnikow);
    ///  - RECE: ludzie okregu (dom + jego uchodzcy) x udzial stanu z ksiegi stanow ZC (chlopi 0.5, niewolni 0.65); ubytek rak robia dalej E4/5 i E4/7 (raz);
    ///  - DOM: straczki, ogrod, mleko, kury, ul (4 kg na pracujacego x ziemia domu), zboze wlasne bez progu ((L - 0.2) / 0.4 potrzeby), reszta rak - zajecia bloku;
    ///  - TABELE SWIATA: P0, koszt K, ha na pracujacego, ziemia (StartCalibration.Tables.cs - port 1:1 modelu v2); regale (zloza, las, oazy, przyprawy) za cena;
    ///  - WYBOR ZAJECIA: rownowaga reguly dochodu rodziny (logit beta 10, premia mapy 1.25, renta ziemi) - Plan na watku roboczym (StartCalibration.Core.cs);
    ///    po wlaczeniu ta sama regula przy 28-dobowym indeksie cen miasta wsi z hamulcem Jeffa (+25% przez 60 dob, koszt przebudowy do kasy miasta, okres przejsciowy,
    ///    najwyzej 1 czesc okregu naraz, najwyzej 5% rak regionu na sezon) - StartCalibration.Rule.cs;
    ///  - PRODUKCJA W GRZE (tylko po wlaczeniu): stawki wsi z KS w MaterialLaw.ProdPostfix (zastepuja lancuch typu, ludzi, stawek, owocow, wielbladow, regulatora i W2),
    ///    towary spoza typu na liscie BK z waga 0 (MineralOnce), magazyn wsi, zywnosc wsi gry (towar z niczego) wylaczona, plon pol mieszczan na polke miasta za zaplata
    ///    kasy miasta ludowi; na targ idzie to, czego okreg nie zje sam (udzial z targu ZC) - w sztukach gry (kg / (sigma x kg sztuki));
    ///  - SPICHLERZ: racje = max(racje KS z czesci domowej, dzisiejszy wzor) - podloga, dopoki nie ma zakupow ZC4 (KS nikogo nie zaglodzi);
    ///  - PLAN co 7 dob (tylko liczy: linie "Kalibracja startu (KS)" i "KS: ...", plik ks-plan.csv wedlug okregu i towaru).
    /// Wlaczenie (Apply) w tym etapie TYLKO w autotescie (klucz Start Calibration Autotest Apply i CrashScribe.Autotest.Active) - po pierwszym Planie; w grze Jeffa
    /// uzbrojenie w zapisie przyjdzie w etapie 2 (projekt rozdz. 12). Stan po wlaczeniu w zapisie (arm_ks); klucz Start Calibration wylaczony = nastepna doba
    /// produkcja wsi jak dzis (Undo bez terminu). Wyjatki per osada liczone (potkniecia) - bez globalnych wylacznikow.
    /// </summary>
    internal static partial class StartCalibration
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string P = "Kalibracja startu (KS): ";
        private const string PK = "KS: ";
        private const string SaveVersion = "ks1";
        internal const int StatePlan = 0, StateApplied = 1;

        // ------------------------------------------------------------ stan kampanii (zapis arm_ks)
        private static int _state = StatePlan;
        private static int _applyDay = -1, _plans, _firstPlanDay = -1;
        private static string _campaign = "";
        private static uint _saveLandCrc, _saveLayoutCrc;
        private static int _saveSigma;
        private static string _pending;               // zapis do rozwiazania przy starcie sesji
        private static string _loadNote = "";

        // ------------------------------------------------------------ pamiec sesji
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _offLogged;
        private static int _lastPlanDay = int.MinValue;

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.StartCalibration; } }
        internal static bool Applied { get { return _state == StateApplied && KeyOn; } }

        internal static void Reset()
        {
            _state = StatePlan; _applyDay = -1; _plans = 0; _firstPlanDay = -1; _campaign = ""; _saveLandCrc = _saveLayoutCrc = 0; _saveSigma = 0;
            _pending = null; _loadNote = "";
            _stumbles = _stumblesDay = 0; _err.Clear(); _offLogged = false; _lastPlanDay = int.MinValue;
            PlanReset();
            LiveReset();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_err.Add(where)) { if (e != null) Log.Error("StartCalibration." + where, e); else Log.Info(PK + "potkniecie - " + where); } } catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        // ============================================================ zapis
        internal static string Export()
        {
            try
            {
                if (_state != StateApplied && _plans == 0) return "";
                var sb = new StringBuilder(96 * 1024);
                sb.Append(SaveVersion).Append('|').Append(_state).Append('|').Append(Clean(_campaign)).Append('|').Append(_applyDay).Append('|').Append(_plans).Append('|')
                  .Append(_firstPlanDay).Append('|').Append(_saveLandCrc.ToString("X8")).Append('|').Append(_saveLayoutCrc.ToString("X8")).Append('|').Append(_saveSigma).Append('|');
                if (_state == StateApplied) ExportLive(sb);
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "-" : s.Replace('|', '/').Replace(';', ',').Replace('~', '-').Replace('!', '.'); }

        internal static void Import(string data)
        {
            _pending = string.IsNullOrEmpty(data) ? null : data;
        }

        /// <summary>Rozwiazanie zapisu (OnSessionLaunched): naglowek, a przy stanie "zastosowane" - zajecia blokow (wymaga pliku ziemi o tym samym crc i ukladu wiosek).</summary>
        private static void ResolvePending()
        {
            if (_pending == null) return;
            string data = _pending; _pending = null;
            try
            {
                var h = data.Split(new[] { '|' }, 10);
                int st, ad, pl, fp, sg; uint lc, yc;
                if (h.Length < 10 || h[0] != SaveVersion || !int.TryParse(h[1], NumberStyles.Integer, Inv, out st) || !int.TryParse(h[3], NumberStyles.Integer, Inv, out ad)
                    || !int.TryParse(h[4], NumberStyles.Integer, Inv, out pl) || !int.TryParse(h[5], NumberStyles.Integer, Inv, out fp)
                    || !uint.TryParse(h[6], NumberStyles.HexNumber, Inv, out lc) || !uint.TryParse(h[7], NumberStyles.HexNumber, Inv, out yc) || !int.TryParse(h[8], NumberStyles.Integer, Inv, out sg))
                { _loadNote = "zapis arm_ks w nieznanym formacie - pominiety (Plan od nowa, produkcja jak dzis)"; return; }
                _plans = Math.Max(0, pl); _firstPlanDay = fp; _campaign = h[2] == "-" ? "" : h[2];
                if (st != StateApplied) { _loadNote = "zapis: Plan (" + _plans + " planow od doby " + _firstPlanDay + ")"; return; }
                string gid = "";
                try { gid = Campaign.Current != null ? Campaign.Current.UniqueGameId ?? "" : ""; } catch { gid = ""; }
                EnsureLand();
                uint lay;
                if (!LandReady || lc != _landCrc) { _loadNote = "zapis ZASTOSOWANY z innym plikiem ziemi (crc " + lc.ToString("X8") + ", plik " + _landCrc.ToString("X8") + ") - stan pominiety, produkcja jak dzis, Plan od nowa"; _plans = 0; return; }
                if (!LayoutMatches(out lay) || yc != lay) { _loadNote = "zapis ZASTOSOWANY z innym ukladem wiosek (crc " + yc.ToString("X8") + ", gra " + lay.ToString("X8") + ") - stan pominiety, produkcja jak dzis, Plan od nowa"; _plans = 0; return; }
                if (gid.Length > 0 && _campaign.Length > 0 && gid != _campaign) { _loadNote = "zapis ZASTOSOWANY innej kampanii (id " + _campaign + ", gra " + gid + ") - stan pominiety, produkcja jak dzis"; _plans = 0; return; }
                int nd, nb, badD;
                if (!ImportLive(h[9], out nd, out nb, out badD)) { _loadNote = "zapis ZASTOSOWANY - blad odczytu zajec blokow - stan pominiety, produkcja jak dzis"; LiveReset(); return; }
                _state = StateApplied; _applyDay = ad; _saveLandCrc = lc; _saveLayoutCrc = yc; _saveSigma = sg;
                _loadNote = "zapis ZASTOSOWANY od doby " + ad + ": okregow " + nd + ", blokow " + nb + (badD > 0 ? " (okregow pominietych " + badD + ")" : "") + ", sigma " + sg;
            }
            catch (Exception e) { _loadNote = "blad odczytu arm_ks - stan pominiety"; LiveReset(); _state = StatePlan; Stumble("ResolvePending", e); }
        }

        // ============================================================ start sesji i doba
        /// <summary>ArmouryBehavior.OnSessionLaunched (po VillageBook.Resolve i McmSettings.Apply): plik ziemi, zapis, linia startu.</summary>
        internal static void SessionStart()
        {
            try
            {
                EnsureLand();
                ResolvePending();
                if (_state == StateApplied) ResolveLive();
                uint lay; bool lm = LandReady && LayoutMatches(out lay);
                var s = Settings.Current;
                Log.Info(P + "start sesji | klucz Start Calibration " + (KeyOn ? "TAK (Plan co 7 dob - tylko liczy)" : "NIE (nic nie liczy; zastosowany stan - produkcja jak dzis)")
                         + " | autotest: wlaczenie po pierwszym Planie " + (s != null && (s.StartCalibrationAutotestApply || ApplyFlag()) ? (AutotestMode() ? "TAK (autotest czynny; " + (s.StartCalibrationAutotestApply ? "klucz" : "plik " + ApplyFlagName) + ")" : "klucz/plik TAK, ale to nie autotest - nic") : "NIE")
                         + " | stan: " + (_state == StateApplied ? "ZASTOSOWANY od doby " + _applyDay : "Plan") + (_loadNote.Length > 0 ? " (" + _loadNote + ")" : "")
                         + " | " + _landNote + " | uklad wiosek tej sesji " + (lm ? "zgodny z plikiem" : "NIEZGODNY albo ksiega wiosek nieczynna - wioski poboczne poza KS, Apply zablokowane")
                         + " | premia mapy " + (s != null ? s.StartCalibrationMapInertia : 1.25f).ToString("0.00", Inv) + ", pola mieszczan " + (s == null || s.StartCalibrationTownFields ? "TAK" : "NIE")
                         + ", regula zmiany zajec po wlaczeniu " + (s == null || s.StartCalibrationSwitchRule ? "TAK" : "NIE") + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        /// <summary>Tryb autotestu CrashScribe (LogGate: 1 pelne, 2 profil) - jedyne miejsce, gdzie klucz Start Calibration Autotest Apply cos robi.</summary>
        private static bool AutotestMode() { int m = LogGate.Mode; return m == 1 || m == 2; }

        /// <summary>ArmouryBehavior.OnDailyTick, po DailyLife.Daily (stany doby): wynik Planu z watku roboczego, nowy Plan co 7 dob, wlaczenie w autotescie,
        /// a w stanie zastosowanym - stawki wsi na jutro, regula zmiany zajec, regale, pola mieszczan, linie. Wewnatrz bloku MoneyLedger (pozycja MKs).</summary>
        internal static void Daily()
        {
            int today = Today(), day = today - 1;
            _stumblesDay = 0;
            try
            {
                if (!KeyOn)
                {
                    PlanCancel();
                    LivePublishOff();
                    if (!_offLogged) { _offLogged = true; Log.Info(P + "dzien " + day + " | WYLACZONY (Start Calibration = off) - bez Planu" + (_state == StateApplied ? "; stan zastosowany zachowany w zapisie, produkcja wsi jak dzis (Undo)" : "") + "."); }
                    return;
                }
                _offLogged = false;
                if (Campaign.Current == null) return;
                EnsureLand();
                if (!LandReady) return;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                PricesDaily(today);                                   // 28-dobowe indeksy cen miast (regula zmiany zajec, regale)
                PlanCollect(day);                                     // wynik Planu z watku roboczego (linie i ks-plan.csv)
                if (_state == StatePlan)
                {
                    if (ApplyWanted() && _plan != null && _plan.Error == null && _planIn != null) Apply(today);
                    else if (day - _lastPlanDay >= 7 || _lastPlanDay == int.MinValue) PlanStart(day);
                }
                else
                {
                    LiveDaily(today);
                    if (day - _lastPlanDay >= 7 || _lastPlanDay == int.MinValue) PlanStart(day);   // Plan dalej co 7 dob - pomiar nacisku (gra bez zmian)
                }
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                AddMs(ms);
                if (LogGate.Lines)
                {
                    Log.Info(P + "dzien " + day + " | stan " + (_state == StateApplied ? "ZASTOSOWANY od doby " + _applyDay : "Plan") + " | Plan: " + (_wBusy ? "w toku (od doby " + _wDay + ")" : "czeka")
                             + ", ostatni gotowy doba " + _planDay + " (planow tego zapisu " + _plans + ", od doby " + _firstPlanDay + ")"
                             + (_state == StateApplied ? " | " + MoneyText() : "")
                             + " | czas doby KS " + ms.ToString("0.00", Inv) + " ms (mediana " + MedianMs().ToString("0.00", Inv) + " ms z " + _msN + " dob; bez watku roboczego)"
                             + " | potkniecia dzis " + _stumblesDay + " (sesja " + _stumbles + ").");
                    if (day % 7 == 0) { try { Log.Info(PriceLine(day)); } catch (Exception e) { Stumble("PriceLine", e); } }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private const int MsKeep = 120;
        private static readonly double[] _ms = new double[MsKeep], _msTmp = new double[MsKeep];
        private static int _msN, _msPos;
        private static void AddMs(double ms) { _ms[_msPos] = ms; _msPos = (_msPos + 1) % MsKeep; if (_msN < MsKeep) _msN++; }
        private static double MedianMs()
        {
            if (_msN <= 0) return 0;
            Array.Copy(_ms, _msTmp, _msN); Array.Sort(_msTmp, 0, _msN);
            return _msN % 2 == 1 ? _msTmp[_msN / 2] : 0.5 * (_msTmp[_msN / 2 - 1] + _msTmp[_msN / 2]);
        }

        internal const string ApplyFlagName = "armoury-ks-wlacz.flag";

        /// <summary>Plik-przelacznik autotestu (jak proba zimy): Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-ks-wlacz.flag - zamiast klucza MCM
        /// (MCM przepisuje ustawienia co godzine gry). Dziala tylko w autotescie.</summary>
        private static bool ApplyFlag()
        {
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), ApplyFlagName));
            }
            catch { return false; }
        }

        /// <summary>Wlaczenie w tym etapie: tylko autotest (klucz Start Calibration Autotest Apply albo plik-przelacznik + CrashScribe.Autotest.Active), stan Plan,
        /// uklad wiosek zgodny z plikiem ziemi.</summary>
        private static bool ApplyWanted()
        {
            var s = Settings.Current;
            if (s == null || !AutotestMode() || !(s.StartCalibrationAutotestApply || ApplyFlag())) return false;
            uint lay;
            return LayoutMatches(out lay);
        }
    }
}
