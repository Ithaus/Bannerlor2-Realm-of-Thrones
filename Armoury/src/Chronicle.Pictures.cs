using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TaleWorlds.Engine.GauntletUI;

namespace Armoury
{
    internal static partial class Chronicle
    {
        /// <summary>
        /// m105-kronika K0 (krok A3) - OBRAZKI OKIEN KRONIKI (KRONIKA-FORMAT 14, plan P8; bez grafik Jeffa i bez promptow).
        /// PULA tematu wiersza (arm_kronika_tematy.tsv, Data.Topic), w tej kolejnosci: (1) pliki Images/Kronika/kronika_&lt;temat&gt;_&lt;n&gt;.png|jpg|jpeg (n - liczba, rosnaco;
        /// katalog czytany przy kazdym oknie - nowy plik wchodzi bez restartu), (2) Images/Prolog/prolog_NN z kolumny prolog, gdy plik istnieje (dzis 01-03),
        /// (3) sprite'y ROT z kolumny sprite - bez sprite'ow poza rotacja (Banned) i bez nieznanych w SpriteData (gdy SpriteData jest). stannis-slain tylko w oknach
        /// wydarzenia 081 (temat stannis_upadek ma tylko = 081; okno spoza kolumny tylko dostaje sprite zastepczy Neutral, nie temat).
        /// WYBOR: indeks = staly skrot FNV-1a id okna (NNN.w albo instancja NNN.w#klucz) mod n; gdy wypada ten sam obrazek, co w poprzednim oknie Kroniki tej sesji -
        /// nastepny (poprzedni idzie na koniec kolejki prob). Plik przez StoryPictures.Acquire (walidacja bajtow, sprite arm_*; zwolnienie po zamknieciu okna -
        /// Chronicle.OnClosed -> StoryPictures.Drop), jak Prologue.Scenes; plik, ktory nie wszedl (zly, usuniety) - nastepny element puli z powodem w opisie.
        /// Brak tematu albo pusta pula - null (silnik: Chronicle.DefaultSprite). Bez wyjatkow na zewnatrz (wolajacy i tak lapie).
        /// </summary>
        internal static class Pictures
        {
            /// <summary>Sprite zastepczy (temat spoza kolumny tylko, pusta pula): list i kruk - pasuje do kazdej wiesci.</summary>
            internal const string Neutral = "messenger-img";
            internal const string StannisSprite = "stannis-slain";
            internal const int StannisEvent = 81;
            // poza rotacja (FORMAT 14) - nawet gdyby trafily do pliku tematow
            private static readonly HashSet<string> Banned = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "event-bg", "BlackwaterWarning", "Davos", "Stannis", "Ship1", "Ship2", "testevent", "jonsnowres", "RobbMarching", "jon_death", "ramsay_death" };
            private static readonly string[] Ext = { ".png", ".jpg", ".jpeg" };

            /// <summary>Element puli: plik (Dir + Stem -&gt; StoryPictures.Acquire) albo sprite ROT. Name - do porownania z poprzednim obrazkiem i do logu.</summary>
            internal sealed class Item
            {
                public string Name = "", Dir, Stem, Sprite;
                public bool IsFile { get { return Dir != null; } }
            }

            private static string _last;   // ostatni obrazek okna Kroniki w sesji (Item.Name)
            private static int _picks, _filePicks, _fileFails;

            /// <summary>Nowa kampania / wczytanie (Chronicle.ResetCampaign): bez pamieci poprzedniego obrazka.</summary>
            internal static void Reset() { _last = null; _picks = _filePicks = _fileFails = 0; }

            internal static string Last { get { return _last; } }

            /// <summary>Okno sie nie otworzylo (StoryWindow.Show = null): pamiec poprzedniego obrazka wraca do stanu sprzed Pick (prev = Last sprzed Pick).</summary>
            internal static void Restore(string prev) { _last = prev; }

            /// <summary>Staly skrot id okna (FNV-1a 32 bit po znakach UTF-16) mod n - ten sam w kazdej sesji i na kazdym komputerze.</summary>
            internal static int Index(string id, int n)
            {
                if (n <= 0) return 0;
                uint h = 2166136261u;
                foreach (char ch in id ?? "") { h ^= ch; h *= 16777619u; }
                return (int)(h % (uint)n);
            }

            /// <summary>Czy temat wolno dac oknu wydarzenia ev (kolumna tylko). null = wolno; inaczej powod.</summary>
            internal static string TopicWhy(Data.TopicDef t, int ev)
            {
                if (t == null) return "temat nieznany";
                if (t.Only != null && !t.Only.Contains(ev)) return "temat " + t.Name + " tylko dla wydarzen " + OnlyText(t);
                return null;
            }

