using System;

namespace Armoury
{
    /// <summary>
    /// sw-uchodzcy (noc 17/18.10, klucz RaidRefugeesToTowns; RaidRefugees.cs): uchodzcy z najazdu mieszkajacy w miescie sa jego BIEDOTA - poza ksiega stanow
    /// (N - mieszczanie ksiegi, suma stanow = ksiega, ZC6 ich nie awansuje), ale w mianowniku Dostatku biedoty miasta (AwayE[ELabour] - dziela zarobek klasy W kiesy ludu,
    /// do ktorej naleza) i w stanach KS (KsSnapshot: biedota i ludzie miasta - potrzeba targu ZC4, koszyk klas, rece pol mieszczan Planu). Okreg wsi - mianownik
    /// tylko z uchodzcami w regionie (WarPeople). Klucz wylaczony: nic (Guests = 0). Liczby do kontroli linii "Uchodzcy do miast (RT)" (RaidRefugees.NoteZc).
    /// </summary>
    internal static partial class DailyLife
    {
        /// <summary>Compute (po PeopleDay i WarPeople): uchodzcy z najazdu w kazdym miescie - Rec.Guests i mianownik biedoty. Nie rzuca na zewnatrz (potkniecia).</summary>
        private static void TownGuests(int day)
        {
            if (_recs == null) return;
            bool on = RaidRefugees.On;
            double lab = 0, guests = 0; int towns = 0;
            foreach (var r in _recs)
            {
                if (r == null || r.Kind != KTown || r.St == null) continue;
                if (Fin(r.N[ELabour])) lab += Math.Max(0.0, r.N[ELabour]);
                if (!on) continue;
                double g = 0;
                try { g = RaidRefugees.PresentIn(r.St); } catch (Exception e) { g = 0; Stumble("TownGuests(miasto)", e); }
                if (!(g > 0) || double.IsInfinity(g)) continue;
                r.Guests = g; r.AwayE[ELabour] += g; guests += g; towns++;
            }
            if (on) RaidRefugees.NoteZc(day, lab, guests, towns);
        }
    }
}
