using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PELNOKRWISTE MODYFIKATORY JAKOSCI (Jeff 29.08: "masterwork pokazuje
    /// tylko jeden wskaznik lepszy, a w vanilli bylo wiecej statystyk
    /// z plusikami/minusami"). Winowajca: RBM nadpisuje item_modifiers
    /// i sprowadza jakosc broni do SAMEGO damage (legendary_sword: damage=20
    /// i nic wiecej). Przy starcie sesji dopisujemy brakujace staty
    /// (tylko tam, gdzie pole == 0, zeby nie mnozyc cudzych wartosci):
    /// bron reczna dostaje Speed, strzelecka MissileSpeed, wedle stopnia
    /// jakosci; fuszerka analogicznie na minus.
    /// </summary>
    internal static class QualityRich
    {
        internal static void Enrich()
        {
            try
            {
                if (!Settings.Current.RichQualityModifiers) return;
                var fSpeed = AccessTools.Field(typeof(ItemModifier), "<Speed>k__BackingField");
                var fMissile = AccessTools.Field(typeof(ItemModifier), "<MissileSpeed>k__BackingField");
                if (fSpeed == null || fMissile == null) { Log.Info("QualityRich: pola nie pasuja - odpuszczam."); return; }

                int touched = 0;
                foreach (var m in MBObjectManager.Instance.GetObjectTypeList<ItemModifier>())
                {
                    if (m == null) continue;
                    int grade = Grade(m.ItemQuality);
                    if (grade == 0) continue;
                    if (m.Damage == 0) continue;   // to nie modyfikator broni

                    string id = m.StringId ?? "";
                    bool ranged = id.Contains("bow") || id.Contains("crossbow");
                    bool ammo = id.Contains("arrow") || id.Contains("bolt") || id.Contains("javelin")
                                || id.Contains("throwing");
                    bool any = false;
                    if (!ammo && m.Speed == 0) { fSpeed.SetValue(m, grade * 2); any = true; }
                    if (ranged && m.MissileSpeed == 0) { fMissile.SetValue(m, grade * 3); any = true; }
                    if (any) touched++;
                }
                if (touched > 0)
                    Log.Info("QualityRich: " + touched + " modyfikatorow jakosci wzbogaconych (Speed/MissileSpeed wedle stopnia).");
            }
            catch (Exception e) { Log.Error("QualityRich.Enrich", e); }
        }

        private static int Grade(ItemQuality q)
        {
            switch (q)
            {
                case ItemQuality.Fine: return 1;
                case ItemQuality.Masterwork: return 2;
                case ItemQuality.Legendary: return 3;
                case ItemQuality.Inferior: return -1;
                case ItemQuality.Poor: return -2;
                default: return 0;
            }
        }
    }
}
