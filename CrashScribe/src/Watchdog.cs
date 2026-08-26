using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace CrashScribe
{
    /// <summary>
    /// Straznik zawieszen. Watek glowny bije serce co klatke; gdy serce ustaje na dobre
    /// kilka sekund, straznik zawiesza go na moment, spisuje jego stos wywolan z etykietami
    /// modow i puszcza dalej. Czysty freeze przestaje byc niemy - log mowi, CZYJ kod stal
    /// w miejscu, przynajmniej do granicy kodu natywnego.
    /// </summary>
    internal static class Watchdog
    {
        private const int StallSeconds = 10;        // w misji
        private const int QuietStallSeconds = 60;   // poza misja - ladowanie sejwa potrafi trwac
        private const int MaxDumps = 3;

        private static Thread _main;
        private static Thread _guard;
        private static long _lastBeat;      // DateTime.UtcNow.Ticks
        private static bool _started;
        private static bool _dumpedThisStall;
        private static int _dumps;

        internal static void Init(Thread mainThread)
        {
            _main = mainThread;
        }

        internal static void Beat()
        {
            Interlocked.Exchange(ref _lastBeat, DateTime.UtcNow.Ticks);
        }

        internal static void Start()
        {
            if (_started || _main == null) return;
            _started = true;
            Beat();
            _guard = new Thread(Loop) { IsBackground = true, Name = "CrashScribe.Watchdog" };
            _guard.Start();
            Scribe.Line("Straznik zawieszen czuwa (prog " + StallSeconds + " s).");
        }

        private static void Loop()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(2000);
                    long last = Interlocked.Read(ref _lastBeat);
                    double quiet = (DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc)).TotalSeconds;

                    int need = InMission() ? StallSeconds : QuietStallSeconds;
                    if (quiet < need) { _dumpedThisStall = false; continue; }
                    if (_dumpedThisStall || _dumps >= MaxDumps) continue;
                    _dumpedThisStall = true;
                    _dumps++;
                    Dump(quiet);
                }
                catch { }
            }
        }

        private static bool InMission()
        {
            try { return TaleWorlds.MountAndBlade.Mission.Current != null; }
            catch { return false; }
        }

        private static void Dump(double quietSeconds)
        {
            try
            {
                // OSOBNY KANAL. Glowny log ma wspolna blokade (Scribe.Gate), a okruchy
                // wspolna blokade Trail.Gate - obie potrafi trzymac ZAMROZONY watek
                // glowny (freeze 26.08 15:02: ostatnia linia logu nalezala do Scribe,
                // raportu HANG nie bylo, bo straznik wisial na tej samej blokadzie).
                // Dlatego stos idzie najpierw do wlasnego pliku hang-*.log pisanego
                // wprost, etapami (najcenniejsze dane najpierw); do glownego logu
                // tylko PROBA dopisania z limitem czasu, na sam koniec.
                string hangFile = null;
                try
                {
                    hangFile = System.IO.Path.Combine(Scribe.ReportDir,
                        "hang-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".log");
                }
                catch { }

                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("#####################################################################");
                sb.AppendLine("# GAME HANG   " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                              "   (main thread silent for " + (int)quietSeconds + " s)");
                sb.AppendLine("#####################################################################");
                WriteHang(hangFile, sb);

                StackTrace trace = null;
                try
                {
#pragma warning disable 618
                    _main.Suspend();
                    try { trace = new StackTrace(_main, false); }
                    finally { _main.Resume(); }
#pragma warning restore 618
                }
                catch (Exception e)
                {
                    sb.AppendLine("  (could not capture the main thread stack: " + e.GetType().Name + ": " + e.Message + ")");
                }

                if (trace != null)
                {
                    var frames = trace.GetFrames();
                    if (frames == null || frames.Length == 0)
                    {
                        sb.AppendLine("  (no managed frames - the thread is deep in native engine/driver code)");
                    }
                    else
                    {
                        sb.AppendLine("--- MAIN THREAD STACK (top = where it is stuck) ---");
                        foreach (var f in frames)
                        {
                            var m = f.GetMethod();
                            string mod = m != null ? Blame.ModuleOf(m.DeclaringType) : "?";
                            string dt = m != null && m.DeclaringType != null ? m.DeclaringType.FullName : "?";
                            sb.AppendLine(string.Format("  [{0,-22}] {1}.{2}",
                                string.IsNullOrEmpty(mod) ? "?" : mod, dt, m != null ? m.Name : "?"));
                        }
                    }
                }
                WriteHang(hangFile, sb);   // stos juz bezpieczny na dysku

                // stan gry dopiero teraz - czyta obiekty gry, wiec sam moze utknac;
                // gdyby utknal, stos wyzej juz jest zapisany
                var ctx = GameState.Describe();
                if (!string.IsNullOrEmpty(ctx))
                {
                    sb.AppendLine("--- GAME STATE ---");
                    sb.AppendLine(ctx);
                }
                sb.AppendLine("#####################################################################");
                WriteHang(hangFile, sb);

                // proba dopisania do glownego logu - z limitem, nigdy na wiszaco
                Scribe.TryRaw(sb.ToString(), 2000);
            }
            catch { }
        }

        /// <summary>Zapis raportu zawieszenia wprost do wlasnego pliku, bez zadnych wspolnych blokad.</summary>
        private static void WriteHang(string file, StringBuilder sb)
        {
            if (file == null) return;
            try { System.IO.File.WriteAllText(file, sb.ToString(), Encoding.UTF8); }
            catch { }
        }
    }
}
