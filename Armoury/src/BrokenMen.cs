using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// ZLAMANI ODCHODZA Z POLA. Jeff: "przeciwnik ponizej 10% HP w kolko sie
    /// drze i nie ucieka tylko stoi w miejscu - kazdy by sie wycofal, skoro
    /// nie moze walczyc".
    ///
    /// Vanilla lamie ludzi tylko przez MORALE calego szyku (BattleMoraleModel),
    /// a pojedynczy ranny stoi i przyjmuje ciosy, dopoki szyk sie trzyma - przy
    /// RBM, ktore przelicza morale po swojemu, tym bardziej. My patrzymy na
    /// CZLOWIEKA, nie na chorągiew: komu zostalo mniej niz WoundedFleePercent
    /// zdrowia, ten panikuje (CommonAIComponent.Panic) i odchodzi na tyly.
    /// Jesli sam odwrot sie nie zaczal - popychamy go jeszcze raz, wprost.
    ///
    /// Bohaterowie domyslnie zostaja: dowodca, ktory ucieka pierwszy, to nie
    /// Westeros. Wlacza sie ich osobnym pokretlem.
    /// </summary>
    internal sealed class BrokenMen : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private readonly HashSet<Agent> _pushed = new HashSet<Agent>();
        private float _accum;

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            try { _pushed.Remove(affectedAgent); } catch { }
        }

        public override void OnMissionTick(float dt)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.WoundedFleeEnabled) return;
                _accum += dt;
                if (_accum < 0.5f) return;      // pol sekundy wystarczy - to nie parowanie
                _accum = 0f;

                var mission = Mission.Current;
                if (mission == null || mission.Agents == null) return;
                if (mission.MainAgent == null && !c.WoundedFleeWithoutPlayer) return;

                float cut = c.WoundedFleePercent / 100f;
                if (cut <= 0f) return;
                if (cut > 0.9f) cut = 0.9f;

                var agents = mission.Agents;
                for (int i = 0; i < agents.Count; i++)
                {
                    var a = agents[i];
                    if (a == null || !a.IsActive() || !a.IsHuman) continue;
                    if (!a.IsAIControlled) continue;                 // gracza nikt nie zmusi do ucieczki
                    if (a == mission.MainAgent) continue;
                    if (a.IsHero && !c.WoundedFleeHeroes) continue;  // dowodcy trzymaja sie do konca

                    // tylko swoi gracza albo wszyscy - wedle ustawienia
                    if (c.WoundedFleeEnemiesOnly && mission.MainAgent != null
                        && a.Team != null && mission.MainAgent.Team != null
                        && !a.Team.IsEnemyOf(mission.MainAgent.Team)) continue;

                    float max = a.HealthLimit;
                    if (max <= 0f) continue;
                    if (a.Health / max >= cut) continue;             // jeszcze stoi o wlasnych silach

                    var ai = a.CommonAIComponent;
                    if (ai == null) continue;
                    if (ai.IsRetreating) continue;                   // juz idzie

                    if (!_pushed.Contains(a))
                    {
                        _pushed.Add(a);
                        ai.Panic();                                  // normalna droga: handler odeslie go na tyly
                    }
                    else
                    {
                        // panika poszla, a on dalej stoi (czyjs model morale ja zjadl) - wprost
                        ai.Retreat();
                    }
                }
            }
            catch (Exception e) { Log.Error("BrokenMen.OnMissionTick", e); }
        }
    }
}
