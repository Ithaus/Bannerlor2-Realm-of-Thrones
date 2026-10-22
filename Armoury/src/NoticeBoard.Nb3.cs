using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Armoury
{
    /// <summary>
    /// m107-gospodarka K6 (Notice Board Lean Save; Jeff 22.10 "zapis tablicy ogloszen tez zrob"; L-8: arm_board 12.5-14.3 KB > 12288 B, 71% napisu to szkody band) - KODEK ZAPISU
    /// arm_board: tablica przedrostkow id (IdTable), format nb3, wspolny odczyt nb2/nb3 (Parse) i proba odczytu nb3 przed zapisem (Probe3). Plik bez typow gry (System.* i skladowe
    /// klasy czesciowej) - ten sam kod kompiluje test poza gra (m107/bench).
    /// nb3 = "nb3|D=doba|P=przedrostki|T=miasta|F=k~id~doba-D;...|W=...|H=..." (wariant B z m107/sim_nb3.py):
    ///  - IdTable(v3): poza podzialem nb2 (do ostatniego '_') takze podzial na granicy litera/cyfra koncowki z samych cyfr: ROT_town12 -> przedrostek "ROT_town", kod "^k-12"
    ///    (cyfry doslownie); wybierany krotszy kod (remis - podzial nb2);
    ///  - T: miasta listow i szkod (ids.Enc, ',' -> '.' jak w nb2); w W i H pole miasta = numer base36 w T (puste - jak w nb2);
    ///  - W: "miasto~banda~ludzie~kwota~termin-D~TL~rodzaj~rod pana[~soltys~wies]" - rodzaj 0 jako puste pole;
    ///  - H: "banda~wpis/wpis/..." - wpis = base36(przesuniecie x 2 + rodzaj)[,miasto[,wies]] (rodzaje tylko 0/1 - AddHarm), reguly pomijania miasta jak w nb2.
    /// Bez utraty danych: kazdy eksport nb3 jest od razu czytany (Parse do osobnych kontenerow) i porownywany pole po polu z zywym stanem filtrowanym jak eksport;
    /// niezgodnosc albo wyjatek - zapis nb2 (NoticeBoard.Export) i linia "Tablica (m107): proba odczytu nb3 NIEZGODNA". Odczyt nb3 niezalezny od klucza.
    /// </summary>
    internal static partial class NoticeBoard
    {
        private const string Ver3 = "nb3";
        internal const string P7 = "Tablica (m107): ";

        // ------------------------------------------------------------ tablica przedrostkow id (nb2: przeniesione bez zmian z NoticeBoard.cs; nb3: podzial litera/cyfra)

        /// <summary>m103-drobne: tablica przedrostkow id do zapisu nb2 - przedrostek (do ostatniego '_') raz w P, w tekscie "^nr_koncowka" (tylko gdy krocej);
        /// koncowka liczbowa od 5 cyfr bez zera na przodzie (bandy ROT: looters_2147131604) - "^nr:liczba w base36" (10 cyfr -> 6 znakow).
        /// m107 (v3): drugi kandydat - koncowka z samych cyfr po literze (ROT_town12, town_EW4) - "^nr-cyfry" (przedrostek do litery wlacznie); wygrywa krotszy kod.</summary>
        private sealed class IdTable
        {
            private readonly Dictionary<string, int> _ix = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly List<string> _list = new List<string>();
            private readonly bool _v3;
            internal IdTable() { }
            internal IdTable(bool v3) { _v3 = v3; }
            internal int Count { get { return _list.Count; } }
            internal string Enc(string id)
            {
                id = Safe(id);
                if (id.Length < 6 || id[0] == '^') return id;
                string best = null, bestPre = null;
                int p = id.LastIndexOf('_');
                if (p >= 3 && p < id.Length - 1)
                {
                    string pre = id.Substring(0, p), suf = id.Substring(p + 1);
                    if (pre.IndexOfAny(BadPre) < 0 && suf.IndexOfAny(BadPre) < 0)
                    {
                        int k;
                        bool known = _ix.TryGetValue(pre, out k);
                        long num;
                        bool digits = suf.Length >= 5 && suf.Length <= 18 && suf[0] != '0' && AllDigits(suf) && long.TryParse(suf, NumberStyles.None, Inv, out num);
                        string code = "^" + B36(known ? k : _list.Count) + (digits ? ":" + B36L(long.Parse(suf, NumberStyles.None, Inv)) : "_" + suf);
                        if (code.Length < id.Length) { best = code; bestPre = pre; }
                    }
                }
                if (_v3)
                {
                    int q = id.Length;
                    while (q > 0 && id[q - 1] >= '0' && id[q - 1] <= '9') q--;
                    char c = q > 0 ? id[q - 1] : '\0';
                    if (q < id.Length && q >= 3 && ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
                    {
                        string pre = id.Substring(0, q);
                        if (pre.IndexOfAny(BadPre) < 0)
                        {
                            int k;
                            bool known = _ix.TryGetValue(pre, out k);
                            string code = "^" + B36(known ? k : _list.Count) + "-" + id.Substring(q);
                            if (code.Length < id.Length && (best == null || code.Length < best.Length)) { best = code; bestPre = pre; }
                        }
                    }
                }
                if (best == null) return id;
                if (!_ix.ContainsKey(bestPre)) { _ix[bestPre] = _list.Count; _list.Add(bestPre); }
                return best;
            }
            internal string Text() { return string.Join(",", _list.ToArray()); }
        }
        private static readonly char[] BadPre = { ',', '^', '/', '.', ':' };

        private static bool AllDigits(string s) { foreach (char c in s) if (c < '0' || c > '9') return false; return true; }

        private static string B36(int v) { return B36L(v); }

        private static string B36L(long v)
        {
            const string D = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (v <= 0) return "0";
            var sb = new StringBuilder(8);
            while (v > 0) { sb.Insert(0, D[(int)(v % 36)]); v /= 36; }
            return sb.ToString();
        }

        private static long FromB36(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 12) return -1;
            long v = 0;
            foreach (char c in s)
            {
                int d = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'z' ? c - 'a' + 10 : -1;
                if (d < 0) return -1;
                v = v * 36 + d;
            }
            return v;
        }

        /// <summary>m103-drobne: id z zapisu nb2 - "^nr_koncowka" albo "^nr:liczba base36" z tablicy przedrostkow; reszta bez zmian.</summary>
        private static string Dec(string s, string[] pre)
        {
            if (string.IsNullOrEmpty(s) || s[0] != '^' || pre == null) return s ?? "";
            int u = s.IndexOfAny(DecSep, 1);
            if (u < 2) return s;
            long k = FromB36(s.Substring(1, u - 1));
            if (k < 0 || k >= pre.Length) return s;
            if (s[u] == '_') return pre[(int)k] + s.Substring(u);
            long num = FromB36(s.Substring(u + 1));
            return num > 0 ? pre[(int)k] + "_" + num.ToString(Inv) : s;
        }
        private static readonly char[] DecSep = { '_', ':' };

        /// <summary>m107: id z zapisu nb3 - jak nb2 (Dec) i "^nr-cyfry" -> przedrostek + cyfry (bez '_'). Osobna funkcja: odczyt nb2 bez zmian.</summary>
        private static string Dec3(string s, string[] pre)
        {
            if (string.IsNullOrEmpty(s) || s[0] != '^' || pre == null) return s ?? "";
            int u = s.IndexOfAny(DecSep3, 1);
            if (u < 2) return s;
            if (s[u] != '-') return Dec(s, pre);
            long k = FromB36(s.Substring(1, u - 1));
            if (k < 0 || k >= pre.Length || u >= s.Length - 1) return s;
            return pre[(int)k] + s.Substring(u + 1);
        }
        private static readonly char[] DecSep3 = { '_', ':', '-' };

        // ------------------------------------------------------------ eksport nb3

        // sklad ostatniego eksportu (dowolnego: linia doby listow, zapis gry) - linie "Listy goncze (m102): dzien" (K6) i "Tablica (m107): zapis gry"
        private static string _secVer = "";
        private static int _secBytes = -1, _secDay = -1, _secFlagN, _secNotN, _secBandN, _secTownN;
        private static int _nb3Bad, _nb3Ok;   // proby odczytu nb3 w sesji: niezgodne / zgodne

        private static string TownNo(string town, IdTable ids, Dictionary<string, int> tix, List<string> tl)
        {
            if (string.IsNullOrEmpty(town)) return "";
            int i;
            if (!tix.TryGetValue(town, out i)) { i = tl.Count; tix[town] = i; tl.Add(ids.Enc(town).Replace(",", ".")); }
            return B36(i);
        }

        /// <summary>m107 K6: zapis nb3 (d0 - doba zapisu) z proba odczytu. Zwraca null przy niezgodnosci albo wyjatku (diff - pierwsza roznica) - wtedy Export pisze nb2.
        /// Po udanej probie ustawia sklad (_sec*). Wpisy szkod starsze niz HarmDays pominiete jak w nb2.</summary>
        private static string ExportV3(int d0, out string diff)
        {
            diff = null;
            try
            {
                var ids = new IdTable(true);
                var tix = new Dictionary<string, int>(StringComparer.Ordinal);
                var tl = new List<string>();
                var f = new StringBuilder();
                int nf = 0;
                foreach (var fl in _flags)
                {
                    if (string.IsNullOrEmpty(fl.Id) || fl.Kind == "?") continue;
                    if (f.Length > 0) f.Append(';');
                    f.Append(fl.Kind).Append('~').Append(ids.Enc(fl.Id)).Append('~').Append((fl.Until - d0).ToString(Inv));
                    nf++;
                }
                var w = new StringBuilder(64 * _notices.Count + 16);
                foreach (var n in _notices)
                {
                    if (w.Length > 0) w.Append(';');
                    w.Append(TownNo(n.Town, ids, tix, tl)).Append('~').Append(ids.Enc(n.Band)).Append('~').Append(n.Men.ToString(Inv)).Append('~').Append(n.Sum.ToString(Inv))
                     .Append('~').Append((n.Due - d0).ToString(Inv)).Append('~').Append(n.Taken ? "T" : "").Append(n.Lord ? "L" : "").Append('~');
                    if (n.Kind != 0) w.Append(n.Kind.ToString(Inv));
                    w.Append('~').Append(ids.Enc(n.Owner));
                    bool g = !string.IsNullOrEmpty(n.Giver), v = !string.IsNullOrEmpty(n.Village);
                    if (g || v) w.Append('~').Append(g ? ids.Enc(n.Giver) : "").Append('~').Append(v ? ids.Enc(n.Village) : "");
                }
                var h = new StringBuilder(24 * _harm.Count + 16);
                int nb = 0, ne = 0;
                foreach (var kv in _harm)
                {
                    bool live = false;
                    foreach (var e in kv.Value) if (d0 - e.Day <= HarmDays) { live = true; break; }
                    if (!live) continue;   // banda bez zywych wpisow - bez klucza (jak nb2)
                    if (h.Length > 0) h.Append(';');
                    h.Append(ids.Enc(kv.Key)).Append('~');
                    string last = null;
                    int inBand = 0;
                    foreach (var e in kv.Value)
                    {
                        if (d0 - e.Day > HarmDays) continue;
                        if (inBand > 0) h.Append('/');
                        h.Append(B36L((long)(d0 - e.Day) * 2 + e.Kind));
                        string town = HarmTown(e) ?? "", vil = e.Vil ?? "";
                        if (vil.Length > 0)
                        {
                            string tw = VilTown(vil) == town ? "" : TownNo(town, ids, tix, tl);
                            h.Append(',').Append(tw).Append(',').Append(ids.Enc(vil).Replace(",", "."));
                        }
                        else
                        {
                            if (town != last) h.Append(',').Append(TownNo(town, ids, tix, tl));
                            last = town;
                        }
                        inBand++;
                    }
                    nb++; ne += inBand;
                }
                var sb = new StringBuilder(96 + 8 * tl.Count + f.Length + w.Length + h.Length);
                sb.Append(Ver3).Append("|D=").Append(d0.ToString(Inv)).Append("|P=").Append(ids.Text()).Append("|T=").Append(string.Join(",", tl.ToArray()));
                int head = sb.Length;
                sb.Append("|F=").Append(f);
                sb.Append("|W=").Append(w);
                sb.Append("|H=").Append(h);
                string s = sb.ToString();
                diff = Probe3(s, d0);
                if (diff != null) { _nb3Bad++; return null; }
                _nb3Ok++;
                _secHead = head; _secF = f.Length + 3; _secW = w.Length + 3; _secH = h.Length + 3; _secHarmN = ne;
                _secVer = Ver3; _secBytes = s.Length; _secDay = d0; _secFlagN = nf; _secNotN = _notices.Count; _secBandN = nb; _secTownN = tl.Count;
                return s;
            }
            catch (Exception e) { _nb3Bad++; diff = "wyjatek eksportu " + e.GetType().Name + ": " + e.Message; return null; }
        }

        // ------------------------------------------------------------ odczyt nb2 / nb3 (wspolny)

        /// <summary>Odczyt zapisu nb2 (v3 false) albo nb3 do podanych kontenerow (Import2/Import3 - statyczne; Probe3 - osobne). Najpierw D, P (i T), potem flagi, listy, szkody.
        /// rethrow: wyjatek czesci przerywa odczyt (proba); inaczej licznik potkniec i dalej (odczyt zapisu - jak dotad WatchImport2). False - brak doby D.</summary>
        private static bool Parse(string[] parts, bool v3, List<Flag> flags, List<Notice> notices, Dictionary<string, List<Harm>> harm, bool rethrow, out int d0, out int npre, out int ntown)
        {
            d0 = 0; npre = 0; ntown = 0;
            bool hasD = false;
            string[] pre = new string[0], towns = new string[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p.StartsWith("D=", StringComparison.Ordinal)) hasD = int.TryParse(p.Substring(2), NumberStyles.Integer, Inv, out d0);
                else if (p.StartsWith("P=", StringComparison.Ordinal)) pre = p.Length > 2 ? p.Substring(2).Split(',') : new string[0];
                else if (v3 && p.StartsWith("T=", StringComparison.Ordinal)) towns = p.Length > 2 ? p.Substring(2).Split(',') : new string[0];
            }
            npre = pre.Length;
            if (!hasD) return false;
            for (int i = 0; i < towns.Length; i++) towns[i] = Dec3(towns[i], pre);
            ntown = towns.Length;
            for (int i = 1; i < parts.Length; i++)
            {
                var p = parts[i];
                try
                {
                    if (p.StartsWith("F=", StringComparison.Ordinal))
                    {
                        if (p.Length <= 2) continue;
                        foreach (var r in p.Substring(2).Split(';'))
                        {
                            var x = r.Split('~');
                            int off;
                            if (x.Length < 3 || !int.TryParse(x[2], NumberStyles.Integer, Inv, out off)) continue;
                            flags.Add(new Flag { Kind = x[0], Id = v3 ? Dec3(x[1], pre) : Dec(x[1], pre), Until = d0 + off });
                        }
                    }
                    else if (p.StartsWith("W=", StringComparison.Ordinal))
                    {
                        notices.Clear();
                        if (p.Length <= 2) continue;
                        foreach (var r in p.Substring(2).Split(';'))
                        {
                            var x = r.Split('~');
                            if (x.Length < 8) continue;
                            int men, sum, due, kind;
                            if (!int.TryParse(x[2], NumberStyles.Integer, Inv, out men) || !int.TryParse(x[3], NumberStyles.Integer, Inv, out sum)
                                || !int.TryParse(x[4], NumberStyles.Integer, Inv, out due)) continue;
                            if (v3 && x[6].Length == 0) kind = 0;
                            else if (!int.TryParse(x[6], NumberStyles.Integer, Inv, out kind)) continue;
                            notices.Add(new Notice
                            {
                                Town = v3 ? TownOf(x[0], towns) : Dec(x[0], pre), Band = v3 ? Dec3(x[1], pre) : Dec(x[1], pre), Name = "", Men = men, Sum = sum, Due = d0 + due,
                                Posted = 0, Days = 0, BonusPct = 0, S = 0f, Taken = x[5].IndexOf('T') >= 0, Lord = x[5].IndexOf('L') >= 0, Kind = kind,
                                Owner = v3 ? Dec3(x[7], pre) : Dec(x[7], pre),
                                Giver = x.Length > 8 ? (v3 ? Dec3(x[8], pre) : Dec(x[8], pre)) : "", Village = x.Length > 9 ? (v3 ? Dec3(x[9], pre) : Dec(x[9], pre)) : ""
                            });
                        }
                    }
                    else if (p.StartsWith("H=", StringComparison.Ordinal))
                    {
                        harm.Clear();
                        if (p.Length <= 2) continue;
                        foreach (var r in p.Substring(2).Split(';'))
                        {
                            int k = r.IndexOf('~');
                            if (k <= 0) continue;
                            var l = new List<Harm>();
                            string last = "";
                            foreach (var e in r.Substring(k + 1).Split('/'))
                            {
                                var y = e.Split(',');
                                int off, kind, ti;
                                if (v3)
                                {
                                    long code = FromB36(y[0]);
                                    if (code < 0 || code > int.MaxValue) continue;
                                    off = (int)(code / 2); kind = (int)(code % 2); ti = 1;
                                }
                                else
                                {
                                    if (y.Length < 2 || !int.TryParse(y[0], NumberStyles.Integer, Inv, out off) || !int.TryParse(y[1], NumberStyles.Integer, Inv, out kind)) continue;
                                    ti = 2;
                                }
                                string tw = y.Length > ti ? (v3 ? TownOf(y[ti], towns) : Dec(y[ti], pre)) : "", vil = y.Length > ti + 1 ? (v3 ? Dec3(y[ti + 1], pre) : Dec(y[ti + 1], pre)) : "";
                                string town;
                                if (vil.Length > 0) town = tw.Length > 0 ? tw : null;   // null - miasto z wsi (HarmTown, leniwie - odczyt nie zalezy od obiektow gry)
                                else { town = tw.Length > 0 ? tw : last; last = town; }
                                l.Add(new Harm { Day = d0 - off, Kind = (byte)kind, Town = town, Vil = vil });
                            }
                            if (l.Count > 0) harm[v3 ? Dec3(r.Substring(0, k), pre) : Dec(r.Substring(0, k), pre)] = l;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (rethrow) throw;
                    WStumble(v3 ? "Parse3(" + (p.Length > 2 ? p.Substring(0, 2) : p) + ")" : "WatchImport2(" + (p.Length > 2 ? p.Substring(0, 2) : p) + ")", e);
                }
            }
            return true;
        }

        /// <summary>nb3: pole miasta - numer base36 w T; puste - ""; numer spoza T - pole bez zmian (proba to wykryje).</summary>
        private static string TownOf(string f, string[] towns)
        {
            if (string.IsNullOrEmpty(f)) return "";
            long i = FromB36(f);
            return i >= 0 && i < towns.Length ? towns[(int)i] : f;
        }

        /// <summary>m107 K6: proba odczytu napisu nb3 (d0 - doba zapisu) - Parse do osobnych kontenerow i porownanie pole po polu z zywym stanem filtrowanym jak eksport
        /// (flagi bez id i "?" pominiete; id po Safe; szkody <= HarmDays, miasto szkody rozwiazane HarmTown, ',' -> '.' jak w nb2). Null - zgodne; inaczej pierwsza roznica.</summary>
        private static string Probe3(string s, int d0)
        {
            try
            {
                var fl = new List<Flag>();
                var no = new List<Notice>();
                var ha = new Dictionary<string, List<Harm>>(StringComparer.Ordinal);
                int pd0, np, nt;
                if (!Parse(s.Split('|'), true, fl, no, ha, true, out pd0, out np, out nt)) return "brak doby D";
                if (pd0 != d0) return "doba D " + pd0 + " zamiast " + d0;
                int i = 0;
                foreach (var f in _flags)
                {
                    if (string.IsNullOrEmpty(f.Id) || f.Kind == "?") continue;
                    if (i >= fl.Count) return "flagi: odczytano " + fl.Count;
                    var g = fl[i++];
                    if (g.Kind != f.Kind || g.Id != Safe(f.Id) || g.Until != f.Until) return "flaga " + i + " (" + Safe(f.Id) + " -> " + g.Id + ")";
                }
                if (i != fl.Count) return "flagi: odczytano " + fl.Count + " zamiast " + i;
                if (no.Count != _notices.Count) return "listy: odczytano " + no.Count + " zamiast " + _notices.Count;
                for (int j = 0; j < no.Count; j++)
                {
                    var a = _notices[j]; var b = no[j];
                    if (b.Town != Safe(a.Town) || b.Band != Safe(a.Band) || b.Men != a.Men || b.Sum != a.Sum || b.Due != a.Due || b.Taken != a.Taken || b.Lord != a.Lord
                        || b.Kind != a.Kind || b.Owner != Safe(a.Owner) || b.Giver != Safe(a.Giver) || b.Village != Safe(a.Village))
                        return "list " + (j + 1) + " (" + Safe(a.Town) + " " + Safe(a.Band) + " -> " + b.Town + " " + b.Band + ")";
                }
                int nb = 0;
                foreach (var kv in _harm)
                {
                    bool live = false;
                    foreach (var e in kv.Value) if (d0 - e.Day <= HarmDays) { live = true; break; }
                    if (!live) continue;
                    nb++;
                    List<Harm> l;
                    if (!ha.TryGetValue(Safe(kv.Key), out l)) return "szkody: brak bandy " + Safe(kv.Key);
                    int j = 0;
                    foreach (var e in kv.Value)
                    {
                        if (d0 - e.Day > HarmDays) continue;
                        if (j >= l.Count) return "szkody " + Safe(kv.Key) + ": odczytano " + l.Count;
                        var g = l[j++];
                        if (g.Day != e.Day || g.Kind != e.Kind || NormT(HarmTown(g)) != NormT(HarmTown(e)) || NormT(g.Vil) != NormT(e.Vil))
                            return "szkoda " + Safe(kv.Key) + " #" + j + " (doba " + e.Day + "/" + g.Day + ", rodzaj " + e.Kind + "/" + g.Kind + ", miasto " + NormT(HarmTown(e)) + "/" + NormT(HarmTown(g)) + ")";
                    }
                    if (j != l.Count) return "szkody " + Safe(kv.Key) + ": odczytano " + l.Count + " zamiast " + j;
                }
                if (nb != ha.Count) return "szkody: band odczytano " + ha.Count + " zamiast " + nb;
                return null;
            }
            catch (Exception e) { return "wyjatek odczytu " + e.GetType().Name + ": " + e.Message; }
        }

        private static string NormT(string s) { return Safe(s ?? "").Replace(",", "."); }

        /// <summary>m107: odczyt zapisu nb3 (Import) - do stanu tablicy; linia jak dla nb2 z liczba miast.</summary>
        private static void Import3(string[] parts, int bytes)
        {
            int d0, np, nt;
            if (!Parse(parts, true, _flags, _notices, _harm, false, out d0, out np, out nt)) { Log.Info(P + "zapis arm_board nb3 bez doby D - pominiety."); return; }
            int hn = 0;
            foreach (var kv in _harm) hn += kv.Value.Count;
            Log.Info(P + "wczytano zapis arm_board nb3 (" + bytes + " B, doba zapisu " + d0 + ", przedrostkow " + np + ", miast " + nt + ") - flagi " + _flags.Count + ", listy " + _notices.Count
                     + ", szkody band " + _harm.Count + " (wpisow " + hn + ").");
        }

        /// <summary>Sklad ostatniego eksportu (K6): linia doby listow i linia zapisu gry.</summary>
        private static string SecText()
        {
            return "naglowek z przedrostkami" + (_secVer == Ver3 ? " i miastami (" + _secTownN + ")" : "") + " " + _secHead + " B, flagi " + _secF + " B (" + _secFlagN + "), listy " + _secW + " B (" + _secNotN
                   + "), szkody " + _secH + " B (band " + _secBandN + ", wpisow " + _secHarmN + ")";
        }
    }
}
