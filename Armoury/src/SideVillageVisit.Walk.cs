using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using SandBox;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.View;
using Path = System.IO.Path;

namespace Armoury
{
    /// <summary>
    /// 474-P7 (Jeff 13.11 "Wioski, i karczmy rob wszystko", projekt docs/PROJEKT-WSIE-POBOCZNE-KARCZMY-2026-11-13.md pkt 1 "Spacer jak po wsi glownej", paczka 7):
    /// SPACER 3D PO WIOSCE POBOCZNEJ. W menu wioski pobocznej (arm_sidevil - ZWYKLE menu, nie menu oczekiwania: otwarcie misji z opcji jak "Walk through the camp"
    /// z arm_camp_wait) opcja "Walk through the village" otwiera wlasna misje typu "Camp" (ten sam typ i zestaw widokow co CampWalk: kamera, Esc, Tab) na scenie
    /// "village_center" WSI GRY tej samej kultury co wies okregu - wybieranej zawsze tak samo dla danej wioski (skrot uid), z pominieciem sceny samej wsi okregu,
    /// gdy kultura ma inne (wioska nie wyglada jak wies obok). Wioski poboczne nie maja wlasnych scen - to jest wada tej drogi (projekt).
    /// DOWOD (dekompilacja 1.4.8): VillageEncounter.CreateAndOpenMissionController -> CampaignMission.OpenVillageMission(location.GetSceneName(1)) ->
    /// SandBoxMissions.OpenVillageMission: CreateSandBoxMissionInitializerRecord(scene, "land_raid", false, DecalAtlasGroup.Town) - ten sam rekord bierzemy tu.
    /// Scena wsi BEZ logik osady (MissionAgentHandler, MissionLocationLogic) dziala: rabunek wsi gry otwiera te sama scene "village_center" przez
    /// OpenBattleMission (zwykla bitwa, bez logik osady). Drogi gry (spotkanie z osada, LocationComplex) NIE uzywamy: wioska poboczna nie jest osada, a wejscie
    /// odpala zdarzenia osady z wieloma latkami (projekt: ryzyko regresji).
    /// Ludzie: chlopi KULTURY WSI OKREGU (CultureObject.Villager, VillageWoman, VillagerMaleTeenager/FemaleTeenager, VillagerMaleChild/FemaleChild) na punktach
    /// sceny "npc_common" / "npc_common_limited" (te same punkty, na ktorych gra stawia mieszkancow wsi), stoja (Controller.None - bez AI, bez zachowan osady);
    /// liczba z ludzi wioski w domu (1 na 12, od 4 do MaxFolk), noca trzecia czesc. SimpleAgentOrigin - bez powiazania z rosterem ani z ksiega wiosek
    /// (nikogo nie ubywa, nic sie nie liczy). Gracz na "spawnpoint_player" (zapas: sp_player_conversation, spawnpoint_player_outside, pierwszy punkt npc_common,
    /// srodek granic). Czas kampanii stoi (misja nad mapa), wyjscie tylko Tab (BasicLeaveMissionLogic + MissionLeaveView) - powrot do menu wioski.
    /// DTE: misja cywilna (DoesMissionRequireCivilianEquipment = true w delegacie, jak CampWalk).
    /// Nie w wojnie z panem okregu, nie w czasie rabunku okregu, nie w spalonej / opuszczonej wiosce, nie w innej scenie (CampWalk.Active, Mission.Current).
    /// Test autotestu (LogGate.Mode 1/2 + plik CrashScribe\armoury-wies-spacer.flag): do 3 spacerow (wioski 3 roznych kultur okregu, pierwsze z ukladu),
    /// kazdy przy wolnej chwili na mapie po wyjsciu z osady, wyjscie po 8 s rzeczywistych. W zwyklej grze WalkTestTick zamyka sie po pierwszym sprawdzeniu.
    /// Klucz SideVillageWalk (MCM "Side Village Walk"); NIE = brak opcji, nic wiecej nie biegnie (test tez nie).
    /// Linie logu "Spacer po wiosce (474-P7): ..." - start sesji, otwieranie (scena, wies wzorcowa), wejscie (gracz, chlopi, punkty), wyjscie, test.
    /// </summary>
    internal static partial class SideVillageVisit
    {
        internal const string WP = "Spacer po wiosce (474-P7): ";
        private const string WalkTestFlag = "armoury-wies-spacer.flag";
        internal const int WalkMaxFolk = 20;
        internal static bool WalkActive;
        private static int _wOpened, _wSt;
        private static readonly HashSet<string> _wErr = new HashSet<string>(StringComparer.Ordinal);
        // sceny "village_center" wsi gry wedlug kultury (raz na kampanie, leniwie przy pierwszym spacerze): kultura -> (scena, wies) bez powtorzen scen
        private static Dictionary<string, List<KeyValuePair<string, string>>> _wScenes;
        private static object _wScenesFor;
        // test autotestu: 0 - nie sprawdzono, 1 - czeka, 2 - koniec
        private static int _wTestState, _wTestWaitLines;
        private static bool _wTestSeenTown;
        private static long _wTestNextMs;
        private static List<string> _wTestUids;

