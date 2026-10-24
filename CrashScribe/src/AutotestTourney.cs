using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using MapState = TaleWorlds.CampaignSystem.GameState.MapState;
using MenuContext = TaleWorlds.CampaignSystem.GameState.MenuContext;

namespace CrashScribe
{
    /// <summary>
    /// m113-turniej (24.10, zadanie "na pozniej" z biegu bitwy: w The Eyrie trwa turniej, wiec "Practice fight" jest wylaczone i autotest
    /// pisal "AUTOTEST ARENA NIE RUSZYLA ... w miescie trwa turniej"; Jeff: AI pojedynku trzeba sprawdzic w prawdziwym turnieju).
    /// Klucze przelacznika: "tournament" (true = etap turnieju; bez klucza etapu nie ma - autotest jak dotad) i "tournament_s"
    /// (twardy limit calego turnieju w s rzeczywistych, 900; 120-3600). Skrypty: autotest-bitwa.ps1 / bieg-bitwa.ps1 -Tournament [-TournamentSec S].
    ///
    /// KIEDY: na poczatku etapu bitew (po osadzeniu w miescie, przed pierwsza doba), PRZED walka cwiczebna i bitwami w polu. Po turnieju
    /// arena i bitwy jak dotad (turniej znika z miasta w chwili dolaczenia - TournamentManager.OnPlayerJoinTournament - wiec "Practice fight"
    /// jest potem czynne). Przy "battles" 0 tylko turniej, potem zwykly bieg dob.
    ///
    /// JAK (to samo, co gracz): "Stop waiting" -> "Go to the arena" [town_arena] -> "Join the tournament" [join_tournament] -> menu
    /// menu_town_tournament_join (opcje TXP "Select Prize" / "Re-roll Prize" pomijane, nagroda jak wylosowana) -> "Join" [mno_tournament_event_1]
    /// (TournamentGame.PrepareForTournamentGame(true) -> misja turnieju z kodem wszystkich modow: TournamentBehavior, TournamentFightMissionController,
    /// TXPTournamentMissionController, GrandTourney). Miedzy meczami ekran turnieju (MissionGauntletTournamentView, TournamentVM) - przyciski
    /// jak gracz: mecz gracza "Join" (ExecuteJoinTournament), inne mecze "Watch" (ExecuteWatchRound - walka SI z SI na zywo, najlepszy
    /// sprawdzian AI pojedynku) dopoki gracz jest w turnieju i minela mniej niz polowa limitu, potem "Skip" (ExecuteSkipRound - wynik z symulacji
    /// gry). Po odpadnieciu gracza reszta meczy "Skip", az gra pokaze ekran konca (TournamentVM.IsOver) - wtedy "Leave" (ExecuteLeave).
    ///
    /// BOHATER W SWOICH MECZACH POD SI: Agent.Controller = AI i WatchState Alarmed (tak gra stawia pozostalych uczestnikow,
    /// TournamentFightMissionController.SpawnAgentWithRandomItems). Uwaga: Armoury DuelAi/VeteranAi pomija agenta gracza (IsMainAgent), wiec
    /// bohater walczy SI gry/RBM, a przeciwnicy profilem pojedynku. Bez przewijania misji - czasy reakcji jak u gracza.
    ///
    /// LIMITY: mecz najwyzej 180 s rzeczywistych - potem jak gracz Tab -> "Yes" (TournamentFightMissionController.SkipMatch -> TournamentBehavior.SkipMatch,
    /// wynik z symulacji, gracz w tym meczu odpada); caly turniej "tournament_s" - potem (albo przy przerwaniu bitew / miekkim limicie biegu) wyjscie
    /// jak gracz: "Leave" + "Yes" w oknie rezygnacji (TournamentVM.EndTournamentMission: EndTournamentViaLeave + Mission.EndMission); +90 s i
    /// misja dalej otwarta - wyjscie wymuszone. Koniec biegu w trakcie turnieju (Autotest.Finish -> Abort) - to samo wyjscie, potem zapis z mapy.
    ///
    /// LINIE: "AUTOTEST TURNIEJ ..." (START, MECZ n START / postep / KONIEC / POMINIETY, ekran konca, KONIEC, NIE RUSZYL); bledy jak w bitwach -
    /// "AUTOTEST BITWA BLAD ..." (skrypt liczy je tak samo).
    /// </summary>
    internal static partial class AutotestBattle
    {
        internal static bool TourOn;                 // "tournament": turniej w miescie startu
        internal static int TourSec = 900;           // "tournament_s": twardy limit calego turnieju (s rzeczywiste od startu misji)
        private const double TourGoLimitS = 120.0, TourLoadLimitS = 300.0, TourMatchS = 180.0, TourAfterLimitS = 150.0;
        private const double TourQuietS = 2.0, TourWatchShare = 0.5, TourUiLimitS = 20.0;
        private const int TourMaxMatches = 40;       // bezpiecznik: turniej gry ma najwyzej 15 meczy (16 uczestnikow, 4 rundy)

