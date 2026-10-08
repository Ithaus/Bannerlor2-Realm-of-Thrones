using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace CrashScribe
{
    /// <summary>
    /// [AT3] Probkowanie stosu watku glownego w autotescie ("sample": ms miedzy probkami, 0 = wylaczone).
    /// Pomiar klatki (FrameProfiler) mowi, KTORY sluchacz zdarzen zjada czas; probki mowia, CO w jego srodku
    /// (np. ktora czesc dziennego tiku Armoury, ktory model gry w AI lordow, nasze latki na modelach).
    /// Co probke: watek glowny wstrzymany na chwile, caly stos zarzadzany, wznowienie. Liczymy:
    ///  - wlasne: metoda na szczycie stosu (tam stoi ostrze),
    ///  - lacznie: metoda gdziekolwiek na stosie (raz na probke) - ile czasu w niej i w tym, co wola.
    /// Tylko mapa kampanii na przyspieszeniu, bez misji. Raport co dobe gry i na koniec biegu.
    /// </summary>
    internal static class StackSampler
    {
        internal static int IntervalMs;   // 0 = wylaczone
        private static Thread _main, _worker;
        private static volatile bool _run;
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, int> Self = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> Incl = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> RunSelf = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> RunIncl = new Dictionary<string, int>();
        private static readonly Dictionary<MethodBase, string> Keys = new Dictionary<MethodBase, string>();
        private static int _samples, _runSamples, _failed;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static void Start(Thread main)
        {
            if (IntervalMs <= 0 || _worker != null || main == null) return;
            _main = main;
            _run = true;
            _worker = new Thread(Loop) { IsBackground = true, Name = "CrashScribe.StackSampler", Priority = ThreadPriority.AboveNormal };
            _worker.Start();
            Autotest.Note("PROBKI: probkowanie stosu watku gry co " + IntervalMs + " ms (tylko mapa na przyspieszeniu, bez misji)", true);
        }

        private static void Loop()
        {
            while (_run)
            {
                try
                {
                    Thread.Sleep(IntervalMs);
                    if (!OnMapFastForward()) continue;
                    Sample();
                }
                catch { }
            }
        }

        private static bool OnMapFastForward()
        {
            try
            {
                if (TaleWorlds.MountAndBlade.Mission.Current != null) return false;
                var c = TaleWorlds.CampaignSystem.Campaign.Current;
                if (c == null) return false;
                var m = c.TimeControlMode;
                return m == TaleWorlds.CampaignSystem.CampaignTimeControlMode.UnstoppableFastForward
                    || m == TaleWorlds.CampaignSystem.CampaignTimeControlMode.StoppableFastForward
                    || m == TaleWorlds.CampaignSystem.CampaignTimeControlMode.UnstoppableFastForwardForPartyWaitTime;
            }
            catch { return false; }
        }

        private static void Sample()
        {
            StackTrace trace = null;
            try
            {
#pragma warning disable 618
                _main.Suspend();
                try { trace = new StackTrace(_main, false); }
                finally { _main.Resume(); }
#pragma warning restore 618
            }
            catch { _failed++; return; }
            var frames = trace != null ? trace.GetFrames() : null;
            lock (Gate)
            {
                _samples++;
                if (frames == null || frames.Length == 0) { Add(Self, "(kod natywny: silnik, render)"); Add(Incl, "(kod natywny: silnik, render)"); return; }
                var seen = new HashSet<string>();
                bool top = true;
                foreach (var f in frames)
                {
                    MethodBase m = null;
                    try { m = f.GetMethod(); } catch { }
                    if (m == null) continue;
                    string k = Key(m);
                    if (top) { Add(Self, k); top = false; }
                    if (seen.Add(k)) Add(Incl, k);
                }
            }
        }

        private static string Key(MethodBase m)
        {
            string k;
            if (Keys.TryGetValue(m, out k)) return k;
            try
            {
                var t = m.DeclaringType;
                var outer = t;
                while (outer != null && outer.IsNested && (outer.Name.StartsWith("<", StringComparison.Ordinal) || outer.Name.Contains("DisplayClass"))) outer = outer.DeclaringType;
                string mn = m.Name;
                if (mn.StartsWith("<", StringComparison.Ordinal)) { int e = mn.IndexOf('>'); mn = (e > 1 ? mn.Substring(1, e - 1) : "lambda") + "~lambda"; }
                // latki Harmony: DMD<...> - nazwa oryginalu w srodku
                if (mn.StartsWith("DMD<", StringComparison.Ordinal)) mn = mn.Substring(4).TrimEnd('>');
                string mod = outer != null ? Blame.ModuleOf(outer) : null;
                k = (outer != null ? outer.Name : "?") + "." + mn + " <" + (string.IsNullOrEmpty(mod) ? "?" : mod) + ">";
            }
            catch { k = "?"; }
            Keys[m] = k;
            return k;
        }

        private static void Add(Dictionary<string, int> d, string k) { int v; d.TryGetValue(k, out v); d[k] = v + 1; }

        internal static void Report(string label, int top)
        {
            if (IntervalMs <= 0 || _worker == null) return;
            try
            {
                List<KeyValuePair<string, int>> incl, self;
                int n;
                lock (Gate)
                {
                    n = _samples;
                    incl = Incl.OrderByDescending(kv => kv.Value).Take(top).ToList();
                    self = Self.OrderByDescending(kv => kv.Value).Take(top).ToList();
                    foreach (var kv in Incl) { int v; RunIncl.TryGetValue(kv.Key, out v); RunIncl[kv.Key] = v + kv.Value; }
                    foreach (var kv in Self) { int v; RunSelf.TryGetValue(kv.Key, out v); RunSelf[kv.Key] = v + kv.Value; }
                    _runSamples += _samples;
                    Incl.Clear(); Self.Clear(); _samples = 0;
                }
                if (n == 0) return;
                Write("PROBKI " + label + " (" + n + " probek, nieudanych " + _failed + ")", n, incl, self);
            }
            catch (Exception e) { Autotest.Fail("Probki.Report", e); }
        }

        internal static void RunSummary(int top)
        {
            if (IntervalMs <= 0 || _worker == null) return;
            try
            {
                Report("ostatnie okno", 0);
                List<KeyValuePair<string, int>> incl, self;
                lock (Gate)
                {
                    incl = RunIncl.OrderByDescending(kv => kv.Value).Take(top).ToList();
                    self = RunSelf.OrderByDescending(kv => kv.Value).Take(top).ToList();
                }
                Write("PROBKI CALY BIEG (" + _runSamples + " probek)", _runSamples, incl, self);
                // nasze mody osobno: lacznie, poza samymi sluchaczami - gdzie w srodku
                foreach (var mod in new[] { "Armoury", "GrandTourney", "RealisticCaptivity" })
                {
                    List<KeyValuePair<string, int>> ours;
                    lock (Gate) ours = RunIncl.Where(kv => kv.Key.EndsWith("<" + mod + ">", StringComparison.Ordinal)).OrderByDescending(kv => kv.Value).Take(top).ToList();
                    if (ours.Count == 0) continue;
                    Autotest.Note("PROBKI " + mod + " - lacznie (metoda i to, co wola):", true);
                    foreach (var kv in ours) Autotest.Note("  " + Pct(kv.Value, _runSamples) + "  " + kv.Key, false);
                }
                _run = false;
            }
            catch (Exception e) { Autotest.Fail("Probki.RunSummary", e); }
        }

        private static void Write(string head, int n, List<KeyValuePair<string, int>> incl, List<KeyValuePair<string, int>> self)
        {
            if (incl.Count == 0 && self.Count == 0) return;
            Autotest.Note(head + " - LACZNIE (metoda gdziekolwiek na stosie):", true);
            foreach (var kv in incl) Autotest.Note("  " + Pct(kv.Value, n) + "  " + kv.Key, false);
            Autotest.Note(head + " - WLASNE (szczyt stosu):", false);
            foreach (var kv in self) Autotest.Note("  " + Pct(kv.Value, n) + "  " + kv.Key, false);
        }

        private static string Pct(int v, int n) { return (100.0 * v / Math.Max(1, n)).ToString("0.0", Inv).PadLeft(5) + "%"; }
    }
}
