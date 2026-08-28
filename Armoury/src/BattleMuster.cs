using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace Armoury
{
    /// <summary>
    /// WYBOR SKLADU DO WSPOLNEJ BITWY - dokladnie ten sam widok co przy
    /// hideoucie (MenuContext.OpenTroopSelection, lista z licznikiem x/N),
    /// tylko limit = szacowane sloty gracza na scenie zamiast 15.
    /// Jeff 28.08: "jest gotowy widok jak wybieram 15 do hideout - zastosowac
    /// ten sam modul, tylko powiekszyc limit".
    /// Egzekucja bez zadnych trikow: wybrane ODDZIALY ida na GORE rosteru,
    /// a scena spawnuje od gory - wybrani wchodza w sloty pierwsi, reszta
    /// czeka w kolejce posilkow. Opcja tylko gdy po stronie gracza walczy
    /// ktos wiecej niz jego wlasna partia.
    /// </summary>
    internal sealed class BattleMuster : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSession);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSession(CampaignGameStarter starter)
        {
            try { AddOption(starter, "join_encounter"); } catch (Exception e) { Log.Error("BattleMuster.menu join_encounter", e); }
            try { AddOption(starter, "encounter"); } catch (Exception e) { Log.Error("BattleMuster.menu encounter", e); }
        }

        private void AddOption(CampaignGameStarter starter, string menuId)
        {
            starter.AddGameMenuOption(menuId, "arm_muster_" + menuId,
                "Hand-pick who takes the field",
                MusterCondition, MusterConsequence, false, 1, false);
        }

        private bool MusterCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            var main = MobileParty.MainParty;
            if (main == null || main.MemberRoster == null || main.MemberRoster.TotalManCount <= 1) return false;

            // wspolna bitwa = po naszej stronie walczy wiecej partii niz nasza
            var me = main.MapEvent;
            if (me != null)
            {
                try
                {
                    var side = me.PlayerSide == TaleWorlds.Core.BattleSideEnum.Attacker
                        ? me.AttackerSide : me.DefenderSide;
                    return side != null && side.Parties != null && side.Parties.Count > 1;
                }
                catch { return false; }
            }
            // jeszcze nie w MapEvencie: menu dolaczenia do trwajacej bitwy
            return args != null && args.MenuContext != null
                && args.MenuContext.GameMenu != null
                && args.MenuContext.GameMenu.StringId == "join_encounter";
        }

        private void MusterConsequence(MenuCallbackArgs args)
        {
            try
            {
                var main = MobileParty.MainParty;
                int slots = EstimateSlots(main);
                // preselekcja jak w hideoucie: najmocniejsi i priorytetowi
                var pre = TroopRoster.CreateDummyTroopRoster();
                try { pre.Add(Helpers.MobilePartyHelper.GetStrongestAndPriorTroops(main, slots, true)); }
                catch (Exception e) { Log.Error("BattleMuster.preselect", e); }
                args.MenuContext.OpenTroopSelection(main.MemberRoster, pre,
                    CanChangeStatusOfTroop, OnPicked, slots, 1);
            }
            catch (Exception e) { Log.Error("BattleMuster.Consequence", e); }
        }

        private static bool CanChangeStatusOfTroop(CharacterObject character)
        {
            return character != null && !character.IsPlayerCharacter;
        }

        /// <summary>Ile miejsc na scenie dostanie partia gracza - udzial liczebny
        /// w polowie Battle Size przypadajacej na nasza strone.</summary>
        private static int EstimateSlots(MobileParty main)
        {
            int mine = main.MemberRoster.TotalHealthyCount;
            if (mine < 1) mine = 1;
            int total = 0;
            try { total = TaleWorlds.MountAndBlade.BannerlordConfig.GetRealBattleSize(); } catch { }
            if (total <= 0) return mine;

            var me = main.MapEvent;
            if (me == null) return mine;   // dopiero dolaczamy - pelny wybor
            try
            {
                var side = me.PlayerSide == TaleWorlds.Core.BattleSideEnum.Attacker
                    ? me.AttackerSide : me.DefenderSide;
                if (side == null || side.Parties == null) return mine;
                int sideHealthy = 0;
                foreach (var p in side.Parties)
                {
                    if (p == null || p.Party == null) continue;
                    sideHealthy += p.Party.NumberOfHealthyMembers;
                }
                if (sideHealthy <= 0) return mine;
                int slots = (int)Math.Round(total * 0.5 * mine / (double)sideHealthy);
                if (slots < 1) slots = 1;
                if (slots > mine) slots = mine;
                return slots;
            }
            catch { return mine; }
        }

        /// <summary>Wybrane oddzialy na gore rosteru - scena spawnuje od gory,
        /// wiec wchodza w sloty pierwsi. Zdejmujemy i dokladamy tylko
        /// NIE-bohaterow (herosi i tak zawsze wchodza); XP stacka wraca z nim.</summary>
        private static void OnPicked(TroopRoster picked)
        {
            try
            {
                var roster = MobileParty.MainParty.MemberRoster;

                var chosen = new List<CharacterObject>();
                int chosenMen = 0;
                for (int i = 0; i < picked.Count; i++)
                {
                    var el = picked.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero || el.Number <= 0) continue;
                    if (!chosen.Contains(el.Character)) chosen.Add(el.Character);
                    chosenMen += el.Number;
                }

                var stacks = new List<TroopRosterElement>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero) continue;
                    stacks.Add(el);
                }

                // zdejmij wszystkie stacki szeregowych...
                foreach (var el in stacks)
                    roster.AddToCounts(el.Character, -el.Number, false, -el.WoundedNumber);

                // ...i doloz od nowa: najpierw wybrane typy (w kolejnosci wyboru), potem reszta
                foreach (var ch in chosen)
                    foreach (var el in stacks)
                        if (el.Character == ch)
                            roster.AddToCounts(el.Character, el.Number, false, el.WoundedNumber, el.Xp);
                foreach (var el in stacks)
                    if (!chosen.Contains(el.Character))
                        roster.AddToCounts(el.Character, el.Number, false, el.WoundedNumber, el.Xp);

                Log.Info("BattleMuster: szyk ustawiony - " + chosen.Count + " typow (" + chosenMen + " ludzi) na czole kolumny.");
                Log.Player("The battle line is formed - your picked men take the field first.", true);
            }
            catch (Exception e) { Log.Error("BattleMuster.OnPicked", e); }
        }
    }
}
