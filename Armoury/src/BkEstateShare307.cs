using System;
using System.Collections;
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
    /// Recenzja 307c (klucz BkEstateShareCapSum): BK obcina kazdy udzial do 1 osobno, a Estate.Population liczy sie osobno od AvailableWorkForce
    ///   (0.85 x (poddani + dzierzawcy) + niewolnicy) - przy malej dodatniej sile roboczej suma udzialow &gt; 1, AccumulateTradeTax daje majatkom (GiveGoldAction z null)
    ///   wiecej niz caly podatek, licznik wsi schodzi ponizej zera i BK go zeruje - nadwyzka zostaje jako zloto z niczego. Teraz mianownik = max(sila robocza,
    ///   suma ludzi wszystkich majatkow wsi) - suma udzialow &lt;= 1 (podatek wsi i majatkow razem = podatek taboru).
    /// Odczyt pol BK refleksja (Armoury nie referuje BannerKings.dll). Wyjatek = potkniecie, metoda BK biegnie (bez globalnego wylacznika).
    /// Linia "Udzial majatku BK (307c): ..." przy wpieciu i raz na dobe (gdy byly wywolania albo poprawki).
    /// </summary>
    internal static class BkEstateShare307
    {
        private static bool _tried, _wired;
        private static string _state = "nie probowano";
        private static PropertyInfo _landProp, _workProp, _edProp, _estProp, _popProp, _slvProp;
        private static int _calls, _fixed, _stumbles, _dStumbles, _dCalls, _dFixed, _capped, _dCapped;
        private static bool CapOn { get { var s = Settings.Current; return s != null && s.BkEstateShareCapSum; } }

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
                var edT = AccessTools.TypeByName("BannerKings.Managers.Populations.Estates.EstateData");
                var esT = AccessTools.TypeByName("BannerKings.Managers.Populations.Estates.Estate");
                if (pd != null && edT != null && esT != null)
                {
                    _edProp = AccessTools.DeclaredProperty(pd, "EstateData");
                    _estProp = AccessTools.DeclaredProperty(edT, "Estates");
                    _popProp = AccessTools.DeclaredProperty(esT, "Population");
                    _slvProp = AccessTools.DeclaredProperty(esT, "Slaves");
                }
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
            Log.Info("Udzial majatku BK (307c): latka " + _state + " | klucz Bk Estate Share No Workforce " + (On ? "TAK" : "NIE") + " - sila robocza wsi <= 0 -> udzial 0 (BK: 0/0 = NaN, x/0 = 1) | klucz Bk Estate Share Cap Sum " + (CapOn ? "TAK" : "NIE")
                     + " - suma udzialow majatkow wsi <= 1 (odczyt majatkow " + (_edProp != null && _estProp != null && _popProp != null && _slvProp != null ? "jest" : "BRAK - bez obciecia sumy") + ").");
        }

        /// <summary>Prefiks: __0 = Estate, __1 = PopulationData. false = wynik 0 (wies bez sily roboczej) albo udzial wzgledem sumy ludzi majatkow (suma &gt; sila robocza).</summary>
        public static bool Prefix(object __0, object __1, ref float __result)
        {
            if ((!On && !CapOn) || __1 == null || _landProp == null || _workProp == null) return true;
            try
            {
                _calls++; _dCalls++;
                var land = _landProp.GetValue(__1, null);
                if (land == null) return true;
                object v = _workProp.GetValue(land, null);
                int work = v is int i ? i : Convert.ToInt32(v);
                if (work > 0) return CapOn ? !CapSum(__0, __1, work, ref __result) : true;
                if (!On) return true;
                __result = 0f;
                _fixed++; _dFixed++;
                return false;
            }
            catch (Exception e) { Stumble(e); return true; }
        }

        /// <summary>Recenzja 307c: suma ludzi majatkow wsi &gt; sila robocza -> udzial = ludzie majatku / suma (true = wynik ustawiony). Inaczej false (BK liczy sam).</summary>
        private static bool CapSum(object estate, object data, int work, ref float result)
        {
            if (estate == null || _edProp == null || _estProp == null || _popProp == null || _slvProp == null) return false;
            var ed = _edProp.GetValue(data, null);
            var list = ed != null ? _estProp.GetValue(ed, null) as IEnumerable : null;
            if (list == null) return false;
            long sum = 0;
            foreach (var e in list) if (e != null) sum += Math.Max(0, Pop(e));
            if (sum <= work) return false;
            long mine = Math.Max(0, Pop(estate));
            result = Math.Max(0f, Math.Min(1f, (float)((double)mine / sum)));
            _capped++; _dCapped++;
            return true;
        }

        private static int Pop(object e)
        {
            object p = _popProp.GetValue(e, null), s = _slvProp.GetValue(e, null);
            return (p is int a ? a : Convert.ToInt32(p)) + (s is int b ? b : Convert.ToInt32(s));
        }

        private static void Stumble(Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error("BkEstateShare307 (potkniecie " + _stumbles + ")", e);
        }

        /// <summary>Raz na dobe (z ArmouryBehavior, przed MoneyLedger.Daily - recenzja: nie z TaxCounterGuard.Sweep): linia doby i zerowanie.</summary>
        internal static void Daily()
        {
            if (_tried && (_dCalls > 0 || _dFixed > 0 || _dStumbles > 0 || _dCapped > 0))
                Log.Info("Udzial majatku BK (307c): doba | latka " + _state + ", klucz " + (On ? "TAK" : "NIE") + " | wywolan " + _dCalls + ", udzial 0 przy sile roboczej <= 0: " + _dFixed
                         + " | suma udzialow > 1 przycieta (klucz Cap Sum " + (CapOn ? "TAK" : "NIE") + "): " + _dCapped
                         + " | w sesji wywolan " + _calls + ", poprawek " + _fixed + ", przyciec sumy " + _capped + ", potkniecia " + _stumbles + " (doba " + _dStumbles + ").");
            _dCalls = _dFixed = _dStumbles = _dCapped = 0;
        }

        /// <summary>Nowa sesja: liczniki od zera (latka zostaje - raz na proces).</summary>
        internal static void Reset() { _calls = _fixed = _stumbles = _dStumbles = _dCalls = _dFixed = _capped = _dCapped = 0; }
    }
}
