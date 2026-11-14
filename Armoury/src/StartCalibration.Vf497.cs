using System;

namespace Armoury
{
    /// <summary>
    /// 497 (noc 14.11, klucz Ks Plan Knows Village Factors, domyslnie TAK): Plan KS zna czynniki, ktore gra doklada do plonu wsi PO Planie.
    /// Pomiar 490 (tasks/wolzo6s5a, meatGrain490): dostawa wsi w grze = Plan x (rok urodzaju / ziemia zmeczona ok. +3.4% Westeros, -6% Essos) x ScaleRates
    /// (drogi i budynki BK, 338 - ok. x1.10 na targu); Plan tego nie wiedzial, wiec zboze Westeros na targ wychodzilo 1.21-1.27 Planu (nadmiar).
    /// Gra (watek glowny, przy przeliczeniu okregu): na towar dostawy i okreg F = (po roku urodzaju / przed; 497-p: ziemia zmeczona tylko w grze, R3-06d) x (stol + targ x s) / (stol + targ),
    /// s = (stawka A po ScaleRates + PlanBack chleba i skory) / na targ przed ScaleRates (to samo, co dotad dostaje mkByM). Wejscie Planu (watek glowny, BuildInput):
    /// zwykle tablice KsDistrict.Vf [NG] i VfItem [ItemCount] - watek roboczy tylko je czyta. Plan (KsCore.Pairs i PairFood): produkcja pary x F towaru
    /// (zywiec x F, a mieso i skory z niego za nim; wieloryb / dziczyzna x F). Wlasny stol w grze nie dostaje ScaleRates - dlatego F miesza s tylko w czesci na targ.
    /// Zamowienia 363 nie wchodza (towar zamowien to nie plon roku). Zima i rece (spustoszenie, wojna) nie wchodza - Plan liczy bez zimy, rece ma z czynnikami.
    /// Regale (rudy, drewno, sol za cena) bez zmian. Klucz NIE - Vf null, Plan co do bitu jak dotad.
    /// </summary>
    internal static partial class StartCalibration
    {
        private static bool SwVf497 { get { var s = Settings.Current; return s != null && s.KsPlanKnowsVillageFactors; } }
        private const double Vf497Lo = 0.5, Vf497Hi = 2.0;
        private static int _vf497St;
        private static int _vf497Kept, _vf497Rebuilt;   // 497-p: rachunki zimowe - czynniki zostawione (ostatnie sprzed zimy) / odbudowane na podstawie bez zimy (od startu sesji)

        /// <summary>497 (watek glowny, po ScaleRates okregu): licznik i mianownik czynnika gry na towar. a - stawka A wsi po ScaleRates (null - pola mieszczan: s = 1).
        /// Bez PreY/PostY (klucz NIE albo rachunek bez czynnikow) - VfNum/VfDen null. Nie rzuca (potkniecie liczone).</summary>
        private static void Vf497Note(LiveDist ld, DistResult res, float[] a)
        {
            if (ld == null) return;
            try
            {
                if (res == null || res.PreY == null || res.PostY == null) { ld.VfNum = null; ld.VfDen = null; return; }
                // 497-p (recenzja): w zimie stol okregu (pelna potrzeba, bez ciecia) bierze wieksza czesc scietego plonu, targ spada do ~0, mieszanka do 1 i F do samego
                // roku urodzaju - Plan (bez zimy) dostalby co sezon inny cel. Dlatego w zimie: ostatnie czynniki zostaja (nie nadpisujemy); bez nich (pierwszy
                // rachunek w zimie) - podstawa bez zimy: na targ' = (stol + targ) / czesc zimowa - stol (stol jak w zimie - dolne oszacowanie stolu bez zimy).
                bool win = res.WinterF < 0.999;
                if (win && ld.VfNum != null && ld.VfDen != null) { _vf497Kept++; return; }
                if (win) _vf497Rebuilt++;
                var num = new double[ItemCount]; var den = new double[ItemCount];
                for (int i = 0; i < ItemCount; i++)
                {
                    double p0 = res.PreY[i];
                    if (!(p0 > 1e-9)) continue;
                    double y = Math.Max(0.0, res.PostY[i]) / p0;
                    double own = Math.Max(0.0, res.Own[i]), mk = Math.Max(0.0, res.MarketKg[i]), tot = own + mk;
                    if (win && !(res.WinterSp != null && i < res.WinterSp.Length && res.WinterSp[i])) { tot = tot / res.WinterF; mk = Math.Max(0.0, tot - own); }   // 497-p: podstawa bez zimy
                    double s = 1.0;
                    if (a != null && i < a.Length && res.Market[i] > 1e-9)
                    {
                        double back = 0; try { back = Buildings338Village.PlanBack(ld.V, i); } catch { back = 0; _vf497St++; }
                        s = (Math.Max(0.0, a[i]) + Math.Max(0.0, back)) / res.Market[i];
                    }
                    double mix = tot > 1e-9 ? (own + s * mk) / tot : 1.0;
                    double f = y * mix;
                    if (double.IsNaN(f) || double.IsInfinity(f)) continue;
                    num[i] = p0 * f; den[i] = p0;
                }
                ld.VfNum = num; ld.VfDen = den;
            }
            catch { _vf497St++; ld.VfNum = null; ld.VfDen = null; }
        }

