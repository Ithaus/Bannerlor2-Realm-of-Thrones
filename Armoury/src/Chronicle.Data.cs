using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m105-kronika K0 (krok A1) - KRONIKA WESTEROS: warstwa danych. Kontrakt: docs/KRONIKA-FORMAT.md (wygrywa przy sprzecznosci), plan: PLANY-E5-WOJNY-KRONIKA
    /// sekcja "## PLAN kronika" (E: Chronicle.Data.cs).
    /// Pliki Armoury/ModuleData/arm_kronika_kNN.tsv (paczki K1-K10, wzorzec kNN - kolejne paczki wchodza same) oraz arm_kronika_tematy.tsv (tematy obrazkow).
    /// k00 (2 okna testowe silnika) TYLKO przy autotescie: LogGate.Mode 1/2 i plik Documents/Mount and Blade II Bannerlord/CrashScribe/armoury-kronika-test.flag
    /// (wzor SideVillageFief.TestWanted).
    /// Wczytanie: Chronicle.Data.EnsureLoaded() przy OnSessionLaunched (wola ChronicleBehavior, krok A2) - pierwszy raz albo po zmianie pliku (rozmiar, data,
    /// zestaw plikow, flaga testu); w tej samej sesji drugie wolanie nic nie czyta.
    /// Zly plik (pusty, bajt 0 = wyzerowany na dysku C, nie UTF-8, brak naglowkow, nieznana sekcja, inne kolumny naglowka, dane przed naglowkiem) wylacza TYLKO
    /// swoja paczke - linia "ZLY PLIK ... - paczka wylaczona". Zly wiersz - linia "ZLY WIERSZ plik:wiersz (powod)" (pierwsze 10 na wczytanie, reszta w liczniku
    /// w podsumowaniu), reszta paczki dziala. Nieznany term w kolumnie warunkow NIE odrzuca wiersza (term liczony jako falsz - okno przepadnie po spoznieniu,
    /// zamiast zniknac bez stanu) - osobny licznik i linia "NIEZNANY TERM".
    /// Teksty: komorki z prefiksem {=ARM_KR_...}; TextObject tworzony swiezy przy kazdym odczycie (Tx) - zmienne {NAZWA} ustawia wolajacy na swojej kopii.
    /// Tylko odczyt plikow; nic w zapisie. Wyjatek per plik - potkniecie (licznik), nic nie gasnie.
    /// </summary>
    internal static partial class Chronicle
    {
        internal const string Tag = "Kronika Westeros (KW): ";
        internal const string TestFlagName = "armoury-kronika-test.flag";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // --- klucze MCM (czytane na zywo; KRONIKA-FORMAT 3.3, plan F) ---
        internal static bool KeyOn { get { var s = Settings.Current; return s == null || s.ChronicleOfWesteros; } }
        internal static bool KeyWorld { get { var s = Settings.Current; return s == null || s.ChronicleWorldChanges; } }
        internal static bool KeyFull { get { var s = Settings.Current; return s != null && s.ChronicleFullVersions; } }
        internal static bool KeyChoices { get { var s = Settings.Current; return s == null || s.ChroniclePlayerChoices; } }

        // --- potkniecia (wspolne dla calej Kroniki): Log.Error tylko przy 1-5, potem licznik w liniach (plan G) ---
        private static int _st;
        internal static int StumblesDay;
        internal static int Stumbles { get { return _st; } }

        internal static void Stumble(string where, Exception e)
        {
            _st++; StumblesDay++;
            if (_st <= 5) Log.Error("Chronicle." + where + " (potkniecie " + _st + ")", e);
        }

        /// <summary>Autotest Kroniki: tryb CrashScribe 1/2 i plik flagi w katalogu CrashScribe (wzor SideVillageFief.TestWanted).</summary>
        internal static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string p = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), TestFlagName);
                return System.IO.File.Exists(p);
            }
            catch { return false; }
        }

        internal static class Data
        {
            internal const string TopicsFile = "arm_kronika_tematy.tsv";
            private const int BadLinesShown = 10;

            // naglowki sekcji - DOKLADNIE jak w KRONIKA-FORMAT.md rozdz. 2 (inne kolumny = zly plik)
            private static readonly Dictionary<string, string[]> Cols = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "#okno", new[] { "id", "war", "od", "do", "po", "przed", "warunki", "gracz", "forma", "inni", "wersja", "temat", "lektor", "tytul", "gora", "dol",
                                   "zmienne", "wybor", "skutki", "powtorz" } },
                { "#wybor", new[] { "okno", "opcja", "etykieta", "podpowiedz", "dostepna", "skutki", "bierna" } },
                { "#tekst", new[] { "id", "tekst" } },
                { "#banda", new[] { "id", "klan", "osada", "min", "max", "herszt", "zachowanie", "dni", "list", "po_rozbiciu" } },
                { "#zadanie", new[] { "id", "rodzaj", "cel", "co", "ilosc", "dni", "nagroda", "tytul", "opis" } },
                { "#para", new[] { "okno", "wiersz", "kierunek" } },
                { "#usuniete", new[] { "id", "powod" } },
            };
            private static readonly string[] TopicCols = { "temat", "sprite", "prolog", "tylko", "opis" };

            // ---------------------------------------------------------------- model

            /// <summary>Paczka = jeden plik arm_kronika_kNN.tsv. Ok = false: zly plik, nic z niego nie weszlo (Why = powod).</summary>
            internal sealed class PackInfo
            {
                public int Num;
                public string File = "", Why = "";
                public bool Ok;
                public int Windows, Rows, Choices, Texts, Bands, Tasks, Pairs, Tombs, Bad;
                public string Name { get { return "k" + Num.ToString("00", Inv); } }
            }

            /// <summary>Zmienna okna: {Name} w tekstach = wartosc tokenu (np. RULER=@wladca:battania) - podstawia krok A2/A4.</summary>
            internal sealed class VarDef
            {
                public string Name = "", Token = "";
            }

            /// <summary>Kolumna powtorz (rozdz. 13): wyzwalacz[:arg] ; prolog ; odstep:N ; max:N.</summary>
            internal sealed class RecurDef
            {
                public string Raw = "", Trigger = "", Arg = "";
                public bool Prolog;
                public int Gap, Max;   // 0 = brak
            }

            /// <summary>Opcja listy po oknie (#wybor). Key = kolumna okno (NNN.w albo NNN.w + litera wariantu).</summary>
            internal sealed class OptionDef
            {
                public string Key = "", Letter = "", Label = "", Hint = "", Avail = "", File = "";
                public string[] Effects = NoStrings;
                public bool Passive;
                public int Line;
                public TextObject LabelText { get { return Tx(Label); } }
                public TextObject HintText { get { return Tx(Hint); } }
            }

            /// <summary>Wiersz #okno: glowny (War = "") albo wariant (War = a-z). Teksty i temat wariantu bez wlasnych - z wiersza glownego (OwnText mowi, czy wlasne).
            /// Od/Do/Po/Przed tylko w wierszu glownym (wariant: puste; czytaj z Window).</summary>
            internal sealed class RowDef
            {
                public WindowDef Window;
                public string War = "", Key = "", File = "";
                public int Line;
                public int Od, Do = -1;                 // dni opowiesci w skali dokumentu (przeliczenie: Terms.DocToStory); Do = -1 - bez konca
                public string Po = "", Przed = "", Cond = "", Player = "";
                public string Form = "", Others = "", Version = "";   // okno|wiesc|log ; okno|wiesc|nic ; lekka|pelna|obie
                public string Topic = "";
                public bool Narrated;                    // lektor = 1
                public bool OwnText;                     // wariant z wlasnym tytulem/gora/dolem (glowny - zawsze true)
                public string Title = "", Upper = "", Lower = "", Question = "";   // surowe komorki {=ARM_KR_...}Tekst (po dziedziczeniu z glownego)
                public readonly List<VarDef> Vars = new List<VarDef>();
                public string[] Effects = NoStrings;
                public readonly List<OptionDef> Options = new List<OptionDef>();   // opcje tego wiersza (wlasne NNN.w<war> albo okna NNN.w); puste = bez listy
                public bool IsMain { get { return War.Length == 0; } }
                public bool Shows { get { return Form == "okno" || Others == "okno"; } }
                public bool NeedsNews { get { return Form == "wiesc" || Others == "wiesc"; } }
                public TextObject TitleText { get { return Tx(Title); } }
                public TextObject UpperText { get { return Tx(Upper); } }
                public TextObject LowerText { get { return Tx(Lower); } }
                public TextObject QuestionText { get { return Tx(Question); } }
                /// <summary>Klucz nagrania lektora: kr_NNN_w[war] (wariant bez wlasnego tekstu - nagranie glownego).</summary>
                public string NarrationKey { get { return "kr_" + Window.Id.Replace('.', '_') + (OwnText ? War : ""); } }
            }

            /// <summary>Okno NNN.w: wiersz glowny + warianty w kolejnosci pliku.</summary>
            internal sealed class WindowDef
            {
                public string Id = "";
                public int Event, Slot;                  // NNN, w
                public PackInfo Pack;
                public RowDef Main;
                public readonly List<RowDef> Variants = new List<RowDef>();
                public RecurDef Recur;                   // null - okno jednorazowe
                public bool HasFull;                     // jakis wiersz wersja=pelna (filtr ChronicleFullVersions, rozdz. 3.1)
                public bool HasChoice;                   // jakis wiersz z lista opcji
                public int Od { get { return Main.Od; } }
                public int Do { get { return Main.Do; } }
                public string Po { get { return Main.Po; } }
                public string Przed { get { return Main.Przed; } }
            }

            internal sealed class BandDef
            {
                public string Id = "", Clan = "", Settlement = "", Leader = "", Behavior = "", File = "";
                public int Min, Max, Days, Line;
                public bool Notice;
                public string[] AfterRout = NoStrings;
                public TextObject LeaderText { get { return Tx(Leader); } }
            }

            internal sealed class TaskDef
            {
                public string Id = "", Kind = "", Target = "", What = "", Title = "", Desc = "", File = "";
                public int Amount, Days, Line;
                public string[] Reward = NoStrings;
                public TextObject TitleText { get { return Tx(Title); } }
                public TextObject DescText { get { return Tx(Desc); } }
            }

            internal sealed class PairDef
            {
                public string Window = "", Row = "", Dir = "po";
            }

            internal sealed class TopicDef
            {
                public string Name = "", Note = "";
                public string[] Sprites = NoStrings, Prolog = NoStrings;
                public HashSet<int> Only;                // null - wszystkie okna
            }

            // ---------------------------------------------------------------- stan danych (raz na proces, odswiezany po zmianie pliku)
            internal static readonly string[] NoStrings = new string[0];
            private static bool _loaded;
            private static string _stamp = "";
            internal static int Generation;              // +1 przy kazdym wczytaniu (wolajacy moze uniewaznic swoje pamieci)
            private static readonly List<PackInfo> _packs = new List<PackInfo>();
            private static readonly List<WindowDef> _windows = new List<WindowDef>();
            private static readonly Dictionary<string, WindowDef> _byId = new Dictionary<string, WindowDef>(StringComparer.Ordinal);
            private static readonly Dictionary<string, RowDef> _byKey = new Dictionary<string, RowDef>(StringComparer.Ordinal);
            private static readonly Dictionary<string, string> _texts = new Dictionary<string, string>(StringComparer.Ordinal);
            private static readonly Dictionary<string, BandDef> _bands = new Dictionary<string, BandDef>(StringComparer.Ordinal);
            private static readonly Dictionary<string, TaskDef> _tasks = new Dictionary<string, TaskDef>(StringComparer.Ordinal);
            private static readonly List<PairDef> _pairs = new List<PairDef>();
            private static readonly Dictionary<string, string> _tombs = new Dictionary<string, string>(StringComparer.Ordinal);
            private static readonly List<TopicDef> _topics = new List<TopicDef>();
            private static readonly Dictionary<string, TopicDef> _topicBy = new Dictionary<string, TopicDef>(StringComparer.Ordinal);
            private static string _topicsWhy = "";
            private static int _bad, _badShown, _unknownTerms;
            private static string _firstBad = "", _firstUnknown = "";
            private static bool _test;

            // ---------------------------------------------------------------- API
            internal static bool Loaded { get { return _loaded; } }
            internal static bool TestPack { get { return _test; } }
            internal static IList<PackInfo> Packs { get { return _packs; } }
            /// <summary>Okna wszystkich dobrych paczek w kolejnosci paczek i pliku.</summary>
            internal static IList<WindowDef> Windows { get { return _windows; } }
            internal static IList<PairDef> Pairs { get { return _pairs; } }
            internal static IList<TopicDef> Topics { get { return _topics; } }
            internal static int BadRows { get { return _bad; } }
            internal static string FirstBad { get { return _firstBad; } }
            internal static int UnknownTerms { get { return _unknownTerms; } }

            internal static WindowDef Window(string id) { WindowDef w; return id != null && _byId.TryGetValue(id, out w) ? w : null; }
            /// <summary>Wiersz po kluczu NNN.w (glowny) albo NNN.w + litera (wariant).</summary>
            internal static RowDef Row(string key) { RowDef r; return key != null && _byKey.TryGetValue(key, out r) ? r : null; }
            /// <summary>Surowy tekst z #tekst ({=ARM_KR_...}Tekst) albo null.</summary>
            internal static string TextRaw(string id) { string t; return id != null && _texts.TryGetValue(id, out t) ? t : null; }
            internal static TextObject Text(string id) { return Tx(TextRaw(id)); }
            /// <summary>Tekst formy wiesc wiersza: NNN.w[war]_W, wariant bez wlasnego - NNN.w_W (rozdz. 5); null - brak.</summary>
            internal static string NewsRaw(RowDef r)
            {
                if (r == null) return null;
                return TextRaw(r.Key + "_W") ?? TextRaw(r.Window.Id + "_W");
            }
            internal static BandDef Band(string id) { BandDef b; return id != null && _bands.TryGetValue(id, out b) ? b : null; }
            internal static TaskDef Task(string id) { TaskDef t; return id != null && _tasks.TryGetValue(id, out t) ? t : null; }
            internal static TopicDef Topic(string name) { TopicDef t; return name != null && _topicBy.TryGetValue(name, out t) ? t : null; }
            /// <summary>Id okna wycofanego (#usuniete) - term kr: tego okna nigdy nie zajdzie.</summary>
            internal static bool IsTomb(string id) { return id != null && _tombs.ContainsKey(id); }

            /// <summary>Nowy TextObject z surowej komorki ({=ID}Tekst) albo null dla pustej.</summary>
            internal static TextObject Tx(string raw) { return string.IsNullOrEmpty(raw) ? null : new TextObject(raw); }

            /// <summary>Tekst bez prefiksu {=ID} (do logu i md5 lektora).</summary>
            internal static string Plain(string raw)
            {
                if (string.IsNullOrEmpty(raw)) return "";
                if (raw.StartsWith("{=", StringComparison.Ordinal)) { int i = raw.IndexOf('}'); if (i > 0) return raw.Substring(i + 1); }
                return raw;
            }

            /// <summary>Do linii startu sesji (plan G): paczki kNN okien/wyborow/band, zle pliki, zle wiersze (pierwszy z powodem), nieznane termy, tematy.</summary>
            internal static string Summary()
            {
                var sb = new StringBuilder(300);
                sb.Append("paczki ").Append(_packs.Count).Append(':');
                if (_packs.Count == 0) sb.Append(" brak");
                foreach (var p in _packs)
                {
                    sb.Append(' ').Append(p.Name);
                    if (!p.Ok) { sb.Append(" ZLY PLIK (").Append(p.Why).Append(')'); continue; }
                    sb.Append(' ').Append(p.Windows).Append('/').Append(p.Choices).Append('/').Append(p.Bands);
                    if (p.Bad > 0) sb.Append(" (zlych ").Append(p.Bad).Append(')');
                }
                sb.Append(" [okien/wyborow/band]");
                if (_test) sb.Append(" | k00 testowa TAK");
                sb.Append(" | okien ").Append(_windows.Count).Append(", tekstow ").Append(_texts.Count).Append(", zadan ").Append(_tasks.Count)
                  .Append(", par ").Append(_pairs.Count).Append(", nagrobkow ").Append(_tombs.Count);
                sb.Append(" | zle wiersze ").Append(_bad);
                if (_bad > 0) sb.Append(" (pierwszy: ").Append(_firstBad).Append(')');
                sb.Append(" | nieznane termy ").Append(_unknownTerms);
                if (_unknownTerms > 0) sb.Append(" (pierwszy: ").Append(_firstUnknown).Append(')');
                int spr = 0;
                foreach (var t in _topics) spr += t.Sprites.Length;
                sb.Append(" | tematy ").Append(_topics.Count).Append(", sprite'ow ").Append(spr);
                if (_topicsWhy.Length > 0) sb.Append(" (").Append(_topicsWhy).Append(')');
                return sb.ToString();
            }

            /// <summary>Wczytanie przy OnSessionLaunched: pierwszy raz albo po zmianie plikow (rozmiar/data/zestaw/flaga testu). Jedna linia "dane ..." na wczytanie.</summary>
            internal static void EnsureLoaded()
            {
                try
                {
                    bool test = TestWanted();
                    string dir = Dir();
                    var files = ListFiles(dir, test);
                    string stamp = Stamp(dir, files, test);
                    if (_loaded && stamp == _stamp) return;
                    bool first = !_loaded;
                    Load(dir, files, test);
                    _stamp = stamp; _loaded = true; Generation++;
                    Log.Info(Tag + "dane " + (first ? "wczytane" : "wczytane ponownie (zmiana pliku)") + " | " + Summary() + ".");
                }
                catch (Exception e) { Stumble("Data.EnsureLoaded", e); }
            }

            // ---------------------------------------------------------------- pliki
            private static string Dir()
            {
                string p = MapVillageTooltip.ModulePath("ModuleData", TopicsFile);
                return p != null ? System.IO.Path.GetDirectoryName(p) : null;
            }

            /// <summary>Paczki kNN (k00 tylko przy tescie) w kolejnosci numerow.</summary>
            private static List<KeyValuePair<int, string>> ListFiles(string dir, bool test)
            {
                var l = new List<KeyValuePair<int, string>>();
                if (dir == null || !System.IO.Directory.Exists(dir)) return l;
                foreach (var f in System.IO.Directory.GetFiles(dir, "arm_kronika_k*.tsv"))
                {
                    string n = System.IO.Path.GetFileName(f);
                    // arm_kronika_kNN.tsv - dokladnie dwie cyfry
                    if (n.Length != 19 || !char.IsDigit(n[13]) || !char.IsDigit(n[14])) continue;
                    int num = (n[13] - '0') * 10 + (n[14] - '0');
                    if (num == 0 && !test) continue;
                    l.Add(new KeyValuePair<int, string>(num, f));
                }
                l.Sort((a, b) => a.Key.CompareTo(b.Key));
                return l;
            }

            private static string Stamp(string dir, List<KeyValuePair<int, string>> files, bool test)
            {
                var sb = new StringBuilder(400);
                sb.Append(test ? "T|" : "N|");
                foreach (var kv in files) AddStamp(sb, kv.Value);
                if (dir != null) AddStamp(sb, System.IO.Path.Combine(dir, TopicsFile));
                return sb.ToString();
            }

            private static void AddStamp(StringBuilder sb, string path)
            {
                try
                {
                    var fi = new System.IO.FileInfo(path);
                    sb.Append(fi.Name).Append(':');
                    if (fi.Exists) sb.Append(fi.Length.ToString(Inv)).Append(':').Append(fi.LastWriteTimeUtc.Ticks.ToString(Inv));
                    else sb.Append('-');
                    sb.Append('|');
                }
                catch { sb.Append(path).Append(":?|"); }
            }

            private static void Clear()
            {
                _packs.Clear(); _windows.Clear(); _byId.Clear(); _byKey.Clear(); _texts.Clear(); _bands.Clear(); _tasks.Clear(); _pairs.Clear(); _tombs.Clear();
                _topics.Clear(); _topicBy.Clear(); _topicsWhy = "";
                _bad = _badShown = _unknownTerms = 0; _firstBad = _firstUnknown = "";
                _test = false;
            }

            /// <summary>Komorki jednego wiersza danych sekcji (po obcieciu spacji, uzupelnione do liczby kolumn).</summary>
            private sealed class TsvLine
            {
                public string Sec = "";
                public int No;
                public string[] C;
            }

            /// <summary>Czyta plik do linii sekcji. Zwraca null i powod przy zlym pliku. Zle wiersze (za duzo kolumn) - do listy bad.</summary>
            private static List<TsvLine> ReadTsv(string path, Dictionary<string, string[]> cols, out string why, List<KeyValuePair<int, string>> bad)
            {
                why = null;
                byte[] raw;
                try { raw = System.IO.File.ReadAllBytes(path); }
                catch (Exception e) { why = "nie moge przeczytac (" + e.GetType().Name + ")"; return null; }
                if (raw.Length == 0) { why = "pusty plik (wyzerowany?)"; return null; }
                for (int i = 0; i < raw.Length; i++) if (raw[i] == 0) { why = "bajt 0 na pozycji " + i.ToString(Inv) + " (plik wyzerowany na dysku)"; return null; }
                int start = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF ? 3 : 0;
                string text;
                try { text = new UTF8Encoding(false, true).GetString(raw, start, raw.Length - start); }
                catch (Exception) { why = "to nie jest UTF-8"; return null; }
                var outp = new List<TsvLine>();
                string sec = null;
                string[] want = null;
                int headers = 0, no = 0;
                foreach (string rl in text.Split('\n'))
                {
                    no++;
                    string line = rl.TrimEnd('\r');
                    if (line.Trim().Length == 0) continue;
                    if (line.StartsWith("# ", StringComparison.Ordinal) || line == "#") continue;
                    var c = line.Split('\t');
                    if (c[0].StartsWith("#", StringComparison.Ordinal))
                    {
                        string name = c[0].Trim();
                        if (!cols.TryGetValue(name, out want)) { why = "nieznana sekcja " + name + " (wiersz " + no.ToString(Inv) + ")"; return null; }
                        if (c.Length - 1 != want.Length) { why = "naglowek " + name + " ma " + (c.Length - 1).ToString(Inv) + " kolumn, a ma miec " + want.Length.ToString(Inv) + " (wiersz " + no.ToString(Inv) + ")"; return null; }
                        for (int i = 0; i < want.Length; i++)
                            if (c[i + 1].Trim() != want[i]) { why = "naglowek " + name + ": kolumna " + (i + 1).ToString(Inv) + " '" + c[i + 1].Trim() + "', a ma byc '" + want[i] + "' (wiersz " + no.ToString(Inv) + ")"; return null; }
                        sec = name;
                        headers++;
                        continue;
                    }
                    if (sec == null) { why = "wiersz danych przed naglowkiem sekcji (wiersz " + no.ToString(Inv) + ")"; return null; }
                    if (c.Length > want.Length) { bad.Add(new KeyValuePair<int, string>(no, "za duzo kolumn (" + c.Length.ToString(Inv) + ", sekcja " + sec + " ma " + want.Length.ToString(Inv) + ")")); continue; }
                    var cells = new string[want.Length];
                    for (int i = 0; i < want.Length; i++) cells[i] = i < c.Length ? c[i].Trim() : "";
                    outp.Add(new TsvLine { Sec = sec, No = no, C = cells });
                }
                if (headers == 0) { why = "brak naglowkow sekcji (plik pusty albo uszkodzony)"; return null; }
                return outp;
            }

            // ---------------------------------------------------------------- wczytanie
            /// <summary>Wiersze jednej paczki przed polaczeniem (zly plik = paczka bez wierszy).</summary>
            private sealed class Raw
            {
                public PackInfo Pack;
                public readonly List<TsvLine> Lines = new List<TsvLine>();
            }

            private static void Load(string dir, List<KeyValuePair<int, string>> files, bool test)
            {
                Clear();
                var raws = new List<Raw>();
                foreach (var kv in files)
                {
                    var p = new PackInfo { Num = kv.Key, File = System.IO.Path.GetFileName(kv.Value) };
                    _packs.Add(p);
                    if (kv.Key == 0) _test = true;
                    try
                    {
                        string why;
                        var bad = new List<KeyValuePair<int, string>>();
                        var lines = ReadTsv(kv.Value, Cols, out why, bad);
                        if (lines == null)
                        {
                            p.Ok = false; p.Why = why;
                            Log.Info(Tag + "ZLY PLIK " + p.File + " (" + why + ") - paczka wylaczona.");
                            continue;
                        }
                        p.Ok = true;
                        foreach (var b in bad) Bad(p, b.Key, b.Value);
                        var r = new Raw { Pack = p };
                        r.Lines.AddRange(lines);
                        raws.Add(r);
                    }
                    catch (Exception e)
                    {
                        p.Ok = false; p.Why = "blad wczytania (" + e.GetType().Name + ")";
                        Log.Info(Tag + "ZLY PLIK " + p.File + " (" + p.Why + ") - paczka wylaczona.");
                        Stumble("Data.Load(" + p.File + ")", e);
                    }
                }
                try { Link(raws); }
                catch (Exception e)
                {
                    // blad polaczenia (nie powinien sie zdarzyc) - bez danych, zeby nie dzialac na polowie
                    Stumble("Data.Link", e);
                    _windows.Clear(); _byId.Clear(); _byKey.Clear(); _texts.Clear(); _bands.Clear(); _tasks.Clear(); _pairs.Clear(); _tombs.Clear();
                    foreach (var p in _packs) if (p.Ok) { p.Ok = false; p.Why = "blad polaczenia danych (" + e.GetType().Name + ")"; }
                }
                try { LoadTopics(dir); }
                catch (Exception e) { Stumble("Data.LoadTopics", e); _topics.Clear(); _topicBy.Clear(); _topicsWhy = "blad wczytania (" + e.GetType().Name + ")"; }
            }

            private static void Bad(PackInfo p, int line, string why)
            {
                _bad++;
                if (p != null) p.Bad++;
                string where = (p != null ? p.File : "?") + ":" + line.ToString(Inv);
                if (_firstBad.Length == 0) _firstBad = where + " " + why;
                if (_badShown < BadLinesShown)
                {
                    _badShown++;
                    Log.Info(Tag + "ZLY WIERSZ " + where + " (" + why + ")" + (_badShown == BadLinesShown ? " - kolejne tylko w liczniku linii startu" : "") + ".");
                }
            }

            private static void Bad(string file, int line, string why)
            {
                PackInfo pk = null;
                foreach (var p in _packs) if (p.File == file) { pk = p; break; }
                if (pk != null) { Bad(pk, line, why); return; }
                _bad++;
                string where = file + ":" + line.ToString(Inv);
                if (_firstBad.Length == 0) _firstBad = where + " " + why;
                if (_badShown < BadLinesShown) { _badShown++; Log.Info(Tag + "ZLY WIERSZ " + where + " (" + why + ")."); }
            }

            private static bool IsWinId(string s)
            {
                // NNN.w - trzy cyfry, kropka, cyfra 1-9
                return s != null && s.Length == 5 && char.IsDigit(s[0]) && char.IsDigit(s[1]) && char.IsDigit(s[2]) && s[3] == '.' && s[4] >= '1' && s[4] <= '9';
            }

            private static bool IsLetter(string s) { return s != null && s.Length == 1 && s[0] >= 'a' && s[0] <= 'z'; }

            private static bool TextOk(string cell) { return cell.Length == 0 || (cell.StartsWith("{=ARM_KR_", StringComparison.Ordinal) && cell.IndexOf('}') > 9); }

            private static bool Int(string s, out int v) { return int.TryParse(s, NumberStyles.AllowLeadingSign, Inv, out v); }

            private static string[] SplitEffects(string cell)
            {
                if (cell.Length == 0) return NoStrings;
                var l = new List<string>();
                foreach (var a in cell.Split(';')) { var t = a.Trim(); if (t.Length > 0) l.Add(t); }
                return l.ToArray();
            }

            /// <summary>Polaczenie paczek: okna (glowny przed wariantami, unikalne id, nagrobki), opcje do wierszy, teksty, bandy, zadania, pary; kontrola wiesci.</summary>
            private static void Link(List<Raw> raws)
            {
                // 1. nagrobki ze wszystkich dobrych paczek
                foreach (var r in raws)
                    foreach (var l in r.Lines)
                    {
                        if (l.Sec != "#usuniete") continue;
                        if (!IsWinId(l.C[0])) { Bad(r.Pack, l.No, "#usuniete: id '" + l.C[0] + "' ma byc NNN.w"); continue; }
                        if (_tombs.ContainsKey(l.C[0])) { Bad(r.Pack, l.No, "#usuniete: powtorzony nagrobek " + l.C[0]); continue; }
                        _tombs[l.C[0]] = l.C[1];
                        r.Pack.Tombs++;
                    }
                // 2. okna
                foreach (var r in raws)
                    foreach (var l in r.Lines)
                        if (l.Sec == "#okno") WindowRow(r.Pack, l);
                // 3. teksty
                foreach (var r in raws)
                    foreach (var l in r.Lines)
                    {
                        if (l.Sec != "#tekst") continue;
                        string id = l.C[0], why = TextId(id);
                        if (why == null && !TextOk(l.C[1])) why = "tekst bez prefiksu {=ARM_KR_...}";
                        if (why == null && l.C[1].Length == 0) why = "pusty tekst";
                        if (why == null && _texts.ContainsKey(id)) why = "powtorzone id tekstu " + id;
                        if (why != null) { Bad(r.Pack, l.No, "#tekst: " + why); continue; }
                        _texts[id] = l.C[1];
                        r.Pack.Texts++;
                    }
                // 4. kontrola wierszy po dziedziczeniu (warianty) i wiesci (#tekst _W)
                CheckRows();
                // 5. opcje do wierszy
                LinkOptions(raws);
                // 6. bandy, zadania, pary
                foreach (var r in raws)
                    foreach (var l in r.Lines)
                    {
                        if (l.Sec == "#banda") BandRow(r.Pack, l);
                        else if (l.Sec == "#zadanie") TaskRow(r.Pack, l);
                        else if (l.Sec == "#para") PairRow(r.Pack, l);
                    }
                // 7. liczby paczek, flagi okien, nieznane termy
                foreach (var w in _windows)
                {
                    w.Pack.Windows++;
                    w.Pack.Rows += 1 + w.Variants.Count;
                    w.HasFull = w.Main.Version == "pelna";
                    w.HasChoice = w.Main.Options.Count > 0;
                    foreach (var v in w.Variants) { if (v.Version == "pelna") w.HasFull = true; if (v.Options.Count > 0) w.HasChoice = true; }
                    if (w.HasChoice) w.Pack.Choices++;
                    ScanTerms(w.Main);
                    foreach (var v in w.Variants) ScanTerms(v);
                }
            }

            private static string TextId(string id)
            {
                // NNN.w[war]_X, X = wielkie litery/cyfry, nie T/U/L/Q/Cx/Hx (rozdz. 5); okno (wiersz) musi istniec
                int u = id.IndexOf('_');
                if (u < 5) return "id '" + id + "' ma byc NNN.w[war]_X";
                string key = id.Substring(0, u), x = id.Substring(u + 1);
                if (!IsWinId(key.Length > 5 ? key.Substring(0, 5) : key) || key.Length > 6 || (key.Length == 6 && !IsLetter(key.Substring(5)))) return "id '" + id + "' ma byc NNN.w[war]_X";
                if (x.Length == 0 || !(x[0] >= 'A' && x[0] <= 'Z')) return "id '" + id + "': X ma byc wielka litera i cyfry";
                foreach (char ch in x) if (!((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))) return "id '" + id + "': X ma byc wielka litera i cyfry";
                if (x == "T" || x == "U" || x == "L" || x == "Q") return "id '" + id + "': X zarezerwowane dla okna";
                if (!_byKey.ContainsKey(key)) return "id '" + id + "': brak okna/wiersza " + key;
                return null;
            }

            private static void WindowRow(PackInfo p, TsvLine l)
            {
                var c = l.C;
                // id war od do po przed warunki gracz forma inni wersja temat lektor tytul gora dol zmienne wybor skutki powtorz
                string id = c[0], war = c[1];
                string why = null;
                if (!IsWinId(id)) why = "id '" + id + "' ma byc NNN.w (np. 008.1)";
                else if (war.Length > 0 && !IsLetter(war)) why = "war '" + war + "' ma byc jedna mala litera a-z";
                else if (_tombs.ContainsKey(id)) why = "id " + id + " jest w #usuniete (nagrobek nie wraca jako okno)";
                if (why != null) { Bad(p, l.No, "#okno: " + why); return; }
                var r = new RowDef { War = war, Key = id + war, File = p.File, Line = l.No };
                WindowDef w;
                _byId.TryGetValue(id, out w);
                if (war.Length == 0)
                {
                    if (w != null) { Bad(p, l.No, "#okno: powtorzony wiersz glowny " + id + (w.Pack != p ? " (juz w " + w.Pack.File + ")" : "")); return; }
                    if (!Int(c[2], out r.Od) || r.Od < 0) { Bad(p, l.No, "#okno " + id + ": od '" + c[2] + "' ma byc liczba >= 0"); return; }
                    if (c[3].Length > 0 && !Int(c[3], out r.Do)) { Bad(p, l.No, "#okno " + id + ": do '" + c[3] + "' ma byc liczba albo puste"); return; }
                    r.Po = c[4]; r.Przed = c[5];
                    if (r.Przed.Length > 0)
                    {
                        string k = r.Przed.StartsWith("kr:", StringComparison.Ordinal) ? "kr" : r.Przed.StartsWith("ev:", StringComparison.Ordinal) ? "ev" : r.Przed.StartsWith("done:", StringComparison.Ordinal) ? "done" : null;
                        if (k == null || r.Przed.IndexOf('&') >= 0 || r.Przed.IndexOf('|') >= 0 || r.Przed.IndexOf('+') >= 0 || r.Przed.IndexOf('!') >= 0 || r.Przed.Split(':').Length != 2)
                        { Bad(p, l.No, "#okno " + id + ": przed '" + r.Przed + "' ma byc JEDNYM termem ev:X, done:X albo kr:NNN.w"); return; }
                    }
                }
                else
                {
                    if (w == null || w.Pack != p) { Bad(p, l.No, "#okno " + id + war + ": wariant bez wiersza glownego wyzej w tym pliku"); return; }
                    foreach (var v in w.Variants) if (v.War == war) { Bad(p, l.No, "#okno " + id + war + ": powtorzona litera wariantu"); return; }
                    if (c[2].Length > 0 || c[3].Length > 0 || c[4].Length > 0 || c[5].Length > 0) { Bad(p, l.No, "#okno " + id + war + ": od/do/po/przed tylko w wierszu glownym"); return; }
                    r.Od = w.Main.Od; r.Do = w.Main.Do;
                }
                r.Cond = c[6]; r.Player = c[7]; r.Form = c[8]; r.Others = c[9]; r.Version = c[10]; r.Topic = c[11];
                if (r.Form != "okno" && r.Form != "wiesc" && r.Form != "log") { Bad(p, l.No, "#okno " + r.Key + ": forma '" + r.Form + "' (okno|wiesc|log)"); return; }
                if (r.Others != "okno" && r.Others != "wiesc" && r.Others != "nic") { Bad(p, l.No, "#okno " + r.Key + ": inni '" + r.Others + "' (okno|wiesc|nic)"); return; }
                if (r.Version != "lekka" && r.Version != "pelna" && r.Version != "obie") { Bad(p, l.No, "#okno " + r.Key + ": wersja '" + r.Version + "' (lekka|pelna|obie)"); return; }
                if (c[12] != "" && c[12] != "0" && c[12] != "1") { Bad(p, l.No, "#okno " + r.Key + ": lektor '" + c[12] + "' (0|1)"); return; }
                r.Narrated = c[12] == "1";
                for (int i = 13; i <= 15; i++) if (!TextOk(c[i])) { Bad(p, l.No, "#okno " + r.Key + ": tekst bez prefiksu {=ARM_KR_...} w kolumnie " + Cols["#okno"][i]); return; }
                if (!TextOk(c[17])) { Bad(p, l.No, "#okno " + r.Key + ": pytanie (wybor) bez prefiksu {=ARM_KR_...}"); return; }
                r.Title = c[13]; r.Upper = c[14]; r.Lower = c[15]; r.Question = c[17];
                r.OwnText = war.Length == 0 || r.Title.Length > 0 || r.Upper.Length > 0 || r.Lower.Length > 0;
                string vwhy = ParseVars(c[16], r.Vars);
                if (vwhy != null) { Bad(p, l.No, "#okno " + r.Key + ": zmienne - " + vwhy); return; }
                r.Effects = SplitEffects(c[18]);
                if (war.Length == 0)
                {
                    if (r.Shows && (r.Title.Length == 0 || r.Upper.Length == 0 || r.Lower.Length == 0)) { Bad(p, l.No, "#okno " + id + ": forma okno bez tytulu, gory albo dolu"); return; }
                    RecurDef rec = null;
                    if (c[19].Length > 0)
                    {
                        string rwhy;
                        rec = ParseRecur(c[19], out rwhy);
                        if (rec == null) { Bad(p, l.No, "#okno " + id + ": powtorz - " + rwhy); return; }
                    }
                    w = new WindowDef { Id = id, Event = (id[0] - '0') * 100 + (id[1] - '0') * 10 + (id[2] - '0'), Slot = id[4] - '0', Pack = p, Main = r, Recur = rec };
                    r.Window = w;
                    _byId[id] = w;
                    _windows.Add(w);
                }
                else
                {
                    if (c[19].Length > 0) { Bad(p, l.No, "#okno " + r.Key + ": powtorz tylko w wierszu glownym"); return; }
                    r.Window = w;
                    w.Variants.Add(r);
                }
                _byKey[r.Key] = r;
            }

            /// <summary>Wariant: dziedziczenie tekstow i tematu z glownego; wiersz z forma okno - teksty, z forma wiesc - tekst _W. Zly wiersz glowny = cale okno odpada.</summary>
            private static void CheckRows()
            {
                for (int i = _windows.Count - 1; i >= 0; i--)
                {
                    var w = _windows[i];
                    var m = w.Main;
                    for (int j = w.Variants.Count - 1; j >= 0; j--)
                    {
                        var v = w.Variants[j];
                        if (v.Title.Length == 0 && v.Upper.Length == 0 && v.Lower.Length == 0)
                        {
                            v.Title = m.Title; v.Upper = m.Upper; v.Lower = m.Lower;
                            // recenzja K0 (R-4): tekst glownego z {ZMIENNYMI} potrzebuje zmiennych glownego (045.1a/b/c: "villages around  more than once");
                            // zmienna o tej samej nazwie we wlasnej kolumnie wariantu ma pierwszenstwo
                            foreach (var mv in m.Vars)
                            {
                                bool own = false;
                                foreach (var x in v.Vars) if (x.Name == mv.Name) { own = true; break; }
                                if (!own) v.Vars.Add(mv);
                            }
                        }
                        if (v.Topic.Length == 0) v.Topic = m.Topic;
                        if (v.Player.Length == 0) v.Player = m.Player;   // recenzja K0 (R-3): pusty gracz wariantu = gracz glownego (FORMAT 3.1; 105.1a, 073.1a/b)
                        string why = null;
                        if (v.Shows && (v.Title.Length == 0 || v.Upper.Length == 0 || v.Lower.Length == 0)) why = "forma okno bez tytulu, gory albo dolu (wlasnych albo z wiersza glownego)";
                        else if (v.NeedsNews && NewsRaw(v) == null) why = "forma wiesc bez tekstu " + v.Key + "_W ani " + w.Id + "_W w #tekst";
                        if (why != null) { Bad(w.Pack, v.Line, "#okno " + v.Key + ": " + why); w.Variants.RemoveAt(j); _byKey.Remove(v.Key); }
                    }
                    if (m.NeedsNews && NewsRaw(m) == null)
                    {
                        Bad(w.Pack, m.Line, "#okno " + w.Id + ": forma wiesc bez tekstu " + w.Id + "_W w #tekst - okno odpada");
                        foreach (var v in w.Variants) { Bad(w.Pack, v.Line, "#okno " + v.Key + ": wiersz glowny odrzucony"); _byKey.Remove(v.Key); }
                        _byKey.Remove(m.Key); _byId.Remove(w.Id); _windows.RemoveAt(i);
                    }
                }
            }

            /// <summary>#wybor: grupa opcji klucza = a, b[, c[, d]] po kolei, 2-4, bierna (1) tylko na ostatniej. Wiersz z pytaniem bierze grupe swojego klucza
            /// (NNN.w + litera wariantu - pierwszenstwo), inaczej grupe okna NNN.w. Grupa bez wiersza z pytaniem i pytanie bez grupy - zle wiersze.</summary>
            private static void LinkOptions(List<Raw> raws)
            {
                var groups = new Dictionary<string, List<OptionDef>>(StringComparer.Ordinal);
                var order = new List<string>();
                var bad = new HashSet<string>(StringComparer.Ordinal);
                foreach (var r in raws)
                    foreach (var l in r.Lines)
                    {
                        if (l.Sec != "#wybor") continue;
                        var c = l.C;
                        var o = new OptionDef { Key = c[0], Letter = c[1], Label = c[2], Hint = c[3], Avail = c[4], Effects = SplitEffects(c[5]), Passive = c[6] == "1", File = r.Pack.File, Line = l.No };
                        string why = null;
                        string baseKey = o.Key.Length >= 5 ? o.Key.Substring(0, 5) : o.Key;
                        if (!IsWinId(baseKey) || o.Key.Length > 6 || (o.Key.Length == 6 && !IsLetter(o.Key.Substring(5)))) why = "okno '" + o.Key + "' ma byc NNN.w albo NNN.w + litera wariantu";
                        else if (o.Letter != "a" && o.Letter != "b" && o.Letter != "c" && o.Letter != "d") why = "opcja '" + o.Letter + "' (a-d)";
                        else if (c[6] != "" && c[6] != "1") why = "bierna '" + c[6] + "' (1 albo puste)";
                        else if (o.Label.Length == 0 || o.Hint.Length == 0 || !TextOk(o.Label) || !TextOk(o.Hint)) why = "etykieta i podpowiedz wymagane, z prefiksem {=ARM_KR_...}";
                        else if (!_byKey.ContainsKey(o.Key)) why = "brak okna/wiersza " + o.Key;
                        if (why != null) { Bad(r.Pack, l.No, "#wybor " + o.Key + o.Letter + ": " + why); continue; }
                        List<OptionDef> g;
                        if (!groups.TryGetValue(o.Key, out g)) { g = new List<OptionDef>(); groups[o.Key] = g; order.Add(o.Key); }
                        g.Add(o);
                    }
                // kontrola grup
                foreach (var k in order)
                {
                    var g = groups[k];
                    string why = null;
                    if (g.Count < 2 || g.Count > 4) why = "opcji " + g.Count.ToString(Inv) + " (ma byc 2-4)";
                    else
                        for (int i = 0; i < g.Count; i++)
                        {
                            if (g[i].Letter[0] != (char)('a' + i)) { why = "opcje nie ida po kolei a, b, c, d"; break; }
                            if (g[i].Passive != (i == g.Count - 1)) { why = "bierna (1) ma byc tylko na ostatniej opcji"; break; }
                        }
                    if (why != null) { foreach (var o in g) Bad(o.File, o.Line, "#wybor " + k + o.Letter + ": grupa odrzucona - " + why); bad.Add(k); }
                }
                var used = new HashSet<string>(StringComparer.Ordinal);
                foreach (var w in _windows)
                {
                    AttachOptions(w.Main, groups, bad, used);
                    foreach (var v in w.Variants) AttachOptions(v, groups, bad, used);
                }
                foreach (var k in order)
                {
                    if (bad.Contains(k) || used.Contains(k)) continue;
                    foreach (var o in groups[k]) Bad(o.File, o.Line, "#wybor " + k + o.Letter + ": opcje bez pytania (kolumna wybor) w " + k);
                }
            }

            private static void AttachOptions(RowDef r, Dictionary<string, List<OptionDef>> groups, HashSet<string> bad, HashSet<string> used)
            {
                if (r.Question.Length == 0) return;
                List<OptionDef> g = null;
                string k = null;
                if (!r.IsMain && groups.ContainsKey(r.Key)) k = r.Key;
                else if (groups.ContainsKey(r.Window.Id)) k = r.Window.Id;
                if (k != null && !bad.Contains(k)) g = groups[k];
                if (g == null)
                {
                    Bad(r.File, r.Line, "#okno " + r.Key + ": pytanie bez opcji #wybor" + (k != null ? " (grupa " + k + " odrzucona)" : "") + " - wiersz zostaje bez listy");
                    r.Question = "";
                    if (k != null) used.Add(k);
                    return;
                }
                used.Add(k);
                r.Options.AddRange(g);
            }

            private static string ParseVars(string cell, List<VarDef> into)
            {
                if (cell.Length == 0) return null;
                foreach (var part in cell.Split(';'))
                {
                    string s = part.Trim();
                    if (s.Length == 0) continue;
                    int eq = s.IndexOf('=');
                    if (eq <= 0) return "'" + s + "' ma byc NAZWA=@token";
                    string n = s.Substring(0, eq).Trim(), t = s.Substring(eq + 1).Trim();
                    if (!(n[0] >= 'A' && n[0] <= 'Z')) return "nazwa '" + n + "' (wielkie litery, cyfry, _)";
                    foreach (char ch in n) if (!((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_')) return "nazwa '" + n + "' (wielkie litery, cyfry, _)";
                    if (!t.StartsWith("@", StringComparison.Ordinal) || t.Length < 2) return "wartosc '" + t + "' ma byc tokenem @...";
                    foreach (var v in into) if (v.Name == n) return "powtorzona zmienna " + n;
                    into.Add(new VarDef { Name = n, Token = t });
                }
                return null;
            }

            private static RecurDef ParseRecur(string cell, out string why)
            {
                why = null;
                var parts = cell.Split(';');
                var r = new RecurDef { Raw = cell };
                string trig = parts[0].Trim();
                if (trig.Length == 0) { why = "brak wyzwalacza"; return null; }
                int c = trig.IndexOf(':');
                r.Trigger = c < 0 ? trig : trig.Substring(0, c);
                r.Arg = c < 0 ? "" : trig.Substring(c + 1);
                for (int i = 1; i < parts.Length; i++)
                {
                    string o = parts[i].Trim();
                    if (o.Length == 0) continue;
                    int n;
                    if (o == "prolog") r.Prolog = true;
                    else if (o.StartsWith("odstep:", StringComparison.Ordinal) && Int(o.Substring(7), out n) && n >= 0) r.Gap = n;
                    else if (o.StartsWith("max:", StringComparison.Ordinal) && Int(o.Substring(4), out n) && n >= 1) r.Max = n;
                    else { why = "opcja '" + o + "' (prolog | odstep:N | max:N)"; return null; }
                }
                return r;
            }

            private static void BandRow(PackInfo p, TsvLine l)
            {
                var c = l.C;
                // id klan osada min max herszt zachowanie dni list po_rozbiciu
                var b = new BandDef { Id = c[0], Clan = c[1], Settlement = c[2], Leader = c[5], Behavior = c[6], AfterRout = SplitEffects(c[9]), File = p.File, Line = l.No };
                string why = null;
                if (b.Id.Length < 4 || b.Id[0] != 'b' || !char.IsDigit(b.Id[1]) || !char.IsDigit(b.Id[2]) || !char.IsDigit(b.Id[3]) || b.Id.Length > 5 || (b.Id.Length == 5 && !IsLetter(b.Id.Substring(4)))) why = "id '" + b.Id + "' ma byc bNNN[litera]";
                else if (b.Clan.Length == 0 || b.Settlement.Length == 0) why = "klan i osada wymagane";
                else if (!Int(c[3], out b.Min) || !Int(c[4], out b.Max) || b.Min < 1 || b.Max > 150 || b.Min > b.Max) why = "min/max '" + c[3] + "'/'" + c[4] + "' (1-150, min <= max)";
                else if (b.Leader.Length == 0 || !TextOk(b.Leader)) why = "herszt wymagany, z prefiksem {=ARM_KR_...}";
                else if (b.Behavior != "patrol" && b.Behavior != "rabunek" && b.Behavior != "oboz") why = "zachowanie '" + b.Behavior + "' (patrol|rabunek|oboz)";
                else if (!Int(c[7], out b.Days) || b.Days < 1 || b.Days > 365) why = "dni '" + c[7] + "' (1-365)";
                else if (c[8] != "0" && c[8] != "1" && c[8] != "") why = "list '" + c[8] + "' (0|1)";
                else if (_bands.ContainsKey(b.Id)) why = "powtorzone id bandy";
                if (why != null) { Bad(p, l.No, "#banda: " + why); return; }
                b.Notice = c[8] == "1";
                _bands[b.Id] = b;
                p.Bands++;
            }

            private static void TaskRow(PackInfo p, TsvLine l)
            {
                var c = l.C;
                // id rodzaj cel co ilosc dni nagroda tytul opis
                var t = new TaskDef { Id = c[0], Kind = c[1], Target = c[2], What = c[3], Reward = SplitEffects(c[6]), Title = c[7], Desc = c[8], File = p.File, Line = l.No };
                string why = null;
                if (t.Id.Length < 4 || t.Id[0] != 't' || !char.IsDigit(t.Id[1]) || !char.IsDigit(t.Id[2]) || !char.IsDigit(t.Id[3]) || t.Id.Length > 5 || (t.Id.Length == 5 && !IsLetter(t.Id.Substring(4)))) why = "id '" + t.Id + "' ma byc tNNN[litera]";
                else if (t.Kind != "deliver" && t.Kind != "defeat" && t.Kind != "prisoners") why = "rodzaj '" + t.Kind + "' (deliver|defeat|prisoners)";
                else if (t.Target.Length == 0 || t.What.Length == 0) why = "cel i co wymagane";
                else if (!Int(c[4], out t.Amount) || t.Amount < 1) why = "ilosc '" + c[4] + "' (>= 1)";
                else if (!Int(c[5], out t.Days) || t.Days < 1 || t.Days > 365) why = "dni '" + c[5] + "' (1-365)";
                else if (t.Title.Length == 0 || t.Desc.Length == 0 || !TextOk(t.Title) || !TextOk(t.Desc)) why = "tytul i opis wymagane, z prefiksem {=ARM_KR_...}";
                else if (_tasks.ContainsKey(t.Id)) why = "powtorzone id zadania";
                if (why != null) { Bad(p, l.No, "#zadanie: " + why); return; }
                _tasks[t.Id] = t;
                p.Tasks++;
            }

            private static void PairRow(PackInfo p, TsvLine l)
            {
                var c = l.C;
                var q = new PairDef { Window = c[0], Row = c[1], Dir = c[2].Length == 0 ? "po" : c[2] };
                string why = null;
                if (!IsWinId(q.Window)) why = "okno '" + q.Window + "' ma byc NNN.w";
                else if (q.Row.Length < 2 || (q.Row[0] != 'm' && q.Row[0] != 'p')) why = "wiersz '" + q.Row + "' ma byc mNN albo pNN";
                else if (q.Dir != "po" && q.Dir != "kr") why = "kierunek '" + q.Dir + "' (po|kr)";
                if (why != null) { Bad(p, l.No, "#para: " + why); return; }
                for (int i = 1; i < q.Row.Length; i++) if (!char.IsDigit(q.Row[i])) { Bad(p, l.No, "#para: wiersz '" + q.Row + "' ma byc mNN albo pNN"); return; }
                _pairs.Add(q);
                p.Pairs++;
            }

            /// <summary>Kolumny warunkow wiersza i jego opcji - nieznane termy (skladnia KRONIKA-FORMAT 10) do licznika i linii; wiersz zostaje (term = falsz).</summary>
            private static void ScanTerms(RowDef r)
            {
                Scan(r.Po, r, "po");
                Scan(r.Przed, r, "przed");
                Scan(r.Cond, r, "warunki");
                if (r.IsMain || r.Window == null || !ReferenceEquals(r.Player, r.Window.Main.Player)) Scan(r.Player, r, "gracz");   // R-3: gracz dziedziczony - liczony raz
                foreach (var o in r.Options) Scan(o.Avail, r, "dostepna " + o.Letter);
            }

            private static void Scan(string expr, RowDef r, string col)
            {
                if (expr.Length == 0 || (!r.IsMain && (col == "po" || col == "przed"))) return;
                string why;
                if (Terms.Syntax(expr, out why)) return;
                _unknownTerms++;
                string where = r.File + ":" + r.Line.ToString(Inv) + " " + r.Key + " " + col;
                if (_firstUnknown.Length == 0) _firstUnknown = where + " " + why;
                if (_unknownTerms <= BadLinesShown)
                    Log.Info(Tag + "NIEZNANY TERM " + where + " (" + why + ") - liczony jako falsz, wiersz zostaje.");
            }

            // ---------------------------------------------------------------- tematy obrazkow (rozdz. 14)
            private static void LoadTopics(string dir)
            {
                string path = dir != null ? System.IO.Path.Combine(dir, TopicsFile) : null;
                if (path == null || !System.IO.File.Exists(path)) { _topicsWhy = "brak pliku " + TopicsFile; return; }
                var cols = new Dictionary<string, string[]>(StringComparer.Ordinal) { { "#temat", TopicCols } };
                string why;
                var bad = new List<KeyValuePair<int, string>>();
                var lines = ReadTsv(path, cols, out why, bad);
                if (lines == null) { _topicsWhy = "ZLY PLIK: " + why; Log.Info(Tag + "ZLY PLIK " + TopicsFile + " (" + why + ") - tematy obrazkow wylaczone."); return; }
                foreach (var b in bad) Bad(TopicsFile, b.Key, b.Value);
                foreach (var l in lines)
                {
                    var c = l.C;
                    var t = new TopicDef { Name = c[0], Note = c[4] };
                    string bwhy = null;
                    if (t.Name.Length == 0) bwhy = "pusty temat";
                    else foreach (char ch in t.Name) if (!((ch >= 'a' && ch <= 'z') || ch == '_')) { bwhy = "temat '" + t.Name + "' (male litery i _)"; break; }
                    if (bwhy == null && _topicBy.ContainsKey(t.Name)) bwhy = "powtorzony temat " + t.Name;
                    t.Sprites = SplitList(c[1]);
                    t.Prolog = SplitList(c[2]);
                    if (bwhy == null)
                        foreach (var n in t.Prolog) { int v; if (n.Length != 2 || !Int(n, out v)) { bwhy = "prolog '" + n + "' (NN)"; break; } }
                    if (bwhy == null && c[3].Length > 0)
                    {
                        t.Only = new HashSet<int>();
                        foreach (var n in SplitList(c[3])) { int v; if (!Int(n, out v) || v < 0) { bwhy = "tylko '" + n + "' (numery NNN)"; break; } t.Only.Add(v); }
                    }
                    if (bwhy != null) { Bad(TopicsFile, l.No, "#temat: " + bwhy); continue; }
                    _topics.Add(t);
                    _topicBy[t.Name] = t;
                }
            }

            private static string[] SplitList(string cell)
            {
                if (cell.Length == 0) return NoStrings;
                var l = new List<string>();
                foreach (var a in cell.Split(',')) { var t = a.Trim(); if (t.Length > 0) l.Add(t); }
                return l.ToArray();
            }
        }
    }
}
