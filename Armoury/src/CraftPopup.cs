using System;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// POPUP WYNIKU KUCIA (Jeff 29.08: "jak kuje pancerz czy luk, nie otwiera
    /// sie okno ze statystykami - powinna byc IDENTYCZNA zasada jak przy
    /// broniach: popup, staty, plusy przy masterwork, minusy przy fuszerce").
    /// Pokazujemy kazda state przedmiotu z wartoscia PO modyfikatorze i roznica
    /// w nawiasie: "Body armour: 34 (+3)".
    /// </summary>
    internal static class CraftPopup
    {
        internal static void Show(ItemObject item, ItemModifier mod, int made)
        {
            try
            {
                if (item == null || !Settings.Current.CraftResultPopup) return;
                var sb = new StringBuilder();
                if (mod != null) sb.AppendLine("Quality: " + mod.Name.ToString());

                if (item.HasArmorComponent)
                {
                    var a = item.ArmorComponent;
                    Line(sb, "Head armour", a.HeadArmor, mod != null ? mod.ModifyArmor(a.HeadArmor) : a.HeadArmor);
                    Line(sb, "Body armour", a.BodyArmor, mod != null ? mod.ModifyArmor(a.BodyArmor) : a.BodyArmor);
                    Line(sb, "Leg armour", a.LegArmor, mod != null ? mod.ModifyArmor(a.LegArmor) : a.LegArmor);
                    Line(sb, "Arm armour", a.ArmArmor, mod != null ? mod.ModifyArmor(a.ArmArmor) : a.ArmArmor);
                }
                var w = item.PrimaryWeapon;
                if (w != null)
                {
                    if (item.ItemType == ItemObject.ItemTypeEnum.Shield)
                        Line(sb, "Hit points", w.MaxDataValue, mod != null ? mod.ModifyHitPoints(w.MaxDataValue) : w.MaxDataValue);
                    else if (item.ItemType == ItemObject.ItemTypeEnum.Arrows || item.ItemType == ItemObject.ItemTypeEnum.Bolts)
                    {
                        Line(sb, "Damage", w.MissileDamage, mod != null ? mod.ModifyDamage(w.MissileDamage) : w.MissileDamage);
                        Line(sb, "Stack", w.MaxDataValue, mod != null ? mod.ModifyStackCount(w.MaxDataValue) : w.MaxDataValue);
                    }
                    else if (item.ItemType == ItemObject.ItemTypeEnum.Bow || item.ItemType == ItemObject.ItemTypeEnum.Crossbow)
                    {
                        Line(sb, "Damage", w.MissileDamage, mod != null ? mod.ModifyDamage(w.MissileDamage) : w.MissileDamage);
                        Line(sb, "Missile speed", w.MissileSpeed, mod != null ? mod.ModifyMissileSpeed(w.MissileSpeed) : w.MissileSpeed);
                        Line(sb, "Accuracy", w.Accuracy, w.Accuracy);
                    }
                    else
                    {
                        Line(sb, "Swing damage", w.SwingDamage, mod != null ? mod.ModifyDamage(w.SwingDamage) : w.SwingDamage);
                        Line(sb, "Thrust damage", w.ThrustDamage, mod != null ? mod.ModifyDamage(w.ThrustDamage) : w.ThrustDamage);
                        Line(sb, "Swing speed", w.SwingSpeed, mod != null ? mod.ModifySpeed(w.SwingSpeed) : w.SwingSpeed);
                        Line(sb, "Handling", w.Handling, mod != null ? mod.ModifySpeed(w.Handling) : w.Handling);
                    }
                }
                Line(sb, "Weight", (int)Math.Round(item.Weight), (int)Math.Round(item.Weight));
                if (made > 1) sb.AppendLine().Append("Made in a batch of " + made + ".");

                string title = (mod != null ? mod.Name + " " : "") + item.Name;
                InformationManager.ShowInquiry(new InquiryData(title, sb.ToString(),
                    true, false, "Take it", null, null, null), true);
            }
            catch (Exception e) { Log.Error("CraftPopup.Show", e); }
        }

        private static void Line(StringBuilder sb, string label, int baseVal, int modVal)
        {
            if (baseVal <= 0 && modVal <= 0) return;
            int diff = modVal - baseVal;
            sb.Append(label).Append(": ").Append(modVal);
            if (diff > 0) sb.Append(" (+").Append(diff).Append(")");
            else if (diff < 0) sb.Append(" (").Append(diff).Append(")");
            sb.AppendLine();
        }
    }
}