        private static bool _tourStarted, _tourKnown, _tourChecked, _tourOverSeen, _tourLeft, _tourMatchForced;
        private static int _tourNot, _tourDone, _tourNo, _tourFought, _tourWonM, _tourWatched, _tourSkipped, _tourForced, _tourAi, _tourRound;
        private static double _tourAt, _tourQuietSince = -1, _tourLastAct, _tourNoUiSince = -1, _tourNoMatchSince = -1, _tourMatchAt, _tourLeaveAt, _tourIdleSince = -1;
        private static TournamentMatch _tourMatch;
        private static string _tourHow, _tourResult, _tourWinner;
        private static float _tourT0;
        private static int _h0Melee, _h0Missile, _h0Unc, _h0Killed;
        private static object _tourView;             // zachowanie misji z polem TournamentVM (MissionGauntletTournamentView)
        private static Mission _tourViewOf;

        // ------------------------------------------------------------------ przelacznik i opis

        internal static void ParseTour(string json)
        {
            string raw = Autotest.Raw(json, "tournament");
            TourOn = raw != null && (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) || raw == "1");
            TourSec = Autotest.Int(json, "tournament_s", 900, 120, 3600);
        }

        private static string TourDescribe()
        {
            if (!TourOn) return "";
            return "turniej w miescie startu (\"Join the tournament\", mecze gracza do odpadniecia albo wygranej - bohater pod SI, inne mecze ogladane do "
                   + (int)(TourSec * TourWatchShare) + " s, potem pominiete; mecz do " + (int)TourMatchS + " s, turniej do " + TourSec + " s)";
        }

        private static string TourSummary()
        {
            if (!TourOn) return "";
            return " | turniej " + (_tourDone > 0 ? "KONIEC (meczy " + _tourNo + ", gracz walczyl " + _tourFought + ", wygral " + _tourWonM + (_tourResult != null ? ", " + _tourResult : "") + ")"
                                   : _tourNot > 0 ? "NIE RUSZYL" : "nie doszedl");
        }

        private static bool IsTour() { return _ph >= Ph.TourGo && _ph <= Ph.TourAfter; }

        /// <summary>Fazy z otwarta (albo otwierana) misja turnieju.</summary>
        private static bool TourMission() { return _ph == Ph.TourLoad || _ph == Ph.TourRun || _ph == Ph.TourEnd; }

        // ------------------------------------------------------------------ wyjscia z etapu turnieju

        private static string TourNextName()
        {
            return Battles > 0 ? (ArenaOn ? "walka cwiczebna na arenie i bitwy w polu" : "bitwy w polu") : "zwykly bieg dob";
        }

        /// <summary>Turniej nie ruszyl: linia, misja (jesli nasza) zamknieta jak "Leave", dalej arena / bitwy / bieg dob.</summary>
        private static void TourNot(double t, string reason)
        {
            _tourNot++;
            _expectMission = false;
            Note("AUTOTEST TURNIEJ NIE RUSZYL: " + reason + " - dalej " + TourNextName(), true);
            var m = Mission.Current;
            if (m != null && ReferenceEquals(m, _mission) && !m.MissionEnded) TourQuit(m, "turniej nie ruszyl");
            NextAfterTour(t);
        }

        private static void NextAfterTour(double t)
        {
            ResetBattle();
            if (Battles > 0) Go(ArenaOn ? Ph.ArenaGo : Ph.Leave, t);
            else Over(t);
        }

        // ------------------------------------------------------------------ odbicia: zachowania misji turnieju i ekran

        private static object Beh(Mission m, string typeName)
        {
            if (m == null) return null;
            foreach (var b in m.MissionBehaviors)
            {
                if (b == null) continue;
                for (var ty = b.GetType(); ty != null; ty = ty.BaseType)
                    if (ty.Name == typeName) return b;
            }
            return null;
        }

        private static object TourBeh(Mission m) { return Beh(m, "TournamentBehavior"); }

        /// <summary>TournamentVM z widoku misji (szukany po typie pola - widok podmieniony przez inny mod tez sie liczy).</summary>
        private static object TourVm(Mission m)
        {
            if (m == null) return null;
            if (!ReferenceEquals(_tourViewOf, m)) { _tourView = null; _tourViewOf = m; }
            if (_tourView == null)
            {
                foreach (var b in m.MissionBehaviors)
                {
                    if (b != null && AutotestUi.FieldOfType(b, "TournamentVM") != null) { _tourView = b; break; }
                }
            }
            return _tourView != null ? AutotestUi.FieldOfType(_tourView, "TournamentVM") : null;
        }

        private static bool HasMember(object o, string name)
        {
            if (o == null) return false;
            const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            for (var ty = o.GetType(); ty != null; ty = ty.BaseType)
                if (ty.GetProperty(name, f | BindingFlags.DeclaredOnly) != null || ty.GetField(name, f | BindingFlags.DeclaredOnly) != null) return true;
            return false;
        }

