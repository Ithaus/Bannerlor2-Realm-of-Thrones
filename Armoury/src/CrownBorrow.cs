using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// PACZKA 186 - KORONA POZYCZA W ZELAZNYM BANKU (decyzja Jeffa 10.10 "oczywiscie, ze korona powinna pozyczac"; projekt etapu 2 rozdz. 3, Q4b).
    /// Wylacznik CrownBorrows, czynny tylko z 165 (CrownIncome.On) i Bankiem (IronBankEnabled). Wylaczony = stan sprzed paczki: bez pozyczek, rat, odsetek
    /// i skutkow zaleglosci (dlug stoi w zapisie).
    ///  - KIEDY: krolestwo w wojnie, a reszta wplywow dnia nie starcza na nalezny zwrot zoldu (KingdomTreasury.WageRefund wola Lend) - skarbiec pozycza
    ///    brakujaca czesc zwrotu z kapitalu Banku i tego samego dnia oddaje ja rodom. Tylko zwrot (renty 180 biora tylko reszte wplywow dnia). Pokoj - nic nowego.
    ///  - ILE: dlug najwyzej CrownLoanLimitDays (180) x sredni podatek (srednia 84 dob naszych wplywow dnia 165 - powinnosci, danina, clo, 1/3 zaworu
    ///    zamkow, 1/9; bez 1/360 zapasu i bez jednorazowych przelewow); kredyt od 7. doby pomiaru. Bank daje koronom tylko z kapitalu ponad
    ///    CrownLoanBankFloor (2 mln), dziennie najwyzej 1/CrownLoanPoolDays (60) tej nadwyzki; przy niedoborze - proporcjonalnie do potrzeby.
    ///  - CENA jak kazda pozyczka Banku: krol 20% rocznie, +10 pp przy biegnacym dlugu, +15 pp po zaleglosci (srednia wazona); oplata 2% od wyplaty.
    ///  - RATA - pierwszy wydatek z wplywow dnia (krok zaraz po CrownIncome.Begin, przed darami 182): 1/CrownLoanRepayDays (182) najwiekszego dlugu tego
    ///    kredytu, najwyzej CrownLoanMaxIncomeShare (0.30) x sredni podatek; z wplywow dnia i 1/360 zapasu, nigdy z rezerwy.
    ///  - ZALEGLOSC: w 28 dobach z dlugiem zaplacone < 50% rat -> bez pozyczek dla korony i jej rodow (LimitFactor 0), rody krolestw w wojnie z dluznikiem -
    ///    limit Banku x CrownArrearsEnemyCredit (1.5). Koniec: 28 dob pelnych rat albo splata.
    ///  - Krolestwo zniszczone - dlug przepada (licznik "Bank stracil").
    /// Platnik -> odbiorca: kapital Banku -> skarbiec (pozyczka; zwrot zoldu do glow rodow w WageRefund); skarbiec -> kapital Banku (rata). Odsetki i oplata -
    /// tylko zapis dlugu. Linia "Kredyt korony (186)", liczniki w "Korona: wplywy dnia (165)" i "Obieg". Zapis: "arm_crown186" (SaveText).
    /// </summary>
    internal static class CrownBorrow
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int Ring = 28;               // okno zaleglosci: doby z dlugiem
        private const double ArrearsShare = 0.5;   // zaleglosc: zaplacone < 50% naleznych rat w oknie
        private const int TaxDays = 84;            // srednia podatku: pierwsze 84 doby zwykla, potem kroczaca
        private const int TaxWarmup = 7;           // doby pomiaru podatku przed pierwszym kredytem

        private sealed class Tax { public double Avg; public int N; }

        private sealed class Loan
        {
            public double Principal, Rate, Peak;   // dlug z odsetkami i oplatami; oprocentowanie roczne; najwiekszy dlug od ostatniej pelnej splaty (rata)
            public bool Arrears, EverArrears, Told;
            public int ArrearsDay = -1;
            public long Borrowed, Repaid;          // od poczatku (linia)
            public readonly long[] Due = new long[Ring], Paid = new long[Ring];
            public int Head, Filled;
            public string Name = "";
            // doba (linia)
            public long DLent, DDue, DPaid;
        }

        private static readonly Dictionary<string, Tax> _tax = new Dictionary<string, Tax>();     // id krolestwa -> sredni podatek
        private static readonly Dictionary<string, Loan> _loans = new Dictionary<string, Loan>(); // id krolestwa -> kredyt
        private static long _lost;                                                                // Bank stracil razem (krolestwa zniszczone)
        private static readonly List<Kingdom> _arrK = new List<Kingdom>();                       // krolestwa w zaleglosci (pamiec podreczna)
        private static bool _arrDirty = true;

        // liczniki doby
        internal static long LastLent, LastRepaid, LastLost; internal static int LastLentN;
        private static long _dLent, _dRepaid, _dDue, _dInterest, _dFees, _dLost, _dNeed, _dPool, _dBankCut, _dLimitCut;
        private static int _dLentN, _dNeedN, _dNoArrears, _dNoWarmup, _dNoLimit, _dNoBank, _dDueN, _dShortN, _dNewArr, _dEndArr, _dLostN;
        private static int _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.CrownBorrows && CrownIncome.On && IronBank.On; } }

        internal static void Reset()
        {
            _tax.Clear(); _loans.Clear(); _lost = 0; _arrK.Clear(); _arrDirty = true;
            ZeroDay(); ZeroLast(); _stumbles = 0; _importN = -1; _importBad = 0; _err.Clear();
        }

        internal static void ZeroLast() { LastLent = LastRepaid = LastLost = 0; LastLentN = 0; }

        private static void ZeroDay()
        {
            _dLent = _dRepaid = _dDue = _dInterest = _dFees = _dLost = _dNeed = _dPool = _dBankCut = _dLimitCut = 0;
            _dLentN = _dNeedN = _dNoArrears = _dNoWarmup = _dNoLimit = _dNoBank = _dDueN = _dShortN = _dNewArr = _dEndArr = _dLostN = 0;
            foreach (var kv in _loans) { kv.Value.DLent = 0; kv.Value.DDue = 0; kv.Value.DPaid = 0; }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CrownBorrow." + where, e); } catch { }
        }

        private static Kingdom FindKingdom(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        private static string NameOf(Kingdom k) { return k != null ? (k.Name != null ? k.Name.ToString() : k.StringId) : "?"; }

        private static double TaxAvg(string id) { Tax t; return id != null && _tax.TryGetValue(id, out t) ? Math.Max(0, t.Avg) : 0; }

        private static double RateFor(Loan l)
        {
            var s = Settings.Current;
            double r = s.IronBankRateKing + (l.Principal > 1 ? s.IronBankRatePerLoan : 0f) + (l.EverArrears ? s.IronBankRateAfterDefault : 0f);
            return Math.Max(0, r) / 100.0;
        }

        private static void RingSums(Loan l, out long due, out long paid)
        {
            due = 0; paid = 0;
            for (int i = 0; i < Ring; i++) { due += l.Due[i]; paid += l.Paid[i]; }
        }

        // ------------------------------------------------------------ odczyty dla Banku i drabiny 168
        private static List<Kingdom> ArrK()
        {
            if (!_arrDirty) return _arrK;
            _arrK.Clear();
            foreach (var kv in _loans)
            {
                if (!kv.Value.Arrears) continue;
                var k = FindKingdom(kv.Key);
                if (k != null && !k.IsEliminated) _arrK.Add(k);
            }
            _arrDirty = false;
            return _arrK;
        }

        /// <summary>Korona krolestwa rodu jest w zaleglosci wobec Banku (Bank nie pozycza jej rodom).</summary>
        internal static bool CrownInArrears(Clan c)
        {
            try
            {
                if (!On || c == null || c.Kingdom == null || c.Kingdom.StringId == null) return false;
                Loan l;
                return _loans.TryGetValue(c.Kingdom.StringId, out l) && l.Arrears;
            }
            catch { return false; }
        }

        /// <summary>Mnoznik limitu Banku rodu: 0 - korona jego krolestwa w zaleglosci; CrownArrearsEnemyCredit (1.5) - krolestwo w wojnie z korona w zaleglosci; inaczej 1.</summary>
        internal static float LimitFactor(Clan c)
        {
            try
            {
                if (!On || c == null) return 1f;
                var k = c.Kingdom;
                if (k == null || k.StringId == null) return 1f;
                var arr = ArrK();
                if (arr.Count == 0) return 1f;
                Loan l;
                if (_loans.TryGetValue(k.StringId, out l) && l.Arrears) return 0f;
                foreach (var dk in arr)
                    if (dk != null && dk != k && !dk.IsEliminated && k.IsAtWarWith(dk)) return Math.Max(1f, Settings.Current.CrownArrearsEnemyCredit);
                return 1f;
            }
            catch { return 1f; }
        }

        /// <summary>Dopisek do linii "Skarbce:" - dlug korony w Banku (pusty, gdy brak).</summary>
        internal static string LedgerNote(Kingdom k)
        {
            try
            {
                Loan l;
                if (k == null || k.StringId == null || !_loans.TryGetValue(k.StringId, out l) || l.Principal < 1) return "";
                return ", dlug w Banku (186) " + (long)l.Principal + " (rata dzis " + l.DPaid + "/" + l.DDue + ", pozyczone dzis " + l.DLent + (l.Arrears ? ", ZALEGLOSC" : "") + ")";
            }
            catch { return ""; }
        }

        // ------------------------------------------------------------ krok 1: sredni podatek, odsetki, rata (zaraz po CrownIncome.Begin, przed darami 182)
        internal static void Instalments()
        {
            ZeroDay();
            if (!On || Campaign.Current == null) return;
            var s = Settings.Current;
            int today = (int)CampaignTime.Now.ToDays;
            // sredni podatek kazdego krolestwa (takze bez dlugu - limit musi byc znany przy pierwszej pozyczce)
            try
            {
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated || k.StringId == null) continue;
                    var d = CrownIncome.DayOf(k);
                    if (d == null) continue;
                    Tax t;
                    if (!_tax.TryGetValue(k.StringId, out t)) { t = new Tax(); _tax[k.StringId] = t; }
                    t.N = Math.Min(TaxDays, t.N + 1);
                    t.Avg += (Math.Max(0, d.Own) - t.Avg) / t.N;
                }
            }
            catch (Exception e) { Stumble("Instalments(podatek)", e); }
            try
            {
                // recenzja 186: srednie krolestw, ktorych juz nie ma (bez dlugu) - poza zapisem
                var gone = new List<string>();
                foreach (var kv in _tax) { if (_loans.ContainsKey(kv.Key)) continue; var k = FindKingdom(kv.Key); if (k == null || k.IsEliminated) gone.Add(kv.Key); }
                foreach (var id in gone) _tax.Remove(id);
            }
            catch (Exception e) { Stumble("Instalments(porzadki)", e); }
            double year = Math.Max(1, IronBank.DaysPerYearNow());
            float share = Math.Max(0f, Math.Min(1f, s.CrownLoanMaxIncomeShare));
            double days = Math.Max(1f, s.CrownLoanRepayDays);
            var drop = new List<string>();
            foreach (var kv in _loans)
            {
                var l = kv.Value;
                try
                {
                    var k = FindKingdom(kv.Key);
                    if (k == null || k.IsEliminated)
                    {
                        // krolestwo zniszczone - dlug przepada (zloto juz przeszlo z Banku do skarbca i dalej; nic nie znika i nic nie powstaje)
                        long lost = (long)Math.Max(0, l.Principal);
                        if (lost > 0)
                        {
                            _dLost += lost; _dLostN++; _lost += lost;
                            Log.Info("Kredyt korony (186): " + l.Name + " (" + kv.Key + ") zniszczone - dlug w Banku " + lost + " zl przepada (Bank stracil razem " + _lost + ").");
                        }
                        drop.Add(kv.Key); continue;
                    }
                    l.Name = NameOf(k);
                    if (l.Principal < 1) { if (!l.EverArrears) drop.Add(kv.Key); continue; }
                    double interest = l.Principal * l.Rate / year;
                    l.Principal += interest; _dInterest += (long)Math.Round(interest);
                    var cd = CrownIncome.DayOf(k);
                    if (cd == null) continue;   // recenzja 186: krolestwo bez dnia 165 (wyjatek w Begin) - dzis bez raty i bez wpisu w oknie zaleglosci
                    double want = Math.Min(Math.Min(l.Peak / days, share * TaxAvg(kv.Key)), l.Principal);
                    long due = (long)Math.Ceiling(Math.Max(0, want));
                    long pay = 0;
                    if (due > 0)
                    {
                        pay = Math.Min(due, Math.Min(CrownIncome.LeftFor(k), (long)Math.Max(0, k.KingdomBudgetWallet)));
                        if (pay > 0)
                        {
                            int ip = (int)Math.Min(int.MaxValue, pay);
                            pay = ip;
                            k.KingdomBudgetWallet -= ip;     // najpierw skarbiec, potem Bank: Bank dostaje dokladnie rate
                            IronBank.CapitalAdd(ip);
                            l.Principal -= ip; l.Repaid += ip;
                            CrownIncome.Spent(k, ip);
                            cd.LoanOut += ip;
                        }
                    }
                    l.DDue = due; l.DPaid = pay;
                    _dDue += due; _dRepaid += pay;
                    if (due > 0) { _dDueN++; if (pay < due) _dShortN++; }
                    // okno 28 dob z dlugiem
                    l.Due[l.Head] = due; l.Paid[l.Head] = pay;
                    l.Head = (l.Head + 1) % Ring; if (l.Filled < Ring) l.Filled++;
                    long sd, sp; RingSums(l, out sd, out sp);
                    if (l.Principal < 1)
                    {
                        // splacone w calosci - koniec kredytu (zaleglosc konczy sie, +15 pp zostaje na przyszle pozyczki)
                        if (l.Arrears) { l.Arrears = false; _arrDirty = true; _dEndArr++; Log.Info("Kredyt korony (186): " + l.Name + " splacilo dlug w Banku - koniec zaleglosci."); }
                        l.Principal = 0; l.Peak = 0; l.Told = false; l.Filled = 0; l.Head = 0;
                        Array.Clear(l.Due, 0, Ring); Array.Clear(l.Paid, 0, Ring);
                        if (!l.EverArrears) drop.Add(kv.Key);
                        continue;
                    }
                    if (!l.Arrears && l.Filled >= Ring && sd > 0 && sp < ArrearsShare * sd)
                    {
                        l.Arrears = true; l.EverArrears = true; l.ArrearsDay = today; _arrDirty = true; _dNewArr++;
                        Log.Info("Kredyt korony (186): " + l.Name + " - ZALEGLOSC: w 28 dobach zaplacilo " + sp + " z " + sd + " zl rat (< 50%) - Bank nie pozycza koronie ani jej rodom,"
                                 + " rody krolestw w wojnie z nia maja limit Banku x" + Math.Max(1f, s.CrownArrearsEnemyCredit).ToString("0.0#", Inv) + "; dlug " + (long)l.Principal + ".");
                        if (Clan.PlayerClan != null && Clan.PlayerClan.Kingdom == k)
                            Log.Player("Your crown has paid less than half of what it owes the Iron Bank for 28 days. The Bank lends nothing more to the crown or to its houses - and it now lends to your enemies.", true);
                    }
                    else if (l.Arrears && l.Filled >= Ring && sd > 0 && sp >= sd)
                    {
                        l.Arrears = false; _arrDirty = true; _dEndArr++;
                        Log.Info("Kredyt korony (186): " + l.Name + " - koniec zaleglosci (28 dob pelnych rat); dlug " + (long)l.Principal + ", nowe pozyczki +"
                                 + s.IronBankRateAfterDefault.ToString("0", Inv) + " pp.");
                    }
                }
                catch (Exception e) { Stumble("Instalments(kredyt)", e); }
            }
            foreach (var id in drop) { _loans.Remove(id); _arrDirty = true; }
        }

        // ------------------------------------------------------------ krok 2: kredyt na brakujaca czesc zwrotu (KingdomTreasury.WageRefund, przed wyplata)
        /// <summary>need: krolestwo w wojnie -> brak zwrotu dzis (nalezny minus reszta wplywow dnia). Zwraca pozyczone (juz w skarbcach).</summary>
        internal static Dictionary<Kingdom, long> Lend(Dictionary<Kingdom, long> need)
        {
            var got = new Dictionary<Kingdom, long>();
            if (!On || need == null || need.Count == 0) return got;
            var s = Settings.Current;
            try
            {
                double fee = Math.Max(0f, s.IronBankLoanFeePercent) / 100.0;
                var ask = new List<KeyValuePair<Kingdom, long>>(); long askSum = 0;
                foreach (var kv in need)
                {
                    try
                    {
                        var k = kv.Key; long n = kv.Value;
                        if (k == null || k.IsEliminated || k.StringId == null || n <= 0) continue;
                        if (CrownIncome.DayOf(k) == null) continue;   // recenzja 186: bez dnia 165 reszta wplywow to 0 - kredyt wzialby caly zwrot
                        _dNeed += n; _dNeedN++;
                        Loan l; _loans.TryGetValue(k.StringId, out l);
                        if (l != null && l.Arrears) { _dNoArrears++; continue; }
                        Tax t;
                        if (!_tax.TryGetValue(k.StringId, out t) || t.N < TaxWarmup) { _dNoWarmup++; continue; }
                        double room = Math.Max(0f, s.CrownLoanLimitDays) * Math.Max(0, t.Avg) - (l != null ? l.Principal : 0);
                        long x = (long)Math.Min((double)n, Math.Floor(room / (1 + fee)));
                        if (x <= 0) { _dNoLimit++; continue; }
                        if (x < n) _dLimitCut += n - x;
                        ask.Add(new KeyValuePair<Kingdom, long>(k, x)); askSum += x;
                    }
                    catch (Exception e) { Stumble("Lend(krolestwo)", e); }
                }
                double free = IronBank.FreeCapital - Math.Max(0, s.CrownLoanBankFloor);
                double pool = Math.Floor(Math.Max(0, free) / Math.Max(1f, s.CrownLoanPoolDays));
                _dPool = (long)pool;   // recenzja 186: pula dnia w linii takze bez chetnych
                if (askSum <= 0) return got;
                if (pool < 1) { _dNoBank += ask.Count; _dBankCut += askSum; return got; }
                double f = askSum <= pool ? 1.0 : pool / askSum;
                if (f < 1.0) _dBankCut += askSum - (long)pool;
                foreach (var kv in ask)
                {
                    try
                    {
                        var k = kv.Key;
                        long y = (long)Math.Floor(kv.Value * f);
                        if (y <= 0) continue;
                        int iy = (int)Math.Min(int.MaxValue, y);
                        Loan l;
                        if (!_loans.TryGetValue(k.StringId, out l)) { l = new Loan(); _loans[k.StringId] = l; }
                        l.Name = NameOf(k);
                        bool running = l.Principal > 1;
                        double r = RateFor(l);
                        double fe = iy * fee;
                        // dlug zapisany przed ruchem zlota; najpierw Bank, potem skarbiec - skarbiec dostaje dokladnie tyle, ile zeszlo z kapitalu
                        l.Rate = running ? (l.Rate * l.Principal + r * iy) / (l.Principal + iy) : r;
                        l.Principal += iy + fe; l.Peak = Math.Max(l.Peak, l.Principal);
                        l.Borrowed += iy; l.DLent += iy;
                        IronBank.CapitalAdd(-iy);
                        try { k.KingdomBudgetWallet += iy; }
                        catch { IronBank.CapitalAdd(iy); l.Principal -= iy + fe; l.Borrowed -= iy; l.DLent -= iy; throw; }   // recenzja 186: zloto wraca do Banku, dlug cofniety
                        got[k] = iy;
                        _dLent += iy; _dLentN++; _dFees += (long)Math.Round(fe);
                        var cd = CrownIncome.DayOf(k);
                        if (cd != null) cd.LoanIn += iy;
                        if (!running)
                        {
                            Log.Info("Kredyt korony (186): " + l.Name + " pozycza w Zelaznym Banku " + iy + " zl na zwrot zoldu (brak dzis " + need[k] + "), "
                                     + (l.Rate * 100).ToString("0", Inv) + "% rocznie + oplata " + s.IronBankLoanFeePercent.ToString("0.#", Inv) + "%; limit "
                                     + (long)(Math.Max(0f, s.CrownLoanLimitDays) * TaxAvg(k.StringId)) + " (" + s.CrownLoanLimitDays.ToString("0", Inv) + " x sredni podatek "
                                     + (long)TaxAvg(k.StringId) + "); rata 1/" + s.CrownLoanRepayDays.ToString("0", Inv) + " najwiekszego dlugu, najwyzej "
                                     + (Math.Max(0f, Math.Min(1f, s.CrownLoanMaxIncomeShare)) * 100).ToString("0", Inv) + "% podatku.");
                            if (!l.Told && Clan.PlayerClan != null && k.RulingClan == Clan.PlayerClan)
                            {
                                l.Told = true;
                                Log.Player("The Iron Bank of Braavos lends your crown " + iy + " denars to pay the wage refunds of your vassals at war. From now on the crown repays the Bank first out of its daily income - at most "
                                           + (Math.Max(0f, Math.Min(1f, s.CrownLoanMaxIncomeShare)) * 100).ToString("0", Inv) + "% of its taxes a day. The Bank will have its due.");
                            }
                        }
                    }
                    catch (Exception e) { Stumble("Lend(wyplata)", e); }
                }
            }
            catch (Exception e) { Stumble("Lend", e); }
            return got;
        }

        // ------------------------------------------------------------ linia "Kredyt korony (186)" (po CrownIncome.End)
        internal static void Report()
        {
            try
            {
                var s = Settings.Current;
                LastLent = _dLent; LastLentN = _dLentN; LastRepaid = _dRepaid; LastLost = _dLost;
                if (s == null || !s.LogEnabled || Campaign.Current == null) return;
                bool on = On;
                if (!on && _loans.Count == 0) return;
                int today = (int)CampaignTime.Now.ToDays;
                long debt = 0, borrowed = 0, repaid = 0; int nDebt = 0, nArr = 0;
                var arrNames = new List<string>();
                var parts = new List<KeyValuePair<double, string>>();
                foreach (var kv in _loans)
                {
                    var l = kv.Value;
                    borrowed += l.Borrowed; repaid += l.Repaid;
                    if (l.Principal < 1) continue;
                    debt += (long)l.Principal; nDebt++;
                    if (l.Arrears) { nArr++; arrNames.Add(l.Name + " (od dnia " + l.ArrearsDay + ")"); }
                    long sd, sp; RingSums(l, out sd, out sp);
                    parts.Add(new KeyValuePair<double, string>(l.Principal, l.Name + " " + (long)l.Principal + "/" + (long)(Math.Max(0f, s.CrownLoanLimitDays) * TaxAvg(kv.Key))
                              + " " + (l.Rate * 100).ToString("0", Inv) + "% rata " + l.DPaid + "/" + l.DDue + (l.DLent > 0 ? " +" + l.DLent : "")
                              + (sd > 0 ? " raty28 " + (100.0 * sp / sd).ToString("0", Inv) + "%" : "") + (l.Arrears ? " ZALEGLOSC" : "")));
                }
                parts.Sort((x, y) => y.Key.CompareTo(x.Key));
                var txt = new List<string>(); foreach (var p in parts) txt.Add(p.Value);
                double cap = IronBank.FreeCapital;
                var sb = new StringBuilder(1200);
                sb.Append("Kredyt korony (186): dzien ").Append(today);
                if (!on) sb.Append(" | WYLACZONE (CrownBorrows albo 165/Bank) - bez rat, odsetek i pozyczek, dlug stoi");
                sb.Append(" | pozyczono dzis ").Append(_dLent).Append(" zl u ").Append(_dLentN).Append(" krolestw (brak zwrotu ").Append(_dNeed).Append(" u ").Append(_dNeedN)
                  .Append("; bez kredytu: zaleglosc ").Append(_dNoArrears).Append(", pomiar podatku < ").Append(TaxWarmup).Append(" dob ").Append(_dNoWarmup)
                  .Append(", na limicie ").Append(_dNoLimit).Append(", limit przycial o ").Append(_dLimitCut)
                  .Append("; pula Banku dzis ").Append(_dPool).Append(" = 1/").Append(Math.Max(1f, s.CrownLoanPoolDays).ToString("0", Inv)).Append(" kapitalu ponad ").Append(Math.Max(0, s.CrownLoanBankFloor))
                  .Append(", przycieta o ").Append(_dBankCut).Append(_dNoBank > 0 ? " (pula 0 - odmowa " + _dNoBank + ")" : "").Append(")")
                  .Append(" | splacono ").Append(_dRepaid).Append(" zl (raty nalezne ").Append(_dDue).Append(" u ").Append(_dDueN).Append(" krolestw, zaplacone ponizej raty ").Append(_dShortN)
                  .Append("); odsetki narosle ").Append(_dInterest).Append(", oplaty ").Append(_dFees)
                  .Append(" | dlug razem ").Append(debt).Append(" u ").Append(nDebt).Append(" krolestw; w zaleglosci ").Append(nArr)
                  .Append(arrNames.Count > 0 ? ": " + string.Join(", ", arrNames.ToArray()) : "")
                  .Append("; nowe zaleglosci dzis ").Append(_dNewArr).Append(", koniec zaleglosci ").Append(_dEndArr)
                  .Append(" | Bank: kapital ").Append((long)cap).Append(", prog kredytu koron ").Append(Math.Max(0, s.CrownLoanBankFloor))
                  .Append(", Bank stracil (krolestwa zniszczone) dzis ").Append(_dLost).Append(" (").Append(_dLostN).Append("), razem ").Append(_lost)
                  .Append(" | od poczatku: pozyczono ").Append(borrowed).Append(", splacono ").Append(repaid)
                  .Append(" | na krolestwo (dlug/limit, oprocentowanie, rata zaplacona/nalezna, +pozyczone dzis, raty 28 dob): ").Append(txt.Count > 0 ? string.Join(", ", txt.ToArray()) : "-")
                  .Append(_stumbles > 0 ? " | potkniecia " + _stumbles : "").Append('.');
                if (_importN >= 0) { sb.Append(" Wczytano z zapisu: kredytow ").Append(_importN).Append(" (bledne ").Append(_importBad).Append(")."); _importN = -1; }
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Report", e); }
        }

        // ------------------------------------------------------------ zapis (SaveText, "arm_crown186")
        /// <summary>"v1|stracil|id=srednia:n;...|id>dlug>proc>szczyt>flagi>doba zaleglosci>pozyczono>splacono>glowa>wypelnienie>rata0,...>zaplata0,...;..."
        /// flagi: 1 zaleglosc, 2 kiedys zaleglosc, 4 komunikat gracza.</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(64 + _tax.Count * 32 + _loans.Count * 400);
                sb.Append("v1|").Append(_lost.ToString(Inv)).Append('|');
                bool first = true;
                foreach (var kv in _tax)
                {
                    if (kv.Key == null || kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append('=').Append(kv.Value.Avg.ToString("0.##", Inv)).Append(':').Append(kv.Value.N.ToString(Inv));
                }
                sb.Append('|');
                first = true;
                foreach (var kv in _loans)
                {
                    var l = kv.Value;
                    if (kv.Key == null || kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    int flags = (l.Arrears ? 1 : 0) | (l.EverArrears ? 2 : 0) | (l.Told ? 4 : 0);
                    sb.Append(kv.Key).Append('>').Append(l.Principal.ToString("0.##", Inv)).Append('>').Append(l.Rate.ToString("0.######", Inv)).Append('>').Append(l.Peak.ToString("0.##", Inv))
                      .Append('>').Append(flags.ToString(Inv)).Append('>').Append(l.ArrearsDay.ToString(Inv)).Append('>').Append(l.Borrowed.ToString(Inv)).Append('>').Append(l.Repaid.ToString(Inv))
                      .Append('>').Append(l.Head.ToString(Inv)).Append('>').Append(l.Filled.ToString(Inv)).Append('>');
                    for (int i = 0; i < Ring; i++) { if (i > 0) sb.Append(','); sb.Append(l.Due[i].ToString(Inv)); }
                    sb.Append('>');
                    for (int i = 0; i < Ring; i++) { if (i > 0) sb.Append(','); sb.Append(l.Paid[i].ToString(Inv)); }
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }
        private static readonly char[] Bad = { '|', ';', '=', '>', ':', ',' };

        internal static void Import(string data)
        {
            _tax.Clear(); _loans.Clear(); _lost = 0; _arrDirty = true; _importN = 0; _importBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 4 || f[0] != "v1") { _importBad++; return; }
                long.TryParse(f[1], NumberStyles.Integer, Inv, out _lost);
                if (f[2].Length > 0)
                    foreach (var p in f[2].Split(';'))
                    {
                        var kv = p.Split('=');
                        if (kv.Length != 2 || kv[0].Length == 0) { _importBad++; continue; }
                        var an = kv[1].Split(':'); double avg; int n;
                        if (an.Length == 2 && double.TryParse(an[0], NumberStyles.Float, Inv, out avg) && int.TryParse(an[1], NumberStyles.Integer, Inv, out n))
                            _tax[kv[0]] = new Tax { Avg = Math.Max(0, avg), N = Math.Max(0, Math.Min(TaxDays, n)) };
                        else _importBad++;
                    }
                if (f[3].Length > 0)
                    foreach (var p in f[3].Split(';'))
                    {
                        var x = p.Split('>');
                        double pr, rate, peak; int flags, aday, head, filled; long bor, rep;
                        if (x.Length != 12 || x[0].Length == 0
                            || !double.TryParse(x[1], NumberStyles.Float, Inv, out pr) || !double.TryParse(x[2], NumberStyles.Float, Inv, out rate) || !double.TryParse(x[3], NumberStyles.Float, Inv, out peak)
                            || !int.TryParse(x[4], NumberStyles.Integer, Inv, out flags) || !int.TryParse(x[5], NumberStyles.Integer, Inv, out aday)
                            || !long.TryParse(x[6], NumberStyles.Integer, Inv, out bor) || !long.TryParse(x[7], NumberStyles.Integer, Inv, out rep)
                            || !int.TryParse(x[8], NumberStyles.Integer, Inv, out head) || !int.TryParse(x[9], NumberStyles.Integer, Inv, out filled))
                        { _importBad++; continue; }
                        var l = new Loan
                        {
                            Principal = Math.Max(0, pr), Rate = Math.Max(0, rate), Peak = Math.Max(0, peak), Arrears = (flags & 1) != 0, EverArrears = (flags & 2) != 0, Told = (flags & 4) != 0,
                            ArrearsDay = aday, Borrowed = bor, Repaid = rep, Head = Math.Max(0, Math.Min(Ring - 1, head)), Filled = Math.Max(0, Math.Min(Ring, filled)), Name = x[0]
                        };
                        var dd = x[10].Split(','); var pp = x[11].Split(',');
                        for (int i = 0; i < Ring; i++)
                        {
                            long v;
                            if (i < dd.Length && long.TryParse(dd[i], NumberStyles.Integer, Inv, out v)) l.Due[i] = Math.Max(0, v);
                            if (i < pp.Length && long.TryParse(pp[i], NumberStyles.Integer, Inv, out v)) l.Paid[i] = Math.Max(0, v);
                        }
                        _loans[x[0]] = l; _importN++;
                    }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
