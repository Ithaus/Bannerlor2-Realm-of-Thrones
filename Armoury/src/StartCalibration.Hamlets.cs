using System;

namespace Armoury
{
    /// <summary>
    /// sw-przysiolki (noc 17/18.10): wejscie KS dla przysiolkow ukladu v6 - wlasny plik czesciowy, zeby nie ruszac StartCalibration.cs (inne paczki nocy dopisuja tam
    /// swoje wiersze). VillageHamlets.SessionStart (OnSessionLaunched, po McmSettings.Apply i PRZED StartCalibration.SessionStart) wola HamletLandOverlay: plik ziemi
    /// (raz na proces, EnsureLand) i nakladka ziemi punktu przysiolka na wiersze S (VillageHamlets.OverlayKs) - zanim ResolveLive zbuduje bloki stanu zastosowanego
    /// z row.O i zanim pierwszy Plan zrobi zrzut. Nic poza tym: Plan, stawki i zapis KS bez zmian.
    /// </summary>
    internal static partial class StartCalibration
    {
        internal static void HamletLandOverlay()
        {
            try
            {
                EnsureLand();
                VillageHamlets.OverlayKs(_landVil);
            }
            catch (Exception e) { Stumble("HamletLandOverlay", e); }
        }
    }
}
