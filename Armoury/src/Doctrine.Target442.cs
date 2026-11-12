using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 442 = PAKIET 4 krok 4.1 audytu AI bitwy (docs/audyty-2026-11-11/AUDYT-AI-BITWA.md rozdz. 3 "Jazda" 4.1, fakty techniczne 3; Jeff 11.11 "rob wszystko po kolei
    /// tak jak radzisz", decyzje AI 11.11: jazda rozbija lucznikow). Klucz CavalryTargetChoice ("Cavalry Target Choice", domyslnie TAK).
    ///
    /// Przyczyna [K] (rbmai RBMBehaviorCavalryCharge): w stanie Charging CalculateCurrentOrder wola CheckForNewChargeTarget = FindSignificantEnemy(piechota + strzelcy)
    ///   - najblizszy duzy szyk, bez oceny (strzelcy odslonieci, szyk zwiazany walka, skrzydlo, szyk, ktory sie zaprze - wszystko jedno). Fakt 3: jezdziec dalej niz
    ///   60 m od celu jedzie na najblizszego pieszego z KAZDEGO duzego szyku (Frontline PrefixGetOrderPositionOfUnit, galaz jazdy -> Utilities.GetCorrectTarget),
    ///   wiec rozkaz "szarza na lucznikow" konczy sie w piechocie, ktora stoi przed nimi.
    /// Wybor (glowny watek, OnMissionTick co 1 s, Doctrine.Tick): dla kazdego szyku jazdy AI (nie konni strzelcy, nie szyk gracza, nie szyk w roli 441) ocena
    ///   kazdego pieszego szyku wroga (piechota albo strzelcy, >= T442MinMen; jazda wroga poza zakresem jak RBM ChargeCavalry NIE):
    ///   strzelcy odslonieci (zaden szyk piechoty wroga nie zaslania odcinka jazda - strzelcy, jak miara 437) 3.0; strzelcy zasloniecie 0.1 (442-p: ponizej
    ///   kazdej piechoty, takze "zaprze sie" 0.2 - decyzja Jeffa 11.11: gdy nie ma drogi do lucznikow, jazda ich nie szarzuje; schowanie robi 443);
    ///   piechota skrzydlowa (nie najwiekszy szyk piechoty swojej druzyny, przesuniety w bok od srodka o > pol szerokosci srodka) 1.2;
    ///   piechota zasloniecie innym szykiem piechoty (np. odwod za srodkiem) 0.1; piechota srodkowa (i nie-boczna, niezasloniecie) 1.0;
    ///   x1.5 szyk zwiazany walka (>= 50% ludzi z ciosem wrecz zadanym / otrzymanym w 6 s - miara RBM FormationFightingInMelee);
    ///   x0.2 szyk, ktory sie zaprze: piechota srodkowa niezwiazana, a moja jazda blizej niej niz moja najblizsza piechota;
    ///   x 1 / (1 + odleglosc / 250 m). Histereza: nowy cel tylko przy ocenie > 1.3 x biezacego; jazda sama w zwarciu (>= 30% wrecz) - cel zamrozony.
    ///   Cel i kotwice (do 5 ludzi celu rozlozonych wzdluz frontu) w tablicach slotu [TeamIndex x 10 + FormationIndex] - podmiana referencji (volatile).
    /// Podmiana celu szyku: postfiks (Priority.Last, after com.rbmai) na WIRTUALNYM RBMBehaviorCavalryCharge.CalculateCurrentOrder (DeclaredMethod - nadpisana w
    ///   RBMBehaviorCavalryCharge; NIE na CheckForNewChargeTarget - mala metoda publiczna, ryzyko wklejenia przez JIT). Gdy rozkaz zachowania to ChargeWithTarget
    ///   na inny szyk niz nasz cel: _lastTarget, kierunek szarzy, rozkaz ChargeToTarget(cel), patrzenie w strone celu, zegar 3 s przejazdu zerowany. RBM dalej
    ///   prowadzi cykl Charging / ChargingPast / Reforming na NASZ cel; przy kazdym powrocie w Charging jego CheckForNewChargeTarget wybiera od nowa - postfiks
    ///   podmienia znowu (licznik "cel podmieniony").
    /// Pojedynczy jezdziec (fakt 3): postfiks RBMAI.Utilities.GetCorrectTarget (DeclaredMethod, static): jezdziec szyku jazdy z naszym celem w rozkazie
    ///   ChargeWithTarget, ktoremu RBM dal czlowieka z INNEGO szyku (dalej niz 15 m - kto juz walczy obok, zostaje przy swoim) - dostaje najblizsza kotwice celu;
    ///   cel zasloniety (strzelcy albo piechota za innym szykiem) - bez kotwic (442-p: kotwica dalej niz 60 m = jazda prosto przez zaslaniajaca piechote);
    ///   jezdziec poscigu 148 i jazdy na strzelca 204 (HorsePursuit.RidingAnyThread / HuntAnyThread) - bez zmiany (jeden wlasciciel jezdzca).
    ///   RBM kieruje go w nia, gdy jest dalej niz 60 m; blizej - jak dotad (gra i RBM).
    /// Watki: oba postfiksy biegna w watku silnika / AI - tylko odczyt tablic slotu (volatile), zapis wlasnych pol zachowania RBM (jak RBM w tej samej metodzie)
    ///   i Interlocked. Skan szykow, ocena, kotwice - tylko glowny watek. Wyjatki per szyk liczone (potkniecia), bez globalnego wylacznika.
    /// Klucz NIE: wybor liczony (glowny watek, tylko odczyt), postfiksy nic nie zmieniaja - licznik "bylby podmieniony"; zachowanie co do znaku jak dotad.
    /// Pomiar: szyk ofiary pierwszego ciosu - linia 437 "pierwszy cios szarzy" (ofiara, "cel RBM" = nasz cel po podmianie); linia "Cel szarzy (442)" na koncu bitwy.
    /// 442 wycina takze martwe S3 (Doctrine.cs: postfiks Attack - BehaviorFlank konnicy skrzydel i ChargeInfantry=false; audyt: 0 wejsc w flanke i 0 "najpierw
    ///   lucznicy" w 21 z 21 bitew) - to samo zjawisko (cel jazdy) ma jedna regule.
    /// </summary>
    internal static partial class Doctrine
    {
        internal const float T442Every = 1f, T442Exposed = 3.0f, T442Covered = 0.1f, T442Wing = 1.2f, T442Centre = 1.0f, T442Bound = 1.5f, T442Brace = 0.2f;
        internal const float T442BoundShare = 0.5f, T442OwnMelee = 0.3f, T442MeleeWin = 6f, T442DistRef = 250f, T442Hyst = 1.3f, T442BlockPad = 10f;
        internal const float T442RiderKeep = 15f, T442WingSide = 0.5f;
        internal const int T442MinMen = 8, T442MinCav = 5, T442Anchors = 5, T442BlockMen = 10, T442SeenMax = 8192;
        // 442-p: klasa celu osobno od mnoznika (zwiazany walka / zaprze sie) - dawniej TcBound nadpisywal klase i rozklad zanizal strzelcow
        private const int TcExp = 0, TcCov = 1, TcWing = 2, TcCentre = 3, TcCovInf = 4, TcK = 5;
        private const int TmNone = 0, TmBound = 1, TmBrace = 2, TmK = 3;
        private static readonly string[] TcName = { "strzelcy odslonieci", "strzelcy zasloniecie", "skrzydlo piechoty", "srodek piechoty", "piechota zasloniecie" };
        private static readonly string[] TmName = { "bez mnoznika", "zwiazany walka x1.5", "zaprze sie x0.2" };
        private static readonly IFormatProvider T4Inv = CultureInfo.InvariantCulture;

        // ---- stan czytany z watkow silnika (pisze glowny watek)
        private static volatile bool _t4Live, _t4Key;
        private static readonly Formation[] _t4Who = new Formation[CrSlots];
        private static readonly Formation[] _t4Tgt = new Formation[CrSlots];
        private static readonly Agent[][] _t4Anc = new Agent[CrSlots][];
        // ---- tylko postfiks szyku (watek AI): ostatnia para (cel RBM, cel 442) policzona przy kluczu NIE - licznik zdarzen, nie wywolan
        private static readonly Formation[] _t4SeenRbm = new Formation[CrSlots];
        private static readonly Formation[] _t4SeenTg = new Formation[CrSlots];
        // ---- tylko postfiks jezdzca (watek silnika): bity "juz policzony w tej bitwie" per Agent.Index - liczniki jezdzcow, nie wywolan
        private const int T4bAim = 1, T4bDry = 2, T4bKeep = 4, T4bOwned = 8;
        private static readonly int[] _t4Seen = new int[T442SeenMax];
        // ---- tylko glowny watek
        private static readonly float[] _t4Score = new float[CrSlots];
        private static readonly byte[] _t4Cat = new byte[CrSlots];
        private static float _t4Acc;

        // ---- latki (raz na proces)
        private static string _t4Patch = "nie wpiete";
        private static Type _t4Cav;
        private static FieldInfo _t4fLast, _t4fDir, _t4fTimer;

        // ---- liczniki misji (T442Reset w Battle291.Begin); z watkow silnika - Interlocked
        private static int _t4Swap, _t4SwapIdle, _t4Dry, _t4SwapErr, _t4Aim, _t4AimDry, _t4AimKeep, _t4AimOwned, _t4PostErr;
        private static int _t4Pick, _t4Change, _t4Held, _t4Frozen, _t4Stum, _t4NoCand, _t4NoAnc;
        private static readonly int[] _t4CatN = new int[TcK];
        private static readonly int[] _t4ModN = new int[TmK];
        private static float _t4CavSec, _t4ChargeSec;

        // ---- skan jednego taktu (glowny watek)
        private static readonly Dictionary<Formation, float> _t4Melee = new Dictionary<Formation, float>();
        private static readonly Dictionary<Formation, Agent[]> _t4AncTick = new Dictionary<Formation, Agent[]>();
        private static readonly Formation[] _t4MainInf = new Formation[MaxTeams];
        private static float _t4Now;
        private static int _t4All, _t4In;

        /// <summary>Battle291.Begin (AfterStart): liczniki linii 442 od zera.</summary>
        internal static void T442Reset()
        {
            Interlocked.Exchange(ref _t4Swap, 0); Interlocked.Exchange(ref _t4SwapIdle, 0); Interlocked.Exchange(ref _t4Dry, 0); Interlocked.Exchange(ref _t4SwapErr, 0);
            Interlocked.Exchange(ref _t4Aim, 0); Interlocked.Exchange(ref _t4AimDry, 0); Interlocked.Exchange(ref _t4AimKeep, 0); Interlocked.Exchange(ref _t4AimOwned, 0); Interlocked.Exchange(ref _t4PostErr, 0);
            _t4Pick = _t4Change = _t4Held = _t4Frozen = _t4Stum = _t4NoCand = _t4NoAnc = 0;
            for (int k = 0; k < TcK; k++) _t4CatN[k] = 0;
            for (int k = 0; k < TmK; k++) _t4ModN[k] = 0;
            Array.Clear(_t4Seen, 0, _t4Seen.Length);   // Agent.Index liczony od nowa w kazdej misji
            _t4CavSec = _t4ChargeSec = 0f;
        }

        /// <summary>Doctrine.Begin (AfterStart, po Clear): klucz misji - bitwa polowa, nie morska. Wybor liczony takze przy kluczu NIE (pomiar "bylby").</summary>
        private static void T442Begin(Mission m)
        {
            var c = Settings.Current;
            bool field = false;
            try { field = m != null && m.IsFieldBattle && !m.IsNavalBattle; } catch { field = false; }
            _t4Acc = 0f;
            _t4Key = c != null && c.CavalryTargetChoice;
            _t4Live = field && _t4Cav != null;
        }

        /// <summary>Doctrine.Clear (Begin / End): sloty od zera. Najpierw klucz - watki silnika przestaja czytac sloty.</summary>
        private static void T442ClearMission()
        {
            _t4Live = false;
            for (int i = 0; i < CrSlots; i++) T442Drop(i);
            for (int i = 0; i < MaxTeams; i++) _t4MainInf[i] = null;
            _t4Melee.Clear(); _t4AncTick.Clear();
            _t4Acc = 0f;
        }

        private static void T442Drop(int idx)
        {
            Volatile.Write(ref _t4Tgt[idx], null);
            Volatile.Write(ref _t4Anc[idx], null);
            Volatile.Write(ref _t4Who[idx], null);
            Volatile.Write(ref _t4SeenRbm[idx], null);
            Volatile.Write(ref _t4SeenTg[idx], null);
            _t4Score[idx] = 0f; _t4Cat[idx] = 0;
        }

        // ------------------------------------------------------------ latki (raz na proces - ApplyMissionPatches)

        private static string T442ApplyPatch(Harmony h)
        {
            var sb = new StringBuilder(120);
            try
            {
                try { _t4Cav = AccessTools.TypeByName("RBMBehaviorCavalryCharge") ?? AccessTools.TypeByName("RBMAI.RBMBehaviorCavalryCharge"); } catch { _t4Cav = null; }
                if (_t4Cav != null && !typeof(BehaviorComponent).IsAssignableFrom(_t4Cav)) _t4Cav = null;
                if (_t4Cav == null) return _t4Patch = "NIE (brak RBMBehaviorCavalryCharge)";
                try { _t4fLast = AccessTools.DeclaredField(_t4Cav, "_lastTarget"); } catch { _t4fLast = null; }
                try { _t4fDir = AccessTools.DeclaredField(_t4Cav, "_initialChargeDirection"); } catch { _t4fDir = null; }
                try { _t4fTimer = AccessTools.DeclaredField(_t4Cav, "_chargeTimer"); } catch { _t4fTimer = null; }
                if (_t4fLast != null && _t4fLast.FieldType != typeof(FormationQuerySystem)) _t4fLast = null;
                if (_t4fDir != null && _t4fDir.FieldType != typeof(Vec2)) _t4fDir = null;
                if (_t4fTimer != null && _t4fTimer.FieldType.IsValueType) _t4fTimer = null;
                int ok = 0;
                // szyk: wirtualna CalculateCurrentOrder zadeklarowana w RBMBehaviorCavalryCharge (protected override)
                var t1 = AccessTools.DeclaredMethod(_t4Cav, "CalculateCurrentOrder", Type.EmptyTypes);
                if (t1 == null || t1.ReturnType != typeof(void) || _t4fLast == null) sb.Append(" CalculateCurrentOrder(brak").Append(_t4fLast == null ? " _lastTarget" : "").Append(')');
                else if (T442PatchOne(h, t1, nameof(T442ChargePost))) ok++;
                else sb.Append(" CalculateCurrentOrder(polknieta)");
                // jezdziec: static RBMAI.Utilities.GetCorrectTarget(Agent) -> Agent
                Type tu = null;
                try { tu = AccessTools.TypeByName("RBMAI.Utilities"); } catch { tu = null; }
                var t2 = tu != null ? AccessTools.DeclaredMethod(tu, "GetCorrectTarget", new[] { typeof(Agent) }) : null;
                if (t2 == null || t2.ReturnType != typeof(Agent) || !t2.IsStatic) sb.Append(" GetCorrectTarget(brak)");
                else if (T442PatchOne(h, t2, nameof(T442AimPost))) ok++;
                else sb.Append(" GetCorrectTarget(polknieta)");
                return _t4Patch = "cel szarzy " + ok + "/2" + (sb.Length > 0 ? " (NIE:" + sb + ")" : "");
            }
            catch (Exception e) { return _t4Patch = "NIE (" + e.GetType().Name + ")"; }
        }

        private static bool T442PatchOne(Harmony h, MethodInfo target, string postName)
        {
            try
            {
                h.Patch(target, postfix: new HarmonyMethod(typeof(Doctrine), postName) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                var info = Harmony.GetPatchInfo(target);
                if (info != null) foreach (var p in info.Postfixes) if (p != null && p.owner == h.Id && p.PatchMethod != null && p.PatchMethod.Name == postName) return true;
                return false;
            }
            catch (Exception e)
            {
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Latka442 " + target.DeclaringType.Name + "." + target.Name, e);
                return false;
            }
        }

        private static int T442Idx(Formation f)
        {
            var t = f != null ? f.Team : null;
            if (t == null) return -1;
            int ti = t.TeamIndex, fi = (int)f.FormationIndex;
            if (ti < 0 || ti >= MaxTeams || fi < 0 || fi >= CrRegular) return -1;
            return ti * 10 + fi;
        }

        // ------------------------------------------------------------ postfiks RBMBehaviorCavalryCharge.CalculateCurrentOrder (watek AI / silnika)

        /// <summary>Rozkaz ChargeWithTarget na inny szyk niz cel 442 slotu - podmiana celu w polach zachowania RBM. Bez alokacji poza rzadka podmiana (boks Vec2).</summary>
        private static void T442ChargePost(BehaviorComponent __instance, ref MovementOrder ____currentOrder, ref bool ___IsCurrentOrderChanged, ref FacingOrder ___CurrentFacingOrder)
        {
            if (!_t4Live || __instance == null) return;
            try
            {
                if (____currentOrder.OrderType != OrderType.ChargeWithTarget) return;
                var f = __instance.Formation;
                int idx = T442Idx(f);
                if (idx < 0 || !ReferenceEquals(Volatile.Read(ref _t4Who[idx]), f)) return;
                var tg = Volatile.Read(ref _t4Tgt[idx]);
                if (tg == null || tg.CountOfUnits <= 0 || ReferenceEquals(____currentOrder.TargetFormation, tg)) return;
                if (!MissionTeams.Enemies(f.Team, tg.Team)) return;
                // 442-p: liczone tylko dla AKTYWNEGO zachowania (FindBestBehavior wola PrecalculateMovementOrder -> CalculateCurrentOrder takze na nieaktywnym)
                var fai = f.AI;
                bool active = fai != null && ReferenceEquals(fai.ActiveBehavior, __instance);
                if (!_t4Key)
                {
                    // 442-p: zdarzenie, nie wywolanie - RBM zostawia ChargeToTarget(stary cel) przez caly Charging, metoda biegnie >= 2 razy na takt AI
                    if (!active) return;
                    var rt = ____currentOrder.TargetFormation;
                    if (!ReferenceEquals(Volatile.Read(ref _t4SeenRbm[idx]), rt) || !ReferenceEquals(Volatile.Read(ref _t4SeenTg[idx]), tg))
                    {
                        Volatile.Write(ref _t4SeenRbm[idx], rt);
                        Volatile.Write(ref _t4SeenTg[idx], tg);
                        Interlocked.Increment(ref _t4Dry);
                    }
                    return;
                }
                var tq = tg.QuerySystem;
                if (tq == null) return;
                Vec2 d = tg.CachedMedianPosition.AsVec2 - f.CachedAveragePosition;
                if (!d.IsValid || d.LengthSquared < 0.01f) return;
                _t4fLast.SetValue(__instance, tq);
                if (_t4fDir != null) _t4fDir.SetValue(__instance, d);
                if (_t4fTimer != null) _t4fTimer.SetValue(__instance, null);   // zegar 3 s przejazdu starego celu nie konczy szarzy na nowy
                ____currentOrder = MovementOrder.MovementOrderChargeToTarget(tg);
                ___IsCurrentOrderChanged = true;
                ___CurrentFacingOrder = FacingOrder.FacingOrderLookAtDirection(d.Normalized());
                if (active)
                {
                    // 442-p: front na szerokosc NASZEGO celu - RBM w tej samej metodzie (stan Charging) ustawil go wedlug swojego celu; ten sam watek i wzor co RBM
                    f.SetFormOrder(FormOrder.FormOrderCustom(Math.Max(1f, tg.Width)));
                    Interlocked.Increment(ref _t4Swap);
                }
                else Interlocked.Increment(ref _t4SwapIdle);
            }
            catch { Interlocked.Increment(ref _t4SwapErr); }
        }

        // ------------------------------------------------------------ postfiks RBMAI.Utilities.GetCorrectTarget (watek silnika)

        /// <summary>Jezdziec szyku z celem 442 w rozkazie, ktoremu RBM dal czlowieka z innego szyku dalej niz 15 m - najblizsza kotwica celu. Bez alokacji.</summary>
        private static void T442AimPost(Agent agent, ref Agent __result)
        {
            if (!_t4Live || agent == null) return;
            try
            {
                var f = agent.Formation;
                if (f == null) return;
                int idx = T442Idx(f);
                if (idx < 0 || !ReferenceEquals(Volatile.Read(ref _t4Who[idx]), f)) return;
                var tg = Volatile.Read(ref _t4Tgt[idx]);
                if (tg == null) return;
                var r = __result;
                if (r != null && ReferenceEquals(r.Formation, tg)) return;
                // jeden wlasciciel jezdzca: poscig 148 i jazda na strzelca 204 steruja nim same (tablice dowolnego watku) - bez zmiany
                if (HorsePursuit.RidingAnyThread(agent) || HorsePursuit.HuntAnyThread(agent)) { T442SeenOnce(agent, T4bOwned, ref _t4AimOwned); return; }
                var o = f.GetReadonlyMovementOrderReference();
                if (o.OrderType != OrderType.ChargeWithTarget) return;
                // klucz NIE: rozkaz szyku to cel RBM (postfiks szyku nie podmienia) - "bylby" liczy jezdzcow, ktorych 442 by przekierowal
                if (_t4Key && !ReferenceEquals(o.TargetFormation, tg)) return;
                Vec2 p = agent.Position.AsVec2;
                if (r != null && p.DistanceSquared(r.Position.AsVec2) < T442RiderKeep * T442RiderKeep) { T442SeenOnce(agent, T4bKeep, ref _t4AimKeep); return; }
                var anc = Volatile.Read(ref _t4Anc[idx]);
                if (anc == null) return;
                Agent best = null;
                float bd = float.MaxValue;
                for (int i = 0; i < anc.Length; i++)
                {
                    var a = anc[i];
                    if (a == null || !a.IsActive() || !ReferenceEquals(a.Formation, tg)) continue;
                    float dd = p.DistanceSquared(a.Position.AsVec2);
                    if (dd < bd) { bd = dd; best = a; }
                }
                if (best == null) return;
                if (!_t4Key) { T442SeenOnce(agent, T4bDry, ref _t4AimDry); return; }
                __result = best;
                T442SeenOnce(agent, T4bAim, ref _t4Aim);
            }
            catch { Interlocked.Increment(ref _t4PostErr); }
        }

        /// <summary>442-p: licznik jezdzcow (raz na jezdzca na bitwe), nie wywolan - na goracej sciezce zwykle tylko odczyt wlasnej komorki.</summary>
        private static void T442SeenOnce(Agent a, int bit, ref int counter)
        {
            int i = a.Index;
            if (i < 0 || i >= T442SeenMax) return;
            int v = Volatile.Read(ref _t4Seen[i]);
            while ((v & bit) == 0)
            {
                int w = Interlocked.CompareExchange(ref _t4Seen[i], v | bit, v);
                if (w == v) { Interlocked.Increment(ref counter); return; }
                v = w;
            }
        }

        // ------------------------------------------------------------ wybor celu (glowny watek, Doctrine.Tick co 1 s)

        internal static void T442Tick(Mission m, float dt)
        {
            if (!_t4Live || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m) || m.MissionEnded) return;
            _t4Acc += dt;
            if (_t4Acc < T442Every) return;
            float step = _t4Acc;
            _t4Acc = 0f;
            bool dep;
            try { dep = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && m.CurrentTime >= P440NoDeploy); } catch { dep = false; }
            if (!dep) return;
            var c = Settings.Current;
            _t4Key = c != null && c.CavalryTargetChoice;
            _t4Now = m.CurrentTime;
            _t4Melee.Clear();
            _t4AncTick.Clear();
            var teams = m.Teams;
            int n = teams != null ? teams.Count : 0;
            // glowna (najwieksza) piechota kazdej druzyny - "srodek"; reszta piechoty - skrzydlo
            for (int i = 0; i < MaxTeams; i++) _t4MainInf[i] = null;
            for (int i = 0; i < n; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!MissionTeams.Valid(t)) continue;
                int ti = t.TeamIndex;
                if (ti < 0 || ti >= MaxTeams) continue;
                try
                {
                    Formation best = null;
                    var fl = t.FormationsIncludingSpecialAndEmpty;
                    for (int j = 0; fl != null && j < fl.Count; j++)
                    {
                        var f = fl[j];
                        if (f == null || f.CountOfUnits < T442MinMen) continue;
                        var q = f.QuerySystem;
                        if (q == null || !q.IsInfantryFormation) continue;
                        if (best == null || f.CountOfUnits > best.CountOfUnits) best = f;
                    }
                    _t4MainInf[ti] = best;
                }
                catch (Exception e) { _t4Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Target442 srodek", e); }
            }
            for (int i = 0; i < n; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!MissionTeams.Valid(t)) continue;
                if (t.Side != BattleSideEnum.Attacker && t.Side != BattleSideEnum.Defender) continue;
                int ti = t.TeamIndex;
                if (ti < 0 || ti >= MaxTeams) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    Formation f = null;
                    try { f = fl[j]; } catch { f = null; }
                    if (f == null) continue;
                    int idx = T442Idx(f);
                    if (idx < 0) continue;
                    try { T442Form(m, t, f, idx, step); }
                    catch (Exception e) { _t4Stum++; T442Drop(idx); RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Target442 szyk", e); }
                }
            }
        }

        private static void T442Form(Mission m, Team t, Formation f, int idx, float step)
        {
            // 443: szyk w roli RAID w fazie szarzy - slot z celem rajdu (postfiks jezdzca GetCorrectTarget trzyma jezdzcow na strzelcach, fakt 3); ocena 442 i liczniki
            // 442 pominiete (jeden wlasciciel - rola). Inny szyk w roli (HIDE, RAID w dojezdzie z boku) - slot pusty jak w 442.
            if (C441Owned(f))
            {
                var rt = R443AimTarget(f, idx) ?? K444AimTarget(f, idx) ?? V445AimTarget(f, idx) ?? F460AimTarget(f, idx);   // 444: szyk w STRIKE w fazie szarzy - cel uderzenia; 445: cel CAVFIRST; 460: scigajacy po zawrocie
                if (rt == null) { if (Volatile.Read(ref _t4Who[idx]) != null) T442Drop(idx); return; }
                if (!ReferenceEquals(Volatile.Read(ref _t4Who[idx]), f)) T442Drop(idx);
                Volatile.Write(ref _t4Who[idx], f);
                T442SetAnchors(idx, rt);
                Volatile.Write(ref _t4Tgt[idx], rt);
                return;
            }
            var q = f.QuerySystem;
            bool cav = f.CountOfUnits >= T442MinCav && q != null && q.IsCavalryFormation && !q.IsRangedCavalryFormation && f.IsAIControlled
                       && !HorsePursuit.PlayerForm(f) && !f.HasPlayerControlledTroop && !C441Owned(f);
            if (!cav) { if (Volatile.Read(ref _t4Who[idx]) != null) T442Drop(idx); return; }
            if (!ReferenceEquals(Volatile.Read(ref _t4Who[idx]), f)) T442Drop(idx);
            _t4CavSec += step;
            var ai = f.AI;
            var act = ai != null ? ai.ActiveBehavior : null;
            if (act != null && act.GetType() == _t4Cav) _t4ChargeSec += step;
            Vec2 me = f.CachedAveragePosition;
            if (!me.IsValid) return;
            var cur = Volatile.Read(ref _t4Tgt[idx]);
            bool curOk = cur != null && cur.CountOfUnits > 0 && MissionTeams.Enemies(t, cur.Team);
            // jazda sama w zwarciu - cel zamrozony (nie odrywamy szyku od walki)
            if (curOk && T442MeleeShare(f) >= T442OwnMelee)
            {
                _t4Frozen++;
                T442Anchor(idx, cur, _t4Cat[idx]);
                return;
            }
            // najblizsza wlasna piechota (do "zaprze sie")
            Formation best = null;
            float bs = 0f, curScore = -1f;
            byte bc = 0, bm = 0, curCat = 0;
            var teams = m.Teams;
            for (int i = 0; i < teams.Count; i++)
            {
                var et = teams[i];
                if (!MissionTeams.Enemies(t, et)) continue;
                int eti = et.TeamIndex;
                var main = eti >= 0 && eti < MaxTeams ? _t4MainInf[eti] : null;
                var efl = et.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; efl != null && j < efl.Count; j++)
                {
                    var e = efl[j];
                    if (e == null || e.CountOfUnits < T442MinMen) continue;
                    var eq = e.QuerySystem;
                    if (eq == null || eq.IsCavalryFormation || eq.IsRangedCavalryFormation) continue;
                    bool ranged = eq.IsRangedFormation, inf = eq.IsInfantryFormation;
                    if (!ranged && !inf) continue;
                    Vec2 ep = e.CachedMedianPosition.AsVec2;
                    if (!ep.IsValid) continue;
                    float d = me.Distance(ep);
                    byte cat, mod = TmNone;
                    float s;
                    if (ranged)
                    {
                        bool blocked = T442Blocked(t, e, me, ep);
                        cat = blocked ? (byte)TcCov : (byte)TcExp;
                        s = blocked ? T442Covered : T442Exposed;
                    }
                    else if (ReferenceEquals(e, main) || main == null) { cat = TcCentre; s = T442Centre; }
                    else if (T442Blocked(t, e, me, ep)) { cat = TcCovInf; s = T442Covered; }   // 442-p: np. odwod za srodkiem - bez premii
                    else if (T442Side(main, ep)) { cat = TcWing; s = T442Wing; }               // 442-p: skrzydlo = boczne przesuniecie wzgledem srodka
                    else { cat = TcCentre; s = T442Centre; }
                    float ms = T442MeleeShareCached(e);
                    if (ms >= T442BoundShare) { s *= T442Bound; mod = TmBound; }
                    else if (cat == TcCentre && T442CavCloser(t, f, me, ep, d)) { s *= T442Brace; mod = TmBrace; }
                    s /= 1f + d / T442DistRef;
                    if (ReferenceEquals(e, cur)) { curScore = s; curCat = cat; }
                    if (best == null || s > bs) { best = e; bs = s; bc = cat; bm = mod; }
                }
            }
            if (best == null) { _t4NoCand++; if (cur != null) T442Drop(idx); Volatile.Write(ref _t4Who[idx], f); return; }
            if (curOk && !ReferenceEquals(best, cur) && curScore >= 0f && bs < curScore * T442Hyst)
            {
                _t4Held++;
                _t4Score[idx] = curScore;
                _t4Cat[idx] = curCat;
                T442Anchor(idx, cur, curCat);
                return;
            }
            if (!ReferenceEquals(best, cur))
            {
                if (cur == null) _t4Pick++; else _t4Change++;
                if (bc < TcK) _t4CatN[bc]++;
                if (bm < TmK) _t4ModN[bm]++;
            }
            _t4Cat[idx] = bc;
            _t4Score[idx] = bs;
            Volatile.Write(ref _t4Who[idx], f);
            T442Anchor(idx, best, bc);   // kotwice przed celem - postfiks jezdzca czyta cel, potem kotwice
            Volatile.Write(ref _t4Tgt[idx], best);
        }

        /// <summary>442-p: cel zasloniety (strzelcy albo piechota za innym szykiem) - bez kotwic; RBM kierowalby jezdzca dalej niz 60 m prosto do kotwicy,
        /// czyli przez zaslaniajaca piechote. Postfiks jezdzca przy braku kotwic zostawia wybor RBM.</summary>
        private static void T442Anchor(int idx, Formation tg, byte cat)
        {
            if (cat == TcCov || cat == TcCovInf)
            {
                if (Volatile.Read(ref _t4Anc[idx]) != null) _t4NoAnc++;
                Volatile.Write(ref _t4Anc[idx], null);
                return;
            }
            T442SetAnchors(idx, tg);
        }

        /// <summary>442-p: szyk piechoty przesuniety w bok od glownej piechoty o wiecej niz pol jej szerokosci (rzut na prawy wektor srodka).</summary>
        private static bool T442Side(Formation main, Vec2 ep)
        {
            Vec2 mc = main.CachedMedianPosition.AsVec2, dir = main.Direction;
            if (!mc.IsValid || !dir.IsValid || dir.LengthSquared < 0.0001f) return false;
            Vec2 r = dir.Normalized().RightVec(), v = ep - mc;
            return Math.Abs(v.x * r.x + v.y * r.y) > T442WingSide * Math.Max(1f, main.Width);
        }

        /// <summary>Do 5 ludzi celu rozlozonych wzdluz jego frontu (najblizszy srodkowi kazdego z pieciu pasow) - raz na takt na cel.</summary>
        private static void T442SetAnchors(int idx, Formation tg)
        {
            Agent[] anc;
            if (!_t4AncTick.TryGetValue(tg, out anc))
            {
                anc = new Agent[T442Anchors];
                var bd = new float[T442Anchors];
                for (int k = 0; k < T442Anchors; k++) bd[k] = float.MaxValue;
                Vec2 c = tg.CachedMedianPosition.AsVec2, dir = tg.Direction;
                float wdt = Math.Max(1f, tg.Width);
                if (c.IsValid && dir.IsValid && dir.LengthSquared > 0.0001f)
                {
                    Vec2 right = dir.Normalized().RightVec();
                    _t4AncC = c; _t4AncR = right; _t4AncW = wdt; _t4AncA = anc; _t4AncD = bd;
                    tg.ApplyActionOnEachUnit(T442AncMan);
                    _t4AncA = null; _t4AncD = null;
                }
                _t4AncTick[tg] = anc;
            }
            Volatile.Write(ref _t4Anc[idx], anc);
        }

        private static Vec2 _t4AncC, _t4AncR;
        private static float _t4AncW;
        private static Agent[] _t4AncA;
        private static float[] _t4AncD;

        private static readonly Action<Agent> T442AncMan = a =>
        {
            if (a == null || !a.IsActive()) return;
            Vec2 p = a.Position.AsVec2 - _t4AncC;
            float lat = p.x * _t4AncR.x + p.y * _t4AncR.y;
            float seg = _t4AncW / T442Anchors;
            for (int k = 0; k < T442Anchors; k++)
            {
                float mid = -0.5f * _t4AncW + (k + 0.5f) * seg;
                float dd = Math.Abs(lat - mid);
                if (dd < _t4AncD[k]) { _t4AncD[k] = dd; _t4AncA[k] = a; }
            }
        };

        /// <summary>Czy szyk piechoty wroga (>= 10 ludzi, inny niz cel) zaslania odcinek jazda - cel (pol szerokosci + 10 m, jak miara 437).</summary>
        private static bool T442Blocked(Team mine, Formation tg, Vec2 a, Vec2 b)
        {
            var et = tg.Team;
            var fl = et != null ? et.FormationsIncludingSpecialAndEmpty : null;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                var x = fl[j];
                if (x == null || ReferenceEquals(x, tg) || x.CountOfUnits < T442BlockMen) continue;
                var xq = x.QuerySystem;
                if (xq == null || !xq.IsInfantryFormation) continue;
                Vec2 xp = x.CachedMedianPosition.AsVec2;
                if (!xp.IsValid) continue;
                if (SegDist442(xp, a, b) < 0.5f * x.Width + T442BlockPad) return true;
            }
            return false;
        }

        private static float SegDist442(Vec2 p, Vec2 a, Vec2 b)
        {
            float vx = b.x - a.x, vy = b.y - a.y, wx = p.x - a.x, wy = p.y - a.y;
            float l2 = vx * vx + vy * vy;
            float k = l2 > 0.0001f ? (wx * vx + wy * vy) / l2 : 0f;
            if (k < 0f) k = 0f; else if (k > 1f) k = 1f;
            float dx = a.x + k * vx - p.x, dy = a.y + k * vy - p.y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>"Zaprze sie": moja jazda blizej celu niz moja najblizsza piechota (albo nie mam piechoty).</summary>
        private static bool T442CavCloser(Team t, Formation cav, Vec2 me, Vec2 ep, float dCav)
        {
            float bestInf = float.MaxValue;
            var fl = t.FormationsIncludingSpecialAndEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                var x = fl[j];
                if (x == null || ReferenceEquals(x, cav) || x.CountOfUnits < T442MinMen) continue;
                var xq = x.QuerySystem;
                if (xq == null || !xq.IsInfantryFormation) continue;
                Vec2 xp = x.CachedMedianPosition.AsVec2;
                if (!xp.IsValid) continue;
                float d = xp.Distance(ep);
                if (d < bestInf) bestInf = d;
            }
            return dCav < bestInf;
        }

        private static float T442MeleeShareCached(Formation f)
        {
            float v;
            if (_t4Melee.TryGetValue(f, out v)) return v;
            v = T442MeleeShare(f);
            _t4Melee[f] = v;
            return v;
        }

        /// <summary>Udzial ludzi z ciosem wrecz zadanym / otrzymanym w ostatnich 6 s (miara RBM FormationFightingInMelee).</summary>
        private static float T442MeleeShare(Formation f)
        {
            _t4All = _t4In = 0;
            f.ApplyActionOnEachUnit(T442MeleeMan);
            return _t4All > 0 ? (float)_t4In / _t4All : 0f;
        }

        private static readonly Action<Agent> T442MeleeMan = a =>
        {
            if (a == null || !a.IsActive()) return;
            _t4All++;
            if (_t4Now - a.LastMeleeAttackTime < T442MeleeWin || _t4Now - a.LastMeleeHitTime < T442MeleeWin) _t4In++;
        };

        // ------------------------------------------------------------ linia konca bitwy (Battle291.Write)

        internal static string T442Line(string kind)
        {
            var c = Settings.Current;
            var sb = new StringBuilder(800);
            sb.Append("Cel szarzy (442): misja ").Append(kind ?? "-")
              .Append(" | klucz wybor celu jazdy ").Append(c != null && c.CavalryTargetChoice ? "TAK" : "NIE")
              .Append(" | jazda AI ").Append(_t4CavSec.ToString("0", T4Inv)).Append(" szyko-s (w szarzy RBM ").Append(_t4ChargeSec.ToString("0", T4Inv)).Append(')')
              .Append(" | wybor: pierwszy cel ").Append(_t4Pick).Append(", zmiana celu ").Append(_t4Change).Append(", trzymany histereza x")
              .Append(T442Hyst.ToString("0.0", T4Inv)).Append(' ').Append(_t4Held).Append(" szyko-s, zamrozony w zwarciu jazdy ").Append(_t4Frozen)
              .Append(" szyko-s, bez kandydata ").Append(_t4NoCand).Append(" szyko-s | wybrany cel wedlug klasy:");
            for (int k = 0; k < TcK; k++) sb.Append(k > 0 ? "," : "").Append(' ').Append(TcName[k]).Append(' ').Append(_t4CatN[k]);
            sb.Append(" | mnoznik wybranego celu:");
            for (int k = 0; k < TmK; k++) sb.Append(k > 0 ? "," : "").Append(' ').Append(TmName[k]).Append(' ').Append(_t4ModN[k]);
            sb.Append(" | kotwice zdjete (cel zasloniety) ").Append(_t4NoAnc)
              .Append(" | cel podmieniony w aktywnej szarzy ").Append(Volatile.Read(ref _t4Swap)).Append(" (w nieaktywnym zachowaniu ").Append(Volatile.Read(ref _t4SwapIdle))
              .Append("; bylby, klucz NIE - zmiana pary cel RBM / cel 442 ").Append(Volatile.Read(ref _t4Dry))
              .Append(", bledy ").Append(Volatile.Read(ref _t4SwapErr)).Append(')')
              .Append(" | jezdzcy skierowani na cel ").Append(Volatile.Read(ref _t4Aim)).Append(" (bylby ").Append(Volatile.Read(ref _t4AimDry))
              .Append(", zostal przy walczacym obok ").Append(Volatile.Read(ref _t4AimKeep)).Append(", jezdziec 148/204 pominiety ").Append(Volatile.Read(ref _t4AimOwned)).Append(')')
              .Append(" | latka ").Append(_t4Patch).Append(" | potkniecia ").Append(_t4Stum).Append(" (postfiks ").Append(Volatile.Read(ref _t4PostErr)).Append(").");
            return sb.ToString();
        }
    }
}
