using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Armoury
{
    /// <summary>
    /// r4-zlecenia (20.10, PLAN 8.3; Jeff 20.10 doslownie: "Nie tylko ze swojej kiesy, kiesa rodu zarzadza szef rodu.") - ZLECENIE KOWALSKIE LORDA Z KIESY RODU
    /// (klucz CraftOrderClanPurse, domyslnie TAK; dziala razem z Craft Orders Historical - bez niego gra wyplaca z niczego).
    ///
    /// Dotad (E8/K, GiverPay.PayFromGiver): nagrode zlecenia placi zleceniodawca TYLKO ze swojej kiesy; lord, ktory nie jest glowa rodu, czesto ma pusta kiese
    /// (kiesa rodu to kiesa glowy - ClanBudget: "kiesa rodziny" dopelnia kiese glowy z kies doroslych) - przy oddaniu "X could pay only N of the agreed M.",
    /// a przeglad NZ (CraftOrdersPurse) nie dawal mu nawet zlozyc zlecenia (linia NZ: "odrzucone przy tworzeniu 11-32 (brak kiesy)" na dobe w testach WGRANIA 91).
    /// Teraz:
    ///   - przy oddaniu: najpierw placi zamawiajacy ze swojej kiesy (jak dotad), brakujaca czesc doplaca GLOWA RODU ze swojej kiesy - najwyzej to, co ma ponad
    ///     AiGearGoldReserve (zold ma pierwszenstwo - ta sama rezerwa co przy sprzecie i koniach AI), a w rodzie z budzetem 166 najwyzej niewydany przydzial
    ///     sprzetu rodu (ClanBudget.GearCap; po doplacie ClanBudget.GearSpent - jak przy koniach i sprzecie AI). Dopiero gdy i glowa nie ma - "could pay only"
    ///     jak dotad. Komunikat gracza mowi, kto zaplacil ile;
    ///   - przy tworzeniu i w przegladzie zlecen (CraftOrdersPurse): miara "stac go" = kiesa zamawiajacego + to, co moze doplacic glowa (w przegladzie doby
    ///     doplata glowy rozdzielana na zlecenia wszystkich czlonkow rodu - jedna kiesa glowy, nie liczona dwa razy).
    /// Kto: zamawiajacy jest lordem (Hero.IsLord - nie notabl, nie szef gangu ze zlecenia zadania), jego rod nie jest rodem gracza, glowa (Clan.Leader) zyje
    /// i nie jest nim samym. Zamknieta ekonomia: GiveGoldAction glowa -> gracz (zdarzenie gry z prawdziwym platnikiem), nic z niczego.
    /// Stan tylko w pamieci (liczniki doby); nic w zapisie. Reset z CraftOrders8.Reset. Wyjatek = licznik potkniec, bez wylacznika.
    /// Linie: przy oddaniu "Zlecenie z kiesy rodu (r4-zlecenia): ..." (raz na oddane zlecenie z niedoplata zamawiajacego - zdarzenie rzadkie, reczne);
    /// licznik doby w "Kuznia (E8/K): dzien N" (doplata glowy) i w "Zlecenia kowalskie (NZ)" (kiesa rodu).
    /// </summary>
    internal static class CraftOrderClanPurse
    {
        private const string Head = "Zlecenie z kiesy rodu (r4-zlecenia): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.CraftOrderClanPurse; } }
        /// <summary>Czynne: klucz i platnik z kiesy (Craft Orders Historical).</summary>
        internal static bool On { get { return KeyOn && CraftOrders8.PayOn; } }

        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("CraftOrderClanPurse." + where, e); } catch { }
        }

        internal static int Stumbles { get { return _stumbles; } }

        internal static void Reset() { _stumbles = 0; _errSites.Clear(); }

        /// <summary>Glowa rodu, ktora moze doplacic za zamawiajacego; null - zamawiajacy nie jest lordem, jest glowa, rod gracza albo bez zywej glowy.</summary>
        internal static Hero HeadOf(Hero giver)
        {
            if (giver == null || !giver.IsLord) return null;
            var c = giver.Clan;
            if (c == null || c.IsEliminated || c == Clan.PlayerClan) return null;
            var h = c.Leader;
            if (h == null || ReferenceEquals(h, giver) || !h.IsAlive || ReferenceEquals(h, Hero.MainHero)) return null;
            return h;
        }

        /// <summary>Ile glowa moze dzis doplacic: kiesa ponad AiGearGoldReserve; w rodzie z budzetem 166 najwyzej niewydany przydzial sprzetu rodu.</summary>
        internal static int Room(Hero head, out bool capped, out int gearLeft)
        {
            capped = false; gearLeft = 0;
            if (head == null) return 0;
            var s = Settings.Current;
            int reserve = s != null ? Math.Max(0, s.AiGearGoldReserve) : 0;
            long room = Math.Max(0L, (long)head.Gold - reserve);
            capped = head.Clan != null && ClanBudget.GearCap(head.Clan, out gearLeft);
            if (capped) room = Math.Min(room, Math.Max(0, gearLeft));
            return (int)Math.Min(int.MaxValue, room);
        }

        /// <summary>CraftOrdersPurse (tworzenie zlecenia): ile glowa zamawiajacego moze doplacic (0 - klucz wylaczony albo zamawiajacy bez glowy, ktora placi).</summary>
        internal static long RoomFor(Hero giver)
        {
            try
            {
                if (!On) return 0;
                var h = HeadOf(giver);
                bool c; int g;
                return h != null ? Room(h, out c, out g) : 0;
            }
            catch (Exception e) { Stumble("RoomFor", e); return 0; }
        }

        /// <summary>GiverPay.PayFromGiver (kontekst zlecen kowalskich z kluczem): zamawiajacy zaplacil "pay" z "amount" - glowa rodu doplaca brak w granicach
        /// Room. Zwraca kwote doplacona przez glowe (0 - nikt nie doplacil); head - glowa, ktora mogla doplacic (null - zamawiajacy bez takiej glowy).</summary>
        internal static int TopUp(Hero giver, Hero recipient, int amount, int pay, bool disableNotification, out Hero head)
        {
            head = null;
            int help = 0;
            try
            {
                int missing = amount - pay;
                if (giver == null || recipient == null || missing <= 0) return 0;
                head = HeadOf(giver);
                if (head == null || ReferenceEquals(head, recipient)) { head = null; return 0; }
                bool capped; int gearLeft;
                int gold0 = head.Gold;
                int room = Room(head, out capped, out gearLeft);
                int give = Math.Min(missing, room);
                if (give > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(head, recipient, give, disableNotification);
                    help = give;
                    if (capped) ClanBudget.GearSpent(head.Clan, help);   // 166: schodzi z przydzialu sprzetu rodu (zloto poszlo glowa -> gracz)
                }
                try
                {
                    var s = Settings.Current;
                    var sb = new StringBuilder(400);
                    sb.Append(Head).Append(Name(giver)).Append(" (rod ").Append(giver.Clan != null && giver.Clan.Name != null ? giver.Clan.Name.ToString() : "?")
                      .Append(") winien ").Append(amount).Append(", zaplacil ze swojej kiesy ").Append(pay)
                      .Append(" | glowa rodu ").Append(Name(head)).Append(" doplacil ").Append(help).Append(" z brakujacych ").Append(missing)
                      .Append(" (kiesa glowy ").Append(gold0).Append(", rezerwa na zold ").Append(s != null ? Math.Max(0, s.AiGearGoldReserve) : 0)
                      .Append(capped ? ", przydzial sprzetu rodu przed/po " + gearLeft.ToString(Inv) + "/" + Math.Max(0, gearLeft - help).ToString(Inv) : ", rod bez budzetu 166 - bez przydzialu")
                      .Append(") | niedoplata ").Append(missing - help).Append(missing - help > 0 ? " (could pay only)" : "").Append('.');
                    Log.Info(sb.ToString());
                }
                catch (Exception e) { Stumble("TopUp(log)", e); }
                return help;
            }
            catch (Exception e) { Stumble("TopUp", e); return help; }   // przelew juz zrobiony liczy sie (komunikat i liczniki)
        }

        internal static string Name(Hero h) { return h != null && h.Name != null ? h.Name.ToString() : "?"; }
    }
}
