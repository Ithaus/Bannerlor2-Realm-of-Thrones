using System;
using System.Collections.Generic;
using BannerKings.UI.Crafting;
using HarmonyLib;
using TaleWorlds.Library;

namespace ForgeView
{
    /// <summary>
    /// ZNANE WZORY NA GORZE LISTY - ZAWSZE (Jeff 26.08: "czemu lista pokazuje
    /// najpierw niedostepne? najpierw dostepne, zeby nie zjezdzac na dol").
    /// Nasz filtr (ArmourFilterMixin.Apply) juz ustawia znane przed zamknietymi,
    /// ale sortownik BK (ArmorCraftingSortController.SortByCurrentState - przyciski
    /// Type/Name/Yield) sortuje CALA liste od nowa i miesza grupy. Postfix po
    /// kazdym sortowaniu BK przegrupowuje stabilnie: znane najpierw, porzadek
    /// sortowania zachowany W OBREBIE obu grup.
    /// </summary>
    [HarmonyPatch(typeof(ArmorCraftingSortController), "SortByCurrentState")]
    internal static class SortKnownFirst
    {
        private static void Postfix(ArmorCraftingSortController __instance)
        {
            try
            {
                var f = AccessTools.Field(typeof(ArmorCraftingSortController), "_listToControl");
                var list = f != null ? f.GetValue(__instance) as MBBindingList<ArmorItemVM> : null;
                if (list == null || list.Count < 2) return;
                var open = new List<ArmorItemVM>();
                var shut = new List<ArmorItemVM>();
                foreach (var vm in list)
                {
                    if (vm != null && vm.Item != null && !ArmourFilterMixin.Known(vm.Item)) shut.Add(vm);
                    else open.Add(vm);
                }
                if (shut.Count == 0 || open.Count == 0) return;   // nic do przegrupowania
                list.Clear();
                foreach (var vm in open) list.Add(vm);
                foreach (var vm in shut) list.Add(vm);
            }
            catch (Exception e) { Log.Error("SortKnownFirst", e); }
        }
    }
}
