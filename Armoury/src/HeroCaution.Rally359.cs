using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 359 (08.11, decyzja Jeffa: "dowodca AI JEDZIE do uciekajacych tylko z eskorta i nie w boj: tylko gdy uciekajacy sa za linia i daleko od wroga; ochrona dowodcow 335
    /// zostaje"). Tryb ModeRally wewnatrz HeroCaution (ta sama pozycja skryptowa 149 / 335, jeden wlasciciel ruchu bohatera):
    ///  - kto: dowodca AI (rola 335 RoleCmd - wodz / kapitan, nie szyk gracza), bez zagrozenia (GuardPass: zagrozenie 335 ma pierwszenstwo), na stanowisku albo bez
    ///    trybu, HP od 45%, klucze RallyRoutedMen + RoutedMenNeverRally + BrokenMenNeverRally i CommandersKeepCommandPost (straz 335);
    ///  - eskorta: zawsze co najmniej 3 wolnych straznikow 335 w 80 m (najpierw sam ich licznik, bez przydzialu) - jada z nim (Summon - ten sam mechanizm i krok
    ///    GuardsStep co przy zagrozeniu, limit czasu straznika RideMax + RideHoldMax); bez eskorty nie jedzie (zostaje zasieg pasywny 12 m). Przeglad 359: "eskorta
    ///    konna" szyku odrzucona - szyk nie jedzie za pozycja skryptowa wodza. Jedna grupa = jeden dowodca (RideTaken);
    ///  - cel: grupa uciekajacych z RoutKeeper.RallyTargets (co najmniej 3 w 15 m, za glownym szykiem od strony wroga, bez wroga w 45 m od srodka) do 120 m, dalej
    ///    niz zasieg pasywny 12 m; pozycja 6 m przed srodkiem grupy od strony dowodcy, sprawdzona jak 149 (pole, bez wroga w 10 m, droga); bez NoAttack (broni sie);
    ///  - koniec: dotarl do 12 m (zasieg pasywny RoutKeeper robi reszte) - stoi, dopoki ktos zbiera sie przy nim (8 s, najwyzej 15 s); w drodze ponad 20 s; grupa
    ///    znikla albo podeszla do wroga; zagrozenie 335, rana 149 / 143, panika, gracz, odwrot na rozkaz (EndMode). Potem pozycja zdjeta, nastepny pass - stanowisko 335.
    ///  - najwyzej 3 wyjazdy na dowodce na bitwe, przerwa 20 s po wyjezdzie (5 s po odmowie: bez eskorty / bez miejsca).
    /// Liczniki w Rally359 (linia "ZBIORKA (359)"). Wyjatek - jak reszta passu HeroCaution (licznik, po 5 bohater wypada).
    /// </summary>
    internal static partial class HeroCaution
    {
        private const int ModeRally = 5;
        internal const float RideMax = 20f, RideCoolGap = 20f, RideRetry = 5f, RideHoldAfter = 8f, RideHoldMax = 15f, RideFront = 6f, RideRepath = 4f, RideTrack = 20f;
        internal const int RidesMax = 3;
        private static class Rally359End { internal const int Threat = 0, Time = 1, Group = 2, Wound = 3, Other = 4, Done = 5; }

        private static bool RallyRideOn(Mission m)
        {
            var c = Settings.Current;
            if (!_gKey || c == null || !c.RallyRoutedMen || !c.RoutedMenNeverRally || !c.BrokenMenNeverRally) return false;
            var k = RoutHold.Live;
            return k != null && ReferenceEquals(k.Mission, m);
        }

        /// <summary>Straz 335 dostepna (GuardOk) w 80 m - sam licznik, bez przydzialu; z juz przydzielona straza tego dowodcy.</summary>
        private static int GuardCount(Man man, Mission m, int need)
        {
            var a = man.A;
            var t = a.Team;
            int n = 0;
            for (int i = 0; i < Guards.Count; i++) if (ReferenceEquals(Guards[i].Of, man)) n++;
            if (n >= need) return n;
            Vec2 c = a.Position.AsVec2;
            if (!c.IsValid || !MissionTeams.Valid(t)) return n;
            try
            {
                m.GetNearbyAgents(c, GuardReach, _gNear);
                for (int i = 0; i < _gNear.Count && n < need; i++) if (GuardOk(_gNear[i], a, t)) n++;
            }
            finally { _gNear.Clear(); }
            return n;
        }

        private static Vec2 RideSpot(Vec2 gc, Vec2 from)
        {
            Vec2 d = from - gc;
            return d.IsValid && d.LengthSquared > 0.25f ? gc + d.Normalized() * RideFront : gc;
        }

        /// <summary>Start wyjazdu (GuardPass, dowodca bez zagrozenia, przed stanowiskiem 335). true = jedzie.</summary>
        private static bool RallyTry(Man man, Mission m, float now)
        {
            if (man.Role != RoleCmd || !RallyRideOn(m)) return false;
            if (man.Mode != ModeNone && man.Mode != ModePost) return false;
            if (man.RallyRides >= RidesMax || now < man.RallyCool) return false;
            var a = man.A;
            float max = a.HealthLimit;
            if (max > 0f && a.Health / max < WoundedBelow) return false;
            var k = RoutHold.Live;
            var gl = k.RallyTargets(a.Team);
            if (gl == null) return false;
            Vec2 pos = a.Position.AsVec2;
            if (!pos.IsValid) return false;
            float reach2 = Rally359.Reach * Rally359.Reach, bd = Rally359.GroupFar * Rally359.GroupFar;
            int bi = -1;
            for (int i = 0; i < gl.Count; i++)
            {
                float d = gl[i].C.DistanceSquared(pos);
                if (d <= reach2) return false;                                    // grupa juz w zasiegu pasywnym
                if (d < bd && !RideTaken(man, gl[i].C)) { bd = d; bi = i; }      // przeglad 359: jedna grupa = jeden dowodca
            }
            if (bi < 0) return false;
            // przeglad 359: zawsze straz 335 jedzie z nim (szyk konny nie jedzie za pozycja skryptowa wodza - "eskorta konna" zostawialaby go samego)
            if (GuardCount(man, m, GuardsMin) < GuardsMin) { Rally359.RideNoEscort++; man.RallyCool = now + RideRetry; return false; }
            Vec2 gc = gl[bi].C;
            Vec2 p = RideSpot(gc, pos);
            WorldPosition wp;
            int r = Check(a, m, p, out wp);
            if (r == CheckLimit) return false;                                    // budzet zapytan - nastepny pass
            if (r != CheckOk) { man.RallyCool = now + RideRetry; return false; }
            if (man.Mode == ModePost) PostOff(man);                               // pozycja stanowiska nadpisana nizej
            man.Mode = ModeRally;
            man.St = StTravel;
            man.Since = now;
            man.NextEval = now;
            man.Target = p;
            man.Wp = wp;
            man.RallyC = gc;
            man.RallyArrived = -1f;
            man.RallyRides++;
            a.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);   // bez NoAttack (broni sie), bez DoNotRun
            // straz 335 jedzie z nim (GuardsStep co 0.25 s); liczniki wezwan 335 bez wyjazdow 359 (przeglad 359) - osobno w Rally359.RideGuards
            int e0 = _gGuardEps, m0 = _gGuardMen, n0 = _gGuardNone;
            Summon(man, m, now, 0);
            Rally359.RideGuards += _gGuardMen - m0;
            _gGuardEps = e0; _gGuardMen = m0; _gGuardNone = n0;
            Rally359.RideStart++;
            return true;
        }

        /// <summary>Przeglad 359: inny dowodca tej druzyny juz jedzie do tej grupy (srodek w promieniu grupy).</summary>
        private static bool RideTaken(Man man, Vec2 gc)
        {
            float r2 = Rally359.GroupRadius * Rally359.GroupRadius;
            var t = man.A != null ? man.A.Team : null;
            for (int i = 0; i < List.Count; i++)
            {
                var o = List[i];
                if (o == null || ReferenceEquals(o, man) || o.Out || o.Mode != ModeRally || o.A == null || !ReferenceEquals(o.A.Team, t)) continue;
                if (o.RallyC.IsValid && o.RallyC.DistanceSquared(gc) <= r2) return true;
            }
            return false;
        }

        /// <summary>Grupa z ostatniego skanu RoutKeeper w RideTrack od starego srodka.</summary>
        private static bool TrackGroup(RoutKeeper k, Team t, Vec2 old, out Vec2 gc)
        {
            gc = Vec2.Invalid;
            var gl = k.RallyTargets(t);
            if (gl == null || !old.IsValid) return false;
            float bd = RideTrack * RideTrack;
            for (int i = 0; i < gl.Count; i++)
            {
                float d = gl[i].C.DistanceSquared(old);
                if (d <= bd) { bd = d; gc = gl[i].C; }
            }
            return gc.IsValid;
        }

        /// <summary>Krok wyjazdu: full = pass 0.5 s (koniec, grupa, nowy cel), inaczej krok 0.25 s (dotarcie, przywrocenie pozycji).</summary>
        private static void RallyStep(Man man, Mission m, float now, bool full)
        {
            var a = man.A;
            if (a == null || !a.IsActive()) return;
            Vec2 pos = a.Position.AsVec2;
            if (man.RallyArrived < 0f && pos.IsValid && man.RallyC.IsValid && pos.DistanceSquared(man.RallyC) <= Rally359.Reach * Rally359.Reach)
            {
                man.RallyArrived = now;
                man.St = StHold;
                Rally359.RideArrived++;
                var here = a.GetWorldPosition();                                  // stoi przy grupie - zasieg pasywny zawraca ludzi
                if (here.IsValid) { a.SetScriptedPosition(ref here, false, Agent.AIScriptedFrameFlags.None); man.Wp = here; man.Target = pos; }
            }
            if (!full)
            {
                if (man.Wp.IsValid)
                {
                    bool lost = false;
                    try { lost = (a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) == 0; } catch { lost = false; }
                    if (lost) { var o = man.Wp; a.SetScriptedPosition(ref o, false, Agent.AIScriptedFrameFlags.None); }
                }
                return;
            }
            if (man.Role != RoleCmd || !RallyRideOn(m)) { Release(man); RallyOff(man, now, Rally359End.Other); return; }
            var k = RoutHold.Live;
            if (man.RallyArrived < 0f)
            {
                if (now - man.Since > RideMax) { Release(man); RallyOff(man, now, Rally359End.Time); return; }
                Vec2 gc;
                if (!TrackGroup(k, a.Team, man.RallyC, out gc)) { Release(man); RallyOff(man, now, Rally359End.Group); return; }
                if (gc.DistanceSquared(man.RallyC) > RideRepath * RideRepath)
                {
                    man.RallyC = gc;
                    Vec2 p = RideSpot(gc, pos);
                    WorldPosition wp;
                    if (Check(a, m, p, out wp) == CheckOk) { man.Target = p; man.Wp = wp; a.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None); }
                }
                return;
            }
            float held = now - man.RallyArrived;
            if ((held >= RideHoldAfter && k.GatherBy(a) == 0) || held >= RideHoldMax) { Release(man); RallyOff(man, now, Rally359End.Done); }
        }

        /// <summary>Koniec wyjazdu (pozycje zdejmuje wolajacy - Release - poza zagrozeniem: tam nadpisze ja cel wycofania 335).</summary>
        private static void RallyOff(Man man, float now, int why)
        {
            switch (why)
            {
                case Rally359End.Threat: Rally359.RideThreat++; break;
                case Rally359End.Time: Rally359.RideTime++; break;
                case Rally359End.Group: Rally359.RideGroup++; break;
                case Rally359End.Wound: Rally359.RideWound++; break;
                case Rally359End.Done: Rally359.RideDone++; break;
                default: Rally359.RideOther++; break;
            }
            man.Mode = ModeNone;
            man.St = StIdle;
            man.PostKind = 0;
            man.Wp = WorldPosition.Invalid;
            man.Target = Vec2.Invalid;
            man.RallyC = Vec2.Invalid;
            man.RallyArrived = -1f;
            man.RallyCool = now + RideCoolGap;
            man.Hits = 0;
        }
    }
}
