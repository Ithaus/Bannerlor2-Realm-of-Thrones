using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// f7-wegiel (noc 18.10): WEGLARZE PRZY KOPALNI - dane okregu KS dla Bloomery.F7 (Bloomery.F7.cs, klucz Bloomery Colliers).
    /// Jedna tabela swiata: rece przy zlozu zelaza w blokach okregu (RF x rece bloku, jak Deliver.Produce), gdyby zamiast rudy ciely i palily drewno na wegiel, daja
    /// P0 drwala (hardwood 400 kg na pracujacego na dobe) x przydatnosc lasu bloku (KsTab.Land2 lumberjack: strefa x teren, pustynia i step 0) zamiast P0 zelaza (2 kg)
    /// x przydatnosc zloza. Wynik: ladunki drewna, ktore daja rece jednej lupy gry (r = kg drewna / kg zelaza x kg lupy / kg ladunku) - bez czynnika okregu, krainy
    /// ani towaru. Ta sama przydatnosc co KsPart.L dla regale (bez pamieci bloku - czysta funkcja, wolne od wyscigu z watkiem z puli diagnozy A7).
    /// </summary>
    internal static partial class StartCalibration
    {
        /// <summary>Bloomery.Daily (watek gry, raz na dobe na kopalnie): r - ladunki drewna z lasu okregu, ktore daja rece jednej lupy gry przy weglu zamiast przy rudzie;
        /// hpb - ludzie przy zelazie na jedna lupe gry na dobe (do linii: ludzie weglarzy); lumberL - srednia przydatnosc lasu blokow z zelazem (waga rak).
        /// 0 - wies poza KS zastosowanym, kraina wrocona (bez regali), pola mieszczan, okreg bez rak przy zelazie albo bez lasu (pustynia, step). Nie rzuca.</summary>
        internal static float F7CollierRatio(Village v, out float hpb, out float lumberL)
        {
            hpb = 0f; lumberL = 0f;
            try
            {
                if (v == null || v.Settlement == null || !AppliedFor(v)) return 0f;
                var st = v.Settlement;
                LiveDist ld;
                if (st.StringId == null || !_live.TryGetValue(st.StringId, out ld) || ld == null || ld.TF || ld.B == null || ld.K == null) return 0f;
                if (LandBackOf(st)) return 0f;                                  // ks-arm: kraina wrocona - bez regali (jak F5IronWeek)
                EnsureItems();
                double kgB = _itemKg != null ? _itemKg[KsDeliver.IIron] : 0.0, kgW = _itemKg != null ? _itemKg[KsDeliver.IWood] : 0.0;
                double p0i = P0Of(KsTab.CIron, KsTab.GIron), p0l = P0Of(KsTab.CLumber, KsTab.GWood);
                if (!(kgB > 0) || !(kgW > 0) || !(p0i > 0) || !(p0l > 0)) return 0f;
                string type = ld.K.Type;
                double h = 0, kgI = 0, kgL = 0;
                foreach (var b in ld.B)
                {
                    if (b == null || b.K == null || b.K.O == null || b.RC == null || b.RF == null) continue;
                    for (int j = 0; j < b.RC.Length && j < b.RF.Length; j++)
                    {
                        if (b.RC[j] != KsTab.CIron) continue;
                        double lab = b.RF[j] * b.Hands;
                        if (!(lab > 0)) continue;
                        double li = RegL(b.K, KsTab.CIron, type), ll = RegL(b.K, KsTab.CLumber, type);
                        h += lab; kgI += lab * p0i * li; kgL += lab * p0l * ll;
                    }
                }
                if (!(h > 0) || !(kgI > 0)) return 0f;
                double sigma = Math.Max(1, DailyLife.KsSigma);
                hpb = (float)(h * sigma * kgB / kgI);
                lumberL = (float)(kgL / (h * p0l));
                double r = kgL / kgI * kgB / kgW;
                return r > 0 && !double.IsInfinity(r) && !double.IsNaN(r) ? (float)r : 0f;
            }
            catch (Exception e) { HookStumble("F7CollierRatio", e); return 0f; }
        }

        /// <summary>Przydatnosc ziemi zajecia regale c w bloku kb - ta sama regula co KsPart.L (blok wsi gry przy typie wsi - LandEff, inaczej land2), bez pamieci.</summary>
        private static double RegL(KsBlock kb, int c, string type)
        {
            return (kb.Main && KsTab.Crafts[c] == type) ? KsCore.LandEff(kb, c, type) : KsTab.Land2(c, kb.O, type);
        }

        /// <summary>P0 towaru g zajecia c (kg na pracujacego na dobe przy ziemi 1) z jednej tabeli swiata; 0 - brak.</summary>
        private static double P0Of(int c, int g)
        {
            foreach (var kv in KsTab.P0[c]) if (kv.Key == g) return kv.Value;
            return 0.0;
        }
    }
}
