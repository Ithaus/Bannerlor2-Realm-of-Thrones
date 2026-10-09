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
    /// WYJATEK K1c (Jeff 09.10, P2): zaloga w trybie GarrisonFightsWithArmoryOnly
    /// walczy tylko tym, co ma w zbrojowni - jej przydzialow nie ubieramy.
    /// sklad7b (Jeff 09.10 07:35: "jesli nie ma sprzetu, to nie ma sprzetu, nic
    /// nie dostaje z kosmosu"; lordowie AI "walcza tylko tym, co maja"): przy
    /// TroopsFightWithOwnKitOnly to samo dla druzyny gracza i partii lordow AI
    /// (takze towarzyszy i partii rodu) - GarrisonKit.OwnKitSpawn (tam tez ostatnia
    /// straz "nic z niczego" przy spawnie). Po bitwie DTE oddawal nasze dolozki do
    /// zbrojowni (zwrot liczy sie z ekwipunku agenta), wiec ubranie bylo mennica.
    /// sklad7b-p: ludzi partii z ta regula, ktorych straz nie widziala (GarrisonKit.OutsideGuard),
    /// tez nie ubieramy - w strone braku, z licznikiem w linii bitwy.
    /// Zostaja: partie bez zbrojowni DTE (bandyci, karawany, milicja...) i umarli.
    /// </summary>
    internal static class DressCode
    {
        private static readonly EquipmentIndex[] ArmourSlots =
        {
            EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg,
            EquipmentIndex.Gloves, EquipmentIndex.Cape
        };

        public static void Prefix(Mission __instance, AgentBuildData agentBuildData)
        {
            try
            {
                if (agentBuildData == null) return;
                var eq = agentBuildData.AgentOverridenSpawnEquipment;
                // K1c (przeglad K1b, Jeff 09.10 P2 "reszta walczy bez uzbrojenia po prostu"): zaloga w trybie "tylko to, co ma" walczy
                // tylko sprzetem ze swojej zbrojowni - pusty slot zostaje pusty. Dotad ubieralismy go ze wzorca (z niczego, na klonie,
                // bez oznaczenia jako tymczasowy), a po bitwie DTE oddawal te sztuki do zbrojowni zalogi.
                // sklad7b: takze druzyna gracza i lordowie AI (TroopsFightWithOwnKitOnly) - i tu ostatnia straz "nic z niczego"
                if (eq != null && GarrisonKit.OwnKitSpawn(eq)) return;
                var ch = agentBuildData.AgentCharacter;
                if (ch == null || ch.IsHero) return;
                // sklad7b-p (uwagi 7, 9 i 16): czlowiek partii z regula "tylko to, co ma", ktorego straz nie widziala (inny mod podmienil
                // ekwipunek, DTE nie dal przydzialu, latka DTE niewpieta) - nie ubieramy ze wzorca (mennica przez zwrot DTE), tylko liczymy
                if (GarrisonKit.OutsideGuard(__instance, agentBuildData, eq != null)) return;
                if (eq == null) return;                                   // bez nadpisu vanilla ubierze sama

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
                var troop = ch as TaleWorlds.CampaignSystem.CharacterObject;
                int ath = 0;
                try { if (troop != null) ath = troop.GetSkillValue(TaleWorlds.Core.DefaultSkills.Athletics); } catch { }
                int given = 0;
                foreach (var sl in ArmourSlots)
                {
                    if (!dressed[sl].IsEmpty) continue;
                    var it = tpl[sl].Item;
                    if (it == null) continue;
                    // ZASADA NADRZEDNA (Jeff, ksiega musztry): bez skilla nie
                    // uzywasz - takze przy dopelnianiu ze wzorca. Ale jak
                    // w DragonUnmount: sztuka ponad skill NIE zostawia golego
                    // slotu, tylko schodzi do najlepszej W RAMACH Atletyki
                    // (wzorce ROT lamia wlasne wymogi w setkach miejsc -
                    // czysty continue rozbieralby pol krolestwa).
                    if (troop != null && !ItemReq.Meets(troop, it))
                    {
                        it = SkillsDecide.TopArmor(it.ItemType, ath, troop != null ? troop.Culture : null);
                        if (it == null) continue;
                    }
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
                Log.Info("DressCode: pusty przydzial pancerza dostaje ubranie ze wzorca - poza ludzmi, ktorzy walcza tylko tym, co maja (zalogi; sklad7b: druzyna gracza i lordowie AI przy Troops Fight With Own Kit Only).");
            }
            catch (Exception e) { Log.Error("DressCode.ApplyAll", e); }
        }
    }
}
