using System;
using HarmonyLib;

namespace Armoury
{
    /// <summary>
    /// POWOLNY ZACIAG. Jeff 28.08: "straty nie sa odczuwalne, zaraz sa nowi
    /// rycerze". BannerKings daje notablowi 50-80% szansy DZIENNIE na nowego
    /// ochotnika w slocie (GetDraftEfficiency: baza 0.5 + Power/150 + populacja),
    /// a nasz dlugi rok (168 dni) podwoil liczbe dziennych tickow na rok -
    /// wojsko odrasta blyskawicznie. Mnozymy dzienna szanse przez
    /// VolunteerRegenPercent (domyslnie 50%) - dotyczy WSZYSTKICH: lordow,
    /// notabli i gracza, wiec wojny tez robia sie wolniejsze i strata armii
    /// naprawde boli. Patch na model BK; gdy BK brak - na vanilla.
    /// </summary>
    internal static class SlowMuster
    {
        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                if (Settings.Current.VolunteerRegenPercent >= 100) { Log.Info("SlowMuster: 100% - vanilla tempo, patch spi."); return; }
                var t = Type.GetType("BannerKings.Models.Vanilla.BKVolunteerModel, BannerKings");
                string who = "BannerKings";
                if (t == null)
                {
                    t = Type.GetType("TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel, TaleWorlds.CampaignSystem");
                    who = "vanilla";
                }
                var m = t != null ? AccessTools.Method(t, "GetDailyVolunteerProductionProbability") : null;
                if (m == null) { Log.Info("SlowMuster: nie znalazlem modelu ochotnikow - patch spi."); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(SlowMuster), "SlowPostfix"));
                Log.Info("SlowMuster: zaciag ochotnikow zwolniony do " + Settings.Current.VolunteerRegenPercent
                         + "% dziennej szansy (model: " + who + ").");
            }
            catch (Exception e) { Log.Error("SlowMuster.ApplyAll", e); }
        }

        public static void SlowPostfix(ref float __result)
        {
            try
            {
                int pct = Settings.Current.VolunteerRegenPercent;
                if (pct >= 100 || pct < 0) return;
                __result *= pct / 100f;
            }
            catch { }
        }
    }
}
