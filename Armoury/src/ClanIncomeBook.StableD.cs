using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// PACZKA 169c - D STALY W LOGU (projekt etapu 2, rozdz. "169c", W-1 z audytu 12, uwagi K8, K12, K17, K34, K39, K40). Sam log i CSV -
    /// niczego nie zmienia w grze. Obok D169 (wplyw doby = a + b + Today) liczymy rozbicie tego samego wplywu na 8 czesci:
    ///  - w D stalym: ZIEMIA (podatek wsi BK "Village Demesnes", renta wsi i zawor miast PopulationLaw oraz zawor zamkow CastlePurse (110) - bez
    ///    czesci "wlasne", cla i podatek miast "Walled Demesnes"), KORONA (polityki krola, zapomoga, zwrot zoldu, mennica i monopole), KONTRAKT
    ///    (kontrakt najemnika gry), MAJATEK (warsztaty i zysk karawan rodu - prawdziwy platnik to handel);
    ///  - poza D: WLASNE (pieniadze rodu wplacone wczoraj do kasy wlasnego miasta albo zamku - zold i sprzet zalogi, wydatki swoich ludzi - ktore
    ///    wrocily zaworem: wplata x czesc kasy, ktora zawor wzial dzis; zamek od 110, K8), JEDNORAZOWE (zdarzenia spoza rodu, trzecia), INNE Z MODELU (majatki BK, "za tier",
    ///    trybut, rada, podatki od wasali BK, wszystko, czego nie rozpoznalismy), PRZELEWY W RODZIE (10% kiesy partii czlonka rodu ponad 10 000
    ///    do glowy - wzor gry AddIncomeFromParty), w tym PODWOJNE (wyplaty majatkow BK widziane jako zdarzenie, choc model liczy je tez w linii
    ///    "Estate properties" - siedza w jednorazowych).
    /// Tozsamosc (z budowy): ziemia + wlasne + korona + kontrakt + majatek + jednorazowe + inne + przelewy = wplyw doby D169.
    /// D staly = ziemia z DZISIEJSZYCH lenn (pierscien 28 dob na osade: podatek wsi wedlug posiadacza tytulu BK, renta wedlug wlasciciela,
    /// cla i podatek miasta wedlug wlasciciela; reszta ziemi bez osady - pierscien rodu) + korona + kontrakt + majatek (pierscienie rodu).
    /// Pierscienie tylko w pamieci sesji (bez zapisu - zapis dojdzie z 166, pierwsza paczka, ktora uzyje D stalego w grze); srednia z dob
    /// zmierzonych (do 28), bez wartosci zastepczej. Dochodu zajetego w D3 nie ma do wgrania 168.
    /// </summary>
    internal static partial class ClanIncomeBook
    {
        internal static bool StableDOn { get { var s = Settings.Current; return s != null && s.ClanIncomeBookEnabled && s.ClanIncomeBookStableD; } }

        // czesci wplywu doby rodu; QDouble jest "w tym" jednorazowych (poza suma)
        private const int QLand = 0, QOwn = 1, QCrown = 2, QContract = 3, QAssets = 4, QOnce = 5, QOther = 6, QFamily = 7, QInflow = 8, QDouble = 9, Q = 10;

        private sealed class Ring
        {
            public readonly long[] V = new long[Days];
            public int Head, Filled;
            public void Push(long v) { V[Head] = v; Head = (Head + 1) % Days; if (Filled < Days) Filled++; }
            public double Avg() { if (Filled <= 0) return 0; long s = 0; for (int i = 0; i < Days; i++) s += V[i]; return (double)s / Filled; }
        }

        private sealed class PartRing
        {
            public readonly long[][] V = new long[Q][];
            public int Head, Filled;
            public PartRing() { for (int q = 0; q < Q; q++) V[q] = new long[Days]; }
            public void Push(long[] t) { for (int q = 0; q < Q; q++) V[q][Head] = t[q]; Head = (Head + 1) % Days; if (Filled < Days) Filled++; }
            public double Avg(int q) { if (Filled <= 0) return 0; long s = 0; var a = V[q]; for (int i = 0; i < Days; i++) s += a[i]; return (double)s / Filled; }
        }

        private static readonly Dictionary<Clan, PartRing> _parts = new Dictionary<Clan, PartRing>();
        private static readonly Dictionary<Settlement, Ring> _rVTax = new Dictionary<Settlement, Ring>();    // podatek wsi BK (wedlug posiadacza tytulu)
        private static readonly Dictionary<Settlement, Ring> _rRent = new Dictionary<Settlement, Ring>();    // renta wsi i zawor miasta bez "wlasnych" (wedlug wlasciciela)
        private static readonly Dictionary<Settlement, Ring> _rTown = new Dictionary<Settlement, Ring>();    // cla i podatek miasta / zamku (wedlug wlasciciela)
        private static readonly Dictionary<Settlement, Ring> _rCastle = new Dictionary<Settlement, Ring>();  // 110: zawor zamku bez "wlasnych" (wedlug wlasciciela)
        private static readonly Dictionary<Clan, Ring> _rLoose = new Dictionary<Clan, Ring>();               // ziemia bez osady (nie do przypisania)
        private static readonly Dictionary<Settlement, long> _ownPaid = new Dictionary<Settlement, long>();  // wplaty rodu do kasy wlasnej osady od ostatniego Daily
        private static readonly Dictionary<Clan, long> _ownByClan = new Dictionary<Clan, long>();            // "wlasne" dzis (zawor oddal rodowi jego pieniadze)
        private static readonly Dictionary<Clan, long> _ownCastleByClan = new Dictionary<Clan, long>();      // 110: "wlasne" dzis z zaworu zamkow
        private static readonly Dictionary<Clan, List<Settlement>> _villagesOf = new Dictionary<Clan, List<Settlement>>();   // dzis: wsie wedlug posiadacza (BK)
        private static readonly Dictionary<Clan, long> _vTaxOf = new Dictionary<Clan, long>();                // dzis: suma podatku wsi posiadacza (do porownania z linia modelu)
        private static readonly Dictionary<Clan, int> _cutToday = new Dictionary<Clan, int>();               // zold przyciety z braku w kiesie (SoldierPay) od ostatniego Daily
        private static readonly Dictionary<Clan, int> _bankrupt = new Dictionary<Clan, int>();               // doby z rzedu: kiesa glowy 0 i zold przyciety (K39)
        private static readonly HashSet<Clan> _cutBlind = new HashSet<Clan>();                                // dzis: zold przyciety "na slepo" (saldo nieznane, kiesa glowy 0 - SoldierPay)
        private static readonly Dictionary<Clan, int> _bankruptBlind = new Dictionary<Clan, int>();          // doby "na slepo" w biezacej serii bankruta (K39)
        private static readonly Dictionary<string, long> _unknown = new Dictionary<string, long>();          // linie modelu NIEROZPOZNANE (ani D, ani znane "inne")
        private static readonly long[] _t = new long[Q];

        // sumy swiata doby (linia)
        private static long _dRentC, _dOwnC;   // 110: zawor zamkow i jego "wlasne" (swiat, dzis)
        private static long _dVd, _dWd, _dRentV, _dRentT, _dOwn, _dPol, _dSup, _dRefund, _dMint, _dMerc, _dShop, _dCarPar, _dFamily, _dOther, _dOnce, _dDouble, _dInflow, _dVdCalc;
        private static int _dVdOff, _dLooseN, _dClans;
        // "inne z modelu" rozbite (przeglad 169c): znane linie spoza D, wplyw bez opisu (m.in. "za tier" gry - Add bez nazwy), nierozpoznane (modul)
        private static long _dKnown, _dUnlisted, _dUnrec, _dUnrecAbs;
        // wsie bez danych BK (pominiete, zeby nie wolac GetPopData -> InitializeSettlementPops); podatek wsi do posiadacza de iure innego niz glowa rodu
        private static int _dVNoPop, _dDeJureN, _dDeJureOtherN;
        private static long _dDeJureGold, _dDeJureOtherGold;
        // koszt D stalego w tej dobie (Stopwatch; prog projektu: +3% czasu doby)
        private static long _ticksStableBegin, _ticksStableClans;
        internal static long LastTicksStableBegin, LastTicksStableClans, LastTicksStableLine;
        internal static long DayEstates;   // dla podwojnych: wyplaty majatkow BK widziane jako zdarzenie (suma doby)

        private static string _nVillage, _nWalled, _nSettle, _nMerc, _nCarPar, _nShop, _nPolicies, _nCrownDues, _nSupport;
        private static string _nEstates, _nCouncillor, _nTribute, _nCallToWar, _nSpring, _nAlley;
        private static string[] _tVassal, _tRetainer, _tTrade;
        private static bool _namesDone;
        private static bool _bkDone;
        private static MethodInfo _mActual, _mVTax, _mPopulated, _mGetTitle;
        private static PropertyInfo _pCfgInstance, _pTaxModel, _pPopMgr, _pTitleMgr, _pDeJure;
        private static string _bkNote = "-";

        internal static void ResetStable()
        {
            _parts.Clear(); _rVTax.Clear(); _rRent.Clear(); _rTown.Clear(); _rLoose.Clear(); _ownPaid.Clear(); _ownByClan.Clear(); _rCastle.Clear(); _ownCastleByClan.Clear();
            _villagesOf.Clear(); _vTaxOf.Clear(); _cutToday.Clear(); _cutBlind.Clear(); _bankrupt.Clear(); _bankruptBlind.Clear(); _unknown.Clear(); DayEstates = 0;
            ZeroStableDay();
            LastTicksStableBegin = LastTicksStableClans = LastTicksStableLine = 0;
            _namesDone = false; _bkDone = false; _mActual = null; _mVTax = null; _mPopulated = null; _mGetTitle = null;
            _pCfgInstance = null; _pTaxModel = null; _pPopMgr = null; _pTitleMgr = null; _pDeJure = null; _bkNote = "-";
        }

        private static void ZeroStableDay()
        {
            _dRentC = _dOwnC = 0;
            _dVd = _dWd = _dRentV = _dRentT = _dOwn = _dPol = _dSup = _dRefund = _dMint = _dMerc = _dShop = _dCarPar = _dFamily = _dOther = _dOnce = _dDouble = _dInflow = _dVdCalc = 0;
            _dVdOff = _dLooseN = _dClans = 0;
            _dKnown = _dUnlisted = _dUnrec = _dUnrecAbs = 0;
            _dVNoPop = _dDeJureN = _dDeJureOtherN = 0; _dDeJureGold = _dDeJureOtherGold = 0;
            _ticksStableBegin = _ticksStableClans = 0;
        }

        // ------------------------------------------------------------ liczniki z innych modulow (kazdy we wlasnym try)
        /// <summary>Rod wplacil do kasy osady (zold zalogi, sprzet, wydatki swoich ludzi) - liczy sie tylko, gdy osada jest jego (czesc "wlasne").</summary>
        internal static void NoteOwnPaid(Settlement st, Clan payer, int amount)
        {
            try
            {
                if (amount <= 0 || st == null || payer == null || !StableDOn) return;
                if (!ReferenceEquals(st.OwnerClan, payer)) return;
                long v; _ownPaid.TryGetValue(st, out v);
                _ownPaid[st] = v + amount;
            }
            catch (Exception e) { Stumble("NoteOwnPaid", e); }
        }

        /// <summary>SoldierPay: zold rodu przyciety, bo saldo nie zmiescilo sie w kiesie glowy (K39 - miara bankructwa). blind - saldo nieznane
        /// (model finansow spoza naszych latek) przy pustej kiesie glowy: przyciecie prawdopodobne, ale niepewne - liczone osobno w linii "D staly".</summary>
        internal static void NoteWageCut(Clan c, long cut, bool blind = false)
        {
            try
            {
                if (c == null || cut <= 0 || !On) return;
                int v; _cutToday.TryGetValue(c, out v);
                _cutToday[c] = (int)Math.Min(int.MaxValue, v + cut);
                if (blind) _cutBlind.Add(c);
            }
            catch (Exception e) { Stumble("NoteWageCut", e); }
        }

        /// <summary>169c: odczyty dla linii pomiaru (Measure169c) - kiesy rodziny, wplyw ostatniej doby D169, zold ostatniego rozliczenia.</summary>
        internal static long FamilyGoldOf(Clan c) { try { return c == null ? 0 : FamilyGold(c); } catch { return 0; } }
        internal static long InflowOf(Clan c) { try { Rec r; return c != null && c.StringId != null && _book.TryGetValue(c.StringId, out r) ? r.Inflow : 0; } catch { return 0; } }
        internal static long WageOf(Clan c) { try { Rec r; return c != null && c.StringId != null && _book.TryGetValue(c.StringId, out r) && r.HadTick ? (long)r.WageLastLord + r.WageLastGar : 0; } catch { return 0; } }

        /// <summary>Rody nieumarlych (Inni) - poza linia "Budzet rodow" i CSV (K40).</summary>
        internal static bool IsUndeadClan(Clan c)
        {
            try
            {
                if (c == null) return false;
                if (c.StringId == "ROTclan_126") return true;
                return c.Leader != null && Undead.Character(c.Leader.CharacterObject);
            }
            catch { return false; }
        }

        // ------------------------------------------------------------ nazwy linii modelu i refleksja BK (raz na sesje / na dobe)
        private static string Txt(Func<string> f) { try { var s = f(); return string.IsNullOrEmpty(s) ? null : s; } catch { return null; } }

        private static void ResolveNames()
        {
            if (_namesDone) return;
            _namesDone = true;
            _nVillage = Txt(() => new TextObject("{=GikQuojv}Village Demesnes").ToString());
            _nWalled = Txt(() => new TextObject("{=!}Walled Demesnes").ToString());
            _nSettle = Txt(() => GameTexts.FindText("str_finance_settlement_income").ToString());
            _nMerc = Txt(() => GameTexts.FindText("str_finance_mercenary").ToString());
            _nCarPar = Txt(() => GameTexts.FindText("str_finance_caravan_and_party_income").ToString());
            _nShop = Txt(() => GameTexts.FindText("str_finance_shop_income").ToString());
            _nPolicies = Txt(() => GameTexts.FindText("str_policies").ToString());
            _nCrownDues = Txt(() => KingdomTreasury.TxtPolicy.ToString());
            _nSupport = Txt(() => GameTexts.FindText("str_finance_kingdom_support").ToString());
            // znane linie "inne z modelu" (poza D, rozpoznane): BK `BKClanFinanceModel.AddIncomes` (majatki, rada, podatki od wasali, sluzba u lorda)
            // i gra `DefaultClanFinanceModel.CalculateClanIncomeInternal` (trybut, wezwanie do wojny, umowy handlowe, perk, zaulki gracza)
            _nEstates = Txt(() => new TextObject("{=QEy4Ddar}Estate properties").ToString());
            _nCouncillor = Txt(() => new TextObject("{=WvhXhUFS}Councillor role").ToString());
            _nTribute = Txt(() => GameTexts.FindText("str_finance_tribute_incomes").ToString());
            _nCallToWar = Txt(() => GameTexts.FindText("str_finance_call_to_war_incomes").ToString());
            _nSpring = Txt(() => TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Trade.SpringOfGold.Name.ToString());
            _nAlley = Txt(() => GameTexts.FindText("str_finance_alley").ToString());
            _tVassal = Tpl(() => new TextObject("{=6keRYbQa}Taxes from {CLAN}").SetTextVariable("CLAN", TplMark));
            _tRetainer = Tpl(() => new TextObject("{=cYac1rMJ}Retainer service for {HERO}").SetTextVariable("HERO", TplMark));
            _tTrade = Tpl(() => GameTexts.FindText("str_finance_trade_agreement_income").CopyTextObject().SetTextVariable("KINGDOM", TplMark));
        }

        // linia z nazwa zmienna (rod, lord, krolestwo): wzor = tekst przed i po znaczniku; bez stalego poczatku nie dopasowujemy
        private const string TplMark = "ZQ169CQZ";
        private static string[] Tpl(Func<TextObject> f)
        {
            try
            {
                var t = f(); if (t == null) return null;
                string s = t.ToString(); int i = s.IndexOf(TplMark, StringComparison.Ordinal);
                return i > 0 ? new[] { s.Substring(0, i), s.Substring(i + TplMark.Length) } : null;
            }
            catch { return null; }
        }
        private static bool Like(string n, string[] t) { return t != null && n.Length >= t[0].Length + t[1].Length && n.StartsWith(t[0], StringComparison.Ordinal) && n.EndsWith(t[1], StringComparison.Ordinal); }

        private static bool KnownOther(string n)
        {
            return Eq(n, _nEstates) || Eq(n, _nCouncillor) || Eq(n, _nTribute) || Eq(n, _nCallToWar) || Eq(n, _nSpring) || Eq(n, _nAlley)
                   || Like(n, _tVassal) || Like(n, _tRetainer) || Like(n, _tTrade);
        }

        private static void ResolveBk()
        {
            if (_bkDone) return;
            _bkDone = true;
            try
            {
                var ext = AccessTools.TypeByName("BannerKings.Extensions.ClanExtensions");
                _mActual = ext != null ? AccessTools.Method(ext, "GetActualVillages", new[] { typeof(Clan) }) : null;
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                _pCfgInstance = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                _pTaxModel = cfgT != null ? AccessTools.Property(cfgT, "TaxModel") : null;
                var tmT = _pTaxModel != null ? _pTaxModel.PropertyType : null;
                _mVTax = tmT != null ? AccessTools.Method(tmT, "CalculateVillageTaxFromIncome", new[] { typeof(Village), typeof(bool), typeof(bool) }) : null;
                // przeglad 169c: GetPopData (wolany przez podatek wsi BK) dla osady bez danych BK wola InitializeSettlementPops (losowanie + zapis) -
                // najpierw czysty odczyt IsSettlementPopulated; tytul wsi (posiadacz de iure) - TitleManager.GetTitle (odczyt pamieci podrecznej)
                _pPopMgr = cfgT != null ? AccessTools.Property(cfgT, "PopulationManager") : null;
                _mPopulated = _pPopMgr != null ? AccessTools.Method(_pPopMgr.PropertyType, "IsSettlementPopulated", new[] { typeof(Settlement) }) : null;
                _pTitleMgr = cfgT != null ? AccessTools.Property(cfgT, "TitleManager") : null;
                _mGetTitle = _pTitleMgr != null ? AccessTools.Method(_pTitleMgr.PropertyType, "GetTitle", new[] { typeof(Settlement) }) : null;
                _pDeJure = _mGetTitle != null ? AccessTools.Property(_mGetTitle.ReturnType, "deJure") : null;
                _bkNote = (_mActual != null ? "wsie BK wedlug tytulu" : "BRAK GetActualVillages") + ", " + (_mVTax != null ? "podatek wsi BK" : "BRAK podatku wsi BK")
                          + ", " + (_mPopulated != null ? "sprawdzenie danych BK" : "BRAK IsSettlementPopulated (podatek wsi BK pominiety)")
                          + ", " + (_pDeJure != null ? "tytul de iure" : "BRAK tytulu de iure");
            }
            catch (Exception e) { Stumble("ResolveBk", e); _bkNote = "BRAK (blad)"; }
        }

        private static bool Eq(string a, string b) { return b != null && string.Equals(a, b, StringComparison.Ordinal); }

        // ------------------------------------------------------------ poczatek doby: wsie, renty i "wlasne" (przed petla rodow)
        private static void StableBegin()
        {
            ZeroStableDay();
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try { StableBeginBody(); }
            finally { _ticksStableBegin = System.Diagnostics.Stopwatch.GetTimestamp() - t0; LastTicksStableBegin = _ticksStableBegin; }
        }

        private static void StableBeginBody()
        {
            _unknown.Clear(); _ownByClan.Clear(); _ownCastleByClan.Clear(); _villagesOf.Clear(); _vTaxOf.Clear();
            ResolveNames();
            ResolveBk();
            // posiadacze wsi (BK: tytul) i podatek kazdej wsi - ta sama funkcja, ktora BK liczy linie "Village Demesnes"
            object tax = null, popMgr = null, titleMgr = null;
            try
            {
                var cfg = _pCfgInstance != null ? _pCfgInstance.GetValue(null, null) : null;
                tax = cfg != null && _pTaxModel != null ? _pTaxModel.GetValue(cfg, null) : null;
                popMgr = cfg != null && _pPopMgr != null ? _pPopMgr.GetValue(cfg, null) : null;
                titleMgr = cfg != null && _pTitleMgr != null ? _pTitleMgr.GetValue(cfg, null) : null;
            }
            catch { tax = null; popMgr = null; titleMgr = null; }
            var holder = new Dictionary<Village, Clan>();
            if (_mActual != null)
                foreach (var c in Clan.All)
                {
                    try
                    {
                        if (c == null || c.IsEliminated || c.IsBanditFaction || c.Leader == null) continue;
                        var list = _mActual.Invoke(null, new object[] { c }) as System.Collections.IEnumerable;
                        if (list == null) continue;
                        foreach (var o in list) { var v = o as Village; if (v != null && !holder.ContainsKey(v)) holder[v] = c; }
                    }
                    catch (Exception e) { Stumble("StableBegin(wsie)", e); }
                }
            foreach (var v in Village.All)
            {
                try
                {
                    if (v == null || v.Settlement == null) continue;
                    long t = 0;
                    if (tax != null && _mVTax != null)
                    {
                        // przeglad 169c: tylko osada z danymi BK (GetPopData w podatku BK inicjuje brakujace dane - zmiana stanu gry)
                        bool pop = false;
                        try { pop = popMgr != null && _mPopulated != null && (bool)_mPopulated.Invoke(popMgr, new object[] { v.Settlement }); } catch { pop = false; }
                        if (pop)
                        {
                            var en = (ExplainedNumber)_mVTax.Invoke(tax, new object[] { v, false, false });
                            t = (long)en.ResultNumber;
                        }
                        else _dVNoPop++;
                    }
                    Clan h;
                    bool actual = holder.TryGetValue(v, out h);
                    if (!actual) h = v.Settlement.OwnerClan;
                    RingOf(_rVTax, v.Settlement).Push(h != null ? t : 0);
                    if (h == null) continue;
                    // przeglad 169c (tylko pomiar): przy prawdziwej wyplacie BK oddaje podatek wsi posiadaczowi de iure, gdy nie jest nim glowa rodu
                    // (BK `ClanFinancesPatches.VillageIncomePrefix`: title.deJure != clan.Leader -> ApplyWithdrawal(..., deJure)); linia modelu
                    // (applyWithdrawals = false), D169 i "ziemia" licza go posiadaczowi faktycznemu - 166 wezmie rod posiadacza de iure
                    if (actual && titleMgr != null && _mGetTitle != null && _pDeJure != null)
                    {
                        try
                        {
                            var title = _mGetTitle.Invoke(titleMgr, new object[] { v.Settlement });
                            var dj = title != null ? _pDeJure.GetValue(title, null) as Hero : null;
                            if (dj != null && dj != h.Leader)
                            {
                                _dDeJureN++; _dDeJureGold += t;
                                if (dj.Clan != h) { _dDeJureOtherN++; _dDeJureOtherGold += t; }
                            }
                        }
                        catch (Exception e) { Stumble("StableBegin(tytul wsi)", e); }
                    }
                    List<Settlement> l;
                    if (!_villagesOf.TryGetValue(h, out l)) { l = new List<Settlement>(); _villagesOf[h] = l; }
                    l.Add(v.Settlement);
                    long s0; _vTaxOf.TryGetValue(h, out s0); _vTaxOf[h] = s0 + t;
                    _dVdCalc += t;
                }
                catch (Exception e) { Stumble("StableBegin(podatek wsi)", e); }
            }
            // renty i "wlasne": renta tej doby z PopulationLaw (na osade), wlasne = wplaty rodu od ostatniej doby x czesc kasy, ktora wzial zawor;
            // 110: zamek tak samo - czesc pana z zaworu CastlePurse i nadwyzka kasy ponad zapas, z ktorej ja wzieto (K8: "wlasne" kazdej osady rodu)
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null) continue;
                    if (st.IsCastle)
                    {
                        long cpay, cspare, cpaid, cown = 0;
                        CastlePurse.DuesOf(st, out cpay, out cspare);
                        if (_ownPaid.TryGetValue(st, out cpaid) && cpaid > 0 && cpay > 0 && cspare > 0)
                            cown = Math.Min(cpay, (long)(cpaid * Math.Min(1.0, (double)cpay / cspare)));
                        if (cown > 0 && st.OwnerClan != null) { long o0; _ownCastleByClan.TryGetValue(st.OwnerClan, out o0); _ownCastleByClan[st.OwnerClan] = o0 + cown; }
                        RingOf(_rCastle, st).Push(cpay - cown);
                        continue;
                    }
                    if (!(st.IsVillage || st.IsTown)) continue;
                    long pay = 0, avail = 0;
                    PopulationLaw.RentOf(st, out pay, out avail);
                    long own = 0;
                    long paid;
                    if (st.IsTown && _ownPaid.TryGetValue(st, out paid) && paid > 0 && pay > 0 && avail > 0)
                        own = Math.Min(pay, (long)(paid * Math.Min(1.0, (double)pay / avail)));
                    if (own > 0 && st.OwnerClan != null) { long o0; _ownByClan.TryGetValue(st.OwnerClan, out o0); _ownByClan[st.OwnerClan] = o0 + own; }
                    RingOf(_rRent, st).Push(pay - own);
                }
                catch (Exception e) { Stumble("StableBegin(renty)", e); }
            }
            _ownPaid.Clear();
        }

        private static Ring RingOf<K>(Dictionary<K, Ring> d, K k)
        {
            Ring r;
            if (!d.TryGetValue(k, out r)) { r = new Ring(); d[k] = r; }
            return r;
        }

        /// <summary>10% kiesy partii czlonka rodu ponad 10 000 (wzor gry AddIncomeFromParty) - przelew w rodzie, nie dochod.</summary>
        private static long FamilyTransfers(Clan c)
        {
            long s = 0;
            var wps = c.WarPartyComponents;
            if (wps == null) return 0;
            for (int i = 0; i < wps.Count; i++)
            {
                var mp = wps[i] != null ? wps[i].MobileParty : null;
                if (mp == null || !mp.IsActive || mp.LeaderHero == c.Leader) continue;
                if (!(mp.IsLordParty || mp.IsGarrison || mp.IsCaravan)) continue;
                int g = mp.PartyTradeGold;
                if (g > 10000) s += (g - 10000) / 10;
            }
            return s;
        }

        // ------------------------------------------------------------ rod w petli Daily (przed zerowaniem licznikow Today*)
        private static void StableClan(Clan c, Rec r, ExplainedNumber en, bool haveEn, long a, int b)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try { StableClanBody(c, r, en, haveEn, a, b); }
            finally { _ticksStableClans += System.Diagnostics.Stopwatch.GetTimestamp() - t0; LastTicksStableClans = _ticksStableClans; }
        }

        private static void StableClanBody(Clan c, Rec r, ExplainedNumber en, bool haveEn, long a, int b)
        {
            long vd = 0, wd = 0, pol = 0, sup = 0, merc = 0, shop = 0, carpar = 0;
            // przeglad 169c: "inne z modelu" (reszta a) = znane linie spoza D + wplyw bez opisu ("za tier" gry i inne Add bez nazwy, przyciecie a do 0)
            // + linie NIEROZPOZNANE; tylko te ostatnie moga byc linia D o zmienionej nazwie - ich modul jest progiem testu (zamkniecie sumy jest z budowy)
            long known = 0, unrec = 0, unrecAbs = 0, lineSum = 0;
            if (haveEn)
            {
                var lines = en.GetLines();
                for (int i = 0; i < lines.Count; i++)
                {
                    string n = lines[i].name;
                    if (n == null) continue;
                    long v = (long)Math.Round(lines[i].number);
                    if (v == 0) continue;
                    lineSum += v;
                    if (Eq(n, _nVillage)) vd += v;
                    else if (Eq(n, _nWalled) || Eq(n, _nSettle)) wd += v;
                    else if (Eq(n, _nPolicies) || Eq(n, _nCrownDues)) pol += v;
                    else if (Eq(n, _nSupport)) sup += v;
                    else if (Eq(n, _nMerc)) merc += v;
                    else if (Eq(n, _nShop)) shop += v;
                    else if (Eq(n, _nCarPar)) carpar += v;
                    else if (KnownOther(n)) known += v;
                    else { unrec += v; unrecAbs += Math.Abs(v); long u; _unknown.TryGetValue(n, out u); _unknown[n] = u + v; }
                }
            }
            _dKnown += known; _dUnrec += unrec; _dUnrecAbs += unrecAbs; _dUnlisted += a - lineSum;
            long family = Math.Max(0L, Math.Min(FamilyTransfers(c), Math.Max(0L, carpar)));
            long rent = Math.Max(0, b);
            int rentV; PopulationLaw.RentVillageToday.TryGetValue(c, out rentV);
            // 110: czesc pana z zaworu zamkow siedzi w tych samych rentach (PopulationLaw.RentToday) - osobno od zaworu miast
            int rentC0; CastlePurse.LordDuesToday.TryGetValue(c, out rentC0);
            long rentC = Math.Max(0L, Math.Min((long)rentC0, rent - Math.Max(0, rentV)));
            long rentT = Math.Max(0L, rent - Math.Max(0, rentV) - rentC);
            long ownT; _ownByClan.TryGetValue(c, out ownT);
            ownT = Math.Max(0L, Math.Min(ownT, rentT));
            long ownC; _ownCastleByClan.TryGetValue(c, out ownC);
            ownC = Math.Max(0L, Math.Min(ownC, rentC));
            long own = ownT + ownC;
            long once = r.Today - r.TodayRefund - r.TodayCrown;     // trzecia + zdarzenia (+ reszta Today z zapisu)
            long other = a - (vd + wd + pol + sup + merc + shop + carpar);
            _t[QLand] = vd + wd + rent - own;
            _t[QOwn] = own;
            _t[QCrown] = pol + sup + r.TodayRefund + r.TodayCrown;
            _t[QContract] = merc;
            _t[QAssets] = shop + carpar - family;
            _t[QOnce] = once;
            _t[QOther] = other;
            _t[QFamily] = family;
            _t[QInflow] = a + rent + r.Today;
            _t[QDouble] = Math.Min(Math.Max(0L, once), r.TodayEstates);
            PartRing pr;
            if (!_parts.TryGetValue(c, out pr)) { pr = new PartRing(); _parts[c] = pr; }
            pr.Push(_t);
            // cla i podatek miast: linia modelu rozdzielona na dzisiejsze lenna rodu wedlug wagi (podatek miasta + cla z licznika)
            long loose = 0;
            var fiefs = c.Fiefs;
            int nf = fiefs != null ? fiefs.Count : 0;
            if (nf > 0)
            {
                var w = new double[nf]; double ws = 0;
                for (int i = 0; i < nf; i++)
                {
                    var f = fiefs[i];
                    if (f == null) continue;
                    double x = 0;
                    try { x = Math.Max(0f, Campaign.Current.Models.SettlementTaxModel.CalculateTownTax(f).ResultNumber); } catch { }
                    x += Math.Max(0, f.TradeTaxAccumulated) / 5.0;
                    w[i] = x; ws += x;
                }
                long given = 0; int last = -1;
                for (int i = 0; i < nf; i++)
                {
                    var f = fiefs[i];
                    if (f == null || f.Settlement == null) continue;
                    long share = wd == 0 ? 0 : (ws > 0 ? (long)Math.Round(wd * w[i] / ws) : wd / nf);
                    RingOf(_rTown, f.Settlement).Push(share);
                    given += share; last = i;
                }
                if (last >= 0 && given != wd) _rTown[fiefs[last].Settlement].V[(_rTown[fiefs[last].Settlement].Head + Days - 1) % Days] += wd - given;   // reszta z zaokraglen do ostatniego lenna
            }
            else loose += wd;
            // podatek wsi: linia modelu wobec sumy podatku wsi posiadacza (ta sama funkcja BK) - nadwyzka linii idzie do ziemi bez osady
            long calc; _vTaxOf.TryGetValue(c, out calc);
            if (vd > calc) loose += vd - calc;
            if (Math.Abs(vd - calc) > 1) _dVdOff++;
            RingOf(_rLoose, c).Push(loose);
            if (loose != 0) _dLooseN++;
            // sumy swiata (linia)
            _dVd += vd; _dWd += wd; _dRentV += Math.Max(0, rentV); _dRentT += rentT; _dOwn += ownT; _dRentC += rentC; _dOwnC += ownC; _dPol += pol; _dSup += sup; _dRefund += r.TodayRefund; _dMint += r.TodayCrown;
            _dMerc += merc; _dShop += shop; _dCarPar += carpar; _dFamily += family; _dOther += other; _dOnce += once; _dDouble += _t[QDouble]; _dInflow += _t[QInflow]; _dClans++;
        }

        // ------------------------------------------------------------ D staly i czesci (odczyt - raport i API)
        internal struct StableParts
        {
            public double D, Land, LandVTax, LandRent, LandTown, LandCastle, LandLoose, Crown, Contract, Assets;
            public double Own, Once, Double, Other, Family, Inflow, SumParts; public int Days;
        }

        private static bool TryStable(Clan c, out StableParts p)
        {
            p = default(StableParts);
            PartRing pr;
            if (c == null || !_parts.TryGetValue(c, out pr) || pr.Filled <= 0) return false;
            List<Settlement> vs;
            if (_villagesOf.TryGetValue(c, out vs)) for (int i = 0; i < vs.Count; i++) { Ring r; if (_rVTax.TryGetValue(vs[i], out r)) p.LandVTax += r.Avg(); }
            var sts = c.Settlements;
            if (sts != null) for (int i = 0; i < sts.Count; i++)
            {
                var st = sts[i]; Ring r;
                if (st == null) continue;
                if (_rRent.TryGetValue(st, out r)) p.LandRent += r.Avg();
                if (_rCastle.TryGetValue(st, out r)) p.LandCastle += r.Avg();   // 110: zawor zamku
            }
            var fiefs = c.Fiefs;
            if (fiefs != null) for (int i = 0; i < fiefs.Count; i++) { var f = fiefs[i]; Ring r; if (f != null && f.Settlement != null && _rTown.TryGetValue(f.Settlement, out r)) p.LandTown += r.Avg(); }
            Ring rl; if (_rLoose.TryGetValue(c, out rl)) p.LandLoose = rl.Avg();
            p.Land = p.LandVTax + p.LandRent + p.LandTown + p.LandCastle + p.LandLoose;
            p.Crown = pr.Avg(QCrown); p.Contract = pr.Avg(QContract); p.Assets = pr.Avg(QAssets);
            p.D = p.Land + p.Crown + p.Contract + p.Assets;
            p.Own = pr.Avg(QOwn); p.Once = pr.Avg(QOnce); p.Double = pr.Avg(QDouble); p.Other = pr.Avg(QOther); p.Family = pr.Avg(QFamily); p.Inflow = pr.Avg(QInflow);
            p.SumParts = pr.Avg(QLand) + p.Own + p.Crown + p.Contract + p.Assets + p.Once + p.Other + p.Family;
            p.Days = pr.Filled;
            return true;
        }

        /// <summary>D staly rodu (W-1) albo -1 - API dla 166/168/178 (w 169c nikt go nie wola w logice gry).</summary>
        internal static double StableD(Clan c)
        {
            try { StableParts p; return StableDOn && TryStable(c, out p) ? p.D : -1; }
            catch { return -1; }
        }

        // ------------------------------------------------------------ raport: kolumny CSV i linia "D staly (169c)"
        private const string CsvHeaderStable = ";d_staly;d_ziemia;d_korona;d_kontrakt;d_majatek;sr_wlasne;sr_jednorazowe;sr_podwojne;sr_inne_z_modelu;sr_przelewy_w_rodzie;sr_wplyw_doby;dni_czesci;zold_przyciety;dni_bankruta";

        /// <summary>Kolumny CSV rodu (puste, gdy D staly wylaczony albo brak pomiaru); aktualizuje licznik bankructwa (K39).</summary>
        private static void StableCsv(StringBuilder csv, Clan c, int leaderGold, bool sd)
        {
            int cut; _cutToday.TryGetValue(c, out cut);
            int streak; _bankrupt.TryGetValue(c, out streak);
            streak = leaderGold <= 0 && cut > 0 ? streak + 1 : 0;
            _bankrupt[c] = streak;
            int blindDays; _bankruptBlind.TryGetValue(c, out blindDays);
            blindDays = streak == 0 ? 0 : blindDays + (_cutBlind.Contains(c) ? 1 : 0);   // doby serii z przycieciem "na slepo" (saldo nieznane)
            _bankruptBlind[c] = blindDays;
            StableParts p;
            if (sd && TryStable(c, out p))
                csv.Append(';').Append(N0(p.D)).Append(';').Append(N0(p.Land)).Append(';').Append(N0(p.Crown)).Append(';').Append(N0(p.Contract)).Append(';').Append(N0(p.Assets))
                   .Append(';').Append(N0(p.Own)).Append(';').Append(N0(p.Once)).Append(';').Append(N0(p.Double)).Append(';').Append(N0(p.Other)).Append(';').Append(N0(p.Family))
                   .Append(';').Append(N0(p.Inflow)).Append(';').Append(p.Days);
            else csv.Append(";;;;;;;;;;;;");
            csv.Append(';').Append(cut).Append(';').Append(streak);
        }

        private static string StableLine(int day, List<Clan> aiClans)
        {
            long tl = System.Diagnostics.Stopwatch.GetTimestamp();
            try { return StableLineBody(day, aiClans); }
            finally { LastTicksStableLine = System.Diagnostics.Stopwatch.GetTimestamp() - tl; }
        }

        private static string Ms(long ticks) { return (ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0", CultureInfo.InvariantCulture); }

        private static string StableLineBody(int day, List<Clan> aiClans)
        {
            var inv = CultureInfo.InvariantCulture;
            var dl = new List<double>(); var castleD = new List<double>(); var castleD169 = new List<double>();
            double sCastle = 0;
            double sD = 0, sLand = 0, sVt = 0, sRent = 0, sTown = 0, sLoose = 0, sCrown = 0, sContr = 0, sAssets = 0, sOwn = 0, sOnce = 0, sDouble = 0, sOther = 0, sFam = 0, sInflow = 0, sParts = 0, sD169 = 0;
            int n = 0, bankrupt = 0, shortDays = 0, bankBlind = 0, bankIb = 0, bankBoth = 0, bankAny = 0;
            for (int i = 0; i < aiClans.Count; i++)
            {
                var c = aiClans[i];
                try
                {
                    // bankructwo do wgrania 168 (projekt rozdz. 1): K39 albo bankrut Banku (IronBank: dlug >= 1 i Defaulted - jak "bankrutow" w linii IronBank)
                    int bs; bool k39 = _bankrupt.TryGetValue(c, out bs) && bs >= 7;
                    if (k39) { bankrupt++; int bb; if (_bankruptBlind.TryGetValue(c, out bb) && bb > 0) bankBlind++; }
                    bool ib = false;
                    try { double pr; int ms, due; bool dfl; ib = IronBank.TryGetDebt(c, out pr, out ms, out dfl, out due) && dfl; } catch { ib = false; }
                    if (ib) bankIb++;
                    if (k39 && ib) bankBoth++;
                    if (k39 || ib) bankAny++;
                    StableParts p;
                    if (!TryStable(c, out p)) continue;
                    n++; if (p.Days < Days) shortDays++;
                    dl.Add(p.D); sD += p.D; sLand += p.Land; sVt += p.LandVTax; sRent += p.LandRent; sTown += p.LandTown; sCastle += p.LandCastle; sLoose += p.LandLoose; sCrown += p.Crown; sContr += p.Contract;
                    sAssets += p.Assets; sOwn += p.Own; sOnce += p.Once; sDouble += p.Double; sOther += p.Other; sFam += p.Family; sInflow += p.Inflow; sParts += p.SumParts;
                    var rr = Of(c); if (rr != null) sD169 += rr.D;
                    int towns = 0, castles = 0;
                    var fiefs = c.Fiefs;
                    if (fiefs != null) for (int k = 0; k < fiefs.Count; k++) { var f = fiefs[k]; if (f == null) continue; if (f.IsCastle) castles++; else if (f.IsTown) towns++; }
                    if (castles > 0 && towns == 0 && !c.IsUnderMercenaryService) { castleD.Add(p.D); if (rr != null) castleD169.Add(rr.D); }
                }
                catch (Exception e) { Stumble("StableLine(rod)", e); }
            }
            dl.Sort(); castleD.Sort(); castleD169.Sort();
            var top = new List<KeyValuePair<string, long>>(_unknown);
            top.Sort((x, y) => Math.Abs(y.Value).CompareTo(Math.Abs(x.Value)));
            var unk = new List<string>();
            for (int i = 0; i < top.Count && i < 4; i++) unk.Add(Clean(top[i].Key) + " " + top[i].Value);
            double closure = sInflow != 0 ? 100.0 * (sParts - sInflow) / Math.Abs(sInflow) : 0;
            double unrecPct = _dInflow != 0 ? 100.0 * _dUnrecAbs / Math.Abs((double)_dInflow) : 0;
            var sb = new StringBuilder(1600);
            sb.Append("D staly (169c): dzien ").Append(day)
              .Append(" | rody AI ").Append(aiClans.Count).Append(" (bez Innych i dworzan BK), z pomiarem czesci ").Append(n).Append(" (krocej niz 28 dob: ").Append(shortDays).Append(')')
              .Append(" | D staly (ziemia z dzisiejszych lenn + korona + kontrakt + majatek, srednio z dob zmierzonych): mediana ").Append(N0(Pctl(dl, 0.5)))
              .Append(", 10% ").Append(N0(Pctl(dl, 0.1))).Append(", 90% ").Append(N0(Pctl(dl, 0.9))).Append(", razem ").Append(N0(sD))
              .Append("; D169 tych rodow razem ").Append(N0(sD169)).Append(" (D staly / D169 ").Append(sD169 > 0 ? (100.0 * sD / sD169).ToString("0", inv) + "%" : "-").Append(')')
              .Append(" | panowie samych zamkow (").Append(castleD.Count).Append("): D staly mediana ").Append(N0(Pctl(castleD, 0.5))).Append(", D169 mediana ").Append(N0(Pctl(castleD169, 0.5)))
              .Append(" (prog testu 400-1300)")
              .Append(" | czesci D (swiat, srednio na dobe): ziemia ").Append(N0(sLand)).Append(" (podatek wsi BK ").Append(N0(sVt)).Append(", renta wsi i zawor miast bez wlasnych ").Append(N0(sRent))
              .Append(", cla i podatek miast ").Append(N0(sTown)).Append(", bez osady ").Append(N0(sLoose)).Append("; zawor zamkow bez wlasnych ").Append(N0(sCastle)).Append("), korona ").Append(N0(sCrown)).Append(", kontrakt ").Append(N0(sContr))
              .Append(", majatek ").Append(N0(sAssets))
              .Append(" | poza D: wlasne ").Append(N0(sOwn)).Append(", jednorazowe ").Append(N0(sOnce)).Append(" (w tym podwojne - majatki BK ").Append(N0(sDouble)).Append("), inne z modelu ").Append(N0(sOther))
              .Append(", przelewy w rodzie ").Append(N0(sFam)).Append(", zajete w D3 - (168)")
              .Append(" | kontrola zamkniecia (z budowy - inne z modelu to reszta; tylko blad ksiegowania): ")
              .Append(N0(sParts)).Append(" wobec ").Append(N0(sInflow)).Append(" - roznica ").Append(closure.ToString("0.00", inv)).Append("%")
              .Append(" | dzis (swiat): linie modelu - podatek wsi ").Append(_dVd).Append(" (wedlug wsi BK ").Append(_dVdCalc).Append(", roznica u ").Append(_dVdOff).Append(" rodow), cla i podatek miast ").Append(_dWd)
              .Append(", polityki krolow ").Append(_dPol).Append(", zapomoga ").Append(_dSup).Append(", kontrakt ").Append(_dMerc).Append(", warsztaty ").Append(_dShop).Append(", karawany i partie ").Append(_dCarPar)
              .Append(" (w tym przelewy w rodzie ").Append(_dFamily).Append("), inne ").Append(_dOther).Append("; renta wsi ").Append(_dRentV).Append(", zawor miast ").Append(_dRentT).Append(" (wlasne ").Append(_dOwn).Append(')')
              .Append(", zawor zamkow ").Append(_dRentC).Append(" (wlasne ").Append(_dOwnC).Append(')')
              .Append(", zwrot korony ").Append(_dRefund).Append(", mennica i monopole ").Append(_dMint).Append(", jednorazowe ").Append(_dOnce).Append(" (podwojne ").Append(_dDouble).Append("), ziemia bez osady u ").Append(_dLooseN).Append(" rodow")
              .Append(" | inne z modelu dzis: znane linie (majatki BK, rada BK, podatki od wasali BK, sluzba u lorda BK, trybut, wezwanie do wojny, umowy handlowe, perk, zaulki) ").Append(_dKnown)
              .Append(", wplyw bez opisu (m.in. 'za tier' gry) ").Append(_dUnlisted)
              .Append(", nierozpoznane: modul ").Append(_dUnrecAbs).Append(" zl = ").Append(unrecPct.ToString("0.00", inv)).Append("% wplywu D169 doby (prog testu <= 2%; suma ").Append(_dUnrec).Append(")")
              .Append(" - najwieksze: ").Append(unk.Count > 0 ? string.Join(", ", unk.ToArray()) : "-")
              .Append(" | bankruci do 168 (K39 lub bankrut Banku, rody AI): ").Append(bankAny)
              .Append(" (K39 - kiesa glowy 0 i zold przyciety >= 7 dob z rzedu: ").Append(bankrupt).Append(", w tym z doba bez znanego salda ").Append(bankBlind)
              .Append("; bankruci Banku: ").Append(bankIb).Append("; oba: ").Append(bankBoth).Append(')')
              .Append(" | podatek wsi BK przy wyplacie do posiadacza de iure innego niz glowa rodu: ").Append(_dDeJureN).Append(" wsi, ").Append(_dDeJureGold)
              .Append(" zl dzis (z innego rodu ").Append(_dDeJureOtherN).Append(" wsi, ").Append(_dDeJureOtherGold).Append(" zl) - w D169 i ziemi u posiadacza faktycznego")
              .Append(" | wsie bez danych BK (podatek pominiety): ").Append(_dVNoPop)
              .Append(" | czas D stalego (ms na dobe): poczatek doby ").Append(Ms(_ticksStableBegin)).Append(", rozbicie rodow ").Append(Ms(_ticksStableClans))
              .Append(", linia poprzedniej doby ").Append(Ms(LastTicksStableLine)).Append("; dochod modelu z opisami (caly) ").Append(Ms(LastTicksModel))
              .Append(" | BK: ").Append(_bkNote)
              .Append(_stumbles > 0 ? " | potkniecia ksiegi " + _stumbles : "").Append('.');
            return sb.ToString();
        }
    }
}
