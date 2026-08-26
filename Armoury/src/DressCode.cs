using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// GOLI WOJACY (Jeff, kryjowka): DTE ubiera z magazynu, ale druzynie
    /// gracza CELOWO nie dopelnia pustych slotow (kara "underequipped"),
    /// a jego awaryjne ubranie jest w kryjowkach wylaczone (wczesny return
    /// w ApplyEmergencyLoadout) - w polu ratowalo, w kryjowce nie. Efekt:
    /// nasi w gaciach wsrod zbojcow. Latka na Mission.SpawnAgent (prefix
    /// Priority.Last, czyli PO prefixie DTE): KAZDY pusty slot pancerza
    /// z osobna dostaje ubranie ze wzorca oddzialu. Robimy to na KLONIE
    /// ekwipunku - przydzial DTE zostaje nietkniety, wiec jego rozliczenie
    /// magazynu (postfix zdejmuje przydzielone sztuki z polek) nie widzi
    /// naszych dolozek i niczego nie gubi. Nikt nie walczy nago - ani
    /// wrog, ani nasi, w zadnej misji.
    /// </summary>
    internal static class DressCode
    {
        private static readonly EquipmentIndex[] ArmourSlots =
        {
            EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg,
            EquipmentIndex.Gloves, EquipmentIndex.Cape
        };

        public static void Prefix(AgentBuildData agentBuildData)
        {
            try
            {
                if (agentBuildData == null) return;
                var eq = agentBuildData.AgentOverridenSpawnEquipment;
                if (eq == null) return;                                   // bez nadpisu vanilla ubierze sama
                var ch = agentBuildData.AgentCharacter;
                if (ch == null || ch.IsHero) return;

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

                // czy w ogole jest co dopelniac? (nie klonujemy po proznicy)
                bool needAny = false;
                foreach (var sl in ArmourSlots)
                    if (eq[sl].IsEmpty && tpl[sl].Item != null) { needAny = true; break; }
                if (!needAny) return;

                var dressed = eq.Clone();
                int given = 0;
                foreach (var sl in ArmourSlots)
                {
                    if (!dressed[sl].IsEmpty) continue;
                    var it = tpl[sl].Item;
                    if (it == null) continue;
                    dressed[sl] = new EquipmentElement(it);
                    given++;
                }
                if (given > 0)
                {
                    agentBuildData.Equipment(dressed);
                    Log.Info("DressCode: " + ch.Name + " mial " + given + " pustych slotow pancerza - dostal ubranie ze wzorca.");
                }
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
