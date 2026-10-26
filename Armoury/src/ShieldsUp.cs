using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 140 (25.10, fala "AI walczy o zycie", krok 2 = projekt 135a "tarcza pod ostrzalem" z design-weccrxf3h z poprawkami krytyka K1-K13 i E).
    /// Jeff 13:25: "przeciwnik jak ma tarcze a widzi ze ktos w niego strzela to zaklada tarcze z plecow ... zamiast zalozyc tarcze to idzie w lucznika
    /// i ginie jak kretyn"; 13:30: "Jak widzi ze kolega padl od strzaly a ma tarcze to zalozy tarcze i sie za nia schowa i idzie na przeciwnika".
    /// Przyczyna (RBMAI AgentAi.cs:243-257): postfiks RBM SetAiRelatedProperties daje AiWeaponFavorMultiplierPolearm = 35, dopoki wrog jest dalej niz 2.5 m
    /// (gra: 1, ASCM.cs:226-228) - czlowiek z dwurecznym drzewcem idzie z nim, tarcza zostaje na plecach; nic w C# gry nie bierze tarczy na pociski.
    ///
    /// Tryb ROZSADEK (przelacznik 139 SurvivalMode.InPanic): w PANICE nic nie robimy (gra opuszcza tarcze, Retreat -> EnforceShieldUsage(None)).
    /// Wykrycie ostrzalu - prawdziwy strzal, bez wyprzedzania:
    ///  S1 trafienie (OnMissileHit z ofiara: cialo, tarcza w rece, tarcza na plecach) - ofiara;
    ///  S2 strzal AI z celem (OnAgentShootMissile, GetTargetAgent na glownym watku) - cel;
    ///  S3 (K6) strzal gracza albo AI bez celu - punkt upadku z balistyki (velocity.z, opad 1.6 m) i wrogowie strzelca w 8 m od niego; pudlo
    ///     (OnMissileHit bez ofiary) - punkt zderzenia cd.CollisionGlobalPosition i wrogowie strzelca w 4.5 m;
    ///  S4 szyk: jedno zdarzenie na pocisk na szyk (trafienie albo pudlo, pamiec indeksu pocisku 3 s), co najmniej ShieldsUpFormationShots w 5 s
    ///     = caly szyk pod ostrzalem do ostatniego + ShieldsUpHoldSeconds (znacznik ludzi przez ApplyActionOnEachUnit co 1 s).
    /// Znacznik czlowieka = teraz + ShieldsUpHoldSeconds, przedluzany kazdym zdarzeniem.
    /// Kogo dotyczy: czlowiek AI (nie gracz), pieszy, z tarcza w ekwipunku, nie Inny (Undead), nie chorazy (sztandar w rece albo w ExtraWeaponSlot),
    /// nie przy drabinie / machinie (IsUsingGameObject), nie odlaczony od szyku, nie w PANICE (SurvivalMode) ani w odwrocie (IsRetreating) - poza odwrotem
    /// rannego 143 (recenzja: ROZSADEK - "cofa sie z tarcza i bronia": przelozenie tarczy do reki tak, uniesienie w drodze i tak blokuje warunek gry
    /// !IsRetreating w Agent.UpdateFormationOrders, na postoju (StopRetreating) - jak kazdy);
    /// strzelec z amunicja (IsRangedCached) strzela dalej - wchodzi tylko przy HoldYourFire szyku (K12: lucznik bez strzal wchodzi, wybor regula
    /// amunicji, nie IsShieldUsageEncouraged gry); ludzie gracza przy ShieldsUpYourMenToo. Bitwy morskie poza (Naval ma wlasna regule tarcz).
    /// Co robi (glowny watek, pass ludzi co 0.25 s tylko po liscie oznaczonych):
    ///  - zestaw po WSZYSTKICH uzyciach (K1): tarcza (slot 0-3), bron jednoreczna z przedmiotow o samych uzyciach jednorecznych (miecz, topor,
    ///    buzdygan, sztylet, jednoreczne drzewce), wlocznia z kilkoma uzyciami na koncu, bron rzucana wcale; "drzewce dwureczne" = dowolne uzycie
    ///    drzewca z NotUsableWithOneHand (w 1.4.8 nie ma klasy Pike);
    ///  - przelozenie (K5) w JEDNYM passie w kolejnosci gry (WieldInitialWeapons): najpierw tarcza (druga reka), potem jednoreczna, przez
    ///    Mission.AddTickActionMT (wykonanie na poczatku nastepnego Mission.OnTick); kontrola po 1.5 s; 16 polecen na pass, 3000 na bitwe, proba co
    ///    4 s, 3 proby na okres znacznika; nigdy przy wrogu blizej niz 8 m;
    ///  - preferencja (K3/K4): postfiks SetAiRelatedProperties (Priority.Last po com.rbmai) u ludzi z drzewcem dwurecznym i osobna jednoreczna:
    ///    Polearm = 1, Melee co najmniej 35 (RBM przy wrogu w 2.5 m daje 55 - zostaje); kazda zmiana flagi z UpdateAgentProperties od razu;
    ///    w 8 m od wroga flaga idzie za tym, co czlowiek trzyma (decyzja Jeffa D2: pikinier w zwarciu przy ostrzale zostaje z tarcza i mieczem;
    ///    zwolnienie po ostrzale odlozone do odejscia wroga poza 8 m - zadnej zmiany broni w zwarciu);
    ///  - uniesienie: postfiks Agent.UpdateFormationOrders - warunek oryginalu (K2: Formation != null, !IsRetreating, bez paniki komponentu),
    ///    druga reka trzyma tarcze, szyk nie ShieldWall / Square / Circle, wrog dalej niz 6 m (u rzucajacych 35 m - K12, rzuty nie duszone):
    ///    EnforceShieldUsage(pierwszy szereg DefendDown, reszta AttackEnd) - wzor gry w ShieldWall i Naval; po wygasnieciu raz None (K10), gdy
    ///    w drugiej rece nie ma tarczy (z tarcza kierunek co wywolanie ustawia gra albo prefiks RBM);
    ///  - konnica (K11, PikesKeptAgainstHorse): czlowiek z drzewcem dwurecznym nie zmienia broni i nie ma preferencji, gdy szyk konny wroga jest
    ///    blizej niz 90 m od jego szyku (z tarcza w ekwipunku i tak nie zaprze piki - CanPerformBrace wymaga !HasShieldCached; zostaje zasieg).
    /// Watki: OnAgentShootMissile / OnMissileHit tylko wpis do kolejki pod lockiem (moga przyjsc z ticku asynchronicznego); postfiksy tylko czytaja
    /// tablice po agent.Index (powieksza je tylko glowny watek, porownanie referencji agenta), liczniki postfiksow przez Interlocked (K8).
    /// Latki raz na proces przy pierwszej bitwie z kluczem (EnsurePatched, K9); kolejnosc po RBM do linii (CheckOrder w AfterStart).
    /// Potkniecia: RoutHold.Stumble miejsce "tarcze" (wyjatek w postfiksie: pierwsze 3 do Stumble, reszta w liczniku) - funkcji nie gasimy,
    /// czlowiek z 5 potknieciami wypada z listy. Linia "Bitwa (140 tarcze)" raz na bitwe (BattleSense.Clean); histogram stanu rak ofiar pociskow (K13)
    /// takze przy kluczu NIE (gdy HitScribeEnabled - punkt wyjscia testu A). Nazwa klasy nie moze byc "Log", "Settings" ani "McmSettings".
    /// 157 (26.10, Jeff 25.10 ok. 22:45 "strzelalem do kawalerzysty mial tarcze na plecach ... nie wzial tarczy do reki"): jezdzcy (klucz
    /// RidersShieldsUpUnderFire) zamiast stalego pominiecia WMounted - regula konnych w One (jednoreczna: tylko tarcza do lewej reki; dwureczna: tarcza
    /// + jednoreczna boczna albo ta sama bron jednorecznie, nie w szarzy szyku ani w kontrze P3; prog zmiany broni 4 m; bez EnforceShieldUsage);
    /// pomiar konnicy AI pod ostrzalem (P1) i wejscie kontry na strzelca (P3, HorsePursuit.Counter.cs) - plik ShieldsUp.Riders.cs.
    /// </summary>
    internal static partial class ShieldsUp
    {
        // ------------------------------------------------------------ stale
        internal const float PassEvery = 0.25f;     // pass ludzi
        internal const float FormEvery = 1f;        // pass szykow (S4, konnica, zmiany szyku)
        internal const float Window = 5f;           // okno strzalow na szyk
        private const int CmdPerPass = 16, CmdPerBattle = 3000, UapPerPass = 32, QueriesPerPass = 64, QueriesPerFrame = 12;
        private const int MaxTries = 3, StubbornAfter = 3, MaxFails = 5, QueueCap = 1024, DedupeN = 128;
        private const float RetryEvery = 4f, CheckAfter = 1.6f;           // kontrola 1.5 s od wykonania (polecenie idzie w nastepnej klatce)
        internal const float NoSwapNear = 8f, RaiseNear = 6f, ThrowerRaiseNear = 35f, CavalryNear = 90f;
        private const float MissRadius = 4.5f, LandRadius = 8f, LandDrop = 1.6f, Gravity = 9.81f, MaxFlight = 6f, DedupeFor = 3f;
        internal const float FavorPolearm = 1f, FavorMelee = 35f;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------ stan misji (glowny watek)
        private static WeakReference _m;
        private static volatile bool _on;           // klucz TAK w tej bitwie - czytane przez postfiksy
        private static volatile bool _live;         // kolejka przyjmuje zdarzenia (klucz albo sam histogram przy HitScribe; 157: albo pomiar konnicy / kontra)
        private static bool _key, _written, _yourMen, _pikes;
        private static bool _hist;                  // 157: histogram ofiar i linia 140 (klucz albo HitScribe - jak dawne _live)
        private static bool _riders;                // 157 P2: regula konnych (RidersShieldsUpUnderFire)
        private static float _hold = 8f;
        private static int _shots = 3;
        private static float _now, _accMen, _accForm;
        private static int _rot;
        private static string _kind = "?";

        // tablice po agent.Index - powieksza tylko glowny watek (Grow), postfiksy tylko czytaja (_forced/_enfSeen pisze tylko postfiks UFO)
        private static Agent[] _who;
        private static bool[] _raise, _favor, _mark;
        private static int[] _forced, _enfSeen;

        private sealed class Entry
        {
            public Agent A;
            public int Idx;
            public float Until, Since;
            public int ShieldSlot = -1, OneSlot = -1;
            public bool Pole2, OnePure, Thrower, Favor;
            public int Tries, Reverts, Fails;
            public float NextTry, CheckAt, SwappedAt;
            public bool Pending, Swapped, Stubborn, NoOne, Done, DeferSeen, Evaluated;
            public bool Near8 = true, NearRaise = true;   // nieznane = blisko (zadnej zmiany, zadnego uniesienia do pierwszej oceny)
            public bool RiderTwo;                         // 157: jezdziec widziany pod ostrzalem z dwureczna w rece (poza szarza) - preferencja jednorecznej
        }

        private sealed class FState
        {
            public readonly Queue<float> Shots = new Queue<float>();
            public float Until = -1f, LastMark = -100f;
            public bool Cav, ArrSeen;
            public ArrangementOrder.ArrangementOrderEnum Arr;
        }

        private static readonly Dictionary<int, Entry> By = new Dictionary<int, Entry>();
        private static readonly List<Entry> List = new List<Entry>();
        private static readonly Predicate<Entry> IsDone = e => e.Done;
        private static readonly Dictionary<Formation, FState> Forms = new Dictionary<Formation, FState>();
        private static readonly List<Formation> TmpF = new List<Formation>();
        private static readonly List<Agent> Units = new List<Agent>();
        private static readonly Action<Agent> Collect = a => Units.Add(a);
        private static readonly MBList<Agent> Buf = new MBList<Agent>();
        private static readonly int[] DedupeIdx = new int[DedupeN];
        private static readonly Formation[] DedupeF = new Formation[DedupeN];
        private static readonly float[] DedupeT = new float[DedupeN];
        private static int _dedupePos;

        // ------------------------------------------------------------ kolejka zdarzen (callbacki - dowolny watek)
        private struct Ev
        {
            public byte K;            // 1 strzal, 2 trafienie / pudlo
            public bool Thrown;       // strzal bronia rzucana
            public byte F;            // trafienie: 1 tarcza w rece, 2 tarcza na plecach, 0 cialo
            public int I;             // indeks pocisku (AffectorWeaponSlotOrMissileIndex)
            public Agent A, V;
            public Vec3 P, Vel;
        }
        private static readonly object QLock = new object();
        private static Ev[] _q = new Ev[QueueCap], _q2 = new Ev[QueueCap];
        private static int _qn, _qOver;

        // ------------------------------------------------------------ latki
        private static bool _patchTried;
        internal static bool PatchedSetAi, PatchedUfo;
        private static MethodInfo _tSetAi, _tUfo;
        private static string _orderSetAi = "?", _orderUfo = "?";

        // ------------------------------------------------------------ liczniki misji (glowny watek, poza oznaczonymi Interlocked)
        private static int _sAi, _sAiNoTarget, _sPlayer, _sHeroAi, _misses, _hits, _hitShield, _hitBack, _hitBody, _queries, _queriesOver, _qOverTotal;
        private static readonly HashSet<int> WithdrawMen = new HashSet<int>();   // recenzja 140/143: ranni w odwrocie 143 oznaczeni pod ostrzalem (nie pominieci)
        private static int _formUnderSec, _commands, _cmdBoth, _cmdShield, _checks, _checksOk, _retries, _reverts, _stubborn, _limitHits;
        private static float _revertTime;
        private static int _favorOn, _favorOff, _favorDefer, _uap, _arrChanges, _toShieldWall, _throws, _throwsMarked, _dropped;
        private static int _mBlk, _mTot, _uBlk, _uTot;
        private static int _vOffShield, _vOffElse, _vNoShield, _vMainTwo, _vMainOne, _vMainEmpty, _aiN;
        private static double _aiDelay, _aiRadius, _aiProb;
        private static int _qFrame;
        private static readonly HashSet<int> MarkedMen = new HashSet<int>(), AlreadyMen = new HashSet<int>(), CmdMen = new HashSet<int>(),
                                             NoOneMen = new HashSet<int>(), OkMen = new HashSet<int>(), FavorMen = new HashSet<int>();
        private static readonly HashSet<Formation> FormUnder = new HashSet<Formation>();
        // 157: WDragon (jezdziec na smoku ROT), WChase (jezdziec w poscigu 148) - tylko przy RidersShieldsUpUnderFire (bez klucza konny = WMounted jak dotad)
        private const int WRanged = 0, WMounted = 1, WBanner = 2, WObject = 3, WDetached = 4, WUndead = 5, WPanic = 6, WPlayer = 7, WHorse = 8, WNear = 9,
                          WDragon = 10, WChase = 11, WReasons = 12;
        private static readonly HashSet<int>[] SkipMen = NewSets(WReasons);
        private static readonly Dictionary<string, int[]> Types = new Dictionary<string, int[]>();
        // postfiksy (watek dowolny) - Interlocked
        private static int _setAiCalls, _setAiFavor, _ufoCalls, _enfCalls, _enfMen, _resets, _postErr;

        private static HashSet<int>[] NewSets(int n)
        {
            var a = new HashSet<int>[n];
            for (int i = 0; i < n; i++) a[i] = new HashSet<int>();
            return a;
        }

        // ------------------------------------------------------------ odczyt dla innych (dowolny watek)

        /// <summary>146: klucz TAK w tej bitwie (sygnal S4 liczony) - CloseRanks.Begin po ShieldsUp.Begin.</summary>
        internal static bool On { get { return _on; } }

        /// <summary>146: szyk pod ostrzalem wedlug S4 (co najmniej ShieldsUpFormationShots pociskow w 5 s, do ostatniego + trzymanie) - tylko glowny watek
        /// (slownik szykow), po ShieldsUp.Tick tej klatki.</summary>
        internal static bool FormFire(Formation f)
        {
            if (!_on || f == null) return false;
            FState st;
            return Forms.TryGetValue(f, out st) && st.Until > _now;
        }

        /// <summary>Czlowiek ma znacznik ostrzalu w tej bitwie teraz (HitScribe, histogram) - tylko odczyt tablic.</summary>
        internal static bool IsMarked(Agent a)
        {
            if (a == null) return false;
            try
            {
                var who = _who;
                var mark = _mark;
                int i = a.Index;
                return who != null && mark != null && i >= 0 && i < who.Length && i < mark.Length && ReferenceEquals(who[i], a) && mark[i];
            }
            catch { return false; }
        }

        /// <summary>K13: stan rak ofiary do linii HIT (HitScribe): klasa BIEZACEGO uzycia broni w rece i CurrentUsageIndex, druga reka, tarcza
        /// w ekwipunku, trafienie w tarcze na plecach, znacznik 140; przy tarczy w rece trzy pola AI tarczy (RBM AgentAi.cs:119/142, gra ASCM.cs:177/206).</summary>
        internal static string VictimState(Agent v, bool back)
        {
            if (v == null || !v.IsHuman) return "";
            var sb = new StringBuilder(160);
            sb.Append(" | ofiara: reka=");
            var mi = v.GetPrimaryWieldedItemIndex();
            if (mi >= EquipmentIndex.WeaponItemBeginSlot && mi < EquipmentIndex.NumAllWeaponSlots && !v.Equipment[mi].IsEmpty)
            {
                var w = v.Equipment[mi];
                var d = w.CurrentUsageItem;
                sb.Append(d != null ? d.WeaponClass.ToString() : "?").Append('#').Append(w.CurrentUsageIndex);
                // 157 (P1, weryfikacja konnik): KTORA bron - id przedmiotu (wzorzec, przydzial DTE czy podniesiona) i liczba uzyc
                var it = w.Item;
                sb.Append(" [").Append(it != null ? (it.StringId ?? "?") : "?").Append(", uzyc ").Append(w.WeaponsCount).Append(']');
            }
            else sb.Append("pusta");
            bool offShield = OffShield(v);
            sb.Append(", lewa=");
            var oi = v.GetOffhandWieldedItemIndex();
            if (offShield) sb.Append("tarcza");
            else if (oi >= EquipmentIndex.WeaponItemBeginSlot && oi < EquipmentIndex.NumAllWeaponSlots && !v.Equipment[oi].IsEmpty) sb.Append("inna");
            else sb.Append("pusta");
            sb.Append(", tarcza w ekw.=").Append(v.HasShieldCached ? "TAK" : "NIE").Append(", plecy=").Append(back ? "TAK" : "NIE")
              .Append(", 140=").Append(IsMarked(v) ? "TAK" : "NIE");
            if (v.HasMount) sb.Append(", konno");   // 157: jezdziec (regula konnych 140 / kontra 157)
            if (offShield)
            {
                var p = v.AgentDrivenProperties;
                if (p != null)
                    sb.Append("; AI tarczy: opoznienie ").Append(p.AiRaiseShieldDelayTimeBase.ToString("0.00", Inv)).Append(", promien ")
                      .Append(p.AiFlyingMissileCheckRadius.ToString("0.0", Inv)).Append(", szansa ").Append(p.AiUseShieldAgainstEnemyMissileProbability.ToString("0.00", Inv));
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ latki (raz na proces, K9)

        /// <summary>Pierwsza bitwa z kluczem TAK (BattleSense.AfterStart przez Begin): postfiksy SetAiRelatedProperties (po com.rbmai) i
        /// Agent.UpdateFormationOrders. Cel przez AccessTools.DeclaredMethod na klasie deklarujacej (pulapka 15.10).</summary>
        private static void EnsurePatched()
        {
            if (_patchTried) return;
            _patchTried = true;
            var h = NightHooks.H;
            if (h == null)
            {
                Log.Info("Tarcze (140): brak instancji Harmony - latki NIE wpiete (przelozenie broni dziala, preferencja broni i uniesienie tarczy nie).");
                return;
            }
            try
            {
                _tSetAi = AccessTools.DeclaredMethod(typeof(AgentStatCalculateModel), "SetAiRelatedProperties");
                if (_tSetAi != null)
                {
                    h.Patch(_tSetAi, postfix: new HarmonyMethod(typeof(ShieldsUp), nameof(SetAiPostfix)) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                    PatchedSetAi = Ours(_tSetAi);
                }
            }
            catch (Exception e) { Stumble("ShieldsUp.Patch SetAiRelatedProperties", e); }
            try
            {
                _tUfo = AccessTools.DeclaredMethod(typeof(Agent), "UpdateFormationOrders");
                if (_tUfo != null)
                {
                    h.Patch(_tUfo, postfix: new HarmonyMethod(typeof(ShieldsUp), nameof(UfoPostfix)) { priority = Priority.Last });
                    PatchedUfo = Ours(_tUfo);
                }
            }
            catch (Exception e) { Stumble("ShieldsUp.Patch UpdateFormationOrders", e); }
            Log.Info("Tarcze (140): latki raz na proces (pierwsza bitwa z kluczem AiShieldsUpUnderFire) - AgentStatCalculateModel.SetAiRelatedProperties "
                     + (_tSetAi == null ? "BRAK CELU" : (PatchedSetAi ? "wpieta" : "NIEWPIETA"))
                     + " (postfiks Priority.Last po com.rbmai: u oznaczonych z preferencja Polearm 1, Melee co najmniej 35), Agent.UpdateFormationOrders "
                     + (_tUfo == null ? "BRAK CELU" : (PatchedUfo ? "wpieta" : "NIEWPIETA"))
                     + " (postfiks: EnforceShieldUsage DefendDown / AttackEnd poza " + RaiseNear.ToString("0", Inv) + " m od wroga, warunek oryginalu).");
        }

        private static bool Ours(MethodBase target)
        {
            try
            {
                var info = Harmony.GetPatchInfo(target);
                if (info == null) return false;
                foreach (var p in info.Postfixes)
                    if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(ShieldsUp)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>AfterStart (po przepieciu latek RBM w EarlyStart): czy nasze postfiksy sa i stoja po RBM (do linii bitwy).</summary>
        private static void CheckOrder()
        {
            _orderSetAi = Order(_tSetAi, true);
            _orderUfo = Order(_tUfo, false);
        }

        private static string Order(MethodBase target, bool afterRbm)
        {
            if (target == null) return _patchTried ? "brak celu" : "bez latki";
            try
            {
                var info = Harmony.GetPatchInfo(target);
                bool ours = false, rbm = false, after = false;
                if (info != null)
                {
                    foreach (var p in info.Postfixes)
                    {
                        if (p == null) continue;
                        if (p.owner == "com.rbmai") rbm = true;
                        if (p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(ShieldsUp))
                        {
                            ours = true;
                            if (p.priority == Priority.Last && (!afterRbm || (p.after != null && Array.IndexOf(p.after, "com.rbmai") >= 0))) after = true;
                        }
                    }
                    foreach (var p in info.Prefixes) if (p != null && p.owner == "com.rbmai") rbm = true;
                }
                if (!ours) return "brak naszej";
                if (!afterRbm) return "TAK (prefiks RBM " + (rbm ? "TAK" : "NIE") + ")";
                return !rbm ? "brak RBM" : (after ? "TAK" : "NIE");
            }
            catch { return "?"; }
        }

        // ------------------------------------------------------------ postfiksy (dowolny watek - tylko odczyt tablic, Interlocked)

        /// <summary>Po RBM (Priority.Last, after com.rbmai): u oznaczonego z preferencja Polearm = 1 (cofniecie 35 z RBM), Melee co najmniej 35
        /// (RBM w 2.5 m daje 55 - zostaje). Parametr to ten sam obiekt co agent.AgentDrivenProperties (AgentDrivenProperties:1198-1201).</summary>
        public static void SetAiPostfix(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            if (!_on) return;
            try
            {
                Interlocked.Increment(ref _setAiCalls);
                if (agent == null || agentDrivenProperties == null) return;
                int i = agent.Index;
                var who = _who;
                var fav = _favor;
                if (who == null || fav == null || i < 0 || i >= who.Length || i >= fav.Length) return;
                if (!fav[i] || !ReferenceEquals(who[i], agent)) return;
                agentDrivenProperties.AiWeaponFavorMultiplierPolearm = FavorPolearm;
                if (agentDrivenProperties.AiWeaponFavorMultiplierMelee < FavorMelee) agentDrivenProperties.AiWeaponFavorMultiplierMelee = FavorMelee;
                Interlocked.Increment(ref _setAiFavor);
            }
            catch (Exception e) { PostfixError("ShieldsUp.SetAiPostfix", e); }
        }

        /// <summary>Po Agent.UpdateFormationOrders (ok. 2 razy na s na czlowieka w szyku, takze z ticku asynchronicznego - ten sam kontekst, w ktorym
        /// gra sama wola EnforceShieldUsage). K2: warunek oryginalu (Formation != null, !IsRetreating) i bez paniki komponentu; K10: raz None po
        /// wygasnieciu, gdy w drugiej rece nie ma tarczy.</summary>
        public static void UfoPostfix(Agent __instance)
        {
            if (!_on) return;
            try
            {
                Interlocked.Increment(ref _ufoCalls);
                var a = __instance;
                if (a == null) return;
                int i = a.Index;
                var who = _who;
                var raise = _raise;
                var forced = _forced;
                if (who == null || raise == null || forced == null || i < 0 || i >= who.Length || i >= raise.Length || i >= forced.Length) return;
                if (!ReferenceEquals(who[i], a)) return;
                var f = a.Formation;
                if (!raise[i])
                {
                    if (forced[i] == 0) return;
                    forced[i] = 0;
                    if (f == null || !OffShield(a))
                    {
                        a.EnforceShieldUsage(Agent.UsageDirection.None);
                        Interlocked.Increment(ref _resets);
                    }
                    return;
                }
                if (f == null || a.IsDetachedFromFormation || a.IsRetreating()) return;
                var cai = a.CommonAIComponent;
                if (cai != null && (cai.IsPanicked || cai.IsRetreating)) return;
                var ae = f.ArrangementOrder.OrderEnum;
                if (ae == ArrangementOrder.ArrangementOrderEnum.ShieldWall || ae == ArrangementOrder.ArrangementOrderEnum.Square
                    || ae == ArrangementOrder.ArrangementOrderEnum.Circle) return;
                if (!OffShield(a)) return;
                int rank = ((IFormationUnit)a).FormationRankIndex;
                a.EnforceShieldUsage(rank == 0 ? Agent.UsageDirection.DefendDown : Agent.UsageDirection.AttackEnd);
                forced[i] = 1;
                Interlocked.Increment(ref _enfCalls);
                var seen = _enfSeen;
                if (seen != null && i < seen.Length && Interlocked.CompareExchange(ref seen[i], 1, 0) == 0) Interlocked.Increment(ref _enfMen);
            }
            catch (Exception e) { PostfixError("ShieldsUp.UfoPostfix", e); }
        }

        private static void PostfixError(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _postErr);
            if (n <= 3) RoutHold.Stumble(RoutHold.SiteShields, where, e);   // K8: lock + Ensure tylko dla pierwszych 3
        }

        private static void Stumble(string where, Exception e)
        {
            RoutHold.Stumble(RoutHold.SiteShields, where, e);
        }

        // ------------------------------------------------------------ wejscia z BattleSense

        /// <summary>BattleSense.AfterStart w bitwie (Night: bitwa, nie pojedynek): stan od zera, klucze, latki raz na proces (K9), kolejnosc po RBM.</summary>
        internal static void Begin(Mission m)
        {
            _on = false;
            _live = false;
            Clear();
            if (m == null) return;
            var c = Settings.Current;
            _m = new WeakReference(m);
            _written = false;
            _kind = MissionTeams.Kind(m);
            bool naval = false;
            try { naval = m.IsNavalBattle || m.IsNavalRaidBattle; } catch { }
            _key = c != null && c.AiShieldsUpUnderFire;
            _hold = c != null ? Clamp(c.ShieldsUpHoldSeconds, 2f, 30f) : 8f;
            _shots = c != null ? Math.Max(1, Math.Min(20, c.ShieldsUpFormationShots)) : 3;
            _yourMen = c == null || c.ShieldsUpYourMenToo;
            _pikes = c == null || c.PikesKeptAgainstHorse;
            _riders = c == null || c.RidersShieldsUpUnderFire;   // 157 P2
            if (naval) return;   // Naval ma wlasna regule tarcz (ShipPlacementDetachment:678) - bez linii
            if (_key) EnsurePatched();
            CheckOrder();
            _now = m.CurrentTime;
            // 157: histogram i linia 140 jak dotad (klucz albo HitScribe); kolejka zdarzen takze dla pomiaru konnicy (P1) i kontry na strzelca (P3)
            _hist = _key || (c != null && c.HitScribeEnabled);
            CavBegin(m, c);
            _live = _hist || _cavLog || _counterKey;
            _on = _key;
        }

        /// <summary>Strzal (moze przyjsc z ticku asynchronicznego): tylko wpis do kolejki pod lockiem.</summary>
        internal static void OnShoot(Mission m, Agent shooter, EquipmentIndex wi, Vec3 pos, Vec3 vel)
        {
            if (!_live || shooter == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;
            bool thrown = false;
            try
            {
                if (wi >= EquipmentIndex.WeaponItemBeginSlot && wi < EquipmentIndex.NumAllWeaponSlots)
                {
                    var d = shooter.Equipment[wi].CurrentUsageItem;
                    thrown = d != null && d.IsConsumable && d.IsRangedWeapon;
                }
            }
            catch { }
            lock (QLock)
            {
                if (_qn >= QueueCap) { _qOver++; return; }
                _q[_qn].K = 1; _q[_qn].A = shooter; _q[_qn].V = null; _q[_qn].P = pos; _q[_qn].Vel = vel; _q[_qn].Thrown = thrown; _q[_qn].F = 0; _q[_qn].I = -1;
                _qn++;
            }
        }

        /// <summary>Zderzenie pocisku (po KAZDYM - takze tarcza i pudlo, Mission:5752-5965; moze przyjsc z innego watku): tylko wpis do kolejki.</summary>
        internal static void OnMissileHit(Mission m, Agent attacker, Agent victim, bool isCanceled, ref AttackCollisionData cd)
        {
            if (!_live || isCanceled) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;
            byte f = 0;
            Vec3 p = Vec3.Zero;
            int idx = -1;
            try
            {
                f = cd.AttackBlockedWithShield ? (byte)1 : (cd.CollidedWithShieldOnBack ? (byte)2 : (byte)0);
                p = cd.CollisionGlobalPosition;
                idx = cd.AffectorWeaponSlotOrMissileIndex;
            }
            catch { }
            lock (QLock)
            {
                if (_qn >= QueueCap) { _qOver++; return; }
                _q[_qn].K = 2; _q[_qn].A = attacker; _q[_qn].V = victim; _q[_qn].P = p; _q[_qn].Vel = Vec3.Zero; _q[_qn].Thrown = false; _q[_qn].F = f; _q[_qn].I = idx;
                _qn++;
            }
        }

        /// <summary>BattleSense.OnMissionTick (glowny watek, w bitwie): kolejka co klatke, pass ludzi co 0.25 s, pass szykow co 1 s.</summary>
        internal static void Tick(Mission m, float dt)
        {
            if (!_live || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;
            _now = m.CurrentTime;
            _qFrame = 0;
            Drain(m);
            // 157 P1: pomiar konnicy AI pod ostrzalem (co 1 s, bitwa polowa) - takze przy kluczu 140 NIE
            if (!_on && !_cavLog) return;
            bool deployed = false;
            try { deployed = m.IsDeploymentFinished; } catch { }
            if (!deployed) return;
            if (_cavLog) { try { CavTick(m, dt); } catch (Exception e) { CavStumble("ShieldsUp.Konnica", e); } }
            if (!_on) return;
            _accMen += dt;
            if (_accMen >= PassEvery)
            {
                _accMen = 0f;
                try { PassMen(m); } catch (Exception e) { Stumble("ShieldsUp.PassMen", e); }
            }
            _accForm += dt;
            if (_accForm >= FormEvery)
            {
                _accForm = 0f;
                try { PassForms(m); } catch (Exception e) { Stumble("ShieldsUp.PassForms", e); }
            }
        }

        /// <summary>BattleSense.Clean (koniec misji / zdjecie zachowania): linia bitwy raz, stan od zera (postfiksy przestaja dzialac).</summary>
        internal static void End(Mission m)
        {
            var w = _m;
            if (w == null) return;
            if (m != null && !ReferenceEquals(w.Target, m)) return;
            bool live = _live;
            _on = false;
            _live = false;
            if (!_written && live)
            {
                _written = true;
                if (_hist) { try { Write(); } catch (Exception e) { Stumble("ShieldsUp.Write", e); } }
                // 157 P1: linia "konnica AI pod ostrzalem (157)" raz na bitwe polowa
                if (_cavLog) { try { CavWrite(); } catch (Exception e) { CavStumble("ShieldsUp.KonnicaLinia", e); } }
            }
            Clear();
            _m = null;
        }

        private static void Clear()
        {
            _who = null; _raise = null; _favor = null; _mark = null; _forced = null; _enfSeen = null;
            By.Clear(); List.Clear(); Forms.Clear(); TmpF.Clear(); Units.Clear(); Buf.Clear();
            for (int i = 0; i < DedupeN; i++) { DedupeIdx[i] = -1; DedupeF[i] = null; DedupeT[i] = -100f; }
            _dedupePos = 0;
            lock (QLock)
            {
                for (int i = 0; i < _qn; i++) { _q[i].A = null; _q[i].V = null; }
                _qn = 0; _qOver = 0;
            }
            _accMen = _accForm = 0f; _rot = 0;
            _sAi = _sAiNoTarget = _sPlayer = _sHeroAi = _misses = _hits = _hitShield = _hitBack = _hitBody = _queries = _queriesOver = _qOverTotal = 0;
            WithdrawMen.Clear();
            _formUnderSec = _commands = _cmdBoth = _cmdShield = _checks = _checksOk = _retries = _reverts = _stubborn = _limitHits = 0;
            _revertTime = 0f;
            _favorOn = _favorOff = _favorDefer = _uap = _arrChanges = _toShieldWall = _throws = _throwsMarked = _dropped = 0;
            _mBlk = _mTot = _uBlk = _uTot = 0;
            _vOffShield = _vOffElse = _vNoShield = _vMainTwo = _vMainOne = _vMainEmpty = _aiN = 0;
            _aiDelay = _aiRadius = _aiProb = 0;
            MarkedMen.Clear(); AlreadyMen.Clear(); CmdMen.Clear(); NoOneMen.Clear(); OkMen.Clear(); FavorMen.Clear(); FormUnder.Clear();
            for (int i = 0; i < SkipMen.Length; i++) SkipMen[i].Clear();
            Types.Clear();
            RiderClear();   // 157: liczniki konnych (P2) i pomiar konnicy (P1)
            Interlocked.Exchange(ref _setAiCalls, 0); Interlocked.Exchange(ref _setAiFavor, 0); Interlocked.Exchange(ref _ufoCalls, 0);
            Interlocked.Exchange(ref _enfCalls, 0); Interlocked.Exchange(ref _enfMen, 0); Interlocked.Exchange(ref _resets, 0); Interlocked.Exchange(ref _postErr, 0);
        }

        // ------------------------------------------------------------ kolejka (glowny watek)

        private static void Drain(Mission m)
        {
            Ev[] q;
            int n, over;
            lock (QLock)
            {
                q = _q; n = _qn; over = _qOver;
                _q = _q2; _q2 = q; _qn = 0; _qOver = 0;
            }
            _qOverTotal += over;
            bool act = _on;
            bool deployed = false;
            if (n > 0 && (act || _cavLog || _counterKey)) { try { deployed = m.IsDeploymentFinished; } catch { } }
            for (int i = 0; i < n; i++)
            {
                try
                {
                    if (q[i].K == 1) Shot(m, ref q[i], act && deployed);
                    else if (q[i].K == 2) Hit(m, ref q[i], act && deployed, deployed);
                }
                catch (Exception e) { Stumble("ShieldsUp.Zdarzenie", e); }
                q[i].A = null; q[i].V = null;
            }
        }

        private static void Shot(Mission m, ref Ev ev, bool act)
        {
            var s = ev.A;
            if (s == null) return;
            if (ev.Thrown && _hist) { _throws++; if (IsMarked(s)) _throwsMarked++; }
            if (!act || !s.IsActive() || s.Team == null) return;
            if (s.IsAIControlled && !s.IsMainAgent)
            {
                Agent t = null;
                try { t = s.GetTargetAgent(); } catch { t = null; }
                if (t != null && t.IsHuman && t.IsActive() && t.Team != null && t.Team.IsEnemyOf(s.Team))
                {
                    _sAi++;
                    TryMark(t, _now + _hold);   // S2
                    return;
                }
                _sAiNoTarget++;
            }
            else if (s.IsAIControlled) _sHeroAi++;   // recenzja 140: bohater gracza pod SI (autotest) - ta sama sciezka S3, osobny licznik
            else _sPlayer++;
            // S3 (K6): punkt upadku z balistyki - opad LandDrop ponizej punktu wylotu, bez oporu powietrza
            var v = ev.Vel;
            float vh2 = v.x * v.x + v.y * v.y;
            if (vh2 < 1f) return;
            float t2 = (v.z + (float)Math.Sqrt(v.z * v.z + 2f * Gravity * LandDrop)) / Gravity;
            if (t2 > MaxFlight) t2 = MaxFlight;
            var land = new Vec2(ev.P.x + v.x * t2, ev.P.y + v.y * t2);
            MarkNear(m, land, LandRadius, s.Team, -1);
        }

        private static void Hit(Mission m, ref Ev ev, bool act, bool deployed)
        {
            var at = ev.A;
            var v = ev.V;
            if (v != null)
            {
                // 157: jezdziec trafiony pociskiem (on sam albo jego kon) - pomiar P1 i wejscie kontry P3; przed filtrem IsHuman (strzala w konia to tez
                // ostrzal jezdzca); wlasny try - wyjatek tu nie moze zabrac 140 tego trafienia
                if ((_cavLog || _counterKey) && deployed)
                {
                    try { RiderHit(m, at, v); } catch (Exception e) { CavStumble("ShieldsUp.JezdziecTrafiony", e); }
                }
                if (!v.IsHuman)
                {
                    // recenzja 157 (P2): strzala w konia - znacznik S1 i zdarzenie S4 dla jezdzca (tylko przy regule konnych; histogram i CloseRanks - tylko ludzie)
                    if (act && _riders && v.IsMount) HorseHit(v, at, ev.I);
                    return;
                }
                if (at != null && at.Team != null && v.Team != null && !v.Team.IsEnemyOf(at.Team)) return;   // swoj w swojego - nie ostrzal
                if (_hist)
                {
                    _hits++;
                    if (ev.F == 1) _hitShield++; else if (ev.F == 2) _hitBack++; else _hitBody++;
                    Histogram(v, ev.F == 1);
                    CloseRanks.OnHit(v, ev.F);   // 146: trafienia wedlug szyku pod ostrzalem (nasz ShieldWall / Loose / bez zmiany)
                }
                if (!act || !v.IsActive()) return;
                TryMark(v, _now + _hold);   // S1
                FEvent(v.Formation, ev.I);
                return;
            }
            if (!act || at == null || at.Team == null) return;
            _misses++;
            MarkNear(m, ev.P.AsVec2, MissRadius, at.Team, ev.I);   // S3 (K6): pudlo - punkt zderzenia
        }

        /// <summary>Recenzja 157: kon trafiony pociskiem - jego jezdziec jak trafiony (S1, S4); swoj w swojego - nic (jak u ludzi).</summary>
        private static void HorseHit(Agent horse, Agent at, int missile)
        {
            var r = horse.RiderAgent;
            if (r == null || !r.IsHuman || !r.IsActive()) return;
            if (at != null && at.Team != null && r.Team != null && !r.Team.IsEnemyOf(at.Team)) return;
            TryMark(r, _now + _hold);   // S1
            FEvent(r.Formation, missile);
        }

        /// <summary>Wrogowie druzyny strzelca w promieniu od punktu: znacznik (S3), przy indeksie pocisku jedno zdarzenie S4 na szyk.</summary>
        private static void MarkNear(Mission m, Vec2 p, float r, Team shooterTeam, int missile)
        {
            if (_qFrame >= QueriesPerFrame) { _queriesOver++; return; }
            _qFrame++;
            _queries++;
            m.GetNearbyEnemyAgents(p, r, shooterTeam, Buf);
            TmpF.Clear();
            for (int i = 0; i < Buf.Count; i++)
            {
                var a = Buf[i];
                if (a == null || !a.IsHuman || !a.IsActive()) continue;
                TryMark(a, _now + _hold);
                var f = a.Formation;
                if (missile >= 0 && f != null && !TmpF.Contains(f)) TmpF.Add(f);
            }
            for (int i = 0; i < TmpF.Count; i++) FEvent(TmpF[i], missile);
            TmpF.Clear();
            Buf.Clear();
        }

        /// <summary>S4: jedno zdarzenie na pocisk na szyk (pamiec indeksu pocisku 3 s), okno 5 s.</summary>
        private static void FEvent(Formation f, int missile)
        {
            if (f == null) return;
            if (missile >= 0)
            {
                for (int i = 0; i < DedupeN; i++)
                    if (DedupeIdx[i] == missile && ReferenceEquals(DedupeF[i], f) && _now - DedupeT[i] < DedupeFor) return;
                DedupeIdx[_dedupePos] = missile; DedupeF[_dedupePos] = f; DedupeT[_dedupePos] = _now;
                _dedupePos = (_dedupePos + 1) % DedupeN;
            }
            var st = GetF(f);
            st.Shots.Enqueue(_now);
            while (st.Shots.Count > 0 && _now - st.Shots.Peek() > Window) st.Shots.Dequeue();
            while (st.Shots.Count > 64) st.Shots.Dequeue();
            if (st.Shots.Count >= _shots)
            {
                FormUnder.Add(f);
                st.Until = _now + _hold;
            }
        }

        private static FState GetF(Formation f)
        {
            FState st;
            if (Forms.TryGetValue(f, out st)) return st;
            st = new FState();
            Forms[f] = st;
            if (_pikes) { try { st.Cav = CavNear(Mission.Current, f); } catch { } }
            return st;
        }

        /// <summary>K13: histogram stanu rak ofiar pociskow (wszystkie trafienia, bez limitu HitScribe) i BLOK oznaczonych / nieoznaczonych.</summary>
        private static void Histogram(Agent v, bool blocked)
        {
            bool off = OffShield(v);
            bool has = v.HasShieldCached;
            if (off) _vOffShield++; else if (has) _vOffElse++; else _vNoShield++;
            int slot;
            int k = MainKind(v, out slot);
            if (k == KTwo) _vMainTwo++; else if (k == KEmpty) _vMainEmpty++; else _vMainOne++;
            bool marked = IsMarked(v);
            if (off && marked) { _mTot++; if (blocked) _mBlk++; }
            else if (!marked && has) { _uTot++; if (blocked) _uBlk++; }
            if (off)
            {
                var p = v.AgentDrivenProperties;
                if (p != null)
                {
                    _aiN++;
                    _aiDelay += p.AiRaiseShieldDelayTimeBase;
                    _aiRadius += p.AiFlyingMissileCheckRadius;
                    _aiProb += p.AiUseShieldAgainstEnemyMissileProbability;
                }
            }
        }

        // ------------------------------------------------------------ kogo dotyczy

        /// <summary>Powod pominiecia (indeks W*) albo -1. full = takze cechy stale (Inny, chorazy, ludzie gracza) - przy pierwszym znaczniku.</summary>
        private static int Skip(Agent a, bool full)
        {
            if (full)
            {
                if (Undead.Character(a.Character)) return WUndead;
                if (!_yourMen && a.Team != null && a.Team.IsPlayerTeam) return WPlayer;
                var ex = a.Equipment[EquipmentIndex.ExtraWeaponSlot];
                if (!ex.IsEmpty && ex.IsBanner()) return WBanner;
            }
            int slot;
            if (MainKind(a, out slot) == KBanner) return WBanner;
            if (a.HasMount)
            {
                // 157 P2: konny wchodzi tylko przy RidersShieldsUpUnderFire; smok ROT i jezdziec w poscigu 148 (cel na uciekajacym) - poza regula
                if (!_riders) return WMounted;
                if (MarchToContact.IsDragon(a.MountAgent)) return WDragon;
                if (HorsePursuit.RidingAnyThread(a)) return WChase;
            }
            if (a.IsUsingGameObject) return WObject;
            if (a.IsDetachedFromFormation) return WDetached;
            if (a.IsRangedCached)
            {
                var f = a.Formation;
                bool hold = f != null && f.FiringOrder.OrderEnum == FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire;
                if (!hold) return WRanged;   // K12: lucznik / kusznik z amunicja strzela dalej
            }
            // recenzja 140/143: ranny w odwrocie z bronia (natywny Agent.Retreat - natywne IsRetreating() prawdziwe w drodze) to ROZSADEK przelacznika - nie pomijamy:
            // tarcza do reki dozwolona (przy wrogu w 8 m i tak nie), uniesienie w drodze blokuje warunek gry !IsRetreating (UpdateFormationOrders), na postoju jak kazdy
            var k = RoutHold.Live;
            if (k != null && k.Withdrawing(a)) { WithdrawMen.Add(a.Index); return -1; }
            if (a.IsRetreating() || SurvivalMode.InPanic(a)) return WPanic;   // przelacznik 139: PANIKA - bez rozsadku
            return -1;
        }

        /// <summary>Znacznik ostrzalu do 'until' (S1-S3: teraz + trzymanie; S4: koniec ostrzalu szyku).</summary>
        private static void TryMark(Agent a, float until)
        {
            if (a == null || !a.IsHuman || !a.IsActive() || a.IsMainAgent || !a.IsAIControlled) return;
            if (!a.HasShieldCached) return;
            int idx = a.Index;
            if (idx < 0) return;
            Entry e;
            if (By.TryGetValue(idx, out e))
            {
                if (ReferenceEquals(e.A, a) && !e.Done)
                {
                    if (until > e.Until) e.Until = until;
                    _mark[idx] = true;
                    var ff = a.Formation;
                    if (ff != null) GetF(ff).LastMark = _now;
                    return;
                }
                Finish(e, false);
            }
            int why = Skip(a, true);
            if (why >= 0) { SkipMen[why].Add(idx); return; }
            Grow(idx);
            e = new Entry { A = a, Idx = idx, Until = until, Since = _now, NextTry = _now };
            e.Thrower = a.HasAnyRangedWeaponCached && !a.IsRangedCached;
            Kit(e);
            By[idx] = e;
            List.Add(e);
            _who[idx] = a;
            _mark[idx] = true;
            bool first = MarkedMen.Add(idx);
            int slot;
            if (OffShield(a) && MainKind(a, out slot) == KOne) AlreadyMen.Add(idx);
            if (first)
            {
                try
                {
                    var id = a.Character != null ? (a.Character.StringId ?? "?") : "?";
                    int[] t;
                    if (!Types.TryGetValue(id, out t)) { t = new int[2]; Types[id] = t; }
                    t[0]++;
                }
                catch { }
            }
            var f = a.Formation;
            if (f != null) GetF(f).LastMark = _now;
        }

        private static void Grow(int idx)
        {
            var who = _who;
            if (who != null && idx < who.Length) return;
            int n = who == null ? 512 : who.Length;
            while (n <= idx) n *= 2;
            var nWho = new Agent[n];
            var nRaise = new bool[n];
            var nFav = new bool[n];
            var nMark = new bool[n];
            var nForced = new int[n];
            var nSeen = new int[n];
            if (who != null)
            {
                Array.Copy(who, nWho, who.Length);
                Array.Copy(_raise, nRaise, _raise.Length);
                Array.Copy(_favor, nFav, _favor.Length);
                Array.Copy(_mark, nMark, _mark.Length);
                Array.Copy(_forced, nForced, _forced.Length);
                Array.Copy(_enfSeen, nSeen, _enfSeen.Length);
            }
            // postfiksy czytaja _who pierwsze (a potem sprawdzaja dlugosc kazdej tablicy) - _who na koncu
            _raise = nRaise; _favor = nFav; _mark = nMark; _forced = nForced; _enfSeen = nSeen;
            _who = nWho;
        }

        // ------------------------------------------------------------ zestaw i rece

        private const int KEmpty = 0, KOne = 1, KTwo = 2, KBanner = 3;

        /// <summary>Glowna reka: pusta / jednoreczna (takze bron rzucana i uzycie jednoreczne wloczni) / dwureczna (biezace uzycie z NotUsableWithOneHand
        /// - takze luk i kusza) / sztandar.</summary>
        private static int MainKind(Agent a, out int slot)
        {
            slot = -1;
            var mi = a.GetPrimaryWieldedItemIndex();
            if (mi < EquipmentIndex.WeaponItemBeginSlot || mi >= EquipmentIndex.NumAllWeaponSlots) return KEmpty;
            var w = a.Equipment[mi];
            if (w.IsEmpty) return KEmpty;
            slot = (int)mi;
            if (w.IsBanner()) return KBanner;
            var d = w.CurrentUsageItem;
            if (d == null) return KEmpty;
            return (d.WeaponFlags & WeaponFlags.NotUsableWithOneHand) != 0 ? KTwo : KOne;
        }

        internal static bool OffShield(Agent a)   // 146: ta sama miara "tarcza w rece" dla szyku (CloseRanks)
        {
            var oi = a.GetOffhandWieldedItemIndex();
            if (oi < EquipmentIndex.WeaponItemBeginSlot || oi >= EquipmentIndex.NumAllWeaponSlots) return false;
            var w = a.Equipment[oi];
            return !w.IsEmpty && w.IsShield();
        }

        private static int Rank(WeaponClass c)
        {
            switch (c)
            {
                case WeaponClass.OneHandedSword: return 1;
                case WeaponClass.OneHandedAxe: return 2;
                case WeaponClass.Mace: case WeaponClass.Pick: return 3;
                case WeaponClass.Dagger: return 4;
                default: return 5;   // jednoreczne drzewce i reszta
            }
        }

        /// <summary>K1: zestaw po WSZYSTKICH uzyciach przedmiotu (MissionWeapon.GetWeaponComponentDataForUsage) - sprawdzany przy kazdym uzyciu
        /// (RBM moze wytracic tarcze, RBMCombat zdejmuje ja przy 0 HP).</summary>
        private static void Kit(Entry e)
        {
            var a = e.A;
            int shield = -1, one = -1, rank = 99;
            bool pole2 = false;
            for (int i = 0; i < 4; i++)
            {
                var w = a.Equipment[i];
                if (w.IsEmpty) continue;
                if (w.IsShield()) { if (shield < 0 && w.HitPoints > 0) shield = i; continue; }
                int n = w.WeaponsCount;
                bool any1h = false, all1h = true, cons = false, ranged = false;
                WeaponClass c1 = WeaponClass.Undefined;
                for (int u = 0; u < n; u++)
                {
                    var d = w.GetWeaponComponentDataForUsage(u);
                    if (d == null) continue;
                    bool two = (d.WeaponFlags & WeaponFlags.NotUsableWithOneHand) != 0;
                    if (d.IsConsumable) cons = true;
                    if (d.IsRangedWeapon) ranged = true;
                    if (d.IsMeleeWeapon && !two) { any1h = true; if (c1 == WeaponClass.Undefined) c1 = d.WeaponClass; }
                    else all1h = false;
                    if (two && d.IsMeleeWeapon && (d.IsPolearm || d.WeaponClass == WeaponClass.TwoHandedPolearm || d.WeaponClass == WeaponClass.LowGripPolearm)) pole2 = true;
                }
                if (cons || ranged || !any1h) continue;   // bron rzucana i strzelecka nie jako jednoreczna (E: "albo wcale")
                int r = all1h ? Rank(c1) : 6;             // wlocznia z kilkoma uzyciami na koncu (K1)
                if (r < rank) { rank = r; one = i; }
            }
            e.ShieldSlot = shield;
            e.OneSlot = one;
            e.OnePure = rank <= 5;   // osobna bron o samych uzyciach jednorecznych (nie ta sama wlocznia)
            e.Pole2 = pole2;
        }

        /// <summary>K11: szyk konny wroga blizej niz 90 m od szyku (po szykach wroga z IsCavalryFormation, nie ClosestSignificantlyLargeEnemyFormation).</summary>
        internal static bool CavNear(Mission m, Formation f)   // 146: bez Loose przy konnicy wroga (CloseRanks) - ta sama miara
        {
            if (m == null || f == null || f.Team == null) return false;
            var my = f.CachedAveragePosition;
            float r2 = CavalryNear * CavalryNear;
            foreach (var t in m.Teams)
            {
                if (t == null || !t.IsEnemyOf(f.Team)) continue;
                foreach (var ef in t.FormationsIncludingEmpty)
                {
                    if (ef == null || ef.CountOfUnits == 0) continue;
                    var q = ef.QuerySystem;
                    if (q == null || !q.IsCavalryFormation) continue;
                    if (ef.CachedAveragePosition.DistanceSquared(my) < r2) return true;
                }
            }
            return false;
        }

        private static bool PikeHorse(Entry e)
        {
            if (!_pikes || !e.Pole2) return false;
            var f = e.A.Formation;
            FState st;
            return f != null && Forms.TryGetValue(f, out st) && st.Cav;
        }

        /// <summary>Wrog w 8 m (zmiana broni) i w promieniu uniesienia (6 m, u rzucajacych 35 m). Szyk daleko od najblizszego szyku wroga = bez zapytania.
        /// 157: jezdziec - prog zmiany broni RiderNoSwapNear (4 m), bez uniesienia (NearRaise zawsze "blisko").</summary>
        private static void Near(Mission m, Entry e, bool rider, ref int queries)
        {
            var a = e.A;
            float swap = rider ? RiderNoSwapNear : NoSwapNear;
            float r = rider ? RiderNoSwapNear : (e.Thrower ? ThrowerRaiseNear : NoSwapNear);
            var f = a.Formation;
            if (f != null)
            {
                float d2 = f.CachedClosestEnemyFormationDistanceSquared;
                float far = r + 0.5f * f.Depth + 25f;
                if (d2 > far * far) { e.Near8 = false; e.NearRaise = false; e.Evaluated = true; return; }
            }
            if (queries >= QueriesPerPass || a.Team == null) return;   // poza limitem - stara ocena
            queries++;
            _queries++;
            m.GetNearbyEnemyAgents(a.Position.AsVec2, r, a.Team, Buf);
            float best = float.MaxValue;
            var p = a.Position.AsVec2;
            for (int i = 0; i < Buf.Count; i++)
            {
                var x = Buf[i];
                if (x == null || !x.IsHuman || !x.IsActive()) continue;
                float d = x.Position.AsVec2.DistanceSquared(p);
                if (d < best) best = d;
            }
            Buf.Clear();
            e.Near8 = best < swap * swap;
            float rr = e.Thrower ? ThrowerRaiseNear : RaiseNear;
            e.NearRaise = rider || best < rr * rr;
            e.Evaluated = true;
        }

        // ------------------------------------------------------------ pass ludzi (co 0.25 s)

        private static void PassMen(Mission m)
        {
            int n = List.Count;
            if (n == 0) return;
            int queries = 0, cmds = 0, uaps = 0;
            int start = n > 0 ? _rot % n : 0;
            _rot += 7;
            for (int k = 0; k < n; k++)
            {
                var e = List[(start + k) % n];
                if (e.Done) continue;
                try { One(m, e, ref queries, ref cmds, ref uaps); e.Fails = 0; }
                catch (Exception ex)
                {
                    Stumble("ShieldsUp.Czlowiek", ex);
                    if (++e.Fails >= MaxFails) { _dropped++; Finish(e, true); }
                }
            }
            List.RemoveAll(IsDone);
        }

        private static void One(Mission m, Entry e, ref int queries, ref int cmds, ref int uaps)
        {
            var a = e.A;
            int idx = e.Idx;
            if (!a.IsActive()) { Finish(e, false); return; }
            int why = Skip(a, false);
            if (why >= 0) { SkipMen[why].Add(idx); Finish(e, uaps++ < UapPerPass); return; }
            if (!a.HasShieldCached) { Finish(e, uaps++ < UapPerPass); return; }
            Kit(e);
            if (e.ShieldSlot < 0) { Finish(e, uaps++ < UapPerPass); return; }
            e.Thrower = a.HasAnyRangedWeaponCached && !a.IsRangedCached;   // po ostatnim rzucie - zwykly promien uniesienia
            // 157 P2: jezdziec (Skip przepuszcza konnego tylko przy RidersShieldsUpUnderFire) - wlasna regula ponizej; po utracie konia jak pieszy
            bool rider = a.HasMount;
            if (rider) RiderMen.Add(idx);
            Near(m, e, rider, ref queries);
            bool expired = _now >= e.Until;
            int mainSlot;
            int kind = MainKind(a, out mainSlot);
            bool off = OffShield(a);
            bool horse = !rider && PikeHorse(e);   // K11 dotyczy pieszego pikiniera (zaparcie piki) - nie jezdzca
            // 157 P2: szarza szyku (rozkaz albo zachowanie szarzy - FormCharging) - jezdziec z dwureczna zostaje przy lancy;
            // recenzja 157: kontra na strzelca P3 osobno - bron i preferencja zamrozone (zwolnienie preferencji przy wejsciu w kontre zabieralo tarcze z reki)
            bool counter = rider && HorsePursuit.Countering(a);
            bool charge = rider && !counter && RiderCharge(a);
            if (rider && !expired && !charge && !counter && kind == KTwo) e.RiderTwo = true;

            // preferencja (K3/K4/K11): flaga tylko przy zmianie, od razu UpdateAgentProperties; w 8 m od wroga zadnej zmiany broni (D2)
            // tylko z OSOBNA jednoreczna - u czlowieka z sama wlocznia o kilku uzyciach Polearm 1 nie ma czego wybrac (rzuty, R6)
            // 157: jezdziec - tylko gdy pod ostrzalem trzymal dwureczna (z jednoreczna w rece nic nie zmieniamy), zwolnienie po ostrzale albo przy szarzy szyku;
            // prog jezdzca 4 m (Near8 liczone z RiderNoSwapNear)
            bool want = rider ? (!expired && !charge && e.RiderTwo && e.Pole2 && e.OneSlot >= 0 && e.OnePure && e.Evaluated)
                              : (!expired && e.Pole2 && e.OneSlot >= 0 && e.OnePure && !horse && e.Evaluated);
            if (want != e.Favor && !counter)
            {
                bool allowed;
                if (!e.Near8) allowed = true;
                else allowed = !want && kind == KTwo;   // w 8 m (jezdziec 4 m): zwolnic wolno tylko, gdy i tak trzyma dwureczna (preferencja idzie za reka)
                if (!allowed)
                {
                    if (want) { if (rider) RiderNear.Add(idx); else SkipMen[WNear].Add(idx); }
                    else if (!e.DeferSeen) { e.DeferSeen = true; _favorDefer++; }
                }
                else if (uaps < UapPerPass)
                {
                    uaps++;
                    e.Favor = want;
                    _favor[idx] = want;
                    if (want) { _favorOn++; FavorMen.Add(idx); if (rider) RiderFavorMen.Add(idx); }
                    else
                    {
                        _favorOff++;
                        // 157: zwolnienie przez szarze - powrot do lancy jest zamierzony, nie liczy sie jako "powrot do dwurecznej" ani do uporu
                        if (rider && charge && !expired) { _rChargeRelease++; e.Swapped = false; }
                    }
                    a.UpdateAgentProperties();
                    _uap++;
                }
            }

            if (expired)
            {
                _raise[idx] = false;
                _mark[idx] = false;
                if (!e.Favor) Finish(e, false);   // z preferencja czeka na zwolnienie (wrog poza 8 m, jezdziec 4 m)
                return;
            }
            _mark[idx] = true;

            // uniesienie (postfiks UpdateFormationOrders): tarcza w rece i wrog poza promieniem; 157: jezdziec nigdy (AI gry samo unosi tarcze na pociski -
            // BLOK u ROTlord_247 w 1. bitwie 25.10; EnforceShieldUsage w siodle niesprawdzone)
            _raise[idx] = !rider && off && !e.NearRaise && e.Evaluated;

            // kontrola po 1.5 s od wykonania polecenia (157: jezdzcy w osobnych licznikach - wiersz AI-5 sprawdz_logi dalej o pieszych)
            if (e.Pending && _now >= e.CheckAt)
            {
                e.Pending = false;
                if (rider) _rChecks++; else _checks++;
                if (off)
                {
                    if (rider) _rChecksOk++; else _checksOk++;
                    e.Swapped = true;
                    e.SwappedAt = _now;
                    if (OkMen.Add(idx))
                    {
                        try
                        {
                            int[] t;
                            var id = a.Character != null ? (a.Character.StringId ?? "?") : "?";
                            if (Types.TryGetValue(id, out t)) t[1]++;
                        }
                        catch { }
                    }
                }
            }
            // powrot do dwurecznej (albo tarcza odlozona) w czasie znacznika
            if (e.Swapped && !(off && kind == KOne))
            {
                e.Swapped = false;
                if (rider) _rReverts++;
                else
                {
                    _reverts++;
                    _revertTime += _now - e.SwappedAt;
                }
                if (++e.Reverts >= StubbornAfter && !e.Stubborn) { e.Stubborn = true; if (rider) _rStubborn++; else _stubborn++; }
            }

            // przelozenie (K5): tarcza, potem jednoreczna, w jednym passie
            if (e.Pending || e.Stubborn || e.NoOne || e.Tries >= MaxTries || _now < e.NextTry || !e.Evaluated) return;
            if (off && kind == KOne) return;
            if (horse) { SkipMen[WHorse].Add(idx); return; }
            if ((charge || counter) && kind != KOne) { RiderInCharge.Add(idx); return; }   // 157: dwureczna w szarzy szyku / kontrze - bez zmiany broni
            if (e.Near8) { if (rider) RiderNear.Add(idx); else SkipMen[WNear].Add(idx); return; }
            if (cmds >= CmdPerPass || _commands >= CmdPerBattle) { _limitHits++; return; }
            bool both = false, onlyShield = false;
            if (kind == KOne)
            {
                if (!off) onlyShield = true;
            }
            else if (kind == KTwo || kind == KEmpty)
            {
                if (e.OneSlot >= 0 && e.OneSlot != mainSlot) both = true;
                else if (e.OneSlot >= 0 && !off) onlyShield = true;   // wlocznia z kilkoma uzyciami trzymana oburacz - sama tarcza
                else if (e.OneSlot < 0) { e.NoOne = true; if (rider) RiderNoOne.Add(idx); else NoOneMen.Add(idx); return; }
            }
            if (!both && !onlyShield) return;
            if (!off) Wield(m, a, e.ShieldSlot);
            if (both) Wield(m, a, e.OneSlot);
            cmds += (off ? 0 : 1) + (both ? 1 : 0);
            _commands += (off ? 0 : 1) + (both ? 1 : 0);
            if (rider)
            {
                // 157: jezdziec - jednoreczna w rece: tylko tarcza; dwureczna (albo pusta reka): jednoreczna boczna + tarcza albo ta sama bron jednorecznie
                if (kind == KOne) { _rCmdShield++; RiderShieldMen.Add(idx); }
                else { _rCmdTwo++; RiderTwoMen.Add(idx); }
            }
            else
            {
                if (both) _cmdBoth++; else _cmdShield++;
                CmdMen.Add(idx);
            }
            if (e.Tries > 0) { if (rider) _rRetries++; else _retries++; }
            e.Tries++;
            e.Pending = true;
            e.CheckAt = _now + CheckAfter;
            e.NextTry = _now + RetryEvery;
        }

        private static void Wield(Mission m, Agent a, int slot)
        {
            m.AddTickActionMT(Mission.MissionTickAction.TryToWieldWeaponInSlot, a, slot, (int)Agent.WeaponWieldActionType.WithAnimation);
        }

        /// <summary>Koniec znacznika czlowieka: flagi postfiksow zgaszone; preferencja zdjeta (z UpdateAgentProperties, gdy wolno - inaczej RBM w 3 s).</summary>
        private static void Finish(Entry e, bool update)
        {
            if (e.Done) return;
            e.Done = true;
            Entry cur;
            if (By.TryGetValue(e.Idx, out cur) && ReferenceEquals(cur, e)) By.Remove(e.Idx);
            int i = e.Idx;
            if (_raise != null && i >= 0 && i < _raise.Length)
            {
                _raise[i] = false;
                _mark[i] = false;
                if (e.Favor)
                {
                    _favor[i] = false;
                    e.Favor = false;
                    _favorOff++;
                    if (update)
                    {
                        try { if (e.A.IsActive()) { e.A.UpdateAgentProperties(); _uap++; } }
                        catch (Exception ex) { Stumble("ShieldsUp.Zwolnienie", ex); }
                    }
                }
            }
        }

        // ------------------------------------------------------------ pass szykow (co 1 s)

        private static void PassForms(Mission m)
        {
            TmpF.Clear();
            foreach (var kv in Forms) TmpF.Add(kv.Key);
            for (int k = 0; k < TmpF.Count; k++)
            {
                var f = TmpF[k];
                FState st;
                if (!Forms.TryGetValue(f, out st)) continue;
                try
                {
                    if (f.CountOfUnits == 0) continue;
                    while (st.Shots.Count > 0 && _now - st.Shots.Peek() > Window) st.Shots.Dequeue();
                    bool recent = _now - st.LastMark < _hold + 2f;
                    if (recent || st.Until > _now)
                    {
                        if (_pikes) st.Cav = CavNear(m, f);
                        // K7: zmiany szyku (RBM: Line / Loose / ShieldWall) w szykach z oznaczonymi
                        var ae = f.ArrangementOrder.OrderEnum;
                        if (st.ArrSeen && ae != st.Arr) { _arrChanges++; if (ae == ArrangementOrder.ArrangementOrderEnum.ShieldWall) _toShieldWall++; }
                        st.Arr = ae;
                        st.ArrSeen = true;
                    }
                    else st.ArrSeen = false;
                    if (st.Until > _now)
                    {
                        _formUnderSec++;
                        Units.Clear();
                        f.ApplyActionOnEachUnit(Collect);   // gra iteruje swoje listy - my tylko zbieramy
                        for (int u = 0; u < Units.Count; u++) TryMark(Units[u], st.Until);   // S4
                        Units.Clear();
                    }
                }
                catch (Exception e) { Units.Clear(); Stumble("ShieldsUp.Szyk", e); }
            }
            TmpF.Clear();
        }

        // ------------------------------------------------------------ linia bitwy

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }

        private static string Avg(double sum, int n, string fmt) { return n > 0 ? (sum / n).ToString(fmt, Inv) : "-"; }

        private static void Write()
        {
            if (!_key && _hits == 0 && _throws == 0) return;   // klucz NIE i bitwa bez pociskow - bez linii
            int stum;
            lock (RoutHold.Gate) { stum = RoutHold.StumblesLocked(RoutHold.SiteShields); }
            var sb = new StringBuilder(1800);
            sb.Append("Bitwa (140 tarcze): misja ").Append(_kind).Append(", klucz ").Append(_key ? "TAK" : "NIE")
              .Append(" (trzymanie ").Append(_hold.ToString("0.#", Inv)).Append(" s, szyk od ").Append(_shots).Append(" pociskow w ").Append(Window.ToString("0", Inv))
              .Append(" s, ludzie gracza ").Append(_yourMen ? "TAK" : "NIE").Append(", piki przy konnicy ").Append(_pikes ? "TAK" : "NIE")
              .Append("; latki: SetAi po RBM ").Append(_orderSetAi).Append(", UpdateFormationOrders ").Append(_orderUfo)
              .Append("; wywolan postfiksu SetAi ").Append(Volatile.Read(ref _setAiCalls)).Append(" (z preferencja ").Append(Volatile.Read(ref _setAiFavor))
              .Append("), UpdateFormationOrders ").Append(Volatile.Read(ref _ufoCalls)).Append(')')
              .Append(" | ostrzal: strzalow AI z celem ").Append(_sAi).Append(", AI bez celu ").Append(_sAiNoTarget).Append(", gracza ").Append(_sPlayer)
              .Append(", bohatera gracza pod SI ").Append(_sHeroAi)
              .Append(" (bez celu: punkt upadku z balistyki), pudel z punktem upadku ").Append(_misses)
              .Append(", trafien ").Append(_hits).Append(" (tarcza w rece ").Append(_hitShield).Append(", tarcza na plecach ").Append(_hitBack).Append(", cialo ").Append(_hitBody)
              .Append("), szykow pod ostrzalem ").Append(FormUnder.Count).Append(" (").Append(_formUnderSec).Append(" s), zapytan ").Append(_queries)
              .Append(" (ponad limit klatki ").Append(_queriesOver).Append("), nadmiar kolejki ").Append(_qOverTotal)
              .Append(" | ludzie: oznaczonych ").Append(MarkedMen.Count).Append(" (juz z tarcza w rece ").Append(AlreadyMen.Count).Append(')')
              .Append(" | przelozenia ").Append(_cmdBoth + _cmdShield).Append(", ludzi ").Append(CmdMen.Count).Append(" (jednoreczna+tarcza ").Append(_cmdBoth)
              .Append(", tylko tarcza ").Append(_cmdShield).Append("; polecen ").Append(_commands).Append("), bez jednorecznej ").Append(NoOneMen.Count)
              .Append("; pominieci: strzelcy z amunicja ").Append(SkipMen[WRanged].Count).Append(", konni ").Append(SkipMen[WMounted].Count)
              .Append(", chorazy ").Append(SkipMen[WBanner].Count).Append(", obiekt ").Append(SkipMen[WObject].Count).Append(", odlaczeni ").Append(SkipMen[WDetached].Count)
              .Append(", Inni ").Append(SkipMen[WUndead].Count).Append(", panika ").Append(SkipMen[WPanic].Count).Append(", ludzie gracza ").Append(SkipMen[WPlayer].Count)
              .Append(" (ranni w odwrocie 143 NIE pominieci ").Append(WithdrawMen.Count).Append(')')
              .Append(", konnica blisko ").Append(SkipMen[WHorse].Count).Append(", wrog <").Append(NoSwapNear.ToString("0", Inv)).Append(" m ").Append(SkipMen[WNear].Count)
              .Append(", limit polecen ").Append(_limitHits);
            AppendRiders(sb);   // 157 P2: segment " | konni (157) ..." - jezdzcy poza licznikami pieszych
            sb
              .Append(" | po 1.5 s tarcza w rece ").Append(_checksOk).Append('/').Append(_checks).Append(", powtorki ").Append(_retries)
              .Append(", powroty do dwurecznej ").Append(_reverts).Append(" (srednio po ").Append(Avg(_revertTime, _reverts, "0.0")).Append(" s), uparci ").Append(_stubborn)
              .Append(" | preferencja (Polearm ").Append(FavorPolearm.ToString("0", Inv)).Append(", Melee ").Append(FavorMelee.ToString("0", Inv)).Append("): wlaczona ")
              .Append(_favorOn).Append(" razy (ludzi ").Append(FavorMen.Count).Append("), zwolniona ").Append(_favorOff).Append(" (odlozona przez wroga <")
              .Append(NoSwapNear.ToString("0", Inv)).Append(" m ").Append(_favorDefer).Append("), UpdateAgentProperties ").Append(_uap)
              .Append(" | EnforceShieldUsage ").Append(Volatile.Read(ref _enfCalls)).Append(" wywolan, ludzi ").Append(Volatile.Read(ref _enfMen))
              .Append(", None po wygasnieciu ").Append(Volatile.Read(ref _resets))
              .Append(" | zmiany szyku w oznaczonych szykach ").Append(_arrChanges).Append(" (na ShieldWall ").Append(_toShieldWall).Append(')')
              .Append(" | trafienia oznaczonych z tarcza w rece: BLOK ").Append(_mBlk).Append('/').Append(_mTot)
              .Append(" (nieoznaczeni z tarcza w ekw.: ").Append(_uBlk).Append('/').Append(_uTot).Append(')')
              .Append(" | rzuty broni rzucanej ").Append(_throws).Append(" (oznaczonych ").Append(_throwsMarked).Append(')')
              .Append(" | ofiary pociskow (stan rak): lewa tarcza ").Append(_vOffShield).Append(" / tarcza nie w rece ").Append(_vOffElse).Append(" / bez tarczy ").Append(_vNoShield)
              .Append("; glowna dwureczna lub luk ").Append(_vMainTwo).Append(", jednoreczna ").Append(_vMainOne).Append(", pusta ").Append(_vMainEmpty)
              .Append("; z tarcza w rece sr. AiRaiseShieldDelayTimeBase ").Append(Avg(_aiDelay, _aiN, "0.00")).Append(", AiFlyingMissileCheckRadius ")
              .Append(Avg(_aiRadius, _aiN, "0.0")).Append(", AiUseShieldAgainstEnemyMissileProbability ").Append(Avg(_aiProb, _aiN, "0.00"))
              .Append(" | potkniecia tarcze ").Append(stum).Append(" (w postfiksach ").Append(Volatile.Read(ref _postErr)).Append(", wypadli z listy ").Append(_dropped).Append(')');
            // probka 3 typow: oznaczonych / z tarcza w rece po 1.5 s
            sb.Append("; probka typow:");
            int shown = 0;
            var used = new HashSet<string>();
            while (shown < 3)
            {
                string bestId = null;
                int[] best = null;
                foreach (var kv in Types)
                {
                    if (used.Contains(kv.Key)) continue;
                    if (best == null || kv.Value[0] > best[0]) { best = kv.Value; bestId = kv.Key; }
                }
                if (bestId == null) break;
                used.Add(bestId);
                sb.Append(' ').Append(bestId).Append(' ').Append(best[1]).Append('/').Append(best[0]);
                shown++;
            }
            if (shown == 0) sb.Append(" -");
            sb.Append('.');
            Log.Info(sb.ToString());
        }
    }
}
