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
    /// 157 P3 (26.10, Jeff 25.10 ok. 22:45: kawalerzysta "nie zaatakowal mnie tylko stal jak debil i dostawal strzaly"; plan 23:55 bez sprzeciwu: "1-2 jezdzcow
    /// moze na 15-20 s porzucic oslone skrzydla, zeby dopasc lucznika w 60 m, ktory do nich strzela"). Klucz RidersRunDownShooters.
    /// Wejscie TYLKO po prawdziwym trafieniu (ShieldsUp.RiderHit z kolejki trafien 140, glowny watek): jezdziec AI z bronia do walki wrecz (nie konny lucznik
    /// z amunicja - IsRangedCached) w szyku konnicy AI (QuerySystem.IsCavalryFormation, nie szyk gracza - PlayerForm), szyk NIE szarzuje (ShieldsUp.FormCharging:
    /// rozkaz Charge / ChargeToTarget albo zachowanie szarzy - szarza RBM w kazdym stanie, szarza gry, oslona skrzydla szarzuje / wraca; trzymajaca oslona = Move), strzelec w 60 m od jezdzca i nie w 15 m od nietknietego szyku pieszego swojej
    /// strony (szyk od 8 ludzi, nie konny - jak IntactMin 148), najwyzej 2 jezdzcow szyku naraz, kon nie zajechany (HorseAiRestBelow 136), jezdziec nie dalej
    /// niz 150 m od miejsca szyku. Wykluczeni: gracz i ludzie pod jego sterowaniem, bohaterowie (149 HeroCaution ustawia ich pozycja skryptowa), chorazowie
    /// (RBM sam steruje ich celem), smoki ROT, umarli, jezdzcy w poscigu 148, panika / odwrot 143, jezdziec z pozycja skryptowa kogos innego albo przy
    /// obiekcie (Agent.CanBeAssignedForScriptedMovement - gra).
    /// Sterowanie - wzor 148 (sprawdzony w dekompilacji 1.4.8): SetAutomaticTargetSelection(false) + SetTargetAgent(strzelec) + SetScriptedPosition(miejsce
    /// strzelca) co 0.25 s, bez NoAttack i DoNotRun. Koniec: 20 s, strzelec padl, strzelec dalej niz 80 m, kon ponizej HorseAiRestBelow, jezdziec ponad 150 m
    /// od miejsca szyku (OrderPosition), szyk ruszyl do szarzy, strzelec wszedl w szyk pieszy (pass co 1 s), panika / odwrot / wiwat / koniec misji / gracz
    /// przejal szyk / klucz wylaczony; potem cel automatyczny, pozycja skryptowa zdjeta (nie u stojacego w odwrocie 143 - ta jest jego) i przerwa 30 s.
    /// Zgodnosc: 136/128 - MarchToContact.PostSpeed pomija cap marszu / odpoczynku u kontrujacego (CounterAnyThread, tablica wedlug Agent.Index jak _riding);
    /// 148 - kontrujacy nie wchodzi do poscigu (RidersPass), gonacy nie kontruje; 143 - BeforeWithdraw zdejmuje kontre przed pierwszym ruchem odwrotu;
    /// 140 P2 - kontrujacy nie zmienia broni ani preferencji (ShieldsUp.One - recenzja 157). Klucz NIE: ta sama ocena na sucho (licznik "na sucho"), bez sterowania.
    /// Segment "kontra na strzelca (157)" w linii "poscig (148)". Wyjatek na jezdzcu = licznik; po 5 kontra konczy sie (bez globalnego wylacznika).
    /// </summary>
    internal static partial class HorsePursuit
    {
        // ------------------------------------------------------------ stale (plan Jeffa 25.10 23:55)
        internal const float CounterReach = 60f;      // strzelec najwyzej tak daleko od jezdzca przy wejsciu
        internal const float CounterLoseAt = 80f;     // strzelec dalej - koniec kontry
        internal const float CounterFootGap = 15f;    // strzelec blizej niz tyle od nietknietego szyku pieszego swojej strony - bez kontry / koniec
        internal const float CounterMaxSec = 20f;     // najdluzej tyle sekund (plan: 15-20 s; strzelec w 60 m jest w zasiegu galopu w 6-8 s)
        internal const float CounterFromForm = 150f;  // jezdziec dalej od miejsca szyku - koniec
        internal const float CounterCool = 30f;       // przerwa jezdzca po kontrze
        internal const int CounterPerForm = 2;        // najwyzej tylu jezdzcow szyku naraz

        // nie zaczal (przypadki przy trafieniu bez szarzy szyku)
        private const int CNoEx = 0, CNoRanged = 1, CNoPanic = 2, CNoChase = 3, CNoBusy = 4, CNoCool = 5, CNoLimit = 6, CNoHorse = 7, CNoFar = 8,
                          CNoShooter = 9, CNoRange = 10, CNoFoot = 11, CNoEnded = 12, CNoKinds = 13;
        // koniec kontry
        private const int KTime = 0, KShooterDown = 1, KAway = 2, KHorse = 3, KFarForm = 4, KFormCharge = 5, KFoot = 6, KPanic = 7, KGone = 8, KEnded = 9,
                          KPlayer = 10, KKey = 11, KOther = 12, KKinds = 13;
        // wykluczeni (rozni ludzie)
        private const int CxHero = 0, CxBanner = 1, CxDragon = 2, CxDead = 3, CxPlayer = 4, CxKinds = 5;

        private sealed class Counter
        {
            public Agent A, S;
            public Formation F;
            public float Start, MaxD;
            public int Fails;
        }

        private static bool _cKey;
        private static readonly Dictionary<Agent, Counter> Counters = new Dictionary<Agent, Counter>();
        private static readonly List<Counter> CounterList = new List<Counter>();
        private static readonly Dictionary<Agent, float> CounterCoolUntil = new Dictionary<Agent, float>();
        // dla latki PostSpeed (watki robocze): jezdziec w kontrze wedlug Agent.Index; pisze tylko glowny watek (wzor _riding)
        private static Agent[] _countering;

        // liczniki misji (glowny watek; _cCapSkip przez Interlocked)
        private static int _cShots, _cShotsCharge, _cShotsNoCharge, _cAgain, _cDry, _cStarts, _cOnPlayer, _cSets, _cKilled, _cShooterOthers, _cRidersLost, _cDropped, _cFails;
        private static int _cCapSkip;
        private static float _cDurSum, _cDurMax, _cMaxFar, _cMaxD;
        private static readonly int[] CNo = new int[CNoKinds];
        private static readonly int[] CEnd = new int[KKinds];
        private static readonly int[] CEx = new int[CxKinds];
        private static readonly HashSet<int> CExSeen = new HashSet<int>();
        private static readonly HashSet<int> CounterRiders = new HashSet<int>();
        private static readonly HashSet<int> CounterShooters = new HashSet<int>();
        private static readonly HashSet<Formation> CounterForms = new HashSet<Formation>();
        private static readonly HashSet<Agent> ShootersDown = new HashSet<Agent>();

        // ------------------------------------------------------------ dla innych paczek

        /// <summary>MarchToContact.PostSpeed (watki robocze): czlowiek jest jezdzcem w kontrze (157) - bez capu marszu / odpoczynku. Bez blokady.</summary>
        internal static bool CounterAnyThread(Agent a)
        {
            if (a == null) return false;
            var k = Volatile.Read(ref _countering);
            if (k == null) return false;
            int i = a.Index;
            return i >= 0 && i < k.Length && ReferenceEquals(k[i], a);
        }

        /// <summary>MarchToContact.PostSpeed: cap marszu pominiety u jezdzca w kontrze (licznik segmentu).</summary>
        internal static void NoteCounterCapSkip() { Interlocked.Increment(ref _cCapSkip); }

        /// <summary>ShieldsUp.One (glowny watek): jezdziec teraz w kontrze na strzelca (bron i preferencja zamrozone).</summary>
        internal static bool Countering(Agent a)
        {
            return a != null && Counters.ContainsKey(a);
        }

        private static void MarkCounter(Agent a, bool on)
        {
            if (a == null) return;
            int i = a.Index;
            if (i < 0 || i >= RidingMaxIndex) return;
            var k = _countering;
            if (k == null || i >= k.Length)
            {
                if (!on) return;
                int n = k == null ? 1024 : k.Length;
                while (n <= i) n *= 2;
                var nk = new Agent[n];
                if (k != null) Array.Copy(k, nk, k.Length);
                nk[i] = a;
                Volatile.Write(ref _countering, nk);
                return;
            }
            if (on) k[i] = a;
            else if (ReferenceEquals(k[i], a)) k[i] = null;
        }

        // ------------------------------------------------------------ poczatek / koniec misji

        private static void CounterBegin(Settings c)
        {
            _cKey = c != null && c.RidersRunDownShooters;
        }

        private static void CounterClear()
        {
            foreach (var c in CounterList) MarkCounter(c.A, false);
            Volatile.Write(ref _countering, null);
            Counters.Clear(); CounterList.Clear(); CounterCoolUntil.Clear();
            _cShots = _cShotsCharge = _cShotsNoCharge = _cAgain = _cDry = _cStarts = _cOnPlayer = _cSets = _cKilled = _cShooterOthers = _cRidersLost = _cDropped = _cFails = 0;
            Interlocked.Exchange(ref _cCapSkip, 0);
            _cDurSum = _cDurMax = _cMaxFar = _cMaxD = 0f;
            Array.Clear(CNo, 0, CNo.Length);
            Array.Clear(CEnd, 0, CEnd.Length);
            Array.Clear(CEx, 0, CEx.Length);
            CExSeen.Clear(); CounterRiders.Clear(); CounterShooters.Clear(); CounterForms.Clear(); ShootersDown.Clear();
        }

        /// <summary>End (przed linia): wszystkie kontry zakonczone, jezdzcy oddani grze.</summary>
        private static void CounterEndAll(float now)
        {
            for (int i = CounterList.Count - 1; i >= 0; i--)
            {
                if (i >= CounterList.Count) continue;
                CounterEnd(CounterList[i], KEnded, now, false);
            }
        }

        // ------------------------------------------------------------ wejscie (ShieldsUp.RiderHit, glowny watek)

        /// <summary>Jezdziec AI z szyku konnicy AI trafiony pociskiem (on albo kon) przez strzelca s; charging = szyk w szarzy (ShieldsUp.FormCharging).</summary>
        internal static void OnRiderShot(Mission m, Agent a, Agent s, bool horse, bool charging)
        {
            if (!_live || a == null || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;
            try
            {
                if (!a.IsActive()) return;   // padl od tego pocisku - nic do kontry (pomiar P1 liczy go osobno)
                float now = m.CurrentTime;
                _cShots++;
                if (charging) { _cShotsCharge++; return; }
                _cShotsNoCharge++;
                if (Counters.ContainsKey(a)) { _cAgain++; return; }
                int why = CounterWhy(m, a, s, now);
                if (why >= 0) { CNo[why]++; return; }
                if (!_cKey) { _cDry++; return; }   // klucz NIE: ocena na sucho, bez sterowania
                CounterStart(a, s, now);
            }
            catch (Exception e) { _cFails++; Stumble("HorsePursuit.KontraWejscie", e); }
        }

        private static void NoteCx(Agent a, int x)
        {
            if (CExSeen.Add((a.Index << 3) | x)) CEx[x]++;
        }

        /// <summary>Przyczyna braku kontry (CNo*) albo -1 = moze.</summary>
        private static int CounterWhy(Mission m, Agent a, Agent s, float now)
        {
            if (!a.IsActive() || !a.IsHuman) return CNoEx;
            if (a.IsMainAgent || !a.IsAIControlled) { NoteCx(a, CxPlayer); return CNoEx; }
            if (a.IsHero) { NoteCx(a, CxHero); return CNoEx; }
            var mt = a.MountAgent;
            if (mt == null) return CNoEx;
            if (MarchToContact.IsDragon(mt)) { NoteCx(a, CxDragon); return CNoEx; }
            if (Undead.Character(a.Character)) { NoteCx(a, CxDead); return CNoEx; }
            if (Banner(a)) { NoteCx(a, CxBanner); return CNoEx; }
            if (a.IsRangedCached) return CNoRanged;   // konny lucznik / kusznik z amunicja strzela dalej
            if (a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a)) return CNoPanic;
            Rider r;
            if (RidingAnyThread(a) || (Riders.TryGetValue(a, out r) && r.St == RChase)) return CNoChase;
            var f = a.Formation;
            if (f == null || PlayerForm(f) || f.HasPlayerControlledTroop) { NoteCx(a, CxPlayer); return CNoEx; }
            if (m.MissionEnded || a.GetComponent<VictoryComponent>() != null) return CNoEnded;
            if (!a.CanBeAssignedForScriptedMovement()) return CNoBusy;   // pozycja skryptowa kogos innego (143 / 149), odlaczony, przy obiekcie
            float until;
            if (CounterCoolUntil.TryGetValue(a, out until) && now < until) return CNoCool;
            int inForm = 0;
            for (int i = 0; i < CounterList.Count; i++) if (ReferenceEquals(CounterList[i].F, f)) inForm++;
            if (inForm >= CounterPerForm) return CNoLimit;
            if (Blown(HorseS(a))) return CNoHorse;
            if (a.Position.AsVec2.Distance(Home(f)) > CounterFromForm) return CNoFar;
            if (s == null || !s.IsActive() || !s.IsHuman || s.Team == null || a.Team == null || !s.Team.IsEnemyOf(a.Team)) return CNoShooter;
            if (a.Position.AsVec2.Distance(s.Position.AsVec2) > CounterReach) return CNoRange;
            if (FootNear(m, s)) return CNoFoot;
            return -1;
        }

        /// <summary>Miejsce szyku: punkt rozkazu (oslona skrzydla - punkt trzymania), bez niego srodek szyku (u szyku 1-2 jezdzcow srodek idzie za kontrujacym).</summary>
        private static Vec2 Home(Formation f)
        {
            Vec2 p = Vec2.Invalid;
            try { p = f.OrderPosition; } catch { p = Vec2.Invalid; }
            if (p.IsValid && (p.x != 0f || p.y != 0f)) return p;
            return f.CachedAveragePosition;
        }

        /// <summary>Strzelec blizej niz CounterFootGap od nietknietego szyku pieszego swojej strony (szyk od IntactMin ludzi, nie konnica ani konni lucznicy;
        /// odleglosc od mediany minus promien szyku, najwyzej 40 m - jak IntactNear 148).</summary>
        private static bool FootNear(Mission m, Agent s)
        {
            var st = s.Team;
            if (st == null) return false;
            Vec2 p = s.Position.AsVec2;
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team t;
                try { t = teams[i]; } catch { continue; }
                if (!MissionTeams.Valid(t) || t.Side != st.Side) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits < IntactMin) continue;
                    var q = f.QuerySystem;
                    if (q == null || q.IsCavalryFormation || q.IsRangedCavalryFormation) continue;
                    var mp = f.CachedMedianPosition;
                    if (!mp.IsValid) continue;
                    float r = Math.Min(RadiusCap, 0.5f * Math.Max(f.Width, f.Depth));
                    if (p.Distance(mp.AsVec2) - r < CounterFootGap) return true;
                }
            }
            return false;
        }

        private static void CounterStart(Agent a, Agent s, float now)
        {
            var c = new Counter { A = a, S = s, F = a.Formation, Start = now };
            Counters[a] = c;
            CounterList.Add(c);
            MarkCounter(a, true);
            a.SetAutomaticTargetSelection(false);
            a.SetTargetAgent(s);
            _cStarts++;
            CounterRiders.Add(a.Index);
            CounterShooters.Add(s.Index);
            if (c.F != null) CounterForms.Add(c.F);
            if (s.IsMainAgent) _cOnPlayer++;
            CounterPush(c);
        }

        /// <summary>Ruch za strzelcem - pozycja skryptowa na jego miejscu (wzor Push 148), bez NoAttack i DoNotRun.</summary>
        private static void CounterPush(Counter c)
        {
            var wp = c.S.GetWorldPosition();
            if (!wp.IsValid) return;
            c.A.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);
            _cSets++;
        }

        // ------------------------------------------------------------ krok (Tick co 0.25 s; pass co 1 s)

        private static void CounterStep(Mission m, float now, bool pass)
        {
            if (CounterList.Count == 0) return;
            for (int i = CounterList.Count - 1; i >= 0; i--)
            {
                if (i >= CounterList.Count) continue;
                var c = CounterList[i];
                try
                {
                    int why = CounterCheck(m, c, now, pass);
                    if (why >= 0) { CounterEnd(c, why, now, true); continue; }
                    CounterPush(c);
                }
                catch (Exception e)
                {
                    _cFails++;
                    Stumble("HorsePursuit.Kontra", e);
                    if (++c.Fails >= FailsOut)
                    {
                        _cDropped++;
                        try { CounterEnd(c, KOther, now, true); }
                        catch { Counters.Remove(c.A); CounterList.Remove(c); MarkCounter(c.A, false); }
                    }
                }
            }
        }

        /// <summary>Kontrola kontry (-1 = dalej, inaczej przyczyna konca K*).</summary>
        private static int CounterCheck(Mission m, Counter c, float now, bool pass)
        {
            var a = c.A;
            if (a == null || !a.IsActive()) return KGone;
            if (a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a)) return KPanic;
            if (a.MountAgent == null || !ReferenceEquals(a.Formation, c.F)) return KOther;   // stracil konia albo zmienil szyk
            if (m.MissionEnded || a.GetComponent<VictoryComponent>() != null) return KEnded;
            if (c.F == null || PlayerForm(c.F)) return KPlayer;
            if (pass && !_cKey) return KKey;
            if (now - c.Start >= CounterMaxSec) return KTime;
            var s = c.S;
            if (s == null || !s.IsActive()) return KShooterDown;
            Vec2 p = a.Position.AsVec2;
            float d = p.Distance(s.Position.AsVec2);
            if (d > c.MaxD) c.MaxD = d;
            if (d > CounterLoseAt) return KAway;
            if (Blown(HorseS(a))) return KHorse;
            float df = p.Distance(Home(c.F));
            if (df > _cMaxFar) _cMaxFar = df;
            if (df > CounterFromForm) return KFarForm;
            if (ShieldsUp.FormCharging(c.F)) return KFormCharge;   // recenzja 157: ta sama miara co wejscie (rozkaz albo zachowanie szarzy)
            if (pass && FootNear(m, s)) return KFoot;
            return -1;
        }

        /// <summary>Koniec kontry: cel automatyczny, pozycja skryptowa zdjeta (chyba ze stoi w odwrocie 143 - ta jest jego), przerwa CounterCool.</summary>
        private static void CounterEnd(Counter c, int why, float now, bool cool)
        {
            Counters.Remove(c.A);
            CounterList.Remove(c);
            MarkCounter(c.A, false);
            CEnd[why]++;
            float dur = Math.Max(0f, now - c.Start);
            _cDurSum += dur;
            if (dur > _cDurMax) _cDurMax = dur;
            if (c.MaxD > _cMaxD) _cMaxD = c.MaxD;
            var a = c.A;
            if (a == null) return;
            if (cool) CounterCoolUntil[a] = now + CounterCool;
            if (!a.IsActive()) return;
            try { a.SetAutomaticTargetSelection(true); a.InvalidateTargetAgent(); } catch { }
            bool touch = a.IsRunningAway || !Withdrawing(a);
            if (!touch) return;
            try { if ((a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) a.DisableScriptedMovement(); } catch { }
        }

        /// <summary>BeforeWithdraw (143, glowny watek, przed pierwszym ruchem odwrotu): jezdziec w kontrze oddany grze, pozycja kontry zdjeta.</summary>
        private static void CounterBeforeWithdraw(Agent a)
        {
            Counter c;
            if (!Counters.TryGetValue(a, out c)) return;
            float now = 0f;
            try { now = Mission.Current != null ? Mission.Current.CurrentTime : 0f; } catch { }
            Counters.Remove(a);
            CounterList.Remove(c);
            MarkCounter(a, false);
            CEnd[KPanic]++;
            float dur = Math.Max(0f, now - c.Start);
            _cDurSum += dur;
            if (dur > _cDurMax) _cDurMax = dur;
            CounterCoolUntil[a] = now + CounterCool;
            if (!a.IsActive()) return;
            try { a.SetAutomaticTargetSelection(true); a.InvalidateTargetAgent(); } catch { }
            try { if ((a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) a.DisableScriptedMovement(); } catch { }
        }

        /// <summary>OnRemoved (glowny watek): jezdziec w kontrze padl / zszedl; strzelec padl (od kontrujacego jezdzca albo od innych) - raz na strzelca.</summary>
        private static void CounterRemoved(Mission m, Agent a, Agent by, bool down)
        {
            if (CounterList.Count == 0) return;
            float now = m != null ? m.CurrentTime : 0f;
            Counter c;
            if (Counters.TryGetValue(a, out c))
            {
                if (down) _cRidersLost++;
                CounterEnd(c, KGone, now, false);
            }
            bool target = false, byRider = false;
            for (int i = 0; i < CounterList.Count; i++)
            {
                var x = CounterList[i];
                if (!ReferenceEquals(x.S, a)) continue;
                target = true;
                if (by != null && (ReferenceEquals(by, x.A) || ReferenceEquals(by, x.A.MountAgent))) byRider = true;
            }
            if (!target || !down || !ShootersDown.Add(a)) return;
            if (byRider) _cKilled++; else _cShooterOthers++;
        }

        // ------------------------------------------------------------ segment linii "poscig (148)"

        private static void AppendCounter(StringBuilder sb)
        {
            sb.Append(" | kontra na strzelca (157): klucz ").Append(_cKey ? "TAK" : "NIE (tylko ocena na sucho)")
              .Append(" (jezdziec AI z bronia do walki wrecz w szyku konnicy AI, trafiony pociskiem, gdy szyk nie szarzuje; strzelec w ")
              .Append(F0(CounterReach)).Append(" m, nie w ").Append(F0(CounterFootGap)).Append(" m od nietknietego szyku pieszego; najwyzej ").Append(CounterPerForm)
              .Append(" na szyk; koniec po ").Append(F0(CounterMaxSec)).Append(" s, strzelec dalej niz ").Append(F0(CounterLoseAt)).Append(" m, kon ponizej ")
              .Append(_restBelow).Append(", ").Append(F0(CounterFromForm)).Append(" m od miejsca szyku; przerwa ").Append(F0(CounterCool)).Append(" s)")
              .Append(": trafien jezdzcow ").Append(_cShots).Append(" (szyk w szarzy ").Append(_cShotsCharge).Append(", bez szarzy ").Append(_cShotsNoCharge)
              .Append(", juz w kontrze ").Append(_cAgain).Append("), kontr ").Append(_cStarts).Append(" (jezdzcow ").Append(CounterRiders.Count)
              .Append(", szykow ").Append(CounterForms.Count).Append(", strzelcow ").Append(CounterShooters.Count).Append(", na gracza ").Append(_cOnPlayer)
              .Append("), na sucho ").Append(_cDry)
              .Append("; nie zaczeli: wykluczeni ").Append(CNo[CNoEx]).Append(", konny lucznik ").Append(CNo[CNoRanged]).Append(", panika / odwrot ").Append(CNo[CNoPanic])
              .Append(", w poscigu 148 ").Append(CNo[CNoChase]).Append(", zajety (pozycja skryptowa, obiekt) ").Append(CNo[CNoBusy]).Append(", przerwa ").Append(CNo[CNoCool])
              .Append(", limit szyku ").Append(CNo[CNoLimit]).Append(", kon ").Append(CNo[CNoHorse]).Append(", daleko od szyku ").Append(CNo[CNoFar])
              .Append(", strzelec niewazny ").Append(CNo[CNoShooter]).Append(", strzelec dalej niz ").Append(F0(CounterReach)).Append(" m ").Append(CNo[CNoRange])
              .Append(", strzelec przy szyku pieszym ").Append(CNo[CNoFoot]).Append(", koniec bitwy ").Append(CNo[CNoEnded])
              .Append(" (wykluczeni ludzie: bohaterowie ").Append(CEx[CxHero]).Append(", chorazowie ").Append(CEx[CxBanner]).Append(", smoki ").Append(CEx[CxDragon])
              .Append(", umarli ").Append(CEx[CxDead]).Append(", gracz / szyk gracza ").Append(CEx[CxPlayer]).Append(')')
              .Append("; koniec kontry: czas ").Append(CEnd[KTime]).Append(", strzelec padl ").Append(CEnd[KShooterDown]).Append(", strzelec dalej niz ")
              .Append(F0(CounterLoseAt)).Append(" m ").Append(CEnd[KAway]).Append(", kon ").Append(CEnd[KHorse]).Append(", daleko od szyku ").Append(CEnd[KFarForm])
              .Append(", szyk ruszyl do szarzy ").Append(CEnd[KFormCharge]).Append(", strzelec w szyku pieszym ").Append(CEnd[KFoot]).Append(", panika / odwrot ")
              .Append(CEnd[KPanic]).Append(", jezdziec padl ").Append(CEnd[KGone]).Append(", koniec misji / wiwat ").Append(CEnd[KEnded]).Append(", gracz przejal ")
              .Append(CEnd[KPlayer]).Append(", klucz ").Append(CEnd[KKey]).Append(", inne ").Append(CEnd[KOther])
              .Append("; sr. czas ").Append(_cStarts > 0 ? (_cDurSum / _cStarts).ToString("0.0", Inv) : "-").Append(" s (najdluzej ").Append(_cDurMax.ToString("0.0", Inv))
              .Append(" s), najdalej od miejsca szyku ").Append(F0(_cMaxFar)).Append(" m, od strzelca ").Append(F0(_cMaxD)).Append(" m")
              .Append("; strzelcy zabici przez kontrujacych ").Append(_cKilled).Append(" (padli od innych ").Append(_cShooterOthers).Append("), kontrujacy padli ")
              .Append(_cRidersLost).Append("; cap marszu pominiety u kontrujacych ").Append(Volatile.Read(ref _cCapSkip)).Append(", odswiezen celu ").Append(_cSets)
              .Append("; wyjatki kontry ").Append(_cFails).Append(" (zakonczone przez wyjatki ").Append(_cDropped).Append(')');
        }
    }
}
