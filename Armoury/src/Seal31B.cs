using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;
using CW = Armoury.CirculationWindows;

namespace Armoury
{
    /// <summary>
    /// ETAP 3.1 (paczka 164a) - KAWALKI E3/4, E3/8 (a, b, c) i E3/9 (PROJEKT-ETAP3-SZCZELNOSC-STATKI-2026-10-10 rozdz. 2 R4, R7, R8, R11, R14
    /// i rozdz. 3; rozpoznanie projekt31\B-drobne.md; krytyka K2, W11, W13, D13, D14). Kazdy kawalek za wlasnym wylacznikiem (wylaczony = stan sprzed);
    /// wyjatki liczone per obiekt (potkniecia), bez globalnych wylacznikow. Latki zakladane w kampanii (EnsureHooks), typy BK przez refleksje -
    /// brak metody = "BRAK" w linii startowej i ten kawalek nieczynny, reszta dziala.
    ///
    /// E3/4 (CaravanWageToPurse) - ZOLD KARAWAN DO SAKIEWEK LUDZI KARAWAN:
    ///  - karawany notabli: gra (NotablesCampaignBehavior.ManageCaravanExpensesOfNotable) zdejmuje zold z kasy karawany, a gdy ta nie ma - z kiesy notabla,
    ///    i nikomu go nie daje (log 457 dob: 143.8 tys. zl/dobe w nicosc). Prefiks liczy ta sama droga gry, ile zejdzie z ktorej kiesy (ta sama kolejnosc
    ///    od konca listy i ta sama doplata do 5 000 - przelew notabl -> karawana), postfiks sprawdza to migawka (kiesa notabla + kasy karawan) i oddaje
    ///    zold do sakiewki ludzi tej karawany (MenPurse); okno ksiegi O02 mierzy dalej "zold karawan notabli", a my odejmujemy oddane z tej samej pozycji
    ///    (CW.AddWorld - jak zwrot LevyGold: netto 0);
    ///  - karawany lordow: SoldierPay.Route (dotad "karawany i inne partie: bez zmian") -> sakiewka ludzi karawany + NoteWageRouted (jak partie lordow);
    ///  - wydatek: MenPurse.OnLeft - karawana wydaje cala sakiewke "na zycie" w miescie przy wyjezdzie, bez oszczednosci na sprzet (karawany nie kupuja
    ///    sprzetu), bez odziezy i bez tarczy zoldu (projekt E3/4; tarcza - etap 5 z K6);
    ///  - sakiewka rozbitej partii (W13, decyzja Jeffa 09.10 Q3b - jedna regula z kiesa w 3.2): zwyciezca z zywym bohaterem - jak dotad 1/3 - 2/3;
    ///    zwyciezca bez bohatera, ale handlujacy (banda, nie Inni) - do jego kiesy; inaczej kasa osady niszczyciela (milicja, zaloga, patrol), potem
    ///    najblizsze miasto (jak dotad). Poprawka po recenzji: karawana, ktorej 3.2 nie uznala za pokonana w bitwie przez przywodce zwyciezcow
    ///    (zwycieska strona bez ludzi - niszczyciel to przywodca PRZEGRANYCH; przylaczenie, zadania) - sakiewka jak w R10: kasa najblizszego miasta
    ///    (dotad dostawal ja niszczyciel, takze przegrany). Wydatek sakiewki karawany tylko przy wlaczonym E3/4 (wylaczony - droga partii jak dotad).
    /// E3/8a (BkWorkshopTaxToLord) - PODATEK WARSZTATOW BK NAPRAWDE PLACI NOTABL: BK BKClanFinanceModel.CalculateNotableDailyGoldChange odejmuje od wyplaty
    ///    notabla TaxExpenses kazdego warsztatu (podatek znika - gra zdjela z warsztatu wyplate brutto), a jednoczesnie dopisuje ten sam podatek panu miasta
    ///    linia "Taxes from {WORKSHOP}" (BKTaxModel; Armoury PopulationLaw.KeptTownLines ja zostawia) - para przez nicosc (54 tys. zl/dobe). Teraz: postfiks
    ///    BK oddaje notablowi podatek w wyplacie (gra wyplaca brutto), a postfiks DailyTickHero przelewa to, co zniknelo (min(podatek, brutto)), notabl ->
    ///    pan miasta warsztatu; czesc autonomii BK (0.6 x autonomia - ta sama, ktora obcinala linie panu) -> kasa miasta; linia "Taxes from" warsztatow
    ///    notabli znika z podatku miasta (PopulationLaw.TownTaxPostfix - NotableTaxLines). Wplata panu liczy sie jak renta miasta (PopulationLaw.RentToday
    ///    po dziennych rentach - RentMerge), wiec D ("ziemia"), powinnosci i budowy bez zmian; bez zdarzenia gry - ClanIncomeBook nie bierze jej
    ///    do "jednorazowych". Czynne tylko, gdy BK jest w lancuchu czynnego modelu finansow (sprawdzane na starcie sesji) - inaczej linia zostaje.
    ///    Poprawka po recenzji 3.1+3.3 (ta sama regula R7 - linia tylko, gdy ktos naprawde placi): linie "Taxes from" warsztatow lordow i gracza,
    ///    ktorych BK nie pobiera (UnpaidOwnerTaxLines: BK bierze "Workshop taxes" tylko glowie rodu, za miasta innych rodow i tylko przy czynnym
    ///    BKWorkshopModel - u Jeffa czynny NavalDLCWorkshopModel), tez znikaja z podatku miasta - bez przelewu, bo nikt ich nie placi (dotad z niczego).
    ///    Przy wyniku BK < 0 (podatek > dochod) gra wyplaca notablowi brutto, notabl oddaje cale brutto (nie wiecej - dotad doplacal z oszczednosci).
    ///    D11-3 (decyzja Jeffa 11.10, wylacznik WorkshopOwnerPaysTownTax): te linie (warsztaty lordow i gracza w miastach INNYCH rodow, ktorych BK nie pobiera)
    ///    placi teraz wlasciciel: raz na dobe (OwnerTaxDaily, nasz tick - po rentach PopulationLaw.Daily, przed RentMerge) ta sama kwota, ktora BK pokazuje
    ///    w 'Taxes from' (GetWorkshopTaxes = zysk x stawka BK, ostatnio policzona przez BK), czesc autonomii (0.6 x autonomia) do kasy miasta, reszta panu
    ///    miasta jako renta (RentMerge - jak E3/8a); najwyzej z kiesy wlasciciela (brak - placi ile ma, zaleglosc nie rosnie, licznik). Linia w podatku miasta
    ///    zostaje zdjeta (UnpaidOwnerTaxLines) - pan dostaje przelew, nie linie z niczego. Gracz ta sama regula (komunikat przy pierwszym poborze w sesji).
    ///    Warsztat czlonka rodu pana miasta - nie cudze miasto: linia zdjeta, bez przelewu (jak dotad). Nie dubluje podatku: gra (DefaultClanFinanceModel) nie ma
    ///    podatku warsztatow, BK pobiera 'Workshop taxes' tylko przy czynnym BKWorkshopModel (te warsztaty pomijamy - bkCharges), notabli - E3/8a.
    ///    121 (noc-p8, 25.10): kwoty GetWorkshopTaxes z egzemplarza BK w CZYNNYM lancuchu modelu finansow gry (TaxFin), nie z BannerKingsConfig - BKROTPatch
    ///    podmienia tam model na nowy egzemplarz, ktorego slownik nikt nie wypelnia (D11-3 pobieral 0). Placi tylko glowa rodu (gracz tez) - tylko jej warsztatom
    ///    gra wyplaca dochod, a BK przy tej wyplacie zapisuje kwote. To samo zrodlo czytaja linie warsztatow w podatku miasta (KeptTownLines, NotableTaxLines,
    ///    UnpaidOwnerTaxLines) - przy rencie znosza sie do zera. Poprawki po recenzji 121: (1) bez egzemplarza BK w lancuchu (albo bez latki DailyTickClan)
    ///    D11-3 nieczynne i cena warsztatu lorda / gracza bez podatku (zapas z konfiguracji BK zostaje tylko dla linii podatku miast); (2) kwota z prawdziwej
    ///    wyplaty rodu - migawka slownika BK w postfiksie DailyTickClan (ClanPayPost: stawka x zysk przed wyplata = 2 x stawka x wyplata, jak w cenie),
    ///    pobierana raz w OwnerTaxDaily od tego, kto dostal wyplate; (3) przy BK Workshop Tax To Lord = WYL KeptTownLines czyta jak przed 121 (konfiguracja
    ///    BK) - inaczej linie warsztatow lordow i gracza dochodzilyby panu miasta z niczego.
    /// E3/8b (BkLordFeesHaveRecipients) - OPLATY LORDOW BK MAJA ODBIORCE (W11): tytuly (roszczenie, nadanie, odebranie, uzurpacja) i dylematy -> skarbiec
    ///    krolestwa platnika (bez krolestwa - kasa najblizszego miasta), zmiana wydatkow dworu (CourtGrace.AddExpense) -> kasa siedziby rodu (ClanBudget.Seat),
    ///    przeniesienie dworu -> kasa nowej siedziby, pasowanie (wiano do 50 000 przy zalozeniu rodu rycerza) -> glowa nowego rodu. Kwota = to, co naprawde
    ///    zeszlo platnikowi w metodzie BK (migawka kiesy przed/po, najwyzej kwota BK). Postfiks oddaje ja platnikowi (okno ksiegi obiegu - migawka platnika,
    ///    finalizer o priorytecie zwyklym - widzi wtedy 0 w nicosc), a nasz finalizer o priorytecie Low (po oknie) przelewa ja odbiorcy.
    /// E3/8c (DeadHeroGoldStays) - ZLOTO ZMARLEGO ZOSTAJE W OSADZIE (R11, D13): prefiks KillCharacterAction.MakeDead (wolane tylko z ApplyInternal - PO
    ///    przekazaniu zlota glowie rodu albo nastepcy, PRZED zmiana stanu na Dead): co zmarlemu (nie gracz) zostalo - notabl: jego osada, inni: osada,
    ///    w ktorej przebywa, inaczej najblizsze miasto. Przelew przed wyjsciem ze swiata - pozycja "zloto bohaterow, ktorzy opuscili swiat" spada sama.
    /// E3/8d (Inni bez zoldu) - ODLOZONE wedlug projektu (zrodlo ich dochodu nieznane; zamkniecie samego zoldu zamieniloby pare na gromadzenie).
    /// E3/9 (CaravanProfitInRealmTown) - ZYSK KARAWANY PANA W KAZDYM MIESCIE JEGO KROLESTWA (D-9, swiadoma zmiana "najblizsze" -> "kazde", D14): BK
    ///    BKPartyBehavior.AddRealisticIncome wplaca zysk ponad 10 000 tylko w siedzibie / osadzie wlasciciela / u udzialowca; gdy BK nie wplacil, a cel jest
    ///    miastem krolestwa wlasciciela-lorda - ta sama wplata (przelew kasa karawany -> wlasciciel, te same skutki co w BK).
    /// Linia logu "Szczelnosc (164a/31b): dzien N" raz na dobe (po ksiedze pieniadza).
    /// </summary>
    internal static class Seal31B
    {
        private static Settings S { get { return Settings.Current; } }
        internal static bool WageOn { get { var s = S; return s != null && s.CaravanWageToPurse; } }
        internal static bool TaxOn { get { var s = S; return s != null && s.BkWorkshopTaxToLord; } }
        internal static bool FeesOn { get { var s = S; return s != null && s.BkLordFeesHaveRecipients; } }
        internal static bool DeadOn { get { var s = S; return s != null && s.DeadHeroGoldStays; } }
        internal static bool ProfitOn { get { var s = S; return s != null && s.CaravanProfitInRealmTown; } }
        internal static bool OwnerTaxOn { get { var s = S; return s != null && s.WorkshopOwnerPaysTownTax; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private static Harmony _harmony;
        internal static void SetHarmony(Harmony h) { _harmony = h; }
        private static bool _hooksTried;
        private static string _hookNote = "-";
        private static bool _wNotCar, _wTax, _wTick, _wDead, _wProfit;
        private static int _wFees;
        private static bool _taxLive;                     // E3/8a: BK w lancuchu czynnego modelu finansow i latki wpiete
        private static bool _linesHooked;                 // E3/8a: latka PopulationLaw.TownTaxPostfix siedzi na czynnym modelu podatku miast (druga noga - linia znika)
        private static string _taxChain = "-", _linesNote = "-";

        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static void Stumble(string where, Exception e)
        {
            System.Threading.Interlocked.Increment(ref _stumbles);
            try { lock (_err) { if (!_err.Add(where)) return; } Log.Error("Seal31B." + where, e); } catch { }
        }

        // ------------------------------------------------------------ liczniki doby
        // E3/4
        private static long _notWage, _notFromCar, _notFromNot, _notSkipped, _lordCarWage, _carSpent, _carPurseHero, _carPurseBand, _carPurseParty, _carPurseSettl, _carPurseTown;
        private static long _allPurseBand, _allPurseParty, _allPurseSettl, _carPurseOdd, _carPurseOddLost;
        /// <summary>f4-pomiary (recenzja): zold karawan notabli z kas karawan oddany do sakiewek ludzi - licznik doby (MoneyLedger.Daily czyta przed Seal31B.Daily/ZeroDay; tylko odczyt).</summary>
        internal static long NotFromCarToday { get { return _notFromCar; } }
        private static int _notN, _notMismatch, _lordCarN, _carSpentN, _carPurseN, _carPurseOddN;
        // E3/8a
        private static long _taxSeen, _taxZ, _taxToLords, _taxToTowns, _taxNoLord, _taxShort, _taxLinesCut, _unpaidCut;
        private static int _unpaidN;
        private static int _taxNotables, _taxNeg;
        private static readonly Dictionary<Clan, int> _rentPend = new Dictionary<Clan, int>();
        private static long _rentMerged;
        // D11-3: podatek warsztatow lordow i gracza w cudzych miastach placi wlasciciel
        private static long _otDue, _otToLords, _otToTowns, _otNoLord, _otShort, _otPlayer, _otPayout, _otDrop;
        private static int _otN, _otOwners, _otShortN, _otPlayerN, _otDropN;
        private static bool _otTold;                        // komunikat dla gracza przy pierwszym poborze w sesji
        // 121 (recenzja uwaga 2): podatek z prawdziwej wyplaty rodu - migawka kwoty BK w postfiksie DailyTickClan, pobierana raz w OwnerTaxDaily
        private sealed class OtPay { public Workshop W; public Hero Owner; public int Tax, Payout; }
        private static readonly List<OtPay> _otPay = new List<OtPay>();
        private static bool _wPay;                          // postfiks DailyTickClan (migawka) wpiety - bez niego D11-3 nieczynne
        // E3/8b - wedlug rodzaju
        private const int FClaim = 0, FCreate = 1, FRevoke = 2, FUsurp = 3, FDilemma = 4, FCourt = 5, FMove = 6, FKnight = 7, Fees = 8;
        private static readonly string[] FeeName = { "roszczenia", "nadania", "odebrania", "uzurpacje", "dylematy", "dwor", "przeniesienie dworu", "pasowanie" };
        private static readonly long[] _feePaid = new long[Fees], _feeToWallet = new long[Fees], _feeToTown = new long[Fees], _feeToHero = new long[Fees], _feeKept = new long[Fees];
        private static readonly int[] _feeN = new int[Fees];
        // E3/8c
        private static long _deadNotable, _deadWanderer, _deadHead, _deadOther, _deadLost; private static int _deadNotableN, _deadWandererN, _deadHeadN, _deadOtherN;
        // E3/9
        private static long _profitPaid, _profitBk; private static int _profitN, _profitBkN;

        private static void ZeroDay()
        {
            _notWage = _notFromCar = _notFromNot = _notSkipped = _lordCarWage = _carSpent = _carPurseHero = _carPurseBand = _carPurseParty = _carPurseSettl = _carPurseTown = 0;
            _allPurseBand = _allPurseParty = _allPurseSettl = _carPurseOdd = _carPurseOddLost = 0;
            _notN = _notMismatch = _lordCarN = _carSpentN = _carPurseN = _carPurseOddN = 0;
            _taxSeen = _taxZ = _taxToLords = _taxToTowns = _taxNoLord = _taxShort = 0; _taxNotables = _taxNeg = 0;
            System.Threading.Interlocked.Exchange(ref _taxLinesCut, 0);
            System.Threading.Interlocked.Exchange(ref _unpaidCut, 0); System.Threading.Interlocked.Exchange(ref _unpaidN, 0);
            _rentMerged = 0;
            _otDue = _otToLords = _otToTowns = _otNoLord = _otShort = _otPlayer = _otPayout = _otDrop = 0; _otN = _otOwners = _otShortN = _otPlayerN = _otDropN = 0;
            Array.Clear(_feePaid, 0, Fees); Array.Clear(_feeToWallet, 0, Fees); Array.Clear(_feeToTown, 0, Fees); Array.Clear(_feeToHero, 0, Fees); Array.Clear(_feeKept, 0, Fees);
            Array.Clear(_feeN, 0, Fees);
            _deadNotable = _deadWanderer = _deadHead = _deadOther = _deadLost = 0; _deadNotableN = _deadWandererN = _deadHeadN = _deadOtherN = 0;
            _profitPaid = _profitBk = 0; _profitN = _profitBkN = 0;
            _stumbles = 0;
        }

        // ------------------------------------------------------------ BK przez refleksje (raz na proces)
        private static MethodInfo _mTaxExp, _mGetWsTax, _mGetPop, _mFulfiller;
        private static PropertyInfo _pCfgInst, _pCfgFin, _pCfgPop, _pAutonomy, _pTaker, _pActGold, _pDilKingdom, _pBkSet, _pRealCar;
        private static FieldInfo _fMoveTown;
        private static object BkCfg() { return _pCfgInst != null ? _pCfgInst.GetValue(null, null) : null; }

        // ------------------------------------------------------------ zakladanie latek (ArmouryBehavior.EnsureHooks, start sesji)
        internal static void EnsureHooks()
        {
            ZeroDay(); _rentPend.Clear(); _pend = null; _tickHero = null; _otTold = false;   // nowa sesja / kampania - liczniki i stan od zera (latki zostaja w procesie)
            _otPay.Clear();   // 121: migawki wyplat poprzedniej kampanii (cudze obiekty Workshop) nie przechodza do nowej
            if (_harmony == null || Campaign.Current == null) return;
            if (!_hooksTried)
            {
                _hooksTried = true;
                var wired = new List<string>(); var missing = new List<string>();
                try { HookAll(wired, missing); }
                catch (Exception e) { missing.Add("blad: " + e.Message); Stumble("EnsureHooks", e); }
                _hookNote = (wired.Count > 0 ? string.Join(", ", wired.ToArray()) : "-") + "; BRAK: " + (missing.Count > 0 ? string.Join(", ", missing.ToArray()) : "-");
            }
            // E3/8a: czy odejmowanie podatku BK naprawde dziala w tej kampanii (lancuch czynnego modelu finansow) - co sesje
            // 121 (noc-p8): lancuch czytany zawsze (nie tylko przy latkach E3/8a) - egzemplarz BK z lancucha to zrodlo kwot podatku warsztatow (TaxFin)
            _fin = null;
            try
            {
                object bkObj; bool inChain = BkInChain(out _taxChain, out bkObj);
                _taxLive = _wTax && _wTick && inChain;
                var ms = Campaign.Current.Models;
                if (bkObj != null && _mGetWsTax != null && _mGetWsTax.DeclaringType.IsInstanceOfType(bkObj) && ms != null && ms.ClanFinanceModel != null)
                    _fin = new FinRef { Head = ms.ClanFinanceModel, Bk = bkObj };
            }
            catch (Exception e) { _taxLive = false; _fin = null; _taxChain = "blad: " + e.Message; Stumble("BkInChain", e); }
            try { _linesHooked = TownTaxHooked(out _linesNote); }
            catch (Exception e) { _linesHooked = false; _linesNote = "blad: " + e.Message; Stumble("TownTaxHooked", e); }
            string wsModel = "-";
            try { var wm = Campaign.Current.Models != null ? Campaign.Current.Models.WorkshopModel : null; wsModel = wm != null ? wm.GetType().Name : "brak"; } catch { }
            Log.Info("Szczelnosc (164a/31b): latki - " + _hookNote + " | podatek warsztatow BK: lancuch modelu " + _taxChain + ", podatek miast " + _linesNote + " -> " + (_taxLive && _linesHooked ? "CZYNNE (przy Population Rent Enabled i Rent Replaces Town Tax)" : "NIECZYNNE (linia 'Taxes from' zostaje, podatek znika jak dotad)")
                     + "; warsztaty lordow i gracza: czynny model warsztatow gry " + wsModel + " - BK pobiera od glow rodow 'Workshop taxes': " + (BkWorkshopModelActive() ? "TAK (linie glow rodow w miastach innych rodow zostaja, reszta zdjeta)" : "NIE (linie 'Taxes from' warsztatow nie-notabli zdjete z podatku miast - nikt ich nie placi)")
                     + "; D11-3 wlasciciel warsztatu (glowa rodu, gracz) w cudzym miescie placi ten podatek panu miasta (kwota BK z wyplaty rodu): " + OwnerTaxState()
                     + "; kwoty podatku warsztatow (GetWorkshopTaxes): " + FinSourceNote()
                     + " | wylaczniki: Caravan Wage To Purse " + Onoff(WageOn) + ", BK Workshop Tax To Lord " + Onoff(TaxOn) + ", Workshop Owner Pays Town Tax " + Onoff(OwnerTaxOn) + ", BK Lord Fees Have Recipients " + Onoff(FeesOn)
                     + ", Dead Hero Gold Stays " + Onoff(DeadOn) + ", Caravan Profit In Realm Town " + Onoff(ProfitOn) + " | E3/8d (Inni bez zoldu) - odlozone.");
        }

        private static string Onoff(bool b) { return b ? "WL" : "WYL"; }

        /// <summary>Jedna latka: brak metody albo blad Harmony = BRAK w linii startowej (ten kawalek nieczynny), reszta latek dalej.</summary>
        private static bool Patch(MethodBase m, string label, string pre, string post, string fin, int postPrio, int finPrio, List<string> wired, List<string> missing)
        {
            if (m == null) { missing.Add(label); return false; }
            try
            {
                _harmony.Patch(m,
                    prefix: pre != null ? new HarmonyMethod(typeof(Seal31B), pre) : null,
                    postfix: post != null ? new HarmonyMethod(typeof(Seal31B), post) { priority = postPrio } : null,
                    finalizer: fin != null ? new HarmonyMethod(typeof(Seal31B), fin) { priority = finPrio } : null);
                wired.Add(label);
                return true;
            }
            catch (Exception e) { missing.Add(label + " (blad: " + e.Message + ")"); Stumble("Patch(" + label + ")", e); return false; }
        }

        private static void HookAll(List<string> wired, List<string> missing)
        {
            // BK - typy i skladowe
            var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
            if (cfgT != null)
            {
                _pCfgInst = AccessTools.Property(cfgT, "Instance");
                _pCfgFin = AccessTools.Property(cfgT, "ClanFinanceModel");
                _pCfgPop = AccessTools.Property(cfgT, "PopulationManager");
                var pmT = _pCfgPop != null ? _pCfgPop.PropertyType : null;
                _mGetPop = pmT != null ? AccessTools.Method(pmT, "GetPopData", new[] { typeof(Settlement) }) : null;
                _pAutonomy = _mGetPop != null ? AccessTools.Property(_mGetPop.ReturnType, "Autonomy") : null;
            }
            var wsExtT = AccessTools.TypeByName("BannerKings.Extensions.WorkshopExtensions");
            _mTaxExp = wsExtT != null ? AccessTools.Method(wsExtT, "TaxExpenses", new[] { typeof(Workshop) }) : null;
            var finT = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKClanFinanceModel");
            _mGetWsTax = finT != null ? AccessTools.Method(finT, "GetWorkshopTaxes", new[] { typeof(Workshop) }) : null;
            _tBkWs = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKWorkshopModel");   // poprawka po recenzji: kto naprawde placi podatek warsztatu (UnpaidOwnerTaxLines)

            // E3/4: zold karawan notabli (gra)
            var mCar = AccessTools.Method(typeof(NotablesCampaignBehavior), "ManageCaravanExpensesOfNotable", new[] { typeof(Hero) });
            _wNotCar = Patch(mCar, "zold karawan notabli (NotablesCampaignBehavior.ManageCaravanExpensesOfNotable)", nameof(NotableCarPre), nameof(NotableCarPost), null, Priority.Normal, Priority.Normal, wired, missing);

            // E3/8a: podatek warsztatow BK (model BK) + doba notabla (gra)
            var mTax = finT != null ? AccessTools.Method(finT, "CalculateNotableDailyGoldChange", new[] { typeof(Hero), typeof(bool) }) : null;
            bool taxRefl = _mTaxExp != null && _mTaxExp.ReturnType == typeof(int) && _mGetWsTax != null && _pCfgInst != null && _pCfgFin != null;
            if (mTax != null && mTax.DeclaringType == finT && taxRefl)
            {
                _wTax = Patch(mTax, "podatek warsztatow BK (BKClanFinanceModel.CalculateNotableDailyGoldChange)", nameof(TaxPre), nameof(TaxPost), null, Priority.Last, Priority.Normal, wired, missing);
                var mTick = AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "DailyTickHero", new[] { typeof(Hero) });
                _wTick = Patch(mTick, "przelew podatku (ClanVariablesCampaignBehavior.DailyTickHero)", nameof(HeroTickPre), nameof(HeroTickPost), nameof(HeroTickFin), Priority.Normal, Priority.Low, wired, missing);
            }
            else missing.Add("podatek warsztatow BK (" + (mTax == null ? "brak BKClanFinanceModel.CalculateNotableDailyGoldChange" : "brak TaxExpenses / GetWorkshopTaxes / BannerKingsConfig") + ")");

            // E3/8b: oplaty lordow BK
            var tmT = AccessTools.TypeByName("BannerKings.Managers.TitleManager");
            var taT = AccessTools.TypeByName("BannerKings.Managers.Titles.TitleAction");
            _pTaker = taT != null ? AccessTools.Property(taT, "ActionTaker") : null;
            _pActGold = taT != null ? AccessTools.Property(taT, "Gold") : null;
            if (tmT != null && _pTaker != null && _pActGold != null && _pActGold.PropertyType == typeof(float))
            {
                foreach (var n in new[] { "AddOngoingClaim", "CreateTitle", "RevokeTitle" })
                {
                    var m = AccessTools.Method(tmT, n, new[] { taT });
                    if (Patch(m, "oplata BK (TitleManager." + n + ")", nameof(TitlePre), nameof(FeePost), nameof(FeeFin), Priority.Normal, Priority.Low, wired, missing)) _wFees++;
                }
                var mu = AccessTools.Method(tmT, "UsurpTitle", new[] { typeof(Hero), taT });
                if (Patch(mu, "oplata BK (TitleManager.UsurpTitle)", nameof(UsurpPre), nameof(FeePost), nameof(FeeFin), Priority.Normal, Priority.Low, wired, missing)) _wFees++;
            }
            else missing.Add("oplaty BK za tytuly (brak TitleManager / TitleAction.ActionTaker / Gold)");
            var dilT = AccessTools.TypeByName("BannerKings.Behaviours.Diplomacy.Dilemmas.BKDilemmaBehavior");
            var dT = AccessTools.TypeByName("BannerKings.Behaviours.Diplomacy.Dilemmas.Dilemma");
            _pDilKingdom = dT != null ? AccessTools.Property(dT, "Kingdom") : null;
            var mDil = dilT != null && dT != null && _pDilKingdom != null ? AccessTools.Method(dilT, "ApplyAiLevers") : null;
            if (Patch(mDil, "oplata BK (BKDilemmaBehavior.ApplyAiLevers)", nameof(DilemmaPre), nameof(FeePost), nameof(FeeFin), Priority.Normal, Priority.Low, wired, missing)) _wFees++;
            var grT = AccessTools.TypeByName("BannerKings.Managers.Court.Grace.CourtGrace");
            var mCourt = grT != null ? AccessTools.Method(grT, "AddExpense") : null;
            if (mCourt != null && (mCourt.GetParameters().Length < 1 || mCourt.GetParameters()[0].ParameterType != typeof(Clan))) mCourt = null;
            if (Patch(mCourt, "oplata BK (CourtGrace.AddExpense)", nameof(CourtPre), nameof(FeePost), nameof(FeeFin), Priority.Normal, Priority.Low, wired, missing)) _wFees++;
            var mvT = AccessTools.TypeByName("BannerKings.Managers.Goals.Decisions.MoveCourtDecision");
            _fMoveTown = mvT != null ? AccessTools.Field(mvT, "town") : null;
            _mFulfiller = mvT != null ? AccessTools.Method(mvT, "GetFulfiller", Type.EmptyTypes) : null;
            var mMove = mvT != null && _fMoveTown != null && _mFulfiller != null ? AccessTools.Method(mvT, "ApplyGoal", Type.EmptyTypes) : null;
            if (Patch(mMove, "oplata BK (MoveCourtDecision.ApplyGoal)", nameof(MovePre), nameof(FeePost), nameof(FeeFin), Priority.Normal, Priority.Low, wired, missing)) _wFees++;
            var knT = AccessTools.TypeByName("BannerKings.Behaviours.BKKnighthoodBehavior");
            var mKn = knT != null ? AccessTools.Method(knT, "CreateClan") : null;
            if (mKn != null)
            {
                var ps = mKn.GetParameters();
                if (ps.Length < 2 || ps[0].ParameterType != typeof(Hero) || ps[1].ParameterType != typeof(Clan)) mKn = null;
            }
            if (Patch(mKn, "oplata BK (BKKnighthoodBehavior.CreateClan)", nameof(KnightPre), nameof(FeePost), nameof(FeeFin), Priority.Normal, Priority.Low, wired, missing)) _wFees++;

            // E3/8c: zloto zmarlego (gra)
            var mDead = AccessTools.Method(typeof(KillCharacterAction), "MakeDead", new[] { typeof(Hero), typeof(bool) });
            _wDead = Patch(mDead, "zloto zmarlego (KillCharacterAction.MakeDead)", nameof(MakeDeadPre), null, null, Priority.Normal, Priority.Normal, wired, missing);

            // E3/9: zysk karawany pana (BK)
            var bpT = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
            var mProf = bpT != null ? AccessTools.Method(bpT, "AddRealisticIncome", new[] { typeof(MobileParty), typeof(Settlement) }) : null;
            var setT = AccessTools.TypeByName("BannerKings.Settings.BannerKingsSettings");
            _pBkSet = setT != null ? AccessTools.Property(setT, "Instance") : null;
            _pRealCar = setT != null ? AccessTools.Property(setT, "RealisticCaravanIncome") : null;
            if (_pBkSet == null || _pRealCar == null) mProf = null;
            _wProfit = Patch(mProf, "zysk karawany pana (BKPartyBehavior.AddRealisticIncome)", nameof(ProfitPre), nameof(ProfitPost), null, Priority.Normal, Priority.Normal, wired, missing);

            // 121 (recenzja uwaga 2): D11-3 - kwota podatku warsztatu z prawdziwej wyplaty rodu (gra), nie z ostatniego przeliczenia BK. Tylko odczyt;
            // Priority.First - zaraz po ciele gry, przed innymi postfiksami rozliczenia rodu (zaden z nich nie przelicza dochodu z warsztatow)
            var mClanTick = AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "DailyTickClan", new[] { typeof(Clan) });
            _wPay = Patch(mClanTick, "podatek warsztatow z wyplaty rodu (ClanVariablesCampaignBehavior.DailyTickClan)", null, nameof(ClanPayPost), null, Priority.First, Priority.Normal, wired, missing);
        }

