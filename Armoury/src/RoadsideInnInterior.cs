using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using SandBox;
using SandBox.Conversation.MissionLogics;
using SandBox.Missions.MissionLogics;
using SandBox.Objects.AnimationPoints;
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
    /// 474-P8 (Jeff 13.11 "Wioski, i karczmy rob wszystko ... 4. Enter the Inn", projekt docs/PROJEKT-WSIE-POBOCZNE-KARCZMY-2026-11-13.md pkt 2, paczka 8):
    /// WNETRZE ZAJAZDU PRZY SZLAKU 3D. Menu zajazdu "arm_roadinn" zostaje krotkie (nocleg, plotki, "Ride on"); dochodzi pierwsza opcja "Enter the Inn" - wlasna misja
    /// typu "Camp" (ten sam typ co spacer po obozie i po wiosce pobocznej, 474-P7) na scenie KARCZMY NAJBLIZSZEGO MIASTA TEJ SAMEJ KULTURY co wies okregu zajazdu
    /// (sceny gry; sceny ROT_* tylko gdy kultura nie ma innych - trzy z nich nie maja punktu gracza, Castle Black to sala obroncow). Wybor raz na zajazd na kampanie.
    /// DOWOD (dekompilacja 1.4.8): karczma miasta = SandBoxMissions.OpenIndoorMission(location.GetSceneName(poziom), GetCivilianUpgradeLevelTag(poziom)); karczmarz,
    /// kelnerka i grajkowie to LocationCharacter na maszynach z tagami "spawnpoint_tavernkeeper" / "sp_tavern_wench" / "musician" (TavernEmployeesCampaignBehavior),
    /// zestawy animacji "_tavern_keeper" / "_barmaid" / "_musician" ustawiane po spawnie (MissionAgentHandler.SpawnWanderingAgentWithInitialFrame: SetActionSet).
    /// Maszyny w scenie zbiera MissionAgentHandler.GetAllProps po tagach encji UsableMachine; punkty to AnimationPoint (animacja petli po Agent.UseGameObject -
    /// stan NotUsing -> StartToUse -> Using, skrypt pozycji tylko dla AI). Logik osady (MissionAgentHandler, MissionLocationLogic) NIE bierzemy - zajazd nie jest osada.
    /// Rozmowa: MissionConversationLogic (bez postaci do teleportu) + widok rozmowy SandBoxViewCreator.CreateMissionConversationView (zestaw "Camp" ma kamere rozmowy,
    /// nie ma widoku). ConversationManager.GetSentenceOptions bierze PIERWSZA pasujaca linie NPC wedlug priorytetu - nasze linie startu maja priorytet 2 000 000 000
    /// i warunek "agent tej misji", wiec warunki linii gry (karczmarz gry czyta Settlement.CurrentSettlement.OwnerClan bez null - NRE poza osada) nie sa nawet liczone.
    /// Ludzie: barman (Tavernkeeper kultury, AI na swoim punkcie), kelnerka (TavernWench), grajkowie (Musician, stoja przy punktach "musician" - bez MusicianGroup gry),
    /// bywalcy (Villager / VillageWoman / Townsman / Townswoman) - siedza przy stolach (474-P9) albo stoja na npc_common. SimpleAgentOrigin, druzyna Team.Invalid jak
    /// mieszkancy osad gry (bez formacji) - nikogo nie ubywa, nic sie nie liczy. Gwar i muzyka: scena empire / vlandia / nord ma wlasne "area/interior/tavern" (skan
    /// scene.xscene 13.11), pozostale dostaja to zdarzenie od nas; muzyka "detail/tavern_track_01" (zadna scena jej nie ma) - obie 2D, zwolnione przy wyjsciu.
    /// Barman: "Pour me an ale. What's the news?" = TEN SAM kufel co opcja menu zajazdu (Rumours.RoadAle -> DrinkConsequence: cena, zaplata do kiesy wsi okregu,
    /// pijanstwo, plotki z puli zajazdu i ta sama ksiega uslyszanych - klucz uid zajazdu) po zamknieciu rozmowy; "Have you beds for the night?" - ceny jak w menu,
    /// nocleg wybiera sie w menu po wyjsciu (sen po misji - SwitchToMenu z misji nie robimy). Kwestie z pliku ModuleData\arm_tavern_lines.tsv (474-P10, teksty
    /// bez nagran), brak pliku - po jednej wbudowanej.
    /// Czas kampanii stoi (misja nad mapa), wyjscie Tab - powrot do menu zajazdu. Bez DTE (misja cywilna). Klucz RoadsideInnInterior (MCM "Roadside Inn Interior");
    /// NIE = brak opcji, menu zajazdu co do opcji jak przed. Test autotestu: plik CrashScribe\armoury-zajazd-wnetrze.flag (2 wejscia, 8 s).
    /// Linie logu "Wnetrze zajazdu (474-P8): ..." - start sesji, otwieranie (scena, miasto wzorcowe), wejscie (ludzie, punkty, siedzacy), rozmowy, wyjscie, test.
    /// </summary>
    internal static class RoadsideInnInterior
    {
        internal const string P = "Wnetrze zajazdu (474-P8): ";
        private const string TestFlag = "armoury-zajazd-wnetrze.flag";
        internal const int TopPriority = 2000000000;
        internal static bool Active;
        internal static bool On { get { var s = Settings.Current; return s != null && s.RoadsideInnInterior && RoadsideInns.On; } }
        private static int _opened, _st, _dTalk, _dAle, _dBeds;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);
        // scena karczmy na zajazd (raz na kampanie): uid -> (scena, miasto)
        private static readonly Dictionary<string, KeyValuePair<string, Settlement>> _scenes = new Dictionary<string, KeyValuePair<string, Settlement>>(StringComparer.Ordinal);
        private static object _scenesFor;
        // test autotestu: 0 - nie sprawdzono, 1 - czeka, 2 - koniec
        private static int _tState, _tLeft = 2, _tWait;
        private static bool _tSeenTown;
        private static long _tNextMs;

        internal static void Stumble(string where, Exception e)
        {
            _st++;
            bool first; lock (_err) first = _err.Add(where);
            if (first || _st % 200 == 0) { try { Log.Error("RoadsideInnInterior." + where + " (potkniecie " + _st + ")", e); } catch { } }
        }

        internal static int Stumbles { get { return _st; } }

        // ------------------------------------------------------------ menu
        /// <summary>ArmouryBehavior.OnSessionLaunched PO RoadsideInns.AddMenus (jedna linia): "Enter the Inn" na poczatku menu zajazdu (indeks 0 wstawiany po opcjach
        /// z indeksem - nocleg 0-2, plotki 5, "Ride on" 9 - wiec pozostale w tej samej kolejnosci).</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                Active = false;
                starter.AddGameMenuOption(RoadsideInns.Menu, "arm_roadinn_enter", "{=!}Enter the Inn", Condition,
                    delegate (MenuCallbackArgs a) { Open(RoadsideInns.CurrentOrNear(), false); }, false, 0);
                Log.Info(P + "opcja \"Enter the Inn\" w menu zajazdu (" + RoadsideInns.Menu + ") | klucz Roadside Inn Interior " + (On ? "TAK" : "NIE") + ".");
            }
            catch (Exception e) { Stumble("AddMenus", e); }
        }

        private static bool Condition(MenuCallbackArgs a)
        {
            if (!On) return false;
            try
            {
                a.optionLeaveType = GameMenuOption.LeaveType.Mission;
                var inn = RoadsideInns.CurrentOrNear();
                if (inn == null) return false;
                string why = Block(inn);
                if (why != null) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true; }
                a.Tooltip = new TextObject("{=!}Step inside " + inn.Name + ": the keeper at his counter, a serving girl, players in the corner and travellers at the tables. Buy an ale for the news, ask after a bed, or sit down with the company. Time stands still while you are inside; hold Tab to step out.");
                return true;
            }
            catch (Exception e) { Stumble("Condition", e); return false; }
        }

        /// <summary>null = mozna wejsc; inaczej powod po angielsku. Wojna / rabunek - te same warunki co kufel i nocleg (TownInns.RoadDrinkBarred).</summary>
        private static string Block(RoadsideInns.Inn inn)
        {
            var mp = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
            if (Campaign.Current == null || mp == null || Hero.MainHero == null) return "No campaign is running.";
            if (mp.MapEvent != null || mp.SiegeEvent != null || PlayerEncounter.Current != null) return "Not while in a battle or siege.";
            if (mp.CurrentSettlement != null) return "Leave the settlement first.";
            if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return "Only on land.";
            if (Mission.Current != null || Active || CampWalk.Active || SideVillageVisit.WalkActive) return "Already in a scene.";
            if (!Hero.MainHero.IsAlive || Hero.MainHero.IsPrisoner) return "Not now.";
            string barred = TownInns.RoadDrinkBarred(inn);
            if (barred != null) return barred;
            return null;
        }

        private static void Open(RoadsideInns.Inn inn, bool test)
        {
            try
            {
                if (!On || inn == null) return;
                if (!test)
                {
                    string why = Block(inn);
                    if (why != null) { Log.Player(why, true); return; }
                }
                Settlement from; int level;
                string scene = SceneFor(inn, out from, out level);
                if (string.IsNullOrEmpty(scene)) { Log.Player("The inn is shut.", true); Log.Info(P + "brak sceny karczmy dla " + inn.Name + " (" + inn.Uid + ", kultura " + CultOf(inn) + ")."); return; }
                Active = true;
                _opened++;
                InnInteriorLogic.Setup(inn, scene, from, test);
                string levels = "";
                try { levels = Campaign.Current.Models.LocationModel.GetCivilianUpgradeLevelTag(level); } catch (Exception e) { Stumble("Levels", e); }
                var rec = SandBoxMissions.CreateSandBoxMissionInitializerRecord(scene, levels, false, DecalAtlasGroup.Town);
                MissionState.OpenNew("Camp", rec, delegate (Mission m)
                {
                    try { m.DoesMissionRequireCivilianEquipment = true; } catch (Exception e) { Stumble("Civilian", e); }
                    return new MissionBehavior[]
                    {
                        new MissionOptionsComponent(),
                        new CampaignMissionComponent(),
                        new BasicLeaveMissionLogic(),
                        new MissionConversationLogic(),
                        new MissionBoundaryPlacer(),
                        new MissionBoundaryCrossingHandler(10f),
                        new EquipmentControllerLeaveLogic(),
                        new InnInteriorLogic(),
                        SandBox.View.SandBoxViewCreator.CreateMissionConversationView(m),
                        ViewCreator.CreateMissionLeaveView()
                    };
                }, true, true);
                Log.Info(P + "otwieranie - " + inn.Name + " (" + inn.Uid + ", okreg " + inn.VillageId + ", kultura " + CultOf(inn) + ", jakosci " + inn.Tiers + ") | scena " + scene
                         + " (karczma miasta " + (from != null ? from.StringId : "-") + ", poziom " + level + " '" + levels + "') | godzina " + CampaignTime.Now.GetHourOfDay
                         + (CampaignTime.Now.IsNightTime ? " (noc)" : " (dzien)") + (test ? " | TEST" : "") + " | wejscie nr " + _opened + " w tej sesji.");
            }
            catch (Exception e)
            {
                Active = false;
                Stumble("Open", e);
                Log.Player("The inn cannot be entered right now.", true);
            }
        }

        private static string CultOf(RoadsideInns.Inn inn)
        {
            try { return inn != null && inn.Village != null && inn.Village.Culture != null ? inn.Village.Culture.StringId : "-"; } catch { return "-"; }
        }

        /// <summary>Scena karczmy: najblizsze miasto kultury wsi okregu ze scena gry; bez niej - scena ROT tej kultury (bez Castle Black); bez niej - najblizsze miasto
        /// dowolnej kultury ze scena gry. Raz na zajazd na kampanie (te same drzwi - ta sama sala).</summary>
        private static string SceneFor(RoadsideInns.Inn inn, out Settlement from, out int level)
        {
            level = Math.Max(1, Math.Min(3, inn.Tiers));
            if (!ReferenceEquals(_scenesFor, Campaign.Current)) { _scenes.Clear(); _scenesFor = Campaign.Current; }
            KeyValuePair<string, Settlement> kv;
            if (_scenes.TryGetValue(inn.Uid, out kv)) { from = kv.Value; return kv.Key; }
            string cult = CultOf(inn);
            var at = new Vec2(inn.X, inn.Y);
            string best = null; Settlement bestT = null; float bestD = float.MaxValue;
            for (int pass = 0; pass < 3 && best == null; pass++)
            {
                foreach (var s in Settlement.All)
                {
                    if (s == null || !s.IsTown || s.Culture == null) continue;
                    bool same = s.Culture.StringId == cult;
                    if (pass < 2 && !same) continue;
                    string sc = TavernOf(s, level);
                    if (string.IsNullOrEmpty(sc)) continue;
                    bool rot = sc.StartsWith("ROT_", StringComparison.OrdinalIgnoreCase);
                    if (rot && (pass != 1 || sc.IndexOf("castle_black", StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    float d = s.GetPosition2D.DistanceSquared(at);
                    if (d < bestD) { bestD = d; best = sc; bestT = s; }
                }
            }
            _scenes[inn.Uid] = new KeyValuePair<string, Settlement>(best, bestT);
            from = bestT;
            return best;
        }

        private static string TavernOf(Settlement s, int level)
        {
            try
            {
                var lc = s.LocationComplex;
                Location loc = lc != null ? lc.GetLocationWithId("tavern") : null;
                return loc != null ? loc.GetSceneName(level) : null;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------ rozmowy (barman, kelnerka, grajkowie, reszta; bywalcy - InnTable 474-P9)
        /// <summary>ArmouryBehavior.OnSessionLaunched (jedna linia). Linie startu z priorytetem TopPriority i warunkiem "agent misji zajazdu" - poza misja nie przechodza.</summary>
        internal static void AddDialogs(CampaignGameStarter starter)
        {
            try
            {
                if (starter == null) return;
                starter.AddDialogLine("arm474_inn_keep", "start", "arm474_inn_bar", "{=!}{ARM474_INN_HELLO}", KeeperStart, null, TopPriority);
                starter.AddPlayerLine("arm474_inn_ale", "arm474_inn_bar", "arm474_inn_pour", "{=!}Pour me an ale. What's the news?", AleCond, null, 110, AleClick);
                starter.AddDialogLine("arm474_inn_pour", "arm474_inn_pour", "close_window", "{=!}{ARM474_INN_POUR}", PourCond, Pour, 110);
                starter.AddPlayerLine("arm474_inn_beds", "arm474_inn_bar", "arm474_inn_beds_a", "{=!}Have you beds for the night?", null, null, 109);
                starter.AddDialogLine("arm474_inn_beds_a", "arm474_inn_beds_a", "arm474_inn_bar", "{=!}{ARM474_INN_BEDS}", BedsCond, null, 110);
                starter.AddPlayerLine("arm474_inn_bye", "arm474_inn_bar", "close_window", "{=!}Nothing for now.", null, null, 100);
                starter.AddDialogLine("arm474_inn_staff", "start", "close_window", "{=!}{ARM474_INN_STAFF}", StaffStart, null, TopPriority - 1);
                InnTable.AddDialogs(starter);   // 474-P9: bywalcy przy stolach (wlasny try; linia startu TopPriority - 2, warunek: klucz InnTableTalk i agent przy stole)
                starter.AddDialogLine("arm474_inn_any", "start", "close_window", "{=!}{ARM474_INN_ANY}", AnyStart, null, TopPriority - 3);
                Log.Info(P + "rozmowy zajazdu dodane (barman: kufel i lozka, kelnerka, grajkowie, reszta; priorytet startu " + TopPriority + ").");
            }
            catch (Exception e) { Stumble("AddDialogs", e); }
        }

        private static Agent TalkAgent()
        {
            var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
            return cm != null ? cm.OneToOneConversationAgent as Agent : null;
        }

        private static bool KeeperStart()
        {
            try
            {
                var l = InnInteriorLogic.Current;
                if (l == null || l.RoleOf(TalkAgent()) != InnInteriorLogic.RoleKeeper) return false;
                _dTalk++;
                MBTextManager.SetTextVariable("ARM474_INN_HELLO", TavernLines.Pick("greet", l.Region, "Welcome to " + l.InnName + ". What'll it be?").Replace("{INN}", l.InnName));
                return true;
            }
            catch (Exception e) { Stumble("KeeperStart", e); return false; }
        }

        private static bool StaffStart()
        {
            try
            {
                var l = InnInteriorLogic.Current;
                if (l == null) return false;
                int r = l.RoleOf(TalkAgent());
                string kind = r == InnInteriorLogic.RoleWench ? "wench" : r == InnInteriorLogic.RoleMusician ? "music" : null;
                if (kind == null) return false;
                _dTalk++;
                string def = kind == "wench" ? "Ale's at the counter, love. Ask the keeper." : "We play for the room. A coin in the cup keeps the strings in tune.";
                MBTextManager.SetTextVariable("ARM474_INN_STAFF", TavernLines.Pick(kind, l.Region, def).Replace("{INN}", l.InnName));
                return true;
            }
            catch (Exception e) { Stumble("StaffStart", e); return false; }
        }

        private static bool AnyStart()
        {
            try
            {
                var l = InnInteriorLogic.Current;
                if (l == null || TalkAgent() == null) return false;
                MBTextManager.SetTextVariable("ARM474_INN_ANY", "Not now, friend.");
                return true;
            }
            catch (Exception e) { Stumble("AnyStart", e); return false; }
        }

        private static bool AleCond()
        {
            var l = InnInteriorLogic.Current;
            return l != null && l.Inn != null && Rumours.On;
        }

        private static bool AleClick(out TextObject why)
        {
            try
            {
                var l = InnInteriorLogic.Current;
                string tip;
                bool ok = Rumours.RoadAleState(l != null ? l.Inn : null, out tip);
                why = new TextObject("{=!}" + (tip ?? ""));
                return ok;
            }
            catch (Exception e) { Stumble("AleClick", e); why = new TextObject("{=!}Not now."); return false; }
        }

        private static bool PourCond()
        {
            MBTextManager.SetTextVariable("ARM474_INN_POUR", "There you are. Drink up, and I'll tell you what the road has been saying.");
            return true;
        }

        private static void Pour()
        {
            try
            {
                var l = InnInteriorLogic.Current;
                var inn = l != null ? l.Inn : null;
                var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                if (inn == null || cm == null) return;
                string uid = inn.Uid;
                cm.ConversationEndOneShot += delegate
                {
                    try
                    {
                        var now = InnInteriorLogic.Current;
                        if (now == null || now.Inn == null || now.Inn.Uid != uid) { Log.Info(P + "kufel w " + uid + " pominiety - misja zajazdu juz zamknieta."); return; }
                        _dAle++;
                        Log.Info(P + "\"Pour me an ale\" u barmana " + now.Inn.Name + " (" + uid + ") - kufel jak z menu zajazdu (linia 'Kufle (m98): kufel w ...' ponizej); kufli z barmana " + _dAle + " od startu gry.");
                        Rumours.RoadAle(now.Inn);
                    }
                    catch (Exception e) { Stumble("Pour.End", e); }
                };
            }
            catch (Exception e) { Stumble("Pour", e); }
        }

        private static bool BedsCond()
        {
            try
            {
                var l = InnInteriorLogic.Current;
                var inn = l != null ? l.Inn : null;
                if (inn == null) return false;
                _dBeds++;
                string t = RoadsideInns.BedsText(inn) + " A man and his horse pay 1 and 1 in the common room, your own room " + TownInns.RoomOf(0) + "."
                           + (inn.Tiers >= 2 ? " Decent beds 2 and 2, a room 8." : "") + (inn.Tiers >= 3 ? " The good rooms 4 and 3, the best 20." : "")
                           + " Settle it with me on your way out and I'll have them made up.";
                MBTextManager.SetTextVariable("ARM474_INN_BEDS", t);
                return true;
            }
            catch (Exception e) { Stumble("BedsCond", e); return false; }
        }

        // ------------------------------------------------------------ sesja i doba
        internal static void SessionStart()
        {
            try
            {
                Log.Info(P + "start sesji | klucz Roadside Inn Interior " + (On ? "TAK" : "NIE") + " | " + TavernLines.Summary() + " | potkniecia " + _st + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        internal static string DayText()
        {
            string t = "wnetrze (474-P8): wejscia " + _opened + " od startu sesji, rozmowy " + _dTalk + ", kufle u barmana " + _dAle + ", pytania o lozka " + _dBeds + ", potkniecia " + _st;
            _dTalk = _dBeds = 0;
            return t;
        }

        // ------------------------------------------------------------ test autotestu (plik CrashScribe\armoury-zajazd-wnetrze.flag)
        internal static void TestTick()
        {
            if (_tState == 2 || Campaign.Current == null) return;
            try { TestStep(); }
            catch (Exception e) { _tState = 2; Stumble("TestTick", e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TestStep()
        {
            if (_tState == 0)
            {
                bool want = false;
                int m = LogGate.Mode;
                if (m == 1 || m == 2) { try { want = File.Exists(Path.Combine(CampWalk.CrashScribeDir(), TestFlag)); } catch { } }
                if (!want || !On) { _tState = 2; return; }
                _tState = 1;
                Log.Info(P + "test - plik " + TestFlag + ": do 2 wejsc (najblizszy zajazd od partii), kazde przy wolnej chwili na mapie po wyjsciu z osady, wyjscie po 8 s.");
            }
            if (Active) return;
            string why = TestGate();
            if (why == null)
            {
                var mp = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
                RoadsideInns.Inn inn = null;
                try { RoadsideInns.EnsureLoaded(); inn = RoadsideInns.NearestAny(mp.GetPosition2D, _tLeft == 2 ? 0 : 1); } catch (Exception e) { Stumble("TestPick", e); }
                _tLeft--;
                if (_tLeft <= 0) _tState = 2;
                if (inn == null) { _tState = 2; Log.Info(P + "test - brak zajazdow - koniec testu."); return; }
                Log.Info(P + "test - otwieram wnetrze " + inn.Name + " (" + inn.Uid + "), zostalo " + _tLeft + ".");
                Open(inn, true);
                if (!Active) Log.Info(P + "test - wnetrze nie otworzylo sie (zob. ERROR in RoadsideInnInterior.Open).");
                _tWait = 0;
                return;
            }
            long now = (long)(System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (_tWait < 5 && (_tWait == 0 || now >= _tNextMs))
            {
                _tWait++;
                _tNextMs = now + 120000;
                Log.Info(P + "test - czekam (" + why + ").");
            }
        }

        private static string TestGate()
        {
            var c = Campaign.Current;
            var gsm = GameStateManager.Current;
            var active = gsm != null ? gsm.ActiveState : null;
            if (!(active is TaleWorlds.CampaignSystem.GameState.MapState)) return "nie mapa (" + (active != null ? active.GetType().Name : "-") + ")";
            if (LoadingWindow.IsLoadingWindowActive) return "ekran wczytywania";
            var msc = SandBox.View.Map.MapScreen.Instance;
            if (msc != null && !msc.IsReady) return "mapa jeszcze nie gotowa";
            var mp = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
            if (mp != null && mp.CurrentSettlement != null) { _tSeenTown = true; return "w osadzie - wnetrze po wyjsciu"; }
            if (!_tSeenTown) return "jeszcze nie bylo osady - autotest otwiera wnetrze po wyjsciu z miasta";
            if (c.CurrentMenuContext != null) return "menu";
            var cm = c.ConversationManager;
            if (cm != null && (cm.IsConversationFlowActive || cm.IsConversationInProgress)) return "rozmowa";
            if (gsm.ActiveStateDisabledByUser) return "menu nad mapa";
            if (InformationManager.IsAnyInquiryActive()) return "okno (zapytanie)";
            if (mp == null || mp.MapEvent != null || mp.SiegeEvent != null || PlayerEncounter.Current != null) return "bitwa / spotkanie";
            if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return "na morzu";
            if (Mission.Current != null || CampWalk.Active || SideVillageVisit.WalkActive) return "inna scena";
            return null;
        }
    }

    /// <summary>474-P8: logika wnetrza zajazdu - gracz, barman, kelnerka, grajkowie, bywalcy (stoly - 474-P9 InnTable), gwar i muzyka. Bez ticku pracy (poza zegarem testu).</summary>
    internal sealed class InnInteriorLogic : MissionLogic
    {
        internal const int RoleKeeper = 1, RoleWench = 2, RoleMusician = 3, RolePatron = 4;
        private static readonly HashSet<string> CrowdInScene = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "empire_house_c_tavern_a", "vlandia_tavern_interior_a", "nord_tavern" };
        private const string EvCrowd = "event:/mission/ambient/area/interior/tavern", EvMusic = "event:/mission/ambient/detail/tavern_track_01";
        private static RoadsideInns.Inn _sInn; private static string _sScene; private static Settlement _sFrom; private static bool _sTest;

        internal static InnInteriorLogic Current;
        internal RoadsideInns.Inn Inn;
        internal string InnName = "the inn", Region = "any", Scene;
        internal CultureObject Cult;
        internal InnTable Tables;   // 474-P9: stoly (null przy kluczu InnTableTalk NIE)
        private Settlement _from;
        private readonly Dictionary<Agent, int> _roles = new Dictionary<Agent, int>();
        private readonly Random _rng = new Random();
        private float _t;
        private string _spawnAt = "-";
        private SoundEvent _crowd, _music;
        private System.Diagnostics.Stopwatch _testClock;

        internal static void Setup(RoadsideInns.Inn inn, string scene, Settlement from, bool test) { _sInn = inn; _sScene = scene; _sFrom = from; _sTest = test; }

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        internal int RoleOf(Agent a) { int r; return a != null && _roles.TryGetValue(a, out r) ? r : 0; }

        internal void SetRole(Agent a, int r) { if (a != null) _roles[a] = r; }

        public override void AfterStart()
        {
            base.AfterStart();
            Inn = _sInn; Scene = _sScene; _from = _sFrom;
            bool test = _sTest;
            _sInn = null; _sScene = null; _sFrom = null; _sTest = false;
            Current = this;
            if (test) _testClock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (Inn != null) InnName = Inn.Name;
                Cult = Inn != null && Inn.Village != null ? Inn.Village.Culture : null;
                Region = TavernLines.RegionOf(Cult != null ? Cult.StringId : null);
            }
            catch (Exception e) { RoadsideInnInterior.Stumble("Setup", e); }
            try { Build(); }
            catch (Exception e) { RoadsideInnInterior.Stumble("Build", e); }
        }

        public override void OnMissionTick(float dt)
        {
            _t += dt;
            if (_testClock != null && _testClock.ElapsedMilliseconds >= 8000)
            {
                _testClock = null;
                try { Mission.EndMission(); } catch (Exception e) { RoadsideInnInterior.Stumble("TestEnd", e); }
            }
        }

        public override InquiryData OnEndMissionRequest(out bool canPlayerLeave)
        {
            canPlayerLeave = true;
            return null;
        }

        protected override void OnEndMission()
        {
            FreeSound(ref _crowd);
            FreeSound(ref _music);
            try
            {
                Log.Info(RoadsideInnInterior.P + "wyjscie z " + InnName + " (" + (Inn != null ? Inn.Uid : "-") + ") po " + _t.ToString("0", CultureInfo.InvariantCulture)
                         + " s | " + (Tables != null ? Tables.ExitText() + " | " : "") + "potkniecia razem " + RoadsideInnInterior.Stumbles + ".");
            }
            catch { }
            _testClock = null;
            Current = null;
            RoadsideInnInterior.Active = false;
            base.OnEndMission();
        }

        private static void FreeSound(ref SoundEvent ev)
        {
            if (ev == null) return;
            try
            {
                int id = ev.GetSoundId();
                ev.Stop();
                BattleSound.ReleaseById(id);
            }
            catch (Exception e) { RoadsideInnInterior.Stumble("FreeSound", e); }
            ev = null;
        }

        private void Build()
        {
            var m = Mission;
            var hero = Hero.MainHero;
            if (m == null || m.Scene == null || hero == null) return;
            int st0 = RoadsideInnInterior.Stumbles;
            var team = m.Teams.Add(BattleSideEnum.Defender, hero.MapFaction != null ? hero.MapFaction.Color : uint.MaxValue, hero.MapFaction != null ? hero.MapFaction.Color2 : uint.MaxValue,
                                   Clan.PlayerClan != null ? Clan.PlayerClan.Banner : null, true, false, true);
            m.PlayerTeam = team;
            var machines = Machines(m);

            // gracz
            MatrixFrame pf = MatrixFrame.Identity;
            bool found = false;
            foreach (string tag in new[] { "spawnpoint_player", "sp_player", "sp_player_conversation", "spawnpoint_player_outside", "npc_common" })
            {
                try
                {
                    var e = m.Scene.FindEntityWithTag(tag);
                    if (e != null) { pf = e.GetGlobalFrame(); _spawnAt = tag; found = true; break; }
                }
                catch (Exception ex) { RoadsideInnInterior.Stumble("PlayerSpot(" + tag + ")", ex); }
            }
            if (!found)
            {
                try
                {
                    Vec3 min, max;
                    m.Scene.GetSceneLimits(out min, out max);
                    var c = new Vec2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
                    pf.origin = new Vec3(c.x, c.y, m.Scene.GetGroundHeightAtPosition(new Vec3(c.x, c.y, max.z)));
                }
                catch (Exception ex) { RoadsideInnInterior.Stumble("PlayerSpot(srodek)", ex); }
                _spawnAt = "srodek sceny";
            }
            SpawnPlayer(m, team, hero, pf);

            // obsluga: barman, kelnerka, grajkowie (kultura wsi okregu, zapas - kultura miasta sceny)
            var cultFrom = _from != null ? _from.Culture : null;
            int keep = 0, wench = 0, mus = 0, musPts = 0;
            UsableMachine um;
            if (FirstFree(machines, "spawnpoint_tavernkeeper", out um) && Spawn(m, Pick(c => c.Tavernkeeper, cultFrom), um, "_tavern_keeper", true, RoleKeeper) != null) keep++;
            if (FirstFree(machines, "sp_tavern_wench", out um) && Spawn(m, Pick(c => c.TavernWench, cultFrom), um, "_barmaid", true, RoleWench) != null) wench++;
            List<UsableMachine> ml;
            if (machines.TryGetValue("musician", out ml))
            {
                // grupa grajkow gry (MusicianGroup) ma kilka punktow - po jednym grajku na punkt, bez UseGameObject (granie wymaga MusicianGroup i instrumentu)
                foreach (var g in ml)
                {
                    if (g == null || g.StandingPoints == null) continue;
                    foreach (var sp in g.StandingPoints)
                    {
                        if (sp == null || sp.IsDeactivated) continue;
                        musPts++;
                        if (mus >= 3) continue;
                        Agent a = null;
                        try { a = SpawnAt(m, Pick(c => c.Musician, cultFrom), sp.GameEntity.GetGlobalFrame(), "_musician", false, RoleMusician); }
                        catch (Exception e) { RoadsideInnInterior.Stumble("Musician", e); }
                        if (a != null) mus++;
                    }
                }
            }

            // bywalcy: przy stolach (474-P9, klucz InnTableTalk), reszta na npc_common
            int want = Patrons();
            int seated = 0, standing = 0;
            if (InnTable.On)
            {
                Tables = new InnTable(this);
                try { seated = Tables.Seat(m, want, _rng); }
                catch (Exception e) { RoadsideInnInterior.Stumble("Seat", e); }
            }
            if (seated < want)
            {
                var pts = new List<MatrixFrame>();
                try { foreach (var e in m.Scene.FindEntitiesWithTag("npc_common")) if (e != null) pts.Add(e.GetGlobalFrame()); }
                catch (Exception e) { RoadsideInnInterior.Stumble("Points", e); }
                for (int i = pts.Count - 1; i > 0; i--) { int j = _rng.Next(i + 1); var tmp = pts[i]; pts[i] = pts[j]; pts[j] = tmp; }
                Vec2 me = pf.origin.AsVec2;
                for (int i = 0; i < pts.Count && seated + standing < want; i++)
                {
                    if (pts[i].origin.AsVec2.Distance(me) < 2f) continue;
                    if (SpawnAt(m, PatronKind(), pts[i], "_villager", false, RolePatron) != null) standing++;
                }
            }

            // gwar (gdy scena nie ma wlasnego) i muzyka
            bool crowdOwn = Scene != null && CrowdInScene.Contains(Scene);
            if (!crowdOwn) _crowd = PlaySound(m, EvCrowd);
            _music = PlaySound(m, EvMusic);

            Log.Info(RoadsideInnInterior.P + "wejscie - " + InnName + " (" + (Inn != null ? Inn.Uid : "-") + ", region tekstow " + Region + ") | scena " + Scene + " | gracz na " + _spawnAt
                     + " | barman " + keep + ", kelnerka " + wench + ", grajkowie " + mus + " (punktow " + musPts + ") | bywalcy chcianych " + want + ": przy stolach " + seated
                     + ", stojacych " + standing + (Tables != null ? " | " + Tables.BuildText() : "")
                     + " | gwar " + (crowdOwn ? "ze sceny" : (_crowd != null ? "nasz" : "BRAK")) + ", muzyka " + (_music != null ? "tak" : "BRAK")
                     + " | potkniecia budowy " + (RoadsideInnInterior.Stumbles - st0) + ".");
        }

        /// <summary>Bywalcy: od ludzi wokol zajazdu (4 + 1 na 5000, do 14), wieczorem (17-23) wiecej o polowe, gleboka noca (0-5) trzecia czesc.</summary>
        private int Patrons()
        {
            int people = Inn != null ? Inn.People : 0;
            int n = Math.Max(4, Math.Min(14, 4 + people / 5000));
            int h = CampaignTime.Now.GetHourOfDay;
            if (h >= 17) n = Math.Min(16, n + n / 2);
            else if (h < 6) n = Math.Max(2, n / 3);
            return n;
        }

        internal CharacterObject PatronKind()
        {
            var c = Cult ?? (_from != null ? _from.Culture : null);
            if (c == null) return null;
            int r = _rng.Next(8);
            CharacterObject ch = r < 3 ? c.Villager : r < 5 ? c.VillageWoman : r < 7 ? c.Townsman : c.Townswoman;
            return ch ?? c.Villager ?? c.Townsman;
        }

        private CharacterObject Pick(Func<CultureObject, CharacterObject> f, CultureObject alt)
        {
            CharacterObject ch = null;
            try { if (Cult != null) ch = f(Cult); } catch { }
            try { if (ch == null && alt != null) ch = f(alt); } catch { }
            return ch;
        }

        /// <summary>Maszyny sceny wedlug tagow encji (jak MissionAgentHandler.GetAllProps) - raz przy budowie.</summary>
        private static Dictionary<string, List<UsableMachine>> Machines(Mission m)
        {
            var d = new Dictionary<string, List<UsableMachine>>(StringComparer.Ordinal);
            foreach (var mo in m.MissionObjects)
            {
                var um = mo as UsableMachine;
                if (um == null) continue;
                try
                {
                    foreach (string tag in um.GameEntity.Tags)
                    {
                        List<UsableMachine> l;
                        if (!d.TryGetValue(tag, out l)) d[tag] = l = new List<UsableMachine>();
                        l.Add(um);
                    }
                }
                catch (Exception e) { RoadsideInnInterior.Stumble("Machines", e); }
            }
            return d;
        }

        private static bool FirstFree(Dictionary<string, List<UsableMachine>> d, string tag, out UsableMachine um)
        {
            um = null;
            List<UsableMachine> l;
            if (!d.TryGetValue(tag, out l)) return false;
            foreach (var x in l) if (x != null && x.StandingPoints != null && x.StandingPoints.Count > 0) { um = x; return true; }
            return false;
        }

        /// <summary>Postac na pierwszym wolnym punkcie maszyny; use - AI i Agent.UseGameObject na punkcie animacji (petla jak w karczmie gry), inaczej stoi.</summary>
        internal Agent Spawn(Mission m, CharacterObject ch, UsableMachine um, string suffix, bool use, int role)
        {
            if (ch == null || um == null) return null;
            StandingPoint sp = null;
            foreach (var x in um.StandingPoints) if (x != null && !x.HasUser && !x.IsDeactivated) { sp = x; break; }
            if (sp == null) return null;
            Agent a = null;
            try { a = SpawnAt(m, ch, sp.GameEntity.GetGlobalFrame(), suffix, use, role); }
            catch (Exception e) { RoadsideInnInterior.Stumble("Spawn(" + suffix + ")", e); }
            if (a != null && use) UseSeat(a, sp);
            return a;
        }

        internal static bool UseSeat(Agent a, StandingPoint sp)
        {
            if (a == null || !(sp is AnimationPoint)) return false;
            try { a.UseGameObject(sp); return true; }
            catch (Exception e) { RoadsideInnInterior.Stumble("UseGameObject", e); return false; }
        }

        internal Agent SpawnAt(Mission m, CharacterObject ch, MatrixFrame f, string suffix, bool ai, int role)
        {
            if (ch == null) return null;
            try
            {
                var pos = f.origin;
                try { pos.z = m.Scene.GetGroundHeightAtPosition(pos); } catch { }
                var dir = f.rotation.f.AsVec2;
                if (dir.LengthSquared < 0.0001f) dir = new Vec2(0f, 1f);
                dir.Normalize();
                Monster monster = TaleWorlds.Core.FaceGen.GetMonsterWithSuffix(ch.Race, "_settlement");
                int minAge = 20, maxAge = 50;
                try { Campaign.Current.Models.AgeModel.GetAgeLimitForLocation(ch, out minAge, out maxAge); } catch { }
                var abd = new AgentBuildData(ch).Monster(monster).Team(Team.Invalid).InitialPosition(in pos).InitialDirection(in dir)
                    .Controller(ai ? AgentControllerType.AI : AgentControllerType.None).NoHorses(true).CivilianEquipment(true).NoWeapons(true)
                    .Age(_rng.Next(Math.Min(minAge, maxAge), Math.Max(minAge, maxAge) + 1))
                    .TroopOrigin(new SimpleAgentOrigin(ch, -1, null, default(UniqueTroopDescriptor)));
                var a = m.SpawnAgent(abd, false);
                if (a == null) return null;
                try
                {
                    var asd = monster.FillAnimationSystemData(MBGlobals.GetActionSet(ActionSetCode.GenerateActionSetNameWithSuffix(monster, ch.IsFemale, suffix)), ch.GetStepSize(), false);
                    a.SetActionSet(ref asd);
                }
                catch (Exception e) { RoadsideInnInterior.Stumble("ActionSet(" + suffix + ")", e); }
                SetRole(a, role);
                return a;
            }
            catch (Exception e) { RoadsideInnInterior.Stumble("SpawnAt(" + suffix + ")", e); return null; }
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
            catch (Exception e) { RoadsideInnInterior.Stumble("SpawnPlayer", e); }
        }

        private static SoundEvent PlaySound(Mission m, string ev)
        {
            try
            {
                var s = SoundEvent.CreateEventFromString(ev, m.Scene);
                if (s == null || !s.IsValid) return null;
                s.Play();
                return s;
            }
            catch (Exception e) { RoadsideInnInterior.Stumble("Sound(" + ev + ")", e); return null; }
        }
    }
}
