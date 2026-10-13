using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Armoury
{
    /// <summary>
    /// E8/O1 (265, PLAN 8.17; RD8 K6 / O6): cudze metody (DTE, BK, gra), ktore wolalismy przez MethodInfo.Invoke, wolane DELEGATEM - bez tablicy object[]
    /// i pakowania argumentow przy kazdym wywolaniu. Ta sama metoda i to samo wejscie metody: delegat nie jest wklejany w wolajacego, wiec prefiksy
    /// Harmony na celu (nasze - UniqueLaw na DTE AddItemToPartyArmory, MaterialOrders.ReleasePrefix na BK ReleaseCaravanFromHold - i cudze, np. straznik
    /// BK na SetMoveGoToSettlement) biegna jak przy Invoke (sprawdzone poza gra na tym samym 0Harmony.dll: delegat zbudowany przed latka i po niej).
    /// Jedyna roznica: wyjatek z celu leci bez opakowania TargetInvocationException (miejsca wywolan lapia Exception - bez zmiany skutku).
    /// Budowa raz na miejsce, leniwie przy pierwszym uzyciu w kampanii (nigdy przy starcie gry - CLAUDE.md 7). Nieudana budowa (inna sygnatura w innej
    /// wersji cudzego moda) = to miejsce dalej wola przez Invoke, a jego nazwa stoi w linii doby "Polka i delegaty (E8/O1)". Wylacznik DteDelegates =
    /// false: kazde miejsce wola przez Invoke jak przed paczka (delegaty zbudowane, nieuzywane).
    /// </summary>
    internal static class FastCall
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.DteDelegates; } }

        // miejsce -> czy delegat zbudowany (od startu procesu: cele sa szukane raz na proces, jak latki)
        private static readonly Dictionary<string, bool> _sites = new Dictionary<string, bool>();
        internal static long Fast, Slow;     // doba: wywolania delegatem / przez MethodInfo.Invoke (takze przy wylaczniku)
        internal static int Stumbles;        // od startu procesu: nieudane budowy (Log.Error raz na miejsce)

        internal static void ClearDay() { Fast = Slow = 0; }

        private static void Note(string what, bool ok, Exception e)
        {
            bool had, was = false;
            lock (_sites) { had = _sites.TryGetValue(what, out was); _sites[what] = ok; }
            if (ok || (had && !was)) return;
            Stumbles++;
            Log.Error("FastCall(" + what + ")", e ?? new InvalidOperationException("delegat nie powstal"));
        }

        /// <summary>Metoda statyczna albo instancyjna typu znanego przy kompilacji (pierwszy parametr delegata = instancja). m == null (cel nie znaleziony
        /// - nie ma tego moda) -> null bez wpisu; nieudana budowa -> null i wpis "nieudane".</summary>
        internal static T Of<T>(MethodInfo m, string what) where T : Delegate
        {
            if (m == null) return null;
            try
            {
                var d = AccessTools.MethodDelegate<T>(m);
                Note(what, d != null, null);
                return d;
            }
            catch (Exception e) { Note(what, false, e); return null; }
        }

        /// <summary>Metoda instancyjna void z jednym parametrem na typie NIEZNANYM przy kompilacji (klasy BK): delegat typowany Action&lt;T, A&gt; zbudowany
        /// raz przez metode generyczna, opakowany rzutowaniem instancji (instancja zawsze z GetCampaignBehavior&lt;T&gt; albo __instance latki tego typu).</summary>
        internal static Action<object, A> Open<A>(MethodInfo m, string what)
        {
            if (m == null) return null;
            try
            {
                var t = m.DeclaringType;
                if (t == null || t.IsValueType || m.IsStatic) { Note(what, false, new ArgumentException("cel nie jest metoda instancyjna klasy")); return null; }
                var g = typeof(FastCall).GetMethod(nameof(Bind), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(t, typeof(A));
                var d = (Action<object, A>)g.Invoke(null, new object[] { m });
                Note(what, d != null, null);
                return d;
            }
            catch (TargetInvocationException e) { Note(what, false, e.InnerException ?? e); return null; }
            catch (Exception e) { Note(what, false, e); return null; }
        }

        private static Action<object, A> Bind<T, A>(MethodInfo m) where T : class
        {
            var d = AccessTools.MethodDelegate<Action<T, A>>(m);
            return (o, a) => d((T)o, a);
        }

        /// <summary>Do linii doby "Polka i delegaty (E8/O1)".</summary>
        internal static string Text()
        {
            int all = 0, ok = 0;
            string bad = null;
            lock (_sites)
            {
                foreach (var kv in _sites)
                {
                    all++;
                    if (kv.Value) ok++; else bad = bad == null ? kv.Key : bad + ", " + kv.Key;
                }
            }
            return "delegaty DteDelegates " + (On ? "[WL]" : "[wyl]") + ": zbudowane " + ok + "/" + all + (bad != null ? " (nieudane - dalej przez Invoke: " + bad + ")" : "")
                   + ", wywolania delegatem " + Fast + " / przez Invoke " + Slow;
        }
    }
}
