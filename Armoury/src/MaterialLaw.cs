using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// PRAWO SUROWCOW (Jeff 04.10, "zbuduj pelny system" - docs/MODEL-MATERIALOW.md, krok 1).
    /// Jednostki gry: ruda 10 kg (50), drewno 10 kg (25), sztabka i wegiel po 0.5 kg.
    /// Vanilla: 2 drewna (20 kg) -> 1 wegiel (0.5 kg), 1 ruda + 1 wegiel -> 2 surowki,
    /// wegiel po 50 (100 za kg, 40x drozszy od drewna). Historycznie koszt zelaza robi
    /// WEGIEL: 1-2 kg wegla na kg rudy, z 4-7 kg drewna na kg wegla.
    ///
    /// 1. Ceny (Value) wegla i sztabek z MCM, liczone lancuchem od drewna i rudy
    ///    (wegiel 9, surowka 87, zelazo kute 118, zelazo 157, stal 210, stal szl. 281,
    ///    stal zamkowa 375 - 177-1, dawniej "valyrianska" 1000). Oryginaly zapamietane - XP za przetop liczymy od nich.
    /// 2. Przetopy (postfix na GetRefiningFormulas; 177-1: bez dawnego podwojnego wsadu stali 6 - ValyrianSteel.DearRefine zdjety):
    ///    1 drewno -> 4 wegla (perk CharcoalMaker 5); 1 ruda + 20 wegla -> 3 surowki
    ///    (perk IronMaker 4); dalej 5 sztabek nizszych + wegiel -> 4 wyzsze (strata metalu
    ///    jak przy zgrzewaniu - Jeff 04.10: 20% na stopien, 5 sztabek -> 4), progi perkow SteelMaker 1/2/3 jak w vanilla.
    /// 3. Wydobycie: postfix na CalculateDailyProductionAmount (vanilla/BK/BEE, licznik
    ///    zagniezdzenia) - ruda x MineOutputMultiplier, drewno x LumberOutputMultiplier.
    ///    Od wpisu 98 mnoznik MNOZY wynik modelu (zima, drogi, prawa BK skaluja sie razem z nim) -
    ///    wczesniej dodawal sie do tych czynnikow. Faktyczne wydobycie pokazuje ksiega (OreLedger).
    ///    Od wpisu 105 ten sam postfix mnozy wynik przez MineralOnce.Times: mineral, ktory BK mial na liscie produkcji wsi
    ///    kilka razy, jest na niej raz, a model oddaje go tyle razy, ile bylo wpisow (kazdy przedmiot, nie tylko ruda).
    /// </summary>
    internal static class MaterialLaw
    {
        private static readonly Dictionary<ItemObject, int> _orig = new Dictionary<ItemObject, int>();
        private static bool _applied;

        internal static bool On { get { var s = Settings.Current; return s != null && s.MaterialLawEnabled; } }

        internal static void Reset() { _orig.Clear(); }   // wpis 87 (audyt pkt 12)

        /// <summary>Cena sprzed prawa surowcow (XP za przetop).</summary>
        internal static int Orig(ItemObject it)
        {
            int v;
            return it != null && _orig.TryGetValue(it, out v) ? v : (it != null ? it.Value : 0);
        }

        // ------------------------------------------------------------ 1. ceny
        internal static void Apply()
        {
            try
            {
                if (!On) { Log.Info("MaterialLaw: prawo surowcow WYLACZONE."); return; }
                var s = Settings.Current;
                var setter = AccessTools.PropertySetter(typeof(ItemObject), "Value");
                if (setter == null) { Log.Info("MaterialLaw: ItemObject.Value bez settera - ceny surowcow bez zmian."); return; }
                var plan = new List<KeyValuePair<CraftingMaterials, int>>
                {
                    new KeyValuePair<CraftingMaterials, int>(CraftingMaterials.Charcoal, s.CharcoalValue),
                    new KeyValuePair<CraftingMaterials, int>(CraftingMaterials.Iron1, s.CrudeIronValue),
                    new KeyValuePair<CraftingMaterials, int>(CraftingMaterials.Iron2, s.WroughtIronValue),
                    new KeyValuePair<CraftingMaterials, int>(CraftingMaterials.Iron3, s.IronValue),
                    new KeyValuePair<CraftingMaterials, int>(CraftingMaterials.Iron4, s.SteelValue),
                    new KeyValuePair<CraftingMaterials, int>(CraftingMaterials.Iron5, s.FineSteelValue),
                    new KeyValuePair<CraftingMaterials, int>(CraftingMaterials.Iron6, s.CastleSteelValue),   // 177-1: stal zamkowa, zwykly stopien lancucha
                };
                var parts = new List<string>();
                foreach (var kv in plan)
                {
                    var it = Recipes.MaterialItem(kv.Key);
                    if (it == null || kv.Value <= 0) continue;
                    if (!_orig.ContainsKey(it)) _orig[it] = it.Value;
                    int before = it.Value;
                    setter.Invoke(it, new object[] { kv.Value });
                    parts.Add(it.StringId + " " + before + "->" + kv.Value);
                }
                _applied = true;
                Log.Info("MaterialLaw: ceny surowcow kuzni: " + string.Join(", ", parts.ToArray())
                         + ". Wytop " + (s.RealRefiningEnabled ? "realny (drewno 1:4 wegla, ruda+20 wegla -> 3 surowki, 5 -> 4 na stopien)" : "vanilla")
                         + ", wydobycie rudy x" + s.MineOutputMultiplier.ToString("0.0") + ", drewna x" + s.LumberOutputMultiplier.ToString("0.0") + ".");
            }
            catch (Exception e) { Log.Error("MaterialLaw.Apply", e); }
        }

        // ------------------------------------------------------------ 2. przetopy
        public static IEnumerable<Crafting.RefiningFormula> RefinePostfix(IEnumerable<Crafting.RefiningFormula> values, Hero weaponsmith)
        {
            var s = Settings.Current;
            if (s == null || !s.MaterialLawEnabled || !s.RealRefiningEnabled || weaponsmith == null)
            {
                foreach (var f in values) yield return f;
                yield break;
            }
            // vanilla zostawia steel-stopnie za perkami - te same progi u nas
            bool charcoal = Perk(weaponsmith, DefaultPerks.Crafting.CharcoalMaker);
            bool ironMaker = Perk(weaponsmith, DefaultPerks.Crafting.IronMaker);
            bool steel1 = Perk(weaponsmith, DefaultPerks.Crafting.SteelMaker);
            bool steel2 = Perk(weaponsmith, DefaultPerks.Crafting.SteelMaker2);
            bool steel3 = Perk(weaponsmith, DefaultPerks.Crafting.SteelMaker3);
            int bloomCoal = Math.Max(1, s.BloomeryCharcoalPerOre);
            if (HistoricalPrices.On)
            {
                // ceny historyczne: wegiel liczony ze swoja PRAWDZIWA waga z gry (5 kg) - z drewna wychodzi ok. 20% wagi
                // w weglu (5 drewna = 50 kg -> 2 wegle = 10 kg); dymarka: 1 ruda (10 kg) + 2 wegle -> 4 surowki (2 kg zelaza);
                // dalsze stopnie: 5 sztabek + 1 wegiel -> 4 (strata 20%, Jeff)
                // wpis 50: ilosci z WAG (ladunek drewna/rudy 100 kg, wegiel i sztabki z gry): drewno -> 20% masy w weglu,
                // dymarka: ruda + tyle samo kg wegla -> WorkshopCrudeKgPerOre/10 masy rudy w surowce (jak warsztaty)
                var wi = Recipes.MaterialItem(CraftingMaterials.Wood); var oi = Recipes.MaterialItem(CraftingMaterials.IronOre);
                var ci = Recipes.MaterialItem(CraftingMaterials.Charcoal); var i1 = Recipes.MaterialItem(CraftingMaterials.Iron1);
                float wW = wi != null ? Math.Max(0.1f, wi.Weight) : 10f, oW = oi != null ? Math.Max(0.1f, oi.Weight) : 10f;
                float cW = ci != null ? Math.Max(0.05f, ci.Weight) : 5f, iW = i1 != null ? Math.Max(0.05f, i1.Weight) : 0.5f;
                float crude = Math.Max(0.01f, s.WorkshopCrudeKgPerOre) / 10f;
                int coalFromWood = Math.Max(1, (int)Math.Round(wW * 0.2f / cW * (charcoal ? 1.5f : 1f)));
                int coalPerOre = Math.Max(1, (int)Math.Round(oW / cW));
                int crudeFromOre = Math.Max(1, (int)Math.Round(oW * crude / iW * (ironMaker ? 1.25f : 1f)));
                yield return new Crafting.RefiningFormula(CraftingMaterials.Wood, 1, CraftingMaterials.Iron1, 0, CraftingMaterials.Charcoal, coalFromWood);
                yield return new Crafting.RefiningFormula(CraftingMaterials.IronOre, 1, CraftingMaterials.Charcoal, coalPerOre, CraftingMaterials.Iron1, crudeFromOre);
                yield return new Crafting.RefiningFormula(CraftingMaterials.Iron1, 5, CraftingMaterials.Charcoal, 1, CraftingMaterials.Iron2, 4);
                yield return new Crafting.RefiningFormula(CraftingMaterials.Iron2, 5, CraftingMaterials.Charcoal, 1, CraftingMaterials.Iron3, 4);
                if (steel1) yield return new Crafting.RefiningFormula(CraftingMaterials.Iron3, 5, CraftingMaterials.Charcoal, 1, CraftingMaterials.Iron4, 4);
                if (steel2) yield return new Crafting.RefiningFormula(CraftingMaterials.Iron4, 5, CraftingMaterials.Charcoal, 1, CraftingMaterials.Iron5, 4);
                if (steel3) yield return new Crafting.RefiningFormula(CraftingMaterials.Iron5, 5, CraftingMaterials.Charcoal, 2, CraftingMaterials.Iron6, 4);
                yield break;
            }
            yield return new Crafting.RefiningFormula(CraftingMaterials.Wood, 1, CraftingMaterials.Iron1, 0, CraftingMaterials.Charcoal, charcoal ? 5 : 4);
            yield return new Crafting.RefiningFormula(CraftingMaterials.IronOre, 1, CraftingMaterials.Charcoal, bloomCoal, CraftingMaterials.Iron1, ironMaker ? 4 : 3);
            yield return new Crafting.RefiningFormula(CraftingMaterials.Iron1, 5, CraftingMaterials.Charcoal, 2, CraftingMaterials.Iron2, 4);
            yield return new Crafting.RefiningFormula(CraftingMaterials.Iron2, 5, CraftingMaterials.Charcoal, 2, CraftingMaterials.Iron3, 4);
            if (steel1) yield return new Crafting.RefiningFormula(CraftingMaterials.Iron3, 5, CraftingMaterials.Charcoal, 3, CraftingMaterials.Iron4, 4);
            if (steel2) yield return new Crafting.RefiningFormula(CraftingMaterials.Iron4, 5, CraftingMaterials.Charcoal, 4, CraftingMaterials.Iron5, 4);
            if (steel3) yield return new Crafting.RefiningFormula(CraftingMaterials.Iron5, 5, CraftingMaterials.Charcoal, 5, CraftingMaterials.Iron6, 4);
        }

        private static bool Perk(Hero h, PerkObject p)
        {
            try { return p != null && h.GetPerkValue(p); } catch { return false; }
        }

        /// <summary>XP za przetop: vanilla 0.3 x cena wyjscia x ilosc - przy nowych cenach
        /// liczymy od ceny SPRZED prawa surowcow, zeby kowalstwo nie skoczylo kilka razy.</summary>
        public static void RefineXpPostfix(ref Crafting.RefiningFormula refineFormula, ref int __result)
        {
            try
            {
                if (!_applied) return;
                var it = Recipes.MaterialItem(refineFormula.Output);
                if (it == null || it.Value <= 0) return;
                int orig = Orig(it);
                if (orig > 0 && orig != it.Value) __result = Math.Max(1, (int)Math.Round(__result * (double)orig / it.Value));
            }
            catch { }
        }

        // ------------------------------------------------------------ 3. wydobycie
        /// <summary>W ilu modelach produkcji wsi siedzi ProdPostfix (0 = w zadnym). MineralOnce zdejmuje powtorzenia z listy BK tylko
        /// wtedy, gdy ten postfix jest wpiety - to on oddaje zdjety wpis mnoznikiem (bez niego mineral spadlby o polowe).</summary>
        internal static int ProdModels;
        [ThreadStatic] private static int _depth;
        public static void ProdPrefix() { _depth++; }
        public static Exception ProdFinalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }
        public static void ProdPostfix(Village village, ItemObject item, ref ExplainedNumber __result)
        {
            if (_depth > 1) return;                    // BK/BEE woluja model bazowy - mnozymy raz
            try
            {
                if (item == null) return;
                var s = Settings.Current;
                string id = item.StringId ?? "";
                float m = 1f;
                if (On && id == "iron") m = s.MineOutputMultiplier * (s.MineOutputStep > 0f ? s.MineOutputStep : 1f);   // 174.3: krok wariantu B Jeffa - ruda razem z rekami
                else if (On && id == "hardwood") m = s.LumberOutputMultiplier;
                // wpis 87 (audyt pkt 13): dzielenie przez ladunek zawsze, gdy waga jest x10 - inaczej wylaczenie MaterialLaw = 10x kg rudy
                m /= HistoricalPrices.BulkScale(item);      // wpis 50: ladunek 100 kg - tyle samo kg co dotad
                // suwak 0 (albo ujemny): mnoznika nie stosujemy i wynik modelu zostaje, jaki byl (znana usterka: to nie jest zero wydobycia).
                // Powtorzenia BK liczymy takze wtedy - inaczej przy suwaku 0 wies dostawalaby polowe tego, co dotad
                if (!(m > 0f)) m = 1f;
                // MineralOnce: BK mial ten mineral na liscie produkcji wsi kilka razy - zdjelismy powtorzenia, a jeden wpis liczymy
                // tyle razy, ile ich bylo (wydobycie bez zmian; niezalezne od wlacznika MaterialLaw i od suwakow)
                m *= MineralOnce.Times(village, item);
                if (Math.Abs(m - 1f) >= 0.001f)
                {
                    // wpis 98: mnoznik ma MNOZYC. ExplainedNumber SUMUJE czynniki (wynik = baza x (1 + suma)), wiec AddFactor(m - 1)
                    // dodawal sie do zimy, drog i praw BK: przy m = 0.3 (x3 w ladunkach) i Dlugiej Nocy (-38..-62%) wies dawala
                    // baza x (0.3 + f - zima) - okolo 1/3 zamiaru, a 5 z 26 wsi z ruda zero. Czynnik (m - 1) x (1 + suma) = wynik x m.
                    float sum = 1f + __result.SumOfFactors;
                    if (sum > 0f) __result.AddFactor((m - 1f) * sum, new TextObject("{=!}Armoury: mines and woods"));
                }
                OreLedger.NoteModel(village, item, __result.ResultNumber);   // wpis 98: wynik modelu PO mnozniku (ksiega - tylko log)
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var rf = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel), "GetRefiningFormulas");
                if (rf != null) h.Patch(rf, postfix: new HarmonyMethod(typeof(MaterialLaw), nameof(RefinePostfix)) { priority = Priority.High });
                var rx = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel), "GetSkillXpForRefining");
                // poprawka po audycie TOWARY 3: przeliczenie XP od dawnej wartosci MUSI biec PRZED sufitem SmithAudit.RefineXpPostfix (p400) - przy
                // rownym priorytecie sufit szedl pierwszy (ApplyAll SmithAudit przed nami) i przeliczenie x dawna/nowa wartosc wynosilo XP ponad
                // RefineXpCap (stal: 133-200 zamiast 60). Priority.High - niezaleznie od kolejnosci ApplyAll.
                if (rx != null) h.Patch(rx, postfix: new HarmonyMethod(typeof(MaterialLaw), nameof(RefineXpPostfix)) { priority = Priority.High });
                int prod = 0;
                var seen = new HashSet<Type>();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch { continue; }
                    foreach (var t in types)
                    {
                        try
                        {
                            if (t == null || t.IsAbstract || !typeof(VillageProductionCalculatorModel).IsAssignableFrom(t)) continue;
                            var m = t.GetMethod("CalculateDailyProductionAmount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (m == null || m.DeclaringType != t || seen.Contains(t)) continue;
                            seen.Add(t);
                            h.Patch(m, prefix: new HarmonyMethod(typeof(MaterialLaw), nameof(ProdPrefix)) { priority = Priority.First },
                                       postfix: new HarmonyMethod(typeof(MaterialLaw), nameof(ProdPostfix)) { priority = Priority.Last },
                                       finalizer: new HarmonyMethod(typeof(MaterialLaw), nameof(ProdFinalizer)));
                            prod++;
                        }
                        catch { }
                    }
                }
                ProdModels = prod;
                Log.Info("MaterialLaw: wytop " + (rf != null ? "wpiety" : "BRAK GetRefiningFormulas")
                         + ", XP przetopu " + (rx != null ? "wpiete" : "BRAK") + ", wydobycie w " + prod + " modelach.");
            }
            catch (Exception e) { Log.Error("MaterialLaw.ApplyAll", e); }
        }
    }
}