        internal static bool WalkOn { get { var s = Settings.Current; return s != null && s.SideVillageWalk && On; } }

        internal static void WalkStumble(string where, Exception e)
        {
            _wSt++;
            bool first; lock (_wErr) first = _wErr.Add(where);
            if (first || _wSt % 200 == 0) { try { Log.Error("SideVillageWalk." + where + " (potkniecie " + _wSt + ")", e); } catch { } }
        }

        internal static int WalkStumbles { get { return _wSt; } }

        /// <summary>ArmouryBehavior, OnAfterSessionLaunched PO LandSale.AddMenus (jedna linia): opcja na poczatku menu wioski pobocznej - indeks 0 wstawiany po wszystkich
        /// opcjach z indeksem (nocleg 0 ... LandSale 6), wiec pozostale opcje zachowuja wzajemna kolejnosc co do jednej.</summary>
        internal static void AddWalkMenu(CampaignGameStarter starter)
        {
            try
            {
                WalkActive = false;
                starter.AddGameMenuOption(Menu, "arm_sidevil_walk", "{=!}Walk through the village", WalkCondition,
                    delegate (MenuCallbackArgs a) { WalkStart(_uid, false); }, false, 0);
                Log.Info(WP + "opcja \"Walk through the village\" w menu wioski pobocznej (" + Menu + ") | klucz Side Village Walk " + (WalkOn ? "TAK" : "NIE")
                         + " | chlopow w scenie najwyzej " + WalkMaxFolk + ".");
            }
            catch (Exception e) { WalkStumble("AddMenus", e); }
        }

        private static bool WalkCondition(MenuCallbackArgs a)
        {
            if (!WalkOn) return false;
            try
            {
                a.optionLeaveType = GameMenuOption.LeaveType.Mission;
                var r = Rec(_uid);
                var st = DistOf(r);
                if (st == null) return false;
                string why = WalkBlock(r, st);
                if (why != null) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true; }
                a.Tooltip = new TextObject("{=!}Walk among the homesteads of " + NameOf(r) + " and meet its folk. Side villages have no scenes of their own - you walk a village of the same people. Time stands still while you walk; hold Tab to go back.");
                return true;
            }
            catch (Exception e) { WalkStumble("Condition", e); return false; }
        }

        /// <summary>null = mozna isc; inaczej powod po angielsku (podpowiedz szarej opcji). Warunki sceny jak CampWalk.CanWalk (bez osady, spotkania, bitwy, misji).</summary>
        private static string WalkBlock(MapVillageRecord r, Settlement st)
        {
            var mp = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
            if (Campaign.Current == null || mp == null || Hero.MainHero == null) return "No campaign is running.";
            if (mp.MapEvent != null || mp.SiegeEvent != null || PlayerEncounter.Current != null) return "Not while in a battle or siege.";
            if (mp.CurrentSettlement != null) return "Leave the settlement first.";
            if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return "Only on land.";
            if (Mission.Current != null || WalkActive || CampWalk.Active) return "Already in a scene.";
            if (!Hero.MainHero.IsAlive || Hero.MainHero.IsPrisoner) return "Not now.";
            if (AtWar(st)) return "You are at war with " + Txt(st.MapFaction.Name) + " - the villagers bar their doors against you.";
            if (st.Village != null && st.Village.VillageState == Village.VillageStates.BeingRaided) return "Raiders are on the lands of " + Txt(st.Name) + " - the village is barred and quiet.";
            VillageBook.PartView me; double homeAll, nAll;
            if (PartOf(st, r != null ? r.Uid : null, out me, out homeAll, out nAll) && Burned(me)) return "The village lies empty - there is nobody to meet and little left standing.";
            return null;
        }

