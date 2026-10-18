using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using TaleWorlds.Library;

namespace Armoury
{
    internal static class Log
    {
        private static string _path, _topicDir;
        private static readonly object Gate = new object();
        private const int KeepLogs = 12;      // ile ostatnich sesji trzymamy
        // E8/0 (242): laczny czas zapisu linii (Info, Csv, Error) i liczba zapisow od ostatniej linii profilu - pod Gate (tylko pomiar)
        private static long _wTicks, _wLines;

        // E8/O6 (270, PLAN 8.17; RD8 O5 bez utraty linii; krytyka K2-5): JEDEN slownik pisarzy (plik -> otwarty StreamWriter) pod Gate dla WSZYSTKICH
        // drog zapisu - Info (glowny plik i pliki tematyczne), Error i Csv ida przez wspolny rdzen Append. Dotad kazda linia otwierala i zamykala plik
        // (File.AppendAllText, ok. 0.11 ms na zapis, ok. 700 zapisow na dobe = 70-90 ms). Gdyby otwarty byl tylko glowny plik Info, File.AppendAllText
        // w Log.Error dostawalby blad wspoldzielenia polykany przez catch - linie ERROR znikalyby bez sladu. Pisarz: FileMode.Append, AutoFlush (kazda
        // linia od razu u systemu - przy CTD nic nie ginie, jak dotad), FileShare.ReadWrite (log mozna czytac w trakcie gry), UTF-8 bez BOM
        // (te same bajty co File.AppendAllText). Klucz LogKeepOpen czytany przy kazdym zapisie: wylaczony = te same wywolania File.* co przed paczka
        // (otwarci pisarze sa najpierw zamykani - stara droga potrzebuje wolnego pliku). Blad jednego pisarza = potkniecie: pisarz zamkniety, ta linia
        // idzie stara droga, nastepna otworzy plik od nowa (zadnego wylacznika na sesje). Zamkniecie: Init (nowa sesja) i wyjscie z gry (Close).
        private static readonly Dictionary<string, StreamWriter> Writers = new Dictionary<string, StreamWriter>(StringComparer.OrdinalIgnoreCase);   // sciezki Windows bez rozrozniania wielkosci liter: jeden plik = jeden pisarz
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private const int WriterChars = 4096;
        private static long _kOpened, _kClosed, _kOpenFails, _kWriteFails, _nKept, _nOld, _nLost;   // od startu sesji (Init)
        private static long _dKept, _dOld;                                                          // od poprzedniej linii doby
        private static string _kFirst;                                                              // pierwsze potkniecie pisarza (do linii doby)

        // proba 3 dob (O6-2, regula R9 "te same liczby"): od startu sesji do trzeciego taktu doby liczone sa bajty wyslane do kazdego pliku (z rozmiarem
        // pliku sprzed pierwszego zapisu) i linie wyslane do glownego pliku; w takcie doby rozmiar kazdego pliku bierzemy od systemu plikow (bez otwierania
        // pliku - pierwsze otwarcie do odczytu po zmianie kosztuje ok. 3 ms na plik) i porownujemy z wyslanym; liczbe linii glownego pliku porownuje
        // narzedzie (numer linii w pliku wobec liczby z linii doby). Po trzeciej kontroli liczenie gasnie.
        private const int SampleDays = 3;
        private const string ControlMark = "Log (E8/O6) wpis kontrolny drogi bledu";
        private static readonly Dictionary<string, long> Sent = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);   // plik -> oczekiwany rozmiar w bajtach
        private static bool _sampling;
        private static int _sampleDone, _controlTried;
        private static long _gapSum, _gapFilesSum, _mainLines;

        /// <summary>E8/0: czas zapisu (ticki zegara) i zapisow od poprzedniego odczytu; zeruje. f13-doba: liczniki atomowe (Append pod Gate dodaje Interlocked) -
        /// odczyt bez Gate, zeby watek gry nie czekal na porcje pisana w tle; te same liczby co pod blokada.</summary>
        internal static void TakeWrite(out long ticks, out long lines)
        {
            ticks = Interlocked.Exchange(ref _wTicks, 0L); lines = Interlocked.Exchange(ref _wLines, 0L);
        }

