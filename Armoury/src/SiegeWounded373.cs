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
    ///  - przenosiny w MapEventEnded (nasz nasluch PO SiegeWalls.OnMapEventEnded - przerwa 371 liczona ze stanu sprzed przenosin), tylko gdy: szturm zalamal sie (371
    ///    NoteBreak), ostatnia misja skonczyla sie odwrotem wroga, bitwa bez zwyciezcy (WinningSide None) i IsSiegeAssault. Kazdy inny wynik (zamek padl, obronca
    ///    wygral bez zalamania) - wpis tylko usuniety (gra rozlicza jak dotad).
    ///  - zolnierze: n = min(zapisani, ranni typu w partii, ktora jest wciaz po stronie atakujacej) -&gt; MemberRoster -n (rannych) -&gt; loch osady +n (jako ranni, jak
    ///    CaptureDefeatedPartyMembers gry); ksiega ludzi PeopleBook.Transfer(FCaptured). Kazdy ranny ma jedno miejsce. Typ, ktorego gra nie bierze w niewole
    ///    (BattleRewardModel.CanTroopBeTakenPrisoner), zostaje.
    ///  - bohaterowie: zywy, ranny, nie jeniec, bez znaku smierci, CanBecomePrisoner, Occupation != Special, w partii po stronie atakujacej -&gt; TakePrisonerAction.Apply(osada).
    ///    373b (crash 09.11 19:43): WODZ PARTII (i wodz armii) zostaje w swojej partii jak w grze - TakePrisonerAction robi tylko RemovePartyLeader, armii nie
    ///    rusza; partia bez wodza zostawala w armii i Army.HourlyTick -> ThinkAboutCohesionBoost -> CalculatePartyInfluenceCost rzucal NRE (armyLeaderParty.LeaderHero).
    ///    Gra nigdy nie zostawia partii bez wodza w armii (KillCharacterAction: najpierw DisbandArmyAction / Army = null, potem nastepca albo StartDisband).
    ///  - zdarzenia OnPrisonerTaken (rozbieranie jencow 3792) NIE odpalamy - jak w grze przy zwyklym pojmaniu zolnierzy w bitwie (to robi tylko ekran / loot).
    /// Wyjatek per partia / typ / bohater = potkniecie, bez globalnego wylacznika. Ksiega tylko w pamieci (wczytanie w trakcie bitwy - gubi wpis, log przy czyszczeniu).
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
        }

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
                if (pt == null || pt.Side != BattleSideEnum.Defender) return _active = false;
                _me = MapEvent.PlayerMapEvent;
                if (_me == null || !_me.IsSiegeAssault) return _active = false;
                if (!Book.TryGetValue(_me, out _t)) { _t = new Tally(); Book[_me] = _t; }
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
                    if (h != null && _t.Heroes.Add(h)) _heroes.Add(h.Name != null ? h.Name.ToString() : h.StringId);
                    return;
                }
                Dictionary<CharacterObject, int> d2;
                if (!_t.Troops.TryGetValue(pb, out d2)) { d2 = new Dictionary<CharacterObject, int>(); _t.Troops[pb] = d2; }
                int c;
                d2.TryGetValue(co, out c);
                d2[co] = c + 1;
            }
            catch (Exception e) { Stumble("Removed", e); }
        }

        protected override void OnEndMission()
        {
            try
            {
                if (Known(Mission) && _t != null)   // takze misja bez rannych - wynik ostatniej misji (odwrot wroga) musi byc jej wlasny
                {
                    var r = Mission != null ? Mission.MissionResult : null;
                    _t.LastRetreated = r != null && r.EnemyRetreated;
                    _t.LastBroken = _t.Broken;   // zalamanie scisle w tej misji (recenzja 2: misja 1 z 371 bez odwrotu, misja 2 z odwrotem bez 371 - bez przenosin)
                    _t.Broken = false;
                    int tot = _inside + _near + _mid + _far + _nopos;
                    string D = Dist.ToString("0", Inv), D2 = (2f * Dist).ToString("0", Inv);
                    Log.Info("Ranni pod murami (373): koniec misji | osada " + _t.St + " | rannych atakujacych " + tot + ": w srodku " + _inside + ", pod murami (<=" + D + " m) " + _near
                             + ", " + D + "-" + D2 + " m " + _mid + ", dalej " + _far + ", bez pozycji " + _nopos + " | punktow murow " + _pts.Count
                             + " | bohaterowie [" + string.Join(", ", _heroes) + "] | zalamanie 371 " + (_t.LastBroken ? "tak" : "nie") + ", wrog odstapil " + (_t.LastRetreated ? "tak" : "nie")
                             + " | w bitwie (misji " + _t.Missions + "): w srodku " + _t.Inside + ", pod murami " + _t.Near + ", dalej " + (_t.Mid + _t.Far) + " | potkniecia " + _stumbles + ".");
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

    /// <summary>373: nasluch MapEventEnded - ranni spod murow do lochu obroncow po zalamaniu szturmu (dodany PO ArmouryBehavior, wiec po SiegeWalls.OnMapEventEnded).</summary>
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
            if (stale > 0) Log.Info("Ranni pod murami (373): ksiega z poprzedniej gry wyczyszczona (" + stale + " bitew bez rozliczenia).");
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            // 373b: armie z partia bez wodza (zapis sprzed poprawki) - naprawa + bezpieczniki Army.HourlyTick / CalculatePartyInfluenceCost
            ArmyRepair373b.Reset();
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter g) { ArmyRepair373b.SessionStart(); });
            CampaignEvents.TickEvent.AddNonSerializedListener(this, ArmyRepair373b.OnTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, ArmyRepair373b.Daily);
        }

        public override void SyncData(IDataStore dataStore) { }

        private static void Stumble(string where, Exception e, ref int s)
        {
            s++;
            if (_errs >= ErrMax) return;
            _errs++;
            Log.Error("SiegeWounded373Behavior." + where, e);
        }

        private static void OnMapEventEnded(MapEvent me)
        {
            if (me == null || SiegeWounded373.Book.Count == 0) return;
            SiegeWounded373.Tally t;
            if (!SiegeWounded373.Book.TryGetValue(me, out t)) return;
            SiegeWounded373.Book.Remove(me);
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
                int moved = 0, saved = 0, missing = 0, emptied = 0, notTaken = 0;
                double book = 0.0;
                var types = new Dictionary<string, int>();
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
                        int from0 = PeopleBook.Men(mr), to0 = PeopleBook.Men(prison), here = 0;
                        foreach (var x in kv.Value)
                        {
                            try
                            {
                                var troop = x.Key;
                                if (!Campaign.Current.Models.BattleRewardModel.CanTroopBeTakenPrisoner(troop)) { notTaken += x.Value; continue; }
                                int idx = mr.FindIndexOfTroop(troop);
                                int w = idx >= 0 ? mr.GetElementWoundedNumber(idx) : 0;
                                int n = Math.Min(x.Value, w);
                                missing += x.Value - n;
                                if (n <= 0) continue;
                                mr.AddToCounts(troop, -n, false, -n);
                                prison.AddToCounts(troop, n, false, n);
                                moved += n; here += n;
                                SiegeSpoils380.NotePrisoners(me, captor, troop, n);   // 380: ci jency do wyboru w oknie jencow
                                string id = troop.StringId;
                                int c; types.TryGetValue(id, out c); types[id] = c + n;
                            }
                            catch (Exception e) { Stumble("Troop", e, ref s); }
                        }
                        if (here > 0)
                        {
                            try { book += PeopleBook.Transfer(mr, from0, prison, to0, PeopleBook.FCaptured); } catch (Exception e) { Stumble("Book", e, ref s); }
                            if (pb.NumberOfAllMembers == 0) emptied++;
                        }
                    }
                    catch (Exception e) { Stumble("Party", e, ref s); }
                }
                // bohaterowie spod murow
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
                if (moved > 0)
                    try { InformationManager.DisplayMessage(new InformationMessage(moved == 1 ? "1 wounded attacker left beneath the walls is taken prisoner." : moved + " wounded attackers left beneath the walls are taken prisoner.", Color.FromUint(0xFF9ACD65u))); } catch { }
                var tb = new StringBuilder();
                foreach (var kv in types) tb.Append(tb.Length > 0 ? ", " : "").Append(kv.Key).Append(' ').Append(kv.Value);
                Log.Info("Ranni pod murami (373): osada " + t.St + " | do lochu " + moved + " (typy: " + (tb.Length > 0 ? tb.ToString() : "-") + "), bohaterowie " + names.Count
                         + " [" + string.Join(", ", names) + "]" + (hMissed > 0 ? " (pominieci " + hMissed + ")" : "") + " | wodzowie partii pominieci (373b) " + hLeads
                         + " | zapisanych " + saved + ", nie bylo juz wsrod rannych partii " + missing + (notTaken > 0 ? ", bez niewoli (typ) " + notTaken : "")
                         + " | loch " + l0 + " -> " + prison.TotalManCount + " | ksiega ludzi " + book.ToString("0", CultureInfo.InvariantCulture)
                         + " | partie oproznione " + emptied + " | potkniecia " + s + ".");
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e, ref s); }
        }
    }
}
