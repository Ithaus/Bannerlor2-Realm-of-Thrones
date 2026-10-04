using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    /// Konie i zwierzeta bez zmian (juz historyczne); z towarow handlowych - surowce warsztatow (skora, len, skory, lnianka).
    /// Popyt miast na przeliczone kategorie liczony w nowej monecie (DemandPostfix).
    /// </summary>
    internal static class HistoricalPrices
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.HistoricalPricesEnabled; } }

        private static readonly Dictionary<ItemObject, int> _orig = new Dictionary<ItemObject, int>();
        private static readonly Dictionary<ItemObject, float> _origWeight = new Dictionary<ItemObject, float>();

        // ------------------------------------------------------------ ladunek zamiast 10 kg (wpis 50)
        // Jeff 04.10: "jak ceny sa ponizej 1, to trzeba pomnozyc x10, aby latwiej oddac ceny". Gra zna tylko pensy calkowite:
        // ruda 10 kg = 0.75 d i drewno 10 kg = 0.35 d stoja na 1 d (2-3x historii). Jednostka rudy i drewna = HistBulkUnitFactor x
        // waga z gry (100 kg, "ladunek"): ruda ~8 d, drewno ~4 d. Wszystko, co liczy sztuki, przeliczamy wagą: wydobycie wsi
        // (MaterialLaw.ProdPostfix / BulkScale), popyt miast (stosunek cen za KG), warsztaty (WorkshopLaw.Needs w kg),
        // przetopy (MaterialLaw.RefinePostfix), kopalnie BK (WorkshopLaw), kuznia gracza (Recipes - juz wedle wagi).
        internal static float BulkScale(ItemObject it)
        {
            float w0;
            if (it == null || !_origWeight.TryGetValue(it, out w0) || w0 <= 0f) return 1f;
            return Math.Max(0.01f, it.Weight) / w0;
        }

        private static void SetWeight(ItemObject it, float w)
        {
            var f = typeof(ItemObject).GetProperty("Weight");
            if (f != null && f.CanWrite) { f.SetValue(it, w, null); return; }
            var bf = typeof(ItemObject).GetField("<Weight>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (bf != null) bf.SetValue(it, w);
        }
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
            float days = HistDays(it, c);
            float wage = IsAmmo(it) ? WageFor(1) : WageFor(t);      // test 04.10: dniowka mistrza t6 dawala snop strzal za 130 d - fletcher to zwykly rzemieslnik
            float mat = c.MetalKg * MetalPerKg(c.Grade)
                        + c.MetalKg * FuelPerMetalKg()               // wpis 48: drewno na wegiel tyle, ile spala warsztat (WorkshopLaw.Needs)
                        + c.LeatherKg * s.HistLeatherPerKg
                        + c.LinenKg * s.HistLinenPerKg
                        + c.WoodKg * s.HistWoodPerKg
                        + c.Special * s.HistSpecialFactor;           // rog, sciegno, klej lukow (w modelu w skali gry)
            float labor = days * wage;
            return (mat + labor) * (1f + Math.Max(0f, s.HistProfitPercent) / 100f);
        }

        private static bool IsAmmo(ItemObject it)
        {
            return it.ItemType == ItemObject.ItemTypeEnum.Arrows || it.ItemType == ItemObject.ItemTypeEnum.Bolts || it.ItemType == ItemObject.ItemTypeEnum.Thrown;
        }

        /// <summary>Dni pracy wedle cen historycznych - te same w wartosci sztuki i w warsztacie (WorkshopLaw), zeby
        /// warsztat liczyl prace tak, jak ja wyceniono: snop strzal x HistAmmoLaborMultiplier, luk x HistBowLaborMultiplier.</summary>
        internal static float HistDays(ItemObject it, ArmsPricing.Cost c)
        {
            var s = Settings.Current;
            float days = c.Days;
            if (IsAmmo(it)) days *= Math.Max(0f, s.HistAmmoLaborMultiplier);          // fletcher i grotnik: snop to dzien-dwa pracy
            else if (it.ItemType == ItemObject.ItemTypeEnum.Bow) days *= Math.Max(0f, s.HistBowLaborMultiplier);   // luk wojenny 12-18 d
            return days;
        }

        /// <summary>Paliwo na kg metalu (pensy): drewno na wegiel do dymarki (WorkshopWoodPerOre na rude, ktora daje
        /// WorkshopCrudeKgPerOre kg surowki) + 2.5 kg na kuznie - po cenie drewna.</summary>
        internal static float FuelPerMetalKg()
        {
            var s = Settings.Current;
            float woodKg = Math.Max(0f, s.WorkshopWoodPerOre) * 10f / Math.Max(0.1f, s.WorkshopCrudeKgPerOre) + 2.5f;
            return woodKg * s.HistWoodPerKg;
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

                // 0. ruda i drewno w ladunkach (wpis 50)
                if (s.HistBulkUnitFactor > 1.01f)
                    foreach (var id in new[] { "iron", "hardwood" })
                    {
                        var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                        if (it == null) continue;
                        if (!_origWeight.ContainsKey(it)) _origWeight[it] = it.Weight;
                        SetWeight(it, _origWeight[it] * s.HistBulkUnitFactor);
                    }

                // 1. surowce kuzni - cena za kg x WAGA (ruda i drewno 100 kg w ladunku, wegiel, sztabki 0.5 kg)
                var raw = new List<string>();
                foreach (var kv in new[]
                {
                    new KeyValuePair<string, float>("iron", s.HistIronOrePerKg),
                    new KeyValuePair<string, float>("hardwood", s.HistWoodPerKg),
                    new KeyValuePair<string, float>("charcoal", s.HistCharcoalPerKg),
                    // test 04.10: skora i len zostaly w skali gry (23-24.5 d/kg) przy zbrojach juz w pensach - warsztaty
                    // nie mialy z czego zarobic na przeszywanicy i robily tylko luki; surowce warsztatow tez historycznie
                    new KeyValuePair<string, float>("leather", s.HistLeatherPerKg),
                    new KeyValuePair<string, float>("linen", s.HistLinenPerKg),
                    new KeyValuePair<string, float>("hides", s.HistHidesPerKg),
                    new KeyValuePair<string, float>("flax", s.HistFlaxPerKg),
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
                // 3. popyt miast w nowej monecie: srednia geometryczna (stara/nowa wartosc) przedmiotow kazdej kategorii
                _catRatio.Clear();
                var sumLog = new Dictionary<ItemCategory, double>(); var cnt = new Dictionary<ItemCategory, int>();
                foreach (var kv in _orig)
                {
                    var cat = kv.Key.ItemCategory;
                    if (cat == null || kv.Value <= 0 || kv.Key.Value <= 0) continue;
                    double l; sumLog.TryGetValue(cat, out l); sumLog[cat] = l + Math.Log((double)kv.Value / kv.Key.Value * BulkScale(kv.Key));   // za kg - ladunek to tyle samo towaru co 10 starych sztuk
                    int k; cnt.TryGetValue(cat, out k); cnt[cat] = k + 1;
                }
                var cats = new List<string>();
                foreach (var kv in sumLog)
                {
                    float r = (float)Math.Exp(kv.Value / cnt[kv.Key]);
                    if (Math.Abs(r - 1f) < 0.02f) continue;
                    _catRatio[kv.Key] = r;
                    cats.Add(kv.Key.StringId + " /" + r.ToString("0.#", CultureInfo.InvariantCulture));
                }
                _applied = true;
                Log.Info("HistoricalPrices: popyt miast przeliczony na nowa monete (" + (s.HistDemandScaling ? "CZYNNE" : "wylaczone") + ") w " + cats.Count + " kategoriach: " + string.Join(", ", cats.ToArray()) + ".");
                if (_origWeight.Count > 0) Log.Info("HistoricalPrices: ruda i drewno w ladunkach - " + string.Join(", ", _origWeight.Select(kv => kv.Key.StringId + " " + kv.Value + " -> " + kv.Key.Weight + " kg = " + kv.Key.Value + " d").ToArray()) + ".");
                Log.Info("HistoricalPrices: surowce kuzni ["+ string.Join(", ", raw.ToArray()) + "]; uzbrojenie " + n + " szt. przeliczone z kosztu historycznego (suma wartosci "
                         + before + " -> " + after + "). Przyklady: " + string.Join("; ", samples.ToArray()) + ".");
            }
            catch (Exception e) { Log.Error("HistoricalPrices.Apply", e); }
        }

        // ------------------------------------------------------------ popyt miast w nowej monecie
        // Test 04.10: polki broni i zbroi opustoszaly w 4 dni (zbroje 88 -> 11, bron jednoreczna 341 -> 16). Miasto liczy
        // popyt w ZLOCIE (DefaultSettlementEconomyModel.GetDailyDemandForCategory = BaseDemand x dobrobyt) i zjada
        // budzet / cena sztuk (ItemConsumptionBehavior.MakeConsumption) - przy cenach 40x nizszych mieszczanie kupowali
        // 40x wiecej mieczy; a wspolczynnik ceny (popyt / podaz w zlocie) szedl pod sufit, wiec ruda i drewno staly na
        // indeksie 1.5. Dzielimy popyt kategorii przez to, ile razy potanialy jej przedmioty - rynek liczy te same sztuki.
        private static readonly Dictionary<ItemCategory, float> _catRatio = new Dictionary<ItemCategory, float>();
        [ThreadStatic] private static int _demandDepth;

        public static void DemandPrefix() { _demandDepth++; }
        public static Exception DemandFinalizer(Exception __exception) { if (_demandDepth > 0) _demandDepth--; return __exception; }
        public static void DemandPostfix(ItemCategory __1, ref float __result)
        {
            if (_demandDepth > 1 || !_applied || __1 == null) return;
            try
            {
                float r;
                if (Settings.Current.HistDemandScaling && _catRatio.TryGetValue(__1, out r) && r > 0f) __result /= r;
            }
            catch { }
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
                int d = 0;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; } catch { continue; }
                    foreach (var ty in types)
                    {
                        try
                        {
                            if (ty == null || ty.IsAbstract || !typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.SettlementEconomyModel).IsAssignableFrom(ty)) continue;
                            var m = ty.GetMethod("GetDailyDemandForCategory", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                            if (m == null) continue;
                            h.Patch(m, prefix: new HarmonyMethod(typeof(HistoricalPrices), nameof(DemandPrefix)) { priority = Priority.First },
                                       postfix: new HarmonyMethod(typeof(HistoricalPrices), nameof(DemandPostfix)) { priority = Priority.Last },
                                       finalizer: new HarmonyMethod(typeof(HistoricalPrices), nameof(DemandFinalizer)));
                            d++;
                        }
                        catch { }
                    }
                }
                Log.Info("HistoricalPrices: popyt miast w nowej monecie wpiety w " + d + " modelach ekonomii osad.");
            }
            catch (Exception e) { Log.Error("HistoricalPrices.ApplyAll", e); }
        }
    }
}
