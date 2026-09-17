using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace Armoury
{
    /// <summary>
    /// LICZNIK ZAGNIEZDZENIA MODELI (Jeff 02.09, screen: "Night (vanilla undone)
    /// +1.5, Night -1" - dwa razy za duzo). Modele predkosci i morale sa
    /// w tej grze LANCUCHEM: ROT.CalculateFinalSpeed woła RealisticBannerlord,
    /// ten woła vanillowy Default - a my latamy KAZDA zadeklarowana metode,
    /// wiec nasz postfix biegl 2-3 razy na jedno liczenie i kary sie sumowaly.
    /// Prefix (Priority.First) zlicza wejscie, finalizer (biegnie po wszystkich
    /// postfixach) zdejmuje - kazdy nasz postfix pyta "czy jestem na
    /// NAJBARDZIEJ zewnetrznym poziomie" (Depth == 1) i tylko wtedy dziala.
    /// Trzy osobne liczniki: predkosc koncowa, predkosc bazowa, morale.
    /// </summary>
    internal static class SpeedDepth
    {
        [ThreadStatic] private static int _final;
        [ThreadStatic] private static int _base;
        [ThreadStatic] private static int _morale;
        [ThreadStatic] private static int _siege;      // 17.09: budowa machin (WorldPace.SiegePostfix)

        internal static bool OutermostFinal { get { return _final <= 1; } }
        internal static bool OutermostBase { get { return _base <= 1; } }
        internal static bool OutermostMorale { get { return _morale <= 1; } }
        internal static bool OutermostSiege { get { return _siege <= 1; } }

        public static void FinalPrefix() { _final++; }
        public static Exception FinalFinalizer(Exception __exception) { if (_final > 0) _final--; return __exception; }
        public static void BasePrefix() { _base++; }
        public static Exception BaseFinalizer(Exception __exception) { if (_base > 0) _base--; return __exception; }
        public static void MoralePrefix() { _morale++; }
        public static Exception MoraleFinalizer(Exception __exception) { if (_morale > 0) _morale--; return __exception; }
        public static void SiegePrefix() { _siege++; }
        public static Exception SiegeFinalizer(Exception __exception) { if (_siege > 0) _siege--; return __exception; }

        internal static void ApplyAll(Harmony h)
        {
            int a = Patch(h, typeof(PartySpeedModel), "CalculateFinalSpeed", "FinalPrefix", "FinalFinalizer");
            int b = Patch(h, typeof(PartySpeedModel), "CalculateBaseSpeed", "BasePrefix", "BaseFinalizer");
            int c = Patch(h, typeof(PartyMoraleModel), "GetEffectivePartyMorale", "MoralePrefix", "MoraleFinalizer");
            // 17.09 (Jeff: "strasznie wolno trwa oblezenie"): budowa machin to tez lancuch -
            // RealisticSiegeEventModel wola bazowy DefaultSiegeEventModel, a WorldPace latal
            // oba, wiec suwak 50% wchodzil DWA razy (x0.25) - patrz WorldPace.SiegePostfix
            int d = Patch(h, typeof(SiegeEventModel), "GetConstructionProgressPerHour", "SiegePrefix", "SiegeFinalizer");
            Log.Info("SpeedDepth: licznik zagniezdzenia - predkosc koncowa " + a + ", bazowa " + b + ", morale " + c + ", budowa machin " + d + " modeli (nasze kary licza sie raz).");
        }

        private static int Patch(Harmony h, Type baseType, string method, string prefixName, string finalizerName)
        {
            int done = 0;
            try
            {
                var pre = new HarmonyMethod(typeof(SpeedDepth).GetMethod(prefixName, BindingFlags.Public | BindingFlags.Static)) { priority = Priority.First };
                var fin = new HarmonyMethod(typeof(SpeedDepth).GetMethod(finalizerName, BindingFlags.Public | BindingFlags.Static));
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract || !baseType.IsAssignableFrom(t)) continue;
                        try
                        {
                            var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            if (m == null || m.IsAbstract) continue;
                            h.Patch(m, prefix: pre, finalizer: fin);
                            done++;
                        }
                        catch { }
                    }
                }
            }
            catch (Exception e) { Log.Error("SpeedDepth.Patch(" + method + ")", e); }
            return done;
        }
    }
}
