using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// DS F3 (DsTowns.cs) - wejscia kiesy ludu dla miast i zamkow przy ustalonych firmach:
    ///  - M6 WYPLATA KUPCOM (EarnDraw): kasa miasta -> kiesy P (udzial pShare) i R jako zarobek okna (podatek pana i 184 nastepnej doby) - zamiast "5% zaworu dla ludu";
    ///    w ksiedze kies jak czesc ludu zaworu (_win.ValveT, EValve). Zamek - EarnCastleValve (kiesa podzamcza, jak 15% zaworu zamku).
    ///  - ZAJECIE (DistrainCommune): zaleglosc firmy miasta z kiesy gminy - najwyzej 2/3 jej stanu w dzien raty; DistrainUndo oddaje zdjete po wyjatku przelewu.
    /// Ksiega kies: _ctlIn / _ctlOut i LDayIn / LDayOut (kontrola zamknieta kiesa-ludu.csv). Nie rzucaja.
    /// </summary>
    internal static partial class TownFolk
    {
        /// <summary>M6: kasa miasta -> kiesy P i R (zarobek okna). Zwraca to, co zeszlo z kasy (0 - kiesa nieczynna, poza watkiem, brak ludu).</summary>
        internal static long EarnDraw(Settlement st, long x, double pShare)
        {
            try
            {
                if (x <= 0 || st == null || !st.IsTown || st.Town == null || !Live || OffMain()) return 0;
                var f = Get(st);
                if (f == null || f.Kind != 0) return 0;
                var town = st.Town;
                int ask = (int)Math.Min(int.MaxValue, Math.Min(x, Math.Max(0, town.Gold)));
                if (ask <= 0) return 0;
                int had = town.Gold;
                town.ChangeGold(-ask);
                long got = had - town.Gold;
                if (got <= 0) return 0;
                double ps = double.IsNaN(pShare) ? 0.7 : Math.Max(0, Math.Min(1, pShare));
                long p = Math.Min(got, (long)Math.Round(got * ps)), r = got - p;
                f.KR += r; f.KP += p; f.EarnWin[1] += r; f.EarnWin[2] += p;
                f.LDayIn += got; f.EarnK[EValve] += got;
                _ctlIn += got; _win.ValveT += got; _win.Earn[EValve] += got;
                return got;
            }
            catch (Exception e) { Stumble("EarnDraw", e); return 0; }
        }

        /// <summary>Zajecie zaleglosci firmy z kiesy gminy miasta: najwyzej `want` i 2/3 stanu kiesy gminy. Zwraca zdjete (wolajacy przelewa panu i koronie).</summary>
        internal static long DistrainCommune(Settlement st, long want)
        {
            try
            {
                if (want <= 0 || st == null || !st.IsTown || !Live || OffMain()) return 0;
                var f = Get(st);
                if (f == null || f.Kind != 0) return 0;
                long cap = (long)Math.Floor(Math.Max(0, f.KG) * 2.0 / 3.0);
                long x = Math.Min(want, cap);
                if (x > int.MaxValue / 2) x = int.MaxValue / 2;
                if (x <= 0) return 0;
                f.KG -= x; _ctlOut += x; f.LDayOut += x;
                return x;
            }
            catch (Exception e) { Stumble("DistrainCommune", e); return 0; }
        }

        /// <summary>Zwrot zdjetego z kiesy gminy (wyjatek przy przelewie - nic w nicosc).</summary>
        internal static void DistrainUndo(Settlement st, long x)
        {
            try
            {
                if (x <= 0 || st == null) return;
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null) return;
                f.KG += x; _ctlOut -= x; f.LDayOut -= x;
            }
            catch (Exception e) { Stumble("DistrainUndo", e); }
        }
    }
}
