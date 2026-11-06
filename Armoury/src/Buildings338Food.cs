using System;
using System.Collections.Generic;
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
    /// dokladnie (1000 x poziom), efekt FoodStock obu budynkow x 10 (perka Battlements +100 bez skali, czynniki np. Contractors z baza; skala BK: spichlerz 1000/poziom = 10 x 100 gry; sufit BK to 2 x
    /// ludnosc, wiec +100..500 byloby niewidoczne). Magazyn 1-3: +1000/3000/5000, spichlerz bez zmian (+1000/2000/3000) + perki x10. Bez danych BK gra liczy sama.
    /// 3b (Buildings338FoodDouble, tylko razem z 3a) DUBEL BK 'Local farmland': BKFoodModel.GetPopulationFoodProduction:192-201 dodaje pierwszemu z CastleFarmlands /
    /// SettlementWarehouse czynnik poziom x 0.5 (zamek) / 0.3 (miasto) produkcji ludnosci - opis tego nie obiecuje (Farmlands ma swoje +6/12/18 w modelu gry, ktore
    /// dziala; magazyn ma zapas z 3a). Postfiks na tej metodzie dodaje ten sam czynnik ze znakiem minus (czynniki ExplainedNumber sie sumuja, po nim BK dodaje tylko
    /// czynniki praw - skasowanie jest dokladne). 'Local farmland' (praca ludnosci) liczy sie dalej, tylko bez premii budynku.
    /// Linia doby mierzy, ile osad schodzi z bilansem jedzenia ponizej zera przez 3b (sprawdz_logi: najwyzej 5% osad z tymi budynkami).
    /// 338c (DECYZJA po tescie 30 dob at338: zdjecie premii = ok. 10 100 jedzenia/d mniej, osad z bilansem &lt; 0 82 -&gt; 172 z 219 - glod): klucz Buildings338FoodDouble
    /// domyslnie NIE. Premia BK 'Local farmland' za Farmlands i Warehouse to sposob, w jaki BK skalibrowal swoj model jedzenia (efekt "jedzenie" zgodny z duchem
    /// opisu farm); +6/12/18 gry dla Farmlands i zapas magazynu/spichlerza (3a) zostaja. Przy kluczu NIE linia podaje tylko liczbe osad z budynkami.
    /// </summary>
    internal static class Buildings338Food
    {
        private const float StockScale = 10f;
        [ThreadStatic] private static bool _skipStock, _skipDouble;
        private static bool _hookedStock, _hookedDouble;
        private static TextObject _txt;
        private static readonly object Gate = new object();
        private static readonly Dictionary<Town, int> _stock = new Dictionary<Town, int>();   // recenzja 338 (wydajnosc): zmiana sufitu na dobe (czyszczona w Daily)

        internal static void SessionStart()
        {
            lock (Gate) _stock.Clear();
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
                int d;
                lock (Gate) { if (_stock.TryGetValue(__instance, out d)) { __result += d; return; } }
                d = StockDelta(__instance);
                lock (Gate) _stock[__instance] = d;
                __result += d;
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GStock, "Food.StockPostfix", e); }
        }

        /// <summary>Zmiana sufitu 338 (0 - BK nie przejal albo brak budynkow): - czlon BK spichlerza + budynki x10 (perka Battlements +100 bez skali - recenzja 338).</summary>
        private static int StockDelta(Town t)
        {
            if (Buildings338.PopData(t.Settlement) == null) return 0;   // BK nie przejal - gra liczy budynki sama
            int gran = 0;
            double add = 0;
            var model = Campaign.Current.Models.BuildingEffectModel;
            foreach (var b in t.Buildings)
            {
                if (b == null || b.CurrentLevel <= 0 || b.BuildingType == null) continue;
                if (b.BuildingType == DefaultBuildingTypes.CastleGranary) gran += 1000 * b.CurrentLevel;   // czlon BK (SettlementPatches:108-114)
                if (!b.BuildingType.HasEffect(BuildingEffectEnum.FoodStock) || (b.BuildingType.IsDailyProject && t.CurrentDefaultBuilding != b)) continue;
                var en = model.GetBuildingEffect(b, BuildingEffectEnum.FoodStock);
                float bas = b.BuildingType.GetBaseBuildingEffectAmount(BuildingEffectEnum.FoodStock, b.CurrentLevel);
                // efekt = (baza + Battlements) x (1 + czynniki, np. Contractors); skala BK x10 tylko dla bazy budynku (z jej czynnikami), dodatek perki bez skali
                add += en.ResultNumber + (StockScale - 1f) * bas * (1f + en.SumOfFactors);
            }
            if (!(add > 0) && gran == 0) return 0;
            return (int)Math.Max(0.0, add) - gran;
        }

        // ------------------------------------------------------------ 3b dubel BK
        public static void DoublePostfix(Town __1, ref ExplainedNumber __result)
        {
            if (_skipDouble || __1 == null || !Buildings338.FoodDoubleOn || __1.IsUnderSiege) return;   // BK dodaje czynnik tylko poza oblezeniem
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
            lock (Gate) _stock.Clear();
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
            bool dbl = Buildings338.FoodDoubleOn && _hookedDouble;
            if (!dbl)   // 338c: klucz domyslnie NIE - premia BK zostaje (tylko liczba osad z budynkami do linii)
                foreach (var t in Town.AllFiefs)
                {
                    try
                    {
                        foreach (var b in t.Buildings)
                            if (b != null && (b.BuildingType == DefaultBuildingTypes.CastleFarmlands || b.BuildingType == DefaultBuildingTypes.SettlementWarehouse))
                            { if (b.CurrentLevel > 0) { if (t.IsCastle) nF++; else nM++; } break; }
                    }
                    catch (Exception e) { Buildings338.Stumble(Buildings338.GDouble, "Food.Daily(3b)", e); }
                }
            if (dbl)
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
                        float a = fm.CalculateTownFoodStocksChange(t, false, false).ResultNumber, b0;   // bez rynku - sam skutek zdjecia premii BK (recenzja 338)
                        bool was = _skipDouble; _skipDouble = true;
                        try { b0 = fm.CalculateTownFoodStocksChange(t, false, false).ResultNumber; } finally { _skipDouble = was; }
                        gone += b0 - a;
                        if (b0 < 0f) negA++;
                        if (a < 0f) negB++;
                    }
                    catch (Exception e) { Buildings338.Stumble(Buildings338.GDouble, "Food.Daily(3b)", e); }
                }
            }
            Log.Info("Budynki (338) dubel jedzenia BK: dzien " + day + " | klucz " + Buildings338.YN(dbl) + (dbl ? " (premia BK zdjeta)" : " (premia BK produkcji jedzenia zostaje - decyzja 338c)") + " | zamkow z Farmlands " + nF + ", miast z Warehouse " + nM + " | zdjeta premia " + gone.ToString("0", Buildings338.Inv)
                     + " jedzenia/d | osad z bilansem < 0: " + negA + " -> " + negB + " z " + all + " osad" + Buildings338.Tail(Buildings338.GDouble));
        }
    }
}
