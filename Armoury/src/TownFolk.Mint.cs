using System;

namespace Armoury
{
    /// <summary>
    /// 311b (CrownMint, CoinWearPerYear) - STRONA LUDU zuzycia monety: moneta w kiesach ludu sciera sie, gubi i obcina (historycznie ok. 0.2-0.5% rocznie).
    ///  - ILE: kazda klasa (W, R, P, gmina; podzamcze - KW) traci perDay x swoja kiesa; ulamek zlotowki przechodzi na jutro (Folk.WearCarry, bez zapisu).
    ///  - Tylko kiesa czynna (Live) i osada skalibrowana (Cal), miasta i podzamcza (Kind 0, 1); wsie maja kiese w kasie osady (te liczy CrownMint przy kasach).
    ///  - Zdjecie: kiesa klasy -> nicosc (ujscie "zuzycie monety" ksiegi pieniadza - wolajacy wpisuje je w Seal31.UCoinWear), ksiega kies: _ctlOut i LDayOut
    ///    (kontrola zamknieta kies r = 0, jak TourneyPurseTake). Nie rzuca.
    /// </summary>
    internal static partial class TownFolk
    {
        /// <summary>311b: zuzycie monety we wszystkich kiesach ludu; zwraca zdjete naprawde, purses - ile kies stracilo choc 1 zl.</summary>
        internal static long CoinWear(double perDay, out int purses)
        {
            purses = 0;
            long tot = 0;
            try
            {
                if (!(perDay > 0) || !Live || OffMain()) return 0;
                foreach (var f in _f.Values)
                {
                    if (f == null || !f.Cal || f.Kind > 1) continue;
                    long took = 0;
                    for (int c = 0; c < 4; c++)
                    {
                        long v = PurseOf(f, c);
                        if (v <= 0) { f.WearCarry[c] = 0; continue; }
                        double w = v * perDay + f.WearCarry[c];
                        long x = (long)Math.Floor(w);
                        f.WearCarry[c] = w - x;
                        if (x <= 0) continue;
                        if (x > v) x = v;
                        AddPurse(f, c, -x);
                        took += x;
                    }
                    if (took <= 0) continue;
                    _ctlOut += took; f.LDayOut += took;
                    tot += took; purses++;
                }
            }
            catch (Exception e) { Stumble("CoinWear", e); }
            return tot;
        }
    }
}
