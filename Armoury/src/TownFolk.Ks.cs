using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// KS (kalibracja startu, StartCalibration.Live.cs): PLON POL MIESZCZAN - kasa miasta (kupcy) placi ludowi miasta za towar z jego pol (chlopi miasta, niewolni
    /// latyfundiow - ich czesc do wlascicieli R40/P60 jak kazda praca niewolnych, wyrobnicy przy zniwach). Ta sama droga co place z kasy (FromCoffer): kasa placi najwyzej
    /// tyle, ile ma; zloto nie powstaje (kasa -> kiesy ludu). Wolac tylko z bloku doby (MoneyLedger: pozycja MKs).
    /// </summary>
    internal static partial class TownFolk
    {
        private static long _ksPaid; private static int _ksPayN;

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
    }
}
