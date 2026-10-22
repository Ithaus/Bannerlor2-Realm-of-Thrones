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
        // m105-drobne (8a): gra sie zamyka (OnSubModuleUnloaded / ProcessExit) - watek glowny przestaje bic na dobre, wiec straznik konczy czuwanie bez raportu.
        // Test 102: hang-2026-10-21_15-35-43.log "main thread silent 61 s, no managed frames, outside a campaign" 62 s po "WYJSCIE: QuitGame" (falszywe zawieszenie).
        private static volatile bool _closing;

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

        /// <summary>m105-drobne (8a): gra sie zamyka - Module.FinalizeModule wola OnSubModuleUnloaded PO Game.OnFinalize i zapisie BannerlordConfig (zawieszenie
        /// w konczeniu kampanii albo w zapisie nadal daje raport), potem silnik sprzata bez ticku aplikacji. Od tej chwili straznik nie robi Suspend i nie pisze
        /// hang-*.log. Wolane z OnSubModuleUnloaded i ProcessExit (pierwsze wygrywa). Nie rzuca.</summary>
        internal static void Closing(string why)
        {
            if (_closing) return;
            _closing = true;
            if (!_started) return;
            try { Scribe.Line("Straznik zawieszen: gra sie zamyka (" + why + ") - koniec czuwania, bez raportu zawieszenia (przy wyjsciu watek glowny przestaje bic, ostatnie bicie " + QuietNow() + " s temu)."); }
            catch { }
        }

        private static string QuietNow()
        {
            try
            {
                long last = Interlocked.Read(ref _lastBeat);
                return ((int)(DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc)).TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return "?"; }
        }

        private static void Loop()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(2000);
                    if (_closing) return;   // m105-drobne (8a): gra sie zamyka - koniec czuwania
                    long last = Interlocked.Read(ref _lastBeat);
                    double quiet = (DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc)).TotalSeconds;

                    int need = InMission() ? StallSeconds : QuietStallSeconds;
                    if (quiet < need) { _dumpedThisStall = false; continue; }
                    if (_dumpedThisStall || _dumps >= MaxDumps || _closing) continue;
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
                if (Autotest.Active)
                    Autotest.NoteOffThread("ZAWIESZENIE: watek glowny milczy " + (int)quietSeconds + " s - stos w " + (hangFile != null ? System.IO.Path.GetFileName(hangFile) : "hang-*.log") + Autotest.QuitHint());
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
