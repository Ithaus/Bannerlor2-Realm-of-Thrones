using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m113-zakup (24.10, Jeff: "kosztuje tyle, ile kosztuje i zarabia ... nie za grosze") - KSIEGA WARTOSCI ZIEMI: ile osada naprawde daje swojemu panu przez caly rok,
    /// nie jednej doby. Podstawa ceny ziemi (LandSale, Z4): dochod ziemi zmienia sie z porami roku (zniwa, zima, wojna), wiec cena z jednej doby albo z 28 dob bylaby
    /// za wysoka po zniwach i za niska zima.
    ///  - Co dobe, ZARAZ PO ClanIncomeBook.Daily: ostatnia doba pierscieni D stalego (ClanIncomeBook.LastDay) dla kazdej wsi gry i zamku: vt - podatek wsi BK (wedlug
    ///    posiadacza tytulu), lr - renta wsi / zawor, cla i podatek, zawor zamku (to, co osada placi wlasciciela).
    ///  - 5 slotow por roku (recenzja: biezaca + 4 poprzednie): absSeason = (doba - 1) / doby pory roku (Jeff: 91 = 13 tygodni x 7; LastDay to wczorajsza doba),
    ///    slot = absSeason % 5; slot sprzed 5 por czyszczony przed pierwszym uzyciem. YearMean - cztery PELNE pory przed biezaca z ta sama waga; inaczej srednia dob
    ///    w slotach; mniej - pierscien 28 dob sesji, potem wartosc dnia. Cena Lordship i zamku wymaga co najmniej 28 dob (LandSale).
    ///  - Zapis arm_landval (SaveText): "lv1|91|stId:abs,vt,lr,n;abs,...|..." (ok. 700 osad, ok. 84 tys. znakow - SaveText dzieli na kawalki). Inna dlugosc pory roku
    ///    w zapisie - ksiega od nowa (linia). Nieznane id osad - pominiete przy pierwszej dobie. Pierscien 28 dob - tylko sesja.
    /// Tylko pomiar (nic w grze sie nie zmienia). Linie logu w LandSale ("Ziemia (m113): ..."). Potkniecia liczone, nic nie rzuca.
    /// </summary>
    internal static class LandValue
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string SaveVer = "lv1";
        internal const int VT = 0, LR = 1;

        // recenzja m113: 5 slotow (pora biezaca + 4 pelne poprzednie) - pierwsza doba nowej pory nie kasuje tej samej pory sprzed roku, a srednia roku bierze
        // cztery PELNE pory (bez biezacej, ktora moze miec 1 dobe). Zapis dalej "lv1" (wpisy niosa abs - starsza DLL ulozy je abs % 4 i wezmie najnowsze).
        private const int S = 5;
        private sealed class E
        {
            public readonly int[] Abs = { -1, -1, -1, -1, -1 };
            public readonly long[] Vt = new long[S], Lr = new long[S];
            public readonly int[] N = new int[S];
            // sesja: pierscien 28 dob i ostatnia doba
            public readonly long[] RVt = new long[28], RLr = new long[28]; public int RHead, RFilled;
            public long LastVt, LastLr; public bool HasLast;
        }

        private static readonly Dictionary<string, E> _book = new Dictionary<string, E>(StringComparer.Ordinal);
        private static int _dis = -1;            // doby pory roku ksiegi
        private static int _days;                // ile dob zmierzono od poczatku ksiegi (najwiecej w jednej osadzie)
        private static int _st;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);
        private static bool _pruned;
        internal static int LastN, LastDropped; internal static string LastNote = "-";

        internal static void Reset()
        {
            _book.Clear(); _dis = -1; _days = 0; _st = 0; _err.Clear(); _pruned = false; LastN = 0; LastDropped = 0; LastNote = "-";
        }

        private static void Stumble(string where, Exception e)
        {
            _st++;
            try { if (_err.Add(where)) Log.Error("LandValue." + where, e); } catch { }
        }

        internal static int Stumbles { get { return _st; } }
        internal static int DaysMeasured { get { return _days; } }

        /// <summary>Doby pory roku z ZYWEGO modelu czasu (Jeff: 7 x 13 = 91; rok 364).</summary>
        internal static int DaysInSeason()
        {
            try { var m = Campaign.Current.Models.CampaignTimeModel; return Math.Max(7, m.DaysInWeek * m.WeeksInSeason); }
            catch { return 91; }
        }

        /// <summary>Raz na dobe, ZARAZ PO ClanIncomeBook.Daily: ostatnia doba pierscieni D stalego wsi i zamkow do slotow por roku. Nie rzuca.</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null || !ClanIncomeBook.StableDOn) { LastNote = "D staly wylaczony - bez pomiaru"; return; }
                int dis = DaysInSeason();
                if (_dis != dis)
                {
                    if (_dis > 0) Log.Info("Ziemia (m113): ksiega wartosci ziemi od nowa - dlugosc pory roku " + _dis + " -> " + dis + " dob.");
                    if (_dis > 0) _book.Clear();
                    _dis = dis; _days = 0;
                }
                if (!_pruned)
                {
                    _pruned = true;
                    var drop = new List<string>();
                    foreach (var k in _book.Keys) { var st = Settlement.Find(k); if (st == null || !(st.IsVillage || st.IsCastle)) drop.Add(k); }
                    foreach (var k in drop) _book.Remove(k);
                    LastDropped = drop.Count;
                    if (drop.Count > 0) Log.Info("Ziemia (m113): ksiega wartosci ziemi - pominiete nieznane id osad z zapisu: " + drop.Count + ".");
                }
                int day = MeasDay();   // recenzja m113: LastDay to wczorajsza doba - jej pora roku, nie dzisiejsza
                int abs = day / dis, slot = abs % S;
                int n = 0, maxDays = 0;
                foreach (var st in Settlement.All)
                {
                    try
                    {
                        if (st == null || st.StringId == null || !(st.IsVillage || st.IsCastle)) continue;
                        long vt, lr;
                        if (!ClanIncomeBook.LastDay(st, out vt, out lr)) continue;
                        E e;
                        if (!_book.TryGetValue(st.StringId, out e)) { e = new E(); _book[st.StringId] = e; }
                        if (e.Abs[slot] != abs) { e.Abs[slot] = abs; e.Vt[slot] = 0; e.Lr[slot] = 0; e.N[slot] = 0; }   // stary slot (sprzed roku) - od nowa
                        e.Vt[slot] += Math.Max(0, vt); e.Lr[slot] += Math.Max(0, lr); e.N[slot]++;
                        e.RVt[e.RHead] = Math.Max(0, vt); e.RLr[e.RHead] = Math.Max(0, lr); e.RHead = (e.RHead + 1) % 28; if (e.RFilled < 28) e.RFilled++;
                        e.LastVt = Math.Max(0, vt); e.LastLr = Math.Max(0, lr); e.HasLast = true;
                        int d = DaysOf(e, abs);
                        if (d > maxDays) maxDays = d;
                        n++;
                    }
                    catch (Exception ex) { Stumble("Daily(osada)", ex); }
                }
                _days = maxDays; LastN = n;
                LastNote = "osad " + n + ", dob w slotach do " + maxDays + ", pora roku " + (abs % 4) + " (abs " + abs + ", slot " + slot + ", " + dis + " dob)";
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static int DaysOf(E e, int curAbs)
        {
            int d = 0;
            for (int i = 0; i < S; i++) if (e.Abs[i] >= 0 && e.Abs[i] > curAbs - S && e.Abs[i] <= curAbs) d += e.N[i];
            return d;
        }

        /// <summary>Doba pomiaru: LastDay niesie wczorajsza dobe (Daily na poczatku nowej doby).</summary>
        private static int MeasDay() { return Math.Max(0, (int)CampaignTime.Now.ToDays - 1); }
        private static int CurAbs() { int dis = _dis > 0 ? _dis : DaysInSeason(); return MeasDay() / Math.Max(1, dis); }

        /// <summary>Srednia dzienna czesci part (VT / LR) osady przez ostatni rok: cztery PELNE pory roku przed biezaca (kazda co najmniej pol pory dob) z ta sama waga;
        /// inaczej srednia wszystkich zmierzonych dob w slotach (gdy co najmniej 28 dob albo nie mniej niz w pierscieniu sesji); potem pierscien 28 dob sesji
        /// (days = jego doby), potem wartosc ostatniej doby (days = 1); brak - 0 (days = 0). Cena ziemi wymaga days >= 28 (LandSale). Nie rzuca.</summary>
        internal static double YearMean(Settlement st, int part, out int days)
        {
            days = 0;
            try
            {
                E e;
                if (st == null || st.StringId == null || !_book.TryGetValue(st.StringId, out e)) return 0;
                int cur = CurAbs();
                int dis = _dis > 0 ? _dis : DaysInSeason();
                // cztery pelne pory przed biezaca
                int full = 0, fullDays = 0; double sumMeans = 0;
                for (int a = cur - 4; a <= cur - 1; a++)
                {
                    if (a < 0) break;
                    int i = a % S;
                    if (e.Abs[i] != a || e.N[i] < Math.Max(1, dis / 2)) break;
                    long v = part == VT ? e.Vt[i] : e.Lr[i];
                    full++; fullDays += e.N[i]; sumMeans += (double)v / e.N[i];
                }
                if (full == 4) { days = fullDays; return sumMeans / 4.0; }
                int tot = 0; double sum = 0;
                for (int i = 0; i < S; i++)
                {
                    if (e.Abs[i] < 0 || e.Abs[i] <= cur - S || e.Abs[i] > cur || e.N[i] <= 0) continue;
                    tot += e.N[i]; sum += part == VT ? e.Vt[i] : e.Lr[i];
                }
                if (tot >= 28 || (tot > 0 && tot >= e.RFilled)) { days = tot; return sum / tot; }
                if (e.RFilled > 0) { days = e.RFilled; return Avg28Of(e, part); }
                if (e.HasLast) { days = 1; return part == VT ? e.LastVt : e.LastLr; }
                return 0;
            }
            catch (Exception ex) { Stumble("YearMean", ex); days = 0; return 0; }
        }

        /// <summary>Srednia dzienna biezacej pory roku z zapisanego slotu (n - jej doby; 0, gdy slot nie jest biezacej pory). Nie rzuca.</summary>
        internal static double SeasonMean(Settlement st, int part, out int n)
        {
            n = 0;
            try
            {
                E e;
                if (st == null || st.StringId == null || !_book.TryGetValue(st.StringId, out e)) return 0;
                int cur = CurAbs(), i = cur % S;
                if (e.Abs[i] != cur || e.N[i] <= 0) return 0;
                n = e.N[i];
                return (double)(part == VT ? e.Vt[i] : e.Lr[i]) / e.N[i];
            }
            catch { n = 0; return 0; }
        }

        /// <summary>Recenzja m113: poprawka pory roku wioski pobocznej (cena z dzisiejszej naleznosci -> srednia roku): YearMean VT okregu / srednia ostatnich dob VT.
        /// "Ostatnie doby" - pierscien 28 dob sesji tylko pelny (28 dob; po wczytaniu jest pusty), inaczej biezaca pora z zapisanego slotu (co najmniej 28 dob);
        /// srednia roku tylko z co najmniej 28 dob i przy co najmniej pelnej porze w slotach. Inaczej 1. clamp 0.5-2.0. note - po polsku do logu. Nie rzuca.</summary>
        internal static double SeasonFix(Settlement st, out string note)
        {
            note = "1 (brak pomiaru)";
            try
            {
                if (st == null || SlotDays(st) < DaysInSeason()) { note = "1 (mniej niz pora roku w ksiedze)"; return 1; }
                int d1; double ym = YearMean(st, VT, out d1);
                if (d1 < 28 || !(ym > 0)) { note = "1 (srednia roku z " + d1 + " dob)"; return 1; }
                int d2; double rec = Avg28(st, VT, out d2); string src = "28 dob sesji";
                if (d2 < 28 || !(rec > 0)) { rec = SeasonMean(st, VT, out d2); src = "biezaca pora " + d2 + " dob"; if (d2 < 28 || !(rec > 0)) { note = "1 (za malo dob ostatnich: sesja i pora " + d2 + ")"; return 1; } }
                double f = Math.Max(0.5, Math.Min(2.0, ym / rec));
                note = f.ToString("0.###", Inv) + " (rok " + ym.ToString("0.##", Inv) + " z " + d1 + " dob / " + rec.ToString("0.##", Inv) + " z " + src + ")";
                return f;
            }
            catch (Exception ex) { Stumble("SeasonFix", ex); note = "1 (blad)"; return 1; }
        }

        private static double Avg28Of(E e, int part)
        {
            if (e.RFilled <= 0) return 0;
            long s = 0; var a = part == VT ? e.RVt : e.RLr;
            for (int i = 0; i < 28; i++) s += a[i];
            return (double)s / e.RFilled;
        }

        /// <summary>Srednia 28 dob sesji czesci part osady (0 - brak). Nie rzuca.</summary>
        internal static double Avg28(Settlement st, int part, out int days)
        {
            days = 0;
            try
            {
                E e;
                if (st == null || st.StringId == null || !_book.TryGetValue(st.StringId, out e) || e.RFilled <= 0) return 0;
                days = e.RFilled;
                return Avg28Of(e, part);
            }
            catch { days = 0; return 0; }
        }

        /// <summary>Doby w slotach por roku tej osady (ostatni rok). Nie rzuca.</summary>
        internal static int SlotDays(Settlement st)
        {
            try { E e; return st != null && st.StringId != null && _book.TryGetValue(st.StringId, out e) ? DaysOf(e, CurAbs()) : 0; }
            catch { return 0; }
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            try { return _book.Count == 0 ? "" : Serialize(_book, _dis > 0 ? _dis : DaysInSeason()); }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        private static string Serialize(Dictionary<string, E> book, int dis)
        {
            var sb = new StringBuilder(128 * book.Count + 16);
            sb.Append(SaveVer).Append('|').Append(dis.ToString(Inv));
            foreach (var kv in book)
            {
                var e = kv.Value;
                bool any = false;
                for (int i = 0; i < S; i++) if (e.Abs[i] >= 0 && e.N[i] > 0) { any = true; break; }
                if (!any) continue;
                sb.Append('|').Append(kv.Key.Replace("|", "/").Replace(":", "/").Replace(";", "/")).Append(':');
                bool first = true;
                for (int i = 0; i < S; i++)
                {
                    if (e.Abs[i] < 0 || e.N[i] <= 0) continue;
                    if (!first) sb.Append(';');
                    first = false;
                    sb.Append(e.Abs[i].ToString(Inv)).Append(',').Append(e.Vt[i].ToString(Inv)).Append(',').Append(e.Lr[i].ToString(Inv)).Append(',').Append(e.N[i].ToString(Inv));
                }
            }
            return sb.ToString();
        }

        /// <summary>Odczyt napisu do book (bez stanu zywego). false - napis nieznany.</summary>
        private static bool Parse(string data, Dictionary<string, E> book, out int dis, out int ok, out int bad)
        {
            ok = 0; bad = 0;
            var f = data.Split('|');
            if (f.Length < 2 || f[0] != SaveVer || !int.TryParse(f[1], NumberStyles.Integer, Inv, out dis)) { dis = -1; return false; }
            for (int i = 2; i < f.Length; i++)
            {
                int c = f[i].IndexOf(':');
                if (c <= 0) { bad++; continue; }
                var e = new E();
                foreach (var sl in f[i].Substring(c + 1).Split(';'))
                {
                    var p = sl.Split(',');
                    int abs, n; long vt, lr;
                    if (p.Length < 4 || !int.TryParse(p[0], NumberStyles.Integer, Inv, out abs) || !long.TryParse(p[1], NumberStyles.Integer, Inv, out vt)
                        || !long.TryParse(p[2], NumberStyles.Integer, Inv, out lr) || !int.TryParse(p[3], NumberStyles.Integer, Inv, out n) || abs < 0 || n <= 0) { bad++; continue; }
                    int slot = abs % S;
                    if (e.Abs[slot] > abs) continue;
                    e.Abs[slot] = abs; e.Vt[slot] = vt; e.Lr[slot] = lr; e.N[slot] = n;
                }
                book[f[i].Substring(0, c)] = e; ok++;
            }
            return true;
        }

        /// <summary>Autotest (T8): napis -> ksiega na boku -> napis; zywa ksiega i pierscienie sesji nietkniete. Nie rzuca.</summary>
        internal static string RoundTrip(string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data)) return "";
                var b = new Dictionary<string, E>(StringComparer.Ordinal); int dis, ok, bad;
                if (!Parse(data, b, out dis, out ok, out bad)) return "?";
                return b.Count == 0 ? "" : Serialize(b, dis);
            }
            catch (Exception e) { Stumble("RoundTrip", e); return "?"; }
        }

        internal static void Import(string data)
        {
            _book.Clear(); _pruned = false;
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                int dis, ok, bad;
                if (!Parse(data, _book, out dis, out ok, out bad)) { _book.Clear(); Log.Info("Ziemia (m113): zapis arm_landval nieznany (" + data.Length + " znakow) - ksiega wartosci ziemi od zera."); return; }
                _dis = dis;
                Log.Info("Ziemia (m113): zapis arm_landval - osad " + ok + (bad > 0 ? ", zle wpisy " + bad : "") + ", pora roku " + dis + " dob.");
            }
            catch (Exception e) { _book.Clear(); Stumble("Import", e); }
        }
    }
}
