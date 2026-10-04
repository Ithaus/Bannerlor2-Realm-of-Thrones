using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// SPRZET STARTOWY, KTORY DA SIE NOSIC (Jeff 04.10: "dostalem mase sprzetu, jak zaczalem
    /// gre, ale nie moge go nosic - nie powinienem dostawac rzeczy, ktorych nie moge nosic").
    /// Kreator postaci (pochodzenie ROT/BK) daje komplet wedle pochodzenia - np. plyte rycerza-
    /// rabusia - a Prawo Wagi CrashScribe (Atletyka 35/tier) stawia mu wymagania, ktorych nowa
    /// postac nie ma. Gra zrzuca to do taboru na czerwono.
    ///
    /// Raz, w pierwszej godzinie po kreatorze (wszystkie mody juz rozdaly swoje): kazda sztuke
    /// w taborze i na postaci, ktorej bohater nie moze uzyc, zamieniamy na NAJLEPSZA sztuke
    /// tego samego typu, ktora moze nosic - tej samej kultury albo neutralna, nie unikat, nie
    /// drozsza od oryginalu. Nic nie przybywa z niczego: tansze za drozsze. Brak zamiennika =
    /// sztuka zostaje (log).
    /// </summary>
    internal static class StartKit
    {
        internal static void Reset() { _pending = false; }
        private static bool _pending;

        internal static void OnCharacterCreationOver() { _pending = true; }

        internal static void Hourly()
        {
            if (!_pending) return;
            _pending = false;
            try { Run(); } catch (Exception e) { Log.Error("StartKit", e); }
        }

        private static SkillObject ReqSkill(ItemObject it)
        {
            if (it == null) return null;
            if (it.RelevantSkill != null) return it.RelevantSkill;
            if (it.ItemType == ItemObject.ItemTypeEnum.Arrows) return DefaultSkills.Bow;
            if (it.ItemType == ItemObject.ItemTypeEnum.Bolts) return DefaultSkills.Crossbow;
            if (it.HasArmorComponent) return DefaultSkills.Athletics;      // Prawo Wagi CrashScribe
            return null;
        }

        private static bool CanUse(Hero h, ItemObject it)
        {
            try
            {
                if (h == null || it == null || it.Difficulty <= 0) return true;
                var rs = ReqSkill(it);
                return rs == null || h.GetSkillValue(rs) >= it.Difficulty;
            }
            catch { return true; }
        }

        private static float Worth(ItemObject it)
        {
            try { return it.Effectiveness > 0f ? it.Effectiveness : it.Value; } catch { return 0f; }
        }

        private static ItemObject Substitute(Hero h, ItemObject orig)
        {
            var culture = orig.Culture;
            ItemObject best = null;
            foreach (var it in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                if (it == null || it == orig || it.ItemType != orig.ItemType || it.NotMerchandise) continue;
                if (it.Value > orig.Value) continue;
                if (it.Culture != null && culture != null && it.Culture != culture && !it.Culture.StringId.StartsWith("neutral")) continue;
                if (ArmsPricing.IsUnique(it)) continue;
                if (orig.ItemType == ItemObject.ItemTypeEnum.Horse && it.HorseComponent != null && it.HorseComponent.IsPackAnimal) continue;
                if (!CanUse(h, it)) continue;
                if (best == null || Worth(it) > Worth(best)) best = it;
            }
            return best;
        }

        private static void Run()
        {
            var h = Hero.MainHero;
            var party = MobileParty.MainParty;
            if (h == null || party == null) return;
            var swaps = new List<string>();
            var kept = new List<string>();

            // 1. na postaci (bojowy i cywilny komplet)
            foreach (var eq in new[] { h.BattleEquipment, h.CivilianEquipment })
            {
                if (eq == null) continue;
                for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                {
                    var el = eq[i];
                    if (el.IsEmpty || el.Item == null || CanUse(h, el.Item)) continue;
                    var sub = Substitute(h, el.Item);
                    if (sub == null) { kept.Add(el.Item.StringId); continue; }
                    swaps.Add(el.Item.StringId + " -> " + sub.StringId);
                    eq[i] = new EquipmentElement(sub);
                }
            }

            // 2. w taborze
            var roster = party.ItemRoster;
            var todo = new List<KeyValuePair<EquipmentElement, int>>();
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !(it.HasArmorComponent || it.WeaponComponent != null || it.HorseComponent != null)) continue;
                if (CanUse(h, it)) continue;
                todo.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, el.Amount));
            }
            foreach (var kv in todo)
            {
                var sub = Substitute(h, kv.Key.Item);
                if (sub == null) { kept.Add(kv.Key.Item.StringId + " x" + kv.Value); continue; }
                roster.AddToCounts(kv.Key, -kv.Value);
                roster.AddToCounts(new EquipmentElement(sub), kv.Value);
                swaps.Add(kv.Key.Item.StringId + " x" + kv.Value + " -> " + sub.StringId);
            }

            Log.Info("StartKit: sprzet startowy dopasowany do umiejetnosci - zamienione " + swaps.Count
                     + (swaps.Count > 0 ? " [" + string.Join(", ", swaps.ToArray()) + "]" : "")
                     + "; bez zamiennika " + kept.Count + (kept.Count > 0 ? " [" + string.Join(", ", kept.ToArray()) + "]" : "") + ".");
            if (swaps.Count > 0)
                Log.Player("Your starting gear has been matched to what you can actually wear.");
        }

        // ------------------------------------------------------------ zloto startowe BK
        // Jeff 04.10: "nie moge miec na starcie 1000 denarow". BK (DefaultStartOptions.Adventurer) daje awanturnikowi
        // 1000 d = ok. 4 funty, ponad rok pracy robotnika. Prefix na StartOption.Initialize podmienia kwote dla
        // "start_adventurer" na StartGoldAdventurer (BKCampaignStartBehavior.RunStartOption ustawia zloto = option.Gold).
        public static void StartOptionPrefix(object __instance, ref int gold)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || s.StartGoldAdventurer < 0) return;
                var id = HarmonyLib.Traverse.Create(__instance).Property("StringId").GetValue() as string;
                if (id == "start_adventurer") gold = s.StartGoldAdventurer;
            }
            catch { }
        }

        internal static void ApplyAll(HarmonyLib.Harmony h)
        {
            try
            {
                var t = HarmonyLib.AccessTools.TypeByName("BannerKings.Managers.CampaignStart.StartOption");
                var m = t != null ? HarmonyLib.AccessTools.Method(t, "Initialize") : null;
                if (m != null) h.Patch(m, prefix: new HarmonyLib.HarmonyMethod(typeof(StartKit), nameof(StartOptionPrefix)));
                Log.Info("StartKit: zloto startowe awanturnika BK = " + Settings.Current.StartGoldAdventurer + " d " + (m != null ? "wpiete" : "(brak BK StartOption)") + ".");
            }
            catch (Exception e) { Log.Error("StartKit.ApplyAll", e); }
        }
    }
}
