using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// PACZKA E5/A1 (111') - KASY MIAST TO PRAWDZIWY PIENIADZ (projekt etapu 5 v2, docs\etap5\PROJEKT-ETAP5-KIESA-LUDU-KASY-MIAST-2026-10-11.md
    /// rozdz. 2 R1-R7 i D4, rozdz. 3 "E5/A1"; plan kodu docs\etap5\PLAN-KODU-E5A1.md; decyzja Jeffa 11.10 16:10 - regulator domyslnie w trybie 2,
    /// miasta i zamki ta sama regula). Wzor: zawor zamku 110 (CastlePurse) i stara 111 (2ac5e72) - przepisane na dzisiejszy kod, nie przeniesione.
    /// Dotad gra co dobe sciagala kase miasta do celu 10 000 + 12 x dobrobyt (cwierc nadwyzki w nicosc, cwierc braku z niczego), "zakupy" mieszczan
    /// dopisywaly do kasy cene zjedzonego towaru z niczego (bieg z31-A: 421 tys./d sr28), a renta pana miasta brala 7% ponad 20 000 z pulapem
    /// "naleznej od ludzi" (10 miast krain spoza tabeli nie placilo wcale) - tarcze zoldu i dworu (107b, 162m) chronily wplaty przed regulatorem.
    ///
    /// Co robimy (jeden klucz glowny TownPurseRegulator; 0 = wszystko jak przed paczka, co do znaku):
    ///  R1 "Zakupy" mieszczan: migawka kasy przed konsumpcja i po niej (latki 110 CastlePurse oddaja miasta tutaj) - przyrost cofniety; towar
    ///     schodzi z polki jak dotad (do B1 "zjedzone bez zaplaty"). Czynne takze przy tworzeniu swiata (dwa ticki przed GameStarted, nieliczone).
    ///  R2 Regulator gry: tryb 2 (domyslnie, Jeff 16:10) - nic nie kasuje i nic nie dosypuje, w miastach i zamkach (zamki przy CastlePurseEnabled);
    ///     tryb 1 - nie kasuje, dosypuje najwyzej do zapasu kupcow (liczone). Przed GameStarted regulator miasta = 0 w kazdym trybie.
    ///  R3/D4 Jedna nadwyzka: zapas kupcow (Reserve = nie mniej niz cel regulatora gry) - jedna liczba dla zaworu, podatkow (TaxFloor), nadwyzki
    ///     RealPayer/portow (Spare przez Seal31.Spare) i naszych zakupow towaru przez kase miasta (BuyFloor, wylacznik TownPaysFromSurplus).
    ///  R4 Zawor: co dobe TownRentShare (7%) nadwyzki ponad zapas - pan TownRentLordShare (2/3; dekret BK skaluje tylko jego czesc), korona reszte;
    ///     E5/B3 (Z15-5): przy czynnej kiesie ludu i kluczu CastleLordsEvenUp pan TownRentLordShareWithFolk (1/2) - LordShare -> LordsEvenUp.TownLordShare
    ///     (miasto rodu bez krolestwa - calosc panu; oblezone i bez zywego pana - nic). Liczony w PopulationLaw.Daily (ValveTowns -> Valve) dla
    ///     WSZYSTKICH miast - "10 miast bez placenia" znika. Bez pulapu naleznej (zostaje liczba w linii).
    ///  R5 Dar startowy (gra 20 000 + BK 40 x dobrobyt) przyciety raz na kampanie do zapasu w BeforeRents (flaga arm_townpurse) - jak zamki 110.
    ///  R6 BK HandleMarketGold: gorna galaz (kasa ponad limit BK: 1% swiata w nicosc) pominieta dla miast i zamkow (zamki przy CastlePurseEnabled);
    ///     dolna (notabl doplaca 1 000 pustej kasie - przelew) zostaje.
    ///  R7 Tarcze 107b/162m zbedne: SoldierPay nie dopisuje znacznikow, znaczniki z zapisu porzucone (linia raz). Bez HomePart (K6).
    /// Wyjatki per obiekt - licznik potkniec (pierwszy do logu), bez globalnych wylacznikow. Zloto: kazdy ruch ma platnika i odbiorce (pan przez
    /// GiveGoldAction, korona para ChangeGold/KingdomBudgetWallet), jedyne ujscie bez odbiorcy - dar startowy (MoneyLedger.MTownTrim, raz).
    /// Pomiar G24 / N3 (audyt zlota z niczego 11.10, rozdz. 1b wiersze 19-20 i 5a; tylko licznik, przy LogEnabled, niczego nie zmienia): przy trybie 2 kasy
    /// biednych miast moga zejsc nisko i ozyja dwie dziury, ktorych dzis nie widac (najnizsza kasa miasta w biegach 26,7 tys.):
    ///  G24 - osada placi bohaterowi wiecej, niz ma w kasie: GiveGoldAction.ApplyForSettlementToCharacter idzie przez ApplyInternal(bohater, null, null, osada, -kwota),
    ///        wiec gra nie obcina kwoty do kasy (obcina tylko dajacego bohatera/partie/osade - G GiveGoldAction.cs:11-25), a SettlementComponent.ChangeGold obcina
    ///        kase do 0 (G SettlementComponent.cs:117-124) - bohater dostaje cala kwote, roznica z niczego (m.in. lord sprzedaje miastu konie i lup - SellItemsAction.cs:62-66).
    ///        Prefiks Last na ApplyInternal (po wszystkich naszych zamianach strony), tylko odczyt.
    ///  N3  - warsztat BK przy pustej kasie: BK ProduceOutputPrefix daje warsztatowi cala cene i zdejmuje ja z kasy miasta (BK EconomyPatches.cs:725-726), kasa
    ///        spada najwyzej do 0 - niedoplata kasy = przyrost kapitalu minus spadek kasy, gdy kasa skonczyla na 0 (okno O35 ksiegi obiegu, CW.WorkshopOutPost).
    /// Latka (obciecie kwoty do kasy) - dopiero, gdy licznik > 0 (audyt 5b "N3 / G24").
    /// </summary>
    internal static class TownPurse
    {
        private const int VanillaStartGold = 20000;          // Town: ChangeGold(20000) przy zakladaniu osady (tylko do napisu - rachunek w CastlePurse.StartGiftCut)
        private const int GameTargetGold = 10000;            // cel regulatora gry: 10 000 + 12 x dobrobyt (DefaultSettlementEconomyModel.GetTownGoldChange)
        private const float GameTargetPerProsperity = 12f;
        private const int BkLimitBase = 50000;               // BK BKEconomyModel.GetSettlementMarketGoldLimit: lenno 50 000 + dobrobyt x 25 (miasto) / x 12 (zamek)
        private const float BkLimitTown = 25f, BkLimitCastle = 12f;
        private const int BkLimitVillageBase = 10000;        // E5/A1 R6 wsie: BK GetSettlementMarketGoldLimit - wies 10 000 + hearth x 3
        private const float BkLimitVillageHearth = 3f;
        private const double FreshAgeDays = 1.5;             // jak CastlePurse: pierwszy tick dobowy nowej kampanii w wieku ok. 1.0 doby
        private const double RegulatorKeeps = 0.75;          // regulator gry zdejmowal cwierc nadwyzki dziennie (stary zapis)
        private const int RingDays = 28;

        // ------------------------------------------------------------ stan kampanii (czysci Reset; Reset wolany z CastlePurse.Reset)
        private static bool _trimDone;                       // dar startowy rozliczony (zapis arm_townpurse "done")
        private static double _liveAge = double.NaN;         // wiek kampanii, w ktorym A1 ruszylo bez przyciecia daru (zapis "on:<wiek>")
        private static bool _trimSkip;                       // wieku kampanii nie da sie odczytac - w tej sesji nie probujemy dalej
        private static bool _shieldsDropped;                 // R7: znaczniki tarcz z zapisu porzucone w tej sesji (linia raz)
        private static bool _errLogged;
        private static int _stumbles;                        // potkniecia od ostatniej linii (LineSegment zeruje)

        // ------------------------------------------------------------ nawias dziennego ticku miasta (konsumpcja -> regulator)
        private static Town _consTown, _regDue;
        private static int _consGold;

        // ------------------------------------------------------------ liczniki tickow gry (ticki miast ida przez cala dobe) - zamyka BeforeRents
        private static long _dRegDown, _dRegUp, _dRegUpCut, _dConsBack, _dConsKept, _dBkSkipGold, _dConsFolk, _dBkSkipGoldV;   // E5/A1 R6 wsie: _dBkSkipGoldV
        private static int _dRegDownN, _dRegUpN, _dRegUpCutN, _dConsN, _dConsHit, _dBkSkipTowns, _dBkSkipCastles, _dBuyAsk, _dBuyZero, _dBkSkipVillages;
        // pomiar G24 / N3 (audyt 11.10) - liczniki tickow gry, zamyka BeforeRents
        private static long _dG24, _dG24Pl, _dN3;
        private static int _dG24N, _dN3N;
        private static bool _g24Hooked;

        // ------------------------------------------------------------ zawor dnia (zeruje BeforeRents, zamyka ValveDay)
        private static long _vLord, _vLordBase, _vLordBaseK, _vCrown, _vDue, _vPlayer, _vFolk;
        private static int _vPayers, _vBelow, _vSiege, _vNoLord, _vTiny, _vExempt, _vNoCrown, _vUnpaid;
        private static readonly HashSet<Kingdom> _vKingdoms = new HashSet<Kingdom>();
        private static readonly Dictionary<Town, int> _crownBy = new Dictionary<Town, int>();   // do kasy-miast.csv

        // ------------------------------------------------------------ odczyty dnia (zeruje MoneyLedger.ClearLast169 w BlockOpen; ustawia BeforeRents i ValveDay)
        internal static long LastRegDown, LastRegUp, LastRegUpCut, LastConsBack, LastConsKept, LastBkSkipGold, LastLordPaid, LastLordBase, LastLordBaseK, LastCrownPaid, LastDue, LastPlayerPaid, LastTrim;
        internal static long LastG24, LastG24Pl, LastN3;                     // pomiar G24 / N3 (audyt 11.10)
        internal static long LastBkSkipGoldV; internal static int LastBkSkipVillages;   // E5/A1 R6 wsie: gorna galaz BK pominieta we wsiach (ile BK skasowalby)
        internal static int LastG24N, LastN3N;
        internal static long LastConsFolk, LastFolkValve;   // E5/B1: lud zaplacil kasom w konsumpcji; czesc ludu z zaworu (5%)
        internal static int LastRegDownN, LastRegUpN, LastRegUpCutN, LastConsN, LastConsHit, LastBkSkipTowns, LastBkSkipCastles, LastBuyAsk, LastBuyZero, LastPayers, LastKingdoms,
                            LastBelow, LastSiege, LastNoLord, LastTiny, LastExempt, LastNoCrown, LastUnpaid;
        /// <summary>165: udzial korony z zaworu kas miast dzis, na krolestwo (jak CastlePurse.CrownToday) - czyszczony w BeforeRents.</summary>
        internal static readonly Dictionary<Kingdom, long> CrownToday = new Dictionary<Kingdom, long>();

        // ------------------------------------------------------------ zaulki i udzial zysku kupcow (Jeff 16:10) - pamiec sesji, bez zapisu
        private static readonly Dictionary<Town, int> _streak = new Dictionary<Town, int>();            // doby z rzedu ponizej zapasu
        private static readonly Dictionary<Town, long> _goldAtBlock = new Dictionary<Town, long>();     // kasa na poczatku BeforeRents (przed przycieciem)
        private static readonly Dictionary<Town, long> _takeWin = new Dictionary<Town, long>();         // pobrane z kasy w oknie blok-blok (zawor + podatki)
        private static readonly Dictionary<Town, long> _trimWin = new Dictionary<Town, long>();         // dar startowy zdjety w oknie
        private static readonly Dictionary<Town, long> _mintWin = new Dictionary<Town, long>();         // z niczego w oknie (dosypka trybu 1, "zakupy" przy wylaczonym R1)
        private static readonly Dictionary<Town, long> _takeLast = new Dictionary<Town, long>(), _netLast = new Dictionary<Town, long>();
        private static readonly long[] _ringTake = new long[RingDays], _ringNet = new long[RingDays];
        private static int _ringHead, _ringFilled;
        internal static long LastTake, LastNet, LastTake28, LastNet28;
        internal static bool LastShareOk;
        internal static int LastBelowN, LastBelow1000, LastAtZero, LastStreakMax, LastStreak7, LastStreak28;
        internal static string LastStreakName;

        // ------------------------------------------------------------ latka na BK (raz na proces) i odczyt limitu BK (czyszczony w Reset)
        private static bool _bkHooked;
        private static MethodInfo _bkLimit; private static PropertyInfo _bkInstance, _bkModel; private static bool _bkResolved;

        internal static void Reset()
        {
            _trimDone = false; _liveAge = double.NaN; _trimSkip = false; _shieldsDropped = false; _errLogged = false; _stumbles = 0;
            _consTown = null; _regDue = null; _consGold = 0;
            _bkResolved = false; _bkLimit = null; _bkInstance = null; _bkModel = null;   // BK tworzy konfiguracje od nowa przy kazdej grze
            ClearTicks(); ClearValve(); CrownToday.Clear();
            _streak.Clear(); _goldAtBlock.Clear(); _takeWin.Clear(); _trimWin.Clear(); _mintWin.Clear(); _takeLast.Clear(); _netLast.Clear();
            ClearRing();
            ZeroLast();
        }

        /// <summary>MoneyLedger.ClearLast169 (pierwsza instrukcja BlockOpen): liczby dnia zerowane w jednym miejscu.</summary>
        internal static void ZeroLast()
        {
            LastRegDown = LastRegUp = LastRegUpCut = LastConsBack = LastConsKept = LastBkSkipGold = LastLordPaid = LastLordBase = LastLordBaseK = LastCrownPaid = LastDue = LastPlayerPaid = LastTrim = 0;
            LastConsFolk = LastFolkValve = 0;
            LastBkSkipGoldV = 0; LastBkSkipVillages = 0;
            LastRegDownN = LastRegUpN = LastRegUpCutN = LastConsN = LastConsHit = LastBkSkipTowns = LastBkSkipCastles = LastBuyAsk = LastBuyZero = LastPayers = LastKingdoms = 0;
            LastBelow = LastSiege = LastNoLord = LastTiny = LastExempt = LastNoCrown = LastUnpaid = 0;
            LastTake = LastNet = LastTake28 = LastNet28 = 0; LastShareOk = false;
            LastBelowN = LastBelow1000 = LastAtZero = LastStreakMax = LastStreak7 = LastStreak28 = 0; LastStreakName = null;
            LastG24 = LastG24Pl = LastN3 = 0; LastG24N = LastN3N = 0;
        }

        private static void ClearTicks()
        {
            _dRegDown = _dRegUp = _dRegUpCut = _dConsBack = _dConsKept = _dBkSkipGold = _dConsFolk = _dBkSkipGoldV = 0;
            _dRegDownN = _dRegUpN = _dRegUpCutN = _dConsN = _dConsHit = _dBkSkipTowns = _dBkSkipCastles = _dBuyAsk = _dBuyZero = _dBkSkipVillages = 0;
            _dG24 = _dG24Pl = _dN3 = 0; _dG24N = _dN3N = 0;
        }

        private static void ClearValve()
        {
            _vLord = _vLordBase = _vLordBaseK = _vCrown = _vDue = _vPlayer = _vFolk = 0;
            _vPayers = _vBelow = _vSiege = _vNoLord = _vTiny = _vExempt = _vNoCrown = _vUnpaid = 0;
            _vKingdoms.Clear(); _crownBy.Clear();
        }

        private static void ClearRing()
        {
            Array.Clear(_ringTake, 0, RingDays); Array.Clear(_ringNet, 0, RingDays); _ringHead = 0; _ringFilled = 0;
        }

        internal static string Export()
        {
            if (_trimDone) return "done";
            return double.IsNaN(_liveAge) ? "" : "on:" + _liveAge.ToString("0.###", CultureInfo.InvariantCulture);
        }

        internal static void Import(string s)
        {
            _trimDone = s == "done";
            _liveAge = double.NaN;
            double a;
            if (!_trimDone && s != null && s.StartsWith("on:", StringComparison.Ordinal)
                && double.TryParse(s.Substring(3), NumberStyles.Float, CultureInfo.InvariantCulture, out a) && !double.IsNaN(a) && !double.IsInfinity(a))
                _liveAge = Math.Max(0.0, a);
        }

        // ------------------------------------------------------------ przelaczniki
        /// <summary>Tryb regulatora kas miast (i zamkow) z ustawien, obciety do 0..2: 0 = jak w grze (cala paczka wylaczona), 1 = bez kasowania,
        /// dosypka najwyzej do zapasu, 2 = regulator nic nie robi (domyslnie, Jeff 11.10 16:10).</summary>
        internal static int Mode
        {
            get
            {
                var s = Settings.Current;
                if (s == null) return 0;
                int m = s.TownPurseRegulator;
                return m <= 0 ? 0 : (m >= 2 ? 2 : 1);
            }
        }

        /// <summary>Kasa miasta prowadzona jako prawdziwy pieniadz: tryb 1 albo 2 i latki 110 (regulator i konsumpcja) wpiete.</summary>
        internal static bool On { get { return Mode > 0 && CastlePurse.HookedModels > 0 && CastlePurse.HookedConsumption; } }

        /// <summary>Regulator bez dosypki - miasta i zamki (zamki tylko w galezi CastlePurse, czyli przy CastlePurseEnabled).</summary>
        internal static bool NoTopUp { get { return Mode == 2 && On; } }

        private static bool NoMint { get { var s = Settings.Current; return s != null && s.TownFolkBuyWithoutMinting && On; } }

        /// <summary>E5/B1: A1 cofa "zakupy" mieszczan (warunek kiesy czynnej - bez niego lud placilby za towar, za ktory BK i tak dopisuje zloto).</summary>
        internal static bool NoMintNow { get { return NoMint; } }

        /// <summary>Zawor kas miast czynny: A1, renty (PopulationRentEnabled) i TownRentShare > 0. Inaczej PopulationLaw liczy rente miast jak dotad.</summary>
        internal static bool ValveLive { get { var s = Settings.Current; return s != null && On && PopulationLaw.On && ValveSplit.Unit(s.TownRentShare) > 0f; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errLogged) return;
            _errLogged = true;
            Log.Error(where, e);
        }

        // ------------------------------------------------------------ rachunek (D4 - jedna nadwyzka)
        private static float Prosperity(Town town)
        {
            float p = town.Prosperity;
            return float.IsNaN(p) || float.IsInfinity(p) || p < 0f ? 0f : p;
        }

        /// <summary>
        /// Zapas kupcow miasta: tej czesci kasy nie bierze zawor ani zaden podatek i z niej miasto nic dla nas nie kupuje. Nigdy ponizej celu
        /// regulatora gry (10 000 + 12 x dobrobyt) - ustawienia moga zapas tylko podniesc (jak CastlePurse.Reserve).
        /// </summary>
        internal static int Reserve(Town town)
        {
            float p = Prosperity(town);
            double game = GameTargetGold + (double)GameTargetPerProsperity * p;
            var s = Settings.Current;
            double r = game;
            if (s != null)
            {
                float pp = s.TownPurseFloorPerProsperity;
                if (float.IsNaN(pp) || float.IsInfinity(pp) || pp < 0f) pp = 0f;
                r = Math.Max(0, s.TownPurseFloorGold) + (double)pp * p;
                if (r < game) r = game;
            }
            return r >= int.MaxValue ? int.MaxValue : (int)r;
        }

        /// <summary>D4: nadwyzka kasy miasta ponad zapas kupcow (z zywej kasy, nie mniej niz 0). Seal31.Spare (RealPayer E3/1, porty 181) przy czynnym A1.</summary>
        internal static long Spare(Town town)
        {
            try
            {
                if (town == null) return 0;
                long v = (long)town.Gold - Reserve(town);
                return v > 0 ? v : 0;
            }
            catch (Exception e) { Stumble("TownPurse.Spare", e); return 0; }
        }

        /// <summary>
        /// Prog kasy dla tych, ktorzy biora z niej "ponad prog" (danina, clo, mennica, Crown Duty, War Tax, Road Tolls, Spoils): przy czynnym A1 dla
        /// MIASTA zapas kupcow (D4), inaczej - i zawsze dla zamku - `legacy` (stary prog, 20 000 albo zapas zamku). Sam odczyt, bez licznika.
        /// </summary>
        internal static int TaxFloor(Town t, int legacy)
        {
            try { if (t != null && t.IsTown && On) return Reserve(t); }
            catch (Exception e) { Stumble("TownPurse.TaxFloor", e); }
            return legacy;
        }

        /// <summary>
        /// Prog kasy dla NASZYCH zakupow towaru przez kase miasta (E5K1-03; jedna regula z zamkami B-2): przy czynnym A1 i TownPaysFromSurplus dla
        /// MIASTA zapas kupcow (ponizej niego gra dosypywala z niczego - zakup z zapasu byl w praktyce zaplacony z niczego), inaczej `legacy`.
        /// Licznik zapytan do linii "Kasy miast (169c)".
        /// </summary>
        internal static int BuyFloor(Town t, int legacy)
        {
            try
            {
                var s = Settings.Current;
                if (t == null || !t.IsTown || s == null || !s.TownPaysFromSurplus || !On) return legacy;
                int r = Reserve(t);
                _dBuyAsk++;
                if (t.Gold <= r) _dBuyZero++;
                return r;
            }
            catch (Exception e) { Stumble("TownPurse.BuyFloor", e); return legacy; }
        }

        /// <summary>CastlePurse.Spendable dla miasta: ile kasa moze wydac na zakup (przy A1 i TownPaysFromSurplus - nadwyzka; inaczej cala kasa).</summary>
        internal static int SpendableTown(Town t)
        {
            if (t == null) return 0;
            int gold = t.Gold;
            long v = (long)gold - BuyFloor(t, 0);
            return v <= 0 ? 0 : (v >= gold ? gold : (int)v);
        }

        /// <summary>Udzial pana w zaworze kasy miasta rodu w krolestwie k (0..1): TownRentLordShare, a przy wyrownaniu dla panow zamkow (E5/B3 - klucz
        /// CastleLordsEvenUp i czynna kiesa ludu) TownRentLordShareWithFolk; bez krolestwa - 1 (calosc panu). Czytaja: zawor (Valve) i ciecie K3 zwrotu korony.</summary>
        internal static float LordShare(Kingdom k)
        {
            var s = Settings.Current;
            if (s == null || k == null || k.IsEliminated) return 1f;
            return LordsEvenUp.TownLordShare(s);
        }

        private static long Get(Dictionary<Town, long> d, Town t) { long v; return d.TryGetValue(t, out v) ? v : 0; }
        private static void Add(Dictionary<Town, long> d, Town t, long x) { long v; d.TryGetValue(t, out v); d[t] = v + x; }

        // ------------------------------------------------------------ nawias dziennego ticku miasta (wolane z latek CastlePurse 110)
        /// <summary>ItemConsumptionBehavior.DeleteOverproducedItems - pierwszy krok dziennego ticku miasta: kasa PRZED "zakupami".</summary>
        internal static void OnShelf(Town town)
        {
            try
            {
                _consTown = null;
                if (town == null || Campaign.Current == null || !On) return;
                _consTown = town; _consGold = town.Gold;
            }
            catch (Exception e) { _consTown = null; Stumble("TownPurse.OnShelf", e); }
        }

        /// <summary>
        /// ItemConsumptionBehavior.MakeConsumption - po "zakupach" mieszczan (jedyny zapis zlota w nawiasie: BK ChangeGold(num6) za kazda pozycje
        /// polki): przyrost kasy wraca do nicosci, z ktorej przyszedl; towar zszedl z polki jak dotad. Wolane z postfiksu First (przed ksiega).
        /// Ticki z tworzenia swiata (przed GameStarted) cofamy tak samo, do licznikow nie wchodza.
        /// </summary>
        internal static void OnConsumed(Town town)
        {
            try
            {
                var t = _consTown; _consTown = null;
                if (t == null || !ReferenceEquals(t, town)) return;
                _regDue = town;                                  // zaraz potem gra pyta model o regulator tej samej osady
                var camp = Campaign.Current;
                bool live = camp != null && camp.GameStarted;
                if (live) _dConsN++;
                int made = town.Gold - _consGold;
                if (made <= 0) return;
                if (!NoMint)
                {
                    // R1 wylaczone (tylko do testow): zloto zostaje w kasie - z niczego; liczone, zeby udzial zysku kupcow go nie bral za doplyw
                    if (live) { _dConsKept += made; Add(_mintWin, town, made); }
                    return;
                }
                // E5/B1 (R1): kiesa czynna - lud placi za towar (TownFolk.PayForGoods robi jedyny zapis zlota: kasa zatrzymuje zaplate ludu, reszta wraca do nicosci)
                if (live && TownFolk.Live)
                {
                    int keep = TownFolk.PayForGoods(town, made);
                    if (keep >= 0) { _dConsBack += made - keep; _dConsHit++; _dConsFolk += keep; return; }
                    TownFolk.NoteFallback();                     // nie obsluzone (potkniecie, poza watkiem) - cofniecie A1 calosci (nic z niczego)
                }
                town.ChangeGold(-made);
                if (live) { _dConsBack += made; _dConsHit++; }
            }
            catch (Exception e) { Stumble("TownPurse.OnConsumed", e); }
        }

        /// <summary>
        /// GetTownGoldChange dowolnego modelu kasy osad, dla MIASTA (wolane z CastlePurse.RegulatorPostfix, First): tryb 2 - wynik 0 w obie strony;
        /// tryb 1 - kasowania nie ma, dosypka najwyzej do zapasu kupcow. Przed GameStarted (tworzenie swiata) 0 w kazdym trybie. Do licznikow raz na
        /// dzienny tick miasta - pytania z ekranow i drugi poziom modelu dostaja ten sam wynik bez licznikow (regula 110).
        /// </summary>
        internal static void OnRegulator(Town town, ref int result)
        {
            int mode = Mode;
            var camp = Campaign.Current;
            if (mode == 0 || town == null || camp == null || !On) return;
            try
            {
                bool due = ReferenceEquals(town, _regDue);
                if (result == 0) { if (due) _regDue = null; return; }
                bool creating = !camp.GameStarted;
                long wanted = result;
                long cut = 0;
                if (wanted < 0) result = 0;                      // kasowanie nadwyzki zablokowane w kazdym trybie
                else if (mode == 2 || creating) { cut = wanted; result = 0; }
                else
                {
                    long room = (long)Reserve(town) - town.Gold;
                    if (room < 0) room = 0;
                    if (wanted > room) { cut = wanted - room; result = (int)room; }
                }
                if (!due) return;
                _regDue = null;
                if (creating) return;
                if (wanted < 0) { _dRegDown -= wanted; _dRegDownN++; }
                else
                {
                    if (result > 0) { _dRegUp += result; _dRegUpN++; Add(_mintWin, town, result); }
                    if (cut > 0) { _dRegUpCut += cut; _dRegUpCutN++; }
                }
            }
            catch (Exception e) { Stumble("TownPurse.OnRegulator", e); }
        }

        // ------------------------------------------------------------ zawor: wolane z PopulationLaw.ValveTowns (raz na miasto i dobe)
        /// <summary>
        /// Zawor jednego miasta (R4). `decree` - dekret podatkowy BK (skaluje tylko czesc pana), `due` - renta nalezna od ludzi (dawny pulap, tylko
        /// liczba do linii). Pan dostaje przelew GiveGoldAction (osada -> glowa rodu, jak dotad renty), korona: kasa -> skarbiec (bez zdarzenia gry,
        /// jak danina i zawor zamku). Zapis rent (RentToday, _rentBy, DayTownRent) robi PopulationLaw. Wyjatek - to jedno miasto, licznik potkniec.
        /// </summary>
        internal static bool Valve(Settlement st, float decree, long due, out int lordPay, out int lordBase, out int crown, out long spare)
        {
            lordPay = 0; lordBase = 0; crown = 0; spare = 0;
            try
            {
                var s = Settings.Current;
                var town = st.Town;
                if (due > 0) _vDue += due;
                int reserve = Reserve(town);
                int gold = town.Gold;
                spare = (long)gold - reserve;
                if (spare <= 0) { _vBelow++; return true; }
                var clan = st.OwnerClan;
                var lord = clan != null ? clan.Leader : null;
                if (lord == null || !lord.IsAlive) { _vNoLord++; return true; }   // martwy / brak pana - nic nie schodzi (czeka)
                int pay = CastlePurse.Dues(gold, reserve, ValveSplit.Unit(s.TownRentShare));
                if (BesiegedPay.Holds(st, BesiegedPay.KTownRent, pay, true)) { _vSiege++; return true; }   // oblezone - nic (E5/A4 R22: bramka z kluczem i licznikiem; scalenie porcji A)
                if (pay <= 0) { _vTiny++; return true; }
                // E5/B1 (R4, KL L6): najpierw czesc ludu (TownFolkMerchantProfitShare, 5%) - kasa -> kiesy W20/R10/P70; pan i korona dziela reszte jak A1
                int folk = 0;
                if (TownFolk.Live)
                {
                    int fp = (int)(pay * (double)ValveSplit.Unit(s.TownFolkMerchantProfitShare));
                    if (fp > 0) { folk = TownFolk.EarnValve(st, fp); pay -= folk; _vFolk += folk; }
                    if (pay <= 0) return true;
                }
                var k = clan.Kingdom;
                bool crownTakes = k != null && !k.IsEliminated;
                if (!crownTakes) _vNoCrown++;
                int lb, cr;
                ValveSplit.Split(pay, crownTakes ? LordShare(k) : 1f, out lb, out cr);
                lordBase = lb; _vLordBase += lb;
                if (crownTakes) _vLordBaseK += lb;           // podstawa udzialu korony (prog A1-T8): tylko miasta w krolestwach
                double d = float.IsNaN(decree) || float.IsInfinity(decree) || decree < 0f ? 0.0 : decree;
                long lp = (long)(lb * d);
                if (lp > spare - folk - cr) lp = spare - folk - cr;   // razem nigdy ponad nadwyzke (po czesci ludu E5/B1)
                if (lp < 0) lp = 0;
                bool exempt = lb > 0 && lp == 0;
                if (exempt) _vExempt++;
                if (lp > 0)
                {
                    // przelew kasa miasta -> pan (ta sama akcja gry co dotychczasowe renty; kwota nigdy nie przekracza kasy)
                    GiveGoldAction.ApplyForSettlementToCharacter(st, lord, (int)lp, true);
                    lordPay = (int)lp; _vLord += lp; _vPayers++;
                    if (lord == Hero.MainHero) _vPlayer += lp;
                    Add(_takeWin, town, lp);
                }
                if (cr > 0 && crownTakes)
                {
                    // najpierw kasa, potem skarbiec: skarbiec dostaje dokladnie tyle, ile zeszlo z kasy
                    int had = town.Gold;
                    town.ChangeGold(-cr);
                    int taken = had - town.Gold;
                    if (taken > 0)
                    {
                        k.KingdomBudgetWallet += taken; crown = taken; _vCrown += taken; _vKingdoms.Add(k);
                        long k0; CrownToday.TryGetValue(k, out k0); CrownToday[k] = k0 + taken;
                        int c0; _crownBy.TryGetValue(town, out c0); _crownBy[town] = c0 + taken;
                        Add(_takeWin, town, taken);
                    }
                }
                // "10 miast bez placenia": nadwyzka byla, pan zywy, nie oblezone, pobor > 0 - a nic nie zeszlo i nie zwolnione dekretem (z budowy 0)
                if (lordPay + crown == 0 && !exempt) _vUnpaid++;
                return true;
            }
            catch (Exception e)
            {
                Stumble("TownPurse.Valve(" + (st != null ? st.StringId : "?") + ")", e);
                if (spare > 0 && lordPay + crown == 0) _vUnpaid++;
                return lordPay > 0 || crown > 0;                 // to, co juz zeszlo, PopulationLaw zapisze (przelew sie stal)
            }
        }

        /// <summary>Koniec doby zaworu (po petli PopulationLaw.ValveTowns): udzial korony z pozycji "renty" do wlasnej (M13) i liczby do linii.</summary>
        internal static void ValveDay(long crownSum)
        {
            if (crownSum > 0) MoneyLedger.SplitTownMark(MoneyLedger.MRent, MoneyLedger.MTownCrown, crownSum);   // tylko licznik; wlasny try w srodku
            if (_vFolk > 0) MoneyLedger.SplitTownMark(MoneyLedger.MRent, MoneyLedger.MFolkValve, _vFolk);      // E5/B1: czesc ludu z zaworu - wlasna pozycja M19
            LastFolkValve = _vFolk;
            LastLordPaid = _vLord; LastLordBase = _vLordBase; LastLordBaseK = _vLordBaseK; LastCrownPaid = _vCrown; LastDue = _vDue; LastPlayerPaid = _vPlayer;
            LastPayers = _vPayers; LastKingdoms = _vKingdoms.Count; LastBelow = _vBelow; LastSiege = _vSiege; LastNoLord = _vNoLord;
            LastTiny = _vTiny; LastExempt = _vExempt; LastNoCrown = _vNoCrown; LastUnpaid = _vUnpaid;
        }

        /// <summary>Licznik pobran z kasy miasta poza zaworem (danina, clo z kasy, mennica, Crown Duty, War Tax, Road Tolls) - udzial zysku kupcow (tylko liczba).</summary>
        internal static void NoteTake(Settlement st, int x)
        {
            try
            {
                if (x <= 0 || st == null || !st.IsTown || st.Town == null || !On) return;
                Add(_takeWin, st.Town, x);
            }
            catch (Exception e) { Stumble("TownPurse.NoteTake", e); }
        }

        // ------------------------------------------------------------ raz na dobe, PRZED rentami (po MoneyLedger.BlockOpen)
        /// <summary>
        /// Poczatek naszego rozliczenia doby: zamyka liczniki tickow gry (okno = doba ksiegi pieniadza), okno udzialu zysku kupcow (migawka kas
        /// przed przycieciem), raz na kampanie przycina dar startowy (przed pierwszym zaworem), liczy zaulki i raz na sesje porzuca znaczniki tarcz.
        /// Biegnie zawsze (takze przy wylaczonych rentach) - przy wylaczonym A1 tylko zera.
        /// </summary>
        internal static void BeforeRents()
        {
            CrownToday.Clear(); ClearValve();
            LastRegDown = _dRegDown; LastRegUp = _dRegUp; LastRegUpCut = _dRegUpCut; LastConsBack = _dConsBack; LastConsKept = _dConsKept; LastBkSkipGold = _dBkSkipGold;
            LastRegDownN = _dRegDownN; LastRegUpN = _dRegUpN; LastRegUpCutN = _dRegUpCutN; LastConsN = _dConsN; LastConsHit = _dConsHit; LastConsFolk = _dConsFolk;
            LastBkSkipTowns = _dBkSkipTowns; LastBkSkipCastles = _dBkSkipCastles; LastBuyAsk = _dBuyAsk; LastBuyZero = _dBuyZero;
            LastBkSkipVillages = _dBkSkipVillages; LastBkSkipGoldV = _dBkSkipGoldV;
            LastG24 = _dG24; LastG24Pl = _dG24Pl; LastG24N = _dG24N; LastN3 = _dN3; LastN3N = _dN3N;
            ClearTicks();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            if (!On)
            {
                _goldAtBlock.Clear(); _takeWin.Clear(); _trimWin.Clear(); _mintWin.Clear(); _takeLast.Clear(); _netLast.Clear(); _streak.Clear();
                ClearRing();
                return;
            }
            try { CloseWindow(); } catch (Exception e) { Stumble("TownPurse.CloseWindow", e); }
            try { TrimStartGift(s); } catch (Exception e) { Stumble("TownPurse.TrimStartGift", e); }
            try { Alleys(); } catch (Exception e) { Stumble("TownPurse.Alleys", e); }
            if (!_shieldsDropped)
            {
                _shieldsDropped = true;
                try
                {
                    long heldSum, courtSum; int heldN, courtN;
                    SoldierPay.DropShields(out heldSum, out heldN, out courtSum, out courtN);
                    if (heldN + courtN > 0)
                        Log.Info("TownPurse (E5/A1): tarcze zoldu i dworu porzucone - znaczniki zoldu " + heldSum + " w " + heldN + " miastach, dworu " + courtSum + " w " + courtN
                                 + " (liczby, zlota nie rusza; nic juz nie kasuje kas miast).");
                }
                catch (Exception e) { Stumble("TownPurse.DropShields", e); }
            }
        }

        /// <summary>
        /// Udzial zysku kupcow (Jeff 16:10, wzor E5/0): okno od poprzedniej migawki (poczatek poprzedniego BeforeRents) do teraz. Doplyw netto miasta =
        /// zmiana kasy + pobrane (zawor panu i koronie, podatki z kasy) + dar startowy zdjety w oknie - zloto z niczego w oknie (dosypka trybu 1,
        /// "zakupy" przy wylaczonym R1). Swiat = suma miast z migawka; srednia 28 dob = suma pobranych / suma doplywow (nie srednia ilorazow).
        /// </summary>
        private static void CloseWindow()
        {
            long take = 0, net = 0; int towns = 0;
            _takeLast.Clear(); _netLast.Clear();
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.Town == null) continue;
                try
                {
                    var t = st.Town;
                    long g = t.Gold, g0;
                    if (_goldAtBlock.TryGetValue(t, out g0))
                    {
                        long tk = Get(_takeWin, t), n = (g - g0) + tk + Get(_trimWin, t) - Get(_mintWin, t);
                        take += tk; net += n; towns++;
                        _takeLast[t] = tk; _netLast[t] = n;
                    }
                    _goldAtBlock[t] = g;
                }
                catch (Exception e) { Stumble("TownPurse.CloseWindow(" + st.StringId + ")", e); }
            }
            _takeWin.Clear(); _trimWin.Clear(); _mintWin.Clear();
            LastShareOk = towns > 0;
            if (LastShareOk)
            {
                LastTake = take; LastNet = net;
                _ringTake[_ringHead] = take; _ringNet[_ringHead] = net;
                _ringHead = (_ringHead + 1) % RingDays; if (_ringFilled < RingDays) _ringFilled++;
            }
            long t28 = 0, n28 = 0;
            for (int i = 0; i < _ringFilled; i++) { t28 += _ringTake[i]; n28 += _ringNet[i]; }
            LastTake28 = t28; LastNet28 = n28;
        }

        /// <summary>Zaulki (Jeff 16:10): kasy ponizej zapasu / 1 000 / przy 0 i serie dob z rzedu ponizej zapasu (pamiec sesji - po wczytaniu od 0).</summary>
        private static void Alleys()
        {
            int below = 0, b1000 = 0, zero = 0, max = 0, w7 = 0, w28 = 0; string maxName = null;
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.Town == null) continue;
                try
                {
                    var t = st.Town;
                    int g = t.Gold, r = Reserve(t), k;
                    _streak.TryGetValue(t, out k);
                    if (g < r) { k++; _streak[t] = k; below++; } else { k = 0; _streak.Remove(t); }
                    if (g < 1000) b1000++;
                    if (g <= 0) zero++;
                    if (k >= 7) w7++;
                    if (k >= 28) w28++;
                    if (k > max) { max = k; maxName = st.Name != null ? st.Name.ToString() : st.StringId; }
                }
                catch (Exception e) { Stumble("TownPurse.Alleys(" + st.StringId + ")", e); }
            }
            LastBelowN = below; LastBelow1000 = b1000; LastAtZero = zero; LastStreakMax = max; LastStreak7 = w7; LastStreak28 = w28; LastStreakName = maxName;
        }

        // ------------------------------------------------------------ raz na kampanie: dar startowy (PRZED pierwszym zaworem; wzor CastlePurse.TrimStartGift)
        private static void TrimStartGift(Settings s)
        {
            if (_trimDone || _trimSkip) return;
            bool trimOn = s.TownPurseTrimAtStart;
            if (!trimOn && !double.IsNaN(_liveAge)) return;     // przyciecie wylaczone, doba startu A1 bez przyciecia juz zapisana
            double age = double.NaN;
            try { age = (CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; }
            catch (Exception e) { Stumble("TownPurse.Age", e); }
            if (double.IsNaN(age))
            {
                _trimSkip = true;
                Log.Info("TownPurse (E5/A1): wieku kampanii nie da sie odczytac - dar startowy w kasach miast bez przyciecia (flaga nie zapisana).");
                return;
            }
            if (!trimOn)
            {
                // A1 rusza bez przyciecia - od tej doby regulator nie kasuje daru, bierze go zawor; zapisujemy wiek (flaga "on:<wiek>")
                _liveAge = Math.Max(0.0, age);
                Log.Info("TownPurse (E5/A1): przyciecie daru startowego WYLACZONE (MCM Town Purse Trim At Start) - kasy miast czynne od "
                         + age.ToString("0.0", CultureInfo.InvariantCulture) + ". doby kampanii bez przyciecia: dar ponad zapas kupcow pobierze zawor (panom i skarbcom krolestw)."
                         + " Wlaczone pozniej, przyciecie zdejmie tylko to, czego z daru nie wzial jeszcze regulator ani zawor (flaga on:wiek w sejwie).");
                return;
            }
            _trimDone = true;                                    // flaga PRZED robota: przerwanego przyciecia nie powtarzamy (drugie zdjeloby prawdziwy doplyw)
            if (age < -0.01)
            {
                Log.Info("TownPurse (E5/A1): kampania ma wiek ujemny (" + age.ToString("0.0", CultureInfo.InvariantCulture)
                         + " dni - data startu z innego kalendarza) - dar startowy w kasach miast BEZ przyciecia (flaga zapisana). Nadwyzke ponad zapas pobierze zawor - panom i skarbcom krolestw.");
                return;
            }
            // mloda kampania: dar nieruszony (regulator przy tworzeniu swiata = 0, od startu A1 nie kasuje) - caly; stary zapis wczytany pierwszy raz:
            // tylko to, co z daru zostawil regulator gry (cwierc nadwyzki dziennie); po dobach A1 bez przyciecia - spadek od zaworu (TownRentShare dziennie)
            double regAge = double.IsNaN(_liveAge) ? age : Math.Min(age, _liveAge);
            double regDays = regAge <= FreshAgeDays ? 0.0 : regAge;
            double valveDays = double.IsNaN(_liveAge) ? 0.0 : Math.Max(0.0, age - _liveAge);
            float valveShare = ValveLive ? ValveSplit.Unit(s.TownRentShare) : 0f;
            double left = (regDays > 0.0 ? Math.Pow(RegulatorKeeps, regDays) : 1.0) * (valveDays > 0.0 ? Math.Pow(1.0 - valveShare, valveDays) : 1.0);
            long before = 0, after = 0, cutSum = 0, reserveSum = 0; int towns = 0, cutN = 0, maxCut = 0; string maxName = null;
            // E5/B1 (R5, plan rozdz. 6.1): mloda kampania - najpierw zapasy startowe kies ludu (W 10, R 21, P 30 dni koszyka KL przy indeksie 1.0) i wsi tego targu
            // (do 15 dni zakupow) z czesci daru, ktora A1 i tak zdejmie; migawka M20 (transfer), potem przyciecie reszty daru (M12 - ujscie w nicosc)
            Dictionary<Town, int> giftLeft = null;
            if (regDays <= 0.0 && valveDays <= 0.0 && TownFolk.SeedTownsDue)
            {
                giftLeft = new Dictionary<Town, int>();
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsTown || st.Town == null) continue;
                    try
                    {
                        var town = st.Town;
                        int cut0 = CastlePurse.StartGiftCut(town.Gold, Reserve(town), Prosperity(town), left);
                        int used = TownFolk.SeedTown(st, cut0);
                        used += TownFolk.SeedVillages(st, cut0 - used);
                        giftLeft[town] = Math.Max(0, cut0 - used);
                    }
                    catch (Exception e) { Stumble("TownPurse.TrimStartGift(R5 " + st.StringId + ")", e); }
                }
                MoneyLedger.Mark(MoneyLedger.MFolkStart);         // tylko licznik; wlasny try w srodku
                TownFolk.SeedDone(true);
            }
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.Town == null) continue;
                try
                {
                    var town = st.Town;
                    int gold = town.Gold, reserve = Reserve(town);
                    towns++; before += gold; reserveSum += reserve;
                    int cut;
                    if (giftLeft != null) { if (!giftLeft.TryGetValue(town, out cut)) cut = 0; cut = Math.Min(cut, Math.Max(0, gold - reserve)); }   // E5/B1: reszta daru po zapasach ludu
                    else cut = CastlePurse.StartGiftCut(gold, reserve, Prosperity(town), left);   // ten sam dar co w zamku: gra 20 000 + BK 40 x dobrobyt
                    if (cut > 0)
                    {
                        int had = town.Gold;
                        town.ChangeGold(-cut);
                        int gone = had - town.Gold;
                        cutSum += gone; cutN++;
                        Add(_trimWin, town, gone);
                        if (gone > maxCut) { maxCut = gone; maxName = st.Name != null ? st.Name.ToString() : st.StringId; }
                    }
                    after += town.Gold;
                }
                catch (Exception e) { Stumble("TownPurse.TrimStartGift(" + st.StringId + ")", e); }
            }
            // ksiega pieniadza: od BlockOpen nic innego nie ruszalo kas - migawka M12 = dokladnie dar (ujscie w nicosc, raz)
            MoneyLedger.Mark(MoneyLedger.MTownTrim);             // tylko licznik; wlasny try w srodku
            LastTrim = cutSum;
            Log.Info("TownPurse (E5/A1): " + (valveDays > 0.0 ? "przyciecie wlaczone w " + age.ToString("0.0", CultureInfo.InvariantCulture) + ". dobie kampanii, kasy miast czynne bez przyciecia od "
                                                          + _liveAge.ToString("0.0", CultureInfo.InvariantCulture) + ". doby (regulator i zawor zostawili ok. " + (left * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "% daru)"
                                             : regDays > 0.0 ? "zapis z " + age.ToString("0.0", CultureInfo.InvariantCulture) + ". doby kampanii wczytany pierwszy raz z ta zmiana (regulator gry zostawil ok. "
                                                        + (left * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "% daru)"
                                                      : "poczatek kampanii (doba " + age.ToString("0.00", CultureInfo.InvariantCulture) + ")")
                     + " - dar startowy w kasach miast (gra " + VanillaStartGold + " + BK 40 x dobrobyt) przyciety do zapasu kupcow: miast " + towns + ", kasy " + before + " -> " + after
                     + " (zdjeto " + cutSum + " w " + cutN + " miastach, najwiecej " + maxCut + (maxName != null ? " - " + maxName : "") + "; zapas razem " + reserveSum
                     + "). To samo zloto regulator gry kasowal dotad w ok. 12 dob; zostawione splyneloby zaworem do panow i koron. Flaga zapisze sie w sejwie (arm_townpurse).");
        }

        // ------------------------------------------------------------ R6: gorna galaz BK HandleMarketGold
        /// <summary>Limit kasy lenna wedlug BK (ponad nim BK kasuje 1% dziennie): z modelu BK, a gdy go nie ma - ze stalych z jego kodu.</summary>
        internal static int BkLimit(Settlement st)
        {
            try
            {
                if (!_bkResolved)
                {
                    _bkResolved = true;
                    var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                    _bkInstance = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                    _bkModel = cfgT != null ? AccessTools.Property(cfgT, "EconomyModel") : null;
                }
                if (_bkInstance != null && _bkModel != null)
                {
                    var cfg = _bkInstance.GetValue(null, null);
                    var model = cfg != null ? _bkModel.GetValue(cfg, null) : null;
                    if (model != null)
                    {
                        if (_bkLimit == null || _bkLimit.DeclaringType == null || !_bkLimit.DeclaringType.IsInstanceOfType(model))
                            _bkLimit = AccessTools.Method(model.GetType(), "GetSettlementMarketGoldLimit", new[] { typeof(Settlement) });
                        if (_bkLimit != null) return Convert.ToInt32(_bkLimit.Invoke(model, new object[] { st }));
                    }
                }
            }
            catch { _bkLimit = null; }      // model BK niedostepny (inna wersja, kampania w budowie) - liczymy ze stalych
            if (st != null && st.IsVillage && st.Village != null)                  // E5/A1 R6 wsie: BK GetSettlementMarketGoldLimit - 10 000 + 3 x hearth
            {
                double lv = BkLimitVillageBase + st.Village.Hearth * BkLimitVillageHearth;
                return lv >= int.MaxValue ? int.MaxValue : (int)lv;
            }
            var town = st != null ? st.Town : null;
            if (town == null) return int.MaxValue;
            double lim = BkLimitBase + (double)Prosperity(town) * (st.IsCastle ? BkLimitCastle : BkLimitTown);
            return lim >= int.MaxValue ? int.MaxValue : (int)lim;
        }

        /// <summary>
        /// R6: prefiks bool Priority.Last na BK BKSettlementBehavior.HandleMarketGold(Settlement) - po prefiksie okna O18 ksiegi obiegu (okno mierzy
        /// przed nami, jego finalizer widzi 0 zmiany). Gdy kasa miasta (zamku - tylko przy CastlePurseEnabled) stoi ponad limitem BK, oryginal nie
        /// biegnie (jego gorna galaz: -1% kasy, potem przez blad znaku +0.5% kasy i -0.5% kiesy notabla - razem 1% w nicosc). W kazdym innym
        /// przypadku oryginal jak dotad (dolna galaz: notabl z >= 30 000 doplaca 1 000 pustej kasie - przelew). Wyjatek - oryginal biegnie.
        /// E5/A1 R6 dla wsi (klucz TownPurseNoBkSkimVillages): to samo dla kiesy wsi ponad limitem BK wsi (10 000 + 3 x hearth) - E5/B1 zostawia wsi 65% utargu.
        /// </summary>
        public static bool BkSkimPrefix(Settlement __0)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || __0 == null || Campaign.Current == null || !On) return true;
                if (__0.IsVillage)
                {
                    // E5/A1 R6 dla wsi (klucz TownPurseNoBkSkimVillages): gorna galaz BK kasuje 1% kiesy wsi ponad limitem (0.5% kiesy wsi i 0.5% od notabla) w nicosc;
                    // dolna galaz (notabl z >= 30 000 doplaca 1 000 pustej wsi - przelew) zostaje jak dotad
                    if (!s.TownPurseNoBkSkimVillages) return true;
                    var vc = __0.SettlementComponent;
                    if (vc == null) return true;
                    int vg = vc.Gold;
                    if (vg <= BkLimit(__0)) return true;
                    _dBkSkipVillages++;
                    _dBkSkipGoldV += (long)(vg * 0.01f);
                    return false;
                }
                if (!s.TownPurseNoBkSkim || __0.Town == null) return true;
                bool castle = __0.IsCastle;
                if (castle ? !s.CastlePurseEnabled : !__0.IsTown) return true;
                var comp = __0.SettlementComponent;
                if (comp == null) return true;
                int gold = comp.Gold;
                if (gold <= BkLimit(__0)) return true;
                if (castle) _dBkSkipCastles++; else _dBkSkipTowns++;
                _dBkSkipGold += (long)(gold * 0.01f);
                return false;
            }
            catch (Exception e) { Stumble("TownPurse.BkSkimPrefix", e); return true; }
        }

        /// <summary>Menu glowne, po CastlePurse.ApplyAll: latka R6 na BK i linia startowa (stan klucza tylko informacyjnie - MCM moze go zmienic;
        /// stan czynny podaje dopisek dzienny w "Kasy miast (169c)").</summary>
        internal static void ApplyAll(Harmony h)
        {
            var miss = new List<string>();
            string bk = "BRAK";
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Behaviours.BKSettlementBehavior");
                var m = t != null ? AccessTools.Method(t, "HandleMarketGold", new[] { typeof(Settlement) }) : null;
                if (m != null && !_bkHooked)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(TownPurse), nameof(BkSkimPrefix)) { priority = Priority.Last });
                    _bkHooked = true;
                }
                if (_bkHooked) bk = "wpiete";
                else miss.Add("BK HandleMarketGold (" + (t == null ? "brak typu BKSettlementBehavior" : "brak metody") + ")");
            }
            catch (Exception e) { miss.Add("BK HandleMarketGold (" + e.Message + ")"); Log.Error("TownPurse.ApplyAll(BK)", e); }
            // pomiar G24 (audyt 11.10): prefiks Last na GiveGoldAction.ApplyInternal - po prefiksach zmieniajacych strone (RealPayer, NotableGold - Low), tylko odczyt
            try
            {
                var give = AccessTools.Method(typeof(GiveGoldAction), "ApplyInternal");
                if (give != null && !_g24Hooked)
                {
                    h.Patch(give, prefix: new HarmonyMethod(typeof(TownPurse), nameof(G24Prefix)) { priority = Priority.Last });
                    _g24Hooked = true;
                }
                if (!_g24Hooked) miss.Add("pomiar G24 (GiveGoldAction.ApplyInternal)");
            }
            catch (Exception e) { miss.Add("pomiar G24 (" + e.Message + ")"); Log.Error("TownPurse.ApplyAll(G24)", e); }
            if (CastlePurse.HookedModels <= 0) miss.Add("regulator kasy (CastlePurse 110)");
            if (!CastlePurse.HookedConsumption) miss.Add("konsumpcja (CastlePurse 110)");
            var s = Settings.Current;
            var inv = CultureInfo.InvariantCulture;
            Log.Info("TownPurse (E5/A1): kasy miast jako prawdziwy pieniadz - klucz Town Purse Regulator " + (s != null ? s.TownPurseRegulator.ToString(inv) : "?") + " (przy starcie)"
                     + " | latki: regulator i \"zakupy\" przez CastlePurse (110) w " + CastlePurse.HookedModels + " modelach, konsumpcja: " + (CastlePurse.HookedConsumption ? "tak" : "BRAK")
                     + ", BK HandleMarketGold - prefiks Last: " + bk + "; BRAK: " + (miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-")
                     + " | zapas kupcow " + (s != null ? s.TownPurseFloorGold.ToString(inv) + " + " + s.TownPurseFloorPerProsperity.ToString("0.##", inv) : "?") + " x dobrobyt (nie mniej niz cel gry "
                     + GameTargetGold + " + " + GameTargetPerProsperity.ToString("0", inv) + " x dobrobyt), zawor " + (s != null ? (ValveSplit.Unit(s.TownRentShare) * 100f).ToString("0.#", inv) : "?")
                     + "% nadwyzki, panu " + (s != null ? (ValveSplit.Unit(s.TownRentLordShare) * 100f).ToString("0.#", inv) : "?")
                     + "% (z wyrownaniem dla panow zamkow E5/B3 przy czynnej kiesie ludu " + (s != null ? (ValveSplit.Unit(s.TownRentLordShareWithFolk) * 100f).ToString("0.#", inv) : "?")
                     + "%, wylacznik Castle Lords Even Up " + (s != null && s.CastleLordsEvenUp ? "tak" : "nie") + ") i reszta skarbcowi krolestwa"
                     + ", dar startowy przycinany: " + (s != null && s.TownPurseTrimAtStart ? "tak" : "nie")
                     + ", zakupy dla nas tylko z nadwyzki: " + (s != null && s.TownPaysFromSurplus ? "tak" : "nie")
                     + ", BK rynek pominiety: " + (s != null && s.TownPurseNoBkSkim ? "tak" : "nie") + " (zamki tylko przy Castle Purse Enabled), wsie: " + (s != null && s.TownPurseNoBkSkimVillages ? "tak" : "nie") + " (E5/A1 R6 wsie).");
        }

        // ------------------------------------------------------------ pomiar G24 / N3 (audyt 11.10) - tylko liczniki
        private static bool MeasureOn { get { var s = Settings.Current; var c = Campaign.Current; return s != null && s.LogEnabled && c != null && c.GameStarted; } }

        /// <summary>G24: prefiks Last na GiveGoldAction.ApplyInternal - osada (odbiorca z ujemna kwota = dajacy) placi bohaterowi wiecej, niz ma w kasie. Tylko odczyt.</summary>
        public static void G24Prefix(Hero __0, PartyBase __1, PartyBase __3, int __4)
        {
            try
            {
                if (__4 >= 0 || __0 == null || __1 != null || __3 == null || !__3.IsSettlement || !MeasureOn) return;
                var sc = __3.Settlement != null ? __3.Settlement.SettlementComponent : null;
                if (sc == null) return;
                long need = -(long)__4, have = Math.Max(0, sc.Gold);
                if (need <= have) return;
                _dG24 += need - have; _dG24N++;
                if (ReferenceEquals(__0, Hero.MainHero)) _dG24Pl += need - have;
            }
            catch (Exception e) { Stumble("TownPurse.G24Prefix", e); }
        }

        /// <summary>N3: okno O35 (CW.WorkshopOutPost) - wyrob warsztatu BK, kasa osady skonczyla na 0, a warsztat dostal wiecej, niz kasa stracila. Tylko licznik.</summary>
        internal static void NoteN3(long shortfall)
        {
            if (shortfall <= 0 || !MeasureOn) return;
            _dN3 += shortfall; _dN3N++;
        }

        /// <summary>Dopisek linii "Kasy miast (169c)" (kazdy tryb A1): pomiar G24 / N3 tej doby.</summary>
        private static string G24Part()
        {
            return " | pomiar G24/N3 (audyt 11.10, tylko licznik; ozyja przy trybie 2): osada zaplacila bohaterowi ponad kase " + LastG24 + " w " + LastG24N + " zdarzeniach (gracz " + LastG24Pl
                   + "), warsztaty BK - niedoplata pustej kasy " + (CirculationWindows.Wired[CirculationWindows.WWorkshopOut] ? LastN3 + " w " + LastN3N + " wyrobach" : "- (okno O35 niewpiete)")
                   + (_g24Hooked ? "" : " [G24: latka niewpieta]");
        }

        // ------------------------------------------------------------ linia "Kasy miast (169c)" i kasy-miast.csv (wolane z Measure169c.TownsLine)
        private static string Pct(long take, long net)
        {
            if (net <= 0) return "-";
            return (100.0 * take / net).ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>Dopisek A1 do linii "Kasy miast (169c)" (zamiast "do korony -"). Zeruje licznik potkniec (jedna linia na dobe).</summary>
        internal static string LineSegment()
        {
            int stum = _stumbles; _stumbles = 0;
            var s = Settings.Current;
            int mode = Mode;
            if (s == null) return "";
            if (mode == 0) return " | E5/A1: WYLACZONE (Town Purse Regulator 0) - regulator gry, \"zakupy\" z niczego, renta z pulapem i tarcze jak dotad" + G24Part();
            if (!On) return " | E5/A1: NIECZYNNE - Town Purse Regulator " + mode + ", ale latki CastlePurse (110) nie sa wpiete (modele " + CastlePurse.HookedModels
                            + ", konsumpcja " + (CastlePurse.HookedConsumption ? "tak" : "BRAK") + ") - kasy miast jak dotad" + G24Part();
            long reserve = 0, spare = 0;
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.Town == null) continue;
                try { var t = st.Town; int r = Reserve(t); reserve += r; long v = (long)t.Gold - r; if (v > 0) spare += v; }
                catch (Exception e) { Stumble("TownPurse.LineSegment(" + st.StringId + ")", e); }
            }
            int unsold, unsoldVisits; MoneyLedger.TownUnsoldToday(out unsold, out unsoldVisits);
            var sb = new StringBuilder(1536);
            sb.Append(" | E5/A1: tryb regulatora ").Append(mode);
            if (ValveLive)
                sb.Append(" | zawor: zeszlo ").Append(LastLordPaid + LastCrownPaid).Append(" = panom ").Append(LastLordPaid).Append(" (przed dekretem ").Append(LastLordBase)
                  .Append("; w krolestwach przed dekretem ").Append(LastLordBaseK).Append("; z ").Append(LastPayers).Append(" miast; rod gracza ").Append(LastPlayerPaid).Append(") + koronie ").Append(LastCrownPaid).Append(" (").Append(LastKingdoms).Append(" krolestw)")
                  .Append("; renta nalezna od ludzi (dawny pulap, tylko liczba) ").Append(LastDue)
                  .Append("; bez poboru: kasa nie ponad zapasem ").Append(LastBelow).Append(", oblezone ").Append(LastSiege).Append(", bez zywego pana ").Append(LastNoLord)
                  .Append(", nadwyzka za mala ").Append(LastTiny).Append(", zwolnione dekretem (tylko korona) ").Append(LastExempt)
                  .Append("; bez krolestwa (calosc panu) ").Append(LastNoCrown).Append("; miast z nadwyzka bez zaworu (10 miast) ").Append(LastUnpaid);
            else
                sb.Append(" | zawor: NIE DZIALA (").Append(!PopulationLaw.On ? "Population Rent Enabled wylaczone" : "Town Rent Share 0").Append(") - kasy miast tylko rosna");
            sb.Append(" | regulator: kasowanie zablokowane ").Append(LastRegDown).Append(" (").Append(LastRegDownN).Append(" tickow), dosypka do zapasu (tryb 1) ").Append(LastRegUp)
              .Append(" (").Append(LastRegUpN).Append(" miast), dosypka zablokowana (tryb 2 albo ponad zapas) ").Append(LastRegUpCut).Append(" (").Append(LastRegUpCutN).Append(" tickow)");
            if (NoMint)
                sb.Append(" | \"zakupy\" mieszczan: zloto z niczego cofniete ").Append(LastConsBack).Append(" (w ").Append(LastConsHit).Append(" z ").Append(LastConsN).Append(" tickow; towar zjedzony jak dotad)")
                  .Append(TownFolk.Live || LastConsFolk > 0 ? "; lud zaplacil kasom (E5/B1) " + LastConsFolk + ", czesc ludu z zaworu " + LastFolkValve : "");
            else
                sb.Append(" | \"zakupy\" mieszczan: zloto z niczego ZOSTAJE (MCM Town Folk Buy Without Minting wylaczone) ").Append(LastConsKept).Append(" (").Append(LastConsN).Append(" tickow)");
            sb.Append(" | zapas kupcow ").Append(reserve).Append(", ponad zapasem ").Append(spare)
              .Append(" | zaulki: kas ponizej zapasu ").Append(LastBelowN).Append(", ponizej 1000 ").Append(LastBelow1000).Append(", przy 0 ").Append(LastAtZero)
              .Append("; najdluzej ponizej zapasu ").Append(LastStreakMax).Append(" dob z rzedu").Append(LastStreakName != null ? " (" + LastStreakName + ")" : "")
              .Append(", miast >= 7 dob ").Append(LastStreak7).Append(", >= 28 dob ").Append(LastStreak28).Append(" (serie od wczytania)")
              .Append("; towar wsi niesprzedany w miastach (po wizycie taboru - kasa kupuje tyle, ile ma) ").Append(unsold).Append(" szt. w ").Append(unsoldVisits).Append(" wizytach");
            sb.Append(" | udzial zysku kupcow zabierany przez pana i korone: dzis ");
            if (LastShareOk) sb.Append(Pct(LastTake, LastNet)).Append("% (pobrano ").Append(LastTake).Append(" z doplywu netto ").Append(LastNet).Append("; okno od poprzedniego bloku)");
            else sb.Append("-% (pierwsza doba sesji - brak migawki)");
            sb.Append(", srednia 28 dob ").Append(Pct(LastTake28, LastNet28)).Append("% (").Append(_ringFilled).Append(" dob; odniesienie historyczne ok. 10%, INFO)");
            sb.Append(" | zakupy dla nas z nadwyzki: ").Append(s.TownPaysFromSurplus ? "zapytan " + LastBuyAsk + ", przy zapasie (0) " + LastBuyZero : "WYLACZONE (MCM Town Pays From Surplus) - miasto placi cala kasa")
              .Append(" | BK rynek osady: ").Append(s.TownPurseNoBkSkim ? (_bkHooked ? "gorna galaz pominieta w " + LastBkSkipTowns + " miastach i " + LastBkSkipCastles + " zamkach (BK skasowalby ok. " + LastBkSkipGold + ")" : "BRAK latki")
                                                                 : "gorna galaz zostaje (MCM Town Purse No Bk Skim wylaczone)")
              .Append("; wsie (E5/A1 R6): ").Append(s.TownPurseNoBkSkimVillages ? (_bkHooked ? "gorna galaz pominieta w " + LastBkSkipVillages + " wsiach (BK skasowalby ok. " + LastBkSkipGoldV + ")" : "BRAK latki")
                                                                 : "gorna galaz zostaje (MCM Town Purse No Bk Skim Villages wylaczone)")
              .Append(" | dar startowy dzis ").Append(LastTrim)
              .Append(G24Part())
              .Append(" | potkniecia ").Append(stum);
            return sb.ToString();
        }

        /// <summary>Scalenie porcji A (A2 "Przy scaleniu"): udzial korony z zaworu tego miasta dzis (poza RentOf - CourtFull.OwnBack zdejmuje go z zapasu
        /// "wlasnego" jak czesc korony zamku 114). Wazne od zaworu do BeforeRents nastepnej doby. Tylko odczyt.</summary>
        internal static int CrownOf(Settlement st)
        {
            int c; return st != null && st.Town != null && _crownBy.TryGetValue(st.Town, out c) ? Math.Max(0, c) : 0;
        }

        internal const string CsvColumns = ";zapas_kupcow;do_korony;pobrane;doplyw_netto;dni_ponizej_zapasu";

        /// <summary>Kolumny A1 na koncu wiersza kasy-miast.csv (miasto po miescie): zapas, udzial korony dzis, pobrane i doplyw netto z okna, doby ponizej zapasu.</summary>
        internal static string CsvOf(Town t)
        {
            if (t == null) return ";;;;;";
            int c; _crownBy.TryGetValue(t, out c);
            int k; _streak.TryGetValue(t, out k);
            return ";" + (On ? Reserve(t).ToString(CultureInfo.InvariantCulture) : "") + ";" + c + ";" + Get(_takeLast, t) + ";" + Get(_netLast, t) + ";" + k;
        }
    }
}
