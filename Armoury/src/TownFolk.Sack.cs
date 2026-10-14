using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// LUP ZDOBYTEGO MIASTA (docs/lup-miast/LUP-ZDOBYTEGO-MIASTA-2026-10-13.md, decyzje Jeffa 13.10 pkt 1-5) - STRONA LUDU: odczyt i zdjecie czesci
    /// pieniedzy mieszczan zdobytej osady (CitySack.cs). Plik osobny, zeby nie ruszac TownFolk.Live.cs (rownolegle prace nad kiesa ludu).
    ///  - Odczyt (L0, L4, L5): stan kies W / R / P i gminy (miasto) albo podzamcza (zamek, KW) TERAZ - CALE kiesy, takze zapas na chleb (decyzja 1:
    ///    "lup z kazdej monety"). Kiesa nieczynna albo zamek przy wylaczonym zaworze zamkow - false (lup tylko z kasy kupcow).
    ///  - Zdjecie (L2): kiesa klasy -> wolajacy (pula lupu), ksiega kies: _ctlOut (kontrola zamknieta kies "wydatki" - r = 0, jak W7 LevyTake) i LDayOut (CSV).
    ///    Zwrot (wyjatek przy podziale) - _ctlOut z powrotem. Bez nowych pol zapisu.
    ///  - Pomiar 30 dob po zdobyciu (L0): "stac na jedzenie" (DAffF) i pobor pana i korony z kasy (DTake) z ostatniej doby ludu - tylko odczyt.
    /// Klasy: 0 = W (zamek: podzamcze), 1 = R, 2 = P, 3 = gmina. Nie rzuca.
    /// </summary>
    internal static partial class TownFolk
    {
        /// <summary>Lup: kiesy ludu osady TERAZ (purse[0..3] = W, R, P, gmina; zamek - tylko [0]). false - kiesa nieczynna, brak osady w kiesie, wies.</summary>
        internal static bool SackRead(Settlement st, long[] purse)
        {
            if (purse == null || purse.Length < 4) return false;
            purse[0] = purse[1] = purse[2] = purse[3] = 0;
            try
            {
                if (st == null || !Live) return false;
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null || f.Kind < 0 || f.Kind > 1) return false;
                if (f.Kind == 1 && !CastleLive) return false;
                if (f.Kind == 1) { purse[0] = Math.Max(0, f.KW); return true; }
                purse[0] = Math.Max(0, f.KW); purse[1] = Math.Max(0, f.KR); purse[2] = Math.Max(0, f.KP); purse[3] = Math.Max(0, f.KG);
                return true;
            }
            catch (Exception e) { Stumble("SackRead", e); purse[0] = purse[1] = purse[2] = purse[3] = 0; return false; }
        }

        /// <summary>Lup (L2): zdejmuje z kiesy klasy c (0-3) osady st najwyzej `amount` (najwyzej stan kiesy) - zwraca zdjete; wolajacy dzieli DOKLADNIE tyle.
        /// Ksiega kies: _ctlOut (kontrola zamknieta). Nie rzuca.</summary>
        internal static long SackTake(Settlement st, int c, long amount)
        {
            try
            {
                Folk f;
                if (st == null || amount <= 0 || c < 0 || c > 3 || !Live || !_f.TryGetValue(st, out f) || f == null || f.Kind > 1) return 0;
                if (f.Kind == 1 && (c != 0 || !CastleLive)) return 0;
                long x = Math.Min(amount, Math.Max(0, PurseOf(f, c)));
                if (x <= 0) return 0;
                AddPurse(f, c, -x);
                _ctlOut += x; f.LDayOut += x;   // recenzja: wiersz osady w kiesa-ludu.csv (kolumna wydatkow) bilansuje sie takze w dobie lupu
                return x;
            }
            catch (Exception e) { Stumble("SackTake", e); return 0; }
        }

        /// <summary>Lup (L2): zwrot zdjetego do kiesy klasy (wyjatek przy podziale - zloto wraca tam, skad wyszlo). Nie rzuca.</summary>
        internal static void SackBack(Settlement st, int c, long amount)
        {
            try
            {
                Folk f;
                if (st == null || amount <= 0 || c < 0 || c > 3 || !_f.TryGetValue(st, out f) || f == null || f.Kind > 1) return;
                AddPurse(f, c, amount);
                _ctlOut -= amount; f.LDayOut -= amount;
            }
            catch (Exception e) { Stumble("SackBack", e); }
        }

        /// <summary>Pomiar L0 (30 dob po zdobyciu): "stac na jedzenie" ostatniej doby ludu i pobor pana i korony z kasy kupcow w ostatnim oknie (NaN - brak liczb).
        /// Tylko odczyt.</summary>
        internal static void SackWatch(Settlement st, out double foodAfford, out double take)
        {
            foodAfford = double.NaN; take = double.NaN;
            try
            {
                Folk f;
                if (st == null || !_f.TryGetValue(st, out f) || f == null || f.Kind > 1) return;
                foodAfford = f.DAffF; take = f.DTake;
            }
            catch { foodAfford = double.NaN; take = double.NaN; }
        }
    }
}
