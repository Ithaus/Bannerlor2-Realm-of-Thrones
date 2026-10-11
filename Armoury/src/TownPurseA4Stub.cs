namespace Armoury
{
    /// <summary>
    /// ZASLEPKA E5/A4 (recenzja A4, uwaga 1) - DO USUNIECIA PRZY SCALENIU Z E5/A1. W tym drzewie nie ma kas miast A1 (TownPurse, galaz w-toku/e5-a1),
    /// wiec kasa miasta NIE jest prawdziwym pieniadzem: regulator gry kasuje co dobe 25% nadwyzki ponad cel. BesiegedPay.Holds pyta TownPurse.On -
    /// tu zawsze false, czyli oblezone miasto placi rente i polityki jak dotad (licznik "kasa pod regulatorem gry"), a wstrzymanie dziala dla zamkow (110).
    /// Scalenie z A1: USUNAC TEN PLIK. Prawdziwy TownPurse z A1 i ta zaslepka w jednym projekcie = blad kompilacji CS0101 - scalenie nie przejdzie po cichu.
    /// Razem z usunieciem (obowiazkowe): w A1 TownPurse.Valve zastapic `if (st.IsUnderSiege) { _vSiege++; return true; }` przez policzenie
    /// `int pay = CastlePurse.Dues(...)` PRZED warunkiem i `if (BesiegedPay.Holds(st, BesiegedPay.KTownRent, pay, true)) { _vSiege++; return true; }`
    /// (opis w BesiegedPay.cs i w CHANGELOG E5/A4, "Poprawki po recenzji").
    /// </summary>
    internal static class TownPurse
    {
        /// <summary>A1: kasa miasta prowadzona jako prawdziwy pieniadz (regulator w trybie 1/2 i latki 110 wpiete). Bez A1 - false.</summary>
        internal static bool On { get { return false; } }
    }
}
