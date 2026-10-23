using System;
using SandBox.Tournaments.MissionLogics;
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
    /// m111-walka faza 1 (24.10): takze flaga POJEDYNKU (turniej, arena, pojedynek ROT) i runda turnieju dla DuelAi / DuelCraft / DuelGuard /
    /// BattleWind. Rodzaj liczony w EarlyStart (OnBehaviorInitialize nie biegnie dla zachowan dodanych w OnMissionBehaviorInitialize -
    /// Mission.cs:3809-3816; EarlyStart wszystkich zachowan idzie przed AfterStart kontrolera areny, w ktorym rodzi sie gracz) i jeszcze raz
    /// w AfterStart. Postfiks asynchroniczny czyta tylko statyki _duel / _m / Round (nigdy MissionTeams.Kind). Zasada pojedynku: rodzaj "turniej"
    /// albo "arena", albo bez bitwy gracza na mapie (rodzaj "inna") i zachowanie z "Duel" w nazwie typu - nazwa do logu raz na misje.
    /// </summary>
    internal sealed class BattleSense : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private static WeakReference _m;
        private static bool _battle;
        // m111-walka: pojedynek (turniej, arena, pojedynek ROT) i runda turnieju (0..3; arena i pojedynek 0)
        private static bool _duel;
        private static string _duelKind = "-";
        private static TournamentBehavior _tb;
        private static bool _duelDone;
        internal static int Round;

        /// <summary>Misja to bitwa na mapie, kryjowka albo bitwa wlasna (rodzaj z linii NM, liczony raz w AfterStart).</summary>
        internal static bool InBattle(Mission m)
        {
            var w = _m;
            return _battle && w != null && m != null && ReferenceEquals(w.Target, m);
        }

        /// <summary>m111-walka: misja to turniej, arena albo pojedynek ROT (liczone w EarlyStart / AfterStart, potem tylko czytane - takze z ticku
        /// asynchronicznego).</summary>
        internal static bool InDuel(Mission m)
        {
            var w = _m;
            return _duel && w != null && m != null && ReferenceEquals(w.Target, m);
        }

        /// <summary>m111-walka: biezaca misja to turniej (ItemReq bez limitu linii) - tylko odczyt statykow.</summary>
        internal static bool TourneyNow()
        {
            return _duel && _duelKind == "turniej" && InDuel(Mission.Current);
        }

        /// <summary>m111-walka: rodzaj misji i flaga pojedynku; nazwa dopasowanego zachowania do logu raz na misje.</summary>
        private static int _duelTest = -1;

        /// <summary>m111-walka TEST: plik flagi autotestu (raz na proces); w zwyklej grze (LogGate 0) zawsze false.</summary>
        private static bool DuelTestFlag()
        {
            if (_duelTest >= 0) return _duelTest == 1;
            _duelTest = 0;
            try
            {
                int mode = LogGate.Mode;
                if (mode != 1 && mode != 2) return false;
                var docs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                if (System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), "armoury-pojedynek-test.flag"))) _duelTest = 1;
            }
            catch { }
            return _duelTest == 1;
        }

        private static void Classify(Mission m, bool first)
        {
            string kind = MissionTeams.Kind(m);
            _battle = kind == "bitwa" || kind == "kryjowka";
            string via = null;
            bool duel = false;
            if (kind == "turniej" || kind == "arena")
            {
                duel = true;
                foreach (var b in m.MissionBehaviors)
                {
                    var n = b != null ? b.GetType().Name : null;
                    if (n != null && (n.Contains("Tournament") || n.Contains("Arena"))) { via = n; break; }
                }
            }
            else if (kind == "inna")
            {
                foreach (var b in m.MissionBehaviors)
                {
                    var n = b != null ? b.GetType().Name : null;
                    if (n != null && n.Contains("Duel")) { via = n; duel = true; break; }
                }
            }
            // m111-walka TEST (tylko DLL autotestu, LogGate 1/2 + plik CrashScribermoury-pojedynek-test.flag): bitwa w polu liczona jak pojedynek - autotest nie
            // przechodzi ekranow turnieju (TXP, GrandTourney), a w miescie testu trwa turniej (walka cwiczebna niedostepna); tak przechodzi caly kod pojedynku
            if (!duel && kind == "bitwa" && DuelTestFlag()) { duel = true; via = "TEST bitwa jako pojedynek (armoury-pojedynek-test.flag)"; }
            // po recenzji: wszystkie klucze pojedynku wylaczone = jak dawniej (bez probkowania, list i linii DU)
            if (duel && !DuelGuard.AnyKey(Settings.Current)) duel = false;
            bool was = InDuel(m);
            _duel = duel;
            _duelKind = duel ? (kind == "inna" ? "pojedynek" : kind) : "-";
            _m = new WeakReference(m);
            if (duel)
            {
                _tb = m.GetMissionBehavior<TournamentBehavior>();
                if (first || !was)
                {
                    _duelDone = false;
                    Round = 0;
                    DuelGuard.Begin(m);
                    Log.Info("pojedynek: " + _duelKind + " przez " + (via ?? "?") + " (m111-walka: Duel AI, zrywy, zasady postury w tej misji; bitwy bez zmian).");
                }
            }
            else { _tb = null; Round = 0; }
        }

        public override void EarlyStart()
        {
            try { Classify(Mission, true); }
            catch (Exception e) { _duel = false; RoutHold.Stumble(RoutHold.SiteDuel, "BattleSense.EarlyStart", e); }
            base.EarlyStart();
        }

        public override void AfterStart()
        {
            var m = Mission;
            try { Classify(m, false); }
            catch (Exception e) { _battle = false; _duel = false; RoutHold.Stumble(RoutHold.SiteVoice, "BattleSense.AfterStart", e); }
            try { BattleGore.Begin(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteGore, "BattleGore.Begin", e); }
            try { BattleCries.Begin(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVoice, "BattleCries.Begin", e); }
            try { BloodPools.Begin(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Begin", e); }
            try { if (_battle || _duel) VeteranAi.CheckOrder(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVeteran, "VeteranAi.CheckOrder", e); }
            try { if (_duel) DuelGuard.CheckOrder(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.CheckOrder", e); }
            try { if (_battle) WarCouncil.Council(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteTactics, "WarCouncil.Council", e); }
            base.AfterStart();
        }

        public override void OnMissionTick(float dt)
        {
            try { BattleCries.Tick(Mission, dt); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVoice, "BattleCries.Tick", e); }
            try { BloodPools.Tick(Mission, dt); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Tick", e); }
            if (_duel && InDuel(Mission)) DuelTick(Mission, dt);
        }

        // m111-walka: tick pojedynku (glowny watek) - runda, zrywy (UpdateAgentStats tylko tutaj), probkowanie, lista meczu, linie DU
        private static void DuelTick(Mission m, float dt)
        {
            try
            {
                var tb = _tb;
                if (tb != null)
                {
                    int r = tb.CurrentRoundIndex;
                    Round = r < 0 ? 0 : (r > 3 ? 3 : r);
                    DuelGuard.NoteRound(Round);
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "BattleSense.Round", e); }
            try { DuelCraft.Tick(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelCraft.Tick", e); }
            try { DuelGuard.Sample(m, dt); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.Sample", e); }
            try { if (_tb != null) DuelGuard.Roster(m, _tb); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.Roster", e); }
            try { DuelAi.Flush(); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelAi.Flush", e); }
        }

        // m111-walka: kontakty w pojedynku (po CreateMeleeBlow - Mission.cs:5367-5385, glowny watek)
        public override void OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            if (!_duel || !InDuel(Mission)) return;
            try { DuelGuard.OnMeleeHit(Mission, attacker, victim, ref collisionData); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.OnMeleeHit", e); }
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
            // m111-walka: linia "Pojedynek (DU)" raz na misje pojedynku
            if (_duel && !_duelDone && InDuel(Mission))
            {
                _duelDone = true;
                try { DuelAi.Flush(); } catch { }
                try { DuelGuard.Write(Mission, _duelKind); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.Write", e); }
                try { DuelGuard.End(); } catch { }   // po recenzji: statyki bez agentow i meczu (skonczona misja nie wisi w pamieci)
            }
            var w = _m;
            if (w != null && ReferenceEquals(w.Target, Mission)) { _battle = false; _duel = false; _duelKind = "-"; _tb = null; Round = 0; _m = null; }
        }
    }
}
