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

        /// <summary>
        /// NAZWA ZGODNA Z REGULA VANILLI (Jeff 15.09: "jak tworze lepsza lub gorsza
        /// wersje, musze recznie kasowac nazwe - za dluga, nie moze byc spacji").
        /// CampaignUIHelper.IsStringApplicableForItemName zada: 3-50 znakow, tylko
        /// litery/cyfry/biale/interpunkcja, bez "{" i "}", bez spacji na brzegach
        /// i BEZ PODWOJNYCH SPACJI. Nazwa vanillowego modyfikatora to szablon
        /// z placeholderem "{ITEMNAME}" ("Masterwork {ITEMNAME}") - sklejane dotad
        /// mod.Name + " " + item.Name zostawialo placeholder pusty, wiec wychodzilo
        /// "Masterwork  Albion" z DWIEMA spacjami, a dlugie nazwy lukow ROT
        /// ("Masterwork 200 Pound Ravens' Teeth Longbow (60%)") przekraczaly 50.
        /// Robimy to jak vanilla: podstawiamy placeholder, scalamy biale znaki,
        /// wycinamy nawiasy klamrowe, przycinamy do 50 na granicy slowa.
        /// </summary>
        internal static string CleanName(ItemObject item, ItemModifier mod)
        {
            string baseName = item != null && item.Name != null ? item.Name.ToString() : "";
            string name = baseName;
            try
            {
                if (mod != null && mod.Name != null)
                {
                    var t = mod.Name.CopyTextObject();
                    t.SetTextVariable("ITEMNAME", baseName);
                    string withMod = t.ToString();
                    // modyfikator bez placeholdera (cudze mody) - doklejamy po staremu
                    name = withMod.Contains(baseName) ? withMod : (mod.Name.ToString() + " " + baseName);
                }
            }
            catch { name = baseName; }
            try
            {
                var sb = new StringBuilder(name.Length);
                bool lastSpace = true;                                   // tnie tez spacje wiodace
                foreach (char ch in name)
                {
                    if (ch == '{' || ch == '}') continue;
                    bool ws = char.IsWhiteSpace(ch);
                    if (ws && lastSpace) continue;
                    sb.Append(ws ? ' ' : ch);
                    lastSpace = ws;
                }
                name = sb.ToString().Trim();
                if (name.Length > 50)
                {
                    int cut = name.LastIndexOf(' ', 49);
                    name = (cut >= 20 ? name.Substring(0, cut) : name.Substring(0, 50)).Trim();
                }
                if (name.Length < 3) name = baseName.Length >= 3 ? baseName : "Crafted item";
            }
            catch { }
            return name;
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
                // tytul z JAKOSCIA jak w vanilla ("Fine Albion IV", nie "Albion IV") -
                // ale ZGODNY z regula nazw vanilli, patrz CleanName
                string clean = CleanName(item, mod);
                var title = new TextObject("{=!}" + clean, null);
                var popup = new WeaponDesignResultPopupVM(item, title, Close, null, null,
                    visual,
                    new MBBindingList<TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemFlagVM>(),
                    props, delegate { });
                // DONE OD RAZU (Jeff 15.09: "musze recznie kasowac nazwe, bo jest za dluga
                // i nie moze byc spacji, nie moge kliknac Done"). Konstruktor vanilli
                // jeszcze raz owija tytul biezacym modyfikatorem z vanillowego craftingu
                // (GetCurrentItemModifier - stan po OSTATNIM vanillowym kuciu, dla nas
                // przypadkowy), a setter ItemName liczy CanConfirm przez
                // CampaignUIHelper.IsStringApplicableForItemName. Nadpisujemy nazwe czysta
                // wersja i odblokowujemy przycisk wprost - nasz FinalizePrefix i tak nazwy
                // nie uzywa (przedmiot juz lezy w sakwach pod wlasna nazwa).
                try { popup.ItemName = clean; popup.CanConfirm = true; } catch { }

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
            // SEMANTYKA WIDGETU (CraftedWeaponDesignResultListPanel):
            // InitValue = BAZA, ChangeAmount = roznica; widget sam animuje
            // baze -> baza+roznica i pokazuje zielone "+N". Wczesniej
            // podawalismy modVal jako InitValue - przy niezerowej roznicy
            // widget doliczalby bonus DRUGI raz.
            list.Add(new WeaponDesignResultPropertyItemVM(
                new TextObject("{=!}" + label, null), baseVal, modVal - baseVal, false));
            _lastDiag.Append(label).Append(' ').Append(baseVal).Append("->").Append(modVal).Append("; ");
        }

        private static readonly StringBuilder _lastDiag = new StringBuilder();

        private static MBBindingList<WeaponDesignResultPropertyItemVM> BuildProps(ItemObject item, ItemModifier mod)
        {
            var list = new MBBindingList<WeaponDesignResultPropertyItemVM>();
            try
            {
                // DIAGNOZA PLUSOW (Jeff 30.08: "balanced bez +2"): jedna linia
                // prawdy o modyfikatorze i kazdej stacie - bez zgadywania
                _lastDiag.Length = 0;
                if (mod != null)
                    _lastDiag.Append("mod=").Append(mod.StringId)
                        .Append(" dmg=").Append(mod.Damage).Append(" spd=").Append(mod.Speed)
                        .Append(" msl=").Append(mod.MissileSpeed).Append(" hp=").Append(mod.HitPoints)
                        .Append(" pm=").Append(mod.PriceMultiplier.ToString("0.00")).Append(" | ");
                else _lastDiag.Append("mod=NULL | ");
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
                        // Fire Rate = SwingSpeed luku; to TU siedzi bonus jakosci
                        // w swiecie RBM (legendary_bow: speed=+15, damage=0) -
                        // bez tego wiersza legendarny luk nie mial ani plusa
                        AddProp(list, "Fire Rate", w.SwingSpeed, mod != null ? mod.ModifySpeed(w.SwingSpeed) : w.SwingSpeed);
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
                Log.Info("CraftPopup diag: " + _lastDiag);
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
                string title = CleanName(item, mod);
                InformationManager.ShowInquiry(new InquiryData(title, sb.ToString(),
                    true, false, "Take it", null, null, null), true);
            }
            catch (Exception e) { Log.Error("CraftPopup.Inquiry", e); }
        }
    }
}
