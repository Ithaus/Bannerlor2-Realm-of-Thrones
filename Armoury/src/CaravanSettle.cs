using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 259 (27.10, Jeff: "czemu moja karawana nic nie zarabia", "te karawany", "tak zacznij od tych pieciu"; audyt docs/AUDYT-2026-10-27.md rozdz. 4) -
    /// CODZIENNE ROZLICZENIE KARAWAN RODOW (klucz CaravanClanSettlement) i raport majatku gracza (CaravanSettle.Player.cs, klucz CaravanReports).
    /// Dowod (log Jeffa 26/27.10): karawana gracza oddala w 25 dobach raz 729 (E3/9, "wplyw caravan 729"); kazda linia E3-T2 "graczowi 0/0" - E3-T2 pomija
    /// karawany z wodzem, gra placi 10%/dobe kiesy ponad 10 000 (AddIncomeFromParty), BK tylko przy wjezdzie do siedziby (AddRealisticIncome), a karawany
    /// zamieniaja kiese w towar (zakup BK pelny od 5 000 w kiesie) - 484 z 490 karawan mialo kiese nie wieksza niz kapital.
    /// Regula (jedna dla gracza i lordow; Settled): karawana czynna, nie konwoj DTE (DoNotMakeNewDecisions), wlasciciel zywy w AliveLords albo Companions
    /// swojego rodu (ta sama lista, z ktorej gra i BK licza dochod i zold karawan rodu), wodz nie glowa rodu. Notable - dalej E3-T2 (bez zmian).
    /// Raz na dobe na karawane, w rozliczeniu rodu gry (prefiks DefaultClanFinanceModel.AddIncomeFromParty, Priority.First - przed prefiksem BK):
    ///   kapital = CaravanModel.GetInitialTradeGold(wlasciciel, morska, elitarna) (gracz 15 000 / 22 500 / 35 000 / 45 000, lord 10 000 / 17 500 / 30 000 / 40 000),
    ///             a gdy E3/7 dal karawanie mniej (zaplacone przez gracza, cena BK) - min(model, dane) (recenzja 259, rekordy arm_259_kapital);
    ///   wartosc = kiesa + towar (CaravanGoods.WorthOf: nizsza z kosztu z ksiegi E3-T1 i rynku; bez kosztu - wartosc x min(1, srednia indeksu));
    ///   podloga = PartyGoldLowerThreshold (5 000 - prog doplaty BK i gry) + zold doby (BK sprawdza kiese PO zdjeciu zoldu; bez tego co dobe +200 BK i z
    ///             powrotem); gdy rod NIE zwraca zoldu (wodz z tytulem Lordship albo glowa rodu - PartyExpensesPrefix BK; bez BK) - 30 dni zoldu (E3-T2 KeepWageDays);
    ///   wyplata = max(0, min(wartosc - kapital, kiesa - podloga)) - tylko zarobione, nigdy towar przed sprzedaza, nic nie wraca od wlasciciela.
    /// Platnik -> odbiorca: kiesa karawany -> saldo rodu (wynik AddIncomeFromParty idzie do "Caravan income" w finansach rodu, gra dopisuje saldo glowie rodu);
    /// nic z niczego. Te same skutki co wyplata gry: XP handlu wodza, slawa za Great Investor, zdarzenie gracza OnPlayerEarnedGoldFromAsset(Caravan).
    /// Przy applyWithdrawals = false tylko odczyt (podglad finansow, AI, KingdomTreasury, IronBank) - bez zapisu ksiegi i bez MBRandom (lista straznika m105).
    /// Zastapione / zablokowane dla rozliczanych (nic dwa razy): gra 10% (prefiks zwraca false), BK AddRealisticIncome (prefiks SettlePre), E3/9 (Seal31B.ProfitPre /
    /// ProfitPost), E3-T2 (CaravanGoods.ProfitToOwners - licznik "rozliczane przez rod (259)"), tygodniowy komunikat BK "awaiting deposit" (WeekPre).
    /// W bitwie albo rozwiazywana - wyplata 0 (kiesa do rodu przy rozwiazaniu - E3/7). Wylaczony klucz albo brak latki AddIncomeFromParty = wszystko jak dotad.
    /// Linia doby "Karawany rodow - rozliczenie dnia (259): dzien N" (swiat) i start sesji; wyjatek per wywolanie - licznik potkniec.
    /// </summary>
    internal static partial class CaravanSettle
    {
        private const string P = "Karawany rodow - rozliczenie dnia (259): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int DepBk = 1, DepE39 = 2, DepEnd = 3;      // NoteDeposit: BK przy wjezdzie, E3/9, kiesa przy rozwiazaniu (E3/7)
        private const int BkKeep = 10000;                          // prog wplaty BK i E3/9 (ta sama liczba co BK AddRealisticIncome) - tylko liczniki "bylo by"

        private static Settings S { get { return Settings.Current; } }
        internal static bool On { get { var s = S; return s != null && s.CaravanClanSettlement; } }
        private static bool Reports { get { var s = S; return s != null && s.CaravanReports; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        // ------------------------------------------------------------ latki (raz na proces)
        private static Harmony _harmony;
        internal static void SetHarmony(Harmony h) { _harmony = h; }
        private static bool _hooksTried, _wIncome, _wBkIncome, _wBkWeek, _wTick, _bkRefund;
        private static string _hookMiss = "-";
        private static MethodInfo _mIncome;

        // ------------------------------------------------------------ liczniki (od ostatniej linii doby; wywolania z rozliczen wszystkich rodow)
        private static readonly object _lk = new object();
        private static int _dN, _dNPl, _dNLord, _dPaidN, _dBelow, _dNoCash, _dNoRefund, _dConvoy, _dBusy, _dDup, _dVanN, _dBkN, _dE39N, _dWeekN, _dSt;
        private static long _dPaid, _dPaidPl, _dPaidLord, _dCtlFrom, _dCtlTo, _dVanV, _dBkV, _dTicks, _dReads;
        private static int _dCapE37, _dCapE37Low, _dCapModel, _dFundNew;   // recenzja 259: zrodlo kapitalu w rozliczeniu doby, nowe zapisy kapitalu E3/7
        private static int _sSt, _sDup;
        private static readonly Dictionary<MobileParty, int> _paidDay = new Dictionary<MobileParty, int>();   // straz: jedna wyplata na karawane na dobe
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _startDone;

        /// <summary>Wyplata w linii "Pieniadz swiata" F4 i "Obieg: notable i karawany" (MoneyLedger.Daily - po Daily tej doby).</summary>
        internal static long LastPaid, LastPaidPlayer;

        /// <summary>Konstruktor ArmouryBehavior (przed SyncData): stan sesji od zera; latki zostaja w procesie.</summary>
        internal static void Reset()
        {
            lock (_lk) { ZeroDay(); _paidDay.Clear(); }
            lock (_funded) { _funded.Clear(); _fundImp = _fundImpBad = 0; _fundHad = false; }
            _sSt = _sDup = 0; _err.Clear(); _startDone = false;
            LastPaid = LastPaidPlayer = 0;
            PlayerReset();
            TestReset();
        }

        private static void ZeroDay()
        {
            _dN = _dNPl = _dNLord = _dPaidN = _dBelow = _dNoCash = _dNoRefund = _dConvoy = _dBusy = _dDup = _dVanN = _dBkN = _dE39N = _dWeekN = _dSt = 0;
            _dPaid = _dPaidPl = _dPaidLord = _dCtlFrom = _dCtlTo = _dVanV = _dBkV = _dTicks = _dReads = 0;
            _dCapE37 = _dCapE37Low = _dCapModel = _dFundNew = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            lock (_lk) { _dSt++; _sSt++; }
            try { bool first; lock (_err) first = _err.Add(where); if (first) Log.Error("CaravanSettle." + where, e); } catch { }
        }

        // ------------------------------------------------------------ kto jest rozliczany
        private const int WOk = 0, WNotCaravan = 1, WInactive = 2, WConvoy = 3, WNoOwner = 4, WOutside = 5, WHeadLeads = 6;

        private static int Classify(MobileParty mp)
        {
            if (mp == null || !mp.IsCaravan) return WNotCaravan;
            if (!mp.IsActive) return WInactive;
            if (Measure31.IsDteConvoy(mp)) return WConvoy;          // konwoj DTE nie handluje (E3/7 punkt 1)
            var owner = mp.Owner;
            if (owner == null || !owner.IsAlive) return WNoOwner;
            var c = owner.Clan;
            if (c == null || c.IsBanditFaction) return WOutside;
            if (!c.AliveLords.Contains(owner) && !c.Companions.Contains(owner)) return WOutside;   // notabl, wedrowiec spoza rodu - E3-T2
            if (mp.LeaderHero != null && mp.LeaderHero == c.Leader) return WHeadLeads;         // gra nie liczy dochodu z partii glowy rodu
            return WOk;
        }

        /// <summary>Karawana rozliczana przez rod (klucz, kampania, latka AddIncomeFromParty wpieta - bez niej nikt by jej nie rozliczal, wiec nic nie blokujemy).
        /// Wolaja: E3-T2 (CaravanGoods.ProfitToOwners), E3/9 (Seal31B.ProfitPre / ProfitPost), prefiksy BK ponizej.</summary>
        internal static bool Settled(MobileParty mp)
        {
            try { return _wIncome && On && Live && Classify(mp) == WOk; }
            catch { return false; }
        }

        // ------------------------------------------------------------ tytuly BK wodza (zwrot zoldu: PartyExpensesPrefix pomija wodza z Lordship i glowe rodu)
        private static PropertyInfo _pCfgInst, _pTm;
        private static MethodInfo _mDeJure;
        private static bool _tmTried;
        private static readonly Dictionary<Type, MemberInfo> _ttype = new Dictionary<Type, MemberInfo>();

        /// <summary>BK TitleManager.GetAllDeJure(h): any - jakikolwiek tytul (BK AddIncomeFromPartyPrefix: wodz z tytulem - gra nie placi 10%), lordship - tytul Lordship.</summary>
        private static void Titles(Hero h, out bool any, out bool lordship)
        {
            any = lordship = false;
            if (h == null) return;
            try
            {
                if (!_tmTried)
                {
                    _tmTried = true;
                    var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                    _pCfgInst = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                    _pTm = cfgT != null ? AccessTools.Property(cfgT, "TitleManager") : null;
                    _mDeJure = _pTm != null ? AccessTools.Method(_pTm.PropertyType, "GetAllDeJure", new[] { typeof(Hero) }) : null;
                }
                if (_mDeJure == null || _pCfgInst == null) return;
                var cfg = _pCfgInst.GetValue(null, null);
                var tm = cfg != null ? _pTm.GetValue(cfg, null) : null;
                if (tm == null) return;
                var list = _mDeJure.Invoke(tm, new object[] { h }) as IEnumerable;
                if (list == null) return;
                foreach (var t in list)
                {
                    if (t == null) continue;
                    any = true;
                    MemberInfo mi;
                    var tt = t.GetType();
                    lock (_ttype)
                    {
                        if (!_ttype.TryGetValue(tt, out mi))
                        {
                            mi = (MemberInfo)AccessTools.Property(tt, "TitleType") ?? AccessTools.Field(tt, "TitleType") ?? AccessTools.Field(tt, "type");
                            _ttype[tt] = mi;
                        }
                    }
                    object v = mi is PropertyInfo ? ((PropertyInfo)mi).GetValue(t, null) : mi is FieldInfo ? ((FieldInfo)mi).GetValue(t) : null;
                    if (v != null && v.ToString() == "Lordship") { lordship = true; return; }
                }
            }
            catch { }
        }

        /// <summary>Czy rod NIE zwroci karawanie zoldu: bez prefiksu BK PartyExpensesPrefix (gra - karawana placi sama), wodz glowa jakiegos rodu, wodz z Lordship.</summary>
        private static bool NoRefund(MobileParty mp)
        {
            if (!_bkRefund) return true;
            var h = mp.LeaderHero;
            if (h == null) return false;
            if (h.Clan != null && h.Clan.Leader == h) return true;
            bool any, lord;
            Titles(h, out any, out lord);
            return lord;
        }

        // ------------------------------------------------------------ regula
        internal struct Calc
        {
            public int Purse, Capital, Floor, Wage, Payout, Kinds, Units;
            public long Goods, Cost, Market, Worth, Other;
            public bool NoRefund;
            public int CapModel, Funded;          // recenzja 259: kapital z modelu gry i dany przez E3/7 (-1 - brak zapisu, kapital z modelu)
            public bool CapE37 { get { return Funded >= 0; } }
        }

        /// <summary>Wyplata wedlug reguli - TYLKO ODCZYT (kiesa, juki, ksiega E3-T1 bez zapisu, model kapitalu i progu gry).</summary>
        internal static Calc Compute(MobileParty mp, Hero owner)
        {
            var c = new Calc();
            var ms = Campaign.Current.Models;
            var cpc = mp.CaravanPartyComponent;
            c.Purse = mp.PartyTradeGold;
            c.CapModel = ms.CaravanModel.GetInitialTradeGold(owner, cpc != null && cpc.CanHaveNavalNavigationCapability, cpc != null && cpc.IsElite);
            c.Funded = FundedOf(mp);
            c.Capital = c.Funded >= 0 ? Math.Min(c.CapModel, c.Funded) : c.CapModel;   // recenzja 259: kapital = to, co karawana dostala (E3/7), nie wiecej niz model
            c.Wage = Math.Max(0, mp.TotalWage);
            c.NoRefund = NoRefund(mp);
            c.Floor = ms.ClanFinanceModel.PartyGoldLowerThreshold + (c.NoRefund ? CaravanGoods.KeepWageDays * c.Wage : c.Wage);
            c.Goods = CaravanGoods.WorthOf(mp, out c.Cost, out c.Market, out c.Kinds, out c.Units, out c.Other);
            c.Worth = c.Purse + c.Goods;
            c.Payout = Formula(c.Worth, c.Capital, c.Purse, c.Floor);
            return c;
        }

        /// <summary>284 (CaravanTrade284, zasada kapitalu obrotowego przy zakupie): kapital i podloga kiesy - TE SAME liczby co Compute dla karawany rozliczanej przez rod
        /// (kapital = min(model, dane E3/7), podloga = PartyGoldLowerThreshold + zold doby albo 30 dni zoldu bez zwrotu); karawana spoza rozliczenia (notabl - E3-T2):
        /// kapital z modelu gry, podloga = PartyGoldLowerThreshold + zold doby (zold idzie z kiesy, notabl dopelnia kiese do progu). Tylko odczyt.</summary>
        internal static void CapFloor(MobileParty mp, out int capital, out int floor)
        {
            var ms = Campaign.Current.Models;
            var cpc = mp.CaravanPartyComponent;
            int model = ms.CaravanModel.GetInitialTradeGold(mp.Owner, cpc != null && cpc.CanHaveNavalNavigationCapability, cpc != null && cpc.IsElite);
            int wage = Math.Max(0, mp.TotalWage);
            if (Settled(mp))
            {
                int f = FundedOf(mp);
                capital = f >= 0 ? Math.Min(model, f) : model;
                floor = ms.ClanFinanceModel.PartyGoldLowerThreshold + (NoRefund(mp) ? CaravanGoods.KeepWageDays * wage : wage);
            }
            else
            {
                capital = model;
                floor = ms.ClanFinanceModel.PartyGoldLowerThreshold + wage;
            }
        }

        /// <summary>max(0, min(wartosc - kapital, kiesa - podloga)) - ta sama postac co K259-2 w sprawdz_logi.</summary>
        internal static int Formula(long worth, int capital, int purse, int floor)
        {
            long p = Math.Min(worth - capital, (long)purse - floor);
            return (int)Math.Max(0L, Math.Min(int.MaxValue, p));
        }

        /// <summary>Czy gra zaplacilaby 10% (BK AddIncomeFromPartyPrefix: wodz bez tytulu - gra; bez wodza - tylko bez Realistic Caravan Income).</summary>
        private static bool VanillaWould(MobileParty mp, Clan clan)
        {
            var h = mp.LeaderHero;
            if (h != null && clan != null && h != clan.Leader) { bool any, lord; Titles(h, out any, out lord); return !any; }
            return !Seal31B.RealisticCaravanIncome();
        }

        // ------------------------------------------------------------ H1: AddIncomeFromParty (rozliczenie rodu gry)
        public static bool IncomePrefix(MobileParty __0, Clan __1, bool __3, ref int __result)
        {
            if (__0 == null || !__0.IsCaravan || !_wIncome) return true;
            long t0 = Stopwatch.GetTimestamp();
            int moved = -1;                                             // >= 0: kiesa juz zdjeta - po wyjatku nie oddajemy grze (bylaby druga wyplata)
            try
            {
                if (!On || !Live) return true;
                int why = Classify(__0);
                if (why != WOk)
                {
                    if (__3 && why == WConvoy) lock (_lk) _dConvoy++;
                    return true;
                }
                var owner = __0.Owner;
                bool busy = __0.MapEvent != null || __0.IsDisbanding;
                if (!__3)
                {
                    // podglad (finanse rodu, AI, KingdomTreasury, IronBank): tylko odczyt
                    lock (_lk) _dReads++;
                    __result = busy ? 0 : Compute(__0, owner).Payout;
                    return false;
                }
                int today = Today;
                lock (_lk)
                {
                    int ld;
                    if (_paidDay.TryGetValue(__0, out ld) && ld == today) { _dDup++; _sDup++; __result = 0; return false; }
                    _paidDay[__0] = today;
                }
                var c = Compute(__0, owner);
                bool pl = owner.Clan == Clan.PlayerClan;
                bool van = c.Purse > BkKeep && VanillaWould(__0, __1);
                lock (_lk)
                {
                    _dN++; if (pl) _dNPl++; else _dNLord++;
                    if (c.NoRefund) _dNoRefund++;
                    if (c.CapE37) { _dCapE37++; if (c.Funded < c.CapModel) _dCapE37Low++; } else _dCapModel++;
                    if (van) { _dVanN++; _dVanV += (c.Purse - BkKeep) / 10; }
                }
                if (busy)
                {
                    lock (_lk) _dBusy++;
                    __result = 0;
                    if (pl) PlayerCalc(__0, c, 0, true);
                    return false;
                }
                int pay = c.Payout;
                moved = 0;
                if (pay > 0)
                {
                    int g0 = __0.PartyTradeGold;
                    __0.PartyTradeGold = g0 - pay;
                    moved = Math.Max(0, g0 - __0.PartyTradeGold);
                }
                __result = moved;
                lock (_lk)
                {
                    _dCtlFrom += moved; _dCtlTo += __result;
                    if (moved > 0) { _dPaidN++; _dPaid += moved; if (pl) _dPaidPl += moved; else _dPaidLord += moved; }
                    if (c.Worth < c.Capital) _dBelow++;
                    else if (pay <= 0 && c.Worth > c.Capital) _dNoCash++;
                }
                if (moved > 0)
                {
                    // te same skutki co wyplata gry (AddIncomeFromParty): XP handlu wodza, slawa za Great Investor, zdarzenie gracza
                    try
                    {
                        if (__0.LeaderHero != null) SkillLevelingManager.OnTradeProfitMade(__0.LeaderHero, moved);
                        var oc = __0.Party != null && __0.Party.Owner != null ? __0.Party.Owner.Clan : null;
                        if (oc != null && oc.Leader != null && oc.Leader.GetPerkValue(DefaultPerks.Trade.GreatInvestor)) oc.AddRenown(DefaultPerks.Trade.GreatInvestor.PrimaryBonus);
                        if (__1 == Clan.PlayerClan && CampaignEventDispatcher.Instance != null)
                            CampaignEventDispatcher.Instance.OnPlayerEarnedGoldFromAsset(DefaultClanFinanceModel.AssetIncomeType.Caravan, moved);
                    }
                    catch (Exception e) { Stumble("IncomePrefix(skutki)", e); }
                }
                if (pl) PlayerCalc(__0, c, moved, false);
                TestCapture(__0, c, moved);
                return false;
            }
            catch (Exception e)
            {
                Stumble("IncomePrefix", e);
                if (moved >= 0) { __result = moved; return false; }
                return true;                                            // nic nie zdjete - gra jak dotad
            }
            finally { long dt = Stopwatch.GetTimestamp() - t0; lock (_lk) _dTicks += dt; }
        }

        // ------------------------------------------------------------ H2: BK AddRealisticIncome (wplata przy wjezdzie)
        public static bool SettlePre(MobileParty __0, Settlement __1)
        {
            try
            {
                if (!Settled(__0)) return true;
                int g = __0.PartyTradeGold;
                var o = __0.Owner;
                // ten sam warunek miejsca co BK (siedziba, osada wlasciciela, wlasciciel w osadzie; bez udzialowcow) - tylko licznik "bylo by"
                bool place = __1 != null && o != null && (__1 == o.HomeSettlement || __1.Owner == o || __1.HeroesWithoutParty.Contains(o)
                             || (o.PartyBelongedTo != null && __1.Parties.Contains(o.PartyBelongedTo)));
                if (g > BkKeep && place && Seal31B.RealisticCaravanIncome()) lock (_lk) { _dBkN++; _dBkV += g - BkKeep; }
                return false;
            }
            catch (Exception e) { Stumble("SettlePre", e); return true; }
        }

        /// <summary>Seal31B.ProfitPre: rozliczana karawana weszla do osady - licznik "E3/9 n" (gdy E3/9 by placil: lord, miasto jego krolestwa, kiesa ponad 10 000).</summary>
        internal static void NoteE39Blocked(MobileParty mp, Settlement st)
        {
            try
            {
                var o = mp.Owner;
                if (o == null || !o.IsLord || o.Clan == null || o.Clan.Kingdom == null || st == null || !st.IsTown || st.MapFaction != o.Clan.Kingdom) return;
                if (mp.PartyTradeGold > BkKeep) lock (_lk) _dE39N++;
            }
            catch { }
        }

        // ------------------------------------------------------------ H3: BK OnWeeklyCaravanLedger ("awaiting deposit on next visit" - nieprawda przy rozliczeniu)
        public static bool WeekPre()
        {
            try { if (_wIncome && On && Live) { lock (_lk) _dWeekN++; return false; } }
            catch { }
            return true;
        }

        // ------------------------------------------------------------ zakladanie latek (ArmouryBehavior, start sesji - PO Seal31B.EnsureHooks)
        internal static void EnsureHooks()
        {
            lock (_lk) { ZeroDay(); _paidDay.Clear(); }
            SessionAtLoad();
            if (_harmony == null || Campaign.Current == null || _hooksTried) return;
            _hooksTried = true;
            var miss = new List<string>();
            try
            {
                var m = AccessTools.Method(typeof(DefaultClanFinanceModel), "AddIncomeFromParty");
                var ps = m != null ? m.GetParameters() : null;
                if (m != null && m.ReturnType == typeof(int) && ps.Length == 4 && ps[0].ParameterType == typeof(MobileParty) && ps[1].ParameterType == typeof(Clan)
                    && ps[3].ParameterType == typeof(bool))
                {
                    _harmony.Patch(m, prefix: new HarmonyMethod(typeof(CaravanSettle), nameof(IncomePrefix)) { priority = Priority.First });
                    _mIncome = m; _wIncome = true;
                }
                else miss.Add("AddIncomeFromParty (inna sygnatura)");
            }
            catch (Exception e) { miss.Add("AddIncomeFromParty (blad: " + e.Message + ")"); Stumble("EnsureHooks(AddIncomeFromParty)", e); }
            var bpT = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
            try
            {
                var m = bpT != null ? AccessTools.Method(bpT, "AddRealisticIncome", new[] { typeof(MobileParty), typeof(Settlement) }) : null;
                if (m != null) { _harmony.Patch(m, prefix: new HarmonyMethod(typeof(CaravanSettle), nameof(SettlePre)) { priority = Priority.First }); _wBkIncome = true; }
                else miss.Add("BK AddRealisticIncome");
            }
            catch (Exception e) { miss.Add("BK AddRealisticIncome (blad: " + e.Message + ")"); Stumble("EnsureHooks(AddRealisticIncome)", e); }
            try
            {
                var m = bpT != null ? AccessTools.Method(bpT, "OnWeeklyCaravanLedger") : null;
                if (m != null && m.GetParameters().Length == 0) { _harmony.Patch(m, prefix: new HarmonyMethod(typeof(CaravanSettle), nameof(WeekPre)) { priority = Priority.First }); _wBkWeek = true; }
                else miss.Add("BK OnWeeklyCaravanLedger");
            }
            catch (Exception e) { miss.Add("BK OnWeeklyCaravanLedger (blad: " + e.Message + ")"); Stumble("EnsureHooks(OnWeeklyCaravanLedger)", e); }
            try
            {
                var m = AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "DailyTickClan", new[] { typeof(Clan) });
                if (m != null)
                {
                    // Priority.First - postfiks przed SoldierPay.ClanTickPostfix (Last), ktory czysci rekordy zoldu tego rozliczenia
                    _harmony.Patch(m, prefix: new HarmonyMethod(typeof(CaravanSettle), nameof(TickPre)) { priority = Priority.First },
                                      postfix: new HarmonyMethod(typeof(CaravanSettle), nameof(TickPost)) { priority = Priority.First });
                    _wTick = true;
                }
                else miss.Add("DailyTickClan");
            }
            catch (Exception e) { miss.Add("DailyTickClan (blad: " + e.Message + ")"); Stumble("EnsureHooks(DailyTickClan)", e); }
            try
            {
                // zwrot zoldu karawan rodu: prefiks BK PartyExpensesPrefix na AddExpensesFromPartiesAndGarrisons (bkecon 268-400)
                var m = AccessTools.Method(typeof(DefaultClanFinanceModel), "AddExpensesFromPartiesAndGarrisons");
                var info = m != null ? Harmony.GetPatchInfo(m) : null;
                if (info != null)
                    foreach (var p in info.Prefixes)
                        if (p != null && p.PatchMethod != null && p.PatchMethod.Name == "PartyExpensesPrefix") _bkRefund = true;
            }
            catch (Exception e) { Stumble("EnsureHooks(PartyExpensesPrefix)", e); }
            EnsureHooks305(miss);   // 305a: osobna linia zoldu karawan gracza (prefiks / postfiks wydatkow rodu, postfiks CalculatePartyWage) - po wykryciu _bkRefund
            _hookMiss = miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-";
        }

        /// <summary>Start sesji (ArmouryBehavior, po SyncData): karawany i warsztaty gracza obecne przy wczytaniu - ich nowe rekordy licza "od wczytania".</summary>
        private static void SessionAtLoad() { try { EnsureAtLoad(); } catch (Exception e) { Stumble("EnsureAtLoad", e); } }

        /// <summary>Nasz prefiks pierwszy na AddIncomeFromParty (najwyzszy priorytet; przy rownym - wczesniej wpiety). Linia startu i T3.</summary>
        private static bool IncomeFirst(out string note)
        {
            note = "-";
            try
            {
                var info = _mIncome != null ? Harmony.GetPatchInfo(_mIncome) : null;
                if (info == null) { note = "brak latek"; return false; }
                HarmonyLib.Patch ours = null;
                foreach (var p in info.Prefixes) if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(CaravanSettle)) ours = p;
                if (ours == null) { note = "naszej latki brak"; return false; }
                var others = new List<string>();
                bool first = true;
                foreach (var p in info.Prefixes)
                {
                    if (p == null || ReferenceEquals(p, ours)) continue;
                    others.Add((p.PatchMethod != null && p.PatchMethod.DeclaringType != null ? p.PatchMethod.DeclaringType.Name + "." + p.PatchMethod.Name : "?") + " " + p.priority);
                    if (p.priority > ours.priority || (p.priority == ours.priority && p.index < ours.index)) first = false;
                }
                note = "nasza " + ours.priority + "; inne: " + (others.Count > 0 ? string.Join(", ", others.ToArray()) : "-");
                return first;
            }
            catch (Exception e) { note = "blad " + e.GetType().Name; return false; }
        }

        private static string Tn(bool b) { return b ? "TAK" : "NIE"; }

        // ------------------------------------------------------------ raz na dobe (ArmouryBehavior: PO CaravanGoods.Daily, PRZED MoneyLedger.Daily)
        internal static void Daily()
        {
            var s = S;
            if (s == null || Campaign.Current == null) return;
            int day = Today - 1;
            if (!_startDone)
            {
                _startDone = true;
                try { StartLine(s); } catch (Exception e) { Stumble("StartLine", e); }
            }
            long paid = 0, paidPl = 0;
            try
            {
                var sb = new StringBuilder(1200);
                lock (_lk)
                {
                    paid = _dPaid; paidPl = _dPaidPl;
                    sb.Append(P).Append("dzien ").Append(day);
                    if (!s.CaravanClanSettlement) sb.Append(" | WYLACZONE (MCM Caravan Clan Settlement) - gra, BK, E3/9 i E3-T2 jak dotad");
                    else if (!_wIncome) sb.Append(" | NIECZYNNE - brak latki AddIncomeFromParty (" + _hookMiss + ") - wszystko jak dotad");
                    else sb.Append(" | CZYNNE");
                    double ms = _dTicks * 1000.0 / Stopwatch.Frequency;
                    sb.Append(" | karawan rodow ").Append(_dN).Append(" (gracza ").Append(_dNPl).Append(", lordow ").Append(_dNLord).Append("), z wyplata ").Append(_dPaidN)
                      .Append(" za ").Append(_dPaid).Append(" zl (graczowi ").Append(_dPaidPl).Append(", lordom ").Append(_dPaidLord).Append("), ponizej kapitalu ").Append(_dBelow)
                      .Append(", wartosc>kapital ale kiesa<=podloga ").Append(_dNoCash).Append(", bez zwrotu zoldu (tytul) ").Append(_dNoRefund)
                      .Append(", konwoje pominiete ").Append(_dConvoy).Append(", w bitwie/rozwiazywane ").Append(_dBusy)
                      .Append(" | kontrola: kiesy -").Append(_dCtlFrom).Append(" = wynik prefiksu +").Append(_dCtlTo).Append(" (roznica ").Append(_dCtlFrom - _dCtlTo).Append(')')
                      .Append(" | zastapione/zablokowane: gra 10% ").Append(_dVanN).Append(" (bylo by ").Append(_dVanV).Append("), BK przy wjezdzie ").Append(_dBkN)
                      .Append(" (bylo by ").Append(_dBkV).Append("), E3/9 ").Append(_dE39N).Append(", tydzien BK ").Append(_dWeekN)
                      .Append(" | drugie wywolanie w dobie ").Append(_dDup).Append(" (sesja ").Append(_sDup).Append(')')
                      .Append(" | czas regul ").Append(ms.ToString("0.00", Inv)).Append(" ms (podgladow ").Append(_dReads).Append(')')
                      .Append(" | potkniecia ").Append(_dSt).Append(" (sesja ").Append(_sSt).Append(')');
                    int fundN; lock (_funded) fundN = _funded.Count;
                    sb.Append(" | kapital karawan: z E3/7 ").Append(_dCapE37).Append(" (nizszy od modelu ").Append(_dCapE37Low).Append("), z modelu ").Append(_dCapModel)
                      .Append(", nowe zapisy E3/7 ").Append(_dFundNew).Append(" (rekordow ").Append(fundN).Append(").");
                    ZeroDay();
                    // porzadki strazy doby: karawany, ktorych juz nie ma
                    if (_paidDay.Count > 0)
                    {
                        var gone = new List<MobileParty>();
                        foreach (var kv in _paidDay) if (kv.Key == null || !kv.Key.IsActive) gone.Add(kv.Key);
                        foreach (var g in gone) _paidDay.Remove(g);
                    }
                }
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            LastPaid = paid; LastPaidPlayer = paidPl;
            try { FundPrune(); } catch (Exception e) { Stumble("FundPrune", e); }
            try { PlayerDaily(s, day); } catch (Exception e) { Stumble("PlayerDaily", e); }
            try { Daily305(s, day); } catch (Exception e) { Stumble("Daily305", e); }   // 305a: linia "Karawany gracza - osobna linia zoldu (305a)" z samotestem
            try { TestStep(day); } catch (Exception e) { Stumble("TestStep", e); }
        }

        // ------------------------------------------------------------ recenzja 259: kapital dany karawanie przez E3/7 (CaravanCapital: kupno BK, karawana gracza)
        // E3/7 daje karawanie min(kapital modelu, zaplacone) - np. konwoj morski gracza: model 35 000, zaplacone 30 000; konwoj lorda BK: model 30 000, cena BK 15 000.
        // Kapital w regule = min(model, dane); bez zapisu (start gry, karawany sprzed recenzji, E3/7 wylaczone, notable) - model jak dotad. Zapis: klucz arm_259_kapital.
        private static readonly Dictionary<string, int> _funded = new Dictionary<string, int>(StringComparer.Ordinal);
        private static int _fundImp, _fundImpBad;
        private static bool _fundHad;

        /// <summary>CaravanCapital.PlayerSettle (przelew miasto -> karawana gracza) i CreateFin (kupno BK, c.Cap): ile karawana dostala na handel.</summary>
        internal static void NoteFunded(MobileParty mp, long cap)
        {
            try
            {
                if (mp == null || mp.StringId == null) return;
                int v = (int)Math.Max(0L, Math.Min(int.MaxValue, cap));
                lock (_funded) _funded[mp.StringId] = v;
                lock (_lk) _dFundNew++;
            }
            catch (Exception e) { Stumble("NoteFunded", e); }
        }

        private static int FundedOf(MobileParty mp)
        {
            if (mp == null || mp.StringId == null) return -1;
            int v;
            lock (_funded) return _funded.TryGetValue(mp.StringId, out v) ? v : -1;
        }

        /// <summary>Raz na dobe: rekordy karawan, ktorych juz nie ma.</summary>
        private static void FundPrune()
        {
            lock (_funded) { if (_funded.Count == 0) return; }
            var live = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mp in MobileParty.AllCaravanParties) if (mp != null && mp.IsActive && mp.StringId != null) live.Add(mp.StringId);
            lock (_funded)
            {
                var gone = new List<string>();
                foreach (var k in _funded.Keys) if (!live.Contains(k)) gone.Add(k);
                foreach (var k in gone) _funded.Remove(k);
            }
        }

        internal static string ExportFunded()
        {
            var sb = new StringBuilder(256);
            lock (_funded) foreach (var kv in _funded) sb.Append(kv.Key).Append('|').Append(kv.Value.ToString(Inv)).Append('\n');
            return sb.ToString();
        }

        internal static void ImportFunded(string s)
        {
            lock (_funded)
            {
                _funded.Clear(); _fundImp = _fundImpBad = 0; _fundHad = s != null;
                if (string.IsNullOrEmpty(s)) return;
                foreach (var line in s.Split('\n'))
                {
                    if (line.Length == 0) continue;
                    var p = line.Split('|');
                    int v;
                    if (p.Length == 2 && p[0].Length > 0 && int.TryParse(p[1], NumberStyles.Integer, Inv, out v) && v >= 0) { _funded[p[0]] = v; _fundImp++; }
                    else _fundImpBad++;
                }
            }
        }

        private static void StartLine(Settings s)
        {
            string fn;
            bool first = IncomeFirst(out fn);
            Log.Info(P + "start sesji | klucz Caravan Clan Settlement " + (s.CaravanClanSettlement ? "tak" : "nie") + ", Caravan Reports " + (s.CaravanReports ? "tak" : "nie")
                     + " | latki: AddIncomeFromParty (First, przed BK) " + Tn(_wIncome && first) + ", BK AddRealisticIncome " + Tn(_wBkIncome)
                     + ", BK OnWeeklyCaravanLedger " + Tn(_wBkWeek) + ", DailyTickClan gracza " + Tn(_wTick) + " (kolejnosc prefiksow: " + fn + "; brak: " + _hookMiss + ")"
                     + " | BK zwrot zoldu karawan rodu (PartyExpensesPrefix) " + Tn(_bkRefund)
                     + " | Optym m105: latka na liscie znanych " + Tn(KingdomTreasury.KtKnows("Armoury:CaravanSettle.IncomePrefix"))
                     + " | rekordy z zapisu: karawan " + _impCars + ", warsztatow " + _impWs + " (bledne " + _impBad + (_impHad ? "" : "; brak klucza arm_259_majatek - liczniki od wczytania") + ")"
                     + " | regula: wyplata = min(wartosc - kapital, kiesa - podloga); kapital z CaravanModel.GetInitialTradeGold; podloga = PartyGoldLowerThreshold + zold doby"
                     + " (bez zwrotu zoldu przez rod - " + CaravanGoods.KeepWageDays + " dni zoldu); towar: nizsza z kosztu (ksiega E3-T1) i rynku (wartosc x srednia indeksu), bez kosztu - wartosc x min(1, srednia indeksu)."
                     + " | recenzja 259: kapital = min(model, dane przez E3/7) - rekordy kapitalu z zapisu " + _fundImp + " (bledne " + _fundImpBad + (_fundHad ? "" : "; brak klucza arm_259_kapital - karawany sprzed recenzji z modelu") + ")"
                     + "; towar liczy tez zwierzeta i inne przedmioty kupione z kiesy (ksiega E3-T1), bez jucznych do liczby ludzi.");
        }
    }
}
