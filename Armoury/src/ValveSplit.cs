using System;

namespace Armoury
{
    /// <summary>
    /// Wspolny pomocnik zaworow kas osad (projekt etapu 2, krok B; S15 - bez TownPurse z paczki 111', ktora jest w etapie 5): przyciecie
    /// udzialu do 0..1. Jedno miejsce na regule, z ktorej skorzysta zawor zamku (110), a pozniej zawor miast (111') i kiesa ludu (KL).
    /// </summary>
    internal static class ValveSplit
    {
        /// <summary>Udzial obciety do 0..1; NaN = 0.</summary>
        internal static float Unit(float v)
        {
            if (float.IsNaN(v) || v <= 0f) return 0f;
            return v >= 1f ? 1f : v;
        }
    }
}
