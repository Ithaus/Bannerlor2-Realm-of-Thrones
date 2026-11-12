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
    /// 445 = PAKIET 4 kroki 4.4 i 4.5 audytu AI bitwy (docs/audyty-2026-11-11/AUDYT-AI-BITWA.md rozdz. 3 "Jazda"; Jeff 11.11 "rob wszystko po kolei tak jak radzisz",
    /// "aby bitwa nie byla wciaz taka sama ... AI bardziej kombinowalo"). Zadnych bonusow do liczb - tylko decyzje. Bitwa polowa, nie morska. Glowny watek
    /// (Doctrine.Tick, wlasny zegar 1 s, po 443, przed 444 i 442). Szyk: jazda AI (IsCavalryFormation, nie konni strzelcy, >= 5, IsAIControlled, nie szyk gracza /
    /// z czlowiekiem gracza), druzyna TeamAIGeneral obu stron. Profil wodza 440 (klucz Commander Personality In Battle NIE albo brak - ZWYKLY) przesuwa progi.
    ///
    /// 4.4 CAVFIRST (klucz CavalryFightsCavalryFirst "Cavalry Fights Cavalry First", rola 441 CrCavFirst, galop): szyk wolny (< 30% wrecz), w roli NONE albo WAIT 444
    ///   (czekanie ustepuje - WAIT nie jest jeszcze uderzeniem; RAID / HIDE 443 i STRIKE 444 nie ruszane).
    ///   (a) kontrszarza: szyk jazdy wroga (nie konni strzelcy, >= 5) w 300 m od nas jedzie na nasz pieszy szyk strzelcow (predkosc > 2 m/s, cos kata > 0.33,
    ///       dojazd <= 15 s - ta sama regula co 443 (d), dluzszy horyzont, bo my musimy zdazyc) albo jest juz w 30 m od nich; nasz szyk >= 75 / 50 / 35% jego ludzi
    ///       (OSTROZNY / ZWYKLY / PORYWCZY) - inaczej "za slaby". Najblizszy nam.
    ///   (b) przewaga konna: nasza jazda (szyki jazdy druzyny, bez konnych strzelcow) >= x2.0 / x1.5 / x1.2 calej konnicy wroga (jazda + konni strzelcy, >= 5) ->
    ///       najblizszy szyk jazdy wroga (nie konni strzelcy - nie do zlapania) w 300 m.
    ///   Cel niezasloniety piechota wroga (T442Blocked - inaczej RBM i tak skreca jezdzcow w piechote). Rozkaz ChargeToTarget(jazda wroga): RBM GetCorrectTarget
    ///   (rbmai NearestAgentFromMultipleFormations) daje jezdzcowi cel z tego szyku, o ile w 30 m nie ma pieszego wroga; slot 442 (V445AimTarget w T442Form,
    ///   postfiks GetCorrectTarget 442) trzyma jezdzcow na celu jak 443 / 444 (przy kluczu 442 NIE tylko "bylby").
    ///   Zwarcie: nasz szyk i cel oba >= 20% ludzi z ciosem wrecz w 6 s (miara 442). Koniec: cel rozbity (polowa ludzi / < 5), nasza jazda stracila 40%, 90 s,
    ///   40 s bez zwarcia (wtedy nowa szarza na jazde najwczesniej po 45 s - bez gonitwy w kolko), cel dalej niz 350 m (odjechal). Po zwarciu szyk zostaje w roli
    ///   do konca walki z ta jazda (RBM po oddaniu wybralby piechote - FindSignificantEnemy bez jazdy). Nowa rola najwczesniej 10 s po oddaniu.
    /// 4.5 DISENGAGE (klucz CavalryDisengage "Cavalry Disengage", rola 441 CrDisengage, galop): szyk w roli NONE (po STRIKE 444 - po jego oddaniu RBM), w zwarciu
    ///   (>= 30% wrecz), najblizszy szyk wroga pieszy (piechota albo strzelcy) w 10 m, szyk stoi (predkosc < 3 m/s) i traci ludzi: w 10 s >= 3 i >= 7 / 10 / 15%
    ///   (OSTROZNY / ZWYKLY / PORYWCZY). Jezdzcy juz zwiazani nie posluchaja (rbmai Frontline PrefixGetOrderPositionOfUnit: > 2 jezdzcow wroga w 15 m albo
    ///   > 2 pieszych w 7 m -> pozycja Invalid, walka wolna - kazdy rozkaz szyku jest dla nich martwy): gdy takich jest >= 50% szyku, oderwania nie ma (licznik
    ///   "zwiazani"); inaczej wolni odjezdzaja, zwiazani dolacza, gdy sie wyrwa. Punkt zbiorki: 60 m od wroga po linii wrog - jazda (potem 40 m, potem +/- 35
    ///   stopni), na siatce nawigacji, nie przy innym szyku wroga. Move galopem (wyjatek klusa 441); w 25 m od punktu -> 6 s formowania (Move w miejscu), potem
    ///   oddanie RBM (nowa szarza z rozpedu); 30 s bez dojazdu -> oddanie. Potem 30 s przerwy, najwyzej 3 oderwania na szyk w bitwie (bez wahadla).
    /// Klucze NIE (albo bez dowodcy jazdy 441): decyzje liczone (tylko odczyt), role nie nadawane - co do znaku jak dotad; liczniki "bylby".
    /// Watki: wszystko w glownym watku (skan szykow, ApplyActionOnEachUnit ze statycznymi delegatami, GetNearbyEnemyAgents tylko dla kandydata oderwania,
    ///   WorldPosition.SetVec2 / GetNavMesh); zachowanie roli 441 czyta tylko rozkaz. Wyjatki per szyk liczone (potkniecia, rola oddana, slot od zera).
    /// 445-p (po recenzji): kontrszarza tylko najblizszy wolny szyk spelniajacy prog sily (cel zajety przez nasz szyk CAVFIRST - nikt wiecej), przewaga konna tez
    ///   z progiem sily szyku i bez jazdy wroga stojacej przy jej piechocie (30 m + pol szerokosci - RBM skreca tam jezdzcow w piechote); CAVFIRST najwyzej 3 wejscia
    ///   na szyk, zakaz po stracie 40% ludzi od pierwszego wejscia, po stracie / czasie / bez zwarcia / odjezdzie 45 s przerwy tylko dla CAVFIRST (DISENGAGE osobno);
    ///   strata celu i nasza liczone zabitymi i rannymi (V445OnRemoved), nie spadkiem CountOfUnits; klucz NIE w trakcie konczy role; dojazd DISENGAGE = >= 70% wolnych
    ///   jezdzcow w 25 m od punktu; punkt zbiorki w granicach pola (>= 15 m od krawedzi); zwiazani liczeni jak RBM (bez filtra IsHuman / IsActive), raz na epizod.
    /// </summary>
    internal static partial class Doctrine
    {
        internal const float V445Every = 1f, V445Range = 300f, V445Lost = 350f, V445Eta = 15f, V445Near = 30f, V445Dot = 0.33f, V445Speed = 2f;
        internal const float V445NoContact = 40f, V445NoContactWait = 35f, V445Max = 90f, V445Loss = 0.4f, V445Broken = 0.5f, V445Reenter = 10f, V445Contact = 0.2f;
        internal const float V445StuckSpeed = 3f, V445EngGap = 10f, V445Back = 60f, V445Back2 = 40f, V445Turn = 0.61f, V445Reach = 25f, V445Ride = 30f;
        internal const float V445Reform = 6f, V445Cool = 30f, V445TangleFoot = 7f, V445TangleRider = 15f, V445TangleMax = 0.5f, V445AtPoint = 30f, V445FoeGap = 30f;
        internal const int V445MaxDis = 3, V445MinLoss = 3;
        // 445-p: limit wejsc CAVFIRST na szyk, udzial wolnych jezdzcow = dojazd, odstep punktu od krawedzi pola, jazda wroga przy swojej piechocie, okno straty w trakcie
        internal const int V445CavMaxN = 3;
        internal const float V445ReachShare = 0.7f, V445Edge = 15f, V445FootGap = 30f, V445LossWin = 10f;
        private const int V4R = 11;   // pierscien liczby ludzi slotu: 10 s (probka co takt 1 s)
        // 445-p: osobny zatrzask na kazdy licznik "co 30 s / 10 s" (slot x VL)
        private const int VL = 10, VlWeak = 0, VlDry = 1, VlRefC = 2, VlCap = 3, VlSpot = 4, VlDisDry = 5, VlRefD = 6, VlCavCap = 7, VlOther = 8, VlAdvWeak = 9;
        // progi profilu wodza 440 (0 OSTROZNY, 1 ZWYKLY, 2 PORYWCZY)
        private static readonly float[] V4Adv = { 2.0f, 1.5f, 1.2f };     // przewaga konna: nasza jazda >= x konnicy wroga
        private static readonly float[] V4Match = { 0.75f, 0.5f, 0.35f }; // kontrszarza: nasz szyk >= x ludzi szyku wroga
        private static readonly float[] V4Bleed = { 0.07f, 0.10f, 0.15f }; // oderwanie: strata w 10 s >= x ludzi szyku
        private const byte VmNone = 0, VmCav = 1, VmDis = 2;
        private static readonly IFormatProvider V4Inv = CultureInfo.InvariantCulture;

        // ---- stan slotow [TeamIndex x 10 + FormationIndex] (tylko glowny watek; V445AimTarget czyta T442Form - tez glowny)
        private static bool _v4Live, _v4KCav, _v4KDis;
        private static float _v4Acc;
        private static int _v4RPos;
        private static readonly Formation[] _v4Who = new Formation[CrSlots];
        private static readonly byte[] _v4Mode = new byte[CrSlots];
        private static readonly float[] _v4Since = new float[CrSlots], _v4Exit = new float[CrSlots], _v4CavWait = new float[CrSlots];
        private static readonly float[] _v4ContactAt = new float[CrSlots], _v4Reached = new float[CrSlots], _v4Cool = new float[CrSlots];
        private static readonly float[] _v4Latch = new float[CrSlots * VL];
        // 445-p: epizod "zwiazani" policzony, 443 wstrzymany policzony w tej roli, strata w trakcie zmierzona
        private static readonly bool[] _v4TangEp = new bool[CrSlots], _v4K443 = new bool[CrSlots], _v4LossMeas = new bool[CrSlots];
        // 445-p: wolni jezdzcy przy starcie oderwania, strata 10 s przed, zabici celu / nasi w roli, wejscia CAVFIRST, ludzie przy pierwszym wejsciu, nasi zabici w CAVFIRST lacznie
        private static readonly int[] _v4Free0 = new int[CrSlots], _v4LossB4 = new int[CrSlots], _v4TgtKill = new int[CrSlots], _v4OwnKill = new int[CrSlots];
        private static readonly int[] _v4CavNi = new int[CrSlots], _v4CavMen00 = new int[CrSlots], _v4CavKilled = new int[CrSlots];
        private static readonly Formation[] _v4LastTgt = new Formation[CrSlots];
        private static readonly Formation[] _v4Tgt = new Formation[CrSlots];
        private static readonly bool[] _v4Counter = new bool[CrSlots];
        private static readonly Vec2[] _v4P = new Vec2[CrSlots];
        private static readonly int[] _v4Men0 = new int[CrSlots], _v4TgtMen0 = new int[CrSlots], _v4DisN = new int[CrSlots];
        private static readonly int[] _v4Ring = new int[CrSlots * V4R];
        private static readonly int[] _v4OurCav = new int[MaxTeams], _v4FoeCav = new int[MaxTeams];
        private static readonly Dictionary<Formation, float> _v4Melee = new Dictionary<Formation, float>();
        private static readonly MBList<Agent> _v4Buf = new MBList<Agent>();

        // ---- liczniki misji (V445Reset w Battle291.Begin)
        private static int _v4CavN, _v4CounterN, _v4AdvN, _v4FromWait, _v4CounterDry, _v4AdvDry, _v4Weak, _v4Blocked, _v4Contact, _v4CavBroken, _v4CavLoss;
        private static int _v4CavTime, _v4NoContact, _v4Fled, _v4CavBack, _v4CavOpen, _v4CavDone, _v4Refused, _v4Keep443, _v4Stum;
        private static int _v4DisN0, _v4DisDry, _v4DisTangled, _v4DisNoSpot, _v4DisCap, _v4DisReached, _v4DisRideOut, _v4DisDone, _v4DisBack, _v4DisEnd, _v4DisOpen;
        private static int _v4MaxCav, _v4MaxDis, _v4InterN;
        private static int _v4CounterOther, _v4AdvWeak, _v4NearFoot, _v4CavGone, _v4CavKeyOff, _v4DisKeyOff, _v4CavCap, _v4CavSpent, _v4SameTgt, _v4LossN;
        private static float _v4CavSec, _v4DisSec;
        private static double _v4TgtLossSum, _v4OwnLossSum, _v4InterSum, _v4TangleSum, _v4AtPointSum, _v4LossBefore, _v4LossDuring;

        /// <summary>Battle291.Begin (AfterStart): liczniki linii 445 od zera.</summary>
        internal static void V445Reset()
        {
            _v4CounterOther = _v4AdvWeak = _v4NearFoot = _v4CavGone = _v4CavKeyOff = _v4DisKeyOff = _v4CavCap = _v4CavSpent = _v4SameTgt = _v4LossN = 0;
            _v4CavN = _v4CounterN = _v4AdvN = _v4FromWait = _v4CounterDry = _v4AdvDry = _v4Weak = _v4Blocked = _v4Contact = _v4CavBroken = _v4CavLoss = 0;
            _v4CavTime = _v4NoContact = _v4Fled = _v4CavBack = _v4CavOpen = _v4CavDone = _v4Refused = _v4Keep443 = _v4Stum = 0;
            _v4DisN0 = _v4DisDry = _v4DisTangled = _v4DisNoSpot = _v4DisCap = _v4DisReached = _v4DisRideOut = _v4DisDone = _v4DisBack = _v4DisEnd = _v4DisOpen = 0;
            _v4MaxCav = _v4MaxDis = _v4InterN = 0;
            _v4CavSec = _v4DisSec = 0f;
            _v4TgtLossSum = _v4OwnLossSum = _v4InterSum = _v4TangleSum = _v4AtPointSum = _v4LossBefore = _v4LossDuring = 0.0;
        }

        /// <summary>Doctrine.Begin (AfterStart, po Clear): bitwa polowa, nie morska - pomiar zawsze, decyzje wedlug kluczy (czytane co takt).</summary>
        private static void V445Begin(Mission m)
        {
            bool field = false;
            try { field = m != null && m.IsFieldBattle && !m.IsNavalBattle; } catch { field = false; }
            _v4Acc = 0f;
            _v4Live = field;
        }

        /// <summary>Doctrine.Clear (Begin / End): stan slotow od zera (role oddaje C441ClearMission). Role w toku przy koncu - liczniki "w toku".</summary>
        private static void V445ClearMission()
        {
            _v4Live = false;
            for (int i = 0; i < CrSlots; i++)
            {
                if (_v4Mode[i] == VmCav) _v4CavOpen++;
                else if (_v4Mode[i] == VmDis) _v4DisOpen++;
                V445SlotClear(i);
                V445SlotNew(i);
                _v4Who[i] = null;
            }
            for (int i = 0; i < _v4Ring.Length; i++) _v4Ring[i] = 0;
            for (int i = 0; i < MaxTeams; i++) { _v4OurCav[i] = _v4FoeCav[i] = 0; }
            _v4Melee.Clear();
            _v4Buf.Clear();
            _v4RPos = 0;
            _v4Acc = 0f;
        }

        private static void V445SlotClear(int i)
        {
            _v4Mode[i] = VmNone; _v4Tgt[i] = null; _v4Counter[i] = false;
            _v4Since[i] = 0f; _v4ContactAt[i] = _v4Reached[i] = -1f;
            _v4P[i] = Vec2.Invalid; _v4Men0[i] = _v4TgtMen0[i] = 0;
            _v4K443[i] = _v4LossMeas[i] = false; _v4Free0[i] = _v4LossB4[i] = _v4TgtKill[i] = _v4OwnKill[i] = 0;
        }

        /// <summary>Stan szyku na cala bitwe (nowy szyk w slocie / koniec misji): przerwy, limity, zatrzaski, epizod zwiazanych.</summary>
        private static void V445SlotNew(int i)
        {
            _v4Exit[i] = _v4CavWait[i] = _v4Cool[i] = -1000f; _v4DisN[i] = 0;
            _v4CavNi[i] = _v4CavMen00[i] = _v4CavKilled[i] = 0; _v4LastTgt[i] = null; _v4TangEp[i] = false;
            for (int k = 0; k < VL; k++) _v4Latch[i * VL + k] = -1000f;
        }

        /// <summary>Zatrzask licznika k slotu idx: true najwyzej raz na dur sekund.</summary>
        private static bool V445Latch(int idx, int k, float now, float dur)
        {
            int i = idx * VL + k;
            if (now < _v4Latch[i]) return false;
            _v4Latch[i] = now + dur;
            return true;
        }

        // ------------------------------------------------------------ odczyty dla 442 / 443 (glowny watek)

        /// <summary>T442Form: szyk w roli CAVFIRST - jego cel (slot 442 jezdzcow); inaczej null.</summary>
        internal static Formation V445AimTarget(Formation f, int idx)
        {
            if (!_v4Live || f == null || idx < 0 || idx >= CrSlots) return null;
            if (_v4Mode[idx] != VmCav || !ReferenceEquals(_v4Who[idx], f)) return null;
            var tg = _v4Tgt[idx];
            return tg != null && tg.CountOfUnits > 0 ? tg : null;
        }

        /// <summary>R443Cav: szyk w roli CAVFIRST / DISENGAGE - 443 nie zaczyna rajdu / schowania (nie nadpisuje roli).</summary>
        internal static bool V445Busy(Formation f)
        {
            byte r = C441RoleOf(f);
            if (r != CrCavFirst && r != CrDisengage) return false;
            // 445-p: raz na role 445 (nie co takt)
            int idx = C441Idx(f);
            if (idx >= 0 && idx < CrSlots && !_v4K443[idx]) { _v4K443[idx] = true; _v4Keep443++; }
            return true;
        }

        /// <summary>445-p: BattleSense.OnAgentRemoved (glowny watek, szyk ofiary jeszcze przypisany): czlowiek zabity / ranny w szyku CAVFIRST albo w jego celu -
        /// strata liczona zabitymi i rannymi (nie spadkiem CountOfUnits, ktory spada tez przy przenosinach ludzi miedzy szykami).</summary>
        internal static void V445OnRemoved(Agent v, AgentState st)
        {
            if (!_v4Live || v == null) return;
            try
            {
                if (st != AgentState.Killed && st != AgentState.Unconscious) return;
                if (!v.IsHuman) return;
                var vf = v.Formation;
                if (vf == null) return;
                int vi = C441Idx(vf);
                if (vi >= 0 && vi < CrSlots && _v4Mode[vi] == VmCav && ReferenceEquals(_v4Who[vi], vf)) { _v4OwnKill[vi]++; _v4CavKilled[vi]++; }
                for (int i = 0; i < CrSlots; i++)
                    if (_v4Mode[i] == VmCav && ReferenceEquals(_v4Tgt[i], vf)) _v4TgtKill[i]++;
            }
            catch (Exception e) { _v4Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.CavFirst445 zabity", e); }
        }

        // ------------------------------------------------------------ takt glownego watku (Doctrine.Tick)

        internal static void V445Tick(Mission m, float dt)
        {
            if (!_v4Live || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m) || m.MissionEnded) return;
            _v4Acc += dt;
            if (_v4Acc < V445Every) return;
            float step = _v4Acc;
            _v4Acc = 0f;
            bool dep;
            try { dep = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && m.CurrentTime >= P440NoDeploy); } catch { dep = false; }
            if (!dep) return;
            var c = Settings.Current;
            if (c == null) return;
            _v4KCav = c.CavalryFightsCavalryFirst; _v4KDis = c.CavalryDisengage;
            float now = m.CurrentTime;
            _t4Now = now;   // T442MeleeShare (glowny watek)
            _v4Melee.Clear();
            _v4RPos = (_v4RPos + 1) % V4R;
            var teams = m.Teams;
            int n = teams != null ? teams.Count : 0;
            // konnica druzyn: nasza jazda (bez konnych strzelcow) i cala konnica wroga (jazda + konni strzelcy)
            for (int i = 0; i < MaxTeams; i++) _v4OurCav[i] = _v4FoeCav[i] = 0;
            for (int i = 0; i < n; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!R443AiTeam(t)) continue;
                try
                {
                    int ti = t.TeamIndex, own = 0, foe = 0;
                    var fl = t.FormationsIncludingSpecialAndEmpty;
                    for (int j = 0; fl != null && j < fl.Count; j++)
                    {
                        var x = fl[j];
                        if (x == null || x.CountOfUnits <= 0) continue;
                        var xq = x.QuerySystem;
                        if (xq != null && xq.IsCavalryFormation && !xq.IsRangedCavalryFormation) own += x.CountOfUnits;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        var et = teams[k];
                        if (!MissionTeams.Enemies(t, et)) continue;
                        var efl = et.FormationsIncludingSpecialAndEmpty;
                        for (int j = 0; efl != null && j < efl.Count; j++)
                        {
                            var x = efl[j];
                            if (x == null || x.CountOfUnits <= 0) continue;
                            var xq = x.QuerySystem;
                            if (xq != null && (xq.IsCavalryFormation || xq.IsRangedCavalryFormation)) foe += x.CountOfUnits;
                        }
                    }
                    _v4OurCav[ti] = own; _v4FoeCav[ti] = foe;
                }
                catch (Exception e) { _v4Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.CavFirst445 konnica", e); }
            }
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
                    try { V445Form(m, t, f, idx, now, step); }
                    catch (Exception e)
                    {
                        // potkniecie szyku: rola oddana (tylko nasza), slot od zera - bez globalnego wylacznika
                        _v4Stum++;
                        try { byte r = C441RoleOf(f); if (r == CrCavFirst || r == CrDisengage) C441SetRole(f, CrNone, default(MovementOrder)); } catch { }
                        V445SlotClear(idx);
                        _v4Exit[idx] = now;
                        RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.CavFirst445 szyk", e);
                    }
                }
            }
            // ile szykow naraz w rolach (martwy kod ozywiony - zasada 4)
            int nc = 0, nd = 0;
            for (int i = 0; i < CrSlots; i++) { if (_v4Mode[i] == VmCav) nc++; else if (_v4Mode[i] == VmDis) nd++; }
            if (nc > _v4MaxCav) _v4MaxCav = nc;
            if (nd > _v4MaxDis) _v4MaxDis = nd;
        }

        private static void V445Form(Mission m, Team t, Formation f, int idx, float now, float step)
        {
            int cnt = f.CountOfUnits;
            int rb = idx * V4R;
            if (!ReferenceEquals(_v4Who[idx], f))
            {
                V445SlotClear(idx);
                V445SlotNew(idx);
                _v4Who[idx] = f;
                for (int k = 0; k < V4R; k++) _v4Ring[rb + k] = cnt;
            }
            // liczba ludzi 10 s temu (pierscien - kazdy slot, co takt)
            int old = _v4Ring[rb + (_v4RPos + 1) % V4R];
            _v4Ring[rb + _v4RPos] = cnt;
            var q = f.QuerySystem;
            bool cav = q != null && q.IsCavalryFormation && !q.IsRangedCavalryFormation;
            bool mine = cnt >= R443CavMin && f.IsAIControlled && !HorsePursuit.PlayerForm(f) && !f.HasPlayerControlledTroop;
            byte mode = _v4Mode[idx];
            if (!cav || !mine)
            {
                if (mode != VmNone) V445End(f, idx, now, 9);
                return;
            }
            byte role = C441RoleOf(f);
            if (mode != VmNone && role != (mode == VmCav ? CrCavFirst : CrDisengage))
            {
                // rola zmieniona przez kogos innego (441: gracz / szyk pusty) - stan 445 od zera, roli nie ruszamy
                if (mode == VmCav) { _v4CavBack++; V445Score(f, idx); } else _v4DisBack++;
                V445SlotClear(idx);
                _v4Exit[idx] = now;
                return;
            }
            Vec2 me = f.CachedAveragePosition;
            if (!me.IsValid) return;
            float own = V445MeleeOf(f);
            sbyte prof = _cpKey ? P440Prof(t) : (sbyte)1;
            if (prof < 0 || prof > 2) prof = 1;
            // 445-p: klucz NIE (albo bez dowodcy jazdy) w trakcie - rola oddana od razu, jak 444
            if (mode == VmCav)
            {
                if (!_v4KCav || !C441Live) { _v4CavKeyOff++; V445End(f, idx, now, 1); return; }
                _v4CavSec += step; V445CavTick(t, f, idx, me, own, now); return;
            }
            if (mode == VmDis)
            {
                if (!_v4KDis || !C441Live) { _v4DisKeyOff++; V445End(f, idx, now, 2); return; }
                _v4DisSec += step; V445DisTick(f, idx, now); return;
            }
            if (own < T442OwnMelee) _v4TangEp[idx] = false;   // 445-p: poza zwarciem - nowy epizod "zwiazani"
            if (now - _v4Exit[idx] < V445Reenter) return;
            if ((role == CrNone || role == CrWait) && own < T442OwnMelee) { V445TryCav(t, f, idx, me, prof, role, now); return; }
            if (role == CrNone && own >= T442OwnMelee) V445TryDis(m, t, f, idx, me, cnt, old, prof, now);
        }

        // ------------------------------------------------------------ 4.4 CAVFIRST

        private static void V445TryCav(Team t, Formation f, int idx, Vec2 me, sbyte prof, byte role, float now)
        {
            if (now < _v4CavWait[idx]) return;   // 445-p: przerwa CAVFIRST po stracie / czasie / bez zwarcia / odjezdzie (DISENGAGE jej nie dzieli)
            int ti = t.TeamIndex;
            Formation tg = null, skip = null;
            bool counter = false, blocked = false, byFoot = false;
            bool spent = !V445CavFree(idx);
            float dArch;
            var thr = V445Threat(t, me, ref blocked, out dArch);
            if (thr != null)
            {
                skip = thr;   // 445-p: gdy nie ten szyk kontruje - galaz (b) nie bierze tego samego celu
                if (f.CountOfUnits < V4Match[prof] * thr.CountOfUnits) { if (V445Latch(idx, VlWeak, now, R443Latch)) _v4Weak++; }
                else if (V445Claimed(t, thr)) { if (V445Latch(idx, VlOther, now, R443Latch)) _v4CounterOther++; }
                else if (spent || ReferenceEquals(V445CounterBy(t, thr, prof, now), f)) { tg = thr; counter = true; }   // spent: odrzucony nizej i liczony
                else if (V445Latch(idx, VlOther, now, R443Latch)) _v4CounterOther++;
            }
            if (tg == null && ti >= 0 && ti < MaxTeams && _v4FoeCav[ti] >= R443CavMin && _v4OurCav[ti] >= V4Adv[prof] * _v4FoeCav[ti])
            {
                var c = V445NearestCav(t, me, skip, ref blocked, ref byFoot);
                if (c != null)
                {
                    // 445-p: prog sily szyku jak przy kontrszarzy (sama przewaga druzyny nie wystarczy - 5 jezdzcow na 40)
                    if (f.CountOfUnits >= V4Match[prof] * c.CountOfUnits) tg = c;
                    else if (V445Latch(idx, VlAdvWeak, now, R443Latch)) _v4AdvWeak++;
                }
            }
            if (blocked) _v4Blocked++;   // 445-p: raz na szyk na takt
            if (byFoot) _v4NearFoot++;
            if (tg == null) return;
            if (spent)
            {
                // 445-p: limit 3 wejsc na szyk albo strata 40% od pierwszego wejscia - bez kolejnych szarz na jazde w tej bitwie
                if (V445Latch(idx, VlCavCap, now, R443Latch)) { if (_v4CavNi[idx] >= V445CavMaxN) _v4CavCap++; else _v4CavSpent++; }
                return;
            }
            if (!_v4KCav || !C441Live)
            {
                if (V445Latch(idx, VlDry, now, R443Latch)) { if (counter) _v4CounterDry++; else _v4AdvDry++; }
                return;
            }
            if (!C441SetRole(f, CrCavFirst, MovementOrder.MovementOrderChargeToTarget(tg)))
            {
                if (V445Latch(idx, VlRefC, now, R443Latch)) _v4Refused++;
                return;
            }
            _v4Mode[idx] = VmCav; _v4Since[idx] = now; _v4Tgt[idx] = tg; _v4Counter[idx] = counter;
            _v4ContactAt[idx] = -1f; _v4Men0[idx] = f.CountOfUnits; _v4TgtMen0[idx] = tg.CountOfUnits;
            _v4TgtKill[idx] = _v4OwnKill[idx] = 0;
            if (_v4CavNi[idx] == 0) _v4CavMen00[idx] = f.CountOfUnits;
            _v4CavNi[idx]++;
            if (ReferenceEquals(_v4LastTgt[idx], tg)) _v4SameTgt++;
            _v4LastTgt[idx] = tg;
            _v4CavN++;
            if (counter) _v4CounterN++; else _v4AdvN++;
            if (role == CrWait) _v4FromWait++;
        }

        /// <summary>445-p: szyk moze jeszcze wejsc w CAVFIRST: mniej niz 3 wejscia i nasi zabici / ranni w CAVFIRST < 40% ludzi z pierwszego wejscia.</summary>
        private static bool V445CavFree(int gi)
        {
            if (_v4CavNi[gi] >= V445CavMaxN) return false;
            return _v4CavMen00[gi] <= 0 || _v4CavKilled[gi] < V445Loss * _v4CavMen00[gi];
        }

        /// <summary>445-p: na ten szyk jazdy wroga jedzie juz nasz szyk CAVFIRST (ta sama druzyna) - drugi nie rusza.</summary>
        private static bool V445Claimed(Team t, Formation thr)
        {
            for (int i = 0; i < CrSlots; i++)
            {
                if (_v4Mode[i] != VmCav || !ReferenceEquals(_v4Tgt[i], thr)) continue;
                var w = _v4Who[i];
                if (w != null && ReferenceEquals(w.Team, t)) return true;
            }
            return false;
        }

        /// <summary>445-p: najblizszy zagrozeniu wolny szyk jazdy AI druzyny (rola NONE / WAIT, poza zwarciem, po przerwach i limitach), ktory spelnia prog sily
        /// kontrszarzy - tylko on kontruje (bez rezerwacji: kazdy szyk liczy to samo w tym samym takcie, wynik ten sam).</summary>
        private static Formation V445CounterBy(Team t, Formation thr, sbyte prof, float now)
        {
            Vec2 tp = thr.CachedAveragePosition;
            if (!tp.IsValid) return null;
            Formation best = null;
            float bd = float.MaxValue;
            float need = V4Match[prof] * thr.CountOfUnits;
            var fl = t.FormationsIncludingSpecialAndEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                var g = fl[j];
                if (g == null || g.CountOfUnits < R443CavMin || g.CountOfUnits < need) continue;
                var gq = g.QuerySystem;
                if (gq == null || !gq.IsCavalryFormation || gq.IsRangedCavalryFormation) continue;
                if (!g.IsAIControlled || HorsePursuit.PlayerForm(g) || g.HasPlayerControlledTroop) continue;
                int gi = C441Idx(g);
                if (gi < 0 || gi >= CrSlots) continue;
                if (ReferenceEquals(_v4Who[gi], g))
                {
                    if (_v4Mode[gi] != VmNone) continue;
                    if (now - _v4Exit[gi] < V445Reenter || now < _v4CavWait[gi]) continue;
                    if (!V445CavFree(gi)) continue;
                }
                byte r = C441RoleOf(g);
                if (r != CrNone && r != CrWait) continue;
                if (V445MeleeOf(g) >= T442OwnMelee) continue;
                Vec2 gp = g.CachedAveragePosition;
                if (!gp.IsValid) continue;
                float d = gp.Distance(tp);
                if (d < bd) { bd = d; best = g; }
            }
            return best;
        }

        private static void V445CavTick(Team t, Formation f, int idx, Vec2 me, float own, float now)
        {
            var tg = _v4Tgt[idx];
            int men0 = _v4TgtMen0[idx];
            bool alive = tg != null && tg.CountOfUnits >= R443CavMin && MissionTeams.Enemies(t, tg.Team);
            // 445-p: szyk celu zniknal / < 5 / nie wrog - osobno od "rozbita" (zabici i ranni celu >= 50% ludzi z poczatku roli)
            if (!alive) { _v4CavGone++; V445End(f, idx, now, 1); return; }
            if (men0 > 0 && _v4TgtKill[idx] >= V445Broken * men0) { _v4CavBroken++; V445End(f, idx, now, 1); return; }
            if (_v4Men0[idx] > 0 && _v4OwnKill[idx] >= V445Loss * _v4Men0[idx]) { _v4CavLoss++; V445End(f, idx, now, 1); _v4CavWait[idx] = now + V445NoContactWait; return; }
            if (now - _v4Since[idx] > V445Max) { _v4CavTime++; V445End(f, idx, now, 1); _v4CavWait[idx] = now + V445NoContactWait; return; }
            Vec2 ep = tg.CachedAveragePosition;
            if (ep.IsValid && me.Distance(ep) > V445Lost) { _v4Fled++; V445End(f, idx, now, 1); _v4CavWait[idx] = now + V445NoContactWait; return; }
            if (_v4ContactAt[idx] >= 0f) return;
            if (own >= V445Contact && V445MeleeOf(tg) >= V445Contact)
            {
                _v4ContactAt[idx] = now;
                _v4Contact++;
                if (_v4Counter[idx] && ep.IsValid)
                {
                    float d = V445ArcherGap(t, ep);
                    if (d < float.MaxValue) { _v4InterSum += d; _v4InterN++; }
                }
                return;
            }
            if (now - _v4Since[idx] > V445NoContact) { _v4NoContact++; V445End(f, idx, now, 1); _v4CavWait[idx] = now + V445NoContactWait; }
        }

        /// <summary>Kontrszarza: szyk jazdy wroga (>= 5, nie konni strzelcy) w 300 m od nas, jadacy na nasz pieszy szyk strzelcow (dojazd <= 15 s) albo w 30 m
        /// od niego, niezasloniety piechota wroga - najblizszy nam. dArch - odstep od zagrozonych strzelcow; blocked - jakis cel odrzucony przez zasloniecie.</summary>
        private static Formation V445Threat(Team t, Vec2 me, ref bool blocked, out float dArch)
        {
            dArch = float.MaxValue;
            Formation best = null;
            float bd = float.MaxValue;
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
                    float d = me.Distance(ep);
                    if (d > V445Range || d >= bd) continue;
                    float g;
                    if (!V445Rides(t, e, ep, out g)) continue;
                    if (T442Blocked(t, e, me, ep)) { blocked = true; continue; }
                    best = e; bd = d; dArch = g;
                }
            }
            return best;
        }

        /// <summary>Czy szyk jazdy wroga e jedzie na nasz pieszy szyk strzelcow (>= 5): odstep <= 30 m albo predkosc > 2 m/s, cos > 0.33 i dojazd <= 15 s.</summary>
        private static bool V445Rides(Team t, Formation e, Vec2 ep, out float gapOut)
        {
            gapOut = float.MaxValue;
            bool thr = false;
            Vec2 v = e.CachedCurrentVelocity;
            float sp = v.IsValid ? v.Length : 0f;
            var fl = t.FormationsIncludingSpecialAndEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                var a = fl[j];
                if (a == null || a.CountOfUnits < R443CavMin) continue;
                var aq = a.QuerySystem;
                if (aq == null || !aq.IsRangedFormation || aq.IsCavalryFormation || aq.IsRangedCavalryFormation) continue;
                Vec2 ap = a.CachedMedianPosition.AsVec2;
                if (!ap.IsValid) continue;
                Vec2 to = ap - ep;
                float len = to.Length;
                float gap = Math.Max(0f, len - 0.5f * (a.Depth + e.Depth));
                if (gap <= V445Near) { thr = true; if (gap < gapOut) gapOut = gap; continue; }
                if (sp <= V445Speed || len < 0.1f) continue;
                float cos = (v.x * to.x + v.y * to.y) / (sp * len);
                if (cos <= V445Dot || gap / sp > V445Eta) continue;
                thr = true;
                if (gap < gapOut) gapOut = gap;
            }
            return thr;
        }

        /// <summary>Przewaga konna: najblizszy szyk jazdy wroga (>= 5, nie konni strzelcy, nie skip) w 300 m, niezasloniety piechota wroga i nie stojacy przy pieszym
        /// szyku swojej druzyny (445-p: RBM GetCorrectTarget daje jezdzcowi pieszego wroga w 30 m przed szykiem-celem - rbmai 10796).</summary>
        private static Formation V445NearestCav(Team t, Vec2 me, Formation skip, ref bool blocked, ref bool byFoot)
        {
            Formation best = null;
            float bd = float.MaxValue;
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
                    if (e == null || ReferenceEquals(e, skip) || e.CountOfUnits < R443CavMin) continue;
                    var eq = e.QuerySystem;
                    if (eq == null || !eq.IsCavalryFormation || eq.IsRangedCavalryFormation) continue;
                    Vec2 ep = e.CachedAveragePosition;
                    if (!ep.IsValid) continue;
                    float d = me.Distance(ep);
                    if (d > V445Range || d >= bd) continue;
                    if (T442Blocked(t, e, me, ep)) { blocked = true; continue; }
                    if (V445ByFoot(et, e, ep)) { byFoot = true; continue; }
                    best = e; bd = d;
                }
            }
            return best;
        }

        /// <summary>445-p: srodek jazdy wroga blizej niz 30 m + pol szerokosci od pieszego szyku (>= 5) jej druzyny.</summary>
        private static bool V445ByFoot(Team et, Formation e, Vec2 ep)
        {
            var fl = et.FormationsIncludingSpecialAndEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                var x = fl[j];
                if (x == null || ReferenceEquals(x, e) || x.CountOfUnits < R443CavMin) continue;
                var xq = x.QuerySystem;
                if (xq == null || xq.IsCavalryFormation || xq.IsRangedCavalryFormation) continue;
                Vec2 xp = x.CachedMedianPosition.AsVec2;
                if (!xp.IsValid) continue;
                if (ep.Distance(xp) < V445FootGap + 0.5f * Math.Max(1f, x.Width)) return true;
            }
            return false;
        }

        /// <summary>Odstep jazdy wroga od najblizszego naszego pieszego szyku strzelcow (pomiar przechwycenia kontrszarzy).</summary>
        private static float V445ArcherGap(Team t, Vec2 ep)
        {
            float best = float.MaxValue;
            var fl = t.FormationsIncludingSpecialAndEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                var a = fl[j];
                if (a == null || a.CountOfUnits < R443CavMin) continue;
                var aq = a.QuerySystem;
                if (aq == null || !aq.IsRangedFormation || aq.IsCavalryFormation || aq.IsRangedCavalryFormation) continue;
                Vec2 ap = a.CachedMedianPosition.AsVec2;
                if (!ap.IsValid) continue;
                float d = Math.Max(0f, ap.Distance(ep) - 0.5f * a.Depth);
                if (d < best) best = d;
            }
            return best;
        }

        // ------------------------------------------------------------ 4.5 DISENGAGE

        private static void V445TryDis(Mission m, Team t, Formation f, int idx, Vec2 me, int cnt, int old, sbyte prof, float now)
        {
            if (now < _v4Cool[idx]) return;
            // najblizszy szyk wroga pieszy, w 10 m
            var nq = f.CachedClosestEnemyFormation;
            var nf = nq != null ? nq.Formation : null;
            if (nf == null) return;
            var nfq = nf.QuerySystem;
            if (nfq == null || nfq.IsCavalryFormation || nfq.IsRangedCavalryFormation) return;
            if (!nfq.IsInfantryFormation && !nfq.IsRangedFormation) return;
            if (R443GapOf(f) > V445EngGap) { _v4TangEp[idx] = false; return; }   // 445-p: poza zwarciem z piechota - nowy epizod "zwiazani"
            // stoi i traci ludzi
            Vec2 v = f.CachedCurrentVelocity;
            float sp = v.IsValid ? v.Length : 0f;
            if (sp >= V445StuckSpeed) return;
            int lost = old - cnt;
            if (lost < V445MinLoss || lost < V4Bleed[prof] * old) return;
            if (_v4DisN[idx] >= V445MaxDis)
            {
                if (V445Latch(idx, VlCap, now, R443Latch)) _v4DisCap++;
                return;
            }
            int free0;
            float tangled = V445Tangled(m, f, out free0);
            if (tangled >= V445TangleMax)
            {
                // 445-p: raz na epizod zaklinowania (nie proba co 10 s) - porownywalne z liczba oderwan
                if (!_v4TangEp[idx]) { _v4TangEp[idx] = true; _v4DisTangled++; }
                return;
            }
            Vec2 p;
            WorldPosition wp;
            if (!V445RallyPoint(m, t, f, nf, me, out p, out wp))
            {
                if (V445Latch(idx, VlSpot, now, V445Reenter)) _v4DisNoSpot++;
                return;
            }
            if (!_v4KDis || !C441Live)
            {
                if (V445Latch(idx, VlDisDry, now, R443Latch)) _v4DisDry++;
                return;
            }
            if (!C441SetRole(f, CrDisengage, MovementOrder.MovementOrderMove(wp)))
            {
                if (V445Latch(idx, VlRefD, now, R443Latch)) _v4Refused++;
                return;
            }
            _v4Mode[idx] = VmDis; _v4Since[idx] = now; _v4P[idx] = p; _v4Reached[idx] = -1f; _v4Men0[idx] = cnt;
            _v4Free0[idx] = Math.Max(1, free0); _v4LossB4[idx] = lost; _v4LossMeas[idx] = false;
            _v4DisN[idx]++;
            _v4DisN0++;
            _v4TangleSum += tangled;
        }

        private static void V445DisTick(Formation f, int idx, float now)
        {
            if (now - _v4Since[idx] >= V445LossWin) V445LossMeasure(f, idx, now);
            if (_v4Reached[idx] < 0f)
            {
                // 445-p: dojazd = >= 70% jezdzcow wolnych przy starcie w 25 m od punktu (srednia szyku z zwiazanymi, ktorzy zostaja w piechocie, nie dojedzie)
                int need = Math.Max(1, (int)Math.Ceiling(V445ReachShare * _v4Free0[idx]));
                if (_v4P[idx].IsValid && V445AtPointCount(f, _v4P[idx], V445Reach) >= need) { _v4Reached[idx] = now; _v4DisReached++; }
                else if (now - _v4Since[idx] > V445Ride) { _v4DisRideOut++; V445End(f, idx, now, 2); return; }
                return;
            }
            if (now - _v4Reached[idx] >= V445Reform) { _v4DisDone++; V445End(f, idx, now, 2); }
        }

        /// <summary>445-p: strata w pierwszych 10 s roli DISENGAGE (przy wczesniejszym koncu - przeliczona na 10 s) obok straty 10 s przed - ta sama populacja.</summary>
        private static void V445LossMeasure(Formation f, int idx, float now)
        {
            if (_v4LossMeas[idx]) return;
            _v4LossMeas[idx] = true;
            float el = Math.Max(1f, now - _v4Since[idx]);
            int l = Math.Max(0, _v4Men0[idx] - (f != null ? f.CountOfUnits : 0));
            _v4LossBefore += _v4LossB4[idx];
            _v4LossDuring += l * (V445LossWin / el);
            _v4LossN++;
        }

        /// <summary>Oddanie roli (tylko gdy rola jest nasza). why: 1 koniec CAVFIRST, 2 koniec DISENGAGE (liczniki powodu u wolajacego), 9 szyk zniknal / gracz.</summary>
        private static void V445End(Formation f, int idx, float now, int why)
        {
            byte mode = _v4Mode[idx];
            if (mode == VmCav) { if (why == 9) _v4CavBack++; V445Score(f, idx); }
            else if (mode == VmDis)
            {
                if (why == 9) _v4DisBack++;
                else
                {
                    _v4DisEnd++;
                    int fr = Math.Max(1, _v4Free0[idx]);
                    _v4AtPointSum += Math.Min(1.0, (double)V445AtPointCount(f, _v4P[idx], V445AtPoint) / fr);
                    V445LossMeasure(f, idx, now);
                }
            }
            try
            {
                if (f != null)
                {
                    byte r = C441RoleOf(f);
                    if (r == CrCavFirst || r == CrDisengage) C441SetRole(f, CrNone, default(MovementOrder));
                }
            }
            catch (Exception e) { _v4Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.CavFirst445 oddanie", e); }
            V445SlotClear(idx);
            _v4Exit[idx] = now;
            if (mode == VmDis) _v4Cool[idx] = now + V445Cool;
        }

        /// <summary>Wynik CAVFIRST: strata celu i naszej jazdy od poczatku roli (445-p: zabici i ranni, nie spadek CountOfUnits).</summary>
        private static void V445Score(Formation f, int idx)
        {
            int t0 = _v4TgtMen0[idx], c0 = _v4Men0[idx];
            if (t0 <= 0 || c0 <= 0) return;
            _v4TgtLossSum += Math.Min(1.0, (double)_v4TgtKill[idx] / t0);
            _v4OwnLossSum += Math.Min(1.0, (double)_v4OwnKill[idx] / c0);
            _v4CavDone++;
        }

        /// <summary>Punkt zbiorki: 60 m od wroga po linii wrog - jazda, potem 40 m, potem +/- 35 stopni (60 m); na siatce nawigacji, z dala od innych szykow wroga,
        /// 445-p: w granicach pola i >= 15 m od krawedzi (przy granicy gra wygasza agentow - jak H439InField / RoutHold.Withdraw).</summary>
        private static bool V445RallyPoint(Mission m, Team t, Formation f, Formation foe, Vec2 me, out Vec2 p, out WorldPosition wp)
        {
            p = Vec2.Invalid;
            wp = default(WorldPosition);
            Vec2 ep = foe.CachedMedianPosition.AsVec2;
            Vec2 away = ep.IsValid ? me - ep : Vec2.Invalid;
            if (!away.IsValid || away.LengthSquared < 1f)
            {
                Vec2 d = f.Direction;
                if (!d.IsValid || d.LengthSquared < 0.0001f) return false;
                away = Vec2.Zero - d;
            }
            away = away.Normalized();
            float cs = (float)Math.Cos(V445Turn), sn = (float)Math.Sin(V445Turn);
            for (int k = 0; k < 4; k++)
            {
                Vec2 dir = away;
                float dist = k == 1 ? V445Back2 : V445Back;
                if (k == 2) dir = new Vec2(away.x * cs - away.y * sn, away.x * sn + away.y * cs);
                else if (k == 3) dir = new Vec2(away.x * cs + away.y * sn, -away.x * sn + away.y * cs);
                Vec2 c = me + dir * dist;
                if (!m.IsPositionInsideBoundaries(c) || m.GetClosestBoundaryPosition(c).Distance(c) < V445Edge) continue;
                if (!V445FoeClear(t, foe, c)) continue;
                WorldPosition w = f.CachedMedianPosition;
                w.SetVec2(c);
                if (!w.IsValid || w.GetNavMesh() == UIntPtr.Zero) continue;
                p = c; wp = w;
                return true;
            }
            return false;
        }

        /// <summary>Punkt nie przy innym szyku wroga (>= 5): odleglosc od jego srodka > pol szerokosci + 30 m.</summary>
        private static bool V445FoeClear(Team t, Formation foe, Vec2 c)
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
                    if (e == null || ReferenceEquals(e, foe) || e.CountOfUnits < R443CavMin) continue;
                    Vec2 ep = e.CachedMedianPosition.AsVec2;
                    if (!ep.IsValid) continue;
                    if (ep.Distance(c) < 0.5f * Math.Max(1f, e.Width) + V445FoeGap) return false;
                }
            }
            return true;
        }

        private static float V445MeleeOf(Formation f)
        {
            float v;
            if (_v4Melee.TryGetValue(f, out v)) return v;
            v = T442MeleeShare(f);
            _v4Melee[f] = v;
            return v;
        }

        // ---- jezdzcy zwiazani (rbmai PrefixGetOrderPositionOfUnit: > 2 jezdzcow wroga w 15 m albo > 2 pieszych w 7 m - rozkaz szyku martwy)
        private static Mission _v4ScanM;
        private static Team _v4ScanT;
        private static int _v4ScanAll, _v4ScanIn;
        private static Vec2 _v4ScanP;
        private static float _v4ScanR2;

        private static readonly Action<Agent> V445TangleMan = a =>
        {
            if (a == null || !a.IsActive() || a.MountAgent == null) return;
            _v4ScanAll++;
            Vec2 p = a.Position.AsVec2;
            _v4ScanM.GetNearbyEnemyAgents(p, V445TangleRider, _v4ScanT, _v4Buf);
            int riders = 0, foot = 0;
            for (int i = 0; i < _v4Buf.Count; i++)
            {
                // 445-p: filtr jak RBM (rbmai 5073-5079: tylko MountAgent != null / == null, bez IsHuman / IsActive) - jesli silnik zwraca konie, RBM liczy je
                // jako pieszych w 7 m i 445 tez
                var x = _v4Buf[i];
                if (x == null) continue;
                if (x.MountAgent != null) riders++;
                else if (p.DistanceSquared(x.Position.AsVec2) <= V445TangleFoot * V445TangleFoot) foot++;
            }
            if (riders > 2 || foot > 2) _v4ScanIn++;
        };

        /// <summary>Udzial jezdzcow szyku, ktorych RBM trzyma w walce wolnej (rozkaz Move ich nie ruszy); free - jezdzcy wolni.</summary>
        private static float V445Tangled(Mission m, Formation f, out int free)
        {
            _v4ScanM = m; _v4ScanT = f.Team; _v4ScanAll = _v4ScanIn = 0;
            try { f.ApplyActionOnEachUnit(V445TangleMan); }
            finally { _v4ScanM = null; _v4ScanT = null; _v4Buf.Clear(); }
            free = _v4ScanAll - _v4ScanIn;
            return _v4ScanAll > 0 ? (float)_v4ScanIn / _v4ScanAll : 1f;
        }

        private static readonly Action<Agent> V445AtPointMan = a =>
        {
            if (a == null || !a.IsActive() || a.MountAgent == null) return;
            if (a.Position.AsVec2.DistanceSquared(_v4ScanP) <= _v4ScanR2) _v4ScanIn++;
        };

        /// <summary>445-p: liczba jezdzcow (na koniu, jak "wolni" przy starcie) szyku w r od punktu zbiorki.</summary>
        private static int V445AtPointCount(Formation f, Vec2 p, float r)
        {
            if (f == null || !p.IsValid) return 0;
            _v4ScanP = p; _v4ScanR2 = r * r; _v4ScanIn = 0;
            f.ApplyActionOnEachUnit(V445AtPointMan);
            return _v4ScanIn;
        }

        // ------------------------------------------------------------ linia konca bitwy (Battle291.Write)

        internal static string V445Line(string kind)
        {
            var c = Settings.Current;
            var sb = new StringBuilder(1400);
            sb.Append("Jazda na jazde i oderwanie (445): misja ").Append(kind ?? "-")
              .Append(" | klucze: najpierw jazda wroga ").Append(c != null && c.CavalryFightsCavalryFirst ? "TAK" : "NIE")
              .Append(", oderwanie ").Append(c != null && c.CavalryDisengage ? "TAK" : "NIE")
              .Append(", dowodca jazdy ").Append(c != null && c.CavalryCommander ? "TAK" : "NIE")
              .Append(", profil wodza ").Append(c != null && c.CommanderPersonalityInBattle ? "TAK" : "NIE")
              .Append(" | progi (OSTROZNY / ZWYKLY / PORYWCZY): przewaga konna x").Append(V4Adv[0].ToString("0.0", V4Inv)).Append(" / x").Append(V4Adv[1].ToString("0.0", V4Inv))
              .Append(" / x").Append(V4Adv[2].ToString("0.0", V4Inv)).Append(", kontrszarza szyk >= ").Append(K444Pct(V4Match[0])).Append(" / ").Append(K444Pct(V4Match[1]))
              .Append(" / ").Append(K444Pct(V4Match[2])).Append(" wroga, oderwanie strata w 10 s >= ").Append(K444Pct(V4Bleed[0])).Append(" / ").Append(K444Pct(V4Bleed[1]))
              .Append(" / ").Append(K444Pct(V4Bleed[2]))
              .Append(" | CAVFIRST: wejsc ").Append(_v4CavN).Append(" (kontrszarza na jazde idaca na strzelcow ").Append(_v4CounterN).Append(", przewaga konna ").Append(_v4AdvN)
              .Append("; z czekania 444 ").Append(_v4FromWait).Append("; ponownie na ten sam cel ").Append(_v4SameTgt).Append("), bylby (klucz NIE / bez dowodcy jazdy): kontrszarza ")
              .Append(_v4CounterDry).Append(", przewaga ").Append(_v4AdvDry)
              .Append("; za slaby na kontrszarze ").Append(_v4Weak).Append(", kontruje inny szyk (blizszy / juz na celu) ").Append(_v4CounterOther)
              .Append(", za slaby na przewage ").Append(_v4AdvWeak)
              .Append(", cel zasloniety piechota (szyko-takty) ").Append(_v4Blocked).Append(", jazda wroga przy swojej piechocie (szyko-takty) ").Append(_v4NearFoot)
              .Append(", limit ").Append(V445CavMaxN).Append(" wejsc na szyk ").Append(_v4CavCap).Append(", szyk stracil ").Append(K444Pct(V445Loss)).Append(" w CAVFIRST ").Append(_v4CavSpent)
              .Append(", ").Append(_v4CavSec.ToString("0", V4Inv)).Append(" szyko-s")
              .Append(" | zwarc ").Append(_v4Contact).Append(" (kontrszarza przechwycila srednio ")
              .Append(_v4InterN > 0 ? (_v4InterSum / _v4InterN).ToString("0", V4Inv) + " m" : "-").Append(" od naszych strzelcow, ").Append(_v4InterN).Append(')')
              .Append(" | koniec: jazda wroga rozbita (zabici i ranni >= ").Append(K444Pct(V445Broken)).Append(") ").Append(_v4CavBroken)
              .Append(", szyk celu zniknal / < 5 / nie wrog ").Append(_v4CavGone)
              .Append(", straty naszej jazdy ").Append(_v4CavLoss).Append(", czas ").Append(_v4CavTime)
              .Append(", bez zwarcia ").Append(_v4NoContact).Append(", odjechala ").Append(_v4Fled).Append(", klucz NIE w trakcie ").Append(_v4CavKeyOff)
              .Append(", oddana (gracz / szyk pusty / 441) ").Append(_v4CavBack).Append(", w toku przy koncu ").Append(_v4CavOpen)
              .Append(" | wynik (zabici i ranni): jazda wroga stracila srednio ").Append(_v4CavDone > 0 ? (100.0 * _v4TgtLossSum / _v4CavDone).ToString("0", V4Inv) + "%" : "-")
              .Append(", nasza srednio ").Append(_v4CavDone > 0 ? (100.0 * _v4OwnLossSum / _v4CavDone).ToString("0", V4Inv) + "%" : "-")
              .Append(" | DISENGAGE: oderwan ").Append(_v4DisN0).Append(", bylby (klucz NIE / bez dowodcy jazdy) ").Append(_v4DisDry)
              .Append(", zwiazani - nie posluchaliby (>= ").Append(K444Pct(V445TangleMax)).Append(" jezdzcow; epizody) ").Append(_v4DisTangled)
              .Append(", brak punktu zbiorki (siatka / granica / szyk wroga) ").Append(_v4DisNoSpot).Append(", limit ").Append(V445MaxDis).Append(" na szyk ").Append(_v4DisCap)
              .Append(", ").Append(_v4DisSec.ToString("0", V4Inv)).Append(" szyko-s; zwiazanych przy oderwaniu srednio ")
              .Append(_v4DisN0 > 0 ? (100.0 * _v4TangleSum / _v4DisN0).ToString("0", V4Inv) + "%" : "-")
              .Append(" | dojechali do punktu (>= ").Append(K444Pct(V445ReachShare)).Append(" wolnych w ").Append(V445Reach.ToString("0", V4Inv)).Append(" m) ").Append(_v4DisReached)
              .Append(", po ").Append(V445Ride.ToString("0", V4Inv)).Append(" s bez dojazdu ").Append(_v4DisRideOut)
              .Append(", po formowaniu oddane RBM ").Append(_v4DisDone).Append(", klucz NIE w trakcie ").Append(_v4DisKeyOff)
              .Append(", oddane (gracz / szyk pusty / 441) ").Append(_v4DisBack).Append(", w toku przy koncu ").Append(_v4DisOpen)
              .Append(" | przy punkcie na koncu srednio ").Append(_v4DisEnd > 0 ? (100.0 * _v4AtPointSum / _v4DisEnd).ToString("0", V4Inv) + "%" : "-")
              .Append(" wolnych jezdzcow; strata w 10 s (ta sama populacja ").Append(_v4LossN).Append("): przed srednio ")
              .Append(_v4LossN > 0 ? (_v4LossBefore / _v4LossN).ToString("0.0", V4Inv) : "-")
              .Append(", w pierwszych 10 s roli srednio ").Append(_v4LossN > 0 ? (_v4LossDuring / _v4LossN).ToString("0.0", V4Inv) : "-")
              .Append(" | naraz maks: CAVFIRST ").Append(_v4MaxCav).Append(", DISENGAGE ").Append(_v4MaxDis)
              .Append(" | rajd 443 wstrzymany (rol 445) ").Append(_v4Keep443).Append(", rola odrzucona ").Append(_v4Refused)
              .Append(" | potkniecia ").Append(_v4Stum).Append('.');
            return sb.ToString();
        }
    }
}
