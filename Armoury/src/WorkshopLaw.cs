using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// WARSZTAT JAKO FIRMA (Jeff 04.10: "warsztaty AI robia wyrob z jednej jednostki surowca bez
    /// wzgledu na tier i nikt nie placi - zrob to"; plan zatwierdzony, strata rafinacji 20%).
    ///
    /// Vanilla 1.4.8 (WorkshopsCampaignBehavior.TickOneProductionCycleForNotableWorkshop :750):
    /// uzbrojenie robi glownie ukryty warsztat "artisans" w kazdym miescie (~13 szt./dzien, z tego
    /// ~8 bez surowca), wyzsze tiery biora 1 jednostke rudy/drewna/skory na sztuke bez wzgledu na
    /// wyrob, a dla linii z nie-towarem flaga effectCapital = false - zadnego zlota.
    ///
    /// Podmieniamy cykl TYLKO dla linii, ktorych wszystkie wyjscia to uzbrojenie (nie towar handlowy):
    ///  1. wyrob WYBIERANY (Jeff: "co brakuje i ma najlepsza marze"): ranking dnia wszystkich wyrobow
    ///     linii uzbrojenia warsztatu wedle zysku na roboczodzien przy cenach targu (brak = wyzsza cena);
    ///  2. receptura z ArmsPricing.CostOf (waga, materialy, gatunek metalu, dni pracy x jakosc):
    ///     metal -> ruda: 1 ruda (10 kg) daje 1.5 kg surowki, kazdy stopien gatunku x1.25 (strata 20%);
    ///     wegiel -> drewno: 5 drewna na rude (dymarka) + 0.25 drewna na kg metalu (kuznia);
    ///     skora/len/drewno wedle receptury, len zastepowalny welna; ulamki jako dlug warsztatu;
    ///  3. czas: pula roboczodni warsztatu (WorkshopWorkersArtisans / WorkshopWorkers ludzi dziennie,
    ///     najwyzej 60 dni zapasu) - kolczuga zjada tygodnie, grot strzaly chwile;
    ///  4. pieniadze: warsztat placi miastu za surowce (cena targu) i place (WorkshopWagePerDay za
    ///     roboczodzien - zostaja w miescie), miasto placi warsztatowi cene wyrobu z targu;
    ///  5. produkuje TYLKO z zyskiem >= WorkshopMinProfitPercent - zawalona polka obniza cene i hamuje
    ///     produkcje, wojna i braki ja nakrecaja, droga ruda zatrzymuje drogie zbroje.
    /// Linie z towarami (mieso, narzedzia, wino...) i warsztaty GRACZA zostaja vanilla.
    /// </summary>
    internal static class WorkshopLaw
    {
        private static ItemObject _ore, _wood, _leather, _linen, _wool;
        private static readonly Dictionary<Workshop, float[]> _owed = new Dictionary<Workshop, float[]>();     // ruda, drewno, skora, len
        private static readonly Dictionary<Workshop, KeyValuePair<float, int>> _labor = new Dictionary<Workshop, KeyValuePair<float, int>>();
        private static int _dayStamp = -1, _made, _skipLoss, _skipMat, _skipLabor, _skipGold;
        private static long _dayRevenue, _dayCost;
        private static readonly Dictionary<ItemObject.ItemTypeEnum, int> _madeByType = new Dictionary<ItemObject.ItemTypeEnum, int>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.WorkshopLawEnabled; } }

        private static bool AllOutputsArms(WorkshopType.Production p)
        {
            if (p.Outputs == null || p.Outputs.Count == 0) return false;   // Production to struct
            foreach (var o in p.Outputs) if (o.Item1 == null || o.Item1.IsTradeGood) return false;
            return true;
        }

        private static void Resolve()
        {
            if (_ore != null) return;
            _ore = MBObjectManager.Instance.GetObject<ItemObject>("iron");
            _wood = MBObjectManager.Instance.GetObject<ItemObject>("hardwood");
            _leather = MBObjectManager.Instance.GetObject<ItemObject>("leather");
            _linen = MBObjectManager.Instance.GetObject<ItemObject>("linen");
            _wool = MBObjectManager.Instance.GetObject<ItemObject>("wool");
        }

        private static int StepsOf(CraftingMaterials g)
        {
            switch (g)
            {
                case CraftingMaterials.Iron1: return 0;
                case CraftingMaterials.Iron2: return 1;
                case CraftingMaterials.Iron3: return 2;
                case CraftingMaterials.Iron4: return 3;
                case CraftingMaterials.Iron5: return 4;
                case CraftingMaterials.Iron6: return 5;
                default: return 1;
            }
        }

        /// <summary>Potrzeby surowcow na sztuke: ruda, drewno, skora, len (jednostki rynku), roboczodni.</summary>
        internal static float[] Needs(ItemObject it, out float days)
        {
            days = 0f;
            var c = ArmsPricing.CostOf(it);
            if (c == null) return null;
            var s = Settings.Current;
            float crudePerOre = Math.Max(0.1f, s.WorkshopCrudeKgPerOre);
            float ore = c.MetalKg > 0f ? c.MetalKg / crudePerOre * (float)Math.Pow(1.25, StepsOf(c.Grade)) : 0f;
            float wood = c.WoodKg / 10f + ore * Math.Max(0f, s.WorkshopWoodPerOre) + c.MetalKg * 0.25f;
            days = Math.Max(0.05f, c.Days);
            return new[] { ore, wood, c.LeatherKg / 10f, c.LinenKg / 10f };
        }

        private static int Available(ItemRoster r, ItemObject it)
        {
            return it != null && r != null ? r.GetItemNumber(it) : 0;
        }

        public static bool CyclePrefix(WorkshopsCampaignBehavior __instance, WorkshopType.Production production, Workshop workshop, ref bool __result)
        {
            try
            {
                if (!On || !AllOutputsArms(production) || workshop == null || workshop.Settlement == null) return true;
                if (workshop.Owner == Hero.MainHero) return true;          // warsztaty gracza - vanilla
                if (!Campaign.Current.GameStarted) return true;           // start gry - vanilla zapelnia rynki
                var town = workshop.Settlement.Town;
                if (town == null) return true;
                Resolve();
                var s = Settings.Current;
                int day = (int)CampaignTime.Now.ToDays;
                if (_dayStamp != day) { Flush(); _dayStamp = day; }

                // pula roboczodni
                float workers = workshop.WorkshopType.IsHidden ? s.WorkshopWorkersArtisans : s.WorkshopWorkers;
                KeyValuePair<float, int> lab;
                float pool = 0f;
                if (_labor.TryGetValue(workshop, out lab)) pool = lab.Key + workers * Math.Max(0, day - lab.Value);
                else pool = workers;
                pool = Math.Min(pool, workers * 60f);

                float[] owed;
                if (!_owed.TryGetValue(workshop, out owed)) { owed = new float[4]; _owed[workshop] = owed; }
                var shelf = town.Owner.ItemRoster;
                bool any = false;
                // WYBOR WYROBU (Jeff 04.10: "powinien wybierac to, czego brakuje, co najbardziej potrzebne
                // i ma najlepsza cene, bo najwiecej zarobi - nie robisz kolczugi, kiedy brakuje lukow i masz
                // wieksza marze"): ranking raz dziennie wszystkiego, co warsztat umie zrobic (wszystkie jego
                // linie uzbrojenia), wedle zysku na roboczodzien przy dzisiejszych cenach targu - brak na
                // polce = wyzsza cena = wyzej w rankingu; cykl bierze pierwsza pozycje, na ktora sa surowce,
                // rece i zloto, z cena przeliczona na nowo (po kazdej sztuce jej cena spada)
                var cands = Candidates(__instance, workshop, town, day);
                int toMake = 0;
                foreach (var output in production.Outputs) toMake += Math.Max(1, output.Item2);
                for (int n = 0; n < toMake; n++)
                {
                    bool done = false; int reason = 0;   // 1 zysk, 2 surowiec, 3 rece, 4 zloto
                    foreach (var it in cands)
                    {
                        float days;
                        var need = Needs(it, out days);
                        if (need == null) continue;
                        if (pool < days) { reason = Math.Max(reason, 3); continue; }
                        var take = new int[4];
                        var mats = new[] { _ore, _wood, _leather, _linen };
                        bool ok = true; int matCost = 0;
                        for (int m = 0; m < 4; m++)
                        {
                            float want = owed[m] + need[m];
                            take[m] = (int)Math.Floor(want);
                            if (take[m] <= 0) continue;
                            var mi = mats[m];
                            int have = Available(shelf, mi);
                            if (m == 3 && have < take[m] && _wool != null) { mi = _wool; have = Available(shelf, mi); mats[m] = mi; }   // welna za len
                            if (mi == null || have < take[m]) { ok = false; break; }
                            matCost += town.GetItemPrice(mi, null, false) * take[m];
                        }
                        if (!ok) { reason = Math.Max(reason, 2); continue; }
                        int wages = (int)Math.Round(days * Math.Max(0f, s.WorkshopWagePerDay));
                        int cost = matCost + wages;
                        var plain = new EquipmentElement(it, null, null, false);
                        int revenue = town.GetItemPrice(plain, null, true);
                        if (revenue < cost * (1f + Math.Max(0f, s.WorkshopMinProfitPercent) / 100f)) { reason = Math.Max(reason, 1); continue; }
                        if (workshop.Capital < cost || town.Gold < revenue) { reason = Math.Max(reason, 4); continue; }
                        // zatwierdzenie: surowce z targu, place, wyrob na targ, zloto w obie strony
                        for (int m = 0; m < 4; m++)
                        {
                            if (take[m] > 0 && mats[m] != null) shelf.AddToCounts(mats[m], -take[m]);
                            owed[m] = owed[m] + need[m] - take[m];
                        }
                        ItemModifier mod = null;
                        try { var g = it.ItemComponent != null ? it.ItemComponent.ItemModifierGroup : null; if (g != null) mod = g.GetRandomItemModifierProductionScoreBased(); } catch { }
                        workshop.ChangeGold(-cost);
                        town.ChangeGold(cost);                 // surowce kupione od miasta, place wydane w miescie
                        town.ChangeGold(-revenue);
                        workshop.ChangeGold(revenue);
                        shelf.AddToCounts(new EquipmentElement(it, mod, null, false), 1);
                        CampaignEventDispatcher.Instance.OnItemProduced(it, workshop.Settlement, 1);
                        pool -= days;
                        any = true; done = true;
                        _made++; _dayRevenue += revenue; _dayCost += cost;
                        int k; _madeByType.TryGetValue(it.ItemType, out k); _madeByType[it.ItemType] = k + 1;
                        break;
                    }
                    if (!done)
                    {
                        if (reason == 1) _skipLoss++; else if (reason == 2) _skipMat++; else if (reason == 3) _skipLabor++; else if (reason == 4) _skipGold++;
                        break;
                    }
                }
                _labor[workshop] = new KeyValuePair<float, int>(pool, day);
                __result = any;
                return false;
            }
            catch (Exception e) { Log.Error("WorkshopLaw.Cycle", e); return true; }
        }

        private static FieldInfo _itemsInCategory;
        private static readonly Dictionary<Workshop, KeyValuePair<int, List<ItemObject>>> _rank = new Dictionary<Workshop, KeyValuePair<int, List<ItemObject>>>();

        /// <summary>Ranking dnia: wszystko z linii uzbrojenia warsztatu (kultura miasta albo neutralne,
        /// a gdy takich brak - wszystko), wedle szacunku zysku na roboczodzien przy dzisiejszych cenach.</summary>
        private static List<ItemObject> Candidates(WorkshopsCampaignBehavior beh, Workshop workshop, Town town, int day)
        {
            KeyValuePair<int, List<ItemObject>> cached;
            if (_rank.TryGetValue(workshop, out cached) && cached.Key == day) return cached.Value;
            var list = new List<ItemObject>();
            try
            {
                if (_itemsInCategory == null) _itemsInCategory = AccessTools.Field(typeof(WorkshopsCampaignBehavior), "_itemsInCategory");
                var dict = _itemsInCategory != null ? _itemsInCategory.GetValue(beh) as Dictionary<ItemCategory, List<ItemObject>> : null;
                var pool = new List<ItemObject>(); var foreign = new List<ItemObject>();
                var seen = new HashSet<ItemObject>();
                if (dict != null)
                    foreach (var p in workshop.WorkshopType.Productions)
                    {
                        if (!AllOutputsArms(p)) continue;
                        foreach (var o in p.Outputs)
                        {
                            List<ItemObject> items;
                            if (o.Item1 == null || !dict.TryGetValue(o.Item1, out items)) continue;
                            foreach (var it in items)
                            {
                                if (it == null || !seen.Add(it) || ArmsPricing.IsUnique(it)) continue;
                                bool local = it.Culture == null || it.Culture.StringId == "neutral_culture" || it.Culture == town.Culture;
                                (local ? pool : foreign).Add(it);
                            }
                        }
                    }
                if (pool.Count == 0) pool = foreign;
                // najwyzej WorkshopCandidates sztuk do rankingu (losowa probka, zeby dzien nie stal)
                int max = Math.Max(5, Settings.Current.WorkshopCandidates);
                while (pool.Count > max) pool.RemoveAt(MBRandom.RandomInt(pool.Count));
                var scored = new List<KeyValuePair<float, ItemObject>>();
                var s = Settings.Current;
                Resolve();
                int pOre = _ore != null ? town.GetItemPrice(_ore, null, false) : 50;
                int pWood = _wood != null ? town.GetItemPrice(_wood, null, false) : 25;
                int pLea = _leather != null ? town.GetItemPrice(_leather, null, false) : 230;
                int pLin = _linen != null ? town.GetItemPrice(_linen, null, false) : 245;
                foreach (var it in pool)
                {
                    float days;
                    var need = Needs(it, out days);
                    if (need == null) continue;
                    float cost = need[0] * pOre + need[1] * pWood + need[2] * pLea + need[3] * pLin + days * s.WorkshopWagePerDay;
                    int revenue = town.GetItemPrice(new EquipmentElement(it, null, null, false), null, true);
                    float perDay = (revenue - cost) / Math.Max(0.1f, days);
                    if (perDay > 0f) scored.Add(new KeyValuePair<float, ItemObject>(perDay, it));
                }
                scored.Sort((a, b) => b.Key.CompareTo(a.Key));
                foreach (var kv in scored) list.Add(kv.Value);
            }
            catch (Exception e) { Log.Error("WorkshopLaw.Candidates", e); }
            _rank[workshop] = new KeyValuePair<int, List<ItemObject>>(day, list);
            return list;
        }

        private static void Flush()
        {
            if (_dayStamp < 0) return;
            if (_made > 0 || _skipLoss + _skipMat + _skipLabor + _skipGold > 0)
            {
                var parts = new List<string>();
                foreach (var kv in _madeByType) parts.Add(kv.Key + " " + kv.Value);
                Log.Info("Warsztaty: dzien " + _dayStamp + " - wykonano " + _made + " szt. [" + string.Join(", ", parts.ToArray())
                         + "], koszt " + _dayCost + ", sprzedaz " + _dayRevenue + "; odpuszczone: bez zysku " + _skipLoss
                         + ", brak surowca " + _skipMat + ", brak rak " + _skipLabor + ", brak zlota " + _skipGold + ".");
            }
            _made = _skipLoss = _skipMat = _skipLabor = _skipGold = 0; _dayRevenue = _dayCost = 0; _madeByType.Clear();
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(WorkshopsCampaignBehavior), "TickOneProductionCycleForNotableWorkshop");
                if (m == null) { Log.Info("WorkshopLaw: brak TickOneProductionCycleForNotableWorkshop - warsztaty vanilla."); return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(WorkshopLaw), nameof(CyclePrefix)));
                Log.Info("WorkshopLaw: warsztaty uzbrojenia jako firmy " + (On ? "CZYNNE" : "wylaczone w MCM") + ".");
            }
            catch (Exception e) { Log.Error("WorkshopLaw.ApplyAll", e); }
        }
    }
}