        /// <summary>Sprawdzenie odbic (raz na misje): czego brak - lista nazw; pusta = wszystko jest.</summary>
        private static List<string> TourMissing(object beh, object vm, object fight)
        {
            var miss = new List<string>();
            foreach (var n in new[] { "ExecuteJoinTournament", "ExecuteWatchRound", "ExecuteSkipRound", "ExecuteLeave", "EndTournamentMission" })
                if (!AutotestUi.Has(vm, n)) miss.Add("TournamentVM." + n + "()");
            foreach (var n in new[] { "IsCurrentMatchActive", "IsOver" })
                if (!HasMember(vm, n)) miss.Add("TournamentVM." + n);
            foreach (var n in new[] { "CurrentMatch", "CurrentRoundIndex", "IsPlayerEliminated", "IsPlayerParticipating", "Winner" })
                if (!HasMember(beh, n)) miss.Add("TournamentBehavior." + n);
            if (!AutotestUi.Has(beh, "EndTournamentViaLeave")) miss.Add("TournamentBehavior.EndTournamentViaLeave()");
            if (fight == null || !AutotestUi.Has(fight, "SkipMatch")) miss.Add("TournamentFightMissionController.SkipMatch()");
            return miss;
        }

        private static int RoundOf(object beh)
        {
            var v = AutotestUi.Get(beh, "CurrentRoundIndex");
            return v is int ? (int)v : -1;
        }

        private static string Name(TournamentParticipant p)
        {
            if (p == null || p.Character == null) return "?";
            return p.Character.Name + (p.Character == CharacterObject.PlayerCharacter ? " (gracz)" : "");
        }

        private static string Names(IEnumerable<TournamentParticipant> ps)
        {
            var parts = new List<string>();
            if (ps != null) foreach (var p in ps) parts.Add(Name(p));
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "brak";
        }

        /// <summary>Uczestnicy meczu druzynami: "A, B vs C, D".</summary>
        private static string Who(TournamentMatch mt)
        {
            if (mt == null) return "mecz ?";
            var teams = new List<string>();
            foreach (var tm in mt.Teams) teams.Add(Names(tm.Participants));
            return "uczestnicy: " + string.Join(" vs ", teams.ToArray());
        }

        private static string OptTip(MenuContext ctx, string optionId)
        {
            try
            {
                var gm = Campaign.Current.GameMenuManager;
                int n = gm.GetVirtualMenuOptionAmount(ctx);
                for (int i = 0; i < n; i++)
                {
                    var opt = gm.GetVirtualGameMenuOption(ctx, i);
                    if (opt == null || opt.IdString != optionId) continue;
                    var tip = gm.GetVirtualMenuOptionTooltip(ctx, i);
                    string s = tip != null ? tip.ToString() : "";
                    return s.Length > 0 ? ", podpowiedz gry: \"" + Autotest.OneLine(s, 160) + "\"" : "";
                }
            }
            catch { }
            return "";
        }

        /// <summary>Krok w ochronie Guard; wyjatek moda (Guard opisal go linia i rzucil dalej) tu sie konczy - wyjscie z turnieju ma dojsc do konca.</summary>
        private static void Safe(string where, Action a)
        {
            try { Guard(where, a); }
            catch (Exception) { }
        }

        // ------------------------------------------------------------------ 0. droga do turnieju (gracz czeka w miescie)

