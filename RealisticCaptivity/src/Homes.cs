using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace RealisticCaptivity
{
    /// <summary>
    /// Dom rodzinny i chaty na wlasnosc. Zaczynasz z domem w miejscu urodzenia; w kazdym
    /// miescie i wsi mozesz dokupic nastepny, wejsc, zostawic zloto w skrzyni i rzeczy
    /// w kufrze, albo sprzedac cale gospodarstwo. Co lezy w domu, tego zaden zdobywca
    /// nie zabierze przy pojmaniu - grabia tylko to, co wozisz przy sobie.
    /// </summary>
    internal static class Homes
    {
        // dane trzyma CaptivityBehavior (SyncData); tu tylko odwolania
        internal static Dictionary<string, ItemRoster> Stash;
        internal static Dictionary<string, int> Vault;
        private static string _cameFrom = "town";

        private static Settlement Here { get { return Settlement.CurrentSettlement; } }
        private static bool Owned(Settlement s) { return s != null && Vault != null && Vault.ContainsKey(s.StringId); }

        internal static int BuyPrice(Settlement s)
        {
            var c = Settings.Current;
            if (s.IsVillage)
                return (int)(c.HomePriceVillage + s.Village.Hearth * c.HomePriceHearthFactor);
            float prosperity = s.Town != null ? s.Town.Prosperity : 3000f;
            return (int)(c.HomePriceTown + prosperity * c.HomePriceProsperityFactor);
        }

        internal static int SellPrice(Settlement s)
        {
            return (int)(BuyPrice(s) * Settings.Current.HomeSellFactor);
        }

        // ------------------------------------------------------------ dom rodzinny
        internal static bool FamilyHomeSold;   // sprzedales rodzinna chate - drugiej darmowej nie ma

        internal static void GrantFamilyHome(ref bool granted)
        {
            try
            {
                var c = Settings.Current;
                if (!c.HomesEnabled || !c.FamilyHomeFree) return;
                // dom juz jest albo rodzinny zostal sprzedany - nic do zrobienia
                if (granted && Vault != null && Vault.Count > 0) return;
                if (granted && FamilyHomeSold) return;
                // flaga byla ustawiona, ale domu nie ma (stara wersja poddawala sie
                // przy zamkach bez wsi) - probujemy jeszcze raz
                var born = Hero.MainHero.BornSettlement;
                // w zamku chat sie nie kupuje - rodzinny dom stoi we wsi w jego cieniu
                if (born != null && born.IsCastle)
                {
                    Settlement v = null;
                    try { if (born.BoundVillages != null && born.BoundVillages.Count > 0) v = born.BoundVillages[0].Settlement; } catch { }
                    born = v;
                }
                if (born == null || (!born.IsTown && !born.IsVillage))
                {
                    // miejsce urodzenia bez chat (twierdza bez wsi, brak danych) -
                    // rodzina osiadla w najblizszej wsi
                    try { born = SettlementHelper.FindNearestVillageToMobileParty(MobileParty.MainParty, MobileParty.NavigationType.Default)?.Settlement; }
                    catch { born = null; }
                }
                if (born == null) { Log.Info("GrantFamilyHome: nie znalazlem zadnej wsi, sprobuje przy nastepnej sesji."); return; }
                granted = true;
                if (Owned(born)) return;
                Vault[born.StringId] = 0;
                Stash[born.StringId] = new ItemRoster();
                Log.Player("The old family hearth in " + born.Name + " still stands, and it is yours.", false);
                Log.Info("Dom rodzinny: " + born.StringId);
            }
            catch (Exception e) { Log.Error("GrantFamilyHome", e); }
        }

        /// <summary>Przypomnienie przy starcie sesji, gdzie stoja twoje domy.</summary>
        internal static void AnnounceOwned()
        {
            try
            {
                if (Vault == null || Vault.Count == 0) return;
                var names = new List<string>();
                foreach (var id in Vault.Keys)
                {
                    var s = Settlement.Find(id);
                    if (s != null) names.Add(s.Name.ToString());
                }
                if (names.Count > 0)
                    Log.Player("Your roof and hearth: a house in " + string.Join(", ", names) +
                               ". You will find it in the town or village menu.");
            }
            catch (Exception e) { Log.Error("AnnounceOwned", e); }
        }

        // ------------------------------------------------------------ menu
        internal static void Add(CampaignGameStarter starter)
        {
            foreach (var root in new[] { "town", "village" })
            {
                starter.AddGameMenuOption(root, "rc_home_buy_" + root,
                    "{=!}Buy a house in this place", BuyCondition, BuyConsequence, false, 4);
                starter.AddGameMenuOption(root, "rc_home_enter_" + root,
                    "{=!}Go to your house", EnterCondition,
                    delegate (MenuCallbackArgs a) { _cameFrom = root; GameMenu.SwitchToMenu("rc_home"); }, false, 4);
            }

            starter.AddGameMenu("rc_home",
                "{=!}Your own roof, your own fire. The strongbox holds {RC_HOME_GOLD} gold.",
                HomeInit, GameMenu.MenuOverlayType.SettlementWithBoth);

            starter.AddGameMenuOption("rc_home", "rc_home_chest",
                "{=!}Open the family chest", c => Leave(c, GameMenuOption.LeaveType.Manage),
                delegate (MenuCallbackArgs a)
                {
                    try { InventoryScreenHelper.OpenScreenAsStash(Stash[Here.StringId]); }
                    catch (Exception e) { Log.Error("OpenChest", e); }
                }, false, 0);

            starter.AddGameMenuOption("rc_home", "rc_home_put",
                "{=!}Leave coin in the strongbox", c => Leave(c, GameMenuOption.LeaveType.Trade),
                delegate (MenuCallbackArgs a) { MoveGold(true); }, false, 1);

            starter.AddGameMenuOption("rc_home", "rc_home_take",
                "{=!}Take coin from the strongbox", c => Leave(c, GameMenuOption.LeaveType.Trade),
                delegate (MenuCallbackArgs a) { MoveGold(false); }, false, 2);

            starter.AddGameMenuOption("rc_home", "rc_home_sell",
                "{=!}Sell the house", SellCondition, SellConsequence, false, 3);

            starter.AddGameMenuOption("rc_home", "rc_home_leave",
                "{=!}Step back outside", c => Leave(c, GameMenuOption.LeaveType.Leave),
                delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu(_cameFrom); }, true, 9);
        }

        private static bool Leave(MenuCallbackArgs args, GameMenuOption.LeaveType t)
        { args.optionLeaveType = t; return true; }

        private static void HomeInit(MenuCallbackArgs args)
        {
            try
            {
                int gold = Owned(Here) ? Vault[Here.StringId] : 0;
                MBTextManager.SetTextVariable("RC_HOME_GOLD", gold);
            }
            catch (Exception e) { Log.Error("HomeInit", e); }
        }

        private static bool BuyCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Trade;
            var c = Settings.Current;
            if (!c.HomesEnabled || Here == null || Owned(Here)) return false;
            int price = BuyPrice(Here);
            args.Tooltip = new TextObject("{=!}A house of your own here costs " + price + " gold.");
            if (Hero.MainHero.Gold < price) { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}You cannot afford it (" + price + " gold)."); }
            return true;
        }

        private static void BuyConsequence(MenuCallbackArgs args)
        {
            try
            {
                int price = BuyPrice(Here);
                if (Hero.MainHero.Gold < price) return;
                string id = Here.StringId;
                InformationManager.ShowInquiry(new InquiryData(
                    "A Roof of Your Own",
                    "Buy a house in " + Here.Name + " for " + price + " gold? A strongbox and a chest come with it - and what lies within is beyond any captor's reach.",
                    true, true, "Buy it", "Not now",
                    delegate
                    {
                        try
                        {
                            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, price);
                            Vault[id] = 0;
                            Stash[id] = new ItemRoster();
                            Log.Player("The house in " + Here.Name + " is yours.", false);
                            Log.Info("Kupiono dom: " + id + " za " + price);
                            GameMenu.SwitchToMenu("rc_home");
                        }
                        catch (Exception e) { Log.Error("BuyHome", e); }
                    }, null), true);
            }
            catch (Exception e) { Log.Error("BuyConsequence", e); }
        }

        private static bool EnterCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            if (!Settings.Current.HomesEnabled || !Owned(Here)) return false;
            if (Here.IsUnderRaid) { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}Raiders are in the streets - not now."); }
            return true;
        }

        private static bool SellCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Trade;
            if (!Owned(Here)) return false;
            args.Tooltip = new TextObject("{=!}A buyer would give " + SellPrice(Here) +
                " gold. Whatever lies in the chest and strongbox comes with you.");
            return true;
        }

        private static void SellConsequence(MenuCallbackArgs args)
        {
            try
            {
                string id = Here.StringId;
                int pay = SellPrice(Here);
                InformationManager.ShowInquiry(new InquiryData(
                    "Selling the House",
                    "Sell your house in " + Here.Name + " for " + pay + " gold? The chest will be emptied onto your packhorses and the strongbox into your purse.",
                    true, true, "Sell it", "Keep it",
                    delegate
                    {
                        try
                        {
                            var roster = MobileParty.MainParty.ItemRoster;
                            var stash = Stash.ContainsKey(id) ? Stash[id] : null;
                            if (stash != null)
                            {
                                for (int i = 0; i < stash.Count; i++)
                                {
                                    var el = stash[i];
                                    roster.AddToCounts(el.EquipmentElement, el.Amount);
                                }
                                stash.Clear();
                            }
                            int banked = Vault.ContainsKey(id) ? Vault[id] : 0;
                            GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, pay + banked);
                            Stash.Remove(id);
                            Vault.Remove(id);
                            // ostatni dom sprzedany - rodzinna chata nie odrasta za darmo
                            if (Vault.Count == 0) FamilyHomeSold = true;
                            Log.Player("The house in " + Here.Name + " has a new owner. " + (pay + banked) + " gold changes hands.", false);
                            Log.Info("Sprzedano dom: " + id + " za " + pay + " + skarbiec " + banked);
                            GameMenu.SwitchToMenu(_cameFrom);
                        }
                        catch (Exception e) { Log.Error("SellHome", e); }
                    }, null), true);
            }
            catch (Exception e) { Log.Error("SellConsequence", e); }
        }

        // ------------------------------------------------------------ skarbiec
        private static void MoveGold(bool deposit)
        {
            try
            {
                string id = Here.StringId;
                int have = deposit ? Hero.MainHero.Gold : (Vault.ContainsKey(id) ? Vault[id] : 0);
                InformationManager.ShowTextInquiry(new TextInquiryData(
                    deposit ? "Into the Strongbox" : "Out of the Strongbox",
                    deposit ? "How much coin do you leave at home? (you carry " + have + ")"
                            : "How much coin do you take? (the box holds " + have + ")",
                    true, true, deposit ? "Leave it" : "Take it", "Never mind",
                    delegate (string text)
                    {
                        try
                        {
                            int amount;
                            if (!int.TryParse((text ?? "").Trim(), out amount) || amount <= 0)
                            { Log.Player("That is no sum of coin.", true); return; }
                            amount = Math.Min(amount, have);
                            if (!Vault.ContainsKey(id)) Vault[id] = 0;
                            if (deposit)
                            {
                                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount);
                                Vault[id] += amount;
                            }
                            else
                            {
                                Vault[id] -= amount;
                                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount);
                            }
                            Log.Player(deposit ? amount + " gold rests under your own roof now."
                                               : amount + " gold back in your purse.", false);
                            GameMenu.SwitchToMenu("rc_home");
                        }
                        catch (Exception e) { Log.Error("MoveGold", e); }
                    },
                    delegate { }), true);
            }
            catch (Exception e) { Log.Error("MoveGoldOuter", e); }
        }
    }
}
