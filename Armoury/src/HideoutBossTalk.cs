using System;
using System.Reflection;
using HarmonyLib;
using SandBox.Missions.MissionLogics.Hideout;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 293 (Jeff 29.10: "Walczylem w kryjowce, i jak dozedlem do walki z bossem nie odaplil sie dialog, aby zapyyal mnie czy walcze sam czy ze
    /// wszystkimi i jak go atakowalem to nie zadawlaem mu obrazn musiale kliknac retreat aby ywjsc"). ROZMOWA Z HERSZTEM KRYJOWKI.
    ///
    /// Gra (SandBox HideoutMissionController, zasadzka HideoutAmbushMissionController - ten sam przebieg): po oczyszczeniu obozu film
    /// (OnInitialFadeOutOver: PlayerTeam.SetIsEnemyOf(wrog, false) - od tej chwili nikt nikogo nie rani), stan ConversationBetweenLeaders,
    /// MissionConversationLogic.StartConversation(herszt). Walke wznawia TYLKO odpowiedz gracza w rozmowie: wiersz gry
    /// "bandit_hideout_start_defender" (HideoutConversationsCampaignBehavior, priorytet 100) prowadzi do dwoch odpowiedzi, ktorych skutki
    /// dopinaja ConversationEndOneShot += StartBossFightDuelMode / StartBossFightBattleMode. CheckBattleResolved w tym stanie wraca od razu.
    /// Gdy rozmowa zamknie sie innym wierszem - stan stoi do konca misji: druzyny nie sa wrogami, herszt nie bierze obrazen, wyjscie tylko
    /// przez Retreat. Herszt (SelectBossAgent): bohater, inaczej BanditBoss kultury bandyckiej, inaczej czlowiek wroga z NAJWYZSZYM poziomem.
    ///
    /// Dowod (sesja Jeffa 29.10, rgl_log_28248.txt 01:27:50-01:28:23, kryjowka hideout_mountain_4 Hill Tribesmen, scena sea_bandit_c_sv):
    /// "Complete objective: hideout_mission_clear_the_main_camp_objective" 01:27:50, "Conversation character name: Wild Hare Horseman /
    /// id: forest_people_tier_3 / culture:North", "AI -> (guard_start)" ("Can't talk right now. Got to keep my eye on things around here."),
    /// "Conversation End" 01:28:04, brak "defeat_hideout_boss_objective", EndMission 01:28:23 (Retreat). Armoury.log: ludzi band 25
    /// (bohaterow 0) z 4 band - bez herszta-bohatera i bez BanditBoss w skladzie, wiec hersztem zostal najwyzszy poziomem zolnierz Polnocy
    /// (zajecie Soldier). Wiersz GuardsCampaignBehavior "guard_start" (priorytet 100, warunek: zajecie rozmowcy Soldier i
    /// PlayerEncounter.InsideSettlement - w kryjowce prawda) zamknal rozmowe. Dla porownania sesja 24.10 (rgl_log_8428.txt 02:06:06):
    /// herszt robber_boss (zajecie Bandit) -> "bandit_hideout_start_defender" -> wybor -> "defeat_hideout_boss_objective".
    /// Czego log nie mowi: czy wiersz gry mial warunek prawdziwy i przegral kolejnoscia, czy byl falszywy (frakcja kryjowki z pierwszej
    /// partii bandy w osadzie). Dlatego linia misji wypisuje oba warunki gry obok naszego wyboru.
    ///
    /// Zmiana (klucz HideoutBossTalkFix, wylaczony = dokladnie jak dotad):
    ///  1. wiersz NPC "arm_hideout_boss_start" (stan "start" -> "bandit_hideout_defender", tekst gry {=nYCXzAYH}, priorytet ponad kazdy
    ///     wiersz poczatku rozmowy) - warunek: misja kryjowki w stanie ConversationBetweenLeaders i rozmowca to herszt kontrolera.
    ///     Kim herszt jest (bohater, herszt bandy, zolnierz, dezerter, kazde zajecie i kultura) - bez znaczenia. Dalej odpowiedzi GRY
    ///     (pojedynek / wszyscy, ta sama blokada "You don't have any men" i te same skutki).
    ///  2. zapas: stan ConversationBetweenLeaders bez trwajacej rozmowy przez SafetySec (rozmowa zamknieta bez odpowiedzi albo w ogole
    ///     nie ruszyla) -> walka rusza tak, jak dalaby ja gra: ludzie gracza na nogach -> StartBossFightBattleMode (wszyscy), bez ludzi ->
    ///     StartBossFightDuelMode (gra wtedy tez daje tylko pojedynek); gdy proba rzuci - jeszcze jedna. Nigdy herszt nietykalny do konca misji.
    ///  3. linia po kazdej misji kryjowki "Kryjowka - herszt (293)" (sciezka, herszt, warunki gry, wybor) i linia przy zapasie.
    /// Stan tylko w pamieci misji, bez zapisu kampanii.
    /// </summary>
    internal static class HideoutBossTalk
    {
        internal const string Tag = "Kryjowka - herszt (293)";
        // ponad wszystkie wiersze poczatku rozmowy (gra 100-300, nasze do 310) - warunek waski: tylko herszt w fazie rozmowy przywodcow
        internal const int Priority = 100000;
        // zapas: tyle sekund misji stanu rozmowy przywodcow bez trwajacej rozmowy (rozmowa gry rusza w tej samej klatce co stan)
        private const float SafetySec = 1.0f;
        private const string TalkState = "ConversationBetweenLeaders";

        private static FieldInfo _hState, _hBoss, _aState, _aBoss;
        private static bool _resolved;
        private static int _stumbles;

        internal static void AddDialogs(CampaignGameStarter starter)
        {
            try
            {
                if (starter == null) return;
                starter.AddDialogLine("arm_hideout_boss_start", "start", "bandit_hideout_defender",
                    "{=nYCXzAYH}You! You've cut quite a swathe through my men there, damn you. How about we settle this, one-on-one?",
                    BossCond, null, Priority);
                Resolve();
                var s = Settings.Current;
                Log.Info(Tag + ": wiersz rozmowy herszta wpiety (priorytet " + Priority + ") | klucz " + (s != null && s.HideoutBossTalkFix ? "TAK" : "NIE")
                         + " | pola gry: kryjowka " + (_hState != null && _hBoss != null ? "TAK" : "NIE")
                         + ", zasadzka " + (_aState != null && _aBoss != null ? "TAK" : "NIE") + ".");
            }
            catch (Exception e) { Stumble("AddDialogs", e); }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            _hState = AccessTools.Field(typeof(HideoutMissionController), "_hideoutMissionState");
            _hBoss = AccessTools.Field(typeof(HideoutMissionController), "_bossAgent");
            _aState = AccessTools.Field(typeof(HideoutAmbushMissionController), "_currentHideoutMissionState");
            _aBoss = AccessTools.Field(typeof(HideoutAmbushMissionController), "_bossAgent");
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3) Log.Error("HideoutBossTalk." + where, e);
        }

        /// <summary>Kontroler kryjowki misji, jego stan (nazwa wartosci) i herszt; false - misja bez kontrolera albo pola gry nieznalezione.</summary>
        internal static bool Read(Mission m, out MissionLogic ctl, out bool ambush, out string state, out Agent boss)
        {
            ctl = null; ambush = false; state = null; boss = null;
            if (m == null) return false;
            Resolve();
            var h = m.GetMissionBehavior<HideoutMissionController>();
            if (h != null)
            {
                if (_hState == null || _hBoss == null) return false;
                ctl = h; state = _hState.GetValue(h)?.ToString(); boss = _hBoss.GetValue(h) as Agent;
                return true;
            }
            var a = m.GetMissionBehavior<HideoutAmbushMissionController>();
            if (a != null)
            {
                if (_aState == null || _aBoss == null) return false;
                ctl = a; ambush = true; state = _aState.GetValue(a)?.ToString(); boss = _aBoss.GetValue(a) as Agent;
                return true;
            }
            return false;
        }

        /// <summary>Ludzie gracza na nogach poza nim samym - ta sama regula co blokada odpowiedzi "I don't fight duels with brigands"
        /// (HideoutConversationsCampaignBehavior.bandit_hideout_continue_battle_on_clickable_condition).</summary>
        private static int PlayerMen(Mission m)
        {
            int n = 0;
            if (m == null || m.PlayerTeam == null) return 0;
            foreach (var a in m.PlayerTeam.ActiveAgents)
                if (a != null && !a.IsMount && (object)a.Character != CharacterObject.PlayerCharacter) n++;
            return n;
        }

        private static bool BossCond()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HideoutBossTalkFix) return false;
                var m = Mission.Current;
                if (!Read(m, out _, out _, out var state, out var boss) || state != TalkState || boss == null) return false;
                var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                if (cm == null || !ReferenceEquals(cm.OneToOneConversationAgent, boss)) return false;
                var logic = m.GetMissionBehavior<Logic>();
                if (logic != null) logic.NoteOurLine(boss);
                return true;
            }
            catch (Exception e) { Stumble("BossCond", e); return false; }
        }

        /// <summary>Warunek wiersza gry bandit_hideout_start_defender (bez sprawdzenia kontrolera - ten jest): frakcja spotkania bandycka
        /// i osada spotkania to kryjowka; zwraca tez id frakcji spotkania (Hideout.MapFaction = klan pierwszej bandy w osadzie).</summary>
        private static bool GameCond(out string faction)
        {
            faction = "-";
            var p = PlayerEncounter.EncounteredParty;
            if (p == null) { faction = "brak spotkania"; return false; }
            var f = p.MapFaction;
            faction = f != null ? f.StringId : "brak frakcji";
            return f != null && f.IsBanditFaction && p.Settlement != null && p.Settlement.IsHideout;
        }

        /// <summary>Warunek wiersza gry guard_start (GuardsCampaignBehavior.conversation_guard_start_on_condition): zajecie Soldier
        /// i spotkanie w osadzie (bez sprawdzenia eskorty - w kryjowce jej nie ma).</summary>
        private static bool GuardCond(CharacterObject c)
        {
            return c != null && c.Occupation == Occupation.Soldier && PlayerEncounter.Current != null && PlayerEncounter.InsideSettlement;
        }

        /// <summary>Zachowanie misji kryjowki (dopinane w SubModuleMain obok HideoutAlarm): zapas i linia misji.
        /// Flagi rozdzielone (recenzja 293): _fired = StartBossFight* wrocilo (zapas zadzialal), _tries = proby zapasu; potkniecia liczone NA MISJE -
        /// wyjatek w zwyklym ticku nie wylacza zapasu, wyjatek w probie zapasu = jeszcze jedna proba po SafetySec, po MaxTries koniec prob.</summary>
        internal sealed class Logic : MissionBehavior
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            // proby zapasu na misje: pierwsza + jedna powtorka (gdy StartBossFight* rzucil, a stan dalej ConversationBetweenLeaders)
            private const int MaxTries = 2;
            // potkniecia zwyklego ticku na misje, po ktorych logika tej misji staje (wyjatek co klatke - nie ma czego ratowac)
            private const int MaxMissionStumbles = 10;

            private bool _talkPhase, _talkSeen, _ourLine, _gameCond, _guardCond, _fired, _inTry, _fallbackDuel, _stopped, _lined;
            private int _ourLines, _menAtTalk = -1, _tries, _missionStumbles;
            private float _idle;
            private string _faction = "-", _lastState = "-", _bossName = "-", _bossId = "-", _bossOcc = "-", _bossHero = "-", _bossBandit = "-";
            private bool _ambush;

            internal void NoteOurLine(Agent boss)
            {
                _ourLine = true; _ourLines++;
                try
                {
                    _gameCond = GameCond(out _faction);
                    _guardCond = GuardCond(boss.Character as CharacterObject);
                    _menAtTalk = PlayerMen(Mission);
                }
                catch (Exception e) { _missionStumbles++; Stumble("NoteOurLine", e); }
            }

            private void NoteBoss(Agent boss)
            {
                if (boss == null || _bossId != "-") return;
                var c = boss.Character as CharacterObject;
                _bossName = boss.Name != null ? boss.Name.ToString() : "?";
                _bossId = c != null ? c.StringId : "?";
                _bossOcc = c != null ? c.Occupation.ToString() : "?";
                _bossHero = boss.IsHero ? "TAK" : "NIE";
                _bossBandit = c != null && c.Culture != null && c.Culture.IsBandit ? "TAK" : "NIE";
            }

            public override void OnMissionTick(float dt)
            {
                var s = Settings.Current;
                if (s == null || !s.HideoutBossTalkFix || _fired || _stopped) return;
                try
                {
                    var m = Mission;
                    if (!Read(m, out _, out _ambush, out var state, out var boss)) return;
                    if (state != null) _lastState = state;
                    if (state != TalkState) { _idle = 0f; return; }
                    _talkPhase = true;
                    NoteBoss(boss);
                    var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                    if (cm != null && cm.IsConversationInProgress) { _talkSeen = true; _idle = 0f; return; }
                    _idle += dt;
                    if (_idle < SafetySec) return;
                    // rozmowa przywodcow skonczona bez odpowiedzi (albo nie ruszyla): walka rusza tak, jak dalaby ja gra
                    _tries++;
                    int men = PlayerMen(m);
                    _fallbackDuel = men == 0 && boss != null && boss.IsActive();
                    _inTry = true;
                    if (_ambush)
                    {
                        if (_fallbackDuel) HideoutAmbushMissionController.StartBossFightDuelMode(); else HideoutAmbushMissionController.StartBossFightBattleMode();
                    }
                    else
                    {
                        if (_fallbackDuel) HideoutMissionController.StartBossFightDuelMode(); else HideoutMissionController.StartBossFightBattleMode();
                    }
                    _inTry = false;
                    _fired = true;
                    Read(m, out _, out _, out var after, out _);
                    if (after != null) _lastState = after;
                    Log.Info(Tag + ": ZAPAS - misja " + (_ambush ? "zasadzka" : "kryjowka") + " | rozmowa z hersztem " + _bossName + " [" + _bossId + "] "
                             + (_talkSeen ? "zamknieta bez odpowiedzi" : "nie ruszyla") + " (" + SafetySec.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s stanu " + TalkState
                             + " bez rozmowy) -> " + (_fallbackDuel ? "pojedynek (ludzi gracza 0)" : "walka wszystkich (ludzi gracza " + men + ")")
                             + " | stan po " + (after ?? "?") + " | proba " + _tries + ".");
                }
                catch (Exception e)
                {
                    _missionStumbles++;
                    Stumble("OnMissionTick", e);
                    if (_inTry)
                    {
                        // proba zapasu rzucila: stan czytany od nowa; gdy dalej rozmowa przywodcow - jeszcze jedna proba po SafetySec (do MaxTries)
                        _inTry = false;
                        _idle = 0f;
                        string after = null;
                        try { Read(Mission, out _, out _, out after, out _); } catch (Exception e2) { _missionStumbles++; Stumble("OnMissionTick.Read", e2); }
                        if (after != null) _lastState = after;
                        if (_tries >= MaxTries) _stopped = true;
                        Log.Info(Tag + ": ZAPAS BLAD - misja " + (_ambush ? "zasadzka" : "kryjowka") + " | herszt " + _bossName + " [" + _bossId + "] | proba " + _tries
                                 + " z " + MaxTries + " (" + (_fallbackDuel ? "pojedynek" : "walka wszystkich") + ") | " + e.GetType().Name + " | stan po " + (after ?? "?")
                                 + " | " + (_stopped ? "koniec prob" : "powtorka za " + SafetySec.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s") + ".");
                    }
                    else if (_missionStumbles >= MaxMissionStumbles) _stopped = true;
                }
            }

            protected override void OnEndMission()
            {
                try { Line(); } catch (Exception e) { _missionStumbles++; Stumble("OnEndMission", e); }
                base.OnEndMission();
            }

            private void Line()
            {
                if (_lined) return;
                _lined = true;
                var s = Settings.Current;
                // stan na koniec czytany jeszcze raz (stany gry tylko rosna) - po zatrzymaniu logiki tick go juz nie odswieza
                try { if (Read(Mission, out _, out _, out var st, out _) && st != null) _lastState = st; }
                catch (Exception e) { _missionStumbles++; Stumble("Line.Read", e); }
                string pick;
                if (!_talkPhase) pick = "bez fazy herszta";
                else if (_fired) pick = _fallbackDuel ? "zapas pojedynek" : "zapas wszyscy";
                else if (_tries > 0) pick = "zapas blad (prob " + _tries + ", stan " + _lastState + ")";
                else if (_lastState == "BossFightWithDuel") pick = "pojedynek";
                else if (_lastState == "BossFightWithAll") pick = "wszyscy";
                else pick = "brak (stan " + _lastState + ")";
                Log.Info(Tag + ": misja " + (_ambush ? "zasadzka" : "kryjowka") + " | klucz " + (s != null && s.HideoutBossTalkFix ? "TAK" : "NIE")
                         + " | faza herszta " + (_talkPhase ? "TAK" : "NIE") + ", stan na koniec " + _lastState
                         + " | herszt " + _bossName + " [" + _bossId + "] (zajecie " + _bossOcc + ", bohater " + _bossHero + ", kultura bandycka " + _bossBandit + ")"
                         + " | rozmowa " + (_talkSeen ? "TAK" : "NIE") + ", nasz wiersz " + (_ourLine ? "TAK" : "NIE") + " (razy " + _ourLines + ")"
                         + ", warunek wiersza gry " + (_ourLine ? (_gameCond ? "TAK" : "NIE") : "-") + " (frakcja spotkania " + _faction + ")"
                         + ", wiersz straznika gry pasowalby " + (_ourLine ? (_guardCond ? "TAK" : "NIE") : "-")
                         + " | ludzi gracza przy rozmowie " + (_menAtTalk < 0 ? "-" : _menAtTalk.ToString())
                         + " | wybor " + pick + " | potkniecia misji " + _missionStumbles + (_stopped ? " (logika zatrzymana)" : "") + ", sesja " + _stumbles + ".");
            }
        }
    }
}
