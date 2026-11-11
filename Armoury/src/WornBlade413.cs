using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 413 (audyt RBM 11.11, C1): ZUZYTA BRON NIE BIJE ZEREM. Lancuch bledu (kod, nie gra):
    ///  1. nasz postfiks ConditionScaling.DamagePatch (Patches.cs) na ItemModifier.ModifyDamage: dla zlego stanu wynik = floor(baza x StatFactor(stan)),
    ///     czyli ModifyDamage(100) = 85 / 49 / 18 dla Uszkodzony / Zmasakrowany / Wrak (krzywa 90 / 2);
    ///  2. RBM nie czyta obrazen broni - stan wchodzi tylko przez RBMCombat.Utilities.getSwingDamageFactor / getThrustDamageFactor (rbmcombat.cs:6511-6529):
    ///     wynik = wcd.SwingDamageFactor + (ModifyDamage(100) - 100) / 100 - kara ODEJMOWANA od wspolczynnika glowni (-0.15 / -0.51 / -0.82);
    ///  3. wspolczynniki w skali RBM to 0.25-1.7; bron nie-kuta (WeaponComponentData.SetDamageFactors, wcd.cs:471-499) dostaje JEDNA liczbe na zamach i
    ///     pchniecie wedlug SwingDamageType: Cut 0.40, Pierce 0.32, Blunt 0.80, bez zamachu 0 - suma schodzi ponizej zera (obuch 0.80 dopiero przy Wraku);
    ///  4. DamageRework (rbmcombat.cs:3747) liczy Math.Sqrt(ujemna) = NaN -> WeaponTypeDamage: prog = armorThresholdModifier / NaN, Math.Max(0, NaN) = NaN
    ///     -> MathF.Floor(NaN) -> ClampInt(.., 0, 2000) = 0 obrazen przy KAZDYM ciosie, takze w cel bez pancerza.
    /// Poprawka: postfiks na cztery metody RBM (RBMCombat.Utilities i kopia RBMAI.Utilities - obrazenia z przelamanej postury) - gdy modyfikator obniza
    /// (ModifyDamage(100) &lt; 100), wspolczynnik = bazowy x ModifyDamage(100) / 100 (mnoznik nie nizszy niz 0.05), czyli zawsze powyzej zera, gdy bazowy jest
    /// powyzej zera. Bazowy &lt;= 0 (bron nie-kuta bez zamachu - blad danych poza ta zmiana): wynik = bazowy, jak dla sztuki sprawnej (nasza kara nie robi z zera
    /// liczby ujemnej). Premie (ModifyDamage(100) >= 100) i brak modyfikatora: wzor RBM bez zmiany. Klucz WornWeaponStillBites NIE = wynik RBM nietkniety.
    /// Jedna regula dla gracza i AI (metoda nie zna wlasciciela broni). Pociski: RBM podaje im modyfikator null - bez zmiany.
    /// Latka w kampanii (NightHooks.InCampaign, raz na proces), nie przy pierwszej bitwie: te same metody licza karty broni w ekwipunku i obrazenia na arenie /
    /// w turnieju / w szturmie (misje bez BattleSense), a zalozona przed pierwszym ciosem procesu nie zostawia wywolan wklejonych przez JIT (male metody; recenzja
    /// 413 podaje 52 bajty IL przy progu wklejania 32 w .NET Framework - nie mierzone w grze). Bitwa wlasna z menu PRZED pierwsza kampania w procesie liczy starym
    /// wzorem (latki jeszcze nie ma), po kampanii latka zostaje czynna takze tam.
    /// 413-p (recenzja): (1) bazowy wspolczynnik czytany wprost z wcd (SwingDamageFactor / ThrustDamageFactor - dwa postfiksy), a nie liczony wstecz z wyniku RBM -
    /// wynik nie zalezy od tego, czy RBM albo cudzy postfiks zmieni wzor kary; (2) kubelki zlego stanu wedlug GearState.Of (nasze stany 1..3; kopie HeroHp ida po
    /// oryginale), reszta obnizajacych (modyfikatory gry / RBM / lupu) osobno; (3) licznik wyniku &lt;= 0 takze dla sztuki sprawnej i bez modyfikatora (dane: bron bez
    /// wspolczynnika tego uzycia); (4) liczniki licza WYWOLANIA metody (1-3 na cios: obrazenia, obnizka pancerza, postura), nie ciosy; (5) w bitwie linia konca misji
    /// pisana takze przy zerze wywolan - odroznia "nikt nie bil wrecz" od "latka ominieta".
    /// </summary>
    internal static class WornBlade413
    {
        private const float MinMul = 0.05f;   // podloga mnoznika = podloga StatFactor

        private static int _patched, _wanted;
        private static string _note = "nie zakladano";

        // liczniki misji (postfiks: glowny watek misji i karty ekwipunku; Interlocked tylko na rzadkiej sciezce zlego stanu)
        private static int _all, _withMod;                 // wszystkie wywolania / z modyfikatorem (zwykly przyrost - przyblizone)
        private static int _worn, _dam, _man, _wr, _otherBad;   // obnizajacy modyfikator: razem i wedlug GearState.Of (Uszkodzony / Zmasakrowany / Wrak / inny obnizajacy)
        private static int _saved, _zeroBase, _offZero;    // stary wzor <= 0 naprawiony / bazowy <= 0 przy zlym stanie / klucz NIE i wynik <= 0 (wywolania)
        private static int _zeroSound;                     // 413-p: wynik <= 0 bez naszej kary (brak modyfikatora albo sztuka sprawna / z premia) - blad danych broni
        private static int _sumP;                          // suma ModifyDamage(100) obnizajacych - srednia

        /// <summary>NightHooks.InCampaign (ArmouryBehavior.OnSessionLaunched, raz na proces). Zwraca opis do linii "Latki nocy (K3): kampania".</summary>
        internal static string ApplyInCampaign(Harmony h)
        {
            if (h == null) { _note = "brak Harmony"; return _note; }
            _patched = 0; _wanted = 0;
            var postSwing = new HarmonyMethod(typeof(WornBlade413), nameof(SwingPostfix)) { priority = Priority.Last };
            var postThrust = new HarmonyMethod(typeof(WornBlade413), nameof(ThrustPostfix)) { priority = Priority.Last };
            string miss = "";
            foreach (var tn in new[] { "RBMCombat.Utilities", "RBMAI.Utilities" })
            {
                Type t = null;
                try { t = AccessTools.TypeByName(tn); } catch { }
                if (t == null) { miss += " brak typu " + tn + ";"; continue; }
                foreach (var mn in new[] { "getSwingDamageFactor", "getThrustDamageFactor" })
                {
                    _wanted++;
                    MethodInfo mi = null;
                    try { mi = AccessTools.DeclaredMethod(t, mn, new[] { typeof(WeaponComponentData), typeof(ItemModifier) }); } catch { }
                    if (mi == null || !mi.IsStatic || mi.ReturnType != typeof(float)) { miss += " brak " + tn + "." + mn + "(WeaponComponentData, ItemModifier);"; continue; }
                    try
                    {
                        h.Patch(mi, postfix: mn == "getSwingDamageFactor" ? postSwing : postThrust);
                        if (Owned(mi, h.Id)) _patched++; else miss += " " + tn + "." + mn + " NIE weszla;";
                    }
                    catch (Exception e) { miss += " " + tn + "." + mn + " BLAD " + e.GetType().Name + ";"; Log.Error("WornBlade413.Patch(" + tn + "." + mn + ")", e); }
                }
            }
            var s = Settings.Current;
            if (_wanted == 0) _note = "RBM nieobecny - regula nieczynna (wzor gry bez zmian)";
            else _note = "wspolczynnik obrazen RBM " + _patched + "/" + _wanted + " (klucz WornWeaponStillBites " + (s != null && s.WornWeaponStillBites ? "TAK" : "NIE") + ")"
                         + (miss.Length > 0 ? " |" + miss : "");
            Log.Info("Zuzyta bron (413): latka raz na proces - " + _note + ". Zly stan broni (ModifyDamage(100) < 100): wspolczynnik glowni = bazowy x stan zamiast"
                     + " bazowy - kara (stary wzor przy Zmasakrowany / Wrak schodzil ponizej zera = cios za 0); krzywa teraz 100 -> "
                     + Probe(GearState.Damaged) + " / " + Probe(GearState.Mangled) + " / " + Probe(GearState.Wrecked) + " (Uszkodzony / Zmasakrowany / Wrak)"
                     + " | krzywa dziala tylko przy: Condition Scales Stats " + (s != null && s.ConditionScalesStats ? "TAK" : "NIE") + ", stany sprzetu w tej sesji "
                     + (GearState.On ? "TAK" : "NIE (jeszcze nie wlaczone albo wylaczone)") + " - przy NIE regula obejmuje tylko zle modyfikatory gry / RBM (-5..-15)."
                     + " Bazowy wspolczynnik czytany z danych broni (413-p). Bitwa wlasna przed pierwsza kampania w procesie liczy starym wzorem.");
            return _note;
        }

        private static string Probe(int st)
        {
            try { return Math.Max(1, (int)Math.Floor(100 * ConditionScaling.StatFactor(GearState.Value[st]))).ToString(); }
            catch { return "?"; }
        }

        private static bool Owned(MethodBase mi, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(mi);
                if (info == null) return false;
                foreach (var p in info.Postfixes) if (p != null && p.owner == id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(WornBlade413)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Po RBM getSwingDamageFactor(wcd, itemModifier). wcd null nie dochodzi (RBM czyta z niego pole przed nami) - straznik na wszelki wypadek.</summary>
        private static void SwingPostfix(WeaponComponentData __0, ItemModifier __1, ref float __result)
        {
            if (__0 == null) return;
            Core(__0.SwingDamageFactor, __1, ref __result);
        }

        /// <summary>Po RBM getThrustDamageFactor(wcd, itemModifier).</summary>
        private static void ThrustPostfix(WeaponComponentData __0, ItemModifier __1, ref float __result)
        {
            if (__0 == null) return;
            Core(__0.ThrustDamageFactor, __1, ref __result);
        }

        /// <summary>413-p: bazowy wspolczynnik uzycia (baseF) prosto z danych broni - ten sam, ktory RBM bierze do wzoru bazowy + (p - 100) / 100.
        /// Bez alokacji, bez try/catch (odczyty pol, jedno ModifyDamage, przy zlym stanie jedno wyszukanie stanu w slowniku).</summary>
        private static void Core(float baseF, ItemModifier __1, ref float __result)
        {
            _all++;
            if (__1 == null)
            {
                if (__result <= 0f) _zeroSound++;                          // bron bez wspolczynnika tego uzycia (dane) - nie nasza kara
                return;
            }
            _withMod++;
            var s = Settings.Current;
            if (s == null || !s.WornWeaponStillBites)
            {
                if (__result <= 0f) Interlocked.Increment(ref _offZero);   // pomiar bledu przy kluczu NIE (wynik bez zmiany)
                return;
            }
            int p = __1.ModifyDamage(100);
            if (p >= 100)                                                  // sprawna albo premia jakosci - wzor RBM
            {
                if (__result <= 0f) _zeroSound++;
                return;
            }
            float old = __result;
            Interlocked.Increment(ref _worn);
            Interlocked.Add(ref _sumP, p);
            int st = GearState.Of(__1);                                    // 1..3 = nasze stany (kopia HeroHp - stan oryginalu); 0 / -1 = inny obnizajacy modyfikator
            if (st == GearState.Damaged) Interlocked.Increment(ref _dam);
            else if (st == GearState.Mangled) Interlocked.Increment(ref _man);
            else if (st == GearState.Wrecked) Interlocked.Increment(ref _wr);
            else Interlocked.Increment(ref _otherBad);
            if (baseF <= 0.0001f)
            {
                // bron bez wspolczynnika tego uzycia (dane): jak sztuka sprawna, kara nie schodzi ponizej zera
                Interlocked.Increment(ref _zeroBase);
                __result = baseF < 0f ? 0f : baseF;
                return;
            }
            float mul = p / 100f;
            if (mul < MinMul) mul = MinMul;
            if (old <= 0f) Interlocked.Increment(ref _saved);
            __result = baseF * mul;
        }

        private static void Reset()
        {
            _all = 0; _withMod = 0; _worn = 0; _dam = 0; _man = 0; _wr = 0; _otherBad = 0; _saved = 0; _zeroBase = 0; _offZero = 0; _zeroSound = 0; _sumP = 0;
        }

        private static void Flush(Mission m)
        {
            var s = Settings.Current;
            string kind = "?";
            try { kind = MissionTeams.Kind(m); } catch { }
            if (_all == 0)
            {
                // spacer po miescie - bez linii; w bitwie / kryjowce linia takze przy zerze (413-p): odroznia "nikt nie bil wrecz" od "latka ominieta"
                if (_wanted > 0 && (kind == "bitwa" || kind == "kryjowka"))
                    Log.Info("Zuzyta bron (413): koniec misji (" + kind + ") | klucz " + (s != null && s.WornWeaponStillBites ? "TAK" : "NIE") + " | latki " + _patched + "/" + _wanted
                             + " | 0 wywolan wspolczynnika - jesli w tej misji byly ciosy wrecz, latka zostala OMINIETA (wywolania wklejone przez JIT przed zalozeniem latki:"
                             + " uruchom gre prosto do kampanii, bez bitwy wlasnej z menu).");
                return;
            }
            Log.Info("Zuzyta bron (413): koniec misji (" + kind + ") | klucz " + (s != null && s.WornWeaponStillBites ? "TAK" : "NIE") + " | latki " + _patched + "/" + _wanted
                     + " | wspolczynnik liczony " + _all + " razy (WYWOLANIA: 1-3 na cios wrecz), z modyfikatorem " + _withMod
                     + " | zly stan " + _worn + " wywolan (Uszkodzony " + _dam + ", Zmasakrowany " + _man + ", Wrak " + _wr + ", inny obnizajacy modyfikator " + _otherBad + ")"
                     + (_worn > 0 ? ", srednie ModifyDamage(100) " + (_sumP / (float)_worn).ToString("0", System.Globalization.CultureInfo.InvariantCulture) : "")
                     + " | stary wzor <= 0 (cios za 0) naprawiony " + _saved + " wywolan"
                     + " | bazowy wspolczynnik <= 0 przy zlym stanie (dane, bez kary) " + _zeroBase
                     + " | wynik <= 0 bez kary stanu (sztuka sprawna albo bez modyfikatora - dane broni) " + _zeroSound
                     + " | klucz NIE i wynik <= 0 (wywolania za 0) " + _offZero + ".");
            Reset();
        }

        /// <summary>Liczniki misji: zerowanie na starcie, linia sumy na koncu.</summary>
        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
            }

            protected override void OnEndMission()
            {
                try { Flush(Mission); } catch (Exception e) { Log.Error("WornBlade413.Flush", e); }
                base.OnEndMission();
            }
        }
    }
}
