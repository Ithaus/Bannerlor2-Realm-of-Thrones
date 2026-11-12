using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 460 = PAKIET 8 krok 8.4 audytu AI bitwy (docs/audyty-2026-11-11/AUDYT-AI-BITWA.md rozdz. 3 "PAKIET 8", warunek wstepny "po calym pakiecie 4" - spelniony
    /// 441-447; Jeff 11.11 "rob wszystko po kolei tak jak radzisz"). Pozorowany odwrot / przyneta jazdy AI (Hastings): piechota wroga, ktora szarzuje na nasza
    /// jazde, jest wciagana w pogon pod nasza piechote, a jazda zawraca i bije scigajacych, ktorzy oderwali sie od swoich. Nie nowe zachowanie - rola FEINT
    /// w szkielecie 441 (BehaviorCavRole441, galop - wyjatek klusa 441), rozkazy Move i ChargeToTarget jak 444 / 445. Klucz CavalryFeignedRetreat ("Cavalry
    /// Feigned Retreat", domyslnie TAK), wymaga dowodcy jazdy 441. Zadnych bonusow do liczb - tylko decyzje. Bitwa polowa, nie morska. Glowny watek
    /// (Doctrine.Tick, wlasny zegar 1 s, po 443 / 445 / 444 / 447 - bierze tylko szyk w roli NONE, pilniejsze role maja pierwszenstwo; przed 442 - slot celu).
    ///
    /// Szyk: jazda AI (IsCavalryFormation, nie konni strzelcy, >= 5, IsAIControlled, nie szyk gracza / z czlowiekiem gracza), druzyna TeamAIGeneral obu stron,
    ///   rola 441 NONE, poza zwarciem (< 30% wrecz, miara 442), 10 s po poprzedniej roli 460, 45 s po poprzedniej przynecie, najwyzej 2 przynety na szyk w bitwie,
    ///   zakaz po stracie 30% ludzi od pierwszej przynety (bez wahadla).
    /// Scigajacy (przyneta chwycila): szyk wroga pieszej piechoty (nie strzelcy, nie jazda, >= 8), poza zwarciem (< 30%), z rozkazem szarzy na nas (ChargeToTarget
    ///   na nasz szyk albo Charge z nami jako najblizszym szykiem wroga), idzie na nas (skladowa predkosci ku nam >= 1.5 m/s), odstep 15-80 m (srodki minus pol
    ///   glebokosci) i liczniejszy niz x0.5 / x0.8 / x1.2 naszego szyku (OSTROZNY / ZWYKLY / PORYWCZY, profil 440; klucz Commander Personality In Battle NIE -
    ///   ZWYKLY): czolowa szarza w zwarta piechote kosztuje (Courtrai), wiec jazda udaje ucieczke; slabszych RBM bije wprost jak dotad. Najblizszy.
    /// Warunki bezpieczenstwa (wzor 444 / 445): jazda wroga albo konni strzelcy wroga (>= 5, nie w WAIT 444) w 80 m od nas - bez przynety (dogonia / strzelaja
    ///   w plecy); glowna piechota druzyny (R443MainInf) za nami (po stronie odwrotnej do scigajacych) i w 250 m - inaczej odwrot nie prowadzi do swoich.
    /// ODWROT (rola FEINT, Move galopem): punkt 70 m od nas w strone od scigajacych, obrot 0 / +-35 / +-60 stopni; z wazonych punktow najblizszy naszej glownej
    ///   piechocie (przyneta prowadzi pod swoich); w granicach pola (>= 15 m od krawedzi), na siatce nawigacji, nie przy innym szyku wroga (V445FoeClear), droga
    ///   nie przez nasze szyki (pol wiekszego wymiaru + 8 m). Koniec odwrotu: >= 70% jezdzcow (na koniu przy starcie) w 25 m od punktu albo 25 s.
    ///   Wtedy ZAWROT, gdy scigajacy dalej idzie (skladowa ku nam >= 0.5 m/s) albo jest w 40 m, odstep <= 110 m, poza zwarciem, droga do niego niezaslonieta
    ///   piechota wroga (T442Blocked) i bez jazdy wroga w 80 m; inaczej przyneta nie chwycila - oddanie RBM.
    ///   Przerwanie odwrotu (oddanie RBM): zwarcie (>= 30% wrecz - dogonieni), jazda wroga w 80 m, scigajacy zniknal / < 8 / nie wrog, strata 25% (zabici i ranni).
    /// ZAWROT (rola FEINT, ChargeToTarget(scigajacy), slot 442 - F460AimTarget w T442Form, postfiks GetCorrectTarget trzyma jezdzcow na celu jak 443 / 444 / 445):
    ///   koniec 10 s po zwarciu (>= 20% wrecz - dalej walczy RBM), 20 s bez zwarcia, scigajacy rozbity (zabici i ranni >= 50% od zawrotu albo < 8), nasza strata
    ///   40% od startu, klucz NIE. Po kazdym koncu 10 s bez nowej roli 460, 45 s bez nowej przynety.
    /// Pomiar (glowny watek): oderwanie scigajacego od jego najblizszego innego szyku (odstep srodkow minus pol szerokosci, m) i rozproszenie (srednia odleglosc
    ///   ludzi od mediany, m) przy starcie i przy zawrocie - miara "czy przyneta rozciagnela pogon"; wynik zawrotu: zabici i ranni scigajacych przez naszych
    ///   jezdzcow i ogolem, nasza strata w odwrocie i w zawrocie. Linia "Pozorowany odwrot (460)" w Battle291.Write.
    /// Klucz NIE (albo bez dowodcy jazdy 441): decyzja liczona (tylko odczyt), rola nie nadawana - co do znaku jak dotad; licznik "bylby". Klucz NIE w trakcie -
    ///   rola oddana od razu.
    /// Jeden wlasciciel: FEINT jest rola twarda (C441Hard / C441Owned) - 148 / 204 / 283 / 311a / 335 / 344 nie biora jezdzcow; 443 nie zaczyna rajdu / schowania
    ///   (F460Busy, jak 444 STRIKE / 445 / 447); 444 / 445 / 447 biora tylko NONE (445 takze WAIT) - FEINT nietkniety.
    /// Watki: wszystko w glownym watku (ApplyActionOnEachUnit ze statycznymi delegatami, WorldPosition.SetVec2 / GetNavMesh); zachowanie roli 441 czyta tylko
    ///   rozkaz. MovementOrder tylko metodami MovementOrderMove / MovementOrderChargeToTarget i default (pulapka 12.11 - bez pol statycznych). Wyjatki per szyk
    ///   liczone (potkniecia, rola oddana, slot od zera), bez globalnego wylacznika.
    /// </summary>
    internal static partial class Doctrine
    {
        internal const float F460Every = 1f, F460GapMin = 15f, F460GapMax = 80f, F460Appr = 1.5f, F460StillAppr = 0.5f, F460CavNear = 80f, F460InfRange = 250f;
        internal const float F460Back = 70f, F460Edge = 15f, F460OwnPad = 8f, F460Reach = 25f, F460ReachShare = 0.7f, F460Ride = 25f, F460TurnNear = 40f;
        internal const float F460TurnFar = 110f, F460RetLoss = 0.25f, F460Loss = 0.4f, F460Broken = 0.5f, F460Contact = 0.2f, F460AfterContact = 10f;
        internal const float F460NoContact = 20f, F460Reenter = 10f, F460Cool = 45f, F460Spent = 0.3f, F460Latch = 30f;
        internal const int F460MaxN = 2;
        // rotacje punktu odwrotu (stopnie): 0, +35, -35, +60, -60
        private static readonly float[] FeTurn = { 0f, 0.61f, -0.61f, 1.047f, -1.047f };
        // progi profilu wodza 440 (0 OSTROZNY, 1 ZWYKLY, 2 PORYWCZY): scigajacy >= x naszego szyku
        private static readonly float[] FeRatio = { 0.5f, 0.8f, 1.2f };
        private const byte FmNone = 0, FmRet = 1, FmTurn = 2;
        // zatrzaski licznikow "co 30 s" (slot x FL)
        private const int FL = 7, FlDry = 0, FlWeak = 1, FlInf = 2, FlSpot = 3, FlCav = 4, FlCap = 5, FlRef = 6;
        private static readonly IFormatProvider FeInv = CultureInfo.InvariantCulture;

        // ---- stan slotow [TeamIndex x 10 + FormationIndex] (tylko glowny watek; F460AimTarget czyta T442Form, F460Busy R443Cav - tez glowny)
        private static bool _feLive, _feKey;
        private static float _feAcc;
        private static readonly Formation[] _feWho = new Formation[CrSlots];
        private static readonly byte[] _feMode = new byte[CrSlots];
        private static readonly Formation[] _feTgt = new Formation[CrSlots];
        private static readonly Vec2[] _feP = new Vec2[CrSlots];
        private static readonly float[] _feSince = new float[CrSlots], _feTurnAt = new float[CrSlots], _feContactAt = new float[CrSlots];
        private static readonly float[] _feExit = new float[CrSlots], _feNext = new float[CrSlots];
        private static readonly float[] _feLatch = new float[CrSlots * FL];
        private static readonly int[] _feMen0 = new int[CrSlots], _feRiders0 = new int[CrSlots], _feTgtMen0 = new int[CrSlots], _feTgtMenT = new int[CrSlots];
        private static readonly int[] _feOwnKill = new int[CrSlots], _feOwnKillT = new int[CrSlots], _feTgtKill = new int[CrSlots], _feTgtByUs = new int[CrSlots];
        private static readonly int[] _feN = new int[CrSlots], _feBase = new int[CrSlots];
        private static readonly bool[] _feBusySeen = new bool[CrSlots];
        private static readonly Dictionary<Formation, float> _feMelee = new Dictionary<Formation, float>();

        // ---- liczniki misji (F460Reset w Battle291.Begin)
        private static int _feStart, _feDry, _feWeak, _feNoInf, _feNoSpot, _feCavNear, _feCap, _feSpentN, _feRefused, _feBusy443, _feStum;
        private static int _feReached, _feRideOut, _feTurnN, _feNoBite, _feCaught, _feRetCav, _feRetGone, _feRetLoss, _feKeyOff, _feBack, _feOpen;
        private static int _feContact, _feAfterC, _feNoContact, _feBroken, _feCutLoss, _feTurnDone, _feMaxN;
        private static float _feRetSec, _feTurnSec;
        private static double _feSepStart, _feSepTurn, _feSpreadStart, _feSpreadTurn, _feToContact, _feTgtLossSum, _feTgtByUsSum, _feOwnRetSum, _feOwnTurnSum;
        private static int _feSepN, _feToContactN, _feRetDone;
        // pomiar przy starcie (na slot) - do sredniej przy zawrocie (ta sama populacja)
        private static readonly float[] _feSep0 = new float[CrSlots], _feSpread0 = new float[CrSlots];

        /// <summary>Battle291.Begin (AfterStart): liczniki linii 460 od zera.</summary>
        internal static void F460Reset()
        {
            _feStart = _feDry = _feWeak = _feNoInf = _feNoSpot = _feCavNear = _feCap = _feSpentN = _feRefused = _feBusy443 = _feStum = 0;
            _feReached = _feRideOut = _feTurnN = _feNoBite = _feCaught = _feRetCav = _feRetGone = _feRetLoss = _feKeyOff = _feBack = _feOpen = 0;
            _feContact = _feAfterC = _feNoContact = _feBroken = _feCutLoss = _feTurnDone = _feMaxN = 0;
            _feRetSec = _feTurnSec = 0f;
            _feSepStart = _feSepTurn = _feSpreadStart = _feSpreadTurn = _feToContact = _feTgtLossSum = _feTgtByUsSum = _feOwnRetSum = _feOwnTurnSum = 0.0;
            _feSepN = _feToContactN = _feRetDone = 0;
        }

        /// <summary>Doctrine.Begin (AfterStart, po Clear): bitwa polowa, nie morska - pomiar zawsze, decyzje wedlug klucza (czytany co takt).</summary>
        private static void F460Begin(Mission m)
        {
            bool field = false;
            try { field = m != null && m.IsFieldBattle && !m.IsNavalBattle; } catch { field = false; }
            _feAcc = 0f;
            _feLive = field;
        }

        /// <summary>Doctrine.Clear (Begin / End): stan slotow od zera (role oddaje C441ClearMission). Przynety w toku przy koncu - licznik "w toku".</summary>
        private static void F460ClearMission()
        {
            _feLive = false;
            for (int i = 0; i < CrSlots; i++)
            {
                if (_feMode[i] != FmNone) _feOpen++;
                F460SlotClear(i);
                F460SlotNew(i);
                _feWho[i] = null;
            }
            _feMelee.Clear();
            _feAcc = 0f;
        }

        private static void F460SlotClear(int i)
        {
            _feMode[i] = FmNone; _feTgt[i] = null; _feP[i] = Vec2.Invalid;
            _feSince[i] = 0f; _feTurnAt[i] = _feContactAt[i] = -1f;
            _feMen0[i] = _feRiders0[i] = _feTgtMen0[i] = _feTgtMenT[i] = 0;
            _feOwnKill[i] = _feOwnKillT[i] = _feTgtKill[i] = _feTgtByUs[i] = 0;
            _feSep0[i] = _feSpread0[i] = 0f;
            _feBusySeen[i] = false;
        }

        /// <summary>Stan szyku na cala bitwe (nowy szyk w slocie / koniec misji): przerwy, limit, zatrzaski.</summary>
        private static void F460SlotNew(int i)
        {
            _feExit[i] = _feNext[i] = -1000f; _feN[i] = 0; _feBase[i] = 0;
            for (int k = 0; k < FL; k++) _feLatch[i * FL + k] = -1000f;
        }

        private static bool F460LatchOk(int idx, int k, float now)
        {
            int i = idx * FL + k;
            if (now < _feLatch[i]) return false;
            _feLatch[i] = now + F460Latch;
            return true;
        }

        // ------------------------------------------------------------ odczyty dla 442 / 443 (glowny watek)

        /// <summary>T442Form: szyk w roli FEINT w fazie zawrotu - scigajacy (slot 442 jezdzcow); inaczej null.</summary>
        internal static Formation F460AimTarget(Formation f, int idx)
        {
            if (!_feLive || f == null || idx < 0 || idx >= CrSlots) return null;
            if (_feMode[idx] != FmTurn || !ReferenceEquals(_feWho[idx], f)) return null;
            var tg = _feTgt[idx];
            return tg != null && tg.CountOfUnits > 0 ? tg : null;
        }

        /// <summary>R443Cav: szyk w roli FEINT - 443 nie zaczyna rajdu / schowania (rola 460 nie nadpisywana). Licznik raz na przynete.</summary>
        internal static bool F460Busy(Formation f)
        {
            if (C441RoleOf(f) != CrFeint) return false;
            int idx = C441Idx(f);
            if (idx >= 0 && idx < CrSlots && !_feBusySeen[idx]) { _feBusySeen[idx] = true; _feBusy443++; }
            return true;
        }

        /// <summary>BattleSense.OnAgentRemoved (glowny watek, szyk ofiary jeszcze przypisany): zabici / ranni w szyku FEINT (odwrot / zawrot osobno)
        /// i w scigajacym po zawrocie (ogolem i przez jezdzcow naszego szyku).</summary>
        internal static void F460OnRemoved(Agent v, Agent by, AgentState st)
        {
            if (!_feLive || v == null) return;
            try
            {
                if (st != AgentState.Killed && st != AgentState.Unconscious) return;
                if (!v.IsHuman) return;
                var vf = v.Formation;
                if (vf == null) return;
                int vi = C441Idx(vf);
                if (vi >= 0 && vi < CrSlots && _feMode[vi] != FmNone && ReferenceEquals(_feWho[vi], vf))
                {
                    if (_feMode[vi] == FmRet) _feOwnKill[vi]++; else _feOwnKillT[vi]++;
                }
                var bf = by != null ? by.Formation : null;
                for (int i = 0; i < CrSlots; i++)
                {
                    if (_feMode[i] != FmTurn || !ReferenceEquals(_feTgt[i], vf)) continue;
                    _feTgtKill[i]++;
                    if (bf != null && ReferenceEquals(bf, _feWho[i])) _feTgtByUs[i]++;
                }
            }
            catch (Exception e) { _feStum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Feint460 zabity", e); }
        }

        // ------------------------------------------------------------ takt glownego watku (Doctrine.Tick)

        internal static void F460Tick(Mission m, float dt)
        {
            if (!_feLive || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m) || m.MissionEnded) return;
            _feAcc += dt;
            if (_feAcc < F460Every) return;
            float step = _feAcc;
            _feAcc = 0f;
            bool dep;
            try { dep = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && m.CurrentTime >= P440NoDeploy); } catch { dep = false; }
            if (!dep) return;
            var c = Settings.Current;
            if (c == null) return;
            _feKey = c.CavalryFeignedRetreat;
            float now = m.CurrentTime;
            _t4Now = now;   // T442MeleeShare (glowny watek)
            _feMelee.Clear();
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
                    try { F460Form(m, t, f, idx, now, step); }
                    catch (Exception e)
                    {
                        // potkniecie szyku: rola oddana (tylko nasza), slot od zera - bez globalnego wylacznika
                        _feStum++;
                        try { if (C441RoleOf(f) == CrFeint) C441SetRole(f, CrNone, default(MovementOrder)); } catch { }
                        F460SlotClear(idx);
                        _feExit[idx] = now;
                        RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Feint460 szyk", e);
                    }
                }
            }
            // ile szykow naraz w roli (martwy kod ozywiony - zasada 4)
            int nm = 0;
            for (int i = 0; i < CrSlots; i++) if (_feMode[i] != FmNone) nm++;
            if (nm > _feMaxN) _feMaxN = nm;
        }

        private static void F460Form(Mission m, Team t, Formation f, int idx, float now, float step)
        {
            if (!ReferenceEquals(_feWho[idx], f))
            {
                F460SlotClear(idx);
                F460SlotNew(idx);
                _feWho[idx] = f;
            }
            var q = f.QuerySystem;
            bool cav = q != null && q.IsCavalryFormation && !q.IsRangedCavalryFormation;
            bool mine = f.CountOfUnits >= R443CavMin && f.IsAIControlled && !HorsePursuit.PlayerForm(f) && !f.HasPlayerControlledTroop;
            byte mode = _feMode[idx];
            if (!cav || !mine)
            {
                if (mode != FmNone) { _feBack++; F460End(f, idx, now); }
                return;
            }
            byte role = C441RoleOf(f);
            if (mode != FmNone && role != CrFeint)
            {
                // rola zmieniona przez kogos innego (441: gracz / szyk pusty) - stan 460 od zera, roli nie ruszamy
                _feBack++;
                F460Score(f, idx);
                F460SlotClear(idx);
                _feExit[idx] = now;
                _feNext[idx] = now + F460Cool;
                return;
            }
            Vec2 me = f.CachedAveragePosition;
            if (!me.IsValid) return;
            if (mode == FmRet)
            {
                _feRetSec += step;
                if (!_feKey || !C441Live) { _feKeyOff++; F460End(f, idx, now); return; }
                F460RetTick(t, f, idx, me, now);
                return;
            }
            if (mode == FmTurn)
            {
                _feTurnSec += step;
                if (!_feKey || !C441Live) { _feKeyOff++; F460End(f, idx, now); return; }
                F460TurnTick(t, f, idx, now);
                return;
            }
            if (role != CrNone) return;   // szyk w innej roli (443 / 444 / 445 / 447) - nie nasz
            if (now - _feExit[idx] < F460Reenter || now < _feNext[idx]) return;
            if (F460MeleeOf(f) >= T442OwnMelee) return;
            sbyte prof = _cpKey ? P440Prof(t) : (sbyte)1;
            if (prof < 0 || prof > 2) prof = 1;
            bool weak;
            Formation e = F460Pursuer(t, f, me, prof, out weak);
            if (e == null)
            {
                if (weak && F460LatchOk(idx, FlWeak, now)) _feWeak++;
                return;
            }
            // od tej chwili przyneta "chwycila" - powody odmowy liczone (zatrzask 30 s na szyk)
            if (_feN[idx] >= F460MaxN) { if (F460LatchOk(idx, FlCap, now)) _feCap++; return; }
            if (_feBase[idx] > 0 && f.CountOfUnits < (1f - F460Spent) * _feBase[idx]) { if (F460LatchOk(idx, FlCap, now)) _feSpentN++; return; }
            if (F460CavNearBy(t, me)) { if (F460LatchOk(idx, FlCav, now)) _feCavNear++; return; }
            Vec2 ep = e.CachedMedianPosition.AsVec2;
            Vec2 away = me - ep;
            if (!ep.IsValid || away.LengthSquared < 1f) return;
            away = away.Normalized();
            var inf = R443MainInf(t, f);
            Vec2 ip = inf != null ? inf.CachedMedianPosition.AsVec2 : Vec2.Invalid;
            if (inf == null || !ip.IsValid || me.Distance(ip) > F460InfRange || (ip - me).DotProduct(away) <= 0f)
            {
                if (F460LatchOk(idx, FlInf, now)) _feNoInf++;
                return;
            }
            Vec2 p;
            WorldPosition wp;
            if (!F460Point(m, t, f, e, me, away, ip, out p, out wp)) { if (F460LatchOk(idx, FlSpot, now)) _feNoSpot++; return; }
            if (!_feKey || !C441Live) { if (F460LatchOk(idx, FlDry, now)) _feDry++; return; }
            int riders = F460Riders(f);
            if (!C441SetRole(f, CrFeint, MovementOrder.MovementOrderMove(wp))) { if (F460LatchOk(idx, FlRef, now)) _feRefused++; return; }
            _feMode[idx] = FmRet; _feTgt[idx] = e; _feP[idx] = p; _feSince[idx] = now; _feTurnAt[idx] = _feContactAt[idx] = -1f;
            _feMen0[idx] = f.CountOfUnits; _feRiders0[idx] = Math.Max(1, riders); _feTgtMen0[idx] = e.CountOfUnits; _feTgtMenT[idx] = 0;
            _feOwnKill[idx] = _feOwnKillT[idx] = _feTgtKill[idx] = _feTgtByUs[idx] = 0;
            _feSep0[idx] = F460Sep(e); _feSpread0[idx] = F460Spread(e);
            if (_feBase[idx] <= 0) _feBase[idx] = f.CountOfUnits;
            _feN[idx]++;
            _feStart++;
        }

        // ------------------------------------------------------------ odwrot i zawrot

        private static void F460RetTick(Team t, Formation f, int idx, Vec2 me, float now)
        {
            var e = _feTgt[idx];
            if (e == null || e.CountOfUnits < T442MinMen || e.Team == null || !MissionTeams.Enemies(t, e.Team)) { _feRetGone++; F460End(f, idx, now); return; }
            if (F460MeleeOf(f) >= T442OwnMelee) { _feCaught++; F460End(f, idx, now); return; }
            if (_feOwnKill[idx] >= F460RetLoss * Math.Max(1, _feMen0[idx])) { _feRetLoss++; F460End(f, idx, now); return; }
            if (F460CavNearBy(t, me)) { _feRetCav++; F460End(f, idx, now); return; }
            int need = Math.Max(1, (int)Math.Ceiling(F460ReachShare * _feRiders0[idx]));
            if (_feP[idx].IsValid && F460AtPoint(f, _feP[idx], F460Reach) >= need) _feReached++;
            else if (now - _feSince[idx] >= F460Ride) _feRideOut++;
            else return;
            // koniec odwrotu: zawrot, gdy pogon trwa; inaczej przyneta nie chwycila
            Vec2 ep = e.CachedMedianPosition.AsVec2;
            float gap = ep.IsValid ? Math.Max(0f, me.Distance(ep) - 0.5f * (f.Depth + e.Depth)) : float.MaxValue;
            float appr = ep.IsValid ? F460Approach(e, ep, me) : 0f;
            bool chase = (appr >= F460StillAppr || gap <= F460TurnNear) && gap <= F460TurnFar;
            if (!chase || F460MeleeOf(e) >= T442OwnMelee || T442Blocked(t, e, me, ep))
            {
                _feNoBite++;
                F460End(f, idx, now);
                return;
            }
            if (!C441SetRole(f, CrFeint, MovementOrder.MovementOrderChargeToTarget(e)))
            {
                _feRefused++;
                F460End(f, idx, now);
                return;
            }
            _feMode[idx] = FmTurn; _feTurnAt[idx] = now; _feTgtMenT[idx] = e.CountOfUnits; _feContactAt[idx] = -1f;
            _feTurnN++;
            _feSepStart += _feSep0[idx]; _feSepTurn += F460Sep(e);
            _feSpreadStart += _feSpread0[idx]; _feSpreadTurn += F460Spread(e);
            _feSepN++;
        }

        private static void F460TurnTick(Team t, Formation f, int idx, float now)
        {
            var e = _feTgt[idx];
            int t0 = Math.Max(1, _feTgtMenT[idx]);
            bool alive = e != null && e.CountOfUnits >= T442MinMen && e.Team != null && MissionTeams.Enemies(t, e.Team);
            if (!alive || _feTgtKill[idx] >= F460Broken * t0) { _feBroken++; F460End(f, idx, now); return; }
            if (_feOwnKill[idx] + _feOwnKillT[idx] >= F460Loss * Math.Max(1, _feMen0[idx])) { _feCutLoss++; F460End(f, idx, now); return; }
            if (_feContactAt[idx] < 0f)
            {
                if (F460MeleeOf(f) >= F460Contact)
                {
                    _feContactAt[idx] = now; _feContact++;
                    _feToContact += now - _feTurnAt[idx]; _feToContactN++;
                    return;
                }
                if (now - _feTurnAt[idx] >= F460NoContact) { _feNoContact++; F460End(f, idx, now); }
                return;
            }
            if (now - _feContactAt[idx] >= F460AfterContact) { _feAfterC++; F460End(f, idx, now); }
        }

        /// <summary>Oddanie roli (tylko gdy rola jest nasza), wynik zawrotu, przerwy.</summary>
        private static void F460End(Formation f, int idx, float now)
        {
            F460Score(f, idx);
            try
            {
                if (f != null && C441RoleOf(f) == CrFeint) C441SetRole(f, CrNone, default(MovementOrder));
            }
            catch (Exception e) { _feStum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Feint460 oddanie", e); }
            F460SlotClear(idx);
            _feExit[idx] = now;
            _feNext[idx] = now + F460Cool;
        }

        /// <summary>Wynik: strata scigajacego od zawrotu (ogolem i przez naszych jezdzcow), nasza strata w odwrocie i w zawrocie (zabici i ranni).</summary>
        private static void F460Score(Formation f, int idx)
        {
            if (_feMode[idx] == FmNone) return;
            int c0 = Math.Max(1, _feMen0[idx]);
            _feOwnRetSum += Math.Min(1.0, (double)_feOwnKill[idx] / c0);
            _feRetDone++;
            if (_feMode[idx] != FmTurn) return;
            int t0 = Math.Max(1, _feTgtMenT[idx]);
            _feTgtLossSum += Math.Min(1.0, (double)_feTgtKill[idx] / t0);
            _feTgtByUsSum += Math.Min(1.0, (double)_feTgtByUs[idx] / t0);
            _feOwnTurnSum += Math.Min(1.0, (double)_feOwnKillT[idx] / c0);
            _feTurnDone++;
        }

        // ------------------------------------------------------------ wybory i geometria (glowny watek)

        /// <summary>Scigajacy: piechota wroga (>= 8, nie strzelcy / jazda) poza zwarciem, z szarza na nasz szyk, idaca na nas, odstep 15-80 m, liczniejsza niz
        /// prog profilu - najblizsza. weak: byl scigajacy, ale za slaby (RBM bije wprost).</summary>
        private static Formation F460Pursuer(Team t, Formation f, Vec2 me, sbyte prof, out bool weak)
        {
            weak = false;
            Formation best = null;
            float bd = float.MaxValue;
            float need = FeRatio[prof] * f.CountOfUnits;
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
                    if (e == null || e.CountOfUnits < T442MinMen) continue;
                    var eq = e.QuerySystem;
                    if (eq == null || !eq.IsInfantryFormation || eq.IsRangedFormation || eq.IsCavalryFormation || eq.IsRangedCavalryFormation) continue;
                    if (!F460ChargesAt(e, f)) continue;
                    Vec2 ep = e.CachedMedianPosition.AsVec2;
                    if (!ep.IsValid) continue;
                    float gap = Math.Max(0f, me.Distance(ep) - 0.5f * (f.Depth + e.Depth));
                    if (gap < F460GapMin || gap > F460GapMax || gap >= bd) continue;
                    if (F460Approach(e, ep, me) < F460Appr) continue;
                    if (F460MeleeOf(e) >= T442OwnMelee) continue;
                    if (e.CountOfUnits < need) { weak = true; continue; }
                    best = e; bd = gap;
                }
            }
            return best;
        }

        /// <summary>Szyk wroga szarzuje na nas: ChargeToTarget na nasz szyk albo Charge z nami jako najblizszym szykiem wroga.</summary>
        private static bool F460ChargesAt(Formation e, Formation f)
        {
            var oe = e.GetReadonlyMovementOrderReference().OrderEnum;
            if (oe == MovementOrder.MovementOrderEnum.ChargeToTarget)
                return ReferenceEquals(e.GetReadonlyMovementOrderReference().TargetFormation, f) || ReferenceEquals(e.TargetFormation, f);
            if (oe != MovementOrder.MovementOrderEnum.Charge) return false;
            var nq = e.CachedClosestEnemyFormation;
            return nq != null && ReferenceEquals(nq.Formation, f);
        }

        /// <summary>Skladowa predkosci szyku e w strone punktu me (m/s).</summary>
        private static float F460Approach(Formation e, Vec2 ep, Vec2 me)
        {
            Vec2 v = e.CachedCurrentVelocity;
            Vec2 d = me - ep;
            if (!v.IsValid || d.LengthSquared < 0.01f) return 0f;
            d = d.Normalized();
            return v.x * d.x + v.y * d.y;
        }

        /// <summary>Jazda albo konni strzelcy wroga (>= 5, nie w WAIT 444) w 80 m od nas.</summary>
        private static bool F460CavNearBy(Team t, Vec2 me)
        {
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
                    if (eq == null || (!eq.IsCavalryFormation && !eq.IsRangedCavalryFormation)) continue;
                    if (C441RoleOf(e) == CrWait) continue;   // jazda wroga czeka przy swojej piechocie (444) - nie zagrozenie
                    Vec2 ep = e.CachedMedianPosition.AsVec2;
                    if (!ep.IsValid) continue;
                    if (ep.Distance(me) - 0.5f * Math.Max(e.Width, e.Depth) < F460CavNear) return true;
                }
            }
            return false;
        }

        /// <summary>Punkt odwrotu: 70 m od nas w strone od scigajacych, obrot 0 / +-35 / +-60 stopni; w granicach pola, na siatce, nie przy innym szyku wroga,
        /// droga nie przez nasze szyki; z waznych najblizszy glownej piechocie.</summary>
        private static bool F460Point(Mission m, Team t, Formation f, Formation e, Vec2 me, Vec2 away, Vec2 ip, out Vec2 p, out WorldPosition wp)
        {
            p = Vec2.Invalid;
            wp = default(WorldPosition);
            float bd = float.MaxValue;
            for (int k = 0; k < FeTurn.Length; k++)
            {
                float a = FeTurn[k];
                float cs = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                Vec2 dir = new Vec2(away.x * cs - away.y * sn, away.x * sn + away.y * cs);
                Vec2 c = me + dir * F460Back;
                if (!m.IsPositionInsideBoundaries(c) || m.GetClosestBoundaryPosition(c).Distance(c) < F460Edge) continue;
                if (!V445FoeClear(t, e, c)) continue;
                if (!F460OwnClear(t, f, me, c)) continue;
                float d = c.Distance(ip);
                if (d >= bd) continue;
                WorldPosition w = f.CachedMedianPosition;
                w.SetVec2(c);
                if (!w.IsValid || w.GetNavMesh() == UIntPtr.Zero) continue;
                p = c; wp = w; bd = d;
            }
            return p.IsValid;
        }

        /// <summary>Droga odwrotu nie przez nasze szyki (>= 5): odleglosc srodka szyku od odcinka > pol wiekszego wymiaru + 8 m.</summary>
        private static bool F460OwnClear(Team t, Formation f, Vec2 a, Vec2 b)
        {
            var fl = t.FormationsIncludingSpecialAndEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                var o = fl[j];
                if (o == null || ReferenceEquals(o, f) || o.CountOfUnits < R443CavMin) continue;
                Vec2 op = o.CachedMedianPosition.AsVec2;
                if (!op.IsValid) continue;
                if (SegDist442(op, a, b) < 0.5f * Math.Max(o.Width, o.Depth) + F460OwnPad) return false;
            }
            return true;
        }

        /// <summary>Pomiar: oderwanie szyku e od jego najblizszego innego szyku (>= 8): odstep srodkow minus pol szerokosci obu (m); brak innego - 0.</summary>
        private static float F460Sep(Formation e)
        {
            var et = e.Team;
            Vec2 ep = e.CachedMedianPosition.AsVec2;
            if (et == null || !ep.IsValid) return 0f;
            float bd = float.MaxValue;
            var fl = et.FormationsIncludingSpecialAndEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                var o = fl[j];
                if (o == null || ReferenceEquals(o, e) || o.CountOfUnits < T442MinMen) continue;
                Vec2 op = o.CachedMedianPosition.AsVec2;
                if (!op.IsValid) continue;
                float d = Math.Max(0f, op.Distance(ep) - 0.5f * (o.Width + e.Width));
                if (d < bd) bd = d;
            }
            return bd < float.MaxValue ? bd : 0f;
        }

        private static float F460MeleeOf(Formation f)
        {
            float v;
            if (_feMelee.TryGetValue(f, out v)) return v;
            v = T442MeleeShare(f);
            _feMelee[f] = v;
            return v;
        }

        // ---- skany ludzi szyku (statyczne delegaty, glowny watek)
        private static Vec2 _feScanP;
        private static float _feScanR2;
        private static int _feScanN;
        private static double _feScanSum;

        private static readonly Action<Agent> F460RiderMan = a =>
        {
            if (a == null || !a.IsActive() || a.MountAgent == null) return;
            _feScanN++;
        };

        private static readonly Action<Agent> F460AtPointMan = a =>
        {
            if (a == null || !a.IsActive() || a.MountAgent == null) return;
            if (a.Position.AsVec2.DistanceSquared(_feScanP) <= _feScanR2) _feScanN++;
        };

        private static readonly Action<Agent> F460SpreadMan = a =>
        {
            if (a == null || !a.IsActive()) return;
            _feScanN++;
            _feScanSum += a.Position.AsVec2.Distance(_feScanP);
        };

        /// <summary>Jezdzcy szyku na koniu.</summary>
        private static int F460Riders(Formation f)
        {
            _feScanN = 0;
            f.ApplyActionOnEachUnit(F460RiderMan);
            return _feScanN;
        }

        /// <summary>Jezdzcy szyku (na koniu) w r od punktu.</summary>
        private static int F460AtPoint(Formation f, Vec2 p, float r)
        {
            if (f == null || !p.IsValid) return 0;
            _feScanP = p; _feScanR2 = r * r; _feScanN = 0;
            f.ApplyActionOnEachUnit(F460AtPointMan);
            return _feScanN;
        }

        /// <summary>Pomiar: rozproszenie szyku - srednia odleglosc ludzi od mediany (m).</summary>
        private static float F460Spread(Formation e)
        {
            Vec2 ep = e.CachedMedianPosition.AsVec2;
            if (!ep.IsValid) return 0f;
            _feScanP = ep; _feScanN = 0; _feScanSum = 0.0;
            e.ApplyActionOnEachUnit(F460SpreadMan);
            return _feScanN > 0 ? (float)(_feScanSum / _feScanN) : 0f;
        }

        // ------------------------------------------------------------ linia konca bitwy (Battle291.Write)

        internal static string F460Line(string kind)
        {
            var c = Settings.Current;
            var sb = new StringBuilder(1200);
            sb.Append("Pozorowany odwrot (460): misja ").Append(kind ?? "-")
              .Append(" | klucze: pozorowany odwrot ").Append(c != null && c.CavalryFeignedRetreat ? "TAK" : "NIE")
              .Append(", dowodca jazdy ").Append(c != null && c.CavalryCommander ? "TAK" : "NIE")
              .Append(", profil wodza ").Append(c != null && c.CommanderPersonalityInBattle ? "TAK" : "NIE")
              .Append(" | prog scigajacych (OSTROZNY / ZWYKLY / PORYWCZY) x").Append(FeRatio[0].ToString("0.0", FeInv)).Append(" / x").Append(FeRatio[1].ToString("0.0", FeInv))
              .Append(" / x").Append(FeRatio[2].ToString("0.0", FeInv)).Append(" naszego szyku")
              .Append(" | przynet ").Append(_feStart).Append(", bylby (klucz NIE / bez dowodcy jazdy) ").Append(_feDry)
              .Append("; odmowy (zatrzask 30 s na szyk): scigajacy za slaby (RBM wprost) ").Append(_feWeak)
              .Append(", bez glownej piechoty za nami ").Append(_feNoInf).Append(", brak punktu (granica / siatka / szyk wroga / nasze szyki) ").Append(_feNoSpot)
              .Append(", jazda wroga w ").Append(F460CavNear.ToString("0", FeInv)).Append(" m ").Append(_feCavNear)
              .Append(", limit ").Append(F460MaxN).Append(" na szyk ").Append(_feCap).Append(", szyk stracil ").Append(K444Pct(F460Spent)).Append(' ').Append(_feSpentN)
              .Append(", rola odrzucona ").Append(_feRefused)
              .Append(" | odwrot ").Append(_feRetSec.ToString("0", FeInv)).Append(" szyko-s: dojechali (>= ").Append(K444Pct(F460ReachShare)).Append(" jezdzcow w ")
              .Append(F460Reach.ToString("0", FeInv)).Append(" m) ").Append(_feReached).Append(", po ").Append(F460Ride.ToString("0", FeInv)).Append(" s ").Append(_feRideOut)
              .Append("; przerwany: dogonieni (zwarcie) ").Append(_feCaught).Append(", jazda wroga blisko ").Append(_feRetCav)
              .Append(", scigajacy zniknal ").Append(_feRetGone).Append(", strata ").Append(K444Pct(F460RetLoss)).Append(' ').Append(_feRetLoss)
              .Append(" | przyneta nie chwycila (pogon stanela / daleko / zwiazana / zasloniety) ").Append(_feNoBite)
              .Append(" | zawrotow ").Append(_feTurnN).Append(" (").Append(_feTurnSec.ToString("0", FeInv)).Append(" szyko-s): zwarc ").Append(_feContact)
              .Append(" (srednio po ").Append(_feToContactN > 0 ? (_feToContact / _feToContactN).ToString("0.0", FeInv) + " s" : "-").Append(')')
              .Append(", koniec: po zwarciu oddane RBM ").Append(_feAfterC).Append(", bez zwarcia ").Append(F460NoContact.ToString("0", FeInv)).Append(" s ").Append(_feNoContact)
              .Append(", scigajacy rozbity ").Append(_feBroken).Append(", nasza strata ").Append(K444Pct(F460Loss)).Append(' ').Append(_feCutLoss)
              .Append(" | wszedzie: klucz NIE w trakcie ").Append(_feKeyOff).Append(", oddane (gracz / szyk pusty / 441) ").Append(_feBack).Append(", w toku przy koncu ").Append(_feOpen)
              .Append(" | pogon przy starcie -> przy zawrocie (zawrotow ").Append(_feSepN).Append("): oderwanie od swoich srednio ")
              .Append(_feSepN > 0 ? (_feSepStart / _feSepN).ToString("0", FeInv) + " -> " + (_feSepTurn / _feSepN).ToString("0", FeInv) + " m" : "-")
              .Append(", rozproszenie srednio ")
              .Append(_feSepN > 0 ? (_feSpreadStart / _feSepN).ToString("0.0", FeInv) + " -> " + (_feSpreadTurn / _feSepN).ToString("0.0", FeInv) + " m" : "-")
              .Append(" | wynik zawrotu (zabici i ranni, ").Append(_feTurnDone).Append("): scigajacy stracil srednio ")
              .Append(_feTurnDone > 0 ? (100.0 * _feTgtLossSum / _feTurnDone).ToString("0", FeInv) + "%" : "-")
              .Append(" (od naszych jezdzcow ").Append(_feTurnDone > 0 ? (100.0 * _feTgtByUsSum / _feTurnDone).ToString("0", FeInv) + "%" : "-").Append(')')
              .Append(", nasza strata w zawrocie ").Append(_feTurnDone > 0 ? (100.0 * _feOwnTurnSum / _feTurnDone).ToString("0", FeInv) + "%" : "-")
              .Append(" | nasza strata w odwrocie (przynet zakonczonych ").Append(_feRetDone).Append(") srednio ").Append(_feRetDone > 0 ? (100.0 * _feOwnRetSum / _feRetDone).ToString("0", FeInv) + "%" : "-")
              .Append(" | naraz maks ").Append(_feMaxN)
              .Append(" | rajd 443 wstrzymany (przynet) ").Append(_feBusy443)
              .Append(" | potkniecia ").Append(_feStum).Append('.');
            return sb.ToString();
        }
    }
}
