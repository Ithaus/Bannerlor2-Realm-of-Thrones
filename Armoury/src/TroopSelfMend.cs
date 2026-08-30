using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// WOJSKO SAMO LATA SWOJ SPRZET (Jeff 29.08: "dostaja zold i czesc lupow,
    /// niech za to naprawiaja; jak im nie starczy, to ja moge"). Kazdego dnia
    /// w MIESCIE (jest kowal) zolnierze oddaja do naprawy najgorsze sztuki
    /// z magazynu kwatermistrza - placa ze swojego zoldu, gracz nie wydaje
    /// ani grosza. Po przerobce 30.08 (Jeff: "10 szt./dzien to bez sensu,
    /// niech naprawiaja PROCENT uszkodzen") dzienna robota to PROCENT calej
    /// puli zuzytych sztuk (min. 3, zeby ogon nie wisial wiecznie) - pelny
    /// remont trwa ~100/procent dni postoju NIEZALEZNIE od wielkosci armii.
    /// Kto sie spieszy, placi kowalowi (Send the men's worn gear...).
    /// </summary>
    internal static class TroopSelfMend
    {
        internal static void Run(Settlement st)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.TroopSelfMendEnabled || s.TroopSelfMendPercentPerDay <= 0) return;
                var main = MobileParty.MainParty;
                if (main == null || st == null || main.CurrentSettlement != st) return;
                if (!st.IsTown) return;   // naprawa wymaga kowala z prawdziwym warsztatem

                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;

                // najgorsze sztuki na wierzch - je lataja najpierw
                var worn = new List<ItemRosterElement>();
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    var mod = el.EquipmentElement.ItemModifier;
                    if (el.Amount <= 0 || mod == null || mod.PriceMultiplier >= 1f) continue;
                    if (el.EquipmentElement.Item == null) continue;
                    worn.Add(el);
                }
                if (worn.Count == 0) return;
                worn.Sort((a, b) => a.EquipmentElement.ItemModifier.PriceMultiplier
                    .CompareTo(b.EquipmentElement.ItemModifier.PriceMultiplier));

                int wornTotal = 0;
                foreach (var el in worn) wornTotal += el.Amount;
                int budget = Math.Max(3, (int)Math.Round(wornTotal * s.TroopSelfMendPercentPerDay / 100.0));
                if (budget > wornTotal) budget = wornTotal;
                int mended = 0;
                foreach (var el in worn)
                {
                    if (budget <= 0) break;
                    int take = Math.Min(budget, el.Amount);
                    armory.AddToCounts(el.EquipmentElement, -take);
                    armory.AddToCounts(new EquipmentElement(el.EquipmentElement.Item), take);   // czysty stan
                    mended += take; budget -= take;
                }
                if (mended > 0)
                {
                    Log.Info("TroopSelfMend: wojsko naprawilo " + mended + " sztuk w " + st.Name + " (z wlasnego zoldu).");
                    Log.Player("The men see to their own kit at " + st.Name + " - " + mended
                               + " pieces mended out of their pay.", true);
                }
            }
            catch (Exception e) { Log.Error("TroopSelfMend.Run", e); }
        }
    }
}
