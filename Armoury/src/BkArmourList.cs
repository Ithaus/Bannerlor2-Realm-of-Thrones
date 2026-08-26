using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// Panel kucia zbroi w Banner Kings wrzuca wszystko do jednego worka: jego wewnetrzny
    /// podzial ma tylko cztery pozycje (BodyArmor, Barding, Shield, Ammo), wiec helm, rekawice,
    /// buty i kirys maja ten sam podpis "Body Armors". Do tego nigdzie nie widac poziomu wyrobu.
    /// Nie przepisujemy calego panelu - podmieniamy dwie etykiety, po ktorych i tak sortuje lista.
    /// </summary>
    internal static class BkArmourList
    {
        private static PropertyInfo _itemProp;

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                if (!Settings.Current.TidyBannerKingsArmourList) return;

                var t = FindType("BannerKings.UI.Crafting.ArmorItemVM");
                if (t == null) { Log.Info("Panel zbroi Banner Kings nieobecny - pomijam porzadkowanie listy."); return; }

                _itemProp = t.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);
                if (_itemProp == null) { Log.Error("BkArmourList: brak wlasciwosci Item.", null); return; }

                Hook(h, t, "ItemTypeText", "CategoryPostfix");
                Hook(h, t, "ItemName", "NamePostfix");
            }
            catch (Exception e) { Log.Error("BkArmourList.ApplyAll", e); }
        }

        private static void Hook(Harmony h, Type t, string prop, string postfix)
        {
            try
            {
                var getter = t.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                if (getter == null || getter.GetGetMethod() == null)
                { Log.Info("BkArmourList: brak " + prop + " - pomijam."); return; }

                var m = typeof(BkArmourList).GetMethod(postfix, BindingFlags.Static | BindingFlags.Public);
                h.Patch(getter.GetGetMethod(), postfix: new HarmonyMethod(m));
                Log.Info("BkArmourList: uporzadkowano " + prop + ".");
            }
            catch (Exception e) { Log.Error("BkArmourList.Hook(" + prop + ")", e); }
        }

        private static ItemObject ItemOf(object vm)
        {
            try { return _itemProp != null ? _itemProp.GetValue(vm, null) as ItemObject : null; }
            catch { return null; }
        }

        /// <summary>Prawdziwa kategoria zamiast zbiorczego "Body Armors".</summary>
        public static void CategoryPostfix(object __instance, ref string __result)
        {
            try
            {
                var item = ItemOf(__instance);
                if (item == null) return;
                __result = Category(item);
            }
            catch (Exception e) { Log.Error("CategoryPostfix", e); }
        }

        /// <summary>Poziom wyrobu na poczatku nazwy - lista sortuje po nazwie, wiec poziomy sie grupuja.</summary>
        public static void NamePostfix(object __instance, ref string __result)
        {
            try
            {
                var item = ItemOf(__instance);
                if (item == null || string.IsNullOrEmpty(__result)) return;
                if (__result.Length > 2 && __result[0] == '[') return;      // juz podpisane
                __result = "[" + Roman(Recipes.Grade(item)) + "] " + __result;
                // wzor jeszcze nieodkryty - widac go na polce, ale kowadlo go nie przyjmie
                // (Jeff: "te ktore mozna sa normalnie, a te ktorych nie mozna na szaro");
                // gdy ForgeView wstrzyknal kolorowe warianty wiersza (ArmourListColour),
                // dopisek jest zbedny - ale zostaje jako zapas, gdyby latka prefabu nie weszla
                if (!RangedLore.KnownOf(item) && !ColourOn()) __result += "   - LOCKED";
            }
            catch (Exception e) { Log.Error("NamePostfix", e); }
        }

        private static string Category(ItemObject item)
        {
            switch (item.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor:     return "Helmets";
                case ItemObject.ItemTypeEnum.BodyArmor:     return "Body armour";
                case ItemObject.ItemTypeEnum.LegArmor:      return "Boots and greaves";
                case ItemObject.ItemTypeEnum.HandArmor:     return "Gauntlets";
                case ItemObject.ItemTypeEnum.Cape:          return "Shoulders and cloaks";
                case ItemObject.ItemTypeEnum.HorseHarness:  return "Barding";
                case ItemObject.ItemTypeEnum.Shield:        return "Shields";
                case ItemObject.ItemTypeEnum.Arrows:        return "Arrows";
                case ItemObject.ItemTypeEnum.Bolts:         return "Bolts";
                case ItemObject.ItemTypeEnum.Bow:           return "Bows";
                case ItemObject.ItemTypeEnum.Crossbow:      return "Crossbows";
                case ItemObject.ItemTypeEnum.Horse:         return "Mounts";
                default:                                    return "Other";
            }
        }

        // ---- czy ForgeView.ArmourListColour realnie pokolorowal liste (reflection, bez twardej zaleznosci) ----
        private static FieldInfo _colourApplied;
        private static bool _colourLooked;

        private static bool ColourOn()
        {
            try
            {
                if (!_colourLooked)
                {
                    _colourLooked = true;
                    var t = FindType("ForgeView.ArmourListColour");
                    if (t != null) _colourApplied = t.GetField("Applied", BindingFlags.Public | BindingFlags.Static);
                }
                // pole czytane za kazdym razem: Applied ustawia sie dopiero przy pierwszym
                // zaladowaniu prefabu ArmorCraftingCategory, nie na starcie gry
                return _colourApplied != null && _colourApplied.GetValue(null) is bool b && b;
            }
            catch { return false; }
        }

        private static string Roman(int n)
        {
            switch (n)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                case 5: return "V";
                case 6: return "VI";
                default: return n.ToString();
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            { try { var t = asm.GetType(fullName, false); if (t != null) return t; } catch { } }
            return null;
        }
    }
}
