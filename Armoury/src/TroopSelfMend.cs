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
                    if (mod.PriceMultiplier < 0.1f) continue;   // audyt pelny K2: wrak - tylko kowal z materialem albo przetop
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
                int mended = 0, paidAll = 0;
                foreach (var el in worn)
                {
                    if (budget <= 0) break;
                    // audyt pelny K2: naprawa PLATNA miastu (robocizna jak u kowala: 25% utraconej wartosci) - wczesniej za darmo
                    int unit = Math.Max(1, (int)(el.EquipmentElement.Item.Value * (1f - el.EquipmentElement.ItemModifier.PriceMultiplier) * 0.25f));
                    int afford = Math.Max(0, TaleWorlds.CampaignSystem.Hero.MainHero.Gold - paidAll) / unit;
                    int take = Math.Min(Math.Min(budget, el.Amount), afford);
                    if (take <= 0) break;
                    paidAll += unit * take;
                    armory.AddToCounts(el.EquipmentElement, -take);
                    armory.AddToCounts(new EquipmentElement(el.EquipmentElement.Item), take);   // czysty stan
                    mended += take; budget -= take;
                }
                if (paidAll > 0) Pay.ToSettlement(paidAll);
                if (mended > 0)
                {
                    Log.Info("TroopSelfMend: wojsko naprawilo " + mended + " sztuk w " + st.Name + " za " + paidAll + " zl do kasy miasta.");
                    Log.Player("The men see to their own kit at " + st.Name + " - " + mended
                               + " pieces mended by the town smiths for " + paidAll + " gold.", true);
                }
            }
            catch (Exception e) { Log.Error("TroopSelfMend.Run", e); }
        }
    }
}
