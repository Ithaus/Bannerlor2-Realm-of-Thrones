using System.Collections.Generic;

namespace Armoury
{
    internal static partial class TownInns
    {
        /// <summary>m96-wies (PayNotes): rozbicie czesci gracza do komunikatu zaplaty - te same skladniki co ostatnia linia podpowiedzi noclegu ("You: bed 1 + your
        /// horse 1 + ..."), np. " (bed 1, horse 1, 2 spare horses 2)". Pozycje z kwota 0 pominiete; brak pozycji - pusty napis.</summary>
        private static string PlayerSplit(Bill b, int q)
        {
            if (b == null || q < 0 || q > 2) return "";
            var parts = new List<string>();
            int room = RoomOf(q);
            if (room > 0) parts.Add((RoomWord(q) == "your bed" ? "bed " : "room ") + room);
            if (b.OwnHorse && PHorse[q] > 0) parts.Add("horse " + PHorse[q]);
            int spare = b.Spare + b.ArmorySpare;
            if (spare > 0 && PHorse[q] > 0) parts.Add(spare + (spare == 1 ? " spare horse " : " spare horses ") + spare * PHorse[q]);
            return parts.Count > 0 ? " (" + string.Join(", ", parts.ToArray()) + ")" : "";
        }

        /// <summary>m96-wies (PayNotes): jeden komunikat na nocleg calej druzyny - zaplata gracza z powodem i rozbiciem, potem ludzie i towarzysze (jak dotad).
        /// Klucz wylaczony albo gracz nie zaplacil nic (komunikatu gry tez nie bylo) - napis jak dotad (asBefore).</summary>
        private static void InnNote(string kind, int q, string place, Bill b, int playerPaid, string rest, string asBefore)
        {
            if (PayNotes.On && playerPaid > 0) PayNotes.Paid(kind, playerPaid, " for your night at a " + QualityOf(q) + " in " + place + PlayerSplit(b, q) + "." + rest);
            else Log.Player(asBefore);
        }
    }
}
