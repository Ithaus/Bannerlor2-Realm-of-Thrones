using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// p6-drogi (R3-07d, POMYSLY-35 pkt 20, Jeff 20.10 "pozostale tak"): AI WYBIERA DROGI. Audyt drog 09.10 (docs/audyt-2026-10-09/08-drogi-wioski-widok.md 1.4,
    /// 3.4, tabela 4.3 M-4): trasa partii = najkrotsza po scianach siatki gry, drog nie zna - na trakcie lezy ok. 5% punktow tras AI (9% przy szerszym polu,
    /// MapRoads.cs). Premia drogi (+15%) dziala dla AI tylko przypadkiem.
    ///
    /// JAK GRA LICZY TRASE (dekompilacja 1.4.8): MobileParty.DoAIMove -> IsComputePathCacheDirty (cel zmienil sciane siatki albo pozycje, inny rodzaj nawigacji,
    /// koniec trasy) -> prywatna MobileParty.ComputePath(cel, rodzaj, cacheNextLongTermPathPoint) -> IMapScene.GetPathBetweenAIFaces (silnik, A* po scianach,
    /// sciany zakazane = PartyNavigationModel.GetInvalidTerrainTypesForNavigationType) -> NavigationPath (stale 128 punktow). Partia idzie potem po prostych
    /// od punktu do punktu (DoAiPathMode) - ruch sprawdza tylko wyspe sciany (TryToMoveThePartyWithCurrentTickMoveData), nie rodzaj terenu. ComputePath wola
    /// watek roboczy gry (ruch partii rownolegle, CampaignTickCacheDataStore.RealTick -> TWParallel.For), raz na nowy cel - nie co klatke.
    /// KOSZT SCIAN DROGI - NIE: drogi ROT to farba terenu, nie sciany siatki (sciana nie wie, ze lezy na drodze; sciany sa duze, droga ma ok. 0.5 jedn.),
    /// a koszt scian liczy silnik (jedyne pokretla: lista scian zakazanych i extraCostMultiplier dla calej trasy). Tanszej drogi po siatce nie da sie dac
    /// bez zmiany siatki mapy ROT - wiec PUNKTY POSREDNIE.
    ///
    /// ZMIANA: postfiks MobileParty.ComputePath (DeclaredMethod, prywatna). Gdy gra wyznaczyla trase (wynik true) partii, ktorej to dotyczy:
    ///  - lord (takze wodz armii; czlonkowie armii nie licza tras), karawana, woz wsi; nie gracz, nie bandy, nie patrole, nie milicja;
    ///  - marsz LADEM (rodzaj Default, partia na ladzie, bez przejscia lad-morze i tratwy), ruch do punktu (MoveModeType.Point - nie za partia), AI czynne
    ///    albo wylaczone przez inny mod przy rozkazie do osady / punktu (uczta BK: DisableAi + SetMoveGoToSettlement, majatek gentry BK: DisableForHours(72);
    ///    recenzja p6-drogi - to zwykle dlugie marsze pokojowe; objazd zmienia tylko punkty trasy, nie cel);
    ///  - zachowanie: GoToSettlement / GoToPoint / PatrolAroundPoint (krotkoterminowe takze None); bez pogoni (EngageParty, GoAroundParty, EscortParty),
    ///    ucieczki (IsFleeing, a RecalculateLongTermPath z liczenia ucieczki - prefiks/finalizer), oblezen (BesiegeSettlement, AssaultSettlement, SiegeEvent,
    ///    BesiegedSettlement), najazdow (RaidSettlement), obrony (DefendSettlement) i bitwy (MapEvent);
    ///  - trasa gry co najmniej MapRoadAiMinDistance (30 jedn.) dlugosci.
    /// SIEC: plik drog ROT (ModuleData/arm_map_roads_geo.tsv, ten sam co premia drogi) -> graf raz na sesje (watek glowny): lamane uproszczone Douglas-Peucker
    /// 0.5 jedn. (cieciwa najwyzej 0.5 od osi drogi - w polu premii: pol-szerokosc >= 0.15 + margines 1.0 / 0.4), konce lamanych laczone z najblizszym
    /// wierzcholkiem innej lamanej do 0.6 jedn. (rozstaje), sciana siatki gry kazdego wezla policzona raz (wezel na scianie zakazanej / bez sciany = poza siecia).
    /// WYBOR: Dijkstra z wielu zrodel po wezlach w promieniu R (1/4 trasy gry, 3..40 jedn.) od startu; koszt = odcinek poza droga x 1 + droga x 1/(1 + premia),
    /// premia = czynna premia traktu (MapRoadSpeedPercent, jak MapRoads.PctOf; recenzja p6-drogi: nie klucz nadkladu); wyjscie w promieniu R od celu.
    /// Najtanszy wariant "start -> wejscie na trakt -> trakt -> zejscie -> cel" przyjety, gdy (po policzeniu trasy przez silnik):
    ///  - CZAS: koszt przejazdu objazdu <= koszt przejazdu trasy gry, OBA z probek co 1 jedn. ta sama miara co premia (MapRoads.KindAt: trakt 1/(1 + premia),
    ///    droga boczna 1/(1 + premia boczna), reszta 1) - trasa gry tez bywa czesciowo na drodze; teren (las, snieg) i bloto poza szacunkiem - czas
    ///    zmierzony osobno (dojazd do celu, nizej);
    ///  - droga (trakt z probek) >= polowa trasy, dlugosc <= trasa gry x (1 + MapRoadAiDetourPercent/100) - nadklad to tylko sufit dlugosci.
    /// Premia traktu 0 albo MapRoadSpeedBonus NIE = bez objazdow (droga nic nie daje).
    /// TRASA: odcinki liczy silnik tym samym wywolaniem co gra (GetPathBetweenAIFaces, promien 0.3, te same sciany zakazane, ten sam mnoznik 2 i koszty
    /// przejscia lad-morze): start -> wejscie, kazda cieciwa drogi, zejscie -> cel (w obrebie jednej sciany - prosta, sciana siatki jest wypukla). Odcinek
    /// bez sciezki, ponad 128 punktow albo dlugosc / udzial drogi po policzeniu poza progiem = trasa gry bez zmian. Przyjeta trasa zastepuje punkty
    /// NavigationPath (Size, PathPoints; PathBegin = 0 juz ustawil ComputePath), NextLongTermPathPoint jak w grze. Cel trasy bez zmian (ostatni punkt = cel).
    /// POWTORKA CELU: pamiec partii bez blokad (1 << 17 slotow long: Party.Index | skrot celu | stan, osobno godzina) - ten sam cel po ODRZUCONYM objezdzie
    /// liczony ponownie w tej samej godzinie = bez planowania (inna latka przesuwajaca cel co klatke nie zrobi z tego kosztu co klatke); po PRZYJETYM objezdzie
    /// planowanie od nowa (recenzja p6-drogi: gra nadpisala Path trasa gry, a bez zmiany celu nie przeliczy jej do konca marszu - np. powrot po krotkiej
    /// ucieczce albo przerwie); najwyzej PlansPerHour (3) plany partii na godzine, dalej trasa gry - wtedy stan slotu "odrzucony" (spis nie liczy partii
    /// jako objazdu, licznik "objazd utracony po limicie").
    /// DOJAZD ZMIERZONY (tylko log; recenzja p6-drogi: "czas szac." to ta sama formula co warunek przyjecia): przy nowym celu godzina planu, cel i czas
    /// szacowany trasy gry (dlugosc trasy gry / predkosc bazowa partii LastCalculatedBaseSpeed) - osobno partie z objazdem i proba porownawcza (trasa gry,
    /// choc droga byla w zasiegu: "droga sie nie oplaca", "malo drogi", "za dlugie"). Dojazd: partia do 2 jedn. od celu albo w osadzie celu (spis godzinny
    /// albo nastepny plan) - stosunek czasu zmierzonego do szacowanego, srednia osobno dla obu grup; nowy cel przed dojazdem = przerwany; ponad 10 dob albo
    /// ponad 5 x szacunek = poza pomiarem. Obie grupy maja te same noce, postoje i teren, wiec objazd "nie pozniej" = srednia objazdow <= srednia proby.
    /// WATKI: graf niezmienny po budowie (jedna referencja volatile); pamiec robocza [ThreadStatic] na watek (raz na watek), bez alokacji w planowaniu;
    /// liczniki Interlocked; potkniecia liczone (pierwsze zapamietane do linii), nigdy wylacznik funkcji.
    /// POMIAR (tylko log): linia "AI drogi (AD): start sesji" i "AI drogi (AD): dzien N" (liczone / przyjete / odrzucone z powodem, sredni nadklad, udzial drogi,
    /// spis partio-godzin w marszu - ta sama miara co MapRoads: MapRoads.KindAt == 2 (trakt) w punkcie partii - osobno partie na objezdzie, koszt ms/dobe). Nic w zapisie
    /// gry: trasa liczy sie od nowa po wczytaniu (ComputePathAfterLoad / nowy cel), stary zapis bez zmian; Reset przy nowej kampanii i przed wczytaniem.
    /// Klucze NOWE: MapRoadAiRoutes (TAK), MapRoadAiMinDistance (30), MapRoadAiDetourPercent (15). Wylaczony klucz albo MapRoadSpeedBonus = trasy gry.
    /// </summary>
    internal static class AiRoadRoutes
    {
        private const string P = "AI drogi (AD): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const float Simplify = 0.5f;      // tolerancja Douglas-Peucker (jedn.) - cieciwa w polu premii drogi
        private const float JoinR = 0.6f;         // rozstaje: koniec lamanej do wierzcholka innej lamanej
        private const float NodeCell = 4f;        // siatka wezlow do szukania wejscia / zejscia
        private const float RadiusShare = 0.25f, RadiusMin = 3f, RadiusMax = 40f;
        private const float MinRoadShare = 0.5f;  // droga co najmniej polowa trasy
        private const int PathCap = 128;          // NavigationPath.PathSize (dekompilacja 1.4.8)
        private const float AgentRadius = 0.3f;   // MobileParty.ComputePath na ladzie
        private const int SlotBits = 17, SlotMask = (1 << SlotBits) - 1;
        private const long StRejected = 1, StAccepted = 2;
        private const float SampleStep = 1f;      // probki drogi na trasie co 1 jedn. (pole traktu ma ok. 2.3 jedn. szerokosci)
        private const int MaxSamples = 600;       // dluzsza trasa - rzadsze probki
        private const int MaxPercent = 50;        // jak MapRoads (premia traktu i drogi bocznej przycieta do 0..50)
        private const float ArriveR = 2f;         // dojazd: partia do 2 jedn. od celu (albo w osadzie celu)
        private const float MaxMarchHours = 240f; // marsz ponad 10 dob - poza pomiarem
        private const float RatioCap = 5f;        // ponad 5 x czas szacowany - poza pomiarem (oblezenie, dlugi postoj po drodze)
        private const int RouteOk = 0, RouteCtl = 1, RouteOther = 2;   // wynik planu: objazd / odrzucony mimo drogi w zasiegu (proba) / inny powod
        private const byte GrpDet = 1, GrpCtl = 2;

        // ------------------------------------------------------------------ graf (niezmienny po budowie)
        private sealed class Graph
        {
            public int Nodes, Lines, Joins, BadNodes, Comps, Points, AdjCount;
            public int EdgesDirect, EdgesBent, EdgesCut;   // krawedzie: prosta po siatce gry / z objazdem silnika / bez sciezki (wyciete)
            public double Length;
            public float[] X, Y;
            public PathFaceRecord[] Face;
            public bool[] Ok;
            public int[] Island;
            public int[] AdjStart, Adj;
            public float[] AdjLen;     // dlugosc przejscia (prosta albo sciezka silnika); < 0 = krawedz bez sciezki (pomijana)
            public bool[] AdjDirect;   // prosta cieciwa przechodzi po siatce gry (sprawdzone raz przy budowie) - bez wywolania silnika w planowaniu
            public float GX0, GY0, GInv;
            public int GNx, GNy;
            public int[] GStart, GNode;
        }

        private sealed class Scratch
        {
            public float[] Cost;
            public int[] Prev, PrevA, Stamp, Chain, HeapN;
            public readonly float[] Cx = new float[4], Cy = new float[4];   // p6-drogi: strefy przepraw wrogiego pana (CrossingLaw) - bez objazdu przez nie
            public int Nc;
            public float Cr2;
            public float[] HeapK;
            public int Now, HeapCount, Size;
            public readonly Vec2[] Out = new Vec2[PathCap];
            public readonly NavigationPath Tmp = new NavigationPath();

            public void Ensure(Graph g)
            {
                int cap = g.Nodes + g.AdjCount + 8;
                if (Cost != null && Cost.Length >= g.Nodes && HeapN.Length >= cap) return;
                Cost = new float[g.Nodes]; Prev = new int[g.Nodes]; PrevA = new int[g.Nodes]; Stamp = new int[g.Nodes]; Chain = new int[g.Nodes + 1];
                HeapN = new int[cap]; HeapK = new float[cap]; Now = 0;
            }

            public void Begin()
            {
                if (Now == int.MaxValue) { Array.Clear(Stamp, 0, Stamp.Length); Now = 0; }
                Now++;
                HeapCount = 0;
                Size = 0;
            }

            public bool Push(int n, float k)
            {
                if (HeapCount >= HeapN.Length) return false;
                int i = HeapCount++;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (HeapK[p] <= k) break;
                    HeapN[i] = HeapN[p]; HeapK[i] = HeapK[p]; i = p;
                }
                HeapN[i] = n; HeapK[i] = k;
                return true;
            }

            public bool Pop(out int n, out float k)
            {
                if (HeapCount == 0) { n = -1; k = 0f; return false; }
                n = HeapN[0]; k = HeapK[0];
                int last = --HeapCount;
                if (last == 0) return true;
                int ln = HeapN[last]; float lk = HeapK[last];
                int i = 0;
                while (true)
                {
                    int c = 2 * i + 1;
                    if (c >= last) break;
                    if (c + 1 < last && HeapK[c + 1] < HeapK[c]) c++;
                    if (HeapK[c] >= lk) break;
                    HeapN[i] = HeapN[c]; HeapK[i] = HeapK[c]; i = c;
                }
                HeapN[i] = ln; HeapK[i] = lk;
                return true;
            }
        }

        private static volatile Graph _g;
        private static string _note = "nie zbudowana";
        private static bool _tried;
        private static bool _hooked, _hookedLong;
        private static string _hookNote = "nie wpinana";
        [ThreadStatic] private static Scratch _scr;
        [ThreadStatic] private static int _longTerm;   // RecalculateLongTermPath w toku (liczenie ucieczki) - bez objazdu

        // pamiec partii: Party.Index << 32 | skrot celu (bity 2..31) | stan (bity 0..1); godzina planu osobno (para nieatomowa - rozjazd = jedno planowanie wiecej/mniej)
        // przeprawy-warownie CrossingLaw (te same id: Twins, Moat Cailin, Bloody Gate) - straz odbija wrogie partie lordow; objazd lorda w wojnie z panem
        // przeprawy nie prowadzi przez strefe (CrossingRadius + 1 jedn.) - inaczej droga wiodlaby go pod most co 3 h (cel-odwrot, przerwa, znow objazd)
        private static readonly string[] CrossingIds = { "ROT_town3", "castle_B1", "castle_EN2" };
        private static TaleWorlds.CampaignSystem.Settlements.Settlement[] _cross = new TaleWorlds.CampaignSystem.Settlements.Settlement[0];
        private static Vec2[] _crossPos = new Vec2[0];
        private static long _crossAvoid;

        private static readonly long[] _slotKey = new long[1 << SlotBits];
        private static readonly int[] _slotHour = new int[1 << SlotBits];
        private static readonly int[] _slotCnt = new int[1 << SlotBits];   // planow tej partii w godzinie _slotHour
        private const int PlansPerHour = 3;
        // dojazd zmierzony (tylko log): marsz partii do celu ze slotu - grupa (0 brak pomiaru, 1 objazd, 2 proba porownawcza; zapisywana ostatnia, czytana
        // pierwsza), godzina planu wzgledem startu sesji, czas szacowany trasy gry (h), cel
        private static readonly byte[] _slotGrp = new byte[1 << SlotBits];
        private static readonly float[] _slotT0 = new float[1 << SlotBits], _slotPred = new float[1 << SlotBits];
        private static readonly float[] _slotTx = new float[1 << SlotBits], _slotTy = new float[1 << SlotBits];
        private static double _hour0;

        // liczniki doby (watki robocze - Interlocked)
        private static long _capped, _replan, _lost, _aiOff;
        private static long _arrDetN, _arrDetPm, _arrCtlN, _arrCtlPm, _arrAbort, _arrOut;
        private static long _seen, _short, _repeat, _planned, _noRoad, _notWorth, _fewRoad, _tooLong, _navFail, _tooMany, _legCalls, _ticks;
        private static readonly long[] _acc = new long[3];
        private static long _overPm, _roadPm, _timePm;
        private static int _stumbles, _stumblesAll;
        private static Exception _first;
        // spis partio-godzin (watek glowny): 0 rody, 1 karawany, 2 wozy wsi
        private static readonly int[] _hN = new int[3], _hOn = new int[3], _hDet = new int[3], _hDetOn = new int[3];
        private static int _hours;
        private static long _censusTicks;

        private static float MinDist(Settings s)
        {
            int v = s.MapRoadAiMinDistance;
            return v < 10 ? 10f : v > 200 ? 200f : v;
        }

        private static float Detour(Settings s)
        {
            int v = s.MapRoadAiDetourPercent;
            return (v < 0 ? 0 : v > 50 ? 50 : v) / 100f;
        }

        /// <summary>Czynna premia traktu - jak MapRoads.PctOf (MapRoadSpeedPercent przyciete do 0..50).</summary>
        private static float RoadPct(Settings s)
        {
            int p = s.MapRoadSpeedPercent;
            return (p < 0 ? 0 : p > MaxPercent ? MaxPercent : p) / 100f;
        }

        /// <summary>Premia drogi bocznej - jak MapRoads.SidePctOf (MapRoads.KindAt oddaje 1 tylko przy czynnych drogach bocznych).</summary>
        private static float SidePct(Settings s)
        {
            float p = s.MapSideRoadSpeedPercent;
            if (!(p > 0f)) return 0f;
            return (p > MaxPercent ? MaxPercent : p) / 100f;
        }

        private static float NowRel()
        {
            return (float)(CampaignTime.Now.ToHours - _hour0);
        }

        private static void ParStumble(Exception e)
        {
            Interlocked.Increment(ref _stumbles);
            Interlocked.Increment(ref _stumblesAll);
            if (_first == null) _first = e;
        }

        private static uint TargetHash(Vec2 t)
        {
            unchecked
            {
                uint hx = (uint)(int)Math.Floor(t.x * 4f), hy = (uint)(int)Math.Floor(t.y * 4f);
                uint h = hx * 73856093u ^ hy * 19349663u;
                return h & ~3u;
            }
        }

        private static int Cat(MobileParty mp)
        {
            return mp.IsLordParty ? 0 : mp.IsCaravan ? 1 : mp.IsVillager ? 2 : -1;
        }

        // ------------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(MobileParty), "ComputePath", new[] { typeof(CampaignVec2), typeof(MobileParty.NavigationType), typeof(bool) });
                if (m == null || m.ReturnType != typeof(bool)) { _hookNote = "NIE wpieta (brak metody MobileParty.ComputePath(CampaignVec2, NavigationType, bool) -> bool)"; Log.Info(P + "latka " + _hookNote + " - trasy gry bez zmian."); return; }
                var ml = AccessTools.DeclaredMethod(typeof(MobileParty), "RecalculateLongTermPath", Type.EmptyTypes);
                // najpierw straznik liczenia ucieczki - bez niego objazd nie jest wpinany (trasa ucieczki nie moze isc przez droge)
                if (ml == null) { _hookNote = "NIE wpieta (brak metody MobileParty.RecalculateLongTermPath - straznik ucieczki)"; Log.Info(P + "latka " + _hookNote + " - trasy gry bez zmian."); return; }
                h.Patch(ml, prefix: new HarmonyMethod(typeof(AiRoadRoutes).GetMethod("LongPrefix", BindingFlags.Public | BindingFlags.Static)),
                        finalizer: new HarmonyMethod(typeof(AiRoadRoutes).GetMethod("LongFinalizer", BindingFlags.Public | BindingFlags.Static)));
                _hookedLong = HasOurs(ml, true) && HasOurs(ml, false);
                if (!_hookedLong) { _hookNote = "NIE wpieta (straznik ucieczki RecalculateLongTermPath niewpiety)"; Log.Info(P + "latka " + _hookNote + " - trasy gry bez zmian."); return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(AiRoadRoutes).GetMethod("ComputePathPostfix", BindingFlags.Public | BindingFlags.Static)));
                var pi = Harmony.GetPatchInfo(m);
                bool ok = false;
                if (pi != null) foreach (var x in pi.Postfixes) if (x.PatchMethod != null && x.PatchMethod.DeclaringType == typeof(AiRoadRoutes)) { ok = true; break; }
                _hooked = ok;
                int others = 0;
                if (pi != null)
                {
                    foreach (var x in pi.Prefixes) if (x.PatchMethod != null && x.PatchMethod.DeclaringType != typeof(AiRoadRoutes)) others++;
                    foreach (var x in pi.Postfixes) if (x.PatchMethod != null && x.PatchMethod.DeclaringType != typeof(AiRoadRoutes)) others++;
                    foreach (var x in pi.Transpilers) if (x.PatchMethod != null && x.PatchMethod.DeclaringType != typeof(AiRoadRoutes)) others++;
                }
                _hookNote = (ok ? "wpieta" : "NIE wpieta (brak postfiksu po Patch)") + " (cudzych latek na ComputePath " + others + ")";
                Log.Info(P + "latka MobileParty.ComputePath " + _hookNote + ", straznik ucieczki RecalculateLongTermPath wpiety - wpieta zawsze, dziala przy kluczu MapRoadAiRoutes.");
            }
            catch (Exception e) { _hookNote = "NIE wpieta (" + e.GetType().Name + ")"; Log.Error("AiRoadRoutes.ApplyAll", e); }
        }

        private static bool HasOurs(MethodBase m, bool prefix)
        {
            var pi = Harmony.GetPatchInfo(m);
            if (pi == null) return false;
            var list = prefix ? pi.Prefixes : pi.Finalizers;
            foreach (var x in list) if (x.PatchMethod != null && x.PatchMethod.DeclaringType == typeof(AiRoadRoutes)) return true;
            return false;
        }

        public static void LongPrefix() { _longTerm++; }

        public static Exception LongFinalizer(Exception __exception) { if (_longTerm > 0) _longTerm--; return __exception; }

        /// <summary>Postfiks MobileParty.ComputePath (watek roboczy gry albo glowny): trasa gry gotowa w __instance.Path - objazd droga albo bez zmian.</summary>
        public static void ComputePathPostfix(MobileParty __instance, CampaignVec2 newTargetPosition, MobileParty.NavigationType navigationType, bool cacheNextLongTermPathPoint, ref bool __result)
        {
            if (!__result || _longTerm > 0) return;
            var g = _g;
            if (g == null) return;
            var s = Settings.Current;
            if (s == null || !s.MapRoadAiRoutes || !s.MapRoadSpeedBonus || s.MapRoadSpeedPercent <= 0) return;
            try { Plan(g, s, __instance, newTargetPosition, navigationType, cacheNextLongTermPathPoint); }
            catch (Exception e) { ParStumble(e); }
        }

        // ------------------------------------------------------------------ planowanie (watek roboczy)
        private static void Plan(Graph g, Settings s, MobileParty mp, CampaignVec2 target, MobileParty.NavigationType nav, bool cacheNext)
        {
            if (mp == null) return;
            int ix = mp.Party.Index, sl = ix & SlotMask;
            long k = ((long)ix << 32) | (long)TargetHash(target.ToVec2());
            // dojazd zmierzony: KAZDA trasa do innego celu (takze pogon, morze, krotki marsz) konczy otwarty marsz tej partii - dojazd albo przerwany
            if (Volatile.Read(ref _slotGrp[sl]) != 0 && (Volatile.Read(ref _slotKey[sl]) & ~3L) != k) CloseMarch(sl, ix, mp, mp.Position.ToVec2());
            if (PlanCore(g, s, mp, target, nav, cacheNext)) return;
            // trasa gry do celu przyjetego objazdu bez planowania (reszta trasy krotsza od progu, inny rodzaj nawigacji, zmiana zachowania) - objazd przepadl:
            // stan slotu "odrzucony" (spis nie liczy partii jako objazdu)
            long old = Volatile.Read(ref _slotKey[sl]);
            if (old == (k | StAccepted)) { Volatile.Write(ref _slotKey[sl], k | StRejected); Interlocked.Increment(ref _lost); }
        }

        /// <summary>Planowanie objazdu; false = partia / trasa poza regula (slot nietkniety), true = slot obsluzony (powtorka, limit, plan).</summary>
        private static bool PlanCore(Graph g, Settings s, MobileParty mp, CampaignVec2 target, MobileParty.NavigationType nav, bool cacheNext)
        {
            if (nav != MobileParty.NavigationType.Default || !target.IsOnLand || mp.IsMainParty) return false;
            int cat = Cat(mp);
            if (cat < 0) return false;
            if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress || mp.IsInRaftState) return false;
            if (mp.PartyMoveMode != MoveModeType.Point || mp.ForceAiNoPathMode) return false;
            if (mp.MapEvent != null || mp.SiegeEvent != null || mp.BesiegedSettlement != null || mp.AttachedTo != null) return false;
            if (mp.Army != null && mp.Army.LeaderParty != mp) return false;
            var ai = mp.Ai;
            if (ai == null) return false;
            var db = mp.DefaultBehavior;
            if (db != AiBehavior.GoToSettlement && db != AiBehavior.GoToPoint && db != AiBehavior.PatrolAroundPoint) return false;
            // AI wylaczone przez inny mod (uczta BK, majatek gentry BK) - tylko rozkaz do osady / punktu (patrol wokol punktu to wlasne myslenie AI)
            bool aiOff = ai.IsDisabled;
            if (aiOff && db == AiBehavior.PatrolAroundPoint) return false;
            var sb = mp.ShortTermBehavior;
            if (sb != AiBehavior.GoToSettlement && sb != AiBehavior.GoToPoint && sb != AiBehavior.PatrolAroundPoint && sb != AiBehavior.None) return false;
            if (mp.IsFleeing()) return false;
            var path = mp.Path;
            if (path == null) return false;
            int n0 = path.Size;
            if (n0 <= 0 || n0 > PathCap) return false;
            var pts = path.PathPoints;
            Vec2 S = mp.Position.ToVec2(), T = target.ToVec2();
            float L = 0f;
            Vec2 pv = S;
            for (int i = 0; i < n0; i++) { L += (pts[i] - pv).Length; pv = pts[i]; }
            Interlocked.Increment(ref _seen);
            if (!(L >= MinDist(s))) { Interlocked.Increment(ref _short); return false; }

            // ten sam cel po ODRZUCONYM objezdzie w tej samej godzinie - bez planowania (trasa gry i tak ta sama); po PRZYJETYM - planowanie od nowa: gra
            // nadpisala Path trasa gry, a bez zmiany celu nie przeliczy jej do konca marszu (powrot po ucieczce / przerwie, koniec przejscia). Cel ruchomy (patrol
            // wokol partii - nowa sciana celu kilka razy na godzine) - najwyzej PlansPerHour planow na partie na godzine, dalej trasa gry; przyjety objazd
            // tego celu przepada - stan slotu "odrzucony" (spis nie liczy partii jako objazdu). Jedna partia = jeden watek naraz (ruch partii raz na klatke).
            int idx = mp.Party.Index;
            long key = ((long)idx << 32) | (long)TargetHash(T);
            int slot = idx & SlotMask;
            int hour = (int)CampaignTime.Now.ToHours;
            long old = Volatile.Read(ref _slotKey[slot]);
            bool sameKey = (old & ~3L) == key;
            bool wasAcc = sameKey && (old & 3L) == StAccepted;
            bool sameHour = hour == Volatile.Read(ref _slotHour[slot]);
            if (sameHour && sameKey && !wasAcc) { Interlocked.Increment(ref _repeat); return true; }
            if (sameHour && Volatile.Read(ref _slotCnt[slot]) >= PlansPerHour)
            {
                Interlocked.Increment(ref _capped);
                if (wasAcc) { Volatile.Write(ref _slotKey[slot], key | StRejected); Interlocked.Increment(ref _lost); }
                return true;
            }
            if (wasAcc) Interlocked.Increment(ref _replan);

            long t0 = Stopwatch.GetTimestamp();
            long st = StRejected;
            int res = RouteOther;
            try
            {
                Interlocked.Increment(ref _planned);
                res = Route(g, s, mp, target, S, T, L, path, cacheNext, cat);
                if (res == RouteOk) { st = StAccepted; if (aiOff) Interlocked.Increment(ref _aiOff); }
            }
            finally
            {
                Volatile.Write(ref _slotCnt[slot], sameHour ? _slotCnt[slot] + 1 : 1);
                Volatile.Write(ref _slotHour[slot], hour);
                Volatile.Write(ref _slotKey[slot], key | st);
                if (!sameKey) OpenMarch(slot, mp, res, L, T);
                Interlocked.Add(ref _ticks, Stopwatch.GetTimestamp() - t0);
            }
            return true;
        }

        // ------------------------------------------------------------------ dojazd zmierzony (tylko log)
        /// <summary>Nowy marsz (nowy cel): grupa pomiaru z wyniku pierwszego planu - objazd albo proba porownawcza (droga w zasiegu, trasa gry).</summary>
        private static void OpenMarch(int slot, MobileParty mp, int res, float L, Vec2 T)
        {
            byte gp = res == RouteOk ? GrpDet : res == RouteCtl ? GrpCtl : (byte)0;
            if (gp == 0) return;
            float v = mp.LastCalculatedBaseSpeed;
            if (!(v > 0.1f)) return;
            _slotT0[slot] = NowRel();
            _slotPred[slot] = L / v;
            _slotTx[slot] = T.x; _slotTy[slot] = T.y;
            Volatile.Write(ref _slotGrp[slot], gp);
        }

        /// <summary>Koniec marszu w slocie: partia przy celu (do ArriveR albo w osadzie celu) = dojazd, inaczej przerwany. Watek roboczy albo glowny.</summary>
        private static void CloseMarch(int slot, int idx, MobileParty mp, Vec2 pos)
        {
            byte gp = Volatile.Read(ref _slotGrp[slot]);
            if (gp == 0) return;
            Volatile.Write(ref _slotGrp[slot], (byte)0);
            if ((Volatile.Read(ref _slotKey[slot]) >> 32) != idx) return;   // slot innej partii (indeks ponad 1 << 17)
            if (AtTarget(slot, mp, pos)) Arrive(gp, NowRel() - _slotT0[slot], _slotPred[slot]);
            else Interlocked.Increment(ref _arrAbort);
        }

        private static bool AtTarget(int slot, MobileParty mp, Vec2 pos)
        {
            float tx = _slotTx[slot], ty = _slotTy[slot];
            if (Len(pos.x - tx, pos.y - ty) <= ArriveR) return true;
            var st = mp.CurrentSettlement;
            if (st == null) return false;
            Vec2 a = st.GetPosition2D, b = st.GatePosition.ToVec2();
            return Len(a.x - tx, a.y - ty) <= ArriveR + 1f || Len(b.x - tx, b.y - ty) <= ArriveR + 1f;
        }

        private static void Arrive(byte gp, float hours, float pred)
        {
            if (!(pred > 0f) || !(hours > 0f) || hours > MaxMarchHours) { Interlocked.Increment(ref _arrOut); return; }
            float r = hours / pred;
            if (r > RatioCap) { Interlocked.Increment(ref _arrOut); return; }
            long pm = (long)Math.Round(r * 1000f);
            if (gp == GrpDet) { Interlocked.Increment(ref _arrDetN); Interlocked.Add(ref _arrDetPm, pm); }
            else { Interlocked.Increment(ref _arrCtlN); Interlocked.Add(ref _arrCtlPm, pm); }
        }

        /// <summary>Spis godzinny (watek glowny, co godzine): partia przy celu otwartego marszu = dojazd; marsz ponad 10 dob = poza pomiarem.</summary>
        private static void CheckArrival(MobileParty mp, int slot, int idx, float now)
        {
            if ((Volatile.Read(ref _slotKey[slot]) >> 32) != idx) return;
            if (now - _slotT0[slot] > MaxMarchHours) { Volatile.Write(ref _slotGrp[slot], (byte)0); Interlocked.Increment(ref _arrOut); return; }
            if (AtTarget(slot, mp, mp.Position.ToVec2())) CloseMarch(slot, idx, mp, mp.Position.ToVec2());
        }

        /// <summary>Koszt przejazdu lamanej start -> pts[0..n) w jednostkach "poza droga": probki co SampleStep, ta sama miara co premia (MapRoads.KindAt:
        /// 2 trakt / (1 + premia), 1 droga boczna / (1 + premia boczna), reszta 1). trakt = dlugosc probek na trakcie. Bez alokacji, watek roboczy.</summary>
        private static float RideCost(Vec2 start, Vec2[] pts, int n, float len, float pct, float spct, out float trakt)
        {
            float step = len / MaxSamples;
            if (step < SampleStep) step = SampleStep;
            float wr = 1f / (1f + pct), ws = 1f / (1f + spct);
            float cost = 0f, tr = 0f;
            Vec2 a = start;
            for (int i = 0; i < n; i++)
            {
                Vec2 b = pts[i];
                float dx = b.x - a.x, dy = b.y - a.y, d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d > 1e-4f)
                {
                    int m = (int)Math.Ceiling(d / step);
                    if (m < 1) m = 1;
                    float sl = d / m;
                    for (int j = 0; j < m; j++)
                    {
                        float t = (j + 0.5f) / m;
                        int k = MapRoads.KindAt(a.x + dx * t, a.y + dy * t);
                        if (k == 2) { cost += sl * wr; tr += sl; }
                        else if (k == 1) cost += sl * ws;
                        else cost += sl;
                    }
                }
                a = b;
            }
            trakt = tr;
            return cost;
        }

        private static int Route(Graph g, Settings s, MobileParty mp, CampaignVec2 target, Vec2 S, Vec2 T, float L, NavigationPath path, bool cacheNext, int cat)
        {
            var startFace = mp.CurrentNavigationFace;
            var endFace = target.Face;
            if (!startFace.IsValid() || !endFace.IsValid() || startFace.FaceIslandIndex != endFace.FaceIslandIndex) { Interlocked.Increment(ref _noRoad); return RouteOther; }
            int island = startFace.FaceIslandIndex;
            var scr = _scr;
            if (scr == null) { scr = new Scratch(); _scr = scr; }
            scr.Ensure(g);
            scr.Begin();
            float detour = Detour(s);   // sufit dlugosci objazdu
            float pct = RoadPct(s);     // waga drogi w koszcie = czynna premia traktu (recenzja p6-drogi: nie klucz nadkladu)
            float w = 1f / (1f + pct);
            float R = L * RadiusShare;
            if (R < RadiusMin) R = RadiusMin; else if (R > RadiusMax) R = RadiusMax;
            float R2 = R * R;
            Avoid(s, mp, scr, cat);

            // zrodla: wezly sieci w promieniu R od startu (ta sama wyspa siatki)
            int seeds = 0;
            int gx0 = (int)((S.x - R - g.GX0) * g.GInv), gx1 = (int)((S.x + R - g.GX0) * g.GInv);
            int gy0 = (int)((S.y - R - g.GY0) * g.GInv), gy1 = (int)((S.y + R - g.GY0) * g.GInv);
            if (gx0 < 0) gx0 = 0; if (gy0 < 0) gy0 = 0;
            if (gx1 >= g.GNx) gx1 = g.GNx - 1; if (gy1 >= g.GNy) gy1 = g.GNy - 1;
            for (int cy = gy0; cy <= gy1; cy++)
                for (int cx = gx0; cx <= gx1; cx++)
                {
                    int c = cy * g.GNx + cx;
                    for (int k = g.GStart[c]; k < g.GStart[c + 1]; k++)
                    {
                        int n = g.GNode[k];
                        if (!g.Ok[n] || g.Island[n] != island) continue;
                        if (scr.Nc > 0 && InZone(scr, g.X[n], g.Y[n])) continue;
                        float dx = g.X[n] - S.x, dy = g.Y[n] - S.y, d2 = dx * dx + dy * dy;
                        if (d2 > R2) continue;
                        float cost = (float)Math.Sqrt(d2);
                        if (scr.Stamp[n] == scr.Now && scr.Cost[n] <= cost) continue;
                        scr.Stamp[n] = scr.Now; scr.Cost[n] = cost; scr.Prev[n] = -1; scr.PrevA[n] = -1;
                        if (scr.Push(n, cost)) seeds++;
                    }
                }
            if (seeds == 0) { Interlocked.Increment(ref _noRoad); return RouteOther; }

            float best = float.MaxValue;
            int bestN = -1;
            int pn; float pk;
            while (scr.Pop(out pn, out pk))
            {
                if (pk > scr.Cost[pn]) continue;   // stary wpis kopca
                if (pk >= best || pk > L) break;
                float ex = T.x - g.X[pn], ey = T.y - g.Y[pn], e2 = ex * ex + ey * ey;
                if (e2 <= R2)
                {
                    float tot = pk + (float)Math.Sqrt(e2);
                    if (tot < best) { best = tot; bestN = pn; }
                }
                for (int a = g.AdjStart[pn]; a < g.AdjStart[pn + 1]; a++)
                {
                    int m = g.Adj[a];
                    float el = g.AdjLen[a];
                    if (el < 0f || !g.Ok[m] || g.Island[m] != island) continue;
                    float nk = pk + el * w;
                    if (nk > L) continue;
                    if (scr.Stamp[m] == scr.Now && nk >= scr.Cost[m]) continue;
                    if (scr.Nc > 0 && ZoneHit(scr, g.X[pn], g.Y[pn], g.X[m], g.Y[m])) continue;
                    scr.Stamp[m] = scr.Now; scr.Cost[m] = nk; scr.Prev[m] = pn; scr.PrevA[m] = a;
                    scr.Push(m, nk);
                }
            }
            if (bestN < 0) { Interlocked.Increment(ref _noRoad); return RouteOther; }
            if (best > L) { Interlocked.Increment(ref _notWorth); return RouteCtl; }

            // lancuch wezlow wejscie -> zejscie
            int cn = 0;
            for (int v = bestN; v >= 0; v = scr.Prev[v])
            {
                if (cn >= scr.Chain.Length) { Interlocked.Increment(ref _navFail); return RouteOther; }
                scr.Chain[cn++] = v;
            }
            for (int i = 0, j = cn - 1; i < j; i++, j--) { int t = scr.Chain[i]; scr.Chain[i] = scr.Chain[j]; scr.Chain[j] = t; }
            int c0 = scr.Chain[0], cl = scr.Chain[cn - 1];
            float road = 0f;
            for (int i = 1; i < cn; i++) road += g.AdjLen[scr.PrevA[scr.Chain[i]]];
            float off1 = Len(g.X[c0] - S.x, g.Y[c0] - S.y), off2 = Len(T.x - g.X[cl], T.y - g.Y[cl]);
            float est = off1 + road + off2;
            if (road < MinRoadShare * est) { Interlocked.Increment(ref _fewRoad); return RouteCtl; }
            if (est > L * (1f + detour)) { Interlocked.Increment(ref _tooLong); return RouteCtl; }

            // trasa po siatce gry: start -> wejscie, cieciwy drogi, zejscie -> cel
            var camp = Campaign.Current;
            IMapScene scene = camp.MapSceneWrapper;
            int[] inv = camp.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(MobileParty.NavigationType.Default);
            int s2l = mp.GetRegionSwitchCostFromSeaToLand(), l2s = mp.GetRegionSwitchCostFromLandToSea();
            Vec2 last = S;
            float total = 0f;
            int r = Leg(scene, scr, startFace, S, g.Face[c0], new Vec2(g.X[c0], g.Y[c0]), inv, s2l, l2s, ref last, ref total);
            for (int i = 1; i < cn && r == 0; i++)
            {
                int a = scr.Chain[i - 1], b = scr.Chain[i];
                // krawedz prosta po siatce gry (sprawdzona przy budowie) - sam punkt; inaczej silnik jak w ComputePath
                if (g.AdjDirect[scr.PrevA[b]]) r = AddPoint(scr, new Vec2(g.X[b], g.Y[b]), ref last, ref total) ? 0 : -2;
                else r = Leg(scene, scr, g.Face[a], new Vec2(g.X[a], g.Y[a]), g.Face[b], new Vec2(g.X[b], g.Y[b]), inv, s2l, l2s, ref last, ref total);
            }
            if (r == 0) r = Leg(scene, scr, g.Face[cl], new Vec2(g.X[cl], g.Y[cl]), endFace, T, inv, s2l, l2s, ref last, ref total);
            if (r == 0 && scr.Size == 0) r = -1;
            if (r == -1) { Interlocked.Increment(ref _navFail); return RouteOther; }
            if (r == -2) { Interlocked.Increment(ref _tooMany); return RouteOther; }
            if (total > L * (1f + detour)) { Interlocked.Increment(ref _tooLong); return RouteCtl; }
            // czas: koszt przejazdu objazdu i trasy gry z probek ta sama miara co premia (trasa gry tez bywa czesciowo na drodze); droga = trakt z probek
            float spct = SidePct(s), detTrakt, gameTrakt;
            float costDet = RideCost(S, scr.Out, scr.Size, total, pct, spct, out detTrakt);
            float costGame = RideCost(S, path.PathPoints, path.Size, L, pct, spct, out gameTrakt);
            if (detTrakt < MinRoadShare * total) { Interlocked.Increment(ref _fewRoad); return RouteCtl; }
            if (!(costDet <= costGame)) { Interlocked.Increment(ref _notWorth); return RouteCtl; }

            // zapis trasy do NavigationPath partii (PathBegin = 0 ustawil ComputePath)
            var pp = path.PathPoints;
            for (int i = 0; i < scr.Size; i++) pp[i] = scr.Out[i];
            path.Size = scr.Size;
            if (cacheNext) mp.NextLongTermPathPoint = new CampaignVec2(scr.Out[0], true);
            Interlocked.Increment(ref _acc[cat]);
            Interlocked.Add(ref _overPm, (long)Math.Round((total / L - 1f) * 1000f));   // bywa ujemny: trasa gry liczona z mnoznikiem 2, cieciwy bez niego
            Interlocked.Add(ref _roadPm, (long)Math.Round(detTrakt / total * 1000f));
            Interlocked.Add(ref _timePm, (long)Math.Round(costDet / costGame * 1000f));
            return RouteOk;
        }

        private static float Len(float dx, float dy) { return (float)Math.Sqrt(dx * dx + dy * dy); }

        private static float Dist(Graph g, int a, int b) { return Len(g.X[b] - g.X[a], g.Y[b] - g.Y[a]); }

        /// <summary>Strefy przepraw CrossingLaw dla tego planu: tylko lord (straz odbija partie lordow) w wojnie z panem przeprawy.</summary>
        private static void Avoid(Settings s, MobileParty mp, Scratch scr, int cat)
        {
            scr.Nc = 0;
            var cs = _cross;
            if (cat != 0 || cs.Length == 0) return;
            var mf = mp.MapFaction;
            if (mf == null) return;
            float r = s.CrossingRadius + 1f;
            scr.Cr2 = r * r;
            var cp = _crossPos;
            for (int i = 0; i < cs.Length && scr.Nc < scr.Cx.Length; i++)
            {
                var st = cs[i];
                if (st == null) continue;
                var of = st.MapFaction;
                if (of == null || of == mf || !mf.IsAtWarWith(of)) continue;
                scr.Cx[scr.Nc] = cp[i].x; scr.Cy[scr.Nc] = cp[i].y; scr.Nc++;
            }
        }

        private static bool InZone(Scratch scr, float x, float y)
        {
            for (int i = 0; i < scr.Nc; i++)
            {
                float dx = x - scr.Cx[i], dy = y - scr.Cy[i];
                if (dx * dx + dy * dy <= scr.Cr2) { Interlocked.Increment(ref _crossAvoid); return true; }
            }
            return false;
        }

        /// <summary>Cieciwa a-b wchodzi w strefe przeprawy (odleglosc odcinka od bramy <= promien).</summary>
        private static bool ZoneHit(Scratch scr, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
            for (int i = 0; i < scr.Nc; i++)
            {
                float px = scr.Cx[i] - ax, py = scr.Cy[i] - ay;
                float t = l2 > 1e-9f ? (px * dx + py * dy) / l2 : 0f;
                if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                float ex = px - t * dx, ey = py - t * dy;
                if (ex * ex + ey * ey <= scr.Cr2) { Interlocked.Increment(ref _crossAvoid); return true; }
            }
            return false;
        }

        /// <summary>Odcinek trasy: ta sama sciana = prosta (sciana wypukla), inaczej silnik (GetPathBetweenAIFaces jak w ComputePath). 0 dobrze, -1 bez sciezki,
        /// -2 ponad 128 punktow.</summary>
        private static int Leg(IMapScene scene, Scratch scr, PathFaceRecord fa, Vec2 a, PathFaceRecord fb, Vec2 b, int[] inv, int s2l, int l2s, ref Vec2 last, ref float len)
        {
            if (!fa.IsValid() || !fb.IsValid()) return -1;
            if (fa.FaceIndex == fb.FaceIndex) return AddPoint(scr, b, ref last, ref len) ? 0 : -2;
            Interlocked.Increment(ref _legCalls);
            var tmp = scr.Tmp;
            if (!scene.GetPathBetweenAIFaces(fa, fb, a, b, AgentRadius, tmp, inv, 2f, s2l, l2s)) return -1;
            int n = tmp.Size;
            if (n <= 0 || n > PathCap) return -1;
            var tp = tmp.PathPoints;
            for (int i = 0; i < n; i++) if (!AddPoint(scr, tp[i], ref last, ref len)) return -2;
            return 0;
        }

        private static bool AddPoint(Scratch scr, Vec2 p, ref Vec2 last, ref float len)
        {
            float d2 = (p - last).LengthSquared;
            if (d2 < 1e-6f) return true;   // ten sam punkt (koniec poprzedniego odcinka)
            if (scr.Size >= PathCap) return false;
            scr.Out[scr.Size++] = p;
            len += (float)Math.Sqrt(d2);
            last = p;
            return true;
        }

        // ------------------------------------------------------------------ sesja, graf (watek glowny)
        internal static void Reset()
        {
            _g = null;
            _note = "nie zbudowana";
            _tried = false;
            _cross = new TaleWorlds.CampaignSystem.Settlements.Settlement[0];
            _crossPos = new Vec2[0];
            Array.Clear(_slotKey, 0, _slotKey.Length);
            Array.Clear(_slotHour, 0, _slotHour.Length);
            Array.Clear(_slotCnt, 0, _slotCnt.Length);
            Array.Clear(_slotGrp, 0, _slotGrp.Length);
            _hour0 = 0;
            ZeroDay();
            _stumblesAll = 0; _first = null;
        }

        private static void ZeroCounters()
        {
            _seen = _short = _repeat = _planned = _noRoad = _notWorth = _fewRoad = _tooLong = _navFail = _tooMany = _legCalls = _ticks = 0;
            for (int i = 0; i < _acc.Length; i++) _acc[i] = 0;
            _overPm = _roadPm = _timePm = 0;
            _crossAvoid = 0;
            _capped = _replan = _lost = _aiOff = 0;
            _arrDetN = _arrDetPm = _arrCtlN = _arrCtlPm = _arrAbort = _arrOut = 0;
            _stumbles = 0;
        }

        private static void ZeroDay()
        {
            for (int i = 0; i < 3; i++) { _hN[i] = 0; _hOn[i] = 0; _hDet[i] = 0; _hDetOn[i] = 0; }
            _hours = 0; _censusTicks = 0;
            ZeroCounters();
        }

        private static string Status()
        {
            var s = Settings.Current;
            if (!_hooked) return "NIECZYNNE (latka ComputePath " + _hookNote + ")";
            if (s == null || !s.MapRoadAiRoutes) return "WYLACZONE (MapRoadAiRoutes)";
            if (!s.MapRoadSpeedBonus) return "NIECZYNNE (MapRoadSpeedBonus wylaczony - bez premii drogi objazd nic nie daje)";
            if (s.MapRoadSpeedPercent <= 0) return "NIECZYNNE (MapRoadSpeedPercent 0 - trakt bez premii, objazd nic nie daje)";
            if (_g == null) return "NIECZYNNE (" + _note + ")";
            return "CZYNNE";
        }

        /// <summary>Start sesji kampanii (OnSessionLaunched, PO MapRoads.SessionStart): pamiec partii od zera, graf drog przy wlaczonych kluczach.</summary>
        internal static void SessionStart()
        {
            Reset();
            try { _hour0 = CampaignTime.Now.ToHours; } catch { _hour0 = 0; }
            var s = Settings.Current;
            if (s == null || !s.MapRoadAiRoutes)
            {
                Log.Info(P + "start sesji - klucz MapRoadAiRoutes nie - trasy AI jak w grze (latka " + _hookNote + ").");
                return;
            }
            double ms = TryBuild();
            var g = _g;
            int cross = FindCrossings();
            Log.Info(P + "start sesji - klucz MapRoadAiRoutes tak, marsz od " + MinDist(s).ToString("0", Inv) + " jedn., nadklad do " + (Detour(s) * 100f).ToString("0", Inv)
                     + "% (sufit dlugosci), premia traktu w koszcie " + (RoadPct(s) * 100f).ToString("0", Inv) + "% (MapRoadSpeedPercent; droga boczna "
                     + (SidePct(s) * 100f).ToString("0.0", Inv) + "%), droga >= " + (MinRoadShare * 100f).ToString("0", Inv) + "% trasy, tylko rody / karawany / wozy wsi | " + _note
                     + (g != null ? " | siec: wezlow " + g.Nodes + " (poza siatka gry " + g.BadNodes + "), polaczen " + (g.AdjCount / 2) + ", rozstajow " + g.Joins + ", skladowych " + g.Comps
                                    + ", lamanych " + g.Lines + ", punktow pliku " + g.Points + ", dlugosc " + g.Length.ToString("0.0", Inv) + " jedn.; krawedzie po siatce gry: proste " + g.EdgesDirect
                                    + ", z objazdem " + g.EdgesBent + ", wyciete " + g.EdgesCut : "")
                     + " | przeprawy CrossingLaw " + cross + "/" + CrossingIds.Length + " (wrogi lord bez objazdu przez strefe)"
                     + " | latka ComputePath " + _hookNote + " | budowa " + ms.ToString("0.0", Inv) + " ms | " + Status() + ".");
        }

        /// <summary>Przeprawy CrossingLaw w tej kampanii (watek glowny; obiekt osady inny w kazdej kampanii).</summary>
        private static int FindCrossings()
        {
            var cs = new TaleWorlds.CampaignSystem.Settlements.Settlement[CrossingIds.Length];
            var cp = new Vec2[CrossingIds.Length];
            int found = 0;
            for (int i = 0; i < cs.Length; i++)
            {
                try { cs[i] = TaleWorlds.CampaignSystem.Settlements.Settlement.Find(CrossingIds[i]); } catch { cs[i] = null; }
                if (cs[i] != null) { cp[i] = cs[i].GetPosition2D; found++; }
            }
            _crossPos = cp;
            _cross = cs;
            return found;
        }

        private static double TryBuild()
        {
            _tried = true;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var s = Settings.Current;
                if (!s.MapRoadSpeedBonus) { _tried = false; _note = "MapRoadSpeedBonus wylaczony - siec niezbudowana"; return 0; }   // wlaczenie premii w MCM - budowa w nastepnej dobie
                if (!MapRoads.Ready) { _note = "premia drog nieczynna (MapRoads bez siatki: brak pliku albo inna mapa) - siec niezbudowana"; return 0; }
                string path = MapVillageData.DataPathFor(MapRoads.FileName);
                if (path == null || !File.Exists(path)) { _note = "BRAK pliku ModuleData\\" + MapRoads.FileName; return 0; }
                string err;
                var g = Build(path, out err);
                if (g == null) { _note = "siec niezbudowana: " + err; if (err != null && err.StartsWith("zaden wezel", StringComparison.Ordinal)) _tried = false; return 0; }
                _g = g;
                _note = "siec z pliku " + MapRoads.FileName;
            }
            catch (Exception e) { _g = null; _note = "blad budowy sieci (" + e.GetType().Name + ")"; Log.Error("AiRoadRoutes.Build", e); }
            return (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        }

        private static Graph Build(string file, out string err)
        {
            err = null;
            var ids = new List<int>(11000);
            var xs = new List<float>(11000);
            var ys = new List<float>(11000);
            int cRoad = 0, cX = 2, cY = 3;
            foreach (string raw in File.ReadAllText(file, Encoding.UTF8).Replace("\r", "").Split('\n'))
            {
                if (raw.Length == 0) continue;
                if (raw[0] == '#')
                {
                    string h = raw.TrimStart('#').Trim();
                    if (h.StartsWith("road\t", StringComparison.Ordinal))
                    {
                        string[] hc = h.Split('\t');
                        for (int i = 0; i < hc.Length; i++)
                        {
                            string nm = hc[i].Trim();
                            if (nm == "road") cRoad = i; else if (nm == "x") cX = i; else if (nm == "y") cY = i;
                        }
                    }
                    continue;
                }
                try
                {
                    string[] c = raw.Split('\t');
                    int id = int.Parse(c[cRoad], Inv);
                    float x = float.Parse(c[cX], Inv), y = float.Parse(c[cY], Inv);
                    if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) continue;
                    ids.Add(id); xs.Add(x); ys.Add(y);
                }
                catch { }
            }
            int np = xs.Count;
            if (np < 2) { err = "plik bez punktow"; return null; }
            // lamane: [ls[i], ls[i+1]) - plik posortowany po road, seq (jak MapRoads.Build)
            var ls = new List<int>();
            for (int i = 0; i < np; i++) if (i == 0 || ids[i] != ids[i - 1]) ls.Add(i);
            ls.Add(np);
            var lineOf = new int[np];
            for (int l = 0; l + 1 < ls.Count; l++) for (int i = ls[l]; i < ls[l + 1]; i++) lineOf[i] = l;
            var keep = new bool[np];
            // siatka punktow do rozstajow (komorka 2 jedn.)
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < np; i++) { minX = Math.Min(minX, xs[i]); maxX = Math.Max(maxX, xs[i]); minY = Math.Min(minY, ys[i]); maxY = Math.Max(maxY, ys[i]); }
            const float pc = 2f;
            int pnx = (int)((maxX - minX) / pc) + 1, pny = (int)((maxY - minY) / pc) + 1;
            var pcount = new int[pnx * pny + 1];
            for (int i = 0; i < np; i++) pcount[(int)((ys[i] - minY) / pc) * pnx + (int)((xs[i] - minX) / pc)]++;
            var pstart = new int[pnx * pny + 1];
            for (int c = 0; c < pnx * pny; c++) pstart[c + 1] = pstart[c] + pcount[c];
            var pfill = new int[pnx * pny];
            var plist = new int[np];
            for (int i = 0; i < np; i++) { int c = (int)((ys[i] - minY) / pc) * pnx + (int)((xs[i] - minX) / pc); plist[pstart[c] + pfill[c]++] = i; }
            var joinA = new List<int>(); var joinB = new List<int>();
            int lines = 0;
            for (int l = 0; l + 1 < ls.Count; l++)
            {
                int a = ls[l], b = ls[l + 1] - 1;
                if (b <= a) continue;   // lamana z jednego punktu - bez odcinkow (jak MapRoads)
                lines++;
                keep[a] = true; keep[b] = true;
                for (int e = 0; e < 2; e++)
                {
                    int p = e == 0 ? a : b;
                    int cx = (int)((xs[p] - minX) / pc), cy = (int)((ys[p] - minY) / pc);
                    int bestJ = -1; float bd = JoinR * JoinR;
                    for (int yy = cy - 1; yy <= cy + 1; yy++)
                        for (int xx = cx - 1; xx <= cx + 1; xx++)
                        {
                            if (xx < 0 || yy < 0 || xx >= pnx || yy >= pny) continue;
                            int c = yy * pnx + xx;
                            for (int k = pstart[c]; k < pstart[c + 1]; k++)
                            {
                                int j = plist[k];
                                if (lineOf[j] == l) continue;
                                int lj = lineOf[j];
                                if (ls[lj + 1] - 1 <= ls[lj]) continue;
                                float dx = xs[j] - xs[p], dy = ys[j] - ys[p], d2 = dx * dx + dy * dy;
                                if (d2 <= bd) { bd = d2; bestJ = j; }
                            }
                        }
                    if (bestJ >= 0) { keep[bestJ] = true; joinA.Add(p); joinB.Add(bestJ); }
                }
            }
            // Douglas-Peucker miedzy punktami wymuszonymi (konce, rozstaje)
            var stack = new List<int>(64);
            for (int l = 0; l + 1 < ls.Count; l++)
            {
                int a = ls[l], b = ls[l + 1] - 1;
                if (b <= a) continue;
                int from = a;
                for (int i = a + 1; i <= b; i++)
                {
                    if (!keep[i]) continue;
                    stack.Clear(); stack.Add(from); stack.Add(i);
                    while (stack.Count > 0)
                    {
                        int q = stack[stack.Count - 1], p = stack[stack.Count - 2];
                        stack.RemoveRange(stack.Count - 2, 2);
                        if (q - p < 2) continue;
                        float ax = xs[p], ay = ys[p], dx = xs[q] - ax, dy = ys[q] - ay, l2 = dx * dx + dy * dy;
                        int bk = -1; float bdv = -1f;
                        for (int k = p + 1; k < q; k++)
                        {
                            float px = xs[k] - ax, py = ys[k] - ay;
                            float t = l2 > 1e-9f ? (px * dx + py * dy) / l2 : 0f;
                            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                            float ex = px - t * dx, ey = py - t * dy, dv = ex * ex + ey * ey;
                            if (dv > bdv) { bdv = dv; bk = k; }
                        }
                        if (bk >= 0 && bdv > Simplify * Simplify)
                        {
                            keep[bk] = true;
                            stack.Add(p); stack.Add(bk); stack.Add(bk); stack.Add(q);
                        }
                    }
                    from = i;
                }
            }
            // wezly
            var nodeOf = new int[np];
            int nn = 0;
            for (int i = 0; i < np; i++) nodeOf[i] = keep[i] ? nn++ : -1;
            var g = new Graph();
            g.Nodes = nn; g.Points = np; g.Lines = lines; g.Joins = joinA.Count;
            g.X = new float[nn]; g.Y = new float[nn]; g.Face = new PathFaceRecord[nn]; g.Ok = new bool[nn]; g.Island = new int[nn];
            for (int i = 0; i < np; i++) if (nodeOf[i] >= 0) { g.X[nodeOf[i]] = xs[i]; g.Y[nodeOf[i]] = ys[i]; }
            // krawedzie: kolejne wezly lamanej + rozstaje
            var ea = new List<int>(nn * 2); var eb = new List<int>(nn * 2);
            double length = 0;
            for (int l = 0; l + 1 < ls.Count; l++)
            {
                int a = ls[l], b = ls[l + 1] - 1;
                if (b <= a) continue;
                int prev = -1;
                for (int i = a; i <= b; i++)
                {
                    if (nodeOf[i] < 0) continue;
                    if (prev >= 0) { ea.Add(nodeOf[prev]); eb.Add(nodeOf[i]); length += Len(xs[i] - xs[prev], ys[i] - ys[prev]); }
                    prev = i;
                }
            }
            for (int k = 0; k < joinA.Count; k++) { ea.Add(nodeOf[joinA[k]]); eb.Add(nodeOf[joinB[k]]); }
            g.Length = length;
            // sciany siatki gry (raz): wezel bez sciany albo na scianie zakazanej dla marszu ladem = poza siecia
            int bad = 0;
            for (int i = 0; i < nn; i++)
            {
                try
                {
                    var cv = new CampaignVec2(new Vec2(g.X[i], g.Y[i]), true);
                    var f = cv.Face;
                    bool ok = f.IsValid() && cv.IsValid();
                    g.Face[i] = f; g.Ok[i] = ok; g.Island[i] = f.FaceIslandIndex;
                    if (!ok) bad++;
                }
                catch { g.Face[i] = PathFaceRecord.NullFaceRecord; g.Ok[i] = false; g.Island[i] = -1; bad++; }
            }
            g.BadNodes = bad;
            if (bad >= nn) { err = "zaden wezel nie lezy na siatce gry (siatka mapy niegotowa?) - proba w nastepnej dobie"; return null; }
            // krawedzie po siatce gry (raz, watek glowny, tym samym wywolaniem co ComputePath): ta sama sciana albo sciezka silnika = sama cieciwa (kazdy punkt
            // sciezki do 0.02 jedn. od cieciwy) -> PROSTA, w planowaniu bez silnika; sciezka z objazdem do 3 x cieciwa + 2 -> dlugosc sciezki, silnik w planowaniu;
            // bez sciezki albo objazd dalszy (np. rzeka bez mostu pod farba drogi) -> krawedz WYCIETA
            int ne = ea.Count;
            var elen = new float[ne];
            var edir = new bool[ne];
            var camp = Campaign.Current;
            IMapScene scene = camp.MapSceneWrapper;
            int[] inv = camp.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(MobileParty.NavigationType.Default);
            int s2l = camp.Models.MapDistanceModel.RegionSwitchCostFromSeaToLand, l2s = camp.Models.MapDistanceModel.RegionSwitchCostFromLandToSea;
            var tmp = new NavigationPath();
            for (int k = 0; k < ne; k++)
            {
                int u = ea[k], v = eb[k];
                float chord = Dist(g, u, v);
                elen[k] = -1f;
                if (!g.Ok[u] || !g.Ok[v] || g.Island[u] != g.Island[v]) { g.EdgesCut++; continue; }
                if (g.Face[u].FaceIndex == g.Face[v].FaceIndex) { elen[k] = chord; edir[k] = true; g.EdgesDirect++; continue; }
                Vec2 a = new Vec2(g.X[u], g.Y[u]), b = new Vec2(g.X[v], g.Y[v]);
                bool ok;
                try { ok = scene.GetPathBetweenAIFaces(g.Face[u], g.Face[v], a, b, AgentRadius, tmp, inv, 2f, s2l, l2s); }
                catch { ok = false; }
                int n = ok ? tmp.Size : 0;
                if (n <= 0 || n > PathCap) { g.EdgesCut++; continue; }
                float pl = 0f, dev = 0f;
                Vec2 last = a;
                float dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy;
                for (int i = 0; i < n; i++)
                {
                    Vec2 q = tmp.PathPoints[i];
                    pl += (q - last).Length; last = q;
                    float px = q.x - a.x, py = q.y - a.y;
                    float t = l2 > 1e-9f ? (px * dx + py * dy) / l2 : 0f;
                    if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                    float ex = px - t * dx, ey = py - t * dy, e = ex * ex + ey * ey;
                    if (e > dev) dev = e;
                }
                if ((last - b).LengthSquared > 1e-4f) { g.EdgesCut++; continue; }   // silnik nie doszedl do konca krawedzi
                if (dev <= 0.02f * 0.02f) { elen[k] = chord; edir[k] = true; g.EdgesDirect++; }
                else if (pl <= chord * 3f + 2f) { elen[k] = pl; g.EdgesBent++; }
                else g.EdgesCut++;
            }
            var deg = new int[nn + 1];
            for (int k = 0; k < ea.Count; k++) { deg[ea[k]]++; deg[eb[k]]++; }
            g.AdjStart = new int[nn + 1];
            for (int i = 0; i < nn; i++) g.AdjStart[i + 1] = g.AdjStart[i] + deg[i];
            g.AdjCount = g.AdjStart[nn];
            g.Adj = new int[g.AdjCount]; g.AdjLen = new float[g.AdjCount]; g.AdjDirect = new bool[g.AdjCount];
            var fill = new int[nn];
            for (int k = 0; k < ea.Count; k++)
            {
                int u = ea[k], v = eb[k];
                int iu = g.AdjStart[u] + fill[u]++; g.Adj[iu] = v; g.AdjLen[iu] = elen[k]; g.AdjDirect[iu] = edir[k];
                int iv = g.AdjStart[v] + fill[v]++; g.Adj[iv] = u; g.AdjLen[iv] = elen[k]; g.AdjDirect[iv] = edir[k];
            }
            // skladowe (tylko do logu)
            var par = new int[nn];
            for (int i = 0; i < nn; i++) par[i] = i;
            for (int k = 0; k < ea.Count; k++)
            {
                int u = ea[k], v = eb[k];
                if (elen[k] < 0f) continue;
                int ru = Find(par, u), rv = Find(par, v);
                if (ru != rv) par[ru] = rv;
            }
            int comps = 0;
            for (int i = 0; i < nn; i++) if (g.Ok[i] && Find(par, i) == i) comps++;
            g.Comps = comps;
            // siatka wezlow (komorka NodeCell)
            float nx0 = float.MaxValue, ny0 = float.MaxValue, nx1 = float.MinValue, ny1 = float.MinValue;
            for (int i = 0; i < nn; i++) { nx0 = Math.Min(nx0, g.X[i]); nx1 = Math.Max(nx1, g.X[i]); ny0 = Math.Min(ny0, g.Y[i]); ny1 = Math.Max(ny1, g.Y[i]); }
            g.GX0 = nx0 - NodeCell; g.GY0 = ny0 - NodeCell; g.GInv = 1f / NodeCell;
            g.GNx = (int)((nx1 - g.GX0) / NodeCell) + 2; g.GNy = (int)((ny1 - g.GY0) / NodeCell) + 2;
            int cells = g.GNx * g.GNy;
            var gc = new int[cells];
            for (int i = 0; i < nn; i++) gc[(int)((g.Y[i] - g.GY0) * g.GInv) * g.GNx + (int)((g.X[i] - g.GX0) * g.GInv)]++;
            g.GStart = new int[cells + 1];
            for (int c = 0; c < cells; c++) g.GStart[c + 1] = g.GStart[c] + gc[c];
            g.GNode = new int[nn];
            var gf = new int[cells];
            for (int i = 0; i < nn; i++) { int c = (int)((g.Y[i] - g.GY0) * g.GInv) * g.GNx + (int)((g.X[i] - g.GX0) * g.GInv); g.GNode[g.GStart[c] + gf[c]++] = i; }
            return g;
        }

        private static int Find(int[] par, int a)
        {
            while (par[a] != a) { par[a] = par[par[a]]; a = par[a]; }
            return a;
        }

        // ------------------------------------------------------------------ pomiar (watek glowny)
        /// <summary>Co godzine (watek glowny): dojazd zmierzony - partia przy celu otwartego marszu (zawsze, gdy klucz czynny; samo porownanie pozycji).
        /// W dobie pomiaru (LogGate.Today) takze spis: partie rodow, karawan i wozow wsi w marszu ladem - na trakcie (MapRoads.KindAt == 2, ta sama miara
        /// co "na drodze" w linii "Drogi (DR)": ten sam filtr partii, ta sama siatka i margines), osobno na objezdzie (przyjety objazd do biezacego celu).</summary>
        internal static void Hourly()
        {
            var s = Settings.Current;
            if (_g == null || s == null || !s.MapRoadAiRoutes || Campaign.Current == null) return;
            bool census = LogGate.Today;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                float now = NowRel();
                var all = MobileParty.All;
                for (int i = 0; i < all.Count; i++)
                {
                    var mp = all[i];
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.IsMainParty) continue;
                        int k = Cat(mp);
                        if (k < 0) continue;
                        int idx = mp.Party.Index, slot = idx & SlotMask;
                        if (Volatile.Read(ref _slotGrp[slot]) != 0) CheckArrival(mp, slot, idx, now);
                        if (!census || mp.AttachedTo != null || mp.IsCurrentlyAtSea || mp.IsTransitionInProgress || !mp.IsMoving) continue;
                        var p = mp.Position;
                        bool on = MapRoads.KindAt(p.X, p.Y) == 2;   // trakt (2), jak "na drodze" w MapRoads.Hourly - droga boczna (1) nie liczy sie (siec objazdow to same trakty)
                        _hN[k]++;
                        if (on) _hOn[k]++;
                        long v = Volatile.Read(ref _slotKey[slot]);
                        if ((v & 3L) == StAccepted && (v & ~3L) == (((long)idx << 32) | (long)TargetHash(mp.MoveTargetPoint.ToVec2())))
                        {
                            _hDet[k]++;
                            if (on) _hDetOn[k]++;
                        }
                    }
                    catch (Exception e) { ParStumble(e); }
                }
                if (census) _hours++;
            }
            catch (Exception e) { ParStumble(e); }
            finally { _censusTicks += Stopwatch.GetTimestamp() - t0; }
        }

        private static string Pct(long a, long b) { return b > 0 ? (100.0 * a / b).ToString("0.0", Inv) : "-"; }

        /// <summary>Takt doby (po MapMud.Daily): w dobie linii (LogGate.Lines) linia "AI drogi (AD): dzien N" za dobe spisu; potem zerowanie (jak MapRoads.Daily).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            try
            {
                if (s.MapRoadAiRoutes && s.MapRoadSpeedBonus && _g == null && !_tried)
                {
                    double ms = TryBuild();   // klucz wlaczony w MCM w trakcie sesji - siec budowana tu (watek glowny)
                    if (_cross.Length == 0) FindCrossings();
                    Log.Info(P + "budowa sieci w trakcie sesji (klucz wlaczony w MCM): " + _note + " | budowa " + ms.ToString("0.0", Inv) + " ms | " + Status() + ".");
                }
                if (!s.MapRoadAiRoutes) { ZeroDay(); return; }
                if (!LogGate.Lines) { ZeroDay(); return; }   // spis godzinny (dojazd) biegnie co godzine - zerowany razem z licznikami
                Line(s, (int)CampaignTime.Now.ToDays - 1);
            }
            catch (Exception e) { _stumbles++; _stumblesAll++; if (_stumblesAll <= 3) Log.Error("AiRoadRoutes.Daily", e); }
            ZeroDay();
        }

        private static void Line(Settings s, int day)
        {
            var ci = Inv;
            long acc = _acc[0] + _acc[1] + _acc[2];
            long rej = _noRoad + _notWorth + _fewRoad + _tooLong + _navFail + _tooMany;
            var sb = new StringBuilder(1200);
            sb.Append(P).Append("dzien ").Append(day.ToString(ci))
              .Append(" | ").Append(Status()).Append(", marsz od ").Append(MinDist(s).ToString("0", ci)).Append(" jedn., nadklad do ").Append((Detour(s) * 100f).ToString("0", ci)).Append('%')
              .Append(" | trasy AI (rody, karawany, wozy wsi; ladem do osady / punktu): liczone ").Append(_seen.ToString(ci))
              .Append(", krotsze od progu ").Append(_short.ToString(ci)).Append(", powtorka celu ").Append(_repeat.ToString(ci))
              .Append(", ponad ").Append(PlansPerHour.ToString(ci)).Append(" plany partii na godzine ").Append(_capped.ToString(ci))
              .Append(" (objazd utracony - limit planow albo trasa gry bez planu, np. krotka reszta trasy: ").Append(_lost.ToString(ci))
              .Append("), ponowione po objezdzie ").Append(_replan.ToString(ci))
              .Append(", planowane ").Append(_planned.ToString(ci))
              .Append(" | objazdy droga ").Append(acc.ToString(ci)).Append(" (").Append(Pct(acc, _planned)).Append("% planowanych; rody ").Append(_acc[0].ToString(ci))
              .Append(", karawany ").Append(_acc[1].ToString(ci)).Append(", wozy wsi ").Append(_acc[2].ToString(ci)).Append(')')
              .Append(" (z AI wylaczonym przez inny mod - uczta / majatek BK: ").Append(_aiOff.ToString(ci)).Append(')')
              .Append(" | odrzucone ").Append(rej.ToString(ci)).Append(": bez drogi w zasiegu ").Append(_noRoad.ToString(ci)).Append(", droga sie nie oplaca ").Append(_notWorth.ToString(ci))
              .Append(", malo drogi ").Append(_fewRoad.ToString(ci)).Append(", za dlugie ").Append(_tooLong.ToString(ci)).Append(", siatka gry (odcinek bez sciezki) ").Append(_navFail.ToString(ci))
              .Append(", ponad 128 punktow ").Append(_tooMany.ToString(ci))
              .Append("; ominiete wezly / krawedzie w strefie przepraw wrogiego pana ").Append(_crossAvoid.ToString(ci));
            if (acc > 0)
                sb.Append(" | objazd sr.: nadklad drogi ").Append((_overPm / (double)acc / 10.0).ToString("+0.0;-0.0;0.0", ci)).Append("% trasy gry, droga ")
                  .Append((_roadPm / (double)acc / 10.0).ToString("0.0", ci)).Append("% trasy, czas szac. ").Append((_timePm / (double)acc / 10.0).ToString("0.0", ci)).Append("% czasu trasy gry");
            else sb.Append(" | objazd sr.: -");
            // dojazd zmierzony: srednia (godziny od planu do celu) / (trasa gry / predkosc bazowa z planu) - objazdy i proba porownawcza (droga w zasiegu, trasa gry)
            sb.Append(" | dojazd zmierzony (godziny do celu / godziny trasy gry wg predkosci bazowej z planu): objazdy ").Append(_arrDetN.ToString(ci))
              .Append(" sr. ").Append(_arrDetN > 0 ? (_arrDetPm / (double)_arrDetN / 10.0).ToString("0.0", ci) : "-")
              .Append("%, proba (trasa gry, droga w zasiegu) ").Append(_arrCtlN.ToString(ci))
              .Append(" sr. ").Append(_arrCtlN > 0 ? (_arrCtlPm / (double)_arrCtlN / 10.0).ToString("0.0", ci) : "-")
              .Append("%, przerwane ").Append(_arrAbort.ToString(ci)).Append(", poza pomiarem ").Append(_arrOut.ToString(ci));
            int n = _hN[0] + _hN[1] + _hN[2], on = _hOn[0] + _hOn[1] + _hOn[2], det = _hDet[0] + _hDet[1] + _hDet[2], deton = _hDetOn[0] + _hDetOn[1] + _hDetOn[2];
            sb.Append(" | spis partio-godzin w marszu ladem (godzin ").Append(_hours.ToString(ci)).Append("): ").Append(n.ToString(ci)).Append(", na drodze ").Append(on.ToString(ci))
              .Append(" (").Append(Pct(on, n)).Append("%; rody ").Append(_hOn[0].ToString(ci)).Append('/').Append(_hN[0].ToString(ci)).Append(" (").Append(Pct(_hOn[0], _hN[0]))
              .Append("%), karawany ").Append(_hOn[1].ToString(ci)).Append('/').Append(_hN[1].ToString(ci)).Append(" (").Append(Pct(_hOn[1], _hN[1]))
              .Append("%), wozy wsi ").Append(_hOn[2].ToString(ci)).Append('/').Append(_hN[2].ToString(ci)).Append(" (").Append(Pct(_hOn[2], _hN[2])).Append("%))")
              .Append(", na objezdzie ").Append(det.ToString(ci)).Append(", z tego na drodze ").Append(deton.ToString(ci)).Append(" (").Append(Pct(deton, det)).Append("%)");
            double planMs = _ticks * 1000.0 / Stopwatch.Frequency, censusMs = _censusTicks * 1000.0 / Stopwatch.Frequency;
            sb.Append(" | koszt: planowanie ").Append(planMs.ToString("0.00", ci)).Append(" ms/dobe (sr. ")
              .Append(_planned > 0 ? (planMs * 1000.0 / _planned).ToString("0.0", ci) : "-").Append(" us/plan, odcinkow siatki gry ").Append(_legCalls.ToString(ci))
              .Append("), spis ").Append(censusMs.ToString("0.000", ci)).Append(" ms/dobe")
              .Append(" | potkniecia dzis ").Append(_stumbles.ToString(ci)).Append(" (od wczytania ").Append(_stumblesAll.ToString(ci)).Append(')')
              .Append(_first != null ? " pierwsze: " + _first.GetType().Name + " " + (_first.Message ?? "") : "").Append('.');
            Log.Info(sb.ToString());
        }
    }
}
