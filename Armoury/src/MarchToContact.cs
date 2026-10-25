using System.Text;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P1 "marsz-doktryny" (projekt ai-marsz-taktyki.json A/M1; klucze MarchToContactEnabled, MarchStepPace, CavalryApproachPace, CavalryGallopDistance,
    /// ArchersWalkUntil, PlayerMoveOrderWalks): szyk idzie stepem do szarzy, biegnie dopiero w szarzy. SZKIELET P0 - tresc wypelnia paczka P1 (wlasciciel pliku).
    ///
    /// KONTRAKT (sygnatury ustalone w P0 - nie zmieniac, inne paczki je wolaja):
    ///  - ApplyMissionPatches(h)  - z NightHooks.FirstBattle (pierwsza bitwa, glowny watek, raz na proces); zwraca krotki opis do linii "Latki nocy (K3)", np. "2/2".
    ///  - Begin / Tick / End      - z BattleSense (AfterStart; OnMissionTick w kolejnosci BattleCries -> MarchToContact -> Doctrine -> BattleSound, K4;
    ///                              OnEndMission / OnRemoveBehavior). Wolane tylko w bitwie (BattleSense: _battle i nie pojedynek); End musi byc bezpieczne wielokrotnie.
    ///  - IsMarching(f)           - dla BattleSound (bebny, K7): szyk ma teraz cap marszu. Glowny watek.
    ///  - CapFor(a)               - dla FieldCraft (M0): cap marszu czlowieka w m/s albo -1 (brak).
    ///  - OnChargeStarted(f)      - JEDYNY sygnal szarzy (K4): wola go BattleCries.ChargeStarted (BattleCries.Events.cs) w chwili przejscia szyku na
    ///                              Charge / ChargeToTarget - tu zdjecie capu i od razu CacheMovementSpeedOfUnits.
    ///  - ResetCounters / Activity / Append - segment 'marsz' linii "Bitwa (NB2)" (RoutHold.Ensure / Write pod RoutHold.Gate - Append nie bierze innej blokady).
    /// Potkniecia: RoutHold.Stumble(RoutHold.SiteMarch, ...). Latka AdjustSpeedLimit biegnie w watkach roboczych - bez alokacji, logu i Settings.Current.
    /// </summary>
    internal static class MarchToContact
    {
        internal static string ApplyMissionPatches(Harmony h) { return "szkielet P0 (0/0)"; }

        internal static void Begin(Mission m) { }

        internal static void Tick(Mission m, float dt) { }

        internal static void End(Mission m) { }

        /// <summary>Szyk idzie teraz marszem (ma cap). Szkielet: zawsze false.</summary>
        internal static bool IsMarching(Formation f) { return false; }

        /// <summary>Cap marszu czlowieka w m/s; -1 = brak capu. Szkielet: zawsze -1.</summary>
        internal static float CapFor(Agent a) { return -1f; }

        /// <summary>Szyk przeszedl na szarze (sygnal z BattleCries.ChargeStarted, glowny watek).</summary>
        internal static void OnChargeStarted(Formation f) { }

        internal static void ResetCounters() { }

        internal static int Activity { get { return 0; } }

        internal static void Append(StringBuilder sb, Settings c) { }
    }
}
