using System;
using System.Text;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P2: kolejnosc funkcji w takcie 0,25 s (po puli): kolejki (ciala, ostrzal, zdarzenia P1 / P3), zmiany rozkazow i rogi, przemowy, bebny, tlo,
    /// po bitwie, probka autotestu. Kazda funkcja we wlasnym try z licznikiem potkniec (RoutHold, miejsce "dzwiek") - wywrotka jednej nie gasi reszty.
    /// </summary>
    internal static partial class BattleSound
    {
        private static void ResetFeatures()
        {
            ResetOrders();
            ResetDrums();
            ResetAfter();
            ResetField();
            ResetTest();
            _thenQ.Clear();
            _evQ.Clear();
            _gallopQ.Clear();
        }

        private static void BeginFeatures(Mission m, Settings c)
        {
            TestBegin(m);
        }

        private static void TickFeatures(Mission m, Settings c)
        {
            Step("Ciala", () => Bodies(m, c));
            Step("Rozkazy", () => Orders(m, c));
            Step("Ostrzal", () => Volleys(m, c));
            Step("Zdarzenia", () => Events(m, c));
            Step("Tetent", () => Gallops(m));
            Step("Przemowy", () => Speeches(m, c));
            Step("Motto po przemowie", () => Thens(m));
            Step("Bebny", () => Drums(m, c));
            Step("Pogoda", () => Weather(m, c));
            Step("Konie", () => Horses(m, c));
            Step("Szczek", () => Clash(m, c, TickEvery));
            Step("Ogien", () => Fires(m, c));
            Step("Ptaki przed bitwa", () => BirdsBefore(m, c));
            Step("Po bitwie", () => After(m, c));
            Step("Probka", () => TestTick(m));
        }

        private static void Step(string name, Action a)
        {
            try { a(); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound." + name, e); }
        }

        /// <summary>Koniec zdarzenia (Pool / Cut): beben traci biezacy takt.</summary>
        private static void OnSndEnded(Snd s)
        {
            if (s.Group == GDrum) DrumEnded(s);
        }

        /// <summary>Koniec misji przed linia: aktywne bebny licza sie jako zatrzymane (start == stop).</summary>
        private static void FinishFeatures()
        {
            foreach (var kv in _drums) if (kv.Value.Active) { kv.Value.Active = false; kv.Value.Cur = null; _cDrumStop++; }
        }

        private static void ClearFeatures()
        {
            ResetFeatures();
        }

        private static void AppendFeatures(StringBuilder sb, Settings c)
        {
            AppendOrders(sb, c);
            AppendDrums(sb, c);
            AppendAfter(sb, c);
            AppendField(sb, c);
            AppendTest(sb);
        }
    }
}
