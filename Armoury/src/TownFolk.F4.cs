using System;

namespace Armoury
{
    /// <summary>
    /// f4-place-ludu (noc 17/18.10, KsFolkWage.cs) - pomocnicze drogi kiesy ludu dla plac wyrobnikow W wedlug koszyka:
    ///  - FromCofferWR / CreditWR: kasa miasta -> kiesy W i R w JAWNYCH kwotach klasy (czesc W po czynniku plac W, czesc R bez zmiany) - ta sama ksiega co FromCoffer /
    ///    Credit (zarobek okna klas, EarnK rodzaju, praca niewolnych z czesci W do wlascicieli R40/P60, _ctlIn); wolane TYLKO, gdy czynnik plac W &lt; 1 (inaczej stara droga
    ///    FromCoffer co do monety);
    ///  - F4HoardRate: stawka doby danin od skarbca klasy W (Ds Hoard Due) w miescie z regula Ks Labour To Basket - ten sam wzor rampy co DaRateNow, ze stawka
    ///    KsFolkWage.HoardRate zamiast Ds Hoard Due Rate (recenzja 18.10: od Ds Hoard Due Rate przy kappa 1 do min(Ds Hoard Due Rate, Ks Labour Basket Hoard Rate) przy kappa
    ///    najnizej, liniowo z kappa miasta); inne klasy, zamki, wsie i miasta bez reguly - stawka podana (jak dotad).
    /// Nie rzucaja (czysta arytmetyka i przelew kasy jak FromCoffer).
    /// </summary>
    internal static partial class TownFolk
    {
        /// <summary>Zapis zarobku W i R w jawnych kwotach (czesc W przed praca niewolnych). Suma przyjeta = aW + aR (nic z niczego, nic w nicosc).</summary>
        private static void CreditWR(Folk f, long aW, long aR, int kind)
        {
            aW = Math.Max(0, aW); aR = Math.Max(0, aR);
            long amt = aW + aR;
            if (amt <= 0) return;
            f.LDayIn += amt;
            if (kind >= 0 && kind < EKinds) f.EarnK[kind] += amt;
            if (f.Kind == 1) { f.KW += amt; f.EarnWin[0] += amt; return; }
            long toW = aW, toR = aR, toP = 0;
            if (f.Slave && f.HS + f.HW > 0 && toW > 0)
            {
                double y = f.HS / (f.HS + f.HW);
                long ys = Math.Min(toW, (long)(toW * y));
                long yr = (long)(ys * OwnR), yp = ys - yr;
                toW -= ys; toR += yr; toP += yp;
                _win.SlaveW += ys;
            }
            f.KW += toW; f.KR += toR; f.KP += toP;
            f.EarnWin[0] += toW; f.EarnWin[1] += toR; f.EarnWin[2] += toP;
        }

        /// <summary>Kasa osady -> kiesy W i R w jawnych kwotach (przelew w calosci tutaj, najwyzej stan kasy; przy przycieciu W i R w tej samej proporcji). Zwraca to,
        /// co naprawde zeszlo z kasy; `gotW` - czesc W tego, co zeszlo (recenzja 18.10: tozsamosc linii f4 liczy zaplacone z przelewu, nie ze zlecenia).</summary>
        private static long FromCofferWR(Folk f, long xW, long xR, int kind, out long gotW)
        {
            gotW = 0;
            xW = Math.Max(0, xW); xR = Math.Max(0, xR);
            long x = xW + xR;
            if (x <= 0 || f == null || f.St == null || f.St.Town == null) return 0;
            var town = f.St.Town;
            int ask = (int)Math.Min(int.MaxValue, Math.Min(x, Math.Max(0, town.Gold)));
            if (ask <= 0) return 0;
            int had = town.Gold;
            town.ChangeGold(-ask);
            long got = had - town.Gold;
            if (got <= 0) return 0;
            long gW = got >= x ? xW : (long)Math.Floor(got * (double)xW / x);
            CreditWR(f, gW, got - gW, kind);
            _ctlIn += got;
            gotW = gW;
            return got;
        }

        /// <summary>f4-place-ludu (c): stawka doby danin od skarbca klasy `c` osady `f` przy firmach DS - klasa W miasta z regula plac wedlug koszyka: rampa jak DaRateNow
        /// z nizsza stawka docelowa (KsFolkWage.HoardRate - schodzi z Ds Hoard Due Rate razem z kappa miasta, recenzja 18.10); inaczej `rate` bez zmiany.</summary>
        private static double F4HoardRate(Folk f, int c, Settings s, double rate)
        {
            try
            {
                if (f == null || f.Kind != 0 || s == null) return rate;
                // f5-lud (Ks Folk Income Hoard Falls): klasy R (c 1) i P (c 2) miasta z regula - stawka schodzi razem z kappa klasy (ten sam wzor rampy); klucz wylaczony
                // albo miasto bez reguly - HoardRate -1 i stawka bez zmiany (jak dotad); W (c 0) - f4 jak dotad
                double nr = c == 0 ? KsFolkWage.HoardRate(f.St) : (c == 1 || c == 2) ? KsFolkIncome.HoardRate(f.St, c) : -1.0;
                if (nr < 0) return rate;
                double r = DsTowns.NewShare;
                return (1 - r) * HoardRateNow(s) + Math.Min(r, DaKeyShare(s)) * nr;
            }
            catch (Exception e) { Stumble("F4HoardRate", e); return rate; }
        }
    }
}
