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
    /// 476-c (Jeff 13.11: "Teatr tak zwieksza lojalnosc, i nazwij to swiatynia"; "courthouse [BK] wylacz, a courthouse ktory zostaje daje tez wplywy i lojalnosc
    /// mieszkancow"; "Dodaj budynek wiary"; projekt docs/PROJEKT-BUDYNKI-EFEKTY-2026-11-13.md czesc C) - SWIATYNIA, SAD, MIEJSCE KULTU.
    ///  (1) BK Theater (bk_building_theater): nazwa w grze "Temple"; efekt bez zmian (BK: lojalnosc i waga kultury pana).
    ///  (2) Sad gry (building_settlement_courthouse): JUZ daje wplywy +0.2/0.5/1 i lojalnosc +0.3/0.6/1 na dobe (DefaultBuildingTypes:234-237; BKLoyaltyModel:283
    ///      AddEffectOfBuildings(Loyalty)) - Jeff chcial "wplywy i lojalnosc mieszkancow": jest, bez dopisku (bez podwojnego liczenia). Tylko linia doby liczy sady.
    ///  (3) MIEJSCE KULTU w slocie BK Court House (bk_building_courthouse - jest w zapisach, BK daje tylko stabilnosc +0.02/poziom; AI go nie stawialo od 473-b6):
    ///      nazwa "Place of Worship" (sept, godswood, sala Utopionego Boga albo swiatynia R'hllora - wedlug wiary pana; opis w grze). Efekty wedlug projektu:
    ///      E1 waga wiary pana w osadzie +10 x poziom (postfiks BKReligionModel.CalculateReligionWeight - dzis pan daje +5, notable 25 x udzial wladzy);
    ///      E2 lojalnosc +0.25 x poziom na dobe, gdy wiara pana jest wiara panujaca osady. E3 (napiecie religijne BK, dzis zawsze 0) - NIE ruszane.
    ///      AI stawia go tylko, gdy wiara pana nie panuje w osadzie albo lojalnosc &lt; 60 (decyzja domyslna noc 13.11 - reszta jak 473-b6: AI nie stawia).
    ///      Platnik: oplacona praca budowy pana (BuildFunding) jak kazda budowla - decyzja domyslna (noc 13.11): dziesiecina "budowa septow" bez zmian (osobny krok).
    /// Klucz Buildings338 Temples And Worship (TAK); NIE - nazwy i efekty jak przed, AI jak 473-b6.
    /// </summary>
    internal static class Buildings338Civic
    {
        internal const string TheaterId = "bk_building_theater", CourtGameId = "building_settlement_courthouse", WorshipId = "bk_building_courthouse";
        internal static bool On { get { var s = Settings.Current; return s != null && s.Buildings338 && s.Buildings338Worship; } }

        private static readonly HashSet<MethodInfo> _patched = new HashSet<MethodInfo>();
        private static RuntimeMethodHandle _loyActive, _relActive;
        private static bool _loyWired, _relWired, _renamed;
        private static PropertyInfo _cfgRelMgr; private static MethodInfo _getHeroRel; private static PropertyInfo _popRelData, _rdDominant;
        private static TextObject _txtWorship, _txtWorshipW;
        private static int _cLoy, _cRel, _stumbles, _aiAllowed, _aiBlocked;

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles);
            if (_stumbles <= 3) { try { Log.Error("Buildings338Civic." + where, e); } catch { } }
        }

        internal static void SessionStart()
        {
            _cLoy = _cRel = _stumbles = _aiAllowed = _aiBlocked = 0;
            string loy = "BRAK", rel = "BRAK", ren = "nie";
            try
            {
                var tCfg = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                _cfgRelMgr = tCfg != null ? AccessTools.Property(tCfg, "ReligionsManager") : null;
                var tRm = _cfgRelMgr != null ? _cfgRelMgr.PropertyType : null;
                _getHeroRel = tRm != null ? AccessTools.Method(tRm, "GetHeroReligion", new[] { typeof(Hero) }) : null;
                var tPm = AccessTools.TypeByName("BannerKings.Managers.PopulationManager");
                var gpd = tPm != null ? AccessTools.Method(tPm, "GetPopData", new[] { typeof(Settlement) }) : null;
                _popRelData = gpd != null ? AccessTools.Property(gpd.ReturnType, "ReligionData") : null;
                _rdDominant = _popRelData != null ? AccessTools.Property(_popRelData.PropertyType, "DominantReligion") : null;
            }
            catch (Exception e) { Stumble("BK", e); }
            try
            {
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SettlementLoyaltyModel : null;
                var m = model != null ? PatchTarget.OfActive(model, "CalculateLoyaltyChange", new[] { typeof(Town), typeof(bool) }, "sad i miejsce kultu (476-c)") : null;
                if (m != null)
                {
                    if (_patched.Add(m)) Buildings338.H.Patch(m, postfix: new HarmonyMethod(typeof(Buildings338Civic), nameof(LoyaltyPostfix)));
                    _loyActive = m.MethodHandle; _loyWired = true; loy = m.DeclaringType != null ? m.DeclaringType.Name : "?";
                }
                var tRel = AccessTools.TypeByName("BannerKings.Models.BKModels.BKReligionModel") ?? AccessTools.TypeByName("BKReligionModel");
                if (tRel == null) foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { try { tRel = a.GetType("BannerKings.Models.BKModels.BKReligionModel", false); } catch { } if (tRel != null) break; }
                var mr = tRel != null ? AccessTools.DeclaredMethod(tRel, "CalculateReligionWeight") : null;
                if (mr != null)
                {
                    if (_patched.Add(mr)) Buildings338.H.Patch(mr, postfix: new HarmonyMethod(typeof(Buildings338Civic), nameof(ReligionPostfix)));
                    _relActive = mr.MethodHandle; _relWired = true; rel = mr.DeclaringType != null ? mr.DeclaringType.Name : "?";
                }
            }
            catch (Exception e) { Stumble("Patch", e); }
            try { if (On) { Rename(); ren = _renamed ? "tak" : "nie (brak typu)"; } } catch (Exception e) { Stumble("Rename", e); }
            Buildings338.Hook("swiatynia i miejsce kultu (476-c): lojalnosc " + loy + ", waga wiary " + rel + ", nazwy " + ren);
        }

        private static BuildingType FindType(string id)
        {
            foreach (var t in Town.AllTowns) { var b = Buildings338.FindBuilding(t, id); if (b != null) return b.BuildingType; }
            foreach (var t in Town.AllCastles) { var b = Buildings338.FindBuilding(t, id); if (b != null) return b.BuildingType; }
            return null;
        }

        private static void Rename()
        {
            var pName = AccessTools.Property(typeof(BuildingType), "Name");
            var pExp = AccessTools.Property(typeof(BuildingType), "Explanation");
            var th = FindType(TheaterId);
            if (th != null && pName != null) { pName.SetValue(th, new TextObject("Temple"), null); _renamed = true; }
            var w = FindType(WorshipId);
            if (w != null && pName != null)
            {
                pName.SetValue(w, new TextObject("Place of Worship"), null);
                if (pExp != null) pExp.SetValue(w, new TextObject("A sept, a godswood, a hall of the Drowned God or a temple of R'hllor - after the faith of the lord. The lord's faith gains weight among the townsfolk (+10 per level), and while it is the faith of the town the people are more loyal (+0.25 a day per level)."), null);
                _renamed = true;
            }
        }

        private static object Religion(Hero h)
        {
            if (h == null || _cfgRelMgr == null || _getHeroRel == null) return null;
            var cfgT = _cfgRelMgr.DeclaringType;
            var inst = AccessTools.Property(cfgT, "Instance");
            var cfg = inst != null ? inst.GetValue(null, null) : null;
            var rm = cfg != null ? _cfgRelMgr.GetValue(cfg, null) : null;
            return rm != null ? _getHeroRel.Invoke(rm, new object[] { h }) : null;
        }

        private static object Dominant(Settlement st)
        {
            var pop = Buildings338.PopData(st);
            if (pop == null || _popRelData == null || _rdDominant == null) return null;
            var rd = _popRelData.GetValue(pop, null);
            return rd != null ? _rdDominant.GetValue(rd, null) : null;
        }

        private static Hero Lord(Settlement st) { return st != null && st.OwnerClan != null ? st.OwnerClan.Leader : null; }

        public static void LoyaltyPostfix(MethodBase __originalMethod, Town __0, bool __1, ref ExplainedNumber __result)
        {
            try
            {
                if (!On || __0 == null || __originalMethod == null || !__originalMethod.MethodHandle.Equals(_loyActive)) return;
                int lw = Buildings338.Level(__0, WorshipId);
                if (lw > 0)
                {
                    var r = Religion(Lord(__0.Settlement));
                    if (r != null && ReferenceEquals(r, Dominant(__0.Settlement)))
                    {
                        if (_txtWorship == null) _txtWorship = new TextObject("Place of worship of the town's faith");
                        __result.Add(0.25f * lw, _txtWorship);
                    }
                }
                if (!__1) Interlocked.Increment(ref _cLoy);
            }
            catch (Exception e) { Stumble("LoyaltyPostfix", e); }
        }

        public static void ReligionPostfix(MethodBase __originalMethod, object[] __args, ref ExplainedNumber __result)
        {
            try
            {
                if (!On || __originalMethod == null || !__originalMethod.MethodHandle.Equals(_relActive) || __args == null || __args.Length < 2) return;
                var st = __args[1] as Settlement;
                var town = st != null ? st.Town : null;
                if (town == null) return;
                int lw = Buildings338.Level(town, WorshipId);
                if (lw <= 0) return;
                var r = Religion(Lord(st));
                if (r == null || !ReferenceEquals(r, __args[0])) return;
                if (_txtWorshipW == null) _txtWorshipW = new TextObject("Place of worship");
                __result.Add(10f * lw, _txtWorshipW);
                Interlocked.Increment(ref _cRel);
            }
            catch (Exception e) { Stumble("ReligionPostfix", e); }
        }

        /// <summary>AI: miejsce kultu (slot BK Court House) tylko gdy wiara pana nie panuje w osadzie albo lojalnosc &lt; 60. null - regula nie dotyczy (klucz NIE).</summary>
        internal static bool? AiWantsWorship(Town t)
        {
            if (!On || t == null) return null;
            try
            {
                var r = Religion(Lord(t.Settlement));
                bool want = (r != null && !ReferenceEquals(r, Dominant(t.Settlement))) || t.Loyalty < 60f;
                if (want) _aiAllowed++; else _aiBlocked++;
                return want;
            }
            catch (Exception e) { Stumble("AiWantsWorship", e); return null; }
        }

        internal static void Daily(int day)
        {
            if (!On) return;
            int courts = 0, worship = 0;
            foreach (var t in Town.AllTowns) { if (Buildings338.Level(t, CourtGameId) > 0) courts++; if (Buildings338.Level(t, WorshipId) > 0) worship++; }
            Log.Info("Budynki (338) swiatynia i miejsce kultu (476-c): dzien " + day + " | latki: lojalnosc " + (_loyWired ? "tak" : "NIE") + ", waga wiary " + (_relWired ? "tak" : "NIE") + ", nazwy " + (_renamed ? "tak" : "NIE")
                     + " | miasta z sadem gry " + courts + ", z miejscem kultu " + worship + " | wywolan lojalnosci (tick) " + _cLoy + ", wagi wiary " + _cRel
                     + " | AI miejsce kultu: chce " + _aiAllowed + ", nie " + _aiBlocked + " | bledy " + _stumbles);
            _cLoy = _cRel = _aiAllowed = _aiBlocked = 0;
        }
    }
}
