using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 333 (B) "Szyk stoi albo idzie - bez wahadla przed kontaktem" (06.11, Jeff po bitwie: "formacje dziwnie sie zachowuja, ida do przodu i do tylu, do przodu i do tylu
    /// zamiast stac w formacji; dopiero jak wrog jest blizej, jakby wychodza z trybu ping-pong i zaczynaja sie normalnie zachowywac"). Klucz FormationsSteadyBeforeContact.
    /// Przyczyna (rozpoznanie 333, dekompilacja dek333): przed zwarciem RBM (Utilities.HasBattleBeenJoined - 75 m, mediany + pol glebokosci obu szykow) szyki ida
    /// zachowaniami "fazy podejscia", a kazde liczy rozkaz Move od nowa co 0.5 s (FormationAI.TickOccasionally -> BehaviorComponent.TickOccasionally ->
    /// Formation.SetMovementOrder) bez histerezy kierunku:
    ///  - BehaviorScreenedSkirmish (lucznicy, waga 1 w taktykach RBM): postfiks RBM CalculateCurrentOrder stawia punkt = mediana glownej piechoty - Direction piechoty x
    ///    (pol glebokosci obu + 5 m), prefiks TickOccasionally co tick SetMovementOrder - lucznicy chodza za kazdym krokiem i obrotem piechoty (wahadlo piechoty x 1, obrot
    ///    Direction przerzuca punkt przod / tyl);
    ///  - BehaviorHoldHighGround (glowna piechota obroncy): vanilla - wzgorze HighGroundCloseToForeseenBattleGround (przesuwa sie z przewidywanym polem bitwy), postfiks RBM -
    ///    wrog w 200 m: punkt z pamieci positionsStorage (zapisany, gdy wrog byl dalej: srednia szyku + Direction x 10 m), wrog dalej: znowu punkt vanilla; przejscie przez
    ///    200 m w obie strony = skok punktu tam i z powrotem;
    ///  - BehaviorCautiousAdvance / Advance / Regroup - kroki RBM +5 / +10 / -10 m i medianowe punkty (opis 333).
    /// Pozycja szyku idzie tylko przez rozkaz ruchu: Formation.Tick bierze _movementOrder.CreateNewOrderWorldPositionMT (Move = staly punkt _position) -> SetPositioning;
    /// RBM SetPositioningPatch zmienia tylko odstep (unitSpacing), a Stop nie ma punktu (szyk zostaje na ostatnim). Jedna droga = prefiks Formation.SetMovementOrder.
    ///
    /// ZASADA (333b, jedna na wszystkie zachowania, bez dotykania wag RBM): prefiks Formation.SetMovementOrder (Priority.Last, po prefiksie com.rbmai), tylko rozkaz Move,
    /// tylko szyk AI (nie szyk gracza - HorsePursuit.PlayerForm), pieszy (piechota albo lucznicy), od IntactMin ludzi, bitwa polowa po rozstawieniu, glowny watek,
    /// i tylko PRZED KONTAKTEM (najblizszy pieszy szyk wroga - bez pieszego dowolny - dalej niz 75 m miara RBM; wyjscie z kontaktu > 90 m). Szyk ma PUNKT PRZYJETY P
    /// (ostatni przepuszczony rozkaz). Nowy punkt T:
    ///  1. pierwszy rozkaz przed kontaktem - przyjety (powrot obroncy na wzgorze, lucznicy za piechote, pierwszy marsz - w dowolna strone);
    ///  2. MARTWA STREFA: |T - P| < max(12 m, glebokosc szyku) - zostaje P (kroki RBM +5 / +10 / -10 m, drganie mediany piechoty pod punktem lucznikow S4);
    ///  3. ZAPADKA KIERUNKU: skladowa (T - P) na osi do wroga przeciwna do ostatniego przyjetego ruchu naprzod / wstecz (ponad pol martwej strefy) - zostaje P, dopoki od
    ///     tamtego ruchu nie minie okno 30 s; kazdy przepuszczony nawrot podwaja okno tego szyku (30 / 60 / 120 / 240 s) - wahadlo z konstrukcji gasnie;
    ///  4. TRZYMANIE: szyk stoi na P (mediana w martwej strefie od P) - nowy punkt najwczesniej 10 s po przyjeciu P (bez dreptania co 0.5 s);
    ///  5. reszta (ruch w tym samym kierunku albo w bok, szyk jeszcze w drodze) - przyjety.
    /// "Zostaje P" = rozkaz zamieniony na Move w punkt P (ta sama WorldPosition - gra uznaje rozkaz za praktycznie ten sam i nie przestawia szyku).
    /// KONTAKT (recenzja 333 W1): takze dowolny szyk wroga (konnica) w 75 m miara RBM albo konnica wroga w 60 m, albo w 120 m i zbliza sie >= 3 m/s (szarza) -
    /// wtedy przez 4 s od ostatniego takiego pomiaru wszystko jak w grze (lucznicy uciekaja przed szarza, piechota sie ustawia); P i zapadka zapomniane.
    /// Szyki druzyny gracza-wodza (takze oddane AI przez F6) - poza 333. Odwrot (Retreat), szarza, Advance / FallBack, Stop - bez zmian.
    /// POMIAR (linia, takze przy kluczu NIE - do porownania): co 0.5 s kazdy szyk AI jak wyzej przed kontaktem - przesuniecie mediany rzutowane na os do wroga,
    /// filtr zygzak z progiem max(3 m, pol glebokosci): nawrot = zmiana kierunku po odcinku >= progu; wynik na szyko-minute przed kontaktem i wedlug zachowania.
    /// Koszt: prefiks raz na rozkaz (co 0.5 s na szyk) - petla po szykach wroga (kilkanascie), pomiar co 0.5 s po szykach; bez zapytan o ludzi.
    /// Potkniecia licza sie (Stumbles), bez wylacznika.
    /// DIAGNOSTYKA 333d (runda 2: nawroty 0.97/min mimo 106 zatrzymanych rozkazow wstecz - ruch ucieka inna droga): linia "AI bitwy (333) nawrot" (pierwsze
    /// MaxRevLines na misje) - szyk, strona, zachowanie, odcinek i powrot mediany oraz srodka ciezkosci (srednia pozycji ludzi, CachedAveragePosition - ten sam
    /// filtr), czy P zmienil sie od ekstremum i ktora regula go przyjela, ostatni cel gry wzgledem P, rozkaz ostatni / biezacy, s od wyjscia z kontaktu, zmiana
    /// kierunku szyku, glebokosci i szerokosci od ekstremum, FacingOrder / ArrangementOrder; "droga" = pierwsza pasujaca przyczyna (artefakt mediany, nowy P,
    /// rozkaz nie-Move, po kontakcie, przestrzelenie P, obrot / przestawienie szyku, P bez zmian). Linie "AI bitwy (333) rozkaz przed kontaktem" (zmiana rozkazu na
    /// nie-Move) i "AI bitwy (333) ustawienie przed kontaktem" (FacingOrder / ArrangementOrder na poczatku i przy zmianie) - po MaxSetLines na misje.
    /// Miara wahadla bez zmian (mediana) - srodek ciezkosci liczony obok, do porownania. Zachowanie latki bez zmian.
    /// 333e (wyniki 333d, bieg at-333-D3: 62 z 86 nawrotow mediany to skok mediany - CachedMedianPosition = czlowiek najblizej sredniej, przeskakuje miedzy ludzmi
    /// szerokiego Loose; srodek ciezkosci stoi; prawdziwe nawroty 24 - nowy P regula 5 "w bok" ze schodkiem wstecz 9, regula Z 6, drgania przy dojsciu do P 7):
    ///  - latka liczy pozycje szyku ze srodka ciezkosci (CachedAveragePosition; bez niego mediana) - regula 1 (kierunek) i 4 (trzymanie);
    ///  - zapadka: przeciwna skladowa na osi do wroga juz od DeadMin (3 m), nie od pol martwej strefy - w oknie punkt OBCIETY do osi P (zostaje sam ruch w bok,
    ///    gdy >= martwej strefy i na siatce nawigacji; inaczej zostaje P); po oknie nawrot tylko >= 2 martwe strefy na osi (mniejszy - obciety jak w oknie);
    ///  - trzymanie liczone takze od dojscia do P (srodek ciezkosci w martwej strefie), nie tylko od przyjecia P.
    /// Wyjatki kontaktu, regula 1 (powrot obroncy na wzgorze, lucznicy za piechote) i miara bez zmian; w linii: nawroty razem ze srodkiem ciezkosci (scisle),
    /// punkty obciete do ruchu w bok, nawroty po oknie za male.
    /// 333f (bieg R3o1 z 333e: mediana 0.50/min, w tyl 0.28/min, srodek ciezkosci 0.31 / 0.22 - drogi: nowy P regula B 4, przestrzelenie P 4, regula Z 2, maly
    /// szyk lucznikow regula 5 "w bok" 3, BehaviorStop artefakt mediany 3):
    ///  - zapadka i obciecie liczone od miejsca, gdzie szyk JEST (Formation.CurrentPosition - srodek ciezkosci w ukladzie punktu rozkazu), nie od P:
    ///    cel za szykiem wzgledem kierunku ruchu (kazda ilosc) obciety do linii szyku (ruch w bok), nawrot po oknie tylko >= 2 martwe strefy OD SZYKU;
    ///  - kierunek ruchu (Dir) od DeadMin (3 m) celu wzgledem szyku, nie od pol martwej strefy;
    ///  - szyk minal P wzdluz kierunku o > OverMin (2 m) - P przesuniety do linii szyku (bez powrotu do minietego punktu);
    ///  - kontakt samej konnicy (4 s) kasuje P, ale nie kierunek zapadki (pierwszy rozkaz po nim przeciwny - obciety do linii szyku).
    /// Wyjatki kontaktu, regula 1 (powrot obroncy na wzgorze, lucznicy za piechote) i miara bez zmian.
    ///
    /// 344 (B) ARBITER - JEDEN ROZKAZ SZYKU (07.11, Jeff: "wojsko przeciwnika chodzi przod-tyl, przod-tyl, jakby uruchamiala sie jedna komenda, a potem byla zamieniana
    /// druga, i nie wiedza, co robic"). Klucz FormationOrderArbiter (zastepuje FormationsSteadyBeforeContact). Projekt: docs/PROJEKT-344-AI-BITWY.md rozdz. 4.
    /// Przyczyna (log Jeffa 06.11 i 07.11): w zwyklym tempie gry MissionState.OnTick wola TickMissionAux(asyncAITick: true) - AI szykow (FormationAI.TickOccasionally ->
    /// zachowania -> prefiksy RBM -> Formation.SetMovementOrder) idzie w WATKU SILNIKA, a Pre 333 mial pierwsza linie "tylko glowny watek": w bitwach Jeffa 0 rozkazow,
    /// w autotestach (przewijanie = AI w glownym watku) setki - testy 333b-f przechodzily, gra nigdy. Do tego zapadka nie obejmowala przelaczania Line / Loose
    /// (BehaviorProtectFlank co tick przy UnderRangedAttackRatio 0.2 / 0.1 - 40 zmian w bitwie 1), szykow bocznych powtarzajacych kazdy ruch glownego szyku
    /// (ProtectFlank / ScreenedSkirmish licza cel od _mainFormation) i przelaczania zachowan (Regroup / HoldHighGround, BehaviorStop / ScreenedSkirmish co 2-4 s).
    /// ZMIANY: prefiks dziala w kazdym watku - caly stan pod jednym lockiem (krotkie sekcje), w watku AI tylko odczyty, ktore gra robi w tym samym ticku
    /// (mediany, CurrentPosition, Depth, QuerySystem, szyki druzyn, CreateNewOrderWorldPositionMT, siatka przez SetVec2MT / GetNavMeshMT); szyki gracza z migawki
    /// glownego watku (co 0.5 s, podmiana referencji); linie i potkniecia z watku AI do kolejki, wypisywane w glownym. Licznik wywolan wedlug watku w linii (A344-0).
    /// Reguly 0-5 z 333f bez zmian, dochodza:
    ///  1. szyk boczny (BehaviorProtectFlank / BehaviorScreenedSkirmish - nastepca) dostaje nowy punkt tylko, gdy punkt przyjety PROWADZACEGO (pole _mainFormation
    ///     zachowania; bez niego najwiekszy szyk piechoty druzyny) przesunal sie od ostatniego przyjecia nastepcy o >= martwa strefa prowadzacego, albo prowadzacy
    ///     w kontakcie - inaczej zostaje jego punkt (drganie mediany prowadzacego nie rusza bokow);
    ///  2. histereza zachowan: nowe zachowanie jest "zmiana planu" (punkt i zapadka od nowa, okno od 30 s) dopiero po 6 s bez przerwy, przy zmianie taktyki druzyny
    ///     (TeamAIComponent._currentTactic) albo gdy to szarza / odwrot / Advance po kontakcie; powrot A -> B -> A w 6 s = "migniecie" (bez zmiany planu);
    ///  3. w 10 s od przyjecia punktu rozkaz przeciwny na osi do wroga (>= 3 m) zawsze obciety do linii szyku (ruch w bok) - takze po zmianie planu;
    ///  4. prefiks Formation.SetArrangementOrder (przed kontaktem, tylko Line / Loose szyku AI): zmiana najwczesniej 20 s po poprzedniej, powrot do poprzedniego
    ///     ustawienia dopiero po 20 s bez ostrzalu (UnderRangedAttackRatio ponizej 0.1 - prog wyjscia gry); kontakt: 5 s, szarza konnicy wroga - od razu; ShieldWall /
    ///     Square / Circle (CloseRanks 146/310, gra przed szarza) - bez zmian; zablokowane = prefiks zwraca false (zostaje biezace ustawienie).
    /// 344r (recenzje 344): nastepca rusza takze, gdy POZYCJA prowadzacego odeszla >= martwa strefa (marsz do dalekiego P); taktyka druzyny pod histereza 6 s
    /// (licznik surowych zmian i szybkich < 10 s na druzyne); krok naprzod do wroga w martwej strefie przyjety po trzymaniu 10 s (CautiousAdvance nie stoi);
    /// "twarda" zmiana planu tylko Advance po kontakcie; ustawienie: tylko szyki Eligible, wejscie w Loose pod ostrzalem (>= 0.2) zawsze, sama bliskosc
    /// konnicy - regula 5 s (od razu tylko szarza i tylko do Line), odrzucenie przez inny prefiks (__runOriginal) bez zapisu blokady; punkt rozkazu liczony przed
    /// lockiem; czas prefiksow w watku AI (czekanie / praca), zgubione linie, szyki stojace > 60 s z celem gry naprzod, pierwszy kontakt - w linii.
    /// </summary>
    internal static class Pendulum333
    {
        internal const float ContactIn = 75f, ContactOut = 90f, SampleEvery = 0.5f, DeadMin = 3f, RefreshEvery = 0.5f;
        internal const float CavClose = 60f, CavNear = 120f, CavClosing = 3f, CavKeep = 4f;
        // 333b: martwa strefa rozkazu, zapadka kierunku (okno podwajane po kazdym nawrocie), trzymanie punktu
        internal const float DeadMove = 12f, Ratchet = 30f, RatchetMax = 240f, HoldMin = 10f;
        internal const float OverMin = 2f;   // 333f: szyk minal P o wiecej - P przesuniety do linii szyku (powrot ponizej progu pomiaru 3 m)
        private const int MaxBeh = 8, MaxRevLines = 30, MaxSetLines = 16, MaxWays = 10;
        // 344: histereza zachowan, blokada przeciwnego rozkazu, ustawienie Line / Loose
        internal const float BehHold = 6f, Block = 10f, ArrHold = 20f, ArrContact = 5f, ArrFire = 0.1f;
        // 344r: wejscie w Loose pod ostrzalem - prog wejscia gry (BehaviorProtectFlank 0.2); szyk stojacy przed kontaktem (pomiar W6); szybka zmiana taktyki
        internal const float ArrFireIn = 0.2f, Still = 60f, TacFast = 10f;
        private const int MaxPend = 32;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class PF
        {
            public Formation F;
            public bool Contact, FootContact;
            public float CavGap = float.MaxValue, CavAt = -1000f, CavUntil = -1000f;
            public float Gap = float.MaxValue, GapAt = -1000f;
            public Vec2 Axis = Vec2.Invalid;
            // 333b: punkt przyjety, zapadka
            public Vec2 P = Vec2.Invalid;
            public WorldPosition PW;
            public float PAt = -1000f, DirAt = -1000f, ArrAt = -1000f;   // 333e: ArrAt - dojscie do P (srodek ciezkosci w martwej strefie)
            public sbyte Dir;
            public int Revs;
            // pomiar
            public Vec2 Last = Vec2.Invalid;
            public float X, Lo, Hi, Ext;
            public sbyte Trend;
            // 333d: diagnostyka
            public float OutAt = -1000f;
            public int AccN;
            public char AccRule = '-';
            public sbyte AccSg;
            public float AccAt = -1000f;
            public Vec2 T = Vec2.Invalid;
            public float TAt = -1000f;
            public MovementOrder.MovementOrderEnum Ord;
            public float OrdAt = -1000f;
            public int Fac = -1, Arr = -1;
            // srodek ciezkosci (srednia pozycji ludzi) - ten sam zygzak co mediana
            public Vec2 LastA = Vec2.Invalid;
            public float XA, LoA, HiA, ExtA;
            public sbyte TrendA;
            // stan mediany w ekstremum i w poprzednim ekstremum
            public float XALo, XAHi, LoAt, HiAt;
            public float ExtAt, ExtPrev, ExtPrevAt, XAExt, XAPrevExt, DepthExt, WidthExt;
            public Vec2 DirExt = Vec2.Invalid, PExt = Vec2.Invalid, CExt = Vec2.Invalid;
            public int AccNExt, FacExt = -1, ArrExt = -1;
            // 344: plan (zachowanie przyjete, kandydat, taktyka), nastepca, blokada 10 s, ustawienie Line / Loose
            public Type Beh, BehNew, Tac;
            public float BehNewAt = -1000f;
            public Vec2 LeadP = Vec2.Invalid, LeadNext = Vec2.Invalid;
            public bool Succ, SuccStill;
            public sbyte BlkDir;
            public float BlkAt = -1000f, FireAt = -1000f, ArrChgAt = -1000f, ArrObsAt = -1000f;
            public int ArrPrevE = -1, ArrObsPrev = -1;
            // 347b B344-1 / B344-3: liczba ludzi przy ostatniej probce i przy ostatniej obserwacji ustawienia, chwila zmiany skladu, wejscie W4 (Loose pod ostrzalem)
            public int Cnt = -1, ArrObsCnt = -1;
            public float CompAt = -1000f, ArrFireAccAt = -1000f;
            // 344r: pozycja prowadzacego przy ostatnim przyjeciu nastepcy (W5), taktyka kandydat (W7), szarza konnicy wroga (W2 techn.), szyk stojacy (W6)
            public Vec2 LeadPos = Vec2.Invalid, LeadPosNext = Vec2.Invalid;
            public Type TacNew;
            public float TacNewAt = -1000f, ChargeUntil = -1000f;
            public Vec2 StillPos = Vec2.Invalid;
            public float StillAt = -1000f;
            public bool StillN, StuckN;
            // 347g (krok A, B344-6): ostatni rozkaz Move zatrzymany galezia Keep nastepcy (prowadzacy bez ruchu) i PF prowadzacego - tylko do linii "stoi z celem naprzod"
            public bool SuccHeld;
            public PF SuccLp;
        }

        /// <summary>344r (recenzja W7): zmiany taktyki druzyny (surowe, przed histereza) - wszystkie i szybkie (w TacFast s od poprzedniej).</summary>
        private sealed class TS
        {
            public Type T;
            public float At = -1000f;
            public int N, Fast;
        }

        private static bool _live, _key, _tried;
        private static int _mainThread = -1;
        private static WeakReference _m;
        private static float _acc;
        private static readonly Dictionary<Formation, PF> Forms = new Dictionary<Formation, PF>();
        private static readonly Dictionary<string, int> RevBy = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<Formation> HeldForms = new HashSet<Formation>();
        // liczniki misji (nazwy segmentu jak w 333: wstecz zatrzymane = zapadka na ruchu wstecz, wstecz dozwolone S4 / po zmianie zachowania = przyjete ruchy wstecz
        // lucznikow S4 / innych, naprzod wstrzymane po cofnieciu = zapadka na ruchu naprzod)
        private static int _seen, _backHeld, _backS4, _backBeh, _fwdHeld, _rev, _revBack, _forms, _contactIn, _contactCav;
        private static int _first, _dead, _hold, _revOk, _accFwd, _accSide;
        private static float _preSec;
        // 333d: diagnostyka
        private static int _revLog, _ordLog, _setLog, _revA, _revABack, _revSmall, _ordChg, _facChg, _arrChg;
        // 333e: nawroty mediany, w ktorych srodek ciezkosci idzie i wraca razem (scisle), punkty obciete do ruchu w bok, nawroty po oknie za male
        private static int _revBoth, _revBothBack, _clip, _zSmall;
        // 333f: P przesuniete po minieciu, cele obciete o mniej niz DeadMin za szykiem
        private static int _over, _clipSmall;
        private static readonly Dictionary<string, int[]> RevWay = new Dictionary<string, int[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> NonMoveBy = new Dictionary<string, int>(StringComparer.Ordinal);
        private static int _stum, _stumPatch;   // recenzja 333 (7): potkniecie latki (przed pierwszym Begin) nie ginie w Clear
        internal static int Stumbles { get { lock (L) return _stum + _stumPatch; } }
        private static string _patch = "nie wpieta", _patchArr = "nie wpieta";
        // 344: jeden lock na caly stan (prefiksy w watku AI silnika i w glownym, pomiar w glownym); watek biezacego wywolania (pod lockiem)
        private static readonly object L = new object();
        private static bool _inMain = true;
        private static HashSet<Formation> _players;   // migawka szykow gracza (glowny watek, podmiana referencji)
        private static readonly List<string> PendLines = new List<string>();
        private static Exception _pendErr;
        private static string _pendWhere;
        private static readonly Dictionary<Type, System.Reflection.FieldInfo> LeadField = new Dictionary<Type, System.Reflection.FieldInfo>();
        private static System.Reflection.FieldInfo _fTactic;
        private static bool _tacticTried;
        private static readonly Dictionary<string, int[]> SrcBy = new Dictionary<string, int[]>(StringComparer.Ordinal);
        private static int _callMain, _callAi, _arrMain, _arrAi, _plan, _blink, _tac, _succKeep, _succAcc, _succNo, _blk;
        private static int _arrSeen, _arrAcc, _arrBlk, _arrBack, _arrCav, _arrBlkC, _arrRet;
        private static int _comp, _arrRetFire, _arrRetComp;   // 347b: zmiany skladu szyku (zygzak od nowa), powroty Line/Loose z wejscia W4 / przy zmianie skladu
        private static float _ffSec, _rtSec;
        // 344r: czas w prefiksach w watku AI (czekanie na lock / praca, ticki Stopwatch), zgubione linie, krok naprzod w martwej strefie, szyki stojace, taktyki
        private static long _aiWait, _aiWaitMax, _aiWork, _aiWorkMax;
        private static int _aiN, _lost, _fwdStep, _still, _stuck, _tacBlink, _arrFireAcc;
        // 347d (B344-6c / B344-1b, diagnostyka): linie szyku stojacego z celem naprzod i cofniecia srodka ciezkosci (najwyzej MaxDiagLines kazdej na misje)
        private const int MaxDiagLines = 16;
        private static int _stuckLog, _backALog;
        private static float _depAt = -1f, _contact1 = -1f;
        private static readonly Dictionary<Team, TS> TacBy = new Dictionary<Team, TS>();

        // ------------------------------------------------------------ latka (Doctrine.ApplyMissionPatches - pierwsza bitwa, raz na proces)

        internal static string ApplyPatch(Harmony h)
        {
            if (_tried) return _patch;
            _tried = true;
            try
            {
                var target = AccessTools.DeclaredMethod(typeof(Formation), nameof(Formation.SetMovementOrder), new[] { typeof(MovementOrder) });
                if (target == null) { _patch = "NIE (brak Formation.SetMovementOrder)"; return _patch; }
                h.Patch(target, prefix: new HarmonyMethod(typeof(Pendulum333), nameof(Pre)) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                _patch = "TAK";
            }
            catch (Exception e)
            {
                _patch = "NIE (" + e.GetType().Name + ")";
                _stumPatch++;
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Pendulum333.Latka", e);
            }
            // 344: ustawienie (Line / Loose) - osobna latka (jej brak nie gasi latki ruchu)
            try
            {
                var ta = AccessTools.DeclaredMethod(typeof(Formation), nameof(Formation.SetArrangementOrder), new[] { typeof(ArrangementOrder) });
                if (ta == null) _patchArr = "NIE (brak Formation.SetArrangementOrder)";
                else
                {
                    h.Patch(ta, prefix: new HarmonyMethod(typeof(Pendulum333), nameof(PreArr)) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                    _patchArr = "TAK";
                }
            }
            catch (Exception e)
            {
                _patchArr = "NIE (" + e.GetType().Name + ")";
                _stumPatch++;
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Pendulum333.LatkaUstawienia", e);
            }
            return _patch + ", ustawienie 344 " + _patchArr;
        }

        // ------------------------------------------------------------ misja (HorsePursuit.Begin / Tick / End - glowny watek)

        internal static void Begin(Mission m, bool field)
        {
            lock (L)
            {
                Clear();
                _live = field && m != null;
                _m = m != null ? new WeakReference(m) : null;
                _mainThread = Thread.CurrentThread.ManagedThreadId;
                var c = Settings.Current;
                _key = c != null && c.FormationOrderArbiter;
            }
        }

        internal static void End()
        {
            lock (L)
            {
                _live = false;
                Clear();
                _m = null;
            }
        }

        private static void Clear()
        {
            Volatile.Write(ref _players, null);
            PendLines.Clear();
            _pendErr = null;
            _pendWhere = null;
            SrcBy.Clear();
            _callMain = _callAi = _arrMain = _arrAi = _plan = _blink = _tac = _succKeep = _succAcc = _succNo = _blk = 0;
            _arrSeen = _arrAcc = _arrBlk = _arrBack = _arrCav = _arrBlkC = _arrRet = 0;
            _comp = _arrRetFire = _arrRetComp = 0;
            _ffSec = _rtSec = 0f;
            _aiWait = _aiWaitMax = _aiWork = _aiWorkMax = 0;
            _aiN = _lost = _fwdStep = _still = _stuck = _tacBlink = _arrFireAcc = 0;
            _stuckLog = _backALog = 0;
            _depAt = _contact1 = -1f;
            TacBy.Clear();
            Forms.Clear(); RevBy.Clear(); HeldForms.Clear();
            _acc = 0f;
            _seen = _backHeld = _backS4 = _backBeh = _fwdHeld = _rev = _revBack = _forms = _contactIn = _contactCav = 0;
            _first = _dead = _hold = _revOk = _accFwd = _accSide = 0;
            _preSec = 0f;
            _stum = 0;
            _revLog = _ordLog = _setLog = _revA = _revABack = _revSmall = _ordChg = _facChg = _arrChg = 0;
            _revBoth = _revBothBack = _clip = _zSmall = 0;
            _over = _clipSmall = 0;
            RevWay.Clear(); NonMoveBy.Clear();
        }

        private static bool Here(Mission m)
        {
            var w = _m;
            return _live && m != null && w != null && ReferenceEquals(w.Target, m);
        }

        /// <summary>Szyk, ktorego to dotyczy: AI, nie gracza, pieszy, od IntactMin ludzi, poza rozstawieniem.</summary>
        private static bool Eligible(Formation f)
        {
            if (f == null || f.CountOfUnits < HorsePursuit.IntactMin || !f.IsAIControlled || f.IsDeployment) return false;
            if (!MissionTeams.Valid(f.Team) || NotAi(f)) return false;
            var q = f.QuerySystem;
            if (q == null || q.IsCavalryFormation || q.IsRangedCavalryFormation) return false;
            return q.IsInfantryFormation || q.IsRangedFormation;
        }

        /// <summary>344: szyk gracza wedlug migawki glownego watku (bez migawki - jeszcze nie wiadomo: nie dotykamy).</summary>
        private static bool NotAi(Formation f)
        {
            var ps = Volatile.Read(ref _players);
            return ps == null || ps.Contains(f);
        }

        private static PF Get(Formation f)
        {
            PF pf;
            if (!Forms.TryGetValue(f, out pf)) { pf = new PF { F = f }; Forms[f] = pf; _forms++; }
            return pf;
        }

        /// <summary>Odleglosc miara RBM (mediany + pol glebokosci obu) do najblizszego pieszego szyku wroga od IntactMin ludzi (bez pieszego - jakiegokolwiek), os do niego,
        /// stan kontaktu z histereza. Najwyzej co RefreshEvery s.</summary>
        private static void Refresh(PF pf, float now)
        {
            if (now - pf.GapAt < RefreshEvery) return;
            pf.GapAt = now;
            var f = pf.F;
            var own = f.Team;
            var m = Mission.Current;
            Vec2 c = f.CachedMedianPosition.AsVec2;
            float best = float.MaxValue, bestAny = float.MaxValue, bestCav = float.MaxValue;
            Vec2 ax = Vec2.Invalid, axAny = Vec2.Invalid;
            var teams = m != null ? m.Teams : null;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team t;
                try { t = teams[i]; } catch { continue; }
                if (!MissionTeams.Valid(t) || own == null || !t.IsEnemyOf(own)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var e = fl[j];
                    if (e == null || e.CountOfUnits < HorsePursuit.IntactMin) continue;
                    var eq = e.QuerySystem;
                    if (eq == null) continue;
                    Vec2 ec = e.CachedMedianPosition.AsVec2;
                    if (!ec.IsValid) continue;
                    float d = c.Distance(ec) + 0.5f * (f.Depth + e.Depth);
                    bool foot = !eq.IsCavalryFormation && !eq.IsRangedCavalryFormation;
                    if (foot && d < best) { best = d; ax = ec - c; }
                    if (d < bestAny) { bestAny = d; axAny = ec - c; }
                    if (!foot && d < bestCav) bestCav = d;
                }
            }
            try { var fq = f.QuerySystem; if (fq != null && fq.UnderRangedAttackRatio >= ArrFire) pf.FireAt = now; } catch { }   // 344: ostrzal szyku (powrot ustawienia)
            float near = bestAny;   // kontakt: pieszy wrog albo dowolny szyk (konnica) miara RBM - recenzja 333 W1
            if (best == float.MaxValue) { best = bestAny; ax = axAny; }
            pf.Gap = best;
            if (ax.IsValid && ax.Length > 0.1f) pf.Axis = ax.Normalized();
            bool was = pf.Contact;
            if (near <= ContactIn) pf.FootContact = true;
            else if (near > ContactOut) pf.FootContact = false;
            // konnica wroga: bardzo blisko albo w 120 m i szarzuje (zbliza sie >= 3 m/s) - kontakt na CavKeep s od ostatniego takiego pomiaru
            bool charge = bestCav <= CavNear && pf.CavGap != float.MaxValue && now - pf.CavAt > 0.05f && now - pf.CavAt < 3f
                          && (pf.CavGap - bestCav) / (now - pf.CavAt) >= CavClosing;
            bool cav = bestCav <= CavClose || charge;
            pf.CavGap = bestCav;
            pf.CavAt = now;
            if (cav) pf.CavUntil = now + CavKeep;
            if (charge) pf.ChargeUntil = now + CavKeep;   // 344r: prawdziwa szarza (zbliza sie) - osobno od samej bliskosci konnicy
            pf.Contact = pf.FootContact || now < pf.CavUntil;
            if (pf.Contact && !was) { _contactIn++; if (!pf.FootContact) _contactCav++; if (_contact1 < 0f && _depAt >= 0f) _contact1 = Math.Max(0f, now - _depAt); }
            if (!pf.Contact && was) pf.OutAt = now;   // 333d
        }

        // ------------------------------------------------------------ prefiks Formation.SetMovementOrder

        private static void Pre(Formation __instance, ref MovementOrder input)
        {
            if (!_live) return;
            bool main = Thread.CurrentThread.ManagedThreadId == _mainThread;
            long t0 = main ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
            // recenzja 344 W5 (techn.): punkt rozkazu (wywolanie silnika) przed lockiem - nie zalezy od stanu arbitra
            WorldPosition tw = WorldPosition.Invalid;
            bool twOk = false;
            if (_key && __instance != null && input.OrderEnum == MovementOrder.MovementOrderEnum.Move)
            {
                try { tw = input.CreateNewOrderWorldPositionMT(__instance, WorldPosition.WorldPositionEnforcedCache.None); twOk = true; } catch { twOk = false; }
            }
            lock (L)
            {
                long t1 = main ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
                _inMain = main;
                try { PreIn(__instance, ref input, main, tw, twOk); }
                catch (Exception e) { _stum++; Err("Pendulum333.Rozkaz", e); }
                finally { _inMain = true; if (!main) AiTime(t0, t1); }
            }
        }

        /// <summary>344r (recenzja D7): czas prefiksu w watku AI - czekanie na lock (t0..t1) i praca (t1..teraz); pod lockiem.</summary>
        private static void AiTime(long t0, long t1)
        {
            long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
            long w = t1 - t0, k = t2 - t1;
            _aiN++;
            _aiWait += w; if (w > _aiWaitMax) _aiWaitMax = w;
            _aiWork += k; if (k > _aiWorkMax) _aiWorkMax = k;
        }

        private static string Ms(long ticks)
        {
            return (ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.00", Inv);
        }

        /// <summary>344: prefiks ruchu pod lockiem - w kazdym watku (333: tylko glowny - w grze Jeffa 0 wywolan). Wyjatki lapie Pre.</summary>
        private static void PreIn(Formation __instance, ref MovementOrder input, bool main, WorldPosition tw, bool twOk)
        {
            if (main) _callMain++; else _callAi++;
            {
                var m = Mission.Current;
                if (!Here(m) || !m.IsDeploymentFinished) return;
                var f = __instance;
                if (!Eligible(f)) return;
                float now = m.CurrentTime;
                var pf = Get(f);
                Refresh(pf, now);
                // 333d: kazdy rozkaz zapamietany (OrderEnum, czas); nie-Move przed kontaktem - licznik i linia przy zmianie
                var oe = input.OrderEnum;
                var prevOrd = pf.Ord;
                pf.Ord = oe;
                pf.OrdAt = now;
                bool pre = !pf.Contact && pf.Gap != float.MaxValue && pf.Axis.IsValid;
                if (oe != MovementOrder.MovementOrderEnum.Move)
                {
                    if (pre) NonMove(pf, oe, prevOrd, now);
                    return;
                }
                if (!_key) return;
                if (!pre) { Forget(pf, pf.Contact && !pf.FootContact); return; }
                Vec2 q = Cur(f);   // 333f: pozycja szyku w ukladzie punktu rozkazu (srodek ciezkosci przesuniety o srednie miejsce w szyku - Formation.CurrentPosition)
                if (!twOk) return;
                Vec2 tp = tw.AsVec2;
                if (!q.IsValid || !tw.IsValid || !tp.IsValid) return;
                _seen++;
                pf.T = tp;   // 333d: ostatni cel gry
                pf.TAt = now;
                float dz = Math.Max(DeadMove, f.Depth);
                Type beh = null;
                BehaviorComponent bc = null;
                try { var ai = f.AI; bc = ai != null ? ai.ActiveBehavior : null; beh = bc != null ? bc.GetType() : null; } catch { beh = null; bc = null; }
                // 344 (2): histereza zachowan - zmiana planu dopiero po BehHold s, zmianie taktyki druzyny albo szarzy / odwrocie / Advance po kontakcie
                PlanCheck(f, pf, beh, now);
                // 344 (1): szyk boczny (nastepca) rusza tylko po przyjetej zmianie punktu prowadzacego
                pf.Succ = false;
                pf.SuccStill = false;
                pf.SuccHeld = false;   // 347g krok A (diagnostyka)
                pf.SuccLp = null;
                pf.LeadNext = Vec2.Invalid;
                if (beh == typeof(BehaviorProtectFlank) || beh == typeof(BehaviorScreenedSkirmish))
                {
                    var lead = Leader(f, bc);
                    PF lp;
                    if (lead != null && Forms.TryGetValue(lead, out lp) && lp.P.IsValid && !lp.Contact)
                    {
                        float ldz = Math.Max(DeadMove, lead.Depth);
                        // recenzja 344 W5: prowadzacy idzie do dalekiego punktu (P bez zmian) - nastepca rusza za jego POZYCJA (>= martwa strefa od ostatniego przyjecia)
                        Vec2 lc = Cur(lead);
                        bool moved = !pf.LeadP.IsValid || !pf.LeadPos.IsValid || lp.P.Distance(pf.LeadP) >= ldz || (lc.IsValid && lc.Distance(pf.LeadPos) >= ldz);
                        if (!moved && pf.P.IsValid) { pf.SuccHeld = true; pf.SuccLp = lp; Keep(f, pf, ref input, beh); _succKeep++; return; }   // 347g krok A: SuccHeld - tylko linia
                        pf.Succ = true;
                        pf.SuccStill = !moved;
                        pf.LeadNext = lp.P;
                        pf.LeadPosNext = lc;
                    }
                }
                // 333f: kierunek celu liczony od miejsca, w ktorym szyk JEST (q), nie od P - 333e liczyl zapadke i obciecie od P, wiec gdy P zostal za szykiem
                // (szyk doszedl dalej albo P obciety "w bok" od starego P), punkt "w bok" albo "nawrot za maly" i tak cofal szyk (bieg R3o1: nowy P regula B 4,
                // regula Z 2, przestrzelenie P 4)
                float u = (tp - q).DotProduct(pf.Axis);
                sbyte sg = SignD(u);
                // 344 (3): w Block s od przyjecia punktu rozkaz przeciwny na osi do wroga (>= DeadMin) obciety do linii szyku - takze po zmianie planu
                if (pf.BlkDir != 0 && now - pf.BlkAt < Block && u * pf.BlkDir < -DeadMin)
                {
                    _blk++;
                    Vec2 tc = tp - pf.Axis * u;
                    if (pf.P.IsValid && tc.Distance(pf.P) < dz) { Keep(f, pf, ref input, beh); return; }
                    if (Side(f, pf, tw, tc, now, beh, ref input)) return;
                    if (pf.P.IsValid) Keep(f, pf, ref input, beh);
                    return;
                }
                // 1. pierwszy rozkaz przed kontaktem - przyjety (powrot obroncy na wzgorze, lucznicy za piechote); po kontakcie samej konnicy (Dir zostal) - cel
                // przeciwny do ruchu sprzed kontaktu obciety do linii szyku (bez nawrotu po 4 s szarzy)
                if (!pf.P.IsValid)
                {
                    _first++;
                    if (pf.Dir != 0 && u * pf.Dir < 0f && !(u * pf.Dir < -DeadMin && now - pf.DirAt >= Win(pf) && Math.Abs(u) >= 2f * dz)
                        && Side(f, pf, tw, tp - pf.Axis * u, now, beh, ref input))
                    {
                        pf.AccRule = '1';
                        return;
                    }
                    Accept(pf, tw, tp, sg, now, beh, '1');
                    return;
                }
                // 0. 333f: szyk minal P wzdluz kierunku ruchu (o > OverMin) - P przesuniety na os do linii szyku: bez powrotu do punktu juz minietego
                if (pf.Dir != 0)
                {
                    float o = (q - pf.P).DotProduct(pf.Axis);
                    if (o * pf.Dir > OverMin) Over(pf, o, now);
                }
                char rule = '5';
                Vec2 tt = tp;
                bool clip = false;
                // 3. zapadka kierunku (okno 30 s, podwajane po kazdym przepuszczonym nawrocie) - od pozycji szyku: kazda skladowa celu na osi za szykiem (wzgledem
                // kierunku ruchu) obcieta do linii szyku (zostaje sam ruch w bok); nawrot tylko po oknie i >= 2 martwe strefy od szyku
                if (pf.Dir != 0 && u * pf.Dir < 0f)
                {
                    bool big = u * pf.Dir < -DeadMin;
                    bool inWin = now - pf.DirAt < Win(pf);
                    // 347d (B344-6, at347 l.790-834 Infantry 26 HoldHighGround ~100 s z celem gry naprzod, at346 10:53 Ranged 51 ScreenedSkirmish regula B 97 s):
                    // przed kontaktem cel NAPRZOD do wroga (u > 0 przy Dir < 0) po oknie przechodzi bez wymogu 2 martwych stref (wystarczy poza martwa strefa P);
                    // cofanie (u < 0) jak dotad
                    // 347d-r (recenzja): tylko szyk stojacy na P (q w martwej strefie P) - szyk w drodze wstecz do P nie zawraca od kroku RBM 3-5 m (P za nim)
                    if (big && !inWin && (Math.Abs(u) >= 2f * dz || (u > 0f && q.Distance(pf.P) < dz && tp.Distance(pf.P) >= dz)))
                    {
                        pf.Revs++;
                        _revOk++;
                        rule = 'Z';   // zapadka przepuscila nawrot po oknie
                    }
                    else
                    {
                        if (big)
                        {
                            if (!inWin) _zSmall++;
                            if (u < 0f) _backHeld++; else _fwdHeld++;
                        }
                        else _clipSmall++;
                        tt = tp - pf.Axis * u;
                        clip = true;
                    }
                }
                // recenzja 344 W6: krok naprzod do wroga (RBM CautiousAdvance +5 / +10 m) wewnatrz martwej strefy - szyk stojacy na P po trzymaniu HoldMin s
                // przyjmuje go (nie przy ruchu wstecz zapadki) - inaczej szyk przed kontaktem stalby bez konca
                if (q.Distance(pf.P) < dz && pf.ArrAt < pf.PAt) pf.ArrAt = now;
                bool fwd = !clip && rule != 'Z' && pf.Dir >= 0 && u >= DeadMin && tt.Distance(pf.P) < dz && q.Distance(pf.P) < dz && pf.ArrAt >= pf.PAt
                           && now - Math.Max(pf.PAt, pf.ArrAt) >= HoldMin;
                if (fwd) { _fwdStep++; Accept(pf, tw, tp, (sbyte)1, now, beh, 'K'); return; }
                // 2. martwa strefa (cel po obcieciu)
                if (tt.Distance(pf.P) < dz) { Keep(f, pf, ref input, beh); if (!clip) _dead++; return; }
                // 4. trzymanie: szyk stoi na P - nowy punkt najwczesniej HoldMin s po przyjeciu P i od dojscia do P (333e)
                if (rule != 'Z' && q.Distance(pf.P) < dz)
                {
                    if (pf.ArrAt < pf.PAt) pf.ArrAt = now;
                    if (now - Math.Max(pf.PAt, pf.ArrAt) < HoldMin)
                    {
                        Keep(f, pf, ref input, beh);
                        _hold++;
                        return;
                    }
                }
                // 5. przyjety (obciety - ruch w bok bez zmiany kierunku zapadki; poza siatka nawigacji zostaje P)
                if (clip)
                {
                    if (!Side(f, pf, tw, tt, now, beh, ref input)) Keep(f, pf, ref input, beh);
                    return;
                }
                Accept(pf, tw, tp, sg, now, beh, rule);
            }
        }

        /// <summary>344: potkniecie - w glownym watku od razu, z watku AI do wypisania w Tick (pierwsze na pomiar).</summary>
        private static void Err(string where, Exception e)
        {
            if (_inMain) { RoutHold.Stumble(RoutHold.SiteDoctrine, where, e); return; }
            if (_pendErr == null) { _pendErr = e; _pendWhere = where; }
        }

        /// <summary>344: linia - w glownym watku od razu, z watku AI do kolejki (Tick).</summary>
        private static void Line(string txt)
        {
            if (_inMain) { Log.Info(txt); return; }
            if (PendLines.Count < MaxPend) PendLines.Add(txt); else _lost++;
        }

        /// <summary>344 (2): histereza zachowan i zmiana planu.</summary>
        private static void PlanCheck(Formation f, PF pf, Type beh, float now)
        {
            if (beh == null) return;
            Type tac = Tactic(f.Team);
            TacRaw(f.Team, tac, now);
            // recenzja 344 W7: taktyka druzyny pod ta sama histereza BehHold s co zachowanie (291B / RBM przerzucajace taktyke - powrot w BehHold s = migniecie)
            bool tacChg = false;
            if (tac != null)
            {
                if (pf.Tac == null) pf.Tac = tac;
                else if (tac == pf.Tac) { if (pf.TacNew != null) { _tacBlink++; pf.TacNew = null; } }
                else
                {
                    if (pf.TacNew != tac) { pf.TacNew = tac; pf.TacNewAt = now; }
                    if (now - pf.TacNewAt >= BehHold) { pf.Tac = tac; pf.TacNew = null; tacChg = true; }
                }
            }
            if (pf.Beh == null) { pf.Beh = beh; return; }
            if (beh == pf.Beh)
            {
                if (pf.BehNew != null) { _blink++; pf.BehNew = null; }
                if (tacChg) _tac++;
                return;
            }
            if (pf.BehNew != beh) { pf.BehNew = beh; pf.BehNewAt = now; }
            // recenzja 344 D4 (techn.): szarza i odwrot wydaja rozkazy nie-Move (PreIn wraca przed PlanCheck) - "twarda" zmiana planu tylko Advance po kontakcie
            bool hard = beh == typeof(BehaviorAdvance) && pf.OutAt > -999f;
            if (now - pf.BehNewAt < BehHold && !tacChg && !hard) return;
            pf.Beh = beh;
            pf.BehNew = null;
            _plan++;
            if (tacChg) _tac++;
            pf.P = Vec2.Invalid;   // regula 1 przyjmie pierwszy rozkaz nowego planu (blokada 10 s zostaje)
            pf.Dir = 0;
            pf.DirAt = -1000f;
            pf.Revs = 0;
        }

        /// <summary>344r (recenzja W7): surowe zmiany taktyki druzyny (pod lockiem) - licznik i szybkie (w TacFast s od poprzedniej).</summary>
        private static void TacRaw(Team t, Type tac, float now)
        {
            if (t == null || tac == null) return;
            TS ts;
            if (!TacBy.TryGetValue(t, out ts)) { if (TacBy.Count >= MaxBeh) return; ts = new TS { T = tac, At = now }; TacBy[t] = ts; return; }
            if (ts.T == tac) return;
            ts.N++;
            if (now - ts.At < TacFast) ts.Fast++;
            ts.T = tac;
            ts.At = now;
        }

        /// <summary>344: typ biezacej taktyki druzyny (TeamAIComponent._currentTactic, refleksja raz na proces).</summary>
        private static Type Tactic(Team t)
        {
            try
            {
                if (!_tacticTried)
                {
                    _tacticTried = true;
                    _fTactic = AccessTools.Field(typeof(TeamAIComponent), "_currentTactic");
                }
                if (_fTactic == null || t == null) return null;
                var ai = t.TeamAI;
                if (ai == null) return null;
                var v = _fTactic.GetValue(ai);
                return v != null ? v.GetType() : null;
            }
            catch { return null; }
        }

        /// <summary>344 (1): szyk prowadzacy nastepcy - pole _mainFormation zachowania (refleksja raz na typ), bez niego najwiekszy szyk piechoty druzyny.</summary>
        private static Formation Leader(Formation f, BehaviorComponent bc)
        {
            Formation lead = null;
            if (bc != null)
            {
                var bt = bc.GetType();
                System.Reflection.FieldInfo fi;
                if (!LeadField.TryGetValue(bt, out fi))
                {
                    try { fi = AccessTools.Field(bt, "_mainFormation"); } catch { fi = null; }
                    if (fi != null && fi.FieldType != typeof(Formation)) fi = null;
                    LeadField[bt] = fi;
                }
                if (fi != null) { try { lead = fi.GetValue(bc) as Formation; } catch { lead = null; } }
            }
            if (lead != null && !ReferenceEquals(lead, f) && lead.CountOfUnits > 0) return lead;
            var t = f.Team;
            if (t == null) return null;
            var fl = t.FormationsIncludingSpecialAndEmpty;
            int nf = fl != null ? fl.Count : 0, best = 0;
            lead = null;
            for (int j = 0; j < nf; j++)
            {
                var x = fl[j];
                if (x == null || ReferenceEquals(x, f) || x.CountOfUnits <= best) continue;
                var xq = x.QuerySystem;
                if (xq == null || !xq.IsInfantryFormation) continue;
                best = x.CountOfUnits;
                lead = x;
            }
            return lead;
        }

        /// <summary>344 B344-2: rozkazy wedlug zrodla (zachowanie): 0 przyjete, 1 trzymane, 2 obciete.</summary>
        private static void Src(Type beh, int k)
        {
            string n = beh != null ? beh.Name : "brak";
            int[] v;
            if (!SrcBy.TryGetValue(n, out v))
            {
                if (SrcBy.Count >= MaxBeh) n = "inne";
                if (!SrcBy.TryGetValue(n, out v)) { v = new int[3]; SrcBy[n] = v; }
            }
            v[k]++;
        }

        /// <summary>344: punkt na siatce nawigacji - w watku AI wersje MT gry (blokada fizyki do odczytu).</summary>
        private static bool OnMesh(ref WorldPosition w, Vec2 p)
        {
            if (_inMain)
            {
                w.SetVec2(p);
                return w.IsValid && w.GetNavMesh() != UIntPtr.Zero;
            }
            w.SetVec2MT(p);
            return w.IsValid && w.GetNavMeshMT() != UIntPtr.Zero;
        }

        // ------------------------------------------------------------ 344 (4): prefiks Formation.SetArrangementOrder

        /// <summary>344: Line / Loose szyku AI przed kontaktem - najwyzej raz na ArrHold s, powrot do poprzedniego dopiero po ArrHold s bez ostrzalu; kontakt
        /// ArrContact s, szarza konnicy wroga - od razu. false = zostaje biezace ustawienie (bez SoftUpdate).</summary>
        public static bool PreArr(Formation __instance, ArrangementOrder order, bool __runOriginal)
        {
            if (!__runOriginal) return false;   // recenzja 344 D3: inny prefiks (CloseRanks 310) juz odrzucil zmiane - bez zapisu blokady
            if (!_live) return true;
            bool main = Thread.CurrentThread.ManagedThreadId == _mainThread;
            long t0 = main ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
            lock (L)
            {
                long t1 = main ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
                _inMain = main;
                try { return PreArrIn(__instance, order, main); }
                catch (Exception e) { _stum++; Err("Pendulum333.Ustawienie", e); return true; }
                finally { _inMain = true; if (!main) AiTime(t0, t1); }
            }
        }

        private static bool PreArrIn(Formation f, ArrangementOrder order, bool main)
        {
            if (f == null) return true;
            var cur = f.ArrangementOrder.OrderEnum;
            var nw = order.OrderEnum;
            if (cur == nw) return true;
            bool ll = (cur == ArrangementOrder.ArrangementOrderEnum.Line || cur == ArrangementOrder.ArrangementOrderEnum.Loose)
                   && (nw == ArrangementOrder.ArrangementOrderEnum.Line || nw == ArrangementOrder.ArrangementOrderEnum.Loose);
            if (!ll) return true;
            var m = Mission.Current;
            if (!Here(m) || !m.IsDeploymentFinished) return true;
            if (!Eligible(f)) return true;   // recenzja 344 W3 (techn.): tylko piechota i lucznicy AI (bez konnicy, bez wpisow PF dla niej)
            if (main) _arrMain++; else _arrAi++;
            float now = m.CurrentTime;
            var pf = Get(f);
            Refresh(pf, now);
            _arrSeen++;
            if (!_key) return true;
            // recenzja 344 W4: wejscie w Loose pod ostrzalem (prog wejscia gry) zawsze - wahadlo to powrot do Line, nie samo rozluznienie
            if (nw == ArrangementOrder.ArrangementOrderEnum.Loose)
            {
                float ur = 0f;
                try { var fq = f.QuerySystem; ur = fq != null ? fq.UnderRangedAttackRatio : 0f; } catch { ur = 0f; }
                if (ur >= ArrFireIn) { _arrFireAcc++; pf.FireAt = now; pf.ArrFireAccAt = now; pf.ArrPrevE = (int)cur; pf.ArrChgAt = now; _arrAcc++; return true; }
            }
            if (pf.Contact)
            {
                // recenzja 344 W2 (techn.): od razu tylko przy prawdziwej szarzy konnicy wroga (zbliza sie) i tylko do Line; sama bliskosc konnicy - regula ArrContact s
                if (!pf.FootContact && now < pf.ChargeUntil && nw == ArrangementOrder.ArrangementOrderEnum.Line) { _arrCav++; pf.ArrPrevE = (int)cur; pf.ArrChgAt = now; return true; }
                if (now - pf.ArrChgAt < ArrContact) { _arrBlkC++; return false; }
                pf.ArrPrevE = (int)cur;
                pf.ArrChgAt = now;
                _arrAcc++;
                return true;
            }
            if (now - pf.ArrChgAt < ArrHold) { _arrBlk++; return false; }
            if ((int)nw == pf.ArrPrevE && now - pf.FireAt < ArrHold) { _arrBack++; return false; }
            pf.ArrPrevE = (int)cur;
            pf.ArrChgAt = now;
            _arrAcc++;
            return true;
        }

        /// <summary>333f: kierunek celu wzgledem szyku - od DeadMin (3 m; 333e: pol martwej strefy - cel 3-6 m za szykiem "w bok" nie ustawial kierunku).</summary>
        private static sbyte SignD(float u)
        {
            return u > DeadMin ? (sbyte)1 : u < -DeadMin ? (sbyte)-1 : (sbyte)0;
        }

        /// <summary>Okno zapadki szyku: 30 / 60 / 120 / 240 s wedlug przepuszczonych nawrotow.</summary>
        private static float Win(PF pf)
        {
            return Math.Min(RatchetMax, Ratchet * (float)Math.Pow(2.0, Math.Min(pf.Revs, 3)));
        }

        /// <summary>333f: pozycja szyku w ukladzie punktu rozkazu - Formation.CurrentPosition (srodek ciezkosci przesuniety o srednie miejsce ludzi w szyku;
        /// Move w P stawia przod szyku w P, srodek ciezkosci stoi pol glebokosci za nim); bez niego srodek ciezkosci, potem mediana.</summary>
        private static Vec2 Cur(Formation f)
        {
            Vec2 v;
            try { v = f.CurrentPosition; } catch { v = Vec2.Invalid; }
            return v.IsValid ? v : Pos(f);
        }

        /// <summary>333f: szyk minal P o o metrow wzdluz osi (w kierunku ruchu) - P przesuniety o o na osi (bok bez zmian), gdy na siatce nawigacji; trzymanie od teraz.</summary>
        private static void Over(PF pf, float o, float now)
        {
            Vec2 np = pf.P + pf.Axis * o;
            var w = pf.PW;
            if (!OnMesh(ref w, np)) return;
            pf.P = np;
            pf.PW = w;
            pf.ArrAt = now;
            pf.AccN++;
            pf.AccRule = 'O';
            pf.AccSg = 0;
            pf.AccAt = now;
            _over++;
        }

        private static void Accept(PF pf, WorldPosition tw, Vec2 tp, sbyte sg, float now, Type beh, char rule, bool count = true)
        {
            if (count) Src(beh, 0);
            if (sg != 0) { pf.BlkDir = sg; pf.BlkAt = now; }   // 344 (3)
            if (pf.Succ)
            {
                if (pf.LeadNext.IsValid) pf.LeadP = pf.LeadNext;
                if (pf.LeadPosNext.IsValid) pf.LeadPos = pf.LeadPosNext;
                if (pf.SuccStill && pf.P.IsValid) _succNo++; else _succAcc++;   // 347b B344-5: nastepca bez P (pierwszy punkt / po Forget, takze regula B przez Side) to nie ruch bez zmiany prowadzacego
                pf.Succ = false;
            }
            pf.AccN++;   // 333d
            pf.AccRule = rule;
            pf.AccSg = sg;
            pf.AccAt = now;
            pf.P = tp;
            pf.PW = tw;
            pf.PAt = now;
            if (sg != 0) { pf.Dir = sg; pf.DirAt = now; }
            if (sg > 0) _accFwd++;
            else if (sg < 0) { if (beh == typeof(BehaviorScreenedSkirmish)) _backS4++; else _backBeh++; }
            else _accSide++;
        }

        /// <summary>Kontakt: punkt przyjety i zapadka zapomniane (po wyjsciu z kontaktu - od nowa; licznik nawrotow okna zostaje). 333f: kontakt samej konnicy
        /// (4 s od szarzy, bez pieszego wroga w 75 m) - kierunek zapadki zostaje (keepDir), znika tylko P.</summary>
        private static void Forget(PF pf, bool keepDir)
        {
            pf.P = Vec2.Invalid;
            if (keepDir) return;
            pf.Dir = 0;
            pf.DirAt = -1000f;
        }

        /// <summary>333e: pozycja szyku dla latki - srodek ciezkosci (srednia pozycji ludzi), bez niego mediana.</summary>
        private static Vec2 Pos(Formation f)
        {
            Vec2 a = f.CachedAveragePosition;
            return a.IsValid ? a : f.CachedMedianPosition.AsVec2;
        }

        /// <summary>333e: punkt obciety do osi P (sam ruch w bok) - przyjety bez zmiany kierunku zapadki, gdy lezy na siatce nawigacji; false - zostaje P.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]   // 333t2: MovementOrder tylko w misji
        private static bool Side(Formation f, PF pf, WorldPosition tw, Vec2 tc, float now, Type beh, ref MovementOrder input)
        {
            var w = tw;
            if (!OnMesh(ref w, tc)) return false;
            input = MovementOrder.MovementOrderMove(w);
            HeldForms.Add(f);
            _clip++;
            Src(beh, 2);
            Accept(pf, w, tc, 0, now, beh, 'B', false);
            return true;
        }

        /// <summary>Rozkaz zamieniony na Move w punkt przyjety P (ta sama WorldPosition).</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]   // 333t2: MovementOrder tylko w misji (Pre: Here(m))
        private static void Keep(Formation f, PF pf, ref MovementOrder input, Type beh)
        {
            input = MovementOrder.MovementOrderMove(pf.PW);
            HeldForms.Add(f);
            Src(beh, 1);
        }

        // ------------------------------------------------------------ pomiar wahadla (co 0.5 s)

        internal static void Tick(Mission m, float now, float dt)
        {
            if (!Here(m)) return;
            _acc += dt;
            if (_acc < SampleEvery) return;
            float step = _acc;
            _acc = 0f;
            // 344: migawka szykow gracza dla prefiksow w watku AI (PlayerForm czyta stan gracza - tylko glowny watek), podmiana referencji
            try { Volatile.Write(ref _players, PlayerSnap(m)); } catch (Exception e) { lock (L) _stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Pendulum333.Migawka", e); }
            lock (L) TickIn(m, now, step);
        }

        private static HashSet<Formation> PlayerSnap(Mission m)
        {
            var hs = new HashSet<Formation>();
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team t;
                try { t = teams[i]; } catch { continue; }
                if (!MissionTeams.Valid(t)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var f = fl[j];
                    if (f != null && HorsePursuit.PlayerForm(f)) hs.Add(f);
                }
            }
            return hs;
        }

        private static void TickIn(Mission m, float now, float step)
        {
            // 344: linie i potkniecia z watku AI
            if (PendLines.Count > 0)
            {
                for (int i = 0; i < PendLines.Count; i++) Log.Info(PendLines[i]);
                PendLines.Clear();
            }
            if (_pendErr != null)
            {
                var pe = _pendErr;
                string pw = _pendWhere ?? "Pendulum333";
                _pendErr = null;
                _pendWhere = null;
                RoutHold.Stumble(RoutHold.SiteDoctrine, pw, pe);
            }
            try
            {
                if (!m.IsDeploymentFinished) return;
                if (_depAt < 0f) _depAt = now;
                bool ff = false;
                try { ff = m.IsFastForward; } catch { ff = false; }
                if (ff) _ffSec += step; else _rtSec += step;
                var teams = m.Teams;
                int nt = teams != null ? teams.Count : 0;
                for (int i = 0; i < nt; i++)
                {
                    Team t;
                    try { t = teams[i]; } catch { continue; }
                    if (!MissionTeams.Valid(t)) continue;
                    var fl = t.FormationsIncludingSpecialAndEmpty;
                    int nf = fl != null ? fl.Count : 0;
                    for (int j = 0; j < nf; j++)
                    {
                        var f = fl[j];
                        if (!Eligible(f)) continue;
                        Sample(Get(f), now, step);
                    }
                }
            }
            catch (Exception e)
            {
                _stum++;
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Pendulum333.Pomiar", e);
            }
        }

        private static void Sample(PF pf, float now, float step)
        {
            Refresh(pf, now);
            var f = pf.F;
            // 347b B344-1: gra przenosi ludzi miedzy szykami (TransferUnits) - srodek ciezkosci i mediana skacza o dziesiatki metrow; zygzak od nowa jak przy kontakcie
            int cnt = f.CountOfUnits;
            bool comp = pf.Cnt > 0 && Math.Abs(cnt - pf.Cnt) >= Math.Max(HorsePursuit.IntactMin, 0.2f * Math.Max(cnt, pf.Cnt));
            pf.Cnt = cnt;
            if (comp)
            {
                pf.CompAt = now;
                _comp++;
                pf.Last = Vec2.Invalid;
                pf.LastA = Vec2.Invalid;
                pf.Trend = 0;
                pf.TrendA = 0;
            }
            Vec2 c = f.CachedMedianPosition.AsVec2;
            Vec2 a = f.CachedAveragePosition;
            if (pf.Contact || pf.Gap == float.MaxValue || !pf.Axis.IsValid || !c.IsValid || !a.IsValid)
            {
                pf.Last = Vec2.Invalid;
                pf.LastA = Vec2.Invalid;
                pf.Trend = 0;
                pf.TrendA = 0;
                if (pf.Contact) Forget(pf, !pf.FootContact);
                pf.StillPos = Vec2.Invalid;
                return;
            }
            Poll(pf, now);   // 333d: FacingOrder / ArrangementOrder przed kontaktem
            StillCheck(pf, now);
            if (pf.P.IsValid && pf.ArrAt < pf.PAt && Cur(f).Distance(pf.P) < Math.Max(DeadMove, f.Depth)) pf.ArrAt = now;   // 333e: dojscie do P (333f: pozycja szyku)
            _preSec += step;
            if (!pf.Last.IsValid || !pf.LastA.IsValid)
            {
                pf.Last = c;
                pf.LastA = a;
                pf.X = pf.Lo = pf.Hi = pf.Ext = 0f;
                pf.Trend = 0;
                pf.XA = pf.LoA = pf.HiA = pf.ExtA = 0f;
                pf.TrendA = 0;
                pf.XALo = pf.XAHi = 0f;
                pf.LoAt = pf.HiAt = now;
                Snap(pf, c, now);
                pf.ExtPrev = 0f; pf.XAPrevExt = 0f; pf.ExtPrevAt = now;
                return;
            }
            pf.X += (c - pf.Last).DotProduct(pf.Axis);
            pf.Last = c;
            pf.XA += (a - pf.LastA).DotProduct(pf.Axis);
            pf.LastA = a;
            float thr = Math.Max(DeadMin, 0.5f * f.Depth);
            ZigA(pf, thr, now);
            float x = pf.X;
            if (pf.Trend == 0)
            {
                if (x < pf.Lo) { pf.Lo = x; pf.XALo = pf.XA; pf.LoAt = now; }
                if (x > pf.Hi) { pf.Hi = x; pf.XAHi = pf.XA; pf.HiAt = now; }
                if (x - pf.Lo >= thr) { pf.Trend = 1; pf.ExtPrev = pf.Lo; pf.XAPrevExt = pf.XALo; pf.ExtPrevAt = pf.LoAt; pf.Ext = x; Snap(pf, c, now); }
                else if (pf.Hi - x >= thr) { pf.Trend = -1; pf.ExtPrev = pf.Hi; pf.XAPrevExt = pf.XAHi; pf.ExtPrevAt = pf.HiAt; pf.Ext = x; Snap(pf, c, now); }
                return;
            }
            if (pf.Trend > 0)
            {
                if (x > pf.Ext) { pf.Ext = x; Snap(pf, c, now); }
                else if (pf.Ext - x >= thr) { Reversal(pf, true, x, thr, c, a, now); pf.Trend = -1; Turn(pf, x, c, now); }
            }
            else
            {
                if (x < pf.Ext) { pf.Ext = x; Snap(pf, c, now); }
                else if (x - pf.Ext >= thr) { Reversal(pf, false, x, thr, c, a, now); pf.Trend = 1; Turn(pf, x, c, now); }
            }
        }

        /// <summary>344r (recenzja W6): szyk przed kontaktem stoi (pozycja w martwej strefie) >= Still s - licznik szykow; "utkniety" = cel gry naprzod >= martwa strefa
        /// (ostatni rozkaz w 5 s) przy stojacym szyku - warunek wgrania obok A344-0.</summary>
        private static void StillCheck(PF pf, float now)
        {
            var f = pf.F;
            Vec2 cp = Cur(f);
            if (!cp.IsValid) return;
            float dz = Math.Max(DeadMove, f.Depth);
            if (!pf.StillPos.IsValid || cp.Distance(pf.StillPos) >= dz) { pf.StillPos = cp; pf.StillAt = now; pf.StillN = pf.StuckN = false; return; }
            if (now - pf.StillAt < Still) return;
            if (!pf.StillN) { pf.StillN = true; _still++; }
            if (!pf.StuckN && _key && pf.T.IsValid && now - pf.TAt < 5f && (pf.T - cp).DotProduct(pf.Axis) >= dz)
            {
                pf.StuckN = true;
                _stuck++;
                // 347d B344-6c: kto stoi i dlaczego (StillCheck nie mowil, ktory szyk) - tylko linia
                if (_stuckLog < MaxDiagLines)
                {
                    _stuckLog++;
                    Line("AI bitwy (333) stoi z celem naprzod: " + S1(now) + " s | szyk " + Who(f) + " | zachowanie " + BehName(f) + " | ostatnia regula " + pf.AccRule
                         + (pf.AccSg > 0 ? " naprzod " : pf.AccSg < 0 ? " wstecz " : " w bok ") + (pf.AccAt > -999f ? S1(now - pf.AccAt) : "-") + " s temu | kierunek "
                         + pf.Dir + " od " + (pf.DirAt > -999f ? S1(now - pf.DirAt) : "-") + " s, okno " + F0(Win(pf)) + " s, nawrotow " + pf.Revs + " | nastepca "
                         + (pf.Succ ? "tak" : "nie") + ", nastepca trzymany: " + (pf.SuccHeld ? "tak" : "nie") + ", prowadzacy stoi "
                         + (pf.SuccHeld && pf.SuccLp != null && pf.SuccLp.StillPos.IsValid ? S1(now - pf.SuccLp.StillAt) : "-") + " s | cel gry - P na osi " + (pf.P.IsValid ? S1((pf.T - pf.P).DotProduct(pf.Axis)) : "-") + " m, cel gry - szyk "
                         + S1((pf.T - cp).DotProduct(pf.Axis)) + " m, szyk - P " + (pf.P.IsValid ? S1((cp - pf.P).DotProduct(pf.Axis)) : "-") + " m (martwa strefa "
                         + S1(dz) + " m) | od wroga " + F0(pf.Gap) + " m.");
                }
            }
        }

        /// <summary>333d: stan szyku w chwili ekstremum mediany (do porownania przy nawrocie).</summary>
        private static void Snap(PF pf, Vec2 c, float now)
        {
            var f = pf.F;
            pf.ExtAt = now;
            pf.XAExt = pf.XA;
            pf.DirExt = f.Direction;
            pf.PExt = pf.P;
            pf.CExt = c;
            pf.DepthExt = f.Depth;
            pf.WidthExt = f.Width;
            pf.AccNExt = pf.AccN;
            pf.FacExt = pf.Fac;
            pf.ArrExt = pf.Arr;
        }

        private static void Turn(PF pf, float x, Vec2 c, float now)
        {
            pf.ExtPrev = pf.Ext;
            pf.XAPrevExt = pf.XAExt;
            pf.ExtPrevAt = pf.ExtAt;
            pf.Ext = x;
            Snap(pf, c, now);
        }

        /// <summary>333d: ten sam zygzak dla srodka ciezkosci (srednia pozycji ludzi) - do porownania z mediana, bez wplywu na miare.</summary>
        private static void ZigA(PF pf, float thr, float now)
        {
            float x = pf.XA;
            if (pf.TrendA == 0)
            {
                if (x < pf.LoA) pf.LoA = x;
                if (x > pf.HiA) pf.HiA = x;
                if (x - pf.LoA >= thr) { pf.TrendA = 1; pf.ExtA = x; }
                else if (pf.HiA - x >= thr) { pf.TrendA = -1; pf.ExtA = x; }
                return;
            }
            if (pf.TrendA > 0)
            {
                if (x > pf.ExtA) pf.ExtA = x;
                else if (pf.ExtA - x >= thr)
                {
                    _revA++;
                    _revABack++;
                    // 347d B344-1b: nawrot srodka ciezkosci w tyl nie mial linii (Reversal tylko przy medianie) - kto cofnal szyk
                    if (_backALog < MaxDiagLines)
                    {
                        _backALog++;
                        var f = pf.F;
                        string cur;
                        try { cur = f.GetReadonlyMovementOrderReference().OrderEnum.ToString(); } catch { cur = "?"; }
                        Line("AI bitwy (333) cofniecie srodka ciezkosci: " + S1(now) + " s | szyk " + Who(f) + " | zachowanie " + BehName(f) + " | powrot "
                             + S1(pf.ExtA - x) + " m (prog " + S1(thr) + " m) | ostatnia regula " + pf.AccRule
                             + (pf.AccSg > 0 ? " naprzod " : pf.AccSg < 0 ? " wstecz " : " w bok ") + (pf.AccAt > -999f ? S1(now - pf.AccAt) : "-") + " s temu | rozkaz ostatni "
                             + pf.Ord + " " + (pf.OrdAt > -999f ? S1(now - pf.OrdAt) : "-") + " s temu, teraz " + cur + " | cel gry - P na osi "
                             + (pf.T.IsValid && pf.P.IsValid ? S1((pf.T - pf.P).DotProduct(pf.Axis)) : "-") + " m | kierunek " + pf.Dir + " | od wyjscia z kontaktu "
                             + (pf.OutAt > -999f ? S1(now - pf.OutAt) : "-") + " s, od wroga " + F0(pf.Gap) + " m.");
                    }
                    pf.TrendA = -1;
                    pf.ExtA = x;
                }
            }
            else
            {
                if (x < pf.ExtA) pf.ExtA = x;
                else if (x - pf.ExtA >= thr) { _revA++; pf.TrendA = 1; pf.ExtA = x; }
            }
        }

        private static string BehName(Formation f)
        {
            try { var ai = f.AI; return ai != null && ai.ActiveBehavior != null ? ai.ActiveBehavior.GetType().Name : "brak"; } catch { return "?"; }
        }

        private static string Who(Formation f)
        {
            var q = f.QuerySystem;
            var t = f.Team;
            return f.FormationIndex + " (" + f.CountOfUnits + " ludzi, " + (q != null && q.IsRangedFormation ? "lucznicy" : "piechota") + ", strona "
                + (t != null ? t.Side.ToString() : "?") + ", " + (t != null && (t.IsPlayerTeam || t.IsPlayerAlly) ? "z graczem" : "wrog gracza") + ")";
        }

        private static string FacName(int v) { return v < 0 ? "-" : ((FacingOrder.FacingOrderEnum)v).ToString(); }
        private static string ArrName(int v) { return v < 0 ? "-" : ((ArrangementOrder.ArrangementOrderEnum)v).ToString(); }
        private static string S1(float v) { return v.ToString("0.0", Inv); }
        private static string F0(float v) { return v.ToString("0", Inv); }

        /// <summary>Kat miedzy kierunkami w stopniach (0-180).</summary>
        private static float Ang(Vec2 a, Vec2 b)
        {
            if (!a.IsValid || !b.IsValid || a.Length < 1e-3f || b.Length < 1e-3f) return 0f;
            double d = Math.Atan2(b.y, b.x) - Math.Atan2(a.y, a.x);
            while (d > Math.PI) d -= 2.0 * Math.PI;
            while (d < -Math.PI) d += 2.0 * Math.PI;
            return (float)(Math.Abs(d) * 180.0 / Math.PI);
        }

        /// <summary>333d: FacingOrder / ArrangementOrder przed kontaktem - poczatek i kazda zmiana (linie najwyzej MaxSetLines na misje).</summary>
        private static void Poll(PF pf, float now)
        {
            var f = pf.F;
            int fac = (int)f.FacingOrder.OrderEnum, arr = (int)f.ArrangementOrder.OrderEnum;
            if (pf.Fac < 0)
            {
                pf.Fac = fac;
                pf.Arr = arr;
                pf.ArrObsCnt = f.CountOfUnits;
                SetLine(pf, now, "poczatek FacingOrder " + FacName(fac) + ", ArrangementOrder " + ArrName(arr));
                return;
            }
            if (fac != pf.Fac) { _facChg++; SetLine(pf, now, "FacingOrder " + FacName(pf.Fac) + " -> " + FacName(fac)); pf.Fac = fac; }
            if (arr != pf.Arr)
            {
                _arrChg++;
                // 344 B344-3: powrot Line / Loose do ustawienia sprzed poprzedniej zmiany w ArrHold s
                bool ll = (arr == (int)ArrangementOrder.ArrangementOrderEnum.Line || arr == (int)ArrangementOrder.ArrangementOrderEnum.Loose)
                          && (pf.Arr == (int)ArrangementOrder.ArrangementOrderEnum.Line || pf.Arr == (int)ArrangementOrder.ArrangementOrderEnum.Loose);
                if (ll && arr == pf.ArrObsPrev && now - pf.ArrObsAt < ArrHold)
                {
                    // 347b B344-3: wejscie w Loose pod ostrzalem (W4, przyjete zawsze) i nowa grupa ludzi w tym samym szyku (CopyOrdersFrom przy TransferUnits) - osobno
                    int oc = f.CountOfUnits;
                    bool compObs = pf.CompAt > pf.ArrObsAt
                                   || (pf.ArrObsCnt > 0 && Math.Abs(oc - pf.ArrObsCnt) >= Math.Max(HorsePursuit.IntactMin, 0.5f * Math.Max(oc, pf.ArrObsCnt)));
                    if (arr == (int)ArrangementOrder.ArrangementOrderEnum.Loose && now - pf.ArrFireAccAt <= SampleEvery + RefreshEvery) _arrRetFire++;
                    else if (compObs) _arrRetComp++;
                    else _arrRet++;
                }
                pf.ArrObsPrev = pf.Arr;
                pf.ArrObsAt = now;
                pf.ArrObsCnt = f.CountOfUnits;
                SetLine(pf, now, "ArrangementOrder " + ArrName(pf.Arr) + " -> " + ArrName(arr));
                pf.Arr = arr;
            }
        }

        private static void SetLine(PF pf, float now, string what)
        {
            if (_setLog >= MaxSetLines) return;
            _setLog++;
            var f = pf.F;
            Log.Info("AI bitwy (333) ustawienie przed kontaktem: " + S1(now) + " s | szyk " + Who(f) + " | zachowanie " + BehName(f) + " | " + what
                + " | od wroga " + F0(pf.Gap) + " m, kierunek szyku do osi wroga " + F0(Ang(f.Direction, pf.Axis)) + " st, glebokosc " + S1(f.Depth) + " m, szerokosc "
                + S1(f.Width) + " m.");
        }

        /// <summary>333d: rozkaz inny niz Move przed kontaktem (latka go nie pilnuje) - licznik wedlug rodzaju, linia przy zmianie na nie-Move.</summary>
        private static void NonMove(PF pf, MovementOrder.MovementOrderEnum oe, MovementOrder.MovementOrderEnum prev, float now)
        {
            string k = oe.ToString();
            int n;
            NonMoveBy.TryGetValue(k, out n);
            if (n == 0 && NonMoveBy.Count >= MaxBeh) k = "inne";
            NonMoveBy.TryGetValue(k, out n);
            NonMoveBy[k] = n + 1;
            if (oe == prev) return;
            _ordChg++;
            if (_ordLog >= MaxSetLines) return;
            _ordLog++;
            var f = pf.F;
            Line("AI bitwy (333) rozkaz przed kontaktem: " + S1(now) + " s | szyk " + Who(f) + " | zachowanie " + BehName(f) + " | " + prev + " -> " + oe
                + " | od wroga " + F0(pf.Gap) + " m, P " + (pf.P.IsValid ? "jest" : "brak") + ", s od wyjscia z kontaktu " + (pf.OutAt > -999f ? S1(now - pf.OutAt) : "-") + ".");
        }

        private static void Reversal(PF pf, bool back, float x, float thr, Vec2 c, Vec2 a, float now)
        {
            var f = pf.F;
            _rev++;
            if (back) _revBack++;
            string n = BehName(f);
            int k;
            RevBy.TryGetValue(n, out k);
            if (k == 0 && RevBy.Count >= MaxBeh) n = "inne";
            RevBy.TryGetValue(n, out k);
            RevBy[n] = k + 1;
            // 333d: droga nawrotu
            try
            {
                float legM = pf.Ext - pf.ExtPrev, retM = x - pf.Ext, legA = pf.XAExt - pf.XAPrevExt, retA = pf.XA - pf.XAExt;
                if (Math.Abs(legM) < 2f * thr) _revSmall++;
                if (legA * legM > 0f && Math.Abs(legA) >= 0.5f * thr && retA * retM > 0f && Math.Abs(retA) >= 0.5f * thr) { _revBoth++; if (back) _revBothBack++; }
                bool medArt = Math.Abs(retA) < 0.5f * thr || retA * retM < 0f;   // srodek ciezkosci nie wrocil (albo poszedl dalej)
                int dP = pf.AccN - pf.AccNExt;
                float dDir = Ang(pf.DirExt, f.Direction);
                float dDepth = f.Depth - pf.DepthExt, dWidth = f.Width - pf.WidthExt;
                var cur = f.GetReadonlyMovementOrderReference().OrderEnum;
                Vec2 ax = pf.Axis;
                float offExt = pf.PExt.IsValid && pf.CExt.IsValid ? (pf.CExt - pf.PExt).DotProduct(ax) : 0f;
                string way;
                if (medArt) way = "artefakt mediany";
                else if (dP > 0) way = "nowy P regula " + pf.AccRule;
                else if (cur != MovementOrder.MovementOrderEnum.Move) way = "rozkaz " + cur;
                else if (pf.OrdAt >= pf.ExtPrevAt && pf.Ord != MovementOrder.MovementOrderEnum.Move) way = "rozkaz " + pf.Ord;
                else if (pf.OutAt >= pf.ExtPrevAt) way = "po kontakcie";
                else if (!pf.P.IsValid) way = "bez P";
                else if (pf.PExt.IsValid && offExt * legM > 0f && Math.Abs(offExt) >= 0.5f * thr) way = "przestrzelenie P";
                else if (dDir > 20f || Math.Abs(dDepth) > Math.Max(2f, 0.3f * pf.DepthExt) || Math.Abs(dWidth) > Math.Max(3f, 0.3f * pf.WidthExt)) way = "obrot / przestawienie szyku";
                else way = "P bez zmian";
                int[] w;
                if (!RevWay.TryGetValue(way, out w))
                {
                    if (RevWay.Count >= MaxWays) way = "inne";
                    if (!RevWay.TryGetValue(way, out w)) { w = new int[2]; RevWay[way] = w; }
                }
                w[0]++;
                if (back) w[1]++;
                if (_revLog >= MaxRevLines) return;
                _revLog++;
                var sb = new StringBuilder(640);
                sb.Append("AI bitwy (333) nawrot: ").Append(S1(now)).Append(" s | szyk ").Append(Who(f)).Append(" | zachowanie ").Append(n)
                  .Append(" | ").Append(back ? "w tyl" : "naprzod").Append(", droga ").Append(way)
                  .Append(" | mediana: odcinek ").Append(S1(legM)).Append(" m w ").Append(S1(pf.ExtAt - pf.ExtPrevAt)).Append(" s, powrot ").Append(S1(retM))
                  .Append(" m w ").Append(S1(now - pf.ExtAt)).Append(" s (prog ").Append(S1(thr)).Append(" m)")
                  .Append(" | srodek ciezkosci: odcinek ").Append(S1(legA)).Append(" m, powrot ").Append(S1(retA)).Append(" m, mediana - srodek ").Append(S1(c.Distance(a))).Append(" m")
                  .Append(" | P: ");
                if (pf.P.IsValid)
                    sb.Append("zmian od ekstremum ").Append(dP).Append(", przyjec razem ").Append(pf.AccN).Append(", ostatnia regula ").Append(pf.AccRule)
                      .Append(pf.AccSg > 0 ? " naprzod" : pf.AccSg < 0 ? " wstecz" : " w bok").Append(' ').Append(S1(now - pf.AccAt)).Append(" s temu, mediana - P na osi teraz ")
                      .Append(S1((c - pf.P).DotProduct(ax))).Append(" / w ekstremum ").Append(S1(offExt)).Append(" m");
                else sb.Append("brak");
                sb.Append("; cel gry - P na osi ")
                  .Append(pf.T.IsValid && pf.P.IsValid ? S1((pf.T - pf.P).DotProduct(ax)) : "-").Append(" m (").Append(pf.TAt > -999f ? S1(now - pf.TAt) : "-").Append(" s temu)")
                  .Append(" | rozkaz ostatni ").Append(pf.Ord).Append(' ').Append(pf.OrdAt > -999f ? S1(now - pf.OrdAt) : "-").Append(" s temu, teraz ").Append(cur)
                  .Append(", pozycja rozkazu - mediana na osi ").Append(S1((f.OrderPosition - c).DotProduct(ax))).Append(" m")
                  .Append(" | od wyjscia z kontaktu ").Append(pf.OutAt > -999f ? S1(now - pf.OutAt) : "-").Append(" s, od wroga ").Append(F0(pf.Gap)).Append(" m")
                  .Append(" | kierunek szyku od ekstremum ").Append(F0(dDir)).Append(" st (do osi wroga ").Append(F0(Ang(f.Direction, ax))).Append(" st), FacingOrder ")
                  .Append(FacName(pf.Fac)).Append(pf.FacExt != pf.Fac ? " (w ekstremum " + FacName(pf.FacExt) + ")" : "").Append(", ArrangementOrder ").Append(ArrName(pf.Arr))
                  .Append(pf.ArrExt != pf.Arr ? " (w ekstremum " + ArrName(pf.ArrExt) + ")" : "")
                  .Append(" | glebokosc ").Append(S1(pf.DepthExt)).Append(" -> ").Append(S1(f.Depth)).Append(" m, szerokosc ").Append(S1(pf.WidthExt)).Append(" -> ")
                  .Append(S1(f.Width)).Append(" m.");
                Log.Info(sb.ToString());
            }
            catch (Exception e)
            {
                _stum++;
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Pendulum333.Nawrot", e);
            }
        }

        // ------------------------------------------------------------ segment linii "AI bitwy (333)"

        internal static void Append(StringBuilder sb)
        {
            lock (L) AppendIn(sb);
        }

        /// <summary>344: segment arbitra (A344-0, B344-1..5) przed segmentem 333 (diagnostyka bez zmian).</summary>
        private static void AppendIn(StringBuilder sb)
        {
            float min = _preSec / 60f;
            sb.Append("szyki: klucz ").Append(_key ? "TAK" : "NIE").Append(" (latka ruchu ").Append(_patch).Append(", latka ustawienia ").Append(_patchArr)
              .Append("; histereza zachowan ").Append(BehHold.ToString("0", Inv)).Append(" s, blokada przeciwnego rozkazu ").Append(Block.ToString("0", Inv))
              .Append(" s, ustawienie Line/Loose co ").Append(ArrHold.ToString("0", Inv)).Append(" s, powrot po ").Append(ArrHold.ToString("0", Inv)).Append(" s bez ostrzalu)")
              .Append(" | wywolania arbitra glowny watek ").Append(_callMain).Append(" / watek AI ").Append(_callAi).Append(", ustawienia glowny ").Append(_arrMain)
              .Append(" / AI ").Append(_arrAi).Append(", czas misji z przewijaniem ").Append(_ffSec.ToString("0", Inv)).Append(" s / bez ").Append(_rtSec.ToString("0", Inv)).Append(" s")
              .Append(" | szyko-minut przed kontaktem ").Append(min.ToString("0.0", Inv)).Append(", nawroty srodka ciezkosci ").Append(_revA).Append(" (w tyl ").Append(_revABack)
              .Append("), mediany ").Append(_rev).Append(" (w tyl ").Append(_revBack).Append("), zmian skladu szyku ").Append(_comp)
              .Append(" | rozkazy Move przyjete/trzymane/obciete wedlug zrodla ");
            if (SrcBy.Count == 0) sb.Append('-');
            else
            {
                bool f1 = true;
                foreach (var kv in SrcBy)
                {
                    if (!f1) sb.Append(", ");
                    f1 = false;
                    sb.Append(kv.Key).Append(' ').Append(kv.Value[0]).Append('/').Append(kv.Value[1]).Append('/').Append(kv.Value[2]);
                }
            }
            sb.Append(" | ustawienie Line/Loose: wywolan ").Append(_arrSeen).Append(", przyjete ").Append(_arrAcc).Append(", zablokowane ").Append(_arrBlk)
              .Append(", powroty pod ostrzalem zablokowane ").Append(_arrBack).Append(", w kontakcie zablokowane ").Append(_arrBlkC).Append(", szarza konnicy ").Append(_arrCav)
              .Append(", zmian ustawienia przed kontaktem ").Append(_arrChg).Append(", powroty w ").Append(ArrHold.ToString("0", Inv)).Append(" s ").Append(_arrRet)
              .Append(" (osobno: z wejscia pod ostrzalem ").Append(_arrRetFire).Append(", przy zmianie skladu ").Append(_arrRetComp).Append(')')
              .Append(" | zachowania: zmian planu ").Append(_plan).Append(", migniec ").Append(_blink).Append(", zmian taktyki ").Append(_tac)
              .Append(" | nastepcy: trzymani ").Append(_succKeep).Append(", ruszeni za prowadzacym ").Append(_succAcc).Append(", ruchy bez zmiany prowadzacego ").Append(_succNo)
              .Append(" | blokada przeciwnego rozkazu ").Append(_blk)
              .Append(" | 344r: krok naprzod w martwej strefie ").Append(_fwdStep).Append(", pierwszy kontakt po ").Append(_contact1 >= 0f ? _contact1.ToString("0", Inv) : "-")
              .Append(" s, szykow stojacych ponad ").Append(Still.ToString("0", Inv)).Append(" s przed kontaktem ").Append(_still).Append(" (z celem gry naprzod ").Append(_stuck)
              .Append("), Loose pod ostrzalem od razu ").Append(_arrFireAcc).Append(", migniec taktyki ").Append(_tacBlink).Append(", taktyki druzyn ");
            if (TacBy.Count == 0) sb.Append('-');
            else
            {
                bool ft = true;
                int ti = 0;
                foreach (var kv in TacBy)
                {
                    if (!ft) sb.Append(", ");
                    ft = false;
                    sb.Append('d').Append(ti++).Append(' ').Append(kv.Value.N).Append(" (szybkich ").Append(kv.Value.Fast).Append(')');
                }
            }
            sb.Append(" | watek AI: wywolan ").Append(_aiN).Append(", czekanie na lock ").Append(Ms(_aiWait)).Append(" ms (najwiecej ").Append(Ms(_aiWaitMax))
              .Append(" ms), praca ").Append(Ms(_aiWork)).Append(" ms (najwiecej ").Append(Ms(_aiWorkMax)).Append(" ms), zgubione linie ").Append(_lost)
              .Append(" | 333: ");
            sb.Append("wahadlo: klucz ").Append(_key ? "TAK" : "NIE").Append(" (latka ").Append(_patch).Append("; przed kontaktem > ").Append(ContactIn.ToString("0", Inv))
              .Append(" m miara RBM, wyjscie > ").Append(ContactOut.ToString("0", Inv)).Append(" m; 333b: punkt przyjety, martwa strefa rozkazu max(")
              .Append(DeadMove.ToString("0", Inv)).Append(" m, glebokosc), zapadka kierunku ").Append(Ratchet.ToString("0", Inv)).Append('/')
              .Append((2f * Ratchet).ToString("0", Inv)).Append('/').Append((4f * Ratchet).ToString("0", Inv)).Append('/').Append(RatchetMax.ToString("0", Inv))
              .Append(" s, trzymanie ").Append(HoldMin.ToString("0", Inv)).Append(" s; pomiar martwa strefa max(").Append(DeadMin.ToString("0", Inv))
              .Append(" m, pol glebokosci); konnica wroga ").Append(CavClose.ToString("0", Inv))
              .Append(" m albo szarza w ").Append(CavNear.ToString("0", Inv)).Append(" m = kontakt)")
              .Append(" | szykow AI ").Append(_forms).Append(", szyko-minut przed kontaktem ").Append(min.ToString("0.0", Inv))
              .Append(", nawroty ").Append(_rev).Append(" (w tym w tyl ").Append(_revBack).Append("; ").Append(min > 0.01f ? (_rev / min).ToString("0.00", Inv) : "-")
              .Append(" na szyko-minute; wedlug zachowania ");
            if (RevBy.Count == 0) sb.Append('-');
            else
            {
                bool first = true;
                foreach (var kv in RevBy)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(kv.Key).Append(' ').Append(kv.Value);
                }
            }
            sb.Append("), wejsc w kontakt ").Append(_contactIn).Append(" (w tym przez konnice wroga ").Append(_contactCav).Append(')');
            // 333d: diagnostyka (bez '|' w srodku - wyrazenie A333 w sprawdz_logi)
            sb.Append("; 333d: nawroty srodka ciezkosci ").Append(_revA).Append(" (w tyl ").Append(_revABack).Append("), nawroty razem ze srodkiem ciezkosci ").Append(_revBoth)
              .Append(" (w tyl ").Append(_revBothBack).Append("), nawrotow z odcinkiem < 2 progi ").Append(_revSmall)
              .Append(", drogi ");
            if (RevWay.Count == 0) sb.Append('-');
            else
            {
                bool fw = true;
                foreach (var kv in RevWay)
                {
                    if (!fw) sb.Append(", ");
                    fw = false;
                    sb.Append(kv.Key).Append(' ').Append(kv.Value[0]).Append(" (w tyl ").Append(kv.Value[1]).Append(')');
                }
            }
            sb.Append(", rozkazy nie-Move przed kontaktem ");
            if (NonMoveBy.Count == 0) sb.Append('-');
            else
            {
                bool fn = true;
                foreach (var kv in NonMoveBy)
                {
                    if (!fn) sb.Append(", ");
                    fn = false;
                    sb.Append(kv.Key).Append(' ').Append(kv.Value);
                }
            }
            sb.Append(" (zmian na nie-Move ").Append(_ordChg).Append("), zmiany FacingOrder ").Append(_facChg).Append(", ArrangementOrder ").Append(_arrChg)
              .Append(" | rozkazy Move przed kontaktem ").Append(_seen).Append(": wstecz zatrzymane ").Append(_backHeld).Append(", wstecz dozwolone S4 ").Append(_backS4)
              .Append(", wstecz po zmianie zachowania ").Append(_backBeh).Append(", naprzod wstrzymane po cofnieciu ").Append(_fwdHeld).Append(", szykow zatrzymanych ")
              .Append(HeldForms.Count)
              .Append("; 333b: pierwszych ").Append(_first).Append(", martwa strefa ").Append(_dead).Append(", trzymanie ").Append(_hold)
              .Append(", nawroty przepuszczone po oknie ").Append(_revOk).Append(", przyjete naprzod ").Append(_accFwd).Append(", w bok ").Append(_accSide)
              .Append("; 333e: obciete do ruchu w bok ").Append(_clip).Append(", nawroty po oknie za male ").Append(_zSmall)
              .Append("; 333f: zapadka od pozycji szyku, P przesuniete po minieciu ").Append(_over).Append(", cele obciete o mniej niz ").Append(DeadMin.ToString("0", Inv))
              .Append(" m za szykiem ").Append(_clipSmall);
        }
    }
}
