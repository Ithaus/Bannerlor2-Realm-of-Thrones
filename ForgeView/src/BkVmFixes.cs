using System;
using HarmonyLib;
using BannerKings.UI.Crafting;
using BannerKings.UI.Extensions;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace ForgeView
{
    /// <summary>
    /// Pasek surowcow dodatkowych Banner Kings nie znika w trybie broni - w prefabie brak
    /// IsVisible, a dopisywanie atrybutow do ich wezlow (SetAttribute) zawieszalo ekran.
    /// Robimy to wiec od strony danych: w trybie broni lista kafli jest oprozniana, w trybie
    /// zbroi odtwarzana. Pusty ListPanel niczego nie rysuje - "0 0" przy kuciu miecza znika.
    /// </summary>
    [HarmonyPatch(typeof(CraftingMixin))]
    internal static class BkMaterialsModeFix
    {
        private static void Sync(CraftingMixin m)
        {
            try
            {
                var list = m.CurrentExtraMaterials;
                if (list == null) return;
                if (!m.IsInArmorMode)
                {
                    if (list.Count > 0) list.Clear();
                    return;
                }
                if (list.Count == 0)
                {
                    var leather = MBObjectManager.Instance.GetObject<ItemObject>("leather");
                    var linen = MBObjectManager.Instance.GetObject<ItemObject>("linen");
                    if (leather != null) list.Add(new ExtraMaterialItemVM(leather));
                    if (linen != null) list.Add(new ExtraMaterialItemVM(linen));
                }
            }
            catch (Exception e) { Log.Error("BkMaterialsModeFix.Sync", e); }
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnRefresh")]
        private static void AfterRefresh(CraftingMixin __instance) { Sync(__instance); }

        [HarmonyPostfix]
        [HarmonyPatch("IsInArmorMode", MethodType.Setter)]
        private static void AfterModeSwitch(CraftingMixin __instance) { Sync(__instance); }
    }
}
