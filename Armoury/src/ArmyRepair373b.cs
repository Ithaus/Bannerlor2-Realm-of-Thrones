using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// 373b (crash Jeffa 09.11 19:43:08, Ironman): NRE w DefaultArmyManagementCalculationModel.CalculatePartyInfluenceCost &lt;- Army.ThinkAboutCohesionBoost
    /// &lt;- Army.HourlyTick. 373 wzial do lochu rannych wodzow partii (w tym wodza armii oblegajacej); TakePrisonerAction robi tylko RemovePartyLeader, armii nie rusza,
    /// wiec partia bez wodza zostala w armii, a gra deref-uje armyLeaderParty.LeaderHero (cs_full.cs:53618), LeaderParty.LeaderHero.Clan (BoostCohesionWithInfluence)
    /// i LeaderHero.GetPerkValue (CalculateTotalInfluenceCostInternal). Przyczyne zamyka SiegeWounded373 (wodzowie zostaja w partii); tu NAPRAWA ZAPISU i bezpieczniki:
    ///  - skan armii (pierwszy takt mapy po starcie sesji, raz na dobe, i natychmiast gdy bezpiecznik cos zlapal): wodz armii brak / martwy / w niewoli -&gt;
    ///    DisbandArmyAction.ApplyByUnknownReason (jak DisbandPartyAction.StartDisband gry) + StartDisband partii lorda bez wodza; czlonek bez wodza -&gt; Army = null
    ///    + StartDisband (jak DisbandPartyCampaignBehavior.OnGameLoadFinished). Armia z gracza w srodku - pomijana (gra sama prowadzi menu "army_dispersed").
    ///    Partia w bitwie (MapEvent) - odlozona do nastepnego skanu (rozwiazywanie partii w trakcie bitwy robi gra dopiero po MapEventSide = null).
    ///  - Prefix Army.HourlyTick: wodz armii brak -&gt; ta godzina pominieta + skan w nastepnym takcie (Campaign.Tick: CampaignEventDispatcher.Tick przed okresowymi).
    ///  - Prefix DefaultArmyManagementCalculationModel.CalculatePartyInfluenceCost (DeclaredMethod na klasie deklarujacej - pulapka 15.10): wodz armii albo czlonka brak
    ///    -&gt; AverageCallToArmyCost zamiast NRE (tak samo jak bezpiecznik BK) + skan.
    /// Latki wpinane w OnSessionLaunched (model kampanii - nie w SubModuleMain), raz na proces. Potkniecia per armia / partia, bez globalnego wylacznika.
    /// </summary>
    internal static class ArmyRepair373b
    {
        private const string Tag = "Armie bez wodza (373b): ";
        private const int ErrMax = 5;
        private static Harmony _h;
        private static bool _tried;
        private static string _wCost = "-", _wHour = "-";
        private static bool _due;
        private static string _why = "-";
        private static int _costHits, _hourHits, _errs;
        private static double _lastH = double.MinValue;

        /// <summary>Nowa kampania / wczytanie (RegisterEvents) - liczniki na kampanie.</summary>
        internal static void Reset()
        {
            _due = false; _why = "-"; _costHits = 0; _hourHits = 0; _errs = 0; _lastH = double.MinValue;
        }

        private static void Stumble(string where, Exception e, ref int s)
        {
            s++;
            if (_errs >= ErrMax) return;
            _errs++;
            Log.Error("ArmyRepair373b." + where, e);
        }

        private static void Due(string why)
        {
            if (_due) return;
            _due = true;
            _why = why;
        }

        internal static void SessionStart()
        {
            int s = 0;
            if (!_tried)
            {
                _tried = true;
                if (_h == null) _h = new Harmony("com.jeff.armoury.armyrepair373b");
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(DefaultArmyManagementCalculationModel), "CalculatePartyInfluenceCost", new[] { typeof(MobileParty), typeof(MobileParty) });
                    if (m == null) _wCost = "BRAK (metoda)";
                    else { _h.Patch(m, prefix: new HarmonyMethod(typeof(ArmyRepair373b), nameof(CostPre)) { priority = Priority.First }); _wCost = "TAK"; }
                }
                catch (Exception e) { _wCost = "BRAK (" + e.GetType().Name + ")"; Stumble("SessionStart.cost", e, ref s); }
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(Army), "HourlyTick");
                    if (m == null) _wHour = "BRAK (metoda)";
                    else { _h.Patch(m, prefix: new HarmonyMethod(typeof(ArmyRepair373b), nameof(HourPre)) { priority = Priority.First }); _wHour = "TAK"; }
                }
                catch (Exception e) { _wHour = "BRAK (" + e.GetType().Name + ")"; Stumble("SessionStart.hour", e, ref s); }
            }
            Log.Info(Tag + "start | bezpiecznik kosztu wezwania (DefaultArmyManagementCalculationModel.CalculatePartyInfluenceCost) " + _wCost
                     + " | bezpiecznik godziny armii (Army.HourlyTick) " + _wHour + " | skan armii w pierwszym takcie mapy.");
            Due("start sesji");
        }

        /// <summary>CampaignEvents.TickEvent - skan, gdy cos go zamowilo (start sesji / bezpiecznik).</summary>
        internal static void OnTick(float dt)
        {
            if (!_due) return;
            bool start = _why == "start sesji";
            double now = CampaignTime.Now.ToHours;
            if (!start && now - _lastH < 1.0) return;   // zamowienia z bezpiecznikow - najwyzej jeden skan na godzine gry (bez lawiny linii, gdy cos wola co klatke)
            _due = false;
            _lastH = now;
            Scan(_why, start);
        }

        internal static void Daily() { Scan("doba", false); }

        private static bool Gone(Hero h) { return h == null || !h.IsAlive || h.IsPrisoner; }

        private static void Scan(string why, bool always)
        {
            int armies = 0, dispersed = 0, detached = 0, disbanding = 0, withPlayer = 0, inBattle = 0, s = 0;
            try
            {
                var main = MobileParty.MainParty;
                foreach (var k in Kingdom.All.ToList())
                {
                    List<Army> list;
                    try { list = k != null && k.Armies != null ? k.Armies.ToList() : null; } catch (Exception e) { Stumble("Kingdom", e, ref s); continue; }
                    if (list == null) continue;
                    foreach (var a in list)
                    {
                        try
                        {
                            if (a == null) continue;
                            armies++;
                            var lp = a.LeaderParty;
                            if (lp == null || lp == main) continue;
                            bool hasMain = main != null && a.Parties.Contains(main);
                            if (Gone(lp.LeaderHero))
                            {
                                if (hasMain) { withPlayer++; continue; }
                                if (lp.MapEvent != null) { inBattle++; continue; }
                                DisbandArmyAction.ApplyByUnknownReason(a);
                                dispersed++;
                                if (lp.LeaderHero == null && lp.IsLordParty && lp.IsActive && !lp.IsDisbanding)
                                {
                                    try { DisbandPartyAction.StartDisband(lp); disbanding++; } catch (Exception e) { Stumble("Disband.leader", e, ref s); }
                                }
                                continue;
                            }
                            foreach (var p in a.Parties.ToList())
                            {
                                try
                                {
                                    if (p == null || p == lp || p == main || !Gone(p.LeaderHero)) continue;
                                    if (p.MapEvent != null) { inBattle++; continue; }
                                    p.Army = null;
                                    detached++;
                                    if (p.LeaderHero == null && p.IsLordParty && p.IsActive && !p.IsDisbanding) { DisbandPartyAction.StartDisband(p); disbanding++; }
                                }
                                catch (Exception e) { Stumble("Member", e, ref s); }
                            }
                        }
                        catch (Exception e) { Stumble("Army", e, ref s); }
                    }
                }
            }
            catch (Exception e) { Stumble("Scan", e, ref s); }
            // partia w bitwie - ponownie po bitwie: bezpiecznik godziny zamawia skan, gdy armia jest poza bitwa; inaczej skan dobowy
            if (always || dispersed + detached + withPlayer + inBattle + s > 0)
                Log.Info(Tag + why + " | armii " + armies + ", rozwiazane " + dispersed + ", partie odlaczone " + detached + ", rozwiazywane " + disbanding
                         + (withPlayer > 0 ? ", z graczem pominiete " + withPlayer : "") + (inBattle > 0 ? ", w bitwie odlozone " + inBattle : "")
                         + " | bezpieczniki w tej grze: koszt wezwania " + _costHits + ", godzina armii " + _hourHits + " | potkniecia " + s + ".");
        }

        // ================================================================ latki (bezpieczniki)
        public static bool CostPre(DefaultArmyManagementCalculationModel __instance, MobileParty __0, MobileParty __1, ref int __result)
        {
            try
            {
                if ((__0 != null && __0.LeaderHero != null) && (__1 != null && __1.LeaderHero != null)) return true;
                _costHits++;
                __result = __instance != null ? __instance.AverageCallToArmyCost : 20;
                Due("bezpiecznik kosztu wezwania");
                return false;
            }
            catch { return true; }
        }

        public static bool HourPre(Army __instance)
        {
            try
            {
                var lp = __instance != null ? __instance.LeaderParty : null;
                if (lp == null || lp == MobileParty.MainParty) return true;
                if (lp.LeaderHero == null)
                {
                    _hourHits++;
                    if (lp.MapEvent == null) Due("bezpiecznik godziny armii");
                    return false;
                }
                var ps = __instance.Parties;
                for (int i = 0; i < ps.Count; i++)
                {
                    var p = ps[i];
                    if (p != null && p != MobileParty.MainParty && p.LeaderHero == null && p.MapEvent == null) { Due("czlonek armii bez wodza"); break; }
                }
                return true;
            }
            catch { return true; }
        }
    }
}
