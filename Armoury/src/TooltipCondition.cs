using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace Armoury
{
    /// <summary>
    /// Tooltip przedmiotu mowi PELNA prawde o zuzyciu - to, o co prosil Jeff:
    /// osobna sekcja "Condition X%" i kazda zmieniona statystyka jako
    /// "baza -> teraz (-roznica)". Zadnego zgadywania, co zabral stan.
    /// </summary>
    [HarmonyPatch(typeof(TooltipRefresherCollection), "RefreshItemTooltip")]
    internal static class TooltipConditionPatch
    {
        private static void Postfix(PropertyBasedTooltipVM propertyBasedTooltipVM, object[] args)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ShowConditionPercent) return;
                var elN = args != null && args.Length > 0 ? args[0] as EquipmentElement? : null;
                if (elN == null) return;
                var el = elN.Value;
                var item = el.Item;
                var m = el.ItemModifier;
                if (item == null || m == null || ArmouryBehavior.NoWear(item)) return;
                float pm = m.PriceMultiplier;
                if (pm >= 0.999f || pm <= 0f) return;
                int pct = Math.Max(1, (int)Math.Round(pm * 100f));

                var vm = propertyBasedTooltipVM;
                vm.AddProperty(" ", " ");
                vm.AddProperty(" ", " ", 0, TooltipProperty.TooltipPropertyFlags.RundownSeperator);
                vm.AddProperty("Condition", pct + "%");

                if (item.HasArmorComponent)
                {
                    var a = item.ArmorComponent;
                    if (item.Type == ItemObject.ItemTypeEnum.HorseHarness)
                        Row(vm, "Horse Armor", a.BodyArmor, el.GetModifiedMountBodyArmor());
                    else
                    {
                        Row(vm, "Head Armor", a.HeadArmor, el.GetModifiedHeadArmor());
                        Row(vm, "Body Armor", a.BodyArmor, el.GetModifiedBodyArmor());
                        Row(vm, "Leg Armor", a.LegArmor, el.GetModifiedLegArmor());
                        Row(vm, "Arm Armor", a.ArmArmor, el.GetModifiedArmArmor());
                    }
                }
                if (item.HasWeaponComponent && item.WeaponComponent.PrimaryWeapon != null)
                {
                    var w = item.WeaponComponent.PrimaryWeapon;
                    Row(vm, "Swing Damage", w.SwingDamage, el.GetModifiedSwingDamageForUsage(0));
                    Row(vm, "Thrust Damage", w.ThrustDamage, el.GetModifiedThrustDamageForUsage(0));
                    Row(vm, "Swing Speed", w.SwingSpeed, el.GetModifiedSwingSpeedForUsage(0));
                    Row(vm, "Thrust Speed", w.ThrustSpeed, el.GetModifiedThrustSpeedForUsage(0));
                    Row(vm, "Missile Damage", w.MissileDamage, el.GetModifiedMissileDamageForUsage(0));
                    Row(vm, "Missile Speed", w.MissileSpeed, el.GetModifiedMissileSpeedForUsage(0));
                    Row(vm, "Hit Points", w.MaxDataValue, el.GetModifiedMaximumHitPointsForUsage(0));
                }
            }
            catch (Exception e) { Log.Error("TooltipCondition", e); }
        }

        /// <summary>Wiersz "24 -> 12 (-12)" - tylko gdy stan cos faktycznie zabral.</summary>
        private static void Row(PropertyBasedTooltipVM vm, string name, int baseVal, int nowVal)
        {
            try
            {
                if (baseVal <= 0 || nowVal == baseVal) return;
                int d = nowVal - baseVal;
                vm.AddProperty(name, baseVal + " → " + nowVal + "  (" + (d > 0 ? "+" : "") + d + ")");
            }
            catch { }
        }
    }

    /// <summary>
    /// Panel przedmiotu w EKWIPUNKU (ten z obrazkiem, w ktorym RBM dopisuje
    /// "RBM Stats") - dopisujemy wlasna sekcje "Armoury Stats":
    /// stan w punktach (3/100) i kazda zmieniona statystyke jako baza -> teraz (-roznica).
    /// </summary>
    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuVM), "SetItem")]
    internal static class ArmouryStatsPatch
    {
        private static System.Reflection.FieldInfo _targetField;

        private static void Postfix(TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuVM __instance)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ShowConditionPercent) return;
                if (_targetField == null)
                    _targetField = AccessTools.Field(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuVM), "_targetItem");
                var itemVm = _targetField != null ? _targetField.GetValue(__instance) as TaleWorlds.Core.ViewModelCollection.ItemVM : null;
                if (itemVm == null) return;
                var el = itemVm.ItemRosterElement.EquipmentElement;
                var item = el.Item;
                if (item == null) return;
                // sekcja pokazuje sie dla KAZDEJ zbroi i broni - nowka ma 100/100 (New);
                // amunicja (strzaly/belty) jest POZA systemem zuzycia - bez sekcji
                if (!item.HasArmorComponent && !item.HasWeaponComponent) return;
                if (ArmouryBehavior.NoWear(item)) return;
                var m = el.ItemModifier;
                float pm = m != null ? m.PriceMultiplier : 1f;
                bool damaged = m != null && pm < 0.999f && pm > 0f;
                int pct = damaged ? Math.Max(1, (int)Math.Round(pm * 100f)) : 100;
                // ta sztuka na Twoim grzbiecie ma WLASNY rachunek w ksiedze zuzycia -
                // i to on jest prawda, a nie etykieta, ktora dochodzi dopiero przy progu
                try
                {
                    var beh = ArmouryBehavior.Instance;
                    float ledger = beh != null ? beh.WornCondition(item) : -1f;
                    if (ledger >= 0f && ledger < pct) { pct = Math.Max(1, (int)Math.Round(ledger)); damaged = pct < 100; }
                }
                catch { }

                var list = __instance.TargetItemProperties;
                if (list == null) return;
                Add(list, " ", " ");
                Add(list, "Armoury Stats", " ");
                Add(list, "Condition", pct + " / 100" + (damaged ? "" : "  (New)"));
                if (item.HasArmorComponent)
                {
                    int pool = ArmouryBehavior.ArmorPool(item);
                    if (pool > 0)
                    {
                        int cur = (int)Math.Round(pool * pct / 100.0);
                        Add(list, "Durability", cur + " / " + pool);
                    }
                }
                if (!damaged) return;   // nowka - bez rozpiski strat

                if (item.HasArmorComponent)
                {
                    var a = item.ArmorComponent;
                    if (item.Type == ItemObject.ItemTypeEnum.HorseHarness)
                        Delta(list, "Horse Armor", a.BodyArmor, el.GetModifiedMountBodyArmor());
                    else
                    {
                        Delta(list, "Head Armor", a.HeadArmor, el.GetModifiedHeadArmor());
                        Delta(list, "Body Armor", a.BodyArmor, el.GetModifiedBodyArmor());
                        Delta(list, "Leg Armor", a.LegArmor, el.GetModifiedLegArmor());
                        Delta(list, "Arm Armor", a.ArmArmor, el.GetModifiedArmArmor());
                    }
                }
                if (item.HasWeaponComponent && item.WeaponComponent.PrimaryWeapon != null)
                {
                    var w = item.WeaponComponent.PrimaryWeapon;
                    Delta(list, "Swing Damage", w.SwingDamage, el.GetModifiedSwingDamageForUsage(0));
                    Delta(list, "Thrust Damage", w.ThrustDamage, el.GetModifiedThrustDamageForUsage(0));
                    Delta(list, "Swing Speed", w.SwingSpeed, el.GetModifiedSwingSpeedForUsage(0));
                    Delta(list, "Thrust Speed", w.ThrustSpeed, el.GetModifiedThrustSpeedForUsage(0));
                    Delta(list, "Missile Damage", w.MissileDamage, el.GetModifiedMissileDamageForUsage(0));
                    Delta(list, "Missile Speed", w.MissileSpeed, el.GetModifiedMissileSpeedForUsage(0));
                    Delta(list, "Hit Points", w.MaxDataValue, el.GetModifiedMaximumHitPointsForUsage(0));
                }
            }
            catch (Exception e) { Log.Error("ArmouryStats", e); }
        }

        private static void Add(
            TaleWorlds.Library.MBBindingList<TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuTooltipPropertyVM> list,
            string name, string value)
        {
            list.Add(new TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuTooltipPropertyVM(name, value, 0, false, null, null, false));
        }

        private static void Delta(
            TaleWorlds.Library.MBBindingList<TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuTooltipPropertyVM> list,
            string name, int baseVal, int nowVal)
        {
            if (baseVal <= 0 || nowVal == baseVal) return;
            int d = nowVal - baseVal;
            Add(list, name, baseVal + " → " + nowVal + "  (" + (d > 0 ? "+" : "") + d + ")");
        }
    }
}
