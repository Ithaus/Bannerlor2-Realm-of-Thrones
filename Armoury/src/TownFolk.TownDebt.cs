using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m96-dlugi (Jeff 20.10 "dlugi miast tak, robimy") - kiesy mieszczan miasta jako wierzyciele i platnicy rady miasta (TownDebt.cs). Te same kiesy W / R / P i ta sama
    /// miara "ponad zapas" co pozyczka miejska f8 (TlPlaceFor): klasa pozycza najwyzej polowe tego, co ma ponad Town Loan Folk Reserve Times x zapas dni (W 10 / R 21 /
    /// P 30 dni potrzeby), liczona od stanu sprzed pozyczek doby - wspolny pulap dzienny z f8 (_tlTaken, TlTakenOf), wiec rada i panowie nie zjadaja razem kiesy do zapasu.
    /// Podatek nadzwyczajny bierze z tego, co klasa ma ponad 1 x zapas dni (nie ponizej - lud nie glodzi sie na dlug rady). Ksiega kies: _ctlOut / _ctlIn i LDayOut / LDayIn
    /// (kontrola zamknieta kies). Odsetki wplacone klasie sa jej zarobkiem okna (EarnWin - jak f8, recenzja f8 uwaga 3). Miasto bez czynnej kiesy - 0 (wolajacy omija).
    /// </summary>
    internal static partial class TownFolk
    {
        private static Folk TdFolk(Settlement st)
        {
            Folk f;
            if (st == null || !st.IsTown || !Live || !_f.TryGetValue(st, out f) || f == null || f.Kind != 0) return null;
            return f;
        }

        private static double TdReserveDays(Settings s, int cls)
        {
            return cls == 0 ? Math.Max(1, Math.Min(60, s.TownFolkReserveDays)) : cls == 1 ? RdR : RdP;
        }

        /// <summary>Ile klasa cls (0 W, 1 R, 2 P) miasta st pozyczy dzis radzie (polowa nadwyzki ponad zapas, minus juz pozyczone dzis - f8 i rada razem). 0 - nic.</summary>
        internal static double TdLendOffer(Settlement st, int cls)
        {
            try
            {
                var s = Settings.Current;
                var f = TdFolk(st);
                if (f == null || s == null || cls < 0 || cls > 2) return 0;
                double need = f.Need[cls];
                if (!(need > 0)) return 0;
                var tk = TlTakenOf(st, false);
                double ex = TlOffer(PurseOf(f, cls), TlFolkTimes(s) * TdReserveDays(s, cls) * need, tk != null ? tk[cls] : 0);
                return ex >= 1 ? ex : 0;
            }
            catch (Exception e) { Stumble("TdLendOffer", e); return 0; }
        }

        /// <summary>Ile klasa cls ma ponad 1 x zapas dni (podstawa podatku nadzwyczajnego rady). 0 - nic.</summary>
        internal static long TdLevyBase(Settlement st, int cls)
        {
            try
            {
                var s = Settings.Current;
                var f = TdFolk(st);
                if (f == null || s == null || cls < 0 || cls > 2) return 0;
                double need = f.Need[cls];
                if (!(need > 0)) return 0;
                double v = PurseOf(f, cls) - TdReserveDays(s, cls) * need;
                return v >= 1 ? (long)Math.Floor(v) : 0;
            }
            catch (Exception e) { Stumble("TdLevyBase", e); return 0; }
        }

        /// <summary>Zdjecie z kiesy klasy (pozyczka - loan true: do pulapu dziennego f8; podatek - false). Wolajacy wplaca zdjete do kasy miasta. Zwraca zdjete.</summary>
        internal static long TdTake(Settlement st, int cls, long x, bool loan)
        {
            try
            {
                var f = TdFolk(st);
                if (f == null || x <= 0 || cls < 0 || cls > 2) return 0;
                long t = Math.Min(x, Math.Max(0, PurseOf(f, cls)));
                if (t <= 0) return 0;
                AddPurse(f, cls, -t); _ctlOut += t; f.LDayOut += t;
                if (loan) { var tk = TlTakenOf(st, true); if (tk != null) tk[cls] += t; }
                return t;
            }
            catch (Exception e) { Stumble("TdTake", e); return 0; }
        }

        /// <summary>Wplata do kiesy klasy (splata rady albo zwrot po wyjatku). interest - czesc bedaca odsetkami (zarobek okna klasy). Kiesa nieczynna - 0 (wolajacy
        /// zostawia zloto w kasie). Zwraca wplacone.</summary>
        internal static long TdGive(Settlement st, int cls, long x, long interest)
        {
            try
            {
                var f = TdFolk(st);
                if (f == null || x <= 0 || cls < 0 || cls > 2) return 0;
                AddPurse(f, cls, x); _ctlIn += x; f.LDayIn += x;
                if (interest > 0) f.EarnWin[cls] += Math.Min(x, interest);
                return x;
            }
            catch (Exception e) { Stumble("TdGive", e); return 0; }
        }

        /// <summary>Zwrot zdjetego po wyjatku (bez zarobku okna; pulap dzienny f8 cofniety dla pozyczki).</summary>
        internal static void TdUndo(Settlement st, int cls, long x, bool loan)
        {
            try
            {
                var f = TdFolk(st);
                if (f == null || x <= 0 || cls < 0 || cls > 2) return;
                AddPurse(f, cls, x); _ctlOut -= x; f.LDayOut -= x;
                if (loan) { var tk = TlTakenOf(st, false); if (tk != null) tk[cls] = Math.Max(0, tk[cls] - x); }
            }
            catch (Exception e) { Stumble("TdUndo", e); }
        }
    }
}
