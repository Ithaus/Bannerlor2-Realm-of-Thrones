using System;
using System.Globalization;
using System.IO;

namespace Armoury
{
    /// <summary>
    /// PROBA ZIMY W AUTOTESCIE (Jeff 11.10, wariant b; projekt etapu 4 rozdz. 3 "Proba zimy"). Dotad proba zimy wymagala zmiany 4 kluczy
    /// w Armoury.json probnej kopii - zabezpieczenie raz to zablokowalo (RAPORT-NOCNY-2026-10-09.md:60). Teraz skrypt testu kladzie plik-przelacznik
    /// "Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-proba-zimy.flag" (katalog CrashScribe - ta sama sciezka co jego logi), a Armoury
    /// przy starcie NOWEJ kampanii (OnNewGameCreated - wczytanie zapisu tego zdarzenia nie ma) ustawia na te jedna kampanie, TYLKO W PAMIECI:
    /// ClimateSummerDaysLeftMin = ClimateSummerDaysLeftMax = 1, ClimateFirstAutumnDaysMin = ClimateFirstAutumnDaysMax = 7.
    /// Skutek (WesterosClimate): lato startowe konczy sie po 1 dobie (pierwszy odcinek nie ma minimum), pierwsza jesien 7 dob (minimum
    /// Math.Max(7, ...) w Daily), zima od ok. 8. doby na Climate First Winter Years (3-5 lat) - zima trwa przez caly bieg 120 dob.
    /// Gdzie: wartosci zyja w WesterosClimate (_trialSummerLeft, _trialFirstAutumn), nie w Settings.Current - McmSettings.Apply przepisuje
    /// ustawienia z MCM co godzine gry, a pierwsza jesien losuje sie w ok. 1. dobie. Ustawienia (Armoury.json, MCM) nietkniete; plik-przelacznik
    /// zostaje (usuwa go skrypt testu). Bez pliku i przy wczytaniu zapisu - zero zmian. Zapis z kampanii probnej niesie juz swoje pory roku
    /// (arm_climate) - wczytany gra dalej zima, bez sprawdzania pliku.
    /// </summary>
    internal static class WinterTrial
    {
        internal const string FlagName = "armoury-proba-zimy.flag";
        private const int SummerDaysLeft = 1, FirstAutumnDays = 7;

