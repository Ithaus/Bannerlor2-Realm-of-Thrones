using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 387f (Jeff 11.11 "tak, rob wszystko 2-4" - punkt 2: "zalamanie szturmu armii AI, gdy jestes w srodku bez dowodzenia"). Gracz walczy w szturmie,
    /// ktorym dowodzi wodz AI (gracz w jego armii albo dolaczyl do oblezenia). Dotad SiegeBreak371 wylaczal sie w takiej misji ("gracz atakuje, ale nie
    /// dowodzi"), a po misji bez zwyciezcy gra zostawiala gracza w menu "encounter" z "Attack!" / "Send troops" / "Abandon army" (cs_full 1.4.8:
    /// PlayerEncounter.ContinueBattle 98852 "Battle continues"; "Leave..." i "Continue siege preparations." tylko dla wodza strony) - szturm w kolko
    /// albo porzucenie armii.
    /// Teraz: w misji lamie sie szturm wodza AI (SiegeBreak371 tryb _al, rekord SiegeWalls.MissionAssault - ten sam co w obronie), a po misji:
    ///  - SiegeBreak371.OnEndMission -> NoteArmyBreak: bitwa czeka na zakonczenie (_alPend, tylko pamiec);
    ///  - postfiks PlayerEncounter.ContinueBattle (prywatna, zadeklarowana w PlayerEncounter; wolana z DoWait po misji bez rozstrzygniecia): po
    ///    straznicach me.FinishBattleAndKeepSiegeEvent() - ta sama metoda, ktora gra wola w tej funkcji dla gracza-obroncy i my w OB2 (bitwa bez
    ///    zwyciezcy, oboz zostaje) - a potem PlayerEncounter.Finish(false). Precedens w grze: DoEnd -> Finish z tego samego Update (99339), on_init menu
    ///    "encounter" sprawdza potem PlayerEncounter.Current == null (184720). Menu zamyka Finish (ExitToLast), Campaign.Tick sam otwiera
    ///    menu_siege_strategies (GetGenericStateMenu 59313: MapEvent null, BesiegerCamp != null). Sami zadnego menu nie przelaczamy.
    ///  - przerwa: istniejaca droga SiegeWalls.OnMapEventEnded -> RegisterMissionBreak -> Repel(finish: false) (a.MissionBreak, WinningSide None) -
    ///    zapis arm_siege_walls, Veto trzyma wodza AI; ranni spod murow: istniejace SiegeWounded373Behavior.Player387c (ten sam MapEventEnded).
    /// Kazda straznica niespelniona albo wyjatek = licznik "pominiete" i gracz zostaje w menu "encounter" jak dotad (zachowanie awaryjne = zachowanie gry).
    /// Wyjatek = potkniecie (Stumble), bez globalnego wylacznika. Stan tylko w pamieci (wczytanie miedzy misja a menu = jak w grze).
    /// </summary>
    internal static partial class SiegeWalls
    {
        /// <summary>387f: szturm armii AI z graczem w szeregu lamie sie jak szturm AI (wymaga 371 w misji).</summary>
        internal static bool ArmyBreakOn { get { var s = Settings.Current; return s != null && s.SiegeAssaultBreak && s.SiegeAssaultBreakMission && s.SiegeArmyAssaultBreaks; } }

        /// <summary>390 (Jeff 11.11, decyzja 1): to samo, gdy gracz dolaczyl do oblezenia wodza AI BEZ ARMII i gra zrobila go generalem misji
        /// (wodzem bitwy i oblezenia zostaje AI). NIE = stan sprzed 390 (taka misja poza regula).</summary>
        internal static bool JoinedBreakOn { get { var s = Settings.Current; return ArmyBreakOn && s != null && s.SiegeJoinedAssaultBreaks; } }

        private static int _dJoSeen, _dJoNone;   // 390: misje gracza-generala w oblezeniu wodza AI (w regule / poza regula)
        internal static void NoteJoinedSeen() { _dJoSeen++; }
        internal static void NoteJoinedNone() { _dJoNone++; }

        private static MapEvent _alPend;
        private static Assault _alPendA;
        private static bool _alPendFuse;
        private static int _dAlSeen, _dAlNone, _dAlWarn, _dAlBreak, _dAlFuse, _dAlLate, _dAlFinished, _dAlSkipped;
        private static int _dAlGen, _dAlReg, _dAlWait, _dAlMend;   // 387f-p: general bez bycia wodzem bitwy (poza regula), przerwy zapisane po 387f, proby poza mapa, naprawy polstanu
        private static string _hookAl = "nie zakladana";

        internal static void NoteArmySeen() { _dAlSeen++; }
        internal static void NoteArmyNone() { _dAlNone++; }
        internal static void NoteArmyWarn() { _dAlWarn++; }
        internal static void NoteArmyGeneral() { _dAlGen++; }   // 387f-p: gracz generalem misji bez bycia wodzem bitwy (dolaczyl do oblezenia bez armii) - bez zalamania
        internal static void NoteArmyRegistered() { _dAlReg++; }

        /// <summary>387f (SiegeBreak371.OnEndMission, tryb _al): szturm armii AI zalamal sie w misji z graczem w szeregu i misja nie skonczyla sie
        /// zwyciestwem atakujacych - bitwe zakonczy postfiks PlayerEncounter.ContinueBattle. Tylko pamiec. Nie rzuca.</summary>
        internal static void NoteArmyBreak(MapEvent me, Assault a, bool fuse, int late)
        {
            _dAlBreak++; if (fuse) _dAlFuse++; _dAlLate += late;
            if (me == null || a == null) return;
            _alPend = me; _alPendA = a; _alPendFuse = fuse;
        }

        /// <summary>Z OnMapEventEnded: bitwa skonczona (takze nasza droga) - oczekujace zakonczenie do kosza.</summary>
        private static void ArmyEventEnded(MapEvent me)
        {
            if (_alPend != null && (ReferenceEquals(_alPend, me) || _alPend.IsFinalized)) { _alPend = null; _alPendA = null; }
        }

        private static void Reset387f() { _alPend = null; _alPendA = null; _alPendFuse = false; ZeroDay387f(); }

        private static void ZeroDay387f() { _dAlSeen = _dAlNone = _dAlWarn = _dAlBreak = _dAlFuse = _dAlLate = _dAlFinished = _dAlSkipped = _dAlGen = _dAlReg = _dAlWait = _dAlMend = _dJoSeen = _dJoNone = 0; }

        private static string Day387fText()
        {
            return "; 387f szturm armii AI z graczem " + (ArmyBreakOn ? "tak" : "nie") + ": misji z graczem w szeregu " + _dAlSeen + ", poza regula " + _dAlNone + ", ostrzezen 75% " + _dAlWarn
                 + ", zalaman " + _dAlBreak + " (bezpiecznik " + _dAlFuse + ", wystawionych po zalamaniu " + _dAlLate + "), bitew zakonczonych " + _dAlFinished + ", pominietych " + _dAlSkipped
                 + ", przerw zapisanych " + _dAlReg + ", gracz generalem bez bycia wodzem bitwy (bez zalamania) " + _dAlGen
                 + "; 390 gracz-general w oblezeniu wodza AI " + (JoinedBreakOn ? "tak" : "nie") + ": misji " + _dJoSeen + ", poza regula " + _dJoNone + " (zalamania, bitwy zakonczone i przerwy - w licznikach 387f)"
                 + (_dAlWait > 0 ? ", prob poza mapa " + _dAlWait : "") + (_dAlMend > 0 ? ", naprawionych polstanow " + _dAlMend : "") + " (latka " + _hookAl + ")";
        }

        private static string Start387fText()
        {
            return " | 387f szturm armii AI z graczem w szeregu: klucz " + (ArmyBreakOn ? "tak" : "nie") + ", latka PlayerEncounter.ContinueBattle " + _hookAl
                 + " | 390 takze gracz-general bez armii w oblezeniu wodza AI: " + (JoinedBreakOn ? "tak" : "nie");
        }

        /// <summary>Powod, dla ktorego bitwy NIE konczymy (null = konczymy). Rzuca - lapie wolajacy.</summary>
        private static string ArmyEndBlock(MapEvent me, Assault a)
        {
            if (!ArmyBreakOn) return "klucz wylaczony";
            if (!Live) return "bez kampanii";
            if (a == null || !a.MissionBreak) return "brak znacznika zalamania";
            if (a.Repelled) return "przerwa tej bitwy juz zapisana";
            if (!ReferenceEquals(MapEvent.PlayerMapEvent, me)) return "to juz nie bitwa gracza";
            if (me.IsFinalized) return "bitwa juz zakonczona";
            if (me.HasWinner) return "bitwa ma zwyciezce (" + me.WinningSide + ")";
            if (me.DiplomaticallyFinished) return "bitwa zakonczona pokojem";
            if (!me.IsSiegeAssault || me.IsNavalMapEvent) return "nie szturm murow";
            if (me.PlayerSide != BattleSideEnum.Attacker) return "gracz nie po stronie atakujacych";
            var lpb = me.AttackerSide != null ? me.AttackerSide.LeaderParty : null;
            if (lpb == null || !lpb.IsMobile || lpb.MobileParty == null) return "brak wodza strony";
            if (lpb == PartyBase.MainParty) return "wodzem bitwy jest gracz (droga 387)";
            var st = me.MapEventSettlement;
            if (st == null || !st.IsFortification || st.SiegeEvent == null) return "brak oblezenia";
            if (st.CurrentSiegeState != Settlement.SiegeState.OnTheWalls) return "mury wziete (sala lorda)";
            if (PlayerEncounter.Current == null) return "brak spotkania gracza";
            if (PlayerEncounter.CurrentBattleSimulation != null) return "trwa symulacja bitwy";
            var hero = Hero.MainHero;
            if (hero == null || hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null) return "gracz w niewoli";
            if (hero.DeathMark != KillCharacterAction.KillCharacterActionDetail.None) return "gracz ginie";
            return null;
        }

        /// <summary>Postfiks PlayerEncounter.ContinueBattle. Zwykle: jedno porownanie (nic nie czeka). Gdy czeka zalamany szturm armii AI z graczem w
        /// szeregu: bitwa konczona bez zwyciezcy (oblezenie trwa) i spotkanie gracza zamykane. Nie rzuca.</summary>
        public static void ContinueBattlePost()
        {
            var me = _alPend;
            if (me == null) return;
            var a = _alPendA; bool fuse = _alPendFuse;
            // 387f-p: Finish -> GameMenu.ExitToLast zaklada mape. Poza MapState oczekiwania NIE zuzywamy - zostaje do nastepnego ContinueBattle
            // (kasuje je i tak ArmyEventEnded / Reset). Mission.Current celowo nie sprawdzane: on_init menu "encounter" biegnie z MapState.OnActivate.
            try
            {
                var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                if (gsm == null || !(gsm.ActiveState is TaleWorlds.CampaignSystem.GameState.MapState))
                {
                    if (_dAlWait++ == 0) Log.Info("Szturm (387f): ContinueBattle poza mapa (" + (gsm != null && gsm.ActiveState != null ? gsm.ActiveState.GetType().Name : "brak stanu") + ") - zakonczenie bitwy czeka na nastepne wywolanie.");
                    return;
                }
            }
            catch (Exception e) { Stumble("ContinueBattlePost.State", e); return; }
            _alPend = null; _alPendA = null;                                     // jedna proba na zalamanie - nieudana = menu "encounter" jak w grze
            string no;
            try { no = ArmyEndBlock(me, a); }
            catch (Exception e) { Stumble("ContinueBattlePost.Guard", e); no = "potkniecie przy sprawdzaniu"; }
            if (no != null) { ArmySkip(me, a, no); return; }
            var st = me.MapEventSettlement;
            var ev = st.SiegeEvent;
            string stId = st.StringId, stName = Nm(st);
            // FinishBattleAndKeepSiegeEvent ustawia _keepSiegeEvent PRZED RecalculateShortTermBehavior wodza (jak w Repel): najpierw ta sama metoda u nas -
            // wyjatek = bitwy nie ruszamy
            try { me.AttackerSide.LeaderParty.MobileParty.RecalculateShortTermBehavior(); }
            catch (Exception e) { Stumble("ContinueBattlePost.Recalculate", e); ArmySkip(me, a, "potkniecie RecalculateShortTermBehavior wodza"); return; }
            try { me.FinishBattleAndKeepSiegeEvent(); }                         // -> MapEventEnded: Player387c (ranni), SiegeWalls.OnMapEventEnded -> RegisterMissionBreak (przerwa)
            catch (Exception e)
            {
                Stumble("ContinueBattlePost.FinishBattle", e);
                bool fin = false; try { fin = me.IsFinalized; } catch { }
                if (!fin) { ArmySkip(me, a, "potkniecie FinishBattleAndKeepSiegeEvent (bitwa nie zakonczona)"); return; }
            }
            // jak gra po FinishBattleAndKeepSiegeEvent w tej samej funkcji (98888-98889): spotkanie nie trzyma juz zakonczonej bitwy
            try
            {
                var cur = PlayerEncounter.Current;
                var f = cur != null ? AccessTools.Field(typeof(PlayerEncounter), "_mapEvent") : null;
                if (f != null && ReferenceEquals(f.GetValue(cur), me)) f.SetValue(cur, null);
            }
            catch (Exception e) { Stumble("ContinueBattlePost.MapEventField", e); }
            bool closed = false;
            try
            {
                if (PlayerEncounter.Current != null) PlayerEncounter.Finish(false);   // zamyka menu "encounter" (ExitToLast); menu oblezenia otwiera sama gra
                closed = PlayerEncounter.Current == null;
            }
            catch (Exception e)
            {
                Stumble("ContinueBattlePost.Finish", e);
                // 387f-p: spotkanie nie zamkniete - gra domknie je sama przy nastepnym Update (PlayerEncounter.UpdateInternal: _leaveEncounter -> Finish)
                try { if (PlayerEncounter.Current != null) PlayerEncounter.LeaveEncounter = true; } catch (Exception e2) { Stumble("ContinueBattlePost.Leave", e2); }
            }
            // 387f-p: polstan po wyjatku cudzego nasluchu MapEventEnded w srodku FinalizeEventAux (bitwa WaitingRemoval, strony nie posprzatane) -
            // druzyna gracza nie moze wskazywac zakonczonej bitwy (GetGenericStateMenu dalby "encounter" bez spotkania)
            try
            {
                var mpb = PartyBase.MainParty;
                if (mpb != null && mpb.MapEventSide != null && ReferenceEquals(mpb.MapEvent, me) && me.IsFinalized)
                {
                    mpb.MapEventSide = null;
                    _dAlMend++;
                    Log.Info("Szturm (387f): polstan po zakonczeniu bitwy - druzyna gracza zdjeta ze strony zakonczonej bitwy (wyjatek cudzego nasluchu MapEventEnded? patrz bledy wyzej).");
                }
            }
            catch (Exception e) { Stumble("ContinueBattlePost.Mend", e); }
            _dAlFinished++;
            double left = 0.0;
            try { left = st.SiegeEvent != null && ReferenceEquals(st.SiegeEvent, ev) ? RepelPauseLeftDays(ev) : 0.0; } catch (Exception e) { Stumble("ContinueBattlePost.Pause", e); }
            try
            {
                InformationManager.DisplayMessage(new InformationMessage("The assault on " + stName + " has failed. The army returns to its siege lines.", Color.FromUint(0xFFE07040u)));
            }
            catch { }
            try
            {
                var main = MobileParty.MainParty;
                Log.Info("Szturm (387f): bitwa zakonczona bez zwyciezcy, oblezenie " + (st.SiegeEvent != null ? "trwa" : "NIE trwa (zwiniete)") + " | osada " + stId + " (" + stName + "), wodz " + (a != null ? a.Leader : "-")
                         + " | przerwa " + (left > 0.0 ? F(left, "0.00") + " dob, do dnia " + F(Now + left, "0.00") : "brak (linia 'Szturm (371): przerwa po zalamaniu w misji' powinna byc wyzej)")
                         + " | bezpiecznik misji " + (fuse ? "tak" : "nie") + " | spotkanie gracza " + (closed ? "zamkniete" : "NIE zamkniete")
                         + " | gracz: " + (main != null && main.Army != null ? "w armii" : "bez armii") + ", oboz oblezniczy " + (main != null && main.BesiegerCamp != null ? "tak (gra otworzy menu oblezenia)" : "nie")
                         + ", bitwa na mapie " + (main != null && main.MapEvent != null ? "JEST (blad?)" : "brak") + ".");
            }
            catch (Exception e) { Stumble("ContinueBattlePost.Log", e); }
        }

        private static void ArmySkip(MapEvent me, Assault a, string why)
        {
            _dAlSkipped++;
            string stId = "-";
            try { var st = me != null ? me.MapEventSettlement : null; if (st != null) stId = st.StringId; } catch { }
            Log.Info("Szturm (387f): NIE zakonczono - " + why + " | osada " + stId + ", wodz " + (a != null ? a.Leader : "-") + " | gracz zostaje w menu bitwy jak w grze ('Attack!' / 'Abandon army'; bez armii: 'Leave...').");
        }

        /// <summary>Z SiegeWalls.EnsureHooks (kampania, raz na proces). Cel prywatny, zadeklarowany w PlayerEncounter - DeclaredMethod.</summary>
        private static void EnsureHooks387f()
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(PlayerEncounter), "ContinueBattle", Type.EmptyTypes);
                if (m == null) { _hookAl = "BRAK (PlayerEncounter.ContinueBattle)"; return; }
                Seal31.Patch("387f: koniec bitwy po zalamanym szturmie armii AI z graczem (PlayerEncounter.ContinueBattle)", m, typeof(SiegeWalls), null, nameof(ContinueBattlePost), null,
                             Priority.Normal, Priority.Last);
                _hookAl = Wired387(m, nameof(ContinueBattlePost)) ? "wpieta" : "BRAK (latka nie weszla - linia 'Szczelnosc (164a)')";
            }
            catch (Exception e) { _hookAl = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooks387f", e); }
        }
    }
}
