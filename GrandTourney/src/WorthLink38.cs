using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GrandTourney
{
    /// <summary>
    /// KROK 3.8 (Armoury FixedSums38): przedzial skromnej nagrody lokalnej (400 - LocalPrizeMaxValue) to KLASA przedmiotu w starej monecie,
    /// nie cena. Ceny historyczne Armoury zmieniaja wartosc uzbrojenia srednio 21 razy (miecz t5 ok. 100 d, plyta t6 ok. 2 500 d), wiec
    /// w 400-2000 zostawaly prawie same ciezkie zbroje. Pytamy Armoury o wartosc sprzed przeliczenia (Armoury.FixedSums38.Worth, refleksja jak
    /// TownWageLink - Armoury nie jest zaleznoscia GrandTourney; wylacznik po stronie Armoury: Hist Worth Windows Old Coin). Bez Armoury - Value.
    /// </summary>
    internal static class WorthLink38
    {
        private static Func<ItemObject, int> _worth;
        private static bool _tried, _fail;

        internal static int Worth(ItemObject it)
        {
            if (it == null) return 0;
            Resolve();
            if (_worth == null) return it.Value;
            try { return _worth(it); }
            catch (Exception e) { if (!_fail) { _fail = true; Log.Error("WorthLink38", e); } return it.Value; }
        }

        private static void Resolve()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                Type t = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.GetName().Name != "Armoury") continue;
                    t = a.GetType("Armoury.FixedSums38", false);
                    if (t != null) break;
                }
                var m = t != null ? t.GetMethod("Worth", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(ItemObject) }, null) : null;
                if (m != null) _worth = (Func<ItemObject, int>)Delegate.CreateDelegate(typeof(Func<ItemObject, int>), m);
                Log.Info("WorthLink38: wartosc nagrody lokalnej z Armoury - " + (_worth != null ? "PODPIETA (Armoury.FixedSums38.Worth - sprzed cen historycznych)" : "BRAK (zwykla wartosc)"));
            }
            catch (Exception e) { Log.Error("WorthLink38.Resolve", e); }
        }
    }
}
