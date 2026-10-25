using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 148 (25.10, fala "AI walczy o zycie", karta syntezy "143 Poscig konnicy AI", decyzja Jeffa D5: "Tak, z limitem" - konnica AI, 60 s / 250 m, noca
    /// polowa, nie formacje gracza). Jeff 13:30: "w panice jest zasada uciekaj i brak rozsadku ... Wtedy ginie najwiecej osob."
    /// Przyczyna (dekompilacje scratchpad/shield RBMAI_all.txt, ai_run, panB): nikt nie goni uciekajacych.
    ///  - RBMAI.Utilities.NearestAgentFromMultipleFormations (Utilities:492-555) pomija IsRunningAway, a GetCorrectTarget (:408-464) szuka celu tylko w
    ///    szykach wroga; Frontline (PrefixGetOrderPositionOfUnit:485-502) kieruje szyk konny AI z ChargeWithTarget na najblizszego NIE uciekajacego wroga,
    ///    gdy jest dalej niz 60 m;
    ///  - uciekajacy w ogole nie maja szyku: Mission.OnAgentFleeing -> Agent.OnFleeing (Agent.cs:5128-5136) robi Formation = null. Dlatego postfiks na
    ///    NearestAgentFromMultipleFormations (jedna z dwoch drog z karty) NIC by nie dal - przechodzi tylko po szykach, a spanikowanego w nich nie ma;
    ///  - RBMBehaviorCavalryCharge (:300-420) po przejezdzie przez cel daje Move na punkt przegrupowania (ChargingPast / Reforming) - szyk w Move jedzie na
    ///    swoje miejsca, nie za uciekajacymi.
    /// Wskazanie celu (druga droga z karty, sprawdzona w dekompilacji 1.4.8): cel na czlowieka - Agent.SetAutomaticTargetSelection(false) + SetTargetAgent
    /// (Agent.cs:2878-2886; ten sam wzor RBM AgentAi:617-629 dla chorazych i NavalDLC) oraz ruch za celem przez Agent.SetScriptedPosition(cel.GetWorldPosition())
    /// odswiezane co 0.25 s - tak gra sama prowadzi czlowieka za innym (ScriptedMovementComponent.OnTick:58-59 - SetScriptedPosition pozycji celu co klatke);
    /// bez flagi NoAttack (atak dozwolony), bez DoNotRun (galop). Ta sama droga co postoj rannego 143 (SetScriptedPosition / DisableScriptedMovement).
    ///
    /// Regula (logika misji: decyzje co 1 s, cel jezdzca co 0.25 s; tylko bitwa polowa - IsFieldBattle, rodzaj "bitwa", bez morza):
    ///  - szyk konnicy albo konnych lucznikow AI (QuerySystem.IsCavalryFormation / IsRangedCavalryFormation), nie formacja gracza (rozkazy gracza, gracz
    ///    w szyku, gracz wodzem druzyny, gracz sierzantem szyku) - takze armie sojusznikow AI i AI w druzynie gracza, gdy wodzem jest AI;
    ///  - strona zlamala wroga: u wroga co najmniej 3 ludzi w PANICE (przelacznik 139, SurvivalMode.InPanic) i odsetek PANIKI wroga wiekszy niz wlasny;
    ///  - co najmniej 3 uciekajacych wroga w 150 m od szyku i zadnej nietknietej formacji wroga (szyk od 8 ludzi - uciekajacy nie maja szyku) w 80 m;
    ///  - konie szyku nie zajechane (136: HorseWind.AvgStamina nie ponizej HorseAiRestBelow; szyk w odpoczynku koni MarchToContact - nie zaczyna);
    ///  - szyk nie dalej niz 250 m od glownej sily (najwiekszy pieszy szyk druzyny; bez piechoty - miejsce wejscia w poscig);
    ///  - po rozstrzygnieciu bitwy (MissionEnded) nowy poscig nie zaczyna sie - trwajacy biegnie do limitow, jezdzcy wybrani przez gre do wiwatu wracaja;
    ///  - jezdzcy: AI, konno, nie bohater (krok 149 - ostrozni pozycyjnie), nie chorazy (RBM sam steruje ich celem), nie smok, nie umarly, nie odlaczony,
    ///    nie w PANICE ani w odwrocie rannego 143; najwyzej 2 jezdzcow na jednego uciekajacego (reszta zostaje w szyku); uciekajacy konno liczony jak 2x dalej;
    ///  - jezdziec wraca do szyku (DisableScriptedMovement, automatyczny wybor celu), gdy: ponad 250 m od glownej sily, nietknieta formacja wroga w 80 m,
    ///    jego kon zajechany (HorseWind.StaminaOf ponizej HorseAiRestBelow), panika / odwrot / wiwat; cel padl albo uszedl - nowy cel od razu albo powrot;
    ///  - koniec poscigu szyku: 60 s, 3 s bez uciekajacych, konie szyku, gracz przejal szyk, szyk pusty; nastepny poscig szyku najwczesniej po 30 s;
    ///  - noca (Campaign.IsNight - czas kampanii stoi w misji, jak DrMarchArrival) 30 s i 125 m.
    /// Zgodnosc: 136 - MarchToContact nie zaklada capu marszu / odpoczynku jezdzcowi w poscigu (PostSpeed: RidingAnyThread), a szyk w odpoczynku koni nie
    /// zaczyna; HorseWind dalej spowalnia zmeczonego konia (MountSpeed). FF - nie zmieniamy: liczymy trafienia wlasnej strony w jezdzcow w poscigu (pociski /
    /// wrecz). 143 - zwolnienie nie zdejmuje pozycji skryptowej czlowiekowi na postoju rannego (Withdrawing bez IsRunningAway).
    /// Klucz NIE: ta sama ocena na sucho (wejscia i szyko-sekundy), bez sterowania jezdzcami. Linia "poscig (148)" raz na bitwe (BattleSense.Clean).
    /// Watki: wszystko na glownym watku (OnMissionTick, OnAgentRemoved, OnAgentHit - callbacki MBCallback glownego watku); latka PostSpeed (watki robocze)
    /// czyta tylko tablice _riding wedlug Agent.Index (wzor RoutHold.KnownAnyThread). Wyjatek na jezdzcu = licznik; po 5 jezdziec wypada z poscigu (bez
    /// globalnego wylacznika). Nazwa klasy nie moze byc "Log", "Settings" ani "McmSettings".
    /// </summary>
    internal static class HorsePursuit
    {
        // ------------------------------------------------------------ stale (karta syntezy 143 / D5)
        internal const float PassEvery = 1f;        // decyzje co 1 s
        internal const float TrackEvery = 0.25f;    // cel jezdzca (pozycja skryptowa) co 0.25 s
        internal const float SeekRadius = 150f;     // uciekajacy w promieniu 150 m
        internal const float IntactRadius = 80f;    // nietknieta formacja wroga w 80 m - bez poscigu
        internal const int IntactMin = 8;           // nietknieta formacja = szyk od tylu ludzi
        internal const float MaxSeconds = 60f;      // limit czasu poscigu szyku
        internal const float MaxFromMain = 250f;    // limit odleglosci od glownej sily
        internal const float NightMul = 0.5f;       // noca polowa
        internal const int MinFleeing = 3;          // tylu uciekajacych w 150 m, zeby zaczac
        internal const int PerVictim = 2;           // najwyzej tylu jezdzcow na uciekajacego
        internal const float Cooldown = 30f;        // nastepny poscig szyku najwczesniej po tylu sekundach
        internal const float QuietEnd = 3f;         // tyle sekund bez uciekajacych i bez gonacych - koniec poscigu
        private const float RadiusCap = 40f;        // promien szyku do odleglosci "w 80 m" najwyzej tyle
        private const float FarMargin = 10f;        // cel najwyzej (limit - 10 m) od glownej sily - bez migania na granicy
        private const int TrackPerStep = 96;        // najwyzej tyle odswiezen celu na krok 0.25 s
        private const int FailsOut = 5;             // potkniec na jezdzcu, po ktorych wypada z poscigu
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private const byte RFree = 0, RChase = 1, ROut = 2;
        // jezdziec zawrocony
        private const int OFar = 0, OIntact = 1, OHorse = 2, OPanic = 3, OCheer = 4, OOther = 5, OKinds = 6;
        // koniec poscigu szyku
        private const int ETime = 0, EQuiet = 1, EHorses = 2, EPlayer = 3, EEmpty = 4, EMission = 5, EKey = 6, EKinds = 7;
        // nie zaczal (przypadki w passach, gdy uciekajacy byli w 150 m)
        private const int NIntact = 0, NHorses = 1, NRest = 2, NFar = 3, NNotBroken = 4, NEnded = 5, NCool = 6, NKinds = 7;
        // wykluczeni jezdzcy (rozni ludzie)
        private const int XHero = 1, XBanner = 2, XDragon = 3, XDead = 4, XPlayer = 5, XKinds = 6;

        private sealed class Rider
        {
            public Agent A;
            public Formation F;
            public FormSt S;
            public Agent V;          // uciekajacy - cel
            public byte St;          // RFree / RChase / ROut
            public int Fails;
        }

        private sealed class FormSt
        {
            public Formation F;
            public bool Active, Dry, Seen, Night;
            public float Start, QuietSince = -1f, CoolUntil = -1000f;
            public Vec2 StartPos, Anchor;
            public bool HasMain;
            public int Side = -1, Enemy = -1;
            public readonly List<Rider> Riders = new List<Rider>();
        }

        // ------------------------------------------------------------ stan misji (glowny watek)
        private static WeakReference _m;
        private static bool _live, _key, _night, _written, _field, _naval;
        private static string _kind = "?";
        private static float _passAcc, _trackAcc, _maxSec = MaxSeconds, _maxFrom = MaxFromMain;
        private static int _restBelow, _rot;
        private static readonly Dictionary<Formation, FormSt> Forms = new Dictionary<Formation, FormSt>();
        private static readonly Dictionary<Agent, Rider> Riders = new Dictionary<Agent, Rider>();
        private static readonly List<Rider> All = new List<Rider>();
        private static readonly Dictionary<Agent, int> Chasers = new Dictionary<Agent, int>();
        private static readonly HashSet<Agent> EverChased = new HashSet<Agent>();
        private static readonly List<Agent>[] Flee = { new List<Agent>(), new List<Agent>() };
        private static readonly List<Vec2>[] FleePos = { new List<Vec2>(), new List<Vec2>() };
        private static readonly List<Vec2>[] IntactPos = { new List<Vec2>(), new List<Vec2>() };
        private static readonly List<float>[] IntactR = { new List<float>(), new List<float>() };
        private static readonly int[] Panic = new int[2], Alive = new int[2];
        private static readonly List<Agent> Units = new List<Agent>();
        private static readonly Action<Agent> Collect = a => Units.Add(a);
        private static readonly List<FormSt> EndList = new List<FormSt>();

        // ---- dla latki PostSpeed (watki robocze): jezdziec w poscigu wedlug Agent.Index; pisze tylko glowny watek (wzor RoutHold._known)
        private static Agent[] _riding;
        private const int RidingMaxIndex = 65536;

        // ------------------------------------------------------------ liczniki misji (glowny watek; _capSkip przez Interlocked)
        private static int _episodes, _assign, _retarget, _sets, _budgetHit, _ridersLost, _dropped;
        private static float _epSum, _epMax, _maxFar;
        private static int _killed, _ko, _escaped, _fellOthers, _byRiderAll;
        private static int _ffMissile, _ffMelee;
        private static long _ffDmg;
        private static int _capSkip;
        private static int _dryEpisodes;
        private static float _drySec;
        private static readonly int[] EndWhy = new int[EKinds];
        private static readonly int[] OutWhy = new int[OKinds];
        private static readonly int[] NoStart = new int[NKinds];
        private static readonly int[] Excluded = new int[XKinds];
        private static readonly HashSet<int> ExSeen = new HashSet<int>();
        private static readonly HashSet<int> EverRiders = new HashSet<int>();
        private static readonly HashSet<int> EverVictims = new HashSet<int>();
        private static readonly HashSet<Formation> PursuedForms = new HashSet<Formation>();
        private static readonly HashSet<Formation> DryForms = new HashSet<Formation>();
        private static readonly HashSet<Formation> PlayerSkipped = new HashSet<Formation>();
        private static int _formsPlayerSide, _formsFoe, _formsCav, _formsHa;
        private static readonly int[,] Lost = new int[2, 4];   // [strona 0 obrona / 1 atak] x [zabici, nieprzytomni, uszli, inne]

        // ------------------------------------------------------------ dla innych paczek

        /// <summary>MarchToContact.PostSpeed (watki robocze): czlowiek jest jezdzcem w poscigu (148) - bez capu marszu / odpoczynku. Bez blokady.</summary>
        internal static bool RidingAnyThread(Agent a)
        {
            if (a == null) return false;
            var k = Volatile.Read(ref _riding);
            if (k == null) return false;
            int i = a.Index;
            return i >= 0 && i < k.Length && ReferenceEquals(k[i], a);
        }

        /// <summary>MarchToContact.PostSpeed: cap marszu pominiety u jezdzca w poscigu (licznik linii).</summary>
        internal static void NoteCapSkip() { Interlocked.Increment(ref _capSkip); }

        private static void MarkRiding(Agent a, bool on)
        {
            if (a == null) return;
            int i = a.Index;
            if (i < 0 || i >= RidingMaxIndex) return;
            var k = _riding;
            if (k == null || i >= k.Length)
            {
                if (!on) return;
                int n = k == null ? 1024 : k.Length;
                while (n <= i) n *= 2;
                var nk = new Agent[n];
                if (k != null) Array.Copy(k, nk, k.Length);
                nk[i] = a;
                Volatile.Write(ref _riding, nk);
                return;
            }
            if (on) k[i] = a;
            else if (ReferenceEquals(k[i], a)) k[i] = null;
        }

        private static void Stumble(string where, Exception e)
        {
            RoutHold.Stumble(RoutHold.SitePursuit, where, e);
        }

        // ------------------------------------------------------------ wejscia z BattleSense (glowny watek)

        /// <summary>BattleSense.AfterStart w bitwie: stan od zera, bitwa polowa, pora dnia.</summary>
        internal static void Begin(Mission m)
        {
            Clear();
            _live = false;
            if (m == null) return;
            _m = new WeakReference(m);
            _written = false;
            _kind = MissionTeams.Kind(m);
            _naval = false;
            try { _naval = m.IsNavalBattle || m.IsNavalRaidBattle; } catch { }
            _field = false;
            try { _field = m.IsFieldBattle && _kind == "bitwa" && !_naval; } catch { }
            _night = false;
            try { _night = Campaign.Current != null && Campaign.Current.IsNight; } catch { }   // czas kampanii stoi w misji - pora bitwy
            _maxSec = MaxSeconds * (_night ? NightMul : 1f);
            _maxFrom = MaxFromMain * (_night ? NightMul : 1f);
            var c = Settings.Current;
            _key = c != null && c.AiCavalryPursues;
            _live = _field;
        }

        /// <summary>BattleSense.OnMissionTick (blok Night, po CloseRanks): decyzje co 1 s, cel jezdzcow co 0.25 s.</summary>
        internal static void Tick(Mission m, float dt)
        {
            if (!_live || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;
            bool deployed = false;
            try { deployed = m.IsDeploymentFinished; } catch { }
            if (!deployed) return;
            float now = m.CurrentTime;
            _passAcc += dt;
            _trackAcc += dt;
            if (_passAcc >= PassEvery)
            {
                _passAcc = 0f;
                _trackAcc = 0f;
                Pass(m, now);
            }
            else if (_trackAcc >= TrackEvery)
            {
                _trackAcc = 0f;
                Track(now);
            }
        }

        /// <summary>BattleSense.OnAgentHit (blok Night): trafienie jezdzca w poscigu (albo jego konia) przez wlasna strone - FF na wlasnej jezdzie.</summary>
        internal static void OnHit(Agent victim, Agent affector, in Blow blow)
        {
            if (!_live || victim == null || affector == null || Riders.Count == 0) return;
            try
            {
                Agent rid = victim.IsMount ? victim.RiderAgent : victim;
                Rider r;
                if (rid == null || !Riders.TryGetValue(rid, out r) || r.St != RChase) return;
                if (ReferenceEquals(affector, rid) || ReferenceEquals(affector, victim)) return;
                var at = affector.Team;
                var vt = rid.Team;
                if (!MissionTeams.Valid(at) || !MissionTeams.Valid(vt) || at.Side != vt.Side) return;
                if (blow.IsMissile) _ffMissile++; else _ffMelee++;
                _ffDmg += blow.InflictedDamage;
            }
            catch (Exception e) { Stumble("HorsePursuit.OnHit", e); }
        }

        /// <summary>BattleSense.OnAgentRemoved (blok Night): straty stron, jezdziec w poscigu padl, uciekajacy dopadniety (przez kogo) albo uszedl.</summary>
        internal static void OnRemoved(Mission m, Agent a, Agent by, AgentState state)
        {
            if (!_live || a == null || !a.IsHuman) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;
            try
            {
                int s = SideOf(a.Team);
                int k = state == AgentState.Killed ? 0 : (state == AgentState.Unconscious ? 1 : (state == AgentState.Routed ? 2 : 3));
                if (s >= 0) Lost[s, k]++;
                bool down = state == AgentState.Killed || state == AgentState.Unconscious;
                Rider r;
                if (Riders.TryGetValue(a, out r))
                {
                    if (r.St == RChase)
                    {
                        if (down) _ridersLost++;
                        Dec(r.V);
                        r.V = null;
                    }
                    r.St = ROut;
                    MarkRiding(a, false);
                    Riders.Remove(a);
                }
                if (down && SurvivalMode.InPanic(a))
                {
                    Rider kr = null;
                    bool mounted = false;
                    if (by != null && !ReferenceEquals(by, a))
                    {
                        mounted = by.IsMount || by.MountAgent != null;
                        if (!Riders.TryGetValue(by, out kr) && by.IsMount && by.RiderAgent != null) Riders.TryGetValue(by.RiderAgent, out kr);
                    }
                    if (mounted) _byRiderAll++;
                    if (kr != null && kr.St == RChase) { if (state == AgentState.Killed) _killed++; else _ko++; }
                    else if (EverChased.Contains(a)) _fellOthers++;
                }
                else if (state == AgentState.Routed && EverChased.Contains(a)) _escaped++;
                Chasers.Remove(a);
            }
            catch (Exception e) { Stumble("HorsePursuit.OnRemoved", e); }
        }

        /// <summary>BattleSense.Clean: jezdzcy oddani grze, linia raz na bitwe, stan od zera.</summary>
        internal static void End(Mission m)
        {
            var w = _m;
            if (w == null) return;
            if (m != null && !ReferenceEquals(w.Target, m)) return;
            float now = 0f;
            try { if (m != null) now = m.CurrentTime; } catch { }
            try
            {
                EndList.Clear();
                foreach (var st in Forms.Values) if (st.Active) EndList.Add(st);
                for (int i = 0; i < EndList.Count; i++) EndEp(EndList[i], EMission, now);
                EndList.Clear();
            }
            catch (Exception e) { Stumble("HorsePursuit.End", e); }
            if (!_written)
            {
                _written = true;
                try { Write(m); } catch (Exception e) { Stumble("HorsePursuit.Write", e); }
            }
            _live = false;
            Clear();
            _m = null;
        }

        private static void Clear()
        {
            foreach (var r in Riders.Values) MarkRiding(r.A, false);
            Volatile.Write(ref _riding, null);
            Forms.Clear(); Riders.Clear(); All.Clear(); Chasers.Clear(); EverChased.Clear(); Units.Clear(); EndList.Clear();
            for (int s = 0; s < 2; s++) { Flee[s].Clear(); FleePos[s].Clear(); IntactPos[s].Clear(); IntactR[s].Clear(); Panic[s] = 0; Alive[s] = 0; }
            _passAcc = _trackAcc = 0f;
            _rot = 0;
            _episodes = _assign = _retarget = _sets = _budgetHit = _ridersLost = _dropped = 0;
            _epSum = _epMax = _maxFar = 0f;
            _killed = _ko = _escaped = _fellOthers = _byRiderAll = 0;
            _ffMissile = _ffMelee = 0;
            _ffDmg = 0;
            Interlocked.Exchange(ref _capSkip, 0);
            _dryEpisodes = 0;
            _drySec = 0f;
            Array.Clear(EndWhy, 0, EndWhy.Length);
            Array.Clear(OutWhy, 0, OutWhy.Length);
            Array.Clear(NoStart, 0, NoStart.Length);
            Array.Clear(Excluded, 0, Excluded.Length);
            Array.Clear(Lost, 0, Lost.Length);
            ExSeen.Clear(); EverRiders.Clear(); EverVictims.Clear(); PursuedForms.Clear(); DryForms.Clear(); PlayerSkipped.Clear();
            _formsPlayerSide = _formsFoe = _formsCav = _formsHa = 0;
        }

        // ------------------------------------------------------------ pomocnicze

        private static int SideOf(Team t)
        {
            if (!MissionTeams.Valid(t)) return -1;
            if (t.Side == BattleSideEnum.Defender) return 0;
            if (t.Side == BattleSideEnum.Attacker) return 1;
            return -1;
        }

        /// <summary>Formacja gracza (nie ruszamy - D5): rozkazy gracza, gracz w szyku, gracz wodzem druzyny, gracz sierzantem szyku.</summary>
        private static bool PlayerForm(Formation f)
        {
            if (!f.IsAIControlled || f.IsPlayerTroopInFormation) return true;
            var t = f.Team;
            if (t != null && t.IsPlayerTeam && t.IsPlayerGeneral) return true;
            var main = Agent.Main;
            return main != null && ReferenceEquals(f.PlayerOwner, main);
        }

        /// <summary>Chorazy wedlug RBM (Utilities.IsBannerBearer:28-58): bron w rece klasy Banner albo uzycie "banner" - RBM sam steruje jego celem.
        /// 149: takze HeroCaution (chorazego nie przestawiamy w szyku) - jedna definicja "chorazy".</summary>
        internal static bool Banner(Agent a)
        {
            return BannerWeapon(a.WieldedWeapon) || BannerWeapon(a.WieldedOffhandWeapon);
        }

        private static bool BannerWeapon(MissionWeapon w)
        {
            if (w.IsEmpty) return false;
            var u = w.CurrentUsageItem;
            if (u == null) return false;
            if (u.WeaponClass == WeaponClass.Banner) return true;
            var s = u.ItemUsage;
            return s != null && s.Contains("banner");
        }

        /// <summary>Stale wykluczenie jezdzca (0 = zaden).</summary>
        private static int Excl(Agent a)
        {
            if (a.IsMainAgent || !a.IsAIControlled) return XPlayer;
            if (a.IsHero) return XHero;
            var mt = a.MountAgent;
            if (mt != null && MarchToContact.IsDragon(mt)) return XDragon;
            if (Undead.Character(a.Character)) return XDead;
            if (Banner(a)) return XBanner;
            return 0;
        }

        private static void NoteEx(Agent a, int x)
        {
            if (ExSeen.Add((a.Index << 3) | x)) Excluded[x]++;
        }

        private static bool Withdrawing(Agent a)
        {
            var k = RoutHold.Live;
            return k != null && k.Withdrawing(a);
        }

        private static float HorseS(Agent a)
        {
            var mt = a.MountAgent;
            return mt != null ? HorseWind.StaminaOf(mt) : -1f;
        }

        private static bool Blown(float s) { return _restBelow > 0 && s >= 0f && s < _restBelow; }

        /// <summary>Punkt blizej niz r od nietknietej formacji strony s (odleglosc od mediany minus promien szyku, najwyzej 40 m).</summary>
        private static bool IntactNear(int s, Vec2 p, float r)
        {
            if (s < 0) return false;
            var ps = IntactPos[s];
            var rs = IntactR[s];
            for (int i = 0; i < ps.Count; i++)
            {
                if (p.Distance(ps[i]) - rs[i] < r) return true;
            }
            return false;
        }

        private static void Inc(Agent v)
        {
            if (v == null) return;
            int c;
            Chasers.TryGetValue(v, out c);
            Chasers[v] = c + 1;
        }

        private static void Dec(Agent v)
        {
            if (v == null) return;
            int c;
            if (!Chasers.TryGetValue(v, out c)) return;
            if (c <= 1) Chasers.Remove(v); else Chasers[v] = c - 1;
        }

        // ------------------------------------------------------------ pass (co 1 s)

        private static void Pass(Mission m, float now)
        {
            var c = Settings.Current;
            _key = c != null && c.AiCavalryPursues;
            _restBelow = c != null && c.HorseStaminaEnabled ? Math.Max(0, Math.Min(80, c.HorseAiRestBelow)) : 0;   // ten sam prog co odpoczynek konnicy AI (MarchToContact)
            try { BuildSides(m); }
            catch (Exception e) { Stumble("HorsePursuit.Strony", e); return; }
            foreach (var st in Forms.Values) st.Seen = false;
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team t;
                try { t = teams[i]; } catch { continue; }
                int s = SideOf(t);
                if (s < 0) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                bool anchorDone = false, hasMain = false;
                Vec2 anchor = Vec2.Invalid;
                for (int j = 0; j < nf; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    var q = f.QuerySystem;
                    if (q == null || !(q.IsCavalryFormation || q.IsRangedCavalryFormation)) continue;
                    if (!anchorDone) { anchorDone = true; hasMain = MainForce(t, out anchor); }
                    FormSt st;
                    if (!Forms.TryGetValue(f, out st)) { st = new FormSt { F = f }; Forms[f] = st; }
                    st.Seen = true;
                    st.Side = s;
                    st.Enemy = 1 - s;
                    try { Form(m, f, q, st, hasMain, anchor, now); }
                    catch (Exception e) { Units.Clear(); Stumble("HorsePursuit.Szyk", e); }
                }
            }
            // szyk zniknal z listy, oproznil sie albo przestal byc konny - koniec poscigu
            EndList.Clear();
            foreach (var st in Forms.Values) if (st.Active && !st.Seen) EndList.Add(st);
            for (int i = 0; i < EndList.Count; i++) EndEp(EndList[i], EEmpty, now);
            EndList.Clear();
            // lista do odswiezania celu (round robin w Track)
            All.Clear();
            foreach (var st in Forms.Values) if (st.Active && !st.Dry) All.AddRange(st.Riders);
            Track(now);
        }

        /// <summary>Uciekajacy (PANIKA 139) i liczba ludzi na strone, nietkniete formacje stron (szyk od IntactMin ludzi - uciekajacy nie maja szyku).</summary>
        private static void BuildSides(Mission m)
        {
            for (int s = 0; s < 2; s++) { Flee[s].Clear(); FleePos[s].Clear(); IntactPos[s].Clear(); IntactR[s].Clear(); Panic[s] = 0; Alive[s] = 0; }
            var ags = m.Agents;
            int n = ags != null ? ags.Count : 0;
            for (int i = 0; i < n; i++)
            {
                Agent a;
                try { a = ags[i]; } catch { continue; }
                if (a == null || !a.IsHuman || !a.IsActive()) continue;
                int s = SideOf(a.Team);
                if (s < 0) continue;
                Alive[s]++;
                if (!SurvivalMode.InPanic(a)) continue;
                Panic[s]++;
                if (a.IsMainAgent) continue;
                var mt = a.MountAgent;
                if (mt != null && MarchToContact.IsDragon(mt)) continue;   // smoka nie dogoni
                Flee[s].Add(a);
                FleePos[s].Add(a.Position.AsVec2);
            }
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team t;
                try { t = teams[i]; } catch { continue; }
                int s = SideOf(t);
                if (s < 0) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits < IntactMin) continue;
                    var mp = f.CachedMedianPosition;
                    if (!mp.IsValid) continue;
                    IntactPos[s].Add(mp.AsVec2);
                    IntactR[s].Add(Math.Min(RadiusCap, 0.5f * Math.Max(f.Width, f.Depth)));
                }
            }
        }

        /// <summary>Glowna sila druzyny: najwiekszy pieszy szyk (piechota albo lucznicy); false = bez piechoty (wtedy miejsce wejscia w poscig).</summary>
        private static bool MainForce(Team t, out Vec2 at)
        {
            at = Vec2.Invalid;
            int best = 0;
            var fl = t.FormationsIncludingSpecialAndEmpty;
            int nf = fl != null ? fl.Count : 0;
            for (int j = 0; j < nf; j++)
            {
                var f = fl[j];
                if (f == null || f.CountOfUnits <= best) continue;
                var q = f.QuerySystem;
                if (q == null || q.IsCavalryFormation || q.IsRangedCavalryFormation || !(q.IsInfantryFormation || q.IsRangedFormation)) continue;
                var mp = f.CachedMedianPosition;
                if (!mp.IsValid) continue;
                best = f.CountOfUnits;
                at = mp.AsVec2;
            }
            return best > 0;
        }

        /// <summary>Strona s zlamala wroga e: u wroga co najmniej MinFleeing w PANICE i odsetek PANIKI wroga wiekszy niz wlasny.</summary>
        private static bool Broke(int s, int e)
        {
            if (Panic[e] < MinFleeing || Alive[e] <= 0) return false;
            float pe = (float)Panic[e] / Alive[e];
            float ps = Alive[s] > 0 ? (float)Panic[s] / Alive[s] : 0f;
            return pe > ps;
        }

        private static int NearCount(int e, Vec2 c)
        {
            int n = 0;
            var ps = FleePos[e];
            for (int i = 0; i < ps.Count; i++) if (c.Distance(ps[i]) <= SeekRadius) n++;
            return n;
        }

        private static void Form(Mission m, Formation f, FormationQuerySystem q, FormSt st, bool hasMain, Vec2 anchor, float now)
        {
            Vec2 c = f.CachedAveragePosition;
            if (!c.IsValid) return;
            bool player = PlayerForm(f);
            if (st.Active)
            {
                st.HasMain = hasMain;
                if (hasMain) st.Anchor = anchor;
                float age = now - st.Start;
                int end = -1;
                if (player) end = EPlayer;
                else if (st.Dry == _key) end = EKey;                  // klucz przelaczony w trakcie
                else if (age >= _maxSec) end = ETime;
                else if (Blown(HorseWind.AvgStamina(f))) end = EHorses;
                if (end >= 0) { EndEp(st, end, now); return; }
                if (st.Dry) _drySec += PassEvery;
                int chasing = 0;
                if (!st.Dry) chasing = RidersPass(f, st, now);
                int near = NearCount(st.Enemy, c);
                if (chasing > 0 || near > 0) st.QuietSince = -1f;
                else if (st.QuietSince < 0f) st.QuietSince = now;
                else if (now - st.QuietSince >= QuietEnd) EndEp(st, EQuiet, now);
                return;
            }
            int e = 1 - st.Side;
            if (NearCount(e, c) < MinFleeing) return;
            if (player) { PlayerSkipped.Add(f); return; }
            int why = -1;
            if (now < st.CoolUntil) why = NCool;
            else if (!Broke(st.Side, e)) why = NNotBroken;
            else if (m.MissionEnded) why = NEnded;
            else if (IntactNear(e, c, IntactRadius)) why = NIntact;
            else if (Blown(HorseWind.AvgStamina(f))) why = NHorses;
            else if (MarchToContact.Resting(f)) why = NRest;
            else if (hasMain && c.Distance(anchor) > _maxFrom) why = NFar;
            if (why >= 0) { NoStart[why]++; return; }
            // wejscie w poscig
            st.Active = true;
            st.Dry = !_key;
            st.Start = now;
            st.QuietSince = -1f;
            st.StartPos = c;
            st.HasMain = hasMain;
            st.Anchor = hasMain ? anchor : c;
            st.Night = _night;
            st.Riders.Clear();
            if (st.Dry)
            {
                _dryEpisodes++;
                DryForms.Add(f);
                return;
            }
            _episodes++;
            if (PursuedForms.Add(f))
            {
                var pt = m.PlayerTeam;
                bool side = pt != null && f.Team != null && f.Team.Side == pt.Side;
                if (side) _formsPlayerSide++; else _formsFoe++;
                if (q.IsRangedCavalryFormation) _formsHa++; else _formsCav++;
            }
            RidersPass(f, st, now);
        }

        /// <summary>Jezdzcy szyku w poscigu: nowi z szyku, kontrola (zawrocenie), utrzymanie celu, przydzial wolnym. Zwraca liczbe gonacych.</summary>
        private static int RidersPass(Formation f, FormSt st, float now)
        {
            // 1) nowi z szyku
            Units.Clear();
            f.ApplyActionOnEachUnit(Collect);   // gra iteruje swoje listy - my tylko zbieramy
            for (int u = 0; u < Units.Count; u++)
            {
                var a = Units[u];
                if (a == null || !a.IsActive() || !a.IsHuman || Riders.ContainsKey(a)) continue;
                int x = Excl(a);
                if (x != 0) { NoteEx(a, x); continue; }
                if (a.MountAgent == null || a.IsDetachedFromFormation || a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a)) continue;
                var r = new Rider { A = a, F = f, S = st, St = RFree };
                Riders[a] = r;
                st.Riders.Add(r);
            }
            Units.Clear();
            // 2) kontrola i utrzymanie celu
            int chasing = 0;
            for (int i = 0; i < st.Riders.Count; i++)
            {
                var r = st.Riders[i];
                if (r.St == ROut) continue;
                try
                {
                    int why = Check(r, st);
                    if (why >= 0)
                    {
                        if (r.St == RChase) Out(r, why);
                        else if (why == OPanic || why == OOther) r.St = ROut;   // wolny, ktory zmienil szyk, zsiadl albo spanikowal - poza poscigiem bez licznika
                        continue;
                    }
                    if (r.St == RChase)
                    {
                        if (VictimOk(r.V, r.A.Position.AsVec2, st)) { chasing++; continue; }
                        Unassign(r);
                    }
                }
                catch (Exception e) { Fail(r, "HorsePursuit.Jezdziec", e); }
            }
            // 3) przydzial wolnym
            for (int i = 0; i < st.Riders.Count; i++)
            {
                var r = st.Riders[i];
                if (r.St != RFree) continue;
                try
                {
                    if (Check(r, st) >= 0) continue;
                    var v = Pick(r, st);
                    if (v == null) continue;
                    Chase(r, v);
                    chasing++;
                }
                catch (Exception e) { Fail(r, "HorsePursuit.Przydzial", e); }
            }
            return chasing;
        }

        /// <summary>Kontrola jezdzca (-1 = moze gonic, inaczej przyczyna zawrocenia).</summary>
        private static int Check(Rider r, FormSt st)
        {
            var a = r.A;
            if (a == null || !a.IsActive() || !ReferenceEquals(a.Formation, r.F) || a.MountAgent == null) return OOther;
            if (a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a)) return OPanic;
            if (a.GetComponent<VictoryComponent>() != null) return OCheer;   // gra wybrala go do wiwatu (AgentVictoryLogic) - wiwat gry zostaje
            if (Blown(HorseS(a))) return OHorse;
            Vec2 p = a.Position.AsVec2;
            float d = p.Distance(st.Anchor);
            if (r.St == RChase && d > _maxFar) _maxFar = d;
            if (d > _maxFrom) return OFar;
            if (IntactNear(st.Enemy, p, IntactRadius)) return OIntact;
            return -1;
        }

        /// <summary>Cel nadal wazny: zywy, w PANICE, w 150 m od jezdzca, nie przy nietknietej formacji wroga, w limicie od glownej sily.</summary>
        private static bool VictimOk(Agent v, Vec2 from, FormSt st)
        {
            if (v == null || !v.IsActive() || !SurvivalMode.InPanic(v)) return false;
            Vec2 p = v.Position.AsVec2;
            if (from.Distance(p) > SeekRadius) return false;
            if (IntactNear(st.Enemy, p, IntactRadius)) return false;
            return p.Distance(st.Anchor) <= _maxFrom - FarMargin;
        }

        /// <summary>Najblizszy uciekajacy (konny liczony jak 2x dalej) z mniej niz PerVictim gonacymi; pozycje z ostatniego passu, wybrany sprawdzany na zywo.</summary>
        private static Agent Pick(Rider r, FormSt st)
        {
            if (st.Enemy < 0) return null;
            Vec2 from = r.A.Position.AsVec2;
            var fs = Flee[st.Enemy];
            var ps = FleePos[st.Enemy];
            Agent best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < fs.Count; i++)
            {
                var v = fs[i];
                if (ReferenceEquals(v, r.V) || !v.IsActive()) continue;   // padl albo uszedl od passu - lista z passu
                float d = from.Distance(ps[i]);
                if (d > SeekRadius) continue;
                int c;
                if (Chasers.TryGetValue(v, out c) && c >= PerVictim) continue;
                if (v.MountAgent != null) d *= 2f;
                if (d >= bd) continue;
                if (IntactNear(st.Enemy, ps[i], IntactRadius) || ps[i].Distance(st.Anchor) > _maxFrom - FarMargin) continue;
                best = v;
                bd = d;
            }
            if (best != null && !VictimOk(best, from, st)) return null;   // padl albo wyszedl z paniki od passu - nastepny pass
            return best;
        }

        private static void Chase(Rider r, Agent v)
        {
            var a = r.A;
            if (r.St != RChase)
            {
                a.SetAutomaticTargetSelection(false);
                r.St = RChase;
                MarkRiding(a, true);
                EverRiders.Add(a.Index);
            }
            if (!ReferenceEquals(r.V, v))
            {
                Dec(r.V);
                r.V = v;
                Inc(v);
                EverChased.Add(v);
                EverVictims.Add(v.Index);
                _assign++;
                a.SetTargetAgent(v);
            }
            Push(r);
        }

        /// <summary>Ruch za celem - pozycja skryptowa na miejscu uciekajacego (wzor ScriptedMovementComponent gry), bez NoAttack i DoNotRun.</summary>
        private static void Push(Rider r)
        {
            var wp = r.V.GetWorldPosition();
            if (!wp.IsValid) return;
            r.A.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);
            _sets++;
        }

        /// <summary>Jezdziec oddany grze: automatyczny wybor celu, bez naszej pozycji skryptowej (chyba ze stoi na postoju rannego 143 - ta jest jego).</summary>
        private static void Uncontrol(Rider r)
        {
            var a = r.A;
            MarkRiding(a, false);
            if (a == null || !a.IsActive()) return;
            try { a.SetAutomaticTargetSelection(true); a.InvalidateTargetAgent(); } catch { }
            bool touch = a.IsRunningAway || !Withdrawing(a);
            if (!touch) return;
            try { if ((a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) a.DisableScriptedMovement(); } catch { }
        }

        private static void Unassign(Rider r)
        {
            Dec(r.V);
            r.V = null;
            Uncontrol(r);
            r.St = RFree;
        }

        private static void Out(Rider r, int why)
        {
            if (r.St == RChase)
            {
                Dec(r.V);
                r.V = null;
                Uncontrol(r);
                OutWhy[why]++;
            }
            r.St = ROut;
        }

        private static void Fail(Rider r, string where, Exception e)
        {
            Stumble(where, e);
            if (++r.Fails < FailsOut) return;
            _dropped++;
            try { Out(r, OOther); } catch { r.St = ROut; MarkRiding(r.A, false); }
        }

        /// <summary>Koniec poscigu szyku: jezdzcy wracaja do szyku, przerwa Cooldown.</summary>
        private static void EndEp(FormSt st, int why, float now)
        {
            if (!st.Active) return;
            st.Active = false;
            st.CoolUntil = why == EKey ? now : now + Cooldown;   // klucz przelaczony w trakcie - bez przerwy (poscig albo pomiar od razu wedlug nowego klucza)
            float dur = Math.Max(0f, now - st.Start);
            if (!st.Dry)
            {
                EndWhy[why]++;
                _epSum += dur;
                if (dur > _epMax) _epMax = dur;
            }
            for (int i = 0; i < st.Riders.Count; i++)
            {
                var r = st.Riders[i];
                try
                {
                    if (r.St == RChase)
                    {
                        Dec(r.V);
                        r.V = null;
                        Uncontrol(r);
                    }
                }
                catch (Exception e) { Stumble("HorsePursuit.Koniec", e); }
                r.St = ROut;
                if (r.A != null)
                {
                    Rider cur;
                    if (Riders.TryGetValue(r.A, out cur) && ReferenceEquals(cur, r)) Riders.Remove(r.A);
                    MarkRiding(r.A, false);
                }
            }
            st.Riders.Clear();
        }

        // ------------------------------------------------------------ cel jezdzcow (co 0.25 s i po kazdym passie)

        private static void Track(float now)
        {
            int n = All.Count;
            if (n == 0) return;
            int budget = TrackPerStep;
            int start = _rot > 0 && _rot < n ? _rot : 0;
            int next = 0;
            for (int k = 0; k < n; k++)
            {
                int i = start + k;
                if (i >= n) i -= n;
                var r = All[i];
                if (r.St != RChase) continue;
                if (budget <= 0) { _budgetHit++; next = i; break; }
                budget--;
                try
                {
                    var a = r.A;
                    if (a == null || !a.IsActive()) { Dec(r.V); r.V = null; r.St = ROut; MarkRiding(a, false); continue; }
                    if (a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a)) { Out(r, OPanic); continue; }
                    if (!ReferenceEquals(a.Formation, r.F) || a.MountAgent == null) { Out(r, OOther); continue; }
                    if (a.GetComponent<VictoryComponent>() != null) { Out(r, OCheer); continue; }   // gra wybrala go do wiwatu - od razu, bez czekania na pass
                    var v = r.V;
                    if (v == null || !v.IsActive() || !SurvivalMode.InPanic(v))
                    {
                        var nv = Pick(r, r.S);
                        if (nv == null) { Unassign(r); continue; }
                        _retarget++;
                        Chase(r, nv);
                        continue;
                    }
                    Push(r);
                }
                catch (Exception e) { Fail(r, "HorsePursuit.Cel", e); }
            }
            _rot = next;
        }

        // ------------------------------------------------------------ linia bitwy

        private static string F0(float v) { return v.ToString("0", Inv); }

        private static string Pct(int a, int b) { return b > 0 ? (100.0 * a / b).ToString("0.0", Inv) + "%" : "-"; }

        private static void Write(Mission m)
        {
            var c = Settings.Current;
            if (!_field)
            {
                if (!_key || _naval) return;
                Log.Info("poscig (148): misja " + _kind + ", klucz TAK - nieczynne: tylko bitwa polowa.");
                return;
            }
            // na polu przy koncu (strony)
            int[] onField = new int[2];
            try
            {
                var ags = m != null ? m.Agents : null;
                int n = ags != null ? ags.Count : 0;
                for (int i = 0; i < n; i++)
                {
                    var a = ags[i];
                    if (a == null || !a.IsHuman || !a.IsActive()) continue;
                    int s = SideOf(a.Team);
                    if (s >= 0) onField[s]++;
                }
            }
            catch { }
            int stum;
            lock (RoutHold.Gate) { stum = RoutHold.StumblesLocked(RoutHold.SitePursuit); }
            var sb = new StringBuilder(2400);
            sb.Append("poscig (148): misja ").Append(_kind).Append(", klucz ").Append(_key ? "TAK" : "NIE (tylko pomiar: bez sterowania jezdzcami)")
              .Append(" (konnica i konni lucznicy AI strony, ktora zlamala wroga: uciekajacy wroga (PANIKA 139) w ").Append(F0(SeekRadius))
              .Append(" m od szyku, bez nietknietej formacji wroga (szyk od ").Append(IntactMin).Append(" ludzi) w ").Append(F0(IntactRadius))
              .Append(" m; limit ").Append(F0(_maxSec)).Append(" s / ").Append(F0(_maxFrom)).Append(" m od glownej sily (noc ").Append(_night ? "TAK - polowa" : "NIE")
              .Append("); najwyzej ").Append(PerVictim).Append(" jezdzcow na uciekajacego; nastepny poscig szyku po ").Append(F0(Cooldown))
              .Append(" s; formacje gracza, bohaterowie, chorazowie, smoki i umarli nie gonia; konie ponizej Horse AI Rest Below ").Append(_restBelow)
              .Append(" (136) nie gonia; Friendly Fire ").Append(c != null && c.FriendlyFireEnabled ? "TAK, pociski " + c.FriendlyFireRangedPercent + "%" : "NIE").Append(')');
            if (_key)
            {
                sb.Append(" | szyki ").Append(PursuedForms.Count).Append(" (strona gracza ").Append(_formsPlayerSide).Append(" / wroga ").Append(_formsFoe)
                  .Append("; konnica ").Append(_formsCav).Append(", konni lucznicy ").Append(_formsHa).Append("), wejsc ").Append(_episodes)
                  .Append(" (sr. czas ").Append(_episodes > 0 ? (_epSum / _episodes).ToString("0.0", Inv) : "-").Append(" s, najdluzej ").Append(_epMax.ToString("0.0", Inv))
                  .Append(" s), jezdzcow w poscigu ").Append(EverRiders.Count).Append(" (przydzialow celu ").Append(_assign).Append(", nowy cel po utracie ").Append(_retarget)
                  .Append("), uciekajacych gonionych ").Append(EverVictims.Count)
                  .Append(" | koniec poscigu szyku: czas ").Append(EndWhy[ETime]).Append(", brak uciekajacych ").Append(EndWhy[EQuiet]).Append(", konie szyku ")
                  .Append(EndWhy[EHorses]).Append(", gracz przejal ").Append(EndWhy[EPlayer]).Append(", szyk pusty ").Append(EndWhy[EEmpty]).Append(", koniec misji ")
                  .Append(EndWhy[EMission]).Append(", klucz ").Append(EndWhy[EKey])
                  .Append(" | jezdzcy zawroceni: ").Append(F0(_maxFrom)).Append(" m od glownej sily ").Append(OutWhy[OFar]).Append(", nietknieta formacja w ")
                  .Append(F0(IntactRadius)).Append(" m ").Append(OutWhy[OIntact]).Append(", kon zajechany ").Append(OutWhy[OHorse]).Append(", panika / odwrot ")
                  .Append(OutWhy[OPanic]).Append(", wiwat ").Append(OutWhy[OCheer]).Append(", inne ").Append(OutWhy[OOther]).Append("; najdalej od glownej sily ")
                  .Append(F0(_maxFar)).Append(" m")
                  .Append(" | wykluczeni z poscigu (ludzi): bohaterowie ").Append(Excluded[XHero]).Append(", chorazowie ").Append(Excluded[XBanner]).Append(", smoki ")
                  .Append(Excluded[XDragon]).Append(", umarli ").Append(Excluded[XDead]).Append(", gracz ").Append(Excluded[XPlayer]);
            }
            else
            {
                sb.Append(" | na sucho: szykow ").Append(DryForms.Count).Append(", wejsc ").Append(_dryEpisodes).Append(", szyko-sekund poscigu ")
                  .Append(_drySec.ToString("0", Inv));
            }
            sb.Append(" | nie zaczeli (passy z uciekajacymi w ").Append(F0(SeekRadius)).Append(" m): nietknieta formacja w ").Append(F0(IntactRadius)).Append(" m ")
              .Append(NoStart[NIntact]).Append(", konie szyku ").Append(NoStart[NHorses]).Append(", odpoczynek koni ").Append(NoStart[NRest]).Append(", poza ")
              .Append(F0(_maxFrom)).Append(" m od glownej sily ").Append(NoStart[NFar]).Append(", wrog nie zlamany ").Append(NoStart[NNotBroken])
              .Append(", po rozstrzygnieciu ").Append(NoStart[NEnded]).Append(", przerwa po poscigu ").Append(NoStart[NCool]).Append("; formacje gracza pominiete ")
              .Append(PlayerSkipped.Count)
              .Append(" | zabici w poscigu ").Append(_killed).Append(" (nieprzytomni ").Append(_ko).Append(") przez gonacych; gonieni padli od innych ").Append(_fellOthers)
              .Append(", gonieni uszli z pola ").Append(_escaped).Append("; uciekajacy dopadnieci przez konnych (jak 'konny' w 139) ").Append(_byRiderAll)
              .Append("; gonacy padli w poscigu ").Append(_ridersLost)
              .Append(" | FF na wlasnej jezdzie w poscigu ").Append(_ffMissile + _ffMelee).Append(" (pociski ").Append(_ffMissile).Append(", wrecz ").Append(_ffMelee)
              .Append(", obrazen ").Append(_ffDmg).Append(')')
              .Append(" | cap marszu pominiety u gonacych ").Append(Volatile.Read(ref _capSkip)).Append(", odswiezen celu ").Append(_sets).Append(" (limit kroku ")
              .Append(_budgetHit).Append(')');
            // straty stron i udzial zabitych przegranych na tle celu historycznego
            int loser = -1;
            double worst = -1;
            for (int s = 0; s < 2; s++)
            {
                int tot = Lost[s, 0] + Lost[s, 1] + Lost[s, 2] + Lost[s, 3] + onField[s];
                if (tot <= 0) continue;
                double share = (double)(Lost[s, 0] + Lost[s, 1] + Lost[s, 2]) / tot;
                if (share > worst) { worst = share; loser = s; }
            }
            sb.Append(" | straty (z wystawionych):");
            for (int s = 1; s >= 0; s--)
            {
                int tot = Lost[s, 0] + Lost[s, 1] + Lost[s, 2] + Lost[s, 3] + onField[s];
                sb.Append(s == 1 ? " atakujacy" : "; obroncy").Append(" zabici ").Append(Lost[s, 0]).Append(" / nieprzytomni ").Append(Lost[s, 1]).Append(" / uszli ")
                  .Append(Lost[s, 2]).Append(" / na polu ").Append(onField[s]).Append(" z ").Append(tot).Append(" (zabici ").Append(Pct(Lost[s, 0], tot)).Append(')');
            }
            if (loser >= 0)
            {
                int w = 1 - loser;
                int tl = Lost[loser, 0] + Lost[loser, 1] + Lost[loser, 2] + Lost[loser, 3] + onField[loser];
                int tw = Lost[w, 0] + Lost[w, 1] + Lost[w, 2] + Lost[w, 3] + onField[w];
                sb.Append("; przegrani (").Append(loser == 1 ? "atakujacy" : "obroncy").Append(") zabici ").Append(Pct(Lost[loser, 0], tl))
                  .Append(" - cel historyczny 15-40%, do 60-65% przy poscigu konnicy na pieszych; zwyciezca ").Append(Pct(Lost[w, 0], tw)).Append(" - cel 1-5%");
            }
            sb.Append(" | potkniecia poscig ").Append(stum).Append(" (wypadli ").Append(_dropped).Append(").");
            Log.Info(sb.ToString());
        }
    }
}
