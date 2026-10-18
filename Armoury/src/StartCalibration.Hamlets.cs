using System;

namespace Armoury
{
    /// <summary>
    /// sw-przysiolki (noc 17/18.10): wejscie KS dla przysiolkow ukladu v6 - wlasny plik czesciowy, zeby nie ruszac StartCalibration.cs (inne paczki nocy dopisuja tam
    /// swoje wiersze). VillageHamlets.SessionStart (OnSessionLaunched, po McmSettings.Apply i PRZED StartCalibration.SessionStart) wola HamletLandOverlay: plik ziemi
    /// (raz na proces, EnsureLand) i nakladka ziemi punktu przysiolka na wiersze S (VillageHamlets.OverlayKs) - zanim ResolveLive zbuduje bloki stanu zastosowanego
    /// z row.O i zanim pierwszy Plan zrobi zrzut. Nic poza tym: Plan, stawki i zapis KS bez zmian.
    /// Recenzja (sw-przysiolki): crc ziemi KS (_landCrc - straznik zapisu ZASTOSOWANEGO w ResolvePending, uzbrojenie i Confirm) obejmuje nakladke: bez nakladki = crc
    /// danych pliku jak dotad, z nakladka = crc pliku + podpis nadpisanych wierszy. Przelaczenie klucza Village Hamlet Ks Land (albo ukladu) po wlaczeniu KS = "inny plik
    /// ziemi" - stan pominiety, Plan od nowa (dotad bloki 215 przysiolkow cicho przebudowane na innej ziemi). Liczone co sesje od crc pliku z pierwszego wczytania.
    /// </summary>
    internal static partial class StartCalibration
    {
        private static uint _landCrcFile;        // crc danych pliku ziemi (EnsureLand - raz na proces); _landCrc tej sesji = ten crc albo crc z nakladka przysiolkow
        private static bool _landCrcFileSet;

        internal static void HamletLandOverlay()
        {
            try
            {
                EnsureLand();
                if (_landVil != null && !_landCrcFileSet) { _landCrcFile = _landCrc; _landCrcFileSet = true; }
                if (_landCrcFileSet) _landCrc = _landCrcFile;           // nakladka poprzedniej sesji zdjeta (OverlayKs przywraca tez wiersze)
                uint eff = VillageHamlets.OverlayKs(_landVil, _landCrc);
                if (_landCrcFileSet) _landCrc = eff;
            }
            catch (Exception e) { Stumble("HamletLandOverlay", e); }
        }
    }
}
