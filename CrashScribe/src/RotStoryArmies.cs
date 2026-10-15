using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Map;

namespace CrashScribe
{
    /// <summary>
    /// WYWROTKA d767 TESTU 6-LETNIEGO (15.10, Jeff "napraw religie, sprawdz, o co chodzi, ze gra sie wywala"). Pierwotny wyjatek (CrashScribe
    /// "PIERWOTNY WYJATEK CRASHA", session-2026-10-14_19-32-07.log): NullReferenceException w GatherArmyLogEntry..ctor <- DefaultLogsCampaignBehavior
    /// .OnArmyGathered <- GatherArmyAction.Apply <- ROT.Events.BlackwaterSiegeEvent.SetupSiegeAttackers <- ROTEventBehavior.OnHourlyTick. Potem okno
    /// raportu BLSE / ButterLib samo pada (0xC0000005 w clr) i gra wisi - to tylko skutek.
    /// Przyczyna: ROT (fabula "Battle of the Blackwater") wola ROTKingdoms.Dragonstone.CreateArmy(Stannis, ...) i zaklada, ze armia powstala;
    /// potem GatherArmyAction.Apply(party Stannisa) - gdy armii nie ma, dziennik gry siega do army.LeaderParty i pada. Kingdom.CreateArmy NIE tworzy
    /// armii, gdy wodz jest nieaktywny (gra) albo prefiks BK CreateArmyPatch (AIArmyFormation) zwroci false z BKArmyManagementModel.CanCreateArmy.
    /// Religia BK (wlaczona 14.10) tylko zmieniala los od zapisu d720 - bez niej w tamtym biegu armia akurat powstala.
    /// Naprawa (klucz RotStoryArmyGuard, domyslnie true):
    ///  (1) znacznik na czas tickow wydarzen ROT (ROTEventBehavior.OnHourlyTick / OnDailyTick - prefiks + finalizer); postfiks Kingdom.CreateArmy:
    ///      gdy w wydarzeniu ROT armia nie powstala, a wodz jest aktywny i ma druzyne bez armii - tworzymy ja jak gra (new Army, Gather,
    ///      OnArmyCreated) z pominieciem bramki BK (fabula ROT musi miec swoja armie); linia w logu;
    ///  (2) prefiks GatherArmyAction.Apply: druzyna bez armii (albo null) - pomijamy (gra by padla na dzienniku) i piszemy linie.
    /// </summary>
    internal static class RotStoryArmies
    {
        private const string HarmonyId = "com.jeff.crashscribe";
        private static bool _installed;
        private static int _inRotEvent, _forced, _skipped, _stumbles;
        private static string _state = "nie wpiete";

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5 || _stumbles % 100 == 0) { try { Scribe.Report("CrashScribe", e, "RotStoryArmies." + where + " (potkniecie " + _stumbles + ")", null); } catch { } }
        }

        /// <summary>Wpiecie raz na proces (OnSessionLaunched) i linia startu.</summary>
        internal static void OnSession()
        {
            try { Install(); } catch (Exception e) { _state = "wyjatek " + e.GetType().Name; Stumble("Install", e); }
            try { Scribe.Line("Armie fabuly ROT (Blackwater): " + _state + " | klucz RotStoryArmyGuard " + (Config.RotStoryArmyGuard ? "TAK" : "NIE") + " | od startu procesu wymuszonych armii " + _forced + ", pominietych zbiorek bez armii " + _skipped + "."); }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            if (!Config.RotStoryArmyGuard) { _state = "WYLACZONE (klucz)"; return; }
            var h = new Harmony(HarmonyId);
            int ok = 0, want = 0;
            var evT = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTEventBehavior");
            foreach (var name in new[] { "OnHourlyTick", "OnDailyTick" })
            {
                var m = evT != null ? AccessTools.Method(evT, name) : null;
                if (m == null) continue;
                want++;
                try
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(RotStoryArmies), nameof(EventPrefix)), finalizer: new HarmonyMethod(typeof(RotStoryArmies), nameof(EventFinalizer)));
                    ok++;
                }
                catch (Exception e) { Stumble("Patch(" + name + ")", e); }
            }
            var mCreate = AccessTools.Method(typeof(Kingdom), "CreateArmy");
            if (mCreate != null)
            {
                want++;
                try { h.Patch(mCreate, postfix: new HarmonyMethod(typeof(RotStoryArmies), nameof(CreateArmyPostfix)) { priority = Priority.Last }); ok++; }
                catch (Exception e) { Stumble("Patch(Kingdom.CreateArmy)", e); }
            }
            var mGather = AccessTools.Method(typeof(GatherArmyAction), "Apply", new[] { typeof(MobileParty), typeof(IMapPoint) });
            if (mGather != null)
            {
                want++;
                try { h.Patch(mGather, prefix: new HarmonyMethod(typeof(RotStoryArmies), nameof(GatherPrefix)) { priority = Priority.First }); ok++; }
                catch (Exception e) { Stumble("Patch(GatherArmyAction.Apply)", e); }
            }
            _state = "wpiete " + ok + "/" + want + (evT == null ? " (BRAK ROTEventBehavior - tylko oslona zbiorki)" : "");
        }

        public static void EventPrefix() { _inRotEvent++; }

        public static Exception EventFinalizer(Exception __exception)
        {
            if (_inRotEvent > 0) _inRotEvent--;
            return __exception;
        }

        /// <summary>Postfiks Kingdom.CreateArmy: w wydarzeniu ROT armia nie powstala (bramka BK albo gra) - tworzymy ja jak gra.</summary>
        public static void CreateArmyPostfix(Kingdom __instance, Hero armyLeader, Settlement targetSettlement, Army.ArmyTypes selectedArmyType)
        {
            try
            {
                if (_inRotEvent <= 0 || !Config.RotStoryArmyGuard || __instance == null || armyLeader == null) return;
                var party = armyLeader.PartyBelongedTo;
                if (party == null || party.Army != null || party.LeaderHero != armyLeader) return;
                if (!armyLeader.IsActive || armyLeader == Hero.MainHero) return;
                var army = new Army(__instance, party, selectedArmyType);
                army.Gather(targetSettlement);
                CampaignEventDispatcher.Instance.OnArmyCreated(army);
                _forced++;
                Scribe.Line("Armie fabuly ROT: armia " + armyLeader.Name + " (" + __instance.Name + ", " + selectedArmyType + ", cel " + (targetSettlement != null ? targetSettlement.Name.ToString() : "-")
                            + ") nie powstala w Kingdom.CreateArmy (bramka BK albo gra) - utworzona jak w grze, zeby wydarzenie ROT mialo swoja armie.");
            }
            catch (Exception e) { Stumble("CreateArmyPostfix", e); }
        }

        /// <summary>Prefiks GatherArmyAction.Apply: druzyna bez armii - pomijamy (dziennik gry padlby na army.LeaderParty).</summary>
        public static bool GatherPrefix(MobileParty leaderParty)
        {
            try
            {
                if (!Config.RotStoryArmyGuard) return true;
                if (leaderParty != null && leaderParty.Army != null && leaderParty.Army.LeaderParty != null) return true;
                _skipped++;
                Scribe.Line("Armie fabuly ROT: GatherArmyAction.Apply bez armii (" + (leaderParty != null ? leaderParty.Name.ToString() : "null") + (_inRotEvent > 0 ? ", w wydarzeniu ROT" : "")
                            + ") - pominiete (gra padlaby na dzienniku GatherArmyLogEntry).");
                return false;
            }
            catch (Exception e) { Stumble("GatherPrefix", e); return true; }
        }
    }

    /// <summary>15.10: wpiecie oslony armii fabuly ROT przy starcie sesji. Bez zapisu.</summary>
    internal sealed class RotStoryArmiesBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { RotStoryArmies.OnSession(); });
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
