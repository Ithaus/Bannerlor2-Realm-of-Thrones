using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace Armoury
{
    /// <summary>
    /// PACZKA 338 - BUDYNKI ROBIA TO, CO PISZE ICH OPIS (Jeff 06.11: "zrob audyt budynkow, czy w ogole dzialaja, czy trzeba je przeprogramowac i naprawic,
    /// aby to, co robia, dzialalo"; audyt docs/AUDYT-338-BUDYNKI.md, projekt docs/PROJEKT-338-BUDYNKI.md).
    ///
    /// Rdzen: klucze (wylacznik glowny Buildings338 + klucz grupy), odczyty BK przez refleksje (Armoury nie ma referencji do BannerKings.dll), latki w kampanii
    /// (raz na proces), linia startu sesji i linie doby kazdej grupy ("Budynki (338) <grupa>: dzien N | ... | bledy n"). Zasady wspolne:
    ///  - efekt z opisu budynku, liczby z gry/BK (Town.AddEffectOfBuildings - ta sama droga co gra: poziom 0 pominiety, projekt dzienny tylko biezacy, perki modelu);
    ///  - nic z niczego (zloto z istniejacych kies, towary z rak, konie z koni);
    ///  - jeden system na efekt (straznik "kto juz to liczy" przy kazdej grupie);
    ///  - zadna grupa nie zapisuje stanu w zapisie gry - wylaczenie klucza przywraca stan sprzed 338.
    /// Wyjatek = potkniecie grupy (licznik, pierwszy do Log.Error); bez wylacznika na sesje (CLAUDE.md 7).
    /// </summary>
    internal static class Buildings338
    {
        internal static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static Harmony _h;
        internal static Harmony H { get { if (_h == null) _h = new Harmony("com.jeff.armoury.buildings338"); return _h; } }

        // ------------------------------------------------------------ klucze
        private static Settings S { get { return Settings.Current; } }
        internal static bool Main { get { var s = S; return s != null && s.Buildings338; } }
        internal static bool WageOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338Wage; } }
        internal static bool AutoRecruitOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338AutoRecruit; } }
        internal static bool FoodStockOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338FoodStock; } }
        internal static bool FoodDoubleOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338FoodStock && s.Buildings338FoodDouble; } }   // 3b tylko razem z 3a
        internal static bool MasonOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338Mason; } }
        internal static bool TaxOfficeOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338TaxOffice; } }
        internal static bool CaravansOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338Caravans; } }
        internal static bool AssimilationOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338Assimilation; } }
        internal static bool RoadsOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338Roads; } }
        internal static bool PortsOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338Ports; } }
        internal static bool VillageProdOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338VillageProd; } }
        internal static bool VillageProjectOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338VillageProd && s.Buildings338VillageProject; } }   // recenzja 338 W4: osobny klucz projektu Production
        internal static bool VillageGoodsOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338VillageGoods; } }
        internal static bool StudsOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338Studs; } }
        internal static bool CastleMinesOn { get { var s = S; return s != null && s.Buildings338 && s.Buildings338CastleMines; } }

        internal static string YN(bool b) { return b ? "TAK" : "NIE"; }

        // ------------------------------------------------------------ potkniecia grup (licznik, pierwszy wyjatek do logu)
        internal const int GWage = 0, GRecruit = 1, GStock = 2, GDouble = 3, GMason = 4, GTax = 5, GCaravan = 6, GAssim = 7, GRoads = 8, GPorts = 9, GVProd = 10, GVGoods = 11, GStuds = 12, GCore = 13, GN = 14;
        private static readonly int[] _err = new int[GN];
        private static readonly int[] _errLogged = new int[GN];   // recenzja 338: Interlocked - jeden pierwszy blad do logu takze przy wywolaniach rownoleglych
        internal static int Errors(int g) { return Volatile.Read(ref _err[g]); }

        internal static void Stumble(int g, string where, Exception e)
        {
            Interlocked.Increment(ref _err[g]);
            if (Interlocked.CompareExchange(ref _errLogged[g], 1, 0) != 0) return;
            try { Log.Error("Buildings338." + where, e); } catch { }
        }

        // ------------------------------------------------------------ BK przez refleksje (raz na proces; null = BK brak)
        private static bool _bkInit;
        private static PropertyInfo _cfgInstance, _cfgPopMgr, _cfgCulture;
        private static MethodInfo _getPopData, _applyProdBld, _getBuildingLevel;
        private static PropertyInfo _popVillageData, _popLandData, _vdCurrentBuilding, _landPasture;
        private static object _villageBuildings;   // DefaultVillageBuildings.Instance
        private static Type _vdType;

        internal static void InitBk()
        {
            if (_bkInit) return;
            _bkInit = true;
            try
            {
                var tCfg = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                if (tCfg == null) return;
                _cfgInstance = AccessTools.Property(tCfg, "Instance");
                _cfgPopMgr = AccessTools.Property(tCfg, "PopulationManager");
                _cfgCulture = AccessTools.Property(tCfg, "CultureModel");
                var tPm = AccessTools.TypeByName("BannerKings.Managers.PopulationManager");
                _getPopData = tPm != null ? AccessTools.Method(tPm, "GetPopData", new[] { typeof(Settlement) }) : null;
                var tPd = _getPopData != null ? _getPopData.ReturnType : null;
                _popVillageData = tPd != null ? AccessTools.Property(tPd, "VillageData") : null;
                _popLandData = tPd != null ? AccessTools.Property(tPd, "LandData") : null;
                _vdType = _popVillageData != null ? _popVillageData.PropertyType : null;
                _getBuildingLevel = _vdType != null ? AccessTools.Method(_vdType, "GetBuildingLevel", new[] { typeof(BuildingType) }) : null;
                _vdCurrentBuilding = _vdType != null ? AccessTools.Property(_vdType, "CurrentBuilding") : null;
                _landPasture = _popLandData != null ? AccessTools.Property(_popLandData.PropertyType, "Pastureland") : null;
                if (tPm != null && _vdType != null)
                    _applyProdBld = AccessTools.Method(tPm, "ApplyProductionBuildingEffect", new[] { typeof(ExplainedNumber).MakeByRefType(), typeof(TaleWorlds.Core.ItemObject), _vdType });
                var tVb = AccessTools.TypeByName("BannerKings.Managers.Populations.Villages.DefaultVillageBuildings");
                var pInst = tVb != null ? AccessTools.Property(tVb, "Instance") : null;
                _villageBuildings = pInst != null ? pInst.GetValue(null, null) : null;
            }
            catch (Exception e) { Stumble(GCore, "InitBk", e); }
        }

        internal static bool BkOk { get { InitBk(); return _cfgInstance != null && _cfgPopMgr != null && _getPopData != null; } }

        internal static object Config()
        {
            InitBk();
            return _cfgInstance != null ? _cfgInstance.GetValue(null, null) : null;
        }

        internal static object CultureModel()
        {
            var c = Config();
            return c != null && _cfgCulture != null ? _cfgCulture.GetValue(c, null) : null;
        }

        /// <summary>BannerKingsConfig.Instance.PopulationManager.GetPopData(st); null - osada bez danych BK albo brak BK.</summary>
        internal static object PopData(Settlement st)
        {
            if (st == null || !BkOk) return null;
            var c = _cfgInstance.GetValue(null, null);
            var pm = c != null ? _cfgPopMgr.GetValue(c, null) : null;
            return pm != null ? _getPopData.Invoke(pm, new object[] { st }) : null;
        }

        internal static object PopManager()
        {
            if (!BkOk) return null;
            var c = _cfgInstance.GetValue(null, null);
            return c != null ? _cfgPopMgr.GetValue(c, null) : null;
        }

        internal static object VillageDataOf(object pop) { return pop != null && _popVillageData != null ? _popVillageData.GetValue(pop, null) : null; }

        internal static float Pasture(object pop)
        {
            if (pop == null || _popLandData == null || _landPasture == null) return 0f;
            var ld = _popLandData.GetValue(pop, null);
            return ld != null ? Convert.ToSingle(_landPasture.GetValue(ld, null), Inv) : 0f;
        }

        /// <summary>BK DefaultVillageBuildings.Instance.&lt;name&gt; (Farming, Skeps, DailyProduction ...); null - brak.</summary>
        internal static BuildingType VillageBuilding(string name)
        {
            InitBk();
            if (_villageBuildings == null) return null;
            var p = AccessTools.Property(_villageBuildings.GetType(), name);
            return p != null ? p.GetValue(_villageBuildings, null) as BuildingType : null;
        }

        internal static int VillageLevel(object vd, BuildingType bt)
        {
            if (vd == null || bt == null || _getBuildingLevel == null) return 0;
            return Convert.ToInt32(_getBuildingLevel.Invoke(vd, new object[] { bt }), Inv);
        }

        /// <summary>VillageData.CurrentBuilding (budowa w toku albo projekt dzienny) - StringId typu albo null.</summary>
        internal static string VillageCurrentId(object vd)
        {
            if (vd == null || _vdCurrentBuilding == null) return null;
            var b = _vdCurrentBuilding.GetValue(vd, null) as Building;
            return b != null && b.BuildingType != null ? b.BuildingType.StringId : null;
        }

        /// <summary>PopulationManager.ApplyProductionBuildingEffect (BK - jedno zrodlo prawdy przypisania kategorii towaru do budynku wsi): czynnik 1 + 0.05 x poziom.</summary>
        internal static float VillageBuildingFactor(TaleWorlds.Core.ItemObject item, object vd)
        {
            if (item == null || vd == null || _applyProdBld == null) return 1f;
            var pm = PopManager();
            if (pm == null) return 1f;
            var args = new object[] { new ExplainedNumber(1f, false, null), item, vd };
            _applyProdBld.Invoke(pm, args);
            float f = ((ExplainedNumber)args[0]).ResultNumber;
            return f > 0f && !float.IsNaN(f) && !float.IsInfinity(f) ? f : 1f;
        }

        internal static Building FindBuilding(Town t, string id)
        {
            if (t == null || t.Buildings == null || id == null) return null;
            foreach (var b in t.Buildings) if (b != null && b.BuildingType != null && b.BuildingType.StringId == id) return b;
            return null;
        }

        internal static int Level(Town t, string id) { var b = FindBuilding(t, id); return b != null ? b.CurrentLevel : 0; }

        // ------------------------------------------------------------ start sesji i doba
        private static readonly List<string> _hooks = new List<string>();
        internal static void Hook(string what) { lock (_hooks) _hooks.Add(what); }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched (kampania istnieje, modele zarejestrowane): latki w kampanii (raz na proces) i linia startu sesji.</summary>
        internal static void SessionStart()
        {
            lock (_hooks) _hooks.Clear();
            for (int i = 0; i < GN; i++) { _err[i] = 0; _errLogged[i] = 0; }
            InitBk();
            try { Buildings338Wage.SessionStart(); } catch (Exception e) { Stumble(GWage, "Wage.SessionStart", e); }
            try { Buildings338AutoRecruit.SessionStart(); } catch (Exception e) { Stumble(GRecruit, "AutoRecruit.SessionStart", e); }
            try { Buildings338Food.SessionStart(); } catch (Exception e) { Stumble(GStock, "Food.SessionStart", e); }
            try { Buildings338Mason.SessionStart(); } catch (Exception e) { Stumble(GMason, "Mason.SessionStart", e); }
            try { Buildings338Income.SessionStart(); } catch (Exception e) { Stumble(GTax, "Income.SessionStart", e); }
            try { Buildings338Assimilation.SessionStart(); } catch (Exception e) { Stumble(GAssim, "Assimilation.SessionStart", e); }
            try { Buildings338Village.SessionStart(); } catch (Exception e) { Stumble(GVProd, "Village.SessionStart", e); }
            try { Buildings338Ports.SessionStart(); } catch (Exception e) { Stumble(GPorts, "Ports.SessionStart", e); }
            try { Buildings338Studs.SessionStart(); } catch (Exception e) { Stumble(GStuds, "Studs.SessionStart", e); }
            string courier = "?";
            try
            {
                var c = VillageBuilding("Courier");
                if (c != null && _villageBuildings != null)
                {
                    var pAll = AccessTools.Property(_villageBuildings.GetType(), "All");
                    var all = pAll != null ? pAll.GetValue(_villageBuildings, null) as System.Collections.IEnumerable : null;
                    bool inAll = false;
                    if (all != null) foreach (var o in all) { var bt = o as BuildingType; if (bt != null && bt.StringId == c.StringId) { inAll = true; break; } }
                    courier = inAll ? "TAK" : "NIE";
                }
            }
            catch (Exception e) { Stumble(GCore, "Courier", e); }
            string hooks;
            lock (_hooks) hooks = _hooks.Count > 0 ? string.Join("; ", _hooks.ToArray()) : "-";
            Log.Info("Budynki (338): start sesji | glowny " + YN(Main) + " | zold " + YN(WageOn) + ", autowerbunek " + YN(AutoRecruitOn) + ", zapas jedzenia " + YN(FoodStockOn)
                     + ", dubel jedzenia BK " + YN(FoodDoubleOn) + ", murarz " + YN(MasonOn) + ", urzad podatkowy " + YN(TaxOfficeOn) + ", karawany " + YN(CaravansOn)
                     + ", asymilacja " + YN(AssimilationOn) + ", drogi " + YN(RoadsOn) + ", porty zamkow " + YN(PortsOn) + ", produkcja wsi " + YN(VillageProdOn) + ", projekt Production " + YN(VillageProjectOn)
                     + ", towary wsi " + YN(VillageGoodsOn) + ", stadniny " + YN(StudsOn) + ", kopalnie zamkow " + YN(CastleMinesOn)
                     + " | BK " + (BkOk ? "jest" : "BRAK") + " | Courier Post na liscie budowy wsi BK: " + courier + " | latki: " + hooks);
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick: tick zamkow (porty, stadniny, kopalnie) i linie doby wszystkich grup.</summary>
        internal static void Daily()
        {
            if (Campaign.Current == null) return;
            int day = (int)CampaignTime.Now.ToDays;
            try { Buildings338Ports.CastleTick(); } catch (Exception e) { Stumble(GPorts, "CastleTick", e); }
            try { Buildings338Studs.CastleTick(); } catch (Exception e) { Stumble(GStuds, "Studs.CastleTick", e); }
            try { Buildings338Wage.Daily(day); } catch (Exception e) { Stumble(GWage, "Wage.Daily", e); }
            try { Buildings338AutoRecruit.Daily(day); } catch (Exception e) { Stumble(GRecruit, "AutoRecruit.Daily", e); }
            try { Buildings338Food.Daily(day); } catch (Exception e) { Stumble(GStock, "Food.Daily", e); }
            try { Buildings338Mason.Daily(day); } catch (Exception e) { Stumble(GMason, "Mason.Daily", e); }
            try { Buildings338Income.Daily(day); } catch (Exception e) { Stumble(GTax, "Income.Daily", e); }
            try { Buildings338Assimilation.Daily(day); } catch (Exception e) { Stumble(GAssim, "Assimilation.Daily", e); }
            try { Buildings338Village.Daily(day); } catch (Exception e) { Stumble(GVProd, "Village.Daily", e); }
            try { Buildings338Ports.Daily(day); } catch (Exception e) { Stumble(GPorts, "Ports.Daily", e); }
            try { Buildings338Studs.Daily(day); } catch (Exception e) { Stumble(GStuds, "Studs.Daily", e); }
        }

        internal static string Tail(int g) { return " | bledy " + Errors(g); }
    }
}
