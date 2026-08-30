using System;
using System.Text;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace Armoury
{
    /// <summary>
    /// OKNO WYNIKU KUCIA 1:1 Z BRONIA (Jeff 29.08, screeny: "chce IDENTYCZNY
    /// panel jak Weapon Crafted!, nie tekstowe okienko"). Ladujemy VANILLOWY
    /// prefab NewCraftedWeaponPopup wlasna warstwa Gauntlet, z vanillowym
    /// WeaponDesignResultPopupVM: model 3D przedmiotu, kolumna statow
    /// z roznicami od jakosci, przycisk Done. Gdy cokolwiek z tej maszynerii
    /// odmowi - stare okienko tekstowe robi za kolo zapasowe.
    /// </summary>
    internal static class CraftPopup
    {
        /// <summary>Vanillowy RefreshUsages pada na _crafting==null (nasze
        /// popupy nie ida przez projektanta broni). Prefix: bez craftingu
        /// budujemy zakladki uzyc wprost z przedmiotu; vanilla - po staremu.</summary>
        internal static void ApplyAll(HarmonyLib.Harmony h)
        {
            try
            {
                var m = HarmonyLib.AccessTools.Method(typeof(WeaponDesignResultPopupVM), "RefreshUsages");
                if (m == null) { Log.Info("CraftPopup: brak RefreshUsages - panel moze padac na fallback."); return; }
                h.Patch(m, prefix: new HarmonyLib.HarmonyMethod(typeof(CraftPopup), "RefreshUsagesPrefix"));
                // Done na panelu wola ExecuteFinalizeCrafting, ktore od razu robi
                // _crafting.SetCraftedWeaponName - u nas _crafting==null -> NRE
                // i CTD (crash-original 29.08 14:54, dowod w stacku). Prefix:
                // bez craftingu tylko zamykamy panel.
                var fin = HarmonyLib.AccessTools.Method(typeof(WeaponDesignResultPopupVM), "ExecuteFinalizeCrafting");
                if (fin != null)
                    h.Patch(fin, prefix: new HarmonyLib.HarmonyMethod(typeof(CraftPopup), "FinalizePrefix"));
                Log.Info("CraftPopup: panel wyniku kucia uzbrojony (usages + Done bez craftingu).");
            }
            catch (Exception e) { Log.Error("CraftPopup.ApplyAll", e); }
        }

        public static bool RefreshUsagesPrefix(WeaponDesignResultPopupVM __instance)
        {
            try
            {
                var tr = HarmonyLib.Traverse.Create(__instance);
                if (tr.Field("_crafting").GetValue() != null) return true;   // vanilla droga
                var item = tr.Field("_craftedItem").GetValue<ItemObject>();
                var sel = __instance.SecondaryUsageSelector;
                if (sel == null) return false;
                sel.ItemList.Clear();
                int shown = 0;
                var weapons = item != null ? item.Weapons : null;
                if (weapons != null)
                    for (int i = 0; i < weapons.Count; i++)
                    {
                        if (!TaleWorlds.CampaignSystem.ViewModelCollection.CampaignUIHelper.IsItemUsageApplicable(weapons[i])) continue;
                        var name = GameTexts.FindText("str_weapon_usage", weapons[i].WeaponDescriptionId);
                        sel.AddItem(new CraftingSecondaryUsageItemVM(name, shown, i, sel));
                        shown++;
                    }
                sel.SelectedIndex = shown > 0 ? 0 : -1;
                return false;
            }
            catch (Exception e) { Log.Error("CraftPopup.RefreshUsagesPrefix", e); return false; }
        }

        public static bool FinalizePrefix(WeaponDesignResultPopupVM __instance)
        {
            try
            {
                var tr = HarmonyLib.Traverse.Create(__instance);
                if (tr.Field("_crafting").GetValue() != null) return true;   // vanilla droga
                var onFin = tr.Field("_onFinalize").GetValue<Action>();
                if (onFin != null) onFin();   // nasze Close()
            }
            catch (Exception e) { Log.Error("CraftPopup.FinalizePrefix", e); Close(); }
            return false;
        }

        private sealed class RootVM : ViewModel
        {
            private WeaponDesignResultPopupVM _popup;
            public RootVM(WeaponDesignResultPopupVM popup) { _popup = popup; }
            [DataSourceProperty]
            public WeaponDesignResultPopupVM CraftingResultPopup
            {
                get { return _popup; }
                set { if (value != _popup) { _popup = value; OnPropertyChangedWithValue(value, "CraftingResultPopup"); } }
            }
        }

        private static GauntletLayer _layer;
        private static object _movie;
        private static RootVM _root;

        internal static void Show(ItemObject item, ItemModifier mod, int made)
        {
            try
            {
                if (item == null || !Settings.Current.CraftResultPopup) return;
                // TYLKO RAZ (Jeff 30.08, seria Albion I-IV: "po co drugi raz
                // popup - i Done nie dziala"). Odbior kilku wyrobow naraz
                // wolal Show per sztuka: kazdy kolejny NADPISYWAL warstwe
                // poprzedniego bez jej zamkniecia - martwe warstwy trzymaly
                // input i Done klikalo w proznie. Panel juz otwarty = kolejne
                // wyroby ida bez okna (i bez dzwieku), sa w komunikatach i logu.
                if (_layer != null)
                {
                    Log.Info("CraftPopup: panel juz otwarty - " + item.StringId + " wydany bez okna.");
                    return;
                }
                // odglos kucia jak w vanilla (Jeff 29.08: "nie bylo odglosu kucia")
                // - sciezka BK nie idzie przez vanillowy ekran, ktory go gra
                try { TaleWorlds.Engine.SoundEvent.PlaySound2D("event:/ui/crafting/craft_success"); } catch { }
                if (!ShowGauntlet(item, mod)) ShowInquiry(item, mod, made);
            }
            catch (Exception e) { Log.Error("CraftPopup.Show", e); }
        }

        private static bool ShowGauntlet(ItemObject item, ItemModifier mod)
        {
            try
            {
                Close();   // pas bezpieczenstwa: nigdy dwoch warstw naraz
                var visual = new ItemCollectionElementViewModel();
                try { visual.FillFrom(new EquipmentElement(item, mod), null); }
                catch { visual = new ItemCollectionElementViewModel(); }

                Func<CraftingSecondaryUsageItemVM, MBBindingList<WeaponDesignResultPropertyItemVM>> props =
                    delegate { return BuildProps(item, mod); };
                // tytul z JAKOSCIA jak w vanilla ("Fine Albion IV", nie "Albion IV")
                var title = mod != null
                    ? new TextObject("{=!}" + mod.Name + " " + item.Name, null)
                    : item.Name;
                var popup = new WeaponDesignResultPopupVM(item, title, Close, null, null,
                    visual,
                    new MBBindingList<TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemFlagVM>(),
                    props, delegate { });

                // pancerz nie ma zakladek uzyc - liste statow ustawiamy wprost
                try
                {
                    popup.DesignResultPropertyList = BuildProps(item, mod);
                    Log.Info("CraftPopup: panel dla " + item.StringId + " - statow " + popup.DesignResultPropertyList.Count + ".");
                }
                catch { }

                _root = new RootVM(popup);
                _layer = new GauntletLayer("GauntletLayer", 4500);
                _movie = _layer.LoadMovie("NewCraftedWeaponPopup", _root);
                _layer.InputRestrictions.SetInputRestrictions();
                _layer.IsFocusLayer = true;
                ScreenManager.TopScreen.AddLayer(_layer);
                ScreenManager.TrySetFocus(_layer);
                return true;
            }
            catch (Exception e)
            {
                Log.Error("CraftPopup.Gauntlet", e);
                Close();
                return false;
            }
        }

        private static void Close()
        {
            try
            {
                if (_layer != null)
                {
                    _layer.InputRestrictions.ResetInputRestrictions();
                    if (_movie is GauntletMovieIdentifier m) _layer.ReleaseMovie(m);
                    if (ScreenManager.TopScreen != null) ScreenManager.TopScreen.RemoveLayer(_layer);
                }
            }
            catch (Exception e) { Log.Error("CraftPopup.Close", e); }
            _layer = null; _movie = null; _root = null;
        }

        private static void AddProp(MBBindingList<WeaponDesignResultPropertyItemVM> list,
            string label, int baseVal, int modVal)
        {
            if (baseVal <= 0 && modVal <= 0) return;
            list.Add(new WeaponDesignResultPropertyItemVM(
                new TextObject("{=!}" + label, null), modVal, modVal - baseVal, false));
        }

        private static MBBindingList<WeaponDesignResultPropertyItemVM> BuildProps(ItemObject item, ItemModifier mod)
        {
            var list = new MBBindingList<WeaponDesignResultPropertyItemVM>();
            try
            {
                if (item.HasArmorComponent)
                {
                    var a = item.ArmorComponent;
                    AddProp(list, "Head Armor", a.HeadArmor, mod != null ? mod.ModifyArmor(a.HeadArmor) : a.HeadArmor);
                    AddProp(list, "Body Armor", a.BodyArmor, mod != null ? mod.ModifyArmor(a.BodyArmor) : a.BodyArmor);
                    AddProp(list, "Leg Armor", a.LegArmor, mod != null ? mod.ModifyArmor(a.LegArmor) : a.LegArmor);
                    AddProp(list, "Arm Armor", a.ArmArmor, mod != null ? mod.ModifyArmor(a.ArmArmor) : a.ArmArmor);
                }
                var w = item.PrimaryWeapon;
                if (w != null)
                {
                    if (item.ItemType == ItemObject.ItemTypeEnum.Shield)
                        AddProp(list, "Hit Points", w.MaxDataValue, mod != null ? mod.ModifyHitPoints(w.MaxDataValue) : w.MaxDataValue);
                    else if (item.ItemType == ItemObject.ItemTypeEnum.Bow || item.ItemType == ItemObject.ItemTypeEnum.Crossbow)
                    {
                        AddProp(list, "Missile Damage", w.MissileDamage, mod != null ? mod.ModifyDamage(w.MissileDamage) : w.MissileDamage);
                        AddProp(list, "Missile Speed", w.MissileSpeed, mod != null ? mod.ModifyMissileSpeed(w.MissileSpeed) : w.MissileSpeed);
                        AddProp(list, "Accuracy", w.Accuracy, w.Accuracy);
                    }
                    else if (item.ItemType == ItemObject.ItemTypeEnum.Arrows || item.ItemType == ItemObject.ItemTypeEnum.Bolts)
                    {
                        AddProp(list, "Damage", w.MissileDamage, mod != null ? mod.ModifyDamage(w.MissileDamage) : w.MissileDamage);
                        AddProp(list, "Stack Amount", w.MaxDataValue, mod != null ? mod.ModifyStackCount(w.MaxDataValue) : w.MaxDataValue);
                    }
                    else
                    {
                        AddProp(list, "Swing Damage", w.SwingDamage, mod != null ? mod.ModifyDamage(w.SwingDamage) : w.SwingDamage);
                        AddProp(list, "Thrust Damage", w.ThrustDamage, mod != null ? mod.ModifyDamage(w.ThrustDamage) : w.ThrustDamage);
                        AddProp(list, "Swing Speed", w.SwingSpeed, mod != null ? mod.ModifySpeed(w.SwingSpeed) : w.SwingSpeed);
                        AddProp(list, "Handling", w.Handling, mod != null ? mod.ModifySpeed(w.Handling) : w.Handling);
                    }
                }
                list.Add(new WeaponDesignResultPropertyItemVM(
                    new TextObject("{=!}Weight", null), item.Weight, 0f, true));
            }
            catch (Exception e) { Log.Error("CraftPopup.BuildProps", e); }
            return list;
        }

        // ---------------------------------------------------------- kolo zapasowe
        private static void ShowInquiry(ItemObject item, ItemModifier mod, int made)
        {
            try
            {
                var sb = new StringBuilder();
                if (mod != null) sb.AppendLine("Quality: " + mod.Name.ToString());
                foreach (var p in BuildProps(item, mod))
                    sb.AppendLine(p.PropertyLbl + ": " + p.InitialValue
                                  + (Math.Abs(p.ChangeAmount) > 0.01f ? " (" + (p.ChangeAmount > 0 ? "+" : "") + p.ChangeAmount + ")" : ""));
                if (made > 1) sb.AppendLine().Append("Made in a batch of " + made + ".");
                string title = (mod != null ? mod.Name + " " : "") + item.Name;
                InformationManager.ShowInquiry(new InquiryData(title, sb.ToString(),
                    true, false, "Take it", null, null, null), true);
            }
            catch (Exception e) { Log.Error("CraftPopup.Inquiry", e); }
        }
    }
}
