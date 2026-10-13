using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Armoury
{
    /// <summary>
    /// E8/K (255; projekt etapu 8 v2 rozdz. 2 R1, rozdz. 5.1 wiersz "GiveGoldAction") - WSPOLNY WRAPPER WYPLAT ETAPU 8: PayFromGiver.
    /// Wspolne z E8/D1 i E8/D2 (te paczki dopisuja swoje konteksty i odbiorcow; tutaj wersja minimalna dla zlecen kowalskich).
    ///
    /// Gra wyplaca graczowi "z niczego": GiveGoldAction.ApplyBetweenCharacters(null, gracz, kwota). Transpiler paczki zamienia TO JEDNO wywolanie
    /// w metodzie gry na PayFromGiver o tej samej sygnaturze (Hero dajacy, Hero odbiorca, int kwota, bool bez komunikatu), a prefiks tej metody
    /// otwiera kontekst (Open: kto naprawde placi), finalizer go zamyka (Close - takze po wyjatku). W kontekscie z czynnym kluczem i znanym platnikiem:
    ///   placi platnik ze swojej kiesy, najwyzej tyle, ile ma (GiveGoldAction tez tnie do kiesy bohatera - liczymy sami, zeby znac niedoplate);
    ///   brak = mniej, komunikat gracza "X could pay only N of the agreed M." i licznik; bez doplat (regula Jeffa: "jesli ubywa, to ubywa").
    /// Bez kontekstu, przy wylaczonym kluczu albo gdy dajacy nie jest "nikim" - wywolanie gry bez zmian (stan sprzed), kwota "z niczego" liczona w kontekscie.
    /// Zdarzenie gry (HeroOrPartyTradedGold) idzie juz z prawdziwym platnikiem - ksiega pieniadza widzi przelew bohater -> bohater, RealPayer.GivePrefix
    /// (stos kontekstow E3/1) nie ma tu nic do zrobienia (jego stos jest pusty, a dajacy nie jest juz null).
    /// Stan: jeden kontekst na watek ([ThreadStatic], zagniezdzenie przez Prev). Nic w zapisie.
    /// </summary>
    internal static class GiverPay
    {
        internal sealed class Ctx
        {
            public string What;          // nazwa okna do logu
            public Hero Payer;           // kto placi zamiast "nikogo"
            public bool On;              // klucz paczki czynny
            public int Calls;            // wyplaty widziane w kontekscie
            public long Due, Paid;       // obiecane / zaplacone przez platnika
            public int Short;            // wyplaty nizsze niz obiecane (pusta kiesa)
            public long FromNothing;     // wyplacone "z niczego" (klucz wylaczony albo brak platnika)
            internal Ctx Prev;
        }

        [ThreadStatic] private static Ctx _ctx;

        internal static Ctx Open(string what, Hero payer, bool on)
        {
            var c = new Ctx { What = what, Payer = payer, On = on, Prev = _ctx };
            _ctx = c;
            return c;
        }

        internal static void Close(Ctx c)
        {
            if (c == null) return;
            if (ReferenceEquals(_ctx, c)) { _ctx = c.Prev; return; }
            // kontekst zamykany nie w kolejnosci (wyjatek glebiej) - wypnij go z lancucha, reszta zostaje
            for (var x = _ctx; x != null; x = x.Prev)
                if (ReferenceEquals(x.Prev, c)) { x.Prev = c.Prev; return; }
        }

        internal static void Reset() { _ctx = null; }

        /// <summary>Zamiast GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, disableNotification) w metodzie gry z otwartym kontekstem.</summary>
        public static void PayFromGiver(Hero giver, Hero recipient, int amount, bool disableNotification)
        {
            var c = _ctx;
            Hero payer = c != null ? c.Payer : null;
            if (c == null || giver != null || recipient == null || amount <= 0 || !c.On || payer == null || ReferenceEquals(payer, recipient))
            {
                if (c != null && giver == null && recipient != null && amount > 0) { c.Calls++; c.FromNothing += amount; }
                GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, disableNotification);   // jak gra
                return;
            }
            int pay = Math.Min(amount, Math.Max(0, payer.Gold));
            c.Calls++; c.Due += amount; c.Paid += pay;
            if (pay > 0) GiveGoldAction.ApplyBetweenCharacters(payer, recipient, pay, disableNotification);
            if (pay < amount)
            {
                c.Short++;
                if (ReferenceEquals(recipient, Hero.MainHero))
                    Log.Player((payer.Name != null ? payer.Name.ToString() : "The client") + " could pay only " + pay + " of the agreed " + amount + ".", true);
            }
        }
    }
}
