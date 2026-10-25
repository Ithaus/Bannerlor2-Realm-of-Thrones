using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P4 "przeglad-armii" (projekt przeglad-armii.json; klucze ArmyReviewEnabled, ArmyReviewWritesOrderOfBattle). Jeff 25.10: "w obozie powinna byc
    /// opcja przeglad armii ... popup, gdzie mamy pokazane wojsko, gdzie mozna stworzyc choragwie i przenosic wojsko i dodawac dowodcow ... potem jak sie uruchamia
    /// bitwa to tak tez podzielona jest od razu armia, do tego pokazane jest ile koni jest uzywanych i ile uprzezy".
    ///
    /// CO ROBI (v1):
    ///  - opcja "Review the army" w naszym menu obozu arm_camp_wait (zwykle menu, indeks 5 - wolny: sen 1, musztra/spacer 2, markietani 3, jency 4, zwiniecie 9)
    ///    i w menu obozu BK bk_camping_wait_menu (menu OCZEKIWANIA - z opcji TYLKO okno inquiry, nigdy SwitchToMenu ani ExitToLast, CLAUDE.md 7);
    ///  - okno (lancuch MultiSelectionInquiry / InquiryData / TextInquiry z pauza): ludzie wedlug rodzaju (zdrowi / ranni), konie i uprzaz (SaddleLaw.Pairs -
    ///    ta sama funkcja co regula jezdzca; w uzyciu / wolne / brakujace, ilu jezdzcow pojdzie pieszo i DLACZEGO), marsz (MarchPace.Explain - ten sam
    ///    CountColumn i Cap co wpis predkosci "Marching column: N afoot");
    ///  - choragwie = 8 formacji Order of Battle gry: zrodlo prawdy to OrderOfBattleCampaignBehavior (GetFormationDataAtIndex / SetFormationInfos - publiczne API,
    ///    bez latek). Okno CZYTA zapis gry (takze to, co gracz ustawil w ostatniej bitwie), kopia robocza, zapis po "Save". Zapis pisze 8 wpisow (pole i armia;
    ///    oblezen nie ruszamy) - SandboxBattleSpawnModel.GetInitialSpawnAssignments rozstawia ludzi wedlug nich, gdy wszystkie 8 istnieje (dec sandbox :130-146),
    ///    SPOrderOfBattleVM.LoadConfiguration pokazuje je na ekranie Order of Battle (gracz dowodzi, >= 20 ludzi). Po rozstawieniu gra nadpisuje zapis tym, co
    ///    gracz wystawil (SaveConfiguration - tylko gdy ekran byl pokazany) - okno zawsze czyta stan gry ("jedna prawda"). Bez ekranu dzialaja tylko wagi:
    ///    kapitanow dobiera AssignBestCaptainsForTeam, HeroTroops i filtry nie sa czytane (recenzja 25.10). Zapis zostaje po wylaczeniu kluczy (spawn czyta go
    ///    niezaleznie od nas) - dlatego "Return to the game's own order" jest czynne takze przy ArmyReviewWritesOrderOfBattle NIE.
    ///  - walidacja: kazdy rodzaj wojska obecny (zdrowi szeregowi partii, ktorymi gracz dowodzi) musi miec choragiew - inaczej OrderOfBattleVM.RefreshMissingFormations
    ///    blokuje start bitwy; brakujacy rodzaj dostaje "the rest" w wolnym indeksie, a gdy 8 zajetych - odmowa zapisu. Wagi kazdej klasy sumuja sie do 100
    ///    (pierwsza choragiew klasy bierze reszte, gdy zadna nie ma "the rest"); bohaterowie martwi, jency i spoza druzyny odfiltrowani, kazdy najwyzej raz.
    ///  - nazwy choragwi to NASZE metadane (zapis arm_army_review): wazne tylko, gdy klasa pod indeksem w zapisie gry jest ta sama (NameOf dla P3).
    /// v2 (noc 25.10 P12, ArmyReview.Pin.cs, klucz ArmyReviewPinTroopTypes): przypiecie konkretnych TYPOW wojska druzyny gracza do choragwi ("Move troop types
    /// into banners") - spawn, posilki, ekran Order of Battle i po rozstawieniu; udzialy choragwi licza tylko nieprzypietych (ShareFit).
    /// CZEGO NIE UMIE: stalej liczby ludzi (waga to procent klasy), formacji mieszanych do edycji (formacje dwu rodzajow z zapisu gry zostaja jak sa), oblezen,
    /// bitew morskich, przypiec ludzi innych partii armii gracza.
    /// Potkniecia liczone per miejsce (bez globalnego wylacznika); linie logu: start sesji, raport (na otwarcie okna), zapis / odmowa / powrot, doba (tylko przy
    /// ruchu), bitwa (ArmyReview.Battle.cs), test (ArmyReview.Test.cs). Wszystkie napisy w grze po angielsku.
    /// </summary>
    internal static partial class ArmyReview
    {
        private const string P = "Przeglad armii (P4): ";
        internal const int Slots = 8;
        private const int KeepShare = -1, TheRest = -2;
        private const int CampIndex = 5, BkIndex = 2;
        private static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };
        private static readonly int[] MenSteps = { 10, 20, 25, 30, 40, 50, 60, 75, 100, 125, 150, 200, 250, 300, 400, 500, 750, 1000 };
        // dowodcy i szeregi choragwi dzialaja tylko z ekranem Order of Battle (CanPlayerSideDeployWithOrderOfBattle: gracz prowadzi strone, >= 20 ludzi, nie wypad);
        // bez ekranu GeneralsAndCaptainsAssignmentLogic.AssignBestCaptainsForTeam sam dobiera kapitanow, a HeroTroops czyta tylko SPOrderOfBattleVM.LoadConfiguration
        private const string OobOnly = " (when you command 20 men or more and the Order of Battle screen shows before the fight; otherwise the game picks the captains"
                                       + " and places your companions itself).";

        internal static bool On { get { var s = Settings.Current; return s != null && s.ArmyReviewEnabled; } }
        private static bool Writes { get { var s = Settings.Current; return s != null && s.ArmyReviewEnabled && s.ArmyReviewWritesOrderOfBattle; } }

        /// <summary>Choragiew w kopii roboczej (indeks tablicy = indeks formacji 0..7).</summary>
        internal sealed class Slot
        {
            public int Cls;                                  // DeploymentFormationClass 1..6
            public int W1, W2;                               // wagi z zapisu gry (procent klasy glownej / drugiej)
            public int Men = KeepShare;                      // -1 udzial z zapisu (W1), -2 reszta klasy, >= 0 liczba ludzi wybrana w oknie
            public readonly bool[] Filt = new bool[7];       // FormationFilterType 1..6
            public Hero Captain;
            public readonly List<Hero> Ranks = new List<Hero>();
            public string Name;
            public bool Auto;                                // dopisana przy zapisie (rodzaj wojska bez choragwi)

            public Slot Clone()
            {
                var s = new Slot { Cls = Cls, W1 = W1, W2 = W2, Men = Men, Captain = Captain, Name = Name, Auto = Auto };
                Array.Copy(Filt, s.Filt, Filt.Length);
                s.Ranks.AddRange(Ranks);
                return s;
            }
        }

        // nazwy choragwi gracza: indeks formacji -> (klasa w zapisie gry w chwili nadania, nazwa)
        private static readonly Dictionary<int, KeyValuePair<int, string>> _names = new Dictionary<int, KeyValuePair<int, string>>();
        private static string _importNote = "brak klucza (stary zapis albo nowa kampania)";

        // okno: kopia robocza choragwi (null - nie otwarta), zmiany niezapisane, kopia z porzadku gry (zapisu brak)
        private static Slot[] _work;
        private static bool _dirty, _fromGame;

        // liczniki doby i sesji
        private static int _dWindows, _dReports, _dBanners, _dSaves, _dRefused, _dAuto, _dResets, _dStumbles;
        private static int _stumbles, _sessionDays;
        private static readonly Dictionary<string, int> _stumbleAt = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly object _owner = new object();

        private static OrderOfBattleCampaignBehavior _oob;
        private static Campaign _oobOf;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            int n;
            _stumbleAt.TryGetValue(where, out n);
            _stumbleAt[where] = ++n;
            if (n <= 3 || n % 50 == 0) { try { Log.Error("ArmyReview." + where + " (potkniecie " + n + ")", e); } catch { } }
        }

        /// <summary>Zachowanie Order of Battle gry tej kampanii (null - inny mod je zdjal).</summary>
        internal static OrderOfBattleCampaignBehavior Behavior()
        {
            var c = Campaign.Current;
            if (c == null) return null;
            if (_oobOf != c) { _oob = c.GetCampaignBehavior<OrderOfBattleCampaignBehavior>(); _oobOf = c; }
            return _oob;
        }

        // ------------------------------------------------------------ kontrakt P0
        /// <summary>Konstruktor ArmouryBehavior (nowa kampania / wczytanie - PRZED SyncData).</summary>
        internal static void Reset()
        {
            _names.Clear();
            _importNote = "brak klucza (stary zapis albo nowa kampania)";
            _work = null; _dirty = false; _fromGame = false;
            _oob = null; _oobOf = null;
            _stumbles = 0; _stumbleAt.Clear(); _sessionDays = 0;
            DayReset();
            TestReset();
            PinReset();   // P12: przypiecia typow i plan bitwy
        }

        private static void DayReset() { _dWindows = _dReports = _dBanners = _dSaves = _dRefused = _dAuto = _dResets = _dStumbles = 0; _dPinSaves = 0; }

        /// <summary>Zapis arm_army_review: "ar1|indeks~klasa~nazwa|...|p~typ~indeks|..." (nazwy bez '|' i '~'; P12: przypiecia typow "p~StringId~indeks" -
        /// naglowek dalej "ar1": stary kod liczy je jako zle wpisy i czyta nazwy); null = brak nazw i przypiec (klucz nie pisany).</summary>
        internal static string Export()
        {
            if (_names.Count == 0 && _pins.Count == 0) return null;
            var sb = new StringBuilder("ar1");
            for (int i = 0; i < Slots; i++)
            {
                KeyValuePair<int, string> nm;
                if (!_names.TryGetValue(i, out nm) || string.IsNullOrEmpty(nm.Value)) continue;
                sb.Append('|').Append(i.ToString(CultureInfo.InvariantCulture)).Append('~').Append(nm.Key.ToString(CultureInfo.InvariantCulture)).Append('~').Append(Clean(nm.Value));
            }
            foreach (var kv in Sorted(_pins))
                if (PinIdOk(kv.Key) && kv.Value >= 0 && kv.Value < Slots)
                    sb.Append("|p~").Append(kv.Key).Append('~').Append(kv.Value.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _names.Clear();
            _pins.Clear();
            if (s == null) { _importNote = "brak klucza (stary zapis albo nowa kampania)"; return; }
            int bad = 0;
            try
            {
                var parts = s.Split('|');
                if (parts.Length == 0 || parts[0] != "ar1") { _importNote = "nieznana wersja zapisu - nazwy pominiete"; return; }
                for (int k = 1; k < parts.Length; k++)
                {
                    var f = parts[k].Split('~');
                    int i, cls;
                    if (f.Length == 3 && f[0] == "p")
                    {
                        // P12: przypiecie typu "p~StringId~indeks"
                        if (PinIdOk(f[1]) && int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out i) && i >= 0 && i < Slots) _pins[f[1]] = i;
                        else bad++;
                        continue;
                    }
                    if (f.Length != 3 || !int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out i)
                        || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out cls) || i < 0 || i >= Slots || cls < 1 || cls > 6)
                    { bad++; continue; }
                    string n = Clean(f[2]);
                    if (n.Length == 0) { bad++; continue; }
                    _names[i] = new KeyValuePair<int, string>(cls, n);
                }
                _importNote = "nazw " + _names.Count + ", przypiec typow " + _pins.Count + (bad > 0 ? ", zlych wpisow " + bad : "");
            }
            catch (Exception e) { Stumble("Import", e); _names.Clear(); _pins.Clear(); _importNote = "blad odczytu - nazwy i przypiecia pominiete"; }
        }

        /// <summary>Nazwa choragwi gracza dla formacji 0..7 (P3 - komunikaty kompanii); null = brak nazwy albo klasa w zapisie gry inna niz przy nadaniu.</summary>
        internal static string NameOf(int formationIndex)
        {
            try
            {
                KeyValuePair<int, string> nm;
                if (formationIndex < 0 || formationIndex >= Slots || !_names.TryGetValue(formationIndex, out nm)) return null;
                var b = Behavior();
                var mp = MobileParty.MainParty;
                if (b == null || mp == null) return null;
                var d = b.GetFormationDataAtIndex(formationIndex, false, mp.Army != null);
                return d != null && (int)d.FormationClass == nm.Key ? nm.Value : null;
            }
            catch (Exception e) { Stumble("NameOf", e); return null; }
        }

        // ------------------------------------------------------------ menu obozu
        internal static void AddMenus(CampaignGameStarter starter)
        {
            _sessionDays = 0; _sessionHours = 0;
            bool camp = false, bk = false, daily = false;
            if (starter != null)
            {
                try
                {
                    starter.AddGameMenuOption("arm_camp_wait", "arm_camp_review", "{=!}Review the army", Cond, delegate (MenuCallbackArgs a) { OpenSafe(); }, false, CampIndex);
                    camp = true;
                }
                catch (Exception e) { Stumble("AddMenus.arm_camp_wait", e); }
                try
                {
                    // menu OCZEKIWANIA BK: opcja tylko otwiera okno (pauza w oknie), menu sie nie zmienia
                    starter.AddGameMenuOption("bk_camping_wait_menu", "arm_bk_review", "{=!}Review the army", Cond, delegate (MenuCallbackArgs a) { OpenSafe(); }, false, BkIndex);
                    bk = true;
                }
                catch (Exception e) { Stumble("AddMenus.bk_camping_wait_menu", e); }
            }
            try
            {
                object owner = null;
                try { owner = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<ArmouryBehavior>() : null; } catch { owner = null; }
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(owner ?? _owner, Daily);
                daily = true;
                // P12: proba autotestu takze w takcie godziny (124c: proba czekala tylko na takt doby)
                CampaignEvents.HourlyTickEvent.AddNonSerializedListener(owner ?? _owner, Hourly);
            }
            catch (Exception e) { Stumble("AddMenus.DailyTick", e); }
            try { Log.Info(P + "start sesji | opcja \"Review the army\" w arm_camp_wait (" + (camp ? "TAK" : "NIE") + ", indeks " + CampIndex + ") i bk_camping_wait_menu ("
                           + (bk ? "TAK" : "NIE") + ", indeks " + BkIndex + "), takt doby " + (daily ? "TAK" : "NIE") + " | klucze Army Review Enabled " + Yes(On)
                           + ", Writes Order Of Battle " + Yes(Writes) + ", Pin Troop Types " + Yes(PinOn) + " | " + OobState() + " | zapis arm_army_review: " + _importNote
                           + " | przypiecia " + PinSummary(_pins) + " | proba autotestu: " + TestState() + "."); }
            catch (Exception e) { Stumble("AddMenus.Log", e); }
        }

        private static string Yes(bool b) { return b ? "TAK" : "NIE"; }

        /// <summary>Stan zapisu Order of Battle gry do linii logu: liczba wpisow i klasy pola i armii.</summary>
        internal static string OobState()
        {
            var b = Behavior();
            if (b == null) return "Order of Battle gry BRAK";
            return "Order of Battle gry JEST, zapis pola " + Entries(b, false) + ", armii " + Entries(b, true);
        }

        private static string Entries(OrderOfBattleCampaignBehavior b, bool army)
        {
            int n = 0;
            var sb = new StringBuilder();
            for (int i = 0; i < Slots; i++)
            {
                var d = b.GetFormationDataAtIndex(i, false, army);
                if (d == null) break;
                n++;
                if (sb.Length > 0) sb.Append(',');
                sb.Append((int)d.FormationClass);
                if (d.Captain != null) sb.Append('k');
            }
            return n == 0 ? "brak" : n + " wpisow (klasy " + sb + ")";
        }

        internal static bool Can(out string why)
        {
            why = null;
            var mp = MobileParty.MainParty;
            if (Campaign.Current == null || mp == null || Hero.MainHero == null) why = "No campaign is running.";
            else if (mp.MapEvent != null || mp.SiegeEvent != null || PlayerEncounter.Current != null) why = "Not while in a battle or siege.";
            else if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) why = "Only on land.";
            else if (!Hero.MainHero.IsAlive || Hero.MainHero.IsPrisoner) why = "Not now.";
            return why == null;
        }

        private static bool Cond(MenuCallbackArgs a)
        {
            try
            {
                if (!On) return false;
                a.optionLeaveType = GameMenuOption.LeaveType.Manage;
                string why;
                if (!Can(out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true; }
                a.Tooltip = new TextObject("{=!}Your men by kind, the horses and harness in use and missing, why riders will go on foot and why the column walks,"
                                           + " and your banners for the next battle.");
                return true;
            }
            catch (Exception e) { Stumble("Cond", e); return false; }
        }

        private static void OpenSafe() { try { _dWindows++; OpenMain(true); } catch (Exception e) { Stumble("Open", e); } }

        // ------------------------------------------------------------ liczenie wojska
        /// <summary>Rodzaj szeregowego wedlug wzorca - te same reguly co SandboxBattleInitializationModel.GetAllAvailableTroopTypes: 0 piechota, 1 lucznicy,
        /// 2 konni, 3 konni lucznicy.</summary>
        internal static int KindOf(CharacterObject ch)
        {
            if (ch.IsMounted) return ch.IsRanged ? 3 : 2;
            return ch.IsRanged ? 1 : 0;
        }

        /// <summary>Zdrowi i ranni wedlug rodzaju (0..3) i bohaterowie (4) partii.</summary>
        internal static void CountMen(MobileParty mp, int[] fit, int[] wounded)
        {
            var r = mp != null ? mp.MemberRoster : null;
            if (r == null) return;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || el.Number <= 0) continue;
                int k = ch.IsHero ? 4 : KindOf(ch);
                int w = Math.Max(0, Math.Min(el.Number, el.WoundedNumber));
                fit[k] += el.Number - w;
                wounded[k] += w;
            }
        }

        /// <summary>Partie, ktorymi gracz dowodzi w bitwie (wagi Order of Battle dziela ich ludzi): druzyna gracza i - gdy prowadzi armie - jej partie.</summary>
        private static List<MobileParty> Commanded()
        {
            var l = new List<MobileParty>();
            var mp = MobileParty.MainParty;
            if (mp == null) return l;
            l.Add(mp);
            var army = mp.Army;
            if (army != null && army.LeaderParty == mp && army.Parties != null)
                foreach (var p in army.Parties) if (p != null && p != mp && !l.Contains(p)) l.Add(p);
            return l;
        }

        /// <summary>Zdrowi szeregowi wedlug rodzaju 0..3 we wszystkich partiach, ktorymi gracz dowodzi.</summary>
        internal static int[] CommandedFit()
        {
            var fit = new int[5];
            var w = new int[5];
            foreach (var p in Commanded()) CountMen(p, fit, w);
            return new[] { fit[0], fit[1], fit[2], fit[3] };
        }

        // ------------------------------------------------------------ teksty rodzajow
        internal static string KindName(int cls)
        {
            switch (cls)
            {
                case 1: return "Infantry";
                case 2: return "Archers";
                case 3: return "Cavalry";
                case 4: return "Horse archers";
                case 5: return "Infantry and archers";
                case 6: return "Cavalry and horse archers";
                default: return "Unset";
            }
        }

        private static string KindLower(int cls) { return KindName(cls).ToLowerInvariant(); }

        private static readonly string[] FilterNames = { "", "shields", "spears", "throwing weapons", "heavy armour", "veterans (high tier)", "recruits (low tier)" };

        private static string Num(float f) { return f.ToString("0.0", CultureInfo.InvariantCulture); }

        // ------------------------------------------------------------ okno 1: przeglad
        private static void OpenMain(bool logIt)
        {
            string why;
            if (!Can(out why)) { Log.Player(why, true); return; }
            var mp = MobileParty.MainParty;
            var fit = new int[5];
            var wnd = new int[5];
            CountMen(mp, fit, wnd);
            var pairs = SaddleLaw.Pairs(mp);
            MarchPace.Column col = null;
            try { col = MarchPace.Explain(mp); } catch (Exception e) { Stumble("Explain", e); }
            var pagesMen = Paginate(MenText(mp, fit, wnd, pairs));
            var pagesHorses = Paginate(HorseText(mp, pairs));
            var pagesMarch = Paginate(MarchText(mp, col, pairs));
            if (logIt) { try { Log.Info(ReportLine(mp, fit, wnd, pairs, col, pagesMen, pagesHorses, pagesMarch)); } catch (Exception e) { Stumble("ReportLine", e); } }

            int allFit = fit[0] + fit[1] + fit[2] + fit[3] + fit[4];
            int allWnd = wnd[0] + wnd[1] + wnd[2] + wnd[3] + wnd[4];
            int prisoners = mp.PrisonRoster != null ? mp.PrisonRoster.TotalManCount : 0;
            string desc = (allFit + allWnd) + (allFit + allWnd == 1 ? " man" : " men") + " under your banner - " + allFit + " fit to fight, " + allWnd + " wounded"
                          + (prisoners > 0 ? "; " + prisoners + " prisoners in the column" : "") + ". Pick what to look at.";

            int riders = pairs.Riders + pairs.Exempt;
            int foot = FootInBattle(pairs);
            var rows = new List<InquiryElement>
            {
                new InquiryElement("men", "Your men: " + fit[0] + " infantry, " + fit[1] + " archers, " + fit[2] + " cavalry, " + fit[3] + " horse archers", null, true,
                                   "Fit to fight by kind (the wounded are counted on the page). Kind follows each man's troop: the same sorting the Order of Battle uses."),
                new InquiryElement("horses", riders == 0 ? "Horses and harness: no riders in your party"
                                                         : "Horses and harness: " + (riders - foot) + " of " + riders + " riders ready to ride", null, true,
                                   foot > 0 ? foot + " riders will fight on foot - the page says why." : "Horses and harness in the troops' armoury and in your baggage."),
                new InquiryElement("march", MarchRow(col), null, true, "Why your column walks or rides on the map, and what would change it."),
            };
            var b = Behavior();
            int raised = 0, entries = 0;
            bool saved = false;
            try { if (b != null) { var cur = ReadSlots(out saved, out entries); if (cur != null) foreach (var s in cur) if (s != null) raised++; } }
            catch (Exception e) { Stumble("Main.Read", e); }
            rows.Add(new InquiryElement("banners", b == null ? "Banners for the next battle (not available)"
                                                  : saved ? "Banners for the next battle (" + raised + " raised)" : "Banners for the next battle (the game's own order)",
                                        null, b != null,
                                        b == null ? "The game's Order of Battle is not running in this campaign, so there is nothing to set."
                                                  : "Each banner is one of the game's 8 formations: its kind of troops, a share of them, a commander and the companions in its ranks."));
            // powrot przywraca porzadek gry (nic nie zapisuje z okna) - czynny takze przy Writes NIE, bo zapisane choragwie dzialaja dalej po wylaczeniu klucza
            rows.Add(new InquiryElement("reset", "Return to the game's own order", null, b != null && entries > 0,
                                        entries <= 0 ? "No order is set - the game already sorts your men by kind."
                                        : (!Writes ? "Army Review Writes Order Of Battle is off, but the banners saved before still set your next battle. " : "")
                                          + "Forget the banners: the next battle sorts your men by kind into the game's own formations, as if no order had ever been set."));
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Review the army", desc, rows, true, 1, 1, "Look", "Close",
                delegate (List<InquiryElement> picked)
                {
                    try
                    {
                        string id = picked != null && picked.Count > 0 ? picked[0].Identifier as string : null;
                        if (id == "men") { _dReports++; ShowPages("Your men", pagesMen, 0); }
                        else if (id == "horses") { _dReports++; ShowPages("Horses and harness", pagesHorses, 0); }
                        else if (id == "march") { _dReports++; ShowPages("On the march", pagesMarch, 0); }
                        else if (id == "banners") { _dBanners++; OpenBanners(true); }
                        else if (id == "reset") AskReset();
                    }
                    catch (Exception e) { Stumble("Main.Pick", e); }
                }, null), true);
        }

        /// <summary>Strony tekstu z powrotem do przegladu ("More" / "Back", na ostatniej "Back" / "Close").</summary>
        private static void ShowPages(string title, List<string> pages, int i)
        {
            if (pages == null || pages.Count == 0) { OpenMain(false); return; }
            if (i < 0 || i >= pages.Count) i = 0;
            bool more = i < pages.Count - 1;
            int k = i + 1;
            string t = pages.Count > 1 ? title + " (" + (i + 1) + "/" + pages.Count + ")" : title;
            InformationManager.ShowInquiry(new InquiryData(t, pages[i], true, true, more ? "More" : "Back", more ? "Back" : "Close",
                delegate { try { if (more) ShowPages(title, pages, k); else OpenMain(false); } catch (Exception e) { Stumble("Pages.Next", e); } },
                delegate { try { if (more) OpenMain(false); } catch (Exception e) { Stumble("Pages.Back", e); } }), true);
        }

        /// <summary>Tekst na strony okna (ramka okna nie przewija - Rumours.PageLines linii na strone, akapity rozdzielone pusta linia).</summary>
        internal static List<string> Paginate(string text)
        {
            var pages = new List<string>();
            var cur = new StringBuilder();
            int n = 0;
            foreach (var para in (text ?? "").Split(new[] { "\n\n" }, StringSplitOptions.None))
            {
                int need = Rumours.EstLines(para) + (cur.Length > 0 ? 1 : 0);
                if (cur.Length > 0 && n + need > Rumours.PageLines) { pages.Add(cur.ToString()); cur.Length = 0; n = 0; need = Rumours.EstLines(para); }
                if (cur.Length > 0) cur.Append("\n\n");
                cur.Append(para);
                n += need;
            }
            if (cur.Length > 0 || pages.Count == 0) pages.Add(cur.ToString());
            return pages;
        }

        // ------------------------------------------------------------ raport: ludzie, konie, marsz
        /// <summary>Jezdzcy, ktorzy w bitwie gracza pojda pieszo: bez konia w zbrojowni (regula "tylko to, co ma") i z koniem bez uprzezy (regula jezdzca E8/H).</summary>
        internal static int FootInBattle(SaddleLaw.PairState p)
        {
            if (p == null || !GarrisonKit.OwnKitOn) return 0;
            return p.NoHorse + (SaddleLaw.On ? p.NoSaddle : 0);
        }

        /// <summary>Jezdzcy-wyjatki (wzorzec bez uprzezy) z koniem - z tozsamosci PairState: NoHorse = (Riders - Mounted) + (Exempt - exMounted).</summary>
        internal static int ExemptMounted(SaddleLaw.PairState p)
        {
            return Math.Max(0, p.Exempt - (p.NoHorse - (p.Riders - p.Mounted)));
        }

        private static string MenText(MobileParty mp, int[] fit, int[] wnd, SaddleLaw.PairState p)
        {
            var sb = new StringBuilder();
            sb.Append("Fit to fight / wounded, by kind:");
            string[] names = { "Infantry", "Archers", "Cavalry", "Horse archers", "You and your companions" };
            for (int k = 0; k < 5; k++) sb.Append('\n').Append(names[k]).Append(": ").Append(fit[k]).Append(" / ").Append(wnd[k]);
            int foot = FootInBattle(p);
            if (foot > 0) sb.Append("\n\nOf your riders, ").Append(foot).Append(" will fight on foot - see Horses and harness.");
            int prisoners = mp.PrisonRoster != null ? mp.PrisonRoster.TotalManCount : 0;
            if (prisoners > 0) sb.Append("\n\nPrisoners in the column: ").Append(prisoners).Append('.');
            var army = mp.Army;
            if (army != null && army.LeaderParty == mp)
            {
                var all = CommandedFit();
                sb.Append("\n\nWith the army you lead: ").Append(all[0]).Append(" infantry, ").Append(all[1]).Append(" archers, ").Append(all[2]).Append(" cavalry, ")
                  .Append(all[3]).Append(" horse archers fit to fight - your banners share them out.");
            }
            sb.Append("\n\nA man's kind is his troop's: a rider without a horse is still counted as cavalry here and when the battle places the men in their banners.");
            return sb.ToString();
        }

        private static string HorseText(MobileParty mp, SaddleLaw.PairState p)
        {
            var sb = new StringBuilder();
            int riders = p.Riders + p.Exempt;
            int exM = ExemptMounted(p);
            int inUse = p.Mounted + exM;
            bool dte = true;
            try { dte = QuartermasterLaw.DteArmory() != null; } catch { dte = false; }
            sb.Append("Riders: ").Append(riders).Append(" (wounded included)");
            if (p.Exempt > 0) sb.Append(" - ").Append(p.Exempt).Append(" of them ride without harness by their kind");
            sb.Append('.');
            sb.Append("\nHorses in the troops' armoury: ").Append(p.Horses).Append(" - ").Append(inUse).Append(" in use, ").Append(Math.Max(0, p.Horses - inUse)).Append(" spare.");
            sb.Append("\nHarness in the troops' armoury: ").Append(p.Harness).Append(" - ").Append(p.Paired).Append(" in use, ").Append(p.FreeHarness).Append(" spare");
            if (p.HarnessOther > 0) sb.Append(" (and ").Append(p.HarnessOther).Append(" for camels and other beasts)");
            sb.Append('.');
            var bag = mp.ItemRoster;
            int bagHorses = bag != null ? bag.NumberOfMounts : 0;
            int bagHarness = SaddleLaw.BagFit(bag);
            sb.Append("\nIn your baggage: ").Append(bagHorses).Append(" spare horses, ").Append(bagHarness).Append(" harness.");
            if (!dte) sb.Append("\n\nThe troops' armoury (Dynamic Troop Equipment) cannot be read now - the numbers above may be empty.");
            sb.Append("\n\n");
            if (!GarrisonKit.OwnKitOn)
                sb.Append("Riders take their horses from their troop kind (Troops Fight With Own Kit Only is off) - none goes on foot for want of a horse.");
            else
            {
                int noSaddle = SaddleLaw.On ? p.NoSaddle : 0;
                int foot = p.NoHorse + noSaddle;
                if (foot <= 0) sb.Append("Every rider has a horse").Append(SaddleLaw.On ? " and a harness" : "").Append(" - none will fight on foot.");
                else
                {
                    sb.Append(foot).Append(foot == 1 ? " rider will fight on foot: " : " riders will fight on foot: ");
                    var why = new List<string>();
                    if (p.NoHorse > 0) why.Add(p.NoHorse + " have no horse in the troops' armoury");
                    if (noSaddle > 0) why.Add(noSaddle + " have a horse but no harness");
                    sb.Append(string.Join(", ", why.ToArray())).Append('.');
                }
                sb.Append(SaddleLaw.On ? "\nA rider needs a horse and a harness from the troops' armoury." : "\nHarness is not needed (Rider Needs Harness is off) - a horse from the troops' armoury is enough.");
                if (bagHorses > 0 || bagHarness > 0) sb.Append(" Your riders are mounted from the troops' armoury; spare horses in your baggage carry footmen on the march.");
            }
            sb.Append("\n\nIn sieges and hideouts every rider fights on foot.");
            return sb.ToString();
        }

        private static string MarchRow(MarchPace.Column col)
        {
            if (col == null) return "On the march: the column rule is off";
            string what = col.Kind == 0 ? col.N + " afoot - the column walks" : col.Kind == 2 ? col.N + " footmen on spare horses" : col.Kind == 3 ? "the baggage train sets the pace" : "all riders";
            return "On the march: " + what + " (at most " + Num(col.Cap) + ")";
        }

        private static string MarchText(MobileParty mp, MarchPace.Column col, SaddleLaw.PairState p)
        {
            var c = Settings.Current;
            if (col == null || c == null)
                return "The marching column rule is off (March Pace Enabled), or your party is not on land - it moves at the game's own pace.";
            var sb = new StringBuilder();
            sb.Append("Your column: ").Append(col.Men).Append(col.Men == 1 ? " man" : " men");
            if (mp.AttachedParties != null && mp.AttachedParties.Count > 0) sb.Append(" (with the parties of your army)");
            sb.Append(", ").Append(col.Foot).Append(" of them without a horse of their own, ").Append(col.Prisoners).Append(" prisoners on the rope.");
            sb.Append("\nSpare horses in your baggage: ").Append(col.Mounts);
            if (col.K0 && col.Armory > 0) sb.Append(", and ").Append(col.Armory).Append(" free horses in the armouries of your army's parties");
            sb.Append(". Spare horses carry the footmen first, then the prisoners.");
            sb.Append("\n\n");
            string capText = Num(col.Cap) + " on the map (" + Num(col.CapRaw) + " at full pace x " + Math.Max(5, Math.Min(100, c.WorldPacePercent)) + "% world pace)";
            if (col.Kind == 0)
            {
                int footWalk = col.Foot - col.RidingFoot;
                int prisWalk = col.Walkers - footWalk;
                sb.Append("Marching column: ").Append(col.N).Append(" afoot - ");
                var parts = new List<string>();
                if (footWalk > 0) parts.Add(footWalk + (footWalk == 1 ? " footman" : " footmen"));
                if (prisWalk > 0) parts.Add(prisWalk + (prisWalk == 1 ? " prisoner" : " prisoners"));
                sb.Append(string.Join(" and ", parts.ToArray())).Append(" walk, so the whole column keeps the walking pace: at most ").Append(capText).Append('.');
                int need = col.Walkers;
                sb.Append("\nWith ").Append(need).Append(need == 1 ? " more spare horse" : " more spare horses").Append(" in the baggage nobody would walk.");
            }
            else if (col.Kind == 2)
                sb.Append("Marching column: ").Append(col.N).Append(" footmen on spare horses - a man in the saddle is not a horseman born: at most ").Append(capText).Append('.');
            else if (col.Kind == 3)
                sb.Append("Marching column: baggage train - ").Append(col.Train).Append(" pack animals and livestock, more than the ").Append(col.Allowance)
                  .Append(" that go free with ").Append(col.Men).Append(" men: at most ").Append(capText).Append('.');
            else
                sb.Append("Marching column: all riders - at most ").Append(capText).Append('.');
            if (!col.Applies) sb.Append(" (So slow a cap is not applied - the game's own pace holds.)");
            float now = -1f;
            try { if (mp.IsActive) now = mp.Speed; } catch { now = -1f; }
            if (now >= 0f)
            {
                sb.Append("\nYour party moves at ").Append(Num(now)).Append(" now");
                if (col.Applies && now < col.Cap - 0.05f) sb.Append(" - other things (ground, weight, wounded, weather) hold it below the column's cap");
                sb.Append('.');
            }
            sb.Append("\n\nIt changes with every prisoner taken or freed, every recruit, every horse bought, sold or moved between the baggage and the troops' armoury - that is why the column walks one day and rides the next.");
            if (!col.K0)
                sb.Append(" Free horses in the troops' armoury do not carry footmen (Spare Horses Carry Footmen is off).");
            else if (p != null && p.Horses - (p.Mounted + ExemptMounted(p)) > 0)
                sb.Append(" The ").Append(p.Horses - (p.Mounted + ExemptMounted(p))).Append(" free horses in your own troops' armoury do not carry your footmen on the march - only spare horses in your baggage do.");
            return sb.ToString();
        }

        /// <summary>Linia logu raportu (na otwarcie okna i w tescie): te same liczby co strony okna.</summary>
        internal static string ReportLine(MobileParty mp, int[] fit, int[] wnd, SaddleLaw.PairState p, MarchPace.Column col, List<string> pm, List<string> ph, List<string> pmarch)
        {
            int exM = ExemptMounted(p);
            var bag = mp.ItemRoster;
            var sb = new StringBuilder(P);
            sb.Append("raport - jezdzcy ").Append(p.Riders).Append(" (wyjatki ").Append(p.Exempt).Append(", wyjatki z koniem ").Append(exM).Append("), konie w zbrojowni ").Append(p.Horses)
              .Append(" (w uzyciu ").Append(p.Mounted + exM).Append("), uprzaz ").Append(p.Harness).Append(" (inna rodzina ").Append(p.HarnessOther).Append("), z para ").Append(p.Paired)
              .Append(", kon bez uprzezy ").Append(p.NoSaddle).Append(", bez konia ").Append(p.NoHorse).Append(", wolna uprzaz ").Append(p.FreeHarness)
              .Append(" | tabor: luzaki ").Append(bag != null ? bag.NumberOfMounts : 0).Append(", uprzaz ").Append(SaddleLaw.BagFit(bag))
              .Append(" | pieszo w bitwie ").Append(FootInBattle(p)).Append(" (tylko to, co ma ").Append(Yes(GarrisonKit.OwnKitOn)).Append(", regula uprzezy ").Append(Yes(SaddleLaw.On)).Append(')');
            if (col == null) sb.Append(" | marsz: kolumna wylaczona albo poza regula");
            else
                sb.Append(" | marsz: ludzi ").Append(col.Men).Append(", bez konia ").Append(col.Foot).Append(", jencow ").Append(col.Prisoners).Append(", luzakow ").Append(col.Mounts)
                  .Append(", konie zbrojowni K0 ").Append(col.Armory).Append(" (K0 ").Append(Yes(col.K0)).Append("), na luzakach ").Append(col.RidingFoot).Append(", idacych ").Append(col.Walkers)
                  .Append(", sufit ").Append(Num(col.Cap)).Append(" (rodzaj ").Append(col.Kind).Append(", n=").Append(col.N).Append(", czynny ").Append(Yes(col.Applies)).Append(')');
            sb.Append(" | zdrowi/ranni: piechota ").Append(fit[0]).Append('/').Append(wnd[0]).Append(", lucznicy ").Append(fit[1]).Append('/').Append(wnd[1])
              .Append(", konni ").Append(fit[2]).Append('/').Append(wnd[2]).Append(", konni lucznicy ").Append(fit[3]).Append('/').Append(wnd[3])
              .Append(", bohaterowie ").Append(fit[4]).Append('/').Append(wnd[4]);
            sb.Append(" | strony ").Append(PagesInfo(pm)).Append(' ').Append(PagesInfo(ph)).Append(' ').Append(PagesInfo(pmarch)).Append(" (miesci sie ").Append(Rumours.PageLines).Append(')');
            sb.Append(" | ").Append(OobState()).Append('.');
            return sb.ToString();
        }

        /// <summary>"strony/najdluzsza" stron jednej czesci raportu.</summary>
        internal static string PagesInfo(List<string> pages)
        {
            int mx = 0;
            if (pages != null) foreach (var s in pages) mx = Math.Max(mx, Rumours.EstLines(s));
            return (pages != null ? pages.Count : 0) + "x" + mx;
        }

        internal static int LongestPage(List<string> pages)
        {
            int mx = 0;
            if (pages != null) foreach (var s in pages) mx = Math.Max(mx, Rumours.EstLines(s));
            return mx;
        }

        // ------------------------------------------------------------ choragwie: odczyt i zapis
        /// <summary>Zapis gry jako choragwie (null - brak zachowania OOB). saved - jest czynny porzadek (8 wpisow i choc jedna formacja z klasa);
        /// entries - ile wpisow ma lista (spawn gry uzywa jej tylko przy 8).</summary>
        internal static Slot[] ReadSlots(out bool saved, out int entries)
        {
            saved = false; entries = 0;
            var b = Behavior();
            var mp = MobileParty.MainParty;
            if (b == null || mp == null) return null;
            bool army = mp.Army != null;
            var r = new Slot[Slots];
            for (int i = 0; i < Slots; i++)
            {
                var d = b.GetFormationDataAtIndex(i, false, army);
                if (d == null) continue;
                entries++;
                int cls = (int)d.FormationClass;
                if (cls < 1 || cls > 6 || (d.PrimaryClassWeight <= 0 && d.SecondaryClassWeight <= 0)) continue;
                var s = new Slot { Cls = cls, W1 = d.PrimaryClassWeight, W2 = d.SecondaryClassWeight, Men = KeepShare, Captain = d.Captain };
                if (d.Filters != null) foreach (var kv in d.Filters) { int f = (int)kv.Key; if (f >= 1 && f <= 6) s.Filt[f] = kv.Value; }
                if (d.HeroTroops != null) foreach (var h in d.HeroTroops) if (h != null && !s.Ranks.Contains(h)) s.Ranks.Add(h);
                KeyValuePair<int, string> nm;
                if (_names.TryGetValue(i, out nm) && nm.Key == cls) s.Name = nm.Value;
                r[i] = s;
            }
            saved = entries >= Slots && Array.Exists(r, x => x != null);
            return r;
        }

        /// <summary>Porzadek gry bez zapisu: kazdy obecny rodzaj w swojej formacji (0 piechota, 1 lucznicy, 2 konni, 3 konni lucznicy), cala klasa.</summary>
        private static Slot[] DefaultSlots()
        {
            var r = new Slot[Slots];
            var fit = CommandedFit();
            for (int k = 1; k <= 4; k++) if (fit[k - 1] > 0) r[k - 1] = new Slot { Cls = k, Men = TheRest };
            return r;
        }

        private static bool Covers(Slot s, int k)
        {
            if (s == null) return false;
            if (s.Cls == k) return true;
            if (s.Cls == 5) return k == 1 || k == 2;
            if (s.Cls == 6) return k == 3 || k == 4;
            return false;
        }

        private static int FirstFree(Slot[] s) { for (int i = 0; i < Slots; i++) if (s[i] == null) return i; return -1; }

        private static bool FirstOfClass(Slot[] s, int idx, int k)
        {
            for (int i = 0; i < Slots; i++) if (i != idx && s[i] != null && s[i].Cls == k) return false;
            return true;
        }

        /// <summary>Wagi choragwi: kazda klasa 1..4 sumuje sie do 100 (minus udzial formacji mieszanych z zapisu gry), kazda choragiew co najmniej 1;
        /// "the rest" dzieli reszte po rowno, a gdy zadnej - reszte bierze pierwsza choragiew klasy. Liczba ludzi -> procent zdrowych tej klasy.</summary>
        internal static void Weights(Slot[] s, int[] fit, int[] w1, int[] w2)
        {
            for (int i = 0; i < Slots; i++)
            {
                w1[i] = 0; w2[i] = 0;
                if (s[i] != null && s[i].Cls >= 5) { w1[i] = Math.Max(0, Math.Min(100, s[i].W1)); w2[i] = Math.Max(0, Math.Min(100, s[i].W2)); }
            }
            for (int k = 1; k <= 4; k++)
            {
                int fixd = 0;
                var basic = new List<int>();
                for (int i = 0; i < Slots; i++)
                {
                    var x = s[i];
                    if (x == null) continue;
                    if (x.Cls == k) basic.Add(i);
                    else if (x.Cls == 5) { if (k == 1) fixd += w1[i]; else if (k == 2) fixd += w2[i]; }
                    else if (x.Cls == 6) { if (k == 3) fixd += w1[i]; else if (k == 4) fixd += w2[i]; }
                }
                if (basic.Count == 0) continue;
                int avail = Math.Max(basic.Count, 100 - fixd);
                int h = Math.Max(1, fit[k - 1]);
                var rest = new List<int>();
                var want = new List<KeyValuePair<int, float>>();
                foreach (int i in basic)
                {
                    if (s[i].Men == TheRest) rest.Add(i);
                    else want.Add(new KeyValuePair<int, float>(i, s[i].Men >= 0 ? 100f * s[i].Men / h : Math.Max(0, s[i].W1)));
                }
                if (rest.Count == 0) { rest.Add(basic[0]); want.RemoveAll(kv => kv.Key == basic[0]); }
                float sum = 0f;
                foreach (var kv in want) sum += kv.Value;
                int budget = avail - rest.Count;
                float f = sum > budget && sum > 0f ? budget / sum : 1f;
                int used = 0;
                foreach (var kv in want) { int v = Math.Max(1, (int)Math.Round(kv.Value * f)); w1[kv.Key] = v; used += v; }
                while (used > budget)
                {
                    int m = -1;
                    foreach (var kv in want) if (w1[kv.Key] > 1 && (m < 0 || w1[kv.Key] > w1[m])) m = kv.Key;
                    if (m < 0) break;
                    w1[m]--; used--;
                }
                int left = Math.Max(rest.Count, avail - used);
                int each = left / rest.Count, extra = left - each * rest.Count;
                for (int j = 0; j < rest.Count; j++) w1[rest[j]] = each + (j < extra ? 1 : 0);
            }
        }

        /// <summary>Bohater wazny w zapisie: zywy, wolny, w druzynie gracza (gracz - tylko jako dowodca).</summary>
        private static bool HeroOk(Hero h, bool allowMain)
        {
            if (h == null || !h.IsAlive || h.IsPrisoner) return false;
            if (h == Hero.MainHero) return allowMain;
            return h.PartyBelongedTo == MobileParty.MainParty;
        }

        private static Dictionary<FormationFilterType, bool> FiltersOf(Slot s)
        {
            bool on = s != null;
            return new Dictionary<FormationFilterType, bool>
            {
                [FormationFilterType.Shield] = on && s.Filt[1],
                [FormationFilterType.Spear] = on && s.Filt[2],
                [FormationFilterType.Thrown] = on && s.Filt[3],
                [FormationFilterType.Heavy] = on && s.Filt[4],
                [FormationFilterType.HighTier] = on && s.Filt[5],
                [FormationFilterType.LowTier] = on && s.Filt[6]
            };
        }

        /// <summary>Wynik zapisu (do okna, logu i testu).</summary>
        internal sealed class WriteResult
        {
            public bool Ok, AsReset, PinCommitted;
            public int PinTypes, PinMen, PinDropped;
            public string Error;
            public readonly List<int> Auto = new List<int>();
            public readonly int[] W1 = new int[Slots], W2 = new int[Slots];
            public bool ReadField, ReadArmy;
            public int Captains, Ranks, Dropped;
            public Slot[] Banners;
        }

        /// <summary>
        /// Zapis choragwi do Order of Battle gry: walidacja rodzajow (brakujacy -> "the rest" w wolnym indeksie, 8 zajetych -> odmowa), wagi, bohaterowie,
        /// 8 wpisow dla pola i dla armii (osobne obiekty), odczyt kontrolny, nazwy. Wszystkie choragwie puste = powrot do porzadku gry.
        /// </summary>
        internal static WriteResult Write(Slot[] work) { return Write(work, null); }

        /// <summary>Zapis choragwi; pins != null - takze przypiecia typow (CommitPins: zapisane zastepuje kopia bez przypiec do zwinietych choragwi),
        /// a udzialy licza tylko nieprzypietych (ShareFit). pins == null - przypiecia zapisane bez zmian.</summary>
        internal static WriteResult Write(Slot[] work, Dictionary<string, int> pins)
        {
            var res = new WriteResult();
            var b = Behavior();
            if (b == null) { res.Error = "The game's Order of Battle is not running in this campaign."; return res; }
            var s = new Slot[Slots];
            for (int i = 0; i < Slots; i++) s[i] = work != null && work[i] != null && work[i].Cls >= 1 && work[i].Cls <= 6 ? work[i].Clone() : null;
            var fit = CommandedFit();
            for (int k = 1; k <= 4; k++)
            {
                if (fit[k - 1] <= 0) continue;
                bool covered = false;
                for (int i = 0; i < Slots; i++) if (Covers(s[i], k)) { covered = true; break; }
                if (covered) continue;
                int idx = FirstFree(s);
                if (idx < 0)
                {
                    res.Error = "Every kind of troop needs a banner: you have " + fit[k - 1] + " " + KindLower(k) + " but no banner for them, and all 8 banners are raised."
                                + " Strike one first.";
                    return res;
                }
                s[idx] = new Slot { Cls = k, Men = TheRest, Auto = true };
                res.Auto.Add(idx);
            }
            if (!Array.Exists(s, x => x != null))
            {
                int had = pins != null ? pins.Count : _pins.Count;
                ResetOrder();
                res.Ok = true; res.AsReset = true; res.Banners = s; res.PinDropped = had; res.PinCommitted = true;
                return res;
            }
            // P12: udzialy z nieprzypietych - przypieci do wystawionych choragwi stoja ponad udzialem (walidacja rodzajow wyzej na wszystkich);
            // klucz przypiec NIE - wszyscy (recenzja P12: w bitwie przypieci ida wtedy wedlug rodzaju; PinnedFit z null = zera)
            var share = (int[])fit.Clone();
            var pinnedFit = PinnedFit(PinOn ? (pins ?? _pins) : null, s, null);
            for (int k = 0; k < 4; k++) share[k] = Math.Max(0, share[k] - pinnedFit[k]);
            Weights(s, share, res.W1, res.W2);
            // bohaterowie: dowodcy najpierw (wedlug indeksu), potem szeregi; kazdy najwyzej raz
            var used = new HashSet<Hero>();
            var caps = new Hero[Slots];
            var ranks = new List<Hero>[Slots];
            for (int i = 0; i < Slots; i++)
            {
                ranks[i] = new List<Hero>();
                if (s[i] == null || s[i].Captain == null) continue;
                if (HeroOk(s[i].Captain, true) && used.Add(s[i].Captain)) { caps[i] = s[i].Captain; res.Captains++; }
                else res.Dropped++;
            }
            for (int i = 0; i < Slots; i++)
            {
                if (s[i] == null) continue;
                foreach (var h in s[i].Ranks)
                {
                    if (HeroOk(h, false) && used.Add(h)) { ranks[i].Add(h); res.Ranks++; }
                    else res.Dropped++;
                }
            }
            var field = new List<OrderOfBattleCampaignBehavior.OrderOfBattleFormationData>(Slots);
            var army = new List<OrderOfBattleCampaignBehavior.OrderOfBattleFormationData>(Slots);
            for (int i = 0; i < Slots; i++)
            {
                var x = s[i];
                var cls = x != null ? (DeploymentFormationClass)x.Cls : DeploymentFormationClass.Unset;
                field.Add(new OrderOfBattleCampaignBehavior.OrderOfBattleFormationData(caps[i], ranks[i].ToArray(), cls, res.W1[i], res.W2[i], FiltersOf(x)));
                army.Add(new OrderOfBattleCampaignBehavior.OrderOfBattleFormationData(caps[i], ranks[i].ToArray(), cls, res.W1[i], res.W2[i], FiltersOf(x)));
            }
            b.SetFormationInfos(field, false, false);
            b.SetFormationInfos(army, false, true);
            res.ReadField = ReadBack(b, false, s, caps, res);
            res.ReadArmy = ReadBack(b, true, s, caps, res);
            // nazwy: tylko choragwie z nazwa, z klasa w chwili zapisu
            _names.Clear();
            for (int i = 0; i < Slots; i++)
            {
                if (s[i] == null) continue;
                s[i].W1 = res.W1[i]; s[i].W2 = res.W2[i]; s[i].Men = s[i].Men == TheRest ? TheRest : KeepShare;
                s[i].Captain = caps[i];
                s[i].Ranks.Clear(); s[i].Ranks.AddRange(ranks[i]);
                if (!string.IsNullOrEmpty(s[i].Name)) _names[i] = new KeyValuePair<int, string>(s[i].Cls, s[i].Name);
            }
            res.Banners = s;
            res.Ok = true;
            CommitPins(res, pins);
            return res;
        }

        private static bool ReadBack(OrderOfBattleCampaignBehavior b, bool army, Slot[] s, Hero[] caps, WriteResult res)
        {
            for (int i = 0; i < Slots; i++)
            {
                var d = b.GetFormationDataAtIndex(i, false, army);
                if (d == null) return false;
                int cls = s[i] != null ? s[i].Cls : 0;
                if ((int)d.FormationClass != cls || d.PrimaryClassWeight != res.W1[i] || d.SecondaryClassWeight != res.W2[i] || d.Captain != caps[i]) return false;
                var want = FiltersOf(s[i]);
                foreach (var kv in want) { bool v; if (d.Filters == null || !d.Filters.TryGetValue(kv.Key, out v) || v != kv.Value) return false; }
            }
            return b.GetFormationDataAtIndex(Slots, false, army) == null;
        }

        /// <summary>Powrot do porzadku gry: puste listy pola i armii (gra rozstawia wedlug rodzaju), nazwy wyczyszczone. true - odczyt null x 16.</summary>
        internal static bool ResetOrder()
        {
            var b = Behavior();
            if (b == null) return false;
            b.SetFormationInfos(new List<OrderOfBattleCampaignBehavior.OrderOfBattleFormationData>(), false, false);
            b.SetFormationInfos(new List<OrderOfBattleCampaignBehavior.OrderOfBattleFormationData>(), false, true);
            _names.Clear();
            _pins.Clear();   // P12: przypiecia potrzebuja choragwi
            for (int i = 0; i < Slots; i++)
                if (b.GetFormationDataAtIndex(i, false, false) != null || b.GetFormationDataAtIndex(i, false, true) != null) return false;
            return true;
        }

        /// <summary>Linia logu zapisu (okno i test).</summary>
        internal static string WriteLine(string who, WriteResult r)
        {
            if (!r.Ok) return P + who + " - odmowa zapisu: " + r.Error;
            if (r.AsReset) return P + who + " - wszystkie choragwie zwiniete: powrot do porzadku gry (listy pola i armii puste), przypiecia zdjete " + r.PinDropped + ".";
            var sb = new StringBuilder(P).Append(who).Append(" - zapis Order of Battle:");
            for (int i = 0; i < Slots; i++)
            {
                var x = r.Banners[i];
                if (x == null) continue;
                sb.Append(' ').Append(Roman[i]).Append(' ').Append(KindName(x.Cls)).Append(" w").Append(r.W1[i]);
                if (x.Cls >= 5) sb.Append('/').Append(r.W2[i]);
                for (int f = 1; f <= 6; f++) if (x.Filt[f]) sb.Append(" +").Append(((FormationFilterType)f).ToString());
                if (x.Captain != null) sb.Append(" kap. ").Append(x.Captain.Name);
                if (x.Ranks.Count > 0) sb.Append(" szereg ").Append(x.Ranks.Count);
                if (x.Auto) sb.Append(" (dopisana)");
                sb.Append(';');
            }
            sb.Append(" odczyt pole ").Append(Yes(r.ReadField)).Append(", armia ").Append(Yes(r.ReadArmy)).Append(" | dopisane ").Append(r.Auto.Count)
              .Append(" | bohaterowie: dowodcow ").Append(r.Captains).Append(", w szeregach ").Append(r.Ranks).Append(", odrzuconych ").Append(r.Dropped)
              .Append(" | nazwy ").Append(_names.Count)
              .Append(" | przypiecia ").Append(r.PinCommitted ? "zapisane: typow " + r.PinTypes + ", zdrowych ludzi " + r.PinMen + ", odpadly (choragiew zwinieta) " + r.PinDropped
                                                               : "bez zmian (" + _pins.Count + ")").Append('.');
            return sb.ToString();
        }

        // ------------------------------------------------------------ okno 2: lista choragwi
        private static void OpenBanners(bool fresh)
        {
            string why;
            if (!Can(out why)) { Log.Player(why, true); return; }
            if (Behavior() == null) { Log.Player("The game's Order of Battle is not running in this campaign.", true); return; }
            if (fresh || _work == null)
            {
                bool saved; int entries;
                var cur = ReadSlots(out saved, out entries);
                _fromGame = !saved;
                _work = saved ? cur : DefaultSlots();
                _workPins = new Dictionary<string, int>(_pins, StringComparer.Ordinal);
                _dirty = false;
            }
            if (_workPins == null) _workPins = new Dictionary<string, int>(_pins, StringComparer.Ordinal);
            var fit = ShareFit(_work, _workPins);   // P12: udzialy z nieprzypietych
            var w1 = new int[Slots]; var w2 = new int[Slots];
            Weights(_work, fit, w1, w2);
            var rows = new List<InquiryElement>();
            int raised = 0;
            for (int i = 0; i < Slots; i++)
            {
                if (_work[i] == null) continue;
                raised++;
                rows.Add(new InquiryElement(i, BannerTitle(i, fit, w1), null, true, BannerHint(i, fit, w1, w2)));
            }
            bool writes = Writes;
            int placed = 0;
            foreach (var kv in _workPins) if (kv.Value >= 0 && kv.Value < Slots && _work[kv.Value] != null) placed++;
            rows.Add(new InquiryElement("pins", "Move troop types into banners" + (placed > 0 && PinOn ? " (" + placed + (placed == 1 ? " type placed)" : " types placed)") : ""), null, PinEdit && raised > 0,
                                        !PinOn ? "Army Review Pin Troop Types is off - banners take men by kind, share and filters only."
                                        : !writes ? "Army Review Writes Order Of Battle is off - the review only shows the game's order."
                                        : raised == 0 ? "Raise a banner first."
                                        : "Pick troop types of your own party and the banner they stand in: they open the battle there and their reinforcements join it."));
            rows.Add(new InquiryElement("raise", "Raise a new banner", null, writes && raised < Slots,
                                        !writes ? "Army Review Writes Order Of Battle is off - the review only shows the game's order."
                                                : raised >= Slots ? "All 8 banners are raised - the game has no more formations." : "A new banner in the first free formation."));
            rows.Add(new InquiryElement("save", "Save the banners for the next battle", null, writes,
                                        writes ? "Write the banners into the game's Order of Battle. A kind of troop with no banner gets one of its own (the rest)."
                                               : "Army Review Writes Order Of Battle is off - the review only shows the game's order."));
            var missing = new List<string>();
            var all = CommandedFit();
            for (int k = 1; k <= 4; k++)
            {
                if (all[k - 1] <= 0) continue;
                bool cov = false;
                for (int i = 0; i < Slots; i++) if (Covers(_work[i], k)) { cov = true; break; }
                if (!cov) missing.Add(KindLower(k));
            }
            var d = new StringBuilder("Each banner is one of the game's 8 formations; your men take their places in them when a field battle opens, and when you command 20 men or more the Order of Battle screen shows them before the fight.");
            d.Append(' ').Append(!writes ? "The review only shows the game's order (Army Review Writes Order Of Battle is off)."
                               : _fromGame && !_dirty ? "No order is set: this is how the game sorts your men by kind."
                               : _dirty ? "You have changes not yet saved." : "This is the order of your next battle.");
            if (missing.Count > 0) d.Append(" Your ").Append(string.Join(", ", missing.ToArray())).Append(missing.Count == 1 ? " has" : " have").Append(" no banner yet - one will be raised on saving.");
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Banners for the next battle", d.ToString(), rows, true, 1, 1, "Choose", "Back",
                delegate (List<InquiryElement> picked)
                {
                    try
                    {
                        object id = picked != null && picked.Count > 0 ? picked[0].Identifier : null;
                        if (id is int) OpenBanner((int)id);
                        else if ((id as string) == "pins") OpenPins();
                        else if ((id as string) == "raise") Raise();
                        else if ((id as string) == "save") SaveWork();
                    }
                    catch (Exception e) { Stumble("Banners.Pick", e); }
                },
                delegate (List<InquiryElement> _) { try { LeaveBanners(); } catch (Exception e) { Stumble("Banners.Back", e); } }), true);
        }

        private static string Approx(int i, int[] fit, int[] w1)
        {
            var s = _work[i];
            if (s == null) return "";
            if (s.Cls >= 5) return "share " + s.W1 + "% / " + s.W2 + "%";
            int men = (int)Math.Round(w1[i] * fit[s.Cls - 1] / 100f);
            return "~" + men + (TakesRest(_work, i) ? " (the rest)" : "");
        }

        /// <summary>Choragiew bierze reszte rodzaju: ustawiona na "the rest" albo pierwsza choragiew rodzaju, gdy zadna inna nie bierze reszty (zasada Weights).</summary>
        private static bool TakesRest(Slot[] w, int i)
        {
            var s = w[i];
            if (s == null || s.Cls < 1 || s.Cls > 4) return false;
            if (s.Men == TheRest) return true;
            for (int j = 0; j < Slots; j++)
            {
                if (w[j] == null || w[j].Cls != s.Cls) continue;
                if (w[j].Men == TheRest) return false;
                if (j < i) return false;
            }
            return true;
        }

        /// <summary>Pierwsza inna choragiew tego samego rodzaju (-1 - brak).</summary>
        private static int FirstOtherOfClass(Slot[] w, int idx, int k)
        {
            for (int j = 0; j < Slots; j++) if (j != idx && w[j] != null && w[j].Cls == k) return j;
            return -1;
        }

        /// <summary>Inna choragiew tego samego rodzaju ma ustawione "the rest".</summary>
        private static bool RestElsewhere(Slot[] w, int idx, int k)
        {
            for (int j = 0; j < Slots; j++) if (j != idx && w[j] != null && w[j].Cls == k && w[j].Men == TheRest) return true;
            return false;
        }

        private static string Label(int i)
        {
            var s = _work[i];
            if (s == null) return Roman[i];
            return Roman[i] + ". " + (!string.IsNullOrEmpty(s.Name) ? s.Name + " - " + KindName(s.Cls) : KindName(s.Cls));
        }

        private static string BannerTitle(int i, int[] fit, int[] w1)
        {
            var s = _work[i];
            var sb = new StringBuilder(Label(i)).Append(' ').Append(Approx(i, fit, w1)).Append(PinnedTail(i));
            var fl = new List<string>();
            for (int f = 1; f <= 6; f++) if (s.Filt[f]) fl.Add(FilterNames[f]);
            if (fl.Count > 0) sb.Append(" (").Append(string.Join(", ", fl.ToArray())).Append(')');
            if (s.Captain != null) sb.Append(" - led by ").Append(s.Captain == Hero.MainHero ? "you" : s.Captain.Name.ToString());
            return sb.ToString();
        }

        private static string BannerHint(int i, int[] fit, int[] w1, int[] w2)
        {
            var s = _work[i];
            var l = new List<string>();
            l.Add("Kind: " + KindName(s.Cls) + (s.Cls >= 5 ? " (a banner of two kinds, kept as the game has it)" : ""));
            if (s.Cls >= 1 && s.Cls <= 4)
            {
                int h = fit[s.Cls - 1];
                int men = (int)Math.Round(w1[i] * h / 100f);
                l.Add("Men: about " + men + " now - " + w1[i] + "% of your " + h + " fit " + KindLower(s.Cls) + (PinOn && _workPins != null && _workPins.Count > 0 ? " not placed by type" : "") + (TakesRest(_work, i) ? (s.Men == TheRest ? " (the rest of them)" : " (the first banner of its kind takes whatever the others leave)") : "")
                      + ". A banner keeps its share, not a count: it grows and shrinks with your " + KindLower(s.Cls) + ".");
                if (h == 0) l.Add("You have no fit " + KindLower(s.Cls) + " now.");
            }
            var fl = new List<string>();
            for (int f = 1; f <= 6; f++) if (s.Filt[f]) fl.Add(FilterNames[f]);
            l.Add("Filled first with: " + (fl.Count > 0 ? string.Join(", ", fl.ToArray()) : "any man of its kind"));
            l.Add("Commander: " + (s.Captain == null ? "none" : HeroLine(s.Captain)));
            if (s.Ranks.Count > 0)
            {
                var n = new List<string>();
                foreach (var h in s.Ranks) n.Add(h.Name.ToString());
                l.Add("In the ranks: " + string.Join(", ", n.ToArray()));
            }
            var pn = PinOn ? PinNames(_workPins, i) : new List<string>();   // klucz przypiec NIE - przypieci ida wedlug rodzaju
            if (pn.Count > 0) l.Add("Troop types placed here (" + PinnedIn(i) + " fit men, on top of the share): " + string.Join(", ", pn.ToArray()));
            if (s.Auto) l.Add("Raised for a kind of troop that had no banner.");
            return string.Join("\n", l.ToArray());
        }

        private static string HeroLine(Hero h)
        {
            string n = h == Hero.MainHero ? "you" : h.Name.ToString();
            if (!h.IsAlive) return n + " (dead - will be dropped)";
            if (h.IsPrisoner) return n + " (a prisoner - will be dropped)";
            if (h != Hero.MainHero && h.PartyBelongedTo != MobileParty.MainParty) return n + " (not in your party - will be dropped)";
            return n + (h.IsWounded ? " (wounded - will not lead)" : "");
        }

        private static void LeaveBanners()
        {
            if (_dirty && Writes)
            {
                InformationManager.ShowInquiry(new InquiryData("Banners for the next battle", "Your changes to the banners are not saved yet.", true, true, "Save them", "Discard",
                    delegate { try { SaveWork(); } catch (Exception e) { Stumble("Leave.Save", e); } },
                    delegate { try { _work = null; _workPins = null; _dirty = false; OpenMain(false); } catch (Exception e) { Stumble("Leave.Discard", e); } }), true);
                return;
            }
            _work = null; _workPins = null; _dirty = false;
            OpenMain(false);
        }

        private static void Raise()
        {
            int idx = FirstFree(_work);
            if (idx < 0) { OpenBanners(false); return; }
            var fit = CommandedFit();
            int k = 0;
            for (int c = 1; c <= 4 && k == 0; c++)
            {
                if (fit[c - 1] <= 0) continue;
                bool cov = false;
                for (int i = 0; i < Slots; i++) if (Covers(_work[i], c)) { cov = true; break; }
                if (!cov) k = c;
            }
            if (k == 0) k = 1;
            _work[idx] = new Slot { Cls = k, Men = FirstOfClass(_work, idx, k) ? TheRest : DefaultMen(fit[k - 1]) };
            _dirty = true;
            OpenBanner(idx);
        }

        /// <summary>Druga i dalsza choragiew rodzaju: tyle ludzi co kompania choragwi w bitwie (BannerCompanySize), najwyzej polowa zdrowych.</summary>
        private static int DefaultMen(int fit)
        {
            var c = Settings.Current;
            int size = c != null ? Math.Max(10, c.BannerCompanySize) : 50;
            if (fit <= 1) return TheRest;
            return Math.Max(1, Math.Min(size, fit / 2));
        }

        // ------------------------------------------------------------ okno 3: jedna choragiew
        private static void OpenBanner(int i)
        {
            if (_work == null || i < 0 || i >= Slots || _work[i] == null) { OpenBanners(false); return; }
            var s = _work[i];
            var fit = ShareFit(_work, _workPins);   // P12
            var w1 = new int[Slots]; var w2 = new int[Slots];
            Weights(_work, fit, w1, w2);
            if (!Writes)
            {
                InformationManager.ShowInquiry(new InquiryData(Label(i), BannerHint(i, fit, w1, w2), true, false, "Back", null,
                    delegate { try { OpenBanners(false); } catch (Exception e) { Stumble("Banner.ReadOnly", e); } }, null), true);
                return;
            }
            bool basic = s.Cls >= 1 && s.Cls <= 4;
            var fl = new List<string>();
            for (int f = 1; f <= 6; f++) if (s.Filt[f]) fl.Add(FilterNames[f]);
            var rows = new List<InquiryElement>
            {
                new InquiryElement("kind", "Kind: " + KindName(s.Cls), null, true, "Which kind of troops this banner takes."),
                new InquiryElement("men", "Men: " + (basic ? Approx(i, fit, w1) + " (" + w1[i] + "% of your " + KindLower(s.Cls) + ")" : Approx(i, fit, w1)), null, basic,
                                   basic ? "How many of its kind the banner takes. The first banner of a kind takes whatever the others leave, unless one is set to the rest."
                                         : "A banner of two kinds keeps the shares the game gave it - choose one kind to set its men."),
                new InquiryElement("filters", "Filled first with: " + (fl.Count > 0 ? string.Join(", ", fl.ToArray()) : "any man of its kind"), null, true,
                                   "Which men of its kind the Order of Battle puts here first (when you command 20 men or more and the screen sorts them)."),
                new InquiryElement("captain", "Commander: " + (s.Captain == null ? "none" : HeroLine(s.Captain)), null, true,
                                   "A hero of your party who leads this banner. Each hero leads one banner at most" + OobOnly),
                new InquiryElement("ranks", "Companions in the ranks: " + s.Ranks.Count, null, true, "Heroes of your party who fight in this banner's ranks" + OobOnly),
                new InquiryElement("name", "Name: " + (string.IsNullOrEmpty(s.Name) ? "none" : s.Name), null, true, "Your own name for the banner."),
                new InquiryElement("strike", "Strike the banner", null, true, "Its men go to the other banners of their kind (or to one raised for them on saving)"
                                   + (PinOn && PinNames(_workPins, i).Count > 0 ? "; the troop types placed in it go back to sorting by kind." : ".")),
            };
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(Label(i), BannerHint(i, fit, w1, w2), rows, true, 1, 1, "Choose", "Back",
                delegate (List<InquiryElement> picked)
                {
                    try
                    {
                        string id = picked != null && picked.Count > 0 ? picked[0].Identifier as string : null;
                        if (id == "kind") OpenKind(i);
                        else if (id == "men") OpenMen(i);
                        else if (id == "filters") OpenFilters(i);
                        else if (id == "captain") OpenCommander(i);
                        else if (id == "ranks") OpenRanks(i);
                        else if (id == "name") AskName(i);
                        else if (id == "strike") { _work[i] = null; DropPinsTo(i); _dirty = true; OpenBanners(false); }
                        else OpenBanner(i);
                    }
                    catch (Exception e) { Stumble("Banner.Pick", e); }
                },
                delegate (List<InquiryElement> _) { try { OpenBanners(false); } catch (Exception e) { Stumble("Banner.Back", e); } }), true);
        }

        private static void OpenKind(int i)
        {
            var s = _work[i];
            var fit = CommandedFit();
            var rows = new List<InquiryElement>();
            for (int k = 1; k <= 4; k++)
                rows.Add(new InquiryElement(k, KindName(k) + " (" + fit[k - 1] + " fit)" + (s.Cls == k ? " - now" : ""), null, true,
                                            fit[k - 1] > 0 ? "" : "You have no fit " + KindLower(k) + " now - the banner would stand empty until you do."));
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(Label(i), "Which kind of troops does this banner take?", rows, true, 1, 1, "Choose", "Back",
                delegate (List<InquiryElement> picked)
                {
                    try
                    {
                        if (picked != null && picked.Count > 0 && picked[0].Identifier is int)
                        {
                            int k = (int)picked[0].Identifier;
                            if (k != s.Cls)
                            {
                                s.Cls = k;
                                s.Men = FirstOfClass(_work, i, k) ? TheRest : (s.Men >= 0 ? Math.Min(s.Men, Math.Max(1, fit[k - 1])) : DefaultMen(fit[k - 1]));
                                s.W2 = 0;
                                _dirty = true;
                            }
                        }
                        OpenBanner(i);
                    }
                    catch (Exception e) { Stumble("Kind.Pick", e); }
                },
                delegate (List<InquiryElement> _) { try { OpenBanner(i); } catch (Exception e) { Stumble("Kind.Back", e); } }), true);
        }

        private static void OpenMen(int i)
        {
            var s = _work[i];
            if (s.Cls < 1 || s.Cls > 4) { OpenBanner(i); return; }
            var fit = ShareFit(_work, _workPins);   // P12: liczby ludzi z nieprzypietych
            int h = fit[s.Cls - 1];
            var rows = new List<InquiryElement>
            {
                new InquiryElement(TheRest, "The rest of the " + KindLower(s.Cls) + (s.Men == TheRest ? " - now" : ""), null, true,
                                   "Whatever the other banners of this kind leave (shared out evenly if several banners take the rest).")
            };
            foreach (int m in MenSteps)
            {
                if (m >= h && rows.Count > 1) break;
                rows.Add(new InquiryElement(m, m + " men" + (h > 0 ? " (" + (int)Math.Round(100f * m / h) + "% of " + h + " fit now)" : "") + (s.Men == m ? " - now" : ""), null, true,
                                            "The banner keeps this share of your " + KindLower(s.Cls) + ": about " + m + " men while you have " + h + "."));
            }
            // recenzja 25.10: jedna choragiew rodzaju zawsze bierze reszte (Weights) - liczba dla tej, ktora bierze ja niejawnie, bylaby po cichu pominieta
            int other = FirstOtherOfClass(_work, i, s.Cls);
            string q = "How many of your " + h + " fit " + KindLower(s.Cls) + (CommandedFit()[s.Cls - 1] > h ? " (not counting those placed by type)" : "") + " does this banner take?";
            if (other < 0) q += " It is your only banner of " + KindLower(s.Cls) + ", so it takes them all until you raise another.";
            else if (TakesRest(_work, i) && !RestElsewhere(_work, i, s.Cls))
                q += " It takes whatever the other banners leave; pick a number and banner " + Roman[other] + " takes the rest instead.";
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(Label(i), q,
                rows, true, 1, 1, "Choose", "Back",
                delegate (List<InquiryElement> picked)
                {
                    try
                    {
                        if (picked != null && picked.Count > 0 && picked[0].Identifier is int)
                        {
                            s.Men = (int)picked[0].Identifier;
                            _dirty = true;
                            // liczba ludzi, a zadna inna choragiew rodzaju nie bierze reszty: reszte dostaje jawnie pierwsza inna (ta sama zasada co Weights)
                            int j = s.Men >= 0 && !RestElsewhere(_work, i, s.Cls) ? FirstOtherOfClass(_work, i, s.Cls) : -1;
                            if (j >= 0)
                            {
                                _work[j].Men = TheRest;
                                Log.Player("Banner " + Roman[j] + " now takes the rest of your " + KindLower(s.Cls) + ".");
                            }
                        }
                        OpenBanner(i);
                    }
                    catch (Exception e) { Stumble("Men.Pick", e); }
                },
                delegate (List<InquiryElement> _) { try { OpenBanner(i); } catch (Exception e) { Stumble("Men.Back", e); } }), true);
        }

        private static void OpenFilters(int i)
        {
            var s = _work[i];
            var rows = new List<InquiryElement>();
            for (int f = 1; f <= 6; f++)
            {
                string n = FilterNames[f];
                rows.Add(new InquiryElement(f, char.ToUpperInvariant(n[0]) + n.Substring(1) + ": " + (s.Filt[f] ? "yes" : "no"), null, true, "Pick to switch it " + (s.Filt[f] ? "off." : "on.")));
            }
            rows.Add(new InquiryElement(0, "Done", null, true, ""));
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(Label(i), "Which men of its kind should fill this banner first?", rows, true, 1, 1, "Choose", "Back",
                delegate (List<InquiryElement> picked)
                {
                    try
                    {
                        int f = picked != null && picked.Count > 0 && picked[0].Identifier is int ? (int)picked[0].Identifier : 0;
                        if (f >= 1 && f <= 6) { s.Filt[f] = !s.Filt[f]; _dirty = true; OpenFilters(i); }
                        else OpenBanner(i);
                    }
                    catch (Exception e) { Stumble("Filters.Pick", e); }
                },
                delegate (List<InquiryElement> _) { try { OpenBanner(i); } catch (Exception e) { Stumble("Filters.Back", e); } }), true);
        }

        /// <summary>Bohaterowie druzyny gracza (gracz pierwszy).</summary>
        private static List<Hero> PartyHeroes()
        {
            var l = new List<Hero>();
            var r = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
            if (Hero.MainHero != null) l.Add(Hero.MainHero);
            if (r == null) return l;
            for (int k = 0; k < r.Count; k++)
            {
                var ch = r.GetCharacterAtIndex(k);
                var h = ch != null && ch.IsHero ? ch.HeroObject : null;
                if (h != null && !l.Contains(h)) l.Add(h);
            }
            return l;
        }

        private static string Where(Hero h, int except)
        {
            for (int i = 0; i < Slots; i++)
            {
                if (i == except || _work[i] == null) continue;
                if (_work[i].Captain == h) return "leads banner " + Roman[i];
                if (_work[i].Ranks.Contains(h)) return "in the ranks of banner " + Roman[i];
            }
            return null;
        }

        private static void Unplace(Hero h)
        {
            for (int i = 0; i < Slots; i++)
            {
                if (_work[i] == null) continue;
                if (_work[i].Captain == h) _work[i].Captain = null;
                _work[i].Ranks.Remove(h);
            }
        }

        private static ImageIdentifier Portrait(Hero h)
        {
            try { return new CharacterImageIdentifier(CharacterCode.CreateFrom(h.CharacterObject)); } catch { return null; }
        }

        private static string CaptainHint(Hero h)
        {
            var l = new List<string>();
            try { l.Add("Tactics " + h.GetSkillValue(DefaultSkills.Tactics) + ", Leadership " + h.GetSkillValue(DefaultSkills.Leadership) + "."); } catch { }
            try
            {
                var m = Campaign.Current.Models.BattleCaptainModel;
                List<PerkObject> perks;
                int inf = (int)(m.GetCaptainRatingForTroopUsages(h, FormationClass.Infantry.GetTroopUsageFlags(), out perks) * 100f);
                int rng = (int)(m.GetCaptainRatingForTroopUsages(h, FormationClass.Ranged.GetTroopUsageFlags(), out perks) * 100f);
                int cav = (int)(m.GetCaptainRatingForTroopUsages(h, FormationClass.Cavalry.GetTroopUsageFlags(), out perks) * 100f);
                int ha = (int)(m.GetCaptainRatingForTroopUsages(h, FormationClass.HorseArcher.GetTroopUsageFlags(), out perks) * 100f);
                l.Add("As a captain (the game's influence): infantry " + inf + ", archers " + rng + ", cavalry " + cav + ", horse archers " + ha + ".");
            }
            catch { }
            if (h.IsWounded) l.Add("Wounded - will not take the field until healed.");
            return string.Join("\n", l.ToArray());
        }

        private static void OpenCommander(int i)
        {
            var s = _work[i];
            var rows = new List<InquiryElement> { new InquiryElement("none", "No commander" + (s.Captain == null ? " - now" : ""), null, true, "") };
            foreach (var h in PartyHeroes())
            {
                if (!HeroOk(h, true)) continue;
                string w = Where(h, i);
                string t = (h == Hero.MainHero ? "You (" + h.Name + ")" : h.Name.ToString()) + (h.IsWounded ? " (wounded)" : "") + (w != null ? " - " + w : "") + (s.Captain == h ? " - now" : "");
                rows.Add(new InquiryElement(h, t, Portrait(h), true, CaptainHint(h) + (w != null ? "\nChoosing here moves this hero from banner " + w.Substring(w.LastIndexOf(' ') + 1) + "." : "")));
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(Label(i), "Who leads this banner?", rows, true, 1, 1, "Choose", "Back",
                delegate (List<InquiryElement> picked)
                {
                    try
                    {
                        object id = picked != null && picked.Count > 0 ? picked[0].Identifier : null;
                        var h = id as Hero;
                        if (h != null && s.Captain != h) { Unplace(h); s.Captain = h; _dirty = true; }
                        else if ((id as string) == "none" && s.Captain != null) { s.Captain = null; _dirty = true; }
                        OpenBanner(i);
                    }
                    catch (Exception e) { Stumble("Commander.Pick", e); }
                },
                delegate (List<InquiryElement> _) { try { OpenBanner(i); } catch (Exception e) { Stumble("Commander.Back", e); } }), true);
        }

        private static void OpenRanks(int i)
        {
            var s = _work[i];
            var rows = new List<InquiryElement>();
            foreach (var h in PartyHeroes())
            {
                if (h == Hero.MainHero || !HeroOk(h, false) || s.Captain == h) continue;
                bool here = s.Ranks.Contains(h);
                string w = here ? null : Where(h, i);
                rows.Add(new InquiryElement(h, h.Name + ": " + (here ? "in these ranks" : w ?? "not in this banner") + (h.IsWounded ? " (wounded)" : ""), Portrait(h), true,
                                            here ? "Pick to take this hero out of these ranks." : "Pick to put this hero in these ranks" + (w != null ? " (leaving the other banner)." : ".")));
            }
            rows.Add(new InquiryElement("done", "Done", null, true, ""));
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(Label(i), rows.Count > 1 ? "Which companions fight in this banner's ranks?" : "You have no companions to place.",
                rows, true, 1, 1, "Choose", "Back",
                delegate (List<InquiryElement> picked)
                {
                    try
                    {
                        var h = picked != null && picked.Count > 0 ? picked[0].Identifier as Hero : null;
                        if (h == null) { OpenBanner(i); return; }
                        if (s.Ranks.Contains(h)) s.Ranks.Remove(h);
                        else { Unplace(h); s.Ranks.Add(h); }
                        _dirty = true;
                        OpenRanks(i);
                    }
                    catch (Exception e) { Stumble("Ranks.Pick", e); }
                },
                delegate (List<InquiryElement> _) { try { OpenBanner(i); } catch (Exception e) { Stumble("Ranks.Back", e); } }), true);
        }

        /// <summary>Nazwa bez znakow, ktore psuja zapis lub tekst gry ({ } | ~ i sterujace), najwyzej 40 znakow.</summary>
        internal static string Clean(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            foreach (char c in s.Trim()) if (c >= ' ' && c != '{' && c != '}' && c != '|' && c != '~') sb.Append(c);
            string r = sb.ToString().Trim();
            return r.Length > 40 ? r.Substring(0, 40).Trim() : r;
        }

        private static void AskName(int i)
        {
            var s = _work[i];
            string def = !string.IsNullOrEmpty(s.Name) ? s.Name : s.Captain != null && s.Captain != Hero.MainHero ? Clean(s.Captain.Name + "'s Banner") : "";
            InformationManager.ShowTextInquiry(new TextInquiryData(Label(i), "What is this banner called? (up to 40 letters; leave it empty for no name)", true, true, "Name it", "Back",
                delegate (string text)
                {
                    try
                    {
                        string n = Clean(text);
                        s.Name = n.Length > 0 ? n : null;
                        _dirty = true;
                        OpenBanner(i);
                    }
                    catch (Exception e) { Stumble("Name.Set", e); }
                },
                delegate { try { OpenBanner(i); } catch (Exception e) { Stumble("Name.Back", e); } }, false, null, "", def), true);
        }

        // ------------------------------------------------------------ zapis z okna, powrot do porzadku gry
        private static void SaveWork()
        {
            if (!Writes) { OpenBanners(false); return; }
            var r = Write(_work, PinOn ? _workPins : null);   // P12: przypiecia razem z choragwiami (klucz wylaczony - zapisane bez zmian)
            try { Log.Info(WriteLine("okno", r)); } catch (Exception e) { Stumble("Save.Log", e); }
            if (!r.Ok)
            {
                _dRefused++;
                Log.Player(r.Error, true);
                OpenBanners(false);
                return;
            }
            _dSaves++;
            _dAuto += r.Auto.Count;
            if (r.PinCommitted) _dPinSaves++;
            if (r.AsReset)
            {
                _dResets++;
                Log.Player("Every banner is struck - your next battle follows the game's own order.");
            }
            else
            {
                int n = 0;
                foreach (var x in r.Banners) if (x != null) n++;
                string msg = "Your banners are set for the next battle: " + n + (n == 1 ? " banner." : " banners.");
                if (r.Auto.Count > 0)
                {
                    var l = new List<string>();
                    foreach (int i in r.Auto) l.Add(Roman[i] + " for your " + KindLower(r.Banners[i].Cls));
                    msg += " Raised for kinds of troop that had none: " + string.Join(", ", l.ToArray()) + ".";
                }
                if (r.Dropped > 0) msg += " " + r.Dropped + (r.Dropped == 1 ? " hero was" : " heroes were") + " left out (dead, captive, away or placed twice).";
                if (r.PinCommitted && r.PinTypes > 0) msg += " " + r.PinTypes + (r.PinTypes == 1 ? " troop type stands" : " troop types stand") + " in the banner you chose.";
                if (!r.ReadField || !r.ReadArmy) msg += " The game did not take the order as written - see the log.";
                Log.Player(msg, !r.ReadField || !r.ReadArmy);
            }
            _work = null; _workPins = null; _dirty = false;
            OpenBanners(true);
        }

        private static void AskReset()
        {
            InformationManager.ShowInquiry(new InquiryData("Return to the game's own order",
                "Forget your banners? The next battle will sort your men by kind into the game's own formations, and the names of your banners and the troop types placed in them are lost.",
                true, true, "Forget them", "Back",
                delegate
                {
                    try
                    {
                        int hadPins = _pins.Count;
                        bool ok = ResetOrder();
                        _dResets++;
                        _work = null; _workPins = null; _dirty = false;
                        Log.Info(P + "okno - powrot do porzadku gry: listy pola i armii puste, odczyt null x 16 " + Yes(ok) + ", nazwy wyczyszczone, przypiecia zdjete " + hadPins + ".");
                        Log.Player(ok ? "Your banners are struck - the next battle follows the game's own order." : "The game did not clear its order - see the log.", !ok);
                        OpenMain(false);
                    }
                    catch (Exception e) { Stumble("Reset", e); }
                },
                delegate { try { OpenMain(false); } catch (Exception e) { Stumble("Reset.Back", e); } }), true);
        }

        // ------------------------------------------------------------ doba
        private static void Daily()
        {
            _sessionDays++;
            try { TestStep(); } catch (Exception e) { Stumble("Test", e); }
            try
            {
                if (_dWindows + _dReports + _dBanners + _dSaves + _dRefused + _dResets + _dStumbles + _dPinSaves > 0)
                    Log.Info(P + "doba " + (int)CampaignTime.Now.ToDays + " | okna " + _dWindows + " (strony raportu " + _dReports + ", choragwie " + _dBanners + "), zapisy " + _dSaves
                             + " (odmowy " + _dRefused + ", dopisane rodzaje " + _dAuto + ", z przypieciami " + _dPinSaves + "), powrot do porzadku gry " + _dResets
                             + " | przypiecia " + PinSummary(_pins) + " | potkniecia doby " + _dStumbles
                             + " (sesji " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            DayReset();
        }
    }
}
