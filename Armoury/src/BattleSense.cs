using System;
using SandBox.Tournaments.MissionLogics;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
    /// m113-walka2 faza 2 (24.10): w bitwie poczatek licznikow FieldDuel (EarlyStart), tick zrywow bohaterow (DuelCraft) i linia
    /// "Bitwa (m113-walka2)" na koniec; rejestracja takze przy kluczach bitwy (FieldDuel.AnyKey).
    /// noc 25.10 P0 (baza nocy, krytyk K2-K4, K15): wszystkie wolania paczek nocy - marsz i doktryny (P1: MarchToContact, Doctrine), dzwiek (P2: BattleSound) -
    /// kazde we wlasnym try z miejscem potkniec RoutHold (SiteMarch / SiteDoctrine / SiteSound), tylko w bitwie (_battle i nie pojedynek). Latki misji P1/P2
    /// zakladane raz, przy pierwszym AfterStart bitwy (NightHooks.FirstBattle, K3). Kolejnosc w OnMissionTick: BattleCries -> MarchToContact -> Doctrine ->
    /// BattleSound (K4: jeden detektor szarzy BattleCries.ChargeStarted, odbiorcy w tym samym takcie). Nowe override (OnDeploymentFinished, OnAgentShootMissile,
    /// OnMissileHit, OnAgentPanicked, OnAgentFleeing) tylko przekazuja do BattleSound - te z trafien moga przyjsc z ticku asynchronicznego (BattleSound: kolejka
    /// pod lockiem). Rejestracja takze przy kluczach nocy (NightKeys).
    /// 140 (25.10): tarcza pod ostrzalem (ShieldsUp) - Begin w AfterStart (latki raz na proces przy pierwszej bitwie z kluczem, K9), Tick w bloku Night
    /// (po BattleSound), strzal i trafienie pociskiem tylko do kolejki ShieldsUp (osobny try od dzwieku), End w Clean (linia "Bitwa (140 tarcze)").
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
        // m113-walka2: bitwa z rozszerzeniami fazy 2 (FieldDuel) - linia raz na bitwe
        private static bool _battleDone;
        private static string _battleKind = "-";

        /// <summary>noc 25.10 P0 (K2): klucze nocy, przy ktorych BattleSense (i RoutKeeper - linia NB2, RoutHold.On dla zlaman choragwi) musi byc w misji:
        /// marsz i doktryny (P1), dzwiek (P2), choragwie (P3). Czytane w SubModuleMain.OnMissionBehaviorInitialize.</summary>
        internal static bool NightKeys(Settings c)
        {
            return c != null && (c.MarchToContactEnabled || c.CultureDoctrines || c.ShootBeforeAttacking || c.CavalrySeeksFlanks || c.ArchersFallBackBehindInfantry
                                 || c.BattleSoundsEnabled || c.BannerCompaniesEnabled
                                 || c.DefendersAdvanceUnderFire     // 141: S2 obronca pod ostrzalem (Doctrine.S2.cs) - wejscia tylko z BattleSense (Doctrine.Begin / Tick / End)
                                 || c.AiSavesBreath || c.WindedShowsIt || c.WindedBreathSound);   // 144: AiBreath / WindedShow - bitwa z BattleSense.InBattle i latka AdjustSpeedLimit (NightHooks.FirstBattle)
        }

        /// <summary>noc 25.10 P0: biezaca misja to bitwa (nie pojedynek) - bramka wolan paczek nocy; tylko odczyt statykow (takze z ticku asynchronicznego).</summary>
        private bool Night { get { return _battle && !_duel && InBattle(Mission); } }

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
        /// <summary>m113-walka2 po recenzji: czy plik flagi testu pojedynku lezy w CrashScribe (bez pamieci na proces, bez LogGate) - ostrzezenie
        /// w tabeli "Bitwa (m113-walka2)": w autotescie (LogGate 1/2) bitwy szlyby jako pojedynek i linii bitwy by nie bylo.</summary>
        internal static bool DuelTestFlagFile()
        {
            try
            {
                var docs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                return System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), "armoury-pojedynek-test.flag"));
            }
            catch { return false; }
        }

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
            bool wasBattle = InBattle(m);
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
            // m113-walka2: poczatek bitwy - liczniki FieldDuel, zrywy i pamiec hakow od zera (raz na misje)
            if (_battle && !duel && (first || !wasBattle))
            {
                _battleDone = false;
                _battleKind = kind;
                FieldDuel.Begin(m);
            }
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
            try { if (_duel || (_battle && FieldDuel.HooksWanted(Settings.Current))) DuelGuard.CheckOrder(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.CheckOrder", e); }
            // noc 25.10 P0 (K3): latki misji P1/P2 raz, przy pierwszej bitwie; potem Begin paczek - Doctrine PRZED WarCouncil (WarCouncil.One wola Doctrine.ApplyList)
            bool night = Night;
            try { if (night) NightHooks.FirstBattle(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteMarch, "NightHooks.FirstBattle", e); }
            try { if (night) MarchToContact.Begin(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteMarch, "MarchToContact.Begin", e); }
            try { if (night) Doctrine.Begin(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Begin", e); }
            try { if (_battle) WarCouncil.Council(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteTactics, "WarCouncil.Council", e); }
            try { if (night) BattleSound.Begin(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Begin", e); }
            // 140: tarcza pod ostrzalem - stan bitwy od zera, latki raz na proces (K9), kolejnosc po RBM (po przepieciu latek RBM w EarlyStart)
            try { if (night) ShieldsUp.Begin(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteShields, "ShieldsUp.Begin", e); }
            base.AfterStart();
        }

        public override void OnMissionTick(float dt)
        {
            try { BattleCries.Tick(Mission, dt); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVoice, "BattleCries.Tick", e); }
            // noc 25.10 P0 (K4): po BattleCries (jedyny detektor szarzy) - marsz, doktryna, dzwiek; kazde we wlasnym try
            if (Night)
            {
                try { MarchToContact.Tick(Mission, dt); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteMarch, "MarchToContact.Tick", e); }
                try { Doctrine.Tick(Mission, dt); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Tick", e); }
                try { BattleSound.Tick(Mission, dt); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Tick", e); }
                try { ShieldsUp.Tick(Mission, dt); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteShields, "ShieldsUp.Tick", e); }   // 140
            }
            try { BloodPools.Tick(Mission, dt); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Tick", e); }
            if (_duel && InDuel(Mission)) DuelTick(Mission, dt);
            // m113-walka2: zrywy bohaterow w bitwie (zgloszenia z hakow B, UpdateAgentStats tylko tutaj, najwyzej 8 na klatke)
            else if (_battle && FieldDuel.BurstsOn(Settings.Current) && InBattle(Mission))
            {
                try { DuelCraft.Tick(Mission, true); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelCraft.Tick bitwa", e); }
            }
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
            // noc 25.10 P0: dzwiek (jeki po bitwie, kruki przy cialach) - tylko kolejka
            if (Night)
            {
                try { BattleSound.OnRemoved(Mission, affectedAgent, affectorAgent, agentState); }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnRemoved", e); }
            }
        }

        // noc 25.10 P0 (K15): koniec rozstawienia - przemowy dowodcow (P2). Mission.OnDeploymentFinished idzie PRZED odpauzowaniem AI (DeploymentMissionController.cs:41-79)
        public override void OnDeploymentFinished()
        {
            if (Night)
            {
                try { BattleSound.OnDeploymentFinished(Mission); }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnDeploymentFinished", e); }
            }
            base.OnDeploymentFinished();
        }

        // noc 25.10 P0 (K15): strzal - rozkaz "loose" (P2); bardzo czeste, moze przyjsc z ticku asynchronicznego - BattleSound tylko O(1) pod lockiem
        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            if (!Night) return;
            try { BattleSound.OnShoot(Mission, shooterAgent, weaponIndex); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnShoot", e); }
            // 140: strzal - S2 (cel AI) / S3 (punkt upadku) i rzuty; tylko kolejka pod lockiem
            try { ShieldsUp.OnShoot(Mission, shooterAgent, weaponIndex, position, velocity); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteShields, "ShieldsUp.OnShoot", e); }
        }

        // noc 25.10 P0 (K15): trafienie pociskiem - rozkaz "shields" (P2); czeste, jak wyzej
        public override void OnMissileHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            if (!Night) return;
            try { BattleSound.OnMissileHit(Mission, attacker, victim, isCanceled); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnMissileHit", e); }
            // 140: trafienie / pudlo - S1, punkt zderzenia (K6), S4 i histogram stanu rak ofiar (K13); tylko kolejka pod lockiem
            try { ShieldsUp.OnMissileHit(Mission, attacker, victim, isCanceled, ref collisionData); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteShields, "ShieldsUp.OnMissileHit", e); }
        }

        // noc 25.10 P0 (K15): panika i ucieczka - licznik uciekajacych na formacje (P2, okrzyk przy zlamaniu wroga); tylko kolejka
        public override void OnAgentPanicked(Agent affectedAgent)
        {
            if (!Night) return;
            try { BattleSound.OnPanicked(Mission, affectedAgent); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnPanicked", e); }
        }

        public override void OnAgentFleeing(Agent affectedAgent)
        {
            if (!Night) return;
            try { BattleSound.OnFleeing(Mission, affectedAgent); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnFleeing", e); }
        }

        // czesc 3: Mission.ResetMission czysci decale sceny sam (ClearRuntimeDecals) - kaluze tylko zapominaja kolejke
        public override void OnClearScene()
        {
            try { BloodPools.Cleared(Mission); } catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Cleared", e); }
            // noc 25.10 P0: dzwiek zwalnia WSZYSTKIE swoje zdarzenia przed zniszczeniem sceny (bez bramki Night - sprzatanie zawsze)
            try { BattleSound.OnClearScene(Mission); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnClearScene", e); }
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
            // noc 25.10 P0: koniec misji paczek nocy - bez bramki (End bezpieczne wielokrotnie i poza bitwa; dzwiek: Release wszystkich, linia misji)
            try { MarchToContact.End(Mission); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteMarch, "MarchToContact.End", e); }
            try { Doctrine.End(Mission); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.End", e); }
            try { BattleSound.End(Mission); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.End", e); }
            // 140: linia "Bitwa (140 tarcze)" raz na bitwe, flagi postfiksow zgaszone (End bezpieczne wielokrotnie i poza bitwa)
            try { ShieldsUp.End(Mission); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteShields, "ShieldsUp.End", e); }
            // m111-walka: linia "Pojedynek (DU)" raz na misje pojedynku
            if (_duel && !_duelDone && InDuel(Mission))
            {
                _duelDone = true;
                try { DuelAi.Flush(); } catch { }
                try { DuelGuard.Write(Mission, _duelKind); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelGuard.Write", e); }
                try { DuelGuard.End(); } catch { }   // po recenzji: statyki bez agentow i meczu (skonczona misja nie wisi w pamieci)
            }
            // m113-walka2: linia "Bitwa (m113-walka2)" raz na bitwe
            if (_battle && !_duel && !_battleDone && InBattle(Mission))
            {
                _battleDone = true;
                try { FieldDuel.Write(Mission, _battleKind); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVeteran, "FieldDuel.Write", e); }
                try { FieldDuel.End(); } catch { }
            }
            var w = _m;
            if (w != null && ReferenceEquals(w.Target, Mission)) { _battle = false; _duel = false; _duelKind = "-"; _tb = null; Round = 0; _m = null; }
        }
    }
}
