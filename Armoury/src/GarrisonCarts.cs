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
    /// 171 ZALOGA ZAMKU KUPUJE W MIESCIE (docs/paczki/171-zbrojenie-zalog.md, C1-C8; Jeff 08.10 "tak potwierdzam" (1): zaloga zamku kupuje bron
    /// w najblizszym przyjaznym miescie - pan zamku placi kasie miasta, towar jedzie do zamku).
    /// Zamek nie ma kupcow (gra sprzedaje lup tylko w miastach, ColdStart zaopatruje tylko miasta), a SupplyDemand.DailyTrade przenosil bron
    /// do zamkow natychmiast, bez drogi. Teraz: zaloga zamku kupuje z polki WLASNEGO zamku (to, co tam lezy, oplacila kasa zamku), a reszte
    /// zamawia raz na GarrisonOrderDays dob w miescie handlowym wsi zamku (ArmyClothing.MarketTown - TradeBound gry, bez miast wroga).
    /// Towar schodzi z polki miasta w chwili zakupu, zloto pana idzie do kasy miasta, a zamowienie jedzie "wozem bez partii": zapis z data
    /// przyjazdu = droga / predkosc wozu wsi (MarketCarts.PerDay - te same wozy, ktore woza plon). Bandyci nie rozbijaja dostawy (uproszczenie; recenzja 174b:
    /// przerzut bez partii na mapie jak SupplyDemand.DailyTrade - do decyzji po 174b, czy ma jezdzic prawdziwa partia; M1 liczy go osobno jako przerzut).
    /// W drodze: zamek oblezony albo bez zalogi - czeka; zamek padl (wojna z rodem placacym) albo rod wymarl, a zamek zmienil strone - zawraca
    /// (towar na polke miasta-zrodla, zwrot zaplaty z kasy miasta, nie wiecej niz kasa); po 30 dobach - zawraca jak woz wsi (MarketCarts).
    /// </summary>
    internal static class GarrisonCarts
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.GarrisonGearFromTown && AiGear.On; } }

        // internal: AiGear buduje liste (krytyka 10, CS0051); Bucket = koszyk potrzeby, na ktory kupiono (t albo t-1 na brak t)
        internal sealed class Line { internal EquipmentElement El; internal int N; internal int Bucket; }
        private sealed class Order
        {
            internal Settlement Castle, Market; internal Clan PayerClan; internal IFaction FactionAtOrder;   // FactionAtOrder = frakcja zamku przy zamowieniu (C4)
            internal int DayOrdered, DayDue, Paid; internal List<Line> Lines = new List<Line>();
            internal int Pieces { get { int n = 0; foreach (var l in Lines) n += l.N; return n; } }
        }
        private static readonly List<Order> _orders = new List<Order>();
        private static readonly Dictionary<Settlement, Dictionary<int, int>> _transit = new Dictionary<Settlement, Dictionary<int, int>>();   // zamek -> koszyk -> szt. w drodze
        private static readonly Dictionary<Settlement, int> _lastOrder = new Dictionary<Settlement, int>();                                 // zamek -> doba ostatniej proby zamowienia (bez zapisu)
        private static string _pending;   // z SyncData, rozwiazywane po starcie sesji

        // liczniki doby (linia "Zaopatrzenie zamkow (171)")
        private static int _dOrders, _dPieces, _dGold, _dPieceLimit, _dBudgetLimit, _dOwnPieces, _dOwnGold;
        private static int _dArrived, _dArrivedPieces, _dArrivedDays, _dWaitSiege, _dWaitNoGarrison;
        private static int _dBack, _dBackPieces, _dRefund, _dBackHostile, _dBackForeign, _dBack30, _dBackNoRefund, _dBackPeace;
        private static int _dNoTown, _dNoRoad, _dTooFar, _dSiegeWar, _dPause, _dUnmet, _stumbles, _errDay = -1;
        private static readonly HashSet<Settlement> _dTowns = new HashSet<Settlement>();
        private static readonly HashSet<string> _errWhere = new HashSet<string>();

        internal static void Reset()
        {
            _orders.Clear(); _transit.Clear(); _lastOrder.Clear(); _pending = null;
            ClearDay(); _stumbles = 0; _errDay = -1; _errWhere.Clear();
        }

        private static void ClearDay()
        {
            _dOrders = _dPieces = _dGold = _dPieceLimit = _dBudgetLimit = _dOwnPieces = _dOwnGold = 0;
            _dArrived = _dArrivedPieces = _dArrivedDays = _dWaitSiege = _dWaitNoGarrison = 0;
            _dBack = _dBackPieces = _dRefund = _dBackHostile = _dBackForeign = _dBack30 = _dBackNoRefund = _dBackPeace = 0;
            _dNoTown = _dNoRoad = _dTooFar = _dSiegeWar = _dPause = _dUnmet = 0;
            _dTowns.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try
            {
                int d = (int)CampaignTime.Now.ToDays;
                if (d != _errDay) { _errDay = d; _errWhere.Clear(); }
                if (_errWhere.Add(where)) Log.Error("GarrisonCarts." + where, e);
            }
            catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        // ------------------------------------------------------------ zamowienie (z AiGear.TryBuy)
        internal static bool CanOrderToday(Settlement castle)
        {
            if (castle == null) return false;
            var s = Settings.Current;
            int D = Math.Max(1, s != null ? s.GarrisonOrderDays : 3);
            // recenzja 171: pierwsze zamowienie (nowa kampania, po wczytaniu - stempli nie zapisujemy) rozlozone po dobach wedlug Id zamku,
            // inaczej ok. 130 zamkow zamawia tego samego dnia i zawsze w tej samej kolejnosci (pierwszy zamek co raz bierze wspolna polke miasta)
            int d; if (!_lastOrder.TryGetValue(castle, out d)) return (long)Today() % D == (long)(castle.Id.InternalValue % (uint)D);
            return Today() - d >= D;
        }

        /// <summary>Stempel proby zamowienia - takze nieudanej (bez miasta, bez drogi, brak towaru): przeglad polki miasta raz na D dob.</summary>
        internal static void MarkTried(Settlement castle) { if (castle != null) _lastOrder[castle] = Today(); }

        internal static void NotePause() { _dPause++; }
        internal static void NoteOwnShelf(int pieces, int gold) { _dOwnPieces += pieces; _dOwnGold += gold; }
        internal static void NoteUnmet(int buckets) { _dUnmet += buckets; }

        /// <summary>C5: towar w drodze odejmowany od potrzeby (koszyk potrzeby, na ktory kupiono).</summary>
        internal static void SubtractTransit(Settlement castle, Dictionary<int, int> need)
        {
            Dictionary<int, int> tr;
            if (castle == null || need == null || !_transit.TryGetValue(castle, out tr)) return;
            foreach (var k in new List<int>(need.Keys))
            {
                int w; if (!tr.TryGetValue(k, out w) || w <= 0) continue;
                need[k] = Math.Max(0, need[k] - w);
            }
        }

        /// <summary>C1: miasto handlowe wsi zamku (TradeBound gry), inaczej najblizsze niewrogie; w chwili zamowienia: bez oblezenia i wojny, droga ladem albo ladem i morzem.</summary>
        internal static Settlement MarketFor(Settlement castle, out float dist, out string why)
        {
            dist = -1f; why = null;
            try
            {
                var market = castle != null ? ArmyClothing.MarketTown(castle) : null;
                if (market == null || market.Town == null || market.ItemRoster == null) { why = "bez miasta"; _dNoTown++; return null; }
                if (market.IsUnderSiege || castle.IsUnderSiege
                    || (castle.MapFaction != null && market.MapFaction != null && FactionManager.IsAtWarAgainstFaction(castle.MapFaction, market.MapFaction)))
                { why = "oblezenie albo wojna"; _dSiegeWar++; return null; }
                var m = Campaign.Current.Models.MapDistanceModel;
                float d;
                try { d = m.GetDistance(market, castle, false, false, MobileParty.NavigationType.Default); } catch { d = -1f; }
                if (!(d >= 0f && d < CartTownExit.BkLimit))   // brak drogi ladowej (wyspa) - ladem i morzem
                    try { d = m.GetDistance(market, castle, market.HasPort, castle.HasPort, MobileParty.NavigationType.All); } catch { d = -1f; }
                if (!(d >= 0f && d < CartTownExit.BkLimit)) { why = "bez drogi"; _dNoRoad++; return null; }
                var s = Settings.Current;
                if (s.MarketMaxDistance > 0f && d > s.MarketMaxDistance) { why = "za daleko"; _dTooFar++; return null; }
                dist = d;
                return market;
            }
            catch (Exception e) { Stumble("MarketFor", e); why = "potkniecie"; return null; }
        }

        /// <summary>C3: zamowienie w drodze - zloto juz w kasie miasta, sztuki zdjete z jego polki.</summary>
        internal static void Place(Settlement castle, Settlement market, Clan payer, List<Line> lines, int paid, float dist, bool atPieceLimit = false, bool atBudgetLimit = false)
        {
            try
            {
                if (castle == null || market == null || lines == null || lines.Count == 0) return;
                var s = Settings.Current;
                double perDay = Math.Max(1.0, MarketCarts.PerDay(s));
                int days = Math.Max(1, (int)Math.Ceiling(Math.Max(0f, dist) / perDay));
                int today = Today();
                var o = new Order { Castle = castle, Market = market, PayerClan = payer, FactionAtOrder = castle.MapFaction, DayOrdered = today, DayDue = today + days, Paid = paid };
                o.Lines.AddRange(lines);
                _orders.Add(o);
                AddTransit(o, +1);
                _dOrders++; _dTowns.Add(market); _dPieces += o.Pieces; _dGold += paid;
                if (atPieceLimit) _dPieceLimit++; else if (atBudgetLimit) _dBudgetLimit++;
            }
            catch (Exception e) { Stumble("Place", e); }
        }

        private static void AddTransit(Order o, int sign)
        {
            if (o == null || o.Castle == null) return;
            Dictionary<int, int> tr;
            if (!_transit.TryGetValue(o.Castle, out tr)) _transit[o.Castle] = tr = new Dictionary<int, int>();
            foreach (var l in o.Lines)
            {
                int v; tr.TryGetValue(l.Bucket, out v); v += sign * l.N;
                if (v > 0) tr[l.Bucket] = v; else tr.Remove(l.Bucket);
            }
            if (tr.Count == 0) _transit.Remove(o.Castle);
        }

        internal static int InTransitPieces() { int n = 0; foreach (var o in _orders) n += o.Pieces; return n; }

        // ------------------------------------------------------------ C4: w drodze (raz na dobe)
        internal static void Daily()
        {
            int today = Today();
            var done = new List<Order>();
            foreach (var o in _orders)
            {
                try
                {
                    if (today < o.DayDue) continue;
                    if (o.Castle == null || o.Market == null) { done.Add(o); continue; }
                    // 30 dob - PRZED warunkami "czeka" (krytyka 9): woz pod oblezonym zamkiem tez kiedys zawraca
                    if (today - o.DayOrdered > 30) { Back(o, true); _dBack30++; done.Add(o); continue; }
                    bool payerAlive = o.PayerClan != null && !o.PayerClan.IsEliminated;
                    if (payerAlive && o.Castle.MapFaction != null && o.PayerClan.MapFaction != null && FactionManager.IsAtWarAgainstFaction(o.Castle.MapFaction, o.PayerClan.MapFaction))
                    { Back(o, true); _dBackHostile++; done.Add(o); continue; }   // zamek padl - towaru nie oddaje sie wrogowi
                    if (!payerAlive && o.Castle.MapFaction != o.FactionAtOrder)
                    { Back(o, false); _dBackForeign++; done.Add(o); continue; }  // nie ma kogo zapytac o wojne, a zamek zmienil strone
                    // recenzja 171: zamek przeszedl do obcych (zdobyty, a potem pokoj, albo oddany w traktacie) - towar nie jedzie do obcego pana; zwrot jak przy wojnie
                    if (payerAlive && o.Castle.MapFaction != o.FactionAtOrder && o.Castle.MapFaction != o.PayerClan.MapFaction)
                    { Back(o, true); _dBackPeace++; done.Add(o); continue; }
                    if (o.Castle.IsUnderSiege) { _dWaitSiege++; continue; }
                    var g = o.Castle.Town != null ? o.Castle.Town.GarrisonParty : null;
                    if (g == null) { _dWaitNoGarrison++; continue; }
                    // dostawa (zmiana pana w tym samym krolestwie / pan zginal - towar jest dla ludzi zalogi, przechodzi z zamkiem)
                    int pcs = 0;
                    foreach (var l in o.Lines)
                        if (l.El.Item != null && l.N > 0 && AiGear.AddToArmory(g, l.El.Item, l.N))
                        {
                            pcs += l.N;
                            try { AiWear.NoteBought(g, l.El, l.N); } catch { }   // recenzja 171: obita z polki miasta dojezdza obita (zapis zuzycia zalogi)
                        }
                    _dArrived++; _dArrivedPieces += pcs; _dArrivedDays += today - o.DayOrdered;
                    done.Add(o);
                }
                catch (Exception e) { Stumble("Daily", e); done.Add(o); }
            }
            foreach (var o in done) { _orders.Remove(o); try { AddTransit(o, -1); } catch { } }
            Flush(today);
        }

        /// <summary>Zawrocenie: sztuki na polke miasta-zrodla; zwrot zaplaty (kontrakt zerwany po stronie kupca) nie wiecej niz kasa miasta.</summary>
        private static void Back(Order o, bool refund)
        {
            int pcs = 0;
            if (o.Market != null && o.Market.ItemRoster != null)
                foreach (var l in o.Lines) if (l.El.Item != null && l.N > 0) { o.Market.ItemRoster.AddToCounts(l.El, l.N); pcs += l.N; Measure174b.NoteArrival(o.Market, l.El.Item, l.N, Measure174b.ArrGarrison, true); }   // 174b.0 M1 (tylko licznik); poprawka 174b: woz bez partii - przerzut
            _dBack++; _dBackPieces += pcs;
            if (!refund) return;
            var leader = o.PayerClan != null && !o.PayerClan.IsEliminated ? o.PayerClan.Leader : null;
            if (leader == null || !leader.IsAlive || o.Market == null || o.Market.Town == null) { _dBackNoRefund++; return; }
            int r = Math.Min(o.Paid, Math.Max(0, o.Market.Town.Gold));
            if (r <= 0) return;
            o.Market.Town.ChangeGold(-r);
            leader.ChangeHeroGold(r);
            MoneyLedger.Note(MoneyLedger.NGear, o.Market, -r);
            _dRefund += r;
        }

        private static void Flush(int today)
        {
            int inTransit = _orders.Count, inPcs = InTransitPieces();
            if (!On && _dArrived + _dBack + inTransit == 0) { ClearDay(); _stumbles = 0; return; }
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("Zaopatrzenie zamkow (171): dzien ").Append(today).Append(On ? "" : " (WYLACZONE - tylko dowoz oplaconych)").Append(" - zamowien ").Append(_dOrders).Append(" w ").Append(_dTowns.Count)
              .Append(" miastach: ").Append(_dPieces).Append(" szt. za ").Append(_dGold).Append(" zl (kiesy panow -> kasy miast; srednio ")
              .Append(_dOrders > 0 ? ((double)_dPieces / _dOrders).ToString("0.0", inv) : "0").Append(" szt. na zamowienie, na limicie szt. ").Append(_dPieceLimit)
              .Append(", na limicie budzetu ").Append(_dBudgetLimit).Append("); z polki wlasnego zamku ").Append(_dOwnPieces).Append(" szt. za ").Append(_dOwnGold)
              .Append(" zl; w drodze ").Append(inTransit).Append(" zamowien (").Append(inPcs).Append(" szt.); dojechalo ").Append(_dArrived).Append(" (")
              .Append(_dArrivedPieces).Append(" szt., srednio ").Append(_dArrived > 0 ? ((double)_dArrivedDays / _dArrived).ToString("0.0", inv) : "0")
              .Append(" doby drogi); czeka: oblezenie ").Append(_dWaitSiege).Append(", brak zalogi ").Append(_dWaitNoGarrison)
              .Append("; zawrocone ").Append(_dBack).Append(" (").Append(_dBackPieces).Append(" szt., zwrot ").Append(_dRefund).Append(" zl; zamek wrogi ").Append(_dBackHostile)
              .Append(", zamek u obcych po pokoju ").Append(_dBackPeace).Append(", rod wymarly i zamek u obcych ").Append(_dBackForeign).Append(", 30 dob ").Append(_dBack30).Append(", bez odbiorcy zwrotu ").Append(_dBackNoRefund)
              .Append("); bez zamowienia: bez miasta ").Append(_dNoTown).Append(", oblezenie albo wojna ").Append(_dSiegeWar).Append(", bez drogi ").Append(_dNoRoad)
              .Append(", za daleko ").Append(_dTooFar).Append(", przerwa (co ").Append(Math.Max(1, Settings.Current.GarrisonOrderDays)).Append(" doby) ").Append(_dPause)
              .Append("; brak towaru w miescie: ").Append(_dUnmet).Append(" koszykow (zamowienia dla warsztatow); potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
            ClearDay(); _stumbles = 0;
        }

        // ------------------------------------------------------------ zapis: zamek|miasto|rod|frakcja|dobaZam|dobaPrzyj|zaplacone|przedmiot:modyfikator:ile:koszyk;...~
        internal static string Export()
        {
            if (_pending != null) ResolvePending("zapis przed startem sesji");
            var sb = new StringBuilder();
            foreach (var o in _orders)
            {
                if (o.Castle == null || o.Market == null) continue;
                sb.Append(o.Castle.StringId).Append('|').Append(o.Market.StringId).Append('|').Append(o.PayerClan != null ? o.PayerClan.StringId : "")
                  .Append('|').Append(o.FactionAtOrder != null ? o.FactionAtOrder.StringId : "").Append('|').Append(o.DayOrdered).Append('|').Append(o.DayDue)
                  .Append('|').Append(o.Paid).Append('|');
                foreach (var l in o.Lines)
                {
                    if (l.El.Item == null || l.N <= 0) continue;
                    sb.Append(l.El.Item.StringId).Append(':').Append(l.El.ItemModifier != null ? l.El.ItemModifier.StringId : "").Append(':').Append(l.N).Append(':').Append(l.Bucket).Append(';');
                }
                sb.Append('~');
            }
            return sb.ToString();
        }

        /// <summary>Z SyncData: tylko zapamietanie (w SyncData obiekty gry nie sa jeszcze do znalezienia) - rozwiazanie w ResolvePending.</summary>
        internal static void Import(string s)
        {
            _orders.Clear(); _transit.Clear();
            _pending = string.IsNullOrEmpty(s) ? null : s;
        }

        /// <summary>Z OnSessionLaunched (i awaryjnie z Export): zamowienia z zapisu na obiekty gry; _transit od nowa.</summary>
        internal static void ResolvePending(string why)
        {
            var s = _pending;
            _pending = null;
            if (string.IsNullOrEmpty(s)) return;
            var om = MBObjectManager.Instance;
            int recs = 0, ok = 0, lost = 0, noFaction = 0, pcs = 0;
            foreach (var rec in s.Split('~'))
            {
                if (rec.Length == 0) continue;
                try
                {
                    var a = rec.Split('|'); if (a.Length != 8) continue;
                    recs++;
                    Settlement castle = null, market = null;
                    try { castle = om.GetObject<Settlement>(a[0]); market = om.GetObject<Settlement>(a[1]); } catch { }
                    if (castle == null || market == null) { lost++; continue; }
                    Clan payer = null; if (a[2].Length > 0) { try { payer = om.GetObject<Clan>(a[2]); } catch { } }
                    IFaction fac = null;
                    if (a[3].Length > 0)
                    {
                        try { fac = om.GetObject<Kingdom>(a[3]); } catch { }
                        if (fac == null) { try { fac = om.GetObject<Clan>(a[3]); } catch { } }
                    }
                    if (fac == null) { fac = castle.MapFaction; noFaction++; }
                    int d0, d1, paid;
                    int.TryParse(a[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out d0);
                    int.TryParse(a[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out d1);
                    int.TryParse(a[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out paid);
                    var o = new Order { Castle = castle, Market = market, PayerClan = payer, FactionAtOrder = fac, DayOrdered = d0, DayDue = d1, Paid = paid };
                    foreach (var tok in a[7].Split(';'))
                    {
                        if (tok.Length == 0) continue;
                        var p = tok.Split(':'); if (p.Length != 4) continue;
                        ItemObject it = null; try { it = om.GetObject<ItemObject>(p[0]); } catch { }
                        if (it == null) continue;
                        ItemModifier m = null; if (p[1].Length > 0) { try { m = om.GetObject<ItemModifier>(p[1]); } catch { } }
                        int n, b;
                        if (!int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n <= 0) continue;
                        int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out b);
                        o.Lines.Add(new Line { El = new EquipmentElement(it, m), N = n, Bucket = b });
                        pcs += n;
                    }
                    if (o.Lines.Count == 0) { lost++; continue; }
                    _orders.Add(o);
                    AddTransit(o, +1);
                    ok++;
                }
                catch (Exception e) { Stumble("ResolvePending", e); lost++; }
            }
            Log.Info("Zaopatrzenie zamkow (171): z zapisu (" + why + ") " + ok + " zamowien w drodze z " + recs + " (" + pcs + " szt.; odrzucone bez zamku/miasta/towaru " + lost
                     + ", bez frakcji z zamowienia - obecna frakcja zamku " + noFaction + ").");
        }
    }
}
