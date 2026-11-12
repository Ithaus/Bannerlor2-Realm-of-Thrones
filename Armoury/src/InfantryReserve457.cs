using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 457 (pakiet 8 krok 8.1 audytu AI bitwy, docs/audyty-2026-11-11/AUDYT-AI-BITWA.md rozdz. 3; Jeff 11.11 "rob wszystko po kolei tak jak radzisz"; tabela
    /// rozdz. 2: "brak odwodu po obu stronach - ZLE"). ODWOD PIECHOTY AI W BITWIE POLOWEJ, W SZYKU 8. Zadnych bonusow do liczb - tylko decyzja, kto gdzie stoi.
    /// Klucz InfantryReserve ("Infantry Reserve"); NIE = co do bitu jak przed (nikt nie jest przekladany, ta sama decyzja liczona i wypisana jako "bylby").
    ///
    /// WARUNKI WSTEPNE AUDYTU (sprawdzone w dekompilacji 1.4.8, rally_dec/mb_full.cs, i RBM decout/full/rbmai.cs):
    ///  - Szyk 8 (NumberOfRegularFormations) jest poza taktyka: Team.FormationsIncludingEmpty = szyki 0-7 (mb 133296-133304), na nich dzialaja TacticComponent,
    ///    ManageFormationCounts, plany RBM (rbmai 343-2095 - wszystkie przez FormationsIncludingEmpty). Szyk wodza pod RBM nie powstaje
    ///    (CanTeamHaveGeneralsFormationPatch, rbmai 2321) - zajety szyk 8 (bez RBM: wodz) = odwodu nie tworzymy.
    ///  - CZY WROG WIDZI SZYK 8: gra - tak, w wyborze celu szyku (FormationQuerySystem ClosestSignificantlyLargeEnemyFormation / Fastest... przez
    ///    FormationsIncludingSpecialAndEmpty, mb 42908 / 42946) i w sile druzyny (TeamPower, mb 44102); nie w TacticCharge.GetTacticWeight (mb 37852, szyki 0-7)
    ///    ani w gromadach wroga (mb 41196). RBM - NIE: FindSignificantEnemy / ...ToPosition / FindSignificantFormations / NearestEnemyAgent (rbmai 10812-11131)
    ///    przegladaja FormationsIncludingEmpty. Pojedynczy czlowiek walczy natywnie z kazdym wrogiem obok, wiec odwod zaatakowany sie broni.
    ///    Wniosek: odwod stojacy za linia jest dla RBM niewidoczny jako cel szyku (jak odwod za wzgorzem) - dlatego (a) wychodzi sam, gdy wrog go dopadnie
    ///    (szarza w szyku 8, potem do linii), (b) wraca do szykow 0-7, gdy linii piechoty juz nie ma albo druzyna dostaje odwrot - nigdy nie zostaje sam
    ///    w szyku 8, ktorego RBM nie widzi; (c) LocalPowerRatio liczone bez ludzi odwodu.
    ///  - KOMPANIE (BannerCompanies.cs:316 / 353 / 927): podzial i przeglad co 3 s pomijaja szyki >= 8 - przelozony do szyku 8 zostalby czlonkiem starej kompanii
    ///    (lamalby sie z nia 50 m dalej, dostawalby jej wstrzasy). ROZSTRZYGNIECIE: przy przelozeniu czlowiek wypisany z kompanii (BannerCompanies.Reserve457.cs,
    ///    Peak - 1 jak przy przeniesieniu w Put); do odwodu nie ida dowodca kompanii, chorazy, biegacz po sztandar ani czlonek kompanii zlamanej / w falach
    ///    zalamania. Po wyjsciu do szyku 0-7 przeglad 3 s zapisuje go do kompanii tego szyku (Joined) - zwykla droga posilkow.
    ///  - LocalPowerRatio gry (mb 42854: sqrt((sila swoich + 1) / (sila wrogow + 1)) w 30 m od CachedAveragePosition, 0.5-1.75) nie jest czytany przez gre ani RBM,
    ///    ale jego listy LocalAllyUnits / LocalEnemyUnits tak (LocalAllyPower, mb 35722) - liczymy TEN SAM wzor wlasnym zapytaniem (GetNearbyAllyAgents /
    ///    GetNearbyEnemyAgents do wlasnych list), bez dotykania pamieci gry (NIE co do bitu). Prog 0.8 (nie 0.6 - audyt).
    ///
    /// REGULA (glowny watek, OnMissionTick co 1 s; bitwa polowa kampanii, nie morska, nie oblezenie, nie kryjowka; druzyny AI z TeamAI - bez druzyny gracza):
    ///  - UTWORZENIE (raz na druzyne, 3-120 s po rozstawieniu, przed kontaktem - zaden wrog w 90 m od szykow piechoty): szyki piechoty AI 0-7 (IsInfantryFormation,
    ///    pod AI, nie szyk gracza, nie w odwrocie, nie w czworoboku 446) razem >= 60 ludzi; odwod = udzial wedlug profilu wodza 440 (OSTROZNY 25%, ZWYKLY 18%,
    ///    PORYWCZY 12%; klucz Commander Personality In Battle NIE - ZWYKLY), z kazdego szyku ten udzial, szyk zostaje >= 30, razem >= 12. Ludzie: piesi AI bez
    ///    luku / kuszy (IsRangedCached), nie bohaterowie, nie chorazowie, nie odlaczeni, nie przy obiekcie, bez pozycji skryptowej, nie w ucieczce / PANICE /
    ///    odwrocie rannego 143 / zbiorce 359, wolni od kompanii (wyzej) - NAJDALEJ OD WROGA (tylne szeregi). Recznie Agent.Formation do szyku 8 (wzor 381 / 454),
    ///    Move na punkt 25 m za tylem najwiekszego szyku zrodla (na linii do wroga; siatka i granice pola, zapas 15 / 8 m), twarza do wroga, SetControlledByAI(false).
    ///    Punkt odnawiany co 4 s, gdy linia sie przesunie o > 8 m. AI szyku wlaczone przez kogos - z powrotem (licznik).
    ///  - WYJSCIE (raz, zapadka), pierwszy spelniony warunek:
    ///    1. odwrot druzyny (rozkaz Retreat szyku piechoty) albo brak szyku piechoty AI 0-7 z >= 10 ludzi - od razu do szyku 0-7 (najblizszy piechoty / dowolny /
    ///       pusty szyk Infantry);
    ///    2. odwod zaatakowany: >= max(5, 20% odwodu) wrogow w 20 m od jego srodka - szarza szyku 8 (pole MovementOrderCharge odbiciem, wzor 456-c), do linii
    ///       po 5 s bez wroga w 30 m albo po 30 s;
    ///    3. PEKAJACA LINIA: szyk piechoty AI w zwarciu (>= 5 wrogow w 30 m) z LocalPowerRatio (wzor gry, bez ludzi odwodu) < 0.8 w dwoch kolejnych skanach -
    ///       caly odwod do TEGO szyku (najslabszego);
    ///    4. SZARZA NA STRZELCOW: szyk jazdy wroga (>= 5, nie konni strzelcy) jedzie na nasz szyk strzelcow AI (>= 5): w 90 m, zbliza sie > 2 m/s i dojedzie w <= 15 s,
    ///       albo w 30 m i sie zbliza; odwod w 150 m od strzelcow - szyk 8 idzie 10 m przed strzelcow w strone jazdy (oslona, punkt co 3 s), koniec po 8 s bez
    ///       jazdy wroga w 50 m od strzelcow albo po 45 s - wtedy do najblizszego szyku piechoty.
    ///    Przy NIE warunki 1, 3, 4 liczone na sucho (linia "bylby"); 2 nie ma sensu bez odwodu.
    /// Linie: "Odwod piechoty (457)" przy utworzeniu i wyjsciu (na druzyne), "Odwod piechoty (457) koniec" na koniec misji (kazda druzyna: profil, utworzenie /
    /// powod braku, wielkosc i skad, wyjscie: chwila, powod, cel, odleglosc, czas od pierwszego spadku ponizej progu, polegli w odwodzie przed wyjsciem,
    /// najnizszy stosunek sil linii, oslona strzelcow / szarza i dojscie do linii, kompanie wypisane / odmowy, AI szyku 8 cofniete, przesuniecia punktu).
    /// Wyjatek per czlowiek / szyk / druzyne = potkniecie (pierwsze 3 do logu), bez globalnego wylacznika. Pola statyczne MovementOrder tylko odbiciem (456-c).
    /// </summary>
    internal sealed class InfantryReserve457 : MissionLogic
    {
        private const string P = "Odwod piechoty (457): ";
        private const float Every = 1f, StartDelay = 3f, NoDeployStart = 10f, FormWindow = 120f, FormGap = 90f;
        private const float HoldBack = 25f, HoldEvery = 4f, HoldShift = 8f;
        private const float LocalR = 30f, RatioOut = 0.8f, AttackR = 20f, CalmR = 30f, ChargeCalm = 5f, ChargeMax = 30f;
        private const float CavFar = 90f, CavNear = 30f, CavSpeed = 2f, CavEta = 15f, CavReach = 150f, GuardAhead = 10f, GuardCalmR = 50f, GuardCalm = 8f, GuardMax = 45f, GuardEvery = 3f;
        private const float ShareCalm = 0.25f, ShareNormal = 0.18f, ShareRash = 0.12f;
        private const int InfMin = 60, KeepMin = 30, ReserveMin = 12, LineMin = 10, ContactMin = 5, AttackMin = 5, CavMin = 5, ArchMin = 5, ErrMax = 3;
        private const byte PhHold = 0, PhGuard = 1, PhCharge = 2, PhDone = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] ProfName = { "OSTROZNY", "ZWYKLY", "PORYWCZY" };

        private sealed class St
        {
            public Team T;
            public sbyte Prof = 1;
            public bool ProfKnown;
            public bool Formed, GiveUp;
            public string Why = "-", Src = "-";
            public float FormAt = -1f, HoldAt;
            public int Size, Detached, Refused, Moves, ReAi;
            public Formation F8, Anchor;
            public WorldPosition Hold = WorldPosition.Invalid;
            public readonly List<Agent> Res = new List<Agent>();
            // wyjscie
            public bool Out;
            public byte Ph = PhHold;
            public float OutAt = -1f, OutDist = -1f, PhAt, CalmAt = -1f, GuardAt;
            public int OutN, LostBefore;
            public string OutWhy = "-", OutTo = "-";
            public Formation Weak, Arch, Cav;
            public float LowAt = -1f, LowFirst = -1f, MinRatio = 9f;
            public Formation LowF;
            public float MergeAt = -1f;
            public int MergeN;
            public string MergeTo = "-";
        }

        private bool _known, _field, _started, _ended, _keyOn;
        private float _startAt = -1f, _next, _t0, _lastScan;
        private int _stumbles, _errs;
        private readonly List<St> _teams = new List<St>();
        private readonly MBList<Agent> _bufA = new MBList<Agent>(), _bufE = new MBList<Agent>();
        private readonly List<Agent> _units = new List<Agent>();
        private readonly Dictionary<Formation, Vec2> _cavPrev = new Dictionary<Formation, Vec2>();
        private readonly Dictionary<Formation, Vec2> _cavNow = new Dictionary<Formation, Vec2>();
        private Action<Agent> _collect;

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        internal static bool On { get { var s = Settings.Current; return s != null && s.InfantryReserve; } }

        private bool Field(Mission m)
        {
            if (!_known)
            {
                try { _field = m != null && m.IsFieldBattle && !m.IsNavalBattle && !m.IsSiegeBattle && !HideoutAlarm.IsHideout(m); } catch { _field = false; }
                _known = true;
            }
            return _field;
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
                    if (!Field(m)) { _ended = true; return; }
                    bool deployed = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && now >= NoDeployStart);
                    if (!deployed) return;
                    if (_startAt < 0f) { _startAt = now + StartDelay; return; }
                    if (now < _startAt) return;
                    _started = true;
                    _t0 = now;
                    _next = now;
                    _lastScan = now;
                    _keyOn = On;
                    _collect = a => _units.Add(a);
                }
                if (m.MissionEnded || now < _next) return;
                _next = now + Every;
                float step = Math.Max(now - _lastScan, 0.1f);
                _lastScan = now;
                Scan(m, now, step);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private St Of(Team t)
        {
            for (int i = 0; i < _teams.Count; i++) if (ReferenceEquals(_teams[i].T, t)) return _teams[i];
            var s = new St { T = t };
            _teams.Add(s);
            return s;
        }

        private void Scan(Mission m, float now, float step)
        {
            // jazda wroga: pozycje teraz (predkosc zblizania z poprzedniego skanu)
            _cavNow.Clear();
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team o = null;
                try { o = teams[i]; } catch { o = null; }
                if (!MissionTeams.Valid(o)) continue;
                var fl = o.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    try
                    {
                        var f = fl[j];
                        if (f == null || f.CountOfUnits < CavMin) continue;
                        var q = f.QuerySystem;
                        if (q == null || !q.IsCavalryFormationReadOnly || q.IsRangedCavalryFormationReadOnly) continue;
                        _cavNow[f] = f.CachedMedianPosition.AsVec2;
                    }
                    catch (Exception e) { Stumble("Jazda", e); }
                }
            }
            for (int i = 0; i < nt; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!MissionTeams.Valid(t) || t.IsPlayerTeam || !t.HasTeamAi) continue;
                if (t.Side != BattleSideEnum.Attacker && t.Side != BattleSideEnum.Defender) continue;
                var s = Of(t);
                try
                {
                    if (!s.Formed) { if (!s.GiveUp) TryForm(m, s, now); }
                    else if (!s.Out) Keep(m, s, now, step);
                    else if (_keyOn && s.Ph != PhDone) Phase(m, s, now);
                }
                catch (Exception e) { Stumble("Druzyna", e); }
            }
            _cavPrev.Clear();
            foreach (var kv in _cavNow) _cavPrev[kv.Key] = kv.Value;
        }

        // ------------------------------------------------------------ pomocnicze

        private static bool AiForm(Formation f, Team t)
        {
            if (f == null || f.CountOfUnits <= 0 || !ReferenceEquals(f.Team, t) || (int)f.FormationIndex >= (int)FormationClass.NumberOfRegularFormations) return false;
            if (!f.IsAIControlled || f.AI == null || HorsePursuit.PlayerForm(f)) return false;
            return true;
        }

        private static bool Inf(Formation f)
        {
            var q = f.QuerySystem;
            return q != null && q.IsInfantryFormationReadOnly;
        }

        private static bool Retreat(Formation f)
        {
            try { return f.GetReadonlyMovementOrderReference().OrderEnum == MovementOrder.MovementOrderEnum.Retreat; } catch { return false; }
        }

        /// <summary>Srodek wrogow (srednia median szykow wazona liczebnoscia); false = brak wroga.</summary>
        private static bool FoeCentre(Mission m, Team t, out Vec2 c)
        {
            c = Vec2.Zero;
            float n = 0f;
            var teams = m.Teams;
            for (int i = 0; teams != null && i < teams.Count; i++)
            {
                var o = teams[i];
                if (!MissionTeams.Enemies(t, o)) continue;
                var fl = o.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    c += f.CachedMedianPosition.AsVec2 * f.CountOfUnits;
                    n += f.CountOfUnits;
                }
            }
            if (n <= 0f) return false;
            c = c * (1f / n);
            return true;
        }

        /// <summary>Wrogowie (ludzie czynni, nie uciekajacy) w promieniu r.</summary>
        private int FoesNear(Mission m, Team t, Vec2 p, float r)
        {
            m.GetNearbyEnemyAgents(p, r, t, _bufE);
            int n = 0;
            for (int i = 0; i < _bufE.Count; i++)
            {
                var a = _bufE[i];
                if (a != null && a.IsActive() && a.IsHuman && !a.IsRunningAway) n++;
            }
            return n;
        }

        /// <summary>LocalPowerRatio wzorem gry (mb 42854) wlasnym zapytaniem, bez ludzi odwodu (f8); -1 = szyk nie w zwarciu (< 5 wrogow w 30 m).</summary>
        private float LocalRatio(Mission m, Team t, Formation f, Formation f8)
        {
            Vec2 c = f.CachedAveragePosition;
            m.GetNearbyEnemyAgents(c, LocalR, t, _bufE);
            float ep = 0f;
            int en = 0;
            for (int i = 0; i < _bufE.Count; i++)
            {
                var a = _bufE[i];
                if (a == null || !a.IsActive()) continue;
                ep += a.CharacterPowerCached;
                if (a.IsHuman && !a.IsRunningAway) en++;
            }
            if (en < ContactMin) return -1f;
            m.GetNearbyAllyAgents(c, LocalR, t, _bufA);
            float ap = 0f;
            for (int i = 0; i < _bufA.Count; i++)
            {
                var a = _bufA[i];
                if (a == null || !a.IsActive()) continue;
                if (f8 != null && ReferenceEquals(a.Formation, f8)) continue;
                ap += a.CharacterPowerCached;
            }
            return MBMath.ClampFloat(TaleWorlds.Library.MathF.Sqrt((ap + 1f) / (ep + 1f)), 0.5f, 1.75f);
        }

        /// <summary>Punkt dist m od p w kierunku dir: na siatce i w granicach pola (wzor 381 PushBack).</summary>
        private static bool Spot(Mission m, WorldPosition basePos, Vec2 p, out WorldPosition w)
        {
            w = WorldPosition.Invalid;
            if (!basePos.IsValid || !m.IsPositionInsideBoundaries(p)) return false;
            var x = basePos;
            x.SetVec2(p);
            if (x.GetNavMesh() == UIntPtr.Zero) return false;
            w = x;
            return true;
        }

        /// <summary>Miejsce odwodu: HoldBack m za tylem szyku kotwicy na linii od wroga (zapas 15 / 8 m).</summary>
        private bool HoldPoint(Mission m, Team t, Formation anchor, out WorldPosition w)
        {
            w = WorldPosition.Invalid;
            if (anchor == null || anchor.CountOfUnits <= 0) return false;
            Vec2 fc;
            if (!FoeCentre(m, t, out fc)) return false;
            var med = anchor.CachedMedianPosition;
            Vec2 ac = med.AsVec2;
            Vec2 d = fc - ac;
            if (d.LengthSquared < 1f) d = anchor.Direction;
            if (!d.IsValid || d.LengthSquared < 0.0001f) return false;
            d = d.Normalized();
            float depth = 0f;
            try { depth = anchor.Depth; } catch { depth = 0f; }
            float[] back = { HoldBack, 15f, 8f };
            for (int k = 0; k < back.Length; k++)
                if (Spot(m, med, ac - d * (depth * 0.5f + back[k]), out w)) return true;
            return false;
        }

        // ------------------------------------------------------------ utworzenie

        private void TryForm(Mission m, St s, float now)
        {
            var t = s.T;
            if (now - _t0 > FormWindow) { s.GiveUp = true; s.Why = "okno " + FormWindow.ToString("0", Inv) + " s minelo (ostatnio: " + s.Why + ")"; return; }
            bool known;
            s.Prof = Doctrine.P457Prof(t, out known);
            s.ProfKnown = known;
            Formation f8 = null;
            try { f8 = t.GetFormation(FormationClass.NumberOfRegularFormations); } catch (Exception e) { Stumble("Szyk8", e); }
            if (f8 == null) { s.GiveUp = true; s.Why = "szyk 8 niedostepny"; return; }
            if (f8.CountOfUnits > 0 || f8.PlayerOwner != null) { s.GiveUp = true; s.Why = "szyk 8 zajety (" + f8.CountOfUnits + " ludzi - szyk wodza gry)"; return; }

            // szyki zrodla
            var src = new List<Formation>();
            int total = 0;
            Formation anchor = null;
            var fl = t.FormationsIncludingEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                try
                {
                    var f = fl[j];
                    if (!AiForm(f, t) || !Inf(f) || Retreat(f) || Doctrine.Q446Holds(f)) continue;
                    src.Add(f);
                    int n = f.CountOfUnitsWithoutDetachedOnes;
                    total += n;
                    if (anchor == null || n > anchor.CountOfUnitsWithoutDetachedOnes) anchor = f;
                }
                catch (Exception e) { Stumble("Zrodlo", e); }
            }
            if (src.Count == 0) { s.Why = "brak szyku piechoty AI"; return; }
            if (total < InfMin) { s.GiveUp = true; s.Why = "piechoty za malo (" + total + " < " + InfMin + ")"; return; }
            for (int k = 0; k < src.Count; k++)
            {
                if (FoesNear(m, t, src[k].CachedMedianPosition.AsVec2, FormGap) > 0)
                {
                    s.GiveUp = true;
                    s.Why = "wrog w " + FormGap.ToString("0", Inv) + " m od piechoty przed utworzeniem";
                    return;
                }
            }
            WorldPosition hold;
            if (!HoldPoint(m, t, anchor, out hold)) { s.Why = "brak miejsca za linia (siatka / granice pola)"; return; }
            Vec2 fc;
            if (!FoeCentre(m, t, out fc)) { s.Why = "brak wroga"; return; }

            float share = s.Prof == 0 ? ShareCalm : (s.Prof == 2 ? ShareRash : ShareNormal);
            var plan = new List<Agent>();
            var given = new List<Formation>();
            var take = new StringBuilder(128);
            int nn = 0;
            for (int k = 0; k < src.Count; k++)
            {
                var f = src[k];
                int n = f.CountOfUnitsWithoutDetachedOnes;
                int give = Math.Min((int)Math.Round(n * share), n - KeepMin);
                if (give <= 0) continue;
                // kandydaci: najdalej od wroga (tylne szeregi)
                _units.Clear();
                f.ApplyActionOnEachUnit(_collect);
                var cand = new List<KeyValuePair<float, Agent>>(_units.Count);
                for (int u = 0; u < _units.Count; u++)
                {
                    var a = _units[u];
                    try
                    {
                        if (!Fit(a)) continue;
                        cand.Add(new KeyValuePair<float, Agent>(a.Position.AsVec2.DistanceSquared(fc), a));
                    }
                    catch (Exception e) { Stumble("Czlowiek", e); }
                }
                _units.Clear();
                cand.Sort((x, y) => y.Key.CompareTo(x.Key));
                give = Math.Min(give, cand.Count);
                for (int u = 0; u < give; u++) plan.Add(cand[u].Value);
                if (give > 0)
                {
                    given.Add(f);
                    take.Append(nn++ > 0 ? ", " : "").Append('F').Append((int)f.FormationIndex).Append(' ').Append(give).Append(" (zostaje ").Append(n - give).Append(')');
                }
            }
            if (plan.Count < ReserveMin)
            {
                s.GiveUp = true;
                s.Why = "za malo ludzi (" + plan.Count + " < " + ReserveMin + "; szyk zostaje >= " + KeepMin + ", udzial " + (share * 100f).ToString("0", Inv) + "%)";
                return;
            }

            if (_keyOn)
            {
                var moved = new List<Agent>(plan.Count);
                for (int u = 0; u < plan.Count; u++)
                {
                    int r = BannerCompanies.Detach457(plan[u]);
                    if (r < 0) { s.Refused++; continue; }
                    if (r > 0) s.Detached++;
                    moved.Add(plan[u]);
                }
                if (moved.Count < ReserveMin) { s.GiveUp = true; s.Why = "po odmowach kompanii za malo ludzi (" + moved.Count + ")"; Log.Info(P + "T" + t.TeamIndex + " | " + s.Why + "."); return; }
                try { f8.SetControlledByAI(false); } catch (Exception e) { Stumble("AI8", e); }   // przed przelozeniem (wzor 454)
                Transfer(t, moved, f8, given);
                s.Res.AddRange(moved);
                try
                {
                    f8.SetControlledByAI(false);
                    f8.SetMovementOrder(MovementOrder.MovementOrderMove(hold));
                    f8.SetFacingOrder(FacingOrder.FacingOrderLookAtEnemy);
                }
                catch (Exception e) { Stumble("Rozkaz8", e); }
            }
            else s.Res.AddRange(plan);
            s.Formed = true;
            s.FormAt = now;
            s.F8 = f8;
            s.Anchor = anchor;
            s.Hold = hold;
            s.HoldAt = now;
            s.Size = s.Res.Count;
            s.Src = take.ToString();
            s.Why = "utworzony";
            Log.Info(P + "t=" + now.ToString("0", Inv) + "s | T" + t.TeamIndex + " " + t.Side + " | klucz " + (_keyOn ? "TAK" : "NIE") + " | wodz " + ProfName[s.Prof]
                     + (s.ProfKnown ? "" : " (profil nieznany)") + " udzial " + (share * 100f).ToString("0", Inv) + "% | " + (_keyOn ? "odwod " : "bylby odwod ") + s.Size
                     + " z " + total + " piechoty w szyku 8, " + HoldBack.ToString("0", Inv) + " m za F" + (int)anchor.FormationIndex + " z: " + s.Src
                     + (_keyOn ? " | wypisani z kompanii " + s.Detached + ", odmowy " + s.Refused : "")
                     + " | wyjscie: linia LocalPowerRatio < " + RatioOut.ToString("0.0", Inv) + " albo szarza jazdy na strzelcow albo atak na odwod.");
        }

        private static bool Fit(Agent a)
        {
            if (a == null || !a.IsActive() || !a.IsHuman || !a.IsAIControlled || a.IsHero || a.IsMainAgent || a.IsPlayerControlled || a.HasMount || a.Banner != null
                || a.IsRangedCached || a.IsDetachedFromFormation || a.CurrentlyUsedGameObject != null || a.IsRunningAway) return false;
            if (a.GetScriptedFlags() != Agent.AIScriptedFrameFlags.None) return false;
            if (SurvivalMode.InPanic(a)) return false;
            var k = RoutHold.Live;
            if (k != null && (k.Knows(a) || k.Withdrawing(a) || k.InRally(a))) return false;
            return BannerCompanies.Free457(a);
        }

        /// <summary>Przelozenie recznie Agent.Formation z masowym przeniesieniem (wzor 454).</summary>
        private void Transfer(Team t, List<Agent> men, Formation to, List<Formation> from)
        {
            var touched = new List<Formation>();
            if (from != null) touched.AddRange(from);
            if (!touched.Contains(to)) touched.Add(to);
            var started = new List<Formation>();
            foreach (var tf in touched) { try { tf.OnMassUnitTransferStart(); started.Add(tf); } catch (Exception e) { Stumble("Start", e); } }
            try
            {
                foreach (var a in men)
                {
                    try
                    {
                        if (a.IsUsingGameObject) a.StopUsingGameObject(false, Agent.StopUsingGameObjectFlags.None);
                        a.Formation = to;
                    }
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
            try { t.QuerySystem.ExpireAfterUnitAddRemove(); } catch (Exception e) { Stumble("DruzynaQ", e); }
        }

        // ------------------------------------------------------------ trzymanie i wyzwalacze

        /// <summary>Zywi ludzie odwodu, ktorzy nie uciekaja (przy TAK - nadal w szyku 8).</summary>
        private int Alive(St s)
        {
            int n = 0;
            for (int i = 0; i < s.Res.Count; i++)
            {
                var a = s.Res[i];
                bool ok = false;
                try { ok = a != null && a.IsActive() && !a.IsRunningAway && (!_keyOn || ReferenceEquals(a.Formation, s.F8)); } catch { ok = false; }
                if (ok) n++;
            }
            return n;
        }

        private Vec2 Centre(St s)
        {
            if (_keyOn && s.F8 != null && s.F8.CountOfUnits > 0) { try { return s.F8.CachedMedianPosition.AsVec2; } catch { } }
            return s.Hold.IsValid ? s.Hold.AsVec2 : Vec2.Invalid;
        }

        private void Keep(Mission m, St s, float now, float step)
        {
            var t = s.T;
            int alive = Alive(s);
            if (_keyOn && s.F8 != null)
            {
                if (s.F8.CountOfUnits <= 0 || alive <= 0) { s.Out = true; s.Ph = PhDone; s.OutAt = now; s.OutWhy = "odwod wyginal / uciekl w miejscu"; s.LostBefore = s.Size; s.OutTo = "-"; Log.Info(P + "t=" + now.ToString("0", Inv) + "s | T" + t.TeamIndex + " | " + s.OutWhy + "."); return; }
                try
                {
                    if (s.F8.IsAIControlled) { s.F8.SetControlledByAI(false); s.F8.SetMovementOrder(MovementOrder.MovementOrderMove(s.Hold)); s.ReAi++; }
                }
                catch (Exception e) { Stumble("AI8", e); }
            }

            // szyki piechoty AI 0-7 (linia)
            Formation weak = null, anyInf = null;
            float wr = 9f;
            bool retreat = false;
            var fl = t.FormationsIncludingEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                try
                {
                    var f = fl[j];
                    if (!AiForm(f, t) || !Inf(f)) continue;
                    if (Retreat(f)) { retreat = true; continue; }
                    if (f.CountOfUnits < LineMin) continue;
                    if (anyInf == null || f.CountOfUnits > anyInf.CountOfUnits) anyInf = f;
                    float r = LocalRatio(m, t, f, _keyOn ? s.F8 : null);
                    if (r < 0f) continue;
                    if (r < s.MinRatio) s.MinRatio = r;
                    if (r < wr) { wr = r; weak = f; }
                }
                catch (Exception e) { Stumble("Linia", e); }
            }

            // punkt odwodu za linia (TAK; przy NIE tylko liczony dla odleglosci)
            if (anyInf != null && now - s.HoldAt >= HoldEvery)
            {
                s.HoldAt = now;
                WorldPosition np;
                var anchor = s.Anchor != null && AiForm(s.Anchor, t) && Inf(s.Anchor) && s.Anchor.CountOfUnits >= LineMin ? s.Anchor : anyInf;
                s.Anchor = anchor;
                if (HoldPoint(m, t, anchor, out np) && (!s.Hold.IsValid || np.AsVec2.Distance(s.Hold.AsVec2) > HoldShift))
                {
                    s.Hold = np;
                    s.Moves++;
                    if (_keyOn && s.F8 != null) { try { s.F8.SetMovementOrder(MovementOrder.MovementOrderMove(np)); } catch (Exception e) { Stumble("Punkt", e); } }
                }
            }

            string why = null;
            byte ph = PhDone;
            Formation target = null;
            if (retreat) why = "odwrot druzyny (Retreat szyku piechoty)";
            else if (anyInf == null) why = "brak szyku piechoty AI z >= " + LineMin + " ludzi";
            if (why == null && _keyOn)
            {
                Vec2 c = Centre(s);
                int foes = c.IsValid ? FoesNear(m, t, c, AttackR) : 0;
                if (foes >= Math.Max(AttackMin, (int)(0.2f * alive))) { why = "odwod zaatakowany (" + foes + " wrogow w " + AttackR.ToString("0", Inv) + " m)"; ph = PhCharge; }
            }
            if (why == null)
            {
                if (weak != null && wr < RatioOut)
                {
                    if (s.LowFirst < 0f) s.LowFirst = now;
                    if (s.LowAt >= 0f && ReferenceEquals(s.LowF, weak)) { why = "pekajaca linia F" + (int)weak.FormationIndex + " (LocalPowerRatio " + wr.ToString("0.00", Inv) + ")"; target = weak; s.Weak = weak; }
                    else { s.LowAt = now; s.LowF = weak; }
                }
                else { s.LowAt = -1f; s.LowF = null; }
            }
            if (why == null)
            {
                Formation arch, cav;
                string cw;
                if (CavOnArchers(m, s, step, out arch, out cav, out cw)) { why = cw; ph = PhGuard; s.Arch = arch; s.Cav = cav; }
            }
            if (why == null) return;
            Release(m, s, now, why, ph, target, alive);
        }

        /// <summary>Szyk jazdy wroga jedzie na nasz szyk strzelcow AI; odwod w zasiegu.</summary>
        private bool CavOnArchers(Mission m, St s, float step, out Formation arch, out Formation cav, out string why)
        {
            arch = null; cav = null; why = null;
            var t = s.T;
            Vec2 rc = Centre(s);
            if (!rc.IsValid) return false;
            float best = float.MaxValue;
            var fl = t.FormationsIncludingEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                var f = fl[j];
                try
                {
                    if (!AiForm(f, t) || f.CountOfUnits < ArchMin) continue;
                    var q = f.QuerySystem;
                    if (q == null || !q.IsRangedFormationReadOnly || q.IsRangedCavalryFormationReadOnly) continue;
                    Vec2 ac = f.CachedMedianPosition.AsVec2;
                    if (ac.Distance(rc) > CavReach) continue;
                    foreach (var kv in _cavNow)
                    {
                        var e = kv.Key;
                        if (!MissionTeams.Enemies(t, e.Team)) continue;
                        Vec2 prev;
                        if (!_cavPrev.TryGetValue(e, out prev)) continue;
                        float d = kv.Value.Distance(ac);
                        float close = (prev.Distance(ac) - d) / step;
                        bool hit = (d <= CavNear && close > 0.5f) || (d <= CavFar && close > CavSpeed && d / close <= CavEta);
                        if (!hit || d >= best) continue;
                        best = d;
                        arch = f;
                        cav = e;
                        why = "szarza jazdy F" + (int)e.FormationIndex + " (" + e.CountOfUnits + ") na strzelcow F" + (int)f.FormationIndex + " (" + d.ToString("0", Inv) + " m, "
                              + close.ToString("0.0", Inv) + " m/s)";
                    }
                }
                catch (Exception ex) { Stumble("Strzelcy", ex); }
            }
            return arch != null;
        }

        // ------------------------------------------------------------ wyjscie

        private void Release(Mission m, St s, float now, string why, byte ph, Formation target, int alive)
        {
            var t = s.T;
            s.Out = true;
            s.OutAt = now;
            s.OutWhy = why;
            s.LostBefore = s.Size - alive;
            s.OutN = alive;
            s.Ph = ph;
            s.PhAt = now;
            s.CalmAt = -1f;
            Vec2 c = Centre(s);
            if (ph == PhGuard)
            {
                s.OutTo = "oslona strzelcow F" + (int)s.Arch.FormationIndex;
                try { s.OutDist = c.IsValid ? s.Arch.CachedMedianPosition.AsVec2.Distance(c) : -1f; } catch { s.OutDist = -1f; }
                if (_keyOn) Guard(m, s, now);
            }
            else if (ph == PhCharge)
            {
                s.OutTo = "szarza szyku 8";
                s.OutDist = 0f;
                if (_keyOn)
                {
                    try { object ch = ChargeOrder(); if (ch != null) s.F8.SetMovementOrder((MovementOrder)ch); else Stumble("Szarza", new InvalidOperationException("brak MovementOrder.MovementOrderCharge")); }
                    catch (Exception e) { Stumble("Szarza", e); }
                }
            }
            else
            {
                string how;
                Formation tgt;
                if (target != null && AiForm(target, t)) { tgt = target; how = "pekajacy szyk"; }
                else tgt = Line(t, c, out how);
                if (tgt != null)
                {
                    s.OutTo = how + " F" + (int)tgt.FormationIndex + " (" + tgt.CountOfUnits + " ludzi)";
                    try { s.OutDist = c.IsValid && tgt.CountOfUnits > 0 ? tgt.CachedMedianPosition.AsVec2.Distance(c) : -1f; } catch { s.OutDist = -1f; }
                }
                else s.OutTo = "brak szyku 0-7";
                if (_keyOn) Merge(t, s, now, tgt, how);
            }
            float react = s.LowFirst >= 0f && s.Weak != null ? now - s.LowFirst : -1f;
            Log.Info(P + "t=" + now.ToString("0", Inv) + "s | T" + t.TeamIndex + " | klucz " + (_keyOn ? "TAK" : "NIE") + " | " + (_keyOn ? "wyszlo " : "bylby wypuscil ") + s.OutN
                     + " z " + s.Size + " (polegli / uciekli w odwodzie " + s.LostBefore + ") -> " + s.OutTo + " | powod: " + why + " | odleglosc "
                     + (s.OutDist >= 0f ? s.OutDist.ToString("0", Inv) + " m" : "-") + (react >= 0f ? " | od pierwszego spadku ponizej progu " + react.ToString("0", Inv) + " s" : "")
                     + " | po " + (now - s.FormAt).ToString("0", Inv) + " s w odwodzie.");
        }

        /// <summary>Szyk 0-7 dla ludzi odwodu: najblizszy szyk piechoty AI (>= 10), potem dowolny szyk AI z ludzmi, potem pusty szyk Infantry druzyny.</summary>
        private static Formation Line(Team t, Vec2 from, out string how)
        {
            how = "-";
            Formation inf = null, small = null, any = null;
            float di = float.MaxValue, ds = float.MaxValue, da = float.MaxValue;
            var fl = t.FormationsIncludingEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                var f = fl[j];
                if (!AiForm(f, t) || Retreat(f)) continue;
                float d = from.IsValid ? f.CachedMedianPosition.AsVec2.Distance(from) : 0f;
                if (Inf(f) && f.CountOfUnits >= LineMin && d < di) { di = d; inf = f; }
                if (Inf(f) && d < ds) { ds = d; small = f; }
                if (d < da) { da = d; any = f; }
            }
            if (inf != null) { how = "najblizszy szyk piechoty"; return inf; }
            if (small != null) { how = "najblizszy maly szyk piechoty"; return small; }
            if (any != null) { how = "najblizszy szyk"; return any; }
            try
            {
                var f0 = t.GetFormation(FormationClass.Infantry);
                if (f0 != null && f0.IsAIControlled && !HorsePursuit.PlayerForm(f0)) { how = "pusty szyk Infantry"; return f0; }
            }
            catch { }
            return null;
        }

        /// <summary>Wszyscy z szyku 8 do szyku celu (koniec odwodu).</summary>
        private void Merge(Team t, St s, float now, Formation tgt, string how)
        {
            s.Ph = PhDone;
            s.MergeAt = now;
            if (s.F8 == null) return;
            if (tgt == null)
            {
                // nie ma szyku 0-7 - szyk 8 szarzuje (ostatni ludzie druzyny), jak 454
                s.MergeTo = "brak szyku 0-7 - szarza szyku 8";
                try { object ch = ChargeOrder(); if (ch != null) s.F8.SetMovementOrder((MovementOrder)ch); } catch (Exception e) { Stumble("Szarza", e); }
                return;
            }
            _units.Clear();
            try { s.F8.ApplyActionOnEachUnit(_collect); } catch (Exception e) { Stumble("Zbior", e); }
            var men = new List<Agent>(_units.Count);
            for (int i = 0; i < _units.Count; i++) { var a = _units[i]; if (a != null && a.IsActive()) men.Add(a); }
            _units.Clear();
            s.MergeN = men.Count;
            s.MergeTo = how + " F" + (int)tgt.FormationIndex;
            if (men.Count > 0) Transfer(t, men, tgt, new List<Formation> { s.F8 });
        }

        /// <summary>Oslona strzelcow: szyk 8 GuardAhead m przed strzelcow w strone jazdy, twarza do jazdy.</summary>
        private void Guard(Mission m, St s, float now)
        {
            s.GuardAt = now;
            try
            {
                if (s.Arch == null || s.Arch.CountOfUnits <= 0 || s.Cav == null || s.F8 == null) return;
                var am = s.Arch.CachedMedianPosition;
                Vec2 ac = am.AsVec2;
                Vec2 cc = s.Cav.CountOfUnits > 0 ? s.Cav.CachedMedianPosition.AsVec2 : ac;
                Vec2 d = cc - ac;
                if (d.LengthSquared < 1f) d = s.Arch.Direction;
                d = d.Normalized();
                WorldPosition w;
                if (!Spot(m, am, ac + d * GuardAhead, out w) && !Spot(m, am, ac, out w)) return;
                s.F8.SetMovementOrder(MovementOrder.MovementOrderMove(w));
                s.F8.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(d));
            }
            catch (Exception e) { Stumble("Oslona", e); }
        }

        /// <summary>Faza po wyjsciu (TAK): oslona strzelcow albo szarza, potem do linii.</summary>
        private void Phase(Mission m, St s, float now)
        {
            var t = s.T;
            if (s.F8 == null || s.F8.CountOfUnits <= 0) { s.MergeTo = "odwod wyginal w " + (s.Ph == PhGuard ? "oslonie" : "szarzy"); s.Ph = PhDone; s.MergeAt = now; return; }
            try { if (s.F8.IsAIControlled) { s.F8.SetControlledByAI(false); s.ReAi++; } } catch (Exception e) { Stumble("AI8", e); }
            Vec2 c = Centre(s);
            bool calm;
            float max;
            if (s.Ph == PhGuard)
            {
                max = GuardMax;
                bool archOk = s.Arch != null && s.Arch.CountOfUnits > 0;
                calm = !archOk || !CavNearArchers(s, s.Arch.CachedMedianPosition.AsVec2);
                if (!archOk) s.CalmAt = now - GuardCalm;
                if (archOk && now - s.GuardAt >= GuardEvery) Guard(m, s, now);
                if (!calm) s.CalmAt = -1f;
            }
            else
            {
                max = ChargeMax;
                calm = !c.IsValid || FoesNear(m, t, c, CalmR) == 0;
                if (!calm) s.CalmAt = -1f;
            }
            if (calm && s.CalmAt < 0f) s.CalmAt = now;
            float need = s.Ph == PhGuard ? GuardCalm : ChargeCalm;
            bool end = (s.CalmAt >= 0f && now - s.CalmAt >= need) || now - s.PhAt >= max;
            if (!end) return;
            string how;
            var tgt = Line(t, c, out how);
            string ph = s.Ph == PhGuard ? "po oslonie strzelcow" : "po szarzy";
            Merge(t, s, now, tgt, how);
            Log.Info(P + "t=" + now.ToString("0", Inv) + "s | T" + t.TeamIndex + " | " + ph + " (" + (now - s.PhAt).ToString("0", Inv) + " s) do linii: " + s.MergeN + " ludzi -> " + s.MergeTo + ".");
        }

        private bool CavNearArchers(St s, Vec2 ac)
        {
            foreach (var kv in _cavNow)
            {
                if (!MissionTeams.Enemies(s.T, kv.Key.Team)) continue;
                if (kv.Value.Distance(ac) <= GuardCalmR) return true;
            }
            return false;
        }

        // ------------------------------------------------------------ koniec misji

        protected override void OnEndMission()
        {
            try
            {
                var m = Mission;
                if (_started && !_ended && _field)
                {
                    float now = m != null ? m.CurrentTime : 0f;
                    var sb = new StringBuilder(1024);
                    sb.Append("Odwod piechoty (457) koniec: t=").Append(now.ToString("0", Inv)).Append("s | klucz ").Append(_keyOn ? "TAK" : "NIE")
                      .Append(" | druzyn AI ").Append(_teams.Count);
                    int formed = 0, outs = 0, line = 0, arch = 0, att = 0, other = 0;
                    foreach (var s in _teams)
                    {
                        sb.Append(" || T").Append(s.T != null ? s.T.TeamIndex : -1).Append(' ').Append(s.T != null ? s.T.Side.ToString() : "?").Append(" wodz ").Append(ProfName[s.Prof])
                          .Append(s.ProfKnown ? "" : " (nieznany)").Append(": ");
                        if (!s.Formed) { sb.Append("nie utworzony - ").Append(s.Why); continue; }
                        formed++;
                        sb.Append(_keyOn ? "utworzony " : "bylby utworzony ").Append(s.Size).Append(" przy t=").Append(s.FormAt.ToString("0", Inv)).Append("s z: ").Append(s.Src);
                        if (_keyOn) sb.Append(", wypisani z kompanii ").Append(s.Detached).Append(", odmowy kompanii ").Append(s.Refused);
                        sb.Append(", punkt przesuwany ").Append(s.Moves).Append(", najnizszy stosunek sil linii ").Append(s.MinRatio < 9f ? s.MinRatio.ToString("0.00", Inv) : "-");
                        if (s.Out)
                        {
                            outs++;
                            if (s.OutWhy.StartsWith("pekajaca", StringComparison.Ordinal)) line++;
                            else if (s.OutWhy.StartsWith("szarza jazdy", StringComparison.Ordinal)) arch++;
                            else if (s.OutWhy.StartsWith("odwod zaatakowany", StringComparison.Ordinal)) att++;
                            else other++;
                            sb.Append(" | wyjscie ").Append(_keyOn ? "" : "bylby ").Append("t=").Append(s.OutAt.ToString("0", Inv)).Append("s, ").Append(s.OutN).Append(" ludzi -> ").Append(s.OutTo)
                              .Append(", powod: ").Append(s.OutWhy).Append(", polegli / uciekli przed wyjsciem ").Append(s.LostBefore)
                              .Append(", odleglosc ").Append(s.OutDist >= 0f ? s.OutDist.ToString("0", Inv) + " m" : "-");
                            if (s.LowFirst >= 0f && s.Weak != null) sb.Append(", od pierwszego spadku ").Append((s.OutAt - s.LowFirst).ToString("0", Inv)).Append(" s");
                            if (_keyOn) sb.Append(", do linii ").Append(s.MergeAt >= 0f ? "t=" + s.MergeAt.ToString("0", Inv) + "s " + s.MergeN + " ludzi -> " + s.MergeTo : "nie (faza trwala do konca)");
                        }
                        else sb.Append(" | wyjscia nie bylo (stal do konca), zywych w odwodzie ").Append(Alive(s)).Append(" z ").Append(s.Size)
                               .Append(s.LowFirst >= 0f ? ", linia ponizej progu od t=" + s.LowFirst.ToString("0", Inv) + "s" : "");
                        if (_keyOn) sb.Append(", AI szyku 8 cofniete ").Append(s.ReAi);
                    }
                    sb.Append(" || razem: utworzone ").Append(formed).Append(", wyjscia ").Append(outs).Append(" (linia ").Append(line).Append(", strzelcy ").Append(arch)
                      .Append(", atak na odwod ").Append(att).Append(", inne ").Append(other).Append(") | prog ").Append(RatioOut.ToString("0.0", Inv))
                      .Append(", udzial 25/18/12% (ostrozny / zwykly / porywczy), piechota >= ").Append(InfMin).Append(", szyk zostaje >= ").Append(KeepMin)
                      .Append(", odwod >= ").Append(ReserveMin).Append(" | potkniecia ").Append(_stumbles).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            _teams.Clear();
            _cavPrev.Clear();
            _cavNow.Clear();
            _units.Clear();
            _bufA.Clear();
            _bufE.Clear();
            base.OnEndMission();
        }

        // 456-c: pole MovementOrderCharge tylko odbiciem, tylko w trwajacej misji, raz na proces (siec CrashScribe JIT-uje metody przy starcie - konstruktor
        // statyczny MovementOrder bez misji psuje typ na caly proces). Wzor SiegeReserve454.ChargeOrder / HorsePursuit.ArmyOrders.
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
            catch (Exception e) { _charge = null; try { Log.Error("InfantryReserve457.ChargeOrder", e); } catch { } }
            return _charge;
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("InfantryReserve457." + where, e); } catch { }
        }
    }

    internal static partial class Doctrine
    {
        /// <summary>457: profil wodza 440 dla odwodu (klucz Commander Personality In Battle NIE albo druzyna bez rzutu 440 - ZWYKLY, jak 444 / 445).</summary>
        internal static sbyte P457Prof(Team t, out bool known)
        {
            known = false;
            if (t == null) return 1;
            int ti = t.TeamIndex;
            known = ti >= 0 && ti < MaxTeams && ReferenceEquals(_p4Ref[ti], t);
            return _cpKey && known ? _cpProf[ti] : (sbyte)1;
        }
    }
}
