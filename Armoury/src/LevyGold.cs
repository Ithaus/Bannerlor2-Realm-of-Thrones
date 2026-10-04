using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// POBOR OD LUDNOSCI - krok 3 (docs/PLAN-POBOR.md pkt 2.5, 2.6; audyt ekonomii 04.10):
    /// 1. ZLOTO ZA WERBUNEK: vanilla RecruitmentCampaignBehavior.ApplyInternal placi za ochotnika
    ///    i za najemnika z karczmy `GiveGoldAction(lord, null)` - zloto znika. U gracza BK placi
    ///    notablowi. Teraz AI tez: za ochotnika - notablowi, ktory go wystawil; za najemnika z karczmy
    ///    (i z kasy karawany) - miastu.
    /// 2. DARMOWY KOMPLET NOWEJ PARTII AI: DTE EveryoneCampaignBehavior.OnMobilePartyCreated wklada do
    ///    zbrojowni kazdej nowej partii AI komplet dla calego skladu - z niczego. Po starcie gry
    ///    pomijamy to dla partii AI (gracz bez zmian); lord kupuje sprzet na targu (AiGear).
    /// </summary>
    internal static class LevyGold
    {
        private static int _toNotables, _toTowns, _skippedKits, _dayStamp = -1;

        internal static void Reset() { _toNotables = _toTowns = _skippedKits = 0; _dayStamp = -1; }

        public static void ApplyInternalPostfix(MobileParty __0, Settlement __1, Hero __2, CharacterObject __3, int __4, object __6)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.RecruitGoldToSeller || __0 == null || __3 == null) return;
                Tick();
                string detail = __6 != null ? __6.ToString() : "";
                var model = Campaign.Current.Models.PartyWageModel;
                int cost = model.GetTroopRecruitmentCost(__3, __0.LeaderHero).RoundedResultNumber;
                if (cost <= 0) return;
                if (detail == "VolunteerFromIndividual")
                {
                    if (__0 == MobileParty.MainParty || __2 == null || !__2.IsAlive) return;   // gracz: BK juz placi notablowi
                    __2.ChangeHeroGold(cost);
                    _toNotables += cost;
                }
                else if (detail == "MercenaryFromTavern")
                {
                    if (__1 == null || __1.SettlementComponent == null) return;
                    int n = Math.Max(1, __4);
                    __1.SettlementComponent.ChangeGold(cost * n);
                    _toTowns += cost * n;
                }
            }
            catch { }
        }

        public static bool PartyCreatedPrefix(MobileParty __0)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.NoFreeKitForNewParties || __0 == null) return true;
                if (Campaign.Current == null || !Campaign.Current.GameStarted) return true;   // start gry - swiat dostaje sprzet
                if (__0 == MobileParty.MainParty) return true;
                Tick();
                _skippedKits++;
                return false;
            }
            catch { return true; }
        }

        private static void Tick()
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (_dayStamp == day) return;
            if (_dayStamp >= 0 && _toNotables + _toTowns + _skippedKits > 0)
                Log.Info("Werbunek: dzien " + _dayStamp + " - zloto AI za ochotnikow do notabli " + _toNotables + ", za najemnikow do miast " + _toTowns
                         + "; nowe partie AI bez darmowego kompletu DTE: " + _skippedKits + ".");
            _toNotables = _toTowns = _skippedKits = 0;
            _dayStamp = day;
        }

        internal static void ApplyAll(Harmony h)
        {
            string a = "BRAK", b = "BRAK";
            try
            {
                var m = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "ApplyInternal");
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(LevyGold), nameof(ApplyInternalPostfix))); a = "wpiete"; }
            }
            catch (Exception e) { Log.Error("LevyGold.ApplyInternal", e); }
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                var m = t != null ? AccessTools.Method(t, "OnMobilePartyCreated") : null;
                if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(LevyGold), nameof(PartyCreatedPrefix))); b = "wpiete"; }
            }
            catch (Exception e) { Log.Error("LevyGold.PartyCreated", e); }
            Log.Info("LevyGold: zloto za werbunek AI do sprzedajacego " + a + "; koniec darmowego kompletu nowej partii AI (DTE) " + b + ".");
        }
    }
}
