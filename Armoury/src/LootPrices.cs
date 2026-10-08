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

        /// <summary>Wrak: tylko kowal z materialem albo przetop (ludzie i AI go nie lataja).
        /// WRAKI NA ZLOM (paczka 158, decyzja Jeffa 07.10 "wraki ida na zlom"; wylacznik WrecksToScrap): wrakiem jest "Mangled" i KAZDY stan
        /// sztuki <= 0.10 (ConditionScaling.ConditionOf - stan, nie cena: Spoils "Mangled" 0.10, najgorsze stany RBM plate_damage_*,
        /// chain_damage_*, leather_damage_* = 0.1, ktore dotad - przy "< 0.1" - kowale odnawiali). Wylaczone: jak dotad (Mangled albo cena < 0.1).</summary>
        internal static bool IsWreck(ItemModifier m)
        {
            if (m == null) return false;
            if (m.StringId == "rl_looted_heavy_max") return true;
            if (ScrapRule) return ConditionScaling.ConditionOf(m) <= WreckState + 0.0001f;
            return m.PriceMultiplier < 0.1f;
        }

        /// <summary>Paczka 158: stan, do ktorego (wlacznie) sztuka jest wrakiem - 10% (jak "Mangled").</summary>
        internal const float WreckState = 0.10f;

        /// <summary>Paczka 158: regula "wraki na zlom" czynna (WrecksToScrap).</summary>
        internal static bool ScrapRule { get { var s = Settings.Current; return s != null && s.WrecksToScrap; } }

        /// <summary>Paczka 158: kowale miasta nie odnawiaja tej sztuki za monete na zadnej drodze (takze przy wylaczonej regule kowali
        /// SmithMendFromMarket - tam dotad wrak szedl za ulamek wartosci). Wlasne kowadlo gracza i przetop - tak.</summary>
        internal static bool SmithRefuses(ItemModifier m) { return ScrapRule && IsWreck(m); }

        /// <summary>Paczka 158: czesc uprzezy gracza jest wrakiem - modyfikator wraku albo stan w ksiedze zuzycia <= 10%.</summary>
        internal static bool HarnessWreck(ItemModifier m, float cond) { return ScrapRule && (IsWreck(m) || cond <= WreckState * 100f + 0.001f); }

        /// <summary>Napis dla gracza (po angielsku): co jest wrakiem i co z nim zrobic.</summary>
        internal static string WreckWhatEn(int n)
        {
            return n == 1
                ? " wreck (Mangled, or worn to a tenth of its worth or less) is not restored for coin by the town's smiths - a wreck is scrap: melt it down, or mend it at your own anvil with your own materials."
                : " wrecks (Mangled, or worn to a tenth of their worth or less) are not restored for coin by the town's smiths - wrecks are scrap: melt them down, or mend them at your own anvil with your own materials.";
        }
    }
}
