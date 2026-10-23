using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ObjectSystem;
using Path = System.IO.Path;

namespace Armoury
{
    /// <summary>
    /// m97-oboz (Jeff 20.10, decyzja 2 po raporcie obozu: "chodzenie po obozie na poczatku kolejki", etap 1) - SPACER PO OBOZIE.
    /// W naszym menu obozu (arm_camp_wait - ZWYKLE menu, nie menu oczekiwania, wiec otwarcie misji z opcji jest bezpieczne; NIE "camp" gry - to menu spotkania) opcja
    /// "Walk through the camp" otwiera wlasna misje typu "Camp" (nieuzywany typ misji SandBox z kompletem widokow: kamera, Esc, pasek zdrowia - SandBox.View
    /// [ViewMethod("Camp")]) na scenie bitwy z miejsca postoju (SceneModel.GetBattleSceneForMapPatch - jak Practice Drills 1.3, public domain, i jak bitwa
    /// gry w tym miejscu); pora dnia i pogoda z mapy. Bez walki: jedna druzyna (gracz), bez logiki bitwy, bez spotkania i MapEvent; czas kampanii stoi
    /// (misja = stan MissionState nad mapa, kampania nie tyka). Wyjscie: TYLKO przytrzymanie Tab (MissionLeaveView + BasicLeaveMissionLogic) - gra wraca
    /// do menu obozu, z ktorego gracz wszedl. Esc NIE wychodzi ze sceny: menu Esc misji (MissionGauntletSingleplayerEscapeMenu) ma tylko Return/Options/
    /// Photo Mode/Exit to Main Menu - to ostatnie porzuca kampanie bez zapisu (komunikat wejscia o tym mowi).
    ///
    /// Oboz wedlug KOLUMNY (druzyna gracza; w armii - gdy gracz jest wodzem albo doczepiony do wodza - wodz i wszystkie doczepione partie, jeden oboz armii):
    /// sam gracz (bez zdrowych ludzi) - poslanie przy ognisku i jego kon; do CampWalkMenPerTent-1 ludzi - poslania wokol ogniska (noca na nich spia, jeden
    /// czuwa przy ognisku); od CampWalkMenPerTent ludzi namioty zolnierskie ROT (Tent_Small, Tent_Small_Campfire; bez ROT - namiot Native) po jednym na
    /// CampWalkMenPerTent ludzi, rzedami ZA obrysem namiotu wodza, ognisko co trzy namioty; namiot wodza (Tent_Leader) od CampWalkMenPerTent ludzi; namiot
    /// oficerski (Tent_Officer) od 60 ludzi; w armii chorazy z herbem rodu WODZA armii przed namiotem wodza. Kazdy element ma obrys (Footprints - z pozycji
    /// dzieci prefabow ROT-Map/Prefabs) i nie wchodzi w obrys innego (Tent_Leader ma ognisko 10 m z boku i beczki, drewno, stragan do 6 m z tylu).
    /// Wozy taborowe gracza (CampCart) stoja z przodu obozu. Konie na postoju (UWIAZANE jak w Practice Drills: bez wedrowania i wsiadania, predkosc 0,
    /// niesmiertelne, cofane na miejsce co PoseEvery): konie i juczne z taboru gracza (najwyzej MaxHorses), konie jezdzcow kolumny (jeden na 8 jezdzcow,
    /// najwyzej MaxRiderHorses) i kon gracza przy jego poslaniu. Ludzie: zolnierze kolumny (najpierw druzyna gracza; SimpleAgentOrigin - bez powiazania
    /// z rosterem, zadne zdarzenie misji nie zabierze nikogo z druzyny), najwyzej CampWalkMaxMen (wydajnosc), siedza przy ogniskach, noca czesc spi, reszta
    /// limitu lezy obok namiotow; poza ta liczba obozu nie widac w ludziach, tylko w namiotach. Scenografia BEZ FIZYKI (jak CampScene - sprawdzony sposob;
    /// gracz przechodzi przez namioty).
    /// DTE: misja oznaczona DoesMissionRequireCivilianEquipment = true (ustawione w delegacie, przed OnMissionBehaviorInitialize modulow) - DTE wtedy nie
    /// dopina swojej logiki zbrojowni (DynamicTroopEquipmentReupload.SubModule.OnMissionBehaviorInitialize), ludzie ida w stroju cywilnym.
    /// Linie logu: "Spacer po obozie (m97): wejscie ..." (scena, ludzie, namioty, ogniska, agenci, konie, wozy), "... wyjscie po N s", bledy przez Stumble.
    /// Etap 2 (nie teraz): ludzie chodza, jedza, rozmowy, cwiczenia; etap 3: kuchnia, platnik, cyrulik, markietani.
    /// m109-oboz: uklad scenografii (namioty, ogniska, poslania, wozy, konie) przeniesiony do CampLayout - ten sam uklad stawia bitwa w obozie (CampScene).
    /// Test autotestu (LogGate.Mode 1/2 + plik CrashScribe\armoury-oboz-spacer.flag): jeden spacer na proces przy pierwszej wolnej chwili na mapie po wyjsciu
    /// z osady, linia "Spacer po obozie (m109): test - uklad ...", wyjscie po 8 s rzeczywistych. W zwyklej grze TestTick zamyka sie po pierwszym sprawdzeniu.
    /// </summary>
    internal static class CampWalk
    {
        internal const string P = "Spacer po obozie (m97): ";
        internal const string P109 = "Spacer po obozie (m109): ";
        internal const string TestFlag = "armoury-oboz-spacer.flag";
        internal static bool Active;
        private static int _opened, _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        // m109-oboz (test spaceru): 0 - nie sprawdzono, 1 - czeka na wolna chwile na mapie, 2 - koniec (otwarty, zwykla gra albo bez pliku)
        private static int _testState, _waitLines;
        private static bool _seenTown;
        private static long _waitNextMs;
        internal static bool TestWalkPending;    // nastepna misja spaceru jest testowa (CampWalkLogic.Build zdejmuje)

        internal static bool On { get { var s = Settings.Current; return s != null && s.CampWalkEnabled; } }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            bool first;
            lock (_err) first = _err.Add(where);
            if (first) { try { Log.Error("CampWalk." + where, e); } catch { } }
        }

        internal static int Stumbles { get { return _stumbles; } }

