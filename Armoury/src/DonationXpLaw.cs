using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// ODDANY SPRZET NIE UCZY (Jeff 09.10: "jak przekazujesz bron lub miecze, to rosnie exp - trzeba to zmienic"; audyt 13, Z1 i czlon handlu z Z3).
    /// 1. Perki kwatermistrza "Giving Hands" (bron) i "Paid in Promise" (zbroja) placily 35-300 XP za KAZDA oddana sztuke wedlug tieru, bez wzgledu
    ///    na cene i stan (DefaultItemDiscardModel), przy kazdym zamknieciu ekranu, ktory gra uznaje za wyrzucanie (lup, 11 ekranow Spoils, zdarzenie
    ///    ROT, lup kryjowki). Ekrany Spoils "War stockpile" i "Inspect trophies" maja trwala lewa strone - gra placila za cala kupke przy kazdym
    ///    zamknieciu, takze po Cancel (petla bez konca). Jedna implementacja w grze (zadna z 263 DLL modow nie ma wlasnego ItemDiscardModel):
    ///    postfiks na GetXpBonusForDiscardingItem i GetXpBonusForDiscardingItems -> 0. Zero w modelu = zero wszedzie: napis "Your troops will
    ///    get N experience" (InventoryLogic), dymek przedmiotu (VM) i wyplata (DiscardItemsCampaignBehavior.OnItemsDiscardedByPlayer).
    ///    Druga polowa perkow (clo gubernatora, pensje towarzyszy) zostaje. Opisy perkow bez zmian - nowe dzialanie to pytanie (a) do Jeffa.
    ///    QuartermasterLaw.XpDonationsPostfix (zbrojownia DTE) staje sie zbedny - zostaje, nic nie psuje.
    /// 2. Handel BK (BKTradeGoodsFixesBehavior.OnProfitMade): XP Handlu za "sprzedane" liczy roznice taboru wzgledem migawki zrobionej TYLKO po
    ///    kliknieciu targu miasta - wszystko, co ubylo pozniej (zbrojownia DTE, magazyn i dary Spoils, zjedzone jedzenie, zuzyte strzaly), przy
    ///    nastepnym handlu we wsi albo w zamku BK uczylo handlu jak sprzedaz (a prawdziwa sprzedaz z targu liczyla sie drugi raz). Postfiks na
    ///    InventoryLogic.Initialize przy isTrading (kazdy ekran handlu: targ miasta, wies, zamek BK, karawana, zaulek, ekrany innych modow):
    ///    migawka = tabor w chwili otwarcia handlu - BK liczy tylko to, co sprzedales przy tym straganie. Czlon BK "5% ceny" zostaje (Z15).
    /// Wylacznik DonationXpOff (domyslnie wlaczony; wylaczony = gra i BK jak dotad), czytany przy kazdym wywolaniu.
    /// Spoils (uzbrojenie dowodcy, resztki trofeow, dar dla miasta, dar jedzenia, napisy) - SpoilsSeal, ten sam wylacznik.
    /// </summary>
    internal static class DonationXpLaw
    {
        private static FieldInfo _fBkRoster;   // BannerKings.Behaviours.BKTradeGoodsFixesBehavior.roster (prywatne, statyczne)
        private static int _stumbles;

        /// <summary>Wylacznik DonationXpOff (bez ustawien = domyslnie wlaczony).</summary>
        internal static bool On { get { var s = Settings.Current; return s == null || s.DonationXpOff; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3) Log.Error(where, e);
        }

        /// <summary>Postfiks DefaultItemDiscardModel.GetXpBonusForDiscardingItem / ...Items: oddany sprzet nie daje XP.</summary>
        public static void Zero(ref int __result)
        {
            try { if (On) __result = 0; }
            catch (Exception e) { Stumble("DonationXpLaw.Zero", e); }
        }

        /// <summary>Postfiks InventoryLogic.Initialize: przy otwarciu handlu migawka BK = Twoj tabor teraz (ta sama kopia co w BK MarketPatch).</summary>
        public static void TradeSnapPostfix(bool isTrading)
        {
            if (!isTrading || _fBkRoster == null) return;
            try
            {
                if (!On) return;
                var bag = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (bag == null) return;
                var snap = _fBkRoster.GetValue(null) as ItemRoster;
                if (snap == null) return;   // BK zawsze tworzy liste przy ladowaniu typu - brak = inna wersja BK, nie ruszamy
                snap.Clear();
                foreach (ItemRosterElement el in bag) snap.Add(el);
            }
            catch (Exception e) { Stumble("DonationXpLaw.TradeSnap", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var post = new HarmonyMethod(typeof(DonationXpLaw), nameof(Zero));
                int n = 0;
                foreach (var name in new[] { "GetXpBonusForDiscardingItem", "GetXpBonusForDiscardingItems" })
                {
                    var m = AccessTools.Method(typeof(DefaultItemDiscardModel), name);
                    if (m != null) { h.Patch(m, postfix: post); n++; }
                }

                // migawka handlu BK - tylko gdy BK ma to pole
                string bk;
                var tBk = QuartermasterLaw.FindType("BannerKings.Behaviours.BKTradeGoodsFixesBehavior");
                _fBkRoster = tBk != null ? AccessTools.Field(tBk, "roster") : null;
                if (_fBkRoster != null && (!_fBkRoster.IsStatic || _fBkRoster.FieldType != typeof(ItemRoster))) _fBkRoster = null;
                MethodInfo init = null;
                foreach (var m in typeof(InventoryLogic).GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name != "Initialize") continue;
                    var ps = m.GetParameters();
                    if (ps.Length > 3 && ps[1].ParameterType == typeof(ItemRoster) && ps[3].ParameterType == typeof(bool) && ps[3].Name == "isTrading") { init = m; break; }
                }
                if (tBk == null) bk = "BK nieobecny - nie ma czego poprawiac";
                else if (_fBkRoster == null) bk = "NIE ZNALEZIONO pola BK BKTradeGoodsFixesBehavior.roster - handel BK po staremu";
                else if (init == null) bk = "NIE ZNALEZIONO InventoryLogic.Initialize(..., isTrading, ...) - handel BK po staremu";
                else
                {
                    h.Patch(init, postfix: new HarmonyMethod(typeof(DonationXpLaw), nameof(TradeSnapPostfix)));
                    bk = "migawka handlu BK przy kazdym otwarciu handlu wpieta (BK liczy tylko to, co sprzedales przy tym straganie)";
                }
                Log.Info("DonationXpLaw: XP za oddany sprzet (Giving Hands / Paid in Promise) wylaczone w " + n + "/2 metodach; " + bk
                         + " - wedle wylacznika Donation Xp Off (domyslnie wlaczony); Spoils - linia SpoilsSeal.");
            }
            catch (Exception e) { Log.Error("DonationXpLaw.ApplyAll", e); }
        }
    }
}
