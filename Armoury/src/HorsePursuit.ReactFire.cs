using System;
using System.Collections.Generic;
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
        // poprawka 132b (autotest 132b: FF lucznikow 283 5 przy 43 strzalach - w lucznika obok przy nekajacym wrecz w ich szeregu i w wodza bandy 29-42 m od
        // lucznikow obok konnego strzelca): promien widzi tylko linie w chwili sprawdzenia, a strzala, ktora chybi, spada przy celu. Swoi przy celu - raz na pass
        // na zagrozenie (jedno zapytanie gry GetNearbyAgents w NearR wokol celu):
        //  (a) nekajacy wrecz w zwarciu ze swoimi (swoj w RaiseNear 6 m - promien zwarcia 140): lucznicy nie strzelaja w to zwarcie - nowych celow brak,
        //      przypisani zdjeci (krok w tyl zostaje, ale bez strzalu - NoAttack, do rozejscia);
        //  (b) rozrzut przy celu: swoj w polowie linii blizszej celowi albo do NearR za celem, w bok od linii nie dalej niz 2 x promien kapsuly ciala
        //      + MissRadius 140 (4.5 m - "pudlo przy ludziach") x odleglosc / HuntScreen (30 m - pelny rozrzut od 30 m) + wyprzedzenie celu (predkosc celu
        //      x czas lotu: odleglosc / predkosc pocisku broni MissileSpeed), najwyzej MissRadius - linia zajeta (przypisany - cel zdjety, nowy - bez celu).
        //      Jeff 27.10 ("3 m za plecami lucznikow"): lucznik z szeregu strzela z 3 m - sasiedzi stoja przy nim, nie w polowie linii przy celu.
        private const float NearR = ShieldsUp.RaiseNear + ShieldsUp.MissRadius;
        private static readonly List<Vec2> NearPos = new List<Vec2>();
        private static readonly List<int> NearIdx = new List<int>();
        private static Threat _nearT;
        private static int _nearPass = -1;
        private static bool _nearMelee;
        private static float _nearV;
        private static int _rNearMelee, _rNearSpread, _rNearDrop, _rNearHold, _rNearQ;

        private static void NearClear()
        {
            NearPos.Clear(); NearIdx.Clear();
            _nearT = null; _nearPass = -1; _nearMelee = false; _nearV = 0f;
            _rNearMelee = _rNearSpread = _rNearDrop = _rNearHold = _rNearQ = 0;
        }

        /// <summary>Swoi (strona ofiary) w NearR od celu - raz na pass na zagrozenie; _nearMelee - nekajacy wrecz ze swoim w RaiseNear.</summary>
        private static void NearFriends(Mission m, Threat t)
        {
            if (ReferenceEquals(_nearT, t) && _nearPass == _hPassN) return;
            _nearT = t;
            _nearPass = _hPassN;
            NearPos.Clear(); NearIdx.Clear();
            _nearMelee = false;
            _nearV = 0f;
            var s = t.S;
            if (s == null || !s.IsActive()) return;
            Vec2 sp = s.Position.AsVec2;
            try { var mv = (s.MountAgent ?? s).MovementVelocity; _nearV = mv.Length; } catch { _nearV = 0f; }
            _rNearQ++;
            m.GetNearbyAgents(sp, NearR, HBuf);
            bool melee = MeleeKind(t);
            for (int i = 0; i < HBuf.Count; i++)
            {
                var x = HBuf[i];
                if (x == null || !x.IsHuman || !x.IsActive() || ReferenceEquals(x, s)) continue;
                if (SideOf(x.Team) != t.Side) continue;
                Vec2 xp = x.Position.AsVec2;
                NearPos.Add(xp);
                NearIdx.Add(x.Index);
                if (melee && xp.Distance(sp) <= ShieldsUp.RaiseNear) _nearMelee = true;
            }
            HBuf.Clear();
        }

        /// <summary>(b) Rozrzut przy celu: swoj w polowie linii blizszej celowi (albo do NearR za nim) i w bok nie dalej niz dopuszczalny rozrzut.</summary>
        private static bool NearSpread(Agent a, Vec2 from, Threat t)
        {
            if (t == null || NearPos.Count == 0 || t.S == null) return false;
            Vec2 tp = t.S.Position.AsVec2;
            Vec2 dir = tp - from;
            float d = dir.Length;
            if (d < 1f) return false;
            dir = dir * (1f / d);
            float rad = 0.4f;
            try { if (a.Monster != null) rad = a.Monster.BodyCapsuleRadius; } catch { }
            float va = 0f;
            try { var w = a.WieldedWeapon; var u = w.CurrentUsageItem; va = u != null ? u.MissileSpeed : 0f; } catch { va = 0f; }
            float lead = va > 10f ? _nearV * d / va : 0f;
            float allow = 2f * rad + Math.Min(ShieldsUp.MissRadius, ShieldsUp.MissRadius * d / HuntScreen + lead);
            int me = a.Index;
            for (int i = 0; i < NearPos.Count; i++)
            {
                if (NearIdx[i] == me) continue;
                Vec2 o = NearPos[i] - from;
                float u = Vec2.DotProduct(o, dir);
                if (u < 0.5f * d || u > d + NearR) continue;
                float lat = Math.Abs(o.x * dir.y - o.y * dir.x);
                if (lat <= allow) return true;
            }
            return false;
        }

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
                if (NearSpread(a, q, _nearT != null && ReferenceEquals(_nearT.S, s) ? _nearT : null)) continue;   // poprawka 132b: (b) takze z punktu kroku
                int r = Line(m, a, new Vec3(eye.x + off.x, eye.y + off.y, eye.z), s);
                if (r < 0) return false;
                if (r == 1) { at = q; _rStepOk++; return true; }
            }
            return false;
        }

        /// <summary>Lucznik idzie na punkt kroku i tam strzela (pozycja skryptowa bez NoAttack; zdejmuje EndHunter). Poprawka 132b: hold - z NoAttack
        /// (krok w tyl, a nekajacy wrecz w zwarciu ze swoimi - lucznik stoi z boku i nie strzela, do rozejscia).</summary>
        private static void ApplyStep(Hunter h, Vec2 at, bool hold = false)
        {
            var a = h.A;
            var wp = a.GetWorldPosition();
            if (!wp.IsValid) return;
            wp.SetVec2(at);
            if (wp.GetNavMesh() == UIntPtr.Zero) return;
            a.SetScriptedPosition(ref wp, false, hold ? Agent.AIScriptedFrameFlags.NoAttack : Agent.AIScriptedFrameFlags.None);
            h.Step = true;
            h.StepAt = at;
            h.Hold = hold;
            _hSets++;
        }

        /// <summary>R1 (283): lucznicy strony w zasiegu - cel na nekajacego z czysta linia (albo krokiem); w pojedynku strzeleckim polowa blizsza nekajacemu;
        /// nekajacy wrecz w 6 m - lucznik z bronia boczna walczy (gra), bez niej krok w tyl na 12 m. Zwraca liczbe nowych.</summary>
        private static int RangedPassR(Mission m, Threat t, Vec2 sp, float now)
        {
            if (RespNear(t)) { _rRespNear++; return 0; }   // recenzja 283 (6): swoi (R2 / R3) walcza z nim - lucznicy nie strzelaja w to zwarcie
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
            NearFriends(m, t);   // poprawka 132b: swoi przy celu (a) zwarcie, (b) rozrzut - zapytanie tylko przy lucznikach w zasiegu
            bool melee = MeleeKind(t);
            int n = 0;
            for (int j = 0; j < RangedForms.Count; j++)
            {
                if (_hNewRanged >= TrackPerStep) { _hRangedCap++; break; }
                if (_hScan >= _hScanCap) { _hScanCut++; break; }
                if (_rRays >= RaysPerPass) { _rRayCap++; break; }
                var fi = fl[RangedForms[j]];
                float range = RangedRange[j];
                // pojedynek strzelecki: wrog (inny szyk) w zasiegu - obraca sie tylko polowa blizsza nekajacemu (RBM 0.5), reszta trzyma swoj cel;
                // recenzja 283 (1): polowa = polplaszczyzna przez srodek szyku od strony nekajacego (nie kolo wokol niego); nekajacy w szyku albo przy nim
                // (w promieniu szyku) - odpowiadaja wszyscy w zasiegu
                bool duel = fi.Gap <= range;
                Vec2 hv = sp - fi.C;
                bool halfOn = duel && hv.Length > fi.R;
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
                        ApplyStep(hb, back, _nearMelee);   // poprawka 132b: przy zwarciu ze swoimi - krok bez strzalu
                        _rBack++;
                        _hNewRanged++;
                        n++;
                        continue;
                    }
                    if (halfOn && Vec2.DotProduct(ap - fi.C, hv) < 0f) { _rHalf++; continue; }   // dalsza polowa trzyma swoj cel (po walce wrecz / kroku w tyl)
                    if (_nearMelee) { _rNearMelee++; continue; }                                 // poprawka 132b (a): nie w zwarcie ze swoimi
                    if (NearSpread(a, ap, t)) { _rNearSpread++; continue; }                       // poprawka 132b (b): swoj przy celu w rozrzucie
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
            if (RespNear(t))
            {
                // recenzja 283 (6): promien widzi tylko linie do celu - swoi walczacy przy nim (obok, za nim) zbieraliby chybione strzaly; cel zdjety
                for (int i = t.Hs.Count - 1; i >= 0; i--)
                {
                    if (i >= t.Hs.Count) continue;
                    var h = t.Hs[i];
                    if (h.Role != RoleRanged) continue;
                    EndHunter(h, UFriend, now, false, false);
                    _rRespDrop++;
                }
                return;
            }
            NearFriends(m, t);   // poprawka 132b: swoi przy celu (a) zwarcie, (b) rozrzut - stan z tego passu
            for (int i = t.Hs.Count - 1; i >= 0; i--)
            {
                if (i >= t.Hs.Count) continue;
                var h = t.Hs[i];
                if (h.Role != RoleRanged || h.A == null || !h.A.IsActive()) continue;
                // poprawka 132b (a): zwarcie ze swoimi - krok w tyl zostaje bez strzalu (NoAttack), reszta zdjeta; po rozejsciu krok znowu ze strzalem
                if (_nearMelee)
                {
                    if (h.Step && h.StepAt.IsValid) { if (!h.Hold) { ApplyStep(h, h.StepAt, true); _rNearHold++; } continue; }
                    EndHunter(h, UFriend, now, false, false);
                    _rNearDrop++;
                    continue;
                }
                if (h.Hold && h.StepAt.IsValid) ApplyStep(h, h.StepAt, false);
                // poprawka 132b (b): swoj przy celu w rozrzucie - cel zdjety (nastepny pass moze dac go znowu, gdy sie rozejda)
                if (NearSpread(h.A, h.A.Position.AsVec2, t))
                {
                    EndHunter(h, UFriend, now, false, false);
                    _rNearDrop++;
                    continue;
                }
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

        /// <summary>Recenzja 283 (6): czlowiek odpowiedzi tego nekajacego (piechota R2 albo jezdziec R3) w 6 m od niego (RaiseNear 140 - zwarcie). Bez zapytan.</summary>
        private static bool RespNear(Threat t)
        {
            var s = t.S;
            if (s == null) return false;
            Vec2 sp = s.Position.AsVec2;
            for (int i = 0; i < t.Hs.Count; i++)
            {
                var h = t.Hs[i];
                if (h.Role == RoleRanged || h.A == null || !h.A.IsActive()) continue;
                if (h.A.Position.AsVec2.Distance(sp) <= ShieldsUp.RaiseNear) return true;
            }
            return false;
        }
    }
}
