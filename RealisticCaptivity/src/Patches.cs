using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace RealisticCaptivity
{
    [HarmonyPatch(typeof(GameMenu), "SwitchToMenu")]
    internal static class SwitchToMenuPatch
    {
        private static bool _redirecting;

        private static readonly string[] EscapeMenus =
        {
            "menu_captivity_end_wilderness_escape",
            "menu_captivity_end_prison_escape",
            "menu_escape_captivity_during_battle"
        };

        private static readonly string[] RansomMenus =
        {
            "menu_captivity_end_propose_ransom_wilderness",
            "menu_captivity_end_propose_ransom_in_prison"
        };

        [HarmonyPrefix]
        private static bool Prefix(string menuId)
        {
            if (_redirecting) { _redirecting = false; return true; }
            try
            {
                if (string.IsNullOrEmpty(menuId)) return true;
                if (Campaign.Current == null || !PlayerCaptivity.IsCaptive) return true;
                var b = CaptivityBehavior.Instance;
                if (b == null) return true;
                var s = Settings.Current;

                if (Array.IndexOf(RansomMenus, menuId) >= 0)
                {
                    float need = s.MinDaysBeforeRansomOffer * (b.IsLowborn ? s.LowbornRansomDelay : 1f);
                    if (PlayerCaptivity.CaptiveTimeInDays < need)
                    {
                        Log.Info("Oferta wykupu za wczesnie (" + PlayerCaptivity.CaptiveTimeInDays + "/" + need + " dni, lowborn=" + b.IsLowborn + ") - blokuje.");
                        return false;
                    }
                    return true;
                }

                if (Array.IndexOf(EscapeMenus, menuId) < 0) return true;

                // --- proba ucieczki ---
                int days = PlayerCaptivity.CaptiveTimeInDays;
                if (days < s.MinDaysBeforeEscape)
                {
                    Log.Info("Ucieczka zablokowana - dopiero " + days + " dni niewoli.");
                    b.OnFailedEscape();
                    return false;
                }
                if (MBRandom.RandomFloat > s.EscapeChanceMultiplier)
                {
                    Log.Info("Ucieczka nieudana (rzut).");
                    b.OnFailedEscape();
                    return false;
                }
                if (Settings.Current.EscapeNeedsHelp)
                {
                    var helper = CaptivityExtras.FindOutsideHelper();
                    if (helper == null)
                    {
                        Log.Info("Brak pomocy z zewnatrz - ucieczka niemozliwa.");
                        Log.Player("There is no one on the outside to bribe a guard for you. The chance passes.", true);
                        return false;
                    }
                    int bribe = Settings.Current.EscapeBribeGold;
                    if (Hero.MainHero.Gold >= bribe && bribe > 0)
                    {
                        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, bribe);
                        Log.Info("Lapowka " + bribe + " zaplacona przez " + helper.Name);
                        Log.Player(helper.Name + " bribed a guard for you (-" + bribe + " gold).");
                    }
                }

                if (b.OnParole)
                {
                    Log.Info("Ucieczka mozliwa, ale gracz na parolu - pytam.");
                    AskAboutBreakingParole();
                    return false;
                }
                Log.Info("Ucieczka udana po " + days + " dniach.");
                return true;
            }
            catch (Exception e) { Log.Error("SwitchToMenuPatch", e); return true; }
        }

        private static void AskAboutBreakingParole()
        {
            try
            {
                var s = Settings.Current;
                InformationManager.ShowInquiry(new InquiryData(
                    "Word of Honour",
                    "A chance to escape presents itself. But you gave your word that you would not take it.\n\n" +
                    "Escaping: -" + s.ParoleRenownLoss + " renown and " + s.ParoleRelationPenalty +
                    " relation with your captor's house.",
                    true, true, "I run", "I keep my word",
                    () =>
                    {
                        try
                        {
                            CaptivityBehavior.Instance.BreakParole();
                            _redirecting = true;
                            EndCaptivityAction.ApplyByEscape(Hero.MainHero);
                        }
                        catch (Exception e) { Log.Error("BreakParoleEscape", e); }
                    },
                    () => { Log.Player("You stay. A word given is a word kept."); }), true);
            }
            catch (Exception e) { Log.Error("AskAboutBreakingParole", e); }
        }
    }

    [HarmonyPatch(typeof(PlayerCaptivity), "SetRansomAmount")]
    internal static class RansomAmountPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                var s = Settings.Current;
                var pc = Campaign.Current.PlayerCaptivity;
                int baseAmount = pc.CurrentRansomAmount;
                float renown = (Hero.MainHero.Clan != null) ? Hero.MainHero.Clan.Renown : 0f;
                long amount = (long)(baseAmount * s.RansomMultiplier + renown * s.RansomRenownFactor);
                var b = CaptivityBehavior.Instance;
                if (b != null && b.OnParole) amount = (long)(amount * s.ParoleRansomDiscount);

                // wykup musi pozostac mozliwy do zaplacenia, inaczej gra nigdy nie zaproponuje oferty.
                // Liczy sie CALY majatek - takze skarbce w domach. Kto chowa zloto pod podloga,
                // ten nie wykpi sie grosikiem: rodzina zaplaci z domowej skrzyni.
                long wealth = Hero.MainHero.Gold;
                try { if (Homes.Vault != null) foreach (var v in Homes.Vault.Values) wealth += v; } catch { }
                long cap = (long)(wealth * 0.95f);
                if (cap < 1) cap = 1;
                if (amount > cap) amount = cap;
                if (amount < 1) amount = 1;

                pc.CurrentRansomAmount = (int)amount;
                Log.Info("Wykup: vanilla " + baseAmount + " -> " + amount + " (reputacja " + (int)renown + ")");
            }
            catch (Exception e) { Log.Error("RansomAmountPatch", e); }
        }
    }

    [HarmonyPatch(typeof(EndCaptivityAction), "ApplyByEscape")]
    internal static class EscapeEndPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Hero character)
        {
            try { if (character == Hero.MainHero) CaptivityBehavior.Instance?.OnCaptivityEnded(); }
            catch (Exception e) { Log.Error("EscapeEndPatch", e); }
        }
    }

    [HarmonyPatch(typeof(EndCaptivityAction), "ApplyByRansom")]
    internal static class RansomEndPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Hero character)
        {
            try { if (character == Hero.MainHero) CaptivityBehavior.Instance?.OnCaptivityEnded(); }
            catch (Exception e) { Log.Error("RansomEndPatch", e); }
        }
    }
}
