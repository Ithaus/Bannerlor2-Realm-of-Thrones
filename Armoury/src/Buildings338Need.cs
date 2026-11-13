using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 476-f (audyt budynkow 12.11 paczka 5: AI wybiera budowe losowo - DefaultBuildingScoreCalculationModel.GetNextBuilding = losowa budowla, 48.7% punktow zamkow
    /// na budowle slabe/martwe; sesja Jeffowi: "AI wybiera budowe wedlug potrzeb: przy glodzie spichlerz i pola, przy drogim zoldzie koszary, przy froncie mury, przy
    /// zlozu kopalnia"; Jeff: "na pozostale sie zgadzam") - WYBOR BUDOWY AI WEDLUG POTRZEB.
    /// Postfiks GetNextBuilding (473-b6, osada nie gracza): kandydaci jak u gry (bez projektow dziennych, poziom &lt; 3, nie w kolejce, bez Skip 473-b6/476), waga 1;
    /// x6 gdy budowla odpowiada potrzebie osady: GLOD (bilans jedzenia &lt; 0 albo maly bilans i zapas &lt; 30 dob) - spichlerz / magazyn / pola / wodociagi; ZOLD (zaloga
    /// &gt;= 150) - koszary, biuro kasztelana; FRONT (WarFront.FrontPeek) - mury, warsztat oblezniczy, straznica; ZLOZE (476-a) - kopalnia; x3 stadnina przy pastwisku
    /// i drogi (zawsze uzyteczne). Ocena tylko w chwili wyboru (gra pyta, gdy kolejka pusta - 10% na dobe). Klucz Buildings338 AI Builds By Need (TAK); NIE - losowo
    /// jak gra (473-b6 bez zmian).
    /// </summary>
    internal static class Buildings338Need
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.Buildings338 && s.Buildings338AiByNeed; } }
        private static int _picks, _hunger, _wage, _front, _ore, _stumbles;

        private static readonly HashSet<string> Food = new HashSet<string>(StringComparer.Ordinal) { "building_castle_granary", "building_settlement_granary", "building_settlement_warehouse", "building_castle_farmlands", "building_settlement_waterworks" };
        private static readonly HashSet<string> Wage = new HashSet<string>(StringComparer.Ordinal) { "building_castle_barracks", "building_settlement_barracks", "building_castle_castallans_office" };
        private static readonly HashSet<string> War = new HashSet<string>(StringComparer.Ordinal) { "building_castle_fortifications", "building_settlement_fortifications", "building_castle_siege_workshop", "building_settlement_siege_workshop", "building_castle_guard_house", "building_settlement_guard_house" };
        private static readonly HashSet<string> Roads = new HashSet<string>(StringComparer.Ordinal) { "building_castle_roads_and_paths", "building_settlement_roads_and_paths" };

        internal static Building Pick(Town town, Func<Town, BuildingType, bool> skip)
        {
            try
            {
                if (town == null || town.Buildings == null) return null;
                float change = 0f;
                try { change = Campaign.Current.Models.SettlementFoodModel.CalculateTownFoodStocksChange(town, true, false).ResultNumber; } catch { }
                bool hunger = change < 0f || (change < 5f && town.FoodStocks < 30f * Math.Max(1f, 5f - change));
                int garrison = town.GarrisonParty != null && town.GarrisonParty.MemberRoster != null ? town.GarrisonParty.MemberRoster.TotalManCount : 0;
                bool wage = garrison >= 150;
                bool front = WarFront.FrontPeek(town.Settlement);
                bool ore = Buildings338Works.OreUsable(town);   // 476-p3
                bool pasture = Buildings338Works.HasPasture(town);
                var cand = new List<Building>(); var w = new List<float>(); float sum = 0f;
                foreach (var b in town.Buildings)
                {
                    if (b == null || b.BuildingType == null || b.BuildingType.IsDailyProject || b.CurrentLevel >= 3) continue;
                    if (town.BuildingsInProgress != null && town.BuildingsInProgress.Contains(b)) continue;
                    if (skip != null && skip(town, b.BuildingType)) continue;
                    string id = b.BuildingType.StringId ?? "";
                    float x = 1f;
                    if (hunger && Food.Contains(id)) x *= 6f;
                    if (wage && Wage.Contains(id)) x *= 6f;
                    if (front && War.Contains(id)) x *= 6f;
                    if (ore && (id == Buildings338Works.TownMinesId || id == Buildings338Ports.CastleMinesId)) x *= 6f;
                    if (pasture && id == Buildings338Works.StudsId) x *= 3f;
                    if (Roads.Contains(id)) x *= 3f;
                    cand.Add(b); w.Add(x); sum += x;
                }
                if (cand.Count == 0) return null;
                float r = MBRandom.RandomFloat * sum;
                Building pick = cand[cand.Count - 1];
                for (int i = 0; i < cand.Count; i++) { r -= w[i]; if (r <= 0f) { pick = cand[i]; break; } }
                _picks++;
                string pid = pick.BuildingType.StringId ?? "";
                if (hunger && Food.Contains(pid)) _hunger++;
                if (wage && Wage.Contains(pid)) _wage++;
                if (front && War.Contains(pid)) _front++;
                if (ore && (pid == Buildings338Works.TownMinesId || pid == Buildings338Ports.CastleMinesId)) _ore++;
                return pick;
            }
            catch (Exception e) { _stumbles++; if (_stumbles <= 3) { try { Log.Error("Buildings338Need.Pick", e); } catch { } } return null; }
        }

        internal static void Daily(int day)
        {
            if (!On) return;
            Log.Info("Budynki (338) wybor budowy AI wg potrzeb (476-f): dzien " + day + " | wyborow " + _picks + " - w tym na potrzebe: glod " + _hunger + ", zold " + _wage + ", front " + _front + ", zloze " + _ore + " | bledy " + _stumbles);
            _picks = _hunger = _wage = _front = _ore = 0;
        }
    }
}
