using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// SMOK TO NIE KON POD SIODLO. Rynsztunki ROT (szablon lorda z dragon_black
    /// w slocie wierzchowca) i wirtualne magazyny DTE (bandyci lupia pokonanych)
    /// potrafia wsadzic jednostce SMOKA jako konia - a silnik w zwyklej polowej
    /// bitwie sie na tym krztusi (28.08: trzy crashe przy wejsciu w te sama
    /// bitwe, potem hang "deep in native"; Jeff: "dwa smoki na polu bitwy").
    /// Prefix na Mission.SpawnAgent: smok schodzi ze slotu PRZED spawnem,
    /// jednostka idzie pieszo. Log mowi, CZYJ byl smok - koniec zgadywania.
    /// Smokow spawnowanych przez ROT jako osobne stwory (nie wierzchowce)
    /// nie ruszamy.
    /// </summary>
    internal static class DragonUnmount
    {
        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var m = AccessTools.Method(typeof(Mission), "SpawnAgent");
                if (m == null) { Log.Info("DragonUnmount: nie znalazlem Mission.SpawnAgent - patch spi."); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(DragonUnmount), "StripDragonMount"));
                Log.Info("DragonUnmount: smoki schodza ze slotu konia przed spawnem.");
            }
            catch (Exception e) { Log.Error("DragonUnmount.ApplyAll", e); }
        }

        public static void StripDragonMount(AgentBuildData agentBuildData)
        {
            try
            {
                if (agentBuildData == null || agentBuildData.AgentData == null) return;
                var eq = agentBuildData.AgentData.AgentOverridenEquipment;
                if (eq == null) return;
                // PRAWO LEGEND przy spawnie: szeregowy z nazwana klinga (np. DTE
                // ubral go z magazynu pelnego lupow) dostaje zwykly odpowiednik;
                // bohaterowie nosza swoje legendy dalej
                try
                {
                    var soldier = agentBuildData.AgentData.AgentCharacter;
                    if (soldier != null && !soldier.IsHero)
                        for (int slot = 0; slot < 4; slot++)
                        {
                            var w = eq[(EquipmentIndex)slot].Item;
                            if (w == null || !w.HasWeaponComponent) continue;
                            if (Settings.Current.LegendaryLootValueFloor <= 0
                                || w.Value < Settings.Current.LegendaryLootValueFloor) continue;
                            var repl = LegendaryLaw.ReplacementFor(w);
                            eq[(EquipmentIndex)slot] = repl != null
                                ? new EquipmentElement(repl) : new EquipmentElement(null);
                            Log.Info("LegendaryLaw: " + w.StringId + " zdjety przy spawnie z szeregowego ("
                                     + soldier.StringId + ").");
                        }
                }
                catch { }
                var horse = eq[(EquipmentIndex)10];          // slot wierzchowca
                var item = horse.Item;
                if (item == null || item.StringId == null) return;
                if (!item.StringId.StartsWith("dragon_")) return;
                // Jeff: smoki ma TYLKO Daenerys (lord_1_14) - jej trojki nie ruszamy;
                // gracz tez moze miec smoka (quest). Cala reszta to przebierancy
                // z wylosowanego szablonu albo lupow DTE - schodza.
                try
                {
                    var rider = agentBuildData.AgentData.AgentCharacter;
                    if (rider != null)
                    {
                        if (rider.IsPlayerCharacter) return;
                        var rid = rider.StringId ?? "";
                        if (rid == "lord_1_14" || rid.StartsWith("lord_1_14_")) return;
                    }
                }
                catch { }
                eq[(EquipmentIndex)10] = new EquipmentElement(null);   // kon precz
                eq[(EquipmentIndex)11] = new EquipmentElement(null);   // rzad konski tez
                string who = "?";
                try
                {
                    var ch = agentBuildData.AgentData.AgentCharacter;
                    if (ch != null && ch.Name != null) who = ch.Name.ToString();
                }
                catch { }
                Log.Info("DragonUnmount: " + item.StringId + " zdjety przy spawnie z: " + who + ".");
            }
            catch (Exception e) { Log.Error("DragonUnmount.StripDragonMount", e); }
        }
    }
}
