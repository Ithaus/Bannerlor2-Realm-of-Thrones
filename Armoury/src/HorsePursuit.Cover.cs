using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 311a "Oslona strzelca" (31.10, Jeff: "chowam sie za drzewem i strzelam do 6 lucznikow ... oni wpadli by na to zeby mnie okrazyc z prawej i lewej strony
    /// a nie stoja przed drzewem ... zeby AI naprawde myslala"). Klucz AiFlankHiddenShooter (z 283 FormationsAnswerHarassers), CoverHoldSeconds, CoverHiddenSeconds.
    /// Przyczyna (rozpoznanie 311 + kontrola): epizod 283 z samymi lucznikami konczyl sie po ShieldsUpHoldSeconds (8 s) ciszy nekajacego (Hunt.cs ThreatPass
    /// "quiet && rx && Melee + Foot <= 0") - dokladnie wtedy, gdy schowany laduje (log Jeffa 30.10 00:58-01:00: "koniec: inne po 8.0 s" 4 razy z rzedu);
    /// wykrycie wymaga pocisku; promien R1b (RayCastForClosestAgent) widzi tylko ludzi - drzewo daje "czysta"; RBM RBMBehaviorArcherSkirmish stoi w Shooting
    /// (martwy _cantShootTimer, nieuzywany stan Flanking - RBMAI.cs:11213/11244/11545); strzelcy w szykach piechoty nie sa w R1, R2 pomija strzelcow.
    ///
    /// JEDNA REGULA (glowny watek; pass co 1 s - start / trzymanie, krok co 0.25 s - kontrola co 0.5 s na epizod):
    ///  - start: nekajacy pieszo, samotny (bez nietknietego szyku swojej strony w 80 m - IntactRadius), rodzaj strzelec (283 KindNow), trafil czlowieka strony
    ///    (OnFire zderzenie - LastHarm) w ostatnich CoverHoldSeconds, w 110 m (HuntClose) od szyku strony; epizod ma wlasny zegar - zagrozenie 283 nie konczy sie
    ///    cisza ShieldsUpHoldSeconds; koniec: CoverHoldSeconds po ostatniej szkodzie, nekajacy padl / zszedl, dalej niz 110 m, dosiadl konia, klucz, koniec bitwy;
    ///  - grupa: ludzie AI strony (pieszo, nie gracz, nie szyki gracza - PlayerForm, nie w odwrocie, bez konnicy - ta ma 204) w 110 m od nekajacego, takze strzelcy
    ///    w szykach piechoty; przeglad najwyzej 128 ludzi co 3 s;
    ///  - ukrycie: co 0.5 s najwyzej 3 obserwatorow (strzelcy grupy, bez nich reszta) - u lucznika z celem 283 na nekajacego stan silnika
    ///    GetLastTargetVisibilityState (TargetIsNotSeen / TargetIsClear, bez promienia), inaczej promien gry Scene.RayCastForClosestEntityOrTerrain (oko -> piers,
    ///    flagi pocisku CommonCollisionExcludeFlagsForMissile jak RangedSiegeWeapon.cs:1807, pod Scene.PhysicsAndRayCastLock jak BloodPools); wiekszosc zaslonieta
    ///    = probka ukryta; ukryty = probki ukryte bez przerwy przez CoverHiddenSeconds; promieni najwyzej 64 na sekunde (wszystkie epizody);
    ///  - odpowiedz (przy ukryciu, ponowny plan co 6 s najwyzej 4 razy, gdy obchodzacy na miejscu dalej nie widza): punkty na luku po obu stronach nekajacego
    ///    (75 / 60 / 90 stopni od linii grupa-cel, promien 0.6 x odleglosc w 20-40 m i o 12 m blizej), punkt na siatce nawigacyjnej i z czystym promieniem
    ///    z wysokosci oka (ziemia + 1.6 m) do piersi celu; strzelcy (najwyzej 16) podzieleni na dwie polowy wedlug strony linii grupa-cel (bez krzyzowania drog),
    ///    pozycja skryptowa na punkt (krok 283 ApplyStep - wlasciciel Hunters, zdejmuje EndHunter); kto zobaczy cel w drodze (stan silnika albo promien) - staje
    ///    tam (pozycja skryptowa w miejscu, bez NoAttack - strzela; samo DisableScriptedMovement oddaloby go RBM, ktory odprowadza do miejsca w szyku za linia
    ///    oslony); na punkcie bez linii - tez staje (licznik); szturm: przy przewadze 3:1 nad nekajacym i jego ludzmi w 30 m ludzie z bronia biala (najwyzej 12
    ///    razem z piechota R2 tego nekajacego; strzelcy z bronia boczna, gdy bialych mniej niz 3) biegna z dwoch stron (283 RoleFoot, punkt z boku celu
    ///    min(12, 0.4 x odleglosc) - zbiega sie w 12 m), smycz od miejsca wyslania = odleglosc do celu + 25 m (najwyzej 130 m), 60 s jak R2;
    ///  - nigdy: gracz i ludzie sterowani przez gracza, szyki gracza, bohaterowie, chorazowie, umarli, konni, panika / odwrot rannego, poscig 148, zajeci
    ///    (CanBeAssignedForScriptedMovement), w zwarciu (MeleeUntil / zapytanie jak R2), przy nietknietym szyku strony nekajacego, w przerwie 30 s;
    ///  - zwolnienie zawsze przez EndHunter (cel automatyczny, pozycja zdjeta): koniec epizodu, koniec zagrozenia, OnAgentRemoved, odwrot 143, koniec misji
    ///    (HuntEndAll); swoi w 6 m od celu (283 RespNear) zdejmuja strzelcow jak dotad (bez strzalu w zwarcie).
    /// RBM: DisableScriptedMovement tylko przy zmianie szyku (RBMAI.cs:10279, agentsToChangeFormation) - wtedy HunterCheck "inne" zwalnia; rozkazy szyku RBM
    /// nie zdejmuja pozycji czlowieka (jak krok R1b i R2). Wyjatek w epizodzie = licznik potkniec epizodu (FailsOut - koniec tego epizodu, bez wylacznika).
    /// Poprawki recenzji 311a: wlasny zegar dopiero po probce ukrytej (otwarte pole - koniec po 8 s ciszy jak dotad); najwyzej 8 epizodow naraz; odpowiedz
    /// wstrzymana przy RespNear / zwarciu ze swoimi (132b), rozrzut z punktu luku; szturm z przerwa FootCool; wszyscy strzelcy obchodza - patrza biali, a bez
    /// obserwatorow probki obchodzacych; obchodzacy w drodze najdluzej 30 s; zatrzymanie z Hold; R1 z czysta linia zostaje; ostatni szturmujacy nie konczy
    /// epizodu 283 przy zywej oslonie; wyslani bez epizodu 283 - StartEp od razu.
    /// Linie: "Oslona strzelca (311a) start: ..." raz na proces, "Oslona strzelca (311a): misja ..." raz na bitwe polowa.
    /// </summary>
    internal static partial class HorsePursuit
    {
        // ------------------------------------------------------------ stale 311a (zrodla - opis klasy)
        private const float CovCheckEvery = 0.5f, CovCollectEvery = 3f, CovReplan = 6f;
        private const int CovReplanMax = 4, CovObsPerCheck = 3, CovRaysPerSec = 64, CovScanMax = 128, CovFlankMax = 16, CovRushMax = 12, CovRushMin = 3, CovQPerSec = 32;
        private const float CovRMin = 20f, CovRMax = 40f, CovOdds = 3f, CovFoesR = 30f, CovArrive = 2.5f, CovEyeZ = 1.6f, CovRushSide = 12f, CovLeashPlus = 25f;
        private const float CovLeashMax = HuntClose + 20f;
        private const int CovBlindReplan = 4;
        private const int CovEpMax = 8;          // recenzja 311a (koszt): najwyzej tyle epizodow naraz (zbieranie grupy co 3 s - ApplyActionOnEachUnit)
        private const float CovMoveMax = 30f;    // recenzja 311a (4): obchodzacy w drodze najdluzej tyle - potem staje (punkt nieosiagalny: rzeka, urwisko)
        private const byte CvFlankL = 1, CvFlankR = 2, CvRushL = 3, CvRushR = 4;
        private const byte CsMove = 0, CsPin = 1;
        // koniec epizodu
        private const int CeClock = 0, CeDown = 1, CeFar = 2, CeMounted = 3, CeEnded = 4, CeKey = 5, CeOther = 6, CeNotLone = 7, CeKinds = 8;
        private static readonly string[] CeName = { "czas po szkodzie", "nekajacy padl", "dalej niz 110 m", "konno", "koniec bitwy", "klucz", "inne", "nie samotny" };
        private static readonly float[] CovAng = { 75f, 60f, 90f };

        private sealed class CovEp
        {
            public Threat T;
            public float Start, HidSince = -1f, NextCheck, NextCollect, NextReplan, SentAt = -1f;
            public bool Sent, Hidden, EverHidden, NeedReplan;
            public int Replans, ObsRot, Men, Fails;
            public Vec2 G = Vec2.Invalid, U;
            public readonly List<Agent> Shoot = new List<Agent>(), Melee = new List<Agent>();
        }

        // ------------------------------------------------------------ stan misji 311a (glowny watek; _covered i _cvTgtHit - OnHit)
        private static bool _cvKey, _cvLogged;
        private static float _cvHold = 45f, _cvHid = 2.5f;
        private static readonly List<CovEp> CovList = new List<CovEp>(), CovTmp = new List<CovEp>();
        private static readonly List<Hunter> CovH = new List<Hunter>();
        private static readonly List<KeyValuePair<float, Agent>> CovCand = new List<KeyValuePair<float, Agent>>();
        private static readonly List<Agent> CovRush = new List<Agent>();
        private static Agent[] _covered;
        private static float _cvSecAt;
        private static int _cvRaySec, _cvQSec;

        // ------------------------------------------------------------ liczniki misji 311a
        private static readonly int[] CeN = new int[CeKinds];
        private static readonly HashSet<int> CvL = new HashSet<int>(), CvR = new HashSet<int>(), CvRL = new HashSet<int>(), CvRR = new HashSet<int>();
        private static int _cvEp, _cvHidEp, _cvResp, _cvReplanN, _cvRays, _cvRayBlk, _cvRayCap, _cvEngNot, _cvEngClear, _cvNoPoint, _cvNoLos, _cvNoOdds, _cvConv,
                           _cvSight, _cvArriveBlind, _cvReflank, _cvHitAfter, _cvKillAfter, _cvTgtDown, _cvTgtDownCov, _cvEnded, _cvRel, _cvLeft, _cvPlayerSkip,
                           _cvQOver, _cvStum, _cvRespN, _cvNoMen, _cvChecks;
        private static int _cvEpCap, _cvNoHide, _cvFootCool, _cvRespHeld, _cvMoveOut, _cvClearKeep, _cvSpread;   // poprawki recenzji 311a
        private static float _cvHidSec, _cvToResp;
        private static long _cvTicks, _cvMaxTicks;
        private static int _cvTickN;
        private static int _cvTgtHit;   // Interlocked (OnHit)

        // ------------------------------------------------------------ poczatek / koniec misji

        private static void CoverBegin(Settings c)
        {
            CoverKeys(c);
            if (_cvLogged || !_field) return;
            _cvLogged = true;
            Log.Info("Oslona strzelca (311a) start: klucz " + (_cvKey ? "TAK" : "NIE") + " (z 283 " + (c != null && c.FormationsAnswerHarassers ? "TAK" : "NIE")
                     + "), trzymanie " + F0(_cvHold) + " s po ostatniej szkodzie, ukryty po " + _cvHid.ToString("0.0", Inv) + " s, kontrola co "
                     + CovCheckEvery.ToString("0.0", Inv) + " s, promieni najwyzej " + CovRaysPerSec + " na s, obchodzacych najwyzej " + CovFlankMax + ", szturm najwyzej "
                     + CovRushMax + " przy przewadze " + CovOdds.ToString("0", Inv) + ":1, luk " + F0(CovRMin) + "-" + F0(CovRMax) + " m.");
        }

        private static void CoverKeys(Settings c)
        {
            _cvKey = c != null && c.AiFlankHiddenShooter;
            _cvHold = c != null ? Math.Max(10f, Math.Min(120f, c.CoverHoldSeconds)) : 45f;
            _cvHid = c != null ? Math.Max(1f, Math.Min(10f, c.CoverHiddenSeconds)) : 2.5f;
        }

        private static void CoverClear()
        {
            Volatile.Write(ref _covered, null);
            CovList.Clear(); CovTmp.Clear(); CovH.Clear(); CovCand.Clear(); CovRush.Clear();
            _cvSecAt = 0f; _cvRaySec = _cvQSec = 0;
            Array.Clear(CeN, 0, CeN.Length);
            CvL.Clear(); CvR.Clear(); CvRL.Clear(); CvRR.Clear();
            _cvEp = _cvHidEp = _cvResp = _cvReplanN = _cvRays = _cvRayBlk = _cvRayCap = _cvEngNot = _cvEngClear = _cvNoPoint = _cvNoLos = _cvNoOdds = _cvConv = 0;
            _cvSight = _cvArriveBlind = _cvReflank = _cvHitAfter = _cvKillAfter = _cvTgtDown = _cvTgtDownCov = _cvEnded = _cvRel = _cvLeft = _cvPlayerSkip = 0;
            _cvQOver = _cvStum = _cvRespN = _cvNoMen = _cvChecks = 0;
            _cvEpCap = _cvNoHide = _cvFootCool = _cvRespHeld = _cvMoveOut = _cvClearKeep = _cvSpread = 0;
            _cvHidSec = _cvToResp = 0f;
            _cvTicks = _cvMaxTicks = 0L;
            _cvTickN = 0;
            Interlocked.Exchange(ref _cvTgtHit, 0);
        }

        private static void CvStumble(string where, Exception e)
        {
            _cvStum++;
            Stumble(where, e);
        }

        // ------------------------------------------------------------ zdarzenia (glowny watek; CoverOnHit - dowolny watek)

        /// <summary>OnFire: pocisk nekajacego trafil czlowieka strony - zegar epizodu od nowa; po obejsciu - licznik szkod.</summary>
        private static void CoverHarm(Threat t, float now)
        {
            t.LastHarm = now;
            if (t.Cov != null && t.Cov.Sent) _cvHitAfter++;
        }

        /// <summary>OnHit (dowolny watek): nekajacy z obejsciem trafiony przez wroga - tylko tablica i Interlocked.</summary>
        private static void CoverOnHit(Agent att, Agent vic)
        {
            if (!In(Volatile.Read(ref _covered), vic)) return;
            var at = att.Team;
            var vt = vic.Team;
            if (!MissionTeams.Valid(at) || !MissionTeams.Valid(vt) || at.Side == vt.Side) return;
            Interlocked.Increment(ref _cvTgtHit);
        }

        /// <summary>HuntRemoved: powalony przez nekajacego z obejsciem (po obejsciu), nekajacy z obejsciem padl (od obchodzacych).</summary>
        private static void CoverRemoved(Agent a, Agent killer, bool down)
        {
            if (!down) return;
            Threat kt;
            if (killer != null && Threats.TryGetValue(killer, out kt) && kt.Cov != null && kt.Cov.Sent) _cvKillAfter++;
            Threat t;
            if (!Threats.TryGetValue(a, out t) || t.Cov == null || !t.Cov.Sent) return;
            _cvTgtDown++;
            Hunter bh;
            if (killer != null && Hunters.TryGetValue(killer, out bh) && ReferenceEquals(bh.T, t) && bh.Cov != 0) _cvTgtDownCov++;
        }

        // ------------------------------------------------------------ start i trzymanie (ThreatPass, pass co 1 s)

        /// <summary>true = zagrozenie zyje wlasnym zegarem 311a (ThreatPass nie konczy go cisza ShieldsUpHoldSeconds / odpedzeniem). Start epizodu tutaj.</summary>
        private static bool CoverKeep(Mission m, Threat t, Vec2 sp, bool lone, float dAll, float now)
        {
            var ep = t.Cov;
            var s = t.S;
            if (!_cvKey || !_rKey)
            {
                if (ep != null) CoverEnd(t, CeKey, now, true);
                return false;
            }
            if (ep != null)
            {
                int why = CoverWhyEnd(m, t, now);
                if (why < 0 && dAll > HuntClose) why = CeFar;
                if (why < 0 && !lone) why = CeNotLone;   // jego ludzie nadeszli - zwykla bitwa (283 / RBM)
                if (why >= 0) { CoverEnd(t, why, now, true); return false; }
                // recenzja 311a (1): wlasny zegar dopiero po dowodzie ukrycia (probka ukryta albo ukryty kiedykolwiek) - strzelec na otwartym polu konczy sie
                // jak dotad cisza ShieldsUpHoldSeconds (bez 45 s postoju R4 / R5 i fal R2)
                return ep.EverHidden || ep.HidSince >= 0f;
            }
            if (t.LastHarm < 0f || now - t.LastHarm > _cvHold) return false;
            if (s.MountAgent != null || !lone || dAll > HuntClose || MeleeKind(t)) return false;
            if (CovList.Count >= CovEpMax) { _cvEpCap++; return false; }   // recenzja 311a (koszt): limit epizodow naraz
            ep = new CovEp { T = t, Start = now, NextCheck = now, NextCollect = now };
            t.Cov = ep;
            CovList.Add(ep);
            _cvEp++;
            return false;   // recenzja 311a (1): nowy epizod bez probek - zegar 283 bez zmian
        }

        /// <summary>-1 = epizod trwa, inaczej Ce* (bez odleglosci - ta w passie z szykow).</summary>
        private static int CoverWhyEnd(Mission m, Threat t, float now)
        {
            var s = t.S;
            if (!_cvKey || !_rKey) return CeKey;   // klucze z MCM na zywo (HuntPass) - takze gdy ThreatPass przy 283 NIE juz nie wola CoverKeep
            if (m.MissionEnded) return CeEnded;
            if (s == null || !s.IsActive()) return CeOther;
            if (s.MountAgent != null) return CeMounted;
            if (now - t.LastHarm > _cvHold) return CeClock;
            return -1;
        }

        /// <summary>Koniec epizodu 311a. release - ludzie 311a oddani grze (EndHunter); z EndThreat bez (zagrozenie zwalnia wszystkich sam, z wlasciwym wynikiem 283).</summary>
        private static void CoverEnd(Threat t, int why, float now, bool release)
        {
            var ep = t.Cov;
            if (ep == null) return;
            t.Cov = null;
            CovList.Remove(ep);
            CeN[why]++;
            if (!ep.EverHidden) _cvNoHide++;
            if (t.S != null) MarkIn(ref _covered, t.S, false);
            if (!release) return;
            CovH.Clear();
            for (int i = 0; i < t.Hs.Count; i++) if (t.Hs[i].Cov != 0) CovH.Add(t.Hs[i]);
            for (int i = 0; i < CovH.Count; i++)
            {
                var h = CovH[i];
                Hunter cur;
                if (h.A == null || !Hunters.TryGetValue(h.A, out cur) || !ReferenceEquals(cur, h)) continue;   // zwolniony wczesniej (koniec epizodu 283)
                try { EndHunter(h, UThreat, now, true, false); }
                catch (Exception e) { CvStumble("HorsePursuit.OslonaKoniec", e); DropHunter(h); }
            }
            CovH.Clear();
        }

        // ------------------------------------------------------------ krok (HuntTrack, co 0.25 s; epizod co 0.5 s)

        private static void CoverTick(Mission m, float now)
        {
            if (CovList.Count == 0) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now >= _cvSecAt) { _cvSecAt = now + 1f; _cvRaySec = 0; _cvQSec = 0; }
            CovTmp.Clear();
            CovTmp.AddRange(CovList);
            for (int i = 0; i < CovTmp.Count; i++)
            {
                var ep = CovTmp[i];
                if (!ReferenceEquals(ep.T.Cov, ep) || now < ep.NextCheck) continue;
                ep.NextCheck = now + CovCheckEvery;
                try { CoverStep(m, ep, now); }
                catch (Exception e)
                {
                    CvStumble("HorsePursuit.Oslona", e);
                    if (++ep.Fails >= FailsOut)
                    {
                        try { CoverEnd(ep.T, CeOther, now, true); } catch { ep.T.Cov = null; CovList.Remove(ep); }
                    }
                }
            }
            CovTmp.Clear();
            long dt = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            _cvTicks += dt;
            if (dt > _cvMaxTicks) _cvMaxTicks = dt;
            _cvTickN++;
        }

        private static void CoverStep(Mission m, CovEp ep, float now)
        {
            var t = ep.T;
            var s = t.S;
            Threat cur;
            // zagrozenie zdjete bez EndThreat (RunThreat catch -> RemoveThreat): recenzja 311a - ludzie 311a tez oddani grze (EndHunter), nie do konca misji
            if (s == null || !Threats.TryGetValue(s, out cur) || !ReferenceEquals(cur, t)) { CoverEnd(t, CeOther, now, true); return; }
            int why = CoverWhyEnd(m, t, now);
            if (why >= 0) { CoverEnd(t, why, now, true); return; }
            if (now >= ep.NextCollect) { CoverCollect(ep); ep.NextCollect = now + CovCollectEvery; }
            _cvChecks++;
            Vec3 tc = s.GetChestGlobalPosition();
            // czy grupa go widzi (bez obchodzacych - ci maja wlasna kontrole); recenzja 311a (3): wszyscy strzelcy obchodza - patrza ludzie z bronia biala
            int seen = 0, blind = 0;
            Observe(m, ep, ep.Shoot, tc, now, ref seen, ref blind);
            if (seen + blind == 0 && ep.Melee.Count > 0) Observe(m, ep, ep.Melee, tc, now, ref seen, ref blind);
            if (ep.Shoot.Count + ep.Melee.Count > 0) ep.ObsRot = (ep.ObsRot + CovObsPerCheck) % Math.Max(1, Math.Max(ep.Shoot.Count, ep.Melee.Count));
            // obchodzacy przed odpowiedzia (recenzja 311a (3): bez obserwatorow ich probki decyduja o ukryciu - inaczej HidSince stal, a ukrycie trwalo do konca)
            bool noObs = seen + blind == 0;
            FlankersStep(m, ep, tc, now, noObs, ref seen, ref blind);
            if (seen + blind > 0)
            {
                if (blind > seen)
                {
                    if (ep.HidSince < 0f) ep.HidSince = now;
                    _cvHidSec += CovCheckEvery;
                }
                else ep.HidSince = -1f;
            }
            ep.Hidden = ep.HidSince >= 0f && now - ep.HidSince >= _cvHid;
            if (ep.Hidden && !ep.EverHidden) { ep.EverHidden = true; _cvHidEp++; }
            if (ep.Hidden && now >= ep.NextReplan && (!ep.Sent || (ep.NeedReplan && ep.Replans < CovReplanMax))) Respond(m, ep, now);
        }

        /// <summary>Do CovObsPerCheck obserwatorow z listy (bez obchodzacych) - probka widzi / zasloniety.</summary>
        private static void Observe(Mission m, CovEp ep, List<Agent> obs, Vec3 tc, float now, ref int seen, ref int blind)
        {
            var t = ep.T;
            int n = obs.Count;
            for (int k = 0, used = 0; k < n && used < CovObsPerCheck; k++)
            {
                int i = (ep.ObsRot + k) % n;
                var a = obs[i];
                if (a == null || !a.IsActive()) continue;
                Hunter h;
                if (Hunters.TryGetValue(a, out h) && h.Cov != 0 && h.Cov <= CvFlankR) continue;
                used++;
                int v = Sight(m, a, t, tc, true, now);
                if (v == 1) seen++; else if (v == 0) blind++;
            }
        }

        /// <summary>1 = widzi, 0 = zasloniety, -1 = bez promienia (budzet). engine - lucznik z celem 283 na nekajacego od co najmniej 1 s: najpierw stan silnika
        /// (stan dotyczy OSTATNIEGO celu - tuz po SetTargetAgent bywa po starym; obchodzacy w drodze - sam promien).</summary>
        private static int Sight(Mission m, Agent a, Threat t, Vec3 tc, bool engine, float now)
        {
            Hunter h;
            if (engine && Hunters.TryGetValue(a, out h) && ReferenceEquals(h.T, t) && h.Role == RoleRanged && now - h.Start >= 1f)
            {
                AITargetVisibilityState vs = AITargetVisibilityState.NotChecked;
                try { vs = a.GetLastTargetVisibilityState(); } catch { vs = AITargetVisibilityState.NotChecked; }
                if (vs == AITargetVisibilityState.TargetIsNotSeen) { _cvEngNot++; return 0; }
                if (vs == AITargetVisibilityState.TargetIsClear) { _cvEngClear++; return 1; }
            }
            return Ray(m, a.GetEyeGlobalPosition(), tc);
        }

        /// <summary>Promien przez teren i obiekty sceny (bez ludzi): 1 = czysty, 0 = zasloniety, -1 = budzet sekundy wyczerpany.</summary>
        private static int Ray(Mission m, Vec3 from, Vec3 to)
        {
            if (_cvRaySec >= CovRaysPerSec) { _cvRayCap++; return -1; }
            Vec3 d = to - from;
            float l = d.Length;
            if (l < 1.5f) return 1;
            var sc = m.Scene;
            if (sc == null) return -1;
            Vec3 dir = d * (1f / l);
            Vec3 a0 = from + dir * 0.4f, b0 = to - dir * 0.6f;
            float len = l - 1f, cd;
            bool hit;
            _cvRaySec++;
            _cvRays++;
            using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
                hit = sc.RayCastForClosestEntityOrTerrain(a0, b0, out cd, 0.01f, BodyFlags.CommonCollisionExcludeFlagsForMissile);
            if (hit && cd < len - 0.05f) { _cvRayBlk++; return 0; }
            return 1;
        }

        /// <summary>Ludzie grupy: AI strony pieszo w 110 m od nekajacego z szykow strony (bez szykow gracza, w odwrocie, konnicy); srodek grupy.</summary>
        private static void CoverCollect(CovEp ep)
        {
            ep.Shoot.Clear(); ep.Melee.Clear();
            ep.Men = 0;
            var t = ep.T;
            Vec2 sp = t.S.Position.AsVec2;
            var fl = HForms[t.Side];
            float gx = 0f, gy = 0f;
            int scanned = 0;
            for (int j = 0; j < fl.Count && scanned < CovScanMax; j++)
            {
                var fi = fl[j];
                if (fi.Cav || fi.Retreat) continue;
                if (Math.Max(0f, sp.Distance(fi.C) - fi.R) > HuntClose) continue;
                if (fi.Player) { _cvPlayerSkip++; continue; }
                HUnits.Clear();
                fi.F.ApplyActionOnEachUnit(HCollect);
                for (int k = 0; k < HUnits.Count && scanned < CovScanMax; k++)
                {
                    var a = HUnits[k];
                    scanned++;
                    if (a == null || !a.IsActive() || !a.IsHuman || a.IsMainAgent || !a.IsAIControlled || a.MountAgent != null) continue;
                    Vec2 ap = a.Position.AsVec2;
                    if (ap.Distance(sp) > HuntClose) continue;
                    gx += ap.x; gy += ap.y;
                    ep.Men++;
                    if (a.IsHero || Banner(a) || Undead.Character(a.Character)) continue;
                    if (a.IsRangedCached) ep.Shoot.Add(a);
                    else if (a.HasMeleeWeaponCached) ep.Melee.Add(a);
                }
                HUnits.Clear();
            }
            ep.G = ep.Men > 0 ? new Vec2(gx / ep.Men, gy / ep.Men) : Vec2.Invalid;
        }

        // ------------------------------------------------------------ odpowiedz

        private static void Respond(Mission m, CovEp ep, float now)
        {
            var t = ep.T;
            var s = t.S;
            // recenzja 311a (1): swoi walcza z nim (283 RespNear - R2 / szturm w 6 m) albo nekajacy wrecz w zwarciu ze swoimi (132b) - bez nowych
            // strzelcow na luk (RangedRecheck i tak by ich zdjal; promien nie widzi ludzi) - proba za 2 s, NeedReplan zostaje
            if (RespNear(t)) { _cvRespHeld++; ep.NextReplan = now + 2f; return; }
            if (_cvQSec >= CovQPerSec) { _cvQOver++; return; }
            _cvQSec++;
            NearFriends(m, t);
            if (_nearMelee) { _cvRespHeld++; ep.NextReplan = now + 2f; return; }
            CoverCollect(ep);
            ep.NextCollect = now + CovCollectEvery;
            ep.NextReplan = now + CovReplan;
            ep.NeedReplan = false;
            if (!ep.G.IsValid) { _cvNoMen++; return; }
            Vec2 c = s.Position.AsVec2;
            Vec2 u = c - ep.G;
            float dd = u.Length;
            if (dd < 3f) return;
            u = u * (1f / dd);
            ep.U = u;
            if (ep.Sent) { ep.Replans++; _cvReplanN++; }
            _cvRespN++;
            Vec2 wl, wr;
            bool okL = ArcPoint(m, s, c, u, dd, 1f, out wl);
            bool okR = ArcPoint(m, s, c, u, dd, -1f, out wr);
            if (!okL) _cvNoPoint++;
            if (!okR) _cvNoPoint++;
            // szturm: przewaga 3:1 nad nekajacym i jego ludzmi w 30 m (jedno zapytanie z budzetu sekundy)
            bool rush = false;
            if (now < t.FootCool) _cvFootCool++;   // recenzja 311a (2): piechota wrocila ze smyczy / czasu - przerwa 30 s jak R2 (React.cs FootPass)
            else if (_cvQSec < CovQPerSec)
            {
                _cvQSec++;
                rush = ep.Men >= CovOdds * Math.Max(1, FoesNear(m, s, t.SSide));
                if (!rush) _cvNoOdds++;
            }
            else _cvQOver++;
            var n = new Vec2(-u.y, u.x);
            int rushed = 0, flanked = 0;
            CovRush.Clear();
            if (rush)
            {
                // piechota R2 tego nekajacego - z dwoch stron (jedna regula, bez drugiej fali)
                for (int i = 0; i < t.Hs.Count; i++)
                {
                    var h = t.Hs[i];
                    if (h.Role != RoleFoot || h.A == null || !h.A.IsActive()) continue;
                    if (h.Cov == 0) _cvConv++;
                    RushSet(h, c, ep.G, n, false);   // recenzja 311a (4): smycz od dotychczasowej podstawy R2 (szyk / Anchor 291), nie od miejsca przejecia
                    rushed++;
                }
                PickNear(ep.Melee, t, c, now, CovRushMax - t.Foot, CovRush);
                if (t.Foot + CovRush.Count < CovRushMin)
                {
                    // malo bialej broni - strzelcy z bronia boczna najblizej celu
                    int want = CovRushMin - t.Foot - CovRush.Count;
                    CovCand.Clear();
                    for (int i = 0; i < ep.Shoot.Count; i++)
                    {
                        var a = ep.Shoot[i];
                        if (a == null || !a.IsActive() || !a.HasMeleeWeaponCached || Hunters.ContainsKey(a) || !CovElig(a, t, now)) continue;
                        CovCand.Add(new KeyValuePair<float, Agent>(a.Position.AsVec2.Distance(c), a));
                    }
                    if (CovCand.Count > 1) CovCand.Sort((x, y) => x.Key.CompareTo(y.Key));
                    for (int i = 0; i < CovCand.Count && want > 0; i++) { CovRush.Add(CovCand[i].Value); want--; }
                    CovCand.Clear();
                }
                for (int i = 0; i < CovRush.Count; i++)
                {
                    var a = CovRush[i];
                    if (!CovFree(m, a, t, now)) continue;
                    int fx = FiOf(t.Side, a.Formation);
                    if (fx < 0) continue;
                    var h = StartHunter(t, a, HForms[t.Side][fx], RoleFoot, 0f, now);
                    RushSet(h, c, ep.G, n, true);
                    Steer(h, now);
                    rushed++;
                }
            }
            // obchodzacy: strzelcy (bez szturmujacych), dwie polowy wedlug strony linii grupa-cel
            if (okL || okR)
            {
                CovCand.Clear();
                for (int i = 0; i < ep.Shoot.Count; i++)
                {
                    var a = ep.Shoot[i];
                    if (a == null || !a.IsActive() || CovRush.Contains(a)) continue;
                    Hunter h;
                    if (Hunters.TryGetValue(a, out h))
                    {
                        if (!ReferenceEquals(h.T, t) || h.Role != RoleRanged) continue;
                        if (h.Cov != 0 && h.CovSt == CsPin && h.CovBlind < CovBlindReplan) continue;   // obchodzacy z linia - zostaje
                        if (h.Cov == 0 && now - h.Start >= 1f && VisClear(a)) { _cvClearKeep++; continue; }   // recenzja 311a (6): lucznik R1 z czysta linia zostaje
                    }
                    else if (!CovElig(a, t, now)) continue;
                    Vec2 o = a.Position.AsVec2 - ep.G;
                    CovCand.Add(new KeyValuePair<float, Agent>(u.x * o.y - u.y * o.x, a));
                    if (CovCand.Count >= CovFlankMax) break;
                }
                if (CovCand.Count > 1) CovCand.Sort((x, y) => x.Key.CompareTo(y.Key));
                int half = CovCand.Count / 2, kl = 0, kr = 0;
                for (int i = 0; i < CovCand.Count; i++)
                {
                    var a = CovCand[i].Value;
                    bool left = CovCand.Count == 1 ? CovCand[i].Key > 0f : i >= half;   // najwieksza strona lewa (n), najmniejsza prawa
                    if (left && !okL) left = false;
                    else if (!left && !okR) left = true;
                    Vec2 w = left ? wl : wr;
                    int k = left ? kl++ : kr++;
                    Vec2 p = FlankSpot(s, c, w, k);
                    if (NearSpread(a, p, t)) { _cvSpread++; continue; }   // recenzja 311a (1): swoj przy celu w rozrzucie z punktu (132b (b))
                    Hunter h;
                    if (!Hunters.TryGetValue(a, out h))
                    {
                        if (!CovFree(m, a, t, now)) continue;
                        int fx = FiOf(t.Side, a.Formation);
                        if (fx < 0) continue;
                        float range = ShooterRange(a);
                        if (range <= 1f || a.Position.AsVec2.Distance(c) > range) continue;
                        h = StartHunter(t, a, HForms[t.Side][fx], RoleRanged, range, now);
                    }
                    else if (h.Cov != 0) _cvReflank++;
                    ApplyStep(h, p, h.Hold);
                    if (!h.Step) continue;
                    h.Cov = left ? CvFlankL : CvFlankR;
                    h.CovSt = CsMove;
                    h.CovBlind = 0;
                    h.CovAt = now;
                    if (left) CvL.Add(a.Index); else CvR.Add(a.Index);
                    flanked++;
                }
                CovCand.Clear();
            }
            CovRush.Clear();
            if (rushed + flanked <= 0) { if (!ep.Sent) ep.NextReplan = now + 2f; return; }   // nikogo - proba za 2 s, nie co kontrole
            if (!ep.Sent)
            {
                ep.Sent = true;
                ep.SentAt = now;
                _cvResp++;
                _cvToResp += now - ep.Start;
                MarkIn(ref _covered, s, true);
            }
            // recenzja 311a (6, stan): ludzie wyslani bez epizodu 283 - epizod od razu (jak koniec ThreatPass), zeby EndEp / kontakt / ReactEndLine ich liczyly;
            // odleglosc w linii startu - od srodka grupy (ThreatPass - od krawedzi szyku)
            if (!t.Active)
            {
                int dn;
                DownBy.TryGetValue(s, out dn);
                StartEp(t, now, 0, 0, flanked, dd, dn);
            }
        }

        /// <summary>Recenzja 311a (6): stan silnika u lucznika - czysta linia do celu.</summary>
        private static bool VisClear(Agent a)
        {
            try { return a.GetLastTargetVisibilityState() == AITargetVisibilityState.TargetIsClear; } catch { return false; }
        }

        /// <summary>Punkt luku po stronie sgn (1 lewa - normalna n, -1 prawa): na siatce i z czystym promieniem z wysokosci oka do piersi celu.</summary>
        private static bool ArcPoint(Mission m, Agent s, Vec2 c, Vec2 u, float dd, float sgn, out Vec2 w)
        {
            w = Vec2.Invalid;
            var wp0 = s.GetWorldPosition();
            if (!wp0.IsValid) return false;
            Vec3 tc = s.GetChestGlobalPosition();
            var n = new Vec2(-u.y, u.x);
            float r1 = Math.Max(CovRMin, Math.Min(CovRMax, 0.6f * dd));
            float r2 = Math.Max(CovRMin, r1 - 12f);
            for (int ri = 0; ri < 2; ri++)
            {
                float r = ri == 0 ? r1 : r2;
                if (ri == 1 && r2 >= r1 - 0.5f) break;
                for (int ai = 0; ai < CovAng.Length; ai++)
                {
                    float ang = CovAng[ai] * (float)Math.PI / 180f;
                    Vec2 q = c + (u * -(float)Math.Cos(ang) + n * (sgn * (float)Math.Sin(ang))) * r;
                    var wq = wp0;
                    wq.SetVec2(q);
                    if (wq.GetNavMesh() == UIntPtr.Zero) continue;
                    Vec3 g = wq.GetGroundVec3();
                    int v = Ray(m, new Vec3(g.x, g.y, g.z + CovEyeZ), tc);
                    if (v < 0) return false;
                    if (v == 1) { w = q; return true; }
                    _cvNoLos++;
                }
            }
            return false;
        }

        /// <summary>Miejsce k-tego obchodzacego przy punkcie w: na przemian 2.5 m w obie strony wzdluz luku (punkt poza siatka - sam w).</summary>
        private static Vec2 FlankSpot(Agent s, Vec2 c, Vec2 w, int k)
        {
            if (k == 0) return w;
            Vec2 r = w - c;
            float l = r.Length;
            if (l < 0.1f) return w;
            var tg = new Vec2(-r.y / l, r.x / l);
            float off = ((k + 1) / 2) * 2.5f * (k % 2 == 0 ? 1f : -1f);
            Vec2 q = w + tg * off;
            var wp = s.GetWorldPosition();
            if (!wp.IsValid) return w;
            wp.SetVec2(q);
            return wp.GetNavMesh() != UIntPtr.Zero ? q : w;
        }

        /// <summary>Szturmujacy: strona wedlug linii grupa-cel, smycz od miejsca wyslania (odleglosc do celu + 25 m, najwyzej 130 m). fresh - nowy (Anchor
        /// = miejsce wyslania); przejety z R2 albo ponowny plan - smycz od jego dotychczasowej podstawy (FootLeash), najwyzej 130 m od niej.</summary>
        private static void RushSet(Hunter h, Vec2 c, Vec2 g, Vec2 n, bool fresh)
        {
            var a = h.A;
            Vec2 ap = a.Position.AsVec2;
            bool left = Vec2.DotProduct(ap - g, n) >= 0f;
            h.Cov = left ? CvRushL : CvRushR;
            if (left) CvRL.Add(a.Index); else CvRR.Add(a.Index);
            if (fresh && h.Home == null && !h.Anchor.IsValid) h.Anchor = ap;
            float lim = Math.Min(CovLeashMax, FootLeash(h, ap) + ap.Distance(c) + CovLeashPlus);
            if (lim > h.Leash) h.Leash = Math.Max(MarchToContact.InfRearm, lim);
        }

        /// <summary>HunterCheck (283 RoleFoot): smycz piechoty - szturmujacy 311a wlasna (RushSet), reszta InfRearm jak dotad.</summary>
        private static float FootLim(Hunter h)
        {
            return h.Leash > 0f ? h.Leash : MarchToContact.InfRearm;
        }

        /// <summary>Steer (283 RoleFoot): punkt szturmujacego z boku celu - min(12 m, 0.4 x odleglosc) na jego strone, w 12 m prosto.</summary>
        private static Vec2 RushPoint(Hunter h, Vec2 sp, float d)
        {
            var ep = h.T.Cov;
            if (ep == null || d <= CovRushSide) return sp;
            Vec2 u = ep.U;
            var n = new Vec2(-u.y, u.x);
            float sg = h.Cov == CvRushL ? 1f : -1f;
            return sp + n * (sg * Math.Min(CovRushSide, 0.4f * d));
        }

        /// <summary>Najblizsi celowi z listy grupy (wolni - CovElig), najwyzej cap.</summary>
        private static void PickNear(List<Agent> src, Threat t, Vec2 c, float now, int cap, List<Agent> dst)
        {
            if (cap <= 0) return;
            CovCand.Clear();
            for (int i = 0; i < src.Count; i++)
            {
                var a = src[i];
                if (a == null || !a.IsActive() || Hunters.ContainsKey(a) || !CovElig(a, t, now)) continue;
                CovCand.Add(new KeyValuePair<float, Agent>(a.Position.AsVec2.Distance(c), a));
            }
            if (CovCand.Count > 1) CovCand.Sort((x, y) => x.Key.CompareTo(y.Key));
            for (int i = 0; i < CovCand.Count && dst.Count < cap; i++) dst.Add(CovCand[i].Value);
            CovCand.Clear();
        }

        /// <summary>Wolny do 311a (bez zapytan): AI pieszo, nie bohater / chorazy / umarly, nie panika / odwrot / poscig 148, nie zajety, nie w zwarciu z ostatnich
        /// ShieldsUp.Window s, nie w przerwie, nie przy nietknietym szyku strony nekajacego, szyk strony bez gracza.</summary>
        private static bool CovElig(Agent a, Threat t, float now)
        {
            if (a == null || !a.IsActive() || !a.IsHuman || a.IsMainAgent || !a.IsAIControlled || a.IsHero || a.MountAgent != null) return false;
            if (Banner(a) || Undead.Character(a.Character)) return false;
            if (a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a) || RidingAnyThread(a)) return false;
            float cu;
            if (HuntCool.TryGetValue(a, out cu) && now < cu) return false;
            if (!a.CanBeAssignedForScriptedMovement()) return false;
            float mu;
            if (MeleeUntil.TryGetValue(a, out mu) && now < mu) return false;
            if (IntactNear(t.SSide, a.Position.AsVec2, CounterFootGap)) return false;
            var f = a.Formation;
            if (f == null || PlayerForm(f)) return false;   // recenzja 311a: licznik tylko przy zbieraniu (bez podwojnego liczenia)
            return FiOf(t.Side, f) >= 0;
        }

        /// <summary>Przed wyslaniem nowego: zwarcie z kims innym niz nekajacy (zapytanie jak R2, budzet sekundy 311a).</summary>
        private static bool CovFree(Mission m, Agent a, Threat t, float now)
        {
            if (_cvQSec >= CovQPerSec) { _cvQOver++; return false; }
            _cvQSec++;
            if (!EnemyNear(m, a, t.S)) return true;
            MeleeUntil[a] = now + ShieldsUp.Window;
            return false;
        }

        /// <summary>Indeks szyku w HForms strony (bez szykow gracza, w odwrocie, konnicy), -1 = brak.</summary>
        private static int FiOf(int side, Formation f)
        {
            if (f == null || side < 0) return -1;
            var fl = HForms[side];
            for (int i = 0; i < fl.Count; i++)
            {
                if (!ReferenceEquals(fl[i].F, f)) continue;
                var fi = fl[i];
                return fi.Player || fi.Retreat || fi.Cav ? -1 : i;
            }
            return -1;
        }

        /// <summary>Nekajacy i jego ludzie (czynni, strona nekajacego) w 30 m - jedno zapytanie gry.</summary>
        private static int FoesNear(Mission m, Agent s, int ss)
        {
            int n = 0;
            m.GetNearbyAgents(s.Position.AsVec2, CovFoesR, HBuf);
            for (int i = 0; i < HBuf.Count; i++)
            {
                var x = HBuf[i];
                if (x != null && x.IsHuman && x.IsActive() && SideOf(x.Team) == ss) n++;
            }
            HBuf.Clear();
            return Math.Max(1, n);
        }

        /// <summary>Obchodzacy (co 0.5 s): w drodze - zobaczyl cel (stan silnika albo promien) = staje tam i strzela, na punkcie bez linii = staje; stoi
        /// i nie widzi przez CovBlindReplan kontroli przy ukrytym celu - ponowny plan.</summary>
        private static void FlankersStep(Mission m, CovEp ep, Vec3 tc, float now, bool noObs, ref int seen, ref int blind)
        {
            var t = ep.T;
            CovH.Clear();
            for (int i = 0; i < t.Hs.Count; i++) { var h = t.Hs[i]; if (h.Cov == CvFlankL || h.Cov == CvFlankR) CovH.Add(h); }
            int alive = 0;
            bool near = RespNear(t);   // recenzja 311a (1): swoi walcza z nim - bez ponownego planu
            for (int i = 0; i < CovH.Count; i++)
            {
                var h = CovH[i];
                var a = h.A;
                Hunter cur;
                if (a == null || !a.IsActive() || !Hunters.TryGetValue(a, out cur) || !ReferenceEquals(cur, h)) continue;
                alive++;
                if (h.CovSt == CsMove)
                {
                    // recenzja 311a (3): zatrzymanie z h.Hold - NoAttack 132b (zwarcie ze swoimi) nie skasowany
                    int v = Sight(m, a, t, tc, false, now);
                    if (v == 1) { ApplyStep(h, a.Position.AsVec2, h.Hold); h.CovSt = CsPin; h.CovBlind = 0; _cvSight++; if (noObs) seen++; continue; }
                    if (h.StepAt.IsValid && a.Position.AsVec2.Distance(h.StepAt) <= CovArrive) { ApplyStep(h, h.StepAt, h.Hold); h.CovSt = CsPin; h.CovBlind = 0; _cvArriveBlind++; }
                    else if (now - h.CovAt >= CovMoveMax) { ApplyStep(h, a.Position.AsVec2, h.Hold); h.CovSt = CsPin; h.CovBlind = 0; _cvMoveOut++; }   // recenzja 311a (4)
                    continue;
                }
                if (!ep.Hidden && !noObs) { h.CovBlind = 0; continue; }
                int vb = Sight(m, a, t, tc, true, now);
                if (vb == 0)
                {
                    if (noObs) blind++;
                    if (ep.Hidden && ++h.CovBlind >= CovBlindReplan && !near) ep.NeedReplan = true;
                }
                else if (vb == 1) { h.CovBlind = 0; if (noObs) seen++; }
            }
            CovH.Clear();
            if (ep.Sent && alive == 0 && ep.Hidden && !near) ep.NeedReplan = true;   // obchodzacy zdjeci (smycz, zasieg) - nowy plan, gdy dalej ukryty
        }

        // ------------------------------------------------------------ koniec misji i linia bitwy

        /// <summary>HuntEndAll (po zwolnieniu wszystkich): ludzie 311a wciaz pod sterowaniem (oczekiwane 0).</summary>
        private static void CoverEndAll()
        {
            int left = 0;
            for (int i = 0; i < HList.Count; i++) if (HList[i].Cov != 0) left++;
            _cvLeft = left + CovList.Count;
            CovList.Clear();
            Volatile.Write(ref _covered, null);
        }

        private static void CoverWrite()
        {
            if (!_field) return;
            double fr = System.Diagnostics.Stopwatch.Frequency;
            var sb = new StringBuilder(1400);
            sb.Append("Oslona strzelca (311a): misja ").Append(_kind).Append(", klucz ").Append(_cvKey ? "TAK" : "NIE").Append(" (283 ").Append(_rKey ? "TAK" : "NIE")
              .Append("), trzymanie ").Append(F0(_cvHold)).Append(" s, ukryty po ").Append(_cvHid.ToString("0.0", Inv)).Append(" s")
              .Append(" | epizody ").Append(_cvEp).Append(" (z ukryciem ").Append(_cvHidEp).Append(", z odpowiedzia ").Append(_cvResp).Append(", planow ").Append(_cvRespN)
              .Append(", ponownych ").Append(_cvReplanN).Append("), sekund ukrycia ").Append(_cvHidSec.ToString("0.0", Inv)).Append(", sr. do odpowiedzi ")
              .Append(_cvResp > 0 ? (_cvToResp / _cvResp).ToString("0.0", Inv) : "-").Append(" s")
              .Append(" | kontroli ").Append(_cvChecks).Append(", promieni ").Append(_cvRays).Append(" (zasloniete ").Append(_cvRayBlk).Append(", ponad budzet ").Append(_cvRayCap)
              .Append("), silnik: nie widzi ").Append(_cvEngNot).Append(", widzi ").Append(_cvEngClear)
              .Append(" | punkty: strona bez punktu ").Append(_cvNoPoint).Append(", punkt bez linii ").Append(_cvNoLos).Append(", grupa pusta ").Append(_cvNoMen)
              .Append(" | obchodzacy lewo ").Append(CvL.Count).Append(" / prawo ").Append(CvR.Count).Append(", szturm lewo ").Append(CvRL.Count).Append(" / prawo ").Append(CvRR.Count)
              .Append(" (z piechoty R2 ").Append(_cvConv).Append("), bez szturmu - przewaga ponizej ").Append(CovOdds.ToString("0", Inv)).Append(":1 ").Append(_cvNoOdds)
              .Append(" | linia odzyskana ").Append(_cvSight).Append(", na punkcie bez linii ").Append(_cvArriveBlind).Append(", ponowne obejscie ").Append(_cvReflank)
              .Append(" | po obejsciu: trafien nekajacego ").Append(_cvHitAfter).Append(", powalonych przez niego ").Append(_cvKillAfter).Append("; nekajacy trafiony ")
              .Append(Volatile.Read(ref _cvTgtHit)).Append(", padl ").Append(_cvTgtDown).Append(" (od obchodzacych / szturmu ").Append(_cvTgtDownCov).Append(')')
              .Append(" | koniec: ");
            for (int i = 0; i < CeKinds; i++) sb.Append(i > 0 ? ", " : "").Append(CeName[i]).Append(' ').Append(CeN[i]);
            sb.Append(" | zwolnien ludzi ").Append(_cvEnded).Append(", pozycji zdjetych ").Append(_cvRel)
              .Append(" | pominieci z szyku gracza ").Append(_cvPlayerSkip).Append(", ponad limit zapytan ").Append(_cvQOver)
              .Append(" | pod sterowaniem 311a na koniec ").Append(_cvLeft).Append(" (oczekiwane 0)")
              .Append(" | koszt sr. ").Append(_cvTickN > 0 ? (_cvTicks * 1000.0 / fr / _cvTickN).ToString("0.000", Inv) : "-")
              .Append(" / max ").Append(_cvTickN > 0 ? (_cvMaxTicks * 1000.0 / fr).ToString("0.000", Inv) : "-").Append(" ms (krokow ").Append(_cvTickN).Append(')')
              .Append(" | potkniecia ").Append(_cvStum)
              .Append(" | recenzja: bez ukrycia ").Append(_cvNoHide).Append(", ponad limit epizodow ").Append(_cvEpCap).Append(", szturm w przerwie ").Append(_cvFootCool)
              .Append(", odpowiedz wstrzymana - swoi przy celu ").Append(_cvRespHeld).Append(", rozrzut przy celu ").Append(_cvSpread)
              .Append(", R1 z linia zostal ").Append(_cvClearKeep).Append(", droga za dluga ").Append(_cvMoveOut).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
