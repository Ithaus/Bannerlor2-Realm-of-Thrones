using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace CrashScribe
{
    /// <summary>
    /// [AT3] Pomiar klatki w autotescie (Jeff 08.10: "gra mocno zwalnia, co tak obciaza gre").
    /// Doba gry przy x8 rosla przez rok z 11.7 s do 24 s, a lordow i wojska bylo tyle samo -
    /// klatka liczy sie dluzej, niz gra pozwala (dt gry jest przyciete), wiec zegar gry zwalnia.
    ///
    /// Dwie warstwy, obie TYLKO gdy autotest ma "profile":true:
    ///  1. sekcje silnika kampanii (latki Harmony prefiks + finalizer): RealTick (ruch druzyn),
    ///     Tick, zdarzenia okresowe, bitwy, myslenie AI (PartiesThink), ekran mapy;
    ///  2. kazdy sluchacz zdarzen kampanii (CampaignEvents: tik, godzina, doba, AI druzyny...)
    ///     owiniety pomiarem - kto (typ + metoda + mod) zjada czas.
    /// Czas liczony "na wlasny rachunek": sekcja nie liczy w sobie sluchaczy i podsekcji, wiec
    /// suma pozycji = zmierzona czesc klatki, reszta = silnik natywny (render, UI, sterownik).
    /// Tylko watek glowny; z innych watkow wywolania przechodza bez pomiaru.
    /// </summary>
    internal static class FrameProfiler
    {
        internal static bool Enabled;
        private static bool _installed;
        private static int _mainId = -1;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // --- pozycje (sloty) ---
        private const int MaxSlots = 8192;
        private static readonly long[] Self = new long[MaxSlots];
        private static readonly long[] Calls = new long[MaxSlots];
        private static readonly long[] RunSelf = new long[MaxSlots];
        private static readonly long[] RunCalls = new long[MaxSlots];
        private static readonly long[] Gc = new long[MaxSlots];      // sprzatania gen0 w czasie wlasnym pozycji (kto tworzy smieci)
        private static readonly long[] RunGc = new long[MaxSlots];
        private static readonly string[] Names = new string[MaxSlots];
        private static int _slots;
        private static readonly Dictionary<string, int> SlotByName = new Dictionary<string, int>();
        private static readonly Dictionary<MethodBase, int> SlotByMethod = new Dictionary<MethodBase, int>();

        // --- stos (watek glowny) ---
        private const int MaxDepth = 256;
        private static readonly long[] StStart = new long[MaxDepth];
        private static readonly long[] StChild = new long[MaxDepth];
        private static readonly int[] StGc = new int[MaxDepth];
        private static readonly int[] StGcChild = new int[MaxDepth];
        private static int _depth;

        // --- klatki ---
        private static long _lastFrameTs;
        private static long _frames, _frameTicks, _frameMax;
        private static long _runFrames, _runFrameTicks;
        private static int _gc0, _gc1, _gc2;
        private static int _wrapped, _wrapFailed;

        internal static void Install(Harmony h)
        {
            if (!Enabled || _installed) return;
            _installed = true;
            _mainId = Thread.CurrentThread.ManagedThreadId;
            var done = new List<string>();
            var miss = new List<string>();
            Section(h, typeof(Campaign), "RealTick", "silnik: RealTick (ruch druzyn, oblezenia)", done, miss);
            Section(h, typeof(Campaign), "Tick", "silnik: Campaign.Tick (reszta)", done, miss);
            Section(h, typeof(Campaign), "LateAITickAux", "silnik: myslenie AI druzyn (PartiesThink)", done, miss);
            Section(h, typeof(Campaign), "DailyTick", "silnik: doba (rozsylanie)", done, miss);
            Section(h, typeof(Campaign), "HourlyTick", "silnik: godzina (rozsylanie)", done, miss);
            var pem = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignPeriodicEventManager");
            Section(h, pem, "MobilePartyHourlyTick", "silnik: godzina kazdej druzyny (MobileParty.HourlyTick)", done, miss);
            Section(h, pem, "TickPeriodicEvents", "silnik: tiki okresowe druzyn/osad/bohaterow (rozsylanie)", done, miss);
            Section(h, pem, "TickPartialHourlyAi", "silnik: AI godzinowe druzyn (rozsylanie)", done, miss);
            Section(h, pem, "OnTick", "silnik: zdarzenia okresowe modow (CheckUpdate)", done, miss);
            Section(h, AccessTools.TypeByName("TaleWorlds.CampaignSystem.MapEvents.MapEventManager"), "Tick", "silnik: bitwy na mapie (MapEventManager)", done, miss);
            var tickData = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignTickCacheDataStore");
            Section(h, tickData, "RealTick", "silnik: ruch druzyn (TickCache.RealTick)", done, miss);
            Section(h, tickData, "Tick", "silnik: TickCache.Tick", done, miss);
            Section(h, AccessTools.TypeByName("TaleWorlds.CampaignSystem.Encounters.EncounterManager"), "Tick", "silnik: spotkania (EncounterManager)", done, miss);
            Section(h, AccessTools.TypeByName("SandBox.View.Map.MapScreen"), "OnFrameTick", "ekran mapy: MapScreen.OnFrameTick (widoki, tabliczki, UI)", done, miss);
            Autotest.Note("PROFIL: pomiar klatki wlaczony - sekcje " + done.Count + " (" + string.Join(", ", done.ToArray()) + ")"
                          + (miss.Count > 0 ? "; brak: " + string.Join(", ", miss.ToArray()) : ""), true);
        }

        private static void Section(Harmony h, Type t, string method, string label, List<string> done, List<string> miss)
        {
            try
            {
                if (t == null) { miss.Add(method + " (brak typu)"); return; }
                var m = AccessTools.DeclaredMethod(t, method);
                if (m == null) { miss.Add(t.Name + "." + method); return; }
                SlotByMethod[m] = Slot(label);
                h.Patch(m, prefix: new HarmonyMethod(typeof(FrameProfiler), nameof(SecPrefix)) { priority = Priority.First },
                        finalizer: new HarmonyMethod(typeof(FrameProfiler), nameof(SecFinalizer)) { priority = Priority.Last });
                done.Add(t.Name + "." + method);
            }
            catch (Exception e) { miss.Add(method + " (" + e.GetType().Name + ")"); }
        }

        public static void SecPrefix(out int __state) { __state = Enter(); }

        public static Exception SecFinalizer(Exception __exception, MethodBase __originalMethod, int __state)
        {
            if (__state >= 0)
            {
                int s;
                if (__originalMethod != null && SlotByMethod.TryGetValue(__originalMethod, out s)) Exit(s, __state);
                else Exit(0, __state);
            }
            return __exception;
        }

        internal static int Slot(string name)
        {
            int s;
            if (SlotByName.TryGetValue(name, out s)) return s;
            if (_slots == 0) { Names[0] = "(nieznane)"; SlotByName[Names[0]] = 0; _slots = 1; }
            if (_slots >= MaxSlots) return 0;
            s = _slots++;
            Names[s] = name;
            SlotByName[name] = s;
            return s;
        }

        /// <summary>Poczatek pomiaru; zwraca glebokosc stosu albo -1 (inny watek, wylaczone).</summary>
        internal static int Enter()
        {
            if (!Enabled || Thread.CurrentThread.ManagedThreadId != _mainId || _depth >= MaxDepth) return -1;
            int d = _depth++;
            StGc[d] = GC.CollectionCount(0);
            StGcChild[d] = 0;
            StStart[d] = Stopwatch.GetTimestamp();
            StChild[d] = 0;
            return d;
        }

        internal static void Exit(int slot, int d)
        {
            if (d < 0 || d >= MaxDepth) return;
            long now = Stopwatch.GetTimestamp();
            int gcEl = GC.CollectionCount(0) - StGc[d];
            int gcSelf = gcEl - StGcChild[d];
            if (gcSelf > 0) Gc[slot] += gcSelf;
            if (d > 0) StGcChild[d - 1] += gcEl;
            _depth = d;   // wyjatek ponizej mogl zostawic stos glebiej - zwijamy do wlasnego poziomu
            long el = now - StStart[d];
            long self = el - StChild[d];
            if (self < 0) self = 0;
            Self[slot] += self;
            Calls[slot]++;
            if (d > 0) StChild[d - 1] += el;
        }

        /// <summary>Z OnApplicationTick: okres klatki (tylko mapa kampanii na przyspieszeniu, bez misji).</summary>
        internal static void Frame()
        {
            if (!Enabled) return;
            long now = Stopwatch.GetTimestamp();
            bool ok = false;
            try
            {
                var c = Campaign.Current;
                ok = c != null && TaleWorlds.MountAndBlade.Mission.Current == null
                     && (c.TimeControlMode == CampaignTimeControlMode.UnstoppableFastForward
                         || c.TimeControlMode == CampaignTimeControlMode.StoppableFastForward
                         || c.TimeControlMode == CampaignTimeControlMode.UnstoppableFastForwardForPartyWaitTime);
            }
            catch { }
            if (ok && _lastFrameTs > 0)
            {
                long el = now - _lastFrameTs;
                if (el < Stopwatch.Frequency * 5)   // dluzsza przerwa = zapis, okno - nie klatka
                {
                    _frames++;
                    _frameTicks += el;
                    if (el > _frameMax) _frameMax = el;
                }
            }
            _lastFrameTs = ok ? now : 0;
        }

        // ------------------------------------------------------------------ sluchacze zdarzen

        /// <summary>Owija wszystkich sluchaczy CampaignEvents, ktorzy nie sa jeszcze owinieci.</summary>
        internal static void WrapListeners()
        {
            if (!Enabled) return;
            int before = _wrapped, failBefore = _wrapFailed;
            try
            {
                var c = Campaign.Current;
                if (c == null) return;
                var evProp = typeof(Campaign).GetProperty("CampaignEvents", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var events = evProp != null ? evProp.GetValue(c) : null;
                if (events == null) { Autotest.Note("PROFIL: brak Campaign.CampaignEvents - sluchacze bez pomiaru", true); return; }
                foreach (var f in events.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    var ft = f.FieldType;
                    if (ft != typeof(MbEvent) && !(ft.IsGenericType && ft.Namespace == "TaleWorlds.CampaignSystem" && ft.Name.StartsWith("MbEvent`", StringComparison.Ordinal))) continue;
                    object ev = f.GetValue(events);
                    if (ev == null) continue;
                    var head = ft.GetField("_nonSerializedListenerList", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (head == null) continue;
                    string evName = EventLabel(f.Name);
                    Type[] args = ft.IsGenericType ? ft.GetGenericArguments() : Type.EmptyTypes;
                    object rec = head.GetValue(ev);
                    int guard = 0;
                    while (rec != null && guard++ < 100000)
                    {
                        WrapRec(rec, evName, args);
                        var next = rec.GetType().GetField("Next", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        rec = next != null ? next.GetValue(rec) : null;
                    }
                }
            }
            catch (Exception e) { Autotest.Fail("Profil.WrapListeners", e); }
            if (_wrapped != before || _wrapFailed != failBefore)
                Autotest.Note("PROFIL: sluchacze zdarzen pod pomiarem: " + _wrapped + " (nowych " + (_wrapped - before) + ", nieudanych " + _wrapFailed + "), pozycji " + _slots, false);
        }

        private static string EventLabel(string field)
        {
            string s = field.TrimStart('_');
            if (s.EndsWith("Event", StringComparison.Ordinal) && s.Length > 5) s = s.Substring(0, s.Length - 5);
            return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s.Substring(1) : field;
        }

        private static readonly Dictionary<Type, PropertyInfo> ActionProps = new Dictionary<Type, PropertyInfo>();

        private static void WrapRec(object rec, string evName, Type[] args)
        {
            try
            {
                var rt = rec.GetType();
                PropertyInfo p;
                if (!ActionProps.TryGetValue(rt, out p))
                {
                    p = rt.GetProperty("Action", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    ActionProps[rt] = p;
                }
                if (p == null) return;
                var d = p.GetValue(rec) as Delegate;
                if (d == null || d.Target is IWrap) return;
                var owner = rt.GetProperty("Owner", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                object o = owner != null ? owner.GetValue(rec) : null;
                int slot = Slot(evName + ": " + Who(d, o));
                Type wt;
                switch (args.Length)
                {
                    case 0: wt = typeof(W0); break;
                    case 1: wt = typeof(W1<>).MakeGenericType(args); break;
                    case 2: wt = typeof(W2<,>).MakeGenericType(args); break;
                    case 3: wt = typeof(W3<,,>).MakeGenericType(args); break;
                    case 4: wt = typeof(W4<,,,>).MakeGenericType(args); break;
                    case 5: wt = typeof(W5<,,,,>).MakeGenericType(args); break;
                    case 6: wt = typeof(W6<,,,,,>).MakeGenericType(args); break;
                    case 7: wt = typeof(W7<,,,,,,>).MakeGenericType(args); break;
                    default: _wrapFailed++; return;
                }
                var w = Activator.CreateInstance(wt);
                wt.GetField("A").SetValue(w, d);
                wt.GetField("S").SetValue(w, slot);
                // typ z deklaracji (Action<TS>), nie z obiektu: przez kontrawariancje w polu moze siedziec np. Action<object>
                var nd = Delegate.CreateDelegate(p.PropertyType, w, "Call");
                p.SetValue(rec, nd);
                _wrapped++;
            }
            catch { _wrapFailed++; }
        }

        private static string Who(Delegate d, object owner)
        {
            var m = d.Method;
            Type t = m != null ? m.DeclaringType : null;
            while (t != null && t.IsNested && (t.Name.StartsWith("<", StringComparison.Ordinal) || t.Name.Contains("DisplayClass")))
                t = t.DeclaringType;
            if (t == null && owner != null) t = owner.GetType();
            string mn = m != null ? m.Name : "?";
            if (mn.StartsWith("<", StringComparison.Ordinal))
            {
                int e = mn.IndexOf('>');
                mn = (e > 1 ? mn.Substring(1, e - 1) : "lambda") + "~lambda";
            }
            string mod = t != null ? Blame.ModuleOf(t) : null;
            return (t != null ? t.Name : "?") + "." + mn + " <" + (string.IsNullOrEmpty(mod) ? "?" : mod) + ">";
        }

        internal interface IWrap { }

        public sealed class W0 : IWrap
        {
            public Action A; public int S;
            public void Call() { int d = Enter(); try { A(); } finally { Exit(S, d); } }
        }
        public sealed class W1<T1> : IWrap
        {
            public Action<T1> A; public int S;
            public void Call(T1 a) { int d = Enter(); try { A(a); } finally { Exit(S, d); } }
        }
        public sealed class W2<T1, T2> : IWrap
        {
            public Action<T1, T2> A; public int S;
            public void Call(T1 a, T2 b) { int d = Enter(); try { A(a, b); } finally { Exit(S, d); } }
        }
        public sealed class W3<T1, T2, T3> : IWrap
        {
            public Action<T1, T2, T3> A; public int S;
            public void Call(T1 a, T2 b, T3 c) { int d = Enter(); try { A(a, b, c); } finally { Exit(S, d); } }
        }
        public sealed class W4<T1, T2, T3, T4> : IWrap
        {
            public Action<T1, T2, T3, T4> A; public int S;
            public void Call(T1 a, T2 b, T3 c, T4 e) { int d = Enter(); try { A(a, b, c, e); } finally { Exit(S, d); } }
        }
        public sealed class W5<T1, T2, T3, T4, T5> : IWrap
        {
            public Action<T1, T2, T3, T4, T5> A; public int S;
            public void Call(T1 a, T2 b, T3 c, T4 e, T5 f) { int d = Enter(); try { A(a, b, c, e, f); } finally { Exit(S, d); } }
        }
        public sealed class W6<T1, T2, T3, T4, T5, T6> : IWrap
        {
            public Action<T1, T2, T3, T4, T5, T6> A; public int S;
            public void Call(T1 a, T2 b, T3 c, T4 e, T5 f, T6 g) { int d = Enter(); try { A(a, b, c, e, f, g); } finally { Exit(S, d); } }
        }
        public sealed class W7<T1, T2, T3, T4, T5, T6, T7> : IWrap
        {
            public Action<T1, T2, T3, T4, T5, T6, T7> A; public int S;
            public void Call(T1 a, T2 b, T3 c, T4 e, T5 f, T6 g, T7 h) { int d = Enter(); try { A(a, b, c, e, f, g, h); } finally { Exit(S, d); } }
        }

        // ------------------------------------------------------------------ raport

        /// <summary>Raport okna (np. jednej doby): klatka, GC, najwieksze pozycje; potem zerowanie okna.</summary>
        internal static void Report(string label, int top)
        {
            if (!Enabled) return;
            try
            {
                double f = Stopwatch.Frequency / 1000.0;   // tikow na ms
                int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
                long mem = GC.GetTotalMemory(false);
                long sum = 0;
                var list = new List<int>();
                for (int i = 0; i < _slots; i++)
                {
                    if (Self[i] <= 0) continue;
                    sum += Self[i];
                    list.Add(i);
                }
                list.Sort((a, b) => Self[b].CompareTo(Self[a]));
                double frames = Math.Max(1, _frames);
                double frameMs = _frames > 0 ? _frameTicks / f / _frames : 0;
                double measMs = sum / f / frames;
                var sb = new StringBuilder();
                sb.Append("PROFIL ").Append(label).Append(": klatek ").Append(_frames)
                  .Append(", klatka srednio ").Append(frameMs.ToString("0.0", Inv)).Append(" ms (najdluzsza ").Append((_frameMax / f).ToString("0", Inv)).Append(" ms)")
                  .Append(", zmierzone ").Append(measMs.ToString("0.0", Inv)).Append(" ms, reszta (silnik natywny, render) ").Append(Math.Max(0, frameMs - measMs).ToString("0.0", Inv)).Append(" ms")
                  .Append(" | GC: gen0 +").Append(g0 - _gc0).Append(", gen1 +").Append(g1 - _gc1).Append(", gen2 +").Append(g2 - _gc2)
                  .Append(", pamiec zarzadzana ").Append((mem / 1048576.0).ToString("0", Inv)).Append(" MB");
                Autotest.Note(sb.ToString(), true);
                int shown = 0;
                foreach (int i in list)
                {
                    if (shown++ >= top) break;
                    Autotest.Note("  " + (Self[i] / f / frames).ToString("0.00", Inv).PadLeft(7) + " ms/klatke  "
                                  + (sum > 0 ? (100.0 * Self[i] / sum).ToString("0.0", Inv) : "0").PadLeft(5) + "%  wywolan " + Calls[i].ToString(Inv).PadLeft(8) + "  gc0 " + Gc[i].ToString(Inv).PadLeft(5) + "  " + Names[i], false);
                }
                for (int i = 0; i < _slots; i++) { RunSelf[i] += Self[i]; RunCalls[i] += Calls[i]; RunGc[i] += Gc[i]; Self[i] = 0; Calls[i] = 0; Gc[i] = 0; }
                _runFrames += _frames; _runFrameTicks += _frameTicks;
                _frames = 0; _frameTicks = 0; _frameMax = 0;
                _gc0 = g0; _gc1 = g1; _gc2 = g2;
            }
            catch (Exception e) { Autotest.Fail("Profil.Report", e); }
        }

        /// <summary>Podsumowanie calego biegu (wolane przy koncu).</summary>
        internal static void RunSummary(int top)
        {
            if (!Enabled) return;
            try
            {
                double f = Stopwatch.Frequency / 1000.0;
                for (int i = 0; i < _slots; i++) { RunSelf[i] += Self[i]; RunCalls[i] += Calls[i]; RunGc[i] += Gc[i]; Self[i] = 0; Calls[i] = 0; Gc[i] = 0; }
                _runFrames += _frames; _runFrameTicks += _frameTicks; _frames = 0; _frameTicks = 0;
                long sum = 0;
                var list = new List<int>();
                for (int i = 0; i < _slots; i++) { if (RunSelf[i] > 0) { sum += RunSelf[i]; list.Add(i); } }
                list.Sort((a, b) => RunSelf[b].CompareTo(RunSelf[a]));
                double frames = Math.Max(1, _runFrames);
                double frameMs = _runFrames > 0 ? _runFrameTicks / f / _runFrames : 0;
                Autotest.Note("PROFIL CALY BIEG: klatek " + _runFrames + ", klatka srednio " + frameMs.ToString("0.0", Inv) + " ms, zmierzone "
                              + (sum / f / frames).ToString("0.0", Inv) + " ms; sluchaczy pod pomiarem " + _wrapped + " (nieudanych " + _wrapFailed + ")", true);
                int shown = 0;
                foreach (int i in list)
                {
                    if (shown++ >= top) break;
                    Autotest.Note("  " + (RunSelf[i] / f / frames).ToString("0.00", Inv).PadLeft(7) + " ms/klatke  "
                                  + (100.0 * RunSelf[i] / Math.Max(1, sum)).ToString("0.0", Inv).PadLeft(5) + "%  wywolan " + RunCalls[i].ToString(Inv).PadLeft(9) + "  gc0 " + RunGc[i].ToString(Inv).PadLeft(6) + "  " + Names[i], false);
                }
                // najwiecej sprzatan pamieci (kto tworzy smieci)
                var byGc = new List<int>(list);
                byGc.Sort((a, b) => RunGc[b].CompareTo(RunGc[a]));
                long gcSum = 0; foreach (int i in byGc) gcSum += RunGc[i];
                var gcTop = new List<string>();
                foreach (int i in byGc) { if (gcTop.Count >= 15 || RunGc[i] <= 0) break; gcTop.Add(Names[i] + " " + RunGc[i] + " (" + (100.0 * RunGc[i] / Math.Max(1, gcSum)).ToString("0", Inv) + "%)"); }
                Autotest.Note("PROFIL smieci (sprzatania gen0 w czasie wlasnym, razem " + gcSum + "): " + string.Join(" | ", gcTop.ToArray()), true);
                // sumy wedlug moda (koncowka nazwy w <>)
                var byMod = new Dictionary<string, long>();
                foreach (int i in list)
                {
                    string n = Names[i] ?? "";
                    int a = n.LastIndexOf('<'), b = n.LastIndexOf('>');
                    string mod = a >= 0 && b > a ? n.Substring(a + 1, b - a - 1) : (n.StartsWith("silnik", StringComparison.Ordinal) ? "silnik (Native, wlasny czas)" : n.StartsWith("ekran", StringComparison.Ordinal) ? "ekran mapy (wlasny czas)" : "?");
                    long v; byMod.TryGetValue(mod, out v); byMod[mod] = v + RunSelf[i];
                }
                var mods = byMod.OrderByDescending(kv => kv.Value).Take(20)
                    .Select(kv => kv.Key + " " + (kv.Value / f / frames).ToString("0.0", Inv) + " ms (" + (100.0 * kv.Value / Math.Max(1, sum)).ToString("0", Inv) + "%)");
                Autotest.Note("PROFIL wedlug modow: " + string.Join(" | ", mods.ToArray()), true);
            }
            catch (Exception e) { Autotest.Fail("Profil.RunSummary", e); }
        }
    }
}
