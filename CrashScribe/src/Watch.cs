using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace CrashScribe
{
    /// <summary>
    /// Okruchy sladu w miejscach, ktore najczesciej koncza sie wysypem: ekrany, menu,
    /// zapis ustawien w MCM, wejscie w misje. Wszystko przez lagodne szukanie metod -
    /// brak metody to nie blad, tylko jeden okruch mniej.
    /// </summary>
    internal static class Watch
    {
        internal static void Install(Harmony h)
        {
            Try(h, typeof(ScreenManager), "PushScreen", "ScreenPushed");
            Try(h, typeof(ScreenManager), "PopScreen", "ScreenPopped");
            TryByName(h, "MCM.Implementation.DefaultSettingsProvider", "SaveSettings", "McmSave");
            TryByName(h, "MCM.Implementation.DefaultSettingsProvider", "ResetSettings", "McmReset");
            // 1.4.8: menu przelacza statyczny GameMenu.SwitchToMenu(menuId);
            // GameMenuManager.SwitchToMenu nie istnieje (okruch byl martwy)
            TryByName(h, "TaleWorlds.CampaignSystem.GameMenus.GameMenu", "SwitchToMenu", "Menu");
            TryByName(h, "TaleWorlds.MountAndBlade.Mission", "AfterStart", "MissionStart");
        }

        // ---- okruchy ----
        internal static void ScreenPushed(ScreenBase screen)
        { Trail.Drop("screen", "opened " + Name(screen)); }

        // PopScreen() nie ma parametrow - stary prefix z (ScreenBase screen)
        // nie pasowal i co sesje sypal 8x "Parameter screen not found",
        // a okruch nigdy nie dzialal. Prefix biegnie PRZED zdjeciem, wiec
        // zamykany ekran to TopScreen.
        internal static void ScreenPopped()
        { Trail.Drop("screen", "closed " + Name(ScreenManager.TopScreen)); }

        internal static void McmSave(object __instance)
        { Trail.Drop("MCM", "SAVED settings: " + SettingsName(__instance)); }

        internal static void McmReset(object __instance)
        { Trail.Drop("MCM", "RESET settings: " + SettingsName(__instance)); }

        internal static void Menu(string menuId)
        { Trail.Drop("menu", menuId ?? "?"); }

        internal static void MissionStart()
        { Trail.Drop("mission", "mission start"); }

        private static string Name(ScreenBase s)
        { try { return s == null ? "(null)" : s.GetType().Name; } catch { return "?"; } }

        private static string SettingsName(object provider)
        {
            try
            {
                if (provider == null) return "(null)";
                return provider.GetType().Name;
            }
            catch { return "?"; }
        }

        // ---- lagodne zakladanie lat ----
        private static void Try(Harmony h, Type t, string method, string hook)
        {
            if (t == null) { Scribe.Line("Breadcrumb skipped (no such type): " + method); return; }
            Patch(h, t, method, hook);
        }

        private static void TryByName(Harmony h, string typeName, string method, string hook)
        {
            Type t = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            { try { t = asm.GetType(typeName, false); if (t != null) break; } catch { } }
            if (t == null) { Scribe.Line("Breadcrumb skipped (no such type " + typeName + ")"); return; }
            Patch(h, t, method, hook);
        }

        private static void Patch(Harmony h, Type t, string method, string hook)
        {
            try
            {
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                var target = t.GetMethod(method, flags);
                if (target == null || target.IsAbstract || target.GetMethodBody() == null)
                { Scribe.Line("Breadcrumb skipped (no such method): " + t.Name + "." + method); return; }

                var pre = typeof(Watch).GetMethod(hook, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                // dopasowujemy sygnature: jesli patch nie pasuje, Harmony rzuci - lapiemy i pomijamy
                h.Patch(target, prefix: new HarmonyMethod(pre));
                Scribe.Line("Breadcrumb set: " + t.Name + "." + method);
            }
            catch (Exception e)
            { Scribe.Line("Breadcrumb skipped (" + t.Name + "." + method + "): " + e.Message); }
        }
    }
}
