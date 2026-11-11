using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 373 (Jeff 09.11: "a co z rannymi ktorzy leza pod murami ... jesli ranni leza blisko murow lub w srodku zamku a obrona sie zalamala to sa schwytani").
    /// RANNI ATAKUJACY POD MURAMI PO ZALAMANIU SZTURMU (371) - do lochu obroncow. Tylko misja szturmu, gracz broni.
    /// Dowod (dekompilacja 1.4.8, scratchpad/decout/cs_full.cs): nieprzytomny atakujacy jest od razu rannym w MemberRoster swojej partii
    /// (BattleAgentLogic.OnAgentRemoved -> MapEventSide.OnTroopWounded -> MapEventParty.OnTroopWounded -> WoundTroop); wynik misji z odwrotem wroga
    /// -> ContinueBattle -> FinishBattleAndKeepSiegeEvent = bitwa bez zwyciezcy, FinalizeEventAux bez CaptureDefeatedPartyMembers - dzis ranni wracaja z partia.
    /// Zasady:
    ///  - w misji (OnAgentRemoved, Unconscious, czlowiek, strona Attacker): "w srodku" = GetCurrentNavigationFaceId() % 10 == 1 (jak Formation.CountUnitsOnNavMeshIDMod10(1)
    ///    w TeamAISiegeComponent.IsFormationInsideCastle); "pod murami" = 2D &lt;= SiegeWoundedWallDistance od bramy / odcinka muru / drabiny / celu taranu i wiezy
    ///    (punkty zebrane raz, przy pierwszym rannym); ksiega [MapEvent][partia][typ] przez wszystkie misje tej bitwy ("Battle continues" - ranni dalej leza pod murami).
    ///  - przenosiny w MapEventEnded (388b: nasz nasluch biegnie PRZED SiegeWalls.OnMapEventEnded - MbEvent wola nasluchy od ostatnio zarejestrowanego; przerwa 371 liczy sie wiec ze stanu PO przenosinach - stan zastany, nie zmieniany), tylko gdy: szturm zalamal sie (371
    ///    NoteBreak), ostatnia misja skonczyla sie odwrotem wroga, bitwa bez zwyciezcy (WinningSide None) i IsSiegeAssault. Kazdy inny wynik (zamek padl, obronca
    ///    wygral bez zalamania) - wpis tylko usuniety (gra rozlicza jak dotad).
    ///  - zolnierze: n = min(zapisani, ranni typu w partii, ktora jest wciaz po stronie atakujacej) -&gt; MemberRoster -n (rannych) -&gt; loch osady +n (jako ranni, jak
    ///    CaptureDefeatedPartyMembers gry); ksiega ludzi PeopleBook.Transfer(FCaptured). Kazdy ranny ma jedno miejsce. Typ, ktorego gra nie bierze w niewole
    ///    (BattleRewardModel.CanTroopBeTakenPrisoner), zostaje.
    ///  - bohaterowie: zywy, ranny, nie jeniec, bez znaku smierci, CanBecomePrisoner, Occupation != Special, w partii po stronie atakujacej -&gt; TakePrisonerAction.Apply(osada).
    ///    373b (crash 09.11 19:43): WODZ PARTII (i wodz armii) zostaje w swojej partii jak w grze - TakePrisonerAction robi tylko RemovePartyLeader, armii nie
    ///    rusza; partia bez wodza zostawala w armii i Army.HourlyTick -> ThinkAboutCohesionBoost -> CalculatePartyInfluenceCost rzucal NRE (armyLeaderParty.LeaderHero).
    ///    Gra nigdy nie zostawia partii bez wodza w armii (KillCharacterAction: najpierw DisbandArmyAction / Army = null, potem nastepca albo StartDisband).
    ///  - 373c (Jeff 11.11): zolnierze maja sufit jak 388 - wolne miejsce lochu (PartyBase.PrisonerSizeLimit, liczone PO bohaterach) i nie wiecej niz obroncow
    ///    na nogach (DefenderSide.TroopCount), ta sama skala dla wszystkich partii; nadwyzka zostaje ranna w swojej partii. Bohaterowie BEZ limitu, brani przed
    ///    zolnierzami. Klucz Siege Wounded Dungeon Limit (NIE = bez sufitu, jak przed 373c).
    ///  - zdarzenia OnPrisonerTaken (rozbieranie jencow 3792) NIE odpalamy - jak w grze przy zwyklym pojmaniu zolnierzy w bitwie (to robi tylko ekran / loot).
    /// Wyjatek per partia / typ / bohater = potkniecie, bez globalnego wylacznika. Ksiega tylko w pamieci (wczytanie w trakcie bitwy - gubi wpis, log przy czyszczeniu).
    /// 387c (Jeff 11.11 "tak, rob wszystko 2-4" - "twoi ranni spod murow do lochu obroncow"): ta sama ksiega misji, gdy GRACZ ATAKUJE (Tally.Pl). Szturm gracza
    /// odparty (zalamanie 371/387, odwrot reczny, gracz pokonany) konczy sie "Battle continues" (PlayerEncounter.ContinueBattle), a potem "Continue siege
    /// preparations." / "Leave..." -> PlayerEncounter.Finish -> LeaveBattle -> MapEvent.FinalizeEvent bez zwyciezcy (cs_full 99560-99575, 112804-112826) -
    /// MapEventEnded pada, gdy MainParty jest jeszcze po stronie atakujacej. Rozliczenie: SiegeWounded373Behavior.Player387c (osobna galaz, galaz obroncy nietknieta).
    /// Sam gracz nigdy; bohaterowie rodu / towarzysze tylko z kluczem Siege Wounded Player Companions (domyslnie NIE); okien 380 nie ma.
    /// </summary>
    internal sealed class SiegeWounded373 : MissionLogic
    {
        private const int ErrMax = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Stan jednej bitwy szturmu (wszystkie misje).</summary>
        internal sealed class Tally
        {
            public readonly Dictionary<PartyBase, Dictionary<CharacterObject, int>> Troops = new Dictionary<PartyBase, Dictionary<CharacterObject, int>>();
            public readonly HashSet<Hero> Heroes = new HashSet<Hero>();
            public bool Broken, LastBroken, LastRetreated;   // Broken - zalamanie 371 w biezacej misji; LastBroken / LastRetreated - wynik ostatniej misji (OnEndMission)
            public int Missions, Inside, Near, Mid, Far, NoPos;
            public string St = "-";
            // 387c: gracz po stronie atakujacych. LastHeld - ostatnia misja bez wziecia murow; LastLeft - gracz zszedl z pola (odwrot reczny / pokonany / bez wyniku)
            public bool Pl, LastHeld, LastLeft;
            public int PlNear, AllyNear;      // zapisani zolnierze (w srodku + pod murami): partia gracza / partie sojusznikow
            public string LastWhy = "-";
        }

        /// <summary>387c: ranni strony gracza-atakujacego tez zostaja pod murami (wymaga klucza nadrzednego 373).</summary>
        internal static bool PlayerSideOn { get { var s = Settings.Current; return s != null && s.SiegeWoundedTakenPrisoner && s.SiegeWoundedPlayerSide; } }

        internal static readonly Dictionary<MapEvent, Tally> Book = new Dictionary<MapEvent, Tally>();

        /// <summary>Z SiegeBreak371.Break: szturm tej bitwy zalamal sie w misji.</summary>
        internal static void NoteBreak(MapEvent me)
        {
            try
            {
                if (me == null) return;
                Tally t;
                if (!Book.TryGetValue(me, out t)) { t = new Tally(); Book[me] = t; }
                t.Broken = true;
            }
            catch { }
        }

        private bool _known, _active;
        private bool _pl, _retreat;   // 387c: gracz atakuje; gracz zszedl z pola (Mission.RetreatMission)
        private MapEvent _me;
        private Tally _t;
        private readonly List<Vec2> _pts = new List<Vec2>();
        private bool _ptsDone;
        private int _inside, _near, _mid, _far, _nopos, _stumbles, _errs;
        private readonly List<string> _heroes = new List<string>();

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private static float Dist { get { var s = Settings.Current; return s != null ? Math.Max(0f, s.SiegeWoundedWallDistance) : 35f; } }

        private bool Known(Mission m)
        {
            if (_known) return _active;
            _known = true;
            try
            {
                if (m == null || !m.IsSiegeBattle) return _active = false;
                var pt = m.PlayerTeam;
                if (pt == null) return _active = false;
                bool pl = false;
                if (pt.Side == BattleSideEnum.Attacker)
                {
                    // 387c: gracz atakuje (wodz albo sierzant w armii) - tylko walka o mury twierdzy; sala lorda poza regula (ranni leza za wlasna linia)
                    if (!PlayerSideOn) return _active = false;
                    var pme = MapEvent.PlayerMapEvent;
                    if (pme == null || !pme.IsSiegeAssault || pme.IsNavalMapEvent || pme.PlayerSide != BattleSideEnum.Attacker) return _active = false;
                    var pst = pme.MapEventSettlement;
                    if (pst == null || !pst.IsFortification || pst.CurrentSiegeState != Settlement.SiegeState.OnTheWalls) return _active = false;
                    pl = true;
                }
                else if (pt.Side != BattleSideEnum.Defender) return _active = false;
                _me = MapEvent.PlayerMapEvent;
                if (_me == null || !_me.IsSiegeAssault) return _active = false;
                if (!Book.TryGetValue(_me, out _t)) { _t = new Tally(); Book[_me] = _t; }
                if (pl) { _pl = true; _t.Pl = true; }
                var st = _me.MapEventSettlement;
                _t.St = st != null ? st.StringId : "-";
                _t.Missions++;
                _active = true;
            }
            catch (Exception e) { Stumble("Known", e); _active = false; }
            return _active;
        }

        /// <summary>Raz: punkty murow - bramy, odcinki murow, drabiny, cele taranow i wiez.</summary>
        private void Points(Mission m)
        {
            _ptsDone = true;
            var all = m.MissionObjects;
            int n = all != null ? all.Count : 0;
            for (int i = 0; i < n; i++)
            {
                try
                {
                    var o = all[i];
                    if (o == null) continue;
                    if (o is CastleGate || o is WallSegment || o is SiegeLadder)
                    {
                        _pts.Add(o.GameEntity.GlobalPosition.AsVec2);
                        var k = o as ICastleKeyPosition;
                        if (k != null) _pts.Add(k.GetPosition().AsVec2);
                    }
                    else if (o is BatteringRam r) Target(r.TargetCastlePosition);
                    else if (o is SiegeTower t) Target(t.TargetCastlePosition);
                }
                catch (Exception e) { Stumble("Points", e); }
            }
        }

        /// <summary>Cel taranu / wiezy (brama albo odcinek muru): pozycja kluczowa jak w 370 (MachText), inaczej srodek obiektu.</summary>
        private void Target(MissionObject target)
        {
            if (target == null) return;
            var k = target as ICastleKeyPosition;
            _pts.Add(k != null ? k.GetPosition().AsVec2 : target.GameEntity.GlobalPosition.AsVec2);
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            if (agentState != AgentState.Unconscious || affectedAgent == null) return;
            var m = Mission;
            if (!Known(m)) return;
            try
            {
                if (!affectedAgent.IsHuman) return;
                var team = affectedAgent.Team;
                if (team == null || team.Side != BattleSideEnum.Attacker) return;
                if (!_ptsDone) Points(m);
                // kategoria polozenia (agent jeszcze istnieje - Mission.OnAgentRemoved)
                int cat;   // 0 w srodku, 1 pod murami, 2 D-2D, 3 dalej, 4 bez pozycji
                try
                {
                    if (affectedAgent.GetCurrentNavigationFaceId() % 10 == 1) cat = 0;
                    else if (_pts.Count == 0) cat = 4;
                    else
                    {
                        var p = affectedAgent.Position.AsVec2;
                        float best = float.MaxValue;
                        for (int i = 0; i < _pts.Count; i++) { float d = p.Distance(_pts[i]); if (d < best) best = d; }
                        float D = Dist;
                        cat = best <= D ? 1 : best <= 2f * D ? 2 : 3;
                    }
                }
                catch (Exception e) { Stumble("Pos", e); cat = 4; }
                switch (cat) { case 0: _inside++; _t.Inside++; break; case 1: _near++; _t.Near++; break; case 2: _mid++; _t.Mid++; break; case 3: _far++; _t.Far++; break; default: _nopos++; _t.NoPos++; break; }
                if (cat > 1) return;
                var origin = affectedAgent.Origin;
                var pb = origin != null ? origin.BattleCombatant as PartyBase : null;
                var co = affectedAgent.Character as CharacterObject;
                if (pb == null || co == null) { Stumble("Origin", null); return; }
                if (co.IsHero)
                {
                    var h = co.HeroObject;
                    if (h != null && h != Hero.MainHero && _t.Heroes.Add(h)) _heroes.Add(h.Name != null ? h.Name.ToString() : h.StringId);   // 387c: sam gracz nigdy
                    return;
                }
                if (_pl) { if (pb == PartyBase.MainParty) _t.PlNear++; else _t.AllyNear++; }
                Dictionary<CharacterObject, int> d2;
                if (!_t.Troops.TryGetValue(pb, out d2)) { d2 = new Dictionary<CharacterObject, int>(); _t.Troops[pb] = d2; }
                int c;
                d2.TryGetValue(co, out c);
                d2[co] = c + 1;
            }
            catch (Exception e) { Stumble("Removed", e); }
        }

        /// <summary>387c: gracz konczy misje odwrotem (TAB / potwierdzenie; takze wyjscie po upadku bohatera) - Mission.RetreatMission. Tylko znacznik.</summary>
        public override void OnRetreatMission()
        {
            _retreat = true;
        }

        protected override void OnEndMission()
        {
            try
            {
                if (Known(Mission) && _t != null)   // takze misja bez rannych - wynik ostatniej misji (odwrot wroga) musi byc jej wlasny
                {
                    var r = Mission != null ? Mission.MissionResult : null;
                    _t.LastRetreated = !_pl && r != null && r.EnemyRetreated;
                    _t.LastBroken = _t.Broken;   // zalamanie scisle w tej misji (recenzja 2: misja 1 z 371 bez odwrotu, misja 2 z odwrotem bez 371 - bez przenosin)
                    _t.Broken = false;
                    if (_pl)
                    {
                        // 387c: wynik ostatniej misji gracza-atakujacego. Mury wziete = AttackerVictory / DefenderPullBack (to samo, co plWon w SiegeBreak371)
                        _t.LastHeld = r == null || (r.BattleState != BattleState.AttackerVictory && r.BattleState != BattleState.DefenderPullBack);
                        _t.LastLeft = _retreat || r == null || r.PlayerDefeated;
                        _t.LastWhy = _t.LastBroken ? "zalamanie 371" : _retreat ? "odwrot reczny" : r == null ? "koniec bez wyniku" : r.PlayerDefeated ? "gracz pokonany" : "bez zalamania i bez odwrotu";
                    }
                    int tot = _inside + _near + _mid + _far + _nopos;
                    string D = Dist.ToString("0", Inv), D2 = (2f * Dist).ToString("0", Inv);
                    Log.Info("Ranni pod murami (373): koniec misji | osada " + _t.St + " | rannych atakujacych " + tot + ": w srodku " + _inside + ", pod murami (<=" + D + " m) " + _near
                             + ", " + D + "-" + D2 + " m " + _mid + ", dalej " + _far + ", bez pozycji " + _nopos + " | punktow murow " + _pts.Count
                             + " | bohaterowie [" + string.Join(", ", _heroes) + "] | zalamanie 371 " + (_t.LastBroken ? "tak" : "nie") + ", wrog odstapil " + (_t.LastRetreated ? "tak" : "nie")
                             + " | w bitwie (misji " + _t.Missions + "): w srodku " + _t.Inside + ", pod murami " + _t.Near + ", dalej " + (_t.Mid + _t.Far) + " | potkniecia " + _stumbles + "."
                             + (_pl ? " (387c: gracz atakuje - zolnierze zapisani w bitwie: gracz " + _t.PlNear + ", sojusznicy " + _t.AllyNear + " | mury utrzymane " + (_t.LastHeld ? "tak" : "nie")
                                 + ", gracz zszedl z pola " + (_t.LastLeft ? "tak" : "nie") + " [" + _t.LastWhy + "], wynik misji " + (r == null ? "-" : r.BattleState.ToString()) + ")" : ""));
                }
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _active = false;
            _pts.Clear();
            base.OnEndMission();
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            if (e != null) Log.Error("SiegeWounded373." + where, e);
            else Log.Info("Ranni pod murami (373): potkniecie " + where + ".");
        }
    }

    /// <summary>373: nasluch MapEventEnded - ranni spod murow do lochu obroncow po zalamaniu szturmu (dodany PO ArmouryBehavior, wiec - 388b - biegnie PRZED jego nasluchami, takze przed SiegeWalls.OnMapEventEnded: MbEvent.AddNonSerializedListener wstawia na poczatek listy).</summary>
    internal sealed class SiegeWounded373Behavior : CampaignBehaviorBase
    {
        private const int ErrMax = 5;
        private static int _errs;

        public override void RegisterEvents()
        {
            _errs = 0;   // limit wpisow potkniec do logu na kampanie, nie na proces
            int stale = SiegeWounded373.Book.Count;
            SiegeWounded373.Book.Clear();
            SiegeSpoils380.Reset();
            Reset388();
            if (stale > 0) Log.Info("Ranni pod murami (373): ksiega z poprzedniej gry wyczyszczona (" + stale + " bitew bez rozliczenia).");
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            // 373b: armie z partia bez wodza (zapis sprzed poprawki) - naprawa + bezpieczniki Army.HourlyTick / CalculatePartyInfluenceCost
            ArmyRepair373b.Reset();
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter g) { ArmyRepair373b.SessionStart(); });
            CampaignEvents.TickEvent.AddNonSerializedListener(this, ArmyRepair373b.OnTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, ArmyRepair373b.Daily);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, Daily388);   // 388: linia doby
        }

        public override void SyncData(IDataStore dataStore) { }

        private static void Stumble(string where, Exception e, ref int s)
        {
            s++;
            if (_errs >= ErrMax) return;
            _errs++;
            Log.Error("SiegeWounded373Behavior." + where, e);
        }

        // ------------------------------------------------------------ 388: ranni pod murami po szturmie odpartym BEZ misji
        // (Jeff 10.11 "rob caly punkt 2" - 2d: "Ranni pod murami (373) ida do lochu tylko po bitwie, ktora sam grasz; przy rozstrzygnieciu bez misji nie").
        // Odparcie bez misji = OB2 (SiegeWalls.UpdatePost -> Repel(finish) -> FinishBattleAndKeepSiegeEvent): bitwa bez zwyciezcy, FinalizeEventAux bez
        // CaptureDefeatedPartyMembers - ranni atakujacy zostaja w swoich partiach. Ranni Z TEGO szturmu = MapEventParty.WoundedInBattle (symulacja:
        // MapEventSide.ApplySimulationDamageToSelectedTroop -> MapEventParty.OnTroopWounded), migawki nie trzeba. Symulacja nie zna pozycji, wiec udzial
        // (SiegeWoundedSimulatedShare, 0.5: misja 09.11 w Goldentooth dala 69% rannych do lochu, ale tam byl pogrom pod murami, a OB2 to uporzadkowany odwrot).
        // Sufit: wolne miejsce w lochu (PartyBase.PrisonerSizeLimit - gra sama go nie pilnuje) i nie wiecej niz obroncow na nogach. Ksiega ludzi zamknieta:
        // MemberRoster -n (ranni) = loch +n (ranni), PeopleBook.Transfer(FCaptured). Bohater: jak 373 / 373b - wodz partii i wodz strony zostaja.
        // Okien 380 nie ma (gracza w bitwie nie ma). Partia bez wodza nie jest oprozniana do zera (zostaje 1 czlowiek). Stan tylko w pamieci.
        // 388b: rozliczenie wolane WPROST z SiegeWalls.OnMapEventEnded (galaz a.Repelled) - wpis-zbior "_sim" z 388 powstawal PO nasluchu, ktory mial go zuzyc
        // (nasluchy MapEventEnded biegna od ostatnio zarejestrowanego, cs_full 22405-22425; log 09.11 19:42:55: linia 373 przed linia przerwy 371) i 388 nie dzialalo nigdy.
        private static int _dRep, _dWnd, _dMoved, _dCutPrison, _dCutDef, _dHero, _dSt, _dSkip;

        private sealed class Cand { public CharacterObject Troop; public int Want; }

        private static void Reset388()
        {
            _dRep = _dWnd = _dMoved = _dCutPrison = _dCutDef = _dHero = _dSt = _dSkip = 0;
        }

        private static void Daily388()
        {
            try
            {
                if (_dRep + _dWnd + _dMoved + _dCutPrison + _dCutDef + _dHero + _dSt + _dSkip == 0) return;   // 388b: bez linii przy samych zerach
                var set = Settings.Current;
                bool on = set != null && set.SiegeWoundedTakenPrisoner && set.SiegeWoundedSimulated && set.SiegeWoundedSimulatedShare > 0f;
                Log.Info("Ranni pod murami (388) doba: dzien " + (int)CampaignTime.Now.ToDays + " | czynne " + (on ? "tak" : "nie") + ", udzial "
                         + (set != null ? set.SiegeWoundedSimulatedShare.ToString("0.00", CultureInfo.InvariantCulture) : "-") + " | odparc bez misji " + _dRep + ", rannych atakujacych " + _dWnd
                         + ", do lochu " + _dMoved + ", obciete lochem " + _dCutPrison + ", obciete zaloga " + _dCutDef + ", bohaterowie " + _dHero
                         + " | pominiete (misja 373 / nie szturm / zwyciezca) " + _dSkip + " | potkniecia " + _dSt + ".");
                _dRep = _dWnd = _dMoved = _dCutPrison = _dCutDef = _dHero = _dSt = _dSkip = 0;
            }
            catch (Exception e) { int s = 0; Stumble("Daily388", e, ref s); }
        }

        /// <summary>388b: wolane wprost z SiegeWalls.OnMapEventEnded (odparcie OB2 bez misji, nie bitwa gracza). Biegnie po nasluchach ArmouryBehavior
        /// zarejestrowanych pozniej niz SiegeWalls (BattleChronicle, AiWear, PeopleLedger, PeopleBook, ArmouryBehavior.OnMapEventEnded...) i przed wczesniejszymi
        /// (SiegeRansom, NoticeBoard, Rumours, SiegeSurrender374 - zaden nie czyta rannych atakujacych z rosterow). Nie rzuca.</summary>
        internal static void Sim388(MapEvent me)
        {
            int s = 0;
            try
            {
                var set = Settings.Current;
                if (set == null || !set.SiegeWoundedTakenPrisoner || !set.SiegeWoundedSimulated) return;
                float share = Math.Max(0f, Math.Min(1f, set.SiegeWoundedSimulatedShare));
                if (share <= 0f) return;
                if (!me.IsSiegeAssault || me.WinningSide != BattleSideEnum.None || me.IsPlayerMapEvent || me.AttackerSide == null || me.DefenderSide == null) { _dSkip++; return; }
                var st = me.MapEventSettlement;
                var captor = st != null ? st.Party : null;
                if (captor == null || !st.IsFortification) { _dSkip++; return; }
                var prison = captor.PrisonRoster;
                int l0 = prison.TotalManCount, limit = captor.PrisonerSizeLimit, free = Math.Max(0, limit - l0);
                int def = Math.Max(0, me.DefenderSide.TroopCount);
                int cap0 = Math.Min(free, def), cap = cap0;
                Hero leader = null;
                try { var lp = me.AttackerSide.LeaderParty; leader = lp != null ? lp.LeaderHero : null; } catch { leader = null; }
                // 1. kandydaci: ranni z tego szturmu, ktorzy wciaz sa rannymi swojej partii
                var cands = new Dictionary<PartyBase, List<Cand>>();
                var heroes = new List<Hero>();
                int wounded = 0, parties = 0, wantSum = 0, notTaken = 0, gone = 0, hSeen = 0, hLeads = 0, hMissed = 0, hStay = 0;
                var model = Campaign.Current.Models.BattleRewardModel;
                foreach (var mep in me.AttackerSide.Parties)
                {
                    try
                    {
                        var pb = mep != null ? mep.Party : null;
                        var wb = mep != null ? mep.WoundedInBattle : null;
                        if (pb == null || !pb.IsActive || wb == null || wb.Count == 0) continue;
                        parties++;
                        var mr = pb.MemberRoster;
                        for (int i = 0; i < wb.Count; i++)
                        {
                            try
                            {
                                var el = wb.GetElementCopyAtIndex(i);
                                var troop = el.Character;
                                int cnt = el.Number;
                                if (troop == null || cnt <= 0) continue;
                                if (troop.IsHero)
                                {
                                    hSeen++;
                                    var h = troop.HeroObject;
                                    var hp = h != null ? h.PartyBelongedTo : null;
                                    if (h == null || h == Hero.MainHero || !h.IsAlive || !h.IsWounded || h.IsPrisoner || hp == null || hp.Party != pb
                                        || h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None || h.Occupation == Occupation.Special || !h.CanBecomePrisoner())
                                    { hMissed++; continue; }
                                    if (h == leader || hp.LeaderHero == h) { hLeads++; continue; }   // 373b: wodz zostaje z ludzmi
                                    if (MBRandom.RandomFloat < share) heroes.Add(h); else hStay++;
                                    continue;
                                }
                                wounded += cnt;
                                if (!model.CanTroopBeTakenPrisoner(troop)) { notTaken += cnt; continue; }
                                int idx = mr.FindIndexOfTroop(troop);
                                int w = Math.Min(cnt, idx >= 0 ? mr.GetElementWoundedNumber(idx) : 0);
                                gone += cnt - w;
                                if (w <= 0) continue;
                                int want = Math.Min(w, MBRandom.RoundRandomized(w * share));
                                if (want <= 0) continue;
                                List<Cand> l;
                                if (!cands.TryGetValue(pb, out l)) { l = new List<Cand>(); cands[pb] = l; }
                                l.Add(new Cand { Troop = troop, Want = want });
                                wantSum += want;
                            }
                            catch (Exception e) { Stumble("Sim388.Cand", e, ref s); }
                        }
                    }
                    catch (Exception e) { Stumble("Sim388.Party", e, ref s); }
                }
                _dRep++; _dWnd += wounded;
                // 2. bohaterowie (nie-wodzowie) - kazdy zajmuje jedno miejsce sufitu
                var names = new List<string>();
                int hCut = 0;
                foreach (var h in heroes)
                {
                    try
                    {
                        if (cap <= 0) { hCut++; continue; }
                        TakePrisonerAction.Apply(captor, h);
                        cap--;
                        names.Add(h.Name != null ? h.Name.ToString() : h.StringId);
                    }
                    catch (Exception e) { Stumble("Sim388.Hero", e, ref s); }
                }
                // 3. zolnierze: ta sama skala dla wszystkich partii, gdy chetnych wiecej niz sufit
                double k = wantSum > cap ? cap / (double)Math.Max(1, wantSum) : 1.0;
                int moved = 0, emptied = 0, kept = 0;
                double book = 0.0;
                var types = new Dictionary<string, int>();
                foreach (var kv in cands)
                {
                    if (cap <= 0) break;
                    var pb = kv.Key;
                    try
                    {
                        var mr = pb.MemberRoster;
                        int from0 = PeopleBook.Men(mr), to0 = PeopleBook.Men(prison), here = 0;
                        bool noLead = pb.LeaderHero == null;
                        foreach (var c in kv.Value)
                        {
                            try
                            {
                                if (cap <= 0) break;
                                int n = k < 1.0 ? Math.Min(c.Want, MBRandom.RoundRandomized((float)(c.Want * k))) : c.Want;
                                int idx = mr.FindIndexOfTroop(c.Troop);
                                n = Math.Min(Math.Min(n, cap), idx >= 0 ? mr.GetElementWoundedNumber(idx) : 0);
                                if (noLead && mr.TotalManCount - n <= 0) { n = mr.TotalManCount - 1; kept++; }   // partia bez wodza nie zostaje pusta
                                if (n <= 0) continue;
                                mr.AddToCounts(c.Troop, -n, false, -n);
                                prison.AddToCounts(c.Troop, n, false, n);
                                PlayerCaptives385d.Note(captor, pb, c.Troop, n);   // 385d: rejestr zolnierzy gracza w niewoli (liczy tylko partie gracza)
                                moved += n; here += n; cap -= n;
                                string id = c.Troop.StringId;
                                int t0; types.TryGetValue(id, out t0); types[id] = t0 + n;
                            }
                            catch (Exception e) { Stumble("Sim388.Troop", e, ref s); }
                        }
                        if (here > 0)
                        {
                            try { book += PeopleBook.Transfer(mr, from0, prison, to0, PeopleBook.FCaptured); } catch (Exception e) { Stumble("Sim388.Book", e, ref s); }
                            if (pb.NumberOfAllMembers == 0) emptied++;
                        }
                    }
                    catch (Exception e) { Stumble("Sim388.Move", e, ref s); }
                }
                int cut = wantSum + heroes.Count > cap0 ? Math.Max(0, wantSum - moved) + hCut : 0;   // 388b: obciecie liczone tylko, gdy sufit naprawde ciasny (nie roznice zaokraglen)
                bool byPrison = free <= def;
                if (cut > 0) { if (byPrison) _dCutPrison += cut; else _dCutDef += cut; }
                _dMoved += moved; _dHero += names.Count; _dSt += s;
                bool mine = false;
                try { mine = st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan; } catch { mine = false; }
                if (mine && moved + names.Count > 0)
                {
                    try
                    {
                        string nm = st.Name != null ? st.Name.ToString() : st.StringId;
                        int all = moved + names.Count;
                        InformationManager.DisplayMessage(new InformationMessage(all == 1 ? "1 wounded attacker left beneath the walls of " + nm + " is taken prisoner."
                            : all + " wounded attackers left beneath the walls of " + nm + " are taken prisoner.", Color.FromUint(0xFF9ACD65u)));
                    }
                    catch { }
                }
                var tb = new StringBuilder();
                foreach (var kv in types) tb.Append(tb.Length > 0 ? ", " : "").Append(kv.Key).Append(' ').Append(kv.Value);
                string owner = "-";
                try { owner = st.OwnerClan != null ? st.OwnerClan.StringId : "-"; } catch { }
                Log.Info("Ranni pod murami (388): dzien " + (int)CampaignTime.Now.ToDays + " | osada " + st.StringId + " (" + owner + (mine ? ", rod gracza" : "") + ") | szturm odparty bez misji"
                         + " | rannych atakujacych w szturmie " + wounded + " (partii " + parties + ")" + (gone > 0 ? ", nie bylo juz wsrod rannych partii " + gone : "")
                         + (notTaken > 0 ? ", bez niewoli (typ) " + notTaken : "") + " | udzial " + share.ToString("0.00", CultureInfo.InvariantCulture) + " -> chce " + wantSum
                         + " | loch " + l0 + "/" + limit + ", wolne " + free + ", obroncow " + def + " -> sufit " + cap0 + " (skala " + k.ToString("0.00", CultureInfo.InvariantCulture) + ")"
                         + " | do lochu " + moved + " (typy: " + (tb.Length > 0 ? tb.ToString() : "-") + ")" + (cut > 0 ? ", obciete " + cut + (byPrison ? " (loch)" : " (zaloga)") : "")
                         + " | bohaterowie ranni " + hSeen + ": do lochu " + names.Count + " [" + string.Join(", ", names) + "], wodzowie pominieci (373b) " + hLeads + ", wyniesieni " + hStay
                         + ", poza regula " + hMissed + (hCut > 0 ? ", bez miejsca " + hCut : "") + " | loch po " + prison.TotalManCount + " | ksiega ludzi " + book.ToString("0", CultureInfo.InvariantCulture)
                         + " | partie oproznione " + emptied + (kept > 0 ? ", bez wodza - zostawiony 1 czlowiek " + kept : "") + " | potkniecia " + s + ".");
            }
            catch (Exception e) { Stumble("Sim388", e, ref s); _dSt += s; }
        }

        // ------------------------------------------------------------ 387c: ranni GRACZA i jego sojusznikow spod murow do lochu obroncow
        /// <summary>387c: bitwa szturmu z graczem po stronie atakujacych skonczona BEZ zwyciezcy (PlayerEncounter.Finish -> LeaveBattle -> FinalizeEvent, gdy
        /// gracz jest wodzem strony), ostatnia misja odparta (zalamanie 371/387 albo - z kluczem Siege Wounded Player On Retreat - odwrot / gracz pokonany).
        /// Zolnierze zapisani w misji (w srodku / pod murami): MemberRoster -n rannych = loch osady +n rannych, PeopleBook.Transfer(FCaptured); sufit ZAWSZE jak
        /// 388 / 373c (wolne miejsce lochu po bohaterach, nie wiecej niz obroncow na nogach, ta sama skala partii). Bohaterowie: filtr 373, wodz partii i wodz
        /// strony zostaja (373b), sam gracz nigdy; bohater z partii gracza / rodu gracza / towarzysz tylko z kluczem Siege Wounded Player Companions.
        /// Partia bez wodza nie do zera (jak 388). SiegeSpoils380 NIE wolane (okna sa dla obroncy). Nie rzuca.</summary>
        private static void Player387c(MapEvent me, SiegeWounded373.Tally t)
        {
            int s = 0;
            try
            {
                var set = Settings.Current;
                int rec = t.Inside + t.Near;
                var st = me.MapEventSettlement;
                var captor = st != null ? st.Party : null;
                string no = null;
                if (set == null || !set.SiegeWoundedTakenPrisoner || !set.SiegeWoundedPlayerSide) no = "klucz wylaczony";
                else if (!me.IsSiegeAssault || me.AttackerSide == null || me.DefenderSide == null) no = "nie szturm";
                else if (me.WinningSide != BattleSideEnum.None) no = "zwyciezca " + me.WinningSide + " - rozlicza gra";
                else if (me.DiplomaticallyFinished) no = "bitwa zakonczona pokojem";
                else if (!me.IsPlayerMapEvent || me.PlayerSide != BattleSideEnum.Attacker) no = "gracz opuscil bitwe przed jej koncem";
                else if (captor == null || !st.IsFortification) no = "brak twierdzy";
                else if (st.CurrentSiegeState != Settlement.SiegeState.OnTheWalls) no = "mury wziete (sala lorda)";
                else if (!t.LastHeld) no = "ostatnia misja wygrana";
                else if (!t.LastBroken && !t.LastLeft) no = "ostatnia misja bez zalamania i bez odwrotu";
                else if (!t.LastBroken && !set.SiegeWoundedPlayerOnRetreat) no = "odwrot bez zalamania, klucz Siege Wounded Player On Retreat NIE";
                if (no != null)
                {
                    if (rec + t.Heroes.Count > 0)
                        Log.Info("Ranni pod murami (387c): osada " + t.St + " | bez przenosin (" + no + "; ostatnia misja: " + t.LastWhy + ", wynik bitwy " + me.BattleState + ") - "
                                 + rec + " rannych pod murami (zolnierze gracza " + t.PlNear + ", sojusznikow " + t.AllyNear + ", bohaterowie " + t.Heroes.Count + ") zostaje jak w grze.");
                    return;
                }
                string stName = st.Name != null ? st.Name.ToString() : st.StringId;
                var parties = new HashSet<PartyBase>();
                foreach (var mep in me.AttackerSide.Parties) if (mep != null && mep.Party != null) parties.Add(mep.Party);
                var prison = captor.PrisonRoster;
                int l0 = prison.TotalManCount;
                // 1. bohaterowie - przed zolnierzami, bez limitu (jak 373c)
                var names = new List<string>();
                var own = new List<string>();
                int hMissed = 0, hLeads = 0, hKept = 0;
                bool comp = set.SiegeWoundedPlayerCompanions;
                Hero leader = null;
                try { var lp = me.AttackerSide.LeaderParty; leader = lp != null ? lp.LeaderHero : null; } catch { leader = null; }
                foreach (var h in t.Heroes)
                {
                    try
                    {
                        var hp = h != null ? h.PartyBelongedTo : null;
                        if (h == null || h == Hero.MainHero || !h.IsAlive || !h.IsWounded || h.IsPrisoner || hp == null || !parties.Contains(hp.Party)
                            || h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None || h.Occupation == Occupation.Special || !h.CanBecomePrisoner())
                        { hMissed++; continue; }
                        if (h == leader || hp.LeaderHero == h) { hLeads++; continue; }   // 373b: wodz zostaje z ludzmi
                        bool mineH = hp == MobileParty.MainParty || h.Clan == Clan.PlayerClan || h.IsPlayerCompanion;
                        if (mineH && !comp) { hKept++; continue; }                       // klucz NIE: swoich bohaterow gracz zawsze wynosi
                        TakePrisonerAction.Apply(captor, h);
                        string nm = h.Name != null ? h.Name.ToString() : h.StringId;
                        if (mineH) own.Add(nm); else names.Add(nm);
                        try { InformationManager.DisplayMessage(new InformationMessage(nm + " lies wounded beneath the walls of " + stName + " and is taken prisoner.", Color.FromUint(0xFFE06050u))); } catch { }
                    }
                    catch (Exception e) { Stumble("387c.Hero", e, ref s); }
                }
                // 2. kandydaci: zapisani w misji, ktorzy wciaz sa rannymi swojej partii
                var cands = new Dictionary<PartyBase, List<Cand>>();
                int wantSum = 0, saved = 0, missing = 0, notTaken = 0;
                var model = Campaign.Current.Models.BattleRewardModel;
                foreach (var kv in t.Troops)
                {
                    var pb = kv.Key;
                    int recP = 0;
                    foreach (var x in kv.Value) recP += x.Value;
                    saved += recP;
                    try
                    {
                        if (pb == null || !parties.Contains(pb) || !pb.IsActive) { missing += recP; continue; }
                        var mr = pb.MemberRoster;
                        foreach (var x in kv.Value)
                        {
                            try
                            {
                                var troop = x.Key;
                                if (!model.CanTroopBeTakenPrisoner(troop)) { notTaken += x.Value; continue; }
                                int idx = mr.FindIndexOfTroop(troop);
                                int w = idx >= 0 ? mr.GetElementWoundedNumber(idx) : 0;
                                int n = Math.Min(x.Value, w);
                                missing += x.Value - n;
                                if (n <= 0) continue;
                                List<Cand> l;
                                if (!cands.TryGetValue(pb, out l)) { l = new List<Cand>(); cands[pb] = l; }
                                l.Add(new Cand { Troop = troop, Want = n });
                                wantSum += n;
                            }
                            catch (Exception e) { Stumble("387c.Cand", e, ref s); }
                        }
                    }
                    catch (Exception e) { Stumble("387c.Party", e, ref s); }
                }
                // 3. sufit jak 388 / 373c: wolne miejsce lochu (po bohaterach) i nie wiecej niz obroncow na nogach. Potkniecie = NIKT nie idzie (ostroznie wobec gracza)
                int l1 = prison.TotalManCount, limit = 0, free = 0, def = 0, cap0 = 0;
                try
                {
                    limit = captor.PrisonerSizeLimit;
                    free = Math.Max(0, limit - l1);
                    def = Math.Max(0, me.DefenderSide.TroopCount);
                    cap0 = Math.Min(free, def);
                }
                catch (Exception e) { Stumble("387c.Limit", e, ref s); cap0 = 0; }
                int cap = cap0;
                double k = wantSum > cap ? cap / (double)Math.Max(1, wantSum) : 1.0;
                int moved = 0, mine = 0, ally = 0, emptied = 0, kept = 0;
                double book = 0.0;
                var types = new Dictionary<string, int>();
                foreach (var kv in cands)
                {
                    if (cap <= 0) break;
                    var pb = kv.Key;
                    try
                    {
                        var mr = pb.MemberRoster;
                        int from0 = PeopleBook.Men(mr), to0 = PeopleBook.Men(prison), here = 0;
                        bool isMain = pb == PartyBase.MainParty;
                        bool noLead = pb.LeaderHero == null;
                        foreach (var c in kv.Value)
                        {
                            try
                            {
                                if (cap <= 0) break;
                                var troop = c.Troop;
                                int n = k < 1.0 ? Math.Min(c.Want, MBRandom.RoundRandomized((float)(c.Want * k))) : c.Want;
                                int idx = mr.FindIndexOfTroop(troop);
                                n = Math.Min(Math.Min(n, cap), idx >= 0 ? mr.GetElementWoundedNumber(idx) : 0);
                                if (noLead && mr.TotalManCount - n <= 0) { n = mr.TotalManCount - 1; kept++; }   // partia bez wodza nie zostaje pusta (gra by ja skasowala)
                                if (n <= 0) continue;
                                mr.AddToCounts(troop, -n, false, -n);
                                prison.AddToCounts(troop, n, false, n);
                                PlayerCaptives385d.Note(captor, pb, troop, n);   // 385d: rejestr zolnierzy gracza w niewoli (druzyna gracza i druzyny lordow jego rodu)
                                moved += n; here += n; cap -= n;
                                if (isMain) mine += n; else ally += n;
                                string id = troop.StringId;
                                int c0; types.TryGetValue(id, out c0); types[id] = c0 + n;
                            }
                            catch (Exception e) { Stumble("387c.Troop", e, ref s); }
                        }
                        if (here > 0)
                        {
                            try { book += PeopleBook.Transfer(mr, from0, prison, to0, PeopleBook.FCaptured); } catch (Exception e) { Stumble("387c.Book", e, ref s); }
                            if (pb.NumberOfAllMembers == 0) emptied++;
                        }
                    }
                    catch (Exception e) { Stumble("387c.Move", e, ref s); }
                }
                int cut = wantSum > cap0 ? Math.Max(0, wantSum - moved) : 0;
                bool byPrison = free <= def;
                Color red = Color.FromUint(0xFFE06050u);
                if (mine > 0)
                    try { InformationManager.DisplayMessage(new InformationMessage(mine == 1 ? "1 of your wounded left beneath the walls of " + stName + " is taken prisoner." : mine + " of your wounded left beneath the walls of " + stName + " are taken prisoner.", red)); } catch { }
                if (ally > 0)
                    try { InformationManager.DisplayMessage(new InformationMessage(ally == 1 ? "1 wounded of your allies left beneath the walls of " + stName + " is taken prisoner." : ally + " wounded of your allies left beneath the walls of " + stName + " are taken prisoner.", red)); } catch { }
                var tb = new StringBuilder();
                foreach (var kv in types) tb.Append(tb.Length > 0 ? ", " : "").Append(kv.Key).Append(' ').Append(kv.Value);
                Log.Info("Ranni pod murami (387c): dzien " + (int)CampaignTime.Now.ToDays + " | osada " + t.St + " | gracz atakowal, szturm odparty: " + t.LastWhy
                         + " | zapisanych zolnierzy " + saved + " (gracz " + t.PlNear + ", sojusznicy " + t.AllyNear + "), nie bylo juz wsrod rannych partii " + missing
                         + (notTaken > 0 ? ", bez niewoli (typ) " + notTaken : "") + " -> chce " + wantSum
                         + " | loch po bohaterach " + l1 + "/" + limit + ", wolne " + free + ", obroncow " + def + " -> sufit " + cap0 + " (skala " + k.ToString("0.00", CultureInfo.InvariantCulture) + ")"
                         + " | do lochu " + moved + ": gracz " + mine + ", sojusznicy " + ally + " (typy: " + (tb.Length > 0 ? tb.ToString() : "-") + ")"
                         + (cut > 0 ? ", obciete " + cut + (byPrison ? " (loch)" : " (zaloga)") + " - wracaja z partia" : "")
                         + " | bohaterowie: do lochu " + names.Count + " [" + string.Join(", ", names) + "], rodu / towarzysze gracza " + own.Count + " [" + string.Join(", ", own) + "]"
                         + ", wyniesieni (klucz towarzyszy " + (comp ? "TAK" : "NIE") + ") " + hKept + ", wodzowie pominieci (373b) " + hLeads + ", poza regula " + hMissed
                         + " | loch " + l0 + " -> " + prison.TotalManCount + " | ksiega ludzi " + book.ToString("0", CultureInfo.InvariantCulture)
                         + " | partie oproznione " + emptied + (kept > 0 ? ", bez wodza - zostawiony 1 czlowiek " + kept : "") + " | potkniecia " + s + ".");
            }
            catch (Exception e) { Stumble("Player387c", e, ref s); }
        }

        private static void OnMapEventEnded(MapEvent me)
        {
            if (me == null) return;
            // 388b: tu tylko 373 (ksiega misji) - jak przed 388; odparcie bez misji rozlicza Sim388 wolane z SiegeWalls.OnMapEventEnded
            if (SiegeWounded373.Book.Count == 0) return;
            SiegeWounded373.Tally t;
            if (!SiegeWounded373.Book.TryGetValue(me, out t)) return;
            SiegeWounded373.Book.Remove(me);
            if (t.Pl) { Player387c(me, t); return; }   // 387c: gracz atakowal - osobne rozliczenie (bez okien 380); galaz obroncy ponizej bez zmian
            int s = 0;
            try
            {
                var set = Settings.Current;
                bool held = t.LastBroken && t.LastRetreated && me.WinningSide == BattleSideEnum.None && me.IsSiegeAssault;
                bool go = set != null && set.SiegeWoundedTakenPrisoner && held;
                var st = me.MapEventSettlement;
                var captor = st != null ? st.Party : null;
                if (held) SiegeSpoils380.NoteHeld(me);   // 380: obrona odparta - okno jencow i lupu (gdy ArmouryBehavior biegl po nas)
                if (!go || captor == null)
                {
                    if (t.Inside + t.Near > 0)
                        Log.Info("Ranni pod murami (373): osada " + t.St + " | bez przenosin (zalamanie " + (t.LastBroken ? "tak" : "nie") + ", wrog odstapil " + (t.LastRetreated ? "tak" : "nie")
                                 + ", wynik " + me.BattleState + (captor == null ? ", brak osady" : "") + ") - " + (t.Inside + t.Near) + " rannych pod murami zostaje jak w grze.");
                    return;
                }
                var parties = new HashSet<PartyBase>();
                foreach (var mep in me.AttackerSide.Parties) if (mep != null && mep.Party != null) parties.Add(mep.Party);
                var prison = captor.PrisonRoster;
                int l0 = prison.TotalManCount;
                int moved = 0, saved = 0, missing = 0, emptied = 0, notTaken = 0, kept = 0;
                double book = 0.0;
                var types = new Dictionary<string, int>();
                // 373c: bohaterowie spod murow PRZED zolnierzami i BEZ limitu (Jeff 11.11) - zolnierze dostaja to, co po nich zostanie wolne w lochu
                var names = new List<string>();
                int hMissed = 0, hLeads = 0;
                Hero leader = null;
                try { var lp = me.AttackerSide.LeaderParty; leader = lp != null ? lp.LeaderHero : null; } catch { leader = null; }
                foreach (var h in t.Heroes)
                {
                    try
                    {
                        var hp = h != null ? h.PartyBelongedTo : null;
                        if (h == null || h == Hero.MainHero || !h.IsAlive || !h.IsWounded || h.IsPrisoner || hp == null || !parties.Contains(hp.Party)
                            || h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None || h.Occupation == Occupation.Special || !h.CanBecomePrisoner())
                        { hMissed++; continue; }
                        // 373b: wodz partii / wodz armii / wodz strony zostaje z ludzmi (jak w grze bez 373) - inaczej partia bez wodza w armii = NRE w Army.HourlyTick
                        if (h == leader || hp.LeaderHero == h) { hLeads++; continue; }
                        TakePrisonerAction.Apply(captor, h);
                        string nm = h.Name != null ? h.Name.ToString() : h.StringId;
                        names.Add(nm);
                        try { InformationManager.DisplayMessage(new InformationMessage(nm + " lies wounded beneath the walls and is taken prisoner.", Color.FromUint(0xFF9ACD65u))); } catch { }
                    }
                    catch (Exception e) { Stumble("Hero", e, ref s); }
                }
                // 373c: kandydaci - zapisani w misji, ktorzy wciaz sa rannymi swojej partii (stan PO wzieciu bohaterow)
                var cands = new Dictionary<PartyBase, List<Cand>>();
                int wantSum = 0;
                var model = Campaign.Current.Models.BattleRewardModel;
                foreach (var kv in t.Troops)
                {
                    var pb = kv.Key;
                    int rec = 0;
                    foreach (var x in kv.Value) rec += x.Value;
                    saved += rec;
                    try
                    {
                        if (pb == null || !parties.Contains(pb) || !pb.IsActive) { missing += rec; continue; }
                        var mr = pb.MemberRoster;
                        foreach (var x in kv.Value)
                        {
                            try
                            {
                                var troop = x.Key;
                                if (!model.CanTroopBeTakenPrisoner(troop)) { notTaken += x.Value; continue; }
                                int idx = mr.FindIndexOfTroop(troop);
                                int w = idx >= 0 ? mr.GetElementWoundedNumber(idx) : 0;
                                int n = Math.Min(x.Value, w);
                                missing += x.Value - n;
                                if (n <= 0) continue;
                                List<Cand> l;
                                if (!cands.TryGetValue(pb, out l)) { l = new List<Cand>(); cands[pb] = l; }
                                l.Add(new Cand { Troop = troop, Want = n });
                                wantSum += n;
                            }
                            catch (Exception e) { Stumble("Cand", e, ref s); }
                        }
                    }
                    catch (Exception e) { Stumble("Party", e, ref s); }
                }
                // 373c: sufit zolnierzy jak w 388 - wolne miejsce lochu (po bohaterach) i nie wiecej niz obroncow na nogach; klucz NIE = bez sufitu (stan sprzed 373c)
                bool lim = set.SiegeWoundedDungeonLimit;
                int l1 = prison.TotalManCount, limit = 0, free = 0, def = 0, cap0 = int.MaxValue;
                if (lim)
                {
                    try
                    {
                        limit = captor.PrisonerSizeLimit;
                        free = Math.Max(0, limit - l1);
                        def = Math.Max(0, me.DefenderSide.TroopCount);
                        cap0 = Math.Min(free, def);
                    }
                    catch (Exception e) { Stumble("Limit", e, ref s); lim = false; cap0 = int.MaxValue; }   // bez sufitu jak przed 373c, potkniecie w logu
                }
                int cap = cap0;
                double k = wantSum > cap ? cap / (double)Math.Max(1, wantSum) : 1.0;   // ta sama skala dla wszystkich partii
                foreach (var kv in cands)
                {
                    if (cap <= 0) break;
                    var pb = kv.Key;
                    try
                    {
                        var mr = pb.MemberRoster;
                        int from0 = PeopleBook.Men(mr), to0 = PeopleBook.Men(prison), here = 0;
                        bool noLead = pb.LeaderHero == null;
                        foreach (var c in kv.Value)
                        {
                            try
                            {
                                if (cap <= 0) break;
                                var troop = c.Troop;
                                int n = k < 1.0 ? Math.Min(c.Want, MBRandom.RoundRandomized((float)(c.Want * k))) : c.Want;
                                int idx = mr.FindIndexOfTroop(troop);
                                n = Math.Min(Math.Min(n, cap), idx >= 0 ? mr.GetElementWoundedNumber(idx) : 0);
                                if (noLead && mr.TotalManCount - n <= 0) { n = mr.TotalManCount - 1; kept++; }   // 373c-p: partia bez wodza nie zostaje pusta (jak 388 / 387c)
                                if (n <= 0) continue;
                                mr.AddToCounts(troop, -n, false, -n);
                                prison.AddToCounts(troop, n, false, n);
                                PlayerCaptives385d.Note(captor, pb, troop, n);   // 385d: rejestr (gracz-obronca bierze cudzych - Note liczy tylko partie gracza, tu zwykle nic)
                                moved += n; here += n; cap -= n;
                                SiegeSpoils380.NotePrisoners(me, captor, troop, n);   // 380: ci jency do wyboru w oknie jencow (tylko faktycznie przeniesieni)
                                string id = troop.StringId;
                                int c0; types.TryGetValue(id, out c0); types[id] = c0 + n;
                            }
                            catch (Exception e) { Stumble("Troop", e, ref s); }
                        }
                        if (here > 0)
                        {
                            try { book += PeopleBook.Transfer(mr, from0, prison, to0, PeopleBook.FCaptured); } catch (Exception e) { Stumble("Book", e, ref s); }
                            if (pb.NumberOfAllMembers == 0) emptied++;
                        }
                    }
                    catch (Exception e) { Stumble("Move", e, ref s); }
                }
                int cut = lim && wantSum > cap0 ? Math.Max(0, wantSum - moved) : 0;   // nadwyzka wraca ze swoja partia jak w grze (nikt jej nie rusza)
                bool byPrison = free <= def;
                // 373c-p: najpierw wzieci, potem odniesieni ("more" tylko wtedy, gdy linia o wzietych padla)
                if (moved > 0)
                    try { InformationManager.DisplayMessage(new InformationMessage(moved == 1 ? "1 wounded attacker left beneath the walls is taken prisoner." : moved + " wounded attackers left beneath the walls are taken prisoner.", Color.FromUint(0xFF9ACD65u))); } catch { }
                if (cut > 0)
                    try
                    {
                        string more = moved > 0 ? " more" : "";
                        InformationManager.DisplayMessage(new InformationMessage((cut == 1 ? "1" + more + " wounded attacker is carried back by his army: " : cut + more + " wounded attackers are carried back by their army: ")
                            + (byPrison ? "the dungeon has no room left." : "too few defenders are left standing to bring them in."), Color.FromUint(0xFF9ACD65u)));
                    }
                    catch { }
                var tb = new StringBuilder();
                foreach (var kv in types) tb.Append(tb.Length > 0 ? ", " : "").Append(kv.Key).Append(' ').Append(kv.Value);
                Log.Info("Ranni pod murami (373): osada " + t.St + " | do lochu " + moved + " (typy: " + (tb.Length > 0 ? tb.ToString() : "-") + "), bohaterowie " + names.Count
                         + " [" + string.Join(", ", names) + "]" + (hMissed > 0 ? " (pominieci " + hMissed + ")" : "") + " | wodzowie partii pominieci (373b) " + hLeads
                         + " | zapisanych " + saved + ", nie bylo juz wsrod rannych partii " + missing + (notTaken > 0 ? ", bez niewoli (typ) " + notTaken : "")
                         + " | sufit 373c " + (lim ? "chce " + wantSum + ", loch po bohaterach " + l1 + "/" + limit + ", wolne " + free + ", obroncow " + def + " -> sufit " + cap0
                             + " (skala " + k.ToString("0.00", CultureInfo.InvariantCulture) + ")" + (cut > 0 ? ", obciete " + cut + (byPrison ? " (loch)" : " (zaloga)") + " - wracaja z partia" : "")
                             : "wylaczony (chce " + wantSum + ")")
                         + " | loch " + l0 + " -> " + prison.TotalManCount + " | ksiega ludzi " + book.ToString("0", CultureInfo.InvariantCulture)
                         + " | partie oproznione " + emptied + (kept > 0 ? ", bez wodza - zostawiony 1 czlowiek " + kept : "") + " | potkniecia " + s + ".");
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e, ref s); }
        }
    }
}
