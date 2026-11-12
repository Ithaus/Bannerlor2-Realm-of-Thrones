using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 454 (pakiet 7 krok 7.5 audytu AI bitwy; Jeff 11.11 "rob wszystko po kolei tak jak radzisz"). OBRONA MUROW: ODWOD W SZYKU 8, WYCHODZI NA PRAWDZIWY PRZYCZOLEK.
    /// Dowod (dekompilacja 1.4.8, rally_dec/mb_full.cs): TacticDefendCastle (38652-39720) rozdziela cala piechote na pasy (CarryOutDefense: kazdy pas
    /// z GetDefenseState != Empty dostaje szyk z BehaviorDefendCastleKeyPosition, BalanceLaneDefenders wyrownuje je wedlug pojemnosci) - odwodu nie ma:
    /// pas z przyczolkiem (SiegeLane.LaneState Conceited = IsOpen, >= 30 atakujacych w regionie, >= 15 przy punkcie i < 15 obroncow, 44957-45067;
    /// stan trzyma sie, dopoki obroncow < 15) dostaje do odbicia (BehaviorRetakeCastleKeyPosition) tylko szyk "wolny" (list9), a zwykle wolnego nie ma;
    /// szyk awaryjny przeciw wrogom w srodku (BehaviorEliminateEnemyInsideCastle) dopiero przy InsideAttackerCount > 30 i tez tylko z wolnych.
    /// IsOpen nie jest wyzwalaczem (prawdziwe juz przy podniesionej drabinie / dostawionej wiezy - RefreshLane 45141-45179). InsideAttackerCount =
    /// atakujacy glownej druzyny na siatce zamku (face id % 10 == 1) poza strefa 15 m przy punktach pasow (LocateAttackers 43506-43570), pamiec 5 s.
    /// Szyk 8 (NumberOfRegularFormations) jest poza taktyka: TacticComponent / ManageFormationCounts / SplitFormationClassIntoGivenNumber / BalanceLaneDefenders /
    /// GetAIControlledFormationCount dzialaja na FormationsIncludingEmpty (0-7); szyk wodza obroncow nie powstaje pod RBM (CanTeamHaveGeneralsFormation,
    /// jak 381) - zajety szyk 8 (bez RBM: wodz) = odwodu nie tworzymy. RBM (StopUsingStrategicAreas itd.) i Vanilla AI Sieges nie ruszaja szyku 8.
    /// Regula (glowny watek, OnMissionTick co 1 s; tylko misja szturmu murow - nie wypad, nie morze; tylko glowna druzyna obroncow AI - Mission.DefenderTeam,
    /// nie druzyna gracza: gdy gracz broni, jego szyki sa jego; sojusznicy obroncow bez zmian):
    ///  - UTWORZENIE (raz, w fazie podejscia: 3-60 s po rozstawieniu, taktyka TacticDefendCastle w stanie ProperDefense, zaden pas Conceited, na kazdym pasie
    ///    < 5 atakujacych przy punkcie, nikt w srodku; miejsce = pozycja oczekiwania bramy wewnetrznej, a bez niej zewnetrznej - na siatce i w polu):
    ///    z kazdego szyku pasa (BehaviorDefendCastleKeyPosition, pod AI, 0-7) do 20% ludzi, szyk zostaje z >= 20; piesi bez broni dystansowej
    ///    (IsRangedCached), nie bohaterowie, nie chorazowie, nie odlaczeni, nie przy machinie, nie uciekajacy - najblizsi miejsca odwodu; razem >= 10,
    ///    inaczej odwodu nie ma. Przelozenie recznie Agent.Formation (wzor 381 / 372b / 453), szyk 8: MovementOrderMove na miejsce, twarza do wroga,
    ///    SetControlledByAI(false); gdy cos wlaczy AI szyku - z powrotem przy skanie (licznik).
    ///  - WYJSCIE (raz, zapadka): pas w stanie Conceited (przy kilku - najwiecej atakujacych przy punkcie) albo InsideAttackerCount >= 15 (pamiec gry),
    ///    albo taktyka obroncow wyszla ze stanu ProperDefense (wypad gry) - caly odwod do szyku celu: dla Conceited szyk pasa (GetLastAssignedFormation),
    ///    dla wrogow w srodku szyk BehaviorEliminateEnemyInsideCastle, potem BehaviorRetakeCastleKeyPosition; zapas - najblizszy szyk 0-7 pod AI z ludzmi,
    ///    nie strzelcy z murow; nie ma zadnego - szyk 8 dostaje MovementOrderCharge (ostatni ludzie zalogi). Przy NIE: zadnego odwodu - co do bitu jak
    ///    przed; ta sama decyzja (utworzenie i wyjscie) liczona i wypisana jako "bylby" (bez odczytu zapytan, ktore przeliczaja - liczniki z pamieci QueryData).
    /// Linie: "Oblezenie misja (370) odwod obrony (454)" przy utworzeniu i wyjsciu, "... (454) koniec" przy koncu misji (utworzony / powod, wielkosc,
    /// polegli w odwodzie przed wyjsciem, wyjscie: chwila, powod, cel, odleglosc, czas reakcji od pierwszego Conceited / wrogow w srodku, dotarcie polowy
    /// do szyku celu, AI szyku 8 cofniete). Wyjatek per czlowiek / szyk = potkniecie (pierwsze 3 do logu), bez globalnego wylacznika.
    /// 454-p (recenzja 454): taktyka gry liczy piechote tylko w szykach 0-7 (_meleeFormations, CheckAndChangeState mb_full 39668-39675) i przy
    /// piechocie < 0.33 * (piechota + strzelcy) RAZ NA STALE ustawia _areRangedNeededForLaneDefense = strzelcy schodza z murow do obrony pasow.
    /// Odwod w szyku 8 znika z tego rachunku, wiec: (a) odwod najwyzej tyle, by piechota gry zostala >= 0.40 * (piechota + strzelcy) (g <= M - 2/3 R;
    /// liczone z list taktyki, odczyt pol); za malo albo przelacznik juz wlaczony albo listy nieczytelne = odwodu nie ma; (b) wyjscie takze, gdy piechota
    /// gry (przy NIE: bez ludzi "bylby" odwodu) spadnie ponizej 0.37 * (piechota + strzelcy) - przed progiem gry; (c) linia konca mowi, czy i kiedy
    /// przelacznik gry zaskoczyl. Znane roznice wzgledem NIE bez zmiany kodu: wypad gry (IsSallyOutApplicable 38759) liczy sile obroncow bez szyku 8 -
    /// wypad rzadszy / pozniejszy, dopoki odwod stoi; przy pierwszym przyczolku gra moze przez <= 1 s (do naszego skanu) przydzielic mniej wolnych szykow.
    /// </summary>
    internal sealed class SiegeReserve454 : MissionLogic
    {
        private const string P = "Oblezenie misja (370) odwod obrony (454): ";
        private const float Every = 1f, StartDelay = 3f, NoDeployStart = 10f, FormWindow = 60f, ArriveDist = 20f;
        private const float Pct = 0.2f;
        private const float GameC = 0.33f, CapC = 0.40f, OutC = 0.37f;   // 454-p: prog lukow gry, zapas przy tworzeniu, wyjscie przed progiem
        private const int KeepMin = 20, ReserveMin = 10, WallMin = 5, InsideN = 15, LaneN = 3, ErrMax = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static FieldInfo _fTactic, _fMelee, _fRanged, _fRangedNeeded;
        private static bool _reflTried;

        // 454-p: przelacznik lukow gry (_areRangedNeededForLaneDefense)
        private float _swFirst = -1f;
        private bool _swKnown;
        private string _gauge = "-";

        private bool _known, _siege, _started, _ended, _keyOn;
        private float _startAt = -1f, _next, _t0;
        private int _stumbles, _errs;

        // utworzenie
        private bool _formed, _formGiveUp;
        private string _formWhy = "-", _place = "-";
        private float _formAt = -1f;
        private int _size;
        private Formation _f8;
        private WorldPosition _hold;
        private readonly List<Agent> _res = new List<Agent>();          // ludzie odwodu (przy NIE: kto bylby)
        private readonly StringBuilder _src = new StringBuilder(128);    // skad wzieci (do linii konca)

        // wyjscie
        private bool _out;
        private float _outAt = -1f, _outDist = -1f, _arriveAt = -1f;
        private int _outN, _lostBefore, _reAi;
        private string _outWhy = "-", _outTo = "-";
        private Formation _target;
        private readonly List<Agent> _sent = new List<Agent>();

        // pomiar
        private readonly float[] _concFirst = { -1f, -1f, -1f };
        private float _insideFirst = -1f;
        private int _insideMax;

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        internal static bool On { get { var s = Settings.Current; return s != null && s.WallDefenceReserve; } }

        private static void Refl()
        {
            if (_reflTried) return;
            _reflTried = true;
            try { _fTactic = AccessTools.Field(typeof(TeamAIComponent), "_currentTactic"); } catch { _fTactic = null; }
            try { _fMelee = AccessTools.Field(typeof(TacticDefendCastle), "_meleeFormations"); } catch { _fMelee = null; }
            try { _fRanged = AccessTools.Field(typeof(TacticDefendCastle), "_rangedFormations"); } catch { _fRanged = null; }
            try { _fRangedNeeded = AccessTools.Field(typeof(TacticDefendCastle), "_areRangedNeededForLaneDefense"); } catch { _fRangedNeeded = null; }
        }

        /// <summary>454-p: piechota i strzelcy tak, jak liczy je taktyka gry (listy szykow 0-7, CountOfUnits), i jej przelacznik lukow. Sam odczyt pol.</summary>
        private bool Gauge(TacticDefendCastle tac, out int melee, out int ranged, out bool sw)
        {
            melee = 0; ranged = 0; sw = false;
            if (tac == null || _fMelee == null || _fRanged == null || _fRangedNeeded == null) return false;
            try
            {
                var ml = _fMelee.GetValue(tac) as List<Formation>;
                var rl = _fRanged.GetValue(tac) as List<Formation>;
                if (ml == null || rl == null) return false;
                for (int i = 0; i < ml.Count; i++) { var f = ml[i]; if (f != null) melee += f.CountOfUnits; }
                for (int i = 0; i < rl.Count; i++) { var f = rl[i]; if (f != null) ranged += f.CountOfUnits; }
                if (melee < 1) melee = 1;      // jak gra: Max(sum, 1)
                if (ranged < 1) ranged = 1;
                sw = (bool)_fRangedNeeded.GetValue(tac);
                return true;
            }
            catch (Exception e) { Stumble("Prog", e); return false; }
        }

        private bool Siege(Mission m)
        {
            if (!_known)
            {
                try { _siege = m != null && m.IsSiegeBattle && !m.IsSallyOutBattle && !m.IsNavalBattle; } catch { _siege = false; }
                _known = true;
            }
            return _siege;
        }

        public override void OnMissionTick(float dt)
        {
            if (_ended) return;
            var m = Mission;
            if (m == null) return;
            try
            {
                float now = m.CurrentTime;
                if (!_started)
                {
                    if (!Siege(m)) { _ended = true; return; }
                    bool deployed = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && now >= NoDeployStart);
                    if (!deployed) return;
                    if (_startAt < 0f) { _startAt = now + StartDelay; return; }
                    if (now < _startAt) return;
                    _started = true;
                    _t0 = now;
                    _next = now;
                    Refl();
                    _keyOn = On;
                }
                if (m.MissionEnded || now < _next) return;
                _next = now + Every;
                Scan(m, now);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private Team Defenders(Mission m)
        {
            var t = m.DefenderTeam;
            if (t == null || !MissionTeams.Valid(t) || t.IsPlayerTeam) return null;   // gracz broni - jego szyki
            return t;
        }

        private TacticDefendCastle Tactic(Team t)
        {
            try { return _fTactic != null && t.TeamAI != null ? _fTactic.GetValue(t.TeamAI) as TacticDefendCastle : null; } catch { return null; }
        }

        private void Scan(Mission m, float now)
        {
            var t = Defenders(m);
            if (t == null) { if (!_formed && !_formGiveUp) { _formGiveUp = true; _formWhy = "obroncy to druzyna gracza albo brak druzyny"; } return; }
            var tac = Tactic(t);

            // pasy: stan gry i liczniki z pamieci (bez przeliczania)
            List<SiegeLane> lanes = null;
            SiegeQuerySystem q = null;
            try { lanes = TeamAISiegeComponent.SiegeLanes; q = TeamAISiegeComponent.QuerySystem; } catch { lanes = null; }
            var close = new int[LaneN];
            var conc = new SiegeLane[LaneN];
            bool anyConc = false, closeKnown = lanes != null && lanes.Count > 0;
            for (int k = 0; k < LaneN; k++) close[k] = -1;
            for (int i = 0; lanes != null && i < lanes.Count; i++)
            {
                try
                {
                    var l = lanes[i];
                    if (l == null) continue;
                    int k = (int)l.LaneSide;
                    if (k < 0 || k >= LaneN) continue;
                    close[k] = SiegeMissionLog370.QVal(q, SiegeMissionLog370.LanePrefix(l.LaneSide) + "CloseAttackerCount");
                    if (close[k] < 0) closeKnown = false;
                    if (l.LaneState == SiegeLane.LaneStateEnum.Conceited)
                    {
                        conc[k] = l;
                        anyConc = true;
                        if (_concFirst[k] < 0f) _concFirst[k] = now;
                    }
                }
                catch (Exception e) { Stumble("Pas", e); }
            }
            int inside = SiegeMissionLog370.QVal(q, "_insideAttackerCount");
            if (inside > _insideMax) _insideMax = inside;
            if (inside >= InsideN && _insideFirst < 0f) _insideFirst = now;
            bool proper = false;
            try { proper = tac != null && tac.CurrentTacticState == TacticDefendCastle.TacticState.ProperDefense; } catch { proper = false; }
            int gm, gr;
            bool sw;
            bool gauge = Gauge(tac, out gm, out gr, out sw);
            if (gauge) { _swKnown = true; if (sw && _swFirst < 0f) _swFirst = now; }

            if (!_formed)
            {
                if (_formGiveUp) return;
                if (now - _t0 > FormWindow) { _formGiveUp = true; _formWhy = "okno " + FormWindow.ToString("0", Inv) + " s minelo (ostatnio: " + _formWhy + ")"; return; }
                if (tac == null) { _formWhy = "brak TacticDefendCastle"; return; }
                if (!proper) { _formWhy = "stan taktyki nie ProperDefense"; return; }
                if (!closeKnown || inside < 0) { _formWhy = "liczniki pasow nieznane"; return; }
                if (anyConc || inside != 0) { _formWhy = "przyczolek / wrogowie w srodku"; return; }
                for (int k = 0; k < LaneN; k++) if (close[k] >= WallMin) { _formWhy = "atakujacy przy murze"; return; }
                if (!gauge) { _formWhy = "progu lukow gry nie da sie policzyc (listy taktyki nieczytelne)"; return; }
                if (sw) { _formGiveUp = true; _formWhy = "przelacznik lukow gry juz wlaczony (piechota " + gm + " < " + GameC.ToString("0.00", Inv) + " x " + (gm + gr) + ")"; return; }
                Form(m, t, now, gm, gr);
                return;
            }
            if (_out)
            {
                if (_keyOn) Arrive(now);
                return;
            }

            // trzymanie odwodu: polegli i AI szyku 8
            int alive = Alive();
            if (_keyOn && _f8 != null && alive > 0)
            {
                try
                {
                    if (_f8.IsAIControlled)
                    {
                        _f8.SetControlledByAI(false);
                        _f8.SetMovementOrder(MovementOrder.MovementOrderMove(_hold));
                        _reAi++;
                    }
                }
                catch (Exception e) { Stumble("AI8", e); }
            }

            // wyzwalacz: Conceited (najwiecej przy punkcie), wrogowie w srodku, wyjscie taktyki z ProperDefense
            SiegeLane lane = null;
            int best = -1;
            for (int k = 0; k < LaneN; k++) if (conc[k] != null && close[k] > best) { best = close[k]; lane = conc[k]; }
            if (lane == null && anyConc) for (int k = 0; k < LaneN; k++) if (conc[k] != null) { lane = conc[k]; break; }
            string why = null;
            if (lane != null) why = "przyczolek (Conceited) pas " + lane.LaneSide + " od t=" + _concFirst[(int)lane.LaneSide].ToString("0", Inv) + "s, przy punkcie " + close[(int)lane.LaneSide];
            else if (inside >= InsideN) why = "w srodku " + inside + " atakujacych (od t=" + _insideFirst.ToString("0", Inv) + "s)";
            else if (!proper) why = tac == null ? "taktyka obroncow zniknela" : "taktyka obroncow " + SafeState(tac);
            else if (gauge && !sw && alive > 0)
            {
                // 454-p: piechota gry blisko progu lukow - odwod wraca, zanim gra sciagnie strzelcow z murow (przy NIE ludzie "bylby" odwodu sa w szykach gry)
                int me = _keyOn ? gm : Math.Max(gm - alive, 1);
                int tot = _keyOn ? gm + gr : Math.Max(gm + gr - alive, 1);
                if (me < OutC * tot) why = "piechota gry blisko progu lukow (" + me + " < " + OutC.ToString("0.00", Inv) + " x " + tot + ")";
            }
            if (why == null) return;
            Release(m, t, now, lane, why, alive);
        }

        /// <summary>Zywi ludzie odwodu, ktorzy nie uciekaja (przy TAK - nadal w szyku 8).</summary>
        private int Alive()
        {
            int n = 0;
            for (int i = 0; i < _res.Count; i++)
            {
                var a = _res[i];
                bool ok = false;
                try { ok = a != null && a.IsActive() && !a.IsRunningAway && (!_keyOn || a.Formation == _f8); } catch { ok = false; }
                if (ok) n++;
            }
            return n;
        }

        private static string SafeState(TacticDefendCastle tac)
        {
            try { return tac.CurrentTacticState.ToString(); } catch { return "?"; }
        }

        /// <summary>Miejsce odwodu: pozycja oczekiwania bramy wewnetrznej, potem zewnetrznej; tylko na siatce i w granicach pola.</summary>
        private bool Place(Mission m, Team t, out WorldPosition pos, out string name)
        {
            pos = WorldPosition.Invalid;
            name = "-";
            var ai = t.TeamAI as TeamAISiegeComponent;
            if (ai == null) return false;
            CastleGate[] gates = { null, null };
            try { gates[0] = ai.InnerGate; } catch { gates[0] = null; }
            try { gates[1] = ai.OuterGate; } catch { gates[1] = null; }
            for (int g = 0; g < 2; g++)
            {
                var gate = gates[g];
                if (gate == null) continue;
                try
                {
                    WorldPosition w = WorldPosition.Invalid;
                    var tp = gate.WaitPosition;
                    if (tp != null) w = tp.Position;
                    if (!w.IsValid) w = gate.DefenseWaitFrame.Origin;
                    if (!w.IsValid || w.GetNavMesh() == UIntPtr.Zero || !m.IsPositionInsideBoundaries(w.AsVec2)) continue;
                    pos = w;
                    name = g == 0 ? "brama wewnetrzna" : "brama zewnetrzna";
                    return true;
                }
                catch (Exception e) { Stumble("Miejsce", e); }
            }
            return false;
        }

        private void Form(Mission m, Team t, float now, int gm, int gr)
        {
            // 454-p: odwod g tak, by piechota gry zostala >= CapC * (piechota + strzelcy): g <= M - CapC/(1-CapC) * R
            int cap = (int)Math.Floor(gm - CapC / (1f - CapC) * gr);
            _gauge = "piechota gry " + gm + ", strzelcy " + gr + ", odwod najwyzej " + Math.Max(cap, 0) + " (zostaje >= " + CapC.ToString("0.00", Inv) + " piechoty; prog gry " + GameC.ToString("0.00", Inv) + ")";
            if (cap < ReserveMin) { _formGiveUp = true; _formWhy = "blisko progu lukow gry (" + _gauge + ")"; return; }
            WorldPosition hold;
            string place;
            if (!Place(m, t, out hold, out place)) { _formGiveUp = true; _formWhy = "brak miejsca odwodu (brama bez pozycji oczekiwania na siatce)"; return; }
            Formation f8 = null;
            try { f8 = t.GetFormation(FormationClass.NumberOfRegularFormations); } catch (Exception e) { Stumble("Szyk8", e); }
            if (f8 == null) { _formGiveUp = true; _formWhy = "szyk 8 niedostepny"; return; }
            if (f8.CountOfUnits > 0 || f8.PlayerOwner != null) { _formGiveUp = true; _formWhy = "szyk 8 zajety (" + f8.CountOfUnits + " ludzi)"; return; }

            // szyki pasow i ich ludzie (kandydaci) - najblizsi miejsca odwodu
            var cand = new Dictionary<Formation, List<KeyValuePair<float, Agent>>>();
            var fl = t.FormationsIncludingEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                try
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits <= 0 || !f.IsAIControlled || f.AI == null || !(f.AI.ActiveBehavior is BehaviorDefendCastleKeyPosition)) continue;
                    cand[f] = new List<KeyValuePair<float, Agent>>();
                }
                catch (Exception e) { Stumble("Szyk", e); }
            }
            if (cand.Count == 0) { _formWhy = "brak szykow pasow (DefendCastleKeyPosition)"; return; }
            Vec2 hp = hold.AsVec2;
            var list = t.ActiveAgents;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                var a = list[i];
                try
                {
                    if (a == null || !a.IsActive() || !a.IsHuman || !a.IsAIControlled || a.IsHero || a.IsMainAgent || a.IsPlayerControlled || a.HasMount || a.Banner != null
                        || a.IsRangedCached || a.IsDetachedFromFormation || a.CurrentlyUsedGameObject != null || a.IsRunningAway) continue;
                    var f = a.Formation;
                    List<KeyValuePair<float, Agent>> l;
                    if (f == null || !cand.TryGetValue(f, out l)) continue;
                    l.Add(new KeyValuePair<float, Agent>(a.Position.AsVec2.Distance(hp), a));
                }
                catch (Exception e) { Stumble("Czlowiek", e); }
            }
            var plan = new List<Agent>();
            var take = new List<KeyValuePair<Formation, int>>();
            foreach (var kv in cand)
            {
                var f = kv.Key;
                int n = f.CountOfUnitsWithoutDetachedOnes;
                int give = Math.Min((int)(n * Pct), n - KeepMin);
                give = Math.Min(give, kv.Value.Count);
                give = Math.Min(give, cap - plan.Count);   // 454-p: zapas przed progiem lukow gry
                if (give <= 0) continue;
                kv.Value.Sort((x, y) => x.Key.CompareTo(y.Key));
                for (int i = 0; i < give; i++) plan.Add(kv.Value[i].Value);
                take.Add(new KeyValuePair<Formation, int>(f, give));
            }
            _src.Length = 0;
            int nn = 0;
            foreach (var kv in take)
            {
                string side = "?";
                try { side = kv.Key.AI.Side.ToString(); } catch { }
                _src.Append(nn++ > 0 ? ", " : "").Append("pas ").Append(side).Append(" F").Append((int)kv.Key.FormationIndex).Append(' ').Append(kv.Value)
                    .Append(" (zostaje ").Append(kv.Key.CountOfUnitsWithoutDetachedOnes - kv.Value).Append(')');
            }
            if (plan.Count < ReserveMin)
            {
                _formGiveUp = true;
                _formWhy = "za malo ludzi (" + plan.Count + " < " + ReserveMin + "; szyk pasa zostaje z >= " + KeepMin + ", najwyzej " + (Pct * 100f).ToString("0", Inv) + "%)";
                return;
            }

            if (_keyOn)
            {
                var touched = new List<Formation>();
                foreach (var kv in take) touched.Add(kv.Key);
                touched.Add(f8);
                var started = new List<Formation>();
                try { f8.SetControlledByAI(false); } catch (Exception e) { Stumble("AI8", e); }
                foreach (var tf in touched) { try { tf.OnMassUnitTransferStart(); started.Add(tf); } catch (Exception e) { Stumble("Start", e); } }
                try
                {
                    foreach (var a in plan)
                    {
                        try { a.Formation = f8; _res.Add(a); }
                        catch (Exception e) { Stumble("Przelozenie", e); }
                    }
                }
                finally
                {
                    foreach (var tf in touched)
                    {
                        try { t.TriggerOnFormationsChanged(tf); if (started.Contains(tf)) tf.OnMassUnitTransferEnd(); tf.QuerySystem.Expire(); }
                        catch (Exception e) { Stumble("Odswiezenie", e); }
                    }
                }
                try { t.QuerySystem.ExpireAfterUnitAddRemove(); } catch (Exception e) { Stumble("Druzyna", e); }
                try
                {
                    f8.SetMovementOrder(MovementOrder.MovementOrderMove(hold));
                    f8.SetFacingOrder(FacingOrder.FacingOrderLookAtEnemy);
                    f8.SetControlledByAI(false);
                }
                catch (Exception e) { Stumble("Rozkaz8", e); }
            }
            else _res.AddRange(plan);
            _formed = true;
            _formAt = now;
            _f8 = f8;
            _hold = hold;
            _place = place;
            _size = _res.Count;
            _formWhy = "utworzony";
            Log.Info(P + "t=" + now.ToString("0", Inv) + "s | T" + t.TeamIndex + " | klucz " + (_keyOn ? "TAK" : "NIE") + " | " + (_keyOn ? "odwod " : "bylby odwod ") + _size
                     + " ludzi w szyku 8 (" + place + ") z: " + _src + " | " + _gauge + " | wyjscie: pas Conceited albo >= " + InsideN + " atakujacych w srodku albo piechota gry < "
                     + OutC.ToString("0.00", Inv) + ".");
        }

        /// <summary>Szyk celu: Conceited - szyk pasa; wrogowie w srodku - Eliminate, potem Retake; zapas - najblizszy szyk 0-7 pod AI z ludzmi (nie strzelcy z murow).</summary>
        private Formation Target(Team t, SiegeLane lane, Vec2 from, out string how)
        {
            how = "-";
            if (lane != null)
            {
                try
                {
                    var x = lane.GetLastAssignedFormation(t.TeamIndex);
                    if (Usable(t, x)) { how = "szyk pasa " + lane.LaneSide; return x; }
                }
                catch (Exception e) { Stumble("CelPas", e); }
            }
            Formation elim = null, ret = null, near = null;
            float nd = float.MaxValue;
            Vec2 at = from;
            if (lane != null) { try { if (lane.DefenderOrigin.IsValid) at = lane.DefenderOrigin.AsVec2; } catch { } }
            var fl = t.FormationsIncludingEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                try
                {
                    var f = fl[j];
                    if (!Usable(t, f)) continue;
                    var ab = f.AI.ActiveBehavior;
                    if (ab is BehaviorEliminateEnemyInsideCastle && elim == null) elim = f;
                    if (ab is BehaviorRetakeCastleKeyPosition && ret == null) ret = f;
                    float d = f.CachedMedianPosition.AsVec2.Distance(at);
                    if (d < nd) { nd = d; near = f; }
                }
                catch (Exception e) { Stumble("Cel", e); }
            }
            if (lane == null && elim != null) { how = "szyk przeciw wrogom w srodku"; return elim; }
            if (ret != null) { how = "szyk odbijajacy pas"; return ret; }
            if (elim != null) { how = "szyk przeciw wrogom w srodku"; return elim; }
            if (near != null) { how = "najblizszy szyk"; return near; }
            return null;
        }

        private static bool Usable(Team t, Formation f)
        {
            if (f == null || f.CountOfUnits <= 0 || !ReferenceEquals(f.Team, t) || !f.IsAIControlled || f.AI == null) return false;
            if ((int)f.FormationIndex >= (int)FormationClass.NumberOfRegularFormations) return false;
            return !(f.AI.ActiveBehavior is BehaviorShootFromCastleWalls);
        }

        private void Release(Mission m, Team t, float now, SiegeLane lane, string why, int alive)
        {
            _out = true;
            _outAt = now;
            _outWhy = why;
            _lostBefore = _size - alive;
            Vec2 from = _hold.IsValid ? _hold.AsVec2 : Vec2.Zero;
            string how;
            var tgt = Target(t, lane, from, out how);
            _target = tgt;
            var live = new List<Agent>();
            for (int i = 0; i < _res.Count; i++)
            {
                var a = _res[i];
                try { if (a != null && a.IsActive() && !a.IsRunningAway && (!_keyOn || a.Formation == _f8)) live.Add(a); } catch (Exception e) { Stumble("Zywi", e); }
            }
            _outN = live.Count;
            if (tgt != null)
            {
                try { _outDist = tgt.CachedMedianPosition.AsVec2.Distance(from); } catch { _outDist = -1f; }
                _outTo = how + " F" + (int)tgt.FormationIndex + " (" + tgt.CountOfUnits + " ludzi)";
            }
            else _outTo = "brak szyku celu - szarza szyku 8";
            if (_keyOn && live.Count > 0)
            {
                if (tgt != null)
                {
                    var started = new List<Formation>();
                    var touched = new List<Formation> { tgt, _f8 };
                    foreach (var tf in touched) { try { tf.OnMassUnitTransferStart(); started.Add(tf); } catch (Exception e) { Stumble("Start", e); } }
                    try
                    {
                        foreach (var a in live)
                        {
                            try
                            {
                                if (a.IsUsingGameObject) a.StopUsingGameObject(false, Agent.StopUsingGameObjectFlags.None);
                                a.Formation = tgt;
                                _sent.Add(a);
                            }
                            catch (Exception e) { Stumble("Wyjscie", e); }
                        }
                    }
                    finally
                    {
                        foreach (var tf in touched)
                        {
                            try { t.TriggerOnFormationsChanged(tf); if (started.Contains(tf)) tf.OnMassUnitTransferEnd(); tf.QuerySystem.Expire(); }
                            catch (Exception e) { Stumble("Odswiezenie", e); }
                        }
                    }
                    try { t.QuerySystem.ExpireAfterUnitAddRemove(); } catch (Exception e) { Stumble("Druzyna", e); }
                }
                else
                {
                    // 456-c: szarza przez ChargeOrder() (odbicie w trwajacej misji), nie wprost MovementOrder.MovementOrderCharge - patrz ChargeOrder
                    try { object ch = ChargeOrder(); if (ch != null) _f8.SetMovementOrder((MovementOrder)ch); else Stumble("Szarza", new InvalidOperationException("brak MovementOrder.MovementOrderCharge")); } catch (Exception e) { Stumble("Szarza", e); }
                }
            }
            float react = Reaction(lane, now);
            Log.Info(P + "t=" + now.ToString("0", Inv) + "s | T" + t.TeamIndex + " | klucz " + (_keyOn ? "TAK" : "NIE") + " | " + (_keyOn ? "wyszlo " : "bylby wypuscil ") + _outN
                     + " z " + _size + " (polegli / uciekli w odwodzie " + _lostBefore + ") -> " + _outTo + " | powod: " + why + " | odleglosc " + (_outDist >= 0f ? _outDist.ToString("0", Inv) + " m" : "-")
                     + " | reakcja " + (react >= 0f ? react.ToString("0", Inv) + " s" : "-") + ".");
        }

        /// <summary>Czas od pierwszego Conceited pasa wyjscia (albo od pierwszych >= 15 w srodku) do wyjscia.</summary>
        private float Reaction(SiegeLane lane, float now)
        {
            float from = -1f;
            try { if (lane != null) from = _concFirst[(int)lane.LaneSide]; } catch { from = -1f; }
            if (from < 0f) from = _insideFirst;
            return from >= 0f ? now - from : -1f;
        }

        /// <summary>Dotarcie: polowa wypuszczonych zywych w promieniu 20 m od srodka szyku celu.</summary>
        private void Arrive(float now)
        {
            if (_arriveAt >= 0f || _target == null || _sent.Count == 0) return;
            try
            {
                if (_target.CountOfUnits <= 0) return;
                Vec2 c = _target.CachedMedianPosition.AsVec2;
                int live = 0, near = 0;
                foreach (var a in _sent)
                {
                    if (a == null || !a.IsActive()) continue;
                    live++;
                    if (a.Position.AsVec2.Distance(c) <= ArriveDist) near++;
                }
                if (live > 0 && near * 2 >= live) _arriveAt = now;
            }
            catch (Exception e) { Stumble("Dotarcie", e); }
        }

        protected override void OnEndMission()
        {
            try
            {
                var m = Mission;
                if (_started && !_ended && _siege)
                {
                    float now = m != null ? m.CurrentTime : 0f;
                    var sb = new StringBuilder(512);
                    sb.Append(P.Substring(0, P.Length - 2)).Append(" koniec: t=").Append(now.ToString("0", Inv)).Append("s | klucz ").Append(_keyOn ? "TAK" : "NIE")
                      .Append(" | odwod: ");
                    if (_formed)
                        sb.Append(_keyOn ? "utworzony " : "bylby utworzony ").Append(_size).Append(" ludzi przy t=").Append(_formAt.ToString("0", Inv)).Append("s (").Append(_place).Append(") z: ").Append(_src);
                    else sb.Append("nie utworzony - ").Append(_formWhy);
                    if (_formed)
                    {
                        sb.Append(" | wyjscie: ");
                        if (_out)
                        {
                            sb.Append(_keyOn ? "" : "bylby ").Append("t=").Append(_outAt.ToString("0", Inv)).Append("s, ").Append(_outN).Append(" ludzi -> ").Append(_outTo)
                              .Append(", powod: ").Append(_outWhy).Append(", polegli / uciekli przed wyjsciem ").Append(_lostBefore)
                              .Append(", odleglosc ").Append(_outDist >= 0f ? _outDist.ToString("0", Inv) + " m" : "-");
                            if (_keyOn) sb.Append(", dotarcie polowy do szyku celu ").Append(_arriveAt >= 0f ? "po " + (_arriveAt - _outAt).ToString("0", Inv) + " s" : "nie");
                        }
                        else sb.Append("nie bylo (stal do konca), zywych w odwodzie ").Append(Alive()).Append(" z ").Append(_size);
                    }
                    sb.Append(" | pierwszy Conceited:");
                    for (int k = 0; k < LaneN; k++) sb.Append(k > 0 ? "," : "").Append(' ').Append((FormationAI.BehaviorSide)k).Append(' ').Append(_concFirst[k] >= 0f ? "t=" + _concFirst[k].ToString("0", Inv) + "s" : "nigdy");
                    sb.Append(" | w srodku >= ").Append(InsideN).Append(": ").Append(_insideFirst >= 0f ? "t=" + _insideFirst.ToString("0", Inv) + "s" : "nigdy").Append(", maks ").Append(_insideMax)
                      .Append(" | przelacznik lukow gry (_areRangedNeededForLaneDefense): ").Append(!_swKnown ? "nieznany" : _swFirst >= 0f ? "wlaczony od t=" + _swFirst.ToString("0", Inv) + "s" : "nie")
                      .Append(" | AI szyku 8 wlaczone przez gre i cofniete ").Append(_reAi)
                      .Append(" | odwod = do ").Append((Pct * 100f).ToString("0", Inv)).Append("% szyku pasa (zostaje >= ").Append(KeepMin).Append("), piesi bez broni dystansowej, razem >= ").Append(ReserveMin)
                      .Append(", tworzony 0-").Append(FormWindow.ToString("0", Inv)).Append(" s przy < ").Append(WallMin).Append(" atak przy kazdym punkcie | potkniecia ").Append(_stumbles).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            _res.Clear();
            _sent.Clear();
            _target = null;
            _f8 = null;
            base.OnEndMission();
        }

        // 456-c (autotest at-456 12.11: TypeInitializationException MovementOrder przy wejsciu na arene, INNER NullReference w MovementOrder..ctor <- ..cctor):
        // pola statyczne MovementOrder (Charge / Stop / ...) tworzy konstruktor statyczny przez new Timer(Mission.Current.CurrentTime) - poza misja NullReference
        // i typ zepsuty do konca procesu. Typ jest beforefieldinit, wiec JIT moze odpalic ten konstruktor juz przy KOMPILACJI metody, ktora czyta pole wprost;
        // siec CrashScribe (Net) zaklada finalizator na kazda metode Armoury przy starcie gry i tym kompiluje ja poza misja (ta sama pulapka co 333t2).
        // Dlatego odczyt pola tylko odbiciem, tylko w trwajacej misji, raz na proces (potem z pamieci) - wzor HorsePursuit.ArmyOrders (333t2).
        private static object _charge;
        private static bool _chargeTried;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object ChargeOrder()
        {
            if (_charge != null) return _charge;
            var m = Mission.Current;
            if (_chargeTried || m == null || m.CurrentState != Mission.State.Continuing) return null;
            _chargeTried = true;
            try
            {
                var fc = typeof(MovementOrder).GetField("MovementOrderCharge", BindingFlags.Public | BindingFlags.Static);
                _charge = fc != null ? fc.GetValue(null) : null;
            }
            catch (Exception e) { _charge = null; try { Log.Error("SiegeReserve454.ChargeOrder", e); } catch { } }
            return _charge;
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("SiegeReserve454." + where, e); } catch { }
        }
    }
}
