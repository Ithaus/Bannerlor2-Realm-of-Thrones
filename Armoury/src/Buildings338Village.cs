using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 338 grupy 8, 10, 11 - PRODUKCJA WSI.
    /// 8 (Buildings338Roads) DROGI (Roads and Paths miasta i zamku: produkcja wsi +5/10/15%, VillageProduction, AddFactor): model gry liczy
    /// (DefaultVillageProductionCalculatorModel:63-65), ale pod KS (StartCalibration.ProdStep, beta 1) wynik zastepuje stawka KS bez budynkow. Naprawa: przy skladaniu
    /// stawek KS wsi (StartCalibration.Live - tablica A, raz na przeliczenie okregu) A[i] x czynnik drog warowni wsi (Town.AddEffectOfBuildings(VillageProduction) na
    /// bazie 1). Wsie poza KS i krainy wrocone - premie liczy model gry (bez nas). W rampie (0 &lt; beta &lt; 1) obie strony maja premie raz.
    /// 10 (Buildings338VillageProd) BUDYNKI PRODUKCJI WSI BK (Farming, Mining, Animal Housing, Sawmill, Fish Farm +5/10/15% swojej kategorii) i PROJEKT Production
    /// (+15%): jedyny czytnik (PopulationManager.ApplyProductionBuildingEffect, BKVillageProductionModel:117/134) - model BK nie jest czynny (BetterEconomy).
    /// Naprawa: czynnik BK (ApplyProductionBuildingEffect - przypisanie kategorii z BK; projekt Production: VillageData.CurrentBuilding jak w BK) - (a) poza KS: krok
    /// w MaterialLaw.ProdPostfix (czynnik x suma - mnozy wynik), (b) pod KS: w tej samej tablicy A co drogi. Gdy czynny bylby model BK - nic (BK liczy sam).
    /// 11 (Buildings338VillageGoods) UL (Bee Skeps): waga miodu BK 0.5 + 1 x poziom ula (drwale i trapperzy) w VillageFruit.Base (wies poza KS). 338c: CHLEB (Bakery,
    /// Mill) z ziarna wsi, SKORA (Fur Tannery) ze skor wsi (338d: bez futer - opis "hides"), RUDY kopalni wsi BK (Mines) z rak wsi (wartosc zdjeta z innych plonow) - pod KS w stawce A i w
    /// Planie, poza KS baza + czynnik plonu-zrodla (opis przy GoodsInit).
    /// Nic z niczego: te same rece daja wiecej towaru z budynku, za ktory pan zaplacil (opis budynku); ludzi i zlota nie przybywa.
    /// </summary>
    internal static class Buildings338Village
    {
        private const string ProductionId = "bannerkings_daily_production";
        private const float ProductionBonus = 0.15f;
        private static readonly object Gate = new object();
        private static readonly Dictionary<Village, double> _roadsAdd = new Dictionary<Village, double>(), _prodAddKs = new Dictionary<Village, double>(), _skeps = new Dictionary<Village, double>();
        private static readonly Dictionary<Village, Dictionary<ItemObject, double>> _prodAdd = new Dictionary<Village, Dictionary<ItemObject, double>>();   // wies -> towar -> dopisek z ostatniego pytania modelu (poza KS)
        // recenzja 338 (wydajnosc): czynnik BK (wies, towar) na dobe - bez refleksji przy kazdym pytaniu modelu; czyszczony w Daily i SessionStart
        private static readonly Dictionary<Village, Dictionary<ItemObject, float>> _factor = new Dictionary<Village, Dictionary<ItemObject, float>>();
        private static ItemObject[] _ksItems;
        private static bool _bkModelActive;
        private static string _modelChain = "?";
        private static TextObject _txt;
        private static BuildingType _skepsType, _farming, _mining, _animal, _sawmill, _fishFarm;

        internal static void SessionStart()
        {
            lock (Gate) { _roadsAdd.Clear(); _prodAddKs.Clear(); _skeps.Clear(); _prodAdd.Clear(); _factor.Clear(); _vg.Clear(); _ksg.Clear(); }
            try { GoodsInit(); } catch (Exception e) { Buildings338.Stumble(Buildings338.GVGoods, "Village.GoodsInit", e); }
            _ksItems = null;
            _skepsType = Buildings338.VillageBuilding("Skeps");
            _farming = Buildings338.VillageBuilding("Farming"); _mining = Buildings338.VillageBuilding("Mining"); _animal = Buildings338.VillageBuilding("AnimalHousing");
            _sawmill = Buildings338.VillageBuilding("Sawmill"); _fishFarm = Buildings338.VillageBuilding("FishFarm");
            // lancuch modelu produkcji wsi (MBGameModel.BaseModel): czy czynny jest model BK (wtedy BK liczy budynki sam - grupa 10 nic nie dodaje)
            _bkModelActive = false;
            var names = new List<string>();
            try
            {
                object m = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.VillageProductionCalculatorModel : null;
                for (int i = 0; m != null && i < 8; i++)
                {
                    names.Add(m.GetType().Name);
                    if (m.GetType().Name == "BKVillageProductionModel") _bkModelActive = true;
                    var p = AccessTools.Property(m.GetType(), "BaseModel");
                    object next = null;
                    try { next = p != null ? p.GetValue(m, null) : null; } catch { }
                    if (next == null || ReferenceEquals(next, m)) break;
                    m = next;
                }
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GVProd, "Village.SessionStart", e); }
            _modelChain = names.Count > 0 ? string.Join(" -> ", names.ToArray()) : "?";
            Buildings338.Hook("produkcja wsi: model " + _modelChain + (_bkModelActive ? " (BK czynny - grupa 10 nic nie dodaje)" : ""));
        }

        private static ItemObject KsItem(int i)
        {
            var a = _ksItems;
            if (a == null)
            {
                var ids = KsDeliver.ItemIds;
                a = new ItemObject[ids.Length];
                for (int j = 0; j < ids.Length; j++) { try { a[j] = MBObjectManager.Instance.GetObject<ItemObject>(ids[j]); } catch { } }
                _ksItems = a;
            }
            return i >= 0 && i < a.Length ? a[i] : null;
        }

        // ------------------------------------------------------------ 8 drogi
        /// <summary>Czynnik drog warowni wsi (1.05/1.10/1.15 z perkami; 1 - brak, klucz wyl.).</summary>
        internal static float RoadsFactor(Village v)
        {
            if (v == null || !Buildings338.RoadsOn) return 1f;
            var b = v.Bound;
            var t = b != null && b.IsFortification ? b.Town : null;
            if (t == null) return 1f;
            var e = new ExplainedNumber(1f);
            t.AddEffectOfBuildings(BuildingEffectEnum.VillageProduction, ref e);
            float f = e.ResultNumber;
            return f > 0f && !float.IsNaN(f) ? f : 1f;
        }

        // ------------------------------------------------------------ 10 budynki produkcji wsi BK
        private static object VData(Village v) { return v != null ? Buildings338.VillageDataOf(Buildings338.PopData(v.Settlement)) : null; }

        /// <summary>Czynnik BK towaru we wsi: 1 + 0.05 x poziom budynku kategorii + 0.15 przy projekcie Production (jak BKVillageProductionModel - czynniki sie sumuja).</summary>
        private static float BkFactor(ItemObject item, object vd)
        {
            if (item == null || vd == null) return 1f;
            float f = Buildings338.VillageBuildingFactor(item, vd);
            if (Buildings338.VillageProjectOn && Buildings338.VillageCurrentId(vd) == ProductionId) f += ProductionBonus;   // W4: osobny klucz projektu
            return f;
        }

        /// <summary>BkFactor z pamiecia na dobe (ProdStep - pytania modelu produkcji wsi).</summary>
        private static float BkFactorCached(Village v, ItemObject item)
        {
            Dictionary<ItemObject, float> d; float f;
            lock (Gate) { if (_factor.TryGetValue(v, out d) && d.TryGetValue(item, out f)) return f; }
            f = BkFactor(item, VData(v));
            lock (Gate) { if (!_factor.TryGetValue(v, out d)) { d = new Dictionary<ItemObject, float>(); _factor[v] = d; } d[item] = f; }
            return f;
        }

        /// <summary>StartCalibration.Live (skladanie stawek KS wsi, raz na przeliczenie okregu): A[i] x drogi x budynki BK. Zwraca nic; liczniki dla linii doby.</summary>
        internal static void ScaleRates(Village v, float[] a)
        {
            if (v == null || a == null) return;
            bool roads = Buildings338.RoadsOn, prod = Buildings338.VillageProdOn && !_bkModelActive;
            if (roads || prod) ScaleCore(v, a, roads, prod);
            try { ScaleGoodsKs(v, a); } catch (Exception e) { Buildings338.Stumble(Buildings338.GVGoods, "Village.ScaleGoodsKs", e); }   // 338c: rudy, chleb, skora pod KS (po drogach i budynkach)
        }

        private static void ScaleCore(Village v, float[] a, bool roads, bool prod)
        {
            try
            {
                float rf = roads ? RoadsFactor(v) : 1f;
                object vd = prod ? VData(v) : null;
                double addR = 0, addP = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    float x = a[i];
                    if (!(x > 0f)) continue;
                    float pf = vd != null ? BkFactor(KsItem(i), vd) : 1f;
                    if (rf == 1f && pf == 1f) continue;
                    float y = x * rf * pf;
                    if (float.IsNaN(y) || float.IsInfinity(y) || y < 0f) continue;
                    addR += x * (rf - 1f);
                    addP += x * rf * (pf - 1f);
                    a[i] = y;
                }
                lock (Gate)
                {
                    if (addR > 0) _roadsAdd[v] = addR; else _roadsAdd.Remove(v);
                    if (addP > 0) _prodAddKs[v] = addP; else _prodAddKs.Remove(v);
                }
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GRoads, "Village.ScaleRates", e); }
        }

        /// <summary>MaterialLaw.ProdPostfix (wies poza KS / lancuch gry, po bazach VillageFruit/VillageCrafts): czynnik budynkow BK x suma czynnikow.</summary>
        internal static void ProdStep(Village v, ItemObject item, ref ExplainedNumber r)
        {
            if (v == null || item == null || _bkModelActive || !Buildings338.VillageProdOn) return;
            try
            {
                if (v.VillageState != Village.VillageStates.Normal) return;
                float f = BkFactorCached(v, item);
                if (!(f > 1f)) return;
                float before = r.ResultNumber;
                float sum = 1f + r.SumOfFactors;
                if (!(sum > 0f)) return;
                if (_txt == null) _txt = new TextObject("{=!}Village buildings (Banner Kings)");
                r.AddFactor((f - 1f) * sum, _txt);
                double add = r.ResultNumber - before;
                lock (Gate) { Dictionary<ItemObject, double> d; if (!_prodAdd.TryGetValue(v, out d)) { d = new Dictionary<ItemObject, double>(); _prodAdd[v] = d; } d[item] = add; }
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GVProd, "Village.ProdStep", e); }
        }

        // ------------------------------------------------------------ 11 ul (Skeps)
        /// <summary>VillageFruit.Base (miod drwali i trapperow): dodatkowa waga BK = poziom ula (1 za poziom, PopulationManager.GetProductions).</summary>
        internal static float Skeps(Village v)
        {
            if (v == null || !Buildings338.VillageGoodsOn || _skepsType == null) return 0f;
            try
            {
                int lvl = Buildings338.VillageLevel(VData(v), _skepsType);
                if (lvl <= 0) return 0f;
                lock (Gate) _skeps[v] = lvl * (v.GetHearthLevel() + 1) * 0.5;
                return lvl;
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GVGoods, "Village.Skeps", e); return 0f; }
        }

        // ============================================================ 11 (338c) CHLEB, SKORA, RUDY KOPALNI WSI BK - produkcja z pracy wsi, nic z niczego
        // Opisy BK: Bakery "allowing serfs to turn wheat into bread. Adds bread as production good"; Mill "grind grains for breadmaking. Increases bread production
        // (50%, 100%, 150%)"; Fur Tannery "tanning hides, turning these into leather. Adds leather to production"; Mines "Ores will be limited to the local resources
        // available and richness of the ground. Levels increase output of ores". Liczby z BK PopulationManager.GetProductions (wagi listy wsi): chleb = zboze x 0.15 x
        // poziom piekarni x (1 + 0.5 x poziom mlyna) (bez piekarni - nic: BK-owe 0.1 x zboze bez piekarni jest spoza opisu), skora 0.5 x poziom garbarni, rudy 0.5 x
        // poziom kopalni x udzial zloza. Waga BK = udzial rak wsi (BK dzieli sile robocza na pozycje listy: waga / suma wag), wiec:
        //  - chleb: udzial s = w / (1 + w) ZBOZA tej wsi idzie do piekarni - zboze x (1 - s), chleb = s x zboze x kg zboza / kg chleba (receptura KS ziarno -> chleb 1.0);
        //  - skora: udzial s = 0.5 L / (waga skor typu (min 1) + 0.5 L) SKOR wsi idzie do garbarni - skory x (1 - s), skora = s x skory x kg / (2.4 kg x kg skory)
        //    (KsTab.LeatherHide); 338d (recenzja): FUTRA NIE - opis BK mowi "tanning hides", a futro (400 zl) w skore (230 zl, 2.4 kg na kg) niszczyloby wartosc pracy
        //    wsi; wies bez skor (takze z samym futrem) - nic (licznik "bez surowca");
        //  - rudy: baza = waga x (poziom ognisk + 1) x 0.5 (wzor modelu gry), wartosc rudy (cena bazowa) zdjeta z INNYCH plonow tej wsi (te same rece - jak E3-D1):
        //    inne plony najwyzej -15%, JEDZENIE najwyzej -5% (338d: kazdy towar IsFood - zboze, ryby, mieso, ser, maslo, jaja, owoce, oliwki, daktyle, miod, wieloryb -
        //    oraz zywiec cow/hog/sheep; jedna grupa jedzenia); gdy prog wiaze - mniej rudy (dodane = zdjete). Poza KS tniemy tylko plony z listy typu wsi (CutSet - te
        //    same, z ktorych liczona jest wartosc zdjeta), nie bazy VillageFruit/VillageCrafts ani pozycje spoza listy.
        // Pod KS (wies zastosowana, beta 1) wszystko w tablicy stawek A (ScaleRates - raz na przeliczenie okregu; Plan widzi to samo, chleb i skora wracaja do Planu jako
        // zboze i skory/futra - PlanBack, bo Plan liczy przetwory z surowca), chleb i skora maja wlasna stawke (KsGoods - pierwszy krok MaterialLaw.ProdPostfix).
        // Poza KS - baza przed mnoznikami (GoodsBase, jak VillageFruit.Base) i czynnik plonu-zrodla (HostFactor) w MaterialLaw. Magazyn wsi + 5 dob nowych towarow (CapAdd).
        private sealed class VGoods
        {
            public float BreadS, LeatherS, OreScale = 1f, OtherC = 1f, FoodC = 1f;         // udzialy i czynniki poza KS
            public Dictionary<ItemObject, float> Ore;                                        // baza rudy poza KS (przed mnoznikami, przed / Times)
            public HashSet<ItemObject> CutSet;                                               // 338d: plony listy typu wsi, z ktorych zdjeto wartosc rudy (tylko je tnie HostFactor)
            public float BreadBase, LeatherBase, OreVal;                                    // poza KS - do linii doby
            public bool NoSrc;
        }
        private sealed class KsGood { public float Bread, Leather, GrainBack, HidesBack, OreAdd, OreVal, CutVal; public bool NoSrc; }
        private static readonly Dictionary<Village, VGoods> _vg = new Dictionary<Village, VGoods>();   // poza KS - na dobe
        private static readonly Dictionary<Village, KsGood> _ksg = new Dictionary<Village, KsGood>();   // pod KS - z ostatniego skladania stawek A (trwa do przeliczenia)
        private static BuildingType _bakery, _mill, _tannery, _minesV;
        private static ItemObject _bread, _leather, _grain, _hides;
        private const float OtherCutMax = 0.15f, FoodCutMax = 0.05f, LeatherHideKg = 2.4f;
        private static TextObject _txtGoods;

        private static void GoodsInit()
        {
            _bakery = Buildings338.VillageBuilding("Bakery"); _mill = Buildings338.VillageBuilding("Mill"); _tannery = Buildings338.VillageBuilding("Tannery"); _minesV = Buildings338.VillageBuilding("Mines");
            var om = MBObjectManager.Instance;
            _bread = om.GetObject<ItemObject>("bread"); _leather = om.GetObject<ItemObject>("leather"); _grain = om.GetObject<ItemObject>("grain");
            _hides = om.GetObject<ItemObject>("hides");
        }

        private static float Kg(ItemObject it) { return it != null && it.Weight > 0.05f ? it.Weight : 10f; }
        private static bool IsMineralId(string id) { return id == "iron" || id == "silver" || id == "salt" || id == "clay" || id == "limestone" || id == "marble" || id == "gold_ore"; }
        /// <summary>338d (recenzja): jedzenie = kazdy towar IsFood (bilans miasta) i zywiec (cow/hog/sheep - KS liczy go jako mieso); prog 5% na cala grupe.</summary>
        private static bool IsFood(ItemObject it) { return it != null && (it.IsFood || (it.HorseComponent != null && it.HorseComponent.IsLiveStock)); }

        private static float BreadShare(object vd)
        {
            int lb = Buildings338.VillageLevel(vd, _bakery);
            if (lb <= 0) return 0f;
            float w = 0.15f * lb * (1f + 0.5f * Buildings338.VillageLevel(vd, _mill));
            return w / (1f + w);
        }

        private static float LeatherShare(object vd, VillageType vt)
        {
            int lt = Buildings338.VillageLevel(vd, _tannery);
            if (lt <= 0) return 0f;
            float ws = 0f;
            if (vt != null) foreach (var p in vt.Productions) if (p.Item1 != null && ReferenceEquals(p.Item1, _hides)) ws += p.Item2;   // 338d: tylko skory (opis "hides")
            if (!(ws > 0f)) return -1f;   // 338d: wies bez skor - nie ma czego garbowac (NoSrc)
            float w = 0.5f * lt;
            return w / (Math.Max(1f, ws) + w);
        }

        /// <summary>Rudy kopalni wsi BK: (towar, waga BK 0.5 x poziom x udzial zloza); pusta - brak kopalni albo zloz.</summary>
        private static List<KeyValuePair<ItemObject, float>> OreWeights(Village v, object vd)
        {
            var res = new List<KeyValuePair<ItemObject, float>>();
            int lm = Buildings338.VillageLevel(vd, _minesV);
            if (lm <= 0) return res;
            int rich;
            foreach (var kv in Buildings338.Minerals(Buildings338.PopData(v.Settlement), out rich))
                if (kv.Key != null && IsMineralId(kv.Key.StringId)) res.Add(new KeyValuePair<ItemObject, float>(kv.Key, 0.5f * lm * kv.Value));
            return res;
        }

        /// <summary>Ciecie innych plonow o wartosc val (inne najwyzej 15%, zboze i ryby najwyzej 5%): zwraca zdjeta wartosc, c inne i c jedzenia.</summary>
        private static double Cut(double val, double vOth, double vFood, out float cOth, out float cFood)
        {
            cOth = 1f; cFood = 1f;
            if (!(val > 0)) return 0;
            double a = Math.Min(val, OtherCutMax * vOth);
            double b = Math.Min(val - a, FoodCutMax * vFood);
            if (vOth > 0) cOth = (float)(1 - a / vOth);
            if (vFood > 0) cFood = (float)(1 - b / vFood);
            return a + b;
        }

        // ------------------------------------------------------------ pod KS (ScaleRates)
        private static void ScaleGoodsKs(Village v, float[] a)
        {
            if (!Buildings338.VillageGoodsOn || _bkModelActive || a == null) { lock (Gate) _ksg.Remove(v); return; }
            object vd = VData(v);
            if (vd == null) { lock (Gate) _ksg.Remove(v); return; }
            var g = new KsGood();
            int iG = KsDeliver.IGrain, iH = KsDeliver.IHides;
            // rudy kopalni wsi: wartosc z innych plonow (przed chlebem i skora - te same rece)
            var ore = OreWeights(v, vd);
            if (ore.Count > 0)
            {
                float hl = (v.GetHearthLevel() + 1) * 0.5f;
                var add = new float[a.Length]; double val = 0;
                foreach (var kv in ore)
                {
                    int i = Array.IndexOf(KsDeliver.ItemIds, kv.Key.StringId);
                    if (i < 0 || i >= a.Length) continue;
                    add[i] += kv.Value * hl; val += kv.Value * hl * Math.Max(1, kv.Key.Value);
                }
                double vO = 0, vF = 0;
                for (int j = 0; j < a.Length; j++)
                {
                    if (!(a[j] > 0f) || add[j] > 0f) continue;
                    var it = KsItem(j); if (it == null) continue;
                    double x = a[j] * Math.Max(1, it.Value);
                    if (IsFood(it)) vF += x; else vO += x;
                }
                float cO, cF;
                double cut = Cut(val, vO, vF, out cO, out cF);
                if (cut > 0 && val > 0)
                {
                    float sc = (float)(cut / val);
                    double addV = 0, cutV = 0;   // 338d (recenzja B338-19): pomiar z tablicy - wartosc dodanej rudy i rzeczywisty spadek innych plonow (przed - po)
                    for (int j = 0; j < a.Length; j++)
                    {
                        var it = KsItem(j);
                        if (add[j] > 0f) { a[j] += add[j] * sc; g.OreAdd += add[j] * sc; if (it != null) addV += add[j] * sc * Math.Max(1, it.Value); continue; }
                        if (!(a[j] > 0f) || it == null) continue;
                        float b0 = a[j];
                        a[j] *= IsFood(it) ? cF : cO;
                        cutV += (b0 - a[j]) * (double)Math.Max(1, it.Value);
                    }
                    g.OreVal = (float)addV; g.CutVal = (float)cutV;
                }
            }
            // chleb z ziarna wsi
            float sb = BreadShare(vd);
            if (sb > 0f && iG < a.Length && a[iG] > 0f && _bread != null)
            {
                float conv = sb * a[iG];
                a[iG] -= conv; g.GrainBack = conv;
                g.Bread = conv * Kg(_grain) / Kg(_bread);
            }
            // skora ze skor wsi (338d: bez futer)
            float sl = LeatherShare(vd, v.VillageType);
            if (sl < 0f) g.NoSrc = true;
            else if (sl > 0f && _leather != null)
            {
                float src = 0f;
                if (iH >= 0 && iH < a.Length && a[iH] > 0f) { float c = sl * a[iH]; a[iH] -= c; g.HidesBack = c; src += c * Kg(_hides); }
                if (src > 0f) g.Leather = src / (LeatherHideKg * Kg(_leather)); else g.NoSrc = true;
            }
            lock (Gate) { if (g.Bread > 0f || g.Leather > 0f || g.OreAdd > 0f || g.NoSrc) _ksg[v] = g; else _ksg.Remove(v); }
        }

        /// <summary>StartCalibration.Live (mk, mkByM): Plan liczy przetwory z surowca - zboze zamienione na chleb i skory/futra na skore wracaja do podazy Planu.</summary>
        internal static double PlanBack(Village v, int i)
        {
            if (v == null) return 0;
            KsGood g;
            lock (Gate) { if (!_ksg.TryGetValue(v, out g)) return 0; }
            if (i == KsDeliver.IGrain) return g.GrainBack;
            if (i == KsDeliver.IHides) return g.HidesBack;
            return 0;   // 338d: futra nie ida do garbarni (Plan robi skore tylko ze skor)
        }

        /// <summary>MaterialLaw.ProdPostfix (pierwszy krok, przed StartCalibration.ProdStep): wies KS zastosowana (beta 1) - chleb i skora = stawka z ScaleRates
        /// (zboze i surowiec juz zmniejszone w stawce A); true - reszta lancucha pominieta (jak towar KS).</summary>
        internal static bool KsGoods(Village v, ItemObject item, ref ExplainedNumber r)
        {
            if (v == null || item == null || (!ReferenceEquals(item, _bread) && !ReferenceEquals(item, _leather))) return false;
            if (!Buildings338.VillageGoodsOn || _bkModelActive || !StartCalibration.FullFor(v)) return false;
            try
            {
                KsGood g;
                lock (Gate) { if (!_ksg.TryGetValue(v, out g)) g = null; }
                if (g == null) return false;   // wies bez piekarni i garbarni - lancuch jak dotad (model gry daje 0)
                float x = ReferenceEquals(item, _bread) ? g.Bread : g.Leather;
                if (v.VillageState != Village.VillageStates.Normal || v.TradeBound == null || !(x > 0f) || float.IsNaN(x)) x = 0f;
                r = new ExplainedNumber(x, false, null);
                return true;
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GVGoods, "Village.KsGoods", e); return false; }
        }

        // ------------------------------------------------------------ poza KS (MaterialLaw)
        private static VGoods GoodsOf(Village v)
        {
            VGoods g;
            lock (Gate) { if (_vg.TryGetValue(v, out g)) return g; }
            g = new VGoods();
            object vd = VData(v);
            var vt = v.VillageType;
            if (vd != null && vt != null)
            {
                float hl = (v.GetHearthLevel() + 1) * 0.5f;
                g.BreadS = BreadShare(vd);
                g.LeatherS = LeatherShare(vd, vt);
                float wg = 0f, wsrc = 0f;
                foreach (var p in vt.Productions)
                {
                    if (p.Item1 == null) continue;
                    if (ReferenceEquals(p.Item1, _grain)) wg += p.Item2;
                    if (ReferenceEquals(p.Item1, _hides)) wsrc += p.Item2 * Kg(_hides);   // 338d: bez futer
                }
                if (g.BreadS > 0f && wg > 0f && _bread != null) g.BreadBase = g.BreadS * wg * hl * Kg(_grain) / Kg(_bread); else g.BreadS = 0f;
                if (g.LeatherS < 0f) { g.LeatherS = 0f; g.NoSrc = true; }
                else if (g.LeatherS > 0f && _leather != null)
                {
                    if (wsrc > 0f) g.LeatherBase = g.LeatherS * wsrc * hl / (LeatherHideKg * Kg(_leather));
                    else { g.LeatherS = 0f; g.NoSrc = true; }
                }
                var ore = OreWeights(v, vd);
                if (ore.Count > 0)
                {
                    double val = 0, vO = 0, vF = 0;
                    var dict = new Dictionary<ItemObject, float>();
                    var cutSet = new HashSet<ItemObject>();
                    foreach (var kv in ore) { float q = kv.Value * hl; float o; dict.TryGetValue(kv.Key, out o); dict[kv.Key] = o + q; val += q * Math.Max(1, kv.Key.Value); }
                    foreach (var p in vt.Productions)
                    {
                        if (p.Item1 == null || dict.ContainsKey(p.Item1) || !(p.Item2 > 0f)) continue;
                        double x = p.Item2 * hl * Math.Max(1, p.Item1.Value);
                        if (IsFood(p.Item1)) vF += x; else vO += x;
                        cutSet.Add(p.Item1);
                    }
                    float cO, cF;
                    double cut = Cut(val, vO, vF, out cO, out cF);
                    if (cut > 0 && val > 0)
                    {
                        g.OreScale = (float)(cut / val); g.OtherC = cO; g.FoodC = cF; g.OreVal = (float)cut; g.CutSet = cutSet;
                        g.Ore = new Dictionary<ItemObject, float>();
                        foreach (var kv in dict) g.Ore[kv.Key] = kv.Value * g.OreScale;
                    }
                }
            }
            lock (Gate) _vg[v] = g;
            return g;
        }

        private static bool GoodsLive(Village v)
        {
            return v != null && Buildings338.VillageGoodsOn && !_bkModelActive && v.VillageState == Village.VillageStates.Normal && v.TradeBound != null && v.VillageType != null
                   && !StartCalibration.FullFor(v);
        }

        /// <summary>MaterialLaw.ProdPostfix (po VillageCrafts.Base, przed mnoznikami): baza chleba, skory i rud kopalni wsi poza KS (ruda / Times - MineralOnce mnozy
        /// caly wynik przez powtorzenia listy BK, baza 338 liczy sie raz).</summary>
        internal static void GoodsBase(Village v, ItemObject item, ref ExplainedNumber r)
        {
            if (item == null || !GoodsLive(v)) return;
            try
            {
                var g = GoodsOf(v);
                float x = 0f;
                if (ReferenceEquals(item, _bread)) x = g.BreadBase;
                else if (ReferenceEquals(item, _leather)) x = g.LeatherBase;
                else if (g.Ore != null) { float o; if (g.Ore.TryGetValue(item, out o)) x = o / Math.Max(1, MineralOnce.Times(v, item)); }
                if (!(x > 0f) || float.IsNaN(x) || float.IsInfinity(x)) return;
                if (_txtGoods == null) _txtGoods = new TextObject("{=!}Village buildings: bakery, tannery, mines (Banner Kings)");
                r.Add(x, _txtGoods);
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GVGoods, "Village.GoodsBase", e); }
        }

        /// <summary>MaterialLaw.ProdPostfix (mnozniki): czynnik plonu-zrodla poza KS - zboze x (1 - udzial chleba), skory x (1 - udzial skory), plony listy typu x c
        /// za rude kopalni (jedzenie x c jedzenia); 1 - nic.</summary>
        internal static float HostFactor(Village v, ItemObject item)
        {
            if (item == null || !GoodsLive(v)) return 1f;
            try
            {
                var g = GoodsOf(v);
                float f = 1f;
                if (ReferenceEquals(item, _grain) && g.BreadS > 0f) f *= 1f - g.BreadS;
                if (ReferenceEquals(item, _hides) && g.LeatherS > 0f) f *= 1f - g.LeatherS;   // 338d: bez futer
                // 338d (recenzja): ciecie za rude tylko w plonach listy typu (CutSet) - tych samych, z ktorych policzono zdjeta wartosc (dodane = zdjete)
                if (g.Ore != null && g.CutSet != null && g.CutSet.Contains(item)) f *= IsFood(item) ? g.FoodC : g.OtherC;
                return f > 0f && !float.IsNaN(f) ? f : 1f;
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GVGoods, "Village.HostFactor", e); return 1f; }
        }

        /// <summary>VillageFruit.CapPostfix: magazyn wsi + 5 dob chleba i skory (pod KS - stawka; poza KS - baza) i rud spoza listy typu poza KS (pod KS liczy CapOverride z A).</summary>
        internal static int CapAdd(Village v)
        {
            if (v == null || !Buildings338.VillageGoodsOn || _bkModelActive || v.VillageState != Village.VillageStates.Normal || v.TradeBound == null) return 0;
            double x = 0;
            if (StartCalibration.FullFor(v))
            {
                KsGood k;
                lock (Gate) { if (!_ksg.TryGetValue(v, out k)) k = null; }
                if (k != null) x = k.Bread + k.Leather;
            }
            else
            {
                if (v.VillageType == null) return 0;
                var g = GoodsOf(v);
                x = g.BreadBase + g.LeatherBase;
                if (g.Ore != null)
                    foreach (var kv in g.Ore)
                    {
                        bool inType = false;
                        foreach (var p in v.VillageType.Productions) if (ReferenceEquals(p.Item1, kv.Key)) { inType = true; break; }
                        if (!inType) x += kv.Value;
                    }
            }
            if (!(x > 0) || double.IsNaN(x)) return 0;
            return (int)Math.Ceiling(5 * x - 1e-4);
        }

        /// <summary>Linia doby: suma towarow wsi 338c (pod KS - z ostatnich stawek A, poza KS - bazy dnia).</summary>
        private static string GoodsLine()
        {
            int kb = 0, kl = 0, ko = 0, kn = 0, ob = 0, ol = 0, oo = 0, on = 0;
            double kB = 0, kG = 0, kL = 0, kS = 0, kO = 0, kV = 0, kC = 0, oB = 0, oL = 0, oV = 0;
            lock (Gate)
            {
                foreach (var kv in _ksg)
                {
                    if (!StartCalibration.FullFor(kv.Key)) continue;   // 338d (recenzja): wpisy wsi, ktore wypadly z KS (drop krainy) - poza linia
                    var g = kv.Value;
                    if (g.Bread > 0f) { kb++; kB += g.Bread; kG += g.GrainBack; }
                    if (g.Leather > 0f) { kl++; kL += g.Leather; kS += g.HidesBack; }
                    if (g.OreAdd > 0f) { ko++; kO += g.OreAdd; kV += g.OreVal; kC += g.CutVal; }
                    if (g.NoSrc) kn++;
                }
                foreach (var g in _vg.Values)
                {
                    if (g.BreadBase > 0f) { ob++; oB += g.BreadBase; }
                    if (g.LeatherBase > 0f) { ol++; oL += g.LeatherBase; }
                    if (g.Ore != null) { oo++; oV += g.OreVal; }
                    if (g.NoSrc) on++;
                }
                _vg.Clear();
            }
            var inv = Buildings338.Inv;
            return " | chleb: wsi KS " + kb + " (+" + kB.ToString("0.0", inv) + " szt./d, zboza -" + kG.ToString("0.0", inv) + "), poza KS " + ob + " (baza +" + oB.ToString("0.0", inv) + ")"
                   + " | skora: wsi KS " + kl + " (+" + kL.ToString("0.0", inv) + " szt./d, skor -" + kS.ToString("0.0", inv) + "), poza KS " + ol + " (baza +" + oL.ToString("0.0", inv) + "), bez surowca " + (kn + on)
                   + " | rudy kopalni wsi BK: wsi KS " + ko + " (+" + kO.ToString("0.0", inv) + " szt./d, wartosc +" + kV.ToString("0", inv) + " zl, inne plony -" + kC.ToString("0", inv) + " zl), poza KS " + oo
                   + " (wartosc " + oV.ToString("0", inv) + " zl)";
        }

        internal static void Daily(int day)
        {
            int nR, nP, nS; double r, pKs, pOut, s;
            lock (Gate)
            {
                nR = _roadsAdd.Count; r = 0; foreach (var x in _roadsAdd.Values) r += x;
                nP = _prodAddKs.Count; pKs = 0; foreach (var x in _prodAddKs.Values) pKs += x;
                pOut = 0; foreach (var d in _prodAdd.Values) foreach (var x in d.Values) pOut += x; _prodAdd.Clear(); _factor.Clear();
                nS = _skeps.Count; s = 0; foreach (var x in _skeps.Values) s += x; _skeps.Clear();
            }
            int vb = 0, fa = 0, mi = 0, an = 0, sa = 0, fi = 0, pr = 0;
            if (Buildings338.VillageProdOn && !_bkModelActive)
                foreach (var v in Village.All)
                {
                    try
                    {
                        var vd = VData(v);
                        if (vd == null) continue;
                        int a1 = Buildings338.VillageLevel(vd, _farming), a2 = Buildings338.VillageLevel(vd, _mining), a3 = Buildings338.VillageLevel(vd, _animal),
                            a4 = Buildings338.VillageLevel(vd, _sawmill), a5 = Buildings338.VillageLevel(vd, _fishFarm);
                        if (a1 > 0) fa++; if (a2 > 0) mi++; if (a3 > 0) an++; if (a4 > 0) sa++; if (a5 > 0) fi++;
                        if (a1 + a2 + a3 + a4 + a5 > 0) vb++;
                        if (Buildings338.VillageProjectOn && Buildings338.VillageCurrentId(vd) == ProductionId) pr++;
                    }
                    catch (Exception e) { Buildings338.Stumble(Buildings338.GVProd, "Village.Daily", e); }
                }
            var inv = Buildings338.Inv;
            // recenzja 338 W4: srednie ceny zboza i drewna na targach miast (porownanie z biegiem bazowym w sprawdz_logi B338-16)
            double pg = 0, pw = 0; int ng = 0;
            try
            {
                var grain = MBObjectManager.Instance.GetObject<ItemObject>("grain");
                var wood = MBObjectManager.Instance.GetObject<ItemObject>("hardwood");
                if (grain != null && wood != null)
                    foreach (var t in Town.AllTowns)
                    {
                        if (t == null || t.MarketData == null) continue;
                        pg += t.MarketData.GetPrice(grain); pw += t.MarketData.GetPrice(wood); ng++;
                    }
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GVProd, "Village.Prices", e); }
            Log.Info("Budynki (338) drogi: dzien " + day + " | wsi KS z drogami " + nR + " | dopisane " + r.ToString("0.0", inv) + " szt./d" + Buildings338.Tail(Buildings338.GRoads));
            Log.Info("Budynki (338) produkcja wsi: dzien " + day + " | wsi z budynkami " + vb + " (Farming " + fa + ", Mining " + mi + ", AnimalHousing " + an + ", Sawmill " + sa + ", FishFarm " + fi
                     + "), projekt Production " + pr + " | KS wsi " + nP + " dopisane " + pKs.ToString("0.0", inv) + " szt./d, poza KS " + pOut.ToString("0.0", inv) + " szt. | model BK czynny "
                     + Buildings338.YN(_bkModelActive) + " | ceny miast: zboze " + (ng > 0 ? pg / ng : 0).ToString("0.0", inv) + ", drewno " + (ng > 0 ? pw / ng : 0).ToString("0.0", inv)
                     + Buildings338.Tail(Buildings338.GVProd));
            Log.Info("Budynki (338) towary wsi: dzien " + day + " | wsi z ulami " + nS + ", miod +" + s.ToString("0.0", inv) + " (baza przed mnoznikami)" + GoodsLine()
                     + Buildings338.Tail(Buildings338.GVGoods));
        }
    }
}
