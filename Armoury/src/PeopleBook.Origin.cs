using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E4/7 (164b-1, PLAN 4.8, 4.13, 4.17 prog; plan kodu docs/etap4/PLAN-KODU-E47.md): POCHODZENIE LUDZI - konta SLUZBA i JENCY ksiegi ludzi z regionem pochodzenia.
    /// Pochodzenie to UDZIALY, nie ludzie z imienia: kazdy roster szeregowych (sklad partii, loch partii, loch osady) ma slownik region -> liczba (double; region =
    /// warownia OutlawLaw.RegionFor, "" = bez pochodzenia). Kazde wyjscie zdejmuje proporcjonalnie (Take), kazde wejscie ze znanym zrodlem dopisuje (Give),
    /// a raz na dobe uzgodnienie (Reconcile) zrownuje slownik ze stanem rosteru - nadwyzka rosteru do "" (z niczego, nazwane wedlug rodzaju partii), niedobor
    /// zdejmowany proporcjonalnie (w nicosc, nazwane). Rekrut zdjety z DOM (BK DeduceManpower, A5, karczma, pobor wymuszony) niesie region swojej wsi; zolnierz
    /// ze znanym pochodzeniem wraca do DOM regionu pochodzenia z hearth (+1/k - PeopleBook.Move), polegly ubywa trwale z regionu pochodzenia (POLEGLI, rg:).
    /// Ten sam warunek LevyLive dla zdjecia i dla powrotu z hearth (symetria - przelaczenie w trakcie kampanii nie robi ludzi z niczego). Przed siewem (pierwsza doba
    /// po wlaczeniu) wszystko tu jest no-op. Stan tylko w "arm_people" (sekcje sw: so: jo: wo: rg:). Kazde cialo we wlasnym try, potkniecia liczone (U24).
    /// Pula wyrzutkow zostaje geograficzna (region miejsca); zolnierz ze znanym pochodzeniem w puli niesie udzial (wo:) - odstepstwo od litery R6, zgodne z jej celem.
    /// </summary>
    internal static partial class PeopleBook
    {
        // ================================================================ przyczyny ruchu kont SLUZBA/JENCY (indeksy licznikow doby)
        internal const int
            // wejscia (Give bez Take)
            FLevyVillage = 0, FLevyTown = 1, FLevyCastle = 2, FTavernAi = 3, FLevyA5 = 4, FLevyPlayer = 5, FTavernPlayer = 6, FPress = 7,
            // FNoneBrake (recenzja E4/7 R1-3): najemnik z karczmy w krainie z hamulcem wolnych rak - obcy, bez zdjecia z DOM
            FPoolBand = 8, FNoneMap = 9, FNoneWatch = 10, FNoneFloor = 11, FNoneBrake = 12, FNoneOff = 13,
            // przenosiny (Take + Give)
            FToGarrison = 14, FFromGarrison = 15, FScreen = 16, FNewParty = 17, FDisbandGarr = 18, FPatrolOut = 19, FPatrolIn = 20,
            FCaptured = 21, FLooted = 22, FTransfer = 23, FPrisRecruit = 24,
            // wyjscia (Take bez Give)
            FDieBattle = 25, FDiePlague = 26, FDieExec = 27, FDieOthers = 28,
            FRouted = 29, FDesert = 30, FFreed = 31, FRelease166 = 32, FGentry179 = 33, FSold = 34, FReleased = 35, FPatrolPris = 36, FDisband = 37,
            FBandDisband = 38, FCaptureLost = 39, FLootLost = 40, FGone = 41,
            // E4/8 (164b-2): numery 42-55. Wejscia (Give): milicja, nowa druzyna, karawany, konwoje morskie, wedrowcy BK,
            // Straz (do E4/9), ulamek czlowieka bez DOM; przenosiny: milicja z rozwiazanej partii (Give - Take robi E4/7 FDisband), zaloga -> nowa druzyna;
            // wyjscia (Take): milicja bez walki do domu, wedrowcy/konwoje u celu, rozwiazane bez bitwy do domu (R6)
            FMilitiaLevy = 42, FMilitiaDisband = 43, FSpawnLevy = 44, FSpawnGarrison = 45, FCaravan = 46, FCaravanRefill = 47, FRoadOut = 48, FRoadNone = 49,
            FSlavesBk = 50, FWatchArms = 51, FArmsFloor = 52, FMilitiaHome = 53, FRoadEnd = 54, FDisbandHome = 55,
            // E4/10 (167): wejscie z karczmy - najem weterana z puli WETERANI (udzialy pochodzenia z puli; Veterans.OnTavernNumber).
            // Scalenie calosci etapu 4: w paczce E4/10 bylo 52 (FKinds 53) - przeniesione za E4/8; poza zakresami Part(...) E4/8 (FMilitiaLevy..FArmsFloor, FMilitiaHome..FDisbandHome)
            FVetHire = 56,
            // E4/9 (192): wejscia (Give) - rekrut Strazy z Westeros (WatchLaw.Levy): z konta MUR, z pul wyrzutkow Westeros, z ludnosci Westeros, reszta bez pokrycia;
            // jency z rabunku - pojmani z konta UCHODZCY napadnietej wsi do lochu najezdzcy (ThrallLaw.CapturePrefix). Pierwsze wolne numery po E4/10
            FWatchWall = 57, FWatchPool = 58, FWatchLevy = 59, FNoneWatchLeft = 60, FCapturedRaid = 61,
            // poprawki E4 po tescie koncowym (fixA): wyjscia (Take) - polegli od ostrzalu w oblezeniu poza bitwa (P1-2), jency zbiegli z przepelnionego lochu partii (P2-T4)
            FDieSiege = 62, FEscape = 63,
            // N7 (StragglersRally, decyzja Jeffa 13.10): wejscie (Give) - rozbici odebrani ze zbiorki pod choragiew (zdjeci przy rozbiciu przez FRouted)
            FRally = 64,
            FKinds = 65;
        private static readonly string[] FName =
        {
            "wsie", "miasta", "zamki", "karczmy AI", "A5", "gracz", "karczma gracza", "pobor wymuszony",
            "z puli do band", "ochotnik z mapy", "Straz (WatchLaw wylaczone; obca partia w osadzie Daru)", "pobor na podlodze", "najemnik z karczmy przy hamulcu (obcy)", "ksiega wylaczona (LevyLive)",
            "partia->zaloga", "zaloga->partia", "ekran druzyny", "nowa partia rodu", "rozwiazane do zalog", "patrole BK wyjscie", "patrole BK powrot",
            "jency po bitwie wzieci", "odbici jency", "przekazani jency", "wcieleni jency",
            "polegli w bitwie", "zaraza", "straceni", "Inni",
            "rozbici", "dezercja", "uwolnieni bez odbiorcy (H3)", "zwolnieni 166", "rycerze 179", "sprzedani jency", "wypuszczeni przez gracza", "jency patroli BK", "rozwiazane partie",
            "rozwiazane bandy", "wzieci bez odbiorcy", "odbici bez odbiorcy", "przy zniknieciu partii",
            "milicja z wolnych rak", "milicja z rozwiazanej partii we wsi", "nowa druzyna z wolnych rak", "nowa druzyna z zalogi siedziby", "karawany z ludzi osady startu",
            "konwoje morskie z ludzi miasta", "wedrowcy i eskorta BK z DOM", "wedrowcy BK bez pochodzenia", "niewolnicy w konwojach BK (klasa BK)", "Straz E4/8 (WatchLaw wylaczone; osada Daru poza Straza)",
            "ulamek czlowieka bez DOM (E4/8)", "milicja bez walki do domu", "wedrowcy i konwoje BK u celu", "rozwiazane bez bitwy do domu (R6)",
            "z karczmy (WETERANI, E4/10)",
            "Straz z Muru (E4/9)", "Straz z pul wyrzutkow Westeros (E4/9)", "Straz z ludnosci Westeros (E4/9)", "Straz bez pokrycia (podloga, E4/9)", "pojmani z rabunku (E4/9)",
            "polegli w oblezeniu (ostrzal)", "ucieczka ponad limit lochu",
            "rozbici pod choragiew (N7)"
        };

        // rodzaj rosteru (nazwy "bez pochodzenia" / "z niczego" / "w nicosc")
        internal const int KLord = 0, KGarrison = 1, KMilitia = 2, KCaravan = 3, KBand = 4, KPatrolBk = 5, KPatrolGame = 6, KPlayer = 7, KWatch = 8, KOther = 9,
                           KPrison = 10, KDungeon = 11,
                           KRoad = 12,      // E4/8 (R19, Z8): wedrowcy i konwoje BK ze slownikiem skladu (TravellersFromPeople) - w drodze, poza S (Z9)
                           KKinds = 13;
        private static readonly string[] KName = { "partie rodow", "zalogi", "milicje", "karawany", "bandy", "patrole BK", "patrole gry", "gracz", "Straz", "inne",
                                                   "lochy partii", "lochy osad", "wedrowcy i konwoje BK" };
        private const int KVillager = -1, KConvoy = -2, KUndead = -3;     // bez slownika skladu (TABOR, konwoje BK - E4/8, sklad Innych)

        // dokad (po Take): smierc, dom, pula, Mur, niewola, nicosc nazwana, rostery bez slownika
        // N7: ORally - rozbici do zbiorki pod choragiew (StragglersRally - udzialy jada z grupa; w rownaniu regionu jak "w drodze")
        private const int ODead = 0, OHomeKnown = 1, OHomeLegacy = 2, OPool = 3, OWall = 4, OSlave = 5, OLost = 6, ODrop = 7, ORally = 8, OKinds = 9;

        private const double Eps = 1e-6;
        /// <summary>Mezczyzni 16-60 w ludnosci (SZACUNEK, DOK rozdz. 4.0) - jedna stala dla R5 (FreeHands) i PeopleLedger.</summary>
        internal const float MenShare = 0.27f;

        // ================================================================ udzialy
        /// <summary>Udzialy pochodzenia w ruchu: region (StringId warowni) -> ludzie; "" = bez pochodzenia. Total = suma.</summary>
        internal sealed class Shares
        {
            internal readonly Dictionary<string, double> M = new Dictionary<string, double>(4);
            internal double Total;
            /// <summary>E4/9 (U5, R14 "sluzba dozywotnia"): ilu z tych ludzi zdjeto ze sluzby Strazy (Take z rosteru Strazy - WatchServes). HomeKnown/ArriveAt
            /// oddaja te czesc do konta MUR zamiast do DOM. Cut dzieli proporcjonalnie, AddAll sumuje (mieszana grupa - tylko czesc Strazy na Mur).</summary>
            internal double W;
            internal double Unknown { get { double v; return M.TryGetValue("", out v) ? v : 0.0; } }
            internal double Known { get { return Math.Max(0.0, Total - Unknown); } }

            internal void Add(string k, double n)
            {
                if (!(n > 0.0) || double.IsInfinity(n)) return;
                k = k ?? "";
                double v; M.TryGetValue(k, out v); M[k] = v + n; Total += n;
            }

            internal void AddAll(Shares s) { if (s != null) { foreach (var kv in s.M) Add(kv.Key, kv.Value); if (s.W > 0.0) W = Math.Min(Total, W + s.W); } }

            internal static Shares None(double n) { var s = new Shares(); s.Add("", n); return s; }

            /// <summary>Wycina n ludzi proporcjonalnie (zdejmuje z tego obiektu); brak - reszta jako "" (bez pochodzenia). E4/9: czesc Strazy (W) - proporcjonalnie.</summary>
            internal Shares Cut(double n)
            {
                var r = new Shares();
                if (!(n > 0.0) || double.IsInfinity(n)) return r;
                if (Total <= Eps) { r.Add("", n); return r; }
                double take = Math.Min(n, Total), f = take / Total;
                if (f >= 1.0 - 1e-12)
                {
                    foreach (var kv in M) r.Add(kv.Key, kv.Value);
                    r.W = W; W = 0.0;
                    M.Clear(); Total = 0.0;
                }
                else
                {
                    foreach (var k in M.Keys.ToList())
                    {
                        double m = M[k] * f;
                        r.Add(k, m);
                        double left = M[k] - m;
                        if (left > Eps) M[k] = left; else M.Remove(k);
                    }
                    Total = 0.0; foreach (var v in M.Values) Total += v;
                    if (W > 0.0) { r.W = Math.Min(r.Total, W * f); W = Math.Max(0.0, Math.Min(Total, W - r.W)); }
                }
                if (n - take > Eps) r.Add("", n - take);
                return r;
            }
        }

        private sealed class Org { public readonly Dictionary<string, double> M = new Dictionary<string, double>(4); public double Sum; }

        /// <summary>
        /// Poprawki E4 po tescie koncowym (fixA, P11): porownanie po referencji (ReferenceEquals + RuntimeHelpers.GetHashCode) dla slownikow i zbiorow kluczowanych
        /// MobileParty. MBObjectBase.GetHashCode = Id.GetHashCode, a gra nadaje Id dopiero w CampaignObjectManager.AddMobileParty - PO _partyComponent.Create, gdzie
        /// rodzi sie sklad bandy (FindAppropriateInitialRosterForMobileParty -> OutlawLaw.RosterPostfix -> Pending / _empty.Add). Klucz dodany przed nadaniem Id lezy
        /// w kubelku hasha 0 i kazde pozniejsze TryGetValue / Remove / indeksator chybia (KeyNotFoundException w ReconcileDaily co dobe od d3). Equals MBObjectBase
        /// i tak jest referencyjne - dla partii z nadanym Id zachowanie bez zmian. PartyBase nie nadpisuje GetHashCode (slowniki _mem/_pris bez zmian).
        /// net472 nie ma ReferenceEqualityComparer (wzor GarrisonKit.RefEq); kontrawariancja IEqualityComparer&lt;in T&gt; pozwala podac go do Dictionary/HashSet
        /// kluczowanych MobileParty bez zmiany typu pola (Leaks310 czyta OutlawLaw._empty refleksja jako HashSet&lt;MobileParty&gt;).
        /// </summary>
        internal sealed class RefEq : IEqualityComparer<object>
        {
            internal static readonly RefEq I = new RefEq();
            bool IEqualityComparer<object>.Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object o) { return o == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }

        private sealed class RegRef
        {
            public double DomRef, SRef, JRef, LRef, Fallen, FallenMen, Civil, Born, Enslaved, ToWall, PoolRef, RefRef, CartRef;
            // recenzja E4/7 R1-1: pobor z regionu od siewu - ze stanu miasta (TownOut) i z wsi (VillOut) - i powroty do stanu miasta (TownBack; dlug miasta = TownOut - TownBack)
            public double TownOut, VillOut, TownBack;
            // recenzja E4/7 R2-3: ludzie przeniesieni miedzy regionami przy przycieciu kluczy slownikow (+ region dostal, - oddal) - rownanie regionu je odejmuje
            public double Drift;
            // E4/8 (R19, 10.5): migracja miedzy regionami od siewu (wedrowcy BK u celu w innym regionie) - rownanie regionu: + MigOut - MigIn
            public double MigIn, MigOut;
            // E4/9 (Z10): niewolni z regionu (wedlug pochodzenia) i konto MUR z regionu w dniu siewu, zmarli niewolni z regionu od siewu - rownanie regionu
            public double TRef, WRef, TDead;
        }

        // ================================================================ stan w zapisie
        private static readonly Dictionary<PartyBase, Org> _mem = new Dictionary<PartyBase, Org>();      // sklad partii - konto SLUZBA
        private static readonly Dictionary<PartyBase, Org> _pris = new Dictionary<PartyBase, Org>();     // loch partii i osady - konto JENCY
        private static readonly Dictionary<string, Dictionary<string, double>> _poolOrg = new Dictionary<string, Dictionary<string, double>>();   // region puli -> pochodzenie -> ludzie
        private static readonly Dictionary<string, RegRef> _rg = new Dictionary<string, RegRef>();      // region -> stan dnia siewu i liczniki od siewu
        private static bool _sown;
        private static int _sowDay = -1;
        private static string _pendSo, _pendJo;                                                         // so:/jo: z zapisu do rozwiazania w OnSessionLaunched
        // udzialy czekajace na roster (narodziny bandy); fixA (P11): klucz dodany przed nadaniem Id partii - porownanie po referencji (RefEq)
        private static readonly Dictionary<MobileParty, Shares> _pending = new Dictionary<MobileParty, Shares>(RefEq.I);

        // ================================================================ liczniki doby (bez zapisu)
        private static readonly double[] _fIn = new double[FKinds], _fOut = new double[FKinds];
        private static readonly double[] _dest = new double[OKinds];
        private static readonly double[] _homeBy = new double[MKinds];       // powroty z pochodzeniem wedlug przyczyny ruchu DOM
        private static readonly double[] _zBy = new double[KKinds], _qBy = new double[KKinds];
        private static readonly double[] _deadBy = new double[FKinds];
        private static double _gAll, _tAll, _zAll, _qAll, _gone, _deadCarts, _pendIn, _pendLost, _homeNoVillage;
        private static double _homeTown, _foldMen, _pressIn, _pressLost;    // recenzja E4/7: powrot do stanu miasta (R1-1), przyciecie kluczy (R2-3), pobor wymuszony (R1-5)
        private static double _pressHome;                                   // E4/7b: niewzieci z poboru wymuszonego, ktorzy wrocili do DOM swojej wsi (doba)
        // poprawki E4 po tescie koncowym (fixA) - liczniki doby (bez zapisu)
        private static int _defParties;                                     // T3: partie w trwajacej bitwie - uzgodnienie skladu odlozone do konca bitwy
        private static double _defMen;                                      // T3: ich niedobor rosteru wobec slownika w chwili uzgodnienia (zdejmie go koniec bitwy)
        private static int _siegeCalls;                                     // P1-2: wywolania ostrzalu (KillRandomTroopsOfEnemy) z ubytkiem w skladzie
        private static double _siegeDead;                                   // P1-2: polegli od ostrzalu (szeregowi bez wightow - roznica rosteru)
        private static double _villTown;                                    // T6: pobor wsi na podlodze 50 hearth wziety ze stanu miasta regionu
        private static Dictionary<string, double> _villTownBy;              // T6: region -> ludzie (nazwy do linii)
        private static double _townShareVill;                               // fix2 (P1-4): pobor E4/7 w miescie wziety z wsi regionu zamiast ze stanu miasta (udzial miasta)
        private static double _townShareBack;                               // fix2 (P1-4): reszta znow ze stanu miasta - wsie regionu na podlodze 50 hearth
        private const int SBattle = 0, SSiege = 1, SSea = 2, SStarve = 3, SArmy = 4, STown = 5, SField = 6, SKinds = 7;
        private static readonly string[] SName = { "bitwa", "oblezenie", "morze", "glod", "armia", "w osadzie", "w polu" };
        private static readonly double[] _cutBy = new double[SKinds];      // P2-T4-sklady (diagnostyka): niedobor skladow (ciecie slownika) wedlug stanu partii
        private static readonly List<KeyValuePair<double, string>> _cutTop = new List<KeyValuePair<double, string>>(6);    // 5 najwiekszych niedoborow doby
        // P2-T4-lochy: ucieczki ponad limit lochu partii od ostatniej linii "Jency (E4/0)" (EscapeSegment czyta i zeruje)
        private static int _escAll, _escHome, _escPool, _escTavern, _escLost;
        private static int _foldKeys;
        private static Shares _press;                                       // R1-5: pobrani przez pobor wymuszony gracza - czekaja na ekran lupu (do konca doby)
        private static Dictionary<Village, double> _pressVil;               // E4/7b: te same oczekujace wedlug wsi zdjecia (do powrotu niewzietych; bez zapisu)
        private static int _deduceCalls, _deduceOutside, _deduceNoWin, _floorCalls;
        private static double _deduceMen, _floorMen;
        private static int _deduceVetCalls;                                 // E4/10 (Z8): najem z karczmy AI przy VetLive - BK DeduceManpower bez zdjecia z DOM (udzialy z puli)
        private static double _deduceVetMen, _vDestTake;                    // E4/10: ludzie tych najmow; udzialy zdjete ze SLUZBY (Take wolajacego) do puli WETERANI
        private static int _oStumbles, _oStumblesDay, _badIds, _resolveMissing, _resolveFound;
        private static string _sowNote, _resolveNote;
        private static double _state0 = double.NaN;                         // stan kont (SLUZBA + JENCY) z konca poprzedniej doby (NaN = pierwsza doba sesji)
        private static double _lastMs;
        [ThreadStatic] internal static int WinRecruited, WinReleased;       // ekran druzyny: wcieleni / wypuszczeni w trakcie DoneLogic (poprawka delt)

        // pamiec pomocnicza (bez zapisu)
        private static readonly Dictionary<string, Settlement> _node = new Dictionary<string, Settlement>();
        private static readonly Dictionary<CharacterObject, bool> _wight = new Dictionary<CharacterObject, bool>();
        private static readonly Dictionary<MobileParty, int> _kind = new Dictionary<MobileParty, int>(RefEq.I);   // fixA (P11): jedna zasada dla slownikow ksiegi kluczowanych MobileParty (Equals bez zmian, hash bez Id)
        private static Func<TroopRoster, PartyBase> _owner;
        private static bool _ownerTried;

        // ================================================================ wylaczniki
        private static bool OriginOn { get { var s = Settings.Current; return s != null && s.PeopleOriginEnabled; } }
        /// <summary>Ksiega pochodzenia dziala (po siewie i przy wlaczonym PeopleOriginEnabled). Inaczej latki E4/7 sa no-op.</summary>
        internal static bool OriginLive { get { return _sown && OriginOn; } }
        /// <summary>Pobor z DOM i powrot z hearth - JEDEN warunek dla obu stron (symetria; P 2.4).</summary>
        internal static bool LevyLive { get { var s = Settings.Current; return _sown && s != null && s.PeopleOriginEnabled && s.LevyFromVillages && s.PeopleUnitEnabled; } }
        internal static bool Sown { get { return _sown; } }
        internal static int SowDay { get { return _sowDay; } }

        private static void OStumble(string where, Exception e) { _oStumbles++; _oStumblesDay++; Stumble("E4/7." + where, e); }

        // ================================================================ reset / zapis
        private static void ResetOrigin()
        {
            _mem.Clear(); _pris.Clear(); _poolOrg.Clear(); _rg.Clear(); _sown = false; _sowDay = -1; _pendSo = _pendJo = null; _pending.Clear();
            _node.Clear(); _wight.Clear(); _kind.Clear();
            _oStumbles = 0; _badIds = 0; _resolveMissing = _resolveFound = 0; _sowNote = null; _resolveNote = null; _state0 = double.NaN;
            _press = null; _pressVil = null;
            _escAll = _escHome = _escPool = _escTavern = _escLost = 0;     // fixA (P2-T4-lochy): liczniki ucieczek tylko z tej kampanii
            // recenzja E4/7 R2-2: BK tworzy przy kazdym wczytaniu i nowej kampanii nowy PopulationManager kluczowany obiektami Settlement (wzor ClanBudget.Reset) -
            // stary menedzer nie zna osad nowej kampanii (A5 z klasa BK po cichu bez zdjecia). BkResolve jest leniwe - podepnie sie do nowego.
            _bkTried = false; _popMgr = null; _getPopData = null;
            OriginNewDay();
            try { FreeHands.Reset(); } catch (Exception e) { Stumble("ResetOrigin(FreeHands)", e); }
        }

        private static void OriginNewDay()
        {
            Array.Clear(_fIn, 0, FKinds); Array.Clear(_fOut, 0, FKinds); Array.Clear(_dest, 0, OKinds); Array.Clear(_homeBy, 0, MKinds);
            Array.Clear(_zBy, 0, KKinds); Array.Clear(_qBy, 0, KKinds); Array.Clear(_deadBy, 0, FKinds);
            _gAll = _tAll = _zAll = _qAll = _gone = _deadCarts = _pendIn = _pendLost = _homeNoVillage = 0;
            _deduceCalls = _deduceOutside = _deduceNoWin = _floorCalls = 0; _deduceMen = _floorMen = 0; _a5Bk = _a5BkFail = 0;
            _deduceVetCalls = 0; _deduceVetMen = 0; _vDestTake = 0;    // E4/10
            _homeTown = 0; _foldKeys = 0; _foldMen = 0; _pressIn = _pressLost = _pressHome = 0;
            _defParties = 0; _defMen = 0; _siegeCalls = 0; _siegeDead = 0; _villTown = 0; _villTownBy = null;     // fixA
            _townShareVill = 0; _townShareBack = 0;                                                     // fix2
            Array.Clear(_cutBy, 0, SKinds); _cutTop.Clear();
            _oStumblesDay = 0;
            _kind.Clear();          // rodzaj partii raz na dobe (Straz, rod - moga sie zmienic)
        }

        private static bool BadKey(string id) { return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf(',') >= 0 || id.IndexOf(';') >= 0 || id.IndexOf('=') >= 0 || id.IndexOf(':') >= 0; }
        private static string N3(double v) { return v.ToString("0.###", Inv); }

        private static void AppendOrg(StringBuilder sb, string id, Org o, ref bool first)
        {
            if (o == null || o.Sum <= Eps) return;
            if (BadKey(id)) { _badIds++; return; }
            sb.Append(first ? "" : ",").Append(id).Append('=');
            first = false;
            bool f2 = true;
            foreach (var kv in o.M)
            {
                if (kv.Value <= Eps) continue;
                string k = kv.Key.Length == 0 ? "~" : kv.Key;
                if (k != "~" && BadKey(k)) { _badIds++; continue; }
                sb.Append(f2 ? "" : ";").Append(k).Append(':').Append(N3(kv.Value));
                f2 = false;
            }
        }

        private static string PartyKey(PartyBase pb)
        {
            if (pb == null) return null;
            if (pb.IsMobile) return pb.MobileParty != null ? pb.MobileParty.StringId : null;
            return pb.Settlement != null && pb.Settlement.StringId != null ? "@" + pb.Settlement.StringId : null;
        }

        private static void ExportOrigin(StringBuilder sb)
        {
            if (!_sown) return;
            int len0 = sb.Length;
            try { ExportOriginBody(sb); }
            finally
            {
                try { Log.Info("Pochodzenie (E4/7): zapis arm_people - slownikow skladu " + _mem.Count + ", lochow " + _pris.Count + ", pul " + _poolOrg.Count + ", regionow " + _rg.Count
                               + "; sekcje E4/7 " + (sb.Length - len0) + " znakow, caly napis " + sb.Length + " (SaveText w kawalkach po 8000); nie zapisane (znaki w id) " + _badIds + "."); } catch { }
            }
        }

        private static void ExportOriginBody(StringBuilder sb)
        {
            sb.Append("|sw:").Append(_sowDay.ToString(Inv)).Append(";1");
            bool first = true;
            sb.Append("|so:");
            foreach (var kv in _mem) AppendOrg(sb, PartyKey(kv.Key), kv.Value, ref first);
            // nierozwiazane z wczytania (partia jeszcze nie znaleziona) - nie gubimy
            first = true;
            sb.Append("|jo:");
            foreach (var kv in _pris) AppendOrg(sb, PartyKey(kv.Key), kv.Value, ref first);
            first = true;
            sb.Append("|wo:");
            foreach (var kv in _poolOrg)
            {
                if (BadKey(kv.Key)) { _badIds++; continue; }
                double sum = 0; foreach (var v in kv.Value.Values) sum += v;
                if (sum <= Eps) continue;
                sb.Append(first ? "" : ",").Append(kv.Key).Append('=');
                first = false;
                bool f2 = true;
                foreach (var o in kv.Value)
                {
                    if (o.Value <= Eps || BadKey(o.Key)) continue;
                    sb.Append(f2 ? "" : ";").Append(o.Key).Append(':').Append(N3(o.Value));
                    f2 = false;
                }
            }
            first = true;
            sb.Append("|rg:");
            foreach (var kv in _rg)
            {
                if (BadKey(kv.Key)) { _badIds++; continue; }
                var r = kv.Value;
                sb.Append(first ? "" : ",").Append(kv.Key).Append('=')
                  .Append(N3(r.DomRef)).Append(';').Append(N3(r.SRef)).Append(';').Append(N3(r.JRef)).Append(';').Append(N3(r.LRef)).Append(';')
                  .Append(N3(r.Fallen)).Append(';').Append(N3(r.FallenMen)).Append(';').Append(N3(r.Civil)).Append(';').Append(N3(r.Born)).Append(';')
                  .Append(N3(r.Enslaved)).Append(';').Append(N3(r.ToWall)).Append(';').Append(N3(r.PoolRef)).Append(';').Append(N3(r.RefRef)).Append(';').Append(N3(r.CartRef))
                  .Append(';').Append(N3(r.TownOut)).Append(';').Append(N3(r.VillOut)).Append(';').Append(N3(r.TownBack)).Append(';').Append(N3(r.Drift))
                  .Append(';').Append(N3(r.MigIn)).Append(';').Append(N3(r.MigOut))        // E4/8 (pola 17, 18; zapis bez nich - 0)
                  .Append(';').Append(N3(r.TRef)).Append(';').Append(N3(r.WRef)).Append(';').Append(N3(r.TDead));     // E4/9 (pola 19-21; zapis bez nich - 0)
                first = false;
            }
        }

        /// <summary>Import: jedna sekcja E4/7 ("sw:", "so:", "jo:", "wo:", "rg:"); false = nie nasza. so/jo - odlozone do ResolveOrigin (partii w SyncData jeszcze nie ma).</summary>
        private static bool ImportOriginPart(string part)
        {
            string p2 = part.Substring(0, 3);
            if (p2 != "sw:" && p2 != "so:" && p2 != "jo:" && p2 != "wo:" && p2 != "rg:") return false;
            try
            {
                string body = part.Substring(3);
                if (p2 == "sw:")
                {
                    var a = body.Split(';');
                    int d; if (a.Length > 0 && int.TryParse(a[0], NumberStyles.Integer, Inv, out d)) { _sown = true; _sowDay = d; }
                }
                else if (p2 == "so:") _pendSo = body;
                else if (p2 == "jo:") _pendJo = body;
                else if (p2 == "wo:")
                {
                    foreach (var item in body.Split(','))
                    {
                        int eq = item.IndexOf('=');
                        if (eq <= 0) continue;
                        var d = new Dictionary<string, double>();
                        foreach (var kv in item.Substring(eq + 1).Split(';'))
                        {
                            int c = kv.LastIndexOf(':');
                            if (c <= 0) continue;
                            double n; if (!double.TryParse(kv.Substring(c + 1), NumberStyles.Float, Inv, out n) || !(n > 0.0) || double.IsInfinity(n)) continue;
                            d[kv.Substring(0, c)] = n;
                        }
                        if (d.Count > 0) _poolOrg[item.Substring(0, eq)] = d;
                    }
                }
                else
                {
                    foreach (var item in body.Split(','))
                    {
                        int eq = item.IndexOf('=');
                        if (eq <= 0) continue;
                        var a = item.Substring(eq + 1).Split(';');
                        var r = new RegRef
                        {
                            DomRef = Num(a, 0), SRef = Num(a, 1), JRef = Num(a, 2), LRef = Num(a, 3), Fallen = Num(a, 4), FallenMen = Num(a, 5), Civil = Num(a, 6),
                            Born = SNum(a, 7), Enslaved = Num(a, 8), ToWall = Num(a, 9), PoolRef = Num(a, 10), RefRef = Num(a, 11), CartRef = Num(a, 12),
                            TownOut = Num(a, 13), VillOut = Num(a, 14), TownBack = Num(a, 15), Drift = SNum(a, 16),     // recenzja E4/7 (R1-1, R2-3); zapis bez nich - 0
                            MigIn = Num(a, 17), MigOut = Num(a, 18),                                                   // E4/8 (R19); zapis E4/7 bez nich - 0
                            TRef = Num(a, 19), WRef = Num(a, 20), TDead = Num(a, 21)                                   // E4/9 (Z10); zapis bez nich - 0 (Num poza dlugoscia - 0)
                        };
                        _rg[item.Substring(0, eq)] = r;
                    }
                }
            }
            catch (Exception e) { OStumble("ImportOriginPart(" + p2 + ")", e); }
            return true;
        }

        /// <summary>Liczba ze znakiem (narodziny netto moga byc ujemne - przyrost naturalny E4/4 w wojnie).</summary>
        private static double SNum(string[] a, int i)
        {
            double x;
            if (a == null || i >= a.Length || !double.TryParse(a[i], NumberStyles.Float, Inv, out x)) return 0.0;
            return double.IsNaN(x) || double.IsInfinity(x) ? 0.0 : x;
        }

        /// <summary>OnSessionLaunched (obok RecruitKit.ResolvePending): slowniki so:/jo: z zapisu do partii i osad (w SyncData partii jeszcze nie ma).</summary>
        internal static void ResolveOrigin(string how)
        {
            if (_pendSo != null || _pendJo != null) ResolveDicts(how);
            // cache doby od razu po wczytaniu (hamulec poboru, plon, menu) - bez czekania na pierwsza dobe sesji
            try { if (_sown && OriginOn) { BuildViews(); FreeHands.Update(_view); } } catch (Exception e) { OStumble("ResolveOrigin(cache)", e); }
        }

        private static void ResolveDicts(string how)
        {
            try
            {
                var byId = new Dictionary<string, MobileParty>();
                foreach (var mp in MobileParty.All) if (mp != null && mp.StringId != null) byId[mp.StringId] = mp;
                int found = 0, missing = 0, dungeons = 0;
                Action<string, Dictionary<PartyBase, Org>, bool> load = (body, dst, prison) =>
                {
                    if (string.IsNullOrEmpty(body)) return;
                    foreach (var item in body.Split(','))
                    {
                        int eq = item.IndexOf('=');
                        if (eq <= 0) continue;
                        string id = item.Substring(0, eq);
                        PartyBase pb = null;
                        if (id[0] == '@') { var st = Settlement.Find(id.Substring(1)); pb = st != null ? st.Party : null; if (pb != null) dungeons++; }
                        else { MobileParty mp; if (byId.TryGetValue(id, out mp)) pb = mp.Party; }
                        if (pb == null) { missing++; continue; }
                        var o = new Org();
                        foreach (var kv in item.Substring(eq + 1).Split(';'))
                        {
                            int c = kv.LastIndexOf(':');
                            if (c <= 0) continue;
                            double n; if (!double.TryParse(kv.Substring(c + 1), NumberStyles.Float, Inv, out n) || !(n > 0.0) || double.IsInfinity(n)) continue;
                            string k = kv.Substring(0, c); if (k == "~") k = "";
                            double v; o.M.TryGetValue(k, out v); o.M[k] = v + n; o.Sum += n;
                        }
                        if (o.Sum > Eps) { dst[pb] = o; found++; }
                    }
                };
                load(_pendSo, _mem, false);
                load(_pendJo, _pris, true);
                _resolveFound = found; _resolveMissing = missing;
                double pc = found + missing > 0 ? 100.0 * missing / (found + missing) : 0.0;
                _resolveNote = how + ": z zapisu " + found + " slownikow (" + _mem.Count + " partii, " + _pris.Count + " lochow, w tym lochy osad " + dungeons + "), regionow " + _rg.Count
                               + "; nie znaleziono " + missing + " (" + pc.ToString("0.#", Inv) + "% - porzucone)";
                Log.Info("Pochodzenie (E4/7): " + _resolveNote + "; siew dnia " + _sowDay + ".");
            }
            catch (Exception e) { OStumble("ResolveOrigin", e); }
            _pendSo = _pendJo = null;
        }

        // ================================================================ pomocnicze
        private static PartyBase OwnerOf(TroopRoster r)
        {
            if (r == null) return null;
            if (!_ownerTried)
            {
                _ownerTried = true;
                try
                {
                    var m = AccessTools.PropertyGetter(typeof(TroopRoster), "OwnerParty");
                    if (m != null) _owner = (Func<TroopRoster, PartyBase>)Delegate.CreateDelegate(typeof(Func<TroopRoster, PartyBase>), m);
                }
                catch (Exception e) { OStumble("OwnerOf(init)", e); }
            }
            try { return _owner != null ? _owner(r) : null; } catch { return null; }
        }

        internal static bool IsWight(CharacterObject c)
        {
            if (c == null) return false;
            bool w;
            if (_wight.TryGetValue(c, out w)) return w;
            w = Undead.Character(c);
            _wight[c] = w;
            return w;
        }

        /// <summary>Szeregowi rosteru bez bohaterow i bez wightow (jedna miara ksiegi pochodzenia).</summary>
        internal static int Men(TroopRoster r)
        {
            if (r == null) return 0;
            int n = 0;
            try
            {
                for (int i = 0; i < r.Count; i++)
                {
                    var c = r.GetCharacterAtIndex(i);
                    if (c == null || c.IsHero || IsWight(c)) continue;
                    int k = r.GetElementNumber(i);
                    if (k > 0) n += k;
                }
            }
            catch (Exception e) { OStumble("Men", e); }
            return n;
        }

        internal static int MenOf(MobileParty mp) { return mp != null ? Men(mp.MemberRoster) : 0; }

        private static bool IsBkPatrol(MobileParty mp)
        {
            try { var c = mp.PartyComponent; return c != null && c.GetType().FullName == "BannerKings.Components.GarrisonPartyComponent"; } catch { return false; }
        }

        private static bool IsBkConvoy(MobileParty mp)
        {
            try { var c = mp.PartyComponent; return c != null && c.GetType().Name == "PopulationPartyComponent"; } catch { return false; }
        }

        /// <summary>Rodzaj partii dla skladu (doba w pamieci): KVillager/KConvoy/KUndead = bez slownika skladu.</summary>
        private static int KindOf(MobileParty mp)
        {
            if (mp == null) return KOther;
            int k;
            if (_kind.TryGetValue(mp, out k)) return k;
            try
            {
                if (mp.IsVillager) k = KVillager;
                // E4/8 (Z8): wedrowcy i konwoje BK ze slownikiem przy TravellersFromPeople; partia, ktora juz ma slownik, zostaje KRoad (wylaczenie w trakcie
                // nie gubi ludzi w drodze - wroca do DOM u celu)
                else if (IsBkConvoy(mp)) k = (RoadOn || (mp.Party != null && _mem.ContainsKey(mp.Party))) ? KRoad : KConvoy;
                else if (Undead.Party(mp)) k = KUndead;
                else if (mp.IsMainParty) k = KPlayer;
                else if (CrownGifts.IsWatchParty(mp)) k = KWatch;
                else if (mp.IsGarrison) k = KGarrison;
                else if (mp.IsMilitia) k = KMilitia;
                else if (mp.IsCaravan) k = KCaravan;
                else if (OutlawLaw.IsOutlawParty(mp)) k = KBand;
                else if (IsBkPatrol(mp)) k = KPatrolBk;
                else if (mp.IsPatrolParty) k = KPatrolGame;
                else if (mp.IsLordParty) k = KLord;
                else k = KOther;
            }
            catch { k = KOther; }
            _kind[mp] = k;
            return k;
        }

        private static string RegKey(Settlement region) { return region != null && region.StringId != null ? region.StringId : ""; }

        private static Settlement Node(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Settlement s;
            if (_node.TryGetValue(id, out s)) return s;
            try { s = Settlement.Find(id); } catch { s = null; }
            _node[id] = s;
            return s;
        }

        private static RegRef Rg(string id)
        {
            RegRef r;
            if (!_rg.TryGetValue(id, out r)) { r = new RegRef(); _rg[id] = r; }
            return r;
        }

        /// <summary>Slownik rosteru: sklad albo loch wlasciciela (create - zaloz pusty, gdy brak). null = roster bez wlasciciela albo bez slownika (TABOR, konwoj, Inni).</summary>
        private static Org OrgOf(TroopRoster r, bool create, out PartyBase owner, out int kind)
        {
            owner = OwnerOf(r); kind = KOther;
            if (owner == null) return null;
            Dictionary<PartyBase, Org> dict;
            if (ReferenceEquals(r, owner.PrisonRoster)) { dict = _pris; kind = owner.IsMobile ? KPrison : KDungeon; }
            else if (ReferenceEquals(r, owner.MemberRoster))
            {
                if (!owner.IsMobile) { kind = KOther; dict = _mem; }
                else
                {
                    kind = KindOf(owner.MobileParty);
                    if (kind < 0) return null;
                    dict = _mem;
                }
            }
            else return null;
            Org o;
            if (dict.TryGetValue(owner, out o)) return o;
            if (!create) return null;
            o = new Org(); dict[owner] = o;
            return o;
        }

        private static void OrgAdd(Org o, string k, double n) { double v; o.M.TryGetValue(k, out v); o.M[k] = v + n; o.Sum += n; }

        // ================================================================ jedna droga ruchu udzialow (U24: nigdy nie rzuca)
        /// <summary>Zdejmuje n ludzi proporcjonalnie ze slownika rosteru (sklad albo loch - po wlascicielu). Brak slownika / przed siewem / TABOR: udzialy "" (TABOR -
        /// region wsi taboru, ludzie zdjeci z DOM przy wyjsciu taboru). Nie patrzy na sam roster - kolejnosc wzgledem zmiany rosteru bez znaczenia.</summary>
        internal static Shares Take(TroopRoster r, double n, int cause)
        {
            if (!(n > 0.0) || double.IsInfinity(n)) return new Shares();
            if (!OriginLive || r == null) return Shares.None(n);
            try
            {
                PartyBase owner; int kind;
                var o = OrgOf(r, false, out owner, out kind);
                if (o == null)
                {
                    // TABOR: wiesniacy maja pochodzenie z definicji - region swojej wsi (bez slownika, bez stanu konta)
                    if (owner != null && owner.IsMobile && owner.MobileParty != null && owner.MobileParty.IsVillager && ReferenceEquals(r, owner.MemberRoster))
                    {
                        var home = owner.MobileParty.HomeSettlement;
                        var s = new Shares(); s.Add(RegKey(home != null ? OutlawLaw.RegionFor(home) : null), n);
                        return s;
                    }
                    var none = Shares.None(n);
                    if (WatchServes(owner, kind)) none.W = n;      // E4/9: roster Strazy bez slownika (nowa partia przed uzgodnieniem) - tez sluzba dozywotnia
                    return none;
                }
                var res = new Shares();
                if (o.Sum <= Eps) { res.Add("", n); if (WatchServes(owner, kind)) res.W = n; return res; }
                double take = Math.Min(n, o.Sum), f = take / o.Sum;
                if (f >= 1.0 - 1e-12)
                {
                    foreach (var kv in o.M) res.Add(kv.Key, kv.Value);
                    o.M.Clear(); o.Sum = 0.0;
                }
                else
                {
                    foreach (var k in o.M.Keys.ToList())
                    {
                        double m = o.M[k] * f;
                        res.Add(k, m);
                        double left = o.M[k] - m;
                        if (left > Eps) o.M[k] = left; else o.M.Remove(k);
                    }
                    o.Sum = 0.0; foreach (var v in o.M.Values) o.Sum += v;
                }
                if (cause >= 0 && cause < FKinds) _fOut[cause] += take;
                _tAll += take;
                if (n - take > Eps) res.Add("", n - take);       // ponad slownik (nowa partia przed uzgodnieniem) - bez pochodzenia, poza stanem
                // E4/9 (4.4, U5): ludzie zdjeci ze sluzby Strazy (partia, zaloga, gracz w Strazy, milicja osady Strazy - WatchServes) - znacznik dozywotniej sluzby
                try
                {
                    if (WatchServes(owner, kind)) res.W = res.Total;
                    if (kind == KWatch) WatchOut(take, cause);
                }
                catch (Exception e) { OStumble("Take(E4/9)", e); }
                return res;
            }
            catch (Exception e) { OStumble("Take", e); return Shares.None(n); }
        }

        /// <summary>Dopisuje udzialy do slownika rosteru (przenosiny, rekrut). Roster bez slownika (TABOR, konwoj BK, Inni, lup gracza PlayerEncounter) - licznik "do rosteru bez ksiegi".</summary>
        internal static void Give(TroopRoster r, Shares s, int cause)
        {
            if (s == null || !(s.Total > Eps) || !OriginLive) return;
            try
            {
                PartyBase owner; int kind = KOther;
                var o = r != null ? OrgOf(r, true, out owner, out kind) : null;
                if (o == null) { _dest[ODrop] += s.Total; return; }
                foreach (var kv in s.M) if (kv.Value > 0.0) OrgAdd(o, kv.Key, kv.Value);
                if (cause >= 0 && cause < FKinds) _fIn[cause] += s.Total;
                _gAll += s.Total;
                if (kind == KWatch) _wIn += s.Total;             // E4/9: kontrola linii "Straz z Westeros" (stan slownikow Strazy)
            }
            catch (Exception e) { OStumble("Give", e); }
        }

        /// <summary>Rekrut ze znanego regionu (zdjety z DOM tego regionu).</summary>
        internal static void GiveNew(TroopRoster r, Settlement region, double n, int cause)
        {
            if (!(n > 0.0) || !OriginLive) return;
            var s = new Shares(); s.Add(RegKey(region), n);
            Give(r, s, cause);
        }

        /// <summary>Koszyk "" z nazwana przyczyna (ochotnik z mapy, Straz, pobor na podlodze, ksiega wylaczona).</summary>
        internal static void GiveNone(TroopRoster r, double n, int cause)
        {
            if (!(n > 0.0) || !OriginLive) return;
            Give(r, Shares.None(n), cause);
        }

        /// <summary>
        /// Recenzja E4/7 R1-5: pobor wymuszony gracza (PeopleUnit.PressPostfix, po Move z DOM wsi). Gra nie dopisuje rekrutow do druzyny, tylko otwiera ekran lupu
        /// (village_force_volunteers_ended_successfully_on_consequence -> PartyScreenHelper.OpenScreenAsLoot) - gracz bierze ilu chce. Udzialy czekaja na ScreenPost
        /// tego ekranu (do wysokosci przyrostu skladu gracza - PressTake); reszta - niewzieci. Nieodebrane do nastepnej doby ksiegi - tez niewzieci (OriginDaily).
        /// E4/7b (decyzja Jeffa 11.10 12:40, "nikt nie znika"): niewzieci wracaja do DOM wsi, z ktorej ich zdjeto (PressUntaken; dotad - nazwana nicosc poza rosterem).
        /// </summary>
        internal static void PressPending(Village v, Settlement region, double men)
        {
            if (!(men > 0.0) || double.IsInfinity(men) || !OriginLive) return;
            try
            {
                if (_press == null) _press = new Shares();
                _press.Add(RegKey(region), men);
                _pressIn += men;
                if (v != null)
                {
                    if (_pressVil == null) _pressVil = new Dictionary<Village, double>();
                    double was; _pressVil.TryGetValue(v, out was); _pressVil[v] = was + men;
                }
            }
            catch (Exception e) { OStumble("PressPending", e); }
        }

        /// <summary>ScreenPost: z oczekujacych udzialow poboru wymuszonego do skladu gracza najwyzej `gain` ludzi (FPress); reszta - niewzieci (PressUntaken). Zwraca, ilu dopisano.</summary>
        private static double PressTake(TroopRoster rm, double gain)
        {
            var p = _press; _press = null;
            if (p == null || !(p.Total > Eps)) { _pressVil = null; return 0.0; }
            double use = Math.Min(Math.Max(0.0, gain), p.Total);
            if (use > Eps) Give(rm, p.Cut(use), FPress);
            PressUntaken(p.Total);
            return use;
        }

        /// <summary>
        /// E4/7b (decyzja Jeffa 11.10 12:40): `rest` niewzietych z poboru wymuszonego gracza wraca do DOM wsi, z ktorych ich zdjeto - proporcjonalnie do zdjetych
        /// z kazdej wsi (_pressVil). Ta sama droga co powrot z pochodzeniem (HomeKnown): PeopleBook.Move SLUZBA -> DOM z przyczyna MBack* (MBackPress, MHearth,
        /// stawka GamePerMan - ta sama co przy zdjeciu w PeopleUnit.PressPostfix), ale do wsi zdjecia zamiast najslabszej wsi regionu i bez czesci do stanu
        /// miasta (zdjeto ich z hearth tej wsi, nie ze stanu miasta). Bez warunku LevyLive - zdjecie (PressPostfix, E4/3) tez go nie ma; oczekujace sa tylko po
        /// zdjeciu przez Move (jednostka czynna). Wylacznik Press Untaken Go Home - przy wylaczonym jak dotad: niewzieci poza rosterem (_pressLost, nazwani).
        /// Zuzywa _pressVil. Nie rzuca (U24).
        /// </summary>
        private static void PressUntaken(double rest)
        {
            var vil = _pressVil; _pressVil = null;
            if (!(rest > Eps)) return;
            double home = 0.0;
            try
            {
                var st = Settings.Current;
                if (st != null && st.PressUntakenGoHome && vil != null && vil.Count > 0)
                {
                    double all = 0.0; foreach (var x in vil.Values) all += x;
                    double f = all > Eps ? Math.Min(1.0, rest / all) : 0.0;
                    foreach (var kv in vil)
                    {
                        double m = kv.Value * f;
                        if (kv.Key == null || !(m > Eps)) continue;
                        Move(kv.Key, AService, ADom, m, MBackPress, PeopleUnit.GamePerMan);
                        home += m;
                    }
                }
            }
            catch (Exception e) { OStumble("PressUntaken", e); }
            _pressHome += home;
            if (rest - home > Eps) _pressLost += rest - home;
        }

        /// <summary>Cofniecie Take (ludzie zostali w rosterze - np. wies nie przyjela zwolnionego): bez licznika ruchu.</summary>
        internal static void Untake(TroopRoster r, Shares s, int cause)
        {
            if (s == null || !(s.Total > Eps) || !OriginLive) return;
            try
            {
                PartyBase owner; int kind = KOther;
                var o = r != null ? OrgOf(r, true, out owner, out kind) : null;
                if (o == null) return;
                double known = 0.0;
                foreach (var kv in s.M) if (kv.Value > 0.0) { OrgAdd(o, kv.Key, kv.Value); known += kv.Value; }
                if (cause >= 0 && cause < FKinds) _fOut[cause] = Math.Max(0.0, _fOut[cause] - known);
                _tAll -= known;
                if (kind == KWatch) WatchOut(-known, cause);     // E4/9: cofniecie zdjecia ze slownika Strazy
            }
            catch (Exception e) { OStumble("Untake", e); }
        }

        /// <summary>Udzialy dla rosteru, ktorego jeszcze nie ma (narodziny bandy z puli - OutlawLaw.RosterPostfix): pierwsze dodatnie uzgodnienie je zjada.</summary>
        internal static void Pending(MobileParty mp, Shares s)
        {
            if (mp == null || s == null || !(s.Total > Eps) || !OriginLive) return;
            try
            {
                Shares p;
                if (_pending.TryGetValue(mp, out p)) p.AddAll(s); else _pending[mp] = s;
            }
            catch (Exception e) { OStumble("Pending", e); }
        }

        /// <summary>Udzialy, ktore zeszly z rosteru i nie maja dokad (nazwana nicosc: rozbici bez H3 i bez puli, uwolnieni bez domu...).</summary>
        internal static void Lost(Shares s) { if (s != null && s.Total > Eps) _dest[OLost] += s.Total; }

        /// <summary>N7: udzialy rozbitych, ktore poszly do zbiorki pod choragiew (StragglersRally trzyma je przy grupie do odbioru albo rozejscia) - licznik "dokad".</summary>
        internal static void RallyShares(Shares s) { if (s != null && s.Total > Eps && OriginLive) _dest[ORally] += s.Total; }

        // ================================================================ dokad
        /// <summary>R3: POLEGLI regionu pochodzenia (trwaly ubytek; DOM juz go nie mial od werbunku); "" - licznik.</summary>
        internal static void DieShares(Shares s, int cause)
        {
            if (s == null || !(s.Total > Eps) || !OriginLive) return;
            try
            {
                foreach (var kv in s.M)
                {
                    if (kv.Key.Length == 0 || !(kv.Value > 0.0)) continue;
                    var r = Rg(kv.Key);
                    r.Fallen += kv.Value; r.FallenMen += kv.Value;
                }
                _dest[ODead] += s.Total;
                if (cause >= 0 && cause < FKinds) _deadBy[cause] += s.Total;
                if (s.W > Eps) WatchDied(s);                     // E4/9: polegli bracia (Westeros / bez pochodzenia) - linia "Straz z Westeros"
            }
            catch (Exception e) { OStumble("DieShares", e); }
        }

        /// <summary>Polegli wiesniacy taborow (konto TABOR - bez slownika): POLEGLI regionu ich wsi.</summary>
        private static void DieCarts(MobileParty mp, int dead)
        {
            if (dead <= 0) return;
            var home = mp.HomeSettlement;
            string k = RegKey(home != null ? OutlawLaw.RegionFor(home) : null);
            if (k.Length > 0) { var r = Rg(k); r.Fallen += dead; r.FallenMen += dead; }
            _deadCarts += dead;
        }

        /// <summary>
        /// Czesc znana udzialow -> DOM regionu pochodzenia (najslabsza wies regionu, warownia bez wsi - najblizszy region z wsiami: OutlawLaw.WeakestVillage),
        /// PeopleBook.Move z hearth (cause - MBack*, MHearth = true; stawka 1/k jak przy poborze). Zdejmuje umieszczonych z `s` (w s zostaje "" i nieumieszczeni).
        /// Recenzja E4/7 R1-1: region-miasto ze stanem E4/4 dostaje z powrotem czesc do stanu miasta (MBackTown, bez hearth) - udzial miasta w poborze regionu
        /// od siewu (TownOut / (TownOut + VillOut)), nie wiecej niz dlug miasta (TownOut - TownBack); reszta do wsi jak dotad. Bez tego pobor z miasta byl
        /// jednokierunkowy (zrodlo miasto, ujscie wies) - stan miasta tylko malal.
        /// Zwraca, ilu umiescil. Gdy !LevyLive - 0 (wolajacy liczy jak w E4/3: Note bez hearth).
        /// E4/9 (4.4, U5 - sluzba dozywotnia): czesc zdjeta ze sluzby Strazy (s.W) przy czynnym WatchLaw idzie najpierw na konto MUR (z "" wlacznie, bez hearth) -
        /// wliczona w zwracanych "umieszczonych"; reszta jak dotad.
        /// </summary>
        internal static double HomeKnown(Shares s, int cause)
        {
            if (s == null || cause < 0 || cause >= MKinds) return 0.0;
            double wall = 0.0;
            if (s.W > Eps) { try { wall = WatchHome(s, cause, false); } catch (Exception e) { OStumble("HomeKnown(Mur)", e); } }
            if (!(s.Known > Eps) || !LevyLive) return wall;
            return wall + HomeKnownBody(s, cause);
        }

        private static double HomeKnownBody(Shares s, int cause)
        {
            double placed = 0.0;
            try
            {
                foreach (var k in s.M.Keys.ToList())
                {
                    if (k.Length == 0) continue;
                    double m = s.M[k];
                    if (!(m > Eps)) continue;
                    var node = Node(k);
                    double tm = 0.0;
                    try { tm = BackToTown(node, k, m); } catch (Exception e) { OStumble("HomeKnown(miasto)", e); tm = 0.0; }
                    if (tm > 0.0) { placed += tm; _homeBy[cause] += tm; _homeTown += tm; m -= tm; }
                    if (!(m > Eps)) { s.M.Remove(k); continue; }
                    var v = OutlawLaw.WeakestVillage(node);
                    if (v == null) { _homeNoVillage += m; s.M[k] = m; continue; }
                    Move(v, AService, ADom, m, cause, PeopleUnit.GamePerMan);
                    placed += m; _homeBy[cause] += m;
                    s.M.Remove(k);
                }
                s.Total = 0.0; foreach (var x in s.M.Values) s.Total += x;
                _dest[OHomeKnown] += placed;
            }
            catch (Exception e) { OStumble("HomeKnown", e); }
            return placed;
        }

        /// <summary>R1-1: ilu z m powracajacych do regionu-miasta `node` wraca do stanu miasta E4/4 (dopisane do stanu, MBackTown, TownBack). 0 - region nie jest
        /// miastem ze stanem albo miasto nie ma dlugu poboru. Udzial = pobor ze stanu miasta / caly pobor regionu od siewu (ten sam podzial co przy zdjeciu).</summary>
        private static double BackToTown(Settlement node, string k, double m)
        {
            if (node == null || !node.IsTown || !(m > Eps)) return 0.0;
            RegRef r;
            if (!_rg.TryGetValue(k, out r)) return 0.0;
            double owed = r.TownOut - r.TownBack, all = r.TownOut + r.VillOut;
            if (!(owed > Eps) || !(all > Eps)) return 0.0;
            float p;
            if (!TryTown(node, out p)) return 0.0;                          // stan miast wylaczony (Town People Frozen) - wszyscy do wsi jak dotad
            double tm = Math.Min(m * r.TownOut / all, owed);
            if (!(tm > Eps)) return 0.0;
            // E4/8 N6 (diagnoza biegu zbiorczego 329f2c7): stan miast to float - ruch i licznik wedlug zmiany faktycznie zapisanej, nie zadanej (reszta wraca do wsi u wolajacego)
            double done;
            lock (Gate) { float np = TownRound(p, tm, out done); _town[node.StringId] = np; _townMv[MBackTown] += done; }     // E4/8: ruch stanu miasta wedlug przyczyny
            if (!(done > 0.0)) return 0.0;
            Count(done, MBackTown, 0.0);
            r.TownBack += done;
            return done;
        }

        /// <summary>Znana czesc -> HomeKnown (z hearth), reszta n -> Note(causeLegacy) jak w E4/3 (causeLegacy &lt; 0 - bez Note). Udzialy `s` zuzyte.</summary>
        internal static void HomeOrNote(Shares s, double n, int causeKnown, int causeLegacy)
        {
            if (!(n > 0.0)) return;
            double placed = 0.0;
            try { placed = HomeKnown(s, causeKnown); } catch (Exception e) { OStumble("HomeOrNote", e); }
            double rest = Math.Max(0.0, n - placed);
            if (rest > Eps)
            {
                if (causeLegacy >= 0) Note(rest, causeLegacy, 0.0);
                _dest[OHomeLegacy] += rest;
            }
        }

        /// <summary>Region puli -> udzial znanych w puli (wo:). "" nie idzie do slownika (jest tylko klucz puli OutlawLaw).</summary>
        internal static void PoolShares(Settlement poolRegion, Shares s)
        {
            if (s == null || !(s.Total > Eps) || !OriginLive) return;
            try
            {
                _dest[OPool] += s.Total;
                if (s.W > Eps) WatchToWoods(poolRegion, s.W);    // E4/9 (4.5): dezerter i rozbity "do lasu" ze Strazy - kontrola: pula Polnocy
                if (poolRegion == null || poolRegion.StringId == null || !(s.Known > Eps)) return;
                Dictionary<string, double> d;
                if (!_poolOrg.TryGetValue(poolRegion.StringId, out d)) { d = new Dictionary<string, double>(); _poolOrg[poolRegion.StringId] = d; }
                foreach (var kv in s.M) { if (kv.Key.Length == 0 || !(kv.Value > 0.0)) continue; double v; d.TryGetValue(kv.Key, out v); d[kv.Key] = v + kv.Value; }
            }
            catch (Exception e) { OStumble("PoolShares", e); }
        }

        /// <summary>n zolnierzy wychodzi z puli regionu (do domu, do bandy); troopsBefore - zolnierze puli tego regionu PRZED zdjeciem (OutlawLaw.PoolTroops).
        /// Udzial znanych proporcjonalnie (znani / zolnierze puli); reszta "".</summary>
        internal static Shares PoolTake(Settlement poolRegion, double n, double troopsBefore)
        {
            var res = new Shares();
            if (!(n > 0.0)) return res;
            if (!OriginLive || poolRegion == null || poolRegion.StringId == null) { res.Add("", n); return res; }
            try
            {
                Dictionary<string, double> d;
                if (_poolOrg.TryGetValue(poolRegion.StringId, out d) && d.Count > 0 && troopsBefore > Eps)
                {
                    double known = 0; foreach (var v in d.Values) known += v;
                    double f = Math.Min(1.0, n / Math.Max(troopsBefore, known));
                    foreach (var k in d.Keys.ToList())
                    {
                        double m = d[k] * f;
                        res.Add(k, m);
                        double left = d[k] - m;
                        if (left > Eps) d[k] = left; else d.Remove(k);
                    }
                    if (d.Count == 0) _poolOrg.Remove(poolRegion.StringId);
                }
                if (n - res.Total > Eps) res.Add("", n - res.Total);
            }
            catch (Exception e) { OStumble("PoolTake", e); res = Shares.None(n); }
            return res;
        }

        /// <summary>
        /// Jency do niewoli (BK niewolnicy/dzierzawcy osady, I1 Enslavement, patrol BK). E4/9 (R15, Z6): osada niewoli `hold` w krainie z niewola (biezaca kultura osady -
        /// LosersFlee.SlaveCulture, jak I1b) przy czynnym ThrallLaw - konto NIEWOLNI (region trzymania x region pochodzenia, z "" wlacznie); inaczej (prawo wylaczone,
        /// osada poza kanonem przy wylaczonym I1, brak osady) - jak w E4/7: licznik regionu Enslaved (odplyw). via: 0 = sprzedaz jencow, 1 = patrol BK.
        /// </summary>
        internal static void EnslaveShares(Shares s, Settlement hold, int via)
        {
            if (s == null || !(s.Total > Eps) || !OriginLive) return;
            try
            {
                _dest[OSlave] += s.Total;
                if (ThrallLaw.On && hold != null && LosersFlee.SlaveCulture(hold.Culture)) { EnslaveAt(OutlawLaw.RegionFor(hold), s, via); return; }
                foreach (var kv in s.M) if (kv.Key.Length > 0 && kv.Value > 0.0) Rg(kv.Key).Enslaved += kv.Value;
                ThrallOutside(s.Total);
            }
            catch (Exception e) { OStumble("EnslaveShares", e); }
        }

        /// <summary>Jency na Mur (I1, migracja do Strazy). E4/9 (4.3): przy czynnym WatchLaw - konto MUR ("przestepcy przyjmowani zawsze", z "" wlacznie, stan regionu
        /// pochodzenia - pierwsi w kolejce Levy); inaczej jak w E4/7 - licznik regionu ToWall (odplyw).</summary>
        internal static void WallShares(Shares s)
        {
            if (s == null || !(s.Total > Eps) || !OriginLive) return;
            try
            {
                _dest[OWall] += s.Total;
                if (WatchLaw.Live) { WallAdd(s); _wConvicts += s.Total; return; }
                foreach (var kv in s.M) if (kv.Key.Length > 0 && kv.Value > 0.0) Rg(kv.Key).ToWall += kv.Value;
            }
            catch (Exception e) { OStumble("WallShares", e); }
        }

        /// <summary>Przenosiny miedzy dwoma rosterami w oknie: przeszlo = min(ubytek zrodla, przyrost celu); udzialy idu z ludzmi. Zwraca, ilu przeniesiono.</summary>
        internal static double Transfer(TroopRoster from, int fromBefore, TroopRoster to, int toBefore, int cause)
        {
            if (!OriginLive || from == null || to == null) return 0.0;
            try
            {
                int lost = fromBefore - Men(from), got = Men(to) - toBefore;
                int t = Math.Min(lost, got);
                if (t <= 0) return 0.0;
                Give(to, Take(from, t, cause), cause);
                return t;
            }
            catch (Exception e) { OStumble("Transfer", e); return 0.0; }
        }

        // ================================================================ pobor z DOM (R4)
        /// <summary>
        /// Rekrut z osady s: DOM -n. Wies - PeopleBook.Move(MLevyVillage, 1/k osady) z podloga 50 hearth (jak wyrzutki - OutlawLaw.TakeFrom); miasto - stan miasta (E4/4,
        /// MLevyTown); zamek - wsie zamku wedlug ludzi (PeopleUnit.Base); brak - inne wsie regionu od najwiekszej. Zwraca, ilu zdjeto (reszta - "pobor na podlodze"),
        /// region = warownia regionu. Gdy !LevyLive - 0. causeM: MLevyVillage albo MLevyA5.
        /// </summary>
        internal static double TakeLevy(Settlement s, double n, int causeM, out Settlement region) { return TakeLevy(s, n, causeM, out region, MLevyTown); }

        /// <summary>E4/8: ten sam rachunek z przyczyna czesci ze stanu miasta (causeTown - przyczyna E4/8, zeby pobor E4/7 "ze stanu miast" liczyl tylko swoich).
        /// "Pobor na podlodze" (linia E4/7, prog T6) liczony tylko dla przyczyn E4/7 - niedobory E4/8 liczy linia "Wojsko z ludzi (E4/8)".</summary>
        internal static double TakeLevy(Settlement s, double n, int causeM, out Settlement region, int causeTown)
        {
            region = s != null ? OutlawLaw.RegionFor(s) : null;
            if (s == null || !(n > 0.0) || !LevyLive) return 0.0;
            double got = 0.0, fromTown = 0.0;
            try
            {
                if (s.IsTown && s.Town != null)
                {
                    // fix2 (decyzja Jeffa 12.10 nr 3, P1-4, wylacznik Town Levy By Share): pobor E4/7 (MLevyVillage, MLevyA5) bierze ze stanu miasta tylko
                    // n x p_miasta / (p_miasta + ludzie wsi regionu), reszte z wsi regionu wedlug ludzi; wsie na podlodze 50 hearth - reszta znow ze stanu miasta
                    // (liczba rekrutow bez zmian). Podzial dlugu miasta TownOut/VillOut nizej idzie za tym sam (BackToTown). E4/8 (milicja, druzyny, karawany,
                    // wedrowcy) bez zmian - ich powrot idzie do osady wyjscia (ArriveAt -> TownShift), pobor z wsi i powrot do miasta bylby sztuczna migracja
                    double nTown = n;
                    if ((causeM == MLevyVillage || causeM == MLevyA5) && TownLevyByShareOn) nTown = n * TownShareOf(s, region);
                    bool share = nTown < n;
                    double t = FromTown(s, nTown, causeTown);
                    got += t; fromTown = t;
                    if (n - got > Eps)
                    {
                        double v = FromVillages(region, n - got, causeM, share);   // udzial - wsie regionu wedlug ludzi; dotad (stan miasta wylaczony / miasto na podlodze) od najwiekszej
                        got += v;
                        if (share) _townShareVill += v;
                    }
                    if (share && n - got > Eps)
                    {
                        double t2 = FromTown(s, n - got, causeTown);           // wsie regionu na podlodze - reszta ze stanu miasta jak dotad
                        got += t2; fromTown += t2; _townShareBack += t2;
                    }
                }
                else if (s.IsVillage && s.Village != null)
                {
                    got += FromVillage(s.Village, n, causeM);
                    if (n - got > Eps) got += FromVillages(region, n - got, causeM, false);
                    // fixA (T6, wylacznik Levy Village From Town): wszystkie wsie regionu na podlodze 50 hearth (dane ROT: Lotus Bay i Ifequeveron - wsie z hearth="50"
                    // w settlements.xml) - reszta ze stanu miasta regionu ta sama droga co galaz miasta (FromTown), zamiast "pobor na podlodze" bez pochodzenia.
                    // Tylko pobor E4/7 (MLevyVillage, MLevyA5) - ten sam warunek co R1-1 nizej (dlug miasta TownOut, podzial powrotow BackToTown) i licznik podlogi;
                    // E4/8 (milicja, druzyny, karawany, wedrowcy) wraca przez TownShift / ArriveAt do wsi i nie podnosi dlugu miasta (recenzja E4/8 R2-1) - bez zmian
                    if (n - got > Eps && (causeM == MLevyVillage || causeM == MLevyA5) && region != null && region.IsTown && LevyVillageFromTownOn)
                    {
                        double t = FromTown(region, n - got, causeTown);
                        if (t > Eps)
                        {
                            got += t; fromTown += t; _villTown += t;
                            if (_villTownBy == null) _villTownBy = new Dictionary<string, double>();
                            string nm = region.Name != null ? region.Name.ToString() : region.StringId;
                            double was; _villTownBy.TryGetValue(nm ?? "?", out was); _villTownBy[nm ?? "?"] = was + t;
                        }
                    }
                }
                else if (s.IsCastle) got += FromVillages(s, n, causeM, true);
            }
            catch (Exception e) { OStumble("TakeLevy", e); }
            try
            {
                // R1-1: pobor regionu od siewu - ze stanu miasta (dlug miasta; powrot - BackToTown) i z wsi (mianownik podzialu powrotu miasto/wsie).
                // Recenzja E4/8 R2-1: tylko pobor E4/7 (MLevyVillage, MLevyA5) - wyjscia E4/8 (milicja, druzyny, karawany, wedrowcy) maja wlasny powrot przez TownShift;
                // liczone tutaj zawyzalyby udzial miasta w powrotach i dlug miasta
                if (region != null && region.StringId != null && got > Eps && (causeM == MLevyVillage || causeM == MLevyA5))
                {
                    var rr = Rg(region.StringId);
                    rr.TownOut += fromTown;
                    if (got - fromTown > Eps) rr.VillOut += got - fromTown;
                }
            }
            catch (Exception e) { OStumble("TakeLevy(licznik)", e); }
            if (n - got > Eps && (causeM == MLevyVillage || causeM == MLevyA5)) { _floorCalls++; _floorMen += n - got; }
            return got;
        }

        private static bool LevyVillageFromTownOn { get { var s = Settings.Current; return s != null && s.LevyVillageFromTown; } }
        private static bool TownLevyByShareOn { get { var s = Settings.Current; return s != null && s.TownLevyByShare; } }

        /// <summary>fix2 (P1-4): udzial miasta w ludnosci krainy p_miasta / (p_miasta + ludzie wsi regionu) - stan miasta E4/4 i PopulationLaw.PeopleOf wsi
        /// (jak DomOf). Brak stanu miasta albo wsi z ludzmi - 1 (pobor jak dotad).</summary>
        private static double TownShareOf(Settlement town, Settlement region)
        {
            float p;
            if (!TryTown(town, out p) || !(p > 0f)) return 1.0;
            double vill = 0.0;
            if (region != null && region.BoundVillages != null)
                foreach (var v in region.BoundVillages) if (v != null && v.Settlement != null) vill += PopulationLaw.PeopleOf(v.Settlement);
            if (!(vill > 0.0) || double.IsInfinity(vill)) return 1.0;
            double sh = p / (p + vill);
            return sh > 0.0 && sh < 1.0 ? sh : 1.0;
        }

        /// <summary>fixA (T6): czesc poboru ze stanu miasta E4/4 (miasto nie schodzi ponizej 100 ludzi) - jedna droga dla galezi miasta i dla odwrotu wsi na podlodze.
        /// Ruch stanu miasta wedlug przyczyny (E4/8 _townMv) i licznik ruchu DOM (Count). Zwraca, ilu zdjeto (0 - stan miast wylaczony albo miasto na podlodze).</summary>
        private static double FromTown(Settlement town, double n, int causeTown)
        {
            if (town == null || !town.IsTown || town.Town == null || !(n > 0.0)) return 0.0;
            float p;
            if (!TryTown(town, out p)) return 0.0;
            double t = Math.Min(n, Math.Max(0.0, p - 100.0));    // miasto nie schodzi ponizej 100 ludzi
            if (!(t > 0.0)) return 0.0;
            // E4/8 N6: ruch i licznik wedlug zmiany faktycznie zapisanej w float (reszta poboru idzie dalej do wsi u wolajacego)
            double done;
            lock (Gate) { float np = TownRound(p, -t, out done); done = -done; _town[town.StringId] = np; _townMv[causeTown] -= done; }     // E4/8: ruch stanu miasta wedlug przyczyny
            if (!(done > 0.0)) return 0.0;
            Count(done, causeTown, 0.0);
            return done;
        }

        /// <summary>E4/8 N6 (diagnoza biegu zbiorczego 329f2c7, kontrola linii Miasta): nowy stan miasta (float) po zmianie o delta i zmiana faktycznie zapisana
        /// (done, ze znakiem delta albo 0). Zaokraglenie float poza zadana kwote cofniete o jeden krok float w strone p - |done| &lt;= |delta|, wiec wolajacy nigdy nie
        /// rozda wiecej ludzi, niz zeszlo ze stanu. Ruch mniejszy niz pol kroku float nie zmienia stanu (done 0). Stan nie spada ponizej 0.</summary>
        internal static float TownRound(float p, double delta, out double done)
        {
            float np = (float)((double)p + delta);
            if (np < 0f) np = 0f;
            done = (double)np - p;
            if (Math.Abs(done) > Math.Abs(delta) && np != p)
            {
                int b = BitConverter.ToInt32(BitConverter.GetBytes(np), 0);
                b += np < p ? 1 : -1;                                       // dodatnie floaty: o jeden krok w strone p
                float nb = BitConverter.ToSingle(BitConverter.GetBytes(b), 0);
                if (nb >= 0f && !float.IsNaN(nb)) { np = nb; done = (double)np - p; }
                if (Math.Abs(done) > Math.Abs(delta)) { np = p; done = 0.0; }    // zabezpieczenie - nic nie zapisujemy
            }
            return np;
        }

        private static double FromVillage(Village v, double n, int causeM)
        {
            if (v == null || !(n > 0.0)) return 0.0;
            bool unit;
            float per = PeopleUnit.PerMan(v, PeopleUnit.GamePerMan, out unit);
            if (!unit || !(per > 0f)) return 0.0;                         // wies bez przelicznika - nie zdejmujemy po stawce gry
            double can = Math.Max(0.0, (v.Hearth - 50.0) / per);          // wies nie znika do zera (podloga 50 hearth - jak wyrzutki)
            double t = Math.Min(can, n);
            if (t <= Eps) return 0.0;
            double h = Move(v, ADom, AService, t, causeM, PeopleUnit.GamePerMan);
            return h < 0.0 ? t : 0.0;
        }

        /// <summary>Wsie regionu: weighted - wedlug ludzi (PeopleUnit.Base), inaczej od najwiekszej.</summary>
        private static double FromVillages(Settlement region, double n, int causeM, bool weighted)
        {
            if (region == null || !(n > 0.0) || region.BoundVillages == null || region.BoundVillages.Count == 0) return 0.0;
            double got = 0.0;
            if (weighted)
            {
                double all = 0.0;
                foreach (var v in region.BoundVillages) all += PeopleUnit.Base(v);
                if (all > 0.0)
                {
                    foreach (var v in region.BoundVillages)
                    {
                        double share = PeopleUnit.Base(v) / all;
                        if (share > 0.0) got += FromVillage(v, n * share, causeM);
                    }
                }
            }
            if (n - got > Eps)
                foreach (var v in region.BoundVillages.OrderByDescending(x => x.Hearth))
                {
                    if (n - got <= Eps) break;
                    got += FromVillage(v, n - got, causeM);
                }
            return got;
        }

        // ================================================================ cywile i narodziny (R5: K_m)
        /// <summary>Zmarli cywile regionu (spustoszenie E4/5, uchodzcy) - K_m = ... + 0.27 x cywile. Od siewu. Recenzja E4/7 R1-6 (scalanie z E4/6): TYLKO zgony zdjete
        /// z DOM przez PeopleBook.Move poza modelem hearth (jak MDevRaid E4/5). Czlon glodu modelu przyrostu E4/4 (g.Hunger w HearthChange) jest juz
        /// w narodzinach netto (NoteBorn) - tu go nie ma. Scalenie E4/6 + E4/7: zgony z glodu E4/6 ida przez Move (MFamineDead, Famine.cs) i TownDie (MFamineTown)
        /// poza modelem - wolane tu raz (inaczej rownanie krain gubiloby ich "w nicosc"); zmarli uchodzcy z glodu - przez RefugeesDie.</summary>
        internal static void NoteCivilDead(Settlement region, double n)
        {
            if (region == null || !(n > 0.0) || !_sown) return;
            NoteCivilDeadId(region.StringId, n);
        }

        internal static void NoteCivilDeadId(string regionId, double n)
        {
            if (string.IsNullOrEmpty(regionId) || !(n > 0.0) || double.IsInfinity(n) || !_sown) return;
            try { Rg(regionId).Civil += n; } catch (Exception e) { OStumble("NoteCivilDead", e); }
        }

        /// <summary>D2: przyrost naturalny wsi (E4/4, ludzie netto - moze byc ujemny) - K_m = ... - 0.27 x narodziny. Wola PopulationLaw.GrowthDaily (raz na dobe, kazda wies raz).</summary>
        internal static void NoteBorn(Village v, double men)
        {
            if (v == null || double.IsNaN(men) || double.IsInfinity(men) || men == 0.0 || !_sown) return;
            try
            {
                var region = v.Settlement != null ? OutlawLaw.RegionFor(v.Settlement) : null;
                if (region == null || region.StringId == null) return;
                Rg(region.StringId).Born += men;
            }
            catch (Exception e) { OStumble("NoteBorn", e); }
        }

        // ================================================================ smierc w bitwie (R3) - CampaignEvents.MapEventEnded (przed usunieciem partii)
        /// <summary>
        /// Polegli kazdej partii (DiedInBattle bez wightow; H3 poprawil listy w ResultsPrefix) - POLEGLI regionu pochodzenia; tabory wsi - region ich wsi.
        /// Rozbici: przy H3 (OutlawLaw.RoutedH3Body) i przy prawie wyrzutkow (OutlawLaw.OnMapEventEnded) - tam; bez obu znikaja (dzis tez) - nicosc nazwana.
        /// </summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            if (me == null || !OriginLive) return;
            bool routedElsewhere = LosersFlee.On || (OutlawLaw.On && Settings.Current != null && Settings.Current.OutlawRoutedShare > 0f);
            foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
            {
                if (side == null) continue;
                foreach (var mep in side.Parties)
                {
                    try
                    {
                        if (mep == null || mep.Party == null) continue;
                        var pb = mep.Party; var mp = pb.MobileParty;
                        int dead = Men(mep.DiedInBattle);
                        if (dead > 0)
                        {
                            if (mp != null && mp.IsVillager) DieCarts(mp, dead);
                            else DieShares(Take(pb.MemberRoster, dead, FDieBattle), FDieBattle);
                        }
                        if (!routedElsewhere && (mp == null || !mp.IsVillager))
                        {
                            int routed = Men(mep.RoutedInBattle);
                            if (routed > 0) Lost(Take(pb.MemberRoster, routed, FRouted));
                        }
                    }
                    catch (Exception e) { OStumble("OnMapEventEnded", e); }
                }
            }
        }

        /// <summary>Koniec partii (MobilePartyDestroyed / OnPartyRemoved): resztka slownika skladu i lochu - w nicosc przy zniknieciu (nazwane), slownik usuniety.</summary>
        internal static void OriginPartyGone(PartyBase pb, bool destroyed)
        {
            if (pb == null) return;
            Org o;
            if (_mem.TryGetValue(pb, out o))
            {
                _gone += o.Sum; _fOut[FGone] += o.Sum; _tAll += o.Sum; _mem.Remove(pb);
                try { if (pb.IsMobile && pb.MobileParty != null && KindOf(pb.MobileParty) == KWatch) _wGone += o.Sum; } catch (Exception e) { OStumble("OriginPartyGone(E4/9)", e); }
            }
            if (_pris.TryGetValue(pb, out o)) { _gone += o.Sum; _fOut[FGone] += o.Sum; _tAll += o.Sum; _pris.Remove(pb); }
            var mp = pb.MobileParty;
            Shares p;
            if (mp != null && _pending.TryGetValue(mp, out p)) { _pendLost += p.Total; _pending.Remove(mp); }
        }

        internal static void OnPartyRemoved(PartyBase pb)
        {
            // E4/8 (recenzja R1-1/R2-5): partia usunieta bez MobilePartyDestroyed (RemoveParty wprost - konwoje DTE) - koniec bez bitwy do DOM, PRZED resztka w nicosc
            try { ArmsPartyRemoved(pb); } catch (Exception e) { OStumble("OnPartyRemoved(E4/8)", e); }
            try { OriginPartyGone(pb, false); } catch (Exception e) { OStumble("OnPartyRemoved", e); }
        }

        // ================================================================ siew (raz na kampanie; NK "siew przy wlaczeniu")
        private static double DomOf(Settlement region)
        {
            if (region == null) return 0.0;
            double d = 0.0;
            try
            {
                if (region.BoundVillages != null) foreach (var v in region.BoundVillages) if (v != null && v.Settlement != null) d += PopulationLaw.PeopleOf(v.Settlement);
                if (region.IsTown) d += PopulationLaw.PeopleOf(region);
            }
            catch (Exception e) { OStumble("DomOf", e); }
            return d;
        }

        /// <summary>Pochodzenie istniejacego wojska przy siewie (bez zdejmowania z DOM; L_ref je obejmuje).</summary>
        private static Shares SeedShares(MobileParty mp, int kind, int n, Dictionary<string, double> dom)
        {
            var s = new Shares();
            try
            {
                Settlement reg = null;
                switch (kind)
                {
                    case KWatch: case KPatrolGame: s.Add("", n); return s;      // Straz: rekrut z calego Westeros (E4/9); patrol gry - z szablonu
                    case KRoad: s.Add("", n); return s;                          // E4/8: wedrowcy w drodze w dniu siewu - nie zdjeci z DOM (u celu bez hearth)
                    case KGarrison: reg = OutlawLaw.RegionFor(mp.CurrentSettlement ?? mp.HomeSettlement); break;
                    case KMilitia: case KCaravan: case KPatrolBk: reg = OutlawLaw.RegionFor(mp.HomeSettlement ?? mp.CurrentSettlement); break;
                    case KBand: reg = OutlawLaw.RegionAt(mp.Position.ToVec2()); break;
                }
                if (reg != null) { s.Add(RegKey(reg), n); return s; }
                if (kind == KGarrison || kind == KMilitia || kind == KCaravan || kind == KPatrolBk || kind == KBand) { s.Add("", n); return s; }
                // partia rodu (i gracza): wedlug ludzi regionow lenn rodu (DOK krok 5 poprawka krytyka); brak lenn - "" (rody najemne, bez ziemi)
                var clan = mp.ActualClan;
                var w = new Dictionary<string, double>();
                if (clan != null && clan.Fiefs != null)
                    foreach (var f in clan.Fiefs)
                    {
                        var st = f != null ? f.Settlement : null;
                        if (st == null || st.StringId == null) continue;
                        double d;
                        if (!dom.TryGetValue(st.StringId, out d)) { d = DomOf(st); dom[st.StringId] = d; }
                        if (d > 0.0) w[st.StringId] = d;
                    }
                double all = 0.0; foreach (var v in w.Values) all += v;
                if (all <= 0.0) { s.Add("", n); return s; }
                foreach (var kv in w) s.Add(kv.Key, n * kv.Value / all);
            }
            catch (Exception e) { OStumble("SeedShares", e); s = Shares.None(n); }
            return s;
        }

        /// <summary>Siew: pierwsza doba sesji bez sw: przy PeopleOriginEnabled, po kalibracji ludzi (k na osade). Istniejace wojsko - regiony (bez zdejmowania), jency - "".</summary>
        private static void EnsureSown(int day)
        {
            if (_sown || !OriginOn || Campaign.Current == null || !PopulationLaw.Calibrated) return;
            try
            {
                float k0;
                Settlement any = null;
                foreach (var st in Settlement.All) if (st != null && st.IsVillage) { any = st; break; }
                if (any != null && !TryUnit(any, out k0)) return;                  // k na osade jeszcze nie zalozone
                _mem.Clear(); _pris.Clear(); _poolOrg.Clear(); _rg.Clear(); _pending.Clear();
                var dom = new Dictionary<string, double>();
                double withOrg = 0, none = 0, watch = 0, landless = 0, tpl = 0, jail = 0;
                int parties = 0, jails = 0;
                var regs = new HashSet<string>();
                foreach (var mp in MobileParty.All)
                {
                    try
                    {
                        if (mp == null || mp.Party == null) continue;
                        int kind = KindOf(mp);
                        if (kind >= 0)
                        {
                            int n = Men(mp.MemberRoster);
                            if (n > 0)
                            {
                                var sh = SeedShares(mp, kind, n, dom);
                                var o = new Org();
                                foreach (var kv in sh.M) if (kv.Value > 0.0) { OrgAdd(o, kv.Key, kv.Value); if (kv.Key.Length > 0) regs.Add(kv.Key); }
                                _mem[mp.Party] = o; parties++;
                                double u = sh.Unknown; withOrg += sh.Known; none += u;
                                if (kind == KWatch) watch += u; else if (kind == KPatrolGame) tpl += u; else landless += u;
                            }
                        }
                        int j = Men(mp.PrisonRoster);
                        if (j > 0) { var o = new Org(); OrgAdd(o, "", j); _pris[mp.Party] = o; jail += j; jails++; }
                    }
                    catch (Exception e) { OStumble("EnsureSown(partia)", e); }
                }
                foreach (var st in Settlement.All)
                {
                    try
                    {
                        if (st == null || st.Party == null) continue;
                        int j = Men(st.Party.PrisonRoster);
                        if (j > 0 && !_pris.ContainsKey(st.Party)) { var o = new Org(); OrgAdd(o, "", j); _pris[st.Party] = o; jail += j; jails++; }
                    }
                    catch (Exception e) { OStumble("EnsureSown(loch)", e); }
                }
                _sown = true;
                _sowDay = day;
                // stan dnia siewu wedlug regionu (L_ref = dom + sluzba + jency; P R5, DOK krok 6 poprawka)
                var now = RegionNow();
                double lref = 0;
                foreach (var kv in now)
                {
                    var r = Rg(kv.Key); var x = kv.Value;
                    r.DomRef = x.Dom; r.SRef = x.S; r.JRef = x.J; r.LRef = x.Dom + x.S + x.J; r.PoolRef = x.Wy; r.RefRef = x.U; r.CartRef = x.Carts;
                    r.TRef = x.T; r.WRef = x.W;          // E4/9 (Z12): NK - bilans otwarcia niewolnych Zelaznych (SeedThralls w Calibrate - przed siewem)
                    lref += r.LRef;
                }
                _sowNote = "siew dnia " + _sowDay + " - partii " + parties + " (ludzi " + N0(withOrg + none) + ": z pochodzeniem " + N0(withOrg) + " w " + regs.Count + " regionach, bez pochodzenia "
                           + N0(none) + ": Straz " + N0(watch) + ", rody bez ziemi i inne " + N0(landless) + ", patrole gry " + N0(tpl) + "), lochow " + jails + " (jency " + N0(jail)
                           + " bez pochodzenia), L_ref swiata " + (lref / 1e6).ToString("0.000", Inv) + " mln";
                Log.Info("Pochodzenie (E4/7): " + _sowNote + ".");
            }
            catch (Exception e) { OStumble("EnsureSown", e); }
        }

        private static string N0(double v) { return v.ToString("0", Inv); }

        // ================================================================ przyciecie kluczy (recenzja E4/7 R2-3)
        private const double FoldMin = 0.05;     // klucz ponizej 0.05 czlowieka - szum proporcjonalnego zdejmowania
        private const int FoldTop = 16;          // najwyzej 16 regionow na slownik (plus "")

        /// <summary>
        /// Raz na dobe (uzgodnienie): male klucze znane (ponizej FoldMin albo spoza FoldTop najwiekszych) wlewa do najwiekszego klucza znanego tego samego slownika.
        /// Bez tego slowniki rozlewaly sie na dziesiatki regionow (Give z mieszanego zrodla dokleja kazdy region po ulamku, Take nie usuwa klucza powyzej 1e-6),
        /// a arm_people i koszt Take rosly z dlugoscia kampanii. Suma bez zmian (kontrola stanu T2 bez zmian, bez nowej nicosci); "" nie jest ruszane (bez
        /// pochodzenia nie dostaje regionu i odwrotnie). Przeniesienie miedzy regionami - Drift w rg: (rownanie regionu je odejmuje, linia je pokazuje).
        /// </summary>
        private static void Fold(Dictionary<string, double> m)
        {
            if (m == null || m.Count < 2) return;
            string big = null; double bv = -1.0; int known = 0;
            foreach (var kv in m) { if (kv.Key.Length == 0) continue; known++; if (kv.Value > bv) { bv = kv.Value; big = kv.Key; } }
            if (known < 2 || big == null) return;
            HashSet<string> keep = null;
            if (known > FoldTop) keep = new HashSet<string>(m.Where(kv => kv.Key.Length > 0).OrderByDescending(kv => kv.Value).Take(FoldTop).Select(kv => kv.Key));
            List<string> drop = null;
            foreach (var kv in m)
            {
                if (kv.Key.Length == 0 || kv.Key == big) continue;
                if (kv.Value < FoldMin || (keep != null && !keep.Contains(kv.Key))) { if (drop == null) drop = new List<string>(); drop.Add(kv.Key); }
            }
            if (drop == null) return;
            double moved = 0.0;
            foreach (var k in drop) { double v = m[k]; m.Remove(k); moved += v; Rg(k).Drift -= v; }
            m[big] = m[big] + moved; Rg(big).Drift += moved;
            _foldKeys += drop.Count; _foldMen += moved;
        }

        // ================================================================ uzgodnienie doby (4.14)
        private static void ReconcileRoster(PartyBase pb, TroopRoster r, Dictionary<PartyBase, Org> dict, int kind, MobileParty mp)
        {
            int n = Men(r);
            Org o;
            dict.TryGetValue(pb, out o);
            double sum = o != null ? o.Sum : 0.0;
            // fixA (P2-z-bandy, sceptyk (c)): oczekujace udzialy narodzin bandy rozliczane przy PIERWSZYM uzgodnieniu skladu partii - czesc zjada nadwyzka rosteru,
            // reszta (banda stracila ludzi przed uzgodnieniem albo jej sklad ma juz slownik) od razu do nazwanej "oczekujace udzialy band porzucone" (_pendLost);
            // bez tego pozniejszy przyrost bandy dostawal cudze pochodzenie (udzialy poleglych przed uzgodnieniem)
            Shares p = null;
            if (mp != null && dict == _mem && _pending.TryGetValue(mp, out p)) _pending.Remove(mp);
            if (n > sum + Eps)
            {
                if (o == null) { o = new Org(); dict[pb] = o; }
                double add = n - sum;
                if (p != null && p.Total > Eps)
                {
                    // recenzja fixA (2): najwyzej tyle, ile oczekuje - Cut(add) przy add > Total dokladal "" na nadwyzke i liczyl ja jako "z puli do band";
                    // nadwyzka rosteru ponad oczekujace (przyrost bez okna przed pierwszym uzgodnieniem) idzie dalej: Straz, potem "" z niczego (_zAll, _zBy)
                    var use = p.Cut(Math.Min(add, p.Total));
                    double known = 0;
                    foreach (var kv in use.M) { OrgAdd(o, kv.Key, kv.Value); known += kv.Value; }
                    _pendIn += known; _fIn[FPoolBand] += known; _gAll += known;
                    add -= known;
                }
                // E4/9 (Z9): nadwyzka rosteru Strazy (kanaly bez okna) - rekrut z Westeros przez WatchLaw.Levy (Mur, pule, ludnosc; reszta "" nazwana), nie "z niczego"
                if (add > Eps && kind == KWatch && WatchLaw.Live)
                {
                    try { add -= ReconcileWatch(o, add); } catch (Exception e) { OStumble("Reconcile(E4/9 Straz)", e); }
                }
                if (add > Eps) { OrgAdd(o, "", add); _zAll += add; _zBy[kind] += add; }
            }
            else if (n < sum - Eps && o != null)
            {
                double cut = sum - n;
                double f = n / sum;
                foreach (var k in o.M.Keys.ToList())
                {
                    double v = o.M[k] * f;
                    if (v > Eps) o.M[k] = v; else o.M.Remove(k);
                }
                o.Sum = 0.0; foreach (var v in o.M.Values) o.Sum += v;
                _qAll += cut; _qBy[kind] += cut;
                if (kind == KWatch) _wQ += cut;                  // E4/9: niedobor slownika Strazy - w nicosc (linia "Straz z Westeros")
                if (dict == _mem) { try { NoteCut(pb, mp, kind, cut); } catch (Exception e) { OStumble("Reconcile(diagnostyka)", e); } }   // fixA (P2-T4-sklady) - tylko licznik
            }
            if (p != null && p.Total > Eps) _pendLost += p.Total;   // fixA: reszta oczekujacych po pierwszym uzgodnieniu - nazwana (linia: oczekujace udzialy band porzucone)
            if (o != null && o.M.Count > 1) { try { Fold(o.M); } catch (Exception e) { OStumble("Fold(roster)", e); } }     // R2-3 (suma bez zmian)
            if (o != null && n == 0 && o.Sum <= Eps) dict.Remove(pb);
        }

        /// <summary>fixA (P2-T4-sklady, tylko diagnostyka - nic nie zmienia): stan partii w chwili ciecia slownika skladu. Kolejnosc rozstrzyga, gdy stanow jest kilka
        /// (bitwa, oblezenie - oblegajacy albo partia w oblezonej osadzie, morze, glod, armia, w osadzie, w polu); osada (sklad bez partii) - "w osadzie".</summary>
        private static int CutState(MobileParty mp)
        {
            if (mp == null) return STown;
            try
            {
                if (mp.MapEvent != null) return SBattle;
                var cs = mp.CurrentSettlement;
                if (mp.BesiegerCamp != null || (cs != null && cs.IsUnderSiege)) return SSiege;
                if (mp.IsCurrentlyAtSea) return SSea;
                if (mp.Party != null && mp.Party.IsStarving) return SStarve;
                if (mp.Army != null) return SArmy;
                if (cs != null) return STown;
            }
            catch { }
            return SField;
        }

        /// <summary>fixA (P2-T4-sklady): niedobor skladu (ciecie slownika w uzgodnieniu) - suma wedlug stanu partii i 5 partii z najwiekszym niedoborem doby.</summary>
        private static void NoteCut(PartyBase pb, MobileParty mp, int kind, double cut)
        {
            if (!(cut > Eps)) return;
            _cutBy[CutState(mp)] += cut;
            if (_cutTop.Count >= 5 && cut <= _cutTop[_cutTop.Count - 1].Key) return;
            string who;
            try
            {
                string nm = mp != null ? (mp.Name != null ? mp.Name.ToString() : mp.StringId) : (pb.Settlement != null && pb.Settlement.Name != null ? pb.Settlement.Name.ToString() : "?");
                var clan = mp != null ? mp.ActualClan : null;
                who = (nm ?? "?") + " (" + (kind >= 0 && kind < KKinds ? KName[kind] : "?") + (clan != null && clan.Name != null ? ", " + clan.Name : "") + ", " + SName[CutState(mp)] + ") " + M0(cut);
            }
            catch { who = "? " + M0(cut); }
            int at = _cutTop.Count;
            while (at > 0 && _cutTop[at - 1].Key < cut) at--;
            _cutTop.Insert(at, new KeyValuePair<double, string>(cut, who));
            if (_cutTop.Count > 5) _cutTop.RemoveAt(_cutTop.Count - 1);
        }

        private static void ReconcileDaily()
        {
            var seen = new HashSet<PartyBase>();
            foreach (var mp in MobileParty.All.ToList())
            {
                try
                {
                    if (mp == null || mp.Party == null) continue;
                    var pb = mp.Party;
                    seen.Add(pb);
                    int kind = KindOf(mp);
                    // fixA (T3/V10/K19 + czesc P1-2, better_fix sceptykow): sklad partii w TRWAJACEJ bitwie - uzgodnienie odlozone do konca bitwy. Gra zdejmuje zabitych
                    // i rozbitych z MemberRoster w kazdej rundzie (MapEventParty.OnTroopKilled / OnTroopRouted), a ksiega dopiero przy MapEventEnded (Take poleglych,
                    // OutlawLaw/LosersFlee - rozbici, CaptureFin - jency). Ciecie tutaj gubilo udzialy regionu pochodzenia bez POLEGLI (reszta krainy trwale ujemna), a
                    // nazajutrz Take poleglych zdejmowal drugi raz i uzgodnienie dosypywalo "" (z niczego). seen.Add wyzej - OriginPartyGone slownika nie zabierze.
                    // Tylko sklad (rundy nie ruszaja lochow); bitwa dluzsza niz doba - odklada dalej
                    if (kind >= 0 && pb.MapEvent != null)
                    {
                        Org od;
                        if (_mem.TryGetValue(pb, out od)) { _defParties++; _defMen += Math.Max(0.0, od.Sum - Men(mp.MemberRoster)); }
                    }
                    else if (kind >= 0) ReconcileRoster(pb, mp.MemberRoster, _mem, kind, mp);
                    else
                    {
                        Org gone;
                        if (_mem.TryGetValue(pb, out gone)) { _gone += gone.Sum; _fOut[FGone] += gone.Sum; _tAll += gone.Sum; _mem.Remove(pb); }   // partia bez slownika skladu (TABOR, Inni) - liczone
                    }
                    ReconcileRoster(pb, mp.PrisonRoster, _pris, KPrison, null);
                }
                catch (Exception e) { OStumble("Reconcile(partia)", e); }
            }
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || st.Party == null) continue;
                    seen.Add(st.Party);
                    ReconcileRoster(st.Party, st.Party.PrisonRoster, _pris, KDungeon, null);
                    if (_mem.ContainsKey(st.Party)) ReconcileRoster(st.Party, st.Party.MemberRoster, _mem, KOther, null);
                }
                catch (Exception e) { OStumble("Reconcile(loch)", e); }
            }
            // slowniki partii, ktorych juz nie ma (bez zdarzenia) - w nicosc przy zniknieciu.
            // fixA (P11, sceptyk B): sprzatanie we wlasnym try - wywrotka tutaj (dotad KeyNotFoundException co dobe od d3 na l. _pending[mp], polykana przez CrashScribe
            // na granicy ReconcileDaily) nie moze gasic petli puli nizej (licz potkniecia, nie gas funkcji); oczekujace po parach z ToList() - bez indeksatora
            try
            {
                foreach (var pb in _mem.Keys.Where(x => !seen.Contains(x)).ToList()) OriginPartyGone(pb, false);
                foreach (var pb in _pris.Keys.Where(x => !seen.Contains(x)).ToList()) OriginPartyGone(pb, false);
                foreach (var kv in _pending.Where(x => x.Key == null || x.Key.Party == null || !seen.Contains(x.Key.Party)).ToList())
                {
                    if (kv.Value != null) _pendLost += kv.Value.Total;
                    _pending.Remove(kv.Key);
                }
            }
            catch (Exception e) { OStumble("Reconcile(oczekujace)", e); }
            // pula: znani nie wiecej niz zolnierze puli regionu (wygasle klucze, podloga 10, powrot prostych i bandytow - bez nazwanego okna)
            foreach (var reg in _poolOrg.Keys.ToList())
            {
                try
                {
                    var d = _poolOrg[reg];
                    double known = 0; foreach (var v in d.Values) known += v;
                    double troops = OutlawLaw.PoolTroops(Node(reg));
                    if (known > troops + Eps)
                    {
                        double f = troops > 0 ? troops / known : 0.0;
                        foreach (var k in d.Keys.ToList()) { double v = d[k] * f; if (v > Eps) d[k] = v; else d.Remove(k); }
                        _poolTrim += known - troops;
                    }
                    if (d.Count > 1) Fold(d);                                   // R2-3 (suma bez zmian)
                    if (d.Count == 0) _poolOrg.Remove(reg);
                }
                catch (Exception e) { OStumble("Reconcile(pula)", e); }
            }
        }
        private static double _poolTrim;

        // ================================================================ stan regionow (rownanie, wolne rece, menu)
        internal struct RegionNum
        {
            public double Dom, S, J, Wy, U, Carts;
            public double Road;     // E4/8 (Z9): wedrowcy i eskorta konwojow BK w drodze (slowniki KRoad) - poza S, w rownaniu regionu jak TABOR
            public double Vet;      // E4/10: WETERANI w karczmach wedlug regionu pochodzenia (konto poza DOM i poza SLUZBA - nie liczy sie do S w R5)
            public double T, W;     // E4/9 (Z11): NIEWOLNI wedlug regionu pochodzenia (wszedzie) i konto MUR wedlug regionu pochodzenia
        }

        private static Dictionary<string, RegionNum> RegionNow()
        {
            var now = new Dictionary<string, RegionNum>();
            foreach (var node in OutlawLaw.RegionNodes())
            {
                try
                {
                    if (node == null || node.StringId == null) continue;
                    var x = new RegionNum { Dom = DomOf(node), Wy = OutlawLaw.PoolCommoners(node) };
                    double away, dead;
                    RegionRefugees(node, out away, out dead);
                    x.U = away;
                    now[node.StringId] = x;
                }
                catch (Exception e) { OStumble("RegionNow(region)", e); }
            }
            Action<Dictionary<PartyBase, Org>, bool> sum = (dict, prison) =>
            {
                foreach (var po in dict)
                {
                    // E4/8 (Z9): sklad wedrowcow i konwojow BK - w drodze (Road), nie w sluzbie (S nie podnosi a wolnych rak)
                    bool road = !prison && po.Key != null && po.Key.IsMobile && po.Key.MobileParty != null && KindOf(po.Key.MobileParty) == KRoad;
                    foreach (var kv in po.Value.M)
                    {
                        if (kv.Key.Length == 0) continue;
                        RegionNum x; now.TryGetValue(kv.Key, out x);
                        if (prison) x.J += kv.Value; else if (road) x.Road += kv.Value; else x.S += kv.Value;
                        now[kv.Key] = x;
                    }
                }
            };
            sum(_mem, false); sum(_pris, true);
            foreach (var d in _poolOrg.Values)
                foreach (var kv in d) { RegionNum x; now.TryGetValue(kv.Key, out x); x.Wy += kv.Value; now[kv.Key] = x; }
            // N7: rozbici czekajacy w osadzie zbiorki (StragglersRally) - w rownaniu regionu jak "w drodze" (Road): poza sluzba (nie podnosza S ani wolnych rak) i poza domem
            try
            {
                var rally = new Dictionary<string, double>();
                StragglersRally.KnownByRegion(rally);
                foreach (var kv in rally) { RegionNum x; now.TryGetValue(kv.Key, out x); x.Road += kv.Value; now[kv.Key] = x; }
            }
            catch (Exception e) { OStumble("RegionNow(N7)", e); }
            // E4/10 (Z10): konto WETERANI w rownaniu regionu (wejscie SLUZBA -> WETERANI, siew DOM miasta -> WETERANI, powrot WETERANI -> DOM, najem WETERANI -> SLUZBA)
            try
            {
                var vet = new Dictionary<string, double>();
                VetByRegion(vet);
                foreach (var kv in vet) { RegionNum x; now.TryGetValue(kv.Key, out x); x.Vet += kv.Value; now[kv.Key] = x; }
            }
            catch (Exception e) { OStumble("RegionNow(weterani)", e); }
            // E4/9 (Z11): konta NIEWOLNI (wedlug pochodzenia) i MUR w rownaniu regionu pochodzenia
            try { CanonByRegion(now); } catch (Exception e) { OStumble("RegionNow(E4/9)", e); }
            try
            {
                foreach (var mp in MobileParty.AllVillagerParties)
                {
                    if (mp == null || mp.HomeSettlement == null) continue;
                    string k = RegKey(OutlawLaw.RegionFor(mp.HomeSettlement));
                    if (k.Length == 0) continue;
                    RegionNum x; now.TryGetValue(k, out x); x.Carts += Men(mp.MemberRoster); now[k] = x;
                }
            }
            catch (Exception e) { OStumble("RegionNow(tabory)", e); }
            return now;
        }

        /// <summary>Region dla menu, wolnych rak i linii: stan dnia (cache z OriginDaily) + liczniki od siewu.</summary>
        internal sealed class RegionView
        {
            public string Id; public Settlement Node; public string Realm;
            public double Dom, S, J, Wy, U, Carts, DomRef, LRef, Fallen, FallenMen, Civil, Born, Enslaved, ToWall, Rest;
            public double Vet;      // E4/10: weterani tej krainy czekajacy w karczmach (menu, rownanie)
            public int Villages; public double VillagePeople, TownPeople;
            public double Road, MigIn, MigOut;     // E4/8: w drodze (wedrowcy i konwoje BK), migracja od siewu
            // E4/9: NIEWOLNI z regionu (wedlug pochodzenia) i MUR z regionu; stan dnia siewu i zmarli niewolni od siewu; niewolni TRZYMANI w regionie (menu, krainy)
            public double T, W, TRef, WRef, TDead, THeld;
            /// <summary>E4/9 (R5, recenzja E4/7 R1-2): niewolni z regionu od siewu (zywi + zmarli - stan dnia siewu) - ubytek rak jak polegli (bilans otwarcia NK nie wchodzi).</summary>
            public double TSince { get { return Math.Max(0.0, T + TDead - TRef); } }
        }
        private static volatile Dictionary<string, RegionView> _view = new Dictionary<string, RegionView>();

        internal static RegionView ViewOf(Settlement region)
        {
            if (region == null || region.StringId == null) return null;
            var v = _view; RegionView r;
            return v != null && v.TryGetValue(region.StringId, out r) ? r : null;
        }

        internal static IEnumerable<RegionView> Views() { var v = _view; return v != null ? v.Values : Enumerable.Empty<RegionView>(); }

        /// <summary>U19: S z regionu (sluzba + jency u wroga) z cache doby - jedna definicja dla linii, hamulca, plonu, menu i PeopleLedger.</summary>
        internal static double UnderArms(Settlement region) { var r = ViewOf(region); return r != null ? r.S + r.J : 0.0; }

        private static void BuildViews()
        {
            var now = RegionNow();
            var view = new Dictionary<string, RegionView>();
            foreach (var kv in now)
            {
                var node = Node(kv.Key);
                if (node == null) continue;
                RegRef r; _rg.TryGetValue(kv.Key, out r);
                var x = kv.Value;
                var v = new RegionView
                {
                    Id = kv.Key, Node = node, Realm = FreeHands.RealmOf(node),
                    Dom = x.Dom, S = x.S, J = x.J, Wy = x.Wy, U = x.U, Carts = x.Carts, Road = x.Road, Vet = x.Vet, T = x.T, W = x.W
                };
                try { v.THeld = ThrallsHeldIn(kv.Key); } catch (Exception e) { OStumble("BuildViews(E4/9)", e); }
                if (r != null)
                {
                    v.DomRef = r.DomRef; v.LRef = r.LRef; v.Fallen = r.Fallen; v.FallenMen = r.FallenMen; v.Civil = r.Civil; v.Born = r.Born; v.Enslaved = r.Enslaved; v.ToWall = r.ToWall;
                    v.MigIn = r.MigIn; v.MigOut = r.MigOut;
                    v.TRef = r.TRef; v.WRef = r.WRef; v.TDead = r.TDead;
                    // rownanie regionu (5): stan - stan siewu - narodziny + polegli + cywile + niewola + Mur = reszta (ludzie z niczego / w nicosc w regionie)
                    // recenzja E4/7 R2-3: minus Drift (ludzie przeniesieni do/z regionu przy przycieciu kluczy - ksiegowanie, nie ruch ludzi)
                    // E4/8 (10.5): + w drodze (wedrowcy BK; w dniu siewu bez pochodzenia - 0) + MigOut - MigIn (wedrowcy u celu w innym regionie)
                    // E4/10 (Z10): + WETERANI (bez stanu w dniu siewu E4/7: pula zaklada sie po nim, a przed siewem E4/7 ma tylko udzialy "" - poza regionami)
                    // E4/9 (4.7): + NIEWOLNI i MUR z regionu (stan) - ich stan dnia siewu + zmarli niewolni od siewu; liczniki Enslaved/ToWall rosna juz tylko przy wylaczonych prawach
                    v.Rest = (x.Dom + x.S + x.J + x.Wy + x.U + x.Carts + x.Road + x.Vet + x.T + x.W) - (r.DomRef + r.SRef + r.JRef + r.PoolRef + r.RefRef + r.CartRef + r.TRef + r.WRef)
                             - r.Born + r.Fallen + r.Civil + r.Enslaved + r.ToWall + r.TDead - r.Drift + r.MigOut - r.MigIn;
                }
                try
                {
                    if (node.BoundVillages != null) foreach (var vg in node.BoundVillages) { if (vg == null || vg.Settlement == null) continue; v.Villages++; v.VillagePeople += PopulationLaw.PeopleOf(vg.Settlement); }
                    if (node.IsTown) v.TownPeople = PopulationLaw.PeopleOf(node);
                }
                catch (Exception e) { OStumble("BuildViews(wsie)", e); }
                view[kv.Key] = v;
            }
            _view = view;
        }

        // ================================================================ doba
        private static void OriginDaily(int day)
        {
            if (!OriginOn)
            {
                if (_sown) { _view = new Dictionary<string, RegionView>(); try { FreeHands.Update(null); } catch { } }
                return;
            }
            if (_pendSo != null || _pendJo != null) ResolveOrigin("pierwsza doba (bez OnSessionLaunched)");
            EnsureSown(day);
            if (!_sown) return;
            if (_press != null) { double left = _press.Total; _press = null; PressUntaken(left); }   // R1-5: ekran lupu bez Done do konca doby - niewzieci (E4/7b: do DOM swojej wsi)
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try { ReconcileDaily(); } catch (Exception e) { OStumble("ReconcileDaily", e); }
            try { BuildViews(); } catch (Exception e) { OStumble("BuildViews", e); }
            try { FreeHands.Update(_view); } catch (Exception e) { OStumble("FreeHands.Update", e); }
            _lastMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        private static void StateNow(out double s, out double sKnown, out double j, out double jKnown, out int regs, double[] noneBy)
        {
            s = sKnown = j = jKnown = 0; var rs = new HashSet<string>();
            foreach (var kv in _mem)
            {
                var o = kv.Value; s += o.Sum;
                double u; o.M.TryGetValue("", out u);
                sKnown += o.Sum - u;
                foreach (var k in o.M.Keys) if (k.Length > 0) rs.Add(k);
                if (u > 0 && noneBy != null)
                {
                    var pb = kv.Key; int kind = pb.IsMobile && pb.MobileParty != null ? KindOf(pb.MobileParty) : KOther;
                    if (kind < 0) kind = KOther;
                    noneBy[kind] += u;
                }
            }
            foreach (var o in _pris.Values) { j += o.Sum; double u; o.M.TryGetValue("", out u); jKnown += o.Sum - u; }
            regs = rs.Count;
        }

        /// <summary>Odcinek linii "Ludzie (ksiega)": konta SLUZBA/JENCY, POLEGLI od siewu, pobor i powroty z hearth, rownanie krain.</summary>
        private static string OriginSegment()
        {
            if (!OriginOn) return " | E4/7 pochodzenie: WYLACZONE (People Origin Enabled)";
            if (!_sown) return " | E4/7 pochodzenie: przed siewem (ludzie jeszcze nieskalibrowani albo k na osade niezalozone)";
            double s, sk, j, jk; int regs;
            StateNow(out s, out sk, out j, out jk, out regs, null);
            double fallen = 0, civil = 0, born = 0;
            foreach (var r in _rg.Values) { fallen += r.Fallen; civil += r.Civil; born += r.Born; }
            double[] men = new double[MKinds], hh = new double[MKinds];
            lock (Gate) { Array.Copy(_mMen, men, MKinds); Array.Copy(_mHearth, hh, MKinds); }
            double levy = men[MLevyVillage] + men[MLevyA5] + men[MLevyTown], levyH = hh[MLevyVillage] + hh[MLevyA5];
            double back = 0, backH = 0;
            for (int i = MBackH3; i <= MBackDisband; i++) { back += men[i]; backH += hh[i]; }
            back += men[MBackTown];                                         // R1-1: do stanu miasta (bez hearth)
            back += men[MBackVeteran]; backH += hh[MBackVeteran];          // E4/10: weterani po czasie czekania (czesc do stanu miasta jest w MBackTown)
            back += men[MBackPress]; backH += hh[MBackPress];               // E4/7b: niewzieci z poboru wymuszonego gracza do swojej wsi
            // rownanie krain (suma regionow kultury regionu)
            var realms = new Dictionary<string, double[]>();
            double world = 0, alive = 0;
            foreach (var v in Views())
            {
                double[] a; string rk = v.Realm ?? "?";
                if (!realms.TryGetValue(rk, out a)) { a = new double[2]; realms[rk] = a; }
                double live = v.Dom + v.S + v.J + v.Wy + v.U + v.Carts + v.Road + v.Vet + v.T + v.W;
                a[0] += v.Rest; a[1] += live;
                world += v.Rest; alive += live;
            }
            string worst = "-"; double worstRest = 0, worstPc = 0; bool worstOff = false;
            foreach (var kv in realms)
            {
                double pc = kv.Value[1] > 0 ? 100.0 * kv.Value[0] / kv.Value[1] : 0.0;
                // Jeff 12.10: prog "0.1% albo 200 ludzi, co wieksze" - kraina poza progiem wypiera kazda w progu (linia podaje jedna kraine; bez tego
                // mala kraina z wiekszym % w 200 ludziach zaslanialaby wieksza poza progiem); w grupie - jak dotad najwiekszy |%|
                bool off = Math.Abs(pc) > 0.1 && Math.Abs(kv.Value[0]) > 200.0;
                if (worst == "-" || (off && !worstOff) || (off == worstOff && Math.Abs(pc) > Math.Abs(worstPc))) { worst = kv.Key; worstRest = kv.Value[0]; worstPc = pc; worstOff = off; }
            }
            var sb = new StringBuilder(500);
            sb.Append(" | E4/7: konto SLUZBA ").Append(N0(sk)).Append(" + \"\" ").Append(N0(s - sk)).Append(", JENCY ").Append(N0(jk)).Append(" + \"\" ").Append(N0(j - jk))
              .Append(", POLEGLI od siewu ").Append(N0(fallen)).Append(" (dzis ").Append(N0(_dest[ODead] + _deadCarts)).Append("), cywile od siewu ").Append(N0(civil))
              .Append(", narodziny netto od siewu ").Append(N0(born))
              .Append(" | pobor z DOM dzis ").Append(N0(levy)).Append(" ludzi / ").Append(H3d(levyH)).Append(" hearth (w tym ze stanu miast ").Append(N0(men[MLevyTown])).Append(")")
              .Append(", powroty z pochodzeniem +").Append(N0(back)).Append(" ludzi / ").Append(H3d(backH)).Append(" hearth (w tym do stanu miast ").Append(N0(men[MBackTown])).Append(")")
              .Append(" | rownanie krain: najgorsza kraina ").Append(worst).Append(" reszta ").Append(H3d(worstRest)).Append(" (").Append(worstPc.ToString("0.###", Inv)).Append("% ludzi krainy), swiat ").Append(H3d(world));
            try { sb.Append(ArmsSegment()); } catch (Exception e) { OStumble("OriginSegment(E4/8)", e); }     // E4/8 (10.5): migracja R19 od siewu, w drodze
            try { sb.Append(CanonSegment()); } catch (Exception e) { OStumble("OriginSegment(E4/9)", e); }    // E4/9: NIEWOLNI, MUR, rownanie z T i W
            // recenzja fixC (1), tylko log: ludzie zywi swiata z definicji rownania krain (suma stanow regionow - te same skladniki co reszta v.Rest; SLUZBA bez
            // wedrowcow BK, ktorzy sa w drodze; tabory; NIEWOLNI, Mur i pula tylko z pochodzeniem) - jedna liczba dla progow E4/7 T11 i E4/5 S15 narzedzia
            sb.Append(" | ludzie zywi (rownanie krain: DOM + uchodzcy + SLUZBA + JENCY + pula + tabory + w drodze + WETERANI + NIEWOLNI + Mur, z pochodzeniem) ").Append(N0(alive));
            return sb.ToString();
        }

        /// <summary>fixA (T6): do 3 regionow z najwiekszym odwrotem wsi do stanu miasta w dobie - " [nazwa N, ...]" albo "".</summary>
        private static string VillTownNames()
        {
            var d = _villTownBy;
            if (d == null || d.Count == 0) return "";
            return " [" + string.Join(", ", d.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key + " " + M0(kv.Value)).ToArray()) + "]";
        }

        private static string Part(double[] a, int from, int to)
        {
            var sb = new StringBuilder();
            for (int i = from; i <= to; i++) sb.Append(i > from ? ", " : "").Append(FName[i]).Append(' ').Append(M0(a[i]));
            return sb.ToString();
        }

        private static double Sum(double[] a, int from, int to) { double s = 0; for (int i = from; i <= to; i++) s += a[i]; return s; }

        /// <summary>Linie "Pochodzenie (E4/7)" i "Wolne rece (E4/7)" (po "Ludzie (ksiega)").</summary>
        private static void OriginLines(int day, long t0)
        {
            if (!OriginOn || !_sown) return;
            double s, sk, j, jk; int regs; var noneBy = new double[KKinds];
            StateNow(out s, out sk, out j, out jk, out regs, noneBy);
            double state = s + j;
            double ins = _gAll, outs = _tAll;
            var sb = new StringBuilder(3000);
            sb.Append("Pochodzenie (E4/7): dzien ").Append(day).Append(" | stan: SLUZBA ").Append(N0(s)).Append(" (z pochodzeniem ").Append(N0(sk)).Append(" w ").Append(regs).Append(" regionach; bez pochodzenia ").Append(N0(s - sk)).Append(':');
            bool f = true;
            for (int k = 0; k < KKinds; k++) { if (k == KPrison || k == KDungeon || noneBy[k] <= 0.5) continue; sb.Append(f ? " " : ", ").Append(KName[k]).Append(' ').Append(N0(noneBy[k])); f = false; }
            if (f) sb.Append(" -");
            sb.Append("), JENCY ").Append(N0(j)).Append(" (z pochodzeniem ").Append(N0(jk)).Append(", bez ").Append(N0(j - jk)).Append(')');
            if (_sowNote != null && _sowDay == day) sb.Append(" [").Append(_sowNote).Append(']');
            sb.Append(" | wejscia: pobor z DOM ").Append(M0(Sum(_fIn, FLevyVillage, FPress))).Append(" (").Append(Part(_fIn, FLevyVillage, FPress)).Append(")")
              .Append(", z puli do band ").Append(M0(_fIn[FPoolBand])).Append(" (w tym narodziny band z oczekujacych ").Append(M0(_pendIn)).Append(")")
              .Append(", bez pochodzenia ").Append(M0(Sum(_fIn, FNoneMap, FNoneOff))).Append(" (").Append(Part(_fIn, FNoneMap, FNoneOff)).Append(")")
              .Append(", z karczmy (WETERANI, E4/10) ").Append(M0(_fIn[FVetHire]))
              .Append(", Straz z Westeros (E4/9) ").Append(M0(Sum(_fIn, FWatchWall, FNoneWatchLeft))).Append(" (").Append(Part(_fIn, FWatchWall, FNoneWatchLeft)).Append(")")
              .Append(", pojmani z rabunku (E4/9) ").Append(M0(_fIn[FCapturedRaid]))
              .Append(", rozbici pod choragiew (N7) ").Append(M0(_fIn[FRally]));
            if (_pressIn > 0 || _pressLost > 0 || _pressHome > 0)
            {
                // E4/7b: zdjeci z DOM = wzieci + wrocili do domu (+ bez powrotu tylko przy wylaczonym Press Untaken Go Home)
                sb.Append(", pobor wymuszony gracza: zdjeci z DOM ").Append(M0(_pressIn)).Append(", wzieci z ekranu lupu ").Append(M0(_fIn[FPress])).Append(", niewzieci - wrocili do domu ").Append(M0(_pressHome));
                if (_pressLost > 0) sb.Append(", niewzieci bez powrotu (poza rosterem) ").Append(M0(_pressLost));
            }
            sb.Append(" | przenosiny: ").Append(Part(_fIn, FToGarrison, FPrisRecruit));
            // E4/8 (Z17): wejscia i wyjscia przyczynami E4/8 - te same liczniki co reszta (wchodza do "wejscia/wyjscia" kontroli)
            sb.Append(" | E4/8: wejscia ").Append(Part(_fIn, FMilitiaLevy, FArmsFloor)).Append("; wyjscia ").Append(Part(_fOut, FMilitiaHome, FDisbandHome));
            sb.Append(" | wyjscia: ").Append(Part(_fOut, FDieBattle, FGone)).Append(", ").Append(Part(_fOut, FDieSiege, FEscape))     // fixA: ostrzal, ucieczki z lochu
              .Append(" -> dokad: polegli ").Append(M0(_dest[ODead])).Append(" (bitwa ").Append(M0(_deadBy[FDieBattle])).Append(", zaraza ").Append(M0(_deadBy[FDiePlague])).Append(", straceni ").Append(M0(_deadBy[FDieExec]))
              .Append(", Inni ").Append(M0(_deadBy[FDieOthers])).Append(", ostrzal w oblezeniu ").Append(M0(_deadBy[FDieSiege])).Append("; tabory poza kontem ").Append(M0(_deadCarts)).Append(")")
              .Append(", do domu z pochodzeniem ").Append(M0(_dest[OHomeKnown])).Append(" (H3 ").Append(M0(_homeBy[MBackH3])).Append(", 166 ").Append(M0(_homeBy[MBack166])).Append(", W4c ").Append(M0(_homeBy[MBackW4c]))
              .Append(", 179 ").Append(M0(_homeBy[MBack179])).Append(", z puli ").Append(M0(_homeBy[MBackPool])).Append(", jency ").Append(M0(_homeBy[MBackFreed])).Append(", rozwiazane ").Append(M0(_homeBy[MBackDisband]))
              .Append("; w tym do stanu miasta ").Append(M0(_homeTown)).Append("; bez wsi w regionie ").Append(M0(_homeNoVillage)).Append(")")
              .Append(", bez pochodzenia do BK/szablonu/nicosci ").Append(M0(_dest[OHomeLegacy]))
              .Append(", do puli ").Append(M0(_dest[OPool])).Append(", na Mur ").Append(M0(_dest[OWall])).Append(", do niewoli ").Append(M0(_dest[OSlave]))
              .Append(", w nicosc nazwana (rozbici bez puli, bez domu) ").Append(M0(_dest[OLost]))
              .Append(", do rosterow bez ksiegi (tabory, Inni, konwoje BK, lup gracza - P2) ").Append(M0(_dest[ODrop]))
              .Append(", do karczmy (WETERANI, E4/10) ").Append(M0(_vDestTake))
              .Append(", do zbiorki pod choragiew (N7) ").Append(M0(_dest[ORally]));
            sb.Append(" | bez nazwy: z niczego +").Append(M0(_zAll)).Append(" (");
            f = true;
            for (int k = 0; k < KKinds; k++) { if (_zBy[k] <= 0) continue; sb.Append(f ? "" : ", ").Append(KName[k]).Append(' ').Append(M0(_zBy[k])); f = false; }
            if (f) sb.Append('-');
            sb.Append("), w nicosc -").Append(M0(_qAll + _gone)).Append(" (");
            f = true;
            for (int k = 0; k < KKinds; k++) { if (_qBy[k] <= 0) continue; sb.Append(f ? "" : ", ").Append(KName[k]).Append(' ').Append(M0(_qBy[k])); f = false; }
            sb.Append(f ? "" : "; ").Append("przy zniknieciu partii ").Append(M0(_gone)).Append(")");
            if (_pendLost > 0) sb.Append(", oczekujace udzialy band porzucone ").Append(M0(_pendLost));
            if (_poolTrim > 0) sb.Append(", pula: znani ponad zolnierzy puli (bez okna) ").Append(M0(_poolTrim));
            sb.Append(" | przyciecie kluczy (ponizej ").Append(FoldMin.ToString("0.##", Inv)).Append(" czlowieka albo spoza ").Append(FoldTop).Append(" najwiekszych): ").Append(_foldKeys)
              .Append(" kluczy, ").Append(M0(_foldMen)).Append(" ludzi do glownego regionu slownika (suma bez zmian)");
            // poprawki E4 po tescie koncowym (fixA): odlozone uzgodnienie w bitwie (T3), ostrzal (P1-2), niedobor skladow wedlug stanu partii (P2-T4-sklady, diagnostyka)
            sb.Append(" | fixA: uzgodnienie odlozone (bitwa w toku) ").Append(_defParties).Append(" partii (roznica ").Append(M0(_defMen)).Append(" ludzi - zdejmie koniec bitwy)")
              .Append(", ostrzal w oblezeniu (poza bitwa): wywolan z ubytkiem ").Append(_siegeCalls).Append(", polegli ").Append(M0(_siegeDead))
              .Append(" | niedobor skladow wedlug stanu partii (diagnostyka P2-T4): ");
            f = true;
            for (int k = 0; k < SKinds; k++) { if (_cutBy[k] <= 0) continue; sb.Append(f ? "" : ", ").Append(SName[k]).Append(' ').Append(M0(_cutBy[k])); f = false; }
            if (f) sb.Append('-');
            sb.Append("; najwiekszy: ");
            if (_cutTop.Count == 0) sb.Append('-');
            for (int k = 0; k < _cutTop.Count; k++) sb.Append(k > 0 ? "; " : "").Append(_cutTop[k].Value);
            if (_screenNoInit > 0) { sb.Append(" | ekran druzyny bez stanu przed (_initialData) ").Append(_screenNoInit); _screenNoInit = 0; }
            // kontrola kodu: zmiana stanu = dopisane (Give + uzgodnienie) - zdjete (Take + uzgodnienie + znikniecie)
            sb.Append(" | kontrola: zmiana stanu - (wejscia - wyjscia + z - q) = ");
            if (double.IsNaN(_state0)) sb.Append("pierwsza doba sesji (od jutra)");
            else sb.Append((state - _state0 - (ins - outs + _zAll - _qAll)).ToString("0.00", Inv));
            sb.Append(" (stan wczoraj ").Append(double.IsNaN(_state0) ? "-" : N0(_state0)).Append(", wejscia ").Append(M0(ins)).Append(", wyjscia ").Append(M0(outs)).Append(')');
            sb.Append(" | DeduceManpower: wywolan ").Append(_deduceCalls).Append(" (ludzi ").Append(M0(_deduceMen)).Append("), poza oknem ").Append(_deduceOutside).Append(", gracz bez okna ").Append(_deduceNoWin)
              .Append(", pobor na podlodze ").Append(_floorCalls).Append(" (ludzi ").Append(M0(_floorMen)).Append(")")
              .Append(", odwrot wsi do stanu miasta regionu (fixA T6) ").Append(LevyVillageFromTownOn ? M0(_villTown) + " ludzi" : "WYLACZONY (Levy Village From Town)").Append(VillTownNames())
              .Append(", pobor miast wedlug udzialu (fix2 P1-4) ").Append(TownLevyByShareOn ? "z wsi regionu " + M0(_townShareVill) + " ludzi, wsie na podlodze - znow z miasta " + M0(_townShareBack) + " ludzi" : "WYLACZONY (Town Levy By Share)")
              .Append(", A5 z klasa BK (DeduceManpower) ").Append(_a5Bk).Append(_a5BkFail > 0 ? " (bledy " + _a5BkFail + ")" : "")
              .Append(", karczma AI z puli weteranow (E4/10 - bez zdjecia z DOM) ").Append(_deduceVetCalls).Append(" (ludzi ").Append(M0(_deduceVetMen)).Append(")")
              .Append(" | pobor z DOM LevyLive: ").Append(LevyLive ? "TAK" : "NIE (ksiega pochodzenia bez zdejmowania)");
            if (_resolveNote != null) { sb.Append(" | wczytanie: ").Append(_resolveNote); _resolveNote = null; }
            if (_badIds > 0) sb.Append(" | nie zapisane (znaki w id) ").Append(_badIds);
            if (_oStumblesDay > 0) sb.Append(" | potkniecia ").Append(_oStumblesDay).Append(" (razem ").Append(_oStumbles).Append(")");
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            sb.Append(" | czas ").Append(ms.ToString("0.0", Inv)).Append(" ms (uzgodnienie i regiony ").Append(_lastMs.ToString("0.0", Inv)).Append(" ms).");
            Log.Info(sb.ToString());
            try { FreeHands.Line(day); } catch (Exception e) { OStumble("FreeHands.Line", e); }
        }

        /// <summary>Koniec doby ksiegi pochodzenia (takze bez logu): stan kont do kontroli jutra, zerowanie licznikow doby.</summary>
        private static void OriginEndDay()
        {
            try
            {
                if (_sown && OriginOn) { double s, sk, j, jk; int regs; StateNow(out s, out sk, out j, out jk, out regs, null); _state0 = s + j; }
                else _state0 = double.NaN;
            }
            catch (Exception e) { _state0 = double.NaN; OStumble("OriginEndDay", e); }
            _poolTrim = 0;
            OriginNewDay();
        }

        // ================================================================ licznik DeduceManpower (latki - PeopleBook.OriginPatches.cs)
        internal static void NoteDeduce(int men, bool outside, bool noWin)
        {
            _deduceCalls++; _deduceMen += men;
            if (outside) _deduceOutside++;
            if (noWin) _deduceNoWin++;
        }
    }
}
