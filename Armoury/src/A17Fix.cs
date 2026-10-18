using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// STARE ZALEGLOSCI A17 (noc 17/18.10, paczka stare-a17; CO-ZOSTALO A17, CHANGELOG 14.10 "A17 - podsumowanie", linie diagnozy A17-D1..D4).
    /// Piec poprawek, kazda z wlasnym kluczem (wylaczony = kod jak przed paczka), sprawdzone w dekompilacji 1.4.8 / BK Redux / NavalDLC:
    ///
    /// F1 KONIE PARTII BK Z NICZEGO (klucz A17BkPartyNoFreeMounts). BK BannerKingsComponent.GiveMounts (karawany niewolnikow i zywnosci PopulationPartyComponent,
    ///    patrole GarrisonPartyComponent, milicja, swity majatkow, wolne kompanie) dopisuje "liczba pieszych - liczba koni" sumpter_horse z niczego, a
    ///    PopulationPartyComponent.GivePackAnimals (wedrowcy) 1/4 skladu jucznych; u celu BKPartyBehavior.AddPopulationPartyBehavior / AddPatrolBehavior wola
    ///    DestroyPartyAction(null) i konie znikaja z partia przy RemoveParty. Ksiega koni (PackSources, m6b) nazywala obie pozycje: fin80 srednio 705 z niczego
    ///    i 702 przepadlo na dobe (juczne) - zrodlo i ujscie bez placacego i odbiorcy. Regula (jak 36b dla wedrowcow - "zwierzat jucznych nie ma wcale"): dar BK
    ///    pominiety (prefiks false); partia BK, ktora gra kasuje bez niszczyciela (dojazd do celu, rozwiazanie), oddaje konie, ktore ma (lup, zakupy), na targ osady,
    ///    w ktorej stoi (albo domu) - wolane z GoodsLedger.OnPartyDestroyed po HorseFate.Spoils (ten pomija partie BK bez niszczyciela). Partie BK "Trading" (BK sam
    ///    kopiuje ich ladunek do miasta) - bez zmian.
    /// F2 RYBY BEZ ZRODLA (klucz A17FishFromHands; brama KS "<= 2 szt./d ze zrodel bez nazwy"; nzc1 ZC1-6 fin80: fish 205/d w "inne ticki dobowe").
    ///    Autorzy (K4 + dekompilacja): (a) NavalDLC NavalFishingCampaignBehaviour.OnDailyTickSettlement - wies, ktorej miasto targowe ma namiestnika z atutem
    ///    Shipmaster.NightRaider, dostaje ryby z niczego do magazynu (ok. 220/d); (b) BK BKBuildingsBehavior.RunPorts - miasto z Harbor/Port dostaje
    ///    poziom x dzierzawcy/300 (albo rzemieslnicy/200) ryb na polke, a kasa placi ich cene w nicosc (ok. 20/d); (c) NavalFishingCampaignBehaviour.OnHourlyTickParty -
    ///    partia na morzu z atutem MasterAngler lowi rybe co godzine (zaloga - prawdziwe rece, ale tick godzinowy byl poza ramkami ksiegi = "bez wyjasnienia").
    ///    Regula: tam, gdzie KS jest zastosowany (StartCalibration.AppliedFor(wies) / Applied dla miast), (a) i (b) pominiete - ryby daja tylko rece wsi rybackich
    ///    (Plan KS) i lodzie rybackie; poza KS (a) i (b) jak dotad, liczone w linii jako nazwane zrodla z niczego; (c) zawsze w ramce polowu ksiegi towarow
    ///    (FFish - zrodlo nazwane: polow zalogi).
    ///    Recenzja stare-a17 (3): poza KS (a) i (b) w ramce polowu z etykieta - ksiega towarow nazywa je grupa "ryby z niczego poza KS (A17 F2)" zamiast "inne ticki
    ///    dobowe"; zloto, ktore BK zdejmuje z kasy za ryby portu, idzie do kiesy ludu (place rybakow, TownFolk.Earn KMine, reszta do kasy; klucz A17PortFishPaysFolk).
    /// F3 UNIKATY ZUZYTE NA AWANS BAND (klucz A17UniquesNotOutlawKit; poz. 4 renly_armor). Linia D3 (sesje 16-17.10): unikaty schodza z polek przez
    ///    OutlawLaw.Equip (paser bandy kupuje zbroje albo konia na awans - sztuka znika w zolnierzu bandy, poza swiatem: lyanna_armor, mace_armor,
    ///    blackfyre_plate, 13 razy), UniqueSpoils (zakup bohatera - zamierzone), SupplyDemand (hurt wozem do innego miasta - zostaje w swiecie), LegendaryLaw,
    ///    CitySack (zamierzone). Regula: paser i juki bandy nie daja unikatu na awans (unikat zostaje na polce albo w jukach - lup).
    /// F4 RANNI POBITEGO OBLEGAJACEGO - DROGA C (klucz A17SiegeCommitRescue). Linie D4: 61 przypadkow, WSZYSTKIE wypad (SallyOut, oblegajacy po stronie
    ///    obroncow) albo bitwa pod murami (SiegeOutside) z pobitym oblegajacym, H3 "objal", a w tej samej dobie postfiks H3 nie sprawdzil bitwy (fin80 d109097:
    ///    objetych 31, sprawdzonych 30; N3 "jency zgubieni przez gre 0") i CrashScribe liczy 2 FirstChance (odfiltrowane bez moda) - tylko w dobach z droga C
    ///    (109096, 109173). Wniosek: MapEvent.CalculateAndCommitMapEventResults rzuca wyjatek (H3 postfiks nie biegnie, CaptureDefeatedPartyMembers nie konczy
    ///    pojmania, CommitCalculatedMapEventResults - XP, slawa, wplyw, morale, zloto zwyciezcow - nie biegnie), a BKROTPatch (finalizer MapEvent.Update) polyka
    ///    NullReferenceException. Regula: finalizer na CalculateAndCommitMapEventResults - wyjatek do logu (typ, komunikat, slad; 5 na sesje), przy NRE w bitwie AI:
    ///    CommitCalculatedMapEventResults dokonczone (osobny try), _mapEventResultsApplied = true, wyjatek zlapany tu zamiast w MapEvent.Update; rannych bierze
    ///    dalej DoomedPartyMen (droga C), tabor HorseFate.Spoils, jencow DoomedPartyMen, statki 181 Gather. Znaczniki pojmania gry (wejscie / koniec) w linii D4.
    ///    Recenzja stare-a17 (1): Commit dokanczany TYLKO, gdy wyjatek padl przed nim (znacznik CommitInPre/CommitInFin) - Commit jest ostatni, a slawa, wplyw
    ///    i morale nie sa w nim zerowane; wyjatek z wnetrza Commit przepuszczony jak dotad (licznik "z wnetrza Commit").
    /// F5 STATKI STOCZNI Z NICZEGO (klucz A17ShipyardBuildsFromTimber). NavalDLC ShipProductionCampaignBehavior.DailyTickTown: przy liczbie statkow portu < limitu
    ///    i losie 0.5 do 10 x CreateShip (statek z niczego, z losowymi czesciami), potem TryRemoveExcessShipsFromTown kasuje nadmiar ponad liczbe idealna (limit - 2).
    ///    Regula (jedna z portami - ShipPortsBuyToIdeal): stocznia buduje tylko ponizej liczby idealnej i tylko z drewna z polki miasta (ShipyardTimberPer1000 pni
    ///    na 1000 wartosci kadluba; deska = 1/5 pnia) i za place cieslom z nadwyzki kasy ponad zapas kupcow (ShipyardLabourShare x wartosc kadluba -> kiesa ludu
    ///    W60/R40, TownFolk.EarnFromTown KBuildLabour; bez kiesy ludu - placa zostaje w kasie); czego brak - statku nie ma. Kadlub losuje gra (GetRandomShipHull) -
    ///    ten sam kadlub, za ktory zaplacono, wraca z postfiksu GetRandomShipHull w CreateShip. Drewno w ramce budow ksiegi towarow + OreLedger.NoteBuild.
    ///    Recenzja stare-a17 (4), (5): czesci, ktore CreateShip zaklada za darmo, rozliczane w finalizerze ta sama regula (pelna wartosc czesci; czego brak -
    ///    czesc zdjeta); potkniecie prefiksu (gra buduje statek z niczego) liczone w "statki z niczego".
    ///
    /// Linie doby: "Konie partii BK (A17 F1)", "Ryby z niczego (A17 F2)", "Unikaty u band (A17 F3)", "Wynik bitwy (A17 F4)", "Stocznie (A17 F5)" - liczby dla
    /// tools/sprawdz_logi.py --grupa na17. Bez pol zapisu. Potkniecia per obiekt (licznik), bez globalnych wylacznikow.
    /// </summary>
    internal static class A17Fix
    {
        private static Settings S { get { return Settings.Current; } }
        internal static bool BkOn { get { var s = S; return s != null && s.A17BkPartyNoFreeMounts; } }
        internal static bool FishOn { get { var s = S; return s != null && s.A17FishFromHands; } }
        internal static bool UniqOn { get { var s = S; return s != null && s.A17UniquesNotOutlawKit; } }
        internal static bool SiegeOn { get { var s = S; return s != null && s.A17SiegeCommitRescue; } }
        internal static bool ShipOn { get { var s = S; return s != null && s.A17ShipyardBuildsFromTimber; } }
        internal static bool PortFolkOn { get { var s = S; return s != null && s.A17PortFishPaysFolk; } }   // recenzja stare-a17 (3)
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly List<string> _hooks = new List<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_errSites.Add(where)) Log.Error("A17Fix." + where, e);
        }

        // ------------------------------------------------------------ liczniki doby
        // F1
        private static long _bkSkipped, _bkSkippedPack, _bkHorsesHome, _bkPartiesHome, _bkNoWhere, _bkTradingKept;
        // F2
        private static long _fNrBlockedV, _fNrBlockedN, _fNrPassV, _fNrPassN, _fPortBlocked, _fPortPass, _fPortPassFish, _fPortPassGold, _fCrew, _fCrewParties, _fPortPaid, _fPortFolk, _fPortBack;
        // F3
        private static long _uSkip, _uSkipAll;
        // F4
        private static long _cFail, _cFailSiege, _cFailOther, _cRescued, _cCommitFail, _cPassed, _cCapIn, _cCapDone, _cInCommit;
        private static bool _commitMarked;   // recenzja stare-a17 (1): znacznik wejscia w Commit wpiety
        private static readonly Dictionary<string, int> _cTypes = new Dictionary<string, int>();
        private static int _cTraces;
        // F5
        private static long _sWant, _sBuilt, _sFull, _sNoWood, _sNoGold, _sWood, _sPlanks, _sWage, _sWageKept, _sNoHull, _sValue;
        private static long _sFree, _sPieces, _sPieceValue, _sPieceNoWood, _sPieceNoGold, _sPieceFree, _sPieceLost;   // recenzja stare-a17 (4), (5)

        internal static void Reset()
        {
            DayReset();
            _stumbles = 0; _errSites.Clear(); _uSkipAll = 0; _cTraces = 0;
            _note.Clear(); _inCommit.Clear(); _forcedHull = null; _pieceTown = null; _isBk.Clear();
        }

        private static void DayReset()
        {
            _stumblesDay = 0;
            _bkSkipped = _bkSkippedPack = _bkHorsesHome = _bkPartiesHome = _bkNoWhere = _bkTradingKept = 0;
            _fNrBlockedV = _fNrBlockedN = _fNrPassV = _fNrPassN = _fPortBlocked = _fPortPass = _fPortPassFish = _fPortPassGold = _fCrew = _fCrewParties = _fPortPaid = _fPortFolk = _fPortBack = 0;
            _uSkip = 0;
            _cFail = _cFailSiege = _cFailOther = _cRescued = _cCommitFail = _cPassed = _cCapIn = _cCapDone = _cInCommit = 0; _cTypes.Clear();
            _sWant = _sBuilt = _sFull = _sNoWood = _sNoGold = _sWood = _sPlanks = _sWage = _sWageKept = _sNoHull = _sValue = 0;
            _sFree = _sPieces = _sPieceValue = _sPieceNoWood = _sPieceNoGold = _sPieceFree = _sPieceLost = 0;
        }

        // ================================================================ F1 konie partii BK
        private static readonly Dictionary<Type, bool> _isBk = new Dictionary<Type, bool>();
        private static PropertyInfo _pTrading;

        private static bool IsBk(MobileParty mp)
        {
            var c = mp != null ? mp.PartyComponent : null;
            if (c == null) return false;
            var t = c.GetType();
            bool r;
            if (_isBk.TryGetValue(t, out r)) return r;
            r = false;
            for (var b = t; b != null; b = b.BaseType)
                if (b.FullName == "BannerKings.Components.BannerKingsComponent") { r = true; break; }
            _isBk[t] = r;
            return r;
        }

        /// <summary>Prefiks BannerKingsComponent.GiveMounts(ref MobileParty): dar BK z niczego pominiety (PackSources.BkPost widzi zmiane 0).</summary>
        public static bool MountsPre()
        {
            try { if (!BkOn) return true; _bkSkipped++; return false; }
            catch (Exception e) { Stumble("MountsPre", e); return true; }
        }

        /// <summary>Prefiks PopulationPartyComponent.GivePackAnimals(ref MobileParty): juczne z niczego pominiete (wedrowcy - jak 36b TravellersRealGoods).</summary>
        public static bool PackPre()
        {
            try { if (!BkOn) return true; _bkSkippedPack++; return false; }
            catch (Exception e) { Stumble("PackPre", e); return true; }
        }

        /// <summary>GoodsLedger.OnPartyDestroyed (po HorseFate.Spoils, przed licznikami "przepadlo"): partia BK kasowana bez niszczyciela - konie z jej taboru
        /// na targ osady, w ktorej stoi (albo domu). Przeniesienie (oba rostery w stanie ksiegi koni), bez zlota - zwierzeta ludzi tej partii. Nie rzuca.</summary>
        internal static void BkArrival(MobileParty mp, PartyBase destroyer)
        {
            if (destroyer != null || mp == null || !BkOn || !Live) return;
            try
            {
                if (!IsBk(mp) || mp.ItemRoster == null) return;
                var c = mp.PartyComponent;
                if (c != null && c.GetType().Name == "PopulationPartyComponent")
                {
                    if (_pTrading == null) _pTrading = AccessTools.Property(c.GetType(), "Trading");
                    object tv = _pTrading != null ? _pTrading.GetValue(c, null) : null;
                    if (tv is bool && (bool)tv) { _bkTradingKept++; return; }   // BK sam kopiuje ladunek handlarzy do miasta (AddPopulationPartyBehavior) - bez drugiej kopii
                }
                var r = mp.ItemRoster;
                List<KeyValuePair<EquipmentElement, int>> moves = null;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || el.EquipmentElement.Item == null || HorseBook.Cat(el.EquipmentElement.Item) < 0) continue;
                    if (moves == null) moves = new List<KeyValuePair<EquipmentElement, int>>();
                    moves.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, el.Amount));
                }
                if (moves == null) return;
                Settlement at = mp.CurrentSettlement;
                if (at == null || at.IsHideout) at = mp.HomeSettlement;
                var dst = at != null && !at.IsHideout ? at.ItemRoster : null;
                int n = 0;
                foreach (var m in moves) n += m.Value;
                if (dst == null) { _bkNoWhere += n; return; }
                foreach (var m in moves) { r.AddToCounts(m.Key, -m.Value); dst.AddToCounts(m.Key, m.Value); }
                _bkHorsesHome += n; _bkPartiesHome++;
            }
            catch (Exception e) { Stumble("BkArrival", e); }
        }

        // ================================================================ F2 ryby
        private static PropertyInfo _pNightRaider;
        private static bool _nrTried;
        private static ItemObject _fish;

        private static ItemObject Fish
        {
            get
            {
                if (_fish == null) { try { _fish = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemObject>("fish") : null; } catch { _fish = null; } }
                return _fish;
            }
        }

        /// <summary>Ile ryb NavalDLC dopisze wsi z atutu Night Raider namiestnika miasta targowego (ta sama formula co NavalFishingCampaignBehaviour); -1 = nie wiadomo.</summary>
        private static int NightRaiderFish(Village v)
        {
            try
            {
                if (v == null || v.TradeBound == null || v.TradeBound.Town == null) return 0;
                if (!_nrTried)
                {
                    _nrTried = true;
                    var t = AccessTools.TypeByName("NavalDLC.CharacterDevelopment.NavalPerks+Shipmaster");
                    _pNightRaider = t != null ? AccessTools.Property(t, "NightRaider") : null;
                }
                var perk = _pNightRaider != null ? _pNightRaider.GetValue(null, null) as TaleWorlds.CampaignSystem.CharacterDevelopment.PerkObject : null;
                if (perk == null) return -1;
                var val = new ExplainedNumber(0f, false, null);
                Helpers.PerkHelper.AddPerkBonusForTown(perk, v.TradeBound.Town, ref val);
                return Math.Max(0, val.RoundedResultNumber);
            }
            catch (Exception e) { Stumble("NightRaiderFish", e); return -1; }
        }

        // recenzja stare-a17 (3): etykiety ramki polowu ksiegi towarow (GoodsLedger FFish: "grupa|podpozycja" albo sama podpozycja w grupie lodzi rybackich).
        // Poza KS ryby z atutu Night Raider i z portow BK sa dalej z niczego (Plan KS ich nie liczy, poza KS stara ekonomia), ale ksiega nazywa je osobna grupa
        // zamiast "inne ticki dobowe"; polow zalogi - wlasna grupa (NIE "lodzie rybackie": StartCalibration.Plan bierze grupe lodzi GoodsLedger.KsFishBoats jako podaz
        // ryb spoza tabeli rozdzielana na krainy - ryba zalogi zostaje w jukach partii na morzu, nie idzie na targ).
        internal const string FishFreeGroup = "ryby z niczego poza KS (A17 F2)";
        private const string LabNightRaider = FishFreeGroup + "|atut Night Raider (NavalDLC)";
        private const string LabPorts = FishFreeGroup + "|porty BK (RunPorts)";
        private const string LabCrew = "polow zalogi na morzu (Master Angler)|";

        private static GoodsLedger.Frame FishFrame(object at, string label)
        {
            var fr = GoodsLedger.Begin(GoodsLedger.FFish, at);
            if (fr != null) fr.Info = label;
            return fr;
        }

        /// <summary>Prefiks NavalFishingCampaignBehaviour.OnDailyTickSettlement(Settlement): wies zastosowana w KS - ryby z atutu namiestnika (z niczego) pominiete.
        /// Poza KS - jak NavalDLC (liczone w linii jako nazwane zrodlo z niczego), w ramce polowu ksiegi towarow z etykieta (recenzja stare-a17 (3)).</summary>
        public static bool NavalVillagePre(Settlement __0, out GoodsLedger.Frame __state)
        {
            __state = null;
            try
            {
                if (!FishOn || __0 == null || !__0.IsVillage || __0.Village == null || __0.Village.TradeBound == null) return true;
                int n = NightRaiderFish(__0.Village);
                bool ks = StartCalibration.AppliedFor(__0.Village);
                if (ks)
                {
                    if (n != 0) { _fNrBlockedV++; if (n > 0) _fNrBlockedN += n; }
                    return false;   // metoda NavalDLC robi tylko ten dopisek ryb - pominieta cala
                }
                if (n > 0) { _fNrPassV++; _fNrPassN += n; }
                if (n != 0) __state = FishFrame(__0, LabNightRaider);   // n = -1: atutu nie odczytano - ramka i tak (zero zmian = zero wpisow)
                return true;
            }
            catch (Exception e) { Stumble("NavalVillagePre", e); return true; }
        }

        public static void NavalVillageFin(GoodsLedger.Frame __state)
        {
            if (__state == null) return;
            try { GoodsLedger.End(__state); } catch (Exception e) { Stumble("NavalVillageFin", e); }
        }

        internal struct PortState { public bool On; public Town T; public int Fish0; public int Gold0; public GoodsLedger.Frame F; }

        /// <summary>Prefiks BK BKBuildingsBehavior.RunPorts(Town): przy KS zastosowanym pominiety (ryby z niczego za kase w nicosc); poza KS - pomiar (ryby, zloto),
        /// ramka polowu ksiegi towarow z etykieta, a zloto zdjete przez BK w nicosc - do kiesy ludu (klucz A17PortFishPaysFolk; recenzja stare-a17 (3)).</summary>
        public static bool PortsPre(Town __0, out PortState __state)
        {
            __state = default(PortState);
            try
            {
                if (!FishOn || __0 == null) return true;
                // recenzja calosci: miasto krainy wroconej przez bramke krainy (ks-arm) - porty BK jak dzis (pomiar, ramka, zloto do kiesy ludu), jak A17 wsi (AppliedFor)
                if (StartCalibration.Applied && !StartCalibration.LandBackOf(__0.Settlement)) { _fPortBlocked++; return false; }
                var f = Fish;
                if (f != null && __0.Settlement != null && __0.Settlement.ItemRoster != null)
                {
                    __state.On = true; __state.T = __0; __state.Fish0 = __0.Settlement.ItemRoster.GetItemNumber(f); __state.Gold0 = __0.Gold;
                    __state.F = FishFrame(__0, LabPorts);
                }
                return true;
            }
            catch (Exception e) { Stumble("PortsPre", e); return true; }
        }

        public static void PortsFin(PortState __state)
        {
            if (!__state.On || __state.T == null) return;
            try
            {
                int df = __state.T.Settlement.ItemRoster.GetItemNumber(Fish) - __state.Fish0;
                int paid = Math.Max(0, __state.Gold0 - __state.T.Gold);
                if (df > 0)
                {
                    _fPortPass++; _fPortPassFish += df;
                    if (paid > 0 && PortFolkOn && Live)
                    {
                        // BK: town.ChangeGold(-cena x ryby) bez odbiorcy. Zloto zdjete z kasy laduje w kiesie ludu (place rybakow portu - dzierzawcy i rzemieslnicy
                        // z formuly BK; TownFolk.Earn KMine: wyrobnicy - sam zapis w kiesie), czesc nieprzyjeta wraca do kasy - ten sam wzor co place gornikow
                        // (BuildFunding.MineRevenuePostfix); okno O19 (CirculationWindows.PortsFin, biegnie PO tym finalizerze) liczy kiese razem z kasa (NotePortsFolk)
                        var town = __state.T;
                        int folk = Math.Max(0, Math.Min(paid, TownFolk.Earn(town.Settlement, paid, TownFolk.KMine)));
                        if (paid - folk > 0) town.ChangeGold(paid - folk);
                        try { CirculationWindows.NotePortsFolk(folk); } catch { }
                        _fPortPaid += paid; _fPortFolk += folk; _fPortBack += paid - folk;
                    }
                    else _fPortPassGold += paid;
                }
            }
            catch (Exception e) { Stumble("PortsFin", e); }
            finally { if (__state.F != null) { try { GoodsLedger.End(__state.F); } catch { } } }
        }

        internal sealed class CrewState { public GoodsLedger.Frame F; public ItemRoster R; public int Fish0; }

        /// <summary>Prefiks NavalFishingCampaignBehaviour.OnHourlyTickParty(MobileParty): partia na morzu - ramka polowu ksiegi towarow (FFish, zrodlo nazwane:
        /// polow zalogi). Kazda inna partia - nic (jedno porownanie).</summary>
        public static void CrewPre(MobileParty __0, out CrewState __state)
        {
            __state = null;
            try
            {
                if (__0 == null || !FishOn || !__0.IsCurrentlyAtSea || __0.ItemRoster == null) return;
                var f = Fish;
                __state = new CrewState { R = __0.ItemRoster, Fish0 = f != null ? __0.ItemRoster.GetItemNumber(f) : 0, F = FishFrame(__0, LabCrew) };
            }
            catch (Exception e) { __state = null; Stumble("CrewPre", e); }
        }

        public static void CrewFin(CrewState __state)
        {
            if (__state == null) return;
            try
            {
                var f = Fish;
                int d = f != null ? __state.R.GetItemNumber(f) - __state.Fish0 : 0;
                if (d > 0) { _fCrew += d; _fCrewParties++; }
            }
            catch (Exception e) { Stumble("CrewFin", e); }
            finally { try { GoodsLedger.End(__state.F); } catch { } }
        }

        // ================================================================ F3 unikaty u band
        /// <summary>OutlawLaw.Equip (warunek sztuki na awans - ostatni czlon): true = pomin (unikat przy wlaczonym kluczu). Licznik kandydatow pominietych.</summary>
        internal static bool OutlawSkip(ItemObject it)
        {
            try
            {
                if (it == null || !UniqOn || !UniqueSpoils.Is(it)) return false;
                _uSkip++; _uSkipAll++;
                return true;
            }
            catch (Exception e) { Stumble("OutlawSkip", e); return false; }
        }

        // ================================================================ F4 wynik bitwy przerwany wyjatkiem
        private static AccessTools.FieldRef<MapEvent, bool> _appliedRef;
        private static MethodInfo _commit;
        private static readonly Dictionary<MapEvent, string> _note = new Dictionary<MapEvent, string>(new MeEq());
        // recenzja stare-a17 (1): bitwy, w ktorych CommitCalculatedMapEventResults wszedl i nie skonczyl sie bez wyjatku. Commit jest OSTATNI
        // w CalculateAndCommitMapEventResults (MapEvent.cs:1427), a MapEventParty.CommitRenownChanges / CommitInfluenceChanges / CommitMoraleChanges
        // nie zeruja GainedRenown / GainedInfluence / GainedMorale (MapEventParty.cs:467-497) - drugi Commit po wyjatku z wnetrza Commit dalby slawe,
        // wplyw i morale drugi raz partiom sprzed wyjatku, a BattlePlunder.CommitPrefix skasowalby zalegle obciazenia pokonanych (zloto z niczego).
        // Wyjatek z wnetrza Commit - przepuszczony jak dotad (bez drugiego Commit), licznik osobno.
        private static readonly HashSet<MapEvent> _inCommit = new HashSet<MapEvent>(new MeEq());

        private sealed class MeEq : IEqualityComparer<MapEvent>
        {
            public bool Equals(MapEvent a, MapEvent b) { return ReferenceEquals(a, b); }
            public int GetHashCode(MapEvent o) { return RuntimeHelpers.GetHashCode(o); }
        }

        private static void Note(MapEvent me, string s)
        {
            if (me == null) return;
            if (_note.Count >= 256) _note.Clear();
            string old;
            _note[me] = _note.TryGetValue(me, out old) ? old + "; " + s : s;
        }

        /// <summary>DoomedPartyMen.Diag (linia D4): co zrobila gra z wynikiem tej bitwy (znaczniki F4).</summary>
        internal static string CommitNote(MapEvent me)
        {
            string s;
            return me != null && _note.TryGetValue(me, out s) ? s : "pojmanie gry bez znacznika (bez wyjatku albo poza pomiarem)";
        }

        /// <summary>DoomedPartyMen.Before (finally): bitwa skonczona - znacznik zdjety.</summary>
        internal static void Forget(MapEvent me)
        {
            try { if (me != null) { _note.Remove(me); _inCommit.Remove(me); } } catch { }
        }

        /// <summary>Prefiks MapEvent.CommitCalculatedMapEventResults (Priority.First): znacznik "Commit wszedl" (recenzja stare-a17 (1)).</summary>
        public static void CommitInPre(MapEvent __instance)
        {
            try
            {
                if (__instance == null) return;
                if (_inCommit.Count >= 256) _inCommit.Clear();   // zgubione znaczniki (bitwy gracza z wyjatkiem w ApplyGainedVariablesOnPlayerBattleContinues) - bez wzrostu
                _inCommit.Add(__instance);
            }
            catch { }
        }

        /// <summary>Finalizer MapEvent.CommitCalculatedMapEventResults (Priority.Last, void - wyjatku nie zmienia): Commit bez wyjatku - znacznik zdjety;
        /// z wyjatkiem - znacznik zostaje dla CommitFin (wyjatek z wnetrza Commit, drugiego Commit nie bedzie).</summary>
        public static void CommitInFin(MapEvent __instance, Exception __exception)
        {
            try { if (__instance != null && __exception == null) _inCommit.Remove(__instance); } catch { }
        }

        public static void CapturePre(MapEvent __instance)
        {
            try { _cCapIn++; Note(__instance, "pojmanie gry weszlo"); } catch { }
        }

        public static void CaptureFin(MapEvent __instance, Exception __exception)
        {
            try
            {
                if (__exception == null) { _cCapDone++; Note(__instance, "pojmanie gry skonczone"); }
                else Note(__instance, "pojmanie gry przerwane: " + __exception.GetType().Name);
            }
            catch { }
        }

        private static bool SiegeKind(MapEvent me)
        {
            try { return me.IsSallyOut || me.IsSiegeOutside || me.IsSiegeAssault || me.IsBlockadeSallyOut || me.IsBlockade; } catch { return false; }
        }

        /// <summary>Finalizer MapEvent.CalculateAndCommitMapEventResults (Priority.Last): wyjatek do logu; NRE w bitwie AI przy kluczu - dokonczenie (Commit +
        /// znacznik wyniku) i wyjatek zlapany tu. Inny wyjatek albo klucz wylaczony - przepuszczony bez zmian (jak dotad).</summary>
        public static Exception CommitFin(MapEvent __instance, Exception __exception)
        {
            if (__exception == null) return null;
            var me = __instance;
            try
            {
                _cFail++;
                bool siege = me != null && SiegeKind(me);
                if (siege) _cFailSiege++; else _cFailOther++;
                string type = __exception.GetType().Name + " (" + (me != null ? me.EventType.ToString() : "?") + ")";
                int k; _cTypes.TryGetValue(type, out k); _cTypes[type] = k + 1;
                if (_cTraces < 5)
                {
                    _cTraces++;
                    string st = __exception.StackTrace ?? "-";
                    st = st.Replace("\r", "").Replace("\n", " <- ");
                    if (st.Length > 1500) st = st.Substring(0, 1500);
                    Log.Info("Wynik bitwy przerwany wyjatkiem (A17 F4, slad " + _cTraces + "/5): " + __exception.GetType().FullName + ": " + __exception.Message
                             + " | bitwa " + (me != null ? me.EventType.ToString() : "?") + ", BattleState " + (me != null ? me.BattleState.ToString() : "?")
                             + ", gracz " + (me != null && me.IsPlayerMapEvent ? "tak" : "nie") + " | slad: " + st);
                }
                Note(me, "wynik gry przerwany wyjatkiem " + __exception.GetType().Name);
                // recenzja stare-a17 (1): wyjatek z wnetrza Commit (znacznik CommitInPre bez CommitInFin bez wyjatku) - Commit czesciowo wykonany, drugi bylby
                // dublem slawy, wplywu i morale; bez znacznika (latka Commit nie weszla) - tez bez drugiego Commit (nie wiadomo, czy Commit biegl)
                bool inCommit = me != null && (_inCommit.Remove(me) || !_commitMarked);
                if (inCommit) { _cInCommit++; Note(me, _commitMarked ? "wyjatek z wnetrza Commit - bez drugiego Commit" : "brak znacznika Commit - bez drugiego Commit"); }
                if (!SiegeOn || me == null || me.IsPlayerMapEvent || !(__exception is NullReferenceException) || inCommit) { _cPassed++; return __exception; }
                try
                {
                    if (_commit == null) _commit = AccessTools.DeclaredMethod(typeof(MapEvent), "CommitCalculatedMapEventResults");
                    if (_commit != null) _commit.Invoke(me, null);
                    Note(me, "Commit dokonczony (A17 F4)");
                }
                catch (Exception e2) { _cCommitFail++; Note(me, "Commit nie wszedl: " + (e2.InnerException ?? e2).GetType().Name); }
                finally { try { _inCommit.Remove(me); } catch { } }
                try { if (_appliedRef != null) _appliedRef(me) = true; } catch { }
                _cRescued++;
                return null;
            }
            catch (Exception e) { Stumble("CommitFin", e); return __exception; }
        }

        // ================================================================ F5 stocznie
        private static MethodInfo _hullPick, _idealOf;
        private static ShipHull _forcedHull;
        private static Town _pieceTown;   // recenzja stare-a17 (4): okno czesci zaplaconego statku (ShipPre -> ShipFin)
        private static int _pieceBefore;   // liczba statkow portu przed CreateShip - nowy statek to ostatni (ApplyByProduction dopisuje na koniec)
        private static ItemObject _hardwood, _planks;

        private static int Count(ItemRoster r, ItemObject it) { return r != null && it != null ? r.GetItemNumber(it) : 0; }

        /// <summary>Zaplata budowy z polki i kasy miasta (jedna regula dla kadluba i czesci): drewno = ceil(wartosc/1000 x ShipyardTimberPer1000) pni (deska = 1/5 pnia)
        /// z polki w ramce budow ksiegi towarow (+ OreLedger.NoteBuild), place = wartosc x ShipyardLabourShare z nadwyzki kasy ponad zapas kupcow -> kiesa ludu
        /// (KBuildLabour; bez kiesy ludu placa zostaje w kasie). Wynik: 0 - zaplacone, 1 - brak drewna, 2 - brak nadwyzki kasy (wtedy nic nie zdjete).</summary>
        private static int PayBuild(Town town, int value, out int useHw, out int usePl, out int wagePaid, out int wageKept)
        {
            useHw = usePl = wagePaid = wageKept = 0;
            var s = S;
            double per = Math.Max(0.0, s.ShipyardTimberPer1000), share = Math.Max(0.0, Math.Min(1.0, s.ShipyardLabourShare));
            int logs = (int)Math.Ceiling(Math.Max(0, value) / 1000.0 * per);
            int wage = (int)Math.Round(Math.Max(0, value) * share);
            if (_hardwood == null) { try { _hardwood = MBObjectManager.Instance.GetObject<ItemObject>("hardwood"); _planks = MBObjectManager.Instance.GetObject<ItemObject>("planks"); } catch { } }
            var shelf = town.Settlement.ItemRoster;
            int hw = Count(shelf, _hardwood), pl = Count(shelf, _planks);
            int uh = Math.Min(hw, logs), up = Math.Min(pl, (logs - uh) * 5);
            if (uh + up / 5 < logs || (logs > 0 && _hardwood == null)) return 1;
            if (wage > 0 && TownPurse.Spare(town) < wage) return 2;
            // drewno - ramka budow ksiegi towarow na osade (ujscie "budowy"), pnie takze w ksiedze drewna (OreLedger)
            var gf = GoodsLedger.Begin(GoodsLedger.FBuild, town.Settlement);
            try
            {
                if (uh > 0) { shelf.AddToCounts(_hardwood, -uh); OreLedger.NoteBuild(_hardwood, uh); }
                if (up > 0 && _planks != null) shelf.AddToCounts(_planks, -up);
            }
            finally { GoodsLedger.End(gf); }
            useHw = uh; usePl = up;
            // place ciesli: kasa -> kiesa ludu (bez kiesy ludu przelew nie wchodzi - placa zostaje w kasie)
            if (wage > 0)
            {
                int g0 = town.Gold;
                TownFolk.EarnFromTown(town.Settlement, wage, TownFolk.KBuildLabour);
                wagePaid = Math.Max(0, g0 - town.Gold);
                if (wagePaid < wage) wageKept = wage - wagePaid;
            }
            return 0;
        }

        /// <summary>Prefiks NavalDLC ShipProductionCampaignBehavior.CreateShip(Town): statek tylko ponizej liczby idealnej, z drewna z polki i za place z nadwyzki kasy.</summary>
        public static bool ShipPre(object __instance, Town __0)
        {
            try
            {
                _pieceTown = null;
                if (__0 == null || __0.Settlement == null || !Live) return true;   // nowa kampania - stocznie startowe jak w grze (stan wyjsciowy)
                _sWant++;                                                           // pomiar takze przy wylaczonym kluczu (statki z niczego)
                if (!ShipOn) return true;
                var town = __0;
                _forcedHull = null;
                int have = town.AvailableShips != null ? town.AvailableShips.Count : 0;
                int ideal = _idealOf != null ? (int)_idealOf.Invoke(null, new object[] { town }) : int.MaxValue;
                if (have >= ideal) { _sFull++; return false; }                    // jedna regula z portami (ShipPortsBuyToIdeal): ponad liczbe idealna stocznia kasuje
                var hull = _hullPick != null ? _hullPick.Invoke(__instance, new object[] { town }) as ShipHull : null;
                if (hull == null) { _sNoHull++; return false; }
                int useHw, usePl, wagePaid, wageKept;
                int r = PayBuild(town, hull.Value, out useHw, out usePl, out wagePaid, out wageKept);
                if (r == 1) { _sNoWood++; return false; }
                if (r == 2) { _sNoGold++; return false; }
                _sWage += wagePaid; _sWageKept += wageKept;
                _sWood += useHw; _sPlanks += usePl; _sValue += hull.Value;
                _forcedHull = hull;
                _pieceTown = town; _pieceBefore = have;   // recenzja stare-a17 (4): czesci zalozone w tym CreateShip - tez z drewna i za place (ShipFin -> SettlePieces)
                return true;
            }
            catch (Exception e) { Stumble("ShipPre", e); _forcedHull = null; _pieceTown = null; if (ShipOn) _sFree++; return true; }   // recenzja stare-a17 (5): gra zbuduje statek z niczego - liczony
        }

        /// <summary>Postfiks GetRandomShipHull: w CreateShip po zaplacie - ten sam kadlub, za ktory miasto zaplacilo.</summary>
        public static void HullPost(ref ShipHull __result)
        {
            if (_forcedHull == null) return;
            __result = _forcedHull;
            _forcedHull = null;
            _sBuilt++;
        }

        /// <summary>Finalizer CreateShip: znaczniki zdjete; po zaplaconym statku bez wyjatku - rozliczenie czesci (SettlePieces).</summary>
        public static void ShipFin(Exception __exception)
        {
            _forcedHull = null;   // statek nie powstal mimo zaplaty (wyjatek przed GetRandomShipHull) - znacznik nie przechodzi na nastepne wywolanie
            var town = _pieceTown;
            _pieceTown = null;    // okno czesci zamkniete (czesci zakladane poza CreateShip - gracz, inne sciezki - bez zmian)
            if (town == null || __exception != null) return;
            try { SettlePieces(town, _pieceBefore); }
            catch (Exception e) { Stumble("SettlePieces", e); _sPieceFree++; }
        }

        /// <summary>Wartosc czesci jak NavalDLCShipCostModel.GetShipUpgradePieceValueInternal bez atutow i polityk: Light / Medium / Heavy wedlug typu kadluba.</summary>
        private static int PieceValue(Ship ship, ShipUpgradePiece piece)
        {
            int t = ship.ShipHull != null ? (int)ship.ShipHull.Type : 0;
            return t == 2 ? piece.HeavyValue : t == 1 ? piece.MediumValue : piece.LightValue;
        }

        /// <summary>Recenzja stare-a17 (4): NavalDLC CreateShip zaklada losowa czesc w kazdym gniezdzie z szansa 0.5 za darmo (town.GetAvailableShipUpgradePieces),
        /// a NavalDLCShipCostModel.GetShipBaseValue dolicza czesci do wartosci statku. Po zaplaconym kadlubie: kazda czesc nowego statku (ostatni w porcie,
        /// ApplyByProduction dopisuje na koniec) z drewna i za place jak kadlub (PayBuild, pelna wartosc czesci); czego brak - czesc zdjeta (EquipUpgradePiece(gniazdo,
        /// null) - wlasciciel to port, nie gracz: bez listy odblokowanych). Statek nie w porcie (inna sciezka) - licznik "niesprawdzone".</summary>
        private static void SettlePieces(Town town, int before)
        {
            var ships = town.AvailableShips;
            if (ships == null || ships.Count != before + 1) { _sPieceLost++; return; }
            var ship = ships[ships.Count - 1];
            if (ship == null || ship.ShipHull == null || ship.ShipHull.AvailableSlots == null) { _sPieceLost++; return; }
            var slots = new List<string>(ship.ShipHull.AvailableSlots.Keys);
            foreach (var slot in slots)
            {
                if (!ship.HasSlot(slot)) continue;
                var piece = ship.GetPieceAtSlot(slot);
                if (piece == null) continue;
                int v = PieceValue(ship, piece);
                int useHw, usePl, wagePaid, wageKept;
                int r = PayBuild(town, v, out useHw, out usePl, out wagePaid, out wageKept);
                if (r == 0)
                {
                    _sPieces++; _sPieceValue += v;
                    _sWood += useHw; _sPlanks += usePl; _sWage += wagePaid; _sWageKept += wageKept;
                }
                else
                {
                    ship.EquipUpgradePiece(slot, null);   // gniazdo puste - czesci nie bylo z czego zrobic
                    if (r == 1) _sPieceNoWood++; else _sPieceNoGold++;
                }
            }
        }

        // ================================================================ wpiecia
        internal static void ApplyAll(Harmony h)
        {
            _hooks.Clear();
            // F1
            try
            {
                var c1 = AccessTools.TypeByName("BannerKings.Components.BannerKingsComponent");
                var c2 = AccessTools.TypeByName("BannerKings.Components.PopulationPartyComponent");
                var gm = c1 != null ? AccessTools.DeclaredMethod(c1, "GiveMounts") : null;
                var gp = c2 != null ? AccessTools.DeclaredMethod(c2, "GivePackAnimals") : null;
                if (gm != null) { h.Patch(gm, prefix: new HarmonyMethod(typeof(A17Fix), nameof(MountsPre))); _hooks.Add("F1 GiveMounts wpiete"); } else _hooks.Add("F1 BRAK BK GiveMounts");
                if (gp != null) { h.Patch(gp, prefix: new HarmonyMethod(typeof(A17Fix), nameof(PackPre))); _hooks.Add("F1 GivePackAnimals wpiete"); } else _hooks.Add("F1 BRAK BK GivePackAnimals");
            }
            catch (Exception e) { _hooks.Add("F1 blad " + e.GetType().Name); Log.Error("A17Fix.ApplyAll(F1)", e); }
            // F2
            try
            {
                var nf = AccessTools.TypeByName("NavalDLC.CampaignBehaviors.NavalFishingCampaignBehaviour");
                var mv = nf != null ? AccessTools.DeclaredMethod(nf, "OnDailyTickSettlement", new[] { typeof(Settlement) }) : null;
                var mp = nf != null ? AccessTools.DeclaredMethod(nf, "OnHourlyTickParty", new[] { typeof(MobileParty) }) : null;
                if (mv != null) { h.Patch(mv, prefix: new HarmonyMethod(typeof(A17Fix), nameof(NavalVillagePre)), finalizer: new HarmonyMethod(typeof(A17Fix), nameof(NavalVillageFin))); _hooks.Add("F2 NavalDLC wsie wpiete"); } else _hooks.Add("F2 BRAK NavalDLC OnDailyTickSettlement");
                if (mp != null) { h.Patch(mp, prefix: new HarmonyMethod(typeof(A17Fix), nameof(CrewPre)), finalizer: new HarmonyMethod(typeof(A17Fix), nameof(CrewFin))); _hooks.Add("F2 NavalDLC zaloga wpieta"); } else _hooks.Add("F2 BRAK NavalDLC OnHourlyTickParty");
                var bb = AccessTools.TypeByName("BannerKings.Behaviours.BKBuildingsBehavior");
                var rp = bb != null ? AccessTools.DeclaredMethod(bb, "RunPorts", new[] { typeof(Town) }) : null;
                if (rp != null) { h.Patch(rp, prefix: new HarmonyMethod(typeof(A17Fix), nameof(PortsPre)) { priority = Priority.First }, finalizer: new HarmonyMethod(typeof(A17Fix), nameof(PortsFin)) { priority = Priority.First }); _hooks.Add("F2 BK RunPorts wpiete"); } else _hooks.Add("F2 BRAK BK RunPorts");   // finalizer First: zaplata do kiesy ludu w oknie O19 (CirculationWindows.PortsFin - priorytet zwykly, po nim)
            }
            catch (Exception e) { _hooks.Add("F2 blad " + e.GetType().Name); Log.Error("A17Fix.ApplyAll(F2)", e); }
            // F4
            try
            {
                try { _appliedRef = AccessTools.FieldRefAccess<MapEvent, bool>("_mapEventResultsApplied"); } catch { _appliedRef = null; }
                _commit = AccessTools.DeclaredMethod(typeof(MapEvent), "CommitCalculatedMapEventResults");
                var cm = AccessTools.DeclaredMethod(typeof(MapEvent), "CalculateAndCommitMapEventResults");
                var cp = AccessTools.DeclaredMethod(typeof(MapEvent), "CaptureDefeatedPartyMembers");
                // recenzja stare-a17 (1): znacznik "Commit wszedl" - ta sama MethodInfo co _commit (DeclaredMethod na klasie deklarujacej); bez niego CommitFin nie dokancza
                _commitMarked = false;
                if (_commit != null)
                {
                    try
                    {
                        h.Patch(_commit, prefix: new HarmonyMethod(typeof(A17Fix), nameof(CommitInPre)) { priority = Priority.First },
                                         finalizer: new HarmonyMethod(typeof(A17Fix), nameof(CommitInFin)) { priority = Priority.Last });
                        _commitMarked = true;
                    }
                    catch (Exception e) { _hooks.Add("F4 znacznik Commit blad " + e.GetType().Name); Log.Error("A17Fix.ApplyAll(F4 znacznik Commit)", e); }
                }
                if (cm != null) { h.Patch(cm, finalizer: new HarmonyMethod(typeof(A17Fix), nameof(CommitFin)) { priority = Priority.Last }); _hooks.Add("F4 wynik bitwy wpiety" + (_commit == null ? " (BRAK CommitCalculatedMapEventResults)" : "") + (_commitMarked ? " (znacznik Commit wpiety)" : " (bez znacznika Commit - bez dokonczen)")); }
                else _hooks.Add("F4 BRAK CalculateAndCommitMapEventResults");
                if (cp != null) { h.Patch(cp, prefix: new HarmonyMethod(typeof(A17Fix), nameof(CapturePre)) { priority = Priority.First }, finalizer: new HarmonyMethod(typeof(A17Fix), nameof(CaptureFin)) { priority = Priority.Last }); _hooks.Add("F4 pojmanie wpiete"); }
                else _hooks.Add("F4 BRAK CaptureDefeatedPartyMembers");
            }
            catch (Exception e) { _hooks.Add("F4 blad " + e.GetType().Name); Log.Error("A17Fix.ApplyAll(F4)", e); }
            // F5
            try
            {
                var sp = AccessTools.TypeByName("NavalDLC.CampaignBehaviors.ShipProductionCampaignBehavior");
                var cs = sp != null ? AccessTools.DeclaredMethod(sp, "CreateShip", new[] { typeof(Town) }) : null;
                _hullPick = sp != null ? AccessTools.DeclaredMethod(sp, "GetRandomShipHull", new[] { typeof(Town) }) : null;
                _idealOf = sp != null ? AccessTools.DeclaredMethod(sp, "GetIdealShipCountForTown", new[] { typeof(Town) }) : null;
                if (cs != null && _hullPick != null && _idealOf != null && _hullPick.ReturnType == typeof(ShipHull))
                {
                    h.Patch(_hullPick, postfix: new HarmonyMethod(typeof(A17Fix), nameof(HullPost)) { priority = Priority.Last });
                    h.Patch(cs, prefix: new HarmonyMethod(typeof(A17Fix), nameof(ShipPre)), finalizer: new HarmonyMethod(typeof(A17Fix), nameof(ShipFin)));
                    _hooks.Add("F5 stocznia wpieta");
                    _hooks.Add("F5 czesci z drewna i za place (finalizer CreateShip)");   // recenzja stare-a17 (4)
                }
                else _hooks.Add("F5 BRAK NavalDLC CreateShip / GetRandomShipHull / GetIdealShipCountForTown - stocznia jak w grze");
            }
            catch (Exception e) { _hooks.Add("F5 blad " + e.GetType().Name); Log.Error("A17Fix.ApplyAll(F5)", e); }
            var s = S;
            Log.Info("A17Fix (stare zaleglosci A17, noc 17/18.10): " + string.Join(", ", _hooks.ToArray()) + " | klucze: A17BkPartyNoFreeMounts " + YN(BkOn) + ", A17FishFromHands " + YN(FishOn)
                     + ", A17PortFishPaysFolk " + YN(PortFolkOn) + ", A17UniquesNotOutlawKit " + YN(UniqOn) + ", A17SiegeCommitRescue " + YN(SiegeOn) + ", A17ShipyardBuildsFromTimber " + YN(ShipOn)
                     + (s != null ? " (pnie na 1000 wartosci " + s.ShipyardTimberPer1000.ToString("0.##", CultureInfo.InvariantCulture) + ", place " + s.ShipyardLabourShare.ToString("0.##", CultureInfo.InvariantCulture) + " wartosci)" : "") + ".");
        }

        private static string YN(bool b) { return b ? "TAK" : "NIE"; }

        // ================================================================ linie doby
        internal static void Daily()
        {
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                string pot = " | potkniecia dzis " + _stumblesDay + " (razem " + _stumbles + ").";
                Log.Info("Konie partii BK (A17 F1): dzien " + day + " | klucz A17BkPartyNoFreeMounts " + YN(BkOn)
                         + " | dar BK z niczego pominiety: GiveMounts " + _bkSkipped + " wywolan, GivePackAnimals " + _bkSkippedPack + " wywolan"
                         + " | partie BK kasowane bez niszczyciela: konie z taboru na targ osady " + _bkHorsesHome + " szt. z " + _bkPartiesHome + " partii, bez osady " + _bkNoWhere
                         + ", handlarze BK (Trading - BK kopiuje ladunek) " + _bkTradingKept
                         + " | ksiega koni: zrodlo 'partie BK' i ujscie 'po zniszczeniu' w linii Konie (E6/0) (przy kluczu ok. 0)" + pot);
                Log.Info("Ryby z niczego (A17 F2): dzien " + day + " | klucz A17FishFromHands " + YN(FishOn) + " | KS zastosowany " + YN(StartCalibration.Applied)
                         + " | atut Night Raider (NavalDLC, wies miasta z namiestnikiem): zablokowane w KS " + _fNrBlockedN + " szt. w " + _fNrBlockedV + " wsiach, dopisane poza KS (z niczego) "
                         + _fNrPassN + " szt. w " + _fNrPassV + " wsiach"
                         + " | porty BK (RunPorts): zablokowane w KS " + _fPortBlocked + " miast, poza KS (z niczego) " + _fPortPassFish + " szt. w " + _fPortPass + " miastach za " + _fPortPassGold + " zl w nicosc"
                         + " (klucz A17PortFishPaysFolk " + YN(PortFolkOn) + ": zaplacone " + _fPortPaid + " zl = do kiesy ludu " + _fPortFolk + " + zostalo w kasie " + _fPortBack + ")"
                         + " | polow zalogi na morzu (Master Angler, ramka polowu ksiegi towarow - zrodlo nazwane) " + _fCrew + " szt. w " + _fCrewParties + " godzinach partii"
                         + " | razem ryby z niczego " + (_fNrPassN + _fPortPassFish) + pot);
                Log.Info("Unikaty u band (A17 F3): dzien " + day + " | klucz A17UniquesNotOutlawKit " + YN(UniqOn) + " | unikaty pominiete przy awansie band (kandydaci u pasera i w jukach) "
                         + _uSkip + " (sesja " + _uSkipAll + ") - unikat zostaje na polce albo w jukach (lup), nie znika w zolnierzu bandy" + pot);
                var tb = new StringBuilder();
                foreach (var kv in _cTypes) tb.Append(tb.Length > 0 ? "; " : "").Append(kv.Key).Append(" x").Append(kv.Value);
                Log.Info("Wynik bitwy (A17 F4): dzien " + day + " | klucz A17SiegeCommitRescue " + YN(SiegeOn) + " | wynikow bitew z wyjatkiem " + _cFail + " (oblezenia " + _cFailSiege + ", inne " + _cFailOther
                         + "), dokonczonych (Commit + znacznik wyniku) " + _cRescued + ", Commit nie wszedl " + _cCommitFail + ", wyjatek przepuszczony " + _cPassed
                         + " (w tym z wnetrza Commit, bez drugiego Commit " + _cInCommit + ")"
                         + " | wyjatki: " + (tb.Length > 0 ? tb.ToString() : "-") + " | pojmanie gry: weszlo " + _cCapIn + ", skonczone " + _cCapDone
                         + " (rannych zostawionych przez przerwane pojmanie bierze droga C linii 'Ludzie kasowanych partii (A17)')" + pot);
                Log.Info("Stocznie (A17 F5): dzien " + day + " | klucz A17ShipyardBuildsFromTimber " + YN(ShipOn) + " | statki NavalDLC chciane " + _sWant + ": zbudowane " + _sBuilt
                         + " (wartosc kadlubow " + _sValue + " zl; drewno " + _sWood + " pni + " + _sPlanks + " desek z polek miast; place ciesli " + _sWage + " zl z kasy do kiesy ludu, bez kiesy ludu zostalo w kasie " + _sWageKept
                         + "), pominiete - port ma liczbe idealna " + _sFull + ", brak drewna " + _sNoWood + ", brak nadwyzki kasy na place " + _sNoGold + ", brak kadluba " + _sNoHull
                         + " | statki z niczego " + (ShipOn ? _sFree : _sWant)
                         + " | czesci nowych statkow (ulepszenia CreateShip, z drewna i za place - wliczone wyzej; czego brak - zdjete): zalozone " + _sPieces + " za " + _sPieceValue + " zl wartosci, pominiete - brak drewna "
                         + _sPieceNoWood + ", brak nadwyzki kasy " + _sPieceNoGold + ", z niczego (potkniecie) " + _sPieceFree + ", statek poza portem (niesprawdzone) " + _sPieceLost + pot);
            }
            catch (Exception e) { Log.Error("A17Fix.Daily", e); }
            DayReset();
        }
    }
}
