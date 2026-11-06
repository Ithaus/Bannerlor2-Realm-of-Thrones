using System;
using System.Collections.Generic;

namespace Armoury
{
    /// <summary>
    /// 334e - TESTY REGUL (bez kampanii, same czyste funkcje; przy starcie sesji, raz). Linie "Sila lokalna (334e): test - Tn ... - wynik TAK|NIE."
    ///  T1 progi i histereza (oblezenie nowe / biezacy cel, rabunek); T2 czas rabunku z wzoru gry (100 ludzi ~ 60 h, 1000 ~ 1 doba); T3 macierz oblezenia w toku na
    ///  przypadkach z logow 06.11 (O1, Red Keep - ODSTAP; Deep Den - SRODEK; Ashemark, Sarsfield, O2/O3 - KONTYNUUJ; przewaza, nie zdazy - KONTYNUUJ; glod);
    ///  T4 przyjazd przewagi; T5 przydzial obrony (1.2x, nikt nie wraca); T6 fantom - kotwice 2/2 (stan latki). T3 takze: sama zaloga nie wywoluje odwrotu (recenzja W3).
    /// </summary>
    internal static class LocalFront334eTest
    {
        private static bool _done;
        internal static void Reset() { _done = false; }

        private static void Say(string t, bool ok, string detail)
        {
            Log.Info(LocalFront334e.Tag + "test - " + t + " " + detail + " - wynik " + (ok ? "TAK" : "NIE") + ".");
        }

