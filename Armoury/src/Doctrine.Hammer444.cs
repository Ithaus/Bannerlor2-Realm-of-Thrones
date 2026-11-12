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
    /// 444 = PAKIET 4 krok 4.3 audytu AI bitwy (docs/audyty-2026-11-11/AUDYT-AI-BITWA.md rozdz. 3 "Jazda" 4.3; Jeff 11.11 "rob wszystko po kolei tak jak radzisz",
    /// "aby bitwa nie byla wciaz taka sama ... AI bardziej kombinowalo"). Kowadlo i mlot: jazda czeka, az cel ma >= 50% ludzi w walce (zwiazany z nasza piechota),
    /// i dopiero wtedy uderza z boku lub z tylu. Klucz HammerAndAnvil ("Hammer And Anvil", domyslnie TAK), wymaga dowodcy jazdy 441 (CavalryCommander).
    /// Zadnych bonusow do liczb - tylko decyzje. Bitwa polowa, nie morska. Glowny watek (Doctrine.Tick, wlasny zegar 1 s, po 443, przed 442).
    ///
    /// Szyk: jazda AI (IsCavalryFormation, nie konni strzelcy, >= 5, IsAIControlled, nie szyk gracza / z czlowiekiem gracza), druzyna TeamAIGeneral obu stron.
    /// Szyk w innej roli 441 (RAID / HIDE 443) - nie ruszany; WAIT oddaje szyk rajdowi / schowaniu 443 (ostrzal ma pierwszenstwo - decyzja Jeffa 11.11),
    /// STRIKE nie (443 nie zaczyna rajdu szykiem w STRIKE - licznik "443 wstrzymany").
    /// Cel uderzenia ("kowadlo"): pieszy szyk wroga (piechota albo strzelcy, >= 8, nie jazda) w 300 m, ktorego najblizszy szyk wroga (dla niego) to nasz pieszy
    ///   szyk, z udzialem ludzi w zwarciu (miara 442: cios wrecz zadany / otrzymany w 6 s) >= progu profilu wodza 440: PORYWCZY 35%, ZWYKLY 50%, OSTROZNY 65%
    ///   (klucz Commander Personality In Battle NIE albo brak profilu - 50%). Najblizszy jezdzie.
    /// WAIT (rola 441 CrWait, klus - bez wyjatku klusa): przed pierwszym uderzeniem szyku, gdy nie ma kowadla, jazda poza zwarciem (< 30% wrecz, odstep >= 40 m),
    ///   druzyna ma glowna piechote (najwiekszy szyk piechoty, >= 10 i >= pol tej jazdy), a wrog pieszy szyk (>= 8). Punkt: przy boku glownej piechoty po
    ///   stronie, po ktorej stoi jazda (pol szerokosci piechoty + pol szerokosci jazdy + 15 m w bok, 10 m w tyl od linii w strone najblizszego pieszego wroga),
    ///   na siatce nawigacji (inaczej za piechota); rozkaz Move odswiezany, gdy punkt przesunie sie o 10 m. Koniec: kowadlo -> STRIKE od razu; limit czekania
    ///   wedlug profilu (PORYWCZY 90 s, ZWYKLY 150 s, OSTROZNY 210 s) - potem szyk juz nie czeka (RBM jak dotad); jazda w zwarciu / wrog w 15 m - oddana RBM
    ///   (ponowne czekanie najwczesniej po 20 s); brak glownej piechoty albo pieszego wroga - oddana.
    /// STRIKE (rola 441 CrStrike, galop): jazda poza zwarciem (< 30%, odstep >= 15 m), kowadlo istnieje. Jazda juz z boku / z tylu celu (cos kata wzgledem
    ///   czola celu <= 0.5) i droga wprost niezaslonieta (T442Blocked) -> ChargeToTarget od razu; inaczej punkt dojazdu: bok blizszy jezdzie (pol szerokosci
    ///   celu + 30 m w bok, pol glebokosci w tyl), drugi bok, tyl (pol glebokosci + 40 m) - oba odcinki niezasloniete, na siatce -> Move galopem (wyjatek klusa
    ///   441); w 30 m od punktu, jazda z boku / z tylu (cos <= 0.2) z otwarta droga albo 40 s dojazdu -> ChargeToTarget; brak punktu - szarza wprost (licznik).
    ///   Jezdzcy trzymaja cel: slot 442 (K444AimTarget w T442Form - postfiks GetCorrectTarget 442, fakt 3; przy kluczu 442 NIE tylko "bylby").
    ///   Koniec: 10 s po pierwszym zwarciu - oddana RBM (442 wybiera dalej; zwiazany cel x1.5), cel rozbity (polowa ludzi / < 8), jazda stracila 40%,
    ///   40 s szarzy bez zwarcia, 90 s calosci. Nowa rola najwczesniej 10 s po oddaniu. Po pierwszym uderzeniu szyk nie czeka juz (bez wahadla
    ///   czekanie - walka - czekanie); kolejne uderzenia, gdy jazda znow wolna, a kowadlo jest.
    /// Pomiar: kat wejscia - przy pierwszym zwarciu w szarzy (jezdziec szyku z ciosem wrecz zadanym / otrzymanym od chwili szarzy, najblizszy srodkowi celu)
    ///   strona wzgledem czola celu: cos > 0.5 czolo, < -0.5 tyl, reszta bok (te same progi co "pierwszy cios szarzy" 437 - ten nie liczy rozkazu
    ///   ChargeToTarget roli, wiec liczymy sami); czas czekania (suma, srednio wedlug profilu); wynik (strata celu i jazdy w %, powody konca).
    /// Klucz NIE (albo bez dowodcy jazdy): decyzje liczone (tylko odczyt), role nie nadawane - co do znaku jak dotad; liczniki "bylby".
    /// Watki: wszystko w glownym watku (skan szykow, ApplyActionOnEachUnit ze statycznym delegatem, WorldPosition.SetVec2 / GetNavMesh); zachowanie roli 441
    ///   czyta tylko rozkaz. Wyjatki per szyk liczone (potkniecia, rola oddana, slot od zera), bez globalnego wylacznika.
    /// </summary>
    internal static partial class Doctrine
    {
        internal const float K444Every = 1f, K444Range = 300f, K444WaitGap = 40f, K444FreeGap = 15f, K444WaitSide = 15f, K444WaitBack = 10f, K444WaitMove = 10f;
        internal const float K444Reenter = 10f, K444Rewait = 20f, K444FrontDot = 0.5f, K444SideDot = 0.2f, K444FlankOff = 30f, K444RearOff = 40f, K444Reach = 30f;
        internal const float K444Approach = 40f, K444NoContact = 40f, K444AfterContact = 10f, K444Max = 90f, K444Stale = 30f, K444Loss = 0.4f, K444Broken = 0.5f;
        internal const float K444InfShare = 0.5f;
        // progi profilu wodza 440 (0 OSTROZNY, 1 ZWYKLY, 2 PORYWCZY): udzial celu w zwarciu i limit czekania
        private static readonly float[] K4Thr = { 0.65f, 0.5f, 0.35f };
        private static readonly float[] K4Cap = { 210f, 150f, 90f };
        private const byte KmNone = 0, KmWait = 1, KmStrike = 2;
        private const int KaFront = 0, KaSide = 1, KaRear = 2, KaK = 3;
        private static readonly string[] KaName = { "czolo", "bok", "tyl" };
        private static readonly IFormatProvider K4Inv = CultureInfo.InvariantCulture;

        // ---- stan slotow [TeamIndex x 10 + FormationIndex] (tylko glowny watek; K444AimTarget czyta T442Form - tez glowny)
        private static bool _k4Live;
        private static float _k4Acc;
        private static bool _k4Key;
        private static readonly Formation[] _k4Who = new Formation[CrSlots];
        private static readonly byte[] _k4Mode = new byte[CrSlots];
        private static readonly float[] _k4Since = new float[CrSlots], _k4Exit = new float[CrSlots], _k4DryWait = new float[CrSlots], _k4DryStrike = new float[CrSlots];
        private static readonly float[] _k4ChargeAt = new float[CrSlots], _k4ContactAt = new float[CrSlots];
        private static readonly Formation[] _k4Tgt = new Formation[CrSlots];
        private static readonly bool[] _k4Flank = new bool[CrSlots], _k4Struck = new bool[CrSlots], _k4WaitedOut = new bool[CrSlots], _k4FromWait = new bool[CrSlots];
        private static readonly Vec2[] _k4P = new Vec2[CrSlots], _k4TgtAt = new Vec2[CrSlots];
        private static readonly int[] _k4Men0 = new int[CrSlots], _k4TgtMen0 = new int[CrSlots];
        private static readonly sbyte[] _k4Prof = new sbyte[CrSlots];
        private static readonly Dictionary<Formation, float> _k4Melee = new Dictionary<Formation, float>();

        // ---- liczniki misji (K444Reset w Battle291.Begin)
        private static int _k4WaitN, _k4WaitDry, _k4WaitAnvil, _k4WaitCap, _k4WaitThreat, _k4WaitNoInf, _k4WaitNoFoe, _k4WaitTo443, _k4WaitBack, _k4WaitNoSpot;
        private static int _k4StrikeN, _k4StrikeDry, _k4StrikeFromWait, _k4StrikeFree, _k4Direct, _k4FlankN, _k4FlankToCharge, _k4FlankLate, _k4NoRoute, _k4Restale;
        private static int _k4Contact, _k4NoContact, _k4AfterC, _k4Broken, _k4CutLoss, _k4CutTime, _k4StrikeBack, _k4Open, _k4Keep443, _k4Stum, _k4Refused;
        private static int _k4MaxWait, _k4MaxStrike, _k4StrikeDone, _k4WaitTo445;
        private static readonly int[] _k4Angle = new int[KaK];
        private static readonly int[] _k4WaitProfN = new int[3];
        private static readonly float[] _k4WaitProfSec = new float[3];
        private static float _k4WaitSec, _k4StrikeSec;
        private static double _k4TgtLossSum, _k4CavLossSum;

        /// <summary>Battle291.Begin (AfterStart): liczniki linii 444 od zera.</summary>
        internal static void K444Reset()
        {
            _k4WaitN = _k4WaitDry = _k4WaitAnvil = _k4WaitCap = _k4WaitThreat = _k4WaitNoInf = _k4WaitNoFoe = _k4WaitTo443 = _k4WaitBack = _k4WaitNoSpot = 0;
            _k4StrikeN = _k4StrikeDry = _k4StrikeFromWait = _k4StrikeFree = _k4Direct = _k4FlankN = _k4FlankToCharge = _k4FlankLate = _k4NoRoute = _k4Restale = 0;
            _k4Contact = _k4NoContact = _k4AfterC = _k4Broken = _k4CutLoss = _k4CutTime = _k4StrikeBack = _k4Open = _k4Keep443 = _k4Stum = _k4Refused = 0;
            _k4MaxWait = _k4MaxStrike = _k4StrikeDone = _k4WaitTo445 = 0;
            for (int k = 0; k < KaK; k++) _k4Angle[k] = 0;
            for (int k = 0; k < 3; k++) { _k4WaitProfN[k] = 0; _k4WaitProfSec[k] = 0f; }
            _k4WaitSec = _k4StrikeSec = 0f;
            _k4TgtLossSum = _k4CavLossSum = 0.0;
        }

        /// <summary>Doctrine.Begin (AfterStart, po Clear): bitwa polowa, nie morska - pomiar zawsze, decyzje wedlug klucza (czytany co takt).</summary>
        private static void K444Begin(Mission m)
        {
            bool field = false;
            try { field = m != null && m.IsFieldBattle && !m.IsNavalBattle; } catch { field = false; }
            _k4Acc = 0f;
            _k4Live = field;
        }

        /// <summary>Doctrine.Clear (Begin / End): stan slotow od zera (role oddaje C441ClearMission). Uderzenia w toku przy koncu - licznik "w toku".</summary>
        private static void K444ClearMission()
        {
            _k4Live = false;
            for (int i = 0; i < CrSlots; i++)
            {
                if (_k4Mode[i] == KmStrike) _k4Open++;
                K444SlotClear(i);
                _k4Who[i] = null; _k4Exit[i] = _k4DryWait[i] = _k4DryStrike[i] = -1000f;
                _k4Struck[i] = false; _k4WaitedOut[i] = false;
            }
            _k4Melee.Clear();
            _k4Acc = 0f;
        }

        private static void K444SlotClear(int i)
        {
            _k4Mode[i] = KmNone; _k4Tgt[i] = null; _k4Flank[i] = false; _k4FromWait[i] = false;
            _k4Since[i] = 0f; _k4ChargeAt[i] = _k4ContactAt[i] = -1f;
            _k4P[i] = Vec2.Invalid; _k4TgtAt[i] = Vec2.Invalid; _k4Men0[i] = _k4TgtMen0[i] = 0; _k4Prof[i] = 1;
        }

        // ------------------------------------------------------------ odczyty dla 442 / 443 (glowny watek)

        /// <summary>T442Form: szyk w roli STRIKE w fazie szarzy - cel uderzenia (slot 442 jezdzcow); inaczej null.</summary>
        internal static Formation K444AimTarget(Formation f, int idx)
        {
            if (!_k4Live || f == null || idx < 0 || idx >= CrSlots) return null;
            if (_k4Mode[idx] != KmStrike || _k4Flank[idx] || !ReferenceEquals(_k4Who[idx], f)) return null;
            var tg = _k4Tgt[idx];
            return tg != null && tg.CountOfUnits > 0 ? tg : null;
        }

        /// <summary>R443Cav: szyk w STRIKE - 443 nie zaczyna rajdu / schowania (uderzenie w toku ma pierwszenstwo).</summary>
        internal static bool K444Striking(Formation f)
        {
            if (C441RoleOf(f) != CrStrike) return false;
            _k4Keep443++;
            return true;
        }

        // ------------------------------------------------------------ takt glownego watku (Doctrine.Tick)

        internal static void K444Tick(Mission m, float dt)
        {
            if (!_k4Live || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m) || m.MissionEnded) return;
            _k4Acc += dt;
            if (_k4Acc < K444Every) return;
            float step = _k4Acc;
            _k4Acc = 0f;
            bool dep;
            try { dep = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && m.CurrentTime >= P440NoDeploy); } catch { dep = false; }
            if (!dep) return;
            var c = Settings.Current;
            if (c == null) return;
            _k4Key = c.HammerAndAnvil;
            float now = m.CurrentTime;
            _t4Now = now;   // T442MeleeShare (glowny watek)
            _k4Melee.Clear();
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
                    try { K444Form(t, f, idx, now, step); }
                    catch (Exception e)
                    {
                        // potkniecie szyku: rola oddana (tylko nasza), slot od zera - bez globalnego wylacznika
                        _k4Stum++;
                        try { byte r = C441RoleOf(f); if (r == CrWait || r == CrStrike) C441SetRole(f, CrNone, default(MovementOrder)); } catch { }
                        K444SlotClear(idx);
                        _k4Exit[idx] = now;
                        RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Hammer444 szyk", e);
                    }
                }
            }
            // ile szykow naraz w rolach (martwy kod ozywiony - zasada 4)
            int nw = 0, ns = 0;
            for (int i = 0; i < CrSlots; i++) { if (_k4Mode[i] == KmWait) nw++; else if (_k4Mode[i] == KmStrike) ns++; }
            if (nw > _k4MaxWait) _k4MaxWait = nw;
            if (ns > _k4MaxStrike) _k4MaxStrike = ns;
        }

        private static void K444Form(Team t, Formation f, int idx, float now, float step)
        {
            if (!ReferenceEquals(_k4Who[idx], f))
            {
                K444SlotClear(idx);
                _k4Who[idx] = f; _k4Exit[idx] = _k4DryWait[idx] = _k4DryStrike[idx] = -1000f; _k4Struck[idx] = false; _k4WaitedOut[idx] = false;
            }
            var q = f.QuerySystem;
            bool cav = q != null && q.IsCavalryFormation && !q.IsRangedCavalryFormation;
            bool mine = f.CountOfUnits >= R443CavMin && f.IsAIControlled && !HorsePursuit.PlayerForm(f) && !f.HasPlayerControlledTroop;
            byte mode = _k4Mode[idx];
            if (!cav || !mine)
            {
                if (mode != KmNone) K444End(f, idx, now, mode == KmWait ? 0 : 9);
                return;
            }
            byte role = C441RoleOf(f);
            if (mode != KmNone && role != (mode == KmWait ? CrWait : CrStrike))
            {
                // rola zmieniona przez kogos innego: 443 (RAID / HIDE) albo 441 (gracz / szyk pusty) - stan 444 od zera, roli nie ruszamy
                if (mode == KmWait) { if (role == CrRaid || role == CrHide) _k4WaitTo443++; else if (role == CrCavFirst) _k4WaitTo445++; else _k4WaitBack++; }
                else { _k4StrikeBack++; K444Score(f, idx); }
                K444SlotClear(idx);
                _k4Exit[idx] = now;
                return;
            }
            if (mode == KmNone && role != CrNone) return;   // szyk w innej roli (443) - nie nasz
            Vec2 me = f.CachedAveragePosition;
            if (!me.IsValid) return;
            float own = K444MeleeOf(f);
            float gap = R443GapOf(f);
            sbyte prof = _cpKey ? P440Prof(t) : (sbyte)1;
            if (prof < 0 || prof > 2) prof = 1;
            float thr = K4Thr[prof];
            Formation anvil = K444FindAnvil(t, me, thr);
            bool free = own < T442OwnMelee && gap >= K444FreeGap;
            if (mode == KmNone)
            {
                if (now - _k4Exit[idx] < K444Reenter) return;
                if (anvil != null && free) { K444StartStrike(t, f, idx, anvil, me, now, false); return; }
                if (_k4Struck[idx] || _k4WaitedOut[idx] || own >= T442OwnMelee || gap < K444WaitGap) return;
                Formation inf, foe;
                if (!K444WaitOk(t, f, me, out inf, out foe)) return;
                Vec2 p;
                WorldPosition wp;
                if (!K444WaitPoint(f, inf, foe, me, out p, out wp))
                {
                    if (now >= _k4DryWait[idx]) { _k4WaitNoSpot++; _k4DryWait[idx] = now + R443Latch; }
                    return;
                }
                if (!_k4Key || !C441Live)
                {
                    if (now >= _k4DryWait[idx]) { _k4WaitDry++; _k4DryWait[idx] = now + R443Latch; }
                    return;
                }
                if (!C441SetRole(f, CrWait, MovementOrder.MovementOrderMove(wp)))
                {
                    if (now >= _k4DryWait[idx]) { _k4Refused++; _k4DryWait[idx] = now + R443Latch; }
                    return;
                }
                _k4Mode[idx] = KmWait; _k4Since[idx] = now; _k4P[idx] = p; _k4Prof[idx] = prof;
                _k4WaitN++;
                return;
            }
            if (mode == KmWait)
            {
                float waited = now - _k4Since[idx];
                _k4WaitSec += step;
                if (!_k4Key || !C441Live) { K444End(f, idx, now, 0); return; }
                // kowadlo przed zagrozeniem: przy zwarciu piechot cel bywa w 15 m od czekajacej jazdy - uderzenie, nie oddanie RBM
                if (anvil != null && own < T442OwnMelee)
                {
                    _k4WaitAnvil++;
                    K444WaitDone(idx, waited);
                    _k4Mode[idx] = KmNone;   // przejscie WAIT -> STRIKE bez oddania roli (Take nadpisuje rozkaz)
                    K444StartStrike(t, f, idx, anvil, me, now, true);
                    if (_k4Mode[idx] != KmStrike) K444End(f, idx, now, -1);   // STRIKE odrzucony - rola oddana
                    return;
                }
                if (own >= T442OwnMelee || gap < K444FreeGap) { _k4WaitThreat++; K444End(f, idx, now, 0); _k4Exit[idx] = now + K444Rewait - K444Reenter; return; }
                if (waited >= K4Cap[_k4Prof[idx] < 0 || _k4Prof[idx] > 2 ? 1 : _k4Prof[idx]]) { _k4WaitCap++; _k4WaitedOut[idx] = true; K444End(f, idx, now, 0); return; }
                Formation inf, foe;
                if (!K444WaitOk(t, f, me, out inf, out foe))
                {
                    if (inf == null) _k4WaitNoInf++; else _k4WaitNoFoe++;
                    K444End(f, idx, now, 0);
                    return;
                }
                Vec2 p;
                WorldPosition wp;
                if (K444WaitPoint(f, inf, foe, me, out p, out wp) && (!_k4P[idx].IsValid || p.Distance(_k4P[idx]) > K444WaitMove))
                {
                    if (C441SetRole(f, CrWait, MovementOrder.MovementOrderMove(wp))) _k4P[idx] = p;
                }
                return;
            }
            // STRIKE
            _k4StrikeSec += step;
            K444StrikeTick(t, f, idx, me, now);
        }

        private static void K444WaitDone(int idx, float waited)
        {
            int p = _k4Prof[idx] < 0 || _k4Prof[idx] > 2 ? 1 : _k4Prof[idx];
            _k4WaitProfN[p]++;
            _k4WaitProfSec[p] += Math.Max(0f, waited);
        }

        private static void K444StartStrike(Team t, Formation f, int idx, Formation tg, Vec2 me, float now, bool fromWait)
        {
            if (!_k4Key || !C441Live)
            {
                if (now >= _k4DryStrike[idx]) { _k4StrikeDry++; _k4DryStrike[idx] = now + R443Latch; }
                return;
            }
            Vec2 ep = tg.CachedMedianPosition.AsVec2;
            if (!ep.IsValid) return;
            Vec2 dir = K444Dir(tg, me, ep);
            bool flank = false, route = true;
            Vec2 p = Vec2.Invalid;
            WorldPosition wp = default(WorldPosition);
            if (K444FrontCos(me, ep, dir) > K444FrontDot || T442Blocked(t, tg, me, ep))
            {
                flank = K444FlankPoint(t, tg, me, ep, dir, out p, out wp);
                route = flank;
            }
            var order = flank ? MovementOrder.MovementOrderMove(wp) : MovementOrder.MovementOrderChargeToTarget(tg);
            if (!C441SetRole(f, CrStrike, order))
            {
                if (now >= _k4DryStrike[idx]) { _k4Refused++; _k4DryStrike[idx] = now + R443Latch; }
                return;
            }
            _k4Mode[idx] = KmStrike; _k4Since[idx] = now; _k4Tgt[idx] = tg; _k4Flank[idx] = flank; _k4FromWait[idx] = fromWait;
            _k4P[idx] = flank ? p : Vec2.Invalid; _k4TgtAt[idx] = ep;
            _k4ChargeAt[idx] = flank ? -1f : now; _k4ContactAt[idx] = -1f;
            _k4Men0[idx] = f.CountOfUnits; _k4TgtMen0[idx] = tg.CountOfUnits;
            _k4Struck[idx] = true;
            _k4StrikeN++;
            if (fromWait) _k4StrikeFromWait++; else _k4StrikeFree++;
            if (flank) _k4FlankN++; else { _k4Direct++; if (!route) _k4NoRoute++; }
        }

        private static void K444StrikeTick(Team t, Formation f, int idx, Vec2 me, float now)
        {
            var tg = _k4Tgt[idx];
            int men0 = _k4TgtMen0[idx];
            bool alive = tg != null && tg.CountOfUnits >= T442MinMen && MissionTeams.Enemies(t, tg.Team);
            if (!alive || (men0 > 0 && tg.CountOfUnits <= (1f - K444Broken) * men0)) { _k4Broken++; K444End(f, idx, now, 1); return; }
            if (f.CountOfUnits < (1f - K444Loss) * _k4Men0[idx]) { _k4CutLoss++; K444End(f, idx, now, 1); return; }
            if (now - _k4Since[idx] > K444Max) { _k4CutTime++; K444End(f, idx, now, 1); return; }
            Vec2 ep = tg.CachedMedianPosition.AsVec2;
            if (!ep.IsValid) return;
            if (_k4Flank[idx])
            {
                Vec2 dir = K444Dir(tg, me, ep);
                bool side = K444FrontCos(me, ep, dir) <= K444SideDot && !T442Blocked(t, tg, me, ep);
                bool reach = _k4P[idx].IsValid && me.Distance(_k4P[idx]) < K444Reach;
                bool late = now - _k4Since[idx] > K444Approach;
                if (side || reach || late)
                {
                    if (C441SetRole(f, CrStrike, MovementOrder.MovementOrderChargeToTarget(tg)))
                    {
                        _k4Flank[idx] = false; _k4ChargeAt[idx] = now;
                        if (late && !side && !reach) _k4FlankLate++; else _k4FlankToCharge++;
                    }
                    return;
                }
                // cel odszedl - punkt od nowa; brak punktu - szarza wprost
                if (_k4TgtAt[idx].IsValid && ep.Distance(_k4TgtAt[idx]) > K444Stale)
                {
                    Vec2 p;
                    WorldPosition wp;
                    if (K444FlankPoint(t, tg, me, ep, dir, out p, out wp))
                    {
                        if (C441SetRole(f, CrStrike, MovementOrder.MovementOrderMove(wp))) { _k4P[idx] = p; _k4TgtAt[idx] = ep; _k4Restale++; }
                    }
                    else if (C441SetRole(f, CrStrike, MovementOrder.MovementOrderChargeToTarget(tg)))
                    {
                        _k4Flank[idx] = false; _k4ChargeAt[idx] = now; _k4NoRoute++;
                    }
                }
                return;
            }
            // szarza: pierwsze zwarcie - kat wejscia; 10 s po nim oddanie RBM
            if (_k4ContactAt[idx] < 0f)
            {
                int side;
                if (K444ContactSide(f, tg, ep, me, _k4ChargeAt[idx], out side))
                {
                    _k4ContactAt[idx] = now;
                    _k4Contact++;
                    if (side >= 0 && side < KaK) _k4Angle[side]++;
                }
                else if (now - _k4ChargeAt[idx] > K444NoContact) { _k4NoContact++; K444End(f, idx, now, 1); }
                return;
            }
            if (now - _k4ContactAt[idx] >= K444AfterContact) { _k4AfterC++; K444End(f, idx, now, 1); }
        }

        /// <summary>Oddanie roli (tylko gdy rola jest nasza). why: 0 koniec czekania (licznik u wolajacego), 1 koniec uderzenia (licznik u wolajacego),
        /// 9 uderzenie bez wyniku (szyk zniknal / gracz), -1 STRIKE odrzucony po czekaniu.</summary>
        private static void K444End(Formation f, int idx, float now, int why)
        {
            byte mode = _k4Mode[idx];
            if (mode == KmWait) K444WaitDone(idx, now - _k4Since[idx]);
            else if (mode == KmStrike) { if (why == 9) _k4StrikeBack++; K444Score(f, idx); }
            try
            {
                if (f != null)
                {
                    byte r = C441RoleOf(f);
                    if (r == CrWait || r == CrStrike) C441SetRole(f, CrNone, default(MovementOrder));
                }
            }
            catch (Exception e) { _k4Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Hammer444 oddanie", e); }
            K444SlotClear(idx);
            _k4Exit[idx] = now;
        }

        /// <summary>Wynik uderzenia: strata celu i jazdy od poczatku STRIKE.</summary>
        private static void K444Score(Formation f, int idx)
        {
            int t0 = _k4TgtMen0[idx], c0 = _k4Men0[idx];
            if (t0 <= 0 || c0 <= 0) return;
            var tg = _k4Tgt[idx];
            int tl = tg != null ? Math.Max(0, tg.CountOfUnits) : 0;
            int cl = f != null ? Math.Max(0, f.CountOfUnits) : 0;
            _k4TgtLossSum += Math.Max(0.0, 1.0 - (double)tl / t0);
            _k4CavLossSum += Math.Max(0.0, 1.0 - (double)cl / c0);
            _k4StrikeDone++;
        }

        // ------------------------------------------------------------ geometria i wybory (glowny watek)

        /// <summary>Kowadlo: pieszy szyk wroga (>= 8, nie jazda) w 300 m, zwiazany z naszym pieszym szykiem (jego najblizszy szyk wroga), w zwarciu >= progu -
        /// najblizszy jezdzie.</summary>
        private static Formation K444FindAnvil(Team t, Vec2 me, float thr)
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
                    if (e == null || e.CountOfUnits < T442MinMen) continue;
                    var eq = e.QuerySystem;
                    if (eq == null || eq.IsCavalryFormation || eq.IsRangedCavalryFormation) continue;
                    if (!eq.IsInfantryFormation && !eq.IsRangedFormation) continue;
                    Vec2 ep = e.CachedMedianPosition.AsVec2;
                    if (!ep.IsValid) continue;
                    float d = me.Distance(ep);
                    if (d > K444Range || d >= bd) continue;
                    // kowadlo: najblizszy szyk wroga celu to nasz (strona druzyny) pieszy szyk
                    var nq = e.CachedClosestEnemyFormation;
                    var nf = nq != null ? nq.Formation : null;
                    if (nf == null || nf.Team == null || !MissionTeams.Enemies(nf.Team, et)) continue;
                    var nfq = nf.QuerySystem;
                    if (nfq == null || nfq.IsCavalryFormation || nfq.IsRangedCavalryFormation) continue;
                    if (K444MeleeOf(e) < thr) continue;
                    best = e; bd = d;
                }
            }
            return best;
        }

        /// <summary>Czekanie ma sens: glowna piechota druzyny (>= 10 i >= pol jazdy) i pieszy szyk wroga (>= 8) - najblizszy tej piechocie.</summary>
        private static bool K444WaitOk(Team t, Formation f, Vec2 me, out Formation inf, out Formation foe)
        {
            foe = null;
            inf = R443MainInf(t, f);
            if (inf != null && inf.CountOfUnits < K444InfShare * f.CountOfUnits) inf = null;
            if (inf == null) return false;
            Vec2 ip = inf.CachedMedianPosition.AsVec2;
            if (!ip.IsValid) { inf = null; return false; }
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
                    if (e == null || e.CountOfUnits < T442MinMen) continue;
                    var eq = e.QuerySystem;
                    if (eq == null || eq.IsCavalryFormation || eq.IsRangedCavalryFormation) continue;
                    if (!eq.IsInfantryFormation && !eq.IsRangedFormation) continue;
                    Vec2 ep = e.CachedMedianPosition.AsVec2;
                    if (!ep.IsValid) continue;
                    float d = ip.Distance(ep);
                    if (d < bd) { bd = d; foe = e; }
                }
            }
            return foe != null;
        }

        /// <summary>Punkt czekania: przy boku glownej piechoty po stronie jazdy (pol szerokosci piechoty + pol szerokosci jazdy + 15 m), 10 m w tyl od linii
        /// w strone wroga; na siatce nawigacji, inaczej za piechota.</summary>
        private static bool K444WaitPoint(Formation f, Formation inf, Formation foe, Vec2 me, out Vec2 p, out WorldPosition wp)
        {
            p = Vec2.Invalid;
            wp = default(WorldPosition);
            Vec2 ip = inf.CachedMedianPosition.AsVec2, ep = foe.CachedMedianPosition.AsVec2;
            if (!ip.IsValid || !ep.IsValid) return false;
            Vec2 d = ep - ip;
            if (d.LengthSquared < 1f) return false;
            d = d.Normalized();
            Vec2 right = d.RightVec();
            Vec2 rel = me - ip;
            float s = rel.x * right.x + rel.y * right.y >= 0f ? 1f : -1f;
            float off = 0.5f * Math.Max(1f, inf.Width) + 0.5f * Math.Max(1f, f.Width) + K444WaitSide;
            float back = 0.5f * inf.Depth + K444WaitBack + 0.5f * f.Depth;
            for (int k = 0; k < 3; k++)
            {
                Vec2 c = k == 0 ? ip + right * (s * off) - d * K444WaitBack
                       : (k == 1 ? ip - right * (s * off) - d * K444WaitBack : ip - d * back);
                WorldPosition w = inf.CachedMedianPosition;
                w.SetVec2(c);
                if (!w.IsValid || w.GetNavMesh() == UIntPtr.Zero) continue;
                p = c; wp = w;
                return true;
            }
            return false;
        }

        /// <summary>Punkt dojazdu: bok blizszy jezdzie (pol szerokosci celu + 30 m w bok, pol glebokosci w tyl), drugi bok, tyl (pol glebokosci + 40 m);
        /// oba odcinki bez zaslony piechoty wroga, na siatce nawigacji.</summary>
        private static bool K444FlankPoint(Team t, Formation tg, Vec2 me, Vec2 ep, Vec2 dir, out Vec2 p, out WorldPosition wp)
        {
            p = Vec2.Invalid;
            wp = default(WorldPosition);
            Vec2 right = dir.RightVec();
            Vec2 rel = me - ep;
            float s = rel.x * right.x + rel.y * right.y >= 0f ? 1f : -1f;
            float off = 0.5f * Math.Max(1f, tg.Width) + K444FlankOff;
            float hd = 0.5f * Math.Max(1f, tg.Depth);
            for (int k = 0; k < 3; k++)
            {
                Vec2 c = k == 0 ? ep + right * (s * off) - dir * hd
                       : (k == 1 ? ep - right * (s * off) - dir * hd : ep - dir * (hd + K444RearOff));
                if (T442Blocked(t, tg, me, c) || T442Blocked(t, tg, c, ep)) continue;
                WorldPosition w = tg.CachedMedianPosition;
                w.SetVec2(c);
                if (!w.IsValid || w.GetNavMesh() == UIntPtr.Zero) continue;
                p = c; wp = w;
                return true;
            }
            return false;
        }

        /// <summary>Kierunek czola celu (Formation.Direction), przy braku - od jazdy do celu.</summary>
        private static Vec2 K444Dir(Formation tg, Vec2 me, Vec2 ep)
        {
            Vec2 dir = tg.Direction;
            if (dir.IsValid && dir.LengthSquared > 0.0001f) return dir.Normalized();
            Vec2 a = ep - me;
            return a.LengthSquared > 0.01f ? (Vec2.Zero - a).Normalized() : new Vec2(0f, 1f);
        }

        /// <summary>cos kata miedzy czolem celu a kierunkiem cel -> punkt: 1 = przed czolem, -1 = za plecami.</summary>
        private static float K444FrontCos(Vec2 at, Vec2 ep, Vec2 dir)
        {
            Vec2 r = at - ep;
            float len = r.Length;
            if (len < 0.1f) return 0f;
            return (r.x * dir.x + r.y * dir.y) / len;
        }

        private static float K444MeleeOf(Formation f)
        {
            float v;
            if (_k4Melee.TryGetValue(f, out v)) return v;
            v = T442MeleeShare(f);
            _k4Melee[f] = v;
            return v;
        }

        // ---- pierwsze zwarcie w szarzy: jezdziec z ciosem wrecz zadanym / otrzymanym od chwili szarzy, najblizszy srodkowi celu
        private static float _k4ScanT0;
        private static Vec2 _k4ScanC, _k4ScanBest;
        private static float _k4ScanBd;

        private static readonly Action<Agent> K444ContactMan = a =>
        {
            if (a == null || !a.IsActive()) return;
            if (a.LastMeleeAttackTime < _k4ScanT0 && a.LastMeleeHitTime < _k4ScanT0) return;
            Vec2 p = a.Position.AsVec2;
            float dd = p.DistanceSquared(_k4ScanC);
            if (dd < _k4ScanBd) { _k4ScanBd = dd; _k4ScanBest = p; }
        };

        /// <summary>Kat wejscia: 0 czolo (cos > 0.5), 2 tyl (cos < -0.5), 1 bok - wzgledem czola celu w chwili wykrycia zwarcia (do 1 s po ciosie).</summary>
        private static bool K444ContactSide(Formation f, Formation tg, Vec2 ep, Vec2 me, float t0, out int side)
        {
            side = -1;
            if (t0 < 0f) return false;
            _k4ScanT0 = t0; _k4ScanC = ep; _k4ScanBd = float.MaxValue; _k4ScanBest = Vec2.Invalid;
            f.ApplyActionOnEachUnit(K444ContactMan);
            if (!_k4ScanBest.IsValid || _k4ScanBd == float.MaxValue) return false;
            Vec2 dir = K444Dir(tg, me, ep);
            float cos = K444FrontCos(_k4ScanBest, ep, dir);
            side = cos > 0.5f ? KaFront : (cos < -0.5f ? KaRear : KaSide);
            return true;
        }

        // ------------------------------------------------------------ linia konca bitwy (Battle291.Write)

        internal static string K444Line(string kind)
        {
            var c = Settings.Current;
            var sb = new StringBuilder(1200);
            int wn = _k4WaitProfN[0] + _k4WaitProfN[1] + _k4WaitProfN[2];
            float ws = _k4WaitProfSec[0] + _k4WaitProfSec[1] + _k4WaitProfSec[2];
            sb.Append("Kowadlo i mlot (444): misja ").Append(kind ?? "-")
              .Append(" | klucz mlot i kowadlo ").Append(c != null && c.HammerAndAnvil ? "TAK" : "NIE")
              .Append(", dowodca jazdy ").Append(c != null && c.CavalryCommander ? "TAK" : "NIE")
              .Append(", profil wodza ").Append(c != null && c.CommanderPersonalityInBattle ? "TAK" : "NIE")
              .Append(" | progi (OSTROZNY / ZWYKLY / PORYWCZY): cel w zwarciu ").Append(K444Pct(K4Thr[0])).Append(" / ").Append(K444Pct(K4Thr[1])).Append(" / ").Append(K444Pct(K4Thr[2]))
              .Append(", limit czekania ").Append(K4Cap[0].ToString("0", K4Inv)).Append(" / ").Append(K4Cap[1].ToString("0", K4Inv)).Append(" / ").Append(K4Cap[2].ToString("0", K4Inv)).Append(" s")
              .Append(" | czekanie WAIT: wejsc ").Append(_k4WaitN).Append(", bylby (klucz NIE / bez dowodcy jazdy) ").Append(_k4WaitDry)
              .Append(", ").Append(_k4WaitSec.ToString("0", K4Inv)).Append(" szyko-s, srednio ").Append(wn > 0 ? (ws / wn).ToString("0", K4Inv) + " s" : "-")
              .Append(" (OSTROZNY ").Append(K444Avg(0)).Append(", ZWYKLY ").Append(K444Avg(1)).Append(", PORYWCZY ").Append(K444Avg(2)).Append(')')
              .Append(", koniec: kowadlo -> uderzenie ").Append(_k4WaitAnvil).Append(", limit czekania ").Append(_k4WaitCap).Append(", zwarcie / wrog w 15 m ").Append(_k4WaitThreat)
              .Append(", brak glownej piechoty ").Append(_k4WaitNoInf).Append(", brak pieszego wroga ").Append(_k4WaitNoFoe)
              .Append(", przejete przez 443 ").Append(_k4WaitTo443).Append(", przez 445 (jazda na jazde) ").Append(_k4WaitTo445).Append(", oddane przez 441 ").Append(_k4WaitBack).Append("; brak miejsca ").Append(_k4WaitNoSpot)
              .Append(" | uderzenia STRIKE ").Append(_k4StrikeN).Append(" (po czekaniu ").Append(_k4StrikeFromWait).Append(", jazda wolna ").Append(_k4StrikeFree)
              .Append("), bylby (klucz NIE / bez dowodcy jazdy) ").Append(_k4StrikeDry).Append(", ").Append(_k4StrikeSec.ToString("0", K4Inv)).Append(" szyko-s")
              .Append(" | dojazd: wprost (juz z boku / tylu) ").Append(_k4Direct - _k4NoRoute).Append(", wprost bez drogi z boku ").Append(_k4NoRoute)
              .Append(", objazd ").Append(_k4FlankN).Append(" (-> szarza ").Append(_k4FlankToCharge).Append(", po ").Append(K444Approach.ToString("0", K4Inv)).Append(" s ").Append(_k4FlankLate)
              .Append(", punkt od nowa ").Append(_k4Restale).Append(')')
              .Append(" | kat wejscia (pierwsze zwarcie w szarzy): ").Append(KaName[KaFront]).Append(' ').Append(_k4Angle[KaFront])
              .Append(", ").Append(KaName[KaSide]).Append(' ').Append(_k4Angle[KaSide]).Append(", ").Append(KaName[KaRear]).Append(' ').Append(_k4Angle[KaRear])
              .Append(" (zwarc ").Append(_k4Contact).Append(')')
              .Append(" | koniec: po zwarciu oddana RBM ").Append(_k4AfterC).Append(", cel rozbity ").Append(_k4Broken).Append(", straty jazdy ").Append(_k4CutLoss)
              .Append(", czas ").Append(_k4CutTime).Append(", bez zwarcia ").Append(_k4NoContact).Append(", oddana (gracz / szyk pusty / 441) ").Append(_k4StrikeBack)
              .Append(", w toku przy koncu ").Append(_k4Open)
              .Append(" | wynik: cel stracil srednio ").Append(_k4StrikeDone > 0 ? (100.0 * _k4TgtLossSum / _k4StrikeDone).ToString("0", K4Inv) + "%" : "-")
              .Append(", jazda stracila srednio ").Append(_k4StrikeDone > 0 ? (100.0 * _k4CavLossSum / _k4StrikeDone).ToString("0", K4Inv) + "%" : "-")
              .Append(" | naraz maks: WAIT ").Append(_k4MaxWait).Append(", STRIKE ").Append(_k4MaxStrike)
              .Append(" | rajd 443 wstrzymany przy STRIKE ").Append(_k4Keep443).Append(", rola odrzucona ").Append(_k4Refused)
              .Append(" | potkniecia ").Append(_k4Stum).Append('.');
            return sb.ToString();
        }

        private static string K444Pct(float v) { return (100f * v).ToString("0", K4Inv) + "%"; }

        private static string K444Avg(int p)
        {
            return _k4WaitProfN[p] > 0 ? (_k4WaitProfSec[p] / _k4WaitProfN[p]).ToString("0", K4Inv) + " s / " + _k4WaitProfN[p] : "- / 0";
        }
    }
}
