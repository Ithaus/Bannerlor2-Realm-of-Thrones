using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// 350 (raport spowolnienia 07.11, zadanie 1 - TYLKO POMIAR, gra bez zmian): doba gry na mapie rosnie w sesji (22.8 -> 36 s po ~45-60 dobach),
    /// nasz takt doby stoi, liczba klatek na dobe stoi (f9), rosnie koszt jednej klatki. Linia "Pomiar sesji (350)" raz na dobe (klucz SessionPerfLog,
    /// niezalezny od Stage8ProfileLog): klatki mapy w biegu i w pauzie, sredni i najdluzszy czas klatki, czas rzeczywisty doby, GC 0/1/2, pamiec zarzadzana
    /// i prywatna procesu. Co 30 dob sesji druga linia: czas WSZYSTKICH sluchaczy HourlyTickEvent (para prefiks First / postfiks Last na
    /// CampaignEventDispatcher.HourlyTick - w srodku nasi, BK, ROT i reszta; godzinowe tiki partii / osad / klanow i AI partii gra rozklada na klatki
    /// i tu NIE wchodza) oraz liczebnosc najwiekszych statycznych kolekcji Armoury (odbicie raz na 30 dob, tylko typy BeforeFieldInit).
    /// Klatki: postfiks MapTimeControlVM.Tick (MapSpeed.TickPostfix) - same dodawania na polach statycznych, bez alokacji.
    /// </summary>
    internal static class SessionPerf350
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.SessionPerfLog; } }

        private const int BlockDays = 30;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }
        private static string F1(double v) { return v.ToString("0.0", Inv); }
        private static string F2(double v) { return v.ToString("0.00", Inv); }

        // ---- klatki (okres = od poprzedniej doby)
        private static long _lastTs;
        private static bool _fDaily;
        private static long _runN, _runTicks, _runMax, _runMaxNoDaily, _runLong, _pauseN, _pauseTicks, _gaps;

        // ---- doba
        private static bool _hadDay;
        private static int _sessionDays, _day = -1;
        private static long _dayTs;
        private static readonly int[] _gc = new int[3];
        private static Process _proc;
        private static int _stumbles, _stumblesSession;

        // ---- blok 30 dob: sluchacze godziny (latka) i nasze z profilu
        private static bool _hookTried, _hooked;
        private static string _hookWhy = "nie zakladana (klucz SessionPerfLog wylaczony przy starcie sesji)";
        private static long _hrTicks, _hrMax, _hrCalls, _hrGc0, _blkGc0, _blkOurs;
        private static bool _blkOursOk = true;
        private static int _hrGcAt;

        // ---- blok 30 dob: kolekcje statyczne (lista pol raz na proces)
        private static FieldInfo[] _cf;
        private static MethodInfo[] _cCount;   // getter Count dla kolekcji bez ICollection (HashSet)
        private static int[] _cPrev;
        private static bool _cHavePrev;
        private static int _cSkippedTypes;

        /// <summary>Nowa gra / wczytanie (pole-inicjalizator ArmouryBehavior, wzor F9Reset): okres i licznik dob sesji od zera; pierwsza doba = baza.</summary>
        internal static void Reset()
        {
            _lastTs = 0; _fDaily = false; ClearFrames();
            _hadDay = false; _sessionDays = 0; _dayTs = 0; _day = -1; _stumbles = 0;
            ClearBlock();
            _cHavePrev = false;
        }

        private static void ClearFrames()
        {
            _runN = _runTicks = _runMax = _runMaxNoDaily = _runLong = _pauseN = _pauseTicks = _gaps = 0;
        }

        private static void ClearBlock()
        {
            _hrTicks = _hrMax = _hrCalls = _hrGc0 = _blkGc0 = _blkOurs = 0; _blkOursOk = true;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesSession++;
            if (_stumblesSession <= 3 || _stumblesSession % 500 == 0) Log.Error("SessionPerf350." + where + " (potkniecie " + _stumblesSession + ")", e);
        }

        // ------------------------------------------------------------ klatki
        /// <summary>Z MapSpeed.TickPostfix co klatke paska mapy. state = MapTimeControlVM.TimeFlowState (1-4 bieg, reszta pauza).
        /// Ta sama regula co f9: odstep ujemny albo > 5 s = przerwa (menu, bitwa, wczytanie), nie klatka.</summary>
        internal static void Frame(int state, long now)
        {
            if (!On) { _lastTs = 0; return; }
            long prev = _lastTs;
            _lastTs = now;
            bool daily = _fDaily;
            _fDaily = false;
            if (prev == 0) return;
            long d = now - prev;
            if (d < 0 || d > Stopwatch.Frequency * 5) { _gaps++; return; }
            if (state >= 1 && state <= 4)
            {
                _runN++; _runTicks += d;
                if (d > _runMax) _runMax = d;
                if (!daily && d > _runMaxNoDaily) _runMaxNoDaily = d;
                if (d > Stopwatch.Frequency / 10) _runLong++;
            }
            else { _pauseN++; _pauseTicks += d; }
        }

        // ------------------------------------------------------------ doba
        /// <summary>Poczatek ArmouryBehavior.OnDailyTick (przed f9 i E8/0 - DayProfile zeruje godzine w TickBegin): linia za okres od poprzedniej doby.</summary>
        internal static void Daily()
        {
            try
            {
                if (!On) { _hadDay = false; _sessionDays = 0; _lastTs = 0; _fDaily = false; ClearFrames(); ClearBlock(); return; }
                long t0 = Stopwatch.GetTimestamp();
                int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
                int cur = -1;
                try { cur = (int)CampaignTime.Now.ToDays; } catch { }
                if (_hadDay)
                {
                    _sessionDays++;
                    int day = _day;   // dzien okresu = doba poprzedniego taktu (jak f9 i E8/0 - ta sama liczba w linii tej samej granicy)
                    // nasze sluchacze godziny z profilu E8/0 (okres = ta sama doba; DayProfile.TickBegin zeruje go zaraz po nas)
                    if (DayProfile.On) _blkOurs += DayProfile.HourTicksPeriod(); else _blkOursOk = false;
                    _blkGc0 += g0 - _gc[0];
                    DayLine(day, t0, g0, g1, g2);
                    if (_sessionDays % BlockDays == 0) BlockLine(day);
                }
                else ClearBlock();
                _gc[0] = g0; _gc[1] = g1; _gc[2] = g2;
                _dayTs = t0; _day = cur;
                _hadDay = true;
            }
            catch (Exception e) { Stumble("Daily", e); }
            ClearFrames();
            _fDaily = true;
        }

        private static void DayLine(int day, long t0, int g0, int g1, int g2)
        {
            var sb = new StringBuilder(512);
            sb.Append("Pomiar sesji (350): dzien ").Append(day).Append(" | doba sesji ").Append(_sessionDays)
              .Append(" | czas rzeczywisty ").Append(F1(Ms(t0 - _dayTs) / 1000.0)).Append(" s");
            double avg = _runN > 0 ? Ms(_runTicks) / _runN : 0.0;
            sb.Append(" | klatki mapy: w biegu ").Append(_runN).Append(" (").Append(F1(Ms(_runTicks) / 1000.0)).Append(" s, sr. ").Append(F2(avg))
              .Append(" ms, maks. ").Append(F1(Ms(_runMax))).Append(" ms, bez taktu doby ").Append(F1(Ms(_runMaxNoDaily))).Append(" ms, >100 ms ").Append(_runLong)
              .Append("), pauza ").Append(_pauseN).Append(" (").Append(F1(Ms(_pauseTicks) / 1000.0)).Append(" s), przerwy > 5 s ").Append(_gaps);
            sb.Append(" | GC 0/1/2 +").Append(g0 - _gc[0]).Append("/+").Append(g1 - _gc[1]).Append("/+").Append(g2 - _gc[2]);
            long managed = GC.GetTotalMemory(false) / 1048576L;
            string priv = "?";
            try
            {
                if (_proc == null) _proc = Process.GetCurrentProcess(); else _proc.Refresh();
                priv = (_proc.PrivateMemorySize64 / 1048576L).ToString(Inv);
            }
            catch (Exception e) { Stumble("Process", e); }
            sb.Append(" | pamiec zarzadzana ").Append(managed.ToString(Inv)).Append(" MB, prywatna procesu ").Append(priv).Append(" MB");
            sb.Append(" | koszt linii ").Append(F2(Ms(Stopwatch.GetTimestamp() - t0))).Append(" ms | potkniecia ").Append(_stumbles).Append(" (od startu gry ").Append(_stumblesSession).Append(").");
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ blok 30 dob
        private static void BlockLine(int day)
        {
            long t0 = Stopwatch.GetTimestamp();
            var sb = new StringBuilder(1536);
            sb.Append("Pomiar sesji (350) - ").Append(BlockDays).Append(" dob: dzien ").Append(day).Append(" | doba sesji ").Append(_sessionDays);
            if (_hooked)
            {
                double tot = Ms(_hrTicks);
                sb.Append(" | sluchacze HourlyTick (wszystkie): ").Append(F1(tot)).Append(" ms, sr. ").Append(F1(tot / BlockDays)).Append(" ms/dobe, maks. godziny ")
                  .Append(F2(Ms(_hrMax))).Append(" ms, wywolan ").Append(_hrCalls).Append(", GC gen0 w srodku ").Append(_hrGc0).Append(" z ").Append(_blkGc0).Append(" w ").Append(BlockDays).Append(" dobach");
            }
            else sb.Append(" | sluchacze HourlyTick: BRAK (").Append(_hookWhy).Append(')');
            if (_blkOursOk) sb.Append(" | nasze z profilu (DayProfile.Hr, dolna granica) ").Append(F1(Ms(_blkOurs) / BlockDays)).Append(" ms/dobe");
            else sb.Append(" | nasze z profilu: brak (Stage8ProfileLog wylaczony w czesci bloku)");
            int stB = _stumbles;
            Collections(sb);
            if (_stumbles > stB) sb.Append(" [potkniecia kolekcji ").Append(_stumbles - stB).Append(']');
            sb.Append(" | koszt bloku ").Append(F1(Ms(Stopwatch.GetTimestamp() - t0))).Append(" ms | potkniecia ").Append(_stumbles).Append(" (od startu gry ").Append(_stumblesSession).Append(").");
            Log.Info(sb.ToString());
            ClearBlock();
        }

        private static void Collections(StringBuilder sb)
        {
            if (_cf == null) BuildFieldList();
            int n = _cf.Length;
            var cnt = new int[n];
            long total = 0;
            for (int i = 0; i < n; i++)
            {
                cnt[i] = -1;
                try
                {
                    object v = _cf[i].GetValue(null);
                    if (v == null) continue;
                    var c = v as ICollection;
                    if (c != null) cnt[i] = c.Count;
                    else if (_cCount[i] != null) cnt[i] = (int)_cCount[i].Invoke(v, null);
                    if (cnt[i] > 0) total += cnt[i];
                }
                catch (Exception e) { cnt[i] = -1; Stumble("Count(" + _cf[i].DeclaringType.Name + "." + _cf[i].Name + ")", e); }
            }
            sb.Append(" | kolekcje statyczne Armoury: pol ").Append(n).Append(", pominiete typy z jawnym konstruktorem statycznym ").Append(_cSkippedTypes)
              .Append(", elementow razem ").Append(total);
            var ord = new List<int>(n);
            for (int i = 0; i < n; i++) if (cnt[i] > 0) ord.Add(i);
            ord.Sort((a, b) => cnt[b].CompareTo(cnt[a]));
            sb.Append(" | najwieksze: ");
            for (int k = 0; k < ord.Count && k < 10; k++)
            {
                int i = ord[k];
                if (k > 0) sb.Append(", ");
                sb.Append(_cf[i].DeclaringType.Name).Append('.').Append(_cf[i].Name).Append(' ').Append(cnt[i]);
                if (_cHavePrev && _cPrev[i] >= 0) sb.Append(" (").Append(cnt[i] - _cPrev[i] >= 0 ? "+" : "").Append(cnt[i] - _cPrev[i]).Append(')');
            }
            if (_cHavePrev)
            {
                var gr = new List<int>(n);
                for (int i = 0; i < n; i++) if (cnt[i] >= 0 && _cPrev[i] >= 0 && cnt[i] > _cPrev[i]) gr.Add(i);
                gr.Sort((a, b) => (cnt[b] - _cPrev[b]).CompareTo(cnt[a] - _cPrev[a]));
                sb.Append(" | najszybciej rosnace: ");
                if (gr.Count == 0) sb.Append("brak");
                for (int k = 0; k < gr.Count && k < 5; k++)
                {
                    int i = gr[k];
                    if (k > 0) sb.Append(", ");
                    sb.Append(_cf[i].DeclaringType.Name).Append('.').Append(_cf[i].Name).Append(" +").Append(cnt[i] - _cPrev[i]).Append(" (").Append(cnt[i]).Append(')');
                }
            }
            else sb.Append(" | najszybciej rosnace: pierwszy blok (brak porownania)");
            _cPrev = cnt; _cHavePrev = true;
        }

        /// <summary>Raz na proces: pola statyczne kolekcji w typach Armoury. Tylko typy BeforeFieldInit (same inicjalizatory pol, bez jawnego konstruktora
        /// statycznego) - odczyt pola moze uruchomic inicjalizacje typu, ktora dotad nie biegla; jawne konstruktory statyczne pomijane (CLAUDE.md 7).
        /// Bez tablic (stale bufory), bez typow generycznych otwartych, bez pol ThreadStatic i stalych.</summary>
        private static void BuildFieldList()
        {
            var fs = new List<FieldInfo>(1024);
            var gs = new List<MethodInfo>(1024);
            Type[] types;
            try { types = typeof(SessionPerf350).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; Stumble("GetTypes", e); }
            int skipped = 0;
            for (int t = 0; t < types.Length; t++)
            {
                var ty = types[t];
                if (ty == null) continue;
                try
                {
                    if (ty.ContainsGenericParameters || ty.IsInterface || ty.IsEnum) continue;
                    var fields = ty.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    bool any = false;
                    for (int i = 0; i < fields.Length; i++)
                    {
                        var f = fields[i];
                        if (f.IsLiteral || f.FieldType.IsArray || f.FieldType.IsValueType || f.FieldType == typeof(string)) continue;
                        if (f.IsDefined(typeof(ThreadStaticAttribute), false)) continue;
                        bool coll = typeof(ICollection).IsAssignableFrom(f.FieldType);
                        MethodInfo getter = null;
                        if (!coll)
                        {
                            if (!IsGenericCollection(f.FieldType)) continue;
                            var p = f.FieldType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                            if (p == null || p.PropertyType != typeof(int)) continue;
                            getter = p.GetGetMethod();
                            if (getter == null) continue;
                        }
                        any = true;
                        if ((ty.Attributes & TypeAttributes.BeforeFieldInit) == 0 && ty.TypeInitializer != null) continue;   // jawny konstruktor statyczny - pomin typ
                        fs.Add(f); gs.Add(getter);
                    }
                    if (any && (ty.Attributes & TypeAttributes.BeforeFieldInit) == 0 && ty.TypeInitializer != null) skipped++;
                }
                catch (Exception e) { Stumble("Fields(" + ty.Name + ")", e); }
            }
            _cf = fs.ToArray(); _cCount = gs.ToArray(); _cSkippedTypes = skipped;
        }

        private static bool IsGenericCollection(Type t)
        {
            foreach (var i in t.GetInterfaces())
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>)) return true;
            return false;
        }

        // ------------------------------------------------------------ latka godziny
        /// <summary>OnSessionLaunched: para prefiks/postfiks na CampaignEventDispatcher.HourlyTick (raz na proces, tylko przy wlaczonym kluczu), linia startu.</summary>
        internal static void EnsureHooks()
        {
            try
            {
                if (Campaign.Current == null) return;
                if (On && !_hookTried) { _hookTried = true; Hook(); }
                Log.Info("Pomiar sesji (350): start | klucz SessionPerfLog " + (On ? "TAK" : "NIE") + " | sluchacze HourlyTick (CampaignEventDispatcher.HourlyTick) "
                         + (_hooked ? "wpiete" : "BRAK (" + _hookWhy + ")")
                         + " | godzinowe tiki partii, osad i klanow oraz AI partii gra rozklada na klatki - nie wchodza do sluchaczy, widac je tylko w czasie klatki.");
            }
            catch (Exception e) { Stumble("EnsureHooks", e); }
        }

        private static void Hook()
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(CampaignEventDispatcher), "HourlyTick", Type.EmptyTypes);
                if (m == null) { _hookWhy = "nie znaleziono metody"; return; }
                var h = new Harmony("com.jeff.armoury.sessionperf350");
                h.Patch(m, prefix: new HarmonyMethod(typeof(SessionPerf350), nameof(HourPre)) { priority = Priority.First },
                           postfix: new HarmonyMethod(typeof(SessionPerf350), nameof(HourPost)) { priority = Priority.Last });
                var info = Harmony.GetPatchInfo(m);
                bool mine = false;
                if (info != null)
                {
                    foreach (var p in info.Prefixes) if (p.owner == "com.jeff.armoury.sessionperf350") mine = true;
                }
                _hooked = mine;
                if (!mine) _hookWhy = "latka nie widoczna w Harmony.GetPatchInfo";
            }
            catch (Exception e) { _hookWhy = "blad: " + e.Message; Stumble("Hook", e); }
        }

        // Prefiks bez parametrow gry i bez bool - nie pomija oryginalu; 0 = stoper stoi (klucz wylaczony)
        public static void HourPre(out long __state)
        {
            __state = 0;
            if (!On) return;
            _hrGcAt = GC.CollectionCount(0);
            __state = Stopwatch.GetTimestamp();
        }

        public static void HourPost(long __state)
        {
            if (__state == 0) return;
            long d = Stopwatch.GetTimestamp() - __state;
            _hrTicks += d; _hrCalls++;
            if (d > _hrMax) _hrMax = d;
            _hrGc0 += GC.CollectionCount(0) - _hrGcAt;
        }
    }
}
