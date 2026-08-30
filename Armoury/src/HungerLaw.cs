using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PRAWO GLODU (Jeff 30.08: "jak gloduja, to cena nie gra roli - kupuja,
    /// bo sa glodni"). Vanilla i BannerKings kaza AI kupowac jedzenie TYLKO
    /// ponizej 120 denarow (DefaultPartyFoodBuyingModel:37,
    /// BKPartyBuyingFoodModel:35) - w wojennej drozyznie lord stoi na pelnym
    /// targu i gloduje, a glod zamienia 25% szeregowych DZIENNIE w rannych
    /// (stad armie samych rannych przy zdrowych dowodcach). Postfix na KAZDA
    /// zadeklarowana implementacje FindItemToBuy: gdy model nie wybral nic,
    /// a partia AI GLODUJE - bierzemy najtansza sztuke jedzenia, na ktora
    /// ja stac, bez limitu ceny. Syty targuje sie jak dotad, glodny placi
    /// ile trzeba.
    /// Parametry po indeksach (__0..__3), bo implementacje roznie nazywaja
    /// out-parametr (vanilla/BK: itemElement, ROT: itemRosterElement) -
    /// nazwy by nie zmatchowaly wszedzie.
    /// </summary>
    internal static class HungerLaw
    {
        private static int _bought;

        public static void Postfix(MobileParty __0, Settlement __1,
            ref ItemRosterElement __2, ref float __3)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.AiStarvingBuysAnyPrice) return;
                if (__2.EquipmentElement.Item != null) return;        // model cos wybral - nie ruszamy
                var party = __0; var settlement = __1;
                if (party == null || settlement == null) return;
                if (party.IsMainParty) return;                        // gracz kupuje sam
                if (party.Party == null || !party.Party.IsStarving) return;

                var comp = settlement.SettlementComponent;
                if (comp == null || settlement.ItemRoster == null) return;
                int gold = party.LeaderHero != null ? party.LeaderHero.Gold : party.PartyTradeGold;
                int bestIdx = -1, bestPrice = int.MaxValue;
                for (int i = 0; i < settlement.ItemRoster.Count; i++)
                {
                    var el = settlement.ItemRoster.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || !it.IsFood) continue;
                    int price = comp.GetItemPrice(el.EquipmentElement, party);
                    if (price > gold) continue;                       // na to go nie stac
                    if (price < bestPrice) { bestPrice = price; bestIdx = i; }
                }
                if (bestIdx < 0) return;                              // targ pusty albo za drogi nawet na dno sakwy
                __2 = settlement.ItemRoster.GetElementCopyAtIndex(bestIdx);
                __3 = bestPrice;
                _bought++;
                if (_bought == 1 || _bought % 100 == 0)
                    Log.Info("HungerLaw: glodne partie kupily juz " + _bought
                             + " szt. jedzenia ponad limit 120 (ostatnio "
                             + (__2.EquipmentElement.Item != null ? __2.EquipmentElement.Item.StringId : "?")
                             + " za " + bestPrice + " w " + settlement.Name + ").");
            }
            catch (Exception e) { Log.Error("HungerLaw", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.AiStarvingBuysAnyPrice) { Log.Info("HungerLaw: wylaczone."); return; }
                var post = new HarmonyMethod(typeof(HungerLaw), "Postfix");
                int done = 0;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (System.Reflection.ReflectionTypeLoadException r) { types = r.Types; }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract || !typeof(PartyFoodBuyingModel).IsAssignableFrom(t)) continue;
                        var m = t.GetMethod("FindItemToBuy",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                        if (m == null || m.IsAbstract) continue;
                        // kazda latka osobno - egzotyczna sygnatura w cudzym
                        // modelu nie moze polozyc pozostalych
                        try { h.Patch(m, postfix: post); done++; }
                        catch (Exception pe) { Log.Error("HungerLaw.Patch(" + t.Name + ")", pe); }
                    }
                }
                Log.Info("HungerLaw: glodny nie patrzy na cene - " + done + " modeli zakupu jedzenia zalatanych.");
            }
            catch (Exception e) { Log.Error("HungerLaw.ApplyAll", e); }
        }
    }
}
