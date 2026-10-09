using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace Armoury
{
    /// <summary>
    /// 177: linia o stali w podpowiedzi przedmiotu - w obu oknach gry: podpowiedz (RefreshItemTooltip - kuznia, mapa) i panel
    /// przedmiotu na ekranie ekwipunku i targu (ItemMenuVM.SetItem - tam gra nie wola RefreshItemTooltip; krytyka 177 pkt 10).
    /// Osobne postfiksy obok TooltipCondition (ten sam cel, niezalezne).
    /// </summary>
    internal static class SteelTooltip
    {
        /// <summary>(nazwa wiersza, tresc) albo null - nic do dopisania.</summary>
        internal static string[] LineFor(ItemObject item)
        {
            try
            {
                if (item == null) return null;
                if (item == Recipes.MaterialItem(CraftingMaterials.Iron6))
                    return new[] { "Steel", "The finest steel of the castle forges - good steel, but not Valyrian." };
            }
            catch { }
            return null;
        }
    }

    [HarmonyPatch(typeof(TooltipRefresherCollection), "RefreshItemTooltip")]
    internal static class SteelTooltipPatch
    {
        private static void Postfix(PropertyBasedTooltipVM propertyBasedTooltipVM, object[] args)
        {
            try
            {
                var elN = args != null && args.Length > 0 ? args[0] as EquipmentElement? : null;
                if (elN == null || propertyBasedTooltipVM == null) return;
                var line = SteelTooltip.LineFor(elN.Value.Item);
                if (line == null) return;
                propertyBasedTooltipVM.AddProperty(" ", " ");
                propertyBasedTooltipVM.AddProperty("", line[1], 0, TooltipProperty.TooltipPropertyFlags.MultiLine);
            }
            catch (Exception e) { Log.Error("SteelTooltip", e); }
        }
    }

    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuVM), "SetItem")]
    internal static class SteelItemMenuPatch
    {
        private static System.Reflection.FieldInfo _targetField;

        private static void Postfix(TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuVM __instance)
        {
            try
            {
                if (_targetField == null)
                    _targetField = AccessTools.Field(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuVM), "_targetItem");
                var itemVm = _targetField != null ? _targetField.GetValue(__instance) as TaleWorlds.Core.ViewModelCollection.ItemVM : null;
                if (itemVm == null) return;
                var line = SteelTooltip.LineFor(itemVm.ItemRosterElement.EquipmentElement.Item);
                if (line == null) return;
                var list = __instance.TargetItemProperties;
                if (list == null) return;
                list.Add(new TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuTooltipPropertyVM(" ", " ", 0, false, null, null, false));
                list.Add(new TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuTooltipPropertyVM(line[0], line[1], 0, false, null, null, false));
            }
            catch (Exception e) { Log.Error("SteelItemMenu", e); }
        }
    }
}
