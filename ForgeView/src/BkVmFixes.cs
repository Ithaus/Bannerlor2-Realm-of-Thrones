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
    /// m109-kuznia: przy rachunku kuzni (Armoury ForgeBillOnScreen TAK) lista BK pusta w obu trybach - skore i len (i reszte
    /// surowcow) pokazuje jeden rzad ForgeBillMixin. BK czyta liste (UpdateMaterials, HasMaterials - First("leather")) tylko
    /// wewnatrz swojego OnRefresh, po wlasnym Clear + Add; czyscimy dopiero po nim (postfiks).
    /// </summary>
    [HarmonyPatch(typeof(CraftingMixin))]
    internal static class BkMaterialsModeFix
    {
        private static void Sync(CraftingMixin m, bool modeOnly)
        {
            try
            {
                bool bill = ForgeBillMixin.Sync(m, modeOnly);   // m109-kuznia: rzad rachunku tego ekranu; true = klucz TAK
                var list = m.CurrentExtraMaterials;
                if (list == null) return;
                if (bill)
                {
                    if (list.Count > 0) list.Clear();
                    return;
                }
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
        private static void AfterRefresh(CraftingMixin __instance) { Sync(__instance, false); }

        [HarmonyPostfix]
        [HarmonyPatch("IsInArmorMode", MethodType.Setter)]
        private static void AfterModeSwitch(CraftingMixin __instance) { Sync(__instance, true); }
    }
}
