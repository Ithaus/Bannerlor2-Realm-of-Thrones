using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// VALYRIANSKA STAL (Jeff 30.08). Najwyzszy metal kuzni (vanilla
    /// "Thamaskene Steel", CraftingMaterials.Iron6) to w swiecie ROT stal
    /// valyrianska - i ma byc jej ODPOWIEDNIO trudno:
    ///  - nazwa itemu: "Thamaskene Steel" -> "Valyrian Steel" (wszedzie w UI);
    ///  - wytop 2x drozszy: formula rafinacji dajaca Iron6 pobiera podwojne
    ///    wsady (bylo 2x stal + 1x wegiel, jest 4x stal + 2x wegiel);
    ///  - przetop przedmiotow zwraca najwyzej POLOWE receptury, a stali
    ///    valyrianskiej dodatkowo polowe z polowy (25%) - patrz
    ///    Recipes.SmeltYield ("pancerz koni dawal 18/18/18 - ma byc 9/9/4").
    /// </summary>
    internal static class ValyrianSteel
    {
        /// <summary>Zmiana nazwy Iron6 w zaladowanych itemach - po starcie sesji.</summary>
        internal static void Rename()
        {
            try
            {
                var it = Recipes.MaterialItem(CraftingMaterials.Iron6);
                if (it == null) { Log.Info("ValyrianSteel: brak itemu Iron6 - nazwa zostaje."); return; }
                var cur = it.Name != null ? it.Name.ToString() : "";
                if (cur == "Valyrian Steel") return;
                var setter = AccessTools.PropertySetter(typeof(ItemObject), "Name");
                if (setter == null) { Log.Info("ValyrianSteel: ItemObject.Name bez settera - nazwa zostaje."); return; }
                setter.Invoke(it, new object[] { new TextObject("{=!}Valyrian Steel", null) });
                Log.Info("ValyrianSteel: '" + cur + "' przemianowana na 'Valyrian Steel'.");
            }
            catch (Exception e) { Log.Error("ValyrianSteel.Rename", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel), "GetRefiningFormulas");
                if (m == null) { Log.Info("ValyrianSteel: GetRefiningFormulas nieznaleziona - wytop bez zmian."); return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(ValyrianSteel), "DearRefine"));
                Log.Info("ValyrianSteel: wytop stali valyrianskiej 2x drozszy (podwojne wsady formuly Iron6).");
            }
            catch (Exception e) { Log.Error("ValyrianSteel.ApplyAll", e); }
        }

        /// <summary>Pass-through: formula z wyjsciem Iron6 pobiera podwojne wsady.
        /// BK nie nadpisuje GetRefiningFormulas (sprawdzone w zrodlach), wiec
        /// patch na modelu bazowym lapie jedyna sciezke - bez podwojnego mnozenia.</summary>
        public static IEnumerable<Crafting.RefiningFormula> DearRefine(IEnumerable<Crafting.RefiningFormula> values)
        {
            foreach (var f in values)
            {
                if (f != null && f.Output == CraftingMaterials.Iron6)
                    yield return new Crafting.RefiningFormula(f.Input1, f.Input1Count * 2,
                                                              f.Input2, f.Input2Count * 2,
                                                              f.Output, f.OutputCount,
                                                              f.Output2, f.Output2Count);
                else
                    yield return f;
            }
        }
    }
}
