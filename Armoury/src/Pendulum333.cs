using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 333 (B) "Szyk stoi albo idzie - bez wahadla przed kontaktem" (06.11, Jeff po bitwie: "formacje dziwnie sie zachowuja, ida do przodu i do tylu, do przodu i do tylu
    /// zamiast stac w formacji; dopiero jak wrog jest blizej, jakby wychodza z trybu ping-pong i zaczynaja sie normalnie zachowywac"). Klucz FormationsSteadyBeforeContact.
    /// Przyczyna (rozpoznanie 333, dekompilacja dek333): przed zwarciem RBM (Utilities.HasBattleBeenJoined - 75 m, mediany + pol glebokosci obu szykow) szyki ida
    /// zachowaniami "fazy podejscia", a kazde liczy rozkaz Move od nowa co 0.5 s (FormationAI.TickOccasionally -> BehaviorComponent.TickOccasionally ->
    /// Formation.SetMovementOrder) bez histerezy kierunku:
    ///  - BehaviorScreenedSkirmish (lucznicy, waga 1 w taktykach RBM): postfiks RBM CalculateCurrentOrder stawia punkt = mediana glownej piechoty - Direction piechoty x
    ///    (pol glebokosci obu + 5 m), prefiks TickOccasionally co tick SetMovementOrder - lucznicy chodza za kazdym krokiem i obrotem piechoty (wahadlo piechoty x 1, obrot
    ///    Direction przerzuca punkt przod / tyl);
    ///  - BehaviorHoldHighGround (glowna piechota obroncy): vanilla - wzgorze HighGroundCloseToForeseenBattleGround (przesuwa sie z przewidywanym polem bitwy), postfiks RBM -
    ///    wrog w 200 m: punkt z pamieci positionsStorage (zapisany, gdy wrog byl dalej: srednia szyku + Direction x 10 m), wrog dalej: znowu punkt vanilla; przejscie przez
    ///    200 m w obie strony = skok punktu tam i z powrotem;
    ///  - BehaviorCautiousAdvance / Advance / Regroup - kroki RBM +5 / +10 / -10 m i medianowe punkty (opis 333).
    /// Pozycja szyku idzie tylko przez rozkaz ruchu: Formation.Tick bierze _movementOrder.CreateNewOrderWorldPositionMT (Move = staly punkt _position) -> SetPositioning;
    /// RBM SetPositioningPatch zmienia tylko odstep (unitSpacing), a Stop nie ma punktu (szyk zostaje na ostatnim). Jedna droga = prefiks Formation.SetMovementOrder.
    ///
    /// ZASADA (333b, jedna na wszystkie zachowania, bez dotykania wag RBM): prefiks Formation.SetMovementOrder (Priority.Last, po prefiksie com.rbmai), tylko rozkaz Move,
    /// tylko szyk AI (nie szyk gracza - HorsePursuit.PlayerForm), pieszy (piechota albo lucznicy), od IntactMin ludzi, bitwa polowa po rozstawieniu, glowny watek,
    /// i tylko PRZED KONTAKTEM (najblizszy pieszy szyk wroga - bez pieszego dowolny - dalej niz 75 m miara RBM; wyjscie z kontaktu > 90 m). Szyk ma PUNKT PRZYJETY P
    /// (ostatni przepuszczony rozkaz). Nowy punkt T:
    ///  1. pierwszy rozkaz przed kontaktem - przyjety (powrot obroncy na wzgorze, lucznicy za piechote, pierwszy marsz - w dowolna strone);
    ///  2. MARTWA STREFA: |T - P| < max(12 m, glebokosc szyku) - zostaje P (kroki RBM +5 / +10 / -10 m, drganie mediany piechoty pod punktem lucznikow S4);
    ///  3. ZAPADKA KIERUNKU: skladowa (T - P) na osi do wroga przeciwna do ostatniego przyjetego ruchu naprzod / wstecz (ponad pol martwej strefy) - zostaje P, dopoki od
    ///     tamtego ruchu nie minie okno 30 s; kazdy przepuszczony nawrot podwaja okno tego szyku (30 / 60 / 120 / 240 s) - wahadlo z konstrukcji gasnie;
    ///  4. TRZYMANIE: szyk stoi na P (mediana w martwej strefie od P) - nowy punkt najwczesniej 10 s po przyjeciu P (bez dreptania co 0.5 s);
    ///  5. reszta (ruch w tym samym kierunku albo w bok, szyk jeszcze w drodze) - przyjety.
    /// "Zostaje P" = rozkaz zamieniony na Move w punkt P (ta sama WorldPosition - gra uznaje rozkaz za praktycznie ten sam i nie przestawia szyku).
    /// KONTAKT (recenzja 333 W1): takze dowolny szyk wroga (konnica) w 75 m miara RBM albo konnica wroga w 60 m, albo w 120 m i zbliza sie >= 3 m/s (szarza) -
    /// wtedy przez 4 s od ostatniego takiego pomiaru wszystko jak w grze (lucznicy uciekaja przed szarza, piechota sie ustawia); P i zapadka zapomniane.
    /// Szyki druzyny gracza-wodza (takze oddane AI przez F6) - poza 333. Odwrot (Retreat), szarza, Advance / FallBack, Stop - bez zmian.
    /// POMIAR (linia, takze przy kluczu NIE - do porownania): co 0.5 s kazdy szyk AI jak wyzej przed kontaktem - przesuniecie mediany rzutowane na os do wroga,
    /// filtr zygzak z progiem max(3 m, pol glebokosci): nawrot = zmiana kierunku po odcinku >= progu; wynik na szyko-minute przed kontaktem i wedlug zachowania.
    /// Koszt: prefiks raz na rozkaz (co 0.5 s na szyk) - petla po szykach wroga (kilkanascie), pomiar co 0.5 s po szykach; bez zapytan o ludzi.
    /// Potkniecia licza sie (Stumbles), bez wylacznika.
    /// </summary>
    internal static class Pendulum333
    {
        internal const float ContactIn = 75f, ContactOut = 90f, SampleEvery = 0.5f, DeadMin = 3f, RefreshEvery = 0.5f;
        internal const float CavClose = 60f, CavNear = 120f, CavClosing = 3f, CavKeep = 4f;
        // 333b: martwa strefa rozkazu, zapadka kierunku (okno podwajane po kazdym nawrocie), trzymanie punktu
        internal const float DeadMove = 12f, Ratchet = 30f, RatchetMax = 240f, HoldMin = 10f;
        private const int MaxBeh = 8;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class PF
        {
            public Formation F;
            public bool Contact, FootContact;
            public float CavGap = float.MaxValue, CavAt = -1000f, CavUntil = -1000f;
            public float Gap = float.MaxValue, GapAt = -1000f;
            public Vec2 Axis = Vec2.Invalid;
            // 333b: punkt przyjety, zapadka
            public Vec2 P = Vec2.Invalid;
            public WorldPosition PW;
            public float PAt = -1000f, DirAt = -1000f;
            public sbyte Dir;
            public int Revs;
            // pomiar
            public Vec2 Last = Vec2.Invalid;
            public float X, Lo, Hi, Ext;
            public sbyte Trend;
        }

        private static bool _live, _key, _tried;
        private static int _mainThread = -1;
        private static WeakReference _m;
        private static float _acc;
        private static readonly Dictionary<Formation, PF> Forms = new Dictionary<Formation, PF>();
        private static readonly Dictionary<string, int> RevBy = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<Formation> HeldForms = new HashSet<Formation>();
        // liczniki misji (nazwy segmentu jak w 333: wstecz zatrzymane = zapadka na ruchu wstecz, wstecz dozwolone S4 / po zmianie zachowania = przyjete ruchy wstecz
        // lucznikow S4 / innych, naprzod wstrzymane po cofnieciu = zapadka na ruchu naprzod)
        private static int _seen, _backHeld, _backS4, _backBeh, _fwdHeld, _rev, _revBack, _forms, _contactIn, _contactCav;
        private static int _first, _dead, _hold, _revOk, _accFwd, _accSide;
        private static float _preSec;
        private static int _stum, _stumPatch;   // recenzja 333 (7): potkniecie latki (przed pierwszym Begin) nie ginie w Clear
        internal static int Stumbles { get { return _stum + _stumPatch; } }
        private static string _patch = "nie wpieta";

        // ------------------------------------------------------------ latka (Doctrine.ApplyMissionPatches - pierwsza bitwa, raz na proces)

        internal static string ApplyPatch(Harmony h)
        {
            if (_tried) return _patch;
            _tried = true;
            try
            {
                var target = AccessTools.DeclaredMethod(typeof(Formation), nameof(Formation.SetMovementOrder), new[] { typeof(MovementOrder) });
                if (target == null) { _patch = "NIE (brak Formation.SetMovementOrder)"; return _patch; }
                h.Patch(target, prefix: new HarmonyMethod(typeof(Pendulum333), nameof(Pre)) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                _patch = "TAK";
            }
            catch (Exception e)
            {
                _patch = "NIE (" + e.GetType().Name + ")";
                _stumPatch++;
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Pendulum333.Latka", e);
            }
            return _patch;
        }

        // ------------------------------------------------------------ misja (HorsePursuit.Begin / Tick / End - glowny watek)

        internal static void Begin(Mission m, bool field)
        {
            Clear();
            _live = field && m != null;
            _m = m != null ? new WeakReference(m) : null;
            _mainThread = Thread.CurrentThread.ManagedThreadId;
            var c = Settings.Current;
            _key = c != null && c.FormationsSteadyBeforeContact;
        }

        internal static void End()
        {
            _live = false;
            Clear();
            _m = null;
        }

        private static void Clear()
        {
            Forms.Clear(); RevBy.Clear(); HeldForms.Clear();
            _acc = 0f;
            _seen = _backHeld = _backS4 = _backBeh = _fwdHeld = _rev = _revBack = _forms = _contactIn = _contactCav = 0;
            _first = _dead = _hold = _revOk = _accFwd = _accSide = 0;
            _preSec = 0f;
            _stum = 0;
        }

        private static bool Here(Mission m)
        {
            var w = _m;
            return _live && m != null && w != null && ReferenceEquals(w.Target, m);
        }

        /// <summary>Szyk, ktorego to dotyczy: AI, nie gracza, pieszy, od IntactMin ludzi, poza rozstawieniem.</summary>
        private static bool Eligible(Formation f)
        {
            if (f == null || f.CountOfUnits < HorsePursuit.IntactMin || !f.IsAIControlled || f.IsDeployment) return false;
            if (!MissionTeams.Valid(f.Team) || HorsePursuit.PlayerForm(f)) return false;
            var q = f.QuerySystem;
            if (q == null || q.IsCavalryFormation || q.IsRangedCavalryFormation) return false;
            return q.IsInfantryFormation || q.IsRangedFormation;
        }

        private static PF Get(Formation f)
        {
            PF pf;
            if (!Forms.TryGetValue(f, out pf)) { pf = new PF { F = f }; Forms[f] = pf; _forms++; }
            return pf;
        }

        /// <summary>Odleglosc miara RBM (mediany + pol glebokosci obu) do najblizszego pieszego szyku wroga od IntactMin ludzi (bez pieszego - jakiegokolwiek), os do niego,
        /// stan kontaktu z histereza. Najwyzej co RefreshEvery s.</summary>
        private static void Refresh(PF pf, float now)
        {
            if (now - pf.GapAt < RefreshEvery) return;
            pf.GapAt = now;
            var f = pf.F;
            var own = f.Team;
            var m = Mission.Current;
            Vec2 c = f.CachedMedianPosition.AsVec2;
            float best = float.MaxValue, bestAny = float.MaxValue, bestCav = float.MaxValue;
            Vec2 ax = Vec2.Invalid, axAny = Vec2.Invalid;
            var teams = m != null ? m.Teams : null;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team t;
                try { t = teams[i]; } catch { continue; }
                if (!MissionTeams.Valid(t) || own == null || !t.IsEnemyOf(own)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var e = fl[j];
                    if (e == null || e.CountOfUnits < HorsePursuit.IntactMin) continue;
                    var eq = e.QuerySystem;
                    if (eq == null) continue;
                    Vec2 ec = e.CachedMedianPosition.AsVec2;
                    if (!ec.IsValid) continue;
                    float d = c.Distance(ec) + 0.5f * (f.Depth + e.Depth);
                    bool foot = !eq.IsCavalryFormation && !eq.IsRangedCavalryFormation;
                    if (foot && d < best) { best = d; ax = ec - c; }
                    if (d < bestAny) { bestAny = d; axAny = ec - c; }
                    if (!foot && d < bestCav) bestCav = d;
                }
            }
            float near = bestAny;   // kontakt: pieszy wrog albo dowolny szyk (konnica) miara RBM - recenzja 333 W1
            if (best == float.MaxValue) { best = bestAny; ax = axAny; }
            pf.Gap = best;
            if (ax.IsValid && ax.Length > 0.1f) pf.Axis = ax.Normalized();
            bool was = pf.Contact;
            if (near <= ContactIn) pf.FootContact = true;
            else if (near > ContactOut) pf.FootContact = false;
            // konnica wroga: bardzo blisko albo w 120 m i szarzuje (zbliza sie >= 3 m/s) - kontakt na CavKeep s od ostatniego takiego pomiaru
            bool cav = false;
            if (bestCav <= CavClose) cav = true;
            else if (bestCav <= CavNear && pf.CavGap != float.MaxValue && now - pf.CavAt > 0.05f && now - pf.CavAt < 3f
                     && (pf.CavGap - bestCav) / (now - pf.CavAt) >= CavClosing) cav = true;
            pf.CavGap = bestCav;
            pf.CavAt = now;
            if (cav) pf.CavUntil = now + CavKeep;
            pf.Contact = pf.FootContact || now < pf.CavUntil;
            if (pf.Contact && !was) { _contactIn++; if (!pf.FootContact) _contactCav++; }
        }

        // ------------------------------------------------------------ prefiks Formation.SetMovementOrder

        private static void Pre(Formation __instance, ref MovementOrder input)
        {
            if (!_live || !_key) return;
            try
            {
                if (input.OrderEnum != MovementOrder.MovementOrderEnum.Move) return;
                if (Thread.CurrentThread.ManagedThreadId != _mainThread) return;
                var m = Mission.Current;
                if (!Here(m) || !m.IsDeploymentFinished) return;
                var f = __instance;
                if (!Eligible(f)) return;
                float now = m.CurrentTime;
                var pf = Get(f);
                Refresh(pf, now);
                if (pf.Contact || pf.Gap == float.MaxValue || !pf.Axis.IsValid) { Forget(pf); return; }
                Vec2 c = f.CachedMedianPosition.AsVec2;
                WorldPosition tw = input.CreateNewOrderWorldPositionMT(f, WorldPosition.WorldPositionEnforcedCache.None);
                Vec2 tp = tw.AsVec2;
                if (!c.IsValid || !tw.IsValid || !tp.IsValid) return;
                _seen++;
                float dz = Math.Max(DeadMove, f.Depth);
                Type beh = null;
                try { var ai = f.AI; beh = ai != null && ai.ActiveBehavior != null ? ai.ActiveBehavior.GetType() : null; } catch { beh = null; }
                // 1. pierwszy rozkaz przed kontaktem - przyjety (kierunek wzgledem mediany szyku)
                if (!pf.P.IsValid)
                {
                    _first++;
                    Accept(pf, tw, tp, Sign((tp - c).DotProduct(pf.Axis), dz), now, beh);
                    return;
                }
                // 2. martwa strefa
                if (tp.Distance(pf.P) < dz) { Keep(f, pf, ref input); _dead++; return; }
                sbyte sg = Sign((tp - pf.P).DotProduct(pf.Axis), dz);
                // 3. zapadka kierunku (okno 30 s, podwajane po kazdym przepuszczonym nawrocie)
                if (sg != 0 && pf.Dir != 0 && sg != pf.Dir)
                {
                    float win = Math.Min(RatchetMax, Ratchet * (float)Math.Pow(2.0, Math.Min(pf.Revs, 3)));
                    if (now - pf.DirAt < win)
                    {
                        Keep(f, pf, ref input);
                        if (sg < 0) _backHeld++; else _fwdHeld++;
                        return;
                    }
                    pf.Revs++;
                    _revOk++;
                }
                // 4. trzymanie: szyk stoi na P - nowy punkt najwczesniej HoldMin s po przyjeciu P
                else if (c.Distance(pf.P) < dz && now - pf.PAt < HoldMin)
                {
                    Keep(f, pf, ref input);
                    _hold++;
                    return;
                }
                // 5. przyjety
                Accept(pf, tw, tp, sg, now, beh);
            }
            catch (Exception e)
            {
                _stum++;
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Pendulum333.Rozkaz", e);
            }
        }

        private static sbyte Sign(float s, float dz)
        {
            return s > 0.5f * dz ? (sbyte)1 : s < -0.5f * dz ? (sbyte)-1 : (sbyte)0;
        }

        private static void Accept(PF pf, WorldPosition tw, Vec2 tp, sbyte sg, float now, Type beh)
        {
            pf.P = tp;
            pf.PW = tw;
            pf.PAt = now;
            if (sg != 0) { pf.Dir = sg; pf.DirAt = now; }
            if (sg > 0) _accFwd++;
            else if (sg < 0) { if (beh == typeof(BehaviorScreenedSkirmish)) _backS4++; else _backBeh++; }
            else _accSide++;
        }

        /// <summary>Kontakt: punkt przyjety i zapadka zapomniane (po wyjsciu z kontaktu - od nowa; licznik nawrotow okna zostaje).</summary>
        private static void Forget(PF pf)
        {
            pf.P = Vec2.Invalid;
            pf.Dir = 0;
            pf.DirAt = -1000f;
        }

        /// <summary>Rozkaz zamieniony na Move w punkt przyjety P (ta sama WorldPosition).</summary>
        private static void Keep(Formation f, PF pf, ref MovementOrder input)
        {
            input = MovementOrder.MovementOrderMove(pf.PW);
            HeldForms.Add(f);
        }

        // ------------------------------------------------------------ pomiar wahadla (co 0.5 s)

        internal static void Tick(Mission m, float now, float dt)
        {
            if (!Here(m)) return;
            _acc += dt;
            if (_acc < SampleEvery) return;
            float step = _acc;
            _acc = 0f;
            try
            {
                if (!m.IsDeploymentFinished) return;
                var teams = m.Teams;
                int nt = teams != null ? teams.Count : 0;
                for (int i = 0; i < nt; i++)
                {
                    Team t;
                    try { t = teams[i]; } catch { continue; }
                    if (!MissionTeams.Valid(t)) continue;
                    var fl = t.FormationsIncludingSpecialAndEmpty;
                    int nf = fl != null ? fl.Count : 0;
                    for (int j = 0; j < nf; j++)
                    {
                        var f = fl[j];
                        if (!Eligible(f)) continue;
                        Sample(Get(f), now, step);
                    }
                }
            }
            catch (Exception e)
            {
                _stum++;
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Pendulum333.Pomiar", e);
            }
        }

        private static void Sample(PF pf, float now, float step)
        {
            Refresh(pf, now);
            var f = pf.F;
            Vec2 c = f.CachedMedianPosition.AsVec2;
            if (pf.Contact || pf.Gap == float.MaxValue || !pf.Axis.IsValid || !c.IsValid)
            {
                pf.Last = Vec2.Invalid;
                pf.Trend = 0;
                if (pf.Contact) Forget(pf);
                return;
            }
            _preSec += step;
            if (!pf.Last.IsValid)
            {
                pf.Last = c;
                pf.X = pf.Lo = pf.Hi = pf.Ext = 0f;
                pf.Trend = 0;
                return;
            }
            pf.X += (c - pf.Last).DotProduct(pf.Axis);
            pf.Last = c;
            float thr = Math.Max(DeadMin, 0.5f * f.Depth);
            float x = pf.X;
            if (pf.Trend == 0)
            {
                if (x < pf.Lo) pf.Lo = x;
                if (x > pf.Hi) pf.Hi = x;
                if (x - pf.Lo >= thr) { pf.Trend = 1; pf.Ext = x; }
                else if (pf.Hi - x >= thr) { pf.Trend = -1; pf.Ext = x; }
                return;
            }
            if (pf.Trend > 0)
            {
                if (x > pf.Ext) pf.Ext = x;
                else if (pf.Ext - x >= thr) { Reversal(f, true); pf.Trend = -1; pf.Ext = x; }
            }
            else
            {
                if (x < pf.Ext) pf.Ext = x;
                else if (x - pf.Ext >= thr) { Reversal(f, false); pf.Trend = 1; pf.Ext = x; }
            }
        }

        private static void Reversal(Formation f, bool back)
        {
            _rev++;
            if (back) _revBack++;
            string n = "?";
            try { var ai = f.AI; n = ai != null && ai.ActiveBehavior != null ? ai.ActiveBehavior.GetType().Name : "brak"; } catch { n = "?"; }
            int k;
            RevBy.TryGetValue(n, out k);
            if (k == 0 && RevBy.Count >= MaxBeh) n = "inne";
            RevBy.TryGetValue(n, out k);
            RevBy[n] = k + 1;
        }

        // ------------------------------------------------------------ segment linii "AI bitwy (333)"

        internal static void Append(StringBuilder sb)
        {
            float min = _preSec / 60f;
            sb.Append("wahadlo: klucz ").Append(_key ? "TAK" : "NIE").Append(" (latka ").Append(_patch).Append("; przed kontaktem > ").Append(ContactIn.ToString("0", Inv))
              .Append(" m miara RBM, wyjscie > ").Append(ContactOut.ToString("0", Inv)).Append(" m; 333b: punkt przyjety, martwa strefa rozkazu max(")
              .Append(DeadMove.ToString("0", Inv)).Append(" m, glebokosc), zapadka kierunku ").Append(Ratchet.ToString("0", Inv)).Append('/')
              .Append((2f * Ratchet).ToString("0", Inv)).Append('/').Append((4f * Ratchet).ToString("0", Inv)).Append('/').Append(RatchetMax.ToString("0", Inv))
              .Append(" s, trzymanie ").Append(HoldMin.ToString("0", Inv)).Append(" s; pomiar martwa strefa max(").Append(DeadMin.ToString("0", Inv))
              .Append(" m, pol glebokosci); konnica wroga ").Append(CavClose.ToString("0", Inv))
              .Append(" m albo szarza w ").Append(CavNear.ToString("0", Inv)).Append(" m = kontakt)")
              .Append(" | szykow AI ").Append(_forms).Append(", szyko-minut przed kontaktem ").Append(min.ToString("0.0", Inv))
              .Append(", nawroty ").Append(_rev).Append(" (w tym w tyl ").Append(_revBack).Append("; ").Append(min > 0.01f ? (_rev / min).ToString("0.00", Inv) : "-")
              .Append(" na szyko-minute; wedlug zachowania ");
            if (RevBy.Count == 0) sb.Append('-');
            else
            {
                bool first = true;
                foreach (var kv in RevBy)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(kv.Key).Append(' ').Append(kv.Value);
                }
            }
            sb.Append("), wejsc w kontakt ").Append(_contactIn).Append(" (w tym przez konnice wroga ").Append(_contactCav).Append(')')
              .Append(" | rozkazy Move przed kontaktem ").Append(_seen).Append(": wstecz zatrzymane ").Append(_backHeld).Append(", wstecz dozwolone S4 ").Append(_backS4)
              .Append(", wstecz po zmianie zachowania ").Append(_backBeh).Append(", naprzod wstrzymane po cofnieciu ").Append(_fwdHeld).Append(", szykow zatrzymanych ")
              .Append(HeldForms.Count)
              .Append("; 333b: pierwszych ").Append(_first).Append(", martwa strefa ").Append(_dead).Append(", trzymanie ").Append(_hold)
              .Append(", nawroty przepuszczone po oknie ").Append(_revOk).Append(", przyjete naprzod ").Append(_accFwd).Append(", w bok ").Append(_accSide);
        }
    }
}
