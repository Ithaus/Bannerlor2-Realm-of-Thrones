using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 469 (Jeff 12.11: "lucznicy zaczynaja strzelac na poczatku bitwy nawet jak nie maja zasiegu ... strzaly laduja w polu przed wojskiem, moze niech
    /// sprawdzaja czy jest zasieg i dopiero wtedy strzelaja"): lucznik / kusznik AI pieszo trzyma ogien, dopoki cel nie jest w zasiegu bojowym jego broni.
    /// Dowod (badanie 469, dekompilacja): decyzja o strzale jest natywna i patrzy na zasieg balistyczny (Agent.GetMissileRange, mb_full.cs:13520), nie na
    /// skuteczny; dla piechoty nie ma zadnego progu "strzelaj od X m"; RBM wlacza strzelcom IgnoreAmmoLimitForRangeCalculation (rbmai.cs:2509) i zdejmuje
    /// skrocenie zasiegu pogoda (rbmai.cs:3026). Przy krancu zasiegu, z rozrzutem pionowym RBM, duza czesc strzal spada przed celem.
    /// Zasieg bojowy G liczymy z tej samej predkosci, z ktora RBM naprawde wystrzeliwuje strzale (Utilities.calculateMissileSpeed, rbmcombat.cs:5466-5500:
    /// naciag w funtach z pola MissileSpeed + hit_points modyfikatora jakosci, waga strzaly z kolczana) - wiec zasieg zalezy od naciagu: G = 0.85 x zasieg
    /// maksymalny z oporem powietrza (AirFrictionArrow, wzor silnika v -= k v^2 dt; symulacja w C#, przechowana per predkosc i roznica wysokosci).
    /// Predkosci ani obrazen NIE zmieniamy (RBM liczy je sam; podwojne liczenie = rozjechane celowanie, badanie P3).
    /// Regula (klucz ArchersHoldFireOutOfRange; NIE = ten sam przeglad na sucho, bez wywolan silnika): bitwa polowa, czlowiek AI pieszo z lukiem lub kusza w reku
    /// (ludzie gracza pod SI tez; gracz nie): odleglosc do celu silnika (GetTargetAgent), bez celu - do skraju najblizszego szyku wroga (CachedClosestEnemyFormation).
    ///  d > 1.1 G - Agent.SetFiringOrder(HoldYourFire) (ponawiane przy kazdym przegladzie - zmiana rozkazu szyku nadpisuje rozkaz czlowieka, Formation.cs:69551);
    ///  d <= G - rozkaz ognia szyku wraca (Formation.FiringOrder); szyk z HoldYourFire - nie ruszamy.
    /// Zwolnienie od razu: wrog w 6 m (czlowiek walczy o zycie, jak 310), bron biala / pusty kolczan, panika / odwrot, kon, przejecie przez 204/283/148/447, chorazy.
    /// Pominieci: konni (RBM AiRangedHorsebackMissileRange 0.35), szyk piechoty (prefiks RBM OverrideAgent.SetFiringOrder, rbmai.cs:4403 - jak 310),
    /// poza szykiem, machiny, nieznany uzytek broni (RBM daje mu 10 m/s - nie zgadujemy). Jeden zarzadca wstrzyman z 310: rozkaz szyku wraca dopiero,
    /// gdy zaden powod wstrzymania nie trwa (CleanShot.Held / FireRange469.Holds).
    /// Budzet: przeglad wszystkich ludzi misji co ok. 0.5 s (ludzi na klatke wg dt, 8..96), zapytan o wroga w 6 m najwyzej 16 na klatke, nowych symulacji lotu najwyzej 2 w przegladzie i 2 przy strzalach (wynik przechowany per predkosc i koszyk wysokosci).
    /// Pomiar (linia "Ogien w zasiegu (469)" raz na bitwe polowa): strzaly AI z luku / kuszy wg odleglosci do celu, strzaly poza G, pierwsza salwa kazdej strony
    /// (6 s od pierwszego strzalu: sredni dystans vs srednie G i natywny GetMissileRange), wstrzymania, czas, bronie (naciag, predkosc, zasieg, G, natywny).
    /// 469-b (klucz HistoricalBowAndCrossbowRange): G dodatkowo ograniczone zasiegiem historycznym wg rodzaju i naciagu (HistRange) - tylko prog decyzji,
    /// predkosc strzaly zostaje z RBM (zmiana MissileSpeed = podwojne liczenie naciagu i rozjechane celowanie, badanie P3).
    /// Linia "Zasieg lukow (469)" na start sesji: tabela kilku typowych broni. Potkniecia liczone (RoutHold miejsce "tarcze"), funkcji nie gasimy.
    /// 469-p (recenzja): szyk pod rozkazami gracza pominiety; wylacznik zastoju (20 s bez zblizenia celu o 10 m - ogien wolny, 30 s przerwy) i ta sama przerwa
    /// po zwolnieniu z powodu broni; Fly() bez wierzcholka lotu dla celu wyzej; w linii natywny statyczny zasieg i licznik "najblizszy szyk wroga w G".
    /// 469-q (Jeff 13.11: "jak nie ma zasiegu to nie strzelaja ... maja strzelac po 20 sekundach jak wroga nie ma w zasiegu"): wylacznik zastoju i przerwa
    /// 469-p USUNIETE - poza G czlowiek w regule nie strzela nigdy (wstrzymanie juz od G, nie od 1.1 G). Pat RBM (rbmai.cs:8580-8650: szyk na num2 = pol natywnego
    /// MaximumMissileRange + glebokosci, w Approaching num < 0.9 num2 od razu wraca do Shooting) rozwiazuje PODEJSCIE SZYKU: szyk strzelcow AI pieszo, w ktorym
    /// >= 30% ludzi (min. 3) jest wstrzymanych przez 469, a najblizszy pieszy szyk wroga przez AdvStall s nie podszedl o AdvStallD m - Move do punktu, gdzie skraj
    /// wroga jest w 0.9 x sr. G wstrzymanych od srodka szyku; najwyzej CoverAhead m przed frontem najdalej wysunietej wlasnej piechoty; nie przy jezdzie wroga
    /// w CavNear m (wtedy stoi i nie strzela; w trakcie - zatrzymanie w miejscu). Tylko gdy czynne zachowanie szyku to RBMBehaviorArcherSkirmish: postfiks
    /// CalculateCurrentOrder (Priority.Last po com.rbmai, wzor 439 H439HighPost) wpisuje Move z tablicy slotow (watek AI - tylko odczyt referencji slotu); RBM
    /// TickOccasionally sam go wydaje (SetMovementOrder, przez arbitra 344). Koniec: dotarl / ludzie w G (wstrzymanych < 30%) / jazda / 60 s / zachowanie inne,
    /// 441 / 443 / gracz; potem RBM (jego CurrentOrder zostaje naszym punktem albo "stoj tu" przy przerwaniu). Szyk gracza, konni strzelcy, oblezenia (469 tylko
    /// w bitwie polowej) - bez ruchu.
    /// 469-q-p (recenzja 469-q): wstrzymany z bronia biala, ale z lukiem / kusza i amunicja w ekwipunku zostaje wstrzymany (bez petli zwolnienie -> FireAtWill);
    /// histereza LowMax s na "za malo wstrzymanych"; bez budzetu zapytan / symulacji - wstrzymanie od razu; strzaly poza G po wrogu w 6 m liczone osobno;
    /// punkt Move = front szyku (cel dla srodka + pol glebokosci), "stoj tu" = obecny front; postfiks wpisuje rozkaz tylko czynnemu zachowaniu.
    /// 469-r (Jeff 13.11: "czesc lucznikow ma luki z zasiegiem na 100 metrow a czesc na 200 ... czy ci co maja zasieg strzelaja"; test at-472b: prawie wszystkie
    /// strzaly oddali lucznicy szyku gracza - sr. 345-410 m przy G 200 m, pominieci jako "szyk gracza"): (1) szyk pod rozkazami gracza (IsAIControlled false) -
    /// ludzie wstrzymani poza swoim G jak w szyku AI (klucz PlayerArchersHoldFireOutOfRange, NIE = pominieci jak przed 469-r); szyku gracza NIE przesuwamy
    /// (Form: ai false), rozkaz HoldYourFire gracza zostaje (szyk wstrzymany - nic), bohater gracza (IsMainAgent / IsPlayerControlled) nietkniety. (2) strzelcy
    /// w szyku piechoty (AI i gracza) - wstrzymani poza G, szyk piechoty bez ruchu (Form: IsRangedFormation false); prefiks RBM OverrideAgent.SetFiringOrder
    /// (rbmai.cs:4403) zmienia rozkaz tylko przy ChargeWithTarget i szarzy jazdy wroga (hamowanie), inaczej przepuszcza. (3) podejscie szyku AI: cel = 30. percentyl
    /// G lucznikow szyku w regule (ok. 70% ma skraj wroga w zasiegu; lucznicy z dluzszym G zwolnieni w drodze), "dosc wstrzymanych" liczone od lucznikow
    /// w regule (nie od calego szyku). (4) linia: strzaly poza G osobno szyk AI / gracza / piechoty, wstrzymania gracza i piechoty.
    /// </summary>
    internal static class FireRange469
    {
        // 469-q: wstrzymanie od G (HoldOver 1.0; 469-a/p: 1.1 G - strzal miedzy G a 1.1 G byl mozliwy); OldOver - dawny licznik "poza 1.1 G" do porownania
        internal const float Every = 0.5f, HoldOver = 1.0f, OldOver = 1.1f, Eff = 0.85f, ThreatR = 6f, FirstVolley = 6f, ArrowW = 0.043f, BoltW = 0.06f, DzStep = 5f;
        // 469-q: podejscie szyku (zastep wylacznika zastoju 469-p): zastoj AdvStall s bez zblizenia wroga o AdvStallD m, >= AdvShare ludzi szyku (min. AdvMinMen)
        // wstrzymanych; cel AdvFrac x sr. G; krok >= AdvMinStep; oslona: front szyku najwyzej CoverAhead m przed frontem wlasnej piechoty; jazda wroga w CavNear m -
        // bez podejscia; podejscie najwyzej AdvMaxT s; postfiks musi odebrac rozkaz w NoHitT s; zatrzymanie trzymane do odbioru, najwyzej StopMax s
        // 469-r: AdvPct - cel podejscia = percentyl G lucznikow szyku (0.3 -> ok. 70% ma wroga w zasiegu; 469-q: 0.9 x sr. G wstrzymanych);
        // GFresh - G czlowieka z przegladu starszego niz tyle s nie wchodzi do percentyla (469-r-p U3: 3 s - pelny obieg przegladu przy ~1500 ludziach i 10 kl./s trwa ~1.6 s)
        // 469-r-p (U1): cel podejscia = (1 - AdvPct) percentyl niedoboru d - G lucznikow szyku (kazdy od swojej pozycji do swojego celu) + AdvReach (tolerancja konca EnReached)
        internal const float FormEvery = 1f, AdvStall = 15f, AdvStallD = 10f, AdvShare = 0.3f, AdvPct = 0.3f, GFresh = 3f, AdvMinStep = 8f, CoverAhead = 5f, CavNear = 80f;
        internal const float AdvMaxT = 60f, AdvReach = 10f, NoHitT = 5f, StopMax = 5f;
        // 469-q-p: NearKeep - strzal po wrogu w 6 m liczony osobno przez tyle s; LowMax - kolejne sekundy "za malo wstrzymanych", po ktorych zastoj / podejscie
        // konczy sie (histereza, recenzja U1)
        internal const float NearKeep = 1.5f;
        internal const int LowMax = 3;
        internal const int AdvMinMen = 3, CavMinMen = 3, CoverMinMen = 5, SlotTeams = 16;
        internal const int MinPer = 8, MaxPer = 96, NearPer = 16, SimPer = 2, WeapMax = 8;
        // 469-r-p (U2): osobny budzet zapytan o wroga w 6 m dla wstrzymanych (sciezka held nie dzieli budzetu z nowymi wstrzymaniami)
        internal const int NearHeldPer = 32;
        // koniec podejscia: 0 dotarl, 1 ludzie w G, 2 jazda, 3 limit czasu, 4 inne zachowanie / 441 / 443, 5 przejete (gracz, szyk pusty, nie pieszy strzelec), 6 bez odbioru
        private const int EnReached = 0, EnInG = 1, EnCav = 2, EnTime = 3, EnBeh = 4, EnTaken = 5, EnNoHit = 6, EnK = 7;
        // zastoj bez podejscia (sekundy szyku): 0 jazda, 1 oslona piechoty, 2 inne zachowanie, 3 441 / 443, 4 juz blisko, 5 bez latki / slotu
        private const int BkCav = 0, BkCover = 1, BkBeh = 2, Bk443 = 3, BkClose = 4, BkPatch = 5, BkK = 6;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class St
        {
            public Agent A;
            public bool Held;
            public float Since;
            public float G;              // 469-q: G z ostatniego przegladu (srednia szyku dla podejscia)
        }

        // 469-q: slot podejscia [TeamIndex x 10 + FormationIndex] - pisze glowny watek (podmiana referencji), czyta postfiks w watku AI
        private sealed class Slot
        {
            public Formation F;
            public MovementOrder Order;
            public int Hits;             // odbiory przez postfiks (Interlocked)
        }

        private sealed class FS
        {
            public float Since = -1f, BestDe, ProgT;
            public int Low;              // 469-q-p: kolejne sekundy bez dosc wstrzymanych (histereza)
            public Slot Adv;
            public bool Stopping;
            public float AdvT0, StopT;
            public Vec2 AdvTo;
        }

        private struct WKey : IEquatable<WKey>
        {
            public ItemObject W; public ItemModifier M; public ItemObject Ammo;
            public bool Equals(WKey o) { return ReferenceEquals(W, o.W) && ReferenceEquals(M, o.M) && ReferenceEquals(Ammo, o.Ammo); }
            public override bool Equals(object o) { return o is WKey && Equals((WKey)o); }
            public override int GetHashCode()
            {
                int h = W != null ? W.GetHashCode() : 0;
                h = h * 31 + (M != null ? M.GetHashCode() : 0);
                return h * 31 + (Ammo != null ? Ammo.GetHashCode() : 0);
            }
        }

        private sealed class Wp
        {
            public string Name, Usage;
            public int Lb, V;          // naciag (funty, z hit_points modyfikatora) i predkosc RBM (m/s); bez RBM Lb = 0, V = predkosc gry
            public int Hist;           // 469-b: zasieg historyczny wg rodzaju i naciagu (m); 0 = brak (bez RBM - naciag nieznany)
            public bool Ok;            // false = nieznany uzytek (RBM 10 m/s) - czlowiek pominiety
            public int Shots, NatN, GN;
            public float NatSum, GSum;
            public float NatStatic = -1f;   // 469-p: natywny Mission.GetMissileRange(v, 0) (mb_full.cs:53901) - czy silnik liczy zasieg z oporem powietrza
        }

        private static WeakReference _m;
        private static bool _live, _key, _written, _rbm, _hist, _player;   // 469-r: _player - szyk pod rozkazami gracza tez w regule (tylko z _key)
        private static string _kind = "?";
        private static float _k = 0.003f;
        private static int _rot, _frameNear, _frameNearH, _frameSim;
        private static long _sumTicks, _maxTicks;

        private static readonly Dictionary<Agent, St> By = new Dictionary<Agent, St>();
        private static readonly Dictionary<WKey, Wp> Weap = new Dictionary<WKey, Wp>();
        private static readonly Dictionary<int, float> RangeCache = new Dictionary<int, float>();   // klucz: v * 64 + koszyk roznicy wysokosci
        private static readonly List<Wp> WeapSeen = new List<Wp>();
        private static readonly MBList<Agent> TBuf = new MBList<Agent>();
        private static readonly List<Agent> Drop = new List<Agent>();
        // naciag z danych broni zapisany na start sesji (poza misja - bez okna, w ktorym RBM podmienia MissileSpeed na m/s przy spawnie / strzale)
        private static readonly Dictionary<ItemObject, int> LbAtStart = new Dictionary<ItemObject, int>();
        // 469-q: podejscie szyku - sloty (postfiks), stan szykow, tally wstrzymanych na szyk (glowny watek)
        private static readonly Slot[] Slots = new Slot[SlotTeams * 10];
        private static readonly Dictionary<Formation, FS> FBy = new Dictionary<Formation, FS>();
        // 469-r: G kazdego lucznika w regule z ostatniego przegladu (wstrzymanego i nie) - percentyl szyku do podejscia (tylko klucz TAK)
        private sealed class GE { public float G, S, T; public Formation F; }   // 469-r-p (U1): S - niedobor d - G (m; <= 0 = wrog w zasiegu)
        private static readonly Dictionary<Agent, GE> GOf = new Dictionary<Agent, GE>();
        private static readonly Dictionary<Formation, List<float>> FGs = new Dictionary<Formation, List<float>>();
        // 469-r-p: niedobory d - G (U1) i wstrzymani ze swiezym wpisem (U3) wg szyku
        private static readonly Dictionary<Formation, List<float>> FSh = new Dictionary<Formation, List<float>>();
        private static readonly Dictionary<Formation, int> FHeld = new Dictionary<Formation, int>();
        private static readonly Dictionary<Formation, int> HeldN = new Dictionary<Formation, int>();
        private static readonly Dictionary<Formation, float> HeldG = new Dictionary<Formation, float>();
        private static readonly List<Formation> FTmp = new List<Formation>();
        private static readonly HashSet<Formation> AdvForms = new HashSet<Formation>();
        private static Type _tArch;
        private static bool _patchOk;
        private static string _patch = "nie wpiete";
        private static int _postErr;   // watek AI - Interlocked
        private static float _formAcc, _depT = -1f;

        // liczniki misji
        private static int _scanned, _archers, _holds, _heldMen, _reHold, _wouldHold, _stum, _overNear, _overSim, _overNearH;
        private static int _relRange, _relNear, _relWeapon, _relPanic, _relTaken, _relMount, _relForm, _relNoFoe, _relEnd, _relDead;
        private static int _skMount, _skInf, _skDetached, _skObject, _skUsage, _skFormHold, _skPlayer, _skNoAmmo, _skHunt, _skBanner, _skPanic;
        private static int _heldNoTarget, _shots, _shotsOver, _shotsHeld, _shotsNoTarget;
        private static int _heldFoeInG;   // 469-p: wstrzymany choc najblizszy szyk wroga w G (tylko pomiar)
        // 469-q: strzaly poza G (wszystkie / ludzie w regule), pierwszy wrog w G wg strony, podejscia
        private static int _shotsOverG, _shotsOverGRule;
        // 469-q-p: przeglady wstrzymanych z lukiem schowanym (U1)
        private static int _heldStowed;
        // 469-r: wg rodzaju szyku [0] strzelcy AI, [1] szyk gracza, [2] szyk piechoty (AI i gracza): strzaly poza G (bez / po wrogu w 6 m - walka o zycie), wstrzymania, ludzie
        private const int CAi = 0, CPl = 1, CInf = 2, CK = 3;
        private static readonly int[] OverG = new int[CK], OverGNear = new int[CK], HoldsBy = new int[CK], HeldMenBy = new int[CK];
        private static readonly Dictionary<Agent, float> NearT = new Dictionary<Agent, float>();
        private static readonly float[] InGT0 = new float[2];
        private static int _adv, _advCovered, _advPull, _advDe1N, _advHits;
        private static float _advDe0Sum, _advToSum, _advGSum, _advDe1Sum, _advSecs, _cavHeldSec, _advNaSum;
        // 469-r-p (U1): niedobor w percentylu przy starcie podejscia; udzial lucznikow szyku z wrogiem w zasiegu na koniec podejscia (cel ok. 0.7)
        private static float _advShSum, _advInSum;
        private static int _advShN, _advInN;
        private static readonly int[] AdvEnd = new int[EnK];
        private static readonly float[] Blk = new float[BkK];
        private static readonly int[] ShotBy = new int[5];
        private static float _holdSecs, _holdMax;
        private static int _holdN;
        private static readonly HashSet<Agent> HeldEver = new HashSet<Agent>();
        // pierwsza salwa: [0] obrona, [1] atak
        private static readonly float[] FvT0 = new float[2], FvD = new float[2], FvG = new float[2], FvNat = new float[2];
        private static readonly int[] FvN = new int[2], FvNatN = new int[2], FvGN = new int[2];

        internal static bool Live { get { return _live; } }

        // ------------------------------------------------------------ start misji / koniec

        /// <summary>ShieldsUp.Begin (po CleanShot.Begin): stan od zera; tylko bitwa polowa (jak 310).</summary>
        internal static void Begin(Mission m, Settings c)
        {
            Clear();
            _live = false;
            _written = false;
            _m = null;
            if (m == null) return;
            bool field = false;
            try { field = m.IsFieldBattle && !m.IsNavalBattle && !m.IsNavalRaidBattle && !HideoutAlarm.IsHideout(m); } catch { field = false; }
            if (!field) return;
            _m = new WeakReference(m);
            _kind = MissionTeams.Kind(m);
            _key = c == null || c.ArchersHoldFireOutOfRange;
            _rbm = RangedRank.RbmLoaded;
            _hist = c == null || c.HistoricalBowAndCrossbowRange;
            _player = _key && (c == null || c.PlayerArchersHoldFireOutOfRange);
            _k = Friction();
            _live = true;
        }

        private static float Friction()
        {
            try
            {
                float k = ManagedParameters.Instance.GetManagedParameter(ManagedParametersEnum.AirFrictionArrow);
                if (k > 0f && k < 0.1f) return k;
            }
            catch { }
            return 0.003f;
        }

        private static void Clear()
        {
            By.Clear(); Weap.Clear(); WeapSeen.Clear(); TBuf.Clear(); Drop.Clear(); HeldEver.Clear();
            _heldFoeInG = 0;
            // 469-q: sloty najpierw (postfiks w watku AI przestaje wpisywac), potem stan szykow
            for (int i = 0; i < Slots.Length; i++) Volatile.Write(ref Slots[i], null);
            FBy.Clear(); HeldN.Clear(); HeldG.Clear(); FTmp.Clear(); AdvForms.Clear();
            _formAcc = 0f; _depT = -1f;
            _shotsOverG = _heldStowed = 0;
            Array.Clear(OverG, 0, CK); Array.Clear(OverGNear, 0, CK); Array.Clear(HoldsBy, 0, CK); Array.Clear(HeldMenBy, 0, CK);
            GOf.Clear(); FGs.Clear(); FSh.Clear(); FHeld.Clear();
            _advShSum = _advInSum = 0f; _advShN = _advInN = 0;
            NearT.Clear();
            InGT0[0] = InGT0[1] = -1f;
            _adv = _advCovered = _advPull = _advDe1N = _advHits = 0;
            _advDe0Sum = _advToSum = _advGSum = _advDe1Sum = _advSecs = _cavHeldSec = _advNaSum = 0f;
            Array.Clear(AdvEnd, 0, AdvEnd.Length); Array.Clear(Blk, 0, Blk.Length);
            Interlocked.Exchange(ref _postErr, 0);
            _rot = _frameNear = _frameNearH = _frameSim = 0;
            _sumTicks = _maxTicks = 0;
            _scanned = _archers = _holds = _heldMen = _reHold = _wouldHold = _stum = _overNear = _overSim = _overNearH = 0;
            _relRange = _relNear = _relWeapon = _relPanic = _relTaken = _relMount = _relForm = _relNoFoe = _relEnd = _relDead = 0;
            _skMount = _skInf = _skDetached = _skObject = _skUsage = _skFormHold = _skPlayer = _skNoAmmo = _skHunt = _skBanner = _skPanic = 0;
            _heldNoTarget = _shots = _shotsOver = _shotsHeld = _shotsNoTarget = 0;
            Array.Clear(ShotBy, 0, ShotBy.Length);
            _holdSecs = _holdMax = 0f; _holdN = 0;
            for (int i = 0; i < 2; i++) { FvT0[i] = -1f; FvD[i] = FvG[i] = FvNat[i] = 0f; FvN[i] = FvNatN[i] = FvGN[i] = 0; }
        }

        private static bool Mine(Mission m)
        {
            var w = _m;
            return _live && m != null && w != null && ReferenceEquals(w.Target, m);
        }

        private static void Stumble(string where, Exception e)
        {
            _stum++;
            RoutHold.Stumble(RoutHold.SiteShields, where, e);
        }

        /// <summary>310 (CleanShot.EndHold): czy 469 wciaz trzyma ogien tego czlowieka - wtedy 310 nie przywraca rozkazu szyku.</summary>
        internal static bool Holds(Agent a)
        {
            if (!_live || a == null) return false;
            St s;
            return By.TryGetValue(a, out s) && s.Held;
        }

        // ------------------------------------------------------------ bron: naciag, predkosc RBM, zasieg

        /// <summary>Bron w reku (luk / kusza) i jej dane; null = brak broni strzeleckiej albo amunicji.</summary>
        private static Wp Weapon(Agent a, bool needAmmo, out bool noAmmo)
        {
            noAmmo = false;
            var eq = a.Equipment;
            if (eq == null) return null;
            return WeaponAt(eq, a.GetPrimaryWieldedItemIndex(), needAmmo, out noAmmo);
        }

        /// <summary>469-q-p (recenzja U1): luk / kusza z amunicja gdziekolwiek w ekwipunku (nie w reku) - wstrzymany czlowiek, ktoremu natywna AI przy
        /// HoldYourFire dala bron biala, zostaje wstrzymany (log 469-p: zwolnien "bron / kolczan" ~ wstrzyman, czas sr. 0.5 s). null = zadnej broni strzeleckiej z amunicja.</summary>
        private static Wp Stowed(Agent a)
        {
            var eq = a.Equipment;
            if (eq == null) return null;
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
            {
                bool na;
                var p = WeaponAt(eq, i, true, out na);
                if (p != null) return p;
            }
            return null;
        }

        private static Wp WeaponAt(MissionEquipment eq, EquipmentIndex wi, bool needAmmo, out bool noAmmo)
        {
            noAmmo = false;
            if (wi < EquipmentIndex.WeaponItemBeginSlot || wi >= EquipmentIndex.NumAllWeaponSlots) return null;
            var w = eq[wi];
            if (w.IsEmpty) return null;
            var u = w.CurrentUsageItem;
            if (u == null || (u.WeaponClass != WeaponClass.Bow && u.WeaponClass != WeaponClass.Crossbow)) return null;
            if (needAmmo && eq.GetAmmoAmount(wi) <= 0) { noAmmo = true; return null; }
            // waga strzaly jak RBM (rbmcombat.cs:4544-4554): naladowana amunicja, inaczej pierwszy kolczan z zapasem; bez niej 0.07 kg
            ItemObject ammo = null;
            float aw = 0.07f;
            var ld = w.AmmoWeapon;
            if (!ld.IsEmpty && ld.Item != null && ld.Amount > 0) { ammo = ld.Item; aw = ld.GetWeight() / ld.Amount; }
            else
            {
                var want = u.WeaponClass == WeaponClass.Bow ? ItemObject.ItemTypeEnum.Arrows : ItemObject.ItemTypeEnum.Bolts;
                for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
                {
                    var x = eq[i];
                    if (x.IsEmpty || x.Item == null || x.Item.ItemType != want || x.Amount <= 0) continue;
                    ammo = x.Item; aw = x.GetWeight() / x.Amount;
                    break;
                }
            }
            var key = new WKey { W = w.Item, M = w.ItemModifier, Ammo = ammo };
            Wp p;
            if (Weap.TryGetValue(key, out p)) return p;
            p = new Wp { Name = w.Item != null ? w.Item.StringId : "?", Usage = u.ItemUsage ?? "" };
            if (_rbm)
            {
                int lb;
                if (w.Item == null || !LbAtStart.TryGetValue(w.Item, out lb)) lb = u.MissileSpeed;
                int hp = 0;
                try { if (w.ItemModifier != null) hp = w.ItemModifier.ModifyHitPoints((short)50) - 50; } catch { hp = 0; }
                p.Lb = lb + hp;
                p.Ok = Known(p.Usage);
                p.V = p.Ok ? RbmSpeed(aw, p.Usage, p.Lb) : 0;
                p.Hist = p.Ok ? HistRange(p.Usage, p.Lb) : 0;
            }
            else
            {
                p.Lb = 0;
                try { p.V = w.GetModifiedMissileSpeedForCurrentUsage(); } catch { p.V = u.MissileSpeed; }
                p.Ok = p.V > 5;
            }
            if (p.Ok && p.V > 0) { try { p.NatStatic = Mission.GetMissileRange(p.V, 0f); } catch { p.NatStatic = -1f; } }
            Weap[key] = p;
            if (WeapSeen.Count < 64) WeapSeen.Add(p);
            return p;
        }

        private static bool Known(string usage)
        {
            return usage == "bow" || usage == "long_bow" || usage == "crossbow" || usage == "crossbow_fast";
        }

        /// <summary>Predkosc strzaly wedlug RBM Utilities.calculateMissileSpeed (rbmcombat.cs:5466-5500) - co do wzoru.</summary>
        internal static int RbmSpeed(float ammoWeight, string usage, int drawWeight)
        {
            float stroke = 0.635f, eff = 0.9f, c = 0.00015f;
            if (usage == "long_bow") { eff = 0.835f; c = 0.00018f; }
            else if (usage == "crossbow" || usage == "crossbow_fast") { stroke = 0.508f; eff = 0.88f; }
            else if (usage != "bow") return 10;
            double e = 0.5f * ((float)drawWeight * 4.448f) * stroke * eff;
            float m = ammoWeight + (float)drawWeight * c;
            if (!(m > 0f)) return 10;
            return (int)Math.Floor(Math.Sqrt(e * 2.0 / (double)m));
        }

        /// <summary>Zasieg maksymalny z oporem (m), wystrzal z 1.5 m nad ziemia, cel o dz wyzej. Przechowany per (v, koszyk 5 m). -1 = bez budzetu klatki.</summary>
        private static float MaxRange(int v, float dz, bool budget)
        {
            int b = (int)Math.Round(dz / DzStep);
            if (b < -6) b = -6; else if (b > 6) b = 6;
            int key = v * 64 + (b + 6);
            float r;
            if (RangeCache.TryGetValue(key, out r)) return r;
            if (budget && _frameSim >= SimPer) { _overSim++; return -1f; }
            _frameSim++;
            r = Simulate(v, b * DzStep, _k);
            RangeCache[key] = r;
            return r;
        }

        /// <summary>Lot strzaly: najwiekszy zasieg po katach 4..60 st (co 2 st, potem +-1 st wokol najlepszego), krok 0.01 s (wzor silnika: v -= k v^2 dt).</summary>
        internal static float Simulate(float v, float dz, float k)
        {
            if (!(v > 0f)) return 0f;
            float best = 0f;
            int bestAng = 0;
            for (int ang = 4; ang <= 60; ang += 2)
            {
                float x = Fly(v, ang, dz, k);
                if (x > best) { best = x; bestAng = ang; }
            }
            if (bestAng > 0)
            {
                float lo = Fly(v, bestAng - 1, dz, k), hi = Fly(v, bestAng + 1, dz, k);
                if (lo > best) best = lo;
                if (hi > best) best = hi;
            }
            return best;
        }

        /// <summary>Jeden lot pod katem ang (st): odleglosc, na ktorej strzala schodzi na wysokosc celu; 0 = nie dochodzi (469-p: takze gdy cel wyzej,
        /// a strzala ani razu nie wzniosla sie na jego wysokosc - dawniej zwracalo odleglosc wierzcholka lotu).</summary>
        private static float Fly(float v, int ang, float dz, float k)
        {
            const float dt = 0.01f, g = 9.81f;
            double a = ang * Math.PI / 180.0;
            float vx = v * (float)Math.Cos(a), vz = v * (float)Math.Sin(a), x = 0f, z = 1.5f - dz;
            bool up = z >= 0f;
            for (int s = 0; s < 3000; s++)
            {
                float sp = (float)Math.Sqrt(vx * vx + vz * vz);
                float f = k * sp;
                vx -= f * vx * dt;
                vz -= (f * vz + g) * dt;
                x += vx * dt;
                z += vz * dt;
                if (z >= 0f) up = true;
                else if (vz < 0f) return up ? x : 0f;
            }
            return 0f;
        }

        /// <summary>Zasieg bojowy G (m) dla broni i roznicy wysokosci; -1 = bez budzetu klatki (nastepny przeglad).
        /// 469-b (klucz HistoricalBowAndCrossbowRange): G = min(zasieg historyczny wg naciagu, 0.85 x zasieg fizyczny) - fizyka skraca pod gore.</summary>
        private static float Gate(Wp p, float dz, bool budget)
        {
            float r = MaxRange(p.V, dz, budget);
            if (r < 0f) return -1f;
            float g = Eff * r;
            if (_hist && p.Hist > 0 && p.Hist < g) g = p.Hist;
            return g;
        }

        /// <summary>469-b: zasieg bojowy (salwa w szyk, nie strzal celowany w pojedynczego czlowieka) wg rodzaju broni i naciagu w skali RBM (pole
        /// MissileSpeed + hit_points modyfikatora). Wartosci z historii (badanie 469 rozdz. 3, wiedza - niepewnosc srednia do duzej): luk mysliwski / krotki
        /// 30-60 lb - salwa 100-180 m; refleksyjny stepowy 90-120 lb - 150-250 m bojowo; dlugi angielski 140-160 lb - 200-250 m ciezka strzala; kusza lekka
        /// (strzemie, hak) - 150-250 m; ciezka (kolowrot, korba) - 250-350 m maks., skuteczna 60-100 m. RBM ma kusze 80-330 lb przy skoku 0.508 m (realnie
        /// 0.10-0.18 m) - progi kusz w skali RBM. 0 = nieznany uzytek.</summary>
        internal static int HistRange(string usage, int lb)
        {
            switch (usage)
            {
                case "bow": return lb < 70 ? 100 : lb < 110 ? 150 : lb < 150 ? 180 : 200;
                case "long_bow": return lb < 110 ? 130 : lb < 150 ? 170 : 200;
                case "crossbow_fast": return 110;
                case "crossbow": return lb < 250 ? 140 : 170;
                default: return 0;
            }
        }

        // ------------------------------------------------------------ przeglad (ShieldsUp.Tick, glowny watek)

        internal static void Tick(Mission m, float dt)
        {
            if (!Mine(m)) return;
            bool deployed = false;
            try { deployed = m.IsDeploymentFinished; } catch { deployed = false; }
            if (!deployed) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                _frameNear = 0;
                _frameNearH = 0;
                _frameSim = 0;
                float now = m.CurrentTime;
                var list = m.Agents;
                int n = list != null ? list.Count : 0;
                if (n > 0)
                {
                    int per = (int)Math.Ceiling(n * Math.Max(dt, 0f) / Every);
                    if (per < MinPer) per = MinPer; else if (per > MaxPer) per = MaxPer;
                    if (per > n) per = n;
                    int start = _rot % n;
                    for (int k = 0; k < per; k++)
                    {
                        var a = list[(start + k) % n];
                        if (a == null || !a.IsHuman) continue;
                        try { Look(m, a, now); }
                        catch (Exception e) { Stumble("FireRange469.Czlowiek", e); St s; if (By.TryGetValue(a, out s)) End(s, now, true); }
                    }
                    _rot = start + per;
                    if (_rot > 1 << 30) _rot = 0;
                }
                if (By.Count > 0)
                {
                    Drop.Clear();
                    foreach (var kv in By) if (!kv.Value.Held) Drop.Add(kv.Key);
                    for (int i = 0; i < Drop.Count; i++) By.Remove(Drop[i]);
                    Drop.Clear();
                }
                // 469-q: podejscie szyku co FormEvery (tylko klucz TAK - przy NIE nikt nie jest wstrzymany, FBy puste)
                if (_depT < 0f) _depT = now;
                _formAcc += Math.Max(dt, 0f);
                if (_formAcc >= FormEvery)
                {
                    _formAcc = 0f;
                    if (_key && (By.Count > 0 || FBy.Count > 0)) Forms(m, now);
                }
            }
            catch (Exception e) { Stumble("FireRange469.Przeglad", e); }
            long dtk = Stopwatch.GetTimestamp() - t0;
            _sumTicks += dtk;
            if (dtk > _maxTicks) _maxTicks = dtk;
        }

        private static void Look(Mission m, Agent a, float now)
        {
            St s;
            By.TryGetValue(a, out s);
            bool held = s != null && s.Held;
            if (!a.IsActive()) { if (held) { _relDead++; End(s, now, false); } return; }
            if (a.IsMainAgent || a.IsPlayerControlled || !a.IsAIControlled) { if (held) { _relTaken++; End(s, now, true); } return; }
            bool noAmmo;
            var p = Weapon(a, true, out noAmmo);
            if (p == null)
            {
                // 469-q-p (U1): wstrzymany bez luku w reku, ale z lukiem / kusza i amunicja w ekwipunku - zostaje wstrzymany (bron biala: HoldYourFire bez znaczenia);
                // zwolnienie tylko bez amunicji w calym ekwipunku (dawniej End -> FireAtWill -> luk -> wstrzymanie: petla co 0.5 s)
                if (held) p = Stowed(a);
                if (p == null)
                {
                    if (held) { _relWeapon++; End(s, now, true); }
                    else if (noAmmo) _skNoAmmo++;
                    return;
                }
                _heldStowed++;
            }
            _scanned++;
            if (a.HasMount) { if (held) { _relMount++; End(s, now, true); } else _skMount++; return; }
            if (HorsePursuit.HuntAnyThread(a) || HorsePursuit.RidingAnyThread(a) || Doctrine.H447AnyThread(a)) { if (held) { _relTaken++; End(s, now, true); } else _skHunt++; return; }
            if (HorsePursuit.Banner(a)) { if (held) { _relTaken++; End(s, now, true); } else _skBanner++; return; }
            if (a.IsRunningAway || SurvivalMode.InPanic(a) || a.IsRetreating()) { if (held) { _relPanic++; End(s, now, true); } else _skPanic++; return; }
            var f = a.Formation;
            if (f == null || a.IsDetachedFromFormation) { if (held) { _relForm++; End(s, now, true); } else _skDetached++; return; }
            // 469-p: szyk pod rozkazami gracza (nie pod SI) - jego rozkaz ognia zostaje (Formation.IsAIControlled, mb_full.cs:68947 / 69563)
            // 469-r: z kluczem gracza (PlayerArchersHoldFireOutOfRange, tylko przy _key) - w regule jak szyk AI (bez ruchu szyku, HoldYourFire gracza nizej - nic)
            bool pl = !f.IsAIControlled;
            if (pl && !_player) { if (held) { _relTaken++; End(s, now, true); } else _skPlayer++; return; }
            if (a.IsUsingGameObject) { if (held) { _relForm++; End(s, now, true); } else _skObject++; return; }
            var q = f.QuerySystem;
            // 469-r: strzelec w szyku piechoty (AI i gracza) w regule przy kluczu TAK; prefiks RBM OverrideAgent.SetFiringOrder (rbmai.cs:4403) zmienia rozkaz tylko
            // przy ChargeWithTarget i szarzy jazdy wroga (hamowanie / ogien), inaczej przepuszcza. NIE - pominiety jak przed 469-r
            bool inf = q != null && q.IsInfantryFormation;
            if (inf && !_key) { if (held) { _relForm++; End(s, now, true); } else _skInf++; return; }
            int cat = inf ? CInf : pl ? CPl : CAi;
            if (f.FiringOrder.OrderEnum == FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire) { if (held) { _relForm++; s.Held = false; Close(s, now); } else _skFormHold++; return; }
            if (!p.Ok) { if (held) { _relWeapon++; End(s, now, true); } else _skUsage++; return; }
            _archers++;
            // odleglosc: cel silnika, bez celu - skraj najblizszego szyku wroga
            float d, dz = 0f;
            bool noTarget = false;
            Agent t = null;
            try { t = a.GetTargetAgent(); } catch { t = null; }
            Agent tm = t != null && t.IsMount ? t.RiderAgent : t;
            if (t != null && t.IsActive() && tm != null && tm.Team != null && a.Team != null && tm.Team.IsEnemyOf(a.Team))
            {
                Vec3 ap = a.Position, tp = t.Position;
                d = ap.AsVec2.Distance(tp.AsVec2);
                dz = tp.z - ap.z;
            }
            else
            {
                noTarget = true;
                d = -1f;
                try
                {
                    var eq = f.CachedClosestEnemyFormation;
                    var ef = eq != null ? eq.Formation : null;
                    if (ef != null && ef.CountOfUnits > 0) d = Math.Max(0f, a.Position.AsVec2.Distance(ef.CachedMedianPosition.AsVec2) - ef.Depth * 0.5f);
                }
                catch { d = -1f; }
                if (d < 0f) { if (held) { _relNoFoe++; End(s, now, true); } return; }
            }
            float g = Gate(p, dz, true);
            if (g < 0f)
            {
                // 469-q-p (U2): bez budzetu symulacji wstrzymany zostaje; nowy - wstrzymany od razu, gdy d ponad zasieg historyczny (przy 469-b G <= Hist), inaczej nastepny przeglad
                if (held || !_hist || p.Hist <= 0 || d <= p.Hist) return;
                g = p.Hist;
            }
            if (_key)
            {
                // 469-r: G czlowieka do percentyla szyku (podejscie liczy tylko szyki AI piesze strzeleckie - reszta odpada w Form)
                GE ge;
                if (!GOf.TryGetValue(a, out ge)) { ge = new GE(); GOf[a] = ge; }
                ge.G = g; ge.S = d - g; ge.T = now; ge.F = f;
            }
            if (d <= g) NoteInG(a, now);
            if (held)
            {
                if (d <= g) { _relRange++; End(s, now, true); return; }
                if (d < ThreatR + 1f || Near(m, a, true)) { _relNear++; NearT[a] = now; End(s, now, true); return; }
                // 469-q: bez wylacznika zastoju 469-p - szyk, ktory nie podchodzi, podchodzi sam (Forms); czlowiek poza G nie strzela
                s.G = g;
                if (!noTarget && FoeInG(f, a, g)) _heldFoeInG++;
                if (_key) { a.SetFiringOrder(FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire); _reHold++; }
                return;
            }
            if (d <= g * HoldOver) return;
            if (d < ThreatR + 1f) { NearT[a] = now; return; }
            // 469-q-p (U2): bez budzetu zapytan - wstrzymanie od razu (bezpiecznie), wrog w 6 m sprawdzony w nastepnym przegladzie (sciezka held)
            if (_frameNear >= NearPer) _overNear++;
            else if (Near(m, a, false)) { NearT[a] = now; return; }
            if (!_key) { _wouldHold++; return; }   // NIE: tylko pomiar
            if (s == null) { s = new St { A = a }; By[a] = s; }
            a.SetFiringOrder(FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire);
            s.Held = true;
            s.Since = now;
            s.G = g;
            _holds++;
            HoldsBy[cat]++;
            if (noTarget) _heldNoTarget++;
            if (HeldEver.Add(a)) { _heldMen++; HeldMenBy[cat]++; }
        }

        /// <summary>469-p (tylko pomiar): wstrzymany wg celu silnika, a skraj najblizszego szyku wroga jest w G (cel silnika dalej niz inny wrog).</summary>
        private static bool FoeInG(Formation f, Agent a, float g)
        {
            try
            {
                var eq = f.CachedClosestEnemyFormation;
                var ef = eq != null ? eq.Formation : null;
                return ef != null && ef.CountOfUnits > 0 && a.Position.AsVec2.Distance(ef.CachedMedianPosition.AsVec2) - ef.Depth * 0.5f <= g;
            }
            catch { return false; }
        }

        /// <summary>Aktywny, nieuciekajacy wrog w 6 m (jedno zapytanie, budzet klatki; 469-r-p U2: wstrzymani - osobny budzet NearHeldPer).</summary>
        private static bool Near(Mission m, Agent a, bool held)
        {
            if (held)
            {
                if (_frameNearH >= NearHeldPer) { _overNearH++; return false; }
                _frameNearH++;
            }
            else
            {
                if (_frameNear >= NearPer) { _overNear++; return false; }
                _frameNear++;
            }
            try
            {
                m.GetNearbyEnemyAgents(a.Position.AsVec2, ThreatR, a.Team, TBuf);
                for (int i = 0; i < TBuf.Count; i++)
                {
                    var x = TBuf[i];
                    if (x == null || !x.IsActive()) continue;
                    var o = x.IsMount ? x.RiderAgent : x;
                    if (o == null || !o.IsHuman || !o.IsActive() || o.IsRunningAway) continue;
                    return true;
                }
            }
            finally { TBuf.Clear(); }
            return false;
        }

        /// <summary>Koniec wstrzymania: rozkaz szyku wraca, chyba ze 310 wciaz trzyma ogien tego czlowieka (apply = false: padl - bez wywolan silnika).</summary>
        private static void End(St s, float now, bool apply)
        {
            if (s == null || !s.Held) return;
            s.Held = false;
            Close(s, now);
            var a = s.A;
            if (!apply || a == null || !a.IsActive() || CleanShot.Held(a)) return;
            try
            {
                var f = a.Formation;
                a.SetFiringOrder(f != null ? f.FiringOrder.OrderEnum : FiringOrder.RangedWeaponUsageOrderEnum.FireAtWill);
            }
            catch (Exception e) { Stumble("FireRange469.Ogien", e); }
        }

        private static void Close(St s, float now)
        {
            float dur = now - s.Since;
            if (dur < 0f) dur = 0f;
            _holdSecs += dur; _holdN++;
            if (dur > _holdMax) _holdMax = dur;
        }

        /// <summary>BattleSense.OnAgentRemoved (glowny watek): wpis bez wywolan silnika.</summary>
        internal static void OnRemoved(Mission m, Agent affected)
        {
            if (!Mine(m) || affected == null || (By.Count == 0 && NearT.Count == 0 && GOf.Count == 0)) return;
            try
            {
                St s;
                NearT.Remove(affected);
                GOf.Remove(affected);
                if (By.TryGetValue(affected, out s)) { if (s.Held) { _relDead++; End(s, m.CurrentTime, false); } By.Remove(affected); }
            }
            catch (Exception e) { Stumble("FireRange469.OnRemoved", e); }
        }

        // ------------------------------------------------------------ strzal (kolejka 140, glowny watek) - tylko pomiar

        internal static void OnShot(Mission m, Agent s, bool thrown)
        {
            if (!Mine(m) || s == null || thrown) return;
            long t0 = Stopwatch.GetTimestamp();
            try { Shot(m, s); }
            catch (Exception e) { Stumble("FireRange469.Strzal", e); }
            _sumTicks += Stopwatch.GetTimestamp() - t0;
        }

        private static void Shot(Mission m, Agent s)
        {
            if (!s.IsActive() || !s.IsHuman || s.Team == null || !s.IsAIControlled || s.IsMainAgent || s.IsPlayerControlled || s.HasMount) return;
            bool noAmmo;
            Wp p;
            // strzal juz poszedl - amunicja mogla sie skonczyc; dane broni bez warunku zapasu
            p = Weapon(s, false, out noAmmo);
            if (p == null || !p.Ok) return;
            Agent t = null;
            try { t = s.GetTargetAgent(); } catch { t = null; }
            Agent tm = t != null && t.IsMount ? t.RiderAgent : t;
            if (t == null || !t.IsActive() || tm == null || tm.Team == null || !tm.Team.IsEnemyOf(s.Team)) { _shotsNoTarget++; return; }
            Vec3 ap = s.Position, tp = t.Position;
            float d = ap.AsVec2.Distance(tp.AsVec2);
            float g = Gate(p, tp.z - ap.z, true);   // -1 = bez budzetu symulacji w tej klatce - strzal liczony bez G
            float nat = -1f;
            try { nat = s.GetMissileRange(); } catch { nat = -1f; }
            _shots++;
            ShotBy[d < 50f ? 0 : d < 100f ? 1 : d < 150f ? 2 : d < 200f ? 3 : 4]++;
            if (g >= 0f && d > g * OldOver) _shotsOver++;
            if (g >= 0f && d > g)
            {
                _shotsOverG++;
                int cat = Cat(s);
                if (cat >= 0)
                {
                    // 469-q-p (U3): zwolniony / niewstrzymany z powodu wroga w 6 m (walczy o zycie, 310) - osobno; reszta ma byc 0 (469-r: wg rodzaju szyku)
                    float nt;
                    if (NearT.TryGetValue(s, out nt) && m.CurrentTime - nt <= NearKeep) OverGNear[cat]++; else OverG[cat]++;
                }
            }
            if (Holds(s)) _shotsHeld++;
            p.Shots++;
            if (g >= 0f) { p.GSum += g; p.GN++; }
            if (nat > 0f) { p.NatSum += nat; p.NatN++; }
            int side = s.Team.Side == BattleSideEnum.Attacker ? 1 : 0;
            float now = m.CurrentTime;
            if (FvT0[side] < 0f) FvT0[side] = now;
            if (now - FvT0[side] <= FirstVolley)
            {
                FvN[side]++; FvD[side] += d;
                if (g >= 0f) { FvG[side] += g; FvGN[side]++; }
                if (nat > 0f) { FvNat[side] += nat; FvNatN[side]++; }
            }
        }

        /// <summary>BattleSense.Clean (po CleanShot.End): wstrzymania zdjete, linia raz na bitwe.</summary>
        internal static void End(Mission m)
        {
            var w = _m;
            if (w == null) return;
            if (m != null && !ReferenceEquals(w.Target, m)) return;
            bool live = _live;
            float now = 0f;
            try { if (m != null) now = m.CurrentTime; } catch { }
            try
            {
                foreach (var kv in By) if (kv.Value.Held) { _relEnd++; End(kv.Value, now, true); }
            }
            catch (Exception e) { Stumble("FireRange469.End", e); }
            _live = false;
            if (live && !_written)
            {
                _written = true;
                try { Write(); } catch (Exception e) { Stumble("FireRange469.Write", e); }
            }
            Clear();
            _m = null;
        }

        private static string F0(float v) { return v.ToString("0", Inv); }

        private static double Ms(long t) { return t * 1000.0 / Stopwatch.Frequency; }

        private static string Fv(int i)
        {
            if (FvN[i] == 0) return "brak";
            return FvN[i] + " strzal, sr. " + F0(FvD[i] / FvN[i]) + " m przy G " + (FvGN[i] > 0 ? F0(FvG[i] / FvGN[i]) : "-") + " m, natywny zasieg "
                + (FvNatN[i] > 0 ? F0(FvNat[i] / FvNatN[i]) : "-") + " m";
        }

        private static void Write()
        {
            var sb = new StringBuilder(1600);
            sb.Append("Ogien w zasiegu (469): misja ").Append(_kind).Append(", klucz ").Append(_key ? "TAK" : "NIE (tylko pomiar, bez wstrzyman)")
              .Append(_rbm ? "" : ", brak RBM (predkosc gry, bez naciagu)")
              .Append(" (G = ").Append(_hist && _rbm ? "min(zasieg historyczny wg naciagu 469-b, " : "").Append(Eff.ToString("0.00", Inv))
              .Append(" x zasieg maks. z oporem k ").Append(_k.ToString("0.0000", Inv)).Append(_hist && _rbm ? ")" : "")
              .Append("; wstrzymanie powyzej ").Append(HoldOver.ToString("0.0", Inv)).Append(" G, ogien od G)")
              .Append(" | strzaly AI z luku / kuszy pieszo: ").Append(_shots).Append(" (<50/50-99/100-149/150-199/200+ m ")
              .Append(ShotBy[0]).Append('/').Append(ShotBy[1]).Append('/').Append(ShotBy[2]).Append('/').Append(ShotBy[3]).Append('/').Append(ShotBy[4])
              .Append("), poza ").Append(OldOver.ToString("0.0", Inv)).Append(" G ").Append(_shotsOver).Append(", mimo wstrzymania ").Append(_shotsHeld)
              .Append(", bez celu ").Append(_shotsNoTarget)
              .Append(" | 469-r strzaly poza G ").Append(_shotsOverG).Append(": szyk strzelcow AI ").Append(OverG[CAi])
              .Append(", szyk gracza ").Append(OverG[CPl]).Append(_player ? "" : " (klucz gracza NIE - poza regula)")
              .Append(", szyk piechoty ").Append(OverG[CInf]).Append(_key ? "" : " (klucz NIE - poza regula)")
              .Append(" - ma byc 0; osobno po wrogu w ").Append(F0(ThreatR)).Append(" m (walka o zycie) AI / gracz / piechota ")
              .Append(OverGNear[CAi]).Append('/').Append(OverGNear[CPl]).Append('/').Append(OverGNear[CInf])
              .Append("; reszta (poza szykiem / pominieci: machina, poscig, chorazy, panika) ").Append(_shotsOverG - OverG[CAi] - OverG[CPl] - OverG[CInf] - OverGNear[CAi] - OverGNear[CPl] - OverGNear[CInf])
              .Append(" | pierwsza salwa (").Append(F0(FirstVolley)).Append(" s): obrona ").Append(Fv(0)).Append("; atak ").Append(Fv(1))
              .Append(" | kontakt (pierwszy wrog w G czlowieka w regule) od rozstawienia: obrona ").Append(Since0(InGT0[0])).Append(", atak ").Append(Since0(InGT0[1]))
              .Append("; pierwsza salwa po kontakcie: obrona ").Append(Gap(InGT0[0], FvT0[0])).Append(", atak ").Append(Gap(InGT0[1], FvT0[1]))
              .Append(" | wstrzyman ").Append(_holds).Append(" (ludzi ").Append(_heldMen).Append(", bez celu - szyk wroga dalej ").Append(_heldNoTarget)
              .Append("; 469-r wstrzyman / ludzi: szyk strzelcow AI ").Append(HoldsBy[CAi]).Append('/').Append(HeldMenBy[CAi])
              .Append(", szyk gracza ").Append(HoldsBy[CPl]).Append('/').Append(HeldMenBy[CPl]).Append(_player ? "" : " (klucz gracza NIE)")
              .Append(", szyk piechoty ").Append(HoldsBy[CInf]).Append('/').Append(HeldMenBy[CInf])
              .Append("), na sucho przegladow z wstrzymaniem ").Append(_wouldHold).Append(", ponowien rozkazu ").Append(_reHold)
              .Append(", wstrzymany z lukiem schowanym (przegladow) ").Append(_heldStowed)
              .Append(", czas sr. ").Append(_holdN > 0 ? (_holdSecs / _holdN).ToString("0.0", Inv) : "-").Append(" s, najdluzszy ").Append(_holdMax.ToString("0.0", Inv)).Append(" s")
              .Append(" | zwolnienia: w zasiegu ").Append(_relRange).Append(", wrog w ").Append(F0(ThreatR)).Append(" m ").Append(_relNear)
              .Append(", bron / kolczan ").Append(_relWeapon).Append(", panika ").Append(_relPanic).Append(", przejete / gracz ").Append(_relTaken)
              .Append(", kon ").Append(_relMount).Append(", szyk (piechota / wstrzymanie szyku / poza szykiem) ").Append(_relForm)
              .Append(", bez wroga ").Append(_relNoFoe).Append(", padl ").Append(_relDead).Append(", koniec misji ").Append(_relEnd)
              .Append(" | wstrzymany, choc najblizszy szyk wroga w G (przegladow) ").Append(_heldFoeInG)
              .Append(" | podejscia szyku 469-q (latka ").Append(_patch).Append("): ").Append(_adv).Append(" (szykow ").Append(AdvForms.Count).Append(')');
            if (_adv > 0)
                sb.Append(", sr. od ").Append(F0(_advDe0Sum / _adv)).Append(" m do celu ").Append(F0(_advToSum / _adv)).Append(" m (").Append(F0(AdvPct * 100f))
                  .Append(". percentyl G lucznikow szyku ").Append(F0(_advGSum / _adv)).Append(" m, lucznikow sr. ").Append((_advNaSum / (float)_adv).ToString("0.0", Inv))
                  .Append("; srodek szyku - skraj wroga; 469-r-p krok = ").Append(F0((1f - AdvPct) * 100f)).Append(". percentyl niedoboru d - G + ").Append(F0(AdvReach))
                  .Append(" m, sr. niedobor ").Append(_advShN > 0 ? F0(_advShSum / _advShN) : "-").Append(" m)")
                  .Append(", w zasiegu na koniec podejscia sr. ").Append(_advInN > 0 ? F0(_advInSum / _advInN * 100f) : "-").Append("% lucznikow szyku (cel ok. ")
                  .Append(F0((1f - AdvPct) * 100f)).Append("%)").Append(", na koniec ").Append(_advDe1N > 0 ? F0(_advDe1Sum / _advDe1N) : "-")
                  .Append(" m, sr. czas ").Append((_advSecs / _adv).ToString("0.0", Inv)).Append(" s, skrocone oslona piechoty ").Append(_advCovered)
                  .Append(", cel blizej niz 0.4 num2 RBM (moze cofnac) ").Append(_advPull).Append(", odbiorow przez RBM ").Append(_advHits);
            sb.Append("; koniec: dotarl ").Append(AdvEnd[EnReached]).Append(", ludzie w G ").Append(AdvEnd[EnInG]).Append(", jazda wroga ").Append(AdvEnd[EnCav])
              .Append(", limit ").Append(F0(AdvMaxT)).Append(" s ").Append(AdvEnd[EnTime]).Append(", inne zachowanie / 441 / 443 ").Append(AdvEnd[EnBeh])
              .Append(", przejete / gracz / szyk ").Append(AdvEnd[EnTaken]).Append(", bez odbioru w ").Append(F0(NoHitT)).Append(" s ").Append(AdvEnd[EnNoHit])
              .Append("; zastoj bez podejscia (szyko-s): jazda wroga w ").Append(F0(CavNear)).Append(" m ").Append(F0(Blk[BkCav]))
              .Append(", oslona piechoty ").Append(F0(Blk[BkCover])).Append(", inne zachowanie ").Append(F0(Blk[BkBeh])).Append(", 441 / 443 ").Append(F0(Blk[Bk443]))
              .Append(", juz blisko ").Append(F0(Blk[BkClose])).Append(", bez latki / slotu ").Append(F0(Blk[BkPatch]))
              .Append("; szyk wstrzymany przy jezdzie wroga w ").Append(F0(CavNear)).Append(" m (szyko-s) ").Append(F0(_cavHeldSec))
              .Append(", bledy postfiksu ").Append(Volatile.Read(ref _postErr))
              .Append(" | przegladow strzelcow ").Append(_scanned).Append(", w regule ").Append(_archers)
              .Append(" | pominiete: konni ").Append(_skMount).Append(", szyk piechoty ").Append(_skInf).Append(", szyk wstrzymany ").Append(_skFormHold)
              .Append(", poza szykiem ").Append(_skDetached).Append(", machina ").Append(_skObject).Append(", nieznany uzytek ").Append(_skUsage)
              .Append(", bez amunicji ").Append(_skNoAmmo).Append(", 204/283/148/447 ").Append(_skHunt).Append(", chorazy ").Append(_skBanner)
              .Append(", panika / odwrot ").Append(_skPanic).Append(", szyk gracza ").Append(_skPlayer)
              .Append(", ponad budzet (zapytania ").Append(_overNear).Append(", zapytania wstrzymanych ").Append(_overNearH).Append(", symulacje ").Append(_overSim).Append(')')
              .Append(" | bronie (naciag lb / v m/s / G sr. / natywny sr. / strzal / zasieg sym. z oporem / natywny statyczny GetMissileRange(v,0)):");
            WeapSeen.Sort((x, y) => y.Shots.CompareTo(x.Shots));
            int shown = 0;
            for (int i = 0; i < WeapSeen.Count && shown < WeapMax; i++)
            {
                var p = WeapSeen[i];
                if (p.Shots <= 0) continue;
                shown++;
                sb.Append(' ').Append(p.Name).Append(' ').Append(p.Lb).Append('/').Append(p.V).Append('/').Append(p.GN > 0 ? F0(p.GSum / p.GN) : "-").Append('/')
                  .Append(p.NatN > 0 ? F0(p.NatSum / p.NatN) : "-").Append('/').Append(p.Shots).Append('/');
                float r0;
                sb.Append(RangeCache.TryGetValue(p.V * 64 + 6, out r0) ? F0(r0) : "-").Append('/').Append(p.NatStatic > 0f ? F0(p.NatStatic) : "-").Append(';');
            }
            if (shown == 0) sb.Append(" brak");
            sb.Append(" | koszt ").Append(Ms(_sumTicks).ToString("0.0", Inv)).Append(" ms razem, najwiecej ").Append(Ms(_maxTicks).ToString("0.000", Inv))
              .Append(" ms na klatke | potkniecia ").Append(_stum).Append('.');
            Log.Info(sb.ToString());
        }

        private static string Since0(float t)
        {
            return t < 0f || _depT < 0f ? "brak" : (t - _depT).ToString("0.0", Inv) + " s";
        }

        private static string Gap(float inG, float shot)
        {
            return inG < 0f || shot < 0f ? "brak" : (shot - inG).ToString("0.0", Inv) + " s";
        }

        /// <summary>469-q (pomiar): pierwszy przeglad strony, w ktorym czlowiek w regule ma wroga w G - "kontakt".</summary>
        private static void NoteInG(Agent a, float now)
        {
            var t = a.Team;
            if (t == null) return;
            int side = t.Side == BattleSideEnum.Attacker ? 1 : 0;
            if (InGT0[side] < 0f) InGT0[side] = now;
        }

        /// <summary>469-r (pomiar strzalu): rodzaj szyku strzelca - CInf szyk piechoty (AI i gracza), CPl szyk pod rozkazami gracza, CAi szyk AI; -1 poza szykiem
        /// (469-r-p: bez machiny / poscigu / chorazego / paniki - jak pominiecia w Look; zostaje przyblizenie: cel zmieniony przez silnik po zwolnieniu w G; czy rodzaj jest w regule - wg kluczy, w linii).</summary>
        private static int Cat(Agent s)
        {
            var f = s.Formation;
            if (f == null || s.IsDetachedFromFormation) return -1;
            // 469-r-p (U5): pominieci przez regule z innych powodow (machina, poscig 204/283/148/447, chorazy, panika / odwrot) - do "reszty", nie do "ma byc 0"
            if (s.IsUsingGameObject || HorsePursuit.HuntAnyThread(s) || HorsePursuit.RidingAnyThread(s) || Doctrine.H447AnyThread(s) || HorsePursuit.Banner(s)
                || s.IsRunningAway || SurvivalMode.InPanic(s) || s.IsRetreating()) return -1;
            var q = f.QuerySystem;
            if (q != null && q.IsInfantryFormation) return CInf;
            return f.IsAIControlled ? CAi : CPl;
        }

        // ------------------------------------------------------------ 469-q: podejscie szyku (glowny watek, co FormEvery)

        private static int Idx(Formation f)
        {
            var t = f.Team;
            if (t == null) return -1;
            int ti = t.TeamIndex, fi = (int)f.FormationIndex;
            if (ti < 0 || ti >= SlotTeams || fi < 0 || fi >= 10) return -1;
            return ti * 10 + fi;
        }

        private static bool RbmActive(Formation f)
        {
            var ai = f.AI;
            var b = ai != null ? ai.ActiveBehavior : null;
            return b != null && _tArch != null && b.GetType() == _tArch;
        }

        private static void Forms(Mission m, float now)
        {
            HeldN.Clear(); HeldG.Clear();
            foreach (var kv in By)
            {
                var s = kv.Value;
                if (!s.Held || s.A == null) continue;
                Formation f = null;
                try { f = s.A.Formation; } catch { f = null; }
                if (f == null) continue;
                int n; HeldN.TryGetValue(f, out n); HeldN[f] = n + 1;
                float g; HeldG.TryGetValue(f, out g); HeldG[f] = g + s.G;
            }
            // 469-r: G lucznikow w regule wg szyku (swieze - z przegladu w GFresh s; stare wypadaja), posortowane - percentyl
            foreach (var kv in FGs) kv.Value.Clear();
            foreach (var kv in FSh) kv.Value.Clear();
            FHeld.Clear();
            Drop.Clear();
            foreach (var kv in GOf)
            {
                var e = kv.Value;
                if (e.F == null || now - e.T > GFresh) { Drop.Add(kv.Key); continue; }
                List<float> l;
                if (!FGs.TryGetValue(e.F, out l)) { l = new List<float>(); FGs[e.F] = l; }
                l.Add(e.G);
                // 469-r-p (U1): niedobor d - G kazdego lucznika; (U3): wstrzymani liczeni w tej samej (swiezej) probie co lucznicy
                if (!FSh.TryGetValue(e.F, out l)) { l = new List<float>(); FSh[e.F] = l; }
                l.Add(e.S);
                St hs;
                if (By.TryGetValue(kv.Key, out hs) && hs.Held) { int hn; FHeld.TryGetValue(e.F, out hn); FHeld[e.F] = hn + 1; }
            }
            for (int i = 0; i < Drop.Count; i++) GOf.Remove(Drop[i]);
            Drop.Clear();
            FTmp.Clear();
            foreach (var kv in HeldN) FTmp.Add(kv.Key);
            foreach (var kv in FBy) if (!HeldN.ContainsKey(kv.Key)) FTmp.Add(kv.Key);
            for (int i = 0; i < FTmp.Count; i++)
            {
                var f = FTmp[i];
                int n; HeldN.TryGetValue(f, out n);
                float gs; HeldG.TryGetValue(f, out gs);
                // 469-r: na - lucznikow w regule ze swiezym G; gp - 30. percentyl ich G (ok. 70% ma G >= gp); bez danych - sr. G wstrzymanych jak 469-q
                List<float> gl;
                int na = 0, nin = 0;
                float gp = n > 0 ? gs / n : 0f;
                float sh = float.NaN;   // 469-r-p (U1): niedobor d - G w (1 - AdvPct) percentylu; NaN - bez danych
                if (FGs.TryGetValue(f, out gl) && gl.Count > 0)
                {
                    gl.Sort();
                    na = gl.Count;
                    gp = gl[(int)Math.Floor(AdvPct * (na - 1))];
                    List<float> sl;
                    if (FSh.TryGetValue(f, out sl) && sl.Count == na)
                    {
                        sl.Sort();
                        sh = sl[(int)Math.Floor((1f - AdvPct) * (na - 1))];
                        for (int j = 0; j < na && sl[j] <= 0f; j++) nin++;
                    }
                    // 469-r-p (U3): "dosc wstrzymanych" liczone w swiezej probie (wstrzymani z wpisem z GFresh s), nie wszyscy wstrzymani wobec swiezych lucznikow
                    int hn; FHeld.TryGetValue(f, out hn);
                    n = hn;
                }
                FS st;
                if (!FBy.TryGetValue(f, out st))
                {
                    // 469-r-p (U6): szyk, ktory nigdy nie jest przesuwany (gracza, piechoty, jazdy) - bez wpisu FS (dawniej nowy obiekt co sekunde i od razu usuwany)
                    if (!Movable(f)) continue;
                    st = new FS(); FBy[f] = st;
                }
                try { Form(m, f, st, n, na, gp, sh, nin, now); }
                catch (Exception e) { Stumble("FireRange469.Szyk", e); try { Release(f, st); } catch { } st.Since = -1f; }
                if (st.Adv == null && st.Since < 0f) FBy.Remove(f);
            }
            FTmp.Clear();
        }

        /// <summary>469-r-p (U6): szyk, ktory Form moze przesunac (te same warunki co ai / foot w Form).</summary>
        private static bool Movable(Formation f)
        {
            if (f.CountOfUnits <= 0 || !f.IsAIControlled || f.IsPlayerTroopInFormation || f.HasPlayerControlledTroop) return false;
            var q = f.QuerySystem;
            return q != null && q.IsRangedFormation && !q.IsCavalryFormation && !q.IsRangedCavalryFormation;
        }

        /// <summary>Jeden szyk: trwajace podejscie (koniec?) albo zastoj wstrzymanego szyku (podejscie, gdy wolno).</summary>
        private static void Form(Mission m, Formation f, FS st, int n, int na, float gp, float sh, int nin, float now)
        {
            int units = f.CountOfUnits;
            var q = units > 0 ? f.QuerySystem : null;
            // szyk gracza (469-r: jego ludzie tez wstrzymani) i szyk piechoty (469-r) - ai / foot false: zadnego ruchu
            bool ai = units > 0 && f.IsAIControlled && !f.IsPlayerTroopInFormation && !f.HasPlayerControlledTroop;
            bool foot = q != null && q.IsRangedFormation && !q.IsCavalryFormation && !q.IsRangedCavalryFormation;
            // 469-r: udzial wstrzymanych wsrod lucznikow w regule (469-q: wsrod wszystkich ludzi szyku)
            bool enough = n >= AdvMinMen && n >= AdvShare * (na > 0 ? na : units);
            Vec2 pos = units > 0 ? f.CachedAveragePosition : Vec2.Invalid;
            Formation ef = null;
            float de = -1f;
            bool cav = false;
            if (ai && foot && pos.IsValid) Scan(m, f, pos, out ef, out de, out cav);
            if (st.Adv != null)
            {
                if (st.Stopping)
                {
                    if (!ai || Volatile.Read(ref st.Adv.Hits) > 0 || now - st.StopT > StopMax) Release(f, st);
                    return;
                }
                bool rbm = ai && foot && RbmActive(f);
                int why = -1;
                if (!ai || !foot) why = EnTaken;
                else if (!rbm || Doctrine.C441Owned(f) || Doctrine.R443Why(f) != 0) why = EnBeh;
                else if (cav) why = EnCav;
                else if (!enough && ++st.Low >= LowMax) why = EnInG;
                else if (pos.IsValid && pos.Distance(st.AdvTo) <= Math.Max(AdvReach, f.Depth)) why = EnReached;
                else if (now - st.AdvT0 > AdvMaxT) why = EnTime;
                else if (Volatile.Read(ref st.Adv.Hits) == 0 && now - st.AdvT0 > NoHitT) why = EnNoHit;
                if (enough) st.Low = 0;
                if (why < 0) return;
                st.Low = 0;
                AdvEnd[why]++;
                if (de >= 0f) { _advDe1Sum += de; _advDe1N++; }
                if (na > 0) { _advInSum += nin / (float)na; _advInN++; }   // 469-r-p (U1): ilu lucznikow szyku ma wroga w zasiegu na koniec (cel ok. 0.7)
                _advSecs += Math.Max(0f, now - st.AdvT0);
                // przerwane w drodze przy zachowaniu RBM - "stoj tu" (inaczej CurrentOrder RBM zostaje naszym punktem i szyk szedlby dalej)
                if (rbm && (why == EnCav || why == EnTime || why == EnBeh || why == EnNoHit)) Stop(f, st, now); else Release(f, st);
                if (de >= 0f) st.BestDe = de;
                st.ProgT = now;   // zastoj liczony od nowa
                return;
            }
            if (!ai || !foot || de < 0f || ef == null) { st.Since = -1f; st.Low = 0; return; }
            if (!enough)
            {
                // 469-q-p (U1): jedna sekunda "za malo wstrzymanych" nie zeruje zastoju - dopiero LowMax kolejnych
                if (st.Since < 0f || ++st.Low >= LowMax) { st.Since = -1f; st.Low = 0; }
                return;
            }
            st.Low = 0;
            if (st.Since < 0f) { st.Since = now; st.BestDe = de; st.ProgT = now; return; }
            if (de < st.BestDe - AdvStallD) { st.BestDe = de; st.ProgT = now; }
            if (cav) _cavHeldSec += FormEvery;
            if (now - st.ProgT < AdvStall) return;
            // zastoj: >= AdvShare szyku wstrzymane, wrog nie podszedl przez AdvStall s - podejscie, gdy wolno (inaczej szyk stoi i nie strzela)
            if (cav) { Blk[BkCav] += FormEvery; return; }
            int idx = Idx(f);
            if (!_patchOk || idx < 0) { Blk[BkPatch] += FormEvery; return; }
            if (!RbmActive(f)) { Blk[BkBeh] += FormEvery; return; }
            if (Doctrine.C441Owned(f) || Doctrine.R443Why(f) != 0) { Blk[Bk443] += FormEvery; return; }
            // 469-r: cel - ok. 70% lucznikow szyku w zasiegu (dalej siegajacy zwalniani w drodze)
            // 469-r-p (U1): krok = niedobor d - G w 70. percentylu (kazdy lucznik od swojej pozycji do swojego celu - glebokosc szyku i jednakowe luki
            // uwzglednione; 469-r: de - 30. percentyl G od srodka dawalo ok. 50%) + AdvReach (koniec EnReached do max(AdvReach, glebokosc) przed celem);
            // bez danych - od srodka z pol glebokosci
            float gf = gp;
            float want = !float.IsNaN(sh) ? sh + AdvReach : de - gf + f.Depth * 0.5f + AdvReach;
            if (want < AdvMinStep) { Blk[BkClose] += FormEvery; return; }
            Vec2 dir = ef.CachedMedianPosition.AsVec2 - pos;
            float len = dir.Normalize();
            if (len < 1f) { Blk[BkClose] += FormEvery; return; }
            float step = want;
            float cover = CoverStep(f, pos, dir);
            if (cover < step) step = cover;
            if (step < AdvMinStep) { Blk[BkCover] += FormEvery; return; }
            Vec2 to = pos + dir * step;
            var wp = f.CachedMedianPosition;
            // 469-q-p (U5): punkt Move to front szyku (OrderPosition; CurrentPosition = srodek - OrderLocalAveragePosition, mb_full.cs:69315) - cel liczony dla srodka
            wp.SetVec2(to + dir * (f.Depth * 0.5f));
            var slot = new Slot { F = f, Order = MovementOrder.MovementOrderMove(wp) };
            Volatile.Write(ref Slots[idx], slot);
            st.Adv = slot; st.Stopping = false; st.AdvT0 = now; st.AdvTo = to;
            _adv++; _advDe0Sum += de; _advToSum += de - step; _advGSum += gf; _advNaSum += na;
            if (!float.IsNaN(sh)) { _advShSum += sh; _advShN++; }
            if (step < want) _advCovered++;
            float n2 = f.Depth * 0.5f + q.MaximumMissileRange * 0.5f + ef.Depth * 0.5f;   // num2 RBM (rbmai.cs:8580, bez dodatku SplitArchers)
            if (de - step + ef.Depth * 0.5f < 0.4f * n2) _advPull++;
            AdvForms.Add(f);
        }

        /// <summary>Najblizszy pieszy szyk wroga (de = srodek naszego szyku - skraj wroga) i jazda wroga w CavNear m (skraj - skraj).</summary>
        private static void Scan(Mission m, Formation f, Vec2 pos, out Formation ef, out float de, out bool cav)
        {
            ef = null; de = -1f; cav = false;
            var me = f.Team;
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Enemies(me, t)) continue;
                var fl = t.FormationsIncludingEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var x = fl[j];
                    if (x == null || x.CountOfUnits <= 0) continue;
                    var xq = x.QuerySystem;
                    if (xq == null) continue;
                    float c = pos.Distance(x.CachedMedianPosition.AsVec2) - x.Depth * 0.5f;
                    if (xq.IsCavalryFormation || xq.IsRangedCavalryFormation)
                    {
                        if (x.CountOfUnits >= CavMinMen && c - f.Depth * 0.5f <= CavNear) cav = true;
                        continue;
                    }
                    if (de < 0f || c < de) { de = Math.Max(0f, c); ef = x; }
                }
            }
        }

        /// <summary>Najwiekszy krok naprzod (wzdluz dir), przy ktorym front szyku jest najwyzej CoverAhead m przed frontem najdalej wysunietej wlasnej piechoty
        /// (szyk piechoty druzyny, >= CoverMinMen). Brak piechoty - bez ograniczenia.</summary>
        private static float CoverStep(Formation f, Vec2 pos, Vec2 dir)
        {
            var t = f.Team;
            var fl = t != null ? t.FormationsIncludingEmpty : null;
            int nf = fl != null ? fl.Count : 0;
            bool any = false;
            float best = 0f;
            for (int j = 0; j < nf; j++)
            {
                var x = fl[j];
                if (x == null || ReferenceEquals(x, f) || x.CountOfUnits < CoverMinMen) continue;
                var xq = x.QuerySystem;
                if (xq == null || !xq.IsInfantryFormation || xq.IsCavalryFormation) continue;
                float p = (x.CachedAveragePosition - pos).DotProduct(dir) + x.Depth * 0.5f;
                if (!any || p > best) { best = p; any = true; }
            }
            return any ? best + CoverAhead - f.Depth * 0.5f : float.MaxValue;
        }

        /// <summary>Przerwane podejscie: "stoj tu" (Move w mediane szyku), trzymane do pierwszego odbioru przez RBM, najwyzej StopMax s.</summary>
        private static void Stop(Formation f, FS st, float now)
        {
            int idx = Idx(f);
            if (idx < 0) { Release(f, st); return; }
            if (st.Adv != null) _advHits += Volatile.Read(ref st.Adv.Hits);
            var wp = f.CachedMedianPosition;
            wp.SetVec2(f.CurrentPosition);   // 469-q-p (U5): "stoj tu" = obecny front (punkt Move), nie mediana - bez cofniecia o pol glebokosci
            var slot = new Slot { F = f, Order = MovementOrder.MovementOrderMove(wp) };
            Volatile.Write(ref Slots[idx], slot);
            st.Adv = slot; st.Stopping = true; st.StopT = now;
        }

        private static void Release(Formation f, FS st)
        {
            var s = st.Adv;
            st.Adv = null;
            st.Stopping = false;
            if (s == null) return;
            int idx = Idx(f);
            if (idx >= 0) Interlocked.CompareExchange(ref Slots[idx], null, s);
            else for (int i = 0; i < Slots.Length; i++) Interlocked.CompareExchange(ref Slots[i], null, s);
            _advHits += Volatile.Read(ref s.Hits);
        }

        /// <summary>NightHooks.FirstBattle (raz na proces): postfiks RBMBehaviorArcherSkirmish.CalculateCurrentOrder (DeclaredMethod - metoda zadeklarowana w nim,
        /// rbmai.cs:8552). Brak RBM - bez latki, bez podejsc (wstrzymanie dziala dalej).</summary>
        internal static string ApplyMissionPatches(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("RBMAI.RBMBehaviorArcherSkirmish") ?? AccessTools.TypeByName("RBMBehaviorArcherSkirmish");
                if (t == null || !typeof(BehaviorComponent).IsAssignableFrom(t)) return _patch = "NIE (brak RBMBehaviorArcherSkirmish - bez podejsc)";
                var target = AccessTools.DeclaredMethod(t, "CalculateCurrentOrder", Type.EmptyTypes);
                if (target == null) return _patch = "NIE (brak CalculateCurrentOrder)";
                h.Patch(target, postfix: new HarmonyMethod(typeof(FireRange469), nameof(ArchPost)) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                var info = Harmony.GetPatchInfo(target);
                bool ok = false;
                if (info != null)
                    foreach (var p in info.Postfixes)
                        if (p != null && p.owner == h.Id && p.PatchMethod != null && p.PatchMethod.Name == nameof(ArchPost)) ok = true;
                if (ok) { _tArch = t; _patchOk = true; }
                return _patch = ok ? "podejscie 1/1" : "podejscie 0/1 (latka polknieta)";
            }
            catch (Exception e) { return _patch = "NIE (" + e.GetType().Name + ")"; }
        }

        /// <summary>Postfiks RBMBehaviorArcherSkirmish.CalculateCurrentOrder (watek AI / silnika): szyk ze slotem 469-q - rozkaz Move ze slotu (gotowy, z glownego
        /// watku). Tylko odczyt referencji slotu i pola wlasnej instancji zachowania (jak RBM / 439); bez alokacji, bez logu.</summary>
        private static void ArchPost(BehaviorComponent __instance, ref MovementOrder ____currentOrder, ref bool ___IsCurrentOrderChanged)
        {
            if (__instance == null) return;
            try
            {
                var f = __instance.Formation;
                if (f == null) return;
                int idx = Idx(f);
                if (idx < 0) return;
                var s = Volatile.Read(ref Slots[idx]);
                if (s == null || !ReferenceEquals(s.F, f)) return;
                // 469-q-p (U6): tylko czynne zachowanie - FindBestBehavior wola PrecalculateMovementOrder -> CalculateCurrentOrder takze na nieczynnym (mb_full.cs:25318, 20426)
                var ai = f.AI;
                if (ai == null || !ReferenceEquals(ai.ActiveBehavior, __instance)) return;
                ____currentOrder = s.Order;
                ___IsCurrentOrderChanged = true;
                Interlocked.Increment(ref s.Hits);
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        // ------------------------------------------------------------ start sesji: tabela zasiegow (tylko log) i naciag z danych broni

        private static readonly string[] Typical =
        {
            "hunting_bow", "steppe_bow", "composite_bow", "nordic_shortbow", "steppe_war_bow", "noble_bow", "highland_ranger_bow", "lowland_longbow",
            "woodland_longbow", "noble_long_bow", "weirwood_bow", "ravens_teeth_longbow", "crossbow_h", "crossbow_a", "crossbow_c", "crossbow_f"
        };

        /// <summary>OnAfterSessionLaunched (po RotBows420.ApplyLore): naciag wszystkich lukow i kusz zapamietany, linia "Zasieg lukow (469)".</summary>
        internal static void StartTable()
        {
            try
            {
                LbAtStart.Clear();
                bool rbm = RangedRank.RbmLoaded;
                var c = Settings.Current;
                bool hist = rbm && (c == null || c.HistoricalBowAndCrossbowRange);
                float k = Friction();
                int bows = 0, xbows = 0;
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || (it.ItemType != ItemObject.ItemTypeEnum.Bow && it.ItemType != ItemObject.ItemTypeEnum.Crossbow)) continue;
                    var w = it.PrimaryWeapon;
                    if (w == null) continue;
                    LbAtStart[it] = w.MissileSpeed;
                    if (it.ItemType == ItemObject.ItemTypeEnum.Bow) bows++; else xbows++;
                }
                var sb = new StringBuilder(1200);
                sb.Append("Zasieg lukow (469): start sesji | ").Append(rbm ? "RBM - predkosc z naciagu (wzor RBM)" : "brak RBM - predkosc gry")
                  .Append(", opor k ").Append(k.ToString("0.0000", Inv)).Append(", strzala ").Append(ArrowW.ToString("0.000", Inv)).Append(" kg, belt ")
                  .Append(BoltW.ToString("0.000", Inv)).Append(" kg, G = ").Append(hist ? "min(historyczny 469-b, " : "").Append(Eff.ToString("0.00", Inv))
                  .Append(" x zasieg maks.").Append(hist ? ")" : "").Append(", zasieg historyczny ").Append(hist ? "TAK" : (rbm ? "NIE" : "brak RBM - bez naciagu"))
                  .Append(" | lukow ").Append(bows).Append(", kusz ").Append(xbows).Append(" | bron: uzytek, naciag lb, v m/s, zasieg maks. m, historyczny m, G m:");
                int shown = 0;
                for (int i = 0; i < Typical.Length; i++)
                {
                    ItemObject it = null;
                    try { it = MBObjectManager.Instance.GetObject<ItemObject>(Typical[i]); } catch { it = null; }
                    if (it == null || it.PrimaryWeapon == null) continue;
                    var w = it.PrimaryWeapon;
                    string u = w.ItemUsage ?? "";
                    int lb = rbm ? w.MissileSpeed : 0;
                    int v = rbm ? RbmSpeed(it.ItemType == ItemObject.ItemTypeEnum.Crossbow ? BoltW : ArrowW, u, lb) : w.MissileSpeed;
                    float r = Simulate(v, 0f, k);
                    int h = rbm ? HistRange(u, lb) : 0;
                    float g = Eff * r;
                    if (hist && h > 0 && h < g) g = h;
                    sb.Append(' ').Append(it.StringId).Append(' ').Append(u).Append(' ').Append(lb).Append(' ').Append(v).Append(' ').Append(F0(r))
                      .Append(' ').Append(h > 0 ? h.ToString(Inv) : "-").Append(' ').Append(F0(g)).Append(';');
                    shown++;
                }
                if (shown == 0) sb.Append(" brak typowych broni w danych");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("FireRange469.StartTable", e); }
        }
    }
}
