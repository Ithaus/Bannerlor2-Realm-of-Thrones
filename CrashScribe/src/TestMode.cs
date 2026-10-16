using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace CrashScribe
{
    /// <summary>
    /// NW (noc 16.10, rozpoznanie wydajnosci WGRANIA 61): czy ten proces to autotest - ten sam sygnal, ktory ustawia przelacznik autotest.json.
    /// DLL autotestu CrashScribe = to samo drzewo + latka autotestu (Autotest.cs, FrameProfiler.cs); Autotest.Arm (OnSubModuleLoad) ustawia
    /// statyczne pole Autotest.Active = true tylko po zuzyciu pliku-przelacznika, a FrameProfiler.Enabled = klucz "profile" przelacznika (bieg -Profile).
    /// W DLL Jeffa typu CrashScribe.Autotest nie ma - to jest zwykla gra. Odczyt odbiciem w tym samym zestawie (bez odwolania w kodzie: zwykla
    /// kompilacja nie ma tych typow). Wynik:
    ///  0 gra (typu nie ma albo Active = false) - pomiary rzadziej;
    ///  1 autotest bez pomiaru klatki - pelne logi codziennie (progi tools/sprawdz_logi.py);
    ///  2 autotest z pomiarem klatki (-Profile) - jak gra (decyzja rozpoznania: profil mierzy gre jak u Jeffa);
    ///  3 nie da sie rozpoznac (typ jest, ale bez pola Active typu bool, albo wyjatek) - pelne logi (bezpieczny kierunek).
    /// Pamiec na proces dopiero w kampanii (Active nie zmienia sie po OnSubModuleLoad, a kampania zawsze biegnie po nim).
    /// </summary>
    internal static class TestMode
    {
        private static int _mode = -1;

        internal static int Mode
        {
            get
            {
                if (_mode >= 0) return _mode;
                int m = Detect();
                if (Campaign.Current != null) _mode = m;
                return m;
            }
        }

        /// <summary>Pelne (codzienne) pomiary: autotest bez pomiaru klatki albo tryb nie rozpoznany.</summary>
        internal static bool FullLogs { get { int m = Mode; return m == 1 || m == 3; } }

        internal static string Describe()
        {
            switch (Mode)
            {
                case 0: return "gra";
                case 1: return "autotest - pelne (codziennie)";
                case 2: return "profil autotestu - jak u gracza";
                default: return "nie rozpoznany - pelne (codziennie)";
            }
        }

        private static int Detect()
        {
            try
            {
                var asm = typeof(TestMode).Assembly;
                var t = asm.GetType("CrashScribe.Autotest", false);
                if (t == null) return 0;
                const BindingFlags st = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
                var f = t.GetField("Active", st);
                if (f == null || f.FieldType != typeof(bool)) return 3;
                if (!(bool)f.GetValue(null)) return 0;
                var p = asm.GetType("CrashScribe.FrameProfiler", false);
                var e = p != null ? p.GetField("Enabled", st) : null;
                if (e == null || e.FieldType != typeof(bool)) return 1;
                return (bool)e.GetValue(null) ? 2 : 1;
            }
            catch { return 3; }
        }
    }
}
