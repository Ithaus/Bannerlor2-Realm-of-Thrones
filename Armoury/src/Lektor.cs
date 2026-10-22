using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using TaleWorlds.Core;
using Scene = TaleWorlds.Engine.Scene;
using SoundEvent = TaleWorlds.Engine.SoundEvent;   // bez using TaleWorlds.Engine - jego Path zaslania System.IO.Path

namespace Armoury
{
    /// <summary>
    /// LK - LEKTOR (m103-lektor, Jeff 21.10: lektor tylko po angielsku, glos David - ElevenLabs voice_id BNgbHR0DNeZixGQVzloa, model eleven_multilingual_v2;
    /// "zostaje przy Starterze, ale zapisuj kazda nagrana plotke, aby potem, jak jest cos nagrane, David nie czytal"). Klucze grupy Narrator (LektorEnabled i nast.).
    /// Ten plik: okna wydarzen ROT czytane z nagran, ktore ida z modem, i wspolny odtwarzacz; plotki na zywo - Lektor.Live.cs.
    ///  - Nagrania: Modules/Armoury/ModuleData/Lektor/EN/arm_lektor.tsv (klucz, tytul okna = EventPanel.EventTitle na ekranie, plik, opcjonalnie md5) + pliki .wav
    ///    (PCM 24 kHz mono 16 bit, naglowek 44 B). Wczytanie raz na proces (Install, menu glowne); plik bez RIFF/WAVE, z innym rozmiarem niz w naglowku albo z blokiem
    ///    zer (dysk C Jeffa zeruje pliki - najdluzsza cisza cyfrowa w 35 nagraniach Davida to 528 probek = 1056 B, blok kontrolny 4096 B) - pominiety i policzony.
    ///    Przy odtworzeniu ponowna kontrola tego jednego pliku (md5 z tsv, gdy jest; inaczej naglowek i bloki).
    ///  - m105-kronika K0 (A3, plan P9): indeks PO KLUCZU przed indeksem po tytule. Klucz = kolumna 1 arm_lektor.tsv (okna ROT i sceny prologu prolog_NN - dalej
    ///    takze po tytule, jak dotad) oraz manifest Kroniki ModuleData/Lektor/EN/arm_lektor_kronika.tsv (klucz kr_NNN_w[war], tytul, plik .ogg, md5 tekstu, md5 pliku,
    ///    paczka; pisze go tools/lektor/nagraj_kronika.py) - wiersze Kroniki TYLKO po kluczu (bez tytulu: warianty o tym samym tytule maja inny tekst).
    ///    Klucz czytany tylko z okna opowiesci Armoury (typ Armoury.ArmStoryEvent po nazwie - ArmStoryEvent.Key = StoryWindow.Spec.Key); okna ROT - po tytule.
    ///    ZASADA md5 (Kronika): nagranie gra tylko, gdy md5 tekstu z manifestu = ArmStoryEvent.VoiceMd5 (md5 UTF-8 tekstu okna zlozonego DOKLADNIE jak
    ///    nagraj_kronika.py - liczy Chronicle.Voice); md5 tekstu w manifescie inne niz 32 znaki hex (np. "nieaktualne") = nagranie ze starym tekstem, cisza z linia.
    ///    Pliki .ogg (Vorbis, glosy gry SandBox/StoryMode VoicedLines tez sa w .ogg i ida ta sama droga CreateEventFromExternalFile "event:/Extra/voiceover"):
    ///    kontrola OggS + naglowek Vorbis + bloki zer przy wczytaniu, md5 pliku przy odtworzeniu (wzor kontroli WAV).
    ///  - Okno: ROT.Screens.EventScreen (bez referencji do ROT.dll - typ odbiciem). Interfejs IGameStateListener jest w nim JAWNY - cele latek z mapy interfejsu
    ///    (GetInterfaceMap), zapasowo po nazwie "TaleWorlds.Core.IGameStateListener.OnActivate". Postfiks OnActivate (pole _dataSource ustawione, wlasciwosc
    ///    EventTitle) -> plik -> odtworzenie; prefiks OnDeactivate -> stop (OnDeactivate zeruje _dataSource). Pustych OnInitialize / OnFinalize NIE latamy (pamiec:
    ///    zawieszenie GC na malej metodzie z detourem). Po latce Harmony.GetPatchInfo - finalizer BKROTPatch polyka bledy Harmony.Patch, wiec "tak" tylko z GetPatchInfo.
    ///    EventPanel.ExecuteDone/ExecuteCancel (takze Esc i Enter z EventScreen.OnFrameTick) robia PopState -> OnDeactivate. Kolejne okno zaraz po zamknieciu -
    ///    nowy OnActivate zatrzymuje poprzedni dzwiek. Ten sam ekran aktywowany drugi raz - bez powtorki.
    ///  - Odtwarzanie jak powiadomienia z glosem w grze (SandBox.GauntletUI.SandBoxGauntletGameNotification): SoundEvent.CreateEventFromExternalFile("event:/Extra/voiceover",
    ///    sciezka, scena null, 2D, bez blokowania) + Play(); koniec: !IsValid albo IsStopped() -> Release(); zatrzymanie - samo Release() (gra tak robi: Release przy
    ///    graniu wola Stop, ktory zeruje uchwyt). Tylko glowny watek. Glosnosc ustawia silnik (suwaki gry) - do sprawdzenia w autotescie i u Jeffa.
    ///  - AppTick (SubModuleMain.OnApplicationTick): koniec klipu, kontrola "grane po 0,5 s" (IsPlaying), straznik stanu (aktywny stan gry to nie EventPopupState,
    ///    a gra klip okna -> stop), plotki (Lektor.Live). Nic nie gra i nic nie czeka - sam test dwoch pol.
    /// Wyjatki liczone per okno / per klip (potkniecia), bez wylacznika. Linie "Lektor (m103): ..."; grupa progow m103lektor w tools/sprawdz_logi.py.
    /// Autotest (LogGate 1/2): okna graja jak w grze (log pokazuje start / grane / stop); plotki na zywo bez sieci - Lektor.Live.
    /// </summary>
    internal static partial class Lektor
    {
        internal const string P = "Lektor (m103): ";
        internal const string VoiceEvent = "event:/Extra/voiceover";
        private const string IndexName = "arm_lektor.tsv";
        private const string KronikaIndexName = "arm_lektor_kronika.tsv";   // m105-kronika K0 (A3): manifest nagran Kroniki (nagraj_kronika.py)
        private const string KronikaPrefix = "kr_";
        private const string StoryEventType = "Armoury.ArmStoryEvent";      // okno opowiesci Armoury - po nazwie (bez typu ROT w tej metodzie)
        private const int KWindow = 1, KGossip = 2;
        private const int ZeroBlock = 4096;
        private const int OggTail = 65536;                                   // koniec pliku .ogg do szukania ostatniej strony (dlugosc klipu do logu)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static bool On { get { var s = Settings.Current; return s != null && s.LektorEnabled; } }
        private static bool WindowsOn { get { var s = Settings.Current; return s != null && s.LektorEnabled && s.LektorEventWindows; } }
        /// <summary>Klucze LektorEnabled i LektorEventWindows TAK (do linii okna Kroniki).</summary>
        internal static bool WindowsEnabled { get { return WindowsOn; } }

