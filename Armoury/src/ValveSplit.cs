using System;

namespace Armoury
{
    /// <summary>
    /// Wspolny pomocnik zaworow kas osad (projekt etapu 2, krok B; S15 - bez TownPurse.Split z paczki 111', ktora jest w etapie 5): przyciecie
    /// udzialu do 0..1 i podzial zdjetej kwoty pan / korona (klucz 114). Jedno miejsce na regule, z ktorej korzysta zawor zamku (110 + 114),
    /// a pozniej zawor miast (111') i kiesa ludu (KL).
    /// </summary>
    internal static class ValveSplit
    {
        /// <summary>Udzial obciety do 0..1; NaN = 0.</summary>
        internal static float Unit(float v)
        {
            if (float.IsNaN(v) || v <= 0f) return 0f;
            return v >= 1f ? 1f : v;
        }

        /// <summary>Podzial zdjetej kwoty: korona dostaje (1 - udzial pana) w dol, pan reszte; suma zawsze rowna `draw` (jak TownPurse.Split z 114).</summary>
        internal static void Split(int draw, float lordShare, out int lord, out int crown)
        {
            if (draw <= 0) { lord = 0; crown = 0; return; }
            double c = draw * (1.0 - Unit(lordShare));
            crown = c <= 0.0 ? 0 : (c >= draw ? draw : (int)c);
            lord = draw - crown;
        }
    }
}
