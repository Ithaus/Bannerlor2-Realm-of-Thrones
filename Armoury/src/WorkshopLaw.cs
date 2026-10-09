using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
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
        internal static void Reset() { _pending = null; _ore = _wood = _leather = _linen = _wool = null; _owed.Clear(); _labor.Clear(); _rank.Clear(); _wip.Clear(); _plans.Clear(); _wipBasket.Clear(); NewDay174(); _soldierItems = null; _madeByType.Clear(); _dayStamp = -1; _made = _skipLoss = _skipMat = _skipLabor = _skipGold = 0; _dayRevenue = _dayCost = 0; Array.Clear(_skipMatBy, 0, _skipMatBy.Length);
            WorkshopTrade.Reset();   // warsztaty towarowe w nowej monecie: stan czyszczony razem z warsztatami zbrojnymi (ta metoda idzie z konstruktora ArmouryBehavior)
            TownCrafts.Reset();      // paczka 148: rzemioslo miasta - dlugi wsadu i rak, srednie zuzycia (przed SyncData wczytania)
            TownFletchers.Reset();   // paczka 172: strzelarze miasta - dlugi surowca i rak, kandydaci, liczniki (przed SyncData wczytania)
        }
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
                // paczka 148: rzemioslo miasta (TownCrafts) przerabia skory, len i welne wedle wartosci w dobie miasta - tu 1:1 juz nie;
                // linia zostaje zablokowana jak reszta "z niczego" (_freeRawBlocked)
                if (TownCrafts.Active) return false;
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

        internal static int StepsOf(CraftingMaterials g)   // internal: MendMaterial (naprawa u kwatermistrza) liczy metal tym samym przelicznikiem gatunku
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
        /// <summary>
        /// Placa za jeden roboczodzien przy tej sztuce w tym miescie (pensy). Jeff 07.10 ("wszystko, co dotyczy placenia, musi byc
        /// spojne"): te same dni (HistDays) i ta sama dniowka mistrza wedle tieru (HistoricalPrices.DayWageOf), z ktorych liczy sie
        /// wartosc sztuki - dotad warsztat placil 3 d za dzien nad plyta t6 wycenionym po 10.5 d i roznice bral jako zysk - razy poziom
        /// plac miasta (TownWage). Przy wylaczonym WorkshopWageByTier albo bez cen historycznych - WorkshopWagePerDay jak dotad.
        /// </summary>
        internal static float DayWage(ItemObject it, Town town)
        {
            var s = Settings.Current;
            if (s == null || !s.WorkshopWageByTier) return Math.Max(0f, s != null ? s.WorkshopWagePerDay : 3f);
            float w = HistoricalPrices.On && it != null ? HistoricalPrices.DayWageOf(it) : Math.Max(0f, s.WorkshopWagePerDay);
            return w * TownWage.Index(town);
        }

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
                // paczka 172: linia "arrows" (strzaly i belty) - BK robi z niej towar handlowy (BKItemCategories :122), wiec szla droga gry
                // BEZ wsadu (artisans x4, fletcher x1) i z mnoznikiem BK - snopy z niczego. Przy czynnych strzelarzach miasta (TownFletchers)
                // zamknieta w warsztatach notabli: jedna droga amunicji - z rudy i drewna. Warsztat gracza tu nie przychodzi (ForPlayerWorkshop).
                if (TownFletchers.ClosesLine(production, workshop)) { TownFletchers.NoteClosed(production, workshop); __result = false; return false; }
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
                float floor;
                float share = LineShare(__instance, workshop, production, town, day, out floor);
                var key = new KeyValuePair<Workshop, string>(workshop, line);
                Wip w;
                if (!_wip.TryGetValue(key, out w)) { w = new Wip { Day = day, Labor = share }; _wip[key] = w; }
                w.Labor += share * Math.Max(0, day - w.Day);
                w.Day = day;
                float planDays = PlanDays;   // 174.1: bank roboty i plan (E) z jednego klucza WorkshopPlanDays (domyslnie 60 - jak dotad)
                w.Labor = Math.Min(w.Labor, share * planDays + (w.Item != null ? w.Days : 0f));   // reka bez roboty nie odklada wiecej niz plan (2 miesiace)

                float[] owed;
                if (!_owed.TryGetValue(workshop, out owed)) { owed = new float[4]; _owed[workshop] = owed; }
                var shelf = town.Owner.ItemRoster;
                bool any = false;
                float minProfit = 1f + Math.Max(0f, s.WorkshopMinProfitPercent) / 100f;
                bool planOn = FollowMaterial;
                for (int guard = 0; guard < 8; guard++)
                {
                    if (w.Item == null)
                    {
                        // nowa sztuka: pierwsza z rankingu linii, na ktora sa surowce i pieniadze (174.1: ranking wedlug braku koszyka, potem zysku
                        // na roboczodzien - WorkshopChooseByShortage); bramka zysku, surowiec i kapital - jak dotad, po prawdziwej cenie polki
                        int reason = 0; bool started = false, planRej = false;
                        int miss = 0; int[] shelfHave = null;      // licznik "brak surowca" wedlug surowca (tylko log)
                        float planCap = w.Labor + Math.Max(share, floor) * planDays;
                        var cands = Candidates(__instance, workshop, production, town, day);
                        for (int ci = 0; ci < cands.Count && !started; ci++)
                        {
                            var c = cands[ci];
                            Start st;
                            int r = TryStart(c, shelf, owed, workshop, town, minProfit, out st, ref miss, ref shelfHave);
                            if (r != 0) { reason = Math.Max(reason, r); continue; }
                            // (E) 174.1: nie zaczynaj sztuki, ktorej nie skonczysz w planie - podloga: rowny podzial rak cechu (linia ze sztuka w robocie ja ma)
                            if (planOn && c.Days > planCap) { _rejPlan++; planRej = true; continue; }
                            // (C) 174.1: w koszyku z brakiem (tier <= WorkshopMunitionMaxTier) czesc rozpoczetych sztuk to najszybsza w robocie ("na amunicje")
                            bool mun = false;
                            if (MunitionOn && c.Short > 0f && c.Basket % 10 <= MunitionMaxTier)
                            {
                                w.MunAcc += MunitionShare;
                                if (w.MunAcc >= 1f)
                                {
                                    w.MunAcc -= 1f;
                                    Cand qc; Start qs;
                                    if (Quickest(cands, c, shelf, owed, workshop, town, minProfit, planOn, planCap, out qc, out qs)) { c = qc; st = qs; }
                                    mun = true;
                                }
                            }
                            DoStart(c, st, shelf, owed, workshop, town, w, mun);
                            started = true;
                        }
                        if (!started)
                        {
                            if (reason == 1) _skipLoss++;
                            else if (reason == 2) { _skipMat++; for (int m = 0; m < 4; m++) if ((miss & (1 << m)) != 0) _skipMatBy[m]++; }
                            else if (reason == 4) _skipGold++;
                            else if (planRej) _skipPlan++;
                            break;
                        }
                    }
                    if (w.Labor < w.Days) { _skipLabor++; break; }      // sztuka w robocie
                    int wagesI = MBRandom.RoundRandomized(w.Days * DayWage(w.Item, town));
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
                    NoteFinished(town, w.Item, w.Days, production);   // 174.1: licznik koszyka w robocie, zrobione t5-6, zuzyte rece cechu, q t1-t3
                    w.Labor -= w.Days;
                    w.Item = null; w.Days = 0f; w.MatCost = 0;
                }
                __result = any;
                return false;
            }
            catch (Exception e) { Log.Error("WorkshopLaw.Cycle", e); return true; }
        }

        // ------------------------------------------------------------ 174.1: start sztuki (wydzielone z petli bez zmiany warunkow)
        private struct Start { public int[] Take; public ItemObject[] Mats; public int Mc; }

        /// <summary>Warunki startu sztuki jak dotad: surowiec na polce (welna za len), zysk >= WorkshopMinProfitPercent po prawdziwej cenie, kapital na surowiec.
        /// 0 = mozna; 1 bez zysku, 2 brak surowca (miss - ktore), 4 brak zlota.</summary>
        private static int TryStart(Cand c, ItemRoster shelf, float[] owed, Workshop workshop, Town town, float minProfit, out Start st, ref int miss, ref int[] shelfHave)
        {
            st = new Start { Take = new int[4], Mats = new[] { _ore, _wood, _leather, _linen } };
            var need = c.Need;
            for (int m = 0; m < 4; m++)
            {
                float want = owed[m] + need[m];
                st.Take[m] = (int)Math.Floor(want);
                if (st.Take[m] <= 0) continue;
                var mi = st.Mats[m];
                int have = Available(shelf, mi);
                if (m == 3 && have < st.Take[m] && _wool != null) { mi = _wool; have = Available(shelf, mi); st.Mats[m] = mi; }   // welna za len
                if (mi == null || have < st.Take[m])
                {
                    try { if (shelfHave == null) shelfHave = ShelfHave(shelf); miss |= MissMask(shelfHave, owed, need); } catch { }
                    return 2;
                }
            }
            // koszt surowcow od zuzycia (ulamki tez), po cenie historycznej / targowej
            float matCost = 0f;
            for (int m = 0; m < 4; m++) matCost += need[m] * MatPrice(town, st.Mats[m], m);
            float revenue = Revenue(town, c.Item);
            if (revenue < (matCost + c.Days * DayWage(c.Item, town)) * minProfit) return 1;
            st.Mc = MBRandom.RoundRandomized(matCost);
            if (workshop.Capital < st.Mc) return 4;
            return 0;
        }

        private static void DoStart(Cand c, Start st, ItemRoster shelf, float[] owed, Workshop workshop, Town town, Wip w, bool munition)
        {
            for (int m = 0; m < 4; m++)
            {
                if (st.Take[m] > 0 && st.Mats[m] != null) { shelf.AddToCounts(st.Mats[m], -st.Take[m]); OreLedger.NoteWorkshop(st.Mats[m], st.Take[m]); }
                owed[m] = owed[m] + c.Need[m] - st.Take[m];
            }
            workshop.ChangeGold(-st.Mc);
            town.ChangeGold(st.Mc);                   // surowce kupione od miasta
            MoneyLedger.Note(MoneyLedger.NShop, workshop.Settlement, st.Mc);       // ksiega przeplywow osad (tylko licznik)
            w.Item = c.Item; w.Days = c.Days; w.MatCost = st.Mc;
            _started++;
            WipBasketAdd(town, c.Basket, +1);
            if (munition) NoteMunition(c.Item);
        }

        /// <summary>(C) najszybsza w robocie sztuka koszyka c0 (remis - zysk na dzien), ktora mozna dzis zaczac i skonczyc w planie.</summary>
        private static bool Quickest(List<Cand> cands, Cand c0, ItemRoster shelf, float[] owed, Workshop workshop, Town town, float minProfit, bool planOn, float planCap, out Cand best, out Start bestSt)
        {
            best = null; bestSt = new Start();
            int miss = 0; int[] have = null;
            foreach (var c in cands)
            {
                if (c.Basket != c0.Basket || c == c0) continue;
                if (best != null && (c.Days > best.Days || (c.Days == best.Days && c.PerDay <= best.PerDay))) continue;
                if (c.Days > c0.Days || (c.Days == c0.Days && c.PerDay <= c0.PerDay)) continue;   // nie szybsza niz wybrana
                if (planOn && c.Days > planCap) continue;
                Start st;
                if (TryStart(c, shelf, owed, workshop, town, minProfit, out st, ref miss, ref have) != 0) continue;
                best = c; bestSt = st;
            }
            return best != null;
        }

        private static FieldInfo _itemsInCategory;
        private static readonly Dictionary<KeyValuePair<Workshop, string>, KeyValuePair<int, List<Cand>>> _rank = new Dictionary<KeyValuePair<Workshop, string>, KeyValuePair<int, List<Cand>>>();

        /// <summary>Pozycja rankingu dnia (174.1): przedmiot, koszyk (typ*10+tier), potrzeby i dni, przychod i koszt z tego samego przebiegu, popyt d i polka s
        /// koszyka w tym miescie (z SupplyDemand.Factor, ktory juz liczy przychod - bez nowych wywolan), brak koszyka i waga braku.</summary>
        private sealed class Cand
        {
            public ItemObject Item; public int Basket; public float[] Need; public float Days, Rev, Cost, MatCost, PerDay, D, Short, W; public int S;
        }

        /// <summary>Ranking dnia: wszystko z linii uzbrojenia warsztatu (kultura miasta albo neutralne, a gdy takich brak - wszystko). Dotad wedlug szacunku
        /// zysku na roboczodzien przy dzisiejszych cenach; 174.1 (WorkshopChooseByShortage, Jeff 04.10 "co brakuje i ma najlepsza marze"): najpierw koszyki
        /// wedlug braku - waga W = mnoznik polki bez sufitu / z sufitem (przy pustej polce i 60 zamowieniach ok. 2.1; sufit x4 nie odroznial "brak 60 zbroi"
        /// od "brak 15 pantofli") razy najlepszy zysk na dzien w koszyku - potem w koszyku zysk na dzien. Przedmioty cywilne bez zolnierza (ladys_shoe i podobne)
        /// poza liniami zbrojnymi. Filtr unikatow, legend i WorkshopForbiddenIds bez zmian.</summary>
        private static List<Cand> Candidates(WorkshopsCampaignBehavior beh, Workshop workshop, WorkshopType.Production production, Town town, int day)
        {
            KeyValuePair<int, List<Cand>> cached;
            // CECHY (Jeff 04.10: "zbrojmistrz, platnerz i lucznik to zupelnie inne role - lucznik nie zrobi miecza"):
            // ranking tylko z LINII, ktorej cykl wlasnie biegnie - wczesniej ukryty "artisans" (97 miast, linie na wszystko)
            // wybieral najoplacalniejsza sztuke ze wszystkich linii i robil same luki
            string line = LineKey(production);
            var key = new KeyValuePair<Workshop, string>(workshop, line);
            if (_rank.TryGetValue(key, out cached) && cached.Key == day) return cached.Value;
            var list = new List<Cand>();
            try
            {
                if (_itemsInCategory == null) _itemsInCategory = AccessTools.Field(typeof(WorkshopsCampaignBehavior), "_itemsInCategory");
                var dict = _itemsInCategory != null ? _itemsInCategory.GetValue(beh) as Dictionary<ItemCategory, List<ItemObject>> : null;
                var pool = new List<ItemObject>(); var foreign = new List<ItemObject>();
                var seen = new HashSet<ItemObject>();
                bool byShort = ChooseByShortage;
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
                                if (byShort && CivilianOnly(it)) { _civSkipped++; continue; }   // 174.1: stroj mieszczan (ladys_shoe) - nie wyrob linii zbrojnej
                                bool local = it.Culture == null || it.Culture.StringId == "neutral_culture" || it.Culture == town.Culture;
                                (local ? pool : foreign).Add(it);
                            }
                        }
                    }
                if (pool.Count == 0) pool = foreign;
                // najwyzej WorkshopCandidates sztuk do rankingu (losowa probka, zeby dzien nie stal)
                int max = Math.Max(5, Settings.Current.WorkshopCandidates);
                while (pool.Count > max) pool.RemoveAt(MBRandom.RandomInt(pool.Count));
                var s = Settings.Current;
                Resolve();
                float pOre = MatPrice(town, _ore, 0), pWood = MatPrice(town, _wood, 1), pLea = MatPrice(town, _leather, 2), pLin = MatPrice(town, _linen, 3);
                float elast = MBMath.ClampFloat(s.SupplyDemandElasticity, 0.05f, 2f), lo = MBMath.ClampFloat(s.SupplyDemandMinFactor, 0.01f, 1f), hi = Math.Max(1f, s.SupplyDemandMaxFactor);
                foreach (var it in pool)
                {
                    float days;
                    var need = Needs(it, out days);
                    if (need == null) continue;
                    float mat = need[0] * pOre + need[1] * pWood + need[2] * pLea + need[3] * pLin;
                    float cost = mat + days * DayWage(it, town);
                    float d; int sh;
                    float revenue = Revenue(town, it, out d, out sh);
                    float perDay = (revenue - cost) / Math.Max(0.1f, days);
                    if (perDay <= 0f) continue;
                    var c = new Cand { Item = it, Basket = (int)it.ItemType * 10 + TierOf(it), Need = need, Days = days, Rev = revenue, Cost = cost, MatCost = mat, PerDay = perDay, D = d, S = sh, W = 1f };
                    // brak koszyka w miescie: popyt - polka - sztuki w robocie; waga braku = mnoznik bez sufitu / z sufitem (>= 1) liczony z sztukami w robocie
                    int wipB = WipBasket(town, c.Basket);
                    c.Short = Math.Max(0f, d - sh - wipB);
                    if (c.Short > 0f)
                    {
                        float fn = (float)Math.Pow((d + 1f) / (sh + wipB + 1f), elast);
                        float fc = MBMath.ClampFloat(fn, lo, hi);
                        c.W = fc > 0f ? Math.Max(1f, fn / fc) : 1f;
                    }
                    NoteBasketDay(town, c);
                    list.Add(c);
                }
                if (list.Count == 0) NoteEmpty(workshop, line, town, pool, pOre, pWood, pLea, pLin);
                if (byShort)
                {
                    // koszyki wedlug W x najlepszy zysk na dzien w koszyku (malejaco), w koszyku - zysk na dzien
                    var bestPd = new Dictionary<int, float>();
                    foreach (var c in list) { float b; if (!bestPd.TryGetValue(c.Basket, out b) || c.PerDay > b) bestPd[c.Basket] = c.PerDay; }
                    list.Sort((x, y) =>
                    {
                        float kx = x.W * bestPd[x.Basket], ky = y.W * bestPd[y.Basket];
                        int r = ky.CompareTo(kx);
                        if (r != 0) return r;
                        r = x.Basket.CompareTo(y.Basket);
                        return r != 0 ? r : y.PerDay.CompareTo(x.PerDay);
                    });
                }
                else list.Sort((x, y) => y.PerDay.CompareTo(x.PerDay));
            }
            catch (Exception e) { Log.Error("WorkshopLaw.Candidates", e); }
            _rank[key] = new KeyValuePair<int, List<Cand>>(day, list);
            return list;
        }

        private static int TierOf(ItemObject it) { try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; } }

        // ------------------------------------------------------------ 174.1: przedmioty cywilne bez zolnierza
        // Civilian="true" i nieobecne w zadnym wzorcu oddzialu (BattleEquipments postaci nie-bohaterow) - lista raz na sesje. ladys_shoe (t1, SandBoxCore)
        // nie wystepuje w zadnym wzorcu ROT: krawcy szyli 110-150 par dziennie jako "buty wojska" (A171). Poza liniami zbrojnymi i poza ColdStart.
        private static HashSet<ItemObject> _soldierItems;
        internal static bool CivilianOnly(ItemObject it)
        {
            if (it == null || !it.IsCivilian) return false;
            if (_soldierItems == null)
            {
                var set = new HashSet<ItemObject>();
                try
                {
                    foreach (var ch in CharacterObject.All)
                    {
                        if (ch == null || ch.IsHero) continue;
                        foreach (var eq in ch.BattleEquipments)
                        {
                            if (eq == null) continue;
                            for (int sl = 0; sl < 12; sl++) { var x = eq[(EquipmentIndex)sl].Item; if (x != null) set.Add(x); }
                        }
                    }
                }
                catch (Exception e) { Log.Error("WorkshopLaw.CivilianOnly", e); }
                if (set.Count == 0) return false;   // postaci jeszcze nie wczytane - nie zgadujemy
                _soldierItems = set;
            }
            return !_soldierItems.Contains(it);
        }

        private static string LineKey(WorkshopType.Production p)
        {
            var parts = new List<string>();
            try { foreach (var o in p.Outputs) if (o.Item1 != null) parts.Add(o.Item1.StringId); } catch { }
            return string.Join("+", parts.ToArray());
        }

        // ------------------------------------------------------------ cechy (wpis 48)
        private sealed class Wip { public ItemObject Item; public float Days, Labor, MunAcc; public int MatCost, Day; }   // MunAcc: 174.1 (C) licznik sztuk "na amunicje" linii
        private static readonly Dictionary<KeyValuePair<Workshop, string>, Wip> _wip = new Dictionary<KeyValuePair<Workshop, string>, Wip>();
        // 174.1: plan rak warsztatu na dobe - udzial i podloga kazdej linii (LineShare)
        private sealed class LinePlan { public string Key, Guild; public bool Wip, Active; public float Short, Share, Floor, Labor; public List<Cand> Rank; }
        private static readonly Dictionary<Workshop, KeyValuePair<int, Dictionary<string, LinePlan>>> _plans = new Dictionary<Workshop, KeyValuePair<int, Dictionary<string, LinePlan>>>();
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
            float d; int sh;
            return Revenue(town, it, out d, out sh);
        }

        /// <summary>174.1: ten sam przychod, a z tego samego wywolania SupplyDemand.Factor popyt d i polka s koszyka (bez prawa podazy: 0 / 0).</summary>
        private static float Revenue(Town town, ItemObject it, out float d, out int sh)
        {
            d = 0f; sh = 0;
            if (town == null || it == null) return 0f;
            float f = 1f;
            try { if (SupplyDemand.Active) f = SupplyDemand.Factor(town.Settlement, it, false, out d, out sh); } catch { }
            float arms = 1f;   // wpis 88 (audyt pkt 9): drozejaca skora/ruda podnosi cene w sklepie - i zarobek warsztatu
            try { arms = ArmsPricing.Multiplier(town.Settlement, it); } catch { }
            return RevenueOf(it, f, arms);
        }

        /// <summary>Paczka 172: wzor przychodu rzemieslnika (wartosc x mnoznik podazy i popytu koszyka x mnoznik wyceny x udzial rzemieslnika) -
        /// jeden dla warsztatow zbrojnych i strzelarzy miasta (TownFletchers liczy Factor raz na koszyk).</summary>
        internal static float RevenueOf(ItemObject it, float factor, float arms)
        {
            if (it == null) return 0f;
            return Math.Max(0.01f, it.Value * factor * arms * MBMath.ClampFloat(Settings.Current.WorkshopSellShare, 0.05f, 1f));
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

        // ------------------------------------------------------------ 174.1: RECE IDA DO SUROWCA, WYROB WEDLUG BRAKU (te same rece, zero nowego surowca)
        internal static bool FollowMaterial { get { var s = Settings.Current; return s != null && s.WorkshopHandsFollowMaterial; } }
        internal static bool FreedByShortage { get { var s = Settings.Current; return s != null && s.WorkshopFreedHandsByShortage; } }
        internal static bool ChooseByShortage { get { var s = Settings.Current; return s != null && s.WorkshopChooseByShortage; } }
        private static bool MunitionOn { get { var s = Settings.Current; return s != null && s.WorkshopMunitionGrade && s.WorkshopMunitionShare > 0f; } }
        private static int MunitionMaxTier { get { var s = Settings.Current; return s == null ? 3 : Math.Max(1, Math.Min(6, s.WorkshopMunitionMaxTier)); } }
        private static float MunitionShare { get { var s = Settings.Current; return s == null ? 0.5f : MBMath.ClampFloat(s.WorkshopMunitionShare, 0f, 1f); } }
        private static float PlanDays { get { var s = Settings.Current; return s == null ? 60f : MBMath.ClampFloat(s.WorkshopPlanDays, 14f, 120f); } }

        /// <summary>
        /// Czesc rak dla linii (roboczodni dziennie). floor = rowny podzial rak cechu miedzy jego czynne linie (podloga planu (E) i linii ze sztuka w robocie).
        /// Dotad: rece / czynne cechy (wagi Paryza 1292) / czynne linie po rowno, czynna = ma cos oplacalnego PO CENACH - kowal bez rudy dostawal swoja czesc
        /// i nic nie robil (624-980 cykli/d "brak rudy" = 25-35% rak swiata). 174.1:
        ///  (A) czynna = sztuka w robocie albo start mozliwy DZIS (surowiec na polce, zysk, kapital) i (E) skonczy w planie (WorkshopHandsFollowMaterial);
        ///      rece cechu bez czynnej linii ida do czynnych cechow wedlug braku ich koszykow (WorkshopFreedHandsByShortage; brak braku - wagi Paryza);
        ///  (D) w cechu: WorkshopLineShortageShare rak wedlug braku linii, reszta po rowno; linia ze sztuka w robocie ma podloge rownego podzialu
        ///      ("dokoncz zaczete"), pozostale skaluja sie tak, zeby suma = rece cechu (kontrola w logu).
        /// Wylaczone klucze = dzisiejszy podzial. Plan liczony raz na warsztat na dobe.
        /// </summary>
        private static float LineShare(WorkshopsCampaignBehavior beh, Workshop workshop, WorkshopType.Production production, Town town, int day, out float floor)
        {
            floor = 0f;
            KeyValuePair<int, Dictionary<string, LinePlan>> c;
            if (!_plans.TryGetValue(workshop, out c) || c.Key != day)
            {
                c = new KeyValuePair<int, Dictionary<string, LinePlan>>(day, BuildPlan(beh, workshop, town, day));
                _plans[workshop] = c;
            }
            LinePlan lp;
            if (!c.Value.TryGetValue(LineKey(production), out lp) || !lp.Active) return 0f;
            floor = lp.Floor;
            return lp.Share;
        }

        private static readonly string[] GuildOrder = { "krawiec", "platnerz", "miecznik", "siodlarz", "lucznik", "tarczownik" };

        private static Dictionary<string, LinePlan> BuildPlan(WorkshopsCampaignBehavior beh, Workshop workshop, Town town, int day)
        {
            var plan = new Dictionary<string, LinePlan>();
            try
            {
                var s = Settings.Current;
                bool follow = FollowMaterial, freedByShort = FreedByShortage;
                float alpha = MBMath.ClampFloat(s.WorkshopLineShortageShare, 0f, 1f);
                float planDays = PlanDays;
                Resolve();
                var shelf = town.Owner != null ? town.Owner.ItemRoster : null;
                float[] owed; if (!_owed.TryGetValue(workshop, out owed)) owed = new float[4];
                int[] have = shelf != null ? ShelfHave(shelf) : new int[5];
                float minProfit = 1f + Math.Max(0f, s.WorkshopMinProfitPercent) / 100f;
                int capital = workshop.Capital;
                foreach (var p in workshop.WorkshopType.Productions)
                {
                    if (!AllOutputsArms(p)) continue;
                    string key = LineKey(p);
                    if (plan.ContainsKey(key)) continue;
                    var lp = new LinePlan { Key = key, Guild = GuildOf(p), Rank = Candidates(beh, workshop, p, town, day) };
                    Wip w;
                    if (_wip.TryGetValue(new KeyValuePair<Workshop, string>(workshop, key), out w) && w != null) { lp.Wip = w.Item != null; lp.Labor = w.Labor; }
                    var seenB = new HashSet<int>();
                    foreach (var cnd in lp.Rank) if (cnd.Short > 0f && seenB.Add(cnd.Basket)) lp.Short += cnd.Short;
                    plan[key] = lp;
                }
                if (plan.Count == 0) return plan;
                // (A) czynna linia: sztuka w robocie albo start mozliwy dzis (pierwsze przejscie bez planu (E))
                Func<LinePlan, float, bool> startable = (lp, cap) =>
                {
                    foreach (var cnd in lp.Rank)
                    {
                        if (cnd.Days > cap) continue;
                        if (MissMask(have, owed, cnd.Need) != 0) continue;
                        if (cnd.Rev < cnd.Cost * minProfit) continue;
                        if (capital < (int)Math.Ceiling(cnd.MatCost)) continue;
                        return true;
                    }
                    return false;
                };
                foreach (var lp in plan.Values) lp.Active = follow ? (lp.Wip || startable(lp, float.MaxValue)) : lp.Rank.Count > 0;
                float H = Hands(workshop, town);
                var hg = new Dictionary<string, float>(); var extraG = new Dictionary<string, float>(); var nG = new Dictionary<string, int>();
                float repairs = 0f;
                for (int round = 0; round < 3; round++)
                {
                    hg.Clear(); extraG.Clear(); nG.Clear(); repairs = 0f;
                    var shortG = new Dictionary<string, float>();
                    float wAll = 0f, wAct = 0f, shortAll = 0f;
                    var guilds = new HashSet<string>();
                    foreach (var lp in plan.Values)
                    {
                        if (guilds.Add(lp.Guild)) wAll += GuildWeight(lp.Guild);
                        if (!lp.Active) continue;
                        int n; nG.TryGetValue(lp.Guild, out n); nG[lp.Guild] = n + 1;
                        float sg; shortG.TryGetValue(lp.Guild, out sg); shortG[lp.Guild] = sg + lp.Short;
                    }
                    foreach (var g in nG.Keys) { wAct += GuildWeight(g); shortAll += shortG[g]; }
                    if (wAct <= 0f || wAll <= 0f) break;
                    float freed = H * Math.Max(0f, wAll - wAct) / wAll;
                    foreach (var g in nG.Keys)
                    {
                        float own = H * GuildWeight(g) / wAll;
                        float extra = freedByShort && shortAll > 0f ? freed * shortG[g] / shortAll : freed * GuildWeight(g) / wAct;
                        float h = own + extra;
                        // wpis 91 + 174.0 (e): kowale, ktorzy wczoraj naprawiali, i konserwacja zapasu na polce - mniej rak platnerzy i miecznikow
                        if (workshop.WorkshopType.IsHidden && (g == "platnerz" || g == "miecznik"))
                        {
                            float smithW = GuildWeight("platnerz") + GuildWeight("miecznik");
                            if (smithW > 0f)
                            {
                                float cut = Math.Min(h, (SmithHours.ManDaysYesterday(town) + ArmsLeaks.UpkeepManDays(town)) * GuildWeight(g) / smithW / Math.Max(1, ActiveSmithWorkshops(town)));
                                h -= cut; repairs += cut;
                            }
                        }
                        hg[g] = h; extraG[g] = extra;
                    }
                    if (!follow || round == 2) break;
                    // (E) plan: linia bez sztuki w robocie jest czynna, gdy cos skonczy w planie z rownego podzialu jako podloga
                    bool changed = false;
                    foreach (var lp in plan.Values)
                    {
                        if (!lp.Active || lp.Wip) continue;
                        float h; int n;
                        if (!hg.TryGetValue(lp.Guild, out h) || !nG.TryGetValue(lp.Guild, out n) || n <= 0) continue;
                        if (!startable(lp, lp.Labor + h / n * planDays)) { lp.Active = false; changed = true; _planIdleLines++; }
                    }
                    if (!changed) break;
                }
                // (D) linie cechu wedlug braku, podloga dla zaczetych, suma = rece cechu
                float toLines = 0f;
                foreach (var g in hg.Keys)
                {
                    var lines = new List<LinePlan>();
                    foreach (var lp in plan.Values) if (lp.Active && lp.Guild == g) lines.Add(lp);
                    int n = lines.Count;
                    if (n == 0) continue;
                    float h = hg[g], fl = h / n, sumShort = 0f;
                    foreach (var lp in lines) sumShort += lp.Short;
                    var raw = new float[n];
                    for (int i = 0; i < n; i++) raw[i] = alpha > 0f && sumShort > 0f ? h * ((1f - alpha) / n + alpha * lines[i].Short / sumShort) : fl;
                    var fixd = new bool[n];
                    for (int it = 0; it <= n; it++)
                    {
                        float rem = h, restRaw = 0f; int free = 0;
                        for (int i = 0; i < n; i++) { if (fixd[i]) rem -= fl; else { restRaw += raw[i]; free++; } }
                        bool changed = false;
                        for (int i = 0; i < n; i++)
                        {
                            if (fixd[i]) { lines[i].Share = fl; continue; }
                            lines[i].Share = restRaw > 0f ? raw[i] * rem / restRaw : (free > 0 ? rem / free : 0f);
                            if (lines[i].Wip && lines[i].Share < fl - 1e-4f) { fixd[i] = true; changed = true; }
                        }
                        if (!changed) break;
                    }
                    float sum = 0f;
                    foreach (var lp in lines) { lp.Floor = fl; sum += lp.Share; }
                    if (h > 0.01f) _devMax = Math.Max(_devMax, Math.Abs(sum - h) / h);
                    toLines += sum;
                    int gi = Array.IndexOf(GuildOrder, g);
                    if (gi >= 0) { _gAssigned[gi] += h; _gFreed[gi] += extraG[g]; }
                }
                _hands += H; _handsLines += toLines; _handsRepair += repairs;
            }
            catch (Exception e) { _planStumbles++; Log.Error("WorkshopLaw.BuildPlan", e); }
            return plan;
        }

        // ------------------------------------------------------------ 174.1: koszyki w robocie (miasto -> koszyk), liczniki doby
        private static readonly Dictionary<Town, Dictionary<int, int>> _wipBasket = new Dictionary<Town, Dictionary<int, int>>();
        private static int BasketOf(ItemObject it) { return it != null ? (int)it.ItemType * 10 + TierOf(it) : 0; }
        private static int WipBasket(Town t, int b) { Dictionary<int, int> d; int n; return t != null && _wipBasket.TryGetValue(t, out d) && d.TryGetValue(b, out n) ? n : 0; }
        private static void WipBasketAdd(Town t, int b, int delta)
        {
            if (t == null) return;
            Dictionary<int, int> d;
            if (!_wipBasket.TryGetValue(t, out d)) { d = new Dictionary<int, int>(); _wipBasket[t] = d; }
            int n; d.TryGetValue(b, out n); d[b] = Math.Max(0, n + delta);
        }
        /// <summary>Po wczytaniu (174.0b): licznik koszykow w robocie z _wip.</summary>
        private static void RebuildWipBasket()
        {
            _wipBasket.Clear();
            foreach (var kv in _wip)
            {
                var ws = kv.Key.Key; var w = kv.Value;
                var t = ws != null && ws.Settlement != null ? ws.Settlement.Town : null;
                if (w != null && w.Item != null && t != null) WipBasketAdd(t, BasketOf(w.Item), +1);
            }
        }

        private static float _hands, _handsLines, _handsRepair, _devMax;
        private static readonly float[] _gAssigned = new float[6], _gFreed = new float[6], _gUsed = new float[6];
        private static int _rejPlan, _skipPlan, _planIdleLines, _civSkipped, _mun, _madeT56, _planStumbles;
        private static readonly float[] _qMun = new float[7], _qAll = new float[7];
        private static readonly int[] _qMunN = new int[7], _qAllN = new int[7];
        private static readonly Dictionary<int, float[]> _worldBasket = new Dictionary<int, float[]>();   // koszyk -> [popyt, polka, w toku, brak] (suma miast, raz na miasto na dobe)
        private static readonly Dictionary<Town, HashSet<int>> _seenTB = new Dictionary<Town, HashSet<int>>();
        private static readonly Dictionary<int, int> _madeBasket = new Dictionary<int, int>();

        private static void NewDay174()
        {
            _hands = _handsLines = _handsRepair = _devMax = 0f;
            Array.Clear(_gAssigned, 0, 6); Array.Clear(_gFreed, 0, 6); Array.Clear(_gUsed, 0, 6);
            _rejPlan = _skipPlan = _planIdleLines = _civSkipped = _mun = _madeT56 = _planStumbles = 0;
            Array.Clear(_qMun, 0, 7); Array.Clear(_qAll, 0, 7); Array.Clear(_qMunN, 0, 7); Array.Clear(_qAllN, 0, 7);
            _worldBasket.Clear(); _seenTB.Clear(); _madeBasket.Clear();
        }

        private static void NoteBasketDay(Town town, Cand c)
        {
            try
            {
                HashSet<int> seen;
                if (!_seenTB.TryGetValue(town, out seen)) { seen = new HashSet<int>(); _seenTB[town] = seen; }
                if (!seen.Add(c.Basket)) return;
                float[] a;
                if (!_worldBasket.TryGetValue(c.Basket, out a)) { a = new float[5]; _worldBasket[c.Basket] = a; }
                a[0] += c.D; a[1] += c.S; a[2] += WipBasket(town, c.Basket); a[3] += c.Short; if (c.Short > 0f) a[4] += 1f;
            }
            catch { }
        }

        private static void NoteFinished(Town town, ItemObject it, float days, WorkshopType.Production production)
        {
            try
            {
                int b = BasketOf(it), t = b % 10;
                WipBasketAdd(town, b, -1);
                int n; _madeBasket.TryGetValue(b, out n); _madeBasket[b] = n + 1;
                if (t >= 5) _madeT56++;
                int gi = Array.IndexOf(GuildOrder, GuildOf(production));
                if (gi >= 0) _gUsed[gi] += days;
                if (t >= 1 && t <= 3) { _qAll[t] += ArmsPricing.QualityOf(it); _qAllN[t]++; }
            }
            catch { }
        }

        private static void NoteMunition(ItemObject it)
        {
            try { _mun++; int t = BasketOf(it) % 10; if (t >= 1 && t <= 6) { _qMun[t] += ArmsPricing.QualityOf(it); _qMunN[t]++; } } catch { }
        }

        /// <summary>Dopisek 174.1 do linii "Warsztaty: dzien".</summary>
        private static string Text174()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            float idle = Math.Max(0f, _hands - _handsLines - _handsRepair);
            sb.Append(" | rece (174.1): ").Append(_hands.ToString("0", inv)).Append(" roboczodni (liniom ").Append(_handsLines.ToString("0", inv)).Append(", naprawy i konserwacja ")
              .Append(_handsRepair.ToString("0", inv)).Append(", bezczynne ").Append(idle.ToString("0", inv)).Append(" = ").Append(_hands > 0f ? (100f * idle / _hands).ToString("0.0", inv) : "0").Append("%), cechy [");
            for (int i = 0; i < 6; i++) { if (i > 0) sb.Append(", "); sb.Append(GuildOrder[i]).Append(' ').Append(_gAssigned[i].ToString("0", inv)).Append('/').Append(_gUsed[i].ToString("0", inv)); }
            float freed = 0f; for (int i = 0; i < 6; i++) freed += _gFreed[i];
            sb.Append("] (przydzielone/w zrobionych sztukach); rece przeniesione z cechow bez roboty: ").Append(freed.ToString("0", inv)).Append(" (");
            for (int i = 0; i < 6; i++) { if (i > 0) sb.Append(", "); sb.Append(GuildOrder[i]).Append(' ').Append(_gFreed[i].ToString("0", inv)); }
            sb.Append(FreedByShortage ? "; wedlug braku" : "; wagi Paryza").Append("); kontrola: suma udzialow linii = rece cechow (odchylenie max ").Append((100f * _devMax).ToString("0.00", inv)).Append("%)");
            int shortB = 0; foreach (var a in _worldBasket.Values) shortB += (int)a[4];
            sb.Append("; wybor: ").Append(ChooseByShortage ? "wedlug braku" : "wedlug zysku").Append(", koszyki z brakiem (miasto x koszyk) ").Append(shortB).Append(", sztuk \"na amunicje\" ").Append(_mun).Append(" (srednie q [");
            for (int t = 1; t <= 3; t++) { if (t > 1) sb.Append(", "); sb.Append('t').Append(t).Append(' ').Append(_qMunN[t] > 0 ? (_qMun[t] / _qMunN[t]).ToString("0.00", inv) : "-"); }
            sb.Append("]; wszystkich zrobionych t1-t3 [");
            for (int t = 1; t <= 3; t++) { if (t > 1) sb.Append(", "); sb.Append('t').Append(t).Append(' ').Append(_qAllN[t] > 0 ? (_qAll[t] / _qAllN[t]).ToString("0.00", inv) : "-"); }
            sb.Append("]), kandydatow odrzuconych \"nie skonczy w planie\" ").Append(_rejPlan).Append(" (cykli bez startu z tego powodu ").Append(_skipPlan).Append(", linii nieczynnych przez plan ").Append(_planIdleLines)
              .Append("); zrobiono t5-6: ").Append(_madeT56).Append(" szt.; cywilnych pominietych ").Append(_civSkipped).Append(" (pozycji rankingu)");
            if (_planStumbles > 0) sb.Append("; potkniecia planu ").Append(_planStumbles);
            return sb.ToString();
        }

        /// <summary>Linia "Warsztaty (diagnoza): najwiekszy brak" - 6 koszykow swiata z najwiekszym brakiem (suma miast).</summary>
        private static void DiagShort()
        {
            try
            {
                if (_worldBasket.Count == 0) return;
                var l = new List<KeyValuePair<int, float[]>>(_worldBasket);
                l.Sort((a, b) => b.Value[3].CompareTo(a.Value[3]));
                var parts = new List<string>();
                for (int i = 0; i < l.Count && i < 6; i++)
                {
                    var a = l[i].Value; int made; _madeBasket.TryGetValue(l[i].Key, out made);
                    parts.Add((ItemObject.ItemTypeEnum)(l[i].Key / 10) + " t" + (l[i].Key % 10) + ": popyt " + (int)a[0] + ", polka " + (int)a[1] + ", w toku " + (int)a[2] + ", brak " + (int)a[3]
                              + " w " + (int)a[4] + " miastach, zrobiono dzis " + made);
                }
                Log.Info("Warsztaty (diagnoza): najwiekszy brak - [" + string.Join("; ", parts.ToArray()) + "] (" + parts.Count + " koszykow swiata; popyt i polka z rankingow warsztatow - miasta, ktore dzis liczyly ten koszyk).");
            }
            catch { }
        }


        /// <summary>Cena jednostki surowca dla warsztatu (m: 0 ruda, 1 drewno, 2 skora, 3 len/welna). Gra zna tylko pensy calkowite:
        /// ruda (0.75 d za 10 kg) i drewno (0.35 d) stoja na 1-2 d, czyli 2-5x historii - wtedy cena historyczna za kg x waga;
        /// skora, len, welna (dziesiatki pensow) - cena targu (brak = drozej).</summary>
        internal static float MatPrice(Town town, ItemObject it, int m)   // internal: MendMaterial - material do naprawy po tej samej cenie, co warsztaty
        {
            if (it == null) return 0f;
            var s = Settings.Current;
            // cena surowcow od niedoboru (RawPrice): przy popycie z prawdziwego zuzycia warsztat placi za rude i drewno cene targu,
            // jak za skore i len - ladunek po 8 d i 4 d miesci sie w calych pensach, a stala cena historyczna dawala w jednym
            // miescie dwie ceny rudy (rzemieslnik 7.5 d, kuznia narzedzi cene targu) i kazala kasie miasta doplacac do drogiej rudy
            if (HistoricalPrices.On && it.Value < 10 && !RawPrice.UseOn)
            {
                float perKg = m == 0 ? s.HistIronOrePerKg : m == 1 ? s.HistWoodPerKg : m == 2 ? s.HistLeatherPerKg : s.HistLinenPerKg;
                return perKg * Math.Max(0.1f, it.Weight);
            }
            return town.GetItemPrice(it, null, false);
        }

        private static int InProgress() { int n = 0; foreach (var w in _wip.Values) if (w.Item != null) n++; return n; }

        // ------------------------------------------------------------ 174.0b: robota w toku w zapisie gry (audyt 05.10 W7)
        // Dotad _wip (sztuki w robocie - surowiec juz zdjety z polki i zaplacony), _owed (dlug ulamkowy surowca) i zamowienia SupplyDemand._unmet
        // zyly tylko w pamieci sesji: kazde wczytanie kasowalo zaczete zbroje razem z kupiona ruda (ujscie w nicosc), dlug ulamkowy dawal kazdemu
        // warsztatowi znowu do 1 jednostki surowca za darmo, a zamowienia startowaly od zera. Teraz klucz "arm_workshops" (SaveText.Sync, kawalki),
        // rozwiazanie na obiekty gry w OnSessionLaunched (w SyncData osad jeszcze nie ma do znalezienia). Warsztat, ktorego nie ma albo ktory zmienil
        // typ, i przedmiot, ktorego nie ma - pominiete (surowiec tej sztuki przepada - jawnie w linii po wczytaniu). Stary zapis: pusto, jak dotad.
        // Rekordy (~): W|osada|indeks warsztatu|typ warsztatu|linia|przedmiot|dni|praca|koszt surowca|dzien  oraz  O|osada|indeks|typ|ruda|drewno|skora|len
        private static string _pending;
        internal static bool SaveOn { get { var s = Settings.Current; return s != null && s.WorkshopStateInSave; } }

        private static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }
        private static float PF(string t) { float v; return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f; }

        private static bool WorkshopRef(Workshop w, out string sid, out int ix, out string type)
        {
            sid = null; ix = -1; type = null;
            try
            {
                var st = w != null ? w.Settlement : null;
                var town = st != null ? st.Town : null;
                if (town == null || town.Workshops == null || w.WorkshopType == null) return false;
                ix = Array.IndexOf(town.Workshops, w);
                if (ix < 0) return false;
                sid = st.StringId; type = w.WorkshopType.StringId;
                return !string.IsNullOrEmpty(sid) && !string.IsNullOrEmpty(type);
            }
            catch { return false; }
        }

        internal static string Export()
        {
            try
            {
                if (_pending != null) ResolvePending("zapis przed startem sesji");
                if (!SaveOn) return "";
                var sb = new StringBuilder();
                int nW = 0, nO = 0, nSkip = 0;
                foreach (var kv in _wip)
                {
                    string sid, type; int ix;
                    var w = kv.Value;
                    if (w == null || !WorkshopRef(kv.Key.Key, out sid, out ix, out type) || (kv.Key.Value ?? "").IndexOf('|') >= 0) { nSkip++; continue; }
                    if (w.Item != null) nW++;
                    sb.Append("W|").Append(sid).Append('|').Append(ix).Append('|').Append(type).Append('|').Append(kv.Key.Value ?? "").Append('|')
                      .Append(w.Item != null ? w.Item.StringId : "").Append('|').Append(F(w.Days)).Append('|').Append(F(w.Labor)).Append('|')
                      .Append(w.MatCost).Append('|').Append(w.Day).Append('~');
                }
                foreach (var kv in _owed)
                {
                    string sid, type; int ix;
                    var o = kv.Value;
                    if (o == null || o.Length < 4 || (o[0] == 0f && o[1] == 0f && o[2] == 0f && o[3] == 0f) || !WorkshopRef(kv.Key, out sid, out ix, out type)) continue;
                    sb.Append("O|").Append(sid).Append('|').Append(ix).Append('|').Append(type).Append('|').Append(F(o[0])).Append('|').Append(F(o[1]))
                      .Append('|').Append(F(o[2])).Append('|').Append(F(o[3])).Append('~');
                    nO++;
                }
                Log.Info("Warsztaty (zapis 174): zapis gry - sztuk w toku " + nW + " (w pamieci " + InProgress() + "), dlugow surowca " + nO + " warsztatow, pozycji bez osady/warsztatu pominietych " + nSkip
                         + ", zamowien " + SupplyDemand.OrdersCount() + "; " + sb.Length + " znakow.");
                return sb.ToString();
            }
            catch (Exception e) { Log.Error("WorkshopLaw.Export", e); return ""; }
        }

        /// <summary>Z SyncData: tylko zapamietanie - rozwiazanie w ResolvePending (OnSessionLaunched).</summary>
        internal static void Import(string s)
        {
            _wip.Clear(); _owed.Clear();
            _pending = string.IsNullOrEmpty(s) ? null : s;
        }

        private static Workshop FindWorkshop(string sid, string ixs, string type)
        {
            Settlement st = null;
            try { st = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<Settlement>(sid); } catch { }
            var town = st != null ? st.Town : null;
            int ix;
            if (town == null || town.Workshops == null || !int.TryParse(ixs, NumberStyles.Integer, CultureInfo.InvariantCulture, out ix) || ix < 0 || ix >= town.Workshops.Length) return null;
            var w = town.Workshops[ix];
            return w != null && w.WorkshopType != null && w.WorkshopType.StringId == type ? w : null;
        }

        /// <summary>174.0b: robota w toku z zapisu na obiekty gry (OnSessionLaunched; awaryjnie z Export) i linia "Warsztaty (zapis 174)".</summary>
        internal static void ResolvePending(string why)
        {
            var s = _pending;
            _pending = null;
            string unmet = SupplyDemand.ImportReport();
            if (string.IsNullOrEmpty(s))
            {
                if (unmet != null) Log.Info("Warsztaty (zapis 174): " + why + " - sztuk w toku w zapisie brak (stary zapis albo pusty stan); " + unmet + ".");
                return;
            }
            if (!SaveOn)
            {
                Log.Info("Warsztaty (zapis 174): " + why + " - zapis ma robote w toku, ale Workshop State In Save = off - pominieta (jak dotad: surowiec zaczetych sztuk przepada).");
                return;
            }
            Resolve();
            int pieces = 0, banks = 0, skipped = 0, owedN = 0, owedSkip = 0;
            var mat = new float[4]; var lost = new float[4];
            var om = TaleWorlds.ObjectSystem.MBObjectManager.Instance;
            foreach (var rec in s.Split('~'))
            {
                if (rec.Length == 0) continue;
                try
                {
                    var a = rec.Split('|');
                    if (a.Length == 10 && a[0] == "W")
                    {
                        ItemObject it = null;
                        if (a[5].Length > 0) { try { it = om.GetObject<ItemObject>(a[5]); } catch { } }
                        float days = PF(a[6]), labor = PF(a[7]);
                        int mc, day;
                        int.TryParse(a[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out mc);
                        int.TryParse(a[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out day);
                        var w = FindWorkshop(a[1], a[2], a[3]);
                        if (w == null || (a[5].Length > 0 && it == null))
                        {
                            skipped++;
                            if (it != null) { float d0; var nd = Needs(it, out d0); if (nd != null) for (int m = 0; m < 4; m++) lost[m] += nd[m]; }
                            continue;
                        }
                        var wip = new Wip { Item = it, Days = it != null ? days : 0f, Labor = Math.Max(0f, labor), MatCost = it != null ? mc : 0, Day = day };
                        _wip[new KeyValuePair<Workshop, string>(w, a[4])] = wip;
                        if (it != null) { pieces++; float d1; var nd = Needs(it, out d1); if (nd != null) for (int m = 0; m < 4; m++) mat[m] += nd[m]; }
                        else banks++;
                    }
                    else if (a.Length == 8 && a[0] == "O")
                    {
                        var w = FindWorkshop(a[1], a[2], a[3]);
                        if (w == null) { owedSkip++; continue; }
                        _owed[w] = new[] { PF(a[4]), PF(a[5]), PF(a[6]), PF(a[7]) };
                        owedN++;
                    }
                }
                catch (Exception e) { Log.Error("WorkshopLaw.ResolvePending", e); skipped++; }
            }
            RebuildWipBasket();   // 174.1: licznik koszykow w robocie miasta z wczytanych sztuk
            var inv = CultureInfo.InvariantCulture;
            Log.Info("Warsztaty (zapis 174): " + why + " - wczytano sztuk w toku " + pieces + " (surowca: ruda " + mat[0].ToString("0.0", inv) + ", drewno " + mat[1].ToString("0.0", inv)
                     + ", skora " + mat[2].ToString("0.0", inv) + ", len " + mat[3].ToString("0.0", inv) + " jednostek rynku), linii z odlozona praca bez sztuki " + banks
                     + ", pominieto " + skipped + " (warsztatu albo przedmiotu juz nie ma - surowiec przepadl: ruda " + lost[0].ToString("0.0", inv) + ", drewno " + lost[1].ToString("0.0", inv)
                     + ", skora " + lost[2].ToString("0.0", inv) + ", len " + lost[3].ToString("0.0", inv) + "), dlugow surowca " + owedN + " warsztatow (pominieto " + owedSkip + "); "
                     + (unmet ?? "zamowien w zapisie brak") + ".");
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
                         + ", brak surowca " + _skipMat + " [ruda " + _skipMatBy[0] + ", drewno " + _skipMatBy[1] + ", skora " + _skipMatBy[2] + ", len/welna " + _skipMatBy[3]
                         + " - cykl liczony przy kazdym surowcu, ktorego zabraklo na ktoras sztuke z rankingu], w robocie (cykle) " + _skipLabor + ", brak zlota/kupca " + _skipGold + "; rozpoczete sztuki " + _started + ", w toku teraz " + InProgress()
                         + (TownCrafts.Active ? " | garbowanie i tkanie 1:1 wylaczone (rzemioslo miasta 148 - linia \"Rzemioslo miasta\")" : " | rzemieslnicy miasta wygarbowali skor " + _tanned + ", utkali plotna " + _woven) + " | z niczego zablokowane: cykle rzemieslnikow " + _freeRawBlocked + ", sztabki/wegiel z losowania -> ruda/drewno " + _swappedSmith + Text174() + ".");
            }
            FlushDiag();
            DiagShort();   // 174.1: najwiekszy brak swiata (koszyki)
            NewDay174();
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
                    float cost = need[0] * pOre + need[1] * pWood + need[2] * pLea + need[3] * pLin + days * DayWage(it, town);
                    float revenue = Revenue(town, it);
                    float pd = (revenue - cost) / Math.Max(0.1f, days);
                    if (pd > bestPd)
                    {
                        bestPd = pd; best = it;
                        bestTxt = it.StringId + " (Value " + it.Value + ", cena " + revenue + ") koszt " + (int)cost
                                  + " = ruda " + need[0].ToString("0.0") + "x" + pOre.ToString("0.##") + " drewno " + need[1].ToString("0.0") + "x" + pWood.ToString("0.##")
                                  + " skora " + need[2].ToString("0.0") + "x" + pLea.ToString("0.##") + " len " + need[3].ToString("0.0") + "x" + pLin.ToString("0.##")
                                  + " dni " + days.ToString("0.0") + "x" + DayWage(it, town).ToString("0.##");
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