        private static void WalkStart(string uid, bool test)
        {
            try
            {
                if (!WalkOn) return;
                var r = Rec(uid);
                var st = DistOf(r);
                if (st == null) { Log.Player("There is nowhere to walk here.", true); return; }
                if (!test)
                {
                    string why = WalkBlock(r, st);
                    if (why != null) { Log.Player(why, true); return; }
                }
                string from; int n;
                string scene = WalkScene(st, uid, out from, out n);
                if (string.IsNullOrEmpty(scene)) { Log.Player("There is nowhere to walk here.", true); Log.Info(WP + "brak sceny wsi dla kultury " + CultId(st) + " (" + NameOf(r) + ", " + uid + ")."); return; }
                VillageBook.PartView me; double homeAll, nAll;
                double home = PartOf(st, uid, out me, out homeAll, out nAll) ? me.Home : 0.0;
                WalkActive = true;
                _wOpened++;
                SideVillageWalkLogic.Setup(uid, NameOf(r), st.Culture, home, test);
                var rec = SandBoxMissions.CreateSandBoxMissionInitializerRecord(scene, "land_raid", false, DecalAtlasGroup.Town);
                MissionState.OpenNew("Camp", rec, delegate (Mission m)
                {
                    try { m.DoesMissionRequireCivilianEquipment = true; } catch (Exception e) { WalkStumble("Civilian", e); }
                    return new MissionBehavior[]
                    {
                        new MissionOptionsComponent(),
                        new CampaignMissionComponent(),
                        new BasicLeaveMissionLogic(),
                        new MissionHardBorderPlacer(),
                        new MissionBoundaryPlacer(),
                        new MissionBoundaryCrossingHandler(10f),
                        new EquipmentControllerLeaveLogic(),
                        new SideVillageWalkLogic(),
                        ViewCreator.CreateMissionLeaveView()
                    };
                }, true, true);
                Log.Info(WP + "otwieranie - " + NameOf(r) + " (" + uid + ", okreg " + Txt(st.Name) + " " + st.StringId + ", kultura " + CultId(st) + ") | scena " + scene
                         + " (wies wzorcowa " + from + ", scen tej kultury " + n + ") | godzina " + CampaignTime.Now.GetHourOfDay + (CampaignTime.Now.IsNightTime ? " (noc)" : " (dzien)")
                         + " | ludzi w domu " + Math.Round(home).ToString("0", CultureInfo.InvariantCulture) + (test ? " | TEST" : "") + " | wejscie nr " + _wOpened + " w tej sesji.");
            }
            catch (Exception e)
            {
                WalkActive = false;
                WalkStumble("Open", e);
                Log.Player("The village cannot be entered right now.", true);
            }
        }

        private static string CultId(Settlement st) { return st != null && st.Culture != null ? st.Culture.StringId : "-"; }

