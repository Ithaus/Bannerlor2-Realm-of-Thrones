using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// SLON NIE ZIMUJE W WINTERFELL. Jeff 28.08: "za duzo sloni, moge kupic
    /// slonia w Winterfell - zrob cos!". ROT-owy elephant ma is_merchandise=true
    /// i item_category=horse, wiec karawany rozwoza go po calej mapie jak
    /// zwykla szkape. Kwarantanna: slon (i slonoiwe zbroje/siodla) zostaje
    /// na targu TYLKO w osadzie tej samej kultury co przedmiot (volantine -
    /// Essos); wszedzie indziej schodzi ze straganu przy dziennym ticku osady
    /// i przy wejsciu gracza do miasta.
    /// </summary>
    internal static class ElephantQuarantine
    {
        private static int _sweptTotal;

        internal static void Sweep(Settlement st)
        {
            try
            {
                if (!Settings.Current.ElephantQuarantineEnabled) return;
                if (st == null || st.ItemRoster == null) return;
                if (!st.IsTown && !st.IsVillage) return;
                var roster = st.ItemRoster;
                int gone = 0;
                for (int i = roster.Count - 1; i >= 0; i--)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || it.StringId == null || el.Amount <= 0) continue;
                    if (it.StringId != "elephant" && !it.StringId.StartsWith("rot_elephant")) continue;
                    if (it.Culture != null && st.Culture == it.Culture) continue;   // Essos handluje slontem u siebie
                    roster.AddToCounts(el.EquipmentElement, -el.Amount);
                    gone += el.Amount;
                }
                if (gone > 0)
                {
                    _sweptTotal += gone;
                    Log.Info("ElephantQuarantine: " + gone + " szt. sloniowego towaru zeszlo z targu w " + st.Name
                             + " (lacznie " + _sweptTotal + ").");
                }
            }
            catch (Exception e) { Log.Error("ElephantQuarantine.Sweep", e); }
        }
    }
}
