using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KS-R4 r4-kupcy H5 (noc 17/18.10, PLAN-KS-RUNDA4 rozdz. 3; projekt DS F2 "kupcy na wlasnym kapitale"): KREDYT KUPIECKI KS-H - rachunki miast celu wobec miast
    /// zrodla za jedzenie dowiezione na kredyt (klucz Ks Food Carts Consign). Woz wyjezdza bez zaplaty (fracht placi zrodlo), przy dostawie cel placi wartosc lotu
    /// u siebie (KsFoodCore.DestIntegral) z kasy ponad KsConsignFloorShare x zapas kupcow, reszta to RACHUNEK (zrodlo, cel, zostalo, doba). Codziennie (SupplyDemand.DailyTrade,
    /// przed wozami kupcow) kazdy cel splaca swoje rachunki od najstarszego z kasy ponad te sama podloge. Rachunek starszy niz KsConsignOverdueDays - cel bez nowych dostaw
    /// (KsFoodCore DstOverdue). Wojna miedzy zrodlem a celem - rachunek przepada ("kupcy stracili": znika roszczenie, zloto nie powstaje i nie ginie; licznik).
    /// Zmiana wlasciciela w jednym krolestwie (i pokoj) - rachunek zostaje. Najwyzej 8 wpisow na pare (zrodlo, cel): nowy przy pelnej liscie dopisywany do najnowszego.
    /// ZLOTO: kazda moneta cel -> zrodlo (ChangeGold z pomiarem obu stron); kontrola doby: otwarte wczoraj + nowe - splacone - przepadle = otwarte dzis (reszta 0).
    /// ZAPIS: klucz arm_daniny_kupcy "kk1|zrodlo~cel~zostalo~doba|..." (najwyzej 64 KB - wiecej: wpisy pary scalane w jeden, najstarsza doba; nic nie przepada); brak klucza
    /// (stary zapis) = brak rachunkow; starsza DLL klucza nie czyta.
    /// </summary>
    internal static class KsFoodCredit
    {
        private sealed class Bill { internal long Left; internal int Day; }
        private sealed class Acct { internal Settlement Src, Dst; internal readonly List<Bill> B = new List<Bill>(8); }

        private const int MaxEntries = 8, SaveCapChars = 64 * 1024;
        private const string Ver = "kk1";
        private static readonly Dictionary<string, Acct> _acc = new Dictionary<string, Acct>(StringComparer.Ordinal);
        private static string _pending;
        // liczniki doby (linia KS-H) i od wczytania
        internal static long DNew, DPaidNow, DBilled, DRepaid, DForfeit; internal static int DNewN, DForfeitN, DDeliv;
        internal static long GDstPaid, GSrcGot;            // zloto doby: cel zaplacil / zrodla dostaly (przy dostawie + splaty)
        internal static long ForfeitTotal; internal static int ForfeitTotalN;
        private static long _openPrev = -1;
        private static int _stum; private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Reset()
        {
            _acc.Clear(); _pending = null; _openPrev = -1; ForfeitTotal = 0; ForfeitTotalN = 0; _stum = 0; _err.Clear();
            ClearDay();
        }

        internal static void ClearDay()
        {
            DNew = DPaidNow = DBilled = DRepaid = DForfeit = 0; DNewN = DForfeitN = DDeliv = 0; GDstPaid = GSrcGot = 0;
        }

        internal static int TakeStumbles() { int n = _stum; _stum = 0; return n; }

        private static void Stumble(string where, Exception e)
        {
            _stum++;
            try { if (_err.Add(where)) Log.Error("KsFoodCredit." + where, e); } catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static string Key(Settlement s, Settlement d) { return s.StringId + "|" + d.StringId; }

        internal static bool Any { get { Resolve(); return _acc.Count > 0; } }

        /// <summary>Cena podlogi kasy celu (zaplata przy dostawie i splaty): KsConsignFloorShare x zapas kupcow (0..1).</summary>
        internal static double FloorShare { get { var s = Settings.Current; return s == null ? 0.5 : Math.Max(0.0, Math.Min(1.0, s.KsConsignFloorShare)); } }
        internal static int OverdueDays { get { var s = Settings.Current; return s == null ? 30 : Math.Max(1, Math.Min(364, s.KsConsignOverdueDays)); } }

        /// <summary>Kasa celu ponad podloge (zl) - z tego placi przy dostawie i splaca rachunki.</summary>
        private static long Free(Town t)
        {
            if (t == null) return 0;
            long res = 0; try { res = Seal31.TownReserve(t); } catch { res = 0; }
            return Math.Max(0L, t.Gold - (long)Math.Ceiling(FloorShare * Math.Max(0L, res)));
        }

        /// <summary>Przelew cel -> zrodlo z pomiarem obu stron; zwraca to, co zeszlo z kasy celu (zrodlo dostaje dokladnie tyle - kasa zrodla nie ma sufitu).</summary>
        private static long Transfer(Town dst, Town src, long x)
        {
            if (x <= 0 || dst == null || src == null) return 0;
            int ask = (int)Math.Min(int.MaxValue, Math.Min(x, Math.Max(0, dst.Gold)));
            if (ask <= 0) return 0;
            int d0 = dst.Gold; dst.ChangeGold(-ask); long got = d0 - dst.Gold;
            if (got <= 0) return 0;
            int s0 = src.Gold; src.ChangeGold((int)got); long rec = src.Gold - s0;
            GDstPaid += got; GSrcGot += rec;
            return got;
        }

        private static bool AtWar(Settlement a, Settlement b)
        {
            try { return a.MapFaction != null && b.MapFaction != null && FactionManager.IsAtWarAgainstFaction(a.MapFaction, b.MapFaction); } catch { return false; }
        }

        /// <summary>Dostawa wozu na kredyt (KsFoodCarts.Arrivals): cel placi z kasy ponad podloge najwyzej wartosc lotu u siebie, reszta = nowy rachunek. Zwraca zaplacone teraz.</summary>
        internal static long Deliver(Settlement src, Settlement dst, long bill, int today)
        {
            Resolve();
            if (bill <= 0 || src == null || dst == null || src.Town == null || dst.Town == null) return 0;
            DBilled += bill; DDeliv++;
            long now = Transfer(dst.Town, src.Town, Math.Min(bill, Free(dst.Town)));
            DPaidNow += now;
            long rest = bill - now;
            if (rest > 0) Open(src, dst, rest, today);
            return now;
        }

        private static void Open(Settlement src, Settlement dst, long x, int today)
        {
            Acct a; string k = Key(src, dst);
            if (!_acc.TryGetValue(k, out a)) { a = new Acct { Src = src, Dst = dst }; _acc[k] = a; }
            var last = a.B.Count > 0 ? a.B[a.B.Count - 1] : null;
            if (last != null && (last.Day == today || a.B.Count >= MaxEntries)) last.Left += x;      // ta sama doba albo pelna lista - do najnowszego
            else a.B.Add(new Bill { Left = x, Day = today });
            DNew += x; DNewN++;
        }

        /// <summary>Splata doby (SupplyDemand.DailyTrade, przed wozami): wojna - rachunek przepada; potem kazdy cel od najstarszego z kasy ponad podloge. Dziala zawsze,
        /// gdy sa rachunki (takze przy wylaczonym kluczu - dlugi trzeba splacac).</summary>
        internal static void PayDaily()
        {
            Resolve();
            if (_acc.Count == 0) return;
            int today = Today();
            try
            {
                // rachunki wedlug celu (stala kolejnosc: StringId celu, potem doba, potem StringId zrodla)
                var byDst = new SortedDictionary<string, List<KeyValuePair<Acct, Bill>>>(StringComparer.Ordinal);
                var gone = new List<string>();
                foreach (var kv in _acc)
                {
                    var a = kv.Value;
                    try
                    {
                        if (a.Src == null || a.Dst == null || a.Src.Town == null || a.Dst.Town == null || AtWar(a.Src, a.Dst))
                        {
                            // kupcy stracili - roszczenie znika (bez zlota)
                            foreach (var b in a.B) { DForfeit += b.Left; DForfeitN++; ForfeitTotal += b.Left; ForfeitTotalN++; }
                            a.B.Clear(); gone.Add(kv.Key); continue;
                        }
                        List<KeyValuePair<Acct, Bill>> l;
                        if (!byDst.TryGetValue(a.Dst.StringId, out l)) { l = new List<KeyValuePair<Acct, Bill>>(); byDst[a.Dst.StringId] = l; }
                        foreach (var b in a.B) l.Add(new KeyValuePair<Acct, Bill>(a, b));
                    }
                    catch (Exception e) { Stumble("PayDaily.war", e); }
                }
                foreach (var kv in byDst)
                {
                    try
                    {
                        var l = kv.Value; if (l.Count == 0) continue;
                        l.Sort((x, y) => x.Value.Day != y.Value.Day ? x.Value.Day.CompareTo(y.Value.Day) : string.CompareOrdinal(x.Key.Src.StringId, y.Key.Src.StringId));
                        var dt = l[0].Key.Dst.Town;
                        long free = Free(dt);
                        foreach (var p in l)
                        {
                            if (free <= 0) break;
                            var b = p.Value; if (b.Left <= 0) continue;
                            long got = Transfer(dt, p.Key.Src.Town, Math.Min(free, b.Left));
                            if (got <= 0) break;
                            b.Left -= got; free -= got; DRepaid += got;
                        }
                    }
                    catch (Exception e) { Stumble("PayDaily.pay", e); }
                }
                foreach (var kv in _acc) { kv.Value.B.RemoveAll(b => b.Left <= 0); if (kv.Value.B.Count == 0) gone.Add(kv.Key); }
                foreach (var k in gone) _acc.Remove(k);
            }
            catch (Exception e) { Stumble("PayDaily", e); }
        }

        /// <summary>Rachunki otwarte wedlug zrodla (zl) - narazenie zrodla (KsFoodCarts.Daily, miejsce kredytu).</summary>
        internal static Dictionary<Settlement, long> OwedBySource()
        {
            Resolve();
            var d = new Dictionary<Settlement, long>();
            foreach (var a in _acc.Values)
            {
                if (a.Src == null) continue;
                long s = 0; foreach (var b in a.B) s += Math.Max(0, b.Left);
                long v; d.TryGetValue(a.Src, out v); d[a.Src] = v + s;
            }
            return d;
        }

        /// <summary>Cele z rachunkiem starszym niz zwloka (dzis - doba > KsConsignOverdueDays).</summary>
        internal static HashSet<Settlement> OverdueDst(int today)
        {
            Resolve();
            var h = new HashSet<Settlement>(); int od = OverdueDays;
            foreach (var a in _acc.Values)
            {
                if (a.Dst == null) continue;
                foreach (var b in a.B) if (b.Left > 0 && today - b.Day > od) { h.Add(a.Dst); break; }
            }
            return h;
        }

        /// <summary>Stan do linii: otwarte (zl), rachunkow, miast celu, starsze niz zwloka (zl i miast).</summary>
        internal static void Census(int today, out long open, out int bills, out int towns, out long overdue, out int overdueTowns, out long ownedMax, out string topDst)
        {
            Resolve();
            open = 0; bills = 0; overdue = 0; ownedMax = 0; topDst = "-";
            var dsts = new Dictionary<Settlement, long>(); var od = new HashSet<Settlement>(); int odd = OverdueDays;
            foreach (var a in _acc.Values)
                foreach (var b in a.B)
                {
                    if (b.Left <= 0) continue;
                    open += b.Left; bills++;
                    long v; dsts.TryGetValue(a.Dst, out v); dsts[a.Dst] = v + b.Left;
                    if (today - b.Day > odd) { overdue += b.Left; od.Add(a.Dst); }
                }
            towns = dsts.Count; overdueTowns = od.Count;
            var top = new List<KeyValuePair<Settlement, long>>(dsts);
            top.Sort((x, y) => y.Value.CompareTo(x.Value));
            if (top.Count > 0)
            {
                ownedMax = top[0].Value;
                var sb = new StringBuilder();
                for (int i = 0; i < top.Count && i < 5; i++) sb.Append(i == 0 ? "" : ", ").Append(top[i].Key == null ? "?" : (top[i].Key.Name != null ? top[i].Key.Name.ToString() : top[i].Key.StringId)).Append(' ').Append(top[i].Value);
                topDst = sb.ToString();
            }
        }

        /// <summary>Koniec doby (KsFoodCarts.Flush, co dobe - takze bez linii): kontrola rachunkow otwarte wczoraj + nowe - splacone - przepadle - otwarte dzis
        /// (pierwsza doba po wczytaniu - "-"); otwarte dzis do nastepnej doby.</summary>
        internal static string EndDay(out long openNow)
        {
            Resolve();
            openNow = 0;
            foreach (var a in _acc.Values) foreach (var b in a.B) if (b.Left > 0) openNow += b.Left;
            string s = _openPrev < 0 ? "-" : (_openPrev + DNew - DRepaid - DForfeit - openNow).ToString(CultureInfo.InvariantCulture);
            _openPrev = openNow;
            return s;
        }

        // ------------------------------------------------------------ zapis (arm_daniny_kupcy)
        internal static string Export()
        {
            Resolve();
            if (_acc.Count == 0) return "";
            string s = Build(false);
            if (s.Length > SaveCapChars)
            {
                // rozmiar ponad 64 KB - wpisy pary scalane w jeden (najstarsza doba, suma); nic nie przepada
                s = Build(true);
                try { Log.Info("KS: zapis arm_daniny_kupcy - wpisy par scalone (rozmiar ponad 64 KB), teraz " + (s.Length / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB."); } catch { }
            }
            try { Log.Info("KS: zapis arm_daniny_kupcy doba " + Today() + " | " + (s.Length / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB (par " + _acc.Count + ")."); } catch { }
            return s;
        }

        private static string Build(bool merge)
        {
            var sb = new StringBuilder(_acc.Count * 40 + 8);
            sb.Append(Ver);
            var keys = new List<string>(_acc.Keys); keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys)
            {
                var a = _acc[k]; if (a.Src == null || a.Dst == null) continue;
                if (merge)
                {
                    long sum = 0; int day = int.MaxValue;
                    foreach (var b in a.B) { if (b.Left <= 0) continue; sum += b.Left; day = Math.Min(day, b.Day); }
                    if (sum > 0) sb.Append('|').Append(a.Src.StringId).Append('~').Append(a.Dst.StringId).Append('~').Append(sum).Append('~').Append(day);
                    continue;
                }
                foreach (var b in a.B)
                    if (b.Left > 0) sb.Append('|').Append(a.Src.StringId).Append('~').Append(a.Dst.StringId).Append('~').Append(b.Left).Append('~').Append(b.Day);
            }
            return sb.ToString();
        }

        internal static void Import(string s) { _acc.Clear(); _openPrev = -1; _pending = string.IsNullOrEmpty(s) ? null : s; }

        /// <summary>Rachunki z zapisu (wczytanie albo pierwsze uzycie): osady wedlug StringId; rekord bez osady - linia (roszczenie odrzucone tylko przy uszkodzonym zapisie).</summary>
        internal static void Resolve()
        {
            var s = _pending; if (string.IsNullOrEmpty(s)) return;
            _pending = null;
            int ok = 0, bad = 0; long sum = 0;
            try
            {
                var om = MBObjectManager.Instance;
                var p = s.Split('|');
                if (p.Length == 0 || p[0] != Ver) { Log.Info("KS: arm_daniny_kupcy - nieznany format (" + (p.Length > 0 ? p[0] : "") + "), rachunki pominiete."); return; }
                for (int i = 1; i < p.Length; i++)
                {
                    try
                    {
                        var q = p[i].Split('~'); long left; int day;
                        if (q.Length < 4 || !long.TryParse(q[2], out left) || !int.TryParse(q[3], out day) || left <= 0) { bad++; continue; }
                        Settlement src = null, dst = null;
                        try { src = om.GetObject<Settlement>(q[0]); dst = om.GetObject<Settlement>(q[1]); } catch { }
                        if (src == null || dst == null) { bad++; continue; }
                        Acct a; string k = Key(src, dst);
                        if (!_acc.TryGetValue(k, out a)) { a = new Acct { Src = src, Dst = dst }; _acc[k] = a; }
                        if (a.B.Count >= MaxEntries) a.B[a.B.Count - 1].Left += left; else a.B.Add(new Bill { Left = left, Day = day });
                        ok++; sum += left;
                    }
                    catch (Exception e) { Stumble("Resolve.rec", e); bad++; }
                }
                foreach (var a in _acc.Values) a.B.Sort((x, y) => x.Day.CompareTo(y.Day));
            }
            catch (Exception e) { Stumble("Resolve", e); }
            try { Log.Info("Kupcy zywnosci (KS-H): rachunki kredytu z zapisu " + ok + " (" + sum + " zl; odrzucone bez osady " + bad + ")."); } catch { }
        }
    }
}
