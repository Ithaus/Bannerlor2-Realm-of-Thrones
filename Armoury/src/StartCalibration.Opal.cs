using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// f4-opal (noc 17/18.10, fala 4; opis regul - TownFolk.Opal.cs): odczyty KS dla opalu mieszczan. Tylko odczyt opublikowanej pamieci (mapa koszyka ZC4, stawki wsi,
    /// okregi) - nic tu nie zmienia produkcji, cen ani towaru. Popyt ceny drewna (Zc4Demand) bierze opal po regule paczki przez FireForPrice.
    /// </summary>
    internal static partial class StartCalibration
    {
        /// <summary>f4-opal krok 2: lambda miasta i opal koszyka (ladunki/d przy pelnej lambdzie, bez pory roku) - miasto w mapie koszyka ZC4 (stan zastosowany, lambda > 0);
        /// false - poza ZC4 (wolajacy zostawia potrzebe jak dzis - tak samo jak Zc4Firewood).</summary>
        internal static bool Zc4FireOf(Town town, out double lam, out double fire)
        {
            lam = 0; fire = 0;
            ZTown zt;
            if (!ZTownOf(town, out zt)) return false;
            lam = zt.Lam; fire = Math.Max(0.0, zt.Fire);
            return !(double.IsNaN(lam) || double.IsNaN(fire) || double.IsInfinity(fire));
        }

        /// <summary>f4-opal krok 3: miasto z koszykiem ZC4 (KS zastosowany w tym miescie, lambda > 0) - tylko tam zapas rzemiosla liczy drewno dymarek wsi.</summary>
        internal static bool Zc4FireTown(Town town)
        {
            ZTown zt;
            return ZTownOf(town, out zt);
        }

        /// <summary>f4-opal krok 2 (zapas, dopoki pierscien zmierzonego dowozu nie ma 7 pelnych dob): drewno na targ miasta wedlug Planu, ladunki/d - stawki KS wsi
        /// (przeplyw na targ okregu = produkcja - zjedzone w okregu) z tym miastem targowym (TradeBound, inaczej Bound - jak koszyk wsi ZC4) i pola mieszczan, kazde
        /// x beta rampy podazy (to samo, co DeliverTownFields i BlendStep dokladaja do stawki). Pusty slownik - poza stanem zastosowanym albo przed pierwsza doba KS.</summary>
        internal static Dictionary<Settlement, double> OpalPlanWood()
        {
            var res = new Dictionary<Settlement, double>();
            if (_state != StateApplied || !KeyOn) return res;
            const int iw = KsDeliver.IWood;
            var rates = _rates;
            foreach (var kv in rates)
            {
                var v = kv.Key; var r = kv.Value;
                if (v == null || r == null || r.A == null || iw >= r.A.Length) continue;
                var mk = v.TradeBound ?? v.Bound;
                if (mk == null || mk.Town == null || !mk.Town.IsTown) continue;
                double x = r.A[iw] * Beta(r.M, iw);
                if (!(x > 0) || double.IsInfinity(x)) continue;
                double o; res.TryGetValue(mk, out o); res[mk] = o + x;
            }
            foreach (var ld in _live.Values)
            {
                if (ld == null || !ld.TF || ld.T == null || ld.T.Settlement == null || ld.Res == null) continue;
                double x = ld.Res.Market[iw] * Beta(ld.Market == 0 ? 0 : 1, iw);
                if (!(x > 0) || double.IsInfinity(x)) continue;
                var st = ld.T.Settlement;
                double o; res.TryGetValue(st, out o); res[st] = o + x;
            }
            return res;
        }

        /// <summary>Zc4Demand (popyt ceny, moze isc z watkow AI): opal miasta w popycie ceny drewna (ladunki/d przy pelnej lambdzie) - przy kluczu Firewood Ks Price Demand
        /// opal po regule paczki z ostatniej doby opalu (pora roku i dowoz), inaczej (albo bez wpisu) pelny opal koszyka jak dotad.</summary>
        private static double FireForPrice(ZTown zt)
        {
            var s = Settings.Current;
            if (s == null || !s.FirewoodKsPriceDemand || zt == null || zt.St == null) return zt != null ? zt.Fire : 0.0;
            double eff;
            return TownFolk.FwEffOf(zt.St, out eff) ? eff : zt.Fire;
        }
    }
}
