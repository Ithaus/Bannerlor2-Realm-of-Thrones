using System.Collections.Generic;

namespace Armoury
{
    /// <summary>
    /// m105-optym (21.10): linia startu kampanii "Optym (m105): start" - klucze skrotow przelomu doby (kazdy osobno, NIE = kod jak dotad 1:1) i tryb kontrolny
    /// OptimVerify (wspolny z m99). Wolane z KingdomTreasury.OptimStartLine (start kampanii / wczytanie). Opis zmian: docs/noc-1920/m105-optym-CHANGELOG.md.
    /// </summary>
    internal static class Optim105
    {
        internal static void StartLine()
        {
            var s = Settings.Current;
            if (s == null) return;
            var on = new List<string>(); var off = new List<string>();
            (s.OptimKtSkipModel ? on : off).Add("Optim Kt Skip Model (KingdomTreasury.Daily - model dochodu pominiety u rodow bez lenn, wsi BK i warsztatu glowy z podstawa ziemia)");
            Log.Info("Optym (m105): start | Optim Verify " + (s.OptimVerify ? "TAK" : "NIE")
                     + " | wlaczone: " + (on.Count > 0 ? string.Join("; ", on.ToArray()) : "brak")
                     + " | wylaczone (kod jak dotad): " + (off.Count > 0 ? string.Join("; ", off.ToArray()) : "brak") + ".");
        }
    }
}