        // ================================================================ f13-doba (klucz DayTickSmooth): ZAPIS W TLE
        // Na watku wolajacego zostaje to, co czyta stan (bramka LogEnabled, LogGate.DropMore - doba gry, tryb), znacznik czasu DateTime.Now i kultura watku;
        // reszta (TopicOf, napis "[HH:mm:ss] ...", Path.Combine, zapis pliku tym samym rdzeniem Append pod Gate) robi watek "Armoury log" w kolejnosci kolejki.
        // Kazde oproznienie kolejki (watek tla albo watek gry) zdejmuje porcje i pisze ja POD Gate - wiec linie trafiaja do pliku w kolejnosci wywolan
        // takze wtedy, gdy watek gry oproznia kolejke sam: Log.Error (blad w pliku od razu, przed ewentualnym CTD), linia "Log (E8/O6)" (proba bajtow
        // i linii po komplecie zapisow), wyjscie z gry (Close), Init, wylaczenie klucza w trakcie gry, wyjscie procesu i nieobsluzony wyjatek. Watek tla nie
        // dotyka obiektow gry (dostaje gotowe napisy, znacznik czasu i kulture). Potkniecia watku liczone (start, petla), bez wylacznika: po nieudanym starcie
        // linie pisze watek wolajacy, nowa proba co 256 linii. Ryzyko: przy CTD silnika (bez kodu zarzadzanego) linie jeszcze w kolejce gina (zwykle ulamek ms).
        private struct QItem
        {
            public bool Csv;          // false = Info: A tekst, B glowny plik, C katalog tematow; true = Csv: A plik, B wiersze, C naglowek
            public string A, B, C;
            public DateTime T;        // znacznik czasu z chwili wywolania
            public CultureInfo Ci;    // kultura watku wolajacego: format HH:mm:ss i porownania TopicOf jak u niego (te same bajty co dotad)
            public bool InTick;       // linia z taktu doby (tylko pomiar)
        }
        private static readonly object QLock = new object(), StartLock = new object();
        private static List<QItem> _q = new List<QItem>(512), _qSpare = new List<QItem>(512);
        private static int _qPending;                 // w kolejce albo w porcji w trakcie zapisu (pod QLock; odczyt Volatile)
        private static bool _qWaiting;
        private static Thread _qThread;
        private static int _qRetryIn;
        private const int QHardCap = 20000;           // kolejka dluzsza (watek tla stoi) - wolajacy oproznia ja sam (pamiec ograniczona)
        private static bool _qExitHooked;
        // pomiar (linia "Doba gladko (f13)"): od poprzedniego odczytu TakeSmooth; pod Gate (zapis) / QLock (kolejka) / Interlocked (watek gry)
        private static long _sEnq, _sEnqTicks, _sEnqTick, _sEnqTickTicks, _sMax;
        private static long _sBgItems, _sBgTicks, _sBgTickItems, _sBgTickTicks, _sFgItems, _sFgTicks, _sFgDrains;
        private static long _sStartFails, _sLoopFails, _sRestarts, _sCap;   // od startu procesu
        private static string _sFirst;
        /// <summary>f13-doba: takt doby w toku (ustawia DayProfile przy wlaczonym kluczu) - linie oznaczone do pomiaru "w takcie".</summary>
        internal static volatile bool TickWindow;

        private static bool Smooth { get { var s = Settings.Current; return s != null && s.DayTickSmooth; } }

        internal struct SmoothStats
        {
            public long Enq, EnqTick, Max, BgItems, BgTickItems, FgItems, FgDrains, StartFails, LoopFails, Restarts, Cap;
            public double EnqMs, EnqTickMs, BgMs, BgTickMs, FgMs;
            public bool Alive; public string First;
        }

        /// <summary>f13-doba: liczniki zapisu w tle od poprzedniego odczytu (zeruje okresowe; potkniecia od startu procesu). Tylko pomiar.</summary>
        internal static SmoothStats TakeSmooth()
        {
            var r = new SmoothStats();
            double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            r.Enq = Interlocked.Exchange(ref _sEnq, 0L); r.EnqMs = Interlocked.Exchange(ref _sEnqTicks, 0L) * f;
            r.EnqTick = Interlocked.Exchange(ref _sEnqTick, 0L); r.EnqTickMs = Interlocked.Exchange(ref _sEnqTickTicks, 0L) * f;
            lock (QLock) { r.Max = _sMax; _sMax = 0; }
            lock (Gate)
            {
                r.BgItems = _sBgItems; r.BgMs = _sBgTicks * f; r.BgTickItems = _sBgTickItems; r.BgTickMs = _sBgTickTicks * f;
                r.FgItems = _sFgItems; r.FgMs = _sFgTicks * f; r.FgDrains = _sFgDrains;
                _sBgItems = _sBgTicks = _sBgTickItems = _sBgTickTicks = _sFgItems = _sFgTicks = _sFgDrains = 0;
            }
            r.StartFails = Interlocked.Read(ref _sStartFails); r.LoopFails = Interlocked.Read(ref _sLoopFails); r.Restarts = Interlocked.Read(ref _sRestarts);
            r.Cap = Interlocked.Read(ref _sCap); r.First = _sFirst;
            var t = _qThread; r.Alive = t != null && t.IsAlive;
            return r;
        }

