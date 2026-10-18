using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// ds-akumulator (klucz DsWorksRealMaterial, noc 17/18.10; Jeff: "nic z niczego"): material robot gminy wsi i budowy septow z dziesieciny - PRAWDZIWY towar z polki
    /// miasta targowego, ta sama regula co material robot ludu (FolkBuy, P2): te same materialy budow (MatIds, deski pierwsze przy PlanksToBuilding), narzedzia - na polce
    /// zostaje 5 sztuk (wsie i warsztaty), drewno - nie mniej niz 50 i zapas rzemiosla miasta (K1), sufit ceny materialu (BuildMaterialPriceCap przy FolkBuildsTown), sztuki
    /// do 5 zl w wozach po 10. Wolane PO budowach pana i ludu tej doby (TownFolk.DaWorksRun) - zlecenia wsi nie zabieraja materialu budowom pana. Sam towar: sztuki zdjete
    /// (ShelfBuy.Take), zwraca koszt - zaplate kasie targu robi wolajacy (zloto platnika -> kasa). Liczniki sztuk wedlug MatIds w `pieces`. Nie rzuca.
    /// </summary>
    internal static partial class BuildFunding
    {
        /// <summary>Nazwy materialow w kolejnosci licznika sztuk (linia "Akumulator kies").</summary>
        internal static string DsWorksMatNames { get { return string.Join("/", MatIds); } }
        internal static int DsWorksMatCount { get { return MatIds.Length; } }

        internal static int DsWorksBuy(Settlement market, double budget, int[] pieces, out int capSkip)
        {
            capSkip = 0;
            int spent = 0;   // poza try: wyjatek po zdjeciu sztuk zwraca to, co juz zeszlo z polki (wolajacy za to placi - nic w nicosc)
            try
            {
                if (market == null || market.Town == null || market.ItemRoster == null || budget < 1 || pieces == null || pieces.Length < MatIds.Length) return 0;
                var roster = market.ItemRoster;
                var order = MatOrder;
                for (int pass = 0; pass < 3 && spent < budget; pass++)
                    foreach (int k in order)
                    {
                        var it = MBObjectManager.Instance.GetObject<ItemObject>(MatIds[k]);
                        if (it == null) continue;
                        int have = roster.GetItemNumber(it);
                        if (MatIds[k] == "tools") have -= ToolsKeep;
                        else if (MatIds[k] == "hardwood") have -= Math.Max(WoodKeep, WoodHold(market, it, true));
                        if (have <= 0) continue;
                        int price = Math.Max(1, market.Town.GetItemPrice(it, null, false));
                        int cap = CapOf(it);
                        if (cap > 0 && price > cap) { if (pass == 0) capSkip++; continue; }
                        int n = Math.Min(have, (int)((budget - spent) / price / (3 - pass)) + (pass == 2 ? 0 : 1));
                        n = Math.Min(n, (int)((budget - spent) / price));
                        if (n <= 0) continue;
                        var mel = new EquipmentElement(it);
                        var town = market.Town;
                        Func<int> pf;
                        if (price <= WagonPrice)
                        {
                            int cnt = 0, cur = price;
                            pf = () => { if (++cnt % WagonN == 0) cur = Math.Max(1, town.GetItemPrice(mel, null, false)); return cur; };
                        }
                        else pf = () => Math.Max(1, town.GetItemPrice(mel, null, false));
                        int cost, first, last;
                        n = ShelfBuy.Take(roster, mel, n, (int)(budget - spent), pf, out cost, out first, out last, price, true, cap);
                        if (n <= 0) continue;
                        OreLedger.NoteBuild(it, n);   // ksiega rudy i drewna: pozycja "budowy" (tylko licznik)
                        pieces[k] += n; spent += cost;
                        if (spent >= budget) break;
                    }
                return spent;
            }
            catch (Exception e) { FolkStumble("DsWorksBuy", e); return spent; }
        }
    }
}
