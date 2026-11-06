using System;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using HarmonyLib;

namespace Armoury
{
    /// <summary>
    /// 338 grupa 7 - ASYMILACJA KULTUROWA (BK, projekt dzienny bk_building_daily_assimilation, opis: "Increases Cultural Presence").
    /// BKBuildings:171-172 - projekt z pusta tablica efektow; BKCultureModel.CalculateCultureWeight i BKROT nie sprawdzaja biezacego projektu - wybranie projektu = brak
    /// projektu. Naprawa: postfiks na CalculateCultureWeight CZYNNEGO modelu kultury BK (BannerKingsConfig.Instance.CultureModel, u Jeffa BKROTCultureModel, ktory
    /// wola base - latka tylko na klasie deklarujacej metode czynnego modelu, postfiks liczy tylko na tej metodzie): gdy projekt jest biezacym projektem dziennym
    /// miasta/zamku i kultura danych = kultura pana osady: +15 wagi. 15 = istniejaca liczba BK: Theater na poziomie 3 (5 x poziom, BKCultureModel:178); dla porownania
    /// gubernator +10, wies tej kultury +15. Koszt dla gracza: brak innego projektu dziennego.
    /// </summary>
    internal static class Buildings338Assimilation
    {
        internal const string ProjectId = "bk_building_daily_assimilation";
        private const float Weight = 15f;
        private static bool _wired;
        private static RuntimeMethodHandle _active;
        private static PropertyInfo _culture;
        private static TextObject _txt;
        private static long _calls, _added;
        private static readonly System.Collections.Generic.HashSet<MethodInfo> _patched = new System.Collections.Generic.HashSet<MethodInfo>();

        internal static void SessionStart()
        {
            _calls = _added = 0;
            _wired = false;
            string why = null;
            try
            {
                var model = Buildings338.CultureModel();
                var tCdc = AccessTools.TypeByName("BannerKings.Managers.Populations.CultureDataClass");
                _culture = tCdc != null ? AccessTools.Property(tCdc, "Culture") : null;
                var m = model != null && tCdc != null ? PatchTarget.OfActive(model, "CalculateCultureWeight", new[] { typeof(Settlement), tCdc, typeof(float) }, "asymilacja kulturowa (338)") : null;
                if (model == null) why = "brak modelu kultury BK";
                else if (m == null || _culture == null) why = "brak metody CalculateCultureWeight(Settlement, CultureDataClass, float) albo CultureDataClass.Culture";
                else
                {
                    if (!_patched.Contains(m))
                    {
                        Buildings338.H.Patch(m, postfix: new HarmonyMethod(typeof(Buildings338Assimilation), nameof(WeightPostfix)));
                        _patched.Add(m);
                    }
                    _active = m.MethodHandle;
                    _wired = true;
                    why = (m.DeclaringType != null ? m.DeclaringType.Name : "?") + ".CalculateCultureWeight wpiety";
                }
            }
            catch (Exception e) { why = "wyjatek"; Buildings338.Stumble(Buildings338.GAssim, "Assimilation.SessionStart", e); }
            Buildings338.Hook("asymilacja: " + (_wired ? why : "BRAK (" + why + ")"));
        }

        private static bool Chosen(Settlement s)
        {
            if (s == null || !(s.IsTown || s.IsCastle) || s.Town == null) return false;
            var cur = s.Town.CurrentDefaultBuilding;
            return cur != null && cur.BuildingType != null && cur.BuildingType.StringId == ProjectId;
        }

        public static void WeightPostfix(MethodBase __originalMethod, Settlement __0, object __1, ref ExplainedNumber __result)
        {
            if (!_wired || __0 == null || __1 == null || !Buildings338.AssimilationOn) return;
            try
            {
                if (__originalMethod == null || !__originalMethod.MethodHandle.Equals(_active)) return;
                Interlocked.Increment(ref _calls);
                if (!Chosen(__0)) return;
                var owner = __0.Owner;
                var c = _culture.GetValue(__1, null) as CultureObject;
                if (owner == null || c == null || c != owner.Culture) return;
                if (_txt == null) _txt = new TextObject("{=!}Cultural assimilation");
                __result.Add(Weight, _txt);
                Interlocked.Increment(ref _added);
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GAssim, "Assimilation.WeightPostfix", e); }
        }

        internal static void Daily(int day)
        {
            int inList = 0, all = 0, chosen = 0;
            foreach (var t in Town.AllFiefs)
            {
                try
                {
                    all++;
                    if (Buildings338.FindBuilding(t, ProjectId) != null) inList++;
                    if (Chosen(t.Settlement)) chosen++;
                }
                catch (Exception e) { Buildings338.Stumble(Buildings338.GAssim, "Assimilation.Daily", e); }
            }
            Log.Info("Budynki (338) asymilacja: dzien " + day + " | osad z projektem w liscie " + inList + "/" + all + ", wybrany w " + chosen + " | latka " + Buildings338.YN(_wired)
                     + ", wywolan " + Interlocked.Exchange(ref _calls, 0) + ", dodano +" + (int)Weight + " razy " + Interlocked.Exchange(ref _added, 0) + Buildings338.Tail(Buildings338.GAssim));
        }
    }
}
