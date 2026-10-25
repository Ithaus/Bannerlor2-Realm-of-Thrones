using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace Armoury
{
    /// <summary>
    /// m103-sceny E4 - 23 SCENY PROLOGU 298 jako okna w wygladzie wydarzen ROT (tresc: PrologueScenes.Data.cs, okno: StoryWindow/ArmStoryEvent, obrazki: StoryPictures),
    /// kazda w swoim dniu prologu (projekt PROJEKT-PROLOG-298 pkt 2; Jeff 21.10 "I zrob wydarzenia wedlug ksiazki"). Architektura krytyka m103 kroki 6-8.
    /// KIEDY: straznik z SubModuleMain.OnApplicationTick, 4 razy na sekunde - poza tickiem kampanii (pchniecie okna z DailyTick, jak robi ROT, wchodzi w srodek
    /// Campaign.Tick) i takze przy zatrzymanym czasie (dzien 0 nie ma doby: pierwsza doba to 1.0; DailyTick nie ponawia, gdy cos blokuje okno). Scena nalezna, gdy
    /// dzien prologu (PrologueDay) >= dzien sceny i scena nie jest w masce pokazanych/pominietych. Jedna scena naraz, zawsze najnizsza nalezna; nastepna dopiero po
    /// zamknieciu poprzedniej i SceneStableSec s spokoju.
    /// BRAMKI (wszystkie przez SceneStableSec s z rzedu): MapState czynny i kampania ruszyla (GameStarted), bez ekranu wczytywania (LoadingWindow) i mapa gotowa
    /// (MapScreen.IsReady - po wczytaniu zapisu MapState jest czynny juz w trakcie rozgrzewania sceny), bez rozmowy (MapConversationActive, ConversationManager),
    /// bez menu Esc / encyklopedii (ActiveStateDisabledByUser), bez zapytania (IsAnyInquiryActive - takze okno startowe Banner Kings) i sceny powiadomienia, bez pauzy
    /// silnika (MBCommon.IsPaused) i blokady czasu (TimeControlModeLock - zdarzenia mapy, zarzadzanie armia), bez bitwy (MainParty.MapEvent), PlayerEncounter brak
    /// ALBO w osadzie bez bitwy (menu miasta i czekanie w miescie MUSZA byc dozwolone - EncounterManager startuje PlayerEncounter przy wjezdzie, autotest stoi w
    /// town_wait_menus; bramka "PlayerEncounter == null" dalaby zero scen, a regula 30 dni zjadlaby wszystkie), bez warstwy "BannerKingsMapView" na TopScreen
    /// (FindLayer - publiczne API; prywatne pole UIManager.mapView zeruje tylko CloseUI), bez okna wydarzenia (EventPopupState) na stosie stanow. Niewola - dozwolona.
    /// POMIJANE (linia POMINIETA z powodem): spoznione ponad SceneMaxLateDays dni (dluga niewola, bitwy), sceny 1-22 po starcie opowiesci ROT (dzien >= 188), scena 23
    /// (dzien 186: smierc krola, areszt Neda) czeka na koniec prologu (_kingFallen - krol w bitwie odklada smierc, DailyEnd co dobe) i odpada, gdy Ned juz stracony.
    /// Scena czekajaca (bramka) - raz na dzien prologu linia "czeka: powod", gdy spoznienie >= 0.25 dnia.
    /// ZAPIS: maski bitowe w arm_prologue ("sc=" pokazane, "sk=" pominiete). Zapis prologu sprzed m103 (WGRANIE 101, bez "sc=") - sceny z dni ponizej biezacego
    /// oznaczane jako pominiete (POMINIETA, powod "zapis sprzed scen") przy pierwszym ticku mapy - bez serii zaleglych okien. Bit "pokazana" przy udanym otwarciu;
    /// 3 nieudane otwarcia tej samej sceny - pominieta (zapas ShowInquiry zwykle sie otwiera, wiec to tylko ostatnia deska).
    /// Klucz PrologueScenes (MCM, czytany na zywo). Bledy: Stumble (licznik prologu), bez globalnego wylacznika.
    /// </summary>
    internal static partial class Prologue
    {
        internal const string ScTag = "Prolog (m103): ";
        private const int SceneArrest = 22;              // indeks sceny dnia 186 (smierc krola, areszt Neda)
        // recenzja m104: scena 15 (Turniej Reki, dzien 125) opowiada przebieg turnieju - przy scenie E5 (klucz PrologueHandTourney) czeka, az Turniej Reki sie skonczy
        // (rozstrzygniety albo odwolany/pominiety; gra rozstrzyga go najpozniej w dniu 127), najdluzej do dnia SceneTourneyLastDay - inaczej okno mowiloby o koncu
        // turnieju, zanim gracz wjedzie w szranki. Zwyciezce turnieju na mapie podaje komunikat sceny E5 (Prologue.World.Tourney).
        private const int SceneTourney = 14, SceneTourneyLastDay = 137;
        private const int SceneMaxLateDays = 30;         // zalegla dluzej - pominieta
        private const double SceneStableSec = 2.0;       // bramki spelnione tyle sekund z rzedu (po kreatorze, po wczytaniu, miedzy scenami)
        private const double SceneWaitLogDays = 0.25;    // linia "czeka" dopiero przy takim spoznieniu (raz na dzien prologu)
        private static readonly long ScCheckTicks = Stopwatch.Frequency / 4;

        private static int _scShown, _scSkipped, _scOpenFails, _scOpenFailScene = -1;
        private static bool _scLegacy, _scEndLogged;
        private static int _scOpen = -1;
        private static long _scOpenedAt, _scOkSince, _scLastCheck;
        private static int _scWaitLoggedDay = -1;

        private static int SceneCount { get { return PrologueScenes.All.Length; } }
        private static bool ScenesKey { get { var s = S; return s != null && s.PrologueScenes; } }

        private static void ResetScenes()
        {
            _scShown = 0; _scSkipped = 0; _scOpenFails = 0; _scOpenFailScene = -1; _scLegacy = false; _scEndLogged = false;
            _scOpen = -1; _scOpenedAt = 0; _scOkSince = 0; _scWaitLoggedDay = -1;
        }

        private static string PackScenes() { return "sc=" + _scShown.ToString(Inv) + ";sk=" + _scSkipped.ToString(Inv) + ";"; }

        private static void UnpackScenes(Dictionary<string, string> map)
        {
            ResetScenes();
            _scLegacy = Active && !map.ContainsKey("sc");
            _scShown = ParseInt(map, "sc", 0);
            _scSkipped = ParseInt(map, "sk", 0);
        }

        private static bool SceneDone(int i) { return ((_scShown | _scSkipped) & (1 << i)) != 0; }

        private static int Bits(int m) { int n = 0; for (; m != 0; m &= m - 1) n++; return n; }

        private static string NN(int i) { return (i + 1).ToString("00", Inv); }

        private static string SceneTitle(int i)
        {
            try { return PrologueScenes.All[i].Title(); } catch { return "scena " + NN(i); }
        }

        // ---------------------------------------------------------------- straznik (SubModuleMain.OnApplicationTick)

        /// <summary>4 razy na sekunde: zamkniecie okna bez przycisku (StoryWindow.Watch) i kolejka zwolnien obrazkow (StoryPictures.AppTick) - w kazdej kampanii
        /// (obrazki okien ROT); sceny - tylko w kampanii z prologiem.</summary>
        internal static void ScenesAppTick()
        {
            long t = Stopwatch.GetTimestamp();
            if (t - _scLastCheck < ScCheckTicks) return;
            _scLastCheck = t;
            StoryWindow.Watch();
            StoryPictures.AppTick();
            if (!Active) return;
            try { ScenesStep(t); }
            catch (Exception e) { Stumble("Scenes.AppTick", e); }
        }

        private static void ScenesStep(long t)
        {
            var c = Campaign.Current;
            if (c == null || !c.GameStarted || c.Models == null) { _scOkSince = 0; return; }
            if (_scOpen >= 0 || StoryWindow.IsOpen) { _scOkSince = 0; return; }   // jedno okno naraz (zamkniecie - OnSceneClosed albo StoryWindow.Watch)
            double day = PrologueDay();
            if (_scLegacy) SkipPassedLegacy(day);
            if (!ScenesKey) { _scOkSince = 0; return; }
            string waitWhy;
            int waitIdx;
            int next = NextScene(day, out waitWhy, out waitIdx);
            if (next < 0)
            {
                _scOkSince = 0;
                if (waitWhy != null) NoteWait(waitIdx, day, waitWhy);
                if (!_scEndLogged && Bits(_scShown | _scSkipped) >= SceneCount) EndScenes(day);
                return;
            }
            string why = SceneGate(c);
            if (why != null) { _scOkSince = 0; NoteWait(next, day, why); return; }
            if (_scOkSince == 0) { _scOkSince = t; return; }
            if (t - _scOkSince < (long)(SceneStableSec * Stopwatch.Frequency)) return;
            _scOkSince = 0;
            OpenScene(next, day, t);
        }

        /// <summary>Najnizsza nalezna scena (pomija i loguje sceny, ktorych juz nie pokazemy); -1 - nic teraz (waitWhy - scena waitIdx czeka: 23 na koniec prologu,
        /// 15 na koniec Turnieju Reki).</summary>
        private static int NextScene(double day, out string waitWhy, out int waitIdx)
        {
            waitWhy = null; waitIdx = -1;
            var all = PrologueScenes.All;
            for (int i = 0; i < all.Length; i++)
            {
                if (SceneDone(i)) continue;
                int due = all[i].Day;
                if (day < due) return -1;
                if (i < SceneArrest && day >= PrologueDays) { SkipScene(i, day, "opowiesc ROT ruszyla (dzien prologu >= " + PrologueDays + ")"); continue; }
                if (i == SceneTourney && day < SceneTourneyLastDay && TourneyPending())
                {
                    waitWhy = "Turniej Reki jeszcze trwa (" + TourneyPendingText() + "; najdluzej do dnia " + SceneTourneyLastDay + ")"; waitIdx = i;
                    return -1;
                }
                if (i == SceneArrest)
                {
                    if (_nedDone) { SkipScene(i, day, "Ned juz stracony (NedEvent przed scena)"); continue; }
                    if (!_kingFallen) { waitWhy = "koniec prologu jeszcze nie wszedl (krol w bitwie albo oblezeniu - smierc krola co dobe)"; waitIdx = i; return -1; }
                    return i;   // po smierci krola - bez limitu spoznienia (krol mogl umrzec pozniej)
                }
                if (day - due > SceneMaxLateDays) { SkipScene(i, day, "spozniona o " + (day - due).ToString("0.0", Inv) + " dni (ponad " + SceneMaxLateDays + ")"); continue; }
                return i;
            }
            return -1;
        }

        /// <summary>null = okno wolno pokazac; inaczej powod czekania. m105-kronika (A2): internal - te same bramki dla okien i list Kroniki (Chronicle.Step), logika bez zmian.</summary>
        internal static string SceneGate(Campaign c)
        {
            var gsm = GameStateManager.Current;
            var active = gsm != null ? gsm.ActiveState : null;
            var ms = active as MapState;
            if (ms == null) return "nie mapa (" + (active != null ? active.GetType().Name : "-") + ")";
            // recenzja m103: po wczytaniu zapisu GameStarted = true od razu (Campaign.SetLoadingParameters), a MapState jest czynny juz w trakcie rozgrzewania sceny
            // mapy - ekran wczytywania zdejmuje tylko MapScreen.HandleIfBlockerStatesDisabled, i to tylko gdy MapScreen jest na wierzchu (HandleIfSceneIsReady:
            // TopScreen == this). Okno pchniete pod ekran wczytywania zostawiloby go na ekranie do zamkniecia sceny (warstwa 115003 nad wszystkim, wejscie zablokowane).
            if (TaleWorlds.Engine.LoadingWindow.IsLoadingWindowActive) return "ekran wczytywania";
            var msc = SandBox.View.Map.MapScreen.Instance;
            if (msc != null && !msc.IsReady) return "mapa jeszcze nie gotowa (MapScreen.IsReady)";
            if (ms.MapConversationActive) return "rozmowa na mapie";
            var cm = c.ConversationManager;
            if (cm != null && (cm.IsConversationFlowActive || cm.IsConversationInProgress)) return "rozmowa";
            if (gsm.ActiveStateDisabledByUser) return "menu nad mapa (Esc, encyklopedia)";
            if (InformationManager.IsAnyInquiryActive()) return "inne okno (zapytanie)";
            if (MBInformationManager.GetIsAnySceneNotificationActive() == true) return "scena powiadomienia";
            if (MBCommon.IsPaused) return "gra wstrzymana (pauza silnika)";
            if (c.TimeControlModeLock) return "czas zablokowany (zdarzenie mapy, zarzadzanie armia)";
            var mp = MobileParty.MainParty; var me = Hero.MainHero;
            if (mp == null || me == null || !me.IsAlive) return "brak gracza";
            if (mp.MapEvent != null) return "bitwa";
            if (PlayerEncounter.Current != null)
            {
                if (PlayerEncounter.Battle != null) return "bitwa (spotkanie)";
                if (PlayerEncounter.EncounterSettlement == null && mp.CurrentSettlement == null) return "spotkanie na mapie";
            }
            var top = ScreenManager.TopScreen;
            if (top != null && top.FindLayer<ScreenLayer>("BannerKingsMapView") != null) return "okno Banner Kings";
            if (StoryPictures.EventWindowOnStack()) return "okno wydarzenia ROT";
            if (SpoilsWindow.IsOpen) return "okno lupu";   // noc 25.10 P5: okno lupu otwarte albo gotowy raport czeka - lup pierwszy (w bramce samego okna lupu - falsz)
            return null;
        }

        private static void NoteWait(int i, double day, string why)
        {
            try
            {
                int due = PrologueScenes.All[i].Day;
                if (day - due < SceneWaitLogDays || (int)day == _scWaitLoggedDay) return;
                _scWaitLoggedDay = (int)day;
                Log.Info(ScTag + "scena " + NN(i) + " '" + SceneTitle(i) + "' czeka: " + why + " (dzien prologu " + day.ToString("0.00", Inv) + ", nalezna od " + due + ").");
            }
            catch (Exception e) { Stumble("Scenes.NoteWait", e); }
        }

        private static void OpenScene(int i, double day, long t)
        {
            var sc = PrologueScenes.All[i];
            long t0 = Stopwatch.GetTimestamp();
            string how;
            string header = StoryPictures.Acquire(StoryPictures.DirPrologue, sc.Stem, out how);
            string img = header != null ? header + " (plik " + how + ")" : sc.Fallback + " (ROT - " + how + ")";
            var spec = new StoryWindow.Spec
            {
                Key = sc.Stem, Title = sc.Title(), Upper = sc.Upper(), Lower = sc.Lower(), DoneText = PrologueScenes.Continue(),
                Header = header ?? sc.Fallback, Background = "event-bg", Tag = i, Closed = OnSceneClosed
            };
            string mode = StoryWindow.Show(spec);
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            if (mode == null)
            {
                if (_scOpenFailScene != i) { _scOpenFailScene = i; _scOpenFails = 0; }
                _scOpenFails++;
                Log.Info(ScTag + "scena " + NN(i) + " '" + spec.Title + "' - okno nie otworzylo sie (proba " + _scOpenFails + "/3).");
                if (header != null) StoryPictures.Drop(header);
                if (_scOpenFails >= 3) { _scOpenFails = 0; SkipScene(i, day, "okno nie otwiera sie (3 proby)"); }
                return;
            }
            _scOpenFails = 0; _scOpenFailScene = -1;
            _scShown |= 1 << i;
            _scOpen = i; _scOpenedAt = t;
            Log.Info(ScTag + "scena " + NN(i) + " '" + spec.Title + "' pokazana | dzien " + day.ToString("0.00", Inv) + " (nalezna od " + sc.Day + ") | obrazek " + img
                     + " | okno " + mode + " | gracz: " + Where() + " | pokazane " + Bits(_scShown) + "/" + SceneCount + ", pominiete " + Bits(_scSkipped)
                     + " | " + ms.ToString("0", Inv) + " ms.");
        }

        /// <summary>Zamkniecie okna sceny (przycisk, Esc, zapas, straznik "bez przycisku"). Kolejna scena - po SceneStableSec s spokoju.</summary>
        private static void OnSceneClosed(StoryWindow.Spec s, string how)
        {
            try
            {
                int i = s != null ? s.Tag : -1;
                if (_scOpen < 0 || _scOpen != i) return;
                double sec = (Stopwatch.GetTimestamp() - _scOpenedAt) / (double)Stopwatch.Frequency;
                _scOpen = -1; _scOkSince = 0;
                Log.Info(ScTag + "scena " + NN(i) + " zamknieta po " + sec.ToString("0.0", Inv) + " s (" + how + ").");
                if (s.Header != null && s.Header.StartsWith(StoryPictures.SpritePrefix, StringComparison.Ordinal)) StoryPictures.Drop(s.Header);
            }
            catch (Exception e) { Stumble("Scenes.Closed", e); }
        }

        private static void SkipScene(int i, double day, string why)
        {
            _scSkipped |= 1 << i;
            Log.Info(ScTag + "POMINIETA scena " + NN(i) + " '" + SceneTitle(i) + "' - " + why + " (dzien prologu " + day.ToString("0.00", Inv) + ").");
        }

        private static void SkipPassedLegacy(double day)
        {
            _scLegacy = false;
            int n = 0;
            var all = PrologueScenes.All;
            for (int i = 0; i < all.Length; i++)
                if (!SceneDone(i) && all[i].Day < Math.Floor(day)) { SkipScene(i, day, "zapis prologu sprzed scen (WGRANIE 101) - minione sceny bez serii zaleglych okien"); n++; }
            Log.Info(ScTag + "zapis prologu sprzed scen (bez sc=) - minionych scen pominietych " + n + " (dzien prologu " + day.ToString("0.0", Inv) + ").");
        }

        private static void EndScenes(double day)
        {
            _scEndLogged = true;
            StoryPictures.DropDir(StoryPictures.DirPrologue);
            Log.Info(ScTag + "sceny prologu zakonczone | pokazane " + Bits(_scShown) + "/" + SceneCount + ", pominiete " + Bits(_scSkipped) + " (dzien prologu "
                     + day.ToString("0.0", Inv) + ") | obrazki prologu do zwolnienia | " + StoryWindow.StatText() + " | potkniecia " + _stumbles + ".");
        }

        private static string Where()
        {
            try
            {
                var mp = MobileParty.MainParty;
                var ctx = Campaign.Current != null ? Campaign.Current.CurrentMenuContext : null;
                string menu = ctx != null && ctx.GameMenu != null ? ctx.GameMenu.StringId : "-";
                string place = mp != null && mp.CurrentSettlement != null ? mp.CurrentSettlement.StringId : "mapa";
                return place + ", menu " + menu + (Hero.MainHero != null && Hero.MainHero.IsPrisoner ? ", w niewoli" : "");
            }
            catch { return "-"; }
        }

        /// <summary>OnSessionLaunched (nowa kampania i wczytanie): linia startu scen (tylko kampania z prologiem).</summary>
        internal static void ScenesStartLine()
        {
            if (!Active) return;
            int next = -1;
            for (int i = 0; i < SceneCount; i++) if (!SceneDone(i)) { next = i; break; }
            Log.Info(ScTag + "start sesji | klucz Prologue Scenes " + (ScenesKey ? "TAK" : "NIE") + " | sceny pokazane " + Bits(_scShown) + "/" + SceneCount
                     + ", pominiete " + Bits(_scSkipped) + (_scLegacy ? " (zapis sprzed scen - minione pominiete przy pierwszym ticku mapy)" : "")
                     + " | nastepna " + (next < 0 ? "-" : NN(next) + " (dzien " + PrologueScenes.All[next].Day + ")")
                     + " | " + StoryPictures.StartText() + " | potkniecia " + _stumbles + ".");
        }

        /// <summary>SubModuleMain.OnGameEnd: okno i obrazki od razu (wyjscie z kampanii).</summary>
        internal static void ScenesGameEnd()
        {
            _scOpen = -1; _scOkSince = 0;
            StoryWindow.Reset();
            StoryPictures.ReleaseAll("koniec gry");
        }
    }
}
