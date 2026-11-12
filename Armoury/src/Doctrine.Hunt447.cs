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
    /// 447 = decyzja Jeffa nr 11 z audytu AI bitwy (docs/audyty-2026-11-11/AUDYT-AI-BITWA.md rozdz. 5: "Jazda AI poluje na ciebie jako wodza, gdy stoisz z tylu
    /// i nie strzelasz" - Jeff 11.11: "tak"; rozdz. 3 pakiet 4: "klin na biernego wodza" - po 4.1, przez sterowanie pojedynczymi jezdzcami (wzor 204), we wspolnej
    /// puli z 204/148 z jawnym pierwszenstwem). Klucz CavalryHuntsRearCommander ("Cavalry Hunts Rear Commander", domyslnie TAK). Zadnych bonusow do liczb - tylko
    /// decyzja. Bitwa polowa, nie morska. Glowny watek (Doctrine.Tick: pass co 1 s po 444, przed 442; krok jezdzcow co 0.25 s).
    ///
    /// GRACZ-WODZ Z TYLU (wszystko naraz, bez przerwy przez 10 s): Agent.Main zywy, wodzem swojej druzyny (Team.IsPlayerGeneral); zaden czlowiek wroga w 80 m;
    ///   bez ciosu wrecz zadanego / otrzymanego w 10 s; bez strzalu w 20 s (BattleSense.OnAgentShootMissile - licznik Interlocked); stoi (< 3 m/s miedzy passami);
    ///   bez strazy (mniej niz 5 ludzi swojej strony w 20 m); z tylu - jest szyk swojej strony (>= 10 ludzi), ktorego srodek jest o >= 30 m blizej najblizszego
    ///   szyku wroga niz gracz.
    /// SZYK (najblizszy graczowi): druzyna AI wroga gracza (TeamAIGeneral), jazda (nie konni strzelcy), >= 5 ludzi, IsAIControlled, nie szyk gracza / z czlowiekiem
    ///   gracza, rola 441 NONE (pilniejsza rola 443 / 444 / 445 ma pierwszenstwo - HUNT bierze tylko szyk wolny), poza zwarciem (< 30% wrecz, miara 442), srodek
    ///   w 300 m od gracza, droga wolna (zaden szyk piechoty strony gracza >= 10 ludzi nie zaslania odcinka szyk - gracz, jak T442Blocked), >= 5 wolnych jezdzcow.
    /// WSPOLNA PULA Z 204 / 148 (jawne pierwszenstwo): szyk, w ktorym >= 50% jezdzcow jest juz pod 148 (poscig) / 204 (jazda na strzelca) / testem 283a, jest zajety
    ///   pilniejszym - HUNT go nie bierze. Po nadaniu roli HUNT szyk jest dla 148 / 204 / 283 / 311a / 344 szykiem w roli (C441Owned - nie biora z niego jezdzcow,
    ///   ich jezdzcy tego szyku wracaja: URole / ERole - 441-p, wlaczone w 447-b) - HUNT ma pierwszenstwo w swoim szyku, a zwolnieni dolaczaja w nastepnym passie. Jezdziec HUNT jest
    ///   dla 149 (straz bohatera 335) i chorazych 158 tym, czym jezdziec 204 (H447AnyThread obok HuntAnyThread / RidingAnyThread). 443 nie zaczyna rajdu / schowania
    ///   szykiem w HUNT (H447Busy).
    /// POLOWANIE (raz na bitwe, wszystkie druzyny razem): rola 441 CrHunt (galop - wyjatek klusa 441) z rozkazem Move na miejsce gracza (odswiezany, gdy gracz
    ///   odszedl > 10 m); kazdy wolny jezdziec szyku (AI, konno, nie bohater, nie chorazy, nie smok, nie umarly, nie w panice / odwrocie / wycofaniu 143, nie pod
    ///   148 / 204 / 283a, CanBeAssignedForScriptedMovement) - wzor 148: SetAutomaticTargetSelection(false) + SetTargetAgent(gracz) + SetScriptedPosition(miejsce
    ///   gracza) co 0.25 s, bez NoAttack. Komunikat dla gracza (po angielsku): "Enemy horsemen ride for you!".
    ///   Koniec: 40 s; szyk stracil 30% ludzi od startu; gracz padl / zniknal; wszyscy jezdzcy zawroceni; rola oddana przez 441 (gracz / szyk pusty); klucz NIE
    ///   w trakcie (447-p); potkniecie passu (447-p). Jezdziec
    ///   zwalniany pojedynczo: padl, bez konia, panika / ucieczka / odwrot szyku, wycofanie 143 (BeforeWithdraw - pozycja 143 nietknieta), zmiana szyku.
    ///   Zwolnienie: cel automatyczny, pozycja skryptowa zdjeta (chyba ze wycofanie 143 - ta jest jego), rola NONE (RBM wraca).
    /// Klucz NIE (albo bez dowodcy jazdy 441): warunki i wybor szyku liczone (tylko odczyt), rola nie nadawana - licznik "bylby" (raz na bitwe); co do znaku jak dotad.
    /// Watki: pass, krok, wybor - glowny watek. OnShoot / OnHit (tick mozliwie asynchroniczny) - tylko Interlocked i odczyt tablicy jezdzcow (Volatile).
    ///   OnRemoved - glowny watek. Wyjatki per jezdziec / pass liczone (potkniecia), polowanie konczone, bez globalnego wylacznika.
    /// Linia "Polowanie na wodza (447)" na koncu bitwy polowej i "Polowanie na wodza (447) start" raz na bitwe.
    /// </summary>
    internal static partial class Doctrine
    {
        internal const float H447Every = 1f, H447Step = 0.25f, H447Fight = 80f, H447MeleeWin = 10f, H447ShotWin = 20f, H447Still = 3f, H447GuardR = 20f;
        internal const float H447Behind = 30f, H447Hold = 10f, H447Range = 300f, H447Max = 40f, H447Loss = 0.3f, H447Reach = 5f, H447Refresh = 10f, H447BusyMax = 0.5f;
        internal const int H447GuardMen = 5, H447FriendMen = 10, H447MinRiders = 5;
        private const int HrTime = 0, HrLoss = 1, HrDown = 2, HrNoRiders = 3, HrBack = 4, HrGone = 5, HrKey = 6, HrErr = 7, HrK = 8;
        private static readonly string[] HrName = { "czas 40 s", "strata 30%", "gracz padl", "jezdzcy zawroceni", "rola oddana (gracz / szyk pusty / 441)", "gracz zniknal",
            "klucz NIE / bez dowodcy jazdy", "potkniecie" };
        private const int HuDown = 0, HuHorse = 1, HuPanic = 2, HuWithdraw = 3, HuForm = 4, HuK = 5;
        private static readonly string[] HuName = { "padl", "bez konia", "panika / odwrot", "wycofanie 143", "zmiana szyku" };
        private const int HnFight = 0, HnMelee = 1, HnShot = 2, HnMove = 3, HnGuard = 4, HnFront = 5, HnK = 6;
        private static readonly string[] HnName = { "wrog w 80 m", "walczy wrecz", "strzela", "w ruchu", "straz", "nie z tylu" };
        private const int HcNone = 0, HcRole = 1, HcMelee = 2, HcFar = 3, HcBlocked = 4, HcBusy = 5, HcFew = 6, HcK = 7;
        private static readonly string[] HcName = { "brak jazdy AI wroga", "w innej roli", "w zwarciu", "dalej niz 300 m", "droga zasloniona", "zajeta 148/204", "za malo wolnych jezdzcow" };
        private static readonly IFormatProvider H7Inv = CultureInfo.InvariantCulture;

        // ---- stan misji (glowny watek; _h7Arr czytany z innych watkow)
        private static bool _h7Live, _h7Done, _h7On;
        private static float _h7Acc, _h7StepAcc, _h7RearSince = -1f, _h7LastShot = -1000f, _h7Start, _h7FirstReach = -1f;
        private static int _h7ShotSeen, _h7Men0, _h7Cnt0;
        private static Vec2 _h7PrevPos = Vec2.Invalid, _h7OrdAt = Vec2.Invalid;
        private static Formation _h7F;
        private static Agent _h7Target;
        private static readonly List<Agent> _h7Riders = new List<Agent>();
        private static readonly HashSet<int> _h7Reached = new HashSet<int>();
        private static readonly HashSet<int> _h7TakenIdx = new HashSet<int>();   // 447-p: jezdzcy wzieci (unikalnie po Index) - ponowne wziecia osobno
        private static bool _h7BusySeen;   // 447-p: wstrzymanie 443 liczone raz na polowanie (nie co takt)
        private static Agent[] _h7Arr;
        private static readonly MBList<Agent> _h7Buf = new MBList<Agent>();
        private static readonly List<Agent> _h7Units = new List<Agent>();
        private static readonly Action<Agent> H447Collect = a => _h7Units.Add(a);

        // ---- liczniki misji (H447Reset w Battle291.Begin); _h7ShotN, _h7HitN, _h7Dmg - Interlocked
        private static int _h7ShotN, _h7HitN, _h7Dmg;
        private static int _h7Starts, _h7Dry, _h7Taken, _h7Late, _h7ReachN, _h7Down, _h7DownBy, _h7Open, _h7Busy443, _h7Stum, _h7RidersLost, _h7Refused, _h7Retake;
        private static float _h7RearSec, _h7GenSec, _h7HuntSec;
        private static readonly int[] _h7End = new int[HrK], _h7Rel = new int[HuK];
        private static readonly float[] _h7NoSec = new float[HnK];
        private static readonly int[] _h7NoCav = new int[HcK];

        /// <summary>Battle291.Begin (AfterStart): liczniki linii 447 od zera.</summary>
        internal static void H447Reset()
        {
            Interlocked.Exchange(ref _h7ShotN, 0); Interlocked.Exchange(ref _h7HitN, 0); Interlocked.Exchange(ref _h7Dmg, 0);
            _h7Starts = _h7Dry = _h7Taken = _h7Late = _h7ReachN = _h7Down = _h7DownBy = _h7Open = _h7Busy443 = _h7Stum = _h7RidersLost = _h7Refused = _h7Retake = 0;
            _h7BusySeen = false; _h7TakenIdx.Clear();
            _h7RearSec = _h7GenSec = _h7HuntSec = 0f;
            _h7Cnt0 = 0; _h7FirstReach = -1f;
            for (int k = 0; k < HrK; k++) _h7End[k] = 0;
            for (int k = 0; k < HuK; k++) _h7Rel[k] = 0;
            for (int k = 0; k < HnK; k++) _h7NoSec[k] = 0f;
            for (int k = 0; k < HcK; k++) _h7NoCav[k] = 0;
        }

        /// <summary>Doctrine.Begin (AfterStart, po Clear): bitwa polowa, nie morska - pomiar zawsze, decyzja wedlug klucza (czytany co pass).</summary>
        private static void H447Begin(Mission m)
        {
            bool field = false;
            try { field = m != null && m.IsFieldBattle && !m.IsNavalBattle; } catch { field = false; }
            _h7Acc = _h7StepAcc = 0f;
            _h7Done = false;
            _h7Live = field;
        }

        /// <summary>Doctrine.Clear (Begin / End): polowanie w toku zakonczone (jezdzcy oddani grze), stan od zera.</summary>
        private static void H447ClearMission()
        {
            _h7Live = false;
            if (_h7On) { _h7Open++; H447Stop(-1); }
            _h7On = false; _h7Done = false;
            _h7F = null; _h7Target = null;
            _h7Riders.Clear(); _h7Reached.Clear(); _h7TakenIdx.Clear(); _h7Units.Clear(); _h7Buf.Clear();
            _h7BusySeen = false;
            Volatile.Write(ref _h7Arr, null);
            _h7Acc = _h7StepAcc = 0f;
            _h7RearSince = -1f; _h7LastShot = -1000f;
            _h7ShotSeen = Volatile.Read(ref _h7ShotN);
            _h7PrevPos = _h7OrdAt = Vec2.Invalid;
        }

        // ------------------------------------------------------------ odczyty dla innych modulow

        /// <summary>149 (straz bohatera) / 158 (chorazy): czlowiek pod sterowaniem polowania 447 - jego pozycja i cel. Dowolny watek, bez blokady.</summary>
        internal static bool H447AnyThread(Agent a)
        {
            if (a == null) return false;
            var k = Volatile.Read(ref _h7Arr);
            if (k == null) return false;
            int i = a.Index;
            return i >= 0 && i < k.Length && ReferenceEquals(k[i], a);
        }

        /// <summary>443 (glowny watek): szyk w roli HUNT - bez rajdu / schowania (rola 447 nie nadpisywana).</summary>
        internal static bool H447Busy(Formation f)
        {
            if (C441RoleOf(f) != CrHunt) return false;
            if (!_h7BusySeen) { _h7BusySeen = true; _h7Busy443++; }   // 447-p: raz na polowanie (443 pyta co takt pod ostrzalem)
            return true;
        }

        private static void H447Mark(Agent a, bool on)
        {
            int i = a.Index;
            if (i < 0 || i >= 65536) return;
            var k = _h7Arr;
            if (k == null || i >= k.Length)
            {
                if (!on) return;
                int n = k == null ? 1024 : k.Length;
                while (n <= i) n *= 2;
                var nk = new Agent[n];
                if (k != null) Array.Copy(k, nk, k.Length);
                nk[i] = a;
                Volatile.Write(ref _h7Arr, nk);
                return;
            }
            if (on) k[i] = a;
            else if (ReferenceEquals(k[i], a)) k[i] = null;
        }

        // ------------------------------------------------------------ zdarzenia (BattleSense)

        /// <summary>BattleSense.OnAgentShootMissile (dowolny watek): strzal gracza - licznik (pass porownuje).</summary>
        internal static void H447OnShoot(Agent shooter)
        {
            if (!_h7Live || shooter == null) return;
            try { if (shooter.IsMainAgent) Interlocked.Increment(ref _h7ShotN); } catch { }
        }

        /// <summary>BattleSense.OnAgentHit (dowolny watek): cios jezdzca polowania w gracza albo jego konia - licznik i obrazenia.</summary>
        internal static void H447OnHit(Agent victim, Agent by, in Blow blow)
        {
            if (!_h7Live || victim == null || by == null) return;
            try
            {
                var man = victim.IsMount ? victim.RiderAgent : victim;
                if (man == null || !man.IsMainAgent || !H447AnyThread(by)) return;
                Interlocked.Increment(ref _h7HitN);
                if (blow.InflictedDamage > 0) Interlocked.Add(ref _h7Dmg, blow.InflictedDamage);
            }
            catch { }
        }

        /// <summary>BattleSense.OnAgentRemoved (glowny watek): gracz padl w czasie polowania (od jezdzca polowania albo innego).</summary>
        internal static void H447OnRemoved(Agent a, Agent by, AgentState state)
        {
            if (!_h7Live || !_h7On || a == null) return;
            try
            {
                if (!ReferenceEquals(a, _h7Target)) return;
                if (state != AgentState.Killed && state != AgentState.Unconscious) return;
                _h7Down++;
                if (H447AnyThread(by)) _h7DownBy++;
            }
            catch { }
        }

        /// <summary>HorsePursuit.BeforeWithdraw (143, glowny watek, przed pierwszym ruchem 143): jezdziec polowania oddany - cel automatyczny, pozycji nie ruszamy.</summary>
        internal static void H447BeforeWithdraw(Agent a)
        {
            if (!_h7On || !H447AnyThread(a)) return;
            int i = _h7Riders.IndexOf(a);
            if (i >= 0) _h7Riders.RemoveAt(i);
            H447Release(a, HuWithdraw, true);
        }

        // ------------------------------------------------------------ takt glownego watku (Doctrine.Tick, co klatke)

        internal static void H447Tick(Mission m, float dt)
        {
            if (!_h7Live || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m) || m.MissionEnded) return;
            bool dep;
            try { dep = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && m.CurrentTime >= P440NoDeploy); } catch { dep = false; }
            if (!dep) return;
            float now = m.CurrentTime;
            _h7Acc += dt;
            _h7StepAcc += dt;
            if (_h7Acc >= H447Every)
            {
                float step = _h7Acc;
                _h7Acc = 0f;
                _h7StepAcc = 0f;
                try { H447Pass(m, now, step); }
                catch (Exception e)
                {
                    _h7Stum++;
                    if (_h7On) { try { H447Stop(HrErr); } catch { } }   // 447-p: wlasny powod konca (nie "rola oddana")
                    RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Hunt447 pass", e);
                }
                return;
            }
            if (_h7On && _h7StepAcc >= H447Step)
            {
                _h7StepAcc = 0f;
                H447Steer(now);
            }
        }

        private static void H447Pass(Mission m, float now, float step)
        {
            // strzaly gracza od ostatniego passu
            int sn = Volatile.Read(ref _h7ShotN);
            if (sn != _h7ShotSeen) { _h7ShotSeen = sn; _h7LastShot = now; }
            var pm = Agent.Main;
            // 447-p: pomiar (wodz / z tylu / powody) przez cala bitwe, takze po decyzji - liczby linii to czasy z calej bitwy (porownywalne TAK / NIE)
            Team pt = null;
            bool gen = false;
            int why = HnFront;
            Vec2 pos = Vec2.Invalid;
            try { gen = pm != null && pm.IsActive() && (pt = pm.Team) != null && MissionTeams.Valid(pt) && pt.IsPlayerGeneral; } catch { gen = false; }
            if (!gen) { _h7RearSince = -1f; _h7PrevPos = Vec2.Invalid; }
            else
            {
                try
                {
                    _h7GenSec += step;
                    pos = pm.Position.AsVec2;
                    why = H447NotRear(m, pm, pt, pos, now, step);
                    _h7PrevPos = pos;
                    if (why >= 0) { _h7NoSec[why] += step; _h7RearSince = -1f; }
                    else { _h7RearSec += step; if (_h7RearSince < 0f) _h7RearSince = now; }
                }
                catch (Exception e)
                {
                    // potkniecie pomiaru nie konczy polowania w toku
                    _h7Stum++; why = HnFront; _h7RearSince = -1f;
                    RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Hunt447 pomiar", e);
                }
            }
            if (_h7On) { _h7HuntSec += step; H447HuntPass(m, pm, now); return; }
            if (_h7Done || !gen || why >= 0) return;
            if (now - _h7RearSince < H447Hold) return;
            Formation f = H447PickCav(m, pt, pos);
            if (f == null) return;
            var c = Settings.Current;
            if (c == null || !c.CavalryHuntsRearCommander || !C441Live)
            {
                // klucz NIE / bez dowodcy jazdy: bylby - raz na bitwe (jak polowanie)
                _h7Dry++;
                _h7Done = true;
                return;
            }
            H447Start(m, pm, f, now);
        }

        /// <summary>-1 = gracz-wodz stoi z tylu; inaczej powod (Hn*).</summary>
        private static int H447NotRear(Mission m, Agent pm, Team pt, Vec2 pos, float now, float step)
        {
            // stoi
            if (_h7PrevPos.IsValid && step > 0.01f && pos.Distance(_h7PrevPos) / step > H447Still) return HnMove;
            // strzela
            if (now - _h7LastShot < H447ShotWin) return HnShot;
            // walczy wrecz
            if (now - pm.LastMeleeAttackTime < H447MeleeWin || now - pm.LastMeleeHitTime < H447MeleeWin) return HnMelee;
            // wrog w 80 m
            _h7Buf.Clear();
            m.GetNearbyEnemyAgents(pos, H447Fight, pt, _h7Buf);
            bool foe = false;
            for (int i = 0; i < _h7Buf.Count; i++) { var x = _h7Buf[i]; if (x != null && x.IsHuman && x.IsActive()) { foe = true; break; } }
            _h7Buf.Clear();
            if (foe) return HnFight;
            // straz
            m.GetNearbyAllyAgents(pos, H447GuardR, pt, _h7Buf);
            int guard = 0;
            for (int i = 0; i < _h7Buf.Count; i++) { var x = _h7Buf[i]; if (x != null && x.IsHuman && x.IsActive() && !x.IsMainAgent) guard++; }
            _h7Buf.Clear();
            if (guard >= H447GuardMen) return HnGuard;
            // z tylu: szyk swojej strony o >= 30 m blizej najblizszego szyku wroga niz gracz
            var teams = m.Teams;
            int n = teams != null ? teams.Count : 0;
            Vec2 ep = Vec2.Invalid;
            float de = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var et = teams[i];
                if (!MissionTeams.Enemies(pt, et)) continue;
                var efl = et.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; efl != null && j < efl.Count; j++)
                {
                    var e = efl[j];
                    if (e == null || e.CountOfUnits < R443CavMin) continue;
                    Vec2 p = e.CachedMedianPosition.AsVec2;
                    if (!p.IsValid) continue;
                    float d = pos.Distance(p);
                    if (d < de) { de = d; ep = p; }
                }
            }
            if (!ep.IsValid) return HnFront;
            for (int i = 0; i < n; i++)
            {
                var ft = teams[i];
                if (!MissionTeams.Valid(ft) || ft.Side != pt.Side) continue;
                var fl = ft.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    var x = fl[j];
                    if (x == null || x.CountOfUnits < H447FriendMen) continue;
                    Vec2 p = x.CachedMedianPosition.AsVec2;
                    if (p.IsValid && p.Distance(ep) <= de - H447Behind) return -1;
                }
            }
            return HnFront;
        }

        /// <summary>Najblizszy graczowi wolny szyk jazdy AI wroga z wolna droga (opis klasy); powody odrzucenia do linii (raz na takt dla calej strony).</summary>
        private static Formation H447PickCav(Mission m, Team pt, Vec2 pos)
        {
            Formation best = null;
            float bd = float.MaxValue;
            bool any = false;
            int why = HcNone;
            var teams = m.Teams;
            int n = teams != null ? teams.Count : 0;
            _t4Now = m.CurrentTime;   // T442MeleeShare (glowny watek)
            for (int i = 0; i < n; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!R443AiTeam(t) || !MissionTeams.Enemies(pt, t)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits < R443CavMin || C441Idx(f) < 0) continue;
                    var q = f.QuerySystem;
                    if (q == null || !q.IsCavalryFormation || q.IsRangedCavalryFormation) continue;
                    if (!f.IsAIControlled || HorsePursuit.PlayerForm(f) || f.HasPlayerControlledTroop) continue;
                    any = true;
                    Vec2 me = f.CachedAveragePosition;
                    if (!me.IsValid) continue;
                    float d = me.Distance(pos);
                    if (C441RoleOf(f) != CrNone) { if (why < HcRole) why = HcRole; continue; }
                    if (d > H447Range) { if (why < HcFar) why = HcFar; continue; }
                    if (d >= bd) continue;
                    if (T442MeleeShare(f) >= T442OwnMelee) { if (why < HcMelee) why = HcMelee; continue; }
                    if (H447Blocked(pt, me, pos)) { if (why < HcBlocked) why = HcBlocked; continue; }
                    int free, busy, all;
                    H447CountRiders(f, out free, out busy, out all);
                    if (all > 0 && busy >= H447BusyMax * all) { if (why < HcBusy) why = HcBusy; continue; }
                    if (free < H447MinRiders) { if (why < HcFew) why = HcFew; continue; }
                    best = f; bd = d;
                }
            }
            if (best == null) _h7NoCav[any ? why : HcNone]++;
            return best;
        }

        /// <summary>Czy szyk piechoty strony gracza (>= 10 ludzi) zaslania odcinek a - b (pol szerokosci + 10 m, jak T442Blocked).</summary>
        private static bool H447Blocked(Team pt, Vec2 a, Vec2 b)
        {
            var teams = pt.Mission != null ? pt.Mission.Teams : null;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var ft = teams[i];
                if (!MissionTeams.Valid(ft) || ft.Side != pt.Side) continue;
                var fl = ft.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    var x = fl[j];
                    if (x == null || x.CountOfUnits < T442BlockMen) continue;
                    var xq = x.QuerySystem;
                    if (xq == null || !xq.IsInfantryFormation) continue;
                    Vec2 xp = x.CachedMedianPosition.AsVec2;
                    if (!xp.IsValid) continue;
                    if (SegDist442(xp, a, b) < 0.5f * x.Width + T442BlockPad) return true;
                }
            }
            return false;
        }

        /// <summary>Jezdzcy szyku: wolni do wziecia, zajeci przez 148 / 204 / 283a, wszyscy konni.</summary>
        private static void H447CountRiders(Formation f, out int free, out int busy, out int all)
        {
            free = busy = all = 0;
            _h7Units.Clear();
            f.ApplyActionOnEachUnit(H447Collect);
            for (int i = 0; i < _h7Units.Count; i++)
            {
                var a = _h7Units[i];
                if (a == null || !a.IsActive() || a.MountAgent == null) continue;
                all++;
                if (HorsePursuit.RidingAnyThread(a) || HorsePursuit.HuntAnyThread(a) || HorsePursuit.TestRunAnyThread(a)) { busy++; continue; }
                if (H447RiderOk(a)) free++;
            }
            _h7Units.Clear();
        }

        /// <summary>Jezdziec do wziecia (opis klasy).</summary>
        private static bool H447RiderOk(Agent a)
        {
            if (a == null || !a.IsActive() || !a.IsHuman || a.IsMainAgent || !a.IsAIControlled || a.IsHero) return false;
            var mt = a.MountAgent;
            if (mt == null || MarchToContact.IsDragon(mt)) return false;
            if (Undead.Character(a.Character) || HorsePursuit.Banner(a)) return false;
            if (a.IsRunningAway || SurvivalMode.InPanic(a) || SurvivalMode.OrderedRetreat(a, a.CommonAIComponent) || H447Withdrawing(a)) return false;
            if (HorsePursuit.RidingAnyThread(a) || HorsePursuit.HuntAnyThread(a) || HorsePursuit.TestRunAnyThread(a) || H447AnyThread(a)) return false;
            return a.CanBeAssignedForScriptedMovement();
        }

        private static bool H447Withdrawing(Agent a)
        {
            var k = RoutHold.Live;
            return k != null && k.Withdrawing(a);
        }

        // ------------------------------------------------------------ polowanie

        private static void H447Start(Mission m, Agent pm, Formation f, float now)
        {
            var wp = pm.GetWorldPosition();
            if (!wp.IsValid) return;
            if (!C441SetRole(f, CrHunt, MovementOrder.MovementOrderMove(wp))) { _h7Refused++; _h7Done = true; return; }
            _h7On = true; _h7Done = true;
            _h7F = f; _h7Target = pm; _h7Start = now; _h7FirstReach = -1f;
            _h7Men0 = f.CountOfUnits;
            _h7OrdAt = wp.AsVec2;
            _h7Riders.Clear(); _h7Reached.Clear(); _h7TakenIdx.Clear();
            _h7BusySeen = false;
            _h7Starts++;
            H447Take(f, false);
            _h7Cnt0 = _h7Riders.Count;
            try { InformationManager.DisplayMessage(new InformationMessage("Enemy horsemen ride for you!", Colors.Red)); } catch { }
            try
            {
                var sb = new StringBuilder(260);
                sb.Append("Polowanie na wodza (447) start: t ").Append(now.ToString("0", H7Inv)).Append(" s | szyk ").Append(f.FormationIndex).Append(" druzyny ")
                  .Append(f.Team != null ? f.Team.TeamIndex : -1).Append(", ludzi ").Append(_h7Men0).Append(", jezdzcow wzietych ").Append(_h7Cnt0)
                  .Append(" | odleglosc do gracza ").Append(f.CachedAveragePosition.Distance(pm.Position.AsVec2).ToString("0", H7Inv)).Append(" m")
                  .Append(" | gracz z tylu od ").Append(_h7RearSince >= 0f ? (now - _h7RearSince).ToString("0", H7Inv) : "-").Append(" s.");
                Log.Info(sb.ToString());
            }
            catch { }
        }

        /// <summary>Wolni jezdzcy szyku pod sterowanie (na starcie i w kazdym passie - zwolnieni przez 148 / 204 dolaczaja).</summary>
        private static void H447Take(Formation f, bool late)
        {
            var pm = _h7Target;
            if (pm == null) return;
            _h7Units.Clear();
            f.ApplyActionOnEachUnit(H447Collect);
            for (int i = 0; i < _h7Units.Count; i++)
            {
                var a = _h7Units[i];
                if (!H447RiderOk(a)) continue;
                bool added = false;
                try
                {
                    // 447-p: wpis CleanShot 310 zdjety (jak 204 StartHunter) - jego Threat / termin nie zdejmie celu polowania
                    CleanShot.Yield(a);
                    a.SetAutomaticTargetSelection(false);
                    a.SetTargetAgent(pm);
                    H447Mark(a, true);
                    _h7Riders.Add(a);
                    added = true;
                    if (_h7TakenIdx.Add(a.Index)) { _h7Taken++; if (late) _h7Late++; }
                    else _h7Retake++;   // 447-p: ten sam jezdziec wziety ponownie (np. po panice) - osobno
                    H447Push(a, pm);
                }
                catch (Exception e)
                {
                    // 447-p: jezdziec zdjety z listy (inaczej krok dalej by go pchal mimo zwolnienia); powod -1 (potkniecie liczy _h7Stum)
                    _h7Stum++;
                    if (added) _h7Riders.Remove(a);
                    H447Release(a, -1);
                    RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Hunt447 jezdziec", e);
                }
            }
            _h7Units.Clear();
        }

        private static void H447Push(Agent a, Agent pm)
        {
            var wp = pm.GetWorldPosition();
            if (!wp.IsValid) return;
            a.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);
        }

        private static void H447HuntPass(Mission m, Agent pm, float now)
        {
            var f = _h7F;
            // 447-p: klucz NIE (albo bez dowodcy jazdy 441) w trakcie - polowanie konczone od razu (jak 444)
            var c = Settings.Current;
            if (c == null || !c.CavalryHuntsRearCommander || !C441Live) { H447Stop(HrKey); return; }
            // 447-p: najpierw stan celu - gra zeruje Agent.Main zaraz po OnAgentRemoved, wiec padly gracz dawal dotad "gracz zniknal"
            var tg = _h7Target;
            if (tg == null || !tg.IsActive()) { H447Stop(_h7Down > 0 ? HrDown : HrGone); return; }
            if (pm == null || !ReferenceEquals(pm, tg)) { H447Stop(HrGone); return; }
            if (f == null || f.CountOfUnits <= 0 || C441RoleOf(f) != CrHunt) { H447Stop(HrBack); return; }
            if (now - _h7Start > H447Max) { H447Stop(HrTime); return; }
            if (f.CountOfUnits <= (1f - H447Loss) * _h7Men0) { H447Stop(HrLoss); return; }
            // rozkaz szyku na miejsce gracza, gdy odszedl
            var wp = pm.GetWorldPosition();
            if (wp.IsValid && (!_h7OrdAt.IsValid || wp.AsVec2.Distance(_h7OrdAt) > H447Refresh))
            {
                if (C441SetRole(f, CrHunt, MovementOrder.MovementOrderMove(wp))) _h7OrdAt = wp.AsVec2;
            }
            H447Take(f, true);
            H447Steer(now);
            if (_h7Riders.Count == 0) H447Stop(HrNoRiders);
        }

        /// <summary>Krok co 0.25 s (i w passie): jezdzcy na gracza; zwolnienie pojedynczo; dojazd w 5 m.</summary>
        private static void H447Steer(float now)
        {
            var pm = _h7Target;
            var f = _h7F;
            if (pm == null || !pm.IsActive() || f == null) return;
            Vec2 pp = pm.Position.AsVec2;
            for (int i = _h7Riders.Count - 1; i >= 0; i--)
            {
                var a = _h7Riders[i];
                int why = -1;
                try
                {
                    if (a == null || !a.IsActive()) why = HuDown;
                    else if (a.MountAgent == null) why = HuHorse;
                    else if (H447Withdrawing(a)) why = HuWithdraw;
                    else if (a.IsRunningAway || SurvivalMode.InPanic(a) || SurvivalMode.OrderedRetreat(a, a.CommonAIComponent)) why = HuPanic;
                    else if (!ReferenceEquals(a.Formation, f)) why = HuForm;
                    if (why >= 0) { _h7Riders.RemoveAt(i); H447Release(a, why); continue; }
                    H447Push(a, pm);
                    if (a.Position.AsVec2.DistanceSquared(pp) <= H447Reach * H447Reach && _h7Reached.Add(a.Index))
                    {
                        _h7ReachN++;
                        if (_h7FirstReach < 0f) _h7FirstReach = now - _h7Start;
                    }
                }
                catch (Exception e)
                {
                    _h7Stum++;
                    _h7Riders.RemoveAt(i);
                    H447Release(a, -1);   // 447-p: potkniecie liczy _h7Stum, nie "zmiana szyku"
                    RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Hunt447 krok", e);
                }
            }
        }

        /// <summary>Jezdziec oddany grze: cel automatyczny, pozycja skryptowa zdjeta (przy wycofaniu 143 - jego pozycja zostaje).</summary>
        private static void H447Release(Agent a, int why, bool force = false)
        {
            if (a == null) return;
            H447Mark(a, false);
            if (why >= 0 && why < HuK) _h7Rel[why]++;
            if (why == HuDown) _h7RidersLost++;
            try
            {
                if (!a.IsActive()) return;
                a.SetAutomaticTargetSelection(true);
                a.InvalidateTargetAgent();
                // force: BeforeWithdraw (przed pierwszym ruchem 143 - nasza pozycja zdjeta, jak 148); wycofanie juz w toku - pozycja 143 zostaje
                bool touch = force || a.IsRunningAway || !H447Withdrawing(a);
                if (touch && (a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) a.DisableScriptedMovement();
            }
            catch { }
        }

        /// <summary>Koniec polowania: wszyscy jezdzcy oddani, rola NONE (RBM wraca). why &lt; 0 - koniec misji (licznik "w toku" u wolajacego).</summary>
        private static void H447Stop(int why)
        {
            if (why >= 0 && why < HrK) _h7End[why]++;
            for (int i = _h7Riders.Count - 1; i >= 0; i--) H447Release(_h7Riders[i], -1);
            _h7Riders.Clear();
            var f = _h7F;
            try { if (f != null && C441RoleOf(f) == CrHunt) C441SetRole(f, CrNone, default(MovementOrder)); }
            catch (Exception e) { _h7Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Hunt447 oddanie", e); }
            _h7On = false;
            _h7F = null;
        }

        // ------------------------------------------------------------ linia konca bitwy (Battle291.Write)

        internal static string H447Line(string kind)
        {
            var c = Settings.Current;
            var sb = new StringBuilder(1100);
            sb.Append("Polowanie na wodza (447): misja ").Append(kind ?? "-")
              .Append(" | klucz polowanie na wodza z tylu ").Append(c != null && c.CavalryHuntsRearCommander ? "TAK" : "NIE")
              .Append(", dowodca jazdy ").Append(c != null && c.CavalryCommander ? "TAK" : "NIE")
              .Append(" | gracz wodzem ").Append(_h7GenSec.ToString("0", H7Inv)).Append(" s, z tylu (wszystkie warunki) ").Append(_h7RearSec.ToString("0", H7Inv))
              .Append(" s (start po ").Append(H447Hold.ToString("0", H7Inv)).Append(" s bez przerwy) | nie z tylu (s):");
            for (int k = 0; k < HnK; k++) sb.Append(k > 0 ? "," : "").Append(' ').Append(HnName[k]).Append(' ').Append(_h7NoSec[k].ToString("0", H7Inv));
            sb.Append(" | brak szyku (takty):");
            for (int k = 0; k < HcK; k++) sb.Append(k > 0 ? "," : "").Append(' ').Append(HcName[k]).Append(' ').Append(_h7NoCav[k]);
            sb.Append(" | proby ").Append(_h7Starts).Append(", bylby (klucz NIE / bez dowodcy jazdy) ").Append(_h7Dry).Append(", rola odrzucona ").Append(_h7Refused)
              .Append(" | jezdzcow wzietych ").Append(_h7Taken).Append(" (na starcie ").Append(_h7Cnt0).Append(", dolaczonych pozniej ").Append(_h7Late).Append(')')
              .Append(", ponownie wzietych ").Append(_h7Retake)
              .Append(", ").Append(_h7HuntSec.ToString("0", H7Inv)).Append(" s polowania")
              .Append(" | dojazd: pierwszy w ").Append(H447Reach.ToString("0", H7Inv)).Append(" m od gracza po ")
              .Append(_h7FirstReach >= 0f ? _h7FirstReach.ToString("0.0", H7Inv) + " s" : "-").Append(", jezdzcow w ").Append(H447Reach.ToString("0", H7Inv)).Append(" m ").Append(_h7ReachN)
              .Append(" | wynik: ciosy w gracza / konia ").Append(Volatile.Read(ref _h7HitN)).Append(", obrazenia ").Append(Volatile.Read(ref _h7Dmg))
              .Append(", gracz padl w polowaniu ").Append(_h7Down).Append(" (od jezdzca polowania ").Append(_h7DownBy).Append(')')
              .Append(", jezdzcow padlo ").Append(_h7RidersLost)
              .Append(" | koniec:");
            for (int k = 0; k < HrK; k++) sb.Append(k > 0 ? "," : "").Append(' ').Append(HrName[k]).Append(' ').Append(_h7End[k]);
            sb.Append(", w toku przy koncu ").Append(_h7Open).Append(" | jezdzcy zwolnieni pojedynczo:");
            for (int k = 0; k < HuK; k++) sb.Append(k > 0 ? "," : "").Append(' ').Append(HuName[k]).Append(' ').Append(_h7Rel[k]);
            sb.Append(" | rajd 443 wstrzymany (polowania) ").Append(_h7Busy443).Append(" | potkniecia ").Append(_h7Stum).Append('.');
            return sb.ToString();
        }
    }
}
