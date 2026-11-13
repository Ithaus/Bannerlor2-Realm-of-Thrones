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
        private static int _repick, _repickNull, _dropped, _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly HashSet<Town> _folkSkip = new HashSet<Town>();   // 473-p1: osady (nie przejscia petli), gdzie lud pominal sad BK tej doby

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
                var town = __args != null && __args.Length > 0 ? __args[0] as Town : null;
                if (Buildings338Need.On && town != null && town.Settlement != null && town.Settlement.OwnerClan != Clan.PlayerClan)   // 476-f: wybor wg potrzeb zamiast losu
                {
                    var need = Buildings338Need.Pick(town, Skip);
                    if (need != null || __result == null) { __result = need; return; }
                }
                if (__result == null) return;
                if (!Skip(town, __result.BuildingType)) return;
                if (Buildings338Works.AiBlocks(town, __result.BuildingType)) Buildings338Works.NoteAiBlocked();   // 476-p2
                var pick = new List<Building>();
                foreach (var b in town.Buildings)
                    if (b != null && b.BuildingType != null && !b.BuildingType.IsDailyProject && b.CurrentLevel < 3 && !Skip(town, b.BuildingType)
                        && (town.BuildingsInProgress == null || !town.BuildingsInProgress.Contains(b)))
                        pick.Add(b);
                if (pick.Count == 0) { __result = null; _repickNull++; return; }
                __result = pick[MBRandom.RandomInt(pick.Count)];
                _repick++;
            }
            catch (Exception e) { Stumble("NextPostfix", e); }
        }

        /// <summary>476-a: AI nie stawia tej budowli w tej osadzie - zbrojownia / sad BK (473-b6, klucz On) albo zaklad bez zasobu okolicy (Buildings338Works, klucz Works).</summary>
        private static bool Skip(Town t, BuildingType bt) { return Skip(t, bt, false); }

        private static bool Skip(Town t, BuildingType bt, bool queued)
        {
            if (IsBlocked(bt) && AiTown(t))
            {
                if (bt.StringId == CourtId) { var w = Buildings338Civic.AiWantsWorship(t, queued); if (w.HasValue) return !w.Value; }   // 476-c: slot sadu BK = miejsce kultu
                return true;
            }
            return Buildings338Works.AiBlocks(t, bt);
        }

        /// <summary>476-p2 (recenzja A2): CitySack.Prioritize nie stawia na czele zgliszcz budowli, ktora AI tej osady i tak wyjmie z kolejki.</summary>
        internal static bool AiSkips(Town t, BuildingType bt) { try { return t != null && bt != null && Skip(t, bt, true); } catch { return false; } }   // R4: ta sama miara co DropQueued (histereza)

        /// <summary>(2) Z ArmouryBehavior przed BuildFunding.Daily: zbrojownia i sad BK (i 476-a: zaklad bez zasobu) wychodza z kolejki osad AI (postep i poziom zostaja na budowli).</summary>
        internal static void DropQueued()
        {
            if ((!On && !Buildings338Works.On) || Campaign.Current == null) return;
            var all = new List<Town>(Town.AllTowns);
            if (Buildings338Works.On) all.AddRange(Town.AllCastles);   // 476-a: kopalnie i stadniny zamkow
            foreach (var t in all)
            {
                try
                {
                    if (t == null || t.Settlement == null || t.Settlement.OwnerClan == Clan.PlayerClan) continue;
                    var q = t.BuildingsInProgress;
                    if (q == null || q.Count == 0) continue;
                    bool any = false;
                    foreach (var b in q) if (b != null && Skip(t, b.BuildingType, true)) { any = true; break; }
                    if (!any) continue;
                    var keep = new List<Building>();
                    foreach (var b in q)
                    {
                        if (b == null || !Skip(t, b.BuildingType, true)) { keep.Add(b); continue; }
                        _dropped++;
                        if (Buildings338Works.AiBlocks(t, b.BuildingType)) Buildings338Works.NoteAiBlocked();   // 476-p2: licznik decyzji, nie wywolan
                    }
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
            _folkSkip.Add(t);
            return true;
        }

        /// <summary>Linia doby (Buildings338.Daily): miasta z poziomem zbrojowni / sadu BK, ile razy AI losowalo ponownie, ile wpisow wyszlo z kolejek.</summary>
        internal static void Daily(int day)
        {
            if (!On) return;
            try
            {
                int arm = 0, court = 0, armLv = 0, courtLv = 0, inQp = 0, inQai = 0;
                foreach (var t in Town.AllTowns)
                {
                    try
                    {
                        if (t == null || t.Buildings == null) continue;
                        int a = Buildings338.Level(t, ArmoryId), c = Buildings338.Level(t, CourtId);
                        if (a > 0) { arm++; armLv += a; }
                        if (c > 0) { court++; courtLv += c; }
                        var q = t.BuildingsInProgress;
                        bool pl = t.Settlement != null && t.Settlement.OwnerClan == Clan.PlayerClan;   // 473-p1: gracz i AI osobno (AI przy TAK powinno byc 0)
                        if (q != null) foreach (var b in q) if (b != null && IsBlocked(b.BuildingType)) { if (pl) inQp++; else inQai++; }
                    }
                    catch (Exception e) { Stumble("Daily(osada)", e); }
                }
                Log.Info("Budynki (338) bez zbrojowni i sadu BK: dzien " + day + " | latka " + (_patched ? "tak (" + _target + ")" : "NIE (" + _target + ")")
                         + " | AI losowalo ponownie " + _repick + " (bez innej budowli " + _repickNull + "), wyjete z kolejek AI " + _dropped + ", osady z sadem pominietym w wyborze ludu " + _folkSkip.Count
                         + " | miasta z Armory " + arm + " (poziomow " + armLv + "), z Court House " + court + " (poziomow " + courtLv + "), w kolejkach teraz gracz " + inQp + ", AI " + inQai
                         + " | bledy " + _stumbles);
            }
            catch (Exception e) { Stumble("Daily", e); }
            _repick = _repickNull = _dropped = 0; _folkSkip.Clear();
        }
    }
}
