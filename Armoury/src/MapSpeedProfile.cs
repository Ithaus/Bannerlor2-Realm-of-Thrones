using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// f9-predkosc - LINIA "Predkosc mapy (f9)" RAZ NA DOBE (tylko log, przy Stage8ProfileLog - jak "Profil doby (E8/0)"; gra bez zmian). Wypisywana na
    /// poczatku ArmouryBehavior.OnDailyTick, PRZED linia profilu doby, za okres od poprzedniego taktu doby. Skladniki:
    ///  - klatki mapy wedlug biegu (pauza / x1 / szybko / bardzo szybko / inny mnoznik): czas realny, liczba klatek, srednia, klatki dluzsze niz
    ///    50 / 100 / 250 / 500 ms (odstep miedzy kolejnymi MapTimeControlVM.Tick - cala klatka gry, nie tylko nasz kod; przerwa > 5 s = misja,
    ///    inny ekran, wczytanie - nie jest klatka), w tym klatki z naszym taktem doby i z naszymi sluchaczami godziny (najdluzsza z kazdego rodzaju);
    ///  - uzycie przyciskow i klawiszy, wybrany bieg;
    ///  - godzina: rozbicie sluchacza "Kwatermistrz+projekty+MaterialOrders.Hourly" (najwiekszy w "Profil doby": 50-107 ms/dobe, maks. 53 ms w jednej
    ///    godzinie) na czesci, w tym bandy w spoczynku (AiBanditRest) z NightRest.OnHourly; koszt wszystkich sluchaczy godziny wedlug godziny doby (3 najdrozsze);
    ///  - haki klatki: NightRest.OnTick (TickEvent + klatka MapScreen), zegar paska (MapClock), przyciski (MapSpeed) - czas i liczba wywolan;
    ///  - oszczedne liczenie (MapSpeedPerf): FindType z pamieci, namioty AI (postawione / zdjete, czas, najwiecej w jednej klatce, zaleglosci),
    ///    siatka band (zapytania, probki kontroli, roznice - musi byc 0), zegar paska (odswiezenia, pominiete przy biegu szybkim).
    /// Liczniki bez blokad (watek gry). Koszt: 1 odczyt zegara na klatke paska + po 2 na hak klatki i czesc godziny.
    /// </summary>
    internal static class MapSpeedProfile
    {
        internal static bool On { get { return DayProfile.On; } }

        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }
        private static string F1(double v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        private static string F2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        // ---- klatki mapy wedlug biegu
        private const int Bins = 5;
        private static readonly string[] BinNames = { "pauza", "x1", "szybko", "bardzo szybko", "inny mnoznik" };
        private static readonly long[] _binTicks = new long[Bins], _binFrames = new long[Bins], _binMax = new long[Bins];
        private static readonly long[] _long = new long[Bins * 4];   // > 50 / 100 / 250 / 500 ms
        private static readonly long[] Thr = new long[4];
        private static long _lastTs, _gaps;
        internal static long FrameNo;
        private static bool _fDaily, _fHour;
        private static long _dailyFrames, _dailyMax, _hourFrames, _hourLong, _hourMax;
        private static int _day = -1;

        // ---- uzycie przyciskow i klawiszy (0 x1, 1 szybko, 2 bardzo szybko)
        private static readonly int[] _uses = new int[3], _keys = new int[3];

        // ---- godzina: czesci sluchacza Kwatermistrz+projekty+MaterialOrders.Hourly
        internal const int QEscrow = 0, QProjects = 1, QCaptives = 2, QNight = 3, QOrders = 4, QRest = 5, QParts = 6;
        private static readonly string[] QNames = { "eskrow kwatermistrza", "projekty kuzni", "jency (zdjecie sprzetu)", "noc (NightRest.OnHourly)", "MaterialOrders.Hourly", "lupy i meldunek" };
        private static readonly long[] _q = new long[QParts], _qMax = new long[QParts];
        private static long _qCalls, _qHourMax, _bandit, _banditMax, _banditCalls;
        private static int _qHourMaxAt = -1;
        // wszystkie sluchacze godziny (DayProfile.Hr) wedlug godziny doby
        private static readonly long[] _hod = new long[24], _hodMax = new long[24];
        private static int _hodKey = int.MinValue;
        private static long _hodAcc;

        // ---- haki klatki
        internal const int HNight = 0, HClock = 1, HButtons = 2, Hooks = 3;
        private static readonly string[] HookNames = { "NightRest.OnTick", "zegar paska", "przyciski" };
        private static readonly long[] _hTicks = new long[Hooks], _hCalls = new long[Hooks], _hMax = new long[Hooks];

        // ---- oszczedne liczenie (MapSpeedPerf)
        internal static long FtCalls, FtHits;   // QuartermasterLaw.FindType: wywolania, z pamieci (liczone takze przy wylaczonym kluczu - tylko wywolania)
        private static long _tentOn, _tentOff, _tentTicks, _tentMaxOne, _tentFrameAcc, _tentFrameMax, _tentFrameNo = -1;
        internal static long TentBacklogRuns, TentClearQueued, TentClearMax;
        internal static long GridQueries, GridSamples, GridDiff;
        private static long _clockSet, _clockSkip;

        static MapSpeedProfile()
        {
            double f = Stopwatch.Frequency / 1000.0;
            Thr[0] = (long)(50 * f); Thr[1] = (long)(100 * f); Thr[2] = (long)(250 * f); Thr[3] = (long)(500 * f);
        }

        /// <summary>Nowa gra / wczytanie (konstruktor ArmouryBehavior): okres od zera, pierwsza klatka bez pomiaru.</summary>
        internal static void Reset()
        {
            ClearPeriod();
            _lastTs = 0; _fDaily = _fHour = false; _day = -1; _hodKey = int.MinValue; _hodAcc = 0;
        }

        private static void ClearPeriod()
        {
            Array.Clear(_binTicks, 0, Bins); Array.Clear(_binFrames, 0, Bins); Array.Clear(_binMax, 0, Bins); Array.Clear(_long, 0, _long.Length);
            _gaps = 0; _dailyFrames = _dailyMax = _hourFrames = _hourLong = _hourMax = 0;
            Array.Clear(_uses, 0, 3); Array.Clear(_keys, 0, 3);
            Array.Clear(_q, 0, QParts); Array.Clear(_qMax, 0, QParts); _qCalls = 0; _qHourMax = 0; _qHourMaxAt = -1;
            _bandit = _banditMax = _banditCalls = 0;
            Array.Clear(_hod, 0, 24); Array.Clear(_hodMax, 0, 24);
            Array.Clear(_hTicks, 0, Hooks); Array.Clear(_hCalls, 0, Hooks); Array.Clear(_hMax, 0, Hooks);
            FtCalls = FtHits = 0;
            _tentOn = _tentOff = _tentTicks = _tentMaxOne = 0; _tentFrameMax = 0;
            TentBacklogRuns = TentClearQueued = TentClearMax = 0;
            GridQueries = GridSamples = GridDiff = 0;
            _clockSet = _clockSkip = 0;
        }

        // ------------------------------------------------------------ klatki
        /// <summary>Postfiks MapTimeControlVM.Tick (MapSpeed.TickPostfix): odstep od poprzedniej klatki paska do kubelka biegu (bin) z flagami taktu doby i godziny.</summary>
        internal static void Frame(int bin, long now)
        {
            FrameNo++;
            long prev = _lastTs;
            _lastTs = now;
            bool daily = _fDaily, hour = _fHour;
            _fDaily = _fHour = false;
            if (prev == 0) return;
            long d = now - prev;
            if (d > Stopwatch.Frequency * 5 || d < 0) { _gaps++; return; }
            if (bin < 0 || bin >= Bins) bin = 0;
            _binTicks[bin] += d; _binFrames[bin]++; if (d > _binMax[bin]) _binMax[bin] = d;
            for (int k = 0; k < 4; k++) if (d > Thr[k]) _long[bin * 4 + k]++;
            if (daily) { _dailyFrames++; if (d > _dailyMax) _dailyMax = d; }
            if (hour) { _hourFrames++; if (d > Thr[0]) _hourLong++; if (d > _hourMax) _hourMax = d; }
        }

        internal static void NoteDaily() { _fDaily = true; }

        internal static void Use(int which, bool key)
        {
            if (which < 0 || which > 2) return;
            if (key) _keys[which]++; else _uses[which]++;
        }

        // ------------------------------------------------------------ godzina
        /// <summary>DayProfile.Hr (finally kazdego sluchacza godziny): koszt do godziny doby; flaga "klatka z godzina".</summary>
        internal static void HourCost(int absHour, long d)
        {
            _fHour = true;
            if (absHour != _hodKey) { _hodKey = absHour; _hodAcc = 0; }
            _hodAcc += d;
            int h = ((absHour % 24) + 24) % 24;
            _hod[h] += d;
            if (_hodAcc > _hodMax[h]) _hodMax[h] = _hodAcc;
        }

        /// <summary>Sluchacz "Kwatermistrz+projekty+MaterialOrders.Hourly": znaczniki czasu miedzy czesciami (t[0] poczatek, t[QParts] koniec).</summary>
        internal static void HourSplit(long[] t)
        {
            if (t == null || t.Length < QParts + 1) return;
            _qCalls++;
            for (int i = 0; i < QParts; i++)
            {
                long d = t[i + 1] - t[i];
                if (d < 0) d = 0;
                _q[i] += d; if (d > _qMax[i]) _qMax[i] = d;
            }
            long all = t[QParts] - t[0];
            if (all > _qHourMax)
            {
                _qHourMax = all;
                try { _qHourMaxAt = CampaignTime.Now.GetHourOfDay; } catch { _qHourMaxAt = -1; }
            }
        }

        /// <summary>NightRest.AiHourly: czas AiBanditRest (bandy w spoczynku) - czesc "noc" sluchacza wyzej.</summary>
        internal static void Bandit(long d)
        {
            _banditCalls++; _bandit += d; if (d > _banditMax) _banditMax = d;
        }

        // ------------------------------------------------------------ haki klatki i oszczedne liczenie
        internal static void Hook(int k, long d)
        {
            if (k < 0 || k >= Hooks) return;
            _hTicks[k] += d; _hCalls[k]++; if (d > _hMax[k]) _hMax[k] = d;
        }

        /// <summary>NightRest.Tent dla partii AI: czas jednego postawienia / zdjecia namiotu i suma w jednej klatce paska.</summary>
        internal static void Tent(bool on, long d)
        {
            if (on) _tentOn++; else _tentOff++;
            _tentTicks += d; if (d > _tentMaxOne) _tentMaxOne = d;
            if (_tentFrameNo != FrameNo) { _tentFrameNo = FrameNo; _tentFrameAcc = 0; }
            _tentFrameAcc += d;
            if (_tentFrameAcc > _tentFrameMax) _tentFrameMax = _tentFrameAcc;
        }

        internal static void ClockSet() { _clockSet++; }
        internal static void ClockSkip() { _clockSkip++; }

        // ------------------------------------------------------------ linia
        /// <summary>Poczatek ArmouryBehavior.OnDailyTick (przed DayProfile.TickBegin): linia za poprzedni okres, zerowanie, flaga "klatka z taktem doby".</summary>
        internal static void Daily()
        {
            try
            {
                if (On && _day >= 0) Line();
            }
            catch (Exception e) { Log.Error("MapSpeedProfile.Line", e); }
            ClearPeriod();
            try { _day = (int)CampaignTime.Now.ToDays; } catch { _day = -1; }
            _fDaily = true;
        }

        private static void Line()
        {
            var s = Settings.Current;
            var sb = new StringBuilder(1536);
            sb.Append("Predkosc mapy (f9): dzien ").Append(_day);
            if (s != null)
            {
                sb.Append(" | przyciski ").Append(s.MapSpeedButtons ? "TAK" : "NIE (mnoznik z FastForwardMultiplier)")
                  .Append(" (szybko x").Append(MapSpeed.FastOf(s)).Append(", bardzo szybko x").Append(MapSpeed.VeryFastOf(s))
                  .Append(", klawisze 4/5/6 ").Append(s.MapSpeedKeys ? "TAK" : "NIE").Append("), bieg >> teraz x")
                  .Append(F1(MapSpeed.FfMultiplier(s))).Append(", uzyte przyciski x1/szybko/bardzo szybko ").Append(_uses[0]).Append('/').Append(_uses[1]).Append('/').Append(_uses[2])
                  .Append(", klawisze ").Append(_keys[0]).Append('/').Append(_keys[1]).Append('/').Append(_keys[2]);
                if (MapSpeed.Stumbles > 0) sb.Append(", potkniecia w sesji ").Append(MapSpeed.Stumbles);
            }
            // klatki
            sb.Append(" | klatki mapy wedlug biegu (s realne / klatek / sr. ms / maks. ms / >50 / >100 / >250 / >500 ms):");
            for (int b = 0; b < Bins; b++)
            {
                if (_binFrames[b] == 0 && b == 4) continue;
                double secs = Ms(_binTicks[b]) / 1000.0;
                sb.Append(b == 0 ? " " : "; ").Append(BinNames[b]).Append(' ').Append(F1(secs)).Append('/').Append(_binFrames[b]).Append('/')
                  .Append(_binFrames[b] > 0 ? F1(Ms(_binTicks[b]) / _binFrames[b]) : "-").Append('/').Append(F1(Ms(_binMax[b])))
                  .Append('/').Append(_long[b * 4]).Append('/').Append(_long[b * 4 + 1]).Append('/').Append(_long[b * 4 + 2]).Append('/').Append(_long[b * 4 + 3]);
            }
            sb.Append("; przerwy > 5 s ").Append(_gaps).Append("; klatki z naszym taktem doby ").Append(_dailyFrames).Append(" (najdluzsza ").Append(F1(Ms(_dailyMax)))
              .Append(" ms), z naszymi sluchaczami godziny ").Append(_hourFrames).Append(" (> 50 ms ").Append(_hourLong).Append(", najdluzsza ").Append(F1(Ms(_hourMax))).Append(" ms)");
            // godzina
            double qAll = 0; for (int i = 0; i < QParts; i++) qAll += Ms(_q[i]);
            sb.Append(" | godzina - Kwatermistrz+projekty+MaterialOrders.Hourly ").Append(F1(qAll)).Append(" ms/").Append(_qCalls).Append(" (maks. godziny ")
              .Append(F1(Ms(_qHourMax))).Append(" ms").Append(_qHourMaxAt >= 0 ? " o " + _qHourMaxAt + ":00" : "").Append("): ");
            for (int i = 0; i < QParts; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(QNames[i]).Append(' ').Append(F1(Ms(_q[i]))).Append('/').Append(F1(Ms(_qMax[i])));
                if (i == QNight) sb.Append(" [w tym bandy w spoczynku (AiBanditRest) ").Append(F1(Ms(_bandit))).Append('/').Append(F1(Ms(_banditMax))).Append(" ms, wywolan ").Append(_banditCalls).Append(']');
            }
            sb.Append(" (suma/maks. ms); wszystkie sluchacze godziny wedlug godziny doby (suma/maks. ms, 3 najdrozsze):");
            var ord = new int[24];
            for (int i = 0; i < 24; i++) ord[i] = i;
            Array.Sort(ord, (a, b) => { int c = _hod[b].CompareTo(_hod[a]); return c != 0 ? c : a.CompareTo(b); });
            for (int i = 0; i < 3; i++) { int h = ord[i]; sb.Append(i == 0 ? " " : ", ").Append(h).Append(":00 ").Append(F1(Ms(_hod[h]))).Append('/').Append(F1(Ms(_hodMax[h]))); }
            // haki klatki
            sb.Append(" | haki klatki (ms / wywolan / maks. ms):");
            for (int k = 0; k < Hooks; k++)
                sb.Append(k == 0 ? " " : "; ").Append(HookNames[k]).Append(' ').Append(F1(Ms(_hTicks[k]))).Append('/').Append(_hCalls[k]).Append('/').Append(F2(Ms(_hMax[k])));
            // oszczedne liczenie
            bool perf = s != null && s.MapSpeedPerf;
            sb.Append(" | oszczedne liczenie (MapSpeedPerf) ").Append(perf ? "TAK" : "NIE").Append(": FindType wywolan ").Append(FtCalls).Append(" (z pamieci ").Append(FtHits)
              .Append("); namioty AI postawione/zdjete ").Append(_tentOn).Append('/').Append(_tentOff).Append(", czas ").Append(F1(Ms(_tentTicks))).Append(" ms (najdluzszy ")
              .Append(F2(Ms(_tentMaxOne))).Append(" ms, najwiecej w jednej klatce ").Append(F1(Ms(_tentFrameMax))).Append(" ms), dobor zaleglych namiotow ").Append(TentBacklogRuns)
              .Append(" x, zdjecia o swicie w kolejce ").Append(TentClearQueued).Append(" (najwiecej naraz ").Append(TentClearMax).Append("); bandy - zapytania siatki ").Append(GridQueries)
              .Append(" (probek kontroli ").Append(GridSamples).Append(", roznic ").Append(GridDiff).Append("); zegar paska odswiezen ").Append(_clockSet).Append(", pominietych przy biegu szybkim ").Append(_clockSkip);
            Log.Info(sb.ToString());
        }
    }
}
