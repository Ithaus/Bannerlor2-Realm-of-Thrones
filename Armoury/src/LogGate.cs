using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// NW O4 (noc 16.10, rozpoznanie wydajnosci WGRANIA 61) - CICHE LOGI. Pomiary czysto diagnostyczne (gra bez zmian, tylko linia w logu) w grze
    /// raz na QuietLogEveryDays dob (domyslnie 7), w autotescie codziennie jak dotad. Bledy, zdarzenia, linie logiki i Profil doby - zawsze.
    /// Zestaw 1 (sprawdzony w kodzie: nikt poza wlasna klasa nie czyta stanu w logice):
    ///  - RawPrice.Daily (linia "Ceny surowcow") - w dobie bez linii tylko potkniecia doby (RawPrice.QuietDay);
    ///  - MarketRoad.Daily (linie "Dowoz" i "Dowoz (skutki)") - w dobie bez linii samo zerowanie licznikow (MarketRoad.Reset), linia mowi o 1 dobie;
    ///  - GoodsLedger: autorzy zmian w tickach osad (slady stosu) i linia "Towary (inne ticki dobowe)" - ramki i bilans ksiegi codziennie;
    ///  - TerrainEase.Hourly (sondy terenu) i Stage7Daily (linia "Kary terenu (E7/T)") - klucz Stage7MeasureLog nietkniety (pilnuje tez WorldClock7);
    ///  - ClanIncomeBook: raport "Budzet rodow (na sucho)", "Dlugi (na sucho)", "D staly (169c)" i budzet-rodow.csv - pierscien D (logika) codziennie.
    ///    Serie "od 3 dob" / K39 w raporcie na sucho licza w trybie gry kolejne raporty (co N dob), nie doby - tylko log.
    /// Doba probna S: zbieracze (Today) biegna w dobie S, linie (Lines) wychodza w takcie S+1 za dobe S - wszystkie ciche linie w tym samym takcie.
    /// Tryb (CrashScribe.Autotest.Active odbiciem - ten sam sygnal, ktory ustawia przelacznik autotest.json; FrameProfiler.Enabled = bieg -Profile):
    ///  0 gra (CrashScribe bez typu Autotest albo Active = false) - ciche; 1 autotest - pelne; 2 profil autotestu - ciche jak u gracza (decyzja
    ///  rozpoznania: profil mierzy gre jak u Jeffa); 3 nie rozpoznany (brak CrashScribe, inne pola, wyjatek) - pelne (bezpieczny kierunek).
    /// Pamiec trybu na proces dopiero w kampanii (Active ustawia OnSubModuleLoad CrashScribe, kampania biegnie zawsze po nim).
    /// </summary>
    internal static class LogGate
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

        /// <summary>Ciche logi czynne: klucz QuietLog i tryb gry albo profilu autotestu.</summary>
        internal static bool On
        {
            get
            {
                var s = Settings.Current;
                if (s == null || !s.QuietLog) return false;
                int m = Mode;
                return m == 0 || m == 2;
            }
        }

        internal static int Every
        {
            get
            {
                var s = Settings.Current;
                int n = s != null ? s.QuietLogEveryDays : 7;
                return n < 1 ? 1 : n > 28 ? 28 : n;
            }
        }

        /// <summary>Doba d z pelnymi pomiarami (ciche logi wylaczone, co dobe albo d podzielne przez N).</summary>
        internal static bool FullDay(int d)
        {
            if (!On) return true;
            int n = Every;
            return n <= 1 || d % n == 0;
        }

        private static int DayNow()
        {
            try { return (int)CampaignTime.Now.ToDays; } catch { return 0; }
        }

        /// <summary>Zbieracze w trakcie doby (latki, sluchacze godziny).</summary>
        internal static bool Today { get { return FullDay(DayNow()); } }

        /// <summary>Linie w takcie doby - za wczoraj (takt doby S+1 pisze doba S).</summary>
        internal static bool Lines { get { return FullDay(DayNow() - 1); } }

        private static int Detect()
        {
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "CrashScribe") continue;
                    const BindingFlags st = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
                    Type t = asm.GetType("CrashScribe.Autotest", false);
                    if (t == null) return 0;                    // DLL Jeffa: typu autotestu nie ma - zwykla gra
                    FieldInfo f = t.GetField("Active", st);
                    if (f == null || f.FieldType != typeof(bool)) return 3;
                    if (!(bool)f.GetValue(null)) return 0;      // DLL autotestu bez przelacznika - zwykla gra
                    Type p = asm.GetType("CrashScribe.FrameProfiler", false);
                    FieldInfo e = p != null ? p.GetField("Enabled", st) : null;
                    if (e == null || e.FieldType != typeof(bool)) return 1;
                    return (bool)e.GetValue(null) ? 2 : 1;
                }
                return 3;   // bez CrashScribe nie wiadomo, czy to autotest - pelne
            }
            catch { return 3; }
        }

        private static string ModeText()
        {
            switch (Mode)
            {
                case 0: return "gra";
                case 1: return "autotest - pelne (codziennie)";
                case 2: return "profil autotestu - ciche jak u gracza";
                default: return "nie rozpoznany (brak CrashScribe albo inne pola autotestu) - pelne (codziennie)";
            }
        }

        /// <summary>Linia startu sesji (ArmouryBehavior.OnSessionLaunched) - grupa progow nwyd sprawdza ja NAJPIERW (tryb autotestu musi byc "pelne").</summary>
        internal static void SessionLine()
        {
            var s = Settings.Current;
            bool key = s != null && s.QuietLog;
            Log.Info("Ciche logi (NW): " + (key ? "[WL] co " + Every + " dob" : "[wyl] - pomiary codziennie")
                     + " - pomiary tylko-log (Ceny surowcow, Kary terenu, Dowoz wsi zamkowych, autorzy zmian w tickach towarow, raport Budzet rodow (na sucho))"
                     + (key ? " w takcie doby " + Every + "k+1 za dobe " + Every + "k" : "")
                     + " | tryb: " + ModeText() + " | dzialaja: " + (On ? "tak" : "nie - pelne") + ".");
        }

        /// <summary>Dopisek linii "Log (E8/O6)" (takt doby, dzien = doba gry - 1).</summary>
        internal static string O6Text(int day)
        {
            var s = Settings.Current;
            bool key = s != null && s.QuietLog;
            return " | ciche logi (NW) " + (key ? "[WL]" : "[wyl]") + " tryb " + (Mode == 0 ? "gra" : Mode == 1 ? "autotest" : Mode == 2 ? "profil" : "nie rozpoznany")
                   + ", linie pomiarow tej doby " + (FullDay(day) ? "tak" : "nie");
        }
    }
}
