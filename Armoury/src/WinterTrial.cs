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
