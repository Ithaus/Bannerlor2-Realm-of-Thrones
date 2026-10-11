using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// KROK 3.5 (PLAN-DO-KONCA-MODA-2026-10-09, etap 3; PROJEKT-EKONOMIA-OBIEG 4.1 wiersz 21; audyt 05.10 pieniadz/P6; audyt 09.10 12-DOCHODY B9/D-8)
    /// - RADA BK: PENSJE RADNYCH OD DOCHODU, KTORY PAN NAPRAWDE MA, I DO KAZDEGO RADNEGO.
    ///
    /// Jak to liczy BK (dekompilacja BannerKings.dll, Redux):
    ///  - kwota: CouncilMember.DueWage (pole w zapisie) = wynik podatku osady x AdministrativeCosts() (1% zwykla pozycja, 2.75% rdzen - Marshall,
    ///    Steward, Spymaster, Chancellor, Spiritual, Spouse - 4.5% rdzen rady krolewskiej). Pisze ja BKTaxModel.CalculateDueWages przy KAZDYM liczeniu
    ///    podatku miasta lub zamku (CalculateTownTax - od pelnego podatku klas ludnosci BK, zanim nasz PopulationLaw.TownTaxPostfix zastapi go renta,
    ///    wpis 49) i wsi (CalculateVillageTaxFromIncome - prawdziwy podatek wsi). Ostatnia policzona osada wygrywa. Rod bez miasta i zamku nie ma osad,
    ///    wiec BK juz nigdy jej nie przelicza - zostaje stara kwota z czasow, gdy rod mial lenno.
    ///  - platnik: BKClanFinanceModel.AddExpenses - linia "Council wages" = minus suma DueWage zajetych pozycji w saldzie rodu (kiesa glowy rodu).
    ///  - odbiorca: radny NIE-lord (notabl, towarzysz) dostaje Member.Gold += DueWage od razu (tylko przy prawdziwym rozliczeniu, applyWithdrawals);
    ///    radny-lord dostaje tylko jako glowa rodu - linia "Councillor role" w saldzie WLASNEGO rodu (AddIncomes, kwota DueWage z chwili JEGO
    ///    rozliczenia, pozycje z CourtManager.GetHeroPositions). Lord, ktory nie jest glowa rodu (malzonka - pozycja rdzenia "Spouse", dzieci, bracia,
    ///    czlonkowie rodow wasali), NIE DOSTAJE NIC - rod placi w nicosc. Radny martwy do dziennego przegladu rady - BK placi i tak (zloto do zmarlego).
    ///
    /// Co robimy (oba wylaczniki domyslnie wlaczone; latki zawsze wpiete - przy wylaczonych tylko licza do logu):
    ///  - 3.5a CouncilWagesRealBase: (1) po zastapieniu podatku miasta/zamku renta (TownTaxPostfix) przeliczamy DueWage rady wlasciciela od tego, co
    ///    pan naprawde dostaje z tej osady: zachowane linie podatku (warsztaty cudzych wlascicieli, kopalnie, materialy budow) + renta miasta tej doby
    ///    (PopulationLaw.RentOf) albo czesc pana z zaworu zamku (CastlePurse.DuesOf). Ta sama regula BK (stawka x podatek osady, ostatnia osada
    ///    wygrywa), tylko od podatku, ktory istnieje. Wies bez zmian (podatek wsi BK jest prawdziwy). (2) Rod bez miasta i zamku: pensje rady 0
    ///    (BK nie ma od czego liczyc; kwota wraca sama, gdy rod dostanie lenno i BK przeliczy podatek).
    ///  - 3.5b CouncilWagesPaidToAll: przy prawdziwym rozliczeniu platnika kazdy radny-lord dostaje DueWage wprost do kiesy (ta sama droga, ktora BK
    ///    placi nie-lordom), takze glowa innego rodu - a jej "Councillor role" w jej wlasnym rozliczeniu jest zdejmowane linia o tej samej nazwie
    ///    (tylko applyWithdrawals - podglad w UI i nasze miary D bez zmian). Kwota zaplacona = kwota naliczona platnikowi, co do monety, w tej samej
    ///    chwili. Radny poza swiatem: platnik dostaje zwrot (linia "Council wages" na plus), a zloto, ktore BK wlozyl zmarlemu nie-lordowi, wraca.
    /// Platnik -> odbiorca: kiesa glowy rodu (saldo rodu) -> kiesa radnego. Rodzina: pensja malzonki/dzieci zostaje w kiesach rodziny (166 "kiesa
    /// rodziny" dopelnia z nich kiese glowy). Nic z niczego (poprawka po recenzji R1): gdy saldo platnika nie zmiesci sie w kiesie glowy, brak obciaza
    /// najpierw zold rodu (SoldierPay.Settle) i splate dlugu wobec korony (CrownIncome.ClanTickEnd) - te same liczby co tam; reszta braku to pensje rady
    /// bez pokrycia - przy 3.5b WL cofane z kies radnych (nie-lordow i lordow) proporcjonalnie, postfiksem DailyTickClan w oknie pomiaru rozliczenia rodu.
    /// Linia logu "Rada (3.5)" raz na dobe (po ksiedze pieniadza - to samo okno co "Pieniadz swiata (rody - przyczyny)", gdzie jest para).
    /// </summary>
    internal static class CouncilWages35
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool BaseOn { get { var s = Settings.Current; return s != null && s.CouncilWagesRealBase; } }
        internal static bool PayOn { get { var s = Settings.Current; return s != null && s.CouncilWagesPaidToAll; } }

        // ------------------------------------------------------------ latki (zakladane w kampanii; zyja przez caly proces)
        private static Harmony _harmony;
        internal static void SetHarmony(Harmony h) { _harmony = h; }
        private static bool _hooksTried, _expWired, _incWired, _tickWired;
        private static string _hookNote = "-";

        // ------------------------------------------------------------ BK przez refleksje (typy i metody - raz na proces; obiekty BK czytane za kazdym razem)
        private static bool _bkTried, _bkOk;
        private static PropertyInfo _pCfgInstance, _pCourt, _pPositions, _pMember, _pDueWage;
        private static MethodInfo _mGetCouncil, _mHeroPositions, _mAdm;
        private static TextObject _txtWages, _txtRole;

        // ------------------------------------------------------------ liczniki doby (zerowane w Daily i na starcie sesji)
        private static long _charged, _toCommon, _toLeaders, _toFamily, _toOtherLords, _toGone, _refund, _roleCut;
        private static int _chargedN, _commonN, _leadersN, _familyN, _otherLordsN, _goneN, _payerClans, _roleN;
        private static long _staleGold; private static int _staleN, _staleClans;
        // poprawka po recenzji (R1): pensje wyplacone w biezacym rozliczeniu platnika (BK nie-lordom, 3.5b lordom; bez glowy platnika - u niej to obieg
        // w jednej kiesie) - postfiks DailyTickClan cofa z nich czesc bez pokrycia
        private struct Pay { public Hero Who; public int Gold; public bool Lord; }
        private static readonly List<Pay> _pend = new List<Pay>();
        private static Clan _pendClan; private static CampaignTime _pendTime;
        private static long _gapRest, _revCommon, _revLords, _revMiss, _revOff; private static int _gapClans, _revN, _blindClans;
        private static long _baseOld, _baseNew, _baseCalls, _baseRentCalls;   // Interlocked - podatek miasta bywa liczony z watkow AI
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles);
            try { lock (_err) { if (!_err.Add(where)) return; } Log.Error("CouncilWages35." + where, e); } catch { }
        }

        private static void ZeroDay()
        {
            _charged = _toCommon = _toLeaders = _toFamily = _toOtherLords = _toGone = _refund = _roleCut = 0;
            _chargedN = _commonN = _leadersN = _familyN = _otherLordsN = _goneN = _payerClans = _roleN = 0;
            _staleGold = 0; _staleN = 0; _staleClans = 0;
            _gapRest = _revCommon = _revLords = _revMiss = _revOff = 0; _gapClans = _revN = _blindClans = 0; _pend.Clear(); _pendClan = null;
            Interlocked.Exchange(ref _baseOld, 0); Interlocked.Exchange(ref _baseNew, 0);
            Interlocked.Exchange(ref _baseCalls, 0); Interlocked.Exchange(ref _baseRentCalls, 0);
            _stumbles = 0;
        }

        private static bool BkResolve()
        {
            if (_bkOk) return true;
            if (_bkTried) return false;
            _bkTried = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var memT = AccessTools.TypeByName("BannerKings.Managers.Court.CouncilMember");
                if (cfgT == null || memT == null) return false;
                _pCfgInstance = AccessTools.Property(cfgT, "Instance");
                _pCourt = AccessTools.Property(cfgT, "CourtManager");
                var cmT = _pCourt != null ? _pCourt.PropertyType : null;
                _mGetCouncil = cmT != null ? AccessTools.Method(cmT, "GetCouncil", new[] { typeof(Clan) }) : null;
                _mHeroPositions = cmT != null ? AccessTools.Method(cmT, "GetHeroPositions", new[] { typeof(Hero) }) : null;
                var cdT = _mGetCouncil != null ? _mGetCouncil.ReturnType : null;
                _pPositions = cdT != null ? AccessTools.Property(cdT, "Positions") : null;
                _pMember = AccessTools.Property(memT, "Member");
                _pDueWage = AccessTools.Property(memT, "DueWage");
                _mAdm = AccessTools.Method(memT, "AdministrativeCosts", Type.EmptyTypes);
                _bkOk = _pCfgInstance != null && _pCourt != null && _mGetCouncil != null && _mHeroPositions != null && _pPositions != null
                        && _pMember != null && _pDueWage != null && _pDueWage.CanWrite && _mAdm != null;
                // te same identyfikatory co BK (BKClanFinanceModel) - ksiega obiegu (CirculationWindows) czyta linie modelu po nazwie
                _txtWages = new TextObject("{=L0Dwod0e}Council wages");
                _txtRole = new TextObject("{=WvhXhUFS}Councillor role");
            }
            catch (Exception e) { _bkOk = false; Stumble("BkResolve", e); }
            return _bkOk;
        }

        private static object Court()
        {
            var cfg = _pCfgInstance.GetValue(null, null);
            return cfg != null ? _pCourt.GetValue(cfg, null) : null;
        }

        /// <summary>Pozycje rady rodu (lista BK CouncilData.Positions) albo null.</summary>
        private static IList PositionsOf(Clan clan)
        {
            var court = Court();
            if (court == null || clan == null) return null;
            var council = _mGetCouncil.Invoke(court, new object[] { clan });
            return council != null ? _pPositions.GetValue(council, null) as IList : null;
        }

        private static int Wage(object pos) { return (int)_pDueWage.GetValue(pos, null); }
        private static void SetWage(object pos, int v) { _pDueWage.SetValue(pos, v, null); }

        // ------------------------------------------------------------ zakladanie latek (ArmouryBehavior, start sesji)
        internal static void EnsureHooks()
        {
            ZeroDay();   // nowa sesja / kampania - liczniki od zera (latki zostaja)
            if (_hooksTried || _harmony == null || Campaign.Current == null) return;
            _hooksTried = true;
            var wired = new List<string>(); var missing = new List<string>();
            try
            {
                if (!BkResolve()) missing.Add("rada BK (CourtManager / CouncilMember)");
                var fT = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKClanFinanceModel");
                var args = new[] { typeof(Clan), typeof(ExplainedNumber).MakeByRefType(), typeof(bool) };
                var exp = fT != null ? AccessTools.Method(fT, "AddExpenses", args) : null;
                var inc = fT != null ? AccessTools.Method(fT, "AddIncomes", args) : null;
                if (_bkOk && exp != null)
                {
                    _harmony.Patch(exp, prefix: new HarmonyMethod(typeof(CouncilWages35), nameof(ExpensesPrefix)),
                                        postfix: new HarmonyMethod(typeof(CouncilWages35), nameof(ExpensesPostfix)) { priority = Priority.Last });
                    _expWired = true; wired.Add("platnik (BK AddExpenses)");
                }
                else missing.Add("platnik (BK AddExpenses)");
                if (_bkOk && inc != null)
                {
                    _harmony.Patch(inc, postfix: new HarmonyMethod(typeof(CouncilWages35), nameof(IncomesPostfix)) { priority = Priority.Last });
                    _incWired = true; wired.Add("urzad radnego (BK AddIncomes)");
                }
                else missing.Add("urzad radnego (BK AddIncomes)");
                // poprawka po recenzji (R1): pokrycie pensji - postfiks DailyTickClan z priorytetem Normal: po ciele gry (saldo dopisane glowie),
                // PRZED pomiarem ksiegi pieniadza (MoneyLedger.ClanTickPostfix, Low) i przed SoldierPay (Last, ktory czysci swoje liczby rozliczenia)
                var tick = _expWired ? AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "DailyTickClan") : null;
                if (tick != null)
                {
                    _harmony.Patch(tick, postfix: new HarmonyMethod(typeof(CouncilWages35), nameof(ClanTickPostfix)) { priority = Priority.Normal });
                    _tickWired = true; wired.Add("pokrycie pensji (DailyTickClan)");
                }
                else missing.Add("pokrycie pensji (DailyTickClan)");
            }
            catch (Exception e) { missing.Add("blad: " + e.Message); Stumble("EnsureHooks", e); }
            _hookNote = (wired.Count > 0 ? string.Join(", ", wired.ToArray()) : "-") + "; BRAK: " + (missing.Count > 0 ? string.Join(", ", missing.ToArray()) : "-");
            Log.Info("Rada (3.5): latki - " + _hookNote + "; podstawa z miast i zamkow przez PopulationLaw.TownTaxPostfix (przy RentReplacesTownTax)"
                     + "; pokrycie z liczb SoldierPay (saldo, kiesa glowy, zold) i CrownIncome (splata dlugu)"
                     + " | podstawa realna (3.5a) " + (BaseOn ? "WL" : "WYL") + ", wyplata kazdemu radnemu (3.5b) " + (PayOn ? "WL" : "WYL") + ".");
        }

        // ------------------------------------------------------------ 3.5a (1): podstawa z miasta / zamku (wolane z PopulationLaw.TownTaxPostfix)
        /// <summary>Po zastapieniu podatku miasta/zamku renta: DueWage rady wlasciciela = (zachowane linie + renta / czesc pana z zaworu) x stawka -
        /// w tym samym miejscu, w ktorym BK przed chwila policzyl ja od zastapionego podatku klas ludnosci.</summary>
        internal static void TownBase(Town town, float kept)
        {
            if (!BaseOn || town == null) return;
            try
            {
                if (!BkResolve()) return;
                var st = town.Settlement;
                var owner = st != null ? st.OwnerClan : null;
                if (owner == null) return;
                var list = PositionsOf(owner);
                if (list == null || list.Count == 0) return;
                long pay = 0, avail;
                if (st.IsTown) PopulationLaw.RentOf(st, out pay, out avail);
                else if (st.IsCastle) CastlePurse.DuesOf(st, out pay, out avail);
                float basis = Math.Max(0f, kept) + Math.Max(0L, pay);
                long o = 0, n = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    if (p == null || _pMember.GetValue(p, null) == null) continue;     // jak BK: tylko zajete pozycje
                    float rate = (float)_mAdm.Invoke(p, null);
                    if (rate <= 0f) continue;                                           // jak BK CalculateDueWages: num > 0
                    int nw = (int)(basis * rate);
                    o += Wage(p); n += nw;
                    SetWage(p, nw);
                }
                Interlocked.Increment(ref _baseCalls);
                if (pay > 0) Interlocked.Increment(ref _baseRentCalls);
                Interlocked.Add(ref _baseOld, o); Interlocked.Add(ref _baseNew, n);
            }
            catch (Exception e) { Stumble("TownBase", e); }
        }

        // ------------------------------------------------------------ BK AddExpenses: platnik
        /// <summary>3.5a (2): rod bez miasta i zamku - stara kwota (BK juz jej nie przeliczy) do zera przed linia "Council wages".</summary>
        public static void ExpensesPrefix(Clan __0, bool __2)
        {
            if (!__2 || __0 == null || !BaseOn) return;
            try
            {
                if (__0.Fiefs.Count > 0) return;
                var list = PositionsOf(__0);
                if (list == null) return;
                bool any = false;
                for (int i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    try
                    {
                        if (p == null || _pMember.GetValue(p, null) == null) continue;
                        int w = Wage(p);
                        if (w == 0) continue;
                        _staleGold += w; _staleN++; any = true;
                        SetWage(p, 0);
                    }
                    catch (Exception e) { Stumble("ExpensesPrefix(pozycja)", e); }
                }
                if (any) _staleClans++;
            }
            catch (Exception e) { Stumble("ExpensesPrefix", e); }
        }

        /// <summary>Po linii "Council wages" (prawdziwe rozliczenie platnika): rozbicie wedlug odbiorcy; 3.5b - radni-lordowie wprost do kiesy,
        /// radny poza swiatem - zwrot platnikowi.</summary>
        public static void ExpensesPostfix(Clan __0, ref ExplainedNumber __1, bool __2)
        {
            if (!__2 || __0 == null) return;
            try
            {
                var list = PositionsOf(__0);
                if (list == null) return;
                bool pay = PayOn, any = false;
                bool payLeaders = pay && _incWired;   // bez latki AddIncomes glowa dostaje przez "Councillor role" jak w BK - nie drugi raz
                long refund = 0;
                var head = __0.Leader;
                if (!ReferenceEquals(_pendClan, __0) || _pendTime != CampaignTime.Now) { _pend.Clear(); _pendClan = __0; _pendTime = CampaignTime.Now; }
                for (int i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    try
                    {
                        var m = p != null ? _pMember.GetValue(p, null) as Hero : null;
                        if (m == null) continue;
                        int w = Wage(p);
                        if (w <= 0) continue;
                        any = true;
                        _charged += w; _chargedN++;
                        bool gone = CirculationWindows.OutOfWorld(m);
                        if (gone)
                        {
                            _toGone += w; _goneN++;
                            if (pay)
                            {
                                if (!m.IsLord) m.ChangeHeroGold(-w);            // BK wlozyl to zmarlemu przed chwila - wraca
                                refund += w;
                            }
                            continue;
                        }
                        if (!m.IsLord)
                        {
                            _toCommon += w; _commonN++;                             // BK zaplacil wprost (Member.Gold += DueWage)
                            if (m != head) _pend.Add(new Pay { Who = m, Gold = w, Lord = false });
                            continue;
                        }
                        bool leader = m.Clan != null && m.Clan.Leader == m;
                        if (leader) { _toLeaders += w; _leadersN++; }
                        else if (m.Clan == __0) { _toFamily += w; _familyN++; }
                        else { _toOtherLords += w; _otherLordsN++; }
                        if (leader ? payLeaders : pay)
                        {
                            m.ChangeHeroGold(w);                                    // ta sama droga co BK dla nie-lordow; w oknie rozliczenia rodu
                            if (m != head) _pend.Add(new Pay { Who = m, Gold = w, Lord = true });
                        }
                    }
                    catch (Exception e) { Stumble("ExpensesPostfix(pozycja)", e); }
                }
                if (any) _payerClans++;
                if (refund > 0) { _refund += refund; __1.Add(refund, _txtWages); }
            }
            catch (Exception e) { Stumble("ExpensesPostfix", e); }
        }

        // ------------------------------------------------------------ BK AddIncomes: odbiorca-glowa rodu
        /// <summary>3.5b: "Councillor role" glowy rodu zaplacone juz wprost u platnika - w prawdziwym rozliczeniu zdjete ta sama kwota i nazwa.</summary>
        public static void IncomesPostfix(Clan __0, ref ExplainedNumber __1, bool __2)
        {
            if (!__2 || __0 == null) return;
            var leader = __0.Leader;
            try
            {
                if (PayOn && _expWired && leader != null)
                {
                    var court = Court();
                    var list = court != null ? _mHeroPositions.Invoke(court, new object[] { leader }) as IList : null;   // ta sama lista, z ktorej BK dodal linie
                    if (list != null && list.Count > 0)
                    {
                        long sum = 0;
                        for (int i = 0; i < list.Count; i++) if (list[i] != null) sum += Wage(list[i]);
                        if (sum != 0) { __1.Add(-sum, _txtRole); _roleCut += sum; _roleN++; }
                    }
                }
            }
            catch (Exception e) { Stumble("IncomesPostfix", e); }
        }

        // ------------------------------------------------------------ poprawka po recenzji (R1): pensje bez pokrycia
        /// <summary>ClanVariablesCampaignBehavior.DailyTickClan, postfiks (priorytet Normal): saldo rodu jest juz dopisane glowie; biegnie PRZED pomiarem
        /// ksiegi pieniadza (MoneyLedger.ClanTickPostfix, Low - cofniecie jest w tym samym oknie rozliczenia rodu) i przed SoldierPay (Last).
        /// Gdy saldo nie zmiescilo sie w kiesie glowy, brak obciaza najpierw zold rodu (SoldierPay.Settle) i splate dlugu wobec korony
        /// (CrownIncome.ClanTickEnd) - te same liczby, ktore oni za chwile wezma; reszta braku to pensje rady wyplacone bez pokrycia (z niczego).
        /// Przy 3.5b WL cofamy ja z kies radnych tego rozliczenia (nie-lordow, ktorym zaplacil BK, i lordow, ktorym zaplacilismy) proporcjonalnie -
        /// ta sama regula co SoldierPay dla zoldu (czesc zostawiona w dol). Przy WYL tylko liczymy.</summary>
        public static void ClanTickPostfix(Clan __0)
        {
            if (_pendClan == null) return;
            try
            {
                if (!ReferenceEquals(_pendClan, __0) || _pendTime != CampaignTime.Now || _pend.Count == 0) return;
                long sum = 0;
                for (int i = 0; i < _pend.Count; i++) sum += _pend[i].Gold;
                if (sum <= 0) return;
                long gap, owed, rest;
                if (SoldierPay.PeekShortfall(__0, out gap, out owed))
                    rest = Math.Max(0L, gap - owed - CrownIncome.DebtCutNow(__0, gap));
                else
                {
                    // saldo nieznane (SoldierPay poza oknem tego rodu albo bez latki salda): regula SoldierPay - pusta kiesa glowy po rozliczeniu = nic nie zaplacono
                    var head = __0.Leader;
                    _blindClans++;
                    rest = head == null || head.Gold <= 0 ? sum : 0;
                }
                if (rest <= 0) return;
                long cut = Math.Min(rest, sum);
                _gapClans++; _gapRest += rest;
                bool pay = PayOn;
                for (int i = 0; i < _pend.Count; i++)
                {
                    var p = _pend[i];
                    try
                    {
                        if (p.Who == null || p.Gold <= 0) continue;
                        int take = p.Gold - (int)((long)p.Gold * (sum - cut) / sum);   // jak SoldierPay.Share: czesc zostawiona w dol - cofniete >= brak
                        if (take <= 0) continue;
                        if (!pay) { _revOff += take; continue; }
                        int got = Math.Min(take, Math.Max(0, p.Who.Gold));
                        if (got > 0) p.Who.ChangeHeroGold(-got);
                        if (p.Lord) _revLords += got; else _revCommon += got;
                        _revN++;
                        if (got < take) _revMiss += take - got;
                    }
                    catch (Exception e) { Stumble("ClanTickPostfix(pozycja)", e); }
                }
            }
            catch (Exception e) { Stumble("ClanTickPostfix", e); }
            finally { _pend.Clear(); _pendClan = null; }
        }

        // ------------------------------------------------------------ log
        /// <summary>Wstawka do linii "Pieniadz swiata (rody - przyczyny)" (MoneyLedger.ClanCausesLine) - to samo okno: rozliczenia rodow od ostatniej doby.</summary>
        internal static string LedgerNote()
        {
            if (!_expWired) return "";
            bool pay = PayOn, payLeaders = pay && _incWired;
            long lords = pay ? _toFamily + _toOtherLords + (payLeaders ? _toLeaders : 0) : 0;
            long direct = _toCommon + lords;
            long lost = pay ? 0 : _toFamily + _toOtherLords + _toGone;
            return " (3.5: naliczone platnikom -" + _charged + ", wprost do kies radnych +" + direct + " [BK nie-lordom " + _toCommon
                   + (pay ? ", 3.5b lordom " + lords : "") + "], zwrot platnikom za radnych poza swiatem +" + _refund
                   + (pay ? ", cofniete z kies radnych bez pokrycia (saldo platnika ponad kiese) -" + (_revCommon + _revLords) : "")
                   + ", 'Councillor role' zdjete -" + _roleCut + (pay ? "" : "; WYL: w nicosc " + lost + " + glowy-radni przez 'Councillor role' " + _toLeaders
                   + ", BK nie-lordom bez pokrycia " + _revOff) + ")";
        }

        internal static void Daily()
        {
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                bool pay = PayOn, bas = BaseOn;
                long sumParts = _toCommon + _toLeaders + _toFamily + _toOtherLords + _toGone;
                var sb = new StringBuilder(900);
                sb.Append("Rada (3.5): dzien ").Append(day)
                  .Append(" | podstawa (3.5a ").Append(bas ? "WL" : "WYL").Append("): przeliczenia pensji z miast i zamkow ").Append(Interlocked.Read(ref _baseCalls))
                  .Append(" (z renta lub zaworem > 0: ").Append(Interlocked.Read(ref _baseRentCalls)).Append("; suma po wywolaniach: wedlug zastapionego podatku BK ")
                  .Append(Interlocked.Read(ref _baseOld)).Append(" -> wedlug zachowanych linii i renty ").Append(Interlocked.Read(ref _baseNew))
                  .Append("), rody bez miasta i zamku: wyzerowane stare pensje ").Append(_staleN).Append(" pozycji u ").Append(_staleClans).Append(" rodow (").Append(_staleGold).Append(" zl/dobe)")
                  .Append(" | wyplata (3.5b ").Append(pay ? "WL" : "WYL").Append("): naliczone platnikom ").Append(_charged).Append(" w ").Append(_chargedN)
                  .Append(" pozycjach u ").Append(_payerClans).Append(" rodow = nie-lordowie (placi BK) ").Append(_toCommon).Append(" [").Append(_commonN)
                  .Append("] + glowy innych rodow ").Append(_toLeaders).Append(" [").Append(_leadersN).Append("] + czlonkowie rodu platnika ").Append(_toFamily)
                  .Append(" [").Append(_familyN).Append("] + lordowie innych rodow (nie glowy) ").Append(_toOtherLords).Append(" [").Append(_otherLordsN)
                  .Append("] + radni poza swiatem ").Append(_toGone).Append(" [").Append(_goneN).Append(']');
                if (pay)
                    sb.Append(" | wprost do kies lordow (3.5b) ").Append(_toFamily + _toOtherLords + (_incWired ? _toLeaders : 0)).Append(", zwrot platnikom za radnych poza swiatem ")
                      .Append(_refund).Append(", 'Councillor role' zdjete w rozliczeniach glow ").Append(_roleCut).Append(" (u ").Append(_roleN).Append(" rodow) - w nicosc 0");
                else
                    sb.Append(" | WYL: w nicosc ").Append(_toFamily + _toOtherLords + _toGone).Append(" (lordowie, ktorzy nie sa glowa rodu, i radni poza swiatem); glowy-radni ")
                      .Append(_toLeaders).Append(" przez 'Councillor role' w swoim rozliczeniu (kwota z chwili ich rozliczenia)");
                sb.Append(" | bez pokrycia (saldo ponad kiese glowy po obcietym zoldzie i splacie dlugu korony)").Append(_tickWired ? "" : " NIE SPRAWDZANE - brak latki DailyTickClan")
                  .Append(": ").Append(_gapClans).Append(" rodow, brak ").Append(_gapRest);
                if (pay)
                    sb.Append(", cofniete z kies radnych ").Append(_revCommon + _revLords).Append(" (nie-lordowie ").Append(_revCommon).Append(", lordowie ").Append(_revLords)
                      .Append(", pozycji ").Append(_revN).Append(", niedobrane ").Append(_revMiss).Append(')');
                else
                    sb.Append(", WYL: nie cofane - BK nie-lordom z niczego ").Append(_revOff);
                sb.Append(", rody bez salda z SoldierPay (decyduje pusta kiesa glowy) ").Append(_blindClans);
                sb.Append(" | potkniecia ").Append(_stumbles).Append(" | latki: ").Append(_hookNote)
                  .Append(" | kontrola: naliczone=").Append(_charged).Append(" czesci=").Append(sumParts).Append(_charged == sumParts ? " OK." : " BLAD.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }
    }
}
