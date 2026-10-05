using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// DROGA NA TARG (wpis 100; Jeff 05.10: "chlop sam do miasta").
    /// BannerKings kieruje tabor kazdej wsi do Bound - dla 260 z 571 wsi to ZAMEK. Zamek kupuje (za zloto z regulatora
    /// kasy), ale niczego nie wywozi i nie ma warsztatow: w 12 dobach testu ruda rosla w zamkach o 87 ladunkow dziennie,
    /// a 68 z 97 miast nie mialo jej wcale. Historycznie zamek byl kasa i magazynem pana, nie rynkiem - chlop wiozl
    /// nadwyzke na najblizszy targ, zeby miec monety na czynsz (docs/EKONOMIA-FUNDAMENT-2026-10-05.md, W1 i K4).
    /// Latka: prefiks (Priority.First) na vanilla VillagerCampaignBehavior.SendVillagerPartyToTradeBoundTown. Gdy wies
    /// jest przypisana do zamku, a jej TradeBound (wyznacza je gra: miasto wlasnej frakcji w zasiegu, inaczej niewrogie)
    /// jest miastem nieoblezonym, niewrogim i nie dalej niz MarketMaxDistance - tabor jedzie tam trasa vanilli, a latka
    /// BK "najpierw Bound" jest pomijana (prefiks BK zwraca bool, wiec po naszym "false" Harmony go nie wola - sprawdzone
    /// proba na zywo w przegladzie wpisu). W kazdym innym przypadku nic nie zmieniamy (BK: do zamku).
    /// Tabor to prawdziwa partia na mapie - bandyci i wrogowie moga go rozbic po drodze, jak dotad.
    /// Sprzedaz w miescie i podzial utargu po powrocie do wsi robi dalej BK (bez zmian). Zywnosc zamku: wies przypisana
    /// dalej zasila spichlerz z samego przypisania; znika tylko zywnosc "z polki" - linie "Dowoz:" pilnuja glodnych
    /// zamkow, zatkanych magazynow wsi (z grupa kontrolna wsi miejskich), rozbitych taborow i kas miast.
    /// </summary>
    internal static class MarketRoad
    {
        private static MethodInfo _move;
        private static bool _errLogged;
        private static readonly TextObject _txtCart = new TextObject("{=!}Armoury: market carts");
        private static int _toTown, _noMarket, _tooFar, _barred, _lost, _lostToBandits;

        internal static void Reset() { _toTown = 0; _noMarket = 0; _tooFar = 0; _barred = 0; _lost = 0; _lostToBandits = 0; }

        /// <summary>Targ dalej niz limit (droga ladowa) albo bez drogi - tabor krazylby tygodniami, a magazyn wsi stanal.</summary>
        private static bool TooFar(Settlement home, Settlement tb, Settings s)
        {
            if (s.MarketMaxDistance <= 0f) return false;        // 0 = bez wlasnego limitu (obowiazuje limit gry dla TradeBound)
            float d = Campaign.Current.Models.MapDistanceModel.GetDistance(home, tb, false, false, MobileParty.NavigationType.Default);
            return d <= 0f || d > s.MarketMaxDistance;
        }

        public static bool RoutePrefix(VillagerCampaignBehavior __instance, MobileParty villagerParty)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CastleVillagesSellInTown || villagerParty == null) return true;
                var home = villagerParty.HomeSettlement;
                var v = home != null ? home.Village : null;
                if (v == null || v.Bound == null || !v.Bound.IsCastle) return true;      // wsie miejskie - bez zmian (BK)
                var tb = v.TradeBound;
                if (tb == null || !tb.IsTown) { _noMarket++; return true; }
                if (tb.IsUnderSiege || (tb.MapFaction != null && home.MapFaction != null && tb.MapFaction.IsAtWarWith(home.MapFaction))) { _barred++; return true; }
                if (TooFar(home, tb, s)) { _tooFar++; return true; }
                if (_move != null) _move.Invoke(__instance, new object[] { villagerParty, tb });
                else villagerParty.SetMoveGoToSettlement(tb, MobileParty.NavigationType.Default, false);
                _toTown++;
                return false;       // pomija latke BK (do zamku) i oryginal - cel juz nadany
            }
            catch (Exception e)
            {
                if (!_errLogged) { _errLogged = true; Log.Error("MarketRoad.RoutePrefix", e); }   // raz na sesje; BK (zamek) dziala dalej
                return true;
            }
        }

        /// <summary>
        /// WOZ (wpis 101). Tabor wsi zamkowej jadacy na targ miasta ma kurs ok. 2.7x dluzszy niz do zamku, a bierze tylko
        /// ok. 14 ladunkow po 100 kg (10 + 20 kg na czlowieka + 100 kg na zwierze juczne): w tescie wpisu 100 magazyny wsi
        /// zamkowych zatykaly sie dwa razy czesciej niz miejskich (17% wobec 8%), a zatkana wies wstrzymuje cala produkcje.
        /// Udzwig taboru wsi zamkowej z targiem w miescie x MarketCartFactor (woz zamiast jukow). Postfiks na modelu
        /// bazowym - model NavalDLC deleguje do niego, BK wlasnego nie rejestruje.
        /// </summary>
        public static void CartPostfix(MobileParty mobileParty, ref ExplainedNumber __result)
        {
            try
            {
                if (mobileParty == null || !mobileParty.IsVillager) return;      // tanie wyjscie - model wolany dla kazdej partii
                var s = Settings.Current;
                if (s == null || !s.CastleVillagesSellInTown || s.MarketCartFactor <= 1f) return;
                var hs = mobileParty.HomeSettlement;
                var v = hs != null ? hs.Village : null;
                if (v == null || v.Bound == null || !v.Bound.IsCastle || v.TradeBound == null || !v.TradeBound.IsTown) return;
                __result.AddFactor(s.MarketCartFactor - 1f, _txtCart);
            }
            catch { }
        }

        /// <summary>Rozbity tabor wiesniakow - ladunek przepada albo trafia do jukow zwyciezcy (bandy go nie sprzedaja).</summary>
        internal static void OnPartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            try
            {
                if (party == null || !party.IsVillager) return;
                _lost++;
                if (destroyer != null && destroyer.MobileParty != null && destroyer.MobileParty.IsBandit) _lostToBandits++;
            }
            catch { }
        }

        /// <summary>Raz na dobe: dokad jada tabory, czy wsie sie nie zatykaja, czy zamki nie glodnieja, czy kasy miast to dzwigaja.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            int castleVillages = 0, townVillages = 0, own = 0, foreign = 0, none = 0, clogCastle = 0, clogTown = 0, headTown = 0, headCastle = 0, onRoad = 0;
            int castles = 0, hungry = 0, bareNeg = 0, shelfFed = 0, empty = 0;
            float shelfFood = 0f;
            foreach (var st in Settlement.All)
            {
                if (st == null) continue;
                if (st.IsCastle && st.Town != null)
                {
                    castles++;
                    try
                    {
                        float fc = st.Town.FoodChange, bare = st.Town.FoodChangeWithoutMarketStocks;
                        shelfFood += fc - bare;
                        if (fc < 0f) hungry++;
                        if (bare < 0f) { bareNeg++; if (fc >= 0f) shelfFed++; }
                        if (st.Town.FoodStocks < 1f) empty++;
                    }
                    catch { }
                    continue;
                }
                var v = st.Village;
                if (v == null || v.Bound == null) continue;
                bool castleVillage = v.Bound.IsCastle;
                bool clogged = false;
                try
                {
                    int stock = 0;
                    var r = st.ItemRoster;
                    if (r != null) for (int i = 0; i < r.Count; i++) stock += r[i].Amount;
                    clogged = stock >= v.GetWarehouseCapacity() * 1.5f;      // ta sama bramka, przy ktorej gra wstrzymuje produkcje wsi
                }
                catch { }
                if (!castleVillage) { townVillages++; if (clogged) clogTown++; continue; }
                castleVillages++;
                if (clogged) clogCastle++;
                var tb = v.TradeBound;
                if (tb == null || !tb.IsTown) none++;
                else if (tb.MapFaction == st.MapFaction) own++;
                else foreign++;
                var mp = v.VillagerPartyComponent != null ? v.VillagerPartyComponent.MobileParty : null;
                if (mp != null && mp.IsActive && mp.CurrentSettlement == null)
                {
                    onRoad++;
                    if (mp.DefaultBehavior == AiBehavior.GoToSettlement && mp.TargetSettlement != null)
                    {
                        if (mp.TargetSettlement.IsTown) headTown++;
                        else if (mp.TargetSettlement.IsCastle) headCastle++;
                    }
                }
            }
            long gold = 0; int low = 0, towns = 0;
            foreach (var t in Town.AllTowns)
            {
                if (t == null) continue;
                towns++; gold += t.Gold;
                if (t.Gold < s.TownRentFloorGold) low++;
            }
            int day = (int)CampaignTime.Now.ToDays - 1;
            Log.Info("Dowoz: dzien " + day + " - tabory wsi zamkowych wyslane na targ miasta " + _toTown
                     + " (do zamku: brak targu " + _noMarket + ", targ za daleko " + _tooFar + ", oblezenie albo wojna " + _barred
                     + "); w drodze teraz " + onRoad + ": do miasta " + headTown + ", do zamku " + headCastle
                     + "; rozbite tabory wsi (wszystkich) " + _lost + ", w tym przez bandy " + _lostToBandits
                     + "; wsi zamkowych " + castleVillages + ": targ we wlasnym krolestwie " + own + ", w obcym " + foreign + ", bez targu " + none
                     + (s.CastleVillagesSellInTown ? "." : ". LATKA WYLACZONA w ustawieniach."));
            Log.Info("Dowoz (skutki): dzien " + day + " - zatkane magazyny: wsie zamkowe " + clogCastle + " z " + castleVillages
                     + ", wsie miejskie " + clogTown + " z " + townVillages
                     + "; zamki: bilans zywnosci ujemny " + hungry + " z " + castles + ", bez polki ujemny " + bareNeg + " (dzis ratuje polka " + shelfFed
                     + "), zywnosc z polki razem " + shelfFood.ToString("0") + " na dobe, pusty spichlerz " + empty
                     + "; kasy miast razem " + gold + ", ponizej progu rent " + low + " z " + towns + ".");
            Reset();
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var target = AccessTools.Method(typeof(VillagerCampaignBehavior), "SendVillagerPartyToTradeBoundTown");
                _move = AccessTools.Method(typeof(VillagerCampaignBehavior), "MoveVillagersToSettlementWithBestNavigationType");
                if (target == null) { Log.Info("MarketRoad: BRAK VillagerCampaignBehavior.SendVillagerPartyToTradeBoundTown - wsie zamkowe woza do zamku jak dotad."); return; }
                h.Patch(target, prefix: new HarmonyMethod(typeof(MarketRoad), nameof(RoutePrefix)) { priority = Priority.First });
                Log.Info("MarketRoad: wsie zamkowe woza plon na targ miasta (TradeBound) - latka wpieta" + (_move != null ? ", trasa vanilli." : ", trasa prosta (brak metody vanilli)."));
                var cap = AccessTools.Method(typeof(DefaultInventoryCapacityModel), "CalculateInventoryCapacity");
                if (cap != null)
                {
                    h.Patch(cap, postfix: new HarmonyMethod(typeof(MarketRoad), nameof(CartPostfix)));
                    var s = Settings.Current;
                    Log.Info("MarketRoad: woz - udzwig taborow wsi zamkowych jadacych na targ x" + (s != null ? s.MarketCartFactor.ToString("0.0") : "?") + ".");
                }
                else Log.Info("MarketRoad: BRAK DefaultInventoryCapacityModel.CalculateInventoryCapacity - woz wylaczony.");
            }
            catch (Exception e) { Log.Error("MarketRoad.ApplyAll", e); }
        }
    }
}
