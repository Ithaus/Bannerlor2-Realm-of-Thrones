using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
    ///    WitnessRadius (12 m) od ciala - bez swiadkow oboz spi dalej
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

                AlarmAround(affectedAgent, s.HideoutAlarmScreamRadius, s.HideoutAlarmScreamRadius);
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

                // czysta likwidacja: tylko swiadkowie przy ciele; nikogo = cisza,
                // ale SWIADEK krzyczy juz pelnym glosem i uruchamia sztafete
                AlarmAround(affectedAgent, s.HideoutAlarmWitnessRadius, s.HideoutAlarmScreamRadius);
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
                    // plyta dudni, skora szepcze: waga pancerza dokladana do halasu
                    float kg = 0f;
                    try { kg = a.SpawnEquipment.GetTotalWeightOfArmor(true); } catch { }
                    CautionAround(a, radius + kg * MathF.Max(0f, s.HideoutNoisePerArmorKg));
                }
            }
            catch (Exception e) { Log.Error("HideoutAlarm.Noise", e); }
        }

        /// <summary>
        /// ZANIEPOKOJENIE (Jeff: "najpierw idzie sprawdzic, alarm dopiero jak
        /// cos zobaczy"). Kroki przechodnia nie stawiaja obozu na nogi -
        /// patrolujacy w zasiegu przechodzi w Cautious (bron w dloni, rozglada
        /// sie, silnik sam go poprowadzi); pelny alarm robi dopiero walka
        /// albo to, co zobaczy na wlasne oczy (natywny wzrok).
        /// </summary>
        private void CautionAround(Agent noisy, float radius)
        {
            try
            {
                var m = Mission;
                if (m == null || m.PlayerTeam == null || radius <= 0f) return;
                var point = noisy.Position;
                foreach (var a in m.Agents)
                {
                    if (a == null || !a.IsHuman || !a.IsActive()) continue;
                    if (a.Team == null || !a.Team.IsEnemyOf(m.PlayerTeam)) continue;
                    if (a.CurrentWatchState != Agent.WatchState.Patrolling) continue;   // czujnego nie cofamy
                    if (a.Position.Distance(point) > radius) continue;
                    a.SetAlarmState(Agent.AIStateFlag.Cautious);
                }
            }
            catch (Exception e) { Log.Error("HideoutAlarm.CautionAround", e); }
        }

        /// <summary>
        /// SZTAFETA KRZYKU (Jeff: "obudzony tez krzyczy i budzi nastepnych").
        /// Pierwszy krag budzi sie w firstRadius od zrodla; kazdy OBUDZONY
        /// krzyczy dalej w relayRadius od SIEBIE - alarm skacze od czlowieka
        /// do czlowieka, poki lancuch ludzi siega, ale nigdy nie przeskakuje
        /// pustki wiekszej niz jeden krzyk. Bezpiecznik na 64 kregi.
        /// </summary>
        private void AlarmAround(Agent victim, float firstRadius, float relayRadius)
        {
            try
            {
                var m = Mission;
                if (m == null || m.PlayerTeam == null || firstRadius <= 0f) return;
                var s = Settings.Current;
                bool relay = s != null && s.HideoutAlarmRelay && relayRadius > 0f;

                var wave = new List<TaleWorlds.Library.Vec3> { victim.Position };
                float radius = firstRadius;
                int woken = 0, rings = 0;
                while (wave.Count > 0 && rings < 64)
                {
                    rings++;
                    var next = new List<TaleWorlds.Library.Vec3>();
                    Agent crier = null;
                    foreach (var a in m.Agents)
                    {
                        if (a == null || a == victim || !a.IsHuman || !a.IsActive()) continue;
                        if (a.Team == null || !a.Team.IsEnemyOf(m.PlayerTeam)) continue;
                        if (a.CurrentWatchState == Agent.WatchState.Alarmed) continue;   // juz krzyczal
                        bool inRange = false;
                        foreach (var p in wave)
                            if (a.Position.Distance(p) <= radius) { inRange = true; break; }
                        if (!inRange) continue;
                        a.SetAlarmState(Agent.AIStateFlag.Alarmed);
                        woken++;
                        if (crier == null) crier = a;
                        if (relay) next.Add(a.Position);
                    }
                    // SLYCHAC ALARM (Jeff: "realny krzyk audio"): jeden czlowiek
                    // na krag drze sie prawdziwym okrzykiem gry - lancuch
                    // pojedynczych wrzaskow niesie sie przez oboz, nie chor
                    if (crier != null && s != null && s.HideoutAlarmVoice)
                        try
                        {
                            crier.MakeVoice(SkinVoiceManager.VoiceType.Yell,
                                SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
                        }
                        catch { }
                    if (!relay) break;
                    wave = next;
                    radius = relayRadius;
                }
                if (woken > 0)
                    Log.Info("HideoutAlarm: alarm obudzil " + woken + " zbojcow w " + rings + " kregach (start "
                             + (int)firstRadius + " m, sztafeta " + (int)relayRadius + " m).");
            }
            catch (Exception e) { Log.Error("HideoutAlarm.AlarmAround", e); }
        }
    }
}
