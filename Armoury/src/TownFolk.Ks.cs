using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KS (kalibracja startu, StartCalibration.Live.cs): PLON POL MIESZCZAN - kasa miasta (kupcy) placi ludowi miasta za towar z jego pol (chlopi miasta, niewolni
    /// latyfundiow - ich czesc do wlascicieli R40/P60 jak kazda praca niewolnych, wyrobnicy przy zniwach). Ta sama droga co place z kasy (FromCoffer): kasa placi najwyzej
    /// tyle, ile ma; zloto nie powstaje (kasa -> kiesy ludu). Wolac tylko z bloku doby (MoneyLedger: pozycja MKs).
    /// KS etap 2 - ZC4 KOSZYK WSI (Zc4BuyVillage, z Villages): wies zastosowana kupuje w miescie targowym to, czego nie zjada z wlasnego pola (StartCalibration.Zc4VillageNeed)
    /// x lambda, po kolei drabiny (zboze, sol, napoj, bialko, reszta jedzenia, przedza, rzeczy), po cenie targu, najwyzej sztuki ponad 5 na polce i najwyzej do kiesy wsi -
    /// pusta kiesa konczy zakupy (dalsze szczeble czekaja). Kiesa wsi -> kasa miasta (jak BuyGoods), towar z polki zuzyty we wsi (ramka ksiegi towarow "zakupy wsi w miastach").
    /// Ulamek sztuki przechodzi na jutro (bez zaleglosci ponad sztuke).
    /// </summary>
    internal static partial class TownFolk
    {
        private static long _ksPaid; private static int _ksPayN;
        private static readonly Dictionary<Village, float[]> _zcCarry = new Dictionary<Village, float[]>();

        /// <summary>KS: kasa miasta st -> kiesy ludu (podzial "inne - wyrobnicy", niewolni do wlascicieli). Zwraca to, co naprawde zeszlo z kasy (0 - kiesa nieczynna,
        /// nie miasto, poza watkiem glownym, kasa pusta). Nie rzuca.</summary>
        internal static long KsFieldPay(Settlement st, long x)
        {
            try
            {
                if (x <= 0 || st == null || !st.IsTown || !Live || OffMain()) return 0;
                EnsureImported();
                var f = Get(st);
                if (f == null || f.Kind != 0) return 0;
                long got = FromCoffer(f, x, -1);
                if (got <= 0) return 0;
                _win.FromTown += got;
                _ksPaid += got; _ksPayN++;
                return got;
            }
            catch (Exception e) { Stumble("KsFieldPay", e); return 0; }
        }

        /// <summary>KS: zaplacone od ostatniego odczytu (zl, przelewy) - do linii KS; zeruje.</summary>
        internal static long KsTakePaid(out int n) { long p = _ksPaid; n = _ksPayN; _ksPaid = 0; _ksPayN = 0; return p; }

        /// <summary>ZC4: koszyk wsi vil (sztuki/d przy pelnej lambdzie, ZIds) x lam kupiony w miescie targowym mk - po kolei drabiny, kiesa wsi -> kasa miasta. Zwraca zloto.</summary>
        private static long Zc4BuyVillage(Village vil, Settlement mk, float[] q, double lam, LDay d)
        {
            var town = mk.Town; var roster = mk.ItemRoster;
            if (town == null || roster == null || q == null) return 0;
            float[] carry;
            if (!_zcCarry.TryGetValue(vil, out carry) || carry == null || carry.Length != StartCalibration.ZN) { carry = new float[StartCalibration.ZN]; _zcCarry[vil] = carry; }
            long spent = 0, pcs = 0; double want = 0, got = 0; int noGold = 0, noGoods = 0;
            var rw = new double[3]; var rg = new double[3];
            bool broke = false;
            foreach (int z in StartCalibration.ZLadder)
            {
                double w = q[z] * lam + carry[z];
                int n = (int)Math.Floor(w);
                carry[z] = (float)Math.Max(0.0, Math.Min(1.0, w - n));
                if (n <= 0) continue;
                int rung = z == StartCalibration.ZGrain || z == StartCalibration.ZSalt ? 0 : (StartCalibration.ZFood(z) ? 1 : 2);
                want += n; rw[rung] += n;
                if (broke) continue;
                var it = StartCalibration.ZItem(z);
                if (it == null) { noGoods++; continue; }
                var e = new EquipmentElement(it, null, null, false);
                int ix = roster.FindIndexOfElement(e);
                if (ix < 0) { noGoods++; continue; }
                int have = roster.GetElementNumber(ix) - ShelfKeep;
                if (have <= 0) { noGoods++; continue; }
                int price = town.MarketData.GetPrice(e);
                if (price <= 0) continue;
                int k = Math.Min(n, have);
                int had = vil.Gold;
                if ((long)k * price > had) { k = had / price; if (k <= 0) { noGold++; broke = true; continue; } }   // pusta kiesa - dalsze szczeble czekaja
                vil.ChangeGold(-k * price);
                int paid = had - vil.Gold;
                if (paid <= 0) { noGold++; broke = true; continue; }
                town.ChangeGold(paid);
                roster.AddToCounts(e, -k);
                spent += paid; pcs += k; got += k; rg[rung] += k;
                d.VBuy += paid; d.VPieces += k;
                NoteVillBought(mk, it.ItemCategory, paid);
            }
            StartCalibration.Zc4NoteVillage(spent, pcs, want, got, noGold, noGoods, rw, rg);
            return spent;
        }
    }
}
