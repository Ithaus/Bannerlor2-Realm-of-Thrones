using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// POWOLNE GOJENIE. Jeff 28.08: "regeneracja zdrowia na mapie za szybka,
    /// ma byc dwa razy dluzsza - bonusy z perkow niech dalej dzialaja".
    /// Postfix na dzienne leczenie szeregowych i bohaterow: AddFactor -50%
    /// NA KONCU rachunku, wiec medycyna i perki licza sie normalnie, a dopiero
    /// wynik jest cinany. BannerKings nie podmienia tego modelu (sprawdzone).
    /// </summary>
    internal static class SlowHealing
    {
        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                if (Settings.Current.HealingRegenPercent >= 100) { Log.Info("SlowHealing: 100% - vanilla tempo, patch spi."); return; }
                var t = typeof(DefaultPartyHealingModel);
                var m1 = AccessTools.Method(t, "GetDailyHealingForRegulars");
                var m2 = AccessTools.Method(t, "GetDailyHealingHpForHeroes");
                int patched = 0;
                if (m1 != null) { harmony.Patch(m1, postfix: new HarmonyMethod(typeof(SlowHealing), "SlowPostfix")); patched++; }
                if (m2 != null) { harmony.Patch(m2, postfix: new HarmonyMethod(typeof(SlowHealing), "SlowPostfix")); patched++; }
                Log.Info("SlowHealing: gojenie na mapie zwolnione do " + Settings.Current.HealingRegenPercent
                         + "% (" + patched + "/2 metod)." );
            }
            catch (Exception e) { Log.Error("SlowHealing.ApplyAll", e); }
        }

        public static void SlowPostfix(ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            try
            {
                int pct = Settings.Current.HealingRegenPercent;
                if (pct >= 100 || pct < 0) return;
                __result.AddFactor(pct / 100f - 1f, new TextObject("{=armSlowMend}Wounds knit slowly", null));
            }
            catch { }
        }
    }
}
