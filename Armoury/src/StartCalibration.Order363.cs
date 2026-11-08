using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 363 (Jeff 08.11: "Wsie napraw to, trzeba naprawic ekonomie" - wybor "Ukryte zloto do lupu": produkcja materialu na zamowienie u wsi drwali i gliniarzy) - strona KS:
    /// wies-wytworca (zajecie regale okregu: lumberjack - hardwood, clay_mine - clay, quarry - marble, lime_pit - limestone; jedno zajecie na okreg - to z najwiekszymi
    /// rekami) zabiera rece z pola (wheat_farm) na zamowienie. Rachunek okregu (ComputeDistrict, tylko gra, co LiveEvery dob): H = min(popyt / sztuki na reke, pulap
    /// VillageOrderFieldShare x rece pola), narastanie najwyzej +1% rak pola na przeliczenie, spadek od razu; glod, rampa KS, wies nie Normal, prace okregu gracza,
    /// klucz wylaczony - H = 0. Kg zajecia wheat_farm (zboze i drobny inwentarz P0) spada o H / F, towar rosnie o H x kg na reke PRZED zima i czynnikami rak; po
    /// czynnikach towar zamowienia wychodzi z produkcji przed stolem okregu (nie spalony jako opal, nie na wozy) jako sztuki doby (DistResult.OrdPcs). Zywnosc: mniej
    /// wlasnego zboza - mniej OwnKcal i racji, koszyk ZC4 dokupuje zboze w miescie z kiesy wytworcy (istniejace reguly KS). Sprzedaz: TownFolk.Order363.cs (DaRunOne:
    /// po polce targu, kiesa zamawiajacej -> kiesa wytworcy po cenie jego targu; niesprzedane sztuki doby przepadaja - rece bez roboty). Pamiec sesji, bez zapisu.
    /// Kazdy okreg we wlasnym try (licznik potkniec KS), bez globalnego wylacznika.
    /// </summary>
    internal static partial class StartCalibration
    {
        private const double OrdRiseStep = 0.01;          // narastanie rak na zamowienie: najwyzej 1% rak pola okregu na przeliczenie (spadek od razu)
        private const double OrdEmaA = 1.0 / 7.0;          // popyt wytworcy: EMA sztuk na dobe (zakupy + przypisany popyt niezaspokojony)
        private const double OrdMinCraftShare = 0.05;      // poprawka 363 (recenzja 2): wytworca tylko okreg, w ktorym zajecie materialowe ma co najmniej 5% rak okregu (nie wies zbozowa ze sladem drwali)

        /// <summary>Zajecia materialowe 363 i ich towar dostawy (KsDeliver): drewno, glina, wapien, marmur.</summary>
        internal static readonly int[] OrdCrafts = { KsTab.CLumber, KsTab.CClay, KsTab.CLime, KsTab.CQuarry };
        internal static readonly int[] OrdItems = { KsDeliver.IWood, KsDeliver.IClay, KsDeliver.ILime, KsDeliver.IMarble };
        internal static readonly string[] OrdNames = { "hardwood", "clay", "limestone", "marble" };
        internal static readonly string[] OrdCraftNames = { "lumberjack", "clay_mine", "lime_pit", "quarry" };

        private static int OrdSlot(int craft) { for (int i = 0; i < OrdCrafts.Length; i++) if (OrdCrafts[i] == craft) return i; return -1; }

        /// <summary>Klucz 363 czynny (Village Made To Order, zasieg i pulap > 0, roboty gminy z prawdziwym materialem).</summary>
        internal static bool OrderOn
        {
            get
            {
                var s = Settings.Current;
                return s != null && s.VillageMadeToOrder && s.VillageOrderRange > 0f && s.VillageOrderFieldShare > 0f && TownFolk.DaWorksOn;
            }
        }

        /// <summary>Rece zajecia C[j] bloku poza domem i zbozem wlasnym x (1 - praca domowa) - ten sam wzor co BlockCraftKg.</summary>
        private static double OrdCraftHands(KsPart lb, int j)
        {
            if (lb == null || lb.C == null || j < 0 || j >= lb.C.Length) return 0.0;
            int c = lb.C[j];
            double rsum = 0; if (lb.RF != null) for (int k = 0; k < lb.RF.Length; k++) rsum += lb.RF[k];
            double x = lb.X[j] * Math.Max(0.0, 1.0 - rsum);
            if (!(x > 0)) return 0.0;
            double rem = Math.Max(0.0, lb.Hands - lb.WH);
            double sub = c != KsTab.CWheat && lb.Lg > 0 ? Math.Min(rem, lb.SubNW) : 0.0;
            double hw = lb.HW > 0 ? Math.Min(0.95, lb.HW) : 0.0;
            double cl = (rem - sub) * (1.0 - hw);
            return cl > 0 ? x * cl : 0.0;
        }

        private static double OrdP0(int craft, int item)
        {
            foreach (var kv in KsTab.P0[craft]) if (KsDeliver.GoodToItem[kv.Key] == item) return kv.Value;
            return 0.0;
        }

        /// <summary>
        /// ComputeDistrict (z czynnikami, wies; po HerdKs, przed WorksKs i zima): wytworca - rece na zamowienie z pola do towaru zajecia materialowego. true - okreg jest
        /// wytworca (OrderAfter po czynnikach liczy sztuki doby i czynnik zimy), takze przy H = 0. Nie rzuca.
        /// </summary>
        private static bool OrderKs(LiveDist ld, double[] prod, double[] kgT)
        {
            if (ld == null) return false;
            ld.OrdAddKg = 0; ld.OrdGrain0 = 0; ld.OrdGrain1 = 0; ld.OrdHungry = false; ld.OrdPre = 0; ld.OrdPreGrain = 0;
            if (ld.TF || ld.V == null || ld.B == null || ld.K == null || prod == null || kgT == null) { ld.OrdH = 0; ld.OrdCraft = -1; ld.OrdItem = -1; ld.OrdOk = false; return false; }
            try
            {
                string type = ld.K.Type;
                var hc = new double[OrdCrafts.Length]; var kc = new double[OrdCrafts.Length];
                double F = 0, all = 0;
                foreach (var lb in ld.B)
                {
                    if (lb == null || lb.K == null || lb.C == null || lb.X == null) continue;
                    if (lb.Hands > 0) all += lb.Hands;
                    for (int j = 0; j < lb.C.Length; j++)
                    {
                        int c = lb.C[j];
                        if (c == KsTab.CWheat) { F += OrdCraftHands(lb, j); continue; }
                        int m = OrdSlot(c);
                        if (m < 0) continue;
                        double h = OrdCraftHands(lb, j);
                        if (!(h > 0)) continue;
                        hc[m] += h; kc[m] += h * OrdP0(c, OrdItems[m]) * lb.L(c, type);
                    }
                    if (lb.RC != null && lb.RF != null)
                        for (int j = 0; j < lb.RC.Length && j < lb.RF.Length; j++)
                        {
                            int m = OrdSlot(lb.RC[j]);
                            if (m < 0) continue;
                            double lab = lb.RF[j] * lb.Hands;                 // regale: rece bloku przy zlozu (jak KsDeliver.Produce)
                            if (!(lab > 0)) continue;
                            hc[m] += lab; kc[m] += lab * OrdP0(lb.RC[j], OrdItems[m]) * lb.L(lb.RC[j], type);
                        }
                }
                int best = -1; double bh = 0;
                for (int m = 0; m < hc.Length; m++) if (hc[m] > bh && kc[m] > 0) { bh = hc[m]; best = m; }
                if (best >= 0 && !(bh >= OrdMinCraftShare * all)) best = -1;   // poprawka 363 (recenzja 2): slad zajecia materialowego - nie wytworca
                if (best < 0) { ld.OrdH = 0; ld.OrdCraft = -1; ld.OrdItem = -1; ld.OrdCapH = 0; ld.OrdFieldH = 0; ld.OrdOk = false; return false; }
                int it = OrdItems[best];
                ld.OrdCraft = OrdCrafts[best]; ld.OrdItem = it;
                ld.OrdKgHand = kc[best] / hc[best];
                ld.OrdFieldH = F;
                var s = Settings.Current;
                double share = s != null ? Math.Max(0.0, Math.Min(0.25, (double)s.VillageOrderFieldShare)) : 0.0;
                ld.OrdCapH = share * F;
                bool ok = OrderOn && F > 0 && RampFull(ld.Market == 0 ? 0 : 1) && ld.V.VillageState == Village.VillageStates.Normal && !DistrictWorks.HasWorks(ld.V);
                if (ok && Famine.HungerOf(ld.V) > 0f) { ld.OrdHungry = true; ok = false; }
                ld.OrdOk = ok;   // poprawka 363 (recenzje 1 i 2): wytworca bez prawa do rak (glod, rampa, prace okregu, nie Normal) - w ofercie "na pulapie", popyt idzie do zdrowego
                // poprawka 363 (recenzja 1): narastanie wedlug czasu, nie liczby przeliczen (_liveFull, KsDirty, przelom pory) - +1% rak pola na LiveEvery dob
                int today = (int)CampaignTime.Now.ToDays;
                double days = ld.OrdDay == int.MinValue ? LiveEvery : today - ld.OrdDay;
                double riseK = Math.Max(0.0, Math.Min(1.0, days / LiveEvery));
                ld.OrdDay = today;
                double H = 0;
                if (ok)
                {
                    double sigma = Math.Max(1, DailyLife.KsSigma);
                    double f = ld.OrdF > 0 && !double.IsNaN(ld.OrdF) ? ld.OrdF : 1.0;
                    double pcsHand = ld.OrdKgHand * f / (sigma * Math.Max(0.01, kgT[it]));
                    double want = pcsHand > 1e-9 && ld.OrdDemand > 0 ? ld.OrdDemand / pcsHand : 0.0;
                    double prev = Math.Max(0.0, ld.OrdH);
                    H = want > prev ? Math.Min(want, prev + OrdRiseStep * F * riseK) : want;
                    H = Math.Max(0.0, Math.Min(H, ld.OrdCapH));
                    if (double.IsNaN(H) || double.IsInfinity(H)) H = 0;
                }
                ld.OrdH = H;
                if (H > 0)
                {
                    // rece z pola: kg zajecia wheat_farm (zboze i drobny inwentarz P0) spada o H / F
                    double frac = Math.Min(1.0, H / F);
                    var tmp = new double[ItemCount];
                    foreach (var lb in ld.B)
                    {
                        if (lb == null || lb.K == null || lb.C == null || lb.X == null) continue;
                        for (int j = 0; j < lb.C.Length; j++) if (lb.C[j] == KsTab.CWheat) BlockCraftKg(ld.K, lb, j, type, tmp);
                    }
                    for (int i = 0; i < ItemCount; i++)
                    {
                        if (!(tmp[i] > 0)) continue;
                        double cut = Math.Min(prod[i], tmp[i] * frac);
                        prod[i] -= cut;
                        if (i == IGrain) ld.OrdGrain0 += cut;
                    }
                    ld.OrdAddKg = H * ld.OrdKgHand;
                    prod[it] += ld.OrdAddKg;
                }
                ld.OrdPre = prod[it]; ld.OrdPreGrain = prod[IGrain];
                return true;
            }
            catch (Exception e) { Stumble("OrderKs", e); ld.OrdH = 0; ld.OrdAddKg = 0; ld.OrdCraft = -1; ld.OrdItem = -1; ld.OrdOk = false; return false; }
        }

        /// <summary>ComputeDistrict (po zimie, czynnikach rak, HarvestYear i LandKs; przed KsDeliver.Own): towar zamowienia wyjety z produkcji - sztuki doby (OrdPcs);
        /// czynnik zimy i rak towaru zapamietany do nastepnego rachunku (sztuki na reke). Nie rzuca.</summary>
        private static void OrderAfter(LiveDist ld, DistResult res, double[] prod, double[] kgT)
        {
            try
            {
                int it = ld.OrdItem;
                res.OrdPcs = 0;
                if (it < 0 || it >= ItemCount) return;
                double r = ld.OrdPre > 1e-12 ? prod[it] / ld.OrdPre : 1.0;
                if (double.IsNaN(r) || double.IsInfinity(r) || r < 0) r = 1.0;
                ld.OrdF = Math.Min(2.0, r);
                if (!(ld.OrdAddKg > 0)) return;
                double ord = Math.Max(0.0, Math.Min(prod[it], ld.OrdAddKg * r));
                prod[it] -= ord;
                // linia 297 (kg produkcji, ktorej zima nie zabiera): bez towaru zamowienia
                if (res.WSpareKg != null && ld.OrdPre > 1e-12) res.WSpareKg[it] *= Math.Max(0.0, 1.0 - ld.OrdAddKg / ld.OrdPre);
                double sigma = Math.Max(1, DailyLife.KsSigma);
                res.OrdPcs = ord / (sigma * Math.Max(0.01, kgT[it]));
                double g = ld.OrdPreGrain > 1e-12 ? prod[IGrain] / ld.OrdPreGrain : 1.0;
                if (double.IsNaN(g) || double.IsInfinity(g) || g < 0) g = 1.0;
                ld.OrdGrain1 = ld.OrdGrain0 * g;
            }
            catch (Exception e) { Stumble("OrderAfter", e); res.OrdPcs = 0; }
        }

        // ============================================================ odczyty i zapisy dla TownFolk.Order363 (watek glowny)
        private static LiveDist OrdLd(Village v)
        {
            if (v == null || v.Settlement == null || v.Settlement.StringId == null || _state != StateApplied) return null;
            LiveDist ld;
            return _live.TryGetValue(v.Settlement.StringId, out ld) ? ld : null;
        }

        /// <summary>Wsie-wytworcy KS (zajecie materialowe z ostatniego rachunku okregu) - do pamieci zasiegu zamawiajacych.</summary>
        internal static void OrderProducers(List<Village> into)
        {
            if (into == null) return;
            into.Clear();
            if (_state != StateApplied || !KeyOn) return;
            foreach (var ld in _live.Values)
            {
                if (ld == null || ld.TF || ld.V == null || ld.St == null || ld.OrdCraft < 0 || ld.OrdItem < 0) continue;
                if (LandBackOf(ld.St)) continue;
                into.Add(ld.V);
            }
        }

        /// <summary>Oferta wytworcy tej doby: towar, sztuki do sprzedania (z ulamkiem) i czy rece na zamowienie doszly do pulapu. false - nie wytworca albo bez rachunku.</summary>
        internal static bool OrderOffer(Village p, out ItemObject it, out double left, out bool atCap, out int slot)
        {
            it = null; left = 0; atCap = false; slot = -1;
            var ld = OrdLd(p);
            if (ld == null || ld.OrdItem < 0 || ld.OrdCraft < 0 || ld.Res == null) return false;
            slot = OrdSlot(ld.OrdCraft);
            if (slot < 0) return false;
            EnsureItems();
            it = _items != null ? _items[ld.OrdItem] : null;
            if (it == null) return false;
            left = Math.Max(0.0, ld.OrdLeft);
            atCap = !ld.OrdOk || ld.OrdCapH <= 0 || ld.OrdH >= ld.OrdCapH * 0.999;   // poprawka 363 (recenzje 1 i 2): bez prawa do rak = na pulapie
            return true;
        }

        /// <summary>Sprzedaz n sztuk tej doby (po zaplacie) - takze popyt doby wytworcy.</summary>
        internal static void OrderTake(Village p, int n)
        {
            var ld = OrdLd(p);
            if (ld == null || n <= 0) return;
            ld.OrdLeft = Math.Max(0.0, ld.OrdLeft - n);
            ld.OrdToday += n;
        }

        /// <summary>Popyt niezaspokojony przypisany wytworcy (sztuki) - do EMA popytu (rece przy nastepnym rachunku okregu).</summary>
        internal static void NoteOrderDemand(Village p, double pcs)
        {
            var ld = OrdLd(p);
            if (ld == null || !(pcs > 0) || double.IsNaN(pcs) || double.IsInfinity(pcs)) return;
            ld.OrdToday += pcs;
        }

        /// <summary>Poczatek doby zamowien (TownFolk.DaWorksRun): sztuki doby z ostatniego rachunku okregu dopisane do ulamka z wczoraj. Klucz wylaczony, a rece jeszcze
        /// na zamowieniu - okreg przeliczany w najblizszym StartCalibration.Daily (flaga OrdRedo; Res zostaje - czytajacy Res nie widza okregu bez rachunku), rece wracaja
        /// na pole bez czekania 7 dob. `made` / `carry` na towar (OrdNames).</summary>
        internal static void OrderDayOpen(double[] made, double[] carry)
        {
            if (_state != StateApplied) return;
            bool on = OrderOn && KeyOn;
            foreach (var ld in _live.Values)
            {
                try
                {
                    if (ld == null || ld.TF || ld.OrdCraft < 0) continue;
                    if (!on) { if (ld.OrdH > 0) ld.OrdRedo = true; ld.OrdLeft = 0; continue; }
                    int m = OrdSlot(ld.OrdCraft);
                    if (m < 0) continue;
                    if (carry != null) carry[m] += ld.OrdLeft;   // poprawka 363 (recenzja 2): ulamek z wczoraj liczony takze przy Res == null (OrderDayClose go liczy)
                    if (ld.Res == null) continue;
                    double x = ld.Res.OrdPcs;
                    if (x > 0 && !double.IsNaN(x) && !double.IsInfinity(x)) { ld.OrdLeft += x; if (made != null) made[m] += x; }
                }
                catch (Exception e) { Stumble("OrderDayOpen", e); }
            }
        }

        /// <summary>Koniec doby zamowien: sztuki niesprzedane (cale) przepadaja - rece bez roboty, ulamek na jutro; EMA popytu z popytu doby. Liczby na towar (OrdNames).</summary>
        internal static void OrderDayClose(double[] idle, double[] carry)
        {
            if (_state != StateApplied) return;
            foreach (var ld in _live.Values)
            {
                try
                {
                    if (ld == null || ld.TF || ld.OrdCraft < 0) continue;
                    int m = OrdSlot(ld.OrdCraft);
                    double lost = Math.Floor(Math.Max(0.0, ld.OrdLeft));
                    ld.OrdLeft = Math.Max(0.0, ld.OrdLeft - lost);
                    if (m >= 0) { if (idle != null) idle[m] += lost; if (carry != null) carry[m] += ld.OrdLeft; }
                    double t = ld.OrdToday; ld.OrdToday = 0;
                    if (double.IsNaN(t) || double.IsInfinity(t) || t < 0) t = 0;
                    ld.OrdDemand += OrdEmaA * (t - ld.OrdDemand);
                    if (ld.OrdDemand < 1e-6) ld.OrdDemand = 0;
                }
                catch (Exception e) { Stumble("OrderDayClose", e); }
            }
        }

        /// <summary>Liczby linii 363 (z ostatniego rachunku okregow): wytworcy na towar (OrdNames), z rekami, rece na zamowienie, rece pola tych okregow, glodni,
        /// zboze z pola mniej (kg/d przed zima i po zimie).</summary>
        internal static void OrderStats(int[] byCraft, out int withHands, out double hands, out double field, out int hungry, out double grain0, out double grain1)
        {
            withHands = 0; hands = 0; field = 0; hungry = 0; grain0 = 0; grain1 = 0;
            if (_state != StateApplied) return;
            foreach (var ld in _live.Values)
            {
                if (ld == null || ld.TF || ld.OrdCraft < 0) continue;
                int m = OrdSlot(ld.OrdCraft);
                if (m >= 0 && byCraft != null && m < byCraft.Length) byCraft[m]++;
                if (ld.OrdHungry) hungry++;
                if (ld.OrdH > 0) { withHands++; hands += ld.OrdH; field += ld.OrdFieldH; grain0 += ld.OrdGrain0; grain1 += ld.OrdGrain1; }
            }
        }
    }
}
