using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 360c (Jeff 08.11: "A i czemu nie liczy sie kruszcow na wozach - powinno liczyc sie cale srebro w obiegu gospodarki") - SPIS SREBRA I RUDY ZLOTA W OBIEGU.
    ///
    /// Jeden spis dla 360c (rece kopaln za nadwyzka rynku) i 360d (cena widzi caly zapas): wedlug rynku (0 Westeros, 1 Essos) i posiadacza - polki miast, wsie, zamki,
    /// wozy wsi (rynek domu wsi), juki karawan (rynek osady, w ktorej stoi, potem celu, potem domu - jak 285 w StartCalibration.Ruda) i wozy surowcow (rynek celu).
    /// Tabory lordow, bandy i gracz - poza spisem (jak 290). Tylko odczyt rosterow, watek glowny; wyjatek - false (bez danych), licznik potkniec.
    /// </summary>
    internal static class OreStock360
    {
        internal const int Metals = 2, MSilver = 0, MGold = 1;
        internal static readonly string[] MetalId = { "silver", "gold_ore" };
        internal const int HShelf = 0, HVil = 1, HCastle = 2, HVilCart = 3, HCaravan = 4, HMatCart = 5, HN = 6;
        internal static readonly string[] HolderName = { "polki miast", "wsie", "zamki", "wozy wsi", "juki karawan", "wozy surowcow" };

        private static Campaign _camp;
        private static readonly ItemObject[] _item = new ItemObject[Metals];
        internal static int Stumbles;
        private static bool _errLogged;

        /// <summary>Przedmiot kruszcu m (cache na kampanie); null - nie ma w grze.</summary>
        internal static ItemObject ItemOf(int m)
        {
            var c = Campaign.Current;
            if (!ReferenceEquals(c, _camp)) { _camp = c; for (int k = 0; k < Metals; k++) _item[k] = null; Stumbles = 0; _errLogged = false; }   // 360r-c/d: liczniki od zera w nowej kampanii
            if (m < 0 || m >= Metals) return null;
            if (_item[m] != null) return _item[m];
            try { var om = MBObjectManager.Instance; _item[m] = om != null ? om.GetObject<ItemObject>(MetalId[m]) : null; } catch { _item[m] = null; }
            return _item[m];
        }

        /// <summary>Kruszec m przedmiotu (-1 - inny towar).</summary>
        internal static int MetalOf(ItemObject it)
        {
            if (it == null) return -1;
            for (int m = 0; m < Metals; m++) if (ReferenceEquals(it, ItemOf(m))) return m;
            return -1;
        }

        /// <summary>Spis w sztukach: w[kruszec, rynek, posiadacz]. false - wyjatek (spis niepelny, w wyzerowany).</summary>
        internal static bool Census(long[,,] w)
        {
            if (w == null) return false;
            Array.Clear(w, 0, w.Length);
            try
            {
                var it = new ItemObject[Metals];
                bool any = false;
                for (int m = 0; m < Metals; m++) { it[m] = ItemOf(m); if (it[m] != null) any = true; }
                if (!any) return false;
                // polki miast
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || !t.IsTown || t.Settlement == null || t.Settlement.ItemRoster == null) continue;
                    int mk = StartCalibration.TownMarketOf(t); if (mk != 0 && mk != 1) continue;
                    for (int m = 0; m < Metals; m++) if (it[m] != null) { int n = t.Settlement.ItemRoster.GetItemNumber(it[m]); if (n > 0) w[m, mk, HShelf] += n; }
                }
                // wsie i zamki
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.IsTown || st.ItemRoster == null || !(st.IsVillage || st.IsCastle)) continue;
                    int mk = StartCalibration.MarketOfSettlement(st); if (mk != 0 && mk != 1) continue;
                    int h = st.IsVillage ? HVil : HCastle;
                    for (int m = 0; m < Metals; m++) if (it[m] != null) { int n = st.ItemRoster.GetItemNumber(it[m]); if (n > 0) w[m, mk, h] += n; }
                }
                // wozy wsi
                foreach (var vp in MobileParty.AllVillagerParties)
                {
                    if (vp == null || !vp.IsActive || vp.ItemRoster == null) continue;
                    int mk = StartCalibration.MarketOfSettlement(vp.HomeSettlement); if (mk != 0 && mk != 1) continue;
                    for (int m = 0; m < Metals; m++) if (it[m] != null) { int n = vp.ItemRoster.GetItemNumber(it[m]); if (n > 0) w[m, mk, HVilCart] += n; }
                }
                // juki karawan (jak 285: osada postoju, cel, dom)
                foreach (var cp in MobileParty.AllCaravanParties)
                {
                    if (cp == null || !cp.IsActive || cp.ItemRoster == null) continue;
                    var ct = cp.CurrentSettlement != null ? cp.CurrentSettlement.Town : null;
                    if (ct == null && cp.TargetSettlement != null) ct = cp.TargetSettlement.Town;
                    if (ct == null && cp.HomeSettlement != null) ct = cp.HomeSettlement.Town;
                    int mk = ct != null ? StartCalibration.TownMarketOf(ct) : -1; if (mk != 0 && mk != 1) continue;
                    for (int m = 0; m < Metals; m++) if (it[m] != null) { int n = cp.ItemRoster.GetItemNumber(it[m]); if (n > 0) w[m, mk, HCaravan] += n; }
                }
                // wozy surowcow (MaterialCarts - poza rosterami)
                MaterialCarts.ForEachInTransitDst((dst, item, n) =>
                {
                    int m = MetalOf(item); if (m < 0 || n <= 0) return;
                    int mk = StartCalibration.MarketOfSettlement(dst); if (mk != 0 && mk != 1) return;
                    w[m, mk, HMatCart] += n;
                });
                return true;
            }
            catch (Exception e)
            {
                Stumbles++;
                if (!_errLogged) { _errLogged = true; try { Log.Error("OreStock360.Census", e); } catch { } }   // pierwszy wyjatek do logu, reszta w licznikach
                Array.Clear(w, 0, w.Length);
                return false;
            }
        }

        /// <summary>Zapas rynku mk kruszcu m (suma posiadaczy od h0).</summary>
        internal static long Sum(long[,,] w, int m, int mk, int h0)
        {
            long s = 0;
            for (int h = h0; h < HN; h++) s += w[m, mk, h];
            return s;
        }
    }
}
