using System;
using System.Collections.Generic;
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
    ///    Skutek uboczny w grze (SPInventoryVM.HandleDone): przy XP = 0 gra pyta "You are discarding items. Are you sure?" przy Done na ekranie
    ///    w trybie Default z rzeczami po lewej - tak jak gracza bez perku. Na ekranach Spoils z trwala lewa strona (War stockpile, trofea, tabor
    ///    wroga, pozostalosci pola), gdzie nic sie nie wyrzuca, pytanie zdejmuje SpoilsSeal 13 (ten sam wylacznik); gdzie rzeczy odchodza - zostaje.
    /// 2. Handel BK (BKTradeGoodsFixesBehavior.OnProfitMade): XP Handlu za "sprzedane" = roznica taboru wzgledem migawki BK zrobionej TYLKO po
    ///    kliknieciu targu miasta. Wszystko, co ubylo z taboru inaczej niz sprzedaza, uczylo handlu jak sprzedaz: (a) po targu - zbrojownia DTE,
    ///    magazyn i dary Spoils, zjedzone jedzenie, zuzyte strzaly (przy nastepnym handlu we wsi albo w zamku BK, a prawdziwa sprzedaz z targu
    ///    liczyla sie drugi raz); (b) w czasie samego handlu - kon albo zbroja z taboru zalozone na postac lub towarzysza (gra pozwala na ekranie
    ///    handlu: TransferItem z taboru na Equipment robi AddToCounts(-1) na taborze), ubite zwierze. Petla: zakladasz rumaka towarzyszowi na
    ///    targu, zamykasz (XP), zdejmujesz w zwyklym ekwipunku, powtarzasz. Odwrotnie: sztuka zdjeta z postaci i sprzedana w tej samej sesji
    ///    nie byla w migawce, wiec prawdziwa sprzedaz znikala.
    ///    Naprawa: prefiks na OnProfitMade ustawia migawke BK = tabor teraz + lista sprzedazy tego ekranu (InventoryLogic.GetSoldItems - gra
    ///    zapisuje kazda sztuke polozona na lade, takze prosto z postaci, i kasuje sprzedaz odkupiona). Roznica BK = dokladnie sztuki sprzedane
    ///    przy tym straganie - zalozone na postac, ubite, oddane wczesniej sie nie licza. Ekran, ktory odpalil zdarzenie, lapie prefiks +
    ///    finalizer na InventoryLogic.DoneLogic (gra odpala OnPlayerTradeProfit wewnatrz DoneLogic). Zapas, gdy ekranu brak (zdarzenie spoza
    ///    DoneLogic, np. z innego moda): postfiks na InventoryLogic.Initialize przy isTrading - migawka = tabor w chwili otwarcia handlu.
    ///    Czlon BK "5% ceny" zostaje (Z15).
    /// Wylacznik DonationXpOff (domyslnie wlaczony; wylaczony = gra i BK jak dotad), czytany przy kazdym wywolaniu.
    /// Spoils (uzbrojenie dowodcy, resztki trofeow, dar dla miasta, dar jedzenia, napisy) - SpoilsSeal, ten sam wylacznik.
    /// </summary>
    internal static class DonationXpLaw
    {
        private static FieldInfo _fBkRoster;   // BannerKings.Behaviours.BKTradeGoodsFixesBehavior.roster (prywatne, statyczne)
        private static InventoryLogic _closing;   // ekran w trakcie DoneLogic (zamykany Done) - zrodlo listy sprzedazy dla BK
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

        /// <summary>Postfiks InventoryLogic.Initialize (zapas): przy otwarciu handlu migawka BK = Twoj tabor teraz (ta sama kopia co w BK MarketPatch).</summary>
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

        /// <summary>Prefiks InventoryLogic.DoneLogic: zapamietaj zamykany ekran (zdarzenie handlu BK biegnie wewnatrz).</summary>
        public static void DonePrefix(InventoryLogic __instance) { _closing = __instance; }

        /// <summary>Finalizer InventoryLogic.DoneLogic: zawsze czysci (takze po wyjatku albo odmowie), bez zmiany wyniku.</summary>
        public static Exception DoneFinalizer(Exception __exception) { _closing = null; return __exception; }

        /// <summary>Prefiks BKTradeGoodsFixesBehavior.OnProfitMade: migawka BK = tabor teraz + sztuki sprzedane na tym ekranie, wiec BK liczy
        /// tylko je. Bez ekranu (zdarzenie spoza DoneLogic) - zostaje migawka z otwarcia handlu.</summary>
        public static void BkProfitPrefix()
        {
            if (_fBkRoster == null) return;
            try
            {
                if (!On) return;
                var logic = _closing;
                if (logic == null || !logic.IsTrading) return;
                var bag = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                var snap = _fBkRoster.GetValue(null) as ItemRoster;
                if (bag == null || snap == null) return;
                var sold = logic.GetSoldItems() ?? new List<(ItemRosterElement, int)>();

                // ile sztuk stara migawka policzylaby jako sprzedane, choc nie poszly na lade (zalozone na postac, ubite, oddane wczesniej)
                int phantom = 0, soldN = 0;
                foreach (ItemRosterElement el in snap)
                {
                    int gone = el.Amount - AmountOf(bag, el.EquipmentElement);
                    if (gone <= 0) continue;
                    int s = 0;
                    foreach (var t in sold) if (t.Item1.EquipmentElement.IsEqualTo(el.EquipmentElement)) s += t.Item1.Amount;
                    if (gone > s) phantom += gone - s;
                }

                snap.Clear();
                foreach (ItemRosterElement el in bag) snap.Add(el);
                foreach (var t in sold)
                {
                    if (t.Item1.Amount <= 0 || t.Item1.EquipmentElement.Item == null) continue;
                    snap.Add(t.Item1);
                    soldN += t.Item1.Amount;
                }
                if (phantom > 0)
                    Log.Info("DonationXpLaw: handel BK - XP Handlu tylko za " + soldN + " szt. sprzedanych przy tym straganie; " + phantom
                             + " szt. ubylych z taboru inaczej (zalozone na postac, ubite, oddane po targu) nie liczone jako sprzedaz (Donation Xp Off).");
            }
            catch (Exception e) { Stumble("DonationXpLaw.BkProfit", e); }
        }

        private static int AmountOf(ItemRoster r, EquipmentElement e)
        {
            int i = r.FindIndexOfElement(e);
            return i >= 0 ? r.GetElementCopyAtIndex(i).Amount : 0;
        }

        internal static void ApplyAll(Harmony h)
        {
            int n = 0;
            try
            {
                var post = new HarmonyMethod(typeof(DonationXpLaw), nameof(Zero));
                foreach (var name in new[] { "GetXpBonusForDiscardingItem", "GetXpBonusForDiscardingItems" })
                {
                    var m = AccessTools.Method(typeof(DefaultItemDiscardModel), name);
                    if (m != null) { h.Patch(m, postfix: post); n++; }
                }
            }
            catch (Exception e) { Log.Error("DonationXpLaw.ApplyAll (perki)", e); }

            // handel BK - tylko gdy BK ma to pole; lista sprzedazy ekranu (glowna) i migawka przy otwarciu (zapas) wpinane osobno
            string bk;
            try
            {
                var tBk = QuartermasterLaw.FindType("BannerKings.Behaviours.BKTradeGoodsFixesBehavior");
                _fBkRoster = tBk != null ? AccessTools.Field(tBk, "roster") : null;
                if (_fBkRoster != null && (!_fBkRoster.IsStatic || _fBkRoster.FieldType != typeof(ItemRoster))) _fBkRoster = null;
                if (tBk == null) bk = "BK nieobecny - nie ma czego poprawiac";
                else if (_fBkRoster == null) bk = "NIE ZNALEZIONO pola BK BKTradeGoodsFixesBehavior.roster - handel BK po staremu";
                else
                {
                    string main, spare;
                    try
                    {
                        var profit = AccessTools.Method(tBk, "OnProfitMade");
                        var done = AccessTools.Method(typeof(InventoryLogic), "DoneLogic");
                        if (profit == null || done == null) main = "NIE ZNALEZIONO " + (profit == null ? "BK OnProfitMade" : "InventoryLogic.DoneLogic") + " - lista sprzedazy ekranu niewpieta";
                        else
                        {
                            h.Patch(done, prefix: new HarmonyMethod(typeof(DonationXpLaw), nameof(DonePrefix)),
                                          finalizer: new HarmonyMethod(typeof(DonationXpLaw), nameof(DoneFinalizer)));
                            h.Patch(profit, prefix: new HarmonyMethod(typeof(DonationXpLaw), nameof(BkProfitPrefix)));
                            main = "BK liczy tylko sztuki sprzedane przy tym straganie (lista sprzedazy ekranu; zalozone na postac, ubite i oddane sie nie licza)";
                        }
                    }
                    catch (Exception e) { Log.Error("DonationXpLaw.ApplyAll (lista sprzedazy BK)", e); main = "lista sprzedazy ekranu - WYJATEK przy wpinaniu"; }
                    try
                    {
                        MethodInfo init = null;
                        foreach (var m in typeof(InventoryLogic).GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (m.Name != "Initialize") continue;
                            var ps = m.GetParameters();
                            if (ps.Length > 3 && ps[1].ParameterType == typeof(ItemRoster) && ps[3].ParameterType == typeof(bool) && ps[3].Name == "isTrading") { init = m; break; }
                        }
                        if (init == null) spare = "NIE ZNALEZIONO InventoryLogic.Initialize(..., isTrading, ...)";
                        else
                        {
                            h.Patch(init, postfix: new HarmonyMethod(typeof(DonationXpLaw), nameof(TradeSnapPostfix)));
                            spare = "migawka przy kazdym otwarciu handlu";
                        }
                    }
                    catch (Exception e) { Log.Error("DonationXpLaw.ApplyAll (migawka BK)", e); spare = "migawka - WYJATEK przy wpinaniu"; }
                    bk = "handel BK: " + main + "; zapas: " + spare;
                }
            }
            catch (Exception e) { Log.Error("DonationXpLaw.ApplyAll (BK)", e); bk = "handel BK - WYJATEK przy wpinaniu"; }

            Log.Info("DonationXpLaw: XP za oddany sprzet (Giving Hands / Paid in Promise) wylaczone w " + n + "/2 metodach; " + bk
                     + " - wedle wylacznika Donation Xp Off (domyslnie wlaczony); Spoils - linia SpoilsSeal.");
        }
    }
}
