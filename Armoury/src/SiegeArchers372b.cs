using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 372b (Jeff 09.11, obrona Goldentooth: "a lucznicy strzelaja slabo"; odpowiedz na pytanie o strzelcow w jego oddzialach: "Przenies strzelcow na mury").
    /// Dowod (log 370 + diagnoza 372 (C)): szyk gracza F3 (93-144 ludzi) stal przy punkcie kluczowym (DefendCastleKeyPosition, rozkaz Move) i zadawal
    /// 0-13 ciosow / 30 s, a szyk F1 na murze (58 lucznikow) 9-27. Szyki gracza (!IsAIControlled) TacticDefendCastle po rozstawieniu nie dowodzi -
    /// FormationAI liczy im zachowanie, ale wykonuje je tylko szyk AI (FormationAI.TickOccasionally); szyk gracza idzie za ostatnim rozkazem.
    /// Zasada (JEDNORAZOWO, pierwszy tik misji >= 1 s po Mission.IsDeploymentFinished - jak odczyt 'start' w 370, w ktorym szyki gracza mialy juz ShootFromCastleWalls): tylko misja szturmu (Mission.IsSiegeBattle), gracz OBRONCA
    /// i wodz swojej druzyny (PlayerTeam.IsPlayerGeneral), druzyna z TeamAISiegeDefender (stanowiska ArcherPositions).
    ///  - Szyki docelowe = szyki gracza, ktore gra ustawila do strzelania z murow: aktywne zachowanie BehaviorShootFromCastleWalls ze stanowiskiem
    ///    (ArcherPosition, nadaje je TacticDefendCastle.DistributeRangedFormations takze szykom gracza) ORAZ pozycja rozkazu szyku naprawde przy tym
    ///    stanowisku (&lt;= max(15 m, pol szerokosci TacticalPosition + 5 m)). Szyk strzelcow, ktory gracz przestawil gdzie indziej, nie jest celem
    ///    ani zrodlem (jego reczne ustawienie zostaje).
    ///  - Zrodla = pozostale niepuste szyki gracza (!IsAIControlled). Przekladani ludzie: aktywni, AI, z bronia dystansowa i amunicja
    ///    (Agent.IsRangedCached - luk / kusza; oszczepy nie), bez konia (w oblezeniu i tak piesi), NIE bohaterowie (gracz, towarzysze, gubernator),
    ///    nie chorazowie (Banner), nie odlaczeni do machin / stoisk (IsDetachedFromFormation, CurrentlyUsedGameObject).
    ///  - Przelozenie: Agent.Formation = cel (to samo, co robi gra w Formation.TransferUnitsAux / TransferUnitsWithPriorityFunction - tamte wybieraja
    ///    ludzi same, bez filtra broni), potem Team.TriggerOnFormationsChanged dla kazdego ruszonego szyku i wygaszenie QuerySystem. Rownomiernie:
    ///    kazdy kolejny czlowiek do szyku docelowego, ktory ma w tej chwili najmniej ludzi. Nowi ida za rozkazem szyku (Move na stanowisko).
    ///  - Brak szykow docelowych: puste szyki gracza (indeksy 0-7, najpierw Ranged) dostaja ludzi i rozkazy, jakie daje BehaviorShootFromCastleWalls
    ///    (Move na TacticalPosition stanowiska, patrz w kierunku stanowiska, rozproszony, ognia do woli, szerokosc stanowiska) - wolne stanowiska
    ///    (bez szyku gracza przy nich) najblizsze sredniej pozycji wroga; szykow min(wolne szyki, wolne stanowiska, ceil(ludzi / 15)).
    /// Dalej dowodzi gracz. Klucz SiegeDefenderArchersToWalls. Wyjatek per czlowiek / szyk = potkniecie (pierwsze 3 do logu), bez globalnego wylacznika.
    /// </summary>
    internal sealed class SiegeArchers372b : MissionLogic
    {
        private const string P = "Strzelcy na mury (372b): ";
        private const float Delay = 1f, NearMin = 15f, PerNewFormation = 15f;
        private const int ErrMax = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _errs;

        private bool _done;
        private float _at = -1f;
        private int _stumbles;

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        public override void OnMissionTick(float dt)
        {
            if (_done) return;
            var m = Mission;
            if (m == null) return;
            try
            {
                if (m.MissionEnded) { _done = true; return; }
                if (!m.IsDeploymentFinished) return;                       // tylko po rozstawieniu (bez rozstawienia - nic)
                float now = m.CurrentTime;
                if (_at < 0f) { _at = now + Delay; return; }               // ResetTactic obroncy (TeamAISiegeDefender.OnDeploymentFinished) juz rozdal stanowiska
                if (now < _at) return;
                _done = true;
                Run(m);
            }
            catch (Exception e) { _done = true; Stumble("Tick", e); }
        }

        private sealed class Target
        {
            internal Formation F;
            internal int Post = -1;
            internal int Added;
            internal bool New;
        }

        private void Run(Mission m)
        {
            var s = Settings.Current;
            if (s == null || !s.SiegeDefenderArchersToWalls) return;
            bool siege;
            try { siege = m.IsSiegeBattle; } catch { siege = false; }
            if (!siege) return;
            var team = m.PlayerTeam;
            if (team == null || team.Side != BattleSideEnum.Defender) return;
            if (!team.IsPlayerGeneral) { Log.Info(P + "gracz broni, ale nie dowodzi druzyna - szykami kieruje AI (TacticDefendCastle), nic nie ruszam."); return; }
            var def = team.TeamAI as TeamAISiegeDefender;
            var posts = def != null ? def.ArcherPositions : null;
            if (posts == null || posts.Count == 0) { Log.Info(P + "brak stanowisk strzelcow (ArcherPositions) - nic nie ruszam."); return; }

            // --- szyki docelowe i zrodla
            var targets = new List<Target>();
            var sources = new List<Formation>();
            var keptAway = new List<string>();
            var takenPost = new bool[posts.Count];
            foreach (var f in team.FormationsIncludingEmpty)
            {
                try
                {
                    if (f == null || f.CountOfUnits <= 0) continue;
                    if (f.IsAIControlled) continue;                          // szyk oddany AI - prowadzi go taktyka gry
                    var b = f.AI != null ? f.AI.ActiveBehavior as BehaviorShootFromCastleWalls : null;
                    int post = b != null && b.ArcherPosition != null ? PostIndex(posts, b.ArcherPosition) : -1;
                    if (post >= 0)
                    {
                        if (AtPost(f, posts[post])) { targets.Add(new Target { F = f, Post = post }); takenPost[post] = true; }
                        else keptAway.Add("F" + (int)f.FormationIndex);       // strzelcy przestawieni przez gracza - nie ruszam
                        continue;
                    }
                    // szyk z przewaga strzelcow (QuerySystem.IsRangedFormation - jak lista _rangedFormations taktyki) stojacy przy stanowisku, choc zachowanie
                    // jeszcze nie przydzielone - tez cel (nie zrodlo)
                    if (f.QuerySystem.IsRangedFormation)
                    {
                        int near = -1;
                        for (int i = 0; i < posts.Count; i++) if (AtPost(f, posts[i])) { near = i; break; }
                        if (near >= 0) { targets.Add(new Target { F = f, Post = near }); takenPost[near] = true; continue; }
                    }
                    sources.Add(f);
                }
                catch (Exception e) { Stumble("Szyk", e); }
            }
            // stanowiska, przy ktorych juz stoi jakis szyk gracza (np. przestawiony recznie) - nie sa wolne
            foreach (var f in team.FormationsIncludingEmpty)
            {
                try
                {
                    if (f == null || f.CountOfUnits <= 0 || !f.OrderPositionIsValid) continue;
                    for (int i = 0; i < posts.Count; i++) if (!takenPost[i] && AtPost(f, posts[i])) takenPost[i] = true;
                }
                catch (Exception e) { Stumble("Wolne", e); }
            }

            // --- ludzie do przelozenia
            var movers = new List<Agent>();
            int heroes = 0, detached = 0, mounted = 0, banner = 0;
            var fromCount = new Dictionary<Formation, int>();
            foreach (var f in sources)
            {
                try
                {
                    var list = new List<Agent>();
                    f.ApplyActionOnEachUnit(a => list.Add(a));
                    foreach (var a in list)
                    {
                        try
                        {
                            if (a == null || !a.IsActive() || !a.IsHuman || !a.IsRangedCached) continue;
                            if (a.IsHero || a.IsMainAgent || a.IsPlayerControlled || !a.IsAIControlled) { heroes++; continue; }
                            if (a.HasMount) { mounted++; continue; }
                            if (a.Banner != null) { banner++; continue; }
                            if (a.IsDetachedFromFormation || a.CurrentlyUsedGameObject != null) { detached++; continue; }
                            movers.Add(a);
                            int c; fromCount.TryGetValue(f, out c); fromCount[f] = c + 1;
                        }
                        catch (Exception e) { Stumble("Czlowiek", e); }
                    }
                }
                catch (Exception e) { Stumble("Zrodlo", e); }
            }
            string skipped = "bohaterowie " + heroes + ", konni " + mounted + ", chorazowie " + banner + ", przy machinach " + detached
                             + (keptAway.Count > 0 ? ", szyki strzelcow poza stanowiskiem (zostaja) " + string.Join(",", keptAway) : "");
            if (movers.Count == 0)
            {
                Log.Info(P + "nikogo do przelozenia | stanowisk " + posts.Count + ", szykow gracza na stanowiskach " + TargetsText(targets) + " | pominieci: " + skipped + ".");
                return;
            }

            // --- brak szykow na stanowiskach: puste szyki gracza dostaja stanowiska jak od BehaviorShootFromCastleWalls
            if (targets.Count == 0) NewTargets(team, posts, takenPost, targets, movers.Count);
            if (targets.Count == 0)
            {
                Log.Info(P + "brak szyku na stanowisku i brak wolnego szyku / stanowiska - " + movers.Count + " strzelcow zostaje na miejscu | stanowisk " + posts.Count
                         + " | pominieci: " + skipped + ".");
                return;
            }

            // --- przelozenie: kazdy do szyku docelowego z najmniejsza liczba ludzi (OnMassUnitTransferStart/End jak ArmyReview.PinDeploy)
            var touched = new List<Formation>();
            foreach (var kv in fromCount) touched.Add(kv.Key);
            foreach (var t in targets) if (!touched.Contains(t.F)) touched.Add(t.F);
            var started = new List<Formation>();
            foreach (var f in touched) { try { f.OnMassUnitTransferStart(); started.Add(f); } catch (Exception e) { Stumble("Start", e); } }
            int moved = 0;
            try
            {
                foreach (var a in movers)
                {
                    try
                    {
                        Target best = null;
                        foreach (var t in targets) if (best == null || t.F.CountOfUnits < best.F.CountOfUnits) best = t;
                        a.Formation = best.F;
                        best.Added++;
                        moved++;
                    }
                    catch (Exception e) { Stumble("Przelozenie", e); }
                }
            }
            finally
            {
                foreach (var f in touched)
                {
                    try { team.TriggerOnFormationsChanged(f); if (started.Contains(f)) f.OnMassUnitTransferEnd(); f.QuerySystem.Expire(); }
                    catch (Exception e) { Stumble("Odswiezenie", e); }
                }
            }
            try { team.QuerySystem.ExpireAfterUnitAddRemove(); } catch (Exception e) { Stumble("Druzyna", e); }
            // szyk nowy: rozkazy po dodaniu ludzi (pierwszy czlowiek ustawil pozycje szyku na swojej - Agent.Formation / SetPositioning)
            foreach (var t in targets) if (t.New && t.Added > 0) { try { OrderToPost(t.F, posts[t.Post]); } catch (Exception e) { Stumble("Rozkaz", e); } }

            var from_ = new StringBuilder();
            foreach (var kv in fromCount) from_.Append(from_.Length > 0 ? ", " : "").Append('F').Append((int)kv.Key.FormationIndex).Append(' ').Append(kv.Value);
            Log.Info(P + "przelozono " + moved + " z " + movers.Count + " (z: " + from_ + ") -> " + TargetsText(targets) + " | stanowisk " + posts.Count
                     + " | pominieci: " + skipped + " | potkniecia " + _stumbles + ".");
            if (moved > 0)
            {
                try { InformationManager.DisplayMessage(new InformationMessage(moved + " archers and crossbowmen sent up to the walls.", Color.FromUint(0xFF9ACD65u))); } catch { }
            }
        }

        /// <summary>Puste szyki gracza (0-7, najpierw Ranged) na wolne stanowiska najblizsze wroga.</summary>
        private void NewTargets(Team team, List<ArcherPosition> posts, bool[] takenPost, List<Target> targets, int men)
        {
            var free = new List<int>();
            for (int i = 0; i < posts.Count; i++) if (!takenPost[i] && posts[i] != null && posts[i].Entity != null) free.Add(i);
            if (free.Count == 0) return;
            Vec2 enemy = Vec2.Invalid;
            try { enemy = team.QuerySystem.AverageEnemyPosition; } catch (Exception e) { Stumble("Wrog", e); }
            if (enemy.IsValid)
                free.Sort((x, y) => PostPos(posts[x]).AsVec2.DistanceSquared(enemy).CompareTo(PostPos(posts[y]).AsVec2.DistanceSquared(enemy)));
            var empty = new List<Formation>();
            var ranged = team.GetFormation(FormationClass.Ranged);
            if (ranged != null && ranged.CountOfUnits == 0 && !ranged.IsAIControlled) empty.Add(ranged);
            for (int i = 0; i < 8; i++)
            {
                var f = team.GetFormation((FormationClass)i);
                if (f != null && f.CountOfUnits == 0 && !f.IsAIControlled && !empty.Contains(f)) empty.Add(f);
            }
            int k = Math.Min(Math.Min(empty.Count, free.Count), Math.Max(1, (int)Math.Ceiling(men / PerNewFormation)));
            for (int i = 0; i < k; i++)
            {
                targets.Add(new Target { F = empty[i], Post = free[i], New = true });   // rozkazy po dodaniu ludzi (pusty szyk nie ma pozycji)
            }
        }

        /// <summary>Rozkazy szyku jak BehaviorShootFromCastleWalls (OnArcherPositionSet + OnBehaviorActivatedAux + TickOccasionally).</summary>
        private static void OrderToPost(Formation f, ArcherPosition p)
        {
            var tp = p.TacticalArcherPosition;
            if (tp != null)
            {
                f.SetMovementOrder(MovementOrder.MovementOrderMove(tp.Position));
                f.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(tp.Direction));
            }
            else
            {
                f.SetMovementOrder(MovementOrder.MovementOrderMove(p.Entity.GlobalPosition.ToWorldPosition()));
                f.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(p.Entity.GetGlobalFrame().rotation.f.AsVec2));
            }
            f.SetArrangementOrder(ArrangementOrder.ArrangementOrderScatter);
            f.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
            f.SetFormOrder(tp != null ? FormOrder.FormOrderCustom(tp.Width) : FormOrder.FormOrderWide);
        }

        private static Vec3 PostPos(ArcherPosition p)
        {
            var tp = p.TacticalArcherPosition;
            return tp != null ? tp.Position.GetGroundVec3() : p.Entity.GlobalPosition;
        }

        /// <summary>Pozycja rozkazu szyku przy stanowisku: &lt;= max(15 m, pol szerokosci stanowiska + 5 m).</summary>
        private static bool AtPost(Formation f, ArcherPosition p)
        {
            if (p == null || p.Entity == null || !f.OrderPositionIsValid) return false;
            var tp = p.TacticalArcherPosition;
            float r = Math.Max(NearMin, tp != null ? tp.Width * 0.5f + 5f : NearMin);
            return f.OrderPosition.DistanceSquared(PostPos(p).AsVec2) <= r * r;
        }

        private static int PostIndex(List<ArcherPosition> posts, GameEntity e)
        {
            for (int i = 0; i < posts.Count; i++) if (posts[i] != null && posts[i].Entity == e) return i;
            return -1;
        }

        private static string TargetsText(List<Target> targets)
        {
            if (targets.Count == 0) return "brak";
            var sb = new StringBuilder();
            foreach (var t in targets)
                sb.Append(sb.Length > 0 ? ", " : "").Append('F').Append((int)t.F.FormationIndex).Append(t.New ? " (nowy)" : "").Append(" @st").Append(t.Post)
                  .Append(" +").Append(t.Added).Append(" = ").Append(t.F.CountOfUnits);
            return sb.ToString();
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("SiegeArchers372b." + where + " (potkniecie " + _stumbles + ")", e); } catch { }
        }
    }
}
