using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace CrashScribe
{
    /// <summary>
    /// Tlumacz sladu stosu na ludzki jezyk: ktory mod stoi za bledem.
    /// Kazda ramka dostaje etykiete [NazwaModa], zeby nie trzeba bylo zgadywac.
    /// </summary>
    internal static class Blame
    {
        // assembly (bez rozszerzenia, malymi literami) -> nazwa folderu moda
        private static readonly Dictionary<string, string> AsmToModule =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> Modules = new List<string>();
        private static bool _mapped;

        internal static List<string> LoadedModules()
        {
            Map();
            return Modules;
        }

        /// <summary>Przechodzimy folder Modules i wiazemy kazdy DLL z jego modem.</summary>
        internal static void Map()
        {
            if (_mapped) return;
            _mapped = true;
            try
            {
                var here = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); // Modules/CrashScribe/bin/Win64_Shipping_Client
                var modulesRoot = Path.GetFullPath(Path.Combine(here, "..", "..", ".."));
                if (!Directory.Exists(modulesRoot)) return;

                foreach (var dir in Directory.GetDirectories(modulesRoot))
                {
                    var mod = Path.GetFileName(dir);
                    var bin = Path.Combine(dir, Path.Combine("bin", "Win64_Shipping_Client"));
                    if (!Directory.Exists(bin)) continue;
                    bool any = false;
                    foreach (var dll in Directory.GetFiles(bin, "*.dll"))
                    {
                        var name = Path.GetFileNameWithoutExtension(dll);
                        if (name.StartsWith("TaleWorlds.", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!AsmToModule.ContainsKey(name)) AsmToModule[name] = mod;
                        any = true;
                    }
                    if (any) Modules.Add(mod);
                }
            }
            catch { }
        }

        /// <summary>Do ktorego moda nalezy dany typ.</summary>
        internal static string ModuleOf(Type t)
        {
            try
            {
                if (t == null) return null;
                Map();
                var asm = t.Assembly.GetName().Name;
                string mod;
                if (AsmToModule.TryGetValue(asm, out mod)) return mod;
                if (asm.StartsWith("TaleWorlds.", StringComparison.OrdinalIgnoreCase)) return "Native";
                if (asm.StartsWith("SandBox", StringComparison.OrdinalIgnoreCase) ||
                    asm.StartsWith("StoryMode", StringComparison.OrdinalIgnoreCase)) return "Native";
                return asm;
            }
            catch { return null; }
        }

        internal static string ModuleOfAssemblyName(string asmName)
        {
            Map();
            if (string.IsNullOrEmpty(asmName)) return null;
            string mod;
            if (AsmToModule.TryGetValue(asmName, out mod)) return mod;
            if (asmName.StartsWith("TaleWorlds.", StringComparison.OrdinalIgnoreCase) ||
                asmName.StartsWith("SandBox", StringComparison.OrdinalIgnoreCase) ||
                asmName.StartsWith("StoryMode", StringComparison.OrdinalIgnoreCase)) return "Native";
            return asmName;
        }

        /// <summary>Lista modow, ktore pojawiaja sie w sladzie - od najblizszego bledu.</summary>
        internal static List<string> Culprits(Exception ex)
        {
            var result = new List<string>();
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();   // E8/0 (242): czas przejscia stosu (tylko pomiar)
            try
            {
                var trace = new System.Diagnostics.StackTrace(ex, false);
                foreach (var f in trace.GetFrames() ?? new System.Diagnostics.StackFrame[0])
                {
                    var m = f.GetMethod();
                    if (m == null) continue;
                    var mod = ModuleOf(m.DeclaringType);
                    if (string.IsNullOrEmpty(mod) || mod == "Native") continue;
                    if (mod == "CrashScribe") continue;
                    if (!result.Contains(mod)) result.Add(mod);
                    if (result.Count >= 5) break;
                }
            }
            catch { }
            System.Threading.Interlocked.Increment(ref CulpritCalls);
            System.Threading.Interlocked.Add(ref CulpritTicks, System.Diagnostics.Stopwatch.GetTimestamp() - t0);
            return result;
        }

        // E8/0 (242, projekt etapu 8 v2 rozdz. 3 "E8/0", RD8 K9): bledy pierwszej szansy na dobe (wszystkie / zgloszone / powtorki pominiete przez Scribe /
        // odfiltrowane - bez moda w sladzie) i czas Blame.Culprits; zdarzenie FirstChance przychodzi z dowolnego watku - Interlocked. Tylko pomiar.
        internal static long FcSeen, FcFiltered, FcReported, FcRepeat, CulpritCalls, CulpritTicks;
        private static int _profDay = int.MinValue;

        /// <summary>Recenzja E8/0: nowa kampania albo wczytanie (EconomyAudit.RegisterEvents) - pierwszy takt doby bez linii. Bez tego wczytanie zapisu z tej samej
        /// doby (poprzedni takt = wczoraj) dawalo linie z bledami pierwszej szansy calego wczytywania jako "doba gry" (jak DayProfile.Reset w Armoury).</summary>
        internal static void DayReset() { _profDay = int.MinValue; }

        /// <summary>Linia "Profil CS (E8/0)" za okres od poprzedniego wywolania (takt doby kampanii); tylko gdy poprzednie bylo dobe wczesniej (bez okresow z wczytania).</summary>
        internal static void DayLine(int nowDay)
        {
            long seen = System.Threading.Interlocked.Exchange(ref FcSeen, 0), filt = System.Threading.Interlocked.Exchange(ref FcFiltered, 0);
            long rep = System.Threading.Interlocked.Exchange(ref FcReported, 0), rpt = System.Threading.Interlocked.Exchange(ref FcRepeat, 0);
            long calls = System.Threading.Interlocked.Exchange(ref CulpritCalls, 0), ticks = System.Threading.Interlocked.Exchange(ref CulpritTicks, 0);
            int prev = _profDay;
            _profDay = nowDay;
            if (!Config.Stage8ProfileLog || prev != nowDay - 1) return;
            Scribe.Line("Profil CS (E8/0): dzien " + prev + " | FirstChance " + seen + " (zgloszone " + rep + ", powtorki pominiete " + rpt + ", odfiltrowane bez moda " + filt
                        + ") | Blame.Culprits " + (ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                        + " ms / " + calls + " wywolan.");
        }

        /// <summary>Slad stosu z dopisana nazwa moda przy kazdej ramce.</summary>
        internal static string Annotate(Exception ex)
        {
            try
            {
                var sb = new StringBuilder();
                var trace = new System.Diagnostics.StackTrace(ex, true);
                var frames = trace.GetFrames();
                if (frames == null || frames.Length == 0)
                    return "  " + (ex.StackTrace ?? "(brak sladu)");

                foreach (var f in frames)
                {
                    var m = f.GetMethod();
                    string mod = m != null ? ModuleOf(m.DeclaringType) : null;
                    string sig;
                    if (m == null) sig = "(nieznana metoda)";
                    else
                    {
                        var dt = m.DeclaringType != null ? m.DeclaringType.FullName : "?";
                        sig = dt + "." + m.Name + "(" + Args(m) + ")";
                    }
                    var file = f.GetFileName();
                    string at = "";
                    if (!string.IsNullOrEmpty(file)) at = "   " + Path.GetFileName(file) + ":" + f.GetFileLineNumber();
                    else if (f.GetILOffset() >= 0) at = "   IL+0x" + f.GetILOffset().ToString("X4");

                    sb.AppendLine(string.Format("  [{0,-22}] {1}{2}",
                        string.IsNullOrEmpty(mod) ? "?" : mod, sig, at));
                }
                return sb.ToString();
            }
            catch { return "  " + (ex.StackTrace ?? "(brak sladu)"); }
        }

        private static string Args(MethodBase m)
        {
            try
            {
                var ps = m.GetParameters();
                if (ps.Length == 0) return "";
                var sb = new StringBuilder();
                for (int i = 0; i < ps.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(ps[i].ParameterType.Name);
                }
                return sb.ToString();
            }
            catch { return "..."; }
        }
    }
}