        private static void StepTourGo(Campaign c, double t)
        {
            if (t - _phAt > TourGoLimitS) { TourNot(t, "nie doszedl do turnieju w " + (int)TourGoLimitS + " s (" + Autotest.Where() + ")"); return; }
            if (_stopWhy != null) { TourNot(t, "bitwy przerwane: " + _stopWhy); return; }
            StopTime(c);
            if (AutotestUi.AnyWindowOpen()) return;
            if (Talking(c))
            {
                if (t - _actAt > 2.5) { _actAt = t; Note("AUTOTEST TURNIEJ: rozmowa w drodze do turnieju - koncze (EndConversation)", true); c.ConversationManager.EndConversation(); }
                return;
            }
            if (!(Autotest.ActiveState() is MapState)) return;
            var mp = MobileParty.MainParty;
            var hero = Hero.MainHero;
            if (mp == null || hero == null) return;
            var town = mp.CurrentSettlement;
            if (town == null || !town.IsTown || town.Town == null) { TourNot(t, "gracz nie jest w miescie (" + Autotest.Where() + ")"); return; }
            TournamentGame tg = null;
            try { tg = c.TournamentManager.GetTournamentGame(town.Town); } catch { }
            if (!_tourKnown)
            {
                _tourKnown = true;
                Note("AUTOTEST TURNIEJ: " + town.Name + " [" + town.StringId + "] turniej w miescie: "
                     + (tg != null ? "TAK (" + tg.GetType().Name + ", nagroda " + (tg.Prize != null ? tg.Prize.Name.ToString() : "brak") + ", od " + tg.CreationTime.ElapsedDaysUntilNow.ToString("0.0", Inv) + " dob)" : "NIE"), true);
                if (tg == null) { TourNot(t, "w miescie " + town.Name + " nie ma turnieju (TournamentManager.GetTournamentGame = null)"); return; }
            }
            var ctx = c.CurrentMenuContext;
            var menu = ctx != null ? ctx.GameMenu : null;
            if (t - _actAt < 1.5 || menu == null) return;
            _actAt = t;
            string id = menu.StringId;
            Hero_Heal(hero, "ranny nie moze dolaczyc do turnieju");
            if (id == "town_wait_menus")
            {
                if (Autotest.ClickOption(ctx, "wait_leave")) Note("AUTOTEST TURNIEJ: klikam \"Stop waiting\" [wait_leave] w " + town.Name, true);
                return;
            }
            if (id == "town")
            {
                if (Autotest.ClickOption(ctx, "town_arena")) Note("AUTOTEST TURNIEJ: klikam \"Go to the arena\" [town_arena]", true);
                else TourNot(t, "w menu town opcja town_arena niedostepna (opcje " + Autotest.ListOptions(ctx) + ")");
                return;
            }
            if (id == "town_arena")
            {
                if (tg == null) { TourNot(t, "turniej zniknal z miasta (opcje " + Autotest.ListOptions(ctx) + ")"); return; }
                if (Autotest.ClickOption(ctx, "join_tournament")) Note("AUTOTEST TURNIEJ: menu town_arena, opcje " + Autotest.ListOptions(ctx) + " - klikam \"Join the tournament\" [join_tournament]", true);
                else TourNot(t, "opcja join_tournament niedostepna (opcje " + Autotest.ListOptions(ctx) + OptTip(ctx, "join_tournament") + ")");
                return;
            }
            if (id == "menu_town_tournament_join")
            {
                _expectMission = true;
                _mission = null;
                Go(Ph.TourLoad, t);
                Note("AUTOTEST TURNIEJ: menu menu_town_tournament_join, opcje " + Autotest.ListOptions(ctx) + " - klikam \"Join\" [mno_tournament_event_1] (to samo, co gracz: TournamentGame.PrepareForTournamentGame(true) + TournamentManager.OnPlayerJoinTournament; nagroda bez zmian, TXP Select/Re-roll pominiete)", true);
                if (!Autotest.ClickOption(ctx, "mno_tournament_event_1"))
                {
                    _expectMission = false;
                    TourNot(t, "opcja mno_tournament_event_1 niedostepna (opcje " + Autotest.ListOptions(ctx) + OptTip(ctx, "mno_tournament_event_1") + ")");
                }
                return;
            }
            // inne podmenu miasta: "Back" do menu town
            if (!Autotest.ClickLeave(ctx) && MenusNoted.Add(id)) Note("AUTOTEST TURNIEJ: menu " + id + " bez wyjscia - czekam (opcje " + Autotest.ListOptions(ctx) + ")", false);
        }

        // ------------------------------------------------------------------ 1. ladowanie misji turnieju

        private static void StepTourLoad(double t)
        {
            if (t - _phAt > TourLoadLimitS) { TourNot(t, "misja turnieju nie ruszyla w " + (int)TourLoadLimitS + " s (" + Autotest.Where() + ")"); return; }
            var m = Mission.Current;
            if (m == null)
            {
                if (Autotest.ActiveState() is MapState && t - _phAt > 25)
                    TourNot(t, "po \"Join\" misja nie ruszyla w 25 s (" + Autotest.Where() + (AutotestUi.AnyWindowOpen() ? ", okno: " + AutotestUi.OpenWindowName() : "") + ")");
                return;
            }
            if (_mission == null) _mission = m;
            if (!ReferenceEquals(m, _mission)) { TourNot(t, "na wierzchu obca misja (" + Autotest.Where() + ")"); return; }
            if (m.CurrentState != Mission.State.Continuing) return;
            if (_log == null || !m.MissionBehaviors.Contains(_log))
            {
                _log = new AutotestBattleLog();
                m.AddMissionBehavior(_log);
                Note("AUTOTEST TURNIEJ: liczniki dolaczone pozno - ciosy liczone od teraz", true);
            }
            _expectMission = false;
            _log.Scene = SceneOf(m);
            var beh = TourBeh(m);
            var fight = Beh(m, "TournamentFightMissionController");
            bool txp = Beh(m, "TXPTournamentMissionController") != null;
            var tg = beh != null ? AutotestUi.Get(beh, "TournamentGame") as TournamentGame : null;
            _tourStarted = true;
            _tourAt = t;
            _tourQuietSince = _tourNoUiSince = _tourNoMatchSince = -1;
            _tourLastAct = 0;
            _tourChecked = false;
            _fightAt = t;
            _lastProgress = t;
            Note("AUTOTEST TURNIEJ START: misja ruszyla po " + (t - _phAt).ToString("0", Inv) + " s ladowania (scena " + _log.Scene + ", " + m.GetType().Name + ", zachowan " + m.MissionBehaviors.Count
                 + ") | TournamentBehavior " + (beh != null ? "tak" : "BRAK") + ", walki " + (fight != null ? fight.GetType().Name : "BRAK") + ", obserwator TXP " + (txp ? "tak" : "nie")
                 + " | gra " + (tg != null ? tg.GetType().Name : "?") + ", nagroda " + (tg != null && tg.Prize != null ? tg.Prize.Name.ToString() : "brak")
                 + ", gracz uczestniczy " + (beh != null && Flag(beh, "IsPlayerParticipating") ? "tak" : "NIE")
                 + " | limit " + TourSec + " s (mecz " + (int)TourMatchS + " s), inne mecze ogladane do " + (int)(TourSec * TourWatchShare)
                 + " s | bohater w swoich meczach pod SI (Agent.Controller = AI, WatchState Alarmed), bez przewijania", true);
            Go(Ph.TourRun, t);
        }

