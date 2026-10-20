using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m96-dlugi - DLUGI RAD MIAST I PODATEK NADZWYCZAJNY (pomysl Jeffa 07.10 "zapisz na pozniej", PLAN-KOSZTY-MIASTA-2026-10-07 rozdz. 6; decyzja 20.10 "dlugi miast tak,
    /// robimy"). Klucz glowny TownDebt (domyslnie TAK; wylaczony = jak przed paczka - nowe dlugi nie powstaja, stare stoja w zapisie bez odsetek i bez skutkow).
    /// DOWOD (log 160 dob WGRANIA 95, Armoury-2026-10-20_12-25-19.log, linie "Firmy miast i zamkow (DS)" i "Kasy miast (169c)" doba 109234): kasa miasta placi z obowiazku
    /// tylko rate firmy DS (pan i korona, co 7 dob, najwyzej do podlogi 0.5 x zapas kupcow); roboty miast (TownHire), zboze (TownGranary) i nasze zakupy biora tylko z nadwyzki.
    /// Gdy kasa stoi na podlodze, rata idzie do ksiegi zaleglosci: 8 miast z zaleglosciami 381 134 zl (Duskendale 277 302 = 5.1 rat, kasa 24 397 przy podlodze 21 032),
    /// 6-19 z 97 kas ponizej zapasu kupcow, Pentos 71 dob z rzedu; po 2 dniach kwartalu - zajecie z kiesy gminy przez pana. Rada miasta nie miala zadnej drogi: ani pozyczki,
    /// ani wlasnego podatku - "miasto" bylo tylko kasa kupcow.
    /// REGULA (sredniowiecze: rada miasta pozycza u swoich bogatych mieszczan i u bankierow, splaca z nadwyzek, a gdy nie moze - sama naklada podatek nadzwyczajny, "tallage";
    /// AI i gracz te same reguly, kazde miasto):
    ///  (1) POZYCZKA: miasto z zaleglosciami firmy z kasy (pan + korona, DsTowns.TdArrears) >= MinNeed pozycza na ich splate najwyzej do limitu Town Debt Limit Reserve x zapas
    ///      kupcow (TownPurse.Reserve): najpierw kupcy i patrycjat (kiesa P), potem rzemieslnicy (kiesa R) - polowa tego, co maja ponad Town Loan Folk Reserve Times x zapas
    ///      dni, wspolny pulap dzienny z pozyczka miejska f8 (TownFolk.TdLendOffer); potem notable miasta (polowa zlota ponad Town Debt Notable Keep); na koncu Bank Zelazny
    ///      (z kapitalu ponad Crown Loan Bank Floor, dziennie najwyzej 1/60 tej nadwyzki dla wszystkich miast razem). Pozyczone zloto wchodzi do kasy i od razu splaca
    ///      zaleglosc panu i koronie (DsTowns.TdPayArrears - ta sama droga przelewu co rata). Bez zywego pana i w oblezeniu - nic (zaleglosc czeka jak dotad).
    ///  (2) ODSETKI: Town Debt Rate rocznie u mieszczan i notabli, Town Debt Bank Rate w Banku (rok kalendarza gry - IronBank.DaysPerYearNow), doliczane co dobe do dlugu.
    ///      Odsetki wplacone kiesie klasy sa jej zarobkiem okna (podstawa podatkow pana - jak f8).
    ///  (3) SPLATA: co dobe Town Debt Repay Share nadwyzki kasy ponad zapas kupcow - najpierw Bank (najdrozszy), potem notable i mieszczanie w stosunku dlugu. KOSZTEM
    ///      RENTY PANA: splata nie jest poborem (TownPurse bez NoteTake) - doplyw netto kupcow maleje o splate, a przeglad firmy DS idzie za doplywem; do tego dlug rady
    ///      liczy sie w DS jak zaleglosc (DsTowns Terms / Review / SurplusReview - ArrearsLike): bez podwyzki firmy i z przegladem wymuszonym w dol, gdy dlug >= raty
    ///      przez 2 dni kwartalu - firma pana i korony schodzi do sil miasta. Zajecie z kiesy gminy (DS) zostaje bez zmian - tylko przy prawdziwej zaleglosci.
    ///  (4) DOBROBYT: miasto z dlugiem traci co dobe Town Debt Prosperity Per Day x (dlug / limit, najwyzej 1).
    ///  (5) NIEWYPLACALNOSC: dlug >= 90% limitu albo zaleglosc, ktorej nikt juz nie pokryl (brak kredytu) - RADA SAMA naklada PODATEK NADZWYCZAJNY (nie pan): jednorazowo
    ///      Town Debt Levy Share tego, co klasy W / R / P maja ponad 1 x zapas dni, i tego, co notable maja ponad Town Debt Notable Keep, najwyzej tyle, ile trzeba (dlug +
    ///      zaleglosc). Zebrane - do kasy, z kasy od razu wierzycielom (Bank pierwszy) i na zaleglosc firmy. Koszt: lojalnosc miasta -Town Debt Levy Loyalty (nie ponizej
    ///      20 z tego powodu), dobrobyt -Town Debt Levy Prosperity x dobrobyt, relacja kazdego placacego notabla z panem miasta -Town Debt Levy Relation. Przerwa miedzy
    ///      zbiorkami Town Debt Levy Gap Days.
    /// NIC Z NICZEGO: kazdy ruch to przelew miedzy posiadaczami "Pieniadza swiata" (kiesy ludu, notable, Bank, kasa miasta, pan, skarbiec); dlug to zapis. Ruchy kas w naszym
    /// ticku sa w pozycji MFolk ksiegi "Przeplywy osad" (wolanie przed Mark MFolk). Bank: CapitalAdd para z ChangeGold, koszt pozyczki NoteCrownLent("td:<miasto>") i dlugi
    /// w rozliczeniu zysku (IronBank.Dividends DvDay -> BankInto). Wierzyciel-notabl zmarly albo juz nie notabl - jego czesc przechodzi na kiese P (spadkobiercy, cech kupcow).
    /// Kiesa ludu nieczynna przy splacie - zloto zostaje w kasie, dlug bez zmiany.
    /// GRACZ: w swoim miescie powiadomienie (pierwsza pozyczka, podatek nadzwyczajny, splata do zera); w kazdym miescie z dlugiem opcja "The council's debts" (Town affairs).
    /// ZAPIS arm_towndebt ("td1|id;P;R;B;IP;IR;dzien podatku;od kiedy;ostatni podatek;notabl~kwota~odsetki,..."), SaveText.Sync; brak klucza (stary zapis, kampania Jeffa
    /// z WGRANIA 94/95) = brak dlugow, start od pierwszej zaleglosci. Reset przy nowej kampanii / przed wczytaniem (ArmouryBehavior, osobna linia _m96DlugiReset).
    /// Linia "Dlugi miast (m96-dlugi): dzien N" co dobe. Watek glowny (tick dobowy); wyjatek per miasto - licznik potkniec, bez globalnego wylacznika (CLAUDE.md 7).
    /// </summary>
    internal static class TownDebt
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Ver = "td1";
        private const double MinNeed = 500;            // zaleglosc ponizej - bez pozyczki (ulamki raty)
        private const double InsolventShare = 0.9;     // dlug >= 90% limitu - niewyplacalne
        private const double BankPoolDays = 60;        // jak kredyt korony (CrownLoanPoolDays): dziennie 1/60 kapitalu ponad prog
        private const float LevyLoyaltyFloor = 20f;    // podatek nadzwyczajny sam nie spycha lojalnosci ponizej 20
        private const string BankPrefix = "td:";

        private sealed class Book
        {
            public Settlement St;                                        // sesja
            public double P, R, B;                                       // dlug wobec kiesy P, kiesy R, Banku (z odsetkami)
            public double IP, IR;                                        // niezaplacone odsetki w P i R (zarobek okna przy splacie)
            public readonly Dictionary<string, double[]> N = new Dictionary<string, double[]>();   // notabl -> [dlug, niezaplacone odsetki]
            public int LevyDay = int.MinValue, Since = -1;
            public long LastLevy;
            public double Total { get { double n = 0; foreach (var v in N.Values) n += v[0]; return P + R + B + n; } }
        }

        private static readonly Dictionary<string, Book> _b = new Dictionary<string, Book>();
        private static readonly List<string> _drop = new List<string>();
        private static readonly List<string> _ids = new List<string>();
        private static readonly List<KeyValuePair<Hero, long>> _nPaid = new List<KeyValuePair<Hero, long>>();
        private static Dictionary<string, Hero> _heroById;               // tylko przy rozwiazaniu notabli z zapisu (raz na sesje)
        private static bool _heroResolved, _offLogged, _errLogged;
        private static double _debtPrev = double.NaN;
        private static int _stumbles, _stumblesSess;

        // ------------------------------------------------------------ liczniki doby (zeruje Daily)
        private static long _dLentP, _dLentR, _dLentN, _dLentB, _dToLord, _dToCrown, _dRepB, _dRepN, _dRepF, _dRepInt, _dLevyW, _dLevyR, _dLevyP, _dLevyN, _dLevyToCred, _dLevyToArr, _dLevyKept, _dLoanKept;
        private static double _dInt, _dIntB, _dPros, _dLevyPros, _dLevyLoy, _dMovedN;
        private static int _dBorrowN, _dNewN, _dClearedN, _dLevies, _dLevyNotables, _dInsolvent, _dNoCredit, _dArrTowns, _dSiege, _dNoLord, _dRepN2;
        private static double _dArrLeft, _dArrSeen;
        private static double _bankPool;
        /// <summary>"Obieg" (MoneyLedger): pozyczki rad tej doby (wierzyciele -> kasy -> panowie i skarbce), splaty (kasy -> wierzyciele), podatek nadzwyczajny (lud i notable -> kasy).</summary>
        internal static long LastLent, LastRepaid, LastLevy;

        internal static bool On { get { var s = Settings.Current; return s != null && s.TownDebt; } }

        internal static void Reset()
        {
            _b.Clear(); _drop.Clear(); _ids.Clear(); _nPaid.Clear(); _heroById = null; _heroResolved = false; _offLogged = false; _errLogged = false;
            _debtPrev = double.NaN; _stumbles = 0; _stumblesSess = 0; ZeroDay(); LastLent = LastRepaid = LastLevy = 0;
        }

        private static void ZeroDay()
        {
            _dLentP = _dLentR = _dLentN = _dLentB = _dToLord = _dToCrown = _dRepB = _dRepN = _dRepF = _dRepInt = _dLevyW = _dLevyR = _dLevyP = _dLevyN = _dLevyToCred = _dLevyToArr = _dLevyKept = _dLoanKept = 0;
            _dInt = _dIntB = _dPros = _dLevyPros = _dLevyLoy = _dMovedN = 0;
            _dBorrowN = _dNewN = _dClearedN = _dLevies = _dLevyNotables = _dInsolvent = _dNoCredit = _dArrTowns = _dSiege = _dNoLord = _dRepN2 = 0;
            _dArrLeft = _dArrSeen = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesSess++;
            if (_errLogged) return;
            _errLogged = true;
            Log.Error("TownDebt." + where, e);
        }

        private static double Clamp(double v, double lo, double hi) { return double.IsNaN(v) ? lo : (v < lo ? lo : (v > hi ? hi : v)); }
        private static double Limit(Town t, Settings s) { return Clamp(s.TownDebtLimitReserve, 0, 20) * Math.Max(0, TownPurse.Reserve(t)); }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static string NameOf(Settlement st) { return st != null && st.Name != null ? st.Name.ToString() : "?"; }

        // ------------------------------------------------------------ odczyty dla innych modulow
        /// <summary>DsTowns (Terms, Review, SurplusReview): dlug rady miasta liczony jak zaleglosc firmy. Klucz wylaczony / brak dlugu - 0.</summary>
        internal static double ArrearsLike(Settlement st)
        {
            try
            {
                if (st == null || st.StringId == null || _b.Count == 0 || !On) return 0;
                Book b;
                return _b.TryGetValue(st.StringId, out b) ? Math.Max(0, b.Total) : 0;
            }
            catch { return 0; }
        }

        /// <summary>IronBank.Dividends DvDay: dlugi rad wobec Banku (z odsetkami) pod id "td:&lt;miasto&gt;" - koszt pozyczek (NoteCrownLent) rozliczany jak kredyt korony.
        /// Zawsze (takze przy kluczu wylaczonym - dlug w Banku stoi, a koszt nie moze zniknac jako strata).</summary>
        internal static void BankInto(Dictionary<string, double> into)
        {
            try
            {
                if (into == null) return;
                foreach (var kv in _b) if (kv.Value != null && kv.Value.B >= 1) into[BankPrefix + kv.Key] = kv.Value.B;
            }
            catch (Exception e) { Stumble("BankInto", e); }
        }

        /// <summary>Dopisek do linii "Obieg" (MoneyLedger) - pusto, gdy dzis nic.</summary>
        internal static string ObiegNote()
        {
            if (LastLent == 0 && LastRepaid == 0 && LastLevy == 0) return "";
            return ", dlugi rad miast (m96-dlugi): pozyczki kiesy ludu, notable i Bank -> kasy miast -> panowie i skarbce " + LastLent
                   + ", splaty kasy miast -> wierzyciele " + LastRepaid + ", podatek nadzwyczajny kiesy ludu i notable -> kasy miast " + LastLevy;
        }

        // ------------------------------------------------------------ doba (ArmouryBehavior: po TownFolk.Daily i TownGranary, przed Mark MFolk)
        internal static void Daily()
        {
            ZeroDay();
            LastLent = LastRepaid = LastLevy = 0;
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            if (!s.TownDebt)
            {
                if (!_offLogged) { _offLogged = true; Log.Info("Dlugi miast (m96-dlugi): WYLACZONE (MCM Town Debt) - rady nie pozyczaja ani nie nakladaja podatku; dlugi z zapisu stoja bez odsetek i skutkow (" + _b.Count + " miast)."); }
                return;
            }
            _offLogged = false;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int today = Today;
            ResolveNotables();
            double year = Math.Max(28, IronBank.DaysPerYearNow());
            double rF = Clamp(s.TownDebtRate, 0, 0.5) / year, rB = Clamp(s.TownDebtBankRate, 0, 1) / year;
            _bankPool = 0;
            try { if (IronBank.On) _bankPool = Math.Max(0, (IronBank.FreeCapital - Math.Max(0, s.CrownLoanBankFloor)) / BankPoolDays); } catch (Exception e) { Stumble("BankPool", e); }
            double bankPool0 = _bankPool;
            foreach (var t in Town.AllTowns)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null || st.StringId == null) continue;
                try { TownDay(st, t, s, today, rF, rB); }
                catch (Exception e) { Stumble("TownDay(" + st.StringId + ")", e); }
            }
            // ksiegi bez dlugu i bez swiezego podatku - won
            _drop.Clear();
            int gap = GapDays(s);
            foreach (var kv in _b) if (kv.Value.Total < 1 && (kv.Value.LevyDay == int.MinValue || today - kv.Value.LevyDay >= gap)) _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++) _b.Remove(_drop[i]);
            LastLent = _dLentP + _dLentR + _dLentN + _dLentB;
            LastRepaid = _dRepB + _dRepN + _dRepF + _dLevyToCred;
            LastLevy = _dLevyW + _dLevyR + _dLevyP + _dLevyN;
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Line(today, s, bankPool0, ms);
        }

        private static int GapDays(Settings s) { return Math.Max(7, Math.Min(364, s.TownDebtLevyGapDays)); }

        private static Book BookOf(Settlement st, bool create)
        {
            Book b;
            if (_b.TryGetValue(st.StringId, out b)) { if (b.St == null) b.St = st; return b; }
            if (!create) return null;
            b = new Book { St = st };
            _b[st.StringId] = b;
            return b;
        }

        private static void TownDay(Settlement st, Town t, Settings s, int today, double rF, double rB)
        {
            Book b = BookOf(st, false);
            double al, ac, wk, cov;
            bool ds = DsTowns.TdArrears(st, out al, out ac, out wk, out cov);
            double arr = ds ? Math.Max(0, al + ac - cov) : 0;                  // tylko to, czego kasa sama nie zaplaci w dzien raty (ponad podloge firm)
            if (arr >= 1) { _dArrTowns++; _dArrSeen += arr; }
            if (b == null && arr < MinNeed) return;                         // wiekszosc miast: bez dlugu i bez zaleglosci - nic
            // 1. odsetki i wierzyciele-notable, ktorzy odeszli
            if (b != null) Accrue(b, rF, rB);
            if (st.IsUnderSiege) { _dSiege++; if (b != null) Prosperity(b, t, s); return; }   // oblezenie: tylko odsetki i dobrobyt (rata DS i tak darowana)
            double limit = Limit(t, s);
            // 2. splata z nadwyzki kasy ponad zapas kupcow - tylko bez zaleglosci firmy (nadwyzka idzie najpierw na rate i zaleglosc panu i koronie w dzien raty)
            if (b != null && b.Total >= 1 && al + ac < 1)
            {
                double spare = (double)t.Gold - TownPurse.Reserve(t);
                double share = Clamp(s.TownDebtRepayShare, 0, 1);
                if (spare >= 1 && share > 0)
                {
                    long pay = (long)Math.Floor(Math.Min(b.Total, share * spare));
                    if (pay > 0) { long got = Repay(st, t, b, pay, false); if (got > 0) _dRepN2++; }
                }
            }
            // 3. pozyczka na zaleglosc firmy
            var clan = st.OwnerClan;
            var lord = clan != null ? clan.Leader : null;
            bool lordOk = lord != null && lord.IsAlive;
            bool lentAny = false;
            if (arr >= MinNeed && !lordOk) _dNoLord++;
            if (arr >= MinNeed && lordOk)
            {
                double total = b != null ? b.Total : 0;
                double room = Math.Max(0, limit - total);
                double need = Math.Min(arr, room);
                if (need >= MinNeed)
                {
                    if (b == null) b = BookOf(st, true);
                    long lent = Borrow(st, t, b, (long)Math.Ceiling(need), today);
                    if (lent > 0)
                    {
                        lentAny = true;
                        long tl, tc;
                        long paid = DsTowns.TdPayArrears(st, lent, out tl, out tc);
                        _dToLord += tl; _dToCrown += tc;
                        _dBorrowN++;
                        if (b.Since < 0)
                        {
                            b.Since = today; _dNewN++;
                            if (clan == Clan.PlayerClan) Say("The council of " + NameOf(st) + " has borrowed " + lent + " denars to pay its dues to you and the crown. While the town is in debt its prosperity suffers.", false);
                        }
                        if (paid < lent) _dLoanKept += lent - paid;              // zostalo w kasie (reszta po zaokragleniu w gore) - dlug zapisany
                    }
                }
                ds = DsTowns.TdArrears(st, out al, out ac, out wk, out cov);
                arr = ds ? Math.Max(0, al + ac - cov) : 0;
            }
            // 4. niewyplacalnosc - podatek nadzwyczajny rady
            double tot = b != null ? b.Total : 0;
            bool noCredit = arr >= MinNeed && lordOk && !lentAny;               // dzis nikt nic nie pozyczyl (brak miejsca w limicie albo pozyczajacych) - pulap dzienny to nie brak kredytu
            if (noCredit) { _dNoCredit++; _dArrLeft += arr; }
            bool insolvent = noCredit || (tot >= 1 && limit > 0 && tot >= InsolventShare * limit);
            if (insolvent)
            {
                _dInsolvent++;
                if (b == null) b = BookOf(st, true);
                if (b.LevyDay == int.MinValue || today - b.LevyDay >= GapDays(s)) Levy(st, t, b, s, today, lordOk ? arr : 0, lord);
            }
            // 5. dobrobyt przy dlugu
            if (b != null) Prosperity(b, t, s);
            // 6. splacone do zera
            if (b != null && b.Since >= 0 && b.Total < 1)
            {
                b.P = b.R = b.B = b.IP = b.IR = 0; b.N.Clear(); b.Since = -1; _dClearedN++;
                if (clan == Clan.PlayerClan) Say("The council of " + NameOf(st) + " has paid off its debts.", true);
            }
        }

        private static void Accrue(Book b, double rF, double rB)
        {
            double iP = b.P * rF, iR = b.R * rF, iB = b.B * rB;
            b.P += iP; b.IP += iP; b.R += iR; b.IR += iR; b.B += iB;
            _dInt += iP + iR + iB; _dIntB += iB;
            if (b.N.Count == 0) return;
            _ids.Clear();
            foreach (var kv in b.N)
            {
                var h = HeroOf(kv.Key);
                if (h == null || !h.IsAlive || !h.IsNotable) { _ids.Add(kv.Key); continue; }
                double i = kv.Value[0] * rF; kv.Value[0] += i; kv.Value[1] += i; _dInt += i;
            }
            for (int k = 0; k < _ids.Count; k++)
            {
                double[] v;
                if (!b.N.TryGetValue(_ids[k], out v)) continue;
                b.P += v[0]; b.IP += v[1]; _dMovedN += v[0];                  // spadkobiercy: kupcy i patrycjat miasta (kiesa P)
                b.N.Remove(_ids[k]);
            }
        }

        private static void Prosperity(Book b, Town t, Settings s)
        {
            double tot = b.Total;
            if (tot < 1) return;
            double lim = Limit(t, s);
            double q = lim > 0 ? Math.Min(1.0, tot / lim) : 1.0;
            double loss = Clamp(s.TownDebtProsperityPerDay, 0, 10) * q;
            if (!(loss > 0)) return;
            float p0 = t.Prosperity;
            t.Prosperity = Math.Max(0f, p0 - (float)loss);
            _dPros += p0 - t.Prosperity;
        }

        // ------------------------------------------------------------ pozyczka: P, R, notable, Bank -> kasa miasta
        private static long Borrow(Settlement st, Town t, Book b, long need, int today)
        {
            long got = 0;
            for (int pass = 0; pass < 2 && got < need; pass++)
            {
                int cls = pass == 0 ? 2 : 1;                                // najpierw kupcy i patrycjat (P), potem rzemieslnicy (R)
                double offer = TownFolk.TdLendOffer(st, cls);
                long want = (long)Math.Min(need - got, Math.Floor(offer));
                if (want <= 0) continue;
                long x = TownFolk.TdTake(st, cls, want, true);
                if (x <= 0) continue;
                try { t.ChangeGold((int)x); }
                catch (Exception e) { TownFolk.TdUndo(st, cls, x, true); Stumble("Borrow(kasa)", e); continue; }
                if (cls == 2) { b.P += x; _dLentP += x; } else { b.R += x; _dLentR += x; }
                got += x;
            }
            if (got < need)
            {
                var s = Settings.Current;
                double keep = Math.Max(0, s.TownDebtNotableKeep);
                var ns = st.Notables;
                if (ns != null)
                    for (int i = 0; i < ns.Count && got < need; i++)
                    {
                        var h = ns[i];
                        if (h == null || !h.IsAlive || !h.IsNotable || h.StringId == null) continue;
                        double off = 0.5 * (h.Gold - keep);
                        long want = (long)Math.Min(need - got, Math.Floor(off));
                        if (want <= 0) continue;
                        int ix = (int)Math.Min(int.MaxValue, want);
                        int g0 = h.Gold;
                        h.ChangeHeroGold(-ix);
                        long x = (long)g0 - h.Gold;
                        if (x <= 0) continue;
                        CirculationWindows.NoteHeroGold(h, -x);
                        try { t.ChangeGold((int)x); }
                        catch (Exception e) { h.ChangeHeroGold((int)x); CirculationWindows.NoteHeroGold(h, x); Stumble("Borrow(notabl)", e); continue; }
                        double[] v;
                        if (!b.N.TryGetValue(h.StringId, out v)) { v = new double[2]; b.N[h.StringId] = v; }
                        v[0] += x; _dLentN += x; got += x;
                        if (_heroById != null) _heroById[h.StringId] = h;
                    }
            }
            if (got < need && _bankPool >= 1 && IronBank.On)
            {
                long x = (long)Math.Min(need - got, Math.Floor(Math.Min(_bankPool, IronBank.FreeCapital)));
                if (x > 0)
                {
                    IronBank.CapitalAdd(-x);
                    try { t.ChangeGold((int)x); }
                    catch (Exception e) { IronBank.CapitalAdd(x); Stumble("Borrow(Bank)", e); x = 0; }
                    if (x > 0)
                    {
                        IronBank.NoteCrownLent(BankPrefix + st.StringId, x);      // f20-d3: koszt pozyczki (kapital, ktory wyszedl) - odsetki to zysk Banku
                        b.B += x; _dLentB += x; got += x; _bankPool -= x;
                    }
                }
            }
            return got;
        }

        // ------------------------------------------------------------ splata: kasa miasta -> Bank, notable, kiesy P i R
        /// <summary>Splata `pay` z kasy (najwyzej kasa): Bank pierwszy, potem notable i kiesy P / R w stosunku dlugu. levy - pieniadz z podatku nadzwyczajnego (licznik).</summary>
        private static long Repay(Settlement st, Town t, Book b, long pay, bool levy)
        {
            long done = 0;
            pay = Math.Min(pay, Math.Max(0, (long)t.Gold));
            if (pay <= 0) return 0;
            // Bank
            if (b.B >= 1 && pay > 0)
            {
                long x = (long)Math.Min(pay, Math.Ceiling(b.B));
                long o = Out(t, x);
                if (o > 0)
                {
                    IronBank.CapitalAdd(o);
                    b.B = Math.Max(0, b.B - o); pay -= o; done += o; if (levy) _dLevyToCred += o; else _dRepB += o;
                }
            }
            if (pay <= 0) return done;
            double nSum = 0; foreach (var v in b.N.Values) nSum += v[0];
            double rest = b.P + b.R + nSum;
            if (rest < 1) return done;
            double f = Math.Min(1.0, pay / rest);
            // notable
            if (nSum >= 1)
            {
                _ids.Clear(); foreach (var k in b.N.Keys) _ids.Add(k);
                for (int i = 0; i < _ids.Count; i++)
                {
                    double[] v; if (!b.N.TryGetValue(_ids[i], out v)) continue;
                    var h = HeroOf(_ids[i]);
                    if (h == null || !h.IsAlive) continue;                      // przejdzie na kiese P przy odsetkach jutro
                    long x = (long)Math.Min(Math.Floor(v[0] * f + 0.5), Math.Ceiling(v[0]));
                    if (x <= 0) continue;
                    long o = Out(t, x);
                    if (o <= 0) continue;
                    try { h.ChangeHeroGold((int)o); CirculationWindows.NoteHeroGold(h, o); }
                    catch (Exception e) { t.ChangeGold((int)o); Stumble("Repay(notabl)", e); continue; }
                    double ip = Math.Min(v[1], o); v[1] -= ip; _dRepInt += (long)ip;
                    v[0] = Math.Max(0, v[0] - o); if (v[0] < 1) b.N.Remove(_ids[i]);
                    done += o; if (levy) _dLevyToCred += o; else _dRepN += o;
                }
            }
            // kiesy P i R
            for (int cls = 2; cls >= 1; cls--)
            {
                double bal = cls == 2 ? b.P : b.R;
                if (bal < 1) continue;
                long x = (long)Math.Min(Math.Floor(bal * f + 0.5), Math.Ceiling(bal));
                if (x <= 0) continue;
                double accr = cls == 2 ? b.IP : b.IR;
                long ip = (long)Math.Min(accr, x);
                long o = Out(t, x);
                if (o <= 0) continue;
                long gv = TownFolk.TdGive(st, cls, o, Math.Min(ip, o));
                if (gv <= 0) { t.ChangeGold((int)o); continue; }               // kiesa nieczynna - zloto zostaje w kasie, dlug bez zmiany
                long ipp = Math.Min(ip, gv);
                if (cls == 2) { b.P = Math.Max(0, b.P - gv); b.IP = Math.Max(0, b.IP - ipp); } else { b.R = Math.Max(0, b.R - gv); b.IR = Math.Max(0, b.IR - ipp); }
                _dRepInt += ipp;
                done += gv; if (levy) _dLevyToCred += gv; else _dRepF += gv;
            }
            return done;
        }

        /// <summary>Zdjecie z kasy miasta (najwyzej kasa). Zwraca zdjete.</summary>
        private static long Out(Town t, long x)
        {
            if (x <= 0) return 0;
            int had = t.Gold;
            int ix = (int)Math.Min(int.MaxValue, Math.Min(x, Math.Max(0, had)));
            if (ix <= 0) return 0;
            t.ChangeGold(-ix);
            return Math.Max(0, (long)had - t.Gold);
        }

        // ------------------------------------------------------------ podatek nadzwyczajny rady
        private static void Levy(Settlement st, Town t, Book b, Settings s, int today, double arr, Hero lord)
        {
            double share = Clamp(s.TownDebtLevyShare, 0, 0.5);
            if (share <= 0) return;
            double target = b.Total + arr;
            if (target < 1) return;
            long bW = TownFolk.TdLevyBase(st, 0), bR = TownFolk.TdLevyBase(st, 1), bP = TownFolk.TdLevyBase(st, 2);
            double keep = Math.Max(0, s.TownDebtNotableKeep);
            double bN = 0;
            var ns = st.Notables;
            if (ns != null) for (int i = 0; i < ns.Count; i++) { var h = ns[i]; if (h != null && h.IsAlive && h.IsNotable && h.Gold > keep) bN += h.Gold - keep; }
            double baseAll = bW + bR + bP + bN;
            if (baseAll < MinNeed) return;                                     // nie ma z czego - bez zbiorki (proba jutro)
            double f = Math.Min(share, target / baseAll);
            long inW = 0, inR = 0, inP = 0, inN = 0;
            long[] bases = { bW, bR, bP };
            for (int cls = 0; cls < 3; cls++)
            {
                long want = (long)Math.Floor(bases[cls] * f);
                if (want <= 0) continue;
                long x = TownFolk.TdTake(st, cls, want, false);
                if (x <= 0) continue;
                try { t.ChangeGold((int)x); }
                catch (Exception e) { TownFolk.TdUndo(st, cls, x, false); Stumble("Levy(kasa)", e); continue; }
                if (cls == 0) inW = x; else if (cls == 1) inR = x; else inP = x;
            }
            _nPaid.Clear();
            if (ns != null && bN >= 1)
                for (int i = 0; i < ns.Count; i++)
                {
                    var h = ns[i];
                    if (h == null || !h.IsAlive || !h.IsNotable || h.Gold <= keep) continue;
                    long want = (long)Math.Floor((h.Gold - keep) * f);
                    if (want <= 0) continue;
                    int g0 = h.Gold;
                    h.ChangeHeroGold(-(int)Math.Min(int.MaxValue, want));
                    long x = (long)g0 - h.Gold;
                    if (x <= 0) continue;
                    CirculationWindows.NoteHeroGold(h, -x);
                    try { t.ChangeGold((int)x); }
                    catch (Exception e) { h.ChangeHeroGold((int)x); CirculationWindows.NoteHeroGold(h, x); Stumble("Levy(notabl)", e); continue; }
                    inN += x; _nPaid.Add(new KeyValuePair<Hero, long>(h, x));
                }
            long sum = inW + inR + inP + inN;
            if (sum <= 0) return;
            _dLevyW += inW; _dLevyR += inR; _dLevyP += inP; _dLevyN += inN; _dLevies++;
            // zebrane: wierzyciele (Bank pierwszy), potem zaleglosc firmy
            long left = sum;
            if (b.Total >= 1) left -= Repay(st, t, b, left, true);
            if (left > 0 && arr >= 1)
            {
                long tl, tc;
                long paid = DsTowns.TdPayArrears(st, left, out tl, out tc);
                _dToLord += tl; _dToCrown += tc; _dLevyToArr += paid; left -= paid;
            }
            if (left > 0) _dLevyKept += left;                                  // reszta (zaokraglenia) w kasie
            // koszt: lojalnosc, dobrobyt, relacje notabli z panem - pelny przy zbiorce Town Debt Levy Share x zapas kupcow (miara wielkosci miasta), mniejszy przy mniejszej
            // (miasto, ktorego mieszczanie oddali juz wszystko ponad zapas w pozyczkach, zbiera malo - i malo traci)
            double q = Math.Min(1.0, sum / Math.Max(1.0, share * Math.Max(1, TownPurse.Reserve(t))));
            float l0 = t.Loyalty, p0 = t.Prosperity;
            double dl = Clamp(s.TownDebtLevyLoyalty, 0, 50) * q;
            if (dl > 0 && l0 > LevyLoyaltyFloor) t.Loyalty = Math.Max(LevyLoyaltyFloor, l0 - (float)dl);
            double dp = Clamp(s.TownDebtLevyProsperity, 0, 0.5) * q * p0;
            if (dp > 0) t.Prosperity = Math.Max(0f, p0 - (float)dp);
            _dLevyLoy += l0 - t.Loyalty; _dLevyPros += p0 - t.Prosperity;
            int rel = Math.Max(0, Math.Min(20, s.TownDebtLevyRelation));
            if (rel > 0 && lord != null && lord.IsAlive)
                for (int i = 0; i < _nPaid.Count; i++)
                {
                    var h = _nPaid[i].Key;
                    if (h == null || h == lord) continue;
                    try
                    {
                        if (lord == Hero.MainHero) ChangeRelationAction.ApplyPlayerRelation(h, -rel, false, false);
                        else ChangeRelationAction.ApplyRelationChangeBetweenHeroes(h, lord, -rel, false);
                        _dLevyNotables++;
                    }
                    catch (Exception e) { Stumble("Levy(relacja)", e); }
                }
            b.LevyDay = today; b.LastLevy = sum;
            if (st.OwnerClan == Clan.PlayerClan)
                Say("The council of " + NameOf(st) + " cannot meet its debts and has levied an extraordinary tax of " + sum + " denars on its burghers and notables. The town's loyalty and prosperity fall.", false);
        }

        // ------------------------------------------------------------ notable z zapisu
        private static Hero HeroOf(string id)
        {
            if (id == null) return null;
            Hero h;
            if (_heroById != null && _heroById.TryGetValue(id, out h)) return h;
            return null;
        }

        /// <summary>Raz na sesje (pierwsza doba z dlugami u notabli): mapa id -> bohater dla wierzycieli z zapisu (jedno przejscie po zywych bohaterach).</summary>
        private static void ResolveNotables()
        {
            if (_heroResolved) return;
            bool any = false;
            foreach (var b in _b.Values) if (b.N.Count > 0) { any = true; break; }
            _heroById = new Dictionary<string, Hero>();
            _heroResolved = true;
            if (!any) return;
            var want = new HashSet<string>();
            foreach (var b in _b.Values) foreach (var k in b.N.Keys) want.Add(k);
            foreach (var h in Hero.AllAliveHeroes) if (h != null && h.StringId != null && want.Contains(h.StringId)) _heroById[h.StringId] = h;
        }

        private static void Say(string text, bool good)
        {
            try { InformationManager.DisplayMessage(new InformationMessage(text, good ? Colors.Green : Colors.Red)); } catch { }
        }

        // ------------------------------------------------------------ linia doby
        private static string N0(double v) { return ((long)Math.Round(v)).ToString(Inv); }

        private static void Line(int today, Settings s, double bankPool0, double ms)
        {
            try
            {
                int towns = 0, withN = 0, withB = 0, levyRecent = 0; double sP = 0, sR = 0, sB = 0, sN = 0, maxD = 0, sLim = 0; string maxName = null; double maxLim = 0;
                int gap = GapDays(s);
                foreach (var kv in _b)
                {
                    var b = kv.Value;
                    double tot = b.Total;
                    if (b.LevyDay != int.MinValue && today - b.LevyDay < gap) levyRecent++;
                    if (tot < 1) continue;
                    towns++; sP += b.P; sR += b.R; sB += b.B;
                    double n = 0; foreach (var v in b.N.Values) n += v[0];
                    sN += n; if (n >= 1) withN++; if (b.B >= 1) withB++;
                    double lim = b.St != null && b.St.Town != null ? Limit(b.St.Town, s) : 0;
                    sLim += lim;
                    if (tot > maxD) { maxD = tot; maxName = NameOf(b.St); maxLim = lim; }
                }
                double debt = sP + sR + sB + sN;
                long lent = _dLentP + _dLentR + _dLentN + _dLentB;
                long repaid = _dRepB + _dRepN + _dRepF + _dLevyToCred;
                double ctl = double.IsNaN(_debtPrev) ? 0 : debt - (_debtPrev + lent + _dInt - repaid);
                long levy = _dLevyW + _dLevyR + _dLevyP + _dLevyN;
                long levyUse = _dLevyToCred + _dLevyToArr + _dLevyKept;
                var sb = new StringBuilder(1400);
                sb.Append("Dlugi miast (m96-dlugi): dzien ").Append(today)
                  .Append(" | miasta z dlugiem ").Append(towns).Append(" (nowe ").Append(_dNewN).Append(", splacone do zera ").Append(_dClearedN).Append("), dlug razem ").Append(N0(debt))
                  .Append(" (limit razem ").Append(N0(sLim)).Append(") - wierzyciele: kupcy i patrycjat (P) ").Append(N0(sP)).Append(", rzemieslnicy (R) ").Append(N0(sR))
                  .Append(", notable ").Append(N0(sN)).Append(" (").Append(withN).Append(" miast), Bank ").Append(N0(sB)).Append(" (").Append(withB).Append(" miast)")
                  .Append("; najwiekszy ").Append(maxName ?? "-").Append(' ').Append(N0(maxD)).Append(" wobec limitu ").Append(N0(maxLim))
                  .Append(" | zaleglosci firm DS z kasy przed pozyczka (bez czesci, ktora kasa sama zaplaci w dzien raty): ").Append(N0(_dArrSeen)).Append(" u ").Append(_dArrTowns).Append(" miast")
                  .Append(" | pozyczki dzis ").Append(lent).Append(" w ").Append(_dBorrowN).Append(" miastach (P ").Append(_dLentP).Append(", R ").Append(_dLentR).Append(", notable ").Append(_dLentN)
                  .Append(", Bank ").Append(_dLentB).Append(" z puli ").Append(N0(bankPool0)).Append(") -> zaleglosci panom ").Append(_dToLord).Append(", koronie ").Append(_dToCrown).Append(" (lacznie z podatku), zostalo w kasie ").Append(_dLoanKept)
                  .Append(" | odsetki ").Append(N0(_dInt)).Append(" (Bank ").Append(N0(_dIntB)).Append(")")
                  .Append(" | splaty z nadwyzki kas (ponad zapas kupcow) ").Append(_dRepB + _dRepN + _dRepF).Append(" w ").Append(_dRepN2).Append(" miastach (Bank ").Append(_dRepB).Append(", notable ").Append(_dRepN)
                  .Append(", kiesy P/R ").Append(_dRepF).Append("; w tym odsetki do kies i notabli ").Append(_dRepInt).Append(")")
                  .Append(" | niewyplacalne ").Append(_dInsolvent).Append(" (zaleglosc bez kredytu ").Append(_dNoCredit).Append(" - zostalo ").Append(N0(_dArrLeft)).Append(")")
                  .Append(", podatki nadzwyczajne ").Append(_dLevies).Append(" na ").Append(levy).Append(" (W ").Append(_dLevyW).Append(", R ").Append(_dLevyR).Append(", P ").Append(_dLevyP).Append(", notable ").Append(_dLevyN)
                  .Append(") -> wierzycielom ").Append(_dLevyToCred).Append(", na zaleglosci ").Append(_dLevyToArr).Append(", w kasie ").Append(_dLevyKept)
                  .Append("; miast z podatkiem w ostatnich ").Append(gap).Append(" dobach ").Append(levyRecent)
                  .Append(" | skutki: dobrobyt -").Append(_dPros.ToString("0.0", Inv)).Append(" (dlug) -").Append(_dLevyPros.ToString("0.0", Inv)).Append(" (podatek), lojalnosc -").Append(_dLevyLoy.ToString("0.0", Inv))
                  .Append(", relacje notabli z panem -").Append(Math.Max(0, Math.Min(20, s.TownDebtLevyRelation))).Append(" x ").Append(_dLevyNotables)
                  .Append(" | bez zywego pana ").Append(_dNoLord).Append(", oblezone ").Append(_dSiege).Append(", dlug zmarlych notabli na kiese P ").Append(N0(_dMovedN))
                  .Append(" | kontrola: dlug - (wczoraj + pozyczone + odsetki - splacone) ").Append(N0(ctl)).Append(" (oczekiwane 0); podatek - (wierzycielom + na zaleglosci + w kasie) ")
                  .Append(levy - levyUse).Append(" (oczekiwane 0)")
                  .Append(" | czas ").Append(ms.ToString("0.000", Inv)).Append(" ms | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumblesSess).Append(").");
                Log.Info(sb.ToString());
                _debtPrev = debt;
                _stumbles = 0;
            }
            catch (Exception e) { Stumble("Line", e); }
        }

        // ------------------------------------------------------------ zapis
        private static string R(double v) { return v.ToString("R", Inv); }

        internal static string Export()
        {
            if (_b.Count == 0) return null;
            var sb = new StringBuilder(64 + _b.Count * 96);
            sb.Append(Ver);
            foreach (var kv in _b)
            {
                var b = kv.Value;
                if (kv.Key.IndexOfAny(new[] { '|', ';', '~', ',' }) >= 0) continue;
                sb.Append('|').Append(kv.Key).Append(';').Append(R(b.P)).Append(';').Append(R(b.R)).Append(';').Append(R(b.B)).Append(';').Append(R(b.IP)).Append(';').Append(R(b.IR))
                  .Append(';').Append(b.LevyDay.ToString(Inv)).Append(';').Append(b.Since.ToString(Inv)).Append(';').Append(b.LastLevy.ToString(Inv)).Append(';');
                bool first = true;
                foreach (var n in b.N)
                {
                    if (n.Key.IndexOfAny(new[] { '|', ';', '~', ',' }) >= 0) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(n.Key).Append('~').Append(R(n.Value[0])).Append('~').Append(R(n.Value[1]));
                }
            }
            return sb.ToString();
        }

        private static double D(string x) { double v; return double.TryParse(x, NumberStyles.Float, Inv, out v) && !double.IsNaN(v) && !double.IsInfinity(v) && v > 0 ? v : 0; }
        private static int I(string x, int def) { int v; return int.TryParse(x, NumberStyles.Integer, Inv, out v) ? v : def; }

        internal static void Import(string data)
        {
            _b.Clear(); _heroById = null; _heroResolved = false; _debtPrev = double.NaN;
            int n = 0, bad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) { Log.Info("Dlugi miast (m96-dlugi): zapis bez klucza arm_towndebt (stary zapis) - rady bez dlugow, start od pierwszej zaleglosci firmy."); return; }
                var parts = data.Split('|');
                if (parts.Length == 0 || parts[0] != Ver) { Log.Info("Dlugi miast (m96-dlugi): zapis w innej wersji (" + (parts.Length > 0 ? parts[0] : "?") + ") - pominiety, rady bez dlugow."); return; }
                for (int i = 1; i < parts.Length; i++)
                {
                    try
                    {
                        var a = parts[i].Split(';');
                        if (a.Length < 10 || string.IsNullOrEmpty(a[0])) { bad++; continue; }
                        var b = new Book { P = D(a[1]), R = D(a[2]), B = D(a[3]), IP = D(a[4]), IR = D(a[5]), LevyDay = I(a[6], int.MinValue), Since = I(a[7], -1) };
                        long ll; b.LastLevy = long.TryParse(a[8], NumberStyles.Integer, Inv, out ll) ? Math.Max(0, ll) : 0;
                        if (a[9].Length > 0)
                            foreach (var e in a[9].Split(','))
                            {
                                var q = e.Split('~');
                                if (q.Length < 3 || q[0].Length == 0) continue;
                                double v0 = D(q[1]); if (v0 < 1) continue;
                                b.N[q[0]] = new double[] { v0, Math.Min(v0, D(q[2])) };
                            }
                        b.IP = Math.Min(b.IP, b.P); b.IR = Math.Min(b.IR, b.R);
                        _b[a[0]] = b; n++;
                    }
                    catch { bad++; }
                }
                double tot = 0; foreach (var b in _b.Values) tot += b.Total;
                Log.Info("Dlugi miast (m96-dlugi): wczytane " + n + " ksiag rad (dlug razem " + N0(tot) + ", odrzucone " + bad + ").");
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        // ------------------------------------------------------------ gracz: menu miasta (Town affairs - m95-menu przenosi opcje)
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption("town", "arm_towndebt_town", "{=!}The council's debts", DebtCond, DebtCons, false, 9);
                Log.Info("Dlugi miast (m96-dlugi): pozycja \"The council's debts\" w menu miasta wpieta (klucz Town Debt; widoczna przy dlugu albo swiezym podatku nadzwyczajnym).");
            }
            catch (Exception e) { Log.Error("TownDebt.AddMenus", e); }
        }

        private static bool DebtCond(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                var st = Settlement.CurrentSettlement;
                if (st == null || !st.IsTown || st.StringId == null || !On) return false;
                Book b;
                if (!_b.TryGetValue(st.StringId, out b)) return false;
                return b.Total >= 1 || (b.LevyDay != int.MinValue && Today - b.LevyDay < GapDays(Settings.Current));
            }
            catch { return false; }
        }

        private static void DebtCons(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                var s = Settings.Current;
                if (st == null || st.Town == null || s == null) return;
                Book b; _b.TryGetValue(st.StringId, out b);
                var t = st.Town;
                double tot = b != null ? b.Total : 0, n = 0;
                if (b != null) foreach (var v in b.N.Values) n += v[0];
                double lim = Limit(t, s), year = Math.Max(28, IronBank.DaysPerYearNow());
                double intYear = b != null ? (b.P + b.R + n) * Clamp(s.TownDebtRate, 0, 0.5) + b.B * Clamp(s.TownDebtBankRate, 0, 1) : 0;
                double q = lim > 0 ? Math.Min(1.0, tot / lim) : 1.0;
                var sb = new StringBuilder(600);
                sb.Append("The council of ").Append(NameOf(st)).Append(" owes ").Append(N0(tot)).Append(" denars (it may owe at most ").Append(N0(lim)).Append(").\n\n");
                if (b != null && tot >= 1)
                {
                    sb.Append("Merchant families: ").Append(N0(b.P)).Append("\nCraftsmen: ").Append(N0(b.R)).Append("\nNotables: ").Append(N0(n)).Append("\nIron Bank of Braavos: ").Append(N0(b.B)).Append("\n\n");
                    sb.Append("Interest: about ").Append(N0(intYear)).Append(" denars a year (").Append(N0(intYear / year * 7)).Append(" a week).\n");
                    sb.Append("The council pays back from what its coffers hold above the merchants' stock (").Append(N0(TownPurse.Reserve(t))).Append(" denars), before the lord's dues can rise again.\n");
                    sb.Append("While in debt the town loses about ").Append((Clamp(s.TownDebtProsperityPerDay, 0, 10) * q).ToString("0.0", Inv)).Append(" prosperity a day.\n");
                    if (tot >= InsolventShare * lim) sb.Append("\nThe council is near the end of its credit: it will tax its burghers and notables to pay.\n");
                }
                if (b != null && b.LevyDay != int.MinValue)
                    sb.Append("\nLast extraordinary tax: ").Append(b.LastLevy).Append(" denars, ").Append(Math.Max(0, Today - b.LevyDay)).Append(" days ago.");
                InformationManager.ShowInquiry(new InquiryData("The council's debts", sb.ToString(), true, false, "Close", null, delegate { }, null), true);
            }
            catch (Exception e) { Log.Error("TownDebt.DebtCons", e); }
        }
    }
}
