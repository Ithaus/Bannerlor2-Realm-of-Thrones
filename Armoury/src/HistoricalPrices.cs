using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// SWIAT W WARTOSCIACH HISTORYCZNYCH (Jeff 04.10: "no kurwa tak, przeliczamy caly swiat do wartosci
    /// historycznych"). Plan: docs/PLAN-CENY-HISTORYCZNE.md, ceny: docs/CENY-HISTORYCZNE.md (Clark/Rogers 1300-49).
    /// Klucz: 1 denar = 1 pens. Zold, dochody i zloto startowe juz sa w tej skali; przesadzone byly bron
    /// i zbroje (wzor gry 100-120 x 2.75^tier - t1 -> t6 x157) i surowce kuzni (ruda, drewno, wegiel, sztabki).
    ///
    /// Etap 1+2 (ten plik): przy starcie sesji ustawiamy Value (setter, jak MaterialLaw):
    ///  - surowce kuzni wedle cen historycznych za kg (wegiel liczony ze swoja prawdziwa waga z gry - 5 kg);
    ///  - kazda bron, zbroja, tarcza, amunicja, uprzaz: KOSZT HISTORYCZNY z ilosci fizycznych naszego modelu
    ///    (ArmsPricing.Cost: kg metalu ze strata, skory, lnu, drewna, dni pracy x jakosc sztuki): material x cena
    ///    historyczna + dni x dniowka mistrza wedle tieru, zysk; unikaty x prestiz.
    /// Wartosc idzie wszedzie: targ (dalej przez prawo podazy i popytu), lup, naprawa, zamowienia, nagrody.
    /// XP kowalstwa (DefaultSmithingModel liczy z Value) przeliczane od wartosci sprzed zmiany.
    /// Konie, zwierzeta i towary handlowe - bez zmian w tym etapie (konie i bydlo juz sa historyczne).
    /// </summary>
    internal static class HistoricalPrices
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.HistoricalPricesEnabled; } }

        private static readonly Dictionary<ItemObject, int> _orig = new Dictionary<ItemObject, int>();
        private static bool _applied;

        internal static void Reset() { }   // Value zyje w obiektach przedmiotow - Apply przy kazdym starcie sesji

        internal static int Orig(ItemObject it)
        {
            int v;
            if (it != null && _orig.TryGetValue(it, out v)) return v;
            return MaterialLaw.Orig(it);
        }

        private static float MetalPerKg(CraftingMaterials g)
        {
            var s = Settings.Current;
            switch (g)
            {
                case CraftingMaterials.Iron1: return s.HistCrudeIronPerKg;
                case CraftingMaterials.Iron2: return s.HistWroughtIronPerKg;
                case CraftingMaterials.Iron3: return s.HistIronPerKg;
                case CraftingMaterials.Iron4: return s.HistSteelPerKg;
                case CraftingMaterials.Iron5: return s.HistFineSteelPerKg;
                default: return s.HistValyrianPerKg;
            }
        }

        private static float WageFor(int tier)
        {
            var s = Settings.Current;
            return Math.Max(0.5f, s.HistMasterWageT1 + s.HistMasterWagePerTier * Math.Max(0, tier - 1));
        }

        /// <summary>Koszt historyczny sztuki w pensach (0 = nie umiemy policzyc).</summary>
        internal static float HistCost(ItemObject it)
        {
            var c = ArmsPricing.CostOf(it);
            if (c == null) return 0f;
            var s = Settings.Current;
            int t = Math.Max(1, Math.Min(6, (int)it.Tier + 1));
            float days = c.Days;
            if (it.ItemType == ItemObject.ItemTypeEnum.Arrows || it.ItemType == ItemObject.ItemTypeEnum.Bolts || it.ItemType == ItemObject.ItemTypeEnum.Thrown)
                days *= Math.Max(0f, s.HistAmmoLaborMultiplier);    // fletcher i grotnik: snop to dzien-dwa pracy
            float mat = c.MetalKg * MetalPerKg(c.Grade)
                        + c.MetalKg * s.HistCharcoalPerKg            // ~1 kg wegla na kg kutego metalu
                        + c.LeatherKg * s.HistLeatherPerKg
                        + c.LinenKg * s.HistLinenPerKg
                        + c.WoodKg * s.HistWoodPerKg
                        + c.Special * s.HistSpecialFactor;           // rog, sciegno, klej lukow (w modelu w skali gry)
            float labor = days * WageFor(t);
            return (mat + labor) * (1f + Math.Max(0f, s.HistProfitPercent) / 100f);
        }

        private static bool IsArms(ItemObject it)
        {
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor: case ItemObject.ItemTypeEnum.BodyArmor: case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor: case ItemObject.ItemTypeEnum.Cape: case ItemObject.ItemTypeEnum.HorseHarness:
                case ItemObject.ItemTypeEnum.OneHandedWeapon: case ItemObject.ItemTypeEnum.TwoHandedWeapon: case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Thrown: case ItemObject.ItemTypeEnum.Shield: case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow: case ItemObject.ItemTypeEnum.Arrows: case ItemObject.ItemTypeEnum.Bolts:
                    return true;
                default: return false;
            }
        }

        internal static void Apply()
        {
            try
            {
                if (!On) { Log.Info("HistoricalPrices: ceny historyczne WYLACZONE (MCM)."); return; }
                var s = Settings.Current;
                var setter = AccessTools.PropertySetter(typeof(ItemObject), "Value");
                if (setter == null) { Log.Info("HistoricalPrices: ItemObject.Value bez settera - bez zmian."); return; }
                Action<ItemObject, float> set = (it, v) =>
                {
                    if (it == null) return;
                    if (!_orig.ContainsKey(it)) _orig[it] = it.Value;
                    setter.Invoke(it, new object[] { Math.Max(1, (int)Math.Round(v)) });
                };

                // 1. surowce kuzni - cena za kg x WAGA z gry (ruda i drewno 10 kg, wegiel 5 kg, sztabki 0.5 kg)
                var raw = new List<string>();
                foreach (var kv in new[]
                {
                    new KeyValuePair<string, float>("iron", s.HistIronOrePerKg),
                    new KeyValuePair<string, float>("hardwood", s.HistWoodPerKg),
                    new KeyValuePair<string, float>("charcoal", s.HistCharcoalPerKg),
                })
                {
                    var it = MBObjectManager.Instance.GetObject<ItemObject>(kv.Key);
                    if (it == null) continue;
                    set(it, it.Weight * kv.Value);
                    raw.Add(kv.Key + " " + _orig[it] + "->" + it.Value);
                }
                foreach (var g in new[] { CraftingMaterials.Iron1, CraftingMaterials.Iron2, CraftingMaterials.Iron3, CraftingMaterials.Iron4, CraftingMaterials.Iron5, CraftingMaterials.Iron6 })
                {
                    var it = Recipes.MaterialItem(g);
                    if (it == null) continue;
                    set(it, Math.Max(0.5f, it.Weight) * MetalPerKg(g));
                    raw.Add(it.StringId + " " + _orig[it] + "->" + it.Value);
                }

                // 2. bron i zbroje z kosztu historycznego
                int n = 0; long before = 0, after = 0;
                var samples = new List<string>();
                var watch = new HashSet<string> { "stark_boots_1", "battania_sword_5_t5", "casterly_heavy_helm", "sturgian_fortified_armor", "ramsay_armor" };
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || !IsArms(it) || it.Value <= 0) continue;
                    float hc = HistCost(it);
                    if (hc <= 0f) continue;
                    if (ArmsPricing.IsUnique(it)) hc *= Math.Max(1f, s.HistUniquePrestige);
                    int was = it.Value;
                    set(it, hc);
                    n++; before += was; after += it.Value;
                    if (watch.Contains(it.StringId)) samples.Add(it.StringId + " (" + it.ItemType + " t" + ((int)it.Tier + 1) + ", " + it.Weight.ToString("0.0", CultureInfo.InvariantCulture) + " kg) " + was + " -> " + it.Value + " d");
                }
                _applied = true;
                Log.Info("HistoricalPrices: surowce kuzni [" + string.Join(", ", raw.ToArray()) + "]; uzbrojenie " + n + " szt. przeliczone z kosztu historycznego (suma wartosci "
                         + before + " -> " + after + "). Przyklady: " + string.Join("; ", samples.ToArray()) + ".");
            }
            catch (Exception e) { Log.Error("HistoricalPrices.Apply", e); }
        }

        // ------------------------------------------------------------ XP kowalstwa od wartosci sprzed zmiany
        public static void XpPostfix(ItemObject item, ref int __result)
        {
            try
            {
                if (!_applied || item == null || item.Value <= 0) return;
                int orig = Orig(item);
                if (orig > 0 && orig != item.Value) __result = Math.Max(1, (int)Math.Round(__result * (double)orig / item.Value));
            }
            catch { }
        }

        // ------------------------------------------------------------ nagrody turniejowe
        // FightTournamentGame szuka nagrody o wartosci 1600-5000 (stale w kodzie gry); po przeliczeniu takich sztuk moze
        // nie byc, a pusta lista = losowanie z pustej listy = CRASH. Przedzial dzielimy przez HistTournamentScale (4),
        // a gdy i tak wyjdzie pusto - wypelniamy zwykla bronia i zbroja (zadna nagroda nie znaczy wyjatku).
        public static void PrizeRangePrefix(ref int __1, ref int __2)
        {
            if (!_applied) return;
            float k = Math.Max(1f, Settings.Current.HistTournamentScale);
            __1 = Math.Max(1, (int)(__1 / k)); __2 = Math.Max(__1 + 1, (int)(__2 / k));
        }

        public static void PrizeListPostfix(ref TaleWorlds.Library.MBList<ItemObject> __result)
        {
            try
            {
                if (!_applied || (__result != null && __result.Count > 0)) return;
                var l = new TaleWorlds.Library.MBList<ItemObject>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                    if (it != null && !it.NotMerchandise && it.Value > 0 && (it.IsCraftedWeapon || it.ArmorComponent != null) && (int)it.Tier >= 2) l.Add(it);
                __result = l;
            }
            catch { }
        }

        // BK BKEducationBehavior.OnBuyBookConsequence: cena ksiazki = Value x 1000 (0.75-1.5 mln) - blad BK; wartosc ksiazki
        // (750-1500 d = 3-6 L) jest historyczna dla rekopisu. Stala 1000 stojaca zaraz po get_Value -> 1.
        private static int _bookSwaps;
        public static System.Collections.Generic.IEnumerable<CodeInstruction> BookTranspiler(System.Collections.Generic.IEnumerable<CodeInstruction> instructions)
        {
            var getValue = AccessTools.PropertyGetter(typeof(ItemObject), "Value");
            CodeInstruction prev = null;
            foreach (var ci in instructions)
            {
                if (prev != null && Equals(prev.operand, getValue) && ci.opcode == System.Reflection.Emit.OpCodes.Ldc_I4 && ci.operand is int && (int)ci.operand == 1000)
                { ci.operand = 1; _bookSwaps++; }
                prev = ci;
                yield return ci;
            }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel);
                int n = 0;
                foreach (var name in new[] { "GetSkillXpForSmelting", "GetSkillXpForSmithingInFreeBuildMode", "GetSkillXpForSmithingInCraftingOrderMode" })
                {
                    var m = AccessTools.Method(t, name, new[] { typeof(ItemObject) });
                    if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(HistoricalPrices), nameof(XpPostfix))); n++; }
                }
                var tm = typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultTournamentModel);
                int p = 0;
                foreach (var name in new[] { "GetRegularRewardItems", "GetEliteRewardItems" })
                {
                    var m = AccessTools.Method(tm, name);
                    if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(HistoricalPrices), nameof(PrizeRangePrefix)), postfix: new HarmonyMethod(typeof(HistoricalPrices), nameof(PrizeListPostfix))); p++; }
                }
                Log.Info("HistoricalPrices: nagrody turniejowe przeliczone w " + p + "/2 metodach.");
                try
                {
                    var bt = AccessTools.TypeByName("BannerKings.Behaviours.BKEducationBehavior");
                    int bm = 0;
                    if (bt != null)
                    {
                        var types = new System.Collections.Generic.List<Type> { bt };
                        types.AddRange(bt.GetNestedTypes(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic));
                        var getValue = AccessTools.PropertyGetter(typeof(ItemObject), "Value");
                        foreach (var ty in types)
                            foreach (var mm in ty.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
                            {
                                try
                                {
                                    if (mm.IsAbstract || mm.ContainsGenericParameters || mm.GetMethodBody() == null) continue;
                                    object last = null; bool hit = false;
                                    foreach (var kv in PatchProcessor.ReadMethodBody(mm)) { if (kv.Value is int && (int)kv.Value == 1000 && Equals(last, getValue)) { hit = true; break; } last = kv.Value; }
                                    if (!hit) continue;
                                    h.Patch(mm, transpiler: new HarmonyMethod(typeof(HistoricalPrices), nameof(BookTranspiler)));
                                    bm++;
                                }
                                catch { }
                            }
                    }
                    Log.Info("HistoricalPrices: cena ksiazek BK (Value x 1000 -> Value) w " + bm + " metodach (stalych " + _bookSwaps + ").");
                }
                catch (Exception e) { Log.Error("HistoricalPrices.Books", e); }
                Log.Info("HistoricalPrices: XP kowalstwa od dawnych wartosci w " + n + "/3 metodach.");
            }
            catch (Exception e) { Log.Error("HistoricalPrices.ApplyAll", e); }
        }
    }
}
