using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
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
        private static readonly Dictionary<ItemObject, int> _target = new Dictionary<ItemObject, int>();

        /// <summary>Towary handlowe: pensy za kg (ujemne = za sztuke). Zrodla: docs/CENY-HISTORYCZNE.md.</summary>
        private static readonly Dictionary<string, float> TradeGoodsPerKg = new Dictionary<string, float>
        {
            // zywnosc
            { "grain", 0.31f }, { "bread", 0.55f }, { "beer", 0.3f }, { "wine", 1.4f }, { "mead", 1.0f },
            { "meat", 0.9f }, { "fish", 1.0f }, { "WhaleMeat", 0.7f }, { "cheese", 1.3f }, { "butter", 2.4f },
            { "Egg", 0.8f }, { "honey", 1.4f }, { "salt", 0.18f }, { "oil", 4.0f }, { "whale_oil", 2.4f },
            { "olives", 0.5f }, { "grape", 0.3f }, { "date_fruit", 6.0f }, { "apple", 0.2f }, { "orange", 1.0f },
            { "carrot", 0.15f }, { "garum", 2.0f }, { "pie", 1.0f }, { "spice", 27f },
            // wlokna, tkaniny, futra (len, plotno, skory - wyzej, wlasne ustawienia)
            { "wool", 8.3f }, { "felt", 20f }, { "cotton", 100f }, { "velvet", 400f }, { "fur", 20f },
            // rzemioslo, budulec
            { "tools", 4f }, { "pottery", 0.8f }, { "clay", 0.05f }, { "planks", 0.15f }, { "limestone", 0.02f }, { "marble", 0.2f },
            // kruszce, luksus
            { "silver", 2f }, { "gold_ore", 5f }, { "goldingot", 9500f }, { "Ink", 10f }, { "Papyrus", 10f }, { "PurpleDye", 30f },
            { "walrus_tusk", 30f }, { "jewelry", -2400f }, { "pouchofgems", -1200f },
        };

        /// <summary>Wpis 58: codzienna kontrola - czy ktos (inny mod) nie nadpisal przeliczonych wartosci; jesli tak, przywracamy i logujemy.</summary>
        // ------------------------------------------------------------ blokada cen (wpis 72)
        // Jeff 05.10: "trzeba wpisac regule, ze nie moze nadpisac - ceny czytane sa z tego, bo to sa ceny". Prefix na setterze
        // ItemObject.Value: po przeliczeniu zadna zmiana wartosci przeliczonego przedmiotu (inny mod: BK AdjustPrices, kto
        // nadpisywal 7 lukow) nie przechodzi; pierwsza proba dla kazdego przedmiotu idzie do logu ze stosem wywolan (kto).
        [ThreadStatic] private static bool _ourSet;
        private static readonly HashSet<ItemObject> _blockedLogged = new HashSet<ItemObject>();
        private static int _blocked;
        public static bool ValueSetPrefix(ItemObject __instance, int value)
        {
            try
            {
                if (_ourSet || !_applied || __instance == null) return true;
                int t;
                if (!_target.TryGetValue(__instance, out t) || t == value) return true;
                _blocked++;
                if (_blockedLogged.Add(__instance) && _blockedLogged.Count <= 40)
                {
                    string who = "";
                    try
                    {
                        var st = new System.Diagnostics.StackTrace(2, false);
                        var parts = new List<string>();
                        for (int i = 0; i < st.FrameCount && parts.Count < 4; i++)
                        {
                            var m = st.GetFrame(i).GetMethod();
                            if (m == null || m.DeclaringType == null) continue;
                            parts.Add(m.DeclaringType.FullName + "." + m.Name);
                        }
                        who = string.Join(" <- ", parts.ToArray());
                    }
                    catch { }
                    Log.Info("HistoricalPrices: ZABLOKOWANO zmiane ceny " + __instance.StringId + " " + t + " -> " + value + " (wola: " + who + ").");
                }
                return false;
            }
            catch { return true; }
        }

        public static void RotRbmPostfix()
        {
            if (!_applied) { Log.Info("HistoricalPrices: ROT-RBM nadpisal ceny lukow przed przeliczeniem - przeliczenie je zastapi."); return; }
            Log.Info("HistoricalPrices: ROT-RBM (ROTRBMCompatibility) nadpisal ceny lukow ROT - przywracam.");
            Recheck();
        }

        internal static void Recheck()
        {
            if (!_applied || _target.Count == 0) return;
            try
            {
                var setter = AccessTools.PropertySetter(typeof(ItemObject), "Value");
                if (setter == null) return;
                var bad = new List<string>(); int n = 0;
                foreach (var kv in _target)
                {
                    if (kv.Key == null || kv.Key.Value == kv.Value) continue;
                    n++;
                    if (bad.Count < 12) bad.Add(kv.Key.StringId + " " + kv.Key.Value + "->" + kv.Value);
                    _ourSet = true; try { setter.Invoke(kv.Key, new object[] { kv.Value }); } finally { _ourSet = false; }
                }
                if (n > 0) Log.Info("HistoricalPrices: kontrola - " + n + " przedmiotow mialo zmieniona wartosc (inny mod?), przywrocone: " + string.Join(", ", bad.ToArray()) + ".");
            }
            catch (Exception e) { Log.Error("HistoricalPrices.Recheck", e); }
        }

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

        // wpis 87 (audyt pkt 12): nowa kampania = nowe obiekty przedmiotow - stare slowniki rosly, _applied zostawalo true,
        // a diagnostyka blokady (40 wpisow) wyczerpywala sie na cale uruchomienie gry
        // Konstruktor ArmouryBehavior (OnGameStart) biegnie PRZED definicjami przedmiotow tej kampanii (Campaign: OnGameStart ->
        // InitializeDefaultCampaignObjects -> DefaultItems -> BKItems.Initialize), wiec czyszczenie definicji niczego nie gubi.
        internal static void Reset() { _target.Clear(); _orig.Clear(); _origWeight.Clear(); _blockedLogged.Clear(); _defValue.Clear(); _defLeft.Clear(); _defUsed.Clear(); _errDefine = false; _stDefine = 0; _shelfWorth.Clear(); _shelfByValue.Clear(); _mixed.Clear(); _errShelf = false; _stShelf = 0; _applied = false; RawPrice.Reset(); }   // RawPrice: cena surowcow liczy na tym przeliczeniu - czysci sie razem z nim; wagi polki (paczka 121) - nowe przedmioty kampanii

        // ------------------------------------------------------------ wartosc z definicji przedmiotu (paczka "towary w nowej monecie")
        // Test 06.10 14:08 (log, linia "surowce kuzni"): chleb 0 -> 6, jajka 0 -> 8, miod 0 -> 14, owoce 0 -> 2..10, garum 0 -> 20,
        // ciasto 0 -> 10, wapien 0 -> 1, papirus 0 -> 100, miod pitny 1 -> 10, futro 1 -> 200, marmur 1 -> 2, atrament 2 -> 100,
        // ruda zlota 4 -> 50. Przyczyna: BKROTPatch (BKItemsInitializePatch) w prefiksie BKItems.InitializeTradeGood robi
        // "value /= 100" (liczby calkowite) dla KAZDEGO towaru BK - takze futra, ktore BK definiuje drugi raz w AdjustPrices
        // (OnNewGameCreated / OnGameLoaded, przed nami). Popyt kategorii BK (BKItemCategories: chleb 100/5, miod 15/30...) i
        // futra (gra) liczono do wartosci z definicji (chleb 20, miod 28, miod pitny 120, futro 125), nie do jednej setnej:
        // przelicznik z zera nie istnieje (kategoria zostawala w starej monecie), a z jedynki wychodzil odwrotny (miod pitny /0.1,
        // futro /0.005 - popyt x200). Regula: przelicznik kazdego przedmiotu liczony od wartosci, ktora podala mu jego definicja,
        // jesli od tamtej chwili nikt jej nie zmienil inaczej (XML wczytany pozniej - np. przyprawy 300 - wygrywa, bo to on jest
        // ostatnia definicja). Nasz prefiks ma pierwszenstwo First, wiec widzi wartosc przed dzieleniem BKROTPatch; postfiks
        // zapisuje, co zostalo. Bez BK albo bez BKROTPatch definicja = wartosc w chwili Apply - nic sie nie zmienia.
        private static readonly Dictionary<ItemObject, int> _defValue = new Dictionary<ItemObject, int>();   // wartosc podana w definicji
        private static readonly Dictionary<ItemObject, int> _defLeft = new Dictionary<ItemObject, int>();    // wartosc, ktora po definicji zostala (po latkach innych modow)
        private static readonly HashSet<ItemCategory> _defUsed = new HashSet<ItemCategory>();               // kategorie z przedmiotem, ktorego definicja rozni sie od wartosci w chwili Apply
        private static bool _errDefine;
        private static int _stDefine;          // audyt 120: potkniecia przy zapisie definicji (kazde liczone, w logu pierwsze) - w linii startowej przelicznika
        private static int _defineHooks;

        /// <summary>Prefiks BKItems.InitializeTradeGood(item, name, mesh, category, value, ...) z pierwszenstwem First: wartosc z definicji BK.</summary>
        public static void DefinePrefix(ItemObject __0, int __4, out int __state)
        {
            __state = __4;
        }

        /// <summary>Postfiks tej samej metody: co zostalo w przedmiocie po definicji (po prefiksach innych modow).</summary>
        public static void DefinePostfix(ItemObject __0, int __state)
        {
            try
            {
                if (__0 == null || _applied) return;   // po przeliczeniu (AdjustPrices BK w OnSessionLaunched po nas) wartosci trzyma blokada cen - nic do zapisania
                _defValue[__0] = __state;
                _defLeft[__0] = __0.Value;
            }
            catch (Exception e)
            {
                _stDefine++;
                if (!_errDefine) { _errDefine = true; Log.Error("HistoricalPrices.DefinePostfix", e); }   // raz na kampanie; przedmiot zostaje przy wartosci z chwili Apply
            }
        }

        /// <summary>Wartosc, od ktorej liczymy przelicznik popytu: z definicji, gdy po niej nikt wartosci nie zmienil inaczej
        /// (before = wartosc w chwili Apply, sprzed MaterialLaw); inaczej before - jak dotad.</summary>
        private static int DemandBase(ItemObject it, int before)
        {
            int def, left;
            if (it != null && _defValue.TryGetValue(it, out def) && _defLeft.TryGetValue(it, out left) && left == before && def > 0) return def;
            return before;
        }

        /// <summary>Kategorie z przedmiotem, ktorego definicja rozni sie od wartosci w chwili Apply - przy wlaczonym HistDemandFromDefinition ich przelicznik jest z definicji (linia dnia "Ceny surowcow").</summary>
        internal static List<ItemCategory> DefinedCategories() { return new List<ItemCategory>(_defUsed); }

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

        internal static float WageFor(int tier)
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
            float mat = c.MetalKg * MetalPerKg(c.Grade)
                        + c.MetalKg * FuelPerMetalKg()               // wpis 48: drewno na wegiel tyle, ile spala warsztat (WorkshopLaw.Needs)
                        + c.LeatherKg * s.HistLeatherPerKg
                        + c.LinenKg * s.HistLinenPerKg
                        + c.WoodKg * s.HistWoodPerKg
                        + c.Special * s.HistSpecialFactor;           // rog, sciegno, klej lukow (w modelu w skali gry)
            float labor = MakingLabor(it, c);
            return (mat + labor) * (1f + Math.Max(0f, s.HistProfitPercent) / 100f);
        }

        /// <summary>Robota wykonania sztuki w pensach: dni pracy (HistDays) x dniowka mistrza wedle tieru - ta sama w wartosci sztuki
        /// (HistCost) i w naprawie u kowali miasta (MendMaterial.Labor: ulamek tego).</summary>
        internal static float MakingLabor(ItemObject it, ArmsPricing.Cost c)
        {
            int t = Math.Max(1, Math.Min(6, (int)it.Tier + 1));
            float days = HistDays(it, c);
            float wage = IsAmmo(it) ? WageFor(1) : WageFor(t);      // test 04.10: dniowka mistrza t6 dawala snop strzal za 130 d - fletcher to zwykly rzemieslnik
            return days * wage;
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
            float woodKg = Math.Max(0f, s.WorkshopWoodPerOre) * 10f / Math.Max(0.1f, s.WorkshopCrudeKgPerOre) + Math.Max(0f, s.WorkshopForgeWoodPerMetalKg);
            return woodKg * s.HistWoodPerKg;
        }

        /// <summary>wpis 90 (audyt 1-43 F1): bron wykuta W TRAKCIE sesji to nowy ItemObject z cena w skali gry (DetermineValue) -
        /// Apply przy starcie go nie widzial; sprzedaz takiego miecza dawala tysiace pensow z kilku pensow sztabek.</summary>
        internal static void PriceOne(ItemObject it)
        {
            try
            {
                if (!On || !_applied || it == null || !IsArms(it) || it.Value <= 0 || _target.ContainsKey(it)) return;
                var setter = AccessTools.PropertySetter(typeof(ItemObject), "Value");
                if (setter == null) return;
                float hc = HistCost(it);
                if (hc <= 0f) return;
                if (ArmsPricing.IsUnique(it)) hc *= Math.Max(1f, Settings.Current.HistUniquePrestige);
                int was = it.Value;
                if (!_orig.ContainsKey(it)) _orig[it] = was;
                _ourSet = true; try { setter.Invoke(it, new object[] { Math.Max(1, (int)Math.Round(hc)) }); } finally { _ourSet = false; }
                _target[it] = it.Value;
                Log.Info("HistoricalPrices: wykuty " + it.StringId + " " + was + " -> " + it.Value + " d.");
            }
            catch (Exception e) { Log.Error("HistoricalPrices.PriceOne", e); }
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
                    if (!_orig.ContainsKey(it)) _orig[it] = MaterialLaw.Orig(it);   // wpis 87 (audyt pkt 11b): cena sprzed MaterialLaw, nie po
                    _ourSet = true; try { setter.Invoke(it, new object[] { Math.Max(1, (int)Math.Round(v)) }); } finally { _ourSet = false; }
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
                    _target[it] = it.Value;   // wpis 87 (audyt pkt 11a): surowce tez pod blokada cen
                    raw.Add(kv.Key + " " + _orig[it] + "->" + it.Value);
                }
                // 1b. towary handlowe (wpis 71, Jeff 05.10: "narzedzia 173, jedwab, ruda srebra ... no to przelicz") - cena za kg
                // z docs/CENY-HISTORYCZNE.md (Clark/Rogers 1300-49; [S] = szacunek) x waga sztuki z gry (zwykle 10 kg);
                // ponizej 1 d za sztuke gra i tak pokaze 1 (rachunki warsztatow licza w ulamkach - wpis 68)
                if (s.HistTradeGoods)
                    foreach (var kv in TradeGoodsPerKg)
                    {
                        var it = MBObjectManager.Instance.GetObject<ItemObject>(kv.Key);
                        if (it == null) continue;
                        set(it, kv.Value < 0 ? -kv.Value : Math.Max(0.1f, it.Weight) * kv.Value);   // ujemna = cena za SZTUKE
                        _target[it] = it.Value;
                        raw.Add(it.StringId + " " + _orig[it] + "->" + it.Value);
                    }

                foreach (var g in new[] { CraftingMaterials.Iron1, CraftingMaterials.Iron2, CraftingMaterials.Iron3, CraftingMaterials.Iron4, CraftingMaterials.Iron5, CraftingMaterials.Iron6 })
                {
                    var it = Recipes.MaterialItem(g);
                    if (it == null) continue;
                    set(it, Math.Max(0.5f, it.Weight) * MetalPerKg(g));
                    _target[it] = it.Value;
                    raw.Add(it.StringId + " " + _orig[it] + "->" + it.Value);
                }

                // 2. bron i zbroje z kosztu historycznego
                int n = 0; long before = 0, after = 0;
                var samples = new List<string>();
                var watch = new HashSet<string> { "stark_boots_1", "battania_sword_5_t5", "casterly_heavy_helm", "sturgian_fortified_armor", "ramsay_armor", "weirwood_bow", "giant_bow", "giant_arrows", "ravens_teeth_longbow" };
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || !IsArms(it) || it.Value <= 0) continue;
                    float hc = HistCost(it);
                    if (hc <= 0f) continue;
                    if (ArmsPricing.IsUnique(it)) hc *= Math.Max(1f, s.HistUniquePrestige);
                    int was = it.Value;
                    set(it, hc);
                    _target[it] = it.Value;
                    n++; before += was; after += it.Value;
                    if (watch.Contains(it.StringId)) samples.Add(it.StringId + " (" + it.ItemType + " t" + ((int)it.Tier + 1) + ", " + it.Weight.ToString("0.0", CultureInfo.InvariantCulture) + " kg) " + was + " -> " + it.Value + " d");
                }
                // 3. popyt miast w nowej monecie: srednia geometryczna (stara/nowa wartosc) przedmiotow kazdej kategorii;
                // stara = wartosc z definicji przedmiotu (HistDemandFromDefinition; opis przy _defValue), wylaczone - wartosc z chwili Apply
                // _defUsed: kategorie, w ktorych definicja rozni sie od wartosci z chwili Apply - zapisywane takze przy wylaczonym
                // wlaczniku (linia dnia pokazuje je wtedy bez przelicznika - do porownania przed / po w tej samej grze)
                _catRatio.Clear(); _defUsed.Clear();
                bool fromDef = s.HistDemandFromDefinition;
                var sumLog = new Dictionary<ItemCategory, double>(); var cnt = new Dictionary<ItemCategory, int>();
                var defLog = new List<string>(); var zeroLog = new List<string>();
                foreach (var kv in _orig)
                {
                    var cat = kv.Key.ItemCategory;
                    if (cat == null || kv.Key.Value <= 0) continue;
                    int def = DemandBase(kv.Key, kv.Value);
                    if (def != kv.Value) { _defUsed.Add(cat); defLog.Add(kv.Key.StringId + " " + kv.Value + " -> " + def); }
                    int old = fromDef ? def : kv.Value;
                    if (old <= 0) { zeroLog.Add(kv.Key.StringId + " (" + cat.StringId + ")"); continue; }
                    double l; sumLog.TryGetValue(cat, out l); sumLog[cat] = l + Math.Log((double)old / kv.Key.Value * BulkScale(kv.Key));   // za kg - ladunek to tyle samo towaru co 10 starych sztuk
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
                Log.Info("HistoricalPrices: przelicznik popytu od wartosci z definicji przedmiotu " + (fromDef ? "CZYNNY" : "WYLACZONY (MCM) - od wartosci z chwili przeliczenia, jak dotad")
                         + " (definicje BK widziane przed latkami innych modow: " + _defValue.Count + ", wpiete w " + _defineHooks + " metodach); "
                         + (defLog.Count > 0 ? (fromDef ? "wziete z definicji " : "definicja inna niz wartosc w chwili przeliczenia (NIEUZYTE) ") + defLog.Count + " [" + string.Join(", ", defLog.ToArray()) + "] w " + _defUsed.Count + " kategoriach"
                                             : "zadna definicja nie rozni sie od wartosci w chwili przeliczenia")
                         + "; potkniecia przy zapisie definicji (wyjatki, pierwszy w logu) " + _stDefine + ".");
                if (zeroLog.Count > 0)
                    Log.Info("HistoricalPrices: UWAGA - " + zeroLog.Count + " przeliczonych przedmiotow ma wartosc 0 i nie ma od czego liczyc przelicznika (" + (fromDef ? "brak wartosci z definicji" : "wartosc z definicji wylaczona w MCM")
                             + "), ich kategorie licza popyt bez nich - bez przelicznika, gdy nie maja innych przedmiotow: " + string.Join(", ", zeroLog.ToArray()) + ".");
                if (_origWeight.Count > 0) Log.Info("HistoricalPrices: ruda i drewno w ladunkach - " + string.Join(", ", _origWeight.Select(kv => kv.Key.StringId + " " + kv.Value + " -> " + kv.Key.Weight + " kg = " + kv.Key.Value + " d").ToArray()) + ".");
                Log.Info("HistoricalPrices: surowce kuzni ["+ string.Join(", ", raw.ToArray()) + "]; uzbrojenie " + n + " szt. przeliczone z kosztu historycznego (suma wartosci "
                         + before + " -> " + after + "). Przyklady: " + string.Join("; ", samples.ToArray()) + ".");
                MixedShelf(s, fromDef);   // paczka 121: wagi sztuk na polce w kategoriach mieszanych - po przeliczniku kategorii, ten sam skladnik przedmiotu
            }
            catch (Exception e) { Log.Error("HistoricalPrices.Apply", e); }
        }

        // ------------------------------------------------------------ kategorie mieszane: waga sztuki na polce (paczka 121)
        // Audyt 07.10 (W2): w kategorii "gold" ruda zlota (400 -> 50, 8 x tansza) i sztabka (1000 -> 4750, 4.75 x drozsza) maja jeden
        // przelicznik popytu (srednia geometryczna 1.30). Wzor ceny gry liczy JEDEN indeks kategorii: (popyt / (0.1 x podaz + 0.04 x
        // wartosc polki + 2))^0.6, a popyt i polka sa w monecie. Popyt zlota w miescie-medianie to ok. 45 nowych d dziennie, wiec jedna
        // sztabka (4750) to ok. 100 dni popytu (w projekcie BK 1000 / 58 = 17): pierwsza sprzedana 0.42 x wartosci, druga 0.28, ruda
        // przy pustej polce 4.6 x (projekt BK: sztabka 1.21 x, ruda 2.02 x). Zaden jeden popyt nie da obu naraz - ruda i sztabka
        // zmienily wzajemny stosunek 38 razy, a popyt kategorii jest jeden.
        // Regula: w danych rynku miasta (wartosc polki, podaz z niej wygladzana, sprzedawana sztuka) kazda sztuka wazy swoja wartosc
        // x (przelicznik przedmiotu / przelicznik, w ktorym liczony jest popyt kategorii) - czyli swoja wartosc z definicji w monecie
        // popytu. Kazda sztuka zapelnia rynek tyle dni popytu, ile w projekcie jej kategorii; cena dalej = nowa wartosc x indeks.
        // Kategoria jednego przedmiotu albo przedmiotow przeliczonych jednakowo: waga = wartosc (nic sie nie zmienia). Moneta popytu =
        // przelicznik kategorii (DemandPostfix); popyt surowca masowego z prawdziwego zuzycia (RawPrice B) liczony jest w wartosci
        // samego surowca - tam moneta popytu = przelicznik tego surowca (ruda zostaje przy swojej wartosci, wagi dostaja sztabki).
        // Uzbrojenie nie: jego cene prowadzi prawo podazy i popytu w sztukach (SupplyDemand), wzor gry zaciska je do 0.8-1.3.
        // Tylko miasta: zamek ma popyt 0 (indeks na podlodze 0.1, polka nie gra roli), a jego danych rynku gra nigdy nie przebudowuje
        // - waga wpisana raz zostalaby tam po wylaczeniu albo cofnieciu paczki.
        // Wagi licza sie raz na sesje (Apply) i nie zmieniaja do nastepnego wczytania - dodawane przyrostowo i przy przebudowie
        // polek musza byc te same.
        internal sealed class MixedCat { public ItemCategory Cat; public float Coin; public bool ByUse; public readonly List<ItemObject> Items = new List<ItemObject>(); public readonly List<int> Worth = new List<int>(); }
        private static readonly Dictionary<ItemObject, int> _shelfWorth = new Dictionary<ItemObject, int>();                       // waga rozna od wartosci (puste = wylaczone)
        private static readonly Dictionary<ItemCategory, Dictionary<int, int>> _shelfByValue = new Dictionary<ItemCategory, Dictionary<int, int>>();   // sprzedawana sztuka: (kategoria, wartosc) -> waga; -1 = niejednoznaczne
        private static readonly List<MixedCat> _mixed = new List<MixedCat>();                                                    // plan (liczony takze przy wylaczonym - do logu)
        private static bool _errShelf;
        private static int _stShelf;
        private static int _shelfHooks;        // 2 = obie latki danych rynku wpiete (na cale uruchomienie gry, jak _defineHooks)
        private static AccessTools.FieldRef<TownMarketData, Town> _marketTown;

        /// <summary>Waga sztuki w danych rynku miasta (jednostka polki): przy wylaczonej paczce i w kategoriach jednorodnych = Value.</summary>
        internal static int ShelfWorth(ItemObject it)
        {
            if (it == null) return 0;
            int w;
            return _shelfWorth.Count > 0 && _shelfWorth.TryGetValue(it, out w) ? w : it.Value;
        }

        /// <summary>Waga sprzedawanej sztuki dla modelu ceny (zna tylko kategorie i wartosc sztuki); bez wpisu albo niejednoznaczne - wartosc.</summary>
        internal static int ShelfWorthOf(ItemCategory cat, int value)
        {
            Dictionary<int, int> m; int w;
            if (_shelfByValue.Count == 0 || cat == null || !_shelfByValue.TryGetValue(cat, out m) || !m.TryGetValue(value, out w) || w < 0) return value;
            return w;
        }

        /// <summary>Kategorie mieszane tej sesji (linia dnia "Ceny surowcow"); Active = wagi czynne.</summary>
        internal static List<MixedCat> MixedCategories() { return new List<MixedCat>(_mixed); }
        internal static bool MixedActive { get { return _shelfWorth.Count > 0; } }

        private static void MixedShelf(Settings s, bool fromDef)
        {
            try
            {
                _shelfWorth.Clear(); _shelfByValue.Clear(); _mixed.Clear();
                // 1. przelicznik kazdego przeliczonego towaru handlowego - ten sam skladnik, z ktorego Apply liczy przelicznik kategorii
                var own = new Dictionary<ItemObject, double>(); var sum = new Dictionary<ItemCategory, double>(); var cnt = new Dictionary<ItemCategory, int>();
                foreach (var kv in _orig)
                {
                    var it = kv.Key; var cat = it.ItemCategory;
                    if (cat == null || !cat.IsTradeGood || it.Value <= 0) continue;
                    int old = fromDef ? DemandBase(it, kv.Value) : kv.Value;
                    if (old <= 0) continue;
                    double l = Math.Log((double)old / it.Value * BulkScale(it));
                    own[it] = l;
                    double a; sum.TryGetValue(cat, out a); sum[cat] = a + l;
                    int k; cnt.TryGetValue(cat, out k); cnt[cat] = k + 1;
                }
                // 2. moneta popytu kategorii (logarytm): ten przelicznik, ktorym DemandPostfix naprawde dzieli popyt (_catRatio); kategorii
                // z przelicznikiem blizej 1 niz 2% Apply nie wpisuje - jej popyt zostaje nieprzeliczony, moneta 1 (recenzja 121: srednia
                // dalaby tam wagi do 2% obok monety popytu); surowiec masowy z popytem z prawdziwego zuzycia - jego wlasny
                var coin = new Dictionary<ItemCategory, double>(); var byUse = new HashSet<ItemCategory>();
                foreach (var kv in sum) coin[kv.Key] = _catRatio.ContainsKey(kv.Key) ? kv.Value / cnt[kv.Key] : 0.0;
                if (s.RawPriceByUse && s.HistDemandScaling)
                    foreach (var b in CaravanBulk.Items())
                    {
                        double l;
                        if (b == null || b.ItemCategory == null || !coin.ContainsKey(b.ItemCategory) || !own.TryGetValue(b, out l)) continue;
                        coin[b.ItemCategory] = l; byUse.Add(b.ItemCategory);
                    }
                // 3. waga kazdego przedmiotu tych kategorii - takze nieprzeliczonego (jego przelicznik: z definicji albo 1)
                var byCat = new Dictionary<ItemCategory, List<ItemObject>>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    var cat = it != null ? it.ItemCategory : null;
                    if (cat == null || !coin.ContainsKey(cat) || it.Value <= 0) continue;
                    List<ItemObject> l; if (!byCat.TryGetValue(cat, out l)) byCat[cat] = l = new List<ItemObject>();
                    l.Add(it);
                }
                foreach (var kv in byCat)
                {
                    double c = coin[kv.Key];
                    MixedCat m = null;
                    foreach (var it in kv.Value)
                    {
                        double l;
                        if (!own.TryGetValue(it, out l))
                        {
                            int def = fromDef ? DemandBase(it, it.Value) : it.Value;
                            l = def > 0 ? Math.Log((double)def / it.Value * BulkScale(it)) : c;
                        }
                        int w = Math.Max(1, (int)Math.Round(it.Value * Math.Exp(l - c), MidpointRounding.AwayFromZero));   // jak gra: sztuka z wartoscia wazy co najmniej 1
                        if (w == it.Value) continue;
                        if (m == null) m = new MixedCat { Cat = kv.Key, Coin = (float)Math.Exp(c), ByUse = byUse.Contains(kv.Key) };
                        m.Items.Add(it); m.Worth.Add(w);
                    }
                    if (m != null) _mixed.Add(m);
                }
                // 4. sprzedawana sztuka: model ceny zna tylko kategorie i wartosc - tablica (kategoria, wartosc) -> waga po WSZYSTKICH
                // przedmiotach kategorii; dwa przedmioty o tej samej wartosci i roznej wadze = niejednoznaczne (liczona wartosc, w logu)
                int ambiguous = 0;
                var byValue = new Dictionary<ItemCategory, Dictionary<int, int>>();
                foreach (var m in _mixed)
                {
                    var map = new Dictionary<int, int>();
                    foreach (var it in byCat[m.Cat])
                    {
                        int i = m.Items.IndexOf(it), w = i >= 0 ? m.Worth[i] : it.Value, had;
                        if (!map.TryGetValue(it.Value, out had)) map[it.Value] = w;
                        else if (had != w && had >= 0) { map[it.Value] = -1; ambiguous++; }
                    }
                    byValue[m.Cat] = map;
                }
                bool on = s.HistMixedCategoryShelf && s.HistDemandScaling && _shelfHooks == 2;
                int towns = 0;
                if (on && _mixed.Count > 0)
                {
                    foreach (var m in _mixed) for (int i = 0; i < m.Items.Count; i++) _shelfWorth[m.Items[i]] = m.Worth[i];
                    foreach (var kv in byValue) _shelfByValue[kv.Key] = kv.Value;
                    towns = FixStores();
                }
                var parts = new List<string>();
                foreach (var m in _mixed)
                {
                    var p = new List<string>();
                    for (int i = 0; i < m.Items.Count; i++) p.Add(m.Items[i].StringId + " " + m.Items[i].Value + " -> " + m.Worth[i]);
                    parts.Add(m.Cat.StringId + " (moneta popytu /" + m.Coin.ToString("0.##", CultureInfo.InvariantCulture) + (m.ByUse ? " - popyt z prawdziwego zuzycia surowca" : "") + "): " + string.Join(", ", p.ToArray()));
                }
                Log.Info("HistoricalPrices: kategorie mieszane - waga sztuki na polce wedle przelicznika przedmiotu "
                         + (on ? "CZYNNA" : !s.HistMixedCategoryShelf ? "WYLACZONA (MCM) - wagi NIEUZYTE, polka liczona wartoscia jak dotad"
                                          : _shelfHooks != 2 ? "NIECZYNNA - latki danych rynku nie weszly" : "NIECZYNNA - wymaga Hist Demand Scaling")
                         + " (" + _mixed.Count + " kategorii; pozostale kategorie - waga = wartosc): " + (parts.Count > 0 ? string.Join("; ", parts.ToArray()) : "brak")
                         + "; niejednoznaczne (kategoria, wartosc) " + ambiguous + (on ? "; wartosc polek przeliczona w " + towns + " miastach" : "") + ".");
            }
            catch (Exception e)
            {
                _shelfWorth.Clear(); _shelfByValue.Clear();   // polowiczne wagi gorsze niz zadne: polka liczona wartoscia jak dotad
                Log.Error("HistoricalPrices.MixedShelf", e);
            }
        }

        /// <summary>Wartosc polki kategorii mieszanych w kazdym miescie od nowa z polki, z waga. Gra przebudowuje dane rynku
        /// w OnSessionLaunched (TradeCampaignBehavior - przed nami, jeszcze bez wag) i co dobe (DailyTickTown -> UpdateStores, tam wagi
        /// dodaje StoresPostfix); tu - zaraz po policzeniu wag, zeby pierwsza doba sesji tez je miala. Inne kategorie nietkniete.</summary>
        private static int FixStores()
        {
            if (Campaign.Current == null) return 0;
            int towns = 0, stumbles = 0;
            var want = new Dictionary<ItemCategory, int>();
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    var shelf = t != null && t.Owner != null ? t.Owner.ItemRoster : null;
                    if (shelf == null || t.MarketData == null) continue;
                    want.Clear();
                    foreach (var m in _mixed) want[m.Cat] = 0;
                    for (int i = 0; i < shelf.Count; i++)
                    {
                        var e = shelf.GetElementCopyAtIndex(i);
                        var it = e.EquipmentElement.Item;
                        var c = it != null && it.ItemCategory != null ? it.GetItemCategory() : null;   // jak TownMarketData.UpdateStores
                        int w;
                        if (c == null || !want.TryGetValue(c, out w)) continue;
                        want[c] = w + e.Amount * ShelfWorth(it);
                    }
                    foreach (var kv in want) AddWorth(t.MarketData, kv.Key, kv.Value - t.MarketData.GetCategoryData(kv.Key).InStoreValue);
                    towns++;
                }
                catch (Exception e) { if (stumbles++ < 1) Log.Error("HistoricalPrices.FixStores (miasto)", e); }
            }
            return towns;
        }

        /// <summary>Dopisuje roznice do wartosci polki kategorii bez zmiany liczby sztuk (dwa publiczne wywolania gry: +1 sztuka
        /// o wartosci delta, -1 sztuka o wartosci 0).</summary>
        private static void AddWorth(TownMarketData md, ItemCategory cat, int delta)
        {
            if (delta == 0 || md == null || cat == null) return;
            md.AddNumberInStore(cat, 1, delta);
            md.AddNumberInStore(cat, -1, 0);
        }

        private static bool InTown(TownMarketData md)
        {
            if (_marketTown == null) _marketTown = AccessTools.FieldRefAccess<TownMarketData, Town>("_town");
            var t = _marketTown(md);
            return t != null && t.IsTown;
        }

        /// <summary>Postfiks TownMarketData.OnTownInventoryUpdated(sztuka, liczba): gra dopisala liczba x Value, my roznice do wagi.</summary>
        public static void StorePostfix(TownMarketData __instance, ItemRosterElement __0, int __1)
        {
            if (_shelfWorth.Count == 0) return;
            try
            {
                var it = __0.EquipmentElement.Item;
                int w;
                if (it == null || __1 == 0 || !_shelfWorth.TryGetValue(it, out w) || !InTown(__instance)) return;
                AddWorth(__instance, it.GetItemCategory(), __1 * (w - it.Value));
            }
            catch (Exception e)
            {
                _stShelf++;
                if (!_errShelf) { _errShelf = true; Log.Error("HistoricalPrices.StorePostfix", e); }   // raz na kampanie; nastepna przebudowa polki (doba) wyrowna
            }
        }

        /// <summary>Postfiks TownMarketData.UpdateStores (gra: co dobe i na starcie sesji przebudowuje polke z Value): roznice do wag.</summary>
        public static void StoresPostfix(TownMarketData __instance)
        {
            if (_shelfWorth.Count == 0) return;
            try
            {
                if (!InTown(__instance)) return;
                var shelf = _marketTown(__instance).Owner != null ? _marketTown(__instance).Owner.ItemRoster : null;
                if (shelf == null) return;
                Dictionary<ItemCategory, int> delta = null;
                for (int i = 0; i < shelf.Count; i++)
                {
                    var e = shelf.GetElementCopyAtIndex(i);
                    var it = e.EquipmentElement.Item;
                    int w;
                    if (it == null || it.ItemCategory == null || !_shelfWorth.TryGetValue(it, out w)) continue;
                    var c = it.GetItemCategory();
                    if (delta == null) delta = new Dictionary<ItemCategory, int>();
                    int d; delta.TryGetValue(c, out d); delta[c] = d + e.Amount * (w - it.Value);
                }
                if (delta != null) foreach (var kv in delta) AddWorth(__instance, kv.Key, kv.Value);
            }
            catch (Exception e)
            {
                _stShelf++;
                if (!_errShelf) { _errShelf = true; Log.Error("HistoricalPrices.StoresPostfix", e); }
            }
        }

        /// <summary>Potkniecia wag polki od poprzedniej linii dnia (linia "Ceny surowcow").</summary>
        internal static int TakeShelfStumbles() { int n = _stShelf; _stShelf = 0; return n; }

        // ------------------------------------------------------------ popyt miast w nowej monecie
        // Test 04.10: polki broni i zbroi opustoszaly w 4 dni (zbroje 88 -> 11, bron jednoreczna 341 -> 16). Miasto liczy
        // popyt w ZLOCIE (DefaultSettlementEconomyModel.GetDailyDemandForCategory = BaseDemand x dobrobyt) i zjada
        // budzet / cena sztuk (ItemConsumptionBehavior.MakeConsumption) - przy cenach 40x nizszych mieszczanie kupowali
        // 40x wiecej mieczy; a wspolczynnik ceny (popyt / podaz w zlocie) szedl pod sufit, wiec ruda i drewno staly na
        // indeksie 1.5. Dzielimy popyt kategorii przez to, ile razy potanialy jej przedmioty - rynek liczy te same sztuki.
        private static readonly Dictionary<ItemCategory, float> _catRatio = new Dictionary<ItemCategory, float>();
        [ThreadStatic] private static int _demandDepth;

        // dla ceny surowcow (RawPrice): czy przeliczenie tej sesji juz obowiazuje i ile starych denarow to jeden nowy w danej
        // kategorii (0 = kategoria nieprzeliczona); slownik jest pisany tylko w Apply, potem sam odczyt
        internal static bool Applied { get { return _applied; } }
        internal static float CoinRatio(ItemCategory cat)
        {
            float r;
            return _applied && cat != null && _catRatio.TryGetValue(cat, out r) && r > 0f ? r : 0f;
        }
        internal static List<ItemCategory> RepricedCategories() { return new List<ItemCategory>(_catRatio.Keys); }
        /// <summary>Czesc popytu gry, ktora mieszczanie naprawde kupuja (ten sam mnoznik, ktorym BudgetPostfix tnie ich budzet).</summary>
        internal static float HouseShare(ItemCategory cat)
        {
            var s = Settings.Current;
            return s != null && s.TownHouseholdUse && cat != null ? TownUse(cat.StringId) : 1f;
        }

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

        // ------------------------------------------------------------ zakupy mieszczan (wpis 53, docs/BILANS-ZUZYCIA.md)
        // BK EconomyPatches.CalculateBudget: budzet wiersza polki = popyt x indeks^a + DOBROBYT/1000/indeks - drugi skladnik
        // jest w ZLOCIE i nie przechodzi przez DemandPostfix: przy rudzie i drewnie po kilka pensow zjadal ~465 ladunkow dziennie.
        // (1) ten skladnik dzielimy przez przelicznik kategorii (jak popyt, wpis 44); (2) caly budzet surowcow mnozymy przez
        // domowa czesc (TownUse*) - len, welna, skory surowe i ruda szly do tkaczy, garbarzy i kowali, nie do domow.
        public static void BudgetPostfix(Town town, ItemCategory category, ref float __result)
        {
            try
            {
                if (!_applied || town == null || category == null) return;
                var s = Settings.Current;
                float idx = Math.Max(0.01f, town.GetItemCategoryPriceIndex(category));
                float extra = town.Prosperity / 1000f / idx;
                float r;
                if (s.HistDemandScaling && _catRatio.TryGetValue(category, out r) && r > 1f)
                    __result = __result - extra + extra / r;
                if (s.TownHouseholdUse) __result *= TownUse(category.StringId);
            }
            catch { }
        }

        private static float TownUse(string id)
        {
            var s = Settings.Current;
            switch (id)
            {
                case "flax": return Math.Max(0f, s.TownUseFlax);
                case "wool": return Math.Max(0f, s.TownUseWool);
                case "hides": return Math.Max(0f, s.TownUseHides);
                case "iron": return Math.Max(0f, s.TownUseIron);
                case "leather": return Math.Max(0f, s.TownUseLeather);
                case "linen": return Math.Max(0f, s.TownUseLinen);
                case "hardwood": return Math.Max(0f, s.TownUseHardwood);
                default: return 1f;
            }
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
                var vs = AccessTools.PropertySetter(typeof(ItemObject), "Value");
                if (vs != null) h.Patch(vs, prefix: new HarmonyMethod(typeof(HistoricalPrices), nameof(ValueSetPrefix)) { priority = Priority.First });
                // wpis 75: winowajca 7 lukow i strzal - ROT.CampaignBehaviors.ROTRBMCompatibility.ModifyBowsAndArrows (przy RBM, na
                // OnSessionLaunched) wpisuje refleksja wprost do pola <Value>k__BackingField (90000, 100000, 200000, 130000, 150000,
                // 12000, 11000) - z pominieciem settera, wiec blokada go nie widzi. Postfix: zaraz po nim przywracamy nasze ceny
                // (nazwy i statystyki RBM zostaja nietkniete).
                var rot = AccessTools.Method(AccessTools.TypeByName("ROT.CampaignBehaviors.ROTRBMCompatibility"), "ModifyBowsAndArrows");
                if (rot != null) h.Patch(rot, postfix: new HarmonyMethod(typeof(HistoricalPrices), nameof(RotRbmPostfix)));
                Log.Info("HistoricalPrices: ROT-RBM luki (ROTRBMCompatibility.ModifyBowsAndArrows) " + (rot != null ? "przechwycone" : "nie znalezione") + ".");
                Log.Info("HistoricalPrices: blokada cen przeliczonych przedmiotow " + (vs != null ? "wpieta (setter Value)" : "BRAK settera") + "; zapis wprost do pola omija ja - wtedy dzienna kontrola.");
                var bud = AccessTools.Method("BannerKings.Patches.EconomyPatches:CalculateBudget");
                if (bud != null) h.Patch(bud, postfix: new HarmonyMethod(typeof(HistoricalPrices), nameof(BudgetPostfix)));
                Log.Info("HistoricalPrices: zakupy mieszczan (BK CalculateBudget) - " + (bud != null ? "domowa czesc surowcow i dodatek BK w nowej monecie wpiete" : "BRAK BK CalculateBudget") + ".");
                // wartosc z definicji towarow BK (opis przy _defValue): BKItems.InitializeTradeGood(ItemObject, TextObject, string, ItemCategory, int, float, ItemTypeEnum, bool)
                // - prywatna statyczna; definiuje nia BK swoje towary (Initialize) i futro (AdjustPrices). Parametry po pozycji, typy sprawdzane.
                try
                {
                    var bkItems = AccessTools.TypeByName("BannerKings.Managers.Items.BKItems");
                    var def = bkItems != null ? AccessTools.Method(bkItems, "InitializeTradeGood") : null;
                    var dp = def != null ? def.GetParameters() : null;
                    if (def != null && def.IsStatic && dp.Length >= 5 && dp[0].ParameterType == typeof(ItemObject) && dp[3].ParameterType == typeof(ItemCategory) && dp[4].ParameterType == typeof(int))
                    {
                        h.Patch(def, prefix: new HarmonyMethod(typeof(HistoricalPrices), nameof(DefinePrefix)) { priority = Priority.First },
                                     postfix: new HarmonyMethod(typeof(HistoricalPrices), nameof(DefinePostfix)) { priority = Priority.Last });
                        _defineHooks++;
                    }
                    Log.Info("HistoricalPrices: wartosc z definicji towarow BK (BKItems.InitializeTradeGood) - " + (_defineHooks > 0 ? "wpieta (przed latkami innych modow)" : bkItems == null ? "bez BK - nic do wpiecia" : "BRAK metody o oczekiwanych parametrach") + ".");
                }
                catch (Exception e) { Log.Error("HistoricalPrices.ApplyAll (BKItems.InitializeTradeGood)", e); }
                // paczka 121: wagi sztuk na polce w kategoriach mieszanych - dane rynku miasta przy zmianie polki i przy jej przebudowie;
                // obie latki albo zadna (jedna bez drugiej rozjechalaby wartosc polki) - MixedShelf sprawdza _shelfHooks
                try
                {
                    var tmd = typeof(TownMarketData);
                    var upd = AccessTools.Method(tmd, "OnTownInventoryUpdated", new[] { typeof(ItemRosterElement), typeof(int) });
                    var reb = AccessTools.Method(tmd, "UpdateStores", Type.EmptyTypes);
                    if (upd != null && reb != null)
                    {
                        h.Patch(upd, postfix: new HarmonyMethod(typeof(HistoricalPrices), nameof(StorePostfix)));
                        h.Patch(reb, postfix: new HarmonyMethod(typeof(HistoricalPrices), nameof(StoresPostfix)));
                        _shelfHooks = 2;
                    }
                    Log.Info("HistoricalPrices: wagi sztuk na polce (kategorie mieszane) - dane rynku miasta " + (_shelfHooks == 2 ? "wpiete (zmiana polki i przebudowa polki)" : "BRAK " + (upd == null ? "OnTownInventoryUpdated " : "") + (reb == null ? "UpdateStores" : "") + " - wagi nieczynne") + ".");
                }
                catch (Exception e) { Log.Error("HistoricalPrices.ApplyAll (TownMarketData)", e); }
            }
            catch (Exception e) { Log.Error("HistoricalPrices.ApplyAll", e); }
        }
    }
}
