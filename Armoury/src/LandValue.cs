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
    ///  - 4 sloty por roku: absSeason = doba / doby pory roku (Jeff: 91 = 13 tygodni x 7), slot = absSeason % 4; slot z innej pory roku (sprzed roku) czyszczony
    ///    przed pierwszym uzyciem. YearMean - srednia z por roku (kazda pora z ta sama waga, gdy sa wszystkie cztery; inaczej srednia dob); mniej niz 28 dob -
    ///    pierscien 28 dob sesji, potem wartosc dnia (oferta mowi "measured over N days").
    ///  - Zapis arm_landval (SaveText): "lv1|91|stId:abs,vt,lr,n;abs,...|..." (ok. 700 osad, ok. 84 tys. znakow - SaveText dzieli na kawalki). Inna dlugosc pory roku
    ///    w zapisie - ksiega od nowa (linia). Nieznane id osad - pominiete przy pierwszej dobie. Pierscien 28 dob - tylko sesja.
    /// Tylko pomiar (nic w grze sie nie zmienia). Linie logu w LandSale ("Ziemia (m113): ..."). Potkniecia liczone, nic nie rzuca.
    /// </summary>
    internal static class LandValue
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string SaveVer = "lv1";
        internal const int VT = 0, LR = 1;

        private sealed class E
        {
            public readonly int[] Abs = { -1, -1, -1, -1 };
            public readonly long[] Vt = new long[4], Lr = new long[4];
            public readonly int[] N = new int[4];
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
                int day = (int)CampaignTime.Now.ToDays;
                int abs = day / dis, slot = abs % 4;
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
                LastNote = "osad " + n + ", dob w slotach do " + maxDays + ", pora roku " + (abs % 4) + " (abs " + abs + ", " + dis + " dob)";
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static int DaysOf(E e, int curAbs)
        {
            int d = 0;
            for (int i = 0; i < 4; i++) if (e.Abs[i] >= 0 && e.Abs[i] > curAbs - 4 && e.Abs[i] <= curAbs) d += e.N[i];
            return d;
        }

        private static int CurAbs() { int dis = _dis > 0 ? _dis : DaysInSeason(); return (int)CampaignTime.Now.ToDays / Math.Max(1, dis); }

        /// <summary>Srednia dzienna czesci part (VT / LR) osady przez ostatni rok: cztery pory roku z ta sama waga, gdy sa wszystkie, inaczej srednia zmierzonych dob.
        /// Mniej niz 28 dob - pierscien 28 dob sesji (days = jego doby), potem wartosc ostatniej doby (days = 1); brak - 0 (days = 0). Nie rzuca.</summary>
        internal static double YearMean(Settlement st, int part, out int days)
        {
            days = 0;
            try
            {
                E e;
                if (st == null || st.StringId == null || !_book.TryGetValue(st.StringId, out e)) return 0;
                int cur = CurAbs();
                int full = 0, tot = 0; double sumMeans = 0, sum = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (e.Abs[i] < 0 || e.Abs[i] <= cur - 4 || e.Abs[i] > cur || e.N[i] <= 0) continue;
                    long v = part == VT ? e.Vt[i] : e.Lr[i];
                    full++; tot += e.N[i]; sum += v; sumMeans += (double)v / e.N[i];
                }
                if (tot >= 28)
                {
                    days = tot;
                    return full == 4 ? sumMeans / 4.0 : sum / tot;
                }
                if (e.RFilled > 0) { days = e.RFilled; return Avg28Of(e, part); }
                if (e.HasLast) { days = 1; return part == VT ? e.LastVt : e.LastLr; }
                return 0;
            }
            catch (Exception ex) { Stumble("YearMean", ex); days = 0; return 0; }
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
            try
            {
                if (_book.Count == 0) return "";
                var sb = new StringBuilder(128 * _book.Count + 16);
                sb.Append(SaveVer).Append('|').Append((_dis > 0 ? _dis : DaysInSeason()).ToString(Inv));
                foreach (var kv in _book)
                {
                    var e = kv.Value;
                    bool any = false;
                    for (int i = 0; i < 4; i++) if (e.Abs[i] >= 0 && e.N[i] > 0) { any = true; break; }
                    if (!any) continue;
                    sb.Append('|').Append(kv.Key.Replace("|", "/").Replace(":", "/").Replace(";", "/")).Append(':');
                    bool first = true;
                    for (int i = 0; i < 4; i++)
                    {
                        if (e.Abs[i] < 0 || e.N[i] <= 0) continue;
                        if (!first) sb.Append(';');
                        first = false;
                        sb.Append(e.Abs[i].ToString(Inv)).Append(',').Append(e.Vt[i].ToString(Inv)).Append(',').Append(e.Lr[i].ToString(Inv)).Append(',').Append(e.N[i].ToString(Inv));
                    }
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _book.Clear(); _pruned = false;
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                var f = data.Split('|');
                int dis;
                if (f.Length < 2 || f[0] != SaveVer || !int.TryParse(f[1], NumberStyles.Integer, Inv, out dis)) { Log.Info("Ziemia (m113): zapis arm_landval nieznany (" + data.Length + " znakow) - ksiega wartosci ziemi od zera."); return; }
                _dis = dis;
                int ok = 0, bad = 0;
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
                        int slot = abs % 4;
                        if (e.Abs[slot] > abs) continue;
                        e.Abs[slot] = abs; e.Vt[slot] = vt; e.Lr[slot] = lr; e.N[slot] = n;
                    }
                    _book[f[i].Substring(0, c)] = e; ok++;
                }
                Log.Info("Ziemia (m113): zapis arm_landval - osad " + ok + (bad > 0 ? ", zle wpisy " + bad : "") + ", pora roku " + dis + " dob.");
            }
            catch (Exception e) { _book.Clear(); Stumble("Import", e); }
        }
    }
}
