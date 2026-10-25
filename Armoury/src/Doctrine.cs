using System.Text;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P1 "marsz-doktryny" (projekt ai-marsz-taktyki.json B/C; klucze CultureDoctrines, ShootBeforeAttacking, ShootFirstSeconds, CavalrySeeksFlanks,
    /// ArchersFallBackBehindInfantry, ArcherFallBackDistance): doktryny kultur ROT jako mnozniki wag taktyk (WarCouncil.One -> ApplyList, plik P1),
    /// S1 najpierw ostrzal, S3 konnica na flanke, S4 lucznicy za piechote. SZKIELET P0 - tresc wypelnia paczka P1 (wlasciciel tego pliku).
    ///
    /// KONTRAKT (sygnatury ustalone w P0):
    ///  - ApplyMissionPatches(h) - z NightHooks.FirstBattle (raz na proces); opis do linii "Latki nocy (K3)".
    ///  - Begin / Tick / End     - z BattleSense (Begin PRZED WarCouncil.Council - tablice gotowe, zanim One wola ApplyList; Tick po MarchToContact, przed BattleSound).
    ///  - ResetCounters / Activity / Append - segment 'doktryna' linii "Bitwa (NB2)" (pod RoutHold.Gate).
    ///  - Zdarzenia dla dzwieku: Doctrine.ArchersFellBack(f) i Doctrine.FlankRide(f) sa w Doctrine.Events.cs (plik integratora) - P1 je WOLA, nie pisze.
    /// Potkniecia: RoutHold.Stumble(RoutHold.SiteDoctrine, ...).
    /// </summary>
    internal static partial class Doctrine
    {
        internal static string ApplyMissionPatches(Harmony h) { return "szkielet P0 (0/0)"; }

        internal static void Begin(Mission m) { }

        internal static void Tick(Mission m, float dt) { }

        internal static void End(Mission m) { }

        internal static void ResetCounters() { }

        internal static int Activity { get { return 0; } }

        internal static void Append(StringBuilder sb, Settings c) { }
    }
}
