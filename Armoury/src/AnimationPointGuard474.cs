using System;
using HarmonyLib;
using SandBox;
using SandBox.Objects.AnimationPoints;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 474-P8-p5 (autotest at-474b, crash-original-2026-11-13_03-04-36.log): NullReferenceException w AnimationPoint.SetAgentItemVisibility
    /// (UserAgent.GetComponent&lt;CampaignAgentComponent&gt;().AgentNavigator.SetPrefabVisibility) przy koncu misji wnetrza zajazdu:
    /// UsableMachine.OnMissionEnded -> Agent.StopUsingGameObjectAux -> AnimationPoint.OnUseStopped -> SetAgentItemsVisibility. Bywalcy wnetrza (InnTable)
    /// i grajkowie sa stawiani przez Mission.SpawnAgent bez CampaignAgentComponent / AgentNavigator (to daje dopiero LocationCharacter + MissionAgentHandler),
    /// a krzeslo (ChairUsePoint) ma przedmioty w dloni (RightHandItem / Drink* / Eat* - lista _itemsForBones tylko rosnie). Gra sama chroni dodanie
    /// przedmiotow warunkiem CanAgentUseItem (navigator != null), ale ukrywanie przy OnUseStopped / rozmowie / ticku - juz nie. Pierwsza wywrotka w tescie
    /// (EndMission po 8 s) zostala polknieta przez try w InnInteriorLogic (misja nie zamknela sie - "mapa nie wrocila do spokoju w 300 s"), druga przy
    /// EndGame - CTD.
    /// Prefiks prywatnej AnimationPoint.SetAgentItemVisibility: agent bez CampaignAgentComponent albo bez AgentNavigator (tego samego warunku uzywa gra
    /// w CanAgentUseItem) - ukrycie pominiete (u takiego agenta przedmioty nigdy nie zostaly pokazane). Agenci gry (karczmy miast, osady) maja nawigator -
    /// zachowanie co do bitu jak w grze; u pozostalych gra i tak rzucalaby NRE. Wpiete raz przy starcie gry (cel w SandBox, nie model kampanii).
    /// </summary>
    internal static class AnimationPointGuard474
    {
        private static bool _installed;
        private static string _info = "-";
        private static int _skips;

        internal static int Skips { get { return _skips; } }
        internal static string Info { get { return _info; } }

        internal static void Install(Harmony h)
        {
            if (_installed || h == null) return;
            _installed = true;
            try
            {
                var target = AccessTools.DeclaredMethod(typeof(AnimationPoint), "SetAgentItemVisibility", new[] { typeof(AnimationPoint.ItemForBone), typeof(bool) });
                if (target == null) _info = "NIE (brak celu)";
                else { h.Patch(target, prefix: new HarmonyMethod(typeof(AnimationPointGuard474), nameof(Prefix))); _info = "wpieta"; }
            }
            catch (Exception e) { _info = "NIE (" + e.GetType().Name + ")"; try { Log.Error("AnimationPointGuard474.Install", e); } catch { } }
            try { Log.Info("Wnetrze zajazdu (474-P8-p5): oslona AnimationPoint.SetAgentItemVisibility (agent bez AgentNavigator) " + _info + "."); } catch { }
        }

        public static bool Prefix(AnimationPoint __instance)
        {
            try
            {
                var a = __instance != null ? __instance.UserAgent : null;
                if (a == null) { _skips++; return false; }
                var c = a.GetComponent<CampaignAgentComponent>();
                if (c == null || c.AgentNavigator == null || a.AgentVisuals == null) { _skips++; return false; }
                return true;
            }
            catch { return true; }
        }
    }
}
