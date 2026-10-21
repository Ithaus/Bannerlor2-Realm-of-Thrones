using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Armoury
{
    internal static partial class Chronicle
    {
        /// <summary>
        /// m105-kronika K0 (krok A1) - stan okien Kroniki czytany przez termy kr:NNN.w[=x] i flag:x (Chronicle.Terms). Pisze go silnik (krok A2: rozstrzygniecie
        /// okna, pominiecie z powodem; A3: wybor opcji, akcja flag:). Trwaly w zapisie: Export/Import daja linie z wlasnymi prefiksami (kr/ks/kc/kf) - wolajacy
        /// (ChronicleBehavior.SyncData, SaveText.Sync) moze sklejac je z innymi liniami; Import czyta tylko swoje prefiksy.
        /// Id okna: NNN.w albo instancja okna powracajacego NNN.w#klucz (kr:NNN.w = rozstrzygnieta jakakolwiek instancja). Brak logiki gry - same slowniki.
        /// </summary>
        internal static class State
        {
            private sealed class Res
            {
                public int Day;
                public string Form = "", Row = "";
            }

            private static readonly Dictionary<string, Res> _res = new Dictionary<string, Res>(StringComparer.Ordinal);       // id -> rozstrzygniecie
            private static readonly Dictionary<string, string> _skip = new Dictionary<string, string>(StringComparer.Ordinal);  // id -> "dzien|powod"
            private static readonly Dictionary<string, char> _choice = new Dictionary<string, char>(StringComparer.Ordinal);    // id (NNN.w) -> opcja a-d
            private static readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);
            private static readonly Dictionary<string, int> _inst = new Dictionary<string, int>(StringComparer.Ordinal);        // NNN.w -> pierwszy dzien instancji (bez zapisu)

            internal static int Resolved { get { return _res.Count; } }
            internal static int Skipped { get { return _skip.Count; } }
            internal static int Choices { get { return _choice.Count; } }
            internal static int Flags { get { return _flags.Count; } }

            /// <summary>Nowa kampania / przed wczytaniem: stan od zera.</summary>
            internal static void Reset() { _res.Clear(); _skip.Clear(); _choice.Clear(); _flags.Clear(); _inst.Clear(); }

            /// <summary>Okno rozstrzygniete (pokazane jako okno/wiesc/log albo "nic" dla gracza spoza kolumny gracz - skutki i tak wykonane).
            /// form: okno|wiesc|log|nic; rowKey: wiersz, ktory wygral (NNN.w albo NNN.w + litera) - kr:NNN.w=x wie, czy ten wiersz ma liste. Raz: drugie wolanie nic nie zmienia.</summary>
            internal static void Resolve(string id, int day, string form, string rowKey)
            {
                if (string.IsNullOrEmpty(id) || _res.ContainsKey(id)) return;
                _res[id] = new Res { Day = day, Form = Clean(form), Row = Clean(rowKey) };
                NoteInstance(id, day);
            }

            /// <summary>Okno pominiete z powodem (POMINIETE) - kr: tego okna nigdy nie zajdzie. Okno juz rozstrzygniete nie przechodzi w pominiete.</summary>
            internal static void Skip(string id, int day, string why)
            {
                if (string.IsNullOrEmpty(id) || _res.ContainsKey(id) || _skip.ContainsKey(id)) return;
                _skip[id] = day.ToString(Inv) + "|" + Clean(why);
            }

            /// <summary>Wybor opcji (takze bierna bez pytania) - zapisywany pod id okna NNN.w (KRONIKA-FORMAT 4). Raz.</summary>
            internal static void Choose(string id, char opt)
            {
                if (string.IsNullOrEmpty(id) || opt < 'a' || opt > 'd' || _choice.ContainsKey(id)) return;
                _choice[id] = opt;
            }

            internal static void SetFlag(string name) { if (!string.IsNullOrEmpty(name)) _flags.Add(name); }

            internal static bool Flag(string name) { return name != null && _flags.Contains(name); }

            /// <summary>Rozstrzygniete: to id albo (dla NNN.w) jakakolwiek instancja NNN.w#klucz.</summary>
            internal static bool IsResolved(string id) { return id != null && (_res.ContainsKey(id) || _inst.ContainsKey(id)); }

            internal static bool IsSkipped(string id) { return id != null && _skip.ContainsKey(id); }

            /// <summary>Rozstrzygniete albo pominiete - okno juz nie nalezne.</summary>
            internal static bool IsDone(string id) { return id != null && (_res.ContainsKey(id) || _skip.ContainsKey(id)); }

            /// <summary>Dzien kampanii rozstrzygniecia (dla NNN.w bez wlasnego wpisu - pierwszej instancji); -1 brak.</summary>
            internal static int ResolvedDay(string id)
            {
                Res r; int d;
                if (id == null) return -1;
                if (_res.TryGetValue(id, out r)) return r.Day;
                return _inst.TryGetValue(id, out d) ? d : -1;
            }

            internal static string ResolvedForm(string id) { Res r; return id != null && _res.TryGetValue(id, out r) ? r.Form : null; }

            internal static string ResolvedRow(string id) { Res r; return id != null && _res.TryGetValue(id, out r) ? r.Row : null; }

            internal static string SkipWhy(string id) { string v; return id != null && _skip.TryGetValue(id, out v) ? v : null; }

            /// <summary>Wybrana opcja a-d albo '\0'.</summary>
            internal static char Choice(string id) { char c; return id != null && _choice.TryGetValue(id, out c) ? c : '\0'; }

            /// <summary>Liczba rozstrzygnietych instancji okna powracajacego NNN.w (wpisy NNN.w#...).</summary>
            internal static int InstanceCount(string id)
            {
                if (id == null) return 0;
                int n = 0;
                string pre = id + "#";
                foreach (var k in _res.Keys) if (k.StartsWith(pre, StringComparison.Ordinal)) n++;
                return n;
            }

            private static void NoteInstance(string id, int day)
            {
                int h = id.IndexOf('#');
                if (h <= 0) return;
                string b = id.Substring(0, h);
                int d;
                if (!_inst.TryGetValue(b, out d) || day < d) _inst[b] = day;
            }

            private static string Clean(string s)
            {
                if (string.IsNullOrEmpty(s)) return "";
                return s.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ').Replace('|', '/');
            }

            /// <summary>Linie zapisu: kr id dzien forma wiersz | ks id dzien|powod | kc id opcja | kf flaga.</summary>
            internal static string Export()
            {
                var sb = new StringBuilder(64 * (_res.Count + _skip.Count) + 16 * (_choice.Count + _flags.Count));
                foreach (var kv in _res) sb.Append("kr\t").Append(kv.Key).Append('\t').Append(kv.Value.Day.ToString(Inv)).Append('\t').Append(kv.Value.Form).Append('\t').Append(kv.Value.Row).Append('\n');
                foreach (var kv in _skip) sb.Append("ks\t").Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
                foreach (var kv in _choice) sb.Append("kc\t").Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
                foreach (var f in _flags) sb.Append("kf\t").Append(f).Append('\n');
                return sb.ToString();
            }

            /// <summary>Wczytanie: Reset, potem linie z prefiksami kr/ks/kc/kf (inne pomijane). Pusty napis (zapis sprzed Kroniki) - stan pusty.</summary>
            internal static void Import(string s)
            {
                Reset();
                if (string.IsNullOrEmpty(s)) return;
                foreach (var raw in s.Split('\n'))
                {
                    var p = raw.TrimEnd('\r').Split('\t');
                    if (p.Length < 2 || p[1].Length == 0) continue;
                    int d;
                    switch (p[0])
                    {
                        case "kr":
                            if (p.Length >= 3 && int.TryParse(p[2], NumberStyles.AllowLeadingSign, Inv, out d))
                            {
                                _res[p[1]] = new Res { Day = d, Form = p.Length >= 4 ? p[3] : "", Row = p.Length >= 5 ? p[4] : "" };
                                NoteInstance(p[1], d);
                            }
                            break;
                        case "ks": if (p.Length >= 3) _skip[p[1]] = p[2]; break;
                        case "kc": if (p.Length >= 3 && p[2].Length == 1 && p[2][0] >= 'a' && p[2][0] <= 'd') _choice[p[1]] = p[2][0]; break;
                        case "kf": _flags.Add(p[1]); break;
                    }
                }
            }
        }
    }
}
