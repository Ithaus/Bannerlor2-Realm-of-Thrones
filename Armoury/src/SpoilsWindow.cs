namespace Armoury
{
    /// <summary>
    /// noc 25.10 P5 "okno-lupu": okno lupu (wlasna warstwa Gauntlet, prefab GUI/Prefabs/ArmSpoilsWindow.xml, zapas StoryWindow / ShowInquiry).
    /// SZKIELET P0 - tresc wypelnia paczka P5 (wlasciciel tego pliku).
    ///
    /// KONTRAKT (sygnatury ustalone w P0):
    ///  - IsOpen       - okno otwarte albo raport czeka na pokazanie: czyta Prologue SceneGate i StoryPictures.AppTick (te warunki dopisuje P5 - pliki na jego liscie).
    ///  - AppTick()    - z SubModuleMain.OnApplicationTick (metoda NoInlining, licznik bledow ladowania typu jak Chronicle); wlasny try w srodku, 4 razy na sekunde.
    ///  - Close(why)   - z SubModuleMain.OnGameEnd (wyjscie z kampanii) - zdjecie warstwy, zwolnienie obrazka.
    /// W autotescie (LogGate.Mode 1/2) okno zamyka sie samo po 1.5 s - inaczej test stanie. Zadnego SwitchToMenu (CLAUDE.md 7).
    /// </summary>
    internal static class SpoilsWindow
    {
        internal static bool IsOpen { get { return false; } }

        internal static void AppTick() { }

        internal static void Close(string why) { }
    }
}