        // ------------------------------------------------------------------ 2. turniej: ekran miedzy meczami i mecze

        private static void StepTourRun(Campaign c, double t)
        {
            var m = Mission.Current;
            if (m == null || !ReferenceEquals(m, _mission)) { TourKoniec(t, "misja turnieju zamknieta poza autotestem"); Go(Ph.TourAfter, t); return; }
            Track(m);
            if (m.MissionEnded) { _endForced = false; Go(Ph.TourEnd, t); return; }
            // twardy limit calosci: wyjscie jak gracz nie zamknelo misji
            if (t - _tourAt > TourSec + 90)
            {
                Err("turniej: misja otwarta " + (TourSec + 90) + " s mimo wyjscia (" + Autotest.Where() + ") - wyjscie wymuszone");
                TourQuit(m, "twardy limit turnieju");
                _endForced = true;
                Go(Ph.TourEnd, t);
                return;
            }
            var beh = TourBeh(m);
            var vm = TourVm(m);
            var fight = Beh(m, "TournamentFightMissionController");
            if (beh == null || vm == null)
            {
                if (_tourNoUiSince < 0) _tourNoUiSince = t;
                else if (t - _tourNoUiSince > TourUiLimitS)
                {
                    Err("turniej: brak " + (beh == null ? "TournamentBehavior" : "ekranu turnieju (pole TournamentVM w widoku misji)") + " przez " + (int)TourUiLimitS + " s - wyjscie");
                    TourQuit(m, "brak ekranu turnieju");
                    _endForced = true;
                    Go(Ph.TourEnd, t);
                }
                return;
            }
            _tourNoUiSince = -1;
            if (!_tourChecked)
            {
                _tourChecked = true;
                var miss = TourMissing(beh, vm, fight);
                Note("AUTOTEST TURNIEJ: ekran turnieju " + vm.GetType().Name + " w " + (_tourView != null ? _tourView.GetType().Name : "?") + " | odbicia: "
                     + (miss.Count == 0 ? "wszystkie metody i pola sa" : "BRAK " + string.Join(", ", miss.ToArray())), true);
                if (miss.Count > 0)
                {
                    Err("turniej: brak metod/pol gry (" + string.Join(", ", miss.ToArray()) + ") - wyjscie");
                    TourQuit(m, "brak metod gry");
                    _endForced = true;
                    Go(Ph.TourEnd, t);
                    return;
                }
            }
            string stop = _stopWhy != null ? "przerwanie (" + _stopWhy + ")" : t - _tourAt > TourSec ? "limit turnieju " + TourSec + " s" : null;
            if (_tourMatch != null) { TourMatchTick(m, beh, vm, t, stop); return; }
            if (Flag(vm, "IsCurrentMatchActive")) return;   // mecz rusza albo ekran jeszcze sie nie odswiezyl
            if (Talking(c))
            {
                _tourQuietSince = -1;
                if (t - _actAt > 2.5) { _actAt = t; Note("AUTOTEST TURNIEJ: rozmowa w misji turnieju - koncze (EndConversation)", true); c.ConversationManager.EndConversation(); }
                return;
            }
            if (AutotestUi.AnyWindowOpen()) { _tourQuietSince = -1; return; }
            if (_tourQuietSince < 0) { _tourQuietSince = t; return; }
            if (t - _tourQuietSince < TourQuietS || t - _tourLastAct < 1.0) return;
            _tourLastAct = t;
            if (Flag(vm, "IsOver")) { TourEndScreen(beh, vm, t); return; }
            if (stop != null) { TourLeave(vm, t, stop); return; }
            if (_tourNo >= TourMaxMatches) { TourLeave(vm, t, "bezpiecznik: " + _tourNo + " meczy"); return; }
            var cur = AutotestUi.Get(beh, "CurrentMatch") as TournamentMatch;
            if (cur == null)
            {
                // brak meczu, a ekranu konca nie ma - gra nie zamknela turnieju sama
                if (_tourNoMatchSince < 0) _tourNoMatchSince = t;
                else if (t - _tourNoMatchSince > 10) TourLeave(vm, t, "brak biezacego meczu i ekranu konca przez 10 s");
                return;
            }
            _tourNoMatchSince = -1;
            bool playerIn = Flag(beh, "IsPlayerParticipating") && !Flag(beh, "IsPlayerEliminated");
            if (cur.IsPlayerParticipating()) { TourStartMatch(m, beh, vm, cur, t, "walka gracza (bohater pod SI)", "ExecuteJoinTournament"); return; }
            if (playerIn && t - _tourAt < TourSec * TourWatchShare) { TourStartMatch(m, beh, vm, cur, t, "ogladanie (SI z SI)", "ExecuteWatchRound"); return; }
            TourSkip(beh, vm, cur, playerIn ? "polowa limitu turnieju minela" : "gracz juz odpadl");
        }

