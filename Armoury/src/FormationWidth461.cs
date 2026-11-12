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
    /// 461 (pakiet 8 krok 8.5 audytu AI bitwy, docs/audyty-2026-11-11/AUDYT-AI-BITWA.md: "Omijanie stoku w natarciu, szerokosc szyku (gleboki / dopasowany) -
    /// jeden prefiks Formation.SetFormOrder; zysk do zmierzenia, nie pewny"). SAM POMIAR "bylby", BEZ REGULY I BEZ KLUCZA (wzor 458): nic nie rozkazujemy,
    /// zadnej latki, nic nie zmieniamy w misji. Klucz "Formation Width By Ground" nie wdrozony - wejdzie dopiero z regula, jesli pomiar pokaze zysk.
    ///
    /// DLACZEGO NIE REGULA (sprawdzone w dekompilacji 1.4.8 i RBM, nie zalozenie):
    ///  1. OMIJANIE STOKU nie jest do zrobienia w SetFormOrder. FormOrder zmienia tylko liczbe szeregow / szerokosc (FormOrder.OnApplyToArrangement,
    ///     mb 33870-33990: LineFormation.FormFromFlankWidth albo FlankWidth = Custom); droge szyku wyznacza MovementOrder zachowania. Natarcie piechoty AI to
    ///     BehaviorAdvance, a RBM ZASTEPUJE jego TickOccasionally (OverrideBehaviorAdvance.PrefixTickOccasionally, rbmai 4757-4793, return false) i co takt
    ///     sam liczy CalculateCurrentOrder + SetMovementOrder. Objazd stoku = nowy punkt Move w tym zachowaniu (osobny postfiks, osobny krok), w kolizji
    ///     z 439 (punkt wzgorza obroncy), 344 (zapadka rozkazow Pendulum333) i 457 / 459 (odwod za linia).
    ///  2. SZEROKOSC: RBM w tym samym prefiksie co takt ustawia szyk pieszy AI w natarciu na Custom = ludzi / 4.5 (Line), / 2.75 (Loose), / 7 (CloseOrder)
    ///     (rbmai 4769-4778) - przy odstepie Line 0.76 m i srednicy 0.4 m to stale ok. 5 szeregow, niezaleznie od wroga. Prefiks na SetFormOrder walczylby
    ///     z tym co takt, a SetFormOrder ma 66 miejsc wywolania (mb), w tym wewnetrzne Formation.SetArrangementOrder (69513, Custom z CalculateDesiredWidth,
    ///     updateDesiredFileCount NIE) i ReapplyFormOrder (71477, przy zmianie skladu) - podmiana szerokosci tam rozjezdza _desiredFileCount. Do tego rozkazy
    ///     gracza (OrderController 31817-32981) i nasze 442 / 372b. Po zwarciu RBM przechodzi w szarze (ludzie wybieraja cel sami) - ksztalt szyku liczy sie
    ///     glownie w chwili zetkniecia. Czy wezszy szyk z wystajacym wrogiem albo atak pod gore realnie traci wiecej, nie wiadomo - stad pomiar.
    ///
    /// POMIAR (glowny watek, OnMissionTick co 1 s; bitwa polowa, nie morska, nie oblezenie, nie kryjowka; druzyny AI z TeamAI bez druzyny gracza; szyki piechoty
    /// 0-7 z >= 20 ludzmi; pod kluczem pomiarow 437 BattleAiMeasurements):
    ///  - cel = najblizszy pieszy szyk wroga (>= 10 ludzi, nie jazda / konni strzelcy); odstep = odleglosc median minus pol glebokosci obu.
    ///  - PODEJSCIE (pierwszy raz odstep <= 150 m): teren drogi prostej do celu co 10 m (Scene.GetTerrainHeight) - najwiekszy spadek pod gore na 10 m (%)
    ///    i suma wzniosu; dwa objazdy przez punkt w polowie drogi przesuniety w bok o 0.35 drogi (w granicach pola) - to samo plus dlugosc. "bylby objazd":
    ///    prosta >= 15% i wznios >= 5 m, objazd <= 10%, wznios nie wiekszy, droga <= 1.3x.
    ///  - w podejsciu (odstep 5-150 m) co 1 s: rozkaz szyku (FormOrder Deep / Wide / Wider / Custom), uklad (Line / Loose / ShieldWall / inny), przestawienia
    ///    szerokosci (zmiana >= 2 m miedzy probkami - widac reset RBM co takt).
    ///  - ZETKNIECIE (pierwszy raz odstep <= 5 m): ludzie, szerokosc, glebokosc i szeregi obu; o ile wrog wystaje poza nasz bok i my poza jego (os szyku);
    ///    wysokosc naszej mediany wzgledem celu i wznios w podejsciu; zachowanie szyku; "bylby dopasowany": wrog szerszy o >= 10 m -> szerokosc celu, ale
    ///    najwyzej do 3 szeregow. Straty 60 s po zetknieciu (zabici + nieprzytomni z obu szykow, od kogokolwiek) - do porownania grup w linii konca:
    ///    wezsi z wystajacym wrogiem (>= 5 m) / reszta; pod gore (<= -2 m) / rowno / z gory (>= 2 m); bylby objazd / reszta.
    /// Linie: "Szerokosc i stok (461): ..." przy kazdym zetknieciu (najwyzej 24 na misje) i "Szerokosc i stok (461) koniec: ..." na koniec misji.
    /// OnAgentRemoved bywa wolany poza glownym watkiem (wzor 313d / 437) - tam tylko liczniki pod lockiem. Wyjatek per szyk = potkniecie (pierwsze 3 do
    /// logu), bez globalnego wylacznika. Zadnych pol MovementOrder ani FormOrder (tylko odczyt Formation.FormOrder.OrderEnum).
    /// </summary>
    internal sealed class FormationWidth461 : MissionLogic
    {
        private const string P = "Szerokosc i stok (461): ";
        private const float Every = 1f, StartDelay = 3f, NoDeployStart = 10f, ApproachGap = 150f, ContactGap = 5f, Window = 60f;
        private const float Step = 10f, DetourSide = 0.35f, SteepGrade = 0.15f, FlatGrade = 0.10f, MinClimb = 5f, DetourLen = 1.3f;
        private const float WidthJump = 2f, Overhang = 5f, WiderBy = 10f, HighDz = 2f;
        private const int MinMen = 20, TgtMin = 10, MinRanks = 3, MaxLines = 24, ErrMax = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Fs
        {
            public Formation F;
            public Team T;
            // podejscie
            public bool Ap;
            public float ApAt;
            public Vec2 ApPos;
            public float ApZ = float.NaN;
            public bool SlopeKnown, Detour;
            public float SMax, SClimb, DMax = -1f, DClimb, DLen;
            public int Samples, Jumps;
            public float LastW = -1f;
            public readonly int[] Form = new int[4];   // Deep / Wide / Wider / Custom
            public readonly int[] Arr = new int[4];    // Line / Loose / ShieldWall / inny
            // zetkniecie
            public bool Contact;
        }

        private sealed class Ev
        {
            public Formation Own, Tgt;
            public float At, End;
            public int OwnLost, TgtLost;   // pod lockiem
            public bool Narrow, Matched, Detour, DzKnown;
            public float Dz;
        }

        private bool _known, _field, _started, _ended;
        private float _startAt = -1f, _next;
        private int _stumbles, _errs, _lines, _approaches, _detours, _slopeUnknown;
        private readonly List<Fs> _fs = new List<Fs>();
        private readonly List<Ev> _ev = new List<Ev>();
        private readonly object L = new object();

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

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
                    _next = now;
                }
                if (m.MissionEnded || now < _next) return;
                _next = now + Every;
                Scan(m, now);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private Fs Of(Formation f, Team t)
        {
            for (int i = 0; i < _fs.Count; i++) if (ReferenceEquals(_fs[i].F, f)) return _fs[i];
            var s = new Fs { F = f, T = t };
            _fs.Add(s);
            return s;
        }

        private static bool Foot(Formation f)
        {
            var q = f.QuerySystem;
            return q != null && !q.IsCavalryFormationReadOnly && !q.IsRangedCavalryFormationReadOnly;
        }

        private void Scan(Mission m, float now)
        {
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!MissionTeams.Valid(t) || t.IsPlayerTeam || !t.HasTeamAi) continue;
                if (t.Side != BattleSideEnum.Attacker && t.Side != BattleSideEnum.Defender) continue;
                var fl = t.FormationsIncludingEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    try
                    {
                        var f = fl[j];
                        if (f == null || (int)f.FormationIndex >= 8 || f.CountOfUnits < MinMen) continue;
                        var q = f.QuerySystem;
                        if (q == null || !q.IsInfantryFormationReadOnly) continue;
                        var s = Of(f, t);
                        if (s.Contact) continue;
                        Watch(m, s, now);
                    }
                    catch (Exception e) { Stumble("Szyk", e); }
                }
            }
        }

        /// <summary>Najblizszy pieszy szyk wroga (>= TgtMin ludzi) i odstep (mediany minus pol glebokosci obu).</summary>
        private static Formation Target(Mission m, Team t, Formation f, out float gap)
        {
            gap = float.MaxValue;
            Formation best = null;
            Vec2 me = f.CachedMedianPosition.AsVec2;
            var teams = m.Teams;
            for (int i = 0; teams != null && i < teams.Count; i++)
            {
                var o = teams[i];
                if (!MissionTeams.Enemies(t, o)) continue;
                var fl = o.FormationsIncludingEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    var e = fl[j];
                    if (e == null || e.CountOfUnits < TgtMin || !Foot(e)) continue;
                    float g = me.Distance(e.CachedMedianPosition.AsVec2) - 0.5f * (Math.Max(0f, f.Depth) + Math.Max(0f, e.Depth));
                    if (g < gap) { gap = g; best = e; }
                }
            }
            return best;
        }

        private static float Ground(Mission m, Vec2 p)
        {
            try { var sc = m.Scene; return sc != null ? sc.GetTerrainHeight(p, true) : float.NaN; } catch { return float.NaN; }
        }

        private void Watch(Mission m, Fs s, float now)
        {
            var f = s.F;
            float gap;
            var tg = Target(m, s.T, f, out gap);
            if (tg == null || gap > ApproachGap) return;
            if (!s.Ap)
            {
                s.Ap = true;
                s.ApAt = now;
                s.ApPos = f.CachedMedianPosition.AsVec2;
                s.ApZ = Ground(m, s.ApPos);
                _approaches++;
                Slope(m, s, tg);
            }
            if (gap > ContactGap)
            {
                s.Samples++;
                var fo = f.FormOrder.OrderEnum;
                int k = fo == FormOrder.FormOrderEnum.Deep ? 0 : fo == FormOrder.FormOrderEnum.Wide ? 1 : fo == FormOrder.FormOrderEnum.Wider ? 2 : 3;
                s.Form[k]++;
                var ao = f.ArrangementOrder.OrderEnum;
                int a = ao == ArrangementOrder.ArrangementOrderEnum.Line ? 0 : ao == ArrangementOrder.ArrangementOrderEnum.Loose ? 1
                      : ao == ArrangementOrder.ArrangementOrderEnum.ShieldWall ? 2 : 3;
                s.Arr[a]++;
                float w = f.Width;
                if (s.LastW >= 0f && Math.Abs(w - s.LastW) >= WidthJump) s.Jumps++;
                s.LastW = w;
                return;
            }
            s.Contact = true;
            Touch(m, s, tg, now);
        }

        // ------------------------------------------------------------ teren drogi (bylby objazd)

        /// <summary>Droga a -> w -> b probkowana co Step m: najwiekszy spadek pod gore na odcinku (ulamek), suma wzniosu, dlugosc. false = brak wysokosci.</summary>
        private static bool PathOf(Mission m, Vec2 a, Vec2 w, Vec2 b, out float maxGrade, out float climb, out float len)
        {
            maxGrade = 0f; climb = 0f; len = 0f;
            float prev = Ground(m, a);
            if (float.IsNaN(prev)) return false;
            for (int leg = 0; leg < 2; leg++)
            {
                Vec2 p0 = leg == 0 ? a : w, p1 = leg == 0 ? w : b;
                float d = p0.Distance(p1);
                if (d < 0.5f) continue;
                int n = Math.Max(1, (int)Math.Ceiling(d / Step));
                float st = d / n;
                for (int i = 1; i <= n; i++)
                {
                    float h = Ground(m, Vec2.Lerp(p0, p1, (float)i / n));
                    if (float.IsNaN(h)) return false;
                    float dz = h - prev;
                    if (dz > 0f) { climb += dz; if (dz / st > maxGrade) maxGrade = dz / st; }
                    prev = h;
                }
                len += d;
            }
            return true;
        }

        private void Slope(Mission m, Fs s, Formation tg)
        {
            Vec2 a = s.ApPos, b = tg.CachedMedianPosition.AsVec2;
            float dist = a.Distance(b);
            if (dist < Step) { _slopeUnknown++; return; }
            float sm, sc, sl;
            if (!PathOf(m, a, (a + b) * 0.5f, b, out sm, out sc, out sl)) { _slopeUnknown++; return; }
            s.SlopeKnown = true;
            s.SMax = sm;
            s.SClimb = sc;
            Vec2 dir = (b - a).Normalized();
            Vec2 side = new Vec2(-dir.y, dir.x);
            for (int k = -1; k <= 1; k += 2)
            {
                Vec2 w = (a + b) * 0.5f + side * (k * DetourSide * dist);
                bool inside;
                try { inside = m.IsPositionInsideBoundaries(w); } catch { inside = false; }
                if (!inside) continue;
                float dm, dc, dl;
                if (!PathOf(m, a, w, b, out dm, out dc, out dl)) continue;
                if (s.DMax < 0f || dm < s.DMax) { s.DMax = dm; s.DClimb = dc; s.DLen = dl / Math.Max(1f, sl); }
            }
            s.Detour = sm >= SteepGrade && sc >= MinClimb && s.DMax >= 0f && s.DMax <= FlatGrade && s.DClimb <= sc && s.DLen <= DetourLen;
            if (s.Detour) _detours++;
        }

        // ------------------------------------------------------------ zetkniecie

        private static int Ranks(Formation f, int men)
        {
            float step = f.UnitDiameter + f.Interval;
            int files = step > 0.01f ? Math.Max(1, (int)Math.Round((f.Width + f.Interval) / step)) : 1;
            return Math.Max(1, (int)Math.Ceiling((float)men / files));
        }

        /// <summary>O ile szyk b wystaje poza boki szyku a (wieksza strona), w osi szyku a.</summary>
        private static float Over(Formation a, Formation b)
        {
            Vec2 dir = a.Direction;
            if (dir.LengthSquared < 0.01f) return 0f;
            dir = dir.Normalized();
            Vec2 right = new Vec2(dir.y, -dir.x);
            Vec2 d = b.CachedMedianPosition.AsVec2 - a.CachedMedianPosition.AsVec2;
            float off = d.x * right.x + d.y * right.y;
            float ha = 0.5f * a.Width, hb = 0.5f * b.Width;
            return Math.Max(0f, Math.Max(off + hb - ha, -off + hb - ha));
        }

        private static string FormName(int k) { return k == 0 ? "Deep" : k == 1 ? "Wide" : k == 2 ? "Wider" : "Custom"; }

        private void Touch(Mission m, Fs s, Formation tg, float now)
        {
            var f = s.F;
            int men = f.CountOfUnits, tmen = tg.CountOfUnits;
            float w = f.Width, tw = tg.Width;
            int rk = Ranks(f, men), trk = Ranks(tg, tmen);
            float foeOver = Over(f, tg), ourOver = Over(tg, f);
            float zMe = Ground(m, f.CachedMedianPosition.AsVec2), zTg = Ground(m, tg.CachedMedianPosition.AsVec2);
            bool dzKnown = !float.IsNaN(zMe) && !float.IsNaN(zTg);
            float dz = dzKnown ? zMe - zTg : 0f;
            float moved = s.ApPos.Distance(f.CachedMedianPosition.AsVec2);
            string climb = !float.IsNaN(s.ApZ) && !float.IsNaN(zMe) && moved >= Step
                ? Sg(zMe - s.ApZ) + " m na " + F0(moved) + " m (" + Sg(100f * (zMe - s.ApZ) / moved) + "%)" : "?";
            // bylby dopasowany: wrog szerszy o >= WiderBy -> szerokosc celu, najwyzej do MinRanks szeregow
            bool matched = tw >= w + WiderBy;
            float mw = w;
            int mrk = rk;
            if (matched)
            {
                float step = f.UnitDiameter + f.Interval;
                int files3 = Math.Max(1, (int)Math.Ceiling((float)men / MinRanks));
                float w3 = (files3 - 1) * step + f.UnitDiameter;
                mw = Math.Min(tw, w3);
                if (mw <= w + 1f) matched = false;
                else mrk = Math.Max(1, (int)Math.Ceiling((float)men / Math.Max(1, (int)Math.Round((mw + f.Interval) / Math.Max(0.01f, step)))));
            }
            var ev = new Ev { Own = f, Tgt = tg, At = now, End = now + Window, Narrow = foeOver >= Overhang, Matched = matched, Detour = s.Detour, DzKnown = dzKnown, Dz = dz };
            lock (L) _ev.Add(ev);
            if (_lines >= MaxLines) return;
            _lines++;
            string beh = "?";
            try { var b = f.AI != null ? f.AI.ActiveBehavior : null; beh = b != null ? b.GetType().Name : "brak"; } catch { beh = "?"; }
            string tcls = "?";
            try { var q = tg.QuerySystem; tcls = q == null ? "?" : q.IsRangedFormationReadOnly ? "strzelcy" : q.IsInfantryFormationReadOnly ? "piechota" : "inny"; } catch { tcls = "?"; }
            int fm = 0, fk = 0;
            for (int k = 0; k < 4; k++) if (s.Form[k] > fm) { fm = s.Form[k]; fk = k; }
            Log.Info(P + "t=" + F0(now) + "s | T" + s.T.TeamIndex + " " + s.T.Side + " F" + (int)f.FormationIndex + " piechota " + men + " (" + beh + ") -> F" + (int)tg.FormationIndex
                     + " " + tcls + " " + tmen + " | zetkniecie po " + F0(now - s.ApAt) + " s podejscia"
                     + " | szerokosc " + F0(w) + " / " + F0(tw) + " m (x" + (tw > 0.1f ? (w / tw).ToString("0.00", Inv) : "?") + "), szeregi ok. " + rk + " / " + trk
                     + ", wrog wystaje poza nasz bok " + F0(foeOver) + " m, my poza jego " + F0(ourOver) + " m"
                     + " | w podejsciu: rozkaz szyku " + (s.Samples > 0 ? FormName(fk) + " " + Pct(fm, s.Samples) : "-") + ", teraz " + FormName(FK(f)) + " / " + f.ArrangementOrder.OrderEnum
                     + ", przestawien szerokosci " + s.Jumps + " na " + s.Samples + " s"
                     + " | wysokosc wzgledem celu " + (dzKnown ? Sg(dz) + " m" : "?") + ", wznios w podejsciu " + climb
                     + " | droga prosta: " + (s.SlopeKnown ? "maks. " + F0(100f * s.SMax) + "% na 10 m, wznios " + F0(s.SClimb) + " m" : "?")
                     + ", objazd: " + (s.DMax >= 0f ? "maks. " + F0(100f * s.DMax) + "%, wznios " + F0(s.DClimb) + " m, droga x" + s.DLen.ToString("0.00", Inv) : "-")
                     + " | bylby (nic nie rozkazano): " + (matched ? "dopasowany " + F0(mw) + " m (ok. " + mrk + " szeregi)" : "szerokosc bez zmian")
                     + (s.Detour ? ", objazd stoku" : "") + ".");
        }

        private static int FK(Formation f)
        {
            var fo = f.FormOrder.OrderEnum;
            return fo == FormOrder.FormOrderEnum.Deep ? 0 : fo == FormOrder.FormOrderEnum.Wide ? 1 : fo == FormOrder.FormOrderEnum.Wider ? 2 : 3;
        }

        // ------------------------------------------------------------ straty 60 s po zetknieciu

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
        {
            if (_ended || affectedAgent == null) return;
            if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;
            try
            {
                if (!affectedAgent.IsHuman) return;
                var f = affectedAgent.Formation;
                if (f == null) return;
                var m = Mission;
                float now = m != null ? m.CurrentTime : 0f;
                lock (L)
                {
                    for (int i = 0; i < _ev.Count; i++)
                    {
                        var e = _ev[i];
                        if (now > e.End) continue;
                        if (ReferenceEquals(f, e.Own)) e.OwnLost++;
                        else if (ReferenceEquals(f, e.Tgt)) e.TgtLost++;
                    }
                }
            }
            catch (Exception e) { lock (L) _stumbles++; if (_errs < ErrMax) { _errs++; try { Log.Error("FormationWidth461.Los", e); } catch { } } }
        }

        // ------------------------------------------------------------ koniec misji

        private sealed class Grp { public int N, Own, Tgt; }

        private static void Add(Grp g, Ev e) { g.N++; g.Own += e.OwnLost; g.Tgt += e.TgtLost; }

        private static string G(string name, Grp g)
        {
            return name + " " + g.N + (g.N > 0 ? " (straty my / wrog " + g.Own + " / " + g.Tgt + (g.Tgt > 0 ? ", x" + ((float)g.Own / g.Tgt).ToString("0.00", Inv) : "") + ")" : "");
        }

        protected override void OnEndMission()
        {
            try
            {
                var m = Mission;
                if (_started && !_ended && _field)
                {
                    float now = m != null ? m.CurrentTime : 0f;
                    var narrow = new Grp(); var rest = new Grp(); var up = new Grp(); var level = new Grp(); var down = new Grp(); var det = new Grp(); var noDet = new Grp();
                    int matched = 0, full = 0;
                    lock (L)
                    {
                        foreach (var e in _ev)
                        {
                            if (now >= e.End) full++;
                            if (e.Matched) matched++;
                            Add(e.Narrow ? narrow : rest, e);
                            if (e.DzKnown) Add(e.Dz <= -HighDz ? up : e.Dz >= HighDz ? down : level, e);
                            Add(e.Detour ? det : noDet, e);
                        }
                    }
                    int samples = 0, jumps = 0;
                    var form = new int[4];
                    var arr = new int[4];
                    foreach (var s in _fs)
                    {
                        samples += s.Samples; jumps += s.Jumps;
                        for (int k = 0; k < 4; k++) { form[k] += s.Form[k]; arr[k] += s.Arr[k]; }
                    }
                    var sb = new StringBuilder(1024);
                    sb.Append("Szerokosc i stok (461) koniec: t=").Append(F0(now)).Append("s | sam pomiar (regula i klucz Formation Width By Ground nie wdrozone - opis 461)")
                      .Append(" | szykow piechoty AI ").Append(_fs.Count).Append(", podejsc ").Append(_approaches).Append(", zetkniec ").Append(_ev.Count)
                      .Append(" (pelne okno 60 s ").Append(full).Append(")")
                      .Append(" | straty 60 s po zetknieciu (zabici + nieprzytomni obu szykow): ").Append(G("wezsi, wrog wystaje >= 5 m", narrow)).Append("; ").Append(G("reszta", rest))
                      .Append(" | ").Append(G("pod gore (<= -2 m)", up)).Append("; ").Append(G("rowno", level)).Append("; ").Append(G("z gory (>= 2 m)", down))
                      .Append(" | ").Append(G("bylby objazd", det)).Append("; ").Append(G("bez objazdu", noDet))
                      .Append(" | bylby: dopasowany ").Append(matched).Append(", objazd stoku ").Append(_detours).Append(" z ").Append(_approaches).Append(" podejsc (teren nieznany ").Append(_slopeUnknown).Append(")")
                      .Append(" | w podejsciu (szyko-s ").Append(samples).Append("): Deep ").Append(Pct(form[0], samples)).Append(", Wide ").Append(Pct(form[1], samples))
                      .Append(", Wider ").Append(Pct(form[2], samples)).Append(", Custom ").Append(Pct(form[3], samples))
                      .Append("; Line ").Append(Pct(arr[0], samples)).Append(", Loose ").Append(Pct(arr[1], samples)).Append(", ShieldWall ").Append(Pct(arr[2], samples)).Append(", inny ").Append(Pct(arr[3], samples))
                      .Append("; przestawien szerokosci ").Append(jumps)
                      .Append(" | potkniecia ").Append(_stumbles).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            _fs.Clear();
            lock (L) _ev.Clear();
            base.OnEndMission();
        }

        private static string F0(float v) { return v.ToString("0", Inv); }

        private static string Sg(float v) { return (v >= 0f ? "+" : "") + v.ToString("0", Inv); }

        private static string Pct(int a, int b) { return b > 0 ? ((int)Math.Round(100f * a / b)).ToString(Inv) + "%" : "-"; }

        private void Stumble(string where, Exception e)
        {
            lock (L) _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("FormationWidth461." + where, e); } catch { }
        }
    }
}
