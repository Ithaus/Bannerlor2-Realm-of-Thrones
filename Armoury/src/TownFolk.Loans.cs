using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// obieg2 (3b) - POZYCZKI OD LUDU (Jeff 15.10: "pamietaj o pozyczkach - lordowie moga pozyczac kase od bankow i od mieszczan"). Klucz FolkLoans.
    ///  - DOWOD (Bank, test 4 lata wersja 54, linie "Obieg" i "Dlugi (168)"): AI pozycza w Banku tylko kredytem wojennym 168 (sciezka IronBank "pozyczki" przy drabinie
    ///    dlugu wylaczona - 0 pozyczek w 4 latach); KW wyplacil 6-17 tys./d u 9-25 rodow, dlug w Banku 0.34-0.63 mln przy kapitale 5.0-5.5 mln; bez KW: na limicie
    ///    (15 x ziemia D + 10 000 / 5 000 za miasto / zamek) 22-113 rodow dziennie. Lud w tym czasie trzymal 70 mln ponad poziom normalny.
    ///  - KTO I KIEDY (wpiecie ClanBudget.Daily, PRZED kredytem wojennym Banku): rod AI krolestwa (nie gracz, nie najemnik, nie Straz bez zoldu) w wojnie, z kiesa
    ///    rodziny G ponizej rezerwy R, bez zaleglosci / zajecia w drabinie 168 i bez zakazu po niesplacie. Miejsce w pulapie zoldu = najwyzej FolkLoanMaxShareOfIncome
    ///    (0.40) x D na dobe, najwyzej do limitu FolkLoanLimitDays (30) x D + 20 000 za miasto + 10 000 za zamek, najwyzej tyle, ile lud wlasnych lenn ma ponad poziom
    ///    normalny (miasta - kiesa W ponad FolkHoardNormalTimes x zapas dni W, zamki - podzamcze, wsie lenn - kiesa wsi ponad poziom normalny wsi; bez osad oblezonych
    ///    i wsi w rabunku). Wyplata = brak do zoldu ponad pulap bez kredytu (jak KW), z najwiekszych nadwyzek; kredyt wojenny Banku dostaje reszte braku.
    ///  - PRZELEW: kiesa ludu (W miasta / podzamcze / kiesa wsi) -> glowa rodu (ChangeHeroGold); dlug zapisany na osade, z ktorej poszlo zloto (rod + osada).
    ///  - ODSETKI FolkLoanRateYear (12%) rocznie (364 doby), doliczane codziennie. RATA (DailyLive, przed miastami): w wojnie tylko odsetki (niezaplacone doliczaja
    ///    sie do dlugu); w pokoju odsetki + max(FolkLoanRepayShareOfIncome (10%) x D, dlug / 364). Glowa placi z kiesy, ile ma; zloto wraca do kies osad wedlug
    ///    dlugu (miasto - klasa W, zamek - podzamcze, wies - kiesa wsi; kiesa ludu wylaczona - kasa osady).
    ///  - NIESPLATA: w pokoju FolkLoanDefaultDays (90) dob z rzedu bez pelnej raty - reszta dlugu przepada (umorzenie), kazde miasto i zamek tego rodu, ktore pozyczylo
    ///    (wies - przez osade macierzysta), traci 10 lojalnosci, rod bez pozyczek od ludu przez 182 doby. Rod wymarly / bez glowy - dlug przepada bez kary.
    ///  - Nic z niczego: pozyczka i splata to przelewy miedzy posiadaczami "Pieniadz swiata"; umorzenie nie rusza zlota. Ksiega kies: _ctlOut / _ctlIn (kontrola
    ///    zamknieta r = 0) i LDayOut / LDayIn (kiesa-ludu.csv). Zapis arm_obieg2 (dlugi, zakazy, stan straznika). Linia "Pozyczki od ludu (obieg2): dzien N".
    /// Wyjatek per rod / osada - licznik potkniec (CLAUDE.md 7), przelew cofany do kiesy.
    /// </summary>
    internal static partial class TownFolk
    {
        private sealed class LnPart { public string St = ""; public int Kind; public double Left; }   // Kind: 0 miasto (W), 1 zamek (podzamcze), 2 wies
        private sealed class LnClan { public readonly List<LnPart> Parts = new List<LnPart>(); public int Missed, Day0 = -1; public double Lent, Repaid, Interest; }
        private sealed class LnSrc { public Settlement St; public Folk F; public Village V; public int Kind; public double Ex; }

        private static readonly Dictionary<string, LnClan> _ln = new Dictionary<string, LnClan>();
        private static readonly Dictionary<string, int> _lnBan = new Dictionary<string, int>();
        private static readonly CultureInfo LnInv = CultureInfo.InvariantCulture;
        private const int LnPerTown = 20000, LnPerCastle = 10000, LnBanDays = 182;
        private const float LnDefaultLoyalty = 10f;
        private static string _lineLoans;
        // ---- okno pozyczek (od DailyLive do DailyLive - wyplaty w ClanBudget.Daily po dobie ludu)
        private static long _lnLentAcc, _lnLentT, _lnLentC, _lnLentV; private static int _lnLentN, _lnCand, _lnRoomN, _lnBlkBan, _lnBlkLadder, _lnBlkLimit, _lnBlkNoSurplus;
        private static double _lnRoom;
        private static readonly HashSet<string> _lnLentClans = new HashSet<string>();
        // ---- doba splat (LoansDay)
        private static long _lnRepaid, _lnRepT, _lnRepC, _lnRepV, _lnRepPurse, _lnWritten, _lnLost; private static double _lnInterest, _lnDebtPrev = double.NaN, _lnWrittenD, _lnLostD, _lnDust;
        private static int _lnMissedN, _lnDefaultN, _lnLoyN, _lnLostN, _lnStumbles, _lnStumbleSess;
        /// <summary>"Obieg": pozyczki od ludu wyplacone od poczatku tej doby ludu (ClanBudget po dobie ludu) i splaty tej doby (DailyLive).</summary>
        internal static long LastLoanLent { get { return _lnLentAcc; } }
        internal static long LastLoanRepaid;

        private static bool LoansOn { get { var s = Settings.Current; return s != null && s.FolkLoans && Live; } }
        private static double LnRate(Settings s) { return HdClamp(s.FolkLoanRateYear, 0, 0.5); }
        private static double LnShare(Settings s) { return HdClamp(s.FolkLoanMaxShareOfIncome, 0, 1); }
        private static double LnDays(Settings s) { return HdClamp(s.FolkLoanLimitDays, 0, 120); }
        private static double LnRepay(Settings s) { return HdClamp(s.FolkLoanRepayShareOfIncome, 0, 0.5); }
        private static int LnDefault(Settings s) { return Math.Max(7, Math.Min(364, s.FolkLoanDefaultDays)); }

        private static void ResetLoans()
        {
            _ln.Clear(); _lnBan.Clear(); _lineLoans = null;
            LoansWinZero(); LoansDayZero();
            _lnDebtPrev = double.NaN; _lnStumbleSess = 0;
        }

        private static void LoansWinZero()
        {
            _lnLentAcc = _lnLentT = _lnLentC = _lnLentV = 0; _lnLentN = _lnCand = _lnRoomN = _lnBlkBan = _lnBlkLadder = _lnBlkLimit = _lnBlkNoSurplus = 0; _lnRoom = 0;
            _lnLentClans.Clear();
        }

        private static void LoansDayZero()
        {
            _lnRepaid = _lnRepT = _lnRepC = _lnRepV = _lnRepPurse = _lnWritten = _lnLost = 0; _lnInterest = _lnWrittenD = _lnLostD = _lnDust = 0;
            _lnMissedN = _lnDefaultN = _lnLoyN = _lnLostN = _lnStumbles = 0;
            LastLoanRepaid = 0;
        }

        private static void LnStumble(string where, Exception e) { _lnStumbles++; _lnStumbleSess++; Stumble("Loans." + where, e); }

        private static LnClan LnOf(Clan c, bool create)
        {
            if (c == null || c.StringId == null) return null;
            LnClan l;
            if (_ln.TryGetValue(c.StringId, out l) || !create) return l;
            l = new LnClan();
            _ln[c.StringId] = l;
            return l;
        }

        private static double LnDebt(LnClan l) { double t = 0; if (l != null) foreach (var p in l.Parts) t += Math.Max(0, p.Left); return t; }

        private static bool LnAtWar(Clan c)
        {
            try
            {
                var f = c != null ? c.MapFaction : null;
                if (f == null) return false;
                var list = f.FactionsAtWarWith;
                if (list != null) for (int i = 0; i < list.Count; i++) { var o = list[i]; if (o != null && o.IsKingdomFaction) return true; }
            }
            catch { }
            return false;
        }

        /// <summary>Nadwyzka ludu wlasnych lenn rodu ponad poziom normalny (miasta - klasa W, zamki - podzamcze, wsie lenn). `into` - zrodla (null - tylko suma).</summary>
        private static double LnAvail(Clan c, List<LnSrc> into, out int towns, out int castles)
        {
            towns = castles = 0;
            double sum = 0;
            var s = Settings.Current;
            double times = HoardTimes(s), rdW = Math.Max(1, Math.Min(60, s.TownFolkReserveDays));
            double pph = Math.Max(0f, Math.Min(1f, s.VillagePurchasePerHead));
            var fiefs = c.Fiefs;
            if (fiefs == null) return 0;
            for (int i = 0; i < fiefs.Count; i++)
            {
                var t = fiefs[i];
                var st = t != null ? t.Settlement : null;
                if (st == null) continue;
                if (st.IsTown) towns++; else if (st.IsCastle) castles++;
                if (st.IsUnderSiege) continue;
                Folk f;
                if (_f.TryGetValue(st, out f) && f != null && f.Need[0] > 0 && (f.Kind == 0 || (f.Kind == 1 && CastleLive)))
                {
                    double ex = f.KW - times * (f.Kind == 0 ? rdW : RdC) * f.Need[0];
                    if (ex >= 1) { sum += ex; if (into != null) into.Add(new LnSrc { St = st, F = f, Kind = f.Kind, Ex = ex }); }
                }
                var bv = st.BoundVillages;
                if (bv == null) continue;
                foreach (var v in bv)
                {
                    if (v == null || v.Settlement == null || v.VillageState != Village.VillageStates.Normal) continue;
                    Folk vf;
                    if (!_f.TryGetValue(v.Settlement, out vf) || vf == null || !vf.Cal) continue;
                    double nv = pph * VillHeads(vf, true);
                    if (!(nv > 0)) continue;
                    double ex = v.Gold - times * RdV * nv;
                    if (ex >= 1) { sum += ex; if (into != null) into.Add(new LnSrc { St = v.Settlement, V = v, Kind = 2, Ex = ex }); }
                }
            }
            return sum;
        }

        /// <summary>
        /// ClanBudget.Daily (krok kredytu wojennego, przed Bankiem): miejsce pozyczki od ludu w pulapie zoldu rodu dzis (0 - rod nie pozycza). D, G, R, war - z budzetu
        /// 166 tej doby. Nie rzuca.
        /// </summary>
        internal static double FolkLoanRoom(Clan c, double D, double G, double R, bool war)
        {
            if (!LoansOn || c == null || c == Clan.PlayerClan || c.Leader == null || c.Kingdom == null) return 0;
            try
            {
                if (!war || !(D > 0) || G >= R) return 0;
                _lnCand++;
                int until;
                if (c.StringId != null && _lnBan.TryGetValue(c.StringId, out until) && (int)CampaignTime.Now.ToDays < until) { _lnBlkBan++; return 0; }
                if (DebtLadder.FolkLoanBlocked(c)) { _lnBlkLadder++; return 0; }
                var s = Settings.Current;
                int towns, castles;
                double avail = LnAvail(c, null, out towns, out castles);
                double limit = LnDays(s) * D + LnPerTown * towns + LnPerCastle * castles;
                double room = Math.Min(LnShare(s) * D, limit - LnDebt(LnOf(c, false)));
                if (room < 1) { _lnBlkLimit++; return 0; }
                if (avail < 1) { _lnBlkNoSurplus++; return 0; }
                room = Math.Floor(Math.Min(room, avail));
                if (room < 1) return 0;
                _lnRoomN++; _lnRoom += room;
                return room;
            }
            catch (Exception e) { LnStumble("FolkLoanRoom", e); return 0; }
        }

        /// <summary>ClanBudget.Daily: wyplata pozyczki od ludu - najwyzej `amount` z najwiekszych nadwyzek wlasnych lenn do kiesy glowy rodu. Zwraca wyplacone. Nie rzuca.</summary>
        internal static int FolkLoanLend(Clan c, double amount)
        {
            if (!LoansOn || c == null || c.Leader == null || !(amount >= 1)) return 0;
            long got = 0;
            try
            {
                var src = new List<LnSrc>();
                int towns, castles;
                LnAvail(c, src, out towns, out castles);
                src.Sort((a, b) => b.Ex.CompareTo(a.Ex));
                long left = (long)Math.Floor(Math.Min(amount, int.MaxValue));
                var lc = LnOf(c, true);
                var lord = c.Leader;
                foreach (var x in src)
                {
                    if (left <= 0) break;
                    long want = Math.Min(left, (long)Math.Floor(x.Ex));
                    if (want <= 0) continue;
                    long t = LnTake(x, want);
                    if (t <= 0) continue;
                    try { lord.ChangeHeroGold((int)t); }
                    catch (Exception e) { if (LnPut(x.St, x.Kind, t, false) == 0) LnStumble("FolkLoanLend(zwrot)", e); LnStumble("FolkLoanLend(glowa)", e); continue; }
                    CirculationWindows.NoteHeroGold(lord, t);
                    LnPart part = null;
                    foreach (var p in lc.Parts) if (p.St == x.St.StringId) { part = p; break; }
                    if (part == null) { part = new LnPart { St = x.St.StringId, Kind = x.Kind }; lc.Parts.Add(part); }
                    part.Left += t; lc.Lent += t;
                    if (x.Kind == 0) _lnLentT += t; else if (x.Kind == 1) _lnLentC += t; else _lnLentV += t;
                    got += t; left -= t;
                }
                if (got > 0)
                {
                    _lnLentAcc += got; _lnLentN++;
                    if (c.StringId != null) _lnLentClans.Add(c.StringId);
                    if (lc.Day0 < 0) lc.Day0 = (int)CampaignTime.Now.ToDays;
                }
                else if (lc.Parts.Count == 0) _ln.Remove(c.StringId);
            }
            catch (Exception e) { LnStumble("FolkLoanLend", e); }
            return (int)Math.Min(int.MaxValue, got);
        }

        /// <summary>Zdjecie z kiesy zrodla (miasto - W, zamek - podzamcze, wies - kiesa wsi); ksiega kies jak W7 LevyTake. Zwraca zdjete.</summary>
        private static long LnTake(LnSrc x, long amount)
        {
            try
            {
                if (amount <= 0) return 0;
                if (x.Kind == 2)
                {
                    if (x.V == null) return 0;
                    int had = x.V.Gold;
                    x.V.ChangeGold(-(int)Math.Min(amount, had));
                    return Math.Max(0, had - x.V.Gold);
                }
                if (x.F == null) return 0;
                long t = Math.Min(amount, Math.Max(0, x.F.KW));
                if (t <= 0) return 0;
                x.F.KW -= t; _ctlOut += t; x.F.LDayOut += t;
                return t;
            }
            catch (Exception e) { LnStumble("LnTake", e); return 0; }
        }

        /// <summary>Wplata do kiesy ludu osady (splata albo zwrot po wyjatku): miasto - W, zamek - podzamcze, wies - kiesa wsi; kiesa ludu nieczynna albo osada bez ludu -
        /// kasa osady (nic w nicosc). `repay` - liczniki splat. Zwraca 1 - do kiesy ludu, 2 - do kasy osady, 0 - nigdzie (wolajacy oddaje zloto platnikowi).</summary>
        private static int LnPut(Settlement st, int kind, long amount, bool repay)
        {
            if (st == null || amount <= 0) return 0;
            if (kind == 2)
            {
                var v = st.Village;
                if (v != null) { v.ChangeGold((int)amount); if (repay) _lnRepV += amount; return 1; }
                return 0;
            }
            Folk f;
            bool live = kind == 0 ? Live : CastleLive;
            if (live && _f.TryGetValue(st, out f) && f != null && f.Kind == kind)
            {
                f.KW += amount;
                if (repay) { _ctlIn += amount; f.LDayIn += amount; if (kind == 0) _lnRepT += amount; else _lnRepC += amount; }
                else { _ctlOut -= amount; f.LDayOut -= amount; }   // zwrot zdjetego po wyjatku - ksiega kies z powrotem
                return 1;
            }
            if (st.Town != null) { st.Town.ChangeGold((int)amount); if (repay) _lnRepPurse += amount; return 2; }
            return 0;
        }

        /// <summary>
        /// DailyLive, po GuardBegin i przed miastami: okno pozyczek z poprzedniej doby (do linii), odsetki, raty, niesplata, linia "Pozyczki od ludu (obieg2)".
        /// Klucz wylaczony - nic (dlugi zostaja w zapisie, zamrozone). Nie rzuca.
        /// </summary>
        private static void LoansDay(int day)
        {
            long lentPrev = _lnLentAcc, lentT = _lnLentT, lentC = _lnLentC, lentV = _lnLentV; int lentN = _lnLentClans.Count, cand = _lnCand, roomN = _lnRoomN;
            int bBan = _lnBlkBan, bLadder = _lnBlkLadder, bLimit = _lnBlkLimit, bNo = _lnBlkNoSurplus; double room = _lnRoom;
            LoansWinZero(); LoansDayZero();
            var s = Settings.Current;
            if (s == null || !s.FolkLoans) { _lineLoans = null; _lnDebtPrev = double.NaN; return; }
            double debtBefore = _lnDebtPrev;
            try
            {
                int today = (int)CampaignTime.Now.ToDays;
                double rate = LnRate(s) / YearDays, repShare = LnRepay(s);
                int dflt = LnDefault(s);
                var drop = new List<string>();
                // zakazy po niesplacie: wygasle - z ksiegi
                if (_lnBan.Count > 0)
                {
                    var old = new List<string>();
                    foreach (var kv in _lnBan) if (kv.Value <= today) old.Add(kv.Key);
                    foreach (var k in old) _lnBan.Remove(k);
                }
                foreach (var kv in _ln)
                {
                    var lc = kv.Value;
                    try
                    {
                        Clan c = Clan.FindFirst(x => x.StringId == kv.Key);
                        double total = LnDebt(lc);
                        if (c == null || c.IsEliminated || c.Leader == null || !c.Leader.IsAlive)
                        {
                            _lnLost += (long)Math.Round(total); _lnLostD += total; _lnLostN++; drop.Add(kv.Key); continue;   // rod wymarly - dlug przepada (zloto juz u glowy - nic z niczego)
                        }
                        // odsetki doby - na kazda czesc (wplata do tej osady przy splacie)
                        double it = 0;
                        foreach (var p in lc.Parts) { double a = Math.Max(0, p.Left) * rate; p.Left += a; it += a; }
                        lc.Interest += it; _lnInterest += it; total += it;
                        bool war = LnAtWar(c);
                        double D = Math.Max(0, ClanBudget.DOf(c));
                        double due = war ? it : it + Math.Max(repShare * D, total / YearDays);
                        due = Math.Min(due, total);
                        var lord = c.Leader;
                        long pay = (long)Math.Floor(Math.Min(due, Math.Max(0, lord.Gold)));
                        if (pay >= total - 0.5) pay = (long)Math.Ceiling(total - 1e-9);
                        if (pay > lord.Gold) pay = Math.Max(0, lord.Gold);
                        if (!war) { if (pay + 1 < due) lc.Missed++; else lc.Missed = 0; }
                        if (pay > 0)
                        {
                            int had = lord.Gold;
                            lord.ChangeHeroGold(-(int)pay);
                            long took = had - lord.Gold;
                            CirculationWindows.NoteHeroGold(lord, -took);
                            if (took > 0) LnSpread(lc, lord, took, total);
                        }
                        if (lc.Missed >= dflt)
                        {
                            double rest = LnDebt(lc);
                            _lnWritten += (long)Math.Round(rest); _lnWrittenD += rest; _lnDefaultN++;
                            var hit = new HashSet<Town>();
                            foreach (var p in lc.Parts)
                            {
                                var st = Settlement.Find(p.St);
                                Settlement home = st == null ? null : (st.IsVillage && st.Village != null ? st.Village.Bound : st);
                                if (home == null || home.Town == null || home.OwnerClan != c || !hit.Add(home.Town)) continue;
                                try { home.Town.Loyalty = Math.Max(0f, home.Town.Loyalty - LnDefaultLoyalty); _lnLoyN++; } catch (Exception e) { LnStumble("LoansDay(lojalnosc)", e); }
                            }
                            _lnBan[kv.Key] = today + LnBanDays;
                            drop.Add(kv.Key);
                            continue;
                        }
                        if (lc.Missed > 0) _lnMissedN++;
                        foreach (var p in lc.Parts) if (p.Left < 0.5) _lnDust += p.Left;   // ulamki ponizej pol zlotowki - koniec czesci (kontrola liczy je osobno)
                        lc.Parts.RemoveAll(p => p.Left < 0.5);
                        if (lc.Parts.Count == 0) drop.Add(kv.Key);
                    }
                    catch (Exception e) { LnStumble("LoansDay(rod)", e); }
                }
                foreach (var k in drop) _ln.Remove(k);
                LastLoanRepaid = _lnRepaid;
                // linia doby
                double debt = 0; int parts = 0; string top = "-"; double topD = 0;
                var places = new HashSet<string>();
                foreach (var kv in _ln)
                {
                    double d = LnDebt(kv.Value); debt += d; parts += kv.Value.Parts.Count;
                    foreach (var p in kv.Value.Parts) places.Add(p.St);
                    if (d > topD) { topD = d; top = kv.Key; }
                }
                double bank = 0; int bankN = 0;
                try { foreach (var id in IronBank.DebtIds()) { var b = IronBank.DebtById(id); if (b != null && b.Principal >= 1) { bank += b.Principal; bankN++; } } } catch (Exception e) { LnStumble("LoansDay(Bank)", e); }
                // kontrola ksiegi dlugu: przed + zawarte (okno) + odsetki - splacone - umorzone - przepadle (zaokraglenia czesci < 0.5 zl - do 1 zl na czesc)
                string ctl = "-";
                if (!double.IsNaN(debtBefore))
                {
                    double diff = debt - (debtBefore + lentPrev + _lnInterest - _lnRepaid - _lnWrittenD - _lnLostD - _lnDust);
                    ctl = Math.Round(diff).ToString("0", LnInv);
                }
                _lnDebtPrev = debt;
                var sb = new StringBuilder(1400);
                sb.Append("Pozyczki od ludu (obieg2): dzien ").Append(day)
                  .Append(" | zawarte w dobie ").Append(day - 1).Append(" (budzet rodow 166 po dobie ludu, przed kredytem wojennym Banku): ").Append(lentPrev).Append(" zl u ").Append(lentN)
                  .Append(" rodow (z kies miast ").Append(lentT).Append(", podzamczy ").Append(lentC).Append(", wsi ").Append(lentV).Append(')')
                  .Append("; kandydaci (wojna, kiesa rodziny ponizej rezerwy) ").Append(cand).Append(", z miejscem w pulapie ").Append(roomN).Append(" (").Append(N0(room)).Append(" zl)")
                  .Append("; bez pozyczki: zakaz po niesplacie ").Append(bBan).Append(", zaleglosc albo zajecie w Banku ").Append(bLadder).Append(", limit ").Append(bLimit)
                  .Append(", lud lenn bez nadwyzki ").Append(bNo)
                  .Append(" | dzis: odsetki ").Append(N0(_lnInterest)).Append(" (").Append((100 * LnRate(s)).ToString("0.#", LnInv)).Append("% rocznie), splacone ").Append(_lnRepaid)
                  .Append(" (do kies miast ").Append(_lnRepT).Append(", podzamczy ").Append(_lnRepC).Append(", wsi ").Append(_lnRepV).Append(", do kas osad ").Append(_lnRepPurse).Append(')')
                  .Append(", rody z niepelna rata w pokoju ").Append(_lnMissedN).Append(", niesplacone (umorzone po ").Append(dflt).Append(" dobach) ").Append(_lnWritten).Append(" zl u ")
                  .Append(_lnDefaultN).Append(" rodow (lojalnosc -").Append(LnDefaultLoyalty.ToString("0", LnInv)).Append(" w ").Append(_lnLoyN).Append(" osadach), przepadlo z rodami wymarlymi ")
                  .Append(_lnLost).Append(" (").Append(_lnLostN).Append(')')
                  .Append(" | dlug rodow u ludu ").Append(N0(debt)).Append(" (rodow ").Append(_ln.Count).Append(", osad ").Append(places.Count).Append(", najwiekszy ").Append(top).Append(' ').Append(N0(topD))
                  .Append("), zakazow ").Append(_lnBan.Count).Append(" | dlug rodow w Banku ").Append(N0(bank)).Append(" (rodow ").Append(bankN).Append(')')
                  .Append(" | kontrola: dlug - (wczoraj + zawarte + odsetki - splacone - umorzone - przepadle) ").Append(ctl).Append(" (oczekiwane 0)")
                  .Append(" | potkniecia ").Append(_lnStumbles).Append(" (sesja ").Append(_lnStumbleSess).Append(").");
                _lineLoans = sb.ToString();
            }
            catch (Exception e) { LnStumble("LoansDay", e); _lineLoans = null; }
        }

        /// <summary>Splata `took` rozdzielona na czesci dlugu wedlug ich stanu (najwiekszy ulamek dostaje reszte) - wplata do kies osad, ktore pozyczyly.</summary>
        private static void LnSpread(LnClan lc, Hero lord, long took, double total)
        {
            if (lc.Parts.Count == 0 || took <= 0) return;
            long left = took;
            var share = new long[lc.Parts.Count];
            for (int i = 0; i < lc.Parts.Count; i++)
            {
                double q = total > 0 ? Math.Max(0, lc.Parts[i].Left) / total : 1.0 / lc.Parts.Count;
                share[i] = Math.Min(left, (long)Math.Floor(took * q));
                left -= share[i];
            }
            for (int i = 0; left > 0 && i < lc.Parts.Count; i++) { long a = Math.Min(left, Math.Max(0, (long)Math.Ceiling(lc.Parts[i].Left) - share[i])); share[i] += a; left -= a; }
            if (left > 0) share[0] += left;
            for (int i = 0; i < lc.Parts.Count; i++)
            {
                long a = share[i];
                if (a <= 0) continue;
                var p = lc.Parts[i];
                var st = Settlement.Find(p.St);
                if (LnPut(st, p.Kind, a, true) == 0)
                {
                    // osada nie istnieje (ani kiesy, ani kasy) - zloto wraca do glowy, dlug tej czesci bez zmian (nic w nicosc)
                    try { lord.ChangeHeroGold((int)a); CirculationWindows.NoteHeroGold(lord, a); } catch (Exception e) { LnStumble("LnSpread(zwrot)", e); }
                    continue;
                }
                p.Left -= a; lc.Repaid += a; _lnRepaid += a;
            }
        }

        /// <summary>Linia startu sesji (TownFolk.Guard.cs): stan pozyczek z zapisu.</summary>
        private static string LoansStateNote()
        {
            double debt = 0; foreach (var kv in _ln) debt += LnDebt(kv.Value);
            return ", pozyczki od ludu: rodow " + _ln.Count + ", dlug " + N0(debt) + ", zakazow " + _lnBan.Count;
        }

        // ============================================================ zapis arm_obieg2 (straznik + pozyczki)
        /// <summary>Zapis: "o2v1;G,mnoznik,doplywEMA;L,rod,zaleglosc,dzien0,pozyczone,splacone,odsetki;P,osada,rodzaj,dlug;...;B,rod,do_dnia". Pusty - nic do zapisania.</summary>
        internal static string ExportObieg2()
        {
            try
            {
                var sb = new StringBuilder(256);
                sb.Append("o2v1;G,").Append(_gdM.ToString("R", LnInv)).Append(',').Append(double.IsNaN(_gdInEma) ? "NaN" : _gdInEma.ToString("R", LnInv));
                foreach (var kv in _ln)
                {
                    var l = kv.Value;
                    if (l.Parts.Count == 0) continue;
                    sb.Append(";L,").Append(kv.Key).Append(',').Append(l.Missed).Append(',').Append(l.Day0).Append(',').Append(l.Lent.ToString("R", LnInv)).Append(',')
                      .Append(l.Repaid.ToString("R", LnInv)).Append(',').Append(l.Interest.ToString("R", LnInv));
                    foreach (var p in l.Parts) sb.Append(";P,").Append(p.St).Append(',').Append(p.Kind).Append(',').Append(p.Left.ToString("R", LnInv));
                }
                foreach (var kv in _lnBan) sb.Append(";B,").Append(kv.Key).Append(',').Append(kv.Value);
                return sb.ToString();
            }
            catch (Exception e) { Log.Error("TownFolk.ExportObieg2", e); return ""; }
        }

        internal static void ImportObieg2(string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data) || !data.StartsWith("o2v1", StringComparison.Ordinal)) return;
                LnClan cur = null;
                foreach (var rec in data.Split(';'))
                {
                    var f = rec.Split(',');
                    if (f.Length == 0) continue;
                    double v;
                    if (f[0] == "G" && f.Length >= 3)
                    {
                        if (double.TryParse(f[1], NumberStyles.Float, LnInv, out v) && v > 0 && !double.IsInfinity(v)) _gdM = v;
                        _gdInEma = double.TryParse(f[2], NumberStyles.Float, LnInv, out v) && !double.IsInfinity(v) ? v : double.NaN;
                    }
                    else if (f[0] == "L" && f.Length >= 7)
                    {
                        cur = new LnClan();
                        int.TryParse(f[2], NumberStyles.Integer, LnInv, out cur.Missed);
                        int.TryParse(f[3], NumberStyles.Integer, LnInv, out cur.Day0);
                        double.TryParse(f[4], NumberStyles.Float, LnInv, out cur.Lent);
                        double.TryParse(f[5], NumberStyles.Float, LnInv, out cur.Repaid);
                        double.TryParse(f[6], NumberStyles.Float, LnInv, out cur.Interest);
                        _ln[f[1]] = cur;
                    }
                    else if (f[0] == "P" && f.Length >= 4 && cur != null)
                    {
                        int k; double left;
                        if (int.TryParse(f[2], NumberStyles.Integer, LnInv, out k) && double.TryParse(f[3], NumberStyles.Float, LnInv, out left) && left > 0 && k >= 0 && k <= 2)
                            cur.Parts.Add(new LnPart { St = f[1], Kind = k, Left = left });
                    }
                    else if (f[0] == "B" && f.Length >= 3)
                    {
                        int until;
                        if (int.TryParse(f[2], NumberStyles.Integer, LnInv, out until)) _lnBan[f[1]] = until;
                    }
                }
                var empty = new List<string>();
                foreach (var kv in _ln) if (kv.Value.Parts.Count == 0) empty.Add(kv.Key);
                foreach (var k in empty) _ln.Remove(k);
            }
            catch (Exception e) { Log.Error("TownFolk.ImportObieg2", e); }
        }
    }
}
