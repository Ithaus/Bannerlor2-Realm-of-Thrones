using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// STRZALY SIE LAMIA (Jeff 02.09: "strzaly w armii sie nie koncza - po
    /// wystrzelaniu kolczanu od nowa mozna ich uzywac, zbieraja po polu.
    /// Ustalmy niewielki procent straconych: im wyzszy tier, tym mniej sie
    /// lamia; jak wyjdzie 5%, to 5% kolczanow/beltow znika i trzeba
    /// uzupelniac; rozne tiery - losowanie, najnizsze najbardziej").
    /// Po kazdej bitwie gracza kazdy kolczan/belt w sakwach partii I w
    /// magazynie oddzialu (DTE) rzuca kostka: peka z szansa
    /// AmmoBreakPercent x mnoznik tieru (tier 3 = 1.0; kazdy tier nizej
    /// +AmmoBreakTierStep%, kazdy wyzej -tyle samo: przy 25 -> t1 1.5,
    /// t2 1.25, t4 0.75, t6 0.25). Pekniete znikaja - uzupelnia sie
    /// u kupca albo w kuzni (FletchForge). Kolczany NOSZONE przez
    /// bohaterow nie pekaja - to ekwipunek, nie zapas.
    /// </summary>
    internal static class AmmoAttrition
    {
        internal static void AfterBattle()
        {
            try
            {
                var s = Settings.Current;
                if (!s.AmmoBreakEnabled || s.AmmoBreakPercent <= 0) return;
                var report = new List<string>();
                int lost = 0;
                lost += Break(MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null, "sakwy", report);
                lost += Break(QuartermasterLaw.DteArmory(), "magazyn", report);
                if (lost <= 0) return;
                Log.Info("Amunicja: po bitwie peklo " + lost + " kolczanow/beltow: " + string.Join(", ", report.ToArray()) + ".");
                Log.Player(lost + (lost == 1 ? " quiver" : " quivers") + " of arrows or bolts broke on the field - restock before the next fight.", true);
            }
            catch (Exception e) { Log.Error("AmmoAttrition.AfterBattle", e); }
        }

        private static int Break(ItemRoster roster, string where, List<string> report)
        {
            if (roster == null) return 0;
            var s = Settings.Current;
            var losses = new List<KeyValuePair<EquipmentElement, int>>();
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var item = el.EquipmentElement.Item;
                if (item == null || !ArmouryBehavior.IsAmmo(item) || el.Amount <= 0) continue;
                int grade = Recipes.Grade(item);
                float mult = 1f + (3 - grade) * (s.AmmoBreakTierStep / 100f);
                if (mult < 0.1f) mult = 0.1f;
                float chance = s.AmmoBreakPercent / 100f * mult;
                int broken = 0;
                for (int n = 0; n < el.Amount; n++)
                    if (MBRandom.RandomFloat < chance) broken++;
                if (broken > 0)
                {
                    losses.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, broken));
                    report.Add(item.StringId + " x" + broken + " (t" + grade + ", " + where + ")");
                }
            }
            int total = 0;
            foreach (var l in losses)
            {
                roster.AddToCounts(l.Key, -l.Value);
                total += l.Value;
            }
            return total;
        }
    }
}