        internal static string FlagPath()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), FlagName);
        }

        // f4-opal (noc 17/18.10; koncowy przeglad ekonomii 18.10: "zima nigdy nie byla testowana - test z wymuszona zima przed wgraniem"): PROBA ZIMY TERAZ - autotest
        // laduje zapis (autotest-F-d240), wiec plik armoury-proba-zimy.flag (tylko nowa kampania) nie dziala. Plik "armoury-zima-teraz.flag" w tym samym katalogu: przy
        // starcie KAZDEJ sesji autotestu (nowa gra i wczytanie; TownFolk.FirewoodSessionStart z OnSessionLaunched; recenzja: tylko przy CrashScribe.Autotest.Active - w grze
        // bez autotestu plik nic nie robi) pora EKONOMII = zima na te sesje (WesterosClimate.ForceEconWinter -
        // tylko w pamieci: odcinki por roku w zapisie, pora nieba i pogoda bez zmian; WinterBite, opal mieszczan, KS, BetterEconomy i inne latki pory ekonomii widza zime).
        // Bez pliku - nic (false). Plik zostaje (usuwa go skrypt testu); NIGDY w grze Jeffa.
        internal const string NowFlagName = "armoury-zima-teraz.flag";

        /// <summary>Start sesji: plik "armoury-zima-teraz.flag" - pora ekonomii zima na te sesje (linia logu); zwraca opis do linii startu opalu.</summary>
        internal static string LoadedSessionStart()
        {
            string path = "?";
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                path = Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), NowFlagName);
                if (!File.Exists(path)) { WesterosClimate.ForceEconWinter(false); return "NIE (brak pliku)"; }
                // recenzja: tylko w autotescie (CrashScribe.Autotest.Active - LogGate.Mode 1 albo 2, jak pliki KS armoury-ks-wlacz.flag i armoury-ks-auto.flag). Plik
                // zapomniany w katalogu CrashScribe nie zrobi wiecznej zimy ekonomii w zapisie Jeffa (DLL CrashScribe Jeffa nie ma typu Autotest - tryb 0).
                int mode = LogGate.Mode;
                if (mode != 1 && mode != 2)
                {
                    WesterosClimate.ForceEconWinter(false);
                    Log.Info("PROBA ZIMY TERAZ (autotest, f4-opal): plik-przelacznik " + path + " jest, ale to nie autotest (tryb " + mode + ") - bez skutku, pora ekonomii wedlug klimatu."
                             + " Plik mozna usunac.");
                    return "plik jest, ale to nie autotest - bez skutku";
                }
                var s = Settings.Current;
                if (s == null || !s.ClimateEnabled || !s.ClimateDrivesEconomy)
                {
                    WesterosClimate.ForceEconWinter(false);
                    Log.Info("PROBA ZIMY TERAZ (autotest, f4-opal): plik-przelacznik " + path + " jest, ale klimat Westeros nie prowadzi ekonomii (Climate Enabled / Climate Drives Economy) - bez skutku.");
                    return "plik jest, klimat nie prowadzi ekonomii - bez skutku";
                }
                WesterosClimate.ForceEconWinter(true);
                Log.Info("PROBA ZIMY TERAZ (autotest, f4-opal): plik-przelacznik " + path + " istnieje - pora EKONOMII = ZIMA na te sesje, tylko w pamieci (odcinki por roku w zapisie,"
                         + " pora nieba i pogoda bez zmian); Winter Bite " + (s.WinterBiteEnabled ? "TAK - skutki zimy czynne" : "NIE - zima bez skutkow") + "; plik zostaje (usuwa go skrypt testu).");
                return "TAK - pora ekonomii zima (Winter Bite " + (s.WinterBiteEnabled ? "TAK" : "NIE") + ")";
            }
            catch (Exception e) { WesterosClimate.ForceEconWinter(false); Log.Error("WinterTrial.LoadedSessionStart(" + path + ")", e); return "BLAD"; }
        }

        /// <summary>Z nasluchu OnNewGameCreated (ArmouryBehavior.RegisterEvents) - tylko nowa kampania, przed pierwsza doba.</summary>
        internal static void OnNewGame()
        {
            string path = "?";
            try
            {
                path = FlagPath();
                if (!File.Exists(path))
                {
                    Log.Info("Proba zimy (autotest): brak pliku-przelacznika " + path + " - klimat wedlug ustawien.");
                    return;
                }
                var s = Settings.Current;
                if (s == null || !s.ClimateEnabled)
                {
                    Log.Info("PROBNA ZIMA (autotest): plik-przelacznik " + path + " jest, ale klimat Westeros wylaczony (Climate Enabled) - bez skutku.");
                    return;
                }
                string how = WesterosClimate.Trial(SummerDaysLeft, FirstAutumnDays);
                Log.Info("PROBNA ZIMA (autotest): plik-przelacznik " + path + " istnieje - na te kampanie, tylko w pamieci (bez zapisu Armoury.json/MCM): "
                         + "ClimateSummerDaysLeftMin = ClimateSummerDaysLeftMax = " + SummerDaysLeft + ", ClimateFirstAutumnDaysMin = ClimateFirstAutumnDaysMax = " + FirstAutumnDays
                         + " (w ustawieniach " + s.ClimateSummerDaysLeftMin + "-" + s.ClimateSummerDaysLeftMax + " / " + s.ClimateFirstAutumnDaysMin + "-" + s.ClimateFirstAutumnDaysMax + ")"
                         + " - lato konczy sie po " + SummerDaysLeft + " dobie, jesien " + FirstAutumnDays + " dob, zima od ok. " + (SummerDaysLeft + FirstAutumnDays) + ". doby na "
                         + s.ClimateFirstWinterYearsMin.ToString("0.#", CultureInfo.InvariantCulture) + "-" + s.ClimateFirstWinterYearsMax.ToString("0.#", CultureInfo.InvariantCulture)
                         + " lat (Climate First Winter Years); " + how + "; plik zostaje (usuwa go skrypt testu).");
            }
            catch (Exception e) { Log.Error("WinterTrial.OnNewGame(" + path + ")", e); }
        }
    }
}
