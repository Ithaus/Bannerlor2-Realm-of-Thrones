using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 204 "Jazda na strzelca" (26.10, Jeff ok. 22:15 po bitwie z WGRANIEM 126: "w bitwie strzelam do jezdzca nawet jak nie trafie a jezdzcy widza ze do nich
    /// strzelam to nie stoja jak kretyni tylko jada w moim kierunku, maja mnie odstraszac nawet jak strzelam do ich piechoty, nawet jak strzelam obok nich ...
    /// za malo sa agresywni maja oslaniac i pilnowac i bronic sie"; zasada stala "AI robi wszystko aby nie dac sie zabic"). Klucz RidersRunDownShooters.
    /// Przyczyna (log Jeffa 26.10 21:57 / 22:07, projekt 204): kontra 157 wchodzila tylko po trafieniu samego jezdzca, pomijala szyk w "szarzy" (przejazd RBM
    /// 56 z 58 s), najwyzej 2 jezdzcow na 20 s; RBM nie szarzuje na konnego strzelca (RBMBehaviorCavalryCharge ChargeCavalry / ChargeHorseArchers = false,
    /// cel tylko ClosestSignificantlyLargeEnemyFormation), a rozkazy calego szyku nadpisuje kazdy wolny tick zachowania - zostaje sterowanie pojedynczym
    /// jezdzcem (wzor 148/157 i NavalDLC Quest5SetPieceBattleMissionController: SetAutomaticTargetSelection(false) + SetTargetAgent).
    ///
    /// WYKRYCIE (glowny watek, kolejka zdarzen 140 - ShieldsUp.Drain wola OnFire przed bramkami 140, wiec dziala takze przy kluczu 140 NIE): konny czlowiek
    /// (MountAgent != null) z druzyny wroga, ktorego pocisk: trafil czlowieka albo konia strony (trafienie), upadl w 4.5 m od jej ludzi (pudlo - punkt zderzenia,
    /// MissRadius 140), strzal AI z celem w czlowieka strony (cel AI), strzal gracza / AI bez celu - punkt upadku z balistyki 140 w 8 m od ludzi (pudlo) albo
    /// tor wylot-upadek w 8 m od krawedzi szyku strony (przelot; promien szyku najwyzej 40 m - RadiusCap 148). Zapytania o ludzi najwyzej 12 na klatke
    /// (QueriesPerFrame 140, licznik "ponad limit klatki"). Strzelec zauwazony = Threat; kazdy jego dalszy strzal liczy sie do okna 5 s (dwie strony bitwy -
    /// kazdy jego pocisk leci w te strone).
    /// ODPOWIEDZ (pass co 1 s, sterowanie co 0.25 s - PassEvery / TrackEvery 148): poziom L1 oddzial (2 + padlo od niego - PerVictim 148 jeden za jednego)
    /// z najblizszego szyku konnicy, ktory NIE szarzuje (szarza wodza zostaje); L2 caly szyk (strzelec w 110 m od szyku konnicy - zasieg szarzy oslony RBM
    /// i odleglosc zatrzymania szarzy RBM, albo ShieldsUpFormationShots pociskow w 5 s, albo ktos padl od niego): wszyscy wolni jezdzcy najblizszego szyku
    /// (takze w szarzy RBM, poza zwarciem) i kazdego szyku konnicy w 110 m; L3 cala jazda (dalszy ogien - znowu tyle pociskow w 5 s po 5 s od L2): kazdy szyk
    /// konnicy strony w 250 m (MaxFromMain 148, noca polowa). Samotny nekajacy (bez nietknietej formacji swojej strony w 80 m - IntactRadius 148) zawsze
    /// co najmniej L2; strona, ktora wrog lamie (Broke 148), tylko L1 - chyba ze nekajacy samotny.
    /// Jezdziec: AI, konno, z bronia do walki wrecz (konny lucznik z amunicja - rola strzelecka), nie bohater (149), nie chorazy (RBM), nie smok, nie umarly,
    /// nie w panice / odwrocie (139/143), nie w poscigu 148, nie zajety (CanBeAssignedForScriptedMovement - gra), nie w zwarciu (wrog inny niz strzelec w 6 m -
    /// RaiseNear 140; najwyzej 64 zapytania na pass - QueriesPerPass 140), kon nie zajechany (HorseAiRestBelow 136), w 250 m od strzelca, po przerwie 30 s
    /// (CounterCool 157). Szyki gracza (PlayerForm), szyki w odwrocie (rozkaz Retreat albo BehaviorRetreat) - nigdy.
    /// Sterowanie: cel na strzelca + pozycja skryptowa co 0.25 s (najwyzej 96 na krok - TrackPerStep 148): co drugi jezdziec w punkt przeciecia (wyprzedzenie
    /// min(v_strzelca x d / v_jezdzca, d)), co drugi odcina ucieczke (punkt 30 m za strzelcem od strony naszego szyku - odleglosc oslony RBM pod ostrzalem);
    /// w 30 m prosto na niego. Strzelec w 15 m od nietknietego szyku pieszego swojej strony (CounterFootGap 157): zaslona 30 m przed wlasnym szykiem pieszym,
    /// bez wjazdu w wlocznie. Lucznicy (piesi i konni) strony w zasiegu (MissileRangeAdjusted / MaximumMissileRange gry) dostaja cel na strzelca, gdy nikt
    /// z ich strony nie stoi w 8 m od niego (LandRadius 140 - FF); najwyzej 96 nowych na pass.
    /// Koniec epizodu: strzelec padl (od jezdzcow / innych), odpedzony (dalej niz jego zasieg od kazdego szyku strony i cisza ShieldsUpHoldSeconds), wszyscy
    /// jezdzcy zawroceni (konie, smycz 250 m od miejsca szyku, odwrot wodza, panika), koniec bitwy, klucz. Jezdziec po powrocie: cel automatyczny, pozycja
    /// skryptowa zdjeta (jak 148), przerwa 30 s.
    /// Watki: OnFire / pass / krok - glowny watek; OnHit bywa z ticku asynchronicznego - tylko tablice wedlug Agent.Index i Interlocked, ciosy w strzelca do
    /// kolejki pod lockiem. Nic nie trafia do zapisu - stan jednej misji (Begin / End). Wyjatek = licznik "potkniecia jazda" (bez wylacznika).
    /// Linie: "Jazda na strzelca (204) start / poziom / kontakt / koniec" (najwyzej 12 na bitwe) i "Jazda na strzelca (204): misja ..." raz na bitwe polowa.
    /// </summary>
    internal static partial class HorsePursuit
    {
        // ------------------------------------------------------------ stale 204 (zrodla - opis klasy)
        internal const float HuntClose = 110f;       // RBM: zasieg szarzy oslony skrzydla pod ostrzalem i odleglosc zatrzymania szarzy
        internal const float HuntScreen = 30f;       // RBM: oslona skrzydla pod ostrzalem 30 m przy wlasnej piechocie
        private const int HuntMaxLines = 12;         // linii epizodow na bitwe
        private const int HuntBlowCap = 256;

        private const byte RoleIntercept = 0, RoleCut = 1, RoleRanged = 2;
        // wykrycie
        private const int DHit = 0, DMiss = 1, DPass = 2, DAi = 3, DKinds = 4;
        private static readonly string[] DName = { "trafienie", "pudlo", "przelot", "cel AI" };
        // wynik epizodu
        private const int HDownRiders = 0, HDownOthers = 1, HAway = 2, HLeash = 3, HHorses = 4, HRetreat = 5, HEnded = 6, HOther = 7, HKinds = 8;
        private static readonly string[] HName = { "padl od jezdzcow", "padl od innych", "odpedzony", "uszedl poza smycz", "konie", "odwrot wodza", "koniec bitwy", "inne" };
        // koniec jezdzca / lucznika
        private const int UGone = 0, UPanic = 1, UHorse = 2, ULeash = 3, URetreat = 4, UPlayer = 5, UKey = 6, UEnded = 7, UOther = 8, UThreat = 9, URange = 10, UFriend = 11, UKinds = 12;
        // nie wyslani (rozni ludzie)
        private const int NwMelee = 0, NwPanic = 1, NwChase = 2, NwHero = 3, NwBanner = 4, NwHorse = 5, NwFar = 6, NwPlayer = 7, NwLosing = 8, NwBusy = 9, NwCool = 10,
                          NwQuery = 11, NwOther = 12, NwKinds = 13;
        private static readonly string[] LName = { "-", "oddzial", "szyk", "cala jazda" };

        private sealed class Threat
        {
            public Agent S;
            public int Side, SSide, First = -1, Level, MeleeEver, Melee, Ranged, RidersLost, Blows, LastEnd = -1, Ep;
            public int Hits, Misses, Pass, AiTgt;
            public readonly Queue<float> Shots = new Queue<float>();
            public float LastFire, L2At, Start, FirstContact = -1f, MaxFar;
            public bool Active, Screen, LosingNoted;
            public Vec2 ScreenC = Vec2.Invalid, EscC = Vec2.Invalid;
            public float ScreenR;
            public readonly List<Hunter> Hs = new List<Hunter>();
        }

        private sealed class Hunter
        {
            public Agent A;
            public Formation F;
            public Threat T;
            public byte Role;
            public float Start, Range;
            public int Fails;
        }

        private struct FI
        {
            public Formation F;
            public Vec2 C;
            public float R;
            public int N;
            public bool Cav, Ha, Foot, Rng, Player, Retreat, Charging;
        }

        // ------------------------------------------------------------ stan misji (glowny watek)
        private static bool _hKey;
        private static float _hold = 8f;
        private static int _shots3 = 3, _hFrameQ, _hQPass, _hNewRanged, _hRot;
        private static readonly Dictionary<Agent, Threat> Threats = new Dictionary<Agent, Threat>();
        private static readonly List<Threat> ThreatList = new List<Threat>();
        private static readonly List<Threat> ThreatTmp = new List<Threat>();
        private static readonly Dictionary<Agent, Hunter> Hunters = new Dictionary<Agent, Hunter>();
        private static readonly List<Hunter> HList = new List<Hunter>();
        private static readonly Dictionary<Agent, float> HuntCool = new Dictionary<Agent, float>();
        private static readonly Dictionary<Agent, int> DownBy = new Dictionary<Agent, int>();
        private static readonly List<FI>[] HForms = { new List<FI>(), new List<FI>() };
        private static readonly List<Agent> HUnits = new List<Agent>();
        private static readonly Action<Agent> HCollect = a => HUnits.Add(a);
        private static readonly MBList<Agent> HBuf = new MBList<Agent>();
        private static readonly List<KeyValuePair<float, Agent>> HCand = new List<KeyValuePair<float, Agent>>();
        private static readonly List<int> RangedForms = new List<int>();
        private static readonly List<float> RangedRange = new List<float>();
        private static readonly StringBuilder HFormsTxt = new StringBuilder(128);
        // tablice wedlug Agent.Index (OnHit z dowolnego watku): strzelcy z Threat, lucznicy z celem 204 (rola strzelecka)
        private static Agent[] _hunted, _aiming;
        private static readonly object HLock = new object();
        private static readonly List<int> BlowQ = new List<int>();

        // ------------------------------------------------------------ liczniki misji
        private static readonly int[] HDet = new int[DKinds], HOut = new int[HKinds], HEpL = new int[4], UEnd = new int[UKinds], Nw = new int[NwKinds];
        private static readonly HashSet<int> HShooters = new HashSet<int>(), HRiders = new HashSet<int>(), HHorseArch = new HashSet<int>(), HFootArch = new HashSet<int>();
        private static readonly HashSet<long> NwSeen = new HashSet<long>();
        private static readonly HashSet<Formation> HFormsSent = new HashSet<Formation>(), NwPlayerForms = new HashSet<Formation>();
        private static int _hPlayerShooter, _hFrameOver, _hShotsSeen, _hEp, _hSentCharge, _hRangedShots, _hContactN, _hFromPlayer, _hLeft, _hRidersLost, _hSets,
                           _hLines, _hStum, _hDropped, _hEscal, _hScreenSec, _hRangedCap, _hFriendBlock;
        private static float _hContactSum, _hContactMin = float.MaxValue, _hContactMax, _hMaxFar;
        private static int _hBlows, _hFFMissile, _hFFMelee;   // Interlocked (OnHit)

        // ------------------------------------------------------------ dla innych paczek (dowolny watek)

        /// <summary>BannerCompanies.Pickup: czlowiek pod sterowaniem 204 (jezdziec w poscigu za strzelcem albo lucznik z celem na strzelca). Bez blokady.</summary>
        internal static bool HuntAnyThread(Agent a)
        {
            return CounterAnyThread(a) || In(Volatile.Read(ref _aiming), a);
        }

        private static bool In(Agent[] k, Agent a)
        {
            if (k == null || a == null) return false;
            int i = a.Index;
            return i >= 0 && i < k.Length && ReferenceEquals(k[i], a);
        }

        // ------------------------------------------------------------ poczatek / koniec misji

        private static void HuntBegin(Settings c)
        {
            _hKey = c != null && c.RidersRunDownShooters;
            _hold = c != null ? Math.Max(2f, Math.Min(30f, c.ShieldsUpHoldSeconds)) : 8f;           // jak ShieldsUp.Begin
            _shots3 = c != null ? Math.Max(1, Math.Min(20, c.ShieldsUpFormationShots)) : 3;          // jak ShieldsUp.Begin
            HuntTestBegin(c);
        }

        private static void HuntClear()
        {
            Volatile.Write(ref _hunted, null);
            Volatile.Write(ref _aiming, null);
            Threats.Clear(); ThreatList.Clear(); ThreatTmp.Clear(); Hunters.Clear(); HList.Clear(); HuntCool.Clear(); DownBy.Clear();
            HForms[0].Clear(); HForms[1].Clear(); HUnits.Clear(); HBuf.Clear(); HCand.Clear(); RangedForms.Clear(); RangedRange.Clear();
            lock (HLock) BlowQ.Clear();
            Array.Clear(HDet, 0, HDet.Length); Array.Clear(HOut, 0, HOut.Length); Array.Clear(HEpL, 0, HEpL.Length);
            Array.Clear(UEnd, 0, UEnd.Length); Array.Clear(Nw, 0, Nw.Length);
            HShooters.Clear(); HRiders.Clear(); HHorseArch.Clear(); HFootArch.Clear(); NwSeen.Clear(); HFormsSent.Clear(); NwPlayerForms.Clear();
            _hFrameQ = _hQPass = _hNewRanged = _hRot = 0;
            _hPlayerShooter = _hFrameOver = _hShotsSeen = _hEp = _hSentCharge = _hRangedShots = _hContactN = _hFromPlayer = _hLeft = _hRidersLost = _hSets = 0;
            _hLines = _hStum = _hDropped = _hEscal = _hScreenSec = _hRangedCap = _hFriendBlock = 0;
            _hContactSum = 0f; _hContactMin = float.MaxValue; _hContactMax = 0f; _hMaxFar = 0f;
            Interlocked.Exchange(ref _hBlows, 0); Interlocked.Exchange(ref _hFFMissile, 0); Interlocked.Exchange(ref _hFFMelee, 0);
            HuntTestClear();
        }

        /// <summary>End (przed linia): epizody zakonczone ("koniec bitwy"), ludzie oddani grze, nekajacy testu zwolniony; potem liczba ludzi wciaz pod
        /// sterowaniem (oczekiwane 0).</summary>
        private static void HuntEndAll(float now)
        {
            ThreatTmp.Clear();
            ThreatTmp.AddRange(ThreatList);
            for (int i = 0; i < ThreatTmp.Count; i++)
            {
                try { EndThreat(ThreatTmp[i], HEnded, now); } catch (Exception e) { HStumble("HorsePursuit.JazdaKoniec", e); }
            }
            ThreatTmp.Clear();
            for (int i = HList.Count - 1; i >= 0; i--)
            {
                if (i >= HList.Count) continue;
                try { EndHunter(HList[i], UEnded, now, false, false); }
                catch (Exception e) { HStumble("HorsePursuit.JazdaKoniec", e); if (i < HList.Count) DropHunter(HList[i]); }
            }
            try { TestEnd(Mission.Current, "koniec bitwy", now); } catch (Exception e) { HStumble("HorsePursuit.JazdaTest", e); }
            _hLeft = CountIn(Volatile.Read(ref _countering)) + CountIn(Volatile.Read(ref _aiming)) + HList.Count;
        }

        private static void HStumble(string where, Exception e)
        {
            _hStum++;
            Stumble(where, e);
        }

        // ------------------------------------------------------------ wykrycie (ShieldsUp.Drain, glowny watek)

        /// <summary>ShieldsUp.Drain na poczatku kolejki klatki: limit zapytan klatki od zera.</summary>
        internal static void FireFrame() { _hFrameQ = 0; }

        /// <summary>Zdarzenie z kolejki 140 (k: 1 strzal - p wylot, vel predkosc; 2 zderzenie - v ofiara albo null = pudlo, p punkt zderzenia).</summary>
        internal static void OnFire(Mission m, byte k, Agent s, Agent v, Vec3 p, Vec3 vel)
        {
            if (!_live || m == null || s == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;
            try
            {
                float now = m.CurrentTime;
                Hunter rh;
                // strzal lucznika 204 w strzelca (cel ustawiony przez 204 - czy SI gry strzela w nasz cel)
                if (k == 1 && Hunters.TryGetValue(s, out rh) && rh.Role == RoleRanged)
                {
                    Agent tg = null;
                    try { tg = s.GetTargetAgent(); } catch { tg = null; }
                    if (tg != null && (ReferenceEquals(tg, rh.T.S) || ReferenceEquals(tg, rh.T.S.MountAgent))) _hRangedShots++;
                }
                if (!s.IsHuman || s.MountAgent == null || !s.IsActive()) return;
                int ss = SideOf(s.Team);
                if (ss < 0) return;
                int side = 1 - ss;
                Threat t;
                Threats.TryGetValue(s, out t);
                bool test = ReferenceEquals(s, _tA);
                if (k == 1)
                {
                    _hShotsSeen++;
                    if (test) _tShots++;
                    Agent tg = null;
                    if (s.IsAIControlled && !s.IsMainAgent) { try { tg = s.GetTargetAgent(); } catch { tg = null; } }
                    if (tg != null && tg.IsMount) tg = tg.RiderAgent;
                    bool ai = tg != null && tg.IsHuman && tg.IsActive() && SideOf(tg.Team) == side;
                    if (t != null)
                    {
                        Fire(t, now);
                        if (ai) Detect(t, DAi, test);
                        return;
                    }
                    int kind = -1;
                    if (ai) kind = DAi;
                    else
                    {
                        Vec2 land;
                        if (!ShieldsUp.Land(p, vel, out land)) return;
                        if (NearMen(m, land, ShieldsUp.LandRadius, s.Team)) kind = DMiss;
                        else if (PassNear(side, p.AsVec2, land)) kind = DPass;
                    }
                    if (kind < 0) return;
                    t = Notice(s, ss, side, kind, now);
                    Fire(t, now);
                    Detect(t, kind, test);
                    return;
                }
                if (k != 2) return;
                if (v != null)
                {
                    Agent hv = v.IsMount ? v.RiderAgent : v;
                    if (hv == null || !hv.IsHuman || SideOf(hv.Team) != side) return;   // kon bez jezdzca, swoj w swojego
                    if (t == null) { t = Notice(s, ss, side, DHit, now); Fire(t, now); }
                    else t.LastFire = now;
                    Detect(t, DHit, test);
                    return;
                }
                if (!NearMen(m, p.AsVec2, ShieldsUp.MissRadius, s.Team)) return;
                if (t == null) { t = Notice(s, ss, side, DMiss, now); Fire(t, now); }
                else t.LastFire = now;
                Detect(t, DMiss, test);
            }
            catch (Exception e) { HStumble("HorsePursuit.JazdaWykrycie", e); }
        }

        private static void Fire(Threat t, float now)
        {
            t.Shots.Enqueue(now);
            while (t.Shots.Count > 0 && now - t.Shots.Peek() > ShieldsUp.Window) t.Shots.Dequeue();
            while (t.Shots.Count > 64) t.Shots.Dequeue();
            t.LastFire = now;
        }

        private static void Detect(Threat t, int kind, bool test)
        {
            HDet[kind]++;
            if (kind == DHit) t.Hits++; else if (kind == DMiss) t.Misses++; else if (kind == DPass) t.Pass++; else t.AiTgt++;
            if (!test) return;
            _tDet++;
            if (kind == DMiss || kind == DPass) _tMissPass++;
        }

        private static Threat Notice(Agent s, int ss, int side, int kind, float now)
        {
            var t = new Threat { S = s, SSide = ss, Side = side, First = kind, LastFire = now };
            Threats[s] = t;
            ThreatList.Add(t);
            MarkIn(ref _hunted, s, true);
            if (HShooters.Add(s.Index) && s.IsMainAgent) _hPlayerShooter++;
            return t;
        }

        /// <summary>Ktos z ludzi strony (wrog druzyny strzelca) w promieniu - najwyzej QueriesPerFrame zapytan na klatke.</summary>
        private static bool NearMen(Mission m, Vec2 p, float r, Team shooterTeam)
        {
            if (_hFrameQ >= ShieldsUp.QueriesPerFrame) { _hFrameOver++; return false; }
            _hFrameQ++;
            bool any = false;
            m.GetNearbyEnemyAgents(p, r, shooterTeam, HBuf);
            for (int i = 0; i < HBuf.Count; i++)
            {
                var a = HBuf[i];
                if (a != null && a.IsHuman && a.IsActive()) { any = true; break; }
            }
            HBuf.Clear();
            return any;
        }

        /// <summary>Tor wylot-upadek w LandRadius od krawedzi szyku strony (srodek szyku do odcinka minus promien) - szyki z ostatniego passu.</summary>
        private static bool PassNear(int side, Vec2 a, Vec2 b)
        {
            var fl = HForms[side];
            Vec2 ab = b - a;
            float l2 = ab.x * ab.x + ab.y * ab.y;
            for (int i = 0; i < fl.Count; i++)
            {
                var fi = fl[i];
                Vec2 c = fi.C;
                float u = l2 > 0.01f ? ((c.x - a.x) * ab.x + (c.y - a.y) * ab.y) / l2 : 0f;
                if (u < 0f) u = 0f; else if (u > 1f) u = 1f;
                var q = new Vec2(a.x + ab.x * u, a.y + ab.y * u);
                if (q.Distance(c) - fi.R <= ShieldsUp.LandRadius) return true;
            }
            return false;
        }

        /// <summary>Zasieg strzelca wedlug gry: MissileRangeAdjusted (roznica wysokosci do najblizszego szyku wroga), bez niego MaximumMissileRange.</summary>
        private static float ShooterRange(Agent s)
        {
            float r = 0f;
            try { r = s.MissileRangeAdjusted; } catch { r = 0f; }
            if (r <= 1f) { try { r = s.MaximumMissileRange; } catch { r = 0f; } }
            return r;
        }

        // ------------------------------------------------------------ szyki stron (pass)

        private static bool FormRetreat(Formation f)
        {
            if (f.GetReadonlyMovementOrderReference().OrderEnum == MovementOrder.MovementOrderEnum.Retreat) return true;
            var ai = f.AI;
            return ai != null && ai.ActiveBehavior is BehaviorRetreat;
        }

        private static void BuildHForms(Mission m)
        {
            HForms[0].Clear(); HForms[1].Clear();
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
                    if (f == null || f.CountOfUnits <= 0) continue;
                    var q = f.QuerySystem;
                    if (q == null) continue;
                    var mp = f.CachedMedianPosition;
                    Vec2 c = mp.IsValid ? mp.AsVec2 : f.CachedAveragePosition;
                    if (!c.IsValid) continue;
                    bool cav = q.IsCavalryFormation || q.IsRangedCavalryFormation;
                    var fi = new FI
                    {
                        F = f, C = c, N = f.CountOfUnits, R = Math.Min(RadiusCap, 0.5f * Math.Max(f.Width, f.Depth)),
                        Cav = cav, Ha = q.IsRangedCavalryFormation, Foot = !cav && (q.IsInfantryFormation || q.IsRangedFormation), Rng = !cav && q.IsRangedFormation,
                        Player = PlayerForm(f) || f.HasPlayerControlledTroop
                    };
                    if (!fi.Player)
                    {
                        try { fi.Retreat = FormRetreat(f); } catch { fi.Retreat = false; }
                        try { fi.Charging = ShieldsUp.FormCharging(f); } catch { fi.Charging = false; }
                    }
                    HForms[s].Add(fi);
                }
            }
        }

        // ------------------------------------------------------------ pass (co 1 s, po Pass 148 - BuildSides: panika, nietkniete szyki)

        private static void HuntPass(Mission m, float now)
        {
            var c = Settings.Current;
            _hKey = c != null && c.RidersRunDownShooters;
            _hQPass = 0;
            _hNewRanged = 0;
            BuildHForms(m);
            DrainBlows(m, now);
            HuntersPass(m, now);
            ThreatTmp.Clear();
            ThreatTmp.AddRange(ThreatList);
            for (int i = 0; i < ThreatTmp.Count; i++)
            {
                var t = ThreatTmp[i];
                if (!Threats.ContainsKey(t.S)) continue;
                try { ThreatPass(m, t, now); }
                catch (Exception e)
                {
                    HStumble("HorsePursuit.JazdaStrzelec", e);
                    try { EndThreat(t, HOther, now); } catch { RemoveThreat(t); }
                }
            }
            ThreatTmp.Clear();
            HuntTrack(m, now);
            try { HuntTestStep(m, now, true); } catch (Exception e) { HStumble("HorsePursuit.JazdaTest", e); }
        }

        /// <summary>Kontrola ludzi pod sterowaniem 204 (co 1 s).</summary>
        private static void HuntersPass(Mission m, float now)
        {
            for (int i = HList.Count - 1; i >= 0; i--)
            {
                if (i >= HList.Count) continue;
                var h = HList[i];
                try
                {
                    int why = HunterCheck(m, h, now, true);
                    if (why >= 0) EndHunter(h, why, now, why != UGone && why != UKey && why != UEnded, false);
                }
                catch (Exception e)
                {
                    HStumble("HorsePursuit.JazdaJezdziec", e);
                    if (++h.Fails >= FailsOut)
                    {
                        _hDropped++;
                        try { EndHunter(h, UOther, now, true, false); } catch { DropHunter(h); }
                    }
                }
            }
        }

        /// <summary>-1 = dalej, inaczej przyczyna konca U*. pass = takze kon, smycz, odwrot szyku, klucz, zasieg lucznika.</summary>
        private static int HunterCheck(Mission m, Hunter h, float now, bool pass)
        {
            var a = h.A;
            if (a == null || !a.IsActive()) return UGone;
            if (a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a)) return UPanic;
            if (!ReferenceEquals(a.Formation, h.F)) return UOther;
            if (m.MissionEnded || a.GetComponent<VictoryComponent>() != null) return UEnded;
            if (h.F == null || PlayerForm(h.F)) return UPlayer;
            var s = h.T.S;
            if (s == null || !s.IsActive()) return UThreat;
            if (!pass) return h.Role != RoleRanged && a.MountAgent == null ? UOther : -1;
            if (!_hKey) return UKey;
            if (FormRetreat(h.F)) return URetreat;
            if (h.Role == RoleRanged)
            {
                if (!a.IsRangedCached) return UOther;
                if (a.Position.AsVec2.Distance(s.Position.AsVec2) > h.Range) return URange;
                return -1;
            }
            if (a.MountAgent == null) return UOther;
            if (Blown(HorseS(a))) return UHorse;
            float df = a.Position.AsVec2.Distance(Home(h.F));
            if (df > h.T.MaxFar) h.T.MaxFar = df;
            if (df > _hMaxFar) _hMaxFar = df;
            if (df > _maxFrom) return ULeash;
            return -1;
        }

        private static void ThreatPass(Mission m, Threat t, float now)
        {
            var s = t.S;
            if (s == null || !s.IsActive()) { EndThreat(t, HOther, now); return; }
            while (t.Shots.Count > 0 && now - t.Shots.Peek() > ShieldsUp.Window) t.Shots.Dequeue();
            Vec2 sp = s.Position.AsVec2;
            var fl = HForms[t.Side];
            float dAll = float.MaxValue, dCav = float.MaxValue, dFoot = float.MaxValue;
            int iCav = -1, iNear = -1, iFoot = -1;
            for (int i = 0; i < fl.Count; i++)
            {
                var fi = fl[i];
                float e = Math.Max(0f, sp.Distance(fi.C) - fi.R);
                if (e < dAll) { dAll = e; iNear = i; }
                if (fi.Foot && e < dFoot) { dFoot = e; iFoot = i; }
                if (fi.Cav && !fi.Player && !fi.Retreat && e < dCav) { dCav = e; iCav = i; }
            }
            t.EscC = iNear >= 0 ? fl[iNear].C : Vec2.Invalid;
            bool screen = FootNear(m, s);
            t.Screen = screen && iFoot >= 0;
            if (t.Screen) { t.ScreenC = fl[iFoot].C; t.ScreenR = fl[iFoot].R; }
            if (t.Screen && t.Active) _hScreenSec++;
            if (m.MissionEnded) { EndThreat(t, HEnded, now); return; }
            float range = ShooterRange(s);
            bool quiet = now - t.LastFire >= _hold;
            if (quiet && dAll > range) { EndThreat(t, HAway, now); return; }   // odpedzony (albo zapomniany, gdy bez epizodu)
            if (quiet && !t.Active) { EndThreat(t, HOther, now); return; }     // zauwazony, ale bez jezdzcow i cicho - zapomniany (nowy strzal = nowe wykrycie)
            // poziom
            int lvl = 1;
            int down;
            DownBy.TryGetValue(s, out down);
            if (dCav <= HuntClose || t.Shots.Count >= _shots3 || down > 0) lvl = 2;
            bool lone = !IntactNear(t.SSide, sp, IntactRadius);
            if (lone && lvl < 2) lvl = 2;
            if (t.Level >= 2 && now - t.L2At >= ShieldsUp.Window && t.Shots.Count >= _shots3) lvl = 3;
            if (!lone && lvl > 1 && Broke(t.SSide, t.Side))
            {
                lvl = 1;
                if (!t.LosingNoted) { t.LosingNoted = true; Nw[NwLosing]++; }
            }
            int prev = t.Level;
            if (lvl > t.Level)
            {
                if (t.Level < 2 && lvl >= 2) t.L2At = now;
                t.Level = lvl;
            }
            if (!_hKey) return;   // klucz NIE: tylko wykrycia (bez sterowania)
            int sent = 0, sentCharge = 0;
            HFormsTxt.Length = 0;
            if (t.Level <= 1)
            {
                int want = PerVictim + down - t.Melee;
                if (want > 0)
                {
                    // najblizszy szyk konnicy AI bez szarzy (szarza wodza zostaje), w smyczy od strzelca
                    int best = -1;
                    float bd = float.MaxValue;
                    for (int i = 0; i < fl.Count; i++)
                    {
                        var fi = fl[i];
                        if (!fi.Cav || fi.Retreat || fi.Charging) continue;
                        float e = Math.Max(0f, sp.Distance(fi.C) - fi.R);
                        if (fi.Player) { NotePlayerForm(t, fi, e); continue; }
                        if (e > _maxFrom || e >= bd) continue;
                        bd = e;
                        best = i;
                    }
                    if (best >= 0) Take(m, t, fl[best], want, now, ref sent, ref sentCharge);
                }
            }
            else
            {
                for (int i = 0; i < fl.Count; i++)
                {
                    var fi = fl[i];
                    if (!fi.Cav || fi.Retreat) continue;
                    float e = Math.Max(0f, sp.Distance(fi.C) - fi.R);
                    bool use = t.Level >= 3 ? e <= _maxFrom : (i == iCav || e <= HuntClose);
                    if (!use) continue;
                    if (fi.Player) { NotePlayerForm(t, fi, e); continue; }
                    Take(m, t, fi, int.MaxValue, now, ref sent, ref sentCharge);
                }
            }
            int archers = RangedPass(m, t, sp, now);
            if (!t.Active)
            {
                if (t.Melee > 0) StartEp(t, now, sent, sentCharge, archers, dAll, down);
            }
            else if (t.Level > prev && sent > 0)
            {
                _hEscal++;
                if (_hLines < HuntMaxLines)
                {
                    _hLines++;
                    Log.Info("Jazda na strzelca (204) poziom: " + LName[t.Level] + " po " + (now - t.Start).ToString("0.0", Inv) + " s | strzelec " + Who(s)
                             + " | pociskow w 5 s " + t.Shots.Count + ", padlo od niego " + down + " | szyki [" + HFormsTxt + "] | +jezdzcow " + sent
                             + " (z szyku w szarzy " + sentCharge + "), razem " + t.Melee + ", lucznikow na cel " + t.Ranged);
                }
            }
        }

        /// <summary>Szyk gracza w zasiegu odpowiedzi - nie rusza (ludzi szyku liczonych raz na bitwe).</summary>
        private static void NotePlayerForm(Threat t, FI fi, float e)
        {
            if (NwPlayerForms.Add(fi.F)) Nw[NwPlayer] += fi.N;
        }

        /// <summary>Wolni jezdzcy wrecz szyku do Threat (want = int.MaxValue: wszyscy; inaczej najblizsi strzelcowi).</summary>
        private static void Take(Mission m, Threat t, FI fi, int want, float now, ref int sent, ref int sentCharge)
        {
            HUnits.Clear();
            fi.F.ApplyActionOnEachUnit(HCollect);   // gra iteruje swoje listy - my tylko zbieramy
            HCand.Clear();
            Vec2 sp = t.S.Position.AsVec2;
            for (int u = 0; u < HUnits.Count; u++)
            {
                var a = HUnits[u];
                int why = Elig(m, a, t, now);
                if (why == -2) continue;
                if (why >= 0) { NoteNw(a, why); continue; }
                HCand.Add(new KeyValuePair<float, Agent>(a.Position.AsVec2.Distance(sp), a));
            }
            HUnits.Clear();
            if (want < HCand.Count) HCand.Sort((x, y) => x.Key.CompareTo(y.Key));
            int n = 0;
            for (int i = 0; i < HCand.Count && n < want; i++)
            {
                StartHunter(t, HCand[i].Value, fi, (byte)(t.MeleeEver % 2 == 0 ? RoleIntercept : RoleCut), 0f, now);
                n++;
            }
            HCand.Clear();
            if (n <= 0) return;
            sent += n;
            if (fi.Charging) sentCharge += n;
            if (HFormsTxt.Length > 0) HFormsTxt.Append(", ");
            HFormsTxt.Append(fi.F.FormationIndex).Append(' ').Append(n).Append('/').Append(fi.N).Append(fi.Charging ? " w szarzy" : "");
        }

        private static void NoteNw(Agent a, int why)
        {
            if (NwSeen.Add(((long)a.Index << 4) | (long)why)) Nw[why]++;
        }

        /// <summary>-1 = moze, -2 = pominiety bez licznika (pieszy, juz pod 204, konny lucznik z amunicja - rola strzelecka), inaczej Nw*.</summary>
        private static int Elig(Mission m, Agent a, Threat t, float now)
        {
            if (a == null || !a.IsActive() || !a.IsHuman || Hunters.ContainsKey(a)) return -2;
            if (a.IsMainAgent || !a.IsAIControlled) return NwPlayer;
            var mt = a.MountAgent;
            if (mt == null) return -2;
            if (a.IsHero) return NwHero;
            if (MarchToContact.IsDragon(mt) || Undead.Character(a.Character)) return NwOther;
            if (Banner(a)) return NwBanner;
            if (a.IsRangedCached) return -2;
            if (a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a)) return NwPanic;
            Rider r;
            if (RidingAnyThread(a) || (Riders.TryGetValue(a, out r) && r.St == RChase)) return NwChase;
            if (m.MissionEnded || a.GetComponent<VictoryComponent>() != null) return NwOther;
            float cu;
            if (HuntCool.TryGetValue(a, out cu) && now < cu) return NwCool;
            if (Blown(HorseS(a))) return NwHorse;
            if (a.Position.AsVec2.Distance(t.S.Position.AsVec2) > _maxFrom) return NwFar;
            if (!a.CanBeAssignedForScriptedMovement()) return NwBusy;   // pozycja skryptowa kogos innego (143 / 149 / 158), odlaczony, przy obiekcie
            if (_hQPass >= ShieldsUp.QueriesPerPass) return NwQuery;
            _hQPass++;
            bool locked = false;
            m.GetNearbyEnemyAgents(a.Position.AsVec2, ShieldsUp.RaiseNear, a.Team, HBuf);
            for (int i = 0; i < HBuf.Count; i++)
            {
                var x = HBuf[i];
                if (x == null || !x.IsHuman || !x.IsActive() || ReferenceEquals(x, t.S)) continue;
                locked = true;
                break;
            }
            HBuf.Clear();
            return locked ? NwMelee : -1;
        }

        /// <summary>Lucznicy strony (piesi i konni lucznicy z amunicja) w zasiegu strzelca - cel na niego, gdy nikt z ich strony nie stoi w 8 m od niego.
        /// Zwraca liczbe nowych.</summary>
        private static int RangedPass(Mission m, Threat t, Vec2 sp, float now)
        {
            // szyki lucznikow w zasiegu (zasieg szyku wedlug pierwszego czlowieka z amunicja - bez wolania gry u kazdego)
            var fl = HForms[t.Side];
            RangedForms.Clear();
            RangedRange.Clear();
            for (int i = 0; i < fl.Count; i++)
            {
                var fi = fl[i];
                if (fi.Player || fi.Retreat || !(fi.Rng || fi.Ha)) continue;   // piesi lucznicy i szyki konnych lucznikow
                var u0 = fi.F.GetFirstUnit();
                float range = 0f;
                if (u0 != null && u0.IsActive() && u0.IsRangedCached) range = ShooterRange(u0);
                if (range <= 1f || Math.Max(0f, sp.Distance(fi.C) - fi.R) > range) continue;
                RangedForms.Add(i);
                RangedRange.Add(range);
            }
            if (RangedForms.Count == 0 && t.Ranged <= 0) return 0;
            // FF: swoi w LandRadius od strzelca - lucznicy zdejmuja cel (jedno zapytanie na strzelca na pass)
            if (_hQPass >= ShieldsUp.QueriesPerPass) return 0;
            _hQPass++;
            bool friends = false;
            m.GetNearbyEnemyAgents(sp, ShieldsUp.LandRadius, t.S.Team, HBuf);
            for (int i = 0; i < HBuf.Count; i++)
            {
                var x = HBuf[i];
                if (x != null && x.IsHuman && x.IsActive()) { friends = true; break; }
            }
            HBuf.Clear();
            if (friends)
            {
                if (t.Ranged > 0)
                {
                    _hFriendBlock++;
                    for (int i = t.Hs.Count - 1; i >= 0; i--)
                    {
                        if (i >= t.Hs.Count) continue;
                        var h = t.Hs[i];
                        if (h.Role == RoleRanged) EndHunter(h, UFriend, now, false, false);
                    }
                }
                return 0;
            }
            int n = 0;
            for (int j = 0; j < RangedForms.Count; j++)
            {
                if (_hNewRanged >= TrackPerStep) { _hRangedCap++; break; }
                var fi = fl[RangedForms[j]];
                float range = RangedRange[j];
                HUnits.Clear();
                fi.F.ApplyActionOnEachUnit(HCollect);
                for (int u = 0; u < HUnits.Count && _hNewRanged < TrackPerStep; u++)
                {
                    var a = HUnits[u];
                    if (a == null || !a.IsActive() || !a.IsHuman || Hunters.ContainsKey(a) || !a.IsAIControlled || a.IsMainAgent || a.IsHero) continue;
                    if (!a.IsRangedCached || Banner(a) || Undead.Character(a.Character)) continue;
                    var mt = a.MountAgent;
                    if (mt != null && MarchToContact.IsDragon(mt)) continue;
                    if (a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a) || RidingAnyThread(a)) continue;
                    if (!a.CanBeAssignedForScriptedMovement()) continue;
                    if (a.Position.AsVec2.Distance(sp) > range) continue;
                    StartHunter(t, a, fi, RoleRanged, range, now);
                    _hNewRanged++;
                    n++;
                }
                HUnits.Clear();
            }
            return n;
        }

        private static void StartHunter(Threat t, Agent a, FI fi, byte role, float range, float now)
        {
            var h = new Hunter { A = a, F = fi.F, T = t, Role = role, Start = now, Range = range };
            Hunters[a] = h;
            HList.Add(h);
            t.Hs.Add(h);
            a.SetAutomaticTargetSelection(false);
            a.SetTargetAgent(t.S);
            if (fi.Player) _hFromPlayer++;   // nie powinno sie zdarzyc (kontrola - oczekiwane 0)
            if (role == RoleRanged)
            {
                MarkIn(ref _aiming, a, true);
                t.Ranged++;
                if (a.MountAgent != null) HHorseArch.Add(a.Index); else HFootArch.Add(a.Index);
                return;
            }
            MarkCounter(a, true);
            t.Melee++;
            t.MeleeEver++;
            HRiders.Add(a.Index);
            HFormsSent.Add(fi.F);
            if (fi.Charging) _hSentCharge++;
            if (ReferenceEquals(t.S, _tA)) _tRiders++;
            Steer(h, now);
        }

        private static void StartEp(Threat t, float now, int sent, int sentCharge, int archers, float dist, int down)
        {
            t.Active = true;
            t.Start = now;
            t.FirstContact = -1f;
            t.Blows = 0;
            t.RidersLost = 0;
            t.MaxFar = 0f;
            t.LastEnd = -1;
            t.Ep = ++_hEp;
            if (ReferenceEquals(t.S, _tA)) _tEp++;
            if (_hLines >= HuntMaxLines) return;
            _hLines++;
            var s = t.S;
            Log.Info("Jazda na strzelca (204) start: strzelec " + Who(s) + " (" + (s.IsMainAgent ? "gracz" : "AI") + ", konno, " + Weapon(s) + ") | strona "
                     + (t.Side == 1 ? "atak" : "obrona") + " | wykrycie " + (t.First >= 0 ? DName[t.First] : "?") + ", pociskow w 5 s " + t.Shots.Count
                     + ", padlo od niego " + down + " | poziom " + LName[t.Level] + (t.Screen ? " (zaslona - strzelec przy swojej piechocie)" : "")
                     + " | szyki [" + HFormsTxt + "] | jezdzcow " + sent + " (z szyku w szarzy " + sentCharge + "), lucznikow na cel " + archers
                     + " | odleglosc od szyku " + F0(dist) + " m");
        }

        /// <summary>Koniec epizodu: wynik, linia, wszyscy ludzie strzelca oddani grze (przerwa jezdzcow).</summary>
        private static void EndEp(Threat t, int outcome, float now)
        {
            if (!t.Active) return;
            t.Active = false;
            HOut[outcome]++;
            HEpL[Math.Max(1, Math.Min(3, t.Level))]++;
            float dur = Math.Max(0f, now - t.Start);
            for (int i = t.Hs.Count - 1; i >= 0; i--)
            {
                if (i >= t.Hs.Count) continue;
                var h = t.Hs[i];
                try { EndHunter(h, UThreat, now, outcome != HEnded, false); }
                catch (Exception e) { HStumble("HorsePursuit.JazdaKoniecEpizodu", e); DropHunter(h); }
            }
            t.Level = 0;
            t.L2At = 0f;
            if (_hLines >= HuntMaxLines) return;
            _hLines++;
            Log.Info("Jazda na strzelca (204) koniec: " + HName[outcome] + " po " + dur.ToString("0.0", Inv) + " s | strzelec " + Who(t.S) + " | jezdzcy padli "
                     + t.RidersLost + ", ciosy w strzelca " + t.Blows + ", kontakt " + (t.FirstContact >= 0f ? (t.FirstContact - t.Start).ToString("0.0", Inv) + " s" : "-")
                     + ", najdalej od szyku " + F0(t.MaxFar) + " m");
        }

        private static void EndThreat(Threat t, int outcome, float now)
        {
            if (t.Active) EndEp(t, outcome, now);
            for (int i = t.Hs.Count - 1; i >= 0; i--)
            {
                if (i >= t.Hs.Count) continue;
                var h = t.Hs[i];
                try { EndHunter(h, UThreat, now, false, false); } catch (Exception e) { HStumble("HorsePursuit.JazdaKoniecStrzelca", e); DropHunter(h); }
            }
            RemoveThreat(t);
        }

        private static void RemoveThreat(Threat t)
        {
            Threat cur;
            if (t.S != null && Threats.TryGetValue(t.S, out cur) && ReferenceEquals(cur, t)) Threats.Remove(t.S);
            ThreatList.Remove(t);
            MarkIn(ref _hunted, t.S, false);
        }

        /// <summary>Czlowiek oddany grze: cel automatyczny, pozycja skryptowa zdjeta (jak 148 Uncontrol; force - przed pierwszym ruchem 143).</summary>
        private static void EndHunter(Hunter h, int why, float now, bool cool, bool force)
        {
            var t = h.T;
            var a = h.A;
            if (a != null)
            {
                Hunter cur;
                if (Hunters.TryGetValue(a, out cur) && ReferenceEquals(cur, h)) Hunters.Remove(a);
            }
            HList.Remove(h);
            t.Hs.Remove(h);
            UEnd[why]++;
            bool melee = h.Role != RoleRanged;
            if (melee) { MarkCounter(a, false); t.Melee--; t.LastEnd = why; }
            else { MarkIn(ref _aiming, a, false); t.Ranged--; }
            if (a != null && a.IsActive())
            {
                try { a.SetAutomaticTargetSelection(true); a.InvalidateTargetAgent(); } catch { }
                if (melee && (force || a.IsRunningAway || !Withdrawing(a)))
                {
                    try { if ((a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) a.DisableScriptedMovement(); } catch { }
                }
            }
            if (cool && melee && a != null) HuntCool[a] = now + CounterCool;
            if (melee && t.Active && t.Melee <= 0) EndEp(t, Outcome(why), now);
        }

        /// <summary>Po wyjatku przy zwalnianiu - tylko wypisanie ze stanu (bez wolania gry).</summary>
        private static void DropHunter(Hunter h)
        {
            if (h.A != null) { Hunters.Remove(h.A); MarkCounter(h.A, false); MarkIn(ref _aiming, h.A, false); }
            HList.Remove(h);
            if (h.T != null)
            {
                if (h.T.Hs.Remove(h)) { if (h.Role != RoleRanged) h.T.Melee--; else h.T.Ranged--; }
            }
        }

        private static int Outcome(int why)
        {
            switch (why)
            {
                case UHorse: return HHorses;
                case ULeash: return HLeash;
                case URetreat: return HRetreat;
                case UEnded: return HEnded;
                default: return HOther;
            }
        }

        // ------------------------------------------------------------ sterowanie (co 0.25 s i po kazdym passie)

        private static void HuntTrack(Mission m, float now)
        {
            DrainBlows(m, now);
            int n = HList.Count;
            if (n > 0)
            {
                int budget = TrackPerStep;
                int start = _hRot > 0 && _hRot < n ? _hRot : 0;
                int next = 0;
                for (int k = 0; k < n; k++)
                {
                    int i = start + k;
                    if (i >= n) i -= n;
                    if (i >= HList.Count) break;
                    var h = HList[i];
                    if (h.Role == RoleRanged) continue;
                    if (budget <= 0) { next = i; break; }
                    budget--;
                    try
                    {
                        int why = HunterCheck(m, h, now, false);
                        if (why >= 0) { EndHunter(h, why, now, why != UGone && why != UEnded, false); n = HList.Count; continue; }
                        Steer(h, now);
                    }
                    catch (Exception e)
                    {
                        HStumble("HorsePursuit.JazdaCel", e);
                        if (++h.Fails >= FailsOut)
                        {
                            _hDropped++;
                            try { EndHunter(h, UOther, now, true, false); } catch { DropHunter(h); }
                            n = HList.Count;
                        }
                    }
                }
                _hRot = next;
            }
            try { HuntTestStep(m, now, false); } catch (Exception e) { HStumble("HorsePursuit.JazdaTest", e); }
        }

        /// <summary>Punkt jezdzca: w 30 m prosto na strzelca; dalej co drugi w punkt przeciecia, co drugi 30 m za strzelcem (odciecie ucieczki od naszego szyku);
        /// strzelec przy swojej piechocie - zaslona 30 m przed naszym szykiem pieszym.</summary>
        private static void Steer(Hunter h, float now)
        {
            var t = h.T;
            var s = t.S;
            var a = h.A;
            if (s == null || !s.IsActive()) return;
            Vec2 sp = s.Position.AsVec2;
            float d = a.Position.AsVec2.Distance(sp);
            if (d <= ShieldsUp.RaiseNear && t.Active && t.FirstContact < 0f) Contact(t, a, d, now);
            Vec2 p = sp;
            if (t.Screen && t.ScreenC.IsValid)
            {
                Vec2 c = t.ScreenC;
                float dc = c.Distance(sp);
                if (dc > 0.1f) p = c + (sp - c) * (Math.Min(t.ScreenR + HuntScreen, dc) / dc);
            }
            else if (d > HuntScreen)
            {
                if (h.Role == RoleIntercept)
                {
                    var sm = s.MountAgent ?? s;
                    Vec2 vel = sm.Velocity.AsVec2;
                    float vs = vel.Length;
                    if (vs > 0.5f)
                    {
                        float vr = 0f;
                        var am = a.MountAgent;
                        try { vr = am != null ? am.GetMaximumForwardUnlimitedSpeed() : 0f; } catch { vr = 0f; }
                        if (vr < vs) vr = vs;
                        float lead = Math.Min(vs * d / vr, d);
                        p = sp + vel * (lead / vs);
                    }
                }
                else if (t.EscC.IsValid)
                {
                    Vec2 e = sp - t.EscC;
                    float le = e.Length;
                    if (le > 0.1f) p = sp + e * (Math.Min(HuntScreen, d) / le);
                }
            }
            var wp = s.GetWorldPosition();
            if (!wp.IsValid) return;
            if (p.x != sp.x || p.y != sp.y)
            {
                var wq = wp;
                wq.SetVec2(p);
                if (wq.GetNavMesh() != UIntPtr.Zero) wp = wq;   // punkt poza siatka - prosto na strzelca
            }
            a.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);
            _hSets++;
        }

        private static void Contact(Threat t, Agent a, float d, float now)
        {
            t.FirstContact = now;
            float dt = Math.Max(0f, now - t.Start);
            _hContactN++;
            _hContactSum += dt;
            if (dt < _hContactMin) _hContactMin = dt;
            if (dt > _hContactMax) _hContactMax = dt;
            if (ReferenceEquals(t.S, _tA) && _tContact < 0f) _tContact = dt;
            if (_hLines >= HuntMaxLines) return;
            _hLines++;
            Log.Info("Jazda na strzelca (204) kontakt: po " + dt.ToString("0.0", Inv) + " s, jezdziec " + Who(a) + ", " + d.ToString("0.0", Inv) + " m | strzelec " + Who(t.S));
        }

        /// <summary>Ciosy jezdzcow 204 w strzelca (z OnHit, dowolny watek) - przypisanie do strzelca na glownym watku; cios = takze kontakt.</summary>
        private static void DrainBlows(Mission m, float now)
        {
            int n;
            lock (HLock) { n = BlowQ.Count; }
            if (n == 0) return;
            int[] idx;
            lock (HLock) { idx = BlowQ.ToArray(); BlowQ.Clear(); }
            for (int i = 0; i < idx.Length; i++)
            {
                for (int j = 0; j < ThreatList.Count; j++)
                {
                    var t = ThreatList[j];
                    if (t.S == null || t.S.Index != idx[i] || !t.Active) continue;
                    t.Blows++;
                    if (t.FirstContact < 0f && t.S.IsActive())
                    {
                        Agent best = null;
                        float bd = float.MaxValue;
                        for (int k = 0; k < t.Hs.Count; k++)
                        {
                            var h = t.Hs[k];
                            if (h.Role == RoleRanged || h.A == null) continue;
                            float d = h.A.Position.AsVec2.Distance(t.S.Position.AsVec2);
                            if (d < bd) { bd = d; best = h.A; }
                        }
                        if (best != null) Contact(t, best, bd, now);
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------ zdarzenia (OnHit - dowolny watek; OnRemoved, BeforeWithdraw - glowny)

        /// <summary>OnHit (bywa z ticku asynchronicznego): cios wrecz jezdzca 204 w strzelca albo jego konia; trafienie jezdzca / lucznika 204 przez wlasna
        /// strone (FF). Tylko tablice wedlug Agent.Index, Interlocked i kolejka pod lockiem.</summary>
        private static void HuntOnHit(Agent victim, Agent affector, in Blow blow)
        {
            Agent vic = victim.IsMount ? victim.RiderAgent : victim;
            Agent att = affector.IsMount ? affector.RiderAgent : affector;
            if (vic == null || att == null || ReferenceEquals(vic, att)) return;
            if (!blow.IsMissile && CounterAnyThread(att) && In(Volatile.Read(ref _hunted), vic))
            {
                Interlocked.Increment(ref _hBlows);
                lock (HLock) { if (BlowQ.Count < HuntBlowCap) BlowQ.Add(vic.Index); }
                return;
            }
            if (!HuntAnyThread(vic)) return;
            var at = att.Team;
            var vt = vic.Team;
            if (!MissionTeams.Valid(at) || !MissionTeams.Valid(vt) || at.Side != vt.Side) return;
            if (blow.IsMissile) Interlocked.Increment(ref _hFFMissile); else Interlocked.Increment(ref _hFFMelee);
        }

        /// <summary>OnRemoved (glowny watek): padlo od konnego (do poziomu), czlowiek 204 padl, strzelec padl (od jezdzcow 204 / od innych), nekajacy testu.</summary>
        private static void HuntRemoved(Mission m, Agent a, Agent by, bool down)
        {
            float now = m != null ? m.CurrentTime : 0f;
            if (down && by != null)
            {
                Agent k = by.IsMount ? by.RiderAgent : by;
                if (k != null && k.IsHuman && k.MountAgent != null && a.Team != null && k.Team != null && a.Team.IsEnemyOf(k.Team))
                {
                    int c;
                    DownBy.TryGetValue(k, out c);
                    DownBy[k] = c + 1;
                }
            }
            Hunter h;
            if (Hunters.TryGetValue(a, out h))
            {
                if (down && h.Role != RoleRanged) { h.T.RidersLost++; _hRidersLost++; }
                EndHunter(h, UGone, now, false, false);
            }
            Threat t;
            if (Threats.TryGetValue(a, out t))
            {
                bool byRider = false;
                if (by != null)
                {
                    Agent k = by.IsMount ? by.RiderAgent : by;
                    Hunter bh;
                    if (k != null && Hunters.TryGetValue(k, out bh) && ReferenceEquals(bh.T, t) && bh.Role != RoleRanged) byRider = true;
                }
                EndThreat(t, down ? (byRider ? HDownRiders : HDownOthers) : HOther, now);
            }
            if (ReferenceEquals(a, _tA)) TestEnd(m, down ? "padl" : "zszedl z pola", now);
        }

        /// <summary>BeforeWithdraw (143, przed pierwszym ruchem odwrotu): czlowiek 204 oddany grze, pozycja zdjeta; nekajacy testu zwolniony.</summary>
        private static void HuntBeforeWithdraw(Agent a)
        {
            float now = 0f;
            try { now = Mission.Current != null ? Mission.Current.CurrentTime : 0f; } catch { }
            Hunter h;
            if (Hunters.TryGetValue(a, out h)) EndHunter(h, UPanic, now, true, true);
            if (ReferenceEquals(a, _tA)) TestEnd(Mission.Current, "odwrot rannego", now);
        }

        // ------------------------------------------------------------ pomocnicze

        private static string Who(Agent a)
        {
            if (a == null) return "?";
            if (a.IsMainAgent) return "gracz";
            string id = "?";
            try { id = a.Character != null ? (a.Character.StringId ?? "?") : "?"; } catch { }
            return id + "#" + a.Index;
        }

        private static string Weapon(Agent a)
        {
            try
            {
                var w = a.WieldedWeapon;
                var d = w.IsEmpty ? null : w.CurrentUsageItem;
                return d != null ? d.WeaponClass.ToString() : "?";
            }
            catch { return "?"; }
        }

        // ------------------------------------------------------------ linia bitwy (raz na bitwe polowa)

        private static void HuntWrite()
        {
            if (!_field) return;
            int wyk = HDet[DHit] + HDet[DMiss] + HDet[DPass] + HDet[DAi];
            int eps = HEpL[1] + HEpL[2] + HEpL[3];
            var sb = new StringBuilder(2000);
            sb.Append("Jazda na strzelca (204): misja ").Append(_kind).Append(", klucz ").Append(_hKey ? "TAK" : "NIE (tylko wykrycia, bez sterowania)")
              .Append(" (konny strzelec wroga zauwazony po trafieniu, pudle w ").Append(ShieldsUp.MissRadius.ToString("0.0", Inv)).Append(" m od ludzi, upadku / przelocie w ")
              .Append(F0(ShieldsUp.LandRadius)).Append(" m, celu AI; L1 oddzial ").Append(PerVictim).Append(" + padlo od niego z szyku bez szarzy, L2 szyk (")
              .Append(F0(HuntClose)).Append(" m od konnicy, ").Append(_shots3).Append(" pociskow w ").Append(F0(ShieldsUp.Window)).Append(" s albo ktos padl), L3 cala jazda w ")
              .Append(F0(_maxFrom)).Append(" m (noc ").Append(_night ? "TAK" : "NIE").Append("); cisza ").Append(F0(_hold)).Append(" s; zaslona ").Append(F0(HuntScreen))
              .Append(" m przy piechocie strzelca w ").Append(F0(CounterFootGap)).Append(" m; kon ponizej ").Append(_restBelow).Append("; przerwa ").Append(F0(CounterCool))
              .Append(" s; szyki gracza, bohaterowie, chorazowie, smoki, umarli nigdy)")
              .Append(" | wykrycia ").Append(wyk).Append(" (trafienia ").Append(HDet[DHit]).Append(", pudla przy ludziach ").Append(HDet[DMiss]).Append(", przelot obok szyku ")
              .Append(HDet[DPass]).Append(", cel AI ").Append(HDet[DAi]).Append("; strzelcow ").Append(HShooters.Count).Append(", w tym gracz ").Append(_hPlayerShooter)
              .Append("; strzalow konnych ").Append(_hShotsSeen).Append("; ponad limit klatki ").Append(_hFrameOver).Append(')')
              .Append(" | epizody ").Append(eps).Append(" (L1 ").Append(HEpL[1]).Append(" / L2 ").Append(HEpL[2]).Append(" / L3 ").Append(HEpL[3])
              .Append("; podniesien poziomu ").Append(_hEscal).Append(", sekund zaslony ").Append(_hScreenSec).Append(')')
              .Append(" | jezdzcy wyslani ").Append(HRiders.Count).Append(" (szykow ").Append(HFormsSent.Count).Append(", z szyku w szarzy ").Append(_hSentCharge)
              .Append(", konni lucznicy na cel ").Append(HHorseArch.Count).Append(", lucznicy pieszo na cel ").Append(HFootArch.Count).Append(", strzaly w strzelca ")
              .Append(_hRangedShots).Append(", cel zdjety - swoi przy strzelcu ").Append(_hFriendBlock).Append(", limit lucznikow ").Append(_hRangedCap).Append(')')
              .Append(" | kontakt ").Append(_hContactN).Append(" (sr. ").Append(_hContactN > 0 ? (_hContactSum / _hContactN).ToString("0.0", Inv) : "-")
              .Append(" / min ").Append(_hContactN > 0 ? _hContactMin.ToString("0.0", Inv) : "-").Append(" / max ").Append(_hContactN > 0 ? _hContactMax.ToString("0.0", Inv) : "-")
              .Append(" s; ciosy w strzelca ").Append(Volatile.Read(ref _hBlows)).Append(')')
              .Append(" | wyniki ");
            for (int i = 0; i < HKinds; i++) sb.Append(i > 0 ? ", " : "").Append(HName[i]).Append(' ').Append(HOut[i]);
            sb.Append(" | nie wyslani: w zwarciu ").Append(Nw[NwMelee]).Append(", panika/odwrot ").Append(Nw[NwPanic]).Append(", poscig 148 ").Append(Nw[NwChase])
              .Append(", bohater ").Append(Nw[NwHero]).Append(", chorazy ").Append(Nw[NwBanner]).Append(", kon ").Append(Nw[NwHorse]).Append(", daleko ").Append(Nw[NwFar])
              .Append(", szyk gracza ").Append(Nw[NwPlayer]).Append(", strona przegrywa ").Append(Nw[NwLosing]).Append(", zajety ").Append(Nw[NwBusy]).Append(", przerwa ")
              .Append(Nw[NwCool]).Append(", limit zapytan ").Append(Nw[NwQuery]).Append(", smok/umarly ").Append(Nw[NwOther])
              .Append(" | powroty: padl ").Append(UEnd[UGone]).Append(", panika ").Append(UEnd[UPanic]).Append(", kon ").Append(UEnd[UHorse]).Append(", smycz ")
              .Append(UEnd[ULeash]).Append(", odwrot wodza ").Append(UEnd[URetreat]).Append(", gracz przejal ").Append(UEnd[UPlayer]).Append(", klucz ").Append(UEnd[UKey])
              .Append(", koniec bitwy ").Append(UEnd[UEnded]).Append(", koniec strzelca ").Append(UEnd[UThreat]).Append(", poza zasiegiem ").Append(UEnd[URange])
              .Append(", swoi przy strzelcu ").Append(UEnd[UFriend]).Append(", inne ").Append(UEnd[UOther]).Append("; najdalej od szyku ").Append(F0(_hMaxFar)).Append(" m")
              .Append("; jezdzcy padli ").Append(_hRidersLost)
              .Append(" | z szyku gracza ").Append(_hFromPlayer).Append(" (oczekiwane 0)")
              .Append(" | pod sterowaniem 204 na koniec ").Append(_hLeft).Append(" (oczekiwane 0)")
              .Append(" | FF w jezdzcow ").Append(Volatile.Read(ref _hFFMissile) + Volatile.Read(ref _hFFMelee)).Append(" (pociski ").Append(Volatile.Read(ref _hFFMissile))
              .Append(", wrecz ").Append(Volatile.Read(ref _hFFMelee)).Append(')')
              .Append(" | cap marszu pominiety ").Append(Volatile.Read(ref _cCapSkip)).Append(", odswiezen celu ").Append(_hSets)
              .Append(" | test: ").Append(TestSummary())
              .Append(" | potkniecia jazda ").Append(_hStum).Append(" (wypadli ").Append(_hDropped).Append(").");
            Log.Info(sb.ToString());
        }
    }
}
