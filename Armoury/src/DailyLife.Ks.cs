using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// KS (kalibracja startu, StartCalibration*.cs) - ODCZYTY Z ZYCIA CODZIENNEGO: stany osad z ksiegi stanow tej doby (ZC0/ZC2), koszyki stanow w kg z poprawka KS-K
    /// (projekt KS rozdz. 4.6: oliwa x4 na poludniu, daktyle chlebem oaz; zboze dopelnia kcal stanu jak w BasketOf), udzial z targu stanu wsi (Mk) i sigma kampanii.
    /// Tylko odczyt - nic w ZC nie zmienia (koszyk KS-K w osobnej pamieci, BasketOf bez zmian).
    /// </summary>
    internal static partial class DailyLife
    {
        /// <summary>KS: osada z ksiegi stanow doby - rodzaj, kultura z kalibracji (kraina), ludzie ksiegi w domu, udzialy 6 stanow, region i miasto targowe.</summary>
        internal sealed class KsRec
        {
            public Settlement St; public bool Town, Castle; public string Cul, ProfId; public double L, Away;
            public readonly double[] Share = new double[6]; public readonly double[] N = new double[6];
            public Settlement Region, Market;
            public double Guests;     // sw-uchodzcy (recenzja): miasto - uchodzcy z najazdu dopisani do biedoty (ZC4 przelicza potrzebe miasta po ich zmianie); wylaczone - 0
        }

        /// <summary>KS: stany wszystkich miast i wsi z ostatniej doby pomiaru ZC (dzien w day). false - ZC nie liczylo (klucz wylaczony, log wylaczony bez ZC2,
        /// przed kalibracja ksiegi ludzi) albo stany starsze niz 1 doba. Nie rzuca.</summary>
        internal static bool KsSnapshot(List<KsRec> into, int today, out int day)
        {
            day = _eDay;
            into.Clear();
            try
            {
                if (_recs == null || _eDay < 0 || today - _eDay > 2) return false;
                foreach (var r in _recs)
                {
                    if (r == null || r.St == null) continue;
                    var k = new KsRec { St = r.St, Town = r.Kind == KTown, Castle = r.Kind == KCastle, Cul = r.Cul, ProfId = Profs[r.Prof].Id, L = r.L, Away = r.Away };
                    for (int e = 0; e < 6; e++) { k.Share[e] = r.Share[e]; k.N[e] = r.N[e]; }
                    // sw-uchodzcy (RaidRefugeesToTowns): uchodzcy z najazdu w miescie - biedota miasta w stanach KS (potrzeba targu, koszyk klasy W, rece pol mieszczan);
                    // okreg wsi traci ich w potrzebie jedzenia (RaidRefugees.AwayInRegion w Planie i stawkach). Wylaczone: Guests = 0
                    if (r.Kind == KTown && r.Guests > 0 && Fin(r.Guests)) { k.N[ELabour] += r.Guests; k.L += r.Guests; k.Guests = r.Guests; }
                    k.Region = r.Region != null ? r.Region.St : null; k.Market = r.Market != null ? r.Market.St : null;
                    into.Add(k);
                }
                return into.Count > 0;
            }
            catch (Exception e) { Stumble("KsSnapshot", e); into.Clear(); return false; }
        }

        /// <summary>KS: sigma kampanii (z zapisu ZC0) albo z MCM, gdy ZC jeszcze jej nie ustalilo.</summary>
        internal static int KsSigma { get { return _sigma > 0 ? _sigma : SigmaFromMcm(); } }

        /// <summary>KS: udzial grupy koszyka g, ktory stan e WSI je z wlasnego pola i domu (1 - udzial z targu, rozdz. 8.1 ZC).</summary>
        internal static double KsKeepVillage(int e, int g) { return 1.0 - Mk(e, false, g); }

        private static readonly Dictionary<string, KsCore.Basket[]> _ksBasket = new Dictionary<string, KsCore.Basket[]>(StringComparer.Ordinal);

        /// <summary>KS: koszyki 6 stanow ksiegi kultury cul z poprawka KS-K (ksk) - jak basket() modelu v2 (ks_dane.py): mnozniki profilu z gornym limitem,
        /// wino na poludniu, KS-K (oliwa x4 w krainach oliwy, daktyle +0.04 kg w Dorne i na wschodzie), zboze dopelnia kcal stanu. Pamiec na sesje.</summary>
        internal static KsCore.Basket[] KsBasket(string cul, bool ksk)
        {
            string key = (cul ?? "") + (ksk ? "|k" : "|-");
            KsCore.Basket[] res;
            lock (_ksBasket) { if (_ksBasket.TryGetValue(key, out res)) return res; }
            if (_target == null) BasketOf(cul);                    // _target liczy BasketOf (kcal stanu z Base)
            bool known;
            int pi = ProfOf(cul, out known);
            var pr = Profs[pi];
            double fish;
            if (cul == null || !IsleFish.TryGetValue(cul, out fish)) fish = 1.0;
            bool oil = OilProf(pr.Id), dates = pr.Id == "wschod" || pr.Id == "dorne";
            res = new KsCore.Basket[6];
            for (int e = 0; e < 6; e++)
            {
                var bs = Base[e];
                var o = (double[])bs.Clone();
                for (int i = 0; i < MulGroup.Length; i++)
                {
                    int g = MulGroup[i];
                    double m = pr.Mul[i];
                    if (g == GF) m *= fish;
                    double v = bs[g] * m, cap;
                    if (m > 1.0 && Cap.TryGetValue(g, out cap)) v = Math.Min(v, bs[g] + cap);
                    o[g] = v;
                }
                if (pr.Mul[6] >= 1.5 && pr.Mul[5] < 1.0) o[GWine] += Math.Min(0.25, (bs[GBeer] - o[GBeer]) * 0.6);
                double extra = 0.0;
                if (ksk)
                {
                    if (oil) o[GO] = o[GO] * 4.0;
                    if (dates) { o[GV] = o[GV] + 0.04; extra = 0.04; }
                }
                double rest = 0;
                for (int g = 1; g < Groups; g++) rest += o[g] * Kcal[g];
                o[GG] = Math.Max(0.25, (_target[e] - rest) / Kcal[GG]);
                var b = new KsCore.Basket { DatesExtra = extra };
                Array.Copy(o, b.G, Groups);
                Array.Copy(pr.Vs, b.Vs, 6);
                res[e] = b;
            }
            lock (_ksBasket) { _ksBasket[key] = res; }
            return res;
        }

        /// <summary>KS: kraina pije oliwe (OILPROF modelu: Dorne, poludnie i wnetrze Wolnych Miast, wschod, Reach, Wyspy Letnie).</summary>
        internal static bool OilProf(string profId)
        {
            return profId == "dorne" || profId == "wm_poludnie" || profId == "wschod" || profId == "reach" || profId == "letnie" || profId == "wm_wnetrze";
        }

        /// <summary>KS: kozuchy Polnocy (KS-K) - profil polnoc albo wyspy.</summary>
        internal static bool NorthFur(string profId) { return profId == "polnoc" || profId == "wyspy"; }
    }
}
