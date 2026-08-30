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
    /// CZLOWIEKA, nie na choragiew: komu zostalo mniej niz WoundedFleePercent
    /// zdrowia, ten panikuje (CommonAIComponent.Panic) i odchodzi na tyly.
    /// Jesli sam odwrot sie nie zaczal - popychamy go jeszcze raz, wprost.
    ///
    /// Bohaterowie domyslnie zostaja: dowodca, ktory ucieka pierwszy, to nie
    /// Westeros. Wlacza sie ich osobnym pokretlem.
    /// </summary>
    internal sealed class BrokenMen : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        // ZAPETLONY KRZYK (Jeff): Retreat() bywa cofany przez model morale
        // (RBM StopRetreating zeruje flagi), a stary kod pchal czlowieka OD NOWA
        // co pol sekundy - kazde pchniecie gralo wrzask od poczatku, na siebie
        // nawzajem ("jakby ktos puszczal kilka nagran naraz"), a czlowiek stal
        // w miejscu szarpany tam i nazad. Teraz: proby z rosnacym odstepem
        // (3/6/9 s), po trzech nieudanych dajemy mu spokoj na 20 s - albo
        // odchodzi, albo morale go pozbieralo i walczy dalej. Zaden dzwiek
        // nie nachodzi na poprzedni.
        private readonly Dictionary<Agent, float> _nextTry = new Dictionary<Agent, float>();
        private readonly Dictionary<Agent, int> _tries = new Dictionary<Agent, int>();
        private float _accum;

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            try { _nextTry.Remove(affectedAgent); _tries.Remove(affectedAgent); } catch { }
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
                    // trup nie zna strachu - wight bije sie do konca (Jeff 27.08)
                    if (Undead.Character(a.Character)) continue;

                    // tylko swoi gracza albo wszyscy - wedle ustawienia
                    if (c.WoundedFleeEnemiesOnly && mission.MainAgent != null
                        && a.Team != null && mission.MainAgent.Team != null
                        && !a.Team.IsEnemyOf(mission.MainAgent.Team)) continue;

                    float max = a.HealthLimit;
                    if (max <= 0f) continue;
                    if (a.Health / max >= cut) continue;             // jeszcze stoi o wlasnych silach

                    var ai = a.CommonAIComponent;
                    if (ai == null) continue;
                    // JUZ UCIEKA - jakakolwiek droga (nasz odwrot, vanillowa
                    // panika morale, masowa ucieczka po zlamaniu armii). Stary
                    // warunek patrzyl tylko na IsRetreating, wiec przy ROZBICIU
                    // wojska (wszyscy biegna vanillowa ucieczka, IsRetreating
                    // false) pchalismy Panic() w kolko - i kazde pchniecie gralo
                    // wrzask od nowa (Jeff 27.08: "krzyki zapetlone przy ucieczce").
                    if (ai.IsRetreating || a.IsRunningAway) { _tries.Remove(a); _nextTry.Remove(a); continue; }

                    float now = mission.CurrentTime;
                    float next;
                    if (_nextTry.TryGetValue(a, out next) && now < next) continue;
                    int tries;
                    _tries.TryGetValue(a, out tries);
                    if (tries == 0) ai.Panic();                      // normalna droga: handler odeslie go na tyly
                    else ai.Retreat();                               // panika zjedzona przez model morale - wprost
                    _tries[a] = tries + 1;
                    _nextTry[a] = now + (tries >= 3 ? 20f : 3f + 3f * tries);
                }
            }
            catch (Exception e) { Log.Error("BrokenMen.OnMissionTick", e); }
        }
    }
}
