using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// zapis-d4 (noc 17/18.10, CO-ZOSTALO D4 "logi ciszej i rozmiar zapisu"): ROZMIAR NASZYCH KLUCZY W ZAPISIE.
    ///
    /// Pomiar na prawdziwym zapisie (autotest-2026-10-17-2109.sav, fin83 calosc 100eaedf, kampania d240 + 120 dob; rozbior pliku: meta + deflate(GameData) -
    /// naglowek, obiekty, kontenery, napisy): plik 36.8 MB = naglowek 4.9 MB + obiekty 9.4 MB + kontenery 19.7 MB + napisy 2.7 MB (po kompresji gry).
    /// NASZE napisy (wartosci kluczy arm_*) to ok. 7.4 MB tekstu = ok. 1.75 MB pliku (4.7%); reszta zapisu to stan gry, BK, DTE i ROT (partie, wojsko,
    /// przedmioty) - tego ta paczka nie zmienia. Najwieksze nasze klucze: arm_recruitkit ok. 2.1 MB tekstu (komplety ochotnikow u notabli), arm_aiwear
    /// ok. 2.1 MB (obite sztuki w zbrojowniach AI: 797 partii, 37 tys. wpisow, 152 tys. sztuk), arm_people ok. 0.8 MB (ksiega ludzi; 0.58 MB to pochodzenie
    /// ludzi 4567 partii), arm_garrisonarmory ok. 0.58 MB (zbrojownie zalog, 503 tys. sztuk); reszta po 0.2 MB i mniej. Wzrost w czasie maly (zapis d720
    /// biegu 6-letniego: 7.7 MB tekstu) - zadna lista nie rosnie bez konca.
    ///
    /// Trzy czesci (kazda pod wlasnym kluczem MCM; wszystkie wylaczone = SaveText.Sync co do bajtu jak dotad, poza odczytem nowego klucza "_z" przy
    /// wczytaniu, ktorego w starym zapisie nie ma):
    ///  1. SaveSizeLog - przy kazdym zapisie gry rozmiar kazdego klucza SaveText (znaki, bajty UTF-8, kawalki) i liczba wpisow kolekcji zapisywanych wprost
    ///     (arm_condition, arm_player_stock ...); po zapisie (CampaignEvents.OnSaveOverEvent) JEDNA linia "Zapis (D4): ..." z suma, kluczami malejaco,
    ///     rozmiarem pliku zapisu i czasem calego zapisu (od OnBeforeSave).
    ///  2. SaveCompact - klucze z listy Packable dluzsze niz SaveCompactMinKb (KB tekstu) ida do zapisu SPAKOWANE pod NOWYM kluczem "&lt;klucz&gt;_z"
    ///     (lista napisow: naglowek "z1|znaki|fnv32|bajty" + base64(deflate(UTF-8)) w kawalkach po 8000 znakow); stary klucz dostaje "". Przed zapisem
    ///     PROBNY ODCZYT (rozpakowanie i porownanie znak w znak) - niezgodnosc albo wyjatek = klucz idzie stara droga (nic nie ginie), potkniecie do linii.
    ///     Wczytanie czyta "_z" ZAWSZE (takze przy wylaczonym kluczu - wylaczenie nie gubi stanu; nastepny zapis znowu stara droga). Starszy DLL klucza "_z"
    ///     nie zna: stary klucz ma "" = jak zapis bez danych tego klucza (arm_aiwear: sztuki AI bez zapisu obicia - sprawne; arm_recruitkit: notable bez
    ///     zapisanych kompletow - ochotnik "z tym, co ma"). Dlatego na liscie tylko te dwa klucze (lacznie ok. 57% naszego tekstu), a nie ksiega ludzi,
    ///     kiesy czy zbrojownie zalog (starszy DLL zgubilby ludzi, zloto i sztuki). Przed cofnieciem DLL ponizej tej paczki: wylaczyc Save Compact,
    ///     zapisac gre, dopiero potem podmienic DLL (CHANGELOG). Recenzja: klucz DOMYSLNIE WYLACZONY - wgranie zbiorcze nocy moze wymagac cofniecia DLL
    ///     do .bak, a w Ironman nie ma zapisu do powrotu; wlaczyc, gdy cofniecie ponizej tej paczki nie bedzie juz potrzebne.
    ///     Zysk w pliku (pomiar na tym samym zapisie): oba klucze 705 KB pliku -> ok. 500 KB (gra kompresuje szybko, my dokladniej); napisy zapisu w pamieci
    ///     4.2 MB -> 0.65 MB (mniej kawalkow po 8000 znakow do skrotow, kodowania i dekodowania gry).
    ///  3. QuietLogMore (LogGate.DropMore) - patrz LogGate.cs.
    /// Wyjatki: per klucz licznik potkniec (pierwsze 3 do logu), bez wylacznika na sesje (CLAUDE.md 7).
    /// </summary>
    internal static class SaveSize
    {
        internal const string ZSuffix = "_z";
        private const string ZHead = "z1";
        private const int Part = 8000;   // jak SaveText.Part: kawalek < 32767 B
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        // klucze, ktore wolno pakowac (starszy DLL czyta wtedy "" - skutki opisane wyzej i w CHANGELOG); kolejnosc bez znaczenia
        private static readonly string[] Packable = { "arm_aiwear", "arm_recruitkit" };

        private struct Row { public int Chars; public long Bytes; public int Parts; public long Packed; }

        // ---------------------------------------------------------------- stan biezacego zapisu (od OnBeforeSave do OnSaveOver)
        private static readonly Dictionary<string, Row> _rows = new Dictionary<string, Row>(StringComparer.Ordinal);
        private static readonly List<string> _coll = new List<string>();
        private static long _saveStart;                  // Stopwatch.GetTimestamp() przy OnBeforeSave (0 = nie bylo)
        private static long _packTicks;                  // czas pakowania i probnego odczytu
        private static int _packTried, _packOk, _packBad;
        private static int _stumbles;                    // potkniecia sesji (pomiar, pakowanie, odczyt) - do linii
        private static int _saves;                       // zapisow w tej sesji (linia)

        // ---------------------------------------------------------------- wczytanie
        private static readonly List<string> _loaded = new List<string>();
        private static int _loadBad;

        private static bool LogOn { get { var s = Settings.Current; return s != null && s.SaveSizeLog; } }
        private static bool PackOn { get { var s = Settings.Current; return s != null && s.SaveCompact; } }

        // ---------------------------------------------------------------- f4b-zapis: nasze napisy W PLIKU (klucz Save Size Log File, tylko przy Save Size Log)
        // Gra pakuje caly zapis deflate "Fastest" (FileDriver/GameData) - te same napisy, ktore ida do zapisu (tekst klucza albo lista "_z"), przez DeflateStream
        // Fastest do licznika bajtow: wynik = ok. ile nasze dane zajmuja w pliku. Bez kopii w pamieci (kawalki po 16 tys. znakow do bufora 48 KB, ponizej LOH).
        private static bool FileOn { get { var s = Settings.Current; return s != null && s.SaveSizeLog && s.SaveSizeLogFile; } }
        private const int FeedChars = 16 * 1024;
        private static CountStream _fcount;
        private static DeflateStream _fz;
        private static byte[] _fbuf;
        private static long _fticks, _fbytes = -1;

        /// <summary>Strumien, ktory tylko liczy zapisane bajty (wyjscie deflate pomiaru pliku).</summary>
        private sealed class CountStream : Stream
        {
            internal long Count;
            public override bool CanRead { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override long Length { get { return Count; } }
            public override long Position { get { return Count; } set { throw new NotSupportedException(); } }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { Count += count; }
        }

        private static void FileReset()
        {
            try { if (_fz != null) _fz.Dispose(); } catch { }
            _fz = null; _fcount = null; _fticks = 0; _fbytes = -1;
        }

        /// <summary>Napis, ktory idzie do zapisu, do pomiaru pliku (UTF-8 kawalkami, bez dzielenia pary zastepczej).</summary>
        private static void FileFeed(string v)
        {
            if (string.IsNullOrEmpty(v) || _fbytes == -2) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (_fz == null)
                {
                    _fcount = new CountStream();
                    _fz = new DeflateStream(_fcount, CompressionLevel.Fastest, true);
                }
                if (_fbuf == null) _fbuf = new byte[FeedChars * 3 + 8];
                int i = 0;
                while (i < v.Length)
                {
                    int n = Math.Min(FeedChars, v.Length - i);
                    if (n < v.Length - i && char.IsHighSurrogate(v[i + n - 1])) n--;
                    int b = Utf8.GetBytes(v, i, n, _fbuf, 0);
                    _fz.Write(_fbuf, 0, b);
                    i += n;
                }
            }
            catch (Exception e) { Stumble("FileFeed", e); FileReset(); _fbytes = -2; }   // -2 = pomiar tego zapisu przerwany (linia mowi "blad"), zapis gry bez zmian
            _fticks += Stopwatch.GetTimestamp() - t0;
        }

        /// <summary>Koniec pomiaru pliku (OnSaveOver): bajty deflate albo -1 (brak pomiaru) / -2 (blad).</summary>
        private static long FileFinish()
        {
            if (_fbytes == -2) return -2;
            if (_fz == null) return -1;
            long t0 = Stopwatch.GetTimestamp();
            try { _fz.Dispose(); _fbytes = _fcount != null ? _fcount.Count : -1; }
            catch (Exception e) { Stumble("FileFinish", e); _fbytes = -2; }
            _fticks += Stopwatch.GetTimestamp() - t0;
            _fz = null; _fcount = null;
            return _fbytes;
        }

        internal static int MinChars
        {
            get
            {
                var s = Settings.Current;
                int kb = s != null ? s.SaveCompactMinKb : 64;
                if (kb < 8) kb = 8; else if (kb > 4096) kb = 4096;
                return kb * 1024;
            }
        }

        internal static bool IsPackable(string key)
        {
            if (key == null) return false;
            for (int i = 0; i < Packable.Length; i++) if (string.Equals(Packable[i], key, StringComparison.Ordinal)) return true;
            return false;
        }

        private static void Stumble(string where, Exception e)
        {
            if (++_stumbles <= 3) Log.Error("SaveSize." + where + " (potkniecie " + _stumbles + ")", e);
        }

        // ================================================================ zapis
        /// <summary>CampaignEvents.OnBeforeSaveEvent: nowy pomiar (gra wola SyncData zachowan miedzy tym a OnSaveOver).</summary>
        internal static void BeforeSave()
        {
            _rows.Clear(); _coll.Clear();
            _packTicks = 0; _packTried = _packOk = _packBad = 0;
            _saveStart = Stopwatch.GetTimestamp();
            FileReset();   // f4b-zapis: nowy pomiar pliku
        }

        /// <summary>
        /// Z SaveText.Sync przy zapisie (PRZED stara droga): pomiar klucza (SaveSizeLog) i - dla klucza z listy Packable ponad prog przy wlaczonym SaveCompact -
        /// zapis spakowany. true = klucz juz zapisany tutaj (stara droga pominieta); false = stara droga jak dotad. Cala praca, ktora moze sie nie udac
        /// (pakowanie, probny odczyt), dzieje sie PRZED pierwszym ds.SyncData - po nim nie ma juz powrotu do starej drogi (drugi SyncData tego samego klucza
        /// rzucilby wyjatek gry i zgubil klucz).
        /// </summary>
        internal static bool TrySave(IDataStore ds, string key, string v)
        {
            Row row = default(Row);
            bool measure = false;
            try
            {
                measure = LogOn;
                if (measure)
                {
                    row.Chars = v.Length;
                    row.Bytes = Utf8.GetByteCount(v);
                    row.Parts = v.Length <= Part ? 1 : (v.Length + Part - 1) / Part;
                    _rows[key] = row;
                }
            }
            catch (Exception e) { Stumble("Measure", e); measure = false; }
            List<string> z = null;
            try
            {
                if (PackOn && IsPackable(key) && v.Length >= MinChars)
                {
                    _packTried++;
                    long t0 = Stopwatch.GetTimestamp();
                    z = Pack(v);
                    string back = Unpack(z, out long packedBytes);
                    _packTicks += Stopwatch.GetTimestamp() - t0;
                    if (back == null || !string.Equals(back, v, StringComparison.Ordinal))
                    {
                        _packBad++;
                        if (++_stumbles <= 3) Log.Info("Zapis (D4): BLAD probnego odczytu klucza " + key + " (" + v.Length + " znakow) - klucz idzie stara droga (bez pakowania), nic nie ginie.");
                        z = null;
                    }
                    else
                    {
                        _packOk++;
                        if (measure) { row.Packed = packedBytes; _rows[key] = row; }
                    }
                }
            }
            catch (Exception e) { Stumble("Pack(" + key + ")", e); z = null; }
            // f4b-zapis: do pomiaru pliku to, co naprawde idzie do zapisu (tekst klucza albo lista "_z"); FileFeed lapie swoje wyjatki - zapis bez zmian
            if (measure && FileOn)
            {
                if (z == null) FileFeed(v);
                else for (int i = 0; i < z.Count; i++) FileFeed(z[i]);
            }
            if (z == null) return false;
            // od tej chwili bez powrotu: "" pod starym kluczem (starszy DLL czyta pusty stan), dane pod nowym kluczem "_z"
            string head = "";
            ds.SyncData(key, ref head);
            ds.SyncData(key + ZSuffix, ref z);
            return true;
        }

        /// <summary>Z ArmouryBehavior.SyncData przy zapisie: kolekcje zapisywane wprost przez gre (kontenery, nie napisy) - tylko liczba wpisow i znakow.</summary>
        internal static void NoteList(string key, ICollection<string> list)
        {
            try
            {
                if (!LogOn) return;
                long ch = 0; int n = 0;
                if (list != null) foreach (var x in list) { n++; if (x != null) ch += x.Length; }
                _coll.Add(key + " " + n + " (" + (ch / 1024.0).ToString("0.0", Inv) + " KB)");
            }
            catch (Exception e) { Stumble("NoteList", e); }
        }

        internal static void NoteDict(string key, IDictionary<string, int> d)
        {
            try
            {
                if (!LogOn) return;
                long ch = 0; int n = 0;
                if (d != null) foreach (var kv in d) { n++; if (kv.Key != null) ch += kv.Key.Length + 4; }
                _coll.Add(key + " " + n + " (" + (ch / 1024.0).ToString("0.0", Inv) + " KB)");
            }
            catch (Exception e) { Stumble("NoteDict", e); }
        }

        /// <summary>CampaignEvents.OnSaveOverEvent (takze nieudany zapis): JEDNA linia "Zapis (D4)" i wyczyszczenie pomiaru.</summary>
        internal static void SaveOver(bool ok, string name)
        {
            try
            {
                _saves++;
                if (FileOn) FileFinish();   // f4b-zapis: bajty deflate Fastest naszych napisow (przed linia)
                if (LogOn && (_rows.Count > 0 || _coll.Count > 0)) Log.Info(Line(ok, name));
            }
            catch (Exception e) { Stumble("SaveOver", e); }
            _rows.Clear(); _coll.Clear(); _saveStart = 0;
            FileReset();   // f4b-zapis
        }

        private static string Kb(long b) { return (b / 1024.0).ToString("0.0", Inv); }

        private static string Line(bool ok, string name)
        {
            long bytes = 0, written = 0, chunked = 0, packedRaw = 0, packed = 0;
            int multi = 0, nPacked = 0;
            var keys = new List<KeyValuePair<string, Row>>(_rows);
            foreach (var kv in keys)
            {
                var r = kv.Value;
                bytes += r.Bytes;
                if (r.Packed > 0) { nPacked++; packedRaw += r.Bytes; packed += r.Packed; written += r.Packed; }
                else written += r.Bytes;
                if (r.Parts > 1 && r.Packed <= 0) { multi++; chunked += r.Bytes; }
            }
            keys.Sort(delegate (KeyValuePair<string, Row> a, KeyValuePair<string, Row> b)
            {
                int c = b.Value.Bytes.CompareTo(a.Value.Bytes);
                return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
            });
            double ms = _packTicks * 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder(4096);
            sb.Append("Zapis (D4): ").Append(string.IsNullOrEmpty(name) ? "?" : name).Append(" | wynik ").Append(ok ? "TAK" : "NIE");
            sb.Append(" | kluczy SaveText ").Append(keys.Count).Append(" (kawalkami ").Append(multi).Append(", spakowanych ").Append(nPacked).Append(')');
            sb.Append(", tekst razem ").Append(Kb(bytes)).Append(" KB (UTF-8), w kawalkach ").Append(Kb(chunked)).Append(" KB");
            sb.Append(", do zapisu po spakowaniu ").Append(Kb(written)).Append(" KB");
            sb.Append(" | Save Compact ").Append(PackOn ? "[WL]" : "[wyl]").Append(" od ").Append(MinChars / 1024).Append(" KB: spakowane ").Append(Kb(packedRaw)).Append(" KB -> ").Append(Kb(packed)).Append(" KB");
            if (packed > 0) sb.Append(" (x").Append((packedRaw / (double)packed).ToString("0.0", Inv)).Append(')');
            sb.Append(", proba odczytu ").Append(_packOk).Append('/').Append(_packTried).Append(" (niezgodne ").Append(_packBad).Append("), czas pakowania ").Append(ms.ToString("0.0", Inv)).Append(" ms");
            sb.Append(" | kolekcje: ").Append(_coll.Count > 0 ? string.Join(", ", _coll.ToArray()) : "-");
            sb.Append(" | plik ").Append(FileText(name));
            if (_saveStart > 0) sb.Append(" | czas zapisu ").Append(((Stopwatch.GetTimestamp() - _saveStart) * 1000.0 / Stopwatch.Frequency).ToString("0", Inv)).Append(" ms");
            if (FileOn) sb.Append(FileSegment(name, written));   // f4b-zapis: nasze napisy w pliku (klucz Save Size Log File; wylaczony - linia jak dotad)
            sb.Append(" | ciche logi wiecej (D4): pominietych linii od startu sesji ").Append(LogGate.MoreDropped);
            sb.Append(" | zapisow w sesji ").Append(_saves).Append(" | potkniecia ").Append(_stumbles);
            sb.Append(" | klucze KB malejaco: ");
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(keys[i].Key).Append('=').Append(Kb(keys[i].Value.Bytes));
                if (keys[i].Value.Packed > 0) sb.Append("(z").Append(Kb(keys[i].Value.Packed)).Append(')');
            }
            sb.Append('.');
            return sb.ToString();
        }

        /// <summary>Rozmiar pliku zapisu (Documents\Mount and Blade II Bannerlord\Game Saves\nazwa.sav) - tylko FileInfo, bez czytania pliku.</summary>
        private static string FileText(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name)) return "?";
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var f = new FileInfo(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "Game Saves"), name + ".sav"));
                if (!f.Exists) return "? (brak pliku w Game Saves)";
                bool fresh = (DateTime.Now - f.LastWriteTime).TotalSeconds < 120;
                return (f.Length / 1048576.0).ToString("0.00", Inv) + " MB" + (fresh ? "" : " (plik sprzed zapisu)");
            }
            catch { return "?"; }
        }

        /// <summary>f4b-zapis: odcinek linii "Zapis (D4)" - ile nasze napisy zajmuja w pliku (deflate Fastest jak gra), udzial w pliku zapisu i czas pomiaru.
        /// Tozsamosc: tekst do zapisu (UTF-8) / bajty w pliku = stopien pakowania gry na naszych danych.</summary>
        private static string FileSegment(string name, long written)
        {
            string ms = (_fticks * 1000.0 / Stopwatch.Frequency).ToString("0", Inv);
            if (_fbytes == -2) return " | w pliku (deflate Fastest jak gra): BLAD pomiaru (zapis bez zmian), pomiar " + ms + " ms";
            if (_fbytes < 0) return " | w pliku (deflate Fastest jak gra): brak pomiaru";
            var sb = new StringBuilder(160);
            sb.Append(" | w pliku (deflate Fastest jak gra) ok. ").Append(Kb(_fbytes)).Append(" KB");
            if (_fbytes > 0) sb.Append(" (tekst do zapisu ").Append(Kb(written)).Append(" KB, x").Append((written / (double)_fbytes).ToString("0.0", Inv)).Append(')');
            long fl = FileLen(name);
            if (fl > 0) sb.Append(" = ").Append((100.0 * _fbytes / fl).ToString("0.0", Inv)).Append("% pliku");
            sb.Append(", pomiar ").Append(ms).Append(" ms");
            return sb.ToString();
        }

        /// <summary>f4b-zapis: dlugosc pliku zapisu w bajtach, tylko swiezy plik (zapisany w ostatnich 120 s); inaczej -1. Jak FileText - samo FileInfo.</summary>
        private static long FileLen(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name)) return -1;
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var f = new FileInfo(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "Game Saves"), name + ".sav"));
                if (!f.Exists || (DateTime.Now - f.LastWriteTime).TotalSeconds >= 120) return -1;
                return f.Length;
            }
            catch { return -1; }
        }

        // ================================================================ wczytanie
        /// <summary>
        /// Z SaveText.Sync przy wczytaniu (PO starej drodze): klucz "&lt;klucz&gt;_z" - rozpakowany napis albo null (brak klucza / blad - zostaje stara droga,
        /// czyli "" z zapisu spakowanego: pusty stan tego klucza, linia i potkniecie). Czytany dla KAZDEGO klucza (takze spoza listy - nowsza lista po cofnieciu
        /// DLL do tej wersji tez sie wczyta) i niezaleznie od kluczy MCM.
        /// </summary>
        internal static string TryLoad(IDataStore ds, string key)
        {
            List<string> z = null;
            try
            {
                if (!ds.SyncData(key + ZSuffix, ref z) || z == null || z.Count == 0) return null;
            }
            catch (Exception e) { Stumble("Load(" + key + ")", e); return null; }
            try
            {
                long packed;
                string s = Unpack(z, out packed);
                if (s == null)
                {
                    _loadBad++;
                    _loaded.Add(key + " BLAD (zly naglowek, dlugosc albo suma - stan klucza pusty)");
                    return null;
                }
                _loaded.Add(key + " " + Kb(packed) + " -> " + Kb(Utf8.GetByteCount(s)) + " KB TAK");
                return s;
            }
            catch (Exception e)
            {
                _loadBad++;
                _loaded.Add(key + " BLAD (" + e.GetType().Name + " - stan klucza pusty)");
                Stumble("Unpack(" + key + ")", e);
                return null;
            }
        }

        /// <summary>Z SaveText.ReportAfterLoad (OnSessionLaunched): linia startu sesji D4 - stan kluczy, tryb cichych logow, klucze spakowane wczytane z zapisu.</summary>
        internal static void SessionLine()
        {
            var s = Settings.Current;
            var sb = new StringBuilder(512);
            sb.Append("Zapis i logi (D4): pomiar kluczy przy zapisie (Save Size Log) ").Append(LogOn ? "[WL]" : "[wyl]");
            sb.Append(" | pakowanie (Save Compact) ").Append(PackOn ? "[WL]" : "[wyl]").Append(" od ").Append(MinChars / 1024).Append(" KB, klucze: ").Append(string.Join(", ", Packable));
            sb.Append(" | ciche logi wiecej (Quiet Log More) ").Append(s != null && s.QuietLogMore ? "[WL]" : "[wyl]").Append(" - ").Append(LogGate.MoreCount)
              .Append(" linii pomiarow tylko-log co ").Append(LogGate.Every).Append(" dob, dzialaja: ").Append(LogGate.MoreActive ? "tak" : "nie - pelne");
            sb.Append(" | wczytane spakowane: ").Append(_loaded.Count > 0 ? string.Join(", ", _loaded.ToArray()) : "brak").Append(" (bledy ").Append(_loadBad).Append(')');
            sb.Append('.');
            Log.Info(sb.ToString());
            // nowa sesja (wczytanie albo nowa kampania w tym samym procesie): liczniki linii "Zapis (D4)" od zera - potkniecia wczytania sa juz w liniach ERROR i w "bledy"
            _loaded.Clear(); _loadBad = 0; _saves = 0; _stumbles = 0;
            LogGate.ResetMore();
        }

        // ================================================================ kodek
        /// <summary>Napis -> ["z1|znaki|fnv32|bajty", base64 po 8000 znakow...]; deflate (Optimal) na UTF-8.</summary>
        private static List<string> Pack(string v)
        {
            byte[] raw = Utf8.GetBytes(v);
            byte[] packed;
            using (var ms = new MemoryStream(raw.Length / 4 + 64))
            {
                using (var zs = new DeflateStream(ms, CompressionLevel.Optimal, true)) zs.Write(raw, 0, raw.Length);
                packed = ms.ToArray();
            }
            string b64 = Convert.ToBase64String(packed);
            var list = new List<string>(b64.Length / Part + 2);
            list.Add(ZHead + "|" + v.Length.ToString(Inv) + "|" + Fnv(v).ToString("x8", Inv) + "|" + packed.Length.ToString(Inv));
            for (int i = 0; i < b64.Length; i += Part) list.Add(b64.Substring(i, Math.Min(Part, b64.Length - i)));
            return list;
        }

        /// <summary>Lista "_z" -> napis albo null (zly naglowek, dlugosc albo suma kontrolna). packedBytes - bajty deflate.</summary>
        private static string Unpack(List<string> z, out long packedBytes)
        {
            packedBytes = 0;
            if (z == null || z.Count < 1 || z[0] == null) return null;
            var h = z[0].Split('|');
            if (h.Length < 4 || h[0] != ZHead) return null;
            int chars; uint fnv; long bytes;
            if (!int.TryParse(h[1], NumberStyles.Integer, Inv, out chars) || chars < 0) return null;
            if (!uint.TryParse(h[2], NumberStyles.HexNumber, Inv, out fnv)) return null;
            if (!long.TryParse(h[3], NumberStyles.Integer, Inv, out bytes) || bytes < 0) return null;
            var sb = new StringBuilder();
            for (int i = 1; i < z.Count; i++) sb.Append(z[i]);
            byte[] packed = Convert.FromBase64String(sb.ToString());
            if (packed.LongLength != bytes) return null;
            packedBytes = bytes;
            string s;
            using (var ms = new MemoryStream(packed))
            using (var zs = new DeflateStream(ms, CompressionMode.Decompress))
            using (var rd = new StreamReader(zs, Utf8)) s = rd.ReadToEnd();
            if (s.Length != chars || Fnv(s) != fnv) return null;
            return s;
        }

        /// <summary>FNV-1a 32 na znakach UTF-16 (suma kontrolna calego napisu, nie kryptografia).</summary>
        private static uint Fnv(string s)
        {
            uint h = 2166136261;
            for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619; }
            return h;
        }
    }
}
