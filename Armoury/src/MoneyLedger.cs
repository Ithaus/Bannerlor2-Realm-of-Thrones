using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KSIEGA PIENIADZA I PRZEPLYWOW OSAD (krok K1 fundamentu, docs/EKONOMIA-FUNDAMENT-2026-10-05.md rozdz. 1.3 P9 i 6.3 K1).
    /// Tylko log - niczego nie zmienia w grze: odczyt stanu, nasluch zdarzen gry, postfiksy-liczniki i jeden prefiks-odczyt (zaden nie rusza wyniku).
    /// Fundament: szesc kluczowych kwot mapy przeplywow to szacunki, bo utarg wiesniakow, kasy osad, "zakupy" mieszczan
    /// i regulator kas nie byly logowane. Krytyk K1: ksiege oprzec na STANIE (jedyny mutator kasy osady to ChangeGold),
    /// a nie na liscie hakow - dzienna zmiana stanu minus pozycje zmierzone = jawna reszta.
    ///
    /// Raz na dobe (na koncu naszego rozliczenia doby):
    ///  "Pieniadz swiata:" - zloto wedlug posiadaczy (kasy miast i zamkow, kiesy wsi, kryjowki, bohaterowie, skarbce krolestw, kapital
    ///    warsztatow, sakiewki ludzi, Bank Zelazny, partie bez wodza, liczniki podatku) z suma i zmiana dobowa - wszystko pomiar stanu;
    ///  "Pieniadz swiata (bilans):" - zmiana sumy = zmierzone zrodla z niczego - zmierzone ujscia w nicosc + reszta;
    ///  "Pieniadz swiata (rody):" - o ile dzienne rozliczenia rodow zmienily zloto swiata (stan przed i po kazdym), saldo
    ///    dopisane glowom, zmiany poza saldem i zold naliczony ("w tym zold" - nie odejmowany w bilansie drugi raz);
    ///  "Przeplywy osad:" - utarg taborow wsi w miastach i zamkach, podzial utargu po powrocie do wsi, zold naliczony;
    ///  "Przeplywy osad (kasy miast | kasy zamkow | kiesy wsi):" - zmiana stanu rozbita na pozycje zmierzone i reszte.
    /// Kazda liczba ma znacznik: [P] pomiar (stan, zdarzenie gry albo licznik), [R] reszta z bilansu.
    ///
    /// Skad pomiary:
    ///  - utarg taborow: zdarzenia BeforeSettlementEntered / AfterSettlementEntered obejmuja cala sprzedaz i caly podzial utargu
    ///    (latka BK na VillagerCampaignBehavior.OnSettlementEntered biegnie miedzy nimi) - roznica zlota taboru, kiesy wsi
    ///    i licznika podatku wsi; wyplaty majatkow BK (GiveGoldAction z niczego w tym oknie) osobno;
    ///  - "zakupy" mieszczan: dwa postfiksy na ItemConsumptionBehavior - DeleteOverproducedItems (przed konsumpcja) i
    ///    MakeConsumption (po niej): roznica kasy = zloto dopisane za zjedzony towar (BK wola te sama sciezke dla zamkow);
    ///  - rozliczenie rodu: para prefiks / postfiks na ClanVariablesCampaignBehavior.DailyTickClan czyta sume WSZYSTKICH
    ///    posiadaczy tuz przed i tuz po rozliczeniu jednego rodu - roznica to dokladnie tyle zlota, ile rozliczenie stworzylo
    ///    albo skasowalo. Samego zdarzenia salda (GiveGoldAction nic -> glowa rodu) do bilansu NIE bierzemy: zold partii glowy
    ///    siedzi w saldzie, BK zdejmuje zold pozostalych partii z kies i wyrownuje je z salda (z doplata 200), rycerz z lennem
    ///    placi sam, dochod z cel i podatku wsi schodzi z licznikow, a pusta kiesa obcina ujemne saldo do zera - licznik zoldu
    ///    plus saldo liczyly zold dwa razy. Licznik zoldu (nizej) zostaje jako informacja "w tym zold";
    ///  - regulator kasy: postfiks na GetTownGoldChange czynnego modelu (czyta wynik, zaraz potem gra robi ChangeGold);
    ///  - przelewy gry: zdarzenie HeroOrPartyTradedGold (kazdy GiveGoldAction: zakupy lordow, karawany, notable);
    ///  - zold: postfiks na DefaultClanFinanceModel.CalculatePartyWage (BK wola ja refleksja dla kazdej partii i garnizonu);
    ///    czesc przekazana dalej przez SoldierPay (sakiewki ludzi, kasy osad) zglasza NoteWageRouted - w bilansie osobne zrodlo
    ///    ("zold oddany do obiegu"), bo SoldierPay wplaca ja juz po pomiarze rozliczenia rodu;
    ///  - nasze moduly poza tickiem dobowym: wywolania-liczniki Note (zakupy AI, najemnicy z karczmy, warsztaty zbrojne,
    ///    sprzet kupiony przez bandy u pasera);
    ///  - nasz tick dobowy: migawki stanu kas miedzy modulami (BlockOpen / Mark) - renty, budowy, korona, wydatki band
    ///    i kryjowek na zycie w miastach, skup lupu band u pasera (OutlawLaw robi swoje trzy migawki sam), reszta ticku.
    /// </summary>
    internal static class MoneyLedger
    {
        // klasy osad
        private const int CTown = 0, CCastle = 1, CVill = 2, Classes = 3;
        private static readonly string[] CName = { "kasy miast", "kasy zamkow", "kiesy wsi" };

        // druga strona przelewu gry (GiveGoldAction)
        private const int PLord = 0, PNotable = 1, PPlayer = 2, PHero = 3, PCaravan = 4, PParty = 5, PSettlement = 6, Sides = 7;
        private static readonly string[] PName = { "lordowie", "notable", "gracz", "inni bohaterowie", "karawany", "inne partie", "inne osady" };

        // nasze moduly liczone wprost, poza tickiem dobowym (Note)
        internal const int NGear = 0, NMerc = 1, NShop = 2, NFence = 3, NWage = 4, NLife = 5;
        private const int Notes = 6;
        private static readonly string[] NName = { "zakupy sprzetu AI", "najemnicy z karczmy", "warsztaty zbrojne", "paser band (sprzet dla band)", "zold garnizonow", "sakiewki ludzi - zycie w miastach" };

        // nasz tick dobowy (Mark)
        internal const int MRent = 0, MBuild = 1, MCrown = 2, MRest = 3, MFence = 4, MLife = 5;
        private const int Marks = 6;
        private static readonly string[] MName = { "renty", "budowy", "korona (danina, clo, mennica)", "pozostale moduly ticku", "paser band (skup lupu)", "bandy i kryjowki (zycie w miastach)" };

        // posiadacze zlota
        private const int HTowns = 0, HCastles = 1, HVillages = 2, HLeaders = 3, HLords = 4, HPlayer = 5, HNotables = 6, HWanderers = 7, HOtherHeroes = 8,
                          HKingdoms = 9, HWorkshops = 10, HPurses = 11, HBank = 12, HVillagers = 13, HCaravans = 14, HGarrisons = 15, HBandits = 16,
                          HOtherParties = 17, HTownTax = 18, HVillageTax = 19, HOtherSettlements = 20, Holders = 21;

        // zold wedlug rodzaju partii
        private const int WLord = 0, WGarrison = 1, WCaravan = 2, WOther = 3, Wages = 4;
        private static readonly string[] WName = { "partie rodow", "garnizony", "karawany", "inne" };

        // ------------------------------------------------------------ stan miedzy dobami
        private static bool _first = true;                 // pierwsza doba po Reset: sam stan, bez zmian i przeplywow
        private static bool _modelsLogged;
        private static long[] _lastSnap, _lastHold;        // kasy i posiadacze na koniec poprzedniej doby
        private static long[] _blockSnap;                  // ostatnia migawka kas w naszym ticku dobowym
        private static bool _inBlock;                      // trwa nasz tick dobowy - zmiany kas lapia migawki, nie liczniki

        // ------------------------------------------------------------ liczniki doby
        private static readonly long[] _vPaid = new long[Classes];
        private static readonly int[] _vVisits = new int[Classes], _vUnsold = new int[Classes], _vUnsoldVisits = new int[Classes];
        private static long _vHanded, _vKept, _vTax, _vEstates;
        private static int _vReturns;
        private static readonly long[] _cons = new long[Classes], _regIn = new long[Classes], _regOut = new long[Classes];
        private static readonly int[] _consTicks = new int[Classes], _regTicks = new int[Classes];
        private static int _consMissed, _regStray;
        private static readonly long[,] _goldIn = new long[Classes, Sides], _goldOut = new long[Classes, Sides];
        private static readonly long[] _fromNothing = new long[Classes], _toNothing = new long[Classes];
        private static long _worldFromNothing, _worldToNothing, _levyBack;
        private static readonly long[,] _noteIn = new long[Classes, Notes], _noteOut = new long[Classes, Notes];
        private static readonly long[,] _mark = new long[Classes, Marks];
        private static readonly long[] _wage = new long[Wages];
        private static readonly int[] _wageN = new int[Wages], _wageShort = new int[Wages];
        private static long _wageToPurses, _wageToCoffers; // zold, ktory nie zniknal: SoldierPay przekazal go do sakiewek ludzi i kas osad
        private static int _stumbles;                      // potkniecia licznikow (wyjatek zlapany przy jednym zdarzeniu) - liczymy, nie gasimy

        // okno "tabor wsi wchodzi do osady"
        private static MobileParty _winParty;
        private static bool _winVillage;                   // okno powrotu do wsi (podzial utargu) - tylko wtedy wyplaty "z niczego" to majatki BK
        private static CampaignTime _winTime;              // chwila otwarcia okna (porownanie tikow, nie ulamkow godzin)
        private static int _winGold, _winHomeGold, _winHomeTax, _winEstates, _winStale;

        // rozliczenie rodu (ClanVariablesCampaignBehavior.DailyTickClan): zloto swiata przed i po - pomiar stanu
        private static bool _clanHooked;                   // para na DailyTickClan zalozona (bez niej salda rodow ida do pozycji GiveGoldAction)
        private static Clan _clanNow;                      // rod, ktorego rozliczenie wlasnie biegnie
        private static CampaignTime _clanTime;             // chwila otwarcia (rozliczenie nie domkniete przez wyjatek nie lapie pozniejszych zdarzen)
        private static long _clanBefore;                   // suma wszystkich posiadaczy tuz przed rozliczeniem
        private static long _clanUp, _clanDown;            // rozliczenia, ktore dodaly zlota swiatu / ktore je skasowaly (suma zmian)
        private static int _clanUpN, _clanDownN, _clanFlatN, _clanStale;
        private static long _netUp, _netDown;              // saldo modelu finansow dopisane glowom rodow (kwota ze zdarzenia gry)
        private static int _netUpN, _netDownN;
        private static long _clanClsFrom, _clanClsTo;      // GiveGoldAction nic <-> osada w trakcie rozliczenia (sa tez w licznikach kas osad)
        private static long _clanOthFrom, _clanOthTo;      // GiveGoldAction nic <-> ktos inny niz glowa rodu w trakcie rozliczenia
        private static bool _clanErrLogged;                // pierwszy wyjatek pomiaru rozliczenia idzie do pliku, kolejne tylko liczymy

        // nawias konsumpcji i regulatora
        private static Town _shelfTown, _regExpect;
        private static int _shelfGold;
        private static object _regModel;
        private static Type _regDecl;

        internal static void Reset()
        {
            _first = true; _modelsLogged = false; _lastSnap = null; _lastHold = null; _blockSnap = null; _inBlock = false;
            _winParty = null; _winVillage = false; _shelfTown = null; _regExpect = null; _regModel = null; _regDecl = null;
            _clanNow = null; _clanErrLogged = false;
            ClearDay();
        }

        private static void ClearDay()
        {
            Array.Clear(_vPaid, 0, Classes); Array.Clear(_vVisits, 0, Classes); Array.Clear(_vUnsold, 0, Classes); Array.Clear(_vUnsoldVisits, 0, Classes);
            _vHanded = _vKept = _vTax = _vEstates = 0; _vReturns = 0;
            _clanUp = _clanDown = _netUp = _netDown = _clanClsFrom = _clanClsTo = _clanOthFrom = _clanOthTo = 0;
            _clanUpN = _clanDownN = _clanFlatN = _clanStale = _netUpN = _netDownN = 0;
            Array.Clear(_cons, 0, Classes); Array.Clear(_regIn, 0, Classes); Array.Clear(_regOut, 0, Classes);
            Array.Clear(_consTicks, 0, Classes); Array.Clear(_regTicks, 0, Classes);
            _consMissed = 0; _regStray = 0;
            Array.Clear(_goldIn, 0, _goldIn.Length); Array.Clear(_goldOut, 0, _goldOut.Length);
            Array.Clear(_fromNothing, 0, Classes); Array.Clear(_toNothing, 0, Classes);
            _worldFromNothing = _worldToNothing = _levyBack = 0;
            Array.Clear(_shopWage, 0, Classes); Array.Clear(_shopKeep, 0, Classes); Array.Clear(_shopFair, 0, Classes);
            Array.Clear(_artIn, 0, Classes); Array.Clear(_artBack, 0, Classes);
            Array.Clear(_noteIn, 0, _noteIn.Length); Array.Clear(_noteOut, 0, _noteOut.Length);
            Array.Clear(_mark, 0, _mark.Length);
            Array.Clear(_wage, 0, Wages); Array.Clear(_wageN, 0, Wages); Array.Clear(_wageShort, 0, Wages);
            _wageToPurses = _wageToCoffers = 0;
            _stumbles = 0; _winStale = 0;
        }

        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private static int ClassOf(Settlement st)
        {
            if (st == null) return -1;
            return st.IsTown ? CTown : (st.IsCastle ? CCastle : (st.IsVillage ? CVill : -1));
        }

        private static string S(long n) { return (n >= 0 ? "+" : "") + n; }

        /// <summary>Kwota ubytku do linii logu: "-123", a przy zerze "0" (nie "-0").</summary>
        private static string Neg(long n) { return n > 0 ? "-" + n : "0"; }

        private static string Pct(long part, long whole) { return whole != 0 ? (100.0 * part / whole).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%" : "-"; }

        // ------------------------------------------------------------ utarg taborow wsi (zdarzenia gry)
        private static bool WinOpen { get { return _winParty != null && _winTime == CampaignTime.Now; } }

        /// <summary>Przed wejsciem taboru wsi do osady: zloto taboru, kiesa i licznik podatku jego wsi.</summary>
        internal static void OnBeforeEntered(MobileParty mp, Settlement st, Hero hero)
        {
            try
            {
                if (mp == null || st == null || !mp.IsVillager || !Live) return;
                if (_winParty != null) _winStale++;        // poprzednie okno nie domkniete (wyjatek w cudzym nasluchu) - liczymy
                var home = mp.HomeSettlement != null ? mp.HomeSettlement.Village : null;
                _winParty = mp; _winVillage = st.IsVillage; _winTime = CampaignTime.Now;
                _winGold = mp.PartyTradeGold; _winEstates = 0;
                _winHomeGold = home != null ? home.Gold : 0;
                _winHomeTax = home != null ? home.TradeTaxAccumulated : 0;
            }
            catch { _stumbles++; }
        }

        /// <summary>Po wejsciu: w miescie albo zamku - ile osada zaplacila; we wsi - jak rozszedl sie utarg.</summary>
        internal static void OnAfterEntered(MobileParty mp, Settlement st, Hero hero)
        {
            try
            {
                if (mp == null || !ReferenceEquals(mp, _winParty)) return;
                _winParty = null;
                if (st == null) return;
                int now = mp.PartyTradeGold;
                if (st.Town != null)
                {
                    int c = st.IsTown ? CTown : CCastle;
                    _vPaid[c] += now - _winGold; _vVisits[c]++;
                    int left = Unsold(mp);
                    if (left > 0) { _vUnsold[c] += left; _vUnsoldVisits[c]++; }
                }
                else if (st.IsVillage)
                {
                    int handed = _winGold - now;
                    if (handed == 0) return;                // powrot bez utargu
                    var home = mp.HomeSettlement != null ? mp.HomeSettlement.Village : null;
                    _vHanded += handed; _vReturns++;
                    if (home != null) { _vKept += home.Gold - _winHomeGold; _vTax += home.TradeTaxAccumulated - _winHomeTax; }
                    _vEstates += _winEstates;
                }
            }
            catch { _stumbles++; }
        }

        /// <summary>Sztuki towaru, ktore zostaly w taborze po sprzedazy (bez zwierzat jucznych, ktore tabor zatrzymuje).</summary>
        private static int Unsold(MobileParty mp)
        {
            int n = 0;
            var r = mp.ItemRoster;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || it.ItemCategory == DefaultItemCategories.PackAnimal) continue;
                n += el.Amount;
            }
            return n;
        }

        // ------------------------------------------------------------ przelewy gry (zdarzenie GiveGoldAction)
        private static int SideOf(Hero h, PartyBase p)
        {
            if (h != null) return h == Hero.MainHero ? PPlayer : (h.IsNotable ? PNotable : (h.IsLord ? PLord : PHero));
            if (p != null && p.IsSettlement) return PSettlement;
            var mp = p != null ? p.MobileParty : null;
            if (mp == null) return PParty;
            if (mp.IsMainParty) return PPlayer;
            return mp.IsCaravan ? PCaravan : (mp.IsLordParty ? PLord : PParty);   // partia rodu placaca jako partia (bez bohatera w przelewie) to tez lord
        }

        internal static void OnGoldTraded((Hero, PartyBase) giver, (Hero, PartyBase) recipient, (int, string) amount, bool showNotification)
        {
            try
            {
                int a = amount.Item1;
                if (a == 0 || !Live) return;
                Hero gh = giver.Item1, rh = recipient.Item1;
                PartyBase gp = giver.Item2, rp = recipient.Item2;
                if (a < 0) { var th = gh; gh = rh; rh = th; var tp = gp; gp = rp; rp = tp; a = -a; }   // gra zapisuje "osada -> bohater" ujemna kwota w druga strone
                bool gNone = gh == null && gp == null, rNone = rh == null && rp == null;
                if (gNone && rNone) return;
                // utarg wsi: wyplata dla wlasciciela majatku BK w oknie POWROTU taboru do wsi to czesc utargu, nie zloto z niczego
                // (okno wizyty w miescie albo zamku tego nie lapie - tam nikt utargu nie dzieli)
                if (gNone && rh != null && _winVillage && WinOpen) { _winEstates += a; return; }
                int gc = gh == null && gp != null && gp.IsSettlement ? ClassOf(gp.Settlement) : -1;
                int rc = rh == null && rp != null && rp.IsSettlement ? ClassOf(rp.Settlement) : -1;
                // w trakcie rozliczenia rodu zloto "z niczego" i "w nicosc" jest juz w roznicy stanu zlota swiata (ClanTickPrefix /
                // ClanTickPostfix) - do bilansu swiata nie wchodzi drugi raz; saldo dopisane glowie rodu notujemy osobno (informacja)
                var clan = ClanOpen ? _clanNow : null;
                if (gNone)
                {
                    if (rc >= 0) { if (!_inBlock) { _fromNothing[rc] += a; if (clan != null) _clanClsFrom += a; } }
                    else if (clan == null) _worldFromNothing += a;
                    else if (rh != null && ReferenceEquals(rh, clan.Leader)) { _netUp += a; _netUpN++; }
                    else _clanOthFrom += a;
                    return;
                }
                if (rNone)
                {
                    if (gc >= 0) { if (!_inBlock) { _toNothing[gc] += a; if (clan != null) _clanClsTo += a; } }
                    else if (clan == null) _worldToNothing += a;
                    else if (gh != null && ReferenceEquals(gh, clan.Leader)) { _netDown += a; _netDownN++; }
                    else _clanOthTo += a;
                    return;
                }
                if (_inBlock) return;                       // nasz tick dobowy: zmiane kas lapia migawki (Mark)
                if (gc >= 0) _goldOut[gc, SideOf(rh, rp)] += a;
                if (rc >= 0) _goldIn[rc, SideOf(gh, gp)] += a;
            }
            catch { _stumbles++; }
        }

        // ------------------------------------------------------------ rozliczenie rodu (stan zlota swiata przed i po)
        private static bool ClanOpen { get { return _clanNow != null && _clanTime == CampaignTime.Now; } }

        /// <summary>
        /// ClanVariablesCampaignBehavior.DailyTickClan - tuz przed dziennym rozliczeniem rodu: suma wszystkich posiadaczy zlota.
        /// Tylko odczyt (prefiks void - oryginalu nie pomija). Rody band gra pomija, wiec ich nie mierzymy.
        /// </summary>
        public static void ClanTickPrefix(Clan __0)
        {
            try
            {
                if (_clanNow != null) { _clanStale++; _clanNow = null; }   // poprzednie rozliczenie nie doszlo do postfiksu (wyjatek w cudzym kodzie) - liczymy
                if (__0 == null || __0.IsBanditFaction || !Live) return;
                _clanBefore = WorldTotal();
                _clanTime = CampaignTime.Now; _clanNow = __0;
            }
            catch (Exception e) { _clanNow = null; ClanStumble("MoneyLedger.ClanTickPrefix", e); }
        }

        /// <summary>Po rozliczeniu rodu: o ile zmienilo sie zloto swiata (saldo, zold, kiesy partii, liczniki podatkow, skarbce - wszystko naraz).</summary>
        public static void ClanTickPostfix(Clan __0)
        {
            try
            {
                var c = _clanNow;
                _clanNow = null;
                if (c == null) return;
                if (!ReferenceEquals(c, __0)) { _clanStale++; return; }
                long d = WorldTotal() - _clanBefore;
                if (d > 0) { _clanUp += d; _clanUpN++; }
                else if (d < 0) { _clanDown -= d; _clanDownN++; }
                else _clanFlatN++;
            }
            catch (Exception e) { ClanStumble("MoneyLedger.ClanTickPostfix", e); }
        }

        /// <summary>Wyjatek przy pomiarze jednego rozliczenia: pierwszy do pliku, kazdy liczony w "Potkniecia licznikow" (pomiaru nie gasimy).</summary>
        private static void ClanStumble(string where, Exception e)
        {
            _stumbles++;
            if (_clanErrLogged) return;
            _clanErrLogged = true;
            Log.Error(where, e);
        }

        // ------------------------------------------------------------ nasze moduly: wywolania-liczniki
        /// <summary>Nasz modul zmienil kase osady poza tickiem dobowym (kwota ze znakiem: + do kasy, - z kasy). Tylko licznik.</summary>
        internal static void Note(int kind, Settlement st, int amount)
        {
            try
            {
                if (_inBlock || amount == 0 || kind < 0 || kind >= Notes) return;
                int c = ClassOf(st);
                if (c < 0) return;
                if (amount > 0) _noteIn[c, kind] += amount; else _noteOut[c, kind] -= amount;
            }
            catch { _stumbles++; }
        }

        /// <summary>
        /// LevyGold oddal notablowi albo miastu zloto, ktore gra przy tym samym werbunku skasowala (GiveGoldAction wodz -> nic,
        /// policzone w "GiveGoldAction w nicosc"). Wolac TYLKO wtedy, gdy gra naprawde kasowala przez GiveGoldAction: karawana
        /// placi za najemnika z kiesy partii bez zdarzenia - jej zaplata to zwykly przelew karawana -> miasto, nie zwrot. Tylko licznik.
        /// </summary>
        internal static void NoteLevyBack(int amount) { if (amount > 0) _levyBack += amount; }

        /// <summary>SoldierPay przekazal zaplacony zold dalej (sakiewka ludzi albo kasa osady) - w bilansie osobne zrodlo "zold oddany do obiegu". Tylko licznik.</summary>
        internal static void NoteWageRouted(bool toPurse, int amount)
        {
            if (amount <= 0) return;
            if (toPurse) _wageToPurses += amount; else _wageToCoffers += amount;
        }

        // warsztaty towarowe (WorkshopTrade): place za cykle i dzienne utrzymanie przekazane z KAPITALU warsztatu do kasy miasta -
        // osobna pozycja linii "Przeplywy osad (kasy miast)". To przelew miedzy posiadaczami (kapital warsztatow -> kasy miast),
        // zlota swiata nie zmienia, wiec do bilansu "Pieniadz swiata" nie wchodzi. Wplaty z kiesy gracza ida przez GiveGoldAction
        // i sa w pozycji "przelewy gry".
        private static readonly long[] _shopWage = new long[Classes], _shopKeep = new long[Classes];
        // 123: nadwyzka ceny wyrobu ponad koszt cyklu + marze (cena sprawiedliwa) oddana z kapitalu warsztatu do kasy miasta
        private static readonly long[] _shopFair = new long[Classes];

        /// <summary>WorkshopTrade wplacil do kasy osady place za cykle i utrzymanie warsztatu z jego kapitalu. Tylko licznik.</summary>
        internal static void NoteWorkshopPay(Settlement st, int wages, int upkeep)
        {
            try
            {
                if (_inBlock) return;                       // w naszym ticku dobowym zmiane kas lapia migawki (Mark)
                int c = ClassOf(st);
                if (c < 0) return;
                if (wages > 0) _shopWage[c] += wages;
                if (upkeep > 0) _shopKeep[c] += upkeep;
            }
            catch { _stumbles++; }
        }

        /// <summary>123: WorkshopTrade oddal kasie miasta nadwyzke ceny wyrobu (cena sprawiedliwa) z kapitalu warsztatu. Tylko licznik.</summary>
        internal static void NoteWorkshopFair(Settlement st, int amount)
        {
            try
            {
                if (_inBlock || amount <= 0) return;
                int c = ClassOf(st);
                if (c >= 0) _shopFair[c] += amount;
            }
            catch { _stumbles++; }
        }

        // 147: rzemieslnicy BK (ArtisanInputs) - wsad dodatkowych cykli zaplacony kasie miasta i zwrot zaplaty BK za sztuki bez wsadu,
        // oba z kapitalu ukrytego warsztatu (te same dwie kasy co prefiks BK; zlota swiata nie zmienia)
        private static readonly long[] _artIn = new long[Classes], _artBack = new long[Classes];

        /// <summary>147: kapital rzemieslnikow BK -> kasa miasta (wsad dodatkowych cykli, zwrot za zdjete sztuki). Tylko licznik.</summary>
        internal static void NoteArtisans(Settlement st, int inputs, int refund)
        {
            try
            {
                if (_inBlock) return;
                int c = ClassOf(st);
                if (c < 0) return;
                if (inputs > 0) _artIn[c] += inputs;
                if (refund > 0) _artBack[c] += refund;
            }
            catch { _stumbles++; }
        }

        // ------------------------------------------------------------ nasz tick dobowy: migawki kas
        private static long[] Snap()
        {
            var s = new long[Classes];
            foreach (var st in Settlement.All)
            {
                if (st == null || st.SettlementComponent == null) continue;
                int c = ClassOf(st);
                if (c >= 0) s[c] += st.SettlementComponent.Gold;
            }
            return s;
        }

        /// <summary>Poczatek naszego rozliczenia doby: stan kas przed pierwszym modulem.</summary>
        internal static void BlockOpen()
        {
            try { _blockSnap = Snap(); _inBlock = true; }
            catch (Exception e) { _inBlock = false; _blockSnap = null; Log.Error("MoneyLedger.BlockOpen", e); }
        }

        /// <summary>Po module naszego ticku: zmiana kas od poprzedniej migawki idzie na jego konto.</summary>
        internal static void Mark(int kind)
        {
            try
            {
                if (!_inBlock || _blockSnap == null || kind < 0 || kind >= Marks) return;
                var s = Snap();
                for (int c = 0; c < Classes; c++) _mark[c, kind] += s[c] - _blockSnap[c];
                _blockSnap = s;
            }
            catch (Exception e) { Log.Error("MoneyLedger.Mark", e); }
        }

        // ------------------------------------------------------------ postfiksy-liczniki (nie zmieniaja wyniku)
        /// <summary>ItemConsumptionBehavior.DeleteOverproducedItems - pierwszy krok dziennej konsumpcji osady: kasa PRZED "zakupami".</summary>
        public static void ShelfPostfix(Town __0)
        {
            try
            {
                if (__0 == null || !Live) return;
                _shelfTown = __0; _shelfGold = __0.Gold;
            }
            catch { _stumbles++; }
        }

        /// <summary>ItemConsumptionBehavior.MakeConsumption - kasa PO "zakupach" mieszkancow (towar znika, kasa dostaje jego cene).</summary>
        public static void ConsumePostfix(Town __0)
        {
            try
            {
                if (__0 == null || !Live) return;
                if (!ReferenceEquals(__0, _shelfTown)) { _consMissed++; return; }
                _shelfTown = null;
                int c = __0.IsTown ? CTown : CCastle;
                _cons[c] += __0.Gold - _shelfGold; _consTicks[c]++;
                _regExpect = __0;                           // zaraz po konsumpcji gra pyta model o regulator tej samej osady
            }
            catch { _stumbles++; }
        }

        /// <summary>SettlementEconomyModel.GetTownGoldChange - dzienna dosypka albo kasowanie kasy osady (gra robi ChangeGold wynikiem).</summary>
        public static void RegulatorPostfix(object __instance, MethodBase __originalMethod, Town __0, int __result)
        {
            try
            {
                if (__0 == null || !Live) return;
                var active = Campaign.Current.Models.SettlementEconomyModel;
                if (!ReferenceEquals(__instance, active)) return;                  // model opakowany przez inny - liczy zewnetrzny
                if (!ReferenceEquals(active, _regModel)) { _regModel = active; _regDecl = DeclOf(active.GetType()); }
                if (__originalMethod == null || __originalMethod.DeclaringType != _regDecl) return;   // metoda bazowa wolana przez nadpisanie
                if (!ReferenceEquals(__0, _regExpect)) { _regStray++; return; }    // pytanie spoza dziennej konsumpcji (nikt nie zmienia kasy)
                _regExpect = null;
                int c = __0.IsTown ? CTown : CCastle;
                if (__result >= 0) _regIn[c] += __result; else _regOut[c] -= __result;
                _regTicks[c]++;
            }
            catch { _stumbles++; }
        }

        /// <summary>Czy gra zaraz zapyta o regulator tej osady w jej dziennym ticku (zaraz po konsumpcji) - odczyt dla tarczy zoldu (SoldierPay).</summary>
        internal static bool RegulatorDue(Town town) { return town != null && ReferenceEquals(town, _regExpect); }

        private static Type DeclOf(Type t)
        {
            try
            {
                var m = t.GetMethod("GetTownGoldChange", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Town) }, null);
                return m != null ? m.DeclaringType : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// DefaultClanFinanceModel.CalculatePartyWage(partia, budzet, applyWithdrawals) - wynik przy rozliczeniu = zold NALICZONY
        /// (min(zold partii, budzet rodu)). To informacja "w tym zold", nie pozycja bilansu: kwota siedzi w rozliczeniu rodu
        /// (saldo albo kiesa partii), a z pustej kiesy schodzi mniej, niz naliczono.
        /// </summary>
        public static void WagePostfix(MobileParty __0, bool __2, int __result)
        {
            try
            {
                if (!__2 || __0 == null || !Live) return;
                int k = __0.IsGarrison ? WGarrison : (__0.IsCaravan ? WCaravan : (__0.IsLordParty ? WLord : WOther));
                _wage[k] += __result; _wageN[k]++;
                if (__0.HasUnpaidWages > 0f) _wageShort[k]++;
            }
            catch { _stumbles++; }
        }

        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>(); var miss = new List<string>();
            try
            {
                var shelf = AccessTools.Method(typeof(ItemConsumptionBehavior), "DeleteOverproducedItems");
                var cons = AccessTools.Method(typeof(ItemConsumptionBehavior), "MakeConsumption");
                if (shelf != null && cons != null)
                {
                    h.Patch(shelf, postfix: new HarmonyMethod(typeof(MoneyLedger), nameof(ShelfPostfix)));
                    h.Patch(cons, postfix: new HarmonyMethod(typeof(MoneyLedger), nameof(ConsumePostfix)));
                    done.Add("konsumpcja mieszkancow");
                }
                else miss.Add("konsumpcja mieszkancow");
            }
            catch (Exception e) { miss.Add("konsumpcja mieszkancow (" + e.Message + ")"); }
            int reg = 0;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types; } catch { continue; }
                    foreach (var t in types)
                    {
                        try
                        {
                            if (t == null || t.IsAbstract || !typeof(SettlementEconomyModel).IsAssignableFrom(t)) continue;
                            var m = t.GetMethod("GetTownGoldChange", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, new[] { typeof(Town) }, null);
                            if (m == null || m.IsAbstract) continue;
                            h.Patch(m, postfix: new HarmonyMethod(typeof(MoneyLedger), nameof(RegulatorPostfix)));
                            reg++;
                        }
                        catch { }
                    }
                }
            }
            catch (Exception e) { Log.Error("MoneyLedger.ApplyAll(regulator)", e); }
            if (reg > 0) done.Add("regulator kasy w " + reg + " modelach"); else miss.Add("regulator kasy");
            try
            {
                var wage = AccessTools.Method(typeof(DefaultClanFinanceModel), "CalculatePartyWage");
                if (wage != null) { h.Patch(wage, postfix: new HarmonyMethod(typeof(MoneyLedger), nameof(WagePostfix))); done.Add("zold"); }
                else miss.Add("zold");
            }
            catch (Exception e) { miss.Add("zold (" + e.Message + ")"); }
            try
            {
                // rozliczenie rodu: prefiks jak najwczesniej, postfiks po latkach innych modow, ale PRZED latkami o priorytecie Last
                // (pomiar obejmuje rozliczenie gry i BK, a nie to, co ktos dopisuje po nim)
                var tick = AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "DailyTickClan");
                if (tick != null)
                {
                    h.Patch(tick, prefix: new HarmonyMethod(typeof(MoneyLedger), nameof(ClanTickPrefix)) { priority = Priority.First },
                                  postfix: new HarmonyMethod(typeof(MoneyLedger), nameof(ClanTickPostfix)) { priority = Priority.Low });
                    _clanHooked = true;
                    done.Add("rozliczenie rodow");
                }
                else miss.Add("rozliczenie rodow");
            }
            catch (Exception e) { miss.Add("rozliczenie rodow (" + e.Message + ")"); }
            Log.Info("MoneyLedger: ksiega pieniadza i przeplywow osad (tylko log) - liczniki wpiete: " + (done.Count > 0 ? string.Join(", ", done.ToArray()) : "zadne")
                     + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "") + "; utarg taborow i przelewy gry - z nasluchu zdarzen.");
        }

        // ------------------------------------------------------------ posiadacze zlota (stan)
        private static long[] ReadHolders(long[] snap)
        {
            var h = new long[Holders];
            h[HTowns] = snap[CTown]; h[HCastles] = snap[CCastle]; h[HVillages] = snap[CVill];
            // kryjowki i inne osady spoza trzech klas: w kasie kryjowki lezy to, co bandy odlozyly z wlasnych kies (OutlawLaw),
            // a wychodzi z niej kiesa startowa nowych band, doplata do sprzetu i wydatki na zycie w miastach - same przelewy;
            // dosypka gry - 25% wartosci jukow przy kazdym wejsciu bandy (BanditSpawnCampaignBehavior) - jest zamknieta
            foreach (var st in Settlement.All)
                if (st != null && st.SettlementComponent != null && ClassOf(st) < 0) h[HOtherSettlements] += st.SettlementComponent.Gold;
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null) continue;
                int g = hero.Gold;
                if (g == 0) continue;
                int k;
                if (hero == Hero.MainHero) k = HPlayer;
                else if (hero.IsNotable) k = HNotables;
                else if (hero.IsLord) k = hero.Clan != null && hero.Clan.Leader == hero ? HLeaders : HLords;
                else if (hero.IsWanderer) k = HWanderers;
                else k = HOtherHeroes;
                h[k] += g;
            }
            foreach (var kd in Kingdom.All) if (kd != null && !kd.IsEliminated) h[HKingdoms] += kd.KingdomBudgetWallet;
            foreach (var t in Town.AllFiefs)
            {
                if (t == null) continue;
                h[HTownTax] += t.TradeTaxAccumulated;
                var ws = t.Workshops;
                if (ws != null) foreach (var w in ws) if (w != null) h[HWorkshops] += w.Capital;
            }
            foreach (var v in Village.All) if (v != null) h[HVillageTax] += v.TradeTaxAccumulated;
            h[HPurses] = MenPurse.TotalNow();
            h[HBank] = IronBank.CapitalNow;
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || !mp.IsActive) continue;
                if (mp.IsLordParty && mp.LeaderHero != null) continue;          // zloto partii z wodzem to kiesa wodza - juz policzona
                int g = mp.PartyTradeGold;
                if (g == 0) continue;
                int k = mp.IsVillager ? HVillagers : (mp.IsCaravan ? HCaravans : (mp.IsGarrison ? HGarrisons : (mp.IsBandit ? HBandits : HOtherParties)));
                h[k] += g;
            }
            return h;
        }

        /// <summary>Zloto swiata teraz: suma tych samych posiadaczy, ktorych pokazuje linia "Pieniadz swiata:" (jedna definicja - bez rozjazdu).</summary>
        private static long WorldTotal()
        {
            var h = ReadHolders(Snap());
            long t = 0;
            for (int i = 0; i < Holders; i++) t += h[i];
            return t;
        }

        private static string D(long[] now, long[] last, int i) { return now[i] + (last != null ? " (" + S(now[i] - last[i]) + ")" : ""); }

        private static string StateLine(int day, long[] now, long[] last)
        {
            long total = 0, lastTotal = 0;
            for (int i = 0; i < Holders; i++) { total += now[i]; if (last != null) lastTotal += last[i]; }
            return "Pieniadz swiata: dzien " + day + " | razem " + total + (last != null ? " (" + S(total - lastTotal) + ")" : "")
                   + " | osady: kasy miast " + D(now, last, HTowns) + ", kasy zamkow " + D(now, last, HCastles) + ", kiesy wsi " + D(now, last, HVillages)
                   + ", kryjowki i inne " + D(now, last, HOtherSettlements)
                   + " | bohaterowie: glowy rodow " + D(now, last, HLeaders) + ", pozostali lordowie " + D(now, last, HLords) + ", gracz " + D(now, last, HPlayer)
                   + ", notable " + D(now, last, HNotables) + ", wedrowcy " + D(now, last, HWanderers) + ", inni " + D(now, last, HOtherHeroes)
                   + " | skarbce krolestw " + D(now, last, HKingdoms) + " | kapital warsztatow " + D(now, last, HWorkshops)
                   + " | sakiewki ludzi " + D(now, last, HPurses) + " | Bank Zelazny " + D(now, last, HBank)
                   + " | partie bez wodza: tabory wsi " + D(now, last, HVillagers) + ", karawany " + D(now, last, HCaravans) + ", garnizony " + D(now, last, HGarrisons)
                   + ", bandy " + D(now, last, HBandits) + ", inne " + D(now, last, HOtherParties)
                   + " | liczniki (zloto w drodze do panow): cla miast i zamkow " + D(now, last, HTownTax) + ", podatek wsi " + D(now, last, HVillageTax)
                   + ". Wszystko [P] - stan na koniec doby, w nawiasach zmiana dobowa.";
        }

        private static string BalanceLine(int day, long[] now, long[] last)
        {
            long total = 0, lastTotal = 0;
            for (int i = 0; i < Holders; i++) { total += now[i]; lastTotal += last[i]; }
            long delta = total - lastTotal;
            long cons = _cons[CTown] + _cons[CCastle], regIn = _regIn[CTown] + _regIn[CCastle], regOut = _regOut[CTown] + _regOut[CCastle];
            // GiveGoldAction z niczego / w nicosc POZA rozliczeniami rodow: te z rozliczen siedza w roznicy stanu (_clanUp / _clanDown);
            // z licznikow kas osad (wspolnych z liniami "Przeplywy osad") odejmujemy czesc zlapana w trakcie rozliczen
            long from = _worldFromNothing - _clanClsFrom, to = _worldToNothing - _clanClsTo;
            for (int c = 0; c < Classes; c++) { from += _fromNothing[c]; to += _toNothing[c]; }
            long wages = 0; for (int k = 0; k < Wages; k++) wages += _wage[k];
            long vanished = _vHanded - _vKept - _vTax - _vEstates;
            // zold NIE jest osobnym ujsciem: partii glowy rodu siedzi w saldzie, pozostalych - w kiesach wyrownywanych z salda;
            // cale rozliczenie rodu (saldo, kiesy, liczniki podatkow, skarbce) mierzy roznica stanu zlota swiata
            // Pozycja "GiveGoldAction z niczego" zawiera tez dzienny dochod notabli (ClanVariablesCampaignBehavior.DailyTickHero ->
            // CalculateNotableDailyGoldChange): gra zdejmuje go z kapitalu warsztatow i kies karawan (Workshop.ChangeGold, PartyTradeGold),
            // a wyplaca zdarzeniem nic -> notabl. Ubytek kapitalu nie ma wlasnej pozycji - siedzi w reszcie ze znakiem minus (mowi to opis reszty).
            // SoldierPay (ogniwo 107) oddaje zaplacony zold do obiegu PO pomiarze rozliczenia (jego postfiks na DailyTickClan ma priorytet
            // Last, nasz Low): sakiewki ludzi i kasy osad rosna poza roznica stanu rozliczenia - to osobne zrodlo, nie "minus" w ujsciach
            // (rozliczenia na minus to tylko czesc rodow; odjecie calego przekazanego zoldu dawaloby ujemne ujscia)
            long routed = _wageToPurses + _wageToCoffers;
            long sources = cons + regIn + _clanUp + routed + from;
            long sinks = _clanDown + regOut + vanished + to - _levyBack;
            return "Pieniadz swiata (bilans): dzien " + day + " | zmiana sumy " + S(delta) + " [P] = zmierzone zrodla z niczego +" + sources
                   + " [P] (\"zakupy\" mieszkancow miast i zamkow " + cons + ", regulator kas dosypal " + regIn + ", rozliczenia rodow na plus " + _clanUp + " w " + _clanUpN
                   + " rodach, zold oddany do obiegu przez SoldierPay " + routed + " (sakiewki ludzi " + _wageToPurses + ", kasy osad " + _wageToCoffers
                   + "), GiveGoldAction z niczego poza rozliczeniami rodow " + from + ")"
                   + " - zmierzone ujscia w nicosc " + sinks + " [P] (rozliczenia rodow na minus " + _clanDown + " w " + _clanDownN + " rodach, regulator kas skasowal " + regOut
                   + ", z utargu wsi zniklo " + vanished
                   + ", GiveGoldAction w nicosc poza rozliczeniami rodow " + to + " minus " + _levyBack + " oddane przez LevyGold notablom i miastom)"
                   + " + reszta " + S(delta - sources + sinks) + " [R] (niezmierzone: BEE, BK poza rozliczeniami rodow, handel partii, liczniki cel rosnace przy handlu, kapital nowych karawan,"
                   + " smierc bohaterow, lupy w kryjowkach; ze znakiem minus: zysk warsztatow i karawan wyplacany notablom - gra zdejmuje go z kapitalu, a wyplate zglasza jak zloto z niczego"
                   + " (jest w zrodlach); rozliczenia rodow sa zmierzone w calosci)."
                   + " W tym zold naliczony " + wages + " [P] - siedzi w rozliczeniach rodow (rozbicie w nastepnej linii), w bilansie nie jest odejmowany drugi raz."
                   + (_clanHooked ? "" : " UWAGA: licznik rozliczen rodow nie jest wpiety - salda rodow sa w pozycjach GiveGoldAction, a zmiany kies partii i licznikow podatkow w reszcie.");
        }

        /// <summary>Dzienne rozliczenia rodow: zmiana zlota swiata (pomiar stanu), saldo dopisane glowom (zdarzenia gry), zold jako "w tym".</summary>
        private static string ClanLine(int day)
        {
            long wages = 0; for (int k = 0; k < Wages; k++) wages += _wage[k];
            var sb = new StringBuilder();
            sb.Append("Pieniadz swiata (rody): dzien ").Append(day);
            if (!_clanHooked)
                return sb.Append(" | licznik rozliczen rodow nie jest wpiety (brak ClanVariablesCampaignBehavior.DailyTickClan) - salda rodow sa w pozycjach GiveGoldAction bilansu;")
                         .Append(" zold naliczony ").Append(wages).Append(" [P] siedzi w tych saldach i w kiesach partii.").ToString();
            long change = _clanUp - _clanDown, net = _netUp - _netDown;
            sb.Append(" | dzienne rozliczenia rodow (model finansow gry i modow + dopisanie salda glowie rodu) zmienily zloto swiata o ").Append(S(change))
              .Append(" [P] w ").Append(_clanUpN + _clanDownN + _clanFlatN).Append(" rozliczeniach: ").Append(_clanUpN).Append(" na plus +").Append(_clanUp).Append(", ")
              .Append(_clanDownN).Append(" na minus ").Append(Neg(_clanDown)).Append(", ").Append(_clanFlatN)
              .Append(" bez zmiany (pomiar: suma wszystkich posiadaczy tuz przed i tuz po kazdym rozliczeniu)")
              .Append(" | z tego saldo dopisane glowom rodow +").Append(_netUp).Append(" (").Append(_netUpN).Append(" rodow) / ").Append(Neg(_netDown)).Append(" (").Append(_netDownN)
              .Append(" rodow) [P] (kwota ze zdarzenia gry - z pustej kiesy schodzi mniej), zmiany poza saldem ").Append(S(change - net))
              .Append(" [P] (kiesy partii i wodzow, liczniki cel i podatku wsi, kapital warsztatow, skarbce krolestw, kasy miast przy podatkach polityk, niedobor pustych kies,")
              .Append(" salda rodow bez zywej glowy)")
              .Append(" | zold naliczony ").Append(wages).Append(" [P] jest czescia tych rozliczen; bez niego zmienilyby zloto swiata o ").Append(S(change + wages))
              .Append(" (wyliczone: zmiana + zold naliczony; z pustej kiesy schodzi mniej, niz naliczono).");
            if (_wageToPurses + _wageToCoffers != 0)
                sb.Append(" Z zoldu SoldierPay oddal do obiegu ").Append(_wageToPurses + _wageToCoffers).Append(" (sakiewki ludzi ").Append(_wageToPurses).Append(", kasy osad ")
                  .Append(_wageToCoffers).Append(") - juz po pomiarze rozliczenia, w bilansie osobne zrodlo.");
            if (_clanClsFrom + _clanClsTo + _clanOthFrom + _clanOthTo != 0)
                sb.Append(" Inne GiveGoldAction w trakcie rozliczen (sa w zmianie zlota swiata): z niczego +").Append(_clanClsFrom + _clanOthFrom)
                  .Append(", w nicosc ").Append(Neg(_clanClsTo + _clanOthTo)).Append('.');
            if (_clanStale > 0) sb.Append(" Rozliczenia niedomkniete (wyjatek w kodzie gry albo moda): ").Append(_clanStale).Append('.');
            return sb.ToString();
        }

        private static string VillagerLine(int day)
        {
            long vanished = _vHanded - _vKept - _vTax - _vEstates;
            var sb = new StringBuilder();
            sb.Append("Przeplywy osad: dzien ").Append(day)
              .Append(" | utarg taborow wsi [P]: miasta zaplacily ").Append(_vPaid[CTown]).Append(" (").Append(_vVisits[CTown]).Append(" wizyt), zamki ")
              .Append(_vPaid[CCastle]).Append(" (").Append(_vVisits[CCastle]).Append(" wizyt); towar niesprzedany po wizycie: w miastach ")
              .Append(_vUnsold[CTown]).Append(" szt. (").Append(_vUnsoldVisits[CTown]).Append(" wizyt), w zamkach ").Append(_vUnsold[CCastle]).Append(" szt. (")
              .Append(_vUnsoldVisits[CCastle]).Append(" wizyt)")
              .Append(" | powrot do wsi: tabory oddaly ").Append(_vHanded).Append(" [P] w ").Append(_vReturns).Append(" powrotach = pan (licznik podatku wsi) ")
              .Append(_vTax).Append(" [P] (").Append(Pct(_vTax, _vHanded)).Append(") + wlasciciele majatkow BK ").Append(_vEstates).Append(" [P] (").Append(Pct(_vEstates, _vHanded))
              .Append(") + kiesa wsi ").Append(_vKept).Append(" [P] (").Append(Pct(_vKept, _vHanded)).Append(") + zniklo ").Append(vanished).Append(" [R] (")
              .Append(Pct(vanished, _vHanded)).Append(")")
              .Append(" | zold naliczony przy rozliczeniach rodow [P]: ");
            long wages = 0;
            for (int k = 0; k < Wages; k++)
            {
                wages += _wage[k];
                if (k > 0) sb.Append(", ");
                sb.Append(WName[k]).Append(' ').Append(_wage[k]).Append(" (").Append(_wageN[k]).Append(" partii, z niedoplata ").Append(_wageShort[k]).Append(')');
            }
            sb.Append(", razem ").Append(wages);
            sb.Append("; z tego przekazano do sakiewek ludzi ").Append(_wageToPurses).Append(", do kas osad ").Append(_wageToCoffers);
            if (wages == 0) sb.Append(" - licznik nie widzial wyplat (rozliczenie rodow idzie inna sciezka?)");
            sb.Append(". [P] = pomiar (zdarzenie, licznik albo roznica stanu), [R] = reszta z bilansu.");
            if (_winStale + _consMissed + _regStray + _stumbles > 0)
                sb.Append(" Potkniecia licznikow: okna taborow niedomkniete ").Append(_winStale).Append(", konsumpcja bez pary ").Append(_consMissed)
                  .Append(", regulator pytany poza konsumpcja ").Append(_regStray).Append(", wyjatki ").Append(_stumbles).Append('.');
            return sb.ToString();
        }

        private static string ClassLine(int day, int c, long now, long delta, List<int> golds)
        {
            var parts = new List<string>();
            long known = 0;
            if (c == CVill)
            {
                known += _vKept;
                parts.Add("z utargu taborow " + S(_vKept) + " [P]");
            }
            else
            {
                known -= _vPaid[c];
                parts.Add("zaplata taborom wsi " + S(-_vPaid[c]) + " [P]");
                known += _cons[c];
                parts.Add("\"zakupy\" mieszkancow " + S(_cons[c]) + " [P] (" + _consTicks[c] + " tickow osad)");
                long reg = _regIn[c] - _regOut[c];
                known += reg;
                parts.Add("regulator kasy " + S(reg) + " [P] (dosypal " + _regIn[c] + ", skasowal " + _regOut[c] + "; " + _regTicks[c] + " tickow)");
            }
            long gin = _fromNothing[c], gout = _toNothing[c];
            var det = new List<string>();
            for (int p = 0; p < Sides; p++)
            {
                gin += _goldIn[c, p]; gout += _goldOut[c, p];
                if (_goldIn[c, p] != 0 || _goldOut[c, p] != 0) det.Add(PName[p] + " +" + _goldIn[c, p] + "/-" + _goldOut[c, p]);
            }
            if (_fromNothing[c] != 0 || _toNothing[c] != 0) det.Add("z niczego +" + _fromNothing[c] + "/w nicosc -" + _toNothing[c]);
            known += gin - gout;
            parts.Add("przelewy gry (GiveGoldAction) " + S(gin - gout) + " [P]" + (det.Count > 0 ? " (" + string.Join(", ", det.ToArray()) + ")" : ""));
            long notes = 0; det = new List<string>();
            for (int k = 0; k < Notes; k++)
            {
                notes += _noteIn[c, k] - _noteOut[c, k];
                if (_noteIn[c, k] != 0 || _noteOut[c, k] != 0) det.Add(NName[k] + " +" + _noteIn[c, k] + "/-" + _noteOut[c, k]);
            }
            if (c != CVill || notes != 0)
            {
                known += notes;
                parts.Add("Armoury poza tickiem dobowym " + S(notes) + " [P]" + (det.Count > 0 ? " (" + string.Join(", ", det.ToArray()) + ")" : ""));
            }
            if (_shopWage[c] != 0 || _shopKeep[c] != 0)
            {
                known += _shopWage[c] + _shopKeep[c];
                parts.Add("warsztaty towarowe - place i utrzymanie z kapitalu warsztatow " + S(_shopWage[c] + _shopKeep[c]) + " [P] (place za cykle +" + _shopWage[c] + ", utrzymanie +" + _shopKeep[c] + ")");
            }
            if (_shopFair[c] != 0)
            {
                known += _shopFair[c];
                parts.Add("warsztaty towarowe - nadwyzka ceny wyrobow ponad koszt i marze (cena sprawiedliwa) " + S(_shopFair[c]) + " [P]");
            }
            if (_artIn[c] != 0 || _artBack[c] != 0)
            {
                known += _artIn[c] + _artBack[c];
                parts.Add("rzemieslnicy BK (147) - z ich kapitalu " + S(_artIn[c] + _artBack[c]) + " [P] (wsad dodatkowych cykli +" + _artIn[c] + ", zwrot zaplaty BK za sztuki bez wsadu +" + _artBack[c] + ")");
            }
            long marks = 0; det = new List<string>();
            for (int k = 0; k < Marks; k++)
            {
                marks += _mark[c, k];
                if (_mark[c, k] != 0) det.Add(MName[k] + " " + S(_mark[c, k]));
            }
            known += marks;
            parts.Add("Armoury tick dobowy " + S(marks) + " [P]" + (det.Count > 0 ? " (" + string.Join(", ", det.ToArray()) + ")" : ""));
            parts.Add((c == CVill ? "reszta - niezmierzone (m.in. podatek gry od zakupow we wsi) " : "reszta - inne niezmierzone (i regulator, gdy jego licznik stoi na 0 tickow) ")
                      + S(delta - known) + " [R]");
            string spread = "";
            if (golds != null && golds.Count > 0)
            {
                golds.Sort();
                int poor = 0; foreach (var g in golds) if (g < 1000) poor++;
                spread = " | rozklad [P]: osad " + golds.Count + ", min " + golds[0] + ", mediana " + golds[golds.Count / 2] + ", max " + golds[golds.Count - 1] + ", ponizej 1000 zlota " + poor;
            }
            return "Przeplywy osad (" + CName[c] + "): dzien " + day + " | stan " + now + ", zmiana " + S(delta) + " [P], w tym: "
                   + string.Join("; ", parts.ToArray()) + spread + ".";
        }

        private static string ModelsLine()
        {
            try
            {
                var m = Campaign.Current.Models;
                var eco = m.SettlementEconomyModel;
                var decl = eco != null ? DeclOf(eco.GetType()) : null;
                return "Przeplywy osad: modele czynne - kasa osad " + Name(eco) + " (regulator: " + (decl != null ? decl.Name : "?") + ".GetTownGoldChange)"
                       + ", produkcja wsi " + Name(m.VillageProductionCalculatorModel) + ", podatek " + Name(m.SettlementTaxModel) + ", zywnosc " + Name(m.SettlementFoodModel)
                       + ", dobrobyt " + Name(m.SettlementProsperityModel) + ", finanse rodow " + Name(m.ClanFinanceModel) + ", zold " + Name(m.PartyWageModel)
                       + ", ochotnicy " + Name(m.VolunteerModel) + ", garnizon " + Name(m.SettlementGarrisonModel) + ".";
            }
            catch (Exception e) { return "Przeplywy osad: modele czynne - blad odczytu (" + e.Message + ")."; }
        }

        private static string Name(object o) { return o != null ? o.GetType().FullName : "brak"; }

        // ------------------------------------------------------------ raz na dobe
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                var end = Snap();
                if (_inBlock && _blockSnap != null)
                    for (int c = 0; c < Classes; c++) _mark[c, MRest] += end[c] - _blockSnap[c];
                var hold = ReadHolders(end);
                if (!_modelsLogged) { _modelsLogged = true; Log.Info(ModelsLine()); }
                if (_first || _lastSnap == null || _lastHold == null)
                    Log.Info(StateLine(day, hold, null) + " Pierwsza doba ksiegi po wczytaniu: zmiany i przeplywy od nastepnej doby.");
                else
                {
                    Log.Info(StateLine(day, hold, _lastHold));
                    Log.Info(BalanceLine(day, hold, _lastHold));
                    Log.Info(ClanLine(day));
                    Log.Info(VillagerLine(day));
                    var golds = new List<int>[Classes];
                    for (int c = 0; c < Classes; c++) golds[c] = new List<int>();
                    foreach (var st in Settlement.All)
                    {
                        if (st == null || st.SettlementComponent == null) continue;
                        int c = ClassOf(st);
                        if (c >= 0) golds[c].Add(st.SettlementComponent.Gold);
                    }
                    for (int c = 0; c < Classes; c++) Log.Info(ClassLine(day, c, end[c], end[c] - _lastSnap[c], golds[c]));
                }
                _lastSnap = end; _lastHold = hold; _first = false;
            }
            catch (Exception e) { Log.Error("MoneyLedger.Daily", e); }
            finally { _inBlock = false; _blockSnap = null; ClearDay(); }
        }
    }
}
