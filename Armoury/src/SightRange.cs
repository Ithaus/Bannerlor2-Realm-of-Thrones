using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// WZROK ZA SLONCEM (Jeff): za dnia kazda partia widzi dalej, w nocy
    /// krocej - ale ciecie nocne jest lagodne, bo w ciemnosci SLYCHAC wiecej
    /// (maszerujaca kolumna halasuje). Postfix na GetPartySpottingRange we
    /// wszystkich modelach widocznosci (RBM/ROT moga podmieniac model).
    /// </summary>
    internal static class SightRange
    {
        // audyt 04.10 (AUDYT-KONFLIKTY W2): modele z innych modow (Naval, ROT, BKROT) wolaja model wewnetrzny - bez licznika efekt szedl raz na kazdy model lancucha; liczymy raz, na zewnatrz
        [ThreadStatic] private static int _depth;
        public static void DepthPrefix() { _depth++; }
        public static Exception DepthFinalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }

        private static readonly TextObject _txtDay = new TextObject("{=!}Daylight");
        private static readonly TextObject _txtNight = new TextObject("{=!}Darkness (sound carries)");

        public static void Postfix(MobileParty party, ref ExplainedNumber __result)
        {
            try
            {
                if (_depth > 1) return;
                var s = Settings.Current;
                if (s == null || !s.SightCycleEnabled) return;
                // m97-oboz: noc wzroku = noc gry (model czasu: SunSet 22 / SunRise 2, ROT te same) - jedna noc dla kary predkosci, wzroku i obozu (bylo 21-4:59)
                bool night = CampaignTime.Now.IsNightTime;
                float f = night ? s.NightSightFactor : s.DaySightFactor;
                if (Math.Abs(f - 1f) > 0.001f) __result.AddFactor(f - 1f, night ? _txtNight : _txtDay);
                if (night)
                {
                    // R3-10a (NightScout): zwiadowca oddaje czesc nocnego ciecia - czynnik nocy + (1 - czynnik) x min(0.5, Zwiad/400)
                    float sc = NightScout.SightFactor(party, f);
                    if (sc > 0.0005f) __result.AddFactor(sc, NightScout.SightLabel);
                }
            }
            catch { }
        }

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var post = new HarmonyMethod(typeof(SightRange).GetMethod("Postfix",
                    BindingFlags.Public | BindingFlags.Static)) { priority = Priority.Last };
                int done = 0;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract || !typeof(MapVisibilityModel).IsAssignableFrom(t)) continue;
                        try
                        {
                            var m = t.GetMethod("GetPartySpottingRange", BindingFlags.Public | BindingFlags.NonPublic |
                                                                          BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            if (m == null || m.IsAbstract) continue;
                            harmony.Patch(m, prefix: new HarmonyMethod(typeof(SightRange), nameof(DepthPrefix)) { priority = Priority.First }, postfix: post, finalizer: new HarmonyMethod(typeof(SightRange), nameof(DepthFinalizer)));
                            done++;
                        }
                        catch { }
                    }
                }
                Log.Info("Wzrok za sloncem: dzien x" + Settings.Current.DaySightFactor + ", noc x"
                         + Settings.Current.NightSightFactor + " (" + done + " modeli).");
            }
            catch (Exception e) { Log.Error("SightRange.ApplyAll", e); }
        }
    }
}
