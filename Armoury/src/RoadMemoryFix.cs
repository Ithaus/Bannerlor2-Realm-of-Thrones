using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PAMIEC DROG MAPY ROT UZUPELNIONA (paczka 131; autotest 5 07.10 10:40: po latce wozow straznik BK odrzuca coraz wiecej rozkazow
    /// KARAWAN i LORDOW w Wickenden, Lord Hewett's Town i zamku Acorn Hall - w 3 osadach 7.1 -> 21.0 -> 32.0 odrzucen na dobe w kolejnych
    /// tercjach 40 dob).
    /// PRZYCZYNA (kod gry 1.4.8, NavalDLC, BK, dane mapy ROT): gra liczy "odleglosc partii" (MapDistanceModel.GetDistance(partia, ...),
    /// takze NavalDLCMapDistanceModel) od "najblizszego wejscia" SCIANY siatki drog, na ktorej partia stoi - z tablicy sciana -> wejscie
    /// osady w pamieci drog (NavigationCache._closestSettlementsToFaceIndices, plik ROT-Map\ModuleData\DistanceCaches\
    /// settlements_distance_cache_{Default,Naval,All}.bin, czytany w SettlementPositionScript.OnInit przy wczytaniu mapy). Siatka
    /// ROT (navmesh.bin 17.08) ma 17864 sciany, a pamiec drog (28.07) widziala sciany 0-17841: 22 sciany dociete pozniej (przy Griffin's
    /// Roost, Lord Hewett's Town, Pinkmaiden, Acorn Hall, kryjowce, Wickenden) nie maja wpisu, a stoja na nich bramy Wickenden, Lord
    /// Hewett's Town i Acorn Hall. Bez wpisu odleglosc partii = 1e8 do KAZDEJ osady, wiec straznik BK (GuardSettlementMove, prog 50000)
    /// odrzuca kazdy rozkaz "jedz" z tych miejsc, a karawany BK licza kazde miasto jako "za daleko" (BKCaravansBehavior.GetTradeScoreForTown).
    /// POPRAWKA U ZRODLA (wylacznik MapRoadTableFix): zaraz po wczytaniu mapy (postfiks na Campaign.LoadMapScene - pamiec drog jest juz
    /// wczytana i zarejestrowana w modelu odleglosci, a zadna partia jeszcze nie ruszyla; przed OnGameLoaded / OnNewGameCreated) kazda
    /// sciana siatki bez wpisu, ktorej generator mapy nie widzial (indeks powyzej najwyzszej sciany w tablicach), dostaje wpis
    /// DOKLADNIE tak, jak robi to generator gry (NavigationCache.GenerateClosestSettlementToFaceCache - ten sam kod gry, wolany dla tej
    /// jednej sciany: srodek sciany, droga siatki do bramy / portu kazdej osady, najkrotsza wygrywa; brak drogi = brak wpisu; dla Default
    /// i Naval ze skrotem gry useEarlyOut, ktory daje ten sam wynik - droga nie jest krotsza od linii prostej). Sciany,
    /// ktore generator widzial, a nie dal im wpisu, zostaja bez wpisu - to jego werdykt (zadna osada w zasiegu drogi). Dla wszystkich
    /// trzech rodzajow drogi (Default, Naval, All). Stan tylko w pamieci gry (pamiec drog powstaje od nowa przy kazdym wczytaniu mapy) -
    /// nic w zapisie. Wylaczony = tablica jak w plikach mapy (zostaje sama linia w logu).
    /// KONTROLA (recenzja 07.10, tylko log): gdy cos dopisujemy, ten sam kod generatora liczy jeszcze raz probke scian, ktore generator edytora
    /// widzial (co 97. sciana z wpisem Default) - zgodnosc z plikiem w linii po wczytaniu pokazuje, ze w grze generator liczy jak edytor mapy.
    /// </summary>
    internal static class RoadMemoryFix
    {
        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static FieldInfo _fFaces;
        private static PropertyInfo _pNav;
        private static MethodInfo _mCount, _mCenter, _mRecord, _mExcluded, _mCost0, _mCost1, _mAll, _mClosest;

        private sealed class Tab
        {
            public NavigationCache<Settlement> Cache;
            public MobileParty.NavigationType Nav;
            public Dictionary<int, NavigationCacheElement<Settlement>> Faces;
            public int Before, MaxKey, Verdict, Filled, Candidates;
            public readonly List<int> New = new List<int>();
            public int[] Excluded; public List<Settlement> All; public int Cost0, Cost1; public bool Ready;   // to, czym generator liczy te tablice
        }

        /// <summary>
        /// Kontrola (tylko log, recenzja 07.10): co ktora sciana z wpisem Default z PLIKU (generator edytora) liczona jeszcze raz tym samym kodem
        /// generatora w grze. Wysoka zgodnosc = w grze kod generatora liczy jak edytor mapy (te same wykluczone tereny, osady, droga silnika),
        /// wiec dopisane wpisy sa takie, jakie dalby generator. Nieliczne roznice moga byc przy scianach przerobionych po pamieci drog.
        /// </summary>
        internal const int ControlStride = 97;

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = typeof(NavigationCache<Settlement>);
                _fFaces = t.GetField("_closestSettlementsToFaceIndices", Inst);
                _pNav = t.GetProperty("_navigationType", Inst);
                _mCount = t.GetMethod("GetNavMeshFaceCount", Inst);
                _mCenter = t.GetMethod("GetNavMeshFaceCenterPosition", Inst);
                _mRecord = t.GetMethod("GetFaceRecordAtIndex", Inst);
                _mExcluded = t.GetMethod("GetExcludedFaceIds", Inst);
                _mCost0 = t.GetMethod("GetRegionSwitchCostTo0", Inst);
                _mCost1 = t.GetMethod("GetRegionSwitchCostTo1", Inst);
                _mAll = t.GetMethod("GetAllRegisteredSettlements", Inst);
                _mClosest = t.GetMethod("GetClosestSettlementToPosition", Inst);
                var load = AccessTools.Method(typeof(Campaign), "LoadMapScene");
                var miss = new List<string>();
                if (_fFaces == null || _fFaces.FieldType != typeof(Dictionary<int, NavigationCacheElement<Settlement>>)) miss.Add("_closestSettlementsToFaceIndices");
                if (_pNav == null) miss.Add("_navigationType");
                if (_mCount == null) miss.Add("GetNavMeshFaceCount");
                if (_mCenter == null) miss.Add("GetNavMeshFaceCenterPosition");
                if (_mRecord == null) miss.Add("GetFaceRecordAtIndex");
                if (_mExcluded == null) miss.Add("GetExcludedFaceIds");
                if (_mCost0 == null || _mCost1 == null) miss.Add("GetRegionSwitchCostTo0/1");
                if (_mAll == null) miss.Add("GetAllRegisteredSettlements");
                if (_mClosest == null || _mClosest.GetParameters().Length != 9) miss.Add("GetClosestSettlementToPosition(9)");
                if (load == null) miss.Add("Campaign.LoadMapScene");
                if (miss.Count > 0)
                {
                    Log.Info("RoadMemoryFix: BRAK w kodzie gry: " + string.Join(", ", miss) + " - pamiec drog mapy zostaje jak w plikach mapy (CartTownExit dalej dziala).");
                    return;
                }
                h.Patch(load, postfix: new HarmonyMethod(typeof(RoadMemoryFix), nameof(AfterMapLoad)));
                var s = Settings.Current;
                Log.Info("RoadMemoryFix: wpiete po wczytaniu mapy (postfiks Campaign.LoadMapScene) - sciany siatki bez wpisu w pamieci drog dostana najblizsze wejscie wedlug generatora gry ("
                         + (s != null && s.MapRoadTableFix ? "CZYNNE" : "WYLACZONE") + ").");
            }
            catch (Exception e) { Log.Error("RoadMemoryFix.ApplyAll", e); }
        }

        /// <summary>Postfiks na Campaign.LoadMapScene: mapa wczytana, pamiec drog zarejestrowana, partie jeszcze stoja.</summary>
        public static void AfterMapLoad()
        {
            try { Run(); }
            catch (Exception e) { Log.Error("RoadMemoryFix.Run", e); }
        }

        private static string Name(Settlement s)
        {
            try { return s != null && s.Name != null ? s.Name.ToString() : "?"; } catch { return "?"; }
        }

        private static string Kind(Settlement s)
        {
            return s == null ? "" : s.IsTown ? "" : s.IsCastle ? " (zamek)" : s.IsVillage ? " (wies)" : s.IsHideout ? " (kryjowka)" : "";
        }

        /// <summary>Pamieci drog zarejestrowane w aktywnym modelu odleglosci (NavalDLC: slownik rodzaj -> pamiec; gra: jedno pole; model owiniety - BaseModel).</summary>
        private static List<NavigationCache<Settlement>> FindCaches(object model)
        {
            var res = new List<NavigationCache<Settlement>>();
            var seen = new HashSet<object>();
            for (int depth = 0; model != null && depth < 6 && seen.Add(model); depth++)
            {
                for (var t = model.GetType(); t != null && t != typeof(object); t = t.BaseType)
                {
                    foreach (var f in t.GetFields(Inst | BindingFlags.DeclaredOnly))
                    {
                        object v;
                        try { v = f.GetValue(model); } catch { continue; }
                        var c = v as NavigationCache<Settlement>;
                        if (c != null) { if (!res.Contains(c)) res.Add(c); continue; }
                        var d = v as IDictionary;
                        if (d == null) continue;
                        foreach (var x in d.Values) { var c2 = x as NavigationCache<Settlement>; if (c2 != null && !res.Contains(c2)) res.Add(c2); }
                    }
                }
                object next = null;
                try { var bp = AccessTools.Property(model.GetType(), "BaseModel"); next = bp != null ? bp.GetValue(model, null) : null; } catch { }
                model = next;
            }
            return res;
        }

        /// <summary>
        /// Uzupelnia tablice sciana -> najblizsze wejscie dla scian siatki, ktorych generator mapy nie widzial. Wolane raz po wczytaniu
        /// mapy; drugie wywolanie na tej samej pamieci drog nie ma juz czego dodac.
        /// </summary>
        internal static void Run()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null || _fFaces == null) return;
            var sw = Stopwatch.StartNew();
            var model = Campaign.Current.Models != null ? Campaign.Current.Models.MapDistanceModel : null;
            var caches = FindCaches(model);
            if (caches.Count == 0)
            {
                Log.Info("Pamiec drog mapy: BRAK pamieci drog w modelu odleglosci " + (model != null ? model.GetType().FullName : "null") + " - nic nie ruszam.");
                return;
            }
            var tabs = new List<Tab>();
            int maxSeen = -1;
            foreach (var c in caches)
            {
                var tab = new Tab { Cache = c, Faces = (Dictionary<int, NavigationCacheElement<Settlement>>)_fFaces.GetValue(c) };
                try { tab.Nav = (MobileParty.NavigationType)_pNav.GetValue(c, null); } catch { tab.Nav = MobileParty.NavigationType.None; }
                if (tab.Faces == null) continue;
                tab.Before = tab.Faces.Count;
                tab.MaxKey = tab.Faces.Count > 0 ? tab.Faces.Keys.Max() : -1;
                if (tab.MaxKey > maxSeen) maxSeen = tab.MaxKey;
                tabs.Add(tab);
            }
            if (tabs.Count == 0) return;
            tabs.Sort((a, b) => ((int)a.Nav).CompareTo((int)b.Nav));
            int faceCount = (int)_mCount.Invoke(tabs[0].Cache, null);
            var def = tabs.FirstOrDefault(x => x.Nav == MobileParty.NavigationType.Default) ?? tabs[0];

            // sprawdzian indeksow: brama osady lezy na scianie, ktorej wpis wskazuje TE SAMA osade (ROT: 782 z 796 bram osad bez kryjowek) -
            // jesli nie, indeksy siatki nie pasuja do pamieci drog i dopisane wpisy bylyby bez sensu - nic nie ruszamy
            int gates = 0, self = 0;
            var staleBefore = new List<Settlement>();
            foreach (var st in Settlement.All)
            {
                if (st == null) continue;
                int f;
                try { f = st.GatePosition.Face.FaceIndex; } catch { continue; }
                if (f < 0) continue;
                NavigationCacheElement<Settlement> e;
                if (def.Faces.TryGetValue(f, out e)) { if (!st.IsHideout) { gates++; if (e.Settlement == st) self++; } }
                else staleBefore.Add(st);
            }
            bool indicesOk = gates > 0 && self * 4 >= gates * 3;

            bool on = s.MapRoadTableFix;
            int stumbles = 0;
            bool errLogged = false;
            int span = Math.Max(0, faceCount - maxSeen - 1);
            if (on && indicesOk)
            {
                foreach (var tab in tabs)
                {
                    int[] excluded; List<Settlement> all; int cost0, cost1;
                    try
                    {
                        excluded = (int[])_mExcluded.Invoke(tab.Cache, null);
                        all = (List<Settlement>)_mAll.Invoke(tab.Cache, null);
                        cost0 = (int)_mCost0.Invoke(tab.Cache, null);
                        cost1 = (int)_mCost1.Invoke(tab.Cache, null);
                        tab.Excluded = excluded; tab.All = all; tab.Cost0 = cost0; tab.Cost1 = cost1; tab.Ready = true;
                    }
                    catch (Exception e) { stumbles++; if (!errLogged) { errLogged = true; Log.Error("RoadMemoryFix.Run (" + tab.Nav + ")", e); } continue; }
                    // skrot gry (useEarlyOut, jak w CheckBeingNeighbor): osady ida wedlug odleglosci w linii prostej, a droga siatki nigdy nie jest
                    // krotsza od linii prostej, wiec przerwanie, gdy linia prosta > najlepsza droga + 25, daje TEN SAM wynik co pelny przeglad
                    // generatora. Dla Default (kolejnosc i skrot wedlug bramy) i Naval (wedlug portu) to sie zgadza; dla All pamiec drog w grze
                    // (SandBoxNavigationCache) ustawia osady wedlug bramy, a skrot liczy min(brama, port) - tam pelny przeglad jak w generatorze
                    bool early = tab.Nav != MobileParty.NavigationType.All;
                    for (int i = maxSeen + 1; i < faceCount; i++)
                    {
                        if (tab.Faces.ContainsKey(i)) continue;
                        tab.Candidates++;
                        try
                        {
                            // to samo, co petla generatora gry (NavigationCache.GenerateClosestSettlementToFaceCache) dla sciany i
                            var center = (Vec2)_mCenter.Invoke(tab.Cache, new object[] { i });
                            var rec = (PathFaceRecord)_mRecord.Invoke(tab.Cache, new object[] { i });
                            var args = new object[] { center, rec, excluded, all, cost0, cost1, float.MaxValue, false, early };
                            var best = _mClosest.Invoke(tab.Cache, args) as Settlement;
                            if (best == null) continue;
                            tab.Faces.Add(i, new NavigationCacheElement<Settlement>(best, (bool)args[7]));
                            tab.Filled++; tab.New.Add(i);
                        }
                        catch (Exception e) { stumbles++; if (!errLogged) { errLogged = true; Log.Error("RoadMemoryFix.Run (" + tab.Nav + ", sciana " + i + ")", e); } }
                    }
                }
            }
            sw.Stop();

            // kontrola generatora w grze (tylko log; tylko gdy cos dopisywalismy): sciany 0..maxSeen co ControlStride z wpisem Default z pliku
            // -> ten sam kod generatora (Default, skrot useEarlyOut) w grze; nic nie zapisuje (zapytania drogi silnika i odczyt tablicy)
            int ctlN = 0, ctlOk = 0; long ctlMs = 0;
            var ctlDiff = new List<string>();
            if (on && indicesOk && span > 0 && def.Ready && def.Nav == MobileParty.NavigationType.Default)
            {
                var sw2 = Stopwatch.StartNew();
                for (int i = 0; i <= maxSeen && i < faceCount; i += ControlStride)
                {
                    NavigationCacheElement<Settlement> e;
                    if (!def.Faces.TryGetValue(i, out e)) continue;
                    ctlN++;
                    try
                    {
                        var center = (Vec2)_mCenter.Invoke(def.Cache, new object[] { i });
                        var rec = (PathFaceRecord)_mRecord.Invoke(def.Cache, new object[] { i });
                        var args = new object[] { center, rec, def.Excluded, def.All, def.Cost0, def.Cost1, float.MaxValue, false, true };
                        var best = _mClosest.Invoke(def.Cache, args) as Settlement;
                        if (best == e.Settlement) ctlOk++;
                        else if (ctlDiff.Count < 6) ctlDiff.Add(i + " (teren " + rec.FaceGroupIndex + ") " + Name(e.Settlement) + " -> " + (best != null ? Name(best) : "brak"));
                    }
                    catch (Exception ex) { stumbles++; if (!errLogged) { errLogged = true; Log.Error("RoadMemoryFix.Run (kontrola, sciana " + i + ")", ex); } }
                }
                sw2.Stop();
                ctlMs = sw2.ElapsedMilliseconds;
            }
            foreach (var tab in tabs)
            {
                int lim = Math.Min(maxSeen, faceCount - 1);
                int have = 0;
                foreach (var k in tab.Faces.Keys) if (k <= lim) have++;
                tab.Verdict = lim + 1 - have;
            }

            // bramy naprawione: osady, ktorych brama byla na scianie bez wpisu (Default), a teraz wpis ma
            var fixedNow = new List<string>();
            var still = new List<string>();
            foreach (var st in staleBefore)
            {
                int f;
                try { f = st.GatePosition.Face.FaceIndex; } catch { continue; }
                NavigationCacheElement<Settlement> e;
                if (def.Faces.TryGetValue(f, out e)) fixedNow.Add(Name(st) + Kind(st) + " [sciana " + f + " -> " + Name(e.Settlement) + "]");
                else still.Add(Name(st) + Kind(st) + " [sciana " + f + "]");
            }

            var sb = new StringBuilder();
            sb.Append("Pamiec drog mapy (MapRoadTableFix ").Append(on ? "CZYNNE" : "WYLACZONE").Append("): siatka mapy ").Append(faceCount)
              .Append(" scian; tablica sciana -> najblizsze wejscie:");
            foreach (var tab in tabs)
                sb.Append(" ").Append(tab.Nav).Append(" ").Append(tab.Before).Append(" wpisow (najwyzsza sciana ").Append(tab.MaxKey).Append(")").Append(tab == tabs[tabs.Count - 1] ? ";" : ",");
            sb.Append(" generator mapy widzial sciany 0-").Append(maxSeen).Append(" - brak wpisu tam to jego werdykt (zadna osada w zasiegu drogi, bez zmian):");
            foreach (var tab in tabs) sb.Append(" ").Append(tab.Nav).Append(" ").Append(tab.Verdict);
            if (span == 0) sb.Append("; zadna sciana siatki nie powstala po pamieci drog - nie ma czego uzupelniac");
            else
            {
                sb.Append("; sciany ").Append(maxSeen + 1).Append("-").Append(faceCount - 1).Append(" (").Append(span).Append(") powstaly po pamieci drog");
                if (!indicesOk)
                    sb.Append(" - NIE RUSZAM: indeksy siatki nie pasuja do pamieci drog (brama wskazuje swoja osade: ").Append(self).Append(" z ").Append(gates).Append(")");
                else if (!on)
                    sb.Append(" - uzupelnianie wylaczone");
                else
                {
                    sb.Append(" - uzupelnione generatorem gry:");
                    foreach (var tab in tabs) sb.Append(" ").Append(tab.Nav).Append(" ").Append(tab.Filled).Append(" z ").Append(tab.Candidates);
                    var per = new Dictionary<string, int>();
                    foreach (var i in def.New) { var n = Name(def.Faces[i].Settlement); int c; per.TryGetValue(n, out c); per[n] = c + 1; }
                    if (per.Count > 0) sb.Append(" [").Append(def.Nav).Append(": ").Append(string.Join(", ", per.OrderByDescending(x => x.Value).Select(x => x.Key + " " + x.Value))).Append("]");
                }
            }
            sb.Append("; bramy bez wpisu przed: ").Append(staleBefore.Count)
              .Append(", naprawione: ").Append(fixedNow.Count > 0 ? string.Join(", ", fixedNow) : "zadna")
              .Append(", nadal bez wpisu: ").Append(still.Count > 0 ? string.Join(", ", still) : "zadna")
              .Append("; sprawdzian indeksow: brama wskazuje swoja osade ").Append(self).Append(" z ").Append(gates);
            if (ctlN > 0)
            {
                sb.Append("; kontrola generatora w grze (co ").Append(ControlStride).Append(". sciana z wpisem Default z pliku, liczona jeszcze raz w grze): zgodnych ")
                  .Append(ctlOk).Append(" z ").Append(ctlN);
                if (ctlDiff.Count > 0) sb.Append(" (rozne: ").Append(string.Join(", ", ctlDiff)).Append(ctlN - ctlOk > ctlDiff.Count ? ", ..." : "").Append(")");
                if (ctlOk * 10 < ctlN * 9) sb.Append(" - UWAGA: ponizej 90%, generator w grze liczy inaczej niz edytor mapy - dopisane wpisy niepewne (zglosic)");
                sb.Append(", ").Append(ctlMs).Append(" ms");
            }
            sb.Append("; model odleglosci ").Append(model != null ? model.GetType().Name : "null")
              .Append("; ").Append(sw.ElapsedMilliseconds).Append(" ms, potkniecia ").Append(stumbles).Append(".");
            Log.Info(sb.ToString());

            // szczegoly (raz po wczytaniu): kazda dopisana sciana -> osada, dla porownania z sasiednimi scianami w probie
            if (on && indicesOk && tabs.Any(x => x.Filled > 0))
            {
                var d = new StringBuilder("Pamiec drog mapy: dopisane sciany ->");
                foreach (var tab in tabs)
                {
                    if (tab.Filled == 0) { d.Append(" ").Append(tab.Nav).Append(": -;"); continue; }
                    d.Append(" ").Append(tab.Nav).Append(": ");
                    d.Append(string.Join(", ", tab.New.Select(i => i + " " + Name(tab.Faces[i].Settlement) + (tab.Faces[i].IsPortUsed ? " (port)" : ""))));
                    d.Append(";");
                }
                Log.Info(d.ToString());
            }
        }
    }
}
