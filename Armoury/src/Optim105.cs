using System;
using System.Collections.Generic;

namespace Armoury
{
    /// <summary>
    /// m105-optym (21.10): linia startu kampanii "Optym (m105): start" - klucze skrotow przelomu doby (kazdy osobno, NIE = kod jak dotad 1:1) i tryb kontrolny
    /// OptimVerify (wspolny z m99); liczniki sesji od zera. Wolane z KingdomTreasury.OptimStartLine (start kampanii / wczytanie).
    /// Opis zmian i dowody rownowaznosci: docs/noc-1920/m105-optym-CHANGELOG.md.
    /// </summary>
    internal static class Optim105
    {
        // noc 21/22.10: tryb kontrolny w autotescie bez ruszania Armoury.json Jeffa - DLL autotestu CrashScribe (LogGate.Mode 1/2) + plik CrashScribe/armoury-optym-kontrola.flag
        internal const string AutoFlagName = "armoury-optym-kontrola.flag";
        private static int _autoFlag = -1;

        /// <summary>OptimVerify z MCM albo z pliku autotestu (sprawdzany raz na uruchomienie gry; w zwyklej grze zawsze tylko MCM).</summary>
        internal static bool VerifyOn(Settings s)
        {
            if (s != null && s.OptimVerify) return true;
            if (_autoFlag < 0)
            {
                _autoFlag = 0;
                try
                {
                    int m = LogGate.Mode;
                    if (m == 1 || m == 2)
                    {
                        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        string p = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName);
                        if (System.IO.File.Exists(p)) _autoFlag = 1;
                    }
                }
                catch { _autoFlag = 0; }
            }
            return _autoFlag == 1;
        }

        internal static void StartLine()
        {
            try { SutlerNear.Reset(); } catch { }
            try { BuildFunding.BfWarReset(); } catch { }
            try { ClanIncomeBook.CdReset(); } catch { }
            var s = Settings.Current;
            if (s == null) return;
            var on = new List<string>(); var off = new List<string>();
            (s.OptimKtSkipModel ? on : off).Add("Optim Kt Skip Model (KingdomTreasury.Daily - model dochodu pominiety u rodow bez lenn, wsi BK i warsztatu glowy z podstawa ziemia)");
            (s.OptimSutlerNearest ? on : off).Add("Optim Sutler Nearest (Sutlers.Daily - najblizsze miasto i wies z tablic doby)");
            (s.OptimBuildWar ? on : off).Add("Optim Build War (BuildFunding.Daily - wojna krolestwa raz na krolestwo)");
            (s.OptimStableDelegates ? on : off).Add("Optim Stable Delegates (ClanIncomeBook.StableBegin - BK przez delegaty zamiast odbicia)");
            Log.Info("Optym (m105): start | Optim Verify " + (VerifyOn(s) ? "TAK" : "NIE") + (s.OptimVerify ? "" : (VerifyOn(s) ? " (plik autotestu " + AutoFlagName + ")" : ""))
                     + " | wlaczone: " + (on.Count > 0 ? string.Join("; ", on.ToArray()) : "brak")
                     + " | wylaczone (kod jak dotad): " + (off.Count > 0 ? string.Join("; ", off.ToArray()) : "brak")
                     + " | bez zmian (wynik zalezy od kolejnosci, skutkow ubocznych albo czeka na pomiar wnetrza - m105-optym-CHANGELOG): model w BuildFunding.Daily i ClanIncomeBook.Daily,"
                     + " SupplyDemand.DailyTrade, WarLedger.OnDaily, Stage6Measure.Daily.");
        }
    }
}
