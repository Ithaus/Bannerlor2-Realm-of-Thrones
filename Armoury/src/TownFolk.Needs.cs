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
    /// rowno na kategorie listy, przy VillWorkOnly nadwyzka tylko na sol i narzedzia, cena targu, ponad 5 sztuk, plotno ponad prog polki K2), ale sztuki towarow potrzeb
    /// najwyzej do zapasu potrzeb wsi (VillNeeds.Room - ludzie x potrzeba dnia x dni zapasu, narzedzia wedlug zuzycia). Czesc budzetu odmowiona przez limit potrzeby
    /// ZOSTAJE W KIESIE wsi (nie idzie do robot gminy); czesc bez towaru na polce - jak dotad (roboty gminy).
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
        /// DsSurplus czesc 2 przy kluczu DsSurplusNeedLimit (wies bez koszyka ZC4): jak BuyGoods, ale towar potrzeb (sol, narzedzia, len, sukno, garnki) najwyzej do zapasu
        /// potrzeb wsi. `keep` - zl odmowione przez limit potrzeby (zostaja w kiesie). Kiesa wsi -> kasa miasta targowego (najpierw zloto, potem towar z polki).
        /// </summary>
        private static long DsBuyNeeds(Village vil, Settlement mk, List<EquipmentElement> list, double want, LDay d, double workExtra, out double keep)
        {
            keep = 0;
            if (list == null || list.Count == 0) return 0;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var pcs = new int[VillNeeds.NG]; int refused = 0;
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
                    bool cloth = clOn && c.StringId == "linen";
                    foreach (var e in list)
                    {
                        if (left < 1) break;
                        if (e.Item.ItemCategory != c) continue;
                        int ix = roster.FindIndexOfElement(e);
                        if (ix < 0) continue;
                        int keepRow = ShelfKeep;
                        if (cloth) { int r = MaterialOrders.ShelfReserve(town, e.Item); if (r > ShelfKeep) keepRow = r; }
                        int have = roster.GetElementNumber(ix) - keepRow;
                        if (have <= 0) continue;
                        int price = town.MarketData.GetPrice(e);
                        if (price <= 0) continue;
                        int n = (int)Math.Min(have, Math.Floor(left / price));
                        if (n <= 0) continue;
                        double kgPc; int g = VillNeeds.GroupOf(e.Item, out kgPc);
                        if (g >= 0)
                        {
                            int room = VillNeeds.Room(vil, e.Item);
                            if (room < n)
                            {
                                int no = n - Math.Max(0, room);
                                refused += no; keep += (double)no * price; left -= (double)no * price;   // odmowione przez limit potrzeby - zostaje w kiesie
                                n = Math.Max(0, room);
                            }
                            if (n <= 0) continue;
                        }
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
                    }
                }
                return spent;
            }
            finally
            {
                VillNeeds.S5Limit(pcs, spent, refused, keep);
                VillNeeds.S5Time(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
            }
        }
    }
}
