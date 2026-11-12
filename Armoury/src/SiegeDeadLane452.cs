using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 452 (pakiet 7 krok 7.3 audytu AI bitwy; Jeff 11.11 "rob wszystko po kolei tak jak radzisz"). PAS-LEJEK: pas szturmu, na ktorym oddzial wyginal,
    /// a przyczolka nie ma, przestaje dostawac ludzi - taktyka atakujacych AI (TacticBreachWalls) przestaje go widziec wsrod pasow w uzyciu.
    /// Dowod (dekompilacja 1.4.8, rally_dec/mb_full.cs 37669-37712): prywatne DetermineCurrentLanes daje liste pasow, na ktore taktyka kieruje szyki piechoty
    /// (TickOccasionally, CheckAndChangeState, ManageFormationCounts, AllInAssault, CheckAndSetAvailableFormationsChanged). Pas, ktory z tej listy wypada, gra
    /// obsluguje sama, ta sama droga co zniszczona wieze: MergeFormationsIfLanesBecameUnavailable (37466-37523) przelewa ludzi szyku tego pasa (tylko
    /// IsSplittableByAI = nikogo w zamku, 69077-69122) do szykow pozostalych pasow; ManageFormationCounts zmniejsza liczbe szykow piechoty do liczby pasow.
    /// Postfiks DetermineCurrentLanes (moze biec poza glownym watkiem - czyta tylko gotowa maske pasow) wyrzuca z wyniku pasy oznaczone jako porzucone:
    ///  - nigdy pusta lista: gdy zostalby zero pasow - wynik gry bez zmian (licznik "bez skutku");
    ///  - lista z jednym pasem (szturm uderzeniowy do 100 ludzi, 37677-37702) - bez zmian.
    /// ShouldRetreat dostaje jednak liste PELNA (prefiks podmienia argument na oryginalny wynik gry przez ConditionalWeakTable): prog odwrotu taktyki
    /// (GetRetreatThresholdRatio liczy pasy zamkniete i otwarte, 37164-37173) nie rosnie przez porzucenie pasa - odwrot jak przed (i dalej pod 372).
    /// Decyzja (glowny watek, OnMissionTick co 1 s; druzyna atakujaca z TacticBreachWalls w stanie AssaultUnderRangedCover / TotalAttack; nie po
    /// zalamaniu 371; tylko glowna druzyna atakujaca - liczniki przy murze nie licza sojusznikow), na pas w uzyciu druzyny (GetLastAssignedFormation, szyk z AI.Side == strona pasa - jak 451/450-p):
    ///  - co najmniej 90 s od poczatku szturmu (rozstawienie + 3 s); polegli z szyku tego pasa (zabici + nieprzytomni, licznik z OnAgentRemoved, mapa
    ///    szyk -> pas z probki) >= 25 i >= 60% sumy polegli + zostali (CountOfUnitsWithoutDetachedOnes);
    ///  - brak przyczolka: przy punkcie pasa < 5 atakujacych (pamiec gry, bez przeliczania) i nikogo z szyku w zamku (IsSplittableByAI) - pasa z naszymi
    ///    w srodku albo na murze nie ruszamy; szyk pod AI (gracz-wodz nie); nie szyk glowny 451 pod ochrona;
    ///  - warunek trwa nieprzerwanie 15 s; zostaje co najmniej jeden inny pas w uzyciu druzyny, ktory sam nie spelnia warunku (nie porzucamy wszystkich);
    ///  - w stanie AssaultUnderRangedCover: pozostale pasy maja stanowisko strzelcow, jesli mialy je wszystkie (inaczej gra przeszlaby w TotalAttack, 37630-37640);
    ///  - najwyzej jeden pas na druzyne na takt; ZAPADKA - porzucony pas nie wraca do konca misji (poza bezpiecznikiem "nigdy pusta lista").
    /// 452-p (recenzja): procent poleglych liczony od calego szyku (CountOfUnits, z odlaczonymi) i pas nie jest porzucany, gdy ktos z szyku obsluguje machine
    /// (odlaczeni) albo machina pasa jeszcze jedzie (HoldLadders - wieza/taran nie dojechaly); kandydat i "inny pas" tylko sposrod pasow z ostatniej listy gry
    /// (maska wyniku DetermineCurrentLanes przed filtrem, zapisywana w postfiksie per taktyka); gotowosc drabin (SetAreLaddersReady z konca TickOccasionally)
    /// liczona tez na pelnej liscie gry (prefiks: false -> wzor gry na pelnej liscie) - przefiltrowana lista nie zatrzymuje drabin (372 ustawia true - bez zmian).
    /// Klucz SiegeAbandonDeadLane (TAK). NIE = maska nigdy nie publikowana, postfiks i prefiks wracaja od razu - co do bitu jak przed; ta sama decyzja liczona
    /// i wypisana jako "bylby". Linie: "Oblezenie misja (370) pas-lejek (452)" przy decyzji i "... (452) koniec" przy koncu misji (polegli / zostali przy
    /// porzuceniu, szyk pasa po 5 s - czy gra przelala ludzi, ile razy lista gry przefiltrowana, ile bez skutku). Wyjatek per druzyna / pas = potkniecie
    /// (pierwsze 3 do logu), bez globalnego wylacznika.
    /// </summary>
    internal sealed class SiegeDeadLane452 : MissionLogic
    {
        private const string P = "Oblezenie misja (370) pas-lejek (452): ";
        private const float Every = 1f, StartDelay = 3f, NoDeployStart = 10f, MinTime = 90f, HoldSec = 15f, AfterSec = 5f, DeadShare = 0.6f;
        private const int WallMin = 5, DeadMin = 25, ErrMax = 3, StAssault = 1, StTotal = 2, LaneN = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static FieldInfo _fTactic, _fState, _fAiSiege;
        private static MethodInfo _mLanes, _mRetreat, _mLadders;
        private static bool _reflTried;
        private static int _sErrs;

        /// <summary>Maska porzuconych pasow druzyny dla postfiksu (czytana takze poza glownym watkiem - tylko odczyt).</summary>
        private sealed class Drop
        {
            internal TacticBreachWalls Tac;
            internal volatile object Ai;                        // TeamAISiegeAttacker tej taktyki (prefiks SetAreLaddersReady)
            internal volatile List<SiegeLane> LastOrig;         // pelna lista gry z ostatniego przefiltrowanego wyniku (null = wynik nieprzefiltrowany)
            internal volatile int Mask;
            internal volatile bool On;
            internal int Filtered, Empty, Ladders;
        }
        /// <summary>Maska stron pasow z ostatniego wyniku gry (przed filtrem) per taktyka; -1 = jeszcze nie widziana.</summary>
        private sealed class Seen { internal volatile int Mask = -1; }
        private static readonly ConditionalWeakTable<TacticBreachWalls, Seen> _seen = new ConditionalWeakTable<TacticBreachWalls, Seen>();
        private static volatile Drop[] _drops = new Drop[0];
        // przefiltrowana lista -> oryginalny wynik gry (dla ShouldRetreat); slabe klucze - lista gry nie zostaje w pamieci
        private static readonly ConditionalWeakTable<List<SiegeLane>, List<SiegeLane>> _orig = new ConditionalWeakTable<List<SiegeLane>, List<SiegeLane>>();

        private sealed class LaneState
        {
            internal int Dead;                 // polegli z szyku tego pasa (pod _lock)
            internal float CondFrom = -1f;     // od kiedy warunek spelniony nieprzerwanie
            internal bool Dropped;             // TAK: porzucony; NIE: "bylby"
            internal float DropAt = -1f;
            internal int DeadAt, CurAt, CloseAt, FIdx = -1, After = -1;
            internal Formation F;
        }

        private sealed class TeamState
        {
            internal Team T;
            internal TacticBreachWalls Tac;
            internal readonly LaneState[] L = { new LaneState(), new LaneState(), new LaneState() };
            internal Drop D;
            internal string Why;
            internal int Skips;                // decyzje wstrzymane przez warunek pozostalych pasow / strzelcow / 451
        }

        private bool _known, _siege, _ended, _started;
        private float _startAt = -1f, _next, _t0;
        private int _stumbles, _errs;
        private bool _keyOn;
        private readonly List<TeamState> _teams = new List<TeamState>();
        // szyk -> (druzyna, pas) z ostatniej probki; OnAgentRemoved bywa z ticku asynchronicznego - pod lockiem
        private readonly object _lock = new object();
        private readonly Dictionary<Formation, KeyValuePair<TeamState, int>> _fMap = new Dictionary<Formation, KeyValuePair<TeamState, int>>();

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        internal static bool On { get { var s = Settings.Current; return s != null && s.SiegeAbandonDeadLane; } }

        // ------------------------------------------------------------ latki (przy starcie gry, przed JIT wolajacych - jak 372 / 451)

        internal static void ApplyAll(Harmony h)
        {
            Refl();
            bool okL = false, okR = false, okD = false;
            try
            {
                if (_mLanes != null)
                {
                    h.Patch(_mLanes, postfix: new HarmonyMethod(typeof(SiegeDeadLane452), nameof(LanesPostfix)));
                    okL = true;
                }
                if (okL && _mRetreat != null)
                {
                    h.Patch(_mRetreat, prefix: new HarmonyMethod(typeof(SiegeDeadLane452), nameof(RetreatPrefix)));
                    okR = true;
                }
                if (okL && _mLadders != null)
                {
                    h.Patch(_mLadders, prefix: new HarmonyMethod(typeof(SiegeDeadLane452), nameof(LaddersPrefix)));
                    okD = true;
                }
            }
            catch (Exception e) { Log.Error("SiegeDeadLane452.ApplyAll", e); }
            Log.Info("Szturm (452): latka TacticBreachWalls.DetermineCurrentLanes " + (okL ? "wpieta" : "NIE wpieta (" + (_mLanes == null ? "brak metody" : "blad") + ")")
                     + ", ShouldRetreat (pelna lista pasow) " + (okR ? "wpieta" : "NIE wpieta") + ", SetAreLaddersReady (pelna lista pasow) " + (okD ? "wpieta" : "NIE wpieta") + ", pola taktyka/stan/AI " + (_fTactic != null ? "+" : "-") + (_fState != null ? "+" : "-")
                     + (_fAiSiege != null ? "+" : "-") + " | klucz Siege Abandon Dead Lane " + (On ? "TAK" : "NIE") + ".");
        }

        private static void Refl()
        {
            if (_reflTried) return;
            _reflTried = true;
            try { _fTactic = AccessTools.Field(typeof(TeamAIComponent), "_currentTactic"); } catch { _fTactic = null; }
            try { _fState = AccessTools.Field(typeof(TacticBreachWalls), "_tacticState"); } catch { _fState = null; }
            try { _fAiSiege = AccessTools.Field(typeof(TacticBreachWalls), "_teamAISiegeAttacker"); } catch { _fAiSiege = null; }
            // obie zadeklarowane w samym TacticBreachWalls (DeclaredMethod - pulapka 15.10 nie dotyczy); Patch tym samym MethodInfo
            try { _mLanes = AccessTools.DeclaredMethod(typeof(TacticBreachWalls), "DetermineCurrentLanes", Type.EmptyTypes); } catch { _mLanes = null; }
            try { _mRetreat = AccessTools.DeclaredMethod(typeof(TacticBreachWalls), "ShouldRetreat", new[] { typeof(List<SiegeLane>), typeof(int) }); } catch { _mRetreat = null; }
            // zadeklarowana w TeamAISiegeComponent (dec 1.4.8, 46156) - DeclaredMethod na klasie deklarujacej
            try { _mLadders = AccessTools.DeclaredMethod(typeof(TeamAISiegeComponent), "SetAreLaddersReady", new[] { typeof(bool) }); } catch { _mLadders = null; }
        }

        /// <summary>Pasy porzucone wypadaja z listy pasow taktyki; nigdy pusta lista, lista jednego pasa bez zmian.</summary>
        private static void LanesPostfix(TacticBreachWalls __instance, ref List<SiegeLane> __result)
        {
            try
            {
                // maska pasow wyniku gry (przed filtrem) - dla decyzji w glownym watku (pasy, ktore gra naprawde uzywa); takze przy NIE (pomiar "bylby")
                if (__instance != null && __result != null)
                {
                    int sm = 0;
                    for (int i = 0; i < __result.Count; i++) { var l = __result[i]; if (l != null) { int k = (int)l.LaneSide; if (k >= 0 && k < LaneN) sm |= 1 << k; } }
                    _seen.GetOrCreateValue(__instance).Mask = sm;
                }
                var ds = _drops;
                if (ds.Length == 0) return;
                if (__result == null || __result.Count < 2) { ClearOrig(ds, __instance); return; }
                Drop d = null;
                for (int i = 0; i < ds.Length; i++) { var x = ds[i]; if (x != null && x.On && ReferenceEquals(x.Tac, __instance)) { d = x; break; } }
                if (d == null) return;
                d.LastOrig = null;
                int mask = d.Mask;
                if (mask == 0) return;
                var src = __result;
                int keep = 0;
                for (int i = 0; i < src.Count; i++) { var l = src[i]; if (l == null || (mask & (1 << (int)l.LaneSide)) == 0) keep++; }
                if (keep == src.Count) return;
                if (keep == 0) { Interlocked.Increment(ref d.Empty); return; }   // nigdy pusta lista - wynik gry
                var r = new List<SiegeLane>(keep);
                for (int i = 0; i < src.Count; i++) { var l = src[i]; if (l == null || (mask & (1 << (int)l.LaneSide)) == 0) r.Add(l); }
                _orig.Add(r, src);
                d.LastOrig = src;
                __result = r;
                Interlocked.Increment(ref d.Filtered);
            }
            catch (Exception e)
            {
                if (_sErrs < ErrMax) { _sErrs++; try { Log.Error("SiegeDeadLane452.LanesPostfix", e); } catch { } }
            }
        }

        private static void ClearOrig(Drop[] ds, TacticBreachWalls tac)
        {
            for (int i = 0; i < ds.Length; i++) { var x = ds[i]; if (x != null && ReferenceEquals(x.Tac, tac)) x.LastOrig = null; }
        }

        /// <summary>Gotowosc drabin: gdy gra (z przefiltrowanej listy) mowi "czekac", liczona jeszcze raz wzorem gry (mb_full 37776) na pelnej liscie -
        /// porzucony pas nie zatrzymuje drabin (pas z SendLadders albo wylom nie znika z rachunku). Wywolania z wartoscia true (372) bez zmian.</summary>
        private static void LaddersPrefix(TeamAISiegeComponent __instance, ref bool areLaddersReady)
        {
            try
            {
                if (areLaddersReady || __instance == null) return;
                var ds = _drops;
                if (ds.Length == 0) return;
                List<SiegeLane> full = null;
                Drop d = null;
                for (int i = 0; i < ds.Length; i++) { var x = ds[i]; if (x != null && x.On && ReferenceEquals(x.Ai, __instance)) { d = x; full = x.LastOrig; break; } }
                if (d == null || full == null) return;
                int breach = 0;
                bool hold = false, send = false;
                for (int i = 0; i < full.Count; i++)
                {
                    var l = full[i];
                    if (l == null) continue;
                    if (l.IsBreach) breach++;
                    var ws = l.PrimarySiegeWeapons;
                    for (int j = 0; ws != null && j < ws.Count; j++) { var w = ws[j]; if (w == null) continue; if (w.HoldLadders) hold = true; if (w.SendLadders) send = true; }
                }
                if (breach > 1 || !hold || send) { areLaddersReady = true; Interlocked.Increment(ref d.Ladders); }
            }
            catch (Exception e)
            {
                if (_sErrs < ErrMax) { _sErrs++; try { Log.Error("SiegeDeadLane452.LaddersPrefix", e); } catch { } }
            }
        }

        /// <summary>Odwrot taktyki liczony na pelnej liscie pasow gry (prog odwrotu jak przed porzuceniem pasa).</summary>
        private static void RetreatPrefix(ref List<SiegeLane> lanes)
        {
            try
            {
                List<SiegeLane> o;
                if (lanes != null && _orig.TryGetValue(lanes, out o) && o != null) lanes = o;
            }
            catch (Exception e)
            {
                if (_sErrs < ErrMax) { _sErrs++; try { Log.Error("SiegeDeadLane452.RetreatPrefix", e); } catch { } }
            }
        }

        // ------------------------------------------------------------ misja

        private bool Siege(Mission m)
        {
            if (!_known)
            {
                try { _siege = m != null && m.IsSiegeBattle; } catch { _siege = false; }
                _known = true;
            }
            return _siege;
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            if (!_started || _ended || !_siege) return;
            try
            {
                if (affectedAgent == null || !affectedAgent.IsHuman || (agentState != AgentState.Killed && agentState != AgentState.Unconscious)) return;
                var f = affectedAgent.Formation;
                if (f == null) return;
                lock (_lock)
                {
                    KeyValuePair<TeamState, int> v;
                    if (_fMap.TryGetValue(f, out v) && v.Key != null && v.Value >= 0 && v.Value < LaneN) v.Key.L[v.Value].Dead++;
                }
            }
            catch { _stumbles++; }
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
                    _drops = new Drop[0];   // maski tylko tej misji
                }
                if (m.MissionEnded || now < _next) return;
                _next = now + Every;
                Scan(m, now);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private TeamState Find(Team t)
        {
            for (int i = 0; i < _teams.Count; i++) if (ReferenceEquals(_teams[i].T, t)) return _teams[i];
            return null;
        }

        private static int State(TacticBreachWalls tac)
        {
            try { return _fState != null ? Convert.ToInt32(_fState.GetValue(tac), Inv) : -1; } catch { return -1; }
        }

        private void Scan(Mission m, float now)
        {
            List<SiegeLane> lanes = null;
            SiegeQuerySystem q = null;
            try { lanes = TeamAISiegeComponent.SiegeLanes; q = TeamAISiegeComponent.QuerySystem; } catch { lanes = null; }
            bool broken = false;
            try { var sb = m.GetMissionBehavior<SiegeBreak371>(); broken = sb != null && sb.IsBroken; } catch { broken = false; }
            var map = new Dictionary<Formation, KeyValuePair<TeamState, int>>();
            var teams = m.Teams;
            for (int ti = 0; teams != null && ti < teams.Count; ti++)
            {
                try
                {
                    var t = teams[ti];
                    if (!MissionTeams.Valid(t) || t.Side != BattleSideEnum.Attacker) continue;
                    var tac = _fTactic != null && t.TeamAI != null ? _fTactic.GetValue(t.TeamAI) as TacticBreachWalls : null;
                    if (tac == null) continue;
                    var st = Find(t);
                    if (st == null) { st = new TeamState { T = t, Tac = tac }; _teams.Add(st); }
                    else if (!ReferenceEquals(st.Tac, tac))
                    {
                        st.Tac = tac;
                        if (st.D != null) { st.D.Tac = tac; st.D.LastOrig = null; try { st.D.Ai = _fAiSiege != null ? _fAiSiege.GetValue(tac) : null; } catch { st.D.Ai = null; } }
                    }
                    var f = new Formation[LaneN];
                    var sl = new SiegeLane[LaneN];
                    for (int i = 0; lanes != null && i < lanes.Count; i++)
                    {
                        var l = lanes[i];
                        if (l == null) continue;
                        int k = (int)l.LaneSide;
                        if (k < 0 || k >= LaneN) continue;
                        Formation x = null;
                        try { x = l.GetLastAssignedFormation(t.TeamIndex); } catch { x = null; }
                        sl[k] = l;
                        if (x == null || x.CountOfUnits <= 0 || !ReferenceEquals(x.Team, t) || x.AI == null || x.AI.Side != l.LaneSide) continue;
                        f[k] = x;
                        if (!map.ContainsKey(x)) map[x] = new KeyValuePair<TeamState, int>(st, k);
                    }
                    // szyk pasa po porzuceniu (czy gra przelala ludzi) - raz, AfterSec s po decyzji
                    for (int k = 0; k < LaneN; k++)
                    {
                        var ls = st.L[k];
                        if (ls.DropAt >= 0f && ls.After < 0 && now - ls.DropAt >= AfterSec)
                        { try { ls.After = ls.F != null ? Math.Max(0, ls.F.CountOfUnits) : 0; } catch { ls.After = -2; } }
                    }
                    if (broken) { st.Why = "szturm zalamany (371)"; continue; }
                    // liczniki zapytan oblezenia (przy murze) licza tylko glowna druzyne atakujaca - druzyny sojusznicze bez decyzji
                    if (!ReferenceEquals(t, m.AttackerTeam)) { st.Why = "druzyna sojusznicza (liczniki gry jej nie licza)"; continue; }
                    Decide(now, st, tac, f, sl, q);
                }
                catch (Exception e) { Stumble("Druzyna", e); }
            }
            lock (_lock)
            {
                _fMap.Clear();
                foreach (var kv in map) _fMap[kv.Key] = kv.Value;
            }
        }

        private bool Cond(TeamState st, int k, Formation f, SiegeLane l, SiegeQuerySystem q, out int dead, out int cur, out int close, out string why)
        {
            dead = 0; cur = 0; close = -1; why = null;
            if (f == null) { why = "pas bez szyku"; return false; }
            lock (_lock) dead = st.L[k].Dead;
            // caly szyk (polegli licza takze odlaczonych do machin - mianownik tak samo)
            cur = Math.Max(0, f.CountOfUnits);
            if (dead < DeadMin) { why = "polegli " + dead + " < " + DeadMin; return false; }
            if (dead < DeadShare * (dead + cur)) { why = "polegli " + dead + ", zostalo " + cur; return false; }
            // ktos z szyku przy machinie (pcha wieze, bije taranem, niesie drabine) - pas jeszcze pracuje
            if (f.CountOfUnits > f.CountOfUnitsWithoutDetachedOnes) { why = "ludzie szyku przy machinie"; return false; }
            // machina pasa jeszcze jedzie (wieza / taran nie dojechaly - HoldLadders); porzucenie nie zdejmuje wiec HoldLadders z rachunku drabin
            var ws = l != null ? l.PrimarySiegeWeapons : null;
            for (int j = 0; ws != null && j < ws.Count; j++) { var w = ws[j]; if (w != null && w.HoldLadders) { why = "machina pasa w drodze"; return false; } }
            close = SiegeMissionLog370.QVal(q, SiegeMissionLog370.LanePrefix((FormationAI.BehaviorSide)k) + "CloseAttackerCount");
            if (close < 0 || close >= WallMin) { why = "przy murze " + close; return false; }
            if (!f.IsAIControlled || !f.IsSplittableByAI) { why = "szyk nie pod AI albo ktos w zamku"; return false; }
            return true;
        }

        private void Decide(float now, TeamState st, TacticBreachWalls tac, Formation[] f, SiegeLane[] sl, SiegeQuerySystem q)
        {
            int s = State(tac);
            if (s != StAssault && s != StTotal) { st.Why = "stan taktyki " + s; for (int k = 0; k < LaneN; k++) st.L[k].CondFrom = -1f; return; }
            if (now - _t0 < MinTime) { st.Why = "przed " + MinTime.ToString("0", Inv) + " s szturmu"; return; }
            // tylko pasy z ostatniej listy gry (przed filtrem); bez niej - brak decyzji
            Seen sn;
            int gm = _seen.TryGetValue(tac, out sn) && sn != null ? sn.Mask : -1;
            if (gm < 0) { st.Why = "brak listy pasow gry"; for (int k = 0; k < LaneN; k++) st.L[k].CondFrom = -1f; return; }
            var g = new Formation[LaneN];
            for (int k = 0; k < LaneN; k++) g[k] = (gm & (1 << k)) != 0 ? f[k] : null;
            f = g;
            var ok = new bool[LaneN];
            var dead = new int[LaneN];
            var cur = new int[LaneN];
            var close = new int[LaneN];
            int used = 0;
            for (int k = 0; k < LaneN; k++)
            {
                var ls = st.L[k];
                if (ls.Dropped || f[k] == null) { ls.CondFrom = -1f; continue; }
                used++;
                string why;
                try { ok[k] = Cond(st, k, f[k], sl[k], q, out dead[k], out cur[k], out close[k], out why); }
                catch (Exception e) { Stumble("Pas", e); ok[k] = false; }
                if (!ok[k]) { ls.CondFrom = -1f; continue; }
                if (ls.CondFrom < 0f) ls.CondFrom = now;
            }
            if (used < 2) { st.Why = "pasow w uzyciu " + used; return; }
            for (int k = 0; k < LaneN; k++)
            {
                var ls = st.L[k];
                if (!ok[k] || now - ls.CondFrom < HoldSec) continue;
                // zostaje inny pas w uzyciu, ktory sam nie jest lejkiem
                bool other = false;
                for (int j = 0; j < LaneN; j++) if (j != k && !st.L[j].Dropped && f[j] != null && !ok[j]) { other = true; break; }
                string stop = null;
                if (!other) stop = "kazdy inny pas tez wykrwawiony";
                else if (SiegeMainEffort451.IsGuardedMain(f[k])) stop = "szyk glowny 451 pod ochrona";
                else if (s == StAssault && !ArchersStay(tac, st, k, f)) stop = "pozostale pasy bez stanowiska strzelcow (gra przeszlaby w TotalAttack)";
                if (stop != null) { if (st.Why != stop) st.Skips++; st.Why = stop; continue; }
                Abandon(now, st, tac, k, f[k], sl[k], dead[k], cur[k], close[k]);
                break;   // najwyzej jeden pas na druzyne na takt
            }
        }

        /// <summary>W AssaultUnderRangedCover: jesli pasy w uzyciu maja stanowiska strzelcow, pozostale po porzuceniu tez musza je miec.</summary>
        private static bool ArchersStay(TacticBreachWalls tac, TeamState st, int drop, Formation[] f)
        {
            var ai = _fAiSiege != null ? _fAiSiege.GetValue(tac) as TeamAISiegeAttacker : null;
            var aps = ai != null ? ai.ArcherPositions : null;
            if (aps == null || aps.Count == 0) return true;
            int before = 0, after = 0;
            for (int i = 0; i < aps.Count; i++)
            {
                var ap = aps[i];
                if (ap == null) continue;
                bool b = false, a = false;
                for (int k = 0; k < LaneN; k++)
                {
                    if (st.L[k].Dropped || f[k] == null) continue;
                    if (!ap.IsArcherPositionRelatedToSide((FormationAI.BehaviorSide)k)) continue;
                    b = true;
                    if (k != drop) a = true;
                }
                if (b) before++;
                if (a) after++;
            }
            return before == 0 || after > 0;
        }

        private void Abandon(float now, TeamState st, TacticBreachWalls tac, int k, Formation f, SiegeLane l, int dead, int cur, int close)
        {
            var ls = st.L[k];
            ls.Dropped = true;
            ls.DropAt = now;
            ls.DeadAt = dead; ls.CurAt = cur; ls.CloseAt = close; ls.F = f;
            try { ls.FIdx = (int)f.FormationIndex; } catch { ls.FIdx = -1; }
            if (_keyOn)
            {
                if (st.D == null)
                {
                    object ai = null;
                    try { ai = _fAiSiege != null ? _fAiSiege.GetValue(tac) : null; } catch { ai = null; }
                    st.D = new Drop { Tac = tac, Ai = ai, On = true };
                    var ds = _drops;
                    var nd = new Drop[ds.Length + 1];
                    Array.Copy(ds, nd, ds.Length);
                    nd[ds.Length] = st.D;
                    _drops = nd;
                }
                st.D.Mask = st.D.Mask | (1 << k);
            }
            string kind = "?";
            try { kind = l == null ? "?" : (l.IsBreach ? "wylom" : (l.HasGate ? "brama" : (l.IsOpen ? "otwarty" : "machiny/drabiny"))); } catch { kind = "?"; }
            var sb = new StringBuilder(256);
            sb.Append("t=").Append(now.ToString("0", Inv)).Append("s | T").Append(st.T.TeamIndex).Append(" | klucz ").Append(_keyOn ? "TAK" : "NIE")
              .Append(" | pas ").Append((FormationAI.BehaviorSide)k).Append(" (").Append(kind).Append(") szyk F").Append(ls.FIdx)
              .Append(": polegli ").Append(dead).Append(", zostalo ").Append(cur).Append(" (").Append(dead + cur > 0 ? (100.0 * dead / (dead + cur)).ToString("0", Inv) : "-").Append("% wybitych)")
              .Append(", przy murze ").Append(close).Append(", warunek ").Append((now - ls.CondFrom).ToString("0", Inv)).Append(" s")
              .Append(_keyOn ? " | PORZUCONY - taktyka przestaje kierowac ludzi na ten pas (gra przelewa szyk do pozostalych pasow)" : " | bylby porzucony");
            Log.Info(P + sb.Append('.'));
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
                    sb.Append("Oblezenie misja (370) pas-lejek (452) koniec: t=").Append(now.ToString("0", Inv)).Append("s | klucz ").Append(_keyOn ? "TAK" : "NIE");
                    if (_teams.Count == 0) sb.Append(" | brak druzyny z TacticBreachWalls");
                    for (int i = 0; i < _teams.Count; i++)
                    {
                        var st = _teams[i];
                        try
                        {
                            sb.Append(" | T").Append(st.T != null ? st.T.TeamIndex : -1).Append(':');
                            int n = 0;
                            for (int k = 0; k < LaneN; k++)
                            {
                                var ls = st.L[k];
                                int d;
                                lock (_lock) d = ls.Dead;
                                sb.Append(k > 0 ? "," : "").Append(' ').Append((FormationAI.BehaviorSide)k).Append(" polegli ").Append(d);
                                if (!ls.Dropped) continue;
                                n++;
                                sb.Append(_keyOn ? " PORZUCONY" : " bylby porzucony").Append(" t=").Append(ls.DropAt.ToString("0", Inv)).Append("s (polegli ").Append(ls.DeadAt)
                                  .Append(", zostalo ").Append(ls.CurAt).Append(", szyk F").Append(ls.FIdx).Append(" po ").Append(AfterSec.ToString("0", Inv)).Append(" s ")
                                  .Append(ls.After >= 0 ? ls.After.ToString(Inv) : "-").Append(')');
                            }
                            sb.Append("; pasow ").Append(_keyOn ? "porzuconych " : "bylby porzuconych ").Append(n);
                            if (st.D != null) sb.Append(", lista pasow gry przefiltrowana ").Append(Volatile.Read(ref st.D.Filtered)).Append(" razy, bez skutku (bylaby pusta) ").Append(Volatile.Read(ref st.D.Empty))
                                                .Append(", drabiny zwolnione wg pelnej listy ").Append(Volatile.Read(ref st.D.Ladders));
                            sb.Append(", wstrzymane ").Append(st.Skips).Append(" (ostatnio: ").Append(st.Why ?? "-").Append(')');
                        }
                        catch (Exception e) { Stumble("Koniec.Druzyna", e); sb.Append(" ?"); }
                    }
                    sb.Append(" | lejek = >= ").Append(DeadMin).Append(" poleglych i >= ").Append((DeadShare * 100f).ToString("0", Inv)).Append("% szyku pasa, < ").Append(WallMin)
                      .Append(" przy murze, nikt w zamku ani przy machinie, machina pasa nie w drodze, ").Append(HoldSec.ToString("0", Inv)).Append(" s, od ").Append(MinTime.ToString("0", Inv)).Append(" s szturmu | potkniecia ").Append(_stumbles).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            // maski tej misji zdjete (postfiks wraca od razu)
            try { var ds = _drops; for (int i = 0; i < ds.Length; i++) if (ds[i] != null) ds[i].On = false; } catch { }
            _drops = new Drop[0];
            _teams.Clear();
            lock (_lock) _fMap.Clear();
            base.OnEndMission();
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("SiegeDeadLane452." + where, e); } catch { }
        }
    }
}