        // ------------------------------------------------------------ nagrania okien (arm_lektor.tsv) i Kroniki (arm_lektor_kronika.tsv)
        private sealed class Rec
        {
            internal string Path, Md5, File, Key, Title;
            internal string TextMd5;   // Kronika: md5 tekstu okna z manifestu (32 hex); null - nagranie ze starym tekstem (Stale)
            internal string Stale;     // Kronika: powod "md5 tekstu 'nieaktualne'" - nagranie nie gra
            internal bool Kronika;
            internal double Sec;
        }
        private static readonly Dictionary<string, Rec> _byTitle = new Dictionary<string, Rec>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Rec> _byLoose = new Dictionary<string, Rec>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Rec> _byKey = new Dictionary<string, Rec>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _krRejected = new Dictionary<string, string>(StringComparer.Ordinal);   // klucz Kroniki -> powod odrzucenia pliku
        private static bool _indexLoaded;
        private static string _indexState = "nie wczytane", _krState = "nie wczytane";
        private static int _idxRows, _idxBad, _idxBadLines, _idxZeroLines, _idxDup, _idxNoMd5;
        private static int _krRows, _krBad, _krBadLines, _krZeroLines, _krDup, _krNoMd5, _krStale, _krOgg;
        private static double _idxSec;
        private static string _idxBadFirst, _krBadFirst;

        // ------------------------------------------------------------ latka EventScreen
        private static bool _installed;
        private static string _patch = "nie zakladana";
        private static Type _stateType;
        private static FieldInfo _fData, _fState;
        private static readonly Dictionary<string, PropertyInfo> _props = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);   // typ|nazwa -> wlasciwosc (EventPanel.EventTitle, zapas Event.Title)
        private static WeakReference _lastScreen;

        // ------------------------------------------------------------ odtwarzacz (jeden klip naraz)
        private static SoundEvent _ev;
        private static int _evKind;
        private static string _evTag;
        private static double _evT0, _evLen;
        private static bool _ev05;

        // ------------------------------------------------------------ liczniki (doba gry; potkniecia - sesja)
        private static int _dWinOpen, _dWinRec, _dWinPlay, _dWin05Yes, _dWin05No, _dWinNoRec, _dWinBad, _dWinOff, _dWinRepeat, _dWinEarly;
        private static int _dWinKey, _dWinStale;   // m105-kronika K0 (A3): nagranie znalezione po kluczu; nagranie Kroniki z innym tekstem niz okno (cisza)
        private static int _dCreateFail, _dStumbles, _stumbles;
        private static int _sWinPlay, _sWin05Yes;   // od startu sesji (linia startu nastepnej sesji nie potrzebuje - tylko linia doby)