        /// <summary>E3/8a: obie nogi naraz - przelew notabl -> pan (TaxPre) i zdjecie linii "Taxes from" z podatku miasta (NotableTaxLines, wolane
        /// z PopulationLaw.TownTaxPostfix tylko przy Population Rent Enabled i Rent Replaces Town Tax). Bez ktorejkolwiek - stan sprzed (para przez nicosc).</summary>
        private static bool TaxLive
        {
            get
            {
                var s = S;
                return s != null && s.BkWorkshopTaxToLord && _taxLive && _linesHooked && s.PopulationRentEnabled && s.RentReplacesTownTax;
            }
        }

        /// <summary>E3/8a: czy na CalculateTownTax czynnego modelu podatku osad (najbardziej pochodna deklaracja) siedzi PopulationLaw.TownTaxPostfix.</summary>
        private static bool TownTaxHooked(out string note)
        {
            note = "-";
            object model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SettlementTaxModel : null;
            if (model == null) { note = "brak modelu"; return false; }
            MethodInfo m = null;
            for (var t = model.GetType(); t != null && m == null; t = t.BaseType)
                m = t.GetMethod("CalculateTownTax", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, new[] { typeof(Town), typeof(bool) }, null);
            if (m == null) { note = model.GetType().Name + " bez CalculateTownTax"; return false; }
            var info = Harmony.GetPatchInfo(m);
            bool ok = false;
            if (info != null)
                foreach (var p in info.Postfixes)
                    if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(PopulationLaw) && p.PatchMethod.Name == "TownTaxPostfix") ok = true;
            note = m.DeclaringType.Name + (ok ? " z renta (PopulationLaw)" : " BEZ latki renty");
            return ok;
        }

