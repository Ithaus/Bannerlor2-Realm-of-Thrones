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
                    if (__0.LeaderHero != null) MoneyLedger.NoteLevyBack(cost);   // ksiega pieniadza (tylko licznik): gra skasowala te zaplate przez GiveGoldAction
                    try
                    {
                        if (__3.IsMounted && RecruitCost.OwnHorseOn) RecruitCost.NoteOwnHorse(false, __1, 1);   // paczka 160: ochotnik z wlasnym koniem - w koszcie konia nie ma (tylko licznik)
                        else if (__3.IsMounted && Settings.Current.HistoricalRecruitCost && Settings.Current.HorsesAtMarketPrice) RecruitCost.NoteVolunteerHorse((int)RecruitCost.HorseCost(__3, __0.LeaderHero));   // linia "Konie rekrutow (157)" (tylko licznik)
                    }
                    catch { }
                }
                else if (detail == "MercenaryFromTavern")
                {
                    if (__1 == null || __1.SettlementComponent == null) return;
                    int n = Math.Max(1, __4);
                    // POPRAWKA 157 (kon najemnika z karczmy): w koszcie siedzi cena konia tylko wtedy, gdy na polce tego miasta stoi kon tej rasy
                    // (RecruitCost.HorseCost) - teraz ten kon naprawde schodzi z polki (po jednym na najemnika), a gdy koni na polce jest mniej niz
                    // najemnikow, nadplata za brakujace wraca do kiesy, ktora placila (lord - gra skasowala jego zaplate; karawana - kiesa partii).
                    // Polka bez takiego konia: w koszcie konia nie ma (najemnik z wlasnym koniem), nic nie schodzi, nic nie wraca.
                    // PACZKA 160 (RecruitsOwnHorse): kon jest wlasnoscia zolnierza zawsze - w koszcie nie ma konia (RecruitCost), MercShelfOn
                    // nieczynne, miasto dostaje caly koszt (dni zoldu, z premia konnego - MountedWage), nic nie schodzi z polki, nic nie wraca.
                    // Poprawka po recenzji 157: kon w koszcie to cena polki RAZY mnoznik kupujacego (perki, kultura, prawa i ranga klanu BK) - do kasy
                    // za konia z polki i z powrotem za konia, ktorego nie bylo, idzie ta sama kwota, ktora kupujacy za konia zaplacil (per = koszt ze
                    // sprzetem - koszt bez sprzetu); dotad zwrot liczyl sama cene polki: przy mnozniku < 1 kasa miasta oddawala lordowi za konie,
                    // ktorych nie bylo (przy 0.5 i 10 najemnikach - na minus), przy > 1 zatrzymywala doplate za nie. Kon "bez targu" (stala gry
                    // w koszcie, polka nieznana) - jak kon spoza polki: wraca cala doplata, miasto nie bierze nic za konia, ktory z niej nie zszedl.
                    int horse = 0, per = 0, took = 0, refund = 0;
                    Settlement market = null;
                    if (__3.IsMounted && RecruitCost.IsMerc(__3) && RecruitCost.MercShelfOn)
                    {
                        horse = RecruitCost.MercHorseQuote(__3, __0.LeaderHero, out market);
                        per = RecruitCost.HorseShareOfCost(__3, __0.LeaderHero, cost, horse);
                        if (horse > 0 && per > 0) took = RecruitCost.TakeShelfHorses(market ?? __1, __3, n);
                        refund = (n - took) * per;
                        RecruitCost.NoteMercHorses(__1, took, n - took, per, refund, market == null || market.Town == null, false);
                    }
                    else if (__3.IsMounted && RecruitCost.OwnHorseOn) RecruitCost.NoteOwnHorse(true, __1, n);   // paczka 160: najemnik z wlasnym koniem - w koszcie konia nie ma, z polki nic nie schodzi (tylko licznik)
                    if (refund > 0)
                    {
                        if (__0.IsCaravan) __0.PartyTradeGold += refund;
                        else if (__0.LeaderHero != null) __0.LeaderHero.ChangeHeroGold(refund);
                    }
                    int toTown = cost * n - refund;
                    __1.SettlementComponent.ChangeGold(toTown);
                    _toTowns += toTown;
                    MoneyLedger.Note(MoneyLedger.NMerc, __1, toTown);   // ksiega przeplywow osad (tylko licznik)
                    // karawana placi z kiesy partii bez GiveGoldAction (nic nie znika) - "zwrot skasowanego" liczymy tylko dla wodza (miastu + zwrot = cala zaplata)
                    if (!__0.IsCaravan && __0.LeaderHero != null) MoneyLedger.NoteLevyBack(cost * n);
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