            private static string OnlyText(Data.TopicDef t)
            {
                var l = new List<int>(t.Only);
                l.Sort();
                var parts = new List<string>();
                foreach (int n in l) parts.Add(n.ToString("000", CultureInfo.InvariantCulture));
                return string.Join(",", parts.ToArray());
            }

            /// <summary>Pula tematu dla okna wydarzenia ev (kolejnosc: pliki Kroniki, prolog_NN, sprite'y ROT). note - pominiete sprite'y (do logu).</summary>
            internal static List<Item> Pool(Data.TopicDef t, int ev, out string note)
            {
                note = "";
                var pool = new List<Item>();
                if (t == null) return pool;
                // (1) pliki Images/Kronika/kronika_<temat>_<n>
                foreach (var kv in ChronicleFiles(t.Name)) pool.Add(new Item { Name = kv.Value, Dir = StoryPictures.DirChronicle, Stem = kv.Value });
                // (2) Images/Prolog/prolog_NN z kolumny prolog (tylko istniejace)
                foreach (var nn in t.Prolog)
                {
                    string stem = "prolog_" + nn;
                    if (StoryPictures.FindFile(StoryPictures.DirPrologue, stem) != null) pool.Add(new Item { Name = stem, Dir = StoryPictures.DirPrologue, Stem = stem });
                }
                // (3) sprite'y ROT
                var data = SpriteDataOrNull();
                var skipped = new List<string>();
                foreach (var s in t.Sprites)
                {
                    if (Banned.Contains(s)) { skipped.Add(s + " (poza rotacja)"); continue; }
                    if (string.Equals(s, StannisSprite, StringComparison.OrdinalIgnoreCase) && ev != StannisEvent) { skipped.Add(s + " (tylko 081)"); continue; }
                    if (data != null && data.GetSprite(s) == null) { skipped.Add(s + " (brak w SpriteData)"); continue; }
                    bool dup = false;
                    foreach (var it in pool) if (!it.IsFile && it.Sprite == s) { dup = true; break; }
                    if (!dup) pool.Add(new Item { Name = s, Sprite = s });
                }
                if (skipped.Count > 0) note = "pominiete sprite'y: " + string.Join(", ", skipped.ToArray());
                return pool;
            }

            private static TaleWorlds.TwoDimension.SpriteData SpriteDataOrNull()
            {
                try { return UIResourceManager.SpriteData; }
                catch { return null; }   // poza gra (harness) - bez kontroli nazw sprite'ow
            }

