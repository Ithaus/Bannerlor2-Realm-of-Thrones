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
