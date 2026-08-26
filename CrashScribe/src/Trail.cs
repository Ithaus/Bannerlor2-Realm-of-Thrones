using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CrashScribe
{
    /// <summary>
    /// Slad okruchow. Natywny wysyp (0xC0000005) nie zostawia zadnego sladu zarzadzanego -
    /// zaden haczyk na wyjatki go nie zlapie, bo wyjatku nie ma. Jedyne, co dziala, to
    /// zapisywac na biezaco CO ROBIL GRACZ, z natychmiastowym zrzutem na dysk. Gdy gra
    /// padnie, przy nastepnym starcie odczytamy ostatni okruch i bedziemy wiedzieli gdzie.
    /// </summary>
    internal static class Trail
    {
        private static string _file;      // biezacy slad, nadpisywany
        private static string _marker;    // znacznik "gra dziala"
        private static readonly Queue<string> Last = new Queue<string>();
        private static readonly object Gate = new object();
        private const int Keep = 40;

        internal static void Init(string dir)
        {
            try
            {
                _file = Path.Combine(dir, "trail.txt");
                _marker = Path.Combine(dir, "running.marker");
                CheckPreviousSession(dir);
                File.WriteAllText(_marker, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Encoding.UTF8);
                Drop("start", "game started");
            }
            catch { }
        }

        /// <summary>Jesli poprzednia sesja nie zamknela sie czysto, ostatni okruch jest miejscem zgonu.</summary>
        private static void CheckPreviousSession(string dir)
        {
            try
            {
                if (!File.Exists(_marker)) return;      // czysty koniec - znacznik skasowany
                string when = "";
                try { when = File.ReadAllText(_marker).Trim(); } catch { }
                string trail = "";
                try { if (File.Exists(_file)) trail = File.ReadAllText(_file); } catch { }

                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("#####################################################################");
                sb.AppendLine("# THE PREVIOUS SESSION DID NOT CLOSE CLEANLY");
                sb.AppendLine("# The game crashed or was killed. Session began: " + when);
                sb.AppendLine("# Below are the last steps before it died - THE LAST LINE is where it went.");
                sb.AppendLine("#####################################################################");
                sb.Append(string.IsNullOrEmpty(trail) ? "  (trail empty)" + Environment.NewLine : trail);
                sb.AppendLine("#####################################################################");
                Scribe.Raw(sb.ToString());
            }
            catch { }
        }

        internal static void Close()
        {
            try
            {
                Drop("end", "game closed cleanly");
                if (_marker != null && File.Exists(_marker)) File.Delete(_marker);
            }
            catch { }
        }

        /// <summary>Zapis okruchu. Kazdy trafia na dysk od razu - inaczej po wysypie nic z niego nie zostanie.</summary>
        internal static void Drop(string kind, string detail)
        {
            if (_file == null) return;
            lock (Gate)
            {
                try
                {
                    Last.Enqueue("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + kind.PadRight(14) + " " + detail);
                    while (Last.Count > Keep) Last.Dequeue();

                    var sb = new StringBuilder();
                    foreach (var l in Last) sb.AppendLine("  " + l);
                    // pelne nadpisanie + flush - plik ma byc kompletny w kazdej chwili
                    using (var fs = new FileStream(_file, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (var w = new StreamWriter(fs, Encoding.UTF8))
                    { w.Write(sb.ToString()); w.Flush(); fs.Flush(true); }
                }
                catch { }
            }
        }
    }
}
