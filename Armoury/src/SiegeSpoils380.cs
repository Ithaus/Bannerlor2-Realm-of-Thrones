using System;
using System.Collections.Generic;
using System.Diagnostics;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace Armoury
{
    /// <summary>
    /// 380 (Jeff 09.11: "po bitwie powinno byc okno jencow i lupow"). OKNO JENCOW I LUPOW PO ODPARTYM SZTURMIE (obrona gracza, zalamanie 371, wrog odstapil).
    /// Dlaczego gra go nie pokazuje (cs_full.cs 1.4.8): odwrot z 371 idzie przez ContinueBattle -&gt; FinishBattleAndKeepSiegeEvent -&gt; _mapEvent = null -&gt;
    /// menu_siege_strategies (:98880) - do DoApplyMapEventResults (:99116) gra nie dochodzi, a bez zwyciezcy i tak szlaby do End, nie PlayerVictory; DoLootMembersAndPrisoners
    /// i DoLootInventory (:99267) nie biegna. Nasza dzialka lupu (BattlefieldLaw.ShareQueue) dotad wracala przez DropShare do zbrojowni wojska ("pole przegrane").
    /// Udawanie zwyciezcy (SetOverrideWinner) odpada - CaptureDefeatedPartyMembers wzialby w niewole wszystkich zdrowych atakujacych.
    /// Zasady:
    ///  - Arm (ArmouryBehavior.OnMapEventEnded, galaz "bez zwyciestwa"): bitwa gracza-obroncy, szturm, bez zwyciezcy, ksiega 373: zalamanie w ostatniej misji i odwrot
    ///    wroga (wpis Book jeszcze jest albo 373 zostawil NoteHeld) i klucz Siege Held Spoils Window -&gt; zamiast DropShare dzialka czeka na okno.
    ///  - jency: ludzie, ktorych 373 wzial do lochu osady (NotePrisoners). Zostaja w lochu (zapis gry w miedzyczasie niczego nie gubi); okno jencow jak ekran
    ///    gry po bitwie (Loot, "Aftermath"): lewa strona = kopia (z rannymi) tych, ktorzy wciaz siedza w lochu; przenosic mozna tylko jencow z lewej na prawa
    ///    (bez rozpuszczania wlasnych ludzi i wypuszczania wlasnych jencow). Done: wzieci zdjeci z lochu W SRODKU DoneLogic (nasz delegat Done - przed
    ///    PeopleBook.ScreenPost, ktory widzi loch osady jako lewa strone i przenosi udzialy FScreen), potem jak gra: TakePrisonerAction.ApplyByTakenFromPartyScreen.
    ///    Bohaterowie poza oknem (gra prowadzi ich osobna rozmowa; zostaja w lochu).
    ///  - lup: dzialka BattlefieldLaw.FlushShareToRoster (ta sama droga co sakwy: stan bojowy, wraki) na liste ekranu lupu gry (InventoryScreenHelper.OpenScreenAsLoot -
    ///    tryb Loot, musztra Drill bierze swoje z reszty jak po zwyklej bitwie). Reszta po zamknieciu: przy Spoils Remnants Home na polke najblizszej przyjaznej
    ///    warowni bez zaplaty (ta sama regula co SpoilsSeal.RemnantPostfix), inaczej przepada jak w grze.
    ///  - bramka (4 razy na sekunde, warunki stabilne 1 s): brak misji, rozmowy i bitwy gracza, na wierzchu mapa (MapScreen), czynny stan MapState, okno lupu 117 /
    ///    opowiesci zamkniete. Ekrany wchodza na stos nad menu oblezenia i po zamknieciu wracaja do niego same - zadnego SwitchToMenu / ExitToLast (CLAUDE.md).
    ///  - zapas: brak bramki przez godzine gry - jency zostaja w lochu, dzialka do sakw (FlushShareToBaggage), jak "ekran lupow nie przyszedl".
    /// Stan tylko w pamieci (wczytanie - Reset; dzialka i tak zyje tylko w pamieci). Potkniecia liczone, bez globalnego wylacznika.
    /// </summary>
    internal static class SiegeSpoils380
    {
        private const string Tag = "Okno jencow i lupu (380): ";
        private const double StableSec = 1.0, WaitHours = 1.0;
        private const int ErrMax = 6;
        private static readonly long CheckTicks = Stopwatch.Frequency / 4;

        private const int PNone = 0, PWaitParty = 1, PParty = 2, PWaitLoot = 3, PLoot = 4;
        private static int _phase;
        private static MapEvent _heldMe, _prisMe;
        private static PartyBase _captor;
        private static string _st = "-";
        private static readonly Dictionary<CharacterObject, int> _pris = new Dictionary<CharacterObject, int>();
        private static double _deadlineH;
        private static long _lastCheck, _okSince;
        private static int _missing, _stumbles, _errs, _rp0;
        private static ItemRoster _loot;
        private static int _lootIn;
        private static string _why = "";

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            Log.Error("SiegeSpoils380." + where, e);
        }

        /// <summary>Nowa kampania / wczytanie (SiegeWounded373Behavior.RegisterEvents).</summary>
        internal static void Reset()
        {
            _phase = PNone; _heldMe = null; _prisMe = null; _captor = null; _st = "-"; _pris.Clear(); _loot = null; _lootIn = 0;
            _okSince = 0; _missing = 0; _stumbles = 0; _errs = 0; _why = "";
        }

        /// <summary>373 (MapEventEnded): obrona odparta w tej bitwie (gdy 373 biegl przed ArmouryBehavior).</summary>
        internal static void NoteHeld(MapEvent me) { _heldMe = me; }

        /// <summary>373: n rannych typu troop wzietych do lochu osady captor w tej bitwie.</summary>
        internal static void NotePrisoners(MapEvent me, PartyBase captor, CharacterObject troop, int n)
        {
            try
            {
                if (me == null || captor == null || troop == null || n <= 0 || troop.IsHero) return;
                if (!ReferenceEquals(_prisMe, me)) { _prisMe = me; _pris.Clear(); }
                _captor = captor;
                int c; _pris.TryGetValue(troop, out c); _pris[troop] = c + n;
            }
            catch (Exception e) { Stumble("NotePrisoners", e); }
        }

        /// <summary>Czy bitwa gracza to odparty szturm na jego twierdze (wynik 371 + 373), bez zwyciezcy.</summary>
        private static bool HeldField(MapEvent me)
        {
            if (me == null || !me.IsSiegeAssault || me.WinningSide != BattleSideEnum.None || me.PlayerSide != BattleSideEnum.Defender) return false;
            if (ReferenceEquals(_heldMe, me)) return true;
            SiegeWounded373.Tally t;
            return SiegeWounded373.Book.TryGetValue(me, out t) && t != null && t.LastBroken && t.LastRetreated;
        }

        /// <summary>ArmouryBehavior.OnMapEventEnded (bitwa gracza bez zwyciestwa): true - dzialka zostaje w kolejce na okno 380 (bez DropShare).</summary>
        internal static bool Arm(MapEvent me)
        {
            var s = Settings.Current;
            if (s == null || !s.SiegeHeldSpoilsWindow || !HeldField(me)) return false;
            if (_phase != PNone) Finish("nowa obrona przed pokazaniem okien poprzedniej", me);
            var st = me.MapEventSettlement;
            if (_captor == null || !ReferenceEquals(_prisMe, me)) _captor = st != null ? st.Party : null;
            _st = st != null && st.Name != null ? st.Name.ToString() : "-";
            if (!ReferenceEquals(_prisMe, me)) { _pris.Clear(); _prisMe = me; }
            _phase = PWaitParty;
            _deadlineH = CampaignTime.Now.ToHours + WaitHours;
            _okSince = 0; _missing = 0; _loot = null; _lootIn = 0; _why = "";
            Log.Info(Tag + "obrona odparta (" + _st + ") - dzialka lupu w kolejce " + (BattlefieldLaw.SharePending() ? "TAK" : "pusta") + ", czeka na okno jencow i lupu (bez zwrotu do zbrojowni).");
            return true;
        }

        // ------------------------------------------------------------ takt (SubModuleMain.SpoilsTick - co klatke)

        internal static void AppTick()
        {
            if (_phase == PNone) return;
            long t = Stopwatch.GetTimestamp();
            if (t - _lastCheck < CheckTicks) return;
            _lastCheck = t;
            try { Step(t); }
            catch (Exception e) { Stumble("Step", e); }
        }

        private static void Step(long t)
        {
            var c = Campaign.Current;
            if (c == null || !c.GameStarted) return;
            if (_phase == PParty)
            {
                if (OnStack<PartyState>()) { _missing = 0; return; }
                if (++_missing < 2) return;
                _missing = 0;
                Note("okno jencow zamkniete bez naszego delegata");
                _phase = PWaitLoot; _okSince = 0;
                return;
            }
            if (_phase == PLoot)
            {
                if (OnStack<InventoryState>()) { _missing = 0; return; }
                if (++_missing < 2) return;
                _missing = 0;
                LootClosed();
                return;
            }
            // czekanie na bramke
            if (CampaignTime.Now.ToHours > _deadlineH)
            {
                int flushed = 0;
                if (BattlefieldLaw.SharePending()) flushed = BattlefieldLaw.FlushShareToBaggage("okno 380 nie przyszlo");
                Note("bramka nie przyszla w " + WaitHours.ToString("0") + " h gry (" + (_why.Length > 0 ? _why : "-") + ") - jency zostaja w lochu, dzialka do sakw " + flushed + " szt.");
                Finish("zapas");
                return;
            }
            string why = Gate(c);
            if (why != null) { _why = why; _okSince = 0; return; }
            if (_okSince == 0) { _okSince = t; return; }
            if ((t - _okSince) / (double)Stopwatch.Frequency < StableSec) return;
            _okSince = 0;
            if (_phase == PWaitParty) OpenParty();
            else if (_phase == PWaitLoot) OpenLoot();
        }

        private static string Gate(Campaign c)
        {
            if (TaleWorlds.MountAndBlade.Mission.Current != null) return "misja";
            var mp = MobileParty.MainParty;
            if (mp == null) return "brak druzyny";
            if (mp.MapEvent != null) return "bitwa gracza";
            if (Hero.MainHero == null || Hero.MainHero.IsPrisoner) return "gracz w niewoli";
            try { var cm = c.ConversationManager; if (cm != null && cm.IsConversationInProgress) return "rozmowa"; } catch { }
            try { if (SpoilsWindow.Busy) return "okno lupu 117"; } catch { }
            try { if (StoryWindow.IsOpen) return "okno opowiesci"; } catch { }
            var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
            if (gsm == null || !(gsm.ActiveState is MapState)) return "stan gry nie mapa (" + (gsm != null && gsm.ActiveState != null ? gsm.ActiveState.GetType().Name : "-") + ")";
            var top = ScreenManager.TopScreen;
            if (!(top is SandBox.View.Map.MapScreen)) return "na wierzchu nie mapa (" + (top != null ? top.GetType().Name : "-") + ")";
            return null;
        }

        private static bool OnStack<T>() where T : TaleWorlds.Core.GameState
        {
            try
            {
                var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                if (gsm == null) return false;
                foreach (var g in gsm.GameStates) if (g is T) return true;
            }
            catch { }
            return false;
        }

        // ------------------------------------------------------------ okno jencow

        private static void OpenParty()
        {
            var copy = TroopRoster.CreateDummyTroopRoster();
            int men = 0;
            try
            {
                var prison = _captor != null ? _captor.PrisonRoster : null;
                if (prison != null)
                    foreach (var kv in _pris)
                    {
                        int idx = prison.FindIndexOfTroop(kv.Key);
                        if (idx < 0) continue;
                        int have = prison.GetElementNumber(idx), wnd = prison.GetElementWoundedNumber(idx);
                        int n = Math.Min(kv.Value, have);
                        if (n <= 0) continue;
                        copy.AddToCounts(kv.Key, n, false, Math.Min(n, wnd));
                        men += n;
                    }
            }
            catch (Exception e) { Stumble("OpenParty.copy", e); men = 0; }
            if (men <= 0)
            {
                if (_pris.Count > 0) Note("okno jencow pominiete - nikogo z " + Sum() + " zapisanych nie ma juz w lochu");
                _phase = PWaitLoot;
                return;
            }
            try
            {
                _rp0 = PeopleBook.Men(MobileParty.MainParty.PrisonRoster);
                var ps = Game.Current.GameStateManager.CreateState<PartyState>();
                ps.IsDonating = false;
                ps.PartyScreenMode = PartyScreenHelper.PartyScreenMode.Loot;
                var logic = new PartyScreenLogic();
                var init = PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty(
                    TroopRoster.CreateDummyTroopRoster(), copy,
                    PartyScreenLogic.TransferState.NotTransferable, PartyScreenLogic.TransferState.Transferable, PartyScreenLogic.TransferState.NotTransferable,
                    Transferable, ps.PartyScreenMode, null,
                    leftPartyName: new TextObject("{=!}Wounded beneath the walls"), header: new TextObject("{=EOQcQa5l}Aftermath"),
                    leftPartyMembersSizeLimit: 0, partyPresentationDoneButtonDelegate: PartyDone, partyScreenClosedDelegate: PartyClosed);
                logic.Initialize(init);
                ps.PartyScreenLogic = logic;
                _phase = PParty; _missing = 0;
                Game.Current.GameStateManager.PushState(ps);
                Log.Info(Tag + _st + " | okno jencow: w lochu do wyboru " + men + " (zapisanych przez 373 " + Sum() + ").");
            }
            catch (Exception e)
            {
                Stumble("OpenParty", e);
                _phase = PWaitLoot;   // jency zostaja w lochu, dalej lup
            }
        }

        private static int Sum() { int n = 0; foreach (var kv in _pris) n += kv.Value; return n; }

        /// <summary>Tylko jency (bez bohaterow) i tylko z lewej strony na prawa - bez rozpuszczania i wypuszczania wlasnych.</summary>
        private static bool Transferable(CharacterObject character, PartyScreenLogic.TroopType type, PartyScreenLogic.PartyRosterSide side, PartyBase leftOwnerParty)
        {
            return character != null && !character.IsHero && type == PartyScreenLogic.TroopType.Prisoner && side == PartyScreenLogic.PartyRosterSide.Left;
        }

        /// <summary>Done ekranu (wewnatrz PartyScreenLogic.DoneLogic, przed PeopleBook.ScreenPost): wzieci zdjeci z lochu, potem jak gra (DefaultDoneHandler).</summary>
        private static bool PartyDone(TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster,
                                      FlattenedTroopRoster takenPrisonerRoster, FlattenedTroopRoster releasedPrisonerRoster, bool isForced, PartyBase leftParty, PartyBase rightParty)
        {
            int took = 0, miss = 0;
            try
            {
                var prison = _captor != null ? _captor.PrisonRoster : null;
                int p0 = prison != null ? PeopleBook.Men(prison) : 0;
                if (takenPrisonerRoster != null && prison != null)
                    foreach (var e in takenPrisonerRoster)
                    {
                        try
                        {
                            var tr = e.Troop;
                            if (tr == null || tr.IsHero) continue;
                            int idx = prison.FindIndexOfTroop(tr);
                            if (idx < 0 || prison.GetElementNumber(idx) <= 0) { miss++; continue; }
                            int w = e.IsWounded && prison.GetElementWoundedNumber(idx) > 0 ? 1 : 0;
                            prison.AddToCounts(tr, -1, false, -w);
                            took++;
                        }
                        catch (Exception ex) { miss++; Stumble("PartyDone.troop", ex); }
                    }
                // ksiega ludzi: ScreenPost bierze loch Settlement.CurrentSettlement jako lewa strone; gdy to nie nasz loch - przenosiny tutaj
                bool bookHere = false;
                try
                {
                    var cur = Settlement.CurrentSettlement;
                    if (took > 0 && prison != null && (cur == null || cur.Party != _captor))
                    {
                        bookHere = true;
                        PeopleBook.Transfer(prison, p0, MobileParty.MainParty.PrisonRoster, _rp0, PeopleBook.FScreen);
                    }
                }
                catch (Exception ex) { Stumble("PartyDone.book", ex); }
                Log.Info(Tag + _st + " | okno jencow Done: wzieci do druzyny " + took + (miss > 0 ? ", bez pokrycia w lochu " + miss : "") + ", zostaje w lochu "
                         + (prison != null ? prison.TotalManCount.ToString() : "-") + " (ksiega ludzi " + (bookHere ? "tutaj" : "ekran druzyny") + ").");
            }
            catch (Exception e) { Stumble("PartyDone", e); }
            // jak PartyScreenHelper.DefaultDoneHandler
            try
            {
                if (releasedPrisonerRoster != null && !releasedPrisonerRoster.IsEmpty()) EndCaptivityAction.ApplyByReleasedByChoice(releasedPrisonerRoster);
                if (takenPrisonerRoster != null && !takenPrisonerRoster.IsEmpty())
                {
                    // jency z walki: ich sprzet DTE zebral z pola (nieprzytomni jak polegli) - bez obszukania w ArmouryBehavior.OnPrisonerTaken (W3)
                    ArmouryBehavior.PrisonersFromField++;
                    try { TakePrisonerAction.ApplyByTakenFromPartyScreen(takenPrisonerRoster); }
                    finally { ArmouryBehavior.PrisonersFromField--; }
                }
            }
            catch (Exception e) { Stumble("PartyDone.game", e); }
            return true;
        }

        private static void PartyClosed(PartyBase leftOwnerParty, TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, PartyBase rightOwnerParty,
                                        TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, bool fromCancel)
        {
            if (fromCancel) Log.Info(Tag + _st + " | okno jencow zamkniete bez zmian (Cancel) - jency zostaja w lochu.");
            _phase = PWaitLoot; _okSince = 0; _missing = 0;   // okno lupu w nastepnych taktach, po zdjeciu ekranu ze stosu
        }

        // ------------------------------------------------------------ okno lupu

        private static void OpenLoot()
        {
            if (!BattlefieldLaw.SharePending()) { Finish("bez lupu (kolejka pusta)"); return; }
            var r = new ItemRoster();
            int n = 0;
            try { n = BattlefieldLaw.FlushShareToRoster(r, "obrona odparta " + _st); }
            catch (Exception e) { Stumble("OpenLoot.flush", e); }
            int pcs = Count(r);
            if (pcs <= 0) { Finish("bez lupu (" + n + " szt. z kolejki, lista pusta)"); return; }
            try
            {
                _loot = r; _lootIn = pcs;
                _phase = PLoot; _missing = 0;
                InventoryScreenHelper.OpenScreenAsLoot(new Dictionary<PartyBase, ItemRoster> { { PartyBase.MainParty, r } });
                Log.Info(Tag + _st + " | okno lupu: " + pcs + " szt. na liscie.");
            }
            catch (Exception e)
            {
                Stumble("OpenLoot", e);
                // ekran sie nie otworzyl - lista do sakw (nic nie przepada)
                try
                {
                    var bag = MobileParty.MainParty.ItemRoster;
                    for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.Amount > 0) bag.AddToCounts(el.EquipmentElement, el.Amount); }
                    r.Clear();
                    Note("okno lupu sie nie otworzylo - " + pcs + " szt. do sakw");
                }
                catch (Exception e2) { Stumble("OpenLoot.bag", e2); }
                Finish("blad otwarcia okna lupu");
            }
        }

        private static int Count(ItemRoster r)
        {
            int n = 0;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++) { int a = r.GetElementCopyAtIndex(i).Amount; if (a > 0) n += a; }
            return n;
        }

        /// <summary>Ekran lupu zdjety ze stosu: InventoryLogic zmienia liste w miejscu (Done; Cancel = Reset do stanu z otwarcia), musztra Drill wziela swoje
        /// w OnItemsDiscardedByPlayer - reszta na polke najblizszej przyjaznej warowni (Spoils Remnants Home) albo przepada jak w grze.</summary>
        private static void LootClosed()
        {
            var r = _loot;
            _loot = null;
            int left = Count(r), moved = 0, kept = 0;
            string where = "-";
            try
            {
                var s = Settings.Current;
                if (left > 0 && s != null && s.SpoilsRemnantsHome)
                {
                    var st = HorseFate.AbandonedShelf(MobileParty.MainParty);
                    if (st != null && st.ItemRoster != null)
                    {
                        where = st.Name != null ? st.Name.ToString() : st.StringId;
                        for (int i = r.Count - 1; i >= 0; i--)
                        {
                            var el = r.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (el.Amount <= 0 || it == null) continue;
                            if (it.NotMerchandise || it.IsBannerItem || el.EquipmentElement.IsQuestItem || UniqueSpoils.Is(it)) { kept += el.Amount; continue; }
                            st.ItemRoster.AddToCounts(el.EquipmentElement, el.Amount);
                            r.AddToCounts(el.EquipmentElement, -el.Amount);
                            moved += el.Amount;
                        }
                        if (moved > 0) Log.Player("What you left of the spoils - " + moved + " pieces - is carted off to the market of " + where + ".");
                    }
                }
                if (r != null) r.Clear();
            }
            catch (Exception e) { Stumble("LootClosed", e); }
            Log.Info(Tag + _st + " | okno lupu zamkniete: na liscie " + _lootIn + " szt., zostawione " + left + (moved > 0 ? " -> na polke " + where + " " + moved : "")
                     + (left - moved > 0 ? ", przepadlo (jak w grze) " + (left - moved) + (kept > 0 ? " (w tym unikaty / nie-towar " + kept + ")" : "") : "") + ".");
            Finish("okna pokazane");
        }

        private static void Note(string what) { Log.Info(Tag + _st + " | " + what + "."); }

        private static void Finish(string how) { Finish(how, null); }

        /// <summary>keep - bitwa, ktorej notatki 373 (NoteHeld / NotePrisoners) zostaja (Arm nowej obrony po notatkach 373 tej samej bitwy).</summary>
        private static void Finish(string how, MapEvent keep)
        {
            if (_phase != PNone)
                Log.Info(Tag + _st + " | koniec (" + how + ") | potkniecia " + _stumbles + ".");
            _phase = PNone; _okSince = 0; _missing = 0; _why = "";
            if (keep == null || !ReferenceEquals(_heldMe, keep)) _heldMe = null;
            if (keep == null || !ReferenceEquals(_prisMe, keep)) { _prisMe = null; _captor = null; _pris.Clear(); }
            if (_loot != null) { try { _loot.Clear(); } catch { } _loot = null; }
        }
    }
}
