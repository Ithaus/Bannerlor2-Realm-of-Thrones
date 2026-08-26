using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.TournamentGames;

namespace GrandTourney
{
    /// <summary>
    /// Dopoki goscie jada, turniej nie moze sam sie rozejsc po kosciach.
    /// UWAGA: TournamentModel.GetTournamentEndChance jest abstrakcyjna - Harmony jej nie zalata.
    /// Trzeba zlapac kazda konkretna implementacje, takze te z innych modow (TournamentsXPanded, ROT).
    /// </summary>
    internal static class NoEarlyEndPatch
    {
        internal static void Postfix(TournamentGame tournament, ref float __result)
        {
            try
            {
                if (tournament == null || tournament.Town == null) return;
                var b = TourneyBehavior.Instance;
                if (b != null && b.IsGathering(tournament.Town)) __result = 0f;
            }
            catch (Exception e) { Log.Error("NoEarlyEndPatch", e); }
        }

        /// <summary>Laty rzucane recznie na wszystkie zaladowane implementacje modelu turnieju.</summary>
        internal static void ApplyAll(Harmony harmony)
        {
            int done = 0;
            var post = new HarmonyMethod(typeof(NoEarlyEndPatch).GetMethod(
                "Postfix", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public));

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !typeof(TournamentModel).IsAssignableFrom(t)) continue;
                    try
                    {
                        var m = t.GetMethod("GetTournamentEndChance",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (m == null || m.IsAbstract) continue;
                        harmony.Patch(m, postfix: post);
                        done++;
                        Log.Info("Lata na koniec turnieju: " + t.FullName);
                    }
                    catch (Exception e) { Log.Error("ApplyAll(" + t.FullName + ")", e); }
                }
            }
            if (done == 0) Log.Error("ApplyAll: nie znaleziono zadnej konkretnej implementacji TournamentModel.", null);
        }
    }
}

namespace GrandTourney
{
    /// <summary>
    /// Dopoki heroldowie jada, a rycerze sa w drodze, listy sa zamkniete.
    /// Bez tego gracz wchodzi w turniej w trakcie zjazdu i widzi na arenie samych rekrutow.
    /// </summary>
    internal static class ClosedListsPatch
    {
        private static bool Gathering()
        {
            try
            {
                var s = TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement;
                if (s == null || s.Town == null) return false;
                var b = TourneyBehavior.Instance;
                return b != null && b.IsGathering(s.Town);
            }
            catch { return false; }
        }

        internal static void JoinPostfix(TaleWorlds.CampaignSystem.GameMenus.MenuCallbackArgs args, ref bool __result)
        {
            try
            {
                if (!__result || !Gathering()) return;
                args.IsEnabled = false;
                args.Tooltip = new TaleWorlds.Localization.TextObject(
                    "{=!}The lists are not open. The heralds have ridden out and the knights are still on the road.");
            }
            catch (Exception e) { Log.Error("JoinPostfix", e); }
        }

        internal static void WatchPostfix(TaleWorlds.CampaignSystem.GameMenus.MenuCallbackArgs args, ref bool __result)
        {
            try
            {
                if (!__result || !Gathering()) return;
                args.IsEnabled = false;
                args.Tooltip = new TaleWorlds.Localization.TextObject(
                    "{=!}Nothing to watch yet - the tourney has been proclaimed but not opened.");
            }
            catch (Exception e) { Log.Error("WatchPostfix", e); }
        }

        internal static void ApplyAll(HarmonyLib.Harmony h)
        {
            Hook(h, "game_menu_join_tournament_on_condition", "JoinPostfix");
            Hook(h, "game_menu_tournament_watch_on_condition", "WatchPostfix");
        }

        private static void Hook(HarmonyLib.Harmony h, string method, string postfix)
        {
            try
            {
                var t = typeof(TaleWorlds.CampaignSystem.TournamentGames.TournamentCampaignBehavior);
                var m = t.GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
                                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (m == null) { Log.Error("ClosedLists: brak metody " + method, null); return; }
                var p = typeof(ClosedListsPatch).GetMethod(postfix,
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                h.Patch(m, postfix: new HarmonyLib.HarmonyMethod(p));
                Log.Info("Listy zamkniete w trakcie zjazdu: " + method);
            }
            catch (Exception e) { Log.Error("ClosedLists.Hook(" + method + ")", e); }
        }
    }
}

namespace GrandTourney
{
    /// <summary>
    /// Szlachta walczy w szrankach. Vanilla NIBY bierze lordow z miasta,
    /// ale w praktyce (stack modow) drabinka potrafi byc pelna samych
    /// chlopow, mimo ze GT sciagnal do miasta dwudziestu rycerzy.
    /// Po zbudowaniu listy uczestnikow WSTAWIAMY obecnych w miescie lordow
    /// na miejsca wypelniaczy-zolnierzy (od konca), z diagnostyka w logu,
    /// zeby bylo widac, kogo i czemu odrzucono.
    /// </summary>
    internal static class NoblesFightPatch
    {
        internal static void Postfix(object __instance,
            TaleWorlds.CampaignSystem.Settlements.Settlement settlement,
            ref TaleWorlds.Library.MBList<TaleWorlds.CampaignSystem.CharacterObject> __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.NoblesFightInTournaments) return;
                if (__result == null || settlement == null || settlement.Town == null) return;

