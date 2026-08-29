using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PANCERZ TEZ SIE NISZCZY (Jeff 29.08: "mam mase rzeczy w ekwipunku,
    /// a na liscie naprawy tylko bron!"). Twardy dowod z danych: 999 pancerzy
    /// ROT i ZADEN nie ma modifier_group - bez grupy gra nie umie nadac stanu
    /// (dented/rusty/...), wiec pancerz jest wiecznie "100%": zuzycie, lupy,
    /// jency i naprawa widzialy tylko vanillowe bronie. Przy starcie sesji
    /// nadajemy brakujace grupy wg materialu (plate/chain/leather/cloth),
    /// lukom i kuszom bez grupy - bow/crossbow. Od tej chwili caly lancuch
    /// zuzycia i Mending Bench obejmuje pelny rynsztunek.
    /// </summary>
    internal static class WearGroups
    {
        internal static void Fix()
        {
            try
            {
                var mgr = MBObjectManager.Instance;
                var gPlate = mgr.GetObject<ItemModifierGroup>("plate");
                var gChain = mgr.GetObject<ItemModifierGroup>("chain");
                var gLeather = mgr.GetObject<ItemModifierGroup>("leather");
                var gCloth = mgr.GetObject<ItemModifierGroup>("cloth");
                var gBow = mgr.GetObject<ItemModifierGroup>("bow");
                var gXbow = mgr.GetObject<ItemModifierGroup>("crossbow");
                var fGroup = AccessTools.Field(typeof(ItemComponent), "<ItemModifierGroup>k__BackingField");
                if (fGroup == null) { Log.Info("WearGroups: brak pola grupy - odpuszczam."); return; }

                int fixedArm = 0, fixedRng = 0;
                foreach (var it in mgr.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || it.ItemComponent == null) continue;
                    if (it.ItemComponent.ItemModifierGroup != null) continue;
                    if (it.HasArmorComponent)
                    {
                        ItemModifierGroup g;
                        switch (it.ArmorComponent.MaterialType)
                        {
                            case ArmorComponent.ArmorMaterialTypes.Plate: g = gPlate; break;
                            case ArmorComponent.ArmorMaterialTypes.Chainmail: g = gChain; break;
                            case ArmorComponent.ArmorMaterialTypes.Leather: g = gLeather; break;
                            default: g = gCloth; break;
                        }
                        if (g != null) { fGroup.SetValue(it.ItemComponent, g); fixedArm++; }
                    }
                    else if (it.ItemType == ItemObject.ItemTypeEnum.Bow && gBow != null)
                    { fGroup.SetValue(it.ItemComponent, gBow); fixedRng++; }
                    else if (it.ItemType == ItemObject.ItemTypeEnum.Crossbow && gXbow != null)
                    { fGroup.SetValue(it.ItemComponent, gXbow); fixedRng++; }
                }
                if (fixedArm + fixedRng > 0)
                    Log.Info("WearGroups: nadano grupy modyfikatorow " + fixedArm + " pancerzom i "
                             + fixedRng + " lukom/kuszom bez grup (dane ROT).");
            }
            catch (Exception e) { Log.Error("WearGroups.Fix", e); }
        }
    }
}
