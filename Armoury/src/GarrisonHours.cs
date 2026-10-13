using System;

namespace Armoury
{
    /// <summary>
    /// E8/O5 (269, PLAN 8.17; RD8 K7 / O7; docs/etap8/PROJEKT-ETAP8-... rozdz. 3 "E8/O5") - TERMINY KOLEJKI NADWYZEK ZALOG PO GODZINACH.
    /// Sama arytmetyka, bez klas gry (sprawdzana poza gra na tym pliku). Kazda zaloga ma termin raz na 168 godzin gry: numer godziny % 168 == Id % 168
    /// (dotad raz na 7 dob w takcie doby: Id % 7 == dzien % 7, ok. 32 zalogi w jednej klatce). Sluchacz godziny (GarrisonArmory.Hourly) pyta Range
    /// o zakres numerow godzin do obsluzenia, a petla kolejki (GarrisonArmory.SellQueue) pyta Due o kazda zaloge.
    /// Dlaczego zakres, a nie "ta godzina": gra nie gubi wywolan HourlyTickEvent (MBCampaignEvent.CheckUpdate nadrabia w petli), ale numer godziny
    /// (int)CampaignTime.Now.ToHours w chwili wywolania moze sie powtorzyc albo przeskoczyc - wywolanie przychodzi w pierwszej klatce PO terminie, wiec
    /// przy fazie zdarzenia tuz przed granica godziny raz wypada przed nia, raz za nia; po wczytaniu zapisu Campaign.CreateCampaignEvents ustawia faze
    /// od nowa. Rowne porownanie gubiloby wtedy termin zalogi na caly tydzien.
    /// </summary>
    internal static class GarrisonHours
    {
        internal const int Week = 7 * 24;

        /// <summary>
        /// Zakres numerow godzin do obsluzenia przy wywolaniu w godzinie `now`, gdy ostatnio obsluzona byla `last` (-1 = pierwsze wywolanie sesji albo
        /// pierwsze po wlaczeniu klucza). False = nic do zrobienia (ten sam numer godziny drugi raz - terminy juz obsluzone). Pierwsze wywolanie bierze
        /// tez dwie poprzednie godziny: zapis nie trzyma `last`, a wczytanie przesuwa faze zdarzenia i gubi jeden numer (dwa, gdy pierwsza klatka po
        /// terminie jest dluga) - zaloga z tych godzin moze byc obsluzona drugi raz (sprzeda najwyzej to, co przybylo), za to zadna nie czeka dodatkowego
        /// tygodnia. Nie do odzyskania bez zapisu: dwa wczytania bez wywolania godziny miedzy nimi. `caught` - ile numerow nadrobiono ponad biezacy
        /// (pierwsze wywolanie nie liczy sie). Najwyzej caly tydzien - kazda zaloga najwyzej raz na wywolanie.
        /// </summary>
        internal static bool Range(int last, int now, out int from, out int caught)
        {
            caught = 0;
            if (last >= 0 && now < last) last = -1;   // czas gry cofniety bez Reset (nie powinno sie zdarzyc) - jak pierwsze wywolanie
            from = last < 0 ? now - 2 : last + 1;
            if (now < from) return false;
            if (now - from >= Week) from = now - (Week - 1);
            if (last >= 0) caught = now - from;
            return true;
        }

        /// <summary>Czy termin zalogi o tym Id (Id % 168) wypada w godzinach from..now (wlacznie; now - from najwyzej 167).</summary>
        internal static bool Due(uint id, int from, int now)
        {
            int slot = (int)(id % Week);
            int off = ((slot - from) % Week + Week) % Week;   // za ile godzin od `from` wypada termin (0..167), takze dla ujemnego `from`
            return off <= now - from;
        }
    }
}
