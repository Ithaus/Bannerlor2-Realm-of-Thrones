using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KS RUNDA 4 - PACZKA r4-bkwies (diagnoza fin80 "sol"): krok 5 wsi DS (DsSurplus, czesc 2 - lista wsi) kupowal sol i narzedzia BEZ limitu potrzeby - cala polke do
    /// 5 sztuk po kazdej cenie; towar znikal, a ksiega liczyla go "bez rynku" (160% rynkowego zuzycia soli). Klucz DsSurplusNeedLimit: ta sama droga co BuyGoods (budzet po
    /// rowno na kategorie listy, przy VillWorkOnly nadwyzka tylko na sol i narzedzia, cena targu, ponad 5 sztuk, plotno ponad prog polki K2 z polowa progu E3-W1b), ale
    /// sztuki towarow potrzeb najwyzej do zapasu potrzeb wsi (VillNeeds.Room - ludzie x potrzeba dnia x dni zapasu, narzedzia wedlug zuzycia).
    /// JEDNA ZASADA na pieniadz kroku 5 (recenzja r4-bkwies): czesc kategorii potrzeby, ktora jest POKRYTA (zapas w domu pelny - na poczatku albo po zakupie do limitu),
    /// ZOSTAJE W KIESIE wsi w calosci; czesc plotna zatrzymana progiem polki K2 - tez w kiesie (jak mowi K2); do robot gminy idzie tylko czesc, ktora nie znalazla towaru
    /// przy potrzebie jeszcze OTWARTEJ (albo kategorii bez potrzeby, jak dotad). To samo u wsi z koszykiem ZC4 (DsSkipShare): czesc listy wsi (towary koszyka - potrzeba
    /// pokryta drabina ZC4) zostaje w kiesie, reszta jak dotad.
    /// </summary>
    internal static partial class TownFolk
    {
        /// <summary>VillNeeds: glowy wsi do potrzeb (te same wolne glowy co koszyk dnia i czynsz - VillHeads); 0 - wies nieskalibrowana albo kiesa nieczynna. Nie rzuca.</summary>
        internal static double NeedHeads(Settlement st)
        {
            try
            {
                if (st == null || !st.IsVillage || !Live) return 0;
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null || f.Kind != 2 || !f.Cal) return 0;
                double h = VillHeads(f, true);
                return double.IsNaN(h) || double.IsInfinity(h) || h < 0 ? 0 : h;
            }
            catch (Exception e) { Stumble("NeedHeads", e); return 0; }
        }

        /// <summary>
        /// K2 / E3-W1b dla zakupow wsi poza koszykiem dnia (krok 5 do potrzeby, rynek BK wedlug potrzeb - recenzja r4-bkwies): prog polki wiersza - plotno (len) przy
        /// VillageClothAboveReserve najwyzej ponad prog polki miasta (MaterialOrders.ShelfReserve), przy VillageClothHalfReserve od polowy progu tam, gdzie plotno tanie
        /// (indeks ceny ponizej 1) - ta sama regula co BuyGoods; inne towary i klucz wylaczony - 5 sztuk (ShelfKeep). `half` - prog z polowy (licznik E3-W1b). Nie rzuca.
        /// </summary>
        internal static int VillShelfKeepRow(Town town, ItemObject item, out bool half)
        {
            half = false;
            try
            {
                var c = item != null ? item.ItemCategory : null;
                if (town == null || c == null || !VillClothOn || c.StringId != "linen") return ShelfKeep;
                int r = MaterialOrders.ShelfReserve(town, item);
                if (ClothHalfOn && r > ShelfKeep && town.GetItemCategoryPriceIndex(c) < 1f) { r = Math.Max(ShelfKeep, r / 2); half = true; }
                return r > ShelfKeep ? r : ShelfKeep;
            }
            catch (Exception e) { Stumble("VillShelfKeepRow", e); return ShelfKeep; }
        }

        /// <summary>
        /// DsSurplus przy Ks Surplus Skips Basket (wies z koszykiem ZC4): czesc reszty kroku 5, ktora lista wsi (BuyGoods - sol, narzedzia, len, garnki, piwo = towary koszyka
        /// ZC4) dostalaby do wydania - dokladnie podzial BuyGoods (budzet po rowno na kategorie obecne na polce; przy VillWorkOnly tylko sol i narzedzia). Te zl ZOSTAJA
        /// W KIESIE wsi (potrzebe pokrywa drabina ZC4 - jedna zasada na jedno zjawisko); reszta (czego lista dostatku nie znalazla na polkach) - jak dotad. Pusta lista - 0.
        /// </summary>
        private static double DsSkipShare(List<EquipmentElement> list, double want, double workExtra)
        {
            if (list == null || list.Count == 0) return 0;
            var cats = new List<ItemCategory>();
            foreach (var e in list) { var c = e.Item.ItemCategory; if (!cats.Contains(c)) cats.Add(c); }
            if (cats.Count == 0) return 0;
            double perW = workExtra > 0 ? workExtra / cats.Count : 0;
            double share = Math.Max(0.0, want);
            if (perW > 0) foreach (var c in cats) if (VillWorkCat(c)) share += perW;
            return double.IsNaN(share) || double.IsInfinity(share) ? 0 : share;
        }

        /// <summary>
        /// DsSurplus czesc 2 przy kluczu DsSurplusNeedLimit (wies bez koszyka ZC4): jak BuyGoods, ale towar potrzeb (sol, narzedzia, len, sukno, garnki) najwyzej do zapasu
        /// potrzeb wsi. `keep` - zl, ktore zostaja w kiesie: cala czesc kategorii potrzeby pokrytej (zapas w domu pelny przed zakupem albo po zakupie do limitu) i czesc plotna
        /// zatrzymana progiem polki K2. Reszta niewydana (potrzeba otwarta, brak towaru na polce) - jak dotad (roboty gminy). Kiesa wsi -> kasa miasta targowego (najpierw
        /// zloto, potem towar z polki).
        /// </summary>
        private static long DsBuyNeeds(Village vil, Settlement mk, List<EquipmentElement> list, double want, LDay d, double workExtra, out double keep)
        {
            keep = 0;
            if (list == null || list.Count == 0) return 0;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var pcs = new int[VillNeeds.NG]; int refused = 0, covered = 0;
            long spent = 0;
            try
            {
                var town = mk.Town; var roster = mk.ItemRoster;
                bool clOn = VillClothOn;
                var cats = new List<ItemCategory>();
                foreach (var e in list) { var c = e.Item.ItemCategory; if (!cats.Contains(c)) cats.Add(c); }
                double per = want / cats.Count;
                double perW = workExtra > 0 ? workExtra / cats.Count : 0;
                foreach (var c in cats)
                {
                    double left = per + (perW > 0 && VillWorkCat(c) ? perW : 0);
                    if (left < 1) continue;
                    // grupa potrzeb kategorii (pierwszy towar listy tej kategorii; lista wsi: sol, narzedzia, len, garnki, piwo - grupa wedlug kategorii)
                    ItemObject gi = null; int gc = -1;
                    foreach (var e in list) if (e.Item.ItemCategory == c) { double kg0; gc = VillNeeds.GroupOf(e.Item, out kg0); gi = e.Item; break; }
                    if (gc >= 0 && VillNeeds.Room(vil, gi) <= 0) { keep += left; covered++; continue; }   // potrzeba pokryta - cala czesc kategorii zostaje w kiesie
                    bool cloth = clOn && c.StringId == "linen", held = false;   // K2: plotno tylko ponad prog polki
                    foreach (var e in list)
                    {
                        if (left < 1) break;
                        if (e.Item.ItemCategory != c) continue;
                        int ix = roster.FindIndexOfElement(e);
                        if (ix < 0) continue;
                        int keepRow = ShelfKeep;
                        if (cloth)
                        {
                            bool half;
                            keepRow = VillShelfKeepRow(town, e.Item, out half);   // K2 + E3-W1b jak BuyGoods
                            if (half) d.VClHalf++;
                            d.VClCap += keepRow; d.VClRows++;
                        }
                        int have = roster.GetElementNumber(ix) - keepRow;
                        if (have <= 0) { if (keepRow > ShelfKeep && roster.GetElementNumber(ix) > ShelfKeep) { d.VClHeld++; held = true; } continue; }
                        int price = town.MarketData.GetPrice(e);
                        if (price <= 0) continue;
                        int n = (int)Math.Min(have, Math.Floor(left / price));
                        if (n <= 0) continue;
                        double kgPc; int g = VillNeeds.GroupOf(e.Item, out kgPc);
                        if (g >= 0)
                        {
                            int room = VillNeeds.Room(vil, e.Item);
                            if (room < n) { refused += n - Math.Max(0, room); n = Math.Max(0, room); }   // ponad zapas potrzeb - nie kupuje
                            if (n <= 0) continue;
                        }
                        if (keepRow > ShelfKeep && n == have) held = true;   // K2: zakup skonczyl sie na progu polki, nie na budzecie
                        int cost = n * price;
                        int had = vil.Gold;
                        if (cost > had) { n = had / price; cost = n * price; if (n <= 0) return spent; }
                        vil.ChangeGold(-cost);
                        int got = had - vil.Gold;
                        if (got <= 0) return spent;
                        town.ChangeGold(got);
                        roster.AddToCounts(e, -n);
                        spent += got; left -= got; d.VBuy += got; d.VPieces += n;
                        NoteVillBought(mk, c, got);
                        if (g >= 0) { pcs[g] += n; VillNeeds.Note(vil, e.Item, n); }
                        if (cloth) d.VClPcs += n;
                    }
                    if (left < 1) continue;
                    // potrzeba pokryta w trakcie (zakup do limitu zapasu) - reszta czesci kategorii zostaje w kiesie; plotno zatrzymane progiem K2 - tez w kiesie (K2);
                    // potrzeba otwarta bez towaru na polce - jak dotad (DsSurplus: roboty gminy)
                    if (gc >= 0 && VillNeeds.Room(vil, gi) <= 0) { keep += left; covered++; }
                    else if (held) { keep += left; d.VClKept += (long)left; }
                }
                return spent;
            }
            finally
            {
                VillNeeds.S5Limit(pcs, spent, refused, keep, covered);
                VillNeeds.S5Time(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
            }
        }
    }
}
