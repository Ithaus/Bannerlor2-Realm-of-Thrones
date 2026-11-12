using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 443 = PAKIET 4 krok 4.2 + PAKIET 5 krok 5.1 audytu AI bitwy (docs/audyty-2026-11-11/AUDYT-AI-BITWA.md rozdz. 3; razem - "inaczej gracz tylko zyskuje albo
    /// tylko traci"). Jeff 11.11: "Postara sie rozbic lucznikow a gdy nie ma chowa sie za piechote i wystawia swoich lucznikow ale musi ocenic wymiane ognia zeby
    /// nie stali jak idioci i dawali sie wystrzelac". Zadnych bonusow do liczb - tylko decyzje. Bitwa polowa, nie morska. Glowny watek (Doctrine.Tick, co 1 s).
    ///
    /// Sygnal ostrzalu (wlasny, nie kolejka 140/157 - te zyja tylko przy swoich kluczach ShieldsUp): BattleSense.OnAgentHit -> R443OnHit - trafienie pociskiem
    ///   (obrazenia > 0, strzelec z druzyny wroga) czlowieka albo KONIA (kon - w szyk jezdzca; silnik tego nie liczy, 437: 55 z 87 trafien jazdy to konie);
    ///   Interlocked na slot [TeamIndex x 10 + FormationIndex]; okno 10 s w glownym watku (pierscien probek co takt).
    ///
    /// (a) RAID (klucz CavalryRaidsArchers, rola 441 CrRaid): szyk jazdy AI (nie konni strzelcy, >= 5, nie szyk gracza) poza zwarciem (< 30% wrecz, odstep >= 15 m)
    ///   z >= R443CavHits (4) trafieniami ludzi / koni w 10 s; cel = pieszy szyk strzelcow wroga (>= 8, nie zwiazany walka, <= 250 m): odsloniety (zaden szyk
    ///   piechoty wroga nie zaslania odcinka - T442Blocked, ta sama miara co 437 / 442) -> ChargeToTarget od razu; zasloniety -> punkt z boku strzelcow
    ///   (pol szerokosci + 40 m, strona blizsza jezdzie, na siatce nawigacji, oba odcinki niezasloniete) -> Move galopem (wyjatek klusa 441), w 30 m od punktu
    ///   albo gdy droga wprost sie otworzy -> ChargeToTarget. Jezdzcy trzymaja cel: slot 442 z celem rajdu (postfiks GetCorrectTarget 442 - fakt 3; przy kluczu
    ///   442 NIE tylko "bylby"). Koniec: cel stracil polowe ludzi / < 8 (rozbity), jazda stracila 40% (straty), 75 s (czas), droga z boku zamknieta (droga).
    /// (b) HIDE (klucz CavalryHidesFromMissiles, rola 441 CrHide): ten sam ostrzal, strzelcy wroga w zasiegu, ale zaden nie ma drogi -> Move za wlasna glowna
    ///   piechote (po stronie przeciwnej do najblizszych strzelcow, pol glebokosci piechoty + 10 m + pol glebokosci jazdy; punkt odswiezany, gdy piechota
    ///   przesunie sie o 8 m). Koniec: piechota w zwarciu (>= 30%) albo jej brak - od razu (szarza znow RBM); po zapadce 30 s: droga do strzelcow -> RAID,
    ///   brak trafien w 10 s -> oddanie. "Wodz wystawia lucznikow": strzelcy druzyny strzelaja jak dotad (gra / RBM); decyzja 443 to tylko ocena wymiany (c).
    ///   Zapadka: rola trzymana co najmniej 30 s (poza koncami twardymi); nowa rola najwczesniej 10 s po oddaniu.
    /// (c) ocena wymiany ognia (klucz ArchersJudgeMissileDuel): bilans strat od pociskow druzyny AI i wszystkich druzyn wroga w oknie 30 s
    ///   (TeamQuerySystem.DeathByRangedCount, jak 291e / S2): przegrana = wrog ma strzelcow, my >= 3 i > 1.5 x wrog. Szyk strzelcow AI tej druzyny trafiany
    ///   (>= 2 trafienia w 10 s), poza zwarciem -> flaga 30 s (odswiezana); Manoeuvres chowa go schowaniem S4 (BehaviorScreenedSkirmish 2, reszta 0) za glowna
    ///   piechote; po wygasnieciu flagi wraca ("wystawieni ponownie").
    /// (d) 5.1 lucznicy przed jazda (klucz ArchersShelterFromCavalry): szyk jazdy wroga (nie konni strzelcy, >= 5) w 20 m albo jadacy na szyk strzelcow
    ///   (predkosc > 2 m/s, cos kata > 0.33, czas dojazdu <= 8 s - ta sama regula co gra dla piechoty w BehaviorTacticalCharge, mb) z KAZDEJ strony -> flaga 3 s
    ///   (odswiezana) -> schowanie S4 jak (c). Flaga gry IsUnderCavalryChargeFromFront nie jest uzywana (nie widzi boku i tylu).
    ///   (c) i (d) dla kazdej druzyny AI (TeamAIGeneral) obu stron, takze bez doktryny (tlum) i takze przeciw graczowi; szyk gracza - nigdy.
    /// Klucze NIE: decyzje liczone (tylko odczyt), role nie nadawane, powody S4 443 = 0 - co do znaku jak dotad; liczniki "bylby".
    /// Watki: R443OnHit (tick mozliwie asynchroniczny) - tylko Interlocked na tablicach; reszta glowny watek; wyjatki per szyk liczone (potkniecia).
    /// </summary>
    internal static partial class Doctrine
    {
        internal const float R443Every = 1f, R443Latch = 30f, R443Reenter = 10f, R443RaidRange = 250f, R443FlankOff = 40f, R443FlankReach = 30f, R443FlankStale = 30f;
        internal const float R443RaidMax = 75f, R443RaidLoss = 0.4f, R443TgtBroken = 0.5f, R443HideGap = 10f, R443HideMove = 8f, R443Gap = 15f, R443TgtMelee = 0.2f;
        internal const float R443DuelRatio = 1.5f, R443CavEta = 8f, R443CavNear = 20f, R443CavDot = 0.33f, R443CavSpeed = 2f, R443CavHold = 3f, R443Still = 1.5f;
        internal const float R443ExpMin = 30f, R443ExpMax = 200f;
        internal const int R443CavHits = 4, R443ArcherHits = 2, R443DuelMin = 3, R443CavMin = 5, R443InfMin = 10;
        private const int R4W = 11, R4DW = 31;   // pierscien: 10 s trafien slotu, 30 s strat druzyny (probka co takt 1 s)
        private const byte RmNone = 0, RmRaid = 1, RmHide = 2;
        private static readonly IFormatProvider R4Inv = CultureInfo.InvariantCulture;

        // ---- trafienia pociskami (pisze R443OnHit - dowolny watek, Interlocked; czyta glowny)
        private static volatile bool _r4Live;
        private static readonly int[] _r4Hit = new int[CrSlots];
        private static readonly int[] _r4Horse = new int[CrSlots];
        // ---- tylko glowny watek
        private static readonly int[] _r4Ring = new int[CrSlots * R4W];
        private static readonly Formation[] _r4RWho = new Formation[CrSlots];
        private static readonly int[] _r4LastHit = new int[CrSlots], _r4LastHorse = new int[CrSlots];
        private static readonly int[] _r4DRing = new int[MaxTeams * R4DW];
        private static readonly Team[] _r4DTeam = new Team[MaxTeams];
        private static readonly int[] _r4DNow = new int[MaxTeams], _r4DOld = new int[MaxTeams];
        private static readonly bool[] _r4Lost = new bool[MaxTeams];
        private static int _r4Pos, _r4DPos;
        private static float _r4Acc, _r4Now;
        private static bool _r4KRaid, _r4KHide, _r4KDuel, _r4KShelter;
        // jazda (slot FormationIndex)
        private static readonly Formation[] _r4CWho = new Formation[CrSlots];
        private static readonly byte[] _r4Mode = new byte[CrSlots];
        private static readonly float[] _r4Since = new float[CrSlots], _r4Exit = new float[CrSlots], _r4DryUntil = new float[CrSlots];
        private static readonly Formation[] _r4Tgt = new Formation[CrSlots];
        private static readonly bool[] _r4Flank = new bool[CrSlots], _r4Contact = new bool[CrSlots];
        private static readonly Vec2[] _r4P = new Vec2[CrSlots], _r4TgtAt = new Vec2[CrSlots], _r4Prev = new Vec2[CrSlots];
        private static readonly int[] _r4Men0 = new int[CrSlots], _r4TgtMen0 = new int[CrSlots];
        // strzelcy (slot FormationIndex)
        private static readonly Formation[] _r4AWho = new Formation[CrSlots];
        private static readonly float[] _r4DuelUntil = new float[CrSlots], _r4CavUntil = new float[CrSlots];
        // S4 (indeks Manoeuvres): wejscie z powodu S4 (liczniki S4) i powody 443 (2 wymiana, 4 jazda)
        private static readonly bool[] _s4Old = new bool[Slots];
        private static readonly byte[] _s4Why = new byte[Slots];
        private static readonly Dictionary<Formation, float> _r4Melee = new Dictionary<Formation, float>();

        // ---- liczniki misji (R443Reset w Battle291.Begin)
        private static float _r4FireSec, _r4FireStill, _r4ExpStill, _r4NoArchSec, _r4RaidSec, _r4HideSec, _r4LostSec, _r4EvenSec, _r4HeldSec, _r4NoScreenSec, _r4EtaSum;
        private static int _r4HitRider, _r4HitHorse, _r4RaidN, _r4RaidDirect, _r4RaidFlank, _r4FlankToCharge, _r4RaidDry, _r4RaidBroken, _r4RaidContact, _r4RaidCut;
        private static int _r4CutLoss, _r4CutTime, _r4CutRoute, _r4RaidDone, _r4HideN, _r4HideDry, _r4HideMelee, _r4HideCalm, _r4HideToRaid, _r4HideNoInf, _r4HideNoInfN;
        private static int _r4Refused, _r4Back, _r4LostIn, _r4DuelIn, _r4DuelDry, _r4DuelTake, _r4DuelOut, _r4CavIn, _r4CavDry, _r4CavTake, _r4CavOut;
        private static int _r4NotSkirm, _r4NoBeh, _r4S4Lost, _r4HeldMax, _r4Stum, _r4Open;
        private static double _r4TgtLossSum;

        /// <summary>Battle291.Begin (AfterStart): liczniki linii 443 od zera.</summary>
        internal static void R443Reset()
        {
            _r4FireSec = _r4FireStill = _r4ExpStill = _r4NoArchSec = _r4RaidSec = _r4HideSec = _r4LostSec = _r4EvenSec = _r4HeldSec = _r4NoScreenSec = _r4EtaSum = 0f;
            _r4HitRider = _r4HitHorse = _r4RaidN = _r4RaidDirect = _r4RaidFlank = _r4FlankToCharge = _r4RaidDry = _r4RaidBroken = _r4RaidContact = _r4RaidCut = 0;
            _r4CutLoss = _r4CutTime = _r4CutRoute = _r4RaidDone = _r4HideN = _r4HideDry = _r4HideMelee = _r4HideCalm = _r4HideToRaid = _r4HideNoInf = _r4HideNoInfN = 0;
            _r4Refused = _r4Back = _r4LostIn = _r4DuelIn = _r4DuelDry = _r4DuelTake = _r4DuelOut = _r4CavIn = _r4CavDry = _r4CavTake = _r4CavOut = 0;
            _r4NotSkirm = _r4NoBeh = _r4S4Lost = _r4HeldMax = _r4Stum = _r4Open = 0;
            _r4TgtLossSum = 0.0;
        }

        /// <summary>Doctrine.Begin (AfterStart, po Clear): bitwa polowa, nie morska - pomiar zawsze, decyzje wedlug kluczy (czytane co takt).</summary>
        private static void R443Begin(Mission m)
        {
            bool field = false;
            try { field = m != null && m.IsFieldBattle && !m.IsNavalBattle; } catch { field = false; }
            _r4Acc = 0f;
            _r4Live = field;
        }

        /// <summary>Doctrine.Clear (Begin / End): stan slotow od zera (role oddaje C441ClearMission). Rajdy w toku przy koncu - licznik "w toku".</summary>
        private static void R443ClearMission()
        {
            _r4Live = false;
            for (int i = 0; i < CrSlots; i++)
            {
                if (_r4Mode[i] == RmRaid) _r4Open++;
                Interlocked.Exchange(ref _r4Hit[i], 0); Interlocked.Exchange(ref _r4Horse[i], 0);
                _r4RWho[i] = null; _r4LastHit[i] = _r4LastHorse[i] = 0;
                R443CavClear(i);
                _r4CWho[i] = null; _r4Prev[i] = Vec2.Invalid; _r4Exit[i] = -1000f; _r4DryUntil[i] = -1000f;
                _r4AWho[i] = null; _r4DuelUntil[i] = _r4CavUntil[i] = -1000f;
            }
            for (int i = 0; i < _r4Ring.Length; i++) _r4Ring[i] = 0;
            for (int i = 0; i < _r4DRing.Length; i++) _r4DRing[i] = 0;
            for (int i = 0; i < MaxTeams; i++) { _r4DTeam[i] = null; _r4DNow[i] = _r4DOld[i] = 0; _r4Lost[i] = false; }
            for (int i = 0; i < Slots; i++) { _s4Old[i] = false; _s4Why[i] = 0; }
            _r4Melee.Clear();
            _r4Pos = _r4DPos = 0;
            _r4Acc = 0f;
        }

        private static void R443CavClear(int i)
        {
            _r4Mode[i] = RmNone; _r4Tgt[i] = null; _r4Flank[i] = false; _r4Contact[i] = false;
            _r4P[i] = Vec2.Invalid; _r4TgtAt[i] = Vec2.Invalid; _r4Men0[i] = _r4TgtMen0[i] = 0; _r4Since[i] = 0f;
        }

        // ------------------------------------------------------------ trafienie (BattleSense.OnAgentHit - dowolny watek)

        /// <summary>Trafienie pociskiem (obrazenia > 0) czlowieka albo konia przez wroga - licznik slotu szyku ofiary (kon - szyk jezdzca). Bez alokacji, bez logu.</summary>
        internal static void R443OnHit(Agent v, Agent by, in Blow blow)
        {
            if (!_r4Live || v == null) return;
            try
            {
                if (!blow.IsMissile || blow.InflictedDamage <= 0) return;
                bool horse = v.IsMount;
                var man = horse ? v.RiderAgent : v;
                if (man == null || !man.IsHuman) return;
                var f = man.Formation;
                var t = man.Team;
                var at = by != null ? by.Team : null;
                if (f == null || t == null || at == null || !t.IsEnemyOf(at)) return;
                int idx = C441Idx(f);
                if (idx < 0) return;
                Interlocked.Increment(ref _r4Hit[idx]);
                if (horse) Interlocked.Increment(ref _r4Horse[idx]);
            }
            catch { }
        }

        // ------------------------------------------------------------ odczyty dla Manoeuvres / 442 (glowny watek)

        /// <summary>Manoeuvres: powody schowania 443 dla szyku strzelcow - 2 przegrana wymiana ognia, 4 jazda wroga dojedzie (klucze TAK, szyk nie gracza).</summary>
        internal static byte R443Why(Formation f)
        {
            if (!_r4Live || f == null || HorsePursuit.PlayerForm(f) || f.HasPlayerControlledTroop) return 0;
            int idx = C441Idx(f);
            if (idx < 0 || !ReferenceEquals(_r4AWho[idx], f)) return 0;
            byte w = 0;
            if (_r4KDuel && _r4Now < _r4DuelUntil[idx]) w |= 2;
            if (_r4KShelter && _r4Now < _r4CavUntil[idx]) w |= 4;
            return w;
        }

        internal static void R443NoteNoScreen() { _r4NoScreenSec += S4Every; }

        /// <summary>TakeOver bez powodu S4: r 0 schowany, 1 brak BehaviorScreenedSkirmish, 2 szyk sie nie ostrzeliwuje.</summary>
        internal static void R443NoteTake(int r, byte why)
        {
            if (r == 1) { _r4NoBeh++; return; }
            if (r != 0) { _r4NotSkirm++; return; }
            if ((why & 2) != 0) _r4DuelTake++;
            if ((why & 4) != 0) _r4CavTake++;
        }

        internal static void R443NoteLost() { _r4S4Lost++; }

        internal static void R443NoteReturn(byte why)
        {
            if ((why & 2) != 0) _r4DuelOut++;
            if ((why & 4) != 0) _r4CavOut++;
        }

        /// <summary>T442Form (glowny watek): szyk w roli RAID w fazie szarzy - cel rajdu (slot 442 jezdzcow); inaczej null.</summary>
        internal static Formation R443AimTarget(Formation f, int idx)
        {
            if (!_r4Live || f == null || idx < 0 || idx >= CrSlots) return null;
            if (_r4Mode[idx] != RmRaid || _r4Flank[idx] || !ReferenceEquals(_r4CWho[idx], f)) return null;
            var tg = _r4Tgt[idx];
            return tg != null && tg.CountOfUnits > 0 ? tg : null;
        }

        // ------------------------------------------------------------ takt glownego watku (Doctrine.Tick, co 1 s)

        internal static void R443Tick(Mission m, float dt)
        {
            if (!_r4Live || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m) || m.MissionEnded) return;
            _r4Acc += dt;
            if (_r4Acc < R443Every) return;
            float step = _r4Acc;
            _r4Acc = 0f;
            bool dep;
            try { dep = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && m.CurrentTime >= P440NoDeploy); } catch { dep = false; }
            if (!dep) return;
            var c = Settings.Current;
            if (c == null) return;
            _r4KRaid = c.CavalryRaidsArchers; _r4KHide = c.CavalryHidesFromMissiles; _r4KDuel = c.ArchersJudgeMissileDuel; _r4KShelter = c.ArchersShelterFromCavalry;
            float now = m.CurrentTime;
            _r4Now = now;
            _t4Now = now;   // T442MeleeShare (glowny watek)
            _r4Melee.Clear();
            _r4Pos = (_r4Pos + 1) % R4W;
            _r4DPos = (_r4DPos + 1) % R4DW;
            var teams = m.Teams;
            int n = teams != null ? teams.Count : 0;
            // 1) straty od pociskow druzyn (okno 30 s) i trafienia slotow (okno 10 s) - wszystkie druzyny, co takt (pierscienie)
            for (int i = 0; i < n; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!MissionTeams.Valid(t)) continue;
                int ti = t.TeamIndex;
                if (ti < 0 || ti >= MaxTeams) continue;
                try
                {
                    var q = t.QuerySystem;
                    int cum = q != null ? q.DeathByRangedCount : 0;
                    int b = ti * R4DW;
                    if (!ReferenceEquals(_r4DTeam[ti], t)) { _r4DTeam[ti] = t; for (int k = 0; k < R4DW; k++) _r4DRing[b + k] = cum; }
                    _r4DOld[ti] = _r4DRing[b + (_r4DPos + 1) % R4DW];
                    _r4DRing[b + _r4DPos] = cum;
                    _r4DNow[ti] = cum;
                    var fl = t.FormationsIncludingSpecialAndEmpty;
                    for (int j = 0; fl != null && j < fl.Count; j++)
                    {
                        var f = fl[j];
                        if (f == null) continue;
                        int idx = C441Idx(f);
                        if (idx < 0) continue;
                        int hc = Volatile.Read(ref _r4Hit[idx]);
                        int rb = idx * R4W;
                        if (!ReferenceEquals(_r4RWho[idx], f))
                        {
                            _r4RWho[idx] = f;
                            for (int k = 0; k < R4W; k++) _r4Ring[rb + k] = hc;
                            _r4LastHit[idx] = hc; _r4LastHorse[idx] = Volatile.Read(ref _r4Horse[idx]);
                        }
                        _r4Ring[rb + _r4Pos] = hc;
                    }
                }
                catch (Exception e) { _r4Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Raid443 pierscien", e); }
            }
            // 2) bilans wymiany ognia druzyn AI
            for (int i = 0; i < n; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!R443AiTeam(t)) continue;
                int ti = t.TeamIndex;
                try
                {
                    int mine = _r4DNow[ti] - _r4DOld[ti], theirs = 0;
                    for (int k = 0; k < n; k++)
                    {
                        var e = teams[k];
                        if (!MissionTeams.Enemies(t, e)) continue;
                        int ek = e.TeamIndex;
                        if (ek >= 0 && ek < MaxTeams && ReferenceEquals(_r4DTeam[ek], e)) theirs += _r4DNow[ek] - _r4DOld[ek];
                    }
                    var q = t.QuerySystem;
                    bool enemyShoots = q != null && q.EnemyRangedRatio + q.EnemyRangedCavalryRatio > 0.01f;
                    bool lost = enemyShoots && mine >= R443DuelMin && mine > R443DuelRatio * theirs;
                    if (lost && !_r4Lost[ti]) _r4LostIn++;
                    _r4Lost[ti] = lost;
                    if (enemyShoots && q.RangedRatio > 0.01f) { if (lost) _r4LostSec += step; else _r4EvenSec += step; }
                }
                catch (Exception e) { _r4Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Raid443 bilans", e); }
            }
            // 3) szyki AI: jazda (role) i strzelcy (flagi S4 443)
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
                    try { R443Form(m, t, f, idx, now, step); }
                    catch (Exception e)
                    {
                        // potkniecie szyku: rola oddana (nie zostaje bez wlasciciela), stan slotu od zera - bez globalnego wylacznika
                        _r4Stum++;
                        try { if (_r4Mode[idx] != RmNone) C441SetRole(f, CrNone, default(MovementOrder)); } catch { }
                        R443CavClear(idx);
                        RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Raid443 szyk", e);
                    }
                }
            }
            // 4) schowania S4 z powodow 443 naraz (indeks Manoeuvres)
            int held = 0;
            for (int i = 0; i < Slots; i++) if (_sBack[i] && !_s4Old[i]) held++;
            _r4HeldSec += held * step;
            if (held > _r4HeldMax) _r4HeldMax = held;
        }

        private static bool R443AiTeam(Team t)
        {
            if (!MissionTeams.Valid(t) || !(t.TeamAI is TeamAIGeneral)) return false;
            if (t.Side != BattleSideEnum.Attacker && t.Side != BattleSideEnum.Defender) return false;
            int ti = t.TeamIndex;
            return ti >= 0 && ti < MaxTeams;
        }

        private static void R443Form(Mission m, Team t, Formation f, int idx, float now, float step)
        {
            int hits = Volatile.Read(ref _r4Hit[idx]) - _r4Ring[idx * R4W + (_r4Pos + 1) % R4W];   // trafienia w 10 s
            var q = f.QuerySystem;
            bool cavCls = q != null && q.IsCavalryFormation && !q.IsRangedCavalryFormation;
            bool mine = f.CountOfUnits > 0 && !HorsePursuit.PlayerForm(f) && !f.HasPlayerControlledTroop;
            // trafienia jazdy AI do linii (ludzie / konie)
            if (cavCls && mine)
            {
                int hc = Volatile.Read(ref _r4Hit[idx]), hh = Volatile.Read(ref _r4Horse[idx]);
                int dh = hc - _r4LastHit[idx], dk = hh - _r4LastHorse[idx];
                if (dk > 0) _r4HitHorse += dk;
                if (dh - dk > 0) _r4HitRider += dh - dk;
                _r4LastHit[idx] = hc; _r4LastHorse[idx] = hh;
            }
            else { _r4LastHit[idx] = Volatile.Read(ref _r4Hit[idx]); _r4LastHorse[idx] = Volatile.Read(ref _r4Horse[idx]); }
            if (cavCls) R443Cav(m, t, f, idx, mine && f.CountOfUnits >= R443CavMin, hits, now, step);
            else if (_r4Mode[idx] != RmNone && ReferenceEquals(_r4CWho[idx], f)) R443End(f, idx, now, 0);   // szyk przestal byc jazda
            if (q != null && q.IsRangedFormation && !cavCls) R443Arch(t, f, idx, mine && f.CountOfUnits >= R443CavMin, hits, now);
            else if (ReferenceEquals(_r4AWho[idx], f)) { _r4DuelUntil[idx] = _r4CavUntil[idx] = -1000f; }
        }

        // ------------------------------------------------------------ jazda: RAID / HIDE

        private static void R443Cav(Mission m, Team t, Formation f, int idx, bool ok, int hits, float now, float step)
        {
            if (!ReferenceEquals(_r4CWho[idx], f))
            {
                R443CavClear(idx);
                _r4CWho[idx] = f; _r4Prev[idx] = Vec2.Invalid; _r4Exit[idx] = -1000f; _r4DryUntil[idx] = -1000f;
            }
            if (!ok)
            {
                if (_r4Mode[idx] != RmNone) R443End(f, idx, now, 0);
                _r4Prev[idx] = Vec2.Invalid;
                return;
            }
            Vec2 me = f.CachedAveragePosition;
            if (!me.IsValid) return;
            // rola oddana przez 441 (gracz przejal szyk / szyk pusty) - stan 443 od zera
            if (_r4Mode[idx] != RmNone)
            {
                byte want = _r4Mode[idx] == RmRaid ? CrRaid : CrHide;
                if (C441RoleOf(f) != want) { _r4Back++; R443CavClear(idx); _r4Exit[idx] = now; }
            }
            float gap = R443GapOf(f);
            float own = R443Melee(f);
            bool still = _r4Prev[idx].IsValid && me.Distance(_r4Prev[idx]) < R443Still * step;
            _r4Prev[idx] = me;
            // strzelcy wroga w zasiegu: cel rajdu (wprost / z boku), najblizsi strzelcy (kierunek schowania)
            Formation tgt, near;
            bool flank, expNear;
            Vec2 fp;
            WorldPosition fwp;
            R443FindRaid(t, f, me, out tgt, out flank, out fp, out fwp, out near, out expNear);
            // pomiar: jazda pod ostrzalem (trafienie ludzi albo koni w 10 s) poza zwarciem - stala / stala przy odslonietych strzelcach
            if (hits > 0 && gap >= R443Gap)
            {
                _r4FireSec += step;
                if (still) { _r4FireStill += step; if (expNear) _r4ExpStill += step; }
            }
            bool pressure = hits >= R443CavHits && own < T442OwnMelee && gap >= R443Gap;
            byte mode = _r4Mode[idx];
            if (mode == RmNone)
            {
                if (!pressure || now - _r4Exit[idx] < R443Reenter) return;
                if (V445Busy(f)) return;   // 445: szyk w CAVFIRST / DISENGAGE - bez rajdu / schowania (rola 445 nie nadpisywana)
                if (K444Striking(f)) return;   // 444: uderzenie kowadla i mlota w toku - bez rajdu / schowania (WAIT oddaje szyk 443)
                if (H447Busy(f)) return;   // 447: polowanie na gracza-wodza w toku - bez rajdu / schowania (rola 447 nie nadpisywana)
                if (tgt != null) { R443StartRaid(f, idx, tgt, flank, fp, fwp, now); return; }
                if (near == null) { _r4NoArchSec += step; return; }   // ostrzal bez pieszych strzelcow wroga w zasiegu (konni strzelcy / gracz) - nie ta regula
                R443StartHide(t, f, idx, near, now);
                return;
            }
            if (mode == RmRaid) { _r4RaidSec += step; R443RaidTick(t, f, idx, me, now); return; }
            // HIDE
            _r4HideSec += step;
            var inf = R443MainInf(t, f);
            if (inf == null) { _r4HideNoInf++; R443End(f, idx, now, 4); return; }
            if (R443Melee(inf) >= T442OwnMelee) { _r4HideMelee++; R443End(f, idx, now, 4); return; }
            if (now - _r4Since[idx] >= R443Latch)
            {
                if (tgt != null && _r4KRaid && hits > 0) { _r4HideToRaid++; R443End(f, idx, now, 4); _r4Exit[idx] = -1000f; R443StartRaid(f, idx, tgt, flank, fp, fwp, now); return; }
                if (hits == 0) { _r4HideCalm++; R443End(f, idx, now, 4); return; }
            }
            if (near == null) return;
            Vec2 hp;
            WorldPosition hwp;
            if (!R443HidePoint(f, inf, near, out hp, out hwp)) return;
            if (!_r4P[idx].IsValid || hp.Distance(_r4P[idx]) > R443HideMove)
            {
                if (C441SetRole(f, CrHide, MovementOrder.MovementOrderMove(hwp))) _r4P[idx] = hp;
            }
        }

        private static void R443StartRaid(Formation f, int idx, Formation tgt, bool flank, Vec2 fp, WorldPosition fwp, float now)
        {
            if (!_r4KRaid || !C441Live)
            {
                if (now >= _r4DryUntil[idx]) { _r4RaidDry++; _r4DryUntil[idx] = now + R443Latch; }
                return;
            }
            var order = flank ? MovementOrder.MovementOrderMove(fwp) : MovementOrder.MovementOrderChargeToTarget(tgt);
            if (!C441SetRole(f, CrRaid, order))
            {
                if (now >= _r4DryUntil[idx]) { _r4Refused++; _r4DryUntil[idx] = now + R443Latch; }
                return;
            }
            _r4Mode[idx] = RmRaid; _r4Since[idx] = now; _r4Tgt[idx] = tgt; _r4Flank[idx] = flank; _r4Contact[idx] = false;
            _r4P[idx] = flank ? fp : Vec2.Invalid;
            _r4TgtAt[idx] = tgt.CachedMedianPosition.AsVec2;
            _r4Men0[idx] = f.CountOfUnits; _r4TgtMen0[idx] = tgt.CountOfUnits;
            _r4RaidN++;
            if (flank) _r4RaidFlank++; else _r4RaidDirect++;
        }

        private static void R443StartHide(Team t, Formation f, int idx, Formation near, float now)
        {
            var inf = R443MainInf(t, f);
            Vec2 hp;
            WorldPosition hwp;
            if (inf == null || R443Melee(inf) >= T442OwnMelee || !R443HidePoint(f, inf, near, out hp, out hwp))
            {
                if (now >= _r4DryUntil[idx]) { _r4HideNoInfN++; _r4DryUntil[idx] = now + R443Latch; }
                return;
            }
            if (!_r4KHide || !C441Live)
            {
                if (now >= _r4DryUntil[idx]) { _r4HideDry++; _r4DryUntil[idx] = now + R443Latch; }
                return;
            }
            if (!C441SetRole(f, CrHide, MovementOrder.MovementOrderMove(hwp)))
            {
                if (now >= _r4DryUntil[idx]) { _r4Refused++; _r4DryUntil[idx] = now + R443Latch; }
                return;
            }
            _r4Mode[idx] = RmHide; _r4Since[idx] = now; _r4P[idx] = hp; _r4Tgt[idx] = null; _r4Flank[idx] = false;
            _r4HideN++;
        }

        private static void R443RaidTick(Team t, Formation f, int idx, Vec2 me, float now)
        {
            var tg = _r4Tgt[idx];
            int men0 = _r4TgtMen0[idx];
            bool alive = tg != null && tg.CountOfUnits >= T442MinMen && MissionTeams.Enemies(t, tg.Team);
            if (!alive || (men0 > 0 && tg.CountOfUnits <= (1f - R443TgtBroken) * men0)) { R443End(f, idx, now, 1); return; }
            if (!_r4Contact[idx] && R443Melee(tg) >= R443TgtMelee) { _r4Contact[idx] = true; _r4RaidContact++; }
            if (f.CountOfUnits < (1f - R443RaidLoss) * _r4Men0[idx]) { _r4CutLoss++; R443End(f, idx, now, 2); return; }
            if (now - _r4Since[idx] > R443RaidMax) { _r4CutTime++; R443End(f, idx, now, 2); return; }
            if (!_r4Flank[idx]) return;
            // dojazd z boku: w 30 m od punktu albo droga wprost otwarta -> szarza
            Vec2 ep = tg.CachedMedianPosition.AsVec2;
            if (!ep.IsValid) return;
            bool open = !T442Blocked(t, tg, me, ep);
            if (open || (_r4P[idx].IsValid && me.Distance(_r4P[idx]) < R443FlankReach))
            {
                if (C441SetRole(f, CrRaid, MovementOrder.MovementOrderChargeToTarget(tg))) { _r4Flank[idx] = false; _r4FlankToCharge++; }
                return;
            }
            // cel odszedl - punkt od nowa; brak drogi - koniec
            if (_r4TgtAt[idx].IsValid && ep.Distance(_r4TgtAt[idx]) > R443FlankStale)
            {
                Vec2 p;
                WorldPosition wp;
                if (!R443FlankPoint(t, tg, f, me, ep, out p, out wp)) { _r4CutRoute++; R443End(f, idx, now, 2); return; }
                if (C441SetRole(f, CrRaid, MovementOrder.MovementOrderMove(wp))) { _r4P[idx] = p; _r4TgtAt[idx] = ep; }
            }
        }

        /// <summary>Oddanie roli. why: 0 bez wyniku (szyk zniknal / gracz), 1 cel rozbity, 2 rajd przerwany, 4 koniec schowania (licznik powodu u wolajacego).</summary>
        private static void R443End(Formation f, int idx, float now, int why)
        {
            if (_r4Mode[idx] == RmRaid)
            {
                var tg = _r4Tgt[idx];
                int men0 = _r4TgtMen0[idx];
                if (men0 > 0)
                {
                    int left = tg != null ? Math.Max(0, tg.CountOfUnits) : 0;
                    _r4TgtLossSum += Math.Max(0.0, 1.0 - (double)left / men0);
                    _r4RaidDone++;
                }
                if (why == 1) _r4RaidBroken++;
                else if (why == 2) _r4RaidCut++;
            }
            try { if (f != null) C441SetRole(f, CrNone, default(MovementOrder)); } catch (Exception e) { _r4Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Raid443 oddanie", e); }
            R443CavClear(idx);
            _r4Exit[idx] = now;
        }

        /// <summary>Szyki strzelcow wroga (piesi, >= 8, nie zwiazani walka) do 250 m: cel rajdu - najblizszy odsloniety, inaczej najblizszy z droga z boku;
        /// near - najblizszy strzelec w zasiegu (takze zasloniety); expNear - odsloniety w 30-200 m (pomiar jak 437).</summary>
        private static void R443FindRaid(Team t, Formation f, Vec2 me, out Formation tgt, out bool flank, out Vec2 fp, out WorldPosition fwp, out Formation near, out bool expNear)
        {
            tgt = null; near = null; flank = false; expNear = false; fp = Vec2.Invalid; fwp = default(WorldPosition);
            float bDirect = float.MaxValue, bFlank = float.MaxValue, bNear = float.MaxValue;
            Formation flankTgt = null;
            Vec2 flankP = Vec2.Invalid;
            WorldPosition flankW = default(WorldPosition);
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
                    if (eq == null || !eq.IsRangedFormation || eq.IsCavalryFormation || eq.IsRangedCavalryFormation) continue;
                    Vec2 ep = e.CachedMedianPosition.AsVec2;
                    if (!ep.IsValid) continue;
                    float d = me.Distance(ep);
                    if (d > R443RaidRange) continue;
                    if (d < bNear) { bNear = d; near = e; }
                    if (R443Melee(e) >= T442OwnMelee) continue;   // strzelcy zwiazani walka - cel 442 (zwiazany), nie rajd
                    bool blocked = T442Blocked(t, e, me, ep);
                    if (!blocked)
                    {
                        if (d >= R443ExpMin && d <= R443ExpMax) expNear = true;
                        if (d < bDirect) { bDirect = d; tgt = e; }
                        continue;
                    }
                    if (tgt != null) continue;   // jest cel wprost - z boku niepotrzebny
                    Vec2 p;
                    WorldPosition wp;
                    if (!R443FlankPoint(t, e, f, me, ep, out p, out wp)) continue;
                    float path = me.Distance(p) + p.Distance(ep);
                    if (path < bFlank) { bFlank = path; flankTgt = e; flankP = p; flankW = wp; }
                }
            }
            if (tgt != null) return;
            if (flankTgt != null) { tgt = flankTgt; flank = true; fp = flankP; fwp = flankW; }
        }

        /// <summary>Punkt z boku strzelcow (pol szerokosci + 40 m, strona blizsza jezdzie) na siatce nawigacji; oba odcinki (jazda - punkt, punkt - strzelcy) bez
        /// zaslony piechoty wroga.</summary>
        private static bool R443FlankPoint(Team t, Formation e, Formation f, Vec2 me, Vec2 ep, out Vec2 p, out WorldPosition wp)
        {
            p = Vec2.Invalid;
            wp = default(WorldPosition);
            Vec2 dir = e.Direction;
            if (!dir.IsValid || dir.LengthSquared < 0.0001f)
            {
                Vec2 a = ep - me;
                if (a.LengthSquared < 0.01f) return false;
                dir = a.Normalized();
            }
            else dir = dir.Normalized();
            Vec2 right = dir.RightVec();
            float off = 0.5f * Math.Max(1f, e.Width) + R443FlankOff;
            Vec2 p1 = ep + right * off, p2 = ep - right * off;
            if (me.DistanceSquared(p2) < me.DistanceSquared(p1)) { var x = p1; p1 = p2; p2 = x; }
            for (int k = 0; k < 2; k++)
            {
                Vec2 c = k == 0 ? p1 : p2;
                if (T442Blocked(t, e, me, c) || T442Blocked(t, e, c, ep)) continue;
                WorldPosition w = e.CachedMedianPosition;
                w.SetVec2(c);
                if (!w.IsValid || w.GetNavMesh() == UIntPtr.Zero) continue;
                p = c; wp = w;
                return true;
            }
            return false;
        }

        /// <summary>Punkt za glowna piechota (strona przeciwna do strzelcow) na siatce nawigacji; przy braku siatki - polowa odstepu.</summary>
        private static bool R443HidePoint(Formation f, Formation inf, Formation near, out Vec2 p, out WorldPosition wp)
        {
            p = Vec2.Invalid;
            wp = default(WorldPosition);
            Vec2 ip = inf.CachedMedianPosition.AsVec2, ap = near.CachedMedianPosition.AsVec2;
            if (!ip.IsValid || !ap.IsValid) return false;
            Vec2 d = ap - ip;
            if (d.LengthSquared < 1f) return false;
            d = d.Normalized();
            float back = 0.5f * inf.Depth + R443HideGap + 0.5f * f.Depth;
            for (int k = 0; k < 2; k++)
            {
                Vec2 c = ip - d * (k == 0 ? back : 0.5f * back);
                WorldPosition w = inf.CachedMedianPosition;
                w.SetVec2(c);
                if (!w.IsValid || w.GetNavMesh() == UIntPtr.Zero) continue;
                p = c; wp = w;
                return true;
            }
            return false;
        }

        /// <summary>Glowna (najwieksza) piechota druzyny, >= 10 ludzi.</summary>
        private static Formation R443MainInf(Team t, Formation cav)
        {
            Formation best = null;
            var fl = t.FormationsIncludingSpecialAndEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                var x = fl[j];
                if (x == null || ReferenceEquals(x, cav) || x.CountOfUnits < R443InfMin) continue;
                var xq = x.QuerySystem;
                if (xq == null || !xq.IsInfantryFormation) continue;
                if (best == null || x.CountOfUnits > best.CountOfUnits) best = x;
            }
            return best;
        }

        private static float R443GapOf(Formation f)
        {
            float d2 = f.CachedClosestEnemyFormationDistanceSquared;
            var nq = f.CachedClosestEnemyFormation;
            var nf = nq != null ? nq.Formation : null;
            if (nf == null || d2 < 0f || d2 >= 1e10f) return float.MaxValue;
            return Math.Max(0f, (float)Math.Sqrt(d2) - 0.5f * (f.Depth + nf.Depth));
        }

        /// <summary>Udzial ludzi w zwarciu (miara 442 - cios wrecz w 6 s), raz na takt na szyk.</summary>
        private static float R443Melee(Formation f)
        {
            float v;
            if (_r4Melee.TryGetValue(f, out v)) return v;
            v = T442MeleeShare(f);
            _r4Melee[f] = v;
            return v;
        }

        // ------------------------------------------------------------ strzelcy: wymiana ognia i jazda wroga

        private static void R443Arch(Team t, Formation f, int idx, bool ok, int hits, float now)
        {
            if (!ReferenceEquals(_r4AWho[idx], f)) { _r4AWho[idx] = f; _r4DuelUntil[idx] = _r4CavUntil[idx] = -1000f; }
            if (!ok) { _r4DuelUntil[idx] = _r4CavUntil[idx] = -1000f; return; }
            int ti = t.TeamIndex;
            // (c) przegrana wymiana ognia druzyny i ten szyk trafiany, poza zwarciem
            if (_r4Lost[ti] && hits >= R443ArcherHits && R443Melee(f) < T442OwnMelee)
            {
                if (now >= _r4DuelUntil[idx]) { _r4DuelIn++; if (!_r4KDuel) _r4DuelDry++; }
                _r4DuelUntil[idx] = now + R443Latch;
            }
            // (d) 5.1 jazda wroga dojedzie za <= 8 s (albo jest w 20 m), z dowolnej strony
            float eta;
            if (R443CavThreat(t, f, out eta))
            {
                if (now >= _r4CavUntil[idx]) { _r4CavIn++; _r4EtaSum += eta; if (!_r4KShelter) _r4CavDry++; }
                _r4CavUntil[idx] = now + R443CavHold;
            }
        }

        private static bool R443CavThreat(Team t, Formation f, out float eta)
        {
            eta = float.MaxValue;
            Vec2 fp = f.CachedMedianPosition.AsVec2;
            if (!fp.IsValid) return false;
            var teams = t.Mission != null ? t.Mission.Teams : null;
            int n = teams != null ? teams.Count : 0;
            bool thr = false;
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
                    Vec2 to = fp - ep;
                    float len = to.Length;
                    float gap = Math.Max(0f, len - 0.5f * (f.Depth + e.Depth));
                    if (gap <= R443CavNear) { thr = true; eta = 0f; continue; }
                    Vec2 v = e.CachedCurrentVelocity;
                    if (!v.IsValid) continue;
                    float sp = v.Length;
                    if (sp <= R443CavSpeed || len < 0.1f) continue;
                    float cos = (v.x * to.x + v.y * to.y) / (sp * len);
                    if (cos <= R443CavDot) continue;
                    float tt = gap / sp;
                    if (tt <= R443CavEta) { thr = true; if (tt < eta) eta = tt; }
                }
            }
            return thr;
        }

        // ------------------------------------------------------------ linia konca bitwy (Battle291.Write)

        internal static string R443Line(string kind)
        {
            var c = Settings.Current;
            var sb = new StringBuilder(1400);
            sb.Append("Rajd i ostrzal (443): misja ").Append(kind ?? "-")
              .Append(" | klucze: rajd na strzelcow ").Append(c != null && c.CavalryRaidsArchers ? "TAK" : "NIE")
              .Append(", jazda chowa sie ").Append(c != null && c.CavalryHidesFromMissiles ? "TAK" : "NIE")
              .Append(", ocena wymiany ognia ").Append(c != null && c.ArchersJudgeMissileDuel ? "TAK" : "NIE")
              .Append(", lucznicy przed jazda ").Append(c != null && c.ArchersShelterFromCavalry ? "TAK" : "NIE")
              .Append(" | jazda AI pod ostrzalem (trafienie ludzi albo koni w 10 s, odstep >= ").Append(R443Gap.ToString("0", R4Inv)).Append(" m) ")
              .Append(_r4FireSec.ToString("0", R4Inv)).Append(" szyko-s: stala ").Append(_r4FireStill.ToString("0", R4Inv)).Append(" (").Append(R443Pct(_r4FireStill, _r4FireSec))
              .Append("), strzelcy wroga odslonieci (").Append(R443ExpMin.ToString("0", R4Inv)).Append('-').Append(R443ExpMax.ToString("0", R4Inv)).Append(" m), a jazda stala ")
              .Append(_r4ExpStill.ToString("0", R4Inv)).Append(" (").Append(R443Pct(_r4ExpStill, _r4FireSec)).Append("; cel ponizej 30%)")
              .Append(" | trafienia pociskami jazdy AI: ludzie ").Append(_r4HitRider).Append(", konie ").Append(_r4HitHorse)
              .Append(" | rajdy na strzelcow ").Append(_r4RaidN).Append(" (wprost ").Append(_r4RaidDirect).Append(", z boku ").Append(_r4RaidFlank)
              .Append(", z boku -> szarza ").Append(_r4FlankToCharge).Append("), bylby (klucz NIE / bez dowodcy jazdy) ").Append(_r4RaidDry)
              .Append(", ").Append(_r4RaidSec.ToString("0", R4Inv)).Append(" szyko-s | wynik: cel rozbity ").Append(_r4RaidBroken).Append(", zwarcie z celem ").Append(_r4RaidContact)
              .Append(", przerwany ").Append(_r4RaidCut).Append(" (straty jazdy ").Append(_r4CutLoss).Append(", czas ").Append(_r4CutTime).Append(", brak drogi ").Append(_r4CutRoute)
              .Append("), w toku przy koncu ").Append(_r4Open).Append(", cel stracil srednio ")
              .Append(_r4RaidDone > 0 ? (100.0 * _r4TgtLossSum / _r4RaidDone).ToString("0", R4Inv) + "%" : "-")
              .Append(" | jazda schowana za piechote ").Append(_r4HideN).Append(", bylaby ").Append(_r4HideDry).Append(", ").Append(_r4HideSec.ToString("0", R4Inv))
              .Append(" szyko-s, koniec: zwarcie piechoty ").Append(_r4HideMelee).Append(", ostrzal ustal ").Append(_r4HideCalm).Append(", w rajd ").Append(_r4HideToRaid)
              .Append(", brak piechoty ").Append(_r4HideNoInf).Append("; nie bylo gdzie sie schowac ").Append(_r4HideNoInfN)
              .Append("; pod ostrzalem bez pieszych strzelcow wroga w zasiegu ").Append(_r4NoArchSec.ToString("0", R4Inv)).Append(" szyko-s")
              .Append(" | rola odrzucona ").Append(_r4Refused).Append(", oddana przez 441 ").Append(_r4Back)
              .Append(" | wymiana ognia: druzyno-s przegranej ").Append(_r4LostSec.ToString("0", R4Inv)).Append(", wyrownanej / wygranej ").Append(_r4EvenSec.ToString("0", R4Inv))
              .Append(", wejsc w przegrana ").Append(_r4LostIn).Append("; strzelcy pod przegrana (wejscia) ").Append(_r4DuelIn).Append(", cofnieci ").Append(_r4DuelTake)
              .Append(", bylby (klucz NIE) ").Append(_r4DuelDry).Append(", wystawieni ponownie ").Append(_r4DuelOut)
              .Append(" | lucznicy przed jazda (dojazd <= ").Append(R443CavEta.ToString("0", R4Inv)).Append(" s albo ").Append(R443CavNear.ToString("0", R4Inv)).Append(" m): zagrozen ")
              .Append(_r4CavIn).Append(" (srednio ").Append(_r4CavIn > 0 ? (_r4EtaSum / _r4CavIn).ToString("0.0", R4Inv) : "-").Append(" s), schowani ").Append(_r4CavTake)
              .Append(", bylby (klucz NIE) ").Append(_r4CavDry).Append(", wyszli po zagrozeniu ").Append(_r4CavOut)
              .Append(" | schowanie S4 z powodow 443: nie ostrzeliwal sie ").Append(_r4NotSkirm).Append(", bez ScreenedSkirmish ").Append(_r4NoBeh)
              .Append(", puszczone po zmianie taktyki ").Append(_r4S4Lost).Append(", bez oslony piechoty ").Append(_r4NoScreenSec.ToString("0", R4Inv))
              .Append(" szyko-s, w schowaniu ").Append(_r4HeldSec.ToString("0", R4Inv)).Append(" szyko-s (naraz maks ").Append(_r4HeldMax).Append(')')
              .Append(" | potkniecia ").Append(_r4Stum).Append('.');
            return sb.ToString();
        }

        private static string R443Pct(float a, float b)
        {
            return b > 0.5f ? (100f * a / b).ToString("0", R4Inv) + "%" : "-";
        }
    }
}