        private static void TourStartMatch(Mission m, object beh, object vm, TournamentMatch cur, double t, string how, string method)
        {
            _tourNo++;
            _tourMatch = cur;
            _tourHow = how;
            _tourMatchAt = t;
            _tourMatchForced = false;
            _tourAi = 0;
            _tourRound = RoundOf(beh);
            _tourT0 = m.CurrentTime;
            var L = _log;
            _h0Melee = L != null ? L.Melee : 0;
            _h0Missile = L != null ? L.Missile : 0;
            _h0Unc = L != null ? L.Unconscious : 0;
            _h0Killed = L != null ? L.Killed : 0;
            _lastProgress = t;
            Note("AUTOTEST TURNIEJ MECZ " + _tourNo + " (runda " + (_tourRound + 1) + "/4) START: " + how + " | " + Who(cur) + " | przycisk jak gracz: TournamentVM." + method, true);
            AutotestUi.Call(vm, method);
        }

        private static void TourMatchTick(Mission m, object beh, object vm, double t, string stop)
        {
            var mt = _tourMatch;
            if (mt.State == TournamentMatch.MatchState.Finished) { TourMatchEnd(m, t); return; }
            if (mt.State == TournamentMatch.MatchState.Ready)
            {
                if (t - _tourMatchAt > 10)
                {
                    Err("turniej: mecz " + _tourNo + " nie ruszyl 10 s po przycisku (" + _tourHow + ")");
                    _tourMatch = null;
                    _tourQuietSince = -1;
                }
                return;
            }
            bool mine = mt.IsPlayerParticipating();
            if (mine)
            {
                Sub("turniej bohater SI", () =>
                {
                    var ma = m.MainAgent;
                    if (ma == null || !ma.IsActive() || ma.Controller != AgentControllerType.Player) return;
                    ma.Controller = AgentControllerType.AI;
                    ma.SetWatchState(Agent.WatchState.Alarmed);
                    _tourAi++;
                    if (_tourAi <= 3) Note("AUTOTEST TURNIEJ MECZ " + _tourNo + ": bohater \"" + ma.Name + "\" pod kontrola SI (Agent.Controller = AI, WatchState Alarmed - jak pozostali uczestnicy)" + (_tourAi > 1 ? " - ponownie (" + _tourAi + ")" : ""), true);
                });
            }
            if (!_tourMatchForced && (stop != null || t - _tourMatchAt > TourMatchS))
            {
                _tourMatchForced = true;
                Note("AUTOTEST TURNIEJ MECZ " + _tourNo + ": " + (stop ?? "limit meczu " + (int)TourMatchS + " s") + " w trakcie walki - jak gracz: Tab -> \"Yes\" (TournamentFightMissionController.SkipMatch -> TournamentBehavior.SkipMatch, wynik z symulacji gry"
                     + (mine ? ", gracz w tym meczu odpada" : "") + ")", true);
                TourSkipLive(m, beh);
                return;
            }
            if (_tourMatchForced && t - _tourMatchAt > TourMatchS + 30)
            {
                Err("turniej: mecz " + _tourNo + " nie skonczyl sie 30 s po pominieciu (" + Autotest.Where() + ") - wyjscie");
                _tourMatch = null;
                TourQuit(m, "mecz nie konczy sie");
                _endForced = true;
                Go(Ph.TourEnd, t);
                return;
            }
            if (t - _lastProgress >= ProgressEveryS)
            {
                _lastProgress = t;
                var L = _log ?? new AutotestBattleLog();
                int alive = 0;
                foreach (Agent a in m.Agents) if (a != null && a.IsHuman && a.IsActive()) alive++;
                Note("AUTOTEST TURNIEJ MECZ " + _tourNo + " postep: czas meczu " + (m.CurrentTime - _tourT0).ToString("0", Inv) + " s (rzeczywisty " + (t - _tourMatchAt).ToString("0", Inv) + " s), ciosy wrecz "
                     + (L.Melee - _h0Melee) + ", strzaly " + (L.Missile - _h0Missile) + ", nieprzytomni " + (L.Unconscious - _h0Unc) + " | zywi ludzie " + alive + ", bohater " + HeroState(m), false);
            }
        }

        private static void TourMatchEnd(Mission m, double t)
        {
            var mt = _tourMatch;
            _tourMatch = null;
            _tourQuietSince = -1;
            bool mine = mt.IsPlayerParticipating();
            bool won = mine && mt.IsPlayerWinner();
            if (mine)
            {
                _tourFought++;
                if (won) _tourWonM++;
                else if (_tourResult == null) _tourResult = "odpadl w rundzie " + (_tourRound + 1) + "/4";
            }
            else _tourWatched++;
            if (_tourMatchForced) _tourForced++;
            var L = _log ?? new AutotestBattleLog();
            Note("AUTOTEST TURNIEJ MECZ " + _tourNo + " KONIEC (runda " + (_tourRound + 1) + "/4, " + _tourHow + (_tourMatchForced ? ", PRZERWANY limitem - wynik z symulacji" : "") + "): zwyciezcy " + Names(mt.Winners)
                 + " | gracz " + (mine ? (won ? "WYGRAL" : "przegral") : "nie gral") + " | ciosy wrecz " + (L.Melee - _h0Melee) + ", strzaly " + (L.Missile - _h0Missile)
                 + ", nieprzytomni " + (L.Unconscious - _h0Unc) + ", zabici " + (L.Killed - _h0Killed) + ", czas meczu " + (m.CurrentTime - _tourT0).ToString("0", Inv)
                 + " s, rzeczywisty " + (t - _tourMatchAt).ToString("0", Inv) + " s", true);
        }

