using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 455 (pakiet 7 krok 7.6 audytu AI bitwy; Jeff 11.11 "rob wszystko po kolei tak jak radzisz"). OGIEN W SZTURMIE - dwie reguly, kazda z wlasnym kluczem.
    /// (A) OGIEN OBRONY ZA NACISKIEM (klucz DefenderFireFollowsAssault). Dowod (dekompilacja 1.4.8, rally_dec/mb_full.cs 38860-38893, 39689-39696):
    ///  TacticDefendCastle.CheckAndChangeState, gdy w tym tiku nie bylo innych przelozen, wola ArcherShiftAround(_rangedFormations): szyki strzelcow z aktywnym
    ///  BehaviorShootFromCastleWalls dostaja ludzi wedlug SAMEGO znacznika stanowiska ("many" 0.4, zwykle 0.2, "few" 0.1, znormalizowane; cel szyku
    ///  Max((int)(razem x udzial), 1)); przelozenie (TransferUnits z szyku o najwiekszej nadwyzce) tylko przy niedoborze > 10% wszystkich - gra nie patrzy,
    ///  gdzie idzie szturm. Prefiks (moze biec poza glownym watkiem - tylko odczyt gotowych mnoznikow): ta sama procedura gry co do kroku, ale udzial
    ///  stanowiska = znacznik x mnoznik pasa (1.0-2.0); mnoznik = 1 + nacisk pasa / najwiekszy nacisk, w krokach 0.25, pas = najsilniejszy ze stron
    ///  polaczonych ze stanowiskiem (ArcherPosition.ConnectedSides). Nacisk (glowny watek, co 1 s, srednia wykladnicza 0.2/s): atakujacy w regionie pasa
    ///  + 2 x atakujacy przy punkcie pasa (pamiec zapytan gry, bez przeliczania; licza glowna druzyne atakujaca). Bez zmiany (wynik gry): nacisk razem
    ///  &lt; 20, wszystkie mnozniki rowne, lista &lt; 2, szyk nie pod AI, druzyna obrony gracza (gracz broni - jego szyki), sojusznicy obrony, brak stanowiska.
    ///  Stanowisko bez nacisku traci najwyzej polowe udzialu gry (mnoznik 1 wobec najwyzej 2); prog 10% gry zostaje (bez dreptania po murach).
    /// (B) STRZELCY ATAKU COFAJA SIE (klucz SiegeArchersFallBack). Dowod (1.4.8, 24095-24170, 37339-37366): w stanie AssaultUnderRangedCover taktyka
    ///  TacticBreachWalls stawia szyki strzelcow na stanowiskach przed murem (BehaviorSparseSkirmish, TickOccasionally co tik: Move na stanowisko) i nic
    ///  ich stamtad nie zabiera - log 09.11: strzelcy ataku 103 -> ok. 50 w 4 min w otwartym polu. RBM i Vanilla AI Sieges nie lataja tego zachowania.
    ///  Decyzja (glowny watek, co 1 s; druzyna atakujaca z TacticBreachWalls w stanie AssaultUnderRangedCover, nie po zalamaniu 371, nie druzyna gracza-wodza;
    ///  szyk pod AI z aktywnym BehaviorSparseSkirmish): w oknie 30 s szyk stracil od pociskow wroga (zabici + nieprzytomni) >= 4 ludzi i co najmniej
    ///  2 x tyle, ilu jego ludzie zabili pociskami - szyk cofa sie 30 m (albo 20 / 12 m) od najblizszego poczatku pasa obrony, na siatke w polu bitwy
    ///  (szyk, ktory juz stoi tak daleko - bez zmiany). Cofniecie trwa 45 s, potem szyk wraca na stanowisko (rozkaz gry), okno strat od nowa, 20 s przerwy.
    ///  Postfiks BehaviorSparseSkirmish.TickOccasionally (watek silnika albo glowny - tylko odczyt tablicy i rozkaz Move, ta sama droga co gra w tym
    ///  wywolaniu) podmienia rozkaz ruchu szyku na punkt cofniecia. Wczesniejszy koniec: zachowanie inne niz SparseSkirmish, inny stan taktyki, 371, szyk pusty.
    /// NIE (kazdy klucz osobno): prefiks / postfiks wracaja od razu bez zmian - co do bitu jak przed; ta sama decyzja liczona i wypisana jako "bylby".
    /// Linie: "Oblezenie misja (370) ogien (455)" przy zmianie mnoznikow obrony i przy cofnieciu (pierwsze 10 na misje) i "... (455) koniec" przy koncu misji.
    /// Wyjatek per szyk / wywolanie = potkniecie (pierwsze 3 do logu), bez globalnego wylacznika.
    /// </summary>
    internal sealed class SiegeFire455 : MissionLogic
    {
        private const string P = "Oblezenie misja (370) ogien (455): ";
        private const float Every = 1f, StartDelay = 3f, NoDeployStart = 10f;
        // (A)
        private const float Ema = 0.2f, PressMin = 20f, Step = 0.25f;
        private const float WMany = 0.4f, WMid = 0.2f, WFew = 0.1f;
        // (B)
        private const float Window = 30f, HoldSec = 45f, CoolSec = 20f, NoRoomCool = 10f, ArriveDist = 10f;
        private const int LossMin = 4, Ratio = 2, LaneN = 3, ErrMax = 3, LineMax = 10, StAssault = 1;
        private static readonly float[] BackDist = { 30f, 20f, 12f };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static FieldInfo _fTactic, _fState;
        private static MethodInfo _mShift, _mSparse;
        private static bool _reflTried;
        private static int _sErrs;

        // ------------------------------------------------------------ (A) stan dla prefiksu (podmiana referencji, obiekt niezmienny po publikacji)
        private sealed class FireW
        {
            internal TacticDefendCastle Tac;
            internal Team Team;
            internal float[] F;     // mnoznik strony 0-2
            internal bool Real;     // TAK - nasz rozdzial; NIE - tylko licznik "bylby"
        }
        private static volatile FireW _w;
        private static int _cCalls, _cOur, _cDry, _cSame, _cSmall, _cForeign, _cMoved, _cTr, _cStum;

        // ------------------------------------------------------------ (B) cofniecia dla postfiksu
        private sealed class BackW
        {
            internal Formation F;
            internal MovementOrder Order;
        }
        private static volatile BackW[] _backs = new BackW[0];
        private static int _bMain, _bOther, _bStum, _mainThread = -1;

        private sealed class Ex
        {
            internal Formation F;
            internal Team T;
            internal readonly List<float> Lost = new List<float>();   // pod _lock
            internal readonly List<float> Kill = new List<float>();   // pod _lock
            internal bool Back, Arrived;
            internal float BackAt = -1f, CoolUntil = -1f;
            internal Vec2 Pt;
            internal int LostAt, KillAt, LostIn, KillIn;
        }

        private bool _known, _siege, _started, _ended, _keyFire, _keyBack;
        private float _startAt = -1f, _next, _t0;
        private int _stumbles, _errs, _lines;

        // (A) pomiar
        private readonly float[] _pe = { -1f, -1f, -1f };
        private float[] _lastF = { 1f, 1f, 1f };
        private string _fireWhy = "-";
        private bool _fireTeam;
        private int _mN, _fChanges;
        private double _mAct, _mGame, _mOur, _mPress;

        // (B) pomiar
        private readonly object _lock = new object();
        private readonly Dictionary<Formation, Ex> _ex = new Dictionary<Formation, Ex>();
        private readonly List<Ex> _exList = new List<Ex>();
        private int _eps, _noRoom, _already, _arrived, _endEarly;
        private int _sLostAt, _sKillAt, _sLostIn, _sKillIn;
        private float _sSecIn;
        private readonly HashSet<Formation> _epF = new HashSet<Formation>();

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        internal static bool FireOn { get { var s = Settings.Current; return s != null && s.DefenderFireFollowsAssault; } }
        internal static bool BackOn { get { var s = Settings.Current; return s != null && s.SiegeArchersFallBack; } }

        // ------------------------------------------------------------ latki (przy starcie gry, przed JIT wolajacych - jak 372 / 452)

        internal static void ApplyAll(Harmony h)
        {
            Refl();
            bool okS = false, okB = false;
            try
            {
                if (_mShift != null)
                {
                    h.Patch(_mShift, prefix: new HarmonyMethod(typeof(SiegeFire455), nameof(ShiftPrefix)));
                    okS = true;
                }
            }
            catch (Exception e) { Log.Error("SiegeFire455.ApplyAll ArcherShiftAround", e); }
            try
            {
                if (_mSparse != null)
                {
                    h.Patch(_mSparse, postfix: new HarmonyMethod(typeof(SiegeFire455), nameof(SparsePostfix)));
                    okB = true;
                }
            }
            catch (Exception e) { Log.Error("SiegeFire455.ApplyAll SparseSkirmish", e); }
            Log.Info("Szturm (455): latka TacticDefendCastle.ArcherShiftAround " + (okS ? "wpieta" : "NIE wpieta (" + (_mShift == null ? "brak metody" : "blad") + ")")
                     + ", BehaviorSparseSkirmish.TickOccasionally " + (okB ? "wpieta" : "NIE wpieta (" + (_mSparse == null ? "brak metody" : "blad") + ")")
                     + ", pola taktyka/stan " + (_fTactic != null ? "+" : "-") + (_fState != null ? "+" : "-")
                     + " | klucze Defender Fire Follows Assault " + (FireOn ? "TAK" : "NIE") + ", Siege Archers Fall Back " + (BackOn ? "TAK" : "NIE") + ".");
        }

        private static void Refl()
        {
            if (_reflTried) return;
            _reflTried = true;
            try { _fTactic = AccessTools.Field(typeof(TeamAIComponent), "_currentTactic"); } catch { _fTactic = null; }
            try { _fState = AccessTools.Field(typeof(TacticBreachWalls), "_tacticState"); } catch { _fState = null; }
            // obie zadeklarowane w samych klasach (DeclaredMethod - pulapka 15.10 nie dotyczy); Patch tym samym MethodInfo
            try { _mShift = AccessTools.DeclaredMethod(typeof(TacticDefendCastle), "ArcherShiftAround", new[] { typeof(List<Formation>) }); } catch { _mShift = null; }
            try { _mSparse = AccessTools.DeclaredMethod(typeof(BehaviorSparseSkirmish), "TickOccasionally", Type.EmptyTypes); } catch { _mSparse = null; }
        }

        private static float TagW(GameEntity e)
        {
            return e.HasTag("many") ? WMany : (e.HasTag("few") ? WFew : WMid);
        }

        private static float SideF(float[] f, int mask)
        {
            float r = 0f;
            for (int k = 0; k < LaneN; k++) if ((mask & (1 << k)) != 0 && f[k] > r) r = f[k];
            return r > 0f ? r : 1f;
        }

        private static ArcherPosition FindAp(List<ArcherPosition> aps, GameEntity e)
        {
            for (int i = 0; i < aps.Count; i++) { var ap = aps[i]; if (ap != null && ap.Entity == e) return ap; }
            return null;
        }

        /// <summary>(A) Rozdzial strzelcow obrony: procedura gry (ArcherShiftAround) z udzialem stanowiska x mnoznik nacisku pasa. false = nasz rozdzial zamiast gry.</summary>
        private static bool ShiftPrefix(TacticDefendCastle __instance, List<Formation> __0)
        {
            var w = _w;
            if (w == null || __instance == null || __0 == null || !ReferenceEquals(w.Tac, __instance)) return true;
            try
            {
                Interlocked.Increment(ref _cCalls);
                var d = w.Team != null ? w.Team.TeamAI as TeamAISiegeDefender : null;
                var aps = d != null ? d.ArcherPositions : null;
                if (aps == null) { Interlocked.Increment(ref _cForeign); return true; }
                var list = new List<Formation>(__0.Count);
                var bw = new List<float>(__0.Count);
                var fac = new List<float>(__0.Count);
                for (int i = 0; i < __0.Count; i++)
                {
                    var f = __0[i];
                    var b = f != null && f.AI != null ? f.AI.ActiveBehavior as BehaviorShootFromCastleWalls : null;
                    if (b == null) continue;
                    var e = b.ArcherPosition;
                    if (e == null) { Interlocked.Increment(ref _cForeign); return true; }   // stanowisko nieznane - gra
                    list.Add(f);
                    bw.Add(TagW(e));
                    var ap = FindAp(aps, e);
                    fac.Add(ap != null ? SideF(w.F, ap.ConnectedSides) : 1f);
                }
                if (list.Count < 2) { Interlocked.Increment(ref _cSmall); return true; }
                for (int i = 0; i < list.Count; i++) if (!list[i].IsAIControlled) { Interlocked.Increment(ref _cForeign); return true; }
                bool same = true;
                for (int i = 1; i < fac.Count; i++) if (Math.Abs(fac[i] - fac[0]) > 0.001f) { same = false; break; }
                if (same) { Interlocked.Increment(ref _cSame); return true; }
                if (!w.Real) { Interlocked.Increment(ref _cDry); return true; }

                // jak gra: udzialy znormalizowane, cel Max((int)(razem x udzial), 1), tolerancja Max((int)(razem x 0.1), 1)
                int n = list.Count;
                float sum = 0f;
                for (int i = 0; i < n; i++) sum += bw[i] * fac[i];
                if (sum <= 0f) return true;
                int total = 0;
                for (int i = 0; i < n; i++) total += list[i].CountOfUnitsWithoutDetachedOnes;
                var tgt = new int[n];
                for (int i = 0; i < n; i++) tgt[i] = Math.Max((int)(total * (bw[i] * fac[i] / sum)), 1);
                int tol = Math.Max((int)(total * 0.1f), 1);
                Interlocked.Increment(ref _cOur);
                for (int i = 0; i < n; i++)
                {
                    var item = list[i];
                    int guard = 0;
                    while (guard < n && tgt[i] - item.CountOfUnitsWithoutDetachedOnes > tol)
                    {
                        int donor = -1, best = int.MinValue;
                        for (int j = 0; j < n; j++)
                        {
                            int over = list[j].CountOfUnitsWithoutDetachedOnes - tgt[j];
                            if (over > best) { best = over; donor = j; }
                        }
                        if (donor < 0 || best <= 0) break;   // jak gra: zaden szyk nie ma nadwyzki
                        int a = Math.Min(tgt[i] - item.CountOfUnitsWithoutDetachedOnes, best);
                        list[donor].TransferUnits(item, a);
                        Interlocked.Add(ref _cMoved, a);
                        Interlocked.Increment(ref _cTr);
                        guard++;
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                Interlocked.Increment(ref _cStum);
                if (_sErrs < ErrMax) { _sErrs++; try { Log.Error("SiegeFire455.ShiftPrefix", e); } catch { } }
                return true;
            }
        }

        /// <summary>(B) Strzelcy ataku w cofnieciu: rozkaz ruchu na punkt cofniecia zamiast stanowiska (tylko odczyt tablicy z glownego watku).</summary>
        private static void SparsePostfix(BehaviorSparseSkirmish __instance)
        {
            var bs = _backs;
            if (bs.Length == 0 || __instance == null) return;
            try
            {
                var f = __instance.Formation;
                if (f == null) return;
                for (int i = 0; i < bs.Length; i++)
                {
                    var b = bs[i];
                    if (b == null || !ReferenceEquals(b.F, f)) continue;
                    if (!f.IsAIControlled) return;
                    f.SetMovementOrder(b.Order);
                    if (Thread.CurrentThread.ManagedThreadId == Volatile.Read(ref _mainThread)) Interlocked.Increment(ref _bMain); else Interlocked.Increment(ref _bOther);
                    return;
                }
            }
            catch (Exception e)
            {
                Interlocked.Increment(ref _bStum);
                if (_sErrs < ErrMax) { _sErrs++; try { Log.Error("SiegeFire455.SparsePostfix", e); } catch { } }
            }
        }

        // ------------------------------------------------------------ misja

        private bool Siege(Mission m)
        {
            if (!_known)
            {
                try { _siege = m != null && m.IsSiegeBattle && !m.IsSallyOutBattle && !m.IsNavalBattle; } catch { _siege = false; }
                _known = true;
            }
            return _siege;
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            if (!_started || _ended || !_siege) return;
            try
            {
                if (affectedAgent == null || affectorAgent == null || !affectedAgent.IsHuman || !blow.IsMissile) return;
                if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;
                if (!MissionTeams.Enemies(affectedAgent.Team, affectorAgent.Team)) return;
                var fv = affectedAgent.Formation;
                var fa = affectorAgent.Formation;
                float now = Mission != null ? Mission.CurrentTime : 0f;
                lock (_lock)
                {
                    Ex x;
                    if (fv != null && _ex.TryGetValue(fv, out x)) { x.Lost.Add(now); if (x.Back) x.LostIn++; }
                    if (fa != null && _ex.TryGetValue(fa, out x)) { x.Kill.Add(now); if (x.Back) x.KillIn++; }
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
                    _keyFire = FireOn;
                    _keyBack = BackOn;
                    Volatile.Write(ref _mainThread, Thread.CurrentThread.ManagedThreadId);
                    // liczniki prefiksu / postfiksu tylko tej misji
                    _w = null;
                    _backs = new BackW[0];
                    _cCalls = _cOur = _cDry = _cSame = _cSmall = _cForeign = _cMoved = _cTr = _cStum = 0;
                    _bMain = _bOther = _bStum = 0;
                }
                if (m.MissionEnded || now < _next) return;
                _next = now + Every;
                SiegeQuerySystem q = null;
                try { q = TeamAISiegeComponent.QuerySystem; } catch { q = null; }
                try { ScanFire(m, now, q); } catch (Exception e) { Stumble("Ogien", e); }
                try { ScanBack(m, now, q); } catch (Exception e) { Stumble("Cofanie", e); }
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        // ------------------------------------------------------------ (A) glowny watek

        private void ScanFire(Mission m, float now, SiegeQuerySystem q)
        {
            var t = m.DefenderTeam;
            if (t == null || !MissionTeams.Valid(t) || t.IsPlayerTeam) { _w = null; _fireWhy = "obroncy to druzyna gracza albo brak druzyny"; _fireTeam = false; return; }
            var tac = _fTactic != null && t.TeamAI != null ? _fTactic.GetValue(t.TeamAI) as TacticDefendCastle : null;
            var d = t.TeamAI as TeamAISiegeDefender;
            if (tac == null || d == null || q == null) { _w = null; _fireWhy = "brak taktyki TacticDefendCastle / zapytan oblezenia"; _fireTeam = false; return; }
            _fireTeam = true;
            _fireWhy = "-";
            var p = new float[LaneN];
            float sum = 0f, max = 0f;
            int km = 0;
            for (int k = 0; k < LaneN; k++)
            {
                string pre = SiegeMissionLog370.LanePrefix((FormationAI.BehaviorSide)k);
                int reg = SiegeMissionLog370.QVal(q, pre + "RegionMemberCount");
                int close = SiegeMissionLog370.QVal(q, pre + "CloseAttackerCount");
                float raw = Math.Max(0, reg) + 2f * Math.Max(0, close);
                _pe[k] = _pe[k] < 0f ? raw : _pe[k] + Ema * (raw - _pe[k]);
                p[k] = _pe[k];
                sum += p[k];
                if (p[k] > max) { max = p[k]; km = k; }
            }
            var f = new float[LaneN];
            for (int k = 0; k < LaneN; k++) f[k] = 1f;
            if (sum >= PressMin && max > 0f)
                for (int k = 0; k < LaneN; k++) f[k] = 1f + (float)Math.Round(p[k] / max / Step) * Step;
            bool changed = false;
            for (int k = 0; k < LaneN; k++) if (Math.Abs(f[k] - _lastF[k]) > 0.001f) changed = true;
            if (changed || _w == null || !ReferenceEquals(_w.Tac, tac))
            {
                _w = new FireW { Tac = tac, Team = t, F = f, Real = _keyFire };
                if (changed)
                {
                    _fChanges++;
                    if (_lines < LineMax)
                    {
                        _lines++;
                        Log.Info(P + "t=" + now.ToString("0", Inv) + "s | ogien obrony " + (_keyFire ? "" : "(bylby) ") + "nacisk L/M/P " + p[0].ToString("0", Inv) + "/" + p[1].ToString("0", Inv) + "/" + p[2].ToString("0", Inv)
                                 + " -> mnozniki " + f[0].ToString("0.00", Inv) + "/" + f[1].ToString("0.00", Inv) + "/" + f[2].ToString("0.00", Inv) + ".");
                    }
                }
                _lastF = f;
            }
            if (sum < PressMin) return;
            // pomiar: strzelcy przy najbardziej naciskanym pasie - rzeczywiscie / cel gry / cel reguly
            var aps = d.ArcherPositions;
            if (aps == null) return;
            int total = 0, at = 0, n = 0;
            float sw = 0f, swAt = 0f, so = 0f, soAt = 0f;
            var fs = t.FormationsIncludingEmpty;
            for (int i = 0; fs != null && i < fs.Count; i++)
            {
                var x = fs[i];
                if (x == null || x.CountOfUnits <= 0 || x.AI == null) continue;
                var b = x.AI.ActiveBehavior as BehaviorShootFromCastleWalls;
                if (b == null || b.ArcherPosition == null) continue;
                var ap = FindAp(aps, b.ArcherPosition);
                float tw = TagW(b.ArcherPosition);
                float fo = ap != null ? SideF(f, ap.ConnectedSides) : 1f;
                bool con = ap != null && (ap.ConnectedSides & (1 << km)) != 0;
                int u = x.CountOfUnitsWithoutDetachedOnes;
                n++;
                total += u;
                sw += tw; so += tw * fo;
                if (con) { at += u; swAt += tw; soAt += tw * fo; }
            }
            if (n < 2 || total <= 0 || sw <= 0f) return;
            _mN++;
            _mAct += (double)at / total;
            _mGame += swAt / sw;
            _mOur += so > 0f ? soAt / so : 0f;
            _mPress += max / sum;
        }

        // ------------------------------------------------------------ (B) glowny watek

        private static int State(TacticBreachWalls tac)
        {
            try { return _fState != null ? Convert.ToInt32(_fState.GetValue(tac), Inv) : -1; } catch { return -1; }
        }

        private void ScanBack(Mission m, float now, SiegeQuerySystem q)
        {
            bool broken = false;
            try { var sb = m.GetMissionBehavior<SiegeBreak371>(); broken = sb != null && sb.IsBroken; } catch { broken = false; }
            var seen = new HashSet<Formation>();
            var teams = m.Teams;
            for (int ti = 0; teams != null && ti < teams.Count; ti++)
            {
                var t = teams[ti];
                try
                {
                    if (!MissionTeams.Valid(t) || t.Side != BattleSideEnum.Attacker) continue;
                    if (t.IsPlayerTeam && t.IsPlayerGeneral) continue;     // szyki gracza-wodza - jego
                    var tac = _fTactic != null && t.TeamAI != null ? _fTactic.GetValue(t.TeamAI) as TacticBreachWalls : null;
                    if (tac == null || broken || State(tac) != StAssault) continue;
                    var fs = t.FormationsIncludingEmpty;
                    for (int i = 0; fs != null && i < fs.Count; i++)
                    {
                        var f = fs[i];
                        try
                        {
                            if (f == null || f.CountOfUnits <= 0 || !f.IsAIControlled || f.AI == null) continue;
                            var sk = f.AI.ActiveBehavior as BehaviorSparseSkirmish;
                            if (sk == null) continue;
                            seen.Add(f);
                            Ex x;
                            lock (_lock)
                            {
                                if (!_ex.TryGetValue(f, out x)) { x = new Ex { F = f, T = t }; _ex[f] = x; _exList.Add(x); }
                            }
                            Decide(m, now, q, x, sk);
                        }
                        catch (Exception e) { Stumble("Szyk", e); }
                    }
                }
                catch (Exception e) { Stumble("Druzyna", e); }
            }
            // cofniecia szykow, ktore przestaly sie kwalifikowac - koniec przed czasem
            for (int i = 0; i < _exList.Count; i++)
            {
                var x = _exList[i];
                if (x.Back && !seen.Contains(x.F)) { End(now, x); _endEarly++; }
            }
        }

        private void Decide(Mission m, float now, SiegeQuerySystem q, Ex x, BehaviorSparseSkirmish sk)
        {
            var f = x.F;
            if (x.Back)
            {
                if (!x.Arrived && f.CurrentPosition.Distance(x.Pt) <= ArriveDist) { x.Arrived = true; if (_keyBack) _arrived++; }
                if (now - x.BackAt >= HoldSec) End(now, x);
                return;
            }
            if (now < x.CoolUntil) return;
            int lost, kill;
            lock (_lock)
            {
                Prune(x.Lost, now - Window);
                Prune(x.Kill, now - Window);
                lost = x.Lost.Count;
                kill = x.Kill.Count;
            }
            if (lost < LossMin || lost < Ratio * kill) return;
            // punkt cofniecia: od najblizszego poczatku pasa obrony przez stanowisko, 30 / 20 / 12 m dalej, na siatce w polu bitwy
            if (q == null) return;
            Vec2 base0 = sk.ArcherPosition != null ? sk.ArcherPosition.GlobalPosition.AsVec2 : f.CurrentPosition;
            Vec2 wall = q.LeftDefenderOrigin.AsVec2;
            float bd = base0.DistanceSquared(wall);
            Vec2 c2 = q.MidDefenderOrigin.AsVec2;
            if (base0.DistanceSquared(c2) < bd) { wall = c2; bd = base0.DistanceSquared(c2); }
            c2 = q.RightDefenderOrigin.AsVec2;
            if (base0.DistanceSquared(c2) < bd) { wall = c2; }
            Vec2 dir = base0 - wall;
            float len = dir.Length;
            if (len < 1f) { _noRoom++; x.CoolUntil = now + NoRoomCool; return; }
            dir = dir * (1f / len);
            float cur = f.CurrentPosition.Distance(wall);
            WorldPosition pos = WorldPosition.Invalid;
            Vec2 pt = Vec2.Invalid;
            bool farAlready = false;
            for (int i = 0; i < BackDist.Length; i++)
            {
                if (cur >= len + BackDist[i] - 2f) { farAlready = true; break; }   // szyk juz stoi tak daleko
                Vec2 c = base0 + dir * BackDist[i];
                if (!m.IsPositionInsideBoundaries(c)) continue;
                WorldPosition wp = f.CachedMedianPosition;
                wp.SetVec2(c);
                if (!wp.IsValid || wp.GetNavMesh() == UIntPtr.Zero) continue;
                pos = wp; pt = c;
                break;
            }
            if (!pt.IsValid)
            {
                if (farAlready) _already++; else _noRoom++;
                x.CoolUntil = now + NoRoomCool;
                return;
            }
            x.Back = true;
            x.Arrived = false;
            x.BackAt = now;
            x.Pt = pt;
            x.LostAt = lost; x.KillAt = kill;
            lock (_lock) { x.LostIn = 0; x.KillIn = 0; }
            _eps++;
            _epF.Add(f);
            _sLostAt += lost; _sKillAt += kill;
            if (_keyBack) Publish(f, MovementOrder.MovementOrderMove(pos), true);
            if (_lines < LineMax)
            {
                _lines++;
                Log.Info(P + "t=" + now.ToString("0", Inv) + "s | F" + (int)f.FormationIndex + " druzyny " + (x.T != null ? x.T.TeamIndex.ToString(Inv) : "?") + " (" + f.CountOfUnits + " ludzi) "
                         + (_keyBack ? "cofa sie" : "bylby sie cofnal") + ": polegli od pociskow " + lost + " w " + Window.ToString("0", Inv) + " s, zabili pociskami " + kill
                         + ", punkt " + base0.Distance(pt).ToString("0", Inv) + " m za stanowiskiem (" + pt.Distance(wall).ToString("0", Inv) + " m od pasa, szyk byl " + cur.ToString("0", Inv) + " m), na " + HoldSec.ToString("0", Inv) + " s.");
            }
        }

        private void End(float now, Ex x)
        {
            int li, ki;
            lock (_lock)
            {
                li = x.LostIn; ki = x.KillIn;
                x.Lost.Clear(); x.Kill.Clear();
                x.LostIn = 0; x.KillIn = 0;
            }
            _sLostIn += li; _sKillIn += ki;
            _sSecIn += Math.Max(0f, now - x.BackAt);
            x.Back = false;
            x.CoolUntil = now + CoolSec;
            if (_keyBack) Publish(x.F, default(MovementOrder), false);
        }

        private static void Publish(Formation f, MovementOrder order, bool add)
        {
            var cur = _backs;
            var r = new List<BackW>(cur.Length + 1);
            for (int i = 0; i < cur.Length; i++) if (cur[i] != null && !ReferenceEquals(cur[i].F, f)) r.Add(cur[i]);
            if (add) r.Add(new BackW { F = f, Order = order });
            _backs = r.ToArray();
        }

        private static void Prune(List<float> l, float from)
        {
            int k = 0;
            while (k < l.Count && l[k] < from) k++;
            if (k > 0) l.RemoveRange(0, k);
        }

        protected override void OnEndMission()
        {
            try
            {
                var m = Mission;
                if (_started && !_ended && _siege)
                {
                    float now = m != null ? m.CurrentTime : 0f;
                    // cofniecia w toku - policzone do konca misji
                    for (int i = 0; i < _exList.Count; i++) if (_exList[i].Back) End(now, _exList[i]);
                    var sb = new StringBuilder(768);
                    sb.Append(P.Substring(0, P.Length - 2)).Append(" koniec: t=").Append(now.ToString("0", Inv)).Append("s")
                      .Append(" | ogien obrony za naciskiem (klucz Defender Fire Follows Assault ").Append(_keyFire ? "TAK" : "NIE").Append("): ");
                    if (!_fireTeam && _cCalls == 0) sb.Append("bez druzyny obrony AI - ").Append(_fireWhy);
                    else
                    {
                        sb.Append("rozdzialow gry ").Append(_cCalls).Append(", ").Append(_keyFire ? "nasz rozdzial " + _cOur : "bylby inny " + _cDry)
                          .Append(", bez zmiany (rowne mnozniki) ").Append(_cSame).Append(", lista < 2 ").Append(_cSmall).Append(", szyk nie AI / bez stanowiska ").Append(_cForeign)
                          .Append(", przelozen ").Append(_cTr).Append(" (").Append(_cMoved).Append(" ludzi), zmian mnoznikow ").Append(_fChanges);
                        if (_mN > 0)
                            sb.Append(" | strzelcy przy najbardziej naciskanym pasie: sr. ").Append((100.0 * _mAct / _mN).ToString("0", Inv)).Append("% (cel gry ")
                              .Append((100.0 * _mGame / _mN).ToString("0", Inv)).Append("%, cel reguly ").Append((100.0 * _mOur / _mN).ToString("0", Inv)).Append("%), nacisk tego pasa ")
                              .Append((100.0 * _mPress / _mN).ToString("0", Inv)).Append("%, probek ").Append(_mN);
                        else sb.Append(" | probek nacisku 0 (nacisk < ").Append(PressMin.ToString("0", Inv)).Append(" albo < 2 stanowiska)");
                    }
                    sb.Append(" | cofanie strzelcow ataku (klucz Siege Archers Fall Back ").Append(_keyBack ? "TAK" : "NIE").Append("): ")
                      .Append(_keyBack ? "cofniec " : "bylby cofniec ").Append(_eps).Append(" (szykow ").Append(_epF.Count).Append("), sledzonych szykow ").Append(_exList.Count)
                      .Append(", brak miejsca ").Append(_noRoom).Append(", juz dalej ").Append(_already).Append(", koniec przed czasem ").Append(_endEarly);
                    if (_keyBack) sb.Append(", dotarlo ").Append(_arrived);
                    if (_eps > 0)
                    {
                        float perIn = _sSecIn > 1f ? Window / _sSecIn : 0f;
                        sb.Append(" | na cofniecie: polegli od pociskow ").Append(((float)_sLostAt / _eps).ToString("0.0", Inv)).Append(" / ").Append(Window.ToString("0", Inv)).Append(" s przed, ")
                          .Append((_sLostIn * perIn).ToString("0.0", Inv)).Append(" / ").Append(Window.ToString("0", Inv)).Append(" s w ").Append(_keyBack ? "cofnieciu" : "oknie (stali)")
                          .Append(", zabili pociskami ").Append(((float)_sKillAt / _eps).ToString("0.0", Inv)).Append(" przed, ").Append((_sKillIn * perIn).ToString("0.0", Inv)).Append(" w")
                          .Append(" (razem ").Append(_sSecIn.ToString("0", Inv)).Append(" s)");
                    }
                    sb.Append(" | postfiks: rozkazow ").Append(_bMain + _bOther).Append(" (glowny watek ").Append(_bMain).Append(", watek silnika ").Append(_bOther).Append(')')
                      .Append(" | progi: >= ").Append(LossMin).Append(" poleglych od pociskow w ").Append(Window.ToString("0", Inv)).Append(" s i >= ").Append(Ratio).Append(" x zabitych przez szyk, ")
                      .Append(BackDist[0].ToString("0", Inv)).Append(" m na ").Append(HoldSec.ToString("0", Inv)).Append(" s; nacisk >= ").Append(PressMin.ToString("0", Inv))
                      .Append(" | potkniecia ").Append(_stumbles).Append(" + prefiks ").Append(_cStum).Append(" + postfiks ").Append(_bStum).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            _w = null;
            _backs = new BackW[0];
            lock (_lock) { _ex.Clear(); }
            _exList.Clear();
            _epF.Clear();
            base.OnEndMission();
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("SiegeFire455." + where, e); } catch { }
        }
    }
}
