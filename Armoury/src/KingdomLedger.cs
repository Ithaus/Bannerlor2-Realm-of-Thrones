using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// KSIEGA KRÓLESTW (wpis 77; Jeff 05.10: "jak sie maja finanse lordow i krolow po zmianach budowy - utrzymaja sie czy
    /// zbankrutuja?"). Tylko log - nic nie zmienia. Codziennie dla kazdego krolestwa: skarbiec (KingdomBudgetWallet) i zmiana
    /// od wczoraj, kiesa krola i jego bilans dzienny (model finansow rodu), suma kies rodow, ile rodow biednych (< FinanceLedgerPoor)
    /// i ile z dziennym bilansem na minusie, wojsko (ludzie w partiach rodow), czy w wojnie. Podsumowanie w glownym logu
    /// ("Skarbce:"), szczegoly - najbiedniejsze rody - w Logs/<sesja>/finanse.log ("Finanse:").
    /// </summary>
    internal static class KingdomLedger
    {
        private static readonly Dictionary<Kingdom, int> _lastWallet = new Dictionary<Kingdom, int>();
        private static readonly Dictionary<Clan, int> _lastGold = new Dictionary<Clan, int>();
        private static readonly Dictionary<Kingdom, int> _lastDay = new Dictionary<Kingdom, int>();   // 312e (recenzja): doba wczorajszej migawki - przerwa w ksiedze = brak rozkladu

        internal static void Reset() { _lastWallet.Clear(); _lastGold.Clear(); _lastDay.Clear(); _st312 = 0; }

        internal static void Daily()
        {
            // 312e: przesuniecia migawki korony od wczorajszej linii - odczyt na poczatku i okno zamkniete zawsze (takze przy wylaczonej ksiedze)
            var shifts = new Dictionary<Kingdom, long[]>();
            try { foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated) shifts[k] = CrownIncome.ShiftWindowOf(k); }
            catch (Exception e) { Log.Error("KingdomLedger(312e)", e); }
            finally { CrownIncome.ShiftWindowClear(); }
            try
            {
                var s = Settings.Current;
                if (s == null || !s.FinanceLedgerEnabled) return;
                int day = (int)CampaignTime.Now.ToDays;
                var model = Campaign.Current.Models.ClanFinanceModel;
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated) continue;
                    int wallet = k.KingdomBudgetWallet, lastW;
                    bool hadW = _lastWallet.TryGetValue(k, out lastW);
                    _lastWallet[k] = wallet;
                    int lastD; bool gap = hadW && (!_lastDay.TryGetValue(k, out lastD) || day - lastD != 1);   // 312e: migawka nie z wczoraj (ksiega wylaczona na kilka dob)
                    _lastDay[k] = day;
                    bool war = false;
                    try { foreach (var o in Kingdom.All) if (o != k && !o.IsEliminated && k.IsAtWarWith(o)) { war = true; break; } } catch { }

                    long sumGold = 0; int clans = 0, poor = 0, minus = 0, men = 0;
                    var rows = new List<KeyValuePair<Clan, int>>();
                    var bal = new Dictionary<Clan, int>();
                    foreach (var c in k.Clans)
                    {
                        if (c == null || c.IsEliminated || c.Leader == null) continue;
                        clans++;
                        int g = c.Gold;
                        sumGold += g;
                        if (g < s.FinanceLedgerPoor) poor++;
                        // wpis 87 (audyt pkt 16): bilans = zmiana kiesy od wczoraj - model finansow nie zna naszych przeplywow
                        int last0; bool had0 = _lastGold.TryGetValue(c, out last0);
                        int b = had0 ? g - last0 : 0;
                        bal[c] = b;
                        if (had0 && b < 0) minus++;
                        try { foreach (var wp in c.WarPartyComponents) if (wp != null && wp.MobileParty != null && wp.MobileParty.MemberRoster != null) men += wp.MobileParty.MemberRoster.TotalManCount; } catch { }
                        rows.Add(new KeyValuePair<Clan, int>(c, g));
                    }
                    var ruler = k.RulingClan;
                    int rg = ruler != null ? ruler.Gold : 0, rb = 0;
                    if (ruler != null) bal.TryGetValue(ruler, out rb);
                    Log.Info("Skarbce: dzien " + day + " - " + k.Name + (war ? " (WOJNA)" : "") + ": skarbiec " + wallet + (hadW ? " (" + Sign(wallet - lastW) + ")" : "")
                             + KingdomTreasury.RefundNote(k) + CrownBorrow.LedgerNote(k)
                             + ", krol " + (ruler != null ? ruler.Name.ToString() : "-") + " kiesa " + rg + " bilans " + Sign(rb) + "/dzien"
                             + "; rodow " + clans + ", kiesy razem " + sumGold + ", biednych (<" + s.FinanceLedgerPoor + ") " + poor + ", na minusie " + minus
                             + Split312(k, hadW, gap, wallet - (long)lastW, shifts, s)   // 312e: rozklad zmiany doby - PRZED "; wojsko rodow" (wzory sprawdz_logi.py)
                             + "; wojsko rodow " + men + " ludzi.");
                    rows.Sort((a, b) => a.Value.CompareTo(b.Value));
                    var parts = new List<string>();
                    foreach (var r in rows.Take(Math.Max(0, s.FinanceLedgerPoorest)))
                    {
                        int last; bool had = _lastGold.TryGetValue(r.Key, out last);
                        int b; bal.TryGetValue(r.Key, out b);
                        parts.Add(r.Key.Name + " " + r.Value + (had ? " (" + Sign(r.Value - last) + ")" : "") + " bilans " + Sign(b));
                    }
                    if (parts.Count > 0) Log.Info("Finanse: dzien " + day + " - " + k.Name + " najbiedniejsze rody: " + string.Join(", ", parts.ToArray()) + ".");
                    foreach (var r in rows) _lastGold[r.Key] = r.Value;
                }
            }
            catch (Exception e) { Log.Error("KingdomLedger", e); }
        }

        private static string Sign(int v) { return v >= 0 ? "+" + v : v.ToString(); }
        private static string Sg(long v) { return v >= 0 ? "+" + v : v.ToString(); }
        private static int _st312;   // 312e: potkniecia rozkladu (per krolestwo; linia dalej bez rozkladu)

        /// <summary>
        /// 312e (Jeff 01.11 "sprawdz czemu traca korony"; raport rozp312 P1) - TYLKO NAPIS: rozklad zmiany skarbca od wczorajszej linii na kanaly z ksiegi doby korony
        /// (CrownIncome.KDay - te same liczby co linia 165) i z przesuniec migawki. Okno: wczorajsza linia -> dzisiejsza (po End). Tozsamosc:
        /// zmiana = wplywy - z budzetu dnia - z zapasu + korona-korona + kredyty + tarczowe W6 - splaty pozyczek miast - nagrody 178 + do zapasu poza wplywami + reszta;
        /// reszta = to, czego zadna z tych pozycji nie liczy (zaokraglenia darow u odbiorcy, wplywy bloku korony spoza okien) - liczona, nie dopasowana.
        /// "Ponad wplywy dnia" (1/450 zapasu wydane z budzetu) to czesc "z budzetu dnia", nie osobny skladnik sumy. Bez wpisu doby korony albo bez wczorajszej
        /// migawki - napis "brak rozkladu". Nie rzuca (potkniecie - licznik, linia bez rozkladu).
        /// </summary>
        private static string Split312(Kingdom k, bool hadW, bool gap, long chg, Dictionary<Kingdom, long[]> shifts, Settings s)
        {
            try
            {
                string g = DsSeason.GrantNote(k);
                long res;   // zapas wojny - ta sama liczba co W5b / f23 (TreasurySurplusFor po End: bez reszty wplywow dnia)
                double sur = KingdomTreasury.TreasurySurplusFor(k, out res);
                string tail = (g.Length > 0 ? "; " + g : "; nadanie wojenne -") + ", zapas wojny " + res + ", nadwyzka " + (long)Math.Floor(sur)
                              + (_st312 + CrownIncome.Stumbles312 > 0 ? ", potkniecia rozkladu " + (_st312 + CrownIncome.Stumbles312) : "");
                var d = CrownIncome.LedgerDayOf(k);
                if (!hadW || gap || d == null || !d.Snap)
                    return "; zmiana doby (312e): brak rozkladu (" + (!hadW ? "pierwsza linia" : gap ? "przerwa w ksiedze" : d == null ? "bez doby korony 165" : "bez wczorajszej migawki") + ")" + tail;
                long[] sh; if (!shifts.TryGetValue(k, out sh) || sh == null) sh = new long[3];
                long inc = d.Own + d.Other + d.OwnLate;
                long zw = d.RefundGiven - d.DsTreas, rent = d.Rent, mer = d.Contract - d.ContractTr;
                long oth = (d.CallToWar - d.CallToWarTr) + (d.LoanOut - d.LoanTr) + d.NeedAid + d.NeedAidRent + d.Bread + d.Court;
                long bud = zw + rent + mer + oth;
                long f24 = d.LoanTr + d.ContractTr + d.CallToWarTr, hb = KingdomTreasury.HoardUsedToday(k), hr = -sh[1];
                long rs = d.CrownAid + d.MidAid + d.DsTreas + f24 + hb + hr;
                long kk = d.RepIn - d.RepOut - d.GiftOut + sh[0];
                long cr = d.LoanIn + d.DsAnt, sc = d.ScutIn - d.ScutOut, tr = d.TownRepaid, oz = sh[2], rw = d.Reward178 + d.Wage314 + d.Gift314;   // 314c: zold i dary krola w tej samej pozycji (wyplata ze skarbca do glow rodow poza budzetem rent)
                long rest = chg - (inc - bud - rs + kk + cr + sc - tr - rw + oz);
                // czesc wydatkow z budzetu dnia ponad wplywy dnia (z 1/N zapasu; raty i dary tez z budzetu, kredyty i raty przyjete go powiekszaja) - tylko informacja, GORNA granica
                // (splaty miast z nadwyzki KlRepay(extra) i nagrody 178 nie sa tu odejmowane od budzetu - recenzja 312e)
                long over = Math.Max(0, Math.Min(d.Release, bud + d.RepOut + d.GiftOut + tr - d.Measured - cr - d.RepIn));
                return "; zmiana doby (312e) " + Sg(chg) + " = wplywy " + Sg(inc) + " (nasze " + d.Own + ", inne " + Sg(d.Other) + ", po otwarciu " + Sg(d.OwnLate) + ")"
                       + " - z budzetu dnia " + bud + " (zwrot " + zw + ", renty " + rent + ", najemnicy " + mer + ", reszta budzetu " + oth + "; ponad wplywy dnia z 1/" + Math.Max(1f, s.CrownReserveReleaseDays).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " zapasu najwyzej " + over + ")"
                       + " - z zapasu " + rs + " (W5b " + d.CrownAid + ", W5c " + d.MidAid + ", K5 " + d.DsTreas + ", f24 " + f24 + ", m105 chleb " + hb + ", m105 rachunki wczoraj " + hr + ")"
                       + " + korona-korona " + Sg(kk) + " (raty +" + d.RepIn + " -" + d.RepOut + ", dary -" + d.GiftOut + ", przejecia skarbu " + Sg(sh[0]) + ")"
                       + " + kredyty " + Sg(cr) + " + tarczowe W6 " + Sg(sc) + " - splaty pozyczek miast " + tr + " - nagrody 178 " + rw + (d.Wage314 + d.Gift314 > 0 ? " (w tym zold krolewski 314c " + d.Wage314 + ", dary za zwyciestwo 314c " + d.Gift314 + ")" : "") + " + do zapasu poza wplywami " + Sg(oz) + " + reszta " + Sg(rest)
                       + tail;
            }
            catch (Exception e)
            {
                if (_st312++ == 0) Log.Error("KingdomLedger.Split312", e);
                return "; zmiana doby (312e): brak rozkladu (potkniecie)";
            }
        }
    }
}
