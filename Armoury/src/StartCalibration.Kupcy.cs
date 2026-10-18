using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// KS-R4 r4-kupcy (noc 17/18.10): dostep kupcow zywnosci KS-H do stanu kalibracji - cena startu pozycji koszyka (H4: sufit sztuki 3 x cena startu) i wejscie korytarzy
    /// Planu z modelem kredytu (H7 = K1: zapas kupcow miast do przepustowosci importera i eksportera). Osobny plik czesciowy (bez zmian w plikach rund 3/4).
    /// </summary>
    internal static partial class StartCalibration
    {
        /// <summary>Indeks ceny startu pozycji koszyka z na rynku m (Plan; bez Planu 1.0) - to samo co sufit pustej polki ZC4 i T* (ZTstar).</summary>
        internal static double ZStartIndex(int m, int z)
        {
            if (z < 0 || z >= ZN) return 1.0;
            return KsCore.StartIndex(ZIds[z], _plan != null ? _plan.P : null, m == 0 ? 0 : 1);
        }

        /// <summary>T9 (bit w bit, KS-H): popyt zaplacony (ZDp) i odbior 7 dob (ZOff7) calego wiersza miasta naraz - jedno szukanie miasta zamiast jednego na towar.
        /// t - miasto, ti - indeks KS-H (KsFoodCarts.IndexOf), z[k] - pozycje koszyka, has[k] - towar w swiecie gry (bez - pole bez zmian, jak w Daily).</summary>
        internal static void ZDpOffRow(Town t, int ti, int[] z, bool[] has, double[] dp, double[] off, int o0)
        {
            ZTown zt = null;
            var map = _zt;
            bool ok = t != null && t.Settlement != null && map.Count > 0 && map.TryGetValue(t.Settlement, out zt) && zt != null;
            var o7 = _off7;
            for (int k = 0; k < z.Length; k++)
            {
                if (!has[k]) continue;
                int zz = z[k];
                double v = 0.0;
                if (ok && zz >= 0 && zz < ZN) { v = zt.Q[zz] + zt.QV[zz] + zt.Der[zz] + zt.One[zz]; if (!(v > 0) || double.IsNaN(v)) v = 0.0; }
                dp[o0 + k] = v;
                off[o0 + k] = ti >= 0 && o7 != null && ti * ZN + zz < o7.Length && zz >= 0 ? o7[ti * ZN + zz] : 0.0;
            }
        }

        /// <summary>H7: klucz Ks Corridors Credit czynny (z Ks Food Carts Consign) - korytarze Planu liczone modelem kredytu kupcow.</summary>
        private static bool SwCorridorsCredit { get { var s = Settings.Current; return s == null || (s.KsCorridorsCredit && s.KsFoodCartsConsign); } }

        /// <summary>H7 (wejscie Planu, watek glowny): zapas kupcow miast Planu (Seal31.TownReserve) i klucz korytarzy na kredyt.</summary>
        private static void KupcyPlanInputs(KsInput inp, List<Settlement> tst)
        {
            inp.CorrCredit = SwCorridorsCredit;
            if (!inp.CorrCredit || tst == null) { inp.TownRes = null; return; }
            int nt = tst.Count;
            inp.TownRes = new double[nt];
            for (int i = 0; i < nt; i++)
            {
                try { var t = tst[i] != null ? tst[i].Town : null; inp.TownRes[i] = t != null ? Math.Max(0L, Seal31.TownReserve(t)) : 0.0; }
                catch { inp.TownRes[i] = 0.0; }
            }
        }
    }
}
