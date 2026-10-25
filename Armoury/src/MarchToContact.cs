using System;
using System.Collections.Generic;
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
    /// noc 25.10 P1 "marsz-doktryny" (projekt ai-marsz-taktyki.json A/M1; klucze MarchToContactEnabled, MarchStepPace, CavalryApproachPace, CavalryGallopDistance,
    /// ArchersWalkUntil, PlayerMoveOrderWalks). Jeff 25.10: "armie nie biegly non stop tylko szly ... dopiero jak jest szarza to jest okrzyk i biegna na siebie,
    /// aby nie tracic sil". Szyk idzie stepem do szarzy, biegnie dopiero w szarzy.
    ///
    /// KONTRAKT (sygnatury ustalone w P0 - nie zmieniac, inne paczki je wolaja):
    ///  - ApplyMissionPatches(h)  - z NightHooks.FirstBattle (pierwsza bitwa, glowny watek, raz na proces); zwraca krotki opis do linii "Latki nocy (K3)".
    ///  - Begin / Tick / End      - z BattleSense (AfterStart; OnMissionTick w kolejnosci BattleCries -> MarchToContact -> Doctrine -> BattleSound, K4;
    ///                              OnEndMission / OnRemoveBehavior). Wolane tylko w bitwie (BattleSense: _battle i nie pojedynek); End bezpieczne wielokrotnie.
    ///  - IsMarching(f)           - dla BattleSound (bebny, K7): szyk ma teraz cap marszu. Glowny watek.
    ///  - CapFor(a)               - dla FieldCraft (M0): gorny limit marszu czlowieka w m/s albo -1 (brak).
    ///  - OnChargeStarted(f)      - JEDYNY sygnal szarzy (K4): BattleCries.ChargeStarted w chwili przejscia szyku na Charge / ChargeToTarget - tu zdjecie capu
    ///                              i od razu CacheMovementSpeedOfUnits (RBM w ChargeWithTarget liczy limit wlasnie z CachedMovementSpeed).
    ///  - ResetCounters / Activity / Append - segment 'marsz' linii "Bitwa (NB2)" (RoutHold.Ensure / Write pod RoutHold.Gate).
    ///  - 144 (AI oszczedza sily, AiBreath): ChargeFresh(f) / ChargeAge(f) - swieza szarza szyku (od sygnalu do pierwszego zwarcia, najwyzej FreshFor = 20 s; B2),
    ///    SpeedPatched; latka AdjustSpeedLimit niesie tez cap oddechu (AiBreath.CapOf) dla szyku bez capu marszu - za wszystkimi wyjsciami (B1: bez szyku,
    ///    uciekajacy, odlaczeni, MissionPeer, konni, dogonienie w odwrocie - nic); CapFor zwraca cap marszu x 1.1, a bez niego cap oddechu (kara FieldCraft
    ///    nie podnosi limitu ponad krok). 134c (MarchAtWalkingPace): tempo szyku pieszego najwyzej WalkPace (1.75 / 1.1 = ok. 1.6 m/s), limit czlowieka najwyzej
    ///    WalkLineMax 1.75 m/s, dogonienie najwyzej WalkCatchUpMax 2.1 m/s (W3 - bez obnizenia tempa szyku ludzie zostawaliby z tylu i wpadali w dogonienie 2.88).
    ///
    /// DOWOD [K] (dekompilacja mbfull, RBMAI): silnik co 0.45-0.55 s na czlowieka AI w szyku (Agent.cs:4723 -> HumanAIComponent.ParallelUpdateFormationMovement)
    /// liczy limit predkosci z Formation.CachedMovementSpeed (GetDesiredSpeedInFormation: Hold = (0.7..1.1) x CachedMovementSpeed - utrzymanie linii, Charge =
    /// srednia predkosc maksymalna) i wpisuje go w AdjustSpeedLimit. CachedMovementSpeed liczy prywatne Formation.CacheMovementSpeedOfUnits (srednia predkosc
    /// biegu ludzi x ograniczenie szyku, co 1.9-2.1 s w Formation.Tick). RBM (AdjustSpeedLimitPatch) w prefiksie: konnica dostaje MountSpeed i return false
    /// (zawsze pelny galop), dolne progi 0.9 w szarzy i manewrach, 0.6 w BehaviorAdvance / Regroup pod ostrzalem.
    ///
    /// ZASADY (stan liczy glowny watek co 0.5 s, per szyk; tablica [TeamIndex x 10 + FormationIndex]):
    ///  - tylko bitwa polowa (Mission.IsFieldBattle; morska, wypad morski, oblezenie, wypad, kryjowka - nie), po rozstawieniu, szyk z ludzmi, wazna druzyna;
    ///  - szyk AI: marsz przy rozkazie Move / Advance / FallBack (Charge, ChargeToTarget, Retreat, Stop, Follow - wolny);
    ///    szyk gracza (nie AI): Advance i Move (Move tylko przy PlayerMoveOrderWalks) = marsz, reszta wolna;
    ///  - piechota: tempo = najwolniejszy chod ludzi szyku (WalkSpeedCached, 1.8 m/s z monsters.xml) x MarchStepPace; ponad 250 m od wroga x1.2, najwyzej
    ///    2.0 m/s (kolumna szybszym krokiem, ponizej progu biegu FieldCraft); pod ostrzalem (UnderRangedAttackRatio >= 0.33) tylko tempo szyku - prog RBM 0.6 (szybki krok) zostaje, celowo;
    ///  - lucznicy pieszo: marsz, dopoki najblizszy wrog dalej niz ArchersWalkUntil (powrot do marszu 20 m dalej); konni lucznicy - nigdy;
    ///  - konnica: klus = CavalryApproachPace x srednia predkosc maksymalna szyku do CavalryGallopDistance;
    ///  - 136 odpoczynek konnicy AI (RestCav): srednia stamina koni szyku (HorseWind) ponizej HorseAiRestBelow i wrog dalej niz 70 m - tempo odpoczynku
    ///    (HorseWind.RestSpeed: step / lekki klus wedlug obciazenia koni; recenzja 136 - klus podejscia 0.45 x pelna to dla obciazonego konia ROT canter, ktory dalej
    ///    meczy), takze przy rozkazie szarzy (przed szarza marszem 128) i po zwolnieniu, az srednia wroci do HorseAiRestUntil albo wrog blizej niz 60 m;
    ///    sygnal szarzy czeka (ChargeHeld liczy odpoczynek na zywo ta sama regula co Eval - RestState);
    ///  - bieg: sygnal szarzy (BattleCries.ChargeStarted) albo rozkaz szarzy w ticku (gdy sygnal nie przyszedl - liczone osobno), zwarcie (wrog 25 m od
    ///    szyku), galop konnicy; szyk zwolniony zostaje wolny 30 s, potem wraca do marszu dopiero, gdy wrog odejdzie (piechota 50 m, lucznicy jak wyzej,
    ///    konnica dystans galopu + 100 m - nawroty RBMBehaviorCavalryCharge sa do 110 m);
    ///  - szyk ze smokiem ROT (wierzchowiec z monsterem dragon*, takze forma latajaca) - bez marszu (czlowiek na smoku pomijany takze w latce).
    /// Latki: (1) postfiks Formation.CacheMovementSpeedOfUnits: CachedMovementSpeed = Min(oryginal, cap) - silnik sam trzyma linie wokol tempa marszu;
    /// (2) postfiks HumanAIComponent.AdjustSpeedLimit (po com.rbmai): koncowy limit czlowieka i konia = Min(to, co ustawila gra, cap x 1.1), a gdy gra
    /// zdjela limit (czlowiek daleko od miejsca w szyku) - cap x 1.6; konnica zawsze (RBM wpisal jej MountSpeed). Uciekajacy i odlaczeni - bez zmian.
    /// Latka (2) biegnie w watkach roboczych: bez alokacji, logu i Settings.Current; tylko tablice pisane w OnMissionTick (ktory nie biegnie rownolegle
    /// z tickiem agentow - Mission.cs:3740-3776) i liczniki Interlocked. Wyjatek = licznik, bez globalnego wylacznika.
    /// </summary>
    internal static class MarchToContact
    {
        private const int MaxTeams = 16, Stride = 10, Slots = MaxTeams * Stride;
        internal const float TickEvery = 0.5f, ScanEvery = 5f, SampleEvery = 2f;
        internal const float FarFrom = 250f, FarMul = 1.2f, LineMul = 1.1f, CatchUpMul = 1.6f;
        // szybszy krok kolumny daleko od wroga najwyzej 2.0 m/s: x1.1 utrzymania linii = 2.2 m/s, czyli ponizej dolnego progu biegu FieldCraft (2.2) -
        // kolumna nie traci sil na dalekim podejsciu (projekt liczyl 1.8 x 1.2 = 2.16 bez marginesu linii)
        internal const float FarPaceMax = 2.0f;
        internal const float ContactGap = 25f, FreeHold = 30f, InfRearm = 50f, ArcherHyst = 20f, CavRearm = 100f, ChargeSampleFor = 15f;
        // 144 (134c, W3): prawdziwy krok - limit czlowieka ponizej progu animacji chodu (human walking_speed_limit 1.8, monsters.xml), tempo szyku tak, zeby
        // utrzymanie linii (x1.1) miescilo sie w kroku, dogonienie ponizej progu biegu FieldCraft (2.2)
        internal const float WalkLineMax = 1.75f, WalkPace = WalkLineMax / LineMul, WalkCatchUpMax = 2.1f;
        // 144 (B2): swieza szarza - od sygnalu do pierwszego zwarcia (wrog 25 m od szyku), najwyzej tyle sekund; AiBreath jej nie zatrzymuje
        internal const float FreshFor = 20f;
        private const byte KNone = 0, KInf = 1, KArch = 2, KCav = 3, KHorseArch = 4;

        // ---- stan czytany przez latki (watki robocze); zapis tylko z glownego watku
        private static volatile bool _on;
        private static readonly float[] _capMove = new float[Slots];    // cap CachedMovementSpeed (m/s), 0 = brak
        private static readonly float[] _capLimit = new float[Slots];   // cap limitu czlowieka i konia (m/s), 0 = latka 2 nie pisze
        private static readonly bool[] _cav = new bool[Slots];          // konnica: pisz zawsze (RBM wpisal MountSpeed)
        private static readonly Formation[] _who = new Formation[Slots];

        // ---- stan glownego watku per szyk
        private static readonly byte[] _kind = new byte[Slots];
        private static readonly float[] _freeUntil = new float[Slots];
        private static readonly bool[] _released = new bool[Slots];
        private static readonly bool[] _archFree = new bool[Slots];
        private static readonly float[] _walk = new float[Slots];
        private static readonly float[] _nextScan = new float[Slots];
        private static readonly bool[] _dragon = new bool[Slots];
        private static readonly bool[] _player = new bool[Slots];
        private static readonly bool[] _wasCharge = new bool[Slots];
        private static readonly bool[] _signal = new bool[Slots];
        private static readonly float[] _chargeAt = new float[Slots];
        private static readonly bool[] _everMarched = new bool[Slots];
        private static readonly bool[] _chargeHeld = new bool[Slots];   // 128: szyk z rozkazem szarzy idzie jeszcze marszem (wrog dalej niz prog biegu)
        private static readonly bool[] _rest = new bool[Slots];         // 136: konnica AI daje koniom odsapnac (klus) - srednia S koni szyku z HorseWind
        private static readonly bool[] _capRest = new bool[Slots];      // 136 (recenzja): obecny cap to odpoczynek (wyjscie z niego w marsz liczy sie jako wejscie w marsz)
        private static readonly float[] _freshUntil = new float[Slots];  // 144 (B2): swieza szarza do tej chwili (0 = nie; zerowane przy pierwszym zwarciu)

        private static WeakReference _m;
        private static float _acc, _sampleAcc;
        private static bool _any;
        // ustawienia skopiowane w Tick (latki ich nie czytaja - tylko tablice)
        private static float _pace = 1f, _cavPace = 0.45f, _gallop = 120f, _archUntil = 120f;
        private static bool _playerMove = true;
        // 128 (25.10, Jeff: "na poczatku powinni maszerowac a nie ciagle wszyscy biegaja w zbrojach"): szarza z daleka = marsz / klus do progu biegu
        private static bool _chargeFirst = true;
        private static float _runFrom = 60f;
        // 136 (25.10, Jeff: "konie tez powinny miec stamine", "przeciwnik ... walczy o zycie"): konnica AI ze zmeczonymi konmi (srednia S szyku ponizej
        // HorseAiRestBelow) idzie klusem, dopoki wrog dalej niz RestNear, az srednia wroci do HorseAiRestUntil; 0 = nigdy
        internal const float RestNear = 60f, RestRearm = 10f;   // wejscie w odpoczynek dopiero ponad RestNear + RestRearm (bez migania klus / galop na granicy)
        private static int _restBelow = 35, _restUntil = 65;
        // 144 (134c, klucz MarchAtWalkingPace): marsz prawdziwym krokiem; czytane tez przez latke (watki robocze) - kopia z Tick
        private static volatile bool _trueWalk;

        // ---- latki
        private static bool _patchedSpeed, _patchedCache, _rbmPrefix, _triedPatch;
        private static Action<Formation, float> _setMoveSpeed;
        private static Action<Formation> _recache;
        private static MethodInfo _tSpeed, _tCache;

        // ---- liczniki misji (pod RoutHold.Gate; zerowane w RoutHold.Ensure przez ResetCounters); latki - Interlocked
        private static int _cInf, _cArch, _cCav, _cPlayer, _cSignal, _cNoSignal, _cContact, _cGallop, _cArchNear, _cOrder, _cRecache, _cDragon, _cStum;
        private static int _cSignals;
        private static int _cHeld, _tHeld, _cHeldRun, _tHeldRun;   // 128: szarz zaczetych marszem / doprowadzonych marszem do progu biegu
        private static float _sumChargeD, _sumGallopD;
        private static int _nChargeD, _nGallopD;
        private static float _sumMarchV, _sumChargeV;
        private static int _nMarchV, _nChargeV;
        private static int _hitLine, _hitCatch, _hitCache, _postErr, _postDragon;
        private static int _hitWalk, _hitWalkCatch;   // 144 (134c): zapisy limitu przyciete do kroku / dogonienie przyciete do 2.1 m/s (Interlocked)
        private static readonly HashSet<int> _marched = new HashSet<int>();

        // ---- pomocnicze bez alokacji (glowny watek)
        private static float _scanWalk;
        private static bool _scanDragon;
        private static readonly Action<Agent> _scan = ScanUnit;
        private static int _sampleLeft;
        private static float _sampleSum;
        private static int _sampleN;
        private static readonly Action<Agent> _sample = SampleUnit;

        // ------------------------------------------------------------ latki (raz na proces, pierwsza bitwa)

        internal static string ApplyMissionPatches(Harmony h)
        {
            if (_triedPatch) return "juz wpiete";
            _triedPatch = true;
            var why = new StringBuilder(160);
            try
            {
                var setter = AccessTools.PropertySetter(typeof(Formation), "CachedMovementSpeed");
                if (setter != null) _setMoveSpeed = AccessTools.MethodDelegate<Action<Formation, float>>(setter);
            }
            catch (Exception e) { _setMoveSpeed = null; why.Append(" setter ").Append(e.GetType().Name); }
            try
            {
                _tCache = AccessTools.DeclaredMethod(typeof(Formation), "CacheMovementSpeedOfUnits");
                if (_tCache != null && _setMoveSpeed != null)
                {
                    h.Patch(_tCache, postfix: new HarmonyMethod(typeof(MarchToContact), nameof(PostCache)) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                    _patchedCache = Owned(_tCache, h.Id, false);
                    if (_patchedCache) _recache = AccessTools.MethodDelegate<Action<Formation>>(_tCache);
                }
                else why.Append(_tCache == null ? " brak CacheMovementSpeedOfUnits" : " brak settera CachedMovementSpeed");
            }
            catch (Exception e) { _patchedCache = false; why.Append(" Cache ").Append(e.GetType().Name); RoutHold.Stumble(RoutHold.SiteMarch, "MarchToContact.LatkaCache", e); }
            try
            {
                _tSpeed = AccessTools.DeclaredMethod(typeof(HumanAIComponent), "AdjustSpeedLimit", new[] { typeof(Agent), typeof(float), typeof(bool) });
                if (_tSpeed != null)
                {
                    h.Patch(_tSpeed, postfix: new HarmonyMethod(typeof(MarchToContact), nameof(PostSpeed)) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                    _patchedSpeed = Owned(_tSpeed, h.Id, false);
                    _rbmPrefix = Owned(_tSpeed, "com.rbmai", true);
                }
                else why.Append(" brak AdjustSpeedLimit");
            }
            catch (Exception e) { _patchedSpeed = false; why.Append(" Speed ").Append(e.GetType().Name); RoutHold.Stumble(RoutHold.SiteMarch, "MarchToContact.LatkaSpeed", e); }
            int ok = (_patchedCache ? 1 : 0) + (_patchedSpeed ? 1 : 0);
            return ok + "/2 (CacheMovementSpeedOfUnits " + (_patchedCache ? "TAK" : "NIE") + ", AdjustSpeedLimit po com.rbmai " + (_patchedSpeed ? "TAK" : "NIE")
                   + ", prefiks RBM na AdjustSpeedLimit " + (_rbmPrefix ? "TAK" : "NIE") + (why.Length > 0 ? ";" + why.ToString() : "") + ")";
        }

        private static bool Owned(MethodBase target, string id, bool prefix)
        {
            try
            {
                var info = Harmony.GetPatchInfo(target);
                if (info == null) return false;
                var list = prefix ? info.Prefixes : info.Postfixes;
                foreach (var p in list) if (p != null && p.owner == id) return true;
            }
            catch { }
            return false;
        }

        private static int Idx(Formation f)
        {
            if (f == null) return -1;
            var t = f.Team;
            if (t == null) return -1;
            int ti = t.TeamIndex, fi = (int)f.FormationIndex;
            if (ti < 0 || ti >= MaxTeams || fi < 0 || fi >= Stride) return -1;
            return ti * Stride + fi;
        }

        internal static bool IsDragon(Agent mount)   // 148: takze HorsePursuit (smok nie goni i nie jest gonionym) - jedna regula "smok"
        {
            if (mount == null) return false;
            var mo = mount.Monster;
            var id = mo != null ? mo.StringId : null;
            return id != null && id.StartsWith("dragon", StringComparison.Ordinal);
        }

        /// <summary>Latka 1 (watek druzyn albo glowny): CachedMovementSpeed = Min(oryginal, cap marszu). Bez alokacji.</summary>
        private static void PostCache(Formation __instance)
        {
            if (!_on) return;
            try
            {
                int idx = Idx(__instance);
                if (idx < 0) return;
                float cap = _capMove[idx];
                if (cap <= 0f || !ReferenceEquals(_who[idx], __instance)) return;
                if (__instance.CachedMovementSpeed > cap)
                {
                    var set = _setMoveSpeed;
                    if (set != null) { set(__instance, cap); Interlocked.Increment(ref _hitCache); }
                }
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        /// <summary>Latka 2 (watki robocze, co ok. 0.5 s na czlowieka AI w szyku): limit czlowieka i konia nie wyzej niz marsz. Bez alokacji i logu.
        /// 144: szyk bez capu marszu - cap oddechu AiBreath (pieszy AI, ktory oszczedza sily), za wszystkimi wyjsciami (B1).</summary>
        private static void PostSpeed(Agent agent, float desiredSpeed, bool limitIsMultiplier)
        {
            if ((!_on && !AiBreath.On) || agent == null) return;
            try
            {
                var f = agent.Formation;
                if (f == null) return;                                // 144 (B1): bez szyku gra nie liczy limitu - nic nie zostawiamy na stale
                int idx = _on ? Idx(f) : -1;
                float cap = idx >= 0 && ReferenceEquals(_who[idx], f) ? _capLimit[idx] : 0f;
                if (cap > 0f)
                {
                    // 128 / M1: cap marszu (bez zmian; 134c przycina do kroku tylko przy MarchAtWalkingPace)
                    if (agent.IsRunningAway || agent.IsDetachedFromFormation || agent.MissionPeer != null) return;
                    // 148: jezdziec w poscigu (HorsePursuit, cel na czlowieka) - bez capu marszu / odpoczynku koni szyku; szyk w odpoczynku poscigu nie zaczyna,
                    // a zajechany kon jezdzca zawraca (ten sam prog HorseAiRestBelow)
                    if (HorsePursuit.RidingAnyThread(agent)) { HorsePursuit.NoteCapSkip(); return; }
                    var mount = agent.MountAgent;
                    if (mount != null && IsDragon(mount)) { Interlocked.Increment(ref _postDragon); return; }
                    bool catchUp = desiredSpeed < 0f;
                    if (catchUp && agent.IsRetreating()) return;          // OnRetreating tez wola AdjustSpeedLimit(-1)
                    float max = mount != null ? mount.GetMaximumForwardUnlimitedSpeed() : agent.GetMaximumForwardUnlimitedSpeed();
                    if (max <= 0.1f) return;
                    float cur = catchUp ? max : (limitIsMultiplier ? desiredSpeed * max : desiredSpeed);
                    float lim = cap * (catchUp ? CatchUpMul : LineMul);
                    if (_trueWalk && mount == null && !_cav[idx])
                    {
                        float top = catchUp ? WalkCatchUpMax : WalkLineMax;
                        if (lim > top && cur > top) { lim = top; if (catchUp) Interlocked.Increment(ref _hitWalkCatch); else Interlocked.Increment(ref _hitWalk); }
                    }
                    if (cur < lim) lim = cur;
                    if (!_cav[idx] && lim >= cur - 0.01f) return;       // nic do obnizenia - zostaje wpis gry / RBM
                    if (lim < 0.3f) lim = 0.3f;
                    agent.SetMaximumSpeedLimit(lim, false);
                    if (mount != null) mount.SetMaximumSpeedLimit(lim, false);
                    if (catchUp) Interlocked.Increment(ref _hitCatch); else Interlocked.Increment(ref _hitLine);
                    return;
                }
                // 144: oddech AI - tylko pieszy w szyku, nie uciekajacy, nie odlaczony, nie gracz sieci, nie w odwrocie (B1); zapis tylko przy obnizeniu
                if (!AiBreath.On) return;
                if (agent.IsRunningAway || agent.IsDetachedFromFormation || agent.MissionPeer != null || agent.MountAgent != null) return;
                bool cu = desiredSpeed < 0f;
                if (cu && agent.IsRetreating()) return;
                float bc = AiBreath.CapOf(agent);
                if (bc <= 0f) return;
                float mx = agent.GetMaximumForwardUnlimitedSpeed();
                if (mx <= 0.1f) return;
                float now = cu ? mx : (limitIsMultiplier ? desiredSpeed * mx : desiredSpeed);
                if (bc >= now - 0.01f) return;
                agent.SetMaximumSpeedLimit(bc, false);
                AiBreath.NotePost();
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        // ------------------------------------------------------------ misja

        internal static void Begin(Mission m)
        {
            ClearState();
            AiBreath.ClearCaps();   // 144: capy oddechu od zera (FieldCraft.AfterStart robi to samo - kolejnosc zachowan obojetna)
            _m = m != null ? new WeakReference(m) : null;
            _acc = 0f;
            _sampleAcc = 0f;
        }

        internal static void End(Mission m)
        {
            // bez zerowania licznikow (linia NB2 pisze RoutKeeper.OnEndMission - moze po nas); tylko stan i wylacznik latek
            _on = false;
            ClearState();
            AiBreath.ClearCaps();   // 144: latka nie czyta capu oddechu po koncu misji
            _m = null;
        }

        private static void ClearState()
        {
            _on = false;
            for (int i = 0; i < Slots; i++)
            {
                _capMove[i] = 0f; _capLimit[i] = 0f; _cav[i] = false; _who[i] = null;
                _kind[i] = KNone; _freeUntil[i] = 0f; _released[i] = false; _archFree[i] = false; _walk[i] = 0f; _nextScan[i] = 0f;
                _dragon[i] = false; _player[i] = false; _wasCharge[i] = false; _signal[i] = false; _chargeAt[i] = -1000f; _everMarched[i] = false;
                _chargeHeld[i] = false; _rest[i] = false; _capRest[i] = false; _freshUntil[i] = 0f;
            }
            _any = false;
        }

        private static bool Field(Mission m)
        {
            return m != null && m.IsFieldBattle && !m.IsNavalBattle && !m.IsNavalRaidBattle && !m.MissionEnded
                   && m.IsDeploymentFinished && m.Mode != MissionMode.Deployment;
        }

        internal static void Tick(Mission m, float dt)
        {
            if (m == null) return;
            _acc += dt;
            _sampleAcc += dt;
            if (_acc < TickEvery) return;
            _acc = 0f;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) { ClearState(); _m = new WeakReference(m); }
            var c = Settings.Current;
            bool want = c != null && c.MarchToContactEnabled && (_patchedCache || _patchedSpeed) && Field(m);
            if (!want)
            {
                if (_any) ReleaseAll();
                _on = false;
                return;
            }
            _pace = Clamp(c.MarchStepPace, 0.5f, 1.2f);
            _cavPace = Clamp(c.CavalryApproachPace, 0.2f, 1f);
            _gallop = Clamp(c.CavalryGallopDistance, 40f, 250f);
            _archUntil = Clamp(c.ArchersWalkUntil, 40f, 250f);
            _playerMove = c.PlayerMoveOrderWalks;
            _chargeFirst = c.ChargeMarchesFirst;
            _runFrom = Clamp(c.ChargeRunsFrom, 20f, 200f);
            _restBelow = c.HorseStaminaEnabled ? Math.Max(0, Math.Min(80, c.HorseAiRestBelow)) : 0;
            _restUntil = Math.Max(20, Math.Min(100, c.HorseAiRestUntil));
            if (_restUntil <= _restBelow) _restUntil = Math.Min(100, _restBelow + 5);
            _trueWalk = c.MarchAtWalkingPace;   // 144 (134c)
            _on = true;
            float now = m.CurrentTime;
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!MissionTeams.Valid(t)) continue;
                if (t.Side != BattleSideEnum.Attacker && t.Side != BattleSideEnum.Defender) continue;
                int ti = t.TeamIndex;
                if (ti < 0 || ti >= MaxTeams) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf && j < Stride; j++)
                {
                    Formation f = fl[j];
                    if (f == null) continue;
                    int idx = Idx(f);
                    if (idx < 0) continue;
                    try { Eval(m, f, idx, now); }
                    catch (Exception e) { _cStumLocal++; RoutHold.Stumble(RoutHold.SiteMarch, "MarchToContact.Szyk", e); }
                }
            }
            if (_sampleAcc >= SampleEvery)
            {
                _sampleAcc = 0f;
                try { Sample(now); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteMarch, "MarchToContact.Probka", e); }
            }
            Flush(m);
        }

        private static int _cStumLocal;
        // przyrosty z ticku - do licznikow pod Gate raz na takt
        private static int _tInf, _tArch, _tCav, _tPlayer, _tNoSignal, _tContact, _tGallop, _tArchNear, _tOrder, _tRecache, _tDragon;
        private static float _tSumChargeD, _tSumGallopD, _tSumMarchV, _tSumChargeV;
        private static int _tNChargeD, _tNGallopD, _tNMarchV, _tNChargeV;
        private static readonly List<int> _tMarched = new List<int>(16);

        private static void Flush(Mission m)
        {
            if (_tInf + _tArch + _tCav + _tPlayer + _tNoSignal + _tContact + _tGallop + _tArchNear + _tOrder + _tRecache + _tDragon + _tNChargeD + _tNGallopD
                + _tNMarchV + _tNChargeV + _tMarched.Count + _cStumLocal + _tHeld + _tHeldRun == 0) return;
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                _cInf += _tInf; _cArch += _tArch; _cCav += _tCav; _cPlayer += _tPlayer; _cNoSignal += _tNoSignal; _cContact += _tContact; _cGallop += _tGallop;
                _cArchNear += _tArchNear; _cOrder += _tOrder; _cRecache += _tRecache; _cDragon += _tDragon; _cStum += _cStumLocal;
                _cHeld += _tHeld; _cHeldRun += _tHeldRun;
                _sumChargeD += _tSumChargeD; _nChargeD += _tNChargeD; _sumGallopD += _tSumGallopD; _nGallopD += _tNGallopD;
                _sumMarchV += _tSumMarchV; _nMarchV += _tNMarchV; _sumChargeV += _tSumChargeV; _nChargeV += _tNChargeV;
                for (int i = 0; i < _tMarched.Count; i++) _marched.Add(_tMarched[i]);
            }
            _tInf = _tArch = _tCav = _tPlayer = _tNoSignal = _tContact = _tGallop = _tArchNear = _tOrder = _tRecache = _tDragon = _cStumLocal = 0;
            _tHeld = _tHeldRun = 0;
            _tSumChargeD = _tSumGallopD = _tSumMarchV = _tSumChargeV = 0f;
            _tNChargeD = _tNGallopD = _tNMarchV = _tNChargeV = 0;
            _tMarched.Clear();
        }

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }

        /// <summary>Odleglosc od szyku do najblizszego szyku wroga (miedzy medianami minus polowy glebokosci); MaxValue = brak wroga.</summary>
        private static float EnemyGap(Formation f)
        {
            var eq = f.CachedClosestEnemyFormation;
            var ef = eq != null ? eq.Formation : null;
            if (ef == null || ef.CountOfUnits <= 0) return float.MaxValue;
            float d = f.CachedMedianPosition.AsVec2.Distance(ef.CachedMedianPosition.AsVec2) - 0.5f * (f.Depth + ef.Depth);
            return d < 0f ? 0f : d;
        }

        private static void ScanUnit(Agent a)
        {
            if (a == null) return;
            try
            {
                float ws = a.WalkSpeedCached;
                if (ws > 0.3f && ws < _scanWalk) _scanWalk = ws;
                if (!_scanDragon && a.MountAgent != null && IsDragon(a.MountAgent)) _scanDragon = true;
            }
            catch { }
        }

        private static void Eval(Mission m, Formation f, int idx, float now)
        {
            if (!ReferenceEquals(_who[idx], f))
            {
                // nowy szyk w tym miejscu tablicy (nowa druzyna DR) - stan od zera
                _capMove[idx] = 0f; _capLimit[idx] = 0f; _cav[idx] = false;
                _kind[idx] = KNone; _freeUntil[idx] = 0f; _released[idx] = false; _archFree[idx] = false; _walk[idx] = 0f; _nextScan[idx] = 0f;
                _dragon[idx] = false; _wasCharge[idx] = false; _signal[idx] = false; _chargeAt[idx] = -1000f; _everMarched[idx] = false;
                _chargeHeld[idx] = false; _rest[idx] = false; _capRest[idx] = false; _freshUntil[idx] = 0f;
                _who[idx] = f;
            }
            if (f.CountOfUnits <= 0) { Set(f, idx, 0f, 0f, false); return; }

            if (now >= _nextScan[idx])
            {
                _nextScan[idx] = now + ScanEvery;
                _scanWalk = float.MaxValue;
                _scanDragon = false;
                f.ApplyActionOnEachUnit(_scan);
                _walk[idx] = _scanWalk < 10f ? Clamp(_scanWalk, 1.2f, 2.2f) : 1.8f;
                if (_scanDragon && !_dragon[idx]) _tDragon++;
                _dragon[idx] = _scanDragon;
            }

            var q = f.QuerySystem;
            byte kind = q == null ? KNone : (q.IsRangedCavalryFormation ? KHorseArch : (q.IsCavalryFormation ? KCav : (q.IsRangedFormation ? KArch : (q.IsInfantryFormation ? KInf : KNone))));
            _kind[idx] = kind;
            bool player = !f.IsAIControlled;
            _player[idx] = player;
            var oe = f.GetReadonlyMovementOrderReference().OrderEnum;
            bool charge = oe == MovementOrder.MovementOrderEnum.Charge || oe == MovementOrder.MovementOrderEnum.ChargeToTarget;
            bool orderWalk;
            if (player) orderWalk = oe == MovementOrder.MovementOrderEnum.Advance || (oe == MovementOrder.MovementOrderEnum.Move && _playerMove);
            else orderWalk = oe == MovementOrder.MovementOrderEnum.Move || oe == MovementOrder.MovementOrderEnum.Advance || oe == MovementOrder.MovementOrderEnum.FallBack;

            bool marching = _capMove[idx] > 0f;
            float gap = EnemyGap(f);
            if (gap <= ContactGap) _freshUntil[idx] = 0f;   // 144 (B2): pierwsze zwarcie konczy swieza szarze

            // 128: szarza z daleka idzie marszem (piechota, lucznicy pieszo) albo klusem (konnica) do progu biegu; sygnal szarzy (BattleCries.ChargeStarted:
            // okrzyk, rog, motto, zdjecie capu) przychodzi dopiero na progu - ChargeHeld mowi BattleCries, ze jeszcze nie. Gracz i AI tak samo.
            if (_released[idx] && now >= _freeUntil[idx] && gap > Rearm(kind)) _released[idx] = false;
            // 136: odpoczynek konnicy AI liczony w kazdym takcie, PRZED szarza marszem 128 (recenzja: klus podejscia 0.45 x pelna dalej meczy obciazone konie ROT,
            // wiec zmeczony szyk idzie tempem odpoczynku takze przy dalekiej szarzy). Bez ksiegowania przejsc szarzy i bez licznika wejsc w marsz (wlasny licznik
            // w linii 136); sygnal szarzy (okrzyk, rog, zdjecie capu) da BattleCries w tej samej klatce, w ktorej odpoczynek sie konczy (ChargeHeld - RestState na zywo).
            if (RestCav(f, idx, kind, player, charge, orderWalk, gap))
            {
                if (!charge) { _wasCharge[idx] = false; _signal[idx] = false; }   // jak na dole Eval - stan rozkazu prawdziwy po odpoczynku
                RestCap(f, idx);
                return;
            }
            if (charge && HoldCharge(idx, kind, gap, now))
            {
                if (!_chargeHeld[idx]) { _chargeHeld[idx] = true; _tHeld++; }
                MarchCap(f, idx, kind == KArch ? KInf : kind, gap, q);   // lucznicy w szarzy ida do progu biegu jak piechota
                return;
            }
            if (_chargeHeld[idx])
            {
                // prog biegu osiagniety (albo rozkaz zmieniony): bez sygnalu tu - sygnal (i Release) daje BattleCries w tym samym takcie albo zwykla sciezka nizej
                _chargeHeld[idx] = false;
                if (charge) { _tHeldRun++; if (!_signal[idx]) Release(idx, now); }
            }

            // szarza bez sygnalu BattleCries (np. paczka dzwieku nie scalona albo szyk szarzowal przed naszym taktem) - ta sama chwila w ciagu 0.5 s
            if (charge && !_wasCharge[idx])
            {
                if (marching && !_signal[idx]) { _tNoSignal++; NoteChargeDistance(kind, gap); }
                _chargeAt[idx] = now;
                if (gap > ContactGap) _freshUntil[idx] = now + FreshFor;   // 144 (B2)
                Release(idx, now);
            }
            _wasCharge[idx] = charge;
            if (!charge) _signal[idx] = false;

            if (kind == KNone || kind == KHorseArch || _dragon[idx] || charge || gap == float.MaxValue)
            {
                Set(f, idx, 0f, 0f, false);
                return;
            }
            if (!orderWalk)
            {
                if (marching) _tOrder++;
                Set(f, idx, 0f, 0f, false);
                return;
            }
            // zwarcie - wolny (z przytrzymaniem)
            if (gap <= ContactGap)
            {
                if (marching) _tContact++;
                Release(idx, now);
                Set(f, idx, 0f, 0f, false);
                return;
            }
            // po szarzy / zwarciu / galopie: 30 s wolny, potem marsz dopiero, gdy wrog odszedl
            if (_released[idx])
            {
                if (now < _freeUntil[idx] || gap <= Rearm(kind)) { Set(f, idx, 0f, 0f, false); return; }
                _released[idx] = false;
            }

            if (kind == KCav)
            {
                if (gap <= _gallop)
                {
                    if (marching) { _tGallop++; _tSumGallopD += gap; _tNGallopD++; }
                    Release(idx, now);
                    Set(f, idx, 0f, 0f, false);
                    return;
                }
            }
            MarchCap(f, idx, kind, gap, q);
        }

        private static float Rearm(byte kind)
        {
            return kind == KCav ? _gallop + CavRearm : (kind == KArch ? _archUntil + ArcherHyst : InfRearm);
        }

        /// <summary>128: czy szyk z rozkazem szarzy ma jeszcze isc marszem (klusem) - wrog dalej niz prog biegu (piechota i lucznicy ChargeRunsFrom,
        /// konnica CavalryGallopDistance), szyk nie zwolniony po zwarciu. Konni lucznicy, smoki, szyki bez rodzaju - nigdy.</summary>
        private static bool HoldCharge(int idx, byte kind, float gap, float now)
        {
            if (!_chargeFirst || gap == float.MaxValue) return false;
            if (kind != KInf && kind != KArch && kind != KCav) return false;
            if (_dragon[idx]) return false;
            if (_released[idx] && (now < _freeUntil[idx] || gap <= Rearm(kind))) return false;
            return gap > (kind == KCav ? _gallop : _runFrom);
        }

        /// <summary>136: odpoczynek szyku konnicy AI (nie konni lucznicy, nie gracz, nie smok; rozkaz szarzy albo marszu): wejscie, gdy srednia S koni szyku
        /// (HorseWind.AvgStamina) spadnie ponizej HorseAiRestBelow, a wrog jest dalej niz RestNear; wyjscie, gdy srednia wroci do HorseAiRestUntil albo wrog
        /// blizej niz RestNear (wtedy szarza / galop jak dotad - "walczy o zycie"); ponowne wejscie dopiero ponad RestNear + RestRearm. Brak danych (stamina koni wylaczona, szyk bez liczonych koni) = bez odpoczynku.</summary>
        private static bool RestCav(Formation f, int idx, byte kind, bool player, bool charge, bool orderWalk, float gap)
        {
            bool rest = RestState(f, idx, kind, player, charge, orderWalk, gap, _rest[idx]);
            if (rest && !_rest[idx]) HorseWind.NoteAiRest();   // wejscie w odpoczynek = odpoczynek naprawde jedzie (galaz rest w Eval jest przed 128)
            _rest[idx] = rest;
            return rest;
        }

        /// <summary>136: regula odpoczynku bez skutkow ubocznych - Eval (RestCav) i ChargeHeld (BattleCries, ta sama klatka PRZED Eval) licza to samo.
        /// cur = szyk juz odpoczywa (wyjscie: srednia &gt;= HorseAiRestUntil albo wrog blizej niz RestNear); inaczej wejscie (ponizej HorseAiRestBelow, wrog dalej niz RestNear + RestRearm).</summary>
        private static bool RestState(Formation f, int idx, byte kind, bool player, bool charge, bool orderWalk, float gap, bool cur)
        {
            if (kind != KCav || player || _dragon[idx] || (!charge && !orderWalk) || gap == float.MaxValue || _restBelow <= 0) return false;
            float avg = HorseWind.AvgStamina(f);
            if (avg < 0f) return false;
            return cur ? (avg < _restUntil && gap >= RestNear) : (avg < _restBelow && gap > RestNear + RestRearm);
        }

        /// <summary>136 (recenzja): cap odpoczynku - tempo odpoczynku koni szyku z HorseWind (step / lekki klus wedlug obciazenia, g = 0.5), podzielone przez margines
        /// linii (latka AdjustSpeedLimit wpisuje koniowi cap x LineMul), 1.5..9 m/s. Bez licznika wejsc w marsz (linia 136 ma wlasny).</summary>
        private static void RestCap(Formation f, int idx)
        {
            float v = HorseWind.RestSpeed(f);
            if (v < 0.5f) v = 3.6f;   // brak danych w tym takcie (odpoczynek wymaga sredniej z tego samego taktu) - klus regulaminowy 3.6 m/s
            float cap = Clamp(v / LineMul, 1.5f, 9f);
            Set(f, idx, cap, cap, true, false);
        }

        /// <summary>148 (poscig konnicy AI): szyk konnicy AI daje teraz koniom odsapnac (136, RestCav) - poscig go nie zaczyna. Glowny watek.</summary>
        internal static bool Resting(Formation f)
        {
            if (!_on || f == null) return false;
            int idx = Idx(f);
            return idx >= 0 && ReferenceEquals(_who[idx], f) && _rest[idx];
        }

        /// <summary>128: dla BattleCries przed sygnalem szarzy (glowny watek): true = szyk idzie jeszcze marszem, sygnal (okrzyk, rog, bieg) pozniej.
        /// 136: takze konnica AI, ktora daje koniom odsapnac - liczone na zywo (RestState z biezacym gap i srednia), bo BattleCries biegnie w klatce PRZED Eval:
        /// stan z poprzedniego Eval wstrzymywal sygnal w klatce konca odpoczynku i szarza liczyla sie jako "bez sygnalu" (recenzja 136).</summary>
        internal static bool ChargeHeld(Formation f)
        {
            if (!_on || f == null) return false;
            var m = Mission.Current;
            if (m == null) return false;
            int idx = Idx(f);
            if (idx < 0 || !ReferenceEquals(_who[idx], f)) return false;
            float gap = EnemyGap(f);
            if (HoldCharge(idx, _kind[idx], gap, m.CurrentTime)) return true;
            return RestState(f, idx, _kind[idx], !f.IsAIControlled, true, false, gap, _rest[idx]);
        }

        /// <summary>Tempo marszu szyku: konnica klus, lucznicy do ArchersWalkUntil, piechota krok (dalej niz 250 m szybszy krok); pod ostrzalem tylko tempo szyku.</summary>
        private static void MarchCap(Formation f, int idx, byte kind, float gap, FormationQuerySystem q)
        {
            bool marching = _capMove[idx] > 0f;
            if (kind == KCav)
            {
                float full = 0f;
                try { full = f.CachedFormationIntegrityData.AverageMaxUnlimitedSpeedExcludeFarAgents; } catch { full = 0f; }
                if (full < 2f) { try { full = q.MovementSpeedMaximum; } catch { full = 0f; } }
                if (full < 2f) full = 10f;
                float cav = Clamp(_cavPace * full, 2.5f, 9f);
                Set(f, idx, cav, cav, true);
                return;
            }

            if (kind == KArch)
            {
                if (_archFree[idx]) { if (gap > _archUntil + ArcherHyst) _archFree[idx] = false; }
                else if (gap <= _archUntil) { _archFree[idx] = true; if (marching) _tArchNear++; }
                if (_archFree[idx]) { Set(f, idx, 0f, 0f, false); return; }
            }

            float pace = _walk[idx] > 0f ? _walk[idx] : 1.8f;
            pace *= _pace;
            if (gap > FarFrom) pace = Math.Max(pace, Math.Min(pace * FarMul, FarPaceMax));
            pace = Clamp(pace, 0.9f, 2.7f);
            if (_trueWalk && pace > WalkPace) pace = WalkPace;   // 144 (134c, W3): szyk krokiem ok. 1.6 m/s - utrzymanie linii (x1.1) miesci sie w 1.75
            bool underFire = false;
            if (kind == KInf) { try { underFire = q.UnderRangedAttackRatio >= 0.33f; } catch { underFire = false; } }
            Set(f, idx, pace, underFire ? 0f : pace, false);
        }

        private static void NoteChargeDistance(byte kind, float gap)
        {
            if (gap == float.MaxValue) return;
            if (kind == KCav) { _tSumGallopD += gap; _tNGallopD++; }
            else { _tSumChargeD += gap; _tNChargeD++; }
        }

        private static void Release(int idx, float now)
        {
            _released[idx] = true;
            float until = now + FreeHold;
            if (_freeUntil[idx] < until) _freeUntil[idx] = until;
        }

        /// <summary>Wpis capu szyku; przy zmianie (wlaczenie, zdjecie, zmiana tempa o ponad 0.05 m/s) od razu przeliczenie CachedMovementSpeed.</summary>
        private static void Set(Formation f, int idx, float capMove, float capLimit, bool cav, bool count = true)
        {
            float old = _capMove[idx];
            bool changed = (old > 0f) != (capMove > 0f) || Math.Abs(old - capMove) > 0.05f;
            _capLimit[idx] = capLimit;
            _cav[idx] = cav && capLimit > 0f;
            _capMove[idx] = capMove;
            bool wasRest = _capRest[idx];
            _capRest[idx] = capMove > 0f && !count;
            if (capMove > 0f)
            {
                _any = true;
                // count = false: odpoczynek konnicy 136 (recenzja - nie wchodzi do "wejsc w marsz: konnica" ani "szykow w marszu"); marsz zaraz po odpoczynku - tak
                if ((old <= 0f || wasRest) && count)
                {
                    byte k = _kind[idx];
                    if (_player[idx]) _tPlayer++;
                    else if (k == KInf) _tInf++;
                    else if (k == KArch) _tArch++;
                    else if (k == KCav) _tCav++;
                    if (!_everMarched[idx]) { _everMarched[idx] = true; _tMarched.Add(idx); }
                }
            }
            if (changed) Recache(f);
        }

        private static void Recache(Formation f)
        {
            var rc = _recache;
            if (rc == null || f == null || f.CountOfUnits <= 0) return;
            try { rc(f); _tRecache++; }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteMarch, "MarchToContact.Recache", e); }
        }

        /// <summary>Klucz wylaczony w trakcie bitwy albo koniec warunkow: wszystkie capy zdjete i tempo szykow przeliczone od razu.</summary>
        private static void ReleaseAll()
        {
            for (int i = 0; i < Slots; i++)
            {
                var f = _who[i];
                bool had = _capMove[i] > 0f;
                _capMove[i] = 0f; _capLimit[i] = 0f; _cav[i] = false;
                if (had && f != null) Recache(f);
            }
            _any = false;
        }

        private static void SampleUnit(Agent a)
        {
            if (_sampleLeft <= 0 || a == null) return;
            try
            {
                if (!a.IsActive() || a.IsRunningAway) return;
                float v = (a.MountAgent ?? a).GetCurrentVelocity().Length;
                if (v < 0.3f) return;   // stoi (rozstawiony, czeka) - nie mowi nic o tempie
                _sampleSum += v;
                _sampleN++;
                _sampleLeft--;
            }
            catch { }
        }

        /// <summary>Probka predkosci co 2 s: do 20 ludzi w marszu (piechota i lucznicy pieszo) i do 20 w swiezej szarzy (15 s od rozkazu).</summary>
        private static void Sample(float now)
        {
            int march = 20, chg = 20;
            for (int i = 0; i < Slots && (march > 0 || chg > 0); i++)
            {
                var f = _who[i];
                if (f == null || f.CountOfUnits <= 0) continue;
                byte k = _kind[i];
                if (k != KInf && k != KArch) continue;
                bool m = _capMove[i] > 0f, c = now - _chargeAt[i] <= ChargeSampleFor && _wasCharge[i];
                if (!m && !c) continue;
                _sampleLeft = Math.Min(5, m ? march : chg);
                if (_sampleLeft <= 0) continue;
                _sampleSum = 0f; _sampleN = 0;
                f.ApplyActionOnEachUnit(_sample);
                if (_sampleN <= 0) continue;
                if (m) { _tSumMarchV += _sampleSum; _tNMarchV += _sampleN; march -= _sampleN; }
                else { _tSumChargeV += _sampleSum; _tNChargeV += _sampleN; chg -= _sampleN; }
            }
        }

        // ------------------------------------------------------------ kontrakty

        /// <summary>Szyk idzie teraz marszem (ma cap). Glowny watek.</summary>
        internal static bool IsMarching(Formation f)
        {
            if (!_on) return false;
            int idx = Idx(f);
            return idx >= 0 && ReferenceEquals(_who[idx], f) && _capMove[idx] > 0f;
        }

        /// <summary>Gorny limit marszu czlowieka AI w m/s (cap x 1.1, ten sam co w latce AdjustSpeedLimit); -1 = brak capu. Glowny watek (FieldCraft, M0).
        /// 144: bez capu marszu - cap oddechu (AiBreath, krok 1.75 m/s), zeby kara FieldCraft nie podnosila limitu ponad krok (punkt 4.3); -1 takze bez szyku,
        /// w odwrocie, u konnego i odlaczonego (B1). 134c: przy MarchAtWalkingPace cap marszu pieszego najwyzej WalkLineMax.</summary>
        internal static float CapFor(Agent a)
        {
            bool breath;
            return CapFor(a, out breath);
        }

        internal static float CapFor(Agent a, out bool breath)
        {
            breath = false;
            if (a == null || !a.IsAIControlled || a.IsRunningAway) return -1f;
            var f = a.Formation;
            if (_on)
            {
                int idx = Idx(f);
                if (idx >= 0 && ReferenceEquals(_who[idx], f))
                {
                    float cap = _capLimit[idx];
                    if (cap > 0f)
                    {
                        float lim = cap * LineMul;
                        if (_trueWalk && !_cav[idx] && a.MountAgent == null && lim > WalkLineMax) lim = WalkLineMax;
                        return lim;
                    }
                }
            }
            if (!AiBreath.On || f == null || a.MountAgent != null || a.IsDetachedFromFormation || a.MissionPeer != null) return -1f;
            try { if (a.IsRetreating()) return -1f; } catch { return -1f; }
            float bc = AiBreath.CapOf(a);
            if (bc <= 0f) return -1f;
            breath = true;
            return bc;
        }

        /// <summary>144 (B2): szyk w swiezej szarzy - od sygnalu (OnChargeStarted albo rozkaz szarzy w ticku) do pierwszego zwarcia (wrog 25 m od szyku),
        /// najwyzej FreshFor sekund; AiBreath nie trzyma wtedy nikogo krokiem (najciezsi szliby 2-3 s po rogu). Bez marszu (klucz NIE) - zawsze false. Glowny watek.</summary>
        internal static bool ChargeFresh(Formation f)
        {
            if (!_on || f == null) return false;
            var m = Mission.Current;
            if (m == null) return false;
            int idx = Idx(f);
            if (idx < 0 || !ReferenceEquals(_who[idx], f)) return false;
            return _wasCharge[idx] && m.CurrentTime < _freshUntil[idx];
        }

        /// <summary>144: sekundy od poczatku szarzy szyku (sygnal albo rozkaz w ticku); -1 = nie wiadomo (marsz nieczynny, szyk bez szarzy). Glowny watek.</summary>
        internal static float ChargeAge(Formation f)
        {
            if (!_on || f == null) return -1f;
            var m = Mission.Current;
            if (m == null) return -1f;
            int idx = Idx(f);
            if (idx < 0 || !ReferenceEquals(_who[idx], f) || !_wasCharge[idx]) return -1f;
            return m.CurrentTime - _chargeAt[idx];
        }

        /// <summary>144: latka AdjustSpeedLimit wpieta (cap oddechu dziala tylko przez nia).</summary>
        internal static bool SpeedPatched { get { return _patchedSpeed; } }

        /// <summary>144 (134c): zapisy limitu przyciete do kroku i dogonienia przyciete do 2.1 m/s w tej misji (do linii "Oddech AI (144)").</summary>
        internal static int WalkHits { get { return Volatile.Read(ref _hitWalk); } }
        internal static int WalkCatchHits { get { return Volatile.Read(ref _hitWalkCatch); } }

        /// <summary>Szyk przeszedl na szarze (sygnal z BattleCries.ChargeStarted, glowny watek, ten sam takt): cap zdjety i tempo przeliczone od razu.</summary>
        internal static void OnChargeStarted(Formation f)
        {
            var m = Mission.Current;
            if (m == null) return;
            lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _cSignals++; }
            int idx = Idx(f);
            if (idx < 0 || !ReferenceEquals(_who[idx], f)) return;
            float now = m.CurrentTime;
            _signal[idx] = true;
            _wasCharge[idx] = true;
            _chargeAt[idx] = now;
            bool marching = _capMove[idx] > 0f;
            Release(idx, now);
            float gap = EnemyGap(f);
            _freshUntil[idx] = gap > ContactGap ? now + FreshFor : 0f;   // 144 (B2): od sygnalu do pierwszego zwarcia, najwyzej 20 s
            if (!marching) return;
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                _cSignal++;
                if (gap != float.MaxValue)
                {
                    if (_kind[idx] == KCav) { _sumGallopD += gap; _nGallopD++; }
                    else { _sumChargeD += gap; _nChargeD++; }
                }
            }
            Set(f, idx, 0f, 0f, false);
            Flush(m);
        }

        // ------------------------------------------------------------ linia misji

        internal static void ResetCounters()
        {
            _cInf = _cArch = _cCav = _cPlayer = _cSignal = _cNoSignal = _cContact = _cGallop = _cArchNear = _cOrder = _cRecache = _cDragon = _cStum = 0;
            _cSignals = 0;
            _cHeld = _cHeldRun = 0;
            _sumChargeD = _sumGallopD = _sumMarchV = _sumChargeV = 0f;
            _nChargeD = _nGallopD = _nMarchV = _nChargeV = 0;
            Interlocked.Exchange(ref _hitLine, 0);
            Interlocked.Exchange(ref _hitCatch, 0);
            Interlocked.Exchange(ref _hitCache, 0);
            Interlocked.Exchange(ref _postErr, 0);
            Interlocked.Exchange(ref _postDragon, 0);
            Interlocked.Exchange(ref _hitWalk, 0);       // 144 (134c)
            Interlocked.Exchange(ref _hitWalkCatch, 0);
            _marched.Clear();
            FieldCraft.ResetSpeedCounters();
        }

        internal static int Activity { get { return _cInf + _cArch + _cCav + _cPlayer + _cSignal + _cNoSignal + _cContact + _cGallop + _cArchNear + _cStum; } }

        private static string Avg(float sum, int n, string fmt) { return n > 0 ? (sum / n).ToString(fmt, System.Globalization.CultureInfo.InvariantCulture) : "-"; }

        /// <summary>Segment ' | marsz ...' (bez znaku '|' w srodku - wzor NBW_LINE w sprawdz_logi.py). Wolajacy trzyma RoutHold.Gate.</summary>
        internal static void Append(StringBuilder sb, Settings c)
        {
            sb.Append(" | marsz ").Append(c != null && c.MarchToContactEnabled ? "TAK" : "NIE")
              .Append(" (szarza marszem ").Append(c != null && c.ChargeMarchesFirst ? "TAK" : "NIE").Append(", bieg od ")
              .Append(c != null ? Clamp(c.ChargeRunsFrom, 20f, 200f).ToString("0") : "-").Append(" m: szarz zaczetych marszem ").Append(_cHeld)
              .Append(", doprowadzonych do progu ").Append(_cHeldRun).Append(")")
              .Append(": szykow w marszu ").Append(_marched.Count)
              .Append(" (wejsc w marsz: piechota ").Append(_cInf).Append(", lucznicy ").Append(_cArch).Append(", konnica ").Append(_cCav).Append(", gracza ").Append(_cPlayer)
              .Append("), przejsc do biegu ").Append(_cSignal + _cNoSignal + _cContact + _cGallop + _cArchNear)
              .Append(" (szarza z sygnalu ").Append(_cSignal).Append(", szarza bez sygnalu ").Append(_cNoSignal).Append(", zwarcie ").Append(_cContact)
              .Append(", galop ").Append(_cGallop).Append(", lucznicy blisko wroga ").Append(_cArchNear).Append("; zmiana rozkazu ").Append(_cOrder)
              .Append(", sygnalow szarzy ").Append(_cSignals)
              .Append("), sr. dystans szarzy piechoty ").Append(Avg(_sumChargeD, _nChargeD, "0")).Append(" m (").Append(_nChargeD)
              .Append("), galopu konnicy ").Append(Avg(_sumGallopD, _nGallopD, "0")).Append(" m (").Append(_nGallopD)
              .Append("), predkosc w marszu sr. ").Append(Avg(_sumMarchV, _nMarchV, "0.00")).Append(" m/s (").Append(_nMarchV)
              .Append("), w szarzy ").Append(Avg(_sumChargeV, _nChargeV, "0.00")).Append(" m/s (").Append(_nChargeV)
              .Append("); latki ").Append((_patchedCache ? 1 : 0) + (_patchedSpeed ? 1 : 0)).Append("/2: tempo szyku ").Append(Volatile.Read(ref _hitCache))
              .Append(", limit ").Append(Volatile.Read(ref _hitLine)).Append(" (dogonienie ").Append(Volatile.Read(ref _hitCatch)).Append("), bledy latek ")
              .Append(Volatile.Read(ref _postErr)).Append(", przeliczen tempa ").Append(_cRecache).Append(", szykow ze smokiem ").Append(_cDragon)
              .Append(" (ludzi na smoku pominieto ").Append(Volatile.Read(ref _postDragon)).Append("), potkniec szyku ").Append(_cStum);
            FieldCraft.AppendSpeed(sb, c);
        }
    }
}