                var list = (System.Collections.Generic.List<TaleWorlds.CampaignSystem.CharacterObject>)__result;
                int comesOfAge = TaleWorlds.CampaignSystem.Campaign.Current.Models.AgeModel.HeroComesOfAge;

                // kandydaci: lordowie fizycznie obecni w miescie
                var cands = new System.Collections.Generic.List<TaleWorlds.CampaignSystem.Hero>();
                int wounded = 0, unskilled = 0, other = 0, present = 0;
                System.Action<TaleWorlds.CampaignSystem.Hero> consider = h =>
                {
                    try
                    {
                        if (h == null || !h.IsLord || h == TaleWorlds.CampaignSystem.Hero.MainHero || !h.IsAlive) return;
                        present++;
                        if (h.IsWounded || h.IsPrisoner) { wounded++; return; }
                        if (h.Age < (float)comesOfAge) { other++; return; }
                        if (!s.NoblesIgnoreSkillGate)
                        {
                            if (h.GetSkillValue(TaleWorlds.Core.DefaultSkills.OneHanded) < 100 &&
                                h.GetSkillValue(TaleWorlds.Core.DefaultSkills.TwoHanded) < 100) { unskilled++; return; }
                        }
                        if (list.Contains(h.CharacterObject)) return;
                        if (!cands.Contains(h)) cands.Add(h);
                    }
                    catch { other++; }
                };
                foreach (var mp in settlement.Parties) { if (mp != null) consider(mp.LeaderHero); }
                foreach (var h in settlement.HeroesWithoutParty) consider(h);

                int lordsIn = 0;
                foreach (var c in list) if (c != null && c.IsHero && c.HeroObject != null && c.HeroObject.IsLord) lordsIn++;

                int cap = System.Math.Max(0, System.Math.Min(15, s.MaxNoblesInBracket));
                int added = 0;
                foreach (var h in cands)
                {
                    if (lordsIn + added >= cap) break;
                    // wstaw na miejsce wypelniacza (zolnierza) od konca
                    int slot = -1;
                    for (int i = list.Count - 1; i >= 0; i--)
                        if (list[i] != null && !list[i].IsHero) { slot = i; break; }
                    if (slot >= 0) list[slot] = h.CharacterObject;
                    else if (list.Count < 16) list.Add(h.CharacterObject);
                    else break;
                    added++;
                }

                if (added > 0 || present > 0)
                    Log.Info("Szranki " + settlement.Name + ": lordow w miescie " + present +
                             ", w drabince bylo " + lordsIn + ", dolozono " + added +
                             (wounded > 0 ? ", rannych/jencow " + wounded : "") +
                             (unskilled > 0 ? ", bez wyszkolenia " + unskilled : "") +
                             (other > 0 ? ", innych odrzuconych " + other : "") + ".");
            }
            catch (Exception e) { Log.Error("NoblesFightPatch", e); }
        }

        internal static void ApplyAll(Harmony harmony)
        {
            int done = 0;
            var post = new HarmonyMethod(typeof(NoblesFightPatch).GetMethod(
                "Postfix", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public));
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !typeof(TournamentGame).IsAssignableFrom(t)) continue;
                    try
                    {
                        var m = t.GetMethod("GetParticipantCharacters",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (m == null || m.IsAbstract) continue;
                        harmony.Patch(m, postfix: post);
                        done++;
                        Log.Info("Szlachta w szrankach: " + t.FullName);
                    }
                    catch (Exception e) { Log.Error("NoblesFight.ApplyAll(" + t.FullName + ")", e); }
                }
            }
            if (done == 0) Log.Error("NoblesFight: nie znaleziono GetParticipantCharacters.", null);
        }
    }
}
