using System;
using System.Threading;
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
    /// Poprawka 132a (autotest 132a: A strzalow 1, wykrycie 67 m od wroga przy wlasnej piechocie; B strzalow 0 i padl; C ani jednego ciosu przez 120 s):
    /// nekajacy zostaje w swoim szyku (A - frey_rider w szyku 0 piechoty "p36 l1 kl3"), a MarchToContact.PostSpeed (cap marszu P1, 1.33 m/s) i CapFor / AiBreath
    /// trzymaly go tempem marszu szyku - konny strzelec szedl z piechota, pieszy strzelec i wojownik szli marszem prosto w szyk wroga (punkt za szykiem = droga przez
    /// jego piechote). Teraz: nekajacy testu bez capu marszu i oddechu (TestRunAnyThread, jak jezdziec 204), droga skrzydlem (przed czolem armii wroga - naroznik
    /// skrzydla 30 m za jej szerokoscia, HuntScreen; potem wzdluz skrzydla na glebokosc celu; potem prosto), na miejscu postoj (pozycja skryptowa nowa dopiero, gdy
    /// punkt odjedzie o 8 m - strzelec stoi i strzela; wczesniej nowa pozycja co 0.25 s), B / C limit 180 s (pieszo dluzsza droga), linie "faza obejscie / skrzydlo
    /// / cel / miejsce / stan" (stan co 30 s: etap, do punktu, od szyku celu, od swoich - samotny, strzalow, predkosc) - dowod drogi w nastepnym autotescie.
    /// Poprawka 132b (autotest 132b: A na miejscu po 70 s, B po 171 s przy 1.0-1.2 m/s, C po 122 s - armie juz w zwarciu, B skonczyl "brak pieszego szyku wroga"
    /// przy zwyciestwie): bez capu marszu nekajacy dalej szedl tempem szyku - gra liczy limit czlowieka w szyku z Formation.CachedMovementSpeed (latka 1
    /// MarchToContact obniza ja do tempa marszu), a PostSpeed tylko nie obnizal dalej. Teraz PostSpeed zdejmuje limit nekajacego testu (-1, czlowiek i kon).
    /// Stan wroga do progow (wrog w marszu, wolna piechota) - z chwili pierwszej szkody od nekajacego (HaltAsk -> TestHarm, linia "faza szkoda"), nie z wyboru
    /// 8 s po rozstawieniu: marsz = szyk ofiary w marszu i pieszy szyk wroga dalej niz 110 m (warunek R4), wolna piechota = szyki piechoty wroga nie w zwarciu
    /// do 110 m od nekajacego (zasieg R2 ze straza strzelcow). B - cel najwiekszy szyk piechoty wroga (nie najblizszy - staly cel drogi); koniec bez szyku
    /// wroga - "wrog rozbity".
    /// </summary>
    internal static partial class HorsePursuit
    {
        private const float TestPickAfter = 8f, TestPickUntil = 30f, TestMaxSec = 120f, TestStandOff = 10f;
        private const int TestMaxEp = 2, TestPhaseLines = 12;   // 283a: 12 (droga obejscia i linie stanu)
        // 283a (poprawka 132a): B / C pieszo - dluzsza droga skrzydlem; obejscie 30 m od skrzydla armii wroga (HuntScreen - oslona skrzydla RBM), punkt drogi
        // osiagniety w 15 m (CounterFootGap); nowa pozycja skryptowa dopiero przy przesunieciu punktu o 2 m; na miejscu w 4 m, postoj trzymany do odjazdu punktu o 8 m
        private const float TestMaxFoot = 180f, TestFlankGap = HuntScreen, TestWayReach = CounterFootGap, TestRepath = 2f, TestHoldR = 4f, TestHoldDrift = 8f,
                            TestStatEvery = 30f;
        private static readonly string[] TestLegName = { "obejscie", "skrzydlo", "cel" };
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
        private static int _tInfArch;   // 291b (recenzja): piesi strzelcy wroga w szykach piechoty (R1 ich nie bierze - tylko szyki Ranged / konnych lucznikow)
        private static bool _tBehind, _tMarch;
        // 283a: nekajacy bez capu marszu (MarchToContact - watki robocze, Volatile), etap drogi, punkt drogi, ostatnia pozycja skryptowa, postoj
        private static Agent _tRun;
        private static int _tLeg, _tSide, _tOff, _tLegLog;
        private static Vec2 _tWay = Vec2.Invalid, _tSet = Vec2.Invalid, _tHoldP = Vec2.Invalid;
        private static bool _tInPlace, _tPlaced;
        private static float _tStatAt;
        private static bool _tHarmed;   // poprawka 132b: stan wroga z chwili pierwszej szkody (TestHarm)
        // 291 (autotest 133a: B 'odwrot rannego po 84.6 s', C 'panika/odwrot po 88.4 s', oba wykryc 0 - ranni / bez ducha w drodze skrzydlem pod strzalami, zanim
        // kogokolwiek drasneli): nekajacy testu zastepuje gracza (gracz nie panikuje i nie wycofuje sie ranny) - do pierwszego wykrycia HP do pelna i morale
        // nie nizej niz przy wyborze; liczniki do linii 'faza oslona'. J204-1: wykrycia ciosem i pudla razem (takze z dala od ludzi) - osobno od wykryc pociskiem
        private static int _tGuardHp, _tGuardMor, _tMelee, _tMissAll;
        // recenzja 291: HP przywracane co 0.25 s przegrywalo z BrokenMen (co 0.5 s -> RoutHold.Withdraw -> BeforeWithdraw 'odwrot rannego') i FieldCraft (bol ->
        // RoutHold.Break) - nekajacy testu do pierwszego wykrycia odrzucany u zrodla (RoutHold.Withdraw / Break, FieldCraft stara droga); ile razy - linia 'faza oslona'
        private static int _tGuardWd;
        private static float _tMor0 = -1f;

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
            _tScen = 0; _tArch = _tFoot = _tHalt = _tFreeInf = _tInfForms = _tEnemyArch = _tInfArch = 0;
            _tBehind = _tMarch = false;
            _tEnd = "-";
            Volatile.Write(ref _tRun, null);
            _tLeg = _tSide = _tOff = _tLegLog = 0;
            _tWay = _tSet = _tHoldP = Vec2.Invalid;
            _tInPlace = _tPlaced = false;
            _tStatAt = 0f;
            _tHarmed = false;
            _tGuardHp = _tGuardMor = _tMelee = _tMissAll = _tGuardWd = 0;   // 291
            _tMor0 = -1f;
        }

        /// <summary>291: nekajacy testu do pierwszego wykrycia - HP do pelna, morale nie nizej niz przy wyborze (jak gracz: bez odwrotu rannego 143 i paniki
        /// przed pierwsza szkoda). Tylko LogGate.Mode 1, klucz HuntHarasserInAutotest, plan Strong (_tOn).</summary>
        private static void TestGuard(Agent a)
        {
            if (_tDet > 0 || a == null) return;
            try
            {
                float lim = a.HealthLimit;
                if (lim > 0f && a.Health < lim) { a.Health = lim; _tGuardHp++; }
                if (_tMor0 >= 0f && a.GetMorale() < _tMor0) { a.SetMorale(_tMor0); _tGuardMor++; }
            }
            catch (Exception e) { HStumble("HorsePursuit.TestOslona", e); }
        }

        /// <summary>Recenzja 291 (glowny watek: RoutHold.Withdraw / Break, FieldCraft): nekajacy testu w polu przed pierwszym wykryciem - odwrot rannego i panika z bolu
        /// odrzucone (wolajacy sprobuje w nastepnym takcie, po pierwszym wykryciu juz normalnie). Poza autotestem _tOn = false - zawsze false.</summary>
        internal static bool TestShielded(Agent a)
        {
            if (!_tOn || _tState != 1 || _tDet > 0 || a == null || !ReferenceEquals(a, _tA)) return false;
            _tGuardWd++;
            return true;
        }

        /// <summary>Poprawka 132b: HaltAsk przy pierwszej szkodzie od nekajacego testu - stan wroga do progow sprawdz_logi (TEST koniec: wrog w marszu, wolnej
        /// piechoty wroga) i linia "faza szkoda".</summary>
        private static void TestHarm(Threat t, float now)
        {
            if (_tHarmed || !_tOn || _tState != 1 || t == null || t.S == null) return;
            _tHarmed = true;
            Vec2 sp = t.S.Position.AsVec2;
            var fl = HForms[t.Side];
            bool march = false;
            float vg = -1f;
            int free = 0, forms = 0;
            for (int i = 0; i < fl.Count; i++)
            {
                var fi = fl[i];
                if (fi.Player || fi.Retreat || fi.F == null) continue;
                if (ReferenceEquals(fi.F, t.VicF))
                {
                    vg = fi.FootGap;
                    try { march = !fi.Ha && Moving(fi.F) && fi.FootGap > HuntClose; } catch { march = false; }
                }
                if (fi.Foot && !fi.Rng && !fi.Cav && !fi.Engaged && Math.Max(0f, sp.Distance(fi.C) - fi.R) <= HuntClose) { free += fi.N; forms++; }
            }
            _tMarch = march;
            _tFreeInf = free;
            _tInfForms = forms;
            TestPhase("szkoda", now, " (szyk ofiary " + (t.VicF != null ? t.VicF.FormationIndex.ToString() : "-") + ", od pieszego szyku wroga "
                      + (vg >= 0f && vg < float.MaxValue ? F0(vg) : "-") + " m, wrog w marszu " + (march ? "TAK" : "NIE") + ", wolnej piechoty wroga w "
                      + F0(HuntClose) + " m " + free + " (szykow " + forms + "), " + TestOwnTxt(t.S, sp) + ", strzalow " + _tShots + ")");
        }

        /// <summary>Poprawka 132b: koniec bez szyku wroga - wrog rozbity albo naprawde brak pieszego szyku.</summary>
        private static string TestNoForm(Agent a, string what)
        {
            int ps = a != null ? SideOf(a.Team) : -1;
            if (ps < 0) return what;
            var fl = HForms[1 - ps];
            for (int i = 0; i < fl.Count; i++) if (fl[i].F != null && fl[i].F.CountOfUnits > 0) return what;
            return "wrog rozbity (bez szyku wroga)";
        }

        /// <summary>283a: MarchToContact.PostSpeed / CapFor (watki robocze) - nekajacy testu w polu (bez capu marszu i oddechu szyku, jak jezdziec 204).</summary>
        internal static bool TestRunAnyThread(Agent a)
        {
            if (a == null) return false;
            var r = Volatile.Read(ref _tRun);
            return r != null && ReferenceEquals(r, a);
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
            TestGuard(a);   // 291: przed sprawdzeniem paniki / odwrotu
            if (a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a)) { TestEnd(m, "panika/odwrot", now); return; }
            if (m.MissionEnded) { TestEnd(m, "koniec bitwy", now); return; }
            float lim = _tScen == 0 ? TestMaxSec : TestMaxFoot;   // 283a: B / C pieszo - dluzsza droga skrzydlem
            if (now - _tStart >= lim) { TestEnd(m, "zwolniony po " + F0(lim) + " s", now); return; }
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
            if (pass && now >= _tStatAt) TestStat(a, now);   // 283a: stan drogi co 30 s
            if (_tScen == 2)
            {
                // C: wojownik wrecz walczy dalej (cel - najblizszy lucznik wroga, po jego upadku nastepny); 283a: droga skrzydlem, bez postoju
                if (pass || _tTgt == null || !_tTgt.IsActive()) TestTarget(m, a);
                if (_tF == null || _tTgt == null) { TestEnd(m, TestNoForm(a, "brak celu"), now); return; }
                TestGo(m, a, _tTgt.Position.AsVec2, now, pass, false);
                return;
            }
            if (_tFlee && now >= _tFleeUntil)
            {
                _tFlee = false;
                _tLeg = 0; _tSide = 0; _tWay = Vec2.Invalid; _tInPlace = false;   // 283a: po ucieczce droga od nowa
                TestPhase("podjazd", now, hd);
            }
            if (!_tFlee && hd <= CounterFootGap)
            {
                _tFlee = true;
                _tFleeUntil = now + _hold;
                TestPhase("ucieczka", now, hd);
            }
            Vec2 ap = a.Position.AsVec2;
            if (_tFlee)
            {
                Vec2 home, p;
                if (a.Team != null && MainForce(a.Team, out home)) p = home;
                else if (hp.IsValid && hp.Distance(ap) > 0.1f) p = ap + (ap - hp) * (HuntScreen / hp.Distance(ap));
                else p = ap;
                TestIssue(a, p);
                return;
            }
            if (pass || _tF == null || _tF.CountOfUnits <= 0 || _tTgt == null || !_tTgt.IsActive()) TestTarget(m, a);
            if (_tF == null) { TestEnd(m, TestNoForm(a, "brak pieszego szyku wroga"), now); return; }
            TestGo(m, a, StandPoint(_tF, ap), now, pass, true);
        }

        /// <summary>283a: droga do celu (skrzydlem, gdy trzeba - TestRoute), na miejscu postoj (hold: A / B - strzelec stoi i strzela, punkt nowy dopiero po
        /// odjezdzie o TestHoldDrift), pozycja skryptowa przez TestIssue.</summary>
        private static void TestGo(Mission m, Agent a, Vec2 goal, float now, bool pass, bool hold)
        {
            if (!goal.IsValid) return;
            Vec2 ap = a.Position.AsVec2;
            if (_tLeg < 2)
            {
                if (pass || !_tWay.IsValid) _tWay = TestRoute(m, a, ap, goal, now);
                if (_tWay.IsValid) { TestIssue(a, _tWay); return; }
            }
            if ((_tLegLog & 4) == 0)
            {
                _tLegLog |= 4;
                TestPhase("cel", now, " (do celu " + F0(ap.Distance(goal)) + " m, " + TestOwnTxt(a, ap) + ")");
            }
            if (hold)
            {
                if (_tInPlace && goal.Distance(_tHoldP) > TestHoldDrift) _tInPlace = false;
                if (!_tInPlace && ap.Distance(goal) <= TestHoldR)
                {
                    _tInPlace = true;
                    _tHoldP = goal;
                    if (!_tPlaced)
                    {
                        _tPlaced = true;
                        TestPhase("miejsce", now, " (" + TestOwnTxt(a, ap) + ", strzalow " + _tShots + ")");
                    }
                }
                if (_tInPlace) goal = _tHoldP;
            }
            TestIssue(a, goal);
        }

        /// <summary>283a: droga skrzydlem w ukladzie szyku celu (srodek - mediana, os - Formation.Direction, czolo armii wroga = najdalej wysuniety pieszy szyk
        /// strony, szerokosc - wszystkie piesze szyki). Etap 0: nekajacy przed czolem i w szerokosci armii - naroznik skrzydla od jego strony (TestFlankGap za
        /// szerokoscia, pol odstepu przed czolem); etap 1: poza szerokoscia - wzdluz skrzydla na glebokosc celu; etap 2: prosto. Punkt poza polem albo bez siatki
        /// - druga strona, potem prosto. Etapy tylko naprzod (bez wahania). Vec2.Invalid = prosto do celu.</summary>
        private static Vec2 TestRoute(Mission m, Agent a, Vec2 ap, Vec2 goal, float now)
        {
            if (!_rKey || _tF == null || _tF.CountOfUnits <= 0) { _tLeg = 2; return Vec2.Invalid; }
            Vec2 dir;
            try { dir = _tF.Direction; } catch { dir = Vec2.Invalid; }
            int es = SideOf(_tF.Team);
            if (es < 0 || !dir.IsValid || dir.LengthSquared < 1E-04f) { _tLeg = 2; return Vec2.Invalid; }
            dir = dir.Normalized();
            var right = new Vec2(dir.y, -dir.x);
            Vec2 c = FootHome(_tF);
            float lo = 0f, hi = 0f, front = 0f;
            var fl = HForms[es];
            for (int i = 0; i < fl.Count; i++)
            {
                var fi = fl[i];
                if (!fi.Foot || fi.F == null || fi.F.CountOfUnits <= 0) continue;
                Vec2 o = fi.C - c;
                float l = Vec2.DotProduct(o, right), f = Vec2.DotProduct(o, dir);
                if (l - fi.R < lo) lo = l - fi.R;
                if (l + fi.R > hi) hi = l + fi.R;
                if (f + fi.R > front) front = f + fi.R;
            }
            Vec2 oa = ap - c;
            float al = Vec2.DotProduct(oa, right), af = Vec2.DotProduct(oa, dir), gf = Vec2.DotProduct(goal - c, dir);
            float gap = TestFlankGap;
            if (_tSide == 0) _tSide = al >= 0.5f * (lo + hi) ? 1 : -1;
            if (_tLeg == 0)
            {
                // nie przed czolem armii albo cel przed jej czolem - prosto (droga nie idzie przez szyki)
                if (af <= front || gf >= front) { _tLeg = 2; return Vec2.Invalid; }
                float L = _tSide > 0 ? hi + gap : lo - gap;
                if (_tSide * al < _tSide * L - 0.5f * gap)
                {
                    Vec2 w = c + right * L + dir * (front + 0.5f * gap);
                    if (!TestOnField(m, a, w))
                    {
                        _tSide = -_tSide;
                        L = _tSide > 0 ? hi + gap : lo - gap;
                        w = c + right * L + dir * (front + 0.5f * gap);
                        if (!TestOnField(m, a, w)) { _tOff++; _tLeg = 2; return Vec2.Invalid; }
                    }
                    if (ap.Distance(w) > TestWayReach)
                    {
                        if ((_tLegLog & 1) == 0)
                        {
                            _tLegLog |= 1;
                            TestPhase("obejscie", now, " (skrzydlo " + (_tSide > 0 ? "prawe" : "lewe") + ", " + F0(Math.Abs(L)) + " m od srodka szyku celu, czolo armii "
                                      + F0(af - front) + " m przed nekajacym, armia szeroka " + F0(hi - lo) + " m)");
                        }
                        return w;
                    }
                }
                _tLeg = 1;
            }
            if (_tLeg == 1)
            {
                float L = _tSide > 0 ? hi + gap : lo - gap;
                if (af <= gf + 0.5f * gap || _tSide * al < _tSide * L - gap) { _tLeg = 2; return Vec2.Invalid; }   // juz na glebokosci celu albo w szerokosci armii
                Vec2 w = c + right * L + dir * gf;
                if (ap.Distance(w) <= TestWayReach) { _tLeg = 2; return Vec2.Invalid; }
                if (!TestOnField(m, a, w)) { _tOff++; _tLeg = 2; return Vec2.Invalid; }
                if ((_tLegLog & 2) == 0)
                {
                    _tLegLog |= 2;
                    TestPhase("skrzydlo", now, " (do glebokosci celu " + F0(af - gf) + " m, " + TestOwnTxt(a, ap) + ")");
                }
                return w;
            }
            return Vec2.Invalid;
        }

        /// <summary>283a: punkt w granicach pola i na siatce nawigacji.</summary>
        private static bool TestOnField(Mission m, Agent a, Vec2 p)
        {
            try
            {
                if (!m.IsPositionInsideBoundaries(p)) return false;
                var wq = a.GetWorldPosition();
                if (!wq.IsValid) return false;
                wq.SetVec2(p);
                return wq.GetNavMesh() != UIntPtr.Zero;
            }
            catch { return false; }
        }

        /// <summary>283a: pozycja skryptowa nekajacego - nowa tylko, gdy punkt przesunal sie o TestRepath albo gra zdjela GoToPosition z dala od punktu
        /// (wczesniej co 0.25 s: strzelec bez przerwy "w drodze").</summary>
        private static void TestIssue(Agent a, Vec2 p)
        {
            if (!p.IsValid) return;
            bool lost;
            try { lost = (a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) == 0; } catch { lost = true; }
            if (_tSet.IsValid && _tSet.Distance(p) < TestRepath && !(lost && a.Position.AsVec2.Distance(p) > TestHoldR)) return;
            var wq = a.GetWorldPosition();
            if (!wq.IsValid) return;
            wq.SetVec2(p);
            if (wq.GetNavMesh() == UIntPtr.Zero) { _tOff++; return; }
            a.SetScriptedPosition(ref wq, false, Agent.AIScriptedFrameFlags.None);
            _tSet = p;
        }

        /// <summary>283a: odleglosc od krawedzi najblizszego nietknietego szyku strony s (IntactPos / IntactR - ta sama miara co IntactNear).</summary>
        private static float OwnIntactGap(int s, Vec2 p)
        {
            float bd = float.MaxValue;
            if (s < 0) return bd;
            var ps = IntactPos[s];
            var rs = IntactR[s];
            for (int i = 0; i < ps.Count; i++)
            {
                float d = p.Distance(ps[i]) - rs[i];
                if (d < bd) bd = d;
            }
            return bd;
        }

        private static string TestOwnTxt(Agent a, Vec2 ap)
        {
            float own = OwnIntactGap(SideOf(a.Team), ap);
            return "od swoich " + (own < float.MaxValue ? F0(own) : "-") + " m, samotny " + (own >= IntactRadius ? "TAK" : "NIE");
        }

        /// <summary>283a: linia stanu co TestStatEvery (dowod drogi: etap, do punktu, od szyku celu, od swoich, strzalow, predkosc).</summary>
        private static void TestStat(Agent a, float now)
        {
            _tStatAt = now + TestStatEvery;
            Vec2 ap = a.Position.AsVec2;
            float df = -1f;
            if (_tF != null && _tF.CountOfUnits > 0)
                df = Math.Max(0f, ap.Distance(FootHome(_tF)) - Math.Min(RadiusCap, 0.5f * Math.Max(_tF.Width, _tF.Depth)));
            float v = 0f;
            try { var mv = (a.MountAgent ?? a).MovementVelocity; v = mv.Length; } catch { v = 0f; }
            TestPhase("stan", now, " (etap " + TestLegName[Math.Min(2, Math.Max(0, _tLeg))] + (_tInPlace ? ", na miejscu" : "") + ", do punktu "
                      + (_tSet.IsValid ? F0(ap.Distance(_tSet)) : "-") + " m, od szyku celu " + (df >= 0f ? F0(df) : "-") + " m, " + TestOwnTxt(a, ap) + ", strzalow "
                      + _tShots + ", predkosc " + v.ToString("0.0", Inv) + " m/s, punkt poza polem " + _tOff
                      + (a.MountAgent == null ? ", krok oddechu " + (AiBreath.CapOf(a) > 0f ? "TAK" : "NIE") : "") + ")");   // 291b: cap AiBreath pieszego nekajacego
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
            TestPhase(what, now, hd < float.MaxValue ? " (jezdziec 204 w " + hd.ToString("0", Inv) + " m)" : "");
        }

        /// <summary>Linia fazy (jedno slowo - wyrazenia tphase sprawdz_logi), detail - reszta w nawiasie.</summary>
        private static void TestPhase(string what, float now, string detail)
        {
            if (_tLines >= TestPhaseLines) return;
            _tLines++;
            string tail = " faza " + what + " po " + (now - _tStart).ToString("0.0", Inv) + " s" + detail;
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
                else if (fi.Foot) { _tFreeInf += fi.N; _tInfForms++; _tInfArch += FootShooters(fi.F); }
                if (fi.Ha) _tEnemyArch += fi.N;
                try { if (!fi.Retreat && Moving(fi.F)) _tMarch = true; } catch { }
            }
            _tState = 1;
            _tStart = now;
            _tStatAt = now + TestStatEvery;
            Volatile.Write(ref _tRun, best);   // 283a: bez capu marszu / oddechu szyku (MarchToContact)
            try { _tMor0 = best.GetMorale(); } catch { _tMor0 = -1f; }   // 291: morale z chwili wyboru (TestGuard)
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
                         + "), lucznikow wroga " + _tEnemyArch + InfArchTxt() + " | wrog w marszu " + (_tMarch ? "TAK" : "NIE"));
            return true;
        }

        /// <summary>291b (recenzja): piesi ludzie z amunicja (IsRangedCached) w szyku - lucznicy band bywaja w szyku piechoty (autotest 296a: 8 skag_bowman
        /// w szyku Infantry, 'lucznikow wroga 0').</summary>
        private static int FootShooters(Formation f)
        {
            int n = 0;
            HUnits.Clear();
            f.ApplyActionOnEachUnit(HCollect);
            for (int u = 0; u < HUnits.Count; u++)
            {
                var x = HUnits[u];
                if (x != null && x.IsActive() && x.IsHuman && x.MountAgent == null && x.IsRangedCached) n++;
            }
            HUnits.Clear();
            return n;
        }

        private static string InfArchTxt() { return _tInfArch > 0 ? " (w szyku piechoty " + _tInfArch + ")" : ""; }

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
            // 291b (autotest 296a C: 'lucznikow wroga 0' - 8 lucznikow bandy (skag_bowman) w szyku piechoty 38 ludzi, szyku Ranged brak; wojownik szedl na
            // najblizszego piechura i nie doszedl do lucznikow): C - najblizszy pieszy strzelec wroga (IsRangedCached) z dowolnego szyku; bez niego jak dotad
            if (_rKey && _tScen == 2 && TestArcherTarget(a, fl, ap)) return;
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
                    // poprawka 132b: B - najwiekszy szyk piechoty wroga (potem najwiekszy pieszy), staly cel drogi skrzydlem; reszta - najblizszy
                    float d = _tScen == 1 && pass < 2 ? -fi.N : ap.Distance(fi.C);
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

        /// <summary>291b: scenariusz C - cel = najblizszy pieszy czlowiek wroga z amunicja (IsRangedCached) w dowolnym szyku strony (lucznicy band bywaja
        /// w szyku piechoty); szyk celu = jego szyk. false = brak takiego strzelca (wolajacy wybiera jak dotad).</summary>
        private static bool TestArcherTarget(Agent a, System.Collections.Generic.List<FI> fl, Vec2 ap)
        {
            Agent tb = null;
            Formation tf = null;
            float td = float.MaxValue;
            for (int i = 0; i < fl.Count; i++)
            {
                var f = fl[i].F;
                if (f == null || f.CountOfUnits <= 0) continue;
                HUnits.Clear();
                f.ApplyActionOnEachUnit(HCollect);
                for (int u = 0; u < HUnits.Count; u++)
                {
                    var x = HUnits[u];
                    if (x == null || !x.IsActive() || !x.IsHuman || x.MountAgent != null || !x.IsRangedCached) continue;
                    float d = x.Position.AsVec2.Distance(ap);
                    if (d < td) { td = d; tb = x; tf = f; }
                }
                HUnits.Clear();
            }
            if (tb == null) return false;
            _tF = tf;
            _tBehind = false;
            if (!ReferenceEquals(tb, _tTgt))
            {
                _tTgt = tb;
                if (_tState == 1) { try { a.SetTargetAgent(tb); } catch { } }
            }
            return true;
        }

        /// <summary>Koniec proby: nekajacy oddany grze (cel automatyczny, pozycja zdjeta), linia "TEST koniec".</summary>
        private static void TestEnd(Mission m, string why, float now)
        {
            if (!_tOn || _tState != 1) return;
            _tState = 2;
            _tEnd = why;
            Volatile.Write(ref _tRun, null);   // 283a: znowu pod capem szyku
            // 291: linia oslony (poza limitem TestPhaseLines - zawsze, przed koncem): ile razy HP / morale przywrocone do pierwszego wykrycia
            string og = " faza oslona po " + Math.Max(0f, now - _tStart).ToString("0.0", Inv) + " s (do pierwszego wykrycia: HP do pelna " + _tGuardHp + " razy, morale "
                        + _tGuardMor + " razy, odwrot/panika wstrzymane " + _tGuardWd + " razy, wykryc " + _tDet + ")";
            if (_tScen == 0) Log.Info("Jazda na strzelca (204) TEST: nekajacy " + Who(_tA) + og);
            else Log.Info("Nekanie (283) TEST " + TestScenName[_tScen] + ": nekajacy " + Who(_tA) + og);
            var a = _tA;
            if (a != null && a.IsActive())
            {
                try { a.SetAutomaticTargetSelection(true); a.InvalidateTargetAgent(); } catch { }
                try { if ((a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) a.DisableScriptedMovement(); } catch { }
            }
            if (_tScen == 0)
                Log.Info("Jazda na strzelca (204) TEST koniec: strzalow " + _tShots + ", wykryc " + _tDet + " (w tym pudla/przelot " + _tMissPass + ", wrecz " + _tMelee
                         + ", pudel razem " + _tMissAll + "), epizodow " + _tEp
                         + ", jezdzcow " + _tRiders + ", kontakt " + (_tContact >= 0f ? _tContact.ToString("0.0", Inv) + " s" : "-") + ", wynik " + why + " po "
                         + Math.Max(0f, now - _tStart).ToString("0.0", Inv) + " s | wolnej jazdy wroga " + _tFreeCav + " (szykow " + _tCavForms + ")");
            if (_rKey)
                Log.Info("Nekanie (283) TEST koniec: scenariusz " + TestScenName[_tScen] + ", nekajacy " + Who(a) + ", wynik " + why + " po "
                         + Math.Max(0f, now - _tStart).ToString("0.0", Inv) + " s | strzalow " + _tShots + ", wykryc " + _tDet + ", epizodow " + _tEp
                         + ", lucznikow na cel " + _tArch + ", piechoty " + _tFoot + ", jezdzcow " + _tRiders + ", szykow stoi " + _tHalt
                         + " | wolnej piechoty wroga " + _tFreeInf + ", wolnej jazdy wroga " + _tFreeCav + ", lucznikow wroga " + _tEnemyArch + InfArchTxt()
                         + ", wrog w marszu " + (_tMarch ? "TAK" : "NIE"));
        }
    }
}