        /// <summary>E3/8a: czy w lancuchu czynnego modelu finansow rodu jest BKClanFinanceModel z wlasnym CalculateNotableDailyGoldChange
        /// (BaseModel albo pole poprzedniego modelu, np. ROT _previousModel) - tylko wtedy BK odejmuje podatek, a my go oddajemy.</summary>
        private static bool BkInChain(out string chain, out object bkObj)
        {
            chain = "-"; bkObj = null;
            var bk = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKClanFinanceModel");
            object m = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.ClanFinanceModel : null;
            var names = new List<string>();
            bool found = false;
            for (int i = 0; i < 10 && m != null; i++)
            {
                var t = m.GetType();
                names.Add(t.Name);
                if (bk != null && bk.IsInstanceOfType(m))
                {
                    bkObj = m;   // 121: ten egzemplarz gra wola (CalculateOwnerIncomeFromWorkshop) - tylko jego workshopTaxes sa wypelniane
                    var mm = t.GetMethod("CalculateNotableDailyGoldChange", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Hero), typeof(bool) }, null);
                    found = mm != null && mm.DeclaringType == bk;
                    break;
                }
                object next = null;
                try { var bp = AccessTools.Property(t, "BaseModel"); next = bp != null ? bp.GetValue(m, null) : null; } catch { next = null; }
                if (next == null || ReferenceEquals(next, m))
                {
                    next = null;
                    for (var tt = t; tt != null && next == null; tt = tt.BaseType)
                        foreach (var f in tt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            if (!typeof(ClanFinanceModel).IsAssignableFrom(f.FieldType)) continue;
                            var v = f.GetValue(m);
                            if (v != null && !ReferenceEquals(v, m)) { next = v; break; }
                        }
                }
                m = next;
            }
            chain = names.Count > 0 ? string.Join(" -> ", names.ToArray()) : "brak";
            return found;
        }

        // ================================================================= E3/4: zold karawan do sakiewek ludzi karawan
        internal sealed class CarState { public Hero H; public long G0, C0; public readonly List<MobileParty> P = new List<MobileParty>(); public readonly List<int> FromCar = new List<int>(), FromNot = new List<int>(); }

        /// <summary>Prefiks ManageCaravanExpensesOfNotable: ta sama droga gry (od konca listy) - ile zoldu zejdzie z kasy kazdej karawany, ile z kiesy notabla,
        /// z doplata do 5 000 (przelew notabl -> karawana, ktory zmniejsza kiese notabla dla nastepnych karawan).</summary>
        public static void NotableCarPre(Hero __0, out CarState __state)
        {
            __state = null;
            try
            {
                if (!WageOn || !MenPurse.On || !Live || __0 == null) return;
                var cs = __0.OwnedCaravans;
                if (cs == null || cs.Count == 0) return;
                var st = new CarState { H = __0, G0 = __0.Gold };
                int g = __0.Gold;
                for (int i = cs.Count - 1; i >= 0; i--)
                {
                    var mp = cs[i] != null ? cs[i].MobileParty : null;
                    if (mp == null) continue;
                    int purse = mp.PartyTradeGold, wage = Math.Max(0, mp.TotalWage), fc = 0, fn = 0;
                    if (purse >= wage) { fc = wage; purse -= wage; }
                    else { fn = Math.Min(wage, g); g -= fn; }
                    if (purse < 5000) { int top = Math.Min(5000 - purse, g); purse += top; g -= top; }
                    st.P.Add(mp); st.FromCar.Add(fc); st.FromNot.Add(fn);
                    st.C0 += mp.PartyTradeGold;
                }
                __state = st;
            }
            catch (Exception e) { __state = null; Stumble("NotableCarPre", e); }
        }

        /// <summary>Postfiks: migawka (kiesa notabla + kasy jego karawan) = ile naprawde zniknelo; tyle (najwyzej wyliczone) do sakiewek ludzi karawan,
        /// a z pozycji ksiegi "zold karawan notabli" (okno O02 - jego finalizer biegnie po nas) odejmujemy oddane.</summary>
        public static void NotableCarPost(Hero __0, CarState __state)
        {
            if (__state == null || __0 == null || !ReferenceEquals(__0, __state.H)) return;
            try
            {
                long c1 = 0;
                for (int i = 0; i < __state.P.Count; i++) c1 += __state.P[i].PartyTradeGold;
                long vanish = (__state.G0 + __state.C0) - ((long)__0.Gold + c1);
                long sim = 0;
                for (int i = 0; i < __state.P.Count; i++) sim += __state.FromCar[i] + __state.FromNot[i];
                if (sim != vanish) _notMismatch++;
                long left = Math.Max(0L, Math.Min(sim, vanish));
                long sumCar = 0, sumNot = 0;
                for (int i = 0; i < __state.P.Count && left > 0; i++)
                {
                    var mp = __state.P[i];
                    int due = __state.FromCar[i] + __state.FromNot[i];
                    int give = (int)Math.Min(due, left);
                    if (give <= 0) continue;
                    left -= give;
                    if (!mp.IsActive) { _notSkipped += give; continue; }   // kasa poza posiadaczami swiata - jak dotad w nicosc (okno O02 tez jej nie liczy)
                    int fc = Math.Min(__state.FromCar[i], give), fn = give - fc;
                    MenPurse.NoteWage(give);                            // licznik linii "Sakiewka ludzi:" (zold wplacony do sakiewek)
                    MenPurse.Add(mp, give);
                    sumCar += fc; sumNot += fn;
                }
                if (sumCar + sumNot > 0)
                {
                    _notWage += sumCar + sumNot; _notFromCar += sumCar; _notFromNot += sumNot; _notN++;
                    // ksiega: zold, ktory gra skasowala (okno O02 mierzy go jako ujscie), wrocil do posiadacza (sakiewki) - ta sama pozycja minus oddane
                    if (CW.On && CW.Wired[CW.WCaravanWage])
                    {
                        CW.AddWorld(CW.UCaravanFromCaravan, -sumCar, -1);
                        CW.AddWorld(CW.UCaravanFromNotable, -sumNot, -1);
                    }
                }
            }
            catch (Exception e) { Stumble("NotableCarPost", e); }
        }

        /// <summary>SoldierPay.Route: zaplacony zold karawany lorda -> sakiewka ludzi tej karawany (jak partie lordow). false = jak dotad ("inne partie").</summary>
        internal static bool CaravanWage(MobileParty mp, int amt)
        {
            try
            {
                if (!WageOn || !MenPurse.On || mp == null || !mp.IsCaravan || amt <= 0) return false;
                if (Undead.Party(mp)) return false;                 // trup zoldu nie wyda
                MenPurse.NoteWage(amt);
                MenPurse.Add(mp, amt);
                MoneyLedger.NoteWageRouted(true, amt);
                _lordCarWage += amt; _lordCarN++;
                return true;
            }
            catch (Exception e) { Stumble("CaravanWage", e); return false; }
        }

        /// <summary>Wstawka do linii "Zold:" (SoldierPay.Daily).</summary>
        internal static string ZoldNote()
        {
            return "; karawany lordow do sakiewek ludzi karawan (164a/31b) " + _lordCarWage + " (" + _lordCarN + ")" + (WageOn ? "" : " - WYL");
        }

        /// <summary>MenPurse.OnLeft: karawana wydaje cala sakiewke w miescie przy wyjezdzie - bez oszczednosci, bez odziezy, bez tarczy zoldu. false = nie karawana.</summary>
        internal static bool CaravanSpends(MobileParty mp, Settlement st, int purse)
        {
            try
            {
                if (!WageOn || mp == null || !mp.IsCaravan || st == null || st.Town == null || purse <= 0) return false;   // poprawka po recenzji: wylaczony E3/4 = droga partii jak dotad
                int t = MenPurse.Take(mp, purse);
                if (t <= 0) return true;
                int folk = TownFolk.EarnService(st, t, TownFolk.KLifeCaravan);   // E5/B1 (R9, 75/25): uslugi do kiesy ludu (arytmetyka - nie rzuca)
                st.Town.ChangeGold(t - folk);                          // karczma, jedzenie, gra - pieniadze zostaja w miescie
                ClanIncomeBook.NoteOwnPaid(st, mp.ActualClan, t);      // 169c: "wlasne" D stalego (tylko licznik) - jak sakiewki partii
                MoneyLedger.Note(MoneyLedger.NLife, st, t - folk);     // ksiega przeplywow osad (tylko licznik) - czesc w kasie
                try { TownFolk.Note(st, t, TownFolk.KLifeCaravan); } catch { }   // E5/0 (195): zycie ludzi karawan w miescie (tylko licznik)
                _carSpent += t; _carSpentN++;
                return true;
            }
            catch (Exception e) { Stumble("CaravanSpends", e); return false; }
        }

        /// <summary>MenPurse.OnPartyDestroyed (sakiewka juz zdjeta): W13 - zwyciezca bez bohatera, ale handlujacy (banda) bierze do kiesy; inaczej kasa osady
        /// niszczyciela. true = obsluzone; false = jak dotad (zwyciezca z bohaterem 1/3 - 2/3, inaczej najblizsze miasto).</summary>
        internal static bool PurseOfBeaten(MobileParty mp, PartyBase destroyer, int purse)
        {
            try
            {
                if (!WageOn || mp == null || purse <= 0) return false;
                bool car = mp.IsCaravan;
                if (car) _carPurseN++;
                if (car && destroyer != null && BeatenPurse.CaravanNotBeatenNow(mp))
                {
                    // poprawka po recenzji (W13 = regula 3.2): karawana niepokonana w bitwie przez przywodce zwyciezcow - jak R10 (sakiewka -> najblizsze miasto)
                    var nt = VillageTakings.NearestTown(mp);
                    if (nt != null && nt.Town != null)
                    {
                        nt.Town.ChangeGold(purse);
                        MoneyLedger.NotePurseGone(purse, true);
                        MoneyLedger.Note169(MoneyLedger.N169PurseGone, nt, purse);
                        _carPurseOdd += purse; _carPurseOddN++;
                    }
                    else _carPurseOddLost += purse;
                    return true;
                }
                var win = destroyer != null ? destroyer.MobileParty : null;
                if (win != null && win.LeaderHero != null && win.LeaderHero.IsAlive) { if (car) _carPurseHero += purse; return false; }
                if (win != null && !ReferenceEquals(win, mp) && win.IsActive && win.LeaderHero == null && win.IsPartyTradeActive && !Undead.Party(win))
                {
                    win.PartyTradeGold += purse;                        // banda (albo inna partia z kiesa handlowa) bierze sakiewke - jak kiese w 3.2
                    MoneyLedger.NotePurseGone(purse, false);
                    bool band = OutlawLaw.IsOutlawParty(win);
                    if (band) { _allPurseBand += purse; if (car) _carPurseBand += purse; } else { _allPurseParty += purse; if (car) _carPurseParty += purse; }
                    return true;
                }
                var own = SettlementOf(destroyer);
                if (own != null && own.SettlementComponent != null)
                {
                    own.SettlementComponent.ChangeGold(purse);         // milicja, zaloga, patrol - kasa ich osady
                    MoneyLedger.NotePurseGone(purse, true);
                    MoneyLedger.Note169(MoneyLedger.N169PurseGone, own, purse);
                    _allPurseSettl += purse; if (car) _carPurseSettl += purse;
                    return true;
                }
                if (car) _carPurseTown += purse;
                return false;
            }
            catch (Exception e) { Stumble("PurseOfBeaten", e); return false; }
        }

        /// <summary>Osada zwyciezcy, ktory nie nosi kiesy: osada-niszczyciel albo osada milicji, zalogi lub patrolu (ta sama regula co 3.2 BeatenPurse).</summary>
        private static Settlement SettlementOf(PartyBase destroyer)
        {
            Settlement st = null;
            if (destroyer == null) return null;
            if (destroyer.IsSettlement) st = destroyer.Settlement;
            else
            {
                var mp = destroyer.MobileParty;
                if (mp != null && (mp.IsMilitia || mp.IsGarrison || mp.IsPatrolParty)) st = mp.CurrentSettlement ?? mp.HomeSettlement;
            }
            return st != null && (st.IsTown || st.IsCastle || st.IsVillage) ? st : null;
        }

        // ================================================================= E3/8a: podatek warsztatow BK - prawdziwy platnik
        internal sealed class TaxState { public Hero H; public long Sum, Orig, Z; public readonly List<Workshop> W = new List<Workshop>(); public readonly List<int> T = new List<int>(); }
        private static Hero _tickHero;                     // doba notabla w toku (ClanVariablesCampaignBehavior.DailyTickHero)
        private static TaxState _pend;                     // podatek tej doby do przelania po wyplacie gry

        public static void HeroTickPre(Hero __0) { _tickHero = __0; _pend = null; }

        /// <summary>Prefiks BK (tylko prawdziwa wyplata w dobie notabla): podatek kazdego warsztatu ta sama funkcja BK (TaxExpenses) - tyle BK odejmie.</summary>
        public static void TaxPre(Hero __0, bool __1, out TaxState __state)
        {
            __state = null;
            try
            {
                if (!__1 || __0 == null || !TaxLive || !__0.IsNotable || !ReferenceEquals(__0, _tickHero)) return;
                if (_pend != null && ReferenceEquals(_pend.H, __0)) return;   // drugie wywolanie w tej samej dobie notabla - podatek oddany juz raz
                var ws = __0.OwnedWorkshops;
                if (ws == null || ws.Count == 0) return;
                var st = new TaxState { H = __0 };
                for (int i = 0; i < ws.Count; i++)
                {
                    var w = ws[i];
                    if (w == null) continue;
                    int t = (int)_mTaxExp.Invoke(null, new object[] { w });
                    st.W.Add(w); st.T.Add(t); st.Sum += t;
                }
                if (st.Sum > 0) __state = st;
            }
            catch (Exception e) { __state = null; Stumble("TaxPre", e); }
        }

        /// <summary>Postfiks BK: podatek wraca do wyplaty (gra wyplaci brutto, ktore zdjela z warsztatow); to, co zniknelo, przelewamy po wyplacie (HeroTickPost).</summary>
        public static void TaxPost(Hero __0, ref int __result, TaxState __state)
        {
            if (__state == null) return;
            try
            {
                long orig = __result;
                long z = Math.Min(__state.Sum, Math.Max(0L, orig + __state.Sum));   // to, co naprawde zniklo (przy wyniku <= 0 gra nic nie wyplaca)
                if (z <= 0) return;
                __state.Orig = orig; __state.Z = z;
                // poprawka po recenzji: gra wyplaca brutto (= wynik BK + caly podatek), a HeroTickPost zdejmuje Z. Dotad orig + z: przy 0 < brutto < podatek
                // (orig < 0) wychodzilo 2 x brutto - podatek, a przelew Z = brutto - notabl doplacal roznice z oszczednosci. Przy orig >= 0 bez zmian (z = podatek).
                __result = (int)Math.Min(int.MaxValue, orig + __state.Sum);
                _pend = __state;
                if (orig < 0) _taxNeg++;
            }
            catch (Exception e) { _pend = null; Stumble("TaxPost", e); }
        }

        /// <summary>Postfiks DailyTickHero (po wyplacie gry): notabl -> pan miasta warsztatu (czesc autonomii -> kasa miasta).</summary>
        public static void HeroTickPost(Hero __0)
        {
            var p = _pend;
            _pend = null;
            if (p == null || __0 == null || !ReferenceEquals(p.H, __0)) return;
            try
            {
                _taxNotables++; _taxSeen += p.Sum; _taxZ += p.Z;
                long given = 0;
                int last = -1;
                for (int i = 0; i < p.W.Count; i++) if (p.T[i] > 0) last = i;
                for (int i = 0; i < p.W.Count; i++)
                {
                    if (p.T[i] <= 0) continue;
                    try
                    {
                        long zw = i == last ? p.Z - given : p.Z * p.T[i] / p.Sum;
                        given += zw;
                        if (zw <= 0) continue;
                        var st = p.W[i].Settlement;
                        if (st == null || st.SettlementComponent == null) { _taxShort += zw; continue; }
                        var lord = st.OwnerClan != null ? st.OwnerClan.Leader : null;
                        bool lordOk = lord != null && lord.IsAlive && !CW.OutOfWorld(lord) && !ReferenceEquals(lord, __0);
                        long toTown = lordOk ? (long)Math.Round(zw * Math.Min(1.0, 0.6 * Autonomy(st))) : zw;
                        long toLord = zw - toTown;
                        if (toTown > 0)
                        {
                            int before = __0.Gold;
                            GiveGoldAction.ApplyForCharacterToSettlement(__0, st, (int)toTown, true);   // zdarzenie gry: notabl -> kasa miasta
                            long moved = before - __0.Gold;
                            if (lordOk) _taxToTowns += moved; else _taxNoLord += moved;
                            if (moved < toTown) _taxShort += toTown - moved;
                        }
                        if (toLord > 0)
                        {
                            int got = (int)Math.Min(toLord, Math.Max(0, __0.Gold));
                            if (got > 0)
                            {
                                // bez zdarzenia gry: wplata liczy sie jak renta miasta (RentMerge), nie jak wplyw jednorazowy
                                __0.ChangeHeroGold(-got);
                                lord.ChangeHeroGold(got);
                                var c = lord.Clan;
                                if (c != null) { int r0; _rentPend.TryGetValue(c, out r0); _rentPend[c] = r0 + got; }
                                _taxToLords += got;
                            }
                            if (got < toLord) _taxShort += toLord - got;
                        }
                    }
                    catch (Exception e) { Stumble("HeroTickPost(warsztat)", e); }
                }
            }
            catch (Exception e) { Stumble("HeroTickPost", e); }
        }

        public static void HeroTickFin(Hero __0) { _tickHero = null; _pend = null; }

        /// <summary>perf-d2d3: Autonomy przez delegaty (te same getter i GetPopData, ten sam Convert.ToSingle i przyciecie); delegat niedostepny - odbicie.</summary>
        private static float AutonomyFast(Settlement st)
        {
            try
            {
                var cfg = BkCfgFast();
                object pm = null;
                if (cfg != null && _pCfgPop != null) { var g = PerfFast.Getter<object>(_pCfgPop); pm = g != null ? g(cfg) : _pCfgPop.GetValue(cfg, null); }
                object pd = null;
                if (pm != null && _mGetPop != null) { var gp = PerfFast.M1<Settlement, object>(_mGetPop); pd = gp != null ? gp(pm, st) : _mGetPop.Invoke(pm, new object[] { st }); }
                if (pd == null || _pAutonomy == null) return 0f;
                var ga = PerfFast.Getter<float>(_pAutonomy);
                return Math.Max(0f, Math.Min(1f, ga != null ? ga(pd) : Convert.ToSingle(_pAutonomy.GetValue(pd, null))));
            }
            catch { return 0f; }
        }

        /// <summary>perf-d2d3: BannerKingsConfig.Instance przez delegat (odbicie, gdy delegat niedostepny).</summary>
        private static object BkCfgFast()
        {
            if (_pCfgInst == null) return null;
            var g = PerfFast.StaticGetter(_pCfgInst);
            return g != null ? g() : _pCfgInst.GetValue(null, null);
        }

        /// <summary>perf-d2d3: model finansow BK z konfiguracji (BannerKingsConfig.ClanFinanceModel) - przez delegat albo odbicie.</summary>
        private static object FinOf(object cfg, bool fast)
        {
            if (cfg == null) return null;
            if (!fast) return _pCfgFin.GetValue(cfg, null);
            var g = PerfFast.Getter<object>(_pCfgFin);
            return g != null ? g(cfg) : _pCfgFin.GetValue(cfg, null);
        }

        // ------------------------------------------------------------ 121 (noc-p8): zrodlo kwot podatku warsztatow BK
        // BKROTPatch (OnGameStart) podmienia BannerKingsConfig.ClanFinanceModel na nowy BKROTClanFinanceModel PO tym, jak BK (Main.cs:207) dodal do gry swoj
        // egzemplarz - w lancuchu gry siedzi egzemplarz BK, a konfiguracja wskazuje drugi, ktorego CalculateOwnerIncomeFromWorkshop nikt nie wola (gra wola
        // Campaign.Current.Models.ClanFinanceModel). Slownik workshopTaxes egzemplarza z konfiguracji byl wiec zawsze pusty (D11-3: 0 warsztatow od 24.10).
        // Kwoty czytamy z egzemplarza w lancuchu (ten sam, ktory BK wypelnia przy wyplacie z warsztatu); zapas - konfiguracja BK (jak dotad).
        private sealed class FinRef { public object Head, Bk; }
        private static volatile FinRef _fin;               // egzemplarz BK z lancucha + glowa lancucha, przy ktorej go znaleziono (EnsureHooks, co sesje)

        /// <summary>Egzemplarz BKClanFinanceModel z czynnego lancucha modelu finansow gry, gdy glowa lancucha ta sama co przy odczycie; inaczej null.</summary>
        private static object ChainFin()
        {
            var f = _fin;
            if (f == null) return null;
            var c = Campaign.Current;
            var ms = c != null ? c.Models : null;
            return ms != null && ReferenceEquals(ms.ClanFinanceModel, f.Head) ? f.Bk : null;
        }

        /// <summary>Model, z ktorego czytamy GetWorkshopTaxes: egzemplarz BK z lancucha gry, zapas - BannerKingsConfig.ClanFinanceModel (jak dotad).</summary>
        private static object TaxFin(object cfg, bool fast)
        {
            var b = ChainFin();
            if (b != null) return b;
            return _pCfgFin != null ? FinOf(cfg, fast) : null;
        }

        /// <summary>121: model kwot podatku warsztatow dla PopulationLaw.KeptTownLines (to samo zrodlo co NotableTaxLines / UnpaidOwnerTaxLines / D11-3 -
        /// linie warsztatow w podatku miasta przy rencie znosza sie do zera tylko przy jednym zrodle). null - BK niedostepny (KeptTownLines bierze swoj zapas).</summary>
        internal static object WsTaxModel()
        {
            try { var b = ChainFin(); if (b != null) return b; return _pCfgFin != null ? FinOf(BkCfgFast(), true) : null; }
            catch { return null; }
        }

        private static string FinSourceNote()
        {
            if (ChainFin() != null) return "egzemplarz BK z lancucha modelu gry (ten, ktory BK wypelnia przy wyplacie z warsztatu)";
            return "ZAPAS BannerKingsConfig.ClanFinanceModel tylko dla linii podatku miast (egzemplarza BK w lancuchu nie znaleziono - kwoty moga byc 0; D11-3 i podatek w cenie warsztatu lorda / gracza nieczynne)";
        }

        /// <summary>perf-d2d3: podatek BK warsztatu (GetWorkshopTaxes) jako float - delegat albo odbicie (Convert.ToSingle, jak dotad).</summary>
        private static float WsTax(object fin, Workshop w, Func<object, Workshop, int> tax)
        {
            return tax != null ? (float)tax(fin, w) : Convert.ToSingle(_mGetWsTax.Invoke(fin, new object[] { w }));
        }

        /// <summary>Autonomia BK osady (0..1); brak danych - 0.</summary>
        private static float Autonomy(Settlement st)
        {
            try
            {
                var cfg = BkCfg();
                var pm = cfg != null && _pCfgPop != null ? _pCfgPop.GetValue(cfg, null) : null;
                var pd = pm != null && _mGetPop != null ? _mGetPop.Invoke(pm, new object[] { st }) : null;
                return pd != null && _pAutonomy != null ? Math.Max(0f, Math.Min(1f, Convert.ToSingle(_pAutonomy.GetValue(pd, null)))) : 0f;
            }
            catch { return 0f; }
        }

        /// <summary>PopulationLaw.TownTaxPostfix: linie "Taxes from" warsztatow notabli x autonomia (to samo, co KeptTownLines dla nich liczy) - odejmowane
        /// od podatku miasta, gdy podatek ida przelewem. 0 - przelew nieczynny (linia zostaje jak dotad). Moze byc wolane z watkow AI.</summary>
        internal static float NotableTaxLines(Town town)
        {
            try
            {
                if (!TaxLive || town == null || town.Workshops == null) return 0f;
                // perf-d2d3 (PerfTownTaxFastHooks): rdzen przez delegaty; co 64. wywolanie tez stara droga - rozjazd: wynik starej drogi (licznik w linii "Szybkosc doby")
                float sum;
                if (PerfD2.FastHooksOn)
                {
                    sum = NotableCore(town, true);
                    if (PerfD2.FastProbe(PerfD2.FNotable)) { float s = NotableCore(town, false); if (!PerfD2.FastSame(PerfD2.FNotable, s == sum)) sum = s; }
                }
                else sum = NotableCore(town, false);
                if (sum <= 0f) return 0f;
                System.Threading.Interlocked.Add(ref _taxLinesCut, (long)sum);
                return sum;
            }
            catch (Exception e) { Stumble("NotableTaxLines", e); return 0f; }
        }

        /// <summary>NotableTaxLines bez licznika (fast = false: cialo sprzed perf-d2d3 - odbicie). 0 - brak linii.</summary>
        private static float NotableCore(Town town, bool fast)
        {
            var cfg = fast ? BkCfgFast() : BkCfg();
            var fin = TaxFin(cfg, fast);   // 121: egzemplarz BK z lancucha gry (zapas - konfiguracja BK)
            if (fin == null) return 0f;
            var tax = fast ? PerfFast.M1<Workshop, int>(_mGetWsTax) : null;
            var leader = town.OwnerClan != null ? town.OwnerClan.Leader : null;
            float sum = 0f;
            foreach (var w in town.Workshops)
                if (w != null && w.Owner != null && w.Owner != leader && w.Owner.IsNotable)
                    sum += WsTax(fin, w, tax);
            if (sum <= 0f) return 0f;
            float aut = fast ? AutonomyFast(town.Settlement) : Autonomy(town.Settlement);
            if (aut > 0f) sum *= Math.Max(0f, 1f - 0.6f * aut);
            return sum;
        }

        /// <summary>
        /// E3/8a (poprawka po recenzji 3.1+3.3, R7 - jedna regula: linia tylko, gdy ktos naprawde placi): linie "Taxes from" warsztatow, ktorych wlasciciel
        /// (nie notabl - te ida przelewem, NotableTaxLines) NIE placi podatku BK x autonomia (to samo, co KeptTownLines dla nich liczy) - odejmowane od podatku
        /// miasta bez przelewu (nie ma czego przelac - dotad pan dostawal je z niczego). BK (BKClanFinanceModel.AddExpenses) pobiera "Workshop taxes" tylko
        /// glowie rodu, za warsztaty w miastach innych rodow i tylko gdy czynnym modelem warsztatow gry jest BKWorkshopModel (BK go nie rejestruje, BKROTPatch
        /// ustawia tylko BannerKingsConfig.WorkshopModel - u Jeffa czynny NavalDLCWorkshopModel, por. WorkshopTrade.TaxShare); BK CalculateOwnerIncomeFromWorkshop
        /// niczego nie odejmuje. Gracz i lordowie - ta sama regula. Wylacznik BkWorkshopTaxToLord (wylaczony = 0 - linia jak dotad); niezalezne od TaxLive
        /// (lancuch BK notabli). Moze byc wolane z watkow AI.
        /// </summary>
        internal static float UnpaidOwnerTaxLines(Town town) { float paid; return UnpaidOwnerTaxLines(town, out paid); }

        /// <summary>To samo; `ownerPaid` - z tych linii czesc, ktora od D11-3 placi wlasciciel panu przelewem (OwnerTaxDaily; x autonomia jak linia) - do podstawy
        /// pensji rady (CouncilWages35.TownBase), tak jak linie notabli placone przelewem E3/8a. 0 przy D11-3 nieczynnym.</summary>
        internal static float UnpaidOwnerTaxLines(Town town, out float ownerPaid)
        {
            ownerPaid = 0f;
            try
            {
                if (!TaxOn || town == null || town.Workshops == null || _mGetWsTax == null || _pCfgFin == null) return 0f;
                // perf-d2d3 (PerfTownTaxFastHooks): rdzen przez delegaty; co 64. wywolanie tez stara droga - rozjazd: wynik starej drogi (licznik w linii "Szybkosc doby")
                float paidSum; int n;
                float sum;
                if (PerfD2.FastHooksOn)
                {
                    sum = UnpaidCore(town, true, out paidSum, out n);
                    if (PerfD2.FastProbe(PerfD2.FUnpaid))
                    {
                        float p2; int n2;
                        float s2 = UnpaidCore(town, false, out p2, out n2);
                        if (!PerfD2.FastSame(PerfD2.FUnpaid, s2 == sum && p2 == paidSum && n2 == n)) { sum = s2; paidSum = p2; n = n2; }
                    }
                }
                else sum = UnpaidCore(town, false, out paidSum, out n);
                if (sum <= 0f) return 0f;
                ownerPaid = paidSum;
                System.Threading.Interlocked.Add(ref _unpaidCut, (long)sum);
                System.Threading.Interlocked.Add(ref _unpaidN, n);
                return sum;
            }
            catch (Exception e) { Stumble("UnpaidOwnerTaxLines", e); return 0f; }
        }

        /// <summary>UnpaidOwnerTaxLines bez licznikow (fast = false: cialo sprzed perf-d2d3 - odbicie). 0 (paid 0) - brak linii.</summary>
        private static float UnpaidCore(Town town, bool fast, out float paid, out int n)
        {
            paid = 0f; n = 0;
            var cfg = fast ? BkCfgFast() : BkCfg();
            var fin = TaxFin(cfg, fast);   // 121: egzemplarz BK z lancucha gry (zapas - konfiguracja BK)
            if (fin == null || !_mGetWsTax.DeclaringType.IsInstanceOfType(fin)) return 0f;
            var tax = fast ? PerfFast.M1<Workshop, int>(_mGetWsTax) : null;
            var townClan = town.OwnerClan;
            var leader = townClan != null ? townClan.Leader : null;
            bool bkCharges = BkWorkshopModelActive();
            bool d113 = OwnerTaxLive;
            float sum = 0f, paidSum = 0f;
            foreach (var w in town.Workshops)
            {
                var o = w != null ? w.Owner : null;
                if (o == null || o == leader || o.IsNotable) continue;                          // pan miasta - BK nie dopisuje linii; notabl - NotableTaxLines
                var oc = o.Clan;
                if (bkCharges && oc != null && oc.Leader == o && townClan != oc) continue;     // ten wlasciciel naprawde placi BK (wydatek "Workshop taxes")
                float t = WsTax(fin, w, tax);
                if (t <= 0f) continue;
                sum += t; n++;
                if (d113 && OwnerPays(o, townClan, leader, bkCharges)) paidSum += t;            // D11-3: placi wlasciciel przelewem (bez czlonkow rodu pana)
            }
            if (sum <= 0f) { n = 0; return 0f; }
            float aut = fast ? AutonomyFast(town.Settlement) : Autonomy(town.Settlement);
            if (aut > 0f) { float f = Math.Max(0f, 1f - 0.6f * aut); sum *= f; paidSum *= f; }
            paid = paidSum;
            return sum;
        }

        private static Type _tBkWs;
        /// <summary>Czy czynnym modelem warsztatow gry jest BKWorkshopModel (tylko wtedy BK pobiera od glow rodow "Workshop taxes").</summary>
        internal static bool BkWorkshopModelActive()   // 259: takze linia majatku gracza (CaravanSettle)
        {
            var m = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.WorkshopModel : null;
            return _tBkWs != null && m != null && _tBkWs.IsInstanceOfType(m);
        }

        // ================================================================= D11-3: podatek warsztatow lordow i gracza w cudzych miastach placi wlasciciel
        /// <summary>D11-3 czynne: wylacznik, E3/8 i druga noga - linia 'Taxes from' naprawde zdjeta z podatku miasta (UnpaidOwnerTaxLines w PopulationLaw.TownTaxPostfix,
        /// czynne tylko przy rentach miast i latce renty na czynnym modelu podatku). Bez ktorejkolwiek - linia zostaje, przelew bylby drugi raz.
        /// 121 (recenzja uwaga 1): tez egzemplarz BK w czynnym lancuchu modelu finansow (ChainFin) i migawka wyplaty (postfiks DailyTickClan) - bez nich
        /// kwot podatku nie ma skad wziac (slownik z konfiguracji BK pusty), wiec D11-3 nieczynne, a cena warsztatu (TaxShare przez OwnerPaysTax) podatku nie wlicza.</summary>
        private static bool OwnerTaxLive
        {
            get
            {
                var s = S;
                return s != null && s.WorkshopOwnerPaysTownTax && s.BkWorkshopTaxToLord && _linesHooked && s.PopulationRentEnabled && s.RentReplacesTownTax
                       && _mGetWsTax != null && _pCfgFin != null && _wPay && ChainFin() != null;
            }
        }

        /// <summary>121: stan D11-3 slowami (linia startu i doby) - przy nieczynnym powod.</summary>
        private static string OwnerTaxState()
        {
            if (OwnerTaxLive) return "CZYNNE";
            var s = S;
            if (s == null || !s.WorkshopOwnerPaysTownTax) return "wylaczone";
            if (!(s.BkWorkshopTaxToLord && _linesHooked && s.PopulationRentEnabled && s.RentReplacesTownTax && _mGetWsTax != null && _pCfgFin != null))
                return "NIECZYNNE - wymaga BK Workshop Tax To Lord, rent miast (Population Rent Enabled, Rent Replaces Town Tax) i latki renty na podatku miast";
            if (!_wPay) return "NIECZYNNE - brak latki DailyTickClan (migawka wyplaty rodu; cena warsztatu bez podatku)";
            return "NIECZYNNE - brak egzemplarza BK w lancuchu modelu finansow gry (cena warsztatu bez podatku)";
        }

        /// <summary>D11-3: czy wlasciciel `o` (nie notabl, nie pan miasta) placi podatek warsztatu w miescie rodu `townClan` - ten sam zbior co UnpaidOwnerTaxLines
        /// bez warsztatow czlonkow rodu pana (nie cudze miasto). bkCharges - BK sam pobiera od glowy obcego rodu (wtedy nie my).</summary>
        private static bool OwnerPays(Hero o, Clan townClan, Hero leader, bool bkCharges)
        {
            if (o == null || o == leader || o.IsNotable) return false;
            var oc = o.Clan;
            // 121: tylko glowa rodu (gracz - glowa swojego) - kwota BK powstaje przy wyplacie z warsztatu, a gra wyplaca tylko glowie (DefaultClanFinanceModel
            // CalculateHeroIncomeFromWorkshops(clan.Leader)); warsztat innego czlonka rodu ma w slowniku BK kwote stara (poprzedniego wlasciciela) albo zadna
            if (oc == null || oc.Leader != o) return false;
            if (oc != null && townClan != null && oc == townClan) return false;                  // czlonek rodu pana - nie cudze miasto
            if (bkCharges && oc != null && oc.Leader == o && townClan != oc) return false;       // BK pobiera 'Workshop taxes'
            return true;
        }

        /// <summary>D11-3 dla ceny warsztatu (WorkshopTrade.TaxShare): czy kupujacy `payer` (nie notabl) bedzie placil podatek BK warsztatu w osadzie `st`.</summary>
        internal static bool OwnerPaysTax(Settlement st, Hero payer)
        {
            try
            {
                if (!OwnerTaxLive || st == null || payer == null || payer.IsNotable) return false;
                var tc = st.OwnerClan;
                return OwnerPays(payer, tc, tc != null ? tc.Leader : null, BkWorkshopModelActive());
            }
            catch (Exception e) { Stumble("OwnerPaysTax", e); return false; }
        }

        /// <summary>
        /// 121 (recenzja uwaga 2), postfiks ClanVariablesCampaignBehavior.DailyTickClan (Priority.First - zaraz po ciele gry, tylko odczyt): gra wlasnie
        /// wyplacila glowie rodu dochod z warsztatow (DefaultClanFinanceModel.CalculateHeroIncomeFromWorkshops(clan.Leader), applyWithdrawals), a BK
        /// (BKClanFinanceModel.CalculateOwnerIncomeFromWorkshop) zapisal przy tym workshopTaxes[w] = TaxExpenses od zysku PRZED wyplata - stawka x zysk,
        /// czyli 2 x stawka x wyplata (wyplata = polowa zysku). Tyle samo pobralby BK przy BKWorkshopModel (AddExpenses w tym samym rozliczeniu) i tyle
        /// zaklada cena warsztatu (WorkshopTrade.TaxShare = 2 x stawka). Zapamietujemy (warsztat, wlasciciel, kwota, wyplata) - OwnerTaxDaily pobiera
        /// dokladnie te kwote, raz na wyplate. Slownika nie czytamy w dobie: kazde pozniejsze przeliczenie (podpowiedzi, AI, KingdomTreasury, IronBank)
        /// nadpisuje go stanem po wyplacie (1-2 x stawka), a po zmianie wlasciciela trzyma kwote poprzedniego.
        /// </summary>
        public static void ClanPayPost(Clan __0)
        {
            try
            {
                if (__0 == null || __0.IsBanditFaction || !OwnerTaxLive || !Live) return;   // bandy: gra nie rozlicza (DailyTickClan)
                var o = __0.Leader;
                if (o == null || o.IsNotable) return;
                var ws = o.OwnedWorkshops;
                if (ws == null || ws.Count == 0) return;
                var fin = ChainFin();
                if (fin == null) return;
                bool bkCharges = BkWorkshopModelActive();
                foreach (var w in ws)
                {
                    try
                    {
                        var st = w != null ? w.Settlement : null;
                        var town = st != null ? st.Town : null;
                        if (town == null) continue;
                        var tc = town.OwnerClan;
                        if (!OwnerPays(o, tc, tc != null ? tc.Leader : null, bkCharges)) continue;
                        int t = Convert.ToInt32(_mGetWsTax.Invoke(fin, new object[] { w }));
                        if (t <= 0) continue;
                        // wyplata (tylko do kontroli w linii doby): gra zdjela z kapitalu floor(zysk/2), zostalo ceil(zysk/2) - ProfitMade teraz = wyplata (+1)
                        _otPay.Add(new OtPay { W = w, Owner = o, Tax = t, Payout = Math.Max(0, w.ProfitMade) });
                    }
                    catch (Exception e) { Stumble("ClanPayPost(warsztat)", e); }
                }
            }
            catch (Exception e) { Stumble("ClanPayPost", e); }
        }

        /// <summary>
        /// D11-3, ArmouryBehavior raz na dobe po PopulationLaw.Daily, przed RentMerge (w bloku ksiegi - zmiane kas miast lapie migawka rent): kazdy warsztat
        /// lorda albo gracza w cudzym miescie placi podatek BK (GetWorkshopTaxes - kwota linii 'Taxes from') - czesc autonomii do kasy miasta (zdarzenie gry),
        /// reszta panu miasta (renta przez _rentPend -> RentMerge, jak E3/8a); najwyzej z kiesy wlasciciela, reszta przepada (zaleglosc nie rosnie).
        /// 121 (recenzja uwaga 2): kwota z migawek wyplat od ostatniej doby (ClanPayPost) - placi ten, kto dostal wyplate, raz za kazda wyplate;
        /// regula (OwnerPays) sprawdzana jeszcze raz na dzis (miasto moglo przejsc na rod wlasciciela, wlasciciel zmarl) - wtedy migawka przepada (licznik).
        /// </summary>
        internal static void OwnerTaxDaily()
        {
            try
            {
                if (!OwnerTaxLive || !Live) return;
                bool bkCharges = BkWorkshopModelActive();
                var owners = new HashSet<Hero>();
                var auts = new Dictionary<Settlement, float>();
                long plPaid = 0;
                foreach (var p in _otPay)
                {
                    try
                    {
                        var w = p.W; var o = p.Owner; int t = p.Tax;
                        var st = w != null ? w.Settlement : null;
                        var town = st != null ? st.Town : null;
                        if (town == null || st.SettlementComponent == null || o == null || t <= 0) { _otDrop += Math.Max(0, t); _otDropN++; continue; }
                        var townClan = town.OwnerClan;
                        var lord = townClan != null ? townClan.Leader : null;
                        if (!OwnerPays(o, townClan, lord, bkCharges) || !o.IsAlive || CW.OutOfWorld(o)) { _otDrop += t; _otDropN++; continue; }
                        float aut;
                        if (!auts.TryGetValue(st, out aut)) { aut = Autonomy(st); auts[st] = aut; }
                        _otN++; _otDue += t; _otPayout += p.Payout; owners.Add(o);
                        bool lordOk = lord != null && lord.IsAlive && !CW.OutOfWorld(lord) && !ReferenceEquals(lord, o);
                        long toTown = lordOk ? (long)Math.Round(t * Math.Min(1.0, 0.6 * aut)) : t;
                        long toLord = t - toTown;
                        long paid = 0;
                        if (toTown > 0)
                        {
                            int before = o.Gold;
                            if (before > 0) GiveGoldAction.ApplyForCharacterToSettlement(o, st, (int)Math.Min(toTown, before), true);   // zdarzenie gry: wlasciciel -> kasa miasta
                            long moved = Math.Max(0, before - o.Gold);
                            if (lordOk) _otToTowns += moved; else _otNoLord += moved;
                            paid += moved;
                        }
                        if (toLord > 0)
                        {
                            int got = (int)Math.Min(toLord, Math.Max(0, o.Gold));
                            if (got > 0)
                            {
                                // bez zdarzenia gry: wplata liczy sie jak renta miasta (RentMerge), nie jak wplyw jednorazowy - jak E3/8a
                                o.ChangeHeroGold(-got);
                                lord.ChangeHeroGold(got);
                                var c = lord.Clan;
                                if (c != null) { int r0; _rentPend.TryGetValue(c, out r0); _rentPend[c] = r0 + got; }
                                _otToLords += got; paid += got;
                            }
                        }
                        if (paid < t) { _otShort += t - paid; _otShortN++; }
                        if (o == Hero.MainHero) { plPaid += paid; _otPlayerN++; CaravanSettle.NoteWsTax(w, t, paid); }   // 259: linia majatku gracza (nalezny / zaplacony)
                    }
                    catch (Exception e) { Stumble("OwnerTaxDaily(warsztat)", e); }
                }
                _otOwners += owners.Count;
                _otPlayer += plPaid;
                if (plPaid > 0 && !_otTold)
                {
                    _otTold = true;
                    Log.Player("Workshop tax: you paid " + plPaid + " denars today to the lords of the towns where your workshops stand (the Banner Kings tax on their profit; the town keeps its autonomy share). It is paid every day from your purse.");
                }
            }
            catch (Exception e) { Stumble("OwnerTaxDaily", e); }
            finally { _otPay.Clear(); }   // kazda migawka raz: pobrana, przepadla albo (D11-3 nieczynne) porzucona
        }

        /// <summary>ArmouryBehavior, zaraz po PopulationLaw.Daily (ktore czysci RentToday): przelewy podatku od ostatniej doby jako renta miasta panow
        /// (D "ziemia", powinnosci 165 i budowy licza od RentToday - tak jak dotad od linii modelu).</summary>
        internal static void RentMerge()
        {
            try
            {
                foreach (var kv in _rentPend)
                {
                    if (kv.Key == null || kv.Key.IsEliminated || kv.Value <= 0) continue;
                    int r0; PopulationLaw.RentToday.TryGetValue(kv.Key, out r0);
                    PopulationLaw.RentToday[kv.Key] = r0 + kv.Value;
                    _rentMerged += kv.Value;
                }
            }
            catch (Exception e) { Stumble("RentMerge", e); }
            finally { _rentPend.Clear(); }
        }

        // ================================================================= E3/8b: oplaty lordow BK - odbiorca wedlug rodzaju
        internal sealed class FeeState
        {
            public int Kind; public object Action; public object Inst; public Hero Knight; public Clan Clan;
            public readonly List<Hero> Payers = new List<Hero>();
            public readonly List<int> G0 = new List<int>(), Cap = new List<int>(), D = new List<int>();
        }

        private static FeeState OneFee(int kind, Hero payer, int cap)
        {
            if (payer == null || !payer.IsAlive) return null;
            var st = new FeeState { Kind = kind };
            st.Payers.Add(payer); st.G0.Add(payer.Gold); st.Cap.Add(cap); st.D.Add(0);
            return st;
        }

        private static int KindOf(MethodBase m)
        {
            string n = m != null ? m.Name : "";
            return n == "AddOngoingClaim" ? FClaim : n == "CreateTitle" ? FCreate : n == "RevokeTitle" ? FRevoke : FUsurp;
        }

        public static void TitlePre(object __0, MethodBase __originalMethod, out FeeState __state)
        {
            __state = null;
            try
            {
                if (!FeesOn || !Live || __0 == null) return;
                __state = OneFee(KindOf(__originalMethod), _pTaker.GetValue(__0, null) as Hero, int.MaxValue);
                if (__state != null) __state.Action = __0;
            }
            catch (Exception e) { __state = null; Stumble("TitlePre", e); }
        }

        public static void UsurpPre(object __1, out FeeState __state)
        {
            __state = null;
            try
            {
                if (!FeesOn || !Live || __1 == null) return;
                __state = OneFee(FUsurp, _pTaker.GetValue(__1, null) as Hero, int.MaxValue);
                if (__state != null) __state.Action = __1;
            }
            catch (Exception e) { __state = null; Stumble("UsurpPre", e); }
        }

        public static void DilemmaPre(object __0, out FeeState __state)
        {
            __state = null;
            try
            {
                if (!FeesOn || !Live || __0 == null) return;
                var k = _pDilKingdom.GetValue(__0, null) as Kingdom;
                if (k == null || k.Clans == null) return;
                var st = new FeeState { Kind = FDilemma };
                foreach (var c in k.Clans)
                {
                    var l = c != null ? c.Leader : null;
                    if (l == null || !l.IsAlive || c == Clan.PlayerClan) continue;   // BK pomija rod gracza
                    st.Payers.Add(l); st.G0.Add(l.Gold); st.Cap.Add(5000); st.D.Add(0);   // BK: najwyzej 5 000 na rod w jednym wywolaniu
                }
                if (st.Payers.Count > 0) __state = st;
            }
            catch (Exception e) { __state = null; Stumble("DilemmaPre", e); }
        }

        public static void CourtPre(Clan __0, out FeeState __state)
        {
            __state = null;
            try
            {
                if (!FeesOn || !Live || __0 == null) return;
                __state = OneFee(FCourt, __0.Leader, int.MaxValue);
                if (__state != null) __state.Clan = __0;
            }
            catch (Exception e) { __state = null; Stumble("CourtPre", e); }
        }

        public static void MovePre(object __instance, out FeeState __state)
        {
            __state = null;
            try
            {
                if (!FeesOn || !Live || __instance == null) return;
                var f = _mFulfiller.Invoke(__instance, null) as Hero;
                var c = f != null ? f.Clan : null;
                __state = OneFee(FMove, c != null ? c.Leader : null, int.MaxValue);
                if (__state != null) { __state.Inst = __instance; __state.Clan = c; }
            }
            catch (Exception e) { __state = null; Stumble("MovePre", e); }
        }

        public static void KnightPre(Hero __0, Clan __1, out FeeState __state)
        {
            __state = null;
            try
            {
                if (!FeesOn || !Live || __0 == null || __1 == null) return;
                __state = OneFee(FKnight, __1.Leader, 50000);       // BK: Math.Min(50000, kiesa)
                if (__state != null) { __state.Knight = __0; __state.Clan = __1; }
            }
            catch (Exception e) { __state = null; Stumble("KnightPre", e); }
        }

        /// <summary>Postfiks (przed finalizerem okna ksiegi obiegu): to, co naprawde zeszlo platnikowi (najwyzej kwota BK), wraca mu na chwile -
        /// okno widzi 0 w nicosc, a przelew do odbiorcy robi finalizer FeeFin (Low - po oknie).</summary>
        public static void FeePost(FeeState __state)
        {
            if (__state == null) return;
            try
            {
                int cap0 = -1;
                if (__state.Action != null && _pActGold != null)
                {
                    float g = (float)_pActGold.GetValue(__state.Action, null);   // kwota po rabacie BK (perk LordshipClaimant)
                    cap0 = g > 0f ? (int)g : 0;
                }
                for (int i = 0; i < __state.Payers.Count; i++)
                {
                    var p = __state.Payers[i];
                    int drop = __state.G0[i] - p.Gold;
                    int cap = cap0 >= 0 ? Math.Min(cap0, __state.Cap[i]) : __state.Cap[i];
                    int d = Math.Max(0, Math.Min(drop, cap));
                    if (d <= 0) continue;
                    p.ChangeHeroGold(d);
                    __state.D[i] = d;
                }
            }
            catch (Exception e) { Stumble("FeePost", e); }
        }

        /// <summary>Finalizer (Low - po finalizerze okna ksiegi): platnik -> odbiorca wedlug rodzaju oplaty.</summary>
        public static void FeeFin(FeeState __state, Exception __exception)
        {
            if (__state == null || __exception != null) return;
            int k = __state.Kind;
            for (int i = 0; i < __state.Payers.Count; i++)
            {
                int d = __state.D[i];
                if (d <= 0) continue;
                var p = __state.Payers[i];
                try
                {
                    _feePaid[k] += d; _feeN[k]++;
                    if (k <= FUsurp || k == FDilemma)
                    {
                        var kg = p.Clan != null ? p.Clan.Kingdom : null;
                        if (kg != null && !kg.IsEliminated)
                        {
                            int take = Math.Min(d, Math.Max(0, p.Gold));
                            p.ChangeHeroGold(-take);
                            kg.KingdomBudgetWallet += take;            // skarbiec krolestwa platnika (korona liczy to jako wplyw dnia)
                            _feeToWallet[k] += take;
                            if (take < d) _feeKept[k] += d - take;
                            continue;
                        }
                        PayTown(k, p, NearestTown(PosOf(p)), d);
                    }
                    else if (k == FCourt)
                    {
                        Settlement seat = null;
                        try { seat = __state.Clan != null ? ClanBudget.Seat(__state.Clan) : null; } catch { seat = null; }
                        PayTown(k, p, seat ?? NearestTown(PosOf(p)), d);
                    }
                    else if (k == FMove)
                    {
                        var town = __state.Inst != null ? _fMoveTown.GetValue(__state.Inst) as Town : null;
                        PayTown(k, p, town != null ? town.Settlement : NearestTown(PosOf(p)), d);
                    }
                    else if (k == FKnight)
                    {
                        var kn = __state.Knight;
                        if (kn != null && kn.IsAlive && !CW.OutOfWorld(kn) && !ReferenceEquals(kn, p) && kn.Clan != null && kn.Clan != __state.Clan)
                        {
                            int before = p.Gold;
                            GiveGoldAction.ApplyBetweenCharacters(p, kn, d, true);   // wiano: stary rod -> glowa nowego rodu rycerza
                            int moved = before - p.Gold;
                            _feeToHero[k] += moved;
                            if (moved < d) _feeKept[k] += d - moved;
                        }
                        else PayTown(k, p, NearestTown(PosOf(kn ?? p)), d);
                    }
                }
                catch (Exception e) { Stumble("FeeFin(" + FeeName[k] + ")", e); }
            }
        }

        private static void PayTown(int k, Hero p, Settlement st, int d)
        {
            if (st == null || st.SettlementComponent == null) { _feeKept[k] += d; return; }   // bez odbiorcy - zostaje u platnika (nie znika)
            int before = p.Gold;
            GiveGoldAction.ApplyForCharacterToSettlement(p, st, d, true);
            int moved = before - p.Gold;
            _feeToTown[k] += moved;
            if (moved < d) _feeKept[k] += d - moved;
        }

        // ================================================================= E3/8c: zloto zmarlego zostaje w osadzie
        /// <summary>Prefiks KillCharacterAction.MakeDead: gra juz oddala zloto glowie rodu / nastepcy; reszta (nikt nie dziedziczy) -> kasa osady.</summary>
        public static void MakeDeadPre(Hero __0)
        {
            try
            {
                if (!DeadOn || !Live || __0 == null || __0 == Hero.MainHero || __0.Gold <= 0) return;
                Settlement st = null;
                if (__0.IsNotable) st = Fief(__0.CurrentSettlement) ?? Fief(__0.HomeSettlement);
                if (st == null) st = Fief(__0.CurrentSettlement);
                if (st == null) st = NearestTown(PosOf(__0));
                int g = __0.Gold;
                if (st == null || st.SettlementComponent == null) { _deadLost += g; return; }
                GiveGoldAction.ApplyForCharacterToSettlement(__0, st, g, true);   // zdarzenie gry: przelew przed wyjsciem ze swiata
                int moved = g - __0.Gold;
                if (__0.IsNotable) { _deadNotable += moved; _deadNotableN++; }
                else if (__0.IsWanderer) { _deadWanderer += moved; _deadWandererN++; }
                else if (__0.Clan != null && __0.Clan.Leader == __0) { _deadHead += moved; _deadHeadN++; }   // glowa rodu bez nastepcy (z nastepca gra oddala mu zloto)
                else { _deadOther += moved; _deadOtherN++; }
            }
            catch (Exception e) { Stumble("MakeDeadPre", e); }
        }

        private static Settlement Fief(Settlement st) { return st != null && (st.IsTown || st.IsCastle || st.IsVillage) && st.SettlementComponent != null ? st : null; }

        // ================================================================= E3/9: zysk karawany pana w kazdym miescie jego krolestwa
        public static void ProfitPre(MobileParty __0, Settlement __1, out int __state)
        {
            __state = int.MinValue;
            try
            {
                if (!ProfitOn || __0 == null || !__0.IsCaravan) return;
                // 259: karawana rozliczana przez rod (CaravanSettle) - zysk idzie codziennie w rozliczeniu rodu; ten prefiks (void) biegnie nawet, gdy prefiks 259
                // na AddRealisticIncome zwrocil false (Harmony pomija tylko prefiksy bool / z ref) - stad wlasny warunek; licznik "E3/9 n" w linii 259
                if (CaravanSettle.Settled(__0)) { CaravanSettle.NoteE39Blocked(__0, __1); return; }
                __state = __0.PartyTradeGold;
            }
            catch { __state = int.MinValue; }
        }

        public static void ProfitPost(MobileParty __0, Settlement __1, int __state)
        {
            if (__state == int.MinValue || __0 == null || __1 == null) return;
            try
            {
                if (!RealisticCaravanIncome()) return;                  // BK nie wplaca wcale (wtedy dochod karawan liczy model) - my tez nie
                if (CaravanSettle.Settled(__0)) return;                 // 259: rozliczana przez rod - druga zapora (gdyby prefiks wyzej dal stan mimo to)
                if (__0.PartyTradeGold < __state)
                {
                    _profitBk += __state - __0.PartyTradeGold; _profitBkN++;   // BK wplacil (siedziba, osada wlasciciela, udzialowiec)
                    if (__0.ActualClan == Clan.PlayerClan) CaravanSettle.NoteDeposit(__0, CaravanSettle.DepBk, __state - __0.PartyTradeGold);   // 259: linia majatku gracza
                    return;
                }
                var owner = __0.Owner;
                if (owner == null || !owner.IsAlive || !owner.IsLord || owner.Clan == null) return;
                var k = owner.Clan.Kingdom;
                if (k == null || !__1.IsTown || __1.MapFaction != k) return;
                int num = Math.Max(0, __0.PartyTradeGold - 10000);         // ta sama kwota co BK
                if (num <= 0) return;
                int before = __0.PartyTradeGold;
                bool pn = owner == Hero.MainHero && PayNotes.On;   // m96-wies: graczowi jeden komunikat z powodem zamiast "You received N" gry
                GiveGoldAction.ApplyForPartyToCharacter(__0.Party, owner, num, pn);
                int moved = before - __0.PartyTradeGold;
                if (moved <= 0) return;
                _profitPaid += moved; _profitN++;
                if (__0.ActualClan == Clan.PlayerClan) CaravanSettle.NoteDeposit(__0, CaravanSettle.DepE39, moved);   // 259: linia majatku gracza
                // te same skutki co wplata BK: komunikat dla gracza, doswiadczenie handlu dowodcy, slawa za perk Great Investor
                if (pn) PayNotes.Got("caravan", moved, ": the " + __0.Name + " deposited its profit with you in " + __1.Name + ".");
                else if (owner == Hero.MainHero) Log.Player("The " + __0.Name + " has deposited " + moved + " denars with you in " + __1.Name + ".");
                if (__0.LeaderHero != null) SkillLevelingManager.OnTradeProfitMade(__0.LeaderHero, moved);
                var leader = owner.Clan.Leader;
                if (leader != null && leader.GetPerkValue(DefaultPerks.Trade.GreatInvestor)) owner.Clan.AddRenown(DefaultPerks.Trade.GreatInvestor.PrimaryBonus, true);
            }
            catch (Exception e) { Stumble("ProfitPost", e); }
        }

        internal static bool RealisticCaravanIncome()                 // E3-T2 (CaravanGoods.ProfitToOwners) czyta ten sam przelacznik BK
        {
            try
            {
                var inst = _pBkSet.GetValue(null, null);
                return inst != null && (bool)_pRealCar.GetValue(inst, null);
            }
            catch { return false; }
        }

        // ================================================================= pomocnicy
        private static Vec2 PosOf(Hero h)
        {
            try
            {
                if (h == null) return Vec2.Invalid;
                if (h.PartyBelongedTo != null) return h.PartyBelongedTo.GetPosition2D;
                if (h.CurrentSettlement != null) return h.CurrentSettlement.GetPosition2D;
                if (h.HomeSettlement != null) return h.HomeSettlement.GetPosition2D;
            }
            catch { }
            return Vec2.Invalid;
        }

        private static Settlement NearestTown(Vec2 p)
        {
            if (!p.IsValid) return null;
            Settlement best = null; float bd = float.MaxValue;
            foreach (var t in Settlement.All)
            {
                if (t == null || !t.IsTown || t.Town == null) continue;
                float d = p.DistanceSquared(t.GetPosition2D);
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }

        // ================================================================= raz na dobe (po ksiedze pieniadza)
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                long carPurses = 0, carWage3 = 0; int carWith = 0;
                try
                {
                    foreach (var mp in MobileParty.AllCaravanParties)
                    {
                        if (mp == null) continue;
                        int g = MenPurse.Get(mp);
                        if (g > 0) { carPurses += g; carWith++; }
                        carWage3 += 3L * Math.Max(0, mp.TotalWage);
                    }
                }
                catch (Exception e) { Stumble("Daily(karawany)", e); }
                long total = 0, orphanGold = 0; int count = 0, max = 0, orphans = 0; string maxKey;
                try { MenPurse.Stats(out total, out count, out max, out maxKey, out orphans, out orphanGold); } catch { }
                var sb = new StringBuilder(2048);
                sb.Append("Szczelnosc (164a/31b): dzien ").Append(day);
                // E3/4
                sb.Append(" | karawany (E3/4 ").Append(Onoff(WageOn)).Append(_wNotCar ? "" : ", karawany notabli NIECZYNNE - BRAK latki").Append("): zold do sakiewek ludzi - notable ").Append(_notWage).Append(" (z kas karawan ").Append(_notFromCar)
                  .Append(", z kies notabli ").Append(_notFromNot).Append("; ").Append(_notN).Append(" notabli; rozjazd migawki ").Append(_notMismatch)
                  .Append(", karawany poza mapa - w nicosc jak dotad ").Append(_notSkipped).Append("), lordowie ").Append(_lordCarWage).Append(" (").Append(_lordCarN).Append(" wyplat)")
                  .Append("; wydane w miastach ").Append(_carSpent).Append(" (").Append(_carSpentN).Append(" wyjazdow)")
                  .Append("; sakiewki karawan razem ").Append(carPurses).Append(" w ").Append(carWith).Append(" karawanach (prog: <= 3 dni ich zoldu ").Append(carWage3).Append(")")
                  .Append("; sakiewki bez partii na mapie (wszystkie) ").Append(orphanGold).Append(" (").Append(orphans).Append(")")
                  .Append("; sakiewki rozbitych karawan ").Append(_carPurseN).Append(": do zwyciezcow z bohaterem ").Append(_carPurseHero).Append(", do band ").Append(_carPurseBand)
                  .Append(", do innych partii z kiesa ").Append(_carPurseParty).Append(", do kas osad niszczyciela ").Append(_carPurseSettl).Append(", do najblizszego miasta ").Append(_carPurseTown)
                  .Append(", poza regula 3.2 (niepokonane w bitwie przez przywodce zwyciezcow) do najblizszego miasta ").Append(_carPurseOdd).Append(" (").Append(_carPurseOddN).Append("), bez miasta ").Append(_carPurseOddLost)
                  .Append("; W13 wszystkie partie: do band ").Append(_allPurseBand).Append(", do innych partii ").Append(_allPurseParty).Append(", do osad niszczyciela ").Append(_allPurseSettl);
                // E3/8a
                sb.Append(" | podatek warsztatow BK (E3/8a ").Append(Onoff(TaxOn)).Append(TaxLive || !TaxOn ? "" : ", NIECZYNNE - BK poza lancuchem modelu, brak latki renty miast albo renty wylaczone").Append("): notabli ").Append(_taxNotables)
                  .Append(", podatek BK ").Append(_taxSeen).Append(", zniklo u notabli (oddane w wyplacie) ").Append(_taxZ).Append(" -> do panow ").Append(_taxToLords)
                  .Append(", do kas miast (autonomia) ").Append(_taxToTowns).Append(", do kas miast bez pana ").Append(_taxNoLord).Append(", nie przelane ").Append(_taxShort)
                  .Append(" (wynik BK <= 0: ").Append(_taxNeg).Append(" notabli); linie 'Taxes from' warsztatow notabli zdjete z podatku miast (suma wywolan modelu) ")
                  .Append(System.Threading.Interlocked.Read(ref _taxLinesCut)).Append("; do rent panow (RentToday) dzis ").Append(_rentMerged)
                  .Append("; linie 'Taxes from' warsztatow, ktorych wlasciciel nie placi (lordowie, gracz - BK nie pobiera), zdjete z podatku miast (suma wywolan modelu, bez przelewu) ")
                  .Append(System.Threading.Interlocked.Read(ref _unpaidCut)).Append(" (").Append(System.Threading.Volatile.Read(ref _unpaidN)).Append(" linii)");
                // D11-3
                sb.Append(" | podatek warsztatow lordow i gracza w cudzych miastach (D11-3 ").Append(Onoff(OwnerTaxOn)).Append(OwnerTaxLive || !OwnerTaxOn ? "" : ", " + OwnerTaxState())
                  .Append("): placi wlasciciel - warsztatow ").Append(_otN).Append(" (wlascicieli ").Append(_otOwners).Append("), podatek BK z wyplat rodow (kwota BK w chwili wyplaty) ").Append(_otDue)
                  .Append(" -> do panow miast (renta) ").Append(_otToLords).Append(", do kas miast (autonomia) ").Append(_otToTowns).Append(", do kas miast bez pana ").Append(_otNoLord)
                  .Append(", nie zaplacone - brak w kiesie wlasciciela (zaleglosc nie rosnie) ").Append(_otShort).Append(" (").Append(_otShortN).Append(" warsztatow); w tym gracz zaplacil ").Append(_otPlayer)
                  .Append(" (warsztatow gracza ").Append(_otPlayerN).Append("); kontrola: wyplaty z tych warsztatow ok. ").Append(_otPayout).Append(", podatek / wyplata ")
                  .Append(_otPayout > 0 ? (100.0 * _otDue / _otPayout).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%" : "-")
                  .Append(" (cena warsztatu zaklada 2 x stawka BK); migawki przepadle (wlasciciel zmarl albo miasto juz nie cudze) ").Append(_otDropN).Append(" (").Append(_otDrop).Append(")");
                // E3/8b
                sb.Append(" | oplaty lordow BK (E3/8b ").Append(Onoff(FeesOn)).Append(", latek ").Append(_wFees).Append("/8):");
                long feeAll = 0;
                for (int k = 0; k < Fees; k++)
                {
                    feeAll += _feePaid[k];
                    if (_feeN[k] == 0) continue;
                    sb.Append(' ').Append(FeeName[k]).Append(' ').Append(_feePaid[k]).Append(" (").Append(_feeN[k]).Append(") -> skarbce ").Append(_feeToWallet[k])
                      .Append(", kasy osad ").Append(_feeToTown[k]).Append(", glowy nowych rodow ").Append(_feeToHero[k]).Append(", zostalo u platnika ").Append(_feeKept[k]).Append(';');
                }
                sb.Append(" razem ").Append(feeAll).Append(" - w nicosc 0");
                // E3/8c
                sb.Append(" | zmarli (E3/8c ").Append(Onoff(DeadOn)).Append(_wDead ? "" : ", NIECZYNNE - BRAK latki").Append("): zloto bez dziedzica do kas osad - notable ").Append(_deadNotable).Append(" (").Append(_deadNotableN)
                  .Append("), wedrowcy ").Append(_deadWanderer).Append(" (").Append(_deadWandererN).Append("), glowy rodow bez nastepcy ").Append(_deadHead).Append(" (").Append(_deadHeadN)
                  .Append("), inni ").Append(_deadOther).Append(" (").Append(_deadOtherN).Append("), bez osady ").Append(_deadLost);
                // E3/9
                sb.Append(" | zysk karawan pana (E3/9 ").Append(Onoff(ProfitOn)).Append(_wProfit ? "" : ", NIECZYNNE - BRAK latki").Append("): w miastach krolestwa ").Append(_profitPaid).Append(" (").Append(_profitN)
                  .Append(" wplat), BK sam (siedziba, osada, udzialowiec) ").Append(_profitBk).Append(" (").Append(_profitBkN).Append(")");
                sb.Append(" | E3/8d Inni bez zoldu - odlozone | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }
    }
}
