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
    /// 473-p2 (Jeff 13.11: "Wylaczam: ... port i Harbor w zamku. Zaden zamek nie ma ich dzis zbudowanych"): ta sama droga (1)-(3) dla Portu i Harbor BK
    /// (building_port, building_harbor) w ZAMKACH (Town.IsCastle) - osobny klucz Buildings338AiSkipCastlePort. W miastach port/Harbor bez zmian. Zamki tez ida przez
    /// DecideBuildingQueue (BuildingsCampaignBehavior.DailyTickSettlement - kazda twierdza), wiec ten sam postfiks; DropQueued i linia doby przechodza takze Town.AllCastles.
    /// Tez NIE przez BK GetAvailableBuildings (zerowanie przy wczytaniu, jak wyzej). Port zamku, gdy kiedys zbudowany, dalej lowi (Buildings338Ports - ryby do jedzenia zamku).
    /// </summary>
    internal static class Buildings338NoBuild
    {
        internal const string ArmoryId = "bk_building_armory", CourtId = "bk_building_courthouse";
        internal const string PortId = Buildings338Ports.PortId, HarborId = Buildings338Ports.HarborId;   // 473-p2

        /// <summary>Klucz zbrojowni i sadu BK (miasta).</summary>
        internal static bool ArmCourtOn { get { var s = Settings.Current; return s != null && s.Buildings338 && s.Buildings338AiSkipArmoryCourt; } }
        /// <summary>473-p2: klucz portu i Harbor BK w zamkach.</summary>
        internal static bool CastlePortOn { get { var s = Settings.Current; return s != null && s.Buildings338 && s.Buildings338AiSkipCastlePort; } }
        /// <summary>Ktorykolwiek z dwoch kluczy.</summary>
        internal static bool On { get { return ArmCourtOn || CastlePortOn; } }

        private static bool _patched;
        private static string _target = "-";
        private static int _repick, _repickNull, _dropped, _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly HashSet<Town> _folkSkip = new HashSet<Town>();   // 473-p1: osady (nie przejscia petli), gdzie lud pominal sad BK tej doby

        /// <summary>Budowla, ktorej AI nie stawia w tej osadzie: zbrojownia/sad BK (klucz ArmCourtOn), port/Harbor BK w zamku (klucz CastlePortOn, 473-p2).</summary>
        internal static bool IsBlocked(Town t, BuildingType bt)
        {
            var id = bt != null ? bt.StringId : null;
            if (id == null) return false;
            if (id == ArmoryId || id == CourtId) return ArmCourtOn;
            if (id == PortId || id == HarborId) return t != null && t.IsCastle && CastlePortOn;
            return false;
        }

        /// <summary>473-p2: miasta, a przy kluczu portu zamku takze zamki (DropQueued i linia doby).</summary>
        private static IEnumerable<Town> Fiefs()
        {
            foreach (var t in Town.AllTowns) yield return t;
            if (CastlePortOn) foreach (var t in Town.AllCastles) yield return t;
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
            _repick = _repickNull = _dropped = 0; _folkSkip.Clear();
            _stumbles = 0; _err.Clear();   // 473-p1: licznik bledow i pierwszy wpis do logu - od nowa w kazdej kampanii
            if (_patched) { Buildings338.Hook("bez zbrojowni i sadu BK: " + _target); return; }   // 473-p1: SessionStart czysci _hooks co sesje - latka znow w linii startu
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
                if (__result == null) return;
                var town = __args != null && __args.Length > 0 ? __args[0] as Town : null;
                if (!AiTown(town) || !IsBlocked(town, __result.BuildingType)) return;
                var pick = new List<Building>();
                foreach (var b in town.Buildings)
                    if (b != null && b.BuildingType != null && !b.BuildingType.IsDailyProject && b.CurrentLevel < 3 && !IsBlocked(town, b.BuildingType)
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
            foreach (var t in Fiefs())
            {
                try
                {
                    if (!AiTown(t)) continue;
                    var q = t.BuildingsInProgress;
                    if (q == null || q.Count == 0) continue;
                    bool any = false;
                    foreach (var b in q) if (b != null && IsBlocked(t, b.BuildingType)) { any = true; break; }
                    if (!any) continue;
                    var keep = new List<Building>();
                    foreach (var b in q) if (b == null || !IsBlocked(t, b.BuildingType)) keep.Add(b); else _dropped++;
                    q.Clear();
                    foreach (var b in keep) q.Enqueue(b);
                }
                catch (Exception e) { Stumble("DropQueued", e); }
            }
        }

        /// <summary>(3) BuildFunding.FolkTarget (osada AI, poza kolejka): lud nie stawia sadu BK.</summary>
        internal static bool FolkSkips(Town t, Building b)
        {
            if (b == null || !AiTown(t) || !IsBlocked(t, b.BuildingType)) return false;
            _folkSkip.Add(t);
            return true;
        }

        /// <summary>Linia doby (Buildings338.Daily): miasta z poziomem zbrojowni / sadu BK, ile razy AI losowalo ponownie, ile wpisow wyszlo z kolejek.</summary>
        internal static void Daily(int day)
        {
            if (!On) return;
            try
            {
                int arm = 0, court = 0, armLv = 0, courtLv = 0, inQp = 0, inQai = 0, cport = 0, cportLv = 0, chav = 0;
                foreach (var t in Fiefs())
                {
                    try
                    {
                        if (t == null || t.Buildings == null) continue;
                        if (t.IsCastle)
                        {
                            // 473-p2: zamki z portem / Harbor BK (poziom > 0) i zamki, ktore te budowle maja na liscie (BK wstawia do zamku tylko Port, bk_all.cs:89053)
                            int pl0 = Math.Max(Buildings338.Level(t, PortId), Buildings338.Level(t, HarborId));
                            if (pl0 > 0) { cport++; cportLv += pl0; }
                            if (Buildings338.FindBuilding(t, PortId) != null || Buildings338.FindBuilding(t, HarborId) != null) chav++;
                        }
                        else
                        {
                            int a = Buildings338.Level(t, ArmoryId), c = Buildings338.Level(t, CourtId);
                            if (a > 0) { arm++; armLv += a; }
                            if (c > 0) { court++; courtLv += c; }
                        }
                        var q = t.BuildingsInProgress;
                        bool pl = t.Settlement != null && t.Settlement.OwnerClan == Clan.PlayerClan;   // 473-p1: gracz i AI osobno (AI przy TAK powinno byc 0)
                        if (q != null) foreach (var b in q) if (b != null && IsBlocked(t, b.BuildingType)) { if (pl) inQp++; else inQai++; }
                    }
                    catch (Exception e) { Stumble("Daily(osada)", e); }
                }
                Log.Info("Budynki (338) bez zbrojowni i sadu BK: dzien " + day + " | latka " + (_patched ? "tak (" + _target + ")" : "NIE (" + _target + ")")
                         + " | AI losowalo ponownie " + _repick + " (bez innej budowli " + _repickNull + "), wyjete z kolejek AI " + _dropped + ", osady z sadem pominietym w wyborze ludu " + _folkSkip.Count
                         + " | klucze: zbrojownia i sad " + (ArmCourtOn ? "TAK" : "NIE") + ", port zamku " + (CastlePortOn ? "TAK" : "NIE")
                         + " | miasta z Armory " + arm + " (poziomow " + armLv + "), z Court House " + court + " (poziomow " + courtLv + ")"
                         + (CastlePortOn ? " | zamki z Port/Harbor " + cport + " (poziomow " + cportLv + "), zamki z ta budowla na liscie " + chav : "")
                         + " | w kolejkach teraz gracz " + inQp + ", AI " + inQai
                         + " | bledy " + _stumbles);
            }
            catch (Exception e) { Stumble("Daily", e); }
            _repick = _repickNull = _dropped = 0; _folkSkip.Clear();
        }
    }
}
