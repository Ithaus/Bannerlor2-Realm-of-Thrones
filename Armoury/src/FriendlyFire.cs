using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P6 "friendly fire" (projekt noc/projekty/friendly-fire.txt; klucze FriendlyFireEnabled, FriendlyFireRangedPercent, FriendlyFireMeleePercent,
    /// FriendlyFireChargePercent): pociski, wrecz i szarza konia rania swoich - jedna regula gracz/AI; pole, oblezenie, kryjowka, morze (nie turniej, arena,
    /// bojki). SZKIELET P0 - tresc wypelnia paczka P6 (wlasciciel tego pliku).
    ///
    /// KONTRAKT (sygnatury ustalone w P0):
    ///  - ApplyAll(h) - z SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot zaraz PO ChargeTemperPatch.ApplyAll (przed JIT wolajacych), we wlasnym try:
    ///                  3 transpilery (MeleeHitCallback, MissileHitCallback, ChargeDamageCallback - cele AccessTools.DeclaredMethod(typeof(Mission), ...),
    ///                  kontrola liczby dopasowan), prefiks/finalizer machin, postfiks AgentApplyDamageModel.CalculateDamage; linia startu "FriendlyFire: wpiete ...".
    ///  - Logic       - zachowanie misji (bramka liczona raz w EarlyStart, linia "FF" w OnEndMission), dodawane w SubModuleMain.OnMissionBehaviorInitialize
    ///                  przy FriendlyFireEnabled i kampanii. Callbacki trafien moga przyjsc z ticku asynchronicznego - tylko statyki / [ThreadStatic],
    ///                  liczniki Interlocked, bez I/O i bez Settings.Current (kopia do statykow w EarlyStart).
    /// Bez globalnego wylacznika: wyjatek w pomocniczej zwraca wartosc gry i jest liczony.
    /// </summary>
    internal static class FriendlyFire
    {
        internal static void ApplyAll(Harmony h) { }

        /// <summary>noc 25.10 P6: bramka misji friendly fire (szkielet P0 - nic nie robi).</summary>
        internal sealed class Logic : MissionBehavior
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }
        }
    }
}
