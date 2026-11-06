using System;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 338 grupa 3 - JEDZENIE.
    /// 3a (Buildings338FoodStock) ZAPAS: jedyny czytnik FoodStock to Town.FoodStocksUpperLimit:467; BK SettlementPatches.FoodStockPatch (prefiks, return false przy
    /// danych BK) liczy 2 x ludnosc + 500/250 + 1000 x poziom TYLKO dla CastleGranary - magazyn miasta (Warehouse +100/300/500) nie podnosi sufitu wcale, a perki
    /// Battlements/Contractors gina. Naprawa: postfiks Last (biegnie mimo return false BK), tylko gdy BK przejal wynik (dane BK osady): czlon BK spichlerza zdjety
    /// dokladnie (1000 x poziom), jedno AddEffectOfBuildings(FoodStock) dla obu budynkow x 10 (skala BK: spichlerz 1000/poziom = 10 x 100 gry; sufit BK to 2 x
    /// ludnosc, wiec +100..500 byloby niewidoczne). Magazyn 1-3: +1000/3000/5000, spichlerz bez zmian (+1000/2000/3000) + perki x10. Bez danych BK gra liczy sama.
    /// 3b (Buildings338FoodDouble, tylko razem z 3a) DUBEL BK 'Local farmland': BKFoodModel.GetPopulationFoodProduction:192-201 dodaje pierwszemu z CastleFarmlands /
    /// SettlementWarehouse czynnik poziom x 0.5 (zamek) / 0.3 (miasto) produkcji ludnosci - opis tego nie obiecuje (Farmlands ma swoje +6/12/18 w modelu gry, ktore
    /// dziala; magazyn ma zapas z 3a). Postfiks na tej metodzie dodaje ten sam czynnik ze znakiem minus (czynniki ExplainedNumber sie sumuja, po nim BK dodaje tylko
    /// czynniki praw - skasowanie jest dokladne). 'Local farmland' (praca ludnosci) liczy sie dalej, tylko bez premii budynku.
    /// Linia doby mierzy, ile osad schodzi z bilansem jedzenia ponizej zera przez 3b (sprawdz_logi: najwyzej 5% osad z tymi budynkami).
    /// </summary>
    internal static class Buildings338Food
    {
        private const float StockScale = 10f;
        [ThreadStatic] private static bool _skipStock, _skipDouble;
        private static bool _hookedStock, _hookedDouble;
        private static TextObject _txt;

        internal static void SessionStart()
        {
            if (!_hookedStock)
            {
                var m = AccessTools.Method(typeof(Town), "FoodStocksUpperLimit");
                if (m != null && m.ReturnType == typeof(int))
                {
                    Buildings338.H.Patch(m, postfix: new HarmonyMethod(typeof(Buildings338Food), nameof(StockPostfix)) { priority = Priority.Last });
                    _hookedStock = true;
                }
            }
            Buildings338.Hook("zapas jedzenia: " + (_hookedStock ? "Town.FoodStocksUpperLimit wpiety" : "BRAK Town.FoodStocksUpperLimit"));
            if (!_hookedDouble)
            {
                var t = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKFoodModel");
                var m = t != null ? AccessTools.Method(t, "GetPopulationFoodProduction") : null;
                if (m != null && m.DeclaringType == t && m.ReturnType == typeof(ExplainedNumber) && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(Town))
                {
                    Buildings338.H.Patch(m, postfix: new HarmonyMethod(typeof(Buildings338Food), nameof(DoublePostfix)) { priority = Priority.Last });
                    _hookedDouble = true;
                }
            }
            Buildings338.Hook("dubel jedzenia BK: " + (_hookedDouble ? "BKFoodModel.GetPopulationFoodProduction wpiety" : "BRAK BKFoodModel.GetPopulationFoodProduction"));
        }

        // ------------------------------------------------------------ 3a zapas
        public static void StockPostfix(Town __instance, ref int __result)
        {
            if (_skipStock || __instance == null || !Buildings338.FoodStockOn) return;
            try
            {
                if (Buildings338.PopData(__instance.Settlement) == null) return;   // BK nie przejal - gra liczy budynki sama
                int gran = 0;
                foreach (var b in __instance.Buildings)
                    if (b != null && b.CurrentLevel > 0 && b.BuildingType == DefaultBuildingTypes.CastleGranary) gran += 1000 * b.CurrentLevel;   // czlon BK (SettlementPatches:108-114)
                var e = new ExplainedNumber(0f);
                __instance.AddEffectOfBuildings(BuildingEffectEnum.FoodStock, ref e);
                float add = StockScale * e.ResultNumber;
                if (!(add > 0f) && gran == 0) return;
                __result = __result - gran + (int)Math.Max(0f, add);
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GStock, "Food.StockPostfix", e); }
        }

        // ------------------------------------------------------------ 3b dubel BK
        public static void DoublePostfix(Town __1, ref ExplainedNumber __result)
        {
            if (_skipDouble || __1 == null || !Buildings338.FoodDoubleOn) return;
            try
            {
                Building val = null;
                foreach (var b in __1.Buildings)   // jak BK: pierwszy z CastleFarmlands / SettlementWarehouse
                    if (b != null && (b.BuildingType == DefaultBuildingTypes.CastleFarmlands || b.BuildingType == DefaultBuildingTypes.SettlementWarehouse)) { val = b; break; }
                if (val == null || val.CurrentLevel <= 0) return;
                if (_txt == null) _txt = new TextObject("{=!}Building bonus not in its description (removed)");
                __result.AddFactor(-(val.CurrentLevel * (__1.IsCastle ? 0.5f : 0.3f)), _txt);
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GDouble, "Food.DoublePostfix", e); }
        }

        internal static void Daily(int day)
        {
            // 3a
            int nW = 0, nG = 0; long addW = 0, addG = 0, bkG = 0;
            string ex = "-";
            if (Buildings338.FoodStockOn)
                foreach (var t in Town.AllFiefs)
                {
                    try
                    {
                        int w = t.IsTown ? Buildings338.Level(t, DefaultBuildingTypes.SettlementWarehouse.StringId) : 0;
                        int g = t.IsCastle ? Buildings338.Level(t, DefaultBuildingTypes.CastleGranary.StringId) : 0;
                        if (w <= 0 && g <= 0) continue;
                        int after = t.FoodStocksUpperLimit(), before;
                        bool was = _skipStock; _skipStock = true;
                        try { before = t.FoodStocksUpperLimit(); } finally { _skipStock = was; }
                        if (w > 0) { nW++; addW += after - before; if (ex == "-") ex = t.Name + " sufit " + before + " -> " + after; }
                        if (g > 0) { nG++; addG += after - before + 1000L * g; bkG += 1000L * g; }
                    }
                    catch (Exception e) { Buildings338.Stumble(Buildings338.GStock, "Food.Daily", e); }
                }
            Log.Info("Budynki (338) zapas jedzenia: dzien " + day + " | miast z Warehouse " + nW + " (sufit +" + addW + "), zamkow z Granary " + nG + " (sufit +" + addG + ", BK bylo +" + bkG + ")"
                     + " | przyklad " + ex + Buildings338.Tail(Buildings338.GStock));
            // 3b
            int nF = 0, nM = 0, all = 0, negA = 0, negB = 0; double gone = 0;
            if (Buildings338.FoodDoubleOn && _hookedDouble)
            {
                var fm = Campaign.Current.Models.SettlementFoodModel;
                foreach (var t in Town.AllFiefs)
                {
                    try
                    {
                        Building val = null;
                        foreach (var b in t.Buildings)
                            if (b != null && (b.BuildingType == DefaultBuildingTypes.CastleFarmlands || b.BuildingType == DefaultBuildingTypes.SettlementWarehouse)) { val = b; break; }
                        if (val == null || val.CurrentLevel <= 0 || Buildings338.PopData(t.Settlement) == null) continue;
                        all++;
                        if (t.IsCastle) nF++; else nM++;
                        float a = fm.CalculateTownFoodStocksChange(t, true, false).ResultNumber, b0;
                        bool was = _skipDouble; _skipDouble = true;
                        try { b0 = fm.CalculateTownFoodStocksChange(t, true, false).ResultNumber; } finally { _skipDouble = was; }
                        gone += b0 - a;
                        if (b0 < 0f) negA++;
                        if (a < 0f) negB++;
                    }
                    catch (Exception e) { Buildings338.Stumble(Buildings338.GDouble, "Food.Daily(3b)", e); }
                }
            }
            Log.Info("Budynki (338) dubel jedzenia BK: dzien " + day + " | zamkow z Farmlands " + nF + ", miast z Warehouse " + nM + " | zdjeta premia " + gone.ToString("0", Buildings338.Inv)
                     + " jedzenia/d | osad z bilansem < 0: " + negA + " -> " + negB + " z " + all + " osad" + Buildings338.Tail(Buildings338.GDouble));
        }
    }
}
