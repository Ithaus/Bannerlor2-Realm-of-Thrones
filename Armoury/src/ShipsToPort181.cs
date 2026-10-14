using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// PACZKA 181 - STATKI DO PORTU (etap 3.3; projekt PROJEKT-ETAP3-SZCZELNOSC-STATKI-2026-10-10 wersja 2, kawalki E3/10, E3/11, E3/12).
    /// DECYZJA: wariant (b) projektu - kod i linia logu pelne, wylaczniki (ShipsToPort, ShipTradeThroughPort, ShipPrizesToPort) do etapu 5 wylaczone.
    /// E5/B5 (paczka "181 ON", projekt etapu 5 v2 rozdz. 2 R18 i rozdz. 3 "E5/B5"; decyzja Jeffa 11.10 (1) "statki razem z podatkiem wojennym 184"):
    /// WSZYSTKIE TRZY wylaczniki DOMYSLNIE WLACZONE; porty placa z nadwyzki D4 (TownPurse.Spare przez Seal31.Spare - przy czynnym A1), wraki po
    /// uszkodzeniu w LootDefeatedPartyShips nie sa pryzami (DestroyPrefix), linia "Statki (181)" podaje kasy portow bez nadwyzki (prog B5: <= 28 dob).
    /// Przy wylaczonych (MCM) gra dokladnie jak dotad:
    /// latki tylko mierza (linia "Statki (181)" - ile zlota z niczego daje dzis kazda sciezka i ile zaplacilyby porty wedlug reguly 181 - symulacja
    /// na kopii licznikow, bez ruszania zlota i statkow), a podmienione wywolania GiveGoldAction (transpiler) wolaja te same metody gry co dotad.
    ///
    /// GRA (dekompilacja 1.4.8 + NavalDLC):
    ///  1. NavalShipDistributionCampaignBehavior.RecoverGoldFromRemainingShipsAfterDistribution (po DistributeShips, przy MobilePartyDestroyed na ladzie
    ///     i OnPartyDisbanded): glowa rodu dostaje z niczego pelna wartosc statkow, ktorych rod nie rozdal (GetShipTradeValue(statek, druzyna, null) =
    ///     kadlub x 1.5; gracz x 0.3). Rozwiazanie na ladzie wola ja DWA razy (DestroyPartyAction.ApplyForDisbanding: OnPartyDisbanded, potem
    ///     MobilePartyDestroyed) - statki nie znikaja po pierwszym razie, wiec gra placi drugi raz (krytyka W6).
    ///  2. ChangeShipOwnerAction.ApplyInternal (ApplyByTrade): port kupuje - sprzedajacy dostaje cene z niczego; port sprzedaje - kupujacy placi
    ///     w nicosc (kasa portu nic nie dostaje). ShipTradeCampaignBehavior: premie zarzadcy portu (MerchantPrince przy handlu, MasterShipwright
    ///     przy naprawie) z niczego; AI sprzedaje w ConsiderSellingShips i przy wymianie w TryPurchasingShipFromTown (najpierw sprzedaz starego, potem kupno).
    ///  3. MapEvent.LootDefeatedPartyShips: statki pokonanych, ktorych zwyciezcy nie wzieli, sa niszczone (DestroyShipAction.Apply), a gdy wodz
    ///     zwyciezcow jest bohaterem i sa partie mogace lupic (flag) - zwyciezcy dostaja z niczego ich pelna wartosc w PlunderedGold (wedlug wkladu).
    ///     E5/B5: wczesniej w tej samej metodzie gra uszkadza statki pokonanych (Ship.OnShipDamaged, 20-50% kadluba - NavalDLCBattleRewardModel);
    ///     statek, ktoremu spadnie do 0, Ship.OnShipDamaged niszczy od razu (DestroyShipAction.Apply - w naszym kontekscie pryzy), a gra nie bierze
    ///     go do podzialu ani za niego nie placi (MapEvent.cs:1773-1774 "HitPoints > 0f") - to wrak, nie pryz.
    ///  Kolejnosc w bitwie (MapEvent.cs:1048-1053, :1411-1431): CalculatePlunderedAndLostGoldAmounts (pula; migawka A6) -> LootDefeatedPartyShips
    ///     (dopisek za statki; 181) -> CaptureDefeatedPartyMembers -> CommitCalculatedMapEventResults (rozliczenie A6, wyplata). Przy WL pryzach
    ///     dopisek jest zdjety w PrizeFin, wiec A6 (CountShips) widzi statki 0, a finalizer pomiaru 164a (Measure31.ShipsLootFin - ten sam priorytet
    ///     Normal, wpiety pozniej, wiec biegnie po PrizeFin) liczy zloto, ktore gra naprawde wyplaci - przy WL 0.
    ///
    /// REGULA 181 (rozdz. 2 R1/R2 projektu; jedna dla gracza i AI):
    ///  - cena statku = cena sprzedazy portowi gry GetShipTradeValue(statek, sprzedajacy, port) = 0.3 x wartosc z politykami - naprawa;
    ///  - port placi z NADWYZKI kasy (Seal31.Spare): przy czynnym A1 (E5/A1) nadwyzka D4 = kasa - zapas kupcow TownPurse.Reserve (ta sama liczba
    ///    co zawor, podatki i nasze zakupy; tarcze zbedne); bez A1 kasa - zapas kupcow (10 000 + 12 x dobrobyt, cel regulatora gry) - tarcza
    ///    zoldu (SoldierPay.HeldOf) - tarcza dworu (162m); nigdy ponizej zapasu (ApplyForSettlementToCharacter nie obcina do kasy - krytyka D2, pilnujemy sami);
    ///  - wybor portu (E3/10, E3/12): miasta z portem i stocznia (poziom > 0), z miejscem do limitu stoczni, nieoblezone; (1) z ShipSalePortsTried
    ///    najblizszych portow wlasnego krolestwa pierwszy, ktory ma cala cene; (2) to samo w portach krolestw nie w wojnie z rodem; (3) inaczej port
    ///    z najwieksza nadwyzka placi, ile moze (sprzedaz z koniecznosci); (4) zaplata 0, cena <= 0 albo brak portu z miejscem - statek ginie (zlom).
    ///    E5/B5 (recenzja 1): stocznia NavalDLC trzyma liczbe idealna (limit - 2) i nastepnej doby kasuje nadmiar (ShipProductionCampaignBehavior
    ///    .DailyTickTown -> TryRemoveExcessShipsFromTown), a do limitu dorabia statki za darmo - zakup ponad liczbe idealna tylko mierzony w linii
    ///    (OverIdeal); miejsce w Gather do limitu stoczni (wiazace), zmiana na liczbe idealna czeka na decyzje Jeffa.
    ///    A17 (poz. 6, decyzja Claude 14.10 - Jeff: ekonomie projektuje Claude): przy ShipPortsBuyToIdeal port kupuje tylko do liczby idealnej
    ///    (Gather pomija port pelny; TradePrefix: port pelny nie kupuje od AI ani od gracza) - B5-14: 37-40% wydatkow portow szlo na statki kasowane.
    ///  - E3/10: statki przechodza do portu (ApplyByTransferring - ten sam wzor co DistributeShips), port placi glowie rodu; drugie wywolanie
    ///    tej samej druzyny w dobie nic nie placi (W6); zlom zostaje przy druzynie i ginie z nia (jak dzis).
    ///  - E3/11: kupno od portu - zaplata do kasy portu; sprzedaz portowi - port placi z nadwyzki; AI nie sprzedaje, gdy port nie ma calej ceny
    ///    (bramka - cala transakcja pominieta; przy wymianie odrzuconej sprzedazy pominiete tez kupno); gracz dostaje najwyzej nadwyzke (wycena
    ///    ekranu portu obcieta do nadwyzki, port bez pieniedzy nie kupuje); premie zarzadcy - z nadwyzki tego portu. E5/B5 (recenzja 2): ekran portu
    ///    wykonuje kupno przed sprzedaza, a gra obcina zaplate do kiesy - gracz bez calej ceny kupna w kiesie nie kupuje (kupno pominiete, komunikat).
    ///  - E3/12: pryz, za ktory gra placi (flag), idzie do portu wzgledem rodu wodza zwyciezcow; port placi zwyciezcom wedlug wkladu (jak gra),
    ///    dopisek gry w PlunderedGold zdjety przed CommitGoldChanges; czesc wodzow do podstawy 1/9 korony (Ransom178.NoteBeatenGold, jak 3.2).
    ///  - KW 168 (E2 R16 "limit bez statkow"): kazda wyplata portu do bohatera idzie z InPortPay - ClanIncomeBook liczy ja w TodayShips i odejmuje
    ///    od wplywow "od prawdziwego platnika" (limit kredytu wojennego bez statkow; splata 50% - jak z lupu).
    /// Ksiega pieniadza: przy wlaczonych wszystkie wyplaty to GiveGoldAction z prawdziwym platnikiem (osada <-> bohater/partia) - "Przeplywy osad"
    /// je licza, okna "statki" i "statki-inne" pokazuja trafien z niczego 0, pozycje "statki NavalDLC inne", "statki: sprzedaz osadom", "statki: kupno
    /// od osad" -> 0, a "bitwy" spada o pryzy. Kazdy wyjatek per obiekt liczony (Stumble) - mechanizmu nie gasimy; wyjatek w sprzedazy = gra placi jak dotad.
    /// </summary>
    internal static class ShipsToPort181
    {
        internal static bool LostOn { get { var s = Settings.Current; return s != null && s.ShipsToPort; } }
        internal static bool TradeOn { get { var s = Settings.Current; return s != null && s.ShipTradeThroughPort; } }
        internal static bool PrizeOn { get { var s = Settings.Current; return s != null && s.ShipPrizesToPort; } }
        /// <summary>A17 (poz. 6, B5-14): port kupuje tylko do liczby idealnej stoczni (limit - 2) - Gather (rozbite druzyny, pryzy) i bramka kupna od partii (TradePrefix).</summary>
        internal static bool IdealOn { get { var s = Settings.Current; return s != null && s.ShipPortsBuyToIdeal; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        /// <summary>KW 168 (E2 R16): trwa wyplata portu za statek - ClanIncomeBook.OnEvent dopisuje ja do TodayShips (poza limitem KW).</summary>
        internal static bool InPortPay;

        /// <summary>E5/B5: suma wszystkich wyplat portow (181) od startu procesu - pomiar 164a (Measure31.RecoverFin) odejmuje roznice z jednego wywolania
        /// RecoverGold (przy WL glowa rodu dostaje zloto z kasy portu, nie z niczego). Tylko rosnie; nikt nie czyta wartosci bezwzglednej.</summary>
        internal static long PaidRun;

        // ------------------------------------------------------------ latki (linia startowa)
        private static bool _wRecover, _wDisband, _wDist, _wTrade, _wPerkTrade, _wPerkRepair, _wSwap, _wPrize, _wDestroy, _wUi, _hooksLogged;
        private static int _ilTrade, _ilPerkTrade, _ilPerkRepair;   // podmienione wywolania GiveGoldAction (transpiler): handel (8 w 1.4.8), premie (po 1)
        private static readonly List<string> _miss = new List<string>();
        private static readonly List<MethodBase> _targets = new List<MethodBase>();
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static int _stumbles;

        // ------------------------------------------------------------ rodzaje
        private const int KCastle = 0, KTown = 1, KNoFief = 2, KMerc = 3, KPlayer = 4, Kinds = 5;
        private static readonly string[] KName = { "zamek", "miasto", "bez lenna", "najemnik", "gracz" };
        private const int WAi = 0, WCaravan = 1, WPlayer = 2, Whos = 3;
        private static readonly string[] WhoName = { "lordowie AI", "karawany", "gracz" };
        private const int POwn = 0, PForeign = 1, PFar = 2, PForced = 3, PScrap = 4;
        private const int ZRoom = 0, ZMoney = 1, ZPrice = 2, ZNoOne = 3;
        private const int PkTrade = 0, PkRepair = 1;

        // ------------------------------------------------------------ stan doby (ClearDay)
        private static readonly Dictionary<Settlement, long> _spent = new Dictionary<Settlement, long>();     // naprawde zaplacone dzis przez porty (181)
        private static readonly Dictionary<Settlement, long> _simSpent = new Dictionary<Settlement, long>();  // symulacja przy wylaczonych (kasa nie maleje)
        private static readonly Dictionary<Settlement, int> _simShips = new Dictionary<Settlement, int>();    // symulacja: statki "kupione" dzis (miejsce w stoczni)
        private static readonly HashSet<MobileParty> _seen = new HashSet<MobileParty>();                     // W6: druzyny, za ktore juz bylo wywolanie dzis

        // 1. statki rozbitej albo rozwiazanej druzyny
        private static int _p1N, _p1DisbN, _p1LostN, _p1Ships, _p1Given, _p1SecondN, _p1SecondSkipped;
        private static long _p1Game, _p1SecondGold;
        private static readonly long[] _p1KindGame = new long[Kinds], _p1KindPaid = new long[Kinds], _p1KindSim = new long[Kinds];
        private static readonly int[] _p1KindN = new int[Kinds];
        // 2./3. handel i premie
        private static readonly int[] _sellN = new int[Whos], _buyN = new int[Whos];
        private static readonly long[] _sellGold = new long[Whos], _buyGold = new long[Whos];
        private static long _sellViaPort, _buyViaPort, _sellCut, _gateGold, _gateSimGold, _playerShortSim, _negGold;
        private static int _gateN, _gateSimN, _swapSkipN, _playerNoCoinN, _playerShortSimN, _negN, _uiCapN;
        private static readonly int[] _perkN = new int[2];
        private static readonly long[] _perkGold = new long[2];
        private static long _perkViaPort, _perkCut;
        // 4. pryzy
        private static int _p4Battles, _p4Sunk, _p4PaidByGameN, _p4NoPayN, _p4Wrecks;   // _p4Wrecks (E5/B5): wraki po uszkodzeniu - gra nie placi, 181 nie rusza
        // E5/B5: kasy portow bez nadwyzki - doby z rzedu (pamiec sesji, jak zaulki A1; czysci Reset, nie ClearDay)
        private static readonly Dictionary<Settlement, int> _dryStreak = new Dictionary<Settlement, int>();
        private static long _p4Game, _p4GamePlayer, _p4Removed, _p4ToLeaders, _p4ToParties, _p4NoRecipient, _p4Ninth;
        // E5/B5 (recenzja 1): zakupy portow, przy ktorych port mial juz liczbe idealna stoczni NavalDLC (limit - 2) - nadmiar, ktory stocznia kasuje (tylko pomiar)
        private const int OvLost = 0, OvTrade = 1, OvPrize = 2, Ovs = 3;
        private static readonly int[] _ovN = new int[Ovs], _ovSimN = new int[Ovs];
        private static readonly long[] _ovGold = new long[Ovs], _ovSimGold = new long[Ovs];
        // E5/B5 (recenzja 2): gracz bez calej ceny kupna w kiesie (kupno przed sprzedaza w jednym potwierdzeniu ekranu portu) - kupno pominiete
        private static int _playerShortBuyN;
        private static long _playerShortBuyGold;
        // A17 (poz. 6): port pelny (liczba idealna stoczni) - pominiety przy wyborze portu (Gather) i odmowy kupna od partii (AI / gracz)
        private static int _idealSkip, _idealGateN, _idealPlayerN;
        private static long _idealGateGold;
        // razem (naprawde dzis, niezaleznie od przelaczenia w ciagu dnia) i kontrolki
        private static long _fromNothing, _toNothing;
        private static int _overSpare, _freeShips;

        /// <summary>Wynik reguly 181 dla jednej sciezki (symulacja albo naprawde).</summary>
        private sealed class RuleStats
        {
            public int N, OwnN, ForN, FarN, ForcedN, ScrapRoomN, ScrapMoneyN, ScrapPriceN, ScrapNoOneN;
            public long Due, Paid, Own, For, Far, Forced, ForcedShort, ScrapMoneyDue;
            public readonly HashSet<Settlement> PortsPaid = new HashSet<Settlement>(), PortsDry = new HashSet<Settlement>();

            public void Clear()
            {
                N = OwnN = ForN = FarN = ForcedN = ScrapRoomN = ScrapMoneyN = ScrapPriceN = ScrapNoOneN = 0;
                Due = Paid = Own = For = Far = Forced = ForcedShort = ScrapMoneyDue = 0;
                PortsPaid.Clear(); PortsDry.Clear();
            }

            public void Note(Pick p, int paid)
            {
                N++;
                for (int i = 0; i < p.DryN; i++) if (p.Dry[i] != null) PortsDry.Add(p.Dry[i]);
                if (p.Kind == PScrap)
                {
                    if (p.Why == ZMoney) { ScrapMoneyN++; ScrapMoneyDue += Math.Max(0, p.Due); Due += Math.Max(0, p.Due); }
                    else if (p.Why == ZPrice) ScrapPriceN++;
                    else if (p.Why == ZNoOne) { ScrapNoOneN++; Due += Math.Max(0, p.Due); }
                    else ScrapRoomN++;
                    return;
                }
                Due += p.Due; Paid += paid;
                if (paid > 0 && p.T != null) PortsPaid.Add(p.T.Settlement);
                if (p.Kind == POwn) { OwnN++; Own += paid; }
                else if (p.Kind == PForeign) { ForN++; For += paid; }
                else if (p.Kind == PFar) { FarN++; Far += paid; }
                else { ForcedN++; Forced += paid; ForcedShort += Math.Max(0, p.Due - paid); }
            }

            public string Text()
            {
                return "statkow " + N + ": nalezne " + Due + ", zaplacone " + Paid + " (" + Pct(Paid, Due) + ") - do portow wlasnego krolestwa " + Own + " (" + OwnN
                       + "), krolestw nie w wojnie " + For + " (" + ForN + "), dalszy port z najwieksza nadwyzka " + Far + " (" + FarN + "), z koniecznosci " + Forced
                       + " (" + ForcedN + ", mniej o " + ForcedShort + "); zlom: brak portu z miejscem " + ScrapRoomN + ", brak pieniedzy " + ScrapMoneyN
                       + " (nalezne " + ScrapMoneyDue + "), cena <= 0 " + ScrapPriceN + (ScrapNoOneN > 0 ? ", bez odbiorcy " + ScrapNoOneN : "")
                       + "; porty placace " + PortsPaid.Count + ", porty bez nadwyzki " + PortsDry.Count;
            }
        }

        private static readonly RuleStats _r1Sim = new RuleStats(), _r1Real = new RuleStats(), _r4Sim = new RuleStats(), _r4Real = new RuleStats();

        // ------------------------------------------------------------ porzadki
        internal static void Reset()
        {
            try
            {
                ClearDay();
                _dryStreak.Clear();
                _hooksLogged = false; _errSites.Clear();
                _trade = null; _prize = null; _perkPort = null; _swap = false; _swapRejected = false; _disbanding = null; InPortPay = false;
            }
            catch { }
        }

        private static void ClearDay()
        {
            _spent.Clear(); _simSpent.Clear(); _simShips.Clear(); _seen.Clear();
            _p1N = _p1DisbN = _p1LostN = _p1Ships = _p1Given = _p1SecondN = _p1SecondSkipped = 0;
            _p1Game = _p1SecondGold = 0;
            Array.Clear(_p1KindGame, 0, Kinds); Array.Clear(_p1KindPaid, 0, Kinds); Array.Clear(_p1KindSim, 0, Kinds); Array.Clear(_p1KindN, 0, Kinds);
            Array.Clear(_sellN, 0, Whos); Array.Clear(_buyN, 0, Whos); Array.Clear(_sellGold, 0, Whos); Array.Clear(_buyGold, 0, Whos);
            _sellViaPort = _buyViaPort = _sellCut = _gateGold = _gateSimGold = _playerShortSim = _negGold = 0;
            _gateN = _gateSimN = _swapSkipN = _playerNoCoinN = _playerShortSimN = _negN = _uiCapN = 0;
            Array.Clear(_perkN, 0, 2); Array.Clear(_perkGold, 0, 2); _perkViaPort = _perkCut = 0;
            _p4Battles = _p4Sunk = _p4PaidByGameN = _p4NoPayN = _p4Wrecks = 0;
            _p4Game = _p4GamePlayer = _p4Removed = _p4ToLeaders = _p4ToParties = _p4NoRecipient = _p4Ninth = 0;
            Array.Clear(_ovN, 0, Ovs); Array.Clear(_ovSimN, 0, Ovs); Array.Clear(_ovGold, 0, Ovs); Array.Clear(_ovSimGold, 0, Ovs);
            _playerShortBuyN = 0; _playerShortBuyGold = 0;
            _idealSkip = _idealGateN = _idealPlayerN = 0; _idealGateGold = 0;
            _fromNothing = _toNothing = 0; _overSpare = _freeShips = 0; _stumbles = 0;
            _r1Sim.Clear(); _r1Real.Clear(); _r4Sim.Clear(); _r4Real.Clear();
        }

        /// <summary>Wyjatek przy jednym obiekcie: liczony zawsze, do pliku raz na miejsce (mechanizmu nie gasimy).</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("ShipsToPort181." + where, e); } catch { }
        }

        private static string Pct(long a, long b) { return b > 0 ? (100.0 * a / b).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%" : "-"; }

        private static void Add(Dictionary<Settlement, long> d, Settlement st, long v) { long x; d.TryGetValue(st, out x); d[st] = x + v; }
        private static void Add(Dictionary<Settlement, int> d, Settlement st, int v) { int x; d.TryGetValue(st, out x); d[st] = x + v; }

        private static int KindOf(Clan c)
        {
            if (c == null) return KNoFief;
            if (c == Clan.PlayerClan) return KPlayer;
            if (c.IsUnderMercenaryService) return KMerc;
            int towns = 0, castles = 0;
            var f = c.Fiefs;
            if (f != null) for (int i = 0; i < f.Count; i++) { var t = f[i]; if (t == null) continue; if (t.IsCastle) castles++; else if (t.IsTown) towns++; }
            return towns > 0 ? KTown : castles > 0 ? KCastle : KNoFief;
        }

        // ------------------------------------------------------------ nadwyzka portu (rozdz. 2 projektu)
        /// <summary>
        /// Nadwyzka portu = jedna funkcja nadwyzki osady E3/1 (Seal31.Spare - poprawka po recenzji 3.1+3.3: dotad osobna formula z tarcza dworu czytana
        /// refleksja): kasa - zapas kupcow (10 000 + 12 x dobrobyt) - tarcza zoldu - tarcza dworu (SoldierPay.CourtOf), z zywej kasy, nie mniej niz 0;
        /// przy czynnym A1 (E5/A1, E5/B5) Seal31.Spare oddaje TownPurse.Spare - nadwyzka D4 ponad zapas kupcow TownPurse.Reserve (tarcze zbedne).
        /// Wyplaty 181 i E3/1 obnizaja kase, wiec obnizaja tez nadwyzke (licznik "wydane dzis" tylko w logu). Porty sa miastami - zamek i wies: 0.
        /// </summary>
        internal static long Spare(Settlement st)
        {
            if (st == null || !st.IsTown || st.Town == null) return 0;
            return Seal31.Spare(st);
        }

        private static long SpareFor(Town t, bool sim)
        {
            long v = Spare(t.Settlement);
            if (sim) { long x; if (_simSpent.TryGetValue(t.Settlement, out x)) v -= x; }
            return v > 0 ? v : 0;
        }

        /// <summary>Port placi z nadwyzki (najwyzej tyle, ile ma ponad zapas) bohaterowi albo partii - GiveGoldAction z prawdziwym platnikiem.</summary>
        private static int PayFromPort(Settlement port, Hero to, PartyBase toParty, int amount)
        {
            if (port == null || amount <= 0 || (to == null && toParty == null)) return 0;
            long spare = Spare(port);
            int pay = (int)Math.Min(amount, spare);
            if (pay <= 0) return 0;
            InPortPay = true;
            try
            {
                if (to != null) GiveGoldAction.ApplyForSettlementToCharacter(port, to, pay, false);
                else GiveGoldAction.ApplyForSettlementToParty(port, toParty, pay, false);
            }
            finally { InPortPay = false; }
            PaidRun += pay;
            Add(_spent, port, pay);
            return pay;
        }

        // ------------------------------------------------------------ stocznia (NavalDLC)
        private static Func<Town, Building> _getShipyard;
        private static bool _shipyardTried;

        private static int MaxShips(Town t)
        {
            var en = default(ExplainedNumber);
            t.AddEffectOfBuildings(BuildingEffectEnum.MaximumShipCount, ref en);   // ten sam limit co ShipProductionCampaignBehavior.GetMaxShipCountForTown
            return (int)en.ResultNumber;
        }

        /// <summary>
        /// E5/B5 (recenzja 1, tylko pomiar): port ma juz co najmniej liczbe idealna stoczni NavalDLC (ShipProductionCampaignBehavior.GetIdealShipCountForTown
        /// = max(limit - 2, 0)) - kupiony statek to nadmiar: DailyTickTown najpierw dorabia za darmo do limitu (szansa 0.5 na dobe), potem
        /// TryRemoveExcessShipsFromTown kasuje statki ponad liczbe idealna (obce kadluby 70%, swoje 30% na statek). Miejsce w Gather zostaje do limitu
        /// stoczni (wiazace "z miejscem do limitu stoczni") - zmiana na liczbe idealna to decyzja Jeffa (wiecej zlomu, mniej zlota dla lordow).
        /// </summary>
        private static bool OverIdeal(Town t, bool sim)
        {
            try
            {
                if (t == null || t.Settlement == null) return false;
                int have = t.AvailableShips != null ? t.AvailableShips.Count : 0;
                if (sim) { int x; if (_simShips.TryGetValue(t.Settlement, out x)) have += x; }
                return have >= Math.Max(0, MaxShips(t) - 2);
            }
            catch (Exception e) { Stumble("OverIdeal", e); return false; }   // pomiar nie przerywa sprzedazy
        }

        private static void NoteOver(int path, bool sim, long gold)
        {
            if (gold <= 0) return;
            if (sim) { _ovSimN[path]++; _ovSimGold[path] += gold; }
            else { _ovN[path]++; _ovGold[path] += gold; }
        }

        private static int ShipyardLevel(Town t)
        {
            if (!_shipyardTried)
            {
                _shipyardTried = true;
                try
                {
                    var ty = AccessTools.TypeByName("NavalDLC.NavalDLCExtensions");
                    var m = ty != null ? AccessTools.Method(ty, "GetShipyard", new[] { typeof(Town) }) : null;
                    if (m != null && m.IsStatic && m.ReturnType == typeof(Building)) _getShipyard = (Func<Town, Building>)Delegate.CreateDelegate(typeof(Func<Town, Building>), m);
                }
                catch { _getShipyard = null; }
            }
            if (_getShipyard == null) return MaxShips(t) > 0 ? 1 : 0;
            var b = _getShipyard(t);
            return b != null ? b.CurrentLevel : 0;
        }

        // ------------------------------------------------------------ wybor portu (wspolny dla E3/10 i E3/12)
        private struct Cand { public Town T; public float D2; public bool Own; }

        internal struct Pick
        {
            public Town T; public int Price, Pay, Due, Kind, Why;
            public Settlement[] Dry; public int DryN;
        }

        private static readonly List<Cand> _cands = new List<Cand>(64);
        private static readonly Comparison<Cand> ByDist = (a, b) => a.D2.CompareTo(b.D2);

        private static void Gather(Clan clan, Vec2 pos, bool sim)
        {
            _cands.Clear();
            var all = Town.AllTowns;
            if (all == null) return;
            var own = clan.Kingdom;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                try
                {
                    if (t == null || !t.IsTown || t.Settlement == null || !t.Settlement.HasPort || t.IsUnderSiege) continue;
                    if (t.Settlement.Party != null && t.Settlement.Party.MapEvent != null) continue;
                    if (ShipyardLevel(t) <= 0) continue;
                    int have = t.AvailableShips != null ? t.AvailableShips.Count : 0;
                    if (sim) { int x; if (_simShips.TryGetValue(t.Settlement, out x)) have += x; }
                    int lim = MaxShips(t);
                    if (have >= lim) continue;
                    // A17 (poz. 6): przy ShipPortsBuyToIdeal miejsce tylko do liczby idealnej stoczni (NavalDLC GetIdealShipCountForTown = max(limit - 2, 0));
                    // statek ponad nia stocznia kasuje nastepnej doby (TryRemoveExcessShipsFromTown) - port zaplacilby za nic
                    if (IdealOn && have >= Math.Max(0, lim - 2)) { _idealSkip++; continue; }
                    var f = t.MapFaction;
                    if (f == null) continue;
                    bool isOwn = own != null && f == own;
                    if (!isOwn && f.IsAtWarWith(clan)) continue;
                    _cands.Add(new Cand { T = t, D2 = pos.DistanceSquared(t.Settlement.GetPosition2D), Own = isOwn });
                }
                catch (Exception e) { Stumble("Gather", e); }
            }
            _cands.Sort(ByDist);
        }

        private static int Price(Ship ship, PartyBase seller, Town t)
        {
            return (int)Campaign.Current.Models.ShipCostModel.GetShipTradeValue(ship, seller, t.Settlement.Party);
        }

        /// <summary>Regula wyboru portu (rozdz. 3 "Wybor portu"); sim - nadwyzka pomniejszona o symulowane wyplaty dnia (kasa nie maleje).</summary>
        private static Pick Choose(Ship ship, PartyBase seller, Clan clan, Vec2 pos, bool sim)
        {
            var p = new Pick { Kind = PScrap, Why = ZRoom, Dry = new Settlement[4] };
            if (ship == null || seller == null || clan == null) return p;
            Gather(clan, pos, sim);
            if (_cands.Count == 0) return p;
            var s = Settings.Current;
            int n = Math.Max(1, Math.Min(12, s != null ? s.ShipSalePortsTried : 3));
            int firstPositive = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int k = 0;
                for (int i = 0; i < _cands.Count; i++)
                {
                    var c = _cands[i];
                    if (c.Own != (pass == 0)) continue;
                    if (k++ >= n) break;
                    int price = Price(ship, seller, c.T);
                    if (price <= 0) continue;
                    if (firstPositive <= 0) firstPositive = price;
                    long spare = SpareFor(c.T, sim);
                    if (spare >= price) { p.T = c.T; p.Price = price; p.Pay = price; p.Due = price; p.Kind = pass == 0 ? POwn : PForeign; return p; }
                    if (spare <= 0 && p.DryN < p.Dry.Length) p.Dry[p.DryN++] = c.T.Settlement;
                }
            }
            // (3) zaden z najblizszych nie ma calej ceny - port z najwieksza nadwyzka placi, ile moze
            Town best = null; long bs = 0;
            for (int i = 0; i < _cands.Count; i++) { long v = SpareFor(_cands[i].T, sim); if (v > bs) { bs = v; best = _cands[i].T; } }
            if (best != null)
            {
                int price = Price(ship, seller, best);
                if (price > 0)
                {
                    p.T = best; p.Price = price; p.Due = price; p.Pay = (int)Math.Min(price, bs);
                    p.Kind = p.Pay < price ? PForced : PFar;
                    return p;
                }
                p.Why = ZPrice; return p;
            }
            // (4) zlom - nikt nie ma pieniedzy (albo cena <= 0 wszedzie, gdzie pytano)
            p.Why = firstPositive > 0 ? ZMoney : ZPrice;
            p.Due = firstPositive;
            return p;
        }

        private static Vec2 PosOf(PartyBase p, MapEvent me)
        {
            try
            {
                if (p != null && p.IsMobile && p.MobileParty != null) return p.MobileParty.GetPosition2D;
                if (p != null && p.IsSettlement && p.Settlement != null) return p.Settlement.GetPosition2D;
                if (me != null) return me.Position.ToVec2();
            }
            catch { }
            return Vec2.Zero;
        }

        private static Clan ClanOf(PartyBase p)
        {
            if (p == null) return null;
            if (p.IsMobile && p.MobileParty != null) return p.MobileParty.ActualClan;
            if (p.IsSettlement && p.Settlement != null) return p.Settlement.OwnerClan;
            return null;
        }

        // ============================================================ E3/10 - statki rozbitej albo rozwiazanej druzyny (181a)
        private static MobileParty _disbanding;
        private static readonly List<Settlement> _msgPorts = new List<Settlement>(4);

        /// <summary>Prefiks NavalShipDistributionCampaignBehavior.OnPartyDisbanded - rozwiazanie (komunikat gracza i podzial w linii).</summary>
        public static void DisbandPre(MobileParty __0, out MobileParty __state) { __state = _disbanding; _disbanding = __0; }
        public static void DisbandFin(MobileParty __state) { _disbanding = __state; }

        /// <summary>DistributeShips: ile statkow rod rozdal innym swoim druzynom (tylko pomiar).</summary>
        public static void DistPre(MobileParty __0, out int __state)
        {
            __state = -1;
            try { if (__0 != null && __0.Ships != null) __state = __0.Ships.Count; } catch { }
        }

        public static void DistPost(MobileParty __0, int __state)
        {
            try { if (__state > 0 && __0 != null && __0.Ships != null) { int d = __state - __0.Ships.Count; if (d > 0) _p1Given += d; } } catch { }
        }

        /// <summary>
        /// Prefiks RecoverGoldFromRemainingShipsAfterDistribution (priorytet zwykly - po oknie O08 ksiegi, Priority.First). Pomiar zawsze; wylaczone -
        /// symulacja reguly i gra placi jak dotad; wlaczone - statki do portow, port placi glowie rodu, gra pominieta.
        /// </summary>
        public static bool RecoverPrefix(MobileParty __0)
        {
            var party = __0;
            if (party == null || !Live) return true;
            Clan clan; int kind; bool second, disb;
            try
            {
                clan = party.ActualClan;
                if (clan == null || clan.IsBanditFaction || clan.Leader == null || !clan.Leader.IsActive || party.Ships == null || party.Ships.Count <= 0) return true;   // gra tez nic nie robi (:52)
                disb = ReferenceEquals(party, _disbanding);
                second = !_seen.Add(party);
                kind = KindOf(clan);
                // to, co gra placi dzis z niczego (te same wywolania co :56-60)
                var model = Campaign.Current.Models.ShipCostModel;
                var ships = party.Ships;
                float sum = 0f;
                for (int i = 0; i < ships.Count; i++) sum += model.GetShipTradeValue(ships[i], party.Party, null);
                int game = (int)sum;
                if (clan == Clan.PlayerClan) game = (int)((float)game * model.GetShipSellingPenalty());
                if (second) { _p1SecondN++; _p1SecondGold += game; }
                else
                {
                    _p1N++; if (disb) _p1DisbN++; else _p1LostN++;
                    _p1Ships += ships.Count; _p1Game += game; _p1KindGame[kind] += game; _p1KindN[kind]++;
                }
                if (!LostOn) _fromNothing += game;
            }
            catch (Exception e) { Stumble("RecoverPrefix(pomiar)", e); return true; }
            if (!LostOn)
            {
                if (!second) { try { Simulate1(party, clan, kind); } catch (Exception e) { Stumble("RecoverPrefix(symulacja)", e); } }
                return true;                                // wylaczone: gra placi jak dotad
            }
            if (second) { _p1SecondSkipped++; return false; }   // W6: drugie wywolanie tej samej druzyny nic nie placi (statki juz w porcie, zlom zostaje przy druzynie)
            try { Sell1(party, clan, kind, disb); return false; }
            catch (Exception e) { Stumble("RecoverPrefix(sprzedaz)", e); return true; }   // za statki, ktore zostaly przy druzynie, placi gra (jak dotad) - liczone
        }

        private static void Simulate1(MobileParty party, Clan clan, int kind)
        {
            var pos = party.GetPosition2D;
            var ships = party.Ships;
            for (int i = 0; i < ships.Count; i++)
            {
                var pk = Choose(ships[i], party.Party, clan, pos, true);
                int pay = 0;
                if (pk.Kind != PScrap)
                {
                    pay = pk.Pay;
                    if (OverIdeal(pk.T, true)) NoteOver(OvLost, true, pay);   // przed dopisaniem statku do symulowanego stanu portu
                    Add(_simSpent, pk.T.Settlement, pay); Add(_simShips, pk.T.Settlement, 1);
                }
                _r1Sim.Note(pk, pay);
                _p1KindSim[kind] += pay;
            }
        }

        private static void Sell1(MobileParty party, Clan clan, int kind, bool disb)
        {
            var pos = party.GetPosition2D;
            var list = new List<Ship>(party.Ships);
            long paidAll = 0; int sold = 0, scrapped = 0;
            _msgPorts.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var ship = list[i];
                if (ship == null || ship.Owner != party.Party) continue;
                var pk = Choose(ship, party.Party, clan, pos, false);
                if (pk.Kind == PScrap) { _r1Real.Note(pk, 0); scrapped++; continue; }
                var st = pk.T.Settlement;
                if (pk.Pay > Spare(st)) _overSpare++;          // kontrolka (prog 0): Choose bierze najwyzej nadwyzke
                bool over = OverIdeal(pk.T, false);            // pomiar (recenzja 1): stan portu przed przekazaniem
                ChangeShipOwnerAction.ApplyByTransferring(st.Party, ship);   // ten sam wzor co DistributeShips (:82) - bezpieczny w tym zdarzeniu
                int paid = PayFromPort(st, clan.Leader, null, pk.Pay);
                if (over) NoteOver(OvLost, false, paid);
                if (paid <= 0) _freeShips++;
                _r1Real.Note(pk, paid);
                _p1KindPaid[kind] += paid;
                paidAll += paid; sold++;
                if (!_msgPorts.Contains(st)) _msgPorts.Add(st);
            }
            if (clan == Clan.PlayerClan && party.Owner != null)
            {
                try { Message1(party, disb, paidAll, sold, scrapped); } catch (Exception e) { Stumble("Message1", e); }
            }
        }

        private static void Message1(MobileParty party, bool disb, long paid, int sold, int scrapped)
        {
            string head = party.Owner.Name + (disb ? "'s party has disbanded." : "'s party was lost.");
            string txt;
            if (sold > 0)
            {
                var names = new List<string>();
                foreach (var st in _msgPorts) names.Add(st.Name.ToString());
                txt = head + (names.Count == 1 ? " The port of " + names[0] : " The ports of " + string.Join(", ", names.ToArray())) + " paid you " + paid + " for its ships.";
                if (scrapped > 0) txt += " Nobody could buy " + scrapped + " of them.";
            }
            else txt = head + " Nobody could buy its ships.";
            MBInformationManager.AddQuickInformation(new TextObject("{=!}" + txt));
        }

        // ============================================================ E3/11 - handel statkami przez kase portu (181b)
        internal sealed class Trade
        {
            public Trade Prev; public Settlement Port; public bool SellerPort, BuyerPort, Player; public int Who, Price, Pay;
            public bool Over, SimOk;                        // pomiar (recenzja 1): port kupuje ponad liczbe idealna stoczni; przy WYL - port zaplacilby (bramka przepuscilaby)
        }

        private static Trade _trade;                        // kontekst biezacego ChangeShipOwnerAction.ApplyInternal (ApplyByTrade)
        private static bool _swap, _swapRejected;           // TryPurchasingShipFromTown: wymiana (sprzedaz starego, potem kupno)

        /// <summary>
        /// Prefiks ChangeShipOwnerAction.ApplyInternal (priorytet zwykly - po oknie O07 ksiegi). Tylko ApplyByTrade: kontekst dla podmienionych
        /// wywolan GiveGoldAction; bramka AI (port bez calej ceny w nadwyzce nie kupuje - cala transakcja pominieta); gracz - port bez pieniedzy nie kupuje.
        /// Wylaczone: tylko pomiar i symulacja bramki.
        /// </summary>
        public static bool TradePrefix(PartyBase __0, Ship __1, ChangeShipOwnerAction.ShipOwnerChangeDetail __2, out Trade __state)
        {
            __state = null;
            if (__2 != ChangeShipOwnerAction.ShipOwnerChangeDetail.ApplyByTrade || __0 == null || __1 == null || !Live) return true;
            Trade c;
            try
            {
                var owner = __1.Owner; var buyer = __0;
                if (owner == null) return true;
                c = new Trade { Prev = _trade, SellerPort = owner.IsSettlement, BuyerPort = buyer.IsSettlement };
                if (c.SellerPort == c.BuyerPort) return true;                   // takich wywolan gra nie ma - bez zmian
                c.Port = c.SellerPort ? owner.Settlement : buyer.Settlement;
                var mp = (c.SellerPort ? buyer : owner).MobileParty;
                if (c.Port == null || mp == null) return true;
                c.Player = mp.ActualClan == Clan.PlayerClan;
                c.Who = mp.IsCaravan || mp.IsVillager ? WCaravan : c.Player ? WPlayer : WAi;
                c.Price = (int)Campaign.Current.Models.ShipCostModel.GetShipTradeValue(__1, owner, buyer);   // to samo wywolanie co gra (:24)
                c.Pay = c.Price;
                bool on = TradeOn;
                if (c.BuyerPort && c.Price > 0)
                {
                    long spare = Spare(c.Port);
                    c.Over = OverIdeal(c.Port.Town, false);
                    // A17 (poz. 6): port pelny (liczba idealna stoczni) nie kupuje - statek zostaje u sprzedajacego; AI i gracz ta sama regula
                    if (on && IdealOn && c.Over)
                    {
                        if (c.Who != WPlayer) { _idealGateN++; _idealGateGold += c.Price; if (_swap) _swapRejected = true; return false; }
                        _idealPlayerN++; MessageFull(c.Port); return false;
                    }
                    c.SimOk = c.Who != WPlayer ? spare >= c.Price : spare > 0;
                    if (c.Who != WPlayer)
                    {
                        if (spare < c.Price)
                        {
                            if (on) { _gateN++; _gateGold += c.Price; if (_swap) _swapRejected = true; return false; }   // bramka AI: statek zostaje
                            _gateSimN++; _gateSimGold += c.Price;
                        }
                    }
                    else
                    {
                        c.Pay = (int)Math.Min(c.Price, spare);
                        if (on && c.Pay <= 0) { _playerNoCoinN++; MessageNoCoin(c.Port); return false; }   // port bez pieniedzy nie kupuje - statek zostaje
                        if (!on && spare < c.Price) { _playerShortSimN++; _playerShortSim += c.Price - Math.Max(0L, spare); }
                    }
                }
                else if (c.SellerPort && on)
                {
                    if (_swap && _swapRejected) { _swapSkipN++; return false; }   // wymiana: sprzedaz starego odrzucona -> bez kupna (limit statkow druzyny)
                    // E5/B5 (recenzja 2): ekran portu wykonuje KUPNO przed SPRZEDAZA (PortScreenTradeModeHandler.OnConfirmChanges :176-186), a GetCanConfirm
                    // sprawdza tylko saldo (kupno - sprzedaz <= zloto gracza). GiveGoldAction obcina zaplate do kiesy kupujacego (G GiveGoldAction :13),
                    // statek i tak przechodzi - port dostalby mniej, a zaraz potem placi pelna cene sprzedazy z nadwyzki. Gracz bez calej ceny w kiesie,
                    // z ktorej gra pobierze zaplate - kupno pominiete (statek zostaje w porcie; ten sam wzor co "port bez pieniedzy").
                    if (c.Player && c.Price > 0 && BuyerGold(mp) < c.Price) { _playerShortBuyN++; _playerShortBuyGold += c.Price; MessageShortBuy(__1); return false; }
                }
            }
            catch (Exception e) { Stumble("TradePrefix", e); return true; }
            _trade = c; __state = c;
            return true;
        }

        public static void TradeFin(Trade __state) { if (__state != null) _trade = __state.Prev; }

        public static void SwapPre(out bool __state) { __state = _swap; _swap = true; _swapRejected = false; }
        public static void SwapFin(bool __state) { _swap = __state; _swapRejected = false; }

        private static void MessageFull(Settlement port)
        {
            try { MBInformationManager.AddQuickInformation(new TextObject("{=!}The shipyard of " + port.Name + " already keeps all the ships it can use - the port buys none today.")); } catch { }
        }

        private static void MessageNoCoin(Settlement port)
        {
            try { MBInformationManager.AddQuickInformation(new TextObject("{=!}The port of " + port.Name + " has no spare coin to buy ships today.")); } catch { }
        }

        /// <summary>Kiesa, z ktorej gra pobierze cene kupna od portu (ChangeShipOwnerAction.ApplyInternal G :27-43): karawana / tabor - kiesa handlowa partii;
        /// inaczej glowa rodu, potem wodz, na koncu kiesa handlowa.</summary>
        private static long BuyerGold(MobileParty mp)
        {
            if (mp.IsCaravan || mp.IsVillager) return mp.PartyTradeGold;
            var leader = mp.ActualClan != null ? mp.ActualClan.Leader : null;
            if (leader != null) return leader.Gold;
            if (mp.LeaderHero != null) return mp.LeaderHero.Gold;
            return mp.PartyTradeGold;
        }

        private static void MessageShortBuy(Ship ship)
        {
            try
            {
                MBInformationManager.AddQuickInformation(new TextObject("{=!}You lack the coin to buy " + (ship != null && ship.Name != null ? ship.Name.ToString() : "this ship")
                                                                        + " now - purchases are paid before sales. Sell first, then buy."));
            }
            catch { }
        }

        /// <summary>
        /// Podmiana GiveGoldAction.ApplyBetweenCharacters w ChangeShipOwnerAction.ApplyInternal (transpiler). Bez kontekstu albo wylaczone -
        /// dokladnie to samo wywolanie gry (plus pomiar). Wlaczone: kupno od portu -> kasa portu; sprzedaz portowi -> port placi z nadwyzki.
        /// </summary>
        public static void T_Between(Hero giver, Hero recipient, int amount, bool disableNotification)
        {
            var c = _trade;
            int mode = 0;                                   // 0 - jak gra, 1 - kupujacy placi (port sprzedaje), 2 - port kupuje, 3 - port kupuje za cene <= 0
            try
            {
                if (c != null)
                {
                    if (c.SellerPort && giver != null && recipient == null) mode = 1;
                    else if (c.BuyerPort && giver == null && recipient != null) mode = amount > 0 ? 2 : 3;
                }
            }
            catch { mode = 0; }
            bool on = mode != 0 && TradeOn;
            int g0 = 0;
            try { if (mode == 1) g0 = giver.Gold; else if (mode == 3) g0 = recipient.Gold; } catch { }
            int paid = 0;
            if (!on) GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, disableNotification);
            else if (mode == 1) GiveGoldAction.ApplyForCharacterToSettlement(giver, c.Port, amount, disableNotification);
            else if (mode == 3) { if (amount < 0) GiveGoldAction.ApplyForCharacterToSettlement(recipient, c.Port, -amount, disableNotification); }   // uszkodzony statek: sprzedajacy doplaca porcie (nie w nicosc)
            else paid = PayFromPort(c.Port, recipient, null, Math.Min(amount, c.Pay));
            if (mode == 0) return;
            try
            {
                if (mode == 1) { int taken = g0 - giver.Gold; _buyN[c.Who]++; _buyGold[c.Who] += taken; if (on) _buyViaPort += taken; else _toNothing += taken; }
                else if (mode == 3) { _negN++; _negGold += g0 - recipient.Gold; if (!on) _toNothing += g0 - recipient.Gold; }
                else NoteSell(c, on, amount, paid);
            }
            catch (Exception e) { Stumble("T_Between", e); }
        }

        /// <summary>Podmiana GiveGoldAction.ApplyForPartyToCharacter (karawana/tabor kupuje statek od portu - dzis w nicosc).</summary>
        public static void T_PartyToChar(PartyBase giverParty, Hero recipient, int amount, bool disableNotification)
        {
            var c = _trade;
            bool mine = false;
            try { mine = c != null && c.SellerPort && giverParty != null && recipient == null && giverParty.IsMobile && giverParty.MobileParty != null; } catch { mine = false; }
            bool on = mine && TradeOn;
            int g0 = 0;
            try { if (mine) g0 = giverParty.MobileParty.PartyTradeGold; } catch { }
            if (!on) GiveGoldAction.ApplyForPartyToCharacter(giverParty, recipient, amount, disableNotification);
            else GiveGoldAction.ApplyForPartyToSettlement(giverParty, c.Port, amount, disableNotification);
            if (!mine) return;
            try { int taken = g0 - giverParty.MobileParty.PartyTradeGold; _buyN[c.Who]++; _buyGold[c.Who] += taken; if (on) _buyViaPort += taken; else _toNothing += taken; }
            catch (Exception e) { Stumble("T_PartyToChar", e); }
        }

        /// <summary>Podmiana GiveGoldAction.ApplyForCharacterToParty (karawana/tabor sprzedaje statek portowi - dzis z niczego).</summary>
        public static void T_CharToParty(Hero giver, PartyBase recipientParty, int amount, bool disableNotification)
        {
            var c = _trade;
            bool mine = c != null && c.BuyerPort && giver == null && recipientParty != null && amount > 0;
            bool on = mine && TradeOn;
            int paid = 0;
            if (!on) GiveGoldAction.ApplyForCharacterToParty(giver, recipientParty, amount, disableNotification);
            else paid = PayFromPort(c.Port, null, recipientParty, Math.Min(amount, c.Pay));
            if (!mine) return;
            try { NoteSell(c, on, amount, paid); } catch (Exception e) { Stumble("T_CharToParty", e); }
        }

        private static void NoteSell(Trade c, bool on, int amount, int paid)
        {
            if (c.Over)
            {
                if (on) NoteOver(OvTrade, false, paid);
                else if (c.SimOk) NoteOver(OvTrade, true, Math.Min(amount, c.Pay));   // WYL: port zaplacilby (bramka przepuscilaby, gracz najwyzej nadwyzke)
            }
            _sellN[c.Who]++;
            if (!on) { _sellGold[c.Who] += amount; _fromNothing += amount; return; }
            _sellGold[c.Who] += paid; _sellViaPort += paid;
            if (paid < amount)
            {
                _sellCut += amount - paid;
                if (c.Player)
                {
                    try
                    {
                        MBInformationManager.AddQuickInformation(new TextObject("{=!}The port of " + c.Port.Name + " could pay only " + paid + " of " + amount + " for your ship."));
                    }
                    catch { }
                }
            }
        }

        // premie zarzadcy portu (ShipTradeCampaignBehavior.OnShipOwnerChanged / OnShipRepaired)
        public struct PerkState { public bool Set; public Settlement Port; public int Kind; }
        private static Settlement _perkPort;
        private static int _perkKind;

        public static void PerkTradePre(Ship __0, PartyBase __1, out PerkState __state)
        {
            __state = new PerkState { Set = true, Port = _perkPort, Kind = _perkKind };
            try
            {
                Settlement p = null;
                if (__1 != null && __1.IsSettlement) p = __1.Settlement;
                else if (__0 != null && __0.Owner != null && __0.Owner.IsSettlement) p = __0.Owner.Settlement;
                _perkPort = p; _perkKind = PkTrade;
            }
            catch { _perkPort = null; }
        }

        public static void PerkRepairPre(Settlement __1, out PerkState __state)
        {
            __state = new PerkState { Set = true, Port = _perkPort, Kind = _perkKind };
            try { _perkPort = __1 != null && __1.IsTown ? __1 : null; _perkKind = PkRepair; } catch { _perkPort = null; }
        }

        public static void PerkFin(PerkState __state) { if (__state.Set) { _perkPort = __state.Port; _perkKind = __state.Kind; } }

        /// <summary>Podmiana GiveGoldAction.ApplyBetweenCharacters w premiach zarzadcy: wylaczone - jak gra (z niczego); wlaczone - z nadwyzki portu.</summary>
        public static void P_Between(Hero giver, Hero recipient, int amount, bool disableNotification)
        {
            var port = _perkPort; int k = _perkKind == PkRepair ? PkRepair : PkTrade;
            bool mine = port != null && giver == null && recipient != null && amount > 0;
            bool on = mine && TradeOn;
            int paid = 0;
            if (!on) GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, disableNotification);
            else paid = PayFromPort(port, recipient, null, amount);
            if (!mine) return;
            try
            {
                _perkN[k]++;
                if (!on) { _perkGold[k] += amount; _fromNothing += amount; }
                else { _perkGold[k] += paid; _perkViaPort += paid; _perkCut += amount - paid; }
            }
            catch (Exception e) { Stumble("P_Between", e); }
        }

        /// <summary>Postfiks PortScreenTradeModeHandler.GetTradeCostOfShip: gracz sprzedaje portowi - wycena ekranu najwyzej nadwyzka portu (tylko wlaczone).</summary>
        private static FieldInfo _uiLeft, _uiRight;

        public static void UiPricePost(object __instance, bool __1, ref int __result)
        {
            if (!__1 || __result <= 0 || __instance == null || !TradeOn) return;   // __1 = isRightSideSelling (gracz sprzedaje)
            try
            {
                var left = _uiLeft != null ? _uiLeft.GetValue(__instance) as PartyBase : null;
                var right = _uiRight != null ? _uiRight.GetValue(__instance) as PartyBase : null;
                if (left == null || !left.IsSettlement || right == null || !right.IsMobile) return;
                long spare = Spare(left.Settlement);
                if (spare < __result) { __result = (int)Math.Max(0L, spare); _uiCapN++; }
            }
            catch (Exception e) { Stumble("UiPricePost", e); }
        }

        // ============================================================ E3/12 - pryzy z bitwy morskiej do portu (181c)
        internal sealed class Prize
        {
            public Prize Prev; public bool Flag; public Clan Clan; public PartyBase Seller; public Vec2 Pos; public int Contrib, Sunk;
            public readonly List<MapEventParty> Plund = new List<MapEventParty>(), W = new List<MapEventParty>();
            public readonly List<int> Pg0 = new List<int>();
        }

        private static Prize _prize;

        /// <summary>Prefiks MapEvent.LootDefeatedPartyShips: zwyciezcy i ich PlunderedGold przed, warunek zaplaty gry (flag :1783), kontekst dla DestroyShipAction.</summary>
        public static void PrizePrefix(MapEvent __instance, MBReadOnlyList<MapEventParty> __0, out Prize __state)
        {
            __state = null;
            if (__instance == null || __0 == null || !Live) return;
            try
            {
                var me = __instance;
                if (!me.IsNavalMapEvent || me.RetreatingSide != BattleSideEnum.None || (me.MapEventSettlement != null && me.MapEventSettlement.IsVillage)) return;   // gra tez nic nie robi (:1752)
                var st = new Prize { Prev = _prize };
                var win = me.Winner;
                var lp = win != null ? win.LeaderParty : null;
                var pl = Campaign.Current.Models.BattleRewardModel.GetWinnerPartiesThatCanPlunderGoldFromShips(__0);   // czysta funkcja modelu (NavalDLC: wklad > 0, bez gracza, band i karawan)
                if (pl != null) for (int i = 0; i < pl.Count; i++) { var w = pl[i]; if (w == null) continue; st.Plund.Add(w); st.Contrib += w.ContributionToBattle; }
                st.Flag = lp != null && lp.LeaderHero != null && st.Plund.Count > 0 && st.Contrib > 0;   // ten sam warunek co gra (:1783)
                st.Seller = lp != null && lp.IsMobile ? lp : (st.Plund.Count > 0 ? st.Plund[0].Party : lp);
                st.Clan = ClanOf(lp);
                st.Pos = PosOf(lp, me);
                for (int i = 0; i < __0.Count; i++) { var w = __0[i]; if (w == null) continue; st.W.Add(w); st.Pg0.Add(w.PlunderedGold); }
                _prize = st; __state = st;
            }
            catch (Exception e) { Stumble("PrizePrefix", e); __state = null; }
        }

        /// <summary>Finalizer LootDefeatedPartyShips: ile gra dopisala zwyciezcom za statki (z niczego dzis); wlaczone - dopisek zdjety (porty juz zaplacily).</summary>
        public static void PrizeFin(Prize __state)
        {
            if (__state == null) return;
            _prize = __state.Prev;
            try
            {
                bool remove = PrizeOn && __state.Flag;
                long add = 0, addPl = 0;
                for (int i = 0; i < __state.W.Count; i++)
                {
                    var w = __state.W[i];
                    int d = w.PlunderedGold - __state.Pg0[i];
                    if (d <= 0) continue;
                    add += d;
                    var mp = w.Party != null ? w.Party.MobileParty : null;
                    if (mp != null && mp.ActualClan == Clan.PlayerClan) addPl += d;
                    if (remove) w.PlunderedGold = __state.Pg0[i];   // dopisek gry za statki (:1817) zdjety - CommitGoldChanges nie wyplaci go z niczego
                }
                if (__state.Sunk > 0 || add > 0) _p4Battles++;
                _p4Game += add; _p4GamePlayer += addPl;
                if (remove) _p4Removed += add; else _fromNothing += add;
            }
            catch (Exception e) { Stumble("PrizeFin", e); }
        }

        /// <summary>
        /// Prefiks DestroyShipAction.Apply w kontekscie LootDefeatedPartyShips: statek niezabrany przez zwyciezcow. Bez zaplaty gry (flag false) - ginie
        /// jak dzis. Wylaczone - symulacja reguly. Wlaczone - statek do portu, port placi zwyciezcom wedlug wkladu; brak kupca - ginie.
        /// Wrak (HitPoints &lt;= 0 - zniszczony uszkodzeniem po bitwie, poza podzialem gry) - ginie jak dzis, tylko licznik (E5/B5).
        /// </summary>
        public static bool DestroyPrefix(Ship __0)
        {
            var st = _prize;
            if (st == null || __0 == null) return true;
            bool moved = false;
            try
            {
                // E5/B5 (sprawdzenie okien przed wlaczeniem): wrak - Ship.OnShipDamaged zniszczyl statek pokonanego przy uszkodzeniu po bitwie (MapEvent.cs:1773,
                // Ship.cs:540-542), zanim gra zbudowala liste do podzialu (tylko HitPoints > 0f, :1774); gra za wrak nie placi - 181 go nie liczy jako pryz
                // i nie sprzedaje (przy WL port placilby zwyciezcom ok. 0.45 wartosci, a stocznia naprawialaby go za darmo - ShipProduction OnShipOwnerChanged)
                if (__0.HitPoints <= 0f) { _p4Wrecks++; return true; }
                st.Sunk++; _p4Sunk++;
                if (!st.Flag) { _p4NoPayN++; return true; }  // gra nikomu nie placi - statek ginie jak dzis (krytyka D11)
                _p4PaidByGameN++;
                bool on = PrizeOn;
                var pk = Choose(__0, st.Seller, st.Clan, st.Pos, !on);
                if (!on)
                {
                    int pay = 0;
                    if (pk.Kind != PScrap)
                    {
                        pay = pk.Pay;
                        if (OverIdeal(pk.T, true)) NoteOver(OvPrize, true, pay);
                        Add(_simSpent, pk.T.Settlement, pay); Add(_simShips, pk.T.Settlement, 1);
                    }
                    _r4Sim.Note(pk, pay);
                    return true;
                }
                if (pk.Kind == PScrap) { _r4Real.Note(pk, 0); return true; }
                if (!AnyRecipient(st, pk.Pay)) { pk.Kind = PScrap; pk.Why = ZNoOne; _r4Real.Note(pk, 0); return true; }
                var port = pk.T.Settlement;
                if (pk.Pay > Spare(port)) _overSpare++;
                bool over = OverIdeal(pk.T, false);
                ChangeShipOwnerAction.ApplyByTransferring(port.Party, __0);
                moved = true;
                int paid = PayPrize(st, port, pk.Pay);
                if (over) NoteOver(OvPrize, false, paid);
                if (paid <= 0) _freeShips++;
                _r4Real.Note(pk, paid);
                return false;                               // statek zostaje w porcie
            }
            catch (Exception e) { Stumble("DestroyPrefix", e); return !moved; }
        }

        private static bool AnyRecipient(Prize st, int pay)
        {
            for (int i = 0; i < st.Plund.Count; i++)
            {
                var w = st.Plund[i];
                int share = (int)Math.Floor((double)w.ContributionToBattle / st.Contrib * pay);
                if (share <= 0) continue;
                var p = w.Party;
                if (p == null) continue;
                if (p.LeaderHero != null) return true;
                if (p.IsMobile && p.MobileParty != null && p.MobileParty.IsPartyTradeActive) return true;
            }
            return false;
        }

        /// <summary>Wyplata portu zwyciezcom wedlug wkladu (jak gra :1806-1818, bez dodatkowej kary gracza - cena portu jest juz cena sprzedazy); reszta z zaokraglen zostaje w porcie.</summary>
        private static int PayPrize(Prize st, Settlement port, int pay)
        {
            int all = 0;
            for (int i = 0; i < st.Plund.Count; i++)
            {
                var w = st.Plund[i];
                int share = (int)Math.Floor((double)w.ContributionToBattle / st.Contrib * pay);
                if (share <= 0) continue;
                var p = w.Party;
                var h = p != null ? p.LeaderHero : null;
                int paid;
                if (h != null) paid = PayFromPort(port, h, null, share);
                else if (p != null && p.IsMobile && p.MobileParty != null && p.MobileParty.IsPartyTradeActive) paid = PayFromPort(port, null, p, share);
                else { _p4NoRecipient += share; continue; }
                all += paid;
                if (h != null)
                {
                    _p4ToLeaders += paid;
                    // pryz to lup: czesc wodza do podstawy 1/9 korony (jak 3.2 - BeatenPurse.SplitPostfix)
                    if (paid > 0 && h.IsAlive && !(p.MobileParty != null && Undead.Party(p.MobileParty))) { Ransom178.NoteBeatenGold(h, paid); _p4Ninth += paid; }
                }
                else _p4ToParties += paid;
            }
            return all;
        }

        // ============================================================ latki
        private static bool Patch(Harmony h, string label, Type t, string method, Type[] args, string pre, string post, string fin, string trans)
        {
            try
            {
                if (t == null) { _miss.Add(label + " (brak typu)"); return false; }
                var m = args != null ? AccessTools.Method(t, method, args) : AccessTools.Method(t, method);
                if (m == null) { _miss.Add(label + " (brak metody " + method + ")"); return false; }
                h.Patch(m,
                        prefix: pre != null ? new HarmonyMethod(typeof(ShipsToPort181), pre) : null,
                        postfix: post != null ? new HarmonyMethod(typeof(ShipsToPort181), post) : null,
                        transpiler: trans != null ? new HarmonyMethod(typeof(ShipsToPort181), trans) : null,
                        finalizer: fin != null ? new HarmonyMethod(typeof(ShipsToPort181), fin) : null);
                _targets.Add(m);
                return true;
            }
            catch (Exception e) { _miss.Add(label + " (blad: " + e.Message + ")"); Log.Error("ShipsToPort181.ApplyAll(" + label + ")", e); return false; }
        }

        // transpilery: GiveGoldAction.X -> nasza metoda o tej samej sygnaturze (bez zmiany stosu); 0 podmian = BRAK w linii startowej
        public static IEnumerable<CodeInstruction> TradeIl(IEnumerable<CodeInstruction> instructions) { int n; var r = SwapCalls(instructions, false, out n); _ilTrade = n; return r; }
        public static IEnumerable<CodeInstruction> PerkTradeIl(IEnumerable<CodeInstruction> instructions) { int n; var r = SwapCalls(instructions, true, out n); _ilPerkTrade = n; return r; }
        public static IEnumerable<CodeInstruction> PerkRepairIl(IEnumerable<CodeInstruction> instructions) { int n; var r = SwapCalls(instructions, true, out n); _ilPerkRepair = n; return r; }

        private static List<CodeInstruction> SwapCalls(IEnumerable<CodeInstruction> instructions, bool perk, out int n)
        {
            var list = new List<CodeInstruction>(instructions);
            n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var ci = list[i];
                if (ci.opcode != OpCodes.Call) continue;
                var mi = ci.operand as MethodInfo;
                if (mi == null || mi.DeclaringType != typeof(GiveGoldAction)) continue;
                string rep = null;
                if (mi.Name == "ApplyBetweenCharacters") rep = perk ? nameof(P_Between) : nameof(T_Between);
                else if (!perk && mi.Name == "ApplyForPartyToCharacter") rep = nameof(T_PartyToChar);
                else if (!perk && mi.Name == "ApplyForCharacterToParty") rep = nameof(T_CharToParty);
                if (rep == null) continue;
                var rm = AccessTools.Method(typeof(ShipsToPort181), rep);
                if (rm == null || !SameParams(mi, rm)) continue;
                ci.operand = rm; n++;
            }
            return list;
        }

        private static bool SameParams(MethodInfo a, MethodInfo b)
        {
            if (a.ReturnType != b.ReturnType) return false;
            var pa = a.GetParameters(); var pb = b.GetParameters();
            if (pa.Length != pb.Length) return false;
            for (int i = 0; i < pa.Length; i++) if (pa[i].ParameterType != pb[i].ParameterType) return false;
            return true;
        }

        /// <summary>Z SubModuleMain (przed oknami ksiegi obiegu): latki zawsze, wylaczniki czytane na zywo - wylaczone = sam pomiar.</summary>
        internal static void ApplyAll(Harmony h)
        {
            _miss.Clear(); _targets.Clear();
            const string navDist = "NavalDLC.CampaignBehaviors.NavalShipDistributionCampaignBehavior", navTrade = "NavalDLC.CampaignBehaviors.ShipTradeCampaignBehavior",
                         navUi = "NavalDLC.ViewModelCollection.Port.PortScreenHandlers.PortScreenTradeModeHandler";
            Type tDist = null, tTrade = null, tUi = null;
            try { tDist = AccessTools.TypeByName(navDist); tTrade = AccessTools.TypeByName(navTrade); tUi = AccessTools.TypeByName(navUi); } catch { }
            // E3/10
            _wRecover = Patch(h, "zwrot za statki (E3/10)", tDist, "RecoverGoldFromRemainingShipsAfterDistribution", new[] { typeof(MobileParty) }, nameof(RecoverPrefix), null, null, null);
            _wDisband = Patch(h, "rozwiazanie druzyny (E3/10)", tDist, "OnPartyDisbanded", new[] { typeof(MobileParty), typeof(Settlement) }, nameof(DisbandPre), null, nameof(DisbandFin), null);
            _wDist = Patch(h, "rozdzial w rodzie (E3/10, pomiar)", tDist, "DistributeShips", new[] { typeof(MobileParty) }, nameof(DistPre), nameof(DistPost), null, null);
            // E3/11
            _wTrade = Patch(h, "handel (E3/11)", typeof(ChangeShipOwnerAction), "ApplyInternal", null, nameof(TradePrefix), null, nameof(TradeFin), nameof(TradeIl));
            _wPerkTrade = Patch(h, "premia zarzadcy - handel (E3/11)", tTrade, "OnShipOwnerChanged", null, nameof(PerkTradePre), null, nameof(PerkFin), nameof(PerkTradeIl));
            _wPerkRepair = Patch(h, "premia zarzadcy - naprawa (E3/11)", tTrade, "OnShipRepaired", null, nameof(PerkRepairPre), null, nameof(PerkFin), nameof(PerkRepairIl));
            _wSwap = Patch(h, "wymiana przy zakupie (E3/11)", tTrade, "TryPurchasingShipFromTown", null, nameof(SwapPre), null, nameof(SwapFin), null);
            try
            {
                if (tUi != null) { _uiLeft = AccessTools.Field(tUi, "_leftOwner"); _uiRight = AccessTools.Field(tUi, "_rightOwner"); }
                if (_uiLeft != null && _uiRight != null)
                    _wUi = Patch(h, "wycena ekranu portu (E3/11, gracz)", tUi, "GetTradeCostOfShip", new[] { typeof(Ship), typeof(bool) }, null, nameof(UiPricePost), null, null);
                else _miss.Add("wycena ekranu portu (E3/11, gracz) (brak typu albo pol _leftOwner/_rightOwner) - zostaje komunikat po sprzedazy");
            }
            catch (Exception e) { _miss.Add("wycena ekranu portu (" + e.Message + ")"); }
            // E3/12
            _wPrize = Patch(h, "pryzy - lup statkow (E3/12)", typeof(MapEvent), "LootDefeatedPartyShips", null, nameof(PrizePrefix), null, nameof(PrizeFin), null);
            _wDestroy = Patch(h, "pryzy - zatopienie (E3/12)", typeof(DestroyShipAction), "Apply", new[] { typeof(Ship) }, nameof(DestroyPrefix), null, null, null);
            var s = Settings.Current;
            Log.Info("Statki (181): latki - zwrot za statki " + (_wRecover ? "tak" : "NIE") + ", rozwiazanie " + (_wDisband ? "tak" : "NIE") + ", rozdzial w rodzie " + (_wDist ? "tak" : "NIE")
                     + ", handel " + (_wTrade ? "tak (podmienione zaplaty " + _ilTrade + "/8)" : "NIE") + ", premie zarzadcy: handel " + (_wPerkTrade ? _ilPerkTrade + "/1" : "NIE")
                     + ", naprawa " + (_wPerkRepair ? _ilPerkRepair + "/1" : "NIE") + ", wymiana przy zakupie " + (_wSwap ? "tak" : "NIE") + ", wycena ekranu portu " + (_wUi ? "tak" : "NIE")
                     + ", pryzy " + (_wPrize && _wDestroy ? "tak" : "NIE") + (_miss.Count > 0 ? "; BRAK: " + string.Join("; ", _miss.ToArray()) : "")
                     + ". Wylaczniki: Ships To Port " + (s != null && s.ShipsToPort ? "WLACZONY" : "wylaczony") + ", Ship Trade Through Port " + (s != null && s.ShipTradeThroughPort ? "WLACZONY" : "wylaczony")
                     + ", Ship Prizes To Port " + (s != null && s.ShipPrizesToPort ? "WLACZONY" : "wylaczony") + " (E5/B5: domyslnie wlaczone; wylaczone = gra jak dotad, sam pomiar;"
                     + " stan z chwili wpiecia - wartosci czynne w linii dziennej).");
        }

        /// <summary>Raz na sesje (pierwsza doba): stocznia NavalDLC i cudze latki na naszych metodach (inne mody - kolizje).</summary>
        private static string HooksLine()
        {
            var others = new List<string>();
            try
            {
                foreach (var m in _targets)
                {
                    var info = Harmony.GetPatchInfo(m);
                    if (info == null) continue;
                    foreach (var o in info.Owners)
                        if (o != null && !o.StartsWith("com.jeff.armoury", StringComparison.Ordinal)) others.Add(m.DeclaringType.Name + "." + m.Name + " <- " + o);
                }
            }
            catch (Exception e) { others.Add("blad odczytu (" + e.Message + ")"); }
            try { if (Town.AllTowns != null && Town.AllTowns.Count > 0) ShipyardLevel(Town.AllTowns[0]); } catch { }   // pierwsze wywolanie wiaze GetShipyard
            return "Statki (181) - start sesji: stocznia NavalDLC (GetShipyard) " + (_getShipyard != null ? "tak" : "NIE - poziom z limitu statkow")
                   + "; podmienione zaplaty: handel " + _ilTrade + "/8, premie " + _ilPerkTrade + "/1 + " + _ilPerkRepair + "/1; cudze latki na naszych metodach: "
                   + (others.Count > 0 ? string.Join(", ", others.ToArray()) : "brak") + ".";
        }

        /// <summary>
        /// E5/B5 (projekt etapu 5 rozdz. 3 "E5/B5" Test: kasy portow ponizej zapasu nie dluzej niz 28 dob): miasta z portem i stocznia (poziom > 0 - te,
        /// ktore Gather pyta o statki), nadwyzka ta sama co przy wyplacie (Spare -> Seal31.Spare; przy czynnym A1 D4 TownPurse). Serie dob bez nadwyzki -
        /// pamiec sesji (po wczytaniu od 0, jak zaulki A1). Tylko odczyt; wyjatek przy jednym miescie - potkniecie (mechanizmu nie gasimy).
        /// </summary>
        private static void PortPurses(StringBuilder sb)
        {
            int ports = 0, dry = 0, max = 0, w7 = 0, w28 = 0; long spareAll = 0; string maxName = null;
            var all = Town.AllTowns;
            if (all != null)
                for (int i = 0; i < all.Count; i++)
                {
                    var t = all[i];
                    try
                    {
                        if (t == null || t.Settlement == null) continue;
                        var st = t.Settlement;
                        if (!t.IsTown || !st.HasPort || ShipyardLevel(t) <= 0) { _dryStreak.Remove(st); continue; }
                        ports++;
                        long sp = Spare(st);
                        int k; _dryStreak.TryGetValue(st, out k);
                        if (sp <= 0) { k++; _dryStreak[st] = k; dry++; }
                        else { k = 0; _dryStreak.Remove(st); spareAll += sp; }
                        if (k >= 7) w7++;
                        if (k >= 28) w28++;
                        if (k > max) { max = k; maxName = st.Name != null ? st.Name.ToString() : st.StringId; }
                    }
                    catch (Exception e) { Stumble("PortPurses", e); }
                }
            sb.Append(" | kasy portow (miasta z portem i stocznia) ").Append(ports).Append(", nadwyzka ")
              .Append(TownPurse.On ? "z D4 TownPurse (A1 czynne)" : "ze starej formuly (A1 nieczynne - kasa - 10 000 - 12 x dobrobyt - tarcze)")
              .Append(": bez nadwyzki dzis ").Append(dry).Append(" (nadwyzka portow razem ").Append(spareAll).Append("), najdluzej bez nadwyzki ").Append(max)
              .Append(" dob z rzedu (").Append(maxName ?? "-").Append("), >= 7 dob ").Append(w7).Append(", >= 28 dob ").Append(w28)
              .Append(" (prog B5: najdluzej <= 28; pamiec sesji)");
        }

        // ============================================================ raz na dobe
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                if (!_hooksLogged) { _hooksLogged = true; Log.Info(HooksLine()); }
                int day = (int)CampaignTime.Now.ToDays - 1;
                bool l = LostOn, t = TradeOn, p = PrizeOn;
                var s = Settings.Current;
                long spentAll = 0; foreach (var v in _spent.Values) spentAll += v;
                var sb = new StringBuilder(2048);
                sb.Append("Statki (181): dzien ").Append(day)
                  .Append(" | wylaczniki: do portu (Ships To Port) ").Append(l ? "WL" : "WYL").Append(", handel przez kase portu (Ship Trade Through Port) ").Append(t ? "WL" : "WYL")
                  .Append(", pryzy do portu (Ship Prizes To Port) ").Append(p ? "WL" : "WYL").Append(", porty pytane o cala cene ").Append(s != null ? s.ShipSalePortsTried : 3);
                // 1.
                sb.Append(" | 1. statki rozbitej albo rozwiazanej druzyny (E3/10): druzyn ze statkami ").Append(_p1N).Append(" (rozwiazane ").Append(_p1DisbN).Append(", rozbite na ladzie ").Append(_p1LostN)
                  .Append("), statkow ").Append(_p1Ships).Append(", rozdane w rodzie ").Append(_p1Given).Append("; zloto gry (dzis z niczego").Append(l ? " - przy WL zastapione wyplata portow" : "").Append(") ")
                  .Append(_p1Game).Append(" - wedlug rodzaju rodu:");
                for (int k = 0; k < Kinds; k++) sb.Append(k == 0 ? " " : ", ").Append(KName[k]).Append(' ').Append(_p1KindGame[k]).Append(" (").Append(_p1KindN[k]).Append(')');
                sb.Append("; drugie wywolanie tej samej druzyny (W6) ").Append(_p1SecondN).Append(", zloto gry drugi raz ").Append(_p1SecondGold).Append(l ? " (pominiete " + _p1SecondSkipped + ")" : "");
                if (_r1Sim.N > 0)
                {
                    sb.Append("; regula 181 - symulacja (bez zmian w grze): ").Append(_r1Sim.Text()).Append("; symulowane wyplaty wedlug rodzaju rodu:");
                    for (int k = 0; k < Kinds; k++) sb.Append(k == 0 ? " " : ", ").Append(KName[k]).Append(' ').Append(_p1KindSim[k]);
                }
                if (_r1Real.N > 0)
                {
                    sb.Append("; regula 181 - naprawde: ").Append(_r1Real.Text()).Append("; zaplacone wedlug rodzaju rodu:");
                    for (int k = 0; k < Kinds; k++) sb.Append(k == 0 ? " " : ", ").Append(KName[k]).Append(' ').Append(_p1KindPaid[k]);
                }
                // 2.
                sb.Append(" | 2. sprzedaz statkow portom (E3/11, dzis z niczego").Append(t ? " - przy WL z kasy portu" : "").Append("):");
                for (int w = 0; w < Whos; w++) sb.Append(w == 0 ? " " : ", ").Append(WhoName[w]).Append(' ').Append(_sellN[w]).Append(" / ").Append(_sellGold[w]);
                if (_sellViaPort > 0 || _sellCut > 0) sb.Append("; z kas portow ").Append(_sellViaPort).Append(", gracz dostal mniej o ").Append(_sellCut);
                sb.Append("; bramka AI (port bez calej ceny w nadwyzce): ").Append(t ? "odrzucone " + _gateN + " / " + _gateGold : "symulacja - odrzuconoby " + _gateSimN + " / " + _gateSimGold);
                if (_gateN > 0 && _gateSimN > 0) sb.Append(" (symulacja przed wlaczeniem ").Append(_gateSimN).Append(" / ").Append(_gateSimGold).Append(')');
                sb.Append(", wymiana przy zakupie pominieta ").Append(_swapSkipN).Append("; gracz: port bez pieniedzy ").Append(_playerNoCoinN)
                  .Append(", symulacja - port zaplacilby mniej ").Append(_playerShortSimN).Append(" razy o ").Append(_playerShortSim)
                  .Append(", wycena ekranu obcieta ").Append(_uiCapN).Append("; cena <= 0 (sprzedajacy doplaca) ").Append(_negN).Append(" / ").Append(_negGold);
                // 3.
                sb.Append(" | 3. kupno statkow od portow (E3/11, dzis w nicosc").Append(t ? " - przy WL do kasy portu" : "").Append("):");
                for (int w = 0; w < Whos; w++) sb.Append(w == 0 ? " " : ", ").Append(WhoName[w]).Append(' ').Append(_buyN[w]).Append(" / ").Append(_buyGold[w]);
                if (_buyViaPort > 0) sb.Append("; do kas portow ").Append(_buyViaPort);
                sb.Append("; gracz bez calej ceny w kiesie - kupno przed sprzedaza w jednym potwierdzeniu, pominiete (E5/B5) ").Append(_playerShortBuyN).Append(" / ").Append(_playerShortBuyGold);
                sb.Append(" | premie zarzadcow portu (E3/11, dzis z niczego").Append(t ? " - przy WL z kasy portu" : "").Append("): za handel ").Append(_perkN[PkTrade]).Append(" / ").Append(_perkGold[PkTrade])
                  .Append(", za naprawe ").Append(_perkN[PkRepair]).Append(" / ").Append(_perkGold[PkRepair]);
                if (_perkViaPort > 0 || _perkCut > 0) sb.Append("; z kas portow ").Append(_perkViaPort).Append(", niedoplacone (brak nadwyzki) ").Append(_perkCut);
                // 4.
                sb.Append(" | 4. pryzy z bitew morskich (E3/12): bitwy ").Append(_p4Battles).Append(", statki zatopione ").Append(_p4Sunk).Append(" (gra placi zwyciezcom za ").Append(_p4PaidByGameN)
                  .Append(", bez placacego zwyciezcy ").Append(_p4NoPayN).Append("), wraki po uszkodzeniu po bitwie (gra nie placi, 181 nie rusza - E5/B5) ").Append(_p4Wrecks)
                  .Append("; zloto gry (dzis z niczego) ").Append(_p4Game).Append(" (w tym rod gracza ").Append(_p4GamePlayer).Append(')');
                if (_p4Removed > 0) sb.Append(", zdjete z lupu zwyciezcow ").Append(_p4Removed);
                if (_r4Sim.N > 0) sb.Append("; regula 181 - symulacja (bez zmian w grze): ").Append(_r4Sim.Text());
                if (_r4Real.N > 0)
                    sb.Append("; regula 181 - naprawde: ").Append(_r4Real.Text()).Append("; do wodzow ").Append(_p4ToLeaders).Append(", do kies partii ").Append(_p4ToParties)
                      .Append(", bez odbiorcy (zostalo w porcie) ").Append(_p4NoRecipient).Append(", podstawa 1/9 korony ").Append(_p4Ninth);
                // razem
                sb.Append(" | razem naprawde dzis: z niczego ").Append(_fromNothing).Append(", w nicosc ").Append(_toNothing).Append(", netto ").Append(_fromNothing - _toNothing)
                  .Append("; kasy portow wydaly (181) ").Append(spentAll).Append(" w ").Append(_spent.Count).Append(" portach, przyjely ").Append(_buyViaPort)
                  .Append("; wyplaty ponad nadwyzke ").Append(_overSpare).Append(" (prog 0), statki przekazane za 0 zl ").Append(_freeShips).Append(" (prog 0)");
                // E5/B5 (recenzja 1): zakupy portow, przy ktorych port mial juz liczbe idealna stoczni (limit - 2) - nadmiar kasowany przez stocznie NavalDLC
                sb.Append(" | zakupy portow ponad liczbe idealna stoczni (limit - 2; nadmiar kasuje NavalDLC TryRemoveExcessShipsFromTown nastepnej doby - obce 70%, swoje 30%;")
                  .Append(" produkcja i tak dorabia do limitu za darmo): naprawde ").Append(_ovN[0] + _ovN[1] + _ovN[2]).Append(" / ").Append(_ovGold[0] + _ovGold[1] + _ovGold[2])
                  .Append(" - rozbite druzyny ").Append(_ovN[OvLost]).Append(" / ").Append(_ovGold[OvLost]).Append(", sprzedaz portom ").Append(_ovN[OvTrade]).Append(" / ").Append(_ovGold[OvTrade])
                  .Append(", pryzy ").Append(_ovN[OvPrize]).Append(" / ").Append(_ovGold[OvPrize]);
                if (_ovSimN[0] + _ovSimN[1] + _ovSimN[2] > 0)
                    sb.Append("; symulacja WYL ").Append(_ovSimN[0] + _ovSimN[1] + _ovSimN[2]).Append(" / ").Append(_ovSimGold[0] + _ovSimGold[1] + _ovSimGold[2])
                      .Append(" - rozbite druzyny ").Append(_ovSimN[OvLost]).Append(" / ").Append(_ovSimGold[OvLost]).Append(", sprzedaz portom ").Append(_ovSimN[OvTrade]).Append(" / ").Append(_ovSimGold[OvTrade])
                      .Append(", pryzy ").Append(_ovSimN[OvPrize]).Append(" / ").Append(_ovSimGold[OvPrize]);
                // E5/B5: kasy portow (miasta z portem i stocznia - te, ktore Gather pyta) - bez nadwyzki dzis i serie dob z rzedu (prog projektu: <= 28 dob)
                // A17 (poz. 6): port kupuje do liczby idealnej stoczni - ile razy port pelny zostal pominiety albo odmowil
                sb.Append(" | kupno do liczby idealnej stoczni (A17, Ship Ports Buy To Ideal): ").Append(IdealOn ? "TAK" : "NIE")
                  .Append(" - porty pelne pominiete przy wyborze portu ").Append(_idealSkip).Append(", odmowy kupna od AI ").Append(_idealGateN).Append(" (").Append(_idealGateGold).Append(" zl)")
                  .Append(", od gracza ").Append(_idealPlayerN);
                PortPurses(sb);
                if (_stumbles > 0) sb.Append(" | wyjatki zlapane ").Append(_stumbles).Append(" (pierwszy z kazdego miejsca w logu jako ERROR)");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("ShipsToPort181.Daily", e); }
            finally { ClearDay(); }
        }
    }
}
