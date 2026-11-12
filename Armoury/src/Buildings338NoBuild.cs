using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 473-b6 (Jeff 13.11: "Wylaczam: Armory i Court House z BannerKings") - AI NIE STAWIA ZBROJOWNI I SADU BK (bk_building_armory, bk_building_courthouse;
    /// oba tylko w miastach - BKBuildings.IsCastleBuilding ich nie zna). Klucz Buildings338AiSkipArmoryCourt (z Buildings338); wylaczony = jak dotad co do bitu.
    /// Skad AI bierze budowle (dekompilacja gry): BuildingsCampaignBehavior.DecideBuildingQueue - pusta kolejka, osada nie gracza, 10% na dobe -
    /// Models.BuildingScoreCalculationModel.GetNextBuilding(town) = losowa budowla z poziomem &lt; 3 (DefaultBuildingScoreCalculationModel); takze
    /// BuildDevelopmentsAtGameStart (nowa kampania). BK nie podmienia tego modelu.
    ///  - DLACZEGO NIE BK InnovationData.GetAvailableBuildings: BKBuildingsBehavior.OnGameLoaded (bk_all.cs:89063-89080) budowle spoza tej listy
    ///    USUWA i wstawia od nowa na poziomie 0 - zbudowane zbrojownie i sady wyzerowalyby sie przy wczytaniu. Dlatego tylko wybor AI.
    ///  - (1) Postfiks na GetNextBuilding czynnego modelu (AccessTools.DeclaredMethod na klasie deklarujacej - CLAUDE.md 7): gdy wynik to zbrojownia/sad BK
    ///    w osadzie nie gracza - losujemy jeszcze raz z tych samych budowli (bez projektow dziennych, poziom &lt; 3, nie w kolejce) bez tych dwoch; brak - null
    ///    (kolejka pusta jak u gry, projekt dzienny dalej dziala).
    ///  - (2) Raz na dobe PRZED BuildFunding.Daily (pan nie placi za nie tej doby): z kolejki osad nie gracza wychodza te dwie budowle (reszta kolejki w tej samej
    ///    kolejnosci). Postep zostaje na obiekcie Building (BuildingProgress), poziom zostaje - nic z zapisu nie ginie, wylaczenie klucza wraca do stanu gry.
    ///  - (3) Roboty ludu (P2, BuildFunding.FolkTarget, osada AI poza kolejka) pomijaja sad BK (zbrojownia byla juz w LordOnly).
    /// Gracz: bez zmian - w swoich lennach buduje co chce. Zbudowane poziomy dalej dzialaja (BK czyta poziom, nie kolejke). Zapis gry: nic nowego.
    /// Linia doby: "Budynki (338) bez zbrojowni i sadu BK: dzien N". Potkniecia per osada (licznik, pierwszy do logu).
    /// </summary>
    internal static class Buildings338NoBuild
    {
        internal const string ArmoryId = "bk_building_armory", CourtId = "bk_building_courthouse";

        internal static bool On { get { var s = Settings.Current; return s != null && s.Buildings338 && s.Buildings338AiSkipArmoryCourt; } }

        private static bool _patched;
        private static string _target = "-";
        private static int _repick, _repickNull, _dropped, _folkSkip, _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static bool IsBlocked(BuildingType bt)
        {
            var id = bt != null ? bt.StringId : null;
            return id == ArmoryId || id == CourtId;
        }

        /// <summary>Osada, w ktorej AI nie stawia zbrojowni i sadu BK: klucz wlaczony, wlasciciel nie jest rodem gracza.</summary>
        internal static bool AiTown(Town t)
        {
            if (t == null || !On) return false;
            var st = t.Settlement;
            return st != null && st.OwnerClan != Clan.PlayerClan;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("Buildings338NoBuild." + where, e); } catch { }
        }

        /// <summary>Z Buildings338.SessionStart (kampania, modele zarejestrowane): latka raz na proces na GetNextBuilding czynnego modelu.</summary>
        internal static void SessionStart()
        {
            _repick = _repickNull = _dropped = _folkSkip = 0;
            if (_patched) return;
            var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.BuildingScoreCalculationModel : null;
            if (model == null) { _target = "brak modelu"; return; }
            MethodInfo m = null;
            for (var t = model.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                m = AccessTools.DeclaredMethod(t, "GetNextBuilding", new[] { typeof(Town) });
                if (m != null && !m.IsAbstract) break;
                m = null;
            }
            if (m == null) { _target = "brak metody (" + model.GetType().FullName + ")"; return; }
            Buildings338.H.Patch(m, postfix: new HarmonyMethod(typeof(Buildings338NoBuild), nameof(NextPostfix)));
            _patched = true;
            _target = m.DeclaringType.FullName + ".GetNextBuilding";
            Buildings338.Hook("bez zbrojowni i sadu BK: " + _target);
        }

        /// <summary>(1) AI wylosowalo zbrojownie albo sad BK - losujemy jeszcze raz bez nich (te same warunki co model gry).</summary>
        public static void NextPostfix(object[] __args, ref Building __result)
        {
            try
            {
                if (__result == null || !IsBlocked(__result.BuildingType)) return;
                var town = __args != null && __args.Length > 0 ? __args[0] as Town : null;
                if (!AiTown(town)) return;
                var pick = new List<Building>();
                foreach (var b in town.Buildings)
                    if (b != null && b.BuildingType != null && !b.BuildingType.IsDailyProject && b.CurrentLevel < 3 && !IsBlocked(b.BuildingType)
                        && (town.BuildingsInProgress == null || !town.BuildingsInProgress.Contains(b)))
                        pick.Add(b);
                if (pick.Count == 0) { __result = null; _repickNull++; return; }
                __result = pick[MBRandom.RandomInt(pick.Count)];
                _repick++;
            }
            catch (Exception e) { Stumble("NextPostfix", e); }
        }

        /// <summary>(2) Z ArmouryBehavior przed BuildFunding.Daily: zbrojownia i sad BK wychodza z kolejki osad AI (postep i poziom zostaja na budowli).</summary>
        internal static void DropQueued()
        {
            if (!On || Campaign.Current == null) return;
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    if (!AiTown(t)) continue;
                    var q = t.BuildingsInProgress;
                    if (q == null || q.Count == 0) continue;
                    bool any = false;
                    foreach (var b in q) if (b != null && IsBlocked(b.BuildingType)) { any = true; break; }
                    if (!any) continue;
                    var keep = new List<Building>();
                    foreach (var b in q) if (b == null || !IsBlocked(b.BuildingType)) keep.Add(b); else _dropped++;
                    q.Clear();
                    foreach (var b in keep) q.Enqueue(b);
                }
                catch (Exception e) { Stumble("DropQueued", e); }
            }
        }

        /// <summary>(3) BuildFunding.FolkTarget (osada AI, poza kolejka): lud nie stawia sadu BK.</summary>
        internal static bool FolkSkips(Town t, Building b)
        {
            if (b == null || !IsBlocked(b.BuildingType) || !AiTown(t)) return false;
            _folkSkip++;
            return true;
        }

        /// <summary>Linia doby (Buildings338.Daily): miasta z poziomem zbrojowni / sadu BK, ile razy AI losowalo ponownie, ile wpisow wyszlo z kolejek.</summary>
        internal static void Daily(int day)
        {
            if (!On) return;
            try
            {
                int arm = 0, court = 0, armLv = 0, courtLv = 0, inQ = 0;
                foreach (var t in Town.AllTowns)
                {
                    try
                    {
                        if (t == null || t.Buildings == null) continue;
                        int a = Buildings338.Level(t, ArmoryId), c = Buildings338.Level(t, CourtId);
                        if (a > 0) { arm++; armLv += a; }
                        if (c > 0) { court++; courtLv += c; }
                        var q = t.BuildingsInProgress;
                        if (q != null) foreach (var b in q) if (b != null && IsBlocked(b.BuildingType)) inQ++;
                    }
                    catch (Exception e) { Stumble("Daily(osada)", e); }
                }
                Log.Info("Budynki (338) bez zbrojowni i sadu BK: dzien " + day + " | latka " + (_patched ? "tak (" + _target + ")" : "NIE (" + _target + ")")
                         + " | AI losowalo ponownie " + _repick + " (bez innej budowli " + _repickNull + "), wyjete z kolejek AI " + _dropped + ", lud pominal sad " + _folkSkip
                         + " | miasta z Armory " + arm + " (poziomow " + armLv + "), z Court House " + court + " (poziomow " + courtLv + "), w kolejkach teraz " + inQ
                         + " (gracz) | bledy " + _stumbles);
            }
            catch (Exception e) { Stumble("Daily", e); }
            _repick = _repickNull = _dropped = _folkSkip = 0;
        }
    }
}
