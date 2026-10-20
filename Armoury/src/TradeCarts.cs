using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E6/R3 (215, PLAN 6.7 (c), R15 projektu etapu 6 v2: "kazdy towar jedzie wozem; koniec przerzutu natychmiastowego") - HURT KUPCOW WOZEM BEZ PARTII.
    /// Dotad SupplyDemand.DailyTrade przenosil kupiona nadwyzke uzbrojenia, uprzezy i koni z polki osady-zrodla na polke celu tej samej doby, bez drogi
    /// (1 301 szt./d przerzutem, rozpoznanie A d91-120). Teraz sztuka schodzi z polki zrodla w chwili zakupu (cel placi przy zakupie - jak dotad, E5 A1),
    /// a na polke celu wchodzi po czasie drogi: doby = droga ladowa (MapDistanceModel, wyspa - ladem i morzem, bez drogi - prosta) / predkosc wozu wsi
    /// (MarketCarts.PerDay - ten sam wzor co wozy zamkow GarrisonCarts). Jeden woz na pare zrodlo-cel na dobe (sztuki z wielu transakcji tej doby razem).
    /// W drodze: cel oblezony - woz czeka; po 30 dobach od zakupu - zawraca (sztuki na polke zrodla, zwrot zaplaty z kasy zrodla, nie wiecej niz kasa -
    /// jak GarrisonCarts.Back). Bandyci wozu nie rozbijaja (uproszczenie jak 171; prawdziwa partia - odlozone, projekt rozdz. 7).
    /// Towar w drodze liczy sie do polki celu przy wyborze celu przez kupcow (Incoming - inaczej kupcy wiezliby co dobe w to samo puste miejsce) i do
    /// stanu ksiag E6/0 (ArmsBook, HorseBook - "w drodze"). Zapis: klucz arm_tradecarts (brak klucza - stary zapis, pusto); po wczytaniu rozwiazanie
    /// w OnSessionLaunched (w SyncData obiektow gry jeszcze nie ma). Wylaczony klucz ArmsTradeByCart - nowe zakupy jak dotad, oplacone wozy dojezdzaja.
    /// Kontrola doby: w drodze wczoraj + kupione - dowiezione - zawrocone - odrzucone przy wczytaniu = w drodze dzis (reszta 0).
    /// </summary>
    internal static class TradeCarts
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.ArmsTradeByCart && SupplyDemand.Active; } }

        private const int MaxDays = 30;

        private sealed class Line { internal EquipmentElement El; internal int N; }
        private sealed class Cart
        {
            internal Settlement Src, Dst; internal int DayBought, DayDue, Paid;
            internal readonly List<Line> Lines = new List<Line>();
            internal int Pieces { get { int n = 0; foreach (var l in Lines) n += l.N; return n; } }
        }

        private static readonly List<Cart> _carts = new List<Cart>();
        private static readonly Dictionary<Settlement, Dictionary<int, int>> _incoming = new Dictionary<Settlement, Dictionary<int, int>>();   // cel -> koszyk (typ*10+tier) -> szt. w drodze
        private static readonly Dictionary<long, Cart> _today = new Dictionary<long, Cart>();   // para zrodlo-cel -> woz tej doby
        private static string _pending;
        private static long _prevTransit = -1;   // w drodze przy poprzedniej linii (-1 = pierwsza linia sesji)

        // liczniki doby (linia "Przewoz kupcow wozem (215)")
        private static int _dCarts, _dBought, _dGold, _dDaysSum, _dArrCarts, _dArr, _dArrDays, _dWait, _dBack, _dBackPcs, _dRefund, _dLost, _stumbles, _errDay = -1;
        private static readonly HashSet<string> _errWhere = new HashSet<string>();

        internal static void Reset()
        {
            _carts.Clear(); _incoming.Clear(); _today.Clear(); _pending = null; _prevTransit = -1;
            ClearDay(); _stumbles = 0; _errDay = -1; _errWhere.Clear();
        }

        private static void ClearDay()
        {
            _dCarts = _dBought = _dGold = _dDaysSum = _dArrCarts = _dArr = _dArrDays = _dWait = _dBack = _dBackPcs = _dRefund = _dLost = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try
            {
                int d = Today();
                if (d != _errDay) { _errDay = d; _errWhere.Clear(); }
                if (_errWhere.Add(where)) Log.Error("TradeCarts." + where, e);
            }
            catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static int Key(ItemObject it) { return (int)it.ItemType * 10 + Math.Max(1, Math.Min(6, (int)it.Tier + 1)); }

        /// <summary>Sztuki koszyka (typ*10+tier), ktore jada juz do tej osady.</summary>
        internal static int Incoming(Settlement dst, int key)
        {
            Dictionary<int, int> d; int n;
            return dst != null && _incoming.TryGetValue(dst, out d) && d.TryGetValue(key, out n) ? n : 0;
        }

        internal static long InTransitPieces() { long n = 0; foreach (var c in _carts) n += c.Pieces; return n; }

        /// <summary>Ksiegi E6/0: kazda sztuka w drodze (stan "w drodze").</summary>
        internal static void ForEachInTransit(Action<ItemObject, int> f)
        {
            foreach (var c in _carts) foreach (var l in c.Lines) if (l.El.Item != null && l.N > 0) f(l.El.Item, l.N);
        }

        /// <summary>F1 (test zbiorczy 5-8, 120 dob): spisy unikatow (ValyrianBlades.Gather, kronika UniqueSpoils.Daily) - kazda linia wozu w drodze ze
        /// zrodlem i celem (zrodlo, cel, sztuka, ile). Tylko odczyt. Najpierw rozwiazanie zapisu (jak Export): spis startowy stali biegnie w
        /// OnSessionLaunched LegendaryLaw, a tego sluchacza gra wola PRZED ArmouryBehavior (MbEvent dopisuje sluchaczy na poczatek listy) - bez tego
        /// wozy z zapisu bylyby jeszcze napisem i klinga w wozie wygladalaby po wczytaniu na brak.</summary>
        internal static void ForEachCartLine(Action<Settlement, Settlement, EquipmentElement, int> f)
        {
            if (_pending != null) ResolvePending("spis unikatow");
            foreach (var c in _carts) foreach (var l in c.Lines) if (l.El.Item != null && l.N > 0) f(c.Src, c.Dst, l.El, l.N);
        }

        /// <summary>Napis miejsca sztuki w wozie - STALY w czasie (bez liczby dob do przyjazdu; inaczej spisy pisalyby "zmiane" co dobe).</summary>
        internal static string RoadLabel(Settlement src, Settlement dst)
        {
            return "w drodze wozem " + (src != null && src.Name != null ? src.Name.ToString() : "?") + " -> " + (dst != null && dst.Name != null ? dst.Name.ToString() : "?");
        }

        private static void AddIncoming(Cart c, Line l, int sign)
        {
            if (c == null || c.Dst == null || l == null || l.El.Item == null) return;
            Dictionary<int, int> d;
            if (!_incoming.TryGetValue(c.Dst, out d)) _incoming[c.Dst] = d = new Dictionary<int, int>();
            int k = Key(l.El.Item), v; d.TryGetValue(k, out v); v += sign * l.N;
            if (v > 0) d[k] = v; else d.Remove(k);
            if (d.Count == 0) _incoming.Remove(c.Dst);
        }

        /// <summary>Doby drogi wozem: droga ladowa, wyspa - ladem i morzem, bez drogi - odleglosc prosta (straight, jak zasieg kupcow); co najmniej 1.</summary>
        private static int Days(Settlement src, Settlement dst, float straight)
        {
            return DaysFor(Road(src, dst, Math.Max(0f, straight)));
        }

        /// <summary>F4a (test zbiorczy 5-8): droga wozem miedzy osadami w jednostkach mapy - ten sam rachunek co Days (wystawiony dla wozow dymarek,
        /// Bloomery): droga ladowa, wyspa - ladem i morzem (porty obu osad), bez drogi - straight; straight ujemne = bez drogi wynik -1.
        /// landOnly - sama droga ladowa (woz do wsi: wies portu nie ma).</summary>
        internal static float Road(Settlement src, Settlement dst, float straight, bool landOnly = false)
        {
            float d = -1f;
            try
            {
                var m = Campaign.Current.Models.MapDistanceModel;
                try { d = m.GetDistance(src, dst, false, false, MobileParty.NavigationType.Default); } catch { d = -1f; }
                if (!landOnly && !(d >= 0f && d < CartTownExit.BkLimit))
                    try { d = m.GetDistance(src, dst, src.HasPort, dst.HasPort, MobileParty.NavigationType.All); } catch { d = -1f; }
            }
            catch { d = -1f; }
            if (!(d >= 0f && d < CartTownExit.BkLimit)) d = straight >= 0f ? straight : -1f;
            return d;
        }

        /// <summary>F4a: doby drogi wozu wsi na odcinku d (predkosc MarketCarts.PerDay); co najmniej 1.</summary>
        internal static int DaysFor(float d)
        {
            double perDay = Math.Max(1.0, MarketCarts.PerDay(Settings.Current));
            return Math.Max(1, (int)Math.Ceiling(d / perDay));
        }

        /// <summary>Z SupplyDemand.DailyTrade: n sztuk zdjetych z polki zrodla i oplaconych przez cel (cost) jedzie wozem; straight - odleglosc prosta pary.</summary>
        internal static void Ship(Settlement src, Settlement dst, EquipmentElement el, int n, int cost, float straight)
        {
            if (src == null || dst == null || el.Item == null || n <= 0) return;
            try
            {
                long pk = ((long)src.Id.InternalValue << 32) | dst.Id.InternalValue;
                Cart c;
                if (!_today.TryGetValue(pk, out c))
                {
                    int today = Today();
                    int days = Days(src, dst, straight) + SeaStorms.CartDays(SeaStorms.CTrade, false, src, dst);   // p6-sztormy (recenzja): zima przez morze - czekanie w porcie
                    c = new Cart { Src = src, Dst = dst, DayBought = today, DayDue = today + days };
                    _today[pk] = c; _carts.Add(c);
                    _dCarts++; _dDaysSum += days;
                }
                var l = new Line { El = el, N = n };
                c.Lines.Add(l); c.Paid += Math.Max(0, cost);
                AddIncoming(c, l, +1);
                _dBought += n; _dGold += Math.Max(0, cost);
            }
            catch (Exception e)
            {
                // woz sie nie zapisal - sztuka nie moze zniknac: na polke celu od razu (jak dotad, przerzut)
                Stumble("Ship", e);
                try { dst.ItemRoster.AddToCounts(el, n); Measure174b.NoteArrival(dst, el.Item, n, Measure174b.ArrTrade); } catch { }
            }
        }

        /// <summary>Raz na dobe na poczatku SupplyDemand.DailyTrade (przed wywozem): dostawy, czekanie pod oblezeniem, zawrocenia po 30 dobach.
        /// Dziala takze przy wylaczonym kluczu (oplacone wozy dojezdzaja).</summary>
        internal static void Arrivals()
        {
            _today.Clear();
            if (_carts.Count == 0) return;
            int today = Today();
            var done = new List<Cart>();
            foreach (var c in _carts)
            {
                try
                {
                    if (today < c.DayDue) continue;
                    bool dstOk = c.Dst != null && c.Dst.ItemRoster != null, srcOk = c.Src != null && c.Src.ItemRoster != null;
                    // recenzja 215: 30 dob liczy sie do czekania - woz z droga dluzsza niz 30 dob (wyspa morzem, niskie tempo swiata) w dniu przyjazdu
                    // nie zawraca od razu (dotad zawracal zawsze, nie dojezdzajac); droga do 30 dob - jak dotad
                    bool late = today - c.DayBought > Math.Max(MaxDays, c.DayDue - c.DayBought);
                    if (!dstOk && !srcOk) { _dLost += c.Pieces; done.Add(c); continue; }   // nie ma dokad ani skad (tylko po zlym zapisie)
                    if (!dstOk || (late && srcOk)) { Back(c); done.Add(c); continue; }   // 30 dob - przed czekaniem (jak GarrisonCarts)
                    if (c.Dst.IsUnderSiege && !late) { _dWait++; continue; }
                    // linia po linii: dostarczona linia ma N = 0 (wyjatek w polowie - nazajutrz tylko reszta, bez dubla)
                    foreach (var l in c.Lines)
                    {
                        if (l.El.Item == null || l.N <= 0) continue;
                        int n = l.N;
                        c.Dst.ItemRoster.AddToCounts(l.El, n);
                        AddIncoming(c, l, -1); l.N = 0;
                        _dArr += n;
                        try { Measure174b.NoteArrival(c.Dst, l.El.Item, n, Measure174b.ArrTrade); } catch { }   // 174b.0 M1 / ksiega E6/0: przerzut kupcow - przyjazd na polke
                    }
                    _dArrCarts++; _dArrDays += today - c.DayBought;
                    done.Add(c);
                }
                catch (Exception e) { Stumble("Arrivals", e); }   // woz zostaje w drodze (z niedostarczonymi liniami) - sprobuje nazajutrz
            }
            foreach (var c in done) { _carts.Remove(c); foreach (var l in c.Lines) if (l.N > 0) try { AddIncoming(c, l, -1); } catch { } }
        }

        /// <summary>Zawrocenie: sztuki na polke zrodla; zwrot zaplaty z kasy zrodla do kasy celu, nie wiecej niz kasa zrodla (reszta zostaje w kasie zrodla -
        /// to samo zloto, ktore cel zaplacil; jak GarrisonCarts.Back).</summary>
        private static void Back(Cart c)
        {
            int pcs = 0;
            if (c.Src != null && c.Src.ItemRoster != null)
                foreach (var l in c.Lines)
                {
                    if (l.El.Item == null || l.N <= 0) continue;
                    int n = l.N;
                    c.Src.ItemRoster.AddToCounts(l.El, n);
                    AddIncoming(c, l, -1); l.N = 0;
                    pcs += n;
                    try { Measure174b.NoteArrival(c.Src, l.El.Item, n, Measure174b.ArrTrade, true); } catch { }
                }
            _dBack++; _dBackPcs += pcs;
            if (c.Src == null || c.Src.Town == null || c.Dst == null || c.Dst.Town == null) return;
            int r = Math.Min(c.Paid, Math.Max(0, c.Src.Town.Gold));
            if (r <= 0) return;
            c.Src.Town.ChangeGold(-r);
            c.Dst.Town.ChangeGold(r);
            _dRefund += r;
        }

        /// <summary>Na koncu SupplyDemand.DailyTrade: linia doby i kontrola "w drodze wczoraj + kupione - dowiezione - zawrocone - odrzucone = w drodze dzis".</summary>
        internal static void Flush()
        {
            _today.Clear();
            long now = InTransitPieces();
            try
            {
                if (!On && _dBought + _dArr + _dBackPcs == 0 && now == 0) { _prevTransit = now; ClearDay(); _stumbles = 0; return; }
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder();
                sb.Append("Przewoz kupcow wozem (215): dzien ").Append(Today()).Append(" | ").Append(On ? "CZYNNE" : "WYLACZONE (tylko dowoz oplaconych)")
                  .Append(" | kupione ").Append(_dBought).Append(" szt. za ").Append(_dGold).Append(" zl w ").Append(_dCarts).Append(" wozach (srednio ")
                  .Append(_dCarts > 0 ? ((double)_dDaysSum / _dCarts).ToString("0.0", inv) : "0").Append(" dob drogi)")
                  .Append(" | dowiezione ").Append(_dArr).Append(" szt. w ").Append(_dArrCarts).Append(" wozach (srednio ")
                  .Append(_dArrCarts > 0 ? ((double)_dArrDays / _dArrCarts).ToString("0.0", inv) : "0").Append(" dob od zakupu)")
                  .Append(" | czeka pod oblezeniem ").Append(_dWait).Append(" wozow | zawrocone ").Append(_dBackPcs).Append(" szt. w ").Append(_dBack)
                  .Append(" wozach (30 dob; zwrot ").Append(_dRefund).Append(" zl) | w drodze ").Append(now).Append(" szt. w ").Append(_carts.Count).Append(" wozach");
                if (_prevTransit >= 0)
                {
                    long rest = _prevTransit + _dBought - _dArr - _dBackPcs - _dLost - now;
                    sb.Append(" | kontrola: w drodze wczoraj ").Append(_prevTransit).Append(" + kupione - dowiezione - zawrocone - bez celu ").Append(_dLost).Append(" - w drodze dzis = reszta ").Append(rest);
                }
                else sb.Append(" | kontrola od nastepnej doby");
                sb.Append(" | potkniecia dzis ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("TradeCarts.Flush", e); }
            _prevTransit = now;
            ClearDay(); _stumbles = 0;
        }

        /// <summary>Dopisek do linii "PodazPopyt: kupcy wywiezli": w drodze a szt. (srednio b dob drogi wozow tej doby).</summary>
        internal static string Brief()
        {
            if (!On) return "";
            return "; wozem (215): dzis w drodze " + _dBought + " szt. w " + _dCarts + " wozach (srednio "
                   + (_dCarts > 0 ? ((double)_dDaysSum / _dCarts).ToString("0.0", CultureInfo.InvariantCulture) : "0") + " dob drogi), razem w drodze " + InTransitPieces() + " szt.";
        }

        // ------------------------------------------------------------ zapis: zrodlo|cel|dobaZakupu|dobaPrzyjazdu|zaplacone|przedmiot:modyfikator:ile;...~
        internal static string Export()
        {
            if (_pending != null) ResolvePending("zapis przed startem sesji");
            var sb = new StringBuilder();
            foreach (var c in _carts)
            {
                if (c.Src == null || c.Dst == null) continue;
                sb.Append(c.Src.StringId).Append('|').Append(c.Dst.StringId).Append('|').Append(c.DayBought).Append('|').Append(c.DayDue).Append('|').Append(c.Paid).Append('|');
                foreach (var l in c.Lines)
                {
                    if (l.El.Item == null || l.N <= 0) continue;
                    sb.Append(l.El.Item.StringId).Append(':').Append(l.El.ItemModifier != null ? l.El.ItemModifier.StringId : "").Append(':').Append(l.N).Append(';');
                }
                sb.Append('~');
            }
            return sb.ToString();
        }

        /// <summary>Z SyncData: tylko zapamietanie - rozwiazanie w ResolvePending (OnSessionLaunched).</summary>
        internal static void Import(string s)
        {
            _carts.Clear(); _incoming.Clear(); _today.Clear(); _prevTransit = -1;
            _pending = string.IsNullOrEmpty(s) ? null : s;
        }

        internal static void ResolvePending(string why)
        {
            var s = _pending;
            _pending = null;
            if (string.IsNullOrEmpty(s)) return;
            var om = MBObjectManager.Instance;
            int recs = 0, ok = 0, lost = 0, pcs = 0;
            foreach (var rec in s.Split('~'))
            {
                if (rec.Length == 0) continue;
                try
                {
                    var a = rec.Split('|'); if (a.Length != 6) continue;
                    recs++;
                    Settlement src = null, dst = null;
                    try { src = om.GetObject<Settlement>(a[0]); dst = om.GetObject<Settlement>(a[1]); } catch { }
                    if (src == null || dst == null) { lost++; continue; }
                    int d0, d1, paid;
                    int.TryParse(a[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out d0);
                    int.TryParse(a[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out d1);
                    int.TryParse(a[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out paid);
                    var c = new Cart { Src = src, Dst = dst, DayBought = d0, DayDue = d1, Paid = Math.Max(0, paid) };
                    foreach (var tok in a[5].Split(';'))
                    {
                        if (tok.Length == 0) continue;
                        var p = tok.Split(':'); if (p.Length != 3) continue;
                        ItemObject it = null; try { it = om.GetObject<ItemObject>(p[0]); } catch { }
                        if (it == null) continue;
                        ItemModifier m = null; if (p[1].Length > 0) { try { m = om.GetObject<ItemModifier>(p[1]); } catch { } }
                        int n;
                        if (!int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n <= 0) continue;
                        c.Lines.Add(new Line { El = new EquipmentElement(it, m), N = n });
                        pcs += n;
                    }
                    if (c.Lines.Count == 0) { lost++; continue; }
                    _carts.Add(c);
                    foreach (var l in c.Lines) AddIncoming(c, l, +1);
                    ok++;
                }
                catch (Exception e) { Stumble("ResolvePending", e); lost++; }
            }
            Log.Info("Przewoz kupcow wozem (215): z zapisu (" + why + ") " + ok + " wozow w drodze z " + recs + " (" + pcs + " szt.; odrzucone bez osady/towaru " + lost + ").");
        }
    }
}
