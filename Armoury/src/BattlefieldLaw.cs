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

                if (dmgType != null) _applyDamage = AccessTools.Method(dmgType, "ApplyDamageToLoot");
                var dispatcher = AccessTools.Method(typeof(CampaignEventDispatcher), "OnCollectLootItems");
                if (dispatcher != null)
                {
                    h.Patch(dispatcher, postfix: new HarmonyMethod(typeof(BattlefieldLaw), "AfterCollectLoot"));
                    Log.Info("BattlefieldLaw: lupy z ekranu dostaja stan bojowy (zuzycie Spoils)."
                             + (_applyDamage == null ? " (Spoils nieobecny - bez zuzycia)" : ""));
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
                        Log.Info("BattlefieldLaw: dzialka gracza z lupow DTE trafia na ekran (solo 100%, z druzyna "
                                 + Settings.Current.PlayerLootSharePercent + "%).");
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
                if (!Settings.Current.BattlefieldLawEnabled) return;
                if (RotEnlisted()) { ShareQueue.Clear(); Wrecks.Clear(); return; }
                if (!RealFoughtBattle()) return;         // symulacje: zostaje loteria Spoils (odbramkowana)
                if (ShareQueue.Count == 0 && Wrecks.Count == 0) return;
                if (__result == null) __result = new ItemRoster();
                __result.Clear();
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
                if (pct <= 0) return;

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
                        if (share <= 0) continue;
                        int idx = armory.FindIndexOfItem(item);
                        int avail = idx >= 0 ? armory.GetElementNumber(idx) : 0;
                        int take = Math.Min(share, avail);
                        if (take <= 0) continue;
                        armory.AddToCounts(item, -take);
                        ShareQueue.AddToCounts(item, take);
                        moved += take;
                    }
                    break;   // rekord partii gracza znaleziony i przetworzony
                }
                if (moved > 0) Log.Info("BattlefieldLaw: " + moved + " szt. (" + pct + "%) z magazynu wojska czeka na ekran lupow.");
            }
            catch (Exception e) { Log.Error("AfterDtePartyItems", e); }
        }

        private static int PlayerSharePercent()
        {
            try
            {
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

        /// <summary>Po usunieciu agenta: rozbita czesc idzie do worka z wrakami.</summary>
        public static void QueueWreck()
        {
            try
            {
                if (_lastPick != null && Settings.Current.WreckSalvageEnabled &&
                    Settings.Current.BattlefieldLawEnabled && Wrecks.Count < 400)
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
