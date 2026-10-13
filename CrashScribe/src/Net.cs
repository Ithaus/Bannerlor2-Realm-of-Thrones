using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.Library;

namespace CrashScribe
{
    /// <summary>
    /// Siec na bledy. Kazda metoda objetych modow dostaje finalizator Harmony:
    /// jesli poleci wyjatek, spisujemy go w calosci i - jesli tak ustawiono - polykamy,
    /// zeby gra nie wywalila sie do pulpitu przez drobiazg w naszym kodzie.
    /// </summary>
    internal static class Net
    {
        [ThreadStatic] private static bool _inside;
        private static int _wrapped;

        // AUDYT 13.10 (siec): oba finalizatory BEZ parametru "MethodBase __originalMethod". Harmony wyliczal go przy
        // KAZDYM wywolaniu zalatanej metody (ldtoken + MethodBase.GetMethodFromHandle: nowy RuntimeMethodInfo 240 B,
        // zamek i szukanie liniowe po metodach typu), zanim finalizator sprawdzil "__exception == null" - 75-400 ns
        // i smieci GC na kazde wywolanie kazdej metody pod siecia. Metode bierzemy dopiero, gdy wyjatek polecial.
        // UWAGA: "new StackFrame(1, false)" MUSI stac w ciele finalizatora, nie w metodzie pomocniczej - ramka 1 to
        // kopia zastepcza Harmony zalatanej metody (to ona wola finalizator). NoInlining pilnuje, zeby tak zostalo.
        // Nie uzywac __exception.TargetSite ani "pierwszej ramki moda ze sladu": kopia ma DeclaringType == null.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Exception ModFinalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (_inside) return __exception;
            try
            {
                _inside = true;
                MethodBase m = null;
                try { m = Harmony.GetOriginalMethodFromStackframe(new System.Diagnostics.StackFrame(1, false)); } catch { }
                m = Backup(m, __exception);
                string where = Describe(m);
                bool fresh = Scribe.Report("ERROR IN A MOD", __exception, where, null);
                if (fresh && Config.ShowInGame) Notify(m, __exception);
                return Config.SwallowModErrors ? null : __exception;
            }
            catch { return __exception; }
            finally { _inside = false; }
        }

