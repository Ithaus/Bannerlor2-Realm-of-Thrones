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

        // ------------------------------------------------------------ recenzja 584 (blad 1, obie recenzje): w wartosci tylko statki KUPIONE przez karawane
        // Statki z szablonu karawana dostaje za darmo (CaravanPartyComponent.CreateCaravanParty :244-247 - kiesa = GetInitialTradeGold, statki bez oplaty),
        // a kapital 259 to sama moneta - liczenie ich w wartosci kazaloby 259 wyplacic graczowi kapital za statek, ktorego nikt nie kupil. Ship nie ma trwalego
        // id (nie MBObjectBase) - rekord: karawana -> lista (kadlub, zaplacone); w wartosci kazdy rekord paruje sie z jednym statkiem tego kadluba u karawany
        // (najtanszym jeszcze nie sparowanym) po min(cena odkupu, zaplacone). Rekord bez statku (zatopiony, porzucony) - nie liczony, kasowany raz na dobe.
        // Darmowa naprawa (RepairShipAction.ApplyForFree) tylko przywraca cene odkupu kupionego statku (uszkodzenie ja wczesniej obnizylo) - bilans zerowy.
        // Zapis: klucz arm_584_statki (linie "karawana|kadlub|zaplacone").
        private struct Bought { public string Hull; public int Paid; }
        private static readonly Dictionary<string, List<Bought>> _bought = new Dictionary<string, List<Bought>>(StringComparer.Ordinal);
        private static int _boughtImp, _boughtImpBad;

        private static void NoteBought(MobileParty mp, Ship sh, int paid)
        {
            if (mp == null || mp.StringId == null || sh == null || sh.ShipHull == null || paid <= 0) return;
            lock (_bought)
            {
                List<Bought> l;
                if (!_bought.TryGetValue(mp.StringId, out l)) { l = new List<Bought>(); _bought[mp.StringId] = l; }
                l.Add(new Bought { Hull = sh.ShipHull.StringId, Paid = paid });
            }
        }

        /// <summary>Kupione statki karawany po min(cena odkupu przez port, zaplacone) (regula 181: GetShipTradeValue(statek, karawana, port) = 1.5 x wartosc x 0.3
        /// - naprawa; ujemna = 0). Statki z szablonu i bez rekordu - 0. Port wyceny: siedziba karawany (miasto), inaczej biezaca osada; bez portu - 0. Tylko odczyt.</summary>
        internal static long ShipsValue(MobileParty mp, out int count)
        {
            count = 0;
            if (mp == null || mp.StringId == null || mp.Ships == null || mp.Ships.Count == 0) return 0;
            List<Bought> recs;
            lock (_bought)
            {
                List<Bought> l;
                if (!_bought.TryGetValue(mp.StringId, out l) || l.Count == 0) return 0;
                recs = new List<Bought>(l);
            }
            var port = mp.HomeSettlement != null && mp.HomeSettlement.IsTown ? mp.HomeSettlement
                     : mp.CurrentSettlement != null && mp.CurrentSettlement.IsTown ? mp.CurrentSettlement : null;
            if (port == null) return 0;
            var model = Campaign.Current.Models.ShipCostModel;
            var pool = new List<KeyValuePair<string, long>>();   // statki karawany: kadlub, cena odkupu
            foreach (var sh in mp.Ships)
            {
                if (sh == null || sh.ShipHull == null) continue;
                pool.Add(new KeyValuePair<string, long>(sh.ShipHull.StringId, Math.Max(0L, (long)model.GetShipTradeValue(sh, mp.Party, port.Party))));
            }
            long v = 0;
            foreach (var r in recs)
            {
                int bi = -1;
                for (int i = 0; i < pool.Count; i++)
                    if (string.Equals(pool[i].Key, r.Hull, StringComparison.Ordinal) && (bi < 0 || pool[i].Value < pool[bi].Value)) bi = i;
                if (bi < 0) continue;   // statku tego kadluba juz nie ma u karawany
                v += Math.Min(pool[bi].Value, (long)r.Paid);
                count++;
                pool.RemoveAt(bi);
            }
            return v;
        }

        /// <summary>Raz na dobe (z FundPrune): rekordy karawan, ktorych juz nie ma, i rekordy ponad liczbe statkow danego kadluba (statek stracony).</summary>
        private static void Prune584(HashSet<string> live)
        {
            lock (_bought)
            {
                if (_bought.Count == 0) return;
                var gone = new List<string>();
                foreach (var k in _bought.Keys) if (!live.Contains(k)) gone.Add(k);
                foreach (var k in gone) _bought.Remove(k);
            }
            foreach (var mp in MobileParty.AllCaravanParties)
            {
                if (mp == null || !mp.IsActive || mp.StringId == null) continue;
                lock (_bought)
                {
                    List<Bought> l;
                    if (!_bought.TryGetValue(mp.StringId, out l)) continue;
                    var have = new Dictionary<string, int>(StringComparer.Ordinal);
                    if (mp.Ships != null)
                        foreach (var sh in mp.Ships)
                            if (sh != null && sh.ShipHull != null) { int n; have.TryGetValue(sh.ShipHull.StringId, out n); have[sh.ShipHull.StringId] = n + 1; }
                    var keep = new List<Bought>();
                    for (int i = l.Count - 1; i >= 0; i--)   // od najnowszych: zostaja ostatnio kupione
                    {
                        int n; have.TryGetValue(l[i].Hull, out n);
                        if (n > 0) { have[l[i].Hull] = n - 1; keep.Insert(0, l[i]); }
                    }
                    if (keep.Count == 0) _bought.Remove(mp.StringId); else _bought[mp.StringId] = keep;
                }
            }
        }

        internal static string ExportBought()
        {
            var sb = new System.Text.StringBuilder(128);
            lock (_bought)
                foreach (var kv in _bought)
                    foreach (var b in kv.Value)
                        sb.Append(kv.Key).Append('|').Append(b.Hull).Append('|').Append(b.Paid.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            return sb.ToString();
        }

        internal static void ImportBought(string s)
        {
            lock (_bought)
            {
                _bought.Clear(); _boughtImp = _boughtImpBad = 0;
                if (string.IsNullOrEmpty(s)) return;
                foreach (var line in s.Split('\n'))
                {
                    if (line.Length == 0) continue;
                    var p = line.Split('|');
                    int v;
                    if (p.Length == 3 && p[0].Length > 0 && p[1].Length > 0
                        && int.TryParse(p[2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0)
                    {
                        List<Bought> l;
                        if (!_bought.TryGetValue(p[0], out l)) { l = new List<Bought>(); _bought[p[0]] = l; }
                        l.Add(new Bought { Hull = p[1], Paid = v });
                        _boughtImp++;
                    }
                    else _boughtImpBad++;
                }
            }
        }

        private static void Reset584()
        {
            lock (_bought) { _bought.Clear(); _boughtImp = _boughtImpBad = 0; }
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
                            if (!heldNoted)
                            {
                                heldNoted = true;
                                _s584Held++;   // recenzja 584 (drobny 3): jedno wstrzymanie na wjazd, nie na kazdego kandydata
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
                    NoteBought(caravan, sh, paid);   // recenzja 584: w wartosci tylko statki kupione (z rekordu), nie z szablonu
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
