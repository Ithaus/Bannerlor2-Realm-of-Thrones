using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// f4-opal (noc 17/18.10, fala 4; opis regul - TownFolk.Opal.cs): odczyty KS dla opalu mieszczan. Tylko odczyt opublikowanej pamieci (mapa koszyka ZC4, stawki wsi,
    /// okregi) - nic tu nie zmienia produkcji, cen ani towaru. Popyt ceny drewna (Zc4Demand) bierze opal x czynnik pory przez FireForPrice.
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

        /// <summary>f4-opal krok 2 (doby, ktorych pierscien zmierzonego dowozu jeszcze nie ma - po wczytaniu, po wlaczeniu klucza): drewno na targ miasta wedlug Planu,
        /// ladunki/d. Recenzja (podaz Planu bez lasu wsi, z drewnem dymarek, bez stanu wsi): (a) stawki KS wsi (przeplyw na targ okregu = produkcja - zjedzone w okregu)
        /// x beta rampy - tylko wies Normal z miastem handlowym (TradeBound), bo ProdStep daje 0 wsi najezdzanej, spladrowanej i bez targu; (b) las wsi
        /// (VillageWoodlot.KsPiecesPerDay - podaz spoza tabeli, jak w Planie KS; sama sprawdza wies Normal i las czynny) do TradeBound, inaczej Bound, minus to, co z lasu
        /// zdejmuja dymarki kopaln okregu, zanim woz pojedzie (Bloomery.FromWoodlot: najwyzej suma potrzeb doby kopaln tego miasta handlowego - Bloomery.WoodlotWant);
        /// (c) pola mieszczan x beta (to samo, co DeliverTownFields). Czesc gry w rampie (1 - beta) poza Planem (rampa trwa krotko, pierscien ja zmierzy).
        /// Pusty slownik - poza stanem zastosowanym albo przed pierwsza doba KS. Raz na dobe opalu (watek gry).</summary>
        internal static Dictionary<Settlement, double> OpalPlanWood()
        {
            var res = new Dictionary<Settlement, double>();
            if (_state != StateApplied || !KeyOn) return res;
            const int iw = KsDeliver.IWood;
            var rates = _rates;
            var lot = new Dictionary<Settlement, double>();   // las wsi wedlug miasta (przed dymarkami)
            foreach (var kv in rates)
            {
                var v = kv.Key; var r = kv.Value;
                if (v == null || r == null) continue;
                var tb = v.TradeBound;
                if (r.A != null && iw < r.A.Length && tb != null && tb.Town != null && tb.Town.IsTown && v.VillageState == Village.VillageStates.Normal)
                {
                    double x = r.A[iw] * Beta(r.M, iw);
                    if (x > 0 && !double.IsInfinity(x)) { double o; res.TryGetValue(tb, out o); res[tb] = o + x; }
                }
                var mk = tb ?? v.Bound;
                if (mk == null || mk.Town == null || !mk.Town.IsTown) continue;
                double w = VillageWoodlot.KsPiecesPerDay(v);
                if (w > 0 && !double.IsInfinity(w)) { double o; lot.TryGetValue(mk, out o); lot[mk] = o + w; }
            }
            foreach (var kv in lot)
            {
                double take = 0;
                try { take = Math.Min(kv.Value, Math.Max(0f, Bloomery.WoodlotWant(kv.Key))); } catch { take = 0; }
                double x = kv.Value - take;
                if (!(x > 0)) continue;
                double o; res.TryGetValue(kv.Key, out o); res[kv.Key] = o + x;
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
        /// opal x czynnik pory z ostatniej doby opalu (BEZ limitu dowozu - recenzja: popyt ceny rowny dowozowi kasowalby sygnal niedoboru), inaczej (albo bez wpisu)
        /// pelny opal koszyka jak dotad.</summary>
        private static double FireForPrice(ZTown zt)
        {
            var s = Settings.Current;
            if (s == null || !s.FirewoodKsPriceDemand || zt == null || zt.St == null) return zt != null ? zt.Fire : 0.0;
            double eff;
            return TownFolk.FwEffOf(zt.St, out eff) ? eff : zt.Fire;
        }
    }
}