        /// <summary>Newralgiczne miejsca gry: spisujemy, ale NIE polykamy - polkniecie moze zepsuc stan gry.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Exception WatchFinalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (_inside) return __exception;
            try
            {
                _inside = true;
                MethodBase m = null;   // ramka 1 = kopia Harmony - patrz uwaga przy ModFinalizer
                try { m = Harmony.GetOriginalMethodFromStackframe(new System.Diagnostics.StackFrame(1, false)); } catch { }
                Scribe.Report("ERROR IN GAME CODE", __exception, Describe(Backup(m, __exception)), null);
                return __exception;
            }
            catch { return __exception; }
            finally { _inside = false; }
        }

        /// <summary>Zapasowa droga, gdy ramka 1 nie dala metody moda (null albo sama kopia Harmony, bez typu):
        /// ostatnia ramka sladu wyjatku to ta sama kopia zastepcza, ktora wlasnie go lapie - pytamy Harmony o nia.
        /// Gdy i to zawiedzie, zostaje kopia: Describe wypisze jej nazwe ("Typ.Metoda_Patch1"); pelny slad i tak jest w raporcie.</summary>
        private static MethodBase Backup(MethodBase m, Exception ex)
        {
            if (m != null && m.DeclaringType != null) return m;
            try
            {
                var st = new System.Diagnostics.StackTrace(ex, false);
                int n = st.FrameCount;
                if (n > 0)
                {
                    MethodBase o = Harmony.GetOriginalMethodFromStackframe(st.GetFrame(n - 1));
                    if (o != null && o.DeclaringType != null) return o;
                    if (m == null) m = o;
                }
            }
            catch { }
            return m;
        }

        private static void Notify(MethodBase m, Exception ex)
        {
            try
            {
                string mod = m != null ? Blame.ModuleOf(m.DeclaringType) : "?";
                if (string.IsNullOrEmpty(mod)) mod = "?";   // kopia Harmony bez typu (mapowanie zawiodlo)
                InformationManager.DisplayMessage(new InformationMessage(
                    "CrashScribe: error in " + mod + " (" + ex.GetType().Name + ") - details in CrashScribe\\" +
                    Path.GetFileName(Scribe.SessionFile), Colors.Red));
            }
            catch { }
        }

        private static string Describe(MethodBase m)
        {
            try
            {
                if (m == null) return "(nieznana metoda)";
                // sama kopia Harmony (mapowanie zawiodlo): jej nazwa "Typ.Metoda_Patch1" niesie typ - lepsze niz "?"
                if (m.DeclaringType == null) return "[?] " + m.Name;
                var dt = m.DeclaringType != null ? m.DeclaringType.FullName : "?";
                return "[" + Blame.ModuleOf(m.DeclaringType) + "] " + dt + "." + m.Name;
            }
            catch { return "(nieznana metoda)"; }
        }

        // ------------------------------------------------------------ zakladanie sieci

        internal static void WrapModules(Harmony harmony)
        {
            if (!Config.WrapModMethods) return;
            var wanted = new List<string>();
            foreach (var s in (Config.WrapModules ?? "").Split(','))
                if (!string.IsNullOrEmpty(s.Trim())) wanted.Add(s.Trim());

            var fin = new HarmonyMethod(typeof(Net).GetMethod("ModFinalizer",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string asmName;
                try { asmName = asm.GetName().Name; } catch { continue; }
                if (asmName == "CrashScribe") continue;

                var mod = Blame.ModuleOfAssemblyName(asmName);
                if (mod == null || mod == "Native") continue;
                if (wanted.Count > 0 && !wanted.Contains(mod)) continue;

                int before = _wrapped;
                WrapAssembly(harmony, asm, fin);
                Scribe.Line("Net cast over " + mod + " (" + asmName + "): " + (_wrapped - before) + " methods.");
            }
        }

        private static void WrapAssembly(Harmony harmony, Assembly asm, HarmonyMethod fin)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
            catch { return; }

            foreach (var t in types)
            {
                if (t == null) continue;
                try
                {
                    if (t.IsInterface || t.ContainsGenericParameters) continue;
                    // logger i ustawienia pomijamy - inaczej zapetlimy sie przy zapisie bledu
                    if (t.Name == "Log" || t.Name == "Settings" || t.Name == "McmSettings") continue;

                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                BindingFlags.Static | BindingFlags.DeclaredOnly;
                    foreach (var m in t.GetMethods(flags))
                    {
                        if (!Patchable(m)) continue;
                        try { harmony.Patch(m, finalizer: fin); _wrapped++; }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private static bool Patchable(MethodInfo m)
        {
            try
            {
                if (m == null) return false;
                if (m.IsAbstract || m.ContainsGenericParameters) return false;
                if (m.GetMethodBody() == null) return false;
                if (m.DeclaringType == null) return false;
                // trywialne akcesory pomijamy - tylko puchna liczbe lat
                if (m.IsSpecialName && (m.Name.StartsWith("get_") || m.Name.StartsWith("set_") ||
                                        m.Name.StartsWith("add_") || m.Name.StartsWith("remove_"))) return false;
                if (m.Name == "Finalize" || m.Name == "GetHashCode" || m.Name == "Equals" || m.Name == "ToString") return false;
                return true;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------ newralgiczne miejsca gry

        /// <summary>Typy gry warte pilnowania. Nazwy rozwiazujemy lagodnie - brak typu nie jest bledem.</summary>
        private static readonly string[] Hotspots =
        {
            "TaleWorlds.CampaignSystem.TournamentGames.TournamentGame",
            "TaleWorlds.CampaignSystem.TournamentGames.FightTournamentGame",
            "TaleWorlds.CampaignSystem.CampaignBehaviors.TournamentCampaignBehavior",
            "TaleWorlds.CampaignSystem.TournamentGames.TournamentManager",
        };

        internal static void WrapHotspots(Harmony harmony)
        {
            if (!Config.WrapCampaignHotspots) return;
            var fin = new HarmonyMethod(typeof(Net).GetMethod("WatchFinalizer",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));

            int n = 0;
            foreach (var name in Hotspots)
            {
                var t = FindType(name);
                if (t == null) { Scribe.Line("Hotspot type not found (skipping): " + name); continue; }

                // lapiemy tez klasy pochodne z innych modow
                foreach (var impl in Concretes(t))
                {
                    foreach (var m in impl.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                      BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        if (!Patchable(m)) continue;
                        try { harmony.Patch(m, finalizer: fin); n++; } catch { }
                    }
                }
            }
            Scribe.Line("Watch over the game's hotspots: " + n + " methods.");
        }

        private static IEnumerable<Type> Concretes(Type baseType)
        {
            var list = new List<Type>();
            if (!baseType.IsAbstract) list.Add(baseType);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || t.ContainsGenericParameters) continue;
                    if (t != baseType && baseType.IsAssignableFrom(t)) list.Add(t);
                }
            }
            return list;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var t = asm.GetType(fullName, false); if (t != null) return t; }
                catch { }
            }
            return null;
        }

        // ------------------------------------------------------------ bledy zglaszane przez sama gre

        internal static void WrapDebugChannel(Harmony harmony)
        {
            try
            {
                var post = new HarmonyMethod(typeof(Net).GetMethod("OnGameError",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
                var t = typeof(Debug);

                var printError = t.GetMethod("PrintError", BindingFlags.Public | BindingFlags.Static);
                if (printError != null) harmony.Patch(printError, prefix: post);
            }
            catch (Exception e) { Scribe.Report("CrashScribe", e, "WrapDebugChannel", null); }
        }

        internal static void OnGameError(string error, string stackTrace)
        {
            if (_inside) return;
            try
            {
                _inside = true;
                Scribe.Raw(Environment.NewLine +
                           "--- THE GAME REPORTS AN ERROR (" + DateTime.Now.ToString("HH:mm:ss") + ") ---" + Environment.NewLine +
                           "  " + error + Environment.NewLine +
                           (string.IsNullOrEmpty(stackTrace) ? "" : "  " + stackTrace + Environment.NewLine) +
                           GameState.Describe() + Environment.NewLine);
            }
            catch { }
            finally { _inside = false; }
        }
    }
}
