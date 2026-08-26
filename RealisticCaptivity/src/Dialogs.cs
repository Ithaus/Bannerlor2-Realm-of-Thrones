using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace RealisticCaptivity
{
    internal static class Dialogs
    {
        internal static void Add(CampaignGameStarter starter)
        {
            starter.AddPlayerLine("rc_buyback_ask", "hero_main_options", "rc_buyback_answer",
                "I would buy back the war gear you took from me after the battle.",
                BuybackCondition, null, 110);

            starter.AddDialogLine("rc_buyback_answer", "rc_buyback_answer", "rc_buyback_choice",
                "{=!}Your ironmongery? It sits in my armoury. You may have it back for {RC_PRICE}{GOLD_ICON}. Not a denar less.",
                SetPriceText, null, 110);

            starter.AddPlayerLine("rc_buyback_accept", "rc_buyback_choice", "rc_buyback_done",
                "Agreed. Here is your gold.", CanAfford, BuyConsequence, 110);

            starter.AddPlayerLine("rc_buyback_refuse", "rc_buyback_choice", "hero_main_options",
                "That is robbery. I will take it back another way.", null, null, 110);

            starter.AddDialogLine("rc_buyback_done", "rc_buyback_done", "hero_main_options",
                "{=!}A sensible choice. My man will fetch it directly.", null, null, 110);

            starter.AddPlayerLine("rc_buyback_poor", "rc_buyback_choice", "hero_main_options",
                "I do not carry that much gold.", CannotAfford, null, 110);
        }

        private static bool BuybackCondition()
        {
            try
            {
                var b = CaptivityBehavior.Instance;
                return b != null && Hero.OneToOneConversationHero != null && b.CanBuyBackFrom(Hero.OneToOneConversationHero);
            }
            catch (Exception e) { Log.Error("BuybackCondition", e); return false; }
        }

        private static bool SetPriceText()
        {
            try
            {
                var b = CaptivityBehavior.Instance;
                if (b == null) return false;
                MBTextManager.SetTextVariable("RC_PRICE", b.BuybackPrice);
                return true;
            }
            catch { return false; }
        }

        private static bool CanAfford()
        {
            var b = CaptivityBehavior.Instance;
            return b != null && Hero.MainHero.Gold >= b.BuybackPrice;
        }

        private static bool CannotAfford()
        {
            var b = CaptivityBehavior.Instance;
            return b != null && Hero.MainHero.Gold < b.BuybackPrice;
        }

        private static void BuyConsequence()
        {
            try { CaptivityBehavior.Instance?.BuyBackGear(); }
            catch (Exception e) { Log.Error("BuyConsequence", e); }
        }
    }
}
