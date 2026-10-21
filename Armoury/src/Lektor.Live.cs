using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// LK - LEKTOR, PLOTKI NA ZYWO (m103-lektor, Jeff 21.10: "zostaje przy Starterze, ale zapisuj kazda nagrana plotke, aby potem, jak jest cos nagrane, David nie czytal").
    /// Rumours.ShowPage (okno strony plotek, przed ShowInquiry) -> GossipPage: tekst strony na kawalki - naglowek (tylko strona 1; bez nawiasow z liczbami: cena kufla,
    /// "(3 more)" - tekst staly, nagrany raz wystarcza; zdania osobno) i kazda plotka ("- " zdjete), tekst Empty. Bez tytulu okna i przyciskow.
    ///  - Klucz pamieci kawalka: SHA1(glos|model|ustawienia|tekst po zlaczeniu bialych znakow) -> Documents/Mount and Blade II Bannerlord/Armoury/Lektor/cache/<klucz>.wav,
    ///    indeks cache/index.tsv (klucz, czas, znaki, tekst, md5 - tylko dopisywany). Plik w pamieci i dobry (md5 z indeksu; bez wpisu - naglowek, rozmiar, bloki zer)
    ///    -> gra od razu, zero sieci. Plik uszkodzony (dysk C zeruje pliki) - usuniety i traktowany jak brak.
    ///  - Brak w pamieci: zapytanie POST https://api.elevenlabs.io/v1/text-to-speech/BNgbHR0DNeZixGQVzloa?output_format=pcm_24000 (model eleven_multilingual_v2,
    ///    ustawienia Davida jak nagrania okien) w JEDNYM watku w tle, jedno naraz; stoper calosci 20 s do odpowiedzi serwera (System.Threading.Timer -> Abort;
    ///    Timeout HttpWebRequest liczy osobno GetRequestStream, GetResponse i kazdy Read, a DNS jest poza nim), po HTTP 200 (znaki juz policzone) pobieranie nie jest
    ///    ciete po 20 s - tylko zastoj odczytu 20 s albo twardy limit 60 s od startu; odpowiedz (surowy PCM 24 kHz mono 16 bit) + naglowek RIFF 44 B
    ///    jak tools/lektor/nagraj_david.py -> zapis ATOMOWY (plik .tmp, Flush(true), Move, kontrola md5) -> wynik przez kolejke do glownego watku (AppTick) ->
    ///    odtworzenie, jesli ta strona plotek dalej otwarta. Glowny watek nigdy nie czeka na siec ani na zapis plikow (ksiega i indeks pod _fileLock, ktorego glowny
    ///    watek nie bierze; _wlock - tylko liczniki).
    ///  - Warunki zapytania: klucz LektorGossipLive, tryb gry (LogGate 0) albo plik Documents/.../CrashScribe/armoury-lektor-live.flag (autotest 1/2 i tryb nie
    ///    rozpoznany 3 - zero sieci), plik klucza %USERPROFILE%/.elevenlabs/key.txt (czytany w chwili zapytania, nigdy nie wypisywany ani kopiowany), limit dzienny
    ///    LektorGossipDailyChars (doba kalendarzowa komputera, ksiega Lektor/usage.tsv), stan konta z GET /v1/user/subscription raz na sesje (zostaje mniej niz
    ///    LektorGossipReserveChars - nic nowego; stanu nie da sie odczytac - nic nowego, bezpieczny kierunek dla planu Starter). Tekst OPLACONY (ElevenLabs oddal
    ///    nagranie - HTTP 200, takze gdy pobieranie przerwano, odpowiedz byla zla, zapis sie nie udal albo plik zniszczyl dysk) nigdy drugi raz nie idzie do API;
    ///    tekst bez nagrania (blad serwera, brak odpowiedzi w 20 s) - najwyzej 2 wyslania w calej historii. Wpis "wyslane" w ksiedze PRZED wyslaniem - znaki
    ///    liczone takze, gdy gra padnie w trakcie; zapytanie, ktore nie wyszlo z komputera (brak DNS, brak polaczenia, TLS przed HTTP) - wpis "niewyslane": znaki
    ///    i proba zwrocone, nowe nagrania wstrzymane na 10 min (potem stan konta od nowa).
    ///    HTTP 401/402/403/429 (zly klucz, brak srodkow, limit) - zapytania wstrzymane do konca sesji (odpowiedz serwera, nie wyjatek obiektu).
    ///  - Koniec: "Hear more" (nowa strona zastepuje stara), "Enough talk" (akcja okna -> GossipStop), straznik w AppTick (zadne okno zapytania nie jest otwarte
    ///    przez 0,3 s). Prosby jeszcze niewyslane dla zamknietej strony sa porzucane (bez kosztu).
    /// Komunikat dla gracza raz na sesje: limit dzienny wyczerpany. Linie "Lektor (m103): ...", liczniki w linii doby (Lektor.cs).
    /// </summary>
    internal static partial class Lektor
    {
        private const string VoiceId = "BNgbHR0DNeZixGQVzloa";
        private const string ModelId = "eleven_multilingual_v2";
        private const string VoiceSettingsJson = "{\"stability\":0.5,\"similarity_boost\":0.78,\"style\":0.15,\"use_speaker_boost\":true,\"speed\":0.95}";
        private const string TtsUrl = "https://api.elevenlabs.io/v1/text-to-speech/" + VoiceId + "?output_format=pcm_24000";
        private const string SubUrl = "https://api.elevenlabs.io/v1/user/subscription";
        private const string LiveFlagName = "armoury-lektor-live.flag";
        private const int TimeoutMs = 20000, SubTimeoutMs = 10000, PaidCapMs = 60000;
        private const int MaxAttempts = 2;   // wyslania BEZ nagrania (blad serwera, brak odpowiedzi); tekst oplacony (HTTP 200) - nigdy drugi raz (_paid)
        private const int CodeSave = -1, CodeThread = -2, CodeNotSent = -3, CodePaidLost = -4;   // kody bledow bez HTTP w linii doby
        private const int PcmRate = 24000, PcmBytesPerSec = 48000;
        private const int MinPcmBytes = 9600, MaxPcmBytes = 48000 * 120;   // 0,2 s .. 120 s
        private const double GapSec = 0.35, NoInquiryGrace = 0.3, SubRetrySec = 600;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        // ------------------------------------------------------------ sciezki (raz)
        private static string _lkDir, _cacheDir, _keyPath, _flagPath;
        private static string LkDir { get { if (_lkDir == null) _lkDir = Path.Combine(Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord"), "Armoury"), "Lektor"); return _lkDir; } }
        private static string CacheDir { get { if (_cacheDir == null) _cacheDir = Path.Combine(LkDir, "cache"); return _cacheDir; } }
        private static string IndexPath { get { return Path.Combine(CacheDir, "index.tsv"); } }
        private static string UsagePath { get { return Path.Combine(LkDir, "usage.tsv"); } }
        private static string KeyPath { get { if (_keyPath == null) _keyPath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".elevenlabs"), "key.txt"); return _keyPath; } }
        private static string FlagPath { get { if (_flagPath == null) _flagPath = Path.Combine(Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord"), "CrashScribe"), LiveFlagName); return _flagPath; } }

        // ------------------------------------------------------------ strona plotek (glowny watek)
        private const int SPending = 0, SReady = 1, SSkip = 2, SPlayed = 3;
        private sealed class Seg { internal string Text, Key, Path; internal int Chars, State; internal double Sec; }
        private static List<Seg> _segs;
        private static int _segIdx;
        private static int _pageGen;           // Interlocked (glowny) / Volatile.Read (watek) - prosba z innej strony jest porzucana
        private static bool _pageActive;
        private static string _pagePlace;
        private static double _gapUntil, _noInqSince = -1, _pageT0;

        // ------------------------------------------------------------ pamiec nagran (glowny watek czyta, watek dopisuje - pod _cacheLock)
        private sealed class CacheEntry { internal string Md5; internal int Chars; }
        private static readonly object _cacheLock = new object();
        private static readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        private static bool _cacheLoaded;
        private static int _cacheFiles, _cacheZeroLines, _cacheBadLines, _cacheTmpRemoved;

        // ------------------------------------------------------------ ksiega zapytan i konto (watek; glowny watek czyta pod _wlock - same liczniki, bez plikow)
        private static readonly object _wlock = new object();
        private static readonly object _fileLock = new object();   // dopisywanie ksiegi i indeksu (watek nagran) - glowny watek go nie bierze (Flush(true) nie stoi na klatce)
        private static string _usageDay = "";
        private static int _usageToday, _usageSentAll, _usageZeroLines;
        private static readonly Dictionary<string, int> _attempts = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> _paid = new HashSet<string>(StringComparer.Ordinal);   // teksty oplacone w ElevenLabs (indeks, "ok", "oplacone") - nigdy drugi raz
        private static int _subState;          // 0 nie pytano, 1 znany, 2 blad chwilowy (ponowienie po SubRetrySec), 3 blad trwaly (do konca sesji)
        private static long _subLeft, _subLimit, _subUsed;
        private static string _subWhy = "nie pytano", _subWhen = "-";
        private static double _subRetryAt;
        private static string _apiStopWhy;     // HTTP 401/402/403/429 - zapytania wstrzymane do konca sesji
        private static bool _tls;

        // ------------------------------------------------------------ watek nagran
        private sealed class Job { internal int Gen, Idx, Chars; internal string Key, Text; }
        private const int RReady = 1, RSkip = 2, RInfo = 3;
        // powody bez glosu: 1 limit dzienny, 2 rezerwa planu, 3 proby wyczerpane, 4 blad API / zapisu, 5 strona zamknieta, 6 brak klucza, 7 API wstrzymane, 8 stan konta nieznany
        private sealed class Res { internal int Kind, Gen, Idx, Chars, Code, Reason; internal string Key, Path, Line; internal double Sec; internal bool Sent, New; }
        private static readonly ConcurrentQueue<Job> _jobs = new ConcurrentQueue<Job>();
        private static readonly ConcurrentQueue<Res> _results = new ConcurrentQueue<Res>();
        private static readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private static Thread _worker;
        private static int _wStumbles;         // Interlocked

        // ------------------------------------------------------------ liczniki (doba gry, glowny watek)
        private static int _dPages, _dSegs, _dHit, _dPlayed, _dNew, _dNewChars, _dSkipLimit, _dSkipReserve, _dSkipOff, _dSkipAttempts, _dSkipErr, _dDropped,
                           _dSkipNoKey, _dSkipStopped, _dSkipSub, _dCorrupt, _dRequests, _dApiErr, _dGossip05No, _dGossipEarly;
        private static readonly SortedDictionary<int, int> _dCodes = new SortedDictionary<int, int>();
        private static int _sRequests, _sNew;
        // m105-kronika K0 (A2): plotki Kroniki Westeros bez lektora na zywo (KRONIKA-FORMAT 5: "bez lektora na zywo") - teksty po Norm, glowny watek; nagranie z pamieci gra
        private static readonly HashSet<string> _noLive = new HashSet<string>(StringComparer.Ordinal);
        private static int _dSkipStory;
        private static bool _limitMsg, _logged05;
        private static int _liveFlag = -1;

        private static int DailyMax() { var s = Settings.Current; int n = s != null ? s.LektorGossipDailyChars : 1000; return n < 0 ? 0 : n > 10000 ? 10000 : n; }
        private static int ReserveChars() { var s = Settings.Current; int n = s != null ? s.LektorGossipReserveChars : 3000; return n < 0 ? 0 : n > 30000 ? 30000 : n; }
        private static string K8(string key) { return key != null && key.Length >= 8 ? key.Substring(0, 8) : (key ?? "-"); }
        private static string Today() { return DateTime.Now.ToString("yyyy-MM-dd", Inv); }
        private static string Clock() { return DateTime.Now.ToString("HH:mm:ss", Inv); }

        // ------------------------------------------------------------ tekst strony -> kawalki
        private static readonly Regex Ws = new Regex(@"\s+", RegexOptions.CultureInvariant);
        private static readonly Regex NumParen = new Regex(@"\s*\([^()]*\d[^()]*\)", RegexOptions.CultureInvariant);
        private static readonly Regex SentenceEnd = new Regex(@"(?<=[.!?])\s+(?=[A-Z""])", RegexOptions.CultureInvariant);

        private static string Norm(string s) { return string.IsNullOrEmpty(s) ? "" : Ws.Replace(s, " ").Trim(); }

        private static List<string> SplitPage(string text, int page)
        {
            var res = new List<string>();
            string[] paras = text.Replace("\r", "").Split(new[] { "\n\n" }, StringSplitOptions.None);
            for (int i = 0; i < paras.Length; i++)
            {
                string p = Norm(paras[i]);
                if (p.Length == 0) continue;
                if (p.StartsWith("- ", StringComparison.Ordinal))
                {
                    p = Norm(p.Substring(2));
                    if (p.Length > 0) res.Add(p);
                    continue;
                }
                if (i == 0 && page == 0)
                {
                    // naglowek okna: bez nawiasow z liczbami (cena kufla "(12 d)", "(3 more)") - ekran je pokazuje, a tekst bez nich jest staly; zdania osobno
                    p = Norm(NumParen.Replace(p, ""));
                    foreach (string sn in SentenceEnd.Split(p)) { string q = Norm(sn); if (q.Length > 0) res.Add(q); }
                    continue;
                }
                res.Add(p);
            }
            return res;
        }

        private static string KeyOf(string text)
        {
            using (var sha = SHA1.Create())
                return Hex(sha.ComputeHash(Utf8.GetBytes(VoiceId + "|" + ModelId + "|" + VoiceSettingsJson + "|" + text)));
        }

        // ------------------------------------------------------------ pamiec nagran i ksiega (raz na proces)
        private static void EnsureCache()
        {
            if (_cacheLoaded) return;
            _cacheLoaded = true;
            try
            {
                if (File.Exists(IndexPath))
                {
                    foreach (string raw in File.ReadAllLines(IndexPath, Utf8))
                    {
                        string line = raw.TrimEnd('\r');
                        if (line.Length == 0 || line[0] == '#') continue;
                        if (line.IndexOf('\0') >= 0) { _cacheZeroLines++; continue; }
                        string[] c = line.Split('\t');
                        int chars;
                        if (c.Length < 5 || c[0].Length != 40 || c[4].Trim().Length != 32 || !int.TryParse(c[2], NumberStyles.Integer, Inv, out chars)) { _cacheBadLines++; continue; }
                        lock (_cacheLock) _cache[c[0]] = new CacheEntry { Md5 = c[4].Trim().ToLowerInvariant(), Chars = chars };
                        lock (_wlock) _paid.Add(c[0]);   // w indeksie = nagranie przyszlo = oplacone
                    }
                }
            }
            catch (Exception e) { Stumble("EnsureCache.Index", e); }
            try
            {
                if (Directory.Exists(CacheDir))
                {
                    _cacheFiles = Directory.GetFiles(CacheDir, "*.wav").Length;
                    foreach (string t in Directory.GetFiles(CacheDir, "*.tmp")) { try { File.Delete(t); _cacheTmpRemoved++; } catch { } }   // zapis przerwany (gra padla) - nigdy nie byl w pamieci
                }
            }
            catch (Exception e) { Stumble("EnsureCache.Dir", e); }
            try
            {
                // raz na proces, zanim powstanie watek nagran (GossipPage startuje go po EnsureCache) - odczyt pliku bez _fileLock
                lock (_wlock)
                {
                    _usageDay = Today();
                    _usageToday = 0;
                    if (File.Exists(UsagePath))
                    {
                        foreach (string raw in File.ReadAllLines(UsagePath, Utf8))
                        {
                            string line = raw.TrimEnd('\r');
                            if (line.Length == 0 || line[0] == '#') continue;
                            if (line.IndexOf('\0') >= 0) { _usageZeroLines++; continue; }
                            string[] c = line.Split('\t');   // data, czas, znaki, klucz, stan
                            int chars, a;
                            if (c.Length < 5 || !int.TryParse(c[2], NumberStyles.Integer, Inv, out chars)) continue;
                            string st = c[4];
                            if (st == "wyslane")
                            {
                                _usageSentAll++;
                                _attempts.TryGetValue(c[3], out a); _attempts[c[3]] = a + 1;
                                if (c[0] == _usageDay) _usageToday += chars;
                            }
                            else if (st == "niewyslane")   // zapytanie nie wyszlo z komputera - zwrot wpisu "wyslane" (data = doba tamtego wpisu)
                            {
                                if (_usageSentAll > 0) _usageSentAll--;
                                _attempts.TryGetValue(c[3], out a); _attempts[c[3]] = a > 0 ? a - 1 : 0;
                                if (c[0] == _usageDay) _usageToday = Math.Max(0, _usageToday - chars);
                            }
                            else if (st == "ok" || st.StartsWith("oplacone", StringComparison.Ordinal)) _paid.Add(c[3]);
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("EnsureCache.Usage", e); }
        }

        /// <summary>Plik pamieci dobry -> sciezka; uszkodzony - usuniety (traktowany jak brak). Glowny watek albo watek nagran.</summary>
        private static bool Cached(string key, out string path, out double sec, bool fromWorker)
        {
            path = null; sec = 0;
            CacheEntry ce;
            lock (_cacheLock) _cache.TryGetValue(key, out ce);
            string p = Path.Combine(CacheDir, key + ".wav");
            if (!File.Exists(p)) return false;
            string why = CheckWav(p, ce != null ? ce.Md5 : null, out sec);
            if (why == null) { path = p; return true; }
            try { File.Delete(p); } catch { }
            lock (_cacheLock) _cache.Remove(key);
            lock (_wlock) _paid.Add(key);   // plik byl = nagranie przyszlo = oplacone (takze gdy linie indeksu i ksiegi zjadl dysk)
            string msg = "pamiec plotek: plik " + K8(key) + " uszkodzony (" + why + ") - usuniety; tekst oplacony, drugi raz nie idzie do ElevenLabs (glos wroci z plikiem z kopii na D:).";
            if (fromWorker) _results.Enqueue(new Res { Kind = RInfo, Reason = -1, Line = msg });
            else { _dCorrupt++; Log.Info(P + msg); }
            return false;
        }

        private static bool KeyFileExists()
        {
            try { var fi = new FileInfo(KeyPath); return fi.Exists && fi.Length > 0; } catch { return false; }
        }

        /// <summary>Klucz API w chwili zapytania (watek nagran). Nigdy nie trafia do logu, ustawien ani plikow. null - brak, pusty albo znaki spoza naglowka HTTP.</summary>
        private static string ReadKey()
        {
            try
            {
                if (!File.Exists(KeyPath)) return null;
                string k = File.ReadAllText(KeyPath).Trim();
                if (k.Length == 0 || k.Length > 256) return null;
                foreach (char ch in k) if (ch < 0x21 || ch > 0x7E) return null;
                return k;
            }
            catch { return null; }
        }

        private static bool LiveFlag()
        {
            if (_liveFlag < 0) { try { _liveFlag = File.Exists(FlagPath) ? 1 : 0; } catch { _liveFlag = 0; } }
            return _liveFlag == 1;
        }

        /// <summary>Powod, dla ktorego nowych plotek nie wysylamy (null = wolno); reason - licznik w linii doby.</summary>
        private static string LiveBlock(out int reason)
        {
            reason = 0;
            var s = Settings.Current;
            if (s == null || !s.LektorEnabled) { reason = 9; return "lektor wylaczony"; }
            if (!s.LektorGossipLive) { reason = 9; return "klucz Narrator Gossip Live NIE"; }
            int m = LogGate.Mode;
            if (m != 0 && !LiveFlag()) { reason = 9; return (m == 3 ? "tryb nie rozpoznany" : "autotest") + " - API WYLACZONE (bez pliku " + LiveFlagName + ")"; }
            lock (_wlock)
            {
                if (_apiStopWhy != null) { reason = 7; return "API wstrzymane do konca sesji: " + _apiStopWhy; }
                if (_subState == 3) { reason = 8; return "stan konta nieznany (" + _subWhy + ")"; }
                if (_usageDay != Today()) { _usageDay = Today(); _usageToday = 0; }
                if (_usageToday >= DailyMax()) { reason = 1; return "limit dzienny " + _usageToday + "/" + DailyMax() + " znakow"; }
            }
            if (!KeyFileExists()) { reason = 6; return "brak pliku klucza .elevenlabs/key.txt"; }
            return null;
        }

        // ------------------------------------------------------------ strona plotek (Rumours.ShowPage)
        /// <summary>m105-kronika K0 (A2): tekst plotki (Rumours, plotka Kroniki z noLive) nie idzie do API na zywo - kawalek bez glosu, gdy nie ma go w pamieci nagran.
        /// Glowny watek (Rumours.Wars przy budowie strony). Najwyzej 400 tekstow (potem od zera). Nie rzuca.</summary>
        internal static void MarkNoLive(string text)
        {
            try
            {
                string n = Norm(text);
                if (n.Length == 0) return;
                if (_noLive.Count >= 400) _noLive.Clear();
                _noLive.Add(n);
            }
            catch { }
        }

        /// <summary>Rumours.ShowPage tuz przed ShowInquiry (glowny watek): kawalki strony, pamiec, prosby do watku, start pierwszego gotowego. Nie rzuca.</summary>
        internal static void GossipPage(string place, string text, int page)
        {
            try
            {
                if (!On) return;
                GossipStopAll("nowa strona");
                if (string.IsNullOrEmpty(text)) return;
                EnsureCache();
                var parts = SplitPage(text, page);
                if (parts.Count == 0) return;
                int gen = Interlocked.Increment(ref _pageGen);
                var segs = new List<Seg>(parts.Count);
                string block = null;
                bool known = false;
                int reason = 0, hit = 0, send = 0, skip = 0, chars = 0, sendChars = 0, story = 0;
                foreach (string t in parts)
                {
                    var sg = new Seg { Text = t, Chars = t.Length, Key = KeyOf(t) };
                    chars += sg.Chars;
                    string path; double sec;
                    if (Cached(sg.Key, out path, out sec, false)) { sg.Path = path; sg.Sec = sec; sg.State = SReady; hit++; }
                    else if (_noLive.Contains(t)) { sg.State = SSkip; story++; }   // m105-kronika: plotka Kroniki - bez lektora na zywo
                    else
                    {
                        if (!known) { block = LiveBlock(out reason); known = true; }
                        if (block != null) { sg.State = SSkip; skip++; }
                        else
                        {
                            sg.State = SPending; send++; sendChars += sg.Chars;
                            _jobs.Enqueue(new Job { Gen = gen, Idx = segs.Count, Key = sg.Key, Text = t, Chars = sg.Chars });
                        }
                    }
                    segs.Add(sg);
                }
                _segs = segs; _segIdx = 0; _pageActive = true; _pagePlace = place; _gapUntil = 0; _noInqSince = -1; _pageT0 = Now;
                _dPages++; _dSegs += segs.Count; _dHit += hit;
                if (skip > 0) CountSkip(reason, skip);
                _dSkipStory += story;
                if (send > 0) { EnsureWorker(); _wake.Set(); }
                Log.Info(P + "plotki w " + place + ", strona " + (page + 1).ToString(Inv) + ": kawalkow " + segs.Count.ToString(Inv) + " (" + chars.ToString(Inv) + " znakow) - z pamieci "
                         + hit.ToString(Inv) + ", do nagrania " + send.ToString(Inv) + " (" + sendChars.ToString(Inv) + " znakow)"
                         + (skip > 0 ? ", bez glosu " + skip.ToString(Inv) + " (" + block + ")" : "")
                         + (story > 0 ? ", plotki Kroniki bez glosu na zywo " + story.ToString(Inv) : "") + ".");
                GossipAdvance();
            }
            catch (Exception e) { Stumble("GossipPage", e); }
        }

        /// <summary>Akcja "Enough talk" okna plotek (Rumours.ShowPage). Nie rzuca.</summary>
        internal static void GossipStop(string why)
        {
            try { GossipStopAll(why); } catch (Exception e) { Stumble("GossipStop", e); }
        }

        /// <summary>Ten sam delegat "Enough talk" dla kazdego okna plotek (InquiryData.HasSameContentWith porownuje akcje - dubel okna rozpoznawany jak przed lektorem).</summary>
        internal static readonly Action GossipStopAction = () => GossipStop("enough talk");

        private static void CountSkip(int reason, int n)
        {
            switch (reason)
            {
                case 1: _dSkipLimit += n; LimitMessage(); break;
                case 2: _dSkipReserve += n; break;
                case 3: _dSkipAttempts += n; break;
                case 4: _dSkipErr += n; break;
                case 5: _dDropped += n; break;
                case 6: _dSkipNoKey += n; break;
                case 7: _dSkipStopped += n; break;
                case 8: _dSkipSub += n; break;
                default: _dSkipOff += n; break;
            }
        }

        private static void LimitMessage()
        {
            if (_limitMsg) return;
            _limitMsg = true;
            Log.Player("The narrator has read enough gossip for today.");
        }

        static partial void GossipStopAll(string why)
        {
            var segs = _segs;
            bool clip = _ev != null && _evKind == KGossip;
            if (segs == null && !clip) return;
            Interlocked.Increment(ref _pageGen);   // prosby w kolejce do tej strony - porzucone przez watek bez kosztu
            _pageActive = false; _segs = null;
            if (clip) StopCurrent(why);
            if (segs == null) return;
            int played = 0, pending = 0;
            foreach (var sg in segs) { if (sg.State == SPlayed) played++; else if (sg.State == SPending) pending++; }
            if (played < segs.Count)
                Log.Info(P + "plotki w " + _pagePlace + " - stop (" + why + ") po " + (Now - _pageT0).ToString("0.0", Inv) + " s, przeczytane " + played.ToString(Inv) + " z "
                         + segs.Count.ToString(Inv) + (pending > 0 ? ", porzucone prosby " + pending.ToString(Inv) : "") + ".");
        }

        static partial void GossipBusy(ref bool busy)
        {
            if (_pageActive || !_results.IsEmpty) busy = true;
        }

        static partial void GossipTick()
        {
            Res r;
            int n = 0;
            while (n++ < 32 && _results.TryDequeue(out r))
            {
                try { HandleResult(r); } catch (Exception e) { Stumble("HandleResult", e); }
            }
            if (!_pageActive || _segs == null) return;
            if (!InformationManager.IsAnyInquiryActive())
            {
                double now = Now;
                if (_noInqSince < 0) _noInqSince = now;
                else if (now - _noInqSince >= NoInquiryGrace) { GossipStopAll("okno plotek zamkniete (straznik)"); return; }
            }
            else _noInqSince = -1;
            GossipAdvance();
        }

        private static void GossipAdvance()
        {
            var segs = _segs;
            if (!_pageActive || segs == null || _ev != null || Now < _gapUntil) return;
            while (_segIdx < segs.Count)
            {
                var sg = segs[_segIdx];
                if (sg.State == SPending) return;   // czekamy na nagranie (kolejnosc czytania jak na ekranie)
                _segIdx++;
                if (sg.State != SReady) continue;
                if (Play(sg.Path, KGossip, K8(sg.Key), sg.Sec)) { sg.State = SPlayed; _dPlayed++; return; }
                sg.State = SSkip;
            }
            int played = 0;
            foreach (var sg in segs) if (sg.State == SPlayed) played++;
            if (played > 0)   // nic nie grane (wszystko bez glosu) - linia strony juz to mowi
                Log.Info(P + "plotki w " + _pagePlace + " - strona przeczytana: " + played.ToString(Inv) + " z " + segs.Count.ToString(Inv) + " kawalkow w " + (Now - _pageT0).ToString("0.0", Inv) + " s.");
            _pageActive = false; _segs = null;
        }

        private static void HandleResult(Res r)
        {
            if (r.Line != null) Log.Info(P + r.Line);
            if (r.Reason == -1) _dCorrupt++;
            if (r.Sent) { _dRequests++; _sRequests++; }
            if (r.Kind == RInfo) return;
            Seg sg = null;
            var segs = _segs;
            if (r.Gen == Volatile.Read(ref _pageGen) && segs != null && r.Idx >= 0 && r.Idx < segs.Count && segs[r.Idx].Key == r.Key && segs[r.Idx].State == SPending) sg = segs[r.Idx];
            if (r.Kind == RReady)
            {
                if (r.New) { _dNew++; _sNew++; _dNewChars += r.Chars; }
                if (sg != null) { sg.Path = r.Path; sg.Sec = r.Sec; sg.State = SReady; GossipAdvance(); }
                return;
            }
            CountSkip(r.Reason, 1);
            if (r.Reason == 4)
            {
                _dApiErr++;
                int c; _dCodes.TryGetValue(r.Code, out c); _dCodes[r.Code] = c + 1;
            }
            if (sg != null) { sg.State = SSkip; GossipAdvance(); }
        }

        static partial void GossipClipStopped(string why, double t) { }

        static partial void GossipClipEnded(double t, bool early)
        {
            if (early) _dGossipEarly++;
            _gapUntil = Now + GapSec;
            if (early) Log.Info(P + "plotka " + _evTag + " - koniec po " + t.ToString("0.0", Inv) + " s z " + _evLen.ToString("0.0", Inv) + " s - ZA WCZESNIE (silnik nie gra pliku?).");
        }

        static partial void GossipClipChecked(bool playing, bool paused)
        {
            if (!playing)
            {
                _dGossip05No++;
                Log.Info(P + "plotka " + _evTag + " - grane po 0,5 s: NIE" + (paused ? " (pauza silnika - okno plotek pauzuje gre)" : "") + ".");
            }
            else if (!_logged05)
            {
                _logged05 = true;
                Log.Info(P + "plotka " + _evTag + " - grane po 0,5 s: tak" + (paused ? " (pauza silnika)" : "") + " (pierwsza w sesji).");
            }
        }

        static partial void GossipReset()
        {
            try { GossipStopAll("nowa kampania / wczytanie"); } catch (Exception e) { Stumble("GossipReset", e); }
            _dPages = _dSegs = _dHit = _dPlayed = _dNew = _dNewChars = _dSkipLimit = _dSkipReserve = _dSkipOff = _dSkipAttempts = _dSkipErr = _dDropped = 0;
            _dSkipNoKey = _dSkipStopped = _dSkipSub = _dCorrupt = _dRequests = _dApiErr = _dGossip05No = _dGossipEarly = 0;
            _dSkipStory = 0;
            _dCodes.Clear();
            _sRequests = _sNew = 0; _limitMsg = false; _logged05 = false; _liveFlag = -1;
            lock (_wlock) { _apiStopWhy = null; _subState = 0; _subWhy = "nie pytano"; _subWhen = "-"; }   // sesja = stan konta od nowa (raz na sesje)
        }

        static partial void GossipSessionText(StringBuilder sb)
        {
            EnsureCache();
            int idx;
            lock (_cacheLock) idx = _cache.Count;
            int today, sent, zl;
            string day;
            lock (_wlock) { if (_usageDay != Today()) { _usageDay = Today(); _usageToday = 0; } today = _usageToday; sent = _usageSentAll; zl = _usageZeroLines; day = _usageDay; }
            int reason;
            string block = LiveBlock(out reason);
            sb.Append(" | plotki: pamiec ").Append(_cacheFiles.ToString(Inv)).Append(" plikow (w indeksie ").Append(idx.ToString(Inv))
              .Append(_cacheZeroLines + _cacheBadLines > 0 ? ", linie z zerami " + _cacheZeroLines.ToString(Inv) + ", zle " + _cacheBadLines.ToString(Inv) : "")
              .Append(_cacheTmpRemoved > 0 ? ", usuniete .tmp " + _cacheTmpRemoved.ToString(Inv) : "").Append(")")
              .Append(", znaki dzis ").Append(today.ToString(Inv)).Append(" / ").Append(DailyMax().ToString(Inv)).Append(" (doba ").Append(day).Append(")")
              .Append(", wyslan w ksiedze ").Append(sent.ToString(Inv)).Append(zl > 0 ? " (linie z zerami " + zl.ToString(Inv) + ")" : "")
              .Append(" | klucz API ").Append(KeyFileExists() ? "jest" : "brak")
              .Append(" | API na zywo: ").Append(block == null ? "tak (stan konta przy pierwszym nagraniu)" : "NIE - " + block);
        }

        static partial void GossipDayText(StringBuilder sb)
        {
            int today; string day, sub;
            lock (_wlock)
            {
                if (_usageDay != Today()) { _usageDay = Today(); _usageToday = 0; }
                today = _usageToday; day = _usageDay;
                sub = _subState == 1 ? "zostalo " + _subLeft.ToString(Inv) + " z " + _subLimit.ToString(Inv) + " (stan z " + _subWhen + ")"
                    : _subState == 0 ? "nie pytano" : "nieznane (" + _subWhy + ")";
                if (_apiStopWhy != null) sub += ", API wstrzymane: " + _apiStopWhy;
            }
            var codes = new StringBuilder();
            foreach (var kv in _dCodes)
                codes.Append(codes.Length > 0 ? ", " : "").Append(kv.Key > 0 ? "HTTP " + kv.Key.ToString(Inv) : kv.Key == 0 ? "siec" : kv.Key == CodeSave ? "zapis pamieci (oplacone)"
                                                                  : kv.Key == CodeNotSent ? "siec - nie wyszlo" : kv.Key == CodePaidLost ? "oplacone bez nagrania" : "watek")
                     .Append(" x").Append(kv.Value.ToString(Inv));
            sb.Append(" | plotki: strony ").Append(_dPages.ToString(Inv)).Append(", kawalki ").Append(_dSegs.ToString(Inv)).Append(", z pamieci ").Append(_dHit.ToString(Inv))
              .Append(", odtworzone ").Append(_dPlayed.ToString(Inv)).Append(" (grane po 0,5 s nie ").Append(_dGossip05No.ToString(Inv)).Append(", za wczesnie ").Append(_dGossipEarly.ToString(Inv)).Append(")")
              .Append(", nowe nagrania ").Append(_dNew.ToString(Inv)).Append(" (znaki ").Append(_dNewChars.ToString(Inv)).Append(")")
              .Append(", bez glosu: limit dzienny ").Append(_dSkipLimit.ToString(Inv)).Append(", rezerwa ").Append(_dSkipReserve.ToString(Inv))
              .Append(", API wylaczone ").Append(_dSkipOff.ToString(Inv)).Append(", stan konta nieznany ").Append(_dSkipSub.ToString(Inv))
              .Append(", API wstrzymane ").Append(_dSkipStopped.ToString(Inv)).Append(", brak klucza ").Append(_dSkipNoKey.ToString(Inv))
              .Append(", proby ").Append(_dSkipAttempts.ToString(Inv)).Append(", blad ").Append(_dSkipErr.ToString(Inv)).Append(", porzucone ").Append(_dDropped.ToString(Inv))
              .Append(", uszkodzone w pamieci ").Append(_dCorrupt.ToString(Inv))
              .Append(_dSkipStory > 0 ? ", plotki Kroniki bez glosu na zywo " + _dSkipStory.ToString(Inv) : "")
              .Append(" | zapytania API ").Append(_dRequests.ToString(Inv)).Append(" (w sesji ").Append(_sRequests.ToString(Inv)).Append(", nowych nagran w sesji ").Append(_sNew.ToString(Inv)).Append(")")
              .Append(", bledy API ").Append(_dApiErr.ToString(Inv)).Append(codes.Length > 0 ? " (" + codes + ")" : "")
              .Append(" | znaki dzis ").Append(today.ToString(Inv)).Append(" / ").Append(DailyMax().ToString(Inv)).Append(" (doba ").Append(day).Append(")")
              .Append(" | konto: ").Append(sub)
              .Append(" | watek nagran: potkniecia ").Append(Volatile.Read(ref _wStumbles).ToString(Inv));
            _dPages = _dSegs = _dHit = _dPlayed = _dNew = _dNewChars = _dSkipLimit = _dSkipReserve = _dSkipOff = _dSkipAttempts = _dSkipErr = _dDropped = 0;
            _dSkipNoKey = _dSkipStopped = _dSkipSub = _dCorrupt = _dRequests = _dApiErr = _dGossip05No = _dGossipEarly = 0;
            _dSkipStory = 0;
            _dCodes.Clear();
        }

        // ------------------------------------------------------------ watek nagran
        private static void EnsureWorker()
        {
            if (_worker != null && _worker.IsAlive) return;
            var t = new Thread(WorkerMain) { IsBackground = true, Name = "Armoury lektor", Priority = ThreadPriority.BelowNormal };
            t.Start();
            _worker = t;
        }

        private static void WorkerMain()
        {
            while (true)
            {
                Job j = null;
                try
                {
                    if (!_jobs.TryDequeue(out j)) { _wake.WaitOne(10000); continue; }
                    Res r = Work(j);
                    if (r != null) _results.Enqueue(r);
                }
                catch (ThreadAbortException) { throw; }
                catch (Exception e)
                {
                    Interlocked.Increment(ref _wStumbles);
                    try
                    {
                        _results.Enqueue(new Res { Kind = j != null ? RSkip : RInfo, Gen = j != null ? j.Gen : 0, Idx = j != null ? j.Idx : -1, Key = j != null ? j.Key : null, Reason = 4, Code = CodeThread,
                                                   Line = "watek nagran: potkniecie " + e.GetType().Name + " - " + Short(e.Message) + (j != null ? " (plotka " + K8(j.Key) + " bez glosu)" : "") + "." });
                        Thread.Sleep(500);
                    }
                    catch { }
                }
            }
        }

        private static string Short(string s)
        {
            if (string.IsNullOrEmpty(s)) return "-";
            s = s.Replace('\r', ' ').Replace('\n', ' ');
            return s.Length > 160 ? s.Substring(0, 160) + "..." : s;
        }

        private static Res Skip(Job j, int reason, string line)
        {
            return new Res { Kind = RSkip, Gen = j.Gen, Idx = j.Idx, Key = j.Key, Reason = reason, Line = line };
        }

        /// <summary>Jedna prosba (watek nagran): pamiec, limit, konto, klucz, ksiega PRZED wyslaniem, POST, zapis atomowy. Nie dotyka obiektow gry.</summary>
        private static Res Work(Job j)
        {
            if (j.Gen != Volatile.Read(ref _pageGen)) return Skip(j, 5, null);   // strona zamknieta - bez kosztu
            string path, early;
            double sec;
            if (Cached(j.Key, out path, out sec, true)) return new Res { Kind = RReady, Gen = j.Gen, Idx = j.Idx, Key = j.Key, Path = path, Sec = sec };   // ten sam tekst nagrany chwile temu
            int daily = DailyMax(), reserve = ReserveChars(), att;
            lock (_wlock)
            {
                if (_apiStopWhy != null) return Skip(j, 7, null);
                if (_paid.Contains(j.Key)) return Skip(j, 3, "plotka " + K8(j.Key) + " - juz oplacona w ElevenLabs (nagrania nie ma albo zniszczone) - drugi raz nie idzie, bez glosu.");
                if (_usageDay != Today()) { _usageDay = Today(); _usageToday = 0; }
                _attempts.TryGetValue(j.Key, out att);
                if (att >= MaxAttempts) return Skip(j, 3, "plotka " + K8(j.Key) + " - wyslana juz " + att + " razy bez nagrania (najwyzej " + MaxAttempts + ") - bez glosu.");
                if (_usageToday + j.Chars > daily) return Skip(j, 1, "limit dzienny: dzis " + _usageToday + " + " + j.Chars + " > " + daily + " znakow - plotka " + K8(j.Key) + " bez glosu.");
            }
            string key = ReadKey();
            if (key == null) return Skip(j, 6, "brak, pusty albo zly plik klucza .elevenlabs/key.txt - plotka " + K8(j.Key) + " bez glosu.");
            early = EnsureSubscription(key);
            if (early != null) _results.Enqueue(new Res { Kind = RInfo, Line = early });
            long left;
            lock (_wlock)
            {
                if (_subState != 1) return Skip(j, 8, null);
                left = _subLeft;
            }
            if (left - j.Chars < reserve)
                return Skip(j, 2, "rezerwa planu: na koncie zostalo " + left + " znakow, plotka " + j.Chars + ", rezerwa " + reserve + " - bez glosu (tylko pamiec).");
            if (j.Gen != Volatile.Read(ref _pageGen)) return Skip(j, 5, null);   // ostatnia kontrola przed kosztem
            string sentDay;
            lock (_wlock)   // same liczniki; plik ponizej pod _fileLock (glowny watek nie czeka na Flush)
            {
                if (_usageDay != Today()) { _usageDay = Today(); _usageToday = 0; }
                sentDay = _usageDay;
                _usageToday += j.Chars; _usageSentAll++; _attempts[j.Key] = att + 1;
            }
            AppendLine(UsagePath, sentDay + "\t" + Clock() + "\t" + j.Chars.ToString(Inv) + "\t" + j.Key + "\twyslane");   // PRZED wyslaniem (ten sam watek)
            var sw = Stopwatch.StartNew();
            int code;
            string err;
            bool paid, notSent;
            byte[] pcm = PostTts(key, j.Text, sw, out code, out err, out paid, out notSent);
            key = null;
            double ms = sw.Elapsed.TotalMilliseconds;
            if (pcm != null && (pcm.Length < MinPcmBytes || (pcm.Length & 1) != 0)) { err = "odpowiedz " + pcm.Length + " B (za krotka albo nieparzysta)"; pcm = null; }
            if (pcm == null && notSent)
            {
                // zapytanie nie wyszlo z komputera (brak DNS, brak polaczenia, TLS przed HTTP) - ElevenLabs nic nie policzyl: znaki i proba zwrocone,
                // nowe nagrania wstrzymane na SubRetrySec (potem stan konta od nowa - darmowe zapytanie sprawdza siec)
                lock (_wlock)
                {
                    if (_usageDay == sentDay) _usageToday = Math.Max(0, _usageToday - j.Chars);
                    if (_usageSentAll > 0) _usageSentAll--;
                    int a; _attempts.TryGetValue(j.Key, out a); _attempts[j.Key] = a > 0 ? a - 1 : 0;
                    _subState = 2; _subWhy = "siec niedostepna przy nagraniu (" + err + ")"; _subRetryAt = Now + SubRetrySec;
                }
                AppendLine(UsagePath, sentDay + "\t" + Clock() + "\t" + j.Chars.ToString(Inv) + "\t" + j.Key + "\tniewyslane");
                return new Res { Kind = RSkip, Gen = j.Gen, Idx = j.Idx, Key = j.Key, Reason = 4, Code = CodeNotSent, Sent = false,
                                 Line = "plotka " + K8(j.Key) + " - siec niedostepna (" + err + ") po " + ms.ToString("0", Inv) + " ms: zapytanie nie wyszlo z komputera, znaki i proba zwrocone;"
                                        + " nowe nagrania wstrzymane na 10 min (potem stan konta od nowa) - bez glosu." };
            }
            if (pcm == null && paid)
            {
                // HTTP 200 - ElevenLabs policzyl znaki, ale nagranie nie doszlo (pobieranie przerwane, zla odpowiedz) - tekst oplacony, drugi raz nie idzie
                lock (_wlock) { _paid.Add(j.Key); if (_subState == 1) _subLeft -= j.Chars; }
                AppendLine(UsagePath, Today() + "\t" + Clock() + "\t" + j.Chars.ToString(Inv) + "\t" + j.Key + "\toplacone, bez nagrania");
                return new Res { Kind = RSkip, Gen = j.Gen, Idx = j.Idx, Key = j.Key, Reason = 4, Code = CodePaidLost, Sent = true,
                                 Line = "plotka " + K8(j.Key) + " - ElevenLabs oddal nagranie (HTTP 200, znaki policzone), ale " + (err ?? "pobieranie nieudane") + " po " + ms.ToString("0", Inv)
                                        + " ms - bez glosu; tekst oplacony, drugi raz nie idzie." };
            }
            if (pcm == null)
            {
                bool stop = code == 401 || code == 402 || code == 403 || code == 429;
                if (stop) lock (_wlock) _apiStopWhy = "HTTP " + code + (err != null ? " " + err : "");
                AppendLine(UsagePath, Today() + "\t" + Clock() + "\t0\t" + j.Key + "\tblad " + (code > 0 ? code.ToString(Inv) : "siec"));
                return new Res { Kind = RSkip, Gen = j.Gen, Idx = j.Idx, Key = j.Key, Reason = 4, Code = code > 0 ? code : 0, Sent = true,
                                 Line = "plotka " + K8(j.Key) + " - blad API " + (code > 0 ? "HTTP " + code : "sieci") + (err != null ? " (" + err + ")" : "") + " po " + ms.ToString("0", Inv)
                                        + " ms - bez glosu" + (stop ? "; zapytania wstrzymane do konca sesji" : "") + "." };
            }
            lock (_wlock) { _paid.Add(j.Key); if (_subState == 1) _subLeft -= j.Chars; }   // nagranie przyszlo - oplacone, cokolwiek bedzie z zapisem
            byte[] wav = Wav(pcm);
            string md5;
            using (var h = MD5.Create()) md5 = Hex(h.ComputeHash(wav));
            string target = Path.Combine(CacheDir, j.Key + ".wav");
            string werr = SaveAtomic(target, wav, md5);
            if (werr != null)
            {
                AppendLine(UsagePath, Today() + "\t" + Clock() + "\t" + j.Chars.ToString(Inv) + "\t" + j.Key + "\toplacone, zapis nieudany");
                return new Res { Kind = RSkip, Gen = j.Gen, Idx = j.Idx, Key = j.Key, Reason = 4, Code = CodeSave, Sent = true,
                                 Line = "plotka " + K8(j.Key) + " - nagranie przyszlo (znaki policzone), zapis pamieci nieudany (" + werr + ") - bez glosu; tekst oplacony, drugi raz nie idzie." };
            }
            int count;
            lock (_cacheLock) { _cache[j.Key] = new CacheEntry { Md5 = md5, Chars = j.Chars }; count = _cache.Count; }
            AppendLine(IndexPath, j.Key + "\t" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv) + "\t" + j.Chars.ToString(Inv) + "\t" + j.Text + "\t" + md5);
            AppendLine(UsagePath, Today() + "\t" + Clock() + "\t" + j.Chars.ToString(Inv) + "\t" + j.Key + "\tok");
            sec = pcm.Length / (double)PcmBytesPerSec;
            return new Res { Kind = RReady, Gen = j.Gen, Idx = j.Idx, Key = j.Key, Path = target, Sec = sec, Chars = j.Chars, Sent = true, New = true,
                             Line = "nowe nagranie plotki " + K8(j.Key) + ": " + j.Chars + " znakow, " + sec.ToString("0.0", Inv) + " s, zapytanie " + ms.ToString("0", Inv) + " ms, w pamieci " + count + "." };
        }

        /// <summary>Stan konta raz na sesje (blad chwilowy - ponowienie po SubRetrySec). Zwraca linie do logu albo null.</summary>
        private static string EnsureSubscription(string key)
        {
            lock (_wlock)
            {
                if (_subState == 1 || _subState == 3) return null;
                if (_subState == 2 && Now < _subRetryAt) return null;
            }
            int code;
            string err;
            string body = GetText(SubUrl, key, out code, out err);
            long used, limit, reset;
            if (body != null && Num(body, "character_count", out used) && Num(body, "character_limit", out limit))
            {
                if (!Num(body, "next_character_count_reset_unix", out reset)) reset = 0;
                lock (_wlock) { _subState = 1; _subUsed = used; _subLimit = limit; _subLeft = limit - used; _subWhy = "ok"; _subWhen = DateTime.Now.ToString("HH:mm", Inv); }
                string when = reset > 0 ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(reset).ToLocalTime().ToString("yyyy-MM-dd", Inv) : "-";
                return "konto ElevenLabs: zuzyte " + used + " z " + limit + " znakow, zostalo " + (limit - used) + " (rezerwa " + ReserveChars() + "), odnowienie " + when + ".";
            }
            bool hard = code == 401 || code == 403 || (code == 200 && body != null);
            string why = code == 200 && body != null ? "odpowiedz bez liczb znakow" : code > 0 && code != 200 ? "HTTP " + code + (err != null ? " " + err : "")
                       : "blad sieci" + (err != null ? " " + err : "");   // 200 bez tresci = odczyt przerwany (stoper, zastoj) - chwilowy
            lock (_wlock) { _subState = hard ? 3 : 2; _subWhy = why; _subRetryAt = Now + SubRetrySec; }
            return "konto ElevenLabs: stanu nie da sie odczytac (" + why + ") - nowe nagrania plotek wstrzymane"
                   + (hard ? " do konca sesji" + (code == 401 || code == 403 ? " (klucz bez uprawnienia User: Read?)" : "") : ", ponowienie najwczesniej za 10 min") + "; pamiec dziala.";
        }

        private static bool Num(string body, string name, out long v)
        {
            v = 0;
            var m = Regex.Match(body, "\"" + name + "\"\\s*:\\s*(-?\\d+)", RegexOptions.CultureInvariant);
            return m.Success && long.TryParse(m.Groups[1].Value, NumberStyles.Integer, Inv, out v);
        }

        // ------------------------------------------------------------ HTTP (HttpWebRequest z System.dll - bez nowych bibliotek)
        private static HttpWebRequest NewRequest(string url, string method, string key, int timeout)
        {
            if (!_tls)
            {
                _tls = true;
                try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }   // tylko dodaje TLS 1.2 (tak samo VoiceActingPatch)
            }
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = method;
            req.Timeout = timeout;
            req.ReadWriteTimeout = timeout;
            req.ServicePoint.Expect100Continue = false;
            req.UserAgent = "Armoury-Lektor/1.0";
            req.Headers.Add("xi-api-key", key);
            return req;
        }

        /// <summary>Stoper calosci zapytania: Timeout HttpWebRequest liczy osobno GetRequestStream, GetResponse i kazdy Read (DNS poza nim), wiec po ms - Abort
        /// (WebException RequestCanceled). cut[0] = 1 - przerwal stoper; cut[1] = 1 - zapytanie skonczone (spozniony stoper nic nie robi). Watek puli, nie rzuca.</summary>
        private static System.Threading.Timer AbortTimer(HttpWebRequest req, int[] cut, int ms)
        {
            return new System.Threading.Timer(_ =>
            {
                if (Volatile.Read(ref cut[1]) != 0) return;
                Volatile.Write(ref cut[0], 1);
                try { req.Abort(); } catch { }
            }, null, ms, Timeout.Infinite);
        }

        /// <summary>Zapytanie nie dotarlo do serwera (nazwa nie rozwiazana, brak polaczenia, TLS/certyfikat przed wyslaniem HTTP) - ElevenLabs nic nie policzyl.
        /// Timeout, zerwane polaczenie, Abort stopera - NIE (serwer mogl juz liczyc).</summary>
        private static bool NotReached(WebExceptionStatus st)
        {
            return st == WebExceptionStatus.NameResolutionFailure || st == WebExceptionStatus.ProxyNameResolutionFailure || st == WebExceptionStatus.ConnectFailure
                || st == WebExceptionStatus.SecureChannelFailure || st == WebExceptionStatus.TrustFailure;
        }

        /// <summary>POST TTS. paid - przyszlo HTTP 200 (znaki policzone); notSent - zapytanie nie wyszlo z komputera. Stoper TimeoutMs do odpowiedzi serwera; po 200
        /// pobieranie oplaconego nagrania nie jest ciete po TimeoutMs - tylko zastoj odczytu (ReadWriteTimeout) albo PaidCapMs od startu.</summary>
        private static byte[] PostTts(string key, string text, Stopwatch sw, out int code, out string err, out bool paid, out bool notSent)
        {
            code = 0; err = null; paid = false; notSent = false;
            var cut = new int[2];
            System.Threading.Timer timer = null;
            try
            {
                var req = NewRequest(TtsUrl, "POST", key, TimeoutMs);
                timer = AbortTimer(req, cut, TimeoutMs);
                req.ContentType = "application/json";
                req.Accept = "audio/*";
                byte[] body = Utf8.GetBytes("{\"text\":" + Json(text) + ",\"model_id\":\"" + ModelId + "\",\"voice_settings\":" + VoiceSettingsJson + "}");
                req.ContentLength = body.Length;
                using (var s = req.GetRequestStream()) s.Write(body, 0, body.Length);
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    code = (int)resp.StatusCode;
                    if (code != 200) { err = "status " + code; return null; }
                    paid = true;
                    timer.Change(Math.Max(1L, PaidCapMs - sw.ElapsedMilliseconds), (long)Timeout.Infinite);
                    return ReadAll(resp, req, MaxPcmBytes, out err);
                }
            }
            catch (WebException we)
            {
                WebExceptionStatus st;
                int c = ErrOf(we, out err, out st);
                if (code == 0) code = c;   // 200 zostaje 200 (odczyt przerwany po odpowiedzi)
                notSent = code == 0 && Volatile.Read(ref cut[0]) == 0 && NotReached(st);
            }
            catch (Exception e) { err = e.GetType().Name; }
            finally { Volatile.Write(ref cut[1], 1); if (timer != null) timer.Dispose(); }
            if (Volatile.Read(ref cut[0]) == 1)
                err = (paid ? "pobieranie przerwane po " : "brak odpowiedzi po ") + (sw.ElapsedMilliseconds / 1000.0).ToString("0.0", Inv) + " s (limit " + ((paid ? PaidCapMs : TimeoutMs) / 1000) + " s)";
            return null;
        }

        private static string GetText(string url, string key, out int code, out string err)
        {
            code = 0; err = null;
            var cut = new int[2];
            System.Threading.Timer timer = null;
            try
            {
                var req = NewRequest(url, "GET", key, SubTimeoutMs);
                timer = AbortTimer(req, cut, SubTimeoutMs);
                req.Accept = "application/json";
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    code = (int)resp.StatusCode;
                    byte[] b = ReadAll(resp, req, 65536, out err);
                    return code == 200 && b != null ? Utf8.GetString(b) : null;
                }
            }
            catch (WebException we) { WebExceptionStatus st; int c = ErrOf(we, out err, out st); if (code == 0) code = c; }
            catch (Exception e) { err = e.GetType().Name; }
            finally { Volatile.Write(ref cut[1], 1); if (timer != null) timer.Dispose(); }
            if (Volatile.Read(ref cut[0]) == 1) err = "ponad " + (SubTimeoutMs / 1000) + " s";
            return null;
        }

        /// <summary>Kod HTTP z bledu (0 = siec) i krotki status z tresci bledu ElevenLabs ("quota_exceeded", "invalid_api_key" ...) - bez reszty tresci.</summary>
        private static int ErrOf(WebException we, out string err, out WebExceptionStatus st)
        {
            st = we.Status;
            err = st.ToString();
            var hr = we.Response as HttpWebResponse;
            if (hr == null) return 0;
            int code = (int)hr.StatusCode;
            err = null;
            try
            {
                using (var rs = hr.GetResponseStream())
                {
                    var buf = new byte[2048];
                    int got = 0, k;
                    while (got < buf.Length && (k = rs.Read(buf, got, buf.Length - got)) > 0) got += k;
                    var m = Regex.Match(Utf8.GetString(buf, 0, got), "\"status\"\\s*:\\s*\"([A-Za-z_]{1,40})\"", RegexOptions.CultureInvariant);
                    if (m.Success) err = m.Groups[1].Value;
                }
            }
            catch { }
            try { hr.Close(); } catch { }
            return code;
        }

        /// <summary>Cala odpowiedz (najwyzej max B). Czas pilnuje stoper AbortTimer (Abort przerywa Read wyjatkiem) i ReadWriteTimeout (zastoj jednego Read).</summary>
        private static byte[] ReadAll(WebResponse resp, HttpWebRequest req, int max, out string err)
        {
            err = null;
            using (var rs = resp.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                var buf = new byte[65536];
                int k;
                while ((k = rs.Read(buf, 0, buf.Length)) > 0)
                {
                    ms.Write(buf, 0, k);
                    if (ms.Length > max) { err = "odpowiedz ponad " + max + " B"; try { req.Abort(); } catch { } return null; }
                }
                return ms.ToArray();
            }
        }

        private static string Json(string s)
        {
            var sb = new StringBuilder(s.Length + 16);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", Inv));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ------------------------------------------------------------ pliki
        /// <summary>Naglowek RIFF 44 B jak tools/lektor/nagraj_david.py (PCM, mono, 24 kHz, 16 bit) + dane.</summary>
        private static byte[] Wav(byte[] pcm)
        {
            var b = new byte[44 + pcm.Length];
            Action<int, string> s4 = (o, t) => { for (int i = 0; i < 4; i++) b[o + i] = (byte)t[i]; };
            Action<int, int> i32 = (o, v) => { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24); };
            Action<int, int> i16 = (o, v) => { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); };
            s4(0, "RIFF"); i32(4, 36 + pcm.Length); s4(8, "WAVE"); s4(12, "fmt "); i32(16, 16); i16(20, 1); i16(22, 1); i32(24, PcmRate); i32(28, PcmBytesPerSec);
            i16(32, 2); i16(34, 16); s4(36, "data"); i32(40, pcm.Length);
            Buffer.BlockCopy(pcm, 0, b, 44, pcm.Length);
            return b;
        }

        /// <summary>Zapis atomowy: plik .tmp, Flush(true), Move na miejsce, kontrola md5 po zapisie. null = dobrze.</summary>
        private static string SaveAtomic(string path, byte[] data, string md5)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(data, 0, data.Length);
                    fs.Flush(true);
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                string m = Md5File(path);
                if (!string.Equals(m, md5, StringComparison.OrdinalIgnoreCase)) { try { File.Delete(path); } catch { } return "md5 po zapisie " + (m ?? "-"); }
                return null;
            }
            catch (Exception e) { return e.GetType().Name; }
        }

        /// <summary>Dopisanie linii z Flush(true) - ksiega zapytan musi przetrwac wywrotke gry. Pod _fileLock (NIE pod _wlock - glowny watek nie czeka na dysk).</summary>
        private static void AppendLine(string path, string line)
        {
            try
            {
                byte[] b = Utf8.GetBytes(line.Replace('\r', ' ').Replace('\n', ' ') + "\n");
                lock (_fileLock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                    {
                        fs.Write(b, 0, b.Length);
                        fs.Flush(true);
                    }
                }
            }
            catch (Exception e)
            {
                Interlocked.Increment(ref _wStumbles);
                _results.Enqueue(new Res { Kind = RInfo, Line = "zapis " + Path.GetFileName(path) + " nieudany (" + e.GetType().Name + ")." });
            }
        }
    }
}
