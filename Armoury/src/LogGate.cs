using System;
using System.Collections.Generic;
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

        // ================================================================ zapis-d4 (noc 17/18.10, CO-ZOSTALO D4): CICHE LOGI WIECEJ (klucz QuietLogMore)
        // Zestaw 2: dzienne linie pomiarow, ktorych NIE czyta zadne narzedzie progow - sprawdzone 17/18.10 na logu fin81 (Armoury-2026-10-17_17-55-14.log,
        // 497 KB/dobe): kazdy przedrostek przeszukany w tools/sprawdz_logi.py (doslownie, bez nawiasow z regexow i jako literal-przedrostek by_day/startswith)
        // i w pozostalych skryptach tools/ (Zbrojownie zalog, Skup sprzetu, Zlom z nadmiaru czyta p174*; Rada (3.5) i Majatki BK (187) - przeplywy zlota czytane przez
        // model danin tools/ekonomia-symulatory-2026-10-17/daniny - zostaja). Razem ok. 30 KB/dobe (6% logu); reszta
        // (ok. 94%) to linie czytane przez progi - bez zmian. Filtr w Log.Info PO zbudowaniu linii: kod, liczniki i zerowania biegna jak dotad, nie powstaje
        // tylko wpis w pliku. Tylko linia doby (przedrostki z ": dzien" itp.) - linie startu sesji i wczytania o tych samych nazwach zostaja. Cadencja jak
        // zestaw 1 (Lines: takt doby S+1 za dobe S, S podzielne przez QuietLogEveryDays); autotest i tryb nie rozpoznany - zawsze pelne; linie z "ERROR"
        // albo "BLAD" nigdy nie znikaja.
        // Recenzja zapis-d4: na liscie TYLKO linie pisane CODZIENNIE - filtr zna jedna doba S+1 na N dob, wiec linia z wlasna rzadsza kadencja ginie prawie zawsze:
        // "Skale ludzi wedlug krolestw (GL0)" (PeopleScale - doba % 7 == 0, a takt S+1 to doba % 7 == 1 - nie wychodzila nigdy) i "Zatkane wsie (przyklad)"
        // (VillageClogDiag co 5 dob - zgodnosc z taktem raz na 35 dob) wypadly; obie i tak rzadkie, zysk zaden. 43 przedrostki; checker nzapis ZAP-Q3 pilnuje
        // na logu pelnym, ze kazdy przedrostek listy jest dzienny.
        private static readonly string[] More =
        {
            "Kiesy rozbitych (3.2): dzien", "Wedrowcy BK - prawdziwy towar (36b): dzien", "Wedrowcy BK - towar (3.6): dzien", "Wedrowcy BK: dzien",
            "Wozy w miastach: dzien", "Zaopatrzenie zamkow (171): dzien", "Spoils - klan najemnikow: dzien",
            "Spoils - naprawa u kwatermistrza: dzien", "Spoils of War (128): dzien", "Dosypka z niczego (RealisticBannerlord): dzien", "Zold konnych (160",
            "Las wsi (126): dzien", "Skale ludzi (GL0): dzien", "Balans krolestw (175): dzien",
            "Zaopatrzenie BK - kara morale (W1): dzien", "Rzemieslnicy BK (147): dzien", "Morale AI (183d): dzien",
            "ROT szczelnosc (3.7): dzien", "Rynek surowcow: zapasy na targach miast", "Rezerwa napraw AI - pamiec dopasowania (F6): dzien",
            "Pule ochotnikow (171): dzien", "PodazPopyt: kupcy wywiezli", "Miasta bez strzal - powod (172): dzien", "Warsztaty wedlug krainy (E6/0",
            "Drobne z niczego (3.10): dzien", "Konie rekrutow (160", "Ceny hurtu (09.10", "Stajnia AI (sklad7b-p): dzien", "Wierzchowce kanonu (208): dzien",
            "Cwiczenia (171): dzien", "Kara za przegrana (D11-1): dzien", "Mineraly (dubel BK): dzien", "Papirus wedlug klimatu (T8): dzien",
            "HouseLevies: dzien", "WPLYW: stan", "Karawany (kierunek): dzien", "Ludzie (regiony): dzien", "Dezercja AI (183): dzien",
            "Zatkane wsie (wozy w drodze): dzien", "Spojnosc armii (AC wiek): dzien", "Ochotnicy (diagnoza):",
            "Musztra (gracz): dzien", "Dowoz (wozy): dzien"
        };
        private static long _moreDropped;
        // przedrostki zestawu 2 wedlug pierwszej litery - Log.Info sprawdza tylko te kilka, nie wszystkie 43 (wolane przy kazdej linii logu)
        private static readonly Dictionary<char, string[]> MoreByFirst = BuildMoreByFirst();

        private static Dictionary<char, string[]> BuildMoreByFirst()
        {
            var tmp = new Dictionary<char, List<string>>();
            foreach (var p in More)
            {
                List<string> l;
                if (!tmp.TryGetValue(p[0], out l)) tmp[p[0]] = l = new List<string>();
                l.Add(p);
            }
            var d = new Dictionary<char, string[]>();
            foreach (var kv in tmp) d[kv.Key] = kv.Value.ToArray();
            return d;
        }

        internal static int MoreCount { get { return More.Length; } }
        internal static long MoreDropped { get { return _moreDropped; } }

        /// <summary>Start sesji (SaveSize.SessionLine): licznik pominietych linii od zera.</summary>
        internal static void ResetMore() { _moreDropped = 0; }

        /// <summary>Zestaw 2 czynny: klucz QuietLogMore i ciche logi czynne (QuietLog + tryb gry albo profilu).</summary>
        internal static bool MoreActive
        {
            get
            {
                var s = Settings.Current;
                return s != null && s.QuietLogMore && Campaign.Current != null && On;
            }
        }

        /// <summary>Z Log.Info: true = linia zestawu 2 poza doba pomiaru (nie trafia do pliku). Najpierw przedrostek (tanie; linie startu gry przed kampania
        /// nigdy nie pasuja - bez wykrywania trybu), potem klucz, tryb i doba. Wyjatek = linia zostaje.</summary>
        internal static bool DropMore(string m)
        {
            try
            {
                if (string.IsNullOrEmpty(m)) return false;
                string[] cand;
                if (!MoreByFirst.TryGetValue(m[0], out cand)) return false;
                bool hit = false;
                for (int i = 0; i < cand.Length; i++) if (m.StartsWith(cand[i], StringComparison.Ordinal)) { hit = true; break; }
                if (!hit) return false;
                if (m.IndexOf("ERROR", StringComparison.Ordinal) >= 0 || m.IndexOf("BLAD", StringComparison.Ordinal) >= 0) return false;
                if (!MoreActive || Lines) return false;
                _moreDropped++;
                return true;
            }
            catch { return false; }
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
