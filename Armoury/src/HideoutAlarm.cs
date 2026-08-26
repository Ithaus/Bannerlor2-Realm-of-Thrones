using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// ALARM W KRYJOWCE (Jeff: "strzelam do goscia, on na mnie biegnie,
    /// a ludzie 10 metrow dalej udaja, ze nic sie nie dzieje"). Vanilla
    /// budzi tylko zaczepiona grupke - reszta obozu spi. Zasady:
    ///  - trafiony, ktory PRZEZYL, krzyczy: wrogowie w promieniu
    ///    ScreamRadius (40 m) od ofiary ida do walki; ci 200 m dalej
    ///    nie slysza nic - odleglosc robi robote;
    ///  - CZYSTE zabojstwo jednym ciosem budzi tylko swiadkow w promieniu
    ///    WitnessRadius (12 m) od ciala - bez swiadkow obóz spi dalej
    ///    (Jeff: "chyba ze zastrzelilem goscia jedna strzala");
    ///  - bijatyka przy graczu tez halasuje - kazde wrogie trafienie
    ///    budzi wrogow wokol miejsca ciosu.
    /// Budzenie to publiczne Agent.SetAlarmState(Alarmed) - ten sam stan,
    /// w ktory vanilla wprawia zaczepionych. Lancuch niesie sie sam:
    /// obudzeni dobiegaja, walka przy nich budzi nastepnych.
    /// </summary>
    internal sealed class HideoutAlarm : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        /// <summary>Czy to misja kryjowki (HideoutMissionController / HideoutAmbushMissionController).</summary>
        internal static bool IsHideout(Mission mission)
        {
            try
            {
                if (mission == null) return false;
                foreach (var b in mission.MissionBehaviors)
                {
                    var n = b != null ? b.GetType().FullName : null;
                    if (n != null && n.Contains("Hideout") && n.Contains("MissionController")) return true;
                }
            }
            catch { }
            return false;
        }

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HideoutAlarmEnabled) return;
                if (affectedAgent == null || affectorAgent == null || affectedAgent == affectorAgent) return;
                if (!affectedAgent.IsHuman) return;
                if (affectedAgent.Team == null || affectorAgent.Team == null) return;
                if (!affectedAgent.Team.IsEnemyOf(affectorAgent.Team)) return;
                if (blow.InflictedDamage <= 0) return;
                // pada od TEGO ciosu - o alarmie zdecyduja swiadkowie (OnAgentRemoved)
                if (affectedAgent.Health <= 0f) return;

                AlarmAround(affectedAgent, s.HideoutAlarmScreamRadius);
            }
            catch (Exception e) { Log.Error("HideoutAlarm.OnAgentHit", e); }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HideoutAlarmEnabled) return;
                if (affectedAgent == null || affectorAgent == null || !affectedAgent.IsHuman) return;
                if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;
                if (affectedAgent.Team == null || affectorAgent.Team == null) return;
                if (!affectedAgent.Team.IsEnemyOf(affectorAgent.Team)) return;

                // czysta likwidacja: tylko swiadkowie przy ciele; nikogo = cisza
                AlarmAround(affectedAgent, s.HideoutAlarmWitnessRadius);
            }
            catch (Exception e) { Log.Error("HideoutAlarm.OnAgentRemoved", e); }
        }

        // ------------------------------------------------- halas krokow
        // Jeff: "w nocy widac mniej, ale slychac bardziej - w kryjowce".
        // Wzrok AI w misji to natywna sprawa silnika; SLUCH dokladamy sami:
        // biegnacy czlowiek (predkosc > prog) budzi zbojcow w promieniu -
        // noca wiekszym (cisza niesie), za dnia mniejszym (gwar obozu maskuje).
        // Chod na Left Ctrl (WalkKey) jest wolny, wiec cichy - skradanie dziala.
        private float _noiseTimer;

        public override void OnMissionTick(float dt)
        {
            try
            {
                _noiseTimer += dt;
                if (_noiseTimer < 1f) return;
                _noiseTimer = 0f;
                var s = Settings.Current;
                if (s == null || !s.HideoutAlarmEnabled || !s.HideoutNoiseEnabled) return;
                var m = Mission;
                if (m == null || m.PlayerTeam == null) return;
                int h = TaleWorlds.CampaignSystem.CampaignTime.Now.GetHourOfDay;
                bool night = h >= 21 || h <= 5;
                float radius = night ? s.HideoutHearNight : s.HideoutHearDay;
                if (radius <= 0f) return;
                foreach (var a in m.Agents)
                {
                    if (a == null || !a.IsHuman || !a.IsActive()) continue;
                    if (a.Team == null || (a.Team != m.PlayerTeam && !a.Team.IsPlayerAlly)) continue;
                    if (a.Velocity.Length < 3.5f) continue;    // marsz i skradanie sa ciche
                    AlarmAround(a, radius);
                }
            }
            catch (Exception e) { Log.Error("HideoutAlarm.Noise", e); }
        }

        /// <summary>Budzi obsade kryjowki (wrogow gracza) w promieniu od ofiary.</summary>
        private void AlarmAround(Agent victim, float radius)
        {
            try
            {
                var m = Mission;
                if (m == null || m.PlayerTeam == null || radius <= 0f) return;
                var point = victim.Position;
                int woken = 0;
                foreach (var a in m.Agents)
                {
                    if (a == null || a == victim || !a.IsHuman || !a.IsActive()) continue;
                    if (a.Team == null || !a.Team.IsEnemyOf(m.PlayerTeam)) continue;
                    if (a.CurrentWatchState == Agent.WatchState.Alarmed) continue;
                    if (a.Position.Distance(point) > radius) continue;
                    a.SetAlarmState(Agent.AIStateFlag.Alarmed);
                    woken++;
                }
                if (woken > 0)
                    Log.Info("HideoutAlarm: walka obudzila " + woken + " zbojcow (promien " + (int)radius + " m).");
            }
            catch (Exception e) { Log.Error("HideoutAlarm.AlarmAround", e); }
        }
    }
}
