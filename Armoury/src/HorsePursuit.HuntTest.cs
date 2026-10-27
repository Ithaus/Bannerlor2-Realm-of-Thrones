using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 204 - PROBA AUTOTESTU "nekajacy konny strzelec" (klucz HuntHarasserInAutotest; TYLKO LogGate.Mode 1 = bieg autotestu CrashScribe, plan bitwy "Strong"
    /// - AutotestBattleKit.TestPlan, bitwa polowa). W autotescie gracz nie strzela, a szyki druzyny gracza sa pod jego rozkazami - odpowiadac moga tylko bandyci.
    /// 8 s po rozstawieniu: czlowiek AI druzyny gracza, konno, z amunicja (IsRangedCached), nie bohater, nie chorazy, nie smok - cel automatyczny wylaczony,
    /// cel = najblizszy czlowiek najblizszego pieszego szyku wroga, pozycja skryptowa co 0.25 s 10 m od krawedzi tego szyku (Jeff: "10 metrow od nich").
    /// Jezdziec 204 w 15 m od niego (CounterFootGap) - ucieczka do glownej sily swojej druzyny na ShieldsUpHoldSeconds, potem powrot. Koniec: 120 s, drugi
    /// epizod 204 na nim zakonczony, padl, panika / odwrot rannego, koniec bitwy - cel automatyczny, pozycja skryptowa zdjeta.
    /// Brak takiego czlowieka do 30 s po rozstawieniu = linia "TEST: brak nekajacego" (sprawdz_logi: NIE SPRAWDZONE). W druzynie gracza autotestu konnego
    /// strzelca daje dopiero DLL autotestu z kluczem "harasser" (dokladka konnych lucznikow przez AutotestBattle - poza repo).
    /// Linie: "Jazda na strzelca (204) TEST: ..." i "Jazda na strzelca (204) TEST koniec: ...".
    /// 283 (klucz FormationsAnswerHarassers): scenariusz zmienia sie z kazda bitwa "mocna" sesji (licznik sesji, nie misji): A - konny strzelec staje 3 m za szykiem
    /// strzelcow wroga (mediana - Direction x (Depth/2 + 3); bez szyku strzelcow jak 204: 10 m od piechoty) - linie 204 jak dotad + "Nekanie (283) TEST A";
    /// B - pieszy strzelec druzyny gracza 15 m za szykiem piechoty wroga (ucieka jak A przed jezdzcem albo piechota 283 w 15 m); C - pieszy wojownik wrecz (najwyzszy tier)
    /// idzie na najblizszego lucznika wroga (bez lucznikow - na piechote) i walczy (bez ucieczki - widac wzrost oddzialu jeden za jednego). B i C - tylko linie
    /// "Nekanie (283) TEST B/C" (linie 204 TEST zostaja dla A - grupa m204jazda bez zmian). Koniec jak wyzej (C bez limitu epizodow).
    /// </summary>
    internal static partial class HorsePursuit
    {
        private const float TestPickAfter = 8f, TestPickUntil = 30f, TestMaxSec = 120f, TestStandOff = 10f;
        private const int TestMaxEp = 2, TestPhaseLines = 8;
        private const float TestBehindA = 3f, TestBehindB = 15f;   // 283: Jeff 27.10 "3 m za plecami", strzelec pieszy 15 m za szykiem (CounterFootGap)
        private static readonly string[] TestScenName = { "A", "B", "C" };

        private static bool _tOn;
        private static string _tWhy = "-";
        private static int _tState;                 // 0 czeka, 1 nekajacy w polu, 2 koniec
        private static float _tDeploy = -1f, _tStart, _tFleeUntil = -1f, _tContact = -1f;
        private static Agent _tA, _tTgt;
        private static Formation _tF;
        private static bool _tFlee;
        private static int _tShots, _tDet, _tMissPass, _tEp, _tRiders, _tLines, _tFreeCav, _tCavForms;
        private static string _tEnd = "-";
        // 283: scenariusz (licznik bitew mocnych sesji - nie zerowany miedzy misjami), liczniki odpowiedzi na nekajacego testu
        private static int _tSeq, _tScen, _tArch, _tFoot, _tHalt, _tFreeInf, _tInfForms, _tEnemyArch;
        private static bool _tBehind, _tMarch;

        private static void HuntTestBegin(Settings c)
        {
            HuntTestClear();
            _tOn = false;
            if (c == null || !c.HuntHarasserInAutotest) { _tWhy = "klucz NIE"; return; }
            int mode = LogGate.Mode;
            if (mode != 1) { _tWhy = "nie autotest"; return; }
            if (!_field) { _tWhy = "nie bitwa polowa"; return; }
            string plan = AutotestBattleKit.TestPlan();
            if (plan != "Strong") { _tWhy = "plan " + (plan ?? "?"); return; }
            _tOn = true;
            _tWhy = "czynny";
            _tSeq++;
            _tScen = _rKey ? (_tSeq - 1) % 3 : 0;   // 283: A, B, C po kolei (bez klucza 283 - zawsze A, jak 204)
        }

        private static void HuntTestClear()
        {
            _tState = 0;
            _tDeploy = -1f; _tStart = 0f; _tFleeUntil = -1f; _tContact = -1f;
            _tA = null; _tTgt = null; _tF = null; _tFlee = false;
            _tShots = _tDet = _tMissPass = _tEp = _tRiders = _tLines = _tFreeCav = _tCavForms = 0;
            _tScen = 0; _tArch = _tFoot = _tHalt = _tFreeInf = _tInfForms = _tEnemyArch = 0;
            _tBehind = _tMarch = false;
            _tEnd = "-";
        }

        /// <summary>Segment linii bitwy 204.</summary>
        private static string TestSummary()
        {
            if (!_tOn) return "bez nekajacego (" + _tWhy + ")";
            if (_tScen != 0) return "bez nekajacego (scenariusz 283 " + TestScenName[_tScen] + ")";
            if (_tA == null && _tState == 2) return "brak nekajacego (" + _tEnd + ")";
            return "nekajacy strzalow " + _tShots + ", wykryc " + _tDet + ", epizodow " + _tEp + ", jezdzcow " + _tRiders + ", wynik " + _tEnd;
        }

        /// <summary>Segment linii bitwy 283.</summary>
        private static string ReactTestSummary()
        {
            if (!_tOn) return "bez nekajacego (" + _tWhy + ")";
            string sc = "scenariusz " + TestScenName[_tScen];
            if (_tA == null && _tState == 2) return sc + ", brak nekajacego (" + _tEnd + ")";
            return sc + ", wykryc " + _tDet + ", epizodow " + _tEp + ", lucznikow " + _tArch + ", piechoty " + _tFoot + ", jezdzcow " + _tRiders + ", szykow stoi " + _tHalt
                   + ", wynik " + _tEnd;
        }

        /// <summary>Co 0.25 s (pass = co 1 s, po HForms tego passu).</summary>
        private static void HuntTestStep(Mission m, float now, bool pass)
        {
            if (!_tOn || _tState == 2 || m == null) return;
            if (_tDeploy < 0f) _tDeploy = now;
            if (_tState == 0)
            {
                if (!pass || now - _tDeploy < TestPickAfter) return;
                if (TestPick(m, now)) return;
                if (now - _tDeploy >= TestPickUntil)
                {
                    _tState = 2;
                    if (_tScen == 0)
                    {
                        _tEnd = "brak konnego strzelca AI w druzynie gracza";
                        Log.Info("Jazda na strzelca (204) TEST: brak nekajacego - " + _tEnd + " (" + (now - _tDeploy).ToString("0", Inv) + " s po rozstawieniu; DLL autotestu bez klucza harasser?)");
                    }
                    else _tEnd = _tScen == 1 ? "brak pieszego strzelca AI w druzynie gracza" : "brak pieszego wojownika AI w druzynie gracza";
                    if (_rKey) Log.Info("Nekanie (283) TEST " + TestScenName[_tScen] + ": brak nekajacego - " + _tEnd + " (" + (now - _tDeploy).ToString("0", Inv) + " s po rozstawieniu)");
                }
                return;
            }
            var a = _tA;
            if (a == null || !a.IsActive()) { TestEnd(m, "padl", now); return; }
            if (a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a)) { TestEnd(m, "panika/odwrot", now); return; }
            if (m.MissionEnded) { TestEnd(m, "koniec bitwy", now); return; }
            if (now - _tStart >= TestMaxSec) { TestEnd(m, "zwolniony po " + F0(TestMaxSec) + " s", now); return; }
            Threat t;
            bool active = Threats.TryGetValue(a, out t) && t.Active;
            if (_tScen != 2 && _tEp >= TestMaxEp && !active) { TestEnd(m, TestMaxEp + " epizody", now); return; }
            // najblizszy jezdziec 204 (i piechota 283) na nim
            float hd = float.MaxValue;
            Vec2 hp = Vec2.Invalid;
            if (t != null)
            {
                for (int i = 0; i < t.Hs.Count; i++)
                {
                    var h = t.Hs[i];
                    if (h.Role == RoleRanged || h.A == null || !h.A.IsActive()) continue;
                    float d = h.A.Position.AsVec2.Distance(a.Position.AsVec2);
                    if (d < hd) { hd = d; hp = h.A.Position.AsVec2; }
                }
            }
            if (_tScen == 2)
            {
                // C: wojownik wrecz walczy dalej (cel - najblizszy lucznik wroga, po jego upadku nastepny)
                if (pass || _tTgt == null || !_tTgt.IsActive()) TestTarget(m, a);
                if (_tF == null || _tTgt == null) { TestEnd(m, "brak celu", now); return; }
                var wc = _tTgt.GetWorldPosition();
                if (!wc.IsValid || wc.GetNavMesh() == UIntPtr.Zero) return;
                a.SetScriptedPosition(ref wc, false, Agent.AIScriptedFrameFlags.None);
                return;
            }
            if (_tFlee && now >= _tFleeUntil)
            {
                _tFlee = false;
                TestPhase("podjazd", now, hd);
            }
            if (!_tFlee && hd <= CounterFootGap)
            {
                _tFlee = true;
                _tFleeUntil = now + _hold;
                TestPhase("ucieczka", now, hd);
            }
            Vec2 ap = a.Position.AsVec2;
            Vec2 p;
            if (_tFlee)
            {
                Vec2 home;
                if (a.Team != null && MainForce(a.Team, out home)) p = home;
                else if (hp.IsValid && hp.Distance(ap) > 0.1f) p = ap + (ap - hp) * (HuntScreen / hp.Distance(ap));
                else p = ap;
            }
            else
            {
                if (pass || _tF == null || _tF.CountOfUnits <= 0 || _tTgt == null || !_tTgt.IsActive()) TestTarget(m, a);
                if (_tF == null) { TestEnd(m, "brak pieszego szyku wroga", now); return; }
                p = StandPoint(_tF, ap);
            }
            var wp = a.GetWorldPosition();
            if (!wp.IsValid) return;
            var wq = wp;
            wq.SetVec2(p);
            if (wq.GetNavMesh() == UIntPtr.Zero) return;
            a.SetScriptedPosition(ref wq, false, Agent.AIScriptedFrameFlags.None);
        }

        /// <summary>Punkt nekajacego: 283 A / B - za szykiem (mediana - Direction x (Depth/2 + 3 albo 15 m)); 204 - 10 m od krawedzi szyku od strony nekajacego.</summary>
        private static Vec2 StandPoint(Formation f, Vec2 ap)
        {
            var mp = f.CachedMedianPosition;
            Vec2 c = mp.IsValid ? mp.AsVec2 : f.CachedAveragePosition;
            if (_tBehind)
            {
                Vec2 dir = f.Direction;
                float dl = dir.Length;
                if (dl > 0.1f) return c - dir * ((0.5f * f.Depth + (_tScen == 1 ? TestBehindB : TestBehindA)) / dl);
            }
            float r = Math.Min(RadiusCap, 0.5f * Math.Max(f.Width, f.Depth));
            float dc = c.Distance(ap);
            return dc > 0.1f ? c + (ap - c) * ((r + TestStandOff) / dc) : ap;
        }

        private static void TestPhase(string what, float now, float hd)
        {
            if (_tLines >= TestPhaseLines) return;
            _tLines++;
            string tail = " faza " + what + " po " + (now - _tStart).ToString("0.0", Inv) + " s" + (hd < float.MaxValue ? " (jezdziec 204 w " + hd.ToString("0", Inv) + " m)" : "");
            if (_tScen == 0) Log.Info("Jazda na strzelca (204) TEST: nekajacy " + Who(_tA) + tail);
            else Log.Info("Nekanie (283) TEST " + TestScenName[_tScen] + ": nekajacy " + Who(_tA) + tail);
        }

        /// <summary>Nekajacy: czlowiek AI druzyny gracza (A konno z amunicja, B pieszo z amunicja, C pieszo wrecz - najwyzszy tier); cel i szyk wroga.</summary>
        private static bool TestPick(Mission m, float now)
        {
            var pt = m.PlayerTeam;
            int ps = SideOf(pt);
            if (ps < 0) return false;
            int es = 1 - ps;
            Agent best = null;
            float bd = float.MaxValue;
            int bt = -1;
            var ags = m.Agents;
            int n = ags != null ? ags.Count : 0;
            for (int i = 0; i < n; i++)
            {
                Agent a;
                try { a = ags[i]; } catch { continue; }
                if (a == null || !a.IsActive() || !a.IsHuman || a.IsMainAgent || !a.IsAIControlled || a.IsHero) continue;
                if (!ReferenceEquals(a.Team, pt)) continue;
                var mt = a.MountAgent;
                if (mt != null && MarchToContact.IsDragon(mt)) continue;
                if (Banner(a)) continue;
                if (_tScen == 0 && (mt == null || !a.IsRangedCached)) continue;
                if (_tScen == 1 && (mt != null || !a.IsRangedCached)) continue;
                if (_tScen == 2 && (mt != null || a.IsRangedCached || !a.HasMeleeWeaponCached)) continue;
                if (a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a)) continue;
                if (_tScen == 2)
                {
                    int tr = TierOf(a);   // najsilniejszy wojownik (najwyzszy tier), przy rownym - blizszy
                    float dd = NearestFoot(es, a.Position.AsVec2);
                    if (tr > bt || (tr == bt && dd < bd)) { bt = tr; bd = dd; best = a; }
                    continue;
                }
                float d = NearestFoot(es, a.Position.AsVec2);
                if (d == float.MaxValue) d = 1e6f;   // wrog bez pieszego szyku - cel z dowolnego (TestTarget)
                if (d < bd) { bd = d; best = a; }
            }
            if (best == null) return false;
            _tA = best;
            TestTarget(m, best);
            if (_tF == null) { _tA = null; return false; }
            // wolna jazda / piechota / strzelcy wroga na starcie (szyki AI, nie gracza), czy wrog w marszu
            var fl = HForms[es];
            for (int i = 0; i < fl.Count; i++)
            {
                var fi = fl[i];
                if (fi.Player) continue;
                if (fi.Cav) { _tFreeCav += fi.N; _tCavForms++; }
                else if (fi.Rng) _tEnemyArch += fi.N;
                else if (fi.Foot) { _tFreeInf += fi.N; _tInfForms++; }
                if (fi.Ha) _tEnemyArch += fi.N;
                try { if (!fi.Retreat && Moving(fi.F)) _tMarch = true; } catch { }
            }
            _tState = 1;
            _tStart = now;
            best.SetAutomaticTargetSelection(false);
            if (_tTgt != null) best.SetTargetAgent(_tTgt);
            string where = _tBehind ? F0(_tScen == 1 ? TestBehindB : TestBehindA) + " m za szykiem" : F0(TestStandOff) + " m od krawedzi szyku";
            string wtxt = (_tScen == 2 ? "na cel wrecz" : "podjazd na " + where);
            if (_tScen == 0)
                Log.Info("Jazda na strzelca (204) TEST: nekajacy " + Who(best) + " (konno, " + Weapon(best) + ", amunicja TAK) | cel " + Who(_tTgt) + " szyku "
                         + _tF.FormationIndex + " (" + _tF.CountOfUnits + " ludzi), odleglosc " + F0(bd) + " m | podjazd na " + where
                         + " | wolnej jazdy wroga " + _tFreeCav + " (szykow " + _tCavForms + ") | faza podjazd");
            if (_rKey)
                Log.Info("Nekanie (283) TEST " + TestScenName[_tScen] + ": nekajacy " + Who(best) + " (" + (best.MountAgent != null ? "konno" : "pieszo") + ", " + Weapon(best)
                         + ", tier " + TierOf(best) + ") | cel " + Who(_tTgt) + " szyku " + _tF.FormationIndex + " (" + _tF.CountOfUnits + " ludzi) | " + wtxt
                         + " | wolnej piechoty wroga " + _tFreeInf + " (szykow " + _tInfForms + "), wolnej jazdy wroga " + _tFreeCav + " (szykow " + _tCavForms
                         + "), lucznikow wroga " + _tEnemyArch + " | wrog w marszu " + (_tMarch ? "TAK" : "NIE"));
            return true;
        }

        private static float NearestFoot(int es, Vec2 p)
        {
            float bd = float.MaxValue;
            var fl = HForms[es];
            for (int i = 0; i < fl.Count; i++)
            {
                if (!fl[i].Foot) continue;
                float d = Math.Max(0f, p.Distance(fl[i].C) - fl[i].R);
                if (d < bd) bd = d;
            }
            return bd;
        }

        /// <summary>Szyk i cel nekajacego: 204 / B - najblizszy pieszy szyk wroga (B - piechota przed strzelcami), A / C (283) - najblizszy szyk strzelcow wroga
        /// (bez niego jak 204); najblizszy czlowiek tego szyku. _tBehind - punkt za szykiem (283 A ze strzelcami, B).</summary>
        private static void TestTarget(Mission m, Agent a)
        {
            int ps = SideOf(a.Team);
            if (ps < 0) { _tF = null; return; }
            var fl = HForms[1 - ps];
            Vec2 ap = a.Position.AsVec2;
            int best = -1;
            float bd = float.MaxValue;
            // przebiegi: 0 - wedlug scenariusza (strzelcy A/C, piechota B), 1 - dowolny pieszy, 2 - dowolny
            for (int pass = (_rKey ? 0 : 1); pass < 3 && best < 0; pass++)
            {
                for (int i = 0; i < fl.Count; i++)
                {
                    var fi = fl[i];
                    if (fi.F == null || fi.F.CountOfUnits <= 0) continue;
                    if (pass == 0 && (_tScen == 1 ? !(fi.Foot && !fi.Rng) : !fi.Rng)) continue;
                    if (pass == 1 && !fi.Foot) continue;
                    float d = ap.Distance(fi.C);
                    if (d < bd) { bd = d; best = i; }
                }
                if (best >= 0) _tBehind = _rKey && _tScen != 2 && pass == 0;
            }
            _tF = best >= 0 ? fl[best].F : null;
            if (_tF == null) return;
            HUnits.Clear();
            _tF.ApplyActionOnEachUnit(HCollect);
            Agent tb = null;
            float td = float.MaxValue;
            for (int u = 0; u < HUnits.Count; u++)
            {
                var x = HUnits[u];
                if (x == null || !x.IsActive() || !x.IsHuman) continue;
                float d = x.Position.AsVec2.Distance(ap);
                if (d < td) { td = d; tb = x; }
            }
            HUnits.Clear();
            if (tb != null && !ReferenceEquals(tb, _tTgt))
            {
                _tTgt = tb;
                if (_tState == 1) { try { a.SetTargetAgent(tb); } catch { } }
            }
        }

        /// <summary>Koniec proby: nekajacy oddany grze (cel automatyczny, pozycja zdjeta), linia "TEST koniec".</summary>
        private static void TestEnd(Mission m, string why, float now)
        {
            if (!_tOn || _tState != 1) return;
            _tState = 2;
            _tEnd = why;
            var a = _tA;
            if (a != null && a.IsActive())
            {
                try { a.SetAutomaticTargetSelection(true); a.InvalidateTargetAgent(); } catch { }
                try { if ((a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) a.DisableScriptedMovement(); } catch { }
            }
            if (_tScen == 0)
                Log.Info("Jazda na strzelca (204) TEST koniec: strzalow " + _tShots + ", wykryc " + _tDet + " (w tym pudla/przelot " + _tMissPass + "), epizodow " + _tEp
                         + ", jezdzcow " + _tRiders + ", kontakt " + (_tContact >= 0f ? _tContact.ToString("0.0", Inv) + " s" : "-") + ", wynik " + why + " po "
                         + Math.Max(0f, now - _tStart).ToString("0.0", Inv) + " s | wolnej jazdy wroga " + _tFreeCav + " (szykow " + _tCavForms + ")");
            if (_rKey)
                Log.Info("Nekanie (283) TEST koniec: scenariusz " + TestScenName[_tScen] + ", nekajacy " + Who(a) + ", wynik " + why + " po "
                         + Math.Max(0f, now - _tStart).ToString("0.0", Inv) + " s | strzalow " + _tShots + ", wykryc " + _tDet + ", epizodow " + _tEp
                         + ", lucznikow na cel " + _tArch + ", piechoty " + _tFoot + ", jezdzcow " + _tRiders + ", szykow stoi " + _tHalt
                         + " | wolnej piechoty wroga " + _tFreeInf + ", wolnej jazdy wroga " + _tFreeCav + ", lucznikow wroga " + _tEnemyArch
                         + ", wrog w marszu " + (_tMarch ? "TAK" : "NIE"));
        }
    }
}
