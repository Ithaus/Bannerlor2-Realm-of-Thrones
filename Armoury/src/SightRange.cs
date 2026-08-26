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
        private static readonly TextObject _txtDay = new TextObject("{=!}Daylight");
        private static readonly TextObject _txtNight = new TextObject("{=!}Darkness (sound carries)");

        public static void Postfix(MobileParty party, ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SightCycleEnabled) return;
                int h = CampaignTime.Now.GetHourOfDay;
                bool night = h >= 21 || h <= 4;
                float f = night ? s.NightSightFactor : s.DaySightFactor;
                if (Math.Abs(f - 1f) > 0.001f) __result.AddFactor(f - 1f, night ? _txtNight : _txtDay);
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
                            harmony.Patch(m, postfix: post);
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