        private static double Vf497Clamp(double n, double d) { return d > 1e-9 ? Math.Max(Vf497Lo, Math.Min(Vf497Hi, n / d)) : 1.0; }

        /// <summary>497 (watek glowny, BuildInput): czynniki okregu Planu z ostatniego rachunku gry tego okregu (_live wedlug Id). Klucz NIE, brak rachunku,
        /// wszystkie F = 1 - Vf/VfItem null (Plan jak dotad). Zwraca true, gdy okreg dostal czynniki.</summary>
        private static bool Vf497Input(KsDistrict kd)
        {
            if (kd == null) return false;
            kd.Vf = null; kd.VfItem = null;
            if (!SwVf497) return false;
            try
            {
                LiveDist ld;
                var live = _live;
                if (live == null || kd.Id == null || !live.TryGetValue(kd.Id, out ld) || ld == null || ld.VfNum == null || ld.VfDen == null) return false;
                var vi = new double[ItemCount]; bool any = false;
                for (int i = 0; i < ItemCount; i++) { vi[i] = Vf497Clamp(ld.VfNum[i], ld.VfDen[i]); if (Math.Abs(vi[i] - 1.0) > 1e-6) any = true; }
                if (!any) return false;
                var vg = new double[KsTab.NG];
                for (int g = 0; g < KsTab.NG; g++)
                {
                    vg[g] = 1.0;
                    if (g == KsTab.GMeat || g == KsTab.GHides || g == KsTab.GHorse) continue;   // mieso i skory z zywca idzie za zywcem (ApplyVf); konie - lancuch gry
                    if (g == KsTab.GWhaleMeat)
                    {
                        // wieloryb wielorybnika i dziczyzna mysliwego (w grze IMeat) - jeden czynnik z obu
                        int w = KsDeliver.IWhaleMeat, mt = KsDeliver.IMeat;
                        vg[g] = Vf497Clamp(ld.VfNum[w] + ld.VfNum[mt], ld.VfDen[w] + ld.VfDen[mt]);
                        continue;
                    }
                    int it = KsDeliver.GoodToItem[g];
                    if (it >= 0 && it < ItemCount) vg[g] = vi[it];
                }
                kd.Vf = vg; kd.VfItem = vi;
                return true;
            }
            catch { _vf497St++; kd.Vf = null; kd.VfItem = null; return false; }
        }
    }

    internal sealed partial class KsDistrict
    {
        // 497: czynniki gry po Planie (rok urodzaju x ScaleRates czesci na targ; 497-p: bez ziemi zmeczonej) - [NG] towar Planu i [ItemCount] towar dostawy; null - 1 (jak dotad).
        // Zwykle tablice z watku glownego (BuildInput), watek roboczy tylko czyta.
        public double[] Vf, VfItem;
    }

    internal static partial class KsCore
    {
        /// <summary>497: produkcja pary (BlockOutput, kg/d) x czynnik gry towaru. Zywiec x F, mieso i skory z niego za nim; wieloryb / dziczyzna x F (mieso Planu za nim).
        /// Mieso, skory i konie bezposrednio - bez zmian. vf null - nic.</summary>
        internal static void ApplyVf(double[] vf, double[] o)
        {
            if (vf == null || o == null) return;
            double dm = 0, dh = 0;
            for (int g = 0; g < KsTab.NG && g < vf.Length; g++)
            {
                double v = o[g];
                if (v == 0.0 || g == KsTab.GMeat || g == KsTab.GHides || g == KsTab.GHorse) continue;
                double f = vf[g];
                if (f == 1.0) continue;
                double dl = v * (f - 1.0);
                o[g] = v + dl;
                if (g == KsTab.GCow || g == KsTab.GHog || g == KsTab.GSheep) { dm += dl * KsTab.MeatOfLive; dh += dl * KsTab.HideOfLive(g); }
                else if (g == KsTab.GWhaleMeat) dm += dl;
            }
            o[KsTab.GMeat] += dm; o[KsTab.GHides] += dh;
        }
    }
}
