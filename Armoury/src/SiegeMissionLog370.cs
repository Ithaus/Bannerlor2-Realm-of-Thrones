using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 370 (Jeff 09.11: "tak zrob logi oblezen aby bylo wiadomo co sie dzieje"; oblezenie Goldentooth - obie strony staly 17 minut, taran / wieza / drabiny
    /// nie ruszyly, a log nie mowil, KTO stoi i dlaczego). TYLKO POMIAR: zadnej zmiany stanu misji, zadnej latki, zadnego rozkazu.
    /// Misja szturmu (Mission.IsSiegeBattle; nie wypad, nie bitwa polowa), klucz SiegeMissionLog. Odczyt: po rozstawieniu (start), co 30 s czasu misji i przy
    /// koncu misji. Na kazdy odczyt linie "Oblezenie misja (370)": po jednej na strone (A = atakujacy, O = obroncy) - taktyka kazdej druzyny
    /// (TeamAIComponent._currentTactic, stan TacticBreachWalls._tacticState / TacticDefendCastle.CurrentTacticState), szyki (ludzi, odlaczonych,
    /// zachowanie AI.ActiveBehavior, rozkaz MovementOrderEnum, czy pozycja rozkazu wazna - Formation.OrderPositionIsValid, odleglosc do pozycji rozkazu,
    /// do najblizszego szyku wroga - CachedClosestEnemyFormationDistanceSquared, ciosy szyku w ostatnich 30 s) - i jedna na machiny (taran, wieze, drabiny:
    /// ludzie na StandingPoint / stoisk, dojechal, odleglosc do celu, szyki uzytkownikow, wylaczone dla AI; machiny miotajace, bramy, wylomy, strzaly obu
    /// stron w 30 s, potkniecia). Przy pierwszym odczycie dodatkowo lista szykow z niewazna pozycja rozkazu (silnik pisze to w rgl jako
    /// "Formation order position is not valid. Team: T, Formation: F" - tu ta sama numeracja T/F).
    /// Odczyt pozycji: tylko wlasciwosci (OrderPosition, OrderPositionIsValid) - nie MovementOrder.GetPosition (ta liczy i zapisuje pamiec podreczna rozkazu).
    /// Koszt: co 30 s jeden przeglad MissionObjects i szykow; co klatke tylko porownanie czasu; per cios / pocisk - licznik. Wyjatki per obiekt / szyk
    /// liczone (potkniecia), pierwszy slad do logu, bez globalnego wylacznika.
    /// </summary>
    internal sealed class SiegeMissionLog370 : MissionLogic
    {
        private const float Every = 30f, StartDelay = 1f, NoDeployStart = 10f;
        private const int ErrMax = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] FcName = { "Pie", "Luk", "Kon", "KLuk", "Har", "CPie", "LKon", "CKon", "Wodz", "Straz" };

        private static FieldInfo _fTactic, _fBreachState, _fLanes;
        private static bool _reflTried;

        private bool _known, _siege, _started, _ended, _firstDone;
        private float _next, _startAt = -1f;
        private int _readings, _stumbles, _errs;
        private int _shotsA, _shotsD, _shotsAllA, _shotsAllD;
        private readonly Dictionary<Formation, int> _hits = new Dictionary<Formation, int>();
        private readonly StringBuilder _sb = new StringBuilder(1024);

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private bool Siege(Mission m)
        {
            if (!_known)
            {
                try { _siege = m != null && m.IsSiegeBattle; } catch { _siege = false; }
                _known = true;
            }
            return _siege;
        }

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            if (!_started || _ended || !_siege) return;
            try
            {
                var f = affectorAgent != null && affectedAgent != null && affectedAgent.IsHuman ? affectorAgent.Formation : null;
                if (f == null) return;
                int n;
                _hits.TryGetValue(f, out n);
                _hits[f] = n + 1;
            }
            catch { _stumbles++; }
        }

        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            if (!_started || _ended || !_siege) return;
            try
            {
                var t = shooterAgent != null ? shooterAgent.Team : null;
                if (t == null) return;
                if (t.Side == BattleSideEnum.Attacker) { _shotsA++; _shotsAllA++; }
                else if (t.Side == BattleSideEnum.Defender) { _shotsD++; _shotsAllD++; }
            }
            catch { _stumbles++; }
        }

        public override void OnMissionTick(float dt)
        {
            if (_ended) return;
            var m = Mission;
            if (m == null) return;
            float now;
            try
            {
                now = m.CurrentTime;
                if (!_started)
                {
                    if (!Siege(m)) { _ended = true; return; }
                    bool deployed = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && now >= NoDeployStart);
                    if (!deployed) return;
                    if (_startAt < 0f) { _startAt = now + StartDelay; return; }   // klatka po rozstawieniu: szyki dostaja pierwsze rozkazy
                    if (now < _startAt) return;
                    _started = true;
                    Reading(m, "start");
                    _next = now + Every;
                    return;
                }
                if (m.MissionEnded) return;   // linia konca w OnEndMission
                if (now < _next) return;
                _next = now + Every;
                Reading(m, null);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        protected override void OnEndMission()
        {
            try
            {
                var m = Mission;
                if (_started && !_ended && m != null) Reading(m, "koniec");
                if (_started)
                    Log.Info("Oblezenie misja (370): koniec | odczytow " + _readings + " | strzaly calej misji: A " + _shotsAllA + ", O " + _shotsAllD
                             + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            _hits.Clear();
            base.OnEndMission();
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("SiegeMissionLog370." + where, e); } catch { }
        }

        // ------------------------------------------------------------ odczyt

        private void Reading(Mission m, string tag)
        {
            _readings++;
            string head = "Oblezenie misja (370): t=" + m.CurrentTime.ToString("0", Inv) + "s" + (tag != null ? " (" + tag + ")" : "");
            List<string> bad = !_firstDone ? new List<string>() : null;
            Side(m, BattleSideEnum.Attacker, head, bad);
            Side(m, BattleSideEnum.Defender, head, bad);
            Machines(m, head);
            if (bad != null)
            {
                _firstDone = true;
                Log.Info("Oblezenie misja (370): niewazne pozycje rozkazow przy pierwszym odczycie: " + (bad.Count == 0 ? "brak" : string.Join("; ", bad)) + ".");
            }
            _hits.Clear();
            _shotsA = 0; _shotsD = 0;
        }

        private void Side(Mission m, BattleSideEnum side, string head, List<string> bad)
        {
            var sb = _sb;
            sb.Length = 0;
            sb.Append(head).Append(" | strona ").Append(side == BattleSideEnum.Attacker ? "A" : "O");
            int teamsN = 0;
            var teams = m.Teams;
            for (int i = 0; teams != null && i < teams.Count; i++)
            {
                Team t;
                try
                {
                    t = teams[i];
                    if (!MissionTeams.Valid(t) || t.Side != side) continue;
                }
                catch (Exception e) { Stumble("Druzyna", e); continue; }
                teamsN++;
                try
                {
                    sb.Append(" | T").Append(t.TeamIndex).Append(t.IsPlayerTeam ? " gracz" : (t.IsPlayerAlly ? " sojusz" : ""))
                      .Append(" taktyka: ").Append(TacticText(t)).Append(" | szyki:");
                }
                catch (Exception e) { Stumble("Taktyka", e); sb.Append(" taktyka: ? | szyki:"); }
                int fN = 0;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    try
                    {
                        var f = fl[j];
                        if (f == null || f.CountOfUnits <= 0) continue;
                        fN++;
                        int idx = (int)f.FormationIndex;
                        sb.Append(' ').Append('F').Append(idx).Append(' ').Append(idx >= 0 && idx < FcName.Length ? FcName[idx] : f.FormationIndex.ToString())
                          .Append(f.IsAIControlled ? "" : "(gracz)")
                          .Append(" ludzi ").Append(f.CountOfUnits);
                        int det = f.CountOfDetachedUnits;
                        if (det > 0) sb.Append(" (odl. ").Append(det).Append(')');
                        var b = f.AI != null ? f.AI.ActiveBehavior : null;
                        sb.Append(", zach ").Append(BehName(b));
                        var ord = f.GetReadonlyMovementOrderReference().OrderEnum;
                        bool valid = f.OrderPositionIsValid;
                        sb.Append(", rozkaz ").Append(ord.ToString()).Append(" poz ").Append(valid ? "TAK" : "NIE");
                        var avg = f.CachedAveragePosition;
                        if (valid) sb.Append(" do celu ").Append(avg.Distance(f.OrderPosition).ToString("0", Inv)).Append('m');
                        float d2 = f.CachedClosestEnemyFormationDistanceSquared;
                        sb.Append(", wrog ").Append(d2 >= 0f && d2 < float.MaxValue * 0.5f ? ((float)Math.Sqrt(d2)).ToString("0", Inv) + "m" : "-");
                        int h;
                        _hits.TryGetValue(f, out h);
                        sb.Append(", ciosy30s ").Append(h).Append(';');
                        if (bad != null && !valid)
                            bad.Add("T" + t.TeamIndex + " F" + idx + " (" + (side == BattleSideEnum.Attacker ? "A" : "O") + ", ludzi " + f.CountOfUnits + ", rozkaz " + ord + ", zach " + BehName(b) + ")");
                    }
                    catch (Exception e) { Stumble("Szyk", e); sb.Append(" ?;"); }
                }
                if (fN == 0) sb.Append(" brak");
            }
            if (teamsN == 0) sb.Append(" | brak druzyn");
            Log.Info(sb.ToString());
        }

        private void Machines(Mission m, string head)
        {
            var sb = _sb;
            sb.Length = 0;
            sb.Append(head).Append(" | machiny:");
            var rams = new StringBuilder(); var towers = new StringBuilder(); var ladders = new StringBuilder();
            int nRam = 0, nTower = 0, nLad = 0, nLadUp = 0, nRanged = 0, nRangedMen = 0, nGate = 0, nGateDown = 0, nGateOpen = 0, nBreach = 0;
            var all = m.MissionObjects;
            int n = all != null ? all.Count : 0;
            for (int i = 0; i < n; i++)
            {
                try
                {
                    var o = all[i];
                    if (o == null) continue;
                    if (o is BatteringRam r)
                    {
                        nRam++;
                        rams.Append(rams.Length > 0 ? "; " : "").Append(MachText(r, r.TargetCastlePosition, r.HasArrivedAtTarget, r.IsDestroyed))
                            .Append(", stan ").Append(r.State.ToString());
                    }
                    else if (o is SiegeTower t)
                    {
                        nTower++;
                        towers.Append(towers.Length > 0 ? "; " : "").Append(MachText(t, t.TargetCastlePosition, t.HasArrivedAtTarget, t.IsDestroyed))
                              .Append(", brama ").Append(t.State.ToString());
                    }
                    else if (o is SiegeLadder l)
                    {
                        nLad++;
                        var st = l.State;
                        if (st != SiegeLadder.LadderState.OnLand && st != SiegeLadder.LadderState.FallToLand) nLadUp++;
                        ladders.Append(ladders.Length > 0 ? "; " : "").Append(st.ToString()).Append(' ')
                               .Append(l.UserCountIncludingInStruckAction).Append('/').Append(Points(l))
                               .Append(Users(l)).Append(l.IsDisabled ? " wyl" : "").Append(l.IsDisabledForAI ? " AIwyl" : "");
                    }
                    else if (o is RangedSiegeWeapon w)
                    {
                        if (w.IsDisabled || w.IsDestroyed) continue;
                        nRanged++;
                        nRangedMen += w.UserCountIncludingInStruckAction;
                    }
                    else if (o is CastleGate g)
                    {
                        if (!g.IsDestructible) continue;
                        nGate++;
                        if (g.IsDestroyed) nGateDown++;
                        else if (g.IsGateOpen) nGateOpen++;
                    }
                    else if (o is WallSegment ws)
                    {
                        if (ws.IsBreachedWall) nBreach++;
                    }
                }
                catch (Exception e) { Stumble("Machina", e); }
            }
            sb.Append(" taran ").Append(nRam).Append(nRam > 0 ? " [" + rams + "]" : "")
              .Append(" | wieza ").Append(nTower).Append(nTower > 0 ? " [" + towers + "]" : "")
              .Append(" | drabiny ").Append(nLad).Append(" (w gorze ").Append(nLadUp).Append(')').Append(nLad > 0 ? " [" + ladders + "]" : "")
              .Append(" | miotajace ").Append(nRanged).Append(" (ludzi ").Append(nRangedMen).Append(')')
              .Append(" | bramy ").Append(nGate).Append(" (rozbite ").Append(nGateDown).Append(", otwarte ").Append(nGateOpen).Append(')')
              .Append(" | wylomy ").Append(nBreach)
              .Append(" | strzaly 30s: O ").Append(_shotsD).Append(", A ").Append(_shotsA)
              .Append(" | potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>Taran / wieza: ludzie na stoiskach / stoisk, dojechal, odleglosc do celu (brama / odcinek muru), szyki uzytkownikow, wylaczenia.</summary>
        private static string MachText(UsableMachine u, MissionObject target, bool arrived, bool destroyed)
        {
            var s = new StringBuilder();
            s.Append("ludzi ").Append(u.UserCountIncludingInStruckAction).Append('/').Append(Points(u))
             .Append(", dojechal ").Append(arrived ? "TAK" : "NIE");
            if (destroyed) s.Append(", ZNISZCZONA");
            try
            {
                var k = target as ICastleKeyPosition;
                var me = u.GameEntity.GlobalPosition;
                if (k != null) s.Append(", do celu ").Append(me.AsVec2.Distance(k.GetPosition().AsVec2).ToString("0", Inv)).Append('m');
                else if (target != null) s.Append(", do celu ").Append(me.AsVec2.Distance(target.GameEntity.GlobalPosition.AsVec2).ToString("0", Inv)).Append('m');
                else s.Append(", cel -");
            }
            catch { s.Append(", do celu ?"); }
            s.Append(Users(u));
            if (u.IsDisabled) s.Append(", wyl");
            if (u.IsDisabledForAI) s.Append(", AIwyl");
            if (u.IsDeactivated) s.Append(", nieczynna");
            return s.ToString();
        }

        private static int Points(UsableMachine u)
        {
            var sp = u.StandingPoints;
            return sp != null ? sp.Count : 0;
        }

        /// <summary>Szyki uzytkownikow machiny (UserFormations): " szyki T1F2,T1F5" albo " szyki -".</summary>
        private static string Users(UsableMachine u)
        {
            var uf = u.UserFormations;
            if (uf == null || uf.Count == 0) return ", szyki -";
            var s = new StringBuilder(", szyki ");
            for (int i = 0; i < uf.Count; i++)
            {
                var f = uf[i];
                if (f == null) continue;
                if (i > 0) s.Append(',');
                s.Append('T').Append(f.Team != null ? f.Team.TeamIndex : -1).Append('F').Append((int)f.FormationIndex);
            }
            return s.ToString();
        }

        private static string BehName(BehaviorComponent b)
        {
            if (b == null) return "-";
            string n = b.GetType().Name;
            if (n.StartsWith("RBMBehavior", StringComparison.Ordinal)) return "RBM:" + n.Substring(11);
            if (n.StartsWith("Behavior", StringComparison.Ordinal) && n.Length > 8) return n.Substring(8);
            return n;
        }

        // ------------------------------------------------------------ taktyka (refleksja raz na proces, tylko odczyt)

        private static void Refl()
        {
            if (_reflTried) return;
            _reflTried = true;
            try { _fTactic = AccessTools.Field(typeof(TeamAIComponent), "_currentTactic"); } catch { _fTactic = null; }
            try { _fBreachState = AccessTools.Field(typeof(TacticBreachWalls), "_tacticState"); } catch { _fBreachState = null; }
            try { _fLanes = AccessTools.Field(typeof(TacticBreachWalls), "_lanesInUse"); } catch { _fLanes = null; }
        }

        private static string TacticText(Team t)
        {
            Refl();
            var ai = t.TeamAI;
            if (ai == null) return "brak AI druzyny";
            if (_fTactic == null) return "? (brak pola _currentTactic)";
            var tac = _fTactic.GetValue(ai) as TacticComponent;
            if (tac == null) return "-";
            string n = tac.GetType().Name;
            if (n.StartsWith("Tactic", StringComparison.Ordinal) && n.Length > 6) n = n.Substring(6);
            if (tac is TacticBreachWalls)
            {
                string st = "?", lanes = "?";
                try { if (_fBreachState != null) st = Convert.ToString(_fBreachState.GetValue(tac), Inv); } catch { }
                try { if (_fLanes != null) lanes = Convert.ToString(_fLanes.GetValue(tac), Inv); } catch { }
                return n + " (" + st + ", pasy " + lanes + ")";
            }
            if (tac is TacticDefendCastle dc)
            {
                string st = "?";
                try { st = dc.CurrentTacticState.ToString(); } catch { }
                return n + " (" + st + ")";
            }
            return n;
        }
    }
}
