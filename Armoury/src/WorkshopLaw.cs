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
        internal static void Reset() { _ore = _wood = _leather = _linen = _wool = null; _owed.Clear(); _labor.Clear(); _rank.Clear(); _wip.Clear(); _guildCache.Clear(); _madeByType.Clear(); _dayStamp = -1; _made = _skipLoss = _skipMat = _skipLabor = _skipGold = 0; _dayRevenue = _dayCost = 0; Array.Clear(_skipMatBy, 0, _skipMatBy.Length); }
        private static ItemObject _ore, _wood, _leather, _linen, _wool;
        private static readonly int[] _skipMatBy = new int[4];      // "brak surowca" wedlug surowca: ruda, drewno, skora, len albo welna (tylko licznik)
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

        private static int _tanned, _woven;
        private static bool TanOrWeave(WorkshopType.Production p, Workshop w)
        {
            try
            {
                var s = Settings.Current;
                if (s.ArtisanTanWeavePerCycle <= 0 || w == null || w.Settlement == null || w.Settlement.Town == null) return false;
                string outId = null;
                foreach (var o in p.Outputs) if (o.Item1 != null) { outId = o.Item1.StringId; break; }
                string inId = outId == "leather" ? "hides" : outId == "linen" ? "flax" : null;
                if (inId == null) return false;
                var inIt = MBObjectManager.Instance.GetObject<ItemObject>(inId);
                var outIt = MBObjectManager.Instance.GetObject<ItemObject>(outId);
                var shelf = w.Settlement.Town.Owner.ItemRoster;
                if (inIt == null || outIt == null || shelf == null) return false;
                int n = Math.Min(s.ArtisanTanWeavePerCycle, shelf.GetItemNumber(inIt));
                if (n <= 0) return false;
                shelf.AddToCounts(inIt, -n);
                shelf.AddToCounts(outIt, n);
                CampaignEventDispatcher.Instance.OnItemProduced(outIt, w.Settlement, n);
                if (outId == "leather") _tanned += n; else _woven += n;
                return true;
            }
            catch { return false; }
        }

        private static float UnitKg(ItemObject it) { return it != null && it.Weight > 0.05f ? it.Weight : 10f; }

        // wpis 50: kopalnia BK robi rude bez wsadu (praca gornikow - to nie "z niczego"), ale w starych sztukach po 10 kg;
        // przy ladunku 100 kg puszczamy co HistBulkUnitFactor-ty cykl, zeby kg rudy sie nie zmienily
        private static bool BulkLineSkip(WorkshopType.Production p)
        {
            try
            {
                if (p.Inputs != null && p.Inputs.Count > 0) return false;
                foreach (var o in p.Outputs)
                {
                    if (o.Item1 == null) continue;
                    string id = o.Item1.StringId;
                    if (id != "iron" && id != "hardwood") return false;
                }
                Resolve();
                float scale = HistoricalPrices.BulkScale(_ore);
                return scale > 1.01f && MBRandom.RandomFloat > 1f / scale;
            }
            catch { return false; }
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
            // wpis 50: liczone w KG, potem na jednostki rynku wedle ich wagi (ladunek rudy/drewna 100 kg)
            Resolve();
            float oreKg = c.MetalKg > 0f ? c.MetalKg / crudePerOre * 10f * (float)Math.Pow(1.25, StepsOf(c.Grade)) : 0f;
            float woodKg = c.WoodKg + oreKg * Math.Max(0f, s.WorkshopWoodPerOre) + c.MetalKg * Math.Max(0f, s.WorkshopForgeWoodPerMetalKg);
            float ore = oreKg / UnitKg(_ore);
            float wood = woodKg / UnitKg(_wood);
            days = Math.Max(0.05f, HistoricalPrices.On ? HistoricalPrices.HistDays(it, c) : c.Days);
            return new[] { ore, wood, c.LeatherKg / UnitKg(_leather), c.LinenKg / UnitKg(_linen) };
        }

        private static int Available(ItemRoster r, ItemObject it)
        {
            return it != null && r != null ? r.GetItemNumber(it) : 0;
        }

        /// <summary>Stan polki dla licznika "brak surowca": ruda, drewno, skora, len, welna - liczony raz na przebieg rankingu.</summary>
        private static int[] ShelfHave(ItemRoster shelf)
        {
            return new[] { Available(shelf, _ore), Available(shelf, _wood), Available(shelf, _leather), Available(shelf, _linen), Available(shelf, _wool) };
        }

        /// <summary>Ktorych surowcow brakuje na te sztuke (bit 0 ruda, 1 drewno, 2 skora, 3 len albo welna) - ten sam warunek co
        /// przy rozpoczeciu sztuki, ale sprawdzony dla WSZYSTKICH czterech (petla startu staje na pierwszym braku). Sam odczyt.</summary>
        private static int MissMask(int[] have, float[] owed, float[] need)
        {
            int mask = 0;
            for (int m = 0; m < 4; m++)
            {
                int take = (int)Math.Floor(owed[m] + need[m]);
                if (take <= 0) continue;
                var mi = m == 0 ? _ore : (m == 1 ? _wood : (m == 2 ? _leather : _linen));
                int h = have[m];
                if (m == 3 && h < take && _wool != null) { mi = _wool; h = have[4]; }   // welna za len
                if (mi == null || h < take) mask |= 1 << m;
            }
            return mask;
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
                    // wpis 64 (test 15:54: skora ~50 i plotno ~20 sztuk na swiat po 150-1000 d, a skor surowych 3000 i lnu 700
                    // na polkach - za malo garbarni i tkalni): linie skory i plotna rzemieslnikow miasta nie robia ich z niczego,
                    // tylko garbuja skory surowe i tkaja len z wlasnego targu (1 -> 1)
                    if (TanOrWeave(production, workshop)) { __result = true; return false; }
                    _freeRawBlocked++;
                    __result = false;
                    return false;
                }
                if (BulkLineSkip(production)) { __result = false; return false; }
                if (!On || !AllOutputsArms(production) || workshop == null || workshop.Settlement == null) return true;
                if (workshop.Owner == Hero.MainHero) return true;          // warsztaty gracza - vanilla
                if (!Campaign.Current.GameStarted) return true;           // start gry - vanilla zapelnia rynki
                var town = workshop.Settlement.Town;
                if (town == null) return true;
                Resolve();

                // CECHY Z WLASNYMI REKAMI I ROBOTA W TOKU (wpis 48, Jeff 04.10: "tak" na wariant B).
                // Wczesniej rece byly wspolna pula warsztatu, a linie szly po kolei: ubrania i lekka zbroja zjadaly cala pule,
                // do kolczugi (45 dni) i plyty (36-44) nigdy nie dochodzilo. Teraz: kazdy cech warsztatu (krawiec, platnerz,
                // miecznik, lucznik, tarczownik, siodlarz) dostaje rowna czesc rak, a w cechu kazda czynna linia swoja czesc;
                // linia ma JEDNA sztuke w robocie - surowce kupione na start, praca dochodzi codziennie, gotowa idzie na targ.
                // Rece rzemieslnikow miasta wedle dobrobytu (WorkshopProsperityPerHand), warsztaty notabli - WorkshopWorkers.
                var s = Settings.Current;
                int day = (int)CampaignTime.Now.ToDays;
                if (_dayStamp != day) { Flush(); _dayStamp = day; }
                string line = LineKey(production);
                float share = LineShare(__instance, workshop, production, town, day);
                var key = new KeyValuePair<Workshop, string>(workshop, line);
                Wip w;
                if (!_wip.TryGetValue(key, out w)) { w = new Wip { Day = day, Labor = share }; _wip[key] = w; }
                w.Labor += share * Math.Max(0, day - w.Day);
                w.Day = day;
                w.Labor = Math.Min(w.Labor, share * 60f + (w.Item != null ? w.Days : 0f));   // reka bez roboty nie odklada wiecej niz 2 miesiace

                float[] owed;
                if (!_owed.TryGetValue(workshop, out owed)) { owed = new float[4]; _owed[workshop] = owed; }
                var shelf = town.Owner.ItemRoster;
                bool any = false;
                float wage = Math.Max(0f, s.WorkshopWagePerDay);
                float minProfit = 1f + Math.Max(0f, s.WorkshopMinProfitPercent) / 100f;
                for (int guard = 0; guard < 8; guard++)
                {
                    if (w.Item == null)
                    {
                        // nowa sztuka: pierwsza z rankingu linii (zysk na roboczodzien), na ktora sa surowce i pieniadze
                        int reason = 0; bool started = false;
                        int miss = 0; int[] shelfHave = null;      // licznik "brak surowca" wedlug surowca (tylko log)
                        foreach (var it in Candidates(__instance, workshop, production, town, day))
                        {
                            float days;
                            var need = Needs(it, out days);
                            if (need == null) continue;
                            var take = new int[4];
                            var mats = new[] { _ore, _wood, _leather, _linen };
                            bool ok = true; float matCost = 0f;
                            for (int m = 0; m < 4; m++)
                            {
                                float want = owed[m] + need[m];
                                take[m] = (int)Math.Floor(want);
                                if (take[m] <= 0) continue;
                                var mi = mats[m];
                                int have = Available(shelf, mi);
                                if (m == 3 && have < take[m] && _wool != null) { mi = _wool; have = Available(shelf, mi); mats[m] = mi; }   // welna za len
                                if (mi == null || have < take[m]) { ok = false; break; }
                            }
                            if (!ok)
                            {
                                reason = Math.Max(reason, 2);
                                try { if (shelfHave == null) shelfHave = ShelfHave(shelf); miss |= MissMask(shelfHave, owed, need); } catch { }
                                continue;
                            }
                            // koszt surowcow od zuzycia (ulamki tez), po cenie historycznej / targowej
                            for (int m = 0; m < 4; m++) matCost += need[m] * MatPrice(town, mats[m], m);
                            float revenue = Revenue(town, it);
                            if (revenue < (matCost + days * wage) * minProfit) { reason = Math.Max(reason, 1); continue; }
                            int mc = MBRandom.RoundRandomized(matCost);
                            if (workshop.Capital < mc) { reason = Math.Max(reason, 4); continue; }
                            for (int m = 0; m < 4; m++)
                            {
                                if (take[m] > 0 && mats[m] != null) { shelf.AddToCounts(mats[m], -take[m]); OreLedger.NoteWorkshop(mats[m], take[m]); }
                                owed[m] = owed[m] + need[m] - take[m];
                            }
                            workshop.ChangeGold(-mc);
                            town.ChangeGold(mc);                   // surowce kupione od miasta
                            MoneyLedger.Note(MoneyLedger.NShop, workshop.Settlement, mc);       // ksiega przeplywow osad (tylko licznik)
                            w.Item = it; w.Days = days; w.MatCost = mc;
                            _started++;
                            started = true;
                            break;
                        }
                        if (!started)
                        {
                            if (reason == 1) _skipLoss++;
                            else if (reason == 2) { _skipMat++; for (int m = 0; m < 4; m++) if ((miss & (1 << m)) != 0) _skipMatBy[m]++; }
                            else if (reason == 4) _skipGold++;
                            break;
                        }
                    }
                    if (w.Labor < w.Days) { _skipLabor++; break; }      // sztuka w robocie
                    int wagesI = MBRandom.RoundRandomized(w.Days * wage);
                    int rev = MBRandom.RoundRandomized(Revenue(town, w.Item));
                    if (town.Gold < rev || workshop.Capital < wagesI) { _skipGold++; break; }   // gotowa czeka na kupca / na place
                    ItemModifier mod = null;
                    try { var g = w.Item.ItemComponent != null ? w.Item.ItemComponent.ItemModifierGroup : null; if (g != null) mod = g.GetRandomItemModifierProductionScoreBased(); } catch { }
                    workshop.ChangeGold(-wagesI);
                    town.ChangeGold(wagesI);                    // place wydane w miescie
                    town.ChangeGold(-rev);
                    workshop.ChangeGold(rev);
                    MoneyLedger.Note(MoneyLedger.NShop, workshop.Settlement, wagesI);   // ksiega przeplywow osad (tylko liczniki)
                    MoneyLedger.Note(MoneyLedger.NShop, workshop.Settlement, -rev);
                    shelf.AddToCounts(new EquipmentElement(w.Item, mod, null, false), 1);
                    CampaignEventDispatcher.Instance.OnItemProduced(w.Item, workshop.Settlement, 1);
                    any = true;
                    int cost = w.MatCost + wagesI;
                    _made++; _dayRevenue += rev; _dayCost += cost; Note(w.Item, rev, cost);
                    int k; _madeByType.TryGetValue(w.Item.ItemType, out k); _madeByType[w.Item.ItemType] = k + 1;
                    w.Labor -= w.Days;
                    w.Item = null; w.Days = 0f; w.MatCost = 0;
                }
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
                                if (it == null || !seen.Add(it) || ArmsPricing.IsUnique(it) || LegendaryLaw.IsLegend(it) || Forbidden(it)) continue;   // Jeff 04.10: zadnych unikatow rodow, klingi valyrianskiej ani legend z warsztatu
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
                float pOre = MatPrice(town, _ore, 0), pWood = MatPrice(town, _wood, 1), pLea = MatPrice(town, _leather, 2), pLin = MatPrice(town, _linen, 3);
                foreach (var it in pool)
                {
                    float days;
                    var need = Needs(it, out days);
                    if (need == null) continue;
                    float cost = need[0] * pOre + need[1] * pWood + need[2] * pLea + need[3] * pLin + days * s.WorkshopWagePerDay;
                    float revenue = Revenue(town, it);
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

        // ------------------------------------------------------------ cechy (wpis 48)
        private sealed class Wip { public ItemObject Item; public float Days, Labor; public int MatCost, Day; }
        private static readonly Dictionary<KeyValuePair<Workshop, string>, Wip> _wip = new Dictionary<KeyValuePair<Workshop, string>, Wip>();
        private static readonly Dictionary<Workshop, KeyValuePair<int, Dictionary<string, int>>> _guildCache = new Dictionary<Workshop, KeyValuePair<int, Dictionary<string, int>>>();
        private static int _started;

        /// <summary>Cech linii wedle kategorii wyrobu.</summary>
        internal static string GuildOf(WorkshopType.Production p)
        {
            try
            {
                foreach (var o in p.Outputs)
                {
                    string id = o.Item1 != null ? o.Item1.StringId : "";
                    if (id == "garment") return "krawiec";
                    if (id.EndsWith("_armor")) return "platnerz";
                    if (id.StartsWith("melee_weapons")) return "miecznik";
                    if (id == "arrows" || id.StartsWith("ranged_weapons")) return "lucznik";
                    if (id.StartsWith("shield")) return "tarczownik";
                    if (id.StartsWith("horse_equipment")) return "siodlarz";
                    return id;
                }
            }
            catch { }
            return "?";
        }

        // wpis 58: test 15:31 - warsztaty robily 80-106 lukow ravens_teeth_longbow, weirwood_bow i giant_bow dziennie
        // (Value 90-200 tys., nie sa NotMerchandise) - przedmioty magiczne i lore nie wychodza z warsztatu miasta
        private static string[] _forbid; private static string _forbidSrc;
        internal static bool Forbidden(ItemObject it)
        {
            var src = Settings.Current.WorkshopForbiddenIds ?? "";
            if (_forbid == null || _forbidSrc != src)
            {
                _forbidSrc = src;
                var l = new List<string>();
                foreach (var p in src.Split(',')) { var t = p.Trim().ToLowerInvariant(); if (t.Length > 0) l.Add(t); }
                _forbid = l.ToArray();
            }
            string id = (it.StringId ?? "").ToLowerInvariant();
            foreach (var f in _forbid) if (id.Contains(f)) return true;
            return false;
        }

        // wpis 59: test 15:31 - cena, jaka miasto placilo warsztatowi (sprzedaz, klient bez partii), wychodzila 4-8% wartosci
        // (miecz 34 -> 2, plyta konska 7861 -> 441; trzymala ja tylko podloga zlomu 5%) - zaden warsztat poza lukami nic nie robil.
        // Vanilla daje sprzedajacemu 60-80%; cos w lancuchu modeli cen (AIInfluence - kod zaciemniony) tnie dalej. Rzemieslnik
        // sprzedawal na targu sam: dostaje cene, jaka placi kupujacy (cena kupna z targu), minus marze kupca (WorkshopSellShare).
        // wpis 68 (Jeff 04.10: "droga 2" - ulamki pensa w rachunkach): gra zna tylko pensy calkowite - drobiazg za 3 d nie
        // tanial ponizej 1 d, a cene lancucha modeli psuly cudze mody. Decyzja warsztatu liczona W ULAMKACH: wartosc x mnoznik
        // NASZEGO prawa podazy i popytu (polka, popyt, zamowienia, oczekiwania wojenne) x udzial rzemieslnika; zaplata
        // miedzy warsztatem a miastem zaokraglana losowo (srednio co do grosza).
        private static float Revenue(Town town, ItemObject it)
        {
            if (town == null || it == null) return 0f;
            float f = 1f;
            try { float d; int sh; if (SupplyDemand.Active) f = SupplyDemand.Factor(town.Settlement, it, false, out d, out sh); } catch { }
            float arms = 1f;   // wpis 88 (audyt pkt 9): drozejaca skora/ruda podnosi cene w sklepie - i zarobek warsztatu
            try { arms = ArmsPricing.Multiplier(town.Settlement, it); } catch { }
            return Math.Max(0.01f, it.Value * f * arms * MBMath.ClampFloat(Settings.Current.WorkshopSellShare, 0.05f, 1f));
        }

        internal static float GuildWeight(string g)
        {
            var s = Settings.Current;
            switch (g)
            {
                case "krawiec": return Math.Max(0f, s.GuildShareTailor);
                case "platnerz": return Math.Max(0f, s.GuildShareArmourer);
                case "miecznik": return Math.Max(0f, s.GuildShareWeaponsmith);
                case "siodlarz": return Math.Max(0f, s.GuildShareSaddler);
                case "lucznik": return Math.Max(0f, s.GuildShareBowyer);
                case "tarczownik": return Math.Max(0f, s.GuildShareShieldwright);
                default: return 0.05f;
            }
        }

        /// <summary>Roboczodni dziennie warsztatu: rzemieslnicy miasta wedle dobrobytu, warsztat notabla stale.</summary>
        // ile ukrytych warsztatow rzemieslnikow ma miasto (zwykle 1) - zeby odjac naprawy raz, nie w kazdym
        private static int ActiveSmithWorkshops(Town town)
        {
            int n = 0;
            try { foreach (var w in town.Workshops) if (w != null && w.WorkshopType != null && w.WorkshopType.IsHidden) n++; } catch { }
            return Math.Max(1, n);
        }

        internal static float TownHands(Town town)
        {
            var s = Settings.Current;
            float per = Math.Max(50f, s.WorkshopProsperityPerHand);
            return MBMath.ClampFloat(town.Prosperity / per, Math.Max(0.1f, s.WorkshopArtisansMin), Math.Max(s.WorkshopArtisansMin, s.WorkshopArtisansMax));
        }

        internal static float Hands(Workshop workshop, Town town)
        {
            var s = Settings.Current;
            if (!workshop.WorkshopType.IsHidden) return Math.Max(0.1f, s.WorkshopWorkers);
            float per = Math.Max(50f, s.WorkshopProsperityPerHand);
            return MBMath.ClampFloat(town.Prosperity / per, Math.Max(0.1f, s.WorkshopArtisansMin), Math.Max(s.WorkshopArtisansMin, s.WorkshopArtisansMax));
        }

        /// <summary>Czesc rak dla linii: rece / liczba czynnych cechow / liczba czynnych linii w cechu (czynna = ma dzis cos oplacalnego).</summary>
        private static float LineShare(WorkshopsCampaignBehavior beh, Workshop workshop, WorkshopType.Production production, Town town, int day)
        {
            KeyValuePair<int, Dictionary<string, int>> c;
            if (!_guildCache.TryGetValue(workshop, out c) || c.Key != day)
            {
                var d = new Dictionary<string, int>();
                foreach (var p in workshop.WorkshopType.Productions)
                {
                    if (!AllOutputsArms(p)) continue;
                    if (Candidates(beh, workshop, p, town, day).Count == 0) continue;
                    string g = GuildOf(p); int n; d.TryGetValue(g, out n); d[g] = n + 1;
                }
                c = new KeyValuePair<int, Dictionary<string, int>>(day, d);
                _guildCache[workshop] = c;
            }
            int lines; string gu = GuildOf(production);
            if (c.Value.Count == 0 || !c.Value.TryGetValue(gu, out lines) || lines <= 0) return 0f;
            // wpis 52: cechy wedle Paryza 1292 (krawcy 30%, platnerze 20%, miecznicy 20%, siodlarze 15%, lucznicy 10%,
            // tarczownicy 5%) - udzial cechu wsrod CZYNNYCH cechow warsztatu (nieczynny oddaje rece pozostalym)
            float sum = 0f; foreach (var g in c.Value.Keys) sum += GuildWeight(g);
            if (sum <= 0f) return 0f;
            float h = Hands(workshop, town) * GuildWeight(gu) / sum;
            // wpis 91: kowale, ktorzy wczoraj naprawiali, nie kuli - mniej rak cechow platnerzy i miecznikow
            if (workshop.WorkshopType.IsHidden && (gu == "platnerz" || gu == "miecznik"))
            {
                float smithW = GuildWeight("platnerz") + GuildWeight("miecznik");
                if (smithW > 0f) h = Math.Max(0f, h - SmithHours.ManDaysYesterday(town) * GuildWeight(gu) / smithW / Math.Max(1, ActiveSmithWorkshops(town)));
            }
            return h / lines;
        }

        /// <summary>Cena jednostki surowca dla warsztatu (m: 0 ruda, 1 drewno, 2 skora, 3 len/welna). Gra zna tylko pensy calkowite:
        /// ruda (0.75 d za 10 kg) i drewno (0.35 d) stoja na 1-2 d, czyli 2-5x historii - wtedy cena historyczna za kg x waga;
        /// skora, len, welna (dziesiatki pensow) - cena targu (brak = drozej).</summary>
        private static float MatPrice(Town town, ItemObject it, int m)
        {
            if (it == null) return 0f;
            var s = Settings.Current;
            if (HistoricalPrices.On && it.Value < 10)
            {
                float perKg = m == 0 ? s.HistIronOrePerKg : m == 1 ? s.HistWoodPerKg : m == 2 ? s.HistLeatherPerKg : s.HistLinenPerKg;
                return perKg * Math.Max(0.1f, it.Weight);
            }
            return town.GetItemPrice(it, null, false);
        }

        private static int InProgress() { int n = 0; foreach (var w in _wip.Values) if (w.Item != null) n++; return n; }

        private static void Flush()
        {
            if (_dayStamp < 0) return;
            if (_made > 0 || _skipLoss + _skipMat + _skipLabor + _skipGold + _freeRawBlocked + _swappedSmith > 0)
            {
                var parts = new List<string>();
                foreach (var kv in _madeByType) parts.Add(kv.Key + " " + kv.Value);
                Log.Info("Warsztaty: dzien " + _dayStamp + " - wykonano " + _made + " szt. [" + string.Join(", ", parts.ToArray())
                         + "], koszt " + _dayCost + ", sprzedaz " + _dayRevenue + "; odpuszczone: bez zysku " + _skipLoss
                         + ", brak surowca " + _skipMat + " [ruda " + _skipMatBy[0] + ", drewno " + _skipMatBy[1] + ", skora " + _skipMatBy[2] + ", len/welna " + _skipMatBy[3]
                         + " - cykl liczony przy kazdym surowcu, ktorego zabraklo na ktoras sztuke z rankingu], w robocie (cykle) " + _skipLabor + ", brak zlota/kupca " + _skipGold + "; rozpoczete sztuki " + _started + ", w toku teraz " + InProgress()
                         + " | rzemieslnicy miasta wygarbowali skor " + _tanned + ", utkali plotna " + _woven + " | z niczego zablokowane: cykle rzemieslnikow " + _freeRawBlocked + ", sztabki/wegiel z losowania -> ruda/drewno " + _swappedSmith + ".");
            }
            FlushDiag();
            _made = _skipLoss = _skipMat = _skipLabor = _skipGold = _freeRawBlocked = _swappedSmith = _started = _tanned = _woven = 0; _dayRevenue = _dayCost = 0; _madeByType.Clear();
            Array.Clear(_skipMatBy, 0, _skipMatBy.Length);
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

        private static void NoteEmpty(Workshop workshop, string line, Town town, List<ItemObject> pool, float pOre, float pWood, float pLea, float pLin)
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
                    float revenue = Revenue(town, it);
                    float pd = (revenue - cost) / Math.Max(0.1f, days);
                    if (pd > bestPd)
                    {
                        bestPd = pd; best = it;
                        bestTxt = it.StringId + " (Value " + it.Value + ", cena " + revenue + ") koszt " + (int)cost
                                  + " = ruda " + need[0].ToString("0.0") + "x" + pOre.ToString("0.##") + " drewno " + need[1].ToString("0.0") + "x" + pWood.ToString("0.##")
                                  + " skora " + need[2].ToString("0.0") + "x" + pLea.ToString("0.##") + " len " + need[3].ToString("0.0") + "x" + pLin.ToString("0.##")
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
