using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 372 (Jeff 09.11, obrona Goldentooth: "nie stoi jak glupi pod murami i nie uzywa maszyn"; "na zrzucie pokazalem ci ze drabina lezy a oni jak idioci biegna
    /// na mur"). Dwie latki na TacticBreachWalls (taktyka atakujacych AI), wpiete przy starcie gry (przed JIT wolajacych - Harmony/MonoMod wylacza wklejanie):
    ///  (1) ODWROT TAKTYKI tylko przez 371. Postfiks prywatnego ShouldRetreat(List&lt;SiegeLane&gt;, int) (TBW:116-125: RemainingPowerRatio / StartingPowerRatio
    ///      &lt; progu 0.6-1.0). W misji, w ktorej dziala SiegeBreak371 (gracz broni, wodz atakujacych AI), wynik zawsze false - takze po zalamaniu: odwrot
    ///      taktyki (StartTacticalRetreat - RetreatToKeep, MovementOrderRetreat -> CommonAIComponent.Retreat) RBM gasi kazdemu z morale &gt; 0
    ///      (rbmai.cs:2825-2827 StopRetreating -> natywne Agent.StopRetreating), a to zdejmuje tez natywny odwrot 371 - wiec dwa odwroty naraz nie moga isc.
    ///      Log 13-25-55: od t=2187 s 'Retreating', machiny porzucone, szyki staly 14-25 m od wroga ponad 15 minut. Jedna regula na jedno zjawisko:
    ///      o odwrocie decyduje prog strat 371. Pierwsze natywne true w misji - jeden wpis z danymi wskaznika (czemu gra chciala odstapic). Poza misja z 371
    ///      (gracz atakuje, bitwa AI, 371 wylaczone) - wynik gry bez zmian. Klucz SiegeAssaultHoldRetreat.
    ///  (2) DRABINY. Gra stawia drabiny tylko zachowaniem BehaviorUseSiegeMachines (waga 0.75); BehaviorWaitForLadders ma wage 1, dopoki
    ///      !TeamAISiegeComponent.AreLaddersReady i co tik zdejmuje swoj szyk z drabin (StopUsingMachine). TBW na koncu TickOccasionally ustawia
    ///      AreLaddersReady = (wylomow &gt; 1) || zadna machina pasa nie ma HoldLadders || jakas ma SendLadders; taran i wieza maja HoldLadders = !HasArrivedAtTarget
    ///      - wiec dopoki taran / wieza nie dojada pod mur, NIKT nie podnosi drabin (zamierzone uderzenie wszystkich machin naraz). Goldentooth: taran 6/12 ludzi
    ///      (0.6 m/s), wieza 6/7 (0.3 m/s), zadna nie dojechala; szyk drabin (133 ludzi) stal 6 m pod murem w WaitForLadders, drabiny 'OnLand 0/4'.
    ///      Postfiks TickOccasionally (po natywnym SetAreLaddersReady w tym samym wywolaniu): AreLaddersReady = true, gdy taktyka szturmuje
    ///      (AssaultUnderRangedCover / TotalAttack) i minelo SiegeLadderWaitSeconds od pierwszego tiku szturmu po rozstawieniu albo wszystkie tarany
    ///      i wieze ze startu sa zniszczone / nieczynne. Wtedy WaitForLadders ma wage 0, a UseSiegeMachines prowadzi szyk do drabin. Nie po zalamaniu 371
    ///      (wtedy atakujacy schodza z murow). Klucz SiegeLadderWaitSeconds (0 = jak w grze). Dotyczy kazdej taktyki szturmu AI (takze sojusznikow gracza).
    /// 'wyl' przy dwoch drabinach (log 370) to NIE blad: SiegeDeploymentHandler.RemoveDeploymentPoints wylacza (SetDisabledSynched) bron punktu rozstawienia,
    /// ktora nie zostala na nim postawiona - na tych punktach stoja taran / wieza; zostaja 2 czynne drabiny.
    /// Wyjatek per wywolanie = potkniecie (pierwsze 3 do logu), bez globalnego wylacznika.
    /// </summary>
    internal static class SiegeHold372
    {
        private const string P = "Szturm (372): ";
        private const int ErrMax = 3;
        private const int StAssault = 1, StTotal = 2;   // TacticBreachWalls.TacticState: Unset 0, AssaultUnderRangedCover 1, TotalAttack 2, Retreating 3
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static MethodInfo _mRetreat, _mTick;
        private static FieldInfo _fState, _fIndicators, _fAiSiege;
        private static int _stumbles, _errs;

        // stan misji (slaba referencja - skonczona misja nie zostaje w pamieci)
        private static WeakReference _mis;
        private static int _held, _released;
        private static bool _diagDone;

        private sealed class LadderClock
        {
            internal float StartAt = -1f;
            internal bool Released;
            internal readonly List<UsableMachine> Engines = new List<UsableMachine>();
        }
        private static readonly ConditionalWeakTable<TacticBreachWalls, LadderClock> _clocks = new ConditionalWeakTable<TacticBreachWalls, LadderClock>();
        private static readonly ConditionalWeakTable<TacticBreachWalls, LadderClock>.CreateValueCallback NewClock = k => new LadderClock();

        internal static bool HoldOn { get { var s = Settings.Current; return s != null && s.SiegeAssaultHoldRetreat; } }
        internal static int LadderWait { get { var s = Settings.Current; return s != null ? s.SiegeLadderWaitSeconds : 0; } }

        internal static void ApplyAll(Harmony h)
        {
            int ok = 0;
            try
            {
                _fState = AccessTools.Field(typeof(TacticBreachWalls), "_tacticState");
                _fIndicators = AccessTools.Field(typeof(TacticBreachWalls), "_indicators");
                _fAiSiege = AccessTools.Field(typeof(TacticBreachWalls), "_teamAISiegeAttacker");
                // obie metody zadeklarowane w samym TacticBreachWalls (DeclaredMethod - pulapka 15.10 nie dotyczy); Patch i ewentualny Unpatch tym samym MethodInfo
                _mRetreat = AccessTools.DeclaredMethod(typeof(TacticBreachWalls), "ShouldRetreat", new[] { typeof(List<SiegeLane>), typeof(int) });
                _mTick = AccessTools.DeclaredMethod(typeof(TacticBreachWalls), "TickOccasionally", Type.EmptyTypes);
                if (_mRetreat != null)
                {
                    h.Patch(_mRetreat, postfix: new HarmonyMethod(typeof(SiegeHold372), nameof(RetreatPostfix)));
                    ok++;
                }
                if (_mTick != null && _fState != null && _fAiSiege != null)
                {
                    h.Patch(_mTick, postfix: new HarmonyMethod(typeof(SiegeHold372), nameof(TickPostfix)));
                    ok++;
                }
            }
            catch (Exception e) { Log.Error("SiegeHold372.ApplyAll", e); }
            Log.Info(P + "latki TacticBreachWalls wpiete " + ok + "/2 (ShouldRetreat " + (_mRetreat != null ? "jest" : "BRAK") + ", TickOccasionally "
                     + (_mTick != null ? "jest" : "BRAK") + ", pola stan/wskaznik/AI " + (_fState != null ? "+" : "-") + (_fIndicators != null ? "+" : "-") + (_fAiSiege != null ? "+" : "-")
                     + ") | klucze: Siege Assault Hold Retreat " + (HoldOn ? "TAK" : "NIE") + ", Siege Ladder Wait Seconds " + LadderWait + ".");
        }

        /// <summary>Nowa misja - liczniki od zera.</summary>
        private static void Touch(Mission m)
        {
            var old = _mis != null ? _mis.Target as Mission : null;
            if (ReferenceEquals(old, m)) return;
            _mis = new WeakReference(m);
            _held = 0; _released = 0; _diagDone = false;
        }

        /// <summary>Dla linii konca misji 371: ile razy zablokowany odwrot taktyki i ile taktyk dostalo drabiny wczesniej.</summary>
        internal static string MissionSummary(Mission m)
        {
            var old = _mis != null ? _mis.Target as Mission : null;
            if (m == null || !ReferenceEquals(old, m)) return "372: odwrot taktyki zablokowany 0, drabiny zwolnione 0";
            return "372: odwrot taktyki zablokowany " + _held + ", drabiny zwolnione " + _released;
        }

        private static SiegeBreak371 Break371(Mission m)
        {
            try { return m != null ? m.GetMissionBehavior<SiegeBreak371>() : null; } catch { return null; }
        }

        // ------------------------------------------------------------ (1) odwrot taktyki

        private static void RetreatPostfix(TacticBreachWalls __instance, List<SiegeLane> lanes, int insideFormationCount, ref bool __result)
        {
            if (!__result) return;
            try
            {
                if (!HoldOn) return;
                var m = Mission.Current;
                var sb = Break371(m);
                if (sb == null || !sb.HoldsAssault) return;   // misja bez 371 - wynik gry
                Touch(m);
                __result = false;
                _held++;
                if (!_diagDone)
                {
                    _diagDone = true;
                    Log.Info(P + "taktyka atakujacych chciala odstapic (natywne ShouldRetreat) - ZABLOKOWANE, o odwrocie decyduje prog 371 | " + Diag(m, __instance, lanes, insideFormationCount)
                             + " | czas misji " + m.CurrentTime.ToString("0", Inv) + " s.");
                }
            }
            catch (Exception e) { Stumble("Retreat", e); }
        }

        /// <summary>Dane wskaznika TBW w chwili pierwszego natywnego true (tylko odczyt).</summary>
        private static string Diag(Mission m, TacticBreachWalls tac, List<SiegeLane> lanes, int inside)
        {
            var s = new System.Text.StringBuilder(256);
            try
            {
                var team = tac.Team;
                var q = team != null ? team.QuerySystem : null;
                float rem = q != null ? q.RemainingPowerRatio : -1f, tot = q != null ? q.TotalPowerRatio : -1f;
                s.Append("Remaining ").Append(rem.ToString("0.000", Inv)).Append(", Total ").Append(tot.ToString("0.000", Inv));
                object ind = _fIndicators != null ? _fIndicators.GetValue(tac) : null;
                if (ind != null)
                {
                    var it = ind.GetType();
                    float start = Convert.ToSingle(AccessTools.Field(it, "StartingPowerRatio").GetValue(ind), Inv);
                    int ilc = Convert.ToInt32(AccessTools.Field(it, "InitialLaneCount").GetValue(ind), Inv);
                    int iuc = Convert.ToInt32(AccessTools.Field(it, "InitialUnitCount").GetValue(ind), Inv);
                    float thr = Convert.ToSingle(AccessTools.Method(it, "GetRetreatThresholdRatio").Invoke(ind, new object[] { lanes, inside }), Inv);
                    s.Append(", Starting ").Append(start.ToString("0.000", Inv)).Append(" -> stosunek ").Append((start != 0f ? rem / start : 0f).ToString("0.000", Inv))
                     .Append(" < prog ").Append(thr.ToString("0.000", Inv)).Append(" | ILC ").Append(ilc).Append(", ludzi przy starcie wskaznika ").Append(iuc);
                }
                else s.Append(", wskaznik -");
                int open = 0, n = lanes != null ? lanes.Count : 0;
                for (int i = 0; i < n; i++) if (lanes[i] != null && lanes[i].IsOpen) open++;
                s.Append(" | pasy ").Append(n).Append(" (otwarte ").Append(open).Append(", zamkniete ").Append(n - open).Append("), szykow w srodku ").Append(inside);
                // strata mocy (CasualtyHandler) i ludzie / moc druzyn obu stron
                var ch = m.GetMissionBehavior<CasualtyHandler>();
                var bp = m.GetMissionBehavior<IBattlePowerCalculationLogic>();
                float lossA = 0f, lossO = 0f, powA = 0f, powO = 0f;
                int menA = 0, menO = 0;
                foreach (var t in m.Teams)
                {
                    if (!MissionTeams.Valid(t) || t.Side == BattleSideEnum.None) continue;
                    bool att = t.Side == BattleSideEnum.Attacker;
                    float p = 0f;
                    try { p = bp != null ? bp.GetTotalTeamPower(t) : 0f; } catch { p = -1f; }
                    if (att) powA += p; else powO += p;
                    foreach (var f in t.FormationsIncludingSpecialAndEmpty)
                    {
                        if (f == null) continue;
                        float l = ch != null ? ch.GetCasualtyPowerLossOfFormation(f) : 0f;
                        if (att) { lossA += l; menA += f.CountOfUnits; } else { lossO += l; menO += f.CountOfUnits; }
                    }
                }
                s.Append(" | moc druzyn (GetTotalTeamPower) A ").Append(powA.ToString("0", Inv)).Append(", O ").Append(powO.ToString("0", Inv))
                 .Append(" | strata mocy A ").Append(lossA.ToString("0.0", Inv)).Append(", O ").Append(lossO.ToString("0.0", Inv))
                 .Append(" | ludzi w szykach A ").Append(menA).Append(", O ").Append(menO);
            }
            catch (Exception e) { Stumble("Diag", e); s.Append(" | diagnoza ?"); }
            return s.ToString();
        }

        // ------------------------------------------------------------ (2) drabiny

        private static void TickPostfix(TacticBreachWalls __instance)
        {
            try
            {
                int wait = LadderWait;
                if (wait <= 0 || __instance == null) return;
                var m = Mission.Current;
                if (m == null || m.MissionEnded) return;
                int st = Convert.ToInt32(_fState.GetValue(__instance), Inv);
                if (st != StAssault && st != StTotal) return;                    // Unset albo Retreating - nic
                if (!m.IsDeploymentFinished && (m.Mode == MissionMode.Deployment || m.CurrentTime < 10f)) return;   // jak 371: po rozstawieniu (misja bez rozstawienia - po 10 s)
                var ai = _fAiSiege.GetValue(__instance) as TeamAISiegeComponent;
                if (ai == null) return;
                var c = _clocks.GetValue(__instance, NewClock);
                float now = m.CurrentTime;
                if (c.StartAt < 0f)
                {
                    c.StartAt = now;
                    var all = m.MissionObjects;
                    int n = all != null ? all.Count : 0;
                    for (int i = 0; i < n; i++)
                    {
                        try
                        {
                            var o = all[i];
                            if ((o is BatteringRam || o is SiegeTower) && !((UsableMachine)o).IsDestroyed && !((UsableMachine)o).IsDisabled) c.Engines.Add((UsableMachine)o);
                        }
                        catch (Exception e) { Stumble("Engines", e); }
                    }
                }
                if (ai.AreLaddersReady) return;                                   // gra sama zwolnila (machina dojechala, drabina w gorze, wylomy)
                var sb = Break371(m);
                if (sb != null && sb.IsBroken) return;                            // po zalamaniu 371 atakujacy schodza z murow
                bool timeUp = now - c.StartAt >= wait;
                bool enginesGone = false;
                if (!timeUp && c.Engines.Count > 0)
                {
                    enginesGone = true;
                    foreach (var x in c.Engines)
                    {
                        try { if (x != null && !x.IsDestroyed && !x.IsDeactivated) { enginesGone = false; break; } }
                        catch { }
                    }
                }
                if (!timeUp && !enginesGone) return;
                ai.SetAreLaddersReady(true);
                if (!c.Released)
                {
                    c.Released = true;
                    Touch(m);
                    _released++;
                    Log.Info(P + "drabiny zwolnione - " + (timeUp ? "minelo " + wait + " s szturmu (Siege Ladder Wait Seconds)" : "tarany i wieze stracone")
                             + ", taran / wieza jeszcze nie pod murem; szyki z drabinami przestaja czekac (WaitForLadders) i stawiaja drabiny | druzyna T"
                             + (__instance.Team != null ? __instance.Team.TeamIndex.ToString(Inv) : "?") + ", tarany i wieze na starcie " + c.Engines.Count
                             + " | czas misji " + now.ToString("0", Inv) + " s.");
                }
            }
            catch (Exception e) { Stumble("Ladders", e); }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("SiegeHold372." + where + " (potkniecie " + _stumbles + ")", e); } catch { }
        }
    }
}
