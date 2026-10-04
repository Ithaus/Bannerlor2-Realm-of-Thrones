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
        /// <summary>Nowa gra/wczytanie: stare przedmioty i pule z poprzedniej kampanii (audyt 04.10 - ryzyko zepsucia save).</summary>
        internal static void Reset() { _ore = _wood = _leather = _linen = _wool = null; _owed.Clear(); _labor.Clear(); _rank.Clear(); _madeByType.Clear(); _dayStamp = -1; _made = _skipLoss = _skipMat = _skipLabor = _skipGold = 0; _dayRevenue = _dayCost = 0; }
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
                // KONIEC SUROWCOW Z NICZEGO (Jeff 04.10, docs/AUDYT-TOWARY.md 6.2): ukryty warsztat BK
                // "artisans" w kazdym miescie mial linie BEZ wsadu, ktore robily drewno, rude, skory surowe,
                // mieso, skore i plotno z powietrza. Surowce maja przychodzic ze wsi (wiesniacy, karawany).
                if (FreeRawLine(production, workshop))
                {
                    int d0 = (int)CampaignTime.Now.ToDays;
                    if (_dayStamp != d0) { Flush(); _dayStamp = d0; }
                    _freeRawBlocked++;
                    __result = false;
                    return false;
                }
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
                var cands = Candidates(__instance, workshop, production, town, day);
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
                        _made++; _dayRevenue += revenue; _dayCost += cost; Note(it, revenue, cost);
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
        private static readonly Dictionary<KeyValuePair<Workshop, string>, KeyValuePair<int, List<ItemObject>>> _rank = new Dictionary<KeyValuePair<Workshop, string>, KeyValuePair<int, List<ItemObject>>>();

        /// <summary>Ranking dnia: wszystko z linii uzbrojenia warsztatu (kultura miasta albo neutralne,
        /// a gdy takich brak - wszystko), wedle szacunku zysku na roboczodzien przy dzisiejszych cenach.</summary>
        private static List<ItemObject> Candidates(WorkshopsCampaignBehavior beh, Workshop workshop, WorkshopType.Production production, Town town, int day)
        {
            KeyValuePair<int, List<ItemObject>> cached;
            // CECHY (Jeff 04.10: "zbrojmistrz, platnerz i lucznik to zupelnie inne role - lucznik nie zrobi miecza"):
            // ranking tylko z LINII, ktorej cykl wlasnie biegnie - wczesniej ukryty "artisans" (97 miast, linie na wszystko)
            // wybieral najoplacalniejsza sztuke ze wszystkich linii i robil same luki
            string line = LineKey(production);
            var key = new KeyValuePair<Workshop, string>(workshop, line);
            if (_rank.TryGetValue(key, out cached) && cached.Key == day) return cached.Value;
            var list = new List<ItemObject>();
            try
            {
                if (_itemsInCategory == null) _itemsInCategory = AccessTools.Field(typeof(WorkshopsCampaignBehavior), "_itemsInCategory");
                var dict = _itemsInCategory != null ? _itemsInCategory.GetValue(beh) as Dictionary<ItemCategory, List<ItemObject>> : null;
                var pool = new List<ItemObject>(); var foreign = new List<ItemObject>();
                var seen = new HashSet<ItemObject>();
                if (dict != null)
                    foreach (var p in new[] { production })
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
                if (scored.Count == 0) NoteEmpty(workshop, line, town, pool, pOre, pWood, pLea, pLin);
                scored.Sort((a, b) => b.Key.CompareTo(a.Key));
                foreach (var kv in scored) list.Add(kv.Value);
            }
            catch (Exception e) { Log.Error("WorkshopLaw.Candidates", e); }
            _rank[key] = new KeyValuePair<int, List<ItemObject>>(day, list);
            return list;
        }

        private static string LineKey(WorkshopType.Production p)
        {
            var parts = new List<string>();
            try { foreach (var o in p.Outputs) if (o.Item1 != null) parts.Add(o.Item1.StringId); } catch { }
            return string.Join("+", parts.ToArray());
        }

        private static void Flush()
        {
            if (_dayStamp < 0) return;
            if (_made > 0 || _skipLoss + _skipMat + _skipLabor + _skipGold + _freeRawBlocked + _swappedSmith > 0)
            {
                var parts = new List<string>();
                foreach (var kv in _madeByType) parts.Add(kv.Key + " " + kv.Value);
                Log.Info("Warsztaty: dzien " + _dayStamp + " - wykonano " + _made + " szt. [" + string.Join(", ", parts.ToArray())
                         + "], koszt " + _dayCost + ", sprzedaz " + _dayRevenue + "; odpuszczone: bez zysku " + _skipLoss
                         + ", brak surowca " + _skipMat + ", brak rak " + _skipLabor + ", brak zlota " + _skipGold
                         + " | z niczego zablokowane: cykle rzemieslnikow " + _freeRawBlocked + ", sztabki/wegiel z losowania -> ruda/drewno " + _swappedSmith + ".");
            }
            FlushDiag();
            _made = _skipLoss = _skipMat = _skipLabor = _skipGold = _freeRawBlocked = _swappedSmith = 0; _dayRevenue = _dayCost = 0; _madeByType.Clear();
        }

        // ------------------------------------------------------------ diagnoza (wpis 46, tylko log)
        // Test 14:05 po wpisie 44: dalej same luki po ~6800 zl sztuka, zbrojarze nic. Zanim cokolwiek zmienimy -
        // log: CO warsztaty robia (id, Value, cena sprzedazy, koszt) i DLACZEGO warsztat ma pusty ranking
        // (najlepsza sztuka: cena wobec kosztu surowcow i pracy).
        private static readonly Dictionary<ItemObject, int[]> _diagMade = new Dictionary<ItemObject, int[]>();
        private static readonly Dictionary<string, int> _diagEmpty = new Dictionary<string, int>();
        private static readonly Dictionary<string, string> _diagEmptySample = new Dictionary<string, string>();

        private static void Note(ItemObject it, int revenue, int cost)
        {
            int[] a;
            if (!_diagMade.TryGetValue(it, out a)) { a = new int[3]; _diagMade[it] = a; }
            a[0]++; a[1] += revenue; a[2] += cost;
        }

        private static void NoteEmpty(Workshop workshop, string line, Town town, List<ItemObject> pool, int pOre, int pWood, int pLea, int pLin)
        {
            try
            {
                string wt = workshop.WorkshopType.StringId + "/" + line;
                int n; _diagEmpty.TryGetValue(wt, out n); _diagEmpty[wt] = n + 1;
                if (_diagEmptySample.ContainsKey(wt)) return;
                if (pool.Count == 0) { _diagEmptySample[wt] = town.Name + ": brak kandydatow (pula pusta)"; return; }
                var s = Settings.Current;
                ItemObject best = null; float bestPd = float.MinValue; string bestTxt = "";
                foreach (var it in pool)
                {
                    float days; var need = Needs(it, out days);
                    if (need == null) continue;
                    float cost = need[0] * pOre + need[1] * pWood + need[2] * pLea + need[3] * pLin + days * s.WorkshopWagePerDay;
                    int revenue = town.GetItemPrice(new EquipmentElement(it, null, null, false), null, true);
                    float pd = (revenue - cost) / Math.Max(0.1f, days);
                    if (pd > bestPd)
                    {
                        bestPd = pd; best = it;
                        bestTxt = it.StringId + " (Value " + it.Value + ", cena " + revenue + ") koszt " + (int)cost
                                  + " = ruda " + need[0].ToString("0.0") + "x" + pOre + " drewno " + need[1].ToString("0.0") + "x" + pWood
                                  + " skora " + need[2].ToString("0.0") + "x" + pLea + " len " + need[3].ToString("0.0") + "x" + pLin
                                  + " dni " + days.ToString("0.0") + "x" + s.WorkshopWagePerDay;
                    }
                }
                _diagEmptySample[wt] = town.Name + ": najlepsza " + (best != null ? bestTxt : "brak receptury") + " (pula " + pool.Count + ")";
            }
            catch { }
        }

        private static void FlushDiag()
        {
            try
            {
                if (_diagMade.Count > 0)
                {
                    var l = new List<KeyValuePair<ItemObject, int[]>>(_diagMade);
                    l.Sort((a, b) => b.Value[1].CompareTo(a.Value[1]));
                    var parts = new List<string>();
                    for (int i = 0; i < l.Count && i < 6; i++)
                        parts.Add(l[i].Key.StringId + " x" + l[i].Value[0] + " (Value " + l[i].Key.Value + ", sprzedaz srednio " + (l[i].Value[1] / l[i].Value[0]) + ", koszt " + (l[i].Value[2] / l[i].Value[0]) + ")");
                    Log.Info("Warsztaty (diagnoza): najwiecej utargu - " + string.Join("; ", parts.ToArray()) + ".");
                }
                if (_diagEmpty.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (var kv in _diagEmpty)
                    {
                        string sm; _diagEmptySample.TryGetValue(kv.Key, out sm);
                        parts.Add(kv.Key + " x" + kv.Value + " [" + sm + "]");
                    }
                    Log.Info("Warsztaty (diagnoza): pusty ranking (zadna sztuka nie daje zysku) - " + string.Join(" | ", parts.ToArray()) + ".");
                }
            }
            catch { }
            _diagMade.Clear(); _diagEmpty.Clear(); _diagEmptySample.Clear();
        }

        private static int _freeRawBlocked, _swappedSmith;
        private static readonly HashSet<string> FreeRawCats = new HashSet<string> { "hardwood", "iron", "hides", "meat", "leather", "linen" };

        /// <summary>Linia ukrytego "artisans" bez wsadu, ktora robi surowiec z powietrza.</summary>
        private static bool FreeRawLine(WorkshopType.Production p, Workshop w)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.WorkshopNoFreeRaw || w == null || w.WorkshopType == null) return false;
                if (w.WorkshopType.StringId != "artisans") return false;
                if (p.Inputs != null && p.Inputs.Count > 0) return false;
                if (p.Outputs == null || p.Outputs.Count == 0) return false;
                foreach (var o in p.Outputs) if (o.Item1 == null || !FreeRawCats.Contains(o.Item1.StringId)) return false;
                return true;
            }
            catch { return false; }
        }

        /// <summary>Losowanie wyrobu z kategorii: wegiel (kategoria drewna) i sztabki (kategoria zelaza)
        /// tylko z wytopu - warsztat i kopalnia daja surowiec, nie gotowa stal (w tym valyrianska).</summary>
        public static void RandomItemPostfix(ref EquipmentElement __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.WorkshopNoFreeRaw) return;
                var it = __result.Item;
                if (it == null) return;
                string id = it.StringId ?? "";
                if (id != "charcoal" && !id.StartsWith("ironIngot")) return;
                Resolve();
                var raw = id == "charcoal" ? _wood : _ore;
                if (raw == null) return;
                __result = new EquipmentElement(raw);
                _swappedSmith++;
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(WorkshopsCampaignBehavior), "TickOneProductionCycleForNotableWorkshop");
                if (m == null) { Log.Info("WorkshopLaw: brak TickOneProductionCycleForNotableWorkshop - warsztaty vanilla."); return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(WorkshopLaw), nameof(CyclePrefix)));
                var ri = AccessTools.Method(typeof(WorkshopsCampaignBehavior), "GetRandomItemAux");
                if (ri != null) h.Patch(ri, postfix: new HarmonyMethod(typeof(WorkshopLaw), nameof(RandomItemPostfix)));
                Log.Info("WorkshopLaw: koniec surowcow z niczego - rzemieslnicy bez wsadu " + (Settings.Current != null && Settings.Current.WorkshopNoFreeRaw ? "ZABLOKOWANI" : "wolni (MCM)")
                         + ", losowanie sztabek/wegla " + (ri != null ? "wpiete" : "BRAK GetRandomItemAux") + ".");
                Log.Info("WorkshopLaw: warsztaty uzbrojenia jako firmy " + (On ? "CZYNNE" : "wylaczone w MCM") + ".");
            }
            catch (Exception e) { Log.Error("WorkshopLaw.ApplyAll", e); }
        }
    }
}
