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
                // f18-mcm (recenzja): latka zakladana ZAWSZE - dotad przy 100 (domyslne) z Armoury.settings.xml nie wchodzila i suwak MCM
                // (VolunteerRegenPercent) nic nie zmienial, nawet po restarcie (start procesu nie czyta MCM). SlowPostfix czyta klucz na zywo
                // (McmSettings.Apply co godzine gry) i przy 100 wychodzi bez zmiany wyniku - przy domyslnych tempo jak dotad.
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
                int pct = Settings.Current.VolunteerRegenPercent;
                Log.Info("SlowMuster: latka wpieta (model: " + who + "), klucz VolunteerRegenPercent na zywo - przy starcie " + pct + "%"
                         + (pct >= 100 ? " (vanilla tempo, postfiks nic nie zmienia)." : " dziennej szansy."));
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
