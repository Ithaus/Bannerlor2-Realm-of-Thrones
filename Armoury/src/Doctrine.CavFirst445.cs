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
    /// </summary>
    internal static partial class Doctrine
    {
        internal const float V445Every = 1f, V445Range = 300f, V445Lost = 350f, V445Eta = 15f, V445Near = 30f, V445Dot = 0.33f, V445Speed = 2f;
        internal const float V445NoContact = 40f, V445NoContactWait = 35f, V445Max = 90f, V445Loss = 0.4f, V445Broken = 0.5f, V445Reenter = 10f, V445Contact = 0.2f;
        internal const float V445StuckSpeed = 3f, V445EngGap = 10f, V445Back = 60f, V445Back2 = 40f, V445Turn = 0.61f, V445Reach = 25f, V445Ride = 30f;
        internal const float V445Reform = 6f, V445Cool = 30f, V445TangleFoot = 7f, V445TangleRider = 15f, V445TangleMax = 0.5f, V445AtPoint = 30f, V445FoeGap = 30f;
        internal const int V445MaxDis = 3, V445MinLoss = 3;
        private const int V4R = 11;   // pierscien liczby ludzi slotu: 10 s (probka co takt 1 s)
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
        private static readonly float[] _v4Since = new float[CrSlots], _v4Exit = new float[CrSlots], _v4DryC = new float[CrSlots], _v4DryD = new float[CrSlots];
        private static readonly float[] _v4ContactAt = new float[CrSlots], _v4Reached = new float[CrSlots], _v4Cool = new float[CrSlots];
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
        private static float _v4CavSec, _v4DisSec;
        private static double _v4TgtLossSum, _v4OwnLossSum, _v4InterSum, _v4TangleSum, _v4AtPointSum, _v4LossBefore, _v4LossDuring;

        /// <summary>Battle291.Begin (AfterStart): liczniki linii 445 od zera.</summary>
        internal static void V445Reset()
        {
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
                _v4Who[i] = null; _v4Exit[i] = _v4DryC[i] = _v4DryD[i] = _v4Cool[i] = -1000f; _v4DisN[i] = 0;
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
            _v4Keep443++;
            return true;
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
                _v4Who[idx] = f; _v4Exit[idx] = _v4DryC[idx] = _v4DryD[idx] = _v4Cool[idx] = -1000f; _v4DisN[idx] = 0;
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
            if (mode == VmCav) { _v4CavSec += step; V445CavTick(t, f, idx, me, own, now); return; }
            if (mode == VmDis) { _v4DisSec += step; V445DisTick(f, idx, me, now); return; }
            if (now - _v4Exit[idx] < V445Reenter) return;
            if ((role == CrNone || role == CrWait) && own < T442OwnMelee) { V445TryCav(t, f, idx, me, prof, role, now); return; }
            if (role == CrNone && own >= T442OwnMelee) V445TryDis(m, t, f, idx, me, cnt, old, prof, now);
        }

        // ------------------------------------------------------------ 4.4 CAVFIRST

        private static void V445TryCav(Team t, Formation f, int idx, Vec2 me, sbyte prof, byte role, float now)
        {
            int ti = t.TeamIndex;
            Formation tg = null;
            bool counter = false;
            float dArch;
            var thr = V445Threat(t, me, out dArch);
            if (thr != null)
            {
                if (f.CountOfUnits >= V4Match[prof] * thr.CountOfUnits) { tg = thr; counter = true; }
                else if (now >= _v4DryC[idx]) { _v4Weak++; _v4DryC[idx] = now + R443Latch; }
            }
            if (tg == null && ti >= 0 && ti < MaxTeams && _v4FoeCav[ti] >= R443CavMin && _v4OurCav[ti] >= V4Adv[prof] * _v4FoeCav[ti])
                tg = V445NearestCav(t, me);
            if (tg == null) return;
            if (!_v4KCav || !C441Live)
            {
                if (now >= _v4DryC[idx]) { if (counter) _v4CounterDry++; else _v4AdvDry++; _v4DryC[idx] = now + R443Latch; }
                return;
            }
            if (!C441SetRole(f, CrCavFirst, MovementOrder.MovementOrderChargeToTarget(tg)))
            {
                if (now >= _v4DryC[idx]) { _v4Refused++; _v4DryC[idx] = now + R443Latch; }
                return;
            }
            _v4Mode[idx] = VmCav; _v4Since[idx] = now; _v4Tgt[idx] = tg; _v4Counter[idx] = counter;
            _v4ContactAt[idx] = -1f; _v4Men0[idx] = f.CountOfUnits; _v4TgtMen0[idx] = tg.CountOfUnits;
            _v4CavN++;
            if (counter) _v4CounterN++; else _v4AdvN++;
            if (role == CrWait) _v4FromWait++;
        }

        private static void V445CavTick(Team t, Formation f, int idx, Vec2 me, float own, float now)
        {
            var tg = _v4Tgt[idx];
            int men0 = _v4TgtMen0[idx];
            bool alive = tg != null && tg.CountOfUnits >= R443CavMin && MissionTeams.Enemies(t, tg.Team);
            if (!alive || (men0 > 0 && tg.CountOfUnits <= (1f - V445Broken) * men0)) { _v4CavBroken++; V445End(f, idx, now, 1); return; }
            if (f.CountOfUnits < (1f - V445Loss) * _v4Men0[idx]) { _v4CavLoss++; V445End(f, idx, now, 1); return; }
            if (now - _v4Since[idx] > V445Max) { _v4CavTime++; V445End(f, idx, now, 1); return; }
            Vec2 ep = tg.CachedAveragePosition;
            if (ep.IsValid && me.Distance(ep) > V445Lost) { _v4Fled++; V445End(f, idx, now, 1); _v4Exit[idx] = now + V445NoContactWait; return; }
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
            if (now - _v4Since[idx] > V445NoContact) { _v4NoContact++; V445End(f, idx, now, 1); _v4Exit[idx] = now + V445NoContactWait; }
        }

        /// <summary>Kontrszarza: szyk jazdy wroga (>= 5, nie konni strzelcy) w 300 m od nas, jadacy na nasz pieszy szyk strzelcow (dojazd <= 15 s) albo w 30 m
        /// od niego, niezasloniety piechota wroga - najblizszy nam. dArch - odstep od zagrozonych strzelcow.</summary>
        private static Formation V445Threat(Team t, Vec2 me, out float dArch)
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
                    if (T442Blocked(t, e, me, ep)) { _v4Blocked++; continue; }
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

        /// <summary>Przewaga konna: najblizszy szyk jazdy wroga (>= 5, nie konni strzelcy) w 300 m, niezasloniety piechota wroga.</summary>
        private static Formation V445NearestCav(Team t, Vec2 me)
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
                    if (e == null || e.CountOfUnits < R443CavMin) continue;
                    var eq = e.QuerySystem;
                    if (eq == null || !eq.IsCavalryFormation || eq.IsRangedCavalryFormation) continue;
                    Vec2 ep = e.CachedAveragePosition;
                    if (!ep.IsValid) continue;
                    float d = me.Distance(ep);
                    if (d > V445Range || d >= bd) continue;
                    if (T442Blocked(t, e, me, ep)) { _v4Blocked++; continue; }
                    best = e; bd = d;
                }
            }
            return best;
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
            if (R443GapOf(f) > V445EngGap) return;
            // stoi i traci ludzi
            Vec2 v = f.CachedCurrentVelocity;
            float sp = v.IsValid ? v.Length : 0f;
            if (sp >= V445StuckSpeed) return;
            int lost = old - cnt;
            if (lost < V445MinLoss || lost < V4Bleed[prof] * old) return;
            if (_v4DisN[idx] >= V445MaxDis)
            {
                if (now >= _v4DryD[idx]) { _v4DisCap++; _v4DryD[idx] = now + R443Latch; }
                return;
            }
            float tangled = V445Tangled(m, f);
            if (tangled >= V445TangleMax)
            {
                if (now >= _v4DryD[idx]) { _v4DisTangled++; _v4DryD[idx] = now + V445Reenter; }
                return;
            }
            Vec2 p;
            WorldPosition wp;
            if (!V445RallyPoint(t, f, nf, me, out p, out wp))
            {
                if (now >= _v4DryD[idx]) { _v4DisNoSpot++; _v4DryD[idx] = now + V445Reenter; }
                return;
            }
            if (!_v4KDis || !C441Live)
            {
                if (now >= _v4DryD[idx]) { _v4DisDry++; _v4DryD[idx] = now + R443Latch; }
                return;
            }
            if (!C441SetRole(f, CrDisengage, MovementOrder.MovementOrderMove(wp)))
            {
                if (now >= _v4DryD[idx]) { _v4Refused++; _v4DryD[idx] = now + R443Latch; }
                return;
            }
            _v4Mode[idx] = VmDis; _v4Since[idx] = now; _v4P[idx] = p; _v4Reached[idx] = -1f; _v4Men0[idx] = cnt;
            _v4DisN[idx]++;
            _v4DisN0++;
            _v4TangleSum += tangled;
            _v4LossBefore += lost;
        }

        private static void V445DisTick(Formation f, int idx, Vec2 me, float now)
        {
            if (_v4Reached[idx] < 0f)
            {
                if (_v4P[idx].IsValid && me.Distance(_v4P[idx]) <= V445Reach) { _v4Reached[idx] = now; _v4DisReached++; }
                else if (now - _v4Since[idx] > V445Ride) { _v4DisRideOut++; V445End(f, idx, now, 2); return; }
                return;
            }
            if (now - _v4Reached[idx] >= V445Reform) { _v4DisDone++; V445End(f, idx, now, 2); }
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
                    _v4AtPointSum += V445AtPointShare(f, _v4P[idx]);
                    int c0 = _v4Men0[idx];
                    _v4LossDuring += Math.Max(0, c0 - (f != null ? f.CountOfUnits : 0));
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

        /// <summary>Wynik CAVFIRST: strata celu i naszej jazdy od poczatku roli.</summary>
        private static void V445Score(Formation f, int idx)
        {
            int t0 = _v4TgtMen0[idx], c0 = _v4Men0[idx];
            if (t0 <= 0 || c0 <= 0) return;
            var tg = _v4Tgt[idx];
            int tl = tg != null ? Math.Max(0, tg.CountOfUnits) : 0;
            int cl = f != null ? Math.Max(0, f.CountOfUnits) : 0;
            _v4TgtLossSum += Math.Max(0.0, 1.0 - (double)tl / t0);
            _v4OwnLossSum += Math.Max(0.0, 1.0 - (double)cl / c0);
            _v4CavDone++;
        }

        /// <summary>Punkt zbiorki: 60 m od wroga po linii wrog - jazda, potem 40 m, potem +/- 35 stopni (60 m); na siatce nawigacji, z dala od innych szykow wroga.</summary>
        private static bool V445RallyPoint(Team t, Formation f, Formation foe, Vec2 me, out Vec2 p, out WorldPosition wp)
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

        private static readonly Action<Agent> V445TangleMan = a =>
        {
            if (a == null || !a.IsActive() || a.MountAgent == null) return;
            _v4ScanAll++;
            Vec2 p = a.Position.AsVec2;
            _v4ScanM.GetNearbyEnemyAgents(p, V445TangleRider, _v4ScanT, _v4Buf);
            int riders = 0, foot = 0;
            for (int i = 0; i < _v4Buf.Count; i++)
            {
                var x = _v4Buf[i];
                if (x == null || !x.IsHuman || !x.IsActive()) continue;
                if (x.MountAgent != null) riders++;
                else if (p.DistanceSquared(x.Position.AsVec2) <= V445TangleFoot * V445TangleFoot) foot++;
            }
            if (riders > 2 || foot > 2) _v4ScanIn++;
        };

        /// <summary>Udzial jezdzcow szyku, ktorych RBM trzyma w walce wolnej (rozkaz Move ich nie ruszy).</summary>
        private static float V445Tangled(Mission m, Formation f)
        {
            _v4ScanM = m; _v4ScanT = f.Team; _v4ScanAll = _v4ScanIn = 0;
            try { f.ApplyActionOnEachUnit(V445TangleMan); }
            finally { _v4ScanM = null; _v4ScanT = null; _v4Buf.Clear(); }
            return _v4ScanAll > 0 ? (float)_v4ScanIn / _v4ScanAll : 1f;
        }

        private static readonly Action<Agent> V445AtPointMan = a =>
        {
            if (a == null || !a.IsActive()) return;
            _v4ScanAll++;
            if (a.Position.AsVec2.DistanceSquared(_v4ScanP) <= V445AtPoint * V445AtPoint) _v4ScanIn++;
        };

        /// <summary>Udzial ludzi szyku w 30 m od punktu zbiorki (ilu sie oderwalo).</summary>
        private static float V445AtPointShare(Formation f, Vec2 p)
        {
            if (f == null || !p.IsValid) return 0f;
            _v4ScanP = p; _v4ScanAll = _v4ScanIn = 0;
            f.ApplyActionOnEachUnit(V445AtPointMan);
            return _v4ScanAll > 0 ? (float)_v4ScanIn / _v4ScanAll : 0f;
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
              .Append("; z czekania 444 ").Append(_v4FromWait).Append("), bylby (klucz NIE / bez dowodcy jazdy): kontrszarza ").Append(_v4CounterDry).Append(", przewaga ").Append(_v4AdvDry)
              .Append("; za slaby na kontrszarze ").Append(_v4Weak).Append(", cel zasloniety piechota (szyko-takty) ").Append(_v4Blocked)
              .Append(", ").Append(_v4CavSec.ToString("0", V4Inv)).Append(" szyko-s")
              .Append(" | zwarc ").Append(_v4Contact).Append(" (kontrszarza przechwycila srednio ")
              .Append(_v4InterN > 0 ? (_v4InterSum / _v4InterN).ToString("0", V4Inv) + " m" : "-").Append(" od naszych strzelcow, ").Append(_v4InterN).Append(')')
              .Append(" | koniec: jazda wroga rozbita ").Append(_v4CavBroken).Append(", straty naszej jazdy ").Append(_v4CavLoss).Append(", czas ").Append(_v4CavTime)
              .Append(", bez zwarcia ").Append(_v4NoContact).Append(", odjechala ").Append(_v4Fled).Append(", oddana (gracz / szyk pusty / 441) ").Append(_v4CavBack)
              .Append(", w toku przy koncu ").Append(_v4CavOpen)
              .Append(" | wynik: jazda wroga stracila srednio ").Append(_v4CavDone > 0 ? (100.0 * _v4TgtLossSum / _v4CavDone).ToString("0", V4Inv) + "%" : "-")
              .Append(", nasza srednio ").Append(_v4CavDone > 0 ? (100.0 * _v4OwnLossSum / _v4CavDone).ToString("0", V4Inv) + "%" : "-")
              .Append(" | DISENGAGE: oderwan ").Append(_v4DisN0).Append(", bylby (klucz NIE / bez dowodcy jazdy) ").Append(_v4DisDry)
              .Append(", zwiazani - nie posluchaliby (>= ").Append(K444Pct(V445TangleMax)).Append(" jezdzcow) ").Append(_v4DisTangled)
              .Append(", brak punktu zbiorki ").Append(_v4DisNoSpot).Append(", limit ").Append(V445MaxDis).Append(" na szyk ").Append(_v4DisCap)
              .Append(", ").Append(_v4DisSec.ToString("0", V4Inv)).Append(" szyko-s; zwiazanych przy oderwaniu srednio ")
              .Append(_v4DisN0 > 0 ? (100.0 * _v4TangleSum / _v4DisN0).ToString("0", V4Inv) + "%" : "-")
              .Append(" | dojechali do punktu ").Append(_v4DisReached).Append(", po ").Append(V445Ride.ToString("0", V4Inv)).Append(" s bez dojazdu ").Append(_v4DisRideOut)
              .Append(", po formowaniu oddane RBM ").Append(_v4DisDone).Append(", oddane (gracz / szyk pusty / 441) ").Append(_v4DisBack).Append(", w toku przy koncu ").Append(_v4DisOpen)
              .Append(" | przy punkcie na koncu srednio ").Append(_v4DisEnd > 0 ? (100.0 * _v4AtPointSum / _v4DisEnd).ToString("0", V4Inv) + "%" : "-")
              .Append(" ludzi; strata 10 s przed srednio ").Append(_v4DisN0 > 0 ? (_v4LossBefore / _v4DisN0).ToString("0.0", V4Inv) : "-")
              .Append(", w trakcie srednio ").Append(_v4DisEnd > 0 ? (_v4LossDuring / _v4DisEnd).ToString("0.0", V4Inv) : "-")
              .Append(" | naraz maks: CAVFIRST ").Append(_v4MaxCav).Append(", DISENGAGE ").Append(_v4MaxDis)
              .Append(" | rajd 443 wstrzymany ").Append(_v4Keep443).Append(", rola odrzucona ").Append(_v4Refused)
              .Append(" | potkniecia ").Append(_v4Stum).Append('.');
            return sb.ToString();
        }
    }
}
