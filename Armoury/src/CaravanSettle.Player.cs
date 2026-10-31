using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 259 - RAPORT MAJATKU GRACZA (karawany i warsztaty): linia doby "Majatek gracza (karawany i warsztaty) (259): dzien N" (karawana: wodz, rodzaj, kapital,
    /// rozliczenie doby - kiesa, podloga, towar w jukach wedlug kosztu i rynku, wartosc, wyplata; teraz; handel dnia - sprzedaz, myto, zakupy, zysk ze sprzedazy;
    /// zold zaplacony przez rod, doplata BK do 5 000, brak rodu; wyplaty wedlug drogi; dni ponizej kapitalu; od zakupu albo od wczytania; warsztat: kapital,
    /// zysk przed wyplata, wyplata, podatek D11-3), linia przy zniknieciu karawany / warsztatu i komunikaty dla gracza (klucz CaravanReports): wyplata dnia
    /// (PayNotes "caravans" albo ekran), karawana ponizej kapitalu od 7 dob (potem co 7 - rytm tygodnia BK i E3-T1 BlockDays), tydzien majatku.
    /// Zrodla (tylko odczyt i liczniki): prefiks / postfiks DailyTickClan rodu gracza (Priority.First - kiesy przed i po rozliczeniu, zold z rekordow SoldierPay
    /// przed ich wyczyszczeniem (Last), kapital warsztatow przed i po - Workshop.ChangeGold zmienia tylko Capital), IncomePrefix (rozliczenie 259),
    /// CaravanGoods.TradePostfix (sprzedaz, zakup, myto), Seal31B.ProfitPost (BK, E3/9), CaravanCapital.RoutePurse (rozwiazanie), Seal31B.OwnerTaxDaily (D11-3).
    /// Zapis: klucz arm_259_majatek (SaveText) - rekordy karawan i warsztatow od zakupu; brak klucza - od wczytania.
    /// </summary>
    internal static partial class CaravanSettle
    {
        private const string PP = "Majatek gracza (karawany i warsztaty) (259): ";
        private const int LowDays = 7;                 // rytm tygodnia BK (OnWeeklyCaravanLedger) i E3-T1 BlockDays
        private const int Ring = 8;                    // pierscien dob (ostatnie 7 pelnych dob + biezaca)

        private sealed class CarRec
        {
            public string Id; public int FirstDay; public bool FromLoad; public int Capital;
            public long Paid, Wage, Topup, Short, Sold, Bought, Toll, Realized;
            public int LowSince = -1, LastLowMsg = -1;
            // sesja (bez zapisu)
            public string Name = "?", Leader = "-";
            public long DSold, DBought, DToll, DReal, DPay259, DPayBk, DPayE39, DPayEnd, DPayVan, DWage, DTop, DShort;
            public bool HaveCalc, LastBusy, WageGuess, DTick;
            public Calc Last;
            public readonly long[,] Wk = new long[Ring, 5];   // 0 sprzedaz, 1 zakupy, 2 myto, 3 zold rodu, 4 doplata BK
            public readonly int[] WkDay = { -1, -1, -1, -1, -1, -1, -1, -1 };
            // 305a (sesja, bez zapisu): dziennik karawany i przyczyna konca
            public readonly List<string> DTx = new List<string>(); public int DTxDrop;
            public int LastTownDay = -1, LastTradeDay = -1, NoTradeRun, MaxNoTradeRun; public string LastTownName = "-";
            public Hero LeaderRef; public long LastPurse, LastGoods; public string EndCause; public bool EndMsg;
        }

        private sealed class WsRec
        {
            public string Key; public int FirstDay; public bool FromLoad;
            public long Paid, Tax;
            public long DPaid, DTaxDue, DTaxPaid; public int DProfit; public bool DTick;
            public string Name = "?";
            // 305b (WorkshopLedgerCapitalSplit): kapital wlasny gracza w warsztacie (kapital przy pierwszym wpisie + doplaty - zwroty; -1 = jeszcze nie znany),
            // czesc wyplat ponizej niego (zwrot kapitalu, nie zysk), zwroty i doplaty przy zmianie produkcji (WorkshopTrade.RetoolSettle) - razem i dzis
            public long Basis = -1, PaidRet, CapRet, CapAdd, DPaidRet, DCapRet, DCapAdd;
        }

        private static readonly Dictionary<string, CarRec> _cars = new Dictionary<string, CarRec>(StringComparer.Ordinal);
        private static readonly Dictionary<string, WsRec> _wss = new Dictionary<string, WsRec>(StringComparer.Ordinal);
        private static readonly HashSet<string> _atLoad = new HashSet<string>(StringComparer.Ordinal);
        private static bool _atLoadDone;
        private static int _impCars, _impWs, _impBad;
        private static bool _impHad;
        // rozliczenie rodu gracza (miedzy TickPre a TickPost)
        private static bool _inTick;
        private static readonly Dictionary<MobileParty, int> _tickPurse = new Dictionary<MobileParty, int>();
        private static readonly Dictionary<MobileParty, int> _tickPay = new Dictionary<MobileParty, int>();
        private static readonly Dictionary<Workshop, int> _tickCap = new Dictionary<Workshop, int>();
        private static readonly Dictionary<Workshop, int> _tickProfit = new Dictionary<Workshop, int>();
        // tydzien gracza (komunikat tygodniowy): 0 karawany do rodu, 1 zold rodu, 2 doplaty BK, 3 warsztaty do rodu, 4 podatek warsztatow, 5 zwrot kapitalu warsztatow (305b)
        private static readonly long[,] _wk = new long[Ring, 6];
        private static readonly int[] _wkDay = { -1, -1, -1, -1, -1, -1, -1, -1 };

        private static void PlayerReset()
        {
            _cars.Clear(); _wss.Clear(); _atLoad.Clear(); _atLoadDone = false;
            _impCars = _impWs = _impBad = 0; _impHad = false;
            _inTick = false; _tickPurse.Clear(); _tickPay.Clear(); _tickCap.Clear(); _tickProfit.Clear();
            Array.Clear(_wk, 0, _wk.Length); for (int i = 0; i < Ring; i++) _wkDay[i] = -1;
        }

        private static string N(long v) { return v.ToString("N0", CultureInfo.InvariantCulture); }

        private static void WkAdd(long[,] a, int[] days, int col, long v)
        {
            if (v == 0) return;
            int d = Today, i = ((d % Ring) + Ring) % Ring;
            if (days[i] != d) { days[i] = d; for (int k = 0; k < a.GetLength(1); k++) a[i, k] = 0; }   // 305b: tydzien gracza ma 6 kolumn
            a[i, col] += v;
        }

        /// <summary>Suma kolumny z dob [dzis - 7, dzis] i liczba dob z wpisem.</summary>
        private static long WkSum(long[,] a, int[] days, int col, out int n)
        {
            int d = Today; long s = 0; n = 0;
            for (int i = 0; i < Ring; i++) if (days[i] >= d - 7 && days[i] <= d) { s += a[i, col]; n++; }
            return s;
        }

        // ------------------------------------------------------------ rekordy
        /// <summary>Karawany i warsztaty gracza w chwili pierwszego uzycia w sesji (wczytanie) - ich rekord bez klucza zapisu liczy "od wczytania".</summary>
        private static void EnsureAtLoad()
        {
            if (_atLoadDone || Campaign.Current == null) return;
            _atLoadDone = true;
            var pc = Clan.PlayerClan;
            if (pc == null) return;
            foreach (var mp in MobileParty.AllCaravanParties) if (mp != null && mp.ActualClan == pc) _atLoad.Add("c" + mp.StringId);
            var ld = pc.Leader;
            if (ld != null && ld.OwnedWorkshops != null) foreach (var w in ld.OwnedWorkshops) { var k = WsKey(w); if (k != null) _atLoad.Add("w" + k); }
        }

        private static CarRec Car(MobileParty mp, bool create)
        {
            if (mp == null || mp.StringId == null) return null;
            CarRec r;
            if (_cars.TryGetValue(mp.StringId, out r)) return r;
            if (!create) return null;
            EnsureAtLoad();
            r = new CarRec { Id = mp.StringId, FirstDay = Today, FromLoad = _atLoad.Contains("c" + mp.StringId) };
            _cars[mp.StringId] = r;
            return r;
        }

        private static string WsKey(Workshop w)
        {
            var st = w != null ? w.Settlement : null;
            var town = st != null ? st.Town : null;
            if (town == null || town.Workshops == null) return null;
            int i = Array.IndexOf(town.Workshops, w);
            return i < 0 ? null : st.StringId + "#" + i;
        }

        private static WsRec Ws(Workshop w, bool create)
        {
            var k = WsKey(w);
            if (k == null) return null;
            WsRec r;
            if (_wss.TryGetValue(k, out r)) return r;
            if (!create) return null;
            EnsureAtLoad();
            r = new WsRec { Key = k, FirstDay = Today, FromLoad = _atLoad.Contains("w" + k) };
            _wss[k] = r;
            return r;
        }

        // ------------------------------------------------------------ wpisy z innych miejsc (tylko liczniki; kazde we wlasnym try)
        /// <summary>CaravanGoods.TradePostfix (karawana rodu gracza): kind 1 zakup (gold - zaplacone), 2 sprzedaz (gold - utarg przed mytem, toll - myto E3-G3,
        /// costRm - koszt zdjety z ksiegi E3-T1).</summary>
        internal static void NoteTrade(MobileParty mp, int kind, long gold, int toll, double costRm) { NoteTrade(mp, kind, gold, toll, costRm, null, null, 0); }

        /// <summary>305a: to samo z towarem, miastem i liczba sztuk - wpis transakcji do dziennika karawany gracza (klucz CaravanPlayerDiary, tylko log).</summary>
        internal static void NoteTrade(MobileParty mp, int kind, long gold, int toll, double costRm, ItemObject it, Settlement st, int units)
        {
            try
            {
                var r = Car(mp, true);
                if (r == null) return;
                try { Tx305(r, kind, gold, toll, costRm, it, st, units); } catch (Exception e) { Stumble305("Tx305", e); }
                if (kind == 1) { r.DBought += gold; r.Bought += gold; WkAdd(r.Wk, r.WkDay, 1, gold); }
                else
                {
                    long real = (long)Math.Round(gold - toll - costRm);
                    r.DSold += gold; r.Sold += gold; r.DToll += toll; r.Toll += toll; r.DReal += real; r.Realized += real;
                    WkAdd(r.Wk, r.WkDay, 0, gold); WkAdd(r.Wk, r.WkDay, 2, toll);
                }
            }
            catch (Exception e) { Stumble("NoteTrade", e); }
        }

        /// <summary>Wplata karawany rodu gracza poza rozliczeniem 259: DepBk (BK przy wjezdzie), DepE39 (E3/9), DepEnd (kiesa przy rozwiazaniu - E3/7).</summary>
        internal static void NoteDeposit(MobileParty mp, int kind, long amt)
        {
            try
            {
                if (amt <= 0) return;
                var r = Car(mp, true);
                if (r == null) return;
                if (kind == DepBk) r.DPayBk += amt; else if (kind == DepE39) r.DPayE39 += amt; else r.DPayEnd += amt;
                r.Paid += amt;
                if (kind != DepEnd) WkAdd(_wk, _wkDay, 0, amt);
            }
            catch (Exception e) { Stumble("NoteDeposit", e); }
        }

        /// <summary>Seal31B.OwnerTaxDaily (D11-3), warsztat gracza: podatek nalezny i zaplacony.</summary>
        internal static void NoteWsTax(Workshop w, int due, long paid)
        {
            try
            {
                var r = Ws(w, true);
                if (r == null) return;
                r.DTaxDue += Math.Max(0, due); r.DTaxPaid += Math.Max(0, paid); r.Tax += Math.Max(0, paid);
                WkAdd(_wk, _wkDay, 4, Math.Max(0, paid));
            }
            catch (Exception e) { Stumble("NoteWsTax", e); }
        }

        private static bool CapSplit { get { var s = Settings.Current; return s != null && s.WorkshopLedgerCapitalSplit; } }

        /// <summary>305b: WorkshopTrade.RetoolSettle - zwrot kapitalu do kiesy (back) albo doplata z kiesy (added) przy zmianie produkcji warsztatu gracza; capBefore -
        /// kapital przed przelewem (punkt wyjscia kapitalu wlasnego, gdy rekord nowy). Tylko licznik.</summary>
        internal static void NoteWsCapital(Workshop w, long back, long added, int capBefore)
        {
            try
            {
                var r = Ws(w, true);
                if (r == null) return;
                if (r.Basis < 0) r.Basis = Math.Max(0, capBefore);
                if (back > 0) { r.CapRet += back; r.DCapRet += back; r.Basis = Math.Max(0L, r.Basis - back); if (CapSplit) WkAdd(_wk, _wkDay, 5, back); else WkAdd(_wk, _wkDay, 3, back); }   // recenzja 305b: bez podzialu - jak dotad "workshops paid you"
                if (added > 0) { r.CapAdd += added; r.DCapAdd += added; r.Basis += added; }
            }
            catch (Exception e) { Stumble("NoteWsCapital", e); }
        }

        /// <summary>IncomePrefix (z wyplata): rozliczenie 259 karawany rodu gracza - wejscia reguly do linii (K259-2) i kwota do TickPost.</summary>
        private static void PlayerCalc(MobileParty mp, Calc c, int moved, bool busy)
        {
            try
            {
                var r = Car(mp, true);
                if (r == null) return;
                r.Last = c; r.HaveCalc = true; r.LastBusy = busy;
                r.DPay259 += moved; r.Paid += moved;
                WkAdd(_wk, _wkDay, 0, moved);
                if (_inTick) { int p0; _tickPay.TryGetValue(mp, out p0); _tickPay[mp] = p0 + moved; }
            }
            catch (Exception e) { Stumble("PlayerCalc", e); }
        }

        // ------------------------------------------------------------ H5: rozliczenie rodu gracza (DailyTickClan, Priority.First)
        private static List<MobileParty> ClanCaravans(Clan c)
        {
            var list = new List<MobileParty>();
            foreach (var h in c.AliveLords) if (h != null && h.OwnedCaravans != null) foreach (var cp in h.OwnedCaravans) { var mp = cp != null ? cp.MobileParty : null; if (mp != null && mp.IsActive && !Measure31.IsDteConvoy(mp)) list.Add(mp); }
            foreach (var h in c.Companions) if (h != null && h.OwnedCaravans != null) foreach (var cp in h.OwnedCaravans) { var mp = cp != null ? cp.MobileParty : null; if (mp != null && mp.IsActive && !Measure31.IsDteConvoy(mp)) list.Add(mp); }
            return list;
        }

        public static void TickPre(Clan __0)
        {
            _inTick = false;
            try
            {
                if (__0 == null || __0 != Clan.PlayerClan || !Live) return;
                _tickPurse.Clear(); _tickPay.Clear(); _tickCap.Clear(); _tickProfit.Clear();
                foreach (var mp in ClanCaravans(__0)) _tickPurse[mp] = mp.PartyTradeGold;
                var ld = __0.Leader;
                if (ld != null && ld.OwnedWorkshops != null)
                    foreach (var w in ld.OwnedWorkshops) if (w != null) { _tickCap[w] = w.Capital; _tickProfit[w] = w.ProfitMade; }
                _inTick = true;
            }
            catch (Exception e) { _inTick = false; Stumble("TickPre", e); }
        }

        /// <summary>Po ciele gry, przed SoldierPay (Last): zwrot rodu = kiesa po - kiesa przed + wyplata + zold zdjety; zold rodu = min(zold, zwrot),
        /// doplata BK = zwrot ponad zold, brak rodu = zold ponad zwrot. Warsztat: wyplata = kapital przed - kapital po.</summary>
        public static void TickPost(Clan __0)
        {
            if (!_inTick) return;
            _inTick = false;
            try
            {
                if (__0 != Clan.PlayerClan) return;
                foreach (var kv in _tickPurse)
                {
                    try
                    {
                        var mp = kv.Key;
                        if (mp == null || !mp.IsActive) continue;
                        var r = Car(mp, true);
                        if (r == null) continue;
                        int pay;
                        if (!_tickPay.TryGetValue(mp, out pay))
                        {
                            pay = 0;
                            // klucz WYL albo karawana nierozliczana: gra placi 10% kiesy ponad 10 000 (AddIncomeFromParty, gdy BK pozwala) - ten sam wzor
                            if (!Settled(mp) && mp.LeaderHero != __0.Leader && kv.Value > BkKeep && VanillaWould(mp, __0)) { pay = (kv.Value - BkKeep) / 10; r.DPayVan += pay; r.Paid += pay; WkAdd(_wk, _wkDay, 0, pay); }
                        }
                        int wage, paid;
                        bool known = SoldierPay.PaidOf(mp, out wage, out paid);
                        if (!known) paid = Math.Max(0, mp.TotalWage);   // SoldierPay wylaczony - zold z partii (przyblizenie, znacznik w linii)
                        long refund = Math.Max(0L, (long)mp.PartyTradeGold - kv.Value + pay + paid);
                        long cw = Math.Min(paid, refund), top = Math.Max(0L, refund - paid), sh = Math.Max(0L, paid - refund);
                        r.DWage += cw; r.Wage += cw; r.DTop += top; r.Topup += top; r.DShort += sh; r.Short += sh;
                        r.WageGuess |= !known; r.DTick = true;
                        WkAdd(r.Wk, r.WkDay, 3, cw); WkAdd(r.Wk, r.WkDay, 4, top);
                        WkAdd(_wk, _wkDay, 1, cw); WkAdd(_wk, _wkDay, 2, top);
                    }
                    catch (Exception e) { Stumble("TickPost(karawana)", e); }
                }
                foreach (var kv in _tickCap)
                {
                    try
                    {
                        var w = kv.Key;
                        if (w == null) continue;
                        var wr = Ws(w, true);
                        if (wr == null) continue;
                        long payout = Math.Max(0, kv.Value - w.Capital);
                        int prof; _tickProfit.TryGetValue(w, out prof);
                        wr.DProfit = prof; wr.DPaid += payout; wr.Paid += payout; wr.DTick = true;
                        // 305b (WorkshopLedgerCapitalSplit): wyplata, ktora zbija kapital ponizej kapitalu wlasnego gracza, to zwrot jego pieniedzy, nie zysk
                        if (wr.Basis < 0) wr.Basis = Math.Max(0, kv.Value);
                        long ret = Math.Max(0L, Math.Min(payout, Math.Max(0L, wr.Basis - w.Capital) - Math.Max(0L, wr.Basis - kv.Value)));
                        if (ret > 0) { wr.PaidRet += ret; wr.DPaidRet += ret; wr.Basis -= ret; }
                        if (CapSplit) { WkAdd(_wk, _wkDay, 3, payout - ret); WkAdd(_wk, _wkDay, 5, ret); }
                        else WkAdd(_wk, _wkDay, 3, payout);
                    }
                    catch (Exception e) { Stumble("TickPost(warsztat)", e); }
                }
            }
            catch (Exception e) { Stumble("TickPost", e); }
            finally { _tickPurse.Clear(); _tickPay.Clear(); _tickCap.Clear(); _tickProfit.Clear(); }
        }

        // ------------------------------------------------------------ linia doby gracza i komunikaty
        private static string Kind(MobileParty mp)
        {
            var cpc = mp.CaravanPartyComponent;
            bool naval = cpc != null && cpc.CanHaveNavalNavigationCapability, elite = cpc != null && cpc.IsElite;
            return naval ? (elite ? "morska elitarna" : "morska") : (elite ? "elitarna" : "zwykla");
        }

        private static string NameOf(MobileParty mp) { try { return mp.Name != null ? mp.Name.ToString() : mp.StringId; } catch { return mp.StringId; } }

        private static string Since(int firstDay, bool fromLoad, int day)
        {
            return (fromLoad ? "od wczytania 259" : "od zakupu") + " (dzien " + firstDay + ", " + Math.Max(0, day - firstDay + 1) + " dob)";
        }

        private static string Totals(CarRec r)
        {
            return "wyplaty " + r.Paid + ", zold rodu " + r.Wage + ", doplaty " + r.Topup + ", brak rodu " + r.Short + ", sprzedaz " + r.Sold + ", zakupy " + r.Bought
                   + ", myto " + r.Toll + ", zysk ze sprzedazy " + r.Realized + ", netto dla rodu " + (r.Paid - r.Wage - r.Topup);
        }

        private static void PlayerDaily(Settings s, int day)
        {
            var pc = Clan.PlayerClan;
            if (pc == null || !Live) return;
            EnsureAtLoad();
            int st0 = _sSt;
            var cur = new List<MobileParty>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mp in MobileParty.AllCaravanParties)
                if (mp != null && mp.IsActive && mp.ActualClan == pc && !Measure31.IsDteConvoy(mp) && mp.StringId != null) { cur.Add(mp); ids.Add(mp.StringId); }
            // karawany, ktorych juz nie ma (rozbite, rozwiazane, sprzedane) - linia i koniec rekordu
            var gone = new List<CarRec>();
            foreach (var r in _cars.Values) if (!ids.Contains(r.Id)) gone.Add(r);
            foreach (var r in gone)
            {
                Log.Info(PP + "karawana '" + r.Name + "' zakonczona (" + (r.DPayEnd > 0 ? "rozwiazana - kiesa do rodu " + r.DPayEnd : "rozbita albo zniknela") + ") | "
                         + Since(r.FirstDay, r.FromLoad, day) + ": " + Totals(r) + EndText305(r) + ".");   // 305a: przyczyna konca i komunikat (rozwiazana / zniknela)
                _cars.Remove(r.Id);
            }
            var ld = pc.Leader;
            var wsNow = new List<Workshop>();
            var wkeys = new HashSet<string>(StringComparer.Ordinal);
            if (ld != null && ld.OwnedWorkshops != null) foreach (var w in ld.OwnedWorkshops) { var k = WsKey(w); if (k != null) { wsNow.Add(w); wkeys.Add(k); } }
            var wgone = new List<WsRec>();
            foreach (var r in _wss.Values) if (!wkeys.Contains(r.Key)) wgone.Add(r);
            foreach (var r in wgone)
            {
                Log.Info(PP + "warsztat '" + r.Name + "' zakonczony (sprzedany albo utracony) | " + Since(r.FirstDay, r.FromLoad, day) + ": wyplaty " + r.Paid + ", podatek " + r.Tax + WsCapText(r) + ".");
                _wss.Remove(r.Key);
            }
            if (cur.Count == 0 && wsNow.Count == 0) return;

            var sb = new StringBuilder(2048);
            sb.Append(PP).Append("dzien ").Append(day);
            long t259 = 0, tBk = 0, tE39 = 0, tEnd = 0, tVan = 0, tWage = 0, tTop = 0, tShort = 0, tWs = 0, tTax = 0, tWsRet = 0;
            var pays = new List<string>(); var caps = new SortedSet<int>();
            int lowMsgs = 0;
            bool rep = s.CaravanReports;
            foreach (var mp in cur)
            {
                try
                {
                    var r = Car(mp, true);
                    r.Name = NameOf(mp);
                    r.Leader = mp.LeaderHero != null && mp.LeaderHero.Name != null ? mp.LeaderHero.Name.ToString() : "bez wodza";
                    var owner = mp.Owner;
                    Calc now = owner != null ? Compute(mp, owner) : new Calc();
                    if (owner != null) r.Capital = now.Capital;
                    var basis = r.HaveCalc ? r.Last : now;
                    bool settled = Settled(mp);
                    int low = 0;
                    // recenzja 259: dni ponizej kapitalu i komunikat tylko dla karawany rozliczanej przez rod (bez rozliczenia placa BK i E3/9 - komunikat bylby nieprawda)
                    if (settled && basis.Capital > 0 && basis.Worth < basis.Capital) { if (r.LowSince < 0) r.LowSince = day; low = day - r.LowSince + 1; }
                    else { r.LowSince = -1; r.LastLowMsg = -1; }
                    sb.Append(" || karawana '").Append(r.Name).Append("' (wodz ").Append(r.Leader).Append(", ").Append(Kind(mp)).Append(", kapital ").Append(r.Capital)
                      .Append(owner == null ? "" : now.CapE37 ? " (E3/7: dane " + now.Funded + ", model " + now.CapModel + ")" : " (model " + now.CapModel + ")")
                      .Append(", ").Append(settled ? "rozliczana przez rod" : "NIE rozliczana przez rod").Append(')');
                    if (r.HaveCalc)
                    {
                        var c = r.Last;
                        sb.Append(" | rozliczenie doby: kiesa ").Append(c.Purse).Append(" (podloga ").Append(c.Floor).Append(c.NoRefund ? ", bez zwrotu zoldu" : "").Append("), towar w jukach ").Append(c.Goods)
                          .Append(" (koszt ").Append(c.Cost).Append(", rynek ").Append(c.Market).Append(", ").Append(c.Kinds).Append(" rodzajow, ").Append(c.Units).Append(" szt., w tym zwierzeta i inne ")
                          .Append(c.Other).Append("), wartosc ").Append(c.Worth)
                          .Append(", kapital ").Append(c.Capital).Append(", wyplata ").Append(c.Payout).Append(r.LastBusy ? " (w bitwie albo rozwiazywana)" : "");
                    }
                    else sb.Append(" | rozliczenie doby: brak (rod gracza nie rozliczony od ostatniej linii albo klucz WYL)");
                    sb.Append(" | teraz: kiesa ").Append(now.Purse).Append(", towar ").Append(now.Goods).Append(", wartosc ").Append(now.Worth)
                      .Append(" | dzis: sprzedaz ").Append(r.DSold).Append(" (myto ").Append(r.DToll).Append("), zakupy ").Append(r.DBought).Append(", zysk ze sprzedazy ").Append(r.DReal)
                      .Append(" | zold ").Append(r.DWage).Append(" zaplacil rod, doplata BK ").Append(r.DTop).Append(", brak rodu ").Append(r.DShort)
                      .Append(r.WageGuess ? " (zold z partii - SoldierPay bez rekordu)" : "").Append(r.DTick ? "" : " (bez rozliczenia rodu)")
                      .Append(" | wyplaty dzis: rozliczenie 259 ").Append(r.DPay259).Append(", BK ").Append(r.DPayBk).Append(", E3/9 ").Append(r.DPayE39).Append(", gra 10% ").Append(r.DPayVan)
                      .Append(", rozwiazanie ").Append(r.DPayEnd)
                      .Append(" | ponizej kapitalu ").Append(low).Append(" dob")
                      .Append(" | ").Append(Since(r.FirstDay, r.FromLoad, day)).Append(": ").Append(Totals(r));
                    try { DiaryLine(sb, mp, r, now, day); } catch (Exception e) { Stumble305("DiaryLine", e); }   // 305a: gdzie / cel / zachowanie / transakcje dnia
                    t259 += r.DPay259; tBk += r.DPayBk; tE39 += r.DPayE39; tEnd += r.DPayEnd; tVan += r.DPayVan; tWage += r.DWage; tTop += r.DTop; tShort += r.DShort;
                    if (r.DPay259 > 0) { pays.Add(r.Name + " " + N(r.DPay259)); caps.Add(basis.Capital); }
                    if (rep && low >= LowDays && (r.LastLowMsg < 0 || day - r.LastLowMsg >= LowDays))
                    {
                        r.LastLowMsg = day; lowMsgs++;
                        int nd;
                        long sold = WkSum(r.Wk, r.WkDay, 0, out nd), bought = WkSum(r.Wk, r.WkDay, 1, out nd), toll = WkSum(r.Wk, r.WkDay, 2, out nd);
                        long wg = WkSum(r.Wk, r.WkDay, 3, out nd), tp = WkSum(r.Wk, r.WkDay, 4, out nd);
                        int days = 0; for (int i = 0; i < Ring; i++) if (r.WkDay[i] >= Today - 7) days++;
                        string span = days >= 7 ? "Last 7 days" : "Since the game was loaded";
                        // recenzja 259: kapital - to, co karawana dostala (E3/7) albo kapital modelu; towar - nizsza z ceny zakupu i rynku; warunek wyplaty - oba warunki reguly
                        string capTxt = basis.CapE37 ? "the " + N(basis.Capital) + " it was given to trade with" : "its trading capital of " + N(basis.Capital);
                        string txt = r.Name + " is worth " + N(basis.Worth) + " (coin " + N(basis.Purse) + ", goods " + N(basis.Goods) + " valued at the lower of what it paid and the market) - less than "
                                     + capTxt + ". " + span + " it sold " + N(sold) + ", bought " + N(bought) + ", paid " + N(toll) + " in tolls; your clan paid its wages, "
                                     + N(wg) + ", and topped up its purse with " + N(tp) + ". It sends you profit only once its coin and goods are worth more than " + N(basis.Capital)
                                     + " and it holds more than " + N(basis.Floor) + " in coin for its wages.";
                        Log.Player(txt);
                        Log.Info(PP + "komunikat - ponizej kapitalu: karawana '" + r.Name + "' ponizej kapitalu " + low + " dob | " + txt);
                    }
                    r.DSold = r.DBought = r.DToll = r.DReal = r.DPay259 = r.DPayBk = r.DPayE39 = r.DPayEnd = r.DPayVan = r.DWage = r.DTop = r.DShort = 0;
                    r.HaveCalc = false; r.LastBusy = false; r.WageGuess = false; r.DTick = false;
                }
                catch (Exception e) { Stumble("PlayerDaily(karawana)", e); }
            }
            bool bkWs = false;
            try { bkWs = Seal31B.BkWorkshopModelActive(); } catch { }
            foreach (var w in wsNow)
            {
                try
                {
                    var r = Ws(w, true);
                    r.Name = (w.WorkshopType != null && w.WorkshopType.Name != null ? w.WorkshopType.Name.ToString() : "?");
                    string town = w.Settlement != null && w.Settlement.Name != null ? w.Settlement.Name.ToString() : "?";
                    sb.Append(" || warsztat '").Append(r.Name).Append("' (").Append(town).Append(", glowa rodu) | kapital ").Append(w.Capital).Append(" (poczatkowy ").Append(w.InitialCapital)
                      .Append("), zysk przed wyplata ").Append(r.DTick ? r.DProfit.ToString(Inv) : "-").Append(", wyplata ").Append(r.DPaid)
                      .Append(", podatek BK (D11-3) nalezny ").Append(r.DTaxDue).Append(" zaplacony ").Append(r.DTaxPaid)
                      .Append(" | ").Append(Since(r.FirstDay, r.FromLoad, day)).Append(": wyplaty ").Append(r.Paid).Append(", podatek ").Append(r.Tax).Append(WsCapText(r))
                      .Append(" | dzis 305b: wyplata zysku ").Append(r.DPaid - r.DPaidRet).Append(", zwrot kapitalu w wyplacie ").Append(r.DPaidRet)
                      .Append(", zwrot przy zmianie produkcji ").Append(r.DCapRet).Append(", doplata kapitalu ").Append(r.DCapAdd);
                    tWs += r.DPaid; tTax += r.DTaxPaid; tWsRet += r.DPaidRet + r.DCapRet;
                    r.DPaid = r.DTaxDue = r.DTaxPaid = 0; r.DProfit = 0; r.DTick = false;
                    r.DPaidRet = r.DCapRet = r.DCapAdd = 0;
                }
                catch (Exception e) { Stumble("PlayerDaily(warsztat)", e); }
            }
            // komunikat wyplaty dnia (rozliczenie 259)
            string payMsg = t259 <= 0 ? "-" : (!rep ? "wylaczone" : "brak");
            if (rep && t259 > 0)
            {
                try
                {
                    string keep = caps.Count == 1 ? "They keep " + N(caps.Min) + " in coin and goods to trade with."
                                                  : "Each keeps what you put in (" + string.Join(" / ", Fmt(caps)) + ") in coin and goods to trade with.";
                    string why = ": your caravans sent you their profit - " + string.Join(", ", pays.ToArray()) + ". " + keep;
                    if (PayNotes.On) { PayNotes.Got("caravans", (int)Math.Min(int.MaxValue, t259), why); payMsg = "PayNotes"; }
                    else { Log.Player("Your caravans sent you " + N(t259) + " denars of profit" + why.Substring(why.IndexOf(" - ", StringComparison.Ordinal))); payMsg = "ekran"; }
                    Log.Info(PP + "komunikat - wyplata (" + payMsg + ") " + t259 + " | " + string.Join(", ", pays.ToArray()) + ".");
                }
                catch (Exception e) { Stumble("PlayerDaily(komunikat)", e); }
            }
            // tydzien majatku (co 7 dob gry)
            bool week = false;
            if (rep && Today % 7 == 0)
            {
                try
                {
                    int n;
                    long car = WkSum(_wk, _wkDay, 0, out n), wg = WkSum(_wk, _wkDay, 1, out n), tp = WkSum(_wk, _wkDay, 2, out n), wsp = WkSum(_wk, _wkDay, 3, out n), tx = WkSum(_wk, _wkDay, 4, out n);
                    long wsr = WkSum(_wk, _wkDay, 5, out n);   // 305b: zwrot kapitalu warsztatow (0 przy wylaczonym kluczu)
                    string txt = "Your caravans and workshops, last 7 days: caravans sent you " + N(car) + "; your clan paid their wages " + N(wg) + " and " + N(tp)
                                 + " to top up their purses; workshops paid you " + N(wsp) + (CapSplit ? " in profit" : "") + (wsr > 0 ? " and gave back " + N(wsr) + " of your own working capital" : "")
                                 + " and you paid " + N(tx) + " in workshop tax.";
                    Log.Player(txt);
                    Log.Info(PP + "komunikat - tydzien | " + txt);
                    week = true;
                }
                catch (Exception e) { Stumble("PlayerDaily(tydzien)", e); }
            }
            sb.Append(" || razem dzis: karawany do rodu ").Append(t259 + tBk + tE39 + tVan + tEnd).Append(" (rozliczenie 259 ").Append(t259).Append(", BK ").Append(tBk).Append(", E3/9 ").Append(tE39)
              .Append(", gra 10% ").Append(tVan).Append(", rozwiazanie ").Append(tEnd).Append("), zold rodu ").Append(tWage).Append(", doplaty BK ").Append(tTop).Append(", brak rodu ").Append(tShort)
              .Append(", warsztaty do rodu ").Append(tWs).Append(" (305b: w tym zwrot kapitalu ").Append(tWsRet).Append(", klucz podzialu ").Append(CapSplit ? "TAK" : "NIE").Append(")").Append(", podatek warsztatow ").Append(tTax)
              .Append(" | komunikaty: wyplata ").Append(payMsg).Append(", ponizej kapitalu ").Append(lowMsgs).Append(", tydzien ").Append(week ? "TAK" : "NIE")
              .Append(" | BK Workshop taxes w modelu warsztatow ").Append(bkWs ? "TAK (pobiera BK w rozliczeniu rodu - poza ta linia)" : "NIE")
              .Append(" | potkniecia ").Append(_sSt - st0).Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>305b: czesc linii warsztatu - zysk i zwrot kapitalu rozdzielone (kapital wlasny, zwroty w wyplatach i przy zmianie produkcji, doplaty).</summary>
        private static string WsCapText(WsRec r)
        {
            return " (305b: z tego wyplaty zysku " + (r.Paid - r.PaidRet) + ", zwrot kapitalu w wyplatach " + r.PaidRet + "; zwrot kapitalu przy zmianie produkcji " + r.CapRet
                   + ", doplata kapitalu " + r.CapAdd + ", kapital wlasny w warsztacie " + (r.Basis >= 0 ? r.Basis.ToString(Inv) : "-") + ")";
        }

        private static string[] Fmt(SortedSet<int> caps)
        {
            var a = new List<string>();
            foreach (var c in caps) a.Add(N(c));
            return a.ToArray();
        }

        // ------------------------------------------------------------ zapis (arm_259_majatek)
        internal static string Export()
        {
            var sb = new StringBuilder(512);
            foreach (var r in _cars.Values)
                sb.Append("c|").Append(r.Id).Append('|').Append(r.FirstDay).Append('|').Append(r.Capital).Append('|').Append(r.Paid).Append('|').Append(r.Wage).Append('|')
                  .Append(r.Topup).Append('|').Append(r.Short).Append('|').Append(r.Sold).Append('|').Append(r.Bought).Append('|').Append(r.Toll).Append('|').Append(r.Realized).Append('|')
                  .Append(r.LowSince).Append('|').Append(r.LastLowMsg).Append('|').Append(r.FromLoad ? 1 : 0).Append('\n');
            foreach (var w in _wss.Values)
                sb.Append("w|").Append(w.Key).Append('|').Append(w.FirstDay).Append('|').Append(w.Paid).Append('|').Append(w.Tax).Append('|').Append(w.FromLoad ? 1 : 0).Append('\n');
            // 305b: kapital wlasny i zwroty w osobnym wierszu (stary format "w" bez zmian - starsza DLL wiersz "wb" odrzuci jako zly, reszta zostaje)
            foreach (var w in _wss.Values)
                sb.Append("wb|").Append(w.Key).Append('|').Append(w.Basis).Append('|').Append(w.PaidRet).Append('|').Append(w.CapRet).Append('|').Append(w.CapAdd).Append('\n');
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _cars.Clear(); _wss.Clear();
            _impCars = _impWs = _impBad = 0;
            _impHad = s != null;
            if (string.IsNullOrEmpty(s)) return;
            foreach (var line in s.Split('\n'))
            {
                if (line.Length == 0) continue;
                try
                {
                    var p = line.Split('|');
                    if (p[0] == "c" && p.Length == 15)
                    {
                        var r = new CarRec { Id = p[1] };
                        r.FirstDay = I(p[2]); r.Capital = I(p[3]); r.Paid = L(p[4]); r.Wage = L(p[5]); r.Topup = L(p[6]); r.Short = L(p[7]); r.Sold = L(p[8]); r.Bought = L(p[9]);
                        r.Toll = L(p[10]); r.Realized = L(p[11]); r.LowSince = I(p[12]); r.LastLowMsg = I(p[13]); r.FromLoad = I(p[14]) != 0;
                        if (r.Id.Length == 0) throw new FormatException("id");
                        _cars[r.Id] = r; _impCars++;
                    }
                    else if (p[0] == "w" && p.Length == 6)
                    {
                        var w = new WsRec { Key = p[1] };
                        w.FirstDay = I(p[2]); w.Paid = L(p[3]); w.Tax = L(p[4]); w.FromLoad = I(p[5]) != 0;
                        if (w.Key.Length == 0) throw new FormatException("klucz");
                        _wss[w.Key] = w; _impWs++;
                    }
                    else if (p[0] == "wb" && p.Length == 6)
                    {
                        WsRec w;
                        if (_wss.TryGetValue(p[1], out w)) { w.Basis = L(p[2]); w.PaidRet = L(p[3]); w.CapRet = L(p[4]); w.CapAdd = L(p[5]); }
                    }
                    else _impBad++;
                }
                catch { _impBad++; }
            }
        }

        private static int I(string v) { return int.Parse(v, NumberStyles.Integer, CultureInfo.InvariantCulture); }
        private static long L(string v) { return long.Parse(v, NumberStyles.Integer, CultureInfo.InvariantCulture); }
    }
}