        /// <summary>Wpiecie (ArmouryBehavior.RegisterEvents - jedna linia): opcja menu obozu w starcie sesji.</summary>
        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(b, delegate (CampaignGameStarter s) { try { Active = false; if (s != null) AddMenus(s); } catch (Exception e) { Stumble("SessionStart", e); } });
        }

        /// <summary>Opcja w naszym menu obozu arm_camp_wait (wlasny oboz, klawisz O; zwykle menu, czas stoi).</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            // tylko nasze arm_camp_wait: "camp" gry to menu SPOTKANIA (DefaultEncounterGameMenuModel - PlayerEncounter trwa), menu BK - menu oczekiwania
            foreach (var menu in new[] { "arm_camp_wait" })
            {
                try
                {
                    starter.AddGameMenuOption(menu, "arm_camp_walk_" + menu, "{=!}Walk through the camp",
                        delegate (MenuCallbackArgs a)
                        {
                            if (!On) return false;
                            a.optionLeaveType = GameMenuOption.LeaveType.Mission;
                            string why;
                            if (!CanWalk(out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); }
                            return true;
                        },
                        delegate (MenuCallbackArgs a) { Start(); }, false, 2);
                }
                catch (Exception e) { Stumble("AddMenus(" + menu + ")", e); }
            }
            Log.Info(P + "opcja \"Walk through the camp\" w menu obozu (arm_camp_wait) | klucz Camp Walk Enabled " + (On ? "TAK" : "NIE")
                     + " | ludzi w scenie najwyzej " + (Settings.Current != null ? Settings.Current.CampWalkMaxMen : 0) + ".");
        }

        internal static bool CanWalk(out string why)
        {
            why = null;
            var mp = MobileParty.MainParty;
            if (Campaign.Current == null || mp == null || Hero.MainHero == null) why = "No campaign is running.";
            else if (mp.MapEvent != null || mp.SiegeEvent != null || PlayerEncounter.Current != null) why = "Not while in a battle or siege.";
            else if (mp.CurrentSettlement != null) why = "Leave the settlement first.";
            else if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) why = "Only on land.";
            else if (Mission.Current != null || Active) why = "Already in a scene.";
            else if (!Hero.MainHero.IsAlive || Hero.MainHero.IsPrisoner) why = "Not now.";
            return why == null;
        }

        private static void Start()
        {
            string why;
            if (!CanWalk(out why)) { Log.Player(why, true); return; }
            try
            {
                Active = true;
                _opened++;
                Open();
            }
            catch (Exception e)
            {
                Active = false;
                Stumble("Open", e);
                Log.Player("The camp cannot be entered right now.", true);
            }
        }

        private static MissionInitializerRecord Record(out string scene)
        {
            var mp = MobileParty.MainParty;
            CampaignVec2 pos = mp.Position;
            MapPatchData patch = Campaign.Current.MapSceneWrapper.GetMapPatchAtPosition(ref pos);
            scene = Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(patch, false);
            var terrain = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mp.CurrentNavigationFace);
            var rec = new MissionInitializerRecord(scene);
            rec.TerrainType = (int)terrain;
            rec.DamageToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
            rec.DamageFromPlayerToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
            rec.NeedsRandomTerrain = false;
            rec.PlayingInCampaignMode = true;
            rec.RandomTerrainSeed = MBRandom.RandomInt(10000);
            rec.AtmosphereOnCampaign = Campaign.Current.Models.MapWeatherModel.GetAtmosphereModel(pos);
            rec.SceneHasMapPatch = true;
            rec.DecalAtlasGroup = 2;
            rec.PatchCoordinates = patch.normalizedCoordinates;
            rec.PatchEncounterDir = new Vec2(0f, 1f);
            return rec;
        }

        private static void Open()
        {
            string scene;
            var rec = Record(out scene);
            if (string.IsNullOrEmpty(scene)) throw new InvalidOperationException("brak sceny dla tego miejsca mapy");
            CampWalkLogic.SceneName = scene;
            MissionState.OpenNew("Camp", rec, delegate (Mission m)
            {
                // DTE i stroj: misja cywilna (DTE pomija ja w OnMissionBehaviorInitialize, ludzie w stroju cywilnym)
                try { m.DoesMissionRequireCivilianEquipment = true; } catch (Exception e) { Stumble("Civilian", e); }
                return new MissionBehavior[]
                {
                    new MissionOptionsComponent(),
                    new CampaignMissionComponent(),
                    new BasicLeaveMissionLogic(),
                    new MissionHardBorderPlacer(),
                    new MissionBoundaryPlacer(),
                    new MissionBoundaryCrossingHandler(10f),
                    new EquipmentControllerLeaveLogic(),
                    new CampWalkLogic(),
                    ViewCreator.CreateMissionLeaveView()
                };
            }, true, true);
            Log.Info(P + "otwieranie | scena " + scene + " | godzina " + CampaignTime.Now.GetHourOfDay + (CampaignTime.Now.IsNightTime ? " (noc)" : " (dzien)")
                     + " | wejscie nr " + _opened + " w tej sesji.");
        }

        // ------------------------------------------------------------ m109-oboz: test spaceru pod plikiem autotestu

        internal static string CrashScribeDir()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe");
        }

        /// <summary>Test spaceru: DLL autotestu CrashScribe (LogGate.Mode 1/2) i plik CrashScribe\armoury-oboz-spacer.flag (wzor MapChatLog.TestWanted).</summary>
        internal static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try { return File.Exists(Path.Combine(CrashScribeDir(), TestFlag)); }
            catch { return false; }
        }

        /// <summary>SubModuleMain.OnApplicationTick (jedna linia): poza kampania i po zamknieciu testu - od razu wyjscie. Kod z typami SandBox.View w TestStep
        /// (osobna metoda - blad ladowania typu wylatuje w tym try, nie do petli modulow gry).</summary>
        internal static void TestTick()
        {
            if (_testState == 2 || Campaign.Current == null) return;
            try { TestStep(); }
            catch (Exception e) { _testState = 2; Stumble("TestTick", e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TestStep()
        {
            if (_testState == 0)
            {
                if (!TestWanted()) { _testState = 2; return; }   // zwykla gra albo bez pliku - test zamkniety do konca procesu
                _testState = 1;
                Log.Info(P109 + "test - plik " + TestFlag + ": jeden spacer przy pierwszej wolnej chwili na mapie po wyjsciu z osady, wyjscie po 8 s.");
            }
            string why = TestGate();
            if (why == null)
            {
                _testState = 2;
                Log.Info(P109 + "test - otwieram spacer (plik " + TestFlag + ").");
                TestWalkPending = true;
                Start();
                if (!Active) { TestWalkPending = false; Log.Info(P109 + "test - spacer nie otworzyl sie (zob. ERROR in CampWalk.Open)."); }
                return;
            }
            long now = (long)(System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (_waitLines < 5 && (_waitLines == 0 || now >= _waitNextMs))
            {
                _waitLines++;
                _waitNextMs = now + 120000;
                Log.Info(P109 + "test - czekam (" + why + ").");
            }
        }

        /// <summary>null = spacer testu mozna otworzyc teraz (mapa gotowa, bez menu, okien i rozmowy, CanWalk), inaczej powod czekania (wzor Prologue.SceneGate).
        /// Najpierw osada (autotest osadza gracza w miescie, bitwy zaczyna wyjsciem z miasta - spacer idzie w tej chwili, przed bitwa 1).</summary>
        private static string TestGate()
        {
            var c = Campaign.Current;
            var gsm = GameStateManager.Current;
            var active = gsm != null ? gsm.ActiveState : null;
            if (!(active is TaleWorlds.CampaignSystem.GameState.MapState)) return "nie mapa (" + (active != null ? active.GetType().Name : "-") + ")";
            if (LoadingWindow.IsLoadingWindowActive) return "ekran wczytywania";
            var msc = SandBox.View.Map.MapScreen.Instance;
            if (msc != null && !msc.IsReady) return "mapa jeszcze nie gotowa";
            var mp = MobileParty.MainParty;
            if (mp != null && mp.CurrentSettlement != null) { _seenTown = true; return "w osadzie - spacer po wyjsciu"; }
            if (!_seenTown) return "jeszcze nie bylo osady - autotest otwiera spacer po wyjsciu z miasta";
            if (c.CurrentMenuContext != null) return "menu";
            var cm = c.ConversationManager;
            if (cm != null && (cm.IsConversationFlowActive || cm.IsConversationInProgress)) return "rozmowa";
            if (gsm.ActiveStateDisabledByUser) return "menu nad mapa";
            if (InformationManager.IsAnyInquiryActive()) return "okno (zapytanie)";
            if (!On) return "klucz Camp Walk Enabled NIE";
            string why;
            if (!CanWalk(out why)) return "CanWalk: " + why;
            return null;
        }
    }

    /// <summary>m97-oboz: logika sceny spaceru - kolumna gracza, gracz, scenografia obozu (obrysy bez nakladania), ludzie przy ogniskach, uwiazane konie;
    /// pozy i pilnowanie miejsca co 1.5 s (tylko postawieni ludzie i konie). m109-oboz: scenografia i konie przez CampLayout (ten sam uklad co bitwa w obozie).</summary>
    internal sealed class CampWalkLogic : MissionLogic
    {
        internal static string SceneName;
        private const float PoseEvery = 1.5f;
        private const string SitPose = "act_dungeon_prisoner_idle_exhausted", SleepPose = "act_dungeon_prisoner_sleep_cycle", LiePose = "act_dungeon_prisoner_lay_cycle";

        private sealed class Sitter { public Agent Agent; public Vec3 Spot; public Vec2 Facing; public string Pose; public float Speed; }
        private readonly List<Sitter> _sitters = new List<Sitter>();
        private readonly Random _rng = new Random();
        private CampLayout _lay;
        private Agent _player;
        private bool _built, _test;
        private float _poseT, _t;
        private int _men, _sleep, _bearer, _parties;
        private System.Diagnostics.Stopwatch _testClock;   // m109-oboz (test): wyjscie po 8 s rzeczywistych

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        public override void AfterStart()
        {
            base.AfterStart();
            try { Build(); }
            catch (Exception e) { CampWalk.Stumble("Build", e); }
        }

        public override void OnMissionTick(float dt)
        {
            _t += dt;
            if (_testClock != null && _testClock.ElapsedMilliseconds >= 8000)
            {
                _testClock = null;
                try { Mission.EndMission(); } catch (Exception e) { CampWalk.Stumble("TestEnd", e); }
            }
            if (!_built) return;
            _poseT += dt;
            if (_poseT < PoseEvery) return;
            _poseT = 0f;
            for (int i = 0; i < _sitters.Count; i++) Hold(_sitters[i]);
            if (_lay != null) _lay.HoldHorses();
        }

        public override InquiryData OnEndMissionRequest(out bool canPlayerLeave)
        {
            canPlayerLeave = true;
            return null;
        }

        protected override void OnEndMission()
        {
            try
            {
                Log.Info(CampWalk.P + "wyjscie po " + _t.ToString("0", CultureInfo.InvariantCulture) + " s | potkniecia razem " + CampWalk.Stumbles + ".");
            }
            catch { }
            _sitters.Clear();
            if (_lay != null) _lay.Reset();
            _lay = null;
            _testClock = null;
            CampWalk.Active = false;
            base.OnEndMission();
        }

        // ------------------------------------------------------------ budowa obozu

        private void Build()
        {
            var m = Mission;
            var hero = Hero.MainHero;
            var party = MobileParty.MainParty;
            _test = CampWalk.TestWalkPending;
            CampWalk.TestWalkPending = false;
            if (_test) _testClock = System.Diagnostics.Stopwatch.StartNew();   // m109-oboz (test): zegar wyjscia od razu - wywrotka albo wczesne wyjscie z Build tez zamyka spacer po 8 s
            if (m == null || m.Scene == null || hero == null || party == null) return;
            var s = Settings.Current;
            int cap = s != null ? Math.Max(0, Math.Min(100, s.CampWalkMaxMen)) : 40;
            int perTent = s != null ? Math.Max(2, Math.Min(20, s.CampWalkMenPerTent)) : 6;
            int st0 = CampWalk.Stumbles;
            // noc obozu: godziny obozu swiata (21-3) albo noc gry (22-2) - wtedy czesc ludzi spi
            bool night = CampaignTime.Now.IsNightTime || NightRest.InCamp(CampaignTime.Now.GetHourOfDay);

            var color = hero.MapFaction != null ? hero.MapFaction.Color : uint.MaxValue;
            var color2 = hero.MapFaction != null ? hero.MapFaction.Color2 : uint.MaxValue;
            var banner = Clan.PlayerClan != null ? Clan.PlayerClan.Banner : null;
            var team = m.Teams.Add(BattleSideEnum.Defender, color, color2, banner, true, false, true);
            m.PlayerTeam = team;

            var up = new Vec2(0f, 1f);
            var right = new Vec2(1f, 0f);
            _lay = new CampLayout(m, Centre(m), up, CampWalk.P, CampWalk.Stumble, _rng);
            _lay.Probe();

            // zdrowi ludzie kolumny (bez bohaterow) - najpierw druzyna gracza (kazda partia osobno przetasowana), jezdzcy do koni na postoju
            var plan = CampLayout.Count(party, perTent, _rng, null);
            var troops = plan.Troops;
            int healthy = plan.Healthy, mounted = plan.Mounted;
            bool army = plan.Army;
            _parties = plan.Column.Count;

            // gracz: 6 m przed srodkiem, patrzy na oboz; kon gracza przy jego poslaniu; chorazy z herbem wodza armii przed namiotem wodza (w armii)
            _lay.OnPlayerSpot = delegate (Vec2 me) { _player = SpawnPlayer(m, team, hero, me, up); };
            _lay.OnHeroHorseSpot = delegate (Vec2 p) { _lay.HeroHorse(hero, p); };
            _lay.OnLeaderTent = delegate (Vec2 lead)
            {
                var flag = plan.LeaderBanner ?? banner;
                if (army && flag != null) SpawnBearer(m, team, troops, lead - up * 5f + right * 3f, -up, flag);
            };
            _lay.Lay(plan, perTent);
            var player = _player;
            var fires = _lay.FireSpots;
            var beds = _lay.BedSpots;
            var tentSpots = _lay.TentSpots;
            Vec2 mainFire = _lay.MainFire;
            int tents = plan.SoldierTents;

            // ludzie przy ogniskach (najwyzej cap), noca czesc spi przy ogniskach
            int men = Math.Min(cap, troops.Count);
            int ti = 0;
            if (tents == 0)
            {
                // maly oddzial: noca spia na poslaniach (jeden czuwa przy ognisku), za dnia po 4 siedza przy ognisku, reszta lezy na poslaniach
                int sitN = 0, bedN = 0;
                for (; ti < men; ti++)
                {
                    bool toBed = night ? ti > 0 && bedN < beds.Count : sitN >= 4 && bedN < beds.Count;
                    if (toBed)
                    {
                        Vec2 b = beds[bedN++];
                        if (SpawnSitter(m, team, troops[ti], b, mainFire - b, night ? SleepPose : LiePose, 0.5f)) { _men++; if (night) _sleep++; }
                        continue;
                    }
                    if (fires.Count == 0 || sitN >= 4) continue;
                    double ang = (sitN * Math.PI * 2.0 / 4.0) + _rng.NextDouble() * 0.5 + Math.PI;
                    sitN++;
                    Vec2 p = fires[0] + new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 1.9f;
                    if (SpawnSitter(m, team, troops[ti], p, fires[0] - p, SitPose, 0.4f)) _men++;
                }
            }
            for (int fi = 0; tents > 0 && fi < fires.Count && ti < men; fi++)
            {
                int around = Math.Min(4, men - ti);
                for (int k = 0; k < around && ti < men; k++)
                {
                    double ang = (k * Math.PI * 2.0 / 4.0) + _rng.NextDouble() * 0.5 + (fi == 0 ? Math.PI : 0.0);
                    Vec2 p = fires[fi] + new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 1.9f;
                    bool sleep = night && _rng.NextDouble() < 0.5;
                    if (SpawnSitter(m, team, troops[ti], p, fires[fi] - p, sleep ? SleepPose : SitPose, 0.4f)) { _men++; if (sleep) _sleep++; }
                    ti++;
                }
            }
            // reszta limitu - lezy obok namiotow zolnierskich, w przerwach miedzy namiotami (noc: spi, dzien: odpoczywa); miejsce zajete - nastepne
            for (int i = 0; ti < men && i < tentSpots.Count * 4; i++)
            {
                Vec2 t = tentSpots[i / 4];
                int k = i % 4;
                Vec2 p = t + right * (k < 2 ? 4f : -4f) + up * ((k % 2 == 0) ? -1.1f : 1.1f);
                if (SpawnSitter(m, team, troops[ti], p, right, night ? SleepPose : LiePose, 0.6f)) { _men++; if (night) _sleep++; ti++; }
            }

            _built = true;
            if (player == null) Log.Player("Your camp is ready, but you could not be placed in it.", true);
            else Log.Player("You walk through your camp. Hold Tab to go back (Esc does not leave the camp).");
            var c = _lay.C;
            Log.Info(CampWalk.P + "wejscie | scena " + (SceneName ?? "?") + " | " + (night ? "noc" : "dzien") + (army ? ", armia (kolumna " + _parties + " partii)" : "") + " | ludzi zdrowych " + healthy
                     + " (namiot na " + perTent + ") | namioty " + _lay.Tents + " (zolnierskich wedlug ludzi " + tents + ", postawionych " + tentSpots.Count + "), poslania " + _lay.Beds + ", ogniska " + _lay.Fires
                     + ", wozy " + _lay.Carts + "/" + CampCart.Carts + " | agenci ludzi " + _men + " z limitu " + cap + " (spiacych " + _sleep + ")" + (_bearer > 0 ? ", chorazy z herbem" : "")
                     + " | konie " + _lay.Horses + " (jezdzcow " + _lay.RiderHorsesN + " z " + mounted + " jezdzcow, uwiazane " + _lay.TiedHorses.Count + ")"
                     + " | elementy " + _lay.Props + " (przesuniete przez drzewa/skaly/wode/obrysy " + _lay.Moved + ", pominiete " + _lay.BlockedN + ", nakladanie obrysow " + _lay.Overlap + ")"
                     + (_lay.Failed > 0 ? ", nieudane " + _lay.Failed : "") + " | gracz " + (player != null ? "tak" : "NIE")
                     + " | srodek " + c.x.ToString("0", CultureInfo.InvariantCulture) + "," + c.y.ToString("0", CultureInfo.InvariantCulture) + ".");
            if (_test)
            {
                // m109-oboz (test): uklad wobec regul (te same liczby co linia bitwy w obozie); wyjscie po 8 s rzeczywistych od poczatku Build
                Log.Info(CampWalk.P109 + "test - uklad: ludzi zdrowych " + healthy + " (namiot na " + perTent + ") | wedlug regul: " + _lay.PlanText(plan)
                         + " | postawione: namioty " + _lay.Tents + ", poslania " + _lay.Beds + ", ogniska " + _lay.Fires + ", wozy " + _lay.Carts + ", konie taboru " + _lay.TrainHorses
                         + " (jezdzcow " + _lay.RiderHorsesN + ") | przesuniete " + _lay.Moved + " | " + _lay.GeometryText() + " | gracz " + (player != null ? "tak" : "NIE")
                         + " | potkniecia " + (CampWalk.Stumbles - st0) + ".");
            }
        }

        /// <summary>Srodek obozu: srodek granic sceny (walk area z MissionBoundaryPlacer), inaczej srodek granic sceny silnika.</summary>
        private static Vec2 Centre(Mission m)
        {
            try
            {
                float sx = 0f, sy = 0f;
                int n = 0;
                foreach (var kv in m.Boundaries)
                {
                    if (kv.Value == null) continue;
                    foreach (var p in kv.Value) { sx += p.x; sy += p.y; n++; }
                }
                if (n > 0)
                {
                    var c = new Vec2(sx / n, sy / n);
                    if (m.IsPositionInsideBoundaries(c)) return c;
                }
            }
            catch (Exception e) { CampWalk.Stumble("Centre(granice)", e); }
            try
            {
                Vec3 min, max;
                m.Scene.GetSceneLimits(out min, out max);
                return new Vec2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
            }
            catch (Exception e) { CampWalk.Stumble("Centre(scena)", e); }
            return new Vec2(0f, 0f);
        }

        private Agent SpawnPlayer(Mission m, Team team, Hero hero, Vec2 at, Vec2 facing)
        {
            try
            {
                var ch = hero.CharacterObject;
                var pos = _lay.Ground(at);
                facing.Normalize();
                var abd = new AgentBuildData(ch).Team(team).InitialPosition(in pos).InitialDirection(in facing)
                    .Controller(AgentControllerType.Player).NoHorses(true).CivilianEquipment(true)
                    .Equipment(hero.CivilianEquipment)
                    .TroopOrigin(new SimpleAgentOrigin(ch, -1, Clan.PlayerClan != null ? Clan.PlayerClan.Banner : null, default(UniqueTroopDescriptor)))
                    .ClothingColor1(team.Color).ClothingColor2(team.Color2);
                var a = m.SpawnAgent(abd, false);
                if (a != null && m.MainAgent == null) m.MainAgent = a;
                return a;
            }
            catch (Exception e) { CampWalk.Stumble("SpawnPlayer", e); return null; }
        }

        /// <summary>Czlowiek w pozie (siedzi, lezy, spi) w miejscu at; r - jego obrys (miejsce zajete przez kogos albo przez namiot/ognisko/woz - bez niego).</summary>
        private bool SpawnSitter(Mission m, Team team, CharacterObject ch, Vec2 at, Vec2 facing, string pose, float r)
        {
            if (ch == null || !m.IsPositionInsideBoundaries(at) || _lay.Blocked(at, 0.5f)) return false;
            var box = _lay.Sq(at, r);
            if (_lay.Hits(box)) return false;
            try
            {
                var pos = _lay.Ground(at);
                if (facing.LengthSquared < 0.0001f) facing = new Vec2(0f, 1f);
                facing.Normalize();
                var abd = new AgentBuildData(ch).Team(team).InitialPosition(in pos).InitialDirection(in facing)
                    .Controller(AgentControllerType.None).NoHorses(true).CivilianEquipment(true).NoWeapons(true)
                    .TroopOrigin(new SimpleAgentOrigin(ch, -1, null, default(UniqueTroopDescriptor)))
                    .ClothingColor1(team.Color).ClothingColor2(team.Color2);
                var a = m.SpawnAgent(abd, false);
                if (a == null) return false;
                _lay.Occupy(box);
                Ready(a);
                var sit = new Sitter { Agent = a, Spot = pos, Facing = facing, Pose = pose, Speed = 0.8f + (float)_rng.NextDouble() * 0.4f };
                _sitters.Add(sit);
                Hold(sit);
                return true;
            }
            catch (Exception e) { CampWalk.Stumble("SpawnSitter", e); return false; }
        }

        /// <summary>Chorazy z herbem (rodu wodza armii) przy namiocie wodza (tylko w armii). Jedna proba; wywrotka - bez choragwi.</summary>
        private void SpawnBearer(Mission m, Team team, List<CharacterObject> troops, Vec2 at, Vec2 facing, Banner banner)
        {
            if (troops.Count == 0) return;
            try
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>("campaign_banner_small");
                if (item == null) return;
                if (!_lay.FreeSpot(ref at, facing, 0.6f, null)) return;
                var ch = troops[troops.Count - 1];
                troops.RemoveAt(troops.Count - 1);
                var pos = _lay.Ground(at);
                facing.Normalize();
                var abd = new AgentBuildData(ch).Team(team).InitialPosition(in pos).InitialDirection(in facing)
                    .Controller(AgentControllerType.None).NoHorses(true).CivilianEquipment(true)
                    .TroopOrigin(new SimpleAgentOrigin(ch, -1, banner, default(UniqueTroopDescriptor)))
                    .Banner(banner).BannerItem(item)
                    .ClothingColor1(team.Color).ClothingColor2(team.Color2);
                var a = m.SpawnAgent(abd, false);
                if (a == null) return;
                _lay.Occupy(_lay.Sq(at, 0.6f));
                try { a.TryToWieldWeaponInSlot(EquipmentIndex.ExtraWeaponSlot, Agent.WeaponWieldActionType.Instant, true); } catch { }
                _bearer++;
            }
            catch (Exception e) { CampWalk.Stumble("SpawnBearer", e); }
        }

        // ------------------------------------------------------------ pozy (wzor Practice Drills: zestaw ruchow kryjowki, potem animacja w kanale 0)

        private static void Ready(Agent a)
        {
            try
            {
                var set = MBGlobals.GetActionSet("as_human_hideout_bandit");
                var data = MonsterExtensions.FillAnimationSystemData(a.Monster, set, a.Character.GetStepSize(), false);
                a.SetActionSet(ref data);
            }
            catch (Exception e) { CampWalk.Stumble("Ready", e); }
        }

        private void Hold(Sitter s)
        {
            try
            {
                var a = s.Agent;
                if (a == null || !a.IsActive()) return;
                if (a.Position.AsVec2.Distance(s.Spot.AsVec2) > 0.8f)
                {
                    a.TeleportToPosition(s.Spot);
                    a.SetMovementDirection(in s.Facing);
                }
                var act = ActionIndexCache.Create(s.Pose);
                var cur = a.GetCurrentAction(0);
                if (!cur.Equals(act))
                    a.SetActionChannel(0, in act, true, (AnimFlags)0, 0f, s.Speed, -0.2f, 0.4f, (float)_rng.NextDouble(), false, -0.2f, 0, true);
            }
            catch (Exception e) { CampWalk.Stumble("Hold", e); }
        }
    }

    /// <summary>
    /// m109-oboz (Jeff 22.10 21:20: "inny jest oboz, jak wchodze do obozu - jedno polanie, a inny, jak jestem atakowany w obozie") - UKLAD OBOZU wspolny
    /// dla spaceru (CampWalkLogic) i bitwy w obozie (CampScene.BuildLikeWalk): te same reguly namiotow wedlug ludzi kolumny, prefaby ROT, ogniska, poslania,
    /// wozy i konie taboru, obrysy bez nakladania (kod przeniesiony ze spaceru bez zmian wartosci i kolejnosci FreeSpot/Occupy).
    /// Uklad lokalny obozu: srodek C, os Up (od srodka w strone namiotu wodza i rzedow), Right = (Up.y, -Up.x); obrysy (Box) w tym ukladzie, pozycje w swiecie
    /// tymi samymi wyrazeniami co dawny spacer (C + Up * 12 itd.). Spacer: Up = (0, 1), Right = (1, 0) - obrysy dawnego spaceru przesuniete o srodek (test
    /// nakladania bez zmian). Bitwa: Clear 6 m od kazdego agenta (migawka SetTroops) i zaden obrys blizej linii wojska niz Clear (FrontY), bez koni jezdzcow
    /// i bez ludzi (haki null - miejsce gracza i jego konia zarezerwowane, puste); DepthLimit poszerza rzedy namiotow (6..12), gdy za linia malo miejsca.
    /// Recenzja m109: w bitwie scenografia (bez koni) moze stac do OutsideMax za granica pola bitwy (Inside - w granicach sceny silnika), bo za wojskiem
    /// na koncu sciezki rozstawienia bywa 50-60 m pola; konie obozu w bitwie poza pula koni bez jezdzca (Unpool - SI nie siega po konia bez CommonAIComponent).
    /// Ludzie spaceru (gracz, siedzacy, chorazy) zostaja w CampWalkLogic (haki OnPlayerSpot, OnHeroHorseSpot, OnLeaderTent).
    /// </summary>
    internal sealed class CampLayout
    {
        internal const int MaxHorses = 12, MaxRiderHorses = 10, MaxTents = 40, MaxCarts = 6, MaxFires = 14;
        internal const float RowGap = 11f;        // odstep rzedow namiotow (obrys Tent_Small 5.2 m + ognisko 5.5 m przed namiotem z siedzacymi)
        internal const float HoldEvery = 1.5f;    // pilnowanie uwiazanych koni (jak PoseEvery spaceru)
        internal const float FrontReach = 15f;    // najdalszy element przed srodkiem obozu: rzad wozow 12 m + obrys woza 2.4 m + 0.6 m zapasu
        internal const float SceneMargin = 10f;   // bitwa: scenografia za granica pola bitwy nie blizej niz tyle od granic sceny silnika (krawedz terenu)

        internal static readonly string[] TentSmall = { "Tent_Small", "Tent_Small_Campfire", "tent_vlandia_c", "tent_vlandia_a" };
        internal static readonly string[] TentLeader = { "Tent_Leader", "tent_vlandia_b", "tent_vlandia_c" };
        internal static readonly string[] TentOfficer = { "Tent_Officer", "Tent_Officer_Hangout", "tent_vlandia_a" };
        internal static readonly string[] Fire = { "fire_stones_bonfire", "burning_campfire", "campfire" };
        internal static readonly string[] Bed = { "bed_ground_a", "bed_ground_b", "bed_ground_c" };
        internal static readonly string[] Cart = { "bd_cart_a", "bd_cart_b", "bd_cart_c" };

        // recenzja techniki (m97): obrys prefabu w jego ukladzie (x0, y0, x1, y1; os y = kierunek "facing" w Put, os x = (f.y, -f.x) jak Mat3.RotateAboutUp) -
        // z pozycji dzieci w ROT-Map/Prefabs (Swan_Siege.xml: Tent_Leader dzieci x -2.6..10.4, y -6.3..2.9 - ognisko burning_campfire_big 10.4 m z boku,
        // stragan, beczki i drewno do 6.3 m z tylu; Tent_Small: tent_vlandia_c + 2 poslania; Tent_Officer_Hangout x -2.3..8.7; Fantasy.xml: Tent_Officer
        // x -2.3..1.3) plus promien samego namiotu ok. 1.2-3 m. Brak w tabeli - kwadrat o boku 2r.
        private static readonly Dictionary<string, float[]> Footprints = new Dictionary<string, float[]>
        {
            { "Tent_Leader", new[] { -3.8f, -7.5f, 11.8f, 4.2f } },
            { "Tent_Small", new[] { -2.6f, -2.6f, 2.6f, 2.6f } },
            { "Tent_Small_Campfire", new[] { -2.6f, -3.2f, 5.8f, 2.6f } },
            { "Tent_Officer", new[] { -3.3f, -3.5f, 3.3f, 3.5f } },
            { "Tent_Officer_Hangout", new[] { -3.3f, -4.4f, 9.5f, 3.4f } },
            { "tent_vlandia_a", new[] { -3.2f, -3.2f, 3.2f, 3.2f } },
            { "tent_vlandia_b", new[] { -3.2f, -3.2f, 3.2f, 3.2f } },
            { "tent_vlandia_c", new[] { -2.6f, -2.6f, 2.6f, 2.6f } },
        };

        // wzor Practice Drills (public domain): najwyzsze cialo fizyczne nad punktem (drzewo, skala, budynek sceny) i woda; maski cial jak w PD (Top / Ground)
        private const BodyFlags TopMask = (BodyFlags)7378688;
        private static readonly float[,] Shifts = { { 0f, 0f }, { 3f, 0f }, { -3f, 0f }, { 0f, -4f }, { 0f, 4f }, { 5f, 0f }, { -5f, 0f }, { 3f, -5f }, { -3f, 5f } };

        internal sealed class Tied { public Agent Agent; public Vec3 Spot; public Vec2 Facing; }
        internal struct Box { public float X0, Y0, X1, Y1; }

        /// <summary>Plan obozu wedlug regul (te same liczby, ktore Lay probuje postawic): kolumna, zdrowi bez bohaterow, jezdzcy, konie taboru.</summary>
        internal sealed class Plan
        {
            public List<MobileParty> Column = new List<MobileParty>();
            public bool Army, HealthyFromTest;
            public Banner LeaderBanner;
            public List<CharacterObject> Troops = new List<CharacterObject>(), Riders = new List<CharacterObject>();
            public List<EquipmentElement> Train = new List<EquipmentElement>();   // konie taboru do postawienia (kolejnosc stosow ItemRoster, z kazdego najwyzej MaxHorses / 3)
            public int Healthy, Mounted, SoldierTents, Tents, Beds, Fires, Carts, TrainHorses;

            /// <summary>Liczby planu z Healthy, Train i liczby wozow (reguly petli Lay: namiot na perTent zdrowych, wodza od perTent, oficerski od 60, ognisko
            /// gracza + co trzy namioty, poslanie gracza + poslania malego oddzialu).</summary>
            internal void Recount(int perTent, int carts)
            {
                int h = Healthy;
                SoldierTents = h >= perTent ? Math.Min(MaxTents, (h + perTent - 1) / perTent) : 0;
                Tents = SoldierTents + (h >= perTent ? 1 : 0) + (h >= perTent && h >= 60 ? 1 : 0);
                Fires = 1 + SoldierTents / 3;
                Beds = 1 + (SoldierTents == 0 && h > 0 ? Math.Min(h, 8) : 0);
                Carts = Math.Max(0, Math.Min(MaxCarts, carts));
                TrainHorses = Train.Count;
            }
        }

        internal readonly Mission M;
        internal readonly Vec2 C, Up, Right;
        private readonly string _p;
        private readonly Action<string, Exception> _stumble;
        private readonly Random _rng;
        private readonly List<Box> _occ = new List<Box>();     // zajete obrysy (uklad lokalny): namioty, ogniska, wozy, konie, ludzie, gracz (poslania sa plaskie - nie licza sie)
        private readonly List<Vec2> _troops = new List<Vec2>(); // bitwa: migawka agentow w ukladzie lokalnym (SetTroops)
        private bool _waterOff, _topOff;   // jak PD: odczyt wody albo cial, ktory "zalewa" prawie cala scene (ponad 90% probek), jest dla tej sceny pomijany
        private int _why;                  // przyczyna ostatniej porazki FreeSpot: 0 granica, 1 teren, 2 obrysy, 3 wojsko
        private bool _limRead, _limOk;     // bitwa: granice sceny silnika (GetSceneLimits) odczytane raz na uklad, pomniejszone o SceneMargin
        private Vec2 _limMin, _limMax;

        // parametry (spacer: 0 / brak / true / false)
        internal float Clear;                          // bitwa: nic blizej niz tyle metrow od agenta i od linii wojska
        internal float FrontY = float.MinValue;        // bitwa: zaden obrys przed y = FrontY (lokalnie; linia wojska + Clear)
        internal float DepthLimit = float.MaxValue;    // bitwa: miejsce za srodkiem obozu do granicy sceny (poszerzanie rzedow)
        internal bool RiderHorses = true;              // konie jezdzcow kolumny (spacer; w bitwie sa pod jezdzcami)
        internal bool CountEntities;                   // bitwa: licz encje scenografii z dziecmi (koszt)
        internal float OutsideMax;                     // bitwa: scenografia (bez koni) do tylu metrow za granica pola bitwy (granica miekka), w granicach sceny silnika
        internal float MaxY = float.MaxValue;          // test bitwy "glebokosc=N": nic dalej niz N m za srodkiem obozu (lokalnie y) - sciezka malej glebokosci
        internal bool Unpool;                          // bitwa: uwiazane konie poza Mission.MountsWithoutRiders (Tie zdejmuje im CommonAIComponent)
        internal Action<Vec2> OnPlayerSpot, OnHeroHorseSpot, OnLeaderTent;

        // wyniki
        internal int Tents, Fires, Beds, Carts, Horses, RiderHorsesN, TrainHorses, Props, Failed, Moved, BlockedN, Overlap, OutsideField;
        internal int SkipBorder, SkipTerrain, SkipOverlap, SkipTroops, SkipPrefab, SkipError, Entities, PerRow = 6;
        internal float RowY = float.NaN;
        internal Vec2 LeaderLocal, MainFire, PlayerSpot;
        internal bool LeaderPlaced, LeaderShifted;
        internal readonly List<Vec2> FireSpots = new List<Vec2>(), BedSpots = new List<Vec2>(), TentSpots = new List<Vec2>();
        internal readonly List<Tied> TiedHorses = new List<Tied>();
        internal readonly List<Box> Placed = new List<Box>();   // obrysy lokalne postawionych elementow i koni (pomiar odleglosci w bitwie)

        internal CampLayout(Mission m, Vec2 c, Vec2 up, string prefix, Action<string, Exception> stumble, Random rng)
        {
            M = m;
            C = c;
            if (up.LengthSquared < 0.0001f) up = new Vec2(0f, 1f);
            up.Normalize();
            Up = up;
            Right = new Vec2(up.y, -up.x);
            _p = prefix;
            _stumble = stumble;
            _rng = rng ?? new Random();
        }

        internal void Reset()
        {
            _occ.Clear();
            _troops.Clear();
            TiedHorses.Clear();
            Placed.Clear();
        }

        /// <summary>Punkt swiata w ukladzie lokalnym obozu (x wzdluz Right, y wzdluz Up, od srodka C).</summary>
        internal Vec2 L(Vec2 w)
        {
            var d = w - C;
            return new Vec2(Vec2.DotProduct(d, Right), Vec2.DotProduct(d, Up));
        }

        /// <summary>Bitwa: migawka pozycji agentow (swiat) - do Clear wchodza tylko ci blisko obozu (lokalnie y > -(FrontReach + Clear + 6)).</summary>
        internal void SetTroops(IEnumerable<Vec2> world)
        {
            _troops.Clear();
            float lim = -(FrontReach + Clear + 6f);
            foreach (var w in world)
            {
                var p = L(w);
                if (p.y > lim) _troops.Add(p);
            }
        }

        // ------------------------------------------------------------ plan (reguly spaceru)

        /// <summary>Kolumna obozu: druzyna gracza; w armii (gracz jest wodzem albo jest doczepiony do wodza) - wodz i wszystkie doczepione partie
        /// (jeden oboz armii). Gracz idacy dopiero do armii (niedoczepiony) - tylko jego druzyna. leaderBanner - herb rodu wodza armii.</summary>
        internal static List<MobileParty> Column(MobileParty party, out bool army, out Banner leaderBanner)
        {
            var list = new List<MobileParty> { party };
            army = false;
            leaderBanner = null;
            var arm = party.Army;
            var lp = arm != null ? arm.LeaderParty : null;
            if (lp == null || (lp != party && party.AttachedTo != lp)) return list;
            army = true;
            try { var lh = lp.LeaderHero; leaderBanner = lh != null && lh.Clan != null ? lh.Clan.Banner : null; } catch { }
            if (lp != party) list.Add(lp);
            var att = lp.AttachedParties;
            if (att != null)
                for (int i = 0; i < att.Count; i++) { var ap = att[i]; if (ap != null && !list.Contains(ap)) list.Add(ap); }
            return list;
        }

        /// <summary>Plan obozu kolumny: zdrowi ludzie bez bohaterow (najpierw druzyna gracza, kazda partia osobno przetasowana), jezdzcy, konie i juczne
        /// z taboru gracza (bez bydla i owiec; z kazdego stosu najwyzej MaxHorses / 3, razem MaxHorses), wozy CampCart. healthyOverride - test autotestu
        /// (liczba zdrowych do regul namiotow; roster bez zmian). Tylko odczyt.</summary>
        internal static Plan Count(MobileParty party, int perTent, Random rng, int? healthyOverride)
        {
            var p = new Plan();
            bool army;
            Banner lb;
            p.Column = Column(party, out army, out lb);
            p.Army = army;
            p.LeaderBanner = lb;
            int healthy = 0, mounted = 0;
            foreach (var cp in p.Column)
            {
                var roster = cp != null ? cp.MemberRoster : null;
                if (roster == null) continue;
                var group = new List<CharacterObject>();
                foreach (var el in roster.GetTroopRoster())
                {
                    if (el.Character == null || el.Character.IsHero) continue;
                    int n = el.Number - el.WoundedNumber;
                    if (n <= 0) continue;
                    healthy += n;
                    bool horsed = el.Character.IsMounted;
                    if (horsed) mounted += n;
                    for (int i = 0; i < n && p.Troops.Count + group.Count < 4000; i++) group.Add(el.Character);
                    for (int i = 0; horsed && i < n && p.Riders.Count < 4000; i++) p.Riders.Add(el.Character);
                }
                Shuffle(group, rng);
                p.Troops.AddRange(group);
            }
            Shuffle(p.Riders, rng);
            p.Healthy = healthy;
            p.Mounted = mounted;
            if (healthyOverride.HasValue) { p.Healthy = Math.Max(0, healthyOverride.Value); p.HealthyFromTest = true; }
            var items = party.ItemRoster;
            if (items != null)
            {
                for (int i = 0; i < items.Count && p.Train.Count < MaxHorses; i++)
                {
                    var el = items.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || it.HorseComponent == null || it.HorseComponent.IsLiveStock) continue;   // konie i juczne, bez bydla i owiec
                    int k = Math.Min(el.Amount, Math.Max(1, MaxHorses / 3));
                    for (int j = 0; j < k && p.Train.Count < MaxHorses; j++) p.Train.Add(el.EquipmentElement);
                }
            }
            p.Recount(perTent, CampCart.Carts);
            return p;
        }

        private static void Shuffle<T>(List<T> l, Random rng)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; }
        }

        internal string PlanText(Plan p)
        {
            return "namioty " + p.Tents + " (zolnierskich " + p.SoldierTents + "), poslania " + p.Beds + ", ogniska " + p.Fires + ", wozy " + p.Carts + ", konie taboru " + p.TrainHorses;
        }

        /// <summary>Wodz (lokalnie, z dopiskiem przesuniecia) i y pierwszego rzedu - linie testu spaceru i bitwy (porownanie geometrii).</summary>
        internal string GeometryText()
        {
            var ci = CultureInfo.InvariantCulture;
            string lead = LeaderPlaced ? "wodz " + LeaderLocal.x.ToString("0.0", ci) + "," + LeaderLocal.y.ToString("0.0", ci) + (LeaderShifted ? " przesuniety" : "") : "wodz -";
            return lead + " | rzad 1 y " + (float.IsNaN(RowY) ? "-" : RowY.ToString("0.0", ci));
        }

        // ------------------------------------------------------------ uklad (kolejnosc wywolan FreeSpot/Occupy jak dawny spacer)

        internal void Lay(Plan p, int perTent)
        {
            int healthy = p.Healthy;
            // gracz: 6 m przed srodkiem (spacer: patrzy na oboz; bitwa: miejsce zarezerwowane, puste)
            Vec2 me = C - Up * 6f;
            FreeSpot(ref me, Up, 0.6f, null);
            Occupy(Sq(me, 0.8f));
            PlayerSpot = me;
            if (OnPlayerSpot != null) OnPlayerSpot(me);

            // ognisko gracza i jego poslanie (sam gracz albo maly oddzial)
            Vec2 mainFire = C + Right * 3f;
            if (Put(Fire, ref mainFire, Up, 1.2f, true)) { FireSpots.Add(mainFire); Fires++; }
            MainFire = mainFire;
            Vec2 myBed = mainFire + Right * 2.4f - Up * 0.5f;
            if (Put(Bed, ref myBed, -Right, 0.8f, false)) Beds++;
            Vec2 myHorse = mainFire + Right * 4.5f + Up * 2f;
            if (FreeSpot(ref myHorse, Up, 1f, null)) { Occupy(Sq(myHorse, 1.1f)); if (OnHeroHorseSpot != null) OnHeroHorseSpot(myHorse); }

            int tents = p.SoldierTents;
            if (healthy > 0 && tents == 0)
            {
                // do (perTent - 1) ludzi - poslania wokol ogniska gracza (noca na nich spia); kat w ukladzie obozu (spacer: jak dawniej)
                for (int i = 0; i < healthy && i < 8; i++)
                {
                    double ang = Math.PI * 0.4 + i * (Math.PI * 1.6 / Math.Max(1, healthy));
                    Vec2 q = mainFire + (Right * (float)Math.Cos(ang) + Up * (float)Math.Sin(ang)) * 2.6f;
                    if (Put(Bed, ref q, mainFire - q, 0.8f, false)) { Beds++; BedSpots.Add(q); }
                }
            }
            if (healthy >= perTent)
            {
                // namiot wodza za srodkiem obozu (w armii chorazy z herbem wodza armii przed nim - hak spaceru), namiot oficerski z boku (od 60 ludzi)
                Vec2 lead = C + Up * 12f;
                if (Put(TentLeader, ref lead, -Up, 3.5f, true))
                {
                    Tents++;
                    LeaderPlaced = true;
                    LeaderLocal = L(lead);
                    LeaderShifted = Math.Abs(LeaderLocal.x) > 0.05f || Math.Abs(LeaderLocal.y - 12f) > 0.05f;
                    if (OnLeaderTent != null) OnLeaderTent(lead);
                }
                if (healthy >= 60) { Vec2 off = C + Up * 12f + Right * 16f; if (Put(TentOfficer, ref off, -Up, 3f, true)) Tents++; }
                // rzedy namiotow zolnierskich ZA obrysem namiotu wodza i oficerskiego (najdalszy zajety punkt + miejsce na ognisko pierwszego rzedu)
                float far = 6f;
                for (int i = 0; i < _occ.Count; i++) far = Math.Max(far, _occ[i].Y1);
                float rowY = far + 8f;
                RowY = rowY;
                // bitwa: malo miejsca za linia - rzedy szersze (6..12 namiotow), zamiast namiotow za granica sceny; spacer zawsze po 6
                if (DepthLimit < float.MaxValue)
                {
                    float room = DepthLimit - 2.6f - rowY;
                    int rowsFit = room < 0f ? 1 : 1 + (int)(room / RowGap);
                    PerRow = Math.Max(6, Math.Min(12, (tents + rowsFit - 1) / rowsFit));
                }
                // namioty zolnierskie rzedami po PerRow, symetrycznie po obu stronach osi obozu (x = -8, +8, -16, +16, -24, +24 ...), ognisko co trzy namioty
                int placed = 0;
                for (int row = 0; placed < tents && row < 10; row++)
                {
                    for (int col = 0; col < PerRow && placed < tents; col++)
                    {
                        float side = (col % 2 == 0) ? -1f : 1f;
                        Vec2 q = C + Up * (rowY + row * RowGap) + Right * (side * (8f + (col / 2) * 8f));
                        if (Put(TentSmall, ref q, -Up, 2.5f, true)) { Tents++; TentSpots.Add(q); }
                        placed++;
                        if (placed % 3 == 0 && FireSpots.Count < MaxFires)
                        {
                            Vec2 f = q - Up * 5.5f;
                            if (Put(Fire, ref f, Up, 1.2f, true)) { FireSpots.Add(f); Fires++; }
                        }
                    }
                }
            }

            // wozy taborowe gracza z przodu obozu po lewej, konie na postoju z przodu po prawej (taboru w pierwszym rzedzie, jezdzcow w drugim)
            for (int i = 0; i < p.Carts; i++)
            {
                Vec2 q = C - Up * 12f - Right * (6f + i * 5f);
                if (Put(Cart, ref q, Up, 2.4f, true)) Carts++;
            }
            SpawnHorses(p.Train, C - Up * 12f + Right * 6f, Right);
            if (RiderHorses) SpawnRiderHorses(p.Riders, p.Mounted, C - Up * 16f + Right * 6f, Right);
        }

        internal Vec3 Ground(Vec2 p)
        {
            float h = 0f;
            try { h = M.Scene.GetTerrainHeight(p, true); } catch { }
            return new Vec3(p.x, p.y, h);
        }

        // ------------------------------------------------------------ obrysy (bez nakladania elementow obozu; uklad lokalny)

        /// <summary>Kwadrat o boku 2r wokol punktu swiata p - w ukladzie lokalnym.</summary>
        internal Box Sq(Vec2 p, float r)
        {
            var l = L(p);
            return new Box { X0 = l.x - r, Y0 = l.y - r, X1 = l.x + r, Y1 = l.y + r };
        }

        /// <summary>Obrys prefabu name postawionego w p, obroconego jak w Put (os y prefabu = facing); brak w tabeli - kwadrat 2r. Narozniki liczone w swiecie
        /// jak dawniej, obrys w ukladzie lokalnym.</summary>
        internal Box Rect(string name, float r, Vec2 p, Vec2 facing)
        {
            float[] fp;
            if (name == null || !Footprints.TryGetValue(name, out fp)) return Sq(p, r);
            if (facing.LengthSquared < 0.0001f) facing = new Vec2(0f, 1f);
            facing.Normalize();
            var sx = new Vec2(facing.y, -facing.x);   // os x prefabu po obrocie (Mat3.RotateAboutUp: s' = s cos + f sin, przy yaw = atan2(-f.x, f.y))
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float lx = (i & 1) == 0 ? fp[0] : fp[2], ly = (i & 2) == 0 ? fp[1] : fp[3];
                Vec2 c = L(p + sx * lx + facing * ly);
                x0 = Math.Min(x0, c.x); y0 = Math.Min(y0, c.y); x1 = Math.Max(x1, c.x); y1 = Math.Max(y1, c.y);
            }
            return new Box { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1 };
        }

        internal bool Hits(Box b)
        {
            for (int i = 0; i < _occ.Count; i++)
            {
                var o = _occ[i];
                if (b.X1 > o.X0 && b.X0 < o.X1 && b.Y1 > o.Y0 && b.Y0 < o.Y1) return true;
            }
            return false;
        }

        internal void Occupy(Box b) { _occ.Add(b); }

        /// <summary>Bitwa: obrys blizej linii wojska niz Clear (przed FrontY) albo agent w obrysie poszerzonym o Clear. Spacer (Clear 0) - nigdy.</summary>
        private bool NearTroops(Box b)
        {
            if (Clear <= 0f) return false;
            if (b.Y0 < FrontY) return true;
            float x0 = b.X0 - Clear, y0 = b.Y0 - Clear, x1 = b.X1 + Clear, y1 = b.Y1 + Clear;
            for (int i = 0; i < _troops.Count; i++)
            {
                var t = _troops[i];
                if (t.x > x0 && t.x < x1 && t.y > y0 && t.y < y1) return true;
            }
            return false;
        }

        /// <summary>Odleglosc punktu (lokalnie) od obrysu.</summary>
        internal static float Dist(Box b, Vec2 l)
        {
            float dx = Math.Max(Math.Max(b.X0 - l.x, 0f), l.x - b.X1);
            float dy = Math.Max(Math.Max(b.Y0 - l.y, 0f), l.y - b.Y1);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Czy w punkcie swiata p moze stanac element obozu: w granicach pola (granica miekka sceny) - spacer i konie; w bitwie scenografia (prop)
        /// takze do OutsideMax za granica pola, w granicach sceny silnika pomniejszonych o SceneMargin (to tylko wyglad - bez fizyki, ludzie tam nie walcza).
        /// Test "glebokosc=N" (MaxY) - nic dalej niz N m za srodkiem. Spacer (OutsideMax 0, MaxY brak) - jak dotad IsPositionInsideBoundaries.</summary>
        internal bool Inside(Vec2 p, bool prop)
        {
            if (MaxY < float.MaxValue && L(p).y > MaxY) return false;
            if (M.IsPositionInsideBoundaries(p)) return true;
            if (!prop || OutsideMax <= 0f) return false;
            if (!_limRead)
            {
                _limRead = true;
                try
                {
                    Vec3 a, b;
                    M.Scene.GetSceneLimits(out a, out b);
                    _limOk = Fin(a.x) && Fin(a.y) && Fin(b.x) && Fin(b.y) && b.x - a.x > 4f * SceneMargin && b.y - a.y > 4f * SceneMargin;
                    _limMin = new Vec2(a.x + SceneMargin, a.y + SceneMargin);
                    _limMax = new Vec2(b.x - SceneMargin, b.y - SceneMargin);
                }
                catch (Exception e) { _limOk = false; _stumble("SceneLimits", e); }
            }
            if (!_limOk || p.x < _limMin.x || p.y < _limMin.y || p.x > _limMax.x || p.y > _limMax.y) return false;
            try { return (M.GetClosestBoundaryPosition(p) - p).Length <= OutsideMax; }
            catch (Exception e) { _stumble("ClosestBoundary", e); return false; }
        }

        private static bool Fin(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }

        /// <summary>Bitwa: ile uwiazanych koni obozu jest w Mission.MountsWithoutRiders (po Unpool ma byc 0). Tylko odczyt.</summary>
        internal int TiedInPool()
        {
            int n = 0;
            try
            {
                var set = new HashSet<Agent>();
                foreach (var t in TiedHorses) if (t.Agent != null) set.Add(t.Agent);
                foreach (var kv in M.MountsWithoutRiders) if (kv.Key != null && set.Contains(kv.Key)) n++;
            }
            catch (Exception e) { _stumble("TiedInPool", e); return -1; }
            return n;
        }

        /// <summary>Czy w promieniu r wokol punktu cos stoi (drzewo, skala - cialo wyzej niz 0.8 m nad terenem) albo jest woda. Tylko odczyt sceny.</summary>
        internal bool Blocked(Vec2 at, float r)
        {
            try
            {
                var sc = M.Scene;
                for (int i = 0; i < 5; i++)
                {
                    Vec2 p = i == 0 ? at : at + new Vec2(i == 1 ? r : i == 2 ? -r : 0f, i == 3 ? r : i == 4 ? -r : 0f) * 0.7f;
                    float g = sc.GetTerrainHeight(p, true);
                    float top = sc.GetGroundHeightAtPosition(new Vec3(p.x, p.y, 1000f, -1f), TopMask);
                    if (!_topOff && top > g + 0.8f && top < g + 200f) return true;
                    if (i == 0 && !_waterOff)
                    {
                        float w = sc.GetWaterLevelAtPosition(p, false, true);
                        if (w > -5000f && w < 5000f && w > g - 0.05f) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>Probki 7 x 7 co 10 m wokol srodka: gdy woda albo "cos stoi" na ponad 90% probek - ten odczyt jest dla tej sceny pomijany (wzor PD).</summary>
        internal void Probe()
        {
            int all = 0, wet = 0, top = 0;
            try
            {
                var sc = M.Scene;
                for (int ix = -3; ix <= 3; ix++)
                    for (int iy = -3; iy <= 3; iy++)
                    {
                        Vec2 p = C + new Vec2(ix * 10f, iy * 10f);
                        if (!Inside(p, true)) continue;
                        all++;
                        float g = sc.GetTerrainHeight(p, true);
                        float t = sc.GetGroundHeightAtPosition(new Vec3(p.x, p.y, 1000f, -1f), TopMask);
                        if (t > g + 0.8f && t < g + 200f) top++;
                        float w = sc.GetWaterLevelAtPosition(p, false, true);
                        if (w > -5000f && w < 5000f && w > g - 0.05f) wet++;
                    }
            }
            catch (Exception e) { _stumble("Probe", e); }
            _waterOff = all > 0 && wet > all * 0.9f;
            _topOff = all > 0 && top > all * 0.9f;
            Log.Info(_p + "probki terenu " + all + " | woda " + wet + (_waterOff ? " (odczyt wody pomijany)" : "") + " | drzewa/skaly " + top
                     + (_topOff ? " (odczyt cial pomijany)" : "") + ".");
        }

        /// <summary>Wolne miejsce najblizej at (przesuniecia jak PD): w granicach sceny, bez drzew/skal/wody, bez wejscia obrysem (name - prefab z tabeli
        /// Footprints, null - kwadrat 2r) w obrys juz postawionego elementu, w bitwie nie blizej niz Clear od wojska. false - nic wolnego (przyczyna w _why:
        /// wojsko, potem obrysy, teren, granica).</summary>
        internal bool FreeSpot(ref Vec2 at, Vec2 facing, float r, string name)
        {
            if (facing.LengthSquared < 0.0001f) facing = new Vec2(0f, 1f);
            facing.Normalize();
            var side = new Vec2(facing.y, -facing.x);
            bool overlap = false, troops = false, terrain = false;
            for (int i = 0; i < Shifts.GetLength(0); i++)
            {
                Vec2 p = at + side * Shifts[i, 0] + facing * Shifts[i, 1];
                if (!Inside(p, true)) continue;
                if (Blocked(p, r)) { terrain = true; continue; }
                var box = Rect(name, r, p, facing);
                if (Hits(box)) { overlap = true; continue; }
                if (Clear > 0f && NearTroops(box)) { troops = true; continue; }
                if (i > 0) Moved++;
                at = p;
                return true;
            }
            BlockedN++;
            if (overlap) Overlap++;
            _why = troops ? 3 : overlap ? 2 : terrain ? 1 : 0;
            return false;
        }

        private void Skip(int why)
        {
            if (why == 3) SkipTroops++;
            else if (why == 2) SkipOverlap++;
            else if (why == 1) SkipTerrain++;
            else SkipBorder++;
        }

        /// <summary>Prefab z listy (pierwszy istniejacy) w wolnym miejscu najblizej at (at = miejsce koncowe), BEZ FIZYKI (jak CampScene); solid - obrys
        /// zajety dla nastepnych elementow i ludzi (poslania sa plaskie - false). Wywrotka = bez elementu. Kazda porazka liczona z przyczyna (Skip*).</summary>
        internal bool Put(string[] names, ref Vec2 at, Vec2 facing, float r, bool solid)
        {
            string name = CampWalkPrefabs.First(names);
            if (name == null) { SkipPrefab++; return false; }
            if (facing.LengthSquared < 0.0001f) facing = new Vec2(0f, 1f);
            if (!FreeSpot(ref at, facing, r, name)) { Skip(_why); return false; }
            try
            {
                var pos = Ground(at);
                var frame = MatrixFrame.Identity;
                float yaw = (float)Math.Atan2(-facing.x, facing.y);
                frame.rotation.RotateAboutUp(yaw);
                frame.origin = pos;
                var ent = GameEntity.InstantiateWithRestOffset(M.Scene, name, false, frame, 0f);
                if (ent == null) { Failed++; SkipError++; return false; }
                ent.SetLocalPosition(pos);
                var box = Rect(name, r, at, facing);
                if (solid) Occupy(box);
                Placed.Add(box);
                Props++;
                if (OutsideMax > 0f && !M.IsPositionInsideBoundaries(at)) OutsideField++;   // bitwa: stoi za granica pola (linia ukladu)
                if (CountEntities)
                {
                    try { var ch = new List<GameEntity>(); ent.GetChildrenRecursive(ref ch); Entities += 1 + ch.Count; }
                    catch { Entities++; }
                }
                return true;
            }
            catch (Exception e) { Failed++; SkipError++; _stumble("Put(" + name + ")", e); return false; }
        }

        internal void HeroHorse(Hero hero, Vec2 at)
        {
            try
            {
                var eq = hero.BattleEquipment;
                if (eq == null) return;
                var horse = eq[EquipmentIndex.Horse];
                if (horse.IsEmpty || horse.Item == null || !M.IsPositionInsideBoundaries(at)) return;
                var harness = eq[EquipmentIndex.HorseHarness];
                if (Tether(horse, harness, at, Up)) Horses++;
            }
            catch (Exception e) { _stumble("SpawnHeroHorse", e); }
        }

        /// <summary>Konie i juczne z taboru (lista planu), rzedem wzdluz along; kazdy postawiony albo pominiety z przyczyna (granica, teren, obrysy, wojsko,
        /// blad). Wywrotka - reszta listy jako bledy.</summary>
        private void SpawnHorses(List<EquipmentElement> train, Vec2 start, Vec2 along)
        {
            int n = 0, done = 0;
            try
            {
                along.Normalize();
                var side = new Vec2(along.y, -along.x);
                for (int i = 0; i < train.Count && n < MaxHorses; i++)
                {
                    Vec2 p = start + along * (n * 2.2f) + side * (float)(_rng.NextDouble() * 0.6);
                    n++;
                    var box = Sq(p, 1f);
                    if (!Inside(p, false)) { SkipBorder++; done++; continue; }   // konie tylko w polu bitwy (agenci)
                    if (Blocked(p, 1f)) { SkipTerrain++; done++; continue; }
                    if (Hits(box)) { SkipOverlap++; done++; continue; }
                    if (NearTroops(box)) { SkipTroops++; done++; continue; }
                    if (Tether(train[i], default(EquipmentElement), p, side)) { Occupy(box); Placed.Add(box); Horses++; TrainHorses++; }
                    else SkipError++;
                    done++;
                }
            }
            catch (Exception e) { SkipError += Math.Max(0, Math.Min(train.Count, MaxHorses) - done); _stumble("SpawnHorses", e); }
        }

        /// <summary>Konie jezdzcow kolumny (wierzchowce sa w ekwipunku postaci, nie w taborze): jeden na 8 jezdzcow, co najmniej jeden, najwyzej
        /// MaxRiderHorses - kon i siodlo z pierwszego zestawu bojowego jezdzca (wzor postaci), rzedem wzdluz along.</summary>
        private void SpawnRiderHorses(List<CharacterObject> riders, int mounted, Vec2 start, Vec2 along)
        {
            if (riders.Count == 0 || mounted <= 0) return;
            try
            {
                int want = Math.Min(MaxRiderHorses, Math.Max(1, (mounted + 7) / 8));
                along.Normalize();
                var side = new Vec2(along.y, -along.x);
                int n = 0;
                for (int i = 0; i < riders.Count && n < want; i++)
                {
                    var eq = riders[i].FirstBattleEquipment;
                    if (eq == null) continue;
                    var horse = eq[EquipmentIndex.Horse];
                    if (horse.IsEmpty || horse.Item == null || horse.Item.HorseComponent == null) continue;
                    Vec2 p = start + along * (n * 2.2f) + side * (float)(_rng.NextDouble() * 0.6);
                    n++;
                    if (!M.IsPositionInsideBoundaries(p) || Blocked(p, 1f) || Hits(Sq(p, 1f))) continue;
                    if (Tether(horse, eq[EquipmentIndex.HorseHarness], p, side)) { Occupy(Sq(p, 1f)); Placed.Add(Sq(p, 1f)); Horses++; RiderHorsesN++; }
                }
            }
            catch (Exception e) { _stumble("SpawnRiderHorses", e); }
        }

        // ------------------------------------------------------------ uwiazane konie (wzor Practice Drills DrillCampLogic.Tether / Hold / HoldAnimals)

        /// <summary>Kon na postoju: bez wedrowania (gra zdejmuje CanWander tylko zwierzetom sceny - AnimalSpawnSettings) i bez wsiadania (Mountable),
        /// Controller None, predkosc 0, niesmiertelny; miejsce pilnowane w HoldHorses. Bitwa (Unpool): kon zdjety z Mission.MountsWithoutRiders - Agent.Build
        /// wpisuje tam konia bez jezdzca, a Controller None zdejmuje mu CommonAIComponent; spieszony zolnierz SI z rozkazem "Mount" (HumanAIComponent.
        /// FindClosestMountAvailable / ReserveMount) czyta CommonAIComponent konia z puli bez sprawdzenia null - wywrotka w ticku agenta.</summary>
        private bool Tether(EquipmentElement horse, EquipmentElement harness, Vec2 at, Vec2 facing)
        {
            var pos = Ground(at);
            if (facing.LengthSquared < 0.0001f) facing = new Vec2(0f, 1f);
            facing.Normalize();
            var a = M.SpawnMonster(horse, harness, in pos, in facing, -1);
            if (a == null) return false;
            var t = new Tied { Agent = a, Spot = pos, Facing = facing };
            Tie(a);
            if (Unpool) { try { M.RemoveMountWithoutRider(a); } catch (Exception e) { _stumble("RemoveMountWithoutRider", e); } }
            TiedHorses.Add(t);
            return true;
        }

        private static void Tie(Agent a)
        {
            try { a.SetAgentFlags(a.GetAgentFlags() & ~(AgentFlag.CanWander | AgentFlag.Mountable)); } catch { }
            try { a.Controller = AgentControllerType.None; } catch { }
            try { a.SetMaximumSpeedLimit(0f, false); } catch { }
            try { a.SetMortalityState(Agent.MortalityState.Invulnerable); } catch { }
        }

        /// <summary>Wszystkie uwiazane konie na swoje miejsce (spacer co PoseEvery, bitwa co HoldEvery).</summary>
        internal void HoldHorses()
        {
            for (int i = 0; i < TiedHorses.Count; i++) HoldHorse(TiedHorses[i]);
        }

        private void HoldHorse(Tied t)
        {
            try
            {
                var a = t.Agent;
                if (a == null || !a.IsActive() || a.RiderAgent != null) return;
                if (a.Controller != AgentControllerType.None) Tie(a);
                if (a.Position.AsVec2.Distance(t.Spot.AsVec2) > 1.5f)
                {
                    a.TeleportToPosition(t.Spot);
                    a.SetMovementDirection(in t.Facing);
                }
            }
            catch (Exception e) { _stumble("HoldHorse", e); }
        }
    }

    /// <summary>m97-oboz: prefaby sceny obozu sprawdzone raz na proces (GameEntity.PrefabExists) - pierwszy istniejacy z listy kandydatow.</summary>
    internal static class CampWalkPrefabs
    {
        private static readonly Dictionary<string, bool> _exists = new Dictionary<string, bool>();

        internal static string First(string[] names)
        {
            foreach (var n in names)
            {
                bool ok;
                if (!_exists.TryGetValue(n, out ok))
                {
                    try { ok = GameEntity.PrefabExists(n); } catch { ok = false; }
                    _exists[n] = ok;
                    if (!ok) Log.Info(CampWalk.P + "brak prefabu " + n + " - nastepny z listy.");
                }
                if (ok) return n;
            }
            return null;
        }
    }
}
