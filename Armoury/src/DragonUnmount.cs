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

        // Z16 E8: licznik linii logu zamian w zestawie zaciagu ROT (pierwsze 20 na sesje) i wlasciwosc z prywatnym setterem
        private static int _e8Log;
        private static System.Reflection.PropertyInfo _pMissionEq;

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var m = AccessTools.Method(typeof(Mission), "SpawnAgent");
                if (m == null) { Log.Info("DragonUnmount: nie znalazlem Mission.SpawnAgent - patch spi."); return; }
                // Z16 (E8): Priority.Low - ten prefiks ma isc PO prefiksach, ktore wydaja sprzet przy spawnie (ROT
                // EnlistmentPatches.patch24 - zestaw zaciagu, DTE SpawnAgentPatch). Dotad tak bylo tylko dzieki kolejnosci
                // wpinania (ROT i DTE w OnSubModuleLoad, my w OnBeforeInitialModuleScreenSetAsRoot) - teraz wprost.
                var pre = new HarmonyMethod(typeof(DragonUnmount), "StripDragonMount");
                pre.priority = Priority.Low;
                harmony.Patch(m, prefix: pre);
                Log.Info("DragonUnmount: smoki schodza ze slotu konia przed spawnem.");
            }
            catch (Exception e) { Log.Error("DragonUnmount.ApplyAll", e); }
        }

        /// <summary>Z16-5: lustro drugiego warunku ROT patch24 (EnlistmentPatches.cs ok. :609-628) - zestaw zaciagu idzie
        /// takze na walke w sali lorda: PlayerEncounter.Current.EncounterSettlementAux.CurrentSiegeState == InTheLordsHall.</summary>
        private static bool InLordsHall()
        {
            try
            {
                var pe = TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.Current;
                var st = pe != null ? pe.EncounterSettlementAux : null;
                return st != null && st.CurrentSiegeState == TaleWorlds.CampaignSystem.Settlements.Settlement.SiegeState.InTheLordsHall;
            }
            catch { return false; }
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
                        // Z16 E8 (Jeff 09.10): ZACIAG ROT - w sluzbie lorda bohater klanu gracza dostaje na bitwe zestaw
                        // jednostki swojej rangi (ROT EnlistmentPatches.patch24: AgentOverridenEquipment + gotowe
                        // AgentOverridenSpawnMissionEquipment). Sztuka WYDANA (inna niz jego wlasna w tym slocie), ktorej
                        // bohater nie udzwignie (ItemReq.MeetsHero: pancerz - Atletyka, bron - jej umiejetnosc, strzaly - Luk,
                        // belty - Kusza): (a) gdy w tym slocie nosi wlasna sztuke TEGO SAMEGO typu - wraca jego wlasna;
                        // (b) inaczej najlepsza sztuka tego typu w granicy jego umiejetnosci (pancerz - TopArmor, bron i amunicja -
                        // PatternFor; typ slotu zostaje, wiec luk zostaje ze strzalami, kusza z beltami, a pusty wlasny slot nie
                        // robi z bohatera golego); (c) gdy takiej brak - zostaje wydana (podloga sprzetu, Jeff 31.08: nikt nie
                        // walczy nago). POPRAWKA Z16-5 (recenzja): dotad sztuka wracala na wlasna z tego slotu takze, gdy slot byl
                        // pusty albo innego typu (gola glowa, luk bez strzal). Wlasne sztuki - bez sita (NOSZENIE, decyzja 3); przy
                        // wlaczonym HeroGearRequirements pomija je takze regula sceny 14.09 nizej. Tylko tam, gdzie ROT wydaje
                        // zestaw: bohater klanu gracza, bitwa w polu / oblezenie / wypad albo walka w sali lorda (lustro warunku ROT
                        // patch24: PlayerEncounter...CurrentSiegeState == InTheLordsHall), gotowa bron agenta
                        // (AgentOverridenSpawnMissionEquipment - poza ROT ustawia ja tylko misja fabularna NavalDLC). Turnieje,
                        // areny, bijatyki i pojedynki ROT - bez zmian.
                        var w0 = new ItemObject[4];
                        for (int slot = 0; slot < 4; slot++) w0[slot] = eq[(EquipmentIndex)slot].Item;
                        var ownEq = soldier.HeroObject != null ? soldier.HeroObject.BattleEquipment : null;
                        bool heroReq = Settings.Current != null && Settings.Current.HeroGearRequirements;
                        bool enlistKit = false;
                        try
                        {
                            var mis = Mission.Current;
                            enlistKit = ownEq != null && agentBuildData.AgentOverridenSpawnMissionEquipment != null
                                && soldier.HeroObject.Clan != null && soldier.HeroObject.Clan == TaleWorlds.CampaignSystem.Clan.PlayerClan
                                && mis != null && (mis.IsFieldBattle || mis.IsSiegeBattle || mis.IsSallyOutBattle || InLordsHall());
                        }
                        catch { enlistKit = false; }
                        if (enlistKit && heroReq)
                        {
                            for (int slot = 0; slot <= 9; slot++)
                            {
                                if (slot == 4) continue;                       // sztandar
                                var it = eq[(EquipmentIndex)slot].Item;
                                if (it == null) continue;
                                var mine = ownEq[(EquipmentIndex)slot];
                                if (mine.Item == it) continue;
                                string whyNot;
                                if (ItemReq.MeetsHero(soldier, it, out whyNot)) continue;
                                string got;
                                if (!mine.IsEmpty && mine.Item.ItemType == it.ItemType)
                                {
                                    eq[(EquipmentIndex)slot] = mine;
                                    got = "bierze swoj " + mine.Item.StringId;
                                }
                                else
                                {
                                    ItemObject best;
                                    if (slot >= 5)
                                        best = SkillsDecide.TopArmor(it.ItemType, soldier.GetSkillValue(TaleWorlds.Core.DefaultSkills.Athletics), soldier.Culture);
                                    else
                                    {
                                        var rsk0 = ItemReq.SkillFor(it);
                                        best = SkillsDecide.PatternFor(it.ItemType, rsk0 != null ? soldier.GetSkillValue(rsk0) : 0);
                                    }
                                    if (best != null)
                                    {
                                        eq[(EquipmentIndex)slot] = new EquipmentElement(best);
                                        got = "dostaje " + best.StringId + " (najlepsza tego typu w granicy umiejetnosci)";
                                    }
                                    else got = "lzejszej sztuki tego typu brak - zostaje przy wydanej";
                                }
                                if (_e8Log++ < 20)
                                    Log.Info("Z16: zaciag - bohater " + soldier.StringId + " nie udzwignie wydanego " + it.StringId
                                             + " (" + whyNot + ") - " + got + ".");
                            }
                        }
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
                        // ZASADA NADRZEDNA U BOHATEROW (Jeff 14.09: "AI ma miec takie same
                        // ograniczenia, nie tylko gracz - sprzet dostosowany do umiejetnosci").
                        // Lord AI, kompan i gracz walcza tym, co udzwigna: bron ponad skill
                        // -> wzorzec klasy w ramach skilla, pancerz ponad Atletyke -> najlepszy
                        // dozwolony, kon ponad Riding -> najlepszy dozwolony (albo pieszo).
                        // NAZWANE klingi i zbroje person (legendy, unikaty) zostaja - to lore,
                        // nie lup. Gdy nie ma czym podmienic, bohater zostaje przy swoim
                        // (lorda nie rozbrajamy). Tylko na scenie - ekwipunek w save nietkniety.
                        // Z16-5 (decyzja 3 Jeffa 09.10: "zbroja juz noszona NIE jest zdejmowana na sile"): przy wlaczonym
                        // HeroGearRequirements regula sceny pomija WLASNE sztuki bohatera (ta sama sztuka co w jego zestawie
                        // bojowym w tym slocie) - dotyczy to lotu smokiem gracza (ROT klonuje jego zestaw), wlasnych slotow
                        // zaciagu (GearOverrides) i pojedynkow ROT; sito zostaje dla sztuk WYDANYCH. Przy wylaczonym - jak 14.09.
                        bool keepOwn = heroReq && ownEq != null;
                        for (int slot = 0; slot < 4; slot++)
                        {
                            var w = eq[(EquipmentIndex)slot].Item;
                            if (w == null || LegendaryLaw.IsLegend(w) || UniqueGear.Is(w) || GiantGear.Is(w)) continue;
                            if (keepOwn && ownEq[(EquipmentIndex)slot].Item == w) continue;   // Z16-5: wlasna - zostaje
                            string whyNot;
                            if (ItemReq.Meets(soldier, w, out whyNot)) continue;
                            var rsk = ItemReq.SkillFor(w);
                            int sk = rsk != null ? soldier.GetSkillValue(rsk) : 0;
                            var swap = SkillsDecide.PatternFor(w.ItemType, sk);
                            if (swap == null) continue;
                            eq[(EquipmentIndex)slot] = new EquipmentElement(swap);
                            if (_floorLog++ < 20)
                                Log.Info("ItemReq: bohater " + soldier.StringId + " nie udzwignie " + w.StringId
                                         + " (" + whyNot + ") - dostaje " + swap.StringId + ".");
                        }
                        for (int slot = 5; slot <= 9; slot++)
                        {
                            var a = eq[(EquipmentIndex)slot].Item;
                            if (a == null || UniqueGear.Is(a) || GiantGear.Is(a) || a.NotMerchandise) continue;   // zbroje person zostaja
                            if (keepOwn && ownEq[(EquipmentIndex)slot].Item == a) continue;   // Z16-5: wlasna - zostaje (decyzja 3)
                            if (ItemReq.Meets(soldier, a)) continue;
                            var top = SkillsDecide.TopArmor(a.ItemType, hath, soldier.Culture);
                            if (top == null) continue;
                            eq[(EquipmentIndex)slot] = new EquipmentElement(top);
                            if (_floorLog++ < 20)
                                Log.Info("ItemReq: bohater " + soldier.StringId + " nie udzwignie " + a.StringId
                                         + " (Atletyka " + hath + " < " + a.Difficulty + ") - dostaje " + top.StringId + ".");
                        }
                        var hm2 = eq[(EquipmentIndex)10].Item;
                        if (hm2 != null && !hm2.StringId.StartsWith("dragon_") && !ItemReq.Meets(soldier, hm2))
                        {
                            var htop2 = SkillsDecide.TopMount(soldier.GetSkillValue(TaleWorlds.Core.DefaultSkills.Riding));
                            eq[(EquipmentIndex)10] = htop2 != null ? new EquipmentElement(htop2) : new EquipmentElement(null);
                            var hh2 = eq[(EquipmentIndex)11].Item;
                            var hmc2 = htop2 != null && htop2.HorseComponent != null ? htop2.HorseComponent.Monster : null;
                            if (htop2 == null || (hh2 != null && hh2.ArmorComponent != null && hmc2 != null
                                                  && hh2.ArmorComponent.FamilyType != hmc2.FamilyType))
                                eq[(EquipmentIndex)11] = new EquipmentElement(null);
                            if (_floorLog++ < 20)
                                Log.Info("ItemReq: bohater " + soldier.StringId + " nie udzwignie " + hm2.StringId
                                         + " (Riding) - " + (htop2 != null ? "dostaje " + htop2.StringId : "idzie pieszo") + ".");
                        }
                        // Z16 E8: ROT buduje bron agenta (AgentOverridenSpawnMissionEquipment) z zestawu PRZED nami - gra bierze
                        // bron z niej, nie ze slotow 0-3 (Mission.SpawnAgent: InitializeMissionEquipment). Gdy w zestawie zaciagu
                        // sloty broni sie zmienily (sito zaciagu albo podmiany wyzej), bron przebudowana z poprawionego zestawu -
                        // tak samo jak robi to ROT (new MissionEquipment(zestaw, AgentBanner)); poza zaciagiem - bez zmian.
                        bool wChanged = false;
                        for (int slot = 0; slot < 4; slot++) if (eq[(EquipmentIndex)slot].Item != w0[slot]) wChanged = true;
                        if (enlistKit && wChanged && agentBuildData.AgentOverridenSpawnMissionEquipment != null)
                        {
                            try
                            {
                                if (_pMissionEq == null)
                                    _pMissionEq = AccessTools.Property(typeof(AgentBuildData), "AgentOverridenSpawnMissionEquipment");
                                if (_pMissionEq != null)
                                    _pMissionEq.SetValue(agentBuildData, new MissionEquipment(eq, agentBuildData.AgentBanner));
                            }
                            catch (Exception e8) { if (_e8Log++ < 20) Log.Error("DragonUnmount.E8 MissionEquipment", e8); }
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
