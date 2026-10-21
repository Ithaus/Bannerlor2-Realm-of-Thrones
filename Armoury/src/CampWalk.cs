using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    /// </summary>
    internal static class CampWalk
    {
        internal const string P = "Spacer po obozie (m97): ";
        internal static bool Active;
        private static int _opened, _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

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
    }

    /// <summary>m97-oboz: logika sceny spaceru - kolumna gracza, gracz, scenografia obozu (obrysy bez nakladania), ludzie przy ogniskach, uwiazane konie;
    /// pozy i pilnowanie miejsca co 1.5 s (tylko postawieni ludzie i konie).</summary>
    internal sealed class CampWalkLogic : MissionLogic
    {
        internal static string SceneName;
        private const float PoseEvery = 1.5f;
        private const int MaxHorses = 12, MaxRiderHorses = 10, MaxTents = 40, MaxCarts = 6, MaxFires = 14;
        private const float RowGap = 11f;        // odstep rzedow namiotow (obrys Tent_Small 5.2 m + ognisko 5.5 m przed namiotem z siedzacymi)
        private const string SitPose = "act_dungeon_prisoner_idle_exhausted", SleepPose = "act_dungeon_prisoner_sleep_cycle", LiePose = "act_dungeon_prisoner_lay_cycle";

        private static readonly string[] TentSmall = { "Tent_Small", "Tent_Small_Campfire", "tent_vlandia_c", "tent_vlandia_a" };
        private static readonly string[] TentLeader = { "Tent_Leader", "tent_vlandia_b", "tent_vlandia_c" };
        private static readonly string[] TentOfficer = { "Tent_Officer", "Tent_Officer_Hangout", "tent_vlandia_a" };
        private static readonly string[] Fire = { "fire_stones_bonfire", "burning_campfire", "campfire" };
        private static readonly string[] Bed = { "bed_ground_a", "bed_ground_b", "bed_ground_c" };
        private static readonly string[] Cart = { "bd_cart_a", "bd_cart_b", "bd_cart_c" };

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

        private sealed class Sitter { public Agent Agent; public Vec3 Spot; public Vec2 Facing; public string Pose; public float Speed; }
        private sealed class Tied { public Agent Agent; public Vec3 Spot; public Vec2 Facing; }
        private struct Box { public float X0, Y0, X1, Y1; }
        private readonly List<Sitter> _sitters = new List<Sitter>();
        private readonly List<Tied> _tied = new List<Tied>();
        private readonly List<Box> _occ = new List<Box>();     // zajete obrysy: namioty, ogniska, wozy, konie, ludzie, gracz (poslania sa plaskie - nie licza sie)
        private readonly Random _rng = new Random();
        private bool _built;
        private float _poseT, _t;
        private int _tents, _fires, _beds, _carts, _men, _sleep, _horses, _riderHorses, _props, _failed, _bearer, _moved, _blocked, _overlap, _parties;
        private bool _waterOff, _topOff;   // jak PD: odczyt wody albo cial, ktory "zalewa" prawie cala scene (ponad 90% probek), jest dla tej sceny pomijany
        private Vec2 _c;

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
            if (!_built) return;
            _poseT += dt;
            if (_poseT < PoseEvery) return;
            _poseT = 0f;
            for (int i = 0; i < _sitters.Count; i++) Hold(_sitters[i]);
            for (int i = 0; i < _tied.Count; i++) HoldHorse(_tied[i]);
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
            _tied.Clear();
            _occ.Clear();
            CampWalk.Active = false;
            base.OnEndMission();
        }

        // ------------------------------------------------------------ budowa obozu

        /// <summary>Kolumna obozu: druzyna gracza; w armii (gracz jest wodzem albo jest doczepiony do wodza) - wodz i wszystkie doczepione partie
        /// (jeden oboz armii). Gracz idacy dopiero do armii (niedoczepiony) - tylko jego druzyna. leaderBanner - herb rodu wodza armii.</summary>
        private static List<MobileParty> Column(MobileParty party, out bool army, out Banner leaderBanner)
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

        private void Build()
        {
            var m = Mission;
            var hero = Hero.MainHero;
            var party = MobileParty.MainParty;
            if (m == null || m.Scene == null || hero == null || party == null) return;
            var s = Settings.Current;
            int cap = s != null ? Math.Max(0, Math.Min(100, s.CampWalkMaxMen)) : 40;
            int perTent = s != null ? Math.Max(2, Math.Min(20, s.CampWalkMenPerTent)) : 6;
            // noc obozu: godziny obozu swiata (21-3) albo noc gry (22-2) - wtedy czesc ludzi spi
            bool night = CampaignTime.Now.IsNightTime || NightRest.InCamp(CampaignTime.Now.GetHourOfDay);
            bool army;
            Banner leaderBanner;
            var column = Column(party, out army, out leaderBanner);
            _parties = column.Count;

            var color = hero.MapFaction != null ? hero.MapFaction.Color : uint.MaxValue;
            var color2 = hero.MapFaction != null ? hero.MapFaction.Color2 : uint.MaxValue;
            var banner = Clan.PlayerClan != null ? Clan.PlayerClan.Banner : null;
            var team = m.Teams.Add(BattleSideEnum.Defender, color, color2, banner, true, false, true);
            m.PlayerTeam = team;

            _c = Centre(m);
            var up = new Vec2(0f, 1f);
            var right = new Vec2(1f, 0f);
            Probe(m);

            // zdrowi ludzie kolumny (bez bohaterow) - najpierw druzyna gracza (kazda partia osobno przetasowana), jezdzcy do koni na postoju
            var troops = new List<CharacterObject>();
            var riders = new List<CharacterObject>();
            int healthy = 0, mounted = 0;
            foreach (var cp in column)
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
                    for (int i = 0; i < n && troops.Count + group.Count < 4000; i++) group.Add(el.Character);
                    for (int i = 0; horsed && i < n && riders.Count < 4000; i++) riders.Add(el.Character);
                }
                Shuffle(group);
                troops.AddRange(group);
            }
            Shuffle(riders);

            // gracz: 6 m przed srodkiem, patrzy na oboz
            Vec2 me = _c - up * 6f;
            FreeSpot(m, ref me, up, 0.6f, null);
            Occupy(Sq(me, 0.8f));
            var player = SpawnPlayer(m, team, hero, me, up);

            // ognisko gracza i jego poslanie (sam gracz albo maly oddzial)
            var fires = new List<Vec2>();
            var beds = new List<Vec2>();
            Vec2 mainFire = _c + right * 3f;
            if (Put(m, Fire, ref mainFire, up, 1.2f, true)) { fires.Add(mainFire); _fires++; }
            Vec2 myBed = mainFire + right * 2.4f - up * 0.5f;
            if (Put(m, Bed, ref myBed, -right, 0.8f, false)) _beds++;
            Vec2 myHorse = mainFire + right * 4.5f + up * 2f;
            if (FreeSpot(m, ref myHorse, up, 1f, null)) { Occupy(Sq(myHorse, 1.1f)); SpawnHeroHorse(m, hero, myHorse, up); }

            int tents = healthy >= perTent ? Math.Min(MaxTents, (healthy + perTent - 1) / perTent) : 0;
            if (healthy > 0 && tents == 0)
            {
                // do (perTent - 1) ludzi - poslania wokol ogniska gracza (noca na nich spia)
                for (int i = 0; i < healthy && i < 8; i++)
                {
                    double ang = Math.PI * 0.4 + i * (Math.PI * 1.6 / Math.Max(1, healthy));
                    Vec2 p = mainFire + new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 2.6f;
                    if (Put(m, Bed, ref p, mainFire - p, 0.8f, false)) { _beds++; beds.Add(p); }
                }
            }
            var tentSpots = new List<Vec2>();
            if (healthy >= perTent)
            {
                // namiot wodza za srodkiem obozu, chorazy z herbem wodza armii przed nim (w armii), namiot oficerski z boku (od 60 ludzi)
                Vec2 lead = _c + up * 12f;
                if (Put(m, TentLeader, ref lead, -up, 3.5f, true))
                {
                    _tents++;
                    var flag = leaderBanner ?? banner;
                    if (army && flag != null) SpawnBearer(m, team, troops, lead - up * 5f + right * 3f, -up, flag);
                }
                if (healthy >= 60) { Vec2 off = _c + up * 12f + right * 16f; if (Put(m, TentOfficer, ref off, -up, 3f, true)) _tents++; }
                // rzedy namiotow zolnierskich ZA obrysem namiotu wodza i oficerskiego (najdalszy zajety punkt + miejsce na ognisko pierwszego rzedu)
                float far = 6f;
                for (int i = 0; i < _occ.Count; i++) far = Math.Max(far, _occ[i].Y1 - _c.y);
                float rowY = far + 8f;
                // namioty zolnierskie rzedami po 6, symetrycznie po obu stronach osi obozu (x = -8, +8, -16, +16, -24, +24), ognisko co trzy namioty
                int placed = 0;
                for (int row = 0; placed < tents && row < 10; row++)
                {
                    for (int col = 0; col < 6 && placed < tents; col++)
                    {
                        float side = (col % 2 == 0) ? -1f : 1f;
                        Vec2 p = _c + up * (rowY + row * RowGap) + right * (side * (8f + (col / 2) * 8f));
                        if (Put(m, TentSmall, ref p, -up, 2.5f, true)) { _tents++; tentSpots.Add(p); }
                        placed++;
                        if (placed % 3 == 0 && fires.Count < MaxFires)
                        {
                            Vec2 f = p - up * 5.5f;
                            if (Put(m, Fire, ref f, up, 1.2f, true)) { fires.Add(f); _fires++; }
                        }
                    }
                }
            }

            // wozy taborowe gracza z przodu obozu po lewej, konie na postoju z przodu po prawej (taboru w pierwszym rzedzie, jezdzcow w drugim)
            int carts = Math.Min(MaxCarts, CampCart.Carts);
            for (int i = 0; i < carts; i++)
            {
                Vec2 p = _c - up * 12f - right * (6f + i * 5f);
                if (Put(m, Cart, ref p, up, 2.4f, true)) _carts++;
            }
            SpawnHorses(m, party, _c - up * 12f + right * 6f, right);
            SpawnRiderHorses(m, riders, mounted, _c - up * 16f + right * 6f, right);

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
            Log.Info(CampWalk.P + "wejscie | scena " + (SceneName ?? "?") + " | " + (night ? "noc" : "dzien") + (army ? ", armia (kolumna " + _parties + " partii)" : "") + " | ludzi zdrowych " + healthy
                     + " (namiot na " + perTent + ") | namioty " + _tents + " (zolnierskich wedlug ludzi " + tents + ", postawionych " + tentSpots.Count + "), poslania " + _beds + ", ogniska " + _fires
                     + ", wozy " + _carts + "/" + CampCart.Carts + " | agenci ludzi " + _men + " z limitu " + cap + " (spiacych " + _sleep + ")" + (_bearer > 0 ? ", chorazy z herbem" : "")
                     + " | konie " + _horses + " (jezdzcow " + _riderHorses + " z " + mounted + " jezdzcow, uwiazane " + _tied.Count + ")"
                     + " | elementy " + _props + " (przesuniete przez drzewa/skaly/wode/obrysy " + _moved + ", pominiete " + _blocked + ", nakladanie obrysow " + _overlap + ")"
                     + (_failed > 0 ? ", nieudane " + _failed : "") + " | gracz " + (player != null ? "tak" : "NIE")
                     + " | srodek " + _c.x.ToString("0", CultureInfo.InvariantCulture) + "," + _c.y.ToString("0", CultureInfo.InvariantCulture) + ".");
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

        private Vec3 Ground(Mission m, Vec2 p)
        {
            float h = 0f;
            try { h = m.Scene.GetTerrainHeight(p, true); } catch { }
            return new Vec3(p.x, p.y, h);
        }

        // ------------------------------------------------------------ obrysy (bez nakladania elementow obozu)

        private static Box Sq(Vec2 p, float r) { return new Box { X0 = p.x - r, Y0 = p.y - r, X1 = p.x + r, Y1 = p.y + r }; }

        /// <summary>Obrys prefabu name postawionego w p, obroconego jak w Put (os y prefabu = facing); brak w tabeli - kwadrat 2r.</summary>
        private static Box Rect(string name, float r, Vec2 p, Vec2 facing)
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
                Vec2 c = p + sx * lx + facing * ly;
                x0 = Math.Min(x0, c.x); y0 = Math.Min(y0, c.y); x1 = Math.Max(x1, c.x); y1 = Math.Max(y1, c.y);
            }
            return new Box { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1 };
        }

        private bool Hits(Box b)
        {
            for (int i = 0; i < _occ.Count; i++)
            {
                var o = _occ[i];
                if (b.X1 > o.X0 && b.X0 < o.X1 && b.Y1 > o.Y0 && b.Y0 < o.Y1) return true;
            }
            return false;
        }

        private void Occupy(Box b) { _occ.Add(b); }

        // wzor Practice Drills (public domain): najwyzsze cialo fizyczne nad punktem (drzewo, skala, budynek sceny) i woda; maski cial jak w PD (Top / Ground)
        private const BodyFlags TopMask = (BodyFlags)7378688;
        private static readonly float[,] Shifts = { { 0f, 0f }, { 3f, 0f }, { -3f, 0f }, { 0f, -4f }, { 0f, 4f }, { 5f, 0f }, { -5f, 0f }, { 3f, -5f }, { -3f, 5f } };

        /// <summary>Czy w promieniu r wokol punktu cos stoi (drzewo, skala - cialo wyzej niz 0.8 m nad terenem) albo jest woda. Tylko odczyt sceny.</summary>
        private bool Blocked(Mission m, Vec2 at, float r)
        {
            try
            {
                var sc = m.Scene;
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
        private void Probe(Mission m)
        {
            int all = 0, wet = 0, top = 0;
            try
            {
                var sc = m.Scene;
                for (int ix = -3; ix <= 3; ix++)
                    for (int iy = -3; iy <= 3; iy++)
                    {
                        Vec2 p = _c + new Vec2(ix * 10f, iy * 10f);
                        if (!m.IsPositionInsideBoundaries(p)) continue;
                        all++;
                        float g = sc.GetTerrainHeight(p, true);
                        float t = sc.GetGroundHeightAtPosition(new Vec3(p.x, p.y, 1000f, -1f), TopMask);
                        if (t > g + 0.8f && t < g + 200f) top++;
                        float w = sc.GetWaterLevelAtPosition(p, false, true);
                        if (w > -5000f && w < 5000f && w > g - 0.05f) wet++;
                    }
            }
            catch (Exception e) { CampWalk.Stumble("Probe", e); }
            _waterOff = all > 0 && wet > all * 0.9f;
            _topOff = all > 0 && top > all * 0.9f;
            Log.Info(CampWalk.P + "probki terenu " + all + " | woda " + wet + (_waterOff ? " (odczyt wody pomijany)" : "") + " | drzewa/skaly " + top
                     + (_topOff ? " (odczyt cial pomijany)" : "") + ".");
        }

        /// <summary>Wolne miejsce najblizej at (przesuniecia jak PD): w granicach sceny, bez drzew/skal/wody i bez wejscia obrysem (name - prefab z tabeli
        /// Footprints, null - kwadrat 2r) w obrys juz postawionego elementu. false - nic wolnego.</summary>
        private bool FreeSpot(Mission m, ref Vec2 at, Vec2 facing, float r, string name)
        {
            if (facing.LengthSquared < 0.0001f) facing = new Vec2(0f, 1f);
            facing.Normalize();
            var side = new Vec2(facing.y, -facing.x);
            bool overlap = false;
            for (int i = 0; i < Shifts.GetLength(0); i++)
            {
                Vec2 p = at + side * Shifts[i, 0] + facing * Shifts[i, 1];
                if (!m.IsPositionInsideBoundaries(p) || Blocked(m, p, r)) continue;
                if (Hits(Rect(name, r, p, facing))) { overlap = true; continue; }
                if (i > 0) _moved++;
                at = p;
                return true;
            }
            _blocked++;
            if (overlap) _overlap++;
            return false;
        }

        /// <summary>Prefab z listy (pierwszy istniejacy) w wolnym miejscu najblizej at (at = miejsce koncowe), BEZ FIZYKI (jak CampScene); solid - obrys
        /// zajety dla nastepnych elementow i ludzi (poslania sa plaskie - false). Wywrotka = bez elementu.</summary>
        private bool Put(Mission m, string[] names, ref Vec2 at, Vec2 facing, float r, bool solid)
        {
            string name = CampWalkPrefabs.First(names);
            if (name == null) return false;
            if (facing.LengthSquared < 0.0001f) facing = new Vec2(0f, 1f);
            if (!FreeSpot(m, ref at, facing, r, name)) return false;
            try
            {
                var pos = Ground(m, at);
                var frame = MatrixFrame.Identity;
                float yaw = (float)Math.Atan2(-facing.x, facing.y);
                frame.rotation.RotateAboutUp(yaw);
                frame.origin = pos;
                var ent = GameEntity.InstantiateWithRestOffset(m.Scene, name, false, frame, 0f);
                if (ent == null) { _failed++; return false; }
                ent.SetLocalPosition(pos);
                if (solid) Occupy(Rect(name, r, at, facing));
                _props++;
                return true;
            }
            catch (Exception e) { _failed++; CampWalk.Stumble("Put(" + name + ")", e); return false; }
        }

        private Agent SpawnPlayer(Mission m, Team team, Hero hero, Vec2 at, Vec2 facing)
        {
            try
            {
                var ch = hero.CharacterObject;
                var pos = Ground(m, at);
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
            if (ch == null || !m.IsPositionInsideBoundaries(at) || Blocked(m, at, 0.5f)) return false;
            var box = Sq(at, r);
            if (Hits(box)) return false;
            try
            {
                var pos = Ground(m, at);
                if (facing.LengthSquared < 0.0001f) facing = new Vec2(0f, 1f);
                facing.Normalize();
                var abd = new AgentBuildData(ch).Team(team).InitialPosition(in pos).InitialDirection(in facing)
                    .Controller(AgentControllerType.None).NoHorses(true).CivilianEquipment(true).NoWeapons(true)
                    .TroopOrigin(new SimpleAgentOrigin(ch, -1, null, default(UniqueTroopDescriptor)))
                    .ClothingColor1(team.Color).ClothingColor2(team.Color2);
                var a = m.SpawnAgent(abd, false);
                if (a == null) return false;
                Occupy(box);
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
                if (!FreeSpot(m, ref at, facing, 0.6f, null)) return;
                var ch = troops[troops.Count - 1];
                troops.RemoveAt(troops.Count - 1);
                var pos = Ground(m, at);
                facing.Normalize();
                var abd = new AgentBuildData(ch).Team(team).InitialPosition(in pos).InitialDirection(in facing)
                    .Controller(AgentControllerType.None).NoHorses(true).CivilianEquipment(true)
                    .TroopOrigin(new SimpleAgentOrigin(ch, -1, banner, default(UniqueTroopDescriptor)))
                    .Banner(banner).BannerItem(item)
                    .ClothingColor1(team.Color).ClothingColor2(team.Color2);
                var a = m.SpawnAgent(abd, false);
                if (a == null) return;
                Occupy(Sq(at, 0.6f));
                try { a.TryToWieldWeaponInSlot(EquipmentIndex.ExtraWeaponSlot, Agent.WeaponWieldActionType.Instant, true); } catch { }
                _bearer++;
            }
            catch (Exception e) { CampWalk.Stumble("SpawnBearer", e); }
        }

        private void SpawnHeroHorse(Mission m, Hero hero, Vec2 at, Vec2 facing)
        {
            try
            {
                var eq = hero.BattleEquipment;
                if (eq == null) return;
                var horse = eq[EquipmentIndex.Horse];
                if (horse.IsEmpty || horse.Item == null || !m.IsPositionInsideBoundaries(at)) return;
                var harness = eq[EquipmentIndex.HorseHarness];
                if (Tether(m, horse, harness, at, facing)) _horses++;
            }
            catch (Exception e) { CampWalk.Stumble("SpawnHeroHorse", e); }
        }

        /// <summary>Konie i juczne z taboru gracza (bez bydla i owiec), najwyzej MaxHorses, rzedem wzdluz along.</summary>
        private void SpawnHorses(Mission m, MobileParty party, Vec2 start, Vec2 along)
        {
            try
            {
                var roster = party.ItemRoster;
                if (roster == null) return;
                along.Normalize();
                var side = new Vec2(along.y, -along.x);
                int n = 0;
                for (int i = 0; i < roster.Count && n < MaxHorses; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || it.HorseComponent == null || it.HorseComponent.IsLiveStock) continue;   // konie i juczne, bez bydla i owiec
                    int k = Math.Min(el.Amount, Math.Max(1, MaxHorses / 3));
                    for (int j = 0; j < k && n < MaxHorses; j++)
                    {
                        Vec2 p = start + along * (n * 2.2f) + side * (float)(_rng.NextDouble() * 0.6);
                        n++;
                        if (!m.IsPositionInsideBoundaries(p) || Blocked(m, p, 1f) || Hits(Sq(p, 1f))) continue;
                        if (Tether(m, el.EquipmentElement, default(EquipmentElement), p, side)) { Occupy(Sq(p, 1f)); _horses++; }
                    }
                }
            }
            catch (Exception e) { CampWalk.Stumble("SpawnHorses", e); }
        }

        /// <summary>Konie jezdzcow kolumny (wierzchowce sa w ekwipunku postaci, nie w taborze): jeden na 8 jezdzcow, co najmniej jeden, najwyzej
        /// MaxRiderHorses - kon i siodlo z pierwszego zestawu bojowego jezdzca (wzor postaci), rzedem wzdluz along.</summary>
        private void SpawnRiderHorses(Mission m, List<CharacterObject> riders, int mounted, Vec2 start, Vec2 along)
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
                    if (!m.IsPositionInsideBoundaries(p) || Blocked(m, p, 1f) || Hits(Sq(p, 1f))) continue;
                    if (Tether(m, horse, eq[EquipmentIndex.HorseHarness], p, side)) { Occupy(Sq(p, 1f)); _horses++; _riderHorses++; }
                }
            }
            catch (Exception e) { CampWalk.Stumble("SpawnRiderHorses", e); }
        }

        // ------------------------------------------------------------ uwiazane konie (wzor Practice Drills DrillCampLogic.Tether / Hold / HoldAnimals)

        /// <summary>Kon na postoju: bez wedrowania (gra zdejmuje CanWander tylko zwierzetom sceny - AnimalSpawnSettings) i bez wsiadania (Mountable),
        /// Controller None, predkosc 0, niesmiertelny; miejsce pilnowane w HoldHorse.</summary>
        private bool Tether(Mission m, EquipmentElement horse, EquipmentElement harness, Vec2 at, Vec2 facing)
        {
            var pos = Ground(m, at);
            if (facing.LengthSquared < 0.0001f) facing = new Vec2(0f, 1f);
            facing.Normalize();
            var a = m.SpawnMonster(horse, harness, in pos, in facing, -1);
            if (a == null) return false;
            var t = new Tied { Agent = a, Spot = pos, Facing = facing };
            Tie(a);
            _tied.Add(t);
            return true;
        }

        private static void Tie(Agent a)
        {
            try { a.SetAgentFlags(a.GetAgentFlags() & ~(AgentFlag.CanWander | AgentFlag.Mountable)); } catch { }
            try { a.Controller = AgentControllerType.None; } catch { }
            try { a.SetMaximumSpeedLimit(0f, false); } catch { }
            try { a.SetMortalityState(Agent.MortalityState.Invulnerable); } catch { }
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
            catch (Exception e) { CampWalk.Stumble("HoldHorse", e); }
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

        private void Shuffle<T>(List<T> l)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = _rng.Next(i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; }
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
