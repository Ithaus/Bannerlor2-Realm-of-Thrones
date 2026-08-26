using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace Armoury
{
    /// <summary>
    /// STATYSTYKI STRZELECKIE W ZAKLADCE CRAFT (Jeff: "nie widac statystyk
    /// lukow, kusz, strzal i beltow"). Podpowiedz Banner Kings wypisuje
    /// sekcje bojowa TYLKO dla przedmiotow z pancerzem - luk, kusza, tarcza
    /// i wiazka strzal pokazywaly wiec sama wage, wartosc i kwit materialowy,
    /// bez ani jednej liczby, po ktorej mozna wybrac wyrob.
    /// Dokladamy wlasna sekcje - te same liczby, ktore widzisz w ekwipunku.
    /// </summary>
    internal static class BowStats
    {
        private static PropertyInfo _pItem, _pDef, _pVal;

        private static ItemObject ItemOf(object vm)
        {
            try
            {
                if (_pItem == null) _pItem = vm.GetType().GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);
                return _pItem != null ? _pItem.GetValue(vm, null) as ItemObject : null;
            }
            catch { return null; }
        }

        private static string Str(object prop, PropertyInfo pi)
        {
            try { return pi != null ? pi.GetValue(prop, null) as string : null; }
            catch { return null; }
        }

        /// <summary>Gdzie zaczyna sie sekcja kucia - wstawiamy sie TUZ PRZED nia.</summary>
        private static int InsertAt(IList<TooltipProperty> list)
        {
            try
            {
                if (_pDef == null) _pDef = typeof(TooltipProperty).GetProperty("DefinitionLabel");
                if (_pVal == null) _pVal = typeof(TooltipProperty).GetProperty("ValueLabel");
                for (int i = 1; i < list.Count; i++)   // [0] to nazwa - ma pusta definicje, ale niepusta wartosc
                {
                    var d = Str(list[i], _pDef);
                    var v = Str(list[i], _pVal);
                    if (string.IsNullOrEmpty(d) && string.IsNullOrEmpty(v)) return i;   // pierwsza pusta linia
                }
            }
            catch { }
            return list.Count;
        }

        private static void Row(IList<TooltipProperty> list, ref int at, string name, object value)
        {
            try { list.Insert(at++, new TooltipProperty(name, Convert.ToString(value), 0, false, TooltipProperty.TooltipPropertyFlags.None)); }
            catch { }
        }

        public static void HintPostfix(object __instance, ref List<TooltipProperty> __result)
        {
            try
            {
                if (__result == null) return;
                var item = ItemOf(__instance);
                if (item == null || !item.HasWeaponComponent) return;
                var w = item.WeaponComponent != null ? item.WeaponComponent.PrimaryWeapon : null;
                if (w == null) return;

                int at = InsertAt(__result);
                var t = item.ItemType;

                __result.Insert(at++, new TooltipProperty(" ", " ", 0, false, TooltipProperty.TooltipPropertyFlags.None));
                __result.Insert(at++, new TooltipProperty(
                    t == ItemObject.ItemTypeEnum.Arrows || t == ItemObject.ItemTypeEnum.Bolts ? "Ammunition" :
                    t == ItemObject.ItemTypeEnum.Shield ? "Shield" : "Weapon",
                    " ", 0, false, TooltipProperty.TooltipPropertyFlags.RundownSeperator));

                if (t == ItemObject.ItemTypeEnum.Bow || t == ItemObject.ItemTypeEnum.Crossbow)
                {
                    Row(__result, ref at, "Damage", w.ThrustDamage);
                    Row(__result, ref at, "Fire Rate", w.ThrustSpeed);
                    Row(__result, ref at, "Accuracy", w.Accuracy);
                    Row(__result, ref at, "Missile Speed", w.MissileSpeed);
                    if (w.WeaponLength > 0) Row(__result, ref at, "Length", w.WeaponLength);
                    Row(__result, ref at, "Draw Skill", w.RelevantSkill != null ? w.RelevantSkill.Name.ToString() : "-");
                }
                else if (t == ItemObject.ItemTypeEnum.Arrows || t == ItemObject.ItemTypeEnum.Bolts)
                {
                    Row(__result, ref at, "Damage", w.MissileDamage);
                    Row(__result, ref at, "In the Quiver", w.MaxDataValue);
                    if (w.WeaponLength > 0) Row(__result, ref at, "Length", w.WeaponLength);
                    if (w.MissileSpeed > 0) Row(__result, ref at, "Missile Speed", w.MissileSpeed);
                }
                else if (t == ItemObject.ItemTypeEnum.Shield)
                {
                    Row(__result, ref at, "Hit Points", w.MaxDataValue);
                    Row(__result, ref at, "Speed", w.SwingSpeed);
                    if (w.WeaponLength > 0) Row(__result, ref at, "Size", w.WeaponLength);
                    if (w.Handling > 0) Row(__result, ref at, "Handling", w.Handling);
                }
                else
                {
                    if (w.SwingDamage > 0) Row(__result, ref at, "Swing Damage", w.SwingDamage);
                    if (w.ThrustDamage > 0) Row(__result, ref at, "Thrust Damage", w.ThrustDamage);
                    if (w.SwingSpeed > 0) Row(__result, ref at, "Swing Speed", w.SwingSpeed);
                    if (w.ThrustSpeed > 0) Row(__result, ref at, "Thrust Speed", w.ThrustSpeed);
                    if (w.WeaponLength > 0) Row(__result, ref at, "Length", w.WeaponLength);
                    if (w.Handling > 0) Row(__result, ref at, "Handling", w.Handling);
                }
            }
            catch (Exception e) { Log.Error("BowStats.Hint", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = QuartermasterLaw.FindType("BannerKings.UI.Crafting.ArmorItemVM");
                var m = t != null ? AccessTools.Method(t, "GetHint") : null;
                if (m == null) { Log.Info("BowStats: brak ArmorItemVM.GetHint."); return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(BowStats), "HintPostfix"));
                Log.Info("BowStats: statystyki strzeleckie dopisane do podpowiedzi w zakladce CRAFT.");
            }
            catch (Exception e) { Log.Error("BowStats.ApplyAll", e); }
        }
    }
}
