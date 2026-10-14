using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// W7 (decyzja Jeffa 13.10 "tak, stawka wojenna podatku pana") - STRONA LUDU: odczyt nadwyzki kiesy ludu osady, ktora zostala PO danine P1 tej doby, i zdjecie
    /// z niej poboru pana (KingdomTreasury.LordLevy.cs). Regula i rachunek - tam.
    ///  - Miara nadwyzki: ta sama co danina P1 (TownLive / CastleLiveDay krok 5): kiesa klasy ponad zapas dni (W TownFolkReserveDays / R 21 / P 30 / podzamcze 10)
    ///    PO daninie (f.DOvC - liczone w kroku 5 tej doby, z gotowka P1 juz zdjeta) - P1 i W7 nigdy nie biora tej samej zlotowki: P1 bierze w dobie ludu, W7
    ///    pozniej tej samej doby i najwyzej z tego, co P1 zostawil.
    ///  - Drugie ograniczenie (stan kiesy TERAZ): kiesa minus zapas dni z kotwicy na jutro (ta sama, od ktorej jutro liczy P1) minus oferta nadwyzki na jutro
    ///    (f.Offer - lud wyda ja jutro na towar) - W7 nie rusza ani zapasu, ani tego, co lud juz postanowil wydac.
    ///  - Tylko kiesa czynna (Live; zamki - CastleLive) i tylko gdy doba ludu tej osady biegla dzis (ADay) - inaczej 0 (bez poboru).
    ///  - Zdjecie: kiesa klasy -> wolajacy (ten sam zloty przelew do glowy rodu u wolajacego), ksiega kies: _ctlOut (kontrola zamknieta kies "wydatki" - r = 0
    ///    jutro, bo pobor biegnie po migawce doby ludu). Bez nowych pol zapisu.
    /// </summary>
    internal static partial class TownFolk
    {
        /// <summary>W7: kiesa ludu czynna (pobor pana z oszczednosci ma z czego brac i ma miare zapasu).</summary>
        internal static bool LevyOn { get { return Live; } }

        /// <summary>
        /// W7: nadwyzka kiesy ludu osady (miasto: W, R, P; zamek: podzamcze w [0]) dostepna dla poboru pana TERAZ - najwyzej to, co zostalo ponad zapas dni po
        /// daninie P1 tej doby, i najwyzej kiesa minus zapas dni na jutro minus oferta nadwyzki na jutro. room - tablica 3 (wypelniana; 0 bez nadwyzki).
        /// false: kiesa nieczynna, osada bez liczb dzisiejszej doby ludu, wies albo zamek przy wylaczonym zaworze zamkow. Tylko odczyt.
        /// </summary>
        internal static bool LevyRoom(Settlement st, double[] room)
        {
            if (room == null || room.Length < 3) return false;
            room[0] = room[1] = room[2] = 0;
            try
            {
                if (st == null || !Live) return false;
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null || f.Kind < 0 || f.Kind > 1) return false;
                if (f.Kind == 1 && !CastleLive) return false;
                if (_liveDay < 0 || f.ADay != _liveDay || (int)CampaignTime.Now.ToDays - 1 != _liveDay) return false;   // doba ludu tej osady biegla dzis
                var s = Settings.Current;
                double rdW = Math.Max(1, Math.Min(60, s != null ? s.TownFolkReserveDays : 10));
                int n = f.Kind == 1 ? 1 : 3;
                for (int c = 0; c < n; c++)
                {
                    double rd = f.Kind == 1 ? RdC : (c == 0 ? rdW : c == 1 ? RdR : RdP);
                    double now = PurseOf(f, c) - rd * Math.Max(0, f.Need[c]) - Math.Max(0, f.Offer[c]);
                    double r = Math.Min(Math.Max(0, f.DOvC[c]), now);
                    room[c] = r > 0 ? Math.Floor(r) : 0;
                }
                return true;
            }
            catch (Exception e) { Stumble("LevyRoom", e); room[0] = room[1] = room[2] = 0; return false; }
        }

        /// <summary>W7: nadwyzka klasy po daninie P1 z kroku 5 tej doby (kontrola "P1 i W7 bez podwojnego poboru") - -1 bez liczb dzisiejszej doby. Tylko odczyt.</summary>
        internal static double LevyAfterAid(Settlement st, int c)
        {
            try
            {
                Folk f;
                if (st == null || c < 0 || c > 2 || !_f.TryGetValue(st, out f) || f == null || f.ADay != _liveDay) return -1;
                return Math.Max(0, f.DOvC[c]);
            }
            catch { return -1; }
        }

        /// <summary>W7: kiesa klasy ponizej zapasu dni na jutro (kontrola po poborze; oczekiwane false). Tylko odczyt.</summary>
        internal static bool LevyBelowReserve(Settlement st, int c)
        {
            try
            {
                Folk f;
                if (st == null || c < 0 || c > 2 || !_f.TryGetValue(st, out f) || f == null || f.Kind > 1) return false;
                var s = Settings.Current;
                double rdW = Math.Max(1, Math.Min(60, s != null ? s.TownFolkReserveDays : 10));
                double rd = f.Kind == 1 ? RdC : (c == 0 ? rdW : c == 1 ? RdR : RdP);
                return PurseOf(f, c) + 0.5 < rd * Math.Max(0, f.Need[c]);
            }
            catch { return false; }
        }

        /// <summary>W7: zdejmuje z kiesy klasy c osady st najwyzej `amount` (najwyzej stan kiesy) - zwraca zdjete; wolajacy przelewa DOKLADNIE tyle glowie rodu.
        /// Ksiega kies: _ctlOut (wydatek kies - kontrola zamknieta). Nie rzuca.</summary>
        internal static long LevyTake(Settlement st, int c, long amount)
        {
            try
            {
                Folk f;
                if (st == null || amount <= 0 || c < 0 || c > 2 || !_f.TryGetValue(st, out f) || f == null || f.Kind > 1) return 0;
                long x = Math.Min(amount, Math.Max(0, PurseOf(f, c)));
                if (x <= 0) return 0;
                AddPurse(f, c, -x);
                _ctlOut += x;
                return x;
            }
            catch (Exception e) { Stumble("LevyTake", e); return 0; }
        }

        /// <summary>W7: zwrot zdjetego do kiesy klasy (wyjatek przy przelewie do glowy - zloto wraca tam, skad wyszlo; ksiega kies: _ctlOut z powrotem). Nie rzuca.</summary>
        internal static void LevyBack(Settlement st, int c, long amount)
        {
            try
            {
                Folk f;
                if (st == null || amount <= 0 || c < 0 || c > 2 || !_f.TryGetValue(st, out f) || f == null || f.Kind > 1) return;
                AddPurse(f, c, amount);
                _ctlOut -= amount;
            }
            catch (Exception e) { Stumble("LevyBack", e); }
        }
    }
}
