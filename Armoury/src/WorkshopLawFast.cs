using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E8/O2 (266, PLAN 8.17; RD8 K2 / O2) - WORKSHOPLAW BEZ REFLEKSJI I POWTORZEN. Optymalizacja: te same sztuki, ceny i place (R9), mniej pracy na cykl.
    /// Wzor Z33: WorkshopLaw.CyclePrefix 840 ms / 7 093 cykli na dobe (136 us na cykl). Cztery rzeczy, jeden wylacznik WorkshopLawFast (off = kod sprzed paczki):
    ///  1. klucze _wip i _rank - wlasne porownanie zamiast refleksji (WorkshopLineKeys.cs; typ klucza, rownosc i kolejnosc wpisow bez zmian - zapis "arm_workshops" ten sam);
    ///  2. nazwa linii (LineKey) z pamieci: lista wyjsc linii (ta sama referencja u kazdej kopii Production) -> napis; przy kazdym odczycie sprawdzane, ze
    ///     kategorie wyjsc sa te same obiekty co przy zapamietaniu (cudzy mod, ktory dopisze albo podmieni wyjscie, dostaje nowy napis);
    ///  3. mnoznik polki (SupplyDemand.Factor) raz na koszyk (typ x tier) w jednym przebiegu Candidates - Factor zalezy tylko od osady, koszyka, polki i zamowien,
    ///     a w przebiegu rankingu nic ich nie zmienia; wyjatek z Factor = bez pamieci (kazda sztuka liczy sama, jak dotad);
    ///  4. lista zakazanych (Forbidden) z pamieci na sztuke - bez ToLowerInvariant() dla kazdej sztuki kategorii przy kazdym rankingu; inna lista w MCM = pamiec od zera.
    ///     Tylko w Candidates - Forbidden wolane z innych klas (ColdStart, CraftRealm, strzelarze) zostaje stara droga.
    /// Pamieci Needs / DayWageOf z projektu NIE MA (odstepstwo, CHANGELOG): proba poza gra - wzor liczy sie ok. 11 ns, odczyt pamieci ok. 10 ns, zysk ponizej szumu,
    /// a pamiec wymagalaby pilnowania 14 wejsc (ustawienia, wagi surowcow, lupa). Poziom plac miasta (K2-6) i dniowka licza sie wiec na zywo, jak dotad.
    /// R9: co 64. trafienie kazdej pamieci liczy tez stara droge i porownuje; rozjazd = wynik starej drogi, licznik, najwyzej 3 linie "ROZJAZD" w logu.
    /// Wyjatek w nowej drodze = potkniecie (licznik, jedna linia Log.Error) i wynik starej drogi dla tego wywolania - bez globalnego wylacznika.
    /// Linia doby (sam log) "Warsztaty bez powtorzen (E8/O2)"; stan czyszczony w WorkshopLaw.Reset (nowa kampania = nowe obiekty przedmiotow i typow warsztatow).
    /// Nic w zapisie, zadnego zlota ani sztuki. Tylko watek gry (jak caly WorkshopLaw).
    /// </summary>
    internal static partial class WorkshopLaw
    {
        internal static bool FastOn { get { var s = Settings.Current; return s != null && s.WorkshopLawFast; } }

        private sealed class RefCmp<T> : IEqualityComparer<T> where T : class
        {
            internal static readonly RefCmp<T> Instance = new RefCmp<T>();
            public bool Equals(T a, T b) { return ReferenceEquals(a, b); }
            public int GetHashCode(T o) { return RuntimeHelpers.GetHashCode(o); }
        }

        // liczniki doby (sam log) i sesji
        private static int _fLineHit, _fLineNew, _fFactorHit, _fFactorNew, _fForbidHit, _fForbidNew, _fProbes;
        private static int _fMismatch, _fMismatchLogged, _fStumbles, _fKeyMisSeen, _fKeyProbesSeen;
        private static bool _fErrLogged;

        private static void FastStumble(string where, Exception e)
        {
            _fStumbles++;
            if (!_fErrLogged) { _fErrLogged = true; Log.Error("WorkshopLaw.Fast(" + where + ")", e); }   // raz na sesje; reszta w liczniku potkniec
        }

        private static void FastMismatch(string what, string detail)
        {
            _fMismatch++;
            if (_fMismatchLogged >= 3) return;
            _fMismatchLogged++;
            Log.Info("Warsztaty bez powtorzen (E8/O2): ROZJAZD " + what + " - " + detail + " - wziety wynik starej drogi; po 3 liniach dalsze rozjazdy tylko w liczniku linii doby.");
        }

        /// <summary>Nowa gra / wczytanie (z WorkshopLaw.Reset): pamieci i liczniki od zera. Tryb kluczy zostaje (slowniki sa wtedy puste).</summary>
        private static void FastReset()
        {
            _lineKeys.Clear(); _forbidMemo.Clear(); _forbidMemoSrc = null; _basketF.Clear();
            _fLineHit = _fLineNew = _fFactorHit = _fFactorNew = _fForbidHit = _fForbidNew = _fProbes = 0;
            _fMismatch = _fMismatchLogged = _fStumbles = 0; _fErrLogged = false;
            WorkshopLineKeys.ClearDay(); _fKeyMisSeen = WorkshopLineKeys.Mismatches; _fKeyProbesSeen = WorkshopLineKeys.Probes;
        }

        // ------------------------------------------------------------ 1. klucze _wip i _rank
        /// <summary>Poczatek cyklu warsztatu (przed pierwszym odczytem _wip / _rank): tryb kluczy wedlug wylacznika; zmiana trybu przepisuje oba slowniki.</summary>
        private static void FastSync()
        {
            try
            {
                bool want = FastOn;
                if (want == WorkshopLineKeys.Fast) return;
                int lost0 = WorkshopLineKeys.Lost;
                WorkshopLineKeys.SetMode(want, _wip, _rank);
                if (WorkshopLineKeys.Lost != lost0)
                    FastStumble("FastSync", new InvalidOperationException("po zmianie trybu kluczy ubylo wpisow: " + (WorkshopLineKeys.Lost - lost0)));
            }
            catch (Exception e) { FastStumble("FastSync", e); }
        }

        // ------------------------------------------------------------ 2. nazwa linii
        private sealed class LineMemo { public ItemCategory[] Cats; public string Key; }
        private static readonly Dictionary<object, LineMemo> _lineKeys = new Dictionary<object, LineMemo>(RefCmp<object>.Instance);

        /// <summary>Nazwa linii (kategorie wyjsc zlaczone "+") - jak LineKeyOld, z pamieci na liste wyjsc linii.</summary>
        private static string LineKey(WorkshopType.Production p)
        {
            if (!FastOn) return LineKeyOld(p);
            try
            {
                var outs = p.Outputs;   // Production to struct; lista wyjsc to ten sam obiekt u kazdej kopii
                if (outs == null) return LineKeyOld(p);
                int n = outs.Count;
                LineMemo m;
                if (_lineKeys.TryGetValue(outs, out m) && m.Cats.Length == n)
                {
                    bool same = true;
                    for (int i = 0; i < n; i++) if (!ReferenceEquals(m.Cats[i], outs[i].Item1)) { same = false; break; }
                    if (same)
                    {
                        if ((++_fLineHit & 63) == 0)   // R9
                        {
                            string old = LineKeyOld(p);
                            _fProbes++;
                            if (!string.Equals(old, m.Key)) { FastMismatch("nazwa linii", "z pamieci \"" + m.Key + "\", stara droga \"" + old + "\""); m.Key = old; }
                        }
                        return m.Key;
                    }
                }
                string k = LineKeyOld(p);
                var cats = new ItemCategory[n];
                for (int i = 0; i < n; i++) cats[i] = outs[i].Item1;
                _lineKeys[outs] = new LineMemo { Cats = cats, Key = k };
                _fLineNew++;
                return k;
            }
            catch (Exception e) { FastStumble("LineKey", e); return LineKeyOld(p); }
        }

        // ------------------------------------------------------------ 3. mnoznik polki raz na koszyk (jeden przebieg Candidates)
        private struct BasketF { public float F, D; public int S; }
        private static readonly Dictionary<int, BasketF> _basketF = new Dictionary<int, BasketF>();

        /// <summary>Jak Revenue(town, it, out d, out sh): ten sam przychod, popyt i polka koszyka; Factor liczony raz na koszyk tego przebiegu rankingu
        /// (_basketF czyszczone na poczatku przebiegu). ArmsPricing.Multiplier zalezy od sztuki - liczony dla kazdej, jak dotad.</summary>
        private static float RevenueFast(Town town, ItemObject it, out float d, out int sh)
        {
            try
            {
                d = 0f; sh = 0;
                if (town == null || it == null) return 0f;
                float f = 1f;
                int basket = BasketOf(it);
                BasketF b;
                if (_basketF.TryGetValue(basket, out b))
                {
                    f = b.F; d = b.D; sh = b.S;
                    if ((++_fFactorHit & 63) == 0)   // R9
                    {
                        float f2 = 1f, d2 = 0f; int s2 = 0; bool ok = false;
                        try { if (SupplyDemand.Active) f2 = SupplyDemand.Factor(town.Settlement, it, false, out d2, out s2); ok = true; } catch { }
                        _fProbes++;
                        if (!ok || !f2.Equals(f) || !d2.Equals(d) || s2 != sh)
                        {
                            FastMismatch("mnoznik polki koszyka", it.StringId + " w " + town.Name + ": z pamieci x" + f + " (popyt " + d + ", polka " + sh + "), stara droga "
                                         + (ok ? "x" + f2 + " (popyt " + d2 + ", polka " + s2 + ")" : "wyjatek"));
                            f = f2; d = d2; sh = s2;
                            if (ok) _basketF[basket] = new BasketF { F = f2, D = d2, S = s2 }; else _basketF.Remove(basket);
                        }
                    }
                }
                else
                {
                    bool ok = false;
                    try { if (SupplyDemand.Active) f = SupplyDemand.Factor(town.Settlement, it, false, out d, out sh); ok = true; } catch { }
                    if (ok) { _basketF[basket] = new BasketF { F = f, D = d, S = sh }; _fFactorNew++; }
                }
                float arms = 1f;   // wpis 88 (audyt pkt 9): jak w Revenue
                try { arms = ArmsPricing.Multiplier(town.Settlement, it); } catch { }
                return RevenueOf(it, f, arms);
            }
            catch (Exception e) { FastStumble("Revenue", e); return Revenue(town, it, out d, out sh); }
        }

        // ------------------------------------------------------------ 4. lista zakazanych
        private static readonly Dictionary<ItemObject, bool> _forbidMemo = new Dictionary<ItemObject, bool>(RefCmp<ItemObject>.Instance);
        private static string _forbidMemoSrc;

        /// <summary>Jak Forbidden(it), z pamieci na sztuke (id sztuki sie nie zmienia); inna lista WorkshopForbiddenIds = pamiec od zera.</summary>
        private static bool ForbiddenFast(ItemObject it)
        {
            try
            {
                var src = Settings.Current.WorkshopForbiddenIds ?? "";
                if (!ReferenceEquals(src, _forbidMemoSrc))
                {
                    if (!string.Equals(src, _forbidMemoSrc)) _forbidMemo.Clear();
                    _forbidMemoSrc = src;
                }
                bool r;
                if (_forbidMemo.TryGetValue(it, out r))
                {
                    if ((++_fForbidHit & 63) == 0)   // R9
                    {
                        bool old = Forbidden(it);
                        _fProbes++;
                        if (old != r) { FastMismatch("lista zakazanych", it.StringId + ": z pamieci " + r + ", stara droga " + old); _forbidMemo[it] = old; r = old; }
                    }
                    return r;
                }
                r = Forbidden(it);
                _forbidMemo[it] = r;
                _fForbidNew++;
                return r;
            }
            catch (Exception e) { FastStumble("Forbidden", e); return Forbidden(it); }
        }

        // ------------------------------------------------------------ linia doby
        /// <summary>E8/O2 (266): linia doby "Warsztaty bez powtorzen (E8/O2)" (ArmouryBehavior, takt doby) - sam log.</summary>
        internal static void DailyFast()
        {
            try
            {
                var s = Settings.Current;
                if (s != null && Campaign.Current != null)
                {
                    // rozjazdy porownania kluczy (WorkshopLineKeys liczy sam - plik bez zaleznosci) dopisywane tu do wspolnego licznika i linii ROZJAZD
                    int km = WorkshopLineKeys.Mismatches;
                    if (km != _fKeyMisSeen)
                    {
                        int add = km - _fKeyMisSeen; _fKeyMisSeen = km;
                        if (add > 0) { FastMismatch("klucz linii", WorkshopLineKeys.LastMismatch ?? "-"); _fMismatch += add - 1; }
                    }
                    Log.Info("Warsztaty bez powtorzen (E8/O2): dzien " + ((int)CampaignTime.Now.ToDays - 1)
                             + " | WorkshopLawFast " + (s.WorkshopLawFast ? "[WL]" : "[wyl]") + (On ? "" : " (warsztaty uzbrojenia jako firmy WYLACZONE w MCM)")
                             + " | klucze linii (robota w toku i ranking): " + (WorkshopLineKeys.Fast ? "wlasne porownanie" : "porownanie domyslne (refleksja)")
                             + ", skrotow " + WorkshopLineKeys.Hashes + ", porownan " + WorkshopLineKeys.Compares + ", przepisan slownikow od startu gry " + WorkshopLineKeys.Rekeys
                             + " | nazwa linii z pamieci " + _fLineHit + " / liczona " + _fLineNew
                             + " | mnoznik polki koszyka z pamieci " + _fFactorHit + " / liczony " + _fFactorNew
                             + " | lista zakazanych z pamieci " + _fForbidHit + " / liczona " + _fForbidNew
                             + " | probka 1/64 (stara i nowa droga): " + (_fProbes + WorkshopLineKeys.Probes - _fKeyProbesSeen) + ", rozjazdow od startu sesji " + _fMismatch
                             + " | potkniecia od startu sesji " + _fStumbles + ".");
                }
            }
            catch (Exception e) { Log.Error("WorkshopLaw.DailyFast", e); }
            _fLineHit = _fLineNew = _fFactorHit = _fFactorNew = _fForbidHit = _fForbidNew = _fProbes = 0;
            _fKeyProbesSeen = WorkshopLineKeys.Probes;
            WorkshopLineKeys.ClearDay();
        }
    }
}
