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

        // 133c (K3): kody decyzji WouldStrip - > 0 sztuka schodzi (repl - czym ja zastapic, null - pusty slot), 0 zostaje, StayHeavy - ponad umiejetnosc, ale
        // lzejszej sztuki tego typu nie ma (zostaje przy swojej - PODLOGA SPRZETU, Jeff 31.08)
        internal const int Keeps = 0, StayHeavy = -1, StripGiant = 1, StripLegend = 2, StripSkill = 3, StripMountLand = 4, StripMountSkill = 5;

        /// <summary>133c (K3, krytyka 133): STRAZ PRZY WYSTAWIENIU SZEREGOWEGO jako czysta funkcja - wolana przez StripDragonMount (bitwa) i przez KitPreview
        /// (okno armii, podglad nastepnej bitwy), wiec jedna decyzja dla bitwy i podgladu. Logika bez zmian wobec dotychczasowego StripDragonMount:
        ///  - bron 0-3: SPRZET OLBRZYMOW NA CZLOWIEKU (Jeff 14.09) schodzi zawsze - zamiennik PatternFor wedlug umiejetnosci broni (RelevantSkill), bez niego
        ///    goly slot; PRAWO LEGEND (prog 100k + lista person) - zamiennik LegendaryLaw.ReplacementFor, potem wzorzec klasy (PatternFor), dopiero nic;
        ///    bron dana swiadomie przez podloge broni (floorKept - ostatnia deska GearFit) zostaje; ZASADA NADRZEDNA - ponad umiejetnosc (ItemReq) schodzi
        ///    do najlepszej w ramach umiejetnosci (PatternFor), a gdy takiej nie ma - zostaje (StayHeavy);
        ///  - pancerz 5-9: olbrzymi - TopArmor w ramach Atletyki (bez niego goly slot); ponad Atletyke - TopArmor, a gdy brak - zostaje (StayHeavy);
        ///  - kon 10: GEOGRAFIA WIERZCHOWCOW (MountLaw - mamut, wielblad, rydwan, slon nie u swoich) albo ponad Jazde - TopMount (null - pieszo).
        /// Bohater i pusty slot - Keeps. Zamienniki sa z niczego - przy wystawieniu straz "nic z niczego" (GarrisonKit.SpawnGuard) zostawia je tylko z pokryciem
        /// w zbrojowni. TopArmor losuje sztuke z pasma (jedno wywolanie na slot - jak dotad).</summary>
        internal static int WouldStrip(TaleWorlds.CampaignSystem.CharacterObject soldier, int slot, ItemObject it, bool floorKept, out ItemObject repl, out string whyNot)
        {
            repl = null; whyNot = null;
            if (soldier == null || soldier.IsHero || it == null) return Keeps;
            if (slot >= 0 && slot < 4)
            {
                if (GiantGear.Is(it) && !GiantGear.MayWear(soldier))
                {
                    int gs = it.RelevantSkill != null ? soldier.GetSkillValue(it.RelevantSkill) : 0;
                    repl = SkillsDecide.PatternFor(it.ItemType, gs);
                    return StripGiant;
                }
                if (LegendaryLaw.IsLegend(it))
                {
                    repl = LegendaryLaw.ReplacementFor(it);
                    if (repl == null)
                    {
                        var rsk = ItemReq.SkillFor(it);
                        int skl = rsk != null ? soldier.GetSkillValue(rsk) : 0;
                        repl = SkillsDecide.PatternFor(it.ItemType, skl);
                    }
                    return StripLegend;
                }
                if (floorKept) return Keeps;
                if (ItemReq.Meets(soldier, it, out whyNot)) return Keeps;
                var rsk2 = ItemReq.SkillFor(it);
                int sk = rsk2 != null ? soldier.GetSkillValue(rsk2) : 0;
                repl = SkillsDecide.PatternFor(it.ItemType, sk);
                return repl != null ? StripSkill : StayHeavy;
            }
            if (slot >= 5 && slot <= 9)
            {
                int ath = soldier.GetSkillValue(TaleWorlds.Core.DefaultSkills.Athletics);
                if (GiantGear.Is(it) && !GiantGear.MayWear(soldier)) { repl = SkillsDecide.TopArmor(it.ItemType, ath, soldier.Culture); return StripGiant; }
                if (ItemReq.Meets(soldier, it, out whyNot)) return Keeps;
                repl = SkillsDecide.TopArmor(it.ItemType, ath, soldier.Culture);
                return repl != null ? StripSkill : StayHeavy;
            }
            if (slot == 10)
            {
                bool geo = !MountLaw.Allowed(soldier, it);
                if (!geo && ItemReq.Meets(soldier, it, out whyNot)) return Keeps;
                repl = SkillsDecide.TopMount(soldier.GetSkillValue(TaleWorlds.Core.DefaultSkills.Riding));
                return geo ? StripMountLand : StripMountSkill;
            }
            return Keeps;
        }

        /// <summary>Recenzja 133 (K3): czy uprzaz zostaje na koniu-zamienniku (TopMount z WouldStrip) - rodzina uprzezy taka jak wierzchowca (Jeff 14.09, crash
        /// 11:17:58: inna rodzina = natywny AccessViolation w AddMountMesh). Logika 1:1 jak dotad w StripDragonMount; wolana tez przez KitPreview (podglad).
        /// Brak uprzezy, konia, ArmorComponent albo Monster - true (nic do zdjecia, jak dotad).</summary>
        internal static bool HarnessFitsMount(ItemObject mount, ItemObject harness)
        {
            if (mount == null || harness == null || harness.ArmorComponent == null) return true;
            var mc = mount.HorseComponent != null ? mount.HorseComponent.Monster : null;
            return mc == null || harness.ArmorComponent.FamilyType == mc.FamilyType;
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
                        // 133c (K3): decyzja "zostaje / schodzi i czym zastapic" w jednej funkcji WouldStrip (ta sama dla bitwy i podgladu okna armii - KitPreview);
                        // tu tylko zapis slotu i linie logu - jak dotad
                        for (int slot = 0; slot < 4; slot++)
                        {
                            var w = eq[(EquipmentIndex)slot].Item;
                            if (w == null) continue;
                            // GearFit (noc 15/16.10): bron ponad umiejetnosc dala swiadomie podloga broni (ostatnia deska) - FloorKept pytany jak dotad tylko
                            // dla sztuk, ktore nie sa olbrzymie ani legendarne (licznik _dKept bez zmian)
                            bool fk = !(GiantGear.Is(w) && !GiantGear.MayWear(soldier)) && !LegendaryLaw.IsLegend(w) && GearFit.FloorKept(eq, slot);
                            ItemObject repl; string whyNot;
                            int code = WouldStrip(soldier, slot, w, fk, out repl, out whyNot);
                            if (code == StripGiant)
                            {
                                eq[(EquipmentIndex)slot] = repl != null ? new EquipmentElement(repl) : new EquipmentElement(null);
                                if (_floorLog++ < 20)
                                    Log.Info("GiantGear: " + w.StringId + " zdjety z " + soldier.StringId
                                             + " - dostaje " + (repl != null ? repl.StringId : "goly slot") + ".");
                            }
                            else if (code == StripLegend)
                            {
                                eq[(EquipmentIndex)slot] = repl != null
                                    ? new EquipmentElement(repl) : new EquipmentElement(null);
                                Log.Info("LegendaryLaw: " + w.StringId + " zdjety przy spawnie z szeregowego ("
                                         + soldier.StringId + ") - dostaje "
                                         + (repl != null ? repl.StringId : "goly slot") + ".");
                            }
                            else if (code == StripSkill)
                            {
                                eq[(EquipmentIndex)slot] = new EquipmentElement(repl);
                                Log.Info("ItemReq: " + soldier.StringId + " nie udzwignie " + w.StringId
                                         + " (" + whyNot + ") - dostaje " + repl.StringId + ".");
                            }
                            else if (code == StayHeavy && _floorLog++ < 20)
                                Log.Info("ItemReq: " + soldier.StringId + " nie udzwignie " + w.StringId
                                         + " (" + whyNot + "), ale nie ma lzejszej broni - zostaje przy swojej.");
                        }
                        // pancerz ponad atletyke -> najlepszy dozwolony; kon ponad
                        // Riding -> najlepszy dozwolony (albo pieszo)
                        int ath = soldier.GetSkillValue(TaleWorlds.Core.DefaultSkills.Athletics);
                        for (int slot = 5; slot <= 9; slot++)
                        {
                            var a = eq[(EquipmentIndex)slot].Item;
                            if (a == null) continue;
                            ItemObject top; string whyA;
                            int code = WouldStrip(soldier, slot, a, false, out top, out whyA);
                            if (code == StripGiant)
                            {
                                eq[(EquipmentIndex)slot] = top != null ? new EquipmentElement(top) : new EquipmentElement(null);
                                if (_floorLog++ < 20)
                                    Log.Info("GiantGear: " + a.StringId + " zdjety z " + soldier.StringId
                                             + " - dostaje " + (top != null ? top.StringId : "goly slot") + ".");
                            }
                            else if (code == StripSkill) eq[(EquipmentIndex)slot] = new EquipmentElement(top);
                            else if (code == StayHeavy && _floorLog++ < 20)
                                Log.Info("ItemReq: " + soldier.StringId + " - brak lzejszego pancerza ("
                                         + a.ItemType + ") w ramach Atletyki " + ath + "; zostaje w swoim.");
                        }
                        var mnt = eq[(EquipmentIndex)10].Item;
                        ItemObject topM = null; string whyM;
                        int mcode = mnt != null ? WouldStrip(soldier, 10, mnt, false, out topM, out whyM) : Keeps;
                        bool geo = mcode == StripMountLand;
                        if (mcode == StripMountLand || mcode == StripMountSkill)
                        {
                            eq[(EquipmentIndex)10] = topM != null
                                ? new EquipmentElement(topM) : new EquipmentElement(null);
                            if (topM == null) eq[(EquipmentIndex)11] = new EquipmentElement(null);
                            else
                            {
                                // UPRZAZ MUSI PASOWAC DO NOWEGO KONIA (Jeff 14.09, crash
                                // 11:17:58): rodzina inna niz wierzchowca = natywny
                                // AccessViolation w AddMountMesh - wtedy bez uprzezy
                                // (recenzja 133, K3: HarnessFitsMount - ta sama funkcja w podgladzie okna armii)
                                if (!HarnessFitsMount(topM, eq[(EquipmentIndex)11].Item))
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
                            // m111-walka (24.10): w turnieju bez limitu 20 linii - lista broni wydanych rycerzom (dotad 4 linie na caly turniej KP)
                            if (_floorLog++ < 20 || BattleSense.TourneyNow())
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
