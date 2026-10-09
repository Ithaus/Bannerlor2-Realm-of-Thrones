using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// SUROWCE MASOWE W KARAWANACH (wpis 103; Jeff 05.10: "karawany maja kupowac rude i przewozic ja tam, gdzie brakuje,
    /// i nie 1-3 sztuki - to przeciez czysty dobry zarobek").
    /// Test wpisu 101 (28 dob): rudy na swiecie przybywa (6974 -> 8845 ladunkow), a 60 z 97 miast nie ma jej wcale; w zamkach
    /// lezy 2641, w partiach "w drodze" 3155. BannerKings prowadzi wlasne karawany (BKCaravansBehavior) i wszystko liczy z
    /// indeksu ceny: miasto jest "pelne" po 4-8 ladunkach rudy (sprzedaz staje), a kupno wycenia stala 200 w starej monecie,
    /// wiec karawana wymiata kazda polke, ktora ma wiecej. Do tego gabka przy wyjezdzie (BKPartyBehavior.OnSettlementLeft):
    /// kazdy towar tanszy niz 33% wartosci do 80% udzwigu - stad tysiace ladunkow wozonych bez celu.
    /// Regula dla surowcow masowych (tabela nizej; liczone po id przedmiotu, nie po kategorii):
    ///  (a) SPRZEDAZ przy wjezdzie do miasta: tyle, ile miastu brakuje do zapasu docelowego (CaravanBulkStockDays dob
    ///      wlasnego zuzycia rzemieslnikow, nie mniej niz podloga z tabeli), nie wiecej niz karawana ma i niz kasa miasta
    ///      ponad TownRentFloorGold; po cenie miasta, przez SellItemsAction - kasa miasta placi kiesie karawany;
    ///  (b) KUPNO przy wyjezdzie z MIASTA: tylko z nadwyzki ponad CaravanBulkSurplusFactor x zapas docelowy, tylko taniej
    ///      niz srednia swiata (indeks) i tylko poki nastepna sztuka kosztuje mniej, niz placa za nia miasta z brakiem
    ///      ("cena zbytu" - obie ceny to prawdziwe ceny gry; warunki sprawdzane co krok zakupu), w kolejnosci najwiekszego zysku na
    ///      kilogram w tej chwili (BuyOrder - bez stalej listy), tylko do udzialu udzwigu (CaravanBulkCapacityShare, jeden
    ///      surowiec najwyzej polowe tego) i tylko poki wszystkie karawany razem woza mniej niz CaravanBulkTransitCover x
    ///      brak wszystkich miast ("tyle, ile zdola sprzedac"); kiesa karawany placi miastu, clo jak przy kazdym zakupie
    ///      (SellItemsAction). Zakup idzie krokami po BuyStep sztuk z cena liczona od nowa: cena rosnie z kazda sztuka
    ///      zdjeta z polki, a akcja gry przycina zaplate do kiesy - jeden duzy zakup po cenie pierwszej sztuki moglby
    ///      wyjsc ponad pol kiesy albo zostawic towar niedoplacony;
    ///  (c) gdy w drodze jest wiecej, niz swiat potrzebuje - karawana rozladowuje surowiec w kazdym miescie az do progu
    ///      nadwyzki (sprzedaz najwyzej do progu, kupno dopiero powyzej - bez przerzucania w kolko); do tego karawana nie
    ///      odkupuje w tym samym miescie surowca, ktory w czasie tej wizyty sprzedala (takze stara sprzedaza BK albo gry).
    /// KOLEJNOSC WEDLE ZYSKU (Jeff 06.10: "to ma byc to, co sie oplaca w danej chwili, a nie sztucznie"): miernikiem jest
    /// prawdziwa cena gry (Town.GetItemPrice - indeks polki, kara handlowa, zaokraglenie do denara), nie sam indeks kategorii.
    /// Indeks zawyza zysk drogiego towaru na pustej polce (plotno: indeks 9.6, a pierwsza sprzedana sztuka idzie po 4.9 x
    /// wartosci, piata po 2.2 x - towar sam zapelnia polke) i nie widzi zaokraglen, ktore przy drewnie po 4 d zjadaja caly zysk.
    /// Cena zbytu = srednia po miastach z brakiem i z kasa z ceny, jaka kazde z nich zaplaci za srodkowa sztuke swojego braku
    /// (liczona raz na dobe - wiesc z rana; w ciagu doby miasta sie dopelniaja, wiec pod wieczor jest to ocena z gory).
    /// Kupno: zysk na kilogram jukow (brakuje udzwigu). Sprzedaz: zysk na denara z kasy miasta (brakuje kasy, nie miejsca).
    /// ZAMKI: karawany BK jezdza tylko do miast (BKCaravansBehavior.FindNextDestinationForCaravan odrzuca kazde lenno, ktore
    /// nie jest miastem; awaryjny cel to dom albo najblizsze miasto), wiec w zamku nie kupujemy nic - gdyby karawana jednak
    /// z zamku wyjezdzala, cofamy tylko zakup gabki. Zapas zamkow rusza dopiero dowoz wsi (wpis 100), nie karawany.
    /// Stara sciezka BK dla tych kategorii jest zamknieta: wycena zakupu = 0 (postfiks CalculateBuyValue), a to, co gabka
    /// dokupi przy wyjezdzie, wraca na polke razem ze zlotem (postfiks OnSettlementLeft). Reszta towarow bez zmian.
    /// Zaczepy: sprzedaz - wlasny sluchacz CampaignEvents.SettlementEntered (bez zaleznosci od BK; zdarzenia gry wolaja
    /// sluchaczy od ostatnio zarejestrowanego, a Armoury laduje sie po BK - nasza sprzedaz idzie wiec przed sprzedaza BK i
    /// gry; odwrotna kolejnosc tez jest poprawna - kto pierwszy, ten dopelnia mniej); kupno - latki na dwoch prywatnych
    /// metodach BK, bo tylko tam da sie zatrzymac stary zakup. Parametry bierzemy po pozycji, ich typy sprawdzamy przy
    /// wpinaniu; niezgodnosc = cala regula wylaczona (sama sprzedaz bez blokady zakupu BK robilaby pompe: miasto kupuje
    /// drogo, karawana odkupuje tanio).
    /// Regula jest jedna dla wszystkich karawan, takze klanu gracza. Wyjatek: gdy karawana gracza ma czynny rozkaz BK
    /// "zaopatruj miasto w zywnosc" (CaravanOrdersBehavior.GetActiveBuyFilter), BK kupuje tylko zywnosc - my tez nie
    /// dokladamy jej surowcow (sprzedaz i cofanie gabki dzialaja dalej).
    /// POPRAWKA 115 (test 06.10 14:08, 20 dob: rudy na targach miast 247 -> 1740 ladunkow, a bez rudy 74 -> 69 miast; karawany
    /// kupily 371 ladunkow, sprzedaly 76, w jukach 290; len 3122 / 661, welna 1451 / 100, drewno 183 / 0). Dwie przyczyny w kodzie:
    ///  (1) KOLEJNOSC. BK w jednym ticku decyzji robi: BuyGoods -> ThinkNextDestination -> wyjazd (HourlyTickPartyImpl), a nasz
    ///      zakup szedl dopiero przy wyjezdzie (prefiks gabki) - cel byl juz wybrany bez nowego ladunku. BKROTPatch dopuszcza
    ///      te decyzje raz na 24 h (BKCaravansHourlyThrottlePatch), wiec karawana robi 2-3 kursy na 20 dob i pierwszy kurs po
    ///      zakupie szedl na slepo. Teraz zakup idzie zaraz po zakupach BK, przed wyborem celu (postfiks BuyGoods).
    ///  (2) MIEJSCE. BK konczy wlasne zakupy po przekroczeniu 80% udzwigu i gora jukow jedzie pusta; nasz zakup liczyl miejsce
    ///      do tej samej kreski, wiec po zakupach BK nie mial go w 7 wyjazdach na 10 ("brak miejsca w jukach" 72 z 92).
    ///      Teraz surowiec z zyskiem wchodzi takze w te pusta czesc (CaravanBulkFillLimit, domyslnie caly udzwig) - niczego
    ///      nie wypiera z zakupow BK w tym miescie. Karawany gracza zostaja przy 80% (jego zwykly handel bez zmian).
    /// Wynik celu BK (GetTradeScoreForTown) liczy nasze surowce z indeksu ceny i tego NIE ruszamy: po wpisie "cena surowcow od
    /// niedoboru" indeks rudy w miescie bez rudy to 10 (dotad 1.5), wiec BK sam widzi, gdzie jej brakuje. Wersja z wynikiem celu
    /// liczonym z naszego utargu (recenzja: symulacja z nowymi cenami) nie zmieniala liczby miast bez rudy wcale (0-0.3 miasta),
    /// a welne i len poprawiala albo psula zaleznie od obrazu ruchu karawan - wycieta.
    /// Kazda z dwoch rzeczy ma wlacznik; obie wylaczone = zachowanie ogniwa 103.
    /// </summary>
    internal static class CaravanBulk
    {
        private sealed class Good
        {
            public readonly int Ix;                           // stale miejsce w tabeli - migawka jukow przy wjezdzie liczy po nim
            public readonly string Id, Name;
            public readonly int Floor, Free;                  // podloga zapasu docelowego miasta; zapas, ktory osada dostaje sama
            public readonly float PerHand, Fixed, PerCycle;   // zuzycie na dobe: na roboczodzien rzemieslnikow, stale, na sztuke cyklu garbowania/tkania
            public ItemObject Item;
            public int Sold, Bought, Undone, First;           // liczniki doby (First: ile wyjazdow zaczelo zakupy od tego surowca)
            public float Gain;                                // oczekiwany zysk z zakupow doby: cena zbytu x sztuki - zaplacone
            public int Need, Carried, Short, Empty, Buyers;   // stan swiata z przeliczenia (Need i Carried poprawiane na biezaco); Buyers = miasta z brakiem i z kasa
            public float AvgIndex;
            public float SellPrice;                           // cena zbytu: srednio tyle placa miasta z brakiem i z kasa za srodkowa sztuke swojego braku (0 = nikt nie kupuje)
            public int LoggedShort = -1, LoggedEmpty = -1;
            // poprawka 115 - liczniki doby: przyczyny przy wyjezdzie z miasta z nadwyzka tego surowca i kierunek ladunku
            public int DepSurplus, DepBought, NoRoom, Gated, Dear, NoGain, Back, Ordered;
            public int In, InShort, Out, OutShort;            // wjazdy / wyjazdy z tym surowcem w jukach; w tym do miasta, ktoremu go brakuje
            public readonly HashSet<Town> Fed = new HashSet<Town>();   // miasta z brakiem, ktore dzis dostaly dostawe
            public int Carriers, InTown;                      // stan z przeliczenia: ile karawan go wiezie; ile sztuk stoi w karawanach w miastach

            public Good(int ix, string id, string name, int floor, int free, float perHand, float fixedUse, float perCycle)
            { Ix = ix; Id = id; Name = name; Floor = floor; Free = free; PerHand = perHand; Fixed = fixedUse; PerCycle = perCycle; }

            public float Kg { get { return Item != null && Item.Weight > 0.05f ? Item.Weight : 10f; } }
            public void NewDay()
            {
                Sold = 0; Bought = 0; Undone = 0; First = 0; Gain = 0f;
                DepSurplus = 0; DepBought = 0; NoRoom = 0; Gated = 0; Dear = 0; NoGain = 0; Back = 0; Ordered = 0;
                In = 0; InShort = 0; Out = 0; OutShort = 0; Fed.Clear();
            }
            public void Reset() { NewDay(); Item = null; Need = 0; Carried = 0; Short = 0; Empty = 0; Buyers = 0; AvgIndex = 0f; SellPrice = 0f; LoggedShort = -1; LoggedEmpty = -1; Carriers = 0; InTown = 0; }
        }

        // Podloga = najwieksza sztuka, jaka warsztat zaczyna naraz (plyta bierze do 7.8 ladunku rudy; warsztat kupuje caly wsad
        // przy starcie sztuki, WorkshopLaw.cs:222-228). Zuzycie na roboczodzien z pomiaru logu 05.10 (warsztaty 26.6 ladunku rudy
        // i 172 drewna dziennie przy 37 miastach z ruda i medianie 28 rak) i z receptur; stale = mieszczanie (budzet BK x TownUse*)
        // i narzedzia. Garbowanie i tkanie rzemieslnikow: predkosc linii 0.2 i 0.5 cyklu na dobe (BannerKings workshops.xml:47,62)
        // x ArtisanTanWeavePerCycle (paczka 148: przy czynnym rzemiosle miasta zamiast tego prawdziwy przerob - TownCrafts.UseOf). Drewno BEZ budow (dziura bez dna) i z 30 ladunkami, ktore RealisticBannerlord dosypuje
        // miastu co dobe - tego karawana nie wozi i nie wykupuje (paczka 125: gdy ta dosypka jest zablokowana, Free = 0 - FreeOf).
        // Linie towarowe warsztatow notabli (tkalnie, garbarnie, tartaki,
        // narzedzia kuzni) nie sa w tabeli - liczy je ShopUse z receptur gry, osobno dla kazdego miasta.
        private static readonly Good[] _all =
        {
            new Good(0, "iron",     "ruda",          8,  0, 0.025f, 0.05f, 0f),
            new Good(1, "hardwood", "drewno",       40, 30, 0.157f, 0.45f, 0f),
            new Good(2, "hides",    "skory surowe",  5,  0, 0f,     0.04f, 0.2f),
            new Good(3, "leather",  "skora",         3,  0, 0.011f, 0.28f, 0f),
            new Good(4, "flax",     "len",           5,  0, 0f,     0.11f, 0.5f),
            new Good(5, "linen",    "plotno",        3,  0, 0.024f, 0.27f, 0f),
            new Good(6, "wool",     "welna",         3,  0, 0f,     0.05f, 0f),
        };

        private const float OneGoodPart = 0.5f;   // jeden surowiec bierze najwyzej polowe udzwigu przeznaczonego na surowce (drewno nie wypiera rudy)
        private const float FillLimit = 0.8f;     // prog zapelnienia jukow, przy ktorym BK sam przestaje kupowac (BKCaravansBehavior.BuyCategory, gabka)
        private const int BuyStep = 10;           // zakup krokami po tyle sztuk: cene i indeks liczymy od nowa przed kazdym krokiem

        private static Good[] _goods;             // znalezione w tej kampanii, w kolejnosci bazowej (najcenniejszy kilogram pierwszy); kolejnosc handlu w danym miescie licza BuyOrder i SellOrder
        private static readonly HashSet<ItemCategory> _cats = new HashSet<ItemCategory>();
        private static readonly Dictionary<MobileParty, int[]> _cameWith = new Dictionary<MobileParty, int[]>();   // juki karawany przy wjezdzie do miasta (po Good.Ix); zdejmowane przy wyjezdzie
        private static MethodInfo _ordersInstance, _ordersFilter;   // BK CaravanOrdersBehavior.Instance / GetActiveBuyFilter - rozkazy gracza dla wlasnych karawan
        private static bool _wired;
        private static bool _errSell, _errLeft, _errBuy, _errUndo, _errValue, _errOrder, _errBack, _errShop, _errPrice;
        private static int _worldDay = -1, _towns, _caravans;
        private static int _entries, _sales, _departures, _buys, _gated, _dear, _noGain, _full, _poor, _back, _ordered, _bkZeroed;
        private static long _townsPaid, _caravansPaid, _refunded;

        // ---- poprawka 115: zakup przed wyborem celu, juki ponad kreske BK

        private static readonly HashSet<MobileParty> _boughtHere = new HashSet<MobileParty>();   // karawany, ktore w tej wizycie juz kupowaly (zdejmowane przy wyjezdzie)
        private static readonly HashSet<Town> _entered = new HashSet<Town>();                    // miasta, do ktorych dzis wjechala jakas karawana
        private static bool _wiredBuy;
        private static bool _errEarly, _errRoute;
        private static int _early, _late, _inTown;
        private static float _kgBought, _kgOver;

        private static void NewDay()
        {
            _entries = 0; _sales = 0; _departures = 0; _buys = 0; _gated = 0; _dear = 0; _noGain = 0; _full = 0; _poor = 0; _back = 0; _ordered = 0; _bkZeroed = 0;
            _townsPaid = 0; _caravansPaid = 0; _refunded = 0;
            _early = 0; _late = 0; _kgBought = 0f; _kgOver = 0f;
            _entered.Clear();
            foreach (var g in _all) g.NewDay();
        }

        /// <summary>Nowa gra albo wczytanie: przedmioty i liczniki poprzedniej kampanii nie przeciekaja do nastepnej.</summary>
        internal static void Reset()
        {
            _goods = null; _cats.Clear(); _cameWith.Clear(); _worldDay = -1; _towns = 0; _caravans = 0;
            _boughtHere.Clear(); _inTown = 0;
            _errEarly = false; _errRoute = false;   // audyt 120: pierwszy blad nowej kampanii tej samej sesji znow idzie do logu
            NewDay();
            foreach (var g in _all) g.Reset();
        }

        private static bool Ready() { return _goods != null || Resolve(); }

        /// <summary>Szuka przedmiotow z tabeli; zastepuje liste tylko wtedy, gdy cos znalazl.</summary>
        private static bool Resolve()
        {
            var om = MBObjectManager.Instance;
            if (om == null) return false;
            var list = new List<Good>();
            foreach (var g in _all)
            {
                g.Item = om.GetObject<ItemObject>(g.Id);
                if (g.Item != null) list.Add(g);
            }
            if (list.Count == 0) return false;      // przedmioty jeszcze nie wczytane - sprobujemy przy nastepnym wywolaniu
            _cats.Clear();
            foreach (var g in list) if (g.Item.ItemCategory != null) _cats.Add(g.Item.ItemCategory);
            _goods = list.ToArray();
            return true;
        }

        /// <summary>Karawana rodu gracza: BKROTPatch nie dlawi jej decyzji, a jej juki ponad kreske 80% zostaja dla jego zwyklego handlu.</summary>
        private static bool Players(MobileParty mp)
        {
            try { return mp.ActualClan != null && mp.ActualClan == Clan.PlayerClan; }
            catch { return false; }
        }

        private static bool Trades(MobileParty mp)
        {
            return Campaign.Current != null && Campaign.Current.GameStarted      // przed startem SellItemsAction nie przenosi zlota karawan
                   && mp.IsActive && mp.IsPartyTradeActive && mp.MapEvent == null && mp.ItemRoster != null && mp.Party != null
                   && (mp.Ai == null || !mp.Ai.DoNotMakeNewDecisions || MaterialOrders.HasContract(mp));   // karawany posilkow DTE nie handluja; 174.2: karawana z kontraktem surowca handluje
        }

        /// <summary>Zapas docelowy miasta w sztukach rynku. Zamek nie ma rzemieslnikow ani warsztatow - nie trzyma nic dla siebie.</summary>
        private static int Target(Town town, Good g, Settings s)
        {
            if (!town.IsTown) return 0;
            float use = Use(town, g, s);
            return Math.Max(g.Floor, (int)Math.Ceiling(Math.Max(0, s.CaravanBulkStockDays) * use));
        }

        /// <summary>Zuzycie dobowe miasta w sztukach rynku: rzemieslnicy (na roboczodzien), czesc domowa i narzedzia (stale),
        /// garbowanie i tkanie rzemieslnikow, linie towarowe warsztatow notabli. Jedno zrodlo dla zapasu docelowego i dla ceny.</summary>
        private static float Use(Town town, Good g, Settings s)
        {
            // paczka 148: przy czynnym rzemiosle miasta (TownCrafts) garbowania i tkania 1:1 nie ma - zamiast jego stalej (PerCycle x sztuk na
            // cykl) prawdziwy przerob rzemiosla w tym miescie (srednia z ok. 14 dob, sztuki wsadu na dobe; takze welna)
            float crafts = TownCrafts.Active ? TownCrafts.UseOf(town, g.Item) : g.PerCycle * Math.Max(0, s.ArtisanTanWeavePerCycle);
            float hands = s.CaravanBulkLegacyHands ? WorkshopLaw.LegacyTownHands(town) : WorkshopLaw.TownHands(town);   // 174.3: cel zapasu i prog nadwyzki na dawnych rekach (zrodla kontraktow 174.2 nie znikaja)
            return hands * g.PerHand + g.Fixed + crafts + ShopUse(town, g);
        }

        /// <summary>Dla ceny surowcow (RawPrice): to samo zuzycie dobowe, z ktorego karawany licza zapas docelowy miasta - przedmiot
        /// surowca, zuzycie razem i jego stala czesc domowa (sztuki rynku na dobe) oraz prog nadwyzki. Falsz = kategoria spoza tabeli,
        /// zamek albo przedmioty jeszcze nie wczytane. Sam odczyt; nie zalezy od tego, czy regula karawan jest wlaczona i wpieta.</summary>
        internal static bool Usage(Town town, ItemCategory cat, out ItemObject item, out float use, out float home, out int keep)
        {
            item = null; use = 0f; home = 0f; keep = 0;
            var s = Settings.Current;
            if (s == null || town == null || cat == null || !town.IsTown || !Ready() || !_cats.Contains(cat)) return false;
            foreach (var g in _goods)
            {
                if (g.Item.ItemCategory != cat) continue;
                item = g.Item; use = Use(town, g, s); home = g.Fixed; keep = Keep(town, g, s);
                return true;
            }
            return false;
        }

        /// <summary>174.2: zuzycie dobowe surowca w miescie (szacunek z rak - ten sam, z ktorego karawany licza zapas docelowy); 0 = spoza tabeli albo zamek.</summary>
        internal static float UseFor(Town town, ItemObject item)
        {
            var s = Settings.Current;
            if (s == null || town == null || item == null || !town.IsTown || !Ready()) return 0f;
            foreach (var g in _goods) if (g.Item == item) return Use(town, g, s);
            return 0f;
        }

        /// <summary>174b.2 (krytyka 16): zuzycie dobowe surowca w miescie z OBECNYCH rak (WorkshopLaw.TownHands) - ilosc zamowienia miasta-celu;
        /// prog nadwyzki zrodla i cel zapasu karawan liczy dalej Use (dawne rece przy CaravanBulkLegacyHands). 0 = spoza tabeli albo zamek.</summary>
        internal static float UseNow(Town town, ItemObject item)
        {
            var s = Settings.Current;
            if (s == null || town == null || item == null || !town.IsTown || !Ready()) return 0f;
            foreach (var g in _goods)
            {
                if (g.Item != item) continue;
                float crafts = TownCrafts.Active ? TownCrafts.UseOf(town, g.Item) : g.PerCycle * Math.Max(0, s.ArtisanTanWeavePerCycle);
                return WorkshopLaw.TownHands(town) * g.PerHand + g.Fixed + crafts + ShopUse(town, g);
            }
            return 0f;
        }

        /// <summary>174.3: suma zapasow docelowych i progow nadwyzki miast dla surowca (linia startowa "Rece (174)").</summary>
        internal static void SumsFor(string id, out int target, out int keep)
        {
            target = 0; keep = 0;
            var s = Settings.Current;
            if (s == null || !Ready()) return;
            Good g = null; foreach (var x in _goods) if (x.Id == id) { g = x; break; }
            if (g == null) return;
            foreach (var t in Town.AllTowns) { if (t == null || !t.IsTown) continue; int tg = Target(t, g, s); target += tg; keep += KeepOf(g, s, tg); }
        }

        /// <summary>174.2: prog nadwyzki miasta (ponizej niego nie sprzedaje karawanom) - prog zrodla kontraktow surowca; 0 = spoza tabeli albo zamek.</summary>
        internal static int KeepFor(Town town, ItemObject item)
        {
            var s = Settings.Current;
            if (s == null || town == null || item == null || !town.IsTown || !Ready()) return 0;
            foreach (var g in _goods) if (g.Item == item) return Keep(town, g, s);
            return 0;
        }

        /// <summary>Kategorie surowcow masowych z tabeli (dla ceny surowcow i jej logu), w kolejnosci tabeli. Pusta = przedmioty nie wczytane.</summary>
        internal static List<ItemObject> Items()
        {
            var list = new List<ItemObject>();
            if (!Ready()) return list;
            foreach (var g in _all) if (g.Item != null) list.Add(g.Item);
            return list;
        }

        /// <summary>Nazwa surowca do logu (ta sama co w liniach "Karawany").</summary>
        internal static string NameOf(ItemObject item)
        {
            foreach (var g in _all) if (g.Item == item) return g.Name;
            return item != null ? item.StringId : "?";
        }

        /// <summary>Zuzycie dobowe linii TOWAROWYCH warsztatow notabli i gracza w miescie: predkosc linii x wsad (spworkshops.xml:
        /// tkalnia welny 3 welny, tkalnia lnu 2 lnu, garbarnia 2 skory surowe, tartak 2 drewna, kuznia - narzedzia 1.5 rudy na dobe).
        /// Te linie zostaly przy grze (WorkshopLaw ich nie rusza) i biora wsad z polki miasta; bez nich miasto z tkalnia chcialoby
        /// 3 ladunkow welny, czyli zapas na jedna dobe, a stary handel BK ta kategoria jest zamkniety. Linie uzbrojenia sa juz w
        /// stawce na roboczodzien, ukryci rzemieslnicy - w stalych z tabeli.</summary>
        private static float ShopUse(Town town, Good g)
        {
            try
            {
                var cat = g.Item != null ? g.Item.ItemCategory : null;
                var shops = town.Workshops;
                if (cat == null || shops == null) return 0f;
                float use = 0f;
                foreach (var w in shops)
                {
                    var type = w != null ? w.WorkshopType : null;
                    if (type == null || type.IsHidden || type.Productions == null) continue;
                    foreach (var p in type.Productions)
                    {
                        if (p.Inputs == null || p.Outputs == null) continue;      // Production to struct
                        bool goods = false;
                        foreach (var o in p.Outputs) if (o.Item1 != null && o.Item1.IsTradeGood) { goods = true; break; }
                        if (!goods) continue;
                        foreach (var i in p.Inputs) if (i.Item1 == cat) use += p.ConversionSpeed * i.Item2;
                    }
                }
                return use;
            }
            catch (Exception e)
            {
                if (!_errShop) { _errShop = true; Log.Error("CaravanBulk.ShopUse", e); }
                return 0f;
            }
        }

        /// <summary>Prog nadwyzki: ponizej niego miasto nie sprzedaje karawanom nic (i nie wiecej niz tyle przyjmie przy rozladunku).</summary>
        private static int Keep(Town town, Good g, Settings s)
        {
            return KeepOf(g, s, Target(town, g, s));
        }

        private static int KeepOf(Good g, Settings s, int target)
        {
            return Math.Max((int)Math.Ceiling(Math.Max(1f, s.CaravanBulkSurplusFactor) * target), FreeOf(g));
        }

        /// <summary>Zapas, ktory miasto dostaje samo (Good.Free - drewno: 30 ladunkow dosypki RealisticBannerlord co dobe). Paczka 125
        /// blokuje te dosypke (FreeSupplies.Blocking): wtedy miasto ma tylko to, co lezy na polce, i brak liczy sie od zera.</summary>
        private static int FreeOf(Good g)
        {
            return g.Free > 0 && FreeSupplies.Blocking ? 0 : g.Free;
        }

        /// <summary>W drodze jest wiecej tego surowca, niz miasta zdolaja kupic - kupno stoi, rozladunek do progu nadwyzki.</summary>
        private static bool Glut(Good g, Settings s)
        {
            return g.Carried > Math.Max(0f, s.CaravanBulkTransitCover) * g.Need;
        }

        private static void EnsureWorld(Settings s)
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (day != _worldDay) Recount(s, day);
        }

        /// <summary>Raz na dobe: brak miast, srednia indeksu ceny, cena zbytu i ladunek karawan dla kazdego surowca (97 miast i ok. 320 karawan).</summary>
        private static void Recount(Settings s, int day)
        {
            _worldDay = day;
            if (_goods.Length < _all.Length) Resolve();      // przedmiot z XML mogl dojsc po pierwszym wywolaniu
            Array.Sort(_goods, (a, b) => (b.Item.Value / b.Kg).CompareTo(a.Item.Value / a.Kg));
            foreach (var g in _goods) { g.Need = 0; g.Carried = 0; g.Short = 0; g.Empty = 0; g.Buyers = 0; g.AvgIndex = 0f; g.SellPrice = 0f; g.Carriers = 0; g.InTown = 0; }
            int reserve = (int)Math.Max(0f, s.TownRentFloorGold);
            int towns = 0, caravans = 0, inTown = 0;
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t.Owner == null || t.Owner.ItemRoster == null) continue;
                towns++;
                var shelf = t.Owner.ItemRoster;
                bool pays = t.Gold > reserve;
                foreach (var g in _goods)
                {
                    int n = shelf.GetItemNumber(g.Item);
                    int lack = Target(t, g, s) - Math.Max(n, FreeOf(g));
                    if (n <= 0) g.Empty++;
                    if (lack > 0)
                    {
                        g.Short++;
                        if (pays)
                        {
                            g.Need += lack;
                            int p = Fetch(t, g, lack);           // miasto z brakiem i z kasa: tu karawana ten surowiec sprzeda
                            if (p > 0) { g.SellPrice += p; g.Buyers++; }
                        }
                    }
                    if (g.Item.ItemCategory != null) g.AvgIndex += t.GetItemCategoryPriceIndex(g.Item.ItemCategory);
                }
            }
            if (towns > 0) foreach (var g in _goods) g.AvgIndex /= towns;
            foreach (var g in _goods) if (g.Buyers > 0) g.SellPrice /= g.Buyers;      // nikt nie kupuje = 0, a wtedy nikt tez nie kupuje na wywoz
            foreach (var c in MobileParty.AllCaravanParties)
            {
                if (c == null || !c.IsActive || c.ItemRoster == null || !c.IsPartyTradeActive || (c.Ai != null && c.Ai.DoNotMakeNewDecisions && !MaterialOrders.HasContract(c))) continue;   // tylko karawany, ktore handluja (174.2: z kontraktem tez)
                caravans++;
                bool stands = c.CurrentSettlement != null;
                if (stands) inTown++;
                foreach (var g in _goods)
                {
                    int a = c.ItemRoster.GetItemNumber(g.Item);
                    if (a <= 0) continue;
                    g.Carried += a; g.Carriers++;
                    if (stands) g.InTown += a;
                }
            }
            _towns = towns; _caravans = caravans; _inTown = inTown;
        }

        /// <summary>Akcja gry przerwana wyjatkiem po przeniesieniu sztuk, a przed zaplata: sztuki wracaja tam, skad wyszly (nic bez zaplaty).</summary>
        private static void GiveBack(ItemRoster holds, ItemRoster owner, EquipmentElement what, int n)
        {
            try { holds.AddToCounts(what, -n); owner.AddToCounts(what, n); }
            catch (Exception e) { if (!_errBack) { _errBack = true; Log.Error("CaravanBulk.GiveBack", e); } }
        }

        // ------------------------------------------------------------ (a) sprzedaz przy wjezdzie do miasta
        internal static void OnEntered(MobileParty mp, Settlement st, Hero hero)
        {
            if (mp == null || st == null || !mp.IsCaravan || !st.IsTown) return;   // tanie wyjscie: zdarzenie pada dla kazdej partii i kazdego bohatera
            try
            {
                if (_boughtHere.Count > 0) _boughtHere.Remove(mp);      // nowa wizyta: znacznik zakupu z poprzedniej nie moze zostac (np. przerzut statkiem BK bez zdarzenia wyjazdu)
                var s = Settings.Current;
                if (!_wired || s == null || !s.CaravanBulkEnabled || st.Town == null || !Trades(mp) || !Ready()) return;
                EnsureWorld(s);
                // migawka jukow przed jakakolwiek sprzedaza tej wizyty (nasza, BK i gry) - przy wyjezdzie nie odkupujemy tego, co tu zeszlo
                var came = new int[_all.Length];
                foreach (var g in _goods) came[g.Ix] = mp.ItemRoster.GetItemNumber(g.Item);
                _cameWith[mp] = came;
                Sell(mp, st.Town, s);
            }
            catch (Exception e)
            {
                if (!_errSell) { _errSell = true; Log.Error("CaravanBulk.OnEntered", e); }   // raz na sesje; nastepna wizyta probuje znowu
            }
        }

        // ------------------------------------------------------------ kolejnosc wedle zysku w tej chwili (prawdziwe ceny gry)
        /// <summary>
        /// Cena zbytu w jednym miescie z brakiem: tyle zaplaci ono za SRODKOWA sztuke swojego braku. Ten sam wzor, ktorym gra
        /// wycenia sprzedaz (Town.GetItemPrice -> TownMarketData.GetPrice -> model cen), tylko z polka powiekszona o sztuki
        /// dowiezione wczesniej: cena spada z kazda sztuka, wiec cena pierwszej zawyzalaby utarg z calej dostawy (plotno na
        /// pustej polce miasta-mediany: 490, 360, 294, 253, 224 d...; dziesiec sztuk daje srednio 249 d). Bez partii - kara
        /// handlowa 1.2% zamiast najwyzej 3% karawany, czyli cena zawyzona najwyzej o 2%. 0 = brak wyceny.
        /// </summary>
        private static int Fetch(Town town, Good g, int lack)
        {
            try
            {
                var cat = g.Item.ItemCategory;
                var market = town.MarketData;
                var model = Campaign.Current != null ? Campaign.Current.Models.TradeItemPriceFactorModel : null;
                if (cat == null || market == null || model == null) return 0;
                var d = market.GetCategoryData(cat);
                int before = Math.Max(0, lack - 1) / 2;      // tyle sztuk zejdzie przed srodkowa
                return Math.Max(1, model.GetPrice(new EquipmentElement(g.Item), null, null, true, d.InStoreValue + before * HistoricalPrices.ShelfWorth(g.Item), d.Supply, d.Demand));   // paczka 121: jednostka polki = waga sztuki
            }
            catch (Exception e)
            {
                if (!_errPrice) { _errPrice = true; Log.Error("CaravanBulk.Fetch", e); }   // raz na sesje; surowiec bez wyceny nie jest kupowany
                return 0;
            }
        }

        /// <summary>Prawdziwa cena jednej sztuki w tym miescie dla tej karawany (selling = karawana sprzedaje miastu). 0 = brak wyceny.</summary>
        private static int PriceOf(Town town, Good g, MobileParty mp, bool selling)
        {
            try { return Math.Max(1, town.GetItemPrice(new EquipmentElement(g.Item), mp, selling)); }
            catch (Exception e)
            {
                if (!_errPrice) { _errPrice = true; Log.Error("CaravanBulk.PriceOf", e); }
                return 0;
            }
        }

        /// <summary>Miejsca od najwiekszego zysku do najmniejszego; przy remisie zostaje kolejnosc wejsciowa (bazowa). Sortowanie
        /// stabilne przez wstawianie - pozycji jest najwyzej siedem.</summary>
        internal static int[] Rank(float[] gain)
        {
            var at = new int[gain.Length];
            for (int i = 0; i < at.Length; i++)
            {
                int j = i;
                while (j > 0 && gain[at[j - 1]] < gain[i]) { at[j] = at[j - 1]; j--; }
                at[j] = i;
            }
            return at;
        }

        private static Good[] Ordered(float[] gain)
        {
            var at = Rank(gain);
            var order = new Good[at.Length];
            for (int i = 0; i < at.Length; i++) order[i] = _goods[at[i]];
            return order;
        }

        private static float[] Unpriced()
        {
            var gain = new float[_goods.Length];
            for (int i = 0; i < gain.Length; i++) gain[i] = float.NegativeInfinity;   // bez wyceny = na koniec, w kolejnosci bazowej
            return gain;
        }

        /// <summary>
        /// Kolejnosc KUPNA wedle zysku w tej chwili (Jeff 06.10: "to ma byc to, co sie oplaca w danej chwili, a nie sztucznie")
        /// - bez stalej listy. Przy kupnie brakuje udzwigu, wiec liczy sie zysk na kilogram jukow:
        /// (cena zbytu - cena kupna tutaj dla tej karawany) / waga sztuki. Wyceniamy tylko to, co lezy ponad progiem nadwyzki
        /// i ma zbyt; gdy taki surowiec jest najwyzej jeden, kolejnosc nie ma znaczenia i zostaje bazowa.
        /// </summary>
        private static Good[] BuyOrder(MobileParty mp, Town town, ItemRoster shelf, Settings s)
        {
            float[] gain = null;
            int priced = 0;
            for (int i = 0; i < _goods.Length; i++)
            {
                var g = _goods[i];
                if (g.SellPrice <= 0f) continue;
                int n = shelf.GetItemNumber(g.Item);
                if (n <= 0 || n <= Keep(town, g, s)) continue;
                int price = PriceOf(town, g, mp, false);
                if (price <= 0) continue;
                if (gain == null) gain = Unpriced();
                gain[i] = (g.SellPrice - price) / g.Kg;
                priced++;
            }
            return priced > 1 ? Ordered(gain) : _goods;
        }

        /// <summary>
        /// Kolejnosc SPRZEDAZY wedle zysku w tej chwili. Kolejnosc wazy tylko wtedy, gdy kasy miasta nie starcza na wszystko,
        /// czyli brakuje kasy, nie udzwigu - liczy sie wiec zysk na denara z tej kasy: (cena tutaj - cena zbytu gdzie indziej)
        /// / cena tutaj. Najpierw schodzi to, za co to miasto placi najwiecej ponad inne miasta; to, co gdzie indziej pojdzie
        /// drozej, zostaje w jukach. Wyceniamy tylko to, co karawana wiezie.
        /// </summary>
        private static Good[] SellOrder(MobileParty mp, Town town, ItemRoster pack)
        {
            float[] gain = null;
            int priced = 0;
            for (int i = 0; i < _goods.Length; i++)
            {
                var g = _goods[i];
                if (pack.GetItemNumber(g.Item) <= 0) continue;
                int price = PriceOf(town, g, mp, true);
                if (price <= 0) continue;
                if (gain == null) gain = Unpriced();
                gain[i] = (price - g.SellPrice) / price;
                priced++;
            }
            return priced > 1 ? Ordered(gain) : _goods;
        }

        private static void Sell(MobileParty mp, Town town, Settings s)
        {
            var pack = mp.ItemRoster;
            var shelf = town.Owner.ItemRoster;
            int reserve = (int)Math.Max(0f, s.TownRentFloorGold);
            List<(EquipmentElement, int)> done = null;
            _entries++;
            _entered.Add(town);
            foreach (var g in _goods)      // dokad trafia ladunek: wjazd z surowcem w jukach, w tym do miasta, ktoremu go brakuje
            {
                if (pack.GetItemNumber(g.Item) <= 0) continue;
                g.In++;
                if (Target(town, g, s) - Math.Max(shelf.GetItemNumber(g.Item), FreeOf(g)) > 0) g.InShort++;
            }
            foreach (var g in town.Gold > reserve ? SellOrder(mp, town, pack) : _goods)   // miasto bez kasy ponad rezerwe i tak nie kupi nic
            {
                int at = pack.FindIndexOfItem(g.Item);
                if (at < 0) continue;
                var el = pack.GetElementCopyAtIndex(at);
                if (el.Amount <= 0) continue;
                int held = Math.Max(shelf.GetItemNumber(g.Item), FreeOf(g));
                int lack = Target(town, g, s) - held;
                int want = Glut(g, s) ? Keep(town, g, s) - held : lack;
                if (want <= 0) continue;
                int spare = town.Gold - reserve;
                if (spare <= 0) { _poor++; break; }      // miasto bez zlota ponad rezerwe na renty nie kupuje niczego
                int price = Math.Max(1, town.GetItemPrice(el.EquipmentElement, mp, true));
                int n = Math.Min(Math.Min(want, el.Amount), spare / price);   // cena spada z kazda sztuka - pierwsza jest gorna granica
                if (n <= 0) { _poor++; continue; }
                int purse = mp.PartyTradeGold, had = el.Amount;
                bool broke = false;
                try { SellItemsAction.Apply(mp.Party, town.Owner, el, n, town.Settlement); }
                catch (Exception e) { broke = true; if (!_errSell) { _errSell = true; Log.Error("CaravanBulk.Sell", e); } }
                int moved = had - pack.GetItemNumber(g.Item);                 // liczymy to, co faktycznie zeszlo z jukow
                int got = Math.Max(0, mp.PartyTradeGold - purse);
                if (broke && moved > 0 && got == 0) { GiveBack(shelf, pack, el.EquipmentElement, moved); moved = 0; }   // towar poszedl, zloto nie - cofamy
                if (moved > 0)
                {
                    g.Sold += moved;
                    _townsPaid += got;
                    g.Carried = Math.Max(0, g.Carried - moved);
                    if (lack > 0)
                    {
                        g.Need = Math.Max(0, g.Need - Math.Min(moved, lack));
                        g.Fed.Add(town);
                    }
                    if (done == null) done = new List<(EquipmentElement, int)>();
                    done.Add((el.EquipmentElement, moved));
                }
                if (broke) break;                                             // po bledzie akcji gry nie handlujemy dalej w tej wizycie
            }
            if (done == null) return;
            _sales++;
            try { CampaignEventDispatcher.Instance.OnCaravanTransactionCompleted(mp, town, done); } catch { }   // jak BK po sprzedazy: dymek nad miastem na mapie
        }

        // ------------------------------------------------------------ (b) kupno przy wyjezdzie + cofniecie gabki BK
        /// <summary>Prefiks BKPartyBehavior.OnSettlementLeft (gabka): najpierw nasz zakup z nadwyzki, potem stan jukow dla postfiksu.</summary>
        public static void LeftPrefix(MobileParty __0, Settlement __1, out int[] __state)
        {
            __state = null;
            if (__0 == null || __1 == null || !__0.IsCaravan || __1.Town == null) return;   // ta sama bramka co w gabce BK
            try
            {
                int[] came = null;
                if (_cameWith.Count > 0 && _cameWith.TryGetValue(__0, out came)) _cameWith.Remove(__0);   // wizyta skonczona - takze gdy regula jest wylaczona
                bool early = _boughtHere.Count > 0 && _boughtHere.Remove(__0);                            // w tej wizycie kupila juz przed wyborem celu
                var s = Settings.Current;
                if (!_wired || s == null || !s.CaravanBulkEnabled || !Trades(__0) || !Ready()) return;
                EnsureWorld(s);
                if (__1.IsTown)      // w zamku nie kupujemy (karawany tam nie jezdza; utarg zamku szedlby w calosci jako "clo") - tylko cofamy gabke
                {
                    if (!early)      // zakup przy wyjezdzie: wlacznik zakupu przed celem wylaczony, brak latki BuyGoods albo wyjazd inna droga niz decyzja BK
                    {
                        try { Buy(__0, __1.Town, s, came, false); }
                        catch (Exception e) { if (!_errBuy) { _errBuy = true; Log.Error("CaravanBulk.Buy", e); } }
                    }
                    Routed(__0, s);
                }
                var pack = __0.ItemRoster;
                var state = new int[_goods.Length];
                for (int i = 0; i < state.Length; i++) state[i] = pack.GetItemNumber(_goods[i].Item);
                __state = state;
            }
            catch (Exception e)
            {
                __state = null;
                if (!_errLeft) { _errLeft = true; Log.Error("CaravanBulk.LeftPrefix", e); }
            }
        }

        /// <summary>Postfiks gabki: surowiec masowy, ktory BK dokupil ponad nasza regule, wraca na polke, a zloto do kiesy (ten sam wzor ceny).</summary>
        public static void LeftPostfix(MobileParty __0, Settlement __1, int[] __state)
        {
            if (__state == null || _goods == null || __state.Length != _goods.Length) return;
            try
            {
                var town = __1.Town;
                var pack = __0.ItemRoster;
                var shelf = town.Owner.ItemRoster;
                for (int i = 0; i < __state.Length; i++)
                {
                    var g = _goods[i];
                    int extra = pack.GetItemNumber(g.Item) - __state[i];
                    if (extra <= 0) continue;
                    pack.AddToCounts(g.Item, -extra);
                    shelf.AddToCounts(g.Item, extra);
                    // polka wrocila do stanu sprzed gabki, wiec cena jest ta sama, ktora BK pomnozyl przez liczbe sztuk
                    int refund = Math.Min(town.Gold, (int)(extra * (float)town.GetItemPrice(new EquipmentElement(g.Item), __0, false)));
                    if (refund > 0) { town.ChangeGold(-refund); __0.PartyTradeGold += refund; _refunded += refund; }
                    if (refund > 0) MoneyLedger.Note169(MoneyLedger.N169Other, __1, -refund);   // paczka 169: linia kas (tylko licznik)
                    g.Undone += extra;
                }
            }
            catch (Exception e)
            {
                if (!_errUndo) { _errUndo = true; Log.Error("CaravanBulk.LeftPostfix", e); }
            }
        }

        /// <summary>Ile sztuk w jednym kroku zakupu. Najwyzej BuyStep i najwyzej trzecia czesc tego, na co starcza budzetu po cenie
        /// pierwszej sztuki (w obrebie kroku cena rosnie); gdy budzetu starcza na mniej niz trzy sztuki - po jednej, czyli dokladnie
        /// po cenie z cennika. Zero = nie stac nawet na jedna.</summary>
        internal static int Step(int left, int afford)
        {
            if (left <= 0 || afford <= 0) return 0;
            return Math.Max(1, Math.Min(Math.Min(left, BuyStep), afford / 3));
        }

        /// <summary>Rozkaz gracza dla jego karawany (BK): filtr kategorii, ktore wolno teraz kupowac; null = brak rozkazu albo brak BK.</summary>
        private static Func<ItemCategory, bool> OrderFilter(MobileParty mp)
        {
            if (_ordersInstance == null || _ordersFilter == null) return null;
            try
            {
                var beh = _ordersInstance.Invoke(null, null);
                return beh == null ? null : _ordersFilter.Invoke(beh, new object[] { mp }) as Func<ItemCategory, bool>;
            }
            catch (Exception e)
            {
                if (!_errOrder) { _errOrder = true; Log.Error("CaravanBulk.OrderFilter", e); }
                return null;
            }
        }

        /// <summary>Dokad jedzie ladunek: przy wyjezdzie cel BK jest juz nadany - liczymy wyjazdy z surowcem w jukach i te do miasta,
        /// ktoremu go teraz brakuje (ta sama miara co przy wjezdzie). Sam odczyt - do logu.</summary>
        private static void Routed(MobileParty mp, Settings s)
        {
            try
            {
                var to = mp.TargetSettlement;
                var town = to != null ? to.Town : null;
                var shelf = town != null && town.IsTown && town.Owner != null ? town.Owner.ItemRoster : null;
                var pack = mp.ItemRoster;
                foreach (var g in _goods)
                {
                    if (pack.GetItemNumber(g.Item) <= 0) continue;
                    g.Out++;
                    if (shelf != null && Target(town, g, s) - Math.Max(shelf.GetItemNumber(g.Item), FreeOf(g)) > 0) g.OutShort++;
                }
            }
            catch (Exception e) { if (!_errRoute) { _errRoute = true; Log.Error("CaravanBulk.Routed", e); } }
        }

        /// <summary>Postfiks BKCaravansBehavior.BuyGoods: nasz zakup zaraz po zakupach BK, a przed wyborem celu (HourlyTickPartyImpl wola
        /// BuyGoods, potem ThinkNextDestination) - cel uwzglednia juz swiezy ladunek. Raz na wizyte: gdy BK nie znajdzie celu i karawana
        /// zostaje, kolejne wywolania BuyGoods nic nie dokupuja. Przed startem kampanii (kursy wstepne BK) Trades() nie przepuszcza.</summary>
        public static void BuyGoodsPostfix(MobileParty __0, Town __1)
        {
            if (__0 == null || __1 == null || !__0.IsCaravan || !__1.IsTown) return;
            try
            {
                var s = Settings.Current;
                if (!_wired || !_wiredBuy || s == null || !s.CaravanBulkEnabled || !s.CaravanBulkBuyBeforeRoute || !Trades(__0) || !Ready()) return;
                if (__0.CurrentSettlement == null || __0.CurrentSettlement.Town != __1) return;   // tylko karawana stojaca w tym miescie
                if (!_boughtHere.Add(__0)) return;                                                 // w tej wizycie juz kupowala
                EnsureWorld(s);
                int[] came = null;
                if (_cameWith.Count > 0) _cameWith.TryGetValue(__0, out came);                     // migawke zdejmuje dopiero wyjazd
                Buy(__0, __1, s, came, true);
            }
            catch (Exception e)
            {
                if (!_errEarly) { _errEarly = true; Log.Error("CaravanBulk.BuyGoodsPostfix", e); }   // raz na sesje; nastepna wizyta probuje znowu
            }
        }

        private static void Buy(MobileParty mp, Town town, Settings s, int[] came, bool early)
        {
            _departures++;
            if (early) _early++; else _late++;
            var pack = mp.ItemRoster;
            var shelf = town.Owner.ItemRoster;
            float cap = mp.InventoryCapacity;
            float allow = cap * MBMath.ClampFloat(s.CaravanBulkCapacityShare, 0f, FillLimit);   // kg na wszystkie surowce masowe razem
            float bulk = 0f;
            foreach (var g in _goods) bulk += pack.GetItemNumber(g.Item) * g.Kg;
            // surowiec z zyskiem wchodzi takze w te czesc jukow, ktorej BK nie uzywa (ponad 80% udzwigu) - nie wypiera nic z jego zakupow
            // w tym miescie; karawany gracza zostaja przy kresce BK
            float fill = Players(mp) ? FillLimit : MBMath.ClampFloat(s.CaravanBulkFillLimit, FillLimit, 1f);
            float carried = mp.TotalWeightCarried;
            float under = Math.Max(0f, cap * FillLimit - carried);      // wolne pod kreska BK - tyle mielismy dotad
            float room = Math.Min(allow - bulk, cap * fill - carried);
            if (room <= 0f)
            {
                _full++;
                foreach (var g in _goods)      // przyczyna na surowiec: nadwyzka byla, miejsca w jukach nie
                    if (shelf.GetItemNumber(g.Item) - Keep(town, g, s) > 0) { g.DepSurplus++; g.NoRoom++; }
                return;
            }
            float cover = Math.Max(0f, s.CaravanBulkTransitCover);
            int budget = mp.PartyTradeGold / 2;        // jak BK: na zakupy najwyzej pol kiesy
            if (budget <= 0) return;
            Func<ItemCategory, bool> orders = null;    // pytamy BK dopiero, gdy jest co kupic
            bool asked = false, broke = false;
            List<(EquipmentElement, int)> done = null;
            float kg = 0f;
            var order = BuyOrder(mp, town, shelf, s);
            int at0 = 0;
            for (; at0 < order.Length; at0++)
            {
                var g = order[at0];
                int at = shelf.FindIndexOfItem(g.Item);
                if (at < 0) continue;
                var el = shelf.GetElementCopyAtIndex(at);
                int surplus = el.Amount - Keep(town, g, s);
                if (surplus <= 0) continue;
                g.DepSurplus++;
                var cat = g.Item.ItemCategory;
                if (!asked) { asked = true; orders = OrderFilter(mp); }
                if (orders != null && !orders(cat)) { _ordered++; g.Ordered++; continue; }   // rozkaz gracza: teraz tylko zywnosc
                int mine = pack.GetItemNumber(g.Item);
                if (came != null && mine < came[g.Ix]) { _back++; g.Back++; continue; }   // ten surowiec karawana sprzedala tu w czasie tej wizyty - nie odkupuje
                int gate = (int)(cover * g.Need) - g.Carried;       // tyle jeszcze zdolaja sprzedac wszystkie karawany razem
                if (gate <= 0) { _gated++; g.Gated++; continue; }
                int fit = (int)(Math.Min(room, allow * OneGoodPart - mine * g.Kg) / g.Kg);
                if (fit <= 0) { _full++; g.NoRoom++; continue; }
                int left = Math.Min(Math.Min(surplus, gate), fit), got = 0;
                while (left > 0)
                {
                    // cena i indeks od nowa przed kazdym krokiem: z kazda sztuka zdjeta z polki towar drozeje
                    if (cat != null && g.AvgIndex > 0f && town.GetItemCategoryPriceIndex(cat) >= g.AvgIndex) { if (got == 0) { _dear++; g.Dear++; } break; }
                    int price = Math.Max(1, town.GetItemPrice(el.EquipmentElement, mp, false));
                    // oplaca sie tylko, poki nastepna sztuka kosztuje mniej, niz dadza za nia miasta z brakiem (prawdziwe ceny gry)
                    if (price >= g.SellPrice) { if (got == 0) { _noGain++; g.NoGain++; } break; }
                    int n = Step(left, budget / price);
                    if (n <= 0) break;
                    int purse = mp.PartyTradeGold, had = pack.GetItemNumber(g.Item);
                    try { SellItemsAction.Apply(town.Owner, mp.Party, el, n, town.Settlement); }
                    catch (Exception e) { broke = true; if (!_errBuy) { _errBuy = true; Log.Error("CaravanBulk.Buy", e); } }
                    int moved = pack.GetItemNumber(g.Item) - had;   // liczymy to, co faktycznie trafilo do jukow
                    int paid = Math.Max(0, purse - mp.PartyTradeGold);
                    if (broke && moved > 0 && paid == 0) { GiveBack(pack, shelf, el.EquipmentElement, moved); moved = 0; }   // towar poszedl, zloto nie - cofamy
                    if (moved > 0) { got += moved; left -= moved; budget -= paid; _caravansPaid += paid; g.Gain += moved * g.SellPrice - paid; }
                    if (moved <= 0 || broke) break;
                }
                if (got > 0)
                {
                    room -= got * g.Kg; kg += got * g.Kg;
                    g.Bought += got; g.Carried += got; g.DepBought++;
                    if (done == null) { g.First++; done = new List<(EquipmentElement, int)>(); }   // od tego surowca ten wyjazd zaczal zakupy
                    done.Add((el.EquipmentElement, -got));
                }
                if (broke || room <= 0f || budget <= 0) break;
            }
            if (!broke && room <= 0f)      // juki pelne przed koncem listy: reszta surowcow z nadwyzka tez odpadla z braku miejsca
                for (int i = at0 + 1; i < order.Length; i++)
                {
                    var g = order[i];
                    if (shelf.GetItemNumber(g.Item) - Keep(town, g, s) > 0) { g.DepSurplus++; g.NoRoom++; }
                }
            if (kg > 0f) { _kgBought += kg; _kgOver += Math.Max(0f, kg - under); }
            if (done == null) return;
            _buys++;
            try { CampaignEventDispatcher.Instance.OnCaravanTransactionCompleted(mp, town, done); } catch { }   // jak BK po zakupie (ilosc ujemna)
        }

        /// <summary>Postfiks BKCaravansBehavior.CalculateBuyValue: BK nie wycenia zakupu kategorii surowcow masowych - kupuje je tylko regula.</summary>
        public static void BuyValuePostfix(ItemCategory __0, ref float __result)
        {
            if (__result <= 0f || __0 == null || !_wired) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CaravanBulkEnabled || !Ready() || !_cats.Contains(__0)) return;
                __result = 0f;
                _bkZeroed++;
            }
            catch (Exception e)
            {
                if (!_errValue) { _errValue = true; Log.Error("CaravanBulk.BuyValuePostfix", e); }
            }
        }

        // ------------------------------------------------------------ log dnia
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || !_wired || !Ready()) return;
            int now = (int)CampaignTime.Now.ToDays;
            Recount(s, now);
            if (_cameWith.Count > 0)      // karawana rozbita albo rozwiazana w miescie nie zglasza wyjazdu - jej migawka by zostala
            {
                List<MobileParty> gone = null;
                foreach (var kv in _cameWith)
                    if (kv.Key == null || !kv.Key.IsActive || kv.Key.CurrentSettlement == null) { if (gone == null) gone = new List<MobileParty>(); gone.Add(kv.Key); }
                if (gone != null) foreach (var k in gone) _cameWith.Remove(k);
            }
            if (_boughtHere.Count > 0)    // to samo dla znacznika "kupila w tej wizycie"
                _boughtHere.RemoveWhere(k => k == null || !k.IsActive || k.CurrentSettlement == null);
            int day = now - 1;
            var sold = new StringBuilder(); var bought = new StringBuilder(); var undone = new StringBuilder(); var world = new StringBuilder();
            var first = new StringBuilder(); var gain = new StringBuilder();
            float gainAll = 0f;
            foreach (var g in _all)
            {
                if (g.Item == null) continue;
                if (g.Sold > 0) sold.Append(sold.Length > 0 ? ", " : "").Append(g.Name).Append(' ').Append(g.Sold);
                if (g.Bought > 0)
                {
                    bought.Append(bought.Length > 0 ? ", " : "").Append(g.Name).Append(' ').Append(g.Bought);
                    gain.Append(gain.Length > 0 ? ", " : "").Append(g.Name).Append(' ').Append((g.Gain / (g.Bought * g.Kg)).ToString("0.###"));
                    gainAll += g.Gain;
                }
                if (g.First > 0) first.Append(first.Length > 0 ? ", " : "").Append(g.Name).Append(' ').Append(g.First);
                if (g.Undone > 0) undone.Append(undone.Length > 0 ? ", " : "").Append(g.Name).Append(' ').Append(g.Undone);
                world.Append(world.Length > 0 ? "; " : "").Append(g.Name).Append(": ponizej zapasu docelowego ")
                     .Append(g.LoggedShort >= 0 ? g.LoggedShort.ToString() : "?").Append(" -> ").Append(g.Short)
                     .Append(" (bez towaru ").Append(g.LoggedEmpty >= 0 ? g.LoggedEmpty.ToString() : "?").Append(" -> ").Append(g.Empty)
                     .Append("), brakuje ").Append(g.Need).Append(", w jukach karawan ").Append(g.Carried)
                     .Append(", cena zbytu ").Append(g.Buyers > 0 ? g.SellPrice.ToString("0.0") + " d (kupuje " + g.Buyers + " miast)" : "brak - nikt nie kupuje")
                     .Append(Glut(g, s) ? ", KUPNO STOI - rozladunek do progu nadwyzki" : "");
                g.LoggedShort = g.Short; g.LoggedEmpty = g.Empty;
            }
            Log.Info("Karawany: dzien " + day + " - wjazdy karawan do miast " + _entries + ", w tym ze sprzedaza surowcow " + _sales
                     + "; sprzedane miastom z brakiem: " + (sold.Length > 0 ? sold.ToString() : "nic") + " (miasta zaplacily " + _townsPaid + " d)"
                     + "; wyjazdy z miast " + _departures + ", w tym z zakupem " + _buys
                     + "; kupione z nadwyzek: " + (bought.Length > 0 ? bought.ToString() : "nic") + " (karawany zaplacily " + _caravansPaid + " d)"
                     + "; kolejnosc kupna wedle zysku na kilogram - wyjazdow, ktore zaczely zakupy od: " + (first.Length > 0 ? first.ToString() : "-")
                     + "; oczekiwany zysk z kupionego w d na kilogram (cena zbytu w miastach z brakiem minus zaplacone): " + (gain.Length > 0 ? gain.ToString() : "-")
                     + " (razem " + gainAll.ToString("0") + " d)"
                     + "; zakupy gabki BK oddane na polke: " + (undone.Length > 0 ? undone.ToString() : "nic") + " (zwrot " + _refunded + " d)"
                     + "; zakup odpuszczony: w drodze dosc " + _gated + ", cena nie nizsza od sredniej " + _dear + ", bez zysku wobec ceny zbytu " + _noGain
                     + ", brak miejsca w jukach " + _full
                     + ", sprzedane tu w tej wizycie " + _back + ", rozkaz gracza (tylko zywnosc) " + _ordered
                     + "; sprzedaz wstrzymana rezerwa kasy miasta " + _poor + "; wyceny zakupu BK wyzerowane " + _bkZeroed
                     + (s.CaravanBulkEnabled ? "." : ". REGULA WYLACZONA w ustawieniach."));
            Log.Info("Karawany (stan): dzien " + day + " - miast " + _towns + ", karawan " + _caravans + " (liczby: wczoraj -> dzis; sztuka rudy i drewna = ladunek) - " + world + ".");
            // poprawka 115: dwie nowe linie (stare zostaja bez zmian - czyta je tools/sprawdz_logi.py)
            var why = new StringBuilder(); var road = new StringBuilder(); var held = new StringBuilder();
            foreach (var g in _all)
            {
                if (g.Item == null) continue;
                if (g.DepSurplus > 0)
                    why.Append(why.Length > 0 ? "; " : "").Append(g.Name).Append(' ').Append(g.DepSurplus).Append(": kupily ").Append(g.DepBought)
                       .Append(", brak miejsca ").Append(g.NoRoom).Append(", w drodze dosc ").Append(g.Gated).Append(", cena nie nizsza od sredniej ").Append(g.Dear)
                       .Append(", bez zysku ").Append(g.NoGain).Append(", sprzedane tu ").Append(g.Back).Append(", rozkaz gracza ").Append(g.Ordered);
                if (g.In > 0 || g.Out > 0)
                    road.Append(road.Length > 0 ? "; " : "").Append(g.Name).Append(": wjazdy ").Append(g.In).Append(", w tym do miasta z brakiem ").Append(g.InShort)
                        .Append(" (dostawe dostalo ").Append(g.Fed.Count).Append(" miast), wyjazdy ").Append(g.Out).Append(", w tym z celem w miescie z brakiem ").Append(g.OutShort);
                if (g.Carriers > 0)
                    held.Append(held.Length > 0 ? ", " : "").Append(g.Name).Append(' ').Append(g.Carried).Append(" w ").Append(g.Carriers).Append(" karawanach (z tego ")
                        .Append(g.InTown).Append(" stoi w miastach)");
            }
            Log.Info("Karawany (przyczyny): dzien " + day + " - zakup surowcow przed wyborem celu " + _early + " wizyt, przy wyjezdzie " + _late
                     + "; surowce zajely " + _kgBought.ToString("0") + " kg jukow, z tego " + _kgOver.ToString("0") + " kg ponad 80% udzwigu (miejsce, ktorego BK nie uzywa; prog "
                     + (MBMath.ClampFloat(s.CaravanBulkFillLimit, FillLimit, 1f) * 100f).ToString("0") + "%)"
                     + "; wyjazdy z miasta z nadwyzka surowca - " + (why.Length > 0 ? why.ToString() : "brak")
                     + (s.CaravanBulkBuyBeforeRoute && !_wiredBuy ? ". BRAK latki BuyGoods - zakup przy wyjezdzie." : "."));
            Log.Info("Karawany (kierunek): dzien " + day + " - wjazdy do " + _entered.Count + " roznych miast; karawan w miastach " + _inTown + ", w drodze " + Math.Max(0, _caravans - _inTown)
                     + "; ladunek surowcow - " + (road.Length > 0 ? road.ToString() : "brak")
                     + "; w jukach teraz: " + (held.Length > 0 ? held.ToString() : "nic") + ".");
            NewDay();
        }

        private static bool Fits(MethodInfo m, Type ret, params Type[] args)
        {
            if (m == null || m.IsStatic || m.ReturnType != ret) return false;
            var p = m.GetParameters();
            if (p.Length != args.Length) return false;
            for (int i = 0; i < p.Length; i++) if (p[i].ParameterType != args[i]) return false;
            return true;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var bkc = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                var bkp = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
                var value = bkc != null ? AccessTools.Method(bkc, "CalculateBuyValue") : null;
                var left = bkp != null ? AccessTools.Method(bkp, "OnSettlementLeft") : null;
                // parametry bierzemy po pozycji (__0, __1), wiec sprawdzamy typy, nie nazwy
                bool okValue = Fits(value, typeof(float), typeof(ItemCategory), typeof(Town), typeof(float), typeof(float));
                bool okLeft = Fits(left, typeof(void), typeof(MobileParty), typeof(Settlement));
                if (!okValue || !okLeft)
                {
                    Log.Info("CaravanBulk: BRAK metod BannerKings (BKCaravansBehavior.CalculateBuyValue - " + (okValue ? "jest" : "brak albo inna sygnatura")
                             + ", BKPartyBehavior.OnSettlementLeft - " + (okLeft ? "jest" : "brak albo inna sygnatura")
                             + ") - regula surowcow masowych WYLACZONA, karawany handluja jak dotad.");
                    return;
                }
                h.Patch(left, prefix: new HarmonyMethod(typeof(CaravanBulk), nameof(LeftPrefix)), postfix: new HarmonyMethod(typeof(CaravanBulk), nameof(LeftPostfix)));
                h.Patch(value, postfix: new HarmonyMethod(typeof(CaravanBulk), nameof(BuyValuePostfix)) { priority = Priority.Last });
                _wired = true;      // dopiero gdy OBIE latki weszly - inaczej wszystkie zaczepy stoja
                // rozkazy gracza dla wlasnych karawan - tylko odczyt, nieobowiazkowe (brak = regula bez wyjatku dla rozkazow)
                try
                {
                    var ord = AccessTools.TypeByName("BannerKings.Behaviours.Caravans.CaravanOrdersBehavior");
                    var inst = ord != null ? AccessTools.PropertyGetter(ord, "Instance") : null;
                    var filt = ord != null ? AccessTools.Method(ord, "GetActiveBuyFilter", new[] { typeof(MobileParty) }) : null;
                    if (inst != null && inst.IsStatic && filt != null && !filt.IsStatic && filt.ReturnType == typeof(Func<ItemCategory, bool>))
                    { _ordersInstance = inst; _ordersFilter = filt; }
                }
                catch (Exception e) { Log.Error("CaravanBulk.ApplyAll (rozkazy BK)", e); }
                // poprawka 115: zakup przed wyborem celu; brak metody = zakup przy wyjezdzie jak dotad
                try
                {
                    var goods = AccessTools.Method(bkc, "BuyGoods");
                    if (Fits(goods, typeof(void), typeof(MobileParty), typeof(Town)))
                    {
                        h.Patch(goods, postfix: new HarmonyMethod(typeof(CaravanBulk), nameof(BuyGoodsPostfix)));
                        _wiredBuy = true;
                    }
                }
                catch (Exception e) { Log.Error("CaravanBulk.ApplyAll (BuyGoods)", e); }
                var s = Settings.Current;
                Log.Info("CaravanBulk: surowce masowe w karawanach wedle brakow miast - latki wpiete (BK CalculateBuyValue, BK OnSettlementLeft), regula "
                         + (s != null && s.CaravanBulkEnabled ? "CZYNNA" : "wylaczona w ustawieniach")
                         + (s != null ? ": zapas docelowy " + s.CaravanBulkStockDays + " dob zuzycia, nadwyzka powyzej x" + s.CaravanBulkSurplusFactor.ToString("0.0")
                                        + ", udzial udzwigu " + (s.CaravanBulkCapacityShare * 100f).ToString("0") + "%, pokrycie brakow x" + s.CaravanBulkTransitCover.ToString("0.0") : "")
                         + "; rozkazy gracza dla karawan (BK): " + (_ordersFilter != null ? "szanowane" : "BRAK w tej wersji BK - bez wyjatku") + ".");
                Log.Info("CaravanBulk: poprawka 115 - zakup przed wyborem celu (BK BuyGoods): " + (_wiredBuy ? "latka wpieta" : "BRAK metody - zakup przy wyjezdzie jak dotad")
                         + (s != null && !s.CaravanBulkBuyBeforeRoute ? " (wylaczone w ustawieniach)" : "")
                         + (s != null ? "; juki na surowce do " + (MBMath.ClampFloat(s.CaravanBulkFillLimit, FillLimit, 1f) * 100f).ToString("0") + "% udzwigu (BK konczy zakupy na 80%; karawany gracza zostaja przy 80%)" : "") + ".");
            }
            catch (Exception e) { Log.Error("CaravanBulk.ApplyAll", e); }
        }
    }
}
