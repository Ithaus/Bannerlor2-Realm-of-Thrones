using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace CrashScribe
{
    /// <summary>Pisarz. Kazdy blad ma trafic na papier w czytelnej formie, z nazwiskiem winnego moda.</summary>
    internal static class Scribe
    {
        internal static string ReportDir;
        internal static string SessionFile;
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, int> Seen = new Dictionary<string, int>();
        private static int _written;

        internal static void Init()
        {
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                ReportDir = Path.Combine(docs, "Mount and Blade II Bannerlord", "CrashScribe");
                Directory.CreateDirectory(ReportDir);
                SessionFile = Path.Combine(ReportDir, "session-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".log");
                Header();
                Prune();
            }
            catch { /* jesli nie da sie pisac, nie wolno przez to wywalic gry */ }
        }

        private static void Header()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=====================================================================");
            sb.AppendLine(" CrashScribe " + Ver.Text + "   " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("=====================================================================");
            sb.AppendLine();
            sb.AppendLine("--- MODULES FOUND ON DISK ---");
            foreach (var m in Blame.LoadedModules()) sb.AppendLine("  " + m);
            sb.AppendLine();
            Raw(sb.ToString());
        }

        internal static void Raw(string text)
        {
            lock (Gate)
            {
                try { File.AppendAllText(SessionFile, text, Encoding.UTF8); }
                catch { }
            }
        }

        internal static void Line(string text)
        {
            Raw("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
        }

        /// <summary>Pelny raport o bledzie. Zwraca false, jesli to powtorka i pominelismy.</summary>
        internal static bool Report(string kind, Exception ex, string where, string extra)
        {
            try
            {
                if (ex == null) return false;
                string key = kind + "|" + where + "|" + ex.GetType().FullName + "|" + FirstFrame(ex);

                lock (Gate)
                {
                    int n;
                    if (Seen.TryGetValue(key, out n))
                    {
                        Seen[key] = n + 1;
                        // ten sam blad w kolko - notujemy tylko co jakis czas, zeby nie utopic pliku
                        if (n != 4 && n != 24 && n % 250 != 0) return false;
                        Raw(Environment.NewLine + "  (powtorka #" + (n + 1) + " of the same error)" + Environment.NewLine);
                        return true;
                    }
                    Seen[key] = 1;
                    if (_written > Config.MaxReportsPerSession) return false;
                    _written++;
                }

                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("#####################################################################");
                sb.AppendLine("# " + kind + "   " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("#####################################################################");
                if (!string.IsNullOrEmpty(where)) sb.AppendLine("WHERE   : " + where);

                var culprits = Blame.Culprits(ex);
                if (culprits.Count > 0)
                {
                    sb.AppendLine("BLAME   : " + string.Join(" <- ", culprits.ToArray()));
                }
                else sb.AppendLine("BLAME   : (no mod in the trace - suspect the game's own native code)");

                sb.AppendLine("TYPE    : " + ex.GetType().FullName);
                sb.AppendLine("MESSAGE : " + ex.Message);
                if (!string.IsNullOrEmpty(extra)) sb.AppendLine("CONTEXT : " + extra);

                var ctx = GameState.Describe();
                if (!string.IsNullOrEmpty(ctx))
                {
                    sb.AppendLine("--- GAME STATE ---");
                    sb.AppendLine(ctx);
                }

                sb.AppendLine("--- STACK ---");
                sb.AppendLine(Blame.Annotate(ex));

                // Plytki slad (1-2 ramki) = ktos rzuca i lyka wyjatek w miejscu -
                // sam StackTrace wyjatku NIE pokazuje wtedy WOLAJACEGO. Dorzucamy
                // zywy stos biezacego watku, zeby nazwac spamera po imieniu
                // (patrz: 13 tys. TargetException/sesje bez zadnego winowajcy).
                try
                {
                    var exSt = ex.StackTrace;
                    bool shallow = string.IsNullOrEmpty(exSt) || exSt.Split('\n').Length <= 2;
                    if (shallow)
                    {
                        sb.AppendLine("--- CALLER (zywy stos watku) ---");
                        var live = Environment.StackTrace.Split('\n');
                        int printed = 0;
                        for (int i = 0; i < live.Length && printed < 14; i++)
                        {
                            var ln = live[i].Trim();
                            if (ln.Length == 0) continue;
                            // ramki samego Scribe'a i infrastruktury wyjatkow pomijamy
                            if (ln.Contains("CrashScribe.") || ln.Contains("System.Environment")
                                || ln.Contains("FirstChanceException") || ln.Contains("AppDomain.OnFirstChance")) continue;
                            sb.AppendLine("  " + ln);
                            printed++;
                        }
                    }
                }
                catch { }

                int depth = 0;
                var inner = ex.InnerException;
                while (inner != null && depth++ < 8)
                {
                    sb.AppendLine("--- INNER (" + depth + ") " + inner.GetType().FullName + " ---");
                    sb.AppendLine(inner.Message);
                    sb.AppendLine(Blame.Annotate(inner));
                    inner = inner.InnerException;
                }

                var rtle = ex as ReflectionTypeLoadException;
                if (rtle != null && rtle.LoaderExceptions != null)
                {
                    sb.AppendLine("--- LOADER EXCEPTIONS ---");
                    foreach (var le in rtle.LoaderExceptions)
                        if (le != null) sb.AppendLine("  " + le.GetType().Name + ": " + le.Message);
                }

                sb.AppendLine("#####################################################################");
                Raw(sb.ToString());
                return true;
            }
            catch { return false; }
        }

        private static string FirstFrame(Exception ex)
        {
            try
            {
                var st = ex.StackTrace;
                if (string.IsNullOrEmpty(st)) return "";
                int nl = st.IndexOf('\n');
                return (nl > 0 ? st.Substring(0, nl) : st).Trim();
            }
            catch { return ""; }
        }

        /// <summary>Podsumowanie na koniec - ile razy co poszlo nie tak.</summary>
        internal static void Summary()
        {
            try
            {
                lock (Gate)
                {
                    if (Seen.Count == 0) { Raw(Environment.NewLine + "Session ended with no errors." + Environment.NewLine); return; }
                    var sb = new StringBuilder();
                    sb.AppendLine();
                    sb.AppendLine("============================= SUMMARY ===============================");
                    foreach (var kv in Seen) sb.AppendLine(string.Format("{0,6} x  {1}", kv.Value, kv.Key));
                    sb.AppendLine("=====================================================================");
                    Raw(sb.ToString());
                }
            }
            catch { }
        }

        /// <summary>Stare raporty kasujemy, zeby folder nie puchl.</summary>
        private static void Prune()
        {
            try
            {
                var files = new List<FileInfo>();
                foreach (var f in Directory.GetFiles(ReportDir, "session-*.log")) files.Add(new FileInfo(f));
                files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                for (int i = Config.KeepSessions; i < files.Count; i++)
                { try { files[i].Delete(); } catch { } }
            }
            catch { }
        }
    }

    internal static class Ver { internal const string Text = "v1.3"; }
}
