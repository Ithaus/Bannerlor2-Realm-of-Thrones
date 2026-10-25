using HarmonyLib;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P3 "choragwie" (projekt choragwie.json A-B; klucze StandardBearerKitSafe, FieldStandards, BannerCompaniesEnabled): latki kampanii -
    /// SandboxBattleBannerBearersModel.GetDesiredNumberOfBannerBearersForFormation, DefaultFormationArrangementModel.GetBannerBearerPositions,
    /// DTE DynamicTroopMissionLogic.ProcessAgentEquipmentRespectingTemporarySlots (zwrot sprzetu odlozonego przez chorazego). SZKIELET P0 - tresc wypelnia P3.
    ///
    /// KONTRAKT: ApplyInCampaign(h) - z NightHooks.InCampaign (ArmouryBehavior.OnSessionLaunched, raz na proces); AccessTools.DeclaredMethod na klasie
    /// deklarujacej, Patch i Unpatch tym samym MethodInfo; zwraca krotki opis do linii "Latki nocy (K3)", np. "liczba chorazych TAK, rozstaw TAK, zwrot DTE TAK".
    /// Dopisanie BannerKit.OnRegistered w GarrisonKit.RegisterPostfix robi P3 we wlasnym zakresie (GarrisonKit.cs - jedna linia, plik na liscie P3).
    /// </summary>
    internal static class BannerKit
    {
        internal static string ApplyInCampaign(Harmony h) { return "szkielet P0 (0/0)"; }
    }
}
