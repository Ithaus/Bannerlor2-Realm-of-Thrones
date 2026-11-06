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
    /// 11 (Buildings338VillageGoods) UL (Bee Skeps): waga miodu BK 0.5 + 1 x poziom ula (drwale i trapperzy) w VillageFruit.Base (wies poza KS). Chleb (Bakery, Mill)
    /// i skora (Fur Tannery) - ODLOZONE: wymagaja przesuniecia rak z plonu zrodlowego takze w stawce KS (inaczej towar z niczego) - osobny projekt.
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
            lock (Gate) { _roadsAdd.Clear(); _prodAddKs.Clear(); _skeps.Clear(); _prodAdd.Clear(); _factor.Clear(); }
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
            if (!roads && !prod) return;
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
            Log.Info("Budynki (338) towary wsi: dzien " + day + " | wsi z ulami " + nS + ", miod +" + s.ToString("0.0", inv) + " (baza przed mnoznikami) | chleb i skora: odlozone"
                     + Buildings338.Tail(Buildings338.GVGoods));
        }
    }
}
