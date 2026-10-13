using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace Armoury
{
    /// <summary>
    /// E8/O2 (266, PLAN 8.17; RD8 K2 / O2) - POROWNANIE KLUCZY "warsztat + linia" dla slownikow WorkshopLaw._wip i _rank.
    ///
    /// Dotad oba slowniki mialy klucz KeyValuePair(warsztat, nazwa linii) z porownaniem domyslnym. KeyValuePair nie ma wlasnego Equals ani GetHashCode,
    /// wiec .NET 4.7.2 liczy skrot tylko z pierwszego pola (wszystkie linie jednego warsztatu w jednym kubelku) i porownuje pola refleksja z pudelkowaniem -
    /// kilka mikrosekund na odczyt, przy kazdym cyklu warsztatu i przy budowie planu doby (jedyne takie klucze w repo - RD8 K2).
    ///
    /// Tu: TEN SAM typ klucza i TA SAMA rownosc (warsztat - ta sama referencja: Workshop nie nadpisuje Equals, gra 1.4.8; nazwa linii - te same znaki),
    /// tylko policzona wprost. Skrot z tozsamosci warsztatu i z nazwy linii - linie jednego warsztatu trafiaja do roznych kubelkow.
    /// Kolejnosc wpisow slownika (kolejnosc dopisywania = kolejnosc rekordow zapisu "arm_workshops") nie zalezy od skrotu - format i kolejnosc zapisu bez zmian.
    ///
    /// Tryb (Fast) zmienia WYLACZNIE SetMode - razem z przepisaniem obu slownikow w tej samej kolejnosci (wpisy policzone starym skrotem nie daloby sie znalezc nowym).
    /// Fast = false: oba wywolania ida do porownania domyslnego - dokladnie kod sprzed paczki. Co 64. porownanie w trybie szybkim liczy tez stara droge (R9).
    /// Plik bez zaleznosci od reszty moda (proba poza gra kompiluje go wprost). Tylko watek gry (jak caly WorkshopLaw).
    /// </summary>
    internal sealed class WorkshopLineKeys : IEqualityComparer<KeyValuePair<Workshop, string>>
    {
        private static readonly IEqualityComparer<KeyValuePair<Workshop, string>> Slow = EqualityComparer<KeyValuePair<Workshop, string>>.Default;
        internal static readonly WorkshopLineKeys Instance = new WorkshopLineKeys();

        /// <summary>Tryb porownania - zmienia tylko SetMode.</summary>
        internal static bool Fast { get; private set; }

        // liczniki (sam log): skroty i porownania w trybie szybkim od ostatniego ClearDay; probki, rozjazdy i przepisania - narastajaco
        internal static int Hashes, Compares, Probes, Mismatches, Rekeys, Lost;
        internal static string LastMismatch;

        private WorkshopLineKeys() { }

        public bool Equals(KeyValuePair<Workshop, string> a, KeyValuePair<Workshop, string> b)
        {
            if (!Fast) return Slow.Equals(a, b);
            bool r = ReferenceEquals(a.Key, b.Key) && string.Equals(a.Value, b.Value);
            if ((++Compares & 63) == 0)   // R9: stara droga obok nowej
            {
                bool old = Slow.Equals(a, b);
                Probes++;
                if (old != r)
                {
                    Mismatches++;
                    LastMismatch = "linia \"" + (a.Value ?? "null") + "\" / \"" + (b.Value ?? "null") + "\": wlasne porownanie " + r + ", domyslne " + old;
                    return old;
                }
            }
            return r;
        }

        public int GetHashCode(KeyValuePair<Workshop, string> k)
        {
            if (!Fast) return Slow.GetHashCode(k);
            Hashes++;
            return (RuntimeHelpers.GetHashCode(k.Key) * 397) ^ (k.Value != null ? k.Value.GetHashCode() : 0);
        }

        internal static void ClearDay() { Hashes = 0; Compares = 0; }

        /// <summary>
        /// Zmiana trybu razem z przepisaniem obu slownikow (te same wpisy, ta sama kolejnosc). Bez zmiany trybu - nic. Zwraca true, gdy przepisano.
        /// Lost > 0 = po przepisaniu ubylo wpisow (nie powinno sie zdarzyc: rownosc w obu trybach jest ta sama) - wolajacy liczy to jako potkniecie.
        /// </summary>
        internal static bool SetMode<TA, TB>(bool fast, Dictionary<KeyValuePair<Workshop, string>, TA> a, Dictionary<KeyValuePair<Workshop, string>, TB> b)
        {
            if (fast == Fast) return false;
            var la = new List<KeyValuePair<KeyValuePair<Workshop, string>, TA>>(a);
            var lb = new List<KeyValuePair<KeyValuePair<Workshop, string>, TB>>(b);
            a.Clear(); b.Clear();
            Fast = fast;
            foreach (var kv in la) a[kv.Key] = kv.Value;
            foreach (var kv in lb) b[kv.Key] = kv.Value;
            Rekeys++;
            int lost = (la.Count - a.Count) + (lb.Count - b.Count);
            if (lost != 0) Lost += Math.Abs(lost);
            return true;
        }
    }
}
