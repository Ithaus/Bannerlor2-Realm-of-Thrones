using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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

        /// <summary>E8/0: czas zapisu (ticki zegara) i zapisow od poprzedniego odczytu; zeruje.</summary>
        internal static void TakeWrite(out long ticks, out long lines)
        {
            lock (Gate) { ticks = _wTicks; lines = _wLines; _wTicks = 0; _wLines = 0; }
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
            try
            {
                string topic = TopicOf(msg);
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
                _wTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; _wLines++;
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
            try { lock (Gate) { CloseAllLocked(); } } catch { }
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
        private static string TopicOf(string m)
        {
            if (m == null) return null;
            if (m.StartsWith("Kronika unikatow") || m.StartsWith("UniqueSpoils")) return "unikaty";
            if (m.StartsWith("Bitwa:")) return "bitwy";
            if (m.StartsWith("Balans krolestw (175) wedlug krolestw")) return "balans";   // 175.0: szczegoly wedlug krolestw (krotka linia dnia zostaje w glownym)
            if (m.StartsWith("Budowa:")) return "budowy";
            if (m.StartsWith("Finanse:")) return "finanse";
            if (m.StartsWith("ZakupyAI: dzien") || m.StartsWith("PodazPopyt: kupcy")) return null;
            if (m.StartsWith("ZakupyAI:") || m.StartsWith("Stajnia AI:") || m.StartsWith("Oferta:")) return "zakupy";
            if (m.StartsWith("PodazPopyt:")) return "handel";
            if (m.StartsWith("Warsztaty (diagnoza)")) return "warsztaty";
            if (m.StartsWith("AiNightCamp")) return "noc";
            if (m.StartsWith("Wioski: ogien") || m.StartsWith("Wioski: zgaszony") || m.StartsWith("Wioski: drzewo")) return "wioski";   // W2: kazdy rabunek i drzewa wsi-matek (diagnostyka) - do pliku tematycznego; start, wypelnienie i podsumowania w glownym
            if (m.StartsWith("UniqueLaw: zamiennik") || m.StartsWith("LegendaryLaw: zamiennik") || m.StartsWith("TroopFit:   ")
                || m.StartsWith("Uniques: ") || m.StartsWith("Rozrzut miotanych:") || m.StartsWith("Podloga zlomu:")) return "start";
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
            lock (Gate) { Append(_path, text, null); }
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
