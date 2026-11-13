using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 476-e (projekt docs/PROJEKT-BUDYNKI-EFEKTY-2026-11-13.md B5; Jeff 13.11: "Wyjasnij mi [Farmlands] dokladniej i na liczbach", potem "na pozostale sie zgadzam") -
    /// FARMLANDS ZAMKU = POLA DWORSKIE (PANSZCZYZNA) zamiast jedzenia z niczego.
    /// Dzis zamek z Farmlands L dostaje: 15% produkcji ludnosci BK ("Local farmland", BKFoodModel.CalculateTownFoodChangeInternal) liczonej Z premia BK +0.5 x L
    /// (BKFoodModel.GetPopulationFoodProduction:39425 - te same rece, wiecej plonu = ok. 1500/3000/4500 "nieistniejacych parobkow") oraz +6/12/18 gry (FoodProduction
    /// budynku). Nowa zasada: budynek nie tworzy jedzenia, tylko przesuwa czesc pracy chlopow z ich zagonow na pole pana - zamek dostaje 20/25/30% (L1/L2/L3)
    /// produkcji ludnosci BEZ premii budynku; +6/12/18 gry zdjete; chlopi placa robocizna - lojalnosc -0.1/-0.2/-0.3 na dobe (jak BK "SerfsAgricultureDuties").
    /// Postfiks na BKFoodModel.CalculateTownFoodChangeInternal (tylko zamki z Farmlands &gt; 0): roznica "Lord's fields (corvee)" = udzial x PFP bez premii - 0.15 x PFP
    /// (z premia) - efekt FoodProduction budynku. Lojalnosc - postfiks CalculateLoyaltyChange czynnego modelu (PatchTarget.OfActive). Miasta (Warehouse x0.3) bez zmian.
    /// Klucz Buildings338 Farmlands Corvee (TAK); NIE - co do bitu jak przed. Linia doby: zamki wg poziomu Farmlands, suma zmiany jedzenia, zamki z bilansem &lt; 0.
    /// </summary>
    internal static class Buildings338Corvee
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.Buildings338 && s.Buildings338FarmlandsCorvee; } }
        private static readonly HashSet<MethodInfo> _patched = new HashSet<MethodInfo>();
        private static RuntimeMethodHandle _loyActive;
        private static MethodInfo _gpfp;
        private static bool _foodWired, _loyWired;
        private static TextObject _txt, _txtLoy;
        private static int _stumbles;
        [ThreadStatic] private static bool _inside;

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles);
            if (_stumbles <= 3) { try { Log.Error("Buildings338Corvee." + where, e); } catch { } }
        }

        internal static void SessionStart()
        {
            _stumbles = 0;
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKFoodModel");
                var m = t != null ? AccessTools.DeclaredMethod(t, "CalculateTownFoodChangeInternal", new[] { typeof(Town), typeof(bool), typeof(bool) }) : null;
                _gpfp = t != null ? AccessTools.DeclaredMethod(t, "GetPopulationFoodProduction") : null;
                if (m != null && _gpfp != null)
                {
                    if (_patched.Add(m)) Buildings338.H.Patch(m, postfix: new HarmonyMethod(typeof(Buildings338Corvee), nameof(FoodPostfix)));
                    _foodWired = true;
                }
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SettlementLoyaltyModel : null;
                var ml = model != null ? PatchTarget.OfActive(model, "CalculateLoyaltyChange", new[] { typeof(Town), typeof(bool) }, "panszczyzna (476-e)") : null;
                if (ml != null)
                {
                    if (_patched.Add(ml)) Buildings338.H.Patch(ml, postfix: new HarmonyMethod(typeof(Buildings338Corvee), nameof(LoyaltyPostfix)));
                    _loyActive = ml.MethodHandle; _loyWired = true;
                }
            }
            catch (Exception e) { Stumble("SessionStart", e); }
            Buildings338.Hook("panszczyzna Farmlands (476-e): jedzenie " + (_foodWired ? "tak" : "BRAK") + ", lojalnosc " + (_loyWired ? "tak" : "BRAK"));
        }

        private static float GameFood(Building b)
        {
            var en = new ExplainedNumber(0f, false, null);
            b.AddEffectOfBuilding(BuildingEffectEnum.FoodProduction, ref en);
            return en.ResultNumber;
        }

        private static float Share(int l) { return l >= 3 ? 0.30f : l == 2 ? 0.25f : 0.20f; }

        /// <summary>Zmiana jedzenia zamku przez panszczyzne (0 - nie dotyczy). __instance - BKFoodModel.</summary>
        private static float Delta(object model, Town town, out int lvl)
        {
            lvl = 0;
            if (town == null || !town.IsCastle) return 0f;
            // 476-p1 (recenzje A1 / B1): w oblezeniu BK nie daje produkcji ludnosci (bk_all.cs:39383), a gra nie dodaje FoodProduction budowli - nie ma czego
            // przesuwac ani odejmowac (dotad -6/-12/-18 na dobe jedzenia, ktorego zamek nie dostal - krotsza obrona 471b)
            if (town.IsUnderSiege) return 0f;
            var b = Buildings338.FindBuilding(town, "building_castle_farmlands");
            lvl = b != null ? b.CurrentLevel : 0;
            if (lvl <= 0) return 0f;
            var pop = Buildings338.PopData(town.Settlement);
            if (pop == null || _gpfp == null) return 0f;
            var en = (ExplainedNumber)_gpfp.Invoke(model, new object[] { pop, town });
            float r = en.ResultNumber;
            if (!(r > 0f)) return -GameFood(b);
            bool bonusIn = !Buildings338.FoodDoubleOn;   // 3b NIE (u Jeffa) - premia BK +0.5 x L jest w wyniku
            float noBonus = bonusIn ? Math.Max(0f, r - en.BaseNumber * 0.5f * lvl) : r;
            float game = GameFood(b);
            return Share(lvl) * noBonus - 0.15f * r - game;
        }

        public static void FoodPostfix(object __instance, Town __0, ref ExplainedNumber __result)
        {
            if (_inside) return;
            try
            {
                if (!On || __0 == null || !__0.IsCastle) return;
                _inside = true;
                int lvl;
                float d = Delta(__instance, __0, out lvl);
                if (lvl <= 0 || float.IsNaN(d) || float.IsInfinity(d) || Math.Abs(d) < 0.001f) return;
                if (_txt == null) _txt = new TextObject("Lord's fields (corvee)");
                __result.Add(d, _txt);
            }
            catch (Exception e) { Stumble("FoodPostfix", e); }
            finally { _inside = false; }
        }

        public static void LoyaltyPostfix(MethodBase __originalMethod, Town __0, ref ExplainedNumber __result)
        {
            try
            {
                if (!On || __0 == null || !__0.IsCastle || __originalMethod == null || !__originalMethod.MethodHandle.Equals(_loyActive)) return;
                int l = Buildings338.Level(__0, "building_castle_farmlands");
                if (l <= 0) return;
                if (_txtLoy == null) _txtLoy = new TextObject("Corvee on the lord's fields");
                __result.Add(-0.1f * l, _txtLoy);
            }
            catch (Exception e) { Stumble("LoyaltyPostfix", e); }
        }

        internal static void Daily(int day)
        {
            if (!On || Campaign.Current == null) return;
            try
            {
                int l1 = 0, l2 = 0, l3 = 0, neg = 0, n = 0;
                double sum = 0;
                var fm = Campaign.Current.Models.SettlementFoodModel;
                foreach (var t in Town.AllCastles)
                {
                    try
                    {
                        int l = Buildings338.Level(t, "building_castle_farmlands");
                        if (l <= 0) continue;
                        n++; if (l == 1) l1++; else if (l == 2) l2++; else l3++;
                        _inside = true;
                        int lv; float d;
                        try { d = Delta(fm, t, out lv); } finally { _inside = false; }
                        sum += d;
                        if (fm.CalculateTownFoodStocksChange(t, true, false).ResultNumber < 0f) neg++;
                    }
                    catch (Exception e) { Stumble("Daily(zamek)", e); }
                }
                Log.Info("Budynki (338) panszczyzna Farmlands (476-e): dzien " + day + " | latki: jedzenie " + (_foodWired ? "tak" : "NIE") + ", lojalnosc " + (_loyWired ? "tak" : "NIE")
                         + " | zamkow z Farmlands " + n + " (L1 " + l1 + ", L2 " + l2 + ", L3 " + l3 + ") | zmiana jedzenia zamkow wobec dawnej premii " + sum.ToString("0.0", Buildings338.Inv)
                         + "/d | zamkow z Farmlands z bilansem jedzenia < 0: " + neg + " | bledy " + _stumbles);
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
