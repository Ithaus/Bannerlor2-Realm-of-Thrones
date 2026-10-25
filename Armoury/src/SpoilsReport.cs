using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P5 "okno-lupu" (projekt okno-lupu.json; klucze SpoilsWindowAfterBattle, SpoilsWindowAfterSiege, SpoilsWindowAfterTourney, SpoilsWindowMinWorth):
    /// raport lupu z ramek stanu przed i po (tylko odczyt - zero zmian w ekonomii). SZKIELET P0 - tresc wypelnia paczka P5 (wlasciciel tego pliku).
    ///
    /// KONTRAKT (sygnatury ustalone w P0):
    ///  - Reset()                      - z konstruktora ArmouryBehavior i z SubModuleMain.OnGameEnd.
    ///  - OnTourneyJoined(town, b)     - CampaignEvents.OnPlayerJoinedTournamentEvent (ramka T: otwarcie).
    ///  - OnTourneyFinished(...)       - CampaignEvents.TournamentFinished; nasluch dopisany PRZED TourneyFolkPurse, wiec biegnie PO nim (gra wola od ostatnio
    ///                                   dopisanego - ArmouryBehavior.RegisterEvents B-1) i widzi juz sakiewke mieszczan.
    ///  - OnTourneyEliminated(r, town) - CampaignEvents.PlayerEliminatedFromTournament (znacznik).
    /// Latki Harmony (DoApplyMapEventResults, CommitGoldChanges, DoEnd, SiegeAftermathAction.ApplyInternal, Spoils) P5 zaklada z BattlefieldLaw.ApplyAll
    /// (plik na liscie P5). Wszystkie wolania glowny watek; potkniecia liczone, bez wylacznika.
    /// </summary>
    internal static class SpoilsReport
    {
        internal static void Reset() { }

        internal static void OnTourneyJoined(Town town, bool isParticipant) { }

        internal static void OnTourneyFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize) { }

        internal static void OnTourneyEliminated(int round, Town town) { }
    }
}
