using System;
using HarmonyLib;

namespace Armoury
{
    /// <summary>
    /// JEDNO ZRODLO ZIMY (Jeff 04.10, po audycie: "jedno zrodlo zimy"). Przy wieloletniej zimie
    /// (WesterosClimate) kary mnozyly sie: plony wsi - nasz WinterBite (-50%, polnoc mocniej) x BEE
    /// SeasonalityProfile.VillageProductionMult (zima 0.7) = 0.35; jedzenie partii - WinterBite (+50%)
    /// x RBL WinterFoodPenalty (+25%); ceny jedzenia - nasza podaz plus BEE FoodPriceBias (+20%);
    /// karawany - RBL WinterSpeedPenalty (-20%, wszystkie partie) x BEE CaravanSpeedMult (0.8).
    ///
    /// Zostaje WinterBite (zna gradient polnocy) i RBL dla rzeczy, ktorych nic nie dubluje
    /// (szybkosc partii, morale, leczenie, dobrobyt). BEE pory roku - neutralne dla plonow, cen
    /// jedzenia i szybkosci karawan (przyrost chlopow BEE zostaje). RBL WinterFoodPenalty -> 0
    /// w jego pliku ustawien MCM, BEE WinterFoodConsumptionMult -> 1.0 w jego XML (CHANGELOG).
    /// </summary>
    internal static class WinterSource
    {
        private static bool On { get { var s = Settings.Current; return s != null && s.SingleWinterSource; } }

        public static bool NeutralMult(ref float __result)
        {
            if (!On) return true;
            __result = 1f;
            return false;
        }

        public static bool NeutralBias(ref float __result)
        {
            if (!On) return true;
            __result = 0f;
            return false;
        }

        // wstawiane przez transpiler WesterosClimate w klasach BEE (male funkcje JIT moze wkleic - prefix nie wystarczy).
        // Bez wylacznika: dokladnie wartosci BEE (SeasonalityProfile.cs).
        public static float ProdMult(int s) { if (On) return 1f; return s == 0 ? 1.05f : s == 1 ? 1.1f : s == 2 ? 1.25f : s == 3 ? 0.7f : 1f; }
        public static float FoodBias(int s) { if (On) return 0f; return s == 0 ? 0.05f : s == 1 ? 0f : s == 2 ? -0.08f : s == 3 ? 0.2f : 0f; }
        public static float CaravanMult(int s) { if (On) return 1f; return s == 3 ? 0.8f : s == 2 ? 0.95f : 1f; }

        internal static void ApplyAll(Harmony h)
        {
            int n = 0;
            try
            {
                var t = AccessTools.TypeByName("BetterEconomy.Config.SeasonalityProfile");
                if (t != null)
                {
                    foreach (var name in new[] { "VillageProductionMult", "CaravanSpeedMult" })
                    {
                        var m = AccessTools.Method(t, name);
                        if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(WinterSource), nameof(NeutralMult))); n++; }
                    }
                    var b = AccessTools.Method(t, "FoodPriceBias");
                    if (b != null) { h.Patch(b, prefix: new HarmonyMethod(typeof(WinterSource), nameof(NeutralBias))); n++; }
                }
            }
            catch (Exception e) { Log.Error("WinterSource.ApplyAll", e); }
            Log.Info("WinterSource: jedno zrodlo zimy - pory roku BEE neutralne w " + n + "/3 funkcjach (plony, ceny jedzenia, karawany); zima = WinterBite.");
        }
    }
}