        /// <summary>Mecz bez gracza pominiety jak gracz ("Skip"): wynik z symulacji gry, od razu.</summary>
        private static void TourSkip(object beh, object vm, TournamentMatch cur, string why)
        {
            _tourNo++;
            _tourSkipped++;
            int r = RoundOf(beh);
            string who = Who(cur);
            AutotestUi.Call(vm, "ExecuteSkipRound");
            Note("AUTOTEST TURNIEJ MECZ " + _tourNo + " (runda " + (r + 1) + "/4) POMINIETY jak gracz (przycisk Skip: TournamentVM.ExecuteSkipRound, wynik z symulacji gry; " + why + ") | " + who
                 + " | zwyciezcy " + (cur.State == TournamentMatch.MatchState.Finished ? Names(cur.Winners) : "? (stan " + cur.State + ")"), true);
        }

        /// <summary>Mecz trwa: jak gracz Tab -> "Yes" (TournamentFightMissionController.SkipMatch()); bez niego TournamentBehavior.SkipMatch(false).</summary>
        private static void TourSkipLive(Mission m, object beh)
        {
            var fight = Beh(m, "TournamentFightMissionController");
            if (fight != null && AutotestUi.Has(fight, "SkipMatch")) { AutotestUi.Call(fight, "SkipMatch"); return; }
            var mi = beh != null ? beh.GetType().GetMethod("SkipMatch", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null) : null;
            if (mi == null) throw new MissingMethodException("TournamentBehavior", "SkipMatch(bool)");
            mi.Invoke(beh, new object[] { false });
        }

        private static void TourEndScreen(object beh, object vm, double t)
        {
            if (!_tourOverSeen)
            {
                _tourOverSeen = true;
                _tourLeaveAt = t;
                var w = AutotestUi.Get(beh, "Winner") as TournamentParticipant;
                bool me = w != null && w.Character == CharacterObject.PlayerCharacter;
                _tourWinner = Name(w);
                if (me) _tourResult = "ZWYCIEZCA turnieju";
                Note("AUTOTEST TURNIEJ: ekran konca turnieju (TournamentVM.IsOver) - zwyciezca " + _tourWinner + ", wynik gracza " + (_tourResult ?? "?") + " | za 3 s wyjscie jak gracz: \"Leave\" (TournamentVM.ExecuteLeave)", true);
                return;
            }
            if (_tourLeft || t - _tourLeaveAt < 3.0) return;
            _tourLeft = true;
            _endForced = false;
            Go(Ph.TourEnd, t);   // przed klikiem: wyjatek w ExecuteLeave nie zostawia etapu w TourRun (TourEnd po 15 s wymusza wyjscie)
            AutotestUi.Call(vm, "ExecuteLeave");
        }

        /// <summary>Wyjscie przed koncem turnieju jak gracz: "Leave" + "Yes" w oknie rezygnacji (TournamentVM.EndTournamentMission).</summary>
        private static void TourLeave(object vm, double t, string why)
        {
            _tourLeft = true;
            if (_tourResult == null) _tourResult = "opuscil turniej (" + why + ")";
            Note("AUTOTEST TURNIEJ: " + why + " - wyjscie jak gracz: \"Leave\" + \"Yes\" w oknie rezygnacji (TournamentVM.EndTournamentMission: TournamentBehavior.EndTournamentViaLeave - reszta meczy z symulacji gry, potem Mission.EndMission)", true);
            _endForced = false;
            Go(Ph.TourEnd, t);   // przed klikiem (jak wyzej)
            AutotestUi.Call(vm, "EndTournamentMission");
        }

        /// <summary>
        /// Wyjscie awaryjne (koniec biegu, brak ekranu, misja sie nie zamyka): trwajacy mecz pominiety jak Tab -> "Yes", reszta turnieju jak "Leave"
        /// (TournamentVM.EndTournamentMission albo TournamentBehavior.EndTournamentViaLeave), na koniec Mission.EndMission. Kazdy krok osobno.
        /// </summary>
        private static void TourQuit(Mission m, string why)
        {
            if (m == null) return;
            Safe("turniej wyjscie", () =>
            {
                var beh = TourBeh(m);
                var cur = beh != null ? AutotestUi.Get(beh, "CurrentMatch") as TournamentMatch : null;
                if (cur != null && cur.State == TournamentMatch.MatchState.Started) TourSkipLive(m, beh);
                var vm = TourVm(m);
                if (vm != null && AutotestUi.Has(vm, "EndTournamentMission"))
                {
                    Note("AUTOTEST TURNIEJ: " + why + " - wyjscie jak \"Leave\" gracza (TournamentVM.EndTournamentMission)", true);
                    AutotestUi.Call(vm, "EndTournamentMission");
                }
                else if (beh != null)
                {
                    Note("AUTOTEST TURNIEJ: " + why + " - wyjscie: TournamentBehavior.EndTournamentViaLeave (bez ekranu turnieju)", true);
                    AutotestUi.Call(beh, "EndTournamentViaLeave");
                }
            });
            Safe("turniej EndMission", () =>
            {
                if (!m.MissionEnded) { Note("AUTOTEST TURNIEJ: " + why + " - Mission.EndMission()", true); m.EndMission(); }
            });
        }

