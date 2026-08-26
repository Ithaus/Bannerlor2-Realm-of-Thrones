using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// BRAMA DLA DTE W KRYJOWCE (Jeff: "w hideout ubrani, ale w oryginalne
    /// pancerze jednostki, nie te z magazynu jak w bitwie"). Patch spawnu DTE
    /// wpuszcza swoj przydzial tylko, gdy misja ma IMissionAgentSpawnLogic
    /// ALBO jest zasadzka na kryjowke - ZWYKLA kryjowka nie ma ani jednego,
    /// wiec DTE odpuszczal i wojsko szlo we wzorcowym rynsztunku. Jego logika
    /// misji MA galezie kryjowkowe (loot, zwroty), czyli mialo dzialac -
    /// zapomnieli o bramie. Ta zaslepka implementuje interfejs pustymi
    /// odpowiedziami: DTE uzywa go WYLACZNIE jako testu obecnosci
    /// (GetMissionBehavior == null), silnik gry w kryjowce nie wola go wcale.
    /// </summary>
    internal sealed class HideoutSpawnShim : MissionBehavior, IMissionAgentSpawnLogic
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        public BattleSideEnum PlayerSide { get { return BattleSideEnum.Attacker; } }
        public void StartSpawner(BattleSideEnum side) { }
        public void StopSpawner(BattleSideEnum side) { }
        public bool IsSideSpawnEnabled(BattleSideEnum side) { return false; }
        public bool IsSideDepleted(BattleSideEnum side) { return false; }
        public float GetReinforcementInterval(BattleSideEnum side = BattleSideEnum.None) { return 0f; }
        public IEnumerable<IAgentOriginBase> GetAllTroopsForSide(BattleSideEnum side) { yield break; }
        public bool GetSpawnHorses(BattleSideEnum side) { return false; }
        public int GetNumberOfPlayerControllableTroops() { return 0; }
    }
}
