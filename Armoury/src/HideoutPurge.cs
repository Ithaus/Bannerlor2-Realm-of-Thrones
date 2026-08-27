using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// PRZETRZEBIONA KRYJOWKA (Jeff 27.08): "jak pokonam kryjowke, to nie od
    /// razu nagroda - musze ja PRZESZUKAC, jak po bitwie. W kryjowce bandytow
    /// powinno byc zrabowane zloto z okolicznych napasci. I renown powinien
    /// pojsc, i reputacja w poblizu - im blizej, tym wiecej. To big deal jest."
    /// Po zwycieskiej bitwie w kryjowce otwiera sie menu przeszukania:
    ///  - "Search the hideout": zloto = baza + stawka x liczba rozbitych band
    ///    (kazda banda zyla z rozboju - tyle zdazyla zlupic okolicy), +-25%;
    ///    renown dla bohatera; wdziecznosc okolicy - notable wiosek, miast
    ///    i zamkow w okregu dostaja relacje od RepMax przy samej kryjowce
    ///    malejaco do zera na skraju okregu;
    ///  - "Leave without searching": big deal zostawiony szczurom.
    /// Vanillowego lupu przedmiotowego nie ruszamy - to nagroda Z WIERZCHU;
    /// nasze zloto i slawa leza GLEBIEJ i trzeba sie schylic.
    /// </summary>
    internal sealed class HideoutPurge : CampaignBehaviorBase
    {
        private static bool _pending;
        private static int _pendingGold;
        private static int _pendingBands;
        private static string _pendingName = "";
        private static CampaignVec2 _pendingPos;
        private static bool _pendingHasPos;   // bez osady nie ma okregu wdziecznosci

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddMenus);
            CampaignEvents.OnHideoutBattleCompletedEvent.AddNonSerializedListener(this, OnHideoutBattle);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnHideoutBattle(BattleSideEnum winnerSide, HideoutEventComponent component,
                                     HideoutEventComponent.HideoutBattleEndState endState)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.HideoutPurgeEnabled) return;
                if (endState != HideoutEventComponent.HideoutBattleEndState.Victory) return;
                var mapEvent = component != null ? component.MapEvent : null;
                if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;
                if (mapEvent.PlayerSide != winnerSide) return;

                int bands = 1;
                try
                {
                    var side = mapEvent.GetMapEventSide(winnerSide == BattleSideEnum.Attacker
                        ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
                    if (side != null && side.Parties != null && side.Parties.Count > 0) bands = side.Parties.Count;
                }
                catch { }

                var sett = mapEvent.MapEventSettlement;
                _pendingHasPos = sett != null;
                if (sett != null) _pendingPos = sett.GatePosition;
                _pendingName = sett != null && sett.Name != null ? sett.Name.ToString() : "the hideout";
                _pendingBands = bands;
                float spread = 0.75f + MBRandom.RandomFloat * 0.5f;   // +-25%
                _pendingGold = (int)MathF.Max(0f,
                    (Math.Max(0, c.HideoutGoldBase) + Math.Max(0, c.HideoutGoldPerBand) * bands) * spread);
                _pending = true;
                Log.Info("HideoutPurge: zwyciestwo w " + _pendingName + ", band " + bands + ", lup " + _pendingGold + " zlota czeka na przeszukanie.");
            }
            catch (Exception e) { Log.Error("HideoutPurge.OnHideoutBattle", e); }
        }

        private void OnTick(float dt)
        {
            try
            {
                if (!_pending || Campaign.Current == null) return;
                // menu dopiero na CZYSTEJ mapie - nigdy w srodku cudzego menu
                // ani rozliczania bitwy (lekcja z klawisza O w NightRest)
                var st = Game.Current != null && Game.Current.GameStateManager != null
                    ? Game.Current.GameStateManager.ActiveState as TaleWorlds.CampaignSystem.GameState.MapState : null;
                if (st == null || st.AtMenu) return;
                if (PlayerEncounter.Current != null || MobileParty.MainParty.MapEvent != null) return;
                _pending = false;
                GameMenu.ActivateGameMenu("arm_hideout_search");
            }
            catch (Exception e) { Log.Error("HideoutPurge.OnTick", e); _pending = false; }
        }

        private static CampaignTime _searchDone = CampaignTime.Zero;
        private static float _searchTotal = 2f;

        private void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenu("arm_hideout_search",
                    "{=!}The hideout lies quiet. Smoke drifts from the fires the bandits will not need again. Their den has not given up everything yet.",
                    null);
                starter.AddGameMenuOption("arm_hideout_search", "arm_hideout_do_search",
                    "{=!}Search the hideout",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Continue; return true; },
                    delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu("arm_hideout_search_wait"); }, false, 0);
                starter.AddGameMenuOption("arm_hideout_search", "arm_hideout_leave",
                    "{=!}Leave without searching",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a) { GameMenu.ExitToLast(); }, true, 1);

                // PASEK PRZESZUKANIA jak po bitwie (Jeff 27.08: "nie od razu
                // rzeczy - pasek ile to zajmie, wykorzystaj ten mechanizm").
                // Ten sam AddWaitGameMenu co sen w obozie; nagrody dopiero
                // po dojechaniu paska do konca.
                starter.AddWaitGameMenu("arm_hideout_search_wait",
                    "{=!}The men turn the hideout over - bedrolls, false floors, the cold ashes of the fire pits.",
                    SearchInit, delegate (MenuCallbackArgs a) { return true; }, null, SearchTick,
                    GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption);
                starter.AddGameMenuOption("arm_hideout_search_wait", "arm_hideout_search_stop",
                    "{=!}Call the search off",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu("arm_hideout_search"); }, true, 9);
            }
            catch (Exception e) { Log.Error("HideoutPurge.AddMenus", e); }
        }

        private static void SearchInit(MenuCallbackArgs args)
        {
            try
            {
                var c = Settings.Current;
                _searchTotal = Math.Max(0.5f, c != null ? c.HideoutSearchHours : 2f);
                _searchDone = CampaignTime.HoursFromNow(_searchTotal);
                args.MenuContext.GameMenu.StartWait();
            }
            catch (Exception e) { Log.Error("HideoutPurge.SearchInit", e); }
        }

        private static void SearchTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                float left = (float)(_searchDone - CampaignTime.Now).ToHours;
                if (left <= 0.01f)
                {
                    DoSearch();
                    GameMenu.ExitToLast();
                    return;
                }
                args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(
                    MathF.Clamp(1f - left / _searchTotal, 0f, 1f));
            }
            catch (Exception e) { Log.Error("HideoutPurge.SearchTick", e); }
        }

        private static void DoSearch()
        {
            try
            {
                var c = Settings.Current;
                if (_pendingGold > 0)
                {
                    Hero.MainHero.ChangeHeroGold(_pendingGold);
                    Log.Player("Buried under the bedrolls you find the plunder of " + _pendingBands
                               + " raiding band" + (_pendingBands > 1 ? "s" : "") + ": " + _pendingGold + " gold.", true);
                }

                float renown = Math.Max(0f, c.HideoutRenown);
                if (renown > 0.01f) GainRenownAction.Apply(Hero.MainHero, renown);

                // wdziecznosc okolicy: im blizej kryjowki, tym wiecej
                int repMax = Math.Max(0, c.HideoutRepMax);
                float radius = Math.Max(1f, c.HideoutRepRadius);
                int touched = 0, best = 0;
                if (repMax > 0 && _pendingHasPos)
                {
                    foreach (var s in Settlement.All)
                    {
                        try
                        {
                            if (s == null || s.IsHideout) continue;
                            if (!s.IsVillage && !s.IsTown && !s.IsCastle) continue;
                            float d = s.GatePosition.Distance(_pendingPos);
                            if (d > radius) continue;
                            int rel = (int)MathF.Round(repMax * (1f - d / radius));
                            if (rel <= 0) continue;
                            bool any = false;
                            foreach (var notable in s.Notables)
                            {
                                if (notable == null || !notable.IsAlive) continue;
                                try
                                {
                                    ChangeRelationAction.ApplyPlayerRelation(notable, rel, false, false);
                                    any = true;
                                }
                                catch { }
                            }
                            if (any) { touched++; if (rel > best) best = rel; }
                        }
                        catch { }
                    }
                }
                if (touched > 0)
                    Log.Player("Word of the purge spreads: the folk of " + touched + " nearby settlement"
                               + (touched > 1 ? "s" : "") + " think better of you (up to +" + best + ").", true);
                Log.Info("HideoutPurge: przeszukano " + _pendingName + " - zloto " + _pendingGold
                         + ", renown " + renown.ToString("0.#") + ", osad z wdziecznoscia " + touched + ".");
            }
            catch (Exception e) { Log.Error("HideoutPurge.DoSearch", e); }
        }
    }
}
