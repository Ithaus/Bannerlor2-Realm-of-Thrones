using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 144 (25.10, fala "AI walczy o zycie", krok 6 = projekt 134 "AI oszczedza sily" z design-ww78cwg0y, etapy 134a-134d i klucz 134e, poprawki krytyka
    /// B1, B2, W1-W6, D1-D4). Jeff 13:20: "pamietaj aby w bitwach przeciwnik nie biegal caly czas bo beda padnieci i czy jest animacja chodu i zmeczenia??";
    /// 13:30: "przeciwnik zachowuje sie jak prawdziwy zywy czlowiek, nie chce dac sie zabic i zrobi wszystko w obszarze jego mozliwosci i skilli aby przetrwac".
    ///
    /// ZASADA (134b, klucz AiSavesBreath; NIE = ta sama decyzja "na sucho", tylko do linii - 134a): pieszy czlowiek AI obu stron biegnie tylko tak daleko,
    /// zeby dobiec z zapasem tchu. Decyzja co 0.6 s na czlowieka (FieldCraft co 0.2 s, rozlozenie (Index + takt) % 3), glowny watek:
    ///  - d = min(d_szyk, d_cel): d_szyk = od czlowieka do mediany CachedClosestEnemyFormation jego szyku minus polowa glebokosci tamtego (D4: rozsypany szyk
    ///    zaniza d), d_cel = do GetTargetAgent(), gdy to czynny wrog; d nieznane = brak capu (W1);
    ///  - budzet [m] = max(0, pasek - zapas) x T x v: pasek = BIEZACA stamina / max (przy oddechu 137 to pula - F - S, wiec odnowa w kroku wedlug Breath jest juz
    ///    w nim - nie liczymy regeneracji z gumki RBM), T = FieldCraft RunSeconds (127: cala pula w T sekund pelnego biegu, liniowo), v = zmierzone
    ///    GetMaximumForwardUnlimitedSpeed (B2: nie zalozone 4.5 m/s); zapas = AiArriveWithBreathPercent (50%), pod ostrzalem szyku (UnderRangedAttackRatio >= 0.33)
    ///    najwyzej 25%;
    ///  - krok, gdy d > budzet i d > AiRunsLastMetres (25 m); powrot do biegu, gdy d &lt; budzet - 10 m albo d &lt;= 25 m (histereza); pogon (cel ucieka i jest
    ///    blizej niz szyk wroga) - bez strefy ostatnich metrow, sam budzet;
    ///  - krok = min(WalkSpeedCached, 1.8) - 0.05 = 1.75 m/s - ponizej progu biegu FieldCraft (2.2), wiec oddech 137 wraca (mnoznik chodu), a nie stoi.
    /// Wyjatki (brak capu): gracz, MissionPeer, konni, umarli (FieldCraft ich pomija), PANIKA wedlug przelacznika 139 (SurvivalMode) albo IsRunningAway /
    /// IsRetreating(), ranny w odwrocie 143, bez szyku (Z1: gra nie liczy mu limitu), odlaczeni (drabiny, wieze, taran, brama), ludzie gracza przy
    /// AiSavesBreathYourTroops NIE, szyk z rozkazem Follow (FollowMe), szyk z capem marszu (MarchToContact.IsMarching - 128 bez zmian), swieza szarza
    /// (MarchToContact.ChargeFresh: od sygnalu do pierwszego zwarcia, najwyzej 20 s - B2), zwarcie (cios wrecz w 3 s albo cel nieuciekajacy w 25 m),
    /// indeks poza tablica. Zakres: bitwa polowa po rozstawieniu; oblezenie i wypad tylko przy AiSavesBreathInSieges (134e, domyslnie NIE); kryjowka, morze,
    /// turniej, arena - nie. D2: przy SprintFatigueEnabled NIE zasada nieczynna, przy SpeedCapOnlyWhenWinded NIE albo bez latki AdjustSpeedLimit - tylko na sucho.
    /// Cap czyta latka MarchToContact.PostSpeed (watki robocze, B1: za wszystkimi wyjsciami, bez szyku / konni / ucieczka - nic) i CapFor (kara FieldCraft
    /// nie podnosi limitu ponad krok, zdjecie kary pisze krok zamiast -1). Tablice po Agent.Index z wlascicielem (ponowne uzycie indeksu), zapis tylko
    /// z glownego watku w kolejnosci Cap = 0 -> Owner -> Cap (D1), odczyt Owner -> Cap -> Owner; czyszczenie przy panice / ucieczce (B1), usunieciu agenta,
    /// poczatku i koncu misji. Wyjatek liczony na czlowieka (5 = wypada), bez globalnego wylacznika (RoutHold.Stumble, miejsce "oddech").
    /// Linia "Oddech AI (144)" raz na bitwe (FieldCraft.OnEndMission, obok "Bieg (127)"); linia kandydatow animacji raz na bitwe (WindedShow).
    /// Nazwa klasy nie moze byc "Log", "Settings" ani "McmSettings".
    /// </summary>
    internal static class AiBreath
    {
        internal const int Size = 16384;
        internal const float FireRatio = 0.33f, FireReserve = 0.25f, MeleeRecent = 3f, Hyst = 10f;
        internal const float WalkTop = 1.8f, WalkMargin = 0.05f;
        internal const float ReinfFrom = 60f, ReinfJoin = 30f, ReinfAfter = 2f;
        internal const float ProbeEvery = 2f, StretchEvery = 2f, StretchShare = 0.30f, DetachedFast = 2.2f;
        internal const int ProbePerGroup = 10, StretchMinMen = 6, MaxFails = 5;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- stan czytany przez latke AdjustSpeedLimit (watki robocze); zapis tylko z glownego watku
        private static volatile bool _on;
        private static readonly float[] _cap = new float[Size];
        private static readonly Agent[] _owner = new Agent[Size];
        private static int _post;                 // zapisy limitu w PostSpeed (Interlocked)

        /// <summary>Zasada czynna w tej misji (klucz TAK, bitwa polowa po rozstawieniu albo oblezenie z kluczem 134e, D2 spelnione). Czytane takze z watkow roboczych.</summary>
        internal static bool On { get { return _on; } }

        /// <summary>Stan czlowieka (w FieldCraft.State - zyje tyle co wpis FieldCraft). Tylko glowny watek.</summary>
        internal sealed class Man
        {
            public bool Counted, Walk, EverWalk, Reinf, FirstMelee, HadMelee, Winded1, Below10, Out, VLogged, Mounted;
            public int Side = -1, Sit = SOther, RunSit = SOther, Fails;
            public float SeenAt = -1f, RunFor;
        }

        // ---- wyjatki (powod braku capu w decyzji) i sytuacje biegu
        private const int XPlayer = 0, XMounted = 1, XPanic = 2, XDetached = 3, XNoForm = 4, XYours = 5, XFollow = 6, XMarch = 7, XFresh = 8, XMelee = 9,
                          XNoD = 10, XNoV = 11, XWithdraw = 12, XPeer = 13, XIdx = 14, XCount = 15;
        private static readonly string[] XName = { "gracz", "konni", "ucieczka/panika", "odlaczeni", "bez szyku", "twoi (klucz NIE)", "FollowMe", "marsz (cap szyku)",
                                                   "swieza szarza", "zwarcie", "d nieznane", "v nieznane", "odwrot rannego (143)", "MissionPeer", "indeks poza zakresem" };
        internal const int SFlee = 0, SMelee = 1, SChase = 2, SReinf = 3, SCharge = 4, SMarch = 5, SSiege = 6, SOther = 7, SCount = 8;
        private static readonly string[] SitName = { "ucieczka", "zwarcie", "pogon", "posilki", "szarza", "marsz/dogonienie", "oblezenie", "inne" };

        // ---- misja (glowny watek)
        private static WeakReference _m;
        private static bool _battle, _live, _real, _siege, _seen, _written, _candDone;
        private static string _why = "-", _kindTxt = "-";
        private static float _liveSince = -1f, _realSecs, _drySecs, _lastNow = -1f;
        private static float _reserve = 0.5f, _last = 25f;
        private static bool _yours = true;
        private static int _tick;

        // ---- liczniki misji: strony [0 = gracz i sojusznicy, 1 = wrog]
        private static readonly int[] _nFoot = new int[2], _nWinded = new int[2], _nBelow10 = new int[2];
        private static readonly float[] _footSecs = new float[2], _runSecs = new float[2], _windSecs = new float[2];
        private static readonly float[] _sitSecs = new float[SCount];
        private static int _runN, _run20, _run30, _runMaxSit = SOther;
        private static float _runSum, _runMax;
        // pierwsze zwarcie
        private static readonly int[] _fmN = new int[2], _fmChN = new int[2], _fmB = new int[4];
        private static readonly float[] _fmSum = new float[2], _fmChSum = new float[2];
        private static int _reinfN, _reinfMeleeN;
        private static float _reinfMeleeSum;
        // kara FieldCraft (zapisy rozbite na przyczyny)
        private static int _penWound, _penWind, _penKey, _penBreath, _release, _penRelCap;
        // zasada
        private static int _decisions, _menCap, _toRun, _budgetN, _vN, _underFire, _freshWalk, _out, _fails;
        private static readonly int[] _vB = new int[4];
        private static readonly int[] _exempt = new int[XCount];
        private static float _walkSecs, _walkAfterMelee, _budgetSum, _vSum, _tSum;
        // D3: szyki rozciagniete (bez marszu i szarzy, ponad 30% ludzi z capem)
        private static readonly Dictionary<Formation, int> _stretchCount = new Dictionary<Formation, int>();
        private static readonly HashSet<Formation> _stretchF = new HashSet<Formation>();
        private static float _stretchAcc, _stretchSecs;
        private static bool _stretchNow;
        // W4 (134e): odlaczeni w oblezeniu w ruchu ponad 2.2 m/s
        private static float _detFastSecs;
        // sonda animacji (W2): grupy 0 marsz, 1 krok z oddechu, 2 inni w ruchu; predkosc 4 przedzialy x WalkMode; limit 5 przedzialow
        private static readonly int[,,] _pb = new int[3, 4, 2];
        private static readonly int[,] _pl = new int[3, 5];
        private static readonly int[] _probeLeft = new int[3];
        private static float _probeAcc;
        // koszt (ms na takt FieldCraft)
        private static double _costSum, _costMax;
        private static int _costN;

        // ------------------------------------------------------------ tablice capu

        /// <summary>Cap oddechu czlowieka w m/s (0 = brak). Watki robocze (PostSpeed) i glowny (CapFor) - tylko odczyt, bez alokacji.</summary>
        internal static float CapOf(Agent a)
        {
            if (a == null || !_on) return 0f;
            int i = a.Index;
            if (i < 0 || i >= Size) return 0f;
            if (!ReferenceEquals(Volatile.Read(ref _owner[i]), a)) return 0f;
            float c = Volatile.Read(ref _cap[i]);
            if (c <= 0f || !ReferenceEquals(Volatile.Read(ref _owner[i]), a)) return 0f;
            return c;
        }

        internal static void NotePost() { Interlocked.Increment(ref _post); }

        private static void SetCap(Agent a, float v)
        {
            int i = a.Index;
            if (i < 0 || i >= Size) return;
            if (ReferenceEquals(_owner[i], a)) { Volatile.Write(ref _cap[i], v); return; }
            Volatile.Write(ref _cap[i], 0f);       // D1: najpierw zero, potem wlasciciel, potem wartosc
            Volatile.Write(ref _owner[i], a);
            Volatile.Write(ref _cap[i], v);
        }

        private static void ClearCap(Agent a)
        {
            if (a == null) return;
            int i = a.Index;
            if (i < 0 || i >= Size) return;
            if (ReferenceEquals(_owner[i], a)) Volatile.Write(ref _cap[i], 0f);
        }

        /// <summary>Wszystkie capy zdjete (poczatek i koniec misji, zasada wylaczona w trakcie). Glowny watek.</summary>
        internal static void ClearCaps()
        {
            _on = false;
            Array.Clear(_cap, 0, Size);
            Array.Clear(_owner, 0, Size);
        }

        /// <summary>B1: panika albo ucieczka (FieldCraft.OnAgentPanicked / OnAgentFleeing) - cap zdjety od razu, nie po najblizszej decyzji.</summary>
        internal static void Drop(Agent a) { ClearCap(a); }

        /// <summary>Agent usuniety z misji (FieldCraft.OnAgentDeleted) - wpis tablicy zwolniony.</summary>
        internal static void Forget(Agent a)
        {
            if (a == null) return;
            int i = a.Index;
            if (i < 0 || i >= Size || !ReferenceEquals(_owner[i], a)) return;
            Volatile.Write(ref _cap[i], 0f);
            Volatile.Write(ref _owner[i], null);
        }

        // ------------------------------------------------------------ misja

        /// <summary>FieldCraft.AfterStart: nowa misja - liczniki od zera (niezapisana linia poprzedniej misji - teraz), capy zdjete.</summary>
        internal static void Begin(Mission m)
        {
            try { if (_seen && !_written) Write(); } catch { }
            Reset();
            ClearCaps();
            _m = m != null ? new WeakReference(m) : null;
        }

        /// <summary>FieldCraft.OnRemoveBehavior: misja bez OnEndMission - capy zdjete (linia przy nastepnym Begin).</summary>
        internal static void Abort() { ClearCaps(); _battle = _live = _real = false; }

        /// <summary>Poczatek taktu FieldCraft (co 0.2 s): czy misja jest bitwa po rozstawieniu (WindedShow) i czy zasada liczy (na sucho albo naprawde).
        /// Zwraca true = wolac Step dla ludzi w tym takcie.</summary>
        internal static bool Gate(Mission m, Settings c, float now)
        {
            bool battle = false, live = false, real = false, siege = false;
            string why = "-";
            try
            {
                var w = _m;
                if (m != null && (w == null || !ReferenceEquals(w.Target, m))) Begin(m);
                if (c != null && m != null && !m.MissionEnded && m.IsDeploymentFinished && m.Mode != MissionMode.Deployment
                    && !m.IsNavalBattle && !m.IsNavalRaidBattle && BattleSense.InBattle(m) && !BattleSense.InDuel(m))
                {
                    battle = true;
                    string kind = MissionTeams.Kind(m);
                    bool field = m.IsFieldBattle && kind == "bitwa";
                    siege = (m.IsSiegeBattle || m.IsSallyOutBattle) && kind == "bitwa";
                    if (!c.SprintFatigueEnabled) why = "nieczynne: Sprint Fatigue NIE (bez puli i czasu biegu)";
                    else if (field || siege)
                    {
                        live = true;
                        if (!c.AiSavesBreath) why = "na sucho: klucz NIE";
                        else if (!c.SpeedCapOnlyWhenWinded) why = "na sucho: Speed Cap Only When Winded NIE (FieldCraft pisze limit co 0.2 s i kasowalby krok)";
                        else if (!MarchToContact.SpeedPatched) why = "na sucho: latka AdjustSpeedLimit nie wpieta";
                        else if (siege && !c.AiSavesBreathInSieges) why = "na sucho: oblezenie, klucz AiSavesBreathInSieges NIE";
                        else { real = true; why = "czynne"; }
                    }
                    else why = "nieczynne: " + (kind == "kryjowka" ? "kryjowka" : "nie bitwa polowa ani oblezenie");
                    if (!_seen) { _seen = true; _kindTxt = siege ? "oblezenie" : (field ? "pole" : kind); }
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteBreath, "AiBreath.Gate", e); battle = live = real = false; }

            float dt = _lastNow >= 0f && now > _lastNow ? Math.Min(1f, now - _lastNow) : 0f;
            _lastNow = now;
            if (real) _realSecs += dt; else if (live) _drySecs += dt;
            if (_real && !real) ClearCaps();
            _battle = battle;
            _live = live;
            _real = real;
            _siege = siege;
            if (live || battle) _why = why;
            _on = real;
            if (live && _liveSince < 0f) _liveSince = now;
            if (live && c != null)
            {
                _reserve = Clamp(c.AiArriveWithBreathPercent, 20, 80) / 100f;
                _last = Clamp(c.AiRunsLastMetres, 10f, 60f);
                _yours = c.AiSavesBreathYourTroops;
                _tick++;
                _stretchAcc += dt;
                _stretchNow = _stretchAcc >= StretchEvery;
                if (_stretchNow) { _stretchAcc = 0f; _stretchCount.Clear(); }
                _probeAcc += dt;
                if (_probeAcc >= ProbeEvery) { _probeAcc = 0f; _probeLeft[0] = _probeLeft[1] = _probeLeft[2] = ProbePerGroup; }
            }
            if (battle && !_candDone)
            {
                _candDone = true;
                try { WindedShow.CandidatesLine(m, c); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteBreath, "WindedShow.CandidatesLine", e); }
            }
            return live;
        }

        /// <summary>Bitwa po rozstawieniu (pole, oblezenie, kryjowka; bez morza i pojedynkow) w tym takcie - dla WindedShow.</summary>
        internal static bool BattleNow { get { return _battle; } }

        /// <summary>Licznik taktow zasady - FieldCraft rozklada decyzje: (Index + Tick) % 3 == 0, czyli co 0.6 s na czlowieka.</summary>
        internal static int Tick { get { return _tick; } }

        /// <summary>Takt zasady na czlowieka (FieldCraft, glowny watek, co 0.2 s; decyzja co trzeci takt). Zwraca true = FieldCraft ma od razu zdjac limit (-1):
        /// cap oddechu zszedl, a gra nie przepisze limitu szybciej niz za 0.5 s (szyk bez capu marszu).</summary>
        internal static bool Step(Agent a, Man man, float ratio, float runSeconds, bool sprinting, float speed, bool winded, float step, float now, bool decide)
        {
            if (!_live || a == null || man == null || man.Out || a.IsMainAgent) return false;
            bool release = false;
            try
            {
                if (man.SeenAt < 0f) First(a, man, now);
                int side = man.Side;
                if (a.MountAgent != null)
                {
                    // konny - poza zasada i poza licznikami pieszych; cap zostawiony z kroku pieszo zdjety
                    if (man.Walk) { man.Walk = false; ClearCap(a); }
                    man.Mounted = true;
                    if (decide) { _decisions++; _exempt[XMounted]++; }
                    if (man.RunFor > 0f) CloseRun(man);
                    return false;
                }
                if (!man.Counted) { man.Counted = true; _nFoot[side]++; }
                _footSecs[side] += step;
                if (winded)
                {
                    if (!man.Winded1) { man.Winded1 = true; _nWinded[side]++; }
                    _windSecs[side] += step;
                }
                if (ratio < 0.10f && !man.Below10) { man.Below10 = true; _nBelow10[side]++; }
                // pierwsze zwarcie: pierwszy cios wrecz, ktory trafil (atakujacy albo ofiara - pola C# Agent.LastMelee*Time, ustawiane tylko przy trafieniu); pasek do 0.2 s po ciosie
                if (!man.FirstMelee && (a.LastMeleeHitTime > float.MinValue || a.LastMeleeAttackTime > float.MinValue)) FirstMeleeOf(a, man, ratio, now);

                if (sprinting)
                {
                    if (man.RunFor <= 0f) man.RunSit = man.Sit;
                    man.RunFor += step;
                    _runSecs[side] += step;
                    _sitSecs[man.Sit] += step;
                }
                else if (man.RunFor > 0f) CloseRun(man);
                if (man.Walk)
                {
                    _walkSecs += step;
                    if (man.HadMelee) _walkAfterMelee += step;
                }
                if (_siege && speed > DetachedFast && a.IsDetachedFromFormation) _detFastSecs += step;

                var f = a.Formation;
                if (_stretchNow && f != null)
                {
                    int v;
                    _stretchCount.TryGetValue(f, out v);
                    _stretchCount[f] = v + 1 + (man.Walk ? 0x10000 : 0);
                }
                if (speed >= 0.3f) Probe(a, man, f, speed);

                if (decide)
                {
                    bool was = man.Walk;
                    float cap;
                    int sit;
                    int x = Decide(a, man, f, ratio, runSeconds, now, out cap, out sit);
                    _decisions++;
                    man.Sit = sit;
                    if (x >= 0) _exempt[x]++;
                    bool walk = cap > 0f;
                    if (walk && !man.EverWalk) { man.EverWalk = true; _menCap++; }
                    if (was && !walk && x < 0) _toRun++;
                    man.Walk = walk;
                    if (_real)
                    {
                        if (walk) SetCap(a, cap);
                        else if (was)
                        {
                            ClearCap(a);
                            release = !(f != null && MarchToContact.IsMarching(f));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                man.Fails++;
                _fails++;
                if (man.Fails >= MaxFails && !man.Out) { man.Out = true; _out++; man.Walk = false; ClearCap(a); }
                RoutHold.Stumble(RoutHold.SiteBreath, "AiBreath.Step", e);
            }
            return release;
        }

        private static void First(Agent a, Man man, float now)
        {
            man.SeenAt = now;
            man.Side = a.Team != null && (a.Team.IsPlayerTeam || a.Team.IsPlayerAlly) ? 0 : 1;
            // posilki: pojawil sie po rozstawieniu (ponad 2 s po pierwszym takcie bitwy) i ponad 60 m od mediany swojego szyku
            var f = a.Formation;
            if (_liveSince >= 0f && now > _liveSince + ReinfAfter && f != null && f.CountOfUnits > 1
                && a.Position.AsVec2.Distance(f.CachedMedianPosition.AsVec2) > ReinfFrom)
            {
                man.Reinf = true;
                _reinfN++;
            }
        }

        private static void FirstMeleeOf(Agent a, Man man, float ratio, float now)
        {
            man.FirstMelee = true;
            man.HadMelee = true;
            int side = man.Side;
            bool ch = IsCharge(a.Formation);
            _fmN[side]++;
            _fmSum[side] += ratio;
            if (ch) { _fmChN[side]++; _fmChSum[side] += ratio; }
            _fmB[ratio < 0.1f ? 0 : (ratio < 0.3f ? 1 : (ratio < 0.6f ? 2 : 3))]++;
            if (man.Reinf)
            {
                _reinfMeleeN++;
                _reinfMeleeSum += now - man.SeenAt;
                man.Reinf = false;
            }
        }

        /// <summary>Koniec ciaglego biegu (bieg ustal, koniec misji, agent usuniety, przesiadka na konia).</summary>
        internal static void CloseRun(Man man)
        {
            if (man == null || man.RunFor <= 0f) return;
            float t = man.RunFor;
            man.RunFor = 0f;
            if (!_live && !_seen) return;
            _runN++;
            _runSum += t;
            if (t > _runMax) { _runMax = t; _runMaxSit = man.RunSit; }
            if (t > 20f) _run20++;
            if (t > 30f) _run30++;
        }

        private static bool IsCharge(Formation f)
        {
            if (f == null) return false;
            var oe = f.GetReadonlyMovementOrderReference().OrderEnum;
            return oe == MovementOrder.MovementOrderEnum.Charge || oe == MovementOrder.MovementOrderEnum.ChargeToTarget;
        }

        /// <summary>Decyzja zasady: -1 = zasada (cap &gt; 0 krok, 0 bieg), inaczej numer wyjatku (cap 0). sit = sytuacja do sekund biegu.</summary>
        private static int Decide(Agent a, Man man, Formation f, float ratio, float T, float now, out float cap, out int sit)
        {
            cap = 0f;
            sit = SOther;
            // stan czlowieka
            bool panic = SurvivalMode.InPanic(a) || a.IsRunningAway;
            if (!panic) { try { panic = a.IsRetreating(); } catch { panic = false; } }
            bool withdraw = false;
            if (!panic) { var k = RoutHold.Live; withdraw = k != null && k.Withdrawing(a); }
            Agent t = null;
            float dT = float.MaxValue;
            bool fleeing = false;
            if (!panic && !withdraw)
            {
                t = a.GetTargetAgent();
                if (t != null && t.IsActive() && t.IsHuman && MissionTeams.Enemies(a.Team, t.Team))
                {
                    dT = a.Position.AsVec2.Distance(t.Position.AsVec2);
                    fleeing = t.IsRunningAway;
                }
                else t = null;
            }
            bool meleeHit = now - a.LastMeleeHitTime <= MeleeRecent || now - a.LastMeleeAttackTime <= MeleeRecent;
            bool melee = meleeHit || (t != null && !fleeing && dT <= _last);
            bool march = f != null && MarchToContact.IsMarching(f);
            bool charge = false;
            if (f != null && IsCharge(f))
            {
                float age = MarchToContact.ChargeAge(f);
                charge = age < 0f || age <= MarchToContact.FreshFor;
            }
            if (man.Reinf && f != null && a.Position.AsVec2.DistanceSquared(f.CachedMedianPosition.AsVec2) < ReinfJoin * ReinfJoin) man.Reinf = false;
            sit = panic || withdraw ? SFlee : (melee ? SMelee : (t != null && fleeing ? SChase : (man.Reinf ? SReinf : (charge ? SCharge : (march ? SMarch : (_siege ? SSiege : SOther))))));

            // wyjatki
            int i = a.Index;
            if (i < 0 || i >= Size) return XIdx;
            if (a.MissionPeer != null) return XPeer;
            if (!a.IsAIControlled) return XPlayer;
            if (panic) return XPanic;
            if (withdraw) return XWithdraw;
            if (f == null) return XNoForm;
            if (a.IsDetachedFromFormation) return XDetached;
            if (!_yours && a.Team != null && a.Team.IsPlayerTeam) return XYours;
            if (f.GetReadonlyMovementOrderReference().OrderEnum == MovementOrder.MovementOrderEnum.Follow) return XFollow;
            if (march) return XMarch;

            // d i budzet (takze dla swiezej szarzy - B2: ile decyzji krok wstrzymala)
            float dF = float.MaxValue;
            var eq = f.CachedClosestEnemyFormation;
            var ef = eq != null ? eq.Formation : null;
            if (ef != null && ef.CountOfUnits > 0)
            {
                dF = a.Position.AsVec2.Distance(ef.CachedMedianPosition.AsVec2) - 0.5f * ef.Depth;
                if (dF < 0f) dF = 0f;
            }
            float d = Math.Min(dF, dT);
            bool chase = t != null && fleeing && dT <= dF;
            float v = 0f;
            try { v = a.GetMaximumForwardUnlimitedSpeed(); } catch { v = 0f; }
            bool fresh = MarchToContact.ChargeFresh(f);
            if (fresh)
            {
                if (d != float.MaxValue && v >= 0.8f && T > 0f && d > _last && d > Math.Max(0f, ratio - Reserve(f, false)) * T * v) _freshWalk++;
                return XFresh;
            }
            if (melee) return XMelee;
            if (d == float.MaxValue) return XNoD;                  // W1: nie wiadomo, gdzie wrog - bez capu
            if (v < 0.8f || T <= 0f) return XNoV;

            float res = Reserve(f, true);
            float budget = Math.Max(0f, ratio - res) * T * v;
            _budgetSum += budget; _budgetN++;
            _vSum += v; _vN++;
            _tSum += T;
            if (!man.VLogged) { man.VLogged = true; _vB[v < 3.5f ? 0 : (v < 4.0f ? 1 : (v < 4.5f ? 2 : 3))]++; }
            bool walk;
            if (man.Walk) walk = !(d < budget - Hyst || (!chase && d <= _last));
            else walk = d > budget && (chase || d > _last);
            if (walk)
            {
                float ws = 0f;
                try { ws = a.WalkSpeedCached; } catch { ws = 0f; }
                if (ws < 0.3f || ws > WalkTop) ws = WalkTop;
                cap = ws - WalkMargin;
            }
            return -1;
        }

        /// <summary>Zapas tchu na dobiegniecie: AiArriveWithBreathPercent, pod ostrzalem szyku (UnderRangedAttackRatio &gt;= 0.33) najwyzej 25%.</summary>
        private static float Reserve(Formation f, bool count)
        {
            float res = _reserve;
            bool fire = false;
            try { var q = f.QuerySystem; fire = q != null && q.UnderRangedAttackRatio >= FireRatio; } catch { fire = false; }
            if (fire && res > FireReserve) { res = FireReserve; if (count) _underFire++; }
            return res;
        }

        /// <summary>W2: sonda animacji - WalkMode x predkosc x limit (GetMaximumSpeedLimit, surowa wartosc) dla probki ludzi w ruchu, do 10 na grupe co 2 s.</summary>
        private static void Probe(Agent a, Man man, Formation f, float speed)
        {
            int g = f != null && MarchToContact.IsMarching(f) ? 0 : (man.Walk ? 1 : 2);
            if (_probeLeft[g] <= 0) return;
            _probeLeft[g]--;
            bool wm = false;
            float lim = -1f;
            try { wm = a.WalkMode; } catch { wm = false; }
            try { lim = a.GetMaximumSpeedLimit(); } catch { lim = -1f; }
            int vb = speed < 1.5f ? 0 : (speed < 1.8f ? 1 : (speed < 2.2f ? 2 : 3));
            _pb[g, vb, wm ? 1 : 0]++;
            int lb = lim < 0f ? 0 : (lim <= 1.0f ? 1 : (lim < 1.8f ? 2 : (lim < 2.2f ? 3 : 4)));
            _pl[g, lb]++;
        }

        /// <summary>Koniec taktu FieldCraft: D3 (szyki rozciagniete) i koszt taktu.</summary>
        internal static void EndTick(double costMs)
        {
            if (!_live) return;
            _costSum += costMs;
            _costN++;
            if (costMs > _costMax) _costMax = costMs;
            if (!_stretchNow || _stretchCount.Count == 0) return;
            try
            {
                foreach (var kv in _stretchCount)
                {
                    int tot = kv.Value & 0xFFFF, walk = kv.Value >> 16;
                    var f = kv.Key;
                    if (tot < StretchMinMen || walk <= StretchShare * tot || f == null) continue;
                    if (MarchToContact.IsMarching(f) || IsCharge(f)) continue;
                    _stretchF.Add(f);
                    _stretchSecs += StretchEvery;
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteBreath, "AiBreath.Stretch", e); }
            _stretchCount.Clear();
        }

        // ------------------------------------------------------------ kara FieldCraft

        internal const int PenWound = 0, PenWind = 1, PenKey = 2;

        /// <summary>FieldCraft zapisal kare (limit &lt; 0.999): przyczyna wiazaca i czy gorny limit pochodzi z capu oddechu.</summary>
        internal static void NotePenalty(int cause, bool breathCap)
        {
            if (!_seen) return;
            if (cause == PenWound) _penWound++; else if (cause == PenKey) _penKey++; else _penWind++;
            if (breathCap) _penBreath++;
        }

        /// <summary>FieldCraft zdjal kare zapisem capu (marsz albo oddech) zamiast -1 (punkt 4.4).</summary>
        internal static void NotePenaltyReleaseCap() { if (_seen) _penRelCap++; }

        /// <summary>FieldCraft zdjal cap oddechu od razu (-1) po decyzji "bieg".</summary>
        internal static void NoteRelease() { _release++; }

        // ------------------------------------------------------------ linia misji

        /// <summary>FieldCraft.OnEndMission: linia "Oddech AI (144)" (raz na bitwe), capy zdjete.</summary>
        internal static void End(Mission m)
        {
            try { if (_seen && !_written) Write(); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteBreath, "AiBreath.Write", e); }
            ClearCaps();
            Reset();
            _m = null;
        }

        private static string P(float a, float b) { return b > 0f ? (100f * a / b).ToString("0", Inv) + "%" : "-"; }
        private static string A(float sum, float n, string fmt) { return n > 0f ? (sum / n).ToString(fmt, Inv) : "-"; }

        private static void Write()
        {
            _written = true;
            var c = Settings.Current;
            var sb = new StringBuilder(2400);
            sb.Append("Oddech AI (144): misja bitwa (").Append(_kindTxt).Append("), klucz ").Append(c != null && c.AiSavesBreath ? "TAK" : "NIE")
              .Append(" - ").Append(_why).Append(" (czynne ").Append(_realSecs.ToString("0", Inv)).Append(" s, na sucho ").Append(_drySecs.ToString("0", Inv)).Append(" s)");
            if (c != null)
                sb.Append(" (zapas ").Append(Clamp(c.AiArriveWithBreathPercent, 20, 80)).Append("%, pod ostrzalem ").Append((int)(FireReserve * 100f))
                  .Append("%, ostatnie ").Append(Clamp(c.AiRunsLastMetres, 10f, 60f).ToString("0", Inv)).Append(" m, krok ").Append((WalkTop - WalkMargin).ToString("0.00", Inv))
                  .Append(" m/s, twoi ").Append(c.AiSavesBreathYourTroops ? "TAK" : "NIE").Append(", oblezenia ").Append(c.AiSavesBreathInSieges ? "TAK" : "NIE")
                  .Append(", marsz krokiem ").Append(c.MarchAtWalkingPace ? "TAK" : "NIE").Append(", latka AdjustSpeedLimit ").Append(MarchToContact.SpeedPatched ? "TAK" : "NIE").Append(')');
            sb.Append(" | pieszych AI: strona gracza ").Append(_nFoot[0]).Append(" / wrog ").Append(_nFoot[1]);
            sb.Append(" | zadyszka: strona gracza ").Append(_nWinded[0]).Append(" (").Append(P(_nWinded[0], _nFoot[0])).Append("), ").Append(_windSecs[0].ToString("0", Inv))
              .Append(" s, ponizej 10% puli ").Append(_nBelow10[0]).Append(" / wrog ").Append(_nWinded[1]).Append(" (").Append(P(_nWinded[1], _nFoot[1])).Append("), ")
              .Append(_windSecs[1].ToString("0", Inv)).Append(" s, ponizej 10% puli ").Append(_nBelow10[1]);
            sb.Append(" | bieg: strona gracza ").Append(P(_runSecs[0], _footSecs[0])).Append(" czasu (").Append(_runSecs[0].ToString("0", Inv)).Append(" z ")
              .Append(_footSecs[0].ToString("0", Inv)).Append(" s), wrog ").Append(P(_runSecs[1], _footSecs[1])).Append(" (").Append(_runSecs[1].ToString("0", Inv)).Append(" z ")
              .Append(_footSecs[1].ToString("0", Inv)).Append(" s); sekundy biegu:");
            for (int i = 0; i < SCount; i++) sb.Append(i > 0 ? "," : "").Append(' ').Append(SitName[i]).Append(' ').Append(_sitSecs[i].ToString("0", Inv));
            sb.Append("; najdluzszy ciagly bieg sr. ").Append(A(_runSum, _runN, "0")).Append(" s (biegow ").Append(_runN).Append("), najdluzej ")
              .Append(_runMax.ToString("0", Inv)).Append(" s (").Append(SitName[_runMaxSit]).Append("), ponad 20 s ").Append(_run20).Append(", ponad 30 s ").Append(_run30);
            sb.Append(" | pierwsze zwarcie (pasek do 0.2 s po pierwszym ciosie wrecz): strona gracza sr. ").Append(A(100f * _fmSum[0], _fmN[0], "0")).Append("% (")
              .Append(_fmN[0]).Append("; szarzujace ").Append(A(100f * _fmChSum[0], _fmChN[0], "0")).Append("% z ").Append(_fmChN[0]).Append("), wrog sr. ")
              .Append(A(100f * _fmSum[1], _fmN[1], "0")).Append("% (").Append(_fmN[1]).Append("; szarzujace ").Append(A(100f * _fmChSum[1], _fmChN[1], "0")).Append("% z ")
              .Append(_fmChN[1]).Append("); rozklad <10% ").Append(_fmB[0]).Append(" / 10-30% ").Append(_fmB[1]).Append(" / 30-60% ").Append(_fmB[2]).Append(" / >60% ")
              .Append(_fmB[3]).Append("; posilki (po rozstawieniu, ponad ").Append((int)ReinfFrom).Append(" m od szyku) ").Append(_reinfN).Append(", od pojawienia do zwarcia sr. ")
              .Append(A(_reinfMeleeSum, _reinfMeleeN, "0")).Append(" s (").Append(_reinfMeleeN).Append(')');
            sb.Append(" | kara FieldCraft (zapisy): rany ").Append(_penWound).Append(", zadyszka i strefa 10% ").Append(_penWind).Append(", klawisz chodu ").Append(_penKey)
              .Append("; z gornym limitem z capu oddechu ").Append(_penBreath).Append(", zdjec kary zapisem capu zamiast -1 ").Append(_penRelCap);
            sb.Append(" | zasada (").Append(_realSecs > _drySecs ? "naprawde" : "na sucho - co by zrobila").Append("): decyzji ").Append(_decisions)
              .Append(", ludzi z capem ").Append(_menCap).Append(", sekund kroku ").Append(_walkSecs.ToString("0", Inv)).Append(" (po zwarciu ").Append(_walkAfterMelee.ToString("0", Inv))
              .Append("), przejsc krok->bieg ").Append(_toRun).Append(", sr. budzet ").Append(A(_budgetSum, _budgetN, "0")).Append(" m, sr. v_bieg ").Append(A(_vSum, _vN, "0.00"))
              .Append(" m/s (ludzi: <3.5 ").Append(_vB[0]).Append(" / 3.5-4.0 ").Append(_vB[1]).Append(" / 4.0-4.5 ").Append(_vB[2]).Append(" / >=4.5 ").Append(_vB[3])
              .Append("), sr. T ").Append(A(_tSum, _vN, "0")).Append(" s, decyzji pod ostrzalem (zapas 25%) ").Append(_underFire)
              .Append("; swieza szarza (B2): decyzji krok wstrzymanych ").Append(_freshWalk).Append("; wyjatki:");
            for (int i = 0; i < XCount; i++) sb.Append(i > 0 ? "," : "").Append(' ').Append(XName[i]).Append(' ').Append(_exempt[i]);
            sb.Append("; zapisow PostSpeed ").Append(Volatile.Read(ref _post)).Append(", zdjec od razu (-1) ").Append(_release)
              .Append("; szyki bez marszu i szarzy z ponad 30% ludzi z capem (D3) ").Append(_stretchF.Count).Append(" (").Append(_stretchSecs.ToString("0", Inv)).Append(" s)")
              .Append("; oblezenie: odlaczeni w ruchu ponad 2.2 m/s ").Append(_detFastSecs.ToString("0", Inv)).Append(" s");
            sb.Append(" | marsz krokiem (134c): limit krokiem ").Append(MarchToContact.WalkHits).Append(", dogonienie przyciete do 2.1 m/s ").Append(MarchToContact.WalkCatchHits);
            sb.Append(" | sonda (W2, co 2 s do ").Append(ProbePerGroup).Append(" na grupe; predkosc m/s: ludzi (z WalkMode); limit GetMaximumSpeedLimit surowy brak/<=1.0/1.0-1.8/1.8-2.2/>2.2):");
            string[] gname = { "marsz", "krok z oddechu", "inni w ruchu" };
            string[] vname = { "<1.5", "1.5-1.8", "1.8-2.2", ">2.2" };
            for (int g = 0; g < 3; g++)
            {
                sb.Append(g > 0 ? ";" : "").Append(' ').Append(gname[g]).Append(':');
                for (int vb = 0; vb < 4; vb++) sb.Append(' ').Append(vname[vb]).Append(' ').Append(_pb[g, vb, 0] + _pb[g, vb, 1]).Append(" (").Append(_pb[g, vb, 1]).Append(')');
                sb.Append(", limit ").Append(_pl[g, 0]).Append('/').Append(_pl[g, 1]).Append('/').Append(_pl[g, 2]).Append('/').Append(_pl[g, 3]).Append('/').Append(_pl[g, 4]);
            }
            sb.Append(" | ");
            WindedShow.Append(sb, c);
            sb.Append(" | potkniecia oddech ").Append(_fails).Append(" (wypadli ").Append(_out).Append(") | koszt sr. ").Append(_costN > 0 ? (_costSum / _costN).ToString("0.000", Inv) : "-")
              .Append(" ms, najwiecej ").Append(_costMax.ToString("0.000", Inv)).Append(" ms na takt 0.2 s.");
            Log.Info(sb.ToString());
        }

        private static void Reset()
        {
            _battle = _live = _real = _siege = _seen = _written = _candDone = false;
            _why = "-"; _kindTxt = "-";
            _liveSince = -1f; _realSecs = _drySecs = 0f; _lastNow = -1f; _tick = 0;
            for (int i = 0; i < 2; i++) { _nFoot[i] = _nWinded[i] = _nBelow10[i] = 0; _footSecs[i] = _runSecs[i] = _windSecs[i] = 0f; _fmN[i] = _fmChN[i] = 0; _fmSum[i] = _fmChSum[i] = 0f; }
            for (int i = 0; i < SCount; i++) _sitSecs[i] = 0f;
            for (int i = 0; i < 4; i++) { _fmB[i] = 0; _vB[i] = 0; }
            for (int i = 0; i < XCount; i++) _exempt[i] = 0;
            _runN = _run20 = _run30 = 0; _runMaxSit = SOther; _runSum = _runMax = 0f;
            _reinfN = _reinfMeleeN = 0; _reinfMeleeSum = 0f;
            _penWound = _penWind = _penKey = _penBreath = _release = _penRelCap = 0;
            _decisions = _menCap = _toRun = _budgetN = _vN = _underFire = _freshWalk = _out = _fails = 0;
            _walkSecs = _walkAfterMelee = _budgetSum = _vSum = _tSum = 0f;
            _stretchCount.Clear(); _stretchF.Clear(); _stretchAcc = _stretchSecs = 0f; _stretchNow = false;
            _detFastSecs = 0f;
            Array.Clear(_pb, 0, _pb.Length);
            Array.Clear(_pl, 0, _pl.Length);
            _probeLeft[0] = _probeLeft[1] = _probeLeft[2] = 0;
            _probeAcc = 0f;
            _costSum = _costMax = 0.0; _costN = 0;
            Interlocked.Exchange(ref _post, 0);
            WindedShow.Reset();
        }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }

        /// <summary>Segment linii startu sesji NB2 (RoutHold) - ustawienia paczki 144.</summary>
        internal static void Session(StringBuilder sb, Settings c)
        {
            sb.Append(" | AI Saves Breath ").Append(c.AiSavesBreath ? "TAK" : "NIE")
              .Append(c.AiSavesBreath && !(c.SprintFatigueEnabled && c.SpeedCapOnlyWhenWinded) ? " (tylko na sucho: Sprint Fatigue albo Speed Cap Only When Winded NIE)" : "")
              .Append(" (pieszy AI biegnie tylko na budzet (pasek - zapas ").Append(Clamp(c.AiArriveWithBreathPercent, 20, 80)).Append("%, pod ostrzalem ")
              .Append((int)(FireReserve * 100f)).Append("%) x czas biegu 127 x v, dalej krok ").Append((WalkTop - WalkMargin).ToString("0.00", Inv)).Append(" m/s, ostatnie ")
              .Append(Clamp(c.AiRunsLastMetres, 10f, 60f).ToString("0", Inv)).Append(" m biegiem; twoi ").Append(c.AiSavesBreathYourTroops ? "TAK" : "NIE")
              .Append(", oblezenia ").Append(c.AiSavesBreathInSieges ? "TAK" : "NIE").Append("; marsz krokiem ").Append(c.MarchAtWalkingPace ? "TAK" : "NIE")
              .Append(" (tempo szyku ").Append(MarchToContact.WalkPace.ToString("0.00", Inv)).Append(", limit ").Append(MarchToContact.WalkLineMax.ToString("0.00", Inv))
              .Append(", dogonienie ").Append(MarchToContact.WalkCatchUpMax.ToString("0.0", Inv)).Append(" m/s); zmeczenie widac ").Append(c.WindedShowsIt ? "TAK" : "NIE")
              .Append(" (poza ").Append(Clamp(c.WindedPose, 0, 3)).Append("), oddech slychac ").Append(c.WindedBreathSound ? "TAK" : "NIE")
              .Append("; linia 'Oddech AI (144)' po bitwie)");
        }
    }
}
