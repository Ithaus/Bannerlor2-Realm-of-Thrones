using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.Core;
using IOPath = System.IO.Path;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P2: KATALOG KLIPOW BITWY (raz na proces, przy pierwszej bitwie z dzwiekiem). Zrodlo: Armoury/ModuleData/Lektor/Bitwa/manifest.tsv (P2a,
    /// tools/lektor/nagraj_bitwa.py: wiersze '#' - naglowek; kolumny TAB albo ';': id, kategoria, kultura/rod, plik (wzgledem Bitwa), glos, dlugosc_s, tekst, rola, ...);
    /// bez manifestu - przeglad katalogow Bitwa/&lt;kategoria&gt;/*.ogg (dlugosc z naglowka Ogg). Klucz klipu z id (arm_bs_&lt;kod&gt;_&lt;reszta&gt;[_NN[b]]):
    /// sp przemowa, mo motto, or rozkaz (region_rozkaz), hr rog (styl_sygnal), dr beben (styl), br zlamanie, bd sztandar wroga, ob nasz sztandar, vi zwyciestwo,
    /// tn wyzwisko, jk jek, ko kon, pt ptaki, wi wiatr, rn deszcz, fi ogien, sz szczek, cr tlum. Kontrola pliku jak Lektor (OggS, Vorbis, czestotliwosc,
    /// bloki zer na koncu i w srodku - dysk C Jeffa zeruje pliki); zly albo brakujacy plik = klip pominiety, licznik w linii katalogu (cisza zamiast bledu silnika).
    /// Wybor: losowy z listy klucza, bez powtorki ostatniego (jeki: bez glosu z trzech ostatnich).
    /// </summary>
    internal static partial class BattleSound
    {
        internal const string ManifestName = "manifest.tsv";
        private const int ZeroBlock = 4096;

        private sealed class Clip
        {
            internal string Id, Code, Key, Path, Role, Cat;
            internal float Dur;
        }

        private sealed class ClipList
        {
            internal readonly List<Clip> L = new List<Clip>();
            internal int Last = -1;
        }

        private static List<Clip> _clips;
        private static readonly Dictionary<string, ClipList> _byKey = new Dictionary<string, ClipList>(StringComparer.Ordinal);
        private static readonly Random _rng = new Random();
        private static string _dir;
        private static bool _manifest;
        private static int _catRows, _catBad, _catMissing, _catBadLines, _catDup, _catNoDur, _catStereo;
        private static string _firstBad, _firstMissing;

        private static void EnsureCatalog()
        {
            if (_clips != null) return;
            _clips = new List<Clip>();
            try { LoadCatalog(); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Katalog", e); }
            try { CatalogLine(); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.KatalogLinia", e); }
        }

        private static void LoadCatalog()
        {
            _dir = IOPath.Combine(IOPath.Combine(IOPath.Combine(Lektor.ModuleDir(), "ModuleData"), "Lektor"), "Bitwa");
            string man = IOPath.Combine(_dir, ManifestName);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (File.Exists(man))
            {
                _manifest = true;
                int iId = 0, iCat = 1, iFile = 3, iVoice = 4, iDur = 5, iRole = 7;
                foreach (var raw in File.ReadAllLines(man, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    string line = raw.TrimEnd('\r', '\n');
                    char sep = line.IndexOf('\t') >= 0 ? '\t' : ';';
                    if (line.StartsWith("#", StringComparison.Ordinal))
                    {
                        // naglowek: kolumny po nazwie (P2a moze dopisac kolumny)
                        var h = line.TrimStart('#', ' ').Split(sep);
                        for (int i = 0; i < h.Length; i++)
                        {
                            string n = h[i].Trim();
                            if (n == "id") iId = i; else if (n == "kategoria") iCat = i; else if (n == "plik") iFile = i; else if (n == "glos") iVoice = i;
                            else if (n == "dlugosc_s") iDur = i; else if (n == "rola") iRole = i;
                        }
                        continue;
                    }
                    var col = line.Split(sep);
                    if (col.Length <= Math.Max(iId, Math.Max(iCat, iFile))) { _catBadLines++; continue; }
                    _catRows++;
                    string id = col[iId].Trim();
                    if (id.Length == 0 || !seen.Add(id)) { _catDup++; continue; }
                    string rel = col[iFile].Trim().Replace('/', IOPath.DirectorySeparatorChar);
                    string path = IOPath.Combine(_dir, rel);
                    float dur = 0f;
                    if (iDur < col.Length) float.TryParse(col[iDur].Trim(), System.Globalization.NumberStyles.Float, Inv, out dur);
                    string role = iRole < col.Length ? col[iRole].Trim() : null;
                    if (string.IsNullOrEmpty(role) && iVoice < col.Length) role = col[iVoice].Trim();
                    Add(id, col[iCat].Trim(), path, dur, role);
                }
                return;
            }
            // bez manifestu: katalogi kategorii (do czasu nagran P2a - zwykle pusto, cisza z licznikiem)
            if (!Directory.Exists(_dir)) return;
            foreach (var d in Directory.GetDirectories(_dir))
            {
                string cat = IOPath.GetFileName(d);
                var files = Directory.GetFiles(d, "arm_bs_*.ogg");
                Array.Sort(files, StringComparer.Ordinal);
                foreach (var f in files)
                {
                    _catRows++;
                    string id = IOPath.GetFileNameWithoutExtension(f);
                    if (!seen.Add(id)) { _catDup++; continue; }
                    Add(id, cat, f, 0f, null);
                }
            }
        }

        private static void Add(string id, string cat, string path, float dur, string role)
        {
            string code, key;
            if (!ParseId(id, out code, out key)) { _catBadLines++; return; }
            if (!File.Exists(path))
            {
                _catMissing++;
                if (_firstMissing == null) _firstMissing = id;
                return;
            }
            double sec;
            string why = CheckClip(path, out sec);
            if (why != null)
            {
                _catBad++;
                if (_firstBad == null) _firstBad = id + " (" + why + ")";
                return;
            }
            if (dur <= 0f) dur = (float)sec;
            if (dur <= 0f) { _catNoDur++; dur = DefaultDur(code); }
            var c = new Clip { Id = id, Code = code, Key = key, Path = path, Role = string.IsNullOrEmpty(role) ? id : role, Cat = cat, Dur = dur };
            _clips.Add(c);
            ClipList l;
            string k = code + ":" + key;
            if (!_byKey.TryGetValue(k, out l)) { l = new ClipList(); _byKey[k] = l; }
            l.L.Add(c);
        }

        /// <summary>arm_bs_&lt;kod&gt;_&lt;reszta&gt;[_NN[b]] -> kod, klucz (reszta bez numeru). Jeki arm_bs_jk_NN -> klucz pusty.</summary>
        private static bool ParseId(string id, out string code, out string key)
        {
            code = key = null;
            if (id == null || !id.StartsWith("arm_bs_", StringComparison.Ordinal) || id.Length < 10) return false;
            string rest = id.Substring(7);
            int u = rest.IndexOf('_');
            code = u < 0 ? rest : rest.Substring(0, u);
            string tail = u < 0 ? "" : rest.Substring(u + 1);
            // numer na koncu: _NN albo _NNb (albo caly ogon to numer)
            int cut = tail.LastIndexOf('_');
            string last = cut < 0 ? tail : tail.Substring(cut + 1);
            if (IsNumber(last)) tail = cut < 0 ? "" : tail.Substring(0, cut);
            key = tail;
            return code.Length > 0;
        }

        private static bool IsNumber(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            int n = s.Length;
            if (s[n - 1] == 'b') n--;
            if (n == 0) return false;
            for (int i = 0; i < n; i++) if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }

        private static float DefaultDur(string code)
        {
            switch (code)
            {
                case "sp": return 7f;
                case "dr": return 8f;
                case "wi": case "rn": case "fi": return 12f;
                case "sz": case "pt": case "ko": case "cr": return 6f;
                case "hr": return 4f;
                default: return 2.5f;
            }
        }

        /// <summary>null = plik dobry. Jak Lektor.CheckOgg, ale bez progu dlugosci 8 KB (krotkie rozkazy) - bloki zer tylko w plikach od 3 x 4 KB.</summary>
        private static string CheckClip(string path, out double sec)
        {
            sec = 0;
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) return "brak pliku";
                long n = fi.Length;
                if (n < 512) return "za krotki (" + n.ToString(Inv) + " B)";
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var h = new byte[64];
                    if (ReadAt(fs, 0, h, 64) != 64) return "naglowek nieczytelny";
                    if (h[0] != 'O' || h[1] != 'g' || h[2] != 'g' || h[3] != 'S' || h[4] != 0) return "naglowek bez OggS (zera?)";
                    int p = 27 + h[26];
                    if (p + 16 > h.Length) return "pierwsza strona Ogg nietypowa";
                    if (h[p] != 1 || h[p + 1] != 'v' || h[p + 2] != 'o' || h[p + 3] != 'r' || h[p + 4] != 'b' || h[p + 5] != 'i' || h[p + 6] != 's')
                        return "Ogg bez naglowka Vorbis";
                    int rate = BitConverter.ToInt32(h, p + 12);
                    if (rate < 8000 || rate > 192000) return "zla czestotliwosc " + rate.ToString(Inv);
                    int ch = h[p + 11];
                    if (ch < 1 || ch > 2) return "kanalow " + ch.ToString(Inv);
                    if (ch == 2) _catStereo++;   // stereo gra, ale 3D wymaga mono (P2a nagrywa mono) - licznik w linii katalogu
                    if (n >= 3 * ZeroBlock)
                    {
                        if (Zeros(fs, n - ZeroBlock)) return "koniec pliku z zer";
                        if (Zeros(fs, (n / 2) & ~1L)) return "srodek pliku z zer";
                    }
                    sec = OggSec(fs, n, rate);
                }
                return null;
            }
            catch (Exception e) { return "blad odczytu " + e.GetType().Name; }
        }

        private static double OggSec(FileStream fs, long n, int rate)
        {
            int len = (int)Math.Min(n, 65536);
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

        private static void CatalogLine()
        {
            var per = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var c in _clips) { int n; per.TryGetValue(c.Code, out n); per[c.Code] = n + 1; }
            var sb = new StringBuilder(600);
            sb.Append(P).Append("katalog - ").Append(_manifest ? "manifest TAK" : "manifest NIE (przeglad katalogow)").Append(", wierszy ").Append(_catRows)
              .Append(", klipow dobrych ").Append(_clips.Count).Append(" (");
            bool first = true;
            foreach (var kv in per) { sb.Append(first ? "" : ", ").Append(kv.Key).Append(' ').Append(kv.Value); first = false; }
            if (first) sb.Append("brak");
            sb.Append("), zle pliki ").Append(_catBad).Append(_firstBad != null ? " (pierwszy " + _firstBad + ")" : "")
              .Append(", brak plikow ").Append(_catMissing).Append(_firstMissing != null ? " (pierwszy " + _firstMissing + ")" : "")
              .Append(", zle wiersze ").Append(_catBadLines).Append(", duble ").Append(_catDup).Append(", bez dlugosci ").Append(_catNoDur).Append(", stereo ").Append(_catStereo)
              .Append("; zdarzenie ").Append(VoiceEvent).Append(" (3D: scena misji + SetPosition), katalog ").Append(_dir ?? "?")
              .Append(", probka autotestu ").Append(TestWanted() ? "TAK" : "NIE").Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>Klip klucza "kod:klucz" - losowy, bez powtorki ostatniego; null = brak (cisza).</summary>
        private static Clip Pick(string code, string key)
        {
            if (key == null) return null;
            ClipList l;
            if (!_byKey.TryGetValue(code + ":" + key, out l) || l.L.Count == 0) return null;
            int n = l.L.Count, i;
            if (n == 1) i = 0;
            else if (l.Last < 0 || l.Last >= n) i = _rng.Next(n);
            else { i = _rng.Next(n - 1); if (i >= l.Last) i++; }
            l.Last = i;
            return l.L[i];
        }

        private static bool Has(string code, string key)
        {
            ClipList l;
            return key != null && _byKey.TryGetValue(code + ":" + key, out l) && l.L.Count > 0;
        }

        /// <summary>Jek: rozne glosy - klip, ktorego glosu (rola z manifestu) nie bylo w trzech ostatnich jekach; inaczej inny niz ostatni.</summary>
        private static Clip PickMoan(List<string> lastRoles)
        {
            ClipList l;
            if (!_byKey.TryGetValue("jk:", out l) || l.L.Count == 0) return null;
            int n = l.L.Count;
            int start = _rng.Next(n);
            for (int k = 0; k < n; k++)
            {
                var c = l.L[(start + k) % n];
                if (!lastRoles.Contains(c.Role) && (start + k) % n != l.Last) { l.Last = (start + k) % n; return c; }
            }
            return Pick("jk", "");
        }
    }
}
