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
        /// <summary>Licznik meldunkow o PODLODZE sprzetu (Jeff 31.08: "tylko
        /// zeby nadzy nie wyszli") - logujemy pierwsze 20 przypadkow na sesje,
        /// zeby nie zalac logu przy kazdym spawnie kazdej bitwy.</summary>
        private static int _floorLog;

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
                    var soldier = agentBuildData.AgentData.AgentCharacter as TaleWorlds.CampaignSystem.CharacterObject;
                    if (soldier != null && !soldier.IsHero)
                    {
                        for (int slot = 0; slot < 4; slot++)
                        {
                            var w = eq[(EquipmentIndex)slot].Item;
                            // SPRZET OLBRZYMOW NA CZLOWIEKU (Jeff 14.09): schodzi ZAWSZE,
                            // w reke wchodzi zwykla bron w ramach skilla - a gdy nie ma
                            // czym, lepiej goly slot niz mamut z maczuga w ludzkiej dloni
                            if (GiantGear.Is(w) && !GiantGear.MayWear(soldier))
                            {
                                int gs = w.RelevantSkill != null ? soldier.GetSkillValue(w.RelevantSkill) : 0;
                                var gswap = SkillsDecide.PatternFor(w.ItemType, gs);
                                eq[(EquipmentIndex)slot] = gswap != null ? new EquipmentElement(gswap) : new EquipmentElement(null);
                                if (_floorLog++ < 20)
                                    Log.Info("GiantGear: " + w.StringId + " zdjety z " + soldier.StringId
                                             + " - dostaje " + (gswap != null ? gswap.StringId : "goly slot") + ".");
                                continue;
                            }
                            if (LegendaryLaw.IsLegend(w))   // prog 100k + lista person
                            {
                                // legenda schodzi ZAWSZE, ale zolnierz nie idzie z golym
                                // slotem: zamiennik -> wzorzec klasy -> dopiero nic
                                var repl = LegendaryLaw.ReplacementFor(w);
                                if (repl == null)
                                {
                                    var rsk = ItemReq.SkillFor(w);
                                    int skl = rsk != null ? soldier.GetSkillValue(rsk) : 0;
                                    repl = SkillsDecide.PatternFor(w.ItemType, skl);
                                }
                                eq[(EquipmentIndex)slot] = repl != null
                                    ? new EquipmentElement(repl) : new EquipmentElement(null);
                                Log.Info("LegendaryLaw: " + w.StringId + " zdjety przy spawnie z szeregowego ("
                                         + soldier.StringId + ") - dostaje "
                                         + (repl != null ? repl.StringId : "goly slot") + ".");
                                continue;
                            }
                            // ZASADA NADRZEDNA na scenie: bron ponad skill schodzi,
                            // ale zolnierz dostaje najlepsza W RAMACH swojego skilla
                            // (nie wchodzi golym slotem) - i log mowi wprost CZEMU
                            // (Jeff 29.08: "kwatermistrz nie przyjal lukow?!")
                            if (w != null)
                            {
                                string whyNot;
                                if (!ItemReq.Meets(soldier, w, out whyNot))
                                {
                                    var rsk2 = ItemReq.SkillFor(w);
                                    int sk = rsk2 != null ? soldier.GetSkillValue(rsk2) : 0;
                                    var swap = SkillsDecide.PatternFor(w.ItemType, sk);
                                    // PODLOGA SPRZETU (Jeff 31.08): degradacja owszem,
                                    // ale gdy NIE MA czym podmienic - zolnierz zostaje
                                    // przy swoim. Bezbronny jest gorszy niz przepakowany.
                                    if (swap != null)
                                    {
                                        eq[(EquipmentIndex)slot] = new EquipmentElement(swap);
                                        Log.Info("ItemReq: " + soldier.StringId + " nie udzwignie " + w.StringId
                                                 + " (" + whyNot + ") - dostaje " + swap.StringId + ".");
                                    }
                                    else if (_floorLog++ < 20)
                                        Log.Info("ItemReq: " + soldier.StringId + " nie udzwignie " + w.StringId
                                                 + " (" + whyNot + "), ale nie ma lzejszej broni - zostaje przy swojej.");
                                }
                            }
                        }
                        // pancerz ponad atletyke -> najlepszy dozwolony; kon ponad
                        // Riding -> najlepszy dozwolony (albo pieszo)
                        int ath = soldier.GetSkillValue(TaleWorlds.Core.DefaultSkills.Athletics);
                        for (int slot = 5; slot <= 9; slot++)
                        {
                            var a = eq[(EquipmentIndex)slot].Item;
                            if (a == null) continue;
                            // PANCERZ OLBRZYMA NA CZLOWIEKU (Jeff 14.09): schodzi zawsze,
                            // wchodzi najlepszy zwykly w ramach Atletyki
                            if (GiantGear.Is(a) && !GiantGear.MayWear(soldier))
                            {
                                var gtop = SkillsDecide.TopArmor(a.ItemType, ath, soldier.Culture);
                                eq[(EquipmentIndex)slot] = gtop != null ? new EquipmentElement(gtop) : new EquipmentElement(null);
                                if (_floorLog++ < 20)
                                    Log.Info("GiantGear: " + a.StringId + " zdjety z " + soldier.StringId
                                             + " - dostaje " + (gtop != null ? gtop.StringId : "goly slot") + ".");
                                continue;
                            }
                            if (ItemReq.Meets(soldier, a)) continue;
                            var top = SkillsDecide.TopArmor(a.ItemType, ath, soldier.Culture);
                            // PODLOGA SPRZETU (Jeff 31.08: "tylko zeby nadzy nie
                            // wyszli"): brak lzejszej sztuki = zolnierz zostaje
                            // w swoim pancerzu. Zla statystyka jest mniejszym zlem
                            // niz goly korpus - to JEDYNE miejsce w repo, ktore
                            // potrafilo wpisac null do slotu pancerza.
                            if (top != null) eq[(EquipmentIndex)slot] = new EquipmentElement(top);
                            else if (_floorLog++ < 20)
                                Log.Info("ItemReq: " + soldier.StringId + " - brak lzejszego pancerza ("
                                         + a.ItemType + ") w ramach Atletyki " + ath + "; zostaje w swoim.");
                        }
                        var mnt = eq[(EquipmentIndex)10].Item;
                        // GEOGRAFIA WIERZCHOWCOW (Jeff 14.09): mamut/wielblad/rydwan/slon
                        // nie u swoich schodzi tak samo jak kon ponad Riding
                        bool geo = mnt != null && !MountLaw.Allowed(soldier, mnt);
                        if (mnt != null && (geo || !ItemReq.Meets(soldier, mnt)))
                        {
                            var topM = SkillsDecide.TopMount(
                                soldier.GetSkillValue(TaleWorlds.Core.DefaultSkills.Riding));
                            eq[(EquipmentIndex)10] = topM != null
                                ? new EquipmentElement(topM) : new EquipmentElement(null);
                            if (topM == null) eq[(EquipmentIndex)11] = new EquipmentElement(null);
                            else
                            {
                                // UPRZAZ MUSI PASOWAC DO NOWEGO KONIA (Jeff 14.09, crash
                                // 11:17:58): rodzina inna niz wierzchowca = natywny
                                // AccessViolation w AddMountMesh - wtedy bez uprzezy
                                var hr = eq[(EquipmentIndex)11].Item;
                                var mc = topM.HorseComponent != null ? topM.HorseComponent.Monster : null;
                                if (hr != null && hr.ArmorComponent != null && mc != null
                                    && hr.ArmorComponent.FamilyType != mc.FamilyType)
                                    eq[(EquipmentIndex)11] = new EquipmentElement(null);
                                if (_floorLog++ < 20)
                                    Log.Info((geo ? "MountLaw: " : "ItemReq: ") + soldier.StringId
                                             + (geo ? " nie ma prawa do " : " nie udzwignie ") + mnt.StringId
                                             + (geo ? " (" + MountLaw.Name(MountLaw.FamilyOf(mnt)) + " nie u swoich)" : " (Riding)")
                                             + " - dostaje " + topM.StringId + ".");
                            }
                            if (topM == null && _floorLog++ < 20)
                                Log.Info((geo ? "MountLaw: " : "ItemReq: ") + soldier.StringId + " traci " + mnt.StringId
                                         + (geo ? " (" + MountLaw.Name(MountLaw.FamilyOf(mnt)) + " nie u swoich)" : " (Riding)") + " - idzie pieszo.");
                        }
                    }
                    else if (soldier != null && soldier.IsHero)
                    {
                        // GEOGRAFIA WIERZCHOWCOW u bohatera (gracz tez): lord Polnocy
                        // nie wjezdza w bitwe na wielbladzie ani mamucie
                        var hm = eq[(EquipmentIndex)10].Item;
                        if (hm != null && !MountLaw.Allowed(soldier, hm))
                        {
                            var htop = SkillsDecide.TopMount(soldier.GetSkillValue(TaleWorlds.Core.DefaultSkills.Riding));
                            eq[(EquipmentIndex)10] = htop != null ? new EquipmentElement(htop) : new EquipmentElement(null);
                            var hh = eq[(EquipmentIndex)11].Item;
                            var hmc = htop != null && htop.HorseComponent != null ? htop.HorseComponent.Monster : null;
                            if (htop == null || (hh != null && hh.ArmorComponent != null && hmc != null
                                                 && hh.ArmorComponent.FamilyType != hmc.FamilyType))
                                eq[(EquipmentIndex)11] = new EquipmentElement(null);
                            if (_floorLog++ < 20)
                                Log.Info("MountLaw: bohater " + soldier.StringId + " nie ma prawa do " + hm.StringId
                                         + " (" + MountLaw.Name(MountLaw.FamilyOf(hm)) + " nie u swoich) - "
                                         + (htop != null ? "dostaje " + htop.StringId : "idzie pieszo") + ".");
                        }
                        // pancerz olbrzyma na bohaterze: czlowiek go nie nosi
                        int hath = soldier.GetSkillValue(TaleWorlds.Core.DefaultSkills.Athletics);
                        for (int slot = 5; slot <= 9; slot++)
                        {
                            var a = eq[(EquipmentIndex)slot].Item;
                            if (!GiantGear.Is(a) || GiantGear.MayWear(soldier)) continue;
                            var gtop = SkillsDecide.TopArmor(a.ItemType, hath, soldier.Culture);
                            eq[(EquipmentIndex)slot] = gtop != null ? new EquipmentElement(gtop) : new EquipmentElement(null);
                            if (_floorLog++ < 20)
                                Log.Info("GiantGear: " + a.StringId + " zdjety z bohatera " + soldier.StringId
                                         + " - dostaje " + (gtop != null ? gtop.StringId : "goly slot") + ".");
                        }
                        // bohater (gracz tez) z maczuga olbrzyma: czlowiek jej nie
                        // uzywa - wchodzi zwykla bron w ramach jego skilla
                        for (int slot = 0; slot < 4; slot++)
                        {
                            var w = eq[(EquipmentIndex)slot].Item;
                            if (!GiantGear.Is(w) || GiantGear.MayWear(soldier)) continue;
                            int gs = w.RelevantSkill != null ? soldier.GetSkillValue(w.RelevantSkill) : 0;
                            var gswap = SkillsDecide.PatternFor(w.ItemType, gs);
                            eq[(EquipmentIndex)slot] = gswap != null ? new EquipmentElement(gswap) : new EquipmentElement(null);
                            if (_floorLog++ < 20)
                                Log.Info("GiantGear: " + w.StringId + " zdjety z bohatera " + soldier.StringId
                                         + " - dostaje " + (gswap != null ? gswap.StringId : "goly slot") + ".");
                        }
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
