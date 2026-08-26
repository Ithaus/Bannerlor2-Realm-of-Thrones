using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// GOLI WOJACY (Jeff, kryjowka): DTE ubiera kazda strone bitwy z JEJ
    /// wlasnego magazynu - a zbojnicy z kryjowki zadnego magazynu nie maja.
    /// Na dodatek awaryjne ubranie DTE jest w kryjowkach CELOWO wylaczone
    /// (wczesny return w ApplyEmergencyLoadout). Efekt: banda w gaciach
    /// z dzidami. Latka na Mission.SpawnAgent: czlowiek, ktoremu przydzial
    /// zostawil WSZYSTKIE sloty pancerza puste, wklada wlasne ubranie ze
    /// swojego wzorca (glowa, korpus, nogi, rece, peleryna). Nikt nie
    /// walczy nago - ani wrog, ani nasi, w zadnej misji.
    /// </summary>
    internal static class DressCode
    {
        public static void Prefix(AgentBuildData agentBuildData)
        {
            try
            {
                if (agentBuildData == null) return;
                var eq = agentBuildData.AgentOverridenSpawnEquipment;
                if (eq == null) return;                                   // bez nadpisu vanilla ubierze sama
                var ch = agentBuildData.AgentCharacter;
                if (ch == null || ch.IsHero) return;

                bool anyArmour =
                       !eq[EquipmentIndex.Head].IsEmpty
                    || !eq[EquipmentIndex.Body].IsEmpty
                    || !eq[EquipmentIndex.Leg].IsEmpty
                    || !eq[EquipmentIndex.Gloves].IsEmpty;
                if (anyArmour) return;                                    // ubrany choc czesciowo - nie ruszamy

                Equipment tpl = null;                                     // wzorzec bojowy oddzialu
                try
                {
                    var co = ch as TaleWorlds.CampaignSystem.CharacterObject;
                    if (co != null)
                    {
                        foreach (var e in co.BattleEquipments)
                            if (e != null && !e.IsEmpty()) { tpl = e; break; }
                    }
                    if (tpl == null) tpl = ch.Equipment;
                }
                catch { }
                if (tpl == null) return;
                int given = 0;
                EquipmentIndex[] slots =
                {
                    EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg,
                    EquipmentIndex.Gloves, EquipmentIndex.Cape
                };
                foreach (var sl in slots)
                {
                    if (!eq[sl].IsEmpty) continue;
                    var it = tpl[sl].Item;
                    if (it == null) continue;
                    eq[sl] = new EquipmentElement(it);
                    given++;
                }
                if (given > 0)
                    Log.Info("DressCode: goly " + ch.Name + " dostal " + given + " czesci ubrania ze wzorca.");
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(Mission), "SpawnAgent");
                if (m == null) { Log.Info("DressCode: brak Mission.SpawnAgent."); return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(DressCode), "Prefix") { priority = Priority.Last });
                Log.Info("DressCode: nikt nie walczy nago - pusty przydzial pancerza dostaje ubranie ze wzorca.");
            }
            catch (Exception e) { Log.Error("DressCode.ApplyAll", e); }
        }
    }
}
