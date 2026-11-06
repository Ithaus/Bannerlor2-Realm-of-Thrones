using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 333t - PROBA AUTOTESTU "przyneta jak u Jeffa" (scenariusz A nekajacego testu, gdy klucz RidersBreakOffNearShootersArmy; reszta jak HorsePursuit.HuntTest.cs -
    /// TYLKO LogGate.Mode 1, klucz HuntHarasserInAutotest, plan bitwy "Strong"). Autotest 333 runda 1: frey_rider#329 jechal 50 s droga skrzydlem na 3 m za lucznikow
    /// wroga, w 30 s byl 126 m od celu (poza zasiegiem luku) i padl - 0 strzalow, wiec poscig z glowa (A) nie byl sprawdzony.
    /// Teraz nekajacy robi to, co Jeff 06.11: "strzelam do kawalerii, oni jada za mna, wyciagam ich pod swoje wojsko ... jak widza moje wojsko zaczynaja sie odwracac
    /// ... ja jade za nimi i strzelam w plecy, oni sie znowu wracaja":
    ///  - PODJAZD: cel = najblizszy szyk konnicy wroga (AI; bez niego - pieszy szyk jak 204), punkt 80 m od jego krawedzi na linii do glownej sily druzyny gracza (ucieczka
    ///    prosto do swoich), odsuniety od pieszych szykow wroga na 40 m; na miejscu (8 m) - STRZAL;
    ///  - STRZAL: stoi i strzela (cel = najblizszy jezdziec wroga, luk w rece - dobycie, gdy w rece co innego; szyk nekajacego FireAtWill); po 25 s bez reakcji podjezdza
    ///    na 65 m;
    ///  - UCIECZKA: jezdziec wroga zbliza sie do niego >= 3 m/s w 90 m albo jest w 35 m - jedzie do glownej sily swojej druzyny (i strzela, jak konny lucznik); spokoj
    ///    (nikt nie zbliza sie, nikt w 35 m) przez 2 s - POGON, a gdy jezdzcow wroga nie ma w 170 m - nowy cykl od PODJAZDU;
    ///  - POGON: jedzie 55 m za najblizszym jezdzcem wroga (nie blizej niz 40 m od pieszych szykow wroga) i strzela w plecy; gdy jezdzcy znowu ruszaja na niego
    ///    (jak wyzej) - NAWROT KONNICY (liczony: ilu jezdzcow naraz; wiecej niz 5 w 120 s po poprzedniej ucieczce = "cala jazda znowu", czyli ping-pong Jeffa)
    ///    i znowu UCIECZKA.
    /// Koniec jak 204 (padl, koniec bitwy, 180 s); morale i odwrot rannego oslaniane przez caly test (gracz nie panikuje), HP - do pierwszego wykrycia (jak 291).
    /// Strzaly liczone w OnFire (HorsePursuit.Hunt.cs) wedlug fazy. Linie: "Jazda na strzelca (204) TEST: ... faza strzal / ucieczka / pogon / nawrot" i
    /// "AI bitwy (333) TEST przyneta koniec: ..." (sprawdz_logi --grupa ai333: A333-7 strzaly, A333-8 nawroty calej jazdy).
    ///
    /// 333t - ARMIA GRACZA STOI (okno pomiaru wahadla B i przyneta jak u Jeffa - jego wojsko stoi): autotest CrashScribe daje od razu SZARZE wszystkim szykom gracza,
    /// wiec runda 1 miala tylko 16.5 szyko-minut przed kontaktem na 8 bitew. W bitwach "Strong" i "Weak" (odwrot bez zmian - ciosy wrecz i odwrot jak dotad) szyki
    /// druzyny gracza dostaja Stop co pass (1 s) przez 150 s czasu misji albo do wroga w 50 m od ktoregokolwiek z nich, potem jedna SZARZA (jak CrashScribe).
    /// Czas misji, nie rzeczywisty - przewijanie zostaje (limit bitwy CrashScribe -BattleSec liczy sekundy rzeczywiste). Linie "AI bitwy (333) TEST armia gracza".
    /// </summary>
    internal static partial class HorsePursuit
    {
        private const float BaitFar = 80f, BaitNear = 65f, BaitReach = 8f, BaitCloserAfter = 25f, BaitFleeR = 35f, BaitChaseR = 90f, BaitApproach = 3f,
                            BaitLeave = -2f, BaitCalm = 2f, BaitTrail = 55f, BaitFootGap = 40f, BaitLost = 170f, BaitMaxSec = 180f, BaitN60 = 60f;
        private const int BaitFull = ZGuardMaxN;
        private static readonly string[] BaitPhName = { "podjazd", "strzal", "ucieczka", "pogon" };

        private static bool _bOn;
        private static int _bPh, _bChase, _bFlee, _bTurn, _bTurnFull, _bTurnMax, _bCycle, _bWield, _bFire;
        private static float _bPhAt, _bCalmAt, _bStand, _bLastFlee;
        private static Vec2 _bSP = Vec2.Invalid;
        private static readonly int[] BShot = new int[4];
        // armia gracza stoi
        private const float TestArmyHold = 150f, TestArmyFree = 50f;
        private static int _aState, _aStops, _aForms;
        private static float _aStart, _aRel;
        private static string _aWhy = "-", _aPlan = "-";

        private static void BaitClear()
        {
            _bOn = false;
            _bPh = _bChase = _bFlee = _bTurn = _bTurnFull = _bTurnMax = _bCycle = _bWield = _bFire = 0;
            _bPhAt = 0f; _bCalmAt = -1f; _bStand = BaitFar; _bLastFlee = -1000f;
            _bSP = Vec2.Invalid;
            Array.Clear(BShot, 0, BShot.Length);
            _aState = _aStops = _aForms = 0;
            _aStart = -1f; _aRel = -1f;
            _aWhy = "-"; _aPlan = "-";
            _aOrdTried = false;   // 333t2: kolejna bitwa probuje znowu (pola trzymane po udanym odczycie)
        }

        /// <summary>OnFire: strzal nekajacego testu wedlug fazy przynety.</summary>
        private static void BaitShot()
        {
            if (!_bOn || _tScen != 0) return;
            BShot[Math.Max(0, Math.Min(3, _bPh))]++;
        }

        // ------------------------------------------------------------ armia gracza stoi (pass 1 s)

        private static void ArmyHoldBegin(Settings c)
        {
            if (c == null || !c.HuntHarasserInAutotest || LogGate.Mode != 1 || !_field) return;
            string plan = AutotestBattleKit.TestPlan();
            if (plan != "Strong" && plan != "Weak") return;
            _aPlan = plan;
            _aState = 1;
        }

        // 333t2 (runda 2: TypeInitializationException MovementOrder przy wczytaniu bitwy 1): pola statyczne MovementOrder (Charge / Stop) tworza sie przez
        // new Timer(Mission.Current.CurrentTime) - odczyt wprost w kodzie pozwala JIT odpalic konstruktor statyczny przy kompilacji metody (typ beforefieldinit),
        // a bez misji typ zostaje zepsuty do konca procesu. Teraz oba rozkazy raz przez refleksje, dopiero w trwajacej misji, potem z pamieci.
        private static object _aCharge, _aStop;
        private static bool _aOrdTried;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ArmyOrders(Mission m)
        {
            if (_aCharge != null && _aStop != null) return true;
            if (_aOrdTried || m == null || !ReferenceEquals(Mission.Current, m) || m.CurrentState != Mission.State.Continuing) return false;
            _aOrdTried = true;
            try
            {
                var t = typeof(MovementOrder);
                var fc = t.GetField("MovementOrderCharge", BindingFlags.Public | BindingFlags.Static);
                var fs = t.GetField("MovementOrderStop", BindingFlags.Public | BindingFlags.Static);
                if (fc == null || fs == null) { Log.Info("AI bitwy (333) TEST armia gracza: brak pol MovementOrder (Charge / Stop) - armia bez postoju"); return false; }
                _aCharge = fc.GetValue(null);
                _aStop = fs.GetValue(null);
            }
            catch (Exception e) { HStumble("HorsePursuit.ArmiaRozkazy", e); _aCharge = _aStop = null; }
            return _aCharge != null && _aStop != null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ArmyHold(Mission m, float now)
        {
            if (!ArmyOrders(m)) { if (_aOrdTried) { _aState = 0; _aWhy = "brak rozkazow"; } return; }
            var pt = m.PlayerTeam;
            int ps = SideOf(pt);
            if (ps < 0) return;
            if (_aStart < 0f)
            {
                _aStart = now;
                Log.Info("AI bitwy (333) TEST armia gracza: stoi (Stop co 1 s) do " + F0(TestArmyHold) + " s czasu misji albo do wroga w " + F0(TestArmyFree)
                         + " m, potem szarza wszystkich szykow (plan " + _aPlan + ") - okno pomiaru wahadla przed kontaktem i przyneta jak u Jeffa (jego wojsko stoi)");
            }
            float gap = float.MaxValue;
            var own = HForms[ps];
            var en = HForms[1 - ps];
            for (int i = 0; i < own.Count; i++)
                for (int j = 0; j < en.Count; j++)
                {
                    if (en[j].N < IntactMin) continue;
                    float d = Math.Max(0f, own[i].C.Distance(en[j].C) - own[i].R - en[j].R);
                    if (d < gap) gap = d;
                }
            string why = null;
            if (gap <= TestArmyFree) why = "wrog w " + F0(gap) + " m";
            else if (now - _aStart >= TestArmyHold) why = F0(TestArmyHold) + " s";
            var fl = pt.FormationsIncludingSpecialAndEmpty;
            int nf = fl != null ? fl.Count : 0, forms = 0;
            for (int j = 0; j < nf; j++)
            {
                var f = fl[j];
                if (f == null || f.CountOfUnits <= 0) continue;
                forms++;
                try
                {
                    if (why != null) f.SetMovementOrder((MovementOrder)_aCharge);
                    else if (f.GetReadonlyMovementOrderReference().OrderEnum != MovementOrder.MovementOrderEnum.Stop) { f.SetMovementOrder((MovementOrder)_aStop); _aStops++; }
                }
                catch (Exception e) { HStumble("HorsePursuit.ArmiaTest", e); }
            }
            if (forms > _aForms) _aForms = forms;
            if (why == null) return;
            _aState = 2;
            _aRel = now - _aStart;
            _aWhy = why;
            Log.Info("AI bitwy (333) TEST armia gracza: puszczona po " + _aRel.ToString("0.0", Inv) + " s (" + why + ") - szarza " + forms + " szykow, rozkazow stoj " + _aStops);
        }

        private static string ArmyTxt()
        {
            if (_aState == 0) return "armia gracza bez postoju (plan " + _aPlan + ")";
            if (_aState == 1) return "armia gracza stala do konca testu, rozkazow stoj " + _aStops;
            return "armia gracza stala " + _aRel.ToString("0.0", Inv) + " s (" + _aWhy + "), rozkazow stoj " + _aStops;
        }

        // ------------------------------------------------------------ przyneta (krok 0.25 s nekajacego testu, scenariusz A)

        /// <summary>Szyk i cel przynety: najblizszy szyk konnicy wroga (AI, bez odwrotu; konni wrecz przed konnymi lucznikami), najblizszy jego jezdziec; bez konnicy - jak 204.</summary>
        private static void BaitTarget(Mission m, Agent a)
        {
            int ps = SideOf(a.Team);
            if (ps < 0) { _tF = null; return; }
            var fl = HForms[1 - ps];
            Vec2 ap = a.Position.AsVec2;
            int best = -1;
            float bd = float.MaxValue;
            for (int pass = 0; pass < 2 && best < 0; pass++)
                for (int i = 0; i < fl.Count; i++)
                {
                    var fi = fl[i];
                    if (!fi.Cav || fi.Player || fi.Retreat || fi.F == null || fi.F.CountOfUnits <= 0) continue;
                    if (pass == 0 && fi.Ha) continue;
                    float d = ap.Distance(fi.C);
                    if (d < bd) { bd = d; best = i; }
                }
            if (best < 0) { TestTarget(m, a); return; }
            _tF = fl[best].F;
            _tBehind = false;
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
            if (tb != null) _tTgt = tb;
        }

        /// <summary>Punkt przynety: _bStand m od krawedzi szyku celu na linii do glownej sily druzyny nekajacego, 40 m od pieszych szykow wroga.</summary>
        private static Vec2 BaitPoint(Mission m, Agent a, int es)
        {
            var mp = _tF.CachedMedianPosition;
            Vec2 c = mp.IsValid ? mp.AsVec2 : _tF.CachedAveragePosition;
            Vec2 ap = a.Position.AsVec2;
            Vec2 home;
            Vec2 dir = (a.Team != null && MainForce(a.Team, out home) && home.Distance(c) > 1f) ? home - c : ap - c;
            if (dir.Length < 0.1f) return ap;
            dir = dir.Normalized();
            float r = Math.Min(RadiusCap, 0.5f * Math.Max(_tF.Width, _tF.Depth));
            Vec2 p = c + dir * (r + _bStand);
            for (int k = 0; k < 8 && NearestFoot(es, p) < BaitFootGap; k++) p += dir * 10f;
            return TestOnField(m, a, p) ? p : ap;
        }

        /// <summary>Jezdzcy wroga wokol nekajacego: najblizszy, ilu sie zbliza (>= 3 m/s w 90 m), ilu odjezdza, ilu w 60 m.</summary>
        private static void BaitScan(Mission m, Agent a, int es, out float er, out Agent near, out int chasers, out int leavers, out int n60)
        {
            er = float.MaxValue; near = null; chasers = leavers = n60 = 0;
            Vec2 ap = a.Position.AsVec2;
            var ags = m.Agents;
            int n = ags != null ? ags.Count : 0;
            for (int i = 0; i < n; i++)
            {
                Agent x;
                try { x = ags[i]; } catch { continue; }
                if (x == null || !x.IsHuman || !x.IsActive() || x.IsRunningAway) continue;
                var mt = x.MountAgent;
                if (mt == null || SideOf(x.Team) != es) continue;
                Vec2 rp = x.Position.AsVec2;
                float d = rp.Distance(ap);
                if (d > 250f) continue;
                if (d < er) { er = d; near = x; }
                if (d <= BaitN60) n60++;
                if (d < 0.5f) { chasers++; continue; }
                float app;
                try { app = mt.AverageVelocity.AsVec2.DotProduct((ap - rp) * (1f / d)); } catch { app = 0f; }
                if (d <= BaitChaseR && app >= BaitApproach) chasers++;
                else if (app <= BaitLeave) leavers++;
            }
        }

        /// <summary>Luk w rece (dobycie broni dystansowej, gdy w rece co innego), cel = jezdziec, szyk nekajacego strzela wedlug uznania.</summary>
        private static void BaitAim(Agent a, Agent tgt, bool pass)
        {
            if (tgt != null && !ReferenceEquals(tgt, _tTgt))
            {
                _tTgt = tgt;
                try { a.SetTargetAgent(tgt); } catch { }
            }
            if (!pass) return;
            try
            {
                var f = a.Formation;
                if (f != null && f.FiringOrder.OrderEnum != FiringOrder.RangedWeaponUsageOrderEnum.FireAtWill) { f.SetFiringOrder(FiringOrder.FiringOrderFireAtWill); _bFire++; }
            }
            catch { }
            try
            {
                var w = a.WieldedWeapon;
                var d = w.IsEmpty ? null : w.CurrentUsageItem;
                if (d != null && d.IsRangedWeapon) return;
                for (int i = 0; i < 5; i++)
                {
                    var s = a.Equipment[(EquipmentIndex)i];
                    if (s.IsEmpty || s.CurrentUsageItem == null || !s.CurrentUsageItem.IsRangedWeapon) continue;
                    a.TryToWieldWeaponInSlot((EquipmentIndex)i, Agent.WeaponWieldActionType.WithAnimation, false);
                    _bWield++;
                    break;
                }
            }
            catch (Exception e) { HStumble("HorsePursuit.PrzynetaLuk", e); }
        }

        private static void BaitGo(int ph, float now, string detail)
        {
            _bPh = ph;
            _bPhAt = now;
            _bCalmAt = -1f;
            TestPhase(ph == 2 ? "ucieczka" : BaitPhName[ph], now, detail);
        }

        private static void BaitStep(Mission m, Agent a, float now, bool pass)
        {
            int ps = SideOf(a.Team);
            if (ps < 0) { TestEnd(m, "bez strony", now); return; }
            int es = 1 - ps;
            if (pass || _tF == null || _tF.CountOfUnits <= 0) BaitTarget(m, a);
            if (_tF == null) { TestEnd(m, TestNoForm(a, "brak szyku wroga"), now); return; }
            float er;
            Agent near;
            int chasers, leavers, n60;
            BaitScan(m, a, es, out er, out near, out chasers, out leavers, out n60);
            Vec2 ap = a.Position.AsVec2;
            bool threat = chasers > 0 || er <= BaitFleeR;
            string rtxt = " (jezdzcow wroga: zbliza sie " + chasers + ", w " + F0(BaitN60) + " m " + n60 + ", odjezdza " + leavers + ", najblizszy "
                          + (er < float.MaxValue ? F0(er) : "-") + " m, strzalow " + _tShots + ")";
            if (_bPh <= 1)
            {
                if (threat)
                {
                    _bChase++;
                    _bFlee++;
                    _bLastFlee = now;
                    BaitGo(2, now, rtxt);
                }
                else
                {
                    if (pass || !_bSP.IsValid) _bSP = BaitPoint(m, a, es);
                    TestIssue(a, _bSP);
                    BaitAim(a, near != null && er <= 120f ? near : _tTgt, pass);
                    if (_bPh == 0 && ap.Distance(_bSP) <= BaitReach)
                        BaitGo(1, now, " (" + F0(_bStand) + " m od szyku konnicy " + _tF.FormationIndex + ", " + TestOwnTxt(a, ap) + ", " + rtxt.Substring(2));
                    else if (_bPh == 1 && _bStand > BaitNear && now - _bPhAt >= BaitCloserAfter) { _bStand = BaitNear; _bSP = Vec2.Invalid; _bPh = 0; _bPhAt = now; }
                    return;
                }
            }
            if (_bPh == 3)
            {
                if (threat)
                {
                    int k = Math.Max(1, chasers);   // ilu jezdzcow naprawde rusza na nekajacego (n60 liczy tez stojacych, za ktorymi jedzie)
                    _bTurn++;
                    if (k > _bTurnMax) _bTurnMax = k;
                    if (k > BaitFull && now - _bLastFlee <= ZLoopWin) _bTurnFull++;
                    TestPhase("nawrot", now, rtxt);
                    _bFlee++;
                    _bLastFlee = now;
                    BaitGo(2, now, rtxt);
                }
                else if (near == null || er > BaitLost)
                {
                    _bCycle++;
                    _bStand = BaitFar;
                    _bSP = Vec2.Invalid;
                    BaitGo(0, now, rtxt);
                    return;
                }
                else
                {
                    Vec2 rp = near.Position.AsVec2;
                    Vec2 g = rp.Distance(ap) > 0.5f ? rp + (ap - rp) * (BaitTrail / rp.Distance(ap)) : ap;
                    if (NearestFoot(es, g) < BaitFootGap || !TestOnField(m, a, g)) g = ap;
                    TestIssue(a, g);
                    BaitAim(a, near, pass);
                    return;
                }
            }
            // ucieczka do glownej sily swojej druzyny
            Vec2 home;
            Vec2 hp = (a.Team != null && MainForce(a.Team, out home)) ? home : ap;
            TestIssue(a, hp);
            BaitAim(a, near != null && er <= 120f ? near : _tTgt, pass);
            if (threat) { _bCalmAt = -1f; return; }
            if (_bCalmAt < 0f) { _bCalmAt = now; return; }
            if (now - _bCalmAt < BaitCalm) return;
            if (near != null && er <= BaitLost) BaitGo(3, now, rtxt);
            else
            {
                _bCycle++;
                _bStand = BaitFar;
                _bSP = Vec2.Invalid;
                BaitGo(0, now, rtxt);
            }
        }

        private static void BaitEndLine(Agent a, string why, float now)
        {
            if (!_bOn || _tScen != 0) return;
            Log.Info("AI bitwy (333) TEST przyneta koniec: nekajacy " + Who(a) + ", wynik " + why + " po " + Math.Max(0f, now - _tStart).ToString("0.0", Inv)
                     + " s | strzalow " + _tShots + " (podjazd " + BShot[0] + ", strzal " + BShot[1] + ", ucieczka " + BShot[2] + ", pogon " + BShot[3]
                     + "), dobyc luku " + _bWield + ", rozkazow strzelania " + _bFire + " | konnica ruszyla " + _bChase + " razy, ucieczek " + _bFlee
                     + ", nawrotow konnicy po ucieczce " + _bTurn + " (calej jazdy > " + BaitFull + " jezdzcow w " + F0(ZLoopWin) + " s " + _bTurnFull
                     + ", najwiecej jezdzcow naraz " + _bTurnMax + "), cykli " + _bCycle + ", faza na koniec " + BaitPhName[Math.Max(0, Math.Min(3, _bPh))]
                     + " | " + ArmyTxt() + ".");
        }
    }
}