        // ------------------------------------------------------------------ 3. zamkniecie misji turnieju

        private static void StepTourEnd(Campaign c, double t)
        {
            var m = Mission.Current;
            if (m != null && ReferenceEquals(m, _mission))
            {
                Track(m);
                if (Talking(c) && t - _actAt > 2.5) { _actAt = t; Note("AUTOTEST TURNIEJ: rozmowa przy wyjsciu - koncze", false); c.ConversationManager.EndConversation(); }
                if (!_endForced && t - _phAt > 15)
                {
                    _endForced = true;
                    Note("AUTOTEST TURNIEJ: misja nie zamknela sie w 15 s - wyjscie wymuszone", true);
                    TourQuit(m, "misja nie zamknela sie w 15 s");
                }
                if (t - _phAt > EndingLimitS)
                {
                    Err("misja turnieju nie zamknela sie w " + (int)EndingLimitS + " s (" + Autotest.Where() + ")");
                    StopAll("misja turnieju sie nie zamyka");
                    TourKoniec(t, "misja turnieju nie zamknela sie");
                    Go(Ph.TourAfter, t);
                }
                return;
            }
            TourKoniec(t, null);
            Go(Ph.TourAfter, t);
        }

        private static void TourKoniec(double t, string note)
        {
            if (_tourDone > 0) return;
            _tourDone++;
            var L = _log;
            if (L == null) { L = new AutotestBattleLog(); note = (note != null ? note + "; " : "") + "liczniki NIE byly podlaczone"; }
            Note("AUTOTEST TURNIEJ KONIEC: meczy " + _tourNo + " (gracz walczyl " + _tourFought + ", wygral " + _tourWonM + "; ogladane " + _tourWatched + "; pominiete " + _tourSkipped
                 + "; przerwane limitem " + _tourForced + "), wynik gracza " + (_tourResult ?? "?") + ", zwyciezca turnieju " + (_tourWinner ?? "?")
                 + " | ciosy wrecz " + L.Melee + ", strzaly " + L.Missile + ", nieprzytomni " + L.Unconscious + ", zabici " + L.Killed + ", kolizje wrecz " + L.MeleeColl + " / pociski " + L.MissileColl
                 + ", scena " + L.Scene + ", czas misji " + L.LastTime.ToString("0", Inv) + " s, czas rzeczywisty " + (_tourStarted ? (t - _tourAt).ToString("0", Inv) : "?") + " s, bledy bitwy " + _errors
                 + (note != null ? " | " + note : ""), true);
        }

        // ------------------------------------------------------------------ 4. po turnieju: mapa spokojna (okna, np. wykup zbroi GrandTourney)

        private static void StepTourAfter(Campaign c, double t)
        {
            if (t - _phAt > TourAfterLimitS)
            {
                Note("AUTOTEST TURNIEJ: po turnieju mapa nie wrocila do spokoju w " + (int)TourAfterLimitS + " s (" + Autotest.Where() + (AutotestUi.AnyWindowOpen() ? ", okno: " + AutotestUi.OpenWindowName() : "") + ") - dalej " + TourNextName(), true);
                NextAfterTour(t);
                return;
            }
            if (AutotestUi.AnyWindowOpen()) { _tourIdleSince = -1; return; }
            var st = Autotest.ActiveState();
            if (st is MissionState || Mission.Current != null)
            {
                _tourIdleSince = -1;
                if (Talking(c) && t - _actAt > 3) { _actAt = t; Note("AUTOTEST TURNIEJ: po turnieju rozmowa w misji - koncze", false); c.ConversationManager.EndConversation(); }
                return;
            }
            if (Talking(c))
            {
                _tourIdleSince = -1;
                if (t - _actAt > 2.5) { _actAt = t; Note("AUTOTEST TURNIEJ: po turnieju rozmowa - koncze (EndConversation)", true); c.ConversationManager.EndConversation(); }
                return;
            }
            if (!(st is MapState)) { _tourIdleSince = -1; return; }
            StopTime(c);
            if (_tourIdleSince < 0) { _tourIdleSince = t; return; }
            if (t - _tourIdleSince < 2.0) return;
            var hero = Hero.MainHero;
            Note("AUTOTEST TURNIEJ: po turnieju mapa spokojna po " + (t - _phAt).ToString("0", Inv) + " s (" + Autotest.Where() + ") | bohater " + (hero != null ? hero.HitPoints + "/" + hero.MaxHitPoints + " HP, zloto " + hero.Gold : "?")
                 + " - dalej " + TourNextName(), false);
            NextAfterTour(t);
        }
    }
}
