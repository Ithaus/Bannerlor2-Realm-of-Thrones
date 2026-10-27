using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 283 R1 / R1b - lucznicy strony strzelaja w nekajacego tylko z czysta linia (zamiast kuli 8 m wokol strzelca z 204 FriendsBlock, ktora w bitwie Jeffa 27.10
    /// zdjela cel 19 razy, bo jechal 3 m za ich szeregiem). Linia: jeden promien gry Mission.RayCastForClosestAgent (ten sam, ktorym gra sprawdza FF machin -
    /// RangedSiegeWeapon.CanShootPointBallistic) od oka lucznika do piersi celu, bez samego lucznika; grubosc = promien kapsuly ciala (Monster.BodyCapsuleRadius)
    /// + odleglosc x AiShooterError (rozrzut celowania AI z modelu gry). Pierwszy trafiony czlowiek swojej strony blizej niz cel = linia zajeta. Zajeta - krok:
    /// punkty 1 i 2 odstepy szyku w bok (Formation.Interval + UnitDiameter - odstep gry) i 2 odstepy do tylu (od nekajacego); pierwszy na siatce z czysta linia =
    /// pozycja skryptowa (lucznik tam stoi i strzela, wraca do szyku na koncu). Przypisany lucznik co pass wedlug stanu silnika (GetLastTargetVisibilityState -
    /// FriendInWay: najpierw krok, drugi raz z rzedu - cel zdjety). Promieni najwyzej 64 na pass (QueriesPerPass 140). Prosty promien przy luku nad glowami
    /// (ponad ok. 60 m) jest ostrozny - oznacza zajeta, nigdy odwrotnie.
    /// </summary>
    internal static partial class HorsePursuit
    {
        /// <summary>1 = czysta, 0 = swoj na linii, -1 = bez promienia (limit passu).</summary>
        private static int Line(Mission m, Agent a, Vec3 eye, Agent s)
        {
            if (_rRays >= RaysPerPass) { _rRayCap++; return -1; }
            _rRays++;
            _rRayN++;
            Vec3 tgt = s.GetChestGlobalPosition();
            float d = eye.Distance(tgt);
            float err = 0f, rad = 0f;
            try { err = a.AgentDrivenProperties.AiShooterError; } catch { err = 0f; }
            try { rad = a.Monster != null ? a.Monster.BodyCapsuleRadius : 0f; } catch { rad = 0f; }
            float cd;
            Agent hit = m.RayCastForClosestAgent(eye, tgt, a.Index, rad + d * Math.Max(0f, err), out cd);
            if (hit == null || ReferenceEquals(hit, s) || ReferenceEquals(hit, s.MountAgent) || ReferenceEquals(hit, a.MountAgent)) { _rRayClear++; return 1; }
            Agent hh = hit.IsMount ? hit.RiderAgent : hit;
            if (hh == null || ReferenceEquals(hh, s) || cd >= d - 0.5f) { _rRayClear++; return 1; }
            var at = a.Team;
            var ht = hh.Team;
            if (at != null && ht != null && at.Side == ht.Side) { _rRayFriend++; return 0; }
            _rRayClear++;   // wrog na linii - nie FF
            return 1;
        }

        /// <summary>Krok lucznika do czystej linii (1 i 2 odstepy w bok, 2 odstepy w tyl). false = brak punktu albo limit promieni.</summary>
        private static bool TryStep(Mission m, Agent a, Agent s, Formation f, out Vec2 at)
        {
            at = Vec2.Invalid;
            _rStepTry++;
            Vec2 p = a.Position.AsVec2, sp = s.Position.AsVec2;
            Vec2 u = sp - p;
            float l = u.Length;
            if (l < 0.1f) return false;
            u = u * (1f / l);
            var nrm = new Vec2(-u.y, u.x);
            float gap = 0f;
            try { gap = f.Interval + f.UnitDiameter; } catch { gap = 0f; }
            if (gap <= 0.1f) return false;
            Vec3 eye = a.GetEyeGlobalPosition();
            for (int k = 0; k < 5; k++)
            {
                Vec2 off = k == 0 ? nrm * gap : k == 1 ? nrm * -gap : k == 2 ? nrm * (2f * gap) : k == 3 ? nrm * (-2f * gap) : u * (-2f * gap);
                Vec2 q = p + off;
                var wp = a.GetWorldPosition();
                if (!wp.IsValid) return false;
                wp.SetVec2(q);
                if (wp.GetNavMesh() == UIntPtr.Zero) continue;
                int r = Line(m, a, new Vec3(eye.x + off.x, eye.y + off.y, eye.z), s);
                if (r < 0) return false;
                if (r == 1) { at = q; _rStepOk++; return true; }
            }
            return false;
        }

        /// <summary>Lucznik idzie na punkt kroku i tam strzela (pozycja skryptowa bez NoAttack; zdejmuje EndHunter).</summary>
        private static void ApplyStep(Hunter h, Vec2 at)
        {
            var a = h.A;
            var wp = a.GetWorldPosition();
            if (!wp.IsValid) return;
            wp.SetVec2(at);
            if (wp.GetNavMesh() == UIntPtr.Zero) return;
            a.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);
            h.Step = true;
            _hSets++;
        }

        /// <summary>R1 (283): lucznicy strony w zasiegu - cel na nekajacego z czysta linia (albo krokiem); w pojedynku strzeleckim polowa blizsza nekajacemu;
        /// nekajacy wrecz w 6 m - lucznik z bronia boczna walczy (gra), bez niej krok w tyl na 12 m. Zwraca liczbe nowych.</summary>
        private static int RangedPassR(Mission m, Threat t, Vec2 sp, float now)
        {
            var fl = HForms[t.Side];
            RangedForms.Clear();
            RangedRange.Clear();
            for (int i = 0; i < fl.Count; i++)
            {
                var fi = fl[i];
                if (!(fi.Rng || fi.Ha)) continue;
                if (fi.Player) continue;   // szyk gracza - nigdy (jak RangedPass 204)
                if (fi.Retreat) continue;
                var u0 = fi.F.GetFirstUnit();
                float range = 0f;
                if (u0 != null && u0.IsActive() && u0.IsRangedCached) range = ShooterRange(u0);
                if (range <= 1f || Math.Max(0f, sp.Distance(fi.C) - fi.R) > range) continue;
                RangedForms.Add(i);
                RangedRange.Add(range);
            }
            if (RangedForms.Count == 0) return 0;
            bool melee = MeleeKind(t);
            int n = 0;
            for (int j = 0; j < RangedForms.Count; j++)
            {
                if (_hNewRanged >= TrackPerStep) { _hRangedCap++; break; }
                if (_hScan >= _hScanCap) { _hScanCut++; break; }
                if (_rRays >= RaysPerPass) { _rRayCap++; break; }
                var fi = fl[RangedForms[j]];
                float range = RangedRange[j];
                // pojedynek strzelecki: wrog (inny szyk) w zasiegu - obraca sie tylko polowa blizsza nekajacemu (RBM 0.5), reszta trzyma swoj cel
                bool duel = fi.Gap <= range;
                float half = duel ? sp.Distance(fi.C) : float.MaxValue;
                HUnits.Clear();
                fi.F.ApplyActionOnEachUnit(HCollect);
                int nu = HUnits.Count, off = nu > _hScanCap - _hScan ? _hScanOff % nu : 0;
                for (int k = 0; k < nu && _hNewRanged < TrackPerStep; k++)
                {
                    int u = off + k;
                    if (u >= nu) u -= nu;
                    var a = HUnits[u];
                    if (a == null || !a.IsActive() || !a.IsHuman || Hunters.ContainsKey(a) || !a.IsRangedCached) continue;
                    if (_hScan >= _hScanCap) { _hScanCut++; _hScanOff += k; break; }
                    _hScan++;
                    if (!a.IsAIControlled || a.IsMainAgent || a.IsHero) continue;
                    if (Banner(a) || Undead.Character(a.Character)) continue;
                    var mt = a.MountAgent;
                    if (mt != null && MarchToContact.IsDragon(mt)) continue;
                    if (a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a) || RidingAnyThread(a)) continue;
                    if (!a.CanBeAssignedForScriptedMovement()) continue;
                    float mu;
                    if (MeleeUntil.TryGetValue(a, out mu) && now < mu) continue;
                    Vec2 ap = a.Position.AsVec2;
                    float d = ap.Distance(sp);
                    if (d > range) continue;
                    if (d > half) { _rHalf++; continue; }
                    if (melee && d <= ShieldsUp.RaiseNear)
                    {
                        if (a.HasMeleeWeaponCached || mt != null) { _rSide++; continue; }   // bron boczna (albo konny) - walczy, gra prowadzi
                        // bez broni bocznej: krok w tyl na 2 x RaiseNear i strzal
                        Vec2 back = d > 0.1f ? ap + (ap - sp) * ((2f * ShieldsUp.RaiseNear - d) / d) : ap;
                        var wb = a.GetWorldPosition();
                        if (!wb.IsValid) continue;
                        wb.SetVec2(back);
                        if (wb.GetNavMesh() == UIntPtr.Zero) continue;
                        var hb = StartHunter(t, a, fi, RoleRanged, range, now);
                        ApplyStep(hb, back);
                        _rBack++;
                        _hNewRanged++;
                        n++;
                        continue;
                    }
                    int r = Line(m, a, a.GetEyeGlobalPosition(), t.S);
                    if (r < 0) break;
                    if (r == 1)
                    {
                        StartHunter(t, a, fi, RoleRanged, range, now);
                        _hNewRanged++;
                        n++;
                        continue;
                    }
                    _hFriendBlock++;   // 204 "cel zdjety - swoi przy strzelcu" = teraz: linia zajeta (bez kroku - nie strzela)
                    Vec2 at;
                    if (mt == null && TryStep(m, a, t.S, fi.F, out at))
                    {
                        var hs = StartHunter(t, a, fi, RoleRanged, range, now);
                        ApplyStep(hs, at);
                        _hNewRanged++;
                        n++;
                    }
                    if (_rRays >= RaysPerPass) break;
                }
                HUnits.Clear();
            }
            return n;
        }

        /// <summary>Co pass (283, zamiast FriendsBlock): przypisani lucznicy wedlug stanu silnika - FriendInWay: krok (raz), drugi raz z rzedu - cel zdjety.</summary>
        private static void RangedRecheck(Mission m, Threat t, float now)
        {
            for (int i = t.Hs.Count - 1; i >= 0; i--)
            {
                if (i >= t.Hs.Count) continue;
                var h = t.Hs[i];
                if (h.Role != RoleRanged || h.A == null || !h.A.IsActive()) continue;
                AITargetVisibilityState vs;
                try { vs = h.A.GetLastTargetVisibilityState(); } catch { continue; }
                if (vs != AITargetVisibilityState.FriendInWay) { h.Blk = 0; continue; }
                _rEngine++;
                h.Blk++;
                Vec2 at;
                if (!h.Step && h.A.MountAgent == null && t.S != null && TryStep(m, h.A, t.S, h.F, out at)) { ApplyStep(h, at); h.Blk = 0; continue; }
                if (h.Blk >= 2) EndHunter(h, UFriend, now, false, false);
            }
        }
    }
}
