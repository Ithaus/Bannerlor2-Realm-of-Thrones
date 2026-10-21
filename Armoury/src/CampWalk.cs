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
    /// (misja = stan MissionState nad mapa, kampania nie tyka). Wyjscie: przytrzymanie Tab (MissionLeaveView + BasicLeaveMissionLogic) albo Esc - gra wraca
    /// do menu obozu, z ktorego gracz wszedl.
    ///
    /// Oboz wedlug druzyny (CampLayout): sam gracz (bez zdrowych ludzi) - poslanie przy ognisku i jego kon; do 5 ludzi - poslania wokol ogniska; od 6 ludzi
    /// namioty zolnierskie ROT (Tent_Small, Tent_Small_Campfire; bez ROT - namiot Native) po jednym na CampWalkMenPerTent ludzi, rzedami, ognisko co trzy
    /// namioty; namiot wodza (Tent_Leader) od 6 ludzi; namiot oficerski (Tent_Officer) od 60 ludzi; przy armii (gracz w armii) chorazy z herbem rodu gracza
    /// przed namiotem wodza. Wozy taborowe gracza (CampCart) stoja z boku obozu. Konie na postoju: prawdziwe konie i juczne z taboru gracza (SpawnMonster),
    /// najwyzej MaxHorses, plus kon gracza przy jego posłaniu. Ludzie: zolnierze z druzyny gracza (SimpleAgentOrigin - bez powiazania z rosterem, zadne
    /// zdarzenie misji nie zabierze nikogo z druzyny), najwyzej CampWalkMaxMen (wydajnosc), siedza przy ogniskach, noca czesc spi; poza ta liczba obozu
    /// nie widac w ludziach, tylko w namiotach. Scenografia BEZ FIZYKI (jak CampScene - sprawdzony sposob; gracz przechodzi przez namioty).
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

    /// <summary>m97-oboz: logika sceny spaceru - druzyna gracza, gracz, scenografia obozu, ludzie przy ogniskach, konie; pozy co 1.5 s (tylko postawieni ludzie).</summary>
    internal sealed class CampWalkLogic : MissionLogic
    {
        internal static string SceneName;
        private const float PoseEvery = 1.5f;
        private const int MaxHorses = 12, MaxTents = 40, MaxCarts = 6, MaxFires = 14;
        private const string SitPose = "act_dungeon_prisoner_idle_exhausted", SleepPose = "act_dungeon_prisoner_sleep_cycle", LiePose = "act_dungeon_prisoner_lay_cycle";

        private static readonly string[] TentSmall = { "Tent_Small", "Tent_Small_Campfire", "tent_vlandia_c", "tent_vlandia_a" };
        private static readonly string[] TentLeader = { "Tent_Leader", "tent_vlandia_b", "tent_vlandia_c" };
        private static readonly string[] TentOfficer = { "Tent_Officer", "Tent_Officer_Hangout", "tent_vlandia_a" };
        private static readonly string[] Fire = { "fire_stones_bonfire", "burning_campfire", "campfire" };
        private static readonly string[] Bed = { "bed_ground_a", "bed_ground_b", "bed_ground_c" };
        private static readonly string[] Cart = { "bd_cart_a", "bd_cart_b", "bd_cart_c" };

        private sealed class Sitter { public Agent Agent; public Vec3 Spot; public Vec2 Facing; public string Pose; public float Speed; }
        private readonly List<Sitter> _sitters = new List<Sitter>();
        private readonly Random _rng = new Random();
        private bool _built;
        private float _poseT, _t;
        private int _tents, _fires, _beds, _carts, _men, _sleep, _horses, _props, _failed, _bearer, _moved, _blocked;
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
            CampWalk.Active = false;
            base.OnEndMission();
        }

        // ------------------------------------------------------------ budowa obozu

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
            bool army = party.Army != null;

            var color = hero.MapFaction != null ? hero.MapFaction.Color : uint.MaxValue;
            var color2 = hero.MapFaction != null ? hero.MapFaction.Color2 : uint.MaxValue;
            var banner = Clan.PlayerClan != null ? Clan.PlayerClan.Banner : null;
            var team = m.Teams.Add(BattleSideEnum.Defender, color, color2, banner, true, false, true);
            m.PlayerTeam = team;

            _c = Centre(m);
            var up = new Vec2(0f, 1f);
            var right = new Vec2(1f, 0f);
            Probe(m);

            // zdrowi ludzie druzyny (bez bohaterow) - lista postaci do scenki
            var troops = new List<CharacterObject>();
            int healthy = 0;
            foreach (var el in party.MemberRoster.GetTroopRoster())
            {
                if (el.Character == null || el.Character.IsHero) continue;
                int n = el.Number - el.WoundedNumber;
                if (n <= 0) continue;
                healthy += n;
                for (int i = 0; i < n && troops.Count < 4000; i++) troops.Add(el.Character);
            }
            Shuffle(troops);

            // gracz: 6 m przed srodkiem, patrzy na oboz
            Vec2 me = _c - up * 6f;
            FreeSpot(m, ref me, up, 0.6f);
            var player = SpawnPlayer(m, team, hero, me, up);

            // ognisko gracza i jego poslanie (sam gracz albo maly oddzial)
            var fires = new List<Vec2>();
            Vec2 mainFire = _c + right * 3f;
            if (Put(m, Fire, ref mainFire, up, 1.2f)) { fires.Add(mainFire); _fires++; }
            Vec2 myBed = mainFire + right * 2.4f - up * 0.5f;
            if (Put(m, Bed, ref myBed, -right, 0.8f)) _beds++;
            SpawnHeroHorse(m, hero, mainFire + right * 4.5f + up * 2f, up);

            int tents = healthy >= perTent ? Math.Min(MaxTents, (healthy + perTent - 1) / perTent) : 0;
            if (healthy > 0 && tents == 0)
            {
                // do (perTent - 1) ludzi - poslania wokol ogniska gracza
                for (int i = 0; i < healthy && i < 8; i++)
                {
                    double ang = Math.PI * 0.4 + i * (Math.PI * 1.6 / Math.Max(1, healthy));
                    Vec2 p = mainFire + new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 2.6f;
                    if (Put(m, Bed, ref p, mainFire - p, 0.8f)) _beds++;
                }
            }
            if (healthy >= perTent)
            {
                // namiot wodza za srodkiem obozu, chorazy z herbem przy armii
                Vec2 lead = _c + up * 10f;
                if (Put(m, TentLeader, ref lead, -up, 3.5f)) _tents++;
                if (army && banner != null) SpawnBearer(m, team, troops, lead - up * 5f + right * 3f, -up, banner);
                if (healthy >= 60) { Vec2 off = _c + up * 10f + right * 12f; if (Put(m, TentOfficer, ref off, -up, 3f)) _tents++; }
                // namioty zolnierskie rzedami po 5 po obu stronach, ognisko co trzy namioty
                int placed = 0;
                for (int row = 0; placed < tents && row < 10; row++)
                {
                    for (int col = 0; col < 5 && placed < tents; col++)
                    {
                        float side = (placed % 2 == 0) ? -1f : 1f;
                        Vec2 p = _c + up * (18f + row * 10f) + right * (side * (8f + col * 8f));
                        if (Put(m, TentSmall, ref p, -up, 2.5f)) _tents++;
                        placed++;
                        if (placed % 3 == 0 && fires.Count < MaxFires)
                        {
                            Vec2 f = p - up * 5.5f;
                            if (Put(m, Fire, ref f, up, 1.2f)) { fires.Add(f); _fires++; }
                        }
                    }
                }
            }

            // wozy taborowe gracza z boku obozu
            int carts = Math.Min(MaxCarts, CampCart.Carts);
            for (int i = 0; i < carts; i++)
            {
                Vec2 p = _c - right * 14f + up * (i * 5f);
                if (Put(m, Cart, ref p, up, 2f)) _carts++;
            }

            // konie na postoju: wierzchowce i juczne z taboru, rzadem po drugiej stronie
            SpawnHorses(m, party, _c + right * 16f, up);

            // ludzie przy ogniskach (najwyzej cap), noca czesc spi przy ogniskach
            int men = Math.Min(cap, troops.Count);
            int ti = 0;
            for (int fi = 0; fi < fires.Count && ti < men; fi++)
            {
                int around = Math.Min(4, men - ti);
                for (int k = 0; k < around && ti < men; k++)
                {
                    double ang = (k * Math.PI * 2.0 / 4.0) + _rng.NextDouble() * 0.5 + (fi == 0 ? Math.PI : 0.0);
                    Vec2 p = fires[fi] + new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 1.9f;
                    if (fi == 0 && (p - me).Length < 2.5f) continue;
                    bool sleep = night && _rng.NextDouble() < 0.5;
                    if (SpawnSitter(m, team, troops[ti], p, fires[fi] - p, sleep ? SleepPose : SitPose)) { _men++; if (sleep) _sleep++; }
                    ti++;
                }
            }
            // reszta limitu - lezy przy namiotach (noc: spi, dzien: odpoczywa)
            for (int i = 0; ti < men && i < 60; i++)
            {
                Vec2 p = _c + up * (14f + (i / 6) * 10f) + right * (((i % 6) - 2.5f) * 4f);
                if (!m.IsPositionInsideBoundaries(p)) { ti++; continue; }
                if (SpawnSitter(m, team, troops[ti], p, right, night ? SleepPose : LiePose)) { _men++; if (night) _sleep++; }
                ti++;
            }

            _built = true;
            if (player == null) Log.Player("Your camp is ready, but you could not be placed in it.", true);
            else Log.Player("You walk through your camp. Hold Tab to go back.");
            Log.Info(CampWalk.P + "wejscie | scena " + (SceneName ?? "?") + " | " + (night ? "noc" : "dzien") + (army ? ", armia" : "") + " | ludzi zdrowych " + healthy
                     + " (namiot na " + perTent + ") | namioty " + _tents + " (zolnierskich wedlug ludzi " + tents + "), poslania " + _beds + ", ogniska " + _fires
                     + ", wozy " + _carts + "/" + CampCart.Carts + " | agenci ludzi " + _men + " z limitu " + cap + " (spiacych " + _sleep + ")" + (_bearer > 0 ? ", chorazy z herbem" : "")
                     + " | konie " + _horses + " | elementy " + _props + " (przesuniete przez drzewa/skaly/wode " + _moved + ", pominiete " + _blocked + ")" + (_failed > 0 ? ", nieudane " + _failed : "") + " | gracz " + (player != null ? "tak" : "NIE")
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

        /// <summary>Wolne miejsce najblizej at (przesuniecia jak PD); false - nic wolnego w granicach sceny.</summary>
        private bool FreeSpot(Mission m, ref Vec2 at, Vec2 facing, float r)
        {
            facing.Normalize();
            var side = new Vec2(facing.y, -facing.x);
            for (int i = 0; i < Shifts.GetLength(0); i++)
            {
                Vec2 p = at + side * Shifts[i, 0] + facing * Shifts[i, 1];
                if (!m.IsPositionInsideBoundaries(p) || Blocked(m, p, r)) continue;
                if (i > 0) _moved++;
                at = p;
                return true;
            }
            _blocked++;
            return false;
        }

        /// <summary>Prefab z listy (pierwszy istniejacy) w wolnym miejscu najblizej at (at = miejsce koncowe), BEZ FIZYKI (jak CampScene). Wywrotka = bez elementu.</summary>
        private bool Put(Mission m, string[] names, ref Vec2 at, Vec2 facing, float r)
        {
            string name = CampWalkPrefabs.First(names);
            if (name == null) return false;
            if (facing.LengthSquared < 0.0001f) facing = new Vec2(0f, 1f);
            if (!FreeSpot(m, ref at, facing, r)) return false;
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

        private bool SpawnSitter(Mission m, Team team, CharacterObject ch, Vec2 at, Vec2 facing, string pose)
        {
            if (ch == null || !m.IsPositionInsideBoundaries(at) || Blocked(m, at, 0.5f)) return false;
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
                Ready(a);
                var sit = new Sitter { Agent = a, Spot = pos, Facing = facing, Pose = pose, Speed = 0.8f + (float)_rng.NextDouble() * 0.4f };
                _sitters.Add(sit);
                Hold(sit);
                return true;
            }
            catch (Exception e) { CampWalk.Stumble("SpawnSitter", e); return false; }
        }

        /// <summary>Chorazy z herbem rodu gracza przy namiocie wodza (tylko w armii). Jedna proba; wywrotka - bez choragwi.</summary>
        private void SpawnBearer(Mission m, Team team, List<CharacterObject> troops, Vec2 at, Vec2 facing, Banner banner)
        {
            if (troops.Count == 0) return;
            try
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>("campaign_banner_small");
                if (item == null) return;
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
                var pos = Ground(m, at);
                facing.Normalize();
                var a = m.SpawnMonster(horse, harness, in pos, in facing, -1);
                if (a != null) _horses++;
            }
            catch (Exception e) { CampWalk.Stumble("SpawnHeroHorse", e); }
        }

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
                        if (!m.IsPositionInsideBoundaries(p)) { n++; continue; }
                        var pos = Ground(m, p);
                        var face = side;
                        var a = m.SpawnMonster(el.EquipmentElement, default(EquipmentElement), in pos, in face, -1);
                        if (a != null) _horses++;
                        n++;
                    }
                }
            }
            catch (Exception e) { CampWalk.Stumble("SpawnHorses", e); }
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
