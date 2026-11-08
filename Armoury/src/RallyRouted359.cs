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
    /// 359 (08.11, Jeff: "jak uciekaja zolnierze a dowodca glowny zyje moze do nich podbiec i zawrocic uciekajacych i uciekajacy moga sie przeformowac zanim uciekna
    /// za mape i wrocic na pole bitwy"; decyzje Jeffa 08.11: zawracaja wodz strony i kapitanowie formacji - lord / towarzysz, zywi i nie w panice - AI i gracz; dowodca AI
    /// jedzie do uciekajacych tylko z eskorta i nie w boj; raz na zolnierza na bitwe, szansa wedlug morale i Leadership; druga panika = ucieka na dobre).
    /// Klucz RallyRoutedMen (dziala przy BrokenMenNeverRally i RoutedMenNeverRally - to maszyna 142). Tylko bitwa polowa (RoutKeeper.NoReturnHere - zakres 142).
    ///
    /// ZASIEG PASYWNY (RoutKeeper.RallyTick, glowny watek, poza TickList - Tick309 przed wczesnym wyjsciem): co 0.5 s
    ///  - zawracajacy: Team.GeneralAgent i Formation.Captain kazdego szyku (bohater), czynny, nie znika, nie w PANICE (139), poza zbiorem zlamanych, poza odwrotem
    ///    rannego 143 i wycofaniem rannego 149, HP od 45% (HeroCaution.WoundedBelow), bez natywnego odwrotu; gracz (Agent.Main) zywy w swojej druzynie - bez klawisza;
    ///  - strona rozbita (aktywnych mniej niz 0.3 x wroga albo glowny szyk w odwrocie - RBM TacticCoordinatedRetreat daje wtedy morale 0 calej druzynie) nie zawraca;
    ///  - kandydat: wpis zbioru zlamanych (bez nowej petli po Mission.Agents) po przejeciu (Pending false) i co najmniej 2 s od zlamania (handler paniki gry juz dal
    ///    Formation = null), bez szyku, AI, nie probowany w tej bitwie, HP od WoundedFleePercent (ranny i tak poszedlby w 143 / BrokenMen), nie w przejsciu paniki
    ///    (IsPanicked bez IsRetreating), zaden wrog w 30 m (Mission.GetNearbyEnemyAgentCount), do 12 m od zawracajacego tej druzyny;
    ///  - bron: bron biala albo strzelecka z amunicja; bez broni - tylko gdy w 20 m lezy bron biala (SpawnedItemEntity) do podniesienia, dalej niz 30 m od wroga
    ///    (bez niej rzut NIE jest zuzyty - moze trafic na bron pozniej);
    ///  - rzut raz na zolnierza: p = 0.10 + 0.35 L + 0.25 M + G - W - S (0.05..0.80): L = Leadership/300 (do 300), M = morale poczatkowe/100 (0.15..1), G = +0.10 wodz,
    ///    W = 0.30 x ubytek HP zawracajacego, S = 0.40 x przewaga strat wlasnych nad strat wroga (udzial ubytku od szczytu aktywnych); najwyzej 8 rzutow na takt.
    /// ZAWROCENIE (najwyzej 4 na klatke, reszta w kolejce): wpis zdjety (Drop - Knows false), StopRetreating komponentu i natywne, morale = morale powrotu gry
    /// (InitialMorale / 2 - nie 0.02: pierwsza kara AgentMoraleInteractionLogic dalaby od razu druga panike), zbiorka 6 m za zawracajacym (od wroga) z rozrzutem
    /// 2-4 m; bezbronny najpierw idzie po bron (HumanAIComponent.MoveToUsableGameObject + UseGameObject - wzor biegacza 158). Postoj 6 s od dotarcia (3 m), najwyzej
    /// 15 s, potem do szyku: szyk sprzed paniki, szyk zawracajacego, szyk klasy postaci (>= 3 ludzi, nie w odwrocie, bez zalamania kompanii). Porazka (bez broni,
    /// bez szyku, zawracajacy padl / spanikowal) - ReAdmit: z powrotem w zbiorze zlamanych BEZ Panic (bez okrzyku i zrzutu), morale 0.02, natywny odwrot - na dobre.
    /// Druga panika zawroconego (gra, BrokenMen, ucieczka FieldCraft / kompania / 307e) - zwykla droga Break, liczona tutaj. Mission.CanAgentRout_AdditionalCondition
    /// NIE uzywane (wolane z OnTickParallel - watki robocze): zawrocony bez IsRunningAway i odwrotu komponentu i tak nie znika (Mission.CanAgentRout).
    /// Dowodca AI jedzie do grupy uciekajacych za linia - HeroCaution.Rally359.cs (grupy z RallyTargets). Okrzyki - BattleSound.Orders.cs (OnRoutTurned).
    /// Wyjatek na czlowieku - licznik (RoutHold.SiteRally), po 5 potknieciach ReAdmit; bez globalnego wylacznika. Linia "ZBIORKA (359)" po "PANIKA BEZ POWROTU (142)".
    /// </summary>
    internal static class Rally359
    {
        internal const float ScanEvery = 0.5f, StepEvery = 0.25f;
        internal const float Reach = 12f;           // zasieg pasywny zawracajacego
        internal const float FoeClear = 30f;        // kandydat / grupa / bron dalej od wroga
        internal const float MinBroken = 2f;        // od zlamania (handler paniki gry: Formation = null)
        internal const float ArmsRange = 20f;       // bron do podniesienia
        internal const float Back = 6f;             // zbiorka za zawracajacym
        internal const float Arrive = 3f;
        internal const float Hold = 6f, GatherMax = 15f;
        internal const float GroupRadius = 15f, GroupFar = 120f;
        internal const int GroupMin = 3, RollsPerScan = 8, StartsPerFrame = 4, ArmsSearchPerScan = 4, MaxFails = 5;
        internal const float BrokenSide = 0.3f;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- liczniki misji (glowny watek; zeruje RoutHold.Ensure)
        internal static readonly HashSet<int> RalGen = new HashSet<int>(), RalCap = new HashSet<int>();
        internal static bool RalPlayer;
        internal static readonly HashSet<int> InRange = new HashSet<int>(), SkipWounded = new HashSet<int>(), SkipNoArms = new HashSet<int>(), SkipFoe = new HashSet<int>();
        internal static int Rolls, OkGen, OkCap, OkPlayer, Fail, Gone, BrokenSideScans;
        internal static int RideStart, RideArrived, RideNoEscort, RideThreat, RideTime, RideGroup, RideWound, RideOther, RideDone, RideGuards;
        internal static int ArmsPicked, ArmsTried;
        internal static int BackOwn, BackLead, BackClass, ReAdmits, NoForm;
        internal static int RePanicGame, RePanicBroken, RePanicFlee, RePanicRbm;
        internal static int FellAfter, Stum;
        internal static int Starts;
        internal static int CostTicks;
        internal static long CostSum, CostMax;
        // 359b: bron w ucieczce (RoutedKeepArms359)
        internal static int ArmsKept;

        internal static void ResetCounters()
        {
            RalGen.Clear(); RalCap.Clear(); RalPlayer = false;
            InRange.Clear(); SkipWounded.Clear(); SkipNoArms.Clear(); SkipFoe.Clear();
            Rolls = OkGen = OkCap = OkPlayer = Fail = Gone = BrokenSideScans = 0;
            RideStart = RideArrived = RideNoEscort = RideThreat = RideTime = RideGroup = RideWound = RideOther = RideDone = RideGuards = 0;
            ArmsPicked = ArmsTried = 0;
            BackOwn = BackLead = BackClass = ReAdmits = NoForm = 0;
            RePanicGame = RePanicBroken = RePanicFlee = RePanicRbm = 0;
            FellAfter = Stum = Starts = 0;
            CostTicks = 0; CostSum = CostMax = 0;
            System.Threading.Interlocked.Exchange(ref ArmsKept, 0);
        }

        internal static void Stumble(string where, Exception e)
        {
            Stum++;
            RoutHold.Stumble(RoutHold.SiteRally, where, e);
        }

        internal static void Cost(long ticks)
        {
            CostTicks++;
            CostSum += ticks;
            if (ticks > CostMax) CostMax = ticks;
        }

        private static string Ms(double ticks) { return (ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.000", Inv); }

        /// <summary>Linia "ZBIORKA (359)" (RoutHold.Write pod Gate) - tylko bitwa polowa z kluczem albo z jakakolwiek praca.</summary>
        internal static void Write(Settings c, bool field)
        {
            if (!field) return;
            bool key = c != null && c.RallyRoutedMen && c.RoutedMenNeverRally && c.BrokenMenNeverRally;
            int kept = System.Threading.Volatile.Read(ref ArmsKept);
            if (!key && Rolls + Starts + kept == 0) return;
            var sb = new StringBuilder(900);
            sb.Append("ZBIORKA (359): klucz ").Append(key ? "TAK" : "NIE")
              .Append(" | zawracajacy wodz ").Append(RalGen.Count).Append(" / kapitan ").Append(RalCap.Count).Append(" / gracz ").Append(RalPlayer ? 1 : 0)
              .Append(" | w zasiegu ").Append(InRange.Count).Append(", rzutow ").Append(Rolls).Append(" (udane ").Append(OkGen + OkCap + OkPlayer).Append(": wodz ").Append(OkGen)
              .Append(" / kapitan ").Append(OkCap).Append(" / gracz ").Append(OkPlayer).Append(", nieudane ").Append(Fail).Append(')')
              .Append(", pominieci: ranni ").Append(SkipWounded.Count).Append(", bez broni ").Append(SkipNoArms.Count).Append(", wrog blisko ").Append(SkipFoe.Count)
              .Append(", znikli w kolejce ").Append(Gone).Append(", strona rozbita (taktow) ").Append(BrokenSideScans)
              .Append(" | wyjazdy dowodcow: start ").Append(RideStart).Append(", dotarl ").Append(RideArrived).Append(", koniec po zbiorce ").Append(RideDone)
              .Append(", przerwane: zagrozenie ").Append(RideThreat).Append(" / czas ").Append(RideTime).Append(" / grupa ").Append(RideGroup).Append(" / rana ").Append(RideWound)
              .Append(" / inne ").Append(RideOther).Append(", bez eskorty ").Append(RideNoEscort).Append(", straznikow wezwanych ").Append(RideGuards)
              .Append(" | zawroconych ").Append(Starts).Append(", bron podniesiona ").Append(ArmsPicked).Append(" z ").Append(ArmsTried)
              .Append(" | wrocili do szyku ").Append(BackOwn + BackLead + BackClass).Append(" (szyk sprzed paniki ").Append(BackOwn).Append(" / dowodcy ").Append(BackLead)
              .Append(" / klasy ").Append(BackClass).Append("), ReAdmit ").Append(ReAdmits).Append(" (bez szyku ").Append(NoForm).Append(')')
              .Append(" | druga panika: gra ").Append(RePanicGame).Append(" (w tym RBM-taktyka ").Append(RePanicRbm).Append("), BrokenMen ").Append(RePanicBroken)
              .Append(", ucieczka (FieldCraft / kompania / 307e) ").Append(RePanicFlee)
              .Append(" | padli po powrocie ").Append(FellAfter)
              .Append(" | bron w ucieczce (359b) ").Append(c != null && c.RoutedMenKeepArms ? "TAK" : "NIE").Append(", latka ").Append(RoutedKeepArms359.Patched ? "wpieta" : "NIE")
              .Append(", zrzut pominiety ").Append(kept)
              .Append(" | potkniecia ").Append(Stum)
              .Append(" | koszt ").Append(CostTicks > 0 ? Ms(CostSum / (double)CostTicks) : "-").Append(" ms sr. / ").Append(Ms(CostMax)).Append(" ms maks. (taktow ").Append(CostTicks).Append(").");
            Log.Info(sb.ToString());
        }
    }

    internal sealed partial class RoutKeeper
    {
        /// <summary>359: grupa uciekajacych za linia (cel wyjazdu dowodcy AI - HeroCaution.Rally359.cs).</summary>
        internal struct RallyGroup { public Vec2 C; public int N; }

        private const int RoleGen = 0, RoleCap = 1, RolePlayer = 2;
        private const int RallySiteEntry = 359;      // Entry.Site wpisu z ReAdmit (zadne liczniki 142 / NB2)

        private sealed class Rl { public Agent A; public Team T; public int Role; public Vec2 P; }
        private sealed class TInfo { public bool Broken; public Vec2 Away = Vec2.Invalid; public Vec2 MainC = Vec2.Invalid; public Vec2 ToFoe = Vec2.Invalid; public int Peak, Active; public float Lost; }
        private sealed class Rg
        {
            public Agent A, By;
            public Team T;
            public int Role, Fails, Moves;
            public float Start, Arrived = -1f, NextSet;
            public Vec2 Point = Vec2.Invalid;
            public WorldPosition Wp = WorldPosition.Invalid;
            public SpawnedItemEntity Item;
            public bool Arms, Done;
        }
        private struct RqItem { public Entry E; public Agent By; public Team T; public int Role; public SpawnedItemEntity Item; }

        private readonly HashSet<Agent> _rallyTried = new HashSet<Agent>();
        private readonly HashSet<Agent> _rallied = new HashSet<Agent>();
        private readonly Dictionary<Agent, Rg> _rallyBy = new Dictionary<Agent, Rg>();
        private readonly List<Rg> _rallyList = new List<Rg>();
        private readonly List<RqItem> _rallyQ = new List<RqItem>();
        private readonly List<Rl> _ralliers = new List<Rl>();
        private readonly Dictionary<Team, TInfo> _tinfo = new Dictionary<Team, TInfo>();
        private readonly Dictionary<Team, List<RallyGroup>> _groups = new Dictionary<Team, List<RallyGroup>>();
        private readonly Dictionary<Team, List<Vec2>> _gcand = new Dictionary<Team, List<Vec2>>();
        private readonly Dictionary<Agent, float> _armsWait = new Dictionary<Agent, float>();
        private readonly WeakGameEntity[] _rallyEnts = new WeakGameEntity[64];
        private readonly UIntPtr[] _rallyIds = new UIntPtr[64];
        private readonly Random _rallyRng = new Random(359);
        private float _rallyScanAcc, _rallyStepAcc, _rallyPeakAcc;
        private bool _rallyEnded;

        /// <summary>359: czlowiek jest teraz w zbiorce (Gather - po zawroceniu, przed szykiem). Glowny watek.</summary>
        internal bool InRally(Agent a)
        {
            return a != null && _rallyBy.ContainsKey(a);
        }

        /// <summary>359: grupy uciekajacych za linia tej druzyny z ostatniego skanu (0.5 s); null - brak.</summary>
        internal List<RallyGroup> RallyTargets(Team t)
        {
            List<RallyGroup> l;
            return t != null && _groups.TryGetValue(t, out l) && l.Count > 0 ? l : null;
        }

        /// <summary>359: ilu ludzi zbiera sie teraz przy tym zawracajacym.</summary>
        internal int GatherBy(Agent by)
        {
            int n = 0;
            for (int i = 0; i < _rallyList.Count; i++) if (!_rallyList[i].Done && ReferenceEquals(_rallyList[i].By, by)) n++;
            return n;
        }

        /// <summary>359: zbiorka czynna w tej misji (klucze i zakres 142).</summary>
        private bool RallyOn(Mission m, Settings c)
        {
            return c != null && c.RallyRoutedMen && c.RoutedMenNeverRally && c.BrokenMenNeverRally && m != null && NoReturnHere(m);
        }

        /// <summary>359b: bitwa polowa z zakresu 142 w tej misji - odczyt z dowolnego watku (pole liczone w AfterStart).</summary>
        internal bool FieldAnyThread(Mission m)
        {
            return m != null && ReferenceEquals(m, Mission) && _noReturn == 1;
        }

        private void RallyClear()
        {
            try
            {
                _rallyTried.Clear(); _rallied.Clear(); _rallyBy.Clear(); _rallyList.Clear(); _rallyQ.Clear(); _ralliers.Clear();
                _tinfo.Clear(); _groups.Clear(); _gcand.Clear(); _armsWait.Clear();
                for (int i = 0; i < _rallyEnts.Length; i++) _rallyEnts[i] = default(WeakGameEntity);
            }
            catch { }
            _rallyScanAcc = _rallyStepAcc = _rallyPeakAcc = 0f;
            _rallyEnded = false;
        }

        // ------------------------------------------------------------ takt (Tick309)

        private void RallyTick(float dt)
        {
            var m = Mission;
            var c = Settings.Current;
            if (m == null) return;
            // przeglad 359: szczyt aktywnych druzyn od poczatku bitwy (S - strona przegrywa), nie od pierwszej paniki; tani odczyt co 0.5 s
            _rallyPeakAcc += dt;
            if (_rallyPeakAcc >= Rally359.ScanEvery && c != null && c.RallyRoutedMen && !m.MissionEnded)
            {
                _rallyPeakAcc = 0f;
                try { PeakScan(m); } catch (Exception e) { Rally359.Stumble("RoutKeeper.RallySzczyt", e); }
            }
            if (_rallyList.Count == 0 && _rallyQ.Count == 0 && _list.Count == 0) { _groups.Clear(); return; }
            if (!RallyOn(m, c))
            {
                // klucz NIE w trakcie: zbiorka zdjeta; przy czynnym 142 ludzie wracaja do ucieczki na dobre (ReAdmit), bez 142 - tylko ruch zdjety (gra jak dotad)
                if (_rallyList.Count > 0 || _rallyQ.Count > 0) RallyReleaseAll(c == null || !c.RoutedMenNeverRally || !c.BrokenMenNeverRally);
                _groups.Clear();
                return;
            }
            if (m.MissionEnded)
            {
                if (!_rallyEnded) { _rallyEnded = true; RallyReleaseAll(true); }
                _groups.Clear();
                return;
            }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                float now = m.CurrentTime;
                _rallyScanAcc += dt;
                _rallyStepAcc += dt;
                if (_rallyScanAcc >= Rally359.ScanEvery)
                {
                    _rallyScanAcc = 0f;
                    try { RallyScan(m, now, c); } catch (Exception e) { Rally359.Stumble("RoutKeeper.RallyScan", e); }
                }
                if (_rallyQ.Count > 0)
                {
                    try { RallyStarts(m, now); } catch (Exception e) { Rally359.Stumble("RoutKeeper.RallyStarts", e); }
                }
                if (_rallyStepAcc >= Rally359.StepEvery && _rallyList.Count > 0)
                {
                    _rallyStepAcc = 0f;
                    bool done = false;
                    for (int i = 0; i < _rallyList.Count; i++)
                    {
                        var g = _rallyList[i];
                        if (g.Done) { done = true; continue; }
                        try { GatherStep(g, m, now, c); }
                        catch (Exception e)
                        {
                            Rally359.Stumble("RoutKeeper.Zbiorka", e);
                            if (++g.Fails >= Rally359.MaxFails) { try { ReAdmit(g, m, now, c); } catch { RallyOut(g); } }
                        }
                        if (g.Done) done = true;
                    }
                    if (done) _rallyList.RemoveAll(x => x.Done);
                }
            }
            finally
            {
                Rally359.Cost(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
            }
        }

        // ------------------------------------------------------------ skan (co 0.5 s)

        private bool RallierOk(Agent a)
        {
            if (a == null || !a.IsHuman || !a.IsHero || !a.IsActive() || a.IsFadingOut()) return false;
            if (a.IsMainAgent) return false;
            if (!a.IsAIControlled) return false;
            if (a.IsRunningAway || SurvivalMode.InPanic(a) || Knows(a) || Withdrawing(a) || HeroCaution.WoundedWithdrawing(a)) return false;
            float max = a.HealthLimit;
            return !(max > 0f) || a.Health / max >= HeroCaution.WoundedBelow;
        }

        private TInfo Info(Mission m, Team t)
        {
            TInfo ti;
            if (!_tinfo.TryGetValue(t, out ti)) { ti = new TInfo(); _tinfo[t] = ti; }
            return ti;
        }

        /// <summary>Przeglad 359: tylko szczyt aktywnych (przed pierwsza panika RallyTick wychodzi wczesnie, bez wpisow).</summary>
        private void PeakScan(Mission m)
        {
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Valid(t)) continue;
                var ti = Info(m, t);
                int act = t.ActiveAgents != null ? t.ActiveAgents.Count : 0;
                if (act > ti.Peak) ti.Peak = act;
            }
        }

        private void TeamScan(Mission m)
        {
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Valid(t)) continue;
                var ti = Info(m, t);
                int act = t.ActiveAgents != null ? t.ActiveAgents.Count : 0;
                ti.Active = act;
                if (act > ti.Peak) ti.Peak = act;
                ti.Lost = ti.Peak > 0 ? 1f - (float)act / ti.Peak : 0f;
                // glowny szyk: AI.IsMainFormation, inaczej najwiekszy
                Formation main = null;
                int best = 0;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    bool isMain = false;
                    try { isMain = f.AI != null && f.AI.IsMainFormation; } catch { }
                    if (isMain) { main = f; break; }
                    if (f.CountOfUnits > best) { best = f.CountOfUnits; main = f; }
                }
                bool retreat = false;
                ti.MainC = Vec2.Invalid;
                ti.ToFoe = Vec2.Invalid;
                if (main != null)
                {
                    try { retreat = main.GetReadonlyMovementOrderReference().OrderEnum == MovementOrder.MovementOrderEnum.Retreat; } catch { }
                    Vec2 c0 = main.CachedMedianPosition.AsVec2;
                    Vec2 d = Vec2.Invalid;
                    try { if (t.QuerySystem != null) d = t.QuerySystem.MedianTargetFormationPosition.AsVec2 - c0; } catch { d = Vec2.Invalid; }
                    if (!d.IsValid || d.LengthSquared < 1f) d = main.Direction;
                    if (c0.IsValid && d.IsValid && d.LengthSquared > 0.0001f) { ti.MainC = c0; ti.ToFoe = d.Normalized(); }
                }
                ti.Away = ti.ToFoe.IsValid ? -ti.ToFoe : Vec2.Invalid;
                ti.Broken = retreat;
            }
            // strona rozbita: aktywnych mniej niz 0.3 x aktywnych wroga
            for (int i = 0; i < nt; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Valid(t)) continue;
                var ti = Info(m, t);
                int foe = 0;
                for (int j = 0; j < nt; j++)
                {
                    var o = teams[j];
                    if (MissionTeams.Valid(o) && MissionTeams.Enemies(t, o)) foe += o.ActiveAgents != null ? o.ActiveAgents.Count : 0;
                }
                if (foe > 0 && ti.Active < Rally359.BrokenSide * foe) ti.Broken = true;
            }
        }

        private float FoeLost(Mission m, Team t)
        {
            float lost = 0f;
            int n = 0;
            foreach (var kv in _tinfo)
            {
                if (!MissionTeams.Valid(kv.Key) || !MissionTeams.Enemies(t, kv.Key)) continue;
                lost += kv.Value.Lost * kv.Value.Peak;
                n += kv.Value.Peak;
            }
            return n > 0 ? lost / n : 0f;
        }

        private void RallyScan(Mission m, float now, Settings c)
        {
            TeamScan(m);
            _ralliers.Clear();
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Valid(t)) continue;
                var ti = Info(m, t);
                if (ti.Broken) { Rally359.BrokenSideScans++; continue; }
                Agent g = null;
                try { g = t.GeneralAgent; } catch { g = null; }
                if (RallierOk(g)) { _ralliers.Add(new Rl { A = g, T = t, Role = RoleGen, P = g.Position.AsVec2 }); Rally359.RalGen.Add(g.Index); }
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    var cap = f.Captain;
                    if (cap == null || ReferenceEquals(cap, g) || !RallierOk(cap)) continue;
                    bool dup = false;
                    for (int k = 0; k < _ralliers.Count; k++) if (ReferenceEquals(_ralliers[k].A, cap)) { dup = true; break; }
                    if (dup) continue;
                    _ralliers.Add(new Rl { A = cap, T = t, Role = RoleCap, P = cap.Position.AsVec2 });
                    Rally359.RalCap.Add(cap.Index);
                }
            }
            // gracz - zawraca sam, bez klawisza (decyzja Jeffa)
            var main = Agent.Main;
            if (main != null && main.IsActive() && !main.IsFadingOut() && MissionTeams.Valid(main.Team))
            {
                TInfo mi;
                if (_tinfo.TryGetValue(main.Team, out mi) && !mi.Broken) { _ralliers.Add(new Rl { A = main, T = main.Team, Role = RolePlayer, P = main.Position.AsVec2 }); Rally359.RalPlayer = true; }
            }

            foreach (var kv in _gcand) kv.Value.Clear();
            int rolls = 0, searches = 0;
            float cut = c.WoundedFleePercent / 100f;
            float r2 = Rally359.Reach * Rally359.Reach;
            for (int i = 0; i < _list.Count; i++)
            {
                var e = _list[i];
                Agent a;
                try
                {
                    int cand = Candidate(e, now, cut);
                    if (cand < 0) continue;
                    a = e.A;
                    var t = a.Team;
                    Vec2 p = a.Position.AsVec2;
                    if (!p.IsValid) continue;
                    // najblizszy zawracajacy tej druzyny w 12 m
                    int best = -1;
                    float bd = r2;
                    for (int k = 0; k < _ralliers.Count; k++)
                    {
                        var rl = _ralliers[k];
                        if (!ReferenceEquals(rl.T, t)) continue;
                        float d = rl.P.DistanceSquared(p);
                        if (d <= bd) { bd = d; best = k; }
                    }
                    if (cand == 1)
                    {
                        if (best >= 0) Rally359.SkipWounded.Add(a.Index);   // ranny w zasiegu - i tak poszedlby w 143 / BrokenMen
                        continue;
                    }
                    if (best < 0)
                    {
                        // poza zasiegiem - kandydat do grupy za linia (wyjazd dowodcy AI)
                        TInfo ti;
                        if (_tinfo.TryGetValue(t, out ti) && !ti.Broken)
                        {
                            List<Vec2> gl;
                            if (!_gcand.TryGetValue(t, out gl)) { gl = new List<Vec2>(); _gcand[t] = gl; }
                            if (gl.Count < 200) gl.Add(p);
                        }
                        continue;
                    }
                    if (rolls >= Rally359.RollsPerScan) continue;
                    Rally359.InRange.Add(a.Index);
                    if (m.GetNearbyEnemyAgentCount(t, p, Rally359.FoeClear) > 0) { Rally359.SkipFoe.Add(a.Index); continue; }
                    SpawnedItemEntity item = null;
                    if (!HasArms(a))
                    {
                        float wait;
                        if (_armsWait.TryGetValue(a, out wait) && now < wait) continue;
                        if (searches >= Rally359.ArmsSearchPerScan) continue;
                        searches++;
                        item = FindArms(a, m, t);
                        if (item == null) { _armsWait[a] = now + 3f; Rally359.SkipNoArms.Add(a.Index); continue; }   // rzut NIE zuzyty
                    }
                    rolls++;
                    var rl2 = _ralliers[best];
                    _rallyTried.Add(a);
                    Rally359.Rolls++;
                    float pr = Chance(m, a, rl2);
                    if (_rallyRng.NextDouble() >= pr) { Rally359.Fail++; continue; }
                    if (rl2.Role == RoleGen) Rally359.OkGen++; else if (rl2.Role == RoleCap) Rally359.OkCap++; else Rally359.OkPlayer++;
                    _rallyQ.Add(new RqItem { E = e, By = rl2.A, T = t, Role = rl2.Role, Item = item });
                }
                catch (Exception ex) { Rally359.Stumble("RoutKeeper.RallyKandydat", ex); }
            }
            Groups(m);
        }

        /// <summary>Kandydat bez warunku odleglosci (A3): tanie warunki najpierw, bez zapytan natywnych. 0 = kandydat, 1 = ranny ponizej WoundedFleePercent, -1 = nie.</summary>
        private int Candidate(Entry e, float now, float cut)
        {
            if (e == null || e.Dead || e.Pending || e.ByPlayer || now - e.Since < Rally359.MinBroken) return -1;
            var a = e.A;
            if (a == null || _rallyTried.Contains(a)) return -1;                   // przeglad 359: tani warunek przed natywnymi
            if (!a.IsActive() || a.IsFadingOut() || !a.IsAIControlled || a.IsMainAgent) return -1;
            if (a.Formation != null || !MissionTeams.Valid(a.Team)) return -1;
            var cai = a.CommonAIComponent;
            if (cai == null || (cai.IsPanicked && !cai.IsRetreating)) return -1;   // przejscie paniki (Panic przed Retreat handlera)
            float max = a.HealthLimit;
            if (max > 0f && a.Health / max < cut) return 1;
            return 0;
        }

        /// <summary>Bron biala albo strzelecka z amunicja.</summary>
        private static bool HasArms(Agent a)
        {
            try { return a.HasMeleeWeaponCached || a.HasRangedWeapon(true); }
            catch { return false; }
        }

        /// <summary>Bron biala do podniesienia w 20 m od czlowieka, dalej niz 30 m od wroga (zapytanie natywne o pudlo sceny, najwyzej 64 przedmioty).</summary>
        private SpawnedItemEntity FindArms(Agent a, Mission m, Team t)
        {
            var sc = m.Scene;
            if (sc == null) return null;
            Vec3 pos = a.Position;
            float r = Rally359.ArmsRange;
            Vec3 mn = pos - new Vec3(r, r, 4f), mx = pos + new Vec3(r, r, 4f);   // przeglad 359: +-4 m (bron na stoku)
            int n = 0, foeQ = 0;
            try { n = sc.SelectEntitiesInBoxWithScriptComponent<SpawnedItemEntity>(ref mn, ref mx, _rallyEnts, _rallyIds, false); }
            catch { n = 0; }
            SpawnedItemEntity best = null;
            float bd = r * r;
            var model = MissionGameModels.Current != null ? MissionGameModels.Current.ItemPickupModel : null;
            for (int i = 0; i < n && i < _rallyEnts.Length; i++)
            {
                SpawnedItemEntity sie = null;
                try { sie = _rallyEnts[i].GetFirstScriptOfType<SpawnedItemEntity>(); } catch { sie = null; }
                if (sie == null || sie.HasUser || sie.HasAIMovingTo || sie.IsStuckMissile() || sie.IsBanner()) continue;
                var w = sie.WeaponCopy;
                if (w.IsEmpty || w.IsShield() || w.CurrentUsageItem == null || !w.CurrentUsageItem.IsMeleeWeapon) continue;
                Vec2 ip;
                try { if (sie.GameEntityWithWorldPosition.GetNavMesh() == UIntPtr.Zero) continue; ip = sie.GameEntityWithWorldPosition.AsVec2; } catch { continue; }
                float d = ip.DistanceSquared(pos.AsVec2);
                if (d > bd) continue;
                var slot = MissionEquipment.SelectWeaponPickUpSlot(a, w, false);
                if (slot == EquipmentIndex.None) continue;
                if (model != null && !model.IsItemAvailableForAgent(sie, a, slot)) continue;
                if (++foeQ > 8) break;                                                 // przeglad 359: najwyzej 8 zapytan o wroga na szukanie
                if (m.GetNearbyEnemyAgentCount(t, ip, Rally359.FoeClear) > 0) continue;
                best = sie;
                bd = d;
            }
            for (int i = 0; i < n && i < _rallyEnts.Length; i++) _rallyEnts[i] = default(WeakGameEntity);
            return best;
        }

        private float Chance(Mission m, Agent a, Rl rl)
        {
            var by = rl.A;
            float lead = 0f;
            try { lead = by.Character != null ? by.Character.GetSkillValue(DefaultSkills.Leadership) : 0f; } catch { lead = 0f; }
            float L = Math.Min(lead, 300f) / 300f;
            float mor = 0.5f;
            try { var cai = a.CommonAIComponent; if (cai != null) mor = cai.InitialMorale / 100f; } catch { }
            float M = Math.Max(0.15f, Math.Min(1f, mor));
            float max = by.HealthLimit;
            float hp = max > 0f ? Math.Max(0f, Math.Min(1f, by.Health / max)) : 1f;
            float W = 0.30f * (1f - hp);
            float S = 0f;
            TInfo ti;
            if (_tinfo.TryGetValue(rl.T, out ti)) S = 0.40f * Math.Max(0f, ti.Lost - FoeLost(m, rl.T));
            float G = 0f;
            if (rl.Role == RoleGen) G = 0.10f;
            else if (rl.Role == RolePlayer) { try { if (rl.T != null && ReferenceEquals(rl.T.GeneralAgent, by)) G = 0.10f; } catch { } }   // przeglad 359: gracz-wodz strony
            float p = 0.10f + 0.35f * L + 0.25f * M + G - W - S;
            return Math.Max(0.05f, Math.Min(0.80f, p));
        }

        /// <summary>Grupy za linia: co najmniej 3 kandydatow w 15 m od siebie, srodek za glownym szykiem (od strony wroga), bez wroga w 45 m od srodka.</summary>
        private void Groups(Mission m)
        {
            foreach (var kv in _groups) kv.Value.Clear();
            foreach (var kv in _gcand)
            {
                var t = kv.Key;
                var pts = kv.Value;
                if (pts.Count < Rally359.GroupMin) continue;
                TInfo ti;
                if (!_tinfo.TryGetValue(t, out ti) || ti.Broken || !ti.MainC.IsValid || !ti.ToFoe.IsValid) continue;
                List<RallyGroup> gl;
                if (!_groups.TryGetValue(t, out gl)) { gl = new List<RallyGroup>(); _groups[t] = gl; }
                float gr2 = Rally359.GroupRadius * Rally359.GroupRadius;
                int queries = 0;
                for (int i = 0; i < pts.Count && gl.Count < 6 && queries < 8; i++)
                {
                    Vec2 p = pts[i];
                    if (!p.IsValid) continue;
                    int n = 0;
                    float sx = 0f, sy = 0f;
                    for (int j = 0; j < pts.Count; j++)
                    {
                        Vec2 q = pts[j];
                        if (!q.IsValid || q.DistanceSquared(p) > gr2) continue;
                        n++; sx += q.x; sy += q.y;
                    }
                    if (n < Rally359.GroupMin) continue;
                    Vec2 cen = new Vec2(sx / n, sy / n);
                    for (int j = 0; j < pts.Count; j++) if (pts[j].IsValid && pts[j].DistanceSquared(p) <= gr2) pts[j] = Vec2.Invalid;   // przydzieleni
                    if ((cen - ti.MainC).DotProduct(ti.ToFoe) >= 0f) continue;                                                      // nie za linia
                    queries++;
                    if (m.GetNearbyEnemyAgentCount(t, cen, Rally359.FoeClear + Rally359.GroupRadius) > 0) continue;
                    gl.Add(new RallyGroup { C = cen, N = n });
                }
            }
        }

        // ------------------------------------------------------------ zawrocenie (najwyzej 4 na klatke)

        private void RallyStarts(Mission m, float now)
        {
            int n = 0;
            while (_rallyQ.Count > 0 && n < Rally359.StartsPerFrame)
            {
                var q = _rallyQ[0];
                _rallyQ.RemoveAt(0);
                try { if (StartOne(q, m, now)) n++; }
                catch (Exception e)
                {
                    Rally359.Stumble("RoutKeeper.Zawrocenie", e);
                    // przeglad 359: wyjatek po Drop - czlowiek bylby poza zbiorem zlamanych i poza zbiorka; wraca do ucieczki na dobre (cicho)
                    try
                    {
                        var a = q.E != null ? q.E.A : null;
                        Rg g;
                        if (a != null && !_by.ContainsKey(a))
                        {
                            if (!_rallyBy.TryGetValue(a, out g)) g = new Rg { A = a, By = q.By, T = q.T, Role = q.Role, Start = now };
                            ReAdmit(g, m, now, Settings.Current);
                        }
                    }
                    catch { }
                }
            }
        }

        private bool StartOne(RqItem q, Mission m, float now)
        {
            var e = q.E;
            var a = e != null ? e.A : null;
            var by = q.By;
            Entry cur;
            if (a == null || e.Dead || !_by.TryGetValue(a, out cur) || !ReferenceEquals(cur, e) || !a.IsActive() || a.IsFadingOut() || !a.IsAIControlled
                || a.Formation != null || by == null || !by.IsActive() || !ReferenceEquals(by.Team, a.Team))
            { Rally359.Gone++; if (a != null) _rallyTried.Remove(a); return false; }   // przeglad 359: rzut bez startu nie przepada
            var cai = a.CommonAIComponent;
            if (cai == null) { Rally359.Gone++; _rallyTried.Remove(a); return false; }
            // bron wzieta / zajeta przez innego w kolejce: bezbronny - rzut wraca (nastepny skan poszuka innej); z wlasna bronia - bez podnoszenia
            bool itemBusy = q.Item != null && (q.Item.HasUser || q.Item.HasAIMovingTo);
            if (itemBusy && !HasArms(a)) { Rally359.Gone++; _rallyTried.Remove(a); return false; }
            if (itemBusy) q.Item = null;
            // wyjscie z odwrotu: wpis zdjety (Knows false), komponent i natywny odwrot
            Drop(e, false);
            _rallied.Add(a);
            BrokenFate.Rallied(a);
            if (cai.IsRetreating) cai.StopRetreating();   // IsPanicked / IsRetreating zerowane + natywne StopRetreating
            a.StopRetreating();                           // natywne: RoutKeeper.Decide pchal Agent.Retreat(pick)
            try { a.DisableScriptedMovement(); } catch { }
            a.SetMorale(Math.Max(cai.RecoveryMorale, 0.5f * cai.InitialMorale));
            var g = new Rg { A = a, By = by, T = a.Team, Role = q.Role, Start = now, NextSet = now };
            _rallyBy[a] = g;
            _rallyList.Add(g);
            Rally359.Starts++;
            // punkt zbiorki: 6 m za zawracajacym (od wroga), rozrzut 2-4 m
            Vec2 bp = by.Position.AsVec2;
            Vec2 away = Vec2.Invalid;
            TInfo ti;
            if (_tinfo.TryGetValue(a.Team, out ti) && ti.Away.IsValid) away = ti.Away;
            if (!away.IsValid) { var f = by.Formation; if (f != null && f.Direction.IsValid && f.Direction.LengthSquared > 0.25f) away = -f.Direction.Normalized(); }
            if (!away.IsValid) away = (a.Position.AsVec2 - bp).IsValid && (a.Position.AsVec2 - bp).LengthSquared > 0.25f ? (a.Position.AsVec2 - bp).Normalized() : new Vec2(1f, 0f);
            double ang = _rallyRng.NextDouble() * Math.PI * 2.0;
            float rad = 2f + (float)_rallyRng.NextDouble() * 2f;
            Vec2 pt = bp + away * Rally359.Back + new Vec2((float)Math.Cos(ang) * rad, (float)Math.Sin(ang) * rad);
            if (!m.IsPositionInsideBoundaries(pt)) pt = bp;
            g.Point = pt;
            if (m.Scene != null) g.Wp = new WorldPosition(m.Scene, UIntPtr.Zero, new Vec3(pt.x, pt.y, by.Position.z), false);
            if (q.Item != null && !HasArms(a))
            {
                g.Item = q.Item;
                Rally359.ArmsTried++;
                try
                {
                    // przeglad 359: bez przejecia cudzego roszczenia (petla UsableMissionObject: while (HasAIMovingTo) MovingAgent.StopUsingGameObject())
                    var haic = a.HumanAIComponent;
                    if (haic != null && a.CanBeAssignedForScriptedMovement() && !q.Item.HasAIMovingTo && haic.GetCurrentlyMovingGameObject() == null)
                        haic.MoveToUsableGameObject(q.Item, null, Agent.AIScriptedFrameFlags.NoAttack);
                    else g.Item = null;
                }
                catch { g.Item = null; }
            }
            if (g.Item == null) SetPoint(g);
            else g.Arms = false;
            // 359d: "Turn back!" przy kazdym zawroceniu (dotad pierwsze zawrocenie dowodcy i 1 na 3) - odstepy pilnuje BattleSound (mowca 6 s, druzyna 3 s)
            try { BattleSound.OnRoutTurned(by); } catch (Exception ex) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnRoutTurned", ex); }
            return true;
        }

        private void SetPoint(Rg g)
        {
            if (!g.Wp.IsValid) return;
            var w = g.Wp;
            g.A.SetScriptedPosition(ref w, false, Agent.AIScriptedFrameFlags.None);   // bez NoAttack (broni sie), bez DoNotRun
        }

        // ------------------------------------------------------------ zbiorka (co 0.25 s)

        private void GatherStep(Rg g, Mission m, float now, Settings c)
        {
            var a = g.A;
            if (a == null || !a.IsActive()) { RallyOut(g); return; }                  // los przyjdzie z OnAgentRemoved
            if (!a.IsAIControlled) { RallyRelease(g); RallyOut(g); return; }            // gracz przejal
            if (Withdrawing(a)) { RallyOut(g); return; }                                // ranny w odwrocie 143 - jego droga, pozycji nie ruszamy
            var cai = a.CommonAIComponent;
            // druga panika - Break / gra prowadzi; chwilowy odwrot komponentu bez paniki (odrzut szarzy RBM) - czekamy, pozycja wraca nizej
            if (Knows(a) || (cai != null && cai.IsPanicked)) { RallyRelease(g); RallyOut(g); return; }
            var by = g.By;
            if (by == null || !by.IsActive() || SurvivalMode.InPanic(by) || Knows(by)) { ReAdmit(g, m, now, c); return; }   // zawracajacy padl / spanikowal
            // bron
            if (g.Item != null)
            {
                if (HasArms(a))
                {
                    g.Item = null;
                    g.Arms = true;
                    Rally359.ArmsPicked++;
                    g.NextSet = now;
                }
                else if (!PickStep(g, a))
                {
                    RallyRelease(g);                                                // przeglad 359: roszczenie do broni zdjete PRZED g.Item = null (RallyRelease zeruje)
                    g.NextSet = now;
                    if (!HasArms(a)) { ReAdmit(g, m, now, c); return; }
                }
                if (g.Item != null)
                {
                    if (now - g.Start > Rally359.GatherMax) { ReAdmit(g, m, now, c); return; }
                    return;
                }
            }
            Vec2 pos = a.Position.AsVec2;
            if (g.Arrived < 0f && pos.IsValid && g.Point.IsValid && pos.DistanceSquared(g.Point) <= Rally359.Arrive * Rally359.Arrive) g.Arrived = now;
            bool ready = (g.Arrived >= 0f && now - g.Arrived >= Rally359.Hold) || now - g.Start >= Rally359.GatherMax;
            if (ready)
            {
                if (!HasArms(a)) { ReAdmit(g, m, now, c); return; }
                if (!Join(g, a)) { Rally359.NoForm++; ReAdmit(g, m, now, c); }
                return;
            }
            // pozycja zdjeta cudza reka (RBM, gra) - ponowienie co 0.5 s
            if (now >= g.NextSet)
            {
                g.NextSet = now + 0.5f;
                bool lost = false;
                try { lost = (a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) == 0; } catch { lost = false; }
                if (lost) SetPoint(g);
            }
        }

        /// <summary>Droga po bron (wzor biegacza 158): w zasiegu jak gra - UseGameObject; false = rezygnacja (przedmiot zniknal / wziety, ruch zdjety po 3 probach).</summary>
        private bool PickStep(Rg g, Agent a)
        {
            var sie = g.Item;
            if (sie == null) return false;
            if (a.IsUsingGameObject && ReferenceEquals(a.CurrentlyUsedGameObject, sie)) return true;   // animacja podnoszenia
            bool valid = false;
            try { valid = sie.GameEntity.IsValid; } catch { valid = false; }
            if (!valid) return false;
            if (sie.HasUser && !ReferenceEquals(sie.UserAgent, a)) return false;
            if (!ReferenceEquals(sie.MovingAgent, a))
            {
                if (sie.HasAIMovingTo) return false;                                // przeglad 359: idzie po nia ktos inny - bez przejecia roszczenia
                var haic = a.HumanAIComponent;
                if (++g.Moves > 3 || haic == null || !a.CanBeAssignedForScriptedMovement()) return false;
                var cur = haic.GetCurrentlyMovingGameObject();
                if (cur != null && !ReferenceEquals(cur, sie)) return false;        // idzie do innego obiektu - nie nadpisujemy
                haic.MoveToUsableGameObject(sie, null, Agent.AIScriptedFrameFlags.NoAttack);
                return true;
            }
            var mg = MissionGameModels.Current;
            float num = (mg != null && mg.AgentStatCalculateModel != null ? mg.AgentStatCalculateModel.GetInteractionDistance(a) : 1.5f) * 3f;
            var uf = sie.GetUserFrameForAgent(a);
            float dsq = uf.Origin.DistanceSquaredWithLimit(a.Position, num * num + 1E-05f);
            if (a.CanReachAndUseObject(sie, dsq)) a.UseGameObject(sie);
            return true;
        }

        /// <summary>Do szyku: sprzed paniki, zawracajacego, klasy postaci (>= 3 ludzi, nie w odwrocie, bez zalamania kompanii).</summary>
        private bool Join(Rg g, Agent a)
        {
            var t = a.Team;
            Formation f = null;
            int kind = -1;
            Formation own;
            if (_fledFrom.TryGetValue(a, out own) && JoinOk(own, t)) { f = own; kind = 0; }
            if (f == null && g.By != null && JoinOk(g.By.Formation, t)) { f = g.By.Formation; kind = 1; }
            if (f == null)
            {
                Formation cf = null;
                try { cf = t.GetFormation(a.Character.DefaultFormationClass); } catch { cf = null; }
                if (JoinOk(cf, t)) { f = cf; kind = 2; }
            }
            if (f == null) return false;
            RallyRelease(g);
            a.Formation = f;
            try { t.TriggerOnFormationsChanged(f); } catch { }
            if (a.IsHero && f.Captain == null) { try { f.Captain = a; } catch { } }
            if (kind == 0) Rally359.BackOwn++; else if (kind == 1) Rally359.BackLead++; else Rally359.BackClass++;
            RallyOut(g);
            return true;
        }

        private static bool JoinOk(Formation f, Team t)
        {
            if (f == null || !ReferenceEquals(f.Team, t) || f.CountOfUnits < 3) return false;
            try
            {
                var o = f.GetReadonlyMovementOrderReference().OrderEnum;
                if (o == MovementOrder.MovementOrderEnum.Retreat || o == MovementOrder.MovementOrderEnum.FallBack) return false;
            }
            catch { return false; }
            return !BannerCompanies.CompanyBreaking(f);
        }

        /// <summary>Nasz ruch zdjety: droga po bron (StopUsingGameObject) albo pozycja skryptowa.</summary>
        private static void RallyRelease(Rg g)
        {
            var a = g.A;
            if (a == null || !a.IsActive()) return;
            try
            {
                var sie = g.Item;
                var haic = a.HumanAIComponent;
                if (sie != null && (ReferenceEquals(sie.MovingAgent, a) || (haic != null && ReferenceEquals(haic.GetCurrentlyMovingGameObject(), sie))
                                    || (a.IsUsingGameObject && ReferenceEquals(a.CurrentlyUsedGameObject, sie)))) a.StopUsingGameObject(false);
            }
            catch { }
            g.Item = null;
            try { if ((a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) a.DisableScriptedMovement(); } catch { }
        }

        /// <summary>Wypis ze zbiorki (bez ruszania ruchu).</summary>
        private void RallyOut(Rg g)
        {
            g.Done = true;
            if (g.A != null) _rallyBy.Remove(g.A);
        }

        /// <summary>Porazka zbiorki: z powrotem w zbiorze zlamanych BEZ Panic (bez okrzyku, zrzutu broni i zarazania), morale 0.02 (pod 0.01 gra sama wolalaby
        /// Panic), natywny odwrot przez Decide - na dobre.</summary>
        private void ReAdmit(Rg g, Mission m, float now, Settings c)
        {
            var a = g.A;
            RallyRelease(g);
            RallyOut(g);
            if (a == null || !a.IsActive() || !a.IsAIControlled || _by.ContainsKey(a)) return;
            Rally359.ReAdmits++;
            var e = new Entry { A = a, Site = RallySiteEntry, Since = now, NextEval = now, Pending = false, Extinct = true, EarlyDone = true };
            _by[a] = e;
            RoutHold.MarkKnown(a, true);
            _list.Add(e);
            a.SetMorale(0.02f);
            WorldPosition gp = WorldPosition.Invalid;
            try { gp = m.GetClosestFleePositionForAgent(a); } catch { }
            e.Game = gp;
            Decide(e, m, now, c, true);
        }

        /// <summary>Koniec bitwy (zwyciezcy wiwatuja, gra prowadzi odwroty) albo klucz NIE w trakcie: zbiorka zdjeta, kolejka pusta.</summary>
        private void RallyReleaseAll(bool ended)
        {
            var m = Mission;
            var c = Settings.Current;
            for (int i = 0; i < _rallyList.Count; i++)
            {
                var g = _rallyList[i];
                if (g.Done) continue;
                try
                {
                    if (ended || m == null || c == null) { RallyRelease(g); RallyOut(g); }
                    else ReAdmit(g, m, m.CurrentTime, c);
                }
                catch (Exception e) { Rally359.Stumble("RoutKeeper.RallyKoniec", e); g.Done = true; }
            }
            _rallyList.Clear();
            _rallyBy.Clear();
            _rallyQ.Clear();
        }

        // ------------------------------------------------------------ zdarzenia (z RoutHold.cs)

        /// <summary>Break / panika / usuniecie / gracz: wypis ze zbiorki. release - ruch zdjety (przed Panic: gra da wlasny odwrot).</summary>
        private void RallyEvent(Agent a, bool release)
        {
            Rg g;
            if (a == null || !a.IsHuman || !_rallyBy.TryGetValue(a, out g)) return;   // przeglad 359: kon (panika od ciosu) nie czyta slownika
            if (release) RallyRelease(g);
            RallyOut(g);
        }

        /// <summary>Przeglad 359: zawrocony - druga panika liczona w Rally359.RePanic*, nie drugi raz w 142 / NB2. Glowny watek.</summary>
        private bool WasRallied(Agent a)
        {
            return a != null && _rallied.Contains(a);
        }

        /// <summary>Break zawroconego - druga panika (na dobre), wedlug miejsca.</summary>
        private void RallyRePanic(Agent a, int site)
        {
            if (a == null || !_rallied.Contains(a)) return;
            if (site == MissionTeams.GamePanic)
            {
                Rally359.RePanicGame++;
                TInfo ti;
                if (a.Team != null && _tinfo.TryGetValue(a.Team, out ti) && ti.Broken) Rally359.RePanicRbm++;
            }
            else if (site == MissionTeams.Broken) Rally359.RePanicBroken++;
            else Rally359.RePanicFlee++;
        }

        /// <summary>Zawrocony padl (zabity / nieprzytomny) nie w panice - "padli po powrocie".</summary>
        private void RallyRemoved(Agent a, AgentState state)
        {
            if (a == null) return;
            RallyEvent(a, false);
            if (!_rallied.Contains(a)) return;
            if ((state == AgentState.Killed || state == AgentState.Unconscious) && !Knows(a))
            {
                bool panic = false;
                try { var cai = a.CommonAIComponent; panic = cai != null && cai.IsPanicked; } catch { }
                if (!panic) Rally359.FellAfter++;
            }
        }
    }
}
