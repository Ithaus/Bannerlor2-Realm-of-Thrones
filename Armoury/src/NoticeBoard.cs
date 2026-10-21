using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m102-tablica E1 (Jeff 20.10: "tablice ogloszen jak w grach RPG z misjami ... jak wchodze, mam liste lordow, kapitana strazy etc., ktorzy daja misje, i jak klikne,
    /// mam visit i rozmawiam z nim i opisuje mi misje"; 21.10 "i rob 5 i 7"; projekt PROJEKT-TABLICA-OGLOSZEN-2026-10-21 pkt 6.1) - TABLICA OGLOSZEN W MIESCIE.
    /// Opcja "Read the notice board" w menu miasta (zaraz za "Go to the tavern district", poza planem przenosin TownMenus - zostaje w menu "town"), z wykrzyknikiem gry,
    /// gdy ktos ma wolne zadanie. Lista (powtarzalna opcja menu, wzor gry "request_meeting" - EncounterGameMenuBehavior): zadania gry (IssueManager) notabli miasta,
    /// bohaterow bez partii i lordow w partiach w miescie (GetIssuesInSettlement), notabli wsi tego miasta (klucz NoticeBoardVillages) i lordow rodu pana miasta poza
    /// miastem; tylko sprawy wolne (IsOngoingWithoutQuest). Kolejnosc: w miescie, wsie, nieobecni; w grupie wyzej lepiej platne. Karta: co zrobic (tytul i opis gry),
    /// za ile (kwota po skali Armoury - getter RewardGold z postfiksem QuestPay8.RewardShown, ta sama liczba co w rozmowie), kto placi i czy go stac (QuestRewardFromGiver:
    /// kiesa zleceniodawcy), rozwiazanie zastepcze gry, termin (IssueDueTime), gdzie osoba jest. Przyciski: "Go and see" (wejscie do sceny przy osobie - dokladnie kod gry
    /// z paska portretow: GameMenuOverlay.ExecuteTroopAction/Conversation), "Talk now" (szybka rozmowa jak QuickConversation gry), "Mark on the map" (flaga
    /// VisualTrackerManager jak HideoutSpotter; nasze flagi w zapisie, zdejmowane po FlagDays). Kazdy przycisk sprawdza sprawe na swiezo ("This notice has been taken down").
    /// Lista budowana na swiezo przy kazdym otwarciu (BK sam zamyka zadania). Bez zmian w zadaniach gry, nagrodach i AI. Watek glowny (menu); wyjatek per wpis - licznik
    /// potkniec, nic nie gasimy (CLAUDE.md 7). Menu rejestrowane zawsze (zapis zrobiony w podmenu wczyta sie po wylaczeniu klucza). Linia doby "Tablica ogloszen (m102)".
    /// </summary>
    internal static partial class NoticeBoard
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const string P = "Tablica ogloszen (m102): ";
        internal const string OptEnter = "arm_board_enter", MenuBoard = "arm_board", MenuNote = "arm_board_note";
        private const string Ver = "nb1";                 // stary format zapisu (do WGRANIA 101) - tylko odczyt
        private const string Ver2 = "nb2";                // m103-drobne: format odchudzony (doby wzgledem D, tablica przedrostkow id, bez pol nieczytanych) - zapis
        private const int FlagDays = 10;                  // flaga z tablicy stoi tyle dob (potem zdjeta - stan w zapisie arm_board)
        private const int DescMax = 600;                  // opis gry na karcie przyciety do ok. 600 znakow

        internal enum Kind { Task = 0, Town = 1, Lord = 2, Village = 3, Absent = 4, Watch = 5, Headman = 6 }

        /// <summary>Wpis tablicy (zywy tylko do nastepnego otwarcia). Group: 0 zadania gracza, 1 w miescie, 2 wsie, 3 nieobecni.</summary>
        internal sealed class Entry
        {
            public Kind K;
            public Hero Hero;
            public IssueBase Issue;
            public QuestBase Quest;
            public Settlement Village;        // wies wpisu (Kind.Village, Headman)
            public object Tag;                // E2 / E2b: list gonczy
            public int Group, Reward = -1;
            public string Line = "", Where = "";
        }

        private static readonly List<Entry> _list = new List<Entry>();
        private static Entry _sel;
        private static Settlement _boardSt;
        private static bool _wired;
        private static int _wiredMenus, _wiredTown;

        /// <summary>Flaga z tablicy (h - bohater, s - osada). Obj zywy w sesji; po wczytaniu rozwiazywany po id.</summary>
        private sealed class Flag { public string Kind, Id; public int Until; public ITrackableCampaignObject Obj; }
        private static readonly List<Flag> _flags = new List<Flag>();

        // ------------------------------------------------------------ liczniki (doba / sesja)
        private static int _dOpen, _dEntries, _dVisit, _dTalk, _dFlag, _dToQuest, _dDenied, _dGone, _dRewardBad, _dTok, _dTexts;
        private static readonly int[] _dKind = new int[7];
        private static double _dOpenMs;
        private static int _stumbles, _stumblesDay;
        private static readonly Dictionary<string, int> _stSites = new Dictionary<string, int>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.NoticeBoard; } }
        private static bool VillagesOn { get { var s = Settings.Current; return s != null && s.NoticeBoardVillages; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        /// <summary>Nowa kampania i kazde wczytanie (konstruktor ArmouryBehavior - przed SyncData).</summary>
        internal static void Reset()
        {
            _list.Clear(); _sel = null; _boardSt = null; _flags.Clear(); _wired = false; _wiredMenus = _wiredTown = 0;
            _stumbles = 0; _stSites.Clear(); ZeroDay();
            ResetTalk();
            ResetWatch();
        }

        private static void ZeroDay()
        {
            _dOpen = _dEntries = _dVisit = _dTalk = _dFlag = _dToQuest = _dDenied = _dGone = _dRewardBad = _dTok = _dTexts = 0;
            for (int i = 0; i < _dKind.Length; i++) _dKind[i] = 0;
            _dOpenMs = 0; _stumblesDay = 0;
        }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            int n; _stSites.TryGetValue(where, out n); n++; _stSites[where] = n;
            if (n <= 3 || n % 200 == 0) Log.Error("NoticeBoard." + where + " (potkniecie " + n + ")", e);
        }

        // ------------------------------------------------------------ rejestracja (OnAfterSessionLaunched - wszystkie menu "town" juz stoja)

        /// <summary>ArmouryBehavior: OnAfterSessionLaunchedEvent. Menu tablicy i karty rejestrowane zawsze; opcja w menu "town" zaraz za "Go to the tavern district".</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            if (starter == null) return;
            try
            {
                starter.AddGameMenu(MenuBoard, "{=!}{ARM_BOARD_TEXT}", BoardInit, GameMenu.MenuOverlayType.SettlementWithBoth);
                starter.AddGameMenuOption(MenuBoard, "arm_board_item", "{=!}{ARM_BOARD_ITEM}", ItemCond, ItemCons, false, -1, true);
                starter.AddGameMenuOption(MenuBoard, "arm_board_back", "{=!}Back to town", BackCond, a => GameMenu.SwitchToMenu("town"), true);
                starter.AddGameMenu(MenuNote, "{=!}{ARM_NOTE_TEXT}", NoteInit, GameMenu.MenuOverlayType.SettlementWithBoth);
                starter.AddGameMenuOption(MenuNote, "arm_note_visit", "{=!}{ARM_NOTE_VISIT}", VisitCond, VisitCons);
                starter.AddGameMenuOption(MenuNote, "arm_note_talk", "{=!}{ARM_NOTE_TALK}", TalkCond, TalkCons);
                starter.AddGameMenuOption(MenuNote, "arm_note_mark", "{=!}{ARM_NOTE_MARK}", MarkCond, MarkCons);
                AddWatchOptions(starter);
                starter.AddGameMenuOption(MenuNote, "arm_note_back", "{=!}Back to the board", BackCond, a => GameMenu.SwitchToMenu(MenuBoard), true);
                _wiredMenus = 2;
                int idx = -1;
                try
                {
                    var town = Campaign.Current != null && Campaign.Current.GameMenuManager != null ? Campaign.Current.GameMenuManager.GetGameMenu("town") : null;
                    if (town != null)
                    {
                        int i = 0;
                        foreach (var o in town.MenuOptions) { if (o != null && o.IdString == "town_backstreet") { idx = i + 1; break; } i++; }
                    }
                }
                catch (Exception e) { Stumble("AddMenus(miejsce)", e); idx = -1; }
                starter.AddGameMenuOption("town", OptEnter, "{=!}Read the notice board", EnterCond, EnterCons, false, idx);
                _wiredTown = 1;
                int lines = AddDialogs(starter);
                _wired = true;
                var s = Settings.Current;
                Log.Info(P + "wpieta - menu town " + _wiredTown + "/1 (za town_backstreet: " + (idx >= 0 ? "index " + idx : "BRAK - na koncu") + "), podmenu " + _wiredMenus + "/2, linie rozmowy " + lines
                         + "; klucze: Notice Board " + YN(s != null && s.NoticeBoard) + ", Villages " + YN(s != null && s.NoticeBoardVillages) + TalkKeysText()
                         + WatchKeysText());
                WatchStartLine();
            }
            catch (Exception e) { Stumble("AddMenus", e); Log.Info(P + "BLAD rejestracji (" + e.Message + ") - menu town " + _wiredTown + "/1, podmenu " + _wiredMenus + "/2."); }
        }

        internal static string YN(bool b) { return b ? "TAK" : "NIE"; }

        // ------------------------------------------------------------ menu miasta

        private static bool EnterCond(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            if (!On) return false;
            var st = Settlement.CurrentSettlement;
            if (st == null || !st.IsTown) return false;
            try { if (Campaign.Current.IsMainHeroDisguised) return false; } catch { }
            try
            {
                if (AnyFree(st)) args.OptionQuestData |= GameMenuOption.IssueQuestFlags.AvailableIssue;
                args.Tooltip = new TextObject("{=!}Everyone with work for you here: the townsfolk, the lords in town" + (VillagesOn ? ", the headmen and landowners of the town's villages" : "")
                                              + (WatchOn ? " and the captain of the watch" : "") + " - what they want, what they pay and where to find them.");
            }
            catch (Exception e) { Stumble("EnterCond", e); }
            return true;
        }

        private static void EnterCons(MenuCallbackArgs args) { GameMenu.SwitchToMenu(MenuBoard); }   // zwykle menu (nie menu oczekiwania) - SwitchToMenu wolno

        private static bool BackCond(MenuCallbackArgs args) { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; }

        /// <summary>Wykrzyknik: ktos w miescie albo we wsiach miasta ma wolne zadanie (bez nagrod i tekstow - tani przeglad).</summary>
        private static bool AnyFree(Settlement st)
        {
            foreach (var issue in IssueManager.GetIssuesInSettlement(st, false))
                if (issue != null && issue.IsOngoingWithoutQuest && Usable(issue.IssueOwner)) return true;
            if (VillagesOn && st.BoundVillages != null)
                foreach (var v in st.BoundVillages)
                {
                    var vs = v != null ? v.Settlement : null;
                    if (vs == null || vs.Notables == null) continue;
                    foreach (var h in vs.Notables) if (h != null && h.Issue != null && h.Issue.IsOngoingWithoutQuest && Usable(h)) return true;
                }
            return AnyWatchFree(st);
        }

        private static bool Usable(Hero h)
        {
            return h != null && h.IsAlive && !h.IsPrisoner && h != Hero.MainHero && h.Clan != Clan.PlayerClan;
        }

        // ------------------------------------------------------------ lista

        private static void BoardInit(MenuCallbackArgs args)
        {
            long t0 = Stopwatch.GetTimestamp();
            var st = Settlement.CurrentSettlement;
            try { args.MenuTitle = new TextObject("{=!}Notice Board"); } catch { }
            try { if (st != null && st.SettlementComponent != null && !string.IsNullOrEmpty(st.SettlementComponent.WaitMeshName)) args.MenuContext.SetBackgroundMeshName(st.SettlementComponent.WaitMeshName); } catch { }
            _list.Clear();
            _sel = null;
            _boardSt = st;
            try
            {
                if (st != null && st.IsTown && On) Build(st, _list, false);
            }
            catch (Exception e) { Stumble("BoardInit(Build)", e); }
            try
            {
                args.MenuContext.SetRepeatObjectList(new List<Entry>(_list));
                MBTextManager.SetTextVariable("ARM_BOARD_TEXT", BoardText(st, _list));
            }
            catch (Exception e)
            {
                Stumble("BoardInit", e);
                try { MBTextManager.SetTextVariable("ARM_BOARD_TEXT", "The notice board."); } catch { }
            }
            _dOpen++;
            _dEntries += CountNotices(_list);
            foreach (var e in _list) _dKind[(int)e.K]++;
            _dOpenMs += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        }

        private static int CountNotices(List<Entry> list) { int n = 0; foreach (var e in list) if (e.K != Kind.Task) n++; return n; }

        private static string BoardText(Settlement st, List<Entry> list)
        {
            var sb = new StringBuilder();
            sb.Append("NOTICE BOARD - ").Append(st != null ? st.Name.ToString().ToUpperInvariant() : "");
            int n = CountNotices(list);
            sb.Append("\n").Append(n == 0 ? "No notices are pinned up today." : n == 1 ? "One notice is pinned up today." : Words(n) + " notices are pinned up today.");
            TasksText(sb, list);
            if (n > 0) sb.Append("\n\nThose in town can be seen at once; for the villages you must ride out. The better paid are pinned up first.");
            return sb.ToString();
        }

        private static string Words(int n)
        {
            string[] w = { "No", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve" };
            return n >= 0 && n < w.Length ? w[n] : n.ToString(Inv);
        }

        /// <summary>Wpisy tablicy miasta (na swiezo). probe: bez LocationComplex (hak autotestu, linia "proba").</summary>
        internal static void Build(Settlement st, List<Entry> list, bool probe)
        {
            var seen = new HashSet<Hero>();
            if (!probe) TasksInto(st, list);
            // 1) miasto: notable, bohaterowie bez partii, lordowie w partiach w miescie (gra: IssueManager.GetIssuesInSettlement)
            foreach (var issue in IssueManager.GetIssuesInSettlement(st, false))
            {
                try
                {
                    if (issue == null || !issue.IsOngoingWithoutQuest) continue;
                    var h = issue.IssueOwner;
                    if (!Usable(h) || !seen.Add(h)) continue;
                    var e = new Entry { K = h.IsLord ? Kind.Lord : Kind.Town, Hero = h, Issue = issue, Group = 1 };
                    if (h.CurrentSettlement != st) { e.K = Kind.Absent; e.Group = 3; }
                    Fill(e, st, probe);
                    list.Add(e);
                }
                catch (Exception ex) { Stumble("Build(miasto)", ex); }
            }
            // 2) wsie miasta: notable z zadaniem (rozmowa na miejscu - tylko flaga)
            if (VillagesOn && st.BoundVillages != null)
                foreach (var v in st.BoundVillages)
                {
                    var vs = v != null ? v.Settlement : null;
                    if (vs == null || vs.Notables == null) continue;
                    foreach (var h in vs.Notables)
                    {
                        try
                        {
                            if (h == null || h.Issue == null || !h.Issue.IsOngoingWithoutQuest || !Usable(h) || !seen.Add(h)) continue;
                            var e = new Entry { K = Kind.Village, Hero = h, Issue = h.Issue, Village = vs, Group = 2 };
                            Fill(e, st, probe);
                            list.Add(e);
                        }
                        catch (Exception ex) { Stumble("Build(wies)", ex); }
                    }
                }
            // 3) lordowie rodu pana miasta poza miastem (tylko flaga)
            try
            {
                var clan = st.OwnerClan;
                if (clan != null && clan != Clan.PlayerClan && clan.Heroes != null)
                    foreach (var h in clan.Heroes)
                    {
                        try
                        {
                            if (h == null || !h.IsLord || h.Issue == null || !h.Issue.IsOngoingWithoutQuest || h.CurrentSettlement == st || !Usable(h) || !seen.Add(h)) continue;
                            var e = new Entry { K = Kind.Absent, Hero = h, Issue = h.Issue, Group = 3 };
                            Fill(e, st, probe);
                            list.Add(e);
                        }
                        catch (Exception ex) { Stumble("Build(nieobecni)", ex); }
                    }
            }
            catch (Exception ex) { Stumble("Build(rod pana)", ex); }
            // 4) E2 / E2b: listy gonczne kapitana i soltysow
            WatchInto(st, list, probe);
            list.Sort(Order);
        }

        private static int Order(Entry a, Entry b)
        {
            int c = a.Group.CompareTo(b.Group);
            if (c != 0) return c;
            return b.Reward.CompareTo(a.Reward);
        }

        /// <summary>Kwota, termin, gdzie osoba jest, linia listy. Wyjatek przy czytaniu nagrody - licznik "nagroda nieczytelna".</summary>
        private static void Fill(Entry e, Settlement st, bool probe)
        {
            e.Reward = RewardOf(e.Issue);
            e.Where = WhereOf(e.Hero, st, e.Village, probe);
            string title = Clean(SafeText(() => e.Issue.Title), 120);
            if (title == null) title = "Work to be done";
            string who = NameOf(e.Hero) + ", " + RoleOf(e.Hero, e.Village);
            e.Line = who + " - " + title + " - " + (e.Reward >= 0 ? e.Reward.ToString("N0", Inv) : "?") + " - " + DueText(e.Issue.IssueDueTime, true) + " - " + e.Where;
        }

        internal static int RewardOf(IssueBase issue)
        {
            try
            {
                int r = QuestPay8.ShownReward(issue);
                if (r < 0) { _dRewardBad++; return -1; }
                return r;
            }
            catch (Exception e) { _dRewardBad++; Stumble("RewardOf", e); return -1; }
        }

        internal static string NameOf(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "someone"; } catch { return "someone"; } }

        /// <summary>Zawod / stan po angielsku (do listy i karty).</summary>
        internal static string RoleOf(Hero h, Settlement village)
        {
            if (h == null) return "";
            string of = village != null ? " of " + village.Name : "";
            if (h.IsLord) return h.IsFemale ? "lady" : "lord";
            if (h.IsMerchant) return "merchant" + of;
            if (h.IsArtisan) return "artisan" + of;
            if (h.IsGangLeader) return "gang leader" + of;
            if (h.IsHeadman) return "headman" + of;
            if (h.IsRuralNotable) return "landowner" + of;
            if (h.IsPreacher) return "preacher" + of;
            return "notable" + of;
        }

        internal static string He(Hero h, bool cap) { bool f = h != null && h.IsFemale; return cap ? (f ? "She" : "He") : (f ? "she" : "he"); }
        internal static string His(Hero h) { return h != null && h.IsFemale ? "her" : "his"; }
        internal static string Him(Hero h) { return h != null && h.IsFemale ? "her" : "him"; }

        /// <summary>Termin: dni do IssueDueTime; brak terminu; po terminie (gra zdejmuje sprawe z szansa 20% na dobe).</summary>
        internal static string DueText(CampaignTime due, bool shortForm)
        {
            try
            {
                if (due == CampaignTime.Never) return shortForm ? "no set time" : "there is no set time";
                double d = (due - CampaignTime.Now).ToDays;
                if (d <= 0.5) return shortForm ? "any day now" : "it is overdue and may be taken down any day";
                int n = Math.Max(1, (int)Math.Round(d));
                return shortForm ? n + (n == 1 ? " day" : " days") : "the offer stays up about " + n + " more " + (n == 1 ? "day" : "days");
            }
            catch { return shortForm ? "?" : "the time left is not known"; }
        }

        /// <summary>Gdzie osoba jest: lokacja w miescie (LocationComplex), wies z odlegloscia, osada, partia w polu.</summary>
        internal static string WhereOf(Hero h, Settlement st, Settlement village, bool probe)
        {
            if (h == null) return "";
            if (h.IsPrisoner) return "held prisoner";
            var cur = h.CurrentSettlement;
            if (cur != null && cur == st)
            {
                if (!probe)
                {
                    var loc = LocationOf(h, st);
                    if (loc != null) return LocName(loc);
                }
                return "in town";
            }
            if (cur != null) return "in " + cur.Name + Away(st, cur.GetPosition2D);
            var mp = h.PartyBelongedTo;
            if (mp != null)
            {
                Vec2 p = mp.GetPosition2D;
                var near = NearestPlace(p);
                string army = mp.Army != null && mp.Army.LeaderParty != null && mp.Army.LeaderParty != mp ? " (in an army)" : mp.Army != null ? " (leading an army)" : "";
                return "absent - with " + His(h) + " party" + (near != null ? " near " + near.Name : "") + army + Away(st, p);
            }
            return "away";
        }

        internal static Location LocationOf(Hero h, Settlement st)
        {
            try
            {
                if (h == null || st == null || Settlement.CurrentSettlement != st) return null;
                var lc = LocationComplex.Current;
                return lc != null ? lc.GetLocationOfCharacter(h) : null;
            }
            catch { return null; }
        }

        private static string LocName(Location loc)
        {
            switch (loc.StringId)
            {
                case "center": return "in the streets";
                case "tavern": return "in the tavern";
                case "lordshall": return "in the keep";
                case "arena": return "at the arena";
                case "prison": return "in the dungeon";
                case "alley": return "in the back alleys";
                case "port": return "at the port";
            }
            try { return "in the " + loc.Name.ToString().ToLowerInvariant(); } catch { return "in town"; }
        }

        /// <summary>" (~N km to the north-east)" od miasta tablicy.</summary>
        internal static string Away(Settlement from, Vec2 to)
        {
            try
            {
                if (from == null) return "";
                Vec2 a = from.GetPosition2D;
                float d = a.Distance(to);
                int km = (int)Math.Round(d * Wayfinder.KmPerUnit);
                if (km < 1) return "";
                return " (~" + km + " km to the " + Compass(to - a) + ")";
            }
            catch { return ""; }
        }

        internal static string Compass(Vec2 d)
        {
            if (d.LengthSquared < 0.0001f) return "here";
            double ang = Math.Atan2(d.y, d.x) * 180.0 / Math.PI;   // 0 = wschod, 90 = polnoc (jak HideoutSpotter)
            if (ang < 0) ang += 360.0;
            string[] names = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
            return names[(int)Math.Round(ang / 45.0) % 8];
        }

        internal static Settlement NearestPlace(Vec2 p)
        {
            Settlement best = null; float bd = float.MaxValue;
            foreach (var s in Settlement.All)
            {
                if (s == null || !(s.IsTown || s.IsCastle || s.IsVillage)) continue;
                float d = s.GetPosition2D.DistanceSquared(p);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        /// <summary>Tekst gry bezpiecznie (czesc tekstow ustawia zmienne dopiero w rozmowie - wyjatek / tokeny licza sie osobno).</summary>
        internal static string SafeText(Func<TextObject> get)
        {
            _dTexts++;
            try { var t = get(); return t != null ? t.ToString() : null; }
            catch { _dTok++; return null; }
        }

        /// <summary>Tekst z niewypelnionymi tokenami ("{...}") - null (licznik "tekst z tokenami"); przyciety do max znakow.</summary>
        internal static string Clean(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return null;
            if (s.IndexOf('{') >= 0) { _dTok++; return null; }
            s = s.Trim();
            if (s.Length > max)
            {
                int cut = s.LastIndexOf(' ', max);
                s = s.Substring(0, cut > max / 2 ? cut : max).TrimEnd() + "...";
            }
            return s;
        }

        private static bool ItemCond(MenuCallbackArgs args)
        {
            try
            {
                var e = args.MenuContext.GetCurrentRepeatableObject() as Entry;
                if (e == null) return false;
                args.optionLeaveType = e.K == Kind.Task ? GameMenuOption.LeaveType.WaitQuest : e.K == Kind.Watch || e.K == Kind.Headman ? GameMenuOption.LeaveType.HostileAction
                                     : e.Group == 1 ? GameMenuOption.LeaveType.Conversation : GameMenuOption.LeaveType.Default;
                MBTextManager.SetTextVariable("ARM_BOARD_ITEM", e.Line);
                if (e.K == Kind.Watch || e.K == Kind.Headman) WatchQuestFlag(args, e);
                else if (e.Hero != null && e.K != Kind.Task) MenuHelper.SetIssueAndQuestDataForHero(args, e.Hero);
                return true;
            }
            catch (Exception ex) { Stumble("ItemCond", ex); return false; }
        }

        private static void ItemCons(MenuCallbackArgs args)
        {
            _sel = args.MenuContext.GetSelectedObject() as Entry;
            GameMenu.SwitchToMenu(MenuNote);
        }

        // ------------------------------------------------------------ karta

        /// <summary>Wpis nadal aktualny (sprawdzane przy otwarciu karty i przed kazdym przyciskiem).</summary>
        private static bool Valid(Entry e)
        {
            if (e == null) return false;
            try
            {
                if (e.K == Kind.Task) return e.Quest != null && e.Quest.IsOngoing;
                if (e.K == Kind.Watch || e.K == Kind.Headman) return WatchValid(e);
                return e.Hero != null && e.Hero.IsAlive && !e.Hero.IsPrisoner && e.Issue != null && e.Hero.Issue == e.Issue && e.Issue.IsOngoingWithoutQuest;
            }
            catch { return false; }
        }

        /// <summary>E3: sprawa wpisu przyjeta przez gracza (zadanie w toku albo wyslani ludzie) - karta mowi "taken up", nie "taken down".</summary>
        private static bool Taken(Entry e)
        {
            try
            {
                var i = e != null ? e.Issue : null;
                if (i == null) return false;
                return (i.IssueQuest != null && i.IssueQuest.IsOngoing) || i.IsSolvingWithAlternative;
            }
            catch { return false; }
        }

        private static void Gone()
        {
            _dGone++;
            Log.Player("This notice has been taken down.");
            _sel = null;
            GameMenu.SwitchToMenu(MenuBoard);
        }

        private static void NoteInit(MenuCallbackArgs args)
        {
            var st = Settlement.CurrentSettlement;
            try { args.MenuTitle = new TextObject("{=!}Notice"); } catch { }
            try { if (st != null && st.SettlementComponent != null && !string.IsNullOrEmpty(st.SettlementComponent.WaitMeshName)) args.MenuContext.SetBackgroundMeshName(st.SettlementComponent.WaitMeshName); } catch { }
            string text;
            try
            {
                var e = _sel;
                if (e != null && !Valid(e) && Taken(e)) { _sel = null; text = "You have taken up this work - it is now among your tasks at the top of the board."; }
                else if (e == null || !Valid(e)) { if (e != null) _dGone++; _sel = null; text = "This notice has been taken down."; }
                else if (e.K == Kind.Task) text = TaskCard(e, st);
                else if (e.K == Kind.Watch || e.K == Kind.Headman) text = WatchCard(e, st);
                else text = IssueCard(e, st);
            }
            catch (Exception ex) { Stumble("NoteInit", ex); text = "The notice is hard to read."; }
            try { MBTextManager.SetTextVariable("ARM_NOTE_TEXT", text); } catch { }
        }

        private static string IssueCard(Entry e, Settlement st)
        {
            var h = e.Hero;
            var issue = e.Issue;
            e.Reward = RewardOf(issue);
            e.Where = WhereOf(h, st, e.Village, false);
            var sb = new StringBuilder();
            string title = Clean(SafeText(() => issue.Title), 120) ?? "Work to be done";
            sb.Append(title.ToUpperInvariant()).Append('\n');
            string home = e.Village != null ? e.Village.Name.ToString() : h.HomeSettlement != null ? h.HomeSettlement.Name.ToString() : st != null ? st.Name.ToString() : "";
            sb.Append("Posted by ").Append(NameOf(h)).Append(", ").Append(h.IsLord ? (h.IsFemale ? "lady" : "lord") + (h.Clan != null ? " of the " + h.Clan.Name : "") : RoleOf(h, null) + (home.Length > 0 ? " of " + home : ""))
              .Append(". ").Append(He(h, true)).Append(" is ").Append(e.Where).Append('.');
            string words = IssueWords(issue);
            if (words != null) sb.Append("\n\n\"").Append(words).Append('"');
            sb.Append("\n\n").Append(PayText(e)).Append("\nTime: ").Append(DueText(issue.IssueDueTime, false)).Append('.');
            if (e.K == Kind.Village) sb.Append("\n\nYou must ride to ").Append(e.Village.Name).Append(" to speak with ").Append(Him(h)).Append(Away(st, e.Village.GetPosition2D)).Append('.');
            else if (e.K == Kind.Absent) sb.Append("\n\n").Append(He(h, true)).Append(" is not in town - you must find ").Append(Him(h)).Append(" to take up the work.");
            return sb.ToString();
        }

        /// <summary>Slowa zleceniodawcy: E3 - IssueBriefByIssueGiver; bez E3 albo z tokenami - opis gry.</summary>
        private static string IssueWords(IssueBase issue)
        {
            string w = BriefOf(issue);
            if (w == null) w = Clean(SafeText(() => issue.Description), DescMax);
            return w;
        }

        private static string PayText(Entry e)
        {
            var h = e.Hero;
            var issue = e.Issue;
            var s = Settings.Current;
            var sb = new StringBuilder("Pay: ");
            if (e.Reward < 0) sb.Append("not stated");
            else if (e.Reward == 0) sb.Append("no coin - the reward is in goodwill or goods");
            else
            {
                sb.Append(e.Reward.ToString("N0", Inv)).Append(" denars");
                if (s != null && s.QuestRewardFromGiver)
                {
                    int gold = h != null ? h.Gold : 0;
                    if (gold >= e.Reward) sb.Append(" - ").Append(He(h, false)).Append(" pays from ").Append(His(h)).Append(" own purse, and ").Append(He(h, false)).Append(" can afford it.");
                    else sb.Append(" - ").Append(He(h, false)).Append(" pays from ").Append(His(h)).Append(" own purse, but has only about ").Append(Round(gold).ToString("N0", Inv)).Append(" to hand.");
                }
                else sb.Append('.');
            }
            try
            {
                if (issue != null && issue.IsThereAlternativeSolution)
                {
                    int men = issue.GetTotalAlternativeSolutionNeededMenCount();
                    int days = issue.GetTotalAlternativeSolutionDurationInDays();
                    if (men > 0 && days > 0) sb.Append("\n     (Or send a companion with ").Append(men).Append(men == 1 ? " man" : " men").Append(" for about ").Append(days).Append(days == 1 ? " day.)" : " days.)");
                }
            }
            catch { }
            return sb.ToString();
        }

        internal static int Round(int g)
        {
            if (g < 100) return Math.Max(0, g);
            if (g < 1000) return g / 10 * 10;
            return g / 50 * 50;
        }

        // ------------------------------------------------------------ przyciski karty

        private static bool InTown(Entry e, Settlement st)
        {
            return e != null && e.Hero != null && (e.K == Kind.Town || e.K == Kind.Lord) && st != null && e.Hero.CurrentSettlement == st && !e.Hero.IsPrisoner;
        }

        private static bool VisitCond(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            var e = _sel;
            var st = Settlement.CurrentSettlement;
            if (!InTown(e, st) || !Valid(e)) return false;
            try
            {
                args.Text.SetTextVariable("ARM_NOTE_VISIT", "Go and see " + Him(e.Hero));
                MBTextManager.SetTextVariable("ARM_NOTE_VISIT", "Go and see " + Him(e.Hero));
                var loc = LocationOf(e.Hero, st);
                if (loc == null || PlayerEncounter.LocationEncounter == null || LocationComplex.Current == null)
                {
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=!}You cannot find " + Him(e.Hero) + " anywhere you could walk to right now. Talk to " + Him(e.Hero) + " here instead.");
                    return true;
                }
                string id = loc.StringId == "alley" ? "center" : loc.StringId;
                if (!Walkable(id))
                {
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=!}You cannot walk there from the board. Talk to " + Him(e.Hero) + " here instead.");
                    return true;
                }
                bool disable; TextObject why;
                bool can = Campaign.Current.Models.SettlementAccessModel.CanMainHeroAccessLocation(st, id, out disable, out why);
                if (!can)
                {
                    args.IsEnabled = false;
                    args.Tooltip = why ?? new TextObject("{=!}You are not let in there.");
                }
                else args.Tooltip = new TextObject("{=!}Walk to " + LocName(loc).Replace("in the ", "the ").Replace("at the ", "the ") + " and speak with " + Him(e.Hero) + ".");
            }
            catch (Exception ex) { Stumble("VisitCond", ex); args.IsEnabled = false; }
            return true;
        }

        private static void VisitCons(MenuCallbackArgs args)
        {
            var e = _sel;
            var st = Settlement.CurrentSettlement;
            if (!Valid(e) || !InTown(e, st)) { Gone(); return; }
            try
            {
                var h = e.Hero;
                var lc = LocationComplex.Current;
                var loc = lc != null ? lc.GetLocationOfCharacter(h) : null;
                if (loc == null || PlayerEncounter.LocationEncounter == null) { _dDenied++; Log.Player("You cannot find " + NameOf(h) + " there now."); return; }
                if (loc.StringId == "alley") loc = lc.GetLocationWithId("center");
                if (loc == null || !Walkable(loc.StringId)) { _dDenied++; return; }
                bool disable; TextObject why;
                if (!Campaign.Current.Models.SettlementAccessModel.CanMainHeroAccessLocation(st, loc.StringId, out disable, out why)) { _dDenied++; if (why != null) Log.Player(why.ToString()); return; }
                Log.Info(P + "odwiedziny - " + h.StringId + " w " + loc.StringId + " (" + st.StringId + ")");   // ostatnia linia przed scena (gdyby gra wywrocila sie do pulpitu)
                _dVisit++;
                MarkFromBoard(h, st);
                // dokladnie jak gra z paska portretow (GameMenuOverlay.ExecuteTroopAction, Conversation)
                CampaignEventDispatcher.Instance.OnPlayerStartTalkFromMenu(h);
                PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(loc, null, h.CharacterObject);
            }
            catch (Exception ex) { Stumble("VisitCons", ex); }
        }

        /// <summary>Lokacje, do ktorych gra sama wchodzi z paska portretow (TownEncounter: ulice i arena osobno, reszta jako wnetrze) - port i inne cudze lokacje bez "Go and see".</summary>
        private static bool Walkable(string id)
        {
            return id == "center" || id == "tavern" || id == "lordshall" || id == "arena";
        }

        private static bool TalkCond(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
            var e = _sel;
            if (!InTown(e, Settlement.CurrentSettlement) || !Valid(e)) return false;
            try
            {
                MBTextManager.SetTextVariable("ARM_NOTE_TALK", "Talk to " + Him(e.Hero) + " now");
                args.Text.SetTextVariable("ARM_NOTE_TALK", "Talk to " + Him(e.Hero) + " now");
                args.Tooltip = new TextObject("{=!}Speak with " + NameOf(e.Hero) + " without walking the town.");
            }
            catch (Exception ex) { Stumble("TalkCond", ex); }
            return true;
        }

        private static void TalkCons(MenuCallbackArgs args)
        {
            var e = _sel;
            var st = Settlement.CurrentSettlement;
            if (!Valid(e) || !InTown(e, st)) { Gone(); return; }
            try
            {
                var h = e.Hero;
                Log.Info(P + "rozmowa teraz - " + h.StringId + " (" + st.StringId + ")");
                _dTalk++;
                MarkFromBoard(h, st);
                // jak gra (GameMenuOverlay.ExecuteTroopAction, QuickConversation)
                CampaignMapConversation.OpenConversation(new ConversationCharacterData(CharacterObject.PlayerCharacter, null, false, false, false, true),
                                                         new ConversationCharacterData(h.CharacterObject, null, false, false, false, true));
            }
            catch (Exception ex) { Stumble("TalkCons", ex); }
        }

        // ------------------------------------------------------------ flagi

        private static ITrackableCampaignObject MarkTarget(Entry e)
        {
            if (e == null) return null;
            if (e.K == Kind.Task) return e.Quest != null ? e.Quest.QuestGiver : null;
            if (e.K == Kind.Village || e.K == Kind.Headman) return e.Village;
            if (e.K == Kind.Watch) return WatchTarget(e);
            return e.Hero;
        }

        private static Flag FlagOf(ITrackableCampaignObject o)
        {
            foreach (var f in _flags) if (f.Obj != null && ReferenceEquals(f.Obj, o)) return f;
            return null;
        }

        private static bool MarkCond(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Default;
            var e = _sel;
            if (!Valid(e)) return false;
            try
            {
                var o = MarkTarget(e);
                if (o == null) return false;
                ResolveFlags();
                bool marked = FlagOf(o) != null;
                string label;
                if (marked) label = "Take the mark off the map";
                else if (o is Settlement && (e.K == Kind.Village || e.K == Kind.Headman)) label = "Ride to " + ((Settlement)o).Name + " (mark it on the map)";
                else if (o is MobileParty) label = "Mark the band on the map";
                else if (e.Hero != null && e.Hero.CurrentSettlement != Settlement.CurrentSettlement) label = "Mark " + His(e.Hero) + " party on the map";
                else label = "Mark " + (e.Hero != null ? Him(e.Hero) : "it") + " on the map";
                MBTextManager.SetTextVariable("ARM_NOTE_MARK", label);
                args.Text.SetTextVariable("ARM_NOTE_MARK", label);
            }
            catch (Exception ex) { Stumble("MarkCond", ex); return false; }
            return true;
        }

        private static void MarkCons(MenuCallbackArgs args)
        {
            var e = _sel;
            var st = Settlement.CurrentSettlement;
            if (!Valid(e)) { Gone(); return; }
            try
            {
                var o = MarkTarget(e);
                var vt = Campaign.Current != null ? Campaign.Current.VisualTrackerManager : null;
                if (o == null || vt == null) return;
                var f = FlagOf(o);
                if (f != null)
                {
                    _flags.Remove(f);
                    try { if (vt.CheckTracked(o)) vt.RemoveTrackedObject(o); } catch { }
                    Log.Player("The mark is taken off your map.");
                }
                else
                {
                    vt.RegisterObject(o);
                    var nf = new Flag { Obj = o, Until = Today + FlagDays };
                    if (o is Hero) { nf.Kind = "h"; nf.Id = ((Hero)o).StringId; }
                    else if (o is Settlement) { nf.Kind = "s"; nf.Id = ((Settlement)o).StringId; }
                    else if (o is MobileParty) { nf.Kind = "p"; nf.Id = ((MobileParty)o).StringId; }
                    else nf.Kind = "?";
                    _flags.Add(nf);
                    _dFlag++;
                    Log.Player("Marked on your map for " + FlagDays + " days: " + MarkName(o) + PlaceOf(o, st) + ".");
                    Log.Info(P + "flaga - " + nf.Kind + ":" + (nf.Id ?? "?") + " (" + (st != null ? st.StringId : "?") + ")");
                }
                GameMenu.SwitchToMenu(MenuNote);   // napis przycisku od nowa (zwykle menu)
            }
            catch (Exception ex) { Stumble("MarkCons", ex); }
        }

        private static string MarkName(ITrackableCampaignObject o)
        {
            if (o is Hero) return NameOf((Hero)o);
            if (o is Settlement) return ((Settlement)o).Name.ToString();
            if (o is MobileParty) return BandLabel((MobileParty)o);
            return "it";
        }

        private static string PlaceOf(ITrackableCampaignObject o, Settlement st)
        {
            try
            {
                Vec2 p;
                if (o is Hero)
                {
                    var h = (Hero)o;
                    if (h.CurrentSettlement != null) return h.CurrentSettlement == st ? " (here in town)" : " in " + h.CurrentSettlement.Name + Away(st, h.CurrentSettlement.GetPosition2D);
                    if (h.PartyBelongedTo == null) return "";
                    p = h.PartyBelongedTo.GetPosition2D;
                }
                else if (o is Settlement) p = ((Settlement)o).GetPosition2D;
                else if (o is MobileParty) p = ((MobileParty)o).GetPosition2D;
                else return "";
                return Away(st, p);
            }
            catch { return ""; }
        }

        /// <summary>Flagi z zapisu: obiekt po id (raz po wczytaniu, przy pierwszym uzyciu albo w dobie).</summary>
        private static void ResolveFlags()
        {
            for (int i = _flags.Count - 1; i >= 0; i--)
            {
                var f = _flags[i];
                if (f.Obj != null) continue;
                try
                {
                    if (f.Kind == "h") f.Obj = Hero.Find(f.Id);
                    else if (f.Kind == "s") f.Obj = Settlement.Find(f.Id);
                    else if (f.Kind == "p") f.Obj = FindParty(f.Id);
                }
                catch { f.Obj = null; }
                if (f.Obj == null) _flags.RemoveAt(i);   // osoby / osady juz nie ma - flaga gry zginela razem z nia
            }
        }

        internal static MobileParty FindParty(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            try { foreach (var mp in MobileParty.All) if (mp != null && mp.StringId == id) return mp; } catch { }
            return null;
        }

        /// <summary>Doba: flagi po terminie zdejmowane (nasza czesc licznika VisualTrackerManager - zadanie gry trzyma swoja).</summary>
        private static void FlagsDaily()
        {
            if (_flags.Count == 0) return;
            ResolveFlags();
            var vt = Campaign.Current != null ? Campaign.Current.VisualTrackerManager : null;
            int today = Today;
            for (int i = _flags.Count - 1; i >= 0; i--)
            {
                var f = _flags[i];
                bool gone = f.Obj is MobileParty && !((MobileParty)f.Obj).IsActive;
                if (today < f.Until && !gone) continue;
                _flags.RemoveAt(i);
                try { if (vt != null && f.Obj != null && vt.CheckTracked(f.Obj)) vt.RemoveTrackedObject(f.Obj); } catch (Exception e) { Stumble("FlagsDaily", e); }
            }
        }

        // ------------------------------------------------------------ doba

        /// <summary>ArmouryBehavior.OnDailyTick: flagi, listy gonczne (E2), linia doby, proba autotestu co 30 dob.</summary>
        internal static void Daily()
        {
            int day = Today;
            long t0 = Stopwatch.GetTimestamp();
            try { FlagsDaily(); } catch (Exception e) { Stumble("Daily(flagi)", e); }
            try { WatchDaily(day); } catch (Exception e) { Stumble("Daily(listy)", e); }
            string probe = "-";
            if (day % 30 == 5) { try { probe = Probe(); } catch (Exception e) { Stumble("Probe", e); probe = "BLAD"; } }
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            try
            {
                var s = Settings.Current;
                double avg = _dOpen > 0 ? (double)_dEntries / _dOpen : 0;
                Log.Info(P + "dzien " + day + ", klucz " + YN(s != null && s.NoticeBoard) + ", otwarcia " + _dOpen + ", wpisow sr. " + avg.ToString("0.0", Inv)
                         + " (zadania " + _dKind[0] + ", miasto " + _dKind[1] + ", lordowie " + _dKind[2] + ", wsie " + _dKind[3] + ", nieobecni " + _dKind[4] + ", straz " + _dKind[5] + ", soltysi " + _dKind[6] + ")"
                         + ", odwiedziny " + _dVisit + ", rozmowy teraz " + _dTalk + ", flagi " + _dFlag + " (czynne " + _flags.Count + "), z tablicy do zadania " + _dToQuest
                         + ", odmowy dostepu " + _dDenied + ", zdjete przed kliknieciem " + _dGone + ", nagroda nieczytelna " + _dRewardBad + ", tekst z tokenami " + _dTok + " (z " + _dTexts + " tekstow)"
                         + ", koszt otwarcia sr. " + (_dOpen > 0 ? _dOpenMs / _dOpen : 0).ToString("0.00", Inv) + " ms, doba " + ms.ToString("0.00", Inv) + " ms | proba: " + probe
                         + " | potkniecia " + _stumblesDay + " (sesja " + _stumbles + "), wpieta " + YN(_wired));
            }
            catch (Exception e) { Stumble("Daily(linia)", e); }
            ZeroDay();
        }

        /// <summary>Hak autotestu (autotest nie klika menu): tablice 10 miast budowane bez UI - liczby wpisow, nagrody, terminy, twarz kapitana. Tylko log.</summary>
        private static string Probe()
        {
            var towns = new List<Settlement>();
            var ew4 = Settlement.Find("town_EW4");
            if (ew4 != null) towns.Add(ew4);
            int step = Math.Max(1, Town.AllTowns.Count / 9);
            for (int i = 0; i < Town.AllTowns.Count && towns.Count < 10; i += step)
            {
                var s = Town.AllTowns[i] != null ? Town.AllTowns[i].Settlement : null;
                if (s != null && !towns.Contains(s)) towns.Add(s);
            }
            long t0 = Stopwatch.GetTimestamp();
            int n = 0, rew = 0, bad = 0, due = 0;
            var kinds = new int[7];
            var sb = new StringBuilder();
            string sample = null;
            foreach (var st in towns)
            {
                var list = new List<Entry>();
                try { Build(st, list, true); } catch (Exception e) { Stumble("Probe(Build " + st.StringId + ")", e); continue; }
                n += list.Count;
                foreach (var e in list)
                {
                    kinds[(int)e.K]++;
                    if (e.Reward >= 0) rew++; else bad++;
                    if (e.Issue != null && e.Issue.IssueDueTime != CampaignTime.Never) due++;
                    if (sample == null && e.Issue != null) sample = st.StringId + ": " + e.Line;
                }
                sb.Append(st.StringId).Append(' ').Append(list.Count).Append(", ");
            }
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            return "miast " + towns.Count + ", wpisow " + n + " [" + sb.ToString().TrimEnd(',', ' ') + "] (miasto " + kinds[1] + ", lordowie " + kinds[2] + ", wsie " + kinds[3] + ", nieobecni " + kinds[4]
                   + ", straz " + kinds[5] + ", soltysi " + kinds[6] + "), nagroda czytelna " + rew + "/" + (rew + bad) + ", z terminem " + due + WatchProbeText(towns) + ", koszt " + ms.ToString("0.0", Inv) + " ms"
                   + (sample != null ? ", przyklad \"" + sample + "\"" : "");
        }

        // ------------------------------------------------------------ zapis arm_board
        // nb2 (m103-drobne): "nb2|D=doba|P=przedrostek0,przedrostek1,...|F=k~id~doba-D;...|W=...|H=..." - id (miasto, banda, wies, rod, soltys, flaga) z przedrostkiem
        // z tablicy P: "^<nr base36>_<koncowka>" (np. sea_raiders_246 -> ^0_246); doby jako przesuniecie wzgledem D (zapisanej doby zapisu - odczyt nie zalezy od zegara).
        // nb1 (stary, zapisy Jeffa z 101): "nb1|F=k~id~doba;...|W=...|H=..." - czytany jak dotad (WatchImport).
        // Bieg 101: nb1 mial 18.5 KB (> 16 KB - SaveText kawalkuje); objetosc: listy (84 x ok. 81 B) i szkody band (159 band, ok. 72 B na bande).
        private static int _secHead, _secF, _secW, _secH, _secHarmN, _secV1 = -1;   // rozmiary czesci ostatniego zapisu (linia doby listow) i nb1 w tym samym stanie

        /// <summary>m103-drobne: tablica przedrostkow id do zapisu nb2 - przedrostek (do ostatniego '_') raz w P, w tekscie "^nr_koncowka" (tylko gdy krocej);
        /// koncowka liczbowa od 5 cyfr bez zera na przodzie (bandy ROT: looters_2147131604) - "^nr:liczba w base36" (10 cyfr -> 6 znakow).</summary>
        private sealed class IdTable
        {
            private readonly Dictionary<string, int> _ix = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly List<string> _list = new List<string>();
            internal string Enc(string id)
            {
                id = Safe(id);
                if (id.Length < 6 || id[0] == '^') return id;
                int p = id.LastIndexOf('_');
                if (p < 3 || p >= id.Length - 1) return id;
                string pre = id.Substring(0, p), suf = id.Substring(p + 1);
                if (pre.IndexOfAny(BadPre) >= 0 || suf.IndexOfAny(BadPre) >= 0) return id;
                int k;
                bool known = _ix.TryGetValue(pre, out k);
                long num;
                bool digits = suf.Length >= 5 && suf.Length <= 18 && suf[0] != '0' && AllDigits(suf) && long.TryParse(suf, NumberStyles.None, Inv, out num);
                string code = "^" + B36(known ? k : _list.Count) + (digits ? ":" + B36L(long.Parse(suf, NumberStyles.None, Inv)) : "_" + suf);
                if (code.Length >= id.Length) return id;
                if (!known) { _ix[pre] = _list.Count; _list.Add(pre); }
                return code;
            }
            internal string Text() { return string.Join(",", _list.ToArray()); }
        }
        private static readonly char[] BadPre = { ',', '^', '/', '.', ':' };

        private static bool AllDigits(string s) { foreach (char c in s) if (c < '0' || c > '9') return false; return true; }

        private static string B36(int v) { return B36L(v); }

        private static string B36L(long v)
        {
            const string D = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (v <= 0) return "0";
            var sb = new StringBuilder(8);
            while (v > 0) { sb.Insert(0, D[(int)(v % 36)]); v /= 36; }
            return sb.ToString();
        }

        private static long FromB36(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 12) return -1;
            long v = 0;
            foreach (char c in s)
            {
                int d = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'z' ? c - 'a' + 10 : -1;
                if (d < 0) return -1;
                v = v * 36 + d;
            }
            return v;
        }

        /// <summary>m103-drobne: id z zapisu nb2 - "^nr_koncowka" albo "^nr:liczba base36" z tablicy przedrostkow; reszta bez zmian.</summary>
        private static string Dec(string s, string[] pre)
        {
            if (string.IsNullOrEmpty(s) || s[0] != '^' || pre == null) return s ?? "";
            int u = s.IndexOfAny(DecSep, 1);
            if (u < 2) return s;
            long k = FromB36(s.Substring(1, u - 1));
            if (k < 0 || k >= pre.Length) return s;
            if (s[u] == '_') return pre[(int)k] + s.Substring(u);
            long num = FromB36(s.Substring(u + 1));
            return num > 0 ? pre[(int)k] + "_" + num.ToString(Inv) : s;
        }
        private static readonly char[] DecSep = { '_', ':' };

        internal static string Export()
        {
            try
            {
                bool any = _flags.Count > 0 || WatchAny();
                if (!any) { _secHead = _secF = _secW = _secH = _secHarmN = 0; return null; }
                int d0 = Today;
                var ids = new IdTable();
                var f = new StringBuilder();
                foreach (var fl in _flags)
                {
                    if (string.IsNullOrEmpty(fl.Id) || fl.Kind == "?") continue;
                    if (f.Length > 0) f.Append(';');
                    f.Append(fl.Kind).Append('~').Append(ids.Enc(fl.Id)).Append('~').Append((fl.Until - d0).ToString(Inv));
                }
                var w = new StringBuilder(); var h = new StringBuilder();
                _secHarmN = WatchExport2(w, h, d0, ids);
                var sb = new StringBuilder(64 + f.Length + w.Length + h.Length);
                sb.Append(Ver2).Append("|D=").Append(d0.ToString(Inv)).Append("|P=").Append(ids.Text());
                _secHead = sb.Length;
                sb.Append("|F=").Append(f); _secF = f.Length + 3;
                sb.Append("|W=").Append(w); _secW = w.Length + 3;
                sb.Append("|H=").Append(h); _secH = h.Length + 3;
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        /// <summary>m103-drobne: stary format nb1 w tym samym stanie - TYLKO do pomiaru w linii doby (porownanie objetosci), nie do zapisu.</summary>
        private static string ExportV1()
        {
            try
            {
                bool any = _flags.Count > 0 || WatchAny();
                if (!any) return null;
                var sb = new StringBuilder(Ver);
                sb.Append("|F=");
                bool first = true;
                foreach (var f in _flags)
                {
                    if (string.IsNullOrEmpty(f.Id) || f.Kind == "?") continue;
                    if (!first) sb.Append(';');
                    first = false;
                    sb.Append(f.Kind).Append('~').Append(Safe(f.Id)).Append('~').Append(f.Until.ToString(Inv));
                }
                WatchExport(sb);
                return sb.ToString();
            }
            catch (Exception e) { Stumble("ExportV1", e); return null; }
        }

        /// <summary>m103-drobne: sklad ostatniego zapisu do linii doby listow (czesci nb2 w bajtach i nb1 w tym samym stanie).</summary>
        private static string SaveMixText(int bytes)
        {
            return "sklad zapisu " + Ver2 + ": naglowek z przedrostkami " + _secHead + " B, flagi " + _secF + " B, listy " + _secW + " B (" + _notices.Count + "), szkody " + _secH + " B (band " + _harm.Count
                   + ", wpisow " + _secHarmN + "), stary format nb1 w tym samym stanie " + (_secV1 >= 0 ? _secV1 + " B" : "-")
                   + (_secV1 > 0 ? " (" + (100.0 * bytes / _secV1).ToString("0", Inv) + "%)" : "");
        }

        internal static void Import(string s)
        {
            _flags.Clear();
            if (string.IsNullOrEmpty(s)) return;
            try
            {
                var parts = s.Split('|');
                if (parts.Length > 0 && parts[0] == Ver2) { Import2(parts, s.Length); return; }   // m103-drobne
                if (parts.Length == 0 || parts[0] != Ver) { Log.Info(P + "zapis arm_board w nieznanej wersji (" + (parts.Length > 0 ? parts[0] : "?") + ") - pominiety."); return; }
                for (int i = 1; i < parts.Length; i++)
                {
                    var p = parts[i];
                    if (p.StartsWith("F=", StringComparison.Ordinal))
                    {
                        foreach (var r in p.Substring(2).Split(';'))
                        {
                            var x = r.Split('~');
                            int until;
                            if (x.Length < 3 || !int.TryParse(x[2], NumberStyles.Integer, Inv, out until)) continue;
                            _flags.Add(new Flag { Kind = x[0], Id = x[1], Until = until });
                        }
                    }
                    else WatchImport(p);
                }
                Log.Info(P + "wczytano zapis arm_board w starym formacie nb1 (" + s.Length + " B) - flagi " + _flags.Count + ", listy " + _notices.Count + ", szkody band " + _harm.Count
                         + "; nastepny zapis w formacie " + Ver2 + ".");
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        /// <summary>m103-drobne: odczyt nb2 - najpierw D (doba zapisu) i P (przedrostki), potem flagi, listy i szkody (doby = D + przesuniecie).</summary>
        private static void Import2(string[] parts, int bytes)
        {
            int d0 = 0; bool hasD = false;
            string[] pre = new string[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p.StartsWith("D=", StringComparison.Ordinal)) hasD = int.TryParse(p.Substring(2), NumberStyles.Integer, Inv, out d0);
                else if (p.StartsWith("P=", StringComparison.Ordinal)) pre = p.Length > 2 ? p.Substring(2).Split(',') : new string[0];
            }
            if (!hasD) { Log.Info(P + "zapis arm_board nb2 bez doby D - pominiety."); return; }
            for (int i = 1; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p.StartsWith("F=", StringComparison.Ordinal))
                {
                    if (p.Length <= 2) continue;
                    foreach (var r in p.Substring(2).Split(';'))
                    {
                        var x = r.Split('~');
                        int off;
                        if (x.Length < 3 || !int.TryParse(x[2], NumberStyles.Integer, Inv, out off)) continue;
                        _flags.Add(new Flag { Kind = x[0], Id = Dec(x[1], pre), Until = d0 + off });
                    }
                }
                else if (p.StartsWith("W=", StringComparison.Ordinal) || p.StartsWith("H=", StringComparison.Ordinal)) WatchImport2(p, d0, pre);
            }
            int hn = 0;
            foreach (var kv in _harm) hn += kv.Value.Count;
            Log.Info(P + "wczytano zapis arm_board nb2 (" + bytes + " B, doba zapisu " + d0 + ", przedrostkow " + pre.Length + ") - flagi " + _flags.Count + ", listy " + _notices.Count
                     + ", szkody band " + _harm.Count + " (wpisow " + hn + ").");
        }

        internal static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("|", "/").Replace(";", ",").Replace("~", "-").Replace("=", "-");
        }
    }
}
