using System;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 446 = PAKIET 5 kroki 5.2 i 5.3 audytu AI bitwy (docs/audyty-2026-11-11/AUDYT-AI-BITWA.md rozdz. 3 "Obrona przed jazda"; tabela rozdz. 2: "piechota zna tylko
    /// mur tarcz od czola, czworoboku brak - ZLE"; Jeff 11.11 "rob wszystko po kolei tak jak radzisz"). Zadnych bonusow do liczb - tylko decyzje. Bitwa polowa,
    /// nie morska. Klucz InfantrySquareAgainstFlankCharge ("Infantry Square Against Flank Charge").
    ///
    /// 5.2 DECYZJA (glowny watek, Doctrine.Tick, wlasny zegar 0.5 s - jak pass CloseRanks): szyk piechoty AI (IsInfantryFormation, >= 12 ludzi, IsAIControlled,
    ///   nie szyk gracza / z czlowiekiem gracza - HorsePursuit.PlayerForm; druzyna TeamAIGeneral obu stron) staje w czworobok (Square; szyk < 40 ludzi - kolo,
    ///   Circle), gdy:
    ///   - szyk jazdy wroga (nie konni strzelcy, >= 5 i >= 10% naszego szyku - prog RBM) jedzie na niego: predkosc > 2 m/s, cos kata > 0.5 i dojazd <= 10 s,
    ///     albo w 25 m i zbliza sie (> 1.5 m/s, cos > 0.3);
    ///   - z BOKU albo z TYLU: kierunek od nas do jazdy wobec frontu szyku (Formation.Direction) cos < 0.5 (czolo zostaje RBM - mur tarcz rbmai ok. 4688 / 13015 /
    ///     13050);
    ///   - BEZ OSLONY: zadna nasza jazda (>= 5) w 40 m od tej jazdy wroga i zaden nasz inny szyk piechoty (>= 10) na odcinku jazda - my (T442Blocked);
    ///   - szyk wolny: < 30% ludzi w zwarciu (miara 442), pieszy wrog dalej niz 25 m (FootGap, S1ContactGap), rozkaz szyku nie Charge / ChargeWithTarget
    ///     (w szarzy ludzie i tak nie stoja w szyku);
    ///   - przerwa 15 s od poprzedniego czworoboku i zostalo >= 10 s budzetu 5.3.
    /// BLOKADA (5.2): szyk w naszym czworoboku nie zmienia ustawienia - Pendulum333.PreArr (prefiks Formation.SetArrangementOrder 344, Priority.Last po com.rbmai,
    ///   kazdy watek) najpierw pyta Q446Guard: kazda zmiana na inne ustawienie odrzucona (zwrot false) i liczona wedlug celu (ShieldWall - trzy miejsca RBM przeciw
    ///   konnicy rbmai ok. 4688 / 13015 / 13050 i decyzja szyku RBM; Line - Regroup RBM co tick, HoldHighGround; Loose; Square / Circle gry; inne) i watku.
    ///   Blokada obejmuje WSZYSTKIE drogi przez SetArrangementOrder (RBM nie lata tej metody - sprawdzone w rbmai). Nasze wlasne wywolania (wejscie / powrot) -
    ///   znacznik watku _q6Self. Szyk oddany graczowi (IsAIControlled false) - nigdy nie blokowany. Bez latki ustawienia 344 decyzja tylko liczona ("bez blokady").
    ///   CloseRanks (146 / 310) pomija szyk w czworoboku (pass i decyzja RBM - wtedy cialo RBM biegnie, a jego zmiana trafia na blokade i licznik).
    /// 5.3 LIMIT: Square ma bieg 0.3 (Circle 0.5) - jeden czworobok najwyzej 30 s, lacznie najwyzej 90 s na szyk w bitwie; wyjscie po 5 s bez zagrozenia (jazda wroga
    ///   >= 5 w 25 m albo jadaca jak wyzej z dowolnej strony), gdy pieszy wrog podejdzie w 25 m (a jazdy w 25 m nie ma), przy rozkazie szarzy; najkrocej 6 s
    ///   (bez migotania). Wyjscie = powrot do ustawienia sprzed czworoboku.
    /// Klucz NIE (albo bez latki 344): te same reguly i te same epizody "na sucho" (bylby) - ustawienie nie ruszane, co do znaku jak dotad; straty w epizodach
    ///   "bylby" do porownania ze stratami w czworoboku.
    /// POMIAR (linia "Czworobok (446)"): wejscia (Square / Circle, z boku / z tylu), odrzucone (z czola, oslona piechota / jazda, zwarcie, piechota wroga blisko,
    ///   szarza, limit, przerwa), szyko-s w czworoboku i bylby, srednio na epizod, maks na szyk, naraz maks, wyjscia wedlug powodu, jazda dojechala do czworoboku,
    ///   proby nadpisania przez RBM / gre (wedlug ustawienia i watku), straty piechoty AI od ciosow wrecz jezdzcow wroga: w czworoboku / w epizodzie bylby / poza,
    ///   srednio 10 s przed wejsciem i w trakcie epizodu.
    /// Watki: decyzja, skan i SetArrangementOrder w glownym watku; Q446Guard / Q446Holds (prefiks w watku silnika, CloseRanks) - tylko odczyt tablic slotow
    ///   (Volatile) i liczniki Interlocked. Potkniecia per szyk liczone (czworobok oddany, slot od zera), bez globalnego wylacznika.
    /// </summary>
    internal static partial class Doctrine
    {
        internal const float Q446Every = 0.5f, Q446Eta = 10f, Q446Near = 25f, Q446Speed = 2f, Q446NearSpeed = 1.5f, Q446Dot = 0.5f, Q446NearDot = 0.3f;
        internal const float Q446Front = 0.5f, Q446Rear = -0.5f, Q446CavCover = 40f, Q446Melee = 0.3f, Q446MinHold = 6f, Q446Calm = 5f, Q446Max = 30f;
        internal const float Q446Budget = 90f, Q446MinLeft = 10f, Q446Cool = 15f, Q446Reach = 5f, Q446FoeShare = 0.1f;
        // 446-p: przedluzenie przy jezdzie w 25 m (limit 30 / 90 s nie zrywa czworoboku w kontakcie) - twardy sufit; obrot czola (cos kierunkow < 45 st.)
        internal const float Q446MaxHeld = 60f, Q446BudgetHeld = 120f, Q446TurnCos = 0.7071f;
        private const int Q6Back = 8;   // 446-p: kierunek szyku sprzed 4 s (8 probek co 0.5 s) - sprzed obrotu czola przez RBM
        internal const int Q446MinMen = 12, Q446SquareMen = 40;
        private const int Q6R = 21;   // pierscien strat od jazdy slotu: 10 s (probka co 0.5 s)
        private const byte QmNone = 0, QmReal = 1, QmDry = 2;
        private static readonly IFormatProvider Q6Inv = CultureInfo.InvariantCulture;

        // ---- stan slotow [TeamIndex x 10 + FormationIndex] (glowny watek; _q6Hold / _q6HoldArr czyta tez prefiks w watku silnika - Volatile)
        private static volatile bool _q6On;   // decyzja prawdziwa: misja polowa + klucz + latka ustawienia 344
        private static bool _q6Live;
        private static float _q6Acc;
        private static int _q6RPos, _q6Main = -1;
        [ThreadStatic] private static bool _q6Self;
        private static readonly Formation[] _q6Who = new Formation[CrSlots], _q6Hold = new Formation[CrSlots];
        private static readonly int[] _q6HoldArr = new int[CrSlots], _q6Before = new int[CrSlots];
        private static readonly byte[] _q6Mode = new byte[CrSlots];
        private static readonly float[] _q6Since = new float[CrSlots], _q6Last = new float[CrSlots], _q6Cool = new float[CrSlots], _q6Used = new float[CrSlots];
        private static readonly float[] _q6DryAt = new float[CrSlots];
        private static readonly bool[] _q6Reached = new bool[CrSlots];
        private static readonly int[] _q6Loss = new int[CrSlots], _q6Loss0 = new int[CrSlots], _q6Ring = new int[CrSlots * Q6R];
        // 446-p: pierscien kierunku szyku (glowny watek) i ostatnie odrzucone ustawienie spoza naszych wywolan (pisze prefiks w dowolnym watku - Volatile)
        private static readonly float[] _q6DirX = new float[CrSlots * Q6R], _q6DirY = new float[CrSlots * Q6R];
        private static readonly int[] _q6Want = new int[CrSlots];
        private static readonly bool[] _q6Ext = new bool[CrSlots];

        // ---- liczniki misji (Q446Reset w Battle291.Begin)
        private static int _q6In, _q6InSq, _q6InCi, _q6InFlank, _q6InRear, _q6Dry, _q6DryNoGuard, _q6Refused;
        private static int _q6Front, _q6CovInf, _q6CovCav, _q6Busy, _q6FootNear, _q6Charge, _q6Cap, _q6CoolN;
        private static int _q6XCalm, _q6XMax, _q6XBudget, _q6XFoot, _q6XCharge, _q6XLost, _q6XBack, _q6XKey, _q6Open, _q6ReachN, _q6EpEnd, _q6Stum;
        private static int _q6DryEnd, _q6MaxNow;
        private static int _q6Turned, _q6InTurned, _q6Retreat, _q6XRetreat, _q6XPlayer, _q6Held, _q6WantUsed, _q6ChargeLine;   // 446-p
        private static float _q6Sec, _q6DrySec, _q6MaxUsed, _q6MaxUsedDry;
        private static int _q6LossReal, _q6LossDry, _q6LossOut;
        private static double _q6BeforeSum, _q6DuringSum, _q6DryBeforeSum, _q6DryDuringSum;
        // blokada (prefiks - dowolny watek): Interlocked
        private static int _q6BWall, _q6BLine, _q6BLoose, _q6BSq, _q6BOther, _q6BAi, _q6BMain, _q6SelfN;

        /// <summary>Battle291.Begin (AfterStart): liczniki linii 446 od zera.</summary>
        internal static void Q446Reset()
        {
            _q6In = _q6InSq = _q6InCi = _q6InFlank = _q6InRear = _q6Dry = _q6DryNoGuard = _q6Refused = 0;
            _q6Front = _q6CovInf = _q6CovCav = _q6Busy = _q6FootNear = _q6Charge = _q6Cap = _q6CoolN = 0;
            _q6XCalm = _q6XMax = _q6XBudget = _q6XFoot = _q6XCharge = _q6XLost = _q6XBack = _q6XKey = _q6Open = _q6ReachN = _q6EpEnd = _q6Stum = 0;
            _q6DryEnd = _q6MaxNow = 0;
            _q6Turned = _q6InTurned = _q6Retreat = _q6XRetreat = _q6XPlayer = _q6Held = _q6WantUsed = _q6ChargeLine = 0;
            _q6Sec = _q6DrySec = _q6MaxUsed = _q6MaxUsedDry = 0f;
            _q6LossReal = _q6LossDry = _q6LossOut = 0;
            _q6BeforeSum = _q6DuringSum = _q6DryBeforeSum = _q6DryDuringSum = 0.0;
            Interlocked.Exchange(ref _q6BWall, 0); Interlocked.Exchange(ref _q6BLine, 0); Interlocked.Exchange(ref _q6BLoose, 0);
            Interlocked.Exchange(ref _q6BSq, 0); Interlocked.Exchange(ref _q6BOther, 0); Interlocked.Exchange(ref _q6BAi, 0);
            Interlocked.Exchange(ref _q6BMain, 0); Interlocked.Exchange(ref _q6SelfN, 0);
        }

        /// <summary>Doctrine.Begin (AfterStart, po Clear): bitwa polowa, nie morska - pomiar zawsze, decyzja wedlug klucza (czytany co takt).</summary>
        private static void Q446Begin(Mission m)
        {
            bool field = false;
            try { field = m != null && m.IsFieldBattle && !m.IsNavalBattle; } catch { field = false; }
            _q6Acc = 0f;
            _q6Main = Thread.CurrentThread.ManagedThreadId;
            _q6Live = field;
            _q6On = false;
        }

        /// <summary>Doctrine.Clear (Begin / End): stan slotow od zera; epizody w toku liczone. Ustawienia nie przywracamy (misja sie konczy).</summary>
        private static void Q446ClearMission()
        {
            _q6On = false;
            _q6Live = false;
            for (int i = 0; i < CrSlots; i++)
            {
                if (_q6Mode[i] != QmNone) _q6Open++;
                Volatile.Write(ref _q6Hold[i], null);
                _q6HoldArr[i] = -1;
                Volatile.Write(ref _q6Want[i], -1);
                _q6Ext[i] = false;
                _q6Who[i] = null; _q6Mode[i] = QmNone; _q6Reached[i] = false;
                _q6Since[i] = _q6Last[i] = 0f; _q6Cool[i] = _q6DryAt[i] = -1000f; _q6Used[i] = 0f;
                _q6Loss[i] = _q6Loss0[i] = 0; _q6Before[i] = 0;
            }
            for (int i = 0; i < _q6Ring.Length; i++) { _q6Ring[i] = 0; _q6DirX[i] = _q6DirY[i] = 0f; }
            _q6RPos = 0;
            _q6Acc = 0f;
        }

        // ------------------------------------------------------------ odczyty dla prefiksu ustawienia i CloseRanks (dowolny watek)

        /// <summary>CloseRanks (pass i prefiks decyzji RBM): szyk stoi teraz w czworoboku 446 - jeden wlasciciel ustawienia.</summary>
        internal static bool Q446Holds(Formation f)
        {
            if (!_q6On || f == null) return false;
            int idx = C441Idx(f);
            return idx >= 0 && ReferenceEquals(Volatile.Read(ref _q6Hold[idx]), f);
        }

        /// <summary>Pendulum333.PreArr (prefiks Formation.SetArrangementOrder, kazdy watek): false = zmiana ustawienia szyku w czworoboku odrzucona (licznik).</summary>
        internal static bool Q446Guard(Formation f, ArrangementOrder order)
        {
            if (!_q6On || f == null) return true;
            try
            {
                int idx = C441Idx(f);
                if (idx < 0 || !ReferenceEquals(Volatile.Read(ref _q6Hold[idx]), f)) return true;
                if (_q6Self) { Interlocked.Increment(ref _q6SelfN); return true; }
                if (!f.IsAIControlled) return true;   // szyk wrocil pod gracza - jego rozkaz zawsze przechodzi
                var nw = order.OrderEnum;
                if ((int)nw == Volatile.Read(ref _q6HoldArr[idx])) return true;
                switch (nw)
                {
                    case ArrangementOrder.ArrangementOrderEnum.ShieldWall: Interlocked.Increment(ref _q6BWall); break;
                    case ArrangementOrder.ArrangementOrderEnum.Line: Interlocked.Increment(ref _q6BLine); break;
                    case ArrangementOrder.ArrangementOrderEnum.Loose: Interlocked.Increment(ref _q6BLoose); break;
                    case ArrangementOrder.ArrangementOrderEnum.Square:
                    case ArrangementOrder.ArrangementOrderEnum.Circle: Interlocked.Increment(ref _q6BSq); break;
                    default: Interlocked.Increment(ref _q6BOther); break;
                }
                if (Thread.CurrentThread.ManagedThreadId == _q6Main) Interlocked.Increment(ref _q6BMain); else Interlocked.Increment(ref _q6BAi);
                // 446-p: ostatnia odrzucona zmiana (np. Line z aktywacji BehaviorCharge) - przy wyjsciu przywracana zamiast stanu sprzed czworoboku
                Volatile.Write(ref _q6Want[idx], (int)nw);
                return false;
            }
            catch { return true; }
        }

        /// <summary>BattleSense.OnAgentRemoved (glowny watek, przed Agent.OnRemove - szyk jeszcze przypisany): czlowiek szyku piechoty AI padl od ciosu wrecz
        /// jezdzca wroga - licznik slotu i podzial: w czworoboku / w epizodzie bylby / poza.</summary>
        internal static void Q446OnRemoved(Agent v, Agent by, AgentState st, in KillingBlow blow)
        {
            if (!_q6Live || v == null || by == null) return;
            try
            {
                if (st != AgentState.Killed && st != AgentState.Unconscious) return;
                if (!v.IsHuman || !by.IsHuman || by.MountAgent == null || blow.IsMissile) return;
                var f = v.Formation;
                if (f == null || !MissionTeams.Enemies(v.Team, by.Team)) return;
                int idx = C441Idx(f);
                if (idx < 0 || !ReferenceEquals(_q6Who[idx], f)) return;
                // 446-p: tylko szyki piechoty (straty "poza" bez lucznikow i jazdy)
                var fq = f.QuerySystem;
                if (fq == null || !fq.IsInfantryFormation || fq.IsCavalryFormation || fq.IsRangedCavalryFormation) return;
                _q6Loss[idx]++;
                byte md = _q6Mode[idx];
                if (md == QmReal) _q6LossReal++;
                else if (md == QmDry) _q6LossDry++;
                else _q6LossOut++;
            }
            catch (Exception e) { _q6Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Square446 strata", e); }
        }

        // ------------------------------------------------------------ takt glownego watku (Doctrine.Tick)

        internal static void Q446Tick(Mission m, float dt)
        {
            if (!_q6Live || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m) || m.MissionEnded) return;
            _q6Acc += dt;
            if (_q6Acc < Q446Every) return;
            float step = _q6Acc;
            _q6Acc = 0f;
            bool dep;
            try { dep = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && m.CurrentTime >= P440NoDeploy); } catch { dep = false; }
            if (!dep) return;
            var c = Settings.Current;
            if (c == null) return;
            _q6On = c.InfantrySquareAgainstFlankCharge && Pendulum333.ArrPatched;
            float now = m.CurrentTime;
            _t4Now = now;   // T442MeleeShare (glowny watek)
            _q6RPos = (_q6RPos + 1) % Q6R;
            var teams = m.Teams;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!R443AiTeam(t)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    Formation f = null;
                    try { f = fl[j]; } catch { f = null; }
                    if (f == null) continue;
                    int idx = C441Idx(f);
                    if (idx < 0) continue;
                    try { Q446Form(t, f, idx, now, step); }
                    catch (Exception e)
                    {
                        // potkniecie szyku: czworobok oddany (ustawienie sprzed), slot epizodu od zera - bez globalnego wylacznika
                        _q6Stum++;
                        try { Q446End(f, idx, now, 0, f != null && f.IsAIControlled); } catch { Volatile.Write(ref _q6Hold[idx], null); _q6Mode[idx] = QmNone; }
                        RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Square446 szyk", e);
                    }
                }
            }
            // ile szykow naraz w czworoboku (martwy kod ozywiony - zasada 4)
            int nr = 0;
            for (int i = 0; i < CrSlots; i++) if (_q6Mode[i] == QmReal) nr++;
            if (nr > _q6MaxNow) _q6MaxNow = nr;
        }

        private static void Q446Form(Team t, Formation f, int idx, float now, float step)
        {
            int rb = idx * Q6R;
            if (!ReferenceEquals(_q6Who[idx], f))
            {
                if (_q6Mode[idx] != QmNone) { Volatile.Write(ref _q6Hold[idx], null); _q6Mode[idx] = QmNone; }
                _q6Who[idx] = f; _q6Cool[idx] = _q6DryAt[idx] = -1000f; _q6Used[idx] = 0f; _q6Loss[idx] = 0;
                Volatile.Write(ref _q6Want[idx], -1); _q6Ext[idx] = false;
                for (int k = 0; k < Q6R; k++) { _q6Ring[rb + k] = 0; _q6DirX[rb + k] = _q6DirY[rb + k] = 0f; }
            }
            // 446-p: kierunek szyku co probke (RBM obraca Direction przez FacingOrder na jazde - mb Formation.Tick SetPositioning)
            Vec2 d0 = f.Direction;
            if (d0.IsValid && d0.LengthSquared >= 0.0001f) { d0 = d0.Normalized(); _q6DirX[rb + _q6RPos] = d0.x; _q6DirY[rb + _q6RPos] = d0.y; }
            else { _q6DirX[rb + _q6RPos] = _q6DirY[rb + _q6RPos] = 0f; }
            int lossOld = _q6Ring[rb + (_q6RPos + 1) % Q6R];   // straty od jazdy 10 s temu (licznik narastajacy)
            _q6Ring[rb + _q6RPos] = _q6Loss[idx];
            var q = f.QuerySystem;
            int cnt = f.CountOfUnits;
            bool inf = q != null && q.IsInfantryFormation && !q.IsCavalryFormation && !q.IsRangedCavalryFormation;
            bool ai = f.IsAIControlled;
            bool mine = cnt >= Q446MinMen && ai && !HorsePursuit.PlayerForm(f) && !f.HasPlayerControlledTroop;
            byte mode = _q6Mode[idx];
            if (mode != QmNone)
            {
                Q446Ep(t, f, idx, now, step, inf && mine, ai);
                return;
            }
            if (!inf || !mine || cnt <= 0) return;
            Vec2 me = f.CachedMedianPosition.AsVec2;
            Vec2 dir = f.Direction;
            if (!me.IsValid || !dir.IsValid || dir.LengthSquared < 0.0001f) return;
            dir = dir.Normalized();
            int ob = rb + (_q6RPos - Q6Back + Q6R) % Q6R;
            Vec2 old = new Vec2(_q6DirX[ob], _q6DirY[ob]);
            // zagrozenie z boku / tylu (najblizsze); samo czolo - licznik; czolo obrocone przez RBM w 4 s - bok / tyl wedlug kierunku sprzed obrotu
            byte side;
            Formation foe;
            bool front, turned;
            float gap = Q446Threat(t, f, me, dir, old, cnt, out foe, out side, out front, out turned);
            if (foe == null)
            {
                if (front && now >= _q6DryAt[idx]) { _q6Front++; _q6DryAt[idx] = now + Q446Cool; }
                return;
            }
            Vec2 ep = foe.CachedAveragePosition;
            // oslona: nasza jazda przy tej jezdzie wroga albo nasza inna piechota na odcinku jazda - my
            if (Q446CavNear(t, ep)) { if (now >= _q6DryAt[idx]) { _q6CovCav++; _q6DryAt[idx] = now + Q446Cool; } return; }
            if (T442Blocked(t, f, ep, me)) { if (now >= _q6DryAt[idx]) { _q6CovInf++; _q6DryAt[idx] = now + Q446Cool; } return; }
            // szyk wolny
            var ot = f.GetReadonlyMovementOrderReference().OrderType;
            if (ot == OrderType.Charge || ot == OrderType.ChargeWithTarget) { if (now >= _q6DryAt[idx]) { _q6Charge++; _q6DryAt[idx] = now + Q446Cool; } return; }
            if (ot == OrderType.Retreat || ot == OrderType.FallBack) { if (now >= _q6DryAt[idx]) { _q6Retreat++; _q6DryAt[idx] = now + Q446Cool; } return; }
            if (FootGap(t, f) <= S1ContactGap) { if (now >= _q6DryAt[idx]) { _q6FootNear++; _q6DryAt[idx] = now + Q446Cool; } return; }
            if (T442MeleeShare(f) >= Q446Melee) { if (now >= _q6DryAt[idx]) { _q6Busy++; _q6DryAt[idx] = now + Q446Cool; } return; }
            if (now < _q6Cool[idx]) { if (now >= _q6DryAt[idx]) { _q6CoolN++; _q6DryAt[idx] = now + Q446Cool; } return; }
            if (_q6Used[idx] > Q446Budget - Q446MinLeft) { if (now >= _q6DryAt[idx]) { _q6Cap++; _q6DryAt[idx] = now + Q446Cool; } return; }
            var target = cnt >= Q446SquareMen ? ArrangementOrder.ArrangementOrderEnum.Square : ArrangementOrder.ArrangementOrderEnum.Circle;
            var cur = f.ArrangementOrder.OrderEnum;
            if (!_q6On)
            {
                // klucz NIE albo bez latki ustawienia 344 - epizod na sucho (te same wyjscia i budzet), ustawienie bez zmian
                _q6Mode[idx] = QmDry; _q6Since[idx] = _q6Last[idx] = now; _q6Reached[idx] = false; _q6Loss0[idx] = _q6Loss[idx]; _q6Ext[idx] = false;
                if (turned) _q6Turned++;
                _q6DryBeforeSum += Math.Max(0, _q6Loss[idx] - lossOld);
                if (Settings.Current != null && Settings.Current.InfantrySquareAgainstFlankCharge) _q6DryNoGuard++; else _q6Dry++;
                return;
            }
            _q6Before[idx] = (int)cur;
            _q6HoldArr[idx] = (int)target;
            Volatile.Write(ref _q6Want[idx], -1);
            Volatile.Write(ref _q6Hold[idx], f);
            if (cur != target)
            {
                _q6Self = true;
                try { f.SetArrangementOrder(target == ArrangementOrder.ArrangementOrderEnum.Square ? ArrangementOrder.ArrangementOrderSquare : ArrangementOrder.ArrangementOrderCircle); }
                finally { _q6Self = false; }
            }
            if (f.ArrangementOrder.OrderEnum != target)
            {
                // inny prefiks odrzucil zmiane - bez czworoboku
                Volatile.Write(ref _q6Hold[idx], null);
                _q6Refused++;
                _q6Cool[idx] = now + Q446Cool;
                return;
            }
            _q6Mode[idx] = QmReal; _q6Since[idx] = _q6Last[idx] = now; _q6Reached[idx] = false; _q6Loss0[idx] = _q6Loss[idx]; _q6Ext[idx] = false;
            if (turned) { _q6Turned++; _q6InTurned++; }
            _q6BeforeSum += Math.Max(0, _q6Loss[idx] - lossOld);
            _q6In++;
            if (target == ArrangementOrder.ArrangementOrderEnum.Square) _q6InSq++; else _q6InCi++;
            if (side == 2) _q6InRear++; else _q6InFlank++;
        }

        /// <summary>Epizod w toku (prawdziwy albo na sucho): czas, zagrozenie, wyjscia 5.3.</summary>
        private static void Q446Ep(Team t, Formation f, int idx, float now, float step, bool ok, bool ai)
        {
            bool real = _q6Mode[idx] == QmReal;
            if (real) _q6Sec += step; else _q6DrySec += step;
            _q6Used[idx] += step;
            if (real) { if (_q6Used[idx] > _q6MaxUsed) _q6MaxUsed = _q6Used[idx]; }
            else if (_q6Used[idx] > _q6MaxUsedDry) _q6MaxUsedDry = _q6Used[idx];
            if (!ok)
            {
                // 446-p: szyk przejety przez gracza - powrot do ustawienia sprzed czworoboku (przez _q6Self; prefiks i tak przepuszcza szyk gracza)
                if (!ai) { _q6XPlayer++; Q446End(f, idx, now, 10, real && f != null && f.CountOfUnits > 0); return; }
                _q6XBack++; Q446End(f, idx, now, 9, real); return;
            }
            if (real && !_q6On) { _q6XKey++; Q446End(f, idx, now, 8, true); return; }
            if (real && (int)f.ArrangementOrder.OrderEnum != _q6HoldArr[idx]) { _q6XLost++; Q446End(f, idx, now, 7, false); return; }
            // 446-p: szarza / odwrot - wyjscie od razu (bez najkrotszego czasu 6 s)
            var ot = f.GetReadonlyMovementOrderReference().OrderType;
            if (ot == OrderType.Charge || ot == OrderType.ChargeWithTarget) { _q6XCharge++; Q446End(f, idx, now, 2, real); return; }
            if (ot == OrderType.Retreat || ot == OrderType.FallBack) { _q6XRetreat++; Q446End(f, idx, now, 1, real); return; }
            Vec2 me = f.CachedMedianPosition.AsVec2;
            if (!me.IsValid) return;
            Vec2 dir = f.Direction;
            if (dir.IsValid && dir.LengthSquared >= 0.0001f) dir = dir.Normalized(); else dir = Vec2.Invalid;
            float near;
            bool threat = Q446AnyThreat(t, f, me, dir, out near);
            if (threat) _q6Last[idx] = now;
            if (near <= Q446Reach && !_q6Reached[idx]) { _q6Reached[idx] = true; _q6ReachN++; }
            bool contact = near <= Q446Near;
            // 446-p: piechota wroga w 25 m (bez jazdy w 25 m) - wyjscie od razu
            if (!contact && FootGap(t, f) <= S1ContactGap) { _q6XFoot++; Q446End(f, idx, now, 1, real); return; }
            float dur = now - _q6Since[idx];
            if (dur < Q446MinHold) return;
            bool overB = _q6Used[idx] >= Q446Budget, overT = dur >= Q446Max;
            if (overB || overT)
            {
                // 446-p: jazda wroga w 25 m - czworobok trwa do "zagrozenie minelo" (twardy sufit 60 s epizod / 120 s na szyk)
                if (contact && dur < Q446MaxHeld && _q6Used[idx] < Q446BudgetHeld)
                {
                    if (!_q6Ext[idx]) { _q6Ext[idx] = true; _q6Held++; }
                }
                else
                {
                    if (overB) _q6XBudget++; else _q6XMax++;
                    Q446End(f, idx, now, 1, real);
                    return;
                }
            }
            if (now - _q6Last[idx] >= Q446Calm) { _q6XCalm++; Q446End(f, idx, now, 1, real); }
        }

        /// <summary>Koniec epizodu: restore - powrot do ustawienia sprzed czworoboku (szyk w naszym czworoboku), a gdy w trakcie gra / RBM chcialy inne
        /// (odrzucone przez blokade) - do ostatniego odrzuconego. why: 0 potkniecie, 1 regula 5.3, 2 szarza (ShieldWall -> Line jak aktywacja BehaviorCharge),
        /// 7 ustawienie zmienione inna droga, 8 klucz wylaczony, 9 szyk pusty / nie piechota, 10 szyk przejety przez gracza (tylko stan sprzed).</summary>
        private static void Q446End(Formation f, int idx, float now, int why, bool restore)
        {
            byte md = _q6Mode[idx];
            if (md == QmReal)
            {
                _q6EpEnd++;
                _q6DuringSum += Math.Max(0, _q6Loss[idx] - _q6Loss0[idx]);
                int hold = _q6HoldArr[idx];
                if (restore && f != null && (int)f.ArrangementOrder.OrderEnum == hold)
                {
                    int back = _q6Before[idx];
                    int want = Volatile.Read(ref _q6Want[idx]);
                    if (why != 10 && want >= 0 && want != hold && want != back) { back = want; _q6WantUsed++; }
                    if (why == 2 && back == (int)ArrangementOrder.ArrangementOrderEnum.ShieldWall) { back = (int)ArrangementOrder.ArrangementOrderEnum.Line; _q6ChargeLine++; }
                    if (back != hold)
                    {
                        _q6Self = true;
                        try { f.SetArrangementOrder(Q446Of((ArrangementOrder.ArrangementOrderEnum)back)); }
                        finally { _q6Self = false; }
                        // 446-p: CloseRanks 146 / 310 - powrot z czworoboku to nie zmiana "inna droga"
                        CloseRanks.Q446Restored(f, f.ArrangementOrder.OrderEnum);
                    }
                }
            }
            else if (md == QmDry)
            {
                _q6DryEnd++;
                _q6DryDuringSum += Math.Max(0, _q6Loss[idx] - _q6Loss0[idx]);
            }
            Volatile.Write(ref _q6Hold[idx], null);
            _q6HoldArr[idx] = -1;
            Volatile.Write(ref _q6Want[idx], -1);
            _q6Mode[idx] = QmNone;
            _q6Reached[idx] = false;
            _q6Ext[idx] = false;
            _q6Cool[idx] = now + Q446Cool;
        }

        private static ArrangementOrder Q446Of(ArrangementOrder.ArrangementOrderEnum e)
        {
            switch (e)
            {
                case ArrangementOrder.ArrangementOrderEnum.Circle: return ArrangementOrder.ArrangementOrderCircle;
                case ArrangementOrder.ArrangementOrderEnum.Column: return ArrangementOrder.ArrangementOrderColumn;
                case ArrangementOrder.ArrangementOrderEnum.Loose: return ArrangementOrder.ArrangementOrderLoose;
                case ArrangementOrder.ArrangementOrderEnum.Scatter: return ArrangementOrder.ArrangementOrderScatter;
                case ArrangementOrder.ArrangementOrderEnum.ShieldWall: return ArrangementOrder.ArrangementOrderShieldWall;
                case ArrangementOrder.ArrangementOrderEnum.Skein: return ArrangementOrder.ArrangementOrderSkein;
                case ArrangementOrder.ArrangementOrderEnum.Square: return ArrangementOrder.ArrangementOrderSquare;
                default: return ArrangementOrder.ArrangementOrderLine;
            }
        }

        /// <summary>Jazda wroga (>= 5 i >= 10% naszego szyku, nie konni strzelcy) jedzie na szyk: dojazd <= 10 s (> 2 m/s, cos > 0.5) albo w 25 m i zbliza sie.
        /// Zwraca odstep najblizszej takiej z boku / tylu (foe, side 1 bok / 2 tyl); front = jest taka tylko od czola (od poczatku). 446-p: gdy czolo obrocil
        /// w ostatnich 4 s RBM (FacingOrder na jazde - kat kierunkow > 45 st.), a wobec kierunku sprzed obrotu jazda jest z boku / tylu - liczy sie bok / tyl
        /// (turned).</summary>
        private static float Q446Threat(Team t, Formation f, Vec2 me, Vec2 dir, Vec2 old, int cnt, out Formation foe, out byte side, out bool front, out bool turned)
        {
            foe = null; side = 0; front = false; turned = false;
            float best = float.MaxValue;
            bool oldOk = old.IsValid && old.LengthSquared > 0.5f && dir.x * old.x + dir.y * old.y < Q446TurnCos;
            var teams = t.Mission != null ? t.Mission.Teams : null;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var et = teams[i];
                if (!MissionTeams.Enemies(t, et)) continue;
                var efl = et.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; efl != null && j < efl.Count; j++)
                {
                    var e = efl[j];
                    if (e == null || e.CountOfUnits < R443CavMin || e.CountOfUnits < Q446FoeShare * cnt) continue;
                    var eq = e.QuerySystem;
                    if (eq == null || !eq.IsCavalryFormation || eq.IsRangedCavalryFormation) continue;
                    Vec2 ep = e.CachedAveragePosition;
                    if (!ep.IsValid) continue;
                    float gap;
                    if (!Q446Rides(f, e, me, ep, dir, out gap)) continue;
                    Vec2 from = ep - me;
                    float len = from.Length;
                    float cs = len > 0.1f ? (dir.x * from.x + dir.y * from.y) / len : 1f;
                    bool tr = false;
                    if (cs >= Q446Front)
                    {
                        float co = oldOk && len > 0.1f ? (old.x * from.x + old.y * from.y) / len : 1f;
                        if (co >= Q446Front) { front = true; continue; }
                        cs = co; tr = true;
                    }
                    if (gap >= best) continue;
                    best = gap; foe = e; side = cs <= Q446Rear ? (byte)2 : (byte)1; turned = tr;
                }
            }
            if (foe != null) front = false;
            return best;
        }

        /// <summary>446-p: odstep = odleglosc srodkow minus polowa rozmiaru naszego szyku WZDLUZ kierunku podejscia (|cos| x Depth + |sin| x Width wobec
        /// Direction; bez kierunku - Depth) i polowa glebokosci jazdy.</summary>
        private static bool Q446Rides(Formation f, Formation e, Vec2 me, Vec2 ep, Vec2 dir, out float gap)
        {
            Vec2 to = me - ep;
            float len = to.Length;
            float half = f.Depth;
            if (len > 0.1f && dir.IsValid)
            {
                float c = Math.Abs(dir.x * to.x + dir.y * to.y) / len;
                if (c > 1f) c = 1f;
                float sn = (float)Math.Sqrt(1f - c * c);
                half = c * f.Depth + sn * f.Width;
            }
            gap = Math.Max(0f, len - 0.5f * (half + e.Depth));
            Vec2 v = e.CachedCurrentVelocity;
            float sp = v.IsValid ? v.Length : 0f;
            if (len < 0.1f) return gap <= Q446Near;
            float cos = sp > 0.01f ? (v.x * to.x + v.y * to.y) / (sp * len) : 0f;
            if (gap <= Q446Near) return sp > Q446NearSpeed && cos > Q446NearDot;
            return sp > Q446Speed && cos > Q446Dot && gap / sp <= Q446Eta;
        }

        /// <summary>W epizodzie: jakakolwiek jazda wroga (>= 5, nie konni strzelcy) w 25 m albo jadaca na szyk (dowolna strona). near - najmniejszy odstep.</summary>
        private static bool Q446AnyThreat(Team t, Formation f, Vec2 me, Vec2 dir, out float near)
        {
            near = float.MaxValue;
            bool any = false;
            var teams = t.Mission != null ? t.Mission.Teams : null;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var et = teams[i];
                if (!MissionTeams.Enemies(t, et)) continue;
                var efl = et.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; efl != null && j < efl.Count; j++)
                {
                    var e = efl[j];
                    if (e == null || e.CountOfUnits < R443CavMin) continue;
                    var eq = e.QuerySystem;
                    if (eq == null || !eq.IsCavalryFormation || eq.IsRangedCavalryFormation) continue;
                    Vec2 ep = e.CachedAveragePosition;
                    if (!ep.IsValid) continue;
                    float gap;
                    bool rides = Q446Rides(f, e, me, ep, dir, out gap);
                    if (gap < near) near = gap;
                    if (rides || gap <= Q446Near) any = true;
                }
            }
            return any;
        }

        /// <summary>Nasza jazda (>= 5) w 40 m od jazdy wroga - oslona.</summary>
        private static bool Q446CavNear(Team t, Vec2 ep)
        {
            var fl = t.FormationsIncludingSpecialAndEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                var x = fl[j];
                if (x == null || x.CountOfUnits < R443CavMin) continue;
                var xq = x.QuerySystem;
                if (xq == null || !xq.IsCavalryFormation) continue;
                Vec2 xp = x.CachedAveragePosition;
                if (!xp.IsValid) continue;
                if (xp.Distance(ep) <= Q446CavCover + 0.5f * x.Depth) return true;
            }
            return false;
        }

        // ------------------------------------------------------------ linia konca bitwy (Battle291.Write)

        internal static string Q446Line(string kind)
        {
            var c = Settings.Current;
            var sb = new StringBuilder(1300);
            int bAll = Volatile.Read(ref _q6BWall) + Volatile.Read(ref _q6BLine) + Volatile.Read(ref _q6BLoose) + Volatile.Read(ref _q6BSq) + Volatile.Read(ref _q6BOther);
            sb.Append("Czworobok (446): misja ").Append(kind ?? "-")
              .Append(" | klucz czworobok przed jazda z boku ").Append(c != null && c.InfantrySquareAgainstFlankCharge ? "TAK" : "NIE")
              .Append(", blokada (prefiks ustawienia 344) ").Append(Pendulum333.ArrPatched ? "TAK" : "NIE")
              .Append(" | progi: dojazd <= ").Append(Q446Eta.ToString("0", Q6Inv)).Append(" s albo ").Append(Q446Near.ToString("0", Q6Inv))
              .Append(" m, bok / tyl cos < ").Append(Q446Front.ToString("0.0", Q6Inv)).Append(", szyk >= ").Append(Q446MinMen).Append(" (kolo < ").Append(Q446SquareMen)
              .Append("), epizod ").Append(Q446MinHold.ToString("0", Q6Inv)).Append("-").Append(Q446Max.ToString("0", Q6Inv)).Append(" s, lacznie <= ")
              .Append(Q446Budget.ToString("0", Q6Inv)).Append(" s na szyk")
              .Append(" | wejsc ").Append(_q6In).Append(" (Square ").Append(_q6InSq).Append(", Circle ").Append(_q6InCi).Append("; z boku ").Append(_q6InFlank)
              .Append(", z tylu ").Append(_q6InRear).Append("; czolo obrocone przez RBM w 4 s ").Append(_q6InTurned).Append("), bylby (klucz NIE) ").Append(_q6Dry).Append(", bylby (bez blokady) ").Append(_q6DryNoGuard)
              .Append(", odrzucone przez inny prefiks ").Append(_q6Refused).Append("; wejscia i bylby z czolem obroconym ").Append(_q6Turned)
              .Append(" | nie (szyko-zdarzenia, zapadka ").Append(Q446Cool.ToString("0", Q6Inv)).Append(" s): tylko z czola (od poczatku) ").Append(_q6Front)
              .Append(", oslona piechota ").Append(_q6CovInf).Append(", oslona jazda ").Append(_q6CovCav).Append(", w zwarciu ").Append(_q6Busy)
              .Append(", piechota wroga blisko ").Append(_q6FootNear).Append(", w szarzy ").Append(_q6Charge).Append(", w odwrocie ").Append(_q6Retreat).Append(", przerwa ").Append(_q6CoolN)
              .Append(", limit laczny ").Append(_q6Cap)
              .Append(" | czas w czworoboku ").Append(_q6Sec.ToString("0", Q6Inv)).Append(" szyko-s (srednio ")
              .Append(_q6In > 0 ? (_q6Sec / _q6In).ToString("0.0", Q6Inv) + " s" : "-").Append(" na epizod), bylby ")
              .Append(_q6DrySec.ToString("0", Q6Inv)).Append(" szyko-s; maks na szyk ").Append(_q6MaxUsed.ToString("0", Q6Inv)).Append(" s (bylby ").Append(_q6MaxUsedDry.ToString("0", Q6Inv)).Append(" s); naraz maks ").Append(_q6MaxNow)
              .Append(" | wyjscia: zagrozenie minelo ").Append(_q6XCalm).Append(", ").Append(Q446Max.ToString("0", Q6Inv)).Append(" s ").Append(_q6XMax)
              .Append(", limit laczny ").Append(_q6XBudget).Append(" (przedluzone przy jezdzie w 25 m ").Append(_q6Held).Append(", sufit ").Append(Q446MaxHeld.ToString("0", Q6Inv))
              .Append(" / ").Append(Q446BudgetHeld.ToString("0", Q6Inv)).Append(" s), piechota wroga ").Append(_q6XFoot).Append(", szarza ").Append(_q6XCharge)
              .Append(" (ShieldWall -> Line ").Append(_q6ChargeLine).Append("), odwrot ").Append(_q6XRetreat)
              .Append(", ustawienie zmienione inna droga ").Append(_q6XLost).Append(", szyk przejety przez gracza ").Append(_q6XPlayer).Append(", szyk pusty / nie piechota ").Append(_q6XBack).Append(", klucz ").Append(_q6XKey)
              .Append(", w toku przy koncu ").Append(_q6Open).Append("; powrot do odrzuconego w trakcie ").Append(_q6WantUsed).Append("; jazda dojechala (<= ").Append(Q446Reach.ToString("0", Q6Inv)).Append(" m) ").Append(_q6ReachN)
              .Append(" | proby nadpisania odrzucone ").Append(bAll).Append(" (ShieldWall - RBM przeciw konnicy / decyzja ").Append(Volatile.Read(ref _q6BWall))
              .Append(", Line ").Append(Volatile.Read(ref _q6BLine)).Append(", Loose ").Append(Volatile.Read(ref _q6BLoose))
              .Append(", Square / Circle ").Append(Volatile.Read(ref _q6BSq)).Append(", inne ").Append(Volatile.Read(ref _q6BOther))
              .Append("; watek silnika ").Append(Volatile.Read(ref _q6BAi)).Append(", glowny ").Append(Volatile.Read(ref _q6BMain))
              .Append("; wlasne przepuszczone ").Append(Volatile.Read(ref _q6SelfN)).Append(')')
              .Append(" | straty piechoty AI od ciosow jezdzcow: w czworoboku ").Append(_q6LossReal).Append(", w epizodzie bylby ").Append(_q6LossDry)
              .Append(", poza ").Append(_q6LossOut)
              .Append("; na 10 szyko-s: w czworoboku ").Append(_q6Sec > 0.5f ? (_q6LossReal * 10f / _q6Sec).ToString("0.00", Q6Inv) : "-")
              .Append(", bylby ").Append(_q6DrySec > 0.5f ? (_q6LossDry * 10f / _q6DrySec).ToString("0.00", Q6Inv) : "-")
              .Append("; kontrola - 10 s przed wejsciem srednio ").Append(_q6In > 0 ? (_q6BeforeSum / _q6In).ToString("0.0", Q6Inv) : "-")
              .Append(", w czworoboku srednio ").Append(_q6EpEnd > 0 ? (_q6DuringSum / _q6EpEnd).ToString("0.0", Q6Inv) : "-")
              .Append("; bylby: przed ").Append((_q6Dry + _q6DryNoGuard) > 0 ? (_q6DryBeforeSum / (_q6Dry + _q6DryNoGuard)).ToString("0.0", Q6Inv) : "-")
              .Append(", w trakcie ").Append(_q6DryEnd > 0 ? (_q6DryDuringSum / _q6DryEnd).ToString("0.0", Q6Inv) : "-")
              .Append(" | potkniecia ").Append(_q6Stum).Append('.');
            return sb.ToString();
        }
    }
}
