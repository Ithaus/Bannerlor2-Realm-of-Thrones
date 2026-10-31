using System;
using System.Reflection;
using HarmonyLib;

namespace Armoury
{
    /// <summary>
    /// 307c - UDZIAL MAJATKU BK BEZ DZIELENIA PRZEZ ZERO (klucz BkEstateShareNoWorkforce; krytyk 307 'reszta' waga 3 - "Licznik podatku osady: przepelnienie"
    /// 142b 32 razy na 90 dob, baza 141 3 razy; Jeff 30.10 4 razy).
    /// [K BK, dekompilacja BannerKings.dll] BKEstatesModel.GetEstateWorkforceProportion(Estate, PopulationData) =
    ///   MathF.Clamp((estate.Population + estate.Slaves) / data.LandData.AvailableWorkForce, 0, 1) - metoda zwykla (nie wirtualna), zadeklarowana w BKEstatesModel
    ///   (BannerKingsConfig.Instance.EstatesModel = new BKEstatesModel()). Przy AvailableWorkForce = 0 (wies po rabunkach - wojna w Dorzeczu): 0/0 = NaN (Clamp
    ///   przepuszcza NaN) -> EstateData.AccumulateTradeTax: RoundRandomized(NaN) = int.MinValue -> licznik wsi +-2^31 (TaxCounterGuard naprawia); x/0 = +inf -> 1
    ///   -> kazdy majatek bierze CALY podatek taboru (kilka majatkow = wiecej niz wies miala; reszta wsi ujemna - "zerowane" straznika).
    /// Zmiana: prefiks na BKEstatesModel.GetEstateWorkforceProportion (AccessTools.DeclaredMethod na klasie deklarujacej - pulapka 15.10): sila robocza wsi <= 0 -> 0
    ///   (nikt nie pracuje - majatek nie ma udzialu w podatku ani produkcji wsi), metoda BK pominieta. Inaczej BK bez zmian. Wolane tez z BK
    ///   CalculateEstateProduction (opis udzialu) - ta sama regula. Straz TaxCounterGuard zostaje. Klucz wylaczony = BK.
    /// Odczyt pol BK refleksja (Armoury nie referuje BannerKings.dll). Wyjatek = potkniecie, metoda BK biegnie (bez globalnego wylacznika).
    /// Linia "Udzial majatku BK (307c): ..." przy wpieciu i raz na dobe (gdy byly wywolania albo poprawki).
    /// </summary>
    internal static class BkEstateShare307
    {
        private static bool _tried, _wired;
        private static string _state = "nie probowano";
        private static PropertyInfo _landProp, _workProp;
        private static int _calls, _fixed, _stumbles, _dStumbles, _dCalls, _dFixed;

        private static bool On { get { var s = Settings.Current; return s != null && s.BkEstateShareNoWorkforce; } }

        /// <summary>Seal31.EnsureHooks (OnSessionLaunched, raz na proces).</summary>
        internal static void EnsureHooks()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Models.BKModels.BKEstatesModel");
                var pd = AccessTools.TypeByName("BannerKings.Managers.Populations.PopulationData");
                var ld = AccessTools.TypeByName("BannerKings.Managers.Populations.LandData");
                if (t == null || pd == null || ld == null) { _state = "BRAK (BannerKings nie znaleziony)"; Log.Info("Udzial majatku BK (307c): latka " + _state + "."); return; }
                var m = AccessTools.DeclaredMethod(t, "GetEstateWorkforceProportion");
                _landProp = AccessTools.DeclaredProperty(pd, "LandData");
                _workProp = AccessTools.DeclaredProperty(ld, "AvailableWorkForce");
                if (m == null || m.ReturnType != typeof(float) || m.GetParameters().Length != 2 || _landProp == null || _workProp == null)
                { _state = "BRAK (inna sygnatura BK)"; Log.Info("Udzial majatku BK (307c): latka " + _state + "."); return; }
                int before = Seal31.Wired.Count;
                Seal31.Patch("udzial majatku BK bez sily roboczej (BKEstatesModel.GetEstateWorkforceProportion)", m, typeof(BkEstateShare307), nameof(Prefix), null, null, Priority.First);
                _wired = Seal31.Wired.Count > before;
                var info = Harmony.GetPatchInfo(m);
                bool ours = false;
                if (info != null) foreach (var p in info.Prefixes) if (p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(BkEstateShare307)) ours = true;
                _wired = _wired && ours;
                _state = _wired ? "wpieta" : "BRAK (Harmony nie wpial)";
            }
            catch (Exception e) { _state = "BRAK (" + e.GetType().Name + ")"; Stumble(e); }
            Log.Info("Udzial majatku BK (307c): latka " + _state + " | klucz Bk Estate Share No Workforce " + (On ? "TAK" : "NIE") + " - sila robocza wsi <= 0 -> udzial 0 (BK: 0/0 = NaN, x/0 = 1).");
        }

        /// <summary>Prefiks: __1 = PopulationData. false = wynik 0 (wies bez sily roboczej).</summary>
        public static bool Prefix(object __1, ref float __result)
        {
            if (!On || __1 == null || _landProp == null || _workProp == null) return true;
            try
            {
                _calls++; _dCalls++;
                var land = _landProp.GetValue(__1, null);
                if (land == null) return true;
                object v = _workProp.GetValue(land, null);
                int work = v is int i ? i : Convert.ToInt32(v);
                if (work > 0) return true;
                __result = 0f;
                _fixed++; _dFixed++;
                return false;
            }
            catch (Exception e) { Stumble(e); return true; }
        }

        private static void Stumble(Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error("BkEstateShare307 (potkniecie " + _stumbles + ")", e);
        }

        /// <summary>Raz na dobe (z TaxCounterGuard.Sweep, przed "Pieniadz swiata"): linia doby i zerowanie.</summary>
        internal static void Daily()
        {
            if (_tried && (_dCalls > 0 || _dFixed > 0 || _dStumbles > 0))
                Log.Info("Udzial majatku BK (307c): doba | latka " + _state + ", klucz " + (On ? "TAK" : "NIE") + " | wywolan " + _dCalls + ", udzial 0 przy sile roboczej <= 0: " + _dFixed
                         + " | w sesji wywolan " + _calls + ", poprawek " + _fixed + ", potkniecia " + _stumbles + " (doba " + _dStumbles + ").");
            _dCalls = _dFixed = _dStumbles = 0;
        }

        /// <summary>Nowa sesja: liczniki od zera (latka zostaje - raz na proces).</summary>
        internal static void Reset() { _calls = _fixed = _stumbles = _dStumbles = _dCalls = _dFixed = 0; }
    }
}
