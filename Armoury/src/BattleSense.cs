using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// NB2 czesc 2 (16.10): zachowanie misji dla taktyki wedlug dowodcy (WarCouncil), krwi przez pancerz (BattleGore) i glosow bitwy (BattleCries);
    /// weteranom wedlug tieru (VeteranAi, postfiks Harmony) sprawdza tylko kolejnosc latek po RBM. Rejestrowane w SubModuleMain.OnMissionBehaviorInitialize,
    /// gdy wlaczony ktorykolwiek z kluczy CommanderTactics, VeteransByTier, BloodThroughArmour, BattleCriesAndMoans, GroundBloodPools (czesc 3, 16.10:
    /// kaluze krwi na ziemi - BloodPools; OnAgentRemoved i OnAgentHit tylko kolejka, decale z OnMissionTick). Liczniki ida do jednej linii
    /// "Bitwa (NB2): misja" (RoutHold.Write, RoutKeeper.OnEndMission). Kazde wejscie w osobnym try/catch z licznikiem potkniec (RoutHold.Stumble) -
    /// OnAgentHit to wywolanie zwrotne silnika, wyjatek nie moze z niego wyjsc. Nazwa klasy nie moze byc "Log", "Settings" ani "McmSettings".
    /// </summary>
    internal sealed class BattleSense : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private static WeakReference _m;
        private static bool _battle;

        /// <summary>Misja to bitwa na mapie, kryjowka albo bitwa wlasna (rodzaj z linii NM, liczony raz w AfterStart).</summary>
        internal static bool InBattle(Mission m)
        {
            var w = _m;
            return _battle && w != null && m != null && ReferenceEquals(w.Target, m);
        }

        public override void AfterStart()
        {
            var m = Mission;
            try
            {
                string kind = MissionTeams.Kind(m);
                _battle = kind == "bitwa" || kind == "kryjowka";
                _m = new WeakReference(m);
            }
            catch (Exception e) { _battle = false; RoutHold.Stumble(RoutHold.SiteVoice, "BattleSense.AfterStart", e); }
            try { BattleGore.Begin(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteGore, "BattleGore.Begin", e); }
            try { BattleCries.Begin(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVoice, "BattleCries.Begin", e); }
            try { BloodPools.Begin(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Begin", e); }
            try { if (_battle) VeteranAi.CheckOrder(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVeteran, "VeteranAi.CheckOrder", e); }
            try { if (_battle) WarCouncil.Council(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteTactics, "WarCouncil.Council", e); }
            base.AfterStart();
        }

        public override void OnMissionTick(float dt)
        {
            try { BattleCries.Tick(Mission, dt); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVoice, "BattleCries.Tick", e); }
            try { BloodPools.Tick(Mission, dt); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Tick", e); }
        }

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            try { BattleGore.OnHit(Mission, affectedAgent, affectorAgent, in blow, in attackCollisionData); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteGore, "BattleGore.OnHit", e); }
            try { BattleCries.OnHit(Mission, affectedAgent); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVoice, "BattleCries.OnHit", e); }
            try { BloodPools.OnHit(Mission, affectedAgent, in blow, in attackCollisionData); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.OnHit", e); }
        }

        // czesc 3 (16.10): zgon / nokaut - kaluze krwi (tylko kolejka; jestesmy w petli gry po zachowaniach misji, Mission.OnAgentRemoved)
        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            try { BloodPools.OnRemoved(Mission, affectedAgent, affectorAgent, agentState, in blow); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.OnRemoved", e); }
        }

        // czesc 3: Mission.ResetMission czysci decale sceny sam (ClearRuntimeDecals) - kaluze tylko zapominaja kolejke
        public override void OnClearScene()
        {
            try { BloodPools.Cleared(Mission); } catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Cleared", e); }
            base.OnClearScene();
        }

        protected override void OnEndMission()
        {
            Clean();
            base.OnEndMission();
        }

        public override void OnRemoveBehavior()
        {
            Clean();
            base.OnRemoveBehavior();
        }

        private void Clean()
        {
            try { BattleCries.Clear(); } catch { }
            try { BattleGore.Clear(); } catch { }
            try { BloodPools.End(Mission); } catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.End", e); }
            var w = _m;
            if (w != null && ReferenceEquals(w.Target, Mission)) { _battle = false; _m = null; }
        }
    }
}
