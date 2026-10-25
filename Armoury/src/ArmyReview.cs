using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P4 "przeglad-armii" (projekt przeglad-armii.json; klucze ArmyReviewEnabled, ArmyReviewWritesOrderOfBattle): opcja "Review the army" w menu obozu,
    /// raport koni i uprzezy, choragwie = 8 formacji Order of Battle gry (SetFormationInfos). SZKIELET P0 - tresc wypelnia paczka P4 (wlasciciel tego pliku).
    ///
    /// KONTRAKT (sygnatury ustalone w P0):
    ///  - AddMenus(starter)  - z ArmouryBehavior.OnSessionLaunched zaraz PO NightRest.AddMenus (menu arm_camp_wait juz stoi). Z menu oczekiwania BK
    ///                         (bk_camping_wait_menu) tylko okno inquiry - nigdy SwitchToMenu ani ExitToLast (CLAUDE.md 7).
    ///  - Export / Import    - zapis arm_army_review w ArmouryBehavior.SyncData (SaveText.Sync): Export zwraca napis albo null (null = nic do zapisu, klucz
    ///                         nie pisany); Import(null) = stary zapis bez klucza (stan pusty). Wersja w napisie ("ar1|...").
    ///  - Reset              - z konstruktora ArmouryBehavior (nowa kampania / wczytanie - PRZED SyncData).
    ///  - NameOf(i)          - nazwa choragwi gracza pod indeksem formacji 0..7 dla P3 (komunikaty kompanii); null = brak nazwy albo klasa w OOB inna niz zapisana.
    /// Okno w autotescie (LogGate.Mode 1/2) - flaga testu (wzor CampInterrogation.Test.cs), inaczej test stanie.
    /// </summary>
    internal static class ArmyReview
    {
        internal static void AddMenus(CampaignGameStarter starter) { }

        internal static string Export() { return null; }

        internal static void Import(string s) { }

        internal static void Reset() { }

        /// <summary>Nazwa choragwi gracza dla formacji 0..7; null = brak.</summary>
        internal static string NameOf(int formationIndex) { return null; }
    }
}