        /// <summary>Scena "village_center" wsi gry kultury wsi okregu: lista scen kultury (bez powtorzen, porzadek nazw), indeks ze skrotu uid (zawsze ta sama dla wioski);
        /// scena samej wsi okregu pominieta, gdy kultura ma inne. Kultura bez scen - scena wsi okregu.</summary>
        private static string WalkScene(Settlement st, string uid, out string from, out int n)
        {
            from = "-"; n = 0;
            if (_wScenes == null || !ReferenceEquals(_wScenesFor, Campaign.Current))
            {
                var d = new Dictionary<string, List<KeyValuePair<string, string>>>(StringComparer.Ordinal);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var s in Settlement.All)
                {
                    if (s == null || !s.IsVillage || s.Culture == null) continue;
                    string sc = SceneOf(s);
                    if (string.IsNullOrEmpty(sc)) continue;
                    string key = s.Culture.StringId + "|" + sc;
                    if (!seen.Add(key)) continue;
                    List<KeyValuePair<string, string>> l;
                    if (!d.TryGetValue(s.Culture.StringId, out l)) d[s.Culture.StringId] = l = new List<KeyValuePair<string, string>>();
                    l.Add(new KeyValuePair<string, string>(sc, s.StringId));
                }
                foreach (var l in d.Values) l.Sort((x, y) => string.CompareOrdinal(x.Key, y.Key));
                _wScenes = d; _wScenesFor = Campaign.Current;
                Log.Info(WP + "sceny wsi wedlug kultury - " + d.Count + " kultur, " + seen.Count + " scen.");
            }
            string own = SceneOf(st);
            List<KeyValuePair<string, string>> all;
            if (st.Culture == null || !_wScenes.TryGetValue(st.Culture.StringId, out all) || all.Count == 0) { from = st.StringId; n = 0; return own; }
            var pick = all;
            if (all.Count > 1 && own != null)
            {
                pick = new List<KeyValuePair<string, string>>(all.Count);
                foreach (var kv in all) if (!string.Equals(kv.Key, own, StringComparison.Ordinal)) pick.Add(kv);
                if (pick.Count == 0) pick = all;
            }
            n = all.Count;
            uint h = 2166136261u;
            foreach (char c in uid ?? "") { h ^= c; h *= 16777619u; }
            var k = pick[(int)(h % (uint)pick.Count)];
            from = k.Value;
            return k.Key;
        }

        private static string SceneOf(Settlement s)
        {
            try
            {
                var lc = s.LocationComplex;
                Location loc = lc != null ? lc.GetLocationWithId("village_center") : null;
                return loc != null ? loc.GetSceneName(1) : null;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------ test autotestu (plik CrashScribe\armoury-wies-spacer.flag)

        private static bool WalkTestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try { return File.Exists(Path.Combine(CampWalk.CrashScribeDir(), WalkTestFlag)); }
            catch { return false; }
        }

        /// <summary>SubModuleMain.OnApplicationTick (jedna linia): poza kampania i po zamknieciu testu - od razu wyjscie (jedno porownanie).</summary>
        internal static void WalkTestTick()
        {
            if (_wTestState == 2 || Campaign.Current == null) return;
            try { WalkTestStep(); }
            catch (Exception e) { _wTestState = 2; WalkStumble("TestTick", e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WalkTestStep()
        {
            if (_wTestState == 0)
            {
                if (!WalkTestWanted() || !WalkOn) { _wTestState = 2; return; }
                _wTestState = 1;
                Log.Info(WP + "test - plik " + WalkTestFlag + ": do 3 spacerow (wioski 3 roznych kultur), kazdy przy wolnej chwili na mapie po wyjsciu z osady, wyjscie po 8 s.");
            }
            if (WalkActive) return;
            string why = WalkTestGate();
            if (why == null)
            {
                if (_wTestUids == null) _wTestUids = WalkTestPick();
                if (_wTestUids.Count == 0) { _wTestState = 2; Log.Info(WP + "test - brak wiosek pobocznych w ukladzie - koniec testu."); return; }
                string uid = _wTestUids[0];
                _wTestUids.RemoveAt(0);
                if (_wTestUids.Count == 0) _wTestState = 2;
                Log.Info(WP + "test - otwieram spacer w " + NameOf(Rec(uid)) + " (" + uid + "), zostalo " + _wTestUids.Count + ".");
                WalkStart(uid, true);
                if (!WalkActive) Log.Info(WP + "test - spacer nie otworzyl sie (zob. ERROR in SideVillageWalk.Open).");
                _wTestWaitLines = 0;
                return;
            }
            long now = (long)(System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (_wTestWaitLines < 5 && (_wTestWaitLines == 0 || now >= _wTestNextMs))
            {
                _wTestWaitLines++;
                _wTestNextMs = now + 120000;
                Log.Info(WP + "test - czekam (" + why + ").");
            }
        }

        /// <summary>Wioski testu: pierwsze z ukladu, ktorych wies okregu ma 3 rozne kultury.</summary>
        private static List<string> WalkTestPick()
        {
            var res = new List<string>();
            var cults = new HashSet<string>(StringComparer.Ordinal);
            var l = VillageBook.LayoutRecords();
            if (l == null) return res;
            foreach (var r in l)
            {
                if (r == null || string.IsNullOrEmpty(r.Uid)) continue;
                var st = DistOf(r);
                if (st == null || st.Culture == null || !cults.Add(st.Culture.StringId)) continue;
                res.Add(r.Uid);
                if (res.Count >= 3) break;
            }
            return res;
        }

        private static string WalkTestGate()
        {
            var c = Campaign.Current;
            var gsm = GameStateManager.Current;
            var active = gsm != null ? gsm.ActiveState : null;
            if (!(active is TaleWorlds.CampaignSystem.GameState.MapState)) return "nie mapa (" + (active != null ? active.GetType().Name : "-") + ")";
            if (LoadingWindow.IsLoadingWindowActive) return "ekran wczytywania";
            var msc = SandBox.View.Map.MapScreen.Instance;
            if (msc != null && !msc.IsReady) return "mapa jeszcze nie gotowa";
            var mp = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
            if (mp != null && mp.CurrentSettlement != null) { _wTestSeenTown = true; return "w osadzie - spacer po wyjsciu"; }
            if (!_wTestSeenTown) return "jeszcze nie bylo osady - autotest otwiera spacer po wyjsciu z miasta";
            if (c.CurrentMenuContext != null) return "menu";
            var cm = c.ConversationManager;
            if (cm != null && (cm.IsConversationFlowActive || cm.IsConversationInProgress)) return "rozmowa";
            if (gsm.ActiveStateDisabledByUser) return "menu nad mapa";
            if (InformationManager.IsAnyInquiryActive()) return "okno (zapytanie)";
            if (mp == null || mp.MapEvent != null || mp.SiegeEvent != null || PlayerEncounter.Current != null) return "bitwa / spotkanie";
            if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return "na morzu";
            if (Mission.Current != null || CampWalk.Active) return "inna scena";
            return null;
        }
    }

    /// <summary>474-P7: logika sceny spaceru po wiosce pobocznej - druzyna gracza, gracz na punkcie gracza sceny wsi, chlopi kultury okregu na punktach npc_common
    /// (stoja; bez AI i zachowan osady). Bez ticku pracy (poza zegarem testu).</summary>
    internal sealed class SideVillageWalkLogic : MissionLogic
    {
        private static string _sUid, _sName;
        private static CultureObject _sCult;
        private static double _sHome;
        private static bool _sTest;

        private readonly Random _rng = new Random();
        private string _uid, _name;
        private CultureObject _cult;
        private double _home;
        private float _t;
        private int _folk, _points;
        private string _spawnAt = "-";
        private System.Diagnostics.Stopwatch _testClock;

        internal static void Setup(string uid, string name, CultureObject cult, double home, bool test)
        {
            _sUid = uid; _sName = name; _sCult = cult; _sHome = home; _sTest = test;
        }

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        public override void AfterStart()
        {
            base.AfterStart();
            _uid = _sUid; _name = _sName; _cult = _sCult; _home = _sHome;
            bool test = _sTest;
            _sUid = null; _sName = null; _sCult = null; _sTest = false;
            if (test) _testClock = System.Diagnostics.Stopwatch.StartNew();   // zegar wyjscia od razu - wywrotka w Build tez zamyka test po 8 s
            try { Build(); }
            catch (Exception e) { SideVillageVisit.WalkStumble("Build", e); }
        }

        public override void OnMissionTick(float dt)
        {
            _t += dt;
            if (_testClock != null && _testClock.ElapsedMilliseconds >= 8000)
            {
                _testClock = null;
                try { Mission.EndMission(); } catch (Exception e) { SideVillageVisit.WalkStumble("TestEnd", e); }
            }
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
                Log.Info(SideVillageVisit.WP + "wyjscie z " + (_name ?? "?") + " (" + (_uid ?? "-") + ") po " + _t.ToString("0", CultureInfo.InvariantCulture)
                         + " s | potkniecia razem " + SideVillageVisit.WalkStumbles + ".");
            }
            catch { }
            _testClock = null;
            SideVillageVisit.WalkActive = false;
            base.OnEndMission();
        }

        private void Build()
        {
            var m = Mission;
            var hero = Hero.MainHero;
            if (m == null || m.Scene == null || hero == null) return;
            int st0 = SideVillageVisit.WalkStumbles;
            var color = hero.MapFaction != null ? hero.MapFaction.Color : uint.MaxValue;
            var color2 = hero.MapFaction != null ? hero.MapFaction.Color2 : uint.MaxValue;
            var banner = Clan.PlayerClan != null ? Clan.PlayerClan.Banner : null;
            var team = m.Teams.Add(BattleSideEnum.Defender, color, color2, banner, true, false, true);
            m.PlayerTeam = team;

            // punkty mieszkancow sceny (te same, na ktorych gra stawia ludzi wsi), w granicach spaceru
            var pts = new List<MatrixFrame>();
            foreach (string tag in new[] { "npc_common", "npc_common_limited" })
            {
                try
                {
                    foreach (var e in m.Scene.FindEntitiesWithTag(tag))
                    {
                        if (e == null) continue;
                        var f = e.GetGlobalFrame();
                        if (m.IsPositionInsideBoundaries(f.origin.AsVec2)) pts.Add(f);
                    }
                }
                catch (Exception ex) { SideVillageVisit.WalkStumble("Points(" + tag + ")", ex); }
            }
            _points = pts.Count;

            // gracz
            MatrixFrame pf = MatrixFrame.Identity;
            bool found = false;
            foreach (string tag in new[] { "spawnpoint_player", "sp_player_conversation", "spawnpoint_player_outside" })
            {
                try
                {
                    var e = m.Scene.FindEntityWithTag(tag);
                    if (e != null) { pf = e.GetGlobalFrame(); _spawnAt = tag; found = true; break; }
                }
                catch (Exception ex) { SideVillageVisit.WalkStumble("PlayerSpot(" + tag + ")", ex); }
            }
            if (!found && pts.Count > 0) { pf = pts[0]; _spawnAt = "npc_common"; found = true; }
            if (!found)
            {
                var c = Centre(m);
                float h = 0f;
                try { h = m.Scene.GetTerrainHeight(c, true); } catch { }
                pf = MatrixFrame.Identity;
                pf.origin = new Vec3(c.x, c.y, h);
                _spawnAt = "srodek granic";
            }
            SpawnPlayer(m, team, hero, pf);

            // chlopi kultury okregu: 1 na 12 ludzi w domu (od 4 do WalkMaxFolk), noca trzecia czesc; nie wiecej niz punktow
            var kinds = Folk(_cult);
            int want = (int)Math.Round(_home / 12.0);
            want = Math.Max(4, Math.Min(SideVillageVisit.WalkMaxFolk, want));
            if (CampaignTime.Now.IsNightTime) want = Math.Max(2, want / 3);
            // przetasowanie punktow (rozne miejsca przy kazdym wejsciu)
            for (int i = pts.Count - 1; i > 0; i--) { int j = _rng.Next(i + 1); var tmp = pts[i]; pts[i] = pts[j]; pts[j] = tmp; }
            Vec2 me = pf.origin.AsVec2;
            int pi = 0;
            for (int n = 0; n < want && kinds.Count > 0 && pi < pts.Count; pi++)
            {
                var f = pts[pi];
                if (f.origin.AsVec2.Distance(me) < 2.5f) continue;   // nie na glowie gracza
                var ch = PickKind(kinds);
                if (SpawnVillager(m, team, ch, f)) { _folk++; n++; }
            }
            Log.Info(SideVillageVisit.WP + "wejscie - " + (_name ?? "?") + " (" + (_uid ?? "-") + ") | gracz na " + _spawnAt + " | chlopi " + _folk
                     + " (punktow npc_common " + _points + ", rodzajow postaci kultury " + (_cult != null ? _cult.StringId : "-") + ": " + kinds.Count + ")"
                     + " | potkniecia budowy " + (SideVillageVisit.WalkStumbles - st0) + ".");
        }

        /// <summary>Postaci wsi kultury z wagami: mezczyzni 4, kobiety 4, mlodziez 1+1, dzieci 1+1 (brakujace pominiete).</summary>
        private static List<KeyValuePair<CharacterObject, int>> Folk(CultureObject c)
        {
            var l = new List<KeyValuePair<CharacterObject, int>>();
            if (c == null) return l;
            Action<CharacterObject, int> add = delegate (CharacterObject ch, int w) { if (ch != null) l.Add(new KeyValuePair<CharacterObject, int>(ch, w)); };
            try
            {
                add(c.Villager, 4); add(c.VillageWoman, 4);
                add(c.VillagerMaleTeenager, 1); add(c.VillagerFemaleTeenager, 1);
                add(c.VillagerMaleChild, 1); add(c.VillagerFemaleChild, 1);
            }
            catch (Exception e) { SideVillageVisit.WalkStumble("Folk", e); }
            return l;
        }

        private CharacterObject PickKind(List<KeyValuePair<CharacterObject, int>> kinds)
        {
            int sum = 0;
            foreach (var kv in kinds) sum += kv.Value;
            int r = _rng.Next(Math.Max(1, sum));
            foreach (var kv in kinds) { if (r < kv.Value) return kv.Key; r -= kv.Value; }
            return kinds[0].Key;
        }

        private static Vec2 Centre(Mission m)
        {
            try
            {
                float sx = 0f, sy = 0f; int n = 0;
                foreach (var kv in m.Boundaries)
                {
                    if (kv.Value == null) continue;
                    foreach (var p in kv.Value) { sx += p.x; sy += p.y; n++; }
                }
                if (n > 0) return new Vec2(sx / n, sy / n);
            }
            catch (Exception e) { SideVillageVisit.WalkStumble("Centre(granice)", e); }
            try
            {
                Vec3 min, max;
                m.Scene.GetSceneLimits(out min, out max);
                return new Vec2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
            }
            catch (Exception e) { SideVillageVisit.WalkStumble("Centre(scena)", e); }
            return new Vec2(0f, 0f);
        }

        private static void SpawnPlayer(Mission m, Team team, Hero hero, MatrixFrame f)
        {
            try
            {
                var ch = hero.CharacterObject;
                var pos = f.origin;
                var dir = f.rotation.f.AsVec2;
                if (dir.LengthSquared < 0.0001f) dir = new Vec2(0f, 1f);
                dir.Normalize();
                var abd = new AgentBuildData(ch).Team(team).InitialPosition(in pos).InitialDirection(in dir)
                    .Controller(AgentControllerType.Player).NoHorses(true).CivilianEquipment(true)
                    .Equipment(hero.CivilianEquipment)
                    .TroopOrigin(new SimpleAgentOrigin(ch, -1, Clan.PlayerClan != null ? Clan.PlayerClan.Banner : null, default(UniqueTroopDescriptor)))
                    .ClothingColor1(team.Color).ClothingColor2(team.Color2);
                var a = m.SpawnAgent(abd, false);
                if (a != null && m.MainAgent == null) m.MainAgent = a;
            }
            catch (Exception e) { SideVillageVisit.WalkStumble("SpawnPlayer", e); }
        }

        private bool SpawnVillager(Mission m, Team team, CharacterObject ch, MatrixFrame f)
        {
            try
            {
                var pos = f.origin;
                var dir = f.rotation.f.AsVec2;
                if (dir.LengthSquared < 0.0001f) dir = new Vec2((float)_rng.NextDouble() - 0.5f, (float)_rng.NextDouble() - 0.5f);
                if (dir.LengthSquared < 0.0001f) dir = new Vec2(0f, 1f);
                dir.Normalize();
                var abd = new AgentBuildData(ch).Team(team).InitialPosition(in pos).InitialDirection(in dir)
                    .Controller(AgentControllerType.None).NoHorses(true).CivilianEquipment(true).NoWeapons(true)
                    .TroopOrigin(new SimpleAgentOrigin(ch, -1, null, default(UniqueTroopDescriptor)));
                if (_cult != null) abd.ClothingColor1(_cult.Color).ClothingColor2(_cult.Color2);   // barwy kultury wsi, nie druzyny gracza
                return m.SpawnAgent(abd, false) != null;
            }
            catch (Exception e) { SideVillageVisit.WalkStumble("SpawnVillager", e); return false; }
        }
    }
}
