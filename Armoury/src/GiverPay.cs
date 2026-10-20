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
    ///   r4-zlecenia (20.10, klucz CraftOrderClanPurse, tylko kontekst zlecen kowalskich - Ctx.ClanPurse): zleceniodawca-lord, ktory nie jest glowa rodu -
    ///   brak doplaca glowa rodu (kiesa rodu, Jeff "kiesa rodu zarzadza szef rodu") w granicach CraftOrderClanPurse.Room; komunikat mowi, kto zaplacil ile.
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
            public long Seq;             // E8/D1: kolejnosc otwarcia wobec ramek zadan (QuestPay8) - wyplate bierze kontekst otwarty pozniej
            public long FromNothing;     // wyplacone "z niczego" (klucz wylaczony albo brak platnika)
            public bool ClanPurse;       // r4-zlecenia (CraftOrderClanPurse): brak zleceniodawcy-lorda doplaca glowa rodu (tylko kontekst zlecen kowalskich)
            public long FromHead;        // r4-zlecenia: doplacone przez glowe rodu
            public int HeadN, HeadFull;  // r4-zlecenia: wyplaty z doplata glowy / w tym doplacone do pelna
            internal Ctx Prev;
        }

        [ThreadStatic] private static Ctx _ctx;
        [ThreadStatic] private static long _seq;   // E8/D1: wspolny licznik otwarc (konteksty zlecen kowalskich i ramki zadan)

        internal static long NextSeq() { return ++_seq; }
        internal static long TopSeq { get { var c = _ctx; return c != null ? c.Seq : 0; } }

        internal static Ctx Open(string what, Hero payer, bool on)
        {
            var c = new Ctx { What = what, Payer = payer, On = on, Prev = _ctx, Seq = NextSeq() };
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
            if (QuestPay8.Route(giver, recipient, amount, disableNotification)) return;   // E8/D1 (256): wyplata w metodzie zadania - platnik / odbiorca / skala w QuestPay8
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
                // r4-zlecenia (Craft Order Clan Purse): lord, ktory nie jest glowa rodu - brak doplaca glowa rodu (kiesa rodu) w granicach CraftOrderClanPurse.Room
                Hero head = null; int help = 0;
                if (c.ClanPurse) help = CraftOrderClanPurse.TopUp(payer, recipient, amount, pay, disableNotification, out head);
                if (help > 0) { c.HeadN++; c.FromHead += help; }
                int got = pay + help;
                if (got < amount) c.Short++; else c.HeadFull++;
                if (ReferenceEquals(recipient, Hero.MainHero))
                {
                    string who = payer.Name != null ? payer.Name.ToString() : "The client";
                    if (help <= 0)
                        Log.Player(who + " could pay only " + pay + " of the agreed " + amount + ".", true);
                    else
                    {
                        string hn = head != null && head.Name != null ? head.Name.ToString() : "the head of the clan";
                        if (got >= amount)
                        {
                            if (pay <= 0) Log.Player(hn + ", head of the clan, paid the agreed " + amount + " on behalf of " + who + ".");   // recenzja: pusta kiesa lorda
                            else Log.Player(who + " paid " + pay + " and " + hn + ", head of the clan, paid the remaining " + help + " of the agreed " + amount + ".");
                        }
                        else if (pay <= 0)
                            Log.Player(hn + ", head of the clan, could pay only " + help + " of the agreed " + amount + " on behalf of " + who + ".", true);
                        else
                            Log.Player(who + " and " + hn + ", head of the clan, could pay only " + got + " of the agreed " + amount
                                       + " (" + who + ": " + pay + ", " + hn + ": " + help + ").", true);
                    }
                }
            }
        }

        // ------------------------------------------------------------ E8/D1 (256): dopiski wspolnego wrappera (uzywa ich tez E8/D2)

        /// <summary>Przelew platnik -> odbiorca do wysokosci kiesy platnika; brak = mniej i komunikat gracza (ten sam co przy zleceniach). Zwraca kwote zaplacona.</summary>
        internal static int Transfer(Hero payer, Hero recipient, int due, bool disableNotification)
        {
            if (payer == null || recipient == null || due <= 0) return 0;
            int pay = Math.Min(due, Math.Max(0, payer.Gold));
            if (pay > 0) GiveGoldAction.ApplyBetweenCharacters(payer, recipient, pay, disableNotification);
            if (pay < due && ReferenceEquals(recipient, Hero.MainHero))
                Log.Player((payer.Name != null ? payer.Name.ToString() : "The client") + " could pay only " + pay + " of the agreed " + due + ".", true);
            return pay;
        }

        /// <summary>Zamiast GiveGoldAction.ApplyForCharacterToParty(giver, party, amount, disableNotification) w metodach zadan (gracz -> nic: odbiorca z QuestPay8).</summary>
        public static void PayToParty(Hero giver, TaleWorlds.CampaignSystem.Party.PartyBase party, int amount, bool disableNotification)
        {
            if (QuestPay8.RouteParty(giver, party, amount, disableNotification)) return;
            GiveGoldAction.ApplyForCharacterToParty(giver, party, amount, disableNotification);   // jak gra
        }

        /// <summary>Zamiast hero.ChangeHeroGold(delta) w metodach zadan (gracz placi "w nicosc" - odbiorca z QuestPay8).</summary>
        public static void HeroGold(Hero hero, int delta)
        {
            if (QuestPay8.RouteHeroGold(hero, delta)) return;
            hero.ChangeHeroGold(delta);   // jak gra
        }
    }
}
