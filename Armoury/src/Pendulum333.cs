using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 333 (B) "Szyk stoi albo idzie - bez wahadla przed kontaktem" (06.11, Jeff po bitwie: "formacje dziwnie sie zachowuja, ida do przodu i do tylu, do przodu i do tylu
    /// zamiast stac w formacji; dopiero jak wrog jest blizej, jakby wychodza z trybu ping-pong i zaczynaja sie normalnie zachowywac"). Klucz FormationsSteadyBeforeContact.
    /// Przyczyna (rozpoznanie 333, dekompilacja): przed zwarciem RBM (Utilities.HasBattleBeenJoined - 75 m, mediany + pol glebokosci obu szykow) piechota idzie
    /// zachowaniami "fazy podejscia". BehaviorCautiousAdvance (piechota oslaniajaca lucznikow w RangedHarrassmentOffensive - nasza doktryna S1 daje jej Shot x2.5 i dodaje
    /// ja do listy) krazy miedzy stanami Approaching / Shooting / PullingBack wedlug tego, czy NASI lucznicy strzelaja (MakingRangedAttackRatio), a postfiks RBM dokleja
    /// do tego kroki +5 / +10 m naprzod i -10 m wstecz co 30-70 tickow; RBM BehaviorAdvance staje i rusza wedlug celu konnicy wroga, BehaviorRegroup (waga 1.5) stawia
    /// szyk na jego medianie. Wszystko to sa rozkazy Move liczone od nowa co 0.5 s (FormationAI.TickOccasionally) bez histerezy kierunku - stad "przod, tyl, przod, tyl".
    ///
    /// ZASADA (jedna na wszystkie zachowania, bez dotykania wag RBM): prefiks Formation.SetMovementOrder (Priority.Last, po prefiksie com.rbmai), tylko rozkaz Move,
    /// tylko szyk AI (nie szyk gracza - HorsePursuit.PlayerForm), pieszy (piechota albo lucznicy, nie konnica), od IntactMin ludzi, bitwa polowa po rozstawieniu,
    /// glowny watek, i tylko PRZED KONTAKTEM: najblizszy pieszy szyk wroga (od IntactMin ludzi; bez pieszego - jakikolwiek) dalej niz 75 m miara RBM (wejscie w kontakt
    /// <= 75 m, wyjscie > 90 m - histereza 15 m). Kierunek rozkazu = rzut (cel rozkazu - mediana szyku) na os do tego wroga; martwa strefa max(3 m, pol glebokosci szyku):
    ///  - NAPRZOD (> martwej strefy): przechodzi; po dozwolonym cofnieciu przez 15 s (Dwell) bez zmiany zachowania - stoj (bez "tyl-przod");
    ///  - STOJ (w martwej strefie): przechodzi;
    ///  - WSTECZ (< -martwej strefy): przed kontaktem zamieniony na "stoj w miejscu" (Move na kotwicy - mediana szyku z chwili pierwszego zatrzymania; ten sam punkt
    ///    przy kolejnych rozkazach, wiec gra nie przestawia szyku co tick). Dozwolone tylko: lucznicy za piechote (BehaviorScreenedSkirmish - nasza S4 z wlasna histereza
    ///    25 m) oraz realna zmiana sytuacji - inne zachowanie szyku niz przy ostatniej decyzji kierunku (zmiana taktyki druzyny, np. obronca wraca na wzgorze 291)
    ///    i co najmniej 15 s od tej decyzji (Dwell - bez wahadla taktyk). Odwrot (rozkaz Retreat), szarza, Advance / FallBack - bez zmian (to nie Move).
    /// Kotwica znika, gdy szyk ruszy naprzod albo wejdzie w kontakt. Po kontakcie - wszystko jak w grze i RBM.
    /// POMIAR (linia, takze przy kluczu NIE - do porownania): co 0.5 s kazdy szyk AI jak wyzej przed kontaktem - przesuniecie mediany rzutowane na os do wroga,
    /// filtr zygzak z progiem max(3 m, pol glebokosci): nawrot = zmiana kierunku po odcinku >= progu; wynik na szyko-minute przed kontaktem i wedlug zachowania.
    /// Koszt: prefiks raz na rozkaz (co 0.5 s na szyk) - petla po szykach wroga (kilkanascie), pomiar co 0.5 s po szykach; bez zapytan o ludzi.
    /// Potkniecia licza sie (Stumbles), bez wylacznika.
    /// </summary>
    internal static class Pendulum333
    {
        internal const float ContactIn = 75f, ContactOut = 90f, Dwell = 15f, SampleEvery = 0.5f, DeadMin = 3f, RefreshEvery = 0.5f;
        private const byte DNone = 0, DFwd = 1, DHold = 2, DBack = 3;
        private const int MaxBeh = 8;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class PF
        {
            public Formation F;
            public byte Dir;
            public float DirSince;
            public Type Beh;
            public Vec2 Anchor = Vec2.Invalid;
            public bool Contact;
            public float Gap = float.MaxValue, GapAt = -1000f;
            public Vec2 Axis = Vec2.Invalid;
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
        // liczniki misji
        private static int _seen, _backHeld, _backS4, _backBeh, _fwdHeld, _rev, _revBack, _forms, _contactIn;
        private static float _preSec;
        private static int _stum;
        internal static int Stumbles { get { return _stum; } }
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
                _stum++;
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
            _seen = _backHeld = _backS4 = _backBeh = _fwdHeld = _rev = _revBack = _forms = _contactIn = 0;
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
            float best = float.MaxValue, bestAny = float.MaxValue;
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
                }
            }
            if (best == float.MaxValue) { best = bestAny; ax = axAny; }
            pf.Gap = best;
            if (ax.IsValid && ax.Length > 0.1f) pf.Axis = ax.Normalized();
            bool was = pf.Contact;
            if (best <= ContactIn) pf.Contact = true;
            else if (best > ContactOut) pf.Contact = false;
            if (pf.Contact && !was) _contactIn++;
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
                if (pf.Contact || pf.Gap == float.MaxValue || !pf.Axis.IsValid) { pf.Dir = DNone; pf.Anchor = Vec2.Invalid; return; }
                Vec2 c = f.CachedMedianPosition.AsVec2;
                Vec2 tp = input.GetPosition(f);
                if (!c.IsValid || !tp.IsValid) return;
                _seen++;
                float s = (tp - c).DotProduct(pf.Axis);
                float dz = Math.Max(DeadMin, 0.5f * f.Depth);
                Type beh = null;
                try { var ai = f.AI; beh = ai != null && ai.ActiveBehavior != null ? ai.ActiveBehavior.GetType() : null; } catch { beh = null; }
                if (s < -dz)
                {
                    bool s4 = beh == typeof(BehaviorScreenedSkirmish);
                    bool changed = pf.Beh != null && beh != null && beh != pf.Beh && (pf.Dir == DNone || now - pf.DirSince >= Dwell);
                    if (s4 || changed)
                    {
                        if (s4) _backS4++; else _backBeh++;
                        pf.Dir = DBack; pf.DirSince = now; pf.Beh = beh; pf.Anchor = Vec2.Invalid;
                        return;
                    }
                    Hold(f, pf, c, now, ref input);
                    _backHeld++;
                    return;
                }
                if (s > dz)
                {
                    if (pf.Dir == DBack && now - pf.DirSince < Dwell && beh == pf.Beh)
                    {
                        Hold(f, pf, c, now, ref input);
                        _fwdHeld++;
                        return;
                    }
                    if (pf.Dir != DFwd) { pf.Dir = DFwd; pf.DirSince = now; }
                    pf.Beh = beh;
                    pf.Anchor = Vec2.Invalid;
                    return;
                }
                if (pf.Dir != DHold && pf.Dir != DBack) { pf.Dir = DHold; pf.DirSince = now; }
                pf.Beh = beh;   // zmiana zachowania = pierwszy rozkaz kierunku nowego zachowania
            }
            catch (Exception e)
            {
                _stum++;
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Pendulum333.Rozkaz", e);
            }
        }

        /// <summary>Rozkaz zamieniony na "stoj w miejscu": Move na kotwicy (mediana szyku z pierwszego zatrzymania).</summary>
        private static void Hold(Formation f, PF pf, Vec2 c, float now, ref MovementOrder input)
        {
            if (!pf.Anchor.IsValid) pf.Anchor = c;
            if (pf.Dir == DFwd || pf.Dir == DNone) { pf.Dir = DHold; pf.DirSince = now; }
            var wp = f.CachedMedianPosition;
            wp.SetVec2(pf.Anchor);
            input = MovementOrder.MovementOrderMove(wp);
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
              .Append(" m miara RBM, wyjscie > ").Append(ContactOut.ToString("0", Inv)).Append(" m; martwa strefa max(").Append(DeadMin.ToString("0", Inv))
              .Append(" m, pol glebokosci); wstecz tylko S4 albo zmiana zachowania po ").Append(Dwell.ToString("0", Inv)).Append(" s)")
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
            sb.Append("), wejsc w kontakt ").Append(_contactIn)
              .Append(" | rozkazy Move przed kontaktem ").Append(_seen).Append(": wstecz zatrzymane ").Append(_backHeld).Append(", wstecz dozwolone S4 ").Append(_backS4)
              .Append(", wstecz po zmianie zachowania ").Append(_backBeh).Append(", naprzod wstrzymane po cofnieciu ").Append(_fwdHeld).Append(", szykow zatrzymanych ")
              .Append(HeldForms.Count);
        }
    }
}
