using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// Prawo pola bitwy. Naprawia potok lupow miedzy Dynamic Troop Equipment
    /// a Spoils of War (RealisticLoot):
    ///
    /// 1. W bitwach STOCZONYCH OSOBISCIE lupem rzadzi DTE - zabici zostawiaja
    ///    faktyczny ekwipunek (czesc trafiona ciosem smiertelnym przepada),
    ///    wojsko czerpie z Armoury, a dzialka gracza wg wkladu w bitwe trafia
    ///    na ekran lupow. Spoils of War dotad WYCINAL ten ekran (prefix na
    ///    PlayerEncounter.DoLootInventory) i podstawial wlasna loterie -
    ///    nasz postfix na jego prefixie kaze mu ustapic.
    /// 2. Przedmioty z ekranu lupow wygranej, stoczonej bitwy dostaja stan
    ///    (modyfikatory zuzycia Spoils of War) - uszkodzone, ale z wartoscia.
    /// 3. W bitwach AUTOMATYCZNYCH Spoils zostaje, ale bez zaszytych mnoznikow
    ///    tieru (T1 x0.18 itd.) - reszte bramek zdjeto configiem.
    ///
    /// Wszystko przez refleksje - dziala tez, gdy ktoregos z tych modow nie ma.
    /// </summary>
    internal static class BattlefieldLaw
    {
        private static PropertyInfo _vanillaHandledSetter;
        private static MethodInfo _applyDamage;
        private static bool _rlPresent;
        // PRAWO ZACHOWANIA LUPU aktywne: vanilla loteria szablonowa za
        // poleglych/rannych wycieta w bitwach osobistych (DTE + jency
        // placa fizycznie) - czytane przez ArmouryBehavior przy jencach
        internal static bool CasualtyLootCut;

        // dzialka gracza wyjeta z magazynu DTE, czekajaca na ekran lupow
        private static readonly ItemRoster ShareQueue = new ItemRoster();
        private static FieldInfo _dteRecords;      // DynamicTroopMissionLogic._partyBattleRecords
        private static FieldInfo _dteLootedItems;  // PartyBattleRecord.LootedItems
        private static FieldInfo _dteArmory;       // ArmyArmory.Armory (ItemRoster)

        // wraki: czesci rozbite ciosem smiertelnym (DTE je wyrzucal - my je zbieramy)
        private static readonly List<ItemObject> Wrecks = new List<ItemObject>();
        private static ItemObject _lastPick;
        private static MethodInfo _rlEnsureCache;      // EquipmentDamageModel.EnsureCacheInitialized
        private static FieldInfo _rlHeavy;             // EquipmentDamageModel._lootedHeavy
        private static FieldInfo _rlHeavyMax;          // EquipmentDamageModel._lootedHeavyMax
        private static MethodInfo _rlRegisterLucky;    // RealisticLootModel.RegisterLuckyElement

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var t = asm.GetType(fullName); if (t != null) return t; } catch { }
            }
            return null;
        }

        // czy ostatnio konczona bitwa byla stoczona osobiscie (nie symulacja)
        private static bool RealFoughtBattle()
        {
            try
            {
                var ev = MapEvent.PlayerMapEvent ?? PlayerEncounter.Battle;
                if (ev == null) return false;
                return !ev.IsPlayerSimulation;
            }
            catch { return false; }
        }

        private static bool RotEnlisted()
        {
            try
            {
                var sub = Type.GetType("ROT.SubModule, ROT");
                if (sub == null) return false;
                var f = sub.GetField("EnlistmentBehavior", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var beh = f != null ? f.GetValue(null) : null;
                if (beh == null) return false;
                var fi = beh.GetType().GetField("IsEnlisted", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var v = fi != null ? fi.GetValue(beh) : null;
                return v is bool && (bool)v;
            }
            catch { return false; }
        }

        /// <summary>
        /// Prefix na RealisticLoot.LootCollectionBehavior.OnMapEventEnded.
        /// Gdy sluzysz w armii lorda (ROT enlisted), caly potok Spoils - rytual
        /// zbierania, setki przedmiotow, zloto z cial - NIE odpala sie dla Ciebie.
        /// Pole sprzata armia; szeregowiec zostaje w szeregu.
        /// </summary>
        internal static bool SoldierStandsDown(MapEvent mapEvent)
        {
            try
            {
                if (!Settings.Current.EnlistedSoldierNoLooting) return true;
                if (!RotEnlisted()) return true;
                if (mapEvent != null && mapEvent.IsPlayerMapEvent
                    && mapEvent.WinningSide == mapEvent.PlayerSide)
                {
                    TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                        "The quartermasters strip the field. A soldier keeps his place in the line.",
                        TaleWorlds.Library.Colors.Yellow));
                    Log.Info("BattlefieldLaw: bitwa armii wygrana w sluzbie - lup zostaje przy armii.");
                }
                return false;
            }
            catch { return true; }
        }

        internal static void ApplyAll(Harmony h)
        {
            if (!Settings.Current.BattlefieldLawEnabled) { Log.Info("BattlefieldLaw: wylaczone w ustawieniach."); return; }
            try
            {
                var skipType = Type.GetType("RealisticLoot.Patches.SkipVanillaLootPatch, RealisticLoot");
                var modelType = Type.GetType("RealisticLoot.Models.RealisticLootModel, RealisticLoot");
                var dmgType = Type.GetType("RealisticLoot.Models.EquipmentDamageModel, RealisticLoot");
                _rlPresent = skipType != null;

                if (modelType != null)
                {
                    if (Settings.Current.SimBattleFullDrop)
                    {
                        var tierMult = AccessTools.Method(modelType, "GetTierDropMultiplier");
                        if (tierMult != null)
                        {
                            h.Patch(tierMult, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "FullTierMultiplier"));
                            Log.Info("BattlefieldLaw: bitwy automatyczne bez mnoznikow tieru.");
                        }
                    }
                    // rytual Spoils (godziny zbierania, oboz, zloto z cial) zostaje;
                    // podmieniamy tylko ZAWARTOSC jego ekranu na prawdziwy lup z DTE
                    var gen = AccessTools.Method(modelType, "GenerateLootFromUnitList");
                    if (gen != null)
                    {
                        h.Patch(gen, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "AfterGenerateLoot"));
                        Log.Info("BattlefieldLaw: ekran Spoils dostaje faktyczny lup z pola (DTE), loteria za burta.");
                    }
                }
                else Log.Info("BattlefieldLaw: RealisticLoot nieobecny - lup DTE plynie ekranem vanilla.");

                // zaciezny zolnierz w cudzej armii NIE rozbiera calego pola bitwy:
                // rytual Spoils (godziny, lup, zloto z cial) nalezy do dowodcy, nie szeregowca
                var collType = Type.GetType("RealisticLoot.Behaviors.LootCollectionBehavior, RealisticLoot");
                if (collType != null)
                {
                    var omee = AccessTools.Method(collType, "OnMapEventEnded");
                    if (omee != null)
                    {
                        h.Patch(omee, prefix: new HarmonyMethod(typeof(BattlefieldLaw), "SoldierStandsDown"));
                        Log.Info("BattlefieldLaw: zaciezny nie lupi pola bitwy armii (kwatermistrz to robi).");
                    }
                }

                // TABOR SPOILS WYCIETY NA STALE (Jeff 30.08: "robimy to").
                // Spoils robi WLASNY, drugi ekran bagazy ze zdjecia z POCZATKU
                // bitwy - rownolegle do vanillowego podzialu wozow wg wkladu:
                // te same rzeczy wpadaly dwa razy, bez ogladania na sojusznikow.
                // Checkbox w MCM wraca przy kazdym presecie/resecie (kazdy
                // preset ma ON), wiec gasimy u zrodla: getter ustawienia
                // odpowiada false, caly kod taboru Spoils spi.
                var mcmType = Type.GetType("RealisticLoot.Settings.MCMSettings, RealisticLoot");
                var gBag = mcmType != null ? AccessTools.PropertyGetter(mcmType, "EnableBaggageTrain") : null;
                if (gBag != null)
                {
                    h.Patch(gBag, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "NoBaggageTrain"));
                    Log.Info("BattlefieldLaw: tabor Spoils (Baggage Train) wyciety na stale - wozy dzieli vanilla wg wkladu.");
                }

                if (dmgType != null) _applyDamage = AccessTools.Method(dmgType, "ApplyDamageToLoot");
                var dispatcher = AccessTools.Method(typeof(CampaignEventDispatcher), "OnCollectLootItems");
                if (dispatcher != null)
                {
                    h.Patch(dispatcher, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "AfterCollectLoot"));
                    Log.Info("BattlefieldLaw: lupy z ekranu dostaja stan bojowy (zuzycie Spoils)."
                             + (_applyDamage == null ? " (Spoils nieobecny - bez zuzycia)" : ""));
                }

                // wpis 82: lup wedlug tego, kto powalil - liczymy powalonych wrogow (gracz/towarzysz vs zolnierz)
                {
                    var missionK = FindType("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                    var removedK = missionK != null ? AccessTools.Method(missionK, "OnAgentRemoved") : null;
                    if (removedK != null)
                    {
                        h.Patch(removedK, postfix: new HarmonyMethod(typeof(BattlefieldLaw), nameof(CountKill)));
                        Log.Info("BattlefieldLaw: dzialka gracza wedlug tego, kto powalil wroga (Ty i towarzysze 100%, zolnierze 1/3 dla kapitana).");
                    }
                }

                // wraki: podgladamy, ktora czesc DTE uznal za rozbita ciosem, i zamiast
                // pozwolic jej zniknac, kladziemy ja na ekranie lupow jako zlom do naprawy
                if (Settings.Current.WreckSalvageEnabled)
                {
                    var selector = FindType("DynamicTroopEquipmentReupload.ArmorSelector");
                    var mission = FindType("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                    var pickM = selector != null ? AccessTools.Method(selector, "GetRandomArmorByBodyPart") : null;
                    var removedM = mission != null ? AccessTools.Method(mission, "OnAgentRemoved") : null;
                    if (pickM != null && removedM != null)
                    {
                        h.Patch(pickM, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "RecordStruckPiece"));
                        h.Patch(removedM, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "QueueWreck"));
                        Log.Info("BattlefieldLaw: czesci rozbite ciosem wracaja jako wraki do naprawy.");
                    }
                    if (modelType != null)
                    {
                        _rlRegisterLucky = AccessTools.Method(modelType, "RegisterLuckyElement");
                        if (dmgType != null)
                        {
                            _rlEnsureCache = AccessTools.Method(dmgType, "EnsureCacheInitialized");
                            _rlHeavy = AccessTools.Field(dmgType, "_lootedHeavy");
                            _rlHeavyMax = AccessTools.Field(dmgType, "_lootedHeavyMax");
                        }
                    }
                }

                // PRAWO ZACHOWANIA LUPU (Jeff 30.08: "nie mozemy dostac NIC
                // ponad to, co ma wroga armia"). W bitwie STOCZONEJ OSOBISCIE
                // sprzet wroga placi sie fizycznie: zabity - DTE zdejmuje go
                // ze sceny do magazynu partii ZABOJCY; ranny wziety do niewoli -
                // obszukanie jenca; ranny, ktory uciekl - nic (uciekl w swoim).
                // Vanilla ROWNOLEGLE generowala DRUGI komplet z SZABLONOW za
                // kazdego zabitego I rannego pokonanych (LootDefeatedParty
                // Casualties) - czysta inflacja materii. Wycinamy ja w bitwach
                // osobistych; symulacje ida po staremu.
                var mCas = AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties");
                if (mCas != null)
                {
                    h.Patch(mCas, prefix: new HarmonyMethod(typeof(BattlefieldLaw), "CutCasualtyLootPrefix"));
                    Log.Info("BattlefieldLaw: prawo zachowania lupu - szablonowa loteria za poleglych wycieta w bitwach osobistych.");
                }

                // dzialka gracza z tego, co DTE sciagnal z zabitych
                var dteMission = FindType("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                var dteRecord = FindType("DynamicTroopEquipmentReupload.PartyBattleRecord");
                var dteArmoryType = FindType("DynamicTroopEquipmentReupload.ArmyArmory");
                if (dteMission != null && dteRecord != null && dteArmoryType != null)
                {
                    _dteRecords = AccessTools.Field(dteMission, "_partyBattleRecords");
                    _dteLootedItems = AccessTools.Field(dteRecord, "LootedItems");
                    _dteArmory = AccessTools.Field(dteArmoryType, "Armory");
                    var handle = AccessTools.Method(dteMission, "HandlePartyItems");
                    if (handle != null && _dteRecords != null && _dteLootedItems != null && _dteArmory != null)
                    {
                        h.Patch(handle, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "AfterDtePartyItems"));
                        // prawo zachowania lupu dziala tylko, gdy DTE realnie
                        // placi za zabitych - bez DTE vanilla loteria zostaje
                        CasualtyLootCut = mCas != null;
                        Log.Info("BattlefieldLaw: dzialka gracza z lupow DTE trafia na ekran (solo 100%, z druzyna "
                                 + Settings.Current.PlayerLootSharePercent + "%)."
                                 + (CasualtyLootCut ? " Jency z bitew ida pod obszukanie (placa fizycznie za rannych)." : ""));
                        // E8/A (250, odpowiedz Jeffa 12.10 nr 1): lup cialo po ciele - kto powalil wlasciciela sztuki dopisywanej do lupu (latki wpiete zawsze,
                        // klucz OwnKillsLootWhole czytany w LootTag i AfterDtePartyItems; bez latek dzialka liczy sie srednia z puli jak dotad)
                        MethodInfo removedL = null, addL = null;
                        try
                        {
                            removedL = AccessTools.Method(dteMission, "OnAgentRemoved");
                            addL = AccessTools.Method(dteRecord, "AddLootedItem", new[] { typeof(ItemObject) });
                            if (removedL != null && addL != null)
                            {
                                h.Patch(removedL, prefix: new HarmonyMethod(typeof(BattlefieldLaw), nameof(LootKillPrefix)),
                                        finalizer: new HarmonyMethod(typeof(BattlefieldLaw), nameof(LootKillFinalizer)));
                                _lootWired++;
                                h.Patch(addL, prefix: new HarmonyMethod(typeof(BattlefieldLaw), nameof(LootTag)));
                                _lootWired++;
                            }
                        }
                        catch (Exception e) { Log.Error("BattlefieldLaw.ApplyAll (E8/A)", e); }
                        Log.Info("Misja gracza (E8/A): start - lup z wlasnych zabojstw w calosci [" + (Settings.Current.OwnKillsLootWhole ? "wl" : "wyl")
                                 + "], latki wpiete " + _lootWired + "/2 (DTE DynamicTroopMissionLogic.OnAgentRemoved - zabojca, PartyBattleRecord.AddLootedItem - sztuka)"
                                 + (_lootWired == 2 ? "" : " | nie znaleziono: " + (removedL == null ? "OnAgentRemoved " : "") + (addL == null ? "AddLootedItem(ItemObject) " : "")
                                                           + (removedL != null && addL != null ? "(wyjatek przy wpinaniu)" : "")) + ".");
                    }
                    else Log.Info("BattlefieldLaw: nie znalazlem wnetrza DTE - dzialka gracza nieaktywna.");
                }
                else Log.Info("BattlefieldLaw: DTE nieobecny - dzialka gracza nieaktywna.");
            }
            catch (Exception e) { Log.Error("BattlefieldLaw.ApplyAll", e); }
        }

        /// <summary>
        /// Ekran Spoils po stoczonej bitwie: zamiast wylosowanych ochlapow -
        /// dokladnie to, co lezalo na polu (dzialka gracza z DTE + udzial w bagazach).
        /// Zloto z cial, godziny zbierania i oboz licza sie w Spoils jak dotad;
        /// zuzycie tez nalozy Spoils (robi to po tej metodzie).
        /// </summary>
        public static void AfterGenerateLoot(ref ItemRoster __result)
        {
            try
            {
                ScreenIsRealLoot = false;   // E8/L (252): do nastepnej podmiany - lista trofeow Spoils to loteria (z niczego)
                if (!Settings.Current.BattlefieldLawEnabled) return;
                // 177-2 (krytyka pkt 2): loteria Spoils losuje z PRAWDZIWEGO zestawu pokonanego bohatera (GetRandomBattleEquipment) i dodaje KOPIE -
                // legenda i stal valyrianska z loterii to zawsze kopia (klinge bohatera bierze, jesli w ogole, zwyczaj wojenny - UniqueSpoils.Take);
                // w bitwie stoczonej osobiscie to samo robi CleanseTrash nizej, w symulacji dotad nic
                if (__result != null && Settings.Current.NoConjuredLegends) StripLegendCopies(__result);
                if (RotEnlisted()) { ShareQueue.Clear(); Wrecks.Clear(); return; }
                bool sim = false;
                if (!RealFoughtBattle())
                {
                    // E8/L (252, PLAN 8.6c): "Send troops" wygrane przez gracza - jedna droga lupu. Za tych samych poleglych placi juz DTE (lup z cial do
                    // zbrojowni druzyny, dzialka kapitana w kolejce - HorseFate.SimShare), wiec loteria Spoils schodzi z ekranu jak w bitwie stoczonej
                    sim = SimOneLoot(MapEvent.PlayerMapEvent ?? PlayerEncounter.Battle);
                    if (!sim) return;                    // pozostale symulacje: zostaje loteria Spoils (odbramkowana)
                }
                if (__result == null) __result = new ItemRoster();
                if (sim)
                {
                    int lot = 0;
                    for (int i = 0; i < __result.Count; i++) { var le = __result.GetElementCopyAtIndex(i); if (le.Amount > 0) lot += le.Amount; }
                    _simCutSpoils += lot; _simCutBattles++;
                    Log.Info("BattlefieldLaw (E8/L): Send troops - loteria Spoils pominieta (" + lot + " szt. z niczego), na ekran idzie dzialka kapitana z lupu DTE ("
                             + ShareQueue.Count + " pozycji w kolejce" + (ShareQueue.Count == 0 ? " - kolejka pusta: dzialka 0 albo DTE rozlicza po Spoils, wtedy trafi do sakw po godzinie" : "") + ").");
                }
                // AUDYT LUPOW (Jeff 29.08: "czy nie ma podwojnego otrzymywania?"):
                // realna bitwa NIGDY nie zostawia vanillowej loterii na ekranie -
                // nawet gdy kolejka jest pusta (np. flush godzinowy juz oddal
                // dzialke do sakw; loteria na wierzchu bylaby DRUGA nagroda)
                __result.Clear();
                ScreenIsRealLoot = true;    // E8/L (252): od teraz na liscie trofeow sa tylko prawdziwe sztuki z pola (albo nic)
                if (ShareQueue.Count == 0 && Wrecks.Count == 0) return;
                for (int i = 0; i < ShareQueue.Count; i++)
                {
                    var el = ShareQueue.GetElementCopyAtIndex(i);
                    __result.AddToCounts(el.EquipmentElement, el.Amount);
                }
                ShareQueue.Clear();
                int wrecks = AppendWrecks(__result);
                CleanseDragons(__result);
                CleanseTrash(__result);
                Log.Info("BattlefieldLaw: ekran Spoils podmieniony na " + __result.Count + " pozycji prawdziwego lupu"
                         + (wrecks > 0 ? " (w tym " + wrecks + " wrakow przed przesianiem)" : "") + ".");
            }
            catch (Exception e) { Log.Error("AfterGenerateLoot", e); }
        }

        private static void StripLegendCopies(ItemRoster roster)
        {
            int n = 0;
            var ids = new List<string>();
            for (int i = roster.Count - 1; i >= 0; i--)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !(LegendaryLaw.IsLegend(it) || ValyrianBlades.Is(it))) continue;
                roster.AddToCounts(el.EquipmentElement, -el.Amount);
                n += el.Amount; ids.Add(it.StringId);
            }
            if (n > 0) Log.Info("BattlefieldLaw: lup Spoils bez kopii legend - " + n + " szt. (" + string.Join(", ", ids.ToArray()) + ") - klinga zostaje przy bohaterze albo idzie zwyczajem wojennym.");
        }

        /// <summary>
        /// SMIEC I LEGENDA NIE LEZA W WORKACH (Jeff 28.08). Smiec: sprzet zbity
        /// ponizej LootMinConditionPercent wartosci jest ZNISZCZONY - nie ma
        /// czego niesc (tnie tez zalew wrakow na 3%). Legenda: nazwane klingi
        /// ROT (Brightroar, Widow's Wail... value 100k+, po kilkadziesiat sztuk
        /// u elitarnych jednostek) nie moga lezec masowo w lupach - "unikat"
        /// ma byc unikatem.
        /// </summary>
        private static void CleanseTrash(ItemRoster roster)
        {
            try
            {
                if (roster == null) return;
                var s = Settings.Current;
                int trash = 0, legends = 0;
                for (int i = roster.Count - 1; i >= 0; i--)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    // SLON NIE LEZY W WORKACH (Jeff 29.08: "w bitwie nie bylo
                    // sloni, a po pladrowaniu mam slonie - wywal!"): pokonani
                    // wozili slonie w bagazach; bojowe slonie Zlotej Kompanii
                    // zyja w szablonach i ich nie ruszamy
                    if (it.StringId != null && (it.StringId == "elephant" || it.StringId.StartsWith("rot_elephant")))
                    {
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);
                        trash += el.Amount;
                        continue;
                    }
                    if (LegendaryLaw.IsLegend(it))
                    {
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);
                        legends += el.Amount;
                        continue;
                    }
                    // sprzet olbrzymow (Jeff 14.09): czlowiek go nie uzywa, wiec
                    // nie leci do worka - chyba ze gracz prowadzi olbrzymow
                    if ((GiantGear.Is(it) && !GiantGear.PartyHasGiants(MobileParty.MainParty))
                        || (MountLaw.IsExotic(it) && !MountLaw.AllowedForParty(MobileParty.MainParty, it)))
                    {
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);
                        legends += el.Amount;
                        continue;
                    }
                    if (s.LootMinConditionPercent > 0)
                    {
                        var mod = el.EquipmentElement.ItemModifier;
                        if (mod != null && mod.PriceMultiplier * 100f <= s.LootMinConditionPercent + 0.01f)
                        {
                            roster.AddToCounts(el.EquipmentElement, -el.Amount);
                            trash += el.Amount;
                        }
                    }
                }
                if (trash > 0 || legends > 0)
                    Log.Info("BattlefieldLaw: lup przesiany - " + trash + " szt. zniszczonych (<=" +
                             s.LootMinConditionPercent + "%) i " + legends + " legendarnych klng odpadlo.");
            }
            catch (Exception e) { Log.Error("CleanseTrash", e); }
        }

        /// <summary>
        /// SMOK TO NIE CHABETA (Jeff 28.08: "jakim cudem mam trzy smoki
        /// w ekwipunku?!"). ROT daje jednostkom Targaryenow wierzchowce
        /// dragon_* (Type=Horse), a gra wrzuca mounty pokonanych do lupu jak
        /// kazdego konia - is_merchandise=false przed tym nie chroni. Smoki
        /// sa fabularne: wycinamy je z KAZDEGO lupu bitewnego.
        /// </summary>
        private static void CleanseDragons(ItemRoster roster)
        {
            try
            {
                if (roster == null) return;
                int cut = 0;
                for (int i = roster.Count - 1; i >= 0; i--)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || it.StringId == null) continue;
                    if (!it.StringId.StartsWith("dragon_")) continue;
                    if (it.ItemType != ItemObject.ItemTypeEnum.Horse) continue;
                    roster.AddToCounts(el.EquipmentElement, -el.Amount);
                    cut += el.Amount;
                }
                if (cut > 0) Log.Info("BattlefieldLaw: " + cut + " smokow wycietych z lupu - smoki nie leza w workach.");
            }
            catch (Exception e) { Log.Error("CleanseDragons", e); }
        }

        /// <summary>Dzialka gracza z tego, co druzyna sciagnela z zabitych (DTE) - wyjeta z magazynu, do kolejki na ekran.</summary>
        public static void AfterDtePartyItems(object __instance, object partyId, bool isPlayerParty, bool isVictorious)
        {
            try
            {
                if (!Settings.Current.BattlefieldLawEnabled || !isPlayerParty || !isVictorious) return;
                if (RotEnlisted()) return;
                int pct = PlayerSharePercent();
                // E8/A (250, odpowiedz Jeffa 1): cialo po ciele - sztuki z cial powalonych przez Ciebie i towarzyszy w calosci, przez zolnierzy trzecia
                // kapitana, bez znanego zabojcy (lup morski z losowania DTE) srednia z puli jak dotad; brak znacznikow z tej misji = sama srednia (kod sprzed)
                bool whole = false;
                int third = 0, wHero = 0, wTroop = 0, wUnk = 0, wAvg = 0, wNew = 0;
                try
                {
                    third = Math.Max(0, Math.Min(100, Settings.Current.PlayerLootSharePercent));
                    lock (_lootHero)
                        whole = Settings.Current.OwnKillsLootWhole && ReferenceEquals(Mission.Current, _lootMission) && _lootHero.Count + _lootTroop.Count > 0;
                }
                catch { whole = false; }
                if (pct <= 0 && !whole) return;

                var armory = _dteArmory.GetValue(null) as ItemRoster;
                if (armory == null) return;

                // rekord bitewny partii gracza - jedyny z isPlayerParty
                var records = _dteRecords.GetValue(__instance) as System.Collections.IEnumerable;
                if (records == null) return;
                int moved = 0;
                foreach (var entry in records)
                {
                    var et = entry.GetType();
                    var key = et.GetProperty("Key").GetValue(entry, null);
                    if (key == null || partyId == null || !key.Equals(partyId)) continue;   // tylko rekord partii gracza
                    var looted = _dteLootedItems.GetValue(et.GetProperty("Value").GetValue(entry, null)) as System.Collections.IEnumerable;
                    if (looted == null) continue;
                    foreach (var kv in looted)
                    {
                        var kt = kv.GetType();
                        var item = kt.GetProperty("Key").GetValue(kv, null) as ItemObject;
                        int count = (int)kt.GetProperty("Value").GetValue(kv, null);
                        if (item == null || count <= 0) continue;
                        int share = Math.Min(count, (int)Math.Round(count * pct / 100.0, MidpointRounding.AwayFromZero));
                        if (whole)
                        {
                            int hk, tk;
                            lock (_lootHero) { _lootHero.TryGetValue(item, out hk); _lootTroop.TryGetValue(item, out tk); }
                            wAvg += share;
                            share = WholeShare(count, ref hk, ref tk, third, pct);
                            wHero += hk; wTroop += tk; wUnk += count - hk - tk; wNew += share;
                        }
                        if (share <= 0) continue;
                        // audyt pelny W1: zdejmujemy po wszystkich elementach tej sztuki RAZEM z ich stanem -
                        // wczesniej AddToCounts(item, -take) celowal w element bez modyfikatora, a kolejka i tak
                        // dostawala "take" (towar z niczego, gdy w magazynie byly tylko sztuki zuzyte)
                        int left = share;
                        for (int i = armory.Count - 1; i >= 0 && left > 0; i--)
                        {
                            var el = armory.GetElementCopyAtIndex(i);
                            if (el.EquipmentElement.Item != item || el.Amount <= 0) continue;
                            int t = Math.Min(left, el.Amount);
                            armory.AddToCounts(el.EquipmentElement, -t);
                            ShareQueue.AddToCounts(el.EquipmentElement, t);
                            left -= t; moved += t;
                        }
                    }
                    break;   // rekord partii gracza znaleziony i przetworzony
                }
                if (moved > 0) Log.Info("BattlefieldLaw: " + moved + " szt. (" + (whole ? "cialo po ciele, srednia z puli " : "") + pct + "%) z magazynu wojska czeka na ekran lupow.");
                if (whole)
                {
                    Log.Info("BattlefieldLaw (E8/A): lup cialo po ciele - z cial powalonych przez Ciebie i towarzyszy " + wHero + " szt. w calosci (powalonych wrogow "
                             + (ReferenceEquals(Mission.Current, _killMission) ? _killsHero : 0) + "), przez zolnierzy " + wTroop + " szt. x " + third
                             + "%, bez znanego zabojcy " + wUnk + " szt. x " + pct + "% | dzialka gracza " + wNew + " szt. (zdjete z magazynu " + moved
                             + "), srednia z puli dalaby " + wAvg + " szt. | potkniecia (sesja) " + _lootStumbles + ".");
                    lock (_lootHero) { _lootHero.Clear(); _lootTroop.Clear(); _lootMission = null; }
                }
            }
            catch (Exception e) { Log.Error("AfterDtePartyItems", e); }
        }

        // E8/A (250; odpowiedz Jeffa 12.10 nr 1 "to, co ja zabilem, idzie do mnie"): LUP CIALO PO CIELE. Wpis 82 liczyl dzialke gracza jako SREDNIA z puli
        // (Ty i towarzysze 100%, zolnierze trzecia) i bral ja po rowno z kazdej pozycji lupu - przy sredniej ponizej 50% pojedyncza sztuka z ciala, ktore
        // powaliles sam, zaokraglala sie do zera i zostawala w zbrojowni ludzi. Teraz kazda sztuka dopisywana przez DTE do lupu partii gracza pamieta, kto
        // powalil jej wlasciciela: Ty albo towarzysz - cala Twoja; zolnierz - trzecia kapitana (PlayerLootSharePercent); bez znanego zabojcy (lup morski
        // z losowania przy rozliczeniu) - srednia z puli jak dotad. Zabojca jak w CountKill (jezdziec za konia). Klucz OwnKillsLootWhole.
        [ThreadStatic] private static Agent _lootKiller;        // zabojca w biegu DTE OnAgentRemoved (prefiks ustawia, finalizer czysci)
        [ThreadStatic] private static int _lootKind;            // 0 poza OnAgentRemoved, -1 jeszcze nie policzone, 1 Ty / towarzysz, 2 zolnierz partii gracza, 3 inni
        private static object _lootMission;
        private static readonly Dictionary<ItemObject, int> _lootHero = new Dictionary<ItemObject, int>();    // sztuki z cial powalonych przez gracza i towarzyszy
        private static readonly Dictionary<ItemObject, int> _lootTroop = new Dictionary<ItemObject, int>();   // ... przez zolnierzy partii gracza
        private static int _lootWired, _lootStumbles;

        /// <summary>Dzialka gracza z jednej pozycji lupu (count sztuk): hero sztuk z cial powalonych przez gracza i towarzyszy - w calosci; troop sztuk z cial
        /// powalonych przez zolnierzy - third procent; reszta (bez znanego zabojcy) - pct procent (srednia z puli). Ulamek zaokraglany jak dotad (od zera),
        /// razem dla czesci zolnierskiej i nieznanej; hero i troop przyciete do count.</summary>
        internal static int WholeShare(int count, ref int hero, ref int troop, int third, int pct)
        {
            if (count <= 0) { hero = 0; troop = 0; return 0; }
            hero = Math.Max(0, Math.Min(count, hero));
            troop = Math.Max(0, Math.Min(count - hero, troop));
            int unk = count - hero - troop;
            return Math.Min(count, hero + (int)Math.Round((troop * third + unk * pct) / 100.0, MidpointRounding.AwayFromZero));
        }

        public static void LootKillPrefix(Agent __1) { _lootKiller = __1; _lootKind = -1; }

        public static void LootKillFinalizer() { _lootKiller = null; _lootKind = 0; }

        /// <summary>Prefiks DTE PartyBattleRecord.AddLootedItem(ItemObject): sztuka idzie do lupu partii zabojcy - zapamietaj, czy powalil bohater partii
        /// gracza, czy jej zolnierz (partia zabojcy = partia gracza, wiec to jej rekord). Tylko licznik; nie rzuca.</summary>
        public static void LootTag(ItemObject __0)
        {
            try
            {
                if (_lootKind == 0 || __0 == null) return;
                var s = Settings.Current;
                if (s == null || !s.OwnKillsLootWhole || !s.BattlefieldLawEnabled) return;
                if (_lootKind < 0) _lootKind = KillerKind(_lootKiller);
                if (_lootKind != 1 && _lootKind != 2) return;
                var m = Mission.Current;
                lock (_lootHero)
                {
                    if (!ReferenceEquals(m, _lootMission)) { _lootMission = m; _lootHero.Clear(); _lootTroop.Clear(); }
                    var d = _lootKind == 1 ? _lootHero : _lootTroop;
                    int n; d.TryGetValue(__0, out n); d[__0] = n + 1;
                }
            }
            catch { _lootStumbles++; }
        }

        private static int KillerKind(Agent a)
        {
            try
            {
                if (a == null) return 3;
                if (a.IsMount && a.RiderAgent != null) a = a.RiderAgent;   // stratowany przez konia - liczy sie jezdziec (jak CountKill)
                if (a == Agent.Main) return 1;
                if (!IsMainPartyAgent(a)) return 3;                           // sojusznicy i wrogowie - ich lup, ich rekord
                return a.IsHero ? 1 : 2;
            }
            catch { return 3; }
        }

        // wpis 82 (Jeff 05.10: "a jak jestem sam z 1 zolnierzem, a zabilem wszystkich ja?"): historycznie lup nalezal do
        // tego, kto go zdobyl; kapitan bral trzecia tylko z tego, co zdobyli jego ludzie (umowy Edwarda III). Liczymy wrogow
        // powalonych (zabici i ogluszeni) przez partie gracza: gracz i towarzysze (bohaterowie partii) -> 100% gracza,
        // zolnierze -> 1/3 gracza (PlayerLootSharePercent), 2/3 ludzi. Bitwa bez misji (symulacja) - stala trzecia.
        private static object _killMission;
        private static int _killsHero, _killsTroop;

        public static void CountKill(Agent affectedAgent, Agent affectorAgent, AgentState agentState)
        {
            try
            {
                var m = Mission.Current;
                if (m == null) return;
                if (!ReferenceEquals(m, _killMission)) { _killMission = m; _killsHero = 0; _killsTroop = 0; }
                if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;
                if (affectedAgent == null || !affectedAgent.IsHuman || affectorAgent == null) return;
                if (m.PlayerTeam == null || affectedAgent.Team == null || !affectedAgent.Team.IsEnemyOf(m.PlayerTeam)) return;
                var a = affectorAgent;
                if (a.IsMount && a.RiderAgent != null) a = a.RiderAgent;   // stratowany przez konia - liczy sie jezdziec
                if (a == Agent.Main) { _killsHero++; return; }
                if (!IsMainPartyAgent(a)) return;                             // sojusznicy - ich lup
                if (a.IsHero) _killsHero++; else _killsTroop++;
            }
            catch { }
        }

        private static int PlayerSharePercent()
        {
            try
            {
                int third = Math.Max(0, Math.Min(100, Settings.Current.PlayerLootSharePercent));
                if (ReferenceEquals(Mission.Current, _killMission) && _killsHero + _killsTroop > 0)
                {
                    float pct = (_killsHero * 100f + _killsTroop * third) / (_killsHero + _killsTroop);
                    int r = Math.Max(0, Math.Min(100, (int)Math.Round(pct)));
                    Log.Info("BattlefieldLaw: powaleni przez partie gracza - Ty i towarzysze " + _killsHero + ", zolnierze " + _killsTroop
                             + " -> dzialka gracza " + r + "% (zolnierskie " + third + "%).");
                    return r;
                }
                int troops = 0;
                var roster = MobileParty.MainParty.MemberRoster;
                if (roster != null)
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        if (el.Character != null && !el.Character.IsHero) troops += el.Number;
                    }
                if (troops == 0) return 100;   // sam jestes druzyna - wszystko twoje
                return Math.Max(0, Math.Min(100, Settings.Current.PlayerLootSharePercent));
            }
            catch { return Settings.Current.PlayerLootSharePercent; }
        }

        /// <summary>
        /// Udzial w bagazach pokonanych (liczy go DTE na ekran vanilla) przenosimy
        /// do kolejki - trafi na ekran Spoils razem z dzialka z pola. Gdy Spoils
        /// nieobecny, kolejka jedzie prosto na ekran vanilla ze zuzyciem.
        /// </summary>
        public static void AfterCollectLoot(PartyBase winnerParty, ItemRoster gainedLoots)
        {
            try
            {
                if (!Settings.Current.BattlefieldLawEnabled) return;
                if (winnerParty == null || winnerParty != PartyBase.MainParty) return;
                if (gainedLoots == null) return;
                if (!RealFoughtBattle() || RotEnlisted()) { ShareQueue.Clear(); Wrecks.Clear(); return; }

                if (_rlPresent)
                {
                    // Spoils obsluzy ekran - zabieramy udzial w bagazach do kolejki
                    if (gainedLoots.Count > 0)
                    {
                        for (int i = 0; i < gainedLoots.Count; i++)
                        {
                            var el = gainedLoots.GetElementCopyAtIndex(i);
                            ShareQueue.AddToCounts(el.EquipmentElement, el.Amount);
                        }
                        gainedLoots.Clear();
                    }
                    return;
                }

                // bez Spoils: dzialka gracza na ekran vanilla, ze zuzyciem jesli mamy czym
                if (ShareQueue.Count > 0)
                {
                    for (int i = 0; i < ShareQueue.Count; i++)
                    {
                        var el = ShareQueue.GetElementCopyAtIndex(i);
                        gainedLoots.AddToCounts(el.EquipmentElement, el.Amount);
                    }
                    ShareQueue.Clear();
                }
                AppendWrecks(gainedLoots);
                CleanseDragons(gainedLoots);
                CleanseTrash(gainedLoots);
                if (!Settings.Current.LootArrivesBattleWorn || _applyDamage == null || gainedLoots.Count == 0) return;
                var damaged = _applyDamage.Invoke(null, new object[] { gainedLoots, 0f }) as ItemRoster;
                if (damaged == null || ReferenceEquals(damaged, gainedLoots)) return;
                gainedLoots.Clear();
                for (int i = 0; i < damaged.Count; i++)
                {
                    var el = damaged.GetElementCopyAtIndex(i);
                    gainedLoots.AddToCounts(el.EquipmentElement, el.Amount);
                }
            }
            catch (Exception e) { Log.Error("AfterCollectLoot", e); }
        }

        /// <summary>
        /// Prefix na MapEvent.LootDefeatedPartyCasualties: w bitwie osobistej
        /// z DTE szablonowa loteria NIE biegnie (dla zadnej partii - sojusznicy
        /// AI dostaja swoje fizycznie przez rekordy DTE). Symulacje i bitwy
        /// bez DTE: vanilla po staremu.
        /// </summary>
        public static bool CutCasualtyLootPrefix(MapEvent __instance)
        {
            try
            {
                if (!CasualtyLootCut || !Settings.Current.BattlefieldLawEnabled) return true;
                // E8/L (252, PLAN 8.6c): "Send troops" wygrane przez gracza - za poleglych i rannych pokonanych placi DTE (DistributePlayerSimulationLoot:
                // prawdziwy sprzet do zbrojowni zwyciezcow), wiec drugi komplet z szablonow nie powstaje - dla zadnej partii tej bitwy, jak w bitwie stoczonej
                if (SimOneLoot(__instance))
                {
                    _simCutGame++;
                    Log.Info("BattlefieldLaw (E8/L): Send troops - szablonowa loteria gry za poleglych/rannych pominieta (placi lup DTE z cial).");
                    return false;
                }
                if (!RealFoughtBattle()) return true;
                Log.Info("BattlefieldLaw: szablonowa loteria za poleglych/rannych pominieta (placi scena DTE i jency).");
                return false;
            }
            catch { return true; }
        }

        // ------------------------------------------------------------------ E8/L (252, PLAN 8.6c): "Send troops" - jedna droga lupu
        private static int _simCutSpoils, _simCutBattles, _simCutGame, _lStumbles;
        internal static int LStumbles { get { return _lStumbles; } }
        /// <summary>Lista trofeow Spoils ostatniej bitwy to prawdziwy lup z pola (podmieniona w AfterGenerateLoot), nie loteria Spoils - tylko taka
        /// reszta zostawiona przy "Leave" idzie na polke warowni (SpoilsSeal.RemnantPostfix); loteria z niczego przepada jak dotad.</summary>
        internal static bool ScreenIsRealLoot;

        /// <summary>Nowa gra albo wczytanie (HorseFate.Reset z konstruktora ArmouryBehavior): liczniki E8/L od zera.</summary>
        internal static void LReset() { _simCutSpoils = _simCutBattles = _simCutGame = _lStumbles = 0; ScreenIsRealLoot = false; }

        /// <summary>Liczniki doby do linii "Lup i dary (E8/L)" (HorseFate.LLine) - zwraca tekst i zeruje.</summary>
        internal static string TakeSimCut()
        {
            string r = "loteria Spoils pominieta w " + _simCutBattles + " bitwach (" + _simCutSpoils + " szt. z niczego), szablonowa loteria gry pominieta " + _simCutGame + " razy";
            _simCutSpoils = _simCutBattles = _simCutGame = 0;
            return r;
        }

        /// <summary>
        /// Czy to bitwa gracza rozstrzygnieta przez "Send troops" i wygrana, za ktora DTE zaplaci lupem z poleglych (dekompilacja DTE v1.4.7
        /// EveryoneCampaignBehavior.OnMapEventEnded :444-464 - te same warunki: zwyciestwo, nie odwrot, nie rozejscie sie okretow, nie kryjowka wyslana
        /// rozkazem). Tylko wtedy tniemy loterie (Spoils i szablonowa gry) - inaczej za poleglych nie zaplacilby nikt. Sluga w cudzej armii (ROT) - jak dotad.
        /// </summary>
        internal static bool SimOneLoot(MapEvent me)
        {
            try
            {
                var s = Settings.Current;
                if (me == null || s == null || !s.SendTroopsOneLoot || !s.BattlefieldLawEnabled) return false;
                if (!CasualtyLootCut || _dteArmory == null || !HorseFate.DteWindowWired) return false;
                if (!me.IsPlayerMapEvent || !me.IsPlayerSimulation) return false;
                if (me.BattleState != BattleState.AttackerVictory && me.BattleState != BattleState.DefenderVictory) return false;
                if (me.WinningSide != me.PlayerSide || me.EndedByRetreat) return false;
                var pe = PlayerEncounter.Current;
                if (pe != null && (pe.IsNavalEncounterFinishedWithDisengage || pe.ForceHideoutSendTroops)) return false;
                return !RotEnlisted();
            }
            catch { _lStumbles++; return false; }
        }

        /// <summary>Dzialka kapitana w bitwie bez misji: stala trzecia (PlayerLootSharePercent - nie wiadomo, kto kogo powalil); sam w druzynie - wszystko.</summary>
        private static int SimSharePercent()
        {
            int third = Math.Max(0, Math.Min(100, Settings.Current.PlayerLootSharePercent));
            int troops = 0;
            var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
            if (roster != null)
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character != null && !el.Character.IsHero) troops += el.Number;
                }
            return troops == 0 ? 100 : third;
        }

        /// <summary>
        /// HorseFate.SimShare (postfiks okna DTE): gained = przybytek zbrojowni druzyny gracza z lupu "Send troops" (przedmiot -> ile). Dzialka kapitana
        /// z kazdej pozycji (zaokraglenie jak w bitwie stoczonej - od zera) schodzi ze zbrojowni do kolejki na ekran lupow: najpierw sztuki bez stanu (te
        /// dopisal przed chwila DTE), potem pozostale tej samej nazwy. Kolejke odbiera ekran Spoils (bitwa polowa) albo po godzinie sakwy gracza
        /// (ArmouryBehavior.LootFlushIn - nasz sluchacz MapEventEnded biegnie PRZED oknem DTE i terminu sam by nie ustawil). Zwraca liczbe sztuk w kolejce.
        /// </summary>
        internal static int QueueSimShare(Dictionary<ItemObject, int> gained, int total)
        {
            int moved = 0, pct = 0;
            try
            {
                var armory = _dteArmory != null ? _dteArmory.GetValue(null) as ItemRoster : null;
                if (armory == null || gained == null || total <= 0)
                {
                    Log.Info("BattlefieldLaw (E8/L): Send troops - DTE nie dopisal lupu do zbrojowni wojska (0 szt.), dzialki nie ma.");
                    return 0;
                }
                pct = SimSharePercent();
                foreach (var kv in gained)
                {
                    if (kv.Key == null || kv.Value <= 0) continue;
                    int left = Math.Min(kv.Value, (int)Math.Round(kv.Value * pct / 100.0, MidpointRounding.AwayFromZero));
                    for (int pass = 0; pass < 2 && left > 0; pass++)
                        for (int i = armory.Count - 1; i >= 0 && left > 0; i--)
                        {
                            var el = armory.GetElementCopyAtIndex(i);
                            if (el.EquipmentElement.Item != kv.Key || el.Amount <= 0) continue;
                            if (pass == 0 && el.EquipmentElement.ItemModifier != null) continue;
                            int n = Math.Min(left, el.Amount);
                            armory.AddToCounts(el.EquipmentElement, -n);
                            ShareQueue.AddToCounts(el.EquipmentElement, n);
                            left -= n; moved += n;
                        }
                }
                if (moved > 0) ArmouryBehavior.LootFlushIn(1f);
                Log.Info("BattlefieldLaw (E8/L): Send troops - lup DTE z poleglych " + total + " szt. w zbrojowni wojska, dzialka kapitana " + pct + "% = " + moved
                         + " szt. czeka na ekran lupow (reszta zostaje ludziom); loterie Spoils i gry za tych samych poleglych pominiete.");
            }
            catch (Exception e) { _lStumbles++; Log.Error("QueueSimShare", e); }
            return moved;
        }

        /// <summary>Czy agent nalezy do partii gracza (po Origin, przez refleksje -
        /// typy originow roznia sie miedzy trybami misji).</summary>
        internal static bool IsMainPartyAgent(Agent agent)
        {
            try
            {
                var origin = agent != null ? agent.Origin : null;
                if (origin == null) return false;
                var p = origin.GetType().GetProperty("Party");
                var party = p != null ? p.GetValue(origin, null) as PartyBase : null;
                if (party != null) return party == PartyBase.MainParty;
                var b = origin.GetType().GetProperty("BattleCombatant");
                return b != null && ReferenceEquals(b.GetValue(origin, null), PartyBase.MainParty);
            }
            catch { return false; }
        }

        /// <summary>Tabor Spoils zawsze OFF - wozy pokonanych dzieli vanilla
        /// wg wkladu, drugi ekran bagazy to byl dubel materii.</summary>
        public static void NoBaggageTrain(ref bool __result)
        {
            try { if (Settings.Current.BattlefieldLawEnabled) __result = false; } catch { }
        }

        /// <summary>Symulacje: trup mial sprzet, to sprzet jest - bez zaszytego x0.10-0.58.</summary>
        public static void FullTierMultiplier(ref float __result)
        {
            try { if (Settings.Current.BattlefieldLawEnabled && Settings.Current.SimBattleFullDrop) __result = 1f; }
            catch { }
        }

        // ------------------------------------------------------------------ wraki

        /// <summary>Postfix na selektorze DTE - zapamietuje, ktora czesc uznano za rozbita.</summary>
        public static void RecordStruckPiece(object __result)
        {
            try { _lastPick = __result as ItemObject; } catch { _lastPick = null; }
        }

        /// <summary>Po usunieciu agenta: rozbita czesc idzie do worka z wrakami -
        /// ale TYLKO gdy zabojca byl z PARTII GRACZA (Jeff 30.08: przy
        /// sojusznikach zbieralismy wraki z CALEGO pola, takze z cudzych
        /// zabojstw - kazdemu jego lup).</summary>
        public static void QueueWreck(Agent affectedAgent, Agent affectorAgent)
        {
            try
            {
                // Jeff 04.10 ("ma byc historycznie"): rozbita czesc WLASNEGO poleglego tez zostaje na polu jako wrak -
                // kto utrzyma pole, ten ja zbiera (przegrana: DropShare czysci worek). Wczesniej znikala.
                if (_lastPick != null && Settings.Current.WreckSalvageEnabled &&
                    Settings.Current.BattlefieldLawEnabled && Wrecks.Count < 400 &&
                    (IsMainPartyAgent(affectorAgent) || IsMainPartyAgent(affectedAgent)))
                    Wrecks.Add(_lastPick);
            }
            catch { }
            _lastPick = null;
        }

        /// <summary>Najciezszy modyfikator zuzycia Spoils - stan wraka.</summary>
        private static ItemModifier WreckModifier()
        {
            try
            {
                if (_rlEnsureCache != null) _rlEnsureCache.Invoke(null, null);
                // audyt 04.10: wrak dostawal heavy_max (3%) i prog zniszczenia (Jeff 28.08: "<=3% = zniszczone") kasowal
                // KAZDY wrak - funkcja byla martwa (w logach 8/8 "zostaly na polu"). Wrak = ciezko uszkodzony (8%), do przetopu.
                // wpis 97: cena lupu = stan - wrakiem jest "Mangled" (10%); "Battered" (25%) to juz zwykle zuzycie
                var m = _rlHeavyMax != null ? _rlHeavyMax.GetValue(null) as ItemModifier : null;
                if (m == null && _rlHeavy != null) m = _rlHeavy.GetValue(null) as ItemModifier;
                return m;
            }
            catch { return null; }
        }

        /// <summary>Dosypuje wraki do rostera (z modyfikatorem i ochrona przed ponownym losowaniem zuzycia).</summary>
        /// <summary>
        /// Pole opuszczone - kolejka lupu idzie do kosza. Zwraca, ile sztuk
        /// przepadlo (dzialka z magazynu wraca tam, skad ja wzielismy).
        /// </summary>
        internal static int DropShare()
        {
            int n = 0;
            try
            {
                n = ShareQueue.Count + Wrecks.Count;
                // to, co wyjelismy z magazynu wojska na ekran lupow, wraca na polke
                var armory = _dteArmory != null ? _dteArmory.GetValue(null) as ItemRoster : null;
                if (armory != null)
                    for (int i = 0; i < ShareQueue.Count; i++)
                    {
                        var el = ShareQueue.GetElementCopyAtIndex(i);
                        armory.AddToCounts(el.EquipmentElement, el.Amount);
                    }
                ShareQueue.Clear();
                Wrecks.Clear();
            }
            catch (Exception e) { Log.Error("DropShare", e); }
            return n;
        }

        /// <summary>Czy dzialka gracza wciaz lezy w kolejce (ekran lupow nie przyszedl).</summary>
        internal static bool SharePending()
        {
            try { return ShareQueue.Count > 0 || Wrecks.Count > 0; } catch { return false; }
        }

        /// <summary>
        /// KRYJOWKI I INNE BITWY BEZ EKRANU LUPOW. Po zwyklej bitwie dzialka
        /// gracza czeka w kolejce na ekran Spoils - ale po zdobyciu kryjowki
        /// zaden ekran nie przychodzi i lupy wisialy w prozni (60 sztuk u Jeffa).
        /// Ta metoda wysypuje kolejke PROSTO DO SAKW: kazda sztuka dostaje stan
        /// bojowy, wraki jada jak wraki. Wolana od razu po kryjowce i godzine
        /// po kazdej bitwie, ktorej ekran sie nie upomnial.
        /// </summary>
        internal static int FlushShareToBaggage(string why)
        {
            int moved = 0;
            try
            {
                var bag = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (bag == null) return 0;
                for (int i = 0; i < ShareQueue.Count; i++)
                {
                    var el = ShareQueue.GetElementCopyAtIndex(i);
                    var item = el.EquipmentElement.Item;
                    if (item == null) continue;
                    for (int n = 0; n < el.Amount; n++)
                    {
                        var mod = el.EquipmentElement.ItemModifier ?? ArmouryBehavior.PickWornModifier(item);
                        bag.AddToCounts(new EquipmentElement(item, mod), 1);
                        moved++;
                    }
                }
                ShareQueue.Clear();
                moved += AppendWrecks(bag);
                if (moved > 0)
                {
                    Log.Info("BattlefieldLaw: kolejka lupow wysypana do sakw (" + moved + " szt.) - " + why + ".");
                    Log.Player("The spoils are packed into your baggage: " + moved + " pieces of gear.", true);
                }
            }
            catch (Exception e) { Log.Error("FlushShareToBaggage", e); }
            return moved;
        }

        private static int AppendWrecks(ItemRoster roster)
        {
            int n = 0;
            try
            {
                if (Wrecks.Count == 0) return 0;
                var mod = WreckModifier();
                // wraki zbite ponizej progu zniszczenia w ogole nie wchodza
                // (Jeff 28.08: "<=3% = zniszczone, nie pojawia sie w loocie")
                if (mod != null && Settings.Current.LootMinConditionPercent > 0
                    && mod.PriceMultiplier * 100f <= Settings.Current.LootMinConditionPercent + 0.01f)
                {
                    int junked = Wrecks.Count;
                    Wrecks.Clear();
                    Log.Info("BattlefieldLaw: " + junked + " wrakow ponizej progu zniszczenia - zostaly na polu.");
                    return 0;
                }
                foreach (var item in Wrecks)
                {
                    if (item == null) continue;
                    var el = new EquipmentElement(item, mod);
                    if (mod != null && _rlRegisterLucky != null)
                    {
                        // Spoils przy nakladaniu zuzycia zachowuje "zarejestrowane" elementy 1:1 -
                        // dzieki temu wrak zostaje wrakiem, a nie dostaje losowych 40-85%
                        try { _rlRegisterLucky.Invoke(null, new object[] { el }); } catch { }
                    }
                    roster.AddToCounts(el, 1);
                    n++;
                }
                Wrecks.Clear();
            }
            catch (Exception e) { Log.Error("AppendWrecks", e); }
            return n;
        }
    }
}
