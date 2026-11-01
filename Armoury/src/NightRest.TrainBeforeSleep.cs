using System;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 311e TRENING PRZED SNEM (Jeff 31.10: "w menu obozu dodaj opcje, ile godzin przed snem armia ma sie zatrzymywac na trening od 1 godziny do 8 godzin.
    /// Wtedy automatycznie sie zatrzymuje wczesniej i trenuje").
    /// ZASADA: ustawienie N (0 = wylaczone, 1-8 h). Kazdej doby (od switu gracza) druzyna gracza - i armia, ktora gracz prowadzi (doczepieni stoja z wodzem) -
    /// staje N godzin przed noca snu (NightRest.SleepNightStart = swit - 9, przy obozie 21-3 godzina 18:00; to ta sama granica, od ktorej cwiczen juz nie ma -
    /// CampDrill.DayHour), wchodzi w to samo menu cwiczen co "Train the men" (arm_camp_drill_wait) i o zmierzchu snu idzie prosto do snu w menu (arm_sleep_wait,
    /// powrot snu = oboz, jak "Bed down and sleep" z menu obozu).
    ///  - JEDNO zrodlo XP: godziny ida przez CampDrill.DrillTick do _dayHours / _owed - ten sam wzor musztry (Drill.Shape: D = godziny / 8 x 2.25, Przywodztwo,
    ///    zapas broni, uzbrojeni, perki), ten sam limit 8 h od switu, ta sama kara (glod, dlug snu o swicie). Zadnego wlasnego XP. Lordowie armii gracza - jak
    ///    dotad z godzin postoju za dnia (Drill.RestHour), bo wodz stoi.
    ///  - start dopiero, gdy do nocy snu zostaje tyle godzin, ile jeszcze wolno cwiczyc (min(N, 8 - godziny od switu)) - cwiczenia koncza sie o zmierzchu,
    ///    nie wczesniej (inaczej pelne 8 h w srodku popoludnia = twarda pauza menu obozu do rana).
    ///  - warunki (jak oboz z rozkazu przy zmierzchu w OnHourly + menu cwiczen CampDrill.WhyNot): czysta mapa (bez menu, spotkania, rozmowy), bez bitwy,
    ///    oblezenia (oboz obleznikow, SiegeEvent), osady, morza, doczepienia do cudzej armii, marszu forsownego, rabunku, niewoli, sluzby ROT, snu w menu,
    ///    poscigu (EngageParty) i bez wrogiego lorda albo bandy w promieniu AiCampDangerRadius (ten sam ThreatHits co oboz AI). Powod odmowy w logu raz na dobe;
    ///    doba nie jest odhaczana - nastepna godzina okna probuje znowu.
    ///  - menu: z ticku godzinowego (GameMenu.ActivateGameMenu, jak OwnCamp przy zmierzchu - ta sama chwila, ta sama sciezka), namiot raz przy starcie (Tent).
    ///    Koniec: CampDrill.Leave z ticku menu oczekiwania (SwitchToMenu z ticku wolno - CLAUDE.md 7 zakazuje go tylko w OPCJI menu oczekiwania).
    /// ZRODLO PRAWDY USTAWIENIA: wybor z menu obozu ("Train before sleep: N h") zapisany w kampanii (arm_nightrest, szoste pole; -1 = nie wybrano).
    /// Klucz MCM CampTrainBeforeSleepHours to ustawienie dla kampanii, w ktorej w menu jeszcze nic nie wybrano (i po "Follow the MCM setting").
    /// Dlaczego nie samo MCM: McmSettings.Apply (co godzine) nadpisuje Settings.Current wartoscia MCM, a zapis Armoury.json z kodu przez MCM nie jest sprawdzony -
    /// wybor z menu ginalby po godzinie albo po restarcie gry. Tak jak rozkaz zmierzchu (CampPromptMode) - stan rozgrywki gracza zyje w zapisie.
    /// Log: "Trening przed snem (311e): ..." - ustawienie, start (godziny, ludzie, armia), pominiecie (raz na dobe), koniec (godziny, powod, sen), linia switu
    /// (starty, godziny, XP z tickow musztry). Grupa sprawdz_logi: trening311e.
    /// </summary>
    internal static partial class NightRest
    {
        internal const string TbsP = "Trening przed snem (311e): ";
        internal const int TbsMax = 8;
        private static int _tbsSet = -1;                       // wybor z menu obozu (zapis), -1 = wedlug MCM
        private static long _tbsDay = long.MinValue;           // doba switu ostatniego startu (albo doby bez miejsca na cwiczenia)
        private static long _tbsSkipDay = long.MinValue;       // doba switu ostatniej linii pominiecia (raz na dobe)
        private static int _tbsWant;                           // godziny zamierzone przy starcie biezacego postoju
        private static double _tbsStartAt;
        // liczniki od ostatniej linii switu
        private static int _tbsStarts, _tbsEnds, _tbsSleeps, _tbsSkips, _tbsStumbles;
        private static float _tbsHours, _tbsXpHours;
        private static double _tbsXp, _tbsXpMen;
        private static string _tbsLastSkip;

        private static string Tf(double v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }

        /// <summary>Godziny treningu przed snem w tej kampanii: wybor z menu obozu, a bez wyboru klucz MCM (0..8).</summary>
        internal static int TbsHours
        {
            get
            {
                if (_tbsSet >= 0) return Math.Min(TbsMax, _tbsSet);
                var s = Settings.Current;
                return s == null ? 0 : Math.Max(0, Math.Min(TbsMax, s.CampTrainBeforeSleepHours));
            }
        }

        private static int TbsMcm { get { var s = Settings.Current; return s == null ? 0 : Math.Max(0, Math.Min(TbsMax, s.CampTrainBeforeSleepHours)); } }

        private static string TbsSource() { return _tbsSet >= 0 ? "z menu obozu" : "z MCM (Camp Train Before Sleep Hours " + TbsMcm + ")"; }

        private static void TbsStumble(string where, Exception e)
        {
            _tbsStumbles++;
            if (_tbsStumbles <= 3) { try { Log.Error("NightRest.Tbs." + where, e); } catch { } }
        }

        /// <summary>Nowa kampania / wczytanie (ResetWorld): wybor z poprzedniej kampanii nie przechodzi; Import ustawia go z zapisu.</summary>
        private static void TbsReset()
        {
            _tbsSet = -1; _tbsDay = long.MinValue; _tbsSkipDay = long.MinValue; _tbsWant = 0; _tbsStartAt = 0;
            _tbsStarts = _tbsEnds = _tbsSleeps = _tbsSkips = 0; _tbsHours = _tbsXpHours = 0f; _tbsXp = _tbsXpMen = 0; _tbsLastSkip = null;
        }

        private static long TbsDawnIdx() { return (long)Math.Floor((CampaignTime.Now.ToHours - PlayerDawn) / 24.0); }

        // ------------------------------------------------------------ godzina (OnHourly, po CreditRest)
        private static void TbsHour(Settings s, MobileParty mp, bool captive, bool enl, bool raidNow, bool forced)
        {
            try
            {
                int n = TbsHours;
                if (n <= 0 || mp == null || !CampDrill.PlayerRuleOn) return;
                if (s.NightLedgerClock && NightClock.CatchUp) return;   // tick nadrabiany w tej samej klatce - menu nie otwieramy (nastepna godzina okna)
                int hc = CampaignTime.Now.GetHourOfDay;
                int left = Mod24(SleepNightStart - hc);                 // pelnych godzin do nocy snu (0 = juz noc snu)
                if (left < 1 || left > n || !CampDrill.DayHour(hc)) return;
                long day = TbsDawnIdx();
                if (_tbsDay == day) return;
                float room = CampDrill.FullHours - CampDrill.PlayerDayHours;
                int want = Math.Min(n, (int)Math.Floor(room + 0.001f));
                if (want <= 0)
                {
                    _tbsDay = day;
                    TbsSkip(day, "pelny dzien cwiczen juz zrobiony (" + F2(CampDrill.PlayerDayHours) + " h z 8 od switu)");
                    return;
                }
                if (left > want) return;   // czekamy, az do nocy snu zostanie tyle godzin, ile jeszcze wolno cwiczyc
                string why = TbsWhyNot(s, mp, captive, enl, raidNow, forced);
                if (why != null) { TbsSkip(day, why); return; }
                _tbsDay = day;
                TbsStart(s, mp, n, left);
            }
            catch (Exception e) { TbsStumble("Hour", e); }
        }

        /// <summary>Powod, dla ktorego teraz nie stajemy (null = stajemy). Kolejnosc: stan mapy, stan druzyny (jak oboz z rozkazu przy zmierzchu), menu cwiczen
        /// (CampDrill.WhyNot - glod, dlug o swicie, brak ludzi, pora), zagrozenie.</summary>
        private static string TbsWhyNot(Settings s, MobileParty mp, bool captive, bool enl, bool raidNow, bool forced)
        {
            var c = Campaign.Current;
            if (c == null) return "brak kampanii";
            if (c.CurrentMenuContext != null) return "menu " + (MenuIdNow() ?? "?");
            var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
            var ms = gsm != null ? gsm.ActiveState as MapState : null;
            if (ms == null || ms.AtMenu) return "nie czysta mapa (misja, ekran albo menu)";
            if (c.ConversationManager != null && c.ConversationManager.IsConversationInProgress) return "rozmowa";
            if (captive) return "niewola";
            if (enl) return "sluzba ROT";
            if (_sleeping) return "sen w menu";
            if (forced || ForcedMarch.Active) return "marsz forsowny";
            bool sideR;
            if (raidNow || PlayerRaidingNow(out sideR)) return "rabunek";
            if (PlayerEncounter.Current != null || mp.MapEvent != null) return "spotkanie albo bitwa";
            if (mp.SiegeEvent != null || mp.BesiegerCamp != null) return "oblezenie";
            if (mp.CurrentSettlement != null) return "w osadzie";
            if (mp.IsCurrentlyAtSea) return "na morzu";
            if (mp.AttachedTo != null || (mp.Army != null && mp.Army.LeaderParty != mp)) return "w cudzej armii";
            if (mp.ShortTermBehavior == AiBehavior.EngageParty || mp.DefaultBehavior == AiBehavior.EngageParty) return "poscig albo atak na partie";
            string drill = CampDrill.WhyNot();
            if (drill != null) return "menu cwiczen: " + drill;
            float r = s.AiCampDangerRadius;
            if (r > 0f)
            {
                var mf = mp.MapFaction;
                foreach (var t in MobileParty.All)
                {
                    if (t == null || !t.IsActive || !(t.IsLordParty || t.IsBandit)) continue;
                    if (ThreatHits(mp, mf, t, r)) return "wrog w promieniu " + Tf(r) + " (" + (t.Name != null ? t.Name.ToString() : t.StringId) + ")";
                }
            }
            return null;
        }

        private static void TbsSkip(long day, string why)
        {
            _tbsSkips++; _tbsLastSkip = why;
            if (_tbsSkipDay == day) return;
            _tbsSkipDay = day;
            Log.Info(TbsP + "pominiety o " + CampaignTime.Now.GetHourOfDay + ":00 (doba switu " + day + ", ustawienie " + TbsHours + " h " + TbsSource() + ") | powod: " + why
                     + " | kolejne godziny okna probuja znowu, linia raz na dobe.");
        }

        private static void TbsStart(Settings s, MobileParty mp, int n, int want)
        {
            int men = 0, armyParties = 0, armyMen = 0;
            try
            {
                var r = mp.MemberRoster;
                if (r != null) men = Math.Max(0, r.TotalRegulars - r.TotalWoundedRegulars);
                if (mp.Army != null && mp.Army.LeaderParty == mp)
                    foreach (var p in mp.Army.Parties)
                    {
                        if (p == null || p == mp) continue;
                        armyParties++;
                        if (p.MemberRoster != null) armyMen += Math.Max(0, p.MemberRoster.TotalHealthyCount);
                    }
            }
            catch (Exception e) { TbsStumble("Start.Men", e); }
            mp.SetMoveModeHold();
            CampDrill.BeginPreSleep();   // menu cwiczen (DrillInit: postoj, sesja, tekst)
            Tent(mp, true);              // namiot raz, przy zmianie stanu (CLAUDE.md 7)
            _tbsStarts++; _tbsWant = want; _tbsStartAt = CampaignTime.Now.ToHours;
            int hc = CampaignTime.Now.GetHourOfDay;
            string dawnTxt = TbsUntilDawn();   // 315a: sen po treningu trwa do switu (klucz SleepUntilDawn)
            Msg(PartyWords.Pick("The column halts " + want + "h before nightfall to drill in camp; at nightfall the men bed down" + dawnTxt + ".",
                                "You halt " + want + "h before nightfall to train; at nightfall you bed down" + dawnTxt + ".",
                                "You and your companions halt " + want + "h before nightfall to train; at nightfall you bed down" + dawnTxt + "."), Colors.White);
            Log.Info(TbsP + "start o " + hc + ":00 | ustawienie " + n + " h " + TbsSource() + ", cwiczen do nocy snu " + want + " h (noc snu od " + SleepNightStart
                     + ":00, dzis od switu juz " + F2(CampDrill.PlayerDayHours) + " h z 8) | ludzi zdolnych " + men
                     + (armyParties > 0 ? ", armia gracza: druzyn doczepionych " + armyParties + ", ludzi " + armyMen + " (cwicza z godzin postoju za dnia - Drill.RestHour)" : "")
                     + " | dlug snu " + Debt + ", odpoczynek doby " + F2(_restTonight) + " h.");
        }

        /// <summary>CampDrill.Leave przy sesji 311e: linia konca i decyzja, czy prosto do snu (zmierzch snu - powod "noc", przy ksiedze snu).</summary>
        internal static bool TbsEnd(float hours, string why, float clock)
        {
            bool sleep = false;
            try
            {
                var s = Settings.Current;
                _tbsEnds++; _tbsHours += Math.Max(0f, hours);
                // poprawka recenzji 311e (2): pelny dzien (8 h) dobity tuz przed noca snu (start rowno o pelnej godzinie) - to ten sam zmierzch, idziemy spac
                // (inaczej twarda pauza menu obozu o 17:59); prog 0.25 h zegara do poczatku nocy snu
                if (why == "pelny dzien")
                {
                    double hd = CampaignTime.Now.CurrentHourInDay;
                    double toNight = ((SleepNightStart - hd) % 24.0 + 24.0) % 24.0;
                    if (toNight <= 0.25) why = "noc (pelny dzien " + F2((float)toNight) + " h przed noca snu)";
                }
                sleep = why.StartsWith("noc", StringComparison.Ordinal) && s != null && s.NightRestEnabled && !_sleeping && MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement == null;
                if (sleep) _tbsSleeps++;
                Log.Info(TbsP + "koniec o " + CampaignTime.Now.GetHourOfDay + ":00 | cwiczen " + F2(hours) + " h (zegara " + F2(clock) + " h, zamierzone " + _tbsWant
                         + " h) | powod: " + why + " | dzis od switu " + F2(CampDrill.PlayerDayHours) + " h z 8, do policzenia " + F2(CampDrill.PlayerOwed)
                         + " h - XP policzy tick musztry (linia 'Szkolenie w obozie (m97): gracz' z dopiskiem 311e) | dalej: " + (sleep ? "sen w menu (arm_sleep_wait" + (s != null && s.SleepUntilDawn ? ", do switu " + PlayerDawn + ":00 - linia 'Sen do switu (315a)'" : "") + ")" : "menu obozu")
                         + ".");
            }
            catch (Exception e) { TbsStumble("End", e); }
            return sleep;
        }

        /// <summary>311e: o zmierzchu snu z ticku menu cwiczen prosto do snu - jak "Bed down and sleep" z menu obozu (powrot snu = oboz, namiot zostaje).</summary>
        internal static void TbsSleepNow()
        {
            _sleepReturn = "arm_camp_wait";
            GameMenu.SwitchToMenu("arm_sleep_wait");
        }

        /// <summary>CampDrill.AfterPlayerTick: godziny 311e zuzyte tickiem musztry i ich czesc XP (do linii switu).</summary>
        internal static void TbsXp(float hours, double xp, double men)
        {
            _tbsXpHours += hours; _tbsXp += xp; if (men > _tbsXpMen) _tbsXpMen = men;
        }

        /// <summary>Linia switu gracza (galaz switu OnHourly) - tylko przy ustawieniu > 0 albo ruchu w liczniku; liczniki od zera.</summary>
        private static void TbsDawnLine()
        {
            try
            {
                if (TbsHours > 0 || _tbsStarts > 0 || _tbsEnds > 0 || _tbsXpHours > 0f || _tbsSkips > 0)
                    Log.Info(TbsP + "doba switu " + (TbsDawnIdx() - 1) + " | ustawienie " + TbsHours + " h " + TbsSource() + " | startow " + _tbsStarts + ", koncow " + _tbsEnds
                             + " (prosto do snu " + _tbsSleeps + "), godzin cwiczen " + F2(_tbsHours) + " | tick musztry: godzin 311e " + F2(_tbsXpHours) + ", XP ok. "
                             + (long)Math.Round(_tbsXp) + " (na glowe " + Tf(_tbsXpMen > 0 ? _tbsXp / _tbsXpMen : 0) + ") | godzin pominietych " + _tbsSkips
                             + (_tbsLastSkip != null ? " (ostatni powod: " + _tbsLastSkip + ")" : "") + " | potkniecia " + _tbsStumbles + ".");
            }
            catch (Exception e) { TbsStumble("Dawn", e); }
            _tbsStarts = _tbsEnds = _tbsSleeps = _tbsSkips = 0; _tbsHours = _tbsXpHours = 0f; _tbsXp = _tbsXpMen = 0; _tbsLastSkip = null;
        }

        // ------------------------------------------------------------ menu obozu
        private static string TbsLabel(int n) { return n <= 0 ? "off" : n + " h"; }

        /// <summary>315a: dopisek do napisow 311e - sen po treningu trwa do switu gracza (klucz SleepUntilDawn); wylaczony - pusty (jak dotad).</summary>
        internal static string TbsUntilDawn()
        {
            var s = Settings.Current;
            return s != null && s.SleepUntilDawn ? " until dawn (" + PlayerDawn + ":00)" : "";
        }

        private static void TbsAddMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("arm_camp_wait", "arm_camp_train_pre", "{=!}{ARM_TBS}",
                delegate (MenuCallbackArgs a)
                {
                    try
                    {
                        if (!CampDrill.PlayerRuleOn) return false;
                        a.optionLeaveType = GameMenuOption.LeaveType.Wait;
                        int n = TbsHours;
                        if (a.Text != null) a.Text.SetTextVariable("ARM_TBS", "Train before sleep: " + TbsLabel(n));
                        a.Tooltip = new TextObject("{=!}Each day on the march your party - and the army you lead - halts this many hours before nightfall ("
                            + SleepNightStart + ":00), drills in camp like Train the men and at nightfall beds down" + TbsUntilDawn() + ". Off, or 1 to 8 hours (a day holds at most 8 hours of drill)."
                            + " Only on open land, by day and with no enemy near. "
                            + (_tbsSet >= 0 ? "Chosen here for this campaign." : "Now following the MCM setting (" + TbsLabel(TbsMcm) + ")."));
                        return true;
                    }
                    catch (Exception e) { TbsStumble("MenuCondition", e); return false; }
                },
                delegate (MenuCallbackArgs a) { TbsChoose(); }, false, 2);
        }

        /// <summary>Wybor godzin: okno wyboru nad zwyklym menu obozu (arm_camp_wait nie jest menu oczekiwania - SwitchToMenu na odswiezenie napisu wolno).</summary>
        private static void TbsChoose()
        {
            try
            {
                int cur = TbsHours;
                var el = new System.Collections.Generic.List<InquiryElement>();
                el.Add(new InquiryElement(0, "Off" + (cur == 0 ? " (current)" : ""), null, true, "No halt before nightfall - drill only when you order it."));
                for (int i = 1; i <= TbsMax; i++)
                    el.Add(new InquiryElement(i, i + (i == 1 ? " hour" : " hours") + (cur == i ? " (current)" : ""), null, true,
                        "Halt " + i + (i == 1 ? " hour" : " hours") + " before nightfall, drill, then bed down."));
                el.Add(new InquiryElement(-1, "Follow the MCM setting (" + TbsLabel(TbsMcm) + ")" + (_tbsSet < 0 ? " (current)" : ""), null, true,
                    "Use Camp Train Before Sleep Hours from the mod settings."));
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "Train before sleep", "How many hours before nightfall should the column halt to drill in camp?",
                    el, true, 1, 1, "Choose", GameTexts.FindText("str_cancel").ToString(),
                    delegate (System.Collections.Generic.List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            int before = TbsHours; string srcBefore = TbsSource();
                            _tbsSet = (int)sel[0].Identifier;
                            int n = TbsHours;
                            Msg(n <= 0 ? "Train before sleep: off." : "Train before sleep: the column will halt " + n + "h before nightfall to drill.", Colors.White);
                            Log.Info(TbsP + "ustawienie z menu obozu: " + before + " h (" + srcBefore + ") -> " + n + " h (" + TbsSource() + ") | noc snu od " + SleepNightStart
                                     + ":00, dzis od switu " + F2(CampDrill.PlayerDayHours) + " h cwiczen.");
                            if (MenuIdNow() == "arm_camp_wait") GameMenu.SwitchToMenu("arm_camp_wait");   // napis opcji od nowa (zwykle menu)
                        }
                        catch (Exception e) { TbsStumble("Choose.Selected", e); }
                    },
                    delegate (System.Collections.Generic.List<InquiryElement> _) { }));
            }
            catch (Exception e) { TbsStumble("Choose", e); }
        }
    }
}
