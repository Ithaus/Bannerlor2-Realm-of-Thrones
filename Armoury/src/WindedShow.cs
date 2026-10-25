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
    /// 144 (134d, Jeff 13:20: "czy jest animacja chodu i zmeczenia??"): ZADYSZKE WIDAC I SLYCHAC - tylko u czlowieka AI z zadyszka (FieldCraft State.Winded,
    /// pusty pasek az do WindedExitBar) w 35 m od gracza (Agent.Main), w bitwie po rozstawieniu (AiBreath.BattleNow: pole, oblezenie, kryjowka; bez morza
    /// i pojedynkow). Animacji zadyszki dla wojownika nie ma w grze ani w modach (projekt 134, punkt 1.9) - kandydaci z krytyki Z2:
    ///  - twarz: SetAgentFacialAnimation(Low, "idle_tired", petla) na poczatku zadyszki, "idle_normal" po niej (obie w Native voices.xml; idle_tired wola w grze tylko
    ///    martwy MissionFacialAnimationHandler) - klucz WindedShowsIt;
    ///  - poza (klucz WindedPose 0-3, domyslnie 1): 1 act_conversation_weary_loop, 2 act_conversation_weary2_loop (as_human_warrior; kobiety dziedzicza,
    ///    giganci ROT maja kopie), 3 act_pickup_down_begin_left_stance (to samo, co RBM gra jako "tired" na kanale 0). Kanal 1 (gora ciala, wzor okrzyku gry),
    ///    ignorePriority false; tylko na postoju (&lt; 0.6 m/s), bez wroga w 8 m, bez pocisku w 8 s, bez luku / kuszy / rzutu w reku, poza panika, drabina,
    ///    machina i woda, przy wolnym kanale 0 i 1 (act_none albo Idle) i klipie istniejacym w zestawie czlowieka; 2.5-4 s, ponowienie co 8-14 s; przerwanie
    ///    act_none z priorytetem min(GetCurrentActionPriority(1), 73) (wzor gry, AgentVictoryLogic) przy ruchu, wrogu w 8 m, pocisku, panice, koncu zadyszki,
    ///    odejsciu poza 35 m; najwyzej 12 ludzi naraz, 3 starty na takt 0.2 s; odmowa SetActionChannel = licznik i bez ponawiania do konca zadyszki.
    ///    Bez podmiany zestawow (Artem przywraca as_human_warrior zestawom z "injured" - projekt 1.10);
    ///  - oddech (klucz WindedBreathSound): zdarzenie FMOD gry "voice/combat/(male 01/04/07/08 | female 02/04)/grunt_rowing" (3.5 s, 80 m) przez
    ///    Mission.MakeSound w pozycji glowy - jednorazowy dzwiek silnika bez uchwytu SoundEvent (nic do zwalniania; wariant (a) projektu; (c) BattleSound
    ///    wymaga wlasnych nagran, ktorych nie ma); tylko w 25 m od gracza, raz na poczatku zadyszki, potem co 6-9 s na postoju; wspolny limiter z okrzykami
    ///    i jekami (BattleCries.TryBreathVoice: glos na czlowieka co 4 s, 6 glosow na sekunde), do tego najwyzej 2 oddechy na sekunde w bitwie.
    /// Wszystko z taktu FieldCraft (co 0.2 s, glowny watek), nic co klatke. ActionIndexCache tworzone dopiero w misji (pulapka 105.4). Wyjatek liczony
    /// (RoutHold.Stumble, miejsce "oddech"); czlowiek z 5 potknieciami wypada. Liczniki ida do linii "Oddech AI (144)"; linia kandydatow raz na bitwe.
    /// </summary>
    internal static class WindedShow
    {
        internal const float Near = 35f, VoiceNear = 25f, StillBelow = 0.6f, EnemyNear = 8f, FireQuiet = 8f, EnemyEvery = 0.4f;
        internal const float PoseMin = 2.5f, PoseSpan = 1.5f, RepeatMin = 8f, RepeatSpan = 6f, BreathMin = 6f, BreathSpan = 3f, HeadUp = 1.5f;
        internal const int MaxPosing = 12, StartsPerTick = 3, QueriesPerTick = 16, BreathsPerSecond = 2, MaxFails = 5;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // kandydaci pozy (indeks = WindedPose) i probki do linii kandydatow (Z2 krytyka)
        private static readonly string[] Poses = { null, "act_conversation_weary_loop", "act_conversation_weary2_loop", "act_pickup_down_begin_left_stance" };
        private static readonly string[] ProbeActs = { "act_conversation_weary2_loop", "act_conversation_weary_loop", "act_pickup_down_begin_left_stance", "act_npc_idle_injured_1" };
        private static readonly string[] ProbeSets = { "as_human_warrior", "as_human_female_warrior", "as_giant_warrior" };
        private static readonly string[] MaleBreath = { "event:/voice/combat/male/01/grunt_rowing", "event:/voice/combat/male/04/grunt_rowing",
                                                        "event:/voice/combat/male/07/grunt_rowing", "event:/voice/combat/male/08/grunt_rowing" };
        private static readonly string[] FemaleBreath = { "event:/voice/combat/female/02/grunt_rowing", "event:/voice/combat/female/04/grunt_rowing" };

        private sealed class W
        {
            public Agent A;
            public bool Face, Posing, Refused, Breathed, Seen, Near8;
            public int Act = -1, Fails;
            public float PoseUntil, PoseNext, NextBreath, EnemyAt = -100f;
        }

        private static readonly Dictionary<Agent, W> _w = new Dictionary<Agent, W>();
        private static readonly List<Agent> _tmp = new List<Agent>();
        private static readonly MBList<Agent> _buf = new MBList<Agent>();
        private static readonly Dictionary<int, bool> _clip = new Dictionary<int, bool>();   // zestaw akcji (hash = indeks) -> klip pozy istnieje

        // stan taktu
        private static bool _on, _show, _sound;
        private static int _pose, _starts, _queries, _posing;
        private static Vec2 _main;
        private static WeakReference _actsFor;
        private static ActionIndexCache[] _acts;
        private static int[] _maleIds, _femaleIds;
        private static int _breathSec = -1, _breathN;
        private static readonly Random _rng = new Random();

        // liczniki misji (do linii "Oddech AI (144)")
        private static int _people, _faces, _faceBack, _poseStarts, _refused, _noClip, _busy0, _busy1, _breaths, _breathLimited, _breathNoEvent, _fails, _out;
        private static int _stopTime, _stopMove, _stopEnemy, _stopFire, _stopPanic, _stopEnd, _stopFar, _stopGame, _stopOther, _maxPosing;
        private static string _busy0Name, _busy1Name;

        internal static bool Any { get { return _w.Count > 0; } }

        internal static void Reset()
        {
            _w.Clear();
            _tmp.Clear();
            _buf.Clear();
            _clip.Clear();
            _on = _show = _sound = false;
            _pose = _starts = _queries = _posing = 0;
            _actsFor = null; _acts = null; _maleIds = null; _femaleIds = null;
            _breathSec = -1; _breathN = 0;
            _people = _faces = _faceBack = _poseStarts = _refused = _noClip = _busy0 = _busy1 = _breaths = _breathLimited = _breathNoEvent = _fails = _out = 0;
            _stopTime = _stopMove = _stopEnemy = _stopFire = _stopPanic = _stopEnd = _stopFar = _stopGame = _stopOther = _maxPosing = 0;
            _busy0Name = _busy1Name = null;
        }

        private static float Rnd() { return (float)_rng.NextDouble(); }

        /// <summary>Poczatek taktu FieldCraft: czy w tym takcie pokazujemy zadyszke (klucze, bitwa, gracz na polu).</summary>
        internal static bool Gate(Mission m, Settings c)
        {
            _starts = 0;
            _queries = 0;
            _show = c != null && c.WindedShowsIt;
            _sound = c != null && c.WindedBreathSound;
            _pose = c != null ? Math.Max(0, Math.Min(3, c.WindedPose)) : 0;
            var main = Agent.Main;
            _on = (_show || _sound) && m != null && AiBreath.BattleNow && main != null && main.IsActive();
            if (_on)
            {
                _main = main.Position.AsVec2;
                Ensure(m);
            }
            return _on;
        }

        private static void Ensure(Mission m)
        {
            var w = _actsFor;
            if (w != null && ReferenceEquals(w.Target, m) && _acts != null) return;
            _actsFor = new WeakReference(m);
            _clip.Clear();
            _acts = new ActionIndexCache[Poses.Length];
            for (int i = 0; i < Poses.Length; i++) _acts[i] = Poses[i] != null ? ActionIndexCache.Create(Poses[i]) : ActionIndexCache.act_none;
            _maleIds = new int[MaleBreath.Length];
            for (int i = 0; i < MaleBreath.Length; i++) _maleIds[i] = SafeEventId(MaleBreath[i]);
            _femaleIds = new int[FemaleBreath.Length];
            for (int i = 0; i < FemaleBreath.Length; i++) _femaleIds[i] = SafeEventId(FemaleBreath[i]);
        }

        private static int SafeEventId(string name)
        {
            try { return SoundEvent.GetEventIdFromString(name); } catch { return -1; }
        }

        /// <summary>Czlowiek w takcie FieldCraft (glowny watek). winded = FieldCraft State.Winded, speed = predkosc pieszego (-1 = konny / nieznana).</summary>
        internal static void See(Agent a, bool winded, float speed, float now)
        {
            if (a == null) return;
            W w;
            bool has = _w.TryGetValue(a, out w);
            if (!winded && !has) return;
            try
            {
                if (!_on || !a.IsActive() || a.IsMainAgent || !a.IsAIControlled || a.MountAgent != null || speed < 0f)
                {
                    if (has) Stop(w, now, 0, true);
                    return;
                }
                if (a.Position.AsVec2.DistanceSquared(_main) > Near * Near)
                {
                    if (has) Stop(w, now, 1, true);
                    return;
                }
                if (!winded)
                {
                    if (has) Stop(w, now, 2, true);
                    return;
                }
                if (!has)
                {
                    w = new W { A = a, PoseNext = now + 0.5f + Rnd() * 1.5f, NextBreath = now };
                    _w[a] = w;
                    _people++;
                }
                w.Seen = true;
                if (_show && !w.Face)
                {
                    a.SetAgentFacialAnimation(Agent.FacialAnimChannel.Low, "idle_tired", true);
                    w.Face = true;
                    _faces++;
                }
                if (w.Posing) KeepPose(w, speed, now);
                else if (_show && _pose > 0 && !w.Refused && now >= w.PoseNext && _starts < StartsPerTick && _posing < MaxPosing) TryPose(w, speed, now);
                if (_sound && now >= w.NextBreath && (speed < StillBelow || !w.Breathed)
                    && a.Position.AsVec2.DistanceSquared(_main) <= VoiceNear * VoiceNear) Breathe(w, now);
            }
            catch (Exception e)
            {
                if (w != null)
                {
                    w.Fails++;
                    // recenzja 144: poza przerwana i licznik _posing zmniejszony (dotad Posing = false bez _posing-- - po 12 takich ludziach MaxPosing blokowal
                    // wszystkie pozy do konca bitwy, a poza na kanale 1 grala dalej)
                    if (w.Fails >= MaxFails) { w.Refused = true; Abort(w); _out++; }
                }
                _fails++;
                RoutHold.Stumble(RoutHold.SiteBreath, "WindedShow.See", e);
            }
        }

        private static bool Busy(Agent a, int ch, out string name)
        {
            name = null;
            var cur = a.GetCurrentAction(ch);
            if (cur.Index < 0) return false;
            if (a.GetCurrentActionType(ch) == Agent.ActionCodeType.Idle) return false;
            try { name = cur.GetName(); } catch { name = "#" + cur.Index; }
            return true;
        }

        private static bool EnemyClose(W w, float now)
        {
            if (now - w.EnemyAt < EnemyEvery) return w.Near8;
            if (_queries >= QueriesPerTick) return true;           // poza limitem zapytan - ostroznie: jak wrog blisko
            _queries++;
            var a = w.A;
            var m = a.Mission ?? Mission.Current;
            bool near = false;
            if (m != null && a.Team != null)
            {
                m.GetNearbyEnemyAgents(a.Position.AsVec2, EnemyNear, a.Team, _buf);
                for (int i = 0; i < _buf.Count; i++)
                {
                    var x = _buf[i];
                    if (x != null && x.IsActive() && x.IsHuman) { near = true; break; }
                }
                _buf.Clear();
            }
            w.EnemyAt = now;
            w.Near8 = near;
            return near;
        }

        private static bool RangedInHand(Agent a)
        {
            var wi = a.GetPrimaryWieldedItemIndex();
            if (wi == EquipmentIndex.None) return false;
            var mw = a.Equipment[wi];
            var u = mw.IsEmpty ? null : mw.CurrentUsageItem;
            return u != null && u.IsRangedWeapon;
        }

        private static bool ClipFor(Agent a, ActionIndexCache act)
        {
            var set = a.ActionSet;
            if (!set.IsValid) return false;
            int key = set.GetHashCode() * 4 + _pose;
            bool ok;
            if (_clip.TryGetValue(key, out ok)) return ok;
            ok = act.Index >= 0 && MBActionSet.CheckActionAnimationClipExists(set, in act);
            _clip[key] = ok;
            return ok;
        }

        private static void TryPose(W w, float speed, float now)
        {
            var a = w.A;
            if (speed >= StillBelow) return;
            if (a.IsUsingGameObject || a.IsInWater()) return;
            if ((a.GetCurrentAnimationFlag(0) & AnimFlags.anf_synch_with_ladder_movement) != 0
                || (a.GetCurrentAnimationFlag(1) & AnimFlags.anf_synch_with_ladder_movement) != 0) return;
            if (SurvivalMode.InPanic(a) || a.IsRunningAway || a.IsRetreating()) return;
            if (now - a.LastRangedHitTime < FireQuiet) return;
            if (RangedInHand(a)) return;
            var act = _acts != null && _pose < _acts.Length ? _acts[_pose] : ActionIndexCache.act_none;
            if (act.Index < 0 || !ClipFor(a, act)) { _noClip++; w.Refused = true; return; }
            string n0, n1;
            if (Busy(a, 0, out n0)) { _busy0++; if (_busy0Name == null) _busy0Name = n0; w.PoseNext = now + 2f; return; }
            if (Busy(a, 1, out n1)) { _busy1++; if (_busy1Name == null) _busy1Name = n1; w.PoseNext = now + 2f; return; }
            if (EnemyClose(w, now)) { w.PoseNext = now + 2f; return; }
            _starts++;
            bool ok = a.SetActionChannel(1, in act, false, (AnimFlags)0, 0f, 1f, 0.3f, 0.4f, 0f, false, -0.2f, 0, true);
            if (!ok) { _refused++; w.Refused = true; return; }
            w.Posing = true;
            w.Act = act.Index;
            w.PoseUntil = now + PoseMin + Rnd() * PoseSpan;
            _posing++;
            if (_posing > _maxPosing) _maxPosing = _posing;
            _poseStarts++;
        }

        private static void KeepPose(W w, float speed, float now)
        {
            var a = w.A;
            if (a.GetCurrentAction(1).Index != w.Act) { w.Posing = false; _posing--; _stopGame++; w.PoseNext = now + RepeatMin + Rnd() * RepeatSpan; return; }
            int why = -1;
            if (now >= w.PoseUntil) why = 3;
            else if (speed >= StillBelow) why = 4;
            else if (now - a.LastRangedHitTime < FireQuiet) why = 6;
            else if (SurvivalMode.InPanic(a) || a.IsRunningAway) why = 7;
            else if (EnemyClose(w, now)) why = 5;
            if (why >= 0) EndPose(w, now, why);
        }

        /// <summary>Recenzja 144: poza zdjeta po potknieciach (sciezka wyjatku) - licznik _posing zawsze zmniejszony, kanal 1 przerwany, jesli wciaz gra nasza akcje;
        /// wywolania silnika we wlasnym try (ten sam wzor co EndPose).</summary>
        private static void Abort(W w)
        {
            if (w == null || !w.Posing) return;
            w.Posing = false;
            _posing--;
            _stopOther++;
            try
            {
                var a = w.A;
                if (a != null && a.IsActive() && a.GetCurrentAction(1).Index == w.Act)
                    a.SetActionChannel(1, in ActionIndexCache.act_none, false, (AnimFlags)Math.Min(a.GetCurrentActionPriority(1), 73));
            }
            catch { }
        }

        private static void EndPose(W w, float now, int why)
        {
            if (!w.Posing) return;
            w.Posing = false;
            _posing--;
            var a = w.A;
            if (a.IsActive() && a.GetCurrentAction(1).Index == w.Act)
                a.SetActionChannel(1, in ActionIndexCache.act_none, false, (AnimFlags)Math.Min(a.GetCurrentActionPriority(1), 73));
            w.PoseNext = now + RepeatMin + Rnd() * RepeatSpan;
            switch (why)
            {
                case 1: _stopFar++; break;
                case 2: _stopEnd++; break;
                case 3: _stopTime++; break;
                case 4: _stopMove++; break;
                case 5: _stopEnemy++; break;
                case 6: _stopFire++; break;
                case 7: _stopPanic++; break;
                default: _stopOther++; break;
            }
        }

        /// <summary>Koniec pokazu u czlowieka: poza przerwana, twarz z powrotem (idle_normal), wpis zdjety.</summary>
        private static void Stop(W w, float now, int why, bool forget)
        {
            var a = w.A;
            bool live = a != null && a.IsActive();
            if (w.Posing)
            {
                if (live) EndPose(w, now, why);
                else { w.Posing = false; _posing--; }
            }
            if (forget)
            {
                if (w.Face && live) { a.SetAgentFacialAnimation(Agent.FacialAnimChannel.Low, "idle_normal", true); _faceBack++; }
                w.Face = false;
                _w.Remove(a);
            }
        }

        private static void Breathe(W w, float now)
        {
            var a = w.A;
            int sec = (int)Math.Floor(now);
            if (sec != _breathSec) { _breathSec = sec; _breathN = 0; }
            if (_breathN >= BreathsPerSecond) { _breathLimited++; w.NextBreath = now + 1f; return; }
            if (!BattleCries.TryBreathVoice(a, now)) { _breathLimited++; w.NextBreath = now + 1.5f; return; }
            int[] ids = a.IsFemale ? _femaleIds : _maleIds;
            int id = ids != null && ids.Length > 0 ? ids[(a.Index & 0x7FFFFFFF) % ids.Length] : -1;
            w.NextBreath = now + BreathMin + Rnd() * BreathSpan;
            w.Breathed = true;
            if (id < 0) { _breathNoEvent++; return; }
            var m = a.Mission ?? Mission.Current;
            if (m == null) return;
            var p = a.Position;
            m.MakeSound(id, new Vec3(p.x, p.y, p.z + HeadUp), false, true, -1, -1);
            _breathN++;
            _breaths++;
        }

        /// <summary>Koniec taktu FieldCraft: wpisy nie widziane w tym takcie (zginal, zniknal z listy) albo pokaz wylaczony - zdjete.</summary>
        internal static void EndTick(float now)
        {
            if (_w.Count == 0) return;
            try
            {
                _tmp.Clear();
                foreach (var kv in _w) if (!kv.Value.Seen || !_on) _tmp.Add(kv.Key);
                for (int i = 0; i < _tmp.Count; i++)
                {
                    W w;
                    if (_w.TryGetValue(_tmp[i], out w)) Stop(w, now, 0, true);
                }
                _tmp.Clear();
                foreach (var kv in _w) kv.Value.Seen = false;
            }
            catch (Exception e)
            {
                _fails++;
                // recenzja 144: trwajace pozy przerwane przed wyczyszczeniem (dotad _w.Clear() zostawial je na kanale 1)
                try { foreach (var kv in _w) Abort(kv.Value); } catch { }
                _w.Clear();
                _posing = 0;
                RoutHold.Stumble(RoutHold.SiteBreath, "WindedShow.EndTick", e);
            }
        }

        /// <summary>Agent usuniety z misji (FieldCraft.OnAgentDeleted) - wpis zdjety bez wolan silnika.</summary>
        internal static void Forget(Agent a)
        {
            W w;
            if (a == null || !_w.TryGetValue(a, out w)) return;
            if (w.Posing) _posing--;
            _w.Remove(a);
        }

        /// <summary>Linia kandydatow animacji (raz na bitwe, AiBreath.Gate przy pierwszym takcie bitwy): indeks akcji, klip, czas, priorytet, cykl, enforce
        /// dla kazdego kandydata w trzech zestawach; twarz i oddech.</summary>
        internal static void CandidatesLine(Mission m, Settings c)
        {
            var sb = new StringBuilder(1400);
            sb.Append("Oddech AI (144) kandydaci zadyszki: poza WindedPose ").Append(c != null ? Math.Max(0, Math.Min(3, c.WindedPose)) : 0).Append(" = ");
            int pz = c != null ? Math.Max(0, Math.Min(3, c.WindedPose)) : 0;
            sb.Append(pz > 0 ? Poses[pz] : "brak (tylko twarz)").Append(" (zmeczenie widac ").Append(c != null && c.WindedShowsIt ? "TAK" : "NIE").Append(", oddech ")
              .Append(c != null && c.WindedBreathSound ? "TAK" : "NIE").Append(')');
            var acts = new ActionIndexCache[ProbeActs.Length];
            for (int i = 0; i < ProbeActs.Length; i++) acts[i] = ActionIndexCache.Create(ProbeActs[i]);
            for (int s = 0; s < ProbeSets.Length; s++)
            {
                sb.Append(" | ").Append(ProbeSets[s]).Append(':');
                MBActionSet set;
                try { set = MBActionSet.GetActionSet(ProbeSets[s]); } catch { set = MBActionSet.InvalidActionSet; }
                if (!set.IsValid) { sb.Append(" brak zestawu"); continue; }
                for (int i = 0; i < acts.Length; i++)
                {
                    var act = acts[i];
                    sb.Append(i > 0 ? ";" : "").Append(' ').Append(ProbeActs[i]).Append(" #").Append(act.Index);
                    if (act.Index < 0) { sb.Append(" brak akcji"); continue; }
                    bool clip = false;
                    try { clip = MBActionSet.CheckActionAnimationClipExists(set, in act); } catch { clip = false; }
                    if (!clip) { sb.Append(" klip NIE"); continue; }
                    float dur = -1f;
                    ulong fl = 0;
                    try { dur = MBActionSet.GetActionAnimationDuration(set, in act); } catch { dur = -1f; }
                    try { fl = (ulong)MBActionSet.GetActionAnimationFlags(set, in act); } catch { fl = 0; }
                    sb.Append(" klip TAK ").Append(dur.ToString("0.00", Inv)).Append(" s prio 0x").Append((fl & 0xFFUL).ToString("X2", Inv))
                      .Append(" cykl ").Append((fl & (ulong)AnimFlags.anf_cyclic) != 0 ? "TAK" : "NIE")
                      .Append(" enforce dol ").Append((fl & (ulong)AnimFlags.anf_enforce_lowerbody) != 0 ? "TAK" : "NIE")
                      .Append(" caly ").Append((fl & (ulong)AnimFlags.anf_enforce_all) != 0 ? "TAK" : "NIE");
                }
            }
            sb.Append(" | twarz idle_tired / idle_normal (Native voices.xml) | oddech grunt_rowing: zdarzen ");
            int found = 0, all = MaleBreath.Length + FemaleBreath.Length;
            var ids = new StringBuilder(80);
            for (int i = 0; i < MaleBreath.Length; i++) { int id = SafeEventId(MaleBreath[i]); if (id >= 0) found++; ids.Append(i > 0 ? "," : "").Append("m").Append(MaleBreath[i].Split('/')[4]).Append('#').Append(id); }
            for (int i = 0; i < FemaleBreath.Length; i++) { int id = SafeEventId(FemaleBreath[i]); if (id >= 0) found++; ids.Append(",f").Append(FemaleBreath[i].Split('/')[4]).Append('#').Append(id); }
            sb.Append(found).Append('/').Append(all).Append(" (").Append(ids.ToString()).Append(").");
            Log.Info(sb.ToString());
        }

        /// <summary>Segment 134d linii "Oddech AI (144)".</summary>
        internal static void Append(StringBuilder sb, Settings c)
        {
            sb.Append("zmeczenie widac (134d) ").Append(c != null && c.WindedShowsIt ? "TAK" : "NIE").Append(" (poza ").Append(c != null ? Math.Max(0, Math.Min(3, c.WindedPose)) : 0)
              .Append("), oddech ").Append(c != null && c.WindedBreathSound ? "TAK" : "NIE").Append(": ludzi z zadyszka w 35 m ").Append(_people)
              .Append(", twarz zmeczona ").Append(_faces).Append(" (powrot ").Append(_faceBack).Append("), startow pozy ").Append(_poseStarts)
              .Append(" (naraz najwiecej ").Append(_maxPosing).Append("), przerwan: czas ").Append(_stopTime).Append(", ruch ").Append(_stopMove).Append(", wrog w 8 m ")
              .Append(_stopEnemy).Append(", pocisk ").Append(_stopFire).Append(", panika ").Append(_stopPanic).Append(", koniec zadyszki ").Append(_stopEnd)
              .Append(", poza 35 m ").Append(_stopFar).Append(", przez gre ").Append(_stopGame).Append(", inne (pokaz wylaczony, konny, gracz przejal) ").Append(_stopOther)
              .Append("; odmow SetActionChannel ").Append(_refused)
              .Append(", bez klipu w zestawie ").Append(_noClip).Append(", kanal 0 zajety ").Append(_busy0).Append(_busy0Name != null ? " (np. " + _busy0Name + ")" : "")
              .Append(", kanal 1 zajety ").Append(_busy1).Append(_busy1Name != null ? " (np. " + _busy1Name + ")" : "")
              .Append("; oddechow ").Append(_breaths).Append(" (odrzucone limitem ").Append(_breathLimited).Append(", bez zdarzenia ").Append(_breathNoEvent)
              .Append("), potkniecia pokazu ").Append(_fails).Append(" (wypadli ").Append(_out).Append(')');
        }
    }
}
