using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GrandTourney
{
    internal static class HostMenu
    {
        internal static void Add(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town", "gt_proclaim_tourney",
                "{=!}Proclaim a tourney",
                Condition, Consequence, false, 4);
        }

        private static bool Condition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                var town = Settlement.CurrentSettlement != null ? Settlement.CurrentSettlement.Town : null;
                var b = TourneyBehavior.Instance;
                if (town == null || b == null) return false;
                string reason;
                if (!b.CanHost(town, out reason))
                {
                    if (reason == "disabled" || reason == "You do not hold this town.") return false;
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject(reason);
                    return true;
                }
                args.Tooltip = new TextObject("Call the knights of the realm to your lists. Fee: " + b.HostFee(town) + " gold, plus the prize you put up.");
                return true;
            }
            catch (Exception e) { Log.Error("HostMenu.Condition", e); return false; }
        }

        private static void Consequence(MenuCallbackArgs args)
        {
            try
            {
                var town = Settlement.CurrentSettlement.Town;
                var b = TourneyBehavior.Instance;
                var s = Settings.Current;
                int fee = b.HostFee(town);

                var options = new List<InquiryElement>
                {
                    new InquiryElement("modest",   "A modest purse - " + s.PrizeModest + " gold", null, Hero.MainHero.Gold >= fee + s.PrizeModest,
                        "Word travels to the nearest holdings."),
                    new InquiryElement("worthy",   "A worthy purse - " + s.PrizeWorthy + " gold", null, Hero.MainHero.Gold >= fee + s.PrizeWorthy,
                        "Word travels across the region."),
                    new InquiryElement("princely", "A princely purse - " + s.PrizePrincely + " gold", null, Hero.MainHero.Gold >= fee + s.PrizePrincely,
                        "Word travels to every hall worth the name.")
                };

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "Proclaim a Tourney",
                    "The heralds await your word. The fee for the lists, trestles and guards is " + fee +
                    " gold. The purse is yours to set - the richer it is, the further the summons carries.",
                    options, true, 1, 1, "Proclaim it", "Not now",
                    delegate (List<InquiryElement> selected)
                    {
                        try
                        {
                            if (selected == null || selected.Count == 0) return;
                            var id = selected[0].Identifier as string;
                            int prize = id == "princely" ? s.PrizePrincely : (id == "worthy" ? s.PrizeWorthy : s.PrizeModest);
                            b.HostTournament(town, prize);
                        }
                        catch (Exception ex) { Log.Error("HostMenu.Selected", ex); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("HostMenu.Consequence", e); }
        }
    }
}
