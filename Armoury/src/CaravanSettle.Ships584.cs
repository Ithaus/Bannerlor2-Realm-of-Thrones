using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 584 (16.11, Jeff: "i jak moja karawana? zarabia" -> "1. tak") - STATKI KARAWANY GRACZA Z NADWYZKI PONAD KAPITAL (klucz PlayerCaravanShipsFromSurplus).
    /// Dowod (log Armoury-2026-11-15_23-13-58, Convoy of Amanda, kapital 30 000): doba 109232 Pebbleton kiesa 25102 -> 15335 przy handlu -85; linia
    /// "Statki (181)" tej doby "karawany 4 / 9749" (inne doby 0-737) - karawana kupila statek za ok. 9,4 tys., a 259 (wartosc = kiesa + towar) i dziennik 305a
    /// tego nie widzialy: karawana "ponizej kapitalu", 259 nic nie wyplaca.
    /// GRA (dekompilacja 1.4.8): CaravansCampaignBehavior.OnSettlementEntered :700-703 -> AdjustConvoyShips :788 (port ma statki, kiesa > 10 000, statkow
    /// < GetIdealShipNumber = 3 dla karawan, NavalDLCShipLimitModel :24-27) -> BuyShips :796-838: kandydaci lekkie (srednie dla elitarnej bez sredniego),
    /// wedlug GetShipPriority malejaco, kupno gdy 0.5 x kiesa >= GetShipTradeValue(statek, port, karawana) (ChangeShipOwnerAction.ApplyByTrade;
    /// 181 kieruje zaplate do kasy portu). NavalDLCShipCostModel.GetShipTradeValue: kupujacy spoza rodu gracza placi 1% kadluba (AIClansShipValueDiscountRatio
    /// 0.01) - karawany AI kupuja statki za ok. 100-150 i problemu nie maja (bez zmian); rod gracza placi pelne 1.5 x wartosc. Odkup przez port
    /// (seller mobilny, buyer osada): 1.5 x wartosc x 0.3 - naprawa (ok. 30% ceny zakupu).
    /// REGULA (tylko karawana rodu gracza; AI bez zmian): prefiks na AdjustConvoyShips (DeclaredMethod) - ten sam warunek wejscia co gra; dalej kopia BuyShips
    /// z jednym dodatkowym warunkiem: statek wolno kupic, gdy (a) karawana nie ma zadnego statku, albo (b) cena <= wartosc - kapital (wartosc 259 ze statkami
    /// po cenie odkupu portu) i kiesa po zakupie >= podloga 259 - kupno tylko z nadwyzki ponad kapital. Wartosc karawany gracza w 259 (Compute) i w ksiegach
    /// wlicza statki po cenie odkupu portu (ShipsValue) - kupiony statek nie jest "strata", a liczenie po cenie odkupu (nie zakupu) nie pozwala 259 wyplacic
    /// kapitalu zamrozonego w statku. Dziennik 305a: "statek kupiony X za Y w Z" / "zakup statku wstrzymany (brak nadwyzki ponad kapital)".
    /// Wylaczony klucz - gra jak dotad (prefiks przepuszcza, wartosc bez statkow). Wyjatek - gra jak dotad, licznik potkniec.
    /// </summary>
    internal static partial class CaravanSettle
    {
        private static bool ShipsSurplusOn { get { var s = S; return s != null && s.PlayerCaravanShipsFromSurplus; } }
        private static bool _w584;
        private static int _s584Buy, _s584Held, _s584First;   // sesja (linia doby 259 swiata); zerowane w Reset

        /// <summary>Z EnsureHooks (kampania, raz na proces).</summary>
        private static void EnsureHooks584(List<string> miss)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(CaravansCampaignBehavior), "AdjustConvoyShips", new[] { typeof(MobileParty), typeof(Town) });
                if (m != null) { _harmony.Patch(m, prefix: new HarmonyMethod(typeof(CaravanSettle), nameof(AdjustPre584))); _w584 = true; }
                else miss.Add("AdjustConvoyShips (584)");
            }
            catch (Exception e) { miss.Add("AdjustConvoyShips (584, blad: " + e.Message + ")"); Stumble("EnsureHooks584", e); }
        }

        /// <summary>Karawana rodu gracza pod regula 584 (klucz). Tylko odczyt.</summary>
        private static bool Player584(MobileParty mp)
        {
            return ShipsSurplusOn && mp != null && mp.IsCaravan && mp.ActualClan == Clan.PlayerClan;
        }

        /// <summary>Statki karawany po cenie odkupu przez port (regula 181: GetShipTradeValue(statek, karawana, port) = 1.5 x wartosc x 0.3 - naprawa; ujemna = 0).
        /// Port wyceny: siedziba karawany (miasto), inaczej biezaca osada; bez portu - 0. Tylko odczyt.</summary>
        internal static long ShipsValue(MobileParty mp, out int count)
        {
            count = 0;
            if (mp == null || mp.Ships == null || mp.Ships.Count == 0) return 0;
            var port = mp.HomeSettlement != null && mp.HomeSettlement.IsTown ? mp.HomeSettlement
                     : mp.CurrentSettlement != null && mp.CurrentSettlement.IsTown ? mp.CurrentSettlement : null;
            if (port == null) return 0;
            var model = Campaign.Current.Models.ShipCostModel;
            long v = 0;
            foreach (var sh in mp.Ships)
            {
                if (sh == null) continue;
                count++;
                v += Math.Max(0L, (long)model.GetShipTradeValue(sh, mp.Party, port.Party));
            }
            return v;
        }

        private static string ShipName(Ship sh)
        {
            try { var n = sh.Name != null ? sh.Name.ToString() : null; return (string.IsNullOrEmpty(n) ? "?" : n) + " (" + (sh.ShipHull != null ? sh.ShipHull.StringId : "?") + ")"; }
            catch { return "?"; }
        }

        private static void Diary584(MobileParty mp, string txt)
        {
            try
            {
                if (!DiaryOn) return;
                var r = Car(mp, true);
                if (r == null) return;
                if (r.DTx.Count >= TxMax) { r.DTxDrop++; return; }
                r.DTx.Add(txt);
            }
            catch (Exception e) { Stumble("Diary584", e); }
        }

        /// <summary>Prefiks CaravansCampaignBehavior.AdjustConvoyShips: karawana gracza - kopia BuyShips z warunkiem nadwyzki ponad kapital; reszta - gra.</summary>
        public static bool AdjustPre584(MobileParty __0, Town __1)
        {
            var caravan = __0; var town = __1;
            bool acted = false;   // po pierwszym ApplyByTrade wyjatek NIE oddaje sterowania grze (kupilaby ponad nadwyzke)
            try
            {
                if (!Live || !Player584(caravan) || town == null || town.Settlement == null) return true;
                var ms = Campaign.Current.Models;
                int ideal = ms.PartyShipLimitModel.GetIdealShipNumber(caravan);
                // ten sam warunek wejscia co gra (:790) - poza nim gra i tak nic nie robi
                if (town.AvailableShips.Count <= 0 || caravan.PartyTradeGold <= 10000 || caravan.Ships.Count >= ideal) return true;
                var owner = caravan.Owner;
                if (owner == null) return true;
                var cpc = caravan.CaravanPartyComponent;
                bool flag = false;   // jak gra: elitarna bez sredniego statku moze kupic sredni
                if (cpc != null && cpc.IsElite)
                {
                    flag = true;
                    foreach (var s0 in caravan.Ships) if (s0 != null && s0.ShipHull.Type == ShipHull.ShipType.Medium) { flag = false; break; }
                }
                var cand = new List<KeyValuePair<Ship, float>>();
                for (int i = town.AvailableShips.Count - 1; i >= 0; i--)
                {
                    var sh = town.AvailableShips[i];
                    if (sh == null) continue;
                    if (sh.ShipHull.Type == ShipHull.ShipType.Light || (sh.ShipHull.Type == ShipHull.ShipType.Medium && flag))
                        cand.Add(new KeyValuePair<Ship, float>(sh, ms.PartyShipLimitModel.GetShipPriority(caravan, sh, false)));
                }
                cand = cand.OrderByDescending(x => x.Value).ToList();   // OrderByDescending stabilne - jak gra
                string townName = Nm(town.Settlement.Name, town.Settlement.StringId);
                bool heldNoted = false;
                foreach (var kv in cand)
                {
                    if (caravan.Ships.Count >= ideal) break;
                    var sh = kv.Key;
                    if (sh.ShipHull.Type == ShipHull.ShipType.Medium && !flag) continue;
                    int purse = caravan.PartyTradeGold;
                    float price = ms.ShipCostModel.GetShipTradeValue(sh, town.Settlement.Party, caravan.Party);
                    if ((float)purse * 0.5f < price) continue;   // regula gry (:829)
                    bool first = caravan.Ships.Count == 0;
                    long surplus = 0; int floor = 0;
                    if (!first)
                    {
                        var c = Compute(caravan, owner);   // wartosc ze statkami po cenie odkupu (584)
                        surplus = c.Worth - c.Capital; floor = c.Floor;
                        if (price > surplus || purse - price < floor)
                        {
                            _s584Held++;
                            if (!heldNoted)
                            {
                                heldNoted = true;
                                Diary584(caravan, "zakup statku wstrzymany (brak nadwyzki ponad kapital) w " + townName + ": " + ShipName(sh) + " za " + (long)price
                                                  + ", nadwyzka " + surplus + " (wartosc " + c.Worth + " - kapital " + c.Capital + "), kiesa " + purse + ", podloga " + floor);
                            }
                            continue;
                        }
                    }
                    acted = true;
                    ChangeShipOwnerAction.ApplyByTrade(caravan.Party, sh);
                    if (sh.Owner != caravan.Party) continue;   // 181 / inna latka odrzucila transakcje - statek zostal w porcie
                    int paid = purse - caravan.PartyTradeGold;
                    _s584Buy++; if (first) _s584First++;
                    Diary584(caravan, "statek kupiony " + ShipName(sh) + " za " + paid + " w " + townName + (first ? " (pierwszy statek)" : " (z nadwyzki " + surplus + ")"));
                    if (sh.ShipHull.Type == ShipHull.ShipType.Medium) flag = false;
                }
                return false;
            }
            catch (Exception e) { Stumble("AdjustPre584", e); return !acted; }
        }
    }
}