        private static double Now { get { return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_stumbles <= 5 || _stumbles % 50 == 0) Log.Error("Lektor." + where + " (potkniecie " + _stumbles + ")", e); } catch { }
        }

        private static string ModuleRoot()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);   // bin/Win64_Shipping_Client
            return Path.GetFullPath(Path.Combine(dir, "..", ".."));
        }

        private static string Loose(string title)
        {
            string t = (title ?? "").Trim();
            while (t.EndsWith(".", StringComparison.Ordinal) || t.EndsWith("!", StringComparison.Ordinal)) t = t.Substring(0, t.Length - 1).TrimEnd();
            return t;
        }

        /// <summary>Wczytanie arm_lektor.tsv i manifestu Kroniki arm_lektor_kronika.tsv (raz na proces; ponownie tylko gdy pierwsze nie znalazlo zadnego z nich). Nie rzuca.</summary>
        private static void LoadIndex()
        {
            if (_indexLoaded) return;
            _indexLoaded = true;
            _byTitle.Clear(); _byLoose.Clear(); _byKey.Clear(); _krRejected.Clear();
            _idxRows = _idxBad = _idxBadLines = _idxZeroLines = _idxDup = _idxNoMd5 = 0; _idxSec = 0; _idxBadFirst = null;
            _krRows = _krBad = _krBadLines = _krZeroLines = _krDup = _krNoMd5 = _krStale = _krOgg = 0; _krBadFirst = null;
            string dir = null;
            try { dir = Path.Combine(Path.Combine(Path.Combine(ModuleRoot(), "ModuleData"), "Lektor"), "EN"); }
            catch (Exception e) { _indexState = _krState = "blad katalogu " + e.GetType().Name; Stumble("LoadIndex.Dir", e); return; }
            bool a = LoadWindowIndex(dir), b = false;
            try { b = LoadKronikaIndex(dir); }
            catch (Exception e) { _krState = "blad " + e.GetType().Name; Stumble("LoadKronikaIndex", e); b = true; }
            if (!a && !b) _indexLoaded = false;   // zaden plik nie znaleziony - nastepne wolanie sprobuje znowu (dwa File.Exists)
        }

        /// <summary>arm_lektor.tsv: okna ROT i sceny prologu - po tytule (jak dotad) i po kluczu (kolumna 1). false - brak pliku.</summary>
        private static bool LoadWindowIndex(string dir)
        {
            try
            {
                string tsv = Path.Combine(dir, IndexName);
                if (!File.Exists(tsv)) { _indexState = "brak pliku " + tsv; return false; }
                foreach (string raw in File.ReadAllLines(tsv, Encoding.UTF8))
                {
                    string line = raw.TrimEnd('\r');
                    if (line.Length == 0 || line[0] == '#') continue;
                    if (line.IndexOf('\0') >= 0) { _idxZeroLines++; continue; }   // linia z zerami (dysk C) - pominieta, policzona
                    string[] c = line.Split('\t');
                    if (c.Length < 3) { _idxBadLines++; continue; }
                    string title = c[1].Trim(), file = c[2].Trim();
                    string md5 = c.Length >= 4 ? c[3].Trim().ToLowerInvariant() : null;
                    if (title.Length == 0 || file.Length == 0 || file.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || file.Contains("..")) { _idxBadLines++; continue; }
                    if (md5 != null && md5.Length != 32) md5 = null;
                    string path = Path.Combine(dir, file);
                    double sec;
                    string why = CheckAudio(path, null, out sec);   // przy starcie bez md5 (38 MB) - naglowek, rozmiar, bloki; md5 przy odtworzeniu (.wav albo .ogg)
                    if (why != null)
                    {
                        _idxBad++;
                        if (_idxBadFirst == null) _idxBadFirst = file + " (" + why + ")";
                        continue;
                    }
                    if (_byTitle.ContainsKey(title)) { _idxDup++; continue; }
                    string key = c[0].Trim();
                    var r = new Rec { Path = path, Md5 = md5, File = file, Sec = sec, Key = key, Title = title };
                    if (md5 == null) _idxNoMd5++;
                    _byTitle[title] = r;
                    string lo = Loose(title);
                    if (!_byLoose.ContainsKey(lo)) _byLoose[lo] = r;
                    if (key.Length > 0 && !_byKey.ContainsKey(key)) _byKey[key] = r;
                    _idxRows++; _idxSec += sec;
                }
                _indexState = "ok";
            }
            catch (Exception e) { _indexState = "blad " + e.GetType().Name; Stumble("LoadIndex", e); }
            return true;
        }

        private static bool IsHex32(string s)
        {
            if (s == null || s.Length != 32) return false;
            foreach (char ch in s) if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f'))) return false;
            return true;
        }

        /// <summary>m105-kronika K0 (A3): manifest nagran Kroniki (klucz, tytul okna, plik, md5 tekstu, md5 pliku, paczka) - tylko po kluczu. Plik sprawdzany jak WAV
        /// (naglowek OggS/Vorbis albo RIFF, bloki zer; md5 pliku przy odtworzeniu). md5 tekstu inne niz 32 hex - wiersz zostaje jako nagranie ze starym tekstem (cisza
        /// z powodem). false - brak pliku.</summary>
        private static bool LoadKronikaIndex(string dir)
        {
            string tsv = Path.Combine(dir, KronikaIndexName);
            if (!File.Exists(tsv)) { _krState = "brak pliku " + KronikaIndexName; return false; }
            foreach (string raw in File.ReadAllLines(tsv, Encoding.UTF8))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                if (line.IndexOf('\0') >= 0) { _krZeroLines++; continue; }   // linia z zerami (dysk C) - pominieta, policzona
                string[] c = line.Split('\t');
                if (c.Length < 5) { _krBadLines++; continue; }
                string key = c[0].Trim(), title = c[1].Trim(), file = c[2].Trim();
                string tmd5 = c[3].Trim().ToLowerInvariant(), fmd5 = c[4].Trim().ToLowerInvariant();
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (!key.StartsWith(KronikaPrefix, StringComparison.Ordinal) || key.Length <= KronikaPrefix.Length || file.Length == 0
                    || file.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || file.Contains("..") || (ext != ".ogg" && ext != ".wav")) { _krBadLines++; continue; }
                if (_byKey.ContainsKey(key) || _krRejected.ContainsKey(key)) { _krDup++; continue; }
                if (!IsHex32(fmd5)) { fmd5 = null; _krNoMd5++; }
                string stale = null;
                if (!IsHex32(tmd5)) { stale = "md5 tekstu w manifescie '" + c[3].Trim() + "' (nagranie ze starym tekstem)"; tmd5 = null; }
                string path = Path.Combine(dir, file);
                double sec;
                string why = CheckAudio(path, null, out sec);   // przy starcie bez md5 - naglowek i bloki zer; md5 przy odtworzeniu
                if (why != null)
                {
                    _krBad++;
                    _krRejected[key] = "plik " + file + " odrzucony (" + why + ")";
                    if (_krBadFirst == null) _krBadFirst = file + " (" + why + ")";
                    continue;
                }
                _byKey[key] = new Rec { Path = path, Md5 = fmd5, File = file, Key = key, Title = title, TextMd5 = tmd5, Stale = stale, Kronika = true, Sec = sec };
                _krRows++;
                if (stale != null) _krStale++;
                if (ext == ".ogg") _krOgg++;
            }
            _krState = "ok";
            return true;
        }

        private static Rec Find(string title)
        {
            if (string.IsNullOrEmpty(title)) return null;
            Rec r;
            if (_byTitle.TryGetValue(title, out r)) return r;
            if (_byTitle.TryGetValue(title.Trim(), out r)) return r;
            return _byLoose.TryGetValue(Loose(title), out r) ? r : null;
        }

        // ------------------------------------------------------------ kontrola pliku .wav
        /// <summary>null = plik dobry (sec = dlugosc); inaczej powod. md5 != null - pelna kontrola md5 (pliki male albo jeden przy odtworzeniu).</summary>
        internal static string CheckWav(string path, string md5, out double sec)
        {
            sec = 0;
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) return "brak pliku";
                long n = fi.Length;
                if (n < 44 + 2 * ZeroBlock) return "za krotki (" + n.ToString(Inv) + " B)";
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var h = new byte[44];
                    if (ReadAt(fs, 0, h, 44) != 44) return "naglowek nieczytelny";
                    if (h[0] != 'R' || h[1] != 'I' || h[2] != 'F' || h[3] != 'F' || h[8] != 'W' || h[9] != 'A' || h[10] != 'V' || h[11] != 'E') return "naglowek bez RIFF/WAVE (zera?)";
                    if (h[36] != 'd' || h[37] != 'a' || h[38] != 't' || h[39] != 'a') return "brak bloku data za naglowkiem 44 B";
                    int byteRate = BitConverter.ToInt32(h, 28);
                    long data = BitConverter.ToUInt32(h, 40);
                    if (byteRate <= 0) return "zly naglowek (bajty na sekunde " + byteRate.ToString(Inv) + ")";
                    if (44L + data != n) return "rozmiar " + n.ToString(Inv) + " B, naglowek mowi " + (44L + data).ToString(Inv) + " B";
                    sec = data / (double)byteRate;
                    if (md5 == null)
                    {
                        if (Zeros(fs, n - ZeroBlock)) return "koniec pliku z zer";
                        if (Zeros(fs, 44 + ((data / 2) & ~1L))) return "srodek pliku z zer";
                    }
                }
                if (md5 != null)
                {
                    string m = Md5File(path);
                    if (!string.Equals(m, md5, StringComparison.OrdinalIgnoreCase)) return "md5 " + (m ?? "-") + " zamiast " + md5;
                }
                return null;
            }
            catch (Exception e) { return "blad odczytu " + e.GetType().Name; }
        }

        /// <summary>Kontrola pliku nagrania wedlug rozszerzenia: .ogg - CheckOgg, inne - CheckWav. null = dobry.</summary>
        internal static string CheckAudio(string path, string md5, out double sec)
        {
            sec = 0;
            string ext;
            try { ext = Path.GetExtension(path ?? "").ToLowerInvariant(); }
            catch (Exception e) { return "zla sciezka (" + e.GetType().Name + ")"; }
            return ext == ".ogg" ? CheckOgg(path, md5, out sec) : CheckWav(path, md5, out sec);
        }

        // ------------------------------------------------------------ kontrola pliku .ogg (m105-kronika K0, A3)
        /// <summary>null = plik dobry; inaczej powod. Pierwsza strona: "OggS", wersja 0, pakiet identyfikacji Vorbis ("\x01vorbis", czestotliwosc 8000..192000);
        /// md5 == null (wczytanie) - bloki zer na koncu i w srodku (dysk C zeruje pliki; dane Vorbis nie maja 4096 zer z rzedu); md5 != null (odtworzenie) - pelna kontrola
        /// md5 i dlugosc klipu z pozycji granuli ostatniej strony (sec; 0 - nie znaleziona).</summary>
        internal static string CheckOgg(string path, string md5, out double sec)
        {
            sec = 0;
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) return "brak pliku";
                long n = fi.Length;
                if (n < 64 + 2 * ZeroBlock) return "za krotki (" + n.ToString(Inv) + " B)";
                int rate;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var h = new byte[64];
                    if (ReadAt(fs, 0, h, 64) != 64) return "naglowek nieczytelny";
                    if (h[0] != 'O' || h[1] != 'g' || h[2] != 'g' || h[3] != 'S' || h[4] != 0) return "naglowek bez OggS (zera?)";
                    int p = 27 + h[26];   // naglowek strony 27 B + tablica segmentow
                    if (p + 16 > h.Length) return "pierwsza strona Ogg nietypowa (segmentow " + h[26].ToString(Inv) + ")";
                    if (h[p] != 1 || h[p + 1] != 'v' || h[p + 2] != 'o' || h[p + 3] != 'r' || h[p + 4] != 'b' || h[p + 5] != 'i' || h[p + 6] != 's')
                        return "Ogg bez naglowka Vorbis (inny kodek?)";
                    rate = BitConverter.ToInt32(h, p + 12);
                    if (rate < 8000 || rate > 192000) return "zla czestotliwosc Vorbis " + rate.ToString(Inv);
                    if (md5 == null)
                    {
                        if (Zeros(fs, n - ZeroBlock)) return "koniec pliku z zer";
                        if (Zeros(fs, (n / 2) & ~1L)) return "srodek pliku z zer";
                    }
                    else sec = OggSeconds(fs, n, rate);
                }
                if (md5 != null)
                {
                    string m = Md5File(path);
                    if (!string.Equals(m, md5, StringComparison.OrdinalIgnoreCase)) return "md5 " + (m ?? "-") + " zamiast " + md5;
                }
                return null;
            }
            catch (Exception e) { return "blad odczytu " + e.GetType().Name; }
        }

        /// <summary>Dlugosc klipu .ogg: pozycja granuli (probki) ostatniej strony "OggS" w koncowce pliku / czestotliwosc. 0 - nie znaleziona.</summary>
        private static double OggSeconds(FileStream fs, long n, int rate)
        {
            int len = (int)Math.Min(n, OggTail);
            var b = new byte[len];
            int got = ReadAt(fs, n - len, b, len);
            for (int i = got - 27; i >= 0; i--)
            {
                if (b[i] != 'O' || b[i + 1] != 'g' || b[i + 2] != 'g' || b[i + 3] != 'S' || b[i + 4] != 0) continue;
                long g = BitConverter.ToInt64(b, i + 6);
                return g > 0 && rate > 0 ? g / (double)rate : 0;
            }
            return 0;
        }

        private static int ReadAt(FileStream fs, long pos, byte[] buf, int len)
        {
            fs.Position = pos;
            int got = 0;
            while (got < len) { int k = fs.Read(buf, got, len - got); if (k <= 0) break; got += k; }
            return got;
        }

        private static bool Zeros(FileStream fs, long pos)
        {
            var b = new byte[ZeroBlock];
            int got = ReadAt(fs, pos, b, ZeroBlock);
            if (got < ZeroBlock) return false;
            for (int i = 0; i < got; i++) if (b[i] != 0) return false;
            return true;
        }

        internal static string Md5File(string path)
        {
            try
            {
                using (var md5 = MD5.Create())
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return Hex(md5.ComputeHash(fs));
            }
            catch { return null; }
        }

        internal static string Hex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            foreach (byte x in b) sb.Append(x.ToString("x2", Inv));
            return sb.ToString();
        }

        // ------------------------------------------------------------ instalacja (OnBeforeInitialModuleScreenSetAsRoot - ROT juz wczytany)
        /// <summary>Raz na proces: nagrania okien i latka EventScreen (odbiciem). Linia "Lektor (m103): latka EventScreen ...". Nie rzuca.</summary>
        internal static void Install(Harmony h)
        {
            if (_installed) return;
            _installed = true;
            LoadIndex();
            try
            {
                Type t = AccessTools.TypeByName("ROT.Screens.EventScreen");
                if (t == null) { _patch = "brak typu ROT.Screens.EventScreen (ROT nie wczytany)"; }
                else if (h == null) { _patch = "brak Harmony"; }
                else
                {
                    _stateType = AccessTools.TypeByName("ROT.CampaignBehaviors.EventPopupState");
                    _fData = AccessTools.Field(t, "_dataSource");
                    _fState = AccessTools.Field(t, "_eventPopupState");
                    MethodInfo act = null, deact = null;
                    try
                    {
                        InterfaceMapping map = t.GetInterfaceMap(typeof(IGameStateListener));
                        for (int i = 0; i < map.InterfaceMethods.Length; i++)
                        {
                            if (map.InterfaceMethods[i].Name == "OnActivate") act = map.TargetMethods[i];
                            else if (map.InterfaceMethods[i].Name == "OnDeactivate") deact = map.TargetMethods[i];
                        }
                    }
                    catch (Exception e) { Stumble("Install.GetInterfaceMap", e); }
                    if (act == null) act = AccessTools.Method(t, "TaleWorlds.Core.IGameStateListener.OnActivate");
                    if (deact == null) deact = AccessTools.Method(t, "TaleWorlds.Core.IGameStateListener.OnDeactivate");
                    if (act != null)
                    {
                        try { h.Patch(act, postfix: new HarmonyMethod(typeof(Lektor), nameof(OnActivatePost))); }
                        catch (Exception e) { Stumble("Install.Patch(OnActivate)", e); }
                    }
                    if (deact != null)
                    {
                        try { h.Patch(deact, prefix: new HarmonyMethod(typeof(Lektor), nameof(OnDeactivatePre))); }
                        catch (Exception e) { Stumble("Install.Patch(OnDeactivate)", e); }
                    }
                    bool a = act != null && HasOurs(act, true), d = deact != null && HasOurs(deact, false);
                    _patch = "OnActivate " + (act == null ? "BRAK METODY" : a ? "tak" : "NIE") + ", OnDeactivate " + (deact == null ? "BRAK METODY" : d ? "tak" : "NIE")
                             + (_fData == null ? ", BRAK pola _dataSource" : "") + (_stateType == null ? ", brak typu EventPopupState (straznik po nazwie)" : "");
                }
            }
            catch (Exception e) { _patch = "blad " + e.GetType().Name; Stumble("Install", e); }
            Log.Info(P + "latka EventScreen: " + _patch + " | " + IndexText() + ".");
        }

        private static bool HasOurs(MethodBase m, bool postfix)
        {
            try
            {
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return false;
                foreach (var p in postfix ? info.Postfixes : info.Prefixes)
                    if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(Lektor)) return true;
            }
            catch (Exception e) { Stumble("HasOurs", e); }
            return false;
        }

        private static string IndexText()
        {
            return "nagran okien " + _idxRows.ToString(Inv) + " (" + _idxSec.ToString("0", Inv) + " s; " + _indexState
                   + (_idxBad > 0 ? ", odrzucone " + _idxBad.ToString(Inv) + ": " + _idxBadFirst : ", odrzucone 0")
                   + (_idxDup > 0 ? ", dubli tytulu " + _idxDup.ToString(Inv) : "") + (_idxBadLines > 0 ? ", zlych linii " + _idxBadLines.ToString(Inv) : "")
                   + (_idxZeroLines > 0 ? ", linii z zerami " + _idxZeroLines.ToString(Inv) : "") + ", bez md5 " + _idxNoMd5.ToString(Inv) + ")"
                   + " | " + KronikaText();
        }

        /// <summary>m105-kronika K0 (A3): "nagran Kroniki N (ogg n; stan, ze starym tekstem s, odrzucone r: pierwszy ...)" - do linii latki, startu lektora i startu Kroniki.</summary>
        internal static string KronikaText()
        {
            LoadIndex();
            return "nagran Kroniki " + _krRows.ToString(Inv) + " (ogg " + _krOgg.ToString(Inv) + "; " + _krState
                   + ", ze starym tekstem " + _krStale.ToString(Inv)
                   + (_krBad > 0 ? ", odrzucone " + _krBad.ToString(Inv) + ": " + _krBadFirst : ", odrzucone 0")
                   + (_krDup > 0 ? ", dubli klucza " + _krDup.ToString(Inv) : "") + (_krBadLines > 0 ? ", zlych linii " + _krBadLines.ToString(Inv) : "")
                   + (_krZeroLines > 0 ? ", linii z zerami " + _krZeroLines.ToString(Inv) : "") + (_krNoMd5 > 0 ? ", bez md5 pliku " + _krNoMd5.ToString(Inv) : "") + ")";
        }

        /// <summary>m105-kronika K0 (A3): czy okno opowiesci z kluczem key i md5 tekstu textMd5 dostanie glos. null = tak (klucze lektora sprawdza wolajacy -
        /// WindowsEnabled); inaczej powod ("brak nagrania", "plik ... odrzucony (...)", "tekst okna inny niz nagrany ...", "md5 tekstu w manifescie ..."). Klucz spoza
        /// Kroniki (prolog_NN) - bez kontroli tekstu, jak dotad. Bez wyjatkow.</summary>
        internal static string VoiceWhy(string key, string textMd5)
        {
            try
            {
                LoadIndex();
                if (string.IsNullOrEmpty(key)) return "brak klucza";
                Rec r;
                if (!_byKey.TryGetValue(key, out r))
                {
                    string rej;
                    return _krRejected.TryGetValue(key, out rej) ? rej : "brak nagrania";
                }
                return r.Kronika ? TextWhy(r, textMd5) : null;
            }
            catch (Exception e) { Stumble("VoiceWhy", e); return "blad " + e.GetType().Name; }
        }

        /// <summary>Zasada md5 Kroniki: null = tekst okna = tekst nagrania; inaczej powod (cisza).</summary>
        private static string TextWhy(Rec r, string textMd5)
        {
            if (r.Stale != null || r.TextMd5 == null) return r.Stale ?? "brak md5 tekstu w manifescie";
            if (string.IsNullOrEmpty(textMd5)) return "okno bez md5 tekstu (zmienne w tekscie?)";
            if (!string.Equals(r.TextMd5, textMd5, StringComparison.OrdinalIgnoreCase))
                return "tekst okna inny niz nagrany (md5 okna " + Md5Short(textMd5) + ", nagrania " + Md5Short(r.TextMd5) + ")";
            return null;
        }

        private static string Md5Short(string md5) { return md5 != null && md5.Length > 8 ? md5.Substring(0, 8) : md5 ?? "-"; }

        // ------------------------------------------------------------ latki okna ROT
        private static string TitleOf(object screen)
        {
            object ds = _fData != null ? _fData.GetValue(screen) : null;
            string t = Prop(ds, "EventTitle") as string;
            if (!string.IsNullOrEmpty(t)) return t;
            object st = _fState != null ? _fState.GetValue(screen) : null;   // zapas: _eventPopupState.Event.Title (to samo, co EventPanel.EventTitle)
            object ev = Prop(st, "Event");
            return Prop(ev, "Title") as string;
        }

        /// <summary>m105-kronika K0 (A3): klucz nagrania i md5 tekstu okna opowiesci Armoury (ArmStoryEvent.Key / VoiceMd5) z _eventPopupState.Event - typ po nazwie
        /// (okna ROT nie maja klucza: key = null, szukanie po tytule jak dotad).</summary>
        private static void KeyOf(object screen, out string key, out string md5)
        {
            key = md5 = null;
            object st = _fState != null ? _fState.GetValue(screen) : null;
            object ev = Prop(st, "Event");
            if (ev == null || ev.GetType().FullName != StoryEventType) return;
            key = Prop(ev, "Key") as string;
            md5 = Prop(ev, "VoiceMd5") as string;
        }

        private static object Prop(object o, string name)
        {
            if (o == null) return null;
            Type t = o.GetType();
            string k = t.FullName + "|" + name;
            PropertyInfo p;
            lock (_props)
            {
                if (!_props.TryGetValue(k, out p)) { p = AccessTools.Property(t, name); _props[k] = p; }
            }
            return p != null ? p.GetValue(o, null) : null;
        }

        /// <summary>Postfiks EventScreen.IGameStateListener.OnActivate: tytul -> nagranie -> odtworzenie (poprzedni klip zatrzymany). Nie rzuca.</summary>
        private static void OnActivatePost(object __instance)
        {
            try
            {
                _dWinOpen++;
                if (!WindowsOn) { _dWinOff++; return; }
                if (_lastScreen != null && ReferenceEquals(_lastScreen.Target, __instance)) { _dWinRepeat++; Log.Info(P + "okno ROT aktywowane ponownie - bez powtorki."); return; }
                _lastScreen = new WeakReference(__instance);
                LoadIndex();
                string title = TitleOf(__instance);
                // m105-kronika K0 (A3, P9): najpierw klucz okna opowiesci Armoury (Kronika kr_NNN_w[war], sceny prologu prolog_NN), potem tytul
                string key, vmd5;
                KeyOf(__instance, out key, out vmd5);
                Rec r = null;
                string via = "";
                if (!string.IsNullOrEmpty(key))
                {
                    if (_byKey.TryGetValue(key, out r))
                    {
                        via = " Po kluczu " + key + (r.Kronika ? " (Kronika, md5 tekstu zgodny)" : "") + ".";
                        if (r.Kronika)
                        {
                            string tw = TextWhy(r, vmd5);
                            if (tw != null) { _dWinStale++; Log.Info(P + "okno ROT \"" + title + "\" (klucz " + key + ") - " + tw + ", cisza."); return; }
                        }
                        _dWinKey++;
                    }
                    else if (key.StartsWith(KronikaPrefix, StringComparison.Ordinal))
                    {
                        // okno Kroniki bez nagrania - bez szukania po tytule (inny wariant moze miec ten sam tytul i inny tekst)
                        string rej;
                        _dWinNoRec++;
                        Log.Info(P + "okno ROT \"" + title + "\" (klucz " + key + ") - " + (_krRejected.TryGetValue(key, out rej) ? rej : "brak nagrania Kroniki") + ", cisza.");
                        return;
                    }
                }
                if (r == null)
                {
                    if (string.IsNullOrEmpty(title)) { _dWinNoRec++; Log.Info(P + "okno ROT bez tytulu - cisza."); return; }
                    r = Find(title);
                }
                if (r == null) { _dWinNoRec++; Log.Info(P + "okno ROT \"" + title + "\" - brak nagrania, cisza."); return; }
                _dWinRec++;
                double sec;
                string why = CheckAudio(r.Path, r.Md5, out sec);
                if (why != null) { _dWinBad++; Log.Info(P + "okno ROT \"" + title + "\" - plik " + r.File + " uszkodzony (" + why + "), cisza."); return; }
                if (sec <= 0) sec = r.Sec;
                GossipStopAll("okno ROT");
                if (Play(r.Path, KWindow, title, sec))
                {
                    _dWinPlay++; _sWinPlay++;
                    Log.Info(P + "okno ROT \"" + title + "\" -> " + r.File + " (" + sec.ToString("0.0", Inv) + " s) - start." + via);
                }
            }
            catch (Exception e) { Stumble("OnActivatePost", e); }
        }

        /// <summary>Prefiks EventScreen.IGameStateListener.OnDeactivate: zamkniecie okna (Done, Cancel, Esc, Enter, PopState) - stop klipu okna. Nie rzuca.</summary>
        private static void OnDeactivatePre(object __instance)
        {
            try { if (_ev != null && _evKind == KWindow) StopCurrent("zamkniecie okna"); }
            catch (Exception e) { Stumble("OnDeactivatePre", e); }
        }

        private static bool EventStateActive()
        {
            var gsm = GameStateManager.Current;
            var st = gsm != null ? gsm.ActiveState : null;
            if (st == null) return false;
            Type t = st.GetType();
            return _stateType != null ? _stateType.IsAssignableFrom(t) : t.FullName == "ROT.CampaignBehaviors.EventPopupState";
        }

        // ------------------------------------------------------------ odtwarzacz
        /// <summary>Glowny watek: zatrzymanie poprzedniego klipu, nowe zdarzenie z pliku, Play. false = cisza (linia w logu).</summary>
        private static bool Play(string path, int kind, string tag, double len)
        {
            StopCurrent("nowy klip");
            SoundEvent ev = null;
            try { ev = SoundEvent.CreateEventFromExternalFile(VoiceEvent, path, (Scene)null, false, false); }
            catch (Exception e) { Stumble("Play.CreateEventFromExternalFile", e); return false; }
            bool valid = false;
            try { valid = ev != null && ev.IsValid; } catch (Exception e) { Stumble("Play.IsValid", e); }
            if (!valid)
            {
                _dCreateFail++;
                Log.Info(P + (kind == KWindow ? "okno ROT \"" + tag + "\"" : "plotka " + tag) + " - silnik nie przyjal pliku (zdarzenie dzwieku niewazne), cisza.");
                return false;
            }
            bool ok = false;
            try { ok = ev.Play(); } catch (Exception e) { Stumble("Play.Play", e); }
            if (!ok)
            {
                _dCreateFail++;
                try { ev.Release(); } catch (Exception e) { Stumble("Play.Release", e); }
                Log.Info(P + (kind == KWindow ? "okno ROT \"" + tag + "\"" : "plotka " + tag) + " - Play() zwrocil false, cisza.");
                return false;
            }
            _ev = ev; _evKind = kind; _evTag = tag; _evT0 = Now; _evLen = len; _ev05 = false;
            return true;
        }

        /// <summary>Zatrzymanie biezacego klipu (Release jak w grze); linia dla klipu okna.</summary>
        private static void StopCurrent(string why)
        {
            var ev = _ev;
            if (ev == null) return;
            _ev = null;
            double t = Now - _evT0;
            try { ev.Release(); } catch (Exception e) { Stumble("StopCurrent.Release", e); }
            if (_evKind == KWindow) Log.Info(P + "okno ROT \"" + _evTag + "\" - stop po " + t.ToString("0.0", Inv) + " s z " + _evLen.ToString("0.0", Inv) + " s (" + why + ").");
            else GossipClipStopped(why, t);
        }

        /// <summary>SubModuleMain.OnApplicationTick (co klatke): koniec klipu, kontrola 0,5 s, straznik okna, plotki. Nic nie gra i nic nie czeka - od razu wyjscie.</summary>
        internal static void AppTick()
        {
            bool busy = _ev != null;
            GossipBusy(ref busy);
            if (!busy) return;
            try { TickClip(); } catch (Exception e) { Stumble("AppTick.Clip", e); }
            try { GossipTick(); } catch (Exception e) { Stumble("AppTick.Gossip", e); }
        }

        private static void TickClip()
        {
            var ev = _ev;
            if (ev == null) return;
            double t = Now - _evT0;
            if (!ev.IsValid || ev.IsStopped())
            {
                _ev = null;
                try { if (ev.IsValid) ev.Release(); } catch (Exception e) { Stumble("TickClip.Release", e); }
                bool early = !_ev05;
                if (_evKind == KWindow)
                {
                    if (early) _dWinEarly++;
                    Log.Info(P + "okno ROT \"" + _evTag + "\" - koniec po " + t.ToString("0.0", Inv) + " s z " + _evLen.ToString("0.0", Inv) + " s"
                             + (early ? " - ZA WCZESNIE (przed kontrola 0,5 s: silnik nie gra pliku?)" : "") + ".");
                }
                else GossipClipEnded(t, early);
                return;
            }
            if (!_ev05 && t >= 0.5)
            {
                _ev05 = true;
                bool playing = false, paused = false;
                try { playing = ev.IsPlaying(); paused = ev.IsPaused(); } catch (Exception e) { Stumble("TickClip.IsPlaying", e); }
                if (_evKind == KWindow)
                {
                    if (playing) { _dWin05Yes++; _sWin05Yes++; } else _dWin05No++;
                    Log.Info(P + "okno ROT \"" + _evTag + "\" - grane po 0,5 s: " + (playing ? "tak" : "NIE") + (paused ? " (pauza silnika)" : "") + ".");
                }
                else GossipClipChecked(playing, paused);
            }
            if (_evKind == KWindow && !EventStateActive()) StopCurrent("straznik: aktywny stan gry to nie okno ROT");
        }

        // ------------------------------------------------------------ plotki (Lektor.Live.cs); bez tego pliku - nic
        static partial void GossipBusy(ref bool busy);
        static partial void GossipTick();
        static partial void GossipStopAll(string why);
        static partial void GossipClipStopped(string why, double t);
        static partial void GossipClipEnded(double t, bool early);
        static partial void GossipClipChecked(bool playing, bool paused);
        static partial void GossipReset();
        static partial void GossipSessionText(StringBuilder sb);
        static partial void GossipDayText(StringBuilder sb);

        // ------------------------------------------------------------ sesja, doba, reset
        /// <summary>Nowe zachowanie kampanii (ArmouryBehavior, OnGameStart): stop klipu i stan strony od zera; liczniki doby od zera. Glowny watek.</summary>
        internal static void Reset()
        {
            try { StopCurrent("nowa kampania / wczytanie"); } catch (Exception e) { Stumble("Reset.Stop", e); }
            GossipReset();
            _lastScreen = null;
            _dWinOpen = _dWinRec = _dWinPlay = _dWin05Yes = _dWin05No = _dWinNoRec = _dWinBad = _dWinOff = _dWinRepeat = _dWinEarly = 0;
            _dWinKey = _dWinStale = 0;
            _dCreateFail = _dStumbles = 0; _stumbles = 0; _sWinPlay = _sWin05Yes = 0;
        }

        private static string YesNo(bool b) { return b ? "TAK" : "NIE"; }

        /// <summary>ArmouryBehavior.OnSessionLaunched: linia "Lektor (m103): start sesji | ...".</summary>
        internal static void SessionStart()
        {
            var s = Settings.Current;
            LoadIndex();
            var sb = new StringBuilder(P + "start sesji | klucze: lektor " + YesNo(s != null && s.LektorEnabled) + ", okna " + YesNo(s != null && s.LektorEventWindows)
                                       + ", plotki na zywo " + YesNo(s != null && s.LektorGossipLive) + ", limit dzienny " + (s != null ? s.LektorGossipDailyChars : 0).ToString(Inv)
                                       + ", rezerwa " + (s != null ? s.LektorGossipReserveChars : 0).ToString(Inv) + " | " + IndexText() + " | latka EventScreen: " + _patch);
            GossipSessionText(sb);
            sb.Append(" | tryb ").Append(ModeName()).Append('.');
            Log.Info(sb.ToString());
        }

        private static string ModeName()
        {
            int m = LogGate.Mode;
            return m == 0 ? "gra" : m == 1 ? "autotest" : m == 2 ? "profil autotestu" : "nie rozpoznany";
        }

        /// <summary>ArmouryBehavior, takt doby: linia "Lektor (m103): dzien N | ..." i liczniki doby od zera.</summary>
        internal static void Daily(int day)
        {
            var sb = new StringBuilder(P + "dzien " + day.ToString(Inv) + " | okna ROT: otwarte " + _dWinOpen.ToString(Inv) + ", z nagraniem " + _dWinRec.ToString(Inv)
                                       + ", odtworzone " + _dWinPlay.ToString(Inv) + " (grane po 0,5 s tak " + _dWin05Yes.ToString(Inv) + " / nie " + _dWin05No.ToString(Inv)
                                       + ", koniec za wczesnie " + _dWinEarly.ToString(Inv) + "), bez nagrania " + _dWinNoRec.ToString(Inv) + ", uszkodzone " + _dWinBad.ToString(Inv)
                                       + ", klucz wylaczony " + _dWinOff.ToString(Inv) + ", ponowna aktywacja " + _dWinRepeat.ToString(Inv) + ", silnik nie przyjal " + _dCreateFail.ToString(Inv)
                                       + ", po kluczu " + _dWinKey.ToString(Inv) + ", Kronika z innym tekstem " + _dWinStale.ToString(Inv));
            GossipDayText(sb);
            sb.Append(" | potkniecia ").Append(_dStumbles.ToString(Inv)).Append(" (w sesji ").Append(_stumbles.ToString(Inv)).Append(") | tryb ").Append(ModeName()).Append('.');
            Log.Info(sb.ToString());
            _dWinOpen = _dWinRec = _dWinPlay = _dWin05Yes = _dWin05No = _dWinNoRec = _dWinBad = _dWinOff = _dWinRepeat = _dWinEarly = 0;
            _dWinKey = _dWinStale = 0;
            _dCreateFail = _dStumbles = 0;
        }
    }
}
