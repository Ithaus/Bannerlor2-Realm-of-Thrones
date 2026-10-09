using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// OKUPY GRACZA I KURIER OKUPU (169c - sam pomiar). Dwa przeplywy gry bez prawdziwej drugiej strony (audyt 04 W3, L4):
    ///  - okup gracza z menu niewoli (PlayerCaptivityCampaignBehavior.game_menu_captivity_end_by_ransom_on_consequence): gra zdejmuje kwote
    ///    z kiesy gracza GiveGoldAction(gracz, nikt) - pieniadz znika, porywacz nic nie dostaje;
    ///  - kurier z oferta okupu (RansomOfferCampaignBehavior.AcceptRansomOffer): gdy placacy AI ma mniej niz cena + 1000, gra USTAWIA mu
    ///    kiese na cena + 1000 (dosypka z niczego), potem placi cene.
    /// Prefiksy tylko czytaja (void, oryginal biegnie) i licza do linii "Niewola lordow i okupy (169c)". Kazde cialo w try.
    /// </summary>
    internal static class RansomFlows
    {
        // liczniki doby (zerowane po linii) i sesji
        internal static long DayPlayerPaid, DayPlayerToNothing, DayCourierIn, DayCourierOut, DayTopUp;
        internal static int DayPlayerN, DayCourierN, DayTopUpN;
        internal static long SessPlayerPaid, SessTopUp;
        internal static int Stumbles;

        private static FieldInfo _fHero, _fPayer;
        private static bool _menuWired, _courierWired;

        internal static void ZeroDay() { DayPlayerPaid = DayPlayerToNothing = DayCourierIn = DayCourierOut = DayTopUp = 0; DayPlayerN = DayCourierN = DayTopUpN = 0; }
        internal static void Reset() { ZeroDay(); SessPlayerPaid = SessTopUp = 0; Stumbles = 0; }

        internal static string Wired { get { return "menu okupu gracza " + (_menuWired ? "wpiete" : "BRAK") + ", kurier okupu " + (_courierWired ? "wpiety" : "BRAK"); } }

        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }

        internal static string Describe(PartyBase p)
        {
            try
            {
                if (p == null) return "-";
                if (p.IsSettlement && p.Settlement != null) return "loch " + p.Settlement.Name + " (pan " + Name(p.Settlement.OwnerClan != null ? p.Settlement.OwnerClan.Leader : null) + ")";
                var mp = p.MobileParty;
                if (mp == null) return "?";
                return "partia " + mp.Name + (mp.LeaderHero != null ? " (wodz " + Name(mp.LeaderHero) + ")" : " (bez wodza)") + (mp.IsBandit ? " - banda" : "");
            }
            catch { return "?"; }
        }

        // ------------------------------------------------------------ okup gracza z menu niewoli
        public static void MenuRansomPrefix()
        {
            try
            {
                var c = Campaign.Current;
                if (c == null || c.PlayerCaptivity == null) return;
                int amt = c.PlayerCaptivity.CurrentRansomAmount;
                if (amt <= 0) return;
                DayPlayerN++; DayPlayerPaid += amt; DayPlayerToNothing += amt; SessPlayerPaid += amt;
                Log.Info("Okup gracza (169c): " + amt + " zl z kiesy gracza (ma " + (Hero.MainHero != null ? Hero.MainHero.Gold : 0) + "), porywacz: " + Describe(PlayerCaptivity.CaptorParty)
                         + " - gra oddaje te kwote nikomu (w nicosc).");
            }
            catch (Exception e) { Stumbles++; Log.Error("RansomFlows.MenuRansomPrefix", e); }
        }

        // ------------------------------------------------------------ kurier z oferta okupu
        public static void AcceptPrefix(object __instance, int __0)
        {
            try
            {
                if (__instance == null || _fHero == null || _fPayer == null) return;
                var hero = _fHero.GetValue(__instance) as Hero;
                var payer = _fPayer.GetValue(__instance) as Hero;
                if (hero == null || payer == null) return;
                int price = __0;
                DayCourierN++;
                string top = "";
                if (payer == Hero.MainHero) DayCourierOut += price;
                else
                {
                    DayCourierIn += price;
                    if ((long)payer.Gold < (long)price + 1000)
                    {
                        long t = (long)price + 1000 - payer.Gold;
                        DayTopUp += t; DayTopUpN++; SessTopUp += t;
                        top = "; placacy ma " + payer.Gold + " < cena + 1000 - gra dosypie mu " + t + " zl z niczego";
                    }
                }
                Log.Info("Kurier okupu (169c): " + Name(hero) + " - placi " + Name(payer) + " (rod " + (payer.Clan != null ? payer.Clan.Name.ToString() : "-") + ") " + price + " zl"
                         + (payer == Hero.MainHero ? " (gracz wykupuje czlonka rodu)" : " graczowi") + top + ".");
            }
            catch (Exception e) { Stumbles++; Log.Error("RansomFlows.AcceptPrefix", e); }
        }

        /// <summary>Z Measure169c.EnsureHooks (kampania istnieje): prefiksy na obu metodach gry, raz na proces.</summary>
        internal static void Hook(Harmony h)
        {
            if (h == null) return;
            try
            {
                if (!_menuWired)
                {
                    var m = AccessTools.Method(typeof(PlayerCaptivityCampaignBehavior), "game_menu_captivity_end_by_ransom_on_consequence");
                    if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(RansomFlows), nameof(MenuRansomPrefix)) { priority = Priority.First }); _menuWired = true; }
                }
            }
            catch (Exception e) { Log.Error("RansomFlows.Hook(menu)", e); }
            try
            {
                if (!_courierWired)
                {
                    _fHero = AccessTools.Field(typeof(RansomOfferCampaignBehavior), "_currentRansomHero");
                    _fPayer = AccessTools.Field(typeof(RansomOfferCampaignBehavior), "_currentRansomPayer");
                    var m = AccessTools.Method(typeof(RansomOfferCampaignBehavior), "AcceptRansomOffer", new[] { typeof(int) });
                    if (m != null && _fHero != null && _fPayer != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(RansomFlows), nameof(AcceptPrefix)) { priority = Priority.First }); _courierWired = true; }
                }
            }
            catch (Exception e) { Log.Error("RansomFlows.Hook(kurier)", e); }
        }
    }
}
