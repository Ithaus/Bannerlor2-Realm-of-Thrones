using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 310 (31.10, Jeff "sprawdz bitwe ... realizm, bez sztucznych regul"; analiza 309/310): lucznik AI nie strzela w klebowisko ze swoimi.
    /// Dowod: w bitwach armii (autotesty 144a/145a/145b b8/b9) 12-21% trafien strzal AI dostaja swoi (razem 441/2631 = 16.8%), w bitwie Jeffa Willow Wood 17.6%
    /// (1105 pociskow AI w swoich); probka HIT: ok. 80% to cel w zwarciu ze swoimi (40-150 m), ok. 20% strzal przez wlasny szereg z bliska.
    /// Przyczyna: w SP gra anuluje trafienie AI w swojego (Mission.cs:5821/5836), wiec silnik nigdy nie dbal o swoich przy strzale; nasza bramka FF
    /// (FriendlyFire.MissileCtl) przepuszcza strzale - AI strzela, jakby swoich nie bylo. Wybor celu jest w silniku (Agent.GetTargetAgent natywne),
    /// FriendInWay silnika widzi swojego za rzadko (283 R1b: 159 na 680 promieni z przeszkoda).
    /// Regula (jedna dla obu stron, ludzie gracza pod SI tez; gracz celuje sam): po strzale AI z celem (kolejka 140 - zdarzenie strzalu, koszt rosnie ze
    /// strzalami, nie z lucznikami x klatki) kontrola tego, co zywy lucznik widzi:
    ///  1) klebowisko - swoj (albo kon swojego) przy celu w pasie rozrzutu (wzor 283 NearSpread: 2 x promien ciala + min(4.5 m, 4.5 x d / 30 m + wyprzedzenie)),
    ///     przy kazdej odleglosci (jedno zapytanie GetNearbyAgents w 10.5 m wokol celu);
    ///  2) plaski tor - cel blizej niz 40 m: promien RayCastForClosestAgent od oka do piersi celu (wzor 283 Line); dalej strzala leci lukiem nad glowami.
    /// Reakcja (klucz ArchersAvoidOwnMen; NIE = ta sama kontrola na sucho, bez reakcji):
    ///  a) nowy cel z budzetem 4 kandydatow: tylne szeregi szyku celu (FormationRankIndex, w Loose / Scatter - odleglosc od celu), potem szyk strzelcow wroga,
    ///     potem inne szyki (mediana); kazdy kandydat przechodzi te same testy; pierwszy czysty: SetAutomaticTargetSelection(false) + SetTargetAgent
    ///     (dzwignia 148/204/283, dekompilacja: wybor celu tylko w silniku); wlasnosc do smierci celu, 8 s, paniki, przejecia przez 204/283;
    ///  b) brak czystego celu - jeden czlowiek wstrzymuje ogien na 3 s (Agent.SetFiringOrder HoldYourFire), potem rozkaz szyku wraca
    ///     (Formation.FiringOrder); szyk piechoty bez wstrzymania (prefiks RBM OverrideAgent.SetFiringOrder zmienia tam rozkaz) - tylko licznik.
    /// Pominieci: gracz, bron rzucana (FF z probki to same strzaly), 204/283 i 148 (maja swoj cel), chorazy (RBM steruje celem), panika / odwrot,
    /// odlaczeni od szyku, machiny, bitwy morskie i oblezenia (na start tylko bitwa polowa).
    /// Budzet: najwyzej 6 kontroli i 12 zapytan / promieni na klatke (nadmiar liczony i pominiety); przeglad wlasnosci i wstrzyman co 0.5 s, najwyzej 16 wpisow.
    /// Strzal przychodzi po zwolnieniu cieciwy - pierwsza strzala w klebowisko i tak poleci (lucznik widzi efekt i zmienia cel).
    /// Bez zmiany obrazen i procentu FF, bez premii. Potkniecia liczone (RoutHold miejsce "tarcze"), funkcji nie gasimy.
    /// Linia "Czysty strzal (310)" raz na bitwe polowa (BattleSense.Clean po linii 140).
    /// </summary>
    internal static class CleanShot
    {
        internal const int ChecksPerFrame = 6, QueriesPerFrame = 12, CandMax = 4, ReviewPer = 16, UnitScan = 120;
        internal const float FlatRange = 40f, NearR = ShieldsUp.RaiseNear + ShieldsUp.MissRadius, OwnFor = 8f, HoldFor = 3f, ReviewEvery = 0.5f, Screen = 30f;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Own
        {
            public Agent A, T;
            public bool Owned, Held, Due;
            public float OwnUntil, HoldUntil, HoldStart;
        }

        private static WeakReference _m;
        private static bool _live, _key, _written;
        private static string _kind = "?";
        private static float _now, _acc;
        private static int _frameChecks, _frameQ, _rot, _scan;
        private static bool _anyDue;
        private static long _frameTicks, _sumTicks, _maxTicks;

        private static readonly Dictionary<Agent, Own> By = new Dictionary<Agent, Own>();
        private static readonly List<Own> L = new List<Own>();
        private static readonly Dictionary<int, float> HoldSum = new Dictionary<int, float>();
        private static readonly MBList<Agent> Buf = new MBList<Agent>();
        private static readonly List<Agent> Units = new List<Agent>();
        private static readonly Action<Agent> Collect = a => Units.Add(a);
        private static readonly List<Agent> Cand = new List<Agent>();
        private static readonly List<byte> CandKind = new List<byte>();

        // liczniki misji (glowny watek)
        private static int _seen, _checks, _clean, _dirty, _line, _over, _queries, _rays, _stum;
        private static readonly int[] DirtyBy = new int[4], LineBy = new int[4];
        private static int _newRear, _newRanged, _newOther, _held, _heldInf, _noCand, _shotHeld, _reDirty;
        private static int _relDead, _relTime, _relPanic, _relTaken, _relBanner, _relEnd;
        private static int _skPlayer, _skThrown, _skNoTarget, _skHunt, _skBanner, _skPanic, _skDetached, _skObject;
        private static float _holdSecs, _holdMax;
        private static int _holdN;

        /// <summary>Kolejka 140 zywa takze dla 310 (ShieldsUp.Begin / Drain).</summary>
        internal static bool Live { get { return _live; } }

        /// <summary>ShieldsUp.Begin (po kluczach 140, bitwa nie morska): stan od zera; tylko bitwa polowa.</summary>
        internal static void Begin(Mission m, Settings c)
        {
            Clear();
            _live = false;
            _written = false;
            _m = null;
            if (m == null) return;
            bool field = false;
            try { field = m.IsFieldBattle && !m.IsNavalBattle && !m.IsNavalRaidBattle && !HideoutAlarm.IsHideout(m); } catch { field = false; }
            if (!field) return;
            _m = new WeakReference(m);
            _kind = MissionTeams.Kind(m);
            _key = c == null || c.ArchersAvoidOwnMen;
            _now = m.CurrentTime;
            _live = true;
        }

        private static void Clear()
        {
            By.Clear(); L.Clear(); HoldSum.Clear(); Buf.Clear(); Units.Clear(); Cand.Clear(); CandKind.Clear();
            _acc = 0f; _frameChecks = _frameQ = _rot = _scan = 0; _anyDue = false;
            _frameTicks = _sumTicks = _maxTicks = 0;
            _seen = _checks = _clean = _dirty = _line = _over = _queries = _rays = _stum = 0;
            Array.Clear(DirtyBy, 0, 4); Array.Clear(LineBy, 0, 4);
            _newRear = _newRanged = _newOther = _held = _heldInf = _noCand = _shotHeld = _reDirty = 0;
            _relDead = _relTime = _relPanic = _relTaken = _relBanner = _relEnd = 0;
            _skPlayer = _skThrown = _skNoTarget = _skHunt = _skBanner = _skPanic = _skDetached = _skObject = 0;
            _holdSecs = _holdMax = 0f; _holdN = 0;
        }

        private static bool Mine(Mission m)
        {
            var w = _m;
            return _live && m != null && w != null && ReferenceEquals(w.Target, m);
        }

        private static int Bucket(float d) { return d < 10f ? 0 : d < 40f ? 1 : d < 100f ? 2 : 3; }

        private static void Stumble(string where, Exception e)
        {
            _stum++;
            RoutHold.Stumble(RoutHold.SiteShields, where, e);
        }

        // ------------------------------------------------------------ klatka (ShieldsUp.Drain / Tick, glowny watek)

        /// <summary>Poczatek Drain: budzet klatki od zera.</summary>
        internal static void Frame()
        {
            _frameChecks = 0;
            _frameQ = 0;
            _frameTicks = 0;
        }

        /// <summary>Koniec Drain: czas klatki do linii 310 i do 309 (modul bez dokladania do sumy klatki - liczony juz w "tarcze 140").</summary>
        internal static void FrameEnd()
        {
            if (_frameTicks <= 0) return;
            _sumTicks += _frameTicks;
            if (_frameTicks > _maxTicks) _maxTicks = _frameTicks;
            FrameCost309.AddTicks(FrameCost309.MClean, _frameTicks);
            _frameTicks = 0;
        }

        /// <summary>Strzal AI z kolejki 140 (glowny watek, po wdrozeniu): kontrola celu i reakcja.</summary>
        internal static void OnShot(Mission m, Agent s, bool thrown)
        {
            if (!Mine(m) || s == null) return;
            long t0 = Stopwatch.GetTimestamp();
            try { Shot(m, s, thrown); }
            catch (Exception e) { Stumble("CleanShot.Strzal", e); }
            _frameTicks += Stopwatch.GetTimestamp() - t0;
        }

        private static void Shot(Mission m, Agent s, bool thrown)
        {
            if (!s.IsActive() || !s.IsHuman || s.Team == null) return;
            _seen++;
            if (!s.IsAIControlled || s.IsMainAgent || s.IsPlayerControlled) { _skPlayer++; return; }
            if (thrown) { _skThrown++; return; }
            Agent t = null;
            try { t = s.GetTargetAgent(); } catch { t = null; }
            if (t == null || !t.IsActive()) { _skNoTarget++; return; }
            Agent tm = t.IsMount ? t.RiderAgent : t;   // cel-kon: strona jezdzca
            if (tm == null || tm.Team == null || !tm.Team.IsEnemyOf(s.Team)) { _skNoTarget++; return; }
            if (HorsePursuit.HuntAnyThread(s) || HorsePursuit.RidingAnyThread(s)) { _skHunt++; return; }
            if (HorsePursuit.Banner(s)) { _skBanner++; return; }
            if (s.IsRunningAway || SurvivalMode.InPanic(s) || s.IsRetreating()) { _skPanic++; return; }
            if (s.Formation == null || s.IsDetachedFromFormation) { _skDetached++; return; }
            if (s.IsUsingGameObject) { _skObject++; return; }
            Own e;
            By.TryGetValue(s, out e);
            if (e != null && e.Held) { _shotHeld++; return; }   // strzal mimo wstrzymania (rozkaz szyku nadpisal) - przeglad zdejmie
            if (_frameChecks >= ChecksPerFrame || _frameQ >= QueriesPerFrame) { _over++; return; }
            _frameChecks++;
            _checks++;
            Vec2 from = s.Position.AsVec2;
            float d = from.Distance(t.Position.AsVec2);
            bool dirty = Dirty(m, s, t, from, d);
            bool line = false;
            if (!dirty && d < FlatRange)
            {
                int r = Line(m, s, t);
                if (r < 0) { _over++; return; }
                line = r == 0;
            }
            if (!dirty && !line) { _clean++; return; }
            int b = Bucket(d);
            if (dirty) { _dirty++; DirtyBy[b]++; } else { _line++; LineBy[b]++; }
            if (e != null && e.Owned) _reDirty++;
            if (!_key) return;   // klucz NIE: tylko pomiar
            byte kind;
            Agent c = Pick(m, s, t, d, out kind);
            if (c != null)
            {
                if (e == null) { e = new Own { A = s }; By[s] = e; L.Add(e); }
                s.SetAutomaticTargetSelection(false);
                s.SetTargetAgent(c);
                e.Owned = true;
                e.T = c;
                e.OwnUntil = _now + OwnFor;
                if (kind == 0) _newRear++; else if (kind == 1) _newRanged++; else _newOther++;
                return;
            }
            _noCand++;
            if (e != null && e.Owned) Release(e, false);   // nasz cel tez brudny - automat gry wraca (i wstrzymanie ponizej)
            var f = s.Formation;
            var q = f != null ? f.QuerySystem : null;
            if (q != null && q.IsInfantryFormation) { _heldInf++; return; }   // prefiks RBM na SetFiringOrder w szyku piechoty - bez wstrzymania
            if (e == null) { e = new Own { A = s }; By[s] = e; L.Add(e); }
            s.SetFiringOrder(FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire);
            e.Held = true;
            e.HoldStart = _now;
            e.HoldUntil = _now + HoldFor;
            _held++;
        }

        /// <summary>1) Klebowisko: swoj (albo kon swojego) przy celu w pasie rozrzutu strzalu z punktu from. Jedno zapytanie.</summary>
        private static bool Dirty(Mission m, Agent s, Agent t, Vec2 from, float d)
        {
            _frameQ++;
            _queries++;
            Vec2 tp = t.Position.AsVec2;
            Vec2 dir = tp - from;
            if (d < 1f) return false;
            dir = dir * (1f / d);
            float rad = 0.4f;
            try { if (s.Monster != null) rad = s.Monster.BodyCapsuleRadius; } catch { }
            float va = 0f, tv = 0f;
            try { var w = s.WieldedWeapon; var u = w.CurrentUsageItem; va = u != null ? u.MissileSpeed : 0f; } catch { va = 0f; }
            try { tv = (t.IsMount ? t : (t.MountAgent ?? t)).MovementVelocity.Length; } catch { tv = 0f; }
            float lead = va > 10f ? tv * d / va : 0f;
            float allow = 2f * rad + Math.Min(ShieldsUp.MissRadius, ShieldsUp.MissRadius * d / Screen + lead);
            var side = s.Team.Side;
            Agent tr = t.IsMount ? t.RiderAgent : t, tmnt = t.IsMount ? t : t.MountAgent;
            try
            {
                m.GetNearbyAgents(tp, NearR, Buf);
                for (int i = 0; i < Buf.Count; i++)
                {
                    var x = Buf[i];
                    if (x == null || !x.IsActive() || ReferenceEquals(x, t) || ReferenceEquals(x, tr) || ReferenceEquals(x, tmnt)) continue;
                    if (ReferenceEquals(x, s) || ReferenceEquals(x, s.MountAgent)) continue;
                    var o = x.IsMount ? x.RiderAgent : x;   // kon swojego liczy sie jak jezdziec (FriendlyFire.OwnSide)
                    if (o == null || !o.IsHuman || ReferenceEquals(o, s) || o.Team == null || o.Team.Side != side) continue;
                    Vec2 ov = x.Position.AsVec2 - from;
                    float u = Vec2.DotProduct(ov, dir);
                    if (u < 0.5f * d || u > d + NearR) continue;
                    float lat = Math.Abs(ov.x * dir.y - ov.y * dir.x);
                    if (lat <= allow) return true;
                }
            }
            finally { Buf.Clear(); }
            return false;
        }

        /// <summary>2) Plaski tor (cel blizej niz 40 m): 1 = czysta, 0 = swoj na linii, -1 = bez promienia (budzet klatki). Wzor 283 Line.</summary>
        private static int Line(Mission m, Agent s, Agent t)
        {
            if (_frameQ >= QueriesPerFrame) return -1;
            _frameQ++;
            _rays++;
            Vec3 eye = s.GetEyeGlobalPosition();
            Vec3 tgt = t.IsMount ? t.Position + new Vec3(0f, 0f, 1.2f) : t.GetChestGlobalPosition();
            float d = eye.Distance(tgt);
            float err = 0f, rad = 0f;
            try { err = s.AgentDrivenProperties.AiShooterError; } catch { err = 0f; }
            try { rad = s.Monster != null ? s.Monster.BodyCapsuleRadius : 0f; } catch { rad = 0f; }
            float cd;
            Agent hit = m.RayCastForClosestAgent(eye, tgt, s.Index, rad + d * Math.Max(0f, err), out cd);
            if (hit == null || ReferenceEquals(hit, t) || ReferenceEquals(hit, t.MountAgent) || ReferenceEquals(hit, s.MountAgent)) return 1;
            Agent hh = hit.IsMount ? hit.RiderAgent : hit;
            if (hh == null || ReferenceEquals(hh, t) || cd >= d - 0.5f) return 1;
            var ht = hh.Team;
            return ht != null && ht.Side == s.Team.Side ? 0 : 1;
        }

        private static bool CleanTarget(Mission m, Agent s, Agent c)
        {
            if (_frameQ >= QueriesPerFrame) return false;
            Vec2 from = s.Position.AsVec2;
            float d = from.Distance(c.Position.AsVec2);
            if (Dirty(m, s, c, from, d)) return false;
            if (d < FlatRange) return Line(m, s, c) == 1;
            return true;
        }

        /// <summary>Kandydaci na czysty cel (najwyzej CandMax testow): tylne szeregi szyku celu (0), szyk strzelcow wroga (1), inne szyki (2).</summary>
        private static Agent Pick(Mission m, Agent s, Agent t, float d, out byte kind)
        {
            kind = 0;
            Cand.Clear(); CandKind.Clear();
            float range = Math.Max(1.2f * d, d + 10f);
            float mr = 0f;
            try { mr = s.GetMissileRange(); } catch { mr = 0f; }
            if (mr > 1f && range > mr) range = mr;
            Vec2 sp = s.Position.AsVec2, tp = t.Position.AsVec2;
            Agent tr = t.IsMount ? t.RiderAgent : t;
            var tf = tr != null ? tr.Formation : null;
            var team = tr != null ? tr.Team : null;
            // 0: tyl szyku celu - dwa najlepsze (szereg dalej od zwarcia)
            if (tf != null && tf.CountOfUnits > 1)
            {
                var ae = tf.ArrangementOrder.OrderEnum;
                bool byRank = ae != ArrangementOrder.ArrangementOrderEnum.Loose && ae != ArrangementOrder.ArrangementOrderEnum.Scatter;
                Units.Clear();
                tf.ApplyActionOnEachUnit(Collect);
                int n = Units.Count, step = n > UnitScan ? n / UnitScan + 1 : 1;
                Agent b1 = null, b2 = null;
                float k1 = float.MinValue, k2 = float.MinValue;
                for (int i = (_scan++ & 0x7fffffff) % step; i < n; i += step)
                {
                    var a = Units[i];
                    if (a == null || ReferenceEquals(a, tr) || !a.IsActive() || !a.IsHuman || a.IsRunningAway) continue;
                    Vec2 ap = a.Position.AsVec2;
                    float da = ap.Distance(sp);
                    if (da > range || da < 3f) continue;
                    float key = byRank ? ((IFormationUnit)a).FormationRankIndex * 100f + ap.Distance(tp) : ap.Distance(tp);
                    if (key > k1) { b2 = b1; k2 = k1; b1 = a; k1 = key; }
                    else if (key > k2) { b2 = a; k2 = key; }
                }
                Units.Clear();
                if (b1 != null) { Cand.Add(b1); CandKind.Add(0); }
                if (b2 != null) { Cand.Add(b2); CandKind.Add(0); }
            }
            // 1 / 2: inne szyki strony celu - strzelcy najpierw, po jednym (mediana)
            if (team != null)
            {
                var fl = team.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int pass = 0; pass < 2 && Cand.Count < CandMax; pass++)
                {
                    for (int j = 0; j < nf && Cand.Count < CandMax; j++)
                    {
                        var f = fl[j];
                        if (f == null || ReferenceEquals(f, tf) || f.CountOfUnits <= 0) continue;
                        var q = f.QuerySystem;
                        bool rng = q != null && q.IsRangedFormation;
                        if ((pass == 0) != rng) continue;
                        if (f.CachedAveragePosition.Distance(sp) > range + 20f) continue;
                        Agent a = null;
                        try { a = f.GetMedianAgent(true, true, f.CachedAveragePosition); } catch { a = null; }
                        if (a == null || !a.IsActive() || !a.IsHuman || a.IsRunningAway) continue;
                        float da = a.Position.AsVec2.Distance(sp);
                        if (da > range || da < 3f) continue;
                        Cand.Add(a); CandKind.Add((byte)(pass == 0 ? 1 : 2));
                    }
                }
            }
            for (int i = 0; i < Cand.Count && i < CandMax; i++)
            {
                if (_frameQ >= QueriesPerFrame) { _over++; break; }
                if (CleanTarget(m, s, Cand[i])) { kind = CandKind[i]; var c = Cand[i]; Cand.Clear(); CandKind.Clear(); return c; }
            }
            Cand.Clear(); CandKind.Clear();
            return null;
        }

        // ------------------------------------------------------------ przeglad (ShieldsUp.Tick, glowny watek)

        internal static void Tick(Mission m, float dt)
        {
            if (!Mine(m)) return;
            _now = m.CurrentTime;
            if (L.Count == 0) { _acc = 0f; return; }
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                _acc += dt;
                bool pass = _acc >= ReviewEvery;
                if (pass) _acc = 0f;
                if (pass || _anyDue)
                {
                    _anyDue = false;
                    int n = L.Count, done = 0;
                    int start = n > 0 ? _rot % n : 0;
                    for (int k = 0; k < n; k++)
                    {
                        var e = L[(start + k) % n];
                        if (e.Due) Review(e);
                        else if (pass && done < ReviewPer) { done++; Review(e); }
                    }
                    if (pass) _rot = start + done;
                    for (int i = L.Count - 1; i >= 0; i--)
                    {
                        var e = L[i];
                        if (e.Owned || e.Held) continue;
                        if (e.A != null) { Own cur; if (By.TryGetValue(e.A, out cur) && ReferenceEquals(cur, e)) By.Remove(e.A); }
                        L.RemoveAt(i);
                    }
                }
            }
            catch (Exception e) { Stumble("CleanShot.Przeglad", e); }
            long dtk = Stopwatch.GetTimestamp() - t0;
            _sumTicks += dtk;
            if (dtk > _maxTicks) _maxTicks = dtk;
            FrameCost309.AddTicks(FrameCost309.MClean, dtk);
        }

        private static void Review(Own e)
        {
            e.Due = false;
            var a = e.A;
            try
            {
                if (a == null || !a.IsActive()) { e.Owned = false; EndHold(e, false); return; }
                if (HorsePursuit.HuntAnyThread(a) || HorsePursuit.RidingAnyThread(a)) { _relTaken++; e.Owned = false; EndHold(e, true); return; }   // cel jego 204/283/148
                if (HorsePursuit.Banner(a)) { _relBanner++; e.Owned = false; EndHold(e, true); return; }                                       // RBM steruje celem
                if (a.IsRunningAway || SurvivalMode.InPanic(a))
                {
                    _relPanic++;
                    EndHold(e, true);
                    if (e.Owned) Release(e, true);
                    return;
                }
                if (e.Held && _now >= e.HoldUntil) EndHold(e, true);
                if (e.Owned)
                {
                    var t = e.T;
                    if (t == null || !t.IsActive()) { _relDead++; Release(e, true); }
                    else if (_now >= e.OwnUntil) { _relTime++; Release(e, true); }
                }
            }
            catch (Exception ex) { Stumble("CleanShot.Wpis", ex); e.Owned = false; e.Held = false; }
        }

        /// <summary>Automat celu gry z powrotem (wzor HorsePursuit.cs:311/966).</summary>
        private static void Release(Own e, bool count)
        {
            e.Owned = false;
            e.T = null;
            var a = e.A;
            if (a == null || !a.IsActive()) return;
            try { a.SetAutomaticTargetSelection(true); a.InvalidateTargetAgent(); }
            catch (Exception ex) { Stumble("CleanShot.Zwolnienie", ex); }
        }

        /// <summary>Koniec wstrzymania: rozkaz ognia szyku wraca (apply = false: czlowiek padl - bez wywolan silnika).</summary>
        private static void EndHold(Own e, bool apply)
        {
            if (!e.Held) return;
            e.Held = false;
            float dur = _now - e.HoldStart;
            if (dur < 0f) dur = 0f;
            _holdSecs += dur; _holdN++;
            if (dur > _holdMax) _holdMax = dur;
            var a = e.A;
            if (a != null)
            {
                float s;
                HoldSum.TryGetValue(a.Index, out s);
                HoldSum[a.Index] = s + dur;
            }
            if (!apply || a == null || !a.IsActive()) return;
            try
            {
                var f = a.Formation;
                a.SetFiringOrder(f != null ? f.FiringOrder.OrderEnum : FiringOrder.RangedWeaponUsageOrderEnum.FireAtWill);
            }
            catch (Exception ex) { Stumble("CleanShot.Ogien", ex); }
        }

        /// <summary>HorsePursuit.StartHunter (glowny watek): 204/283 bierze czlowieka - nasz wpis zdjety, wstrzymanie zdjete, celu nie ruszamy (ustawia go 204).</summary>
        internal static void Yield(Agent a)
        {
            if (!_live || a == null) return;
            try
            {
                Own e;
                if (!By.TryGetValue(a, out e)) return;
                _relTaken++;
                e.Owned = false;
                e.T = null;
                EndHold(e, true);
                e.Due = false;
            }
            catch (Exception ex) { Stumble("CleanShot.Yield", ex); }
        }

        /// <summary>BattleSense.OnAgentRemoved (glowny watek): strzelec padl - wpis bez wywolan silnika; cel padl - zwolnienie w najblizszym Tick.</summary>
        internal static void OnRemoved(Mission m, Agent affected)
        {
            if (!Mine(m) || affected == null || L.Count == 0) return;
            try
            {
                for (int i = 0; i < L.Count; i++)
                {
                    var e = L[i];
                    if (ReferenceEquals(e.A, affected)) { e.Owned = false; e.T = null; EndHold(e, false); continue; }
                    if (e.Owned && ReferenceEquals(e.T, affected)) { e.Due = true; _anyDue = true; }
                }
            }
            catch (Exception ex) { Stumble("CleanShot.OnRemoved", ex); }
        }

        // ------------------------------------------------------------ koniec misji

        /// <summary>BattleSense.Clean (po ShieldsUp.End): wszystkim nasz cel i wstrzymanie zdjete, linia raz na bitwe.</summary>
        internal static void End(Mission m)
        {
            var w = _m;
            if (w == null) return;
            if (m != null && !ReferenceEquals(w.Target, m)) return;
            bool live = _live;
            _live = false;
            try
            {
                for (int i = 0; i < L.Count; i++)
                {
                    var e = L[i];
                    if (e.Owned || e.Held) _relEnd++;
                    EndHold(e, true);
                    if (e.Owned) Release(e, true);
                }
            }
            catch (Exception ex) { Stumble("CleanShot.End", ex); }
            if (live && !_written)
            {
                _written = true;
                try { Write(); } catch (Exception ex) { Stumble("CleanShot.Write", ex); }
            }
            Clear();
            _m = null;
        }

        private static string B4(int[] a) { return a[0] + "/" + a[1] + "/" + a[2] + "/" + a[3]; }

        private static double Ms(long t) { return t * 1000.0 / Stopwatch.Frequency; }

        private static void Write()
        {
            int over20 = 0;
            foreach (var kv in HoldSum) if (kv.Value > 20f) over20++;
            var sb = new StringBuilder(1400);
            sb.Append("Czysty strzal (310): misja ").Append(_kind).Append(", klucz ").Append(_key ? "TAK" : "NIE (tylko pomiar, bez reakcji)")
              .Append(" (swoj przy celu w pasie rozrzutu w ").Append(NearR.ToString("0.0", Inv)).Append(" m, plaski tor ponizej ").Append(FlatRange.ToString("0", Inv))
              .Append(" m - promien; nowy cel do ").Append(CandMax).Append(" kandydatow, wlasnosc ").Append(OwnFor.ToString("0", Inv)).Append(" s, wstrzymanie ")
              .Append(HoldFor.ToString("0", Inv)).Append(" s)")
              .Append(" | strzalow AI w kolejce ").Append(_seen).Append(", kontroli ").Append(_checks).Append(", czyste ").Append(_clean)
              .Append(", brudne: klebowisko ").Append(_dirty).Append(" (<10/10-39/40-99/100+ m ").Append(B4(DirtyBy)).Append("), linia zajeta ").Append(_line)
              .Append(" (").Append(B4(LineBy)).Append("), nasz cel znow brudny ").Append(_reDirty)
              .Append(" | reakcja: nowy cel ").Append(_newRear + _newRanged + _newOther).Append(" (tyl szyku ").Append(_newRear).Append(", strzelcy ").Append(_newRanged)
              .Append(", inny szyk ").Append(_newOther).Append("), bez czystego celu ").Append(_noCand).Append(" (wstrzyman ").Append(_held)
              .Append(", szyk piechoty bez wstrzymania ").Append(_heldInf).Append("), wstrzymanie sr. ").Append(_holdN > 0 ? (_holdSecs / _holdN).ToString("0.0", Inv) : "-")
              .Append(" s, najdluzsze ").Append(_holdMax.ToString("0.0", Inv)).Append(" s, ludzi wstrzymanych ponad 20 s lacznie ").Append(over20)
              .Append(", strzalow mimo wstrzymania ").Append(_shotHeld)
              .Append(" | zwolnienia: cel padl ").Append(_relDead).Append(", czas ").Append(_relTime).Append(", panika ").Append(_relPanic)
              .Append(", przejete przez 204/283/148 ").Append(_relTaken).Append(", chorazy ").Append(_relBanner).Append(", koniec misji ").Append(_relEnd)
              .Append(" | pominiete: gracz ").Append(_skPlayer).Append(", bron rzucana ").Append(_skThrown).Append(", bez celu ").Append(_skNoTarget)
              .Append(", 204/283/148 ").Append(_skHunt).Append(", chorazy ").Append(_skBanner).Append(", panika / odwrot ").Append(_skPanic)
              .Append(", poza szykiem ").Append(_skDetached).Append(", machina ").Append(_skObject).Append(", ponad budzet ").Append(_over)
              .Append(" | zapytan ").Append(_queries).Append(", promieni ").Append(_rays).Append(" | koszt ").Append(Ms(_sumTicks).ToString("0.0", Inv))
              .Append(" ms razem, najwiecej ").Append(Ms(_maxTicks).ToString("0.000", Inv)).Append(" ms na klatke | potkniecia ").Append(_stum).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