            /// <summary>Pliki kronika_&lt;temat&gt;_&lt;n&gt;.png|jpg|jpeg w Images/Kronika: (n, stem) rosnaco po n, jeden stem raz (rozszerzenie wybiera FindFile/Acquire).</summary>
            internal static List<KeyValuePair<int, string>> ChronicleFiles(string topic)
            {
                var l = new List<KeyValuePair<int, string>>();
                try
                {
                    string d = StoryPictures.Dir(StoryPictures.DirChronicle);
                    if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) return l;
                    string pre = "kronika_" + topic + "_";
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string f in Directory.GetFiles(d, pre + "*"))
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (Array.IndexOf(Ext, ext) < 0) continue;
                        string stem = Path.GetFileNameWithoutExtension(f);
                        string num = stem.Substring(pre.Length);
                        int n;
                        if (num.Length == 0 || num.Length > 4 || !AllDigits(num) || !int.TryParse(num, NumberStyles.None, CultureInfo.InvariantCulture, out n)) continue;   // kronika_dwor_x_1 - inny temat
                        if (!seen.Add(stem)) continue;
                        l.Add(new KeyValuePair<int, string>(n, stem));
                    }
                    l.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : string.CompareOrdinal(a.Value, b.Value));
                }
                catch (Exception e) { Stumble("Pictures.ChronicleFiles(" + topic + ")", e); }
                return l;
            }

            private static bool AllDigits(string s)
            {
                foreach (char ch in s) if (ch < '0' || ch > '9') return false;
                return true;
            }

            /// <summary>Kolejnosc prob dla okna: od indeksu skrotu po kolei; element rowny poprzedniemu obrazkowi sesji (prev) - na koniec (gdy pula ma &gt; 1).</summary>
            internal static List<Item> Order(List<Item> pool, string id, string prev, out int index, out bool repeat)
            {
                int n = pool.Count;
                index = Index(id, n);
                repeat = false;
                var outp = new List<Item>(n);
                Item back = null;
                for (int k = 0; k < n; k++)
                {
                    var it = pool[(index + k) % n];
                    if (n > 1 && prev != null && it.Name == prev) { back = it; if (k == 0) repeat = true; continue; }
                    outp.Add(it);
                }
                if (back != null) outp.Add(back);
                return outp;
            }

            /// <summary>Nazwa sprite'a do HeaderImage okna (arm_* z pliku albo sprite ROT) albo null (silnik - Chronicle.DefaultSprite). how - opis do linii
            /// "okno ... pokazane". w - okno, r - wiersz, ktory wygral (temat dziedziczony z glownego), id - NNN.w albo instancja NNN.w#klucz.</summary>
            internal static string Pick(Data.WindowDef w, Data.RowDef r, string id, out string how)
            {
                how = "-";
                if (r == null || w == null) { how = "brak wiersza"; return null; }
                if (r.Topic.Length == 0) { how = "wiersz bez tematu"; return null; }
                var t = Data.Topic(r.Topic);
                if (t == null) { how = "temat '" + r.Topic + "' nieznany (arm_kronika_tematy.tsv)"; return null; }
                string tw = TopicWhy(t, w.Event);
                if (tw != null) { how = tw + " - sprite zastepczy"; _picks++; _last = Neutral; return Neutral; }
                string note;
                var pool = Pool(t, w.Event, out note);
                int files = 0, prolog = 0;
                foreach (var it in pool) { if (it.IsFile) { if (it.Dir == StoryPictures.DirChronicle) files++; else prolog++; } }
                string poolText = "pula " + pool.Count + " (plikow Kroniki " + files + ", prolog " + prolog + ", ROT " + (pool.Count - files - prolog) + ")" + (note.Length > 0 ? ", " + note : "");
                if (pool.Count == 0) { how = poolText + " - pusta"; return null; }
                int index; bool repeat;
                var order = Order(pool, id, _last, out index, out repeat);
                var fails = new List<string>();
                foreach (var it in order)
                {
                    string header = it.Sprite, fh = null;
                    if (it.IsFile)
                    {
                        header = StoryPictures.Acquire(it.Dir, it.Stem, out fh);
                        if (header == null) { _fileFails++; fails.Add(it.Stem + ": " + fh); continue; }
                        _filePicks++;
                    }
                    _picks++;
                    _last = it.Name;
                    how = poolText + ", indeks " + index + (repeat ? " (poprzedni obrazek sesji - nastepny)" : "") + " -> " + it.Name + (fh != null ? " [" + fh + "]" : "")
                          + (fails.Count > 0 ? ", nie weszly: " + string.Join("; ", fails.ToArray()) : "");
                    return header;
                }
                how = poolText + " - zaden obrazek nie wszedl: " + string.Join("; ", fails.ToArray());
                return null;
            }

            /// <summary>Sprite, gdy Pick nie da obrazka: pierwszy dozwolony sprite tematu (bez poza rotacja, stannis-slain tylko 081, kolumna tylko), inaczej Neutral.</summary>
            internal static string Fallback(Data.RowDef r)
            {
                var t = r != null ? Data.Topic(r.Topic) : null;
                int ev = r != null && r.Window != null ? r.Window.Event : -1;
                if (t == null || TopicWhy(t, ev) != null) return Neutral;
                foreach (var s in t.Sprites)
                {
                    if (Banned.Contains(s)) continue;
                    if (string.Equals(s, StannisSprite, StringComparison.OrdinalIgnoreCase) && ev != StannisEvent) continue;
                    return s;
                }
                return Neutral;
            }

            /// <summary>Do linii startu Kroniki (plan G "tematy n, sprite'ow n, plikow n"): pliki Kroniki i prologu w pulach.</summary>
            internal static string StartText()
            {
                int kr = 0, pr = 0, prAll = 0;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var t in Data.Topics)
                {
                    kr += ChronicleFiles(t.Name).Count;
                    foreach (var nn in t.Prolog)
                    {
                        if (!seen.Add(nn)) continue;
                        prAll++;
                        if (StoryPictures.FindFile(StoryPictures.DirPrologue, "prolog_" + nn) != null) pr++;
                    }
                }
                return "obrazki: plikow Kroniki w tematach " + kr + " (Images/Kronika: " + StoryPictures.CountFiles(StoryPictures.DirChronicle, "kronika_") + "), prolog_NN z tematow " + pr + "/" + prAll;
            }

            /// <summary>Do linii doby: obrazki od wczytania (pliki odrzucone - zly albo usuniety plik puli).</summary>
            internal static string DayText()
            {
                return "obrazki w sesji: wybrane " + _picks + " (z pliku " + _filePicks + ", pliki odrzucone " + _fileFails + ")";
            }
        }
    }
}
