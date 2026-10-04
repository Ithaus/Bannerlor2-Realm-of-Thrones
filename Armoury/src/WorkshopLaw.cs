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
    ///  1. wyrob losowany jak w vanilla (GetRandomItem - kultura miasta, tansze czesciej);
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
        private static MethodInfo _randomItem;
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
                if (_randomItem == null) _randomItem = AccessTools.Method(typeof(WorkshopsCampaignBehavior), "GetRandomItem");
                if (_randomItem == null) return true;
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
                foreach (var output in production.Outputs)
                {
                    for (int n = 0; n < output.Item2; n++)
                    {
                        var el = (EquipmentElement)_randomItem.Invoke(__instance, new object[] { output.Item1, town });
                        var it = el.Item;
                        if (it == null) continue;
                        float days;
                        var need = Needs(it, out days);
                        if (need == null) continue;
                        if (pool < days) { _skipLabor++; continue; }
                        // ile pelnych jednostek trzeba zabrac teraz (z dlugiem ulamkow)
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
                        if (!ok) { _skipMat++; continue; }
                        int wages = (int)Math.Round(days * Math.Max(0f, s.WorkshopWagePerDay));
                        int cost = matCost + wages;
                        int revenue = town.GetItemPrice(el, null, true);
                        if (revenue < cost * (1f + Math.Max(0f, s.WorkshopMinProfitPercent) / 100f)) { _skipLoss++; continue; }
                        if (workshop.Capital < cost || town.Gold < revenue) { _skipGold++; continue; }
                        // zatwierdzenie: surowce z targu, place, wyrob na targ, zloto w obie strony
                        for (int m = 0; m < 4; m++)
                        {
                            if (take[m] > 0 && mats[m] != null) shelf.AddToCounts(mats[m], -take[m]);
                            owed[m] = owed[m] + need[m] - take[m];
                        }
                        workshop.ChangeGold(-cost);
                        town.ChangeGold(cost);                 // surowce kupione od miasta, place wydane w miescie
                        town.ChangeGold(-revenue);
                        workshop.ChangeGold(revenue);
                        shelf.AddToCounts(el, 1);
                        CampaignEventDispatcher.Instance.OnItemProduced(it, workshop.Settlement, 1);
                        pool -= days;
                        any = true;
                        _made++; _dayRevenue += revenue; _dayCost += cost;
                        int k; _madeByType.TryGetValue(it.ItemType, out k); _madeByType[it.ItemType] = k + 1;
                    }
                }
                _labor[workshop] = new KeyValuePair<float, int>(pool, day);
                __result = any;
                return false;
            }
            catch (Exception e) { Log.Error("WorkshopLaw.Cycle", e); return true; }
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
