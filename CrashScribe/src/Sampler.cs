using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace CrashScribe
{
    /// <summary>
    /// Profiler przyspieszenia. Jeff 28.08: "na x1 gra dziala normalnie, na x3
    /// ogromny spadek klatek - nie da sie grac". Na x3 watek glowny robi
    /// trzykrotnie wiecej symulacji na klatke, ale KTO dokladnie zre czas -
    /// tego zaden log nie mowil. Ten watek, TYLKO gdy kampania idzie na
    /// przyspieszeniu i nie ma misji, zdejmuje co pol sekundy stos watku
    /// glownego (ten sam mechanizm co straznik zawieszen) i zlicza, w czyim
    /// kodzie utknelo ostrze. Raport do logu co ~30 s probkowania:
    /// procent probek + ramka + pierwszy mod w lancuchu wywolan.
    /// </summary>
    internal static class Sampler
    {
        private static Thread _main;
        private static Thread _worker;
        private static bool _started;
        private static readonly Dictionary<string, int> _hits = new Dictionary<string, int>();
        private static int _samples;
        private static DateTime _lastReport = DateTime.UtcNow;

        internal static void Start(Thread mainThread)
        {
            if (_started || mainThread == null) return;
            _started = true;
            _main = mainThread;
            _worker = new Thread(Loop) { IsBackground = true, Name = "CrashScribe.Sampler" };
            _worker.Start();
            Scribe.Line("Profiler przyspieszenia czuwa (probki stosu tylko na fast forward, poza misja).");
        }

        private static void Loop()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(500);
                    if (!FastForwardOnMap())
                    {
                        // zwolnil - oddaj raport z tego, co sie nazbieralo
                        if (_samples >= 20) Report();
                        continue;
                    }
                    Sample();
                    if (_samples >= 20 && (DateTime.UtcNow - _lastReport).TotalSeconds >= 30) Report();
                }
                catch { }
            }
        }

        private static bool FastForwardOnMap()
        {
            try
            {
                if (TaleWorlds.MountAndBlade.Mission.Current != null) return false;
                var c = TaleWorlds.CampaignSystem.Campaign.Current;
                if (c == null) return false;
                var m = c.TimeControlMode;
                return m == TaleWorlds.CampaignSystem.CampaignTimeControlMode.StoppableFastForward
                    || m == TaleWorlds.CampaignSystem.CampaignTimeControlMode.UnstoppableFastForward
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
            catch { return; }
            if (trace == null) return;

            string key;
            var frames = trace.GetFrames();
            if (frames == null || frames.Length == 0)
            {
                // stos pusty = watek w kodzie natywnym (silnik, render, sterownik)
                key = "(native: silnik/render/sterownik)";
            }
            else
            {
                System.Reflection.MethodBase top = null;
                string mod = null;
                foreach (var f in frames)
                {
                    var m = f.GetMethod();
                    if (m == null || m.DeclaringType == null) continue;
                    if (top == null) top = m;
                    if (mod == null)
                    {
                        var b = Blame.ModuleOf(m.DeclaringType);
                        if (!string.IsNullOrEmpty(b) && b != "Native") mod = b;
                    }
                    if (top != null && mod != null) break;
                }
                key = (top != null && top.DeclaringType != null
                        ? top.DeclaringType.Name + "." + top.Name : "(bez ramek)")
                    + (mod != null ? "  <" + mod + ">" : "  <silnik>");
            }
            _samples++;
            int v; _hits.TryGetValue(key, out v); _hits[key] = v + 1;
        }

        private static void Report()
        {
            try
            {
                _lastReport = DateTime.UtcNow;
                var list = new List<KeyValuePair<string, int>>(_hits);
                list.Sort((a, b) => b.Value.CompareTo(a.Value));
                var sb = new StringBuilder();
                sb.Append("PROFIL FAST-FORWARD (").Append(_samples).Append(" probek):");
                int shown = 0;
                foreach (var kv in list)
                {
                    if (shown++ >= 6) break;
                    sb.Append("  ").Append((int)Math.Round(100.0 * kv.Value / _samples))
                      .Append("% ").Append(kv.Key).Append(" |");
                }
                Scribe.Line(sb.ToString());
            }
            catch { }
            _hits.Clear();
            _samples = 0;
        }
    }
}
