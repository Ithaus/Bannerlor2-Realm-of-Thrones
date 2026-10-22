namespace Armoury
{
    /// <summary>
    /// m107-gospodarka (22.10, Jeff: "biedne wsie sprawdz, rozwiaz stojace wozy, zaleglosci zamkow napraw, wycena ziarna, zapis tablicy ogloszen tez zrob"): linia startu sesji
    /// "Gospodarka (m107): start sesji" z szescioma kluczami pakietu (K1 Village Cart Flee Unload - CartGuard.cs; K2 Ds Castle Folk Part No Arrears i K3 Ds Castle Fast Cut -
    /// DsTowns.cs; K4 Ks Plan Market Balance i K5 Ks Fach Market Room - StartCalibration.Core.cs / Fach.cs / Plan.cs; K6 Notice Board Lean Save - NoticeBoard.cs).
    /// Wolana z ArmouryBehavior.OnSessionLaunched po McmSettings.Apply (klucze z MCM). Tylko log (prog M7G-0 grupy m107gosp).
    /// </summary>
    internal static class M107Start
    {
        internal const string P = "Gospodarka (m107): ";

        private static string YN(bool b) { return b ? "TAK" : "NIE"; }

        internal static void SessionStart()
        {
            var s = Settings.Current;
            if (s == null) { Log.Info(P + "start sesji | brak ustawien."); return; }
            Log.Info(P + "start sesji | Village Cart Flee Unload " + YN(s.VillageCartFleeUnload) + " | Ds Castle Folk Part No Arrears " + YN(s.DsCastleFolkPartNoArrears)
                     + " | Ds Castle Fast Cut " + YN(s.DsCastleFastCut) + " | Ks Plan Market Balance " + YN(s.KsPlanMarketBalance) + " | Ks Fach Market Room " + YN(s.KsFachMarketRoom)
                     + " | Notice Board Lean Save " + YN(s.NoticeBoardLeanSave) + ".");
        }
    }
}
