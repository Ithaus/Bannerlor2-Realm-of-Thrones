using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// PACZKA 169 - KSIEGA OBIEGU (sam log, docs/paczki/169-ksiega-obiegu.md). Niczego nie zmienia w grze: zadna latka nie zwraca false,
    /// nie zmienia __result ani parametrow ref, nie rusza zlota, towaru ani ludzi. Okna pomiaru wokol metod gry, BK, BEE i NavalDLC
    /// nazywaja przyczyny zmian zlota swiata, ktore dotad siedzialy w reszcie ksiegi pieniadza (MoneyLedger):
    ///  - okna flagowe (F): prefiks First ustawia przyczyne, istniejace zdarzenie HeroOrPartyTradedGold (MoneyLedger.OnGoldTraded) rozbija
    ///    "z niczego / w nicosc" wedlug tej przyczyny (rozbicie "w tym" - suma czesci + "inne" = calosc, nic nie liczy sie drugi raz);
    ///  - migawki (M): stan 1-6 kies przed metoda i po niej (finalizer biegnie zawsze, takze po wyjatku) - zmiany BEZ zdarzenia
    ///    dostaja wlasna pozycje bilansu (Z* zrodla, U* ujscia, N* netto, K1 korekta zrodel);
    ///  - nasluchy zdarzen gry (L): partie i krolestwa znikaja, bohaterowie wychodza ze swiata i wracaja;
    ///  - linie czynnego modelu finansow rodu (ML): trybut, najemnicy, dlug, rada, podatek BK - tylko informacja rodow.
    /// Probki swiata (2.4): najwyzej ProbesPerDay (12; 169b - bylo 8, okien przybylo 8) na dobe, kazde okno raz na 3 doby - zmiana CALEGO swiata w oknie wobec pozycji nazwanych
    /// w tej samej chwili; to jedyna prawdziwa kontrola okien (tozsamosc linii jest z budowy).
    /// Wszystko O(1) na wywolanie, bez LINQ i bez alokacji w goracych sciezkach; pelne przeglady swiata tylko w probkach.
    /// Kazde cialo w try/catch - okno nigdy nie rzuca do gry; bledy liczone (Stumbles), pierwszy na miejsce do logu; okna nie gasimy.
    /// </summary>
    internal static class CirculationWindows
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.CirculationLedgerEnabled && s.LogEnabled; } }
        internal static bool ProbeOn { get { var s = Settings.Current; return s != null && s.CirculationLedgerEnabled && s.LogEnabled && s.CirculationProbeEnabled; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }
        private static int _main = -1;                     // watek glowny (ApplyAll)
        private static Harmony _harmony;

        // klasy osad - te same wartosci co MoneyLedger.CTown/CCastle/CVill
        internal const int CTown = 0, CCastle = 1, CVill = 2, Classes = 3;

        // ------------------------------------------------------------ okna (kontrolka 6.8 - kolejnosc wydruku)
        internal const int WNotableIncome = 0, WCaravanWage = 1, WNotableBand = 2, WNotableNew = 3, WUpgrade = 4, WRecruit = 5, WShips = 6, WShipsOther = 7,
                           WPrisoners = 8, WSiege = 9, WTournament = 10, WRaid = 11, WBee = 12, WBattle = 13, WSell = 14, WLoot = 15, WCaravanNew = 16,
                           WCaravanCap = 17, WMarket = 18, WPorts = 19, WMines = 20, WConvoys = 21, WSlaves = 22, WVillageMarket = 23, WTitles = 24,
                           WKnight = 25, WCourt = 26, WDilemma = 27, WMoveCourt = 28, WRecruitKnight = 29, WImperial = 30, WBuildMat = 31, WLordBuy = 32,
                           WTolls = 33, WWorkshopOut = 34, WWorkshopIn = 35, WHeroState = 36, WPartyGone = 37, WKingdomGone = 38, WModelLines = 39,
                           // 169b: odszkodowania wojenne Diplomacy, majatki BK, BetterEconomy (renta majatkow, wyplaty skarbcow, karawany)
                           WReparations = 40, WBkEstates = 41, WBeeEstates = 42, WBeePayout = 43, WBeeEscort = 44, WBeePromote = 45, WBeeDeliver = 46,
                           WBeeDanger = 47,
                           Windows = 48;
        internal static readonly string[] WinName =
        {
            "notable-dochod", "karawany-notabli", "pasmo-notabli", "nowy-notabl", "awanse", "werbunek", "statki", "statki-inne",
            "jency", "oblezenie", "turniej", "rabunek-BK", "BEE", "bitwy", "prowizja", "lup-z-cial", "nowe-karawany",
            "kapital-karawan", "rynek-osady", "porty", "kopalnie", "konwoje", "niewolnicy", "rynek-wsi", "tytuly",
            "pasowanie", "dwor", "dylematy", "przeniesienie-dworu", "rycerz-BKROT", "donatywa", "material-budowy", "kupno-BK",
            "myto", "warsztaty-wyrob", "warsztaty-wsad", "stan-bohatera", "partie-znikaja", "krolestwa", "linie-modelu",
            "odszkodowania", "majatki-BK", "renta-majatkow-BEE", "wyplaty-BEE", "BEE-eskorta", "BEE-awanse", "BEE-dostawy", "BEE-drogi"
        };

        // ------------------------------------------------------------ przyczyny rozbicia from/to (okna flagowe, 3.2); 0 = brak flagi
        internal const int KNotableBand = 1, KNotableNew = 2, KNotableIncome = 3, KUpgrade = 4, KRecruitTavern = 5, KRecruitNotable = 6, KRecruitMap = 7,
                           KShips = 8, KShipsOther = 9, KPrisonersParty = 10, KPrisonersFort = 11, KSiege = 12, KTournament = 13, KRaidCapture = 14,
                           KBeeContribute = 15, KBeeCamp = 16, KBeeInvest = 17, KBeeMarket = 18, KBattle = 19,
                           KReparations = 20, KBkEstates = 21, KBeeEstateRent = 22, KBeePayout = 23, Kinds = 24;   // 169b
        internal static readonly string[] KindIn =
        {
            "", "pasmo notabli", "nowi notable", "wyplata dochodu notablom", "awanse", "werbunek najemnikow", "werbunek ochotnikow od notabli",
            "ochotnicy z mapy", "statki: sprzedaz osadom i premie zarzadcow", "statki NavalDLC inne", "jency sprzedani przez partie",
            "jency do kas twierdz", "lup z oblezen", "nagrody turniejow", "jency z rabunkow BK", "BetterEconomy: skarbce zamkow",
            "BetterEconomy: obozy", "BetterEconomy: inwestycje we wsie", "BetterEconomy: dostep do targu", "bitwy",
            "odszkodowania wojenne (Diplomacy) dla glowy krolestwa, najemnikow i gracza", "majatki BK - dochod z produkcji dla wlascicieli",
            "BetterEconomy: renta majatkow dla wlascicieli", "BetterEconomy: wyplaty ze skarbcow miast i z umow handlowych"
        };
        internal static readonly string[] KindOut =
        {
            "", "pasmo notabli", "nowi notable", "wyplata dochodu notablom", "awanse", "werbunek najemnikow", "werbunek ochotnikow od notabli",
            "ochotnicy z mapy", "statki: kupno od osad", "statki NavalDLC inne", "jency sprzedani przez partie",
            "jency do kas twierdz", "lup z oblezen", "nagrody turniejow", "jency z rabunkow BK", "skarbce zamkow",
            "obozy", "inwestycje we wsie", "dostep do targu", "bitwy",
            "odszkodowania wojenne (Diplomacy)", "majatki BK", "BetterEconomy: renta majatkow", "BetterEconomy: wyplaty skarbcow"
        };
        internal static readonly int[] KindWin =
        {
            -1, WNotableBand, WNotableNew, WNotableIncome, WUpgrade, WRecruit, WRecruit, WRecruit, WShips, WShipsOther, WPrisoners, WPrisoners,
            WSiege, WTournament, WRaid, WBee, WBee, WBee, WBee, WBattle, WReparations, WBkEstates, WBeeEstates, WBeePayout
        };

        // ------------------------------------------------------------ pozycje Sum[] (3.4): Z* zrodlo +, U* ujscie +, N* zmiana swiata ze znakiem,
        // K1 przelew zgloszony jako z niczego +, I* informacja (nie wchodzi do bilansu)
        internal const int ZLoot = 0, ZCaravanCapitalNotable = 1, ZCaravanCapitalLord = 2, ICaravanNewN = 3, ICaravanCapAfter = 4, ICaravanNoCap = 5,
                           ZSlaves = 6, ZHeroesBack = 7, ZTolls = 8, ITollN = 9, K1NotableAssets = 10,
                           UCaravanFromCaravan = 11, UCaravanFromNotable = 12, ICaravanDue = 13, ICaravanTopup = 14, ICaravanShort = 15, ICaravanN = 16,
                           ICaravanNotables = 17, ICaravanShortN = 18,
                           UCommissionVillage = 19, UCommissionTownLost = 20, ICommissionToCounter = 21, UPorts = 22, IPortsN = 23, UMinesNet = 24,
                           IMinesBack = 25, UConvoys = 26, IConvoysN = 27,
                           ULordClaim = 28, ULordCreate = 29, ULordRevoke = 30, ULordUsurp = 31, ULordKnightClan = 32, ULordCourt = 33, ULordDilemma = 34,
                           ULordMoveCourt = 35, ULordRecruitKnight = 36, ULordImperial = 37, ULordBuildMaterial = 38, ULordCaravanBuy = 39, ICaravanBuyN = 40,
                           UHeroesLeft = 41, IHeroesLeftNotableN = 42, IHeroesLeftNotableGold = 43, IHeroesLeftLordN = 44, IHeroesLeftLordGold = 45,
                           IHeroesLeftOtherN = 46, IHeroesLeftOtherGold = 47,
                           UPartyCaravan = 48, UPartyVillager = 49, UPartyBandit = 50, UPartyGarrison = 51, UPartyOther = 52, IPartyGoneN = 53,
                           NMarketKasa = 54, NMarketNotables = 55, NVillageMarketVillage = 56, NVillageMarketTown = 57, NWorkshopOutKasa = 58,
                           NWorkshopOutCap = 59, NWorkshopInKasa = 60, NWorkshopInCap = 61, NWorkshopBuyCap = 62, IWorkshopBuyN = 63,
                           NBattleNoEvent = 64, NDeadTransfers = 65, NKingdomGone = 66, INotableIncomeN = 67,
                           ICaravanNewNotableN = 68, IPartyGoneCaravanN = 69, IPartyGoneVillagerN = 70, IPartyGoneBanditN = 71, IPartyGoneGarrisonN = 72,
                           IPartyGoneOtherN = 73,
                           // 169b: odszkodowania Diplomacy (skarbce bez zdarzenia - netto swiata; informacje: do skarbcow, ze skarbcow, dlug trybutu placacych)
                           NReparations = 74, IReparationsIn = 75, IReparationsOut = 76, IReparationsDebt = 77, IReparationsN = 78,
                           // 169b: karawany BetterEconomy (kasa karawany bez zdarzenia): eskorta, awanse zalogi, straty na drogach (ujscia), dostawy kontraktow (netto)
                           UBeeEscort = 79, UBeePromote = 80, NBeeDeliver = 81, UBeeDanger = 82, IBeeEscortN = 83, IBeeDeliverN = 84, IBeeDangerN = 85,
                           // 169b: nasze moduly (ChangeHeroGold bez zdarzenia) z bohaterem poza swiatem - czesc N5 (informacja)
                           IDeadDirectN = 86, IDeadDirect = 87,
                           // 169b: z majatkow (BK, BEE) do notabli - informacja do porownania z licznikiem "do notabli z niczego" (WorkshopTrade)
                           IEstatesNotBk = 88, IEstatesNotBee = 89,
                           Items = 90;

        // pozycje linii kas (Cls[klasa, *]) - zmiany kas osad nazwane w oknach, tylko poza naszym tickiem dobowym (tam lapia je migawki Mark)
        internal const int LCommission = 0, LCommissionToCounter = 1, LWorkshopOut = 2, LWorkshopIn = 3, LPorts = 4, LMines = 5, LConvoys = 6, LMarket = 7,
                           LVillageMarket = 8, ClsItems = 9;

        // linie modelu finansow (O40) - wartosci NALICZONE w modelu (przed obcieciem pustej kiesy)
        internal const int MTributeOut = 0, MTributeIn = 1, MMercOut = 2, MMercIn = 3, MCallWarOut = 4, MCallWarIn = 5, MDebt = 6, MSupport = 7, MBkTax = 8,
                           MCouncilPay = 9, MCouncilGet = 10, MTier = 11, MTierMerc = 12, MTierN = 13, MClansRead = 14, ModelItems = 15;

        // ------------------------------------------------------------ liczniki doby (ClearDay)
        internal static readonly long[] In = new long[Kinds], Out = new long[Kinds];
        internal static readonly long[] Sum = new long[Items];
        internal static readonly long[,] Cls = new long[Classes, ClsItems];
        internal static readonly long[] Model = new long[ModelItems];
        internal static readonly int[] Calls = new int[Windows], Hits = new int[Windows], Opened = new int[Windows], Sampled = new int[Windows];
        internal static readonly long[] Ticks = new long[Windows];
        internal static readonly bool[] Wired = new bool[Windows];                        // latka wpieta (Reset tego NIE czysci)
        internal static readonly string[] Missing = new string[Windows];                  // powod BRAK (linia startowa)
        internal static int OffThread, Stumbles, Nested, InClanSkipped, ProbeStale;
        internal static long BlockNamed;                                                   // pozycje nazwane w naszym ticku dobowym (RB)
        internal static long NamedRun;                                                     // biegnaca suma pozycji nazwanych (probki 2.4) - nie zerowana w ClearDay
        internal static long ToCounterRun;                                                 // biegnaca suma prowizji dopisanej do licznikow cel (O15 -> O46)
        internal static readonly int[] ProbeN = new int[Windows], ProbeBad = new int[Windows];
        internal static readonly long[] ProbeDiff = new long[Windows];
        internal static long ProbeTicks;
        internal static int GoldCalls, GoldSampled;                                        // koszt nasluchu zdarzen zlota (OnGold) - probka 1/256
        internal static long GoldTicks;
        internal static int ProbeScans;                                                    // pelne przeglady swiata w probkach dzis
        private static int _probesToday;
        // 169b: 12 probek na dobe (24 przeglady swiata, ok. 10 ms) - bylo 8; przy 8 nowych oknach rzadziej probkowane okna dziennego ticku
        // bohaterow (dochod i karawany notabli - po 6-8 probek na 40 dob w autotescie 169) spadlyby ponizej 5 probek
        private const int ProbesPerDay = 12;
        internal static readonly int[] ProbeNSess = new int[Windows], ProbeBadSess = new int[Windows];   // od startu sesji (Reset, nie ClearDay)
        private static readonly int[] _probeDay = new int[Windows];                       // ostatnia doba probki okna (Reset: -1000)
        private static bool _probeOn;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        // ------------------------------------------------------------ stan okien
        private static int _ctx, _ctxW = -1;                // okno flagowe: przyczyna (0 = brak) i indeks okna
        internal static int CurrentKind { get { return _ctx; } }   // 169c: przyczyna otwartego okna flagowego (tylko odczyt)
        private static Hero _ctxSide;                       // oczekiwana strona przelewu (null = dowolna)
        private static Settlement _sellSt;                  // okno prowizji (O15): osada sprzedajaca
        private static long _sellIn;                        // zdarzenia gry netto do kasy tej osady w oknie
        private static Hero _snapHero;                      // okno migawki bohatera (O22, O24-O34, O45)
        private static bool _snapLeaders;                   // okno "glowy wszystkich rodow" (O30)
        private static long _snapEvt;                       // zdarzenia gry z udzialem bohatera migawki (odejmowane - sa juz w from/to albo to przelewy)
        private static bool _capOpen, _capSeen;             // okno nowej karawany (O17): otwarte / pierwszy wynik modelu odczytany
        private static int _capGiven;                       // kwota nadana (pierwszy wynik GetInitialTradeGold w oknie)
        private static bool _minesOpen;                     // okno kopalni BK (O20) - place gornikow wracajace do miasta
        private static Type _popType;                       // BannerKings.Components.PopulationPartyComponent (O21)
        private static PropertyInfo _actionTaker;           // BannerKings TitleAction.ActionTaker (O24-O27)
        private static int[] _wsCap0 = new int[16];         // O45: kapital warsztatow miasta przed (bufory wielokrotnego uzytku)
        private static Hero[] _wsOwn0 = new Hero[16];       // O45: wlasciciele warsztatow przed

        // warsztaty (O35/O36) - pary Pre/Post z istniejacych latek WorkshopTrade (metody nie sa wspolbiezne; osobne pola wyrob/wsad)
        private static Workshop _woW, _wiW;
        private static Settlement _woSt, _wiSt;
        private static long _woK0, _woC0, _woT0, _woW0, _woN0, _wiK0, _wiC0, _wiT0, _wiW0, _wiN0;
        private static bool _woP, _wiP;

        // linie modelu i kapital karawan (zakladane w kampanii - EnsureModelHooks)
        private static readonly Type[] FinArgs = { typeof(Clan), typeof(bool), typeof(bool), typeof(bool) };
        private static readonly Type[] CapArgs = { typeof(Hero), typeof(bool), typeof(bool) };
        private static readonly HashSet<Type> _finHooked = new HashSet<Type>(), _capHooked = new HashSet<Type>();
        private static object _finTriedFor, _capTriedFor, _finModel;
        private static Type _finDecl;
        private static readonly string[] _lineNames = new string[11];
        private static readonly int[] _lineIdx = { MTributeOut, MTributeIn, MMercIn, MMercOut, MCallWarOut, MCallWarIn, MDebt, MSupport, MBkTax, MCouncilPay, MCouncilGet };
        internal static int LineNamesFound;
        internal static string FinModelName = "-", CapModelName = "-";

        // ------------------------------------------------------------ stany okien w __state (struktury - bez alokacji)
        /// <summary>Okno flagowe (F) - takze F+M (O01: aktywa notabla, O14: kiesa partii bez wodza). Kind/W/Side = flaga POPRZEDNIA (zagniezdzenie).</summary>
        internal struct Ctx
        {
            public bool Active; public int Kind, W, Me; public Hero Side;
            public bool M; public long A0; public Hero H; public MobileParty Mp;
            public long T0; public bool P; public long W0, N0;
        }
        internal struct CaravanState { public bool On; public long G0, C0, Due, CarPaid; public int N, ShortN, CountAll; public long T0; public bool P; public long W0, N0; }
        internal struct SellState { public Settlement St; public int S0, Tax0; public bool On; public long T0; public bool P; public long W0, N0; }
        internal struct SnapState { public bool On; public Hero H; public long G0; public int Item, Me; public Settlement St; public long X; public int Cnt; public long T0; public bool P; public long W0, N0; }
        internal struct CapState { public bool On, PrevOpen, PrevSeen; public int PrevGiven; public long T0; public bool P; public long W0, N0; }
        internal struct TollState { public bool On; public Town T; public long C0, R0; public long T0; public bool P; public long W0, N0; }
        /// <summary>Migawka kas osad / kiesy partii (O16, O18-O21, O23; 169b: karawany BEE - Me = okno).</summary>
        internal struct KasaState { public bool On; public Settlement St, St2; public MobileParty Mp; public long V, V2; public int Me; public long T0; public bool P; public long W0, N0; }

        // ------------------------------------------------------------ porzadki
        internal static void Reset()
        {
            try
            {
                ClearDay();
                _ctx = 0; _ctxW = -1; _ctxSide = null; _sellSt = null; _sellIn = 0; _snapHero = null; _snapLeaders = false; _snapEvt = 0;
                _capOpen = false; _capSeen = false; _capGiven = 0; _minesOpen = false; _repOpen = false;
                _woW = null; _wiW = null; _woSt = null; _wiSt = null; _woP = false; _wiP = false;
                NamedRun = 0; ToCounterRun = 0; _probeOn = false;
                for (int w = 0; w < Windows; w++) { ProbeNSess[w] = 0; ProbeBadSess[w] = 0; _probeDay[w] = -1000; }
                _finTriedFor = null; _capTriedFor = null; _finModel = null; _finDecl = null;   // nowa kampania = nowe obiekty modeli (latki zostaja w procesie)
                _errSites.Clear();
            }
            catch { }
        }

        /// <summary>Koniec doby ksiegi (MoneyLedger.ClearDay) - zeruje liczniki doby; NamedRun i _probeDay zostaja.</summary>
        internal static void ClearDay()
        {
            try
            {
                Array.Clear(In, 0, Kinds); Array.Clear(Out, 0, Kinds); Array.Clear(Sum, 0, Items); Array.Clear(Cls, 0, Cls.Length);
                Array.Clear(Model, 0, ModelItems);
                Array.Clear(Calls, 0, Windows); Array.Clear(Hits, 0, Windows); Array.Clear(Opened, 0, Windows); Array.Clear(Sampled, 0, Windows);
                Array.Clear(Ticks, 0, Windows);
                Array.Clear(ProbeN, 0, Windows); Array.Clear(ProbeBad, 0, Windows); Array.Clear(ProbeDiff, 0, Windows);
                OffThread = Stumbles = Nested = InClanSkipped = ProbeStale = 0;
                BlockNamed = 0; ProbeTicks = 0; ProbeScans = 0; _probesToday = 0;
                GoldCalls = GoldSampled = 0; GoldTicks = 0;
                // probka nie moze byc otwarta na koniec doby (nasz tick nie biegnie w zadnym oknie) - zostala tylko po wyjatku bez zamkniecia
                if (_probeOn) { _probeOn = false; ProbeStale++; }
            }
            catch { }
        }

        private static void Stumble(string where, Exception e)
        {
            Stumbles++;
            try { if (_errSites.Add(where)) Log.Error("CirculationWindows." + where, e); } catch { }
        }

        /// <summary>Bohater poza swiatem = definicja Campaign.AliveHeroes (Dead albo Disabled) - jego zloto nie jest w posiadaczach.</summary>
        internal static bool OutOfWorld(Hero h) { return h != null && (h.HeroState == Hero.CharacterStates.Dead || h.HeroState == Hero.CharacterStates.Disabled); }

        internal static int ClassOf(Settlement st)
        {
            if (st == null) return -1;
            return st.IsTown ? CTown : (st.IsCastle ? CCastle : (st.IsVillage ? CVill : -1));
        }

        /// <summary>Strona przelewu: bohater albo wodz partii lorda (setter PartyTradeGold partii lorda pisze do kiesy wodza).</summary>
        private static Hero Eff(Hero h, PartyBase p)
        {
            if (h != null) return h;
            if (p == null || !p.IsMobile) return null;
            var mp = p.MobileParty;
            return mp != null && mp.IsLordParty ? mp.LeaderHero : null;
        }

        private static bool IsLeader(Hero e) { return e != null && e.Clan != null && !e.Clan.IsBanditFaction && e.Clan.Leader == e; }

        /// <summary>Bramka kazdego okna: ksiega wlaczona, kampania, watek glowny. Liczy wywolania okna.</summary>
        private static bool Gate(int w)
        {
            if (!On || !Live) return false;
            if (Environment.CurrentManagedThreadId != _main) { OffThread++; return false; }
            Calls[w]++;
            _gT = (Calls[w] & 15) == 1 ? Stopwatch.GetTimestamp() : 0L;   // probka kosztu (1/16) - od bramki
            return true;
        }

        // Koszt okien (kontrolka 6.8): czas SAMYCH naszych cial - prefiks od bramki do CostStart i finalizer od CostResume do CostEnd.
        // Metoda gry, cudze latki i probki swiata (wlasny licznik ProbeTicks) sa poza pomiarem. Probka: co 16. wywolanie od pierwszego
        // w dobie (1, 17, 33...), wiec kazde okno wolane w dobie ma co najmniej jedna probke.
        private static long _gT;                           // znacznik czasu z bramki (0 = wywolanie bez probki)

        /// <summary>Koniec mierzonej czesci prefiksu (liczy otwarte okno). Zwraca 1, gdy to probka - finalizer zmierzy tez swoje cialo.</summary>
        private static long CostStart(int w)
        {
            Opened[w]++;
            long t = _gT; _gT = 0;
            if (t == 0 || w < 0) return 0L;
            Ticks[w] += Stopwatch.GetTimestamp() - t; Sampled[w]++;
            return 1L;
        }

        /// <summary>Poczatek ciala finalizera (tylko w probce).</summary>
        private static long CostResume(long mark) { return mark != 0 ? Stopwatch.GetTimestamp() : 0L; }

        /// <summary>Koniec ciala finalizera - przed ProbeClose.</summary>
        private static void CostEnd(int w, long t1)
        {
            if (t1 == 0 || w < 0) return;
            Ticks[w] += Stopwatch.GetTimestamp() - t1;
        }

        // ------------------------------------------------------------ pozycje bilansu
        /// <summary>Jedyna droga zapisu pozycji bilansu (Z, U, N, K1). sign: +1 dla Z i N, -1 dla U i K1. W rozliczeniu rodu nic (D7).</summary>
        internal static void AddWorld(int item, long value, int sign)
        {
            try
            {
                if (value == 0) return;
                if (MoneyLedger.InClanTick) { InClanSkipped++; return; }
                Sum[item] += value;
                long v = sign * value;
                NamedRun += v;
                if (MoneyLedger.InBlock) BlockNamed += v;
            }
            catch (Exception e) { Stumble("AddWorld", e); }
        }

        /// <summary>MoneyLedger.OnGoldTraded: zdarzenie liczone w from/to (2.2) - do NamedRun i (w bloku) do BlockNamed.</summary>
        internal static void NoteCounted(int a, bool gNone, bool rNone, bool counted, bool inBlock)
        {
            try
            {
                if (!counted || !(gNone || rNone) || !On) return;
                long v = gNone ? a : -a;
                NamedRun += v;
                if (inBlock) BlockNamed += v;
            }
            catch (Exception e) { Stumble("NoteCounted", e); }
        }

        /// <summary>MoneyLedger.NoteLevyBack: LevyGold oddal zaplate za werbunek bez zdarzenia (zrodlo nazwane w starej ksiedze).</summary>
        internal static void NoteLevy(int amount)
        {
            try
            {
                if (amount <= 0 || !On) return;
                NamedRun += amount;
                if (MoneyLedger.InBlock) BlockNamed += amount;
            }
            catch (Exception e) { Stumble("NoteLevy", e); }
        }

        /// <summary>BuildFunding.MineRevenuePostfix: polowa urobku kopalni wrocila do miasta jako place gornikow (informacja okna O20).</summary>
        internal static void NoteMineWages(int revenue)
        {
            try { if (_minesOpen && revenue > 0 && On) Sum[IMinesBack] += revenue; }
            catch (Exception e) { Stumble("NoteMineWages", e); }
        }

        /// <summary>
        /// 169b: nasz modul zmienil kiese bohatera BEZ zdarzenia gry (ChangeHeroGold - KingdomTreasury, IronBank, BuildFunding). Gdy bohater
        /// jest poza swiatem (Dead albo Disabled - jego kiesa nie jest w posiadaczach), zloto naprawde wyszlo ze swiata albo do niego weszlo:
        /// ta sama pozycja N5 co zdarzenia gry z bohaterem poza swiatem. delta ze znakiem jak ChangeHeroGold. Tylko licznik.
        /// </summary>
        internal static void NoteHeroGold(Hero h, long delta)
        {
            try
            {
                if (h == null || delta == 0 || !On || !OutOfWorld(h)) return;
                if (Environment.CurrentManagedThreadId != _main) { OffThread++; return; }
                AddWorld(NDeadTransfers, -delta, +1);
                Sum[IDeadDirect] += -delta; Sum[IDeadDirectN]++;
            }
            catch (Exception e) { Stumble("NoteHeroGold", e); }
        }

        // ------------------------------------------------------------ probki swiata (2.4)
        private static bool ProbeOpen(int w, out long w0, out long n0)
        {
            w0 = 0; n0 = 0;
            try
            {
                if (_probeOn || _probesToday >= ProbesPerDay || !ProbeOn) return false;
                if (MoneyLedger.InBlock || MoneyLedger.InClanTick || MoneyLedger.WinOpenNow) return false;
                int day = (int)CampaignTime.Now.ToDays;
                if (day - _probeDay[w] < 3) return false;
                long t = Stopwatch.GetTimestamp();
                w0 = MoneyLedger.WorldNow();
                ProbeTicks += Stopwatch.GetTimestamp() - t; ProbeScans++;
                n0 = NamedRun;
                _probeOn = true; _probeDay[w] = day; _probesToday++;
                return true;
            }
            catch (Exception e) { _probeOn = false; Stumble("ProbeOpen", e); return false; }
        }

        private static void ProbeClose(int w, long w0, long n0)
        {
            try
            {
                long t = Stopwatch.GetTimestamp();
                long dw = MoneyLedger.WorldNow() - w0;
                ProbeTicks += Stopwatch.GetTimestamp() - t; ProbeScans++;
                long dn = NamedRun - n0;
                ProbeN[w]++; ProbeNSess[w]++;
                if (dw != dn) { ProbeBad[w]++; ProbeBadSess[w]++; ProbeDiff[w] += dw - dn; }
            }
            catch (Exception e) { Stumble("ProbeClose", e); }
            finally { _probeOn = false; }
        }

        // ------------------------------------------------------------ zdarzenie gry (MoneyLedger.OnGoldTraded)
        /// <summary>Kazdy GiveGoldAction (kwota juz dodatnia, strony juz zamienione przez MoneyLedger). Rozbicie flag, prowizja, migawki, N5.</summary>
        internal static void OnGold(Hero gh, PartyBase gp, Hero rh, PartyBase rp, int a, bool gNone, bool rNone, int gc, int rc, bool inClan, bool inBlock, bool counted)
        {
            long tg = 0;
            try
            {
                if (!On) return;
                if (Environment.CurrentManagedThreadId != _main) { OffThread++; return; }
                tg = (++GoldCalls & 255) == 1 ? Stopwatch.GetTimestamp() : 0L;   // koszt nasluchu (probka 1/256)
                // szybka sciezka: zdarzenia samych partii przechodza dalej tylko przy otwartym oknie
                if (_ctx == 0 && _sellSt == null && _snapHero == null && !_snapLeaders && gh == null && rh == null) return;
                // N5: przelewy od i do bohaterow poza swiatem (ich zloto nie jest w posiadaczach)
                if (!inClan)
                {
                    if (gh != null && OutOfWorld(gh)) AddWorld(NDeadTransfers, a, +1);
                    if (rh != null && OutOfWorld(rh)) AddWorld(NDeadTransfers, -a, +1);
                }
                // flaga: rozbicie "z niczego / w nicosc" wedlug przyczyny okna (tylko zdarzenia liczone w from/to)
                if (_ctx != 0 && (gNone || rNone) && counted)
                {
                    var side = gNone ? Eff(rh, rp) : Eff(gh, gp);
                    if (_ctxSide == null || ReferenceEquals(side, _ctxSide))
                    {
                        int k = _ctx;
                        if (k == KPrisonersParty && gNone && rc >= 0) k = KPrisonersFort;   // twierdza sprzedaje jencow do wlasnej kasy
                        if (gNone) In[k] += a; else Out[k] += a;
                        if (gNone && (k == KBkEstates || k == KBeeEstateRent) && rh != null && rh.IsNotable) Sum[k == KBkEstates ? IEstatesNotBk : IEstatesNotBee] += a;   // 169b
                        if (_ctxW >= 0) Hits[_ctxW]++;
                    }
                }
                // okno prowizji: zdarzenia netto do kasy osady sprzedajacej
                if (_sellSt != null)
                {
                    if (rh == null && rp != null && rp.IsSettlement && ReferenceEquals(rp.Settlement, _sellSt)) _sellIn += a;
                    if (gh == null && gp != null && gp.IsSettlement && ReferenceEquals(gp.Settlement, _sellSt)) _sellIn -= a;
                }
                // migawki bohatera: zdarzenia z jego udzialem (takze po stronie jego partii) odejmujemy - sa juz w from/to albo to przelewy
                if (_snapHero != null)
                {
                    var er = Eff(rh, rp); var eg = Eff(gh, gp);
                    if (ReferenceEquals(er, _snapHero)) _snapEvt += a;
                    if (ReferenceEquals(eg, _snapHero)) _snapEvt -= a;
                }
                else if (_snapLeaders)
                {
                    var er = Eff(rh, rp); var eg = Eff(gh, gp);
                    if (IsLeader(er)) _snapEvt += a;
                    if (IsLeader(eg)) _snapEvt -= a;
                }
            }
            catch (Exception e) { Stumble("OnGold", e); }
            finally { if (tg != 0) { GoldTicks += Stopwatch.GetTimestamp() - tg; GoldSampled++; } }
        }

        // ------------------------------------------------------------ okna flagowe (F) - wspolny szkielet
        private static void OpenFlag(ref Ctx st, int w, int kind, Hero side)
        {
            st.Active = true; st.Kind = _ctx; st.Side = _ctxSide; st.W = _ctxW; st.Me = w;
            if (_ctx != 0) Nested++;
            _ctx = kind; _ctxSide = side; _ctxW = w;
            st.T0 = CostStart(w);
            st.P = ProbeOpen(w, out st.W0, out st.N0);
        }

        /// <summary>Wspolny finalizer okien flagowych: pozycje migawki (O01, O14), przywrocenie flagi poprzedniej, koszt, probka (ostatnia).</summary>
        public static void FlagFin(Ctx __state)
        {
            if (!__state.Active) return;                    // okno sie nie otworzylo - nic nie liczy i nic nie przywraca
            long tf = CostResume(__state.T0);       // koszt: cialo finalizera (bez metody gry, bez probki swiata)
            try
            {
                if (__state.M)
                {
                    if (__state.Me == WNotableIncome && __state.H != null)
                    {
                        // dochod notabla: gra zdejmuje go z kapitalu warsztatow i kas karawan, a wyplaca zdarzeniem nic -> notabl
                        long k1 = __state.A0 - Assets(__state.H);
                        AddWorld(K1NotableAssets, k1, -1);
                        Sum[INotableIncomeN]++;
                    }
                    else if (__state.Me == WBattle && __state.Mp != null)
                    {
                        // bitwy: zloto partii bez wodza zmienia sie bez zdarzenia
                        long d = (long)__state.Mp.PartyTradeGold - __state.A0;
                        AddWorld(NBattleNoEvent, d, +1);
                        if (d != 0) Hits[WBattle]++;
                    }
                    else if (__state.Me == WReparations) ReparationsClose(__state.A0);
                }
            }
            catch (Exception e) { _repOpen = false; Stumble("FlagFin", e); }
            _ctx = __state.Kind; _ctxSide = __state.Side; _ctxW = __state.W;
            try { CostEnd(__state.Me, tf); } catch { }
            if (__state.P) ProbeClose(__state.Me, __state.W0, __state.N0);
        }

        /// <summary>Aktywa notabla w posiadaczach swiata: kapital warsztatow + kasy jego aktywnych karawan (petle for, 0-5 pozycji).</summary>
        private static long Assets(Hero h)
        {
            long s = 0;
            var ws = h.OwnedWorkshops;
            if (ws != null) for (int i = 0; i < ws.Count; i++) { var w = ws[i]; if (w != null) s += w.Capital; }
            var cs = h.OwnedCaravans;
            if (cs != null)
                for (int i = 0; i < cs.Count; i++)
                {
                    var mp = cs[i] != null ? cs[i].MobileParty : null;
                    if (mp != null && mp.IsActive && !(mp.IsLordParty && mp.LeaderHero != null)) s += mp.PartyTradeGold;
                }
            return s;
        }

        // O01 - dochod notabla z aktywow (ClanVariablesCampaignBehavior.DailyTickHero)
        public static void NotableIncomePre(Hero __0, out Ctx __state)
        {
            __state = default(Ctx);
            try
            {
                if (!Gate(WNotableIncome) || __0 == null || !__0.IsActive || !__0.IsNotable) return;
                __state.M = true; __state.H = __0; __state.A0 = Assets(__0);
                OpenFlag(ref __state, WNotableIncome, KNotableIncome, __0);
            }
            catch (Exception e) { Stumble("NotableIncomePre", e); }
        }

        // O03 - pasmo notabli (NotablePowerManagementBehavior.BalanceGoldAndPowerOfNotable)
        public static void BandPre(Hero __0, out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WNotableBand) && __0 != null) OpenFlag(ref __state, WNotableBand, KNotableBand, __0); }
            catch (Exception e) { Stumble("BandPre", e); }
        }

        // O04 - nowy notabl (NotablesCampaignBehavior.OnHeroCreated)
        public static void NewNotablePre(Hero __0, out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WNotableNew) && __0 != null) OpenFlag(ref __state, WNotableNew, KNotableNew, __0); }
            catch (Exception e) { Stumble("NewNotablePre", e); }
        }

        // O05 - awanse (PartyUpgraderCampaignBehavior.ApplyEffects); platnik jak w grze: Owner, gdy zywy, inaczej wodz
        public static void UpgradePre(PartyBase __0, out Ctx __state)
        {
            __state = default(Ctx);
            try
            {
                if (!Gate(WUpgrade) || __0 == null) return;
                var side = __0.Owner != null && __0.Owner.IsAlive ? __0.Owner : (__0.LeaderHero != null && __0.LeaderHero.IsAlive ? __0.LeaderHero : null);
                if (side != null) OpenFlag(ref __state, WUpgrade, KUpgrade, side);
            }
            catch (Exception e) { Stumble("UpgradePre", e); }
        }

        // O06 - werbunek (RecruitmentCampaignBehavior.ApplyInternal); do garnizonu - bez zlota, bez flagi
        public static void RecruitPre(MobileParty __0, RecruitmentCampaignBehavior.RecruitingDetail __6, out Ctx __state)
        {
            __state = default(Ctx);
            try
            {
                if (!Gate(WRecruit) || __0 == null || __0.LeaderHero == null) return;
                int k = __6 == RecruitmentCampaignBehavior.RecruitingDetail.MercenaryFromTavern ? KRecruitTavern
                      : __6 == RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividual ? KRecruitNotable
                      : __6 == RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromMap ? KRecruitMap : 0;
                if (k != 0) OpenFlag(ref __state, WRecruit, k, __0.LeaderHero);
            }
            catch (Exception e) { Stumble("RecruitPre", e); }
        }

        // O07 - statki (ChangeShipOwnerAction.ApplyInternal), tylko handel
        public static void ShipsPre(ChangeShipOwnerAction.ShipOwnerChangeDetail __2, out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WShips) && __2 == ChangeShipOwnerAction.ShipOwnerChangeDetail.ApplyByTrade) OpenFlag(ref __state, WShips, KShips, null); }
            catch (Exception e) { Stumble("ShipsPre", e); }
        }

        // O08 - statki NavalDLC inne (zwrot za statki po rozdziale, premia za naprawe)
        public static void ShipsOtherPre(out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WShipsOther)) OpenFlag(ref __state, WShipsOther, KShipsOther, null); }
            catch (Exception e) { Stumble("ShipsOtherPre", e); }
        }

        // O09 - jency (SellPrisonersAction.ApplyInternal); podzial partie / twierdze w OnGold
        public static void PrisonersPre(out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WPrisoners)) OpenFlag(ref __state, WPrisoners, KPrisonersParty, null); }
            catch (Exception e) { Stumble("PrisonersPre", e); }
        }

        // O10 - lup z oblezen (SiegeAftermathCampaignBehavior.OnSiegeAftermathApplied)
        public static void SiegePre(out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WSiege)) OpenFlag(ref __state, WSiege, KSiege, null); }
            catch (Exception e) { Stumble("SiegePre", e); }
        }

        // O11 - nagrody turniejow (TournamentManager.GivePrizeToWinner)
        public static void TournamentPre(out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WTournament)) OpenFlag(ref __state, WTournament, KTournament, null); }
            catch (Exception e) { Stumble("TournamentPre", e); }
        }

        // O12 - jency z rabunkow BK (BKRaidCaptureBehavior.ExecuteCapture), strona = wodz
        public static void RaidPre(Hero __1, out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WRaid) && __1 != null) OpenFlag(ref __state, WRaid, KRaidCapture, __1); }
            catch (Exception e) { Stumble("RaidPre", e); }
        }

        // O13 - BetterEconomy (prefiksy bez parametrow - zadnych typow BEE)
        public static void BeeContributePre(out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WBee)) OpenFlag(ref __state, WBee, KBeeContribute, null); }
            catch (Exception e) { Stumble("BeeContributePre", e); }
        }
        public static void BeeCampPre(out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WBee)) OpenFlag(ref __state, WBee, KBeeCamp, null); }
            catch (Exception e) { Stumble("BeeCampPre", e); }
        }
        public static void BeeInvestPre(out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WBee)) OpenFlag(ref __state, WBee, KBeeInvest, null); }
            catch (Exception e) { Stumble("BeeInvestPre", e); }
        }
        public static void BeeMarketPre(out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WBee)) OpenFlag(ref __state, WBee, KBeeMarket, null); }
            catch (Exception e) { Stumble("BeeMarketPre", e); }
        }

        // O14 - bitwy (MapEventParty.CommitGoldChanges): flaga + migawka kiesy partii bez wodza
        public static void BattlePre(MapEventParty __instance, out Ctx __state)
        {
            __state = default(Ctx);
            try
            {
                if (!Gate(WBattle) || __instance == null) return;
                var party = __instance.Party;
                var mp = party != null && party.LeaderHero == null ? party.MobileParty : null;
                if (mp != null && mp.IsActive) { __state.M = true; __state.Mp = mp; __state.A0 = mp.PartyTradeGold; }
                OpenFlag(ref __state, WBattle, KBattle, null);
            }
            catch (Exception e) { Stumble("BattlePre", e); }
        }

        // ------------------------------------------------------------ O02 - zold karawan notabli (3.5)
        public static void CaravanWagePre(Hero __0, out CaravanState __state)
        {
            __state = default(CaravanState);
            try
            {
                if (!Gate(WCaravanWage) || __0 == null) return;
                var cs = __0.OwnedCaravans;
                if (cs == null || cs.Count == 0) return;
                __state.G0 = __0.Gold; __state.CountAll = cs.Count;
                for (int i = 0; i < cs.Count; i++)
                {
                    var mp = cs[i] != null ? cs[i].MobileParty : null;
                    if (mp == null || !mp.IsActive || (mp.IsLordParty && mp.LeaderHero != null)) continue;   // tylko kasy w posiadaczach swiata
                    long p = mp.PartyTradeGold, w = mp.TotalWage;
                    __state.C0 += p; __state.Due += w;
                    if (p >= w) __state.CarPaid += w; else __state.ShortN++;
                    __state.N++;
                }
                __state.On = true;
                __state.T0 = CostStart(WCaravanWage);
                __state.P = ProbeOpen(WCaravanWage, out __state.W0, out __state.N0);
            }
            catch (Exception e) { __state.On = false; Stumble("CaravanWagePre", e); }
        }

        public static void CaravanWageFin(Hero __0, CaravanState __state)
        {
            if (!__state.On) return;
            long tf = CostResume(__state.T0);       // koszt: cialo finalizera (bez metody gry, bez probki swiata)
            try
            {
                var cs = __0 != null ? __0.OwnedCaravans : null;
                if (cs == null || cs.Count != __state.CountAll) { Stumbles++; }   // lista zmienila sie w trakcie (gra tego nie robi) - wynik zostaje w reszcie
                else
                {
                    long c1 = 0;
                    for (int i = 0; i < cs.Count; i++)
                    {
                        var mp = cs[i] != null ? cs[i].MobileParty : null;
                        if (mp == null || !mp.IsActive || (mp.IsLordParty && mp.LeaderHero != null)) continue;
                        c1 += mp.PartyTradeGold;
                    }
                    long vanish = (__state.G0 + __state.C0) - ((long)__0.Gold + c1);
                    AddWorld(UCaravanFromCaravan, __state.CarPaid, -1);
                    AddWorld(UCaravanFromNotable, vanish - __state.CarPaid, -1);
                    Sum[ICaravanTopup] += (c1 - __state.C0) + __state.CarPaid;
                    Sum[ICaravanDue] += __state.Due;
                    Sum[ICaravanShort] += __state.Due - vanish;
                    Sum[ICaravanN] += __state.N; Sum[ICaravanShortN] += __state.ShortN; Sum[ICaravanNotables]++;
                    if (vanish != 0) Hits[WCaravanWage]++;
                }
            }
            catch (Exception e) { Stumble("CaravanWageFin", e); }
            try { CostEnd(WCaravanWage, tf); } catch { }
            if (__state.P) ProbeClose(WCaravanWage, __state.W0, __state.N0);
        }

        // ------------------------------------------------------------ O15 - prowizja od sprzedazy partiom (3.6)
        public static void SellPre(PartyBase __0, out SellState __state)
        {
            __state = default(SellState);
            try
            {
                if (!Gate(WSell)) return;
                if (_sellSt != null) { Nested++; return; }      // okno juz otwarte - wewnetrzne sie NIE otwiera i niczego nie zmienia
                if (__0 == null || !__0.IsSettlement || __0.Settlement == null || __0.Settlement.SettlementComponent == null) return;
                var st = __0.Settlement;
                // 112: we wsi takze licznik podatku wsi (112-p: VillageTakings oddaje skasowana cene w calosci do kiesy wsi, licznik zwykle bez zmian;
                // zostaje na wypadek cudzej latki, ktora cos na nim dopisze w tym oknie)
                __state.St = st; __state.S0 = st.SettlementComponent.Gold; __state.Tax0 = st.Town != null ? st.Town.TradeTaxAccumulated : (st.Village != null ? st.Village.TradeTaxAccumulated : 0);
                _sellSt = st; _sellIn = 0; __state.On = true;
                __state.T0 = CostStart(WSell);
                __state.P = ProbeOpen(WSell, out __state.W0, out __state.N0);
            }
            catch (Exception e) { if (!__state.On) _sellSt = null; Stumble("SellPre", e); }
        }

        public static void SellFin(SellState __state)
        {
            if (!__state.On) return;                        // prefiks nie ruszyl _sellSt / _sellIn - okno zewnetrzne nietkniete
            long tf = CostResume(__state.T0);       // koszt: cialo finalizera (bez metody gry, bez probki swiata)
            try
            {
                var st = __state.St;
                long s1 = st.SettlementComponent.Gold, t1 = st.Town != null ? st.Town.TradeTaxAccumulated : (st.Village != null ? st.Village.TradeTaxAccumulated : 0);
                long num2 = __state.S0 + _sellIn - s1;      // kasa osady stracila bez zdarzenia
                long toCounter = t1 - __state.Tax0;          // prowizja dopisana do licznika cel (posiadacz swiata)
                long lost = num2 - toCounter;                // w nicosc
                if (st.Town != null) ToCounterRun += toCounter;   // licznik cel miast (okno myta O46); licznik podatku wsi (112) - nie
                int c = ClassOf(st);
                if (c == CVill) AddWorld(UCommissionVillage, lost, -1);
                else { AddWorld(UCommissionTownLost, lost, -1); Sum[ICommissionToCounter] += toCounter; }
                if (c >= 0 && !MoneyLedger.InBlock) { Cls[c, LCommission] -= num2; Cls[c, LCommissionToCounter] += toCounter; }
                if (num2 != 0) Hits[WSell]++;
            }
            catch (Exception e) { Stumble("SellFin", e); }
            _sellSt = null; _sellIn = 0;
            try { CostEnd(WSell, tf); } catch { }
            if (__state.P) ProbeClose(WSell, __state.W0, __state.N0);
        }

        // ------------------------------------------------------------ migawki kas osad i kiesy partii (KasaState)
        private static void KasaOpen(ref KasaState st, int w)
        {
            st.On = true;
            st.T0 = CostStart(w);
            st.P = ProbeOpen(w, out st.W0, out st.N0);
        }

        private static void KasaClose(KasaState st, int w, long tf)
        {
            try { CostEnd(w, tf); } catch { }
            if (st.P) ProbeClose(w, st.W0, st.N0);
        }

        private static long Gold(Settlement st) { return st != null && st.SettlementComponent != null ? st.SettlementComponent.Gold : 0L; }

        // O16 - lup z cial dla AI (MapEvent.LootCasualtyCharacter): kiesa zwyciezcy (u lorda = kiesa wodza)
        public static void LootPre(MapEventParty __1, out KasaState __state)
        {
            __state = default(KasaState);
            try
            {
                if (!Gate(WLoot) || __1 == null || __1.Party == null) return;
                var leader = __1.Party.LeaderHero;
                var mp = __1.Party.MobileParty;
                if (leader == null || leader == Hero.MainHero || mp == null) return;
                __state.Mp = mp; __state.V = mp.PartyTradeGold;
                KasaOpen(ref __state, WLoot);
            }
            catch (Exception e) { Stumble("LootPre", e); }
        }

        public static void LootFin(KasaState __state)
        {
            if (!__state.On) return;
            long tf = CostResume(__state.T0);       // koszt: cialo finalizera (bez metody gry, bez probki swiata)
            try
            {
                long d = (long)__state.Mp.PartyTradeGold - __state.V;
                AddWorld(ZLoot, d, +1);
                if (d != 0) Hits[WLoot]++;
            }
            catch (Exception e) { Stumble("LootFin", e); }
            KasaClose(__state, WLoot, tf);
        }

        // O18 - BK rynek osady (BKSettlementBehavior.HandleMarketGold): kasa osady + kiesy jej notabli
        private static long NotablesGold(Settlement st)
        {
            long s = 0;
            var ns = st.Notables;
            if (ns != null) for (int i = 0; i < ns.Count; i++) { var h = ns[i]; if (h != null && !OutOfWorld(h)) s += h.Gold; }
            return s;
        }

        public static void MarketPre(Settlement __0, out KasaState __state)
        {
            __state = default(KasaState);
            try
            {
                if (!Gate(WMarket) || __0 == null || __0.SettlementComponent == null) return;
                __state.St = __0; __state.V = Gold(__0); __state.V2 = NotablesGold(__0);
                KasaOpen(ref __state, WMarket);
            }
            catch (Exception e) { Stumble("MarketPre", e); }
        }

        public static void MarketFin(KasaState __state)
        {
            if (!__state.On) return;
            long tf = CostResume(__state.T0);       // koszt: cialo finalizera (bez metody gry, bez probki swiata)
            try
            {
                long dK = Gold(__state.St) - __state.V, dN = NotablesGold(__state.St) - __state.V2;
                AddWorld(NMarketKasa, dK, +1); AddWorld(NMarketNotables, dN, +1);
                int c = ClassOf(__state.St);
                if (c >= 0 && !MoneyLedger.InBlock) Cls[c, LMarket] += dK;
                if (dK != 0 || dN != 0) Hits[WMarket]++;
            }
            catch (Exception e) { Stumble("MarketFin", e); }
            KasaClose(__state, WMarket, tf);
        }

        // O19 - BK porty (BKBuildingsBehavior.RunPorts): ryba z niczego na polke, kasa w nicosc
        public static void PortsPre(Town __0, out KasaState __state)
        {
            __state = default(KasaState);
            try
            {
                if (!Gate(WPorts) || __0 == null || __0.Settlement == null) return;
                __state.St = __0.Settlement; __state.V = __0.Gold;
                KasaOpen(ref __state, WPorts);
            }
            catch (Exception e) { Stumble("PortsPre", e); }
        }

        public static void PortsFin(KasaState __state)
        {
            if (!__state.On) return;
            long tf = CostResume(__state.T0);       // koszt: cialo finalizera (bez metody gry, bez probki swiata)
            try
            {
                long dK = Gold(__state.St) - __state.V;
                AddWorld(UPorts, -dK, -1);
                int c = ClassOf(__state.St);
                if (c >= 0 && !MoneyLedger.InBlock) Cls[c, LPorts] += dK;
                if (dK != 0) { Hits[WPorts]++; Sum[IPortsN]++; }
            }
            catch (Exception e) { Stumble("PortsFin", e); }
            KasaClose(__state, WPorts, tf);
        }

        // O20 - BK kopalnie (BKBuildingsBehavior.RunMines): kasa netto po placach gornikow (BuildFunding.MineRevenuePostfix w oknie)
        public static void MinesPre(Town __0, out KasaState __state)
        {
            __state = default(KasaState);
            try
            {
                if (!Gate(WMines) || __0 == null || __0.Settlement == null) return;
                __state.St = __0.Settlement; __state.V = __0.Gold;
                _minesOpen = true;
                KasaOpen(ref __state, WMines);
            }
            catch (Exception e) { Stumble("MinesPre", e); }
        }

        public static void MinesFin(KasaState __state)
        {
            if (!__state.On) return;
            long tf = CostResume(__state.T0);       // koszt: cialo finalizera (bez metody gry, bez probki swiata)
            _minesOpen = false;
            try
            {
                long dK = Gold(__state.St) - __state.V;
                AddWorld(UMinesNet, -dK, -1);
                int c = ClassOf(__state.St);
                if (c >= 0 && !MoneyLedger.InBlock) Cls[c, LMines] += dK;
                if (dK != 0) Hits[WMines]++;
            }
            catch (Exception e) { Stumble("MinesFin", e); }
            KasaClose(__state, WMines, tf);
        }

        // O21 - BK konwoje ludnosci (BKPartyBehavior.AddPopulationPartyBehavior): kasa miasta placi za towar konwoju
        public static void ConvoyPre(MobileParty __0, Settlement __1, out KasaState __state)
        {
            __state = default(KasaState);
            try
            {
                if (!Gate(WConvoys) || __0 == null || __1 == null || __1.Town == null || _popType == null) return;
                var pc = __0.PartyComponent;
                if (pc == null || pc.GetType() != _popType) return;
                __state.St = __1; __state.V = __1.Town.Gold;
                KasaOpen(ref __state, WConvoys);
            }
            catch (Exception e) { Stumble("ConvoyPre", e); }
        }

        public static void ConvoyFin(KasaState __state)
        {
            if (!__state.On) return;
            long tf = CostResume(__state.T0);       // koszt: cialo finalizera (bez metody gry, bez probki swiata)
            try
            {
                long dK = Gold(__state.St) - __state.V;
                AddWorld(UConvoys, -dK, -1);
                int c = ClassOf(__state.St);
                if (c >= 0 && !MoneyLedger.InBlock) Cls[c, LConvoys] += dK;
                if (dK != 0) { Hits[WConvoys]++; Sum[IConvoysN]++; }
            }
            catch (Exception e) { Stumble("ConvoyFin", e); }
            KasaClose(__state, WConvoys, tf);
        }

        // O23 - BK rynek wsi (BKBuildingsBehavior.OnDailyTickSettlement -> HandleVillage): kiesa wsi i kasa jej osady macierzystej (Bound)
        public static void VillageMarketPre(Settlement __0, out KasaState __state)
        {
            __state = default(KasaState);
            try
            {
                if (!Gate(WVillageMarket) || __0 == null || !__0.IsVillage || __0.Village == null) return;
                var bound = __0.Village.Bound;
                __state.St = __0; __state.V = Gold(__0);
                __state.St2 = bound != null && bound.Town != null ? bound : null; __state.V2 = Gold(__state.St2);
                KasaOpen(ref __state, WVillageMarket);
            }
            catch (Exception e) { Stumble("VillageMarketPre", e); }
        }

        public static void VillageMarketFin(KasaState __state)
        {
            if (!__state.On) return;
            long tf = CostResume(__state.T0);       // koszt: cialo finalizera (bez metody gry, bez probki swiata)
            try
            {
                long dV = Gold(__state.St) - __state.V, dT = __state.St2 != null ? Gold(__state.St2) - __state.V2 : 0;
                AddWorld(NVillageMarketVillage, dV, +1); AddWorld(NVillageMarketTown, dT, +1);
                if (!MoneyLedger.InBlock)
                {
                    Cls[CVill, LVillageMarket] += dV;
                    int c = ClassOf(__state.St2);
                    if (c >= 0) Cls[c, LVillageMarket] += dT;
                }
                if (dV != 0 || dT != 0) Hits[WVillageMarket]++;
            }
            catch (Exception e) { Stumble("VillageMarketFin", e); }
            KasaClose(__state, WVillageMarket, tf);
        }

        // ------------------------------------------------------------ O17 - kapital nowych karawan (D18)
        public static void CapPre(out CapState __state)
        {
            __state = default(CapState);
            try
            {
                if (!Gate(WCaravanNew)) return;
                __state.PrevOpen = _capOpen; __state.PrevSeen = _capSeen; __state.PrevGiven = _capGiven; __state.On = true;
                _capOpen = true; _capSeen = false; _capGiven = 0;
                __state.T0 = CostStart(WCaravanNew);
                __state.P = ProbeOpen(WCaravanNew, out __state.W0, out __state.N0);
            }
            catch (Exception e) { Stumble("CapPre", e); }
        }

        public static void CapFin(Hero __0, MobileParty __result, CapState __state)
        {
            if (!__state.On) return;
            long tf = CostResume(__state.T0);       // koszt: cialo finalizera (bez metody gry, bez probki swiata)
            try
            {
                if (_capSeen)
                {
                    bool notable = __0 != null && __0.IsNotable;
                    AddWorld(notable ? ZCaravanCapitalNotable : ZCaravanCapitalLord, _capGiven, +1);
                    Sum[ICaravanNewN]++;
                    if (notable) Sum[ICaravanNewNotableN]++;
                    if (__result != null) Sum[ICaravanCapAfter] += __result.PartyTradeGold;
                    Hits[WCaravanNew]++;
                }
                else if (__result != null) Sum[ICaravanNoCap]++;   // hak modelu niewpiety - kwota zostaje w reszcie
            }
            catch (Exception e) { Stumble("CapFin", e); }
            _capOpen = __state.PrevOpen; _capSeen = __state.PrevSeen; _capGiven = __state.PrevGiven;
            try { CostEnd(WCaravanNew, tf); } catch { }
            if (__state.P) ProbeClose(WCaravanNew, __state.W0, __state.N0);
        }

        /// <summary>GetInitialTradeGold czynnego CaravanModel: PIERWSZY wynik w oknie CreateCaravanParty = kwota dla InitializePartyTrade.</summary>
        public static void CapGoldPostfix(int __result)
        {
            try
            {
                if (!_capOpen || _capSeen) return;
                if (Environment.CurrentManagedThreadId != _main) return;
                _capSeen = true; _capGiven = Math.Max(0, __result);
                Calls[WCaravanCap]++; Hits[WCaravanCap]++;
            }
            catch (Exception e) { Stumble("CapGoldPostfix", e); }
        }

        // ------------------------------------------------------------ migawka bohatera z odjeciem zdarzen (3.7)
        private static bool SnapOpen(ref SnapState st, int w, int item, Hero h)
        {
            if (h == null) return false;
            if (_snapHero != null || _snapLeaders) { Nested++; return false; }   // okno wewnetrzne sie NIE otwiera i niczego nie zmienia
            st.H = h; st.G0 = h.Gold; st.Item = item; st.Me = w; st.On = true;
            _snapHero = h; _snapEvt = 0;
            st.T0 = CostStart(w);
            st.P = ProbeOpen(w, out st.W0, out st.N0);
            return true;
        }

        private static long LeadersGold()
        {
            long s = 0;
            var all = Clan.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c.IsBanditFaction) continue;
                var l = c.Leader;
                if (l != null && !OutOfWorld(l)) s += l.Gold;
            }
            return s;
        }

        /// <summary>Wspolny finalizer migawek bohatera: d = zmiana kiesy BEZ zdarzen; ujscia U6 (lordowie BK), zrodlo Z3 (niewolnicy).</summary>
        public static void SnapFin(SnapState __state)
        {
            if (!__state.On) return;                        // bez przywracania - stan okna zewnetrznego zostaje nietkniety
            long tf = CostResume(__state.T0);       // koszt: cialo finalizera (bez metody gry, bez probki swiata)
            try
            {
                long now = __state.Me == WDilemma ? LeadersGold() : (__state.H != null ? __state.H.Gold : 0L);
                long d = (now - __state.G0) - _snapEvt;
                if (__state.Item == ZSlaves) AddWorld(ZSlaves, d, +1);
                else AddWorld(__state.Item, -d, -1);
                if (d != 0) Hits[__state.Me]++;
                if (__state.Me == WLordBuy) LordBuyExtra(__state);
            }
            catch (Exception e) { Stumble("SnapFin", e); }
            _snapHero = null; _snapLeaders = false; _snapEvt = 0;
            try { CostEnd(__state.Me, tf); } catch { }
            if (__state.P) ProbeClose(__state.Me, __state.W0, __state.N0);
        }

        // O22 - BK sprzedaz niewolnikow (BKPartyBehavior.SellSurplusSlaves): zloto z niczego glowie rodu wlasciciela
        public static void SlavesPre(Settlement __0, out SnapState __state)
        {
            __state = default(SnapState);
            try { if (Gate(WSlaves) && __0 != null && __0.OwnerClan != null) SnapOpen(ref __state, WSlaves, ZSlaves, __0.OwnerClan.Leader); }
            catch (Exception e) { Stumble("SlavesPre", e); }
        }

        // O24-O27 - BK tytuly (TitleManager): kiesa action.ActionTaker
        private static Hero Taker(object action)
        {
            if (action == null || _actionTaker == null) return null;
            try { return _actionTaker.GetValue(action, null) as Hero; } catch { return null; }
        }
        public static void ClaimPre(object __0, out SnapState __state)
        {
            __state = default(SnapState);
            try { if (Gate(WTitles)) SnapOpen(ref __state, WTitles, ULordClaim, Taker(__0)); }
            catch (Exception e) { Stumble("ClaimPre", e); }
        }
        public static void CreateTitlePre(object __0, out SnapState __state)
        {
            __state = default(SnapState);
            try { if (Gate(WTitles)) SnapOpen(ref __state, WTitles, ULordCreate, Taker(__0)); }
            catch (Exception e) { Stumble("CreateTitlePre", e); }
        }
        public static void RevokePre(object __0, out SnapState __state)
        {
            __state = default(SnapState);
            try { if (Gate(WTitles)) SnapOpen(ref __state, WTitles, ULordRevoke, Taker(__0)); }
            catch (Exception e) { Stumble("RevokePre", e); }
        }
        public static void UsurpPre(object __1, out SnapState __state)
        {
            __state = default(SnapState);
            try { if (Gate(WTitles)) SnapOpen(ref __state, WTitles, ULordUsurp, Taker(__1)); }
            catch (Exception e) { Stumble("UsurpPre", e); }
        }

        // O28 - BK pasowanie z nowym rodem (BKKnighthoodBehavior.CreateClan): kiesa glowy starego rodu
        public static void KnightClanPre(Clan __1, out SnapState __state)
        {
            __state = default(SnapState);
            try { if (Gate(WKnight) && __1 != null) SnapOpen(ref __state, WKnight, ULordKnightClan, __1.Leader); }
            catch (Exception e) { Stumble("KnightClanPre", e); }
        }

        // O29 - BK koszty laski dworu (CourtGrace.AddExpense)
        public static void CourtPre(Clan __0, out SnapState __state)
        {
            __state = default(SnapState);
            try { if (Gate(WCourt) && __0 != null) SnapOpen(ref __state, WCourt, ULordCourt, __0.Leader); }
            catch (Exception e) { Stumble("CourtPre", e); }
        }

        // O30 - BK dylematy (BKDilemmaBehavior.ApplyAiLevers): suma kies glow wszystkich rodow
        public static void DilemmaPre(out SnapState __state)
        {
            __state = default(SnapState);
            try
            {
                if (!Gate(WDilemma)) return;
                if (_snapHero != null || _snapLeaders) { Nested++; return; }
                __state.G0 = LeadersGold(); __state.Item = ULordDilemma; __state.Me = WDilemma; __state.On = true;
                _snapLeaders = true; _snapEvt = 0;
                __state.T0 = CostStart(WDilemma);
                __state.P = ProbeOpen(WDilemma, out __state.W0, out __state.N0);
            }
            catch (Exception e) { Stumble("DilemmaPre", e); }
        }

        // O31 - BK przeniesienie dworu (MoveCourtDecision.ApplyGoal) - tylko gracz
        public static void MoveCourtPre(out SnapState __state)
        {
            __state = default(SnapState);
            try { if (Gate(WMoveCourt) && Clan.PlayerClan != null) SnapOpen(ref __state, WMoveCourt, ULordMoveCourt, Clan.PlayerClan.Leader); }
            catch (Exception e) { Stumble("MoveCourtPre", e); }
        }

        // O32 - wyposazenie rycerza BKROT (BKClanBehavior.EvaluateRecruitKnight; prefiks BKROT zawsze false - nasz First czyta przed nim)
        public static void RecruitKnightPre(Clan __0, out SnapState __state)
        {
            __state = default(SnapState);
            try { if (Gate(WRecruitKnight) && __0 != null) SnapOpen(ref __state, WRecruitKnight, ULordRecruitKnight, __0.Leader); }
            catch (Exception e) { Stumble("RecruitKnightPre", e); }
        }

        // O33 - BK donatywa imperium (BKImperialLoyaltyBehavior.ProcessImperialRealm)
        public static void ImperialPre(Kingdom __0, out SnapState __state)
        {
            __state = default(SnapState);
            try { if (Gate(WImperial) && __0 != null && __0.RulingClan != null) SnapOpen(ref __state, WImperial, ULordImperial, __0.RulingClan.Leader); }
            catch (Exception e) { Stumble("ImperialPre", e); }
        }

        // O34 - BK material na koniec budowy (BKBuildingsBehavior.OnBuildingChanged) - liczy tylko przy wylaczonym PaidConstruction
        // (przy wlaczonym BuildFunding.SkipIfOn pomija BK - okno sie nie otwiera)
        public static void BuildMatPre(Town __0, out SnapState __state)
        {
            __state = default(SnapState);
            try
            {
                if (!Gate(WBuildMat) || __0 == null || BuildFunding.On) return;
                if (__0.OwnerClan != null) SnapOpen(ref __state, WBuildMat, ULordBuildMaterial, __0.OwnerClan.Leader);
            }
            catch (Exception e) { Stumble("BuildMatPre", e); }
        }

        // O45 - lordowie BK kupuja karawany i warsztaty (BKLordPropertyBehavior.OnSettlementEntered)
        public static void LordBuyPre(MobileParty __0, Settlement __1, out SnapState __state)
        {
            __state = default(SnapState);
            try
            {
                if (!Gate(WLordBuy) || __0 == null || !__0.IsLordParty || __0.LeaderHero == null) return;
                if (!SnapOpen(ref __state, WLordBuy, ULordCaravanBuy, __0.LeaderHero)) return;
                __state.X = Sum[ICaravanNewN];
                if (__1 != null && __1.IsTown && __1.Town != null && __1.Town.Workshops != null)
                {
                    var ws = __1.Town.Workshops;
                    if (_wsCap0.Length < ws.Length) { _wsCap0 = new int[ws.Length]; _wsOwn0 = new Hero[ws.Length]; }
                    for (int i = 0; i < ws.Length; i++) { var w = ws[i]; _wsCap0[i] = w != null ? w.Capital : 0; _wsOwn0[i] = w != null ? w.Owner : null; }
                    __state.St = __1; __state.Cnt = ws.Length;
                }
            }
            catch (Exception e) { Stumble("LordBuyPre", e); }
        }

        private static void LordBuyExtra(SnapState st)
        {
            Sum[ICaravanBuyN] += Sum[ICaravanNewN] - st.X;
            if (st.St == null || st.St.Town == null || st.Cnt <= 0) return;
            var ws = st.St.Town.Workshops;
            if (ws == null || ws.Length != st.Cnt) { Stumbles++; return; }
            long dCap = 0;
            for (int i = 0; i < ws.Length; i++)
            {
                var w = ws[i];
                if (w == null) continue;
                dCap += (long)w.Capital - _wsCap0[i];
                if (!ReferenceEquals(w.Owner, _wsOwn0[i])) Sum[IWorkshopBuyN]++;
            }
            AddWorld(NWorkshopBuyCap, dCap, +1);
        }

        // ------------------------------------------------------------ O46 - myto (perk Tollgates zarzadcy miasta)
        public static void TollPre(MobileParty __0, Settlement __1, out TollState __state)
        {
            __state = default(TollState);
            try
            {
                if (!Gate(WTolls) || __0 == null || !__0.IsCaravan || __1 == null || !__1.IsTown) return;
                var t = __1.Town;
                if (t == null || t.Governor == null || !t.Governor.GetPerkValue(DefaultPerks.Trade.Tollgates)) return;
                __state.T = t; __state.C0 = t.TradeTaxAccumulated; __state.R0 = ToCounterRun; __state.On = true;
                __state.T0 = CostStart(WTolls);
                __state.P = ProbeOpen(WTolls, out __state.W0, out __state.N0);
            }
            catch (Exception e) { Stumble("TollPre", e); }
        }

        public static void TollFin(TollState __state)
        {
            if (!__state.On) return;
            long tf = CostResume(__state.T0);       // koszt: cialo finalizera (bez metody gry, bez probki swiata)
            try
            {
                // licznik cel w oknie minus prowizja dopisana przez sprzedaz w tym samym oknie (O15)
                long z5 = ((long)__state.T.TradeTaxAccumulated - __state.C0) - (ToCounterRun - __state.R0);
                AddWorld(ZTolls, z5, +1);
                if (z5 != 0) { Hits[WTolls]++; Sum[ITollN]++; }
            }
            catch (Exception e) { Stumble("TollFin", e); }
            try { CostEnd(WTolls, tf); } catch { }
            if (__state.P) ProbeClose(WTolls, __state.W0, __state.N0);
        }

        // ------------------------------------------------------------ O35/O36 - warsztaty (wolane z istniejacych latek WorkshopTrade)
        /// <summary>WorkshopTrade.OutPrefix (ProduceAnOutputToTown, [First]) - przed ArtisanInputs.OutPre.</summary>
        internal static void WorkshopOutPre(Workshop w)
        {
            try
            {
                if (_woW != null) { if (_woP) { _probeOn = false; ProbeStale++; } _woW = null; _woP = false; }   // poprzednie okno bez Post (wyjatek w oryginale)
                if (w == null || !Gate(WWorkshopOut)) return;
                var st = w.Settlement;
                if (st == null || st.SettlementComponent == null) return;
                _woSt = st; _woK0 = st.SettlementComponent.Gold; _woC0 = w.Capital; _woW = w;
                _woT0 = CostStart(WWorkshopOut);
                _woP = ProbeOpen(WWorkshopOut, out _woW0, out _woN0);
            }
            catch (Exception e) { Stumble("WorkshopOutPre", e); }
        }

        /// <summary>WorkshopTrade.OutPostfix - PIERWSZA linia (przed wplatami rzemieslnikow 147, ktore nie moga wejsc do okna).</summary>
        internal static void WorkshopOutPost(Workshop w)
        {
            if (_woW == null || !ReferenceEquals(w, _woW)) return;
            bool p = _woP; long w0 = _woW0, n0 = _woN0, t0 = CostResume(_woT0);   // koszt: cialo Post
            try
            {
                long dK = Gold(_woSt) - _woK0, dC = (long)w.Capital - _woC0;
                int c = ClassOf(_woSt);
                if (c >= 0 && !MoneyLedger.InBlock) Cls[c, LWorkshopOut] += dK;
                AddWorld(NWorkshopOutKasa, dK, +1); AddWorld(NWorkshopOutCap, dC, +1);
                if (dK != 0 || dC != 0) Hits[WWorkshopOut]++;
            }
            catch (Exception e) { Stumble("WorkshopOutPost", e); }
            _woW = null; _woSt = null; _woP = false;
            try { CostEnd(WWorkshopOut, t0); } catch { }
            if (p) ProbeClose(WWorkshopOut, w0, n0);
        }

        /// <summary>WorkshopTrade.InPrefix (ConsumeInputFromTownMarket, [First]).</summary>
        internal static void WorkshopInPre(Town town, Workshop w)
        {
            try
            {
                if (_wiW != null) { if (_wiP) { _probeOn = false; ProbeStale++; } _wiW = null; _wiP = false; }
                if (w == null || town == null || town.Settlement == null || !Gate(WWorkshopIn)) return;
                _wiSt = town.Settlement; _wiK0 = town.Gold; _wiC0 = w.Capital; _wiW = w;
                _wiT0 = CostStart(WWorkshopIn);
                _wiP = ProbeOpen(WWorkshopIn, out _wiW0, out _wiN0);
            }
            catch (Exception e) { Stumble("WorkshopInPre", e); }
        }

        /// <summary>WorkshopTrade.InPostfix - pierwsza linia.</summary>
        internal static void WorkshopInPost(Town town, Workshop w)
        {
            if (_wiW == null || !ReferenceEquals(w, _wiW)) return;
            bool p = _wiP; long w0 = _wiW0, n0 = _wiN0, t0 = CostResume(_wiT0);   // koszt: cialo Post
            try
            {
                long dK = Gold(_wiSt) - _wiK0, dC = (long)w.Capital - _wiC0;
                int c = ClassOf(_wiSt);
                // wsad dodatkowych cykli rzemieslnikow BK (ArtisanInputs.Decide) stara linia kas juz zna (NoteArtisans) - tu tylko swiat (N3)
                if (c >= 0 && !MoneyLedger.InBlock && !ArtisanInputs.InDecide) Cls[c, LWorkshopIn] += dK;
                AddWorld(NWorkshopInKasa, dK, +1); AddWorld(NWorkshopInCap, dC, +1);
                if (dK != 0 || dC != 0) Hits[WWorkshopIn]++;
            }
            catch (Exception e) { Stumble("WorkshopInPost", e); }
            _wiW = null; _wiSt = null; _wiP = false;
            try { CostEnd(WWorkshopIn, t0); } catch { }
            if (p) ProbeClose(WWorkshopIn, w0, n0);
        }

        // ------------------------------------------------------------ 169b: odszkodowania wojenne Diplomacy (KingdomWalletCost.ApplyCost)
        // Diplomacy przy pokoju (KingdomPeaceAction.AcceptPeace -> HybridCost.ApplyCost) wola GiveGoldToKingdomAction z portfelem "Reparations":
        // placacy oddaje skarbiec ponad 2 mln BEZ zdarzenia, reszte zapisuje jako dlug TributeWallet (rody splacaja go potem w rozliczeniach -
        // linia modelu "trybut zaplacony"); odbiorca dostaje Z GORY: 1/3 glowa krolestwa i do 1/6 najemnicy - zdarzeniem z niczego (flaga),
        // reszta do skarbca krolestwa BEZ zdarzenia (pozycja N8). Okno rzadkie (tylko przy pokoju): migawka skarbcow wszystkich krolestw.
        private static Kingdom[] _repK = new Kingdom[64];
        private static int[] _repW0 = new int[64], _repT0 = new int[64];
        private static int _repN;
        private static bool _repOpen;

        /// <summary>Skarbce krolestw w swiecie (jak ReadHolders: bez wyeliminowanych) + zapis portfeli do porownania w finalizerze.</summary>
        private static long ReparationsSnap()
        {
            var all = Kingdom.All;
            int n = all.Count;
            if (_repK.Length < n) { _repK = new Kingdom[n]; _repW0 = new int[n]; _repT0 = new int[n]; }
            long s = 0;
            for (int i = 0; i < n; i++)
            {
                var k = all[i];
                _repK[i] = k;
                if (k == null) continue;
                _repW0[i] = k.KingdomBudgetWallet; _repT0[i] = k.TributeWallet;
                if (!k.IsEliminated) s += k.KingdomBudgetWallet;
            }
            _repN = n; _repOpen = true;
            return s;
        }

        public static void ReparationsPre(out Ctx __state)
        {
            __state = default(Ctx);
            try
            {
                if (!Gate(WReparations)) return;
                if (!_repOpen) { __state.M = true; __state.A0 = ReparationsSnap(); }   // zagniezdzenie (gra tego nie robi) - tylko flaga
                OpenFlag(ref __state, WReparations, KReparations, null);
            }
            catch (Exception e) { _repOpen = false; __state.M = false; Stumble("ReparationsPre", e); }
        }

        /// <summary>Zmiana skarbcow krolestw w oknie (bez zdarzenia) = N8; do skarbcow / ze skarbcow i nowy dlug trybutu - informacja.</summary>
        private static void ReparationsClose(long w0)
        {
            try
            {
                var all = Kingdom.All;
                long now = 0, inW = 0, outW = 0, debt = 0;
                bool same = all.Count == _repN;
                for (int i = 0; i < all.Count; i++)
                {
                    var k = all[i];
                    if (k == null) continue;
                    if (!k.IsEliminated) now += k.KingdomBudgetWallet;
                    if (!same || !ReferenceEquals(k, _repK[i])) { same = false; continue; }
                    long d = (long)k.KingdomBudgetWallet - _repW0[i];
                    if (!k.IsEliminated) { if (d > 0) inW += d; else outW -= d; }
                    long dt = (long)k.TributeWallet - _repT0[i];
                    if (dt < 0) debt -= dt;
                }
                AddWorld(NReparations, now - w0, +1);
                if (same) { Sum[IReparationsIn] += inW; Sum[IReparationsOut] += outW; Sum[IReparationsDebt] += debt; }
                Sum[IReparationsN]++;
                if (now != w0) Hits[WReparations]++;
            }
            catch (Exception e) { Stumble("ReparationsClose", e); }
            finally { _repOpen = false; for (int i = 0; i < _repN && i < _repK.Length; i++) _repK[i] = null; }
        }

        // ------------------------------------------------------------ 169b: zloto z niczego dla wlascicieli majatkow i z skarbcow BEE (okna flagowe)
        // BK EstateData.DailyProductionIncome (BKSettlementBehavior, kazda wies raz na dobe): GiveGoldAction nic -> wlasciciel majatku (notabl
        // albo lord); BEE FeudalEconomyCampaignBehavior.TickEstateRent: renta majatku dla wlasciciela (notabl lokalny albo glowa rodu) z niczego;
        // BEE TownEconomyCampaignBehavior.TryPayTreasurySurplus i TradeAgreementCampaignBehavior.AccrueCustoms: wyplaty ze skarbcow BEE (poza
        // swiatem) dla glow rodow i krolestw. Wszystkie dotad w "inne" zrodel z niczego. Prefiksy bez parametrow (zadnych typow BK/BEE).
        public static void BkEstatesPre(out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WBkEstates)) OpenFlag(ref __state, WBkEstates, KBkEstates, null); }
            catch (Exception e) { Stumble("BkEstatesPre", e); }
        }
        public static void BeeEstatesPre(out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WBeeEstates)) OpenFlag(ref __state, WBeeEstates, KBeeEstateRent, null); }
            catch (Exception e) { Stumble("BeeEstatesPre", e); }
        }
        public static void BeePayoutPre(out Ctx __state)
        {
            __state = default(Ctx);
            try { if (Gate(WBeePayout)) OpenFlag(ref __state, WBeePayout, KBeePayout, null); }
            catch (Exception e) { Stumble("BeePayoutPre", e); }
        }

        // ------------------------------------------------------------ 169b: karawany BetterEconomy (CaravanCampaignBehavior)
        // BEE przy wjezdzie karawany do miasta (OnSettlementEntered) najmuje eskorte (TryHireEscort: 190 za konnego, 95 za pieszego), awansuje
        // zaloge (TryPromoteRoster) i rozlicza dostawe kontraktu (TryDeliverContract: kasa karawany - koszt + przychod ze skarbcow BEE), a raz
        // na dobe zabiera kasie karawany straty na niebezpiecznej drodze (ApplyDangerPressure). Wszystko przez PartyTradeGold BEZ zdarzenia i bez
        // odbiorcy w swiecie - to byl rozjazd probek okna nowych karawan (nowa karawana najmuje eskorte przy pierwszym wjezdzie). Migawka kasy
        // karawany; prefiks deklaruje tylko MobileParty __0 (typy BEE nie sa deklarowane).
        private static void BeeCarOpen(MobileParty mp, ref KasaState st, int w)
        {
            if (!Gate(w) || mp == null || !mp.IsActive) return;
            st.Mp = mp; st.V = mp.PartyTradeGold; st.Me = w;
            KasaOpen(ref st, w);
        }
        public static void BeeEscortPre(MobileParty __0, out KasaState __state)
        {
            __state = default(KasaState);
            try { BeeCarOpen(__0, ref __state, WBeeEscort); }
            catch (Exception e) { __state.On = false; Stumble("BeeEscortPre", e); }
        }
        public static void BeePromotePre(MobileParty __0, out KasaState __state)
        {
            __state = default(KasaState);
            try { BeeCarOpen(__0, ref __state, WBeePromote); }
            catch (Exception e) { __state.On = false; Stumble("BeePromotePre", e); }
        }
        public static void BeeDeliverPre(MobileParty __0, out KasaState __state)
        {
            __state = default(KasaState);
            try { BeeCarOpen(__0, ref __state, WBeeDeliver); }
            catch (Exception e) { __state.On = false; Stumble("BeeDeliverPre", e); }
        }
        public static void BeeDangerPre(MobileParty __0, out KasaState __state)
        {
            __state = default(KasaState);
            try { BeeCarOpen(__0, ref __state, WBeeDanger); }
            catch (Exception e) { __state.On = false; Stumble("BeeDangerPre", e); }
        }

        /// <summary>Wspolny finalizer okien karawan BEE: zmiana kasy karawany bez zdarzenia (BEE nie wola GiveGoldAction w tych metodach).</summary>
        public static void BeeCarFin(KasaState __state)
        {
            if (!__state.On) return;
            long tf = CostResume(__state.T0);       // koszt: cialo finalizera (bez metody BEE, bez probki swiata)
            try
            {
                var mp = __state.Mp;
                if (mp != null && mp.IsActive)
                {
                    long d = (long)mp.PartyTradeGold - __state.V;
                    switch (__state.Me)
                    {
                        case WBeeEscort: AddWorld(UBeeEscort, -d, -1); if (d != 0) Sum[IBeeEscortN]++; break;
                        case WBeePromote: AddWorld(UBeePromote, -d, -1); break;
                        case WBeeDeliver: AddWorld(NBeeDeliver, d, +1); if (d != 0) Sum[IBeeDeliverN]++; break;
                        case WBeeDanger: AddWorld(UBeeDanger, -d, -1); if (d != 0) Sum[IBeeDangerN]++; break;
                    }
                    if (d != 0) Hits[__state.Me]++;
                }
            }
            catch (Exception e) { Stumble("BeeCarFin", e); }
            KasaClose(__state, __state.Me, tf);
        }

        // ------------------------------------------------------------ O37 - stan bohatera (CampaignObjectManager.HeroStateChanged)
        public static void HeroStatePostfix(Hero __0, Hero.CharacterStates __1)
        {
            try
            {
                if (__0 == null || !Gate(WHeroState)) return;
                bool wasIn = !(__1 == Hero.CharacterStates.Dead || __1 == Hero.CharacterStates.Disabled);
                bool isIn = !OutOfWorld(__0);
                if (wasIn == isIn) return;
                int g = __0.Gold;
                if (wasIn && !isIn)
                {
                    AddWorld(UHeroesLeft, g, -1);
                    bool dead = __0.HeroState == Hero.CharacterStates.Dead;
                    if (dead && __0.IsNotable) { Sum[IHeroesLeftNotableN]++; Sum[IHeroesLeftNotableGold] += g; }
                    else if (dead && __0.IsLord) { Sum[IHeroesLeftLordN]++; Sum[IHeroesLeftLordGold] += g; }
                    else { Sum[IHeroesLeftOtherN]++; Sum[IHeroesLeftOtherGold] += g; }
                }
                else AddWorld(ZHeroesBack, g, +1);
                Hits[WHeroState]++;
            }
            catch (Exception e) { Stumble("HeroStatePostfix", e); }
        }

        // ------------------------------------------------------------ O38/O39 - nasluchy zdarzen gry (ArmouryBehavior.RegisterEvents)
        /// <summary>Partia znika z mapy (zdarzenie idzie PRZED RemoveParty): kiesa partii bez wodza przepada. B-1: ostatni z naszych nasluchow tego
        /// zdarzenia (dopisany pierwszy - gra wola od ostatnio dopisanego), zeby liczyc kiese PO przelewach 112 (sakwa taboru do zwyciezcy albo wsi).</summary>
        internal static void OnPartyDestroyed(MobileParty mp, PartyBase destroyer)
        {
            try
            {
                if (mp == null || !Gate(WPartyGone)) return;
                if (!mp.IsActive || (mp.IsLordParty && mp.LeaderHero != null)) return;   // kiesa wodza zostaje z wodzem
                int g = mp.PartyTradeGold;
                if (g <= 0) return;
                int item, cnt;
                if (mp.IsCaravan) { item = UPartyCaravan; cnt = IPartyGoneCaravanN; }
                else if (mp.IsVillager) { item = UPartyVillager; cnt = IPartyGoneVillagerN; }
                else if (mp.IsBandit) { item = UPartyBandit; cnt = IPartyGoneBanditN; }
                else if (mp.IsGarrison) { item = UPartyGarrison; cnt = IPartyGoneGarrisonN; }
                else { item = UPartyOther; cnt = IPartyGoneOtherN; }
                AddWorld(item, g, -1);
                Sum[cnt]++; Sum[IPartyGoneN]++;
                Hits[WPartyGone]++;
            }
            catch (Exception e) { Stumble("OnPartyDestroyed", e); }
        }

        /// <summary>Krolestwo upada: jego skarbiec wypada z posiadaczy (ReadHolders pomija krolestwa wyeliminowane).</summary>
        internal static void OnKingdomDestroyed(Kingdom k)
        {
            try
            {
                if (k == null || !Gate(WKingdomGone)) return;
                long wallet = k.KingdomBudgetWallet;
                AddWorld(NKingdomGone, -wallet, +1);
                if (wallet != 0) Hits[WKingdomGone]++;
            }
            catch (Exception e) { Stumble("OnKingdomDestroyed", e); }
        }

        // ------------------------------------------------------------ O40 - linie modelu finansow (3.8)
        public static void ModelLinesPostfix(object __instance, MethodBase __originalMethod, Clan __0, bool __2, ExplainedNumber __result)
        {
            try
            {
                if (!__2 || __0 == null || Campaign.Current == null) return;
                if (Environment.CurrentManagedThreadId != _main) return;
                if (!MoneyLedger.InClanTickFor(__0)) return;
                var active = Campaign.Current.Models.ClanFinanceModel;
                if (!ReferenceEquals(__instance, active)) return;                  // model opakowany przez inny - liczy zewnetrzny
                if (!ReferenceEquals(active, _finModel)) { _finModel = active; _finDecl = DeclOf(active.GetType(), "CalculateClanGoldChange", FinArgs); }
                if (__originalMethod == null || __originalMethod.DeclaringType != _finDecl) return;   // metoda bazowa wolana przez nadpisanie
                // D19: saldo naliczone tego rozliczenia - tylko do CSV i linii budzetu (niezaleznie od ksiegi obiegu)
                ClanIncomeBook.NoteModelSaldo(__0, (int)__result.ResultNumber);
                if (!On) return;
                Calls[WModelLines]++;
                var lines = __result.GetLines();
                for (int i = 0; i < lines.Count; i++)
                {
                    string name = lines[i].name;
                    if (name == null) continue;
                    for (int j = 0; j < _lineNames.Length; j++)
                        if (_lineNames[j] != null && string.Equals(name, _lineNames[j], StringComparison.Ordinal)) { Model[_lineIdx[j]] += (long)Math.Round(lines[i].number); break; }
                }
                // dochod za tier (linia bez opisu - wzor gry DefaultClanFinanceModel: rody poza krolestwem albo najemnicy, bez lenn)
                var mf = __0.MapFaction;
                // 185: "za tier" AI w sluzbie zdjety wpisem bez opisu (MercContract.TierPostfix) - w saldzie go nie ma, wiec nie jest zrodlem z niczego
                if (__0 != Clan.PlayerClan && mf != null && (!mf.IsKingdomFaction || __0.IsUnderMercenaryService) && __0.Fiefs.Count == 0 && !MercContract.CancelsTier(__0))
                {
                    long t = __0.Tier * (80 + (__0.IsUnderMercenaryService ? 40 : 0));
                    Model[MTier] += t; Model[MTierN]++;
                    if (__0.IsUnderMercenaryService) Model[MTierMerc] += t;
                }
                Model[MClansRead]++;
                Hits[WModelLines]++;
            }
            catch (Exception e) { Stumble("ModelLinesPostfix", e); }
        }

        private static Type DeclOf(Type t, string name, Type[] args)
        {
            try
            {
                var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, args, null);
                return m != null ? m.DeclaringType : null;
            }
            catch { return null; }
        }

        private static string Txt(Func<string> f)
        {
            try { var s = f(); return string.IsNullOrEmpty(s) ? null : s; } catch { return null; }
        }

        /// <summary>
        /// Z ArmouryBehavior.OnSessionLaunched (kampania istnieje): postfiks na CalculateClanGoldChange czynnego modelu finansow (O40)
        /// i na GetInitialTradeGold czynnego modelu karawan (O17) - raz na klase. Przy starcie gry modeli nie latamy (konstruktor statyczny
        /// DefaultClanFinanceModel czyta Game.Current - SoldierPay.cs). Nazwy linii raz na sesje, w jezyku gry; pol statycznych modelu nie czytamy.
        /// </summary>
        internal static void EnsureModelHooks()
        {
            var h = _harmony;
            if (h == null || Campaign.Current == null) return;
            var models = Campaign.Current.Models;
            // nazwy linii (kolejnosc jak _lineIdx), kazda osobno
            _lineNames[0] = Txt(() => GameTexts.FindText("str_finance_tribute_expenses").ToString());
            _lineNames[1] = Txt(() => GameTexts.FindText("str_finance_tribute_incomes").ToString());
            _lineNames[2] = Txt(() => GameTexts.FindText("str_finance_mercenary").ToString());
            _lineNames[3] = Txt(() => GameTexts.FindText("str_finance_mercenary_expenses").ToString());
            _lineNames[4] = Txt(() => GameTexts.FindText("str_finance_call_to_war_expenses").ToString());
            _lineNames[5] = Txt(() => GameTexts.FindText("str_finance_call_to_war_incomes").ToString());
            _lineNames[6] = Txt(() => GameTexts.FindText("str_finance_debt").ToString());
            _lineNames[7] = Txt(() => GameTexts.FindText("str_finance_kingdom_support").ToString());
            _lineNames[8] = Txt(() => new TextObject("{=7uzvI8e8}Kingdom Budget Expense").ToString());
            _lineNames[9] = Txt(() => new TextObject("{=L0Dwod0e}Council wages").ToString());
            _lineNames[10] = Txt(() => new TextObject("{=WvhXhUFS}Councillor role").ToString());
            int found = 0;
            for (int i = 0; i < _lineNames.Length; i++) if (_lineNames[i] != null) found++;
            LineNamesFound = found;
            // O40 - linie modelu finansow
            var fin = models.ClanFinanceModel;
            if (fin != null && !ReferenceEquals(fin, _finTriedFor))
            {
                _finTriedFor = fin;
                string where = "BRAK (brak metody CalculateClanGoldChange)";
                try
                {
                    var decl = DeclOf(fin.GetType(), "CalculateClanGoldChange", FinArgs);
                    if (decl != null && _finHooked.Contains(decl)) where = "wpiete wczesniej";
                    else if (decl != null)
                    {
                        var m = decl.GetMethod("CalculateClanGoldChange", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, FinArgs, null);
                        if (m != null && !m.IsAbstract)
                        {
                            h.Patch(m, postfix: new HarmonyMethod(typeof(CirculationWindows), nameof(ModelLinesPostfix)) { priority = Priority.Last });
                            _finHooked.Add(decl);
                            where = "wpiete teraz";
                        }
                    }
                    Wired[WModelLines] = decl != null && _finHooked.Contains(decl);
                    if (!Wired[WModelLines]) Missing[WModelLines] = "BRAK (model " + fin.GetType().FullName + ")";
                }
                catch (Exception e) { Wired[WModelLines] = false; where = "BRAK (blad: " + e.Message + ")"; Missing[WModelLines] = where; }
                FinModelName = fin.GetType().FullName;
                Log.Info("Obieg (169): linie modelu finansow z " + FinModelName + " (" + where + "); nazwy linii: " + found + " z " + _lineNames.Length + " znalezione.");
            }
            // O17 - kapital nowych karawan: pierwszy wynik GetInitialTradeGold w oknie CreateCaravanParty
            var cm = models.CaravanModel;
            if (cm != null && !ReferenceEquals(cm, _capTriedFor))
            {
                _capTriedFor = cm;
                string where = "BRAK";
                try
                {
                    var decl = DeclOf(cm.GetType(), "GetInitialTradeGold", CapArgs);
                    if (decl != null && _capHooked.Contains(decl)) where = "wpiety wczesniej";
                    else if (decl != null)
                    {
                        var m = decl.GetMethod("GetInitialTradeGold", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, CapArgs, null);
                        if (m != null && !m.IsAbstract)
                        {
                            h.Patch(m, postfix: new HarmonyMethod(typeof(CirculationWindows), nameof(CapGoldPostfix)) { priority = Priority.Last });
                            _capHooked.Add(decl);
                            where = "wpiety";
                        }
                    }
                    Wired[WCaravanCap] = decl != null && _capHooked.Contains(decl);
                    if (!Wired[WCaravanCap]) Missing[WCaravanCap] = "BRAK (model " + cm.GetType().FullName + ")";
                }
                catch (Exception e) { Wired[WCaravanCap] = false; where = "BRAK (blad: " + e.Message + ")"; Missing[WCaravanCap] = where; }
                CapModelName = cm.GetType().FullName;
                Log.Info("Obieg (169): kapital nowych karawan z " + CapModelName + " (" + where + ").");
            }
        }

        // ------------------------------------------------------------ wpiecie (z SubModuleMain - po naszych latkach na te same metody, przed GoodsLedger)
        private static void Wire(Harmony h, int w, string label, Type t, string typeName, string method, Type[] args, string pre, string fin, string post,
                                 List<string> done, List<string> miss)
        {
            try
            {
                if (t == null) { miss.Add(label + " BRAK (brak typu " + typeName + ")"); return; }
                MethodInfo m = args != null ? AccessTools.Method(t, method, args) : AccessTools.Method(t, method);
                if (m == null) { miss.Add(label + " BRAK (brak metody " + method + ")"); return; }
                // parametry latek bierzemy po pozycji (__0, __1...), a Harmony sprawdza tylko numer, nie typ - inna sygnatura w innej wersji
                // modu dalaby latce obiekt innego typu; wtedy okno sie nie wpina (BRAK z powodem), gra bez zmian
                string bad = SignatureMismatch(m, pre) ?? SignatureMismatch(m, fin) ?? SignatureMismatch(m, post);
                if (bad != null) { miss.Add(label + " BRAK (inna sygnatura " + method + ": " + bad + ")"); return; }
                h.Patch(m,
                        prefix: pre != null ? new HarmonyMethod(typeof(CirculationWindows), pre) { priority = Priority.First } : null,
                        postfix: post != null ? new HarmonyMethod(typeof(CirculationWindows), post) : null,
                        finalizer: fin != null ? new HarmonyMethod(typeof(CirculationWindows), fin) : null);
                Wired[w] = true;
                done.Add(label);
            }
            catch (Exception e) { miss.Add(label + " BRAK (blad: " + e.Message + ")"); }
        }

        /// <summary>
        /// Czy parametry naszej latki pasuja do metody: __N - typ parametru N metody rowny typowi w latce (object - dowolna klasa),
        /// __instance - metoda instancji typu latki, __result - ten sam typ wyniku. null = pasuje, inaczej opis niezgodnosci.
        /// </summary>
        private static string SignatureMismatch(MethodInfo m, string patch)
        {
            if (patch == null) return null;
            var pm = AccessTools.Method(typeof(CirculationWindows), patch);
            if (pm == null) return "brak latki " + patch;
            var ps = m.GetParameters();
            foreach (var pp in pm.GetParameters())
            {
                string n = pp.Name;
                Type want = pp.ParameterType;
                if (n == "__state" || n == "__exception" || n == "__originalMethod") continue;
                if (n == "__instance")
                {
                    if (m.IsStatic || !want.IsAssignableFrom(m.DeclaringType)) return n + " (" + want.Name + ")";
                    continue;
                }
                if (n == "__result")
                {
                    if (m.ReturnType != want) return n + ": " + m.ReturnType.Name + " zamiast " + want.Name;
                    continue;
                }
                int idx;
                if (n.Length > 2 && n.StartsWith("__", StringComparison.Ordinal) && int.TryParse(n.Substring(2), out idx))
                {
                    if (idx < 0 || idx >= ps.Length) return n + ": metoda ma " + ps.Length + " parametrow";
                    var have = ps[idx].ParameterType;
                    bool ok = want == typeof(object) ? (!have.IsValueType && !have.IsByRef) : have == want;
                    if (!ok) return n + ": " + have.Name + " zamiast " + want.Name;
                }
            }
            return null;
        }

        /// <summary>Czy istniejaca latka WorkshopTrade (prefiks o danej nazwie) siedzi na metodzie - okna O35/O36 jada na niej.</summary>
        private static bool HasOurPrefix(MethodBase m, string name)
        {
            try
            {
                if (m == null) return false;
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return false;
                foreach (var p in info.Prefixes)
                    if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(WorkshopTrade) && p.PatchMethod.Name == name) return true;
            }
            catch { }
            return false;
        }

        internal static void ApplyAll(Harmony h)
        {
            _harmony = h;
            _main = Environment.CurrentManagedThreadId;
            var done = new List<string>(); var miss = new List<string>();
            int before = 0;
            // gra 1.4.8
            Wire(h, WNotableIncome, "notable-dochod", typeof(ClanVariablesCampaignBehavior), null, "DailyTickHero", new[] { typeof(Hero) }, nameof(NotableIncomePre), nameof(FlagFin), null, done, miss);
            Wire(h, WCaravanWage, "karawany-notabli", typeof(NotablesCampaignBehavior), null, "ManageCaravanExpensesOfNotable", new[] { typeof(Hero) }, nameof(CaravanWagePre), nameof(CaravanWageFin), null, done, miss);
            Wire(h, WNotableBand, "pasmo-notabli", typeof(NotablePowerManagementBehavior), null, "BalanceGoldAndPowerOfNotable", new[] { typeof(Hero) }, nameof(BandPre), nameof(FlagFin), null, done, miss);
            Wire(h, WNotableNew, "nowy-notabl", typeof(NotablesCampaignBehavior), null, "OnHeroCreated", new[] { typeof(Hero), typeof(bool) }, nameof(NewNotablePre), nameof(FlagFin), null, done, miss);
            Wire(h, WUpgrade, "awanse", typeof(PartyUpgraderCampaignBehavior), null, "ApplyEffects", null, nameof(UpgradePre), nameof(FlagFin), null, done, miss);
            Wire(h, WRecruit, "werbunek", typeof(RecruitmentCampaignBehavior), null, "ApplyInternal", null, nameof(RecruitPre), nameof(FlagFin), null, done, miss);
            Wire(h, WShips, "statki", typeof(ChangeShipOwnerAction), null, "ApplyInternal", null, nameof(ShipsPre), nameof(FlagFin), null, done, miss);
            const string navDist = "NavalDLC.CampaignBehaviors.NavalShipDistributionCampaignBehavior", navTrade = "NavalDLC.CampaignBehaviors.ShipTradeCampaignBehavior";
            Wire(h, WShipsOther, "statki-inne (zwrot za statki)", AccessTools.TypeByName(navDist), navDist, "RecoverGoldFromRemainingShipsAfterDistribution", null, nameof(ShipsOtherPre), nameof(FlagFin), null, done, miss);
            Wire(h, WShipsOther, "statki-inne (premia za naprawe)", AccessTools.TypeByName(navTrade), navTrade, "OnShipRepaired", null, nameof(ShipsOtherPre), nameof(FlagFin), null, done, miss);
            Wire(h, WPrisoners, "jency", typeof(SellPrisonersAction), null, "ApplyInternal", null, nameof(PrisonersPre), nameof(FlagFin), null, done, miss);
            Wire(h, WSiege, "oblezenie", typeof(SiegeAftermathCampaignBehavior), null, "OnSiegeAftermathApplied", null, nameof(SiegePre), nameof(FlagFin), null, done, miss);
            Wire(h, WTournament, "turniej", typeof(TournamentManager), null, "GivePrizeToWinner", null, nameof(TournamentPre), nameof(FlagFin), null, done, miss);
            Wire(h, WBattle, "bitwy", typeof(MapEventParty), null, "CommitGoldChanges", null, nameof(BattlePre), nameof(FlagFin), null, done, miss);
            Wire(h, WSell, "prowizja", typeof(SellItemsAction), null, "ApplyInternal", null, nameof(SellPre), nameof(SellFin), null, done, miss);
            Wire(h, WLoot, "lup-z-cial", typeof(MapEvent), null, "LootCasualtyCharacter", null, nameof(LootPre), nameof(LootFin), null, done, miss);
            Wire(h, WCaravanNew, "nowe-karawany", typeof(CaravanPartyComponent), null, "CreateCaravanParty", null, nameof(CapPre), nameof(CapFin), null, done, miss);
            Wire(h, WTolls, "myto (vanilla)", typeof(CaravansCampaignBehavior), null, "OnSettlementEntered", new[] { typeof(MobileParty), typeof(Settlement), typeof(Hero) }, nameof(TollPre), nameof(TollFin), null, done, miss);
            Wire(h, WHeroState, "stan-bohatera", typeof(CampaignObjectManager), null, "HeroStateChanged", new[] { typeof(Hero), typeof(Hero.CharacterStates) }, null, null, nameof(HeroStatePostfix), done, miss);
            // BannerKings
            const string bkRaid = "BannerKings.Behaviours.Raids.BKRaidCaptureBehavior", bkSet = "BannerKings.Behaviours.BKSettlementBehavior",
                         bkBld = "BannerKings.Behaviours.BKBuildingsBehavior", bkParty = "BannerKings.Behaviours.BKPartyBehavior", bkTitles = "BannerKings.Managers.TitleManager",
                         bkKnight = "BannerKings.Behaviours.BKKnighthoodBehavior", bkGrace = "BannerKings.Managers.Court.Grace.CourtGrace",
                         bkDil = "BannerKings.Behaviours.Diplomacy.Dilemmas.BKDilemmaBehavior", bkMove = "BannerKings.Managers.Goals.Decisions.MoveCourtDecision",
                         bkClan = "BannerKings.Behaviours.BKClanBehavior", bkImp = "BannerKings.Behaviours.Diplomacy.BKImperialLoyaltyBehavior",
                         bkLord = "BannerKings.Behaviours.BKLordPropertyBehavior", bkCar = "BannerKings.Behaviours.BKCaravansBehavior";
            try { _popType = AccessTools.TypeByName("BannerKings.Components.PopulationPartyComponent"); } catch { _popType = null; }
            try
            {
                var ta = AccessTools.TypeByName("BannerKings.Managers.Titles.TitleAction");
                _actionTaker = ta != null ? AccessTools.Property(ta, "ActionTaker") : null;
            }
            catch { _actionTaker = null; }
            Wire(h, WRaid, "rabunek-BK", AccessTools.TypeByName(bkRaid), bkRaid, "ExecuteCapture", null, nameof(RaidPre), nameof(FlagFin), null, done, miss);
            Wire(h, WMarket, "rynek-osady", AccessTools.TypeByName(bkSet), bkSet, "HandleMarketGold", null, nameof(MarketPre), nameof(MarketFin), null, done, miss);
            Wire(h, WPorts, "porty", AccessTools.TypeByName(bkBld), bkBld, "RunPorts", null, nameof(PortsPre), nameof(PortsFin), null, done, miss);
            Wire(h, WMines, "kopalnie", AccessTools.TypeByName(bkBld), bkBld, "RunMines", null, nameof(MinesPre), nameof(MinesFin), null, done, miss);
            if (_popType != null)
                Wire(h, WConvoys, "konwoje", AccessTools.TypeByName(bkParty), bkParty, "AddPopulationPartyBehavior", null, nameof(ConvoyPre), nameof(ConvoyFin), null, done, miss);
            else miss.Add("konwoje BRAK (brak typu BannerKings.Components.PopulationPartyComponent)");
            Wire(h, WSlaves, "niewolnicy", AccessTools.TypeByName(bkParty), bkParty, "SellSurplusSlaves", null, nameof(SlavesPre), nameof(SnapFin), null, done, miss);
            Wire(h, WVillageMarket, "rynek-wsi", AccessTools.TypeByName(bkBld), bkBld, "OnDailyTickSettlement", null, nameof(VillageMarketPre), nameof(VillageMarketFin), null, done, miss);
            if (_actionTaker != null)
            {
                Wire(h, WTitles, "tytuly (roszczenia)", AccessTools.TypeByName(bkTitles), bkTitles, "AddOngoingClaim", null, nameof(ClaimPre), nameof(SnapFin), null, done, miss);
                Wire(h, WTitles, "tytuly (nadania)", AccessTools.TypeByName(bkTitles), bkTitles, "CreateTitle", null, nameof(CreateTitlePre), nameof(SnapFin), null, done, miss);
                Wire(h, WTitles, "tytuly (odebrania)", AccessTools.TypeByName(bkTitles), bkTitles, "RevokeTitle", null, nameof(RevokePre), nameof(SnapFin), null, done, miss);
                Wire(h, WTitles, "tytuly (uzurpacje)", AccessTools.TypeByName(bkTitles), bkTitles, "UsurpTitle", null, nameof(UsurpPre), nameof(SnapFin), null, done, miss);
            }
            else miss.Add("tytuly BRAK (brak typu BannerKings.Managers.Titles.TitleAction albo wlasciwosci ActionTaker)");
            Wire(h, WKnight, "pasowanie", AccessTools.TypeByName(bkKnight), bkKnight, "CreateClan", null, nameof(KnightClanPre), nameof(SnapFin), null, done, miss);
            Wire(h, WCourt, "dwor", AccessTools.TypeByName(bkGrace), bkGrace, "AddExpense", null, nameof(CourtPre), nameof(SnapFin), null, done, miss);
            Wire(h, WDilemma, "dylematy", AccessTools.TypeByName(bkDil), bkDil, "ApplyAiLevers", null, nameof(DilemmaPre), nameof(SnapFin), null, done, miss);
            Wire(h, WMoveCourt, "przeniesienie-dworu", AccessTools.TypeByName(bkMove), bkMove, "ApplyGoal", Type.EmptyTypes, nameof(MoveCourtPre), nameof(SnapFin), null, done, miss);
            Wire(h, WRecruitKnight, "rycerz-BKROT", AccessTools.TypeByName(bkClan), bkClan, "EvaluateRecruitKnight", null, nameof(RecruitKnightPre), nameof(SnapFin), null, done, miss);
            Wire(h, WImperial, "donatywa", AccessTools.TypeByName(bkImp), bkImp, "ProcessImperialRealm", null, nameof(ImperialPre), nameof(SnapFin), null, done, miss);
            Wire(h, WBuildMat, "material-budowy", AccessTools.TypeByName(bkBld), bkBld, "OnBuildingChanged", null, nameof(BuildMatPre), nameof(SnapFin), null, done, miss);
            Wire(h, WLordBuy, "kupno-BK", AccessTools.TypeByName(bkLord), bkLord, "OnSettlementEntered", null, nameof(LordBuyPre), nameof(SnapFin), null, done, miss);
            Wire(h, WTolls, "myto (BK)", AccessTools.TypeByName(bkCar), bkCar, "OnSettlementEntered", new[] { typeof(MobileParty), typeof(Settlement), typeof(Hero) }, nameof(TollPre), nameof(TollFin), null, done, miss);
            // BetterEconomy
            const string beeCastle = "BetterEconomy.Behaviors.CastleEconomyCampaignBehavior", beeInv = "BetterEconomy.Behaviors.VillageInvestmentCampaignBehavior",
                         beeDev = "BetterEconomy.Behaviors.VillageDevelopmentCampaignBehavior";
            Wire(h, WBee, "BEE (skarbce zamkow)", AccessTools.TypeByName(beeCastle), beeCastle, "TryAiContribute", null, nameof(BeeContributePre), nameof(FlagFin), null, done, miss);
            Wire(h, WBee, "BEE (obozy)", AccessTools.TypeByName(beeCastle), beeCastle, "TryAiBuildTrainingCamp", null, nameof(BeeCampPre), nameof(FlagFin), null, done, miss);
            Wire(h, WBee, "BEE (inwestycje we wsie)", AccessTools.TypeByName(beeInv), beeInv, "TryApplyLordInvestment", null, nameof(BeeInvestPre), nameof(FlagFin), null, done, miss);
            Wire(h, WBee, "BEE (dostep do targu)", AccessTools.TypeByName(beeDev), beeDev, "ApplyMarketAccess", null, nameof(BeeMarketPre), nameof(FlagFin), null, done, miss);
            // 169b: BetterEconomy - renta majatkow, wyplaty skarbcow, karawany (eskorta, awanse, dostawy, straty na drogach)
            const string beeFeud = "BetterEconomy.Behaviors.FeudalEconomyCampaignBehavior", beeTown = "BetterEconomy.Behaviors.TownEconomyCampaignBehavior",
                         beeTrade = "BetterEconomy.Behaviors.TradeAgreementCampaignBehavior", beeCar = "BetterEconomy.Behaviors.CaravanCampaignBehavior";
            Wire(h, WBeeEstates, "renta-majatkow-BEE", AccessTools.TypeByName(beeFeud), beeFeud, "TickEstateRent", null, nameof(BeeEstatesPre), nameof(FlagFin), null, done, miss);
            Wire(h, WBeePayout, "wyplaty-BEE (nadwyzka skarbca miasta)", AccessTools.TypeByName(beeTown), beeTown, "TryPayTreasurySurplus", null, nameof(BeePayoutPre), nameof(FlagFin), null, done, miss);
            Wire(h, WBeePayout, "wyplaty-BEE (umowy handlowe)", AccessTools.TypeByName(beeTrade), beeTrade, "AccrueCustoms", null, nameof(BeePayoutPre), nameof(FlagFin), null, done, miss);
            Wire(h, WBeeEscort, "BEE-eskorta", AccessTools.TypeByName(beeCar), beeCar, "TryHireEscort", null, nameof(BeeEscortPre), nameof(BeeCarFin), null, done, miss);
            Wire(h, WBeePromote, "BEE-awanse", AccessTools.TypeByName(beeCar), beeCar, "TryPromoteRoster", null, nameof(BeePromotePre), nameof(BeeCarFin), null, done, miss);
            Wire(h, WBeeDeliver, "BEE-dostawy", AccessTools.TypeByName(beeCar), beeCar, "TryDeliverContract", null, nameof(BeeDeliverPre), nameof(BeeCarFin), null, done, miss);
            Wire(h, WBeeDanger, "BEE-drogi", AccessTools.TypeByName(beeCar), beeCar, "ApplyDangerPressure", null, nameof(BeeDangerPre), nameof(BeeCarFin), null, done, miss);
            // 169b: BannerKings - dochod majatkow z produkcji (nic -> wlasciciel, kazda wies raz na dobe)
            const string bkEst = "BannerKings.Managers.Populations.Estates.EstateData";
            Wire(h, WBkEstates, "majatki-BK", AccessTools.TypeByName(bkEst), bkEst, "DailyProductionIncome", Type.EmptyTypes, nameof(BkEstatesPre), nameof(FlagFin), null, done, miss);
            // 169b: Diplomacy - odszkodowania wojenne przy pokoju (KingdomWalletCost.ApplyCost - wirtualna, wolana przez HybridCost)
            const string dipCost = "Diplomacy.Costs.KingdomWalletCost";
            Wire(h, WReparations, "odszkodowania", AccessTools.TypeByName(dipCost), dipCost, "ApplyCost", Type.EmptyTypes, nameof(ReparationsPre), nameof(FlagFin), null, done, miss);
            // warsztaty: okna na istniejacych latkach WorkshopTrade (wpiete wczesniej w SubModuleMain)
            Wired[WWorkshopOut] = HasOurPrefix(AccessTools.Method(typeof(WorkshopsCampaignBehavior), "ProduceAnOutputToTown"), "OutPrefix");
            Wired[WWorkshopIn] = HasOurPrefix(AccessTools.Method(typeof(WorkshopsCampaignBehavior), "ConsumeInputFromTownMarket"), "InPrefix");
            if (Wired[WWorkshopOut]) done.Add("warsztaty-wyrob"); else miss.Add("warsztaty-wyrob BRAK (latka WorkshopTrade na ProduceAnOutputToTown niewpieta)");
            if (Wired[WWorkshopIn]) done.Add("warsztaty-wsad"); else miss.Add("warsztaty-wsad BRAK (latka WorkshopTrade na ConsumeInputFromTownMarket niewpieta)");
            // nasluchy (ArmouryBehavior.RegisterEvents) - zawsze
            Wired[WPartyGone] = true; Wired[WKingdomGone] = true;
            done.Add("partie-znikaja (nasluch)"); done.Add("krolestwa (nasluch)");
            for (int w = 0; w < Windows; w++) if (!Wired[w] && Missing[w] == null) Missing[w] = "BRAK";
            foreach (var m in miss) { int sp = m.IndexOf(' '); string n = sp > 0 ? m.Substring(0, sp) : m; for (int w = 0; w < Windows; w++) if (WinName[w] == n && !Wired[w]) Missing[w] = m.Substring(sp + 1); }
            before = done.Count;
            bool toSeller = Settings.Current != null && Settings.Current.RecruitGoldToSeller;
            Log.Info("Obieg (169): ksiega obiegu (tylko log) - okna wpiete " + before + ": " + (done.Count > 0 ? string.Join(", ", done.ToArray()) : "zadne")
                     + (miss.Count > 0 ? "; BRAK: " + string.Join("; ", miss.ToArray()) : "")
                     + "; latki zakladane zawsze, liczenie przy CirculationLedgerEnabled i LogEnabled, probki swiata przy CirculationProbeEnabled (najwyzej " + ProbesPerDay + " na dobe)"
                     + "; linie modelu finansow i kapital karawan - w kampanii (osobne linie)"
                     + "; zaplata karawan za najemnikow do miast (RecruitGoldToSeller): " + (toSeller ? "tak" : "nie - najem przez karawane zostaje w reszcie")
                     + "; okno rodu z finalizerem: " + (MoneyLedger.ClanFinalizerWired ? "tak" : "nie") + ".");
        }
    }
}