        internal static void Run()
        {
            if (_done) return;
            _done = true;
            var s = Settings.Current;
            if (s == null || !s.LogEnabled) return;
            // T1
            bool t1 = LocalFront334e.SiegeMult(0.8, false) == 1.2f && LocalFront334e.SiegeMult(0.81, false) == 0.8f && LocalFront334e.SiegeMult(1.0, false) == 0.8f
                      && LocalFront334e.SiegeMult(1.01, false) == 0f && LocalFront334e.SiegeMult(1.2, true) == 0.8f && LocalFront334e.SiegeMult(1.26, true) == 0f
                      && LocalFront334e.RaidMult(0.5) == 1.2f && LocalFront334e.RaidMult(1.25) == 0.8f && LocalFront334e.RaidMult(1.26) == 0f
                      && LocalFront334e.Ratio(0, 0) == 0 && LocalFront334e.Ratio(10, 0) == 99;
            Say("T1 progi i histereza", t1, "11 przypadkow");
            // T2
            float r100 = LocalFront334e.RaidDays(1f, 100) * 24f, r1000 = LocalFront334e.RaidDays(1f, 1000);
            bool t2 = Math.Abs(r100 - 60f) < 0.5f && Math.Abs(r1000 - 1.02f) < 0.05f;
            Say("T2 czas rabunku (wzor gry)", t2, "100 ludzi " + r100.ToString("0.0") + " h, 1000 ludzi " + r1000.ToString("0.00") + " doby");
            // T3
            double q;
            var cases = new List<string>();
            bool t3 = true;
            Action<string, double, double, double, float, float, float, int> C = (name, camp, rel, gar, arr, tl, food, want) =>
            {
                int got = LocalFront334e.JudgeRule(camp, rel, gar, arr, tl, food, out q);
                if (got != want) { t3 = false; cases.Add(name + " " + LocalFront334e.StateName[got] + "!=" + LocalFront334e.StateName[want]); }
            };
            C("O1", 3908, 6003, 0, 1.5f, 4f, 6f, 4);
            C("RedKeep", 3487, 4365, 0, 2f, 3f, 6f, 4);
            C("DeepDen", 9182, 8088, 1000, 1f, 4f, 6f, 2);
            C("Ashemark", 5000, 1500, 800, 2f, 5f, 6f, 1);
            C("Sarsfield", 6000, 0, 900, float.MaxValue, 6f, 5f, 1);
            C("O2", 4200, 2000, 600, 3f, 5f, 4f, 1);
            C("O3", 7000, 3000, 1500, 2f, 3f, 8f, 1);
            C("nie-zdazy", 3000, 6000, 300, 5f, 2f, 6f, 3);
            C("glod", 5000, 1000, 500, 2f, 4f, 1f, 5);
            C("zaloga-bez-odsieczy", 1000, 0, 1500, float.MaxValue, 4f, 6f, 2);
            C("zaloga-nie-wywoluje-odwrotu", 3000, 800, 3000, 1f, 4f, 6f, 2);   // recenzja 334e W3: zaloga liczona do odwrotu najwyzej 0.8 x oboz
            Say("T3 macierz oblezenia w toku (przypadki 06.11)", t3, "11 przypadkow" + (cases.Count > 0 ? " [" + string.Join("; ", cases.ToArray()) + "]" : ""));
            // T4
            var rel = new List<KeyValuePair<float, float>> { new KeyValuePair<float, float>(1f, 1000f), new KeyValuePair<float, float>(2f, 3000f), new KeyValuePair<float, float>(5f, 5000f) };
            float a1 = LocalFront334e.Arrival(rel, 500, 3000), a2 = LocalFront334e.Arrival(rel, 0, 10000);
            bool t4 = Math.Abs(a1 - 2f) < 0.001f && a2 == float.MaxValue;
            Say("T4 przyjazd przewagi", t4, "baza 500 oboz 3000 -> " + a1.ToString("0.0") + ", oboz 10000 -> nigdy");
            // T5
            var cand = new List<KeyValuePair<float, float>> { new KeyValuePair<float, float>(0.5f, 500f), new KeyValuePair<float, float>(1f, 800f), new KeyValuePair<float, float>(3f, 2000f) };
            double s1, s2;
            int n1 = LocalFront334e.PickDefenders(cand, 1000, 2f, out s1), n2 = LocalFront334e.PickDefenders(cand, 1000, 0.8f, out s2);
            bool t5 = n1 == 2 && Math.Abs(s1 - 1300) < 0.1 && n2 == 0;
            Say("T5 przydzial obrony", t5, "T 2 doby -> " + n1 + " (suma " + s1.ToString("0") + "), T 0.8 doby -> " + n2 + " (nikt nie wraca)");
            // T6
            Say("T6 fantom kotwice", LocalFront334e.PhantomAnchors == 2, "kotwic " + LocalFront334e.PhantomAnchors + "/2");
            // T7 (334f, L14): histereza odwrotu z mysli dowodcy (sad co ok. 3 h) - stany: 1 KONTYNUUJ, 2 SRODEK, 3 NIE-ZDAZY, 4 ODSTAP, 5 GLOD
            var t7 = new List<string>();
            Func<int[], double[], int> Seq = (states, hours) =>
            {
                int bad = 0; double bh = -1;
                for (int i = 0; i < states.Length; i++) bad = LocalFront334e.BadStep(bad, bh, states[i], hours[i], out bh);
                return bad;
            };
            if (Seq(new[] { 4, 4 }, new[] { 100.0, 103.0 }) < 2) t7.Add("ODSTAP co 3 h x2 bez odwrotu");
            if (Seq(new[] { 4, 2, 4 }, new[] { 100.0, 103.0, 106.0 }) < 2) t7.Add("ODSTAP-SRODEK-ODSTAP bez odwrotu");
            if (Seq(new[] { 4, 3, 4 }, new[] { 100.0, 103.0, 106.0 }) < 2) t7.Add("ODSTAP-NIE-ZDAZY-ODSTAP bez odwrotu");
            if (Seq(new[] { 4, 1, 4 }, new[] { 100.0, 103.0, 106.0 }) >= 2) t7.Add("KONTYNUUJ nie zeruje");
            if (Seq(new[] { 4, 4 }, new[] { 100.0, 113.0 }) >= 2) t7.Add("ODSTAP po 13 h liczony dalej");
            if (Seq(new[] { 4, 4 }, new[] { 100.0, 100.4 }) >= 2) t7.Add("ta sama godzina liczona 2 x");
            Say("T7 histereza odwrotu (2 mysli w 12 h)", t7.Count == 0, "6 przypadkow" + (t7.Count > 0 ? " [" + string.Join("; ", t7.ToArray()) + "]" : ""));
        }
    }
}