        private static void NoteQ(string what, Exception e)
        {
            if (_sFirst != null) return;
            try
            {
                string m = what + " - " + (e != null ? e.GetType().Name + ": " + e.Message : "?");
                _sFirst = (m.Length > 200 ? m.Substring(0, 200) : m).Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
            }
            catch { _sFirst = what; }
        }

        /// <summary>f13-doba: linia (Info) albo wiersze CSV do kolejki - na watku wolajacego tylko znacznik czasu, kultura i dopisanie do listy.</summary>
        private static void Enqueue(bool csv, string a, string b, string c)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            bool tick = TickWindow;
            var it = new QItem { Csv = csv, A = a, B = b, C = c, InTick = tick };
            if (!csv) { it.T = DateTime.Now; it.Ci = CultureInfo.CurrentCulture; }
            int n;
            lock (QLock)
            {
                _q.Add(it); _qPending++;
                n = _q.Count;
                if (n > _sMax) _sMax = n;
                if (_qWaiting) Monitor.Pulse(QLock);
            }
            if (n > QHardCap) { Interlocked.Increment(ref _sCap); DrainNow(); }
            else EnsureWorker();
            long d = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            Interlocked.Increment(ref _sEnq); Interlocked.Add(ref _sEnqTicks, d);
            if (tick) { Interlocked.Increment(ref _sEnqTick); Interlocked.Add(ref _sEnqTickTicks, d); }
        }

        private static void EnsureWorker()
        {
            var t = _qThread;
            if (t != null && t.IsAlive) return;
            bool sync = false;
            lock (StartLock)
            {
                t = _qThread;
                if (t != null && t.IsAlive) return;
                if (_qRetryIn > 0) { _qRetryIn--; sync = true; }
                else
                {
                    try
                    {
                        if (!_qExitHooked)
                        {
                            _qExitHooked = true;
                            // wyjscie procesu zabija watki tla - linie z kolejki najpierw do pliku (wyjatek nieobsluzony tez; CTD silnika - nie)
                            try { AppDomain.CurrentDomain.ProcessExit += delegate { DrainNow(); }; } catch { }
                            try { AppDomain.CurrentDomain.UnhandledException += delegate { DrainNow(); }; } catch { }
                        }
                        // nazwe "Armoury log" sprawdza CrashScribe GameState.Describe: blad IO tego watku (FirstChance -> Scribe.Report) nie czyta stanu gry poza watkiem gry
                        var nt = new Thread(QLoop) { IsBackground = true, Name = "Armoury log" };
                        nt.Start();
                        if (t != null) Interlocked.Increment(ref _sRestarts);
                        _qThread = nt;
                    }
                    catch (Exception e) { Interlocked.Increment(ref _sStartFails); NoteQ("start watku zapisu", e); _qRetryIn = 256; sync = true; }
                }
            }
            if (sync) DrainNow();   // bez watku tla - ta linia (i reszta kolejki) pisana tu, jak przed paczka
        }

        private static void QLoop()
        {
            while (true)
            {
                try
                {
                    lock (QLock)
                    {
                        while (_q.Count == 0) { _qWaiting = true; Monitor.Wait(QLock); _qWaiting = false; }
                    }
                    lock (Gate) { DrainLocked(true); }
                }
                catch (ThreadAbortException) { throw; }
                catch (Exception e)
                {
                    Interlocked.Increment(ref _sLoopFails); NoteQ("petla watku zapisu", e);
                    try { Thread.Sleep(5); } catch { }
                }
            }
        }

        /// <summary>f13-doba: oproznienie kolejki na watku wolajacego (pod Gate - po porcji, ktora pisze watek tla). Nie rzuca.</summary>
        private static void DrainNow()
        {
            try { lock (Gate) { DrainLocked(false); } } catch { }
        }

        /// <summary>f13-doba: wolane POD Gate. Porcje zdejmowane z GLOWY kolejki tylko pod Gate i pisane w calosci przed zwolnieniem Gate - kolejnosc w pliku =
        /// kolejnosc wywolan. Watek tla bierze najwyzej QBatch linii na jedno wejscie pod Gate (watek gry czeka na Gate najwyzej jedna porcje); watek gry - wszystko.</summary>
        private static void DrainLocked(bool worker)
        {
            if (!worker && Volatile.Read(ref _qPending) <= 0) return;
            if (!worker) _sFgDrains++;
            while (true)
            {
                List<QItem> b;
                lock (QLock)
                {
                    if (_q.Count == 0) return;
                    if (!worker || _q.Count <= QBatch) { b = _q; _q = _qSpare ?? new List<QItem>(512); _qSpare = null; }
                    else
                    {
                        b = _qSpare ?? new List<QItem>(512); _qSpare = null;
                        for (int i = 0; i < QBatch; i++) b.Add(_q[i]);
                        _q.RemoveRange(0, QBatch);
                    }
                }
                try
                {
                    for (int i = 0; i < b.Count; i++) Process(b[i], worker);
                }
                finally
                {
                    int n = b.Count;
                    b.Clear();
                    lock (QLock) { if (_qSpare == null) _qSpare = b; _qPending -= n; }
                }
                if (worker) return;   // watek tla: jedna porcja na wejscie pod Gate (QLoop wraca po nastepna bez czekania)
            }
        }
        private const int QBatch = 256;

        /// <summary>f13-doba: jedna pozycja kolejki - ten sam napis i ten sam rdzen zapisu co Info / Csv przed paczka (pod Gate). Nie rzuca.</summary>
        private static void Process(QItem it, bool worker)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (it.Csv) Append(it.A, it.B, it.C ?? "");
                else
                {
                    string topic = TopicOf(it.A, it.Ci);
                    string file = topic != null && it.C != null ? Path.Combine(it.C, topic + ".log") : it.B;
                    string text = "[" + it.T.ToString("HH:mm:ss", it.Ci) + "] " + it.A + Environment.NewLine;
                    Append(file, text, null);
                }
            }
            catch { }   // jak catch Info / Csv przed paczka: zapis stracony policzony w Append (_nLost)
            long d = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            if (worker)
            {
                _sBgItems++; _sBgTicks += d;
                if (it.InTick) { _sBgTickItems++; _sBgTickTicks += d; }
            }
            else { _sFgItems++; _sFgTicks += d; }
        }

        /// <summary>
        /// PLIK NA SESJE (Jeff 13.09: "logi gina, zanim zdazysz o bledzie powiedziec").
        /// Init robil File.WriteAllText na STALEJ nazwie, wiec kazde odpalenie gry
        /// kasowalo dowody z poprzedniej sesji. 13.09 przepadl przez to caly zapis
        /// niewoli, w ktorej Jeff stracil rynsztunek - uratowalo nas tylko to, ze
        /// audyt przeczytal plik przed jego restartem. Teraz jak w CrashScribe:
        /// osobny plik ze znacznikiem czasu, stare kasujemy dopiero powyzej KeepLogs.
        /// </summary>
        internal static void Init(string moduleDir)
        {
            // E8/O6: nowa sesja = pisarze poprzedniej zamknieci, liczniki i proba od zera
            try
            {
                lock (Gate)
                {
                    DrainLocked(false);   // f13-doba: linie poprzedniej sesji z kolejki najpierw (pelne sciezki - do starych plikow)
                    CloseAllLocked();
                    _kOpened = _kClosed = _kOpenFails = _kWriteFails = _nKept = _nOld = _nLost = _dKept = _dOld = 0; _kFirst = null;
                    Sent.Clear(); _sampling = true; _sampleDone = 0; _controlTried = 0; _gapSum = 0; _gapFilesSum = 0; _mainLines = 0;
                }
            }
            catch { }
            try
            {
                _path = Path.Combine(moduleDir, "Armoury-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".log");
                File.WriteAllText(_path, "=== Armoury " + DateTime.Now + " ===" + Environment.NewLine);
                _mainLines = 1;   // E8/O6: naglowek to pierwsza linia glownego pliku (proba liczy linie wyslane)
                // stary plik o stalej nazwie nigdy juz nie dostanie ani linii - znika,
                // zeby nikt (ani Jeff, ani Claude) nie czytal za rok zamrozonych bzdur
                try { var legacy = Path.Combine(moduleDir, "Armoury.log"); if (File.Exists(legacy)) File.Delete(legacy); } catch { }
                Prune(moduleDir);
                // wpis 63 (Jeff 04.10: "ten log sie robi tak dlugi, ze za duzo tam jest smieci - trzeba to uporzadkowac"):
                // szczegoly ida do osobnych plikow tematycznych w Logs/<sesja>/, glowny log = start, bledy, podsumowania dnia
                try
                {
                    var root = Path.Combine(moduleDir, "Logs");
                    _topicDir = Path.Combine(root, Path.GetFileNameWithoutExtension(_path).Replace("Armoury-", ""));
                    Directory.CreateDirectory(_topicDir);
                    var dirs = new List<DirectoryInfo>();
                    foreach (var d in Directory.GetDirectories(root)) dirs.Add(new DirectoryInfo(d));
                    dirs.Sort(delegate (DirectoryInfo a, DirectoryInfo b) { return b.CreationTimeUtc.CompareTo(a.CreationTimeUtc); });
                    for (int i = KeepLogs; i < dirs.Count; i++) { try { dirs[i].Delete(true); } catch { } }
                }
                catch { _topicDir = null; }
            }
            catch { _path = null; }
        }

        /// <summary>Zostawia KeepLogs najnowszych logow sesji, reszte kasuje.</summary>
        private static void Prune(string dir)
        {
            try
            {
                var files = new List<FileInfo>();
                foreach (var f in Directory.GetFiles(dir, "Armoury-*.log")) files.Add(new FileInfo(f));
                files.Sort(delegate (FileInfo a, FileInfo b) { return b.CreationTimeUtc.CompareTo(a.CreationTimeUtc); });
                for (int i = KeepLogs; i < files.Count; i++) { try { files[i].Delete(); } catch { } }
            }
            catch { }
        }

        internal static void Info(string msg)
        {
            if (_path == null || !Settings.Current.LogEnabled) return;
            if (LogGate.DropMore(msg)) return;   // zapis-d4: ciche logi wiecej (QuietLogMore) - dzienna linia pomiaru tylko-log poza doba pomiaru w grze
            if (Smooth) { try { Enqueue(false, msg, _path, _topicDir); } catch { } return; }   // f13-doba: plik pisze watek tla
            if (Volatile.Read(ref _qPending) > 0) DrainNow();   // f13-doba: klucz wylaczony w trakcie gry - linie z kolejki najpierw (kolejnosc w pliku)
            try
            {
                string topic = TopicOf(msg, CultureInfo.CurrentCulture);
                string file = topic != null && _topicDir != null ? Path.Combine(_topicDir, topic + ".log") : _path;
                string text = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine;
                lock (Gate) { Append(file, text, null); }
            }
            catch { }
        }

        /// <summary>
        /// Plik CSV w katalogu sesji (Logs/&lt;sesja&gt;/nazwa): naglowek przy pierwszym zapisie, potem dopisywanie wierszy.
        /// Zwraca sciezke pliku albo null (log wylaczony, brak katalogu sesji, blad zapisu). Wiersze koncza sie znakiem nowej linii.
        /// </summary>
        internal static string Csv(string name, string header, string rows)
        {
            if (_topicDir == null || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(rows)) return null;
            try
            {
                if (!Settings.Current.LogEnabled) return null;
                string file = Path.Combine(_topicDir, name);
                // f13-doba: wiersze do kolejki (ta sama sciezka zwracana od razu - Path.Combine; blad zapisu w tle liczony jako zapis stracony w linii O6)
                if (Smooth) { Enqueue(true, file, rows, header ?? ""); return file; }
                if (Volatile.Read(ref _qPending) > 0) DrainNow();
                lock (Gate) { Append(file, rows, header ?? ""); }
                return file;
            }
            catch { return null; }
        }

        /// <summary>
        /// E8/O6 (270): wspolny rdzen zapisu wszystkich drog (Info, Error, Csv) - wolany pod Gate. csvHeader != null = plik CSV (naglowek, gdy pliku
        /// jeszcze nie ma). Klucz LogKeepOpen wlaczony: otwarty pisarz ze slownika; wylaczony albo potkniecie pisarza: stara droga (File.AppendAllText).
        /// Wyjatek starej drogi wychodzi do wolajacego (jego catch, jak dotad) - liczony jako zapis stracony. Stoper E8/0 obejmuje sam zapis.
        /// </summary>
        private static void Append(string file, string text, string csvHeader)
        {
            bool sample = _sampling;
            if (sample && !Sent.ContainsKey(file)) { long b0 = SizeOnDisk(file, -1); Sent[file] = b0 > 0 ? b0 : 0; }   // proba: rozmiar pliku przed pierwszym zapisem tej sesji
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            bool done = false, header = false, owed = false;
            try
            {
                var s = Settings.Current;
                if (s != null && s.LogKeepOpen)
                {
                    if (WriteKept(file, text, csvHeader, ref header, ref owed)) { done = true; _nKept++; _dKept++; return; }
                }
                else if (Writers.Count > 0) CloseAllLocked();   // klucz wylaczony w trakcie gry: stara droga potrzebuje wolnych plikow
                // stara droga (te same wywolania co przed paczka; "owed" = pisarz utworzyl plik CSV, ale naglowka nie zapisal)
                if (csvHeader != null)
                {
                    if (!File.Exists(file)) { header = true; File.WriteAllText(file, csvHeader + Environment.NewLine); }
                    else if (owed) File.AppendAllText(file, csvHeader + Environment.NewLine);
                }
                File.AppendAllText(file, text);
                done = true; _nOld++; _dOld++;
            }
            finally
            {
                Interlocked.Add(ref _wTicks, System.Diagnostics.Stopwatch.GetTimestamp() - t0); Interlocked.Increment(ref _wLines);   // f13-doba: atomowo (TakeWrite bez Gate)
                if (!done) _nLost++;
                if (sample)
                {
                    long n;
                    Sent.TryGetValue(file, out n);
                    Sent[file] = n + Utf8NoBom.GetByteCount(text) + (header ? Utf8NoBom.GetByteCount(csvHeader) + Environment.NewLine.Length : 0);
                    if (file == _path) _mainLines += CountNl(text);
                }
            }
        }

        /// <summary>E8/O6: zapis otwartym pisarzem; false = potkniecie (policzone, pisarz zamkniety) - wolajacy pisze stara droga.</summary>
        private static bool WriteKept(string file, string text, string csvHeader, ref bool header, ref bool owed)
        {
            StreamWriter w;
            if (!Writers.TryGetValue(file, out w))
            {
                FileStream fs = null;
                try
                {
                    bool fresh = csvHeader != null && !File.Exists(file);
                    fs = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    w = new StreamWriter(fs, Utf8NoBom, WriterChars);
                    w.AutoFlush = true;
                    Writers[file] = w; _kOpened++;
                    if (fresh) { header = true; owed = true; }
                }
                catch (Exception e)
                {
                    _kOpenFails++; NoteStumble("otwarcie", file, e);
                    if (fs != null) { try { fs.Dispose(); } catch { } }
                    return false;
                }
            }
            try
            {
                if (owed) w.Write(csvHeader + Environment.NewLine + text); else w.Write(text);
                owed = false;
                return true;
            }
            catch (Exception e)
            {
                _kWriteFails++; NoteStumble("zapis", file, e);
                Writers.Remove(file);
                try { w.Dispose(); } catch { }
                _kClosed++;
                return false;
            }
        }

        private static void NoteStumble(string what, string file, Exception e)
        {
            if (_kFirst != null) return;
            try
            {
                string m = what + " " + Path.GetFileName(file) + " - " + e.GetType().Name + ": " + e.Message;
                _kFirst = (m.Length > 200 ? m.Substring(0, 200) : m).Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
            }
            catch { _kFirst = what; }
        }

        private static void CloseAllLocked()
        {
            if (Writers.Count == 0) return;
            foreach (var kv in Writers) { try { kv.Value.Dispose(); } catch { } _kClosed++; }
            Writers.Clear();
        }

        /// <summary>E8/O6: pisarze zamykani przy wyjsciu z gry (SubModuleMain.OnSubModuleUnloaded). Kazda linia jest juz w pliku (flush po linii);
        /// linia zapisana pozniej otworzy plik od nowa.</summary>
        internal static void Close()
        {
            try { lock (Gate) { DrainLocked(false); CloseAllLocked(); } } catch { }   // f13-doba: kolejka najpierw
        }

        private static int CountNl(string s)
        {
            int n = 0;
            if (s != null) for (int i = s.IndexOf('\n'); i >= 0; i = s.IndexOf('\n', i + 1)) n++;
            return n;
        }

        /// <summary>Rozmiar pliku wedlug systemu plikow (bez otwierania pliku; brak pliku = 0). Gdy rozni sie od oczekiwanego, drugi odczyt osobnym
        /// uchwytem tylko do odczytu (FileShare.ReadWrite - tak samo czyta log ktos z zewnatrz w trakcie gry). Blad odczytu = -1.</summary>
        private static long SizeOnDisk(string file, long expected)
        {
            try
            {
                var fi = new FileInfo(file);
                if (!fi.Exists) return 0L;
                long n = fi.Length;
                if (expected >= 0 && n != expected)
                {
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) n = fs.Length;
                }
                return n;
            }
            catch { return -1L; }
        }

        /// <summary>
        /// E8/O6 (270): linia doby "Log (E8/O6)" - stan klucza, otwarci pisarze, zapisy pisarzem / stara droga, zapisy stracone i potkniecia pisarza
        /// od startu sesji. W pierwszych trzech taktach doby sesji proba (O6-2): bajty wyslane do kazdego pliku wobec rozmiaru pliku i liczba linii
        /// wyslanych do glownego pliku (z ta linia - narzedzie porownuje ja z numerem linii w pliku); przed proba jeden wpis kontrolny droga Log.Error
        /// (O6-3) - bez slow "ERROR in", zeby progi "0 bledow" go nie liczyly. Linia pisana pod Gate razem z liczeniem. Tylko log, gra bez zmian.
        /// </summary>
        internal static void DailyO6(int day)
        {
            var s = Settings.Current;
            if (_path == null || s == null || !s.LogEnabled) return;
            bool sampleNow; int k;
            lock (Gate) { sampleNow = _sampling; k = _sampleDone + 1; if (sampleNow) _controlTried++; }
            if (sampleNow)
            {
                try { ErrorText(ControlMark + " " + k + "/" + SampleDays + " (to nie blad: sprawdza, czy droga Log.Error trafia do pliku)"); }
                catch { }
            }
            lock (Gate)
            {
                DrainLocked(false);   // f13-doba: komplet zapisow przed liczeniem (pisarze, zapisy doby, proba bajtow i linii) - liczby jak przy zapisie synchronicznym
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(640);
                sb.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ")
                  .Append("Log (E8/O6): dzien ").Append(day).Append(" | LogKeepOpen ").Append(s.LogKeepOpen ? "[WL]" : "[wyl]")
                  .Append(" | pisarze otwarci ").Append(Writers.Count).Append(" (od startu sesji otwarc ").Append(_kOpened).Append(", zamkniec ").Append(_kClosed).Append(')')
                  .Append(" | zapisow w dobie ").Append(_dKept + _dOld).Append(": pisarzem ").Append(_dKept).Append(", stara droga ").Append(_dOld)
                  .Append(" | od startu sesji: zapisow ").Append(_nKept + _nOld + _nLost).Append(", straconych ").Append(_nLost)
                  .Append(", potkniecia pisarza ").Append(_kOpenFails + _kWriteFails).Append(" (otwarcie ").Append(_kOpenFails).Append(", zapis ").Append(_kWriteFails).Append(')');
                if (_kFirst != null) sb.Append(" [pierwsze: ").Append(_kFirst).Append(']');
                try { sb.Append(LogGate.O6Text(day)); } catch { }   // NW O4: ciche logi - tryb i czy ta doba ma linie pomiarow (przed proba - narzedzie czyta koniec linii proby)
                _dKept = 0; _dOld = 0;
                if (_sampling)
                {
                    long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    long sent = 0, disk = 0, gap = 0, worstGap = 0; int files = 0, gapFiles = 0; string worst = null;
                    foreach (var kv in Sent)
                    {
                        files++;
                        long n = SizeOnDisk(kv.Key, kv.Value);
                        sent += kv.Value; if (n > 0) disk += n;
                        long g = n < 0 ? kv.Value : (kv.Value > n ? kv.Value - n : n - kv.Value);
                        if (g > 0 || n < 0)
                        {
                            gap += g; gapFiles++;
                            if (worst == null || g > worstGap) { worstGap = g; worst = Path.GetFileName(kv.Key) + " wyslane " + kv.Value + " / w pliku " + (n < 0 ? "blad odczytu" : n.ToString(inv)); }
                        }
                    }
                    _sampleDone++; _gapSum += gap; _gapFilesSum += gapFiles;
                    double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    sb.Append(" | proba ").Append(_sampleDone).Append('/').Append(SampleDays).Append(": plikow ").Append(files).Append(", bajtow wyslanych ").Append(sent)
                      .Append(", w plikach ").Append(disk).Append(", rozjazd ").Append(gap).Append(" (plikow z rozjazdem ").Append(gapFiles)
                      .Append("), glowny plik linii wyslanych ").Append(_mainLines + 1).Append(" (z ta linia), sprawdzenie ").Append(ms.ToString("0.00", inv))
                      .Append(" ms, wpisow kontrolnych drogi bledu ").Append(_controlTried);
                    if (worst != null) sb.Append(" [najwiekszy rozjazd: ").Append(worst).Append(']');
                }
                else
                {
                    sb.Append(" | proba ").Append(_sampleDone).Append('/').Append(SampleDays).Append(" zakonczona: rozjazd lacznie ").Append(_gapSum)
                      .Append(" (plikow z rozjazdem lacznie ").Append(_gapFilesSum).Append("), wpisow kontrolnych drogi bledu ").Append(_controlTried);
                }
                sb.Append(Environment.NewLine);
                // linia doby idzie do glownego pliku tym samym rdzeniem co Info, pod ta sama blokada co liczenie (liczba "z ta linia" zgadza sie z plikiem)
                try { Append(_path, sb.ToString(), null); }
                finally { if (_sampling && _sampleDone >= SampleDays) { _sampling = false; Sent.Clear(); } }
            }
        }

        /// <summary>Plik tematyczny dla szczegolowej linii (null = glowny log). Podsumowania dnia zostaja w glownym.</summary>
        /// <summary>f13-doba: ci = kultura watku wolajacego (porownania StartsWith(x, false, ci) = StartsWith(x) na tym watku - te same tematy na watku tla).</summary>
        private static string TopicOf(string m, CultureInfo ci)
        {
            if (m == null) return null;
            if (m.StartsWith("Kronika unikatow", false, ci) || m.StartsWith("UniqueSpoils", false, ci)) return "unikaty";
            if (m.StartsWith("Bitwa:", false, ci)) return "bitwy";
            if (m.StartsWith("Balans krolestw (175) wedlug krolestw", false, ci)) return "balans";   // 175.0: szczegoly wedlug krolestw (krotka linia dnia zostaje w glownym)
            if (m.StartsWith("Budowa:", false, ci)) return "budowy";
            if (m.StartsWith("Finanse:", false, ci)) return "finanse";
            if (m.StartsWith("ZakupyAI: dzien", false, ci) || m.StartsWith("PodazPopyt: kupcy", false, ci)) return null;
            if (m.StartsWith("ZakupyAI:", false, ci) || m.StartsWith("Stajnia AI:", false, ci) || m.StartsWith("Oferta:", false, ci)) return "zakupy";
            if (m.StartsWith("PodazPopyt:", false, ci)) return "handel";
            if (m.StartsWith("Warsztaty (diagnoza)", false, ci)) return "warsztaty";
            if (m.StartsWith("AiNightCamp", false, ci)) return "noc";
            if (m.StartsWith("Wioski: ogien", false, ci) || m.StartsWith("Wioski: zgaszony", false, ci) || m.StartsWith("Wioski: drzewo", false, ci)) return "wioski";   // W2: kazdy rabunek i drzewa wsi-matek (diagnostyka) - do pliku tematycznego; start, wypelnienie i podsumowania w glownym
            if (m.StartsWith("UniqueLaw: zamiennik", false, ci) || m.StartsWith("LegendaryLaw: zamiennik", false, ci) || m.StartsWith("TroopFit:   ", false, ci)
                || m.StartsWith("Uniques: ", false, ci) || m.StartsWith("Rozrzut miotanych:", false, ci) || m.StartsWith("Podloga zlomu:", false, ci)) return "start";
            return null;
        }

        internal static void Error(string where, Exception e)
        {
            if (_path == null) return;
            try { ErrorText("ERROR in " + where + ": " + e); }
            catch { }
        }

        /// <summary>E8/O6: droga bledu - plik glowny, bez bramki LogEnabled; ten sam rdzen i ten sam pisarz co Info. Wyjatek wychodzi do wolajacego.</summary>
        private static void ErrorText(string body)
        {
            string text = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + body + Environment.NewLine;
            lock (Gate) { DrainLocked(false); Append(_path, text, null); }   // f13-doba: kolejka najpierw - blad w pliku od razu i po liniach sprzed niego
        }

        internal static void Player(string text, bool bad = false)
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(text,
                    bad ? Colors.Red : new Color(0.9f, 0.8f, 0.4f)));
            }
            catch { }
        }
    }
}
