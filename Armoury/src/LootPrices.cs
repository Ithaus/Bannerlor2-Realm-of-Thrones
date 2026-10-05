using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// CENA LUPU = STAN (wpis 97; Jeff 05.10: "im bardziej zniszczony, tym nizsza cena" -> "zrob tak"). Spoils of War daje
    /// lupowi stany nominalne 55/40/25/10%, ale ceny 0.20/0.14/0.08/0.03 - miecz "Plundered" sprawny w 55% kosztowal jak zlom.
    /// Na starcie sesji ustawiamy PriceMultiplier modyfikatorow rl_* rowny ich stanowi (jak wszedzie w modzie: cena = stan).
    /// Wrakiem zostaje tylko "Mangled" (rl_looted_heavy_max, 10%) - IsWreck; wraki z bitwy dostaja Mangled.
    /// </summary>
    internal static class LootPrices
    {
        internal static void Apply()
        {
            try
            {
                if (!Settings.Current.LootPriceFollowsCondition) return;
                var setter = AccessTools.PropertySetter(typeof(ItemModifier), "PriceMultiplier");
                var field = setter == null ? AccessTools.Field(typeof(ItemModifier), "<PriceMultiplier>k__BackingField") : null;
                int n = 0;
                foreach (var kv in new[] { new Tuple<string, float>("rl_looted", 0.55f), new Tuple<string, float>("rl_looted_medium", 0.40f),
                                           new Tuple<string, float>("rl_looted_heavy", 0.25f), new Tuple<string, float>("rl_looted_heavy_max", 0.10f) })
                {
                    var m = MBObjectManager.Instance.GetObject<ItemModifier>(kv.Item1);
                    if (m == null) continue;
                    if (setter != null) setter.Invoke(m, new object[] { kv.Item2 });
                    else if (field != null) field.SetValue(m, kv.Item2);
                    else continue;
                    n++;
                }
                Log.Info("LootPrices: cena lupu Spoils = stan (Plundered 55%, Damaged 40%, Battered 25%, Mangled 10%) - " + n + "/4 modyfikatorow.");
            }
            catch (Exception e) { Log.Error("LootPrices", e); }
        }

        /// <summary>Wrak: tylko kowal z materialem albo przetop (ludzie i AI go nie lataja).</summary>
        internal static bool IsWreck(ItemModifier m)
        {
            if (m == null) return false;
            if (m.StringId == "rl_looted_heavy_max") return true;
            return m.PriceMultiplier < 0.1f;
        }
    }
}
