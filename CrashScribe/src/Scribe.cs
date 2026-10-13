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

        // --- klucz powtorek (SCRIBE-KLUCZ-POWTOREK, 11.10; wylacznik Config.RepeatKeyByMessage) ---
        // Dawny klucz (rodzaj|miejsce|typ|pierwsza ramka sladu) to teraz RODZINA. Klucz bazowy = rodzina + komunikat
        // znormalizowany; dla FirstChance dochodzi ramka wolajacego z zywego stosu, bo slad wyjatku w chwili pierwszej
        // szansy ma tylko ramke rzutu (ThrowHelper, EmitCallParameter, ReportException) i sklejal rozne bledy calej gry
        // w jedna "powtorke" (session-2026-10-11_14-45-58.log: po countChange BK 6 kolejnych klas BK bez sladu).
        private const int MsgMax = 300;           // dlugosc komunikatu w kluczu (przy 160 przeciazenia z latek Harmony sie sklejaja)
        private const int FrameUntil = 100;       // ramke z zywego stosu liczymy dla pierwszych N zdarzen klucza bazowego (koszt stosu)
        private const int MaxMsgsPerFamily = 20;  // najwyzej tyle roznych komunikatow na rodzine (id w tresci), reszta razem pod (inne)
        private const int MaxFramesPerBase = 5;   // najwyzej tyle roznych wolajacych na klucz bazowy, reszta razem pod (inne)
        private const string Other = "(inne)";
        private sealed class BaseInfo { internal int Count; internal readonly List<string> Frames = new List<string>(); }
        private static readonly Dictionary<string, BaseInfo> Bases = new Dictionary<string, BaseInfo>();
        private static readonly Dictionary<string, int> FamilyMsgs = new Dictionary<string, int>();
        // blad rzucony w trakcie skladania raportu (FirstChance z wnetrza Report) nie wchodzi drugi raz - bez rekurencji
        [ThreadStatic] private static bool _busy;

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

        /// <summary>
        /// Zapis z limitem czekania - dla straznika zawieszen. Gdy Gate trzyma
        /// zamrozony watek glowny, poddajemy sie po timeoutMs zamiast wisiec
        /// razem z nim w nieskonczonosc.
        /// </summary>
        internal static bool TryRaw(string text, int timeoutMs)
        {
            if (!System.Threading.Monitor.TryEnter(Gate, timeoutMs)) return false;
            try
            {
                try { File.AppendAllText(SessionFile, text, Encoding.UTF8); } catch { }
                return true;
            }
            finally { System.Threading.Monitor.Exit(Gate); }
        }

        internal static void Line(string text)
        {
            Raw("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
        }

        /// <summary>Pelny raport o bledzie. Zwraca false, jesli to powtorka i pominelismy.</summary>
        internal static bool Report(string kind, Exception ex, string where, string extra)
        {
            if (ex == null || _busy) return false;
            _busy = true;
            try
            {
                string family = kind + "|" + where + "|" + ex.GetType().FullName + "|" + FirstFrame(ex);
                bool split = Config.RepeatKeyByMessage;
                string msg = split ? Norm(MessageOf(ex)) : null;
                bool fc = split && where == "FirstChanceException";

                lock (Gate)
                {
                    string frame = "";
                    string key = split ? KeyOf(family, msg, fc, out frame) : family;
                    int n;
                    if (Seen.TryGetValue(key, out n))
                    {
                        Seen[key] = n + 1;
                        // ten sam blad w kolko - notujemy tylko co jakis czas, zeby nie utopic pliku
                        if (n != 4 && n != 24 && n % 250 != 0) return false;
                        // z opisem: linia stoi zwykle pod pelnym raportem INNEGO bledu
                        Raw(Environment.NewLine + "  (powtorka #" + (n + 1)
                            + (split ? ": " + RepeatLabel(kind, ex, where, frame) + ")" : " of the same error)") + Environment.NewLine);
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
            finally { _busy = false; }
        }

        /// <summary>Klucz powtorek (wolane pod Gate). Baza = rodzina + komunikat znormalizowany (najwyzej MaxMsgsPerFamily
        /// komunikatow na rodzine); dla FirstChance + ramka wolajacego (najwyzej MaxFramesPerBase na baze). Ramke liczymy
        /// tylko dla pierwszych FrameUntil zdarzen bazy; potem bez kosztu stosu: jedyna znana ramka albo (inne).</summary>
        private static string KeyOf(string family, string msg, bool fc, out string frame)
        {
            frame = "";
            string bkey = family + "|" + msg;
            BaseInfo b;
            if (!Bases.TryGetValue(bkey, out b))
            {
                int m;
                FamilyMsgs.TryGetValue(family, out m);
                if (m < MaxMsgsPerFamily) FamilyMsgs[family] = m + 1;
                else
                {
                    bkey = family + "|" + Other;
                    Bases.TryGetValue(bkey, out b);
                }
                if (b == null) { b = new BaseInfo(); Bases[bkey] = b; }
            }
            b.Count++;
            if (!fc) return bkey;
            if (b.Count <= FrameUntil)
            {
                frame = LiveFrame();
                if (frame.Length > 0 && !b.Frames.Contains(frame))
                {
                    if (b.Frames.Count < MaxFramesPerBase) b.Frames.Add(frame);
                    else frame = Other;
                }
            }
            else frame = b.Frames.Count == 1 ? b.Frames[0] : (b.Frames.Count == 0 ? "" : Other);
            return frame.Length > 0 ? bkey + "|" + frame : bkey;
        }

        /// <summary>Wolajacy z zywego stosu watku (FirstChance): pierwsza ramka moda, a bez niej pierwsza ramka gry.
        /// Pomijamy infrastrukture (System., Microsoft., HarmonyLib., MonoMod., Mono., CrashScribe.) i ramki bez typu
        /// (metody dynamiczne); przyrostek _PatchN latki Harmony obciety. "" gdy nic sie nie nadaje.</summary>
        private static string LiveFrame()
        {
            try
            {
                var frames = new System.Diagnostics.StackTrace(1, false).GetFrames();
                if (frames == null) return "";
                string game = null;
                foreach (var f in frames)
                {
                    var m = f != null ? f.GetMethod() : null;
                    var t = m != null ? m.DeclaringType : null;
                    if (t == null) continue;
                    string tn = t.FullName ?? t.Name ?? "";
                    if (Starts(tn, "System.") || Starts(tn, "Microsoft.") || Starts(tn, "HarmonyLib.")
                        || Starts(tn, "MonoMod.") || Starts(tn, "Mono.") || Starts(tn, "CrashScribe.")) continue;
                    string name = tn + "." + NoPatchSuffix(m.Name);
                    if (Starts(tn, "TaleWorlds.") || Starts(tn, "SandBox.") || Starts(tn, "StoryMode.") || Starts(tn, "ManagedCallbacks."))
                    {
                        if (game == null) game = name;
                        continue;
                    }
                    return name;
                }
                return game ?? "";
            }
            catch { return ""; }
        }

        private static bool Starts(string s, string prefix) { return s.StartsWith(prefix, StringComparison.Ordinal); }

        /// <summary>"Metoda_Patch3" -> "Metoda": ponowne latanie tej samej metody nie zmienia klucza.</summary>
        private static string NoPatchSuffix(string name)
        {
            if (string.IsNullOrEmpty(name)) return name ?? "";
            int p = name.LastIndexOf("_Patch", StringComparison.Ordinal);
            if (p <= 0 || p + 6 >= name.Length) return name;
            for (int k = p + 6; k < name.Length; k++)
                if (name[k] < '0' || name[k] > '9') return name;
            return name.Substring(0, p);
        }

        /// <summary>Komunikat do klucza: kazdy ciag cyfr -> '#'; "0x.." i ciag samych znakow hex z cyfra dlugi na 6+
        /// (adresy, hashe) -> '#' (litery a-f w zwyklych slowach zostaja); biale znaki zwiniete; najwyzej MsgMax znakow.</summary>
        private static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(Math.Min(s.Length, MsgMax) + 8);
            int i = 0, len = s.Length;
            while (i < len && sb.Length < MsgMax)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                    i++;
                    continue;
                }
                if (!char.IsLetterOrDigit(c) && c != '_') { sb.Append(c); i++; continue; }
                int j = i;
                bool digit = false, hex = true;
                while (j < len && (char.IsLetterOrDigit(s[j]) || s[j] == '_'))
                {
                    char d = s[j];
                    if (d >= '0' && d <= '9') digit = true;
                    else if (!IsHex(d)) hex = false;
                    j++;
                }
                bool ox = j - i > 2 && s[i] == '0' && (s[i + 1] == 'x' || s[i + 1] == 'X') && HexOnly(s, i + 2, j);
                if (ox || (digit && hex && j - i >= 6)) sb.Append('#');
                else
                {
                    for (int k = i; k < j; k++)
                    {
                        char d = s[k];
                        if (d >= '0' && d <= '9') { if (k == i || s[k - 1] < '0' || s[k - 1] > '9') sb.Append('#'); }
                        else sb.Append(d);
                    }
                }
                i = j;
            }
            if (sb.Length > MsgMax) sb.Length = MsgMax;
            return sb.ToString();
        }

        private static bool IsHex(char d) { return (d >= '0' && d <= '9') || (d >= 'a' && d <= 'f') || (d >= 'A' && d <= 'F'); }

        private static bool HexOnly(string s, int from, int to)
        {
            for (int k = from; k < to; k++) if (!IsHex(s[k])) return false;
            return true;
        }

        private static string MessageOf(Exception ex)
        {
            try { return ex.Message ?? ""; } catch { return ""; }
        }

        /// <summary>Opis do linii powtorki: RODZAJ | TYP @ wolajacy (albo metoda / miejsce rzutu) - surowy komunikat do 100 znakow.</summary>
        private static string RepeatLabel(string kind, Exception ex, string where, string frame)
        {
            string at;
            if (!string.IsNullOrEmpty(frame) && frame != Other) at = frame;
            else
            {
                at = !string.IsNullOrEmpty(where) && where != "FirstChanceException" ? where : ThrowSite(ex);
                if (frame == Other) at = Other + " " + at;
            }
            return kind + " | " + ex.GetType().FullName + " @ " + at + " - " + Squash(MessageOf(ex), 100);
        }

        /// <summary>Pierwsza ramka sladu bez "at " i bez parametrow.</summary>
        private static string ThrowSite(Exception ex)
        {
            string s = FirstFrame(ex);
            if (s.StartsWith("at ", StringComparison.Ordinal)) s = s.Substring(3);
            int p = s.IndexOf('(');
            return p > 0 ? s.Substring(0, p) : s;
        }

        /// <summary>Jedna linia: biale znaki zwiniete, najwyzej max znakow (dalej "...").</summary>
        private static string Squash(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(Math.Min(s.Length, max) + 4);
            foreach (char c in s)
            {
                if (sb.Length >= max) { sb.Append("..."); break; }
                if (char.IsWhiteSpace(c)) { if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' '); }
                else sb.Append(c);
            }
            return sb.ToString().TrimEnd();
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

                // raporty zawieszen ze straznika - osobny wzorzec, ten sam limit
                var hangs = new List<FileInfo>();
                foreach (var f in Directory.GetFiles(ReportDir, "hang-*.log")) hangs.Add(new FileInfo(f));
                hangs.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                for (int i = Config.KeepSessions; i < hangs.Count; i++)
                { try { hangs[i].Delete(); } catch { } }
            }
            catch { }
        }
    }

    internal static class Ver { internal const string Text = "v1.3"; }
}
