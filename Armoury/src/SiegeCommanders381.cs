using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 381 (Jeff 09.11 "dodaj poprawke ze glowno dowodzacy nie atakuja w glownym szturmie, zeby nie zostac pojmanym"; Goldentooth 09.11: wodz armii
    /// Galbart Glover padl w szturmie 19:36, a po zalamaniu 373 wzial do lochu kilkudziesieciu rannych lordow). WODZOWIE ZOSTAJA Z TYLU W SZTURMIE AI.
    /// Dowod (dekompilacja 1.4.8, rally_dec/mb_full.cs): misja oblezenia ma GeneralsAndCaptainsAssignmentLogic (createBodyguard false) - OnTeamDeployed
    /// tworzy szyk wodza (szyk 8, NumberOfRegularFormations, BehaviorGeneral) tylko gdy CanTeamHaveGeneralsFormation, a RBMAI.CanTeamHaveGeneralsFormationPatch
    /// zwraca false - szyku wodza nie ma; AssignBestCaptainsForTeam robi kazdego bohatera kapitanem najwiekszego zwyklego szyku 0-7 i przenosi go tam
    /// (OnCaptainAssignedToFormation) - TacticBreachWalls posyla te szyki na drabiny, wieze i brame, lordowie ida z nimi. BehaviorGeneral (gdyby szyk 8 byl)
    /// stoi Depth + 3 m za glownym szykiem - pod murami tez pod strzalami. EpicSieges / VanillaAiSiegesforRBM: bez odwolan do General / GeneralAgent (grep DLL).
    /// Regula (klucz SiegeAttackerLordsStayBack; tylko szturm murow - IsSiegeBattle, bez wypadu i morza; tylko gdy gracz NIE jest po stronie atakujacych):
    ///  - po rozstawieniu (jak 371 / 378) i potem co 2 s (posilki): kazdy bohater-lord AI atakujacych (Agent.IsHero, IsAIControlled, nie gracz,
    ///    HeroObject.IsLord, nie rod gracza), ktory nie ucieka, idzie do szyku 8 swojej druzyny (pusty przy RBM; bez RBM - szyk wodza gry, przejety);
    ///  - szyk 8: pozycja z planu rozstawienia gry (SetFormationPositioningFromDeploymentPlan - ta sama, co szyk wodza gry), a gdy blizej niz SafeDist (150 m)
    ///    od najblizszego obroncy - cofniety po linii od niego, krok 10 m, tylko w granicach pola i na siatce (najdalszy dobry punkt do SafeDist);
    ///    MovementOrderMove na te pozycje, twarza do wroga, SetControlledByAI(false) - druzyna AI nie rusza szyku poza swoimi szykami 0-7
    ///    (TacticBreachWalls / ManageFormationCounts RBM i gry dzialaja na FormationsIncludingEmpty = 0-7). Gdy cos wlaczy AI szyku - z powrotem przy skanie;
    ///  - bohater na machinie / drabinie: StopUsingGameObject przed przeniesieniem. Kapitan: gra sama zdejmuje kapitana, ktory nie dowodzi z daleka
    ///    (Formation.OnUnitRemoved); wodz druzyny (CanLeadFormationsRemotely) dowodzi dalej z tylu - jak historyczny dowodca;
    ///  - odwrot 371 / 378 (Agent.Retreat) obejmuje ich tak samo (od pierwszego uciekajacego bohatera nie ruszamy szyku); wodz nadal liczy sie
    ///    do wstrzasu 371 (strzaly, machiny, wypad obroncow);
    ///  - zostali sami: 2 skany z rzedu bez zadnego innego walczacego czlowieka atakujacych i bez odwodu spawnera - wodzowie odstepuja (Agent.Retreat
    ///    do najblizszego wyjscia, jak 371) - inaczej misja stalaby bez konca (obroncy nie schodza z murow).
    /// Linie logu: 'Wodzowie (381): start ...' (ilu, imiona, odleglosc od obroncow), 'dolaczyl' (posilki), 'koniec misji' (ilu padlo / rannych, z imionami).
    /// 381b (recenzja): odwrot (_broken) tylko gdy polowa strony ucieka; posilek do oproznionego szyku 8 - AI szyku znow wylaczone; punkt z planu
    /// tylko w granicach pola i do 400 m od lordow; 'zostali sami' liczone takze przy kluczu wylaczonym w bitwie i przy nieznanym spawnerze (8 skanow).
    /// 387 (Jeff 10.11 "rob caly punkt 2" - 2c: "wodzowie AI zostaja z tylu tylko wtedy, gdy sie bronisz"): takze gdy gracz walczy PO STRONIE ATAKUJACYCH,
    /// a szturmem dowodzi AI (wodz strony atakujacej nie jest druzyna gracza; klucz SiegeAllyLordsStayBack) - tryb _ally. Dowod (mb_full 57175
    /// Mission.GetAgentTeam): partie z armii gracza trafiaja do Mission.PlayerTeam, wiec lordowie AI stoja w druzynie gracza - pomijamy ja tylko wtedy,
    /// gdy gracz jest jej generalem (Team.IsPlayerGeneral - wtedy wszystkie szyki maja PlayerOwner i sa pod jego rozkazami). Szyk 8 oddany graczowi
    /// (PlayerOwner) zostaje nietkniety. Bohaterowie rodu gracza i towarzysze - nie (Lord()). Gracz dowodzi szturmem sam - modul nieczynny jak dotad.
    /// Wyjatek per czlowiek / druzyna = potkniecie (log 3 pierwszych), bez globalnego wylacznika.
    /// </summary>
    internal sealed class SiegeCommanders381 : MissionLogic
    {
        private const float ScanEvery = 2f, NoDeployStart = 10f, SafeDist = 150f, Step = 10f, MaxPlanFar = 400f;
        private const int ErrMax = 3, NamesMax = 30, UnknownHits = 8;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Hold
        {
            public Team T;
            public Formation F;
            public WorldPosition Pos;
            public bool Placed;
            public float Dist0 = -1f, Dist = -1f;   // odleglosc od najblizszego obroncy: z planu i po cofnieciu
        }

        private bool _known, _active, _started, _ended, _alone, _broken, _startLogged, _ally;   // _ally (387): gracz po stronie atakujacych, szturmem dowodzi AI
        private float _nextScan;
        private int _stumbles, _errs, _aloneHits, _reAi;
        private MapEvent _me;
        private readonly List<Hold> _holds = new List<Hold>();
        private readonly HashSet<Agent> _moved = new HashSet<Agent>();
        private readonly List<string> _names = new List<string>(), _late = new List<string>(), _killed = new List<string>(), _wounded = new List<string>();

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private static string F(double v, string f) { return v.ToString(f, Inv); }

        private static bool On { get { var s = Settings.Current; return s != null && s.SiegeAttackerLordsStayBack; } }
        private static bool AllyOn { get { var s = Settings.Current; return s != null && s.SiegeAllyLordsStayBack; } }

        private bool Known(Mission m)
        {
            if (_known) return _active;
            _known = true;
            try
            {
                if (m == null || !m.IsSiegeBattle || m.IsSallyOutBattle || m.IsNavalBattle) return _active = false;
                var pt = m.PlayerTeam;
                if (pt == null) return _active = false;
                _me = MapEvent.PlayerMapEvent;
                if (_me == null || !_me.IsSiegeAssault || _me.IsNavalMapEvent) return _active = false;
                if (pt.Side == BattleSideEnum.Attacker)
                {
                    // 387: gracz po stronie atakujacych - tylko gdy szturmem dowodzi AI (gracz-wodz: jego sprawa, jak dotad) i klucz
                    var lpb = _me.AttackerSide != null ? _me.AttackerSide.LeaderParty : null;
                    if (lpb == null || lpb == PartyBase.MainParty || !AllyOn) return _active = false;
                    _ally = true;
                }
                _active = true;
            }
            catch (Exception e) { Stumble("Known", e); _active = false; }
            return _active;
        }

        public override void OnMissionTick(float dt)
        {
            if (_ended) return;
            var m = Mission;
            if (m == null) return;
            try
            {
                if (!Known(m)) { _ended = true; return; }
                if (m.MissionEnded) return;
                float now = m.CurrentTime;
                if (!_started)
                {
                    bool deployed = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && now >= NoDeployStart);
                    if (!deployed) return;
                    _started = true;
                    _nextScan = now;
                }
                if (now < _nextScan) return;
                _nextScan = now + ScanEvery;
                if (On && (!_ally || AllyOn))                      // klucz NIE w trakcie - nic nowego (juz odsunieci stoja, gdzie stoja)
                {
                    Scan(m);
                    if (!_startLogged && _moved.Count > 0) LogStart(m);
                }
                Alone(m);                                          // 381b: takze przy kluczu wylaczonym w bitwie - odsunieci nie moga zawiesic misji
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        /// <summary>Spis bohaterow-lordow atakujacych i przeniesienie do szyku 8; pilnowanie, by szyk 8 nie wrocil pod AI druzyny.</summary>
        private void Scan(Mission m)
        {
            if (!_broken && SideRouting(m)) _broken = true;       // 381b: odwrot strony (371 / 378 / zalamanie morale), nie pojedynczy uciekajacy
            foreach (var t in m.Teams)
            {
                try
                {
                    if (!MissionTeams.Valid(t) || t.Side != BattleSideEnum.Attacker) continue;
                    if (t.IsPlayerTeam && (!_ally || t.IsPlayerGeneral)) continue;   // 387: druzyna gracza tylko wtedy, gdy dowodzi nia AI (gracz w armii AI)
                    Hold h = null;
                    var list = t.ActiveAgents;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var a = list[i];
                        try
                        {
                            if (!Lord(a)) continue;
                            if (a.IsRunningAway) continue;               // 381b: sam jeden uciekajacy lord nie gasi pilnowania szyku 8
                            if (h == null) h = HoldOf(m, t);
                            if (h == null || h.F == null) break;
                            if (a.Formation == h.F) { Record(a, h); continue; }   // bez RBM: wodz juz w szyku wodza gry (przejetym)
                            Move(a, h);
                        }
                        catch (Exception e) { Stumble("Agent", e); }
                    }
                    if (h == null) h = Find(t);
                    if (h != null && h.Placed && !_broken && h.F.CountOfUnits > 0 && h.F.IsAIControlled)
                    {
                        h.F.SetControlledByAI(false);                    // ktos wlaczyl AI szyku (BehaviorGeneral szedlby za glownym szykiem pod mury)
                        h.F.SetMovementOrder(MovementOrder.MovementOrderMove(h.Pos));
                        _reAi++;
                    }
                }
                catch (Exception e) { Stumble("Team", e); }
            }
        }

        /// <summary>381b: strona atakujacych w odwrocie - co najmniej polowa jej ludzi w polu ucieka (zapasc 371 cofa wszystkich naraz).</summary>
        private static bool SideRouting(Mission m)
        {
            int all = 0, run = 0;
            foreach (var t in m.Teams)
            {
                if (!MissionTeams.Valid(t) || t.Side != BattleSideEnum.Attacker) continue;
                foreach (var a in t.ActiveAgents)
                {
                    if (a == null || !a.IsHuman) continue;
                    all++;
                    if (a.IsRunningAway) run++;
                }
            }
            return all > 0 && run * 2 >= all;
        }

        private static bool Lord(Agent a)
        {
            if (a == null || !a.IsActive() || !a.IsHuman || !a.IsHero || !a.IsAIControlled || a.IsMainAgent) return false;
            var co = a.Character as CharacterObject;
            var hero = co != null ? co.HeroObject : null;
            if (hero == null || !hero.IsLord || hero == Hero.MainHero) return false;
            if (hero.Clan != null && hero.Clan == Clan.PlayerClan) return false;
            return true;
        }

        private Hold Find(Team t)
        {
            foreach (var h in _holds) if (h.T == t) return h;
            return null;
        }

        /// <summary>Szyk 8 druzyny ustawiony z tylu (raz na druzyne).</summary>
        private Hold HoldOf(Mission m, Team t)
        {
            var h = Find(t);
            if (h != null) return h;
            h = new Hold { T = t };
            _holds.Add(h);
            try
            {
                var f = t.GetFormation(FormationClass.NumberOfRegularFormations);
                if (f == null) return h;
                if (_ally && f.PlayerOwner != null) return h;   // 387: gra oddala ten szyk graczowi - nie ruszamy (lordowie zostaja w szykach gry)
                h.F = f;
                bool planned = false;
                try
                {
                    m.SetFormationPositioningFromDeploymentPlan(f);
                    h.Pos = f.CreateNewOrderWorldPosition(WorldPosition.WorldPositionEnforcedCache.GroundVec3);
                    planned = h.Pos.IsValid && h.Pos.GetNavMesh() != UIntPtr.Zero && m.IsPositionInsideBoundaries(h.Pos.AsVec2);
                    if (planned)
                    {
                        // 381b: plan bez punktu startu dla szyku 8 (WorldFrame.Invalid) daje stare / zerowe polozenie - musi byc blisko lordow
                        foreach (var a in t.ActiveAgents)
                            if (Lord(a)) { if (a.Position.AsVec2.Distance(h.Pos.AsVec2) > MaxPlanFar) planned = false; break; }
                    }
                }
                catch (Exception e) { Stumble("Plan", e); }
                if (!planned)
                {
                    // zapas: miejsce pierwszego lorda (po rozstawieniu - w strefie startowej atakujacych)
                    foreach (var a in t.ActiveAgents) if (Lord(a)) { h.Pos = a.GetWorldPosition(); break; }
                }
                if (!h.Pos.IsValid) { h.F = null; return h; }
                PushBack(m, h);
                f.SetMovementOrder(MovementOrder.MovementOrderMove(h.Pos));
                f.SetFacingOrder(FacingOrder.FacingOrderLookAtEnemy);
                f.SetControlledByAI(false);
                h.Placed = true;
            }
            catch (Exception e) { Stumble("Hold", e); h.F = null; }
            return h;
        }

        /// <summary>Cofniecie pozycji poza zasieg z murow: najblizszy obronca blizej niz SafeDist - punkt dalej po linii od niego (granice pola, siatka).</summary>
        private void PushBack(Mission m, Hold h)
        {
            Vec2 p = h.Pos.AsVec2;
            Vec2 near;
            float d = NearestDefender(m, p, out near);
            h.Dist0 = d;
            h.Dist = d;
            if (d < 0f || d >= SafeDist) return;
            Vec2 dir = p - near;
            if (dir.Length < 0.5f) return;
            dir = dir.Normalized();
            Vec2 best = p;
            float bestD = d;
            for (float k = Step; d + k <= SafeDist + 0.1f; k += Step)
            {
                Vec2 q = p + dir * k;
                if (!m.IsPositionInsideBoundaries(q)) break;
                var w = h.Pos;
                w.SetVec2(q);
                if (w.GetNavMesh() == UIntPtr.Zero) continue;
                best = q;
                bestD = d + k;
            }
            if (bestD > d)
            {
                var w = h.Pos;
                w.SetVec2(best);
                h.Pos = w;
                Vec2 n2;
                h.Dist = NearestDefender(m, best, out n2);
            }
        }

        private static float NearestDefender(Mission m, Vec2 p, out Vec2 at)
        {
            at = p;
            float best = -1f;
            foreach (var t in m.Teams)
            {
                if (!MissionTeams.Valid(t) || t.Side != BattleSideEnum.Defender) continue;
                foreach (var a in t.ActiveAgents)
                {
                    if (a == null || !a.IsHuman) continue;
                    Vec2 q = a.Position.AsVec2;
                    float d = q.Distance(p);
                    if (best < 0f || d < best) { best = d; at = q; }
                }
            }
            return best;
        }

        private void Move(Agent a, Hold h)
        {
            var old = a.Formation;
            if (a.IsUsingGameObject) a.StopUsingGameObject(false, Agent.StopUsingGameObjectFlags.None);
            a.Formation = h.F;
            if (!_broken && h.Placed && h.F.IsAIControlled)
            {
                // 381b: szyk 8 oprozniony wczesniej - gra (Formation.RemoveUnit) wlaczyla mu AI; posilek nie moze pojsc z nim pod mury
                h.F.SetControlledByAI(false);
                h.F.SetMovementOrder(MovementOrder.MovementOrderMove(h.Pos));
                _reAi++;
            }
            h.T.TriggerOnFormationsChanged(h.F);
            h.F.QuerySystem.Expire();
            if (old != null && old != h.F)
            {
                try { h.T.TriggerOnFormationsChanged(old); old.QuerySystem.Expire(); } catch (Exception e) { Stumble("Old", e); }
            }
            Record(a, h);
        }

        private void Record(Agent a, Hold h)
        {
            if (!_moved.Add(a)) return;
            string nm = Name(a);
            if (!_startLogged) _names.Add(nm); else Late(nm, h);
        }

        private void Late(string nm, Hold h)
        {
            _late.Add(nm);
            if (_late.Count <= NamesMax)
                Log.Info("Wodzowie (381): dolaczyl " + nm + " (posilki) - do szyku 8 z tylu, " + (h.Dist >= 0f ? F(h.Dist, "0") + " m od najblizszego obroncy" : "bez obroncow w polu") + ".");
        }

        private void LogStart(Mission m)
        {
            _startLogged = true;
            var sb = new StringBuilder();
            foreach (var h in _holds)
            {
                if (h.F == null) { sb.Append(" | druzyna " + Side(h.T) + ": szyk 8 niedostepny (zostaja w szykach gry)"); continue; }
                sb.Append(" | druzyna " + Side(h.T) + ": szyk 8, ludzi " + h.F.CountOfUnits + ", od najblizszego obroncy " + (h.Dist >= 0f ? F(h.Dist, "0") + " m" : "-")
                          + (h.Dist0 >= 0f && h.Dist > h.Dist0 + 0.5f ? " (plan gry " + F(h.Dist0, "0") + " m, cofniety)" : h.Dist0 >= 0f && h.Dist0 < SafeDist ? " (blizej niz " + F(SafeDist, "0") + " m - dalej sie nie dalo)" : ""));
            }
            string gen = "-";
            try { var g = m.AttackerTeam != null ? m.AttackerTeam.GeneralAgent : null; gen = g != null ? Name(g) + (_moved.Contains(g) ? " (z tylu)" : " (nie lord AI - bez zmian)") : "brak w misji"; } catch { }
            Log.Info("Wodzowie (381): start" + (_ally ? " (387: gracz po stronie atakujacych, szturmem dowodzi AI)" : "") + " | osada " + Settlement() + " | odsunieto " + _names.Count + " bohaterow-lordow atakujacych: " + Join(_names) + " | wodz druzyny " + gen + sb + ".");
        }

        /// <summary>Same wodzowie bez wojska i bez odwodu - odstepuja (inaczej misja bez konca).</summary>
        private void Alone(Mission m)
        {
            if (_alone || _moved.Count == 0) return;
            int others = 0, ours = 0;
            foreach (var t in m.Teams)
            {
                if (!MissionTeams.Valid(t) || t.Side != BattleSideEnum.Attacker) continue;
                foreach (var a in t.ActiveAgents)
                {
                    if (a == null || !a.IsHuman || a.IsRunningAway) continue;
                    if (_moved.Contains(a)) ours++; else others++;
                }
            }
            int rem = -1;                                      // spawner nieznany - nie zgadujemy, czy odwod jeszcze przyjdzie
            try { var sp = m.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>(); if (sp != null) rem = Math.Max(0, sp.NumberOfRemainingAttackerTroops); } catch (Exception e) { Stumble("Spawn", e); }
            // 381b: spawner nieznany - nie wiemy o odwodzie; czekamy dluzej (UnknownHits skanow bez zadnego innego atakujacego), potem odstepujemy
            if (ours == 0 || others > 0 || rem > 0) { _aloneHits = 0; return; }
            if (++_aloneHits < (rem < 0 ? UnknownHits : 2)) return;
            _alone = true;
            int sent = 0;
            foreach (var a in _moved)
            {
                try
                {
                    if (a == null || !a.IsActive() || a.IsRunningAway || !MissionTeams.Valid(a.Team)) continue;
                    var wp = m.GetClosestFleePositionForAgent(a);
                    if (!wp.IsValid) continue;
                    a.Retreat(wp);
                    sent++;
                }
                catch (Exception e) { Stumble("Alone", e); }
            }
            Log.Info("Wodzowie (381): zostali sami (" + ours + " bohaterow, wojska ani odwodu atakujacych brak) - odstepuja: odwrot " + sent + ".");
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            if (!_active || _ended) return;
            try
            {
                if (affectedAgent == null || !_moved.Contains(affectedAgent)) return;
                if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;
                string how = blow.IsMissile ? "pocisk" : (affectorAgent != null && affectorAgent.IsHuman ? "wrecz" : "inne");
                string nm = Name(affectedAgent) + " (" + how + ")";
                if (agentState == AgentState.Killed) _killed.Add(nm); else _wounded.Add(nm);
            }
            catch (Exception e) { Stumble("Removed", e); }
        }

        protected override void OnEndMission()
        {
            try
            {
                if (_active && _started && (_moved.Count > 0 || _holds.Count > 0))
                    Log.Info("Wodzowie (381): koniec misji | osada " + Settlement() + " | odsunietych " + _moved.Count + " (na starcie " + _names.Count + ", posilki " + _late.Count
                             + ") | mimo to zabici " + _killed.Count + (_killed.Count > 0 ? " [" + Join(_killed) + "]" : "") + ", ranni (nieprzytomni) " + _wounded.Count
                             + (_wounded.Count > 0 ? " [" + Join(_wounded) + "]" : "") + " | zostali sami: " + (_alone ? "tak (odstapili)" : "nie") + ", odwrot (371/378): "
                             + (_broken ? "tak" : "nie") + " | AI szyku przywrocone przez gre i cofniete " + _reAi + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            base.OnEndMission();
        }

        private static string Join(List<string> l)
        {
            if (l.Count == 0) return "-";
            if (l.Count <= NamesMax) return string.Join(", ", l);
            return string.Join(", ", l.GetRange(0, NamesMax)) + " ... (+" + (l.Count - NamesMax) + ")";
        }

        private static string Name(Agent a)
        {
            try { return a.Name != null ? a.Name.ToString() : "?"; } catch { return "?"; }
        }

        private string Side(Team t)
        {
            try { return Mission != null && t == Mission.AttackerTeam ? "atakujacych" : "sojusznikow atakujacych"; } catch { return "?"; }
        }

        private string Settlement()
        {
            try { var st = _me != null ? _me.MapEventSettlement : null; return st != null ? st.StringId : "-"; } catch { return "-"; }
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Info("Wodzowie (381): potkniecie " + where + " - " + e.GetType().Name + ": " + e.Message); } catch { }
        }
    }
}
